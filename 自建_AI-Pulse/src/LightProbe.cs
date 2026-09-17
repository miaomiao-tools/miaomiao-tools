using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace AIPulse;

/// <summary>Opens a transport connection, then closes it without sending an HTTP request to the destination.</summary>
public sealed class LightProbe
{
    public async Task<ProbeResult> ProbeAsync(Endpoint endpoint, Settings settings, CancellationToken cancel)
    {
        var result = new ProbeResult { EndpointId = endpoint.Id, EndpointName = endpoint.Name, Url = endpoint.Url, Kind = ProbeKind.Light, Route = Dashboard.RouteKey(settings), Time = DateTimeOffset.Now };
        var total = Stopwatch.StartNew(); string stage = "DNS";
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        budget.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds)); var ct = budget.Token;
        Stream? stream = null;
        try
        {
            var target = new Uri(endpoint.Url);
            Uri? proxy = settings.Route switch
            {
                RouteMode.Custom => new Uri(settings.ProxyUrl),
                RouteMode.System => HttpClient.DefaultProxy.IsBypassed(target) ? null : HttpClient.DefaultProxy.GetProxy(target),
                _ => null
            };
            if (proxy == target) proxy = null;
            if (proxy != null && (!string.IsNullOrEmpty(proxy.UserInfo) || proxy.Scheme is not ("http" or "https" or "socks5")))
                throw new ProxyProbeException("轻量探测支持无认证的 HTTP、HTTPS 和 SOCKS5 代理。当前系统代理类型或认证方式不受支持。");
            var dial = proxy ?? target; result.DialHost = dial.IdnHost;
            ct.ThrowIfCancellationRequested();
            var watch = Stopwatch.StartNew();
            var addresses = await Dns.GetHostAddressesAsync(dial.IdnHost, ct).ConfigureAwait(false);
            result.DnsMs = watch.Elapsed.TotalMilliseconds;
            if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
            stage = "TCP"; watch.Restart();
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses, dial.Port, ct).ConfigureAwait(false);
                result.TcpMs = watch.Elapsed.TotalMilliseconds;
                var address = (socket.RemoteEndPoint as IPEndPoint)?.Address;
                result.RemoteIp = address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4().ToString() : address?.ToString() ?? "";
                stream = new NetworkStream(socket, true);
            }
            catch { socket.Dispose(); throw; }
            if (proxy != null)
            {
                stage = "代理隧道";
                if (proxy.Scheme == "https") stream = await SecureAsync(stream, proxy.IdnHost, result, ct).ConfigureAwait(false);
                if (proxy.Scheme == "socks5") await SocksTunnelAsync(stream, target, ct).ConfigureAwait(false);
                else await HttpTunnelAsync(stream, target, ct).ConfigureAwait(false);
            }
            if (target.Scheme == "https")
            {
                stage = "TLS"; watch.Restart();
                stream = await SecureAsync(stream, target.IdnHost, result, ct).ConfigureAwait(false);
                result.TlsMs = watch.Elapsed.TotalMilliseconds;
                result.Protocol = ((SslStream)stream).SslProtocol.ToString();
            }
            else result.Protocol = "TCP";
            result.State = ProbeState.TransportOk;
            result.FinalHost = target.IdnHost;
            result.Detail = target.Scheme == "https"
                ? "TCP 连接与 TLS 握手成功，已校验证书；没有向目标发送 HTTP 请求。仅说明基础连接建立，不代表网站、API、账号或模型生成可用。"
                : "仅建立了 TCP 连接，没有向目标发送 HTTP 请求。HTTP 地址不进行 TLS 验证，也未验证应用服务。";
            if (proxy != null) result.Detail += " DNS/TCP 计时对应代理主机；远端连接通过代理隧道建立。";
        }
        catch (OperationCanceledException)
        {
            result.State = cancel.IsCancellationRequested ? ProbeState.Cancelled : ProbeState.Timeout;
            result.Detail = cancel.IsCancellationRequested ? "检测已取消。" : $"在 {stage} 阶段超过 {settings.TimeoutSeconds} 秒。没有向目标发送 HTTP 请求。";
        }
        catch (ProxyProbeException ex) { result.State = ProbeState.ProxyError; result.Detail = ex.Message; result.ErrorKind = "ProxyTunnel"; }
        catch (Exception ex) when (ex is SocketException or IOException or AuthenticationException or HttpRequestException or ArgumentException or InvalidOperationException)
        {
            result.State = ex is SocketException s && s.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain
                ? ProbeState.DnsError : stage == "DNS" ? ProbeState.DnsError : stage == "TLS" ? ProbeState.TlsError : stage == "代理隧道" ? ProbeState.ProxyError : ProbeState.ConnectError;
            if (string.IsNullOrEmpty(result.ErrorKind)) result.ErrorKind = ex is SocketException se ? se.SocketErrorCode.ToString() : ex.GetType().Name;
            result.Detail = $"{stage} 阶段未完成：{result.ErrorKind}。没有向目标发送 HTTP 请求，可检查网关、代理、证书和系统时间后再试。";
        }
        finally { stream?.Dispose(); }
        if (result.State != ProbeState.Cancelled) result.TotalMs = total.Elapsed.TotalMilliseconds;
        return result;
    }
    private static async Task<SslStream> SecureAsync(Stream transport, string host, ProbeResult result, CancellationToken ct)
    {
        var ssl = new SslStream(transport, false, (_, _, chain, errors) =>
        {
            if (errors != SslPolicyErrors.None) result.ErrorKind = errors + " / " + string.Join(", ", chain?.ChainStatus.Select(s => s.Status.ToString()) ?? []);
            return errors == SslPolicyErrors.None;
        });
        try
        {
            await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = host, ApplicationProtocols = [SslApplicationProtocol.Http2, SslApplicationProtocol.Http11] }, ct).ConfigureAwait(false);
            return ssl;
        }
        catch { ssl.Dispose(); throw; }
    }
    private static async Task HttpTunnelAsync(Stream stream, Uri target, CancellationToken ct)
    {
        string host = target.HostNameType == UriHostNameType.IPv6 ? "[" + target.IdnHost.Trim('[', ']') + "]" : target.IdnHost;
        string authority = host + ":" + target.Port;
        byte[] request = Encoding.ASCII.GetBytes($"CONNECT {authority} HTTP/1.1\r\nHost: {authority}\r\nUser-Agent: AIPulse/1.1\r\n\r\n");
        await stream.WriteAsync(request, ct).ConfigureAwait(false);
        var bytes = new byte[16384]; int length = 0;
        while (length < bytes.Length)
        {
            await stream.ReadExactlyAsync(bytes.AsMemory(length, 1), ct).ConfigureAwait(false); length++;
            if (length >= 4 && bytes[length - 4] == 13 && bytes[length - 3] == 10 && bytes[length - 2] == 13 && bytes[length - 1] == 10) break;
        }
        var first = Encoding.ASCII.GetString(bytes, 0, length).Split('\n')[0].Trim().Split(' ');
        if (first.Length < 2 || !first[0].StartsWith("HTTP/") || !int.TryParse(first[1], out int code)) throw new ProxyProbeException("代理没有返回合法的 CONNECT 响应。");
        if (code != 200) throw new ProxyProbeException(code == 407 ? "代理需要认证，尚未建立目标连接。" : $"代理拒绝建立隧道（HTTP {code}），尚未验证目标连接。");
        if (length == bytes.Length) throw new ProxyProbeException("代理响应头过长，已停止建立连接。");
    }
    private static async Task SocksTunnelAsync(Stream stream, Uri target, CancellationToken ct)
    {
        await stream.WriteAsync(new byte[] { 5, 1, 0 }, ct).ConfigureAwait(false);
        var greeting = new byte[2]; await stream.ReadExactlyAsync(greeting, ct).ConfigureAwait(false);
        if (greeting[0] != 5 || greeting[1] != 0) throw new ProxyProbeException("SOCKS5 代理没有接受无认证连接。");
        var host = Encoding.ASCII.GetBytes(target.IdnHost);
        if (host.Length > 255) throw new ProxyProbeException("SOCKS5 目标主机名过长。");
        var request = new byte[7 + host.Length]; request[0] = 5; request[1] = 1; request[3] = 3; request[4] = (byte)host.Length;
        host.CopyTo(request, 5); request[^2] = (byte)(target.Port >> 8); request[^1] = (byte)target.Port;
        await stream.WriteAsync(request, ct).ConfigureAwait(false);
        var reply = new byte[4]; await stream.ReadExactlyAsync(reply, ct).ConfigureAwait(false);
        if (reply[0] != 5 || reply[1] != 0) throw new ProxyProbeException($"SOCKS5 代理未能建立目标连接（状态 {reply[1]}）。");
        int size = reply[3] switch { 1 => 4, 4 => 16, 3 => 0, _ => throw new ProxyProbeException("SOCKS5 代理返回了未知地址类型。") };
        if (reply[3] == 3) { var len = new byte[1]; await stream.ReadExactlyAsync(len, ct).ConfigureAwait(false); size = len[0]; }
        await stream.ReadExactlyAsync(new byte[size + 2], ct).ConfigureAwait(false);
    }
    private sealed class ProxyProbeException(string message) : Exception(message);
}
