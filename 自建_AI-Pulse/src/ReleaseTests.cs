using System.IO;

namespace AIPulse;

/// <summary>Release-only boundary regressions; all data is created beneath the supplied test directory.</summary>
public static class ReleaseTests
{
    public static void Run(Action<string, bool, string?> check, string root)
    {
        Directory.CreateDirectory(root);
        try
        {
            string blocked = Path.Combine(root, "not-a-directory");
            File.WriteAllText(blocked, "local fixture");
            bool refused = false;
            try { _ = new Storage(blocked); }
            catch (IOException) { refused = true; }
            check("Explicit inaccessible storage cannot fall back to a user profile", refused, null);
            check("Refused storage leaves the blocking file unchanged", File.ReadAllText(blocked) == "local fixture", null);

            var store = new Storage(Path.Combine(root, "fresh"));
            using var model = new Dashboard(store);
            check("Fresh first launch leaves auto polling disabled", !model.Auto && !model.Running, null);
            check("Fresh first launch has no copied history or custom endpoints", model.Rounds.Count == 0 && model.Settings.CustomEndpoints.Count == 0 && model.Rows.All(r => r.Result == null), null);
            check("Fresh first launch preserves conservative timing", model.Settings.IntervalSeconds >= 300 && model.Settings.TimeoutSeconds == 12, null);
            check("Fresh first launch does not create a history of fake probes", !File.Exists(Path.Combine(store.Root, "history.json")), null);
            check("Fresh first launch creates empty durable protection state", File.ReadAllText(Path.Combine(store.Root, "cooldowns.json")).Trim().TrimStart('\uFEFF') == "{}", null);
        }
        catch (Exception ex) { check("Release data boundary checks", false, ex.GetType().Name); }
    }
}
