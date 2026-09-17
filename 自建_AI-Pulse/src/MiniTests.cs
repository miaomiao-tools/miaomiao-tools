using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace AIPulse;

public static class MiniTests
{
    public static async Task<int> RunAsync(string root)
    {
        Directory.CreateDirectory(root);
        var tests = new List<object>(); int failures = 0;
        void Check(string name, bool pass, object? detail = null) { if (!pass) failures++; tests.Add(new { Name = name, Passed = pass, Detail = detail }); }
        async Task Idle() => await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        var bindingErrors = new BindingTrace(); PresentationTraceSources.DataBindingSource.Listeners.Add(bindingErrors);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        try
        {
            var store = new Storage(Path.Combine(root, "data-" + Guid.NewGuid().ToString("N")));
            using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var endpoint = new Endpoint("local", "Local probe", "", $"http://127.0.0.1:{port}/", "+", "#536B79", false, "本机验证", true);
            store.Save("settings.json", new Settings { TimeoutSeconds = 3, DisabledIds = Catalog.All().Select(x => x.Id).ToList(), CustomEndpoints = [endpoint] });
            var model = new Dashboard(store);
            var shell = new AppShell(model, store, offscreen: true); shell.Start(false);
            await Idle();
            Check("Initial full window is visible and auto polling stays off", shell.Full.IsVisible && !model.Auto && !model.Running);
            WindowLayout.ActivateView(shell.Full, 1); await Idle(); await Task.Delay(30);
            Check("Shortcut activation message switches the existing instance to mini", shell.IsMini && shell.Mini?.IsVisible == true);
            WindowLayout.ActivateView(shell.Full, 2); await Idle(); await Task.Delay(30);
            Check("Full-view activation message restores the existing instance", !shell.IsMini && shell.Full.IsVisible);
            ((Button)shell.Full.FindName("MiniModeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Idle();
            Check("Full-panel mini button opens the mini window", shell.IsMini && shell.Mini?.IsVisible == true && !shell.Full.IsVisible);
            Check("Both windows share the original dashboard instance", ReferenceEquals(shell.Full.Model, shell.Mini?.Model));
            Check("Switching views does not start a probe", !model.Running && model.Rounds.Count == 0 && !listener.Pending());
            model.Search = "no such service"; model.OnlyIssues = true;
            Check("Mini services are independent of the full-panel search filter", shell.Mini!.Presenter.Services.Cast<object>().Count() == 1 && !model.Services.Cast<object>().Any());
            model.Search = ""; model.OnlyIssues = false;
            var toggle = (ToggleButton)shell.Mini.FindName("PinButton"); toggle.IsChecked = false;
            Check("Pin control updates native Topmost and persists preference", !shell.Mini.Topmost && !WindowPreferences.Load(store).Pinned);
            toggle.IsChecked = true;
            Check("Pin can be enabled again", shell.Mini.Topmost && WindowPreferences.Load(store).Pinned);
            ((ToggleButton)shell.Mini.FindName("AutoButton")).IsChecked = true;
            Check("Mini auto control uses the existing scheduler", model.Auto && !model.Running && !listener.Pending());
            var accepting = listener.AcceptTcpClientAsync();
            var running = model.RunAsync();
            shell.ShowFull(); shell.ShowMini(); shell.ShowFull(); shell.ShowMini();
            await running;
            using var client = await accepting.WaitAsync(TimeSpan.FromSeconds(4));
            int received = await client.GetStream().ReadAsync(new byte[128]).AsTask().WaitAsync(TimeSpan.FromSeconds(4));
            Check("Switching during a scan keeps one round and one TCP connection", model.Rounds.Count == 1 && model.CompletedCount == 1 && !listener.Pending() && received == 0);
            Check("Automatic polling remains active across view switches", model.Auto && !model.Running);
            Check("Mini metric updates from shared results", shell.Mini!.Presenter.Count == "1 / 1" && shell.Mini.Presenter.Headline == "连接检查完成");
            await model.RunAsync();
            Check("Mini retains the existing cooldown guard", model.PausedCount == 1 && model.Rounds.Count == 1 && !listener.Pending() && shell.Mini.Presenter.Count == "—");
            ((Button)shell.Mini.FindName("ExpandButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("Expand button restores the existing full panel", shell.Full.IsVisible && !shell.Mini.IsVisible && !shell.IsMini && model.Auto);
            shell.ShowMini();
            shell.Mini!.Close();
            Check("Closing mini also closes hidden full window and stops auto polling", !shell.Full.IsVisible && shell.Mini == null && !model.Auto);
            Check("Last mini mode is saved for next launch", WindowPreferences.Load(store).MiniMode);
            var restoredModel = new Dashboard(store);
            var restoredShell = new AppShell(restoredModel, store, offscreen: true); restoredShell.Start();
            Check("Restart restores mini mode and pin without enabling auto polling", restoredShell.IsMini && restoredShell.Mini?.Topmost == true && !restoredModel.Auto);
            restoredShell.ShowFull();
            restoredShell.Full.Close();
            Check("Closing full also disposes hidden mini view", restoredShell.Mini == null && !restoredModel.Auto);
        }
        catch (Exception e) { Check("Mini controls and lifecycle", false, e.ToString()); }
        try
        {
            var work = new WindowBounds(1920, 0, 1920, 1040);
            var saved = new WindowBounds(2100, 100, 400, 650);
            Check("Valid second-screen position is preserved", WindowLayout.Fit(saved, work, 340, 460) == saved);
            var negative = new WindowBounds(-1920, 0, 1920, 1040);
            Check("Negative monitor coordinates are preserved", WindowLayout.Fit(new(-1700, 100, 400, 650), negative, 340, 460).X == -1700);
            var offscreen = WindowLayout.Fit(new(6000, -4000, 400, 650), new(0, 0, 1920, 1040), 340, 460);
            Check("Removed monitor position is clamped into a working screen", offscreen.X >= 8 && offscreen.Y >= 8 && offscreen.X + offscreen.Width <= 1912 && offscreen.Y + offscreen.Height <= 1032);
            var oversized = WindowLayout.Fit(new(0, 0, 2000, 1800), new(0, 0, 1280, 720), 340, 460);
            Check("Oversized saved window fits the available work area", oversized.Width == 1264 && oversized.Height == 704);
            var tiny = WindowLayout.Fit(new(0, 0, 400, 650), new(0, 0, 320, 400), 340, 460);
            Check("Small work areas do not throw while fitting", tiny.Width == 304 && tiny.Height == 384);
            var store = new Storage(Path.Combine(root, "placement"));
            store.Save("window.json", new WindowPreferences { MiniMode = true, Pinned = false, Mini = saved, Full = new(100, 50, 1400, 900) });
            var read = WindowPreferences.Load(store);
            Check("Window preferences round trip separately from probe settings", read.MiniMode && !read.Pinned && read.Mini == saved && !File.Exists(Path.Combine(store.Root, "settings.json")));
            File.WriteAllText(Path.Combine(store.Root, "window.json"), "{ broken");
            Check("Corrupt window preferences recover without breaking startup", !WindowPreferences.Load(store).MiniMode);
            using var model = new Dashboard(new Storage(Path.Combine(root, "native-placement")));
            var hidden = new MiniWindow(model) { ShowInTaskbar = false, ShowActivated = false };
            WindowLayout.Restore(hidden, new(80000, -60000, 400, 650));
            var actual = WindowLayout.Capture(hidden);
            Check("Native restore recovers an offscreen hidden window", actual?.Valid == true && actual.X != 80000 && actual.Y != -60000 && !hidden.IsVisible);
            store.Save("window.json", new WindowPreferences { MiniMode = true, Mini = actual });
            var second = new MiniWindow(model) { ShowInTaskbar = false, ShowActivated = false };
            WindowLayout.Restore(second, WindowPreferences.Load(store).Mini);
            Check("Saved physical bounds restore correctly into a new native window", WindowLayout.Capture(second) == actual && !second.IsVisible);
            second.Close();
            hidden.Close();
        }
        catch (Exception e) { Check("Window placement integration", false, e.ToString()); }
        try
        {
            // Deliberately labelled visual fixtures: no external requests or fabricated live results.
            var store = new Storage(Path.Combine(root, "visual-data"));
            var model = new Dashboard(store); var shell = new AppShell(model, store, offscreen: true); shell.Start(true);
            var mini = shell.Mini!;
            await Idle();
            MainWindow.SaveScreenshot(mini, Path.Combine(root, "01-mini-ready.png"));
            var states = new[] { ProbeState.TransportOk, ProbeState.TransportOk, ProbeState.TransportOk, ProbeState.CoolingDown, ProbeState.TransportOk, ProbeState.Timeout, ProbeState.TransportOk, ProbeState.TransportOk, ProbeState.TransportOk, ProbeState.CoolingDown, ProbeState.TransportOk, ProbeState.TransportOk };
            int index = 0; var time = DateTimeOffset.Now;
            foreach (var row in model.Rows)
            {
                var state = states[index % states.Length]; index++;
                row.Apply(new() { EndpointId = row.Endpoint.Id, EndpointName = row.Name, Url = row.Endpoint.Url, Kind = ProbeKind.Light, State = state, Time = time, TlsMs = state == ProbeState.TransportOk ? 40 : null, TotalMs = state == ProbeState.TransportOk ? 120 + index * 27 : null, CooldownUntil = state == ProbeState.CoolingDown ? time.AddHours(1) : null, Detail = "界面验证用模拟状态，未进行外网探测。" }, false);
            }
            model.Notice = "界面预览 · 模拟状态，未访问外网"; model.Refresh(); await Idle();
            MainWindow.SaveScreenshot(mini, Path.Combine(root, "02-mini-preview.png"));
            Check("All twelve enabled services remain present in mini view", mini.Presenter.Services.Cast<object>().Count() == 12);
            mini.Width = 340; mini.Height = 460; await Idle();
            MainWindow.SaveScreenshot(mini, Path.Combine(root, "03-mini-small.png"));
            Check("Small layout retains primary controls", ((Button)mini.FindName("RunButton")).ActualHeight >= 40 && ((Button)mini.FindName("ExpandButton")).ActualWidth >= 40);
            mini.Width = 500; mini.Height = 700; await Idle();
            MainWindow.SaveScreenshot(mini, Path.Combine(root, "04-mini-large.png"));
            // Exercise WPF logical scaling without touching the user's display configuration.
            var content = (FrameworkElement)mini.Content;
            content.LayoutTransform = new System.Windows.Media.ScaleTransform(1.5, 1.5);
            mini.Width = 600; mini.Height = 1020; await Idle();
            MainWindow.SaveScreenshot(mini, Path.Combine(root, "05-mini-scaled.png"));
            content.LayoutTransform = System.Windows.Media.Transform.Identity;
            mini.Width = 400; mini.Height = 680;
            foreach (var row in model.Rows) row.Apply(new() { EndpointId = row.Endpoint.Id, Kind = ProbeKind.Light, State = ProbeState.CoolingDown, Time = time, CooldownUntil = time.AddHours(1) }, false);
            model.Refresh(); await Idle(); MainWindow.SaveScreenshot(mini, Path.Combine(root, "06-mini-paused.png"));
            Check("All-paused mini view does not imply an outage", mini.Presenter.Count == "—" && mini.Presenter.Headline == "保护暂停");
            shell.ShowFull(); await Idle(); MainWindow.SaveScreenshot(shell.Full, Path.Combine(root, "07-full-panel.png"));
            shell.Full.Close();
        }
        catch (Exception e) { Check("Mini visual rendering", false, e.ToString()); }
        Check("No WPF data binding errors", bindingErrors.Messages.Count == 0, bindingErrors.Messages);
        PresentationTraceSources.DataBindingSource.Listeners.Remove(bindingErrors);
        File.WriteAllText(Path.Combine(root, "mini-tests.json"), JsonSerializer.Serialize(new { Version = "1.2.0", Total = tests.Count, Failed = failures, Tests = tests }, Storage.Json));
        return failures;
    }
    private sealed class BindingTrace : TraceListener
    {
        public List<string> Messages { get; } = [];
        public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) Messages.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}
