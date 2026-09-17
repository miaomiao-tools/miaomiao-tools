using System.IO;
using System.Windows;

namespace AIPulse;

public partial class App : Application
{
    private Mutex? instance;
    private bool ownsInstance;
    protected override void OnExit(ExitEventArgs e)
    {
        if (ownsInstance) instance?.ReleaseMutex();
        instance?.Dispose(); base.OnExit(e);
    }
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        string? Arg(string name) { int index = Array.IndexOf(e.Args, name); return index >= 0 && index + 1 < e.Args.Length ? e.Args[index + 1] : null; }
        try
        {
            // The old network smoke runner intentionally is not part of the public release.
            if (e.Args.Contains("--smoke")) { Shutdown(2); return; }
            if (e.Args.Contains("--self-test"))
            {
                if (Arg("--self-test") is not string testPath) { Shutdown(2); return; }
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                int errors = await ContractTests.RunAsync(testPath); Shutdown(errors == 0 ? 0 : 1); return;
            }
            if (e.Args.Contains("--mini-qa"))
            {
                if (Arg("--mini-qa") is not string miniPath) { Shutdown(2); return; }
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                int errors = await MiniTests.RunAsync(miniPath); Shutdown(errors == 0 ? 0 : 1); return;
            }
            // Keep the existing lock shared with prior AI Pulse builds: never double the probes.
            instance = new Mutex(true, "Local\\AIPulse.SingleInstance", out ownsInstance);
            if (!ownsInstance)
            {
                if (!await WindowLayout.ActivateExistingAsync(e.Args.Contains("--mini") ? 1 : e.Args.Contains("--full") ? 2 : 0))
                    MessageBox.Show("已有 AI Pulse 实例正在运行，请使用现有窗口，或先正常退出它。", "AI Pulse");
                Shutdown(); return;
            }
            var storage = new Storage();
            var model = new Dashboard(storage);
            var shell = new AppShell(model, storage); MainWindow = shell.Full;
            shell.Start(e.Args.Contains("--mini") ? true : e.Args.Contains("--full") ? false : null);
        }
        catch (Exception ex)
        {
            string path = Path.Combine(AppContext.BaseDirectory, "startup-error.txt");
            try { File.WriteAllText(path, ex.ToString()); } catch { }
            if (!e.Args.Contains("--self-test") && !e.Args.Contains("--mini-qa")) MessageBox.Show("程序启动未完成。错误信息已保存到：\n" + path, "AI Pulse");
            Shutdown(1);
        }
    }
}