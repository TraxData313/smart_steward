using System;
using System.Collections.Generic;
using SmartSteward.Core.Pricing;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The rows the player's hand is on (<see cref="PlanRow.IsTouched"/>) when the steward plans again around them — the
    /// live re-plan (DESIGN §1.1, step 15). A pinned row keeps its quantity; the planners walk it FIRST in its phase and
    /// plan only the untouched rows, with what the pinned rows left.
    /// </summary>
    /// <remarks>
    /// The precedence [decided: Claude, 2026.09.28 — step 15]: in every phase of the walk (food sales · animal sales · loot
    /// sales · food buys · animal buys) the player's rows go before the steward's, so the player's rows take the stock, the
    /// market's gold and a category's price room first. And because a phase can only see what came before it, the steward's
    /// rows also leave room for the player's rows of LATER phases: its sales leave the market the gold the player's later
    /// sales need (<see cref="SellGoldAfterFood"/>, <see cref="SellGoldAfterAnimals"/>), its buys leave the purse what the
    /// player's later buys and hires cost (<see cref="SpendAfterFood"/>, <see cref="SpendAfterAnimals"/>) — then the money
    /// floors, as ever. The amounts are read from the plan's last walk: a phase's own categories are the only ones its
    /// prices walk in, so they hold.
    /// </remarks>
    internal sealed class PlanPins
    {
        private readonly Dictionary<string, int> _changes;

        public static readonly PlanPins None = new PlanPins(new Dictionary<string, int>(StringComparer.Ordinal), 0, 0, 0, 0);

        private PlanPins(Dictionary<string, int> changes, int sellGoldAfterFood, int sellGoldAfterAnimals, int spendAfterFood,
            int spendAfterAnimals)
        {
            _changes = changes;
            SellGoldAfterFood = sellGoldAfterFood;
            SellGoldAfterAnimals = sellGoldAfterAnimals;
            SpendAfterFood = spendAfterFood;
            SpendAfterAnimals = spendAfterAnimals;
        }

        public bool IsEmpty => _changes.Count == 0;

        /// <summary>Market gold the steward's FOOD sales leave for the player's animal and loot sales.</summary>
        public int SellGoldAfterFood { get; }

        /// <summary>Market gold the steward's ANIMAL sales leave for the player's loot sales.</summary>
        public int SellGoldAfterAnimals { get; }

        /// <summary>Gold the steward's FOOD buys leave for the player's animal buys and hires.</summary>
        public int SpendAfterFood { get; }

        /// <summary>Gold the steward's ANIMAL buys leave for the player's hires.</summary>
        public int SpendAfterAnimals { get; }

        /// <summary>The player's quantity for this row, if the row is pinned.</summary>
        public bool TryGet(string rowId, out int change) => _changes.TryGetValue(rowId, out change);

        public bool Contains(string rowId) => _changes.ContainsKey(rowId);

        /// <summary>The pinned rows' ids.</summary>
        public IEnumerable<string> Ids => _changes.Keys;

        /// <summary>Pins every touched row of a plan at its quantity, with what it sells and spends in the plan's last walk.</summary>
        public static PlanPins From(IEnumerable<PlanRow> rows)
        {
            var changes = new Dictionary<string, int>(StringComparer.Ordinal);
            int animalSales = 0, lootSales = 0, animalBuys = 0, hires = 0;
            foreach (var row in rows)
            {
                if (!row.IsTouched)
                    continue;
                changes[row.Id] = row.Change;
                if (row.Type == RowType.Tavern)
                {
                    hires += Math.Max(0, row.Change) * (row.Tavern?.UnitPrice ?? 0);
                    continue;
                }
                int sold = 0, bought = 0;
                foreach (var tally in row.Tallies)
                {
                    if (tally.Direction == TradeDirection.Sell) sold += tally.Gold;
                    else bought += tally.Gold;
                }
                if (row.Type == RowType.Loot)
                    lootSales += sold;
                else if (row.Section == PlanSectionKind.Mounts)
                {
                    animalSales += sold;
                    animalBuys += bought;
                }
            }
            return new PlanPins(changes, animalSales + lootSales, lootSales, animalBuys + hires, hires);
        }
    }
}
