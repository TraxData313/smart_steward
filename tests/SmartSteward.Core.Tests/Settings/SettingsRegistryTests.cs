using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Settings;

/// <summary>
/// The registry's own promises (its agreement with DESIGN §7 is in <see cref="SettingsTests"/>): every
/// accessor reads and writes ITS property, values are clamped where they enter, and the texts are fit
/// for MCM and the file.
/// </summary>
public class SettingsRegistryTests
{
    /// <summary>A valid value different from the default, for any scalar setting.</summary>
    internal static object OtherValue(SettingDefinition def) => def switch
    {
        BoolSetting b => !b.Default,
        IntSetting n => n.Default == n.Max ? n.Max - 1 : n.Default + 1,
        FloatSetting f => f.Normalize(f.Default == f.Max ? f.Max - 0.5 : f.Default + 0.25),
        EnumSetting e => (e.DefaultIndex + 1) % e.Names.Count,
        _ => throw new ArgumentException(def.Key),
    };

    private static void SetDirect(SettingDefinition def, StewardSettings settings, object value)
    {
        switch (def)
        {
            case BoolSetting b: b.Set(settings, (bool)value); break;
            case IntSetting n: n.Set(settings, (int)value); break;
            case FloatSetting f: f.Set(settings, (double)value); break;
            case EnumSetting e: e.SetIndex(settings, (int)value); break;
            default: throw new ArgumentException(def.Key);
        }
    }

    public static IEnumerable<object[]> Scalars() =>
        SettingsRegistry.All.Where(d => d.IsScalar).Select(d => new object[] { d.Key });

    [Theory]
    [MemberData(nameof(Scalars))]
    public void Each_setter_writes_its_own_property_and_nothing_else(string key)
    {
        var def = SettingsRegistry.Find(key)!;
        var settings = new StewardSettings();
        var before = SettingsFile.Generate(new StewardSettings());
        SetDirect(def, settings, OtherValue(def));

        var property = typeof(StewardSettings).GetProperty(key)!.GetValue(settings);
        Assert.Equal(def.GetValue(settings), property);
        Assert.NotEqual(def.DefaultValue, property);

        // Exactly one line of the file changed: this key's.
        var changed = before.Split(SettingsFile.NewLine).Zip(SettingsFile.Generate(settings).Split(SettingsFile.NewLine))
            .Where(pair => pair.First != pair.Second).ToList();
        var line = Assert.Single(changed).Second;
        Assert.StartsWith("  \"" + key + "\": ", line);
    }

    [Fact]
    public void Collections_are_the_price_book_and_the_prisoner_list_only()
    {
        var collections = SettingsRegistry.All.Where(d => !d.IsScalar).ToList();
        Assert.Equal(new[] { "PriceBook", "PrisonersExcluded" }, collections.Select(d => d.Key));
        var settings = new StewardSettings();
        Assert.Same(settings.PriceBook, collections[0].GetValue(settings));
        Assert.Same(settings.PrisonersExcluded, collections[1].GetValue(settings));
    }

    [Fact]
    public void Keys_are_unique_and_found_ignoring_case()
    {
        Assert.Equal(SettingsRegistry.All.Count, SettingsRegistry.All.Select(d => d.Key).Distinct().Count());
        Assert.Same(SettingsRegistry.Find("FoodPerMan"), SettingsRegistry.Find(" foodperman "));
        Assert.Null(SettingsRegistry.Find("FoodPerMen"));
    }

