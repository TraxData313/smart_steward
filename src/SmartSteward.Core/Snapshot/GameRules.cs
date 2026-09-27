using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartSteward.Core.Snapshot
{
    /// <summary>
    /// The small pure rules the Module applies while it reads the game (PLAN step 6) — kept here so they are
    /// tested and the game glue stays a thin reader. Every rule mirrors v1.4.8 code; the source is named on
    /// each member (docs/RESEARCH.md has the details).
    /// </summary>
    public static class GameRules
    {
        /// <summary>
        /// <see cref="ItemStack.Key"/> of a roster element: item id + "|" + modifier id. A roster holds one element
        /// per item + modifier (<c>ItemRoster.FindIndexOfElement</c> → <c>EquipmentElement.IsEqualTo</c> compares
        /// exactly those two), so the key is unique on each side and the same for the party's and the market's
        /// stack of one element.
        /// </summary>
        public static string StackKey(string itemId, string? modifierId) => itemId + "|" + (modifierId ?? "");

        /// <summary>The inventory-lock id of an element: item id + modifier id, NO separator
        /// (<c>CampaignUIHelper.GetItemLockStringID</c>, RESEARCH §6).</summary>
        public static string LockId(string itemId, string? modifierId) => itemId + (modifierId ?? "");

        /// <summary>
        /// The steward's kind of a roster element (DESIGN terms, RESEARCH §1 and §6). Quest items and items the
        /// game does not let change hands (<c>!IsTransferable</c>) are never touched; food is <c>IsFood</c>; an
        /// animal is a pack animal, a mount, or livestock (left alone); equipment is anything in a V1 loot group.
        /// </summary>
        public static ItemKind Classify(bool isFood, bool hasHorseComponent, bool isPackAnimal, bool isMount,
            LootGroup lootGroup, bool isQuestItem, bool isTransferable)
        {
            if (isQuestItem || !isTransferable)
                return ItemKind.Other;
            if (isFood)
                return ItemKind.Food;
            if (hasHorseComponent)
            {
                if (isPackAnimal) return ItemKind.PackAnimal;
                if (isMount) return ItemKind.Mount;
                return ItemKind.Other; // livestock — not food to the steward, never traded in V1
            }
            return lootGroup != LootGroup.None ? ItemKind.Equipment : ItemKind.Other;
        }

        /// <summary>
        /// The party screen's "can upgrade now" count for one stack and target, NOT capped by gold or by the
        /// animals held (those are what the steward fills) — <c>PartyCharacterVM.InitializeUpgrades</c>:
        /// ready when the target's level ≥ the troop's and the stack's pooled XP ≥ one upgrade's cost, then
        /// <c>clamp(floor(xp / cost), 0, number)</c>; zero when the party lacks the required perk (and, for
        /// bandits, when <c>CanPartyUpgradeTroopToTarget</c> says no — the button is disabled then).
        /// </summary>
        public static int UpgradeReadyCount(int targetLevel, int troopLevel, int stackXp, int xpCost, int stackCount,
            bool allowed)
        {
            if (!allowed || stackCount <= 0 || targetLevel < troopLevel || stackXp < xpCost)
                return 0;
            if (xpCost <= 0)
                return stackCount;
            return Math.Max(0, Math.Min(stackCount, stackXp / xpCost));
        }

        /// <summary>The mean of the category's price factor over the other towns (the inventory's own average,
        /// <c>InventoryLogic.InitializeCategoryAverages</c>); 1 when there are none. Vanilla divides by the town
        /// count − 1 even when the excluded settlement is a castle (a castle-bound village) — we divide by the
        /// towns actually summed.</summary>
        public static float MeanFactor(IEnumerable<float> factors)
        {
            float sum = 0f;
            int n = 0;
            foreach (var f in factors ?? Enumerable.Empty<float>())
            {
                sum += f;
                n++;
            }
            return n == 0 ? 1f : sum / n;
        }

        /// <summary>
        /// DESIGN §4.2's average BUY price: one unit in an average town, penalty included — the price model's
        /// buy side at the average factor (<c>DefaultTradeItemPriceFactorModel.GetPrice</c>:
        /// <c>max(1, ceil(value × factor × (1 + penalty)))</c>, float maths like the game).
        /// </summary>
        public static int AverageBuyPrice(int itemValue, float averageFactor, float buyPenalty)
        {
            float priceFactor = averageFactor * (1f + buyPenalty);
            float f = itemValue * priceFactor;
            return Math.Max(1, (int)Math.Ceiling(f));
        }

        /// <summary>The average SELL price: <c>max(1, floor(value × factor / (1 + penalty)))</c> — an animal
        /// fetches about half, equipment about a third (not used: equipment gets no averages).</summary>
        public static int AverageSellPrice(int itemValue, float averageFactor, float sellPenalty)
        {
            float priceFactor = averageFactor * 1f / (1f + sellPenalty);
            float f = itemValue * priceFactor;
            return Math.Max(1, (int)Math.Floor(f));
        }

        /// <summary>The Military Coronae policy's boost on donation influence
        /// (<c>GainKingdomInfluenceAction.ApplyInternal</c>: × 1.2f).</summary>
        public const float MilitaryCoronaeFactor = 1.2f;

        /// <summary>Influence one donated prisoner brings: the model's number
        /// (<c>PrisonerDonationModel.CalculateInfluenceGainAfterPrisonerDonation</c> = 0.2 × ransom^0.4), × 1.2
        /// under Military Coronae; 0 when the player is in no kingdom (the action gives nothing then).</summary>
        public static double DonationInfluence(float modelValue, bool inKingdom, bool militaryCoronae)
        {
            if (!inKingdom || modelValue <= 0f)
                return 0;
            float value = militaryCoronae ? modelValue * MilitaryCoronaeFactor : modelValue;
            return Math.Round(value, 4);
        }

        /// <summary>
        /// Donating prisoners is offered (DESIGN §2.5, <c>game_menu_castle_leave_prisoners_on_condition</c>): a
        /// town of the player's own map faction that his clan does NOT own (own fiefs get "Manage prisoners" and
        /// no influence), whose dungeon he may enter. A mercenary qualifies — his map faction is the kingdom.
        /// </summary>
        public static bool DonateAllowed(bool isTown, bool sameMapFaction, bool ownClanFief, bool dungeonAccess) =>
            isTown && sameMapFaction && !ownClanFief && dungeonAccess;

        /// <summary>A short tag of a wanderer's best skills, e.g. "Scouting 120, Riding 95": the highest values
        /// first (ties by name), at most <paramref name="count"/>, zeros left out; null when nothing is left.</summary>
        public static string? SkillTag(IEnumerable<KeyValuePair<string, int>> skills, int count = 2)
        {
            var best = (skills ?? Enumerable.Empty<KeyValuePair<string, int>>())
                .Where(s => s.Value > 0 && !string.IsNullOrEmpty(s.Key))
                .OrderByDescending(s => s.Value)
                .ThenBy(s => s.Key, StringComparer.Ordinal)
                .Take(Math.Max(0, count))
                .Select(s => s.Key + " " + s.Value)
                .ToList();
            return best.Count == 0 ? null : string.Join(", ", best);
        }

        /// <summary>Wounded men to move with <paramref name="count"/> of a stack: the wounded go first (never
        /// more than the stack has), so the healthy ones are only moved once no wounded are left.</summary>
        public static int WoundedToMove(int count, int woundedInStack) =>
            Math.Max(0, Math.Min(Math.Max(0, count), Math.Max(0, woundedInStack)));
    }
}
