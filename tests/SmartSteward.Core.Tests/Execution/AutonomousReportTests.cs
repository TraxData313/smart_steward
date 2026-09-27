using SmartSteward.Core.Execution;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Execution;

/// <summary>
/// The Full-autonomous steward's message-log report (DESIGN §6, PLAN step 8): one entry per job that did something
/// and the purse, a line of its own for trouble, nothing at all when nothing happened — from the executor's REAL
/// outcomes, in the game fonts' glyphs (en-dash minus, » arrow, · between jobs).
/// </summary>
public class AutonomousReportTests
{
    /// <summary>A town where the autonomous steward buys food of two kinds and riding horses, sells a loot group
    /// and ransoms prisoners — flat prices, so every number is easy to check by hand.</summary>
    private static (StewardPlan Plan, Scenario S) Sargot()
    {
        var s = new Scenario().Party(10, footmen: 3).Gold(312_400, marketGold: 100_000)
            .Food("grain", market: 100, buy: 10)
            .Food("fish", market: 100, buy: 12)
            .Mount("hunter", "horse", market: 10, buy: 180)
            .Loot("rags", LootGroup.Armour, held: 41, sell: 52)
            .Prisoner("looter", 12, 80);
        s.Settings.SellLoot = true;
        s.Settings.AutonomousMinGold = 0;
        return (StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous), s);
    }

    /// <summary>Every transaction done exactly as planned.</summary>
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
    public void One_line_names_every_job_that_did_something_then_the_purse()
    {
        var (plan, _) = Sargot();
        Assert.Equal(20, plan.Rows.Where(r => r.Type == RowType.Food).Sum(r => r.Change)); // target 2 × 10 men
        var summary = AutonomousReport.From(plan, AsPlanned(plan, 312_400));

        var lines = summary.Lines("Steward at Sargot:", "Steward:");

        Assert.Equal(
            "Steward at Sargot: food +20 (2 kinds) –220 · mounts +4 –720 · armour & weapons 41 sold +2,132 · "
            + "prisoners 12 ransomed +960 · gold 312,400 » 314,552",
            Assert.Single(lines));
        Assert.Equal(new[] { StewardJob.Food, StewardJob.Mounts, StewardJob.ArmourAndWeapons, StewardJob.Prisoners },
            summary.Jobs.Select(j => j.Job));
        Assert.False(summary.IsEmpty);
    }

    [Fact]
    public void Nothing_happened_means_no_message()
    {
        var s = new Scenario().Party(10).Food("grain", held: 20, market: 100, buy: 10); // food on target, nothing else
        var plan = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Empty(plan.Transactions);

        var summary = AutonomousReport.From(plan, AsPlanned(plan, 100_000));

        Assert.True(summary.IsEmpty);
        Assert.Empty(summary.Lines("Steward at X:", "Steward:"));
    }

    [Fact]
    public void Skipped_and_cut_short_transactions_get_their_own_line_and_only_real_units_count()
    {
        var (plan, _) = Sargot();
        var report = new ExecutionReport(312_400);
        var ransom = plan.Transactions.Single(t => t.Kind == TransactionKind.Ransom);
        report.Add(ransom).AddUnits(ransom.Count, ransom.Gold);
        var sellRags = plan.Transactions.Single(t => t.Kind == TransactionKind.Sell);
        var cut = report.Add(sellRags);
        for (int i = 0; i < 30; i++) cut.AddUnit(sellRags.UnitPrices[i]);
        cut.Stop(SkipReason.MarketOutOfGold);
        foreach (var buy in plan.Transactions.Where(t => t.Kind == TransactionKind.Buy))
            report.Add(buy).Stop(SkipReason.NotOnOffer);
        report.GoldAfter = 312_400 + 960 + 30 * 52;

        var summary = AutonomousReport.From(plan, report);
        var lines = summary.Lines("Steward at Sargot:", "Steward:");

        Assert.Equal(2, lines.Count);
        Assert.Equal("Steward at Sargot: armour & weapons 30 sold +1,560 · prisoners 12 ransomed +960 · gold 312,400 » 314,920",
            lines[0]);
        int buys = plan.Transactions.Count(t => t.Kind == TransactionKind.Buy);
        Assert.Equal("Steward: 1 cut short, " + buys + " skipped — see smart_steward.log", lines[1]);
    }

    [Fact]
    public void All_skipped_gives_only_the_trouble_line()
    {
        var (plan, _) = Sargot();
        var report = new ExecutionReport(312_400);
        foreach (var t in plan.Transactions)
            report.Add(t).Stop(SkipReason.NotAllowedHere);

        var lines = AutonomousReport.From(plan, report).Lines("Steward at Sargot:", "Steward:");

        Assert.Equal("Steward: " + plan.Transactions.Count + " skipped — see smart_steward.log", Assert.Single(lines));
    }

    [Fact]
    public void An_aborted_run_says_nothing_was_done()
    {
        var (plan, _) = Sargot();
        var report = new ExecutionReport(312_400) { Abort = "the party is no longer in Sargot" };

        var lines = AutonomousReport.From(plan, report).Lines("Steward at Sargot:", "Steward:");

        Assert.Equal("Steward: nothing was done — see smart_steward.log", Assert.Single(lines));
    }

    [Fact]
    public void Donations_report_their_influence_beside_the_ransom()
    {
        var s = new Scenario().Prisoner("looter", 8, 20).Prisoner("bandit", 4, 30, influence: 0.8);
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = 4 };
        s.Settings.DonatePrisonersWhenPossible = true;
        s.Settings.AutonomousMinGold = 0;
        var plan = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);

        var summary = AutonomousReport.From(plan, AsPlanned(plan, 100_000));

        var prisoners = Assert.Single(summary.Jobs);
        Assert.Equal(8, prisoners.Ransomed);
        Assert.Equal(4, prisoners.Donated);  // the dungeon's room, most valuable first
        Assert.Equal("prisoners 8 ransomed, 4 donated +160 +3.2 influence",
            AutonomousReport.Describe(prisoners, new ReportWords()));
    }

    [Theory]
    [InlineData(StewardJob.Food, 5, 1, 0, 0, -50, "food +5 (1 kind) –50")]
    [InlineData(StewardJob.Food, 0, 0, 12, 0, 96, "food 12 sold +96")]
    [InlineData(StewardJob.Mounts, 3, 1, 2, 0, -400, "mounts +3, 2 sold –400")]
    [InlineData(StewardJob.Mounts, 2, 1, 2, 0, 0, "mounts +2, 2 sold")]
    [InlineData(StewardJob.ArmourAndWeapons, 0, 0, 1234, 0, 45678, "armour & weapons 1,234 sold +45,678")]
    public void A_job_reads_what_moved_and_its_net_gold(StewardJob job, int bought, int kinds, int sold, int ransomed,
        int gold, string expected)
    {
        var j = new JobResult(job) { Bought = bought, KindsBought = kinds, Sold = sold, Ransomed = ransomed, Gold = gold };
        Assert.Equal(expected, AutonomousReport.Describe(j, new ReportWords()));
    }

    [Fact]
    public void The_words_come_from_outside_so_a_translation_replaces_them()
    {
        var (plan, _) = Sargot();
        var words = new ReportWords
        {
            Food = "Essen", Mounts = "Pferde", ArmourAndWeapons = "Beute", Prisoners = "Gefangene",
            Kind = "Sorte", Kinds = "Sorten", Sold = "verkauft", Ransomed = "freigekauft", Gold = "Gold",
        };

        var line = AutonomousReport.From(plan, AsPlanned(plan, 312_400)).Lines("Verwalter in Sargot:", "Verwalter:", words)[0];

        Assert.Equal("Verwalter in Sargot: Essen +20 (2 Sorten) –220 · Pferde +4 –720 · Beute 41 verkauft +2,132 · "
            + "Gefangene 12 freigekauft +960 · Gold 312,400 » 314,552", line);
    }
}
