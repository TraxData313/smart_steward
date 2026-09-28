using SmartSteward.Core.Execution;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// The troops section (PLAN step 16, DESIGN §2.8 — Anton 2026.09.28: "list me all the recruits available in the town —
/// don't worry about the troop limit, just show me that I'm above it — and add all my troops like with the goods, but
/// don't list every possible troop: list what is on offer at the top and what I have, so I can manage them, drop some of
/// mine and recruit new"). The live re-plan's acceptance cases with troops (a village among them) are in LivePlanTests.
/// </summary>
public class TroopPlanTests
{
    // ── Which rows, where ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Recruits_on_offer_sit_on_top_and_the_partys_other_troops_below()
    {
        var plan = new Scenario()
            .Troop("vlandian_recruit", inParty: 3, onOffer: 5, price: 10)
            .Troop("aserai_recruit", onOffer: 2, price: 12)
            .Troop("imperial_legionary", inParty: 10)
            .Troop("quest_escort", inParty: 1, canDismiss: false)           // bound to a quest, not on offer: not listed
            .Troop("bound_archer", inParty: 4, onOffer: 1, canDismiss: false) // on offer: listed, but never dismissed
            .Plan();

        Assert.Equal(new[] { "troops:aserai_recruit", "troops:bound_archer", "troops:vlandian_recruit" },
            plan.Section(PlanSectionKind.Recruits)!.Rows.Select(r => r.Id));
        Assert.Equal(new[] { "troops:imperial_legionary" }, plan.Section(PlanSectionKind.Troops)!.Rows.Select(r => r.Id));
        Assert.Null(plan.FindRow("troops:quest_escort"));

        var vlandian = plan.Row("troops:vlandian_recruit");
        Assert.Equal((3, 5, 5, 3), (vlandian.Mine, vlandian.Market!.Value, vlandian.MaxBuy, vlandian.MaxSell));
        var aserai = plan.Row("troops:aserai_recruit");
        Assert.Equal((0, 2, 0), (aserai.Mine, aserai.MaxBuy, aserai.MaxSell));
        var legionary = plan.Row("troops:imperial_legionary");
        Assert.Equal((10, 0, 10), (legionary.Mine, legionary.MaxBuy, legionary.MaxSell));
        Assert.Null(legionary.Market);                                       // "—": not on offer here
        Assert.Equal(0, plan.Row("troops:bound_archer").MaxSell);

        // Every row starts at 0: the steward never recruits or dismisses by itself.
        var troops = plan.Rows.Where(r => r.Type == RowType.Troop).ToList();
        Assert.Equal(4, troops.Count);
        Assert.All(troops, r => Assert.Equal(0, r.Change));
        Assert.All(plan.Section(PlanSectionKind.Recruits)!.Rows.Concat(plan.Section(PlanSectionKind.Troops)!.Rows),
            r => Assert.Equal(RowType.Troop, r.Type));
        Assert.False(plan.HasChanges);
        Assert.Empty(plan.Transactions);
    }

    [Fact]
    public void The_troops_section_comes_right_after_the_tavern()
    {
        var plan = Scenario.BusyTown().Troop("a", inParty: 5, onOffer: 3).Troop("c", inParty: 7).Plan();
        var kinds = plan.Sections.Select(x => x.Kind).ToList();
        Assert.Equal(new[] { PlanSectionKind.Tavern, PlanSectionKind.Recruits, PlanSectionKind.Troops, PlanSectionKind.Food },
            kinds.Take(4));
    }

    [Fact]
    public void Villages_have_troops_too_the_autonomous_steward_never_and_the_switch_hides_them()
    {
        var s = new Scenario().Village().Troop("a", inParty: 5, onOffer: 3).Troop("c", inParty: 7);
        Assert.Equal(2, s.Plan().Rows.Count(r => r.Type == RowType.Troop));  // headmen and rural notables offer volunteers

        var autonomous = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.DoesNotContain(autonomous.Rows, r => r.Type == RowType.Troop);

        s.Settings.ShowTroops = false;
        Assert.DoesNotContain(s.Plan().Rows, r => r.Type == RowType.Troop);
    }

