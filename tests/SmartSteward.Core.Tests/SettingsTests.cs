using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests;

/// <summary>
/// Holds <see cref="StewardSettings"/> to DESIGN §7: the property names ARE the table's keys and every
/// default is the table's default — a key added to one side only, or a default changed in one place,
/// fails here.
/// </summary>
public class SettingsTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SmartSteward.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("SmartSteward.sln not found");
    }

    /// <summary>(key, default) of every row of DESIGN §7's settings table.</summary>
    private static List<(string Key, string Default)> DesignTable()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, "docs", "DESIGN.md"));
        int start = text.IndexOf("## 7. Settings", StringComparison.Ordinal);
        int end = text.IndexOf("## 8.", start, StringComparison.Ordinal);
        var rows = new List<(string, string)>();
        foreach (var line in text[start..end].Split('\n'))
        {
            var cells = line.Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length < 5 || cells[2] == "Key" || cells[2].StartsWith("---", StringComparison.Ordinal))
                continue;
            rows.Add((cells[2], cells[3]));
        }
        return rows;
    }

    private static PropertyInfo[] SettingProperties() =>
        typeof(StewardSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance);

    [Fact]
    public void Property_names_are_exactly_the_DESIGN_7_keys()
    {
        var keys = DesignTable().Select(r => r.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var props = SettingProperties().Select(p => p.Name).OrderBy(k => k, StringComparer.Ordinal).ToList();
        Assert.True(keys.Count > 40, "the §7 table was not found or not parsed");
        Assert.Equal(keys, props);
    }

    [Fact]
    public void Defaults_are_the_DESIGN_7_defaults()
    {
        var settings = new StewardSettings();
        foreach (var (key, text) in DesignTable())
        {
            var value = typeof(StewardSettings).GetProperty(key)!.GetValue(settings);
            switch (value)
            {
                case bool b:
                    Assert.True(bool.Parse(text) == b, key);
                    break;
                case int i:
                    Assert.True(int.Parse(text, CultureInfo.InvariantCulture) == i, key);
                    break;
                case double d:
                    Assert.True(double.Parse(text, CultureInfo.InvariantCulture) == d, key);
                    break;
                case Enum e:
                    Assert.True(text == e.ToString(), key);
                    break;
                case System.Collections.IDictionary dict:
                    Assert.True(text == "{}" && dict.Count == 0, key);
                    break;
                case System.Collections.IList list:
                    Assert.True(text == "[]" && list.Count == 0, key);
                    break;
                default:
                    Assert.Fail($"{key}: unhandled type {value?.GetType()}");
                    break;
            }
        }
    }

    [Fact]
    public void Enum_values_match_the_design_options()
    {
        Assert.Equal(new[] { "Balanced", "Cheapest" }, Enum.GetNames(typeof(FoodStrategy)));
        Assert.Equal(new[] { "Cheapest", "LowestPricePerKg", "MostExpensive" }, Enum.GetNames(typeof(SellLootOrder)));
    }

    [Theory]
    [InlineData(11, 1.2, 13)] // DESIGN §1.3's own example
    [InlineData(7, 0.8, 6)]   // and its sell side
    [InlineData(10, 0.8, 8)]  // binary slop must not round 8.0000001 away
    [InlineData(5, 1.3, 7)]   // halves round away from zero
    [InlineData(100, 0, 0)]
    public void Final_price_is_base_times_multiplier_rounded(int basePrice, double multiplier, int expected)
    {
        Assert.Equal(expected, PriceBook.Final(basePrice, multiplier));
        Assert.Equal(expected, PriceBook.Final(basePrice, (float)multiplier)); // an MCM float must agree
    }

    [Fact]
    public void Price_book_uses_overrides_then_placeholders_per_group()
    {
        var settings = new StewardSettings();
        var avg = new AveragePrices(100, 50);

        var food = PriceBook.Resolve("grain", PriceBookGroup.Food, settings, avg);
        Assert.True(food.BuyTicked && food.SellTicked);
        Assert.Equal(100, food.BuyBase);
        Assert.True(food.BuyBaseIsPlaceholder);
        Assert.Equal(120, food.FinalMaxBuy);
        Assert.Equal(40, food.FinalMinSell);

        // War mounts do not auto-fill by default: empty bases.
        var war = PriceBook.Resolve("charger", PriceBookGroup.WarMounts, settings, avg);
        Assert.Null(war.BuyBase);
        Assert.Null(war.FinalMaxBuy);
        Assert.Null(war.FinalMinSell);

        settings.PriceBook["charger"] = new PriceBookEntry { BuyBase = 1500, Sell = false };
        settings.BuyPriceMultiplier = 2;
        war = PriceBook.Resolve("charger", PriceBookGroup.WarMounts, settings, avg);
        Assert.Equal(1500, war.BuyBase);
        Assert.False(war.BuyBaseIsPlaceholder);
        Assert.Equal(3000, war.FinalMaxBuy);
        Assert.False(war.SellTicked);
        Assert.True(war.BuyTicked);
    }

    [Theory]
    [InlineData(ItemKind.Food, "grain", PriceBookGroup.Food)]
    [InlineData(ItemKind.PackAnimal, "sumpter_horse", PriceBookGroup.PackAnimals)]
    [InlineData(ItemKind.Mount, "horse", PriceBookGroup.Mounts)]
    [InlineData(ItemKind.Mount, "war_horse", PriceBookGroup.WarMounts)]
    [InlineData(ItemKind.Mount, "noble_horse", PriceBookGroup.WarMounts)]
    public void Price_book_groups(ItemKind kind, string category, PriceBookGroup expected)
    {
        Assert.Equal(expected, PriceBook.GroupOf(kind, category));
    }

    [Theory]
    [InlineData(ItemKind.Equipment)]
    [InlineData(ItemKind.Other)]
    public void Armour_weapons_and_others_are_never_in_the_V1_price_book(ItemKind kind)
    {
        Assert.Null(PriceBook.GroupOf(kind, "anything"));
    }

    [Theory]
    [InlineData(null, 300, 300)]
    [InlineData(250, 300, 250)]
    [InlineData(400, 300, 300)]
    [InlineData(400, 0, 400)]
    [InlineData(null, 0, null)]
    public void Animal_buy_limit_is_the_lower_of_book_and_role_cap(int? book, int cap, int? expected)
    {
        Assert.Equal(expected, PriceBook.AnimalBuyLimit(book, cap));
    }

    [Theory]
    [InlineData("HeadArmor", LootGroup.Armour)]
    [InlineData("HorseHarness", LootGroup.Armour)]
    [InlineData("Cape", LootGroup.Armour)]
    [InlineData("Polearm", LootGroup.MeleeWeapons)]
    [InlineData("TwoHandedWeapon", LootGroup.MeleeWeapons)]
    [InlineData("Arrows", LootGroup.Ranged)]
    [InlineData("SlingStones", LootGroup.Ranged)]
    [InlineData("Musket", LootGroup.Ranged)]
    [InlineData("Shield", LootGroup.Shields)]
    [InlineData("Horse", LootGroup.None)]
    [InlineData("Goods", LootGroup.None)]
    [InlineData("Banner", LootGroup.None)]
    [InlineData("Book", LootGroup.None)]
    [InlineData(null, LootGroup.None)]
    public void Loot_groups_follow_the_DESIGN_table(string? itemType, LootGroup expected)
    {
        Assert.Equal(expected, LootGroups.FromItemType(itemType));
    }

    [Fact]
    public void The_loot_group_table_in_DESIGN_names_the_same_item_types()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, "docs", "DESIGN.md"));
        var named = Regex.Matches(text, @"\| (Armour|Melee weapons|Ranged \(Anton's ""firing""\)|Shields) \| ([^|]+) \|")
            .SelectMany(m => Regex.Matches(m.Groups[2].Value, @"[A-Z][A-Za-z]+").Select(x => x.Value))
            .Distinct().ToList();
        Assert.True(named.Count >= 20, "the §2.6 table was not found");
        foreach (var type in named)
            Assert.NotEqual(LootGroup.None, LootGroups.FromItemType(type));
    }
}
