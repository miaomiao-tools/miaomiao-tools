using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace AIPulse;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Notify(name); return true; }
    public void Refresh() => Notify(string.Empty);
}

public enum ProbeState { Waiting, Running, Ok, Auth, Restricted, RateLimited, ServerError, Unexpected, DnsError, ConnectError, TlsError, Timeout, ProxyError, Cancelled, TransportOk, CoolingDown }
public enum ProbeKind { Http, Light }
public enum RouteMode { Gateway, System, Custom }

public sealed record Endpoint(string Id, string Name, string Category, string Url, string Mark, string Accent, bool ExpectsJson = false, string Note = "", bool IsCustom = false);

public sealed class ProbeResult
{
    public string EndpointId { get; set; } = "";
    public string EndpointName { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;
    public ProbeState State { get; set; }
    public ProbeKind Kind { get; set; } = ProbeKind.Http;
    public double? TlsMs { get; set; }
    public DateTimeOffset? RetryAfter { get; set; }
    public DateTimeOffset? CooldownUntil { get; set; }
    public int? HttpCode { get; set; }
    public double? DnsMs { get; set; }
    public double? TcpMs { get; set; }
    public double? TotalMs { get; set; }
    public string DialHost { get; set; } = "";
    public string RemoteIp { get; set; } = "";
    public string Route { get; set; } = "";
    public string Detail { get; set; } = "";
    public string ErrorKind { get; set; } = "";
    public string Protocol { get; set; } = "";
    public string FinalHost { get; set; } = "";
    public int Redirects { get; set; }
    [JsonIgnore] public bool Reached => HttpCode.HasValue && State != ProbeState.ProxyError;
    [JsonIgnore] public bool Connected => State == ProbeState.TransportOk || Reached;
    [JsonIgnore] public bool Attempted => State is not (ProbeState.Waiting or ProbeState.Running or ProbeState.Cancelled or ProbeState.CoolingDown);
    [JsonIgnore] public bool NeedsAttention => State is not (ProbeState.Ok or ProbeState.Auth or ProbeState.Waiting or ProbeState.Running or ProbeState.Cancelled or ProbeState.TransportOk or ProbeState.CoolingDown);
    [JsonIgnore] public string Label => Labels.State(State);
    public ProbeResult ForEndpoint(Endpoint ep) { var copy = (ProbeResult)MemberwiseClone(); copy.EndpointId = ep.Id; copy.EndpointName = ep.Name; copy.Url = ep.Url; return copy; }
}

public static class Labels
{
    public static string State(ProbeState value) => value switch
    {
        ProbeState.Waiting => "等待检测", ProbeState.Running => "检测中", ProbeState.Ok => "响应正常", ProbeState.Auth => "可达 · 需认证",
        ProbeState.Restricted => "访问受限", ProbeState.RateLimited => "请求限流", ProbeState.ServerError => "服务端异常",
        ProbeState.Unexpected => "响应待确认", ProbeState.DnsError => "DNS 失败", ProbeState.ConnectError => "连接失败",
        ProbeState.TlsError => "TLS 失败", ProbeState.Timeout => "连接超时", ProbeState.ProxyError => "代理异常", ProbeState.Cancelled => "已取消", ProbeState.TransportOk => "仅连接成功", ProbeState.CoolingDown => "保护暂停", _ => "未知"
    };
    public static string Color(ProbeState value) => value switch
    {
        ProbeState.Ok or ProbeState.TransportOk => "#087E65", ProbeState.Auth => "#176C94", ProbeState.Running => "#176C94",
        ProbeState.Restricted or ProbeState.RateLimited or ProbeState.Unexpected => "#9B6508",
        ProbeState.DnsError or ProbeState.ConnectError or ProbeState.TlsError or ProbeState.Timeout or ProbeState.ServerError or ProbeState.ProxyError => "#BC4747", _ => "#75808C"
    };
    public static string Background(ProbeState value) => value switch
    {
        ProbeState.Ok or ProbeState.TransportOk => "#E8F5EF", ProbeState.Auth or ProbeState.Running => "#E9F3FA",
        ProbeState.Restricted or ProbeState.RateLimited or ProbeState.Unexpected => "#FFF4DF",
        ProbeState.DnsError or ProbeState.ConnectError or ProbeState.TlsError or ProbeState.Timeout or ProbeState.ServerError or ProbeState.ProxyError => "#FBEDEC", _ => "#EEF1F5"
    };
    public static string Route(RouteMode route) => route switch { RouteMode.Gateway => "路由器网关", RouteMode.System => "系统 / 环境代理", _ => "指定代理" };
    public static string Kind(ProbeKind kind) => kind == ProbeKind.Light ? "轻量连接" : "HTTP 检测";
}

public sealed class EndpointRow(Endpoint endpoint) : Observable
{
    public Endpoint Endpoint { get; } = endpoint;
    public string Name => Endpoint.Name;
    public string Host => new Uri(Endpoint.Url).Host;
    public string Category => Endpoint.Category;
    public string Mark => Endpoint.Mark;
    public string Accent => Endpoint.Accent;
    public bool Enabled { get; set; } = true;
    public ObservableCollection<ProbeResult> History { get; } = [];
    public ProbeResult? Result { get; private set; }
    public ProbeState State { get; private set; } = ProbeState.Waiting;
    public string Label => Labels.State(State);
    public string StateColor => Labels.Color(State);
    public string StateBackground => Labels.Background(State);
    public string Latency => Result?.TotalMs is double ms ? $"{ms:N0}" : "···";
    public string Http => State == ProbeState.CoolingDown ? "本次未发请求" : Result?.HttpCode is int code ? $"HTTP {code}" : Result?.Kind == ProbeKind.Light ? (Result.State == ProbeState.TransportOk ? (Result.TlsMs.HasValue ? "TLS 握手成功" : "仅 TCP 已建立") : "轻量连接检测") : State == ProbeState.Waiting ? Category : "未收到 HTTP";
    public string Dns => Result?.DnsMs is double d ? $"{d:N0} ms" : "未完成";
    public string Tcp => Result?.TcpMs is double d ? $"{d:N0} ms" : "未完成";
    public string Note => Result?.Detail ?? Endpoint.Note;
    public string MiniName => Endpoint.Id switch
    {
        "codex" => "Codex / ChatGPT", "claude" => "Claude Code", "gemini" => "Gemini API", "copilot" => "Copilot", "openaiauth" => "OpenAI 登录", "claudeweb" => "Claude 网页", "geminiweb" => "Gemini 网页", _ => Name
    };
    public string MiniColor => State switch
    {
        ProbeState.TransportOk or ProbeState.Ok => "#90E5C3",
        ProbeState.Auth or ProbeState.Running => "#8FC8FF",
        ProbeState.Restricted or ProbeState.RateLimited or ProbeState.Unexpected => "#F6C478",
        ProbeState.DnsError or ProbeState.ConnectError or ProbeState.TlsError or ProbeState.Timeout or ProbeState.ServerError or ProbeState.ProxyError => "#FFAAA6",
        _ => "#A5B4C0"
    };
    public string MiniStatus => Label + (Result?.TotalMs is double ms ? $" · {ms:N0} ms" : "");
    public string MiniTooltip => $"{Name}\n{Endpoint.Url}\n{Note}";
    public string MiniAccessibleName => Name + "，" + MiniStatus;
    public void Start() { State = ProbeState.Running; Result = null; Refresh(); }
    public void Apply(ProbeResult result, bool add = true)
    {
        Result = result; State = result.State;
        if (add && result.Attempted) { History.Add(result); while (History.Count > 60) History.RemoveAt(0); }
        Refresh();
    }
    public void Reset() { Result = null; State = ProbeState.Waiting; Refresh(); }
}

public sealed class Settings
{
    public RouteMode Route { get; set; } = RouteMode.Gateway;
    public string ProxyUrl { get; set; } = "http://127.0.0.1:7890";
    public int TimeoutSeconds { get; set; } = 12;
    public int IntervalSeconds { get; set; } = 300;
    public List<string> DisabledIds { get; set; } = [];
    public List<Endpoint> CustomEndpoints { get; set; } = [];
}

public sealed class Round
{
    public ProbeKind Kind { get; set; } = ProbeKind.Http;
    public DateTimeOffset Time { get; set; } = DateTimeOffset.Now;
    public string Route { get; set; } = "";
    public bool Cancelled { get; set; }
    public List<ProbeResult> Results { get; set; } = [];
    [JsonIgnore] public int Reachable => Results.Count(r => r.Connected);
    [JsonIgnore] public int Attempted => Results.Count(r => r.Attempted);
    [JsonIgnore] public int Skipped => Results.Count(r => r.State == ProbeState.CoolingDown);
    [JsonIgnore] public int Attention => Results.Count(r => r.NeedsAttention);
    [JsonIgnore] public double? MedianMs { get { var a = Results.Where(r => r.Connected && r.TotalMs.HasValue).Select(r => r.TotalMs!.Value).Order().ToArray(); return a.Length == 0 ? null : a.Length % 2 == 1 ? a[a.Length / 2] : (a[a.Length / 2 - 1] + a[a.Length / 2]) / 2; } }
    [JsonIgnore] public string TimeLabel => Time.ToLocalTime().ToString("MM-dd HH:mm:ss");
    [JsonIgnore] public string Summary => $"{Labels.Kind(Kind)} · {Reachable}/{Attempted} 可达 · {Attention} 项需关注" + (Skipped > 0 ? $" · {Skipped} 项暂停" : "") + (Cancelled ? " · 部分完成" : "");
}

public static class Catalog
{
    public static List<Endpoint> All() => [
        new("codex", "Codex / ChatGPT", "登录入口", "https://chatgpt.com/", "Cx", "#182D30", false, "ChatGPT 登录入口的 HTTP 连通性；不验证 Codex 会话、WebSocket 或模型生成。"),
        new("openai", "OpenAI API", "模型 API", "https://api.openai.com/v1/models", "O", "#087E65", true, "Codex 使用 API Key 时的 API 域名。401 表示网络已通，需要认证。"),
        new("claude", "Claude Code / API", "模型 API", "https://api.anthropic.com/v1/models", "A", "#A1664B", true, "Anthropic 模型列表接口；不验证 Claude 账户、订阅或流式生成。"),
        new("gemini", "Gemini API", "模型 API", "https://generativelanguage.googleapis.com/v1beta/models", "G", "#326CCC", true, "Gemini API 域名；无密钥可能返回 400、401 或 403，按实际原因判断。"),
        new("copilot", "GitHub Copilot", "官方探针", "https://api.githubcopilot.com/_ping", "Gh", "#454C6A", false, "GitHub 官方网络排查探针；不验证 Copilot 订阅或补全响应。"),
        new("cursor", "Cursor", "API 域名", "https://api2.cursor.sh/", "Cu", "#2E4359", false, "Cursor API 域名探测。根路径返回 404 属于响应待确认；不代表 agent 完整可用。"),
        new("deepseek", "DeepSeek", "模型 API", "https://api.deepseek.com/models", "Ds", "#4462BA", true, "DeepSeek 模型列表接口，401 表示需要 API Key。"),
        new("openrouter", "OpenRouter", "公开模型目录", "https://openrouter.ai/api/v1/models", "Or", "#786097", true, "公开模型目录的连通性；目录正常不保证付费模型生成可用。"),
        new("openaiauth", "OpenAI 登录", "认证域名", "https://auth.openai.com/", "ID", "#4C7271", false, "登录认证域名；403 可能是网页人机校验，不能单独认定出口被封。"),
        new("claudeweb", "Claude 网页", "网页入口", "https://claude.ai/", "Cl", "#A1664B", false, "Claude 网页域名探测；网页和 API 可能使用不同的网络规则。"),
        new("geminiweb", "Gemini 网页", "网页入口", "https://gemini.google.com/", "Ge", "#326CCC", false, "Gemini 网页入口探测；不验证账户和地区权限。"),
        new("github", "GitHub API", "认证依赖", "https://api.github.com/", "Git", "#454C6A", true, "GitHub 公共 API，作为 Copilot 登录依赖的补充检查。")
    ];
}
