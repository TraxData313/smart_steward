using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// The two prisoner actions (PLAN step 20, Anton 2026.09.28 — round 4: "A toggle ransom/donate lords. Then a toggle to
/// offload them to ransom for money or to dungeon for Influence"): LordPrisonerAction (Keep) and PrisonerAction (Ransom),
/// each Keep | Ransom | Donate; Donate where the game forbids it → the others are ransomed, the lords kept. Prisoners carry
/// their tier, lowest first.
/// </summary>
public class PrisonerActionTests
{
    private static Scenario Camp() => new Scenario().Party(10)
        .Prisoner("looter", 12, 16).Prisoner("raider", 9, 44).Prisoner("spearman", 4, 90).Prisoner("tolun", 1, 8_210, hero: true);

    private static void Tiers(Scenario s)
    {
        foreach (var p in s.Snap.Prisoners)
            p.Tier = p.TroopId switch { "looter" => 1, "raider" => 2, "spearman" => 3, _ => 0 };
    }

    [Fact]
    public void Defaults_keep_the_lords_and_ransom_the_others()
    {
        var d = new StewardSettings();
        Assert.Equal(PrisonerChoice.Keep, d.LordPrisonerAction);
        Assert.Equal(PrisonerChoice.Ransom, d.PrisonerAction);

        var plan = Camp().Plan();
        Assert.Equal(0, plan.Row("prisoner:tolun").Change);
        Assert.Equal(1, plan.Row("prisoner:tolun").MaxSell); // kept, but a click ransoms him
        Assert.Equal(-12, plan.Row("prisoner:looter").Change);
        Assert.Equal(0, plan.Totals.PrisonersAfter - 1);
    }

    [Fact]
    public void Rows_carry_their_tier_lowest_first_and_the_lords_last()
    {
        var s = Camp();
        Tiers(s);
        var rows = s.Plan().Section(PlanSectionKind.Prisoners)!.Rows;
        Assert.Equal(new[] { "prisoner:looter", "prisoner:raider", "prisoner:spearman", "prisoner:tolun" }, rows.Select(r => r.Id));
        Assert.Equal(new[] { 1, 2, 3, 0 }, rows.Select(r => r.Prisoner!.Tier));
    }

    [Fact]
    public void Keep_proposes_nothing_and_a_click_ransoms_by_hand()
    {
        var s = Camp();
        s.Settings.PrisonerAction = PrisonerChoice.Keep;
        var plan = s.Plan();
        Assert.All(plan.Rows.Where(r => r.Type == RowType.Prisoner), r => Assert.Equal(0, r.Change));
        plan.Decrease("prisoner:raider", EditSize.Five);
        Assert.Equal(5, plan.Row("prisoner:raider").Prisoner!.RansomCount);
        Assert.Equal(5 * 44, plan.Row("prisoner:raider").GoldDelta);
    }

