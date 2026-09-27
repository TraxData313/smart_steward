using System;
using System.Collections.Generic;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The player's pending row edits across a re-plan (PLAN step 7). The window re-plans when a setting changes
    /// (the Prices or Instructions tab) — the steward's suggestion may move, but the player's hand stays: every row
    /// the player edited is set back to its edited quantity in the new plan, as far as the new plan allows.
    /// [decided: Claude, 2026.09.27 — step 7]
    /// <list type="bullet">
    /// <item>Only EDITED rows carry over (Change ≠ suggestion); the others take the new suggestion.</item>
    /// <item>Rows are matched by their stable id; an edited row the new plan no longer has (its job switched off,
    ///   an item unticked away) is dropped — nothing to hold it.</item>
    /// <item>Each is re-applied with <see cref="StewardPlan.SetChange"/> in plan order, so the new limits clamp it
    ///   (an item unticked for buying can no longer be bought) and no re-applied row takes what another has.</item>
    /// <item>After Do it the window plans afresh with NO carry-over: the edits were carried out.</item>
    /// </list>
    /// </summary>
    public sealed class PlanCarryOver
    {
        private readonly List<KeyValuePair<string, int>> _edits;

        private PlanCarryOver(List<KeyValuePair<string, int>> edits)
        {
            _edits = edits;
        }

        /// <summary>Row id → the edited quantity, in plan order.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Edits => _edits;

        public bool IsEmpty => _edits.Count == 0;

        /// <summary>The edited rows of <paramref name="plan"/>.</summary>
        public static PlanCarryOver Capture(StewardPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var edits = new List<KeyValuePair<string, int>>();
            foreach (var row in plan.Rows)
                if (row.IsEdited)
                    edits.Add(new KeyValuePair<string, int>(row.Id, row.Change));
            return new PlanCarryOver(edits);
        }

        /// <summary>Re-applies the edits to a fresh plan. Returns how many rows it found (whether or not the new
        /// limits let them reach the old quantity).</summary>
        public int ApplyTo(StewardPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            int applied = 0;
            foreach (var edit in _edits)
            {
                if (plan.FindRow(edit.Key) == null)
                    continue;
                plan.SetChange(edit.Key, edit.Value);
                applied++;
            }
            return applied;
        }
    }
}
