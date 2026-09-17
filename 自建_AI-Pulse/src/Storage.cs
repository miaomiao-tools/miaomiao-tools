using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIPulse;

public sealed class Storage
{
    public static readonly JsonSerializerOptions Json = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public string Root { get; }
    public string? Warning { get; private set; }
    public Storage(string? root = null)
    {
        Root = root ?? Path.Combine(AppContext.BaseDirectory, "data");
        try { Directory.CreateDirectory(Root); File.WriteAllText(Path.Combine(Root, ".writecheck"), ""); File.Delete(Path.Combine(Root, ".writecheck")); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An explicit test/import location must never silently fall back to a user's data.
            if (root != null) throw new IOException("指定的数据目录不可写，已停止操作。", ex);
            Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MiaomiaoTools", "AIPulse");
            Directory.CreateDirectory(Root); Warning = "程序目录无法写入，配置和历史改存于：" + Root;
        }
    }
    public Settings LoadSettings()
    {
        var s = Read<Settings>("settings.json") ?? new();
        s.TimeoutSeconds = Math.Clamp(s.TimeoutSeconds, 3, 60);
        s.IntervalSeconds = Math.Clamp(s.IntervalSeconds, 300, 3600);
        s.DisabledIds ??= []; s.CustomEndpoints ??= [];
        s.CustomEndpoints = s.CustomEndpoints.Where(e => e != null && !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Name) && ProbeEngine.ValidateUrl(e.Url) == null).DistinctBy(e => e.Id).Take(30).ToList();
        if (!Enum.IsDefined(s.Route)) s.Route = RouteMode.Gateway;
        if (ProbeEngine.ValidateUrl(s.ProxyUrl, true) != null) { s.ProxyUrl = "http://127.0.0.1:7890"; s.Route = RouteMode.Gateway; }
        return s;
    }
    public List<Round> LoadHistory() => (Read<List<Round>>("history.json") ?? []).Where(r => r != null && r.Results != null && r.Results.All(x => x != null)).TakeLast(240).ToList();
    private T? Read<T>(string file)
    {
        try { var path = Path.Combine(Root, file); return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : default; }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { Warning = $"无法读取 {file}，已使用默认值；原文件保留。"; return default; }
    }
    public bool Save<T>(string file, T value)
    {
        try
        {
            string path = Path.Combine(Root, file); File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(value, Json), Encoding.UTF8); File.Move(path + ".tmp", path, true); return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Warning = "保存失败，请检查数据文件夹空间和写入权限。"; return false; }
    }
    public static string Csv(IEnumerable<Round> rounds)
    {
        string Cell(object? value)
        {
            var s = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            // Protect spreadsheet users when custom names or URLs are exported.
            if (s.TrimStart().StartsWith('=') || s.TrimStart().StartsWith('+') || s.TrimStart().StartsWith('-') || s.TrimStart().StartsWith('@')) s = "'" + s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        var b = new StringBuilder("轮次时间,检测时间,网络模式,服务,地址,状态,HTTP,总耗时毫秒,DNS毫秒,TCP毫秒,实际连接主机,连接IP,最终域名,说明,检测范围,TLS毫秒,暂停至\r\n");
        foreach (var round in rounds) foreach (var r in round.Results)
            b.AppendLine(string.Join(",", new object?[] { round.Time.ToString("O"), r.Time.ToString("O"), round.Route, r.EndpointName, r.Url, r.Label, r.HttpCode, r.TotalMs is double d ? Math.Round(d) : null, r.DnsMs is double dn ? Math.Round(dn) : null, r.TcpMs is double t ? Math.Round(t) : null, r.DialHost, r.RemoteIp, r.FinalHost, r.Detail, Labels.Kind(r.Kind), r.TlsMs, r.CooldownUntil?.ToString("O") }.Select(Cell)));
        return b.ToString();
    }
}
