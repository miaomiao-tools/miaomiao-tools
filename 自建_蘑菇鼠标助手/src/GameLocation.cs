using System;
using System.IO;
using System.Text;

namespace ShroomMouse
{
    internal enum SelectionResult { Changed, Cancelled, Invalid, SaveFailed, ReleaseFailed }
    internal static class GameLocation
    {
        internal const string FileName = "Shroom and Gloom.exe";

        internal static SelectionResult Reselect(Movement movement, string file, string current, Func<string> select, out string target)
        {
            target = current;
            movement.Suspend();
            try
            {
                // Never open a picker while a failed key-up still belongs to this helper.
                if (movement.Key != 0) return SelectionResult.ReleaseFailed;
                string choice = select();
                if (choice == null) return SelectionResult.Cancelled;
                string valid = Validate(choice);
                if (valid == null) return SelectionResult.Invalid;
                if (!Save(file, valid)) return SelectionResult.SaveFailed;
                target = valid;
                return SelectionResult.Changed;
            }
            finally { movement.Resume(); }
        }

        internal static string Resolve(string file, Func<string> select, out bool saved)
        {
            string path = Load(file);
            saved = path != null;
            if (saved) return path;
            path = Validate(select());
            if (path != null) saved = Save(file, path);
            return path;
        }

        internal static bool Matches(string configured, string processPath)
        {
            return !string.IsNullOrEmpty(configured) &&
                string.Equals(configured, processPath, StringComparison.OrdinalIgnoreCase);
        }

