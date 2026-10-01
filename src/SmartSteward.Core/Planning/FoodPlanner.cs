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

            /// <summary>The player's own quantity — a goal of yours (round 5: goal − Mine) — walked first, never re-planned.</summary>
            public int? Pinned { get; }

            /// <summary>The player's own walk of this row (sell or buy), to tell why it stopped short.</summary>
            public WalkLine? Hand { get; set; }
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
                // Step 26: the units a quest keeps are out of the steward's sell lane; a goal of yours walks the full lane (it wins).
                var fullSell = book.SellTicked
                    ? new TradeLane(TradeDirection.Sell, LanePick.MostExpensive,
                        item.Held.Where(s => !ctx.IsGuarded(s)).Select(s => new LaneStack(s, s.Count, book.FinalMinSell)))
                    : TradeLane.Empty(TradeDirection.Sell);
                var sellLane = book.SellTicked
                    ? new TradeLane(TradeDirection.Sell, LanePick.MostExpensive,
                        item.Held.Where(s => !ctx.IsGuarded(s)).Select(s => new LaneStack(s, ctx.Quests.Free(s), book.FinalMinSell)))
                    : TradeLane.Empty(TradeDirection.Sell);

                // Round 5: with ManualGoalsObeyPriceCaps off a goal buys and sells this food at any price — its own lanes
                // without the limits (the ticks still decide the stacks); the steward's own walk keeps the limits.
                TradeLane? handBuy = null, handSell = sellLane.Capacity == fullSell.Capacity ? null : fullSell;
                if (ctx.GoalsIgnoreCaps)
                {
                    handBuy = book.BuyTicked
                        ? new TradeLane(TradeDirection.Buy, LanePick.Cheapest, item.Offered.Select(s => new LaneStack(s, s.Count, null)))
                        : TradeLane.Empty(TradeDirection.Buy);
                    handSell = PlanContext.Unlimited(fullSell);
                }

                int held = item.Held.Sum(s => s.Count);
                if (held == 0 && buyLane.Capacity == 0 && (handBuy?.Capacity ?? 0) == 0 && !ctx.Goals.ContainsKey("food:" + item.Id))
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
                    MaxBuy = PlanMath.EligibleOnOffer(handBuy ?? buyLane, ctx.Market),
                    MaxSell = (handSell ?? sellLane).Capacity,
                    PriceBook = book,
                    BuyLane = buyLane,
                    SellLane = sellLane,
                    HandBuyLaneOverride = handBuy,
                    HandSellLaneOverride = handSell,
                    StartsAtDenari = startsAt,
                    Quest = ctx.Quests.ForStacks(item.Held, item.Id),
                };
                int? pinned = ctx.PinOf(row, ManagedJob.Food);
                _lines.Add(new Line(row, item.Held, new WalkLine(row, sellLane, held, book: row.Book), pinned));
            }
            Share = FairShare(Target, _lines.Count);
        }

        public int Eaters { get; }
        public int Target { get; }
        public double SellAbove { get; }

        /// <summary>The even share of the target per food kind (step 25, Anton 2026.10.01): ceil(target / the food rows) — rounded up
        /// so the kinds together never fall short of the target. A goal of yours counts toward the target only up to it (the rest
        /// is a stockpile on top), and the steward's surplus sale never takes a kind below it (variety is kept).</summary>
        public int Share { get; }

        internal static int FairShare(int target, int kinds) =>
            kinds <= 0 || target <= 0 ? 0 : (target + kinds - 1) / kinds;

        public IReadOnlyList<PlanRow> Rows => _lines.Select(l => l.Row).ToList();

        private IEnumerable<Line> Steward => _lines.Where(l => l.Pinned == null);

        /// <summary>Food the party will hold as the steward sees it: its own rows as the walk goes, the player's rows (your
        /// goals — round 5: they count toward the days goal FIRST) at their result — asked, before their buys are walked;
        /// walked, after (<paramref name="walked"/>): the steward plans the rest around them. A goal counts only up to the even
        /// <see cref="Share"/> (step 25): above it is a stockpile ON TOP of the days (a quest hoard), never eating the other kinds'
        /// share.</summary>
        /// <para>Step 26: the units a quest keeps on a steward row count the same way — up to the share, the rest on top (they are
        /// never sold, so the row's held units never fall below them).</para>
        private int HeldTotal(bool walked) =>
            _lines.Sum(l => l.Pinned == null
                ? l.Sell.Held - QuestKept(l) + Math.Min(QuestKept(l), Share)
                : Math.Min(Share, l.Row.Mine + (walked ? l.Row.Book.Net : l.Pinned.Value)));

        private static int QuestKept(Line line) => Math.Min(line.Sell.Held, line.Row.Quest?.Kept ?? 0);

        private int TotalHeld => HeldTotal(false);

        /// <summary>The player's food sales first (a live re-plan, <see cref="PlanPins"/>), then the steward's surplus: only
        /// above target × (1 + tolerance), back down to the target — the most-held type first (keeps variety), only
        /// Sell-ticked types at ≥ their min sell, never beyond the market's gold (minus what the player's later sales need) — and
        /// never a kind below its even <see cref="Share"/> (step 25: the variety guard; short of the target it stops there). The
        /// player's own goal sales are not guarded: the player is in control.</summary>
        public void PlanSells()
        {
            if (!_active)
                return;
            var mine = _lines.Where(l => l.Pinned < 0)
                .Select(l => l.Hand = new WalkLine(l.Row, l.Row.HandSellLane!, l.Row.Mine, -l.Pinned!.Value, l.Row.Book)).ToList();
            if (mine.Count > 0)
                PlanWalk.SellMostHeldFirst(_ctx.Walk, mine, () => true);

            if (!_stewardActs || !_ctx.Settings.SellFoodSurplus || TotalHeld <= SellAbove)
                return;
            var sells = Steward.Select(l => l.Sell).ToList();
            foreach (var line in sells)
                line.Quota = Math.Max(0, line.Held - Share);
            PlanWalk.SellMostHeldFirst(_ctx.Walk, sells, () => TotalHeld > Target, _ctx.FoodSellCeiling);
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
            // Your goals' buys (round 5) stop at the goals' food floor (ManualGoalsKeepPurseFloor; AutonomousMinGold always).
            var mine = _lines.Where(l => l.Pinned > 0)
                .Select(l => l.Hand = new WalkLine(l.Row, l.Row.HandBuyLane!, l.Row.Mine, l.Pinned!.Value, l.Row.Book)).ToList();
            if (mine.Count > 0)
                PlanWalk.BuyFood(walk, mine, balanced, () => _ctx.GoalFoodCeiling(walk), () => true);

            if (!_stewardActs)
                return;
            // No row sells and buys in one visit (sold only above target + tolerance, bought only below the
            // target), so the buy walk starts from the units held after the sales.
            var buys = Steward.Select(l => new WalkLine(l.Row, l.Row.BuyLane!, l.Sell.Held, book: l.Row.Book)).ToList();
            int total = HeldTotal(true);
            PlanWalk.BuyFood(walk, buys, balanced,
                () => _ctx.FoodBuyCeiling(walk), () => total + buys.Sum(b => b.Cursor.Moved) < Target);
        }

        public void Finish()
        {
            foreach (var line in _lines)
            {
                PlanMath.FinishItemRow(line.Row,
                    line.HeldStacks.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
                if (line.Hand != null && line.Row.Result != line.Row.ManualGoal)
                    line.Row.GoalShort = GoalReasons.Of(line.Hand, _ctx.GoalFloors.Food != null);
            }
        }
    }
}
