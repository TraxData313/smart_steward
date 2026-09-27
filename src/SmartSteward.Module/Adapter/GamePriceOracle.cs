using System;
using System.Collections.Generic;
using System.Globalization;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace SmartSteward.Adapter
{
    /// <summary>
    /// Core's <see cref="IPriceOracle"/> with the game's own price model (DESIGN §4.1, RESEARCH §8): in a TOWN the
    /// model itself — <c>TradeItemPriceFactorModel.GetPrice(element, MainParty, settlement.Party, isSelling,
    /// inStoreValue + delta, supply, demand)</c> — at the category's in-store value moved by the plan's walk; in a
    /// VILLAGE the flat price the trade screen shows (<c>Village.MarketData.GetPrice</c>, which reads the
    /// trade-bound town's figures that a village trade never moves), whatever the delta.
    /// </summary>
    internal sealed class GamePriceOracle : IPriceOracle
    {
        private readonly Settlement _settlement;
        private readonly IReadOnlyDictionary<string, EquipmentElement> _elements;

        public GamePriceOracle(Settlement settlement, IReadOnlyDictionary<string, EquipmentElement> elements)
        {
            _settlement = settlement;
            _elements = elements;
        }

        public int GetPrice(string stackKey, bool isSelling, int categoryStoreValueDelta)
        {
            // An unknown key would be a bug in the snapshot; a made-up price could buy for free — fail loud.
            if (!_elements.TryGetValue(stackKey, out var element))
                throw new InvalidOperationException("No game element for stack key '" + stackKey + "'.");
            var main = MobileParty.MainParty;
            if (_settlement.IsVillage)
                return _settlement.Village.MarketData.GetPrice(element, main, isSelling, _settlement.Party);
            var data = _settlement.Town.MarketData.GetCategoryData(element.Item.ItemCategory);
            return Campaign.Current.Models.TradeItemPriceFactorModel.GetPrice(element, main, _settlement.Party, isSelling,
                data.InStoreValue + categoryStoreValueDelta, data.Supply, data.Demand);
        }

        /// <summary>
        /// The self-check (PLAN step 6): the oracle's price for the first unit must equal the price the game's own
        /// trade screen shows for it — <c>InventoryLogic.GetItemPrice</c> → <c>MarketData.GetPrice(element,
        /// MainParty, isSelling, settlement.Party)</c>. Checked for every stack the steward may trade: the market's
        /// on the buying side, the party's on the selling side. Returns one log line.
        /// </summary>
        public string SelfCheck(StewardSnapshot snapshot)
        {
            var inv = CultureInfo.InvariantCulture;
            IMarketData market = _settlement.IsVillage ? (IMarketData)_settlement.Village.MarketData : _settlement.Town.MarketData;
            var main = MobileParty.MainParty;
            int checkedCount = 0, mismatches = 0;
            var examples = new List<string>();
            void Check(ItemStack stack, bool isSelling)
            {
                if (stack.Kind == ItemKind.Other || !_elements.TryGetValue(stack.Key, out var element))
                    return;
                checkedCount++;
                int ours = GetPrice(stack.Key, isSelling, 0);
                int game = market.GetPrice(element, main, isSelling, _settlement.Party);
                if (ours == game)
                    return;
                mismatches++;
                if (examples.Count < 5)
                    examples.Add(stack.Key + (isSelling ? " sell " : " buy ") + ours.ToString(inv) + " vs game " + game.ToString(inv));
            }
            if (snapshot.CanTrade)
            {
                foreach (var stack in snapshot.Market)
                    Check(stack, false);
                foreach (var stack in snapshot.Inventory)
                    Check(stack, true);
            }
            return "price self-check: " + checkedCount.ToString(inv) + " first-unit prices, "
                   + (mismatches == 0 ? "all equal to the trade screen's" : mismatches.ToString(inv) + " DIFFER: " + string.Join("; ", examples));
        }
    }
}
