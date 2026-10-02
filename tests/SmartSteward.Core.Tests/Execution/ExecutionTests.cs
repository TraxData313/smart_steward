using SmartSteward.Core.Settings;
using SmartSteward.Core.Execution;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Execution;

/// <summary>The executor's pure half (PLAN step 6): the unit-by-unit budget, the price limits it takes from the
/// plan, and the outcomes and report it writes to the log.</summary>
public class ExecutionTests
{
    [Fact]
    public void A_sale_needs_the_market_to_pay_and_the_min_price()
    {
        var budget = new ExecutionBudget(purse: 100, marketGold: 25);
        Assert.Equal(SkipReason.PriceLimit, budget.Check(TradeDirection.Sell, 9, limit: 10));
        Assert.Equal(SkipReason.None, budget.Check(TradeDirection.Sell, 10, limit: 10));
        budget.Record(TradeDirection.Sell, 20);
        Assert.Equal(120, budget.Purse);
        Assert.Equal(5, budget.MarketGoldLeft);
        Assert.Equal(SkipReason.MarketOutOfGold, budget.Check(TradeDirection.Sell, 6, limit: null));
        Assert.Equal(SkipReason.None, budget.Check(TradeDirection.Sell, 5, limit: null));
        Assert.Equal(20, budget.Sales);
    }

    [Fact]
    public void A_purchase_needs_the_purse_and_the_max_price()
    {
        var budget = new ExecutionBudget(purse: 30, marketGold: 0);
        Assert.Equal(SkipReason.PriceLimit, budget.Check(TradeDirection.Buy, 13, limit: 12));
        Assert.Equal(SkipReason.None, budget.Check(TradeDirection.Buy, 12, limit: 12));
        budget.Record(TradeDirection.Buy, 12);
        budget.Record(TradeDirection.Buy, 12);
        Assert.Equal(6, budget.Purse);
        Assert.Equal(SkipReason.NotEnoughGold, budget.Check(TradeDirection.Buy, 7, limit: null));
        Assert.Equal(SkipReason.None, budget.Check(TradeDirection.Buy, 6, limit: null));
        Assert.Equal(24, budget.Purchases);
        // buys never raise what the market can pay (the planner's gross-sales rule)
        Assert.Equal(0, budget.MarketGoldLeft);
    }

    [Fact]
    public void A_purchase_keeps_the_floor_it_is_given()
    {
        var budget = new ExecutionBudget(purse: 1_000, marketGold: 0);
        Assert.Equal(SkipReason.None, budget.Check(TradeDirection.Buy, 100, limit: null, floor: 900));
        Assert.Equal(SkipReason.BelowFloor, budget.Check(TradeDirection.Buy, 101, limit: null, floor: 900));
        // the purse and the price limit are named first; no floor = the old rule
        Assert.Equal(SkipReason.NotEnoughGold, budget.Check(TradeDirection.Buy, 1_001, limit: null, floor: 900));
        Assert.Equal(SkipReason.PriceLimit, budget.Check(TradeDirection.Buy, 200, limit: 150, floor: 900));
        Assert.Equal(SkipReason.None, budget.Check(TradeDirection.Buy, 1_000, limit: null));
        // a sale never answers to a floor
        Assert.Equal(SkipReason.None, new ExecutionBudget(0, 500).Check(TradeDirection.Sell, 100, limit: null, floor: 900));
    }

    [Fact]
    public void Only_the_autonomous_steward_holds_its_floors_again_at_the_click()
    {
        // The window: the player saw the flags and clicked — no floor at the click.
        var window = Scenario.BusyTown().Plan();
        foreach (var tx in window.Transactions)
            Assert.Equal(0, ExecutionBudget.FloorOf(window, tx));

        // Autonomous: food answers to max(MinGoldAfterDeal, AutonomousMinGold), animals to the higher animal floor.
        var s = Scenario.BusyTown();
        s.Gold(400_000);
        s.Settings.AutonomousMinGold = 150_000;
        s.Settings.MinGoldForHorses = 200_000;
        var plan = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        var food = plan.Transactions.First(t => t.Kind == TransactionKind.Buy && t.RowId.StartsWith("food:", StringComparison.Ordinal));
        var horse = plan.Transactions.First(t => t.Kind == TransactionKind.Buy && t.RowId.StartsWith("mounts:", StringComparison.Ordinal));
        Assert.Equal(150_000, ExecutionBudget.FloorOf(plan, food));
        Assert.Equal(200_000, ExecutionBudget.FloorOf(plan, horse));
    }

