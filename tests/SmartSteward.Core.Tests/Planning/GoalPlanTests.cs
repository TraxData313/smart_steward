using SmartSteward.Core.Execution;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// Round 5 (DESIGN §1.1 "THE GOAL" — Anton 2026.09.28): the manual goals of food and the pack / riding / war horse rows are
/// STANDING ORDERS planned first, the policy fills the rest; a click is a goal edit; ⟲ gives the row back; "Goals you set by
/// hand" — the thresholds, the purse floors and the price limits; the Goal cell of every row.
/// </summary>
public class GoalPlanTests
{
    /// <summary>10 men at vanilla's rate: 40 days = a target of 20 food.</summary>
    private static Scenario TwoFoods() =>
        new Scenario().Party(10).Food("grain", market: 100, buy: 10).Food("fish", market: 100, buy: 12);

    private static void Save(StewardPlan plan, Scenario s)
    {
        foreach (var edit in plan.TakeGoalEdits())
            edit.ApplyTo(s.Settings);
    }

    // ── Planned first, the policy fills the rest ──────────────────────────────────────────────────────

    [Fact]
    public void A_standing_food_goal_is_planned_first_and_the_steward_fills_the_rest()
    {
        var s = TwoFoods();
        s.Settings.Goals["food:grain"] = 8;            // below the even share (20 / 2 kinds = 10): it counts fully (step 25)
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(8, grain.Change);
        Assert.True(grain.IsTouched);                  // yours: the ⟲ shows
        Assert.Equal(8, grain.ManualGoal);
        Assert.Equal(12, plan.Row("food:fish").Change); // the days goal (20) minus your 8
        Assert.False(plan.Row("food:fish").IsTouched);

        var cell = RowGoal.Of(grain);
        Assert.Equal(8, cell.Value);
        Assert.True(cell.IsYours && cell.Editable && !cell.IsShort && !cell.HandsOff);
        var fish = RowGoal.Of(plan.Row("food:fish"));
        Assert.Equal(12, fish.Value);                   // the steward's goal: its share of the days goal
        Assert.True(fish.Editable && !fish.IsYours);
    }

    [Fact]
    public void A_goal_past_the_even_share_is_a_stockpile_on_top_and_the_stewards_food_keeps_its_share()
    {
        // Step 25 (Anton 2026.10.01): your goal counts toward the target (20) only up to the even share (10); the 20 above it
        // are on top. The fish (20 held) is surplus down to its own share — never drained.
        var s = new Scenario().Party(10).Food("grain", market: 100, buy: 10).Food("fish", held: 20, sell: 10);
        s.Settings.Goals["food:grain"] = 30;
        var plan = s.Plan();
        Assert.Equal(10, plan.Facts.FoodShare);
        Assert.Equal(30, plan.Row("food:grain").Change);
        Assert.Equal(-10, plan.Row("food:fish").Change); // 10 counted + 20 > 25: sold down to the target — the fish keeps 10
    }

    [Fact]
    public void A_quest_hoard_never_drains_the_other_kinds()
    {
        // Anton's playtest (docs/feedback/2026-10-01-grain-hoard.png): 9 kinds, a target of 235 (40 days, 10% tolerance),
        // grain typed at 120 — the steward sold ALL the fish and meat. Now grain counts 27 (ceil 235 / 9), the 8 others share
        // the rest: nothing is sold, the market's little butter and cheese are bought.
        var s = new Scenario().Party(94).Gold(200_000)
            .Food("beer", held: 19).Food("butter", held: 19, market: 1, buy: 10).Food("cheese", held: 19, market: 2, buy: 10)
            .Food("date_fruit", held: 19).Food("fish", held: 24, sell: 7).Food("grain", held: 128, market: 17, buy: 5, sell: 5)
            .Food("grapes", held: 20).Food("meat", held: 24, sell: 16).Food("olives", held: 19);
        foreach (var unsold in new[] { "beer", "butter", "cheese", "date_fruit", "grapes", "olives" })
            s.Settings.PriceBook[unsold] = new PriceBookEntry { Sell = false };
        s.Snap.Party.DailyFoodUse = 94 * 0.0625; // 40 days x 94 eaters = 235, the screenshot's target
        s.Settings.FoodSurplusTolerancePercent = 10;
        s.Settings.Goals["food:grain"] = 120;
        var plan = s.Plan();
        Assert.Equal(235, plan.Facts.FoodTarget);
        Assert.Equal(27, plan.Facts.FoodShare);
        Assert.Equal(-8, plan.Row("food:grain").Change);  // your goal: 128 → 120 (your sale is not guarded)
        Assert.Equal(0, plan.Row("food:fish").Change);
        Assert.Equal(0, plan.Row("food:meat").Change);
        Assert.Equal(1, plan.Row("food:butter").Change);
        Assert.Equal(2, plan.Row("food:cheese").Change);
        Assert.Equal(9, plan.Rows.Count(r => r.Type == RowType.Food && r.Result > 0)); // every kind kept

        LivePlanTests.ApplyDoIt(s, plan);
        foreach (var row in s.Plan().Rows.Where(r => r.Type == RowType.Food))
            Assert.True(row.Change == 0, row.Id + " " + row.Change);  // Do it leaves nothing new to suggest
    }