    [Fact]
    public void A_closed_market_keeps_the_table_while_troops_can_be_managed()
    {
        // A village with nothing on sale: the game shuts its market ("There are no available products right now."), but its
        // headman still offers volunteers and the party can always dismiss — the table stays, the reason above it.
        var s = new Scenario().Village().Food("grain", held: 10).Troop("a", onOffer: 3);
        s.Snap.CanTrade = false;
        s.Snap.Market.Clear();
        Assert.True(PlanFooter.ShowsTable(s.Plan(), marketClosed: true));

        s.Snap.Troops.Clear();                                                // nothing anyone can click: the reason alone
        s.Troop("c", inParty: 4, canDismiss: false);
        Assert.False(PlanFooter.ShowsTable(s.Plan(), marketClosed: true));

        s.Troop("d", inParty: 2);                                            // one troop to dismiss is enough
        Assert.True(PlanFooter.ShowsTable(s.Plan(), marketClosed: true));
    }

    // ── The clamps ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Plus_recruits_up_to_what_is_on_offer_and_minus_dismisses_down_to_what_is_held()
    {
        var plan = new Scenario().Party(30).Troop("a", inParty: 3, onOffer: 5, price: 10).Troop("b", onOffer: 2)
            .Troop("c", inParty: 10).Plan();

        var all = plan.Increase("troops:a", EditSize.All);
        Assert.Equal(5, all.After);
        Assert.Equal(EditBlock.AllOnOffer, plan.Row("troops:a").IncreaseBlock);
        var more = plan.Increase("troops:a");
        Assert.Equal((5, EditBlock.AllOnOffer), (more.After, more.Block));

        Assert.Equal(0, plan.Decrease("troops:a", EditSize.All).After);    // ctrl stops at zero
        Assert.Equal(-3, plan.Decrease("troops:a", EditSize.All).After);   // then dismisses every man of the type
        Assert.Equal(EditBlock.AllDismissed, plan.Row("troops:a").DecreaseBlock);
        Assert.Equal(-3, plan.SetChange("troops:a", -50).After);

        Assert.Equal(EditBlock.NoneToDismiss, plan.Row("troops:b").DecreaseBlock);   // on offer, none in the party
        Assert.Equal(EditBlock.NotOnOfferHere, plan.Row("troops:c").IncreaseBlock);  // mine, not on offer here
        Assert.Equal(-10, plan.Decrease("troops:c", EditSize.All).After);
        Assert.Equal(30 - 3 - 10, plan.Totals.MembersAfter);
    }

    [Fact]
    public void Shift_stops_at_zero_a_plain_click_steps_through_it()
    {
        var plan = new Scenario().Troop("a", inParty: 8, onOffer: 5).Plan();
        Assert.Equal(-5, plan.Decrease("troops:a", EditSize.Five).After);
        Assert.Equal(0, plan.Increase("troops:a", EditSize.Five).After);
        Assert.Equal(-1, plan.Decrease("troops:a").After);
        Assert.Equal(0, plan.Increase("troops:a").After);
        Assert.Equal(1, plan.Increase("troops:a").After);
        Assert.Equal(-2, plan.SetChange("troops:a", -2).After);
        Assert.Equal(2, plan.SetChange("troops:a", 2).After);                 // straight across zero
        Assert.True(plan.Row("troops:a").IsTouched);
        plan.Reset("troops:a");
        Assert.Equal(0, plan.Row("troops:a").Change);                         // the steward's number is always 0
        Assert.False(plan.Row("troops:a").IsTouched);
    }

    // ── Prices and the purse ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Recruits_cost_their_price_per_man_and_dismissals_are_free()
    {
        var plan = new Scenario().Troop("a", inParty: 3, onOffer: 5, price: 20).Troop("c", inParty: 10).Plan();
        Assert.Equal("20", RowCells.Of(plan.Row("troops:a")).Price);           // on offer at 0: the price per man
        Assert.Equal("", RowCells.Of(plan.Row("troops:c")).Price);

        plan.SetChange("troops:a", 3);
        var a = plan.Row("troops:a");
        Assert.Equal(-60, a.GoldDelta);
        Assert.Equal((20, 20), (a.UnitPriceMin, a.UnitPriceMax));
        Assert.Equal(UiFormat.PriceCell(3, 20, 20, -60), RowCells.Of(a).Price);
        Assert.Equal(UiColors.ForChange(3), RowCells.Of(a).Color);

        plan.SetChange("troops:c", -4);
        var c = plan.Row("troops:c");
        Assert.Equal(0, c.GoldDelta);
        Assert.Equal("", RowCells.Of(c).Price);
        Assert.Equal(UiColors.ForChange(-4), RowCells.Of(c).Color);
        Assert.Equal("6", RowCells.Of(c).Result);

        Assert.Equal(60, plan.Totals.Spent);
        Assert.Equal(plan.Totals.GoldNow - 60, plan.Totals.GoldAfter);
        Assert.Equal(10 + 3 - 4, plan.Totals.MembersAfter);
    }

