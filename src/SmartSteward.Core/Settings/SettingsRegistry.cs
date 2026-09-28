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
            General, Money, Food, Prices, Pack, Mounts, WarMounts, Prisoners, Loot, Tavern,
        };

        /// <summary>Every registered setting, in the §7 table's order.</summary>
        public static IReadOnlyList<SettingDefinition> All { get; } = Build();

        private static readonly Dictionary<string, SettingDefinition> ByKey = Index();

        /// <summary>The group's heading for players ("Pack" → "Pack animals").</summary>
        public static string GroupLabel(string group) => group == Pack ? "Pack animals" : group;

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
            new IntSetting(nameof(StewardSettings.MinGoldAfterDeal), Money, "Always keep (gold)",
                "No purchase takes your purse below this many denars.",
                0, MaxGold, s => s.MinGoldAfterDeal, (s, v) => s.MinGoldAfterDeal = v),
            new IntSetting(nameof(StewardSettings.MinGoldForHorses), Money, "Keep before buying animals (gold)",
                "No horse, mule or camel purchase takes your purse below this. Food only answers to the floor above - "
                + "food comes first.",
                0, MaxGold, s => s.MinGoldForHorses, (s, v) => s.MinGoldForHorses = v),
            new IntSetting(nameof(StewardSettings.AutonomousMinGold), Money, "Keep while autonomous (gold)",
                "While the full-autonomous steward is on, it never takes your purse below this - both floors above "
                + "rise to it when they are lower. Raise the price limits to let it spend more freely, never this.",
                0, MaxAutonomousGold, s => s.AutonomousMinGold, (s, v) => s.AutonomousMinGold = v),

            // ── Food ─────────────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.FoodEnabled), Food, "Manage food",
                "Buy food up to the target and sell what is far above it.",
                s => s.FoodEnabled, (s, v) => s.FoodEnabled = v),
            new FloatSetting(nameof(StewardSettings.FoodPerMan), Food, "Food per man",
                "Food units kept for every man. A man eats about one unit in 20 days, so 2 lasts about 40 days.",
                0.1, 10, 2, s => s.FoodPerMan, (s, v) => s.FoodPerMan = v),
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
            new FloatSetting(nameof(StewardSettings.BuyPriceMultiplier), Prices, "Buy price multiplier",
                "Max buy price = the item's base price (Prices tab) times this. 1.2 pays up to 20% over the average. "
                + "Richer now? Raise it.",
                0.1, 10, 2, s => s.BuyPriceMultiplier, (s, v) => s.BuyPriceMultiplier = v),
            new FloatSetting(nameof(StewardSettings.SellPriceMultiplier), Prices, "Sell price multiplier",
                "Min sell price = the item's base price (Prices tab) times this. 0.8 sells at 80% of the average sell "
                + "price or better; 0 sells at any price.",
                0, 10, 2, s => s.SellPriceMultiplier, (s, v) => s.SellPriceMultiplier = v),
            new BoolSetting(nameof(StewardSettings.AutoFillFoodPrices), Prices, "Average prices for food",
                "Food you gave no price in the Prices tab uses its average price as the base.",
                s => s.AutoFillFoodPrices, (s, v) => s.AutoFillFoodPrices = v),
            new BoolSetting(nameof(StewardSettings.AutoFillPackAndMountPrices), Prices, "Average prices for pack animals and horses",
                "Pack animals and riding horses you gave no price use their average price as the base.",
                s => s.AutoFillPackAndMountPrices, (s, v) => s.AutoFillPackAndMountPrices = v),
            new BoolSetting(nameof(StewardSettings.AutoFillWarMountPrices), Prices, "Average prices for war horses",
                "War and noble horses you gave no price use their average price as the base. When off, they are bought "
                + "only under the upgrade horse price cap until you type a price.",
                s => s.AutoFillWarMountPrices, (s, v) => s.AutoFillWarMountPrices = v),
            new PriceBookSetting(nameof(StewardSettings.PriceBook), Prices, "Price book",
                "Your own prices and ticks from the Prices tab, by item id - only what you changed. Each item may have "
                + "\"Buy\": false (never buy it), \"BuyBase\": your max buy price before the buy multiplier, "
                + "\"Sell\": false (never sell it), \"SellBase\": your min sell price before the sell multiplier. "
                + "Leave a field out to use the default (ticked, average price).\n"
                + "Example: \"grain\": { \"BuyBase\": 12, \"Sell\": false }"),

            // ── Pack animals ─────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.PackAnimalsEnabled), Pack, "Manage pack animals",
                "Keep a set number of pack animals (sumpter horses, mules, pack camels). Each one carries about 100 more weight.",
                s => s.PackAnimalsEnabled, (s, v) => s.PackAnimalsEnabled = v),
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
                "Buy horses for the men who walk, so the party moves faster.",
                s => s.MountsEnabled, (s, v) => s.MountsEnabled = v),
            new IntSetting(nameof(StewardSettings.MountsPer100Footmen), Mounts, "Horses per 100 footmen",
                "Riding horses kept for every 100 men on foot: 110 is one each plus 10% spare.",
                0, 300, s => s.MountsPer100Footmen, (s, v) => s.MountsPer100Footmen = v),
            new IntSetting(nameof(StewardSettings.MountMaxPrice), Mounts, "Max price per riding horse",
                "Never pay more than this for a footman's horse, whatever the price book says. 0 = no cap. "
                + "Not scaled by the buy multiplier.",
                0, MaxAnimalPrice, s => s.MountMaxPrice, (s, v) => s.MountMaxPrice = v),
            new BoolSetting(nameof(StewardSettings.WarMountsCountAsMounts), Mounts, "Upgrade horses carry footmen",
                "Horses kept for troop upgrades also count as riding horses for the footmen until the upgrade takes them.",
                s => s.WarMountsCountAsMounts, (s, v) => s.WarMountsCountAsMounts = v),
            new BoolSetting(nameof(StewardSettings.SellMountSurplus), Mounts, "Sell surplus riding horses",
                "Sell riding horses above the target, the dearest first.",
                s => s.SellMountSurplus, (s, v) => s.SellMountSurplus = v),

            // ── War mounts ───────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.WarMountsEnabled), WarMounts, "Manage upgrade horses",
                "Keep the horses your troops need to upgrade into cavalry.",
                s => s.WarMountsEnabled, (s, v) => s.WarMountsEnabled = v),
            new IntSetting(nameof(StewardSettings.WarMountsHorseTarget), WarMounts, "Horses for upgrades",
                "Horses kept for troops whose upgrade needs a plain horse (the party screen's upgrade tooltip says "
                + "\"Required: Horse\"). -1 = automatic: as many as your troops ready to upgrade need right now, plus the "
                + "spares below. 0 or more = keep exactly that many.",
                -1, 500, s => s.WarMountsHorseTarget, (s, v) => s.WarMountsHorseTarget = v),
            new IntSetting(nameof(StewardSettings.WarMountsWarHorseTarget), WarMounts, "War horses for upgrades",
                "War horses kept for troops whose upgrade needs one (the tooltip says \"Required: War Horse\"). "
                + "-1 = automatic: as many as your troops ready to upgrade need right now, plus the spares below. "
                + "0 or more = keep exactly that many.",
                -1, 500, s => s.WarMountsWarHorseTarget, (s, v) => s.WarMountsWarHorseTarget = v),
            new IntSetting(nameof(StewardSettings.WarMountsExtra), WarMounts, "Spare upgrade horses (each kind)",
                "Extra horses kept on top of the automatic count - this many horses AND this many war horses, for "
                + "the kinds your troops upgrade into. Not added to a kind you gave a fixed number above.",
                0, 100, s => s.WarMountsExtra, (s, v) => s.WarMountsExtra = v),
            new IntSetting(nameof(StewardSettings.WarMountMaxPrice), WarMounts, "Max price per upgrade horse",
                "Never pay more than this for one upgrade horse, whatever the price book says. 0 = no cap. "
                + "Not scaled by the buy multiplier.",
                0, MaxAnimalPrice, s => s.WarMountMaxPrice, (s, v) => s.WarMountMaxPrice = v),
            new BoolSetting(nameof(StewardSettings.SellWarMountSurplus), WarMounts, "Sell surplus upgrade horses",
                "Sell upgrade horses above what your troops need, the dearest first.",
                s => s.SellWarMountSurplus, (s, v) => s.SellWarMountSurplus = v),

            // ── Prisoners ────────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.RansomPrisoners), Prisoners, "Ransom prisoners",
                "Offer your prisoners to the town's ransom broker.",
                s => s.RansomPrisoners, (s, v) => s.RansomPrisoners = v),
            new BoolSetting(nameof(StewardSettings.RansomHeroPrisoners), Prisoners, "Include lords",
                "Also ransom (or donate) captured lords. A ransomed lord goes free.",
                s => s.RansomHeroPrisoners, (s, v) => s.RansomHeroPrisoners = v),
            new BoolSetting(nameof(StewardSettings.DonatePrisonersWhenPossible), Prisoners, "Donate when possible",
                "In a town of your kingdom that is not your clan's, send prisoners to its dungeon for influence "
                + "instead of gold. Those that do not fit are ransomed.",
                s => s.DonatePrisonersWhenPossible, (s, v) => s.DonatePrisonersWhenPossible = v),

            // ── Loot ─────────────────────────────────────────────────────────────────────────────
            new BoolSetting(nameof(StewardSettings.SellLoot), Loot, "Sell loot",
                "Offer to sell captured gear in groups (armour, melee weapons, ranged, shields).",
                s => s.SellLoot, (s, v) => s.SellLoot = v),
            new BoolSetting(nameof(StewardSettings.SellLootEquipment), Loot, "Sell weapons and armour",
                "Include weapons, armour, shields and ammunition in the loot sale.",
                s => s.SellLootEquipment, (s, v) => s.SellLootEquipment = v),
            new IntSetting(nameof(StewardSettings.SellLootMaxItemValue), Loot, "Keep pieces worth more than",
                "Never sell a piece worth more than this many denars. 0 = no cap. Pieces you locked in the inventory "
                + "are never sold anyway.",
                0, MaxGold, s => s.SellLootMaxItemValue, (s, v) => s.SellLootMaxItemValue = v),
            new EnumSetting<SellLootOrder>(nameof(StewardSettings.SellLootOrder), Loot, "Loot selling order",
                "Which pieces go first when the market cannot pay for everything: the cheapest (the most weight for "
                + "the gold), the lowest price per kg, or the most expensive (the game's own habit).",
                new[] { "Cheapest first", "Lowest price per kg", "Most expensive first" },
                s => s.SellLootOrder, (s, v) => s.SellLootOrder = v),

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
        };
    }
}
