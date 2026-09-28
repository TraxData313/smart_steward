using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>
/// PLAN step 23: the typed Goal box — what the player left in it (Enter or a click elsewhere) against what it showed. Only a
/// changed whole number of 0 or more commits; anything else reverts; the same text never pins the steward's goal as yours.
/// </summary>
public class GoalInputTests
{
    [Theory]
    [InlineData("60", "60")]
    [InlineData(" 60 ", "60")]
    [InlineData("–*", "–*")]
    [InlineData("", "")]
    public void The_text_the_box_showed_is_no_edit(string typed, string shown)
    {
        Assert.Equal(GoalTyped.Unchanged, GoalInput.Read(typed, shown, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-5")]
    [InlineData("–5")]
    [InlineData("1.5x")]
    public void Anything_but_a_whole_number_of_0_or_more_reverts(string typed)
    {
        Assert.Equal(GoalTyped.Invalid, GoalInput.Read(typed, "60", out _));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("75", 75)]
    [InlineData("1,200", 1200)]
    [InlineData("060", 60)]
    [InlineData("999999999", ManualGoals.MaxGoal)]
    public void A_changed_number_is_a_goal_clamped_to_the_settings_range(string typed, int goal)
    {
        Assert.Equal(GoalTyped.Goal, GoalInput.Read(typed, "60", out int got));
        Assert.Equal(goal, got);
    }

    [Fact]
    public void A_goal_typed_over_the_hands_off_mark_orders_the_food_anyway()
    {
        Assert.Equal(GoalTyped.Goal, GoalInput.Read("40", "–*", out int goal));
        Assert.Equal(40, goal);
    }

    [Fact]
    public void A_typed_goal_through_the_plan_becomes_yours_and_waits_to_be_saved()
    {
        var s = new Scenario().Party(10).Food("grain", market: 100, buy: 10).Food("fish", market: 100, buy: 12);
        var plan = s.Plan();
        var grain = SheetView.Build(plan).Sections.SelectMany(x => x.Items).Single(i => i.Key == "row:food:grain");
        Assert.False(grain.Goal.IsYours);

        Assert.Equal(GoalTyped.Goal, GoalInput.Read("30", grain.Goal.Text, out int goal));
        var result = plan.SetGoal(grain.Row!.Id, goal);
        Assert.Equal(30, result.Goal);

        var after = SheetView.Build(plan).Sections.SelectMany(x => x.Items).Single(i => i.Key == "row:food:grain");
        Assert.Equal(("30", true, true), (after.Goal.Text, after.Goal.IsYours, after.CanReset));
        var edits = plan.TakeGoalEdits();
        Assert.Single(edits);
        Assert.Empty(plan.TakeGoalEdits()); // taken once
    }
}
