using System;
using System.Collections.Generic;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Pricing
{
    /// <summary>Which stack a lane moves its next unit from.</summary>
    public enum LanePick
    {
        /// <summary>The lowest marginal price (buys: "the cheapest eligible").</summary>
        Cheapest,

        /// <summary>The highest marginal price (sells: "the most expensive surplus one").</summary>
        MostExpensive,

        /// <summary>The first stack with units left, in the lane's order (loot groups: the SellLootOrder).</summary>
        InOrder,
    }

    /// <summary>A stack a lane may move units of.</summary>
    public sealed class LaneStack
    {
        public LaneStack(ItemStack stack, int available, int? priceLimit)
        {
            Stack = stack ?? throw new ArgumentNullException(nameof(stack));
            Available = Math.Max(0, available);
            PriceLimit = priceLimit;
        }

        public ItemStack Stack { get; }

        /// <summary>Units this lane may move from the stack (a buy is capped again by the market's stock left,
        /// which several lanes may share).</summary>
        public int Available { get; }

        /// <summary>Buy: the highest unit price paid. Sell: the lowest unit price taken. Null = no limit.</summary>
        public int? PriceLimit { get; }

        public bool Accepts(TradeDirection direction, int price)
        {
            if (PriceLimit == null) return true;
            return direction == TradeDirection.Buy ? price <= PriceLimit.Value : price >= PriceLimit.Value;
        }
    }

    /// <summary>
    /// Everything one plan row can move in one direction: its stacks, their availability and price limits,
    /// and the pick rule. This is the "walk data" a row keeps, so a changed quantity can be re-priced by
    /// walking the same lane again (PLAN step 4b).
    /// </summary>
    public sealed class TradeLane
    {
        public TradeLane(TradeDirection direction, LanePick pick, IEnumerable<LaneStack> stacks)
        {
            Direction = direction;
            Pick = pick;
            Stacks = new List<LaneStack>(stacks ?? throw new ArgumentNullException(nameof(stacks)));
        }

        public TradeDirection Direction { get; }
        public LanePick Pick { get; }
        public IReadOnlyList<LaneStack> Stacks { get; }

        /// <summary>The most units the lane could ever move (ignoring price limits).</summary>
        public int Capacity
        {
            get
            {
                int sum = 0;
                foreach (var s in Stacks) sum += s.Available;
                return sum;
            }
        }

        public static TradeLane Empty(TradeDirection direction) =>
            new TradeLane(direction, LanePick.Cheapest, Array.Empty<LaneStack>());
    }

    /// <summary>The next unit a lane would move, at its marginal price.</summary>
    public readonly struct UnitQuote
    {
        public UnitQuote(int index, ItemStack stack, int price)
        {
            Index = index;
            Stack = stack;
            Price = price;
        }

        /// <summary>Index of the stack in the lane.</summary>
        public int Index { get; }
        public ItemStack Stack { get; }
        public int Price { get; }
    }

    /// <summary>A walk along one lane: how many units it has moved from each stack.</summary>
    public sealed class LaneCursor
    {
        private readonly int[] _moved;

        public LaneCursor(TradeLane lane)
            : this(lane, new int[lane.Stacks.Count], 0)
        {
        }

        private LaneCursor(TradeLane lane, int[] moved, int total)
        {
            Lane = lane;
            _moved = moved;
            Moved = total;
        }

        public TradeLane Lane { get; }

        /// <summary>Units moved so far.</summary>
        public int Moved { get; private set; }

        public int MovedFrom(int index) => _moved[index];

        public int Remaining(int index, MarketState market)
        {
            var laneStack = Lane.Stacks[index];
            int left = laneStack.Available - _moved[index];
            if (Lane.Direction == TradeDirection.Buy)
                left = Math.Min(left, market.StockLeft(laneStack.Stack));
            return left;
        }

        /// <summary>
        /// The unit the lane would move next: among stacks with units left whose marginal price passes
        /// their own limit (and is ≤ <paramref name="ceiling"/> when given — a purse or the market's gold),
        /// the one the pick rule prefers; ties → the earlier stack. Null when nothing qualifies.
        /// </summary>
        public UnitQuote? Peek(MarketState market, int? ceiling = null)
        {
            UnitQuote? best = null;
            for (int i = 0; i < Lane.Stacks.Count; i++)
            {
                if (Remaining(i, market) <= 0)
                    continue;
                var laneStack = Lane.Stacks[i];
                int price = market.Quote(laneStack.Stack, Lane.Direction);
                if (!laneStack.Accepts(Lane.Direction, price))
                    continue;
                if (ceiling != null && price > ceiling.Value)
                    continue;
                var quote = new UnitQuote(i, laneStack.Stack, price);
                if (Lane.Pick == LanePick.InOrder)
                    return quote;
                if (best == null
                    || (Lane.Pick == LanePick.Cheapest && price < best.Value.Price)
                    || (Lane.Pick == LanePick.MostExpensive && price > best.Value.Price))
                    best = quote;
            }
            return best;
        }

        /// <summary>Moves the quoted unit: counts it here and books it in the market.</summary>
        public void Take(UnitQuote quote, MarketState market)
        {
            _moved[quote.Index]++;
            Moved++;
            market.Record(quote.Stack, Lane.Direction, quote.Price);
        }

        public LaneCursor Clone() => new LaneCursor(Lane, (int[])_moved.Clone(), Moved);
    }
}
