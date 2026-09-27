using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// Food (DESIGN §2.1): keep ceil(eaters × FoodPerMan) units, varied, fairly priced. One row per food
    /// item. Buying and selling never happen in one visit: food is sold only above the target plus the
    /// tolerance, and bought only below the target.
    /// </summary>
    internal sealed class FoodPlanner
    {
        private sealed class Line
        {
            public Line(PlanRow row, List<ItemStack> heldStacks, WalkLine sell)
            {
                Row = row;
                HeldStacks = heldStacks;
                Sell = sell;
            }

            public PlanRow Row { get; }
            public List<ItemStack> HeldStacks { get; }

            /// <summary>The sell walk; its Held is the units held as the plan goes.</summary>
            public WalkLine Sell { get; }
        }

        private readonly PlanContext _ctx;
        private readonly List<Line> _lines = new List<Line>();
        private readonly bool _active;

        public FoodPlanner(PlanContext ctx, int prisonersAfter)
        {
            _ctx = ctx;
            var settings = ctx.Settings;

            // A prisoner eats half a man's ration — the game halves with integer division (RESEARCH §2).
            // Prisoners the deal ransoms or donates eat nothing more, so the target counts those left.
            Eaters = Math.Max(0, ctx.Snapshot.Party.Members)
                     + (settings.FoodCountPrisoners ? Math.Max(0, prisonersAfter) / 2 : 0);
            Target = PlanMath.Ceiling(Eaters * Math.Max(0, settings.FoodPerMan));
            SellAbove = Math.Round(Target * (1 + Math.Max(0, settings.FoodSurplusTolerancePercent) / 100.0), 6);

            _active = settings.FoodEnabled && ctx.Snapshot.CanTrade;
            if (!_active)
                return;

            var heldByItem = ctx.Inventory(ItemKind.Food).GroupBy(s => s.ItemId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            var offeredByItem = ctx.MarketStacks(ItemKind.Food).GroupBy(s => s.ItemId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            var items = heldByItem.Keys.Concat(offeredByItem.Keys).Distinct(StringComparer.Ordinal)
                .Select(id =>
                {
                    heldByItem.TryGetValue(id, out var held);
                    offeredByItem.TryGetValue(id, out var offered);
                    return new
                    {
                        Id = id,
                        Held = held ?? new List<ItemStack>(),
                        Offered = offered ?? new List<ItemStack>(),
                    };
                })
                .Select(x => new { x.Id, x.Held, x.Offered, First = x.Held.Concat(x.Offered).First() })
                .OrderBy(x => x.First.Name, StringComparer.Ordinal)
                .ThenBy(x => x.Id, StringComparer.Ordinal);

            foreach (var item in items)
            {
                var book = ctx.Book(item.First)!;
                var buyLane = book.BuyTicked && book.FinalMaxBuy != null
                    ? new TradeLane(TradeDirection.Buy, LanePick.Cheapest,
                        item.Offered.Select(s => new LaneStack(s, s.Count, book.FinalMaxBuy)))
                    : TradeLane.Empty(TradeDirection.Buy);
                var sellLane = book.SellTicked
                    ? new TradeLane(TradeDirection.Sell, LanePick.MostExpensive,
                        item.Held.Where(s => !s.IsLocked).Select(s => new LaneStack(s, s.Count, book.FinalMinSell)))
                    : TradeLane.Empty(TradeDirection.Sell);

                int held = item.Held.Sum(s => s.Count);
                if (held == 0 && buyLane.Capacity == 0)
                    continue; // on the market but never buyable (unticked, or no price to judge it by)

                var row = new PlanRow("food:" + item.Id, PlanSectionKind.Food, RowType.Food)
                {
                    Name = item.First.Name,
                    ItemId = item.Id,
                    CategoryId = item.First.CategoryId,
                    Mine = held,
                    Locked = item.Held.Sum(s => s.LockedCount),
                    Market = item.Offered.Sum(s => s.Count),
                    MaxBuy = PlanMath.EligibleOnOffer(buyLane, ctx.Market),
                    MaxSell = sellLane.Capacity,
                    PriceBook = book,
                    BuyLane = buyLane,
                    SellLane = sellLane,
                };
                _lines.Add(new Line(row, item.Held, new WalkLine(row, sellLane, held, book: row.Book)));
            }
        }

        public int Eaters { get; }
        public int Target { get; }
        public double SellAbove { get; }

        public IReadOnlyList<PlanRow> Rows => _lines.Select(l => l.Row).ToList();

        private int TotalHeld => _lines.Sum(l => l.Sell.Held);

        /// <summary>Surplus: only above target × (1 + tolerance), back down to the target — the most-held type
        /// first (keeps variety), only Sell-ticked types at ≥ their min sell, never beyond the market's gold.</summary>
        public void PlanSells()
        {
            if (!_active || !_ctx.Settings.SellFoodSurplus)
                return;
            if (TotalHeld <= SellAbove)
                return;
            PlanWalk.SellMostHeldFirst(_ctx.Walk, _lines.Select(l => l.Sell).ToList(), () => TotalHeld > Target);
        }

        /// <summary>Below the target: one unit at a time — Balanced: the allowed type held the fewest of, ties →
        /// the cheapest; Cheapest: the cheapest. Never below MinGoldAfterDeal.</summary>
        public void PlanBuys()
        {
            if (!_active)
                return;
            // No row sells and buys in one visit (sold only above target + tolerance, bought only below the
            // target), so the buy walk starts from the units held after the sales.
            var buys = _lines.Select(l => new WalkLine(l.Row, l.Row.BuyLane!, l.Sell.Held, book: l.Row.Book)).ToList();
            int total = TotalHeld;
            PlanWalk.BuyFood(_ctx.Walk, buys, _ctx.Settings.FoodStrategy == FoodStrategy.Balanced,
                () => _ctx.Gold - _ctx.FoodFloor, () => total + buys.Sum(b => b.Cursor.Moved) < Target);
        }

        public void Finish()
        {
            foreach (var line in _lines)
                PlanMath.FinishItemRow(line.Row,
                    line.HeldStacks.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
        }
    }
}
