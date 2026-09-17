using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AIPulse;

public sealed class SettingsDialog : Window
{
    public Settings Value { get; private set; }
    private readonly ComboBox route = new(), timeout = new(), interval = new();
    private readonly TextBox proxy = new(), customName = new(), customUrl = new();
    private readonly TextBlock error = new(), routeExplanation = new();
    private readonly WrapPanel endpoints = new();
    private readonly StackPanel customRows = new();
    private readonly Dictionary<string, CheckBox> choices = [];
    public SettingsDialog(Settings source)
    {
        Value = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(source, Storage.Json), Storage.Json)!;
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "检测设置 · AI Pulse"; Width = 750; Height = 780; MinWidth = 620; MinHeight = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel();
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(0,18,0,0) };
        var cancel = new Button { Content = "取消", IsCancel = true, Margin = new(0,0,10,0) };
        var save = new Button { Content = "保存设置", Style = (Style)Application.Current.FindResource("Primary"), MinWidth = 120 };
        save.Click += (_, _) => Save(); footer.Children.Add(cancel); footer.Children.Add(save); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var body = new StackPanel { Margin = new(0,0,12,0) }; scroll.Content = body; root.Children.Add(scroll);
        body.Children.Add(new TextBlock { Text = "让检测走对线路", FontSize = 24, FontWeight = FontWeights.SemiBold });
        body.Children.Add(Description("这里的设置只影响 AI Pulse，不会修改 Windows 或路由器的网络配置。"));
        Label(body, "网络模式");
        foreach (string name in new[] { "路由器网关（默认）", "系统 / 环境代理", "指定 HTTP / SOCKS5 代理" }) route.Items.Add(name);
        route.SelectedIndex = (int)Value.Route; body.Children.Add(route);
        route.SelectionChanged += (_, _) => UpdateRoute();
        routeExplanation.Style = (Style)Application.Current.FindResource("MutedText"); routeExplanation.FontSize = 12; routeExplanation.Margin = new(0,8,0,0); body.Children.Add(routeExplanation);
        Label(body, "指定代理地址"); proxy.Text = Value.ProxyUrl; body.Children.Add(proxy);
        body.Children.Add(Description("例如 http://127.0.0.1:7890 或 socks5://127.0.0.1:7891。此版本支持无认证代理。"));
        var timing = new Grid { Margin = new(0,12,0,0) }; timing.ColumnDefinitions.Add(new()); timing.ColumnDefinitions.Add(new());
        var t1 = new StackPanel { Margin = new(0,0,10,0) }; var t2 = new StackPanel { Margin = new(10,0,0,0) };
        Label(t1, "单项超时（秒）"); Label(t2, "自动轻量巡检间隔（秒）");
        foreach (int n in new[] { 5, 8, 12, 20, 30, 60 }) timeout.Items.Add(n);
        foreach (int n in new[] { 300, 600, 900, 1800, 3600 }) interval.Items.Add(n);
        timeout.IsEditable = interval.IsEditable = true; timeout.Text = Value.TimeoutSeconds.ToString(); interval.Text = Value.IntervalSeconds.ToString();
        t1.Children.Add(timeout); t2.Children.Add(interval); Grid.SetColumn(t2, 1); timing.Children.Add(t1); timing.Children.Add(t2); body.Children.Add(timing);
        body.Children.Add(Description("自动巡检只建立 TCP / TLS 连接，至少间隔 5 分钟，最多同时连接 2 个目标。从上一轮结束后计时。手动轻量检测同一域名至少间隔 60 秒。"));
        body.Children.Add(Description("HTTP 只检查手动选中的一项，每个域名至少间隔 10 分钟。遇到 403、429 或人机验证后暂停该域名全部探测至少 1 小时；遵守服务器要求的更长等待时间。冷却记录保留到下次启动。"));
        Label(body, "启用服务"); endpoints.Margin = new(0,4,0,0); body.Children.Add(endpoints);
        foreach (var ep in Catalog.All())
        {
            var choice = new CheckBox { Content = ep.Name, IsChecked = !Value.DisabledIds.Contains(ep.Id), Width = 205, ToolTip = ep.Url };
            choices[ep.Id] = choice; endpoints.Children.Add(choice);
        }
        Label(body, "自定义检测地址");
        body.Children.Add(Description("可添加其他 AI API、代理中转服务或本地 Ollama。轻量检测只连接主机；手动 HTTP 才会向此地址发送无认证 GET，请填写适合读取的地址。"));
        var addGrid = new Grid { Margin = new(0,8,0,8) }; addGrid.ColumnDefinitions.Add(new() { Width = new(140) }); addGrid.ColumnDefinitions.Add(new()); addGrid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        customName.ToolTip = "服务名称"; System.Windows.Automation.AutomationProperties.SetName(customName, "自定义服务名称"); customName.Margin = new(0,0,8,0);
        customUrl.ToolTip = "完整 HTTP 或 HTTPS 地址"; System.Windows.Automation.AutomationProperties.SetName(customUrl, "自定义检测地址"); customUrl.Margin = new(0,0,8,0);
        var add = new Button { Content = "添加" }; add.Click += (_, _) => AddEndpoint(); Grid.SetColumn(customUrl, 1); Grid.SetColumn(add, 2); addGrid.Children.Add(customName); addGrid.Children.Add(customUrl); addGrid.Children.Add(add); body.Children.Add(addGrid);
        body.Children.Add(customRows); RebuildCustom();
        error.TextWrapping = TextWrapping.Wrap; error.Foreground = new SolidColorBrush(Color.FromRgb(188,71,71)); error.Margin = new(0,12,0,0); body.Children.Add(error);
        body.Children.Add(Description("检测不会读取你的 Codex / Claude 登录信息，不会调用付费模型。HTTP 可达不代表账户、额度、流式生成或 WebSocket 已验证。"));
        UpdateRoute(); Content = new Border { Background = new SolidColorBrush(Color.FromRgb(243,246,249)), Padding = new Thickness(28), Child = root };
    }
    private static void Label(Panel parent, string text) => parent.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new(0,20,0,8) });
    private static TextBlock Description(string text) => new() { Text = text, Style = (Style)Application.Current.FindResource("MutedText"), FontSize = 12, Margin = new(0,8,0,0) };
    private void UpdateRoute()
    {
        proxy.IsEnabled = route.SelectedIndex == 2;
        routeExplanation.Text = route.SelectedIndex switch
        {
            0 => "适合路由器 / 旁路由上的梯子。跳过电脑上的系统和环境代理，仍会经过网关与 TUN。",
            1 => "使用 .NET / Windows 当前进程的代理选择。环境变量可能覆盖系统设置；修改系统代理后请重启本程序。不同 agent 可能使用自己的代理配置。",
            _ => "用指定代理对照网关线路。DNS 和 TCP 计时针对实际连接的代理主机；代理端解析远端域名不单独计时。"
        };
    }
    private void AddEndpoint()
    {
        var name = customName.Text.Trim(); var url = customUrl.Text.Trim();
        if (name.Length is < 1 or > 40) { error.Text = "服务名称需为 1–40 个字符。"; return; }
        if (ProbeEngine.ValidateUrl(url) is string invalid) { error.Text = invalid; return; }
        if (Value.CustomEndpoints.Count >= 30) { error.Text = "最多可添加 30 个自定义服务。"; return; }
        if (Catalog.All().Concat(Value.CustomEndpoints).Any(e => e.Url.Equals(url, StringComparison.OrdinalIgnoreCase))) { error.Text = "该地址已经在检测列表中。"; return; }
        Value.CustomEndpoints.Add(new(Guid.NewGuid().ToString("N"), name, "自定义地址", url, "+", "#536B79", false, "用户自定义的 GET 探针；不验证完整模型能力。", true));
        customName.Clear(); customUrl.Clear(); error.Text = ""; RebuildCustom();
    }
    private void RebuildCustom()
    {
        customRows.Children.Clear();
        foreach (var ep in Value.CustomEndpoints.ToList())
        {
            var panel = new DockPanel { Margin = new(0,4,0,4) };
            var delete = new Button { Content = "移除", FontSize = 11, Padding = new(10,5,10,5), MinHeight = 30 };
            delete.Click += (_, _) => { Value.CustomEndpoints.Remove(ep); Value.DisabledIds.Remove(ep.Id); choices.Remove(ep.Id); RebuildCustom(); };
            DockPanel.SetDock(delete, Dock.Right); panel.Children.Add(delete);
            if (!choices.TryGetValue(ep.Id, out var check)) choices[ep.Id] = check = new CheckBox { IsChecked = !Value.DisabledIds.Contains(ep.Id) };
            if (check.Parent is Panel oldParent) oldParent.Children.Remove(check);
            check.Content = ep.Name + "  ·  " + ep.Url; check.ToolTip = ep.Url;
            panel.Children.Add(check); customRows.Children.Add(panel);
        }
    }
    private void Save()
    {
        if (!int.TryParse(timeout.Text, out int t) || t is < 3 or > 60) { error.Text = "单项超时请输入 3–60 秒。"; return; }
        if (!int.TryParse(interval.Text, out int i) || i is < 300 or > 3600) { error.Text = "自动巡检间隔请输入 300–3600 秒。"; return; }
        if (route.SelectedIndex == 2 && ProbeEngine.ValidateUrl(proxy.Text.Trim(), true) is string invalid) { error.Text = invalid; return; }
        if (!choices.Values.Any(c => c.IsChecked == true)) { error.Text = "至少启用一个检测服务。"; return; }
        Value.Route = (RouteMode)route.SelectedIndex; Value.TimeoutSeconds = t; Value.IntervalSeconds = i;
        if (ProbeEngine.ValidateUrl(proxy.Text.Trim(), true) == null) Value.ProxyUrl = proxy.Text.Trim();
        Value.DisabledIds = choices.Where(kv => kv.Value.IsChecked != true).Select(kv => kv.Key).ToList();
        DialogResult = true;
    }
}
