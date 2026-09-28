using System;
using System.Collections.Generic;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The party's load and carrying capacity AFTER the deal, on land and at sea — the footer's weight line
    /// (Anton 2026.09.28, playtest round 3: <c>Weight 1,000 +120 kg » 1,120 kg · capacity land 1,500 / sea 1,000</c>, the
    /// part over a capacity in red). The game's own numbers now (<see cref="CarryInfo"/>, read from its model) plus what
    /// the deal moves, at the vanilla formula's per-unit rates (RESEARCH §19): the goods bought and sold (their weight on
    /// land and at sea), the mounts and pack animals (land capacity), the hires (capacity everywhere, and a mounted man's
    /// horse at sea), the prisoners who leave (capacity, with Forced Labor only). Pure arithmetic, live with every click.
    /// </summary>
    public sealed class CarryTotals
    {
        /// <summary>The capacity was read (<see cref="CarryInfo.Known"/>) — else only the weight change is known.</summary>
        public bool Known { get; private set; }

        /// <summary>Load on land now and after the deal (kg).</summary>
        public double WeightNow { get; private set; }
        public double WeightAfter { get; private set; }

        public double CapacityLandNow { get; private set; }
        public double CapacityLandAfter { get; private set; }

        /// <summary>The party has ships (War Sails): the sea part is shown.</summary>
        public bool ShowSea { get; private set; }

        /// <summary>Load at sea now and after the deal — animals and mounted troops' horses weigh there.</summary>
        public double WeightAtSeaNow { get; private set; }
        public double WeightAtSeaAfter { get; private set; }

        public double CapacitySeaNow { get; private set; }
        public double CapacitySeaAfter { get; private set; }

        /// <summary>Mounts and pack animals the deal adds (negative: removes) — what moves the land capacity.</summary>
        public int MountsChange { get; private set; }
        public int PackAnimalsChange { get; private set; }

        /// <summary>Kilos over the land capacity after the deal (0 when within it or unknown).</summary>
        public double OverLand => Known ? Math.Max(0, WeightAfter - CapacityLandAfter) : 0;

        /// <summary>Kilos over the sea capacity after the deal (0 when within it, or no ships).</summary>
        public double OverSea => ShowSea ? Math.Max(0, WeightAtSeaAfter - CapacitySeaAfter) : 0;

        /// <summary>At sea the party carries noticeably more than on land (its horses) — the line says so.</summary>
        public bool SeaLoadDiffers => ShowSea && Math.Abs(WeightAtSeaAfter - WeightAfter) >= 0.5;

        internal static CarryTotals Compute(IEnumerable<PlanRow> rows, CarryInfo? carry, double weightChange,
            int prisonersNow, int prisonersAfter)
        {
            carry ??= new CarryInfo();
            int mounts = 0, pack = 0, hired = 0;
            double seaWeightChange = 0;
            foreach (var row in rows)
            {
                foreach (var tally in row.Tallies)
                {
                    if (tally.Count == 0)
                        continue;
                    int moved = tally.Direction == TradeDirection.Buy ? tally.Count : -tally.Count;
                    seaWeightChange += moved * tally.Stack.UnitWeightAtSea;
                    if (tally.Stack.Kind == ItemKind.Mount)
                        mounts += moved;
                    else if (tally.Stack.Kind == ItemKind.PackAnimal)
                        pack += moved;
                }
                if (row.Type == RowType.Tavern && row.Change > 0)
                {
                    hired += row.Change;
                    if (row.Tavern?.Kind == TavernRowKind.Mercenaries)
                        seaWeightChange += row.Change * Math.Max(0, row.Tavern.SeaWeightPerMan);
                }
            }

            // The wounded leave first (the executor's rule), so healthy prisoners drop only once no wounded are left.
            int healthyNow = Math.Max(0, carry.HealthyPrisoners);
            int healthyChange = Math.Min(healthyNow, Math.Max(0, prisonersAfter)) - healthyNow;

            var totals = new CarryTotals
            {
                Known = carry.Known,
                WeightNow = carry.WeightNow,
                WeightAfter = Math.Max(0, carry.WeightNow + weightChange),
                MountsChange = mounts,
                PackAnimalsChange = pack,
            };
            if (!totals.Known)
                return totals;

            totals.CapacityLandNow = carry.CapacityLandNow;
            totals.CapacityLandAfter = Math.Max(GameRules.CapacityBase, carry.CapacityLandNow
                + hired * carry.LandPerMember + mounts * carry.LandPerMount + pack * carry.LandPerPackAnimal
                + healthyChange * carry.LandPerPrisoner);

            totals.ShowSea = carry.HasShips;
            if (totals.ShowSea)
            {
                totals.WeightAtSeaNow = carry.WeightAtSeaNow;
                totals.WeightAtSeaAfter = Math.Max(0, carry.WeightAtSeaNow + seaWeightChange);
                totals.CapacitySeaNow = carry.CapacitySeaNow;
                totals.CapacitySeaAfter = Math.Max(GameRules.CapacityBase, carry.CapacitySeaNow
                    + hired * carry.SeaPerMember + healthyChange * carry.SeaPerPrisoner);
            }
            return totals;
        }
    }
}