    [Fact]
    public void The_purse_limits_recruits_never_the_party_size_limit()
    {
        // 50 gold, 20 a man: two recruits (vanilla's recruit screen wants the cart's total ≤ the gold). The party limit of 11
        // is passed without a word from the buttons — the footer shows Party 12/11, red.
        var s = new Scenario().Gold(50).Troop("a", onOffer: 5, price: 20);
        s.Snap.Party.PartySizeLimit = 11;
        var plan = s.Plan();
        var all = plan.Increase("troops:a", EditSize.All);
        Assert.Equal((2, EditBlock.NotEnoughGold), (all.After, all.Block));
        Assert.Equal(EditBlock.NotEnoughGold, plan.Row("troops:a").IncreaseBlock);
        Assert.Equal(12, plan.Totals.MembersAfter);
        Assert.True(plan.Totals.OverPartyLimit);
        Assert.False(plan.Totals.CannotAfford);
        Assert.True(plan.Totals.BelowMinGoldAfterDeal);                        // a red flag, never a block

        var exact = new Scenario().Gold(40).Troop("a", onOffer: 5, price: 20).Plan();
        Assert.Equal(2, exact.Increase("troops:a", EditSize.All).After);       // 40 ≤ 40: both
        Assert.Equal(0, exact.Totals.GoldAfter);
    }

    [Fact]
    public void Recruits_are_paid_first_and_the_stewards_buys_give_way()
    {
        // 3,000 gold: the steward buys 20 grain (to 2 per man). Recruiting 30 men at 50 (1,500) still works — the food
        // for 40 men is planned with what the recruits leave, down to MinGoldAfterDeal.
        var s = new Scenario().Party(10).Gold(3_000).Food("grain", market: 1_000, buy: 10).Troop("a", onOffer: 30, price: 50);
        var plan = s.Plan();
        Assert.Equal(20, plan.Row("food:grain").Change);
        Assert.Equal(30, plan.Increase("troops:a", EditSize.All).After);
        Assert.Equal(50, plan.Row("food:grain").Change);                       // 3,000 − 1,500 − 1,000 floor = 500 of food
        Assert.Equal(1_000, plan.Totals.GoldAfter);
        Assert.Equal(80, plan.Facts.FoodTarget);
    }

    /// <summary>The troop rows' live blocks skip the trial walk (a big party lists dozens of types); they must say exactly
    /// what the trial would.</summary>
    [Fact]
    public void A_troop_rows_live_block_is_exactly_the_trial_walks()
    {
        var s = Scenario.BusyTown().Gold(9_000, marketGold: 5_000)
            .Troop("a", inParty: 3, onOffer: 5, price: 200)
            .Troop("b", onOffer: 40, price: 300, mounted: true)
            .Troop("c", inParty: 10, wounded: 2);
        var plan = s.Plan();
        var states = new List<Action>
        {
            () => { },
            () => plan.Increase("troops:b", EditSize.Five),
            () => plan.Increase("food:grain", EditSize.Five),
            () => plan.SetChange("troops:a", -2),
            () => plan.Increase("tavern:mercenaries", EditSize.All),
            () => plan.Increase("troops:b", EditSize.All),                        // the purse binds
            () => plan.Increase("tavern:wanderer:w1"),
            () => plan.Decrease("troops:c", EditSize.All),
            () => plan.Reset("food:grain"),
            () => plan.Decrease("troops:b", EditSize.Five),
        };
        int compared = 0, purseBound = 0;
        foreach (var step in states)
        {
            step();
            foreach (var row in plan.Rows.Where(r => r.Type == RowType.Troop).ToList())
                foreach (int direction in new[] { +1, -1 })
                {
                    int next = row.Change + direction;
                    if (row.Change * direction < 0 || next > row.MaxBuy || next < -row.MaxSell)
                        continue; // toward zero, or out of the row's range: never a trial
                    var live = direction > 0 ? row.IncreaseBlock : row.DecreaseBlock;
                    Assert.Equal(plan.Trial(row, next), live);
                    compared++;
                    if (live == EditBlock.NotEnoughGold) purseBound++;
                }
        }
        Assert.True(compared >= 20, "compared " + compared);
        Assert.True(purseBound > 0, "the purse never bound a recruit");
    }