    [Fact]
    public void Without_a_goal_the_surplus_sale_stops_at_the_even_share()
    {
        // The variety guard (step 25): target 20 over 2 kinds = 10 each; 60 grain held but only the fish may be sold — the fish
        // goes down to its share and no further, though the total stays above the target.
        var s = new Scenario().Party(10).Food("grain", held: 60).Food("fish", held: 30, sell: 10);
        s.Settings.PriceBook["grain"] = new PriceBookEntry { Sell = false };
        var plan = s.Plan();
        Assert.Equal(10, plan.Facts.FoodShare);
        Assert.Equal(0, plan.Row("food:grain").Change);
        Assert.Equal(-20, plan.Row("food:fish").Change);  // 30 → 10, never 0

        LivePlanTests.ApplyDoIt(s, plan);
        Assert.Equal(0, s.Plan().Row("food:fish").Change); // still above the target: the guard holds the same line again
    }

    [Fact]
    public void A_goal_below_the_share_counts_fully_and_one_above_counts_only_the_share()
    {
        var s = new Scenario().Party(20).Food("grain", market: 200, buy: 10).Food("fish", market: 200, buy: 10)
            .Food("cheese", market: 200, buy: 10).Food("olives", market: 200, buy: 10); // target 40, share 10
        s.Settings.Goals["food:grain"] = 4;
        var plan = s.Plan();
        Assert.Equal(10, plan.Facts.FoodShare);
        Assert.Equal(40, plan.Totals.FoodUnitsAfter);     // 4 counted fully: the steward's three kinds share 36
        Assert.Equal(36, plan.Rows.Where(r => r.Type == RowType.Food && r.ItemId != "grain").Sum(r => r.Result));

        s.Settings.Goals["food:grain"] = 50;
        plan = s.Plan();
        Assert.Equal(80, plan.Totals.FoodUnitsAfter);     // 10 counted + 40 on top; the three others share 30
        Assert.Equal(30, plan.Rows.Where(r => r.Type == RowType.Food && r.ItemId != "grain").Sum(r => r.Result));
    }

    [Fact]
    public void A_goal_holds_over_a_live_re_plan_and_in_a_fresh_plan_from_the_settings()
    {
        var s = TwoFoods();
        s.Snap.Tavern = new TavernInfo { Mercenaries = new MercenaryOffer { TroopId = "merc", Name = "Spears", Available = 10, PricePerMan = 50 } };
        var plan = s.Plan();
        Assert.Equal(15, plan.SetChange("food:grain", 15).After);
        Save(plan, s);
        Assert.Equal(15, s.Settings.Goals["food:grain"]);

        plan.Increase("tavern:mercenaries", EditSize.All);   // 20 eaters: the target doubles to 40
        Assert.Equal(15, plan.Row("food:grain").Change);     // your goal stays
        Assert.Equal(25, plan.Row("food:fish").Change);      // the steward fills the rest

        var fresh = s.Plan();                                 // another town, another day: the goal is still there
        Assert.Equal(15, fresh.Row("food:grain").Change);
        Assert.True(fresh.Row("food:grain").IsTouched);
        Assert.Empty(fresh.TakeGoalEdits());                  // nothing to save: it came from the settings
    }

