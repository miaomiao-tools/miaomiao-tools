using System.IO;
using System.Text.Json;

namespace AIPulse;

public sealed class HostBudget
{
    public DateTimeOffset NextLight { get; set; }
    public DateTimeOffset NextHttp { get; set; }
    public DateTimeOffset BlockedUntil { get; set; }
    public int Restrictions { get; set; }
    public int Failures { get; set; }
}

/// <summary>Conservative per-host budgets shared by every path and network mode.</summary>
public sealed class ProbeGuard
{
    private readonly Storage storage;
    private readonly Func<DateTimeOffset> now;
    private readonly object gate = new();
    private Dictionary<string, HostBudget> budgets = new(StringComparer.OrdinalIgnoreCase);
    public bool Healthy { get; private set; } = true;
    public ProbeGuard(Storage storage, Func<DateTimeOffset>? clock = null)
    {
        this.storage = storage; now = clock ?? (() => DateTimeOffset.UtcNow);
        try
        {
            var path = Path.Combine(storage.Root, "cooldowns.json");
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<Dictionary<string, HostBudget>>(File.ReadAllText(path), Storage.Json) ?? throw new JsonException();
                if (loaded.Any(p => p.Value == null)) throw new JsonException();
                budgets = new(loaded, StringComparer.OrdinalIgnoreCase);
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { Healthy = false; }
    }
    private static string Key(Endpoint ep) => new Uri(ep.Url).IdnHost.TrimEnd('.').ToLowerInvariant();
    private HostBudget Get(Endpoint ep) { var key = Key(ep); if (!budgets.TryGetValue(key, out var value)) budgets[key] = value = new(); return value; }
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
    private bool Persist() => Healthy = Healthy && storage.Save("cooldowns.json", budgets);
    public bool TryReserve(Endpoint ep, ProbeKind kind, out ProbeResult? skipped)
    {
        lock (gate)
        {
            var b = Get(ep); var time = now();
            var until = Max(b.BlockedUntil, kind == ProbeKind.Light ? b.NextLight : b.NextHttp);
            if (!Healthy || until > time)
            {
                skipped = new() { EndpointId = ep.Id, EndpointName = ep.Name, Url = ep.Url, Time = time, Kind = kind, State = ProbeState.CoolingDown, CooldownUntil = Healthy ? until : null, Detail = Healthy ? $"该域名处于保护期，{until.ToLocalTime():MM-dd HH:mm:ss} 后可再试。本次没有连接目标，也没有发送 HTTP 请求；切换线路或重启不会清除保护期。" : "保护记录无法读取或保存，检测已暂停。请检查 data/cooldowns.json 的读写权限与完整性。" };
                return false;
            }
            // Reserve before network I/O so cancellation, crashes and restarts cannot bypass spacing.
            b.NextLight = time.AddSeconds(60);
            if (kind == ProbeKind.Http) b.NextHttp = time.AddMinutes(10);
            if (!Persist())
            {
                skipped = new() { EndpointId = ep.Id, EndpointName = ep.Name, Url = ep.Url, Kind = kind, State = ProbeState.CoolingDown, Detail = "无法持久保存保护记录，已取消本次连接。" };
                return false;
            }
            skipped = null; return true;
        }
    }
    public void Observe(Endpoint ep, ProbeResult result)
    {
        lock (gate)
        {
            if (!result.Attempted) return;
            var b = Get(ep); var time = now();
            if (result.State is ProbeState.Restricted or ProbeState.RateLimited || result.HttpCode is 403 or 429 or 451)
            {
                b.Restrictions = Math.Min(6, b.Restrictions + 1);
                double hours = Math.Min(24, Math.Pow(2, b.Restrictions - 1));
                b.BlockedUntil = Max(b.BlockedUntil, Max(time.AddHours(hours), result.RetryAfter ?? DateTimeOffset.MinValue));
            }
            else if (result.State == ProbeState.ServerError)
                b.BlockedUntil = Max(b.BlockedUntil, Max(time.AddMinutes(30), result.RetryAfter ?? DateTimeOffset.MinValue));
            else if (!result.Connected)
            {
                b.Failures = Math.Min(7, b.Failures + 1);
                b.NextLight = Max(b.NextLight, time.AddSeconds(Math.Min(3600, 60 * Math.Pow(2, b.Failures - 1))));
            }
            else b.Failures = 0;
            if (result.RetryAfter is DateTimeOffset serverDeadline && serverDeadline > time)
                b.BlockedUntil = Max(b.BlockedUntil, serverDeadline);
            if (b.BlockedUntil > time)
            {
                result.CooldownUntil = b.BlockedUntil;
                result.Detail += $" 已暂停该域名全部探测，最早 {b.BlockedUntil.ToLocalTime():MM-dd HH:mm:ss} 再试。";
            }
            Persist();
        }
    }
    public void ImportHistory(IEnumerable<Round> rounds)
    {
        lock (gate)
        {
            foreach (var r in rounds.SelectMany(r => r.Results).Where(r => r.Kind == ProbeKind.Http && r.Attempted && Uri.TryCreate(r.Url, UriKind.Absolute, out _)))
            {
                var ep = new Endpoint(r.EndpointId, r.EndpointName, "", r.Url, "", ""); var b = Get(ep);
                b.NextHttp = Max(b.NextHttp, r.Time.AddMinutes(10));
                if (r.State is ProbeState.Restricted or ProbeState.RateLimited || r.HttpCode is 403 or 429 or 451)
                {
                    b.Restrictions = Math.Max(1, b.Restrictions);
                    b.BlockedUntil = Max(b.BlockedUntil, Max(r.CooldownUntil ?? r.Time.AddHours(1), r.RetryAfter ?? DateTimeOffset.MinValue));
                }
            }
            Persist();
        }
    }
}