    [Fact]
    public void Texts_are_fit_for_MCM_and_the_file()
    {
        foreach (var def in SettingsRegistry.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.Label), def.Key);
            Assert.False(string.IsNullOrWhiteSpace(def.Help), def.Key);
            // MCM splits group paths on '/', reads "{=…}" as a string id, and names a property by its label.
            foreach (var text in new[] { def.Label, SettingsRegistry.GroupLabel(def.Group) })
            {
                Assert.DoesNotContain("/", text);
                Assert.DoesNotContain("{=", text);
            }
            // Plain ASCII: the file opens right in any editor.
            Assert.True((def.Label + def.Help).All(c => c < 128), def.Key + " has a non-ASCII character");
        }
        foreach (var group in SettingsRegistry.Groups)
        {
            var labels = SettingsRegistry.InGroup(group).Select(d => d.Label).ToList();
            Assert.Equal(labels.Count, labels.Distinct().Count());
        }
    }

    [Fact]
    public void Hints_carry_the_help_the_default_and_the_range()
    {
        var gold = (IntSetting)SettingsRegistry.Find("MinGoldAfterDeal")!;
        Assert.Equal(gold.Help + " Default: 1,000. Range: 0 to 1,000,000.", gold.Hint);

        var food = (FloatSetting)SettingsRegistry.Find("FoodPerMan")!;
        Assert.EndsWith(" Default: 2.0. Range: 0.1 to 10.0.", food.Hint);

        var popup = SettingsRegistry.Find("AutoExecute")!;
        Assert.EndsWith(" Default: off.", popup.Hint);

        var strategy = SettingsRegistry.Find("FoodStrategy")!;
        Assert.EndsWith(" Default: Balanced (variety first).", strategy.Hint);

        foreach (var def in SettingsRegistry.All)
        {
            Assert.StartsWith(def.Help.Replace("\n", " "), def.Hint);
            Assert.DoesNotContain("\n", def.Hint);
        }
    }

    [Fact]
    public void Int_settings_clamp_what_they_are_given()
    {
        var target = (IntSetting)SettingsRegistry.Find("WarMountsManualTarget")!;
        var s = new StewardSettings();
        target.Set(s, -50);
        Assert.Equal(-1, s.WarMountsManualTarget);
        target.Set(s, 9999);
        Assert.Equal(500, s.WarMountsManualTarget);
        Assert.Equal(500, target.Clamp(long.MaxValue));
        Assert.Equal(-1, target.Clamp(long.MinValue));
    }

    [Fact]
    public void Float_settings_clamp_round_and_refuse_nonsense()
    {
        var multiplier = (FloatSetting)SettingsRegistry.Find("BuyPriceMultiplier")!;
        var s = new StewardSettings();
        multiplier.Set(s, 1.2f); // what MCM's float slider hands over: 1.2000000476837158
        Assert.Equal(1.2, s.BuyPriceMultiplier);
        multiplier.Set(s, 0.0);
        Assert.Equal(0.1, s.BuyPriceMultiplier);
        multiplier.Set(s, 55);
        Assert.Equal(10, s.BuyPriceMultiplier);
        multiplier.Set(s, 1.23456);
        Assert.Equal(1.23, s.BuyPriceMultiplier);
        multiplier.Set(s, double.NaN);
        Assert.Equal(1.2, s.BuyPriceMultiplier);
        multiplier.Set(s, double.PositiveInfinity);
        Assert.Equal(1.2, s.BuyPriceMultiplier);
        Assert.Equal("1.25", multiplier.Format(1.25));
        Assert.Equal("10.0", multiplier.Format(10));
    }

    [Fact]
    public void Enum_settings_work_by_index_and_by_name()
    {
        var order = (EnumSetting)SettingsRegistry.Find("SellLootOrder")!;
        var s = new StewardSettings();
        Assert.Equal(new[] { "Cheapest", "LowestPricePerKg", "MostExpensive" }, order.Names);
        Assert.Equal(3, order.Labels.Count);
        Assert.Equal(0, order.DefaultIndex);

        order.SetIndex(s, 2);
        Assert.Equal(SellLootOrder.MostExpensive, s.SellLootOrder);
        Assert.Equal(2, order.GetIndex(s));

        order.SetIndex(s, 7); // out of range → the default
        Assert.Equal(SellLootOrder.Cheapest, s.SellLootOrder);

        Assert.Equal(1, order.IndexOf(" lowestpriceperkg "));
        Assert.Equal(-1, order.IndexOf("Dearest"));
    }

    [Fact]
    public void Group_labels_are_the_groups_but_friendlier()
    {
        Assert.Equal("Pack animals", SettingsRegistry.GroupLabel(SettingsRegistry.Pack));
        Assert.Equal("War mounts", SettingsRegistry.GroupLabel(SettingsRegistry.WarMounts));
        Assert.Equal(SettingsRegistry.Groups.Count, SettingsRegistry.Groups.Select(SettingsRegistry.GroupLabel).Distinct().Count());
    }
}
