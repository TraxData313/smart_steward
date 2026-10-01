using System;
using System.Collections.Generic;

namespace SmartSteward.Core.Settings
{
    /// <summary>
    /// THE settings registry: every V1 setting of DESIGN §7, in the table's order, with its group, UI
    /// label, player help, range and accessors onto <see cref="StewardSettings"/>. The settings file,
    /// MCM and the Instructions tab are all generated from this list, so they cannot drift apart; tests
    /// hold it to the §7 table (keys, order, groups, defaults, ranges). LATER settings (DESIGN "Release
    /// scope") are not registered.
    /// <para>Help texts are for players: plain words, one or two sentences, no default or range inside
    /// (those are added from the numbers here). Plain ASCII — the file is opened in any editor. A
    /// <c>\n</c> starts a new comment line in the file (an example); tooltips read it as a space.</para>
    /// </summary>
    public static class SettingsRegistry
    {
        public const string General = "General";
        public const string Money = "Money";

        /// <summary>"Goals you set by hand" (round 5) — the switches for the manual goals, and the goals themselves.</summary>
        public const string Goals = "Goals";
        public const string Food = "Food";
        public const string Prices = "Prices";
        public const string Pack = "Pack";
        public const string Mounts = "Mounts";
        public const string WarMounts = "War mounts";
        public const string Prisoners = "Prisoners";
        public const string Loot = "Loot";
        public const string Tavern = "Tavern";

        /// <summary>A gold amount the player may type: 0 … one million denars.</summary>
        public const int MaxGold = 1_000_000;

        /// <summary>The autonomous purse floor: 0 … ten million denars — the rich player's chest (DESIGN §6).</summary>
        public const int MaxAutonomousGold = 10_000_000;

        /// <summary>A role price cap (per animal): 0 … 100,000 denars.</summary>
        public const int MaxAnimalPrice = 100_000;

        /// <summary>The §7 groups in the table's order.</summary>
        public static IReadOnlyList<string> Groups { get; } = new[]
        {
            General, Money, Goals, Food, Prices, Pack, Mounts, WarMounts, Prisoners, Loot, Tavern,
        };

        /// <summary>Every registered setting, in the §7 table's order.</summary>
        public static IReadOnlyList<SettingDefinition> All { get; } = Build();

        private static readonly Dictionary<string, SettingDefinition> ByKey = Index();

        /// <summary>The group's heading for players ("Pack" → "Pack animals").</summary>
        public static string GroupLabel(string group) =>
            group == Pack ? "Pack animals" : group == Goals ? "Goals you set by hand" : group;

        /// <summary>The setting with this key, ignoring case (a hand-edited file may say "foodperman").</summary>
        public static SettingDefinition? Find(string key) =>
            ByKey.TryGetValue(key.Trim(), out var def) ? def : null;

        /// <summary>The settings of one group, in order.</summary>
        public static IEnumerable<SettingDefinition> InGroup(string group)
        {
            foreach (var def in All)
                if (def.Group == group)
                    yield return def;
        }