    [Fact]
    public void The_promise_holds_with_goals_do_it_and_nothing_new_is_suggested()
    {
        var s = TwoFoods().Pack("mule", held: 3, market: 20, buy: 140);
        s.Settings.Goals["food:grain"] = 12;
        s.Settings.Goals["mounts:pack"] = 6;
        var plan = s.Plan();
        Assert.Equal(3, plan.Row("mounts:pack").Change);
        LivePlanTests.ApplyDoIt(s, plan);
        var fresh = s.Plan();
        foreach (var row in fresh.Rows.Where(r => r.Type is RowType.Food or RowType.Pack))
            Assert.True(row.Change == 0, row.Id + " " + row.Change);
        Assert.Equal(12, RowGoal.Of(fresh.Row("food:grain")).Value);
    }

    [Fact]
    public void A_fresh_plan_leaves_the_market_gold_a_later_goal_sells_into()
    {
        // The planner's second pass (round 5): the steward's food surplus leaves the village's denari to your mules' sale.
        var s = new Scenario().Village().Party(10).Gold(100_000, marketGold: 1_000)
            .Food("grain", held: 200, sell: 8)
            .Pack("mule", held: 5, sell: 150);
        s.Settings.Goals["mounts:pack"] = 0;
        var plan = s.Plan();
        Assert.Equal(-5, plan.Row("mounts:pack").Change);            // 750
        Assert.Equal(-31, plan.Row("food:grain").Change);            // 248 of the 250 left
        Assert.False(plan.Totals.ExceedsMarketGold);
    }

    // ── A click is a goal edit; ⟲ and Reset all ───────────────────────────────────────────────────────

    [Fact]
    public void A_click_is_a_goal_edit_saved_once_and_reset_gives_the_row_back_to_the_policy()
    {
        var s = new Scenario().Party(10).Pack("mule", held: 3, market: 20, buy: 140);
        var plan = s.Plan();
        Assert.Equal(7, plan.Row("mounts:pack").Change);             // PackAnimalsTarget 10
        Assert.Equal(10, RowGoal.Of(plan.Row("mounts:pack")).Value);  // the steward's goal: the target

        var click = plan.Increase("mounts:pack");
        Assert.Equal(8, click.After);
        Assert.Equal(11, click.Goal);
        Assert.Equal(11, plan.Row("mounts:pack").ManualGoal);
        var edit = Assert.Single(plan.TakeGoalEdits());
        Assert.Equal("mounts:pack = 11", edit.ToString());
        edit.ApplyTo(s.Settings);
        Assert.Equal(8, s.Plan().Row("mounts:pack").Change);         // the next town: still 11

        var reset = plan.Reset("mounts:pack");
        Assert.Equal(7, reset.After);
        Assert.Null(plan.Row("mounts:pack").ManualGoal);
        Assert.False(plan.Row("mounts:pack").IsTouched);
        var back = Assert.Single(plan.TakeGoalEdits());
        Assert.Null(back.Goal);
        back.ApplyTo(s.Settings);
        Assert.Empty(s.Settings.Goals);
    }

    [Fact]
    public void Shift_stops_at_what_is_held_and_a_plain_click_crosses_it()
    {
        var s = new Scenario().Party(10).Pack("mule", held: 12, market: 20, buy: 140, sell: 70);
        var plan = s.Plan();
        Assert.Equal(-2, plan.Row("mounts:pack").Change);            // the surplus above 10
        Assert.Equal(0, plan.Increase("mounts:pack", EditSize.Five).After); // stops at Mine: goal 12
        Assert.Equal(12, plan.Row("mounts:pack").ManualGoal);
        Assert.Equal(1, plan.Increase("mounts:pack").After);         // crosses: a goal of 13
        Assert.Equal(13, plan.Row("mounts:pack").ManualGoal);
    }

    [Fact]
    public void A_typed_goal_is_kept_as_typed_even_when_the_market_cannot_reach_it()
    {
        var s = new Scenario().Party(10).Pack("mule", held: 3, market: 20, buy: 140);
        var plan = s.Plan();
        var typed = plan.SetGoal("mounts:pack", 40);
        Assert.Equal(20, typed.After);                                // all the market has
        Assert.Equal(40, typed.Goal);
        Assert.Equal(GoalShort.MarketStock, typed.GoalShort);
        var cell = RowGoal.Of(plan.Row("mounts:pack"));
        Assert.Equal(40, cell.Value);
        Assert.Equal(GoalShort.MarketStock, cell.Short);
        Assert.Equal(EditBlock.AllOnOffer, plan.Row("mounts:pack").IncreaseBlock);

        Assert.Equal(ManualGoals.MaxGoal, plan.SetGoal("mounts:pack", 1_000_000).Goal); // clamped
        Assert.Throws<ArgumentException>(() => plan.SetGoal("mounts:noble", 3));
        Assert.Equal(2, plan.TakeGoalEdits().Count);
        plan.SetGoal("mounts:pack", ManualGoals.MaxGoal);
        Assert.Empty(plan.TakeGoalEdits());                           // the same goal again: nothing new to save
    }

