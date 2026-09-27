using SmartSteward.Core.Planning;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>The money chain (DESIGN §3), the plan's shape and its header/footer numbers.</summary>
public class StewardPlanTests
{
    [Fact]
    public void Ransom_gold_funds_the_food()
    {
        var plan = new Scenario().Party(10).Gold(1_000)
            .Prisoner("looter", 10, 20)
            .Food("grain", market: 100, buy: 10)
            .Plan();
        Assert.Equal(20, plan.Row("food:grain").Change); // 200 of ransom, 200 of grain
        Assert.Equal(1_000, plan.Totals.GoldAfter);
        Assert.Equal(200, plan.Totals.Earned);
        Assert.Equal(200, plan.Totals.Spent);
        Assert.Equal(0, plan.Totals.GoldChange);
    }

    [Fact]
    public void Surplus_sales_fund_the_buys_of_another_kind()
    {
        var s = new Scenario().Party(10, footmen: 1).Gold(5_000)
            .Pack("mule", held: 12, sell: 100)
            .Mount("hunter", "horse", market: 5, buy: 200);
        var plan = s.Plan();
        Assert.Equal(-2, plan.Row("mounts:pack").Change);   // +200
        Assert.Equal(1, plan.Row("mounts:riding").Change);  // ceil(1.1) = 2 wanted, 200 affordable
        Assert.Equal(5_000, plan.Totals.GoldAfter);
    }

    [Fact]
    public void Buys_go_food_then_pack_then_mounts_then_upgrade_horses()
    {
        var plan = new Scenario().Party(10, footmen: 10).Gold(6_000)
            .Upgrade("recruit", 10, ("war_horse", 2))
            .Food("grain", market: 100, buy: 10)
            .Pack("mule", market: 20, buy: 150)
            .Mount("hunter", "horse", market: 20, buy: 200)
            .Mount("charger", "war_horse", market: 5, buy: 1500)
            .Plan();
        Assert.Equal(20, plan.Row("food:grain").Change);                // 200 → 5,800
        Assert.Equal(5, plan.Row("mounts:pack").Change);                 // 750 → 5,050
        Assert.Equal(0, plan.Row("mounts:riding").Change);
        Assert.Equal(0, plan.Row("mounts:upgrade:war_horse").Change);
        Assert.Equal(5_050, plan.Totals.GoldAfter);
    }

    [Fact]
    public void Food_answers_only_to_MinGoldAfterDeal()
    {
        var plan = new Scenario().Party(10).Gold(3_000)
            .Food("grain", market: 100, buy: 10)
            .Pack("mule", market: 20, buy: 150)
            .Plan();
        Assert.Equal(20, plan.Row("food:grain").Change);
        Assert.Equal(0, plan.Row("mounts:pack").Change);
    }

    [Fact]
    public void The_steward_never_breaches_its_own_floors_or_the_market_purse()
    {
        var plan = BusyTown().Plan();
        Assert.True(plan.HasChanges);
        Assert.False(plan.Totals.BelowMinGoldAfterDeal);
        Assert.False(plan.Totals.BelowMinGoldForHorses);
        Assert.False(plan.Totals.CannotAfford);
        Assert.False(plan.Totals.ExceedsMarketGold);
    }

    [Fact]
    public void Floor_flags_turn_on_when_the_purse_ends_below_a_floor()
    {
        var s = new Scenario().Party(10).Gold(20_000)
            .Food("grain", market: 100, buy: 10)
            .Pack("mule", market: 20, buy: 150);
        var plan = s.Plan();
        var stricter = new StewardSettings { MinGoldAfterDeal = 25_000, MinGoldForHorses = 30_000 };
        var totals = PlanTotals.Compute(plan.Rows, s.Snap, stricter);
        Assert.True(totals.BelowMinGoldAfterDeal);
        Assert.True(totals.BelowMinGoldForHorses);

        // Only food bought → the horse floor does not apply.
        var foodOnly = new Scenario().Party(10).Gold(20_000).Food("grain", market: 100, buy: 10);
        totals = PlanTotals.Compute(foodOnly.Plan().Rows, foodOnly.Snap, stricter);
        Assert.True(totals.BelowMinGoldAfterDeal);
        Assert.False(totals.BelowMinGoldForHorses);
    }

    [Fact]
    public void A_purse_already_below_the_floor_is_not_a_breach_when_nothing_is_bought()
    {
        var plan = new Scenario().Party(10).Gold(500).Food("grain", held: 60, sell: 8).Plan();
        Assert.Equal(-40, plan.Row("food:grain").Change);
        Assert.False(plan.Totals.BelowMinGoldAfterDeal);
    }

    [Fact]
    public void Sections_come_in_the_design_order_and_empty_ones_are_left_out()
    {
        var plan = BusyTown().Plan();
        Assert.Equal(
            new[] { PlanSectionKind.Tavern, PlanSectionKind.Food, PlanSectionKind.Mounts,
                PlanSectionKind.ArmourAndWeapons, PlanSectionKind.Prisoners },
            plan.Sections.Select(x => x.Kind));
        Assert.Equal(new[] { "mounts:pack", "mounts:riding", "mounts:upgrade:war_horse" },
            plan.Section(PlanSectionKind.Mounts)!.Rows.Select(r => r.Id));

        var bare = new Scenario().Plan();
        Assert.Equal(new[] { PlanSectionKind.Mounts }, bare.Sections.Select(x => x.Kind)); // the pack target shows
    }

