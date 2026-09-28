using System;
using System.Collections.Generic;
using SmartSteward.Core.Planning;

namespace SmartSteward.Core.Presentation
{
    /// <summary>
    /// The numbers of one Suggestion-table row as the window shows them (DESIGN §1.1: Mine | Change | Result | Price |
    /// Market | Item | Type), computed from a <see cref="PlanRow"/> — pure, so the columns are tested. Words (the
    /// "locked" of <c>41 (+3 locked)</c>, the "influence" of a donation, the Type column) are added by the window
    /// through TextObjects; everything here is digits and the fonts' symbols.
    /// </summary>
    public sealed class RowCells
    {
        private RowCells()
        {
        }

        /// <summary>What the party holds now (loot: the sellable pieces; role rows: the units in the role).</summary>
        public string Mine { get; private set; } = "";

        /// <summary>Loot / animals: locked units shown apart ("+3 locked"); 0 = none.</summary>
        public int Locked { get; private set; }

        /// <summary>Loot: unlocked pieces kept by the value cap ("+2 kept"); 0 = none.</summary>
        public int OverValueCap { get; private set; }

        /// <summary><c>+7</c>, <c>–5</c>, <c>0</c>.</summary>
        public string Change { get; private set; } = "";

        public string Result { get; private set; } = "";

        /// <summary><c>3 × 180–240 = –630</c>; a tavern row (or a troop row on offer) at 0 shows its price to hire; empty when
        /// nothing moves, and for a dismissal (free).</summary>
        public string Price { get; private set; } = "";

        /// <summary>Prisoners donated: the influence they bring, <c>+2.4</c>; null = none.</summary>
        public string? Influence { get; private set; }

        /// <summary>Loot: the weight the sale frees, <c>–120.5</c>; null when the row frees nothing.</summary>
        public string? WeightFreed { get; private set; }

        /// <summary>How many the settlement has; "—" when the column does not apply (loot, prisoners, a wanderer).</summary>
        public string Market { get; private set; } = "";

        /// <summary>The row's direction colour: buy / hire green, sell / ransom red, untouched grey.</summary>
        public string Color { get; private set; } = UiColors.Muted;

        /// <summary>The row has a per-type breakdown behind a ▸ (the mount role rows, DESIGN §1.1.1).</summary>
        public bool HasBreakdown { get; private set; }

        public static RowCells Of(PlanRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            var cells = new RowCells
            {
                Mine = UiFormat.Money(row.Mine),
                Locked = Math.Max(0, row.Locked),
                OverValueCap = Math.Max(0, row.OverValueCap),
                Change = UiFormat.SignedCount(row.Change),
                Result = UiFormat.Money(row.Result),
                Market = row.Market == null ? UiFormat.None : UiFormat.Money(row.Market.Value),
                Color = UiColors.ForChange(row.Change),
                HasBreakdown = row.Role != null && row.Breakdown.Count > 0,
            };

            switch (row.Type)
            {
                case RowType.Prisoner:
                    var p = row.Prisoner;
                    if (p != null)
                    {
                        cells.Price = UiFormat.PriceCell(p.RansomCount, p.RansomValue, p.RansomValue, row.GoldDelta);
                        if (p.DonateCount > 0)
                            cells.Influence = UiFormat.SignedInfluence(row.InfluenceDelta);
                    }
                    break;
                case RowType.Tavern:
                    var t = row.Tavern;
                    if (t != null)
                        cells.Price = row.Change > 0
                            ? UiFormat.PriceCell(row.Change, t.UnitPrice, t.UnitPrice, row.GoldDelta)
                            : UiFormat.Money(t.UnitPrice);
                    break;
                case RowType.Troop:
                    // Step 16: recruits cost their price per man (a row on offer at 0 shows it); a dismissal is free — no price.
                    var troop = row.Troop;
                    if (troop != null)
                        cells.Price = row.Change > 0
                            ? UiFormat.PriceCell(row.Change, troop.UnitPrice, troop.UnitPrice, row.GoldDelta)
                            : row.Change == 0 && troop.OnOffer > 0 ? UiFormat.Money(troop.UnitPrice) : "";
                    break;
                default:
                    cells.Price = UiFormat.PriceCell(Math.Abs(row.Change), row.UnitPriceMin, row.UnitPriceMax, row.GoldDelta);
                    if (row.Type == RowType.Loot && row.WeightDelta < -0.05)
                        cells.WeightFreed = UiFormat.SignedWeight(row.WeightDelta);
                    break;
            }
            return cells;
        }

        /// <summary>One line of a role row's breakdown, in the same columns.</summary>
        public static RowCells Of(PlanRowLine line)
        {
            if (line == null) throw new ArgumentNullException(nameof(line));
            return new RowCells
            {
                Mine = UiFormat.Money(line.Mine),
                Change = UiFormat.SignedCount(line.Change),
                Result = UiFormat.Money(line.Mine + line.Change),
                Price = UiFormat.PriceCell(Math.Abs(line.Change), line.UnitPriceMin, line.UnitPriceMax, line.GoldDelta),
                Market = line.Market == null ? UiFormat.None : UiFormat.Money(line.Market.Value),
                Color = UiColors.ForChange(line.Change),
            };
        }
    }

    /// <summary>What the footer warns about (DESIGN §1.1, §3) — shown in red; only <see cref="CannotAfford"/> stops
    /// Do it (the player's hand overrides the floors).</summary>
    public enum PlanWarning
    {
        /// <summary>The deal costs more than the purse holds (or leaves a wanderer's hire unpaid).</summary>
        CannotAfford,

        /// <summary>A purchase takes the purse below MinGoldAfterDeal.</summary>
        BelowMinGoldAfterDeal,

        /// <summary>An animal purchase takes the purse below MinGoldForHorses.</summary>
        BelowMinGoldForHorses,

        /// <summary>The market cannot pay for all the sales.</summary>
        ExceedsMarketGold,
    }

    /// <summary>The footer's pure half.</summary>
    public static class PlanFooter
    {
        /// <summary>The warnings to show, most serious first.</summary>
        public static IReadOnlyList<PlanWarning> Warnings(PlanTotals totals)
        {
            if (totals == null) throw new ArgumentNullException(nameof(totals));
            var list = new List<PlanWarning>();
            if (totals.CannotAfford) list.Add(PlanWarning.CannotAfford);
            if (totals.BelowMinGoldAfterDeal) list.Add(PlanWarning.BelowMinGoldAfterDeal);
            if (totals.BelowMinGoldForHorses) list.Add(PlanWarning.BelowMinGoldForHorses);
            if (totals.ExceedsMarketGold) list.Add(PlanWarning.ExceedsMarketGold);
            return list;
        }

        /// <summary>Do it works when there is something to do and the purse can pay for it (DESIGN §1.1, the 4b
        /// note); the floors never stop it.</summary>
        public static bool CanExecute(StewardPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            return plan.Transactions.Count > 0 && !plan.Totals.CannotAfford;
        }
    }
}