    [Fact]
    public void Reset_all_keeps_the_standing_goals_and_hands_back_the_visits_edits()
    {
        var s = Scenario.BusyTown();
        var plan = s.Plan();
        plan.SetGoal("food:grain", 30);
        plan.Increase("loot:MeleeWeapons");
        Assert.True(plan.Row("loot:MeleeWeapons").IsTouched);
        plan.ResetAll();
        Assert.False(plan.Row("loot:MeleeWeapons").IsTouched);
        Assert.Equal(30, plan.Row("food:grain").ManualGoal);          // [Claude's call]: a goal goes only by its own ⟲
        Assert.Equal(25, plan.Row("food:grain").Change);
        Assert.True(plan.IsEdited);
    }

    // ── Goals you set by hand: the purse floors ───────────────────────────────────────────────────────

    [Fact]
    public void Goals_keep_the_purse_floor_by_default_and_the_button_says_so()
    {
        var s = new Scenario().Party(10).Gold(1_500).Food("grain", market: 100, buy: 10);
        s.Settings.ManualGoalsKeepPurseFloor = true; // the real default (the test kit keeps the pre-round-5 way)
        s.Settings.Goals["food:grain"] = 100;
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(50, grain.Change);                               // 500 above MinGoldAfterDeal
        Assert.Equal(1_000, plan.Totals.GoldAfter);
        Assert.False(plan.Totals.BelowMinGoldAfterDeal);
        Assert.Equal(GoalShort.PurseFloor, RowGoal.Of(grain).Short);
        Assert.Equal(EditBlock.PurseFloor, grain.IncreaseBlock);
        Assert.Equal(EditBlock.None, grain.DecreaseBlock);

        s.Settings.ManualGoalsKeepPurseFloor = false;                  // the old way: flagged, never blocked
        plan = s.Plan();
        Assert.Equal(100, plan.Row("food:grain").Change);
        Assert.True(plan.Totals.BelowMinGoldAfterDeal);
    }

    [Fact]
    public void Animal_goals_keep_the_higher_animal_floor()
    {
        var s = new Scenario().Party(10).Gold(10_000).Mount("charger", "war_horse", market: 10, buy: 1_500);
        s.Settings.ManualGoalsKeepPurseFloor = true;
        s.Settings.Goals["mounts:war"] = 5;
        var plan = s.Plan();
        var war = plan.Row("mounts:war");
        Assert.Equal(3, war.Change);                                  // 4,500 of the 5,000 above MinGoldForHorses
        Assert.Equal(GoalShort.PurseFloor, RowGoal.Of(war).Short);
        Assert.Equal(EditBlock.PurseFloor, war.IncreaseBlock);
        Assert.False(plan.Totals.BelowMinGoldForHorses);
    }

    [Fact]
    public void A_click_past_the_floor_greys_the_button_instead_of_moving()
    {
        var s = new Scenario().Party(10).Gold(1_300).Food("grain", market: 100, buy: 10);
        s.Settings.ManualGoalsKeepPurseFloor = true;
        var plan = s.Plan();
        Assert.Equal(20, plan.Row("food:grain").Change);              // the steward: 200 of the 300 above the floor
        var all = plan.Increase("food:grain", EditSize.All);
        Assert.Equal(30, all.After);                                  // 300 above the floor
        Assert.Equal(EditBlock.PurseFloor, all.Block);
        Assert.Equal(30, plan.Row("food:grain").ManualGoal);          // the goal is the Result it got
        Assert.Equal(1_000, plan.Totals.GoldAfter);
    }

    // ── Goals you set by hand: the price limits ───────────────────────────────────────────────────────

