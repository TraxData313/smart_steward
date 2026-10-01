using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// Step 28 — noble horses to keep (Anton 2026.10.01: "add me option to keep noble mounts like I keep war mounts, same defaults,
/// but number is 0 by default, some mods want nobles for upgrades, but vanilla players dont need that"). With
/// <c>NobleHorsesToKeep</c> N &gt; 0 (or a goal of yours on the noble row) the noble row works like the war row: bought up to N at
/// the price book's noble prices, the dearest sold above it, its own threshold (<c>NobleHorsesMinDenari</c>), a goal; the kept
/// ones count among the horses for the footmen like the war horses. N = 0 is exactly step 17 (<c>NobleAndLameHorseTests</c>).
/// </summary>
public class NobleKeepTests
{
    private static Scenario Footmen(int footmen) => new Scenario().Party(Math.Max(10, footmen), footmen);

    [Fact]
    public void Zero_is_exactly_the_sell_only_row_of_step_17()
    {
        var s = Footmen(0)
            .Mount("noble_a", "noble_horse", held: 2, market: 5, buy: 3000, sell: 1500)
            .Mount("noble_a", "noble_horse", held: 1, buy: 300, sell: 150, modifier: "lame_horse", priceFactor: Scenario.Lame);
        Assert.Equal(0, s.Settings.NobleHorsesToKeep);
        Assert.Equal(20_000, new StewardSettings().NobleHorsesMinDenari); // the war horses' default, unused at 0
        var plan = s.Plan();
        var noble = plan.Row("mounts:noble");
        Assert.False(noble.KeepsNobles);
        Assert.False(noble.TakesGoal);
        Assert.Null(noble.Market);                                     // never bought: no market column
        Assert.Equal(-3, noble.Change);                                // every one, the lame one too (no lame row for it)
        Assert.Null(plan.FindRow("mounts:lame"));
        Assert.Equal(0, noble.MaxBuy);                                 // [+] only takes back a sale
        Assert.Null(plan.Facts.StartsAt(ManagedJob.NobleHorses));
        Assert.Equal(0, plan.Facts.NobleTarget);
        Assert.True(PriceBook.IsSellOnly(PriceBookGroup.NobleHorses, s.Settings));
        Assert.False(RowGoal.Of(noble).Editable);
    }

    [Fact]
    public void With_fewer_held_it_buys_up_to_the_number_and_the_riding_horses_fill_the_rest()
    {
        var s = Footmen(10)                                             // T = 11
            .Mount("noble_a", "noble_horse", held: 1, market: 10, buy: 3000, sell: 1500)
            .Mount("hunter", "horse", market: 50, buy: 200);
        s.Settings.NobleHorsesToKeep = 3;
        var plan = s.Plan();
        var noble = plan.Row("mounts:noble");
        Assert.True(noble.KeepsNobles);
        Assert.True(noble.TakesGoal);
        Assert.Equal(10, noble.Market);
        Assert.Equal(2, noble.Change);                                 // 1 → 3
        Assert.Equal(3, noble.Target);
        Assert.Equal(8, plan.Row("mounts:riding").Change);             // 11 − the 3 noble horses kept
        Assert.Equal(8, plan.Facts.RidingTarget);
        Assert.Equal(3, plan.Facts.NobleTarget);
        Assert.All(plan.Transactions.Where(t => t.RowId == "mounts:noble"), t => Assert.Equal(TransactionKind.Buy, t.Kind));
        var goal = RowGoal.Of(noble);
        Assert.Equal((3, true, false), (goal.Value, goal.Editable, goal.IsYours));

        LivePlanTests.ApplyDoIt(s, plan);                               // Do it → nothing new
        var fresh = s.Plan();
        Assert.Equal(0, fresh.Row("mounts:noble").Change);
        Assert.Equal(0, fresh.Row("mounts:riding").Change);
    }

    [Fact]
    public void Buying_them_sells_the_riding_surplus_against_them_in_the_same_visit()
    {
        // 11 riding horses for 10 footmen; keeping 2 noble horses makes 2 riding horses surplus — sold in this visit (the noble
        // horses are pledged like the war horses), so Do it leaves nothing new.
        var s = Footmen(10)
            .Mount("hunter", "horse", held: 11, market: 50, buy: 200, sell: 150)
            .Mount("noble_a", "noble_horse", market: 10, buy: 3000, sell: 1500);
        s.Settings.NobleHorsesToKeep = 2;
        var plan = s.Plan();
        Assert.Equal(2, plan.Row("mounts:noble").Change);
        Assert.Equal(-2, plan.Row("mounts:riding").Change);

        LivePlanTests.ApplyDoIt(s, plan);
        var fresh = s.Plan();
        Assert.Equal(0, fresh.Row("mounts:noble").Change);
        Assert.Equal(0, fresh.Row("mounts:riding").Change);
    }

