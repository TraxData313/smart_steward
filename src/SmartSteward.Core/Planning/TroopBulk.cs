using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The troops section's two aggregate lines (PLAN step 18, reworked in step 20 — Anton 2026.09.28, round 4: "collapse them
    /// and leave one line with just troops and when I press - there drop the lowest tier unit, if I press + add them back the
    /// same order I dropped them, in some brackets show me what im dropping"; mockup choice 8: "Recruits only hire (best tier
    /// first), Your troops only dismiss (lowest tier first, wounded first; [+] re-adds in reverse) — a type you have that is also
    /// on offer shows in both"):
    /// <list type="bullet">
    /// <item><b>Your troops</b> (<see cref="YourTroopRows"/>): [−] <see cref="DismissLowest"/> drops men lowest tier first;
    ///   [+] <see cref="ReAddDropped"/> takes them back in the REVERSE order they were dropped — an undo stack on the line.</item>
    /// <item><b>Recruits</b> (<see cref="RecruitRows"/>): [+] <see cref="RecruitBest"/> hires the highest tier on offer first;
    ///   [−] <see cref="TakeBackRecruits"/> gives them back in the reverse order.</item>
    /// </list>
    /// Each line moves only its own side of a row: Your troops never touches a row with recruits queued, Recruits never one with
    /// dismissals queued (a type recruited AND dismissed in one deal would be pointless). They are ORDINARY row edits underneath
    /// — each moves troop rows with <see cref="SetChange"/>, so the rows are touched, the food and horses re-plan live, and Do
    /// it runs the same transactions; there is no new executor path. The stacks are checked when popped (a row the player
    /// moved by hand since may have less to give back) and fall back to the reverse of the line's order once empty; Reset all
    /// clears them.
    /// </summary>
    public sealed partial class StewardPlan
    {
        /// <summary>Your troops' [−], newest last: (row id, men dropped by that click).</summary>
        private readonly List<KeyValuePair<string, int>> _dropped = new List<KeyValuePair<string, int>>();

        /// <summary>Recruits' [+], newest last: (row id, men recruited by that click).</summary>
        private readonly List<KeyValuePair<string, int>> _recruited = new List<KeyValuePair<string, int>>();

        /// <summary>The Your troops line's rows: every troop type the party holds and may dismiss, lowest tier first
        /// (<see cref="TroopPlanner.DismissOrder"/>) — a type also on offer is in <see cref="RecruitRows"/> too.</summary>
        public IReadOnlyList<PlanRow> YourTroopRows => TroopPlanner.DismissOrder(Rows);

        /// <summary>The Recruits line's rows: every type on offer here, highest tier first (<see cref="TroopPlanner.RecruitOrder"/>).</summary>
        public IReadOnlyList<PlanRow> RecruitRows => TroopPlanner.RecruitOrder(Rows);

        /// <summary>
        /// Your troops' [−]: <paramref name="size"/> men (1, 5, or every man) leave the party after the deal, the lowest tier
        /// first (<see cref="TroopPlanner.DismissOrder"/>; the executor takes the wounded first within a type). Rows with
        /// recruits queued are left to the Recruits line. One <see cref="EditResult"/> per row it moved.
        /// </summary>
        public IReadOnlyList<EditResult> DismissLowest(EditSize size = EditSize.One)
        {
            var results = new List<EditResult>();
            int want = Men(size);
            foreach (var row in TroopPlanner.DismissOrder(Rows))
            {
                if (want <= 0)
                    break;
                int room = row.Change > 0 ? 0 : row.Change + row.MaxSell; // down to −MaxSell
                if (room <= 0)
                    continue;
                var result = SetChange(row.Id, row.Change - Math.Min(room, want));
                results.Add(result);
                int moved = Math.Max(0, result.Before - result.After);
                if (moved > 0)
                    _dropped.Add(new KeyValuePair<string, int>(row.Id, moved));
                want -= moved;
            }
            return results;
        }

        /// <summary>
        /// Your troops' [+]: <paramref name="size"/> men dropped on this line come back, in the REVERSE order they were dropped
        /// (Anton: "add them back the same order I dropped them" — undone newest first); once the line's own record is used up,
        /// the rows still dropping men give them back highest tier first.
        /// </summary>
        public IReadOnlyList<EditResult> ReAddDropped(EditSize size = EditSize.One) =>
            Undo(_dropped, Men(size), -1, () => TroopPlanner.DismissOrder(Rows).Where(r => r.Change < 0).Reverse());

        /// <summary>
        /// Recruits' [+]: <paramref name="size"/> men (1, 5, or all on offer) recruited, the highest tier on offer first
        /// (<see cref="TroopPlanner.RecruitOrder"/>); a type the purse cannot pay is passed for the next (a lower tier costs
        /// less). Rows with dismissals queued are left to the Your troops line. One <see cref="EditResult"/> per row tried.
        /// </summary>
        public IReadOnlyList<EditResult> RecruitBest(EditSize size = EditSize.One)
        {
            var results = new List<EditResult>();
            int want = Men(size);
            foreach (var row in TroopPlanner.RecruitOrder(Rows))
            {
                if (want <= 0)
                    break;
                int room = row.Change < 0 ? 0 : row.MaxBuy - row.Change;
                if (room <= 0)
                    continue;
                var result = SetChange(row.Id, row.Change + Math.Min(room, want));
                results.Add(result);
                int moved = Math.Max(0, result.After - result.Before);
                if (moved > 0)
                    _recruited.Add(new KeyValuePair<string, int>(row.Id, moved));
                want -= moved;
            }
            return results;
        }

        /// <summary>Recruits' [−]: <paramref name="size"/> recruits given back, the newest first; then the lowest tier first.</summary>
        public IReadOnlyList<EditResult> TakeBackRecruits(EditSize size = EditSize.One) =>
            Undo(_recruited, Men(size), +1, () => TroopPlanner.RecruitOrder(Rows).Where(r => r.Change > 0).Reverse());

        /// <summary>
        /// Pops a line's record newest first: each entry gives back at most what its row still has on this side
        /// (<paramref name="side"/> −1: dismissals, +1: recruits); what is left of a half-used entry goes back on the stack. Then
        /// the <paramref name="fallback"/> rows, in order.
        /// </summary>
        private List<EditResult> Undo(List<KeyValuePair<string, int>> stack, int want, int side, Func<IEnumerable<PlanRow>> fallback)
        {
            var results = new List<EditResult>();
            while (want > 0 && stack.Count > 0)
            {
                var last = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                var row = FindRow(last.Key);
                int onSide = row == null ? 0 : Math.Max(0, side * row.Change);
                int n = Math.Min(Math.Min(last.Value, onSide), want);
                if (n <= 0)
                    continue;
                var result = SetChange(row!.Id, row.Change - side * n);
                results.Add(result);
                int moved = Math.Abs(result.After - result.Before);
                want -= moved;
                if (last.Value - moved > 0 && moved > 0)
                    stack.Add(new KeyValuePair<string, int>(last.Key, last.Value - moved));
                if (moved < n)
                    break; // the walk would not go further (should not happen: toward zero always works)
            }
            foreach (var row in fallback().ToList())
            {
                if (want <= 0)
                    break;
                int n = Math.Min(Math.Max(0, side * row.Change), want);
                if (n <= 0)
                    continue;
                var result = SetChange(row.Id, row.Change - side * n);
                results.Add(result);
                want -= Math.Abs(result.After - result.Before);
            }
            return results;
        }

        /// <summary>Reset all forgets both lines' records.</summary>
        private void ClearTroopLines()
        {
            _dropped.Clear();
            _recruited.Clear();
        }

        /// <summary>Why Your troops' [−] cannot drop a man now (<see cref="EditBlock.None"/> = it can): nobody left who may go
        /// (<see cref="EditBlock.NoneToDismiss"/>) or everybody already dropped (<see cref="EditBlock.AllDismissed"/>).</summary>
        public EditBlock DismissLowestBlock
        {
            get
            {
                bool dropped = false;
                foreach (var row in TroopPlanner.DismissOrder(Rows))
                {
                    if (row.Change <= 0 && row.Change > -row.MaxSell)
                        return EditBlock.None;
                    dropped |= row.Change < 0;
                }
                return dropped ? EditBlock.AllDismissed : EditBlock.NoneToDismiss;
            }
        }

        /// <summary>Why Your troops' [+] cannot bring a man back (<see cref="EditBlock.NothingDropped"/>: nobody is dropped).</summary>
        public EditBlock ReAddDroppedBlock =>
            TroopPlanner.DismissOrder(Rows).Any(r => r.Change < 0) ? EditBlock.None : EditBlock.NothingDropped;

        /// <summary>Why Recruits' [+] cannot recruit now (<see cref="EditBlock.None"/> = it can): nobody offers a troop here
        /// (<see cref="EditBlock.NotOnOfferHere"/>), every volunteer is already in the plan (<see cref="EditBlock.AllOnOffer"/>),
        /// or the purse cannot pay even the cheapest (<see cref="EditBlock.NotEnoughGold"/>).</summary>
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
                    if (row.Change < 0 || row.Change >= row.MaxBuy)
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

        /// <summary>Why Recruits' [−] cannot give a recruit back (<see cref="EditBlock.NothingRecruited"/>: none queued).</summary>
        public EditBlock TakeBackRecruitsBlock =>
            TroopPlanner.RecruitOrder(Rows).Any(r => r.Change > 0) ? EditBlock.None : EditBlock.NothingRecruited;

        private static int Men(EditSize size) => size == EditSize.One ? 1 : size == EditSize.Five ? 5 : int.MaxValue;
    }
}
