using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>A re-plan keeps the player's hand (PLAN step 7): edited rows carry over, clamped by the new plan. Since round 5 a
/// food or pack / riding / war row's hand is its GOAL, which the settings carry (the window saves it) — not the carry-over.</summary>
public class PlanCarryOverTests
{
    [Fact]
    public void SetChange_goes_as_far_as_the_row_allows()
    {
        var plan = new Scenario().Village().Party(10).Food("grain", held: 5, market: 100, buy: 10).Plan();
        Assert.Equal(40, plan.SetChange("food:grain", 40).After);
        Assert.Equal(-5, plan.SetChange("food:grain", -9).After); // clamped to what is held, crossing zero
        Assert.Equal(100, plan.SetChange("food:grain", 500).After); // clamped to the market
        Assert.Equal(15, plan.SetChange("food:grain", 15).After);
        Assert.True(plan.Row("food:grain").IsTouched); // the steward's number again, but by the player's hand
        Assert.Equal(20, plan.Row("food:grain").ManualGoal); // round 5: the hand on a food row is its goal (5 held + 15)
    }

    [Fact]
    public void Only_edited_rows_are_captured_in_plan_order_and_goals_go_to_the_settings()
    {
        var plan = Scenario.BusyTown().Plan();
        Assert.True(PlanCarryOver.Capture(plan).IsEmpty);
        plan.Increase("tavern:wanderer:w1");
        plan.Decrease("food:grain", EditSize.Five);
        plan.Increase("loot:MeleeWeapons");                              // one spear kept back by hand
        var carry = PlanCarryOver.Capture(plan);
        Assert.Equal(new[] { "tavern:wanderer:w1", "loot:MeleeWeapons" }, carry.Edits.Select(e => e.Key));
        Assert.Equal(1, carry.Edits[0].Value);
        var goal = Assert.Single(plan.TakeGoalEdits());
        Assert.Equal("food:grain", goal.Key);
        Assert.Equal(plan.Row("food:grain").Result, goal.Goal);
        Assert.Empty(plan.TakeGoalEdits()); // taken once
    }

    [Fact]
    public void A_re_plan_keeps_the_goals_and_the_steward_plans_the_rest_around_them()
    {
        var s = new Scenario().Village().Party(10)
            .Food("grain", held: 5, market: 100, buy: 10)
            .Food("fish", held: 0, market: 100, buy: 10);
        var plan = s.Plan();
        plan.SetChange("food:grain", 30);
        var carry = PlanCarryOver.Capture(plan);
        Assert.True(carry.IsEmpty);                                // the goal is no visit edit…
        foreach (var edit in plan.TakeGoalEdits())
            edit.ApplyTo(s.Settings);                              // …the window saves it (SaveQuietly)
        Assert.Equal(35, s.Settings.Goals["food:grain"]);

        s.Settings.FoodDays = 80; // the steward now wants more food: 4 per man
        var fresh = s.Plan();
        Assert.Equal(0, carry.ApplyTo(fresh));
        Assert.Equal(30, fresh.Row("food:grain").Change);         // the player's goal
        Assert.True(fresh.Row("food:grain").IsTouched);
        Assert.Equal(40 - 35, fresh.Row("food:fish").Change);     // the steward fills the new target (40) around it
    }

    [Fact]
    public void The_new_limits_clamp_a_goal_and_vanished_rows_are_dropped()
    {
        var s = Scenario.BusyTown();
        var plan = s.Plan();
        plan.SetGoal("food:grain", 45);
        plan.Increase("tavern:wanderer:w1");
        plan.Increase("prisoner:looter");                                   // one looter kept back by hand
        var carry = PlanCarryOver.Capture(plan);
        foreach (var edit in plan.TakeGoalEdits())
            edit.ApplyTo(s.Settings);

        s.Settings.PriceBook["grain"] = new PriceBookEntry { Buy = false }; // unticked for buying (still held)
        s.Settings.ShowTavern = false;                                      // the tavern rows are gone
        var fresh = s.Plan();
        Assert.Equal(1, carry.ApplyTo(fresh));                              // the prisoner row; the wanderer is gone
        Assert.Null(fresh.FindRow("tavern:wanderer:w1"));
        var grain = fresh.Row("food:grain");
        Assert.Equal(45, grain.ManualGoal);                                 // the standing goal stays…
        Assert.True(grain.Change <= 0);                                     // …but nothing may be bought
        Assert.Equal(GoalShort.NoneEligible, RowGoal.Of(grain).Short);
    }
}
