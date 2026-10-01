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

        /// <summary>The even share per food kind: ceil(target / food rows) — a goal counts toward the target up to it, the
        /// surplus sale never takes a kind below it (step 25).</summary>
        public int FoodShare { get; internal set; }
        public int PackTarget { get; internal set; }
        /// <summary>Men on foot in the party after the deal (the game's <c>NumberOfMenWithoutHorse</c> + the footmen hired).</summary>
        public int Footmen { get; internal set; }

        /// <summary>T: the horses kept for the footmen — ceil(footmen × MountsPer100Footmen / 100) (step 17, <see cref="MountGoal"/>).</summary>
        public int MountTarget { get; internal set; }

        /// <summary>The riding horses the plan aims for: T minus the war, noble and lame horses kept after the deal.</summary>
        public int RidingTarget { get; internal set; }

        /// <summary>W: the war horses to keep (WarMountsToKeep; 0 when war horses are not managed).</summary>
        public int WarTarget { get; internal set; }

        /// <summary>The switched-on jobs whose activation threshold the purse before the deal did not reach (round 4,
        /// <see cref="JobThresholds"/>) → the denari each needs; in <see cref="JobThresholds.All"/>'s order. Empty = every job
        /// acts. The section overviews say "starts at 20,000 denari".</summary>
        public IReadOnlyList<KeyValuePair<ManagedJob, int>> Waiting { get; internal set; } =
            Array.Empty<KeyValuePair<ManagedJob, int>>();

        /// <summary>The threshold a job waits for; null when it acts (or is switched off).</summary>
        public int? StartsAt(ManagedJob job)
        {
            foreach (var pair in Waiting)
                if (pair.Key == job)
                    return pair.Value;
            return null;
        }
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

        /// <summary>The load and carrying capacity after the deal, on land and at sea (round 3) — the footer's weight line.</summary>
        public CarryTotals Carry { get; internal set; } = new CarryTotals();

        /// <summary>The horses after the deal against the most before the herd slows the party (step 18) — the footer's herd
        /// line, the game's own rule (RESEARCH §23).</summary>
        public HerdTotals Herd { get; internal set; } = new HerdTotals();

        public double InfluenceGained { get; internal set; }

        /// <summary>The party after the deal (hires and recruits added, dismissed men gone) — against <see cref="PartySizeLimit"/>
        /// in the footer.</summary>
        public int MembersAfter { get; internal set; }

        /// <summary>The party size limit — information, never a wall (Anton 2026.09.28, playtest round 3: hire past it,
        /// just show <c>Party 99/96</c>, red when over).</summary>
        public int PartySizeLimit { get; internal set; }

        /// <summary>The party after the deal is bigger than its size limit (shown red; blocks nothing).</summary>
        public bool OverPartyLimit => PartySizeLimit > 0 && MembersAfter > PartySizeLimit;

        public int PrisonersAfter { get; internal set; }

        /// <summary>Party members now (the Total line's "party 104 » 105", step 20).</summary>
        public int MembersNow { get; internal set; }

        /// <summary>Prisoners now (the Total line's "prisoners 52 » 0").</summary>
        public int PrisonersNow { get; internal set; }

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
                PartySizeLimit = Math.Max(0, snapshot.Party.PartySizeLimit),
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
                    case RowType.Troop: // step 16: recruits join (paid), dismissed men leave
                        if (row.Change > 0)
                            bought = true;
                        hired += row.Change;
                        break;
                }
            }

            totals.FoodUnitsNow = foodNow;
            totals.FoodUnitsAfter = foodNow + foodChange;
            totals.MembersAfter = Math.Max(0, snapshot.Party.Members + hired);
            totals.PrisonersAfter = prisonersNow - prisonersMoved;
            totals.Carry = CarryTotals.Compute(rows, snapshot.Carry, totals.WeightChange, prisonersNow, totals.PrisonersAfter);
            totals.Herd = HerdTotals.Compute(rows, snapshot);
            totals.Carry.ComputeSlowdown(totals.Herd.Men, snapshot.Carry, snapshot.Party.Attached);
            totals.MembersNow = Math.Max(0, snapshot.Party.Members);
            totals.PrisonersNow = prisonersNow;
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

        /// <summary>Only the sections that have rows, in order: tavern, food, mounts, armour &amp; weapons, prisoners. Replaced
        /// only when a live re-plan adds or removes a row (<see cref="Layout"/>).</summary>
        public IReadOnlyList<PlanSection> Sections { get; private set; }

        /// <summary>Bumps when a live re-plan changed WHICH rows the plan has (an upgrade row appears or goes with the party
        /// after the deal) — the window rebuilds its table then; otherwise the row objects stay and only their numbers move.</summary>
        public int Layout { get; private set; }

        /// <summary>The header and footer — replaced after every edit.</summary>
        public PlanTotals Totals { get; private set; }

        /// <summary>What the steward derived when planning (targets, needs) — for the party after the deal: a party-changing
        /// edit re-plans and replaces them (step 15).</summary>
        public PlanFacts Facts { get; private set; }

        /// <summary>The snapshot the plan was made from (the spreadsheet's overviews read the party and the market) — null for
        /// a plan made without inputs.</summary>
        internal Snapshot.StewardSnapshot? Snapshot => _inputs?.Snapshot;

        /// <summary>The settings the plan was made with.</summary>
        internal StewardSettings? Settings => _inputs?.Settings;

        /// <summary>The dungeon's room here in men (donations).</summary>
        internal int DungeonRoom => _inputs?.DungeonRoom ?? 0;

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

        /// <summary>
        /// Takes over a re-plan of this plan (<see cref="StewardPlanner"/> with the touched rows pinned): every row by id
        /// adopts its re-planned self (<see cref="PlanRow.AdoptFrom"/>) — the objects stay, so the window's rows stay —, rows
        /// only the re-plan has join, rows it no longer has go (the untouched upgrade row of a kind nobody needs any more), and
        /// the facts are the party after the deal's. The totals and transactions follow from the caller's walk.
        /// </summary>
        internal void Adopt(StewardPlan planned)
        {
            var mine = new Dictionary<string, PlanRow>(StringComparer.Ordinal);
            foreach (var row in Rows)
                mine[row.Id] = row;
            bool changed = false;
            int count = 0;
            var sections = new List<PlanSection>();
            foreach (var section in planned.Sections)
            {
                var rows = new List<PlanRow>();
                foreach (var fresh in section.Rows)
                {
                    count++;
                    if (mine.TryGetValue(fresh.Id, out var live))
                    {
                        live.AdoptFrom(fresh);
                        rows.Add(live);
                    }
                    else
                    {
                        fresh.Owner = this;
                        rows.Add(fresh);
                        changed = true;
                    }
                }
                sections.Add(new PlanSection(section.Kind, rows));
            }
            if (changed || count != mine.Count)
            {
                Sections = sections;
                Layout++;
            }
            Facts = planned.Facts;
        }
    }
}
