using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// The activation thresholds (PLAN step 20, Anton 2026.09.28 — round 4: "i want it in such a way that the players turn it on
/// and with the default settings they will be happy like that, as they become richer those start activating and helping
/// them"): a job acts only when the purse before the deal is at least its threshold; below it the steward neither buys nor
/// sells for it, and its rows stay for the player's hand.
/// </summary>
public class JobThresholdTests
{
    /// <summary>A scenario with the REAL round-4 defaults (the test kit's legacy defaults switch the thresholds off).</summary>
    private static Scenario Real()
    {
        var s = new Scenario();
        var d = new StewardSettings();
        s.Settings.FoodMinDenari = d.FoodMinDenari;
        s.Settings.PackAnimalsMinDenari = d.PackAnimalsMinDenari;
        s.Settings.MountsMinDenari = d.MountsMinDenari;
        s.Settings.WarHorsesMinDenari = d.WarHorsesMinDenari;
        s.Settings.WarMountsToKeep = d.WarMountsToKeep;
        return s;
    }

    [Fact]
    public void The_defaults_are_Antons_numbers()
    {
        var d = new StewardSettings();
        Assert.Equal(2_000, d.FoodMinDenari);
        Assert.Equal(2_000, d.PackAnimalsMinDenari);
        Assert.Equal(5_000, d.MountsMinDenari);
        Assert.Equal(20_000, d.WarHorsesMinDenari);
        Assert.Equal(10, d.WarMountsToKeep);
    }

    [Theory]
    [InlineData(1_999, 0)]
    [InlineData(2_000, 20)]
    public void Food_waits_below_its_threshold_then_buys(int gold, int bought)
    {
        var plan = Real().Party(10).Gold(gold).Food("grain", market: 100, buy: 10).Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(bought, grain.Change);
        Assert.Equal(gold < 2_000 ? 2_000 : (int?)null, grain.StartsAtDenari);
    }

    [Fact]
    public void A_waiting_job_does_not_sell_its_surplus_either()
    {
        var s = Real().Party(10).Gold(1_000).Food("fish", held: 100, market: 5).Pack("mule", held: 13, market: 2);
        var waiting = s.Plan();
        Assert.Equal(0, waiting.Row("food:fish").Change);
        Assert.Equal(0, waiting.Row("mounts:pack").Change);

        s.Gold(3_000);
        var acting = s.Plan();
        Assert.Equal(-80, acting.Row("food:fish").Change); // back down to the target of 20
        Assert.Equal(-3, acting.Row("mounts:pack").Change); // the pack job acts: its surplus sells
    }

    [Fact]
    public void The_floors_still_cap_an_active_job()
    {
        // 3,000 denari: pack animals act (2,000) but MinGoldForHorses (5,000) keeps them from buying — thresholds switch a
        // job on, floors cap what it spends.
        var plan = Real().Party(10).Gold(3_000).Pack("mule", held: 5, market: 20, buy: 100).Plan();
        var pack = plan.Row("mounts:pack");
        Assert.Null(pack.StartsAtDenari);
        Assert.Equal(0, pack.Change);
    }

    [Theory]
    [InlineData(4_999, 0, 0)]
    [InlineData(10_000, 11, -1)]
    public void Riding_and_noble_horses_wait_for_the_mounts_threshold(int gold, int riding, int noble)
    {
        var plan = Real().Party(10, footmen: 10).Gold(gold)
            .Mount("hunter", "horse", market: 30, buy: 200)
            .Mount("palmatian", "noble_horse", held: 1, buy: 2_000, sell: 900)
            .Plan();
        Assert.Equal(riding, plan.Row("mounts:riding").Change);
        Assert.Equal(noble, plan.Row("mounts:noble").Change);
        int? startsAt = gold < 5_000 ? 5_000 : null;
        Assert.Equal(startsAt, plan.Row("mounts:riding").StartsAtDenari);
        Assert.Equal(startsAt, plan.Row("mounts:noble").StartsAtDenari);
    }

    [Theory]
    [InlineData(19_999, 0, 22)]
    [InlineData(60_000, 10, 12)]
    public void War_horses_wait_for_20000_then_ten_are_kept(int gold, int war, int riding)
    {
        var plan = Real().Party(20, footmen: 20).Gold(gold)
            .Mount("hunter", "horse", market: 50, buy: 200)
            .Mount("charger", "war_horse", market: 20, buy: 1_500)
            .Plan();
        Assert.Equal(war, plan.Row("mounts:war").Change);
        Assert.Equal(riding, plan.Row("mounts:riding").Change); // 22 horses to keep: the war horses count among them
        Assert.Equal(gold < 20_000 ? 20_000 : (int?)null, plan.Row("mounts:war").StartsAtDenari);
    }

