using SmartSteward.Core.Planning;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Planning;

public class FoodPlannerTests
{
    [Fact]
    public void The_goal_is_days_of_food_at_the_games_own_rate_with_its_perks_and_half_eating_prisoners()
    {
        // Anton 2026.09.28: "keep food [40] days". 10 men and 6 prisoners kept eat like 10 + 6/2 = 13 men; a perk cuts the
        // party's daily use by a fifth: 13/20 × 0.8 = 0.52 a day → 0.04 per eater → 40 days = 1.6 per eater → ceil(20.8).
        var s = new Scenario().Party(10).Prisoner("looter", 6, 20);
        s.Snap.Prison.CanRansom = false; // keep them
        s.Snap.Party.DailyFoodUse = 13 / 20.0 * 0.8;
        var plan = s.Plan();
        Assert.Equal(13, plan.Facts.FoodEaters);
        Assert.Equal(21, plan.Facts.FoodTarget);
        Assert.Equal(1.6, FoodGoal.PerSoul(s.Settings, s.Snap), 6);          // the bracket: "~1.6 per soul"
        Assert.Equal("1.6", SmartSteward.Core.Presentation.UiFormat.Decimal(FoodGoal.PerSoul(s.Settings, s.Snap), 1));

        s.Settings.FoodDays = 50;                                             // 2.0 per eater
        Assert.Equal(26, s.Plan().Facts.FoodTarget);

        s.Snap.Party.DailyFoodUse = 0;                                        // not read: vanilla's 1/20
        s.Settings.FoodDays = 40;
        Assert.Equal(26, s.Plan().Facts.FoodTarget);
        Assert.Equal(2.0, FoodGoal.PerSoul(s.Settings, s.Snap), 6);           // 40 days = the old 2 per man
    }

    [Fact]
    public void Days_follow_the_party_after_the_deal()
    {
        // The rate is the party's NOW; the eaters are those the deal leaves: the ransomed stop eating, the hired start.
        var s = new Scenario().Party(20).Prisoner("looter", 10, 20).Food("grain", market: 500, buy: 10);
        s.Snap.Party.DailyFoodUse = 25 / 20.0; // (20 + 10/2) / 20, no perks
        s.Snap.Tavern = new SmartSteward.Core.Snapshot.TavernInfo
        {
            Mercenaries = new SmartSteward.Core.Snapshot.MercenaryOffer { TroopId = "merc", Name = "Spears", Available = 10, PricePerMan = 50 },
        };
        var plan = s.Plan();
        Assert.Equal(20, plan.Facts.FoodEaters);  // the 10 looters are ransomed
        Assert.Equal(40, plan.Facts.FoodTarget);
        plan.Increase("tavern:mercenaries", EditSize.All);
        Assert.Equal(60, plan.Facts.FoodTarget);   // 30 men × 40 days / 20
        plan.Increase("prisoner:looter", EditSize.All);
        Assert.Equal(70, plan.Facts.FoodTarget);   // + the 10 looters kept, half each
    }

    [Fact]
    public void Target_is_days_of_food_for_the_eaters_with_prisoners_as_half_eaters()
    {
        var s = new Scenario().Party(10).Prisoner("looter", 5, 20);
        s.Snap.Prison.CanRansom = false; // keep the prisoners
        var plan = s.Plan();
        Assert.Equal(12, plan.Facts.FoodEaters); // 10 + 5/2 (integer halves, like the game)
        Assert.Equal(24, plan.Facts.FoodTarget);

        s.Settings.FoodCountPrisoners = false;
        Assert.Equal(20, s.Plan().Facts.FoodTarget);

        s.Settings.FoodDays = 22; // 22 days × 1/20 = 1.1 per man: 11.000000000000002 must not ceil to 12
        Assert.Equal(11, s.Plan().Facts.FoodTarget);
    }

    [Fact]
    public void Prisoners_ransomed_in_this_visit_are_not_fed()
    {
        var plan = new Scenario().Party(10).Prisoner("looter", 10, 20).Plan();
        Assert.Equal(-10, plan.Row("prisoner:looter").Change);
        Assert.Equal(10, plan.Facts.FoodEaters);
    }

    [Fact]
    public void Balanced_buys_variety_first_ties_to_the_cheapest()
    {
        var plan = new Scenario().Party(10)
            .Food("grain", market: 100, buy: 10)
            .Food("fish", market: 100, buy: 12)
            .Plan();
        Assert.Equal(10, plan.Row("food:grain").Change);
        Assert.Equal(10, plan.Row("food:fish").Change);
        Assert.Equal(-(10 * 10 + 10 * 12), plan.Row("food:grain").GoldDelta + plan.Row("food:fish").GoldDelta);
    }

    [Fact]
    public void Balanced_fills_the_type_held_the_fewest_of()
    {
        var plan = new Scenario().Party(10)
            .Food("grain", held: 15, market: 100, buy: 10)
            .Food("fish", market: 100, buy: 30)
            .Plan();
        Assert.Equal(0, plan.Row("food:grain").Change);
        Assert.Equal(5, plan.Row("food:fish").Change);
        Assert.Equal(20, plan.Totals.FoodUnitsAfter);
    }

