using System;
using System.Collections.Generic;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The folded Troops line's own [−] and [+] (PLAN step 18 — Anton 2026.09.28: "the COLLAPSED Troops line carries [-] [+]
    /// of its own: [-] dismisses from the lowest tier up, [+] recruits the highest tier on offer first (shift/ctrl steps as
    /// usual)"). They are ordinary row edits underneath — each moves troop rows with <see cref="SetChange"/>, so the rows are
    /// touched, the food and horses re-plan live, and Do it runs the same transactions; there is no new executor path.
    /// </summary>
    public sealed partial class StewardPlan
    {
        /// <summary>
        /// [−] on the folded Troops line: <paramref name="size"/> men (1, 5, or every man) leave the party after the deal, the
        /// lowest tier first — the party's own troops (not on offer here) lowest tier up, then the types on offer by the same
        /// order (<see cref="TroopPlanner.DismissOrder"/>). A type with recruits queued gives those back first (they are the
        /// men of that type the deal would bring). One <see cref="EditResult"/> per row it moved.
        /// </summary>
        public IReadOnlyList<EditResult> DismissLowest(EditSize size = EditSize.One)
        {
            var results = new List<EditResult>();
            int want = Men(size);
            foreach (var row in TroopPlanner.DismissOrder(Rows))
            {
                if (want <= 0)
                    break;
                int room = row.Change + row.MaxSell; // down to −MaxSell
                if (room <= 0)
                    continue;
                var result = SetChange(row.Id, row.Change - Math.Min(room, want));
                results.Add(result);
                want -= Math.Max(0, result.Before - result.After);
            }
            return results;
        }

        /// <summary>
        /// [+] on the folded Troops line: <paramref name="size"/> men (1, 5, or all on offer) recruited, the highest tier on
        /// offer first (<see cref="TroopPlanner.RecruitOrder"/>); a type the purse cannot pay is passed for the next (a lower
        /// tier costs less). A type with dismissals queued takes those back first. One <see cref="EditResult"/> per row tried.
        /// </summary>
        public IReadOnlyList<EditResult> RecruitBest(EditSize size = EditSize.One)
        {
            var results = new List<EditResult>();
            int want = Men(size);
            foreach (var row in TroopPlanner.RecruitOrder(Rows))
            {
                if (want <= 0)
                    break;
                int room = row.MaxBuy - row.Change;
                if (room <= 0)
                    continue;
                var result = SetChange(row.Id, row.Change + Math.Min(room, want));
                results.Add(result);
                want -= Math.Max(0, result.After - result.Before);
            }
            return results;
        }

        /// <summary>Why the folded line's [−] cannot move a man now (<see cref="EditBlock.None"/> = it can): nobody left who may
        /// go (<see cref="EditBlock.NoneToDismiss"/>) or everybody already dismissed (<see cref="EditBlock.AllDismissed"/>).</summary>
        public EditBlock DismissLowestBlock
        {
            get
            {
                bool any = false;
                foreach (var row in TroopPlanner.DismissOrder(Rows))
                {
                    if (row.Change > -row.MaxSell)
                        return EditBlock.None;
                    any |= row.MaxSell > 0;
                }
                return any ? EditBlock.AllDismissed : EditBlock.NoneToDismiss;
            }
        }

        /// <summary>Why the folded line's [+] cannot recruit now (<see cref="EditBlock.None"/> = it can): nobody offers a troop
        /// here (<see cref="EditBlock.NotOnOfferHere"/>), every volunteer is already in the plan
        /// (<see cref="EditBlock.AllOnOffer"/>), or the purse cannot pay even the cheapest (<see cref="EditBlock.NotEnoughGold"/>).</summary>
        public EditBlock RecruitBestBlock
        {
            get
            {
                var order = TroopPlanner.RecruitOrder(Rows);
                if (order.Count == 0)
                    return EditBlock.NotOnOfferHere;
                var first = EditBlock.AllOnOffer;
                foreach (var row in order)
                {
                    if (row.Change >= row.MaxBuy)
                        continue;
                    var block = row.IncreaseBlock;
                    if (block == EditBlock.None)
                        return EditBlock.None;
                    if (first == EditBlock.AllOnOffer)
                        first = block;
                }
                return first;
            }
        }

        private static int Men(EditSize size) => size == EditSize.One ? 1 : size == EditSize.Five ? 5 : int.MaxValue;
    }
}
