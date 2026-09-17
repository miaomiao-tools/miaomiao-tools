using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace AIPulse;

public sealed class ProbeEngine
{
    public static string? ValidateUrl(string value, bool proxy = false)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var u) || string.IsNullOrWhiteSpace(u.Host)) return "请输入完整地址，例如 https://example.com/v1/models。";
        if (proxy ? u.Scheme is not ("http" or "socks5") : u.Scheme is not ("http" or "https")) return proxy ? "代理支持 http:// 或 socks5://。" : "检测地址支持 http:// 或 https://。";
        if (!string.IsNullOrEmpty(u.UserInfo) || !string.IsNullOrEmpty(u.Query) || !string.IsNullOrEmpty(u.Fragment)) return "地址中请勿加入密码、密钥、查询参数或 # 片段。";
        if (proxy && u.AbsolutePath != "/") return "代理地址只填写主机和端口。";
        return null;
    }

    public async Task<ProbeResult> ProbeAsync(Endpoint endpoint, Settings settings, CancellationToken cancellation)
    {
        var result = new ProbeResult { EndpointId = endpoint.Id, EndpointName = endpoint.Name, Url = endpoint.Url, Route = Labels.Route(settings.Route), State = ProbeState.Running };
        var total = Stopwatch.StartNew();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        budget.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));
        var ct = budget.Token;
        try
        {
            using var handler = new SocketsHttpHandler
            {
                UseProxy = settings.Route != RouteMode.Gateway,
                Proxy = settings.Route == RouteMode.Custom ? new WebProxy(new Uri(settings.ProxyUrl)) : null,
                AllowAutoRedirect = false, UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All,
                ConnectTimeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
                MaxResponseHeadersLength = 64,
                SslOptions = new()
                {
                    RemoteCertificateValidationCallback = (_, certificate, chain, errors) =>
                    {
                        if (errors != SslPolicyErrors.None)
                            result.ErrorKind = "证书验证：" + errors + (chain == null ? "" : " / " + string.Join(", ", chain.ChainStatus.Select(s => s.Status.ToString()).Distinct()));
                        return errors == SslPolicyErrors.None;
                    }
                },
                PooledConnectionLifetime = TimeSpan.Zero,
                ConnectCallback = async (context, token) =>
                {
                    result.DialHost = context.DnsEndPoint.Host;
                    var watch = Stopwatch.StartNew();
                    var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, token).ConfigureAwait(false);
                    result.DnsMs = (result.DnsMs ?? 0) + watch.Elapsed.TotalMilliseconds;
                    if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
                    watch.Restart();
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, token).ConfigureAwait(false);
                        result.TcpMs = (result.TcpMs ?? 0) + watch.Elapsed.TotalMilliseconds;
                        var remote = (socket.RemoteEndPoint as IPEndPoint)?.Address;
                        result.RemoteIp = remote?.IsIPv4MappedToIPv6 == true ? remote.MapToIPv4().ToString() : remote?.ToString() ?? "";
                        return new NetworkStream(socket, true);
                    }
                    catch { socket.Dispose(); throw; }
                }
            };
            using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
            var uri = new Uri(endpoint.Url);
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri) { Version = HttpVersion.Version11, VersionPolicy = HttpVersionPolicy.RequestVersionExact };
                request.Headers.UserAgent.ParseAdd("AIPulse/1.1 (Windows; ConnectivityCheck)");
                request.Headers.CacheControl = new() { NoCache = true, NoStore = true };
                request.Headers.TryAddWithoutValidation("Accept", endpoint.ExpectsJson ? "application/json" : "text/html,application/json;q=0.9,*/*;q=0.8");
                if (uri.Host == "api.anthropic.com") request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                int code = (int)response.StatusCode;
                result.HttpCode = code;
                if (response.Headers.TryGetValues("Retry-After", out var retry)) result.RetryAfter = ParseRetryAfter(retry.FirstOrDefault(), DateTimeOffset.UtcNow);
                result.TotalMs = total.Elapsed.TotalMilliseconds;
                result.Protocol = $"HTTP/{response.Version}";
                result.FinalHost = uri.Host;
                string sample = "";
                bool bodyRead = true;
                try
                {
                    using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                    var bytes = new byte[8192]; int length = 0;
                    using var sampleBudget = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    sampleBudget.CancelAfter(TimeSpan.FromSeconds(2));
                    while (length < bytes.Length)
                    {
                        int n = await stream.ReadAsync(bytes.AsMemory(length), sampleBudget.Token).ConfigureAwait(false);
                        if (n == 0) break;
                        length += n;
                    }
                    sample = Encoding.UTF8.GetString(bytes, 0, length);
                }
                catch (Exception e) when (e is OperationCanceledException or IOException or HttpRequestException) { bodyRead = false; }
                bool challenge = response.Headers.TryGetValues("cf-mitigated", out var values) && values.Any(v => v.Contains("challenge"));
                (result.State, result.Detail) = Classify(code, response.Content.Headers.ContentType?.MediaType ?? "", sample, endpoint.ExpectsJson, challenge);
                if (!bodyRead)
                {
                    if (result.State == ProbeState.Ok && endpoint.ExpectsJson) result.State = ProbeState.Unexpected;
                    result.Detail += " 响应正文未完整读取，内容判断可能不完整。";
                }
                if (code is 301 or 302 or 303 or 307 or 308) result.Detail += " 为减少请求，本次没有跟随跳转。";
            }
        }
        catch (OperationCanceledException)
        {
            result.State = cancellation.IsCancellationRequested ? ProbeState.Cancelled : ProbeState.Timeout;
            result.Detail = result.State == ProbeState.Cancelled ? "本轮检测已停止。" : $"超过 {settings.TimeoutSeconds} 秒仍未完成。可切换网关节点后复测，或在设置中延长超时。";
            result.ErrorKind = result.State.ToString();
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or AuthenticationException or IOException or ArgumentException or InvalidOperationException)
        {
            var socket = Find<SocketException>(ex);
            var http = Find<HttpRequestException>(ex);
            result.State = http?.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => ProbeState.DnsError,
                HttpRequestError.SecureConnectionError => ProbeState.TlsError,
                HttpRequestError.ProxyTunnelError => ProbeState.ProxyError,
                _ => Find<AuthenticationException>(ex) != null ? ProbeState.TlsError : socket?.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain ? ProbeState.DnsError : ProbeState.ConnectError
            };
            if (string.IsNullOrEmpty(result.ErrorKind)) result.ErrorKind = http?.HttpRequestError.ToString() ?? socket?.SocketErrorCode.ToString() ?? ex.GetType().Name;
            result.Detail = result.State switch
            {
                ProbeState.DnsError => "实际连接目标的 DNS 解析失败。检查路由器 DNS、代理主机地址及网络连接。",
                ProbeState.TlsError => "安全连接握手或证书验证失败。检查系统时间、证书与代理的 HTTPS 处理。程序没有跳过证书验证。",
                ProbeState.ProxyError => "代理隧道没有建立。检查代理地址、端口和认证设置。",
                _ => result.TcpMs.HasValue ? "TCP 已建立，但 HTTPS 请求中断。可能涉及网关、远端或连接重置，请复测。" : "尚未建立 TCP 连接。检查网关节点、代理端口或网络。"
            };
            result.Detail += $" 错误类型：{result.ErrorKind}。";
        }
        if (result.TotalMs is null && result.State != ProbeState.Cancelled) result.TotalMs = total.Elapsed.TotalMilliseconds;
        return result;
    }

    private static T? Find<T>(Exception? e) where T : Exception { while (e != null) { if (e is T t) return t; e = e.InnerException; } return null; }

    public static DateTimeOffset? ParseRetryAfter(string? value, DateTimeOffset now)
    {
        if (long.TryParse(value, out var seconds))
            return seconds < 0 ? null : seconds >= (DateTimeOffset.MaxValue - now).TotalSeconds ? DateTimeOffset.MaxValue : now.AddSeconds(seconds);
        string[] formats = ["r", "dddd, dd-MMM-yy HH:mm:ss 'GMT'", "ddd MMM d HH:mm:ss yyyy", "ddd MMM  d HH:mm:ss yyyy"];
        if (DateTimeOffset.TryParseExact(value, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var date)) return date;
        return null;
    }

    public static (ProbeState State, string Detail) Classify(int code, string contentType, string sample, bool json, bool challenge = false)
    {
        string body = sample.ToLowerInvariant();
        bool humanCheck = challenge || body.Contains("<title>just a moment") || body.Contains("cf-chl-") || body.Contains("challenge-platform");
        bool missingAuth = body.Contains("unregistered callers") || body.Contains("api key not valid") || body.Contains("api_key_invalid") || body.Contains("missing api key") || body.Contains("api key is missing") || body.Contains("authentication_error") || body.Contains("unauthenticated");
        if (code == 407) return (ProbeState.ProxyError, "代理要求认证；尚不能确认目标服务是否可达。");
        if (humanCheck) return (ProbeState.Restricted, "收到了人机校验页面。网络已有响应，但自动探测受限；请在浏览器中确认登录和人机验证。");
        if (code == 401 || code is 400 or 403 && missingAuth) return (ProbeState.Auth, "网络已到达认证接口，当前请求未携带密钥或登录信息。账户权限、额度和生成能力尚未验证。");
        if (code == 403 || code == 451)
            return (ProbeState.Restricted, body.Contains("country") || body.Contains("region") || body.Contains("location") ? "服务返回地区或访问限制提示。请核对出口地区和该服务的可用范围。" : "服务拒绝了本次请求；可能是访问策略、权限或网页防护。仅凭此状态无法认定网关断网。");
        if (code == 429) return (ProbeState.RateLimited, "服务可以连接，但请求受频率或额度限制。暂停自动检测，稍后再试。");
        if (code >= 500) return (ProbeState.ServerError, "收到了服务端或中间网关的错误响应。网络并非完全不通；请结合其他服务与复测结果判断。");
        if (code >= 200 && code < 300)
        {
            if (json && (!contentType.Contains("json", StringComparison.OrdinalIgnoreCase) || !(sample.TrimStart().StartsWith('{') || sample.TrimStart().StartsWith('['))))
                return (ProbeState.Unexpected, "HTTP 成功，但响应内容不像预期的 JSON。可能是拦截页、认证门户或探针路径变化。");
            return (ProbeState.Ok, "已收到预期类型的 HTTP 响应。此结果只验证该探针，不验证登录账户、模型生成或长连接。");
        }
        return (ProbeState.Unexpected, $"收到了 HTTP {code} 响应；网络已有响应，但探针路径、请求方式或参数需进一步确认。");
    }
}

