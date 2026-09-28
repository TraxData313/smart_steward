using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>The Suggestion table's columns (DESIGN §1.1) from real plans, and the footer's pure half.</summary>
public class RowCellsTests
{
    private const string M = "–";

    [Fact]
    public void A_food_row_shows_mine_change_result_price_and_market()
    {
        var plan = new Scenario().Village().Party(10).Food("grain", held: 5, market: 100, buy: 10).Plan();
        var cells = RowCells.Of(plan.Row("food:grain"));
        Assert.Equal("5", cells.Mine);
        Assert.Equal("+15", cells.Change);
        Assert.Equal("20", cells.Result);
        Assert.Equal("15 × 10 = " + M + "150", cells.Price);
        Assert.Equal("100", cells.Market);
        Assert.Equal(UiColors.Buy, cells.Color);
        Assert.False(cells.HasBreakdown);
    }

    [Fact]
    public void A_sold_row_is_red_and_earns()
    {
        var plan = new Scenario().Village().Party(10).Food("grain", held: 5, market: 100, buy: 10, sell: 8).Plan();
        plan.Decrease("food:grain", EditSize.All);
        plan.Decrease("food:grain", EditSize.Five);
        var cells = RowCells.Of(plan.Row("food:grain"));
        Assert.Equal(M + "5", cells.Change);
        Assert.Equal("0", cells.Result);
        Assert.Equal("5 × 8 = +40", cells.Price);
        Assert.Equal(UiColors.Sell, cells.Color);
    }

    [Fact]
    public void An_untouched_row_is_grey_with_an_empty_price()
    {
        var plan = new Scenario().Village().Party(10).Food("grain", held: 20, market: 100, buy: 10).Plan();
        var cells = RowCells.Of(plan.Row("food:grain"));
        Assert.Equal("0", cells.Change);
        Assert.Equal("", cells.Price);
        Assert.Equal(UiColors.Muted, cells.Color);
    }

    [Fact]
    public void A_loot_row_shows_the_locked_apart_the_weight_freed_and_no_market()
    {
        var s = Scenario.BusyTown();
        s.Loot("mail", LootGroup.Armour, held: 3, sell: 90, locked: true);
        var plan = s.Plan();
        var armour = plan.Row("loot:Armour");
        var cells = RowCells.Of(armour);
        Assert.Equal("34", cells.Mine);       // 30 rags + 4 helmets sellable
        Assert.Equal(3, cells.Locked);        // the locked mail, shown apart
        Assert.Equal(M + "34", cells.Change);
        Assert.Equal("—", cells.Market);
        Assert.Equal(UiFormat.SignedWeight(armour.WeightDelta), cells.WeightFreed);
        Assert.StartsWith("34 × ", cells.Price);
        Assert.EndsWith("= +" + UiFormat.Money(armour.GoldDelta), cells.Price);
    }

    [Fact]
    public void A_prisoner_row_prices_the_ransom_and_names_the_donation_apart()
    {
        var s = new Scenario().Prisoner("looter", 8, 20);
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = 3 };
        s.Settings.DonatePrisonersWhenPossible = true;
        var plan = s.Plan();
        var cells = RowCells.Of(plan.Row("prisoner:looter"));
        Assert.Equal(M + "8", cells.Change);
        Assert.Equal("5 × 20 = +100", cells.Price); // 3 donated, 5 ransomed
        Assert.Equal("+3", cells.Influence);
        Assert.Equal("—", cells.Market);
    }

    [Fact]
    public void A_tavern_row_shows_the_price_to_hire_until_hired()
    {
        var plan = Scenario.BusyTown().Plan();
        var arn = RowCells.Of(plan.Row("tavern:wanderer:w1"));
        Assert.Equal("0", arn.Change);
        Assert.Equal("700", arn.Price);
        Assert.Equal("—", arn.Market);

        plan.Increase("tavern:wanderer:w1");
        arn = RowCells.Of(plan.Row("tavern:wanderer:w1"));
        Assert.Equal("+1", arn.Change);
        Assert.Equal("1 × 700 = " + M + "700", arn.Price);
        Assert.Equal(UiColors.Buy, arn.Color);

        var band = RowCells.Of(plan.Row("tavern:mercenaries"));
        Assert.Equal("8", band.Market);
        Assert.Equal("100", band.Price);
    }

    [Fact]
    public void Mount_role_rows_have_a_breakdown_in_the_same_columns()
    {
        var plan = Scenario.BusyTown().Plan();
        var riding = plan.Row("mounts:riding");
        Assert.True(RowCells.Of(riding).HasBreakdown);
        Assert.False(RowCells.Of(plan.Row("food:grain")).HasBreakdown);
        foreach (var line in riding.Breakdown)
        {
            var cells = RowCells.Of(line);
            Assert.Equal(UiFormat.Money(line.Mine + line.Change), cells.Result);
            Assert.Equal(UiColors.ForChange(line.Change), cells.Color);
        }
    }

    [Fact]
    public void Footer_warnings_come_most_serious_first()
    {
        var plan = Scenario.BusyTown().Gold(500).Plan();
        plan.Increase("tavern:mercenaries", EditSize.All); // the hires alone take the purse past the floors (the steward's
                                                           // own buys give way to them — the live re-plan)
        var warnings = PlanFooter.Warnings(plan.Totals);
        Assert.Contains(PlanWarning.BelowMinGoldAfterDeal, warnings);
        Assert.Equal(warnings.OrderBy(w => w).ToList(), warnings.ToList());
        Assert.True(PlanFooter.CanExecute(plan)); // the floors never stop Do it
    }

    [Fact]
    public void Do_it_needs_something_to_do_and_a_purse_that_pays()
    {
        var idle = new Scenario().Village().Party(10).Food("grain", held: 20, market: 100).Plan();
        Assert.False(PlanFooter.CanExecute(idle));

        // A loot sale funds a mule; taking the sale back leaves a deal the purse cannot pay.
        var s = new Scenario().Village().Gold(0).Pack("mule", market: 20, buy: 140)
            .Loot("rags", LootGroup.Armour, held: 30, sell: 8);
        s.Settings.SellLoot = true;
        s.Settings.MinGoldAfterDeal = 0;
        s.Settings.MinGoldForHorses = 0;
        var plan = s.Plan();
        Assert.Equal(-30, plan.Row("loot:Armour").Change);
        Assert.Equal(1, plan.Row("mounts:pack").Change);
        Assert.True(PlanFooter.CanExecute(plan));
        plan.Increase("loot:Armour", EditSize.All);
        Assert.True(plan.Totals.CannotAfford);
        Assert.Equal(PlanWarning.CannotAfford, PlanFooter.Warnings(plan.Totals)[0]);
        Assert.False(PlanFooter.CanExecute(plan));
    }
}
