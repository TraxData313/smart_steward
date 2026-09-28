using System;
using System.Collections.Generic;

namespace SmartSteward.Core.Settings
{
    /// <summary>
    /// The standing goals the player types or clicks in the Suggestion tab (DESIGN §1.1 "THE GOAL" — Anton 2026.09.28, round 5:
    /// "a goal tab that I can directly edit here so that I can start controlling stuff directly from here"): row id → the Result
    /// the row should end at, kept in <see cref="StewardSettings.Goals"/> (settings.json's commented <c>Goals</c> object) until
    /// the row's ⟲. Only four kinds of rows take one: every food row and the pack, riding and war horse role rows.
    /// </summary>
    public static class ManualGoals
    {
        /// <summary>A goal is kept within 0 … this.</summary>
        public const int MaxGoal = 100_000;

        /// <summary>The prefix of a food row's id (<c>food:grain</c>).</summary>
        public const string FoodPrefix = "food:";

        /// <summary>The role rows that take a goal (<c>Planning.PackAnimalPlanner</c> / <c>MountPlanner</c> ids).</summary>
        public const string Pack = "mounts:pack";
        public const string Riding = "mounts:riding";
        public const string War = "mounts:war";

        /// <summary>Does a row with this id take a goal typed by hand? Food rows (<c>food:&lt;item id&gt;</c>) and the pack, riding
        /// and war horse rows.</summary>
        public static bool IsGoalKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;
            return key == Pack || key == Riding || key == War
                   || (key!.StartsWith(FoodPrefix, StringComparison.Ordinal) && key.Length > FoodPrefix.Length
                       && key.Trim() == key);
        }

        /// <summary>A goal kept in range.</summary>
        public static int Clamp(long value) => (int)Math.Max(0, Math.Min(MaxGoal, value));

        /// <summary>The goal of a row; null = none (the row follows the policy).</summary>
        public static int? Get(StewardSettings settings, string key)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return settings.Goals != null && settings.Goals.TryGetValue(key, out int goal) ? goal : (int?)null;
        }

        /// <summary>Sets (clamped) or, with null, removes a row's goal. A key that takes no goal is ignored (false).</summary>
        public static bool Set(StewardSettings settings, string key, int? goal)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!IsGoalKey(key))
                return false;
            settings.Goals ??= new Dictionary<string, int>(StringComparer.Ordinal);
            if (goal == null)
                return settings.Goals.Remove(key);
            settings.Goals[key] = Clamp(goal.Value);
            return true;
        }

        /// <summary>A copy of the goals (a plan keeps its own and edits it — <c>StewardPlan.SetGoal</c>).</summary>
        public static Dictionary<string, int> CopyOf(StewardSettings? settings)
        {
            var copy = new Dictionary<string, int>(StringComparer.Ordinal);
            if (settings?.Goals != null)
                foreach (var pair in settings.Goals)
                    if (IsGoalKey(pair.Key))
                        copy[pair.Key] = Clamp(pair.Value);
            return copy;
        }
    }

    /// <summary>One goal edit a plan made (a click, a typed goal, a ⟲) for the window to save: the row id and the new goal
    /// (null = removed — the row is the policy's again).</summary>
    public sealed class GoalEdit
    {
        public GoalEdit(string key, int? goal)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Goal = goal;
        }

        public string Key { get; }
        public int? Goal { get; }

        /// <summary>Writes the edit into <paramref name="settings"/> (<c>SettingsService.SaveQuietly</c>).</summary>
        public void ApplyTo(StewardSettings settings) => ManualGoals.Set(settings, Key, Goal);

        public override string ToString() => Key + " = " + (Goal?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "policy");
    }
}