public sealed record NetworkSnapshot(string Adapter, string Gateways, string DnsServers, string GatewayPing, string Baseline, string ProxySummary, string Detail);

public static class NetworkDiagnostics
{
    public static async Task<NetworkSnapshot> CollectAsync(Settings settings, CancellationToken ct)
    {
        string adapters = "未发现活动网卡", gateways = "未发现", dns = "未发现", pingText = "未检测", proxy = Labels.Route(settings.Route);
        var gatewayIps = new List<IPAddress>();
        try
        {
            var cards = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback).Select(n => new { n.Name, Info = n.GetIPProperties() }).Where(n => n.Info.GatewayAddresses.Count > 0).ToList();
            if (cards.Count > 0)
            {
                adapters = string.Join(" / ", cards.Select(n => n.Name));
                gatewayIps = cards.SelectMany(n => n.Info.GatewayAddresses.Select(g => g.Address)).Where(a => !a.Equals(IPAddress.Any) && !a.Equals(IPAddress.IPv6Any)).Distinct().ToList();
                gateways = string.Join(", ", gatewayIps);
                dns = string.Join(", ", cards.SelectMany(n => n.Info.DnsAddresses).Distinct());
            }
            if (gatewayIps.Count > 0)
            {
                var checks = await Task.WhenAll(gatewayIps.Take(3).Select(async ip =>
                {
                    try { using var ping = new Ping(); var r = await ping.SendPingAsync(ip, TimeSpan.FromSeconds(2), cancellationToken: ct); return r.Status == IPStatus.Success ? $"{ip} · {r.RoundtripTime} ms" : $"{ip} · 未回应 ICMP"; }
                    catch (Exception e) when (e is PingException or OperationCanceledException or PlatformNotSupportedException) { return $"{ip} · ICMP 未完成"; }
                }));
                pingText = string.Join("；", checks);
            }
            if (settings.Route == RouteMode.System)
            {
                // Show only sanitized proxy targets. Never inspect or persist credentials.
                var target = new Uri("https://api.openai.com/");
                var p = HttpClient.DefaultProxy;
                proxy = p.IsBypassed(target) ? "OpenAI API：系统未选用代理" : $"OpenAI API：{p.GetProxy(target)?.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped)}";
            }
            else if (settings.Route == RouteMode.Custom) proxy = new Uri(settings.ProxyUrl).GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped);
        }
        catch (Exception e) when (e is NetworkInformationException or SocketException or InvalidOperationException or ArgumentException) { pingText = "网卡信息暂不可读取"; }
        return new(adapters, gateways, dns, pingText, "外网判断见服务连接结果", proxy, "此处只读取网卡信息并检查本地网关，不轮询公共网站。网关 ICMP 不回应不等于断网。多个网关时，以 Windows 实际路由为准。");
    }
}