    [Fact]
    public void Price_limits_come_from_the_rows_lanes()
    {
        var plan = Scenario.BusyTown().Plan();
        var buyFood = plan.Transactions.First(t => t.Kind == TransactionKind.Buy && t.RowId.StartsWith("food:", StringComparison.Ordinal));
        var foodRow = plan.FindRow(buyFood.RowId)!;
        Assert.Equal(foodRow.PriceBook!.FinalMaxBuy, ExecutionBudget.PriceLimitOf(plan, buyFood));

        // loot sells at any price: no limit
        var sellLoot = plan.Transactions.First(t => t.Kind == TransactionKind.Sell && t.RowId.StartsWith("loot:", StringComparison.Ordinal));
        Assert.Null(ExecutionBudget.PriceLimitOf(plan, sellLoot));

        // animals: the price book AND the role cap
        var buyPack = plan.Transactions.First(t => t.Kind == TransactionKind.Buy && t.RowId == "mounts:pack");
        var packLane = plan.FindRow("mounts:pack")!.BuyLane!;
        Assert.Equal(packLane.Stacks.Single(s => s.Stack.Key == buyPack.StackKey).PriceLimit,
            ExecutionBudget.PriceLimitOf(plan, buyPack));

        // prisoners have none
        var ransom = plan.Transactions.First(t => t.Kind == TransactionKind.Ransom);
        Assert.Null(ExecutionBudget.PriceLimitOf(plan, ransom));
        Assert.Equal(TradeDirection.Buy, ExecutionBudget.DirectionOf(buyPack));
        Assert.Equal(TradeDirection.Sell, ExecutionBudget.DirectionOf(sellLoot));
    }

    [Fact]
    public void An_outcome_keeps_the_real_prices_the_drift_and_the_first_reason()
    {
        var plan = Scenario.BusyTown().Plan();
        var buy = plan.Transactions.First(t => t.Kind == TransactionKind.Buy && t.Count >= 3);
        var o = new TransactionOutcome(buy);
        o.AddUnit(buy.UnitPrices[0]);
        o.AddUnit(buy.UnitPrices[1] + 2);
        Assert.Equal(2, o.Done);
        Assert.Equal(buy.UnitPrices[0] + buy.UnitPrices[1] + 2, o.Gold);
        Assert.Equal(2, o.Drift);
        Assert.Equal(buy.Count - 2, o.Skipped);

        o.Stop(SkipReason.NotEnoughGold, "purse 3");
        o.Stop(SkipReason.PriceLimit);
        Assert.Equal(SkipReason.NotEnoughGold, o.Reason);
        Assert.Equal("purse 3", o.Detail);

        o.RollBack("DoneLogic refused");
        Assert.Equal(0, o.Done);
        Assert.Equal(0, o.Gold);
        Assert.Empty(o.UnitPrices);
        Assert.Equal(SkipReason.RolledBack, o.Reason);
    }

    [Fact]
    public void A_ransom_is_paid_in_one_go_and_donations_carry_their_influence()
    {
        var s = new Scenario().Prisoner("looter", 8, 20).Prisoner("bandit", 4, 30);
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = 4 };
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        var plan = s.Plan();
        var donate = plan.Transactions.Single(t => t.Kind == TransactionKind.Donate);
        var ransom = plan.Transactions.Single(t => t.Kind == TransactionKind.Ransom);

        var d = new TransactionOutcome(donate);
        d.AddUnits(donate.Count, 0);
        Assert.Equal(donate.Influence, d.Influence, 6);

