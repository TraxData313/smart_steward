using System;
using System.Collections.Generic;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    public sealed class PlanSection
    {
        internal PlanSection(PlanSectionKind kind, IReadOnlyList<PlanRow> rows)
        {
            Kind = kind;
            Rows = rows;
        }

        public PlanSectionKind Kind { get; }
        public IReadOnlyList<PlanRow> Rows { get; }
    }

    /// <summary>The numbers the planners derived — for tooltips, the log and tests.</summary>
    public sealed class PlanFacts
    {
        /// <summary>Members + half the prisoners the deal leaves (when FoodCountPrisoners), like the game.</summary>
        public int FoodEaters { get; internal set; }
        public int FoodTarget { get; internal set; }

        /// <summary>Food is sold only while held is ABOVE this: target × (1 + tolerance/100).</summary>
        public double FoodSellAbove { get; internal set; }
        public int PackTarget { get; internal set; }
        public int Footmen { get; internal set; }
        public int RidingTarget { get; internal set; }

        /// <summary>Mounts counting toward the riding target now (unreserved + reserved when
        /// WarMountsCountAsMounts).</summary>
        public int RidingCounted { get; internal set; }

        /// <summary>Upgrade horses needed per category (only categories the party's troops upgrade into).</summary>
        public IReadOnlyDictionary<string, int> UpgradeNeed { get; internal set; } =
            new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Held horses reserved for upgrades per category (the cheapest unlocked first).</summary>
        public IReadOnlyDictionary<string, int> UpgradeReserved { get; internal set; } =
            new Dictionary<string, int>(StringComparer.Ordinal);
    }

    /// <summary>The header and footer (DESIGN §1.1).</summary>
    public sealed class PlanTotals
    {
        public int GoldNow { get; internal set; }
        public int Earned { get; internal set; }
        public int Spent { get; internal set; }
        public int GoldAfter => GoldNow + Earned - Spent;
        public int GoldChange => Earned - Spent;

        public int MarketGold { get; internal set; }

        /// <summary>What the market pays for the items sold (ransom is not paid by the market).</summary>
        public int MarketSales { get; internal set; }
        public bool ExceedsMarketGold => MarketSales > MarketGold;

        /// <summary>Food items held (livestock excluded — the steward's own measure).</summary>
        public int FoodUnitsNow { get; internal set; }
        public int FoodUnitsAfter { get; internal set; }
        public int LivestockFoodUnits { get; internal set; }

        /// <summary>≈ days the food lasts, the game's way: (food + livestock meat) / daily use; null when the
        /// party eats nothing.</summary>
        public double? FoodDaysNow { get; internal set; }
        public double? FoodDaysAfter { get; internal set; }

        public double WeightFreed { get; internal set; }
        public double WeightAdded { get; internal set; }
        public double WeightChange => WeightAdded - WeightFreed;

        public double InfluenceGained { get; internal set; }
        public int MembersAfter { get; internal set; }
        public int PrisonersAfter { get; internal set; }

        /// <summary>Something is bought or hired and the purse ends below MinGoldAfterDeal — the plan's
        /// <see cref="MoneyFloors.All"/> (shown red).</summary>
        public bool BelowMinGoldAfterDeal { get; internal set; }

        /// <summary>An animal is bought and the purse ends below MinGoldForHorses — the plan's
        /// <see cref="MoneyFloors.Animals"/> (shown red).</summary>
        public bool BelowMinGoldForHorses { get; internal set; }

        /// <summary>The deal costs more than the party has — or leaves a wanderer's hire with no more gold than
        /// his price (vanilla wants more). Only the player's edits can get here; the window should not run it.</summary>
        public bool CannotAfford => GoldAfter < 0 || HireUnaffordable;

        internal bool HireUnaffordable { get; set; }

        internal static PlanTotals Compute(IEnumerable<PlanRow> rows, StewardSnapshot snapshot, MoneyFloors floors)
        {
            var totals = new PlanTotals
            {
                GoldNow = snapshot.PlayerGold,
                MarketGold = snapshot.MarketGold,
                LivestockFoodUnits = Math.Max(0, snapshot.Party.LivestockFoodUnits),
            };

            int foodNow = 0;
            foreach (var stack in snapshot.Inventory ?? new List<ItemStack>())
                if (stack != null && stack.Kind == ItemKind.Food)
                    foodNow += stack.Count;
            int prisonersNow = 0;
            foreach (var p in snapshot.Prisoners ?? new List<PrisonerStack>())
                if (p != null && p.Count > 0)
                    prisonersNow += p.Count;

            int foodChange = 0, prisonersMoved = 0, hired = 0;
            bool bought = false, animalBought = false;
            foreach (var row in rows)
            {
                if (row.GoldDelta > 0) totals.Earned += row.GoldDelta;
                else totals.Spent -= row.GoldDelta;
                if (row.WeightDelta > 0) totals.WeightAdded += row.WeightDelta;
                else totals.WeightFreed -= row.WeightDelta;
                totals.InfluenceGained += row.InfluenceDelta;

                foreach (var tally in row.Tallies)
                {
                    if (tally.Direction == TradeDirection.Sell)
                        totals.MarketSales += tally.Gold;
                    else if (tally.Count > 0)
                    {
                        bought = true;
                        if (row.Section == PlanSectionKind.Mounts)
                            animalBought = true;
                    }
                }

                switch (row.Type)
                {
                    case RowType.Food:
                        foodChange += row.Change;
                        break;
                    case RowType.Prisoner:
                        prisonersMoved -= row.Change;
                        break;
                    case RowType.Tavern:
                        if (row.Change > 0)
                        {
                            hired += row.Change;
                            bought = true;
                        }
                        break;
                }
            }

            totals.FoodUnitsNow = foodNow;
            totals.FoodUnitsAfter = foodNow + foodChange;
            totals.MembersAfter = snapshot.Party.Members + hired;
            totals.PrisonersAfter = prisonersNow - prisonersMoved;
            totals.BelowMinGoldAfterDeal = bought && totals.GoldAfter < floors.All;
            totals.BelowMinGoldForHorses = animalBought && totals.GoldAfter < floors.Animals;

            double daily = snapshot.Party.DailyFoodUse;
            if (daily > 0)
            {
                // The game eats (members + prisoners/2) / 20 a day, perks multiplying it (RESEARCH §2), so the
                // daily use after the deal scales with its eaters — no need to know the 20 or the perks.
                int eatersNow = GameEaters(snapshot.Party.Members, prisonersNow);
                int eatersAfter = GameEaters(totals.MembersAfter, totals.PrisonersAfter);
                double dailyAfter = daily * eatersAfter / eatersNow;
                totals.FoodDaysNow = (foodNow + totals.LivestockFoodUnits) / daily;
                totals.FoodDaysAfter = Math.Max(0, totals.FoodUnitsAfter + totals.LivestockFoodUnits) / dailyAfter;
            }
            return totals;
        }

        /// <summary>The game's eater count: members + prisoners/2 (integer halves), at least 1.</summary>
        internal static int GameEaters(int members, int prisoners) => Math.Max(1, members + prisoners / 2);
    }

    /// <summary>
    /// The steward's proposal for one visit: the sections in DESIGN §1.1's order, the totals, and the facts
    /// behind them. Deterministic for a given snapshot, settings and price oracle. The player edits it in place
    /// (PlanEditing.cs): rows, totals and <see cref="Transactions"/> follow every click.
    /// </summary>
    public sealed partial class StewardPlan
    {
        private readonly PlanInputs? _inputs;

        internal StewardPlan(IReadOnlyList<PlanSection> sections, PlanTotals totals, PlanFacts facts, PlanInputs? inputs)
        {
            Sections = sections;
            Totals = totals;
            Facts = facts;
            _inputs = inputs;
            foreach (var row in Rows)
                row.Owner = this;
        }

        /// <summary>Only the sections that have rows, in order: tavern, food, mounts, armour &amp; weapons, prisoners.</summary>
        public IReadOnlyList<PlanSection> Sections { get; }

        /// <summary>The header and footer — replaced after every edit.</summary>
        public PlanTotals Totals { get; private set; }

        /// <summary>What the steward derived when planning (targets, needs) — edits do not change them.</summary>
        public PlanFacts Facts { get; }

        /// <summary>Who the plan was made for (the window, or the autonomous steward).</summary>
        public PlanMode Mode => _inputs?.Mode ?? PlanMode.Window;

        /// <summary>The purse floors the plan answers to — the red footer flags compare against these.</summary>
        public MoneyFloors Floors => _inputs?.Floors ?? new MoneyFloors(0, 0);

        /// <summary>At least one row changes something — PopupOnlyWithChanges opens the window only then.</summary>
        public bool HasChanges
        {
            get
            {
                foreach (var row in Rows)
                    if (row.HasChange)
                        return true;
                return false;
            }
        }

        public IEnumerable<PlanRow> Rows
        {
            get
            {
                foreach (var section in Sections)
                    foreach (var row in section.Rows)
                        yield return row;
            }
        }

        public PlanSection? Section(PlanSectionKind kind)
        {
            foreach (var section in Sections)
                if (section.Kind == kind)
                    return section;
            return null;
        }

        public PlanRow? FindRow(string id)
        {
            foreach (var row in Rows)
                if (string.Equals(row.Id, id, StringComparison.Ordinal))
                    return row;
            return null;
        }
    }
}
