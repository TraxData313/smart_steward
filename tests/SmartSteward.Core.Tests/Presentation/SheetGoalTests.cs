using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>
/// Round 5 (DESIGN §1.1 "THE GOAL"): the Goal cell of every line and title line as the window will bind it in step 23 — the
/// number, the typed box, "yours", the hands-off <c>–*</c> with its hover, the Result's reason when short; the Troops title's
/// party limit with Mine red over it.
/// </summary>
public class SheetGoalTests
{
    private const string M = "–";

    private static SheetItem Item(SheetView view, string key) =>
        view.Sections.SelectMany(s => s.Items).Single(i => i.Key == key);

    [Fact]
    public void A_goal_of_yours_and_the_stewards_own_show_as_numbers_in_typed_boxes()
    {
        var s = new Scenario().Party(10).Food("grain", market: 100, buy: 10).Food("fish", market: 100, buy: 12);
        s.Settings.Goals["food:grain"] = 15;
        var view = SheetView.Build(s.Plan());
        var grain = Item(view, "row:food:grain").Goal;
        Assert.Equal(("15", true, true, false), (grain.Text, grain.Editable, grain.IsYours, grain.HandsOff));
        Assert.True(Item(view, "row:food:grain").CanReset);        // the ⟲ of a goal
        var fish = Item(view, "row:food:fish").Goal;
        Assert.Equal(("10", true, false), (fish.Text, fish.Editable, fish.IsYours)); // the even share: 15 counts 10 (step 25)
        Assert.False(Item(view, "row:food:fish").CanReset);

        var food = view.Section(SheetGroup.Food)!;
        Assert.Equal("25", food.Goal.Text);                          // the sum of the rows: the target + your 5 on top
        Assert.False(food.Goal.Editable);
        Assert.Equal(("0", "25"), (food.Mine, food.Result));
    }

    [Fact]
    public void Below_the_threshold_the_goal_is_a_dash_with_a_star_and_says_why()
    {
        var s = new Scenario().Party(10).Gold(1_450).Food("grain", market: 100, buy: 10)
            .Mount("noble_a", "noble_horse", held: 1, sell: 2_000);
        s.Settings.FoodMinDenari = 2_000;
        s.Settings.MountsMinDenari = 5_000;
        var view = SheetView.Build(s.Plan());
        var grain = Item(view, "row:food:grain").Goal;
        Assert.Equal((M + "*", true, true), (grain.Text, grain.HandsOff, grain.Editable));
        Assert.Equal("Not managed yet: the steward starts on food at 2,000 denari " + M + " you have 1,450. "
                     + "Type a goal to order it anyway.", grain.Hint);
        var noble = Item(view, "row:mounts:noble").Goal;
        Assert.Equal("Not managed yet: the steward starts on riding horses at 5,000 denari " + M + " you have 1,450.", noble.Hint);
        Assert.False(noble.Editable);

        var food = view.Section(SheetGroup.Food)!.Goal;               // every row hands-off: the title too
        Assert.Equal((M + "*", true), (food.Text, food.HandsOff));
        Assert.Equal(grain.Hint, food.Hint);
    }

    [Fact]
    public void A_result_short_of_its_goal_says_why_in_its_hover()
    {
        var s = new Scenario().Party(10).Gold(1_500).Food("grain", market: 100, buy: 10);
        s.Settings.ManualGoalsKeepPurseFloor = true;
        s.Settings.Goals["food:grain"] = 100;
        var grain = Item(SheetView.Build(s.Plan()), "row:food:grain");
        Assert.Equal(("100", "50"), (grain.Goal.Text, grain.Result));
        Assert.Equal(GoalShort.PurseFloor, grain.Goal.Short);
        Assert.Equal("Short of the goal: keeps your purse at 1,000 denari.", grain.Goal.ShortText);

        s.Settings.Goals["food:grain"] = 50;                          // reached: no reason
        Assert.Equal("", Item(SheetView.Build(s.Plan()), "row:food:grain").Goal.ShortText);
    }

    [Fact]
    public void Every_reason_has_words()
    {
        var words = new SheetWords();
        var s = new Scenario().Party(10).Pack("mule", held: 3, market: 20, buy: 140);
        var plan = s.Plan();
        plan.SetGoal("mounts:pack", 40);
        Assert.Equal("Short of the goal: the market has no more on offer.",
            SheetView.GoalCell(plan.Row("mounts:pack"), plan, words).ShortText);
        foreach (var reason in Enum.GetValues<GoalShort>().Where(r => r != GoalShort.None))
            Assert.True(typeof(SheetWords).GetProperty("Short" + reason) != null, reason + " has no words");
    }

    [Fact]
    public void The_troops_title_aims_at_the_party_limit_and_mine_goes_red_over_it()
    {
        var view = SheetView.Build(SuggestionSheetTests.Lycaron().Plan);
        var troops = view.Section(SheetGroup.Troops)!;
        Assert.Equal("101", troops.Goal.Text);                        // the party size limit
        Assert.False(troops.Goal.Editable);
        Assert.Equal("103", troops.Mine);
        Assert.True(troops.MineWarning);                              // 103 men, limit 101: red
        foreach (var item in troops.Items)
            Assert.Equal("", item.Goal.Text);                          // no goal on the troop lines (Anton)
    }

    [Fact]
    public void Mine_is_plain_within_the_limit()
    {
        var s = new Scenario().Party(10).Troop("recruit", inParty: 10, onOffer: 5);
        var troops = SheetView.Build(s.Plan()).Section(SheetGroup.Troops)!;
        Assert.Equal("100", troops.Goal.Text);
        Assert.False(troops.MineWarning);
    }

    [Fact]
    public void Prisoner_lines_aim_at_zero_or_keep_and_other_lines_at_zero()
    {
        var plan = Scenario.BusyTown().Plan(); // lords Keep, others Ransom
        var view = SheetView.Build(plan);
        var lords = view.Sections.SelectMany(x => x.Items).Single(i => i.Kind == SheetItemKind.Lords).Goal;
        var others = view.Sections.SelectMany(x => x.Items).Single(i => i.Kind == SheetItemKind.OtherPrisoners).Goal;
        Assert.Equal("1", lords.Text);                                // kept: the goal is what you have
        Assert.Equal("0", others.Text);
        Assert.False(lords.Editable || others.Editable);
        Assert.Equal("1", view.Section(SheetGroup.Prisoners)!.Goal.Text);
        Assert.Equal("0", Item(view, "row:loot:Armour").Goal.Text);
        Assert.Equal("0", view.Section(SheetGroup.Other)!.Goal.Text);
        Assert.Equal("", Item(view, "row:tavern:mercenaries").Goal.Text);
    }

    [Fact]
    public void A_breakdown_line_has_no_goal()
    {
        var s = new Scenario().Party(10, footmen: 10).Mount("hunter", "horse", market: 30, buy: 200);
        var view = SheetView.Build(s.Plan());
        var riding = Item(view, "row:mounts:riding");
        Assert.Equal("11", riding.Goal.Text);                          // the riding target: 10 footmen × 110 / 100
        Assert.All(view.Sections.SelectMany(x => x.Items).Where(i => i.Kind == SheetItemKind.SubLine),
            i => Assert.Equal("", i.Goal.Text));
    }
}
