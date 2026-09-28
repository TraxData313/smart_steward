using System;
using System.Collections.Generic;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The player's pending row edits across a re-plan (PLAN step 7). The window re-plans when a setting changes
    /// (the Prices or Instructions tab) — the steward's suggestion may move, but the player's hand stays: every row
    /// the player touched is set back to its quantity in the new plan, as far as the new plan allows.
    /// [decided: Claude, 2026.09.27 — step 7; the touch made explicit in step 15]
    /// <list type="bullet">
    /// <item>Only TOUCHED rows carry over (<see cref="PlanRow.IsTouched"/>); the others are the steward's and plan
    ///   around them — the hires and prisoners among them define the party after the deal (the live re-plan).</item>
    /// <item>Rows are matched by their stable id; a touched row the new plan no longer has (its job switched off,
    ///   an item unticked away) is dropped — nothing to hold it.</item>
    /// <item>They are put back together with <see cref="StewardPlan.Restore"/>: one re-plan in which they walk first, so the
    ///   new limits clamp them (an item unticked for buying can no longer be bought).</item>
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

        /// <summary>Row id → the touched quantity, in plan order.</summary>
        public IReadOnlyList<KeyValuePair<string, int>> Edits => _edits;

        public bool IsEmpty => _edits.Count == 0;

        /// <summary>The touched rows of <paramref name="plan"/>.</summary>
        public static PlanCarryOver Capture(StewardPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var edits = new List<KeyValuePair<string, int>>();
            foreach (var row in plan.Rows)
                if (row.IsTouched)
                    edits.Add(new KeyValuePair<string, int>(row.Id, row.Change));
            return new PlanCarryOver(edits);
        }

        /// <summary>Puts the player's hand back on a fresh plan (<see cref="StewardPlan.Restore"/>). Returns how many rows
        /// it found (whether or not the new limits let them reach the old quantity).</summary>
        public int ApplyTo(StewardPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            return _edits.Count == 0 ? 0 : plan.Restore(_edits);
        }
    }
}