    [Fact]
    public void Goals_obey_the_price_limits_by_default_and_buy_at_any_price_when_told()
    {
        var s = new Scenario().Party(10).Food("grain", market: 100, buy: 10);
        s.Settings.PriceBook["grain"] = new PriceBookEntry { BuyBase = 5 }; // max 6 — grain costs 10
        s.Settings.Goals["food:grain"] = 10;
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(0, grain.Change);
        Assert.Equal(GoalShort.PriceCap, RowGoal.Of(grain).Short);

        s.Settings.ManualGoalsObeyPriceCaps = false;
        plan = s.Plan();
        grain = plan.Row("food:grain");
        Assert.Equal(10, grain.Change);
        Assert.False(RowGoal.Of(grain).IsShort);
        var tx = plan.Transactions.Single(t => t.RowId == "food:grain");
        Assert.Null(ExecutionBudget.PriceLimitOf(plan, tx));          // the executor judges it by the goal's own lane
    }

    [Fact]
    public void Without_the_price_limits_a_goal_buys_a_food_nobody_priced_but_never_one_unticked()
    {
        var s = new Scenario().Party(10).Food("salt_fish", market: 50, buy: 20, noAverage: true)
            .Food("grain", market: 100, buy: 10);
        s.Settings.Goals["food:salt_fish"] = 8;
        var plan = s.Plan();
        Assert.Equal(0, plan.Row("food:salt_fish").Change);           // no price to judge it by
        Assert.Equal(GoalShort.NoneEligible, RowGoal.Of(plan.Row("food:salt_fish")).Short);

        s.Settings.ManualGoalsObeyPriceCaps = false;
        Assert.Equal(8, s.Plan().Row("food:salt_fish").Change);
        s.Settings.PriceBook["salt_fish"] = new PriceBookEntry { Buy = false };
        Assert.Equal(0, s.Plan().Row("food:salt_fish").Change);       // the tick still holds
    }

    [Fact]
    public void A_goal_below_what_is_held_sells_at_its_min_price_or_says_why_not()
    {
        var s = new Scenario().Party(10).Food("grain", held: 50, sell: 8);
        s.Settings.Goals["food:grain"] = 20;
        Assert.Equal(-30, s.Plan().Row("food:grain").Change);
        s.Settings.PriceBook["grain"] = new PriceBookEntry { SellBase = 20 }; // min 16 — grain fetches 8
        var grain = s.Plan().Row("food:grain");
        Assert.Equal(0, grain.Change);
        Assert.Equal(GoalShort.MinSellPrice, RowGoal.Of(grain).Short);
    }

    // ── Goals you set by hand: the thresholds, and hands-off ──────────────────────────────────────────

    [Fact]
    public void Below_the_threshold_the_stewards_rows_are_hands_off_and_your_goal_still_acts()
    {
        var s = TwoFoods().Gold(1_450);
        s.Settings.FoodMinDenari = 2_000;
        var plan = s.Plan();
        var fish = RowGoal.Of(plan.Row("food:fish"));
        Assert.True(fish.HandsOff);                                   // "–*": not managed yet
        Assert.Null(fish.Value);
        Assert.Equal(2_000, fish.StartsAt);
        Assert.True(fish.Editable);                                   // type a goal to order it anyway

        s.Settings.Goals["food:grain"] = 10;
        plan = s.Plan();
        Assert.Equal(10, plan.Row("food:grain").Change);              // ManualGoalsWaitForThresholds is off: your order
        Assert.Equal(0, plan.Row("food:fish").Change);                // the steward still waits
        Assert.False(RowGoal.Of(plan.Row("food:grain")).HandsOff);
    }

    [Fact]
    public void With_the_switch_on_your_goal_waits_too_and_a_typed_goal_is_still_kept()
    {
        var s = TwoFoods().Gold(1_450);
        s.Settings.FoodMinDenari = 2_000;
        s.Settings.ManualGoalsWaitForThresholds = true;
        s.Settings.Goals["food:grain"] = 10;
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(0, grain.Change);
        Assert.True(grain.GoalWaits);
        Assert.Equal(GoalShort.Threshold, RowGoal.Of(grain).Short);
        Assert.Equal(EditBlock.WaitsForThreshold, grain.IncreaseBlock);
        Assert.Equal(EditBlock.WaitsForThreshold, plan.Row("food:fish").IncreaseBlock);

        var typed = plan.SetGoal("food:grain", 12);
        Assert.Equal(12, typed.Goal);
        Assert.Equal(0, typed.After);
        Assert.Equal(12, plan.Goals["food:grain"]);
        Assert.Single(plan.TakeGoalEdits());
    }

