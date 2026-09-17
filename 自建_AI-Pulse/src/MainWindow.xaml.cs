using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace AIPulse;

public partial class MainWindow : Window
{
    public Dashboard Model { get; }
    public event Action? MiniRequested;
    public MainWindow(Dashboard model)
    {
        InitializeComponent(); Model = model; DataContext = model;
        Width = Math.Min(1400, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(900, SystemParameters.WorkArea.Height - 40);
        SizeChanged += (_, _) =>
        {
            MetricsPanel.Visibility = ActualHeight < 790 ? Visibility.Collapsed : Visibility.Visible;
            DetailColumn.Width = new GridLength(ActualWidth < 1200 ? 260 : 300);
        };
        PreviewKeyDown += async (_, e) => { if (e.Key == Key.F5) { e.Handled = true; await Model.ToggleRunAsync(); } else if (e.Key == Key.M && Keyboard.Modifiers == ModifierKeys.Control) { e.Handled = true; MiniRequested?.Invoke(); } };
        Closed += (_, _) => Model.Dispose();
    }
    private async void RunClick(object sender, RoutedEventArgs e) => await Model.ToggleRunAsync();
    private void MiniClick(object sender, RoutedEventArgs e) => MiniRequested?.Invoke();
    private async void HttpClick(object sender, RoutedEventArgs e) => await Model.RunHttpSelectedAsync();
    private void SettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(Model.Settings) { Owner = this, Height = Math.Min(780, SystemParameters.WorkArea.Height - 50) };
        if (dialog.ShowDialog() == true) Model.UpdateSettings(dialog.Value);
    }
    private void DashboardClick(object sender, RoutedEventArgs e) => ShowPage(false);
    private void HistoryClick(object sender, RoutedEventArgs e) => ShowPage(true);
    public void ShowPage(bool history)
    {
        HistoryPage.Visibility = history ? Visibility.Visible : Visibility.Collapsed;
        DashboardPage.Visibility = history ? Visibility.Collapsed : Visibility.Visible;
        var active = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#28434D"));
        DashboardNav.Background = history ? Brushes.Transparent : active; HistoryNav.Background = history ? active : Brushes.Transparent;
    }
    private void ExportClick(object sender, RoutedEventArgs e)
    {
        if (Model.Rounds.Count == 0 && Model.Current.Count == 0) { Model.Notice = "先完成一轮检测，再导出报告。"; return; }
        string reports = Path.Combine(AppContext.BaseDirectory, "reports");
        try { Directory.CreateDirectory(reports); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { reports = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); }
        var dialog = new SaveFileDialog { Title = "导出连通性报告", InitialDirectory = reports, FileName = "AI-Pulse-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), Filter = "Excel 可读 CSV 报告 (*.csv)|*.csv|完整 JSON 报告 (*.json)|*.json", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var rounds = Model.Rounds.Reverse().ToList();
            if (Model.Running && Model.Current.Count > 0) rounds.Add(new Round { Route = Model.RouteIdentity, Kind = Model.CurrentKind, Results = Model.Current, Cancelled = true });
            var text = dialog.FilterIndex == 1 ? Storage.Csv(rounds) : JsonSerializer.Serialize(new { App = "AI Pulse 1.2", Exported = DateTimeOffset.Now, Scope = "Light 仅 DNS/TCP/TLS；Http 为手动无认证 GET。均不验证实际 agent 生成。", Network = Model.Network, Rounds = rounds }, Storage.Json);
            File.WriteAllText(dialog.FileName, text, new UTF8Encoding(true)); Model.Notice = "报告已保存：" + dialog.FileName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Model.Notice = "导出失败：请换一个可以写入的文件夹。"; }
    }
    private void CopyClick(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(Model.SummaryText()); Model.Notice = "本轮摘要已复制。"; }
        catch (System.Runtime.InteropServices.COMException) { Model.Notice = "剪贴板正在使用，请稍后再试。"; }
    }
    private void ClearClick(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "清空本机保存的所有检测历史？", "清空历史", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) Model.ClearHistory();
    }
    private void HistoryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (HistoryList.SelectedItem is not Round round) return;
        string text = $"{round.Time:yyyy-MM-dd HH:mm:ss}\n线路：{round.Route}\n{round.Summary}\n\n" + string.Join("\n\n", round.Results.Select(r => $"{r.EndpointName}  ·  {Labels.Kind(r.Kind)}  ·  {r.Label}\nHTTP {r.HttpCode?.ToString() ?? (r.Kind == ProbeKind.Light ? "未请求" : "未收到响应")}  ·  总耗时 {r.TotalMs:N0} ms  ·  TLS {r.TlsMs:N0} ms\n{r.Url}\n{r.Detail}"));
        new Window { Owner = this, Title = "本轮详细记录", Width = 720, Height = Math.Min(680, SystemParameters.WorkArea.Height - 70), WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new(20), BorderThickness = new(0), Background = Brushes.Transparent } }.ShowDialog();
    }
    public static void SaveScreenshot(Window window, string path)
    {
        window.UpdateLayout();
        var visual = window.Content as FrameworkElement ?? window;
        var size = visual.LayoutTransform.TransformBounds(new Rect(visual.RenderSize)).Size;
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); png.Save(stream);
    }
}
