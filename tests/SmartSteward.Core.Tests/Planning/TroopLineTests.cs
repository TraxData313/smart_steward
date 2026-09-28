using SmartSteward.Core.Planning;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// The troops section's two aggregate lines (PLAN step 20 — Anton 2026.09.28, round 4: "when I press - there drop the lowest
/// tier unit, if I press + add them back the same order I dropped them"; mockup choice 8: Recruits only hire, best tier first;
/// Your troops only dismiss, lowest tier first, and [+] re-adds in reverse — an undo stack on the line).
/// </summary>
public class TroopLineTests
{
    /// <summary>The mockup's Lycaron: two T0 peasants and six T1 recruits of his own (the recruits also on offer), a T2 archer.</summary>
    private static Scenario Lycaron() => new Scenario().Party(40, footmen: 40)
        .Troop("peasant", inParty: 2, tier: 0, name: "Empire Peasant")
        .Troop("recruit", inParty: 6, onOffer: 15, price: 17, tier: 1, wounded: 2, name: "Imperial Recruit")
        .Troop("archer", inParty: 1, tier: 2, name: "Imperial Archer")
        .Troop("vigla", onOffer: 3, price: 38, tier: 2, name: "Imperial Vigla Recruit");

    private static string Changes(StewardPlan plan) =>
        string.Join(" ", plan.Rows.Where(r => r.Type == RowType.Troop).OrderBy(r => r.Id).Select(r => r.Id[7..] + ":" + r.Change));

    [Fact]
    public void Your_troops_lists_every_held_type_lowest_tier_first_and_recruits_every_offer_best_first()
    {
        var plan = Lycaron().Plan();
        Assert.Equal(new[] { "troops:peasant", "troops:recruit", "troops:archer" }, plan.YourTroopRows.Select(r => r.Id));
        Assert.Equal(new[] { "troops:vigla", "troops:recruit" }, plan.RecruitRows.Select(r => r.Id));
    }

    [Fact]
    public void Minus_drops_the_lowest_tier_first_across_every_held_type()
    {
        var plan = Lycaron().Plan();
        plan.DismissLowest();
        plan.DismissLowest();
        plan.DismissLowest(); // the two peasants, then a recruit (the wounded go first at Do it)
        Assert.Equal("archer:0 peasant:-2 recruit:-1 vigla:0", Changes(plan));
    }

    [Fact]
    public void Plus_re_adds_in_the_reverse_order_they_were_dropped()
    {
        var plan = Lycaron().Plan();
        plan.DismissLowest(); // peasant
        plan.DismissLowest(); // peasant
        plan.DismissLowest(); // recruit
        plan.ReAddDropped();
        Assert.Equal("archer:0 peasant:-2 recruit:0 vigla:0", Changes(plan)); // the last one dropped comes back first
        plan.ReAddDropped();
        Assert.Equal("archer:0 peasant:-1 recruit:0 vigla:0", Changes(plan));
    }

    [Fact]
    public void A_shift_click_is_undone_part_by_part()
    {
        var plan = Lycaron().Plan();
        plan.DismissLowest(EditSize.Five); // 2 peasants + 3 recruits in one click
        Assert.Equal("archer:0 peasant:-2 recruit:-3 vigla:0", Changes(plan));
        plan.ReAddDropped();
        Assert.Equal("archer:0 peasant:-2 recruit:-2 vigla:0", Changes(plan));
        plan.ReAddDropped(EditSize.Five); // the other 2 recruits, then both peasants — 4 of the 5 asked, nobody else is dropped
        Assert.Equal("archer:0 peasant:0 recruit:0 vigla:0", Changes(plan));
        Assert.Equal(EditBlock.NothingDropped, plan.ReAddDroppedBlock);
    }

    [Fact]
    public void The_record_follows_rows_moved_by_hand_since_and_falls_back_to_the_highest_tier()
    {
        var plan = Lycaron().Plan();
        plan.DismissLowest(EditSize.Five);          // peasants 2, recruits 3
        plan.Increase("troops:recruit", EditSize.Five); // the player takes the recruits back on their own row
        plan.Decrease("troops:archer");             // and drops the archer by hand
        plan.ReAddDropped();                        // the record's recruits are gone: its peasants come back
        Assert.Equal("archer:-1 peasant:-1 recruit:0 vigla:0", Changes(plan));
        plan.ReAddDropped(EditSize.Five);           // the last peasant, then the rows still dropping men: the archer
        Assert.Equal("archer:0 peasant:0 recruit:0 vigla:0", Changes(plan));
    }

    [Fact]
    public void Recruits_hire_the_best_tier_first_and_minus_gives_back_the_newest_first()
    {
        var plan = Lycaron().Plan();
        Assert.Equal(EditBlock.NothingRecruited, plan.TakeBackRecruitsBlock);
        plan.RecruitBest(EditSize.Five); // 3 Vigla (T2), then 2 Imperial Recruits (T1)
        Assert.Equal("archer:0 peasant:0 recruit:2 vigla:3", Changes(plan));
        plan.TakeBackRecruits();
        Assert.Equal("archer:0 peasant:0 recruit:1 vigla:3", Changes(plan));
        plan.TakeBackRecruits(EditSize.All);
        Assert.Equal("archer:0 peasant:0 recruit:0 vigla:0", Changes(plan));
    }

    [Fact]
    public void Reset_all_forgets_the_lines_records()
    {
        var plan = Lycaron().Plan();
        plan.DismissLowest(EditSize.Five);
        plan.ResetAll();
        Assert.Equal(EditBlock.NothingDropped, plan.ReAddDroppedBlock);
        plan.Decrease("troops:archer");
        plan.ReAddDropped(); // no stale record: the archer, the only one dropped
        Assert.Equal("archer:0 peasant:0 recruit:0 vigla:0", Changes(plan));
    }

    [Fact]
    public void The_lines_are_ordinary_row_edits_the_food_re_plans_and_do_it_dismisses_the_same_way()
    {
        var s = Lycaron().Food("grain", held: 10, market: 200, buy: 10);
        var plan = s.Plan();
        int target = plan.Facts.FoodTarget;
        plan.DismissLowest(EditSize.Five);
        Assert.True(plan.Row("troops:recruit").IsTouched);
        Assert.True(plan.Facts.FoodTarget < target); // five mouths fewer
        Assert.Contains(plan.Transactions, t => t.Kind == TransactionKind.Dismiss && t.RowId == "troops:recruit" && t.Count == 3);
    }
}
