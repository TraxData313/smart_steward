using System;
using System.Collections.Generic;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The game's overburden speed penalty (RESEARCH §25, read in game-decompiled-1.4.8): <c>DefaultPartySpeedCalculatingModel.
    /// GetOverburdenedEffect</c> on land and War Sails' <c>NavalDLCPartySpeedCalculationModel.GetOverburdenedEffect</c> at sea —
    /// an <c>ExplainedNumber(−rate × extraWeight / capacity)</c> with the perks as factors, ADDED to the party's base speed
    /// (<c>AddFromExplainedNumber</c>: <c>BaseNumber += result</c>) when the load is over the capacity (the capacity cast to
    /// int). No cap on it.
    /// </summary>
    public static class Overburden
    {
        /// <summary>Land: <c>OverburdenedEffect = −0.4f</c> per capacity over.</summary>
        public const double LandRate = 0.4;

        /// <summary>At sea (War Sails): <c>OverburdenedEffect = −1f</c> per capacity over.</summary>
        public const double SeaRate = 1.0;

        /// <summary>The land model's <c>BaseSpeed</c>.</summary>
        public const double BaseSpeed = 4.0;

        /// <summary>The land base speed of a party of <paramref name="men"/> (<c>CalculateBaseSpeedForParty</c>:
        /// <c>4 × (200 / (200 + men))^0.4</c>) — every factor multiplies it afterwards.</summary>
        public static double LandBaseSpeed(int men) => BaseSpeed * Math.Pow(200.0 / (200.0 + Math.Max(0, men)), 0.4);

        /// <summary>The speed points the load takes off the base: <c>rate × (load − capacity) / capacity × (1 + perks)</c> when the
        /// load is over the (whole-number) capacity, else 0. <paramref name="perks"/> = the perks' factors summed (−0.2 each).</summary>
        public static double Penalty(double load, double capacity, double rate, double perks)
        {
            double cap = Math.Floor(capacity);
            if (cap <= 0 || load <= cap)
                return 0;
            return rate * (load - cap) / cap * Math.Max(0, 1 + perks);
        }
    }

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

        /// <summary>The speed the load after the deal takes on LAND (PLAN step 20, RESEARCH §25 — the game's "Overburdened"):
        /// the share of the party's speed lost, 0 = none (0.12 = 12% slower). An army pools its attached parties, as the game
        /// does. 0 when the capacity was not read.</summary>
        public double LandSlowdown { get; private set; }

        /// <summary>The same in the game's speed points — the party-speed tooltip's "Overburdened" line (land).</summary>
        public double LandSpeedLoss { get; private set; }

        /// <summary>The speed the load after the deal would take AT SEA (War Sails' own rule, RESEARCH §25): the share lost when
        /// the ships' speed is known, else 0 — see <see cref="SeaSpeedLoss"/>.</summary>
        public double SeaSlowdown { get; private set; }

        /// <summary>The same in speed points (at sea).</summary>
        public double SeaSpeedLoss { get; private set; }

        /// <summary>The sea slowdown is a share of the ships' speed (the fleet's speed was read) — else only the points are known.</summary>
        public bool SeaSlowdownKnown { get; private set; }

        /// <summary>
        /// The overburden slowdowns for the load and capacity after the deal (RESEARCH §25): the game adds
        /// <c>−rate × (load − capacity) / capacity × (1 + perks)</c> to the party's BASE speed (land: rate 0.4, base
        /// <c>4 × (200 / (200 + men))^0.4</c>, perks Athletics.Energetic and Scouting.Unburdened; sea: rate 1.0, base the fleet's
        /// <c>(average ship speed + slowest ship speed) / 2</c>, perk Boatswain.VeteransWisdom) before every factor multiplies the
        /// base — so the share of speed lost is the penalty over the base, whatever the other factors (unless the game's floor of
        /// 1 speed bites).
        /// </summary>
        internal void ComputeSlowdown(int menAfter, CarryInfo carry, AttachedParties? attached)
        {
            carry ??= new CarryInfo();
            attached ??= new AttachedParties();
            if (Known)
            {
                double load = WeightAfter + Math.Max(0, attached.Weight);
                double capacity = CapacityLandAfter + Math.Max(0, attached.Capacity);
                LandSpeedLoss = Overburden.Penalty(load, capacity, Overburden.LandRate, carry.OverburdenPerksLand);
                LandSlowdown = LandSpeedLoss / Overburden.LandBaseSpeed(menAfter);
            }
            if (ShowSea)
            {
                // At sea the game pools the attached parties' loads but weighs them against the leader's capacity alone.
                SeaSpeedLoss = Overburden.Penalty(WeightAtSeaAfter + Math.Max(0, attached.Weight), CapacitySeaAfter,
                    Overburden.SeaRate, carry.OverburdenPerksSea);
                SeaSlowdownKnown = carry.FleetBaseSpeed > 0;
                SeaSlowdown = SeaSlowdownKnown ? SeaSpeedLoss / carry.FleetBaseSpeed : 0;
            }
        }

        internal static CarryTotals Compute(IEnumerable<PlanRow> rows, CarryInfo? carry, double weightChange,
            int prisonersNow, int prisonersAfter)
        {
            carry ??= new CarryInfo();
            int mounts = 0, pack = 0, hired = 0;
            double seaWeightChange = 0;
            foreach (var row in rows)
            {
                // The row's load at sea: its goods and animals, a mounted man's horse (the spreadsheet's Sea kg, step 20).
                seaWeightChange += PlanMetrics.Of(row).SeaKg;
                foreach (var tally in row.Tallies)
                {
                    if (tally.Count == 0)
                        continue;
                    int moved = tally.Direction == TradeDirection.Buy ? tally.Count : -tally.Count;
                    if (tally.Stack.Kind == ItemKind.Mount)
                        mounts += moved;
                    else if (tally.Stack.Kind == ItemKind.PackAnimal)
                        pack += moved;
                }
                if (row.Type == RowType.Tavern && row.Change > 0)
                    hired += row.Change;
                else if (row.Type == RowType.Troop && row.Troop != null && row.Change != 0)
                    // Step 16: recruits join healthy; dismissals take the wounded first, so the healthy members — the ones
                    // that carry — drop only once no wounded are left.
                    hired += row.Change > 0 ? row.Change : -row.Troop.HealthyAmong(-row.Change);
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
