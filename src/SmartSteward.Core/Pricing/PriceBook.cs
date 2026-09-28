using System;
using System.Collections.Generic;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Pricing
{
    /// <summary>The price book's groups (DESIGN §1.3) — V1 has food and the four horse sub-headers only.
    /// Armour and weapons are NEVER in the price book. (LATER: Others, §1.3.1.)</summary>
    public enum PriceBookGroup
    {
        Food,
        PackAnimals,
        Mounts,
        WarMounts,

        /// <summary>Noble horses — sell only (step 17, Anton 2026.09.28: never bought, sold unless locked): the Prices tab
        /// shows no buy column, and the sell placeholder is always filled so they never go for a pittance.</summary>
        NobleHorses,
    }

    /// <summary>One price-book row resolved against the settings: ticks, bases (typed or placeholder) and
    /// the final limits the planners use.</summary>
    public sealed class PriceBookPrices
    {
        internal PriceBookPrices(string itemId, PriceBookGroup group)
        {
            ItemId = itemId;
            Group = group;
        }

        public string ItemId { get; }
        public PriceBookGroup Group { get; }
        public bool BuyTicked { get; internal set; }
        public bool SellTicked { get; internal set; }

        /// <summary>The base of the max buy price: the typed one, else the average when the group
        /// auto-fills; null = empty.</summary>
        public int? BuyBase { get; internal set; }
        public bool BuyBaseIsPlaceholder { get; internal set; }
        public int? SellBase { get; internal set; }
        public bool SellBaseIsPlaceholder { get; internal set; }

        /// <summary>base × BuyPriceMultiplier, rounded; null when the base is empty.</summary>
        public int? FinalMaxBuy { get; internal set; }

        /// <summary>base × SellPriceMultiplier, rounded; null when the base is empty (= any price).</summary>
        public int? FinalMinSell { get; internal set; }
    }

    /// <summary>Price-book rules of DESIGN §1.3.</summary>
    public static class PriceBook
    {
        /// <summary>
        /// The mount categories of the price book's sub-headers: <c>war_horse</c> under "War mounts" (<c>AutoFillWarMountPrices</c>,
        /// off by default: no trader's cheat sheet for the dear ones), <c>noble_horse</c> under "Noble horses — sell only", and
        /// every other one — vanilla's plain riding horses and camels (<c>horse</c>) and any a mod adds — under "Mounts"
        /// (<c>AutoFillPackAndMountPrices</c>): to the steward they are riding horses. [decided: Claude, 2026.09.27 — step 4;
        /// step 17: noble horses left the war mounts (Anton 2026.09.28), a mod's category joined the riding horses — nothing
        /// counts upgrades any more.]
        /// </summary>
        public static PriceBookGroup? GroupOf(ItemKind kind, string categoryId)
        {
            switch (kind)
            {
                case ItemKind.Food:
                    return PriceBookGroup.Food;
                case ItemKind.PackAnimal:
                    return PriceBookGroup.PackAnimals;
                case ItemKind.Mount:
                    if (string.Equals(categoryId, Planning.MountGoal.WarHorse, StringComparison.Ordinal))
                        return PriceBookGroup.WarMounts;
                    if (string.Equals(categoryId, Planning.MountGoal.NobleHorse, StringComparison.Ordinal))
                        return PriceBookGroup.NobleHorses;
                    return PriceBookGroup.Mounts;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The min sell price of one STACK: the item's final min sell scaled by the stack's modifier
        /// (<see cref="ItemStack.ModifierPriceFactor"/>) — the game prices a lame horse at a tenth of a plain one
        /// (<c>EquipmentElement.ItemValue</c>), so it is judged against a lame horse's worth, not a healthy one's; null stays
        /// null (any price). [decided: Claude, 2026.09.28 — step 17]
        /// </summary>
        public static int? MinSellOf(int? finalMinSell, double modifierPriceFactor)
        {
            if (finalMinSell == null || Math.Abs(modifierPriceFactor - 1.0) < 1e-9)
                return finalMinSell;
            return Final(finalMinSell.Value, Math.Max(0, modifierPriceFactor));
        }

        /// <summary>Resolves one item's row: the player's overrides first, then the defaults (ticks on,
        /// placeholder = the average price when the group auto-fills).</summary>
        public static PriceBookPrices Resolve(string itemId, PriceBookGroup group, StewardSettings settings,
            AveragePrices? averages)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            PriceBookEntry? entry = null;
            settings.PriceBook?.TryGetValue(itemId, out entry);
            bool autoFill = AutoFills(group, settings);

            var prices = new PriceBookPrices(itemId, group)
            {
                BuyTicked = entry?.Buy ?? true,
                SellTicked = entry?.Sell ?? true,
            };

            if (entry?.BuyBase != null)
                prices.BuyBase = entry.BuyBase;
            else if (autoFill && averages != null)
            {
                prices.BuyBase = averages.Buy;
                prices.BuyBaseIsPlaceholder = true;
            }

            if (entry?.SellBase != null)
                prices.SellBase = entry.SellBase;
            else if (autoFill && averages != null)
            {
                prices.SellBase = averages.Sell;
                prices.SellBaseIsPlaceholder = true;
            }

            if (prices.BuyBase != null)
                prices.FinalMaxBuy = Final(prices.BuyBase.Value, settings.BuyPriceMultiplier);
            if (prices.SellBase != null)
                prices.FinalMinSell = Final(prices.SellBase.Value, settings.SellPriceMultiplier);
            return prices;
        }

        public static bool AutoFills(PriceBookGroup group, StewardSettings settings)
        {
            switch (group)
            {
                case PriceBookGroup.Food: return settings.AutoFillFoodPrices;
                case PriceBookGroup.PackAnimals:
                case PriceBookGroup.Mounts: return settings.AutoFillPackAndMountPrices;
                case PriceBookGroup.WarMounts: return settings.AutoFillWarMountPrices;
                // Only ever sold: the average SELL price is no trader's cheat sheet, and without it a noble horse would go
                // at any price (DESIGN §1.3, step 17).
                case PriceBookGroup.NobleHorses: return true;
                default: return false;
            }
        }

        /// <summary>
        /// base × multiplier rounded to the nearest whole denar, halves away from zero — the number the
        /// Prices tab shows (<c>[11] × 1.2 → 13</c>, <c>[7] × 0.8 → 6</c>). The product is first rounded to
        /// 6 decimals so binary slop (10 × 0.8f = 8.0000001) never moves it. Never negative.
        /// </summary>
        public static int Final(int basePrice, double multiplier)
        {
            double product = Math.Round(basePrice * multiplier, 6);
            return Math.Max(0, (int)Math.Round(product, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// The buy limit of an animal: the price-book max AND the role cap (0 = no cap), whichever is lower.
        /// Both empty → null: not bought at all ("an empty base is not bought unless a role cap covers it").
        /// </summary>
        public static int? AnimalBuyLimit(int? finalMaxBuy, int roleCap)
        {
            int? cap = roleCap > 0 ? roleCap : (int?)null;
            if (finalMaxBuy == null) return cap;
            if (cap == null) return finalMaxBuy;
            return Math.Min(finalMaxBuy.Value, cap.Value);
        }
    }
}
