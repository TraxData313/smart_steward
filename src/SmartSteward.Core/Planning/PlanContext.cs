using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>One planning run: the inputs, the market as the plan moves it, and the party's purse.</summary>
    internal sealed class PlanContext
    {
        private readonly Dictionary<string, PriceBookPrices> _books =
            new Dictionary<string, PriceBookPrices>(StringComparer.Ordinal);

        public PlanContext(StewardSnapshot snapshot, StewardSettings settings, IPriceOracle oracle, PlanMode mode,
            PlanPins? pins = null, int warPledge = 0, IReadOnlyDictionary<string, int>? goals = null)
        {
            Snapshot = snapshot;
            Settings = settings;
            Oracle = oracle;
            Mode = mode;
            Pins = pins ?? PlanPins.None;
            WarPledge = Math.Max(0, warPledge);
            Goals = goals ?? ManualGoals.CopyOf(settings);
            Party = PartyAfter.Of(snapshot);
            Floors = MoneyFloors.For(settings, mode);
            GoalFloors = MoneyFloors.ForGoals(settings, mode);
            Quests = new QuestKeep(snapshot, settings);
            Walk = new WalkState(new MarketState(oracle, snapshot.MarketGold), snapshot.PlayerGold);
        }

        /// <summary>What the player's quests keep and ask for (step 26, DESIGN §2.9) — <see cref="QuestKeep.None"/>-like when the
        /// switch is off or no quest wants anything.</summary>
        public QuestKeep Quests { get; }

        /// <summary>The player's standing goals by row id (the plan's own copy — <see cref="ManualGoals"/>).</summary>
        public IReadOnlyDictionary<string, int> Goals { get; }

        /// <summary>The floors the manual goals answer to (<see cref="MoneyFloors.ForGoals"/>); null = none.</summary>
        public (int? Food, int? Animals) GoalFloors { get; }

        /// <summary>The goals walk their own lanes without the price limits (<c>ManualGoalsObeyPriceCaps</c> off).</summary>
        public bool GoalsIgnoreCaps => !Settings.ManualGoalsObeyPriceCaps;

        /// <summary>
        /// The player's quantity for a row — walked first in its phase, never re-planned (the live re-plan's pins, step 15):
        /// for a row that takes a goal, YOUR GOAL (round 5 — <c>goal − Mine</c>; 0 while it waits for <paramref name="job"/>'s
        /// threshold under <c>ManualGoalsWaitForThresholds</c>), marking the row as yours; for any other row its touched
        /// quantity. Null = the steward's row.
        /// <para>Step 26 (DESIGN §2.9): a food a quest asks for and the party holds less of is an automatic goal — pinned at
        /// <c>need − Mine</c>, walked first like a goal of yours under the same switches (it waits with the steward under
        /// <c>ManualGoalsWaitForThresholds</c>), but NOT touched (no ⟲: it follows the quest). A goal of yours wins.</para>
        /// </summary>
        public int? PinOf(PlanRow row, ManagedJob? job)
        {
            if (!row.TakesGoal)
                return Pins.TryGet(row.Id, out int pin) ? pin : (int?)null;
            if (!Goals.TryGetValue(row.Id, out int goal))
            {
                var quest = row.Quest;
                if (quest?.FoodGoal == null || row.Mine >= quest.FoodGoal.Value)
                    return null;
                if (job != null && Settings.ManualGoalsWaitForThresholds && !JobActive(job.Value))
                {
                    row.GoalShort = GoalShort.Threshold;
                    return null; // it waits with the steward - the food it holds is still kept
                }
                quest.Buys = true;
                return quest.FoodGoal.Value - row.Mine;
            }
            row.ManualGoal = goal;
            row.IsTouched = true;
            if (job != null && Settings.ManualGoalsWaitForThresholds && !JobActive(job.Value))
            {
                row.GoalWaits = true;
                row.GoalShort = goal == row.Mine ? GoalShort.None : GoalShort.Threshold;
                return 0;
            }
            return goal - row.Mine;
        }

        /// <summary>The most a goal's next food unit may cost: the purse above the goals' food floor (null = no floor).</summary>
        public int? GoalFoodCeiling(WalkState walk) => GoalFloors.Food == null ? (int?)null : walk.Gold - GoalFloors.Food.Value;

        /// <summary>The most a goal's next animal may cost.</summary>
        public int? GoalAnimalCeiling(WalkState walk) =>
            GoalFloors.Animals == null ? (int?)null : walk.Gold - GoalFloors.Animals.Value;

        /// <summary>A lane without its price limits — a goal's own lane when the goals ignore the caps (the ticks decided the
        /// stacks already).</summary>
        public static TradeLane Unlimited(TradeLane lane) =>
            new TradeLane(lane.Direction, lane.Pick, lane.Stacks.Select(s => new LaneStack(s.Stack, s.Available, null)));

        public StewardSnapshot Snapshot { get; }
        public StewardSettings Settings { get; }
        public IPriceOracle Oracle { get; }
        public PlanMode Mode { get; }

        /// <summary>The rows the player's hand is on (a live re-plan); <see cref="PlanPins.None"/> for a fresh plan.</summary>
        public PlanPins Pins { get; }

        /// <summary>The war horses the steward will buy in this plan, as a first pass found them (<see cref="StewardPlanner"/>) —
        /// so the riding surplus is sold against the war horses the party will HAVE after the deal, not only those it holds
        /// (step 17: R = T − war horses kept). 0 on the first pass.</summary>
        public int WarPledge { get; }

        /// <summary>The party the steward plans for — the snapshot's, with the plan's hires (and step 16's recruits and
        /// dismissals) applied (<see cref="PartyAfter"/>). Set by the planner once the party rows are known.</summary>
        public PartyAfter Party { get; set; }

        /// <summary>The most the steward's own FOOD sales may take of the market's gold: what is left, minus what the player's
        /// later sales need (<see cref="PlanPins"/>).</summary>
        public int? FoodSellCeiling() => Market.MarketGoldLeft - Pins.SellGoldAfterFood;

        /// <summary>The most the steward's own ANIMAL sales may take of the market's gold.</summary>
        public int? AnimalSellCeiling() => Market.MarketGoldLeft - Pins.SellGoldAfterAnimals;

        /// <summary>The most the steward's next food unit may cost: the purse above MinGoldAfterDeal, minus what the player's
        /// later buys and hires cost.</summary>
        public int? FoodBuyCeiling(WalkState walk) => walk.Gold - FoodFloor - Pins.SpendAfterFood;

        /// <summary>The most the steward's next animal may cost: the purse above both floors, minus the player's hires.</summary>
        public int? AnimalBuyCeiling(WalkState walk) => walk.Gold - AnimalFloor - Pins.SpendAfterAnimals;

        /// <summary>The purse floors in effect (raised while autonomous - DESIGN §6).</summary>
        public MoneyFloors Floors { get; }

        /// <summary>The market and the purse as the plan moves them.</summary>
        public WalkState Walk { get; }
        public MarketState Market => Walk.Market;

        /// <summary>The purse as the plan goes: gold now + earned − spent.</summary>
        public int Gold
        {
            get => Walk.Gold;
            set => Walk.Gold = value;
        }

        public bool IsTown => Snapshot.SettlementKind == SettlementKind.Town;

        /// <summary>Does the steward's own side of this job act — the purse before the deal at least its threshold
        /// (<see cref="JobThresholds"/>, round 4)? The player's own rows walk either way.</summary>
        public bool JobActive(ManagedJob job) => JobThresholds.IsActive(job, Settings, Snapshot.PlayerGold);

        /// <summary>The threshold a waiting job needs (for <see cref="PlanRow.StartsAtDenari"/>); null when it acts.</summary>
        public int? StartsAt(ManagedJob job) => JobActive(job) ? (int?)null : JobThresholds.Of(job, Settings);

        /// <summary>No purchase takes the purse below this.</summary>
        public int FoodFloor => Floors.All;

        /// <summary>No animal purchase takes the purse below this — animals answer to both floors.</summary>
        public int AnimalFloor => Math.Max(Floors.All, Floors.Animals);

        /// <summary>Does a lock guard stacks of this kind from sale (<see cref="LockRule"/>)?</summary>
        public bool LockGuards(ItemKind kind) => LockRule.Guards(kind, Settings);

        /// <summary>Locked and guarded by its lock: never sold, not counted as sellable (<see cref="LockRule"/>).</summary>
        public bool IsGuarded(ItemStack stack) => LockRule.IsGuarded(stack, Settings);

        public IEnumerable<ItemStack> Inventory(ItemKind kind) =>
            ByKey((Snapshot.Inventory ?? new List<ItemStack>()).Where(s => s != null && s.Kind == kind && s.Count > 0));

        public IEnumerable<ItemStack> MarketStacks(ItemKind kind) =>
            ByKey((Snapshot.Market ?? new List<ItemStack>()).Where(s => s != null && s.Kind == kind && s.Count > 0));

        /// <summary>The min sell price of one held stack: its item's final min sell scaled by the stack's modifier — a lame
        /// horse against a lame horse's worth (<see cref="PriceBook.MinSellOf"/>); null = any price.</summary>
        public int? MinSellOf(ItemStack stack) => PriceBook.MinSellOf(Book(stack)?.FinalMinSell, stack.ModifierPriceFactor);

        /// <summary>The resolved price-book row of a stack's item; null for items not in the V1 price book.</summary>
        public PriceBookPrices? Book(ItemStack stack)
        {
            var group = PriceBook.GroupOf(stack.Kind, stack.CategoryId);
            if (group == null)
                return null;
            if (_books.TryGetValue(stack.ItemId, out var cached))
                return cached;
            AveragePrices? averages = null;
            Snapshot.AveragePrices?.TryGetValue(stack.ItemId, out averages);
            var resolved = PriceBook.Resolve(stack.ItemId, group.Value, Settings, averages);
            _books[stack.ItemId] = resolved;
            return resolved;
        }

        private static IEnumerable<ItemStack> ByKey(IEnumerable<ItemStack> stacks) =>
            stacks.OrderBy(s => s.Key, StringComparer.Ordinal);
    }

    /// <summary>Small shared pieces of the planners.</summary>
    internal static class PlanMath
    {
        /// <summary>Ceiling that ignores binary slop (10 × 1.1 = 11.000000000000002 → 11).</summary>
        public static int Ceiling(double value) =>
            value <= 0 ? 0 : (int)Math.Ceiling(Math.Round(value, 6));

        /// <summary>Units on offer in a buy lane whose first unit passes its limit at the market's current state
        /// — the role rows' Market column.</summary>
        public static int EligibleOnOffer(TradeLane lane, MarketState market)
        {
            int sum = 0;
            foreach (var laneStack in lane.Stacks)
                if (laneStack.Accepts(lane.Direction, market.Quote(laneStack.Stack, lane.Direction)))
                    sum += Math.Min(laneStack.Available, market.StockLeft(laneStack.Stack));
            return sum;
        }

        /// <summary>
        /// Fills a row from its tallies, freezes the suggestion, and keeps what the row holds per stack
        /// (<paramref name="held"/>: stack → units in this row) for its breakdown.
        /// </summary>
        public static void FinishItemRow(PlanRow row, IEnumerable<KeyValuePair<ItemStack, int>> held)
        {
            row.HeldStacks = held.Where(p => p.Value > 0).ToList();
            RefreshItemRow(row);
            row.SuggestedChange = row.Change;
        }

        /// <summary>
        /// Change, prices, gold and weight of an item row from its tallies, and its per-stack breakdown from
        /// what it holds and the stacks its buy lane may take. The editor calls it after every re-walk.
        /// </summary>
        public static void RefreshItemRow(PlanRow row)
        {
            row.SumTallies();

            var lines = new Dictionary<string, PlanRowLine>(StringComparer.Ordinal);
            PlanRowLine Line(ItemStack stack)
            {
                if (!lines.TryGetValue(stack.Key, out var line))
                {
                    line = new PlanRowLine(stack);
                    lines[stack.Key] = line;
                }
                return line;
            }

            foreach (var pair in row.HeldStacks)
                Line(pair.Key).Mine += pair.Value;
            if (row.BuyLane != null)
                foreach (var laneStack in row.BuyLane.Stacks)
                    Line(laneStack.Stack).Market = laneStack.Available;
            var priced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tally in row.Tallies)
            {
                if (tally.Count == 0) continue;
                var line = Line(tally.Stack);
                int sign = tally.Direction == TradeDirection.Buy ? 1 : -1;
                bool first = priced.Add(tally.Stack.Key);
                line.Change += sign * tally.Count;
                line.GoldDelta -= sign * tally.Gold;
                line.UnitPriceMin = first ? tally.MinPrice : Math.Min(line.UnitPriceMin, tally.MinPrice);
                line.UnitPriceMax = first ? tally.MaxPrice : Math.Max(line.UnitPriceMax, tally.MaxPrice);
            }

            row.Breakdown = lines.Values
                .OrderBy(l => l.Name, StringComparer.Ordinal)
                .ThenBy(l => l.ModifierId ?? "", StringComparer.Ordinal)
                .ThenBy(l => l.StackKey, StringComparer.Ordinal)
                .ToList();
        }
    }
}