    [Fact]
    public void With_more_held_it_sells_only_the_surplus_the_dearest_first_like_the_war_horses()
    {
        var s = Footmen(10)
            .Mount("noble_a", "noble_horse", held: 3, buy: 3000, sell: 1500)
            .Mount("noble_b", "noble_horse", held: 2, buy: 4000, sell: 2000)
            .Mount("hunter", "horse", market: 50, buy: 200);
        s.Settings.NobleHorsesToKeep = 2;
        var plan = s.Plan();
        var noble = plan.Row("mounts:noble");
        Assert.Equal(-3, noble.Change);                                 // 5 → 2
        Assert.Equal(-2, noble.Moved("noble_b"));                       // the dearest go first …
        Assert.Equal(-1, noble.Moved("noble_a"));                       // … two of the cheaper ones stay
        Assert.Equal(9, plan.Row("mounts:riding").Change);              // 11 − 2

        s.Settings.SellNobleHorses = false;                             // the surplus is kept, and says so
        var kept = s.Plan().Row("mounts:noble");
        Assert.Equal(0, kept.Change);
        Assert.Equal(GoalShort.SurplusKept, RowGoal.Of(kept).Short);
    }

    [Fact]
    public void A_locked_noble_horse_counts_toward_the_number_and_is_never_sold()
    {
        var s = Footmen(0)
            .Mount("noble_a", "noble_horse", held: 3, buy: 3000, sell: 1500)
            .Mount("noble_c", "noble_horse", held: 1, buy: 9000, sell: 5000, locked: true);
        s.Settings.NobleHorsesToKeep = 2;
        var noble = s.Plan().Row("mounts:noble");
        Assert.Equal(-2, noble.Change);                                 // 4 → 2: the locked one + one noble_a
        Assert.Equal(0, noble.Moved("noble_c"));
    }

    [Fact]
    public void Below_its_threshold_it_waits_hands_off_with_its_own_words()
    {
        var s = Footmen(0).Gold(10_000).Mount("noble_a", "noble_horse", held: 5, market: 5, buy: 3000, sell: 1500);
        s.Settings.NobleHorsesToKeep = 2;
        s.Settings.NobleHorsesMinDenari = 20_000;
        var plan = s.Plan();
        var noble = plan.Row("mounts:noble");
        Assert.Equal(0, noble.Change);                                  // neither bought nor sold
        Assert.Equal(20_000, noble.StartsAtDenari);
        Assert.Equal(20_000, plan.Facts.StartsAt(ManagedJob.NobleHorses));
        var goal = RowGoal.Of(noble);
        Assert.True(goal.HandsOff);
        Assert.True(goal.Editable);

        var view = SheetView.Build(plan);
        var cell = view.Sections.SelectMany(x => x.Items).Single(i => i.Key == "row:mounts:noble").Goal;
        Assert.Equal("Not managed yet: the steward starts on noble horses at 20,000 denari – you have 10,000. "
                     + "Type a goal to order it anyway.", cell.Hint);
        Assert.Contains("noble horses start at 20,000 denari", SuggestionSheet.Of(plan).Section(SheetGroup.Horses)!.Overview);

        s.Settings.NobleHorsesToKeep = 0;                               // none kept: sold with the riding horses (step 17)
        var sold = s.Plan();
        Assert.Equal(-5, sold.Row("mounts:noble").Change);
        Assert.Null(sold.Facts.StartsAt(ManagedJob.NobleHorses));
    }