    // ── Horses ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_riding_goal_is_a_plain_number_and_a_war_goal_counts_among_the_horses()
    {
        var s = new Scenario().Party(30, footmen: 30)
            .Mount("hunter", "horse", market: 80, buy: 200)
            .Mount("charger", "war_horse", market: 10, buy: 1_500);
        s.Settings.Goals["mounts:war"] = 5;
        var plan = s.Plan();
        Assert.Equal(33, plan.Facts.MountTarget);                     // 30 footmen × 110 / 100
        Assert.Equal(5, plan.Row("mounts:war").Change);
        Assert.Equal(28, plan.Row("mounts:riding").Change);           // the steward's riding horses fill the rest
        Assert.Equal(28, RowGoal.Of(plan.Row("mounts:riding")).Value);

        s.Settings.Goals["mounts:riding"] = 10;
        plan = s.Plan();
        Assert.Equal(10, plan.Row("mounts:riding").Change);           // your number, whatever the footmen
        Assert.Equal(5, plan.Row("mounts:war").Change);
    }

    // ── The Goal of the other rows ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Prisoners_other_lines_tavern_and_troops_show_the_rules_goal()
    {
        var plan = Scenario.BusyTown().Plan();
        var looter = RowGoal.Of(plan.Row("prisoner:looter"));           // PrisonerAction Ransom
        Assert.Equal(0, looter.Value);
        Assert.False(looter.Editable);
        var lord = RowGoal.Of(plan.Row("prisoner:lord_x"));             // LordPrisonerAction Keep
        Assert.Equal(1, lord.Value);
        Assert.False(lord.IsShort);
        Assert.Equal(0, RowGoal.Of(plan.Row("loot:Armour")).Value);
        Assert.False(RowGoal.Of(plan.Row("loot:Armour")).Editable);
        Assert.Null(RowGoal.Of(plan.Row("tavern:wanderer:w1")).Value);
        Assert.Null(RowGoal.Of(plan.Row("tavern:mercenaries")).Value);
    }

    [Fact]
    public void A_lord_to_donate_where_the_game_forbids_it_is_short_of_his_goal_and_says_so()
    {
        var s = new Scenario().Party(10).Prisoner("lord_x", 1, 3_000, hero: true);
        s.Settings.LordPrisonerAction = PrisonerChoice.Donate; // not allowed here: a lord to donate is kept
        var lord = RowGoal.Of(s.Plan().Row("prisoner:lord_x"));
        Assert.Equal(0, lord.Value);
        Assert.Equal(GoalShort.NotPossibleHere, lord.Short);
    }

    [Fact]
    public void Noble_and_lame_horses_aim_at_zero_and_a_lock_says_why_one_stays()
    {
        var s = new Scenario().Party(10)
            .Mount("noble_a", "noble_horse", held: 1, sell: 2_000, locked: true)
            .Mount("hunter", "horse", held: 2, buy: 40, sell: 12, modifier: "lame_horse", priceFactor: Scenario.Lame);
        var plan = s.Plan();
        var noble = RowGoal.Of(plan.Row("mounts:noble"));
        Assert.Equal(0, noble.Value);
        Assert.False(noble.Editable);
        Assert.Equal(GoalShort.NothingToSell, noble.Short);           // locked: always kept
        var lame = RowGoal.Of(plan.Row("mounts:lame"));
        Assert.Equal(0, lame.Value);
        Assert.False(lame.IsShort);

        s.Settings.MountsMinDenari = 1_000_000;                        // the riding job waits: the noble row is hands-off
        Assert.True(RowGoal.Of(s.Plan().Row("mounts:noble")).HandsOff);
    }

    [Fact]
    public void Selling_the_surplus_switched_off_is_why_a_row_stays_above_its_goal()
    {
        var s = new Scenario().Party(10).Pack("mule", held: 14, sell: 70);
        s.Settings.SellPackAnimalSurplus = false;
        var pack = RowGoal.Of(s.Plan().Row("mounts:pack"));
        Assert.Equal(10, pack.Value);
        Assert.Equal(GoalShort.SurplusKept, pack.Short);
    }
}
