using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// Food's own price multipliers (PLAN step 20, Anton 2026.09.28 — round 4: "food prices multipliers set from 0.5 to 2, gain
/// costs 10 up to 20 id be happy to buy"): food ×2.0 / ×0.5, horses keep ×1.2 / ×0.8 under their renamed keys.
/// </summary>
public class FoodMultiplierTests
{
    private static Scenario RealMultipliers()
    {
        var s = new Scenario();
        var d = new StewardSettings();
        s.Settings.FoodBuyPriceMultiplier = d.FoodBuyPriceMultiplier;
        s.Settings.FoodSellPriceMultiplier = d.FoodSellPriceMultiplier;
        return s;
    }

    [Fact]
    public void The_defaults_are_two_and_a_half_for_food_and_the_old_pair_for_horses()
    {
        var d = new StewardSettings();
        Assert.Equal(2.0, d.FoodBuyPriceMultiplier);
        Assert.Equal(0.5, d.FoodSellPriceMultiplier);
        Assert.Equal(1.2, d.HorseBuyPriceMultiplier);
        Assert.Equal(0.8, d.HorseSellPriceMultiplier);
    }

    [Theory]
    [InlineData(19, 20)] // grain at 10 on average: the steward pays up to 20
    [InlineData(21, 0)]
    public void Grain_at_10_is_bought_up_to_20(int price, int bought)
    {
        var s = RealMultipliers().Party(10).Food("grain", market: 100, buy: price);
        s.Snap.AveragePrices["grain"] = new AveragePrices(10, 8);
        Assert.Equal(bought, s.Plan().Row("food:grain").Change);
    }

    [Theory]
    [InlineData(4, -80)] // min sell = 8 × 0.5 = 4
    [InlineData(3, 0)]
    public void Surplus_food_sells_down_to_half_the_average_sell_price(int price, int change)
    {
        var s = RealMultipliers().Party(10).Food("fish", held: 100, market: 1, buy: 12, sell: price);
        s.Snap.AveragePrices["fish"] = new AveragePrices(12, 8);
        Assert.Equal(change, s.Plan().Row("food:fish").Change);
    }

    [Fact]
    public void Horses_keep_their_own_multipliers()
    {
        var s = RealMultipliers().Party(10, footmen: 10).Mount("hunter", "horse", market: 30, buy: 250);
        s.Snap.AveragePrices["hunter"] = new AveragePrices(200, 100);
        Assert.Equal(0, s.Plan().Row("mounts:riding").Change); // 250 > 200 × 1.2
        s.Settings.HorseBuyPriceMultiplier = 1.3;
        Assert.Equal(11, s.Plan().Row("mounts:riding").Change); // 260 ≥ 250
    }

    [Fact]
    public void An_old_file_carries_its_multipliers_over_to_the_horse_pair_once()
    {
        var parsed = SettingsFile.Parse("{ \"BuyPriceMultiplier\": 1.5, \"sellpricemultiplier\": 0.7 }");
        Assert.Empty(parsed.Problems);
        Assert.Equal(1.5, parsed.Settings.HorseBuyPriceMultiplier);
        Assert.Equal(0.7, parsed.Settings.HorseSellPriceMultiplier);
        Assert.Equal(2.0, parsed.Settings.FoodBuyPriceMultiplier); // food starts at its own defaults
        Assert.Equal(0.5, parsed.Settings.FoodSellPriceMultiplier);
        Assert.Contains(parsed.Renamed, n => n.Contains("\"BuyPriceMultiplier\" is now HorseBuyPriceMultiplier") && n.Contains("1.5"));
        Assert.Contains(parsed.Renamed, n => n.Contains("is now HorseSellPriceMultiplier"));

        var rewritten = SettingsFile.Generate(parsed.Settings);
        Assert.Contains("\"HorseBuyPriceMultiplier\": 1.5,", rewritten);
        Assert.DoesNotContain("\"BuyPriceMultiplier\"", rewritten);
        Assert.DoesNotContain("\"SellPriceMultiplier\"", rewritten);
        Assert.Empty(SettingsFile.Parse(rewritten).Renamed); // once
    }

    [Fact]
    public void The_new_key_written_too_wins()
    {
        var parsed = SettingsFile.Parse("{ \"BuyPriceMultiplier\": 1.5, \"HorseBuyPriceMultiplier\": 2.5 }");
        Assert.Equal(2.5, parsed.Settings.HorseBuyPriceMultiplier);
        Assert.Contains(parsed.Problems, p => p.Contains("the old name of HorseBuyPriceMultiplier"));
    }
}
