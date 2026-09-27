using SmartSteward.Core.Presentation;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>The Prices tab's pure half (DESIGN §1.3): edits store only the player's changes; a row shows the typed
/// base or the grey placeholder, the multiplier and the final.</summary>
public class PriceBookViewTests
{
    private static readonly AveragePrices Grain = new(11, 7);

    [Fact]
    public void Unticking_stores_an_override_and_ticking_back_removes_it()
    {
        var s = new StewardSettings();
        PriceBookEditor.SetBuy(s, "grain", false);
        Assert.False(s.PriceBook["grain"].Buy);
        PriceBookEditor.SetSell(s, "grain", false);
        PriceBookEditor.SetBuy(s, "grain", true);
        Assert.Null(s.PriceBook["grain"].Buy);
        PriceBookEditor.SetSell(s, "grain", true);
        Assert.False(s.PriceBook.ContainsKey("grain")); // nothing left to remember
    }

    [Fact]
    public void Bases_are_clamped_and_cleared_by_reset_while_the_ticks_stay()
    {
        var s = new StewardSettings();
        PriceBookEditor.SetBuyBase(s, "grain", 12);
        PriceBookEditor.SetSellBase(s, "grain", 5_000_000);
        PriceBookEditor.SetSell(s, "grain", false);
        Assert.Equal(12, s.PriceBook["grain"].BuyBase);
        Assert.Equal(PriceBookSetting.MaxBase, s.PriceBook["grain"].SellBase);

        PriceBookEditor.ClearBases(s, "grain");
        Assert.Null(s.PriceBook["grain"].BuyBase);
        Assert.Null(s.PriceBook["grain"].SellBase);
        Assert.False(s.PriceBook["grain"].Sell);

        PriceBookEditor.SetBuyBase(s, "fish", -3);
        Assert.Equal(0, s.PriceBook["fish"].BuyBase);
        PriceBookEditor.SetBuyBase(s, "fish", null);
        Assert.False(s.PriceBook.ContainsKey("fish"));
        Assert.Null(PriceBookEditor.EntryOf(s, "fish"));
    }

    [Fact]
    public void An_untouched_food_row_shows_the_placeholders_and_the_finals()
    {
        var view = PriceRowView.Of("grain", PriceBookGroup.Food, new StewardSettings(), Grain);
        Assert.True(view.BuyTicked);
        Assert.True(view.SellTicked);
        Assert.Equal("", view.BuyBaseText);
        Assert.Equal("11", view.BuyPlaceholder);
        Assert.Equal("7", view.SellPlaceholder);
        Assert.Equal("× 1.2", view.BuyMultiplier);
        Assert.Equal("× 0.8", view.SellMultiplier);
        Assert.Equal("13", view.BuyFinal);  // [11] × 1.2 → 13 (DESIGN §1.3)
        Assert.Equal("6", view.SellFinal);  // [7] × 0.8 → 6
        Assert.False(view.HasTypedBase);
    }

    [Fact]
    public void A_typed_base_replaces_the_placeholder_in_the_final()
    {
        var s = new StewardSettings { BuyPriceMultiplier = 2 };
        PriceBookEditor.SetBuyBase(s, "grain", 20);
        var view = PriceRowView.Of("grain", PriceBookGroup.Food, s, Grain);
        Assert.Equal("20", view.BuyBaseText);
        Assert.Equal("40", view.BuyFinal);
        Assert.Equal("× 2", view.BuyMultiplier);
        Assert.True(view.HasTypedBase);
    }

    [Fact]
    public void War_mounts_have_no_placeholder_until_their_auto_fill_is_on()
    {
        var s = new StewardSettings();
        var view = PriceRowView.Of("charger", PriceBookGroup.WarMounts, s, new AveragePrices(1500, 700));
        Assert.Equal("", view.BuyPlaceholder);
        Assert.Equal("—", view.BuyFinal); // no base: only the role cap buys it
        Assert.Equal("—", view.SellFinal); // no base: any price

        s.AutoFillWarMountPrices = true;
        view = PriceRowView.Of("charger", PriceBookGroup.WarMounts, s, new AveragePrices(1500, 700));
        Assert.Equal("1,500", view.BuyPlaceholder);
        Assert.Equal("1,800", view.BuyFinal);
    }

    [Fact]
    public void An_unticked_row_still_shows_its_prices()
    {
        var s = new StewardSettings();
        PriceBookEditor.SetBuy(s, "grain", false);
        var view = PriceRowView.Of("grain", PriceBookGroup.Food, s, Grain);
        Assert.False(view.BuyTicked);
        Assert.Equal("13", view.BuyFinal);
    }

    [Fact]
    public void Edits_survive_the_settings_file_round_trip()
    {
        var s = new StewardSettings();
        PriceBookEditor.SetBuyBase(s, "grain", 12);
        PriceBookEditor.SetSell(s, "mule", false);
        var back = SettingsFile.Parse(SettingsFile.Generate(s)).Settings;
        Assert.Equal(12, back.PriceBook["grain"].BuyBase);
        Assert.False(back.PriceBook["mule"].Sell);
        Assert.Equal(2, back.PriceBook.Count);
    }
}
