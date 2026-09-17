using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace AIPulse;

public sealed class HistoryStrip : FrameworkElement
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(HistoryStrip), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, Changed));
    public IEnumerable? Items { get => (IEnumerable?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (HistoryStrip)d;
        if (e.OldValue is INotifyCollectionChanged old) old.CollectionChanged -= c.OnCollection;
        if (e.NewValue is INotifyCollectionChanged current) current.CollectionChanged += c.OnCollection;
    }
    private void OnCollection(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();
    protected override void OnRender(DrawingContext dc)
    {
        var values = Items?.Cast<ProbeResult>().TakeLast(20).ToList() ?? [];
        double gap = 3, width = Math.Max(2, (ActualWidth - 19 * gap) / 20);
        for (int i = 0; i < 20; i++)
        {
            int index = i - (20 - values.Count);
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(index < 0 ? "#E9EEF2" : Labels.Color(values[index].State)));
            dc.DrawRoundedRectangle(brush, null, new(i * (width + gap), 1, width, Math.Max(2, ActualHeight - 2)), 2, 2);
        }
        ToolTip = values.Count == 0 ? "尚无记录。每格代表一次实际检测，最右侧为最新。保护暂停不计入。" : string.Join("\n", values.TakeLast(8).Select(r => $"{r.Time:HH:mm:ss}  {Labels.Kind(r.Kind)}  {r.Label}  {r.TotalMs:N0} ms"));
    }
}

public sealed class TrendChart : FrameworkElement
{
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IEnumerable), typeof(TrendChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public IEnumerable? Items { get => (IEnumerable?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    private void Text(DrawingContext dc, string value, double x, double y, string color = "#647383", double size = 11)
    {
        var t = new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(t, new(x, y));
    }
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth < 60 || ActualHeight < 50) return;
        var rounds = Items?.Cast<Round>().TakeLast(30).ToList() ?? [];
        double left = 41, top = 12, w = ActualWidth - left - 12, h = ActualHeight - 40;
        double max = Math.Max(500, Math.Ceiling((rounds.Select(r => r.MedianMs ?? 0).DefaultIfEmpty(0).Max()) / 500) * 500);
        var grid = new Pen(new SolidColorBrush(Color.FromRgb(231, 236, 241)), 1);
        for (int i = 0; i <= 2; i++)
        {
            double y = top + h * i / 2;
            dc.DrawLine(grid, new(left, y), new(left + w, y));
            Text(dc, (max * (2 - i) / 2).ToString("N0"), 0, y - 7);
        }
        var brush = new SolidColorBrush(Color.FromRgb(8, 126, 101));
        var line = new Pen(brush, 2.5);
        Point? previous = null;
        for (int i = 0; i < rounds.Count; i++)
        {
            double x = left + (rounds.Count < 2 ? w / 2 : w * i / (rounds.Count - 1));
            if (rounds[i].MedianMs is not double ms) { dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(188, 71, 71)), null, new(x, top + h), 4, 4); previous = null; continue; }
            var point = new Point(x, top + h * (1 - ms / max));
            if (previous is Point p) dc.DrawLine(line, p, point);
            dc.DrawEllipse(Brushes.White, line, point, 3, 3); previous = point;
        }
        if (rounds.Count == 0) Text(dc, "等待第一轮真实检测", left + 18, top + h / 2 - 7, "#7B8794", 12);
        else
        {
            Text(dc, rounds[0].Time.ToLocalTime().ToString("HH:mm"), left, top + h + 9);
            if (rounds.Count > 1) Text(dc, rounds[^1].Time.ToLocalTime().ToString("HH:mm"), left + w - 32, top + h + 9);
        }
    }
}
