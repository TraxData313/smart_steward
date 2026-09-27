using System;
using System.Collections.Generic;

namespace SmartSteward.Core.Pricing
{
    /// <summary>
    /// Remembers every price it has been asked for. The plan editor re-walks the whole plan after each click
    /// (and once per row for the buttons' blocked reasons); the same few (stack, side, delta) quotes come up
    /// again and again, and the game's price model is not free. Valid because a plan's prices are a snapshot:
    /// nothing trades while the window is open.
    /// </summary>
    internal sealed class CachingPriceOracle : IPriceOracle
    {
        private readonly IPriceOracle _inner;
        private readonly Dictionary<(string, bool, int), int> _prices = new Dictionary<(string, bool, int), int>();

        public CachingPriceOracle(IPriceOracle inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public int GetPrice(string stackKey, bool isSelling, int categoryStoreValueDelta)
        {
            var key = (stackKey, isSelling, categoryStoreValueDelta);
            if (!_prices.TryGetValue(key, out var price))
            {
                price = _inner.GetPrice(stackKey, isSelling, categoryStoreValueDelta);
                _prices[key] = price;
            }
            return price;
        }
    }
}