    [Fact]
    public void Cheapest_buys_only_the_cheapest_type()
    {
        var s = new Scenario().Party(10).Food("grain", market: 100, buy: 10).Food("fish", market: 100, buy: 12);
        s.Settings.FoodStrategy = FoodStrategy.Cheapest;
        var plan = s.Plan();
        Assert.Equal(20, plan.Row("food:grain").Change);
        Assert.Equal(0, plan.Row("food:fish").Change);
    }

    [Fact]
    public void Market_stock_limits_the_buys_and_the_next_type_takes_over()
    {
        var s = new Scenario().Party(10).Food("grain", market: 3, buy: 10).Food("fish", market: 100, buy: 12);
        s.Settings.FoodStrategy = FoodStrategy.Cheapest;
        var plan = s.Plan();
        Assert.Equal(3, plan.Row("food:grain").Change);
        Assert.Equal(17, plan.Row("food:fish").Change);
        Assert.Equal(3, plan.Row("food:grain").MaxBuy);
    }

    [Theory]
    [InlineData(24, 0)]  // 24 ≤ 20 × 1.25 = 25: inside the tolerance
    [InlineData(25, 0)]  // exactly at the threshold: still kept
    [InlineData(26, -6)] // above: sold back down to the target
    public void Surplus_is_sold_only_above_target_plus_tolerance_and_back_to_target(int held, int expected)
    {
        var plan = new Scenario().Party(10).Food("grain", held: held, sell: 8).Plan();
        Assert.Equal(25.0, plan.Facts.FoodSellAbove);
        Assert.Equal(expected, plan.Row("food:grain").Change);
    }

    [Fact]
    public void Surplus_sells_the_most_held_type_first()
    {
        var plan = new Scenario().Party(10)
            .Food("grain", held: 20, sell: 8)
            .Food("fish", held: 12, sell: 9)
            .Plan();
        // 32 held, target 20: grain goes down to fish's level, then they alternate.
        Assert.Equal(-10, plan.Row("food:grain").Change);
        Assert.Equal(-2, plan.Row("food:fish").Change);
        Assert.Equal(20, plan.Totals.FoodUnitsAfter);
    }

    [Fact]
    public void SellFoodSurplus_off_keeps_the_surplus()
    {
        var s = new Scenario().Party(10).Food("grain", held: 60);
        s.Settings.SellFoodSurplus = false;
        var row = s.Plan().Row("food:grain");
        Assert.Equal(0, row.Change);
        Assert.Equal(60, row.MaxSell); // the player may still sell by hand
    }

    [Fact]
    public void With_LocksProtectFoodAndHorses_locked_food_counts_as_held_but_is_never_sold()
    {
        var s = new Scenario().Party(10)
            .Food("grain", held: 40, locked: true)
            .Food("fish", held: 10);
        s.Settings.LocksProtectFoodAndHorses = true; // the old way; off (the default) sells it - InventoryLockTests
        var plan = s.Plan();
        Assert.Equal(0, plan.Row("food:grain").Change);
        Assert.Equal(40, plan.Row("food:grain").Locked);
        Assert.Equal(-10, plan.Row("food:fish").Change); // 50 held, target 20: only the fish can go
    }

    [Fact]
    public void Unticked_food_is_not_bought_and_not_sold()
    {
        var s = new Scenario().Party(10).Food("grain", market: 100).Food("fish", held: 60);
        s.Settings.PriceBook["grain"] = new PriceBookEntry { Buy = false };
        s.Settings.PriceBook["fish"] = new PriceBookEntry { Sell = false };
        var plan = s.Plan();
        Assert.Null(plan.FindRow("food:grain")); // on the market but never buyable → no row
        Assert.Equal(0, plan.Row("food:fish").Change);
    }

    [Fact]
    public void Food_dearer_than_its_final_max_buy_is_not_bought()
    {
        // Average 10 × 1.2 = 12: a town asking 13 is too dear.
        var s = new Scenario().Party(10).Food("grain", market: 100, buy: 13, noAverage: true);
        s.Snap.AveragePrices["grain"] = new(10, 8);
        Assert.Equal(0, s.Plan().Row("food:grain").Change);

        s.Settings.BuyPriceMultiplier = 1.3; // "getting richer = raise one multiplier"
        Assert.Equal(20, s.Plan().Row("food:grain").Change);
    }

    [Fact]
    public void Empty_base_means_food_is_not_bought_until_the_player_types_a_price()
    {
        var s = new Scenario().Party(10).Food("grain", market: 100, buy: 10, noAverage: true);
        Assert.Null(s.Plan().FindRow("food:grain"));

        s.Snap.AveragePrices["grain"] = new(10, 8);
        s.Settings.AutoFillFoodPrices = false; // average known, but not shown
        Assert.Null(s.Plan().FindRow("food:grain"));

        s.Settings.PriceBook["grain"] = new PriceBookEntry { BuyBase = 10 };
        Assert.Equal(20, s.Plan().Row("food:grain").Change);
    }

