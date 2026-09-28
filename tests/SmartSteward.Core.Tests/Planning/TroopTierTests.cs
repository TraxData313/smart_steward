using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// Troop tiers (PLAN step 18 — Anton 2026.09.28: "tier before every troop name ('T1 Vlandian Recruit'); MY troops ordered by
/// tier, LOWEST on top; the COLLAPSED Troops line carries [-] [+] of its own: [-] dismisses from the lowest tier up, [+]
/// recruits the highest tier on offer first (shift/ctrl steps as usual), and the line says what it does").
/// </summary>
public class TroopTierTests
{
    private const string M = "–";

    [Fact]
    public void Every_troop_row_carries_its_tier_before_its_name()
    {
        var plan = new Scenario().Troop("vlandian_recruit", inParty: 3, tier: 1, name: "Vlandian Recruit").Plan();
        var row = plan.Row("troops:vlandian_recruit");
        Assert.Equal(1, row.Troop!.Tier);
        Assert.Equal("T1 Vlandian Recruit", SectionSummary.TroopName(row));
        Assert.Equal("Rang1 Vlandian Recruit", SectionSummary.TroopName(row, new SummaryWords { TierPrefix = "Rang" }));
    }

    [Fact]
    public void My_troops_lowest_tier_first_recruits_on_offer_highest_first_ties_by_name()
    {
        var plan = new Scenario()
            .Troop("m_t3", inParty: 5, tier: 3, name: "Imperial Legionary")
            .Troop("m_t1b", inParty: 4, tier: 1, name: "Imperial Recruit")
            .Troop("m_t1a", inParty: 2, tier: 1, name: "Aserai Recruit")
            .Troop("m_t2", inParty: 1, tier: 2, name: "Aserai Footman")
            .Troop("o_t1", onOffer: 3, tier: 1, name: "Vlandian Recruit")
            .Troop("o_t3", onOffer: 2, tier: 3, name: "Vlandian Footman")
            .Troop("o_t2b", onOffer: 1, tier: 2, name: "Vlandian Spearman")
            .Troop("o_t2a", onOffer: 1, tier: 2, name: "Vlandian Crossbowman")
            .Plan();
        Assert.Equal(new[] { "troops:m_t1a", "troops:m_t1b", "troops:m_t2", "troops:m_t3" },
            plan.Section(PlanSectionKind.Troops)!.Rows.Select(r => r.Id));
        Assert.Equal(new[] { "troops:o_t3", "troops:o_t2a", "troops:o_t2b", "troops:o_t1" },
            plan.Section(PlanSectionKind.Recruits)!.Rows.Select(r => r.Id));
    }

    /// <summary>Anton's troops, the example line of the plan: two low recruits of his own, a veteran, and footmen on offer.</summary>
    private static Scenario Camp() => new Scenario().Party(30, footmen: 30)
        .Troop("vlandian_recruit", inParty: 4, tier: 1, name: "Vlandian Recruit")
        .Troop("imperial_peasant", inParty: 1, tier: 1, name: "Imperial Peasant")
        .Troop("legionary", inParty: 6, tier: 4, name: "Imperial Legionary")
        .Troop("vlandian_footman", inParty: 2, onOffer: 4, price: 80, tier: 3, name: "Vlandian Footman")
        .Troop("vlandian_levy", onOffer: 5, price: 20, tier: 1, name: "Vlandian Levy");

    [Fact]
    public void The_troops_lines_minus_dismisses_from_the_lowest_tier_up_and_says_so()
    {
        var plan = Camp().Plan();
        var first = plan.DismissLowest(EditSize.One);
        Assert.Equal(("troops:imperial_peasant", 0, -1), (first.Single().RowId, first.Single().Before, first.Single().After));
        plan.DismissLowest(EditSize.One);                          // the peasant is gone: the next T1, the Vlandian Recruit
        Assert.Equal(-1, plan.Row("troops:vlandian_recruit").Change);

        plan.RecruitBest(EditSize.One);
        plan.RecruitBest(EditSize.One);                            // the highest tier on offer: T3 Vlandian Footman
        Assert.Equal(2, plan.Row("troops:vlandian_footman").Change);
        Assert.Equal("dismissing 1 T1 Imperial Peasant, 1 T1 Vlandian Recruit " + UiFormat.Dot
                     + " recruiting 2 T3 Vlandian Footman " + M + "160", SectionSummary.Of(plan, SectionGroup.Troops));
    }

    [Fact]
    public void Shift_takes_five_across_the_rows_ctrl_takes_everyone_own_troops_before_the_types_on_offer()
    {
        var plan = Camp().Plan();
        var five = plan.DismissLowest(EditSize.Five);
        Assert.Equal(new[] { "troops:imperial_peasant", "troops:vlandian_recruit" }, five.Select(r => r.RowId));
        Assert.Equal((-1, -4), (plan.Row("troops:imperial_peasant").Change, plan.Row("troops:vlandian_recruit").Change));

        plan.DismissLowest(EditSize.All);
        // Every own troop first (the T4 legionaries too), then the men of the types on offer (the footmen held).
        Assert.Equal(-6, plan.Row("troops:legionary").Change);
        Assert.Equal(-2, plan.Row("troops:vlandian_footman").Change);
        Assert.Equal(EditBlock.AllDismissed, plan.DismissLowestBlock);
        Assert.Empty(plan.DismissLowest(EditSize.One));
    }

