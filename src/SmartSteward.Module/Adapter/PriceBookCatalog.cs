using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace SmartSteward.Adapter
{
    /// <summary>One item of the price book as the Prices tab lists it.</summary>
    internal sealed class PriceBookItem
    {
        public PriceBookItem(string itemId, string name, PriceBookGroup group, AveragePrices? averages)
        {
            ItemId = itemId;
            Name = name;
            Group = group;
            Averages = averages;
        }

        public string ItemId { get; }
        public string Name { get; }
        public PriceBookGroup Group { get; }

        /// <summary>The placeholders (DESIGN §4.2) at this settlement; null when the game gives no category.</summary>
        public AveragePrices? Averages { get; }
    }

    /// <summary>
    /// Every item the V1 price book covers (DESIGN §1.3): the game's food and its pack animals and riding animals —
    /// livestock, quest and non-transferable items left out, like the steward's own classification
    /// (<see cref="GameRules.Classify"/>) — and only merchandise (an item no market ever sells needs no price),
    /// plus whatever the party or this market holds. Placeholders come from the snapshot builder's own formula.
    /// </summary>
    internal static class PriceBookCatalog
    {
        public static List<PriceBookItem> Build(GameVisit visit)
        {
            var result = new List<PriceBookItem>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var held = new HashSet<string>(visit.Snapshot.Inventory.Concat(visit.Snapshot.Market).Select(s => s.ItemId),
                StringComparer.Ordinal);
            var main = MobileParty.MainParty;
            var factors = new Dictionary<ItemCategory, float>();
            foreach (var item in Items.All)
            {
                try
                {
                    if (item == null || string.IsNullOrEmpty(item.StringId) || seen.Contains(item.StringId))
                        continue;
                    if (item.NotMerchandise && !held.Contains(item.StringId))
                        continue;
                    var horse = item.HorseComponent;
                    var kind = GameRules.Classify(item.IsFood, horse != null, horse != null && horse.IsPackAnimal,
                        horse != null && horse.IsMount, LootGroup.None, false, item.IsTransferable);
                    var group = PriceBook.GroupOf(kind, item.ItemCategory?.StringId ?? "");
                    if (group == null)
                        continue;
                    seen.Add(item.StringId);
                    AveragePrices? averages = visit.Snapshot.AveragePrices.TryGetValue(item.StringId, out var known)
                        ? known
                        : main == null ? null : SnapshotBuilder.AverageOf(item, visit.Settlement, main, factors);
                    result.Add(new PriceBookItem(item.StringId, item.Name?.ToString() ?? item.StringId, group.Value, averages));
                }
                catch (Exception ex)
                {
                    ModLog.Error("prices", "reading item " + (item?.StringId ?? "?"), ex);
                }
            }
            // Cheapest first within each group (round 4, Anton 2026.09.28) - the Core's order.
            return PriceBookOrder.Sort(result, i => i.Group, i => i.Averages, i => i.Name, i => i.ItemId);
        }
    }
}