        /// <summary>The values that differ from the defaults, for the log — a bug report then says which
        /// settings were in effect: <c>FoodPerMan=3.0, SellLoot=true, PriceBook: 2 items</c>, or
        /// <c>all defaults</c>.</summary>
        public static string DescribeNonDefaults(StewardSettings settings)
        {
            var parts = new List<string>();
            foreach (var def in All)
            {
                switch (def)
                {
                    case PriceBookSetting book:
                        int items = 0;
                        foreach (var entry in book.Get(settings).Values)
                            if (entry != null && !entry.IsEmpty) items++;
                        if (items > 0) parts.Add(def.Key + ": " + items + (items == 1 ? " item" : " items"));
                        break;
                    case GoalsSetting goals:
                        var set = goals.Get(settings);
                        if (set.Count > 0)
                            parts.Add(def.Key + ": " + string.Join(", ", System.Linq.Enumerable.Select(
                                System.Linq.Enumerable.OrderBy(set, p => p.Key, StringComparer.Ordinal),
                                p => p.Key + "=" + p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                        break;
                    default:
                        var text = SettingsFile.ValueText(def, settings);
                        if (text != def.DefaultFileText) parts.Add(def.Key + "=" + text.Trim('"'));
                        break;
                }
            }
            return parts.Count == 0 ? "all defaults" : string.Join(", ", parts);
        }

        private static Dictionary<string, SettingDefinition> Index()
        {
            var index = new Dictionary<string, SettingDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var def in All) index.Add(def.Key, def);
            return index;
        }

        private static List<SettingDefinition> Build() => new List<SettingDefinition>
        {
            // ── General ──────────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.ModEnabled), General, "Steward on",
                "Master switch. When off, the steward stays out of the way: no window, no menu entry, no warnings, "
                + "nothing done on its own.",
                s => s.ModEnabled, (s, v) => s.ModEnabled = v),
            new BoolSetting(nameof(StewardSettings.AutoPopupOnTownEnter), General, "Open on entering a town",
                "Open the Party Steward window when your party enters a town.",
                s => s.AutoPopupOnTownEnter, (s, v) => s.AutoPopupOnTownEnter = v),
            new BoolSetting(nameof(StewardSettings.AutoPopupOnVillageEnter), General, "Open on entering a village",
                "Open the Party Steward window when your party enters a village.",
                s => s.AutoPopupOnVillageEnter, (s, v) => s.AutoPopupOnVillageEnter = v),
            new BoolSetting(nameof(StewardSettings.PopupOnlyWithChanges), General, "Only open with suggestions",
                "Open the window by itself only when the steward has something to suggest.",
                s => s.PopupOnlyWithChanges, (s, v) => s.PopupOnlyWithChanges = v),
            new BoolSetting(nameof(StewardSettings.WarnIfNotReviewed), General, "Warn before leaving",
                "Ask before you leave a town or village whose suggestions you never looked at.",
                s => s.WarnIfNotReviewed, (s, v) => s.WarnIfNotReviewed = v),
            new BoolSetting(nameof(StewardSettings.AutonomousSteward), General, "Full-autonomous steward",
                "For when you are rich and done with micromanaging: on arriving at a town or village the steward "
                + "carries out its plan at once - no window, no questions, no warning - and sums it up in the message "
                + "log. It never hires in the tavern and never takes your purse below Keep while autonomous. "
                + "The Party Steward menu entry stays, to look or adjust. When off, the steward only proposes and you click.",
                s => s.AutonomousSteward, (s, v) => s.AutonomousSteward = v),
            new BoolSetting(nameof(StewardSettings.LocksProtectFoodAndHorses), General, "Locks protect food & horses",
                "Off: the steward manages food and horses even when you locked them in the inventory - your locks keep "
                + "guarding armour and weapons. On: locked food and horses are left alone, like armour and weapons.",
                s => s.LocksProtectFoodAndHorses, (s, v) => s.LocksProtectFoodAndHorses = v),

            // ── Money ────────────────────────────────────────────────────────────────────────────
            new IntSetting(nameof(StewardSettings.MinGoldAfterDeal), Money, "Always keep (denari)",
                "No purchase takes your purse below this many denari.",
                0, MaxGold, s => s.MinGoldAfterDeal, (s, v) => s.MinGoldAfterDeal = v),
            new IntSetting(nameof(StewardSettings.MinGoldForHorses), Money, "Keep before buying animals (denari)",
                "No horse, mule or camel purchase takes your purse below this. Food only answers to the floor above - "
                + "food comes first.",
                0, MaxGold, s => s.MinGoldForHorses, (s, v) => s.MinGoldForHorses = v),
            new IntSetting(nameof(StewardSettings.AutonomousMinGold), Money, "Keep while autonomous (denari)",
                "While the full-autonomous steward is on, it never takes your purse below this - both floors above "
                + "rise to it when they are lower. Raise the price limits to let it spend more freely, never this.",
                0, MaxAutonomousGold, s => s.AutonomousMinGold, (s, v) => s.AutonomousMinGold = v),

            // ── Goals you set by hand (round 5) ─────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.ManualGoalsWaitForThresholds), Goals, "Your goals wait for the thresholds",
                "A goal you typed (or clicked) in the Suggestion tab acts even while your purse is below its job's \"Manage ... "
                + "from\" denari - it is your order. On: your goals wait for those thresholds too, like the steward.",
                s => s.ManualGoalsWaitForThresholds, (s, v) => s.ManualGoalsWaitForThresholds = v),
            new BoolSetting(nameof(StewardSettings.ManualGoalsKeepPurseFloor), Goals, "Your goals keep the purse floors",
                "Buying for your goals stops at the floors above - food at Always keep, horses at Keep before buying animals. "
                + "Off: your goals may spend below them. While autonomous, Keep while autonomous always holds.",
                s => s.ManualGoalsKeepPurseFloor, (s, v) => s.ManualGoalsKeepPurseFloor = v),
            new BoolSetting(nameof(StewardSettings.ManualGoalsObeyPriceCaps), Goals, "Your goals obey the price limits",
                "Your goals buy only up to the max buy prices (Prices tab) and the max price per animal, and sell only at the "
                + "min sell prices. Off: they buy and sell at any price. An item unticked in the Prices tab is never traded "
                + "either way.",
                s => s.ManualGoalsObeyPriceCaps, (s, v) => s.ManualGoalsObeyPriceCaps = v),
            new BoolSetting(nameof(StewardSettings.QuestGoalsEnabled), Goals, "Keep what your quests need",
                "The steward reads your ongoing quests and keeps what they ask for - the grain for a headman, the horses for a "
                + "lord, the troops for a garrison, the prisoners for a landowner: never sold, ransomed, donated or dismissed. A food "
                + "a quest asks for is bought up to the need, like a goal of yours. A goal you type still wins. Off: quests are "
                + "not read.",
                s => s.QuestGoalsEnabled, (s, v) => s.QuestGoalsEnabled = v),
            new GoalsSetting(nameof(StewardSettings.Goals), Goals, "Goals",
                "The goals you set in the Suggestion tab - where a row should end after the deal - kept for every town until "
                + "you click its reset. By row: \"food:\" and the item id for a food, \"mounts:pack\" (pack animals), "
                + "\"mounts:riding\" (riding horses), \"mounts:war\" (war horses), \"mounts:noble\" (noble horses). A row "
                + "without a goal follows the rules "
                + "of the Instructions tab.\n"
                + "Example: \"food:grain\": 60, \"mounts:war\": 15"),

            // ── Food ─────────────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.FoodEnabled), Food, "Manage food",
                "Buy food up to the target and sell what is far above it.",
                s => s.FoodEnabled, (s, v) => s.FoodEnabled = v),
            new IntSetting(nameof(StewardSettings.FoodMinDenari), Food, "Manage food from (denari)",
                "The steward starts managing food once your purse holds at least this many denari when you arrive - below it "
                + "it neither buys nor sells food, and you trade by hand. 0 = always.",
                0, MaxGold, s => s.FoodMinDenari, (s, v) => s.FoodMinDenari = v),
            new IntSetting(nameof(StewardSettings.FoodDays), Food, "Keep food for (days)",
                "How many days of food the steward keeps for your party as it will be after the deal - at the game's own "
                + "rate, perks included (a man eats one food in about 20 days, a prisoner half as much), so 40 days is about "
                + "2 food per man. The Instructions tab shows what your days mean per man right now.",
                1, 365, s => s.FoodDays, (s, v) => s.FoodDays = v),
            new BoolSetting(nameof(StewardSettings.FoodCountPrisoners), Food, "Feed prisoners too",
                "Count your prisoners when working out the food target - half a man each, as the game feeds them.",
                s => s.FoodCountPrisoners, (s, v) => s.FoodCountPrisoners = v),
            new EnumSetting<FoodStrategy>(nameof(StewardSettings.FoodStrategy), Food, "Food buying",
                "Balanced buys the kinds you hold least of first - every different food lifts morale. "
                + "Cheapest always buys the cheapest kind.",
                new[] { "Balanced (variety first)", "Cheapest" },
                s => s.FoodStrategy, (s, v) => s.FoodStrategy = v),
            new BoolSetting(nameof(StewardSettings.SellFoodSurplus), Food, "Sell surplus food",
                "Sell food above the target plus the tolerance below, back down to the target, most-held kind first.",
                s => s.SellFoodSurplus, (s, v) => s.SellFoodSurplus = v),
            new IntSetting(nameof(StewardSettings.FoodSurplusTolerancePercent), Food, "Surplus tolerance (%)",
                "How far above the target food may pile up before the steward sells: 25 sells only above 125% of the target.",
                0, 500, s => s.FoodSurplusTolerancePercent, (s, v) => s.FoodSurplusTolerancePercent = v),

