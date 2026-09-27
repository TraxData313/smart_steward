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
            public Line(PlanRow row, int held, List<ItemStack> heldStacks, LaneCursor buy, LaneCursor sell)
            {
                Row = row;
                Held = held;
                HeldStacks = heldStacks;
                Buy = buy;
                Sell = sell;
            }

            public PlanRow Row { get; }
            public int Held { get; set; }
            public List<ItemStack> HeldStacks { get; }
            public LaneCursor Buy { get; }
            public LaneCursor Sell { get; }
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
                    MaxBuy = buyLane.Capacity,
                    MaxSell = sellLane.Capacity,
                    PriceBook = book,
                    BuyLane = buyLane,
                    SellLane = sellLane,
                };
                _lines.Add(new Line(row, held, item.Held, new LaneCursor(buyLane), new LaneCursor(sellLane)));
            }
        }

        public int Eaters { get; }
        public int Target { get; }
        public double SellAbove { get; }

        public IReadOnlyList<PlanRow> Rows => _lines.Select(l => l.Row).ToList();

        private int TotalHeld => _lines.Sum(l => l.Held);

        /// <summary>Surplus: only above target × (1 + tolerance), back down to the target — the most-held type
        /// first (keeps variety), only Sell-ticked types at ≥ their min sell, never beyond the market's gold.</summary>
        public void PlanSells()
        {
            if (!_active || !_ctx.Settings.SellFoodSurplus)
                return;
            int total = TotalHeld;
            if (total <= SellAbove)
                return;

            var market = _ctx.Market;
            while (total > Target)
            {
                Line? best = null;
                UnitQuote bestQuote = default;
                foreach (var line in _lines)
                {
                    var quote = line.Sell.Peek(market, market.MarketGoldLeft);
                    if (quote == null)
                        continue;
                    if (best == null || line.Held > best.Held
                        || (line.Held == best.Held && quote.Value.Price > bestQuote.Price))
                    {
                        best = line;
                        bestQuote = quote.Value;
                    }
                }
                if (best == null)
                    break;
                PlanMath.Take(best.Row, best.Sell, bestQuote, market);
                best.Held--;
                total--;
                _ctx.Gold += bestQuote.Price;
            }
        }

        /// <summary>Below the target: one unit at a time — Balanced: the allowed type held the fewest of, ties →
        /// the cheapest; Cheapest: the cheapest. Never below MinGoldAfterDeal.</summary>
        public void PlanBuys()
        {
            if (!_active)
                return;
            bool balanced = _ctx.Settings.FoodStrategy == FoodStrategy.Balanced;
            var market = _ctx.Market;
            int total = TotalHeld;
            while (total < Target)
            {
                int ceiling = _ctx.Gold - _ctx.FoodFloor;
                if (ceiling < 0)
                    break;
                Line? best = null;
                UnitQuote bestQuote = default;
                foreach (var line in _lines)
                {
                    var quote = line.Buy.Peek(market, ceiling);
                    if (quote == null)
                        continue;
                    int price = quote.Value.Price;
                    bool better = best == null
                        || (balanced
                            ? line.Held < best.Held || (line.Held == best.Held && price < bestQuote.Price)
                            : price < bestQuote.Price || (price == bestQuote.Price && line.Held < best.Held));
                    if (better)
                    {
                        best = line;
                        bestQuote = quote.Value;
                    }
                }
                if (best == null)
                    break;
                PlanMath.Take(best.Row, best.Buy, bestQuote, market);
                best.Held++;
                total++;
                _ctx.Gold -= bestQuote.Price;
            }
        }

        public void Finish()
        {
            foreach (var line in _lines)
                PlanMath.FinishItemRow(line.Row,
                    line.HeldStacks.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
        }
    }
}
