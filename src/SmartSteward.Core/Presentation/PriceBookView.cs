using System;
using System.Collections.Generic;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Presentation
{
    /// <summary>
    /// The Prices tab's edits on the price book (DESIGN §1.3, §8) — pure, run inside
    /// <see cref="SettingsService.Update"/>. Only the player's changes are stored: ticking an item back on, or
    /// clearing a base, removes the override, and an entry with nothing left is dropped.
    /// </summary>
    public static class PriceBookEditor
    {
        /// <summary>Buy tick: off is stored (<c>"Buy": false</c>); on is the default, so nothing is stored.</summary>
        public static void SetBuy(StewardSettings settings, string itemId, bool ticked) =>
            Edit(settings, itemId, e => e.Buy = ticked ? (bool?)null : false);

        public static void SetSell(StewardSettings settings, string itemId, bool ticked) =>
            Edit(settings, itemId, e => e.Sell = ticked ? (bool?)null : false);

        /// <summary>The typed base of the max buy price; null clears it (the placeholder shows again).</summary>
        public static void SetBuyBase(StewardSettings settings, string itemId, int? value) =>
            Edit(settings, itemId, e => e.BuyBase = Clamp(value));

        public static void SetSellBase(StewardSettings settings, string itemId, int? value) =>
            Edit(settings, itemId, e => e.SellBase = Clamp(value));

        /// <summary>⟲: both typed bases back to the placeholder; the ticks stay as they are.</summary>
        public static void ClearBases(StewardSettings settings, string itemId) =>
            Edit(settings, itemId, e =>
            {
                e.BuyBase = null;
                e.SellBase = null;
            });

        /// <summary>The player's entry for an item, or null when he changed nothing.</summary>
        public static PriceBookEntry? EntryOf(StewardSettings settings, string itemId)
        {
            if (settings?.PriceBook == null || string.IsNullOrEmpty(itemId)) return null;
            return settings.PriceBook.TryGetValue(itemId, out var entry) && entry != null && !entry.IsEmpty ? entry : null;
        }

        private static int? Clamp(int? value) =>
            value == null ? (int?)null : Math.Max(0, Math.Min(PriceBookSetting.MaxBase, value.Value));

        private static void Edit(StewardSettings settings, string itemId, Action<PriceBookEntry> edit)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrEmpty(itemId)) return;
            var book = settings.PriceBook ??= new Dictionary<string, PriceBookEntry>(StringComparer.Ordinal);
            if (!book.TryGetValue(itemId, out var entry) || entry == null)
                entry = new PriceBookEntry();
            edit(entry);
            if (entry.IsEmpty)
                book.Remove(itemId);
            else
                book[itemId] = entry;
        }
    }

    /// <summary>
    /// One price-book row as the Prices tab shows it (DESIGN §1.3): <c>☑ [ 11 ] × 1.2 » 13 | ☑ [ 7 ] × 0.8 » 6</c>. The
    /// box holds only what the player typed; an empty box shows the grey placeholder (the average price, when its
    /// group auto-fills); the final is base × multiplier, "—" when there is no base.
    /// </summary>
    public sealed class PriceRowView
    {
        private PriceRowView()
        {
        }

        public bool BuyTicked { get; private set; }
        public bool SellTicked { get; private set; }

        /// <summary>The typed base, or "" (then the placeholder shows).</summary>
        public string BuyBaseText { get; private set; } = "";
        public string SellBaseText { get; private set; } = "";

        /// <summary>The average price shown grey in an empty box; "" when the group does not auto-fill.</summary>
        public string BuyPlaceholder { get; private set; } = "";
        public string SellPlaceholder { get; private set; } = "";

        /// <summary><c>× 1.2</c>.</summary>
        public string BuyMultiplier { get; private set; } = "";
        public string SellMultiplier { get; private set; } = "";

        /// <summary>The limit the steward uses: <c>13</c>, or "—" (no base: buy — only a role cap buys an animal;
        /// sell — any price).</summary>
        public string BuyFinal { get; private set; } = "";
        public string SellFinal { get; private set; } = "";

        /// <summary>A base was typed — ⟲ has something to clear.</summary>
        public bool HasTypedBase { get; private set; }

        public static PriceRowView Of(string itemId, PriceBookGroup group, StewardSettings settings, AveragePrices? averages)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            var prices = PriceBook.Resolve(itemId, group, settings, averages);
            var entry = PriceBookEditor.EntryOf(settings, itemId);
            return new PriceRowView
            {
                BuyTicked = prices.BuyTicked,
                SellTicked = prices.SellTicked,
                BuyBaseText = entry?.BuyBase == null ? "" : entry.BuyBase.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                SellBaseText = entry?.SellBase == null ? "" : entry.SellBase.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                BuyPlaceholder = PriceBook.AutoFills(group, settings) && averages != null ? UiFormat.Money(averages.Buy) : "",
                SellPlaceholder = PriceBook.AutoFills(group, settings) && averages != null ? UiFormat.Money(averages.Sell) : "",
                BuyMultiplier = UiFormat.Times + " " + UiFormat.Decimal(settings.BuyPriceMultiplier),
                SellMultiplier = UiFormat.Times + " " + UiFormat.Decimal(settings.SellPriceMultiplier),
                BuyFinal = prices.FinalMaxBuy == null ? UiFormat.None : UiFormat.Money(prices.FinalMaxBuy.Value),
                SellFinal = prices.FinalMinSell == null ? UiFormat.None : UiFormat.Money(prices.FinalMinSell.Value),
                HasTypedBase = entry?.BuyBase != null || entry?.SellBase != null,
            };
        }

        /// <summary>What a typed base means: a (possibly empty) whole number → the value to store; false = not a
        /// number, nothing is stored.</summary>
        public static bool TryReadBase(string? text, out int? value) => UiFormat.TryParseBase(text, out value);
    }
}