            // ── Prices ───────────────────────────────────────────────────────────────────────────
            new FloatSetting(nameof(StewardSettings.FoodBuyPriceMultiplier), Prices, "Food buy price multiplier",
                "Max buy price of a food = its base price (Prices tab) times this. 2.0 pays up to twice the average - "
                + "grain at 10 is bought up to 20. Food is cheap and keeps the party alive, so this is generous.",
                0.1, 10, 2, s => s.FoodBuyPriceMultiplier, (s, v) => s.FoodBuyPriceMultiplier = v),
            new FloatSetting(nameof(StewardSettings.FoodSellPriceMultiplier), Prices, "Food sell price multiplier",
                "Min sell price of a food = its base price (Prices tab) times this. 0.5 sells surplus food at half the "
                + "average sell price or better; 0 sells at any price.",
                0, 10, 2, s => s.FoodSellPriceMultiplier, (s, v) => s.FoodSellPriceMultiplier = v),
            new FloatSetting(nameof(StewardSettings.HorseBuyPriceMultiplier), Prices, "Horse buy price multiplier",
                "Max buy price of a horse, mule or camel = its base price (Prices tab) times this. 1.2 pays up to 20% over "
                + "the average. Richer now? Raise it.",
                0.1, 10, 2, s => s.HorseBuyPriceMultiplier, (s, v) => s.HorseBuyPriceMultiplier = v),
            new FloatSetting(nameof(StewardSettings.HorseSellPriceMultiplier), Prices, "Horse sell price multiplier",
                "Min sell price of a horse, mule or camel = its base price (Prices tab) times this. 0.8 sells at 80% of the "
                + "average sell price or better; 0 sells at any price.",
                0, 10, 2, s => s.HorseSellPriceMultiplier, (s, v) => s.HorseSellPriceMultiplier = v),
            new BoolSetting(nameof(StewardSettings.AutoFillFoodPrices), Prices, "Average prices for food",
                "Food you gave no price in the Prices tab uses its average price as the base.",
                s => s.AutoFillFoodPrices, (s, v) => s.AutoFillFoodPrices = v),
            new BoolSetting(nameof(StewardSettings.AutoFillPackAndMountPrices), Prices, "Average prices for pack animals and horses",
                "Pack animals and riding horses you gave no price use their average price as the base.",
                s => s.AutoFillPackAndMountPrices, (s, v) => s.AutoFillPackAndMountPrices = v),
            new BoolSetting(nameof(StewardSettings.AutoFillWarMountPrices), Prices, "Average prices for war horses",
                "War horses you gave no price use their average price as the base. When off, they are bought only under "
                + "the war horse price cap until you type a price. (Noble horses always show their average sell price - "
                + "they are only ever sold.)",
                s => s.AutoFillWarMountPrices, (s, v) => s.AutoFillWarMountPrices = v),
            new PriceBookSetting(nameof(StewardSettings.PriceBook), Prices, "Price book",
                "Your own prices and ticks from the Prices tab, by item id - only what you changed. Each item may have "
                + "\"Buy\": false (never buy it), \"BuyBase\": your max buy price before its buy multiplier (food or "
                + "horse), \"Sell\": false (never sell it), \"SellBase\": your min sell price before its sell multiplier. "
                + "Leave a field out to use the default (ticked, average price).\n"
                + "Example: \"grain\": { \"BuyBase\": 12, \"Sell\": false }"),

