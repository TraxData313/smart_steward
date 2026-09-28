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
            if (lootGroup == LootGroup.OtherGoods)
                return ItemKind.Goods; // round 4: a trade good that is not food
            return lootGroup != LootGroup.None ? ItemKind.Equipment : ItemKind.Other;
        }

        /// <summary>
        /// A modifier worth less than the plain item — the game's own test for a BAD modifier: <c>ItemModifier.PriceMultiplier
        /// &lt; 1</c> (<c>BattleCampaignBehavior.OnCollectLootItems</c>: the Metallurgy perk strips exactly those). For horses in
        /// v1.4.8 that is "Lame" (<c>lame_horse</c>, 0.1 — a horse badly hurt in battle) and "Old" (<c>companion_horse</c>, 0.2 —
        /// a wanderer's own horse); no horse modifier worth more is active (RESEARCH §22).
        /// </summary>
        public static bool IsBadModifier(double priceFactor) => priceFactor < 1.0;

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

        /// <summary>Carrying capacity of one healthy member: <c>TroopsFactor 2 × GetItemAverageWeight() 10</c>
        /// (<c>DefaultInventoryCapacityModel</c>, RESEARCH §19).</summary>
        public const double CapacityPerMember = 20;

        /// <summary>… of one mount on land (<c>SpareMountsFactor 2 × 10</c> — every <c>IsMount</c> animal, ridden or not).</summary>
        public const double CapacityPerMount = 20;

        /// <summary>… of one pack animal on land (<c>PackAnimalsFactor 10 × 10</c>).</summary>
        public const double CapacityPerPackAnimal = 100;

        /// <summary>The model's base and its floor (<c>Add(10)</c>, <c>LimitMin(10)</c>).</summary>
        public const double CapacityBase = 10;

        /// <summary>
        /// The per-unit rates of <c>DefaultInventoryCapacityModel.CalculateInventoryCapacity</c> (RESEARCH §19), perks
        /// included — an <c>ExplainedNumber</c> is base × (1 + Σ factors): on land a member gives 20 × (1 + Arenicos'
        /// Horses), a mount 20, a pack animal 100 × (1 + Beast Whisperer's secondary + Deeper Sacks + Arenicos' Mules), a
        /// healthy prisoner 20 with Forced Labor (not multiplied by Arenicos' Horses), and Caravan Master multiplies the
        /// whole land total; at sea only members (20) and Forced Labor's prisoners count — ships add a fixed cargo the deal
        /// never changes. <paramref name="forcedLaborNow"/> = the perk AND the party not at sea right now (the game checks
        /// the live state for that perk, whichever capacity it computes). The rounding of Arenicos' Horses
        /// (<c>Round(members × bonus)</c>) is left out: at most one member's worth.
        /// </summary>
        public static void SetCarryRates(CarryInfo carry, double troopsBonus, double packBonus, double caravanBonus,
            bool forcedLaborNow)
        {
            if (carry == null) throw new ArgumentNullException(nameof(carry));
            double caravan = 1 + caravanBonus;
            carry.LandPerMember = CapacityPerMember * (1 + troopsBonus) * caravan;
            carry.LandPerMount = CapacityPerMount * caravan;
            carry.LandPerPackAnimal = CapacityPerPackAnimal * (1 + packBonus) * caravan;
            carry.LandPerPrisoner = forcedLaborNow ? CapacityPerMember * caravan : 0;
            carry.SeaPerMember = CapacityPerMember;
            carry.SeaPerPrisoner = forcedLaborNow ? CapacityPerMember : 0;
        }
    }
}
