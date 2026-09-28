using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// Food (DESIGN §2.1): keep FoodDays days of food — ceil(days × the game's daily use per eater × eaters after the deal)
    /// units (<see cref="FoodGoal"/>), varied, fairly priced. One row per food
    /// item. Buying and selling never happen in one visit: food is sold only above the target plus the
    /// tolerance, and bought only below the target. Locked food counts as held and is sold like any other unless
    /// LocksProtectFoodAndHorses (<see cref="LockRule"/>).
    /// </summary>
    internal sealed class FoodPlanner
    {
        private sealed class Line
        {
            public Line(PlanRow row, List<ItemStack> heldStacks, WalkLine sell, int? pinned)
            {
                Row = row;
                HeldStacks = heldStacks;
                Sell = sell;
                Pinned = pinned;
            }

            public PlanRow Row { get; }
            public List<ItemStack> HeldStacks { get; }

            /// <summary>The sell walk; its Held is the units held as the plan goes.</summary>
            public WalkLine Sell { get; }

            /// <summary>The player's own quantity (a touched row in a live re-plan) — walked first, never re-planned.</summary>
            public int? Pinned { get; }
        }

        private readonly PlanContext _ctx;
        private readonly List<Line> _lines = new List<Line>();
        private readonly bool _active;

        /// <summary>The steward's own side acts (the purse before the deal reached FoodMinDenari — round 4).</summary>
        private readonly bool _stewardActs;

        public FoodPlanner(PlanContext ctx, int prisonersAfter)
        {
            _ctx = ctx;
            var settings = ctx.Settings;

            // A prisoner eats half a man's ration — the game halves with integer division (RESEARCH §2).
            // Prisoners the deal ransoms or donates eat nothing more, so the target counts those left; the men the deal
            // hires eat from today (the party after the deal — the live re-plan, step 15).
            Eaters = Math.Max(0, ctx.Party.Members)
                     + (settings.FoodCountPrisoners ? Math.Max(0, prisonersAfter) / 2 : 0);
            // FoodDays days of food at the game's own rate per eater (perks included) — Anton 2026.09.28 (FoodGoal).
            Target = FoodGoal.Target(settings.FoodDays, FoodGoal.PerEaterPerDay(ctx.Snapshot), Eaters);
            SellAbove = Math.Round(Target * (1 + Math.Max(0, settings.FoodSurplusTolerancePercent) / 100.0), 6);

            _active = settings.FoodEnabled && ctx.Snapshot.CanTrade;
            if (!_active)
                return;
            // Round 4: below FoodMinDenari the steward neither buys nor sells food - the rows stay for the player's hand.
            _stewardActs = ctx.JobActive(ManagedJob.Food);
            int? startsAt = ctx.StartsAt(ManagedJob.Food);

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
                        item.Held.Where(s => !ctx.IsGuarded(s)).Select(s => new LaneStack(s, s.Count, book.FinalMinSell)))
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
                    Locked = item.Held.Where(ctx.IsGuarded).Sum(s => s.Count),
                    LocksGuard = ctx.LockGuards(ItemKind.Food),
                    Market = item.Offered.Sum(s => s.Count),
                    MaxBuy = PlanMath.EligibleOnOffer(buyLane, ctx.Market),
                    MaxSell = sellLane.Capacity,
                    PriceBook = book,
                    BuyLane = buyLane,
                    SellLane = sellLane,
                    StartsAtDenari = startsAt,
                };
                int? pinned = ctx.Pins.TryGet(row.Id, out int pin) ? pin : (int?)null;
                _lines.Add(new Line(row, item.Held, new WalkLine(row, sellLane, held, book: row.Book), pinned));
            }
        }

        public int Eaters { get; }
        public int Target { get; }
        public double SellAbove { get; }

        public IReadOnlyList<PlanRow> Rows => _lines.Select(l => l.Row).ToList();

        private IEnumerable<Line> Steward => _lines.Where(l => l.Pinned == null);

        /// <summary>Food the party will hold as the steward sees it: its own rows as the walk goes, the player's rows at their
        /// result (their own buys and sales already counted — the steward plans the rest around them).</summary>
        private int TotalHeld => _lines.Sum(l => l.Pinned == null ? l.Sell.Held : l.Row.Mine + l.Pinned.Value);

        /// <summary>The player's food sales first (a live re-plan, <see cref="PlanPins"/>), then the steward's surplus: only
        /// above target × (1 + tolerance), back down to the target — the most-held type first (keeps variety), only
        /// Sell-ticked types at ≥ their min sell, never beyond the market's gold (minus what the player's later sales need).</summary>
        public void PlanSells()
        {
            if (!_active)
                return;
            var mine = _lines.Where(l => l.Pinned < 0)
                .Select(l => new WalkLine(l.Row, l.Row.SellLane!, l.Row.Mine, -l.Pinned!.Value, l.Row.Book)).ToList();
            if (mine.Count > 0)
                PlanWalk.SellMostHeldFirst(_ctx.Walk, mine, () => true);

            if (!_stewardActs || !_ctx.Settings.SellFoodSurplus || TotalHeld <= SellAbove)
                return;
            PlanWalk.SellMostHeldFirst(_ctx.Walk, Steward.Select(l => l.Sell).ToList(), () => TotalHeld > Target,
                _ctx.FoodSellCeiling);
        }

        /// <summary>The player's food buys first, then the steward's below the target: one unit at a time — Balanced: the
        /// allowed type held the fewest of, ties → the cheapest; Cheapest: the cheapest. Never below MinGoldAfterDeal (nor
        /// into what the player's later buys and hires cost).</summary>
        public void PlanBuys()
        {
            if (!_active)
                return;
            var walk = _ctx.Walk;
            bool balanced = _ctx.Settings.FoodStrategy == FoodStrategy.Balanced;
            var mine = _lines.Where(l => l.Pinned > 0)
                .Select(l => new WalkLine(l.Row, l.Row.BuyLane!, l.Row.Mine, l.Pinned!.Value, l.Row.Book)).ToList();
            if (mine.Count > 0)
                PlanWalk.BuyFood(walk, mine, balanced, () => null, () => true);

            if (!_stewardActs)
                return;
            // No row sells and buys in one visit (sold only above target + tolerance, bought only below the
            // target), so the buy walk starts from the units held after the sales.
            var buys = Steward.Select(l => new WalkLine(l.Row, l.Row.BuyLane!, l.Sell.Held, book: l.Row.Book)).ToList();
            int total = TotalHeld;
            PlanWalk.BuyFood(walk, buys, balanced,
                () => _ctx.FoodBuyCeiling(walk), () => total + buys.Sum(b => b.Cursor.Moved) < Target);
        }

        public void Finish()
        {
            foreach (var line in _lines)
                PlanMath.FinishItemRow(line.Row,
                    line.HeldStacks.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
        }
    }
}
