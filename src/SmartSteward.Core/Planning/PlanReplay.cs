using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>What the editor needs of a planning run to walk the plan again — captured when the plan is
    /// made, so an edit prices with the rules the suggestion was made with.</summary>
    internal sealed class PlanInputs
    {
        public PlanInputs(StewardSnapshot snapshot, StewardSettings settings, PlanMode mode, MoneyFloors floors,
            IPriceOracle oracle, bool foodBalanced, Comparison<ItemStack> lootOrder, bool ransom, bool donate, int dungeonRoom,
            Dictionary<string, int>? goals = null)
        {
            Snapshot = snapshot;
            Settings = settings;
            Mode = mode;
            Floors = floors;
            Oracle = oracle as CachingPriceOracle ?? new CachingPriceOracle(oracle); // the planner's own cache, warm
            FoodBalanced = foodBalanced;
            LootOrder = lootOrder;
            Ransom = ransom;
            Donate = donate;
            DungeonRoom = dungeonRoom;
            CompanionSlots = snapshot.Party.CompanionSlotsFree;
            Goals = goals ?? ManualGoals.CopyOf(settings);
            GoalFloors = MoneyFloors.ForGoals(settings, mode);
        }

        /// <summary>The plan's own copy of the player's standing goals (round 5) — edited by <see cref="StewardPlan.SetGoal"/>,
        /// passed to every re-plan; the window saves the edits (<see cref="StewardPlan.TakeGoalEdits"/>).</summary>
        public Dictionary<string, int> Goals { get; }

        /// <summary>The floors the goals answer to (<see cref="MoneyFloors.ForGoals"/>).</summary>
        public (int? Food, int? Animals) GoalFloors { get; }

        public StewardSnapshot Snapshot { get; }
        public StewardSettings Settings { get; }
        public PlanMode Mode { get; }
        public MoneyFloors Floors { get; }
        public IPriceOracle Oracle { get; }
        public bool FoodBalanced { get; }
        public Comparison<ItemStack> LootOrder { get; }
        public bool Ransom { get; }
        public bool Donate { get; }
        public int DungeonRoom { get; }
        public int CompanionSlots { get; }
    }

    /// <summary>One row in one replay: the quantity asked, the quantity the walk could do, and why not all.</summary>
    internal sealed class RowWalk
    {
        public RowWalk(PlanRow row, int requested)
        {
            Row = row;
            Requested = requested;
        }

        public PlanRow Row { get; }
        public int Requested { get; }
        public int Realized { get; set; }
        public bool IsShort => Realized != Requested;

        /// <summary>Why <see cref="Realized"/> fell short of <see cref="Requested"/>.</summary>
        public EditBlock Short { get; set; }

        /// <summary>Item rows: the units moved, with their prices.</summary>
        public TallyBook Book { get; } = new TallyBook();

        public int Donated { get; set; }
        public int Ransomed { get; set; }

        /// <summary>A wanderer hired with no more gold than his price left in the purse (vanilla wants more).</summary>
        public bool HireUnaffordable { get; set; }
    }

    /// <summary>The result of one replay.</summary>
    internal sealed class WalkOutcome
    {
        private readonly Dictionary<PlanRow, RowWalk> _byRow;

        public WalkOutcome(IReadOnlyList<RowWalk> rows, IReadOnlyList<WalkStep> steps, int goldAfter)
        {
            Rows = rows;
            Steps = steps;
            GoldAfter = goldAfter;
            _byRow = rows.ToDictionary(r => r.Row);
        }

        /// <summary>Every row of the plan, in the plan's order.</summary>
        public IReadOnlyList<RowWalk> Rows { get; }

        /// <summary>Every item unit moved, in the walk's order.</summary>
        public IReadOnlyList<WalkStep> Steps { get; }

        /// <summary>The purse at the end of the walk (after the hires).</summary>
        public int GoldAfter { get; }

        public bool HireUnaffordable => Rows.Any(r => r.HireUnaffordable);
        public bool AllRealized => Rows.All(r => !r.IsShort);

        public RowWalk Of(PlanRow row) => _byRow[row];
    }

    /// <summary>
    /// Walks the plan again at the rows' current quantities — the canonical order of
    /// <see cref="StewardPlanner"/>, the picking rules of <see cref="PlanWalk"/>, each row's quantity as its
    /// quota, NO purse floor (the player's hand overrides the steward; breaches are flags) but still the lanes'
    /// price limits, the market's stock and the market's gold. For an unedited plan it reproduces the planner
    /// unit for unit: prices only climb while buying and fall while selling, so the same picking capped by the
    /// planner's own quantities makes the same choices.
    /// <para>The precedence of the live re-plan (step 15, <see cref="PlanPins"/>): in every phase the rows the player's hand
    /// is on (<paramref name="touchedOf"/>) walk first, then the steward's — the order the re-plan planned the steward's rows
    /// in, so what it planned is exactly what the walk can do. With no row touched it is the planner's own order.</para>
    /// </summary>
    internal static class PlanReplay
    {
        public static WalkOutcome Walk(PlanInputs inputs, IReadOnlyList<PlanRow> rows, Func<PlanRow, int> changeOf,
            Func<PlanRow, bool>? touchedOf = null)
        {
            var log = new List<WalkStep>();
            var market = new MarketState(inputs.Oracle, inputs.Snapshot.MarketGold);
            var walk = new WalkState(market, inputs.Snapshot.PlayerGold, log);
            var all = rows.Select(r => new RowWalk(r, changeOf(r))).ToList();
            List<RowWalk> Of(params RowType[] types) => all.Where(o => Array.IndexOf(types, o.Row.Type) >= 0).ToList();
            touchedOf ??= r => r.IsTouched;
            var players = new HashSet<PlanRow>(rows.Where(touchedOf));
            // The player's rows first, then the steward's (with nothing touched: one pass, the planner's order).
            var passes = players.Count == 0 ? new[] { false } : new[] { true, false };
            List<RowWalk> Pass(IEnumerable<RowWalk> phase, bool player) =>
                phase.Where(o => players.Contains(o.Row) == player).ToList();

            // 1. Prisoners — ransom gold (paid by the game) and donations.
            var prisoners = Of(RowType.Prisoner);
            var moves = PrisonerPlanner.Split(prisoners.Select(p => -p.Requested).ToList(),
                prisoners.Select(p => p.Row.Prisoner!).ToList(), inputs.DungeonRoom);
            for (int i = 0; i < prisoners.Count; i++)
            {
                var p = prisoners[i];
                p.Donated = moves[i].Donated;
                p.Ransomed = moves[i].Ransomed;
                p.Realized = -moves[i].Moved;
                if (p.IsShort)
                    p.Short = p.Requested > 0 ? EditBlock.SellOnly : EditBlock.DungeonFull;
                walk.Gold += p.Ransomed * p.Row.Prisoner!.RansomValue;
            }

            // 2. Sell: food (most held first), the animals in their rank order (lame, pack, noble, war, riding), loot (in
            //    SellLootOrder across groups).
            var lines = new Dictionary<RowWalk, WalkLine>();
            WalkLine Line(RowWalk o, TradeLane lane, int quota)
            {
                var line = new WalkLine(o.Row, lane, o.Row.Mine, quota, o.Book);
                lines[o] = line;
                return line;
            }
            // Round 5: a goal of the player's (a touched food or pack / riding / war row) walks its hand lanes (without the price
            // limits when ManualGoalsObeyPriceCaps is off) and buys under the goals' floors (ManualGoalsKeepPurseFloor).
            bool Hand(RowWalk o) => o.Row.TakesGoal && players.Contains(o.Row);
            TradeLane SellLaneOf(RowWalk o) => Hand(o) ? o.Row.HandSellLane! : o.Row.SellLane!;
            TradeLane BuyLaneOf(RowWalk o) => Hand(o) ? o.Row.HandBuyLane! : o.Row.BuyLane!;
            var floored = new HashSet<RowWalk>();
            int? goalFood() => inputs.GoalFloors.Food == null ? (int?)null : walk.Gold - inputs.GoalFloors.Food.Value;
            int? goalAnimals() => inputs.GoalFloors.Animals == null ? (int?)null : walk.Gold - inputs.GoalFloors.Animals.Value;
            List<RowWalk> Floored(List<RowWalk> rows, bool player, bool food)
            {
                if (player && (food ? inputs.GoalFloors.Food : inputs.GoalFloors.Animals) != null)
                    foreach (var o in rows)
                        if (Hand(o))
                            floored.Add(o);
                return rows;
            }

            var itemRows = Of(RowType.Food, RowType.Pack, RowType.Mount, RowType.WarMount, RowType.Loot);
            var sells = itemRows.Where(o => o.Requested < 0 && o.Row.SellLane != null).ToList();
            var buys = itemRows.Where(o => o.Requested > 0 && o.Row.BuyLane != null).ToList();
            int? goldLeft() => market.MarketGoldLeft;
            int? noCeiling() => null;

            foreach (bool player in passes)
                PlanWalk.SellMostHeldFirst(walk,
                    Pass(sells.Where(o => o.Row.Type == RowType.Food), player)
                        .Select(o => Line(o, SellLaneOf(o), -o.Requested)).ToList(),
                    () => true);
            foreach (bool player in passes)
                foreach (var o in Pass(sells.Where(o => o.Row.Section == PlanSectionKind.Mounts), player)
                             .OrderBy(o => AnimalSellRank(o.Row)))
                    PlanWalk.WalkLane(walk, Line(o, SellLaneOf(o), -o.Requested), int.MaxValue, goldLeft);
            foreach (bool player in passes)
                PlanWalk.SellInOrder(walk,
                    Pass(sells.Where(o => o.Row.Type == RowType.Loot), player)
                        .Select(o => Line(o, o.Row.SellLane!, -o.Requested)).ToList(),
                    inputs.LootOrder);

            // 3. Buy: food, pack, riding, war horses (noble and lame horses are never bought).
            foreach (bool player in passes)
                PlanWalk.BuyFood(walk,
                    Floored(Pass(buys.Where(o => o.Row.Type == RowType.Food), player), player, true)
                        .Select(o => Line(o, BuyLaneOf(o), o.Requested)).ToList(),
                    inputs.FoodBalanced, player ? goalFood : noCeiling, () => true);
            foreach (bool player in passes)
            {
                foreach (var o in Floored(Pass(buys.Where(o => o.Row.Type == RowType.Pack || o.Row.Type == RowType.Mount), player),
                             player, false))
                    PlanWalk.WalkLane(walk, Line(o, BuyLaneOf(o), o.Requested), int.MaxValue,
                        player && Hand(o) ? goalAnimals : noCeiling);
                PlanWalk.BuyCheapestAcross(walk,
                    Floored(Pass(buys.Where(o => o.Row.Type == RowType.WarMount), player), player, false)
                        .Select(o => Line(o, BuyLaneOf(o), o.Requested)).ToList(),
                    player ? goalAnimals : noCeiling);
            }

            foreach (var o in itemRows)
            {
                o.Realized = o.Book.Net;
                if (!o.IsShort)
                    continue;
                if (lines.TryGetValue(o, out var line))
                    o.Short = floored.Contains(o) && line.Short == LaneStop.Ceiling && line.Direction == TradeDirection.Buy
                        ? EditBlock.PurseFloor
                        : ToBlock(line.Short, line.Direction);
                else
                    o.Short = o.Requested > 0
                        ? (o.Row.Type == RowType.Loot ? EditBlock.SellOnly : EditBlock.NoneEligible)
                        : EditBlock.NothingToSell;
            }

            // 4. The tavern — hires after the trades, wanderers then mercenaries; the clan's companion slots are
            //    shared by the wanderers. The party size limit never blocks a hire (round 3 — vanilla has no such
            //    check either): the footer shows the party after the deal against it.
            int slots = inputs.CompanionSlots;
            foreach (var o in Of(RowType.Tavern))
            {
                var info = o.Row.Tavern!;
                int want = Math.Max(0, o.Requested);
                int hired = 0;
                if (info.Kind == TavernRowKind.Wanderer)
                {
                    if (want > 0)
                    {
                        if (slots <= 0)
                            o.Short = EditBlock.CompanionLimit;
                        else
                        {
                            hired = 1;
                            slots--;
                            // Vanilla hires a companion only with MORE gold than his price (RESEARCH §7).
                            o.HireUnaffordable = walk.Gold <= info.UnitPrice;
                        }
                        if (want > 1 && o.Short == EditBlock.None)
                            o.Short = EditBlock.AllOnOffer;
                    }
                }
                else
                {
                    int available = Math.Max(0, o.Row.Market ?? 0);
                    hired = Math.Min(want, available);
                    if (hired < want)
                        o.Short = EditBlock.AllOnOffer;
                }
                if (o.Requested < 0)
                    o.Short = EditBlock.BuyOnly;
                walk.Gold -= hired * info.UnitPrice;
                o.Realized = hired;
            }

            // 5. The troops section (step 16) — recruits after the hires (paid last, the table's order), clamped to what the
            //    notables offer the player; dismissals clamped to the men the party screen lets go, and free. The party
            //    size limit never blocks either.
            foreach (var o in Of(RowType.Troop))
            {
                var info = o.Row.Troop!;
                int done = o.Requested;
                if (done > o.Row.MaxBuy)
                {
                    done = Math.Max(0, o.Row.MaxBuy);
                    o.Short = o.Row.MaxBuy <= 0 ? EditBlock.NotOnOfferHere : EditBlock.AllOnOffer;
                }
                else if (done < -o.Row.MaxSell)
                {
                    done = -Math.Max(0, o.Row.MaxSell);
                    o.Short = o.Row.MaxSell <= 0 ? EditBlock.NoneToDismiss : EditBlock.AllDismissed;
                }
                walk.Gold -= Math.Max(0, done) * info.UnitPrice;
                o.Realized = done;
            }

            return new WalkOutcome(all, log, walk.Gold);
        }

        /// <summary>
        /// The order the animal rows SELL in — the planner's (<see cref="StewardPlanner"/>) and the replay's alike (step 17): the
        /// lame horses first (they are replaced, and fetch little), then pack animals, noble horses, war horses above the number
        /// to keep, and last the riding horses — whose surplus is counted after every other kept horse is known. The table
        /// shows them Pack · Riding · War · Noble · Lame.
        /// </summary>
        public static int AnimalSellRank(PlanRow row)
        {
            switch (row.Role)
            {
                case MountRole.Lame: return 0;
                case MountRole.Pack: return 1;
                case MountRole.Noble: return 2;
                case MountRole.War: return 3;
                case MountRole.Riding: return 4;
                default: return 5;
            }
        }

        /// <summary>A lane's stop, in the words of the row's buttons.</summary>
        public static EditBlock ToBlock(LaneStop stop, TradeDirection direction)
        {
            bool buying = direction == TradeDirection.Buy;
            switch (stop)
            {
                case LaneStop.Exhausted: return buying ? EditBlock.AllOnOffer : EditBlock.AllSold;
                case LaneStop.StockTaken: return EditBlock.NeededByAnotherRow;
                case LaneStop.PriceLimit: return buying ? EditBlock.PriceLimit : EditBlock.BelowMinSellPrice;
                case LaneStop.Ceiling: return buying ? EditBlock.NotEnoughGold : EditBlock.MarketOutOfGold;
                default: return buying ? EditBlock.NoneEligible : EditBlock.NothingToSell;
            }
        }
    }
}