    [Fact]
    public void Donate_fills_the_dungeon_by_value_whatever_the_tables_order_and_ransoms_the_rest()
    {
        var s = Camp();
        Tiers(s);
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = 10 };
        var plan = s.Plan();
        // The spearmen (90) then the raiders (44) take the 10 places — the looters, first in the table, are ransomed.
        Assert.Equal(4, plan.Row("prisoner:spearman").Prisoner!.DonateCount);
        Assert.Equal(6, plan.Row("prisoner:raider").Prisoner!.DonateCount);
        Assert.Equal(3, plan.Row("prisoner:raider").Prisoner!.RansomCount);
        Assert.Equal(12, plan.Row("prisoner:looter").Prisoner!.RansomCount);
        Assert.Equal(0, plan.Row("prisoner:tolun").Change); // the lords: Keep
    }

    [Fact]
    public void Donate_where_the_game_forbids_it_ransoms_the_others_and_keeps_the_lords()
    {
        var s = Camp();
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        s.Settings.LordPrisonerAction = PrisonerChoice.Donate;
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = false, DungeonRoom = 50 };
        var plan = s.Plan();
        Assert.Equal(-12, plan.Row("prisoner:looter").Change);
        Assert.Equal(12, plan.Row("prisoner:looter").Prisoner!.RansomCount);
        var lord = plan.Row("prisoner:tolun");
        Assert.Equal(0, lord.Change);
        Assert.Equal(0, lord.MaxSell); // a lord to donate is never ransomed
        Assert.False(lord.Prisoner!.MayRansom);
    }

    [Fact]
    public void A_lord_to_donate_goes_to_the_dungeon_first_and_a_full_dungeon_keeps_him()
    {
        var s = Camp();
        s.Settings.LordPrisonerAction = PrisonerChoice.Donate;
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = 3 };
        var plan = s.Plan();
        Assert.Equal(1, plan.Row("prisoner:tolun").Prisoner!.DonateCount); // the most valuable of all
        Assert.Equal(2, plan.Row("prisoner:spearman").Prisoner!.DonateCount);
        Assert.Equal(2, plan.Row("prisoner:spearman").Prisoner!.RansomCount);

        s.Snap.Prison.DungeonRoom = 0;
        var full = s.Plan();
        Assert.Equal(0, full.Row("prisoner:tolun").Change);             // kept
        Assert.Equal(-4, full.Row("prisoner:spearman").Change);         // ransomed
    }

    [Fact]
    public void Lords_to_ransom_are_ransomed()
    {
        var s = Camp();
        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        Assert.Equal(8_210, s.Plan().Row("prisoner:tolun").GoldDelta);
    }

    [Theory]
    [InlineData("{ }", PrisonerChoice.Ransom, PrisonerChoice.Keep, 0)]
    [InlineData("{ \"RansomPrisoners\": false }", PrisonerChoice.Keep, PrisonerChoice.Keep, 2)]
    [InlineData("{ \"RansomHeroPrisoners\": true }", PrisonerChoice.Ransom, PrisonerChoice.Ransom, 2)]
    [InlineData("{ \"ransomprisoners\": true, \"RansomHeroPrisoners\": true, \"DonatePrisonersWhenPossible\": true }",
        PrisonerChoice.Donate, PrisonerChoice.Donate, 2)]
    [InlineData("{ \"RansomPrisoners\": false, \"DonatePrisonersWhenPossible\": true }", PrisonerChoice.Donate, PrisonerChoice.Keep, 2)]
    public void An_old_files_switches_carry_over_once(string json, PrisonerChoice others, PrisonerChoice lords, int notes)
    {
        var parsed = SettingsFile.Parse(json);
        Assert.Empty(parsed.Problems);
        Assert.Equal(others, parsed.Settings.PrisonerAction);
        Assert.Equal(lords, parsed.Settings.LordPrisonerAction);
        Assert.Equal(notes, parsed.Renamed.Count);
        Assert.Empty(parsed.Retired);
        var rewritten = SettingsFile.Generate(parsed.Settings);
        Assert.DoesNotContain("RansomPrisoners", rewritten);
        Assert.DoesNotContain("DonatePrisonersWhenPossible", rewritten);
        Assert.Empty(SettingsFile.Parse(rewritten).Renamed);
        if (notes > 0)
            Assert.Contains(parsed.Renamed, n => n.Contains("now PrisonerAction = \"" + others + "\""));
    }

    [Fact]
    public void The_new_keys_win_over_the_old_switches()
    {
        var parsed = SettingsFile.Parse("{ \"RansomPrisoners\": false, \"PrisonerAction\": \"Donate\" }");
        Assert.Equal(PrisonerChoice.Donate, parsed.Settings.PrisonerAction);
        Assert.Equal(PrisonerChoice.Keep, parsed.Settings.LordPrisonerAction);
        Assert.Contains(parsed.Problems, p => p.Contains("ignored for PrisonerAction"));
    }

    [Fact]
    public void An_old_switch_of_the_wrong_type_counts_as_its_old_default()
    {
        var parsed = SettingsFile.Parse("{ \"RansomPrisoners\": \"yes\" }");
        Assert.Equal(PrisonerChoice.Ransom, parsed.Settings.PrisonerAction);
        Assert.Contains(parsed.Problems, p => p.Contains("\"RansomPrisoners\"") && p.Contains("old default true"));
    }

    [Fact]
    public void A_village_has_no_prisoner_rows()
    {
        Assert.Null(Camp().Village().Plan().Section(PlanSectionKind.Prisoners));
    }
}
