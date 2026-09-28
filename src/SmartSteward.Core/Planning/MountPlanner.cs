using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// Riding mounts for the footmen (DESIGN §2.3) and upgrade horses (§2.4), as the ROLE rows of §1.1.1.
    /// </summary>
    /// <remarks>
    /// How a held mount gets its role:
    /// 1. Need per upgrade category (only categories the party's troops upgrade into — <see cref="UpgradeNeeds"/>):
    ///    the ready troops (a stack counted once, at its best horse-needing target — foot-or-horse recruits count as
    ///    needing the horse) + WarMountsExtra, or that kind's own fixed number (WarMountsHorseTarget /
    ///    WarMountsWarHorseTarget) when ≥ 0.
    /// 2. Reserved = the first `need` held mounts of the category in the order the game's upgrade consumes
    ///    them: unlocked before locked, cheapest base value first (PartyScreenLogic.RemoveItemFromItemRoster).
    /// 3. Every other mount — war and noble horses and camels too — is a riding mount; its surplus is sold
    ///    most expensive first (a horse of an upgrade category only with SellWarMountSurplus).
    /// Locked mounts count and are sold like any other unless LocksProtectFoodAndHorses (<see cref="LockRule"/>); the
    /// reservation still takes unlocked horses first, since that is the order the game's upgrade consumes them in.
    /// The riding target counts the riding mounts plus, with WarMountsCountAsMounts, the reserved ones and
    /// the upgrade horses about to be bought (the man upgraded takes his horse: counting them twice buys a
    /// horse too many per upgrade).
    /// </remarks>
    internal sealed class MountPlanner
    {
        private sealed class UpgradeLine
        {
            public UpgradeLine(string category, int need, int reserved, PlanRow row, LaneCursor buy, int? pinned)
            {
                Category = category;
                Need = need;
                Reserved = reserved;
                Row = row;
                Buy = buy;
                Pinned = pinned;
            }

            public string Category { get; }
            public int Need { get; }
            public int Reserved { get; }
            public int Short => Math.Max(0, Need - Reserved);
            public PlanRow Row { get; }
            public LaneCursor Buy { get; }

            /// <summary>The player's own quantity (a touched row in a live re-plan) — walked first, never re-planned.</summary>
            public int? Pinned { get; }
        }

        private const string UpgradeRowPrefix = "mounts:upgrade:";

        private readonly PlanContext _ctx;
        private readonly List<ItemStack> _held;
        private readonly Dictionary<string, int> _reservedByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<UpgradeLine> _upgrades = new List<UpgradeLine>();
        private readonly LaneCursor? _ridingBuy;
        private readonly WalkLine? _ridingSell;

        /// <summary>The player's own riding quantity (a touched row in a live re-plan) — walked first, never re-planned.</summary>
        private readonly int? _ridingPinned;
        private bool _soldRiding;

        public MountPlanner(PlanContext ctx)
        {
            _ctx = ctx;
            var settings = ctx.Settings;
            _held = ctx.Inventory(ItemKind.Mount).ToList();

            // 1. What the upgrades need, per category in play (each kind its own fixed number or automatic) — for the party
            //    after the deal: hired men are never ready, but their troop's kinds of upgrade horse come into play.
            var upgrades = ctx.Party.Upgrades;
            var inPlay = upgrades.InPlay;
            var need = upgrades.Need(settings);
            var readyNow = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var category in inPlay)
                readyNow[category] = upgrades.ReadyFor(category);
            UpgradeReady = readyNow;

            // 2. Reserve the held horses the upgrades will take.
            var reserved = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var pair in need)
            {
                int left = pair.Value;
                foreach (var stack in _held.Where(s => s.CategoryId == pair.Key)
                             .OrderBy(s => s.IsLocked).ThenBy(s => s.StoreValueStep)
                             .ThenBy(s => s.Key, StringComparer.Ordinal))
                {
                    if (left <= 0) break;
                    int take = Math.Min(left, stack.Count);
                    _reservedByKey[stack.Key] = take;
                    left -= take;
                }
                reserved[pair.Key] = pair.Value - left;
            }
            UpgradeNeed = need;
            UpgradeReserved = reserved;

            // 3. The riding pool and its target — the footmen of the party after the deal (the men hired on foot too).
            int pool = _held.Sum(s => s.Count - ReservedOf(s));
            int reservedTotal = reserved.Values.Sum();
            Footmen = Math.Max(0, ctx.Party.Footmen);
            RidingTarget = settings.MountsEnabled
                ? PlanMath.Ceiling(Footmen * (double)Math.Max(0, settings.MountsPer100Footmen) / 100.0)
                : 0;
            RidingCounted = pool + (settings.WarMountsCountAsMounts ? reservedTotal : 0);

            if (!ctx.Snapshot.CanTrade)
                return;

            if (settings.MountsEnabled)
            {
                // With war mounts managed and SellWarMountSurplus off, a horse of a category the party upgrades
                // into is never sold — it is kept for the upgrades to come. (War mounts not managed at all →
                // such horses are plain riding mounts.)
                bool sellWarSurplus = !settings.WarMountsEnabled || settings.SellWarMountSurplus;
                var sellLane = new TradeLane(TradeDirection.Sell, LanePick.MostExpensive,
                    _held.Where(s => !ctx.IsGuarded(s) && s.Count - ReservedOf(s) > 0 && ctx.Book(s)!.SellTicked
                                     && (sellWarSurplus || !inPlay.Contains(s.CategoryId)))
                        .Select(s => new LaneStack(s, s.Count - ReservedOf(s), ctx.Book(s)!.FinalMinSell)));
                var buyLane = AnimalBuyLane(ctx, s => true, settings.MountMaxPrice);
                RidingRow = new PlanRow("mounts:riding", PlanSectionKind.Mounts, RowType.Mount)
                {
                    Role = MountRole.Riding,
                    Mine = pool,
                    Locked = _held.Where(ctx.IsGuarded).Sum(s => s.Count - ReservedOf(s)),
                    LocksGuard = ctx.LockGuards(ItemKind.Mount),
                    Target = RidingTarget,
                    Market = PlanMath.EligibleOnOffer(buyLane, ctx.Market),
                    MaxSell = sellLane.Capacity,
                    BuyLane = buyLane,
                    SellLane = sellLane,
                };
                RidingRow.MaxBuy = RidingRow.Market ?? 0;
                _ridingBuy = new LaneCursor(buyLane);
                _ridingSell = new WalkLine(RidingRow, sellLane, book: RidingRow.Book);
                if (ctx.Pins.TryGet(RidingRow.Id, out int pin))
                    _ridingPinned = pin;
            }

            // One row per kind that needs upgrade horses — and any kind the player has his hand on, kept even when the
            // party after the deal no longer needs it (step 16's dismissals), while war mounts are managed at all.
            var categories = need.Where(p => p.Value > 0).Select(p => p.Key);
            if (settings.WarMountsEnabled)
                categories = categories.Concat(ctx.Pins.Ids.Where(id => id.StartsWith(UpgradeRowPrefix, StringComparison.Ordinal))
                    .Select(id => id.Substring(UpgradeRowPrefix.Length)));
            foreach (var category in categories.Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal))
            {
                int needed = need.TryGetValue(category, out var n) ? n : 0;
                int reservedHere = reserved.TryGetValue(category, out var r) ? r : 0;
                var mine = _held.Where(s => s.CategoryId == category && ReservedOf(s) > 0).ToList();
                var sellLane = new TradeLane(TradeDirection.Sell, LanePick.MostExpensive,
                    mine.Where(s => !ctx.IsGuarded(s) && ctx.Book(s)!.SellTicked)
                        .Select(s => new LaneStack(s, ReservedOf(s), ctx.Book(s)!.FinalMinSell)));
                var buyLane = AnimalBuyLane(ctx, s => s.CategoryId == category, settings.WarMountMaxPrice);
                var row = new PlanRow(UpgradeRowPrefix + category, PlanSectionKind.Mounts, RowType.WarMount)
                {
                    Role = MountRole.Upgrade,
                    CategoryId = category,
                    Mine = reservedHere,
                    Locked = mine.Where(ctx.IsGuarded).Sum(ReservedOf),
                    LocksGuard = ctx.LockGuards(ItemKind.Mount),
                    Need = needed,
                    Market = PlanMath.EligibleOnOffer(buyLane, ctx.Market),
                    MaxSell = sellLane.Capacity,
                    BuyLane = buyLane,
                    SellLane = sellLane,
                };
                row.MaxBuy = row.Market ?? 0;
                int? pinned = ctx.Pins.TryGet(row.Id, out int upgradePin) ? upgradePin : (int?)null;
                _upgrades.Add(new UpgradeLine(category, needed, reservedHere, row, new LaneCursor(buyLane), pinned));
            }
        }

        public int Footmen { get; }
        public int RidingTarget { get; }
        public int RidingCounted { get; }
        public IReadOnlyDictionary<string, int> UpgradeReady { get; }
        public IReadOnlyDictionary<string, int> UpgradeNeed { get; }
        public IReadOnlyDictionary<string, int> UpgradeReserved { get; }
        public PlanRow? RidingRow { get; }

        public IEnumerable<PlanRow> Rows
        {
            get
            {
                if (RidingRow != null) yield return RidingRow;
                foreach (var line in _upgrades) yield return line.Row;
            }
        }

        /// <summary>The upgrade rows the steward plans (the player's own are pinned).</summary>
        private List<UpgradeLine> StewardUpgrades => _upgrades.Where(u => u.Pinned == null).ToList();

        /// <summary>The mounts counting toward the riding target once the player's upgrade rows are done: with
        /// WarMountsCountAsMounts the horses the player buys (or sells) for upgrades count, like the steward's own.</summary>
        private int CountedWithPlayers =>
            RidingCounted + (_ctx.Settings.WarMountsCountAsMounts ? _upgrades.Sum(u => u.Pinned ?? 0) : 0);

        private int ReservedOf(ItemStack stack) =>
            _reservedByKey.TryGetValue(stack.Key, out var n) ? n : 0;

        private static TradeLane AnimalBuyLane(PlanContext ctx, Func<ItemStack, bool> filter, int roleCap) =>
            new TradeLane(TradeDirection.Buy, LanePick.Cheapest,
                ctx.MarketStacks(ItemKind.Mount)
                    .Where(s => !s.IsModified && filter(s) && ctx.Book(s)!.BuyTicked)
                    .Select(s => new { Stack = s, Limit = PriceBook.AnimalBuyLimit(ctx.Book(s)!.FinalMaxBuy, roleCap) })
                    .Where(x => x.Limit != null)
                    .Select(x => new LaneStack(x.Stack, x.Stack.Count, x.Limit)));

        /// <summary>The player's own mount sales (touched riding / upgrade rows) — first among the animal sales, in row order
        /// (<see cref="PlanPins"/>).</summary>
        public void PlanPinnedSells()
        {
            var walk = _ctx.Walk;
            if (RidingRow != null && _ridingPinned < 0)
                PlanWalk.WalkLane(walk, new WalkLine(RidingRow, RidingRow.SellLane!, RidingRow.Mine, -_ridingPinned.Value,
                    RidingRow.Book), int.MaxValue, () => walk.Market.MarketGoldLeft);
            foreach (var line in _upgrades.Where(u => u.Pinned < 0))
                PlanWalk.WalkLane(walk, new WalkLine(line.Row, line.Row.SellLane!, line.Row.Mine, -line.Pinned!.Value,
                    line.Row.Book), int.MaxValue, () => walk.Market.MarketGoldLeft);
        }

        /// <summary>The player's own mount buys — riding first, then the upgrade rows the cheapest next across them, before
        /// any of the steward's.</summary>
        public void PlanPinnedBuys()
        {
            var walk = _ctx.Walk;
            if (RidingRow != null && _ridingPinned > 0)
                PlanWalk.WalkLane(walk, new WalkLine(RidingRow, RidingRow.BuyLane!, RidingRow.Mine, _ridingPinned.Value,
                    RidingRow.Book), int.MaxValue, () => null);
            var mine = _upgrades.Where(u => u.Pinned > 0)
                .Select(u => new WalkLine(u.Row, u.Row.BuyLane!, u.Row.Mine, u.Pinned!.Value, u.Row.Book)).ToList();
            if (mine.Count > 0)
                PlanWalk.BuyCheapestAcross(walk, mine, () => null);
        }

        /// <summary>Riding surplus, most expensive first — but never in a visit that buys mounts: selling a
        /// horse fetches about half its price (DESIGN §2.2), so when an upgrade horse is on offer (or the player buys one)
        /// the surplus waits (after the upgrade it is surplus for real).</summary>
        public void PlanSells()
        {
            if (RidingRow == null || _ridingSell == null || _ridingPinned != null || !_ctx.Settings.SellMountSurplus)
                return;
            int surplus = CountedWithPlayers - RidingTarget;
            if (surplus <= 0)
                return;
            var market = _ctx.Market;
            foreach (var line in StewardUpgrades)
                if (line.Short > 0 && line.Buy.Peek(market) != null)
                    return;
            if (_upgrades.Any(u => u.Pinned > 0))
                return;
            _soldRiding = PlanWalk.WalkLane(_ctx.Walk, _ridingSell, surplus, _ctx.AnimalSellCeiling) > 0;
        }

        /// <summary>
        /// Riding mounts first, then upgrade horses (DESIGN §3), both under MinGoldForHorses. With
        /// WarMountsCountAsMounts the upgrade horses about to be bought count toward the riding target; how many
        /// that will be depends on the money the riding buys leave, so it is found by simulation: pledge all,
        /// and while fewer upgrade horses are affordable, buy more riding mounts instead. The player's own rows are
        /// already walked (<see cref="PlanPinnedBuys"/>) and counted; the steward plans only its own.
        /// </summary>
        public void PlanBuys()
        {
            var settings = _ctx.Settings;
            var walk = _ctx.Walk;
            var stewardUpgrades = StewardUpgrades;
            bool stewardRides = RidingRow != null && _ridingBuy != null && _ridingPinned == null;
            int ridingNeed = stewardRides && !_soldRiding
                ? Math.Max(0, RidingTarget - CountedWithPlayers)
                : 0;
            int shortTotal = stewardUpgrades.Sum(u => u.Short);

            int ridingCap = ridingNeed;
            if (settings.WarMountsCountAsMounts && ridingNeed > 0 && shortTotal > 0)
            {
                int pledged = shortTotal;
                while (true)
                {
                    ridingCap = Math.Max(0, ridingNeed - pledged);
                    var whatIf = walk.Simulation();
                    BuyRiding(whatIf, new WalkLine(null, _ridingBuy!.Clone()), ridingCap);
                    int got = BuyUpgrades(whatIf, stewardUpgrades.Select(u => new WalkLine(null, u.Buy.Clone(), quota: u.Short)));
                    if (got >= pledged)
                        break;
                    pledged = got;
                }
            }

            if (stewardRides)
                BuyRiding(walk, new WalkLine(RidingRow, _ridingBuy!, book: RidingRow!.Book), ridingCap);
            BuyUpgrades(walk, stewardUpgrades.Select(u => new WalkLine(u.Row, u.Buy, quota: u.Short, book: u.Row.Book)));
        }

        private int BuyRiding(WalkState walk, WalkLine line, int cap) =>
            PlanWalk.WalkLane(walk, line, cap, () => _ctx.AnimalBuyCeiling(walk));

        /// <summary>The cheapest next upgrade horse across the categories still short; ties → category order.</summary>
        private int BuyUpgrades(WalkState walk, IEnumerable<WalkLine> lines) =>
            PlanWalk.BuyCheapestAcross(walk, lines.ToList(), () => _ctx.AnimalBuyCeiling(walk));

        public void Finish()
        {
            if (RidingRow != null)
                PlanMath.FinishItemRow(RidingRow,
                    _held.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count - ReservedOf(s))));
            foreach (var line in _upgrades)
                PlanMath.FinishItemRow(line.Row,
                    _held.Where(s => s.CategoryId == line.Category)
                        .Select(s => new KeyValuePair<ItemStack, int>(s, ReservedOf(s))));
        }
    }
}
