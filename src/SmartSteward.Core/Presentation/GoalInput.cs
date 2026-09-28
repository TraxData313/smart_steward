using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Presentation
{
    /// <summary>What a typed Goal box holds when the player leaves it (Enter or a click elsewhere — PLAN step 23).</summary>
    public enum GoalTyped
    {
        /// <summary>The box still says what the steward showed: nothing to do (a goal is never pinned by just passing through).</summary>
        Unchanged,

        /// <summary>Not a whole number of 0 or more (empty, letters, a minus): the box goes back to what it showed.</summary>
        Invalid,

        /// <summary>A goal for <c>StewardPlan.SetGoal</c> — clamped to 0 … <see cref="ManualGoals.MaxGoal"/>.</summary>
        Goal,
    }

    /// <summary>
    /// The Suggestion tab's typed Goal box (DESIGN §1.1 "THE GOAL", round 5): reads what the player typed against what the box
    /// showed. The window commits a <see cref="GoalTyped.Goal"/> through <c>StewardPlan.SetGoal</c>; anything else reverts.
    /// Empty never means "give it back" — that is the ⟲ beside the box.
    /// </summary>
    public static class GoalInput
    {
        /// <summary>Reads <paramref name="typed"/> (thousands separators allowed: <c>1,200</c>) against <paramref name="shown"/>.</summary>
        public static GoalTyped Read(string? typed, string? shown, out int goal)
        {
            goal = 0;
            string t = (typed ?? "").Trim();
            if (t == (shown ?? "").Trim())
                return GoalTyped.Unchanged;
            if (!UiFormat.TryParseWhole(t, out long value) || value < 0)
                return GoalTyped.Invalid;
            goal = ManualGoals.Clamp(value);
            return GoalTyped.Goal;
        }

        /// <summary>The goal a box shows for <paramref name="goal"/>: <c>1,200</c>.</summary>
        public static string Text(int goal) => UiFormat.Money(goal);
    }
}
