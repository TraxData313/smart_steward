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
    /// 1. Need per upgrade category (only categories the party's troops upgrade into): the ready troops
    ///    (a stack counted once, at its best horse-needing target — foot-or-horse recruits count as needing
    ///    the horse) + WarMountsExtra, or WarMountsManualTarget when ≥ 0.
    /// 2. Reserved = the first `need` held mounts of the category in the order the game's upgrade consumes
    ///    them: unlocked before locked, cheapest base value first (PartyScreenLogic.RemoveItemFromItemRoster).
    /// 3. Every other mount — war and noble horses and camels too — is a riding mount; its surplus is sold
    ///    most expensive first (a horse of an upgrade category only with SellWarMountSurplus).
    /// The riding target counts the riding mounts plus, with WarMountsCountAsMounts, the reserved ones and
    /// the upgrade horses about to be bought (the man upgraded takes his horse: counting them twice buys a
    /// horse too many per upgrade).
    /// </remarks>
    internal sealed class MountPlanner
    {
        private sealed class UpgradeLine
        {
            public UpgradeLine(string category, int need, int reserved, PlanRow row, LaneCursor buy)
            {
                Category = category;
                Need = need;
                Reserved = reserved;
                Row = row;
                Buy = buy;
            }

            public string Category { get; }
            public int Need { get; }
            public int Reserved { get; }
            public int Short => Math.Max(0, Need - Reserved);
            public PlanRow Row { get; }
            public LaneCursor Buy { get; }
        }

        private readonly PlanContext _ctx;
        private readonly List<ItemStack> _held;
        private readonly Dictionary<string, int> _reservedByKey = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<UpgradeLine> _upgrades = new List<UpgradeLine>();
        private readonly LaneCursor? _ridingBuy;
        private readonly LaneCursor? _ridingSell;
        private bool _soldRiding;

        public MountPlanner(PlanContext ctx)
        {
            _ctx = ctx;
            var settings = ctx.Settings;
            _held = ctx.Inventory(ItemKind.Mount).ToList();

            // 1. What the upgrades need, per category in play.
            var inPlay = new SortedSet<string>(StringComparer.Ordinal);
            var ready = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var stack in ctx.Snapshot.Upgrades ?? new List<UpgradeStack>())
            {
                if (stack?.Targets == null)
                    continue;
                UpgradeTarget? best = null;
                foreach (var target in stack.Targets)
                {
                    if (target == null || string.IsNullOrEmpty(target.RequiredCategoryId))
                        continue;
                    inPlay.Add(target.RequiredCategoryId!);
                    if (target.ReadyCount > 0 && (best == null || target.ReadyCount > best.ReadyCount))
                        best = target;
                }
                if (best != null)
                {
                    int count = Math.Min(best.ReadyCount, Math.Max(0, stack.Count));
                    ready[best.RequiredCategoryId!] = (ready.TryGetValue(best.RequiredCategoryId!, out var r) ? r : 0) + count;
                }
            }
            var need = new Dictionary<string, int>(StringComparer.Ordinal);
            if (settings.WarMountsEnabled)
                foreach (var category in inPlay)
                    need[category] = settings.WarMountsManualTarget >= 0
                        ? settings.WarMountsManualTarget
                        : (ready.TryGetValue(category, out var r) ? r : 0) + Math.Max(0, settings.WarMountsExtra);

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

            // 3. The riding pool and its target.
            int pool = _held.Sum(s => s.Count - ReservedOf(s));
            int reservedTotal = reserved.Values.Sum();
            Footmen = Math.Max(0, ctx.Snapshot.Party.Footmen);
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
                    _held.Where(s => !s.IsLocked && s.Count - ReservedOf(s) > 0 && ctx.Book(s)!.SellTicked
                                     && (sellWarSurplus || !inPlay.Contains(s.CategoryId)))
                        .Select(s => new LaneStack(s, s.Count - ReservedOf(s), ctx.Book(s)!.FinalMinSell)));
                var buyLane = AnimalBuyLane(ctx, s => true, settings.MountMaxPrice);
                RidingRow = new PlanRow("mounts:riding", PlanSectionKind.Mounts, RowType.Mount)
                {
                    Role = MountRole.Riding,
                    Mine = pool,
                    Locked = _held.Where(s => s.IsLocked).Sum(s => s.Count - ReservedOf(s)),
                    Target = RidingTarget,
                    Market = PlanMath.EligibleOnOffer(buyLane, ctx.Market),
                    MaxBuy = buyLane.Capacity,
                    MaxSell = sellLane.Capacity,
                    BuyLane = buyLane,
                    SellLane = sellLane,
                };
                _ridingBuy = new LaneCursor(buyLane);
                _ridingSell = new LaneCursor(sellLane);
            }

            foreach (var pair in need.Where(p => p.Value > 0))
            {
                string category = pair.Key;
                var mine = _held.Where(s => s.CategoryId == category && ReservedOf(s) > 0).ToList();
                var sellLane = new TradeLane(TradeDirection.Sell, LanePick.MostExpensive,
                    mine.Where(s => !s.IsLocked && ctx.Book(s)!.SellTicked)
                        .Select(s => new LaneStack(s, ReservedOf(s), ctx.Book(s)!.FinalMinSell)));
                var buyLane = AnimalBuyLane(ctx, s => s.CategoryId == category, settings.WarMountMaxPrice);
                var row = new PlanRow("mounts:upgrade:" + category, PlanSectionKind.Mounts, RowType.WarMount)
                {
                    Role = MountRole.Upgrade,
                    CategoryId = category,
                    Mine = reserved[category],
                    Locked = mine.Where(s => s.IsLocked).Sum(ReservedOf),
                    Need = pair.Value,
                    Market = PlanMath.EligibleOnOffer(buyLane, ctx.Market),
                    MaxBuy = buyLane.Capacity,
                    MaxSell = sellLane.Capacity,
                    BuyLane = buyLane,
                    SellLane = sellLane,
                };
                _upgrades.Add(new UpgradeLine(category, pair.Value, reserved[category], row, new LaneCursor(buyLane)));
            }
        }

        public int Footmen { get; }
        public int RidingTarget { get; }
        public int RidingCounted { get; }
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

        private int ReservedOf(ItemStack stack) =>
            _reservedByKey.TryGetValue(stack.Key, out var n) ? n : 0;

        private static TradeLane AnimalBuyLane(PlanContext ctx, Func<ItemStack, bool> filter, int roleCap) =>
            new TradeLane(TradeDirection.Buy, LanePick.Cheapest,
                ctx.MarketStacks(ItemKind.Mount)
                    .Where(s => !s.IsModified && filter(s) && ctx.Book(s)!.BuyTicked)
                    .Select(s => new { Stack = s, Limit = PriceBook.AnimalBuyLimit(ctx.Book(s)!.FinalMaxBuy, roleCap) })
                    .Where(x => x.Limit != null)
                    .Select(x => new LaneStack(x.Stack, x.Stack.Count, x.Limit)));

        /// <summary>Riding surplus, most expensive first — but never in a visit that buys mounts: selling a
        /// horse fetches about half its price (DESIGN §2.2), so when an upgrade horse is on offer the surplus
        /// waits (after the upgrade it is surplus for real).</summary>
        public void PlanSells()
        {
            if (RidingRow == null || _ridingSell == null || !_ctx.Settings.SellMountSurplus)
                return;
            int surplus = RidingCounted - RidingTarget;
            if (surplus <= 0)
                return;
            var market = _ctx.Market;
            foreach (var line in _upgrades)
                if (line.Short > 0 && line.Buy.Peek(market) != null)
                    return;
            while (surplus > 0)
            {
                var quote = _ridingSell.Peek(market, market.MarketGoldLeft);
                if (quote == null)
                    break;
                PlanMath.Take(RidingRow, _ridingSell, quote.Value, market);
                _ctx.Gold += quote.Value.Price;
                surplus--;
                _soldRiding = true;
            }
        }

        /// <summary>
        /// Riding mounts first, then upgrade horses (DESIGN §3), both under MinGoldForHorses. With
        /// WarMountsCountAsMounts the upgrade horses about to be bought count toward the riding target; how many
        /// that will be depends on the money the riding buys leave, so it is found by simulation: pledge all,
        /// and while fewer upgrade horses are affordable, buy more riding mounts instead.
        /// </summary>
        public void PlanBuys()
        {
            var settings = _ctx.Settings;
            int ridingNeed = RidingRow != null && _ridingBuy != null && !_soldRiding
                ? Math.Max(0, RidingTarget - RidingCounted)
                : 0;
            int shortTotal = _upgrades.Sum(u => u.Short);

            int ridingCap = ridingNeed;
            if (settings.WarMountsCountAsMounts && ridingNeed > 0 && shortTotal > 0)
            {
                int pledged = shortTotal;
                while (true)
                {
                    ridingCap = Math.Max(0, ridingNeed - pledged);
                    var market = _ctx.Market.Clone();
                    int gold = _ctx.Gold;
                    BuyRiding(market, ref gold, _ridingBuy!.Clone(), ridingCap, null);
                    int got = BuyUpgrades(market, ref gold, _upgrades.Select(u => u.Buy.Clone()).ToList(), false);
                    if (got >= pledged)
                        break;
                    pledged = got;
                }
            }

            int purse = _ctx.Gold;
            if (_ridingBuy != null && RidingRow != null)
                BuyRiding(_ctx.Market, ref purse, _ridingBuy, ridingCap, RidingRow);
            BuyUpgrades(_ctx.Market, ref purse, _upgrades.Select(u => u.Buy).ToList(), true);
            _ctx.Gold = purse;
        }

        private void BuyRiding(MarketState market, ref int gold, LaneCursor cursor, int cap, PlanRow? row)
        {
            for (int bought = 0; bought < cap; bought++)
            {
                var quote = cursor.Peek(market, gold - _ctx.AnimalFloor);
                if (quote == null)
                    return;
                if (row != null)
                    PlanMath.Take(row, cursor, quote.Value, market);
                else
                    cursor.Take(quote.Value, market);
                gold -= quote.Value.Price;
            }
        }

        /// <summary>The cheapest next upgrade horse across the categories still short; ties → category order.</summary>
        private int BuyUpgrades(MarketState market, ref int gold, IReadOnlyList<LaneCursor> cursors, bool record)
        {
            var left = _upgrades.Select(u => u.Short).ToArray();
            int total = 0;
            while (true)
            {
                int ceiling = gold - _ctx.AnimalFloor;
                int bestIndex = -1;
                UnitQuote bestQuote = default;
                for (int i = 0; i < cursors.Count; i++)
                {
                    if (left[i] <= 0)
                        continue;
                    var quote = cursors[i].Peek(market, ceiling);
                    if (quote != null && (bestIndex < 0 || quote.Value.Price < bestQuote.Price))
                    {
                        bestIndex = i;
                        bestQuote = quote.Value;
                    }
                }
                if (bestIndex < 0)
                    return total;
                if (record)
                    PlanMath.Take(_upgrades[bestIndex].Row, cursors[bestIndex], bestQuote, market);
                else
                    cursors[bestIndex].Take(bestQuote, market);
                gold -= bestQuote.Price;
                left[bestIndex]--;
                total++;
            }
        }

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