        internal static string Validate(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            try
            {
                string path = value.Trim();
                // A drive-relative path such as X:game.exe must never use the caller's current directory.
                if (!Path.IsPathRooted(path) || Path.GetPathRoot(path).Length < 3) return null;
                path = Path.GetFullPath(path);
                if (!string.Equals(Path.GetFileName(path), FileName, StringComparison.OrdinalIgnoreCase)) return null;
                return File.Exists(path) ? path : null;
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            catch (System.Security.SecurityException) { return null; }
        }

        internal static string Load(string file)
        {
            try { return Validate(File.ReadAllText(file, Encoding.UTF8)); }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        internal static bool Save(string file, string path)
        {
            string valid = Validate(path);
            if (valid == null) return false;
            string temporary = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(valid);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                // Same-directory atomic replace: a failed save must not truncate the previous setting.
                if (File.Exists(file)) File.Replace(temporary, file, null);
                else File.Move(temporary, file);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (System.Security.SecurityException) { return false; }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
    }

    internal static class GameLocationTests
    {
        internal static void Run(Action<bool, string> assert, string reportDirectory)
        {
            string root = Path.Combine(reportDirectory, "path-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string install = Path.Combine(root, "游戏 安装");
                Directory.CreateDirectory(install);
                string exe = Path.Combine(install, GameLocation.FileName);
                string config = Path.Combine(root, "game-path.txt");
                File.WriteAllBytes(exe, new byte[0]); // Never executed; tests use file existence only.
                assert(GameLocation.Load(config) == null, "First run has no implicit game installation");
                bool saved;
                assert(GameLocation.Resolve(config, delegate { return null; }, out saved) == null && !saved && !File.Exists(config), "Cancel first selection leaves target and config unset");
                assert(GameLocation.Validate(exe) == exe, "Accept Unicode and spaces in selected installation");
                assert(GameLocation.Validate(GameLocation.FileName) == null, "Reject relative executable path");
                assert(GameLocation.Validate(Path.GetPathRoot(exe).Substring(0, 2) + GameLocation.FileName) == null, "Reject drive-relative executable path");
                assert(GameLocation.Validate("\0") == null, "Reject invalid path without throwing");
                string other = Path.Combine(install, "other.exe"); File.WriteAllBytes(other, new byte[0]);
                assert(GameLocation.Validate(other) == null, "Reject other executable names");
                assert(GameLocation.Resolve(config, delegate { return other; }, out saved) == null && !File.Exists(config), "Wrong selected executable cannot enable target");
                assert(GameLocation.Validate(Path.Combine(root, GameLocation.FileName)) == null, "Reject missing installation");
                assert(GameLocation.Save(config, exe) && GameLocation.Load(config) == exe, "Persist and reload selected installation");
                int picks = 0;
                assert(GameLocation.Resolve(config, delegate { picks++; return null; }, out saved) == exe && saved && picks == 0, "Restart reuses valid exact path without selecting again");
                assert(GameLocation.Matches(exe, exe.ToUpperInvariant()), "Full installation match is case insensitive");
                assert(GameLocation.Matches(GameLocation.Validate(exe.Replace('\\', '/')), exe), "Selected forward-slash path resolves to exact installed path");
                string repeated = Path.Combine(install, ".") + "\\\\" + GameLocation.FileName;
                assert(GameLocation.Matches(GameLocation.Validate(repeated), exe), "Selected repeated separators and dot segment resolve before matching");
                assert(GameLocation.Validate(Path.Combine(install, "game.lnk")) == null, "Shortcut cannot replace selected game executable");
                assert(!GameLocation.Matches(exe, Path.Combine(root, GameLocation.FileName)), "Same executable name in another directory is not a target");
                assert(!GameLocation.Matches(null, exe), "No configured path cannot match any process");
                File.WriteAllText(config, exe + Environment.NewLine, Encoding.UTF8);
                assert(GameLocation.Load(config) == exe, "Accept UTF-8 config with BOM and trailing newline");
                File.WriteAllText(config, "invalid", Encoding.UTF8);
                assert(GameLocation.Load(config) == null, "Invalid saved path returns to selection");
                assert(GameLocation.Resolve(config, delegate { return null; }, out saved) == null && !saved, "Cancelling invalid saved installation keeps target disabled");
                assert(!GameLocation.Save(root, exe), "Unwritable config reports failure without replacing directory");
                assert(GameLocation.Save(config, exe), "Valid installation can replace invalid saved setting");
                RunReselection(assert, root, exe, config);
                File.Delete(exe);
                assert(GameLocation.Load(config) == null, "Moved or removed game returns to selection");
                picks = 0;
                assert(GameLocation.Resolve(config, delegate { picks++; return null; }, out saved) == null && picks == 1, "Missing installation requires fresh selection and cancel is safe");
            }
            finally { Directory.Delete(root, true); }
        }
        static void RunReselection(Action<bool, string> assert, string root, string oldExe, string config)
        {
            string otherFolder = Path.Combine(root, "另一份 游戏"); Directory.CreateDirectory(otherFolder);
            string newExe = Path.Combine(otherFolder, GameLocation.FileName); File.WriteAllBytes(newExe, new byte[0]);
            var events = new System.Collections.Generic.List<string>();
            var movement = new Movement(delegate(int key, bool down) { events.Add(key + (down ? " down" : " up")); return true; });
            string target; string original = File.ReadAllText(config); bool pickerSafe = false;
            movement.Press(0x57, 0, true);
            var result = GameLocation.Reselect(movement, config, oldExe, delegate {
                pickerSafe = movement.Suspended && movement.Key == 0 && !movement.PointerHeld;
                assert(!movement.Press(0x53, 1, true), "Input remains blocked during game selection");
                return null;
            }, out target);
            assert(pickerSafe && events.Count == 2 && events[1] == "87 up", "Held W is released before opening selector");
            assert(result == SelectionResult.Cancelled && target == oldExe && File.ReadAllText(config) == original, "Cancel preserves exact target and config bytes");
            assert(!movement.Suspended && movement.Key == 0 && !movement.PointerHeld, "Cancel resumes availability without resuming held movement");
            result = GameLocation.Reselect(movement, config, oldExe, delegate { return Path.Combine(root, "other.exe"); }, out target);
            assert(result == SelectionResult.Invalid && target == oldExe && File.ReadAllText(config) == original, "Invalid reselection preserves previous target and config");
            using (var locked = new FileStream(config, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                result = GameLocation.Reselect(movement, config, oldExe, delegate { return newExe; }, out target);
                assert(result == SelectionResult.SaveFailed && target == oldExe, "Save failure cannot switch target");
            }
            assert(File.ReadAllText(config) == original && GameLocation.Load(config) == oldExe, "Failed atomic replacement preserves original configuration");
            assert(Directory.GetFiles(root, "game-path.txt.*.tmp").Length == 0, "Failed save cleans only its temporary file");
            movement.Press(0x53, 10, true);
            result = GameLocation.Reselect(movement, config, oldExe, delegate { return newExe; }, out target);
            assert(result == SelectionResult.Changed && target == newExe && movement.Key == 0 && events[events.Count-1] == "83 up", "Held S stops before a successful switch");
            assert(GameLocation.Matches(target, newExe) && !GameLocation.Matches(target, oldExe), "Only newly selected full path matches same-name installations");
            bool saved; int picks = 0;
            assert(GameLocation.Resolve(config, delegate { picks++; return null; }, out saved) == newExe && saved && picks == 0, "Restart persists newly selected installation");
            bool failUp = true; int attempts = 0;
            var failing = new Movement(delegate(int key, bool down) { return down || !failUp; });
            failing.Press(0x57, 0, true);
            result = GameLocation.Reselect(failing, config, newExe, delegate { attempts++; return oldExe; }, out target);
            assert(result == SelectionResult.ReleaseFailed && attempts == 0 && target == newExe && failing.Key == 0x57, "Failed release prevents opening picker and changing target");
            failUp = false; failing.Tick(10, false, false);
            assert(failing.Key == 0, "Release retry still works after failed reselection");
            try { GameLocation.Reselect(movement, config, newExe, delegate { throw new InvalidOperationException("test"); }, out target); }
            catch (InvalidOperationException) { }
            assert(!movement.Suspended && movement.Key == 0 && GameLocation.Load(config) == newExe, "Picker exception leaves input released and saved target unchanged");
            assert(GameLocation.Save(config, oldExe), "Restore isolated fixture for missing-path regression");
        }
    }
}
