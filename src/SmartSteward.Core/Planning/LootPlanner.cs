using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// Armour &amp; weapons (DESIGN §2.6): only ever SOLD, in bulk, one row per loot group — never bought, no
    /// price book, no averages. Locked pieces and pieces above SellLootMaxItemValue are never sold and are not
    /// in the row's Mine.
    /// </summary>
    /// <remarks>
    /// Each group's pieces are ordered once by SellLootOrder, judged at the untouched market's price
    /// (Cheapest, LowestPricePerKg or MostExpensive); "−N" on a group row always means "the first N in that
    /// order". The steward walks all groups together in that same order — so a poor village's gold goes to the
    /// pieces the order prefers, whatever their group — and a group stops at its first piece the market can no
    /// longer pay for (DESIGN §3.4: never a sale the market cannot pay).
    /// </remarks>
    internal sealed class LootPlanner
    {
        private sealed class Group
        {
            public Group(int index, PlanRow row, LaneCursor sell, List<ItemStack> held)
            {
                Index = index;
                Row = row;
                Sell = sell;
                Held = held;
            }

            public int Index { get; }
            public PlanRow Row { get; }
            public LaneCursor Sell { get; }
            public List<ItemStack> Held { get; }
            public bool Stopped { get; set; }
        }

        private readonly PlanContext _ctx;
        private readonly List<Group> _groups = new List<Group>();
        private readonly Comparison<ItemStack> _order;

        public LootPlanner(PlanContext ctx)
        {
            _ctx = ctx;
            var settings = ctx.Settings;
            var startPrice = new Dictionary<string, int>(StringComparer.Ordinal);
            _order = Order(settings.SellLootOrder, stack =>
            {
                if (!startPrice.TryGetValue(stack.Key, out var price))
                {
                    price = ctx.Market.QuoteAt(stack, TradeDirection.Sell, 0);
                    startPrice[stack.Key] = price;
                }
                return price;
            });

            if (!ctx.Snapshot.CanTrade || !settings.SellLoot || !settings.SellLootEquipment)
                return;

            int cap = settings.SellLootMaxItemValue;
            var equipment = ctx.Inventory(ItemKind.Equipment).ToList();
            for (int index = 0; index < LootGroups.All.Count; index++)
            {
                var group = LootGroups.All[index];
                var held = equipment.Where(s => s.LootGroup == group).ToList();
                var sellable = held.Where(s => !s.IsLocked && (cap <= 0 || s.UnitValue <= cap)).ToList();
                if (sellable.Count == 0)
                    continue;
                sellable.Sort(_order);
                var lane = new TradeLane(TradeDirection.Sell, LanePick.InOrder,
                    sellable.Select(s => new LaneStack(s, s.Count, null)));
                var row = new PlanRow("loot:" + group, PlanSectionKind.ArmourAndWeapons, RowType.Loot)
                {
                    LootGroup = group,
                    Mine = lane.Capacity,
                    Locked = held.Sum(s => s.LockedCount),
                    OverValueCap = held.Where(s => !s.IsLocked && cap > 0 && s.UnitValue > cap).Sum(s => s.Count),
                    Market = null,
                    MaxSell = lane.Capacity,
                    SellLane = lane,
                };
                _groups.Add(new Group(index, row, new LaneCursor(lane), sellable));
            }
        }

        public IReadOnlyList<PlanRow> Rows => _groups.Select(g => g.Row).ToList();

        /// <summary>
        /// The static order of SellLootOrder, at the untouched market's sell price; ties → the lower value,
        /// then the stack key. Weightless pieces come last under LowestPricePerKg.
        /// </summary>
        internal static Comparison<ItemStack> Order(SellLootOrder order, Func<ItemStack, int> startPrice)
        {
            switch (order)
            {
                case SellLootOrder.MostExpensive:
                    return (a, b) =>
                    {
                        int c = startPrice(b).CompareTo(startPrice(a));
                        if (c == 0) c = b.UnitValue.CompareTo(a.UnitValue);
                        return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
                    };
                case SellLootOrder.LowestPricePerKg:
                    return (a, b) =>
                    {
                        int c = PerKg(a, startPrice).CompareTo(PerKg(b, startPrice));
                        if (c == 0) c = startPrice(a).CompareTo(startPrice(b));
                        return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
                    };
                default:
                    return (a, b) =>
                    {
                        int c = startPrice(a).CompareTo(startPrice(b));
                        if (c == 0) c = a.UnitValue.CompareTo(b.UnitValue);
                        return c != 0 ? c : string.CompareOrdinal(a.Key, b.Key);
                    };
            }
        }

        private static double PerKg(ItemStack stack, Func<ItemStack, int> startPrice) =>
            stack.UnitWeight > 0 ? startPrice(stack) / stack.UnitWeight : double.PositiveInfinity;

        public void PlanSells()
        {
            var market = _ctx.Market;
            while (true)
            {
                Group? best = null;
                UnitQuote bestQuote = default;
                foreach (var group in _groups)
                {
                    if (group.Stopped)
                        continue;
                    var quote = group.Sell.Peek(market);
                    if (quote == null)
                        continue;
                    if (best == null || _order(quote.Value.Stack, bestQuote.Stack) < 0)
                    {
                        best = group;
                        bestQuote = quote.Value;
                    }
                }
                if (best == null)
                    return;
                if (bestQuote.Price > market.MarketGoldLeft)
                {
                    best.Stopped = true;
                    continue;
                }
                PlanMath.Take(best.Row, best.Sell, bestQuote, market);
                _ctx.Gold += bestQuote.Price;
            }
        }

        public void Finish()
        {
            foreach (var group in _groups)
                PlanMath.FinishItemRow(group.Row,
                    group.Held.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
        }
    }
}