    // ── The party after the deal ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Recruits_and_dismissals_re_plan_the_food_and_the_horses()
    {
        var s = new Scenario().Village().Party(20, footmen: 10).Gold(100_000)
            .Upgrade("footman", 8, ("war_horse", 3))
            .Food("grain", market: 500, buy: 10)
            .Mount("hunter", "horse", market: 100, buy: 200)
            .Mount("charger", "war_horse", market: 10, buy: 1500)
            .Troop("footman", inParty: 8, upgrade: "war_horse")
            .Troop("peasant", onOffer: 6, price: 10, upgrade: "horse")
            .Troop("rider", inParty: 5, mounted: true);
        s.Settings.AutoFillWarMountPrices = true;
        s.Settings.WarMountsHorseTarget = 2;                                   // a fixed 2 — once some troop needs a horse
        var plan = s.Plan();
        Assert.Equal((20, 10, 3), (plan.Facts.FoodEaters, plan.Facts.Footmen, plan.Facts.UpgradeNeed["war_horse"]));
        Assert.Null(plan.FindRow("mounts:upgrade:horse"));

        plan.Increase("troops:peasant", EditSize.All);                         // 6 foot recruits whose upgrade needs a horse
        Assert.Equal((26, 16), (plan.Facts.FoodEaters, plan.Facts.Footmen));
        Assert.Equal(52, plan.Facts.FoodTarget);
        Assert.Equal(2, plan.Row("mounts:upgrade:horse").Need);                // the kind comes in play, never ready
        Assert.Equal(0, plan.Facts.UpgradeReady["horse"]);

        plan.SetChange("troops:footman", -6);                                  // 6 of the 8 ready-to-upgrade footmen go
        Assert.Equal((20, 10), (plan.Facts.FoodEaters, plan.Facts.Footmen));
        Assert.Equal(2, plan.Facts.UpgradeNeed["war_horse"]);                  // 2 left: at most 2 ready
        Assert.Equal(2, plan.Row("mounts:upgrade:war_horse").Change);

        plan.Decrease("troops:rider", EditSize.All);                           // mounted men leave: fewer eaters, same footmen
        Assert.Equal((15, 10), (plan.Facts.FoodEaters, plan.Facts.Footmen));
        Assert.Equal(15, plan.Totals.MembersAfter);
        Assert.False(plan.Row("food:grain").IsTouched);                        // the steward's rows follow
        Assert.Equal(30, plan.Row("food:grain").Change);
    }

    [Fact]
    public void Dismissals_take_the_wounded_first_so_capacity_drops_only_after_them()
    {
        var s = new Scenario().Troop("a", inParty: 5, wounded: 3, onOffer: 4, price: 10, mounted: true, seaWeight: 50);
        s.Snap.Carry = new CarryInfo { WeightNow = 1_000, CapacityLandNow = 1_500, HasShips = true, WeightAtSeaNow = 1_200, CapacitySeaNow = 2_000 };
        GameRules.SetCarryRates(s.Snap.Carry, 0, 0, 0, false);
        var plan = s.Plan();

        plan.SetChange("troops:a", -3);                                        // the 3 wounded: nobody who carries
        var c = plan.Totals.Carry;
        Assert.Equal((1_500d, 2_000d), (c.CapacityLandAfter, c.CapacitySeaAfter));
        Assert.Equal(1_200 - 3 * 50, c.WeightAtSeaAfter);                     // their horses leave the ships all the same

        plan.SetChange("troops:a", -5);                                        // then 2 healthy men
        c = plan.Totals.Carry;
        Assert.Equal((1_500d - 2 * 20, 2_000d - 2 * 20), (c.CapacityLandAfter, c.CapacitySeaAfter));

        plan.SetChange("troops:a", 4);                                         // 4 recruits, healthy, mounted
        c = plan.Totals.Carry;
        Assert.Equal((1_500d + 4 * 20, 2_000d + 4 * 20), (c.CapacityLandAfter, c.CapacitySeaAfter));
        Assert.Equal(1_200 + 4 * 50, c.WeightAtSeaAfter);
        Assert.Equal(2, new TroopRowInfo { Wounded = 3 }.HealthyAmong(5));
    }

    [Fact]
    public void A_settings_re_plan_keeps_the_troop_edits()
    {
        var s = new Scenario().Party(20, footmen: 10).Troop("a", inParty: 3, onOffer: 5).Troop("c", inParty: 10);
        var plan = s.Plan();
        plan.SetChange("troops:a", 3);
        plan.SetChange("troops:c", -4);
        var carry = PlanCarryOver.Capture(plan);
        s.Settings.MountsPer100Footmen = 120;
        var fresh = s.Plan();
        carry.ApplyTo(fresh);
        Assert.Equal((3, -4), (fresh.Row("troops:a").Change, fresh.Row("troops:c").Change));
        Assert.Equal(19, fresh.Totals.MembersAfter);
        Assert.Equal(19, fresh.Facts.FoodEaters);
    }

