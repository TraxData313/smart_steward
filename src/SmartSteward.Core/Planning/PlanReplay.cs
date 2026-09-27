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
            IPriceOracle oracle, bool foodBalanced, Comparison<ItemStack> lootOrder, bool ransom, bool donate, int dungeonRoom)
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
            PartyRoom = snapshot.Party.Room;
            CompanionSlots = snapshot.Party.CompanionSlotsFree;
        }

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
        public int PartyRoom { get; }
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
    /// </summary>
    internal static class PlanReplay
    {
        public static WalkOutcome Walk(PlanInputs inputs, IReadOnlyList<PlanRow> rows, Func<PlanRow, int> changeOf)
        {
            var log = new List<WalkStep>();
            var market = new MarketState(inputs.Oracle, inputs.Snapshot.MarketGold);
            var walk = new WalkState(market, inputs.Snapshot.PlayerGold, log);
            var all = rows.Select(r => new RowWalk(r, changeOf(r))).ToList();
            List<RowWalk> Of(params RowType[] types) => all.Where(o => Array.IndexOf(types, o.Row.Type) >= 0).ToList();

            // 1. Prisoners — ransom gold (paid by the game) and donations.
            var prisoners = Of(RowType.Prisoner);
            var moves = PrisonerPlanner.Split(prisoners.Select(p => -p.Requested).ToList(),
                inputs.Ransom, inputs.Donate, inputs.DungeonRoom);
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

            // 2. Sell: food (most held first), pack, riding, upgrade horses, loot (in SellLootOrder across groups).
            var lines = new Dictionary<RowWalk, WalkLine>();
            WalkLine Line(RowWalk o, TradeLane lane, int quota)
            {
                var line = new WalkLine(o.Row, lane, o.Row.Mine, quota, o.Book);
                lines[o] = line;
                return line;
            }

            var itemRows = Of(RowType.Food, RowType.Pack, RowType.Mount, RowType.WarMount, RowType.Loot);
            var sells = itemRows.Where(o => o.Requested < 0 && o.Row.SellLane != null).ToList();
            var buys = itemRows.Where(o => o.Requested > 0 && o.Row.BuyLane != null).ToList();
            int? goldLeft() => market.MarketGoldLeft;
            int? noCeiling() => null;

            PlanWalk.SellMostHeldFirst(walk,
                sells.Where(o => o.Row.Type == RowType.Food).Select(o => Line(o, o.Row.SellLane!, -o.Requested)).ToList(),
                () => true);
            foreach (var o in sells.Where(o => o.Row.Section == PlanSectionKind.Mounts))
                PlanWalk.WalkLane(walk, Line(o, o.Row.SellLane!, -o.Requested), int.MaxValue, goldLeft);
            PlanWalk.SellInOrder(walk,
                sells.Where(o => o.Row.Type == RowType.Loot).Select(o => Line(o, o.Row.SellLane!, -o.Requested)).ToList(),
                inputs.LootOrder);

            // 3. Buy: food, pack, riding, upgrade horses (the cheapest next across categories).
            PlanWalk.BuyFood(walk,
                buys.Where(o => o.Row.Type == RowType.Food).Select(o => Line(o, o.Row.BuyLane!, o.Requested)).ToList(),
                inputs.FoodBalanced, noCeiling, () => true);
            foreach (var o in buys.Where(o => o.Row.Type == RowType.Pack || o.Row.Type == RowType.Mount))
                PlanWalk.WalkLane(walk, Line(o, o.Row.BuyLane!, o.Requested), int.MaxValue, noCeiling);
            PlanWalk.BuyCheapestAcross(walk,
                buys.Where(o => o.Row.Type == RowType.WarMount).Select(o => Line(o, o.Row.BuyLane!, o.Requested)).ToList(),
                noCeiling);

            foreach (var o in itemRows)
            {
                o.Realized = o.Book.Net;
                if (!o.IsShort)
                    continue;
                if (lines.TryGetValue(o, out var line))
                    o.Short = ToBlock(line.Short, line.Direction);
                else
                    o.Short = o.Requested > 0
                        ? (o.Row.Type == RowType.Loot ? EditBlock.SellOnly : EditBlock.NoneEligible)
                        : EditBlock.NothingToSell;
            }

            // 4. The tavern — hires after the trades, wanderers then mercenaries; the party's room and the
            //    clan's companion slots are shared by every hire.
            int slots = inputs.CompanionSlots, room = inputs.PartyRoom;
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
                        else if (room <= 0)
                            o.Short = EditBlock.PartyFull;
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
                    hired = Math.Min(want, Math.Min(available, room));
                    if (hired < want)
                        o.Short = room < available ? EditBlock.PartyFull : EditBlock.AllOnOffer;
                }
                if (o.Requested < 0)
                    o.Short = EditBlock.BuyOnly;
                room -= hired;
                walk.Gold -= hired * info.UnitPrice;
                o.Realized = hired;
            }

            return new WalkOutcome(all, log, walk.Gold);
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