        var r = new TransactionOutcome(ransom);
        r.AddUnits(ransom.Count, ransom.Gold);
        Assert.Equal(0, r.Drift);
        Assert.Equal(ransom.Count, r.UnitPrices.Count);
    }

    [Fact]
    public void The_report_counts_and_logs_every_transaction()
    {
        var plan = Scenario.BusyTown().Plan();
        var txs = plan.Transactions;
        Assert.True(txs.Count >= 3);
        var report = new ExecutionReport(goldBefore: 30_000);
        var full = report.Add(txs[0]);
        full.AddUnits(txs[0].Count, txs[0].Gold);
        var cut = report.Add(txs[1]);
        if (txs[1].Count > 1)
        {
            cut.AddUnit(txs[1].UnitPrices[0]);
            cut.Stop(SkipReason.MarketOutOfGold);
        }
        var none = report.Add(txs[2]);
        none.Stop(SkipReason.Locked);
        report.GoldAfter = 28_530;

        Assert.Equal(3, report.Planned);
        Assert.Equal(1, report.NotDone);
        var lines = report.LogLines().ToList();
        Assert.Equal(4, lines.Count);
        Assert.Contains("done " + txs[0].Count, lines[0]);
        Assert.Contains("(as planned)", lines[0]);
        Assert.Contains("stopped: Locked", lines[2]);
        Assert.StartsWith("gold 30,000 -> 28,530 (-1,470)", lines[3]);
    }

    [Theory]
    [InlineData(701, 700, 1, SkipReason.None)]
    [InlineData(700, 700, 1, SkipReason.NotEnoughGold)] // vanilla wants MORE than the price
    [InlineData(5000, 700, 0, SkipReason.CompanionLimit)]
    public void A_wanderer_needs_a_slot_and_more_gold_than_his_price_never_party_room(int gold, int price, int slots,
        SkipReason expected)
    {
        Assert.Equal(expected, ExecutionBudget.WandererBlock(gold, price, slots));
    }

    [Theory]
    [InlineData(8, 8, 10_000, 100, 8, SkipReason.None)]
    [InlineData(8, 5, 10_000, 100, 5, SkipReason.NotOnOffer)]    // the band shrank
    [InlineData(8, 8, 450, 100, 4, SkipReason.NotEnoughGold)]    // the tavern menu's Gold / price
    [InlineData(8, 8, 50, 100, 0, SkipReason.NotEnoughGold)]
    [InlineData(8, 0, 10_000, 100, 0, SkipReason.NotOnOffer)]
    [InlineData(0, 8, 10_000, 100, 0, SkipReason.None)]
    [InlineData(8, 8, 0, 0, 8, SkipReason.None)]                 // free men (a mod): no purse cap
    public void Mercenaries_are_capped_by_offer_and_purse_never_party_room(int wanted, int available, int gold,
        int price, int expected, SkipReason expectedReason)
    {
        Assert.Equal(expected, ExecutionBudget.MercenaryCount(wanted, available, gold, price, out var reason));
        Assert.Equal(expectedReason, reason);
    }

    [Fact]
    public void A_rollback_keeps_the_error_that_caused_it_and_earlier_reasons()
    {
        var tx = Scenario.BusyTown().Plan().Transactions.First(t => t.Kind == TransactionKind.Sell);
        var failed = new TransactionOutcome(tx);
        failed.AddUnit(5);
        failed.Stop(SkipReason.Error, "boom");
        failed.RollBack("batch reset");
        Assert.Equal(SkipReason.Error, failed.Reason);
        Assert.Equal("boom", failed.Detail);
        Assert.Equal(0, failed.Done);

        var neverStarted = new TransactionOutcome(tx);
        neverStarted.Stop(SkipReason.Locked);
        neverStarted.RollBack("batch reset");
        Assert.Equal(SkipReason.Locked, neverStarted.Reason);

        var untouched = new TransactionOutcome(tx);
        untouched.RollBack("batch reset");
        Assert.Equal(SkipReason.RolledBack, untouched.Reason);
    }

    [Fact]
    public void Units_moved_before_an_error_are_never_counted_as_done()
    {
        // DoneLogic threw after the goods moved: the outcome keeps its units but stops on Error — the window's line
        // and the autonomous report send the player to the log instead of calling it a success.
        var plan = Scenario.BusyTown().Plan();
        var tx = plan.Transactions.First(t => t.Kind == TransactionKind.Sell);
        var report = new ExecutionReport(1_000);
        var o = report.Add(tx);
        for (int i = 0; i < tx.Count; i++)
            o.AddUnit(tx.UnitPrices[i]);
        Assert.Equal(1, report.FullyDone);
        o.Stop(SkipReason.Error, "the game's trade logic failed after the goods moved");
        Assert.Equal(0, report.FullyDone);
        Assert.Equal(1, report.CutShort);
        Assert.Equal(0, report.NotDone);
        Assert.Contains("stopped: Error", ExecutionReport.Describe(o));
        report.GoldAfter = 1_000 + tx.Gold;
        var lines = AutonomousReport.From(plan, report).Lines("Steward report at X:", "Steward report, by job:");
        Assert.EndsWith("1 cut short — see smart_steward.log", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void An_aborted_run_says_so()
    {
        var report = new ExecutionReport(500) { Abort = "the party left the settlement" };
        Assert.Equal("ABORTED: the party left the settlement", report.LogLines().First());
    }

    [Fact]
    public void Describe_names_the_drift_when_the_game_priced_differently()
    {
        var plan = Scenario.BusyTown().Plan();
        var sell = plan.Transactions.First(t => t.Kind == TransactionKind.Sell);
        var o = new TransactionOutcome(sell);
        o.AddUnit(sell.UnitPrices[0] - 1);
        string line = ExecutionReport.Describe(o);
        Assert.Contains("drift -1", line);
        Assert.StartsWith("Sell ", line);
        Assert.Contains("[" + sell.RowId + "]", line);
    }
}
