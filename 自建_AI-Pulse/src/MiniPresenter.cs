using System.ComponentModel;
using System.Windows.Data;

namespace AIPulse;

/// <summary>A view over the existing dashboard, never a second probe scheduler.</summary>
public sealed class MiniPresenter : Observable, IDisposable
{
    public Dashboard Model { get; }
    public ListCollectionView Services { get; }
    public MiniPresenter(Dashboard model)
    {
        Model = model;
        Services = new ListCollectionView(model.Rows) { Filter = item => item is EndpointRow row && row.Enabled };
        Model.PropertyChanged += Changed;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName)) Services.Refresh();
        Refresh();
    }
    public string Count => Model.Current.Count == 0 ? "—" : Model.ReachedMetric;
    public string Scope => Model.CurrentKind == ProbeKind.Light ? "基础连接已建立" : "HTTP 已收到响应";
    public string Headline => Model.Running ? "正在检测" : Model.Current.Count == 0 ? "等待检测" : Model.Current.All(r => !r.Attempted) ? "保护暂停" : Model.AttentionCount > 0 ? "有项目需关注" : Model.PausedCount > 0 ? "部分项目暂停" : "连接检查完成";
    public string Accent => Model.Running ? "#8FC8FF" : Model.AttentionCount > 0 ? "#F6C478" : Model.Current.Count == 0 || Model.Current.All(r => !r.Attempted) ? "#ADBBC5" : "#90E5C3";
    public string Meta => Model.Current.Count == 0 ? $"已启用 {Model.EnabledCount} 项服务" : $"{Model.AttentionCount} 项需关注    ·    {Model.PausedCount} 项暂停";
    public string Route => Model.Settings.Route switch { RouteMode.Gateway => "路由器网关", RouteMode.System => "系统 / 环境代理", _ => "指定代理" };
    public string Recent => Model.Rounds.FirstOrDefault(r => r.Route == Model.RouteIdentity)?.Time.ToLocalTime().ToString("最近记录 HH:mm:ss") ?? "尚无记录";
    public string DetailTitle => Model.Selected == null ? "点选服务查看状态" : Model.Selected.MiniName + " · " + Model.Selected.Label;
    public string DetailLine => !string.IsNullOrEmpty(Model.Notice) ? Model.Notice : Model.Selected?.Result is not ProbeResult r ? "仅测网络连接，生成能力需在客户端确认。" : r.State == ProbeState.CoolingDown ? (r.CooldownUntil is DateTimeOffset until ? $"保护暂停至 {until.ToLocalTime():MM-dd HH:mm:ss}，本轮未连接。" : r.Detail) : $"{r.Time.ToLocalTime():HH:mm:ss} · {(r.Kind == ProbeKind.Light ? "仅 TCP / TLS；未验证应用响应。" : "HTTP 响应；未验证模型生成。")}";
    public string DetailTooltip => !string.IsNullOrEmpty(Model.Notice) ? Model.Notice : Model.SelectedDetails;
    public void Dispose() => Model.PropertyChanged -= Changed;
}
