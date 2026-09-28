using System;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.UI
{
    /// <summary>
    /// The window's words for Core's plain values — row types, roles, loot groups, sections, the reasons a button is
    /// greyed out, the footer's warnings — each a TextObject with its own string id (DESIGN §9: English at release,
    /// translatable later).
    /// </summary>
    internal static class UiLabels
    {
        public static string Type(RowType type)
        {
            switch (type)
            {
                case RowType.Tavern: return UiText.S("ss_ui_type_tavern", "Tavern");
                case RowType.Troop: return UiText.S("ss_ui_type_troop", "Troop");
                case RowType.Food: return UiText.S("ss_ui_type_food", "Food");
                case RowType.Pack: return UiText.S("ss_ui_type_pack", "Pack");
                case RowType.Mount: return UiText.S("ss_ui_type_mount", "Mount");
                case RowType.WarMount: return UiText.S("ss_ui_type_war_mount", "War mount");
                case RowType.Loot: return UiText.S("ss_ui_type_loot", "Loot");
                case RowType.Prisoner: return UiText.S("ss_ui_type_prisoner", "Prisoner");
                default: return type.ToString();
            }
        }

        public static string Section(PlanSectionKind kind)
        {
            switch (kind)
            {
                case PlanSectionKind.Tavern: return UiText.S("ss_ui_sec_tavern", "Tavern");
                case PlanSectionKind.Recruits: return UiText.S("ss_ui_sec_recruits", "Recruits on offer");
                case PlanSectionKind.Troops: return UiText.S("ss_ui_sec_troops", "Your troops");
                case PlanSectionKind.Food: return UiText.S("ss_ui_sec_food", "Food");
                case PlanSectionKind.Mounts: return UiText.S("ss_ui_sec_mounts", "Mounts");
                case PlanSectionKind.Other: return UiText.S("ss_ui_sec_other", "Other");
                case PlanSectionKind.Prisoners: return UiText.S("ss_ui_sec_prisoners", "Prisoners");
                default: return kind.ToString();
            }
        }

        /// <summary>A row's Item column: its name, or the role / loot-group label.</summary>
        public static string RowName(PlanRow row)
        {
            if (!string.IsNullOrEmpty(row.Name))
                return row.Name;
            if (row.Role != null)
            {
                switch (row.Role.Value)
                {
                    case MountRole.Pack: return UiText.S("ss_ui_role_pack", "Pack animals");
                    case MountRole.Riding: return UiText.S("ss_ui_role_riding", "Riding mounts");
                    case MountRole.War: return UiText.S("ss_ui_role_war", "War horses");
                    case MountRole.Noble: return UiText.S("ss_ui_role_noble", "Noble horses");
                    case MountRole.Lame: return UiText.S("ss_ui_role_lame", "Lame horses");
                }
            }
            if (row.Type == RowType.Loot)
            {
                switch (row.LootGroup)
                {
                    case LootGroup.Armour: return UiText.S("ss_ui_loot_armour", "Armour");
                    case LootGroup.MeleeWeapons: return UiText.S("ss_ui_loot_melee", "Melee weapons");
                    case LootGroup.Ranged: return UiText.S("ss_ui_loot_ranged", "Ranged");
                    case LootGroup.Shields: return UiText.S("ss_ui_loot_shields", "Shields");
                    case LootGroup.OtherGoods: return UiText.S("ss_ui_loot_other_goods", "Other goods");
                }
            }
            return row.Id;
        }

        /// <summary>The tooltip of a greyed [+] / [−] (DESIGN §1.1: "the button greys with the reason").</summary>
        public static string Block(EditBlock block)
        {
            switch (block)
            {
                case EditBlock.None: return "";
                case EditBlock.SellOnly:
                    return UiText.S("ss_ui_block_sell_only", "Only ever sold, ransomed or donated - never bought.");
                case EditBlock.BuyOnly: return UiText.S("ss_ui_block_buy_only", "Only ever hired.");
                case EditBlock.NoneEligible:
                    return UiText.S("ss_ui_block_none_eligible",
                        "Nothing on offer the steward may buy: none in the market, unticked in the Prices tab, or dearer than its max buy price.");
                case EditBlock.AllOnOffer:
                    return UiText.S("ss_ui_block_all_on_offer", "Everything on offer is already in the plan.");
                case EditBlock.NothingToSell:
                    return UiText.S("ss_ui_block_nothing_to_sell",
                        "Nothing to sell: none held, all locked, or unticked for selling in the Prices tab.");
                case EditBlock.AllSold:
                    return UiText.S("ss_ui_block_all_sold", "Everything that may go is already in the plan. Locked ones never are.");
                case EditBlock.PriceLimit:
                    return UiText.S("ss_ui_block_price_limit",
                        "The next one costs more than its max buy price - a town's prices climb as you buy.");
                case EditBlock.BelowMinSellPrice:
                    return UiText.S("ss_ui_block_min_sell",
                        "The next one would fetch less than its min sell price - a town's prices fall as you sell.");
                case EditBlock.MarketOutOfGold:
                    return UiText.S("ss_ui_block_market_gold", "The market has no gold left to pay for more.");
                case EditBlock.NotEnoughGold: return UiText.S("ss_ui_block_gold", "You do not have the gold for it.");
                case EditBlock.NeededByAnotherRow:
                    return UiText.S("ss_ui_block_other_row", "What is left is already planned for another row.");
                case EditBlock.CompanionLimit:
                    return UiText.S("ss_ui_block_companions", "Your clan's companion limit is reached.");
                case EditBlock.DungeonFull: return UiText.S("ss_ui_block_dungeon_full", "The dungeon has no more room.");
                case EditBlock.NotOnOfferHere:
                    return UiText.S("ss_ui_block_not_on_offer", "No notable here offers you this troop.");
                case EditBlock.NoneToDismiss:
                    return UiText.S("ss_ui_block_none_to_dismiss", "None of these in your party to dismiss.");
                case EditBlock.AllDismissed:
                    return UiText.S("ss_ui_block_all_dismissed", "All of them are already dismissed in the plan.");
                default: return block.ToString();
            }
        }

        public static string Warning(PlanWarning warning, int minGoldAfterDeal, int minGoldForHorses)
        {
            switch (warning)
            {
                case PlanWarning.CannotAfford:
                    return UiText.S("ss_ui_warn_cannot_afford", "You cannot afford this deal - take something back.");
                case PlanWarning.BelowMinGoldAfterDeal:
                    return UiText.S1("ss_ui_warn_min_gold", "Below the gold you always keep ({GOLD}).", "GOLD",
                        UiFormat.Money(minGoldAfterDeal));
                case PlanWarning.BelowMinGoldForHorses:
                    return UiText.S1("ss_ui_warn_min_gold_horses", "Below the gold you keep before buying animals ({GOLD}).",
                        "GOLD", UiFormat.Money(minGoldForHorses));
                case PlanWarning.ExceedsMarketGold:
                    return UiText.S("ss_ui_warn_market_gold", "The market cannot pay for all the sales.");
                default:
                    return warning.ToString();
            }
        }

        /// <summary>Safe string for a name that might be missing.</summary>
        public static string OrId(string? name, string id) => string.IsNullOrEmpty(name) ? id : name!;

        public static bool Is(string? a, string b) => string.Equals(a, b, StringComparison.Ordinal);
    }
}
