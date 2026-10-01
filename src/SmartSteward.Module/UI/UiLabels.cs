using System;
using System.Linq;
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
            NobleHorsesStart = UiText.S("ss_ui_sheet_noble_start", "noble horses start at"),
            Lord = UiText.S("ss_ui_sheet_lord", "lord"),
            Lords = UiText.S("ss_ui_sheet_lords", "lords"),
            TierPrefix = UiText.S("ss_ui_tier_prefix", "T"),
            Sold = UiText.S("ss_ui_sheet_sold", "sold"),
            Pieces = UiText.S("ss_ui_sheet_pieces", "pieces"),
            Goods = UiText.S("ss_ui_sheet_goods", "goods"),
            NothingSold = UiText.S("ss_ui_sheet_nothing_sold", "nothing sold"),
            CheapestFirst = UiText.S("ss_ui_sheet_cheapest_first", "cheapest first"),
            LowestPerWeightFirst = UiText.S("ss_ui_sheet_lowest_per_weight", "lowest price per weight first"),
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
            // Round 5: the Goal column (the window binds it in step 23).
            HandsOffMark = UiText.S("ss_ui_sheet_hands_off", "–*"),
            NotManagedYet = UiText.S("ss_ui_sheet_not_managed", "Not managed yet: the steward starts on"),
            FoodJob = UiText.S("ss_ui_sheet_job_food", "food"),
            PackAnimalsJob = UiText.S("ss_ui_sheet_job_pack", "pack animals"),
            RidingHorsesJob = UiText.S("ss_ui_sheet_job_riding", "riding horses"),
            WarHorsesJob = UiText.S("ss_ui_sheet_job_war", "war horses"),
            NobleHorsesJob = UiText.S("ss_ui_sheet_job_noble", "noble horses"),
            At = UiText.S("ss_ui_sheet_at", "at"),
            YouHave = UiText.S("ss_ui_sheet_you_have", "you have"),
            TypeGoalAnyway = UiText.S("ss_ui_sheet_type_goal", "Type a goal to order it anyway."),
            ShortOfGoal = UiText.S("ss_ui_sheet_short", "Short of the goal:"),
            ShortMarketStock = UiText.S("ss_ui_sheet_short_stock", "the market has no more on offer"),
            ShortStockTaken = UiText.S("ss_ui_sheet_short_taken", "another row took the rest"),
            ShortPriceCap = UiText.S("ss_ui_sheet_short_price", "the next one costs more than your max price"),
            ShortMinSellPrice = UiText.S("ss_ui_sheet_short_min_sell", "the next one would fetch less than your min price"),
            ShortMarketGold = UiText.S("ss_ui_sheet_short_market_gold", "the market is out of denari"),
            ShortPurseFloor = UiText.S("ss_ui_sheet_short_floor", "keeps your purse at"),
            ShortThreshold = UiText.S("ss_ui_sheet_short_threshold", "waits for"),
            ShortNoneEligible = UiText.S("ss_ui_sheet_short_none_eligible", "nothing on offer the steward may buy"),
            ShortNothingToSell = UiText.S("ss_ui_sheet_short_nothing_to_sell", "nothing more it may sell (locked or unticked)"),
            ShortSurplusKept = UiText.S("ss_ui_sheet_short_surplus_kept", "selling the surplus is off in the Instructions"),
            ShortNotPossibleHere = UiText.S("ss_ui_sheet_short_not_here", "not possible here"),
            // Step 26's quest hover (it went out in English only until step 32 — now translatable like the rest).
            QuestKeptFor = UiText.S("ss_ui_sheet_quest_kept_for", "Kept for your quests:"),
            QuestBelow = UiText.S("ss_ui_sheet_quest_below", "Below what your quests need:"),
            QuestYouHold = UiText.S("ss_ui_sheet_quest_you_hold", "you hold"),
            // Step 32: the quest note after a name ("Grain  120 needed for quest").
            QuestNoteNeeded = UiText.S("ss_ui_sheet_quest_note_needed", "needed for quest"),
            QuestNoteNeededMany = UiText.S("ss_ui_sheet_quest_note_needed_many", "needed for quests"),
            QuestNoteHeld = UiText.S("ss_ui_sheet_quest_note_held", "for quest, held"),
            QuestNoteHeldMany = UiText.S("ss_ui_sheet_quest_note_held_many", "for quests, held"),
            QuestAskFor = UiText.S("ss_ui_sheet_quest_ask_for", "Your quests ask for:"),
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
                case EditBlock.PurseFloor:
                    return UiText.S("ss_ui_block_purse_floor",
                        "The next one would take your purse below the floor your goals keep (Instructions: Goals you set by hand).");
                case EditBlock.WaitsForThreshold:
                    return UiText.S("ss_ui_block_waits",
                        "Your goals wait until your purse reaches this job's threshold (Instructions: Goals you set by hand).");
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

        /// <summary>A part's name for the run's message line and the log (PLAN step 27): the section's title, the line's name, a
        /// tavern row's own name.</summary>
        public static string PartName(PlanPart part, StewardPlan? plan)
        {
            switch (part.Kind)
            {
                case PlanPartKind.Troops: return SheetSection(SheetGroup.Troops);
                case PlanPartKind.Food: return SheetSection(SheetGroup.Food);
                case PlanPartKind.Horses: return SheetSection(SheetGroup.Horses);
                case PlanPartKind.Prisoners: return SheetSection(SheetGroup.Prisoners);
                case PlanPartKind.Other: return SheetSection(SheetGroup.Other);
                case PlanPartKind.Lords: return UiText.S("ss_ui_sheet_lords_line", "Lords");
                case PlanPartKind.OtherPrisoners: return UiText.S("ss_ui_sheet_others_line", "Others");
                case PlanPartKind.Recruits: return UiText.S("ss_ui_sheet_recruits", "Recruits");
                case PlanPartKind.YourTroops: return UiText.S("ss_ui_sheet_your_troops", "Your troops");
                case PlanPartKind.OtherGoods: return UiText.S("ss_ui_loot_other_goods", "Other goods");
                default:
                    var row = part.RowId == null ? null : plan?.FindRow(part.RowId);
                    if (row == null)
                        return part.Id;
                    if (part.StackKey != null) // step 29: a breakdown line by its own item's name
                    {
                        var line = row.Breakdown.FirstOrDefault(l => Is(l.StackKey, part.StackKey));
                        if (line != null)
                            return OrId(line.Name, line.ItemId);
                    }
                    return SheetView.RowName(row, SheetText());
            }
        }

        /// <summary>
        /// The Deal / Deal group button's hover (PLAN step 27, DESIGN §1.1 "Do just this part"): enabled, what the part does alone in one line —
        /// <c>Ransom 9, sell 12: +3,160 denari</c> — and, when the purse alone cuts it, how many fewer and why; greyed, why. A line
        /// of one row (step 29) names it: <c>Buy 6 Grain: -120 denari</c> (<paramref name="name"/>; null = a section or a line of
        /// many rows).
        /// </summary>
        public static string PartHint(PartDeal? deal, string? name = null)
        {
            if (deal == null)
                return "";
            switch (deal.Block)
            {
                case PartBlock.NothingToDo:
                    return name == null
                        ? UiText.S("ss_ui_part_nothing_group", "Nothing to do in this group.") // step 31: it says "Deal group"
                        : UiText.S("ss_ui_part_nothing_line", "Nothing to do on this line.");
                case PartBlock.PurseFloor:
                    return UiText.S1("ss_ui_part_floor_deal_all",
                        "Alone it would take your purse below {GOLD} denari - the rest of the deal pays for it. Use Deal all.",
                        "GOLD", UiFormat.Money(deal.Floor));
                case PartBlock.NotEnoughGold:
                    return UiText.S("ss_ui_part_no_gold_deal_all",
                        "Alone your purse cannot pay for it - the rest of the deal pays for it. Use Deal all.");
            }
            var moves = new System.Collections.Generic.List<string>();
            void Add(int n, string text)
            {
                if (n > 0) moves.Add(text);
            }
            Add(deal.Ransomed, UiText.S1("ss_ui_part_ransom", "ransom {N}", "N", UiFormat.Money(deal.Ransomed)));
            Add(deal.Donated, UiText.S1("ss_ui_part_donate", "to the dungeon {N}", "N", UiFormat.Money(deal.Donated)));
            Add(deal.Sold, UiText.S1("ss_ui_part_sell", "sell {N}", "N", UiFormat.Money(deal.Sold)));
            Add(deal.Bought, UiText.S1("ss_ui_part_buy", "buy {N}", "N", UiFormat.Money(deal.Bought)));
            Add(deal.Hired, UiText.S1("ss_ui_part_hire", "hire {N}", "N", UiFormat.Money(deal.Hired)));
            Add(deal.Recruited, UiText.S1("ss_ui_part_recruit", "recruit {N}", "N", UiFormat.Money(deal.Recruited)));
            Add(deal.Dismissed, UiText.S1("ss_ui_part_dismiss", "dismiss {N}", "N", UiFormat.Money(deal.Dismissed)));
            string what = string.Join(", ", moves);
            if (what.Length > 0 && !string.IsNullOrEmpty(name))
                what = UiText.S2("ss_ui_part_named", "{WHAT} {NAME}", "WHAT", what, "NAME", name!);
            if (what.Length > 0)
                what = char.ToUpperInvariant(what[0]) + what.Substring(1);
            var money = new System.Collections.Generic.List<string>();
            if (deal.Gold != 0)
                money.Add(UiText.S1("ss_ui_part_denari", "{GOLD} denari", "GOLD", UiFormat.SignedMoney(deal.Gold)));
            if (deal.Influence > 0.05)
                money.Add(UiText.S1("ss_ui_part_influence", "{N} influence", "N", UiFormat.SignedInfluence(deal.Influence)));
            string text = money.Count == 0 ? what : what + ": " + string.Join(", ", money);
            if (deal.CutUnits > 0)
                text += "\n" + (deal.CutBy == PartBlock.PurseFloor
                    ? UiText.S2("ss_ui_part_cut_floor", "Alone: {N} fewer than in the whole deal - it keeps your purse at {GOLD} denari.",
                        "N", UiFormat.Money(deal.CutUnits), "GOLD", UiFormat.Money(deal.Floor))
                    : UiText.S1("ss_ui_part_cut_gold", "Alone: {N} fewer than in the whole deal - your purse alone cannot pay for them.",
                        "N", UiFormat.Money(deal.CutUnits)));
            return text;
        }

        /// <summary>Safe string for a name that might be missing.</summary>
        public static string OrId(string? name, string id) => string.IsNullOrEmpty(name) ? id : name!;

        public static bool Is(string? a, string b) => string.Equals(a, b, StringComparison.Ordinal);
    }
}
