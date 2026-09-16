using System;
using System.IO;
using System.Text;

namespace ShroomMouse
{
    internal static class GameLocation
    {
        internal const string FileName = "Shroom and Gloom.exe";

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
            try { File.WriteAllText(file, valid, new UTF8Encoding(false)); return true; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
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
                assert(!GameLocation.Matches(exe, Path.Combine(root, GameLocation.FileName)), "Same executable name in another directory is not a target");
                assert(!GameLocation.Matches(null, exe), "No configured path cannot match any process");
                File.WriteAllText(config, exe + Environment.NewLine, Encoding.UTF8);
                assert(GameLocation.Load(config) == exe, "Accept UTF-8 config with BOM and trailing newline");
                File.WriteAllText(config, "invalid", Encoding.UTF8);
                assert(GameLocation.Load(config) == null, "Invalid saved path returns to selection");
                assert(GameLocation.Resolve(config, delegate { return null; }, out saved) == null && !saved, "Cancelling invalid saved installation keeps target disabled");
                assert(!GameLocation.Save(root, exe), "Unwritable config reports failure without replacing directory");
                assert(GameLocation.Save(config, exe), "Valid installation can replace invalid saved setting");
                File.Delete(exe);
                assert(GameLocation.Load(config) == null, "Moved or removed game returns to selection");
                picks = 0;
                assert(GameLocation.Resolve(config, delegate { picks++; return null; }, out saved) == null && picks == 1, "Missing installation requires fresh selection and cancel is safe");
            }
            finally { Directory.Delete(root, true); }
        }
    }
}
