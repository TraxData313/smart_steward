using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>
/// The folded sections of the Suggestion tab (PLAN step 18 — Anton 2026.09.28: "each section header expands/collapses;
/// collapsed = ONE summary line (what the section will do + its gold, e.g. 'Food +29 (5 kinds) −510 · 64 → 71 days')"):
/// the groups, the line of each, the neutral line when nothing is queued, and that the line follows every click.
/// </summary>
public class SectionSummaryTests
{
    private const string M = "–";

    [Fact]
    public void The_troops_halves_fold_together_and_every_section_has_a_group()
    {
        Assert.Equal(SectionGroup.Troops, SectionGroups.Of(PlanSectionKind.Recruits));
        Assert.Equal(SectionGroup.Troops, SectionGroups.Of(PlanSectionKind.Troops));
        Assert.Equal(SectionGroup.Tavern, SectionGroups.Of(PlanSectionKind.Tavern));
        Assert.Equal(SectionGroup.Food, SectionGroups.Of(PlanSectionKind.Food));
        Assert.Equal(SectionGroup.Mounts, SectionGroups.Of(PlanSectionKind.Mounts));
        Assert.Equal(SectionGroup.ArmourAndWeapons, SectionGroups.Of(PlanSectionKind.ArmourAndWeapons));
        Assert.Equal(SectionGroup.Prisoners, SectionGroups.Of(PlanSectionKind.Prisoners));
        // Every group, in the table's order (DESIGN §1.1).
        Assert.Equal(Enum.GetValues<SectionGroup>(), SectionGroups.All);
        Assert.Equal(Enum.GetValues<PlanSectionKind>().Select(SectionGroups.Of).Distinct(), SectionGroups.All);
    }

    [Theory]
    [InlineData("Food", SectionGroup.Food)]
    [InlineData("armourandweapons", SectionGroup.ArmourAndWeapons)]
    [InlineData("  TROOPS ", SectionGroup.Troops)]
    public void Group_names_read_back_case_insensitively(string key, SectionGroup expected)
    {
        Assert.True(SectionGroups.TryParse(key, out var group));
        Assert.Equal(expected, group);
    }

