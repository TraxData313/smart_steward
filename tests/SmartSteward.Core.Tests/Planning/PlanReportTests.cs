using SmartSteward.Core.Planning;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>The plan as text for the log (PLAN step 6).</summary>
public class PlanReportTests
{
    [Fact]
    public void The_text_is_plain_ascii_for_the_games_font()
    {
        var plan = Scenario.BusyTown().Plan();
        Assert.All(PlanReport.Full(plan), line => Assert.All(line, c => Assert.True(c < 128, "non-ASCII '" + c + "' in " + line)));
    }

    [Fact]
    public void Full_lists_every_row_and_every_transaction()
    {
        var plan = Scenario.BusyTown().Plan();
        var lines = PlanReport.Full(plan).ToList();
        foreach (var row in plan.Rows)
            Assert.Contains(lines, l => l.TrimStart().StartsWith(row.Id + " ", StringComparison.Ordinal));
        Assert.Contains("transactions: " + plan.Transactions.Count, lines);
        Assert.Equal(plan.Transactions.Count, lines.Count(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^  \d+\. ")));
        Assert.StartsWith("gold 30,000 -> ", lines[0]);
        Assert.StartsWith("facts: ", lines[1]);
    }

    [Fact]
    public void Role_and_group_rows_get_english_labels()
    {
        var plan = Scenario.BusyTown().Plan();
        Assert.Equal("Pack animals", PlanReport.RowLabel(plan.FindRow("mounts:pack")!));
        Assert.Equal("Riding mounts", PlanReport.RowLabel(plan.FindRow("mounts:riding")!));
        Assert.Equal("Upgrade horses (war_horse)", PlanReport.RowLabel(plan.FindRow("mounts:upgrade:war_horse")!));
        Assert.Equal("Armour", PlanReport.RowLabel(plan.FindRow("loot:Armour")!));
        Assert.Equal("grain", PlanReport.RowLabel(plan.FindRow("food:grain")!));
    }

    [Fact]
    public void An_idle_plan_lists_no_transactions()
    {
        var lines = PlanReport.Full(new Scenario().Plan()).ToList();
        Assert.Contains("transactions: 0", lines);
        Assert.DoesNotContain(lines, l => l.StartsWith("flags: ", StringComparison.Ordinal));
    }

    [Fact]
    public void Floor_breaches_are_flagged()
    {
        // 5,200 gold and a 300 horse: the steward keeps the 5,000 floor for horses and buys none…
        var s = new Scenario().Party(10, footmen: 10).Gold(5_200).Mount("hunter", "horse", market: 5, buy: 300);
        var plan = s.Plan();
        Assert.Equal(0, plan.FindRow("mounts:riding")!.Change);
        Assert.DoesNotContain(PlanReport.Full(plan), l => l.StartsWith("flags: ", StringComparison.Ordinal));

        // …the player's hand buys one anyway: the floor shows, it does not block
        plan.Increase("mounts:riding");
        var edited = PlanReport.Full(plan).ToList();
        Assert.Contains(edited, l => l.Contains("mounts:riding \"Riding mounts\" Mount: mine 0, change +1 (yours, suggested +0)"));
        Assert.Contains("flags: below the minimum gold for horses", edited);
        Assert.DoesNotContain(edited, l => l.Contains("cannot afford"));
    }

    [Fact]
    public void Transactions_are_described_with_their_prices()
    {
        var plan = Scenario.BusyTown().Plan();
        var sell = plan.Transactions.First(t => t.Kind == TransactionKind.Sell);
        string text = PlanReport.DescribeTransaction(sell);
        Assert.StartsWith("Sell " + sell.Count + " x " + sell.StackKey + " [" + sell.RowId + "] = ", text);
        var donateScenario = new Scenario().Prisoner("looter", 3, 20, influence: 1.5);
        donateScenario.Snap.Prison = new PrisonInfo { DonateAllowed = true, DungeonRoom = 10 };
        donateScenario.Settings.DonatePrisonersWhenPossible = true;
        var donate = donateScenario.Plan().Transactions.Single();
        Assert.Equal("Donate 3 x looter [prisoner:looter] = +4.5 influence", PlanReport.DescribeTransaction(donate));
    }
}
