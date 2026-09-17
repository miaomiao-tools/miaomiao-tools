using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace AIPulse;

public sealed class ActionCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
    public event EventHandler? CanExecuteChanged;
    public void Raise() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class Dashboard : Observable, IDisposable
{
    private readonly Storage storage;
    private readonly ProbeEngine engine = new();
    private readonly LightProbe light = new();
    private readonly ProbeGuard guard;
    private readonly Func<DateTimeOffset> now;
    private int targetCount;
    public ProbeKind CurrentKind { get; private set; } = ProbeKind.Light;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private CancellationTokenSource? cancellation;
    private DateTimeOffset? nextRun;
    public Settings Settings { get; private set; }
    public ObservableCollection<EndpointRow> Rows { get; } = [];
    public ObservableCollection<Round> Rounds { get; } = [];
    public ICollectionView Services { get; }
    private EndpointRow? selected;
    public EndpointRow? Selected { get => selected; set { if (Set(ref selected, value)) { Notify(nameof(SelectedDetails)); Notify(nameof(CanHttp)); Notify(nameof(ChartRounds)); Notify(nameof(ChartCaption)); } } }
    private bool running;
    public bool Running { get => running; private set { if (Set(ref running, value)) Refresh(); } }
    public bool Idle => !Running;
    public string RunLabel => Running ? "停止检测" : "轻量检测";
    public bool CanHttp => Idle && Selected?.Enabled == true;
    public string ScopeLabel => CurrentKind == ProbeKind.Light ? "TLS / TCP 可达" : "HTTP 可达";
    public string LatencyLabel => CurrentKind == ProbeKind.Light ? "连接耗时中位数" : "HTTP 响应中位数";
    public string ProtectionNote => "轻量巡检不读取网页内容；HTTP 仅手动单项，受限后自动暂停。";
    private bool auto;
    public bool Auto { get => auto; set { if (Set(ref auto, value)) { nextRun = value ? now().AddSeconds(Math.Max(300, Settings.IntervalSeconds)) : null; Refresh(); } } }
    private bool onlyIssues;
    public bool OnlyIssues { get => onlyIssues; set { if (Set(ref onlyIssues, value)) Services.Refresh(); } }
    private string search = "";
    public string Search { get => search; set { if (Set(ref search, value)) Services.Refresh(); } }
    private string banner = "轻量巡检已就绪";
    public string Banner { get => banner; private set => Set(ref banner, value); }
    private string subline = "只检查 DNS、TCP 与 TLS，不向目标发送 HTTP 请求。需要进一步确认时，再手动检查选中项。";
    public string Subline { get => subline; private set => Set(ref subline, value); }
    private string notice = "";
    public string Notice { get => notice; set => Set(ref notice, value); }
    public string DataPath => storage.Root;
    public string RouteName => Labels.Route(Settings.Route);
    public string RouteIdentity => RouteKey(Settings);
    public static string RouteKey(Settings s) => Labels.Route(s.Route) + (s.Route == RouteMode.Custom ? " · " + s.ProxyUrl : "");
    public string RouteHelp => Settings.Route == RouteMode.Gateway ? "不使用电脑上的应用代理；仍经过路由器网关及 TUN。" : Settings.Route == RouteMode.System ? "遵循此进程读取到的系统 / 环境代理与绕过规则。" : "通过指定代理建立连接；代理可能收到 CONNECT 请求。";
    public int EnabledCount => Rows.Count(r => r.Enabled);
    public List<ProbeResult> Current => Rows.Where(r => r.Enabled && r.Result != null && r.Result.State != ProbeState.Cancelled).Select(r => r.Result!).ToList();
    public int ReachedCount => Current.Count(r => r.Connected);
    public int PausedCount => Current.Count(r => r.State == ProbeState.CoolingDown);
    public int AttentionCount => Current.Count(r => r.NeedsAttention);
    public int CompletedCount => Current.Count;
    public double Progress => targetCount == 0 ? 0 : (double)CompletedCount / targetCount * 100;
    public string ReachedMetric => Current.Count > 0 && Current.All(r => !r.Attempted) ? "—" : $"{ReachedCount} / {(Current.Count > 0 ? Current.Count(r => r.Attempted) : targetCount == 0 ? EnabledCount : targetCount)}";
    public string AttentionMetric => AttentionCount.ToString();
    public string LatencyMetric => new Round { Results = Current }.MedianMs is double ms ? $"{ms:N0}" : "···";
    public string RoundMetric => Rounds.Count.ToString();
    public string Countdown => Running ? $"正在检测 {CompletedCount}/{targetCount}" : Auto && nextRun.HasValue ? $"{Math.Max(0, (int)(nextRun.Value - now()).TotalSeconds)} 秒后轻量巡检" : "自动巡检未开启";
    public string LastCheck => Rounds.FirstOrDefault()?.Time.ToLocalTime().ToString("最近记录  HH:mm:ss") ?? "尚无检测记录";
    public string OverallColor => AttentionCount > 0 || PausedCount > 0 ? "#9B6508" : Current.Count == 0 ? "#586B7B" : "#087E65";
    public string OverallBackground => AttentionCount > 0 || PausedCount > 0 ? "#FFF4DF" : Current.Count == 0 ? "#E9EFF5" : "#E8F5EF";
    public List<Round> ChartRounds => Rounds.Where(r => r.Route == RouteIdentity && r.Kind == CurrentKind && (CurrentKind == ProbeKind.Light || r.Results.Any(x => x.EndpointId == Selected?.Endpoint.Id)))
        .Reverse().TakeLast(30).Select(r => CurrentKind == ProbeKind.Light ? r : new Round { Kind = r.Kind, Time = r.Time, Route = r.Route, Cancelled = r.Cancelled, Results = r.Results.Where(x => x.EndpointId == Selected?.Endpoint.Id).ToList() }).ToList();
    public string ChartCaption => ChartRounds.Count == 0 ? $"{Labels.Kind(CurrentKind)} · 尚无记录" : $"{Labels.Kind(CurrentKind)} · 最近 {ChartRounds.Count} 轮 · 毫秒";
    public string Diagnosis
    {
        get
        {
            if (Current.Count == 0) return "默认只检测基础连接。自动巡检至少间隔 5 分钟，最多同时连接 2 个目标。手动 HTTP 检查每个域名至少间隔 10 分钟。";
            if (PausedCount == Current.Count) return "本轮均在保护期内，没有连接目标。暂停项不会被算作断网，等待冷却结束后再试。重启和切换代理不会清除冷却记录。";
            int failed = Current.Count(r => r.Attempted && !r.Connected);
            if (failed > 0) return $"有 {failed} 项基础连接未完成，可查看具体阶段。此结果不能判断是否被网站风控。重复失败会自动延长连接间隔。";
            if (CurrentKind == ProbeKind.Light) return "连接成功只说明 TCP 或 TLS 建立。没有访问网页/API 内容，不代表登录、接口调用或模型生成已验证；目标仍能看到连接 IP 和握手。";
            if (Current.Any(r => r.State == ProbeState.Restricted)) return "各项均有 HTTP 响应，但部分访问受限。网页可能触发人机校验；请打开对应客户端确认，不能仅凭 403 判断梯子失效。";
            if (AttentionCount > 0) return "网络已经有响应，但存在限流、服务错误或待确认的路径。点击对应服务查看原因，再决定是否切换节点。";
            return "手动 HTTP 探针已收到响应或认证提示。账号、额度与长连接仍需在客户端确认。请勿用重复探测尝试绕过访问限制。";
        }
    }
    private NetworkSnapshot? network;
    public NetworkSnapshot? Network { get => network; private set => Set(ref network, value); }
    public string SelectedDetails
    {
        get
        {
            if (Selected == null) return "选择左侧服务查看详细结果。";
            var r = Selected.Result;
            return r == null ? "轻量检测只检查此域名的连接。\n\n手动 HTTP 范围：" + Selected.Endpoint.Note : $"{Labels.Kind(r.Kind)}\n{r.Detail}\n\nDNS {Selected.Dns}  ·  TCP {Selected.Tcp}\nTLS {(r.TlsMs.HasValue ? $"{r.TlsMs:N0} ms" : "未验证 / 不适用")}\n实际连接：{r.DialHost}  {r.RemoteIp}\n{r.Protocol}  ·  {r.Time:HH:mm:ss}";
        }
    }

    public Dashboard(Storage storage, Func<DateTimeOffset>? clock = null)
    {
        this.storage = storage; now = clock ?? (() => DateTimeOffset.UtcNow); Settings = storage.LoadSettings();
        storage.Save("settings.json", Settings);
        foreach (var round in storage.LoadHistory().AsEnumerable().Reverse()) Rounds.Add(round);
        guard = new ProbeGuard(storage, now); guard.ImportHistory(Rounds);
        ReloadRows();
        Services = CollectionViewSource.GetDefaultView(Rows);
        Services.Filter = item => item is EndpointRow r && r.Enabled && (!OnlyIssues || r.Result?.NeedsAttention == true) && (string.IsNullOrWhiteSpace(Search) || (r.Name + " " + r.Host).Contains(Search, StringComparison.OrdinalIgnoreCase));
        Selected = Rows.FirstOrDefault(r => r.Enabled);
        Notice = storage.Warning ?? "";
        timer.Tick += Tick;
        timer.Start();
    }
    private void ReloadRows()
    {
        Rows.Clear();
        foreach (var endpoint in Catalog.All().Concat(Settings.CustomEndpoints).DistinctBy(e => e.Id))
        {
            var row = new EndpointRow(endpoint) { Enabled = !Settings.DisabledIds.Contains(endpoint.Id) };
            foreach (var r in Rounds.Where(r => r.Route == RouteIdentity).Reverse().SelectMany(r => r.Results).Where(r => r.EndpointId == endpoint.Id && r.Url == endpoint.Url && r.Attempted).TakeLast(60)) row.History.Add(r);
            Rows.Add(row);
        }
    }
    private async void Tick(object? sender, EventArgs e)
    {
        Notify(nameof(Countdown));
        await RunDueAsync();
    }
    public Task RunDueAsync() => Auto && !Running && nextRun <= now() ? RunAsync() : Task.CompletedTask;
    public async Task ToggleRunAsync() { if (Running) Stop(); else await RunAsync(); }
    public void Stop() { Auto = false; cancellation?.Cancel(); Banner = "正在停止检测"; }
    public Task RunAsync() => RunCoreAsync(ProbeKind.Light, Rows.Where(r => r.Enabled).ToList());
    public Task RunHttpSelectedAsync() => Selected?.Enabled == true ? RunCoreAsync(ProbeKind.Http, [Selected]) : Task.CompletedTask;
    private async Task RunCoreAsync(ProbeKind kind, List<EndpointRow> enabled)
    {
        if (Running) return;
        if (enabled.Count == 0) { Notice = "请在检测设置中至少启用一个服务。"; Auto = false; return; }
        CurrentKind = kind; targetCount = enabled.Count;
        Running = true; Notice = ""; Banner = kind == ProbeKind.Light ? "正在检查基础连接" : "正在手动检查选中项"; Subline = kind == ProbeKind.Light ? "只检查 DNS、TCP 和 TLS；不会向目标发送 HTTP 请求。" : "仅向选中的地址发送一次 GET，不携带账号或密钥，不跟随跳转。";
        cancellation = new CancellationTokenSource();
        var ct = cancellation.Token;
        var round = new Round { Route = RouteIdentity, Kind = kind, Time = now() };
        foreach (var row in Rows) row.Reset();
        Network = null;
        using var throttle = new SemaphoreSlim(2);
        try
        {
            // Start system-proxy discovery away from the dispatcher: PAC discovery can block.
            var networkTask = Task.Run(() => NetworkDiagnostics.CollectAsync(Settings, ct), ct);
            var groups = enabled.GroupBy(r => kind == ProbeKind.Light ? new Uri(r.Endpoint.Url).GetLeftPart(UriPartial.Authority).ToLowerInvariant() : r.Endpoint.Id);
            await Task.WhenAll(groups.Select(async group =>
            {
                var row = group.First();
                bool held = false;
                try
                {
                    await throttle.WaitAsync(ct); held = true;
                    ProbeResult result;
                    if (!guard.TryReserve(row.Endpoint, kind, out var skipped)) result = skipped!;
                    else
                    {
                        foreach (var member in group) member.Start(); Refresh();
                        result = await Task.Run(() => kind == ProbeKind.Light ? light.ProbeAsync(row.Endpoint, Settings, ct) : engine.ProbeAsync(row.Endpoint, Settings, ct), ct);
                        guard.Observe(row.Endpoint, result);
                    }
                    result.Route = round.Route;
                    foreach (var member in group)
                    {
                        var copy = result.ForEndpoint(member.Endpoint);
                        if (group.Count() > 1) copy.Detail += " 同一目标的多个地址共用本轮连接结果。";
                        member.Apply(copy);
                        if (copy.State != ProbeState.Cancelled) round.Results.Add(copy);
                    }
                    Services.Refresh(); Refresh();
                }
                catch (OperationCanceledException) { row.Reset(); }
                finally { if (held) throttle.Release(); }
            }));
            try { Network = await networkTask; } catch (OperationCanceledException) { }
            round.Cancelled = ct.IsCancellationRequested;
            if (round.Results.Any(r => r.Attempted))
            {
                round.Results = round.Results.OrderBy(r => enabled.FindIndex(e => e.Endpoint.Id == r.EndpointId)).ToList();
                Rounds.Insert(0, round); while (Rounds.Count > 240) Rounds.RemoveAt(Rounds.Count - 1);
                if (!storage.Save("history.json", Rounds.Reverse().ToList())) Notice = storage.Warning ?? "历史保存失败。";
            }
            Banner = ct.IsCancellationRequested ? "本轮检测已停止" : PausedCount == enabled.Count ? "保护期内，已跳过本轮" : AttentionCount > 0 ? $"{AttentionCount} 项值得关注" : $"{Labels.Kind(kind)}检查完成";
            Subline = ct.IsCancellationRequested ? "保留已完成结果，取消项不计入失败率。" : PausedCount == enabled.Count ? "本轮没有连接目标，未产生新的连通性结果。全部项目处于保护暂停。" : $"本轮实际检查 {Current.Count(r => r.Attempted)} 项，{ReachedCount} 项{(kind == ProbeKind.Light ? "基础连接建立；未验证 HTTP 或 agent 能力" : "收到 HTTP 响应")}。{(PausedCount > 0 ? $"{PausedCount} 项保护暂停。" : "")}";
        }
        catch (Exception ex)
        {
            Auto = false; Banner = "本轮检测未完成"; Notice = "检测出现异常：" + ex.GetType().Name + "。可以重新检测或调整设置。";
        }
        finally
        {
            foreach (var row in enabled.Where(r => r.State == ProbeState.Running)) row.Reset();
            Running = false; cancellation.Dispose(); cancellation = null;
            nextRun = Auto ? now().AddSeconds(Math.Max(300, Settings.IntervalSeconds)) : null;
            Refresh();
        }
    }
    public void UpdateSettings(Settings settings)
    {
        if (Running) return;
        settings.IntervalSeconds = Math.Clamp(settings.IntervalSeconds, 300, 3600); Settings = settings;
        if (!storage.Save("settings.json", Settings)) Notice = storage.Warning ?? "设置保存失败。";
        else Notice = "设置已保存，下次检测按新设置执行。";
        ReloadRows(); Selected = Rows.FirstOrDefault(r => r.Enabled); Services.Refresh(); Network = null;
        Banner = "设置已更新"; Subline = "点击开始检测，使用当前网络模式重新检查。";
        targetCount = 0; CurrentKind = ProbeKind.Light;
        nextRun = Auto ? now().AddSeconds(Settings.IntervalSeconds) : null; Refresh();
    }
    public void ClearHistory()
    {
        Rounds.Clear(); foreach (var row in Rows) row.History.Clear();
        Notice = storage.Save("history.json", new List<Round>()) ? "历史记录已清空。" : storage.Warning ?? "历史保存失败。";
        Refresh();
    }
    public string SummaryText() => $"AI Pulse 1.2 · {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}\n网络模式：{RouteIdentity}\n范围：{Labels.Kind(CurrentKind)}\n{Banner}\n{Subline}\n\n" + string.Join("\n", Current.Select(r => $"{r.EndpointName}：{r.Label} / {r.Protocol} / HTTP {r.HttpCode?.ToString() ?? "未请求"} / {r.TotalMs:N0} ms")) + "\n\n" + Diagnosis;
    public void Dispose() { timer.Stop(); Auto = false; cancellation?.Cancel(); }
}
