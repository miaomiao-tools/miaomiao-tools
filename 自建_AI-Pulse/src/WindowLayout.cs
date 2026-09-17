using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;

namespace AIPulse;

public sealed record WindowBounds(int X, int Y, int Width, int Height)
{
    public bool Valid => Math.Abs((long)X) < 500000 && Math.Abs((long)Y) < 500000 && Width is > 0 and <= 20000 && Height is > 0 and <= 20000;
}
public sealed class WindowPreferences
{
    public bool MiniMode { get; set; }
    public bool Pinned { get; set; } = true;
    public WindowBounds? Full { get; set; }
    public WindowBounds? Mini { get; set; }
    public static WindowPreferences Load(Storage storage)
    {
        try
        {
            string path = Path.Combine(storage.Root, "window.json");
            var value = File.Exists(path) ? JsonSerializer.Deserialize<WindowPreferences>(File.ReadAllText(path), Storage.Json) ?? new() : new();
            if (value.Full?.Valid != true) value.Full = null;
            if (value.Mini?.Valid != true) value.Mini = null;
            return value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new(); }
    }
}

/// <summary>Physical-pixel placement avoids confusing negative monitor coordinates with WPF DIPs.</summary>
public static class WindowLayout
{
    public static readonly uint ActivationMessage = RegisterWindowMessage("MiaomiaoTools.AIPulse.Activate.1");
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rectangle);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref NativeRect rectangle, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr parameter, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool AllowSetForegroundWindow(uint processId);
    public static WindowBounds? Capture(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        return hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var r) ? new(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top) : null;
    }
    public static WindowBounds Fit(WindowBounds bounds, WindowBounds work, int minWidth, int minHeight)
    {
        int margin = Math.Min(8, Math.Min(work.Width, work.Height) / 4);
        int availableWidth = Math.Max(1, work.Width - margin * 2), availableHeight = Math.Max(1, work.Height - margin * 2);
        int width = Math.Clamp(bounds.Width, Math.Min(minWidth, availableWidth), availableWidth);
        int height = Math.Clamp(bounds.Height, Math.Min(minHeight, availableHeight), availableHeight);
        return new(Math.Clamp(bounds.X, work.X + margin, work.X + work.Width - margin - width), Math.Clamp(bounds.Y, work.Y + margin, work.Y + work.Height - margin - height), width, height);
    }
    public static void Restore(Window window, WindowBounds? saved)
    {
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        var wanted = saved?.Valid == true ? saved : Capture(window);
        if (wanted == null) return;
        var rect = new NativeRect { Left = wanted.X, Top = wanted.Y, Right = wanted.X + wanted.Width, Bottom = wanted.Y + wanted.Height };
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromRect(ref rect, 2), ref info)) return;
        var work = new WindowBounds(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
        // Move first, allowing PerMonitorV2 to select the target monitor's DPI, then fit the size.
        var preliminary = Fit(wanted, work, 1, 1);
        SetWindowPos(hwnd, IntPtr.Zero, preliminary.X, preliminary.Y, 0, 0, 0x0015);
        double scale = Math.Max(96, GetDpiForWindow(hwnd)) / 96d;
        if (window is MiniWindow)
        {
            window.MinWidth = Math.Min(340, Math.Max(240, (work.Width - 16) / scale));
            window.MinHeight = Math.Min(460, Math.Max(360, (work.Height - 16) / scale));
        }
        var fitted = Fit(wanted, work, (int)Math.Ceiling(window.MinWidth * scale), (int)Math.Ceiling(window.MinHeight * scale));
        SetWindowPos(hwnd, IntPtr.Zero, fitted.X, fitted.Y, fitted.Width, fitted.Height, 0x0014);
    }
    public static WindowBounds? Beside(Window full, Window mini)
    {
        var anchor = Capture(full); if (anchor == null) return null;
        double scale = Math.Max(96, GetDpiForWindow(new WindowInteropHelper(full).Handle)) / 96d;
        int width = (int)Math.Round(mini.Width * scale), height = (int)Math.Round(mini.Height * scale);
        return new(anchor.X + Math.Max(0, anchor.Width - width), anchor.Y, width, height);
    }
    public static async Task<bool> ActivateExistingAsync(int mode)
    {
        for (int attempt = 0; attempt < 25; attempt++)
        {
            var hwnd = FindWindow(null, "AI Pulse · miaomiao tools");
            if (hwnd != IntPtr.Zero)
            {
                GetWindowThreadProcessId(hwnd, out uint pid); AllowSetForegroundWindow(pid);
                return PostMessage(hwnd, ActivationMessage, new IntPtr(mode), IntPtr.Zero);
            }
            await Task.Delay(100);
        }
        return false;
    }
    internal static bool ActivateView(Window window, int mode) => PostMessage(new WindowInteropHelper(window).Handle, ActivationMessage, new IntPtr(mode), IntPtr.Zero);
}
