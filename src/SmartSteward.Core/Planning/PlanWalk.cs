using System;
using System.Collections.Generic;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>The units one row moved, per stack and direction, in the order first moved.</summary>
    internal sealed class TallyBook
    {
        private readonly List<StackTally> _tallies = new List<StackTally>();
        private readonly Dictionary<string, StackTally> _buys = new Dictionary<string, StackTally>(StringComparer.Ordinal);
        private readonly Dictionary<string, StackTally> _sells = new Dictionary<string, StackTally>(StringComparer.Ordinal);
        private StackTally? _last;

        public IReadOnlyList<StackTally> Tallies => _tallies;

        /// <summary>Books one unit. Consecutive units of one stack are the common case (a lane walks a stack out),
        /// so the last tally is tried first; the rest is a lookup, never a scan (PLAN step 9, review area 5).</summary>
        public void Add(ItemStack stack, TradeDirection direction, int price)
        {
            var tally = _last;
            if (tally == null || tally.Direction != direction || !string.Equals(tally.Stack.Key, stack.Key, StringComparison.Ordinal))
            {
                var index = direction == TradeDirection.Buy ? _buys : _sells;
                if (!index.TryGetValue(stack.Key, out tally))
                {
                    tally = new StackTally(stack, direction);
                    _tallies.Add(tally);
                    index[stack.Key] = tally;
                }
                _last = tally;
            }
            tally.Add(price);
        }

        /// <summary>Units bought minus units sold.</summary>
        public int Net
        {
            get
            {
                int net = 0;
                foreach (var t in _tallies)
                    net += t.Direction == TradeDirection.Buy ? t.Count : -t.Count;
                return net;
            }
        }
    }

    /// <summary>One unit moved by the walk — the raw material of the executor's transactions.</summary>
    internal readonly struct WalkStep
    {
        public WalkStep(PlanRow row, ItemStack stack, TradeDirection direction, int price)
        {
            Row = row;
            Stack = stack;
            Direction = direction;
            Price = price;
        }

        public PlanRow Row { get; }
        public ItemStack Stack { get; }
        public TradeDirection Direction { get; }
        public int Price { get; }
    }

    /// <summary>
    /// One row's lane in one walk: the cursor along it, the units the row holds as the walk goes (food picks
    /// the type held the fewest / most of), and an optional quota. The planner walks with no quota (its own
    /// targets and the purse decide when to stop); the editor walks with each row's chosen quantity as quota.
    /// </summary>
    internal sealed class WalkLine
    {
        public WalkLine(PlanRow? row, LaneCursor cursor, int held = 0, int? quota = null, TallyBook? book = null)
        {
            Row = row;
            Cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            Held = held;
            Quota = quota;
            Book = book;
        }

        public WalkLine(PlanRow? row, TradeLane lane, int held = 0, int? quota = null, TallyBook? book = null)
            : this(row, new LaneCursor(lane), held, quota, book)
        {
        }

        /// <summary>The row the units belong to; null in a what-if simulation.</summary>
        public PlanRow? Row { get; }
        public LaneCursor Cursor { get; }
        public TradeDirection Direction => Cursor.Lane.Direction;
        public int Held { get; set; }

        /// <summary>Units the line may still move; null = no quota.</summary>
        public int? Quota { get; set; }

        /// <summary>Where the units are booked; null = nowhere (a simulation).</summary>
        public TallyBook? Book { get; }

        /// <summary>Loot: a piece the market could not pay stopped the group.</summary>
        public bool Stopped { get; set; }

        /// <summary>Why the line ended with quota left (<see cref="LaneStop.None"/> = it did not).</summary>
        public LaneStop Short { get; set; }

        public bool Wants => Quota == null || Quota > 0;
    }

    /// <summary>The market and the purse as one walk moves them, and (for the editor) its unit log.</summary>
    internal sealed class WalkState
    {
        public WalkState(MarketState market, int gold, List<WalkStep>? log = null)
        {
            Market = market;
            Gold = gold;
            Log = log;
        }

        public MarketState Market { get; }

        /// <summary>The purse as the walk goes: gold now + earned − spent.</summary>
        public int Gold { get; set; }

        public List<WalkStep>? Log { get; }

        /// <summary>A what-if copy: its own market and purse, no log.</summary>
        public WalkState Simulation() => new WalkState(Market.Clone(), Gold);

        /// <summary>Moves the quoted unit along the line: market, purse, the line's count and quota, its book
        /// and the log.</summary>
        public void Take(WalkLine line, UnitQuote quote)
        {
            line.Cursor.Take(quote, Market);
            bool selling = line.Direction == TradeDirection.Sell;
            Gold += selling ? quote.Price : -quote.Price;
            line.Held += selling ? -1 : 1;
            if (line.Quota != null)
                line.Quota--;
            line.Book?.Add(quote.Stack, line.Direction, quote.Price);
            if (line.Row != null)
                Log?.Add(new WalkStep(line.Row, quote.Stack, line.Direction, quote.Price));
        }
    }

    /// <summary>
    /// The picking rules of the canonical walk (DESIGN §3, §4.1) — ONE implementation, used by the planner
    /// (with its targets and the purse floors as ceilings) and by the plan editor (with each row's chosen
    /// quantity as quota and no purse ceiling). Every price comes from <see cref="LaneCursor.Peek"/>, so
    /// the two can never price a unit differently. The order the phases run in lives in
    /// <see cref="StewardPlanner"/> (planner) and <see cref="PlanReplay"/> (editor).
    /// </summary>
    internal static class PlanWalk
    {
        /// <summary>Food surplus: the line holding the most goes first (keeps variety), ties → the dearer unit;
        /// a unit only while the market can pay for it.</summary>
        public static void SellMostHeldFirst(WalkState walk, IReadOnlyList<WalkLine> lines, Func<bool> more)
        {
            var market = walk.Market;
            while (more())
            {
                WalkLine? best = null;
                UnitQuote bestQuote = default;
                foreach (var line in lines)
                {
                    if (!line.Wants)
                        continue;
                    var quote = line.Cursor.Peek(market, market.MarketGoldLeft);
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
                walk.Take(best, bestQuote);
            }
            MarkShort(lines, market, market.MarketGoldLeft);
        }

        /// <summary>Food buys: Balanced — the line held the fewest of, ties → the cheapest; Cheapest — the
        /// cheapest, ties → the fewest held. Each unit at most <paramref name="ceiling"/> (null = none).</summary>
        public static void BuyFood(WalkState walk, IReadOnlyList<WalkLine> lines, bool balanced,
            Func<int?> ceiling, Func<bool> more)
        {
            var market = walk.Market;
            while (more())
            {
                int? limit = ceiling();
                if (limit < 0)
                    break;
                WalkLine? best = null;
                UnitQuote bestQuote = default;
                foreach (var line in lines)
                {
                    if (!line.Wants)
                        continue;
                    var quote = line.Cursor.Peek(market, limit);
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
                walk.Take(best, bestQuote);
            }
            MarkShort(lines, market, ceiling());
        }

        /// <summary>One lane by its own pick rule (pack and riding animals: buys the cheapest, sells the dearest)
        /// — up to <paramref name="count"/> units, each at most the ceiling. Returns the units moved.</summary>
        public static int WalkLane(WalkState walk, WalkLine line, int count, Func<int?> ceiling)
        {
            int moved = 0;
            while (moved < count && line.Wants)
            {
                var quote = line.Cursor.Peek(walk.Market, ceiling());
                if (quote == null)
                    break;
                walk.Take(line, quote.Value);
                moved++;
            }
            MarkShort(new[] { line }, walk.Market, ceiling());
            return moved;
        }

        /// <summary>Upgrade horses: the cheapest next unit across the lines (categories), ties → the earlier
        /// line; each at most the ceiling. Returns the units moved.</summary>
        public static int BuyCheapestAcross(WalkState walk, IReadOnlyList<WalkLine> lines, Func<int?> ceiling)
        {
            var market = walk.Market;
            int total = 0;
            while (true)
            {
                int? limit = ceiling();
                WalkLine? best = null;
                UnitQuote bestQuote = default;
                foreach (var line in lines)
                {
                    if (!line.Wants)
                        continue;
                    var quote = line.Cursor.Peek(market, limit);
                    if (quote != null && (best == null || quote.Value.Price < bestQuote.Price))
                    {
                        best = line;
                        bestQuote = quote.Value;
                    }
                }
                if (best == null)
                    break;
                walk.Take(best, bestQuote);
                total++;
            }
            MarkShort(lines, market, ceiling());
            return total;
        }

        /// <summary>
        /// Loot: every group's next piece in the lane's (SellLootOrder) order, the one the order prefers first
        /// — so a poor market's gold goes to the preferred pieces whatever their group. A group stops at its
        /// first piece the market can no longer pay, so "−N" is always the first N of the group.
        /// </summary>
        public static void SellInOrder(WalkState walk, IReadOnlyList<WalkLine> lines, Comparison<ItemStack> order)
        {
            var market = walk.Market;
            while (true)
            {
                WalkLine? best = null;
                UnitQuote bestQuote = default;
                foreach (var line in lines)
                {
                    if (line.Stopped || !line.Wants)
                        continue;
                    var quote = line.Cursor.Peek(market);
                    if (quote == null)
                        continue;
                    if (best == null || order(quote.Value.Stack, bestQuote.Stack) < 0)
                    {
                        best = line;
                        bestQuote = quote.Value;
                    }
                }
                if (best == null)
                    break;
                if (bestQuote.Price > market.MarketGoldLeft)
                {
                    best.Stopped = true;
                    continue;
                }
                walk.Take(best, bestQuote);
            }
            foreach (var line in lines)
                if (line.Quota > 0)
                    line.Short = line.Stopped ? LaneStop.Ceiling : line.Cursor.WhyNot(market);
        }

        private static void MarkShort(IEnumerable<WalkLine> lines, MarketState market, int? ceiling)
        {
            foreach (var line in lines)
                if (line.Quota > 0)
                    line.Short = line.Cursor.WhyNot(market, ceiling);
        }
    }
}
