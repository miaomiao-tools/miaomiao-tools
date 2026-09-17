using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace AIPulse;

/// <summary>Owns two views of one dashboard. Hiding a view never cancels or duplicates a scan.</summary>
public sealed class AppShell
{
    private readonly Storage storage;
    private readonly bool offscreen;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private bool switching, closing;
    private HwndSource? source;
    public Dashboard Model { get; }
    public MainWindow Full { get; }
    public MiniWindow? Mini { get; private set; }
    public WindowPreferences Preferences { get; }
    public bool IsMini => Preferences.MiniMode;
    public AppShell(Dashboard model, Storage storage, bool offscreen = false)
    {
        Model = model; this.storage = storage; this.offscreen = offscreen;
        Preferences = WindowPreferences.Load(storage);
        Full = new MainWindow(model);
        Configure(Full);
        Full.MiniRequested += ShowMini;
        Full.Closing += (_, _) =>
        {
            if (closing) return;
            closing = true; Remember(); Save(); saveTimer.Stop();
            Mini?.Close();
            source?.RemoveHook(ActivationHook);
        };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); Save(); };
        source = HwndSource.FromHwnd(new WindowInteropHelper(Full).EnsureHandle());
        source?.AddHook(ActivationHook);
    }
    private IntPtr ActivationHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x007E && !offscreen)
        {
            Full.Dispatcher.BeginInvoke(() =>
            {
                if (closing) return;
                Window active = IsMini && Mini != null ? Mini : Full;
                if (active.IsVisible && active.WindowState == WindowState.Normal) WindowLayout.Restore(active, WindowLayout.Capture(active));
            });
        }
        if ((uint)message != WindowLayout.ActivationMessage) return IntPtr.Zero;
        handled = true; int mode = wParam.ToInt32();
        Full.Dispatcher.BeginInvoke(() => { if (!closing) { if (mode == 1 || mode == 0 && IsMini) ShowMini(); else ShowFull(); } });
        return IntPtr.Zero;
    }
    private void Configure(Window window)
    {
        if (offscreen)
        {
            window.ShowInTaskbar = false; window.ShowActivated = false;
            window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -12000; window.Top = -12000;
        }
        window.LocationChanged += (_, _) => GeometryChanged(window);
        window.SizeChanged += (_, _) => GeometryChanged(window);
    }
    private void GeometryChanged(Window window)
    {
        if (switching || closing || !window.IsVisible || window.WindowState != WindowState.Normal) return;
        Capture(window); saveTimer.Stop(); saveTimer.Start();
    }
    private void Capture(Window window)
    {
        if (offscreen || !window.IsVisible || window.WindowState != WindowState.Normal) return;
        if (window == Full) Preferences.Full = WindowLayout.Capture(window);
        else Preferences.Mini = WindowLayout.Capture(window);
    }
    private void Remember()
    {
        Capture(Full); if (Mini != null) { Capture(Mini); Preferences.Pinned = Mini.Topmost; }
    }
    private void Save()
    {
        if (!storage.Save("window.json", Preferences)) Model.Notice = "窗口位置暂时无法保存，请检查数据目录。";
    }
    public void Start(bool? mini = null) { if (mini ?? Preferences.MiniMode) ShowMini(); else ShowFull(); }
    public void ShowMini()
    {
        if (closing) return;
        Remember(); switching = true;
        try
        {
            bool created = Mini == null;
            if (Mini == null)
            {
                Mini = new MiniWindow(Model) { Topmost = Preferences.Pinned };
                Configure(Mini);
                Mini.ExpandRequested += ShowFull;
                Mini.PinChanged += () => { Preferences.Pinned = Mini.Topmost; Save(); };
                Mini.Closed += (_, _) => { Mini = null; if (!closing) Full.Close(); };
            }
            var initial = Preferences.Mini ?? (Full.IsVisible ? WindowLayout.Beside(Full, Mini) : null);
            Preferences.MiniMode = true;
            Full.Hide();
            if (Mini.WindowState == WindowState.Minimized) Mini.WindowState = WindowState.Normal;
            Mini.Show();
            if (!offscreen && (created || initial != null)) WindowLayout.Restore(Mini, initial);
            if (!offscreen) Mini.Activate();
        }
        finally { switching = false; Remember(); Save(); }
    }
    public void ShowFull()
    {
        if (closing) return;
        Remember(); switching = true;
        try
        {
            Preferences.MiniMode = false; Mini?.Hide();
            if (Full.WindowState == WindowState.Minimized) Full.WindowState = WindowState.Normal;
            Full.Show();
            if (!offscreen && Full.WindowState == WindowState.Normal) WindowLayout.Restore(Full, Preferences.Full);
            if (!offscreen) Full.Activate();
        }
        finally { switching = false; Remember(); Save(); }
    }
}