    [Fact]
    public void The_troops_lines_plus_recruits_the_highest_tier_first_then_the_next()
    {
        var plan = Camp().Plan();
        var five = plan.RecruitBest(EditSize.Five);
        Assert.Equal((4, 1), (plan.Row("troops:vlandian_footman").Change, plan.Row("troops:vlandian_levy").Change));
        Assert.Equal(new[] { "troops:vlandian_footman", "troops:vlandian_levy" }, five.Select(r => r.RowId));

        plan.RecruitBest(EditSize.All);
        Assert.Equal(5, plan.Row("troops:vlandian_levy").Change);
        Assert.Equal(EditBlock.AllOnOffer, plan.RecruitBestBlock);
        Assert.Equal("recruiting 4 T3 Vlandian Footman, 5 T1 Vlandian Levy " + M + "420", SectionSummary.Of(plan, SectionGroup.Troops));
    }

    [Fact]
    public void A_tier_the_purse_cannot_pay_is_passed_for_the_next()
    {
        var s = Camp().Gold(100);
        var plan = s.Plan();
        plan.RecruitBest(EditSize.One);                            // a footman costs 80: paid
        Assert.Equal(1, plan.Row("troops:vlandian_footman").Change);
        plan.RecruitBest(EditSize.One);                            // 20 left: the next footman is too dear, a levy is not
        Assert.Equal((1, 1), (plan.Row("troops:vlandian_footman").Change, plan.Row("troops:vlandian_levy").Change));
        Assert.Equal(EditBlock.NotEnoughGold, plan.RecruitBestBlock);
        Assert.False(plan.Totals.CannotAfford);
    }

    [Fact]
    public void Each_line_moves_only_its_own_side_of_a_type_that_is_in_both()
    {
        // Step 20 (mockup choice 8): Recruits only hire, Your troops only dismiss — a type held AND on offer is in both lines.
        var plan = new Scenario().Troop("footman", inParty: 2, onOffer: 4, price: 80, tier: 3).Plan();
        Assert.Equal(new[] { "troops:footman" }, plan.RecruitRows.Select(r => r.Id));
        Assert.Equal(new[] { "troops:footman" }, plan.YourTroopRows.Select(r => r.Id));
        plan.RecruitBest(EditSize.Five);
        Assert.Equal(4, plan.Row("troops:footman").Change);
        Assert.Empty(plan.DismissLowest(EditSize.Five));           // its recruits belong to the Recruits line
        Assert.Equal(EditBlock.NoneToDismiss, plan.DismissLowestBlock);
        plan.TakeBackRecruits(EditSize.All);
        Assert.Equal(0, plan.Row("troops:footman").Change);
        plan.DismissLowest(EditSize.Five);                         // clamped to the 2 held
        Assert.Equal(-2, plan.Row("troops:footman").Change);
        Assert.Empty(plan.RecruitBest(EditSize.One));              // its dismissals belong to the Your troops line
        plan.ReAddDropped(EditSize.One);
        Assert.Equal(-1, plan.Row("troops:footman").Change);
    }

    [Fact]
    public void The_lines_buttons_are_ordinary_row_edits_touched_re_planned_and_executed()
    {
        var s = Camp().Mount("hunter", "horse", held: 33, market: 20, buy: 300);
        var plan = s.Plan();
        int footmenBefore = plan.Facts.Footmen;
        plan.DismissLowest(EditSize.Five);
        plan.RecruitBest(EditSize.One);
        Assert.True(plan.Row("troops:imperial_peasant").IsTouched);
        Assert.True(plan.Row("troops:vlandian_footman").IsTouched);
        Assert.Equal(footmenBefore - 4, plan.Facts.Footmen);        // 5 on foot out, 1 in: the horses re-plan for them
        var kinds = plan.Transactions.Select(t => (t.Kind, t.RowId, t.Count)).ToList();
        Assert.Contains((TransactionKind.Dismiss, "troops:imperial_peasant", 1), kinds);
        Assert.Contains((TransactionKind.Dismiss, "troops:vlandian_recruit", 4), kinds);
        Assert.Contains((TransactionKind.Recruit, "troops:vlandian_footman", 1), kinds);

        plan.Reset("troops:vlandian_recruit");                      // ⟲ on a row the line moved works like on any row
        Assert.Equal(0, plan.Row("troops:vlandian_recruit").Change);
    }

    [Fact]
    public void Past_three_types_the_line_counts_instead_of_naming()
    {
        var s = new Scenario();
        for (int i = 1; i <= 5; i++)
            s.Troop("t" + i, inParty: 3, tier: i);
        var plan = s.Plan();
        plan.DismissLowest(EditSize.All);
        Assert.Equal("dismissing 15 (5 types)", SectionSummary.Of(plan, SectionGroup.Troops));
    }

    [Fact]
    public void Nothing_to_click_says_why()
    {
        var plan = new Scenario().Plan();
        Assert.Equal(EditBlock.NoneToDismiss, plan.DismissLowestBlock);
        Assert.Equal(EditBlock.NotOnOfferHere, plan.RecruitBestBlock);
        Assert.Empty(plan.RecruitBest(EditSize.All));

        plan = new Scenario().Troop("quest_bound", inParty: 3, onOffer: 1, canDismiss: false).Plan();
        Assert.Equal(EditBlock.NoneToDismiss, plan.DismissLowestBlock);
        Assert.Equal(EditBlock.None, plan.RecruitBestBlock);
    }
}
