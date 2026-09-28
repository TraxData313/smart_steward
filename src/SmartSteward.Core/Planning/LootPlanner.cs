using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The Other section (DESIGN §2.6 — "Armour &amp; weapons" until round 4): only ever SOLD, in bulk, one row per loot group —
    /// Armour, Melee weapons, Ranged, Shields (SellLootEquipment) and, since round 4, Other goods (SellLootOtherGoods: every
    /// trade good that is not food, an animal or equipment — Anton 2026.09.28: "a line there that combines all other stuff that
    /// I have not pinned, like coal, jewelry etc") — never bought, no price book, no averages. Locked pieces and pieces above
    /// SellLootMaxItemValue are never sold and are not in the row's Mine.
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
            public Group(PlanRow row, WalkLine sell, List<ItemStack> held)
            {
                Row = row;
                Sell = sell;
                Held = held;
            }

            public PlanRow Row { get; }
            public WalkLine Sell { get; }
            public List<ItemStack> Held { get; }
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

            if (!ctx.Snapshot.CanTrade || !settings.SellLoot)
                return;

            int cap = settings.SellLootMaxItemValue;
            var sellable = ctx.Inventory(ItemKind.Equipment).Concat(ctx.Inventory(ItemKind.Goods)).ToList();
            foreach (var group in LootGroups.All)
            {
                if (LootGroups.IsEquipment(group) ? !settings.SellLootEquipment : !settings.SellLootOtherGoods)
                    continue;
                var held = sellable.Where(s => s.LootGroup == group).ToList();
                var free = held.Where(s => !ctx.IsGuarded(s) && (cap <= 0 || s.UnitValue <= cap)).ToList();
                if (free.Count == 0)
                    continue;
                free.Sort(_order);
                var lane = new TradeLane(TradeDirection.Sell, LanePick.InOrder,
                    free.Select(s => new LaneStack(s, s.Count, null)));
                var row = new PlanRow("loot:" + group, PlanSectionKind.Other, RowType.Loot)
                {
                    LootGroup = group,
                    Mine = lane.Capacity,
                    Locked = held.Where(ctx.IsGuarded).Sum(s => s.Count),
                    LocksGuard = ctx.LockGuards(ItemKind.Equipment), // always: locks keep guarding armour and weapons
                    OverValueCap = held.Where(s => !ctx.IsGuarded(s) && cap > 0 && s.UnitValue > cap).Sum(s => s.Count),
                    Market = null,
                    MaxSell = lane.Capacity,
                    SellLane = lane,
                };
                _groups.Add(new Group(row, new WalkLine(row, lane, book: row.Book), free));
            }
        }

        public IReadOnlyList<PlanRow> Rows => _groups.Select(g => g.Row).ToList();

        /// <summary>The static SellLootOrder of this visit — the editor walks the groups by it too.</summary>
        public Comparison<ItemStack> SellOrder => _order;

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

        /// <summary>The player's own loot rows first (touched rows in a live re-plan, <see cref="PlanPins"/>), then the
        /// steward's: every group sold in the order, with the market's gold that is left.</summary>
        public void PlanSells()
        {
            var mine = new List<WalkLine>();
            var steward = new List<WalkLine>();
            foreach (var group in _groups)
            {
                if (_ctx.Pins.TryGet(group.Row.Id, out int pin))
                {
                    if (pin < 0)
                        mine.Add(new WalkLine(group.Row, group.Row.SellLane!, group.Row.Mine, -pin, group.Row.Book));
                }
                else
                    steward.Add(group.Sell);
            }
            if (mine.Count > 0)
                PlanWalk.SellInOrder(_ctx.Walk, mine, _order);
            PlanWalk.SellInOrder(_ctx.Walk, steward, _order);
        }

        public void Finish()
        {
            foreach (var group in _groups)
                PlanMath.FinishItemRow(group.Row,
                    group.Held.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
        }
    }
}
