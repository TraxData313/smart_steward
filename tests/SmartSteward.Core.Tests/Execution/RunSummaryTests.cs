using SmartSteward.Core.Execution;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Execution;

/// <summary>
/// The "Steward report:" line (PLAN step 33 — Anton 2026.10.02: "the line in the battle log 'Steward: 44 of 44...' is
/// confusing, like that something changed with my Steward skill, make it 'Steward report: deals made, gold influence change,
/// prisoners ransomed/donated'"): Deal all, a part's Deal and the autonomous steward all say it this way — the executor's
/// real counts, zeros left out, the trouble at the end.
/// </summary>
public class RunSummaryTests
{
    private static StewardPlan Prisoners(int looters, int bandits)
    {
        var s = new Scenario().Prisoner("looter", looters, 20);
        if (bandits > 0) s.Prisoner("bandit", bandits, 30, influence: 0.8);
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = bandits };
        s.Settings.PrisonerAction = bandits > 0 ? PrisonerChoice.Donate : PrisonerChoice.Ransom;
        return s.Plan();
    }

    private static ExecutionReport AsPlanned(StewardPlan plan, int goldBefore)
    {
        var report = new ExecutionReport(goldBefore);
        foreach (var t in plan.Transactions)
        {
            var o = report.Add(t);
            if (t.Kind == TransactionKind.Ransom || t.Kind == TransactionKind.Donate)
                o.AddUnits(t.Count, t.Gold);
            else
                foreach (var price in t.UnitPrices)
                    o.AddUnit(price);
        }
        report.GoldAfter = goldBefore + plan.Totals.GoldChange;
        return report;
    }

    [Fact]
    public void Deals_denari_influence_and_prisoners_in_one_line()
    {
        var plan = Prisoners(8, 4);
        var summary = RunSummary.From(AsPlanned(plan, 100_000));

        Assert.Equal((2, 160, 8, 4), (summary.Deals, summary.Denari, summary.Ransomed, summary.Donated));
        Assert.Equal("Steward report: 2 deals made · +160 denari · +3.2 influence · 8 prisoners ransomed · 4 prisoners donated",
            summary.Line("Steward report:"));
    }

    [Fact]
    public void One_of_a_kind_reads_in_the_singular()
    {
        var summary = RunSummary.From(AsPlanned(Prisoners(1, 0), 500));
        Assert.Equal("Steward report: 1 deal made · +20 denari · 1 prisoner ransomed", summary.Line("Steward report:"));
    }

    [Fact]
    public void A_busy_town_counts_every_deal_and_the_purses_real_change()
    {
        var plan = Scenario.BusyTown().Plan();
        var report = AsPlanned(plan, 50_000);
        report.GoldAfter -= 7;                                              // the game priced a little higher at the click
        var line = RunSummary.From(report).Line("Steward report:");

        Assert.StartsWith("Steward report: " + plan.Transactions.Count + " deals made · ", line);
        Assert.Contains(UiSigned(plan.Totals.GoldChange - 7) + " denari", line);
        Assert.DoesNotContain("of", line.Split('·')[0]);                  // never "44 of 44" again
    }

    [Fact]
    public void Nothing_to_the_purse_leaves_the_denari_out()
    {
        var plan = Prisoners(0, 3);                                         // donations only: influence, no gold
        var line = RunSummary.From(AsPlanned(plan, 1_000)).Line("Steward report (Prisoners):");
        Assert.Equal("Steward report (Prisoners): 1 deal made · +2.4 influence · 3 prisoners donated", line);
    }

    [Fact]
    public void Trouble_ends_the_line_and_sends_the_player_to_the_log()
    {
        var plan = Prisoners(8, 4);
        var report = new ExecutionReport(100_000);
        var ransom = plan.Transactions.Single(t => t.Kind == TransactionKind.Ransom);
        var cut = report.Add(ransom);
        cut.AddUnits(5, 100);
        cut.Stop(SkipReason.Error, "the broker left");
        report.Add(plan.Transactions.Single(t => t.Kind == TransactionKind.Donate)).Stop(SkipReason.NotAllowedHere);
        report.GoldAfter = 100_100;

        Assert.Equal("Steward report: 1 deal made · +100 denari · 5 prisoners ransomed · 1 cut short, 1 skipped — see smart_steward.log",
            RunSummary.From(report).Line("Steward report:"));
    }

    [Fact]
    public void An_aborted_run_says_nothing_was_done()
    {
        var report = new ExecutionReport(500) { Abort = "the party left the settlement" };
        Assert.Equal("Steward report: nothing was done — see smart_steward.log", RunSummary.From(report).Line("Steward report:"));
    }

    [Fact]
    public void The_words_come_from_outside()
    {
        var words = new SummaryWords
        {
            DealsMany = n => n + " Geschäfte", Denari = n => n + " Dinar", Influence = n => n + " Einfluss",
            RansomedMany = n => n + " freigekauft", DonatedMany = n => n + " in den Kerker",
        };
        Assert.Equal("Verwalterbericht: 2 Geschäfte · +160 Dinar · +3.2 Einfluss · 8 freigekauft · 4 in den Kerker",
            RunSummary.From(AsPlanned(Prisoners(8, 4), 100_000)).Line("Verwalterbericht:", words));
    }

    private static string UiSigned(int value) => SmartSteward.Core.Presentation.UiFormat.SignedMoney(value);
}
