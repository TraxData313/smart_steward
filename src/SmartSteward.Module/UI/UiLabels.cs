using System;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.UI
{
    /// <summary>
    /// The window's words for Core's plain values — the spreadsheet's words and section titles (step 21), the reasons a button
    /// is greyed out, the footer's warnings — each a TextObject with its own string id (DESIGN §9: English at release,
    /// translatable later).
    /// </summary>
    internal static class UiLabels
    {
        /// <summary>A section's title (the spreadsheet, step 21).</summary>
        public static string SheetSection(SheetGroup group)
        {
            switch (group)
            {
                case SheetGroup.Troops: return UiText.S("ss_ui_sec_troops_group", "Troops");
                case SheetGroup.Food: return UiText.S("ss_ui_sec_food", "Food");
                case SheetGroup.Horses: return UiText.S("ss_ui_sec_horses", "Horses");
                case SheetGroup.Prisoners: return UiText.S("ss_ui_sec_prisoners", "Prisoners");
                default: return UiText.S("ss_ui_sec_other", "Other");
            }
        }

        /// <summary>The spreadsheet's words (Core <see cref="SmartSteward.Core.Presentation.SheetWords"/>) from TextObjects, so the
        /// strings file gathers them and a translation replaces them (the step-20 NOTICED line).</summary>
        public static SheetWords SheetText() => new SheetWords
        {
            MenAfterNote = UiText.S("ss_ui_sheet_men_after", "men after the deal / party limit"),
            Kinds = UiText.S("ss_ui_sheet_kinds", "kinds"),
            Avg = UiText.S("ss_ui_sheet_avg", "avg"),
            PerKind = UiText.S("ss_ui_sheet_per_kind", "per kind"),
            Min = UiText.S("ss_ui_sheet_min", "min"),
            Days = UiText.S("ss_ui_sheet_days", "days"),
            About = UiText.S("ss_ui_sheet_about", "~"),
            BeforeHerd = UiText.S("ss_ui_sheet_before_herd", "before the herd slows you"),
            OnFoot = UiText.S("ss_ui_sheet_on_foot", "on foot"),
            HorsesToKeep = UiText.S("ss_ui_sheet_horses_to_keep", "horses to keep"),
            StartsAt = UiText.S("ss_ui_sheet_starts_at", "starts at"),
            Denari = UiText.S("ss_ui_sheet_denari", "denari"),
            PackAnimalsStart = UiText.S("ss_ui_sheet_pack_start", "pack animals start at"),
            RidingHorsesStart = UiText.S("ss_ui_sheet_riding_start", "riding horses start at"),
            WarHorsesStart = UiText.S("ss_ui_sheet_war_start", "war horses start at"),
            Lord = UiText.S("ss_ui_sheet_lord", "lord"),
            Lords = UiText.S("ss_ui_sheet_lords", "lords"),
            TierPrefix = UiText.S("ss_ui_tier_prefix", "T"),
            Sold = UiText.S("ss_ui_sheet_sold", "sold"),
            Pieces = UiText.S("ss_ui_sheet_pieces", "pieces"),
            Goods = UiText.S("ss_ui_sheet_goods", "goods"),
            NothingSold = UiText.S("ss_ui_sheet_nothing_sold", "nothing sold"),
            CheapestFirst = UiText.S("ss_ui_sheet_cheapest_first", "cheapest first"),
            LowestPerKgFirst = UiText.S("ss_ui_sheet_lowest_per_kg", "lowest price per kg first"),
            DearestFirst = UiText.S("ss_ui_sheet_dearest_first", "dearest first"),
            OnOffer = UiText.S("ss_ui_sheet_on_offer", "on offer"),
            BestTierFirst = UiText.S("ss_ui_sheet_best_tier_first", "[+] takes the best tier first"),
            Recruiting = UiText.S("ss_ui_sheet_recruiting", "recruiting"),
            LowestTierFirst = UiText.S("ss_ui_sheet_lowest_tier_first", "[–] drops the lowest tier first"),
            Dropping = UiText.S("ss_ui_sheet_dropping", "dropping"),
            Types = UiText.S("ss_ui_sheet_types", "types"),
            Ransomed = UiText.S("ss_ui_sheet_ransomed", "ransomed"),
            ToDungeon = UiText.S("ss_ui_sheet_to_dungeon", "to the dungeon"),
            Full = UiText.S("ss_ui_sheet_full", "full"),
            Kept = UiText.S("ss_ui_sheet_kept", "kept"),
            Party = UiText.S("ss_ui_sheet_party", "party"),
            Prisoners = UiText.S("ss_ui_sheet_prisoners", "prisoners"),
            Influence = UiText.S("ss_ui_sheet_influence", "influence"),
            NoSlowdown = UiText.S("ss_ui_sheet_no_slowdown", "none"),
            Speed = UiText.S("ss_ui_sheet_speed", "speed"),
            RecruitsLine = UiText.S("ss_ui_sheet_recruits", "Recruits"),
            YourTroopsLine = UiText.S("ss_ui_sheet_your_troops", "Your troops"),
            LordsLine = UiText.S("ss_ui_sheet_lords_line", "Lords"),
            OthersLine = UiText.S("ss_ui_sheet_others_line", "Others"),
            PackAnimals = UiText.S("ss_ui_role_pack", "Pack animals"),
            RidingHorses = UiText.S("ss_ui_role_riding", "Riding horses"),
            WarHorses = UiText.S("ss_ui_role_war", "War horses"),
            NobleHorses = UiText.S("ss_ui_role_noble", "Noble horses"),
            LameHorses = UiText.S("ss_ui_role_lame", "Lame horses"),
            Armour = UiText.S("ss_ui_loot_armour", "Armour"),
            MeleeWeapons = UiText.S("ss_ui_loot_melee", "Melee weapons"),
            Ranged = UiText.S("ss_ui_loot_ranged", "Ranged"),
            Shields = UiText.S("ss_ui_loot_shields", "Shields"),
            OtherGoods = UiText.S("ss_ui_loot_other_goods", "Other goods"),
            Each = UiText.S("ss_ui_sheet_each", "each"),
            ToHire = UiText.S("ss_ui_sheet_to_hire", "to hire"),
            Mercenaries = UiText.S("ss_ui_sheet_mercenaries", "mercenaries"),
            Keep = UiText.S("ss_ui_sheet_keep", "keep"),
            SellOnly = UiText.S("ss_ui_sheet_sell_only", "sell only"),
            ReplacedByHealthy = UiText.S("ss_ui_sheet_replaced", "replaced by healthy ones"),
            WoundedGoFirst = UiText.S("ss_ui_sheet_wounded", "wounded - they go first"),
            Locked = UiText.S("ss_ui_sheet_locked", "locked"),
            Ransom = UiText.S("ss_ui_sheet_ransom", "ransom"),
            OtherGoodsNote = UiText.S("ss_ui_sheet_other_goods_note", "every unlocked good that is not food, animal or gear"),
            YourMax = UiText.S("ss_ui_sheet_your_max", "your max"),
            YourMin = UiText.S("ss_ui_sheet_your_min", "your min"),
        };

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
                    return UiText.S("ss_ui_block_market_gold", "The market has no denari left to pay for more.");
                case EditBlock.NotEnoughGold: return UiText.S("ss_ui_block_gold", "You do not have the denari for it.");
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
                case EditBlock.NothingDropped:
                    return UiText.S("ss_ui_block_nothing_dropped", "Nobody is dismissed, so nobody comes back.");
                case EditBlock.NothingRecruited:
                    return UiText.S("ss_ui_block_nothing_recruited", "No recruit is queued to give back.");
                case EditBlock.DismissingThisType:
                    return UiText.S("ss_ui_block_dismissing_type",
                        "Men of this type are being dismissed under Your troops - take that back there first.");
                case EditBlock.RecruitingThisType:
                    return UiText.S("ss_ui_block_recruiting_type",
                        "Men of this type are being recruited under Recruits - take that back there first.");
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
                    return UiText.S1("ss_ui_warn_min_gold", "Below the denari you always keep ({GOLD}).", "GOLD",
                        UiFormat.Money(minGoldAfterDeal));
                case PlanWarning.BelowMinGoldForHorses:
                    return UiText.S1("ss_ui_warn_min_gold_horses", "Below the denari you keep before buying animals ({GOLD}).",
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
