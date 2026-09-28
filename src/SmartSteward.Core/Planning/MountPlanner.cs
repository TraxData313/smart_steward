using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The horses for the footmen (DESIGN §2.3–§2.4) as the role rows of §1.1.1: Riding mounts, War horses, Noble horses —
    /// the model of <see cref="MountGoal"/> (Anton 2026.09.28, step 17: no upgrade is counted any more).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Horses to keep T = ceil(footmen after the deal × MountsPer100Footmen / 100). EVERY mount kept counts toward it —
    ///   riding horses, war horses, noble horses kept (locked, or SellNobleHorses off) and lame ones kept (ReplaceLameHorses
    ///   off, a guarding lock, or unsold) — because the game lets a footman ride any of them (RESEARCH §3).</item>
    /// <item>War horses (war_horse): a plain number W = WarMountsToKeep — the cheapest eligible bought up to it (under
    ///   WarMountMaxPrice), the dearest sold above it (SellWarMountSurplus).</item>
    /// <item>Riding horses fill the rest: bought (cheapest eligible, under MountMaxPrice) while the horses kept after the deal
    ///   are short of T, sold (dearest first, SellMountSurplus) while above it. Riding mounts outrank war horses for the purse
    ///   (DESIGN §3): when the purse cannot pay for every war horse short, riding horses fill the gap first (the pledge
    ///   simulation). The riding surplus is sold against the war horses the party will HAVE — those it buys in this visit
    ///   too, as the planner's first pass found them (<see cref="PlanContext.WarPledge"/>, <see cref="PledgeHint"/>).</item>
    /// <item>Noble horses (noble_horse): never bought; every one NOT locked is sold (a lock always keeps a noble horse —
    ///   <see cref="LockRule"/>), each at no less than its min sell price.</item>
    /// </list>
    /// Modified horses are never bought (lame and old ones by Anton's rule; any other modifier as since step 4). A lame one held
    /// sits in the Lame horses row while ReplaceLameHorses is on (<see cref="LameHorsePlanner"/>) and is counted here only
    /// when that row could not sell it. The sale order among the animal rows is <see cref="PlanReplay.AnimalSellRank"/>: lame,
    /// pack, noble, war, riding — each sees what the ones before it left.
    /// </remarks>
    internal sealed class MountPlanner
    {
        public const string RidingId = "mounts:riding";
        public const string WarId = "mounts:war";
        public const string NobleId = "mounts:noble";

        private readonly PlanContext _ctx;
        private readonly LameHorsePlanner _lame;
        private readonly List<ItemStack> _riding;
        private readonly List<ItemStack> _war;
        private readonly List<ItemStack> _noble;
        private readonly int _ridingHeld;
        private readonly int _warHeld;
        private readonly int _nobleHeld;
        private readonly LaneCursor? _ridingBuy;
        private readonly LaneCursor? _warBuy;
        private readonly WalkLine? _ridingSell;
        private readonly WalkLine? _warSell;
        private readonly WalkLine? _nobleSell;

        /// <summary>The player's own quantities (touched rows in a live re-plan) — walked first, never re-planned.</summary>
        private readonly int? _ridingPinned;
        private readonly int? _warPinned;
        private readonly int? _noblePinned;
        private bool _soldRiding;

        public MountPlanner(PlanContext ctx, LameHorsePlanner lame)
        {
            _ctx = ctx;
            _lame = lame;
            var settings = ctx.Settings;
            var held = ctx.Inventory(ItemKind.Mount).Where(s => !lame.Holds(s)).ToList();
            _noble = held.Where(MountGoal.IsNoble).ToList();
            _war = held.Where(s => MountGoal.IsWar(s, settings)).ToList();
            _riding = held.Where(s => MountGoal.IsRiding(s, settings)).ToList();
            _ridingHeld = _riding.Sum(s => s.Count);
            _warHeld = _war.Sum(s => s.Count);
            _nobleHeld = _noble.Sum(s => s.Count);

            // The footmen of the party after the deal (the men hired on foot too — the live re-plan).
            Footmen = Math.Max(0, ctx.Party.Footmen);
            MountTarget = MountGoal.Total(settings, Footmen);
            WarTarget = MountGoal.War(settings);
            RidingTarget = settings.MountsEnabled ? MountGoal.Riding(settings, Footmen) : 0;

            if (!ctx.Snapshot.CanTrade)
                return;

            if (settings.MountsEnabled)
            {
                var sellLane = SellLane(_riding);
                var buyLane = BuyLane(s => MountGoal.IsRiding(s, settings), settings.MountMaxPrice);
                RidingRow = RoleRow(RidingId, RowType.Mount, MountRole.Riding, null, _riding, sellLane, buyLane);
                RidingRow.StartsAtDenari = ctx.StartsAt(ManagedJob.Mounts);
                _ridingBuy = new LaneCursor(buyLane);
                _ridingSell = new WalkLine(RidingRow, sellLane, book: RidingRow.Book);
                _ridingPinned = Pin(RidingRow);
            }

            if (settings.WarMountsEnabled && (_warHeld > 0 || WarTarget > 0 || ctx.Pins.Contains(WarId)))
            {
                var sellLane = SellLane(_war);
                var buyLane = BuyLane(s => MountGoal.IsWar(s, settings), settings.WarMountMaxPrice);
                WarRow = RoleRow(WarId, RowType.WarMount, MountRole.War, MountGoal.WarHorse, _war, sellLane, buyLane);
                WarRow.Target = WarTarget;
                WarRow.StartsAtDenari = ctx.StartsAt(ManagedJob.WarHorses);
                _warBuy = new LaneCursor(buyLane);
                _warSell = new WalkLine(WarRow, sellLane, book: WarRow.Book);
                _warPinned = Pin(WarRow);
            }

            if (settings.SellNobleHorses && _nobleHeld > 0)
            {
                var sellLane = SellLane(_noble);
                NobleRow = RoleRow(NobleId, RowType.Mount, MountRole.Noble, MountGoal.NobleHorse, _noble, sellLane, null);
                NobleRow.LocksGuard = true; // a lock always keeps a noble horse — one locked after the plan too, at the click
                NobleRow.StartsAtDenari = ctx.StartsAt(ManagedJob.Mounts);
                _nobleSell = new WalkLine(NobleRow, sellLane, book: NobleRow.Book);
                _noblePinned = Pin(NobleRow);
            }
        }

        public int Footmen { get; }

        /// <summary>T: the horses kept for the footmen after the deal (war, noble and lame ones kept count toward it).</summary>
        public int MountTarget { get; }

        /// <summary>W: the war horses to keep.</summary>
        public int WarTarget { get; }

        /// <summary>The riding horses the plan aims for: T minus the war, noble and lame horses kept after the deal (set in
        /// <see cref="Finish"/>; before that <c>max(0, T − W)</c>).</summary>
        public int RidingTarget { get; private set; }

        public PlanRow? RidingRow { get; }
        public PlanRow? WarRow { get; }
        public PlanRow? NobleRow { get; }

        public IEnumerable<PlanRow> Rows
        {
            get
            {
                if (RidingRow != null) yield return RidingRow;
                if (WarRow != null) yield return WarRow;
                if (NobleRow != null) yield return NobleRow;
            }
        }

        /// <summary>
        /// After this pass: the war horses the steward bought while the riding row still had a surplus it could sell against
        /// them — the planner then plans once more with them pledged (<see cref="PlanContext.WarPledge"/>), so the fresh plan
        /// after Do it has nothing left to sell. 0 = no second pass needed.
        /// </summary>
        public int PledgeHint
        {
            get
            {
                if (_ctx.WarPledge > 0 || WarRow == null || _warPinned != null || RidingRow == null || _ridingPinned != null
                    || !_ctx.Settings.SellMountSurplus || !RidingActs)
                    return 0;
                int bought = Bought(WarRow);
                bool sellableLeft = RidingRow.MaxSell - Sold(RidingRow) > 0;
                return bought > 0 && sellableLeft && Counted() > MountTarget ? bought : 0;
            }
        }

        /// <summary>The steward's riding side acts (MountsMinDenari met — round 4); it sells the noble horses too.</summary>
        private bool RidingActs => _ctx.JobActive(ManagedJob.Mounts);

        /// <summary>The steward's war side acts (WarHorsesMinDenari met — round 4).</summary>
        private bool WarActs => _ctx.JobActive(ManagedJob.WarHorses);

        private int? Pin(PlanRow row) => _ctx.Pins.TryGet(row.Id, out int pin) ? pin : (int?)null;

        private PlanRow RoleRow(string id, RowType type, MountRole role, string? category, List<ItemStack> stacks,
            TradeLane sellLane, TradeLane? buyLane)
        {
            var row = new PlanRow(id, PlanSectionKind.Mounts, type)
            {
                Role = role,
                CategoryId = category,
                Mine = stacks.Sum(s => s.Count),
                Locked = stacks.Where(_ctx.IsGuarded).Sum(s => s.Count),
                LocksGuard = _ctx.LockGuards(ItemKind.Mount),
                Market = buyLane == null ? (int?)null : PlanMath.EligibleOnOffer(buyLane, _ctx.Market),
                MaxSell = sellLane.Capacity,
                BuyLane = buyLane,
                SellLane = sellLane,
            };
            row.MaxBuy = row.Market ?? 0;
            return row;
        }

        /// <summary>What a row may sell: its stacks not guarded by a lock and Sell-ticked, each at its own min sell price.</summary>
        private TradeLane SellLane(IEnumerable<ItemStack> stacks) =>
            new TradeLane(TradeDirection.Sell, LanePick.MostExpensive,
                stacks.Where(s => !_ctx.IsGuarded(s) && _ctx.Book(s)!.SellTicked)
                    .Select(s => new LaneStack(s, s.Count, _ctx.MinSellOf(s))));

        /// <summary>What a row may buy: plain (unmodified) horses of its kind, Buy-ticked, under the price book AND the role cap.</summary>
        private TradeLane BuyLane(Func<ItemStack, bool> kind, int roleCap) =>
            new TradeLane(TradeDirection.Buy, LanePick.Cheapest,
                _ctx.MarketStacks(ItemKind.Mount)
                    .Where(s => !s.IsModified && kind(s) && _ctx.Book(s)!.BuyTicked)
                    .Select(s => new { Stack = s, Limit = PriceBook.AnimalBuyLimit(_ctx.Book(s)!.FinalMaxBuy, roleCap) })
                    .Where(x => x.Limit != null)
                    .Select(x => new LaneStack(x.Stack, x.Stack.Count, x.Limit)));

        private static int Sold(PlanRow? row) =>
            row == null ? 0 : row.Tallies.Where(t => t.Direction == TradeDirection.Sell).Sum(t => t.Count);

        private static int Bought(PlanRow? row) =>
            row == null ? 0 : row.Tallies.Where(t => t.Direction == TradeDirection.Buy).Sum(t => t.Count);

        private int RidingNow => _ridingHeld - Sold(RidingRow) + Bought(RidingRow);
        private int WarNow => _warHeld - Sold(WarRow) + Bought(WarRow);
        private int NobleNow => _nobleHeld - Sold(NobleRow);

        /// <summary>Every mount counting toward T as the walk stands: riding, war, noble kept, and the lame ones not sold.</summary>
        private int Counted() => RidingNow + WarNow + NobleNow + _lame.LeftOf(ItemKind.Mount);

        /// <summary>The war horses still to come in the buy phase, as the riding sale sees them: the player's own (a touched
        /// war row), or the steward's as its first pass found them (<see cref="PlanContext.WarPledge"/>).</summary>
        private int WarToBuy()
        {
            if (WarRow == null)
                return 0;
            if (_warPinned != null)
                return Math.Max(0, _warPinned.Value);
            return Math.Min(_ctx.WarPledge, Math.Max(0, WarTarget - WarNow));
        }

        /// <summary>The player's own mount sales (touched rows) — first among the animal sales, noble, war, riding
        /// (<see cref="PlanPins"/>).</summary>
        public void PlanPinnedSells()
        {
            PinnedSell(NobleRow, _noblePinned);
            PinnedSell(WarRow, _warPinned);
            PinnedSell(RidingRow, _ridingPinned);
        }

        private void PinnedSell(PlanRow? row, int? pinned)
        {
            var walk = _ctx.Walk;
            if (row != null && pinned < 0)
                PlanWalk.WalkLane(walk, new WalkLine(row, row.SellLane!, row.Mine, -pinned.Value, row.Book), int.MaxValue,
                    () => walk.Market.MarketGoldLeft);
        }

        /// <summary>The player's own mount buys — riding, then war — before any of the steward's.</summary>
        public void PlanPinnedBuys()
        {
            var walk = _ctx.Walk;
            if (RidingRow != null && _ridingPinned > 0)
                PlanWalk.WalkLane(walk, new WalkLine(RidingRow, RidingRow.BuyLane!, RidingRow.Mine, _ridingPinned.Value,
                    RidingRow.Book), int.MaxValue, () => null);
            if (WarRow != null && _warPinned > 0)
                PlanWalk.BuyCheapestAcross(walk,
                    new[] { new WalkLine(WarRow, WarRow.BuyLane!, WarRow.Mine, _warPinned.Value, WarRow.Book) }, () => null);
        }

        /// <summary>
        /// The steward's sales, in the rank order: every noble horse that may go; war horses above W; riding horses above
        /// what the footmen need once the war, noble and lame horses kept after the deal are counted.
        /// </summary>
        public void PlanSells()
        {
            var settings = _ctx.Settings;
            var walk = _ctx.Walk;
            if (NobleRow != null && _nobleSell != null && _noblePinned == null && RidingActs)
                PlanWalk.WalkLane(walk, _nobleSell, int.MaxValue, _ctx.AnimalSellCeiling);
            if (WarRow != null && _warSell != null && _warPinned == null && settings.SellWarMountSurplus && WarActs)
                PlanWalk.WalkLane(walk, _warSell, WarNow - WarTarget, _ctx.AnimalSellCeiling);
            if (RidingRow != null && _ridingSell != null && _ridingPinned == null && settings.SellMountSurplus && RidingActs)
            {
                int surplus = Counted() + WarToBuy() - MountTarget;
                if (surplus > 0)
                    _soldRiding = PlanWalk.WalkLane(walk, _ridingSell, surplus, _ctx.AnimalSellCeiling) > 0;
            }
        }

        /// <summary>
        /// Riding mounts first, then war horses (DESIGN §3), both under the animal floor. The war horses about to be bought
        /// count toward T, and how many that will be depends on the money the riding buys leave — so it is found by
        /// simulation: pledge them all, and while fewer war horses are affordable (or on offer), buy more riding horses
        /// instead. Never a riding horse in a visit that sells riding horses (a horse fetches about half its price — DESIGN
        /// §2.2); the player's own rows are already walked and counted.
        /// </summary>
        public void PlanBuys()
        {
            var walk = _ctx.Walk;
            bool stewardRides = RidingRow != null && _ridingBuy != null && _ridingPinned == null && !_soldRiding && RidingActs;
            bool stewardWar = WarRow != null && _warBuy != null && _warPinned == null && WarActs;
            int warShort = stewardWar ? Math.Max(0, WarTarget - WarNow) : 0;
            int ridingNeed = stewardRides ? Math.Max(0, MountTarget - Counted()) : 0;

            int ridingCap = ridingNeed;
            if (ridingNeed > 0 && warShort > 0)
            {
                int pledged = warShort;
                while (true)
                {
                    ridingCap = Math.Max(0, ridingNeed - pledged);
                    var whatIf = walk.Simulation();
                    BuyRiding(whatIf, new WalkLine(null, _ridingBuy!.Clone()), ridingCap);
                    int got = BuyWar(whatIf, new WalkLine(null, _warBuy!.Clone(), quota: warShort));
                    if (got >= pledged)
                        break;
                    pledged = got;
                }
            }

            if (stewardRides)
                BuyRiding(walk, new WalkLine(RidingRow, _ridingBuy!, book: RidingRow!.Book), ridingCap);
            if (stewardWar && warShort > 0)
                BuyWar(walk, new WalkLine(WarRow, _warBuy!, quota: warShort, book: WarRow!.Book));
        }

        private int BuyRiding(WalkState walk, WalkLine line, int cap) =>
            PlanWalk.WalkLane(walk, line, cap, () => _ctx.AnimalBuyCeiling(walk));

        private int BuyWar(WalkState walk, WalkLine line) =>
            PlanWalk.BuyCheapestAcross(walk, new[] { line }, () => _ctx.AnimalBuyCeiling(walk));

        public void Finish()
        {
            RidingTarget = RidingRow == null ? 0 : Math.Max(0, MountTarget - WarNow - NobleNow - _lame.LeftOf(ItemKind.Mount));
            if (RidingRow != null)
            {
                RidingRow.Target = RidingTarget;
                PlanMath.FinishItemRow(RidingRow, _riding.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
            }
            if (WarRow != null)
                PlanMath.FinishItemRow(WarRow, _war.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
            if (NobleRow != null)
                PlanMath.FinishItemRow(NobleRow, _noble.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
        }
    }
}
