using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace AIPulse;

// All wire-level tests terminate at loopback listeners. They never contact AI services.
public static class SafetyTests
{
    public static async Task RunAsync(Action<string, bool, string?> check, string root)
    {
        Directory.CreateDirectory(root);
        void Check(string name, bool passed, string? detail = null) => check(name, passed, detail);
        Storage Store(string name) => new(Path.Combine(root, name));
        var time = new DateTimeOffset(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
        var initial = time;
        var ep = new Endpoint("one", "one", "", "https://test.invalid/a", "", "");
        try
        {
            var store = Store("budgets"); var guard = new ProbeGuard(store, () => time);
            Check("First light connection reserves a slot", guard.TryReserve(ep, ProbeKind.Light, out _));
            Check("Immediate light repeat is skipped", !guard.TryReserve(ep, ProbeKind.Light, out var paused) && paused?.State == ProbeState.CoolingDown && !paused.Attempted && !paused.NeedsAttention && !paused.Connected);
            var alternate = ep with { Url = "http://TEST.invalid.:8080/another-path" };
            Check("Host budget spans paths, ports, case and trailing dot", !guard.TryReserve(alternate, ProbeKind.Light, out _));
            Check("Different host has an independent budget", guard.TryReserve(ep with { Url = "https://other.invalid/" }, ProbeKind.Light, out _));
            Check("HTTP can explicitly follow a light check", guard.TryReserve(ep, ProbeKind.Http, out _));
            Check("HTTP repeat is blocked for ten minutes", !guard.TryReserve(ep, ProbeKind.Http, out paused) && paused?.CooldownUntil == time.AddMinutes(10));
            var restarted = new ProbeGuard(new Storage(store.Root), () => time);
            Check("Restart preserves reserved HTTP slot", !restarted.TryReserve(ep, ProbeKind.Http, out _));
            time = initial.AddSeconds(59);
            Check("Light cannot run before sixty seconds", !restarted.TryReserve(ep, ProbeKind.Light, out _));
            time = initial.AddSeconds(60);
            Check("Light can run at its deadline", restarted.TryReserve(ep, ProbeKind.Light, out _));
            time = initial.AddMinutes(10);
            Check("HTTP can run at its deadline", restarted.TryReserve(ep, ProbeKind.Http, out _));
            var restricted = new ProbeResult { State = ProbeState.Restricted, HttpCode = 403 };
            restarted.Observe(ep, restricted);
            Check("403 pauses the entire host for one hour", restricted.CooldownUntil == time.AddHours(1) && !restarted.TryReserve(ep, ProbeKind.Light, out _) && !restarted.TryReserve(ep, ProbeKind.Http, out _));
            time = restricted.CooldownUntil!.Value;
            Check("Host resumes when restriction expires", restarted.TryReserve(ep, ProbeKind.Http, out _));
            var again = new ProbeResult { State = ProbeState.RateLimited, HttpCode = 429 };
            restarted.Observe(ep, again);
            Check("Repeated restriction doubles the pause", again.CooldownUntil == time.AddHours(2));
            time = again.CooldownUntil!.Value;
            var longPause = new ProbeResult { State = ProbeState.RateLimited, HttpCode = 429, RetryAfter = time.AddDays(3) };
            restarted.Observe(ep, longPause);
            Check("Retry-After beyond a day is not truncated", longPause.CooldownUntil == time.AddDays(3));
            Check("Blocked response survives another restart", !new ProbeGuard(store, () => time).TryReserve(ep, ProbeKind.Light, out paused) && paused?.CooldownUntil == longPause.CooldownUntil);

            var auth403 = new ProbeGuard(Store("auth403"), () => time);
            var authResult = new ProbeResult { State = ProbeState.Auth, HttpCode = 403 };
            auth403.Observe(ep, authResult);
            Check("Auth-like 403 still receives conservative cooldown", authResult.CooldownUntil == time.AddHours(1));
            var challenge = new ProbeGuard(Store("challenge"), () => time);
            var challengeResult = new ProbeResult { State = ProbeState.Restricted, HttpCode = 200 };
            challenge.Observe(ep, challengeResult);
            Check("Challenge page with 200 pauses all probes", !challenge.TryReserve(ep, ProbeKind.Light, out _));
            var service = new ProbeGuard(Store("server"), () => time);
            var serviceResult = new ProbeResult { State = ProbeState.ServerError, HttpCode = 503, RetryAfter = time.AddHours(2) };
            service.Observe(ep, serviceResult);
            Check("503 honors a longer Retry-After", serviceResult.CooldownUntil == time.AddHours(2));
            var redirectGuard = new ProbeGuard(Store("redirect-budget"), () => time);
            var redirectResult = new ProbeResult { State = ProbeState.Unexpected, HttpCode = 302, RetryAfter = time.AddMinutes(20) };
            redirectGuard.Observe(ep, redirectResult);
            Check("Retry-After is also respected on other responses", redirectResult.CooldownUntil == time.AddMinutes(20));

            var failures = new ProbeGuard(Store("failures"), () => time);
            failures.TryReserve(ep, ProbeKind.Light, out _);
            for (int i = 0; i < 7; i++) failures.Observe(ep, new() { Kind = ProbeKind.Light, State = ProbeState.Timeout });
            Check("Repeated connection failure backs off to one hour", !failures.TryReserve(ep, ProbeKind.Light, out paused) && paused?.CooldownUntil == time.AddHours(1));
            var cancelled = new ProbeGuard(Store("cancelled"), () => time);
            cancelled.TryReserve(ep, ProbeKind.Http, out _);
            cancelled.Observe(ep, new() { State = ProbeState.Cancelled });
            Check("Cancel does not remove a reserved HTTP slot", !cancelled.TryReserve(ep, ProbeKind.Http, out _));

            var corruptStore = Store("corrupt");
            File.WriteAllText(Path.Combine(corruptStore.Root, "cooldowns.json"), "{ broken");
            var corrupt = new ProbeGuard(corruptStore, () => time);
            corrupt.ImportHistory([]);
            Check("Corrupt cooldown file fails closed and is retained", !corrupt.Healthy && !corrupt.TryReserve(ep, ProbeKind.Light, out _) && File.ReadAllText(Path.Combine(corruptStore.Root, "cooldowns.json")) == "{ broken");
            var unwritableStore = Store("write-failure");
            Directory.CreateDirectory(Path.Combine(unwritableStore.Root, "cooldowns.json.tmp"));
            var unwritable = new ProbeGuard(unwritableStore, () => time);
            Check("Failed reservation persistence prevents network permission", !unwritable.TryReserve(ep, ProbeKind.Http, out _) && !unwritable.Healthy);

            var migrationStore = Store("migration");
            migrationStore.Save("settings.json", new Settings { Route = RouteMode.System, IntervalSeconds = 60, DisabledIds = ["cursor"], CustomEndpoints = [ep] });
            var migrated = migrationStore.LoadSettings();
            Check("Old polling setting migrates without changing route or services", migrated.IntervalSeconds == 300 && migrated.Route == RouteMode.System && migrated.DisabledIds.Contains("cursor") && migrated.CustomEndpoints.Count == 1);
            var oldRound = JsonSerializer.Deserialize<Round>("{\"Results\":[{\"State\":\"Auth\",\"HttpCode\":401}]}", Storage.Json)!;
            Check("Old history remains classified as HTTP", oldRound.Kind == ProbeKind.Http && oldRound.Results[0].Kind == ProbeKind.Http);
            var imported = new ProbeGuard(Store("import"), () => time);
            imported.ImportHistory([new() { Results = [new() { Url = ep.Url, EndpointId = ep.Id, State = ProbeState.Restricted, HttpCode = 403, Time = time.AddMinutes(-5) }] }]);
            Check("Upgrade imports recent restrictions from old history", !imported.TryReserve(ep, ProbeKind.Light, out paused) && paused?.CooldownUntil == time.AddMinutes(55));
            Check("Retry-After delta seconds", ProbeEngine.ParseRetryAfter("7200", time) == time.AddHours(2));
            Check("Retry-After HTTP date", ProbeEngine.ParseRetryAfter(time.AddHours(2).ToString("r"), time) == time.AddHours(2));
            Check("Invalid and negative Retry-After are ignored", ProbeEngine.ParseRetryAfter("tomorrow", time) == null && ProbeEngine.ParseRetryAfter("-1", time) == null);
            Check("Huge Retry-After cannot overflow", ProbeEngine.ParseRetryAfter(long.MaxValue.ToString(), time) == DateTimeOffset.MaxValue);
            var lightResult = new ProbeResult { State = ProbeState.TransportOk, Kind = ProbeKind.Light, TotalMs = 30 };
            var round = new Round { Kind = ProbeKind.Light, Results = [lightResult, new() { State = ProbeState.CoolingDown }] };
            Check("Transport success is not misrepresented as HTTP success", lightResult.Connected && !lightResult.Reached && lightResult.HttpCode == null);
            Check("Skipped probes are excluded from attempts and latency", round.Attempted == 1 && round.Skipped == 1 && round.MedianMs == 30);
            var row = new EndpointRow(ep); row.Apply(new() { State = ProbeState.CoolingDown });
            Check("Cooldown does not add a failure strip sample", row.History.Count == 0);
        }
        catch (Exception e) { Check("Protection policy integration", false, e.ToString()); }

        var settings = new Settings { Route = RouteMode.Gateway, TimeoutSeconds = 3 };
        var light = new LightProbe(); var http = new ProbeEngine();
        try
        {
            using var server = new WireServer();
            var r = await light.ProbeAsync(server.Endpoint, settings, CancellationToken.None);
            await server.WaitForAsync(1);
            Check("Light TCP sends zero application bytes", r.State == ProbeState.TransportOk && server.Requests.Single().Length == 0 && r.HttpCode == null, r.Detail);
        }
        catch (Exception e) { Check("Zero HTTP light integration", false, e.ToString()); }
        try
        {
            using var target = new WireServer();
            using var redirect = new WireServer(response: $"HTTP/1.1 302 Found\r\nLocation: {target.Endpoint.Url}\r\nRetry-After: 7200\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            var r = await http.ProbeAsync(redirect.Endpoint, settings, CancellationToken.None);
            await redirect.WaitForAsync(1);
            Check("Manual HTTP does not follow redirects", r.HttpCode == 302 && target.Accepted == 0 && redirect.Accepted == 1);
            Check("Manual HTTP uses one anonymous GET", redirect.Requests.Single().StartsWith("GET / HTTP/1.1") && !redirect.Requests.Single().Contains("Cookie:", StringComparison.OrdinalIgnoreCase) && !redirect.Requests.Single().Contains("Authorization:", StringComparison.OrdinalIgnoreCase));
            Check("Retry-After is parsed from a real response", r.RetryAfter > DateTimeOffset.UtcNow.AddMinutes(119));
        }
        catch (Exception e) { Check("Bounded HTTP integration", false, e.ToString()); }
        try
        {
            string connect = ""; int tunneledBytes = -1;
            using var proxy = new WireServer(async (stream, ct) =>
            {
                connect = await ReadHeader(stream, ct);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n"), ct);
                tunneledBytes = await stream.ReadAsync(new byte[256], ct);
                return connect;
            });
            var remote = ep with { Url = "http://remote.invalid/path" };
            var r = await light.ProbeAsync(remote, new() { Route = RouteMode.Custom, ProxyUrl = proxy.Endpoint.Url.TrimEnd('/'), TimeoutSeconds = 3 }, CancellationToken.None);
            await proxy.WaitForAsync(1);
            Check("Light HTTP proxy only negotiates CONNECT", r.State == ProbeState.TransportOk && connect.StartsWith("CONNECT remote.invalid:80 HTTP/1.1") && tunneledBytes == 0, r.Detail);
            Check("Proxy route measures the proxy DNS and TCP", r.DialHost == "127.0.0.1" && r.DnsMs.HasValue && r.TcpMs.HasValue);
        }
        catch (Exception e) { Check("HTTP tunnel integration", false, e.ToString()); }
        try
        {
            using var proxy = new WireServer(response: "HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            var r = await light.ProbeAsync(ep, new() { Route = RouteMode.Custom, ProxyUrl = proxy.Endpoint.Url.TrimEnd('/'), TimeoutSeconds = 3 }, CancellationToken.None);
            Check("Light CONNECT rejection is not target success", r.State == ProbeState.ProxyError && !r.Connected && r.HttpCode == null);
        }
        catch (Exception e) { Check("Rejected tunnel integration", false, e.ToString()); }
        try
        {
            string domain = ""; int tunneledBytes = -1; bool noAuth = false;
            using var proxy = new WireServer(async (stream, ct) =>
            {
                var greeting = new byte[3]; await stream.ReadExactlyAsync(greeting, ct);
                noAuth = greeting.SequenceEqual(new byte[] { 5, 1, 0 });
                await stream.WriteAsync(new byte[] { 5, 0 }, ct);
                var header = new byte[5]; await stream.ReadExactlyAsync(header, ct);
                var host = new byte[header[4]]; await stream.ReadExactlyAsync(host, ct); domain = Encoding.ASCII.GetString(host);
                await stream.ReadExactlyAsync(new byte[2], ct);
                await stream.WriteAsync(new byte[] { 5, 0, 0, 1, 127, 0, 0, 1, 0, 80 }, ct);
                tunneledBytes = await stream.ReadAsync(new byte[256], ct);
                return domain;
            });
            var r = await light.ProbeAsync(ep with { Url = "http://remote.invalid/path" }, new() { Route = RouteMode.Custom, ProxyUrl = proxy.Endpoint.Url.Replace("http:", "socks5:").TrimEnd('/'), TimeoutSeconds = 3 }, CancellationToken.None);
            await proxy.WaitForAsync(1);
            Check("SOCKS5 uses remote DNS without HTTP or credentials", r.State == ProbeState.TransportOk && domain == "remote.invalid" && noAuth && tunneledBytes == 0, r.Detail);
        }
        catch (Exception e) { Check("SOCKS5 integration", false, e.ToString()); }
        try
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest("CN=localhost", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder(); san.AddIpAddress(IPAddress.Loopback); request.CertificateExtensions.Add(san.Build());
            using var cert = request.CreateSelfSigned(DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now.AddDays(1));
            bool authenticated = false;
            using var server = new WireServer(async (stream, ct) =>
            {
                using var tls = new SslStream(stream, true);
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = cert }, ct);
                authenticated = true;
                return await ReadHeader(tls, ct);
            });
            var r = await light.ProbeAsync(server.Endpoint with { Url = server.Endpoint.Url.Replace("http:", "https:") }, settings, CancellationToken.None);
            Check("Light TLS rejects an untrusted certificate", r.State == ProbeState.TlsError && !r.Connected && r.HttpCode == null, r.ErrorKind + "; server completed=" + authenticated);
        }
        catch (Exception e) { Check("Light TLS integration", false, e.ToString()); }
        try
        {
            using var server = new WireServer(async (stream, ct) => { await Task.Delay(6000, ct); return ""; });
            var secure = server.Endpoint with { Url = server.Endpoint.Url.Replace("http:", "https:") };
            using var cancel = new CancellationTokenSource(150);
            var r = await light.ProbeAsync(secure, settings, cancel.Token);
            Check("Light handshake cancellation remains cancellation", r.State == ProbeState.Cancelled && !r.Attempted);
            r = await light.ProbeAsync(secure, settings, CancellationToken.None);
            Check("Light handshake timeout stays bounded", r.State == ProbeState.Timeout && !r.Connected);
        }
        catch (Exception e) { Check("Light cancellation integration", false, e.ToString()); }
        try
        {
            using var one = new WireServer(); using var other = new WireServer();
            var a = one.Endpoint with { Id = "selected" }; var b = other.Endpoint with { Id = "other" };
            var store = Store("dashboard"); time = DateTimeOffset.UtcNow;
            store.Save("settings.json", new Settings { Route = RouteMode.Gateway, TimeoutSeconds = 3, DisabledIds = Catalog.All().Select(x => x.Id).ToList(), CustomEndpoints = [a, b] });
            using var dashboard = new Dashboard(store, () => time);
            await dashboard.RunHttpSelectedAsync(); await one.WaitForAsync(1);
            Check("Manual HTTP touches only the selected service", one.Accepted == 1 && other.Accepted == 0 && dashboard.Current.Count == 1 && dashboard.CurrentKind == ProbeKind.Http);
            Check("HTTP round labels scope explicitly", dashboard.Rounds.Single().Kind == ProbeKind.Http && dashboard.ChartRounds.Count == 1);
            await dashboard.RunHttpSelectedAsync();
            Check("Rapid HTTP clicks cannot create extra requests or chart failures", one.Accepted == 1 && dashboard.PausedCount == 1 && dashboard.Rounds.Count == 1);
            dashboard.ClearHistory(); await dashboard.RunHttpSelectedAsync();
            Check("Clearing history cannot reset HTTP protection", one.Accepted == 1 && dashboard.PausedCount == 1 && dashboard.Rounds.Count == 0);
            dashboard.Auto = true; time = time.AddSeconds(301);
            await dashboard.RunDueAsync(); await one.WaitForAsync(2);
            Check("Auto remains transport-only after a manual HTTP check", dashboard.CurrentKind == ProbeKind.Light && one.Requests.Last() == "" && one.Requests.Count(x => x.StartsWith("GET")) == 1);
            Check("Auto history and chart remain separate from HTTP", dashboard.Rounds.All(x => x.Kind == ProbeKind.Light) && dashboard.Current.All(x => x.Kind == ProbeKind.Light));
            dashboard.Auto = false;
            var updated = store.LoadSettings(); updated.Route = RouteMode.Custom; updated.ProxyUrl = "http://127.0.0.1:9";
            dashboard.UpdateSettings(updated); await dashboard.RunHttpSelectedAsync();
            Check("Changing proxy cannot reset per-host protection", dashboard.PausedCount == 1 && one.Accepted == 2);
        }
        catch (Exception e) { Check("Manual and auto policy integration", false, e.ToString()); }
        try
        {
            using var server = new WireServer(); var a = server.Endpoint;
            var store = Store("grouping");
            store.Save("settings.json", new Settings { TimeoutSeconds = 3, DisabledIds = Catalog.All().Select(x => x.Id).ToList(), CustomEndpoints = [a, a with { Id = "second-path", Url = a.Url + "models" }] });
            using var dashboard = new Dashboard(store);
            await dashboard.RunAsync(); await server.WaitForAsync(1);
            Check("Two paths on one target share exactly one light connection", server.Accepted == 1 && dashboard.ReachedCount == 2 && dashboard.Current.All(x => x.Kind == ProbeKind.Light));
            await dashboard.RunAsync();
            Check("Immediate repeat skips all paths without false outage history", dashboard.PausedCount == 2 && dashboard.AttentionCount == 0 && dashboard.Rounds.Count == 1 && server.Accepted == 1);
            Check("Fully paused dashboard does not display a zero success rate", dashboard.ReachedMetric == "—" && dashboard.Subline.Contains("没有连接目标"));
        }
        catch (Exception e) { Check("Grouped light policy integration", false, e.ToString()); }
    }

    private static async Task<string> ReadHeader(Stream stream, CancellationToken ct)
    {
        byte[] bytes = new byte[16384]; int length = 0;
        while (length < bytes.Length)
        {
            int count = await stream.ReadAsync(bytes.AsMemory(length, 1), ct);
            if (count == 0) break;
            length += count;
            if (length >= 4 && bytes[length - 4] == 13 && bytes[length - 3] == 10 && bytes[length - 2] == 13 && bytes[length - 1] == 10) break;
        }
        return Encoding.ASCII.GetString(bytes, 0, length);
    }
    private sealed class WireServer : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task serving;
        private readonly object gate = new();
        private readonly List<Task> clients = [];
        private readonly List<string> requests = [];
        private int accepted;
        public int Accepted => Volatile.Read(ref accepted);
        public List<string> Requests { get { lock (gate) return requests.ToList(); } }
        public Endpoint Endpoint { get; }
        public WireServer(Func<Stream, CancellationToken, Task<string>>? handler = null, string response = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: 2\r\nConnection: close\r\n\r\n{}")
        {
            listener.Start(); Endpoint = new("wire", "loopback", "", $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/", "", "", true, "", true);
            serving = Task.Run(async () =>
            {
                try
                {
                    while (!stop.IsCancellationRequested)
                    {
                        var client = await listener.AcceptTcpClientAsync(stop.Token); Interlocked.Increment(ref accepted);
                        var task = Task.Run(async () =>
                        {
                            using (client)
                            try
                            {
                                var stream = client.GetStream();
                                string request = handler != null ? await handler(stream, stop.Token) : await ReadHeader(stream, stop.Token);
                                lock (gate) requests.Add(request);
                                if (handler == null && request.Length > 0) await stream.WriteAsync(Encoding.UTF8.GetBytes(response), stop.Token);
                            }
                            catch (Exception e) when (e is OperationCanceledException or IOException or SocketException or ObjectDisposedException or AuthenticationException) { }
                        });
                        lock (gate) clients.Add(task);
                    }
                }
                catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException) { }
            });
        }
        public async Task WaitForAsync(int count)
        {
            var end = DateTimeOffset.UtcNow.AddSeconds(4);
            while (Requests.Count < count && DateTimeOffset.UtcNow < end) await Task.Delay(10);
            if (Requests.Count < count) throw new TimeoutException($"Expected {count} completed loopback connections, got {Requests.Count}.");
        }
        public void Dispose()
        {
            stop.Cancel(); listener.Stop(); serving.GetAwaiter().GetResult();
            Task[] tasks; lock (gate) tasks = clients.ToArray();
            Task.WhenAll(tasks).GetAwaiter().GetResult(); stop.Dispose();
        }
    }
}