            // ── Pack animals ─────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.PackAnimalsEnabled), Pack, "Manage pack animals",
                "Keep a set number of pack animals (sumpter horses, mules, pack camels). Each one carries about 100 more weight.",
                s => s.PackAnimalsEnabled, (s, v) => s.PackAnimalsEnabled = v),
            new IntSetting(nameof(StewardSettings.PackAnimalsMinDenari), Pack, "Manage pack animals from (denari)",
                "The steward starts managing pack animals once your purse holds at least this many denari when you arrive - "
                + "below it it neither buys nor sells them. 0 = always.",
                0, MaxGold, s => s.PackAnimalsMinDenari, (s, v) => s.PackAnimalsMinDenari = v),
            new IntSetting(nameof(StewardSettings.PackAnimalsTarget), Pack, "Pack animals to keep",
                "How many pack animals the party keeps.",
                0, 500, s => s.PackAnimalsTarget, (s, v) => s.PackAnimalsTarget = v),
            new IntSetting(nameof(StewardSettings.PackAnimalMaxPrice), Pack, "Max price per pack animal",
                "Never pay more than this for one pack animal, whatever the price book says. 0 = no cap. "
                + "Not scaled by the buy multiplier.",
                0, MaxAnimalPrice, s => s.PackAnimalMaxPrice, (s, v) => s.PackAnimalMaxPrice = v),
            new BoolSetting(nameof(StewardSettings.SellPackAnimalSurplus), Pack, "Sell surplus pack animals",
                "Sell pack animals above the target, the dearest first.",
                s => s.SellPackAnimalSurplus, (s, v) => s.SellPackAnimalSurplus = v),

