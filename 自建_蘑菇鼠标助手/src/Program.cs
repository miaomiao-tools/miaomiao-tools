using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ShroomMouse
{
    internal static class Program
    {
        internal static string GameExe;
        internal static Overlay Current;
        [STAThread] static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--test") { Tests.Run(args.Length > 1 ? args[1] : "test-results.txt"); return; }
            if (args.Length > 0 && args[0] == "--guard") { Guardian.Run(args); return; }
            bool demo = args.Length > 0 && args[0] == "--demo";
            bool first;
            using (Mutex mutex = new Mutex(true, "Local\\ShroomMouseControls_v1" + (demo ? "_demo" : ""), out first))
            {
                if (!first) { MessageBox.Show("鼠标助手已在运行。进入游戏即可看到按钮；也可点击任务栏通知区的绿色双箭头图标。", "蘑菇鼠标助手"); return; }
                try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { Native.SetProcessDPIAware(); }
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    if (Current != null) Current.EmergencyStop();
                    MessageBox.Show("助手遇到错误，已停止发送按键。请关闭后重新打开。\n" + e.Exception.Message, "蘑菇鼠标助手");
                    Application.Exit();
                };
                AppDomain.CurrentDomain.ProcessExit += delegate { if (Current != null) Current.EmergencyStop(); };
                try
                {
                    // Resolve the selected installation before installing hooks or starting the guardian.
                    if (!demo && !SelectGame()) return;
                    TrainingWindow training = null;
                    if (demo) { training = new TrainingWindow(); training.Show(); }
                    Current = new Overlay(training);
                    Application.Run(Current);
                    if (training != null) training.Dispose();
                }
                catch (Exception ex) { if (Current != null) Current.EmergencyStop(); MessageBox.Show("无法启动鼠标助手：\n" + ex.Message, "蘑菇鼠标助手"); }
            }
        }
        static bool SelectGame()
        {
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game-path.txt");
            bool saved, choseFile = false;
            GameExe = GameLocation.Resolve(file, delegate
            {
                string choice = PickGame("首次使用：选择 Shroom and Gloom.exe（取消即退出）");
                choseFile = choice != null; return choice;
            }, out saved);
            if (GameExe == null)
            {
                if (choseFile) MessageBox.Show("请选择游戏安装目录中的 Shroom and Gloom.exe。", "未选择有效游戏文件");
                return false;
            }
            if (!saved)
                MessageBox.Show("游戏位置仅在本次运行中有效。请将助手解压到可写的文件夹，便于下次记住位置。", "游戏位置未保存");
            return true;
        }
        internal static string PickGame(string title)
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = title; dialog.Filter = "Shroom and Gloom|Shroom and Gloom.exe";
                dialog.CheckFileExists = true; dialog.Multiselect = false; dialog.RestoreDirectory = true;
                return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : null;
            }
        }
    }

    internal sealed class Guardian : IDisposable
    {
        EventWaitHandle w, s;
        readonly EventWaitHandle done;
        internal Guardian()
        {
            string token = "Local\\ShroomMouse_" + Process.GetCurrentProcess().Id + "_" + Guid.NewGuid().ToString("N");
            w = new EventWaitHandle(false, EventResetMode.ManualReset, token + "_w");
            s = new EventWaitHandle(false, EventResetMode.ManualReset, token + "_s");
            done = new EventWaitHandle(false, EventResetMode.ManualReset, token + "_done");
            var start = new ProcessStartInfo(Application.ExecutablePath, "--guard " + Process.GetCurrentProcess().Id + " " + token);
            start.UseShellExecute = false; start.CreateNoWindow = true; start.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(start).Dispose();
        }
        internal bool Send(int key, bool down)
        {
            var flag = key == 0x57 ? w : s;
            if (down) flag.Set(); // Record ownership before sending, including the crash window.
            bool ok = Native.SendKey(key, down);
            if (ok && !down) flag.Reset();
            if (!ok && down) flag.Reset();
            return ok;
        }
        internal static void Run(string[] args)
        {
            try
            {
                using (var gw = EventWaitHandle.OpenExisting(args[2] + "_w"))
                using (var gs = EventWaitHandle.OpenExisting(args[2] + "_s"))
                using (var gd = EventWaitHandle.OpenExisting(args[2] + "_done"))
                {
                    Process parent = null;
                    try { parent = Process.GetProcessById(int.Parse(args[1])); } catch (ArgumentException) { }
                    while (parent != null && !parent.HasExited && !gd.WaitOne(80)) { }
                    if (gw.WaitOne(0)) Native.SendKey(0x57, false);
                    if (gs.WaitOne(0)) Native.SendKey(0x53, false);
                    if (parent != null) parent.Dispose();
                }
            }
            catch { /* No global releases without confirmed ownership. */ }
        }
        public void Dispose() { done.Set(); w.Dispose(); s.Dispose(); done.Dispose(); }
    }

    internal sealed class Preferences
    {
        public int X = int.MinValue, Y = int.MinValue, Size = 1, Opacity = 95;
        public void Load(string file)
        {
            try
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    string[] pair = line.Split('='); int n;
                    if (pair.Length != 2 || !int.TryParse(pair[1], out n)) continue;
                    if (pair[0] == "X") X = n; if (pair[0] == "Y") Y = n;
                    if (pair[0] == "Size") Size = Math.Max(0, Math.Min(2, n));
                    if (pair[0] == "Opacity") Opacity = Math.Max(75, Math.Min(100, n));
                }
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public bool Save(string file)
        {
            try { File.WriteAllLines(file, new string[] { "X=" + X, "Y=" + Y, "Size=" + Size, "Opacity=" + Opacity }, Encoding.UTF8); return true; }
            catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
        }
    }

    internal sealed class OverlayLayer
    {
        readonly Func<bool> isTopmost, restore;
        long nextCheck;
        internal OverlayLayer(Func<bool> readTopmost, Func<bool> restoreTopmost) { isTopmost = readTopmost; restore = restoreTopmost; }
        internal void Refresh(long now, bool visible, bool suspended, bool force)
        {
            if (!visible || suspended) { nextCheck = 0; return; }
            if (!force && now < nextCheck) return;
            nextCheck = now + 500;
            // The WinForms property can remain true after the native style has lost TOPMOST.
            // A failed OS request is retried at the next check, without activating any window.
            if (force || !isTopmost()) restore();
        }
        internal static bool ShouldShow(bool active, bool editing, bool hadGame, bool hasGameWindow)
        { return active || editing || (!hadGame && !hasGameWindow); }
        internal static void Test(Action<bool, string> assert)
        {
            bool topmost = false, accept = true; int reads = 0, writes = 0;
            var layer = new OverlayLayer(delegate { reads++; return topmost; }, delegate { writes++; if (accept) topmost = true; return accept; });
            layer.Refresh(0, false, false, true);
            assert(writes == 0 && reads == 0, "Hidden overlay cannot restore its layer or steal visibility");
            layer.Refresh(1, true, true, true);
            assert(writes == 0 && reads == 0, "Game selection suspends layer restoration even when forced");
            layer.Refresh(2, true, false, true);
            assert(writes == 1 && topmost, "Showing overlay restores native topmost state");
            layer.Refresh(502, true, false, false);
            assert(writes == 1 && reads == 1, "Healthy steady overlay avoids repeated native writes");
            topmost = false; layer.Refresh(1001, true, false, false);
            assert(writes == 1, "Native state checks are throttled");
            layer.Refresh(1002, true, false, false);
            assert(writes == 2 && topmost, "Native topmost loss is repaired despite unchanged managed state");
            layer.Refresh(1003, true, false, true);
            assert(writes == 3, "Returning to game restores layer even if topmost flag survived");
            topmost = false; accept = false; layer.Refresh(1503, true, false, false); layer.Refresh(1504, true, false, false);
            assert(writes == 4 && !topmost, "Failed native restoration does not cause per-frame write loop");
            accept = true; layer.Refresh(2003, true, false, false);
            assert(writes == 5 && topmost, "Failed native restoration is retried on next scheduled check");
            assert(ShouldShow(false, false, false, false), "Waiting before first game keeps setup visible");
            assert(!ShouldShow(false, false, false, true), "Recognized background game keeps overlay hidden");
            assert(ShouldShow(true, false, true, true), "Foreground game displays overlay");
            assert(!ShouldShow(false, false, true, true) && !ShouldShow(false, false, true, false), "Focus loss and game exit hide an already connected overlay");
            assert(ShouldShow(false, true, true, false), "Explicit tray edit restores a disconnected panel");
        }
    }

    internal sealed class Overlay : Form
    {
        readonly TrainingWindow training;
        readonly Preferences prefs = new Preferences();
        readonly string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "controls.ini");
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Movement movement;
        readonly Guardian guardian;
        readonly OverlayLayer layer;
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Dictionary<string, Rectangle> regions = new Dictionary<string, Rectangle>();
        readonly Native.HookProc mouseProc;
        readonly Native.WinEventProc focusProc;
        IntPtr mouseHook, focusHook, gameWindow;
        Process game;
        bool active, captured, dragging, options, cleaned, editing, hadGame, disposing, selectingGame;
        int inputEpoch;
        string pressed = "", hovered = "", status = "等待游戏启动";
        Point dragOffset;
        long nextScan, launchAt = -20000;
        float uiScale = 1;
        Font labelFont, smallFont, actionFont;
        static readonly Color Background = Color.FromArgb(25, 29, 26);
        static readonly Color Surface = Color.FromArgb(43, 50, 43);
        static readonly Color Hover = Color.FromArgb(58, 70, 55);
        static readonly Color TextColor = Color.FromArgb(241, 242, 229);
        static readonly Color Muted = Color.FromArgb(185, 194, 174);
        static readonly Color Accent = Color.FromArgb(189, 220, 134);

        internal Overlay(TrainingWindow test)
        {
            training = test;
            if (training == null) prefs.Load(settingsFile);
            Text = training == null ? "蘑菇鼠标助手" : "蘑菇鼠标助手 · 验证";
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true; TopMost = true; AutoScaleMode = AutoScaleMode.None;
            BackColor = Background; DoubleBuffered = true;
            AccessibleName = "蘑菇鼠标助手：前进和后退悬浮按钮";
            guardian = new Guardian(); movement = new Movement(guardian.Send);
            layer = new OverlayLayer(delegate { return Native.IsTopmost(Handle); }, delegate { return Native.RestoreTopmost(Handle); });
            mouseProc = MouseHook; focusProc = ForegroundChanged;
            options = training == null;
            IntPtr handle = Handle;
            Rebuild();
            Rectangle area = training == null ? Screen.PrimaryScreen.WorkingArea : training.Bounds;
            Location = prefs.X == int.MinValue || prefs.Y == int.MinValue
                ? new Point(area.Right - Width - 50, area.Top + Math.Max(80, (area.Height - Height) / 2))
                : new Point(prefs.X, prefs.Y);
            ClampTo(Screen.FromRectangle(Bounds).WorkingArea);
            Icon = CreateIcon(); tray.Icon = Icon; tray.Text = "蘑菇鼠标助手 · 鼠标前进 / 后退";
            var menu = new ContextMenuStrip();
            menu.Items.Add("显示 / 调整按钮", null, delegate { ShowForEditing(); });
            menu.Items.Add("启动 / 返回游戏", null, delegate { LaunchGame(); });
            menu.Items.Add("重新选择游戏…", null, delegate { ReselectGame(); }).Enabled = training == null;
            menu.Items.Add("重置位置和大小", null, delegate { prefs.Size = 1; prefs.Opacity = 95; Rebuild(); Rectangle a = Screen.PrimaryScreen.WorkingArea; Location = new Point(a.Right - Width - 50, a.Top + (a.Height - Height) / 2); Save(); ShowForEditing(); });
            menu.Items.Add("退出助手", null, delegate { Close(); });
            tray.ContextMenuStrip = menu; tray.MouseClick += delegate(object sender, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowForEditing(); }; tray.Visible = true;
            mouseHook = Native.SetWindowsHookEx(14, mouseProc, Native.GetModuleHandle(null), 0);
            if (mouseHook == IntPtr.Zero) throw new InvalidOperationException("无法监听鼠标，助手没有启用。");
            focusHook = Native.SetWinEventHook(3, 3, IntPtr.Zero, focusProc, 0, 0, 0);
            timer.Interval = 16; timer.Tick += delegate { UpdateState(); }; timer.Start();
            Shown += delegate { UpdateState(); if (training != null) Native.SetForegroundWindow(training.Handle); };
            if (training != null) training.FormClosed += delegate { Close(); };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00040000;
                // Preserve the desired flag when opacity/DPI changes regenerate native styles.
                if (TopMost) p.ExStyle |= 0x00000008;
                return p;
            }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; } // MA_NOACTIVATE
            if (m.Msg == 0x02E0) { base.WndProc(ref m); if (movement != null) { movement.Cancel(); Rebuild(); ClampTo(Screen.FromRectangle(Bounds).WorkingArea); } return; }
            base.WndProc(ref m);
        }
        bool GameIsForeground()
        {
            if (selectingGame) return false;
            var foreground = Native.GetForegroundWindow();
            return gameWindow != IntPtr.Zero && foreground == gameWindow && Native.IsWindow(gameWindow) && !Native.IsIconic(gameWindow);
        }
        void FindGame()
        {
            if (training != null) { gameWindow = training.IsDisposed ? IntPtr.Zero : training.Handle; return; }
            if (game != null)
            {
                try { if (!game.HasExited) { game.Refresh(); gameWindow = game.MainWindowHandle; return; } } catch { }
                game.Dispose(); game = null;
            }
            gameWindow = IntPtr.Zero;
            foreach (Process p in Process.GetProcessesByName("Shroom and Gloom"))
            {
                bool accepted = false;
                try
                {
                    if (GameLocation.Matches(Program.GameExe, p.MainModule.FileName))
                    { game = p; gameWindow = p.MainWindowHandle; accepted = true; }
                }
                catch { }
                if (!accepted) p.Dispose(); else break;
            }
        }
        void ForegroundChanged(IntPtr hook, uint evt, IntPtr window, int obj, int child, uint thread, uint time)
        {
            if (disposing) return;
            if (selectingGame) { movement.Cancel(); return; }
            // A programmatic window activation (including accessibility testing) must not steal game focus.
            if (window == Handle && active && gameWindow != IntPtr.Zero)
            { Native.SetForegroundWindow(gameWindow); return; }
            if (window != gameWindow) movement.Cancel();
            UpdateState();
        }
        void UpdateState()
        {
            if (disposing) return;
            if (selectingGame) { movement.Tick(clock.ElapsedMilliseconds, false, false); return; }
            if (clock.ElapsedMilliseconds >= nextScan) { FindGame(); nextScan = clock.ElapsedMilliseconds + 800; }
            bool nowActive = GameIsForeground();
            bool becameActive = nowActive && !active;
            if (nowActive) { editing = false; hadGame = true; }
            if (nowActive != active)
            {
                active = nowActive; movement.Cancel(); pressed = ""; dragging = false;
                if (active) { options = false; Rebuild(); Native.Rect r; if (Native.GetWindowRect(gameWindow, out r)) ClampTo(new Rectangle(r.L, r.T, r.R-r.L, r.B-r.T)); }
            }
            bool shouldShow = OverlayLayer.ShouldShow(active, editing, hadGame, gameWindow != IntPtr.Zero);
            bool becameVisible = shouldShow && !Visible;
            if (Visible != shouldShow) { if (shouldShow) Show(); else Hide(); }
            layer.Refresh(clock.ElapsedMilliseconds, Visible, selectingGame || disposing, becameActive || becameVisible);
            string nextStatus = movement.Failed ? "按键发送失败，请重启助手" : active ? "已连接 · 按住可持续移动" : gameWindow != IntPtr.Zero ? "切回游戏后可操作" : "等待游戏启动";
            if (nextStatus != status) { status = nextStatus; Invalidate(); }
            Point cursor = PointToClient(Cursor.Position);
            string hit = Hit(cursor);
            bool inside = movement.Key == 0 || (movement.Key == 0x57 ? "forward" : "back") == hit;
            int oldKey = movement.Key;
            movement.Tick(clock.ElapsedMilliseconds, nowActive && Visible, inside);
            if (oldKey != movement.Key) Invalidate();
            string nextHover = Visible ? hit : "";
            if (hovered != nextHover) { hovered = nextHover; Invalidate(); }
        }
        IntPtr MouseHook(int code, IntPtr msg, IntPtr data)
        {
            if (code < 0 || disposing || selectingGame) return Native.CallNextHookEx(mouseHook, code, msg, data);
            int epoch = inputEpoch;
            var mouse = (Native.MouseData)Marshal.PtrToStructure(data, typeof(Native.MouseData));
            int message = msg.ToInt32(); Point screen = new Point(mouse.Pt.X, mouse.Pt.Y);
            if (message == 0x0201 && Visible && Bounds.Contains(screen) && Native.WindowFromPoint(mouse.Pt) == Handle)
            {
                captured = true;
                // Queue native input outside the hook callback; never block the low-level hook.
                BeginInvoke((Action)delegate { if (epoch == inputEpoch) PointerDown(screen); });
                return new IntPtr(1);
            }
            if (message == 0x0202 && captured)
            {
                captured = false;
                BeginInvoke((Action)delegate { if (epoch == inputEpoch) PointerUp(screen); });
                return new IntPtr(1);
            }
            if (message == 0x0200 && captured)
            {
                // Mouse moves remain available to the OS. Dragging is updated on our UI queue.
                if (dragging) BeginInvoke((Action)delegate { if (epoch == inputEpoch && dragging) Location = new Point(screen.X-dragOffset.X, screen.Y-dragOffset.Y); });
            }
            return Native.CallNextHookEx(mouseHook, code, msg, data);
        }
        void PointerDown(Point screen)
        {
            if (disposing || selectingGame || !Visible) return;
            string hit = Hit(PointToClient(screen)); pressed = hit;
            if (hit == "forward" || hit == "back")
            {
                movement.Press(hit == "forward" ? 0x57 : 0x53, clock.ElapsedMilliseconds, GameIsForeground());
            }
            else if (hit == "drag") { movement.Cancel(); dragging = true; dragOffset = new Point(screen.X-Left, screen.Y-Top); }
            else movement.Cancel();
            Invalidate();
        }
        void PointerUp(Point screen)
        {
            if (disposing || selectingGame) return;
            bool wasDragging = dragging; dragging = false;
            movement.ReleasePointer(clock.ElapsedMilliseconds);
            string released = Hit(PointToClient(screen)); string action = pressed; pressed = "";
            if (wasDragging) { ClampTo(Screen.FromRectangle(Bounds).WorkingArea); Save(); }
            else if (action == released)
            {
                if (action == "close") { Close(); return; }
                if (action == "options") { movement.Cancel(); options = !options; Rebuild(); ClampTo(Screen.FromRectangle(Bounds).WorkingArea); }
                if (action == "size") { prefs.Size = (prefs.Size + 1) % 3; Rebuild(); ClampTo(Screen.FromRectangle(Bounds).WorkingArea); Save(); }
                if (action == "opacity") { prefs.Opacity = prefs.Opacity == 95 ? 85 : prefs.Opacity == 85 ? 75 : prefs.Opacity == 75 ? 100 : 95; Opacity = prefs.Opacity / 100.0; Save(); }
                if (action == "launch") LaunchGame();
                if (action == "select") ReselectGame();
            }
            Invalidate();
        }
        void LaunchGame()
        {
            if (selectingGame) return;
            movement.Cancel(); FindGame();
            if (gameWindow != IntPtr.Zero) { editing = false; Native.SetForegroundWindow(gameWindow); return; }
            if (clock.ElapsedMilliseconds - launchAt < 15000) return;
            try
            {
                if (training == null) Process.Start(new ProcessStartInfo("steam://rungameid/3271280") { UseShellExecute = true });
                launchAt = clock.ElapsedMilliseconds; status = "正在启动游戏…"; Invalidate();
            }
            catch (Exception e) { MessageBox.Show("请从 Steam 启动游戏。\n" + e.Message, "蘑菇鼠标助手"); }
        }
        void ReselectGame()
        {
            if (selectingGame || disposing || training != null) return;
            selectingGame = true; inputEpoch++; captured = false; dragging = false; pressed = ""; active = false;
            bool wasTopMost = TopMost; TopMost = false;
            try
            {
                string target;
                var result = GameLocation.Reselect(movement, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "game-path.txt"), Program.GameExe,
                    delegate { return Program.PickGame("重新选择 Shroom and Gloom.exe（取消保留原游戏）"); }, out target);
                if (result == SelectionResult.Changed)
                {
                    Program.GameExe = target;
                    if (game != null) { game.Dispose(); game = null; }
                    gameWindow = IntPtr.Zero; nextScan = 0; hadGame = false; launchAt = -20000;
                }
                else if (result == SelectionResult.Invalid)
                    MessageBox.Show("请选择 Shroom and Gloom.exe。原游戏位置已保留。", "没有更改游戏");
                else if (result == SelectionResult.SaveFailed)
                    MessageBox.Show("无法保存新位置，原游戏和配置已保留。请确认助手文件夹可写后重试。", "没有更改游戏");
                else if (result == SelectionResult.ReleaseFailed)
                    MessageBox.Show("按键尚未成功释放，暂时不能选择游戏。请松开鼠标，稍后重试。", "已停止切换");
            }
            finally
            {
                selectingGame = false; inputEpoch++; TopMost = wasTopMost;
                if (!disposing) { ShowForEditing(); UpdateState(); }
            }
        }
        void ShowForEditing() { movement.Cancel(); editing = true; options = true; Rebuild(); Show(); ClampTo(Screen.FromRectangle(Bounds).WorkingArea); layer.Refresh(clock.ElapsedMilliseconds, Visible, selectingGame || disposing, true); Invalidate(); }
        void Save()
        {
            if (training != null) return;
            prefs.X = Left; prefs.Y = Top;
            if (!prefs.Save(settingsFile)) { tray.ShowBalloonTip(3000, "位置未保存", "当前按钮可以使用，但助手目录不可写，重启后会恢复默认位置。", ToolTipIcon.Info); }
        }
        string Hit(Point point) { foreach (var pair in regions) if (pair.Value.Contains(point)) return pair.Key; return ""; }
        int S(float value) { return (int)Math.Round(value * uiScale); }
        Rectangle R(int x, int y, int w, int h) { return new Rectangle(S(x), S(y), S(w), S(h)); }
        void Rebuild()
        {
            float dpi = 96; try { dpi = Native.GetDpiForWindow(Handle); } catch { }
            uiScale = Math.Max(1, dpi / 96f) * (prefs.Size == 0 ? 0.85f : prefs.Size == 2 ? 1.2f : 1f);
            if (labelFont != null) { labelFont.Dispose(); smallFont.Dispose(); actionFont.Dispose(); }
            labelFont = new Font("Microsoft YaHei UI", S(13), FontStyle.Regular, GraphicsUnit.Pixel);
            smallFont = new Font("Microsoft YaHei UI", S(11), FontStyle.Regular, GraphicsUnit.Pixel);
            actionFont = new Font("Microsoft YaHei UI", S(20), FontStyle.Bold, GraphicsUnit.Pixel);
            ClientSize = new Size(S(228), S(options ? 416 : 252));
            regions.Clear(); regions["drag"] = R(8, 4, 164, 44); regions["close"] = R(180, 4, 44, 44);
            regions["forward"] = R(12, 52, 204, 64); regions["back"] = R(12, 124, 204, 64);
            regions["options"] = R(156, 204, 60, 44);
            if (options) { regions["size"] = R(12, 252, 98, 44); regions["opacity"] = R(118, 252, 98, 44); regions["launch"] = R(12, 304, 204, 44); regions["select"] = R(12, 356, 204, 44); }
            Opacity = prefs.Opacity / 100.0; Invalidate();
        }
        void ClampTo(Rectangle area)
        {
            if (area.Width <= 0 || area.Height <= 0) return;
            Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right-Width)), Math.Max(area.Top, Math.Min(Top, area.Bottom-Height)));
        }
        void DrawText(Graphics g, string value, Rectangle rect, Font font, Color color, bool centered)
        {
            TextRenderer.DrawText(g, value, font, rect, color, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | (centered ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left));
        }
        void ButtonBackground(Graphics g, string key, bool enabled)
        {
            bool down = (key == "forward" && movement.Key == 0x57) || (key == "back" && movement.Key == 0x53) || (pressed == key && key != "forward" && key != "back");
            Color fill = down ? Accent : enabled && hovered == key ? Hover : Surface;
            using (var b = new SolidBrush(fill)) g.FillRectangle(b, regions[key]);
            using (var pen = new Pen(down ? Accent : Color.FromArgb(74, 86, 66), Math.Max(1, S(1)))) g.DrawRectangle(pen, regions[key]);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e); Graphics g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(87, 103, 72))) g.DrawRectangle(pen, 0, 0, Width-1, Height-1);
            DrawText(g, "鼠标行走  ·  拖动这里", R(14, 4, 164, 44), labelFont, Muted, false);
            using (var p = new Pen(hovered == "close" ? Accent : Muted, S(1.5f))) { g.DrawLine(p, S(195), S(19), S(209), S(33)); g.DrawLine(p, S(209), S(19), S(195), S(33)); }
            foreach (string key in new string[] { "forward", "back" })
            {
                ButtonBackground(g, key, active); Rectangle r = regions[key];
                bool down = movement.Key == (key == "forward" ? 0x57 : 0x53);
                Color ink = down ? Background : active ? TextColor : Muted;
                int y = key == "forward" ? 84 : 156;
                Point[] triangle = key == "forward" ? new Point[] { new Point(S(33), S(y+5)), new Point(S(43), S(y-5)), new Point(S(53), S(y+5)) } : new Point[] { new Point(S(33), S(y-5)), new Point(S(43), S(y+5)), new Point(S(53), S(y-5)) };
                using (var brush = new SolidBrush(ink)) g.FillPolygon(brush, triangle);
                DrawText(g, key == "forward" ? "前进" : "后退", new Rectangle(S(69), r.Y, S(90), r.Height), actionFont, ink, false);
                DrawText(g, key == "forward" ? "W" : "S", new Rectangle(S(180), r.Y, S(24), r.Height), labelFont, ink, true);
            }
            DrawText(g, status, R(13, 189, 202, 22), smallFont, movement.Failed ? Color.LightSalmon : Muted, false);
            DrawText(g, "点击 / 按住", R(14, 210, 138, 34), labelFont, Muted, false);
            DrawText(g, options ? "收起" : "设置", regions["options"], labelFont, hovered == "options" ? Accent : TextColor, true);
            if (options)
            {
                ButtonBackground(g, "size", true); ButtonBackground(g, "opacity", true); ButtonBackground(g, "launch", true);
                DrawText(g, "大小：" + (prefs.Size == 0 ? "小" : prefs.Size == 2 ? "大" : "中"), regions["size"], labelFont, TextColor, true);
                DrawText(g, "不透明 " + prefs.Opacity + "%", regions["opacity"], smallFont, TextColor, true);
                DrawText(g, gameWindow == IntPtr.Zero ? "启动游戏" : "返回游戏", regions["launch"], labelFont, Accent, true);
                ButtonBackground(g, "select", training == null);
                DrawText(g, training == null ? "重新选择游戏…" : "测试模式 · 固定测试窗口", regions["select"], smallFont, training == null ? TextColor : Muted, true);
            }
        }
        static Icon CreateIcon()
        {
            using (Bitmap b = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Background);
                    using (var brush = new SolidBrush(Accent))
                    { g.FillPolygon(brush, new Point[] { new Point(7, 13), new Point(16, 4), new Point(25, 13) }); g.FillPolygon(brush, new Point[] { new Point(7, 19), new Point(16, 28), new Point(25, 19) }); }
                }
                IntPtr h = b.GetHicon(); Icon result = (Icon)Icon.FromHandle(h).Clone(); Native.DestroyIcon(h); return result;
            }
        }
        internal void EmergencyStop() { if (movement != null) movement.Cancel(); }
        protected override void OnFormClosing(FormClosingEventArgs e) { EmergencyStop(); Save(); base.OnFormClosing(e); }
        protected override void Dispose(bool disposingManaged)
        {
            if (!cleaned && disposingManaged)
            {
                cleaned = true; disposing = true; timer.Stop(); timer.Dispose(); EmergencyStop();
                if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook);
                if (focusHook != IntPtr.Zero) Native.UnhookWinEvent(focusHook);
                tray.Visible = false; tray.Dispose(); guardian.Dispose();
                if (game != null) game.Dispose();
                if (labelFont != null) { labelFont.Dispose(); smallFont.Dispose(); actionFont.Dispose(); }
            }
            base.Dispose(disposingManaged);
        }
    }

    internal sealed class TrainingWindow : Form
    {
        readonly List<string> events = new List<string>();
        int forwards, backwards, clicks; bool w, s;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "demo-events.txt");
        internal TrainingWindow()
        {
            Text = "鼠标控制验证 · 测试场景"; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(980, 650);
            BackColor = Color.FromArgb(35, 38, 35); ForeColor = Color.White; Font = new Font("Microsoft YaHei UI", 15);
            KeyPreview = true; DoubleBuffered = true;
            KeyDown += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.W) { w = true; forwards++; Record("W DOWN"); } if (e.KeyCode == Keys.S) { s = true; backwards++; Record("S DOWN"); } Invalidate(); };
            KeyUp += delegate(object sender, KeyEventArgs e) { if (e.KeyCode == Keys.W) { w = false; Record("W UP"); } if (e.KeyCode == Keys.S) { s = false; Record("S UP"); } Invalidate(); };
            MouseDown += delegate { clicks++; Record("SCENE CLICK"); Invalidate(); };
            Activated += delegate { Record("FOCUS GAIN"); Invalidate(); };
            Deactivate += delegate { Record("FOCUS LOST"); Invalidate(); };
        }
        void Record(string value) { events.Add(clock.ElapsedMilliseconds + " " + value); try { File.WriteAllLines(log, events); } catch { } }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.DrawString("鼠标控制验证\n\n点击悬浮面板的前进 / 后退按钮\n\n前进次数：" + forwards + "\n后退次数：" + backwards + "\n场景误点：" + clicks + "\n\nW：" + (w ? "按住" : "松开") + "    S：" + (s ? "按住" : "松开") + "\n\n此场景不会改变游戏存档。", Font, Brushes.White, 45, 50);
        }
    }

    internal static class Tests
    {
        internal static void Run(string file)
        {
            var report = new List<string>();
            Action<bool, string> assert = delegate(bool ok, string name) { if (!ok) throw new Exception(name); report.Add("PASS " + name); };
            try
            {
                var events = new List<string>();
                var m = new Movement(delegate(int key, bool down) { events.Add(key + (down ? " down" : " up")); return true; });
                assert(!m.Press(0x57, 0, false) && events.Count == 0, "Inactive game: no input");
                assert(!m.Press(0x41, 0, true) && events.Count == 0, "Only W and S accepted");
                m.Press(0x57, 0, true); m.ReleasePointer(10); m.Tick(99, true, true);
                assert(m.Key == 0x57, "Fast click remains visible for at least 100 ms");
                m.Tick(100, true, true); assert(m.Key == 0 && events.Count == 2, "Fast click emits one down and one up");
                m.Press(0x53, 200, true); m.Tick(2000, true, true); assert(m.Key == 0x53 && m.PointerHeld, "Hold preserves S without repeated down events");
                m.ReleasePointer(2001); assert(m.Key == 0, "Mouse release stops hold");
                m.Press(0x57, 3000, true); m.Tick(3010, false, true); assert(m.Key == 0, "Focus loss overrides minimum pulse immediately");
                m.Press(0x57, 4000, true); m.Tick(4010, true, false); assert(m.Key == 0, "Pointer leaving button cancels");
                m.Press(0x57, 5000, true); m.Press(0x53, 5001, true); assert(m.Key == 0x53 && events[events.Count-2] == "87 up", "Changing direction releases previous key first");
                m.Tick(35001, true, true); assert(m.Key == 0, "30 second stuck hold safeguard");
                m.Press(0x57, 40000, true); m.Cancel(); m.Cancel(); assert(m.Key == 0, "Exit cleanup is idempotent");
                bool fail = true; var failing = new Movement(delegate(int key, bool down) { return down || !fail; });
                failing.Press(0x57, 0, true); failing.Cancel(); assert(failing.Key == 0x57 && failing.Failed, "Failed key up preserves ownership for retry");
                fail = false; failing.Tick(101, false, false); assert(failing.Key == 0, "Key up is retried successfully");
                var failedDown = new Movement(delegate { return false; }); assert(!failedDown.Press(0x57, 0, true) && failedDown.Key == 0 && failedDown.Failed, "Failed key down is reported without latching");
                assert(Marshal.SizeOf(typeof(Native.Input)) == (IntPtr.Size == 8 ? 40 : 28), "Native SendInput ABI size correct");
                GameLocationTests.Run(assert, Path.GetDirectoryName(Path.GetFullPath(file)));
                OverlayLayer.Test(assert);
                report.Add("ALL CHECKS PASSED");
            }
            catch (Exception e) { report.Add("FAIL " + e.Message); Environment.ExitCode = 1; }
            File.WriteAllLines(file, report);
        }
    }
}
