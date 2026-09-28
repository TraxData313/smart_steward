using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// The Full-autonomous steward's plan (DESIGN §6, PLAN step 8): the floors rise to AutonomousMinGold —
/// max(MinGoldAfterDeal, it) for everything, max(MinGoldForHorses, it) for animals — and the tavern is never
/// offered, so nothing can ever be hired without the player's click.
/// </summary>
public class AutonomousPlanTests
{
    [Fact]
    public void The_window_keeps_the_normal_floors()
    {
        var floors = MoneyFloors.For(new StewardSettings(), PlanMode.Window);
        Assert.Equal(1_000, floors.All);
        Assert.Equal(5_000, floors.Animals);
    }

    [Fact]
    public void Autonomous_floors_rise_to_AutonomousMinGold()
    {
        var floors = MoneyFloors.For(new StewardSettings(), PlanMode.Autonomous);
        Assert.Equal(100_000, floors.All);
        Assert.Equal(100_000, floors.Animals);
    }

    [Fact]
    public void Autonomous_floors_never_go_below_the_normal_ones()
    {
        var settings = new StewardSettings { MinGoldAfterDeal = 200_000, MinGoldForHorses = 300_000, AutonomousMinGold = 250_000 };
        var floors = MoneyFloors.For(settings, PlanMode.Autonomous);
        Assert.Equal(250_000, floors.All);     // max(200,000, 250,000)
        Assert.Equal(300_000, floors.Animals); // max(300,000, 250,000)

        settings.AutonomousMinGold = 0;
        floors = MoneyFloors.For(settings, PlanMode.Autonomous);
        Assert.Equal(200_000, floors.All);
        Assert.Equal(300_000, floors.Animals);
    }

    [Fact]
    public void Food_purchases_stop_at_the_autonomous_floor()
    {
        var s = new Scenario().Party(100).Gold(101_000).Food("grain", market: 1_000, buy: 10);

        var window = s.Plan();
        Assert.Equal(200, window.Row("food:grain").Change); // the whole target: ceil(100 × 2)

        var alone = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Equal(PlanMode.Autonomous, alone.Mode);
        Assert.Equal(100, alone.Row("food:grain").Change);  // 1,000 gold above the floor buys 100 at 10
        Assert.Equal(100_000, alone.Totals.GoldAfter);
        Assert.False(alone.Totals.BelowMinGoldAfterDeal);
    }

    [Fact]
    public void Animal_purchases_stop_at_the_higher_of_the_horse_floor_and_the_autonomous_floor()
    {
        var s = new Scenario().Party(10).Gold(101_000).Pack("mule", market: 20, buy: 150);
        Assert.Equal(10, s.Plan().Row("mounts:pack").Change);

        var alone = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Equal(6, alone.Row("mounts:pack").Change);   // 1,000 above 100,000 buys 6 at 150

        s.Settings.MinGoldForHorses = 100_500;               // now the horse floor is the higher one
        alone = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Equal(3, alone.Row("mounts:pack").Change);   // 500 above 100,500 buys 3
        Assert.False(alone.Totals.BelowMinGoldForHorses);
    }

    [Fact]
    public void Below_the_autonomous_floor_the_steward_still_sells_and_ransoms_but_buys_nothing()
    {
        var s = Scenario.BusyTown(); // 30,000 gold — far below the default 100,000
        var alone = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);

        Assert.DoesNotContain(alone.Transactions, t => t.Kind == TransactionKind.Buy);
        Assert.Contains(alone.Transactions, t => t.Kind == TransactionKind.Sell);
        Assert.Contains(alone.Transactions, t => t.Kind == TransactionKind.Ransom);
        Assert.True(alone.Totals.GoldAfter > s.Snap.PlayerGold);
    }

    [Fact]
    public void The_tavern_is_never_offered_to_the_autonomous_steward()
    {
        var s = Scenario.BusyTown();
        s.Settings.AutonomousMinGold = 0;

        var window = s.Plan();
        Assert.NotNull(window.Section(PlanSectionKind.Tavern));

        var alone = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Null(alone.Section(PlanSectionKind.Tavern));
        Assert.DoesNotContain(alone.Transactions,
            t => t.Kind == TransactionKind.HireWanderer || t.Kind == TransactionKind.HireMercenaries);
        // Everything else is the window's plan: with the floor at 0 the autonomous floors equal the normal ones.
        Assert.Equal(window.Rows.Where(r => r.Section != PlanSectionKind.Tavern).Select(r => r.Id + " " + r.Change),
            alone.Rows.Select(r => r.Id + " " + r.Change));
    }

    [Fact]
    public void An_edit_past_the_autonomous_floor_is_flagged_against_it()
    {
        // A loot sale taken back by hand is no goal: the floors only flag it.
        var s = new Scenario().Party(100).Gold(101_000).Food("grain", market: 1_000, buy: 10)
            .Loot("rags", LootGroup.Armour, held: 10, sell: 100);
        s.Settings.SellLoot = true;
        var alone = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Equal(100_000, alone.Floors.All);
        Assert.Equal(-10, alone.Row("loot:Armour").Change);

        alone.Increase("loot:Armour", EditSize.All); // the 1,000 of the rags taken back after the food was bought with it

        Assert.True(alone.Totals.GoldAfter < 100_000);
        Assert.True(alone.Totals.BelowMinGoldAfterDeal); // below 100,000, though far above MinGoldAfterDeal
    }

    [Fact]
    public void Goals_never_take_the_autonomous_purse_below_AutonomousMinGold()
    {
        // Round 5: your goals obey AutonomousMinGold whatever ManualGoalsKeepPurseFloor says (off in the test kit).
        var s = new Scenario().Party(100).Gold(101_000).Food("grain", market: 1_000, buy: 10);
        s.Settings.Goals["food:grain"] = 900;
        Assert.False(s.Settings.ManualGoalsKeepPurseFloor);
        var alone = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        var grain = alone.Row("food:grain");
        Assert.Equal(100, grain.Change);                  // 1,000 denari above the floor: 100 grain
        Assert.Equal(100_000, alone.Totals.GoldAfter);
        Assert.Equal(GoalShort.PurseFloor, RowGoal.Of(grain).Short);
        Assert.Equal(EditBlock.PurseFloor, grain.IncreaseBlock);
    }

    [Fact]
    public void The_master_switch_empties_the_autonomous_plan_too()
    {
        var s = Scenario.BusyTown();
        s.Settings.ModEnabled = false;
        var alone = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Empty(alone.Sections);
        Assert.Empty(alone.Transactions);
    }
}
