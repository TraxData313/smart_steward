using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// Pack animals (DESIGN §2.2): keep PackAnimalsTarget. One role row. Buys the cheapest eligible (Buy-ticked,
    /// unmodified, within its price-book max AND PackAnimalMaxPrice); sells the surplus most expensive first.
    /// Modified (lame…) and locked animals count as held; locked ones are never sold.
    /// </summary>
    internal sealed class PackAnimalPlanner
    {
        private readonly PlanContext _ctx;
        private readonly List<ItemStack> _held;
        private readonly LaneCursor? _buy;
        private readonly LaneCursor? _sell;
        private int _heldCount;

        public PackAnimalPlanner(PlanContext ctx)
        {
            _ctx = ctx;
            var settings = ctx.Settings;
            Target = Math.Max(0, settings.PackAnimalsTarget);
            _held = ctx.Inventory(ItemKind.PackAnimal).ToList();
            _heldCount = _held.Sum(s => s.Count);
            if (!settings.PackAnimalsEnabled || !ctx.Snapshot.CanTrade)
                return;

            var sellLane = new TradeLane(TradeDirection.Sell, LanePick.MostExpensive,
                _held.Where(s => !s.IsLocked && ctx.Book(s)!.SellTicked)
                    .Select(s => new LaneStack(s, s.Count, ctx.Book(s)!.FinalMinSell)));
            var buyLane = new TradeLane(TradeDirection.Buy, LanePick.Cheapest,
                ctx.MarketStacks(ItemKind.PackAnimal)
                    .Where(s => !s.IsModified && ctx.Book(s)!.BuyTicked)
                    .Select(s => new { Stack = s, Limit = PriceBook.AnimalBuyLimit(ctx.Book(s)!.FinalMaxBuy, settings.PackAnimalMaxPrice) })
                    .Where(x => x.Limit != null)
                    .Select(x => new LaneStack(x.Stack, x.Stack.Count, x.Limit)));

            if (_heldCount == 0 && Target == 0 && buyLane.Capacity == 0)
                return;

            Row = new PlanRow("mounts:pack", PlanSectionKind.Mounts, RowType.Pack)
            {
                Role = MountRole.Pack,
                Mine = _heldCount,
                Locked = _held.Sum(s => s.LockedCount),
                Target = Target,
                Market = PlanMath.EligibleOnOffer(buyLane, ctx.Market),
                MaxSell = sellLane.Capacity,
                BuyLane = buyLane,
                SellLane = sellLane,
            };
            Row.MaxBuy = Row.Market ?? 0;
            _buy = new LaneCursor(buyLane);
            _sell = new LaneCursor(sellLane);
        }

        public int Target { get; }
        public PlanRow? Row { get; }

        public void PlanSells()
        {
            if (Row == null || _sell == null || !_ctx.Settings.SellPackAnimalSurplus)
                return;
            var market = _ctx.Market;
            while (_heldCount > Target)
            {
                var quote = _sell.Peek(market, market.MarketGoldLeft);
                if (quote == null)
                    break;
                PlanMath.Take(Row, _sell, quote.Value, market);
                _heldCount--;
                _ctx.Gold += quote.Value.Price;
            }
        }

        public void PlanBuys()
        {
            if (Row == null || _buy == null)
                return;
            var market = _ctx.Market;
            while (_heldCount < Target)
            {
                var quote = _buy.Peek(market, _ctx.Gold - _ctx.AnimalFloor);
                if (quote == null)
                    break;
                PlanMath.Take(Row, _buy, quote.Value, market);
                _heldCount++;
                _ctx.Gold -= quote.Value.Price;
            }
        }

        public void Finish()
        {
            if (Row != null)
                PlanMath.FinishItemRow(Row, _held.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
        }
    }
}
