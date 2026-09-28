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
    /// Modified (lame…) and locked animals count as held; a locked one is sold like any other unless
    /// LocksProtectFoodAndHorses (<see cref="LockRule"/>). With ReplaceLameHorses (step 17) the lame ones sit in the Lame
    /// horses row instead (<see cref="LameHorsePlanner"/>, sold first) and this row buys healthy ones in their place — a lame
    /// one the market could not take still counts.
    /// </summary>
    internal sealed class PackAnimalPlanner
    {
        private readonly PlanContext _ctx;
        private readonly LameHorsePlanner _lame;
        private readonly List<ItemStack> _held;
        private readonly WalkLine? _buy;
        private readonly WalkLine? _sell;
        private int _heldCount;

        public PackAnimalPlanner(PlanContext ctx, LameHorsePlanner lame)
        {
            _ctx = ctx;
            _lame = lame;
            var settings = ctx.Settings;
            Target = Math.Max(0, settings.PackAnimalsTarget);
            _held = ctx.Inventory(ItemKind.PackAnimal).Where(s => !lame.Holds(s)).ToList();
            _heldCount = _held.Sum(s => s.Count);
            if (!settings.PackAnimalsEnabled || !ctx.Snapshot.CanTrade)
                return;

            var sellLane = new TradeLane(TradeDirection.Sell, LanePick.MostExpensive,
                _held.Where(s => !ctx.IsGuarded(s) && ctx.Book(s)!.SellTicked)
                    .Select(s => new LaneStack(s, s.Count, ctx.MinSellOf(s))));
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
                Locked = _held.Where(ctx.IsGuarded).Sum(s => s.Count),
                LocksGuard = ctx.LockGuards(ItemKind.PackAnimal),
                Target = Target,
                Market = PlanMath.EligibleOnOffer(buyLane, ctx.Market),
                MaxSell = sellLane.Capacity,
                BuyLane = buyLane,
                SellLane = sellLane,
                StartsAtDenari = ctx.StartsAt(ManagedJob.PackAnimals),
            };
            Row.MaxBuy = Row.Market ?? 0;
            _buy = new WalkLine(Row, buyLane, book: Row.Book);
            _sell = new WalkLine(Row, sellLane, book: Row.Book);
            if (ctx.Pins.TryGet(Row.Id, out int pin))
                _pinned = pin;
        }

        /// <summary>The player's own quantity (a touched row in a live re-plan) — walked first, never re-planned.</summary>
        private readonly int? _pinned;

        public int Target { get; }
        public PlanRow? Row { get; }

        /// <summary>The pack animals counting toward the target: this row's, plus the lame ones the Lame horses row has not sold
        /// (it sells first).</summary>
        private int Counted => _heldCount + _lame.LeftOf(ItemKind.PackAnimal);

        /// <summary>The player's sale of pack animals (a touched row) — first among the animal sales (<see cref="PlanPins"/>).</summary>
        public void PlanPinnedSells()
        {
            if (Row != null && _pinned < 0)
                PlanWalk.WalkLane(_ctx.Walk, new WalkLine(Row, Row.SellLane!, Row.Mine, -_pinned.Value, Row.Book), int.MaxValue,
                    () => _ctx.Market.MarketGoldLeft);
        }

        /// <summary>The player's purchase of pack animals (a touched row) — first among the animal buys.</summary>
        public void PlanPinnedBuys()
        {
            if (Row != null && _pinned > 0)
                PlanWalk.WalkLane(_ctx.Walk, new WalkLine(Row, Row.BuyLane!, Row.Mine, _pinned.Value, Row.Book), int.MaxValue,
                    () => null);
        }

        /// <summary>Surplus above the target, the most expensive first, while the market can pay.</summary>
        public void PlanSells()
        {
            if (Row == null || _sell == null || _pinned != null || !_ctx.Settings.SellPackAnimalSurplus
                || !_ctx.JobActive(ManagedJob.PackAnimals))
                return;
            _heldCount -= PlanWalk.WalkLane(_ctx.Walk, _sell, Counted - Target, _ctx.AnimalSellCeiling);
        }

        /// <summary>Up to the target, the cheapest eligible first, never below the animal floor.</summary>
        public void PlanBuys()
        {
            if (Row == null || _buy == null || _pinned != null || !_ctx.JobActive(ManagedJob.PackAnimals))
                return;
            var walk = _ctx.Walk;
            _heldCount += PlanWalk.WalkLane(walk, _buy, Target - Counted, () => _ctx.AnimalBuyCeiling(walk));
        }

        public void Finish()
        {
            if (Row != null)
                PlanMath.FinishItemRow(Row, _held.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
        }
    }
}
