using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartSteward.Core.Planning
{
    /// <summary>Why a part's "Do" button is greyed, or why its deal is smaller than the same rows inside Do it.</summary>
    public enum PartBlock
    {
        /// <summary>The button works (or: nothing was cut).</summary>
        None,

        /// <summary>The part moves nothing in the plan as it stands.</summary>
        NothingToDo,

        /// <summary>Alone, its purchases would take the purse below their floor (MinGoldAfterDeal, the animal floor, a goal's floor) —
        /// inside the whole deal the other parts' sales and ransoms pay for them.</summary>
        PurseFloor,

        /// <summary>Alone, the purse cannot pay for it (a hire, a recruit, a goal bought without a floor) — inside the whole deal
        /// the other parts' income pays.</summary>
        NotEnoughGold,
    }

    /// <summary>
    /// What one part of the plan does ALONE (PLAN step 27, DESIGN §1.1 "Do just this part"): the plan's own transactions of that
    /// part — the player's edits and goals included, the very prices the plan walked — cut only where the purse, without the
    /// rest of the deal, cannot carry them: a purchase stops at the floor it answers to in Do it, a hire or recruit at what the
    /// purse pays. <see cref="Transactions"/> is the executor's list; the counts and denari are the button's hover.
    /// </summary>
    public sealed class PartDeal
    {
        internal PartDeal(PlanPart part)
        {
            Part = part;
        }

        public PlanPart Part { get; }

        /// <summary>What the executor runs, in <see cref="StewardPlan.Transactions"/> order (cut where the purse alone stops it).</summary>
        public IReadOnlyList<PlanTransaction> Transactions { get; internal set; } = Array.Empty<PlanTransaction>();

        /// <summary>The part's transactions in the plan before any cut — what the same rows do inside Do it.</summary>
        public int Planned { get; internal set; }

        public int Ransomed { get; internal set; }
        public int Donated { get; internal set; }
        public int Sold { get; internal set; }
        public int Bought { get; internal set; }

        /// <summary>Wanderers and mercenaries hired.</summary>
        public int Hired { get; internal set; }
        public int Recruited { get; internal set; }
        public int Dismissed { get; internal set; }

        /// <summary>The denari the part makes (+) or costs (−) at the plan's prices.</summary>
        public int Gold { get; internal set; }

        /// <summary>The influence the donations bring.</summary>
        public double Influence { get; internal set; }

        /// <summary>Units (items, men) fewer than the same rows do inside Do it — 0 when the part runs whole.</summary>
        public int CutUnits { get; internal set; }

        /// <summary>Why <see cref="CutUnits"/> &gt; 0: the floor (<see cref="PartBlock.PurseFloor"/>, <see cref="Floor"/> denari) or the
        /// empty purse (<see cref="PartBlock.NotEnoughGold"/>).</summary>
        public PartBlock CutBy { get; internal set; }

        /// <summary>The purse floor that cut a purchase (for the words); 0 otherwise.</summary>
        public int Floor { get; internal set; }

        /// <summary>None = the button works; else why it is greyed (nothing to do, or everything cut).</summary>
        public PartBlock Block { get; internal set; }

        public bool CanRun => Block == PartBlock.None;
    }

    public sealed partial class StewardPlan
    {
        /// <summary>
        /// The deal <paramref name="part"/> makes ALONE (PLAN step 27): the plan's transactions of the part — every row edit and goal
        /// as it stands, the plan's prices (a town's price walks per item category and every category lives in one section, so a
        /// part's prices alone are the whole deal's) — walked through the purse as the executor will: ransoms and sales first,
        /// then each purchase, which stops at the floor its row answers to in Do it (the steward's food at MinGoldAfterDeal, its
        /// animals at the higher animal floor, a goal at its own — <see cref="MoneyFloors.ForGoals"/>), then the hires and recruits,
        /// which stop where the purse no longer pays (a wanderer needs MORE than his price, as in vanilla). Inside Do it the
        /// rest of the deal's income pays for them; alone it may not — the deal says how many fewer (<see cref="PartDeal.CutUnits"/>).
        /// A row cut once buys nothing more (its next stack would be priced on units it never bought).
        /// </summary>
        public PartDeal DealOf(PlanPart part)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            var deal = new PartDeal(part);
            var mine = new List<(PlanTransaction Tx, PlanRow Row)>();
            if (part.RowId != null)
            {
                // Step 29: a part of one row (every line has a button now — the sheet asks for each one after every click).
                var single = FindRow(part.RowId);
                if (single != null)
                    foreach (var tx in Transactions)
                        if (string.Equals(tx.RowId, part.RowId, StringComparison.Ordinal) && part.Includes(single, tx))
                            mine.Add((tx, single));
            }
            else
            {
                var rows = new Dictionary<string, PlanRow>(StringComparer.Ordinal);
                foreach (var r in Rows)
                    if (!rows.ContainsKey(r.Id))
                        rows[r.Id] = r;
                foreach (var tx in Transactions)
                {
                    if (rows.TryGetValue(tx.RowId, out var row) && part.Includes(row, tx))
                        mine.Add((tx, row));
                }
            }
            deal.Planned = mine.Count;
            if (mine.Count == 0)
            {
                deal.Block = PartBlock.NothingToDo;
                return deal;
            }

            int purse = Totals.GoldNow;
            var stopped = new HashSet<string>(StringComparer.Ordinal);
            var kept = new List<PlanTransaction>();
            bool cutByFloor = false, cutByPurse = false;
            foreach (var (tx, row) in mine)
            {
                int n = tx.Count;
                switch (tx.Kind)
                {
                    case TransactionKind.Ransom:
                    case TransactionKind.Sell:
                        purse += tx.Gold;
                        break;
                    case TransactionKind.Buy:
                    {
                        if (stopped.Contains(row.Id))
                        {
                            n = 0;
                            break;
                        }
                        int floor = PartFloorOf(row);
                        n = 0;
                        foreach (int price in tx.UnitPrices)
                        {
                            if (price > purse) { cutByPurse = true; break; }
                            if (purse - price < floor)
                            {
                                cutByFloor = true;
                                deal.Floor = Math.Max(deal.Floor, floor);
                                break;
                            }
                            purse -= price;
                            n++;
                        }
                        if (n < tx.Count)
                            stopped.Add(row.Id);
                        break;
                    }
                    case TransactionKind.HireWanderer:
                    {
                        int price = tx.UnitPrices.Count > 0 ? tx.UnitPrices[0] : 0;
                        n = purse > price ? tx.Count : 0; // vanilla's hire wants MORE gold than his price
                        if (n == 0) cutByPurse = true;
                        purse -= n * price;
                        break;
                    }
                    case TransactionKind.HireMercenaries:
                    case TransactionKind.Recruit:
                    {
                        int price = tx.UnitPrices.Count > 0 ? tx.UnitPrices[0] : 0;
                        if (price > 0 && Math.Max(0, purse) / price < n)
                        {
                            n = Math.Max(0, purse) / price;
                            cutByPurse = true;
                        }
                        purse -= n * price;
                        break;
                    }
                }
                deal.CutUnits += tx.Count - n;
                if (n <= 0)
                    continue;
                var done = n == tx.Count ? tx : tx.Cut(n);
                kept.Add(done);
                switch (done.Kind)
                {
                    case TransactionKind.Donate:
                        deal.Donated += n;
                        deal.Influence += done.Influence;
                        break;
                    case TransactionKind.Ransom:
                        deal.Ransomed += n;
                        deal.Gold += done.Gold;
                        break;
                    case TransactionKind.Sell:
                        deal.Sold += n;
                        deal.Gold += done.Gold;
                        break;
                    case TransactionKind.Buy:
                        deal.Bought += n;
                        deal.Gold -= done.Gold;
                        break;
                    case TransactionKind.HireWanderer:
                    case TransactionKind.HireMercenaries:
                        deal.Hired += n;
                        deal.Gold -= done.Gold;
                        break;
                    case TransactionKind.Recruit:
                        deal.Recruited += n;
                        deal.Gold -= done.Gold;
                        break;
                    case TransactionKind.Dismiss:
                        deal.Dismissed += n;
                        break;
                }
            }
            deal.Transactions = kept;
            if (deal.CutUnits > 0)
                deal.CutBy = cutByFloor ? PartBlock.PurseFloor : cutByPurse ? PartBlock.NotEnoughGold : PartBlock.None;
            if (kept.Count == 0)
                deal.Block = deal.CutBy == PartBlock.None ? PartBlock.NothingToDo : deal.CutBy;
            return deal;
        }

        /// <summary>The floor a purchase of <paramref name="row"/> answers to in Do it, whoever carries it out: the steward's food at
        /// MinGoldAfterDeal, its animals at the higher animal floor; a goal of the player's (or a quest's food goal) at the goals'
        /// floors — none with ManualGoalsKeepPurseFloor off (DESIGN §3).</summary>
        private int PartFloorOf(PlanRow row)
        {
            var floors = Floors;
            bool animal = row.Section == PlanSectionKind.Mounts;
            if (row.TakesGoal && row.WalksFirst && Settings != null)
            {
                var goals = MoneyFloors.ForGoals(Settings, Mode);
                return (animal ? goals.Animals : goals.Food) ?? 0;
            }
            return animal ? Math.Max(floors.All, floors.Animals) : floors.All;
        }
    }
}