    // ── Do it ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Transactions_run_prisoners_dismissals_trades_hires_then_recruits()
    {
        var s = Scenario.BusyTown().Troop("a", inParty: 3, onOffer: 5, price: 25).Troop("c", inParty: 10, wounded: 2);
        var plan = s.Plan();
        plan.SetChange("troops:a", 2);
        plan.SetChange("troops:c", -3);
        plan.Increase("tavern:mercenaries", EditSize.Five);
        plan.Increase("tavern:wanderer:w1");

        var kinds = plan.Transactions.Select(t => t.Kind).ToList();
        int Rank(TransactionKind k) => k switch
        {
            TransactionKind.Donate or TransactionKind.Ransom => 0,
            TransactionKind.Dismiss => 1,
            TransactionKind.Sell => 2,
            TransactionKind.Buy => 3,
            TransactionKind.HireWanderer => 4,
            TransactionKind.HireMercenaries => 5,
            _ => 6,
        };
        Assert.Equal(kinds.OrderBy(Rank).ToList(), kinds);
        foreach (var kind in new[] { TransactionKind.Ransom, TransactionKind.Dismiss, TransactionKind.Sell, TransactionKind.Buy,
                     TransactionKind.HireWanderer, TransactionKind.HireMercenaries, TransactionKind.Recruit })
            Assert.Contains(kind, kinds);

        var dismiss = plan.Transactions.Single(t => t.Kind == TransactionKind.Dismiss);
        Assert.Equal(("c", 3, 0, "troops:c"), (dismiss.TroopId, dismiss.Count, dismiss.Gold, dismiss.RowId));
        Assert.Empty(dismiss.UnitPrices);
        var recruit = plan.Transactions.Single(t => t.Kind == TransactionKind.Recruit);
        Assert.Equal(("a", 2, 50), (recruit.TroopId, recruit.Count, recruit.Gold));
        Assert.Equal(new[] { 25, 25 }, recruit.UnitPrices);
        Assert.Equal(TransactionKind.Recruit, kinds[^1]);
    }

    [Fact]
    public void The_executor_recruits_what_the_slots_and_the_purse_allow_and_dismisses_what_is_held()
    {
        Assert.Equal(3, ExecutionBudget.RecruitCount(5, slotsOpen: 3, gold: 1_000, pricePerMan: 20, out var r1));
        Assert.Equal(SkipReason.NotOnOffer, r1);
        Assert.Equal(2, ExecutionBudget.RecruitCount(5, slotsOpen: 10, gold: 50, pricePerMan: 20, out var r2));
        Assert.Equal(SkipReason.NotEnoughGold, r2);
        Assert.Equal(5, ExecutionBudget.RecruitCount(5, slotsOpen: 10, gold: 100, pricePerMan: 20, out var r3)); // total ≤ gold
        Assert.Equal(SkipReason.None, r3);

        Assert.Equal(3, ExecutionBudget.DismissCount(5, held: 3, out var d1));
        Assert.Equal(SkipReason.NotHeld, d1);
        Assert.Equal(2, ExecutionBudget.DismissCount(2, held: 10, out var d2));
        Assert.Equal(SkipReason.None, d2);
    }

    [Fact]
    public void The_log_tells_the_troop_rows_and_their_transactions()
    {
        var plan = new Scenario().Troop("a", inParty: 3, onOffer: 5, price: 20, upgrade: "horse")
            .Troop("c", inParty: 10, wounded: 2, mounted: true).Plan();
        plan.SetChange("troops:a", 3);
        plan.SetChange("troops:c", -4);
        var text = string.Join("\n", PlanReport.Full(plan));
        Assert.Contains("troops:a \"a\" Troop: mine 3, change +3 (yours, suggested +0), result 6, market 5", text);
        Assert.Contains("on offer 5 at 20, wage 2, on foot, upgrades need horse", text);
        Assert.Contains("troops:c \"c\" Troop: mine 10, change -4", text);
        Assert.Contains("not on offer, wage 2, 2 wounded, mounted", text);
        Assert.Contains("Dismiss 4 x c [troops:c] (free, the wounded first)", text);
        Assert.Contains("Recruit 3 x a [troops:a] = 60 (20)", text);
        Assert.Contains("party 9/100", text);

        var outcome = new TransactionOutcome(plan.Transactions.Single(t => t.Kind == TransactionKind.Dismiss));
        outcome.AddUnits(4, 0);
        Assert.Equal("Dismiss c x4 [troops:c]: done 4, free", ExecutionReport.Describe(outcome));
    }
}