    [Fact]
    public void The_plan_is_deterministic_whatever_the_input_order()
    {
        var a = BusyTown();
        var b = BusyTown();
        b.Snap.Inventory.Reverse();
        b.Snap.Market.Reverse();
        b.Snap.Prisoners.Reverse();
        b.Snap.Upgrades.Reverse();
        b.Snap.Tavern!.Wanderers.Reverse();
        Assert.Equal(Describe(a.Plan()), Describe(b.Plan()));
        Assert.Equal(Describe(a.Plan()), Describe(a.Plan()));
    }

    [Fact]
    public void Suggested_change_is_the_proposal_and_rows_keep_their_walk_data()
    {
        var plan = BusyTown().Plan();
        foreach (var row in plan.Rows)
            Assert.Equal(row.SuggestedChange, row.Change);
        var riding = plan.Row("mounts:riding");
        Assert.NotNull(riding.BuyLane);
        Assert.NotNull(riding.SellLane);
        Assert.Equal(TradeDirection.Buy, riding.BuyLane!.Direction);
        Assert.Equal(LanePick.Cheapest, riding.BuyLane.Pick);
        Assert.Equal(LanePick.MostExpensive, riding.SellLane!.Pick);
        Assert.Equal(LanePick.InOrder, plan.Row("loot:Armour").SellLane!.Pick);
    }

    [Fact]
    public void Walking_a_row_lane_again_reproduces_its_price()
    {
        // The re-pricing contract for PLAN step 4b: the same lane, the same order, the same prices.
        var s = new Scenario().Party(10, footmen: 10)
            .Mount("hunter", "horse", market: 5, buy: 200)
            .Mount("aserai_horse", "horse", market: 20, buy: 250);
        s.Oracle.Slope = 0.0002;
        var plan = s.Plan();
        var row = plan.Row("mounts:riding");

        var market = new MarketState(s.Oracle, s.Snap.MarketGold);
        var cursor = new LaneCursor(row.BuyLane!);
        int total = 0;
        for (int i = 0; i < row.Change; i++)
        {
            var quote = cursor.Peek(market)!.Value;
            cursor.Take(quote, market);
            total += quote.Price;
        }
        Assert.Equal(-row.GoldDelta, total);
    }

    [Fact]
    public void Transactions_list_every_sale_before_any_buy()
    {
        var plan = BusyTown().Plan();
        var trades = plan.Transactions.Where(t => t.IsItemTrade).ToList();
        Assert.NotEmpty(trades);
        int firstBuy = trades.FindIndex(t => t.Kind == TransactionKind.Buy);
        Assert.True(firstBuy > 0);
        Assert.All(trades.Skip(firstBuy), t => Assert.Equal(TransactionKind.Buy, t.Kind));
        Assert.Equal(plan.Totals.MarketSales, trades.Where(t => t.Kind == TransactionKind.Sell).Sum(t => t.Gold));
    }

    [Fact]
    public void Food_days_follow_the_game_and_the_eaters_the_deal_leaves()
    {
        var s = new Scenario().Party(10).Food("grain", held: 30).Prisoner("looter", 10, 20);
        s.Settings.SellFoodSurplus = false;
        s.Snap.Party.DailyFoodUse = 0.75;   // (10 + 10/2) / 20
        s.Snap.Party.LivestockFoodUnits = 6;
        var totals = s.Plan().Totals;
        Assert.Equal(30, totals.FoodUnitsNow);
        Assert.Equal(30, totals.FoodUnitsAfter);
        Assert.Equal(36 / 0.75, totals.FoodDaysNow!.Value, 6);
        Assert.Equal(36 / 0.5, totals.FoodDaysAfter!.Value, 6); // the ransomed prisoners stop eating
        Assert.Equal(0, totals.PrisonersAfter);
    }

    [Fact]
    public void Mod_disabled_gives_an_empty_plan()
    {
        var s = BusyTown();
        s.Settings.ModEnabled = false;
        var plan = s.Plan();
        Assert.Empty(plan.Sections);
        Assert.False(plan.HasChanges);
        Assert.Equal(plan.Totals.GoldNow, plan.Totals.GoldAfter);
    }

    [Fact]
    public void Without_trade_access_only_the_tavern_and_prisoners_remain()
    {
        var s = BusyTown();
        s.Snap.CanTrade = false;
        Assert.Equal(new[] { PlanSectionKind.Tavern, PlanSectionKind.Prisoners },
            s.Plan().Sections.Select(x => x.Kind));
    }

    [Fact]
    public void A_snapshot_with_missing_lists_still_plans()
    {
        var snapshot = new StewardSnapshot
        {
            Inventory = null!, Market = null!, Prisoners = null!, Upgrades = null!, AveragePrices = null!,
            Prison = null!, Party = null!, PlayerGold = 1_000,
        };
        var plan = StewardPlanner.Plan(snapshot, new StewardSettings { PrisonersExcluded = null!, PriceBook = null! },
            new FakeOracle());
        Assert.False(plan.HasChanges);
        Assert.Equal(1_000, plan.Totals.GoldAfter);

        var s = new Scenario().Party(10).Food("grain", market: 100);
        s.Settings.PriceBook = null!; // a settings file without a price book = all defaults
        Assert.Equal(20, s.Plan().Row("food:grain").Change);
    }

    private static Scenario BusyTown() => Scenario.BusyTown();

    private static string Describe(StewardPlan plan) => plan.Describe();
}