    [Fact]
    public void A_waiting_war_job_keeps_its_horses_and_they_still_carry_footmen()
    {
        var plan = Real().Party(10, footmen: 10).Gold(10_000)
            .Mount("hunter", "horse", held: 2, market: 30, buy: 200)
            .Mount("charger", "war_horse", held: 15, buy: 1_500)
            .Plan();
        Assert.Equal(0, plan.Row("mounts:war").Change); // 15 > 10 kept, but the war job waits: nothing sold
        // 11 horses to keep, 15 war horses held: the riding job (acting at 10,000) sells its 2 as surplus.
        Assert.Equal(-2, plan.Row("mounts:riding").Change);
    }

    [Fact]
    public void Lame_horses_stay_counted_in_their_role_row_while_its_job_waits()
    {
        var s = Real().Party(10, footmen: 10).Gold(4_000)
            .Mount("hunter", "horse", held: 3, market: 30, buy: 200, sell: 100, modifier: "lame_horse", priceFactor: Scenario.Lame);
        var plan = s.Plan();
        Assert.Null(plan.FindRow("mounts:lame"));
        Assert.Equal(3, plan.Row("mounts:riding").Mine);

        s.Gold(10_000);
        Assert.Equal(-3, s.Plan().Row("mounts:lame").Change);
    }

    [Fact]
    public void The_purse_BEFORE_the_deal_decides_so_ransom_gold_never_switches_a_job_on()
    {
        var plan = Real().Party(10).Gold(1_500).Food("grain", market: 100, buy: 10).Prisoner("looter", 10, ransom: 500).Plan();
        Assert.Equal(5_000, plan.Row("prisoner:looter").GoldDelta);
        Assert.Equal(0, plan.Row("food:grain").Change);
    }

    [Fact]
    public void The_players_hand_works_on_a_waiting_row()
    {
        var plan = Real().Party(10).Gold(1_500).Food("grain", market: 100, buy: 10).Plan();
        var result = plan.Increase("food:grain", EditSize.Five);
        Assert.Equal(5, result.After);
        Assert.Equal(2_000, plan.Row("food:grain").StartsAtDenari);
    }

    [Fact]
    public void A_live_re_plan_keeps_the_same_jobs_waiting()
    {
        var plan = Real().Party(10).Gold(1_500).Food("grain", market: 100, buy: 10).Troop("recruit", onOffer: 20, price: 10).Plan();
        plan.Increase("troops:recruit", EditSize.Five); // a party-changing edit: the steward's rows re-plan
        Assert.Equal(5, plan.Row("troops:recruit").Change);
        Assert.Equal(0, plan.Row("food:grain").Change);
        Assert.Equal(2_000, plan.Row("food:grain").StartsAtDenari);
    }

    [Fact]
    public void Zero_means_always()
    {
        var s = Real().Party(10).Gold(1_500).Food("grain", market: 100, buy: 10);
        s.Settings.FoodMinDenari = 0;
        Assert.Equal(20, s.Plan().Row("food:grain").Change);
    }

    [Fact]
    public void The_autonomous_steward_answers_to_the_thresholds_too()
    {
        var s = Real().Party(10).Gold(1_500).Food("fish", held: 100, market: 5);
        s.Settings.AutonomousMinGold = 0;
        var plan = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Equal(0, plan.Row("food:fish").Change);
    }

    [Fact]
    public void The_facts_list_the_waiting_jobs_in_order_and_leave_out_the_ones_switched_off()
    {
        var s = Real().Party(10).Gold(1_000);
        Assert.Equal(new[] { "Food:2000", "PackAnimals:2000", "Mounts:5000", "WarHorses:20000" },
            s.Plan().Facts.Waiting.Select(p => p.Key + ":" + p.Value));

        s.Gold(6_000);
        var plan = s.Plan();
        Assert.Equal(new[] { "WarHorses:20000" }, plan.Facts.Waiting.Select(p => p.Key + ":" + p.Value));
        Assert.Equal(20_000, plan.Facts.StartsAt(ManagedJob.WarHorses));
        Assert.Null(plan.Facts.StartsAt(ManagedJob.Food));

        s.Gold(1_000);
        s.Settings.FoodEnabled = false;
        Assert.DoesNotContain(s.Plan().Facts.Waiting, p => p.Key == ManagedJob.Food);
    }
}
