using SmartSteward.Core.Planning;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>The plan as text — the debug door's popup and the log (PLAN step 6).</summary>
public class PlanReportTests
{
    [Fact]
    public void Compact_shows_the_gold_line_the_changing_rows_and_the_footer()
    {
        var plan = Scenario.BusyTown().Plan();
        string text = PlanReport.Compact(plan);
        var t = plan.Totals;

        Assert.StartsWith("Gold 30,000 -> " + t.GoldAfter.ToString("N0", System.Globalization.CultureInfo.InvariantCulture), text);
        Assert.Contains("TAVERN: 2 wanderers, 8 Blades at 100 on offer", text);
        Assert.Contains("FOOD (target ", text);
        Assert.Contains("MOUNTS (footmen 10)", text);
        Assert.Contains("ARMOUR & WEAPONS", text);
        Assert.Contains("PRISONERS", text);
        Assert.Contains("Pack animals: ", text);
        Assert.Contains("looter: 8 -8 = 0 (8 ransomed +160)", text);
        Assert.Contains(plan.Transactions.Count + " transactions", text);
        // rows that change nothing are left out
        foreach (var row in plan.Rows.Where(r => !r.HasChange && r.Type != RowType.Tavern))
            Assert.DoesNotContain("  " + PlanReport.RowLabel(row) + ": ", text);
    }

    [Fact]
    public void The_text_is_plain_ascii_for_the_games_font()
    {
        var plan = Scenario.BusyTown().Plan();
        Assert.All(PlanReport.Compact(plan), c => Assert.True(c < 128, "non-ASCII '" + c + "'"));
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
    public void An_idle_plan_says_there_is_nothing_to_do()
    {
        var plan = new Scenario().Plan();
        string text = PlanReport.Compact(plan);
        Assert.Contains("Nothing to do here.", text);
        Assert.Contains("0 transactions", text);
    }

    [Fact]
    public void Floor_breaches_are_flagged()
    {
        // 5,200 gold and a 300 horse: the steward keeps the 5,000 floor for horses and buys none…
        var s = new Scenario().Party(10, footmen: 10).Gold(5_200).Mount("hunter", "horse", market: 5, buy: 300);
        var plan = s.Plan();
        Assert.Equal(0, plan.FindRow("mounts:riding")!.Change);
        Assert.DoesNotContain("! ", PlanReport.Compact(plan));

        // …the player's hand buys one anyway: the floor shows, it does not block
        plan.Increase("mounts:riding");
        var edited = PlanReport.Compact(plan);
        Assert.Contains("Riding mounts: 0 +1 = 1 (1 x 300 = -300)", edited);
        Assert.Contains("! below the minimum gold for horses", edited);
        Assert.DoesNotContain("cannot afford", edited);
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
