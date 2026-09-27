using System;
using System.Collections.Generic;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Pricing
{
    public enum TradeDirection
    {
        Buy,
        Sell,
    }

    /// <summary>
    /// The market as the plan has changed it so far: each category's in-store value delta (the town
    /// price walk), the units bought out of each market stack, and the market's purse. Every unit the
    /// plan moves goes through <see cref="Record"/>, so the next quote is the true marginal price.
    /// </summary>
    public sealed class MarketState
    {
        private readonly IPriceOracle _oracle;
        private readonly Dictionary<string, int> _categoryDelta;
        private readonly Dictionary<string, int> _boughtByKey;

        public MarketState(IPriceOracle oracle, int marketGold)
            : this(oracle, marketGold, new Dictionary<string, int>(StringComparer.Ordinal),
                new Dictionary<string, int>(StringComparer.Ordinal))
        {
        }

        private MarketState(IPriceOracle oracle, int marketGold, Dictionary<string, int> categoryDelta,
            Dictionary<string, int> boughtByKey)
        {
            _oracle = oracle ?? throw new ArgumentNullException(nameof(oracle));
            MarketGoldLeft = marketGold;
            _categoryDelta = categoryDelta;
            _boughtByKey = boughtByKey;
        }

        /// <summary>What the market can still pay. The walks never take a sale past it (the planner and the
        /// editor alike); only a caller recording units by hand could drive it negative.</summary>
        public int MarketGoldLeft { get; private set; }

        public int CategoryDelta(string categoryId) =>
            _categoryDelta.TryGetValue(categoryId, out var delta) ? delta : 0;

        /// <summary>The marginal price of the next unit of this stack.</summary>
        public int Quote(ItemStack stack, TradeDirection direction) =>
            QuoteAt(stack, direction, CategoryDelta(stack.CategoryId));

        /// <summary>The price of a unit at a given category delta (0 = the untouched market).</summary>
        public int QuoteAt(ItemStack stack, TradeDirection direction, int categoryDelta) =>
            Math.Max(0, _oracle.GetPrice(stack.Key, direction == TradeDirection.Sell, categoryDelta));

        /// <summary>Units of a MARKET stack the plan has not bought yet.</summary>
        public int StockLeft(ItemStack marketStack) =>
            marketStack.Count - (_boughtByKey.TryGetValue(marketStack.Key, out var bought) ? bought : 0);

        /// <summary>Books one unit moved at <paramref name="price"/>: the category walks, a buy takes stock,
        /// a sale takes the market's gold.</summary>
        public void Record(ItemStack stack, TradeDirection direction, int price)
        {
            int step = direction == TradeDirection.Buy ? -stack.StoreValueStep : stack.StoreValueStep;
            _categoryDelta[stack.CategoryId] = CategoryDelta(stack.CategoryId) + step;
            if (direction == TradeDirection.Buy)
                _boughtByKey[stack.Key] = (_boughtByKey.TryGetValue(stack.Key, out var b) ? b : 0) + 1;
            else
                MarketGoldLeft -= price;
        }

        /// <summary>An independent copy — for what-if simulations that must not touch the real plan.</summary>
        public MarketState Clone() =>
            new MarketState(_oracle, MarketGoldLeft,
                new Dictionary<string, int>(_categoryDelta, StringComparer.Ordinal),
                new Dictionary<string, int>(_boughtByKey, StringComparer.Ordinal));
    }
}
