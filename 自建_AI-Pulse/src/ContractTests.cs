using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace AIPulse;

// Deterministic integration checks run only with --self-test <report.json>.
// No API credentials, paid endpoints, or persisted user settings are used.
public static class ContractTests
{
    public static async Task<int> RunAsync(string reportPath)
    {
        var tests = new List<object>(); int failed = 0;
        void Check(string name, bool pass, string? detail = null) { if (!pass) failed++; tests.Add(new { Name = name, Passed = pass, Detail = detail }); }
        void Code(string name, int status, ProbeState expected, string type = "application/json", string body = "{}", bool json = true, bool challenge = false)
        { var (actual, _) = ProbeEngine.Classify(status, type, body, json, challenge); Check(name, actual == expected, actual.ToString()); }
        Code("401 is reachable, authentication unverified", 401, ProbeState.Auth);
        Code("403 is restricted, not offline", 403, ProbeState.Restricted);
        Code("Gemini unregistered caller means auth required", 403, ProbeState.Auth, body: "{\"error\":\"unregistered callers\"}");
        Code("400 missing key means auth required", 400, ProbeState.Auth, body: "{\"error\":\"missing API key\"}");
        Code("404 requires path confirmation", 404, ProbeState.Unexpected);
        Code("405 does not imply complete usability", 405, ProbeState.Unexpected);
        Code("429 rate limiting", 429, ProbeState.RateLimited);
        Code("503 server or gateway error", 503, ProbeState.ServerError);
        Code("407 proxy authentication", 407, ProbeState.ProxyError);
        Code("200 JSON success", 200, ProbeState.Ok);
        Code("200 HTML on API is suspicious", 200, ProbeState.Unexpected, "text/html", "<html>Sign in</html>");
        Code("200 challenge page is restricted", 200, ProbeState.Restricted, "text/html", "<title>Just a moment</title>", false);
        Code("CF challenge header has priority", 403, ProbeState.Restricted, challenge: true);
        Check("Reject URL credentials", ProbeEngine.ValidateUrl("https://user:secret@example.com/") != null);
        Check("Reject query-string keys", ProbeEngine.ValidateUrl("https://example.com/?api_key=secret") != null);
        Check("Allow local Ollama endpoint", ProbeEngine.ValidateUrl("http://127.0.0.1:11434/api/tags") == null);
        Check("Allow SOCKS5 proxy", ProbeEngine.ValidateUrl("socks5://127.0.0.1:7891", true) == null);
        Check("Reject proxy path", ProbeEngine.ValidateUrl("http://127.0.0.1:7890/api", true) != null);
        Check("401 counts as HTTP reached", new ProbeResult { HttpCode = 401, State = ProbeState.Auth }.Reached);
        Check("407 does not count target reached", !new ProbeResult { HttpCode = 407, State = ProbeState.ProxyError }.Reached);
        Check("Cancellation excluded from alerts", !new ProbeResult { State = ProbeState.Cancelled }.NeedsAttention);
        Check("Even median is average", new Round { Results = [new() { HttpCode = 200, State = ProbeState.Ok, TotalMs = 100 }, new() { HttpCode = 401, State = ProbeState.Auth, TotalMs = 300 }] }.MedianMs == 200);
        Check("Timeout excluded from HTTP median", new Round { Results = [new() { State = ProbeState.Timeout, TotalMs = 12000 }, new() { HttpCode = 403, State = ProbeState.Restricted, TotalMs = 200 }] }.MedianMs == 200);
        var engine = new ProbeEngine();
        var settings = new Settings { TimeoutSeconds = 3, Route = RouteMode.Gateway };
        try
        {
            using var server = new LocalServer("HTTP/1.1 401 Unauthorized\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}");
            var r = await engine.ProbeAsync(server.Endpoint, settings, CancellationToken.None);
            Check("HTTP integration receives 401", r.State == ProbeState.Auth && r.HttpCode == 401, r.Detail);
            Check("Actual DNS TCP and response timings captured", r.DnsMs.HasValue && r.TcpMs.HasValue && r.TotalMs.HasValue && r.RemoteIp == "127.0.0.1");
            Check("Probe sends no bearer key", !server.Request.Contains("authorization:", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception e) { Check("HTTP integration", false, e.ToString()); }
        try
        {
            using var server = new LocalServer("HTTP/1.1 200 OK\r\nContent-Type: text/html\r\nContent-Length: 13\r\nConnection: close\r\n\r\n<html></html>");
            var r = await engine.ProbeAsync(server.Endpoint, settings, CancellationToken.None);
            Check("Portal HTML not reported as API success", r.State == ProbeState.Unexpected && r.Reached, r.Detail);
        }
        catch (Exception e) { Check("Portal integration", false, e.ToString()); }
        try
        {
            using var server = new LocalServer("", 6000);
            var watch = Stopwatch.StartNew(); var r = await engine.ProbeAsync(server.Endpoint, settings, CancellationToken.None);
            Check("Stalled connection has bounded timeout", r.State == ProbeState.Timeout && watch.Elapsed.TotalSeconds < 5, r.State.ToString());
        }
        catch (Exception e) { Check("Timeout integration", false, e.ToString()); }
        try
        {
            using var server = new LocalServer("", 6000); using var ct = new CancellationTokenSource(150);
            var watch = Stopwatch.StartNew(); var r = await engine.ProbeAsync(server.Endpoint, settings, ct.Token);
            Check("Cancellation is prompt and distinct from failure", r.State == ProbeState.Cancelled && watch.Elapsed.TotalSeconds < 2, r.State.ToString());
        }
        catch (Exception e) { Check("Cancellation integration", false, e.ToString()); }
        try
        {
            using var server = new LocalServer("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=local\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            var proxySettings = new Settings { TimeoutSeconds = 3, Route = RouteMode.Custom, ProxyUrl = server.Endpoint.Url.TrimEnd('/') };
            var endpoint = new Endpoint("proxy-test", "proxy test", "", "https://example.com/", "", "");
            var r = await engine.ProbeAsync(endpoint, proxySettings, CancellationToken.None);
            Check("Rejected CONNECT is proxy failure, not target success", r.State == ProbeState.ProxyError && !r.Reached, r.State + " " + r.Detail);
        }
        catch (Exception e) { Check("Proxy integration", false, e.ToString()); }
        try
        {
            using var closedPort = new TcpListener(IPAddress.Loopback, 0); closedPort.Start(); int port = ((IPEndPoint)closedPort.LocalEndpoint).Port; closedPort.Stop();
            var r = await engine.ProbeAsync(new("closed", "closed", "", $"http://127.0.0.1:{port}/", "", ""), settings, CancellationToken.None);
            Check("Refused TCP connection is not authentication error", r.State == ProbeState.ConnectError && !r.Reached, r.State.ToString());
        }
        catch (Exception e) { Check("Refused connection integration", false, e.ToString()); }
        try
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
            using var certificate = request.CreateSelfSigned(DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now.AddDays(1));
            using var server = new LocalServer("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\n\r\n{}", certificate: certificate);
            var secure = server.Endpoint with { Url = server.Endpoint.Url.Replace("http:", "https:") };
            var r = await engine.ProbeAsync(secure, settings, CancellationToken.None);
            Check("Untrusted TLS certificate is rejected", r.State == ProbeState.TlsError && !r.Reached, r.ErrorKind);
        }
        catch (Exception e) { Check("TLS integration", false, e.ToString()); }
        try
        {
            using var server = new LocalServer("HTTP/1.1 401 Unauthorized\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}", 150);
            var temp = Path.Combine(Path.GetDirectoryName(reportPath)!, "dashboard-test-" + Guid.NewGuid().ToString("N"));
            var store = new Storage(temp);
            store.Save("settings.json", new Settings { TimeoutSeconds = 3, DisabledIds = Catalog.All().Select(e => e.Id).ToList(), CustomEndpoints = [server.Endpoint with { IsCustom = true }] });
            using var dashboard = new Dashboard(store);
            var first = dashboard.RunAsync(); var duplicate = dashboard.RunAsync();
            await Task.WhenAll(first, duplicate);
            Check("Double-click cannot overlap rounds", dashboard.Rounds.Count == 1 && dashboard.CompletedCount == 1 && !dashboard.Running);
            dashboard.Search = "does-not-exist";
            Check("Search filters unmatched services", !dashboard.Services.Cast<object>().Any());
            dashboard.Search = ""; dashboard.OnlyIssues = true;
            Check("Successful transport is not included in issue filter", !dashboard.Services.Cast<object>().Any());
            dashboard.OnlyIssues = false;
            Check("Clearing filters restores the enabled service", dashboard.Services.Cast<object>().Count() == 1);
            Check("Completed round survives restart", new Storage(temp).LoadHistory().Count == 1);
            var changed = store.LoadSettings(); changed.Route = RouteMode.Custom;
            dashboard.UpdateSettings(changed);
            Check("Changing route clears current results and trend", dashboard.Current.Count == 0 && dashboard.ChartRounds.Count == 0 && dashboard.Rows.All(r => r.State == ProbeState.Waiting));
            dashboard.ClearHistory();
            Check("Clear history persists", new Storage(temp).LoadHistory().Count == 0);
        }
        catch (Exception e) { Check("Dashboard integration", false, e.ToString()); }
        try
        {
            string temp = Path.Combine(Path.GetDirectoryName(reportPath)!, "contract-test-data"); var store = new Storage(temp);
            var original = new Settings { Route = RouteMode.Custom, ProxyUrl = "socks5://127.0.0.1:7891", TimeoutSeconds = 20, DisabledIds = ["cursor"] };
            Check("Atomic settings write", store.Save("settings.json", original));
            var loaded = store.LoadSettings(); Check("Settings round trip", loaded.Route == original.Route && loaded.ProxyUrl == original.ProxyUrl && loaded.DisabledIds.Contains("cursor"));
            File.WriteAllText(Path.Combine(temp, "settings.json"), "{ broken");
            Check("Corrupt settings recover without crash", store.LoadSettings().Route == RouteMode.Gateway && store.Warning != null);
            var csv = Storage.Csv([new() { Results = [new() { EndpointName = "=DDE()", Detail = "a,\"b\"\nc", State = ProbeState.Auth, HttpCode = 401 }] }]);
            Check("CSV escapes spreadsheet formulas and quotes", csv.Contains("'=DDE()") && csv.Contains("a,\"\"b\"\"\nc"));
        }
        catch (Exception e) { Check("Storage integration", false, e.ToString()); }
        await SafetyTests.RunAsync(Check, Path.Combine(Path.GetDirectoryName(reportPath)!, "safety-" + Guid.NewGuid().ToString("N")));
        ReleaseTests.Run(Check, Path.Combine(Path.GetDirectoryName(reportPath)!, "release-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(new { Total = tests.Count, Failed = failed, Tests = tests }, Storage.Json));
        return failed;
    }

    private sealed class LocalServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource cts = new();
        private readonly Task serving;
        public Endpoint Endpoint { get; }
        public string Request { get; private set; } = "";
        public LocalServer(string response, int delay = 0, X509Certificate2? certificate = null)
        {
            listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Endpoint = new("local", "local", "", $"http://127.0.0.1:{port}/", "", "", true);
            serving = Task.Run(async () =>
            {
                try
                {
                    using var client = await listener.AcceptTcpClientAsync(cts.Token);
                    using var network = client.GetStream();
                    using var secure = certificate == null ? null : new SslStream(network, true);
                    if (secure != null) await secure.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = certificate }, cts.Token);
                    Stream stream = secure ?? (Stream)network;
                    var data = new byte[8192]; int length = 0;
                    while (length < data.Length)
                    {
                        int n = await stream.ReadAsync(data.AsMemory(length), cts.Token); if (n == 0) break; length += n;
                        Request = Encoding.ASCII.GetString(data, 0, length); if (Request.Contains("\r\n\r\n")) break;
                    }
                    if (delay > 0) await Task.Delay(delay, cts.Token);
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(response), cts.Token);
                }
                catch (Exception e) when (e is OperationCanceledException or IOException or SocketException or ObjectDisposedException or AuthenticationException) { }
            });
        }
        public void Dispose() { cts.Cancel(); listener.Stop(); serving.GetAwaiter().GetResult(); cts.Dispose(); }
    }
}