    [Fact]
    public void A_typed_goal_works_like_the_war_rows_and_its_reset_gives_the_number_back()
    {
        var s = Footmen(0).Mount("noble_a", "noble_horse", held: 5, market: 10, buy: 3000, sell: 1500);
        s.Settings.NobleHorsesToKeep = 3;
        var plan = s.Plan();
        Assert.Equal(-2, plan.Row("mounts:noble").Change);

        var typed = plan.SetGoal("mounts:noble", 1);
        Assert.Equal(-4, typed.After);
        Assert.Equal(1, plan.Row("mounts:noble").ManualGoal);
        var goal = RowGoal.Of(plan.Row("mounts:noble"));
        Assert.Equal((1, true), (goal.Value, goal.IsYours));
        var edit = Assert.Single(plan.TakeGoalEdits());
        Assert.Equal(("mounts:noble", (int?)1), (edit.Key, edit.Goal));

        plan.Reset("mounts:noble");                                     // ⟲: the number to keep again
        Assert.Equal(-2, plan.Row("mounts:noble").Change);
        Assert.Null(Assert.Single(plan.TakeGoalEdits()).Goal);
    }

    [Fact]
    public void A_goal_in_the_file_keeps_noble_horses_even_with_the_number_at_zero()
    {
        var s = Footmen(0).Mount("noble_a", "noble_horse", held: 1, market: 10, buy: 3000, sell: 1500);
        s.Settings.Goals["mounts:noble"] = 3;
        var plan = s.Plan();
        var noble = plan.Row("mounts:noble");
        Assert.True(noble.KeepsNobles);
        Assert.True(noble.IsTouched);
        Assert.Equal(2, noble.Change);
        Assert.False(PriceBook.IsSellOnly(PriceBookGroup.NobleHorses, s.Settings)); // the Prices tab shows their buy column
    }

    [Fact]
    public void The_autonomous_steward_keeps_them_too_above_its_own_floor()
    {
        var s = Footmen(0).Gold(500_000).Mount("noble_a", "noble_horse", market: 10, buy: 3000, sell: 1500);
        s.Settings.NobleHorsesToKeep = 2;
        var plan = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Equal(2, plan.Row("mounts:noble").Change);

        s.Settings.Goals["mounts:noble"] = 4;                           // a goal of yours: obeyed
        Assert.Equal(4, StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous).Row("mounts:noble").Change);

        s.Gold(101_000);                                                // AutonomousMinGold 100,000 always holds
        Assert.Equal(0, StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous).Row("mounts:noble").Change);
    }

    [Fact]
    public void A_lame_noble_horse_is_replaced_like_any_while_they_are_kept()
    {
        var s = Footmen(0)
            .Mount("noble_a", "noble_horse", held: 1, market: 10, buy: 3000, sell: 1500)
            .Mount("noble_a", "noble_horse", held: 1, buy: 300, sell: 150, modifier: "lame_horse", priceFactor: Scenario.Lame);
        s.Settings.NobleHorsesToKeep = 2;
        var plan = s.Plan();
        Assert.Equal(-1, plan.Row("mounts:lame").Change);               // the lame one goes …
        var noble = plan.Row("mounts:noble");
        Assert.Equal(1, noble.Mine);                                    // … and is not counted …
        Assert.Equal(1, noble.Change);                                  // … a healthy one takes its place
    }

    [Fact]
    public void A_quest_keeps_its_noble_horses_from_the_surplus_sale()
    {
        var s = Footmen(0).Mount("noble_a", "noble_horse", held: 3, buy: 3000, sell: 1500);
        s.Snap.QuestNeeds.Add(new QuestNeed
        {
            QuestId = "q_horses", Title = "Lord Needs Horses", Kind = QuestNeedKind.Items, Amount = 2, What = "Noble Horse",
            Ids = new List<string> { "noble_a" },
        });
        s.Settings.NobleHorsesToKeep = 1;
        var noble = s.Plan().Row("mounts:noble");
        Assert.Equal(-1, noble.Change);                                 // the surplus is 2, the quest keeps 2 of the 3
        var goal = RowGoal.Of(noble);
        Assert.Equal((2, true), (goal.Value, goal.IsQuest));
    }

    [Fact]
    public void The_price_book_is_sell_only_for_noble_horses_only_while_none_are_kept()
    {
        var settings = new StewardSettings();
        Assert.True(PriceBook.IsSellOnly(PriceBookGroup.NobleHorses, settings));
        Assert.False(PriceBook.IsSellOnly(PriceBookGroup.WarMounts, settings));
        settings.NobleHorsesToKeep = 1;
        Assert.False(PriceBook.IsSellOnly(PriceBookGroup.NobleHorses, settings));
        // Kept, they buy at the average buy price × the horse multiplier: the group always auto-fills.
        var book = PriceBook.Resolve("noble_a", PriceBookGroup.NobleHorses, settings, new AveragePrices(3000, 1500));
        Assert.Equal(3600, book.FinalMaxBuy);
    }
}
