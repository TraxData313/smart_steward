using SmartSteward.Core.Execution;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// Step 30 — the noble horse max price (Anton 2026.10.01: "I want max price per noble horse to be able to be different from war
/// horse"). <c>NobleHorseMaxPrice</c> is the kept noble row's role cap with the war cap's semantics: the lower of it and the
/// price-book max, 0 = none, a goal of yours obeys it while "Your goals obey the price limits" is on. N = 0 is unchanged.
/// </summary>
public class NobleMaxPriceTests
{
    private static Scenario Rich() => new Scenario().Party(10, 0).Gold(500_000);

    [Fact]
    public void The_default_is_ten_thousand_beside_the_war_cap()
    {
        var s = new StewardSettings();
        Assert.Equal(10_000, s.NobleHorseMaxPrice);
        Assert.Equal(2_000, s.WarMountMaxPrice);
        var keys = SettingsRegistry.All.Select(x => x.Key).ToList();
        Assert.Equal(keys.IndexOf(nameof(StewardSettings.WarMountMaxPrice)) + 1,
            keys.IndexOf(nameof(StewardSettings.NobleHorseMaxPrice)));
    }

    [Fact]
    public void A_noble_horse_above_the_cap_is_not_bought_one_below_it_is()
    {
        var s = Rich()
            .Mount("noble_dear", "noble_horse", market: 5, buy: 12_000, sell: 6_000)   // price book max 14,400 — over the cap
            .Mount("noble_fair", "noble_horse", market: 1, buy: 8_000, sell: 4_000);   // max 9,600 — under it
        s.Settings.NobleHorsesToKeep = 3;
        var noble = s.Plan().Row("mounts:noble");
        Assert.Equal(1, noble.Change);                                  // only the fair one
        Assert.Equal(1, noble.Moved("noble_fair"));
        Assert.Equal(0, noble.Moved("noble_dear"));
        Assert.True(RowGoal.Of(noble).IsShort);                         // short of 3, and says why

        s.Settings.NobleHorseMaxPrice = 15_000;                         // raised: the dear ones too
        Assert.Equal(3, s.Plan().Row("mounts:noble").Change);
    }

    [Fact]
    public void The_cap_trims_the_price_book_and_zero_means_no_cap()
    {
        var s = Rich().Mount("noble_a", "noble_horse", market: 5, buy: 50_000, sell: 25_000);
        s.Settings.NobleHorsesToKeep = 2;
        Assert.Equal(0, s.Plan().Row("mounts:noble").Change);           // 50,000 > 10,000

        s.Settings.NobleHorseMaxPrice = 0;                              // no cap: the price book's 60,000 alone
        var plan = s.Plan();
        Assert.Equal(2, plan.Row("mounts:noble").Change);
        var tx = plan.Transactions.First(t => t.RowId == "mounts:noble");
        Assert.Equal(60_000, ExecutionBudget.PriceLimitOf(plan, tx));   // the executor walks by the same limit

        s.Settings.NobleHorseMaxPrice = 55_000;                         // below the price book: the cap is the limit
        plan = s.Plan();
        tx = plan.Transactions.First(t => t.RowId == "mounts:noble");
        Assert.Equal(55_000, ExecutionBudget.PriceLimitOf(plan, tx));
    }

    [Fact]
    public void The_war_and_noble_caps_are_independent()
    {
        var s = Rich()
            .Mount("charger", "war_horse", market: 5, buy: 1_500, sell: 750)
            .Mount("noble_a", "noble_horse", market: 5, buy: 3_000, sell: 1_500);
        s.Settings.WarMountsToKeep = 2;
        s.Settings.NobleHorsesToKeep = 2;
        s.Settings.AutoFillWarMountPrices = true;

        s.Settings.NobleHorseMaxPrice = 1_000;                          // the noble cap stops the noble horses only
        var plan = s.Plan();
        Assert.Equal(2, plan.Row("mounts:war").Change);
        Assert.Equal(0, plan.Row("mounts:noble").Change);

        s.Settings.NobleHorseMaxPrice = 10_000;
        s.Settings.WarMountMaxPrice = 1_000;                            // the war cap stops the war horses only
        plan = s.Plan();
        Assert.Equal(0, plan.Row("mounts:war").Change);
        Assert.Equal(2, plan.Row("mounts:noble").Change);
    }

    [Fact]
    public void With_none_kept_the_cap_changes_nothing()
    {
        var s = new Scenario().Party(10, 0)
            .Mount("noble_a", "noble_horse", held: 2, market: 5, buy: 3_000, sell: 1_500);
        Assert.Equal(0, s.Settings.NobleHorsesToKeep);
        var before = s.Plan().Row("mounts:noble");
        s.Settings.NobleHorseMaxPrice = 1;
        var after = s.Plan().Row("mounts:noble");
        Assert.False(after.KeepsNobles);
        Assert.Null(after.Market);
        Assert.Equal((-2, before.GoldDelta), (after.Change, after.GoldDelta)); // the sell-only row of step 17, the same sale
    }

    [Fact]
    public void A_typed_goal_obeys_the_cap_unless_your_goals_ignore_the_price_limits()
    {
        var s = Rich().Mount("noble_a", "noble_horse", market: 5, buy: 12_000, sell: 6_000);
        s.Settings.Goals["mounts:noble"] = 2;
        var noble = s.Plan().Row("mounts:noble");
        Assert.Equal(0, noble.Change);
        Assert.Equal(GoalShort.PriceCap, RowGoal.Of(noble).Short);

        s.Settings.ManualGoalsObeyPriceCaps = false;
        Assert.Equal(2, s.Plan().Row("mounts:noble").Change);
    }
}
