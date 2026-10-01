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
        public static PlanCarryOver Capture(StewardPlan plan) => Capture(plan, null);

        /// <summary>
        /// The touched rows of <paramref name="plan"/> that <paramref name="done"/> did NOT carry out (PLAN step 27: a part ran alone,
        /// the window plans afresh on the world it left, and the player's hand on every OTHER part goes back on the new plan). A
        /// troop row counts by its side: after Recruits its dismissal stays, after Your troops its recruits. A row the part ran is
        /// dropped even when the executor cut it short — the fresh plan proposes it anew. A breakdown line run alone (step 29)
        /// leaves the rest of its row's edit: the row's change less the line's (<see cref="PlanPart.EditLeft"/>).
        /// </summary>
        public static PlanCarryOver Capture(StewardPlan plan, PlanPart? done)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            var edits = new List<KeyValuePair<string, int>>();
            foreach (var row in plan.Rows)
            {
                if (!row.IsTouched || row.TakesGoal) // round 5: a goal row's hand is its goal — the settings carry it
                    continue;
                int left = done == null ? row.Change : done.EditLeft(row);
                // A row the part did not touch keeps its hand as it is (a 0 the player set included); one it ran keeps the rest.
                if (left == row.Change || left != 0)
                    edits.Add(new KeyValuePair<string, int>(row.Id, left));
            }
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