    [Fact]
    public void Surplus_is_sold_only_at_or_above_the_final_min_sell()
    {
        var s = new Scenario().Party(10).Food("grain", held: 60, sell: 8);
        s.Settings.PriceBook["grain"] = new PriceBookEntry { SellBase = 11 }; // 11 × 0.8 = 9 > 8
        Assert.Equal(0, s.Plan().Row("food:grain").Change);

        s.Settings.PriceBook["grain"] = new PriceBookEntry { SellBase = 10 }; // 10 × 0.8 = 8
        Assert.Equal(-40, s.Plan().Row("food:grain").Change);
    }

    [Fact]
    public void Empty_sell_base_sells_at_any_price()
    {
        var s = new Scenario().Party(10).Food("grain", held: 60, sell: 1, noAverage: true);
        Assert.Equal(-40, s.Plan().Row("food:grain").Change);
    }

    [Fact]
    public void Purchases_stop_at_MinGoldAfterDeal()
    {
        var plan = new Scenario().Party(10).Gold(1_105).Food("grain", market: 100, buy: 10).Plan();
        Assert.Equal(10, plan.Row("food:grain").Change);
        Assert.Equal(1_005, plan.Totals.GoldAfter);
        Assert.False(plan.Totals.BelowMinGoldAfterDeal);
    }

    [Fact]
    public void When_the_fewest_type_is_too_dear_for_the_purse_a_cheaper_one_is_bought()
    {
        var plan = new Scenario().Party(10).Gold(1_050)
            .Food("grain", held: 5, market: 100, buy: 10)
            .Food("fish", market: 100, buy: 60)
            .Plan();
        Assert.Equal(0, plan.Row("food:fish").Change);
        Assert.Equal(5, plan.Row("food:grain").Change);
    }

    [Fact]
    public void Zero_gold_buys_nothing()
    {
        var s = new Scenario().Party(10).Gold(0).Food("grain", market: 100);
        s.Settings.MinGoldAfterDeal = 0;
        var plan = s.Plan();
        Assert.Equal(0, plan.Row("food:grain").Change);
        Assert.Equal(0, plan.Totals.GoldAfter);
    }

    [Fact]
    public void Empty_market_leaves_only_the_held_rows()
    {
        var plan = new Scenario().Party(10).Food("grain", held: 5).Plan();
        var section = Assert.IsType<PlanSection>(plan.Section(PlanSectionKind.Food));
        var row = Assert.Single(section.Rows);
        Assert.Equal(0, row.Change);
        Assert.Equal(0, row.Market);
    }

    [Fact]
    public void Surplus_sale_never_exceeds_the_market_gold()
    {
        var plan = new Scenario().Party(10).Gold(100_000, marketGold: 25).Food("grain", held: 60, sell: 8).Plan();
        Assert.Equal(-3, plan.Row("food:grain").Change);
        Assert.Equal(24, plan.Totals.MarketSales);
        Assert.False(plan.Totals.ExceedsMarketGold);
    }

    [Fact]
    public void Town_prices_walk_per_category_unit_by_unit()
    {
        var s = new Scenario().Party(1).Food("grain", market: 100, buy: 100);
        s.Settings.FoodDays = 60; // 3 food for the one man
        s.Oracle.Slope = 0.001; // each unit bought (store value 100) makes the next 10% dearer
        var row = s.Plan().Row("food:grain");
        Assert.Equal(3, row.Change);
        Assert.Equal(-(100 + 110 + 120), row.GoldDelta);
        Assert.Equal(100, row.UnitPriceMin);
        Assert.Equal(120, row.UnitPriceMax);
        var buys = s.Oracle.Calls.Where(c => c.Key == "grain" && !c.Selling).Select(c => c.Delta).Distinct();
        Assert.Equal(new[] { 0, -100, -200 }, buys);
    }

    [Fact]
    public void Village_prices_are_flat()
    {
        var s = new Scenario().Village().Party(1).Food("grain", market: 100, buy: 100);
        s.Settings.FoodDays = 60;
        var row = s.Plan().Row("food:grain");
        Assert.Equal(-300, row.GoldDelta);
        Assert.Equal(row.UnitPriceMin, row.UnitPriceMax);
    }

    [Fact]
    public void Food_off_or_no_trade_access_means_no_food_section()
    {
        var s = new Scenario().Party(10).Food("grain", held: 1, market: 100);
        s.Settings.FoodEnabled = false;
        Assert.Null(s.Plan().Section(PlanSectionKind.Food));

        s.Settings.FoodEnabled = true;
        s.Snap.CanTrade = false;
        Assert.Null(s.Plan().Section(PlanSectionKind.Food));
    }

    [Fact]
    public void Food_row_carries_its_price_book_and_weight()
    {
        var plan = new Scenario().Party(10).Food("grain", market: 100, buy: 10, weight: 2).Plan();
        var row = plan.Row("food:grain");
        Assert.Equal(12, row.PriceBook!.FinalMaxBuy);
        Assert.Equal(40, row.WeightDelta);
        Assert.Equal(40, plan.Totals.WeightAdded);
        Assert.Equal(100, row.Market);
        Assert.Equal(TradeDirection.Buy, row.BuyLane!.Direction);
    }
}