            // ── Mounts ───────────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.MountsEnabled), Mounts, "Manage riding horses",
                "Keep horses for the men who walk, so the party moves faster.",
                s => s.MountsEnabled, (s, v) => s.MountsEnabled = v),
            new IntSetting(nameof(StewardSettings.MountsMinDenari), Mounts, "Manage riding horses from (denari)",
                "The steward starts managing riding horses once your purse holds at least this many denari when you arrive - "
                + "below it it neither buys nor sells them, nor sells noble or lame horses. 0 = always.",
                0, MaxGold, s => s.MountsMinDenari, (s, v) => s.MountsMinDenari = v),
            new IntSetting(nameof(StewardSettings.MountsPer100Footmen), Mounts, "Horses per 100 footmen",
                "Horses kept for every 100 men on foot: 110 is one each plus 10% spare. The war horses you keep count "
                + "among them; riding horses fill the rest.",
                0, 300, s => s.MountsPer100Footmen, (s, v) => s.MountsPer100Footmen = v),
            new IntSetting(nameof(StewardSettings.MountMaxPrice), Mounts, "Max price per riding horse",
                "Never pay more than this for a footman's horse, whatever the price book says. 0 = no cap. "
                + "Not scaled by the buy multiplier.",
                0, MaxAnimalPrice, s => s.MountMaxPrice, (s, v) => s.MountMaxPrice = v),
            new BoolSetting(nameof(StewardSettings.SellMountSurplus), Mounts, "Sell surplus riding horses",
                "Sell riding horses above the target, the dearest first.",
                s => s.SellMountSurplus, (s, v) => s.SellMountSurplus = v),
            new BoolSetting(nameof(StewardSettings.SellNobleHorses), Mounts, "Sell noble horses",
                "Noble horses are for you and your companions - no vanilla troop needs one. The steward sells the party's "
                + "noble horses above the number to keep (Noble horses to keep, 0 by default), never below their min sell "
                + "price in the Prices tab. A noble horse you lock in the inventory is always kept, and a kept one carries "
                + "a footman.",
                s => s.SellNobleHorses, (s, v) => s.SellNobleHorses = v),
            new BoolSetting(nameof(StewardSettings.ReplaceLameHorses), Mounts, "Replace lame horses",
                "Sell the party's lame and old horses and pack animals and buy healthy ones in their place. When off they "
                + "are kept - a lame horse still carries a footman. The steward never buys one either way.",
                s => s.ReplaceLameHorses, (s, v) => s.ReplaceLameHorses = v),

            // ── War mounts ───────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.WarMountsEnabled), WarMounts, "Manage war horses",
                "Keep a set number of war horses for upgrading your troops into cavalry. When off, a war horse is just "
                + "another riding horse to the steward.",
                s => s.WarMountsEnabled, (s, v) => s.WarMountsEnabled = v),
            new IntSetting(nameof(StewardSettings.WarHorsesMinDenari), WarMounts, "Manage war horses from (denari)",
                "The steward starts keeping war horses once your purse holds at least this many denari when you arrive - "
                + "below it it neither buys nor sells them. 0 = always.",
                0, MaxGold, s => s.WarHorsesMinDenari, (s, v) => s.WarHorsesMinDenari = v),
            new IntSetting(nameof(StewardSettings.WarMountsToKeep), WarMounts, "War horses to keep",
                "How many war horses the party keeps - bought up to it, sold above it. They carry footmen until you "
                + "upgrade men with them (who then ride as cavalry), so they count among the horses per 100 footmen. "
                + "0 = none kept: spare war horses are sold. Only once your purse reaches the war horse threshold above.",
                0, 500, s => s.WarMountsToKeep, (s, v) => s.WarMountsToKeep = v),
            new IntSetting(nameof(StewardSettings.NobleHorsesToKeep), WarMounts, "Noble horses to keep",
                "How many noble horses the party keeps, like the war horses - bought up to it at their prices in the Prices "
                + "tab (and at most the max price per noble horse), the rest sold (Sell noble horses). Some mods upgrade troops with noble horses; vanilla never does. "
                + "They count among the horses per 100 footmen. 0 = none kept: every noble horse you did not lock is sold.",
                0, 500, s => s.NobleHorsesToKeep, (s, v) => s.NobleHorsesToKeep = v),
            new IntSetting(nameof(StewardSettings.NobleHorsesMinDenari), WarMounts, "Manage kept noble horses from (denari)",
                "When you keep noble horses, the steward starts managing them once your purse holds at least this many "
                + "denari when you arrive - below it it neither buys nor sells them. With none to keep they are sold "
                + "with the riding horses. 0 = always.",
                0, MaxGold, s => s.NobleHorsesMinDenari, (s, v) => s.NobleHorsesMinDenari = v),
            new IntSetting(nameof(StewardSettings.WarMountMaxPrice), WarMounts, "Max price per war horse",
                "Never pay more than this for one war horse, whatever the price book says. 0 = no cap. "
                + "Not scaled by the buy multiplier.",
                0, MaxAnimalPrice, s => s.WarMountMaxPrice, (s, v) => s.WarMountMaxPrice = v),
            new IntSetting(nameof(StewardSettings.NobleHorseMaxPrice), WarMounts, "Max price per noble horse",
                "Never pay more than this for one noble horse you keep, whatever the price book says. 0 = no cap. "
                + "Not scaled by the buy multiplier. Only matters while you keep noble horses.",
                0, MaxAnimalPrice, s => s.NobleHorseMaxPrice, (s, v) => s.NobleHorseMaxPrice = v),
            new BoolSetting(nameof(StewardSettings.SellWarMountSurplus), WarMounts, "Sell surplus war horses",
                "Sell war horses above the number to keep, the dearest first.",
                s => s.SellWarMountSurplus, (s, v) => s.SellWarMountSurplus = v),

            // ── Prisoners ────────────────────────────────────────────────────────────────────────
            new EnumSetting<PrisonerChoice>(nameof(StewardSettings.LordPrisonerAction), Prisoners, "Captured lords",
                "What the steward does with captured lords in a town: keep them, ransom them for denari (a ransomed lord goes "
                + "free), or donate them to the dungeon of a town of your kingdom that is not your clan's, for influence. "
                + "A lord to donate is kept where the game does not allow it or the dungeon is full.",
                new[] { "Keep", "Ransom for denari", "Donate for influence" },
                s => s.LordPrisonerAction, (s, v) => s.LordPrisonerAction = v),
            new EnumSetting<PrisonerChoice>(nameof(StewardSettings.PrisonerAction), Prisoners, "Other prisoners",
                "What the steward does with every other prisoner in a town: keep them, ransom them for denari, or donate "
                + "them to the dungeon of a town of your kingdom that is not your clan's, for influence - the most valuable "
                + "first; those that do not fit, or all of them where the game does not allow it, are ransomed.",
                new[] { "Keep", "Ransom for denari", "Donate for influence" },
                s => s.PrisonerAction, (s, v) => s.PrisonerAction = v),

            // ── Loot ─────────────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.SellLoot), Loot, "Sell loot",
                "Offer to sell captured gear in groups (armour, melee weapons, ranged, shields) and your other goods, in the "
                + "Other section.",
                s => s.SellLoot, (s, v) => s.SellLoot = v),
            new BoolSetting(nameof(StewardSettings.SellLootEquipment), Loot, "Sell weapons and armour",
                "Include weapons, armour, shields and ammunition in the loot sale.",
                s => s.SellLootEquipment, (s, v) => s.SellLootEquipment = v),
            new BoolSetting(nameof(StewardSettings.SellLootOtherGoods), Loot, "Sell other goods",
                "Include every trade good that is not food or an animal - wool, salt, pottery, jewelry, iron... - in the sale, "
                + "one Other goods line. Goods you lock in the inventory are never sold.",
                s => s.SellLootOtherGoods, (s, v) => s.SellLootOtherGoods = v),
            new IntSetting(nameof(StewardSettings.SellLootMaxItemValue), Loot, "Keep pieces worth more than",
                "Never sell a piece worth more than this many denari. 0 = no cap. Pieces you locked in the inventory "
                + "are never sold anyway.",
                0, MaxGold, s => s.SellLootMaxItemValue, (s, v) => s.SellLootMaxItemValue = v),
            new EnumSetting<SellLootOrder>(nameof(StewardSettings.SellLootOrder), Loot, "Loot selling order",
                "Which pieces go first when the market cannot pay for everything: the cheapest (the most weight for "
                + "the denari), the lowest price per weight, or the most expensive (the game's own habit).",
                new[] { "Cheapest first", "Lowest price per weight", "Most expensive first" },
                s => s.SellLootOrder, (s, v) => s.SellLootOrder = v,
                new Dictionary<string, string> { ["LowestPricePerKg"] = nameof(SellLootOrder.LowestPricePerWeight) }),

            // ── Tavern ───────────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.ShowTavern), Tavern, "Show the tavern",
                "Show the tavern section in towns: wanderers and mercenaries to hire with a click.",
                s => s.ShowTavern, (s, v) => s.ShowTavern = v),
            new BoolSetting(nameof(StewardSettings.ShowWanderers), Tavern, "Show wanderers",
                "List the wanderers for hire in the town's tavern.",
                s => s.ShowWanderers, (s, v) => s.ShowWanderers = v),
            new BoolSetting(nameof(StewardSettings.ShowMercenaries), Tavern, "Show mercenaries",
                "List the mercenary band in the town's tavern.",
                s => s.ShowMercenaries, (s, v) => s.ShowMercenaries = v),
            new BoolSetting(nameof(StewardSettings.ShowTroops), Tavern, "Show the troops",
                "Show the troops section in towns and villages: the recruits the notables offer you, then your own troops, "
                + "to recruit or dismiss with a click. Nothing is ever recruited or dismissed unless you click.",
                s => s.ShowTroops, (s, v) => s.ShowTroops = v),
        };
    }
}