    [Theory]
    [InlineData("Horses")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unknown_group_name_is_refused(string? key) => Assert.False(SectionGroups.TryParse(key, out _));

    [Fact]
    public void Nothing_queued_reads_as_a_short_neutral_line()
    {
        var plan = Scenario.BusyTown().Village().Troop("a", inParty: 5, onOffer: 3).Plan();
        plan.ResetAll();
        foreach (var row in plan.Rows.ToList())
            if (row.Change != 0)
                plan.SetChange(row.Id, 0);

        Assert.Equal("nobody hired", SectionSummary.Of(plan, SectionGroup.Tavern));
        Assert.Equal("nobody recruited or dismissed", SectionSummary.Of(plan, SectionGroup.Troops));
        Assert.StartsWith("no change " + UiFormat.Dot + " ", SectionSummary.Of(plan, SectionGroup.Food));
        Assert.EndsWith(" days", SectionSummary.Of(plan, SectionGroup.Food));
        Assert.Equal("no change", SectionSummary.Of(plan, SectionGroup.Mounts));
        Assert.Equal("nothing sold", SectionSummary.Of(plan, SectionGroup.ArmourAndWeapons));
        Assert.Equal("nobody ransomed", SectionSummary.Of(plan, SectionGroup.Prisoners));
    }

    [Fact]
    public void A_section_the_plan_lacks_reads_neutral_too()
    {
        var plan = new Scenario().Village().Party(10).Plan();
        Assert.Equal("nobody hired", SectionSummary.Of(plan, SectionGroup.Tavern));
        Assert.Equal("no change", SectionSummary.Of(plan, SectionGroup.Mounts));
    }

    [Fact]
    public void Food_says_what_it_buys_its_gold_and_the_days_before_and_after()
    {
        // 10 eaters at vanilla's 0.5 a day: 5 grain = 10 days; the goal of 40 days wants 20 → +15 at 10 = –150.
        var plan = new Scenario().Village().Party(10).Food("grain", held: 5, market: 100, buy: 10).Plan();
        Assert.Equal("+15 (1 kind) " + M + "150 " + UiFormat.Dot + " 10 " + UiFormat.Arrow + " 40 days",
            SectionSummary.Of(plan, SectionGroup.Food));

        // Held at the goal: nothing moves, the days alone.
        plan = new Scenario().Village().Party(10).Food("grain", held: 20, market: 100, buy: 10).Plan();
        Assert.Equal("no change " + UiFormat.Dot + " 40 days", SectionSummary.Of(plan, SectionGroup.Food));
    }

    [Fact]
    public void Food_counts_the_kinds_bought_and_the_units_sold()
    {
        var plan = new Scenario().Village().Party(10)
            .Food("grain", held: 5, market: 100, buy: 10)
            .Food("fish", market: 50, buy: 14)
            .Plan();
        int bought = plan.Section(PlanSectionKind.Food)!.Rows.Sum(r => Math.Max(0, r.Change));
        int kinds = plan.Section(PlanSectionKind.Food)!.Rows.Count(r => r.Change > 0);
        Assert.Equal(2, kinds);
        Assert.StartsWith("+" + bought + " (2 kinds) " + M, SectionSummary.Of(plan, SectionGroup.Food));

        // Sold by hand: "N sold" joins the line, the gold is the section's net.
        plan.Decrease("food:grain", EditSize.All);
        plan.Decrease("food:grain", EditSize.Five);
        string line = SectionSummary.Of(plan, SectionGroup.Food);
        Assert.Contains("(1 kind), 5 sold ", line);
        int gold = plan.Section(PlanSectionKind.Food)!.Rows.Sum(r => r.GoldDelta);
        Assert.Contains(" " + UiFormat.SignedMoney(gold) + " " + UiFormat.Dot + " ", line);
    }

    [Fact]
    public void Mounts_say_what_they_buy_and_sell_and_the_gold()
    {
        // 10 footmen at 110 per 100 → 11 horses at 300 in a village (flat prices).
        var s = new Scenario().Village().Party(10, footmen: 10).Mount("hunter", "horse", market: 30, buy: 300);
        var plan = s.Plan();
        Assert.Equal("+11 (1 kind) " + M + "3,300", SectionSummary.Of(plan, SectionGroup.Mounts));

        s = new Scenario().Village().Party(10).Mount("hunter", "horse", held: 4, market: 30, buy: 300, sell: 150);
        plan = s.Plan();
        Assert.Equal("4 sold +600", SectionSummary.Of(plan, SectionGroup.Mounts));
    }

    [Fact]
    public void Armour_and_weapons_say_the_pieces_sold_the_gold_and_the_weight_freed()
    {
        var s = new Scenario().Village().Loot("rags", LootGroup.Armour, held: 30, sell: 8, weight: 5)
            .Loot("spear", LootGroup.MeleeWeapons, held: 11, sell: 20, weight: 2);
        s.Settings.SellLoot = true;
        var plan = s.Plan();
        Assert.Equal("41 sold +460 " + UiFormat.Dot + " " + M + "172 kg", SectionSummary.Of(plan, SectionGroup.ArmourAndWeapons));
    }

    [Fact]
    public void Prisoners_say_ransomed_donated_gold_and_influence()
    {
        var plan = new Scenario().Prisoner("looter", 8, 20).Plan();
        Assert.Equal("8 ransomed +160", SectionSummary.Of(plan, SectionGroup.Prisoners));

        var s = new Scenario().Prisoner("infantry", 3, 100, influence: 1.5).Prisoner("looter", 4, 50, influence: 1.0);
        s.Settings.DonatePrisonersWhenPossible = true;
        s.Snap.Prison.DonateAllowed = true;
        s.Snap.Prison.DungeonRoom = 5;
        Assert.Equal("2 ransomed, 5 to the dungeon +100 +6.5 influence", SectionSummary.Of(s.Plan(), SectionGroup.Prisoners));
    }

    [Fact]
    public void The_line_follows_every_click_tavern_and_troops()
    {
        var s = Scenario.BusyTown().Troop("vlandian_recruit", inParty: 3, onOffer: 5, price: 20).Troop("legionary", inParty: 10);
        var plan = s.Plan();
        Assert.Equal("nobody hired", SectionSummary.Of(plan, SectionGroup.Tavern));

        plan.Increase("tavern:mercenaries", EditSize.One);
        plan.Increase("tavern:mercenaries", EditSize.One);
        Assert.Equal("+2 hired " + M + "200", SectionSummary.Of(plan, SectionGroup.Tavern));

        plan.Increase("troops:vlandian_recruit", EditSize.One);
        plan.Increase("troops:vlandian_recruit", EditSize.One);
        plan.Increase("troops:vlandian_recruit", EditSize.One);
        Assert.Equal("recruiting 3 T0 vlandian_recruit " + M + "60", SectionSummary.Of(plan, SectionGroup.Troops));
        plan.Decrease("troops:legionary", EditSize.One);
        plan.Decrease("troops:legionary", EditSize.One);
        Assert.Equal("dismissing 2 T0 legionary " + UiFormat.Dot + " recruiting 3 T0 vlandian_recruit " + M + "60",
            SectionSummary.Of(plan, SectionGroup.Troops));
    }

    [Fact]
    public void English_words_are_the_default_and_a_translation_replaces_them()
    {
        var plan = new Scenario().Prisoner("looter", 8, 20).Plan();
        var words = new SummaryWords { Ransomed = "rançonnés" };
        Assert.Equal("8 rançonnés +160", SectionSummary.Of(plan, SectionGroup.Prisoners, words));
    }
}
