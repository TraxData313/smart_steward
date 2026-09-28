using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>The Instructions tab's pure half (DESIGN §1.2): every setting grouped as §7, typed values read, the
/// prisoner tick-list.</summary>
public class SettingEditTests
{
    [Fact]
    public void Every_scalar_setting_shows_once_grouped_and_ordered_as_the_registry()
    {
        var groups = SettingEdit.Groups();
        Assert.Equal(SettingsRegistry.Groups, groups.Select(g => g.Key));
        var shown = groups.SelectMany(g => g.Value).ToList();
        Assert.Equal(SettingsRegistry.All.Where(d => d.IsScalar), shown);
        Assert.All(groups, g => Assert.All(g.Value, d => Assert.Equal(g.Key, d.Group)));
        Assert.DoesNotContain(shown, d => d.Kind == SettingKind.PriceBook || d.Kind == SettingKind.IdList);
    }

    [Fact]
    public void Number_boxes_show_the_value_and_the_range()
    {
        var s = new StewardSettings();
        var gold = (IntSetting)SettingsRegistry.Find("MinGoldAfterDeal")!;
        var food = (FloatSetting)SettingsRegistry.Find("FoodPerMan")!;
        var manual = (IntSetting)SettingsRegistry.Find("WarMountsHorseTarget")!;
        Assert.Equal("1000", SettingEdit.Text(gold, s));
        Assert.Equal("2", SettingEdit.Text(food, s));
        Assert.Equal("-1", SettingEdit.Text(manual, s));
        Assert.Equal("0–1,000,000", SettingEdit.Range(gold));
        Assert.Equal("0.1–10", SettingEdit.Range(food));
        Assert.Equal("–1–500", SettingEdit.Range(manual));
        Assert.Equal("", SettingEdit.Range(SettingsRegistry.Find("ModEnabled")!));
    }

    [Fact]
    public void Typed_values_are_read_and_the_service_clamps_them()
    {
        var gold = SettingsRegistry.Find("MinGoldAfterDeal")!;
        var food = SettingsRegistry.Find("FoodPerMan")!;
        Assert.True(SettingEdit.TryRead(gold, "2,500", out var g));
        Assert.Equal(2500L, g);
        Assert.True(SettingEdit.TryRead(food, "1,5", out var f));
        Assert.Equal(1.5, f);
        Assert.False(SettingEdit.TryRead(gold, "", out _));
        Assert.False(SettingEdit.TryRead(food, "lots", out _));
        Assert.False(SettingEdit.TryRead(SettingsRegistry.Find("ModEnabled")!, "1", out _));

        var service = new SettingsService(new MemoryStorage(), _ => { });
        service.Load();
        Assert.True(SettingEdit.TryRead(gold, "99999999", out var huge));
        service.Set(gold, huge);
        Assert.Equal(SettingsRegistry.MaxGold, service.Current.MinGoldAfterDeal);
    }

    [Fact]
    public void An_enum_button_cycles_through_its_values()
    {
        var s = new StewardSettings();
        var order = (EnumSetting)SettingsRegistry.Find("SellLootOrder")!;
        Assert.Equal(1, SettingEdit.NextIndex(order, s));
        order.SetIndex(s, 2);
        Assert.Equal(0, SettingEdit.NextIndex(order, s));
    }

    [Fact]
    public void Unticking_a_prisoner_excludes_it_and_ticking_it_back_forgets_it()
    {
        var s = new StewardSettings();
        Assert.True(PrisonerTicks.IsTicked(s, "looter"));
        PrisonerTicks.SetRansom(s, "looter", false);
        PrisonerTicks.SetRansom(s, "looter", false);
        Assert.Equal(new[] { "looter" }, s.PrisonersExcluded);
        Assert.False(PrisonerTicks.IsTicked(s, "looter"));
        PrisonerTicks.SetRansom(s, "looter", true);
        Assert.Empty(s.PrisonersExcluded);
    }

    [Fact]
    public void The_tick_list_holds_the_prisoners_now_and_the_ones_unticked_before()
    {
        var s = new StewardSettings { PrisonersExcluded = new List<string> { "sea_raider", "looter" } };
        var rows = PrisonerTicks.Rows(new[] { "looter", "bandit", "looter" }, s);
        Assert.Equal(new[] { "looter", "bandit", "sea_raider" }, rows);
    }

    private sealed class MemoryStorage : ISettingsStorage
    {
        private string? _text;

        public string? ReadText() => _text;

        public void WriteText(string text) => _text = text;

        public void WriteBackup(string text)
        {
        }

        public string? Stamp() => _text == null ? null : _text.Length.ToString();
    }
}
