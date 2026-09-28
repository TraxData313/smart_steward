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

        public PlanContext(StewardSnapshot snapshot, StewardSettings settings, IPriceOracle oracle, PlanMode mode)
        {
            Snapshot = snapshot;
            Settings = settings;
            Oracle = oracle;
            Mode = mode;
            Floors = MoneyFloors.For(settings, mode);
            Walk = new WalkState(new MarketState(oracle, snapshot.MarketGold), snapshot.PlayerGold);
        }

        public StewardSnapshot Snapshot { get; }
        public StewardSettings Settings { get; }
        public IPriceOracle Oracle { get; }
        public PlanMode Mode { get; }

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
