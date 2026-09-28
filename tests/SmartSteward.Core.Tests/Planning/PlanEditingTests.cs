using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>The player's hand on the plan (DESIGN §1.1, PLAN step 4b): steps, clamps, reset, live re-pricing,
/// the money flags, the tavern, and the executor's transaction list.</summary>
public class PlanEditingTests
{
    // ── Steps and clamps ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Click_shift_and_ctrl_move_one_five_and_all()
    {
        var plan = new Scenario().Village().Party(10).Food("grain", held: 5, market: 100, buy: 10).Plan();
        var row = plan.Row("food:grain");
        Assert.Equal(15, row.Change); // target 20, 5 held

        Assert.Equal(16, plan.Increase("food:grain").After);
        Assert.Equal(-160, row.GoldDelta);
        Assert.Equal(21, plan.Increase("food:grain", EditSize.Five).After);
        Assert.Equal(16, plan.Decrease("food:grain", EditSize.Five).After);
        Assert.Equal(15, plan.Decrease("food:grain").After);
        Assert.False(row.IsEdited);

        var all = plan.Increase("food:grain", EditSize.All);
        Assert.Equal(100, all.After);           // everything on offer within the price
        Assert.Equal(EditBlock.None, all.Block);
        Assert.Equal(-1_000, row.GoldDelta);
        Assert.True(row.IsEdited);
    }

    [Fact]
    public void Buying_stops_at_the_market_stock_and_selling_at_what_is_held()
    {
        var plan = new Scenario().Village().Party(10).Food("grain", held: 5, market: 100, buy: 10).Plan();
        var row = plan.Row("food:grain");
        plan.Increase("food:grain", EditSize.All);
        var more = plan.Increase("food:grain");
        Assert.False(more.Moved);
        Assert.Equal(EditBlock.AllOnOffer, more.Block);
        Assert.Equal(EditBlock.AllOnOffer, row.IncreaseBlock);

        Assert.Equal(0, plan.Decrease("food:grain", EditSize.All).After);   // ctrl stops at zero first
        var sellAll = plan.Decrease("food:grain", EditSize.All);
        Assert.Equal(-5, sellAll.After);                                    // then sells everything held
        Assert.Equal(40, row.GoldDelta);
        var sellMore = plan.Decrease("food:grain");
        Assert.Equal(-5, sellMore.After);
        Assert.Equal(EditBlock.AllSold, sellMore.Block);
        Assert.False(row.CanDecrease);
        Assert.True(row.CanIncrease);
    }

    [Fact]
    public void A_click_crosses_zero_but_shift_and_ctrl_stop_there()
    {
        var plan = new Scenario().Village().Party(10).Food("grain", held: 5, market: 100, buy: 10).Plan();
        plan.Decrease("food:grain", EditSize.All); // 15 → 0
        Assert.Equal(-1, plan.Decrease("food:grain").After);
        Assert.Equal(0, plan.Increase("food:grain").After);
        Assert.Equal(1, plan.Increase("food:grain").After);

        plan.Increase("food:grain"); // 2
        var shift = plan.Decrease("food:grain", EditSize.Five);
        Assert.Equal(0, shift.Asked);
        Assert.Equal(0, shift.After);
        plan.Decrease("food:grain", EditSize.Five); // −5, all held
        Assert.Equal(0, plan.Increase("food:grain", EditSize.All).After);
    }

    [Fact]
    public void Loot_and_prisoners_only_sell_and_tavern_rows_only_hire()
    {
        var plan = Scenario.BusyTown().Plan();
        var armour = plan.Row("loot:Armour");
        Assert.Equal(-34, armour.Change);                       // every sellable piece
        Assert.Equal(EditBlock.AllSold, armour.DecreaseBlock);
        Assert.Equal(0, plan.Increase("loot:Armour", EditSize.All).After);
        var up = plan.Increase("loot:Armour");
        Assert.Equal(0, up.After);
        Assert.Equal(EditBlock.SellOnly, up.Block);

        Assert.Equal(-8, plan.Row("prisoner:looter").Change);
        Assert.Equal(EditBlock.AllSold, plan.Row("prisoner:looter").DecreaseBlock);
        plan.Increase("prisoner:looter", EditSize.All);
        Assert.Equal(EditBlock.SellOnly, plan.Row("prisoner:looter").IncreaseBlock);
        Assert.Equal(-5, plan.Decrease("prisoner:looter", EditSize.Five).After);

        Assert.Equal(EditBlock.BuyOnly, plan.Row("tavern:wanderer:w1").DecreaseBlock);
        Assert.Equal(EditBlock.BuyOnly, plan.Decrease("tavern:mercenaries").Block);
    }

    [Fact]
    public void Mount_role_rows_buy_the_cheapest_eligible_and_sell_the_dearest_unreserved()
    {
        var plan = new Scenario().Village().Party(10, footmen: 5)
            .Mount("hunter", "horse", held: 5, market: 10, buy: 200, sell: 100)
            .Mount("charger", "war_horse", held: 1, sell: 800)
            .Mount("aserai_horse", "horse", market: 10, buy: 260)
            .Plan();
        var riding = plan.Row("mounts:riding");
        Assert.Equal(0, riding.Change);                    // 6 held = ceil(5 × 1.1)

        plan.Decrease(riding.Id);
        Assert.Equal(-1, riding.Moved("charger"));         // the dearest goes first
        plan.Decrease(riding.Id);
        Assert.Equal(-1, riding.Moved("hunter"));
        Assert.Equal(900, riding.GoldDelta);

        plan.Increase(riding.Id, EditSize.All);            // back to zero
        plan.Increase(riding.Id, EditSize.Five);
        Assert.Equal(5, riding.Moved("hunter"));           // the cheapest eligible
        Assert.Equal(0, riding.Moved("aserai_horse"));
        Assert.Equal(1_000, -riding.GoldDelta);
    }

    [Fact]
    public void Loot_rows_clamp_to_the_unlocked_pieces()
    {
        var s = new Scenario().Loot("rags", LootGroup.Armour, held: 10, sell: 8)
            .Loot("helm", LootGroup.Armour, held: 3, sell: 60, locked: true);
        s.Settings.SellLoot = true;
        var plan = s.Plan();
        var armour = plan.Row("loot:Armour");
        Assert.Equal(-10, armour.Change);
        plan.Increase(armour.Id, EditSize.All);
        var all = plan.Decrease(armour.Id, EditSize.All);
        Assert.Equal(-10, all.After);
        Assert.Equal(EditBlock.AllSold, armour.DecreaseBlock);
        Assert.Equal(0, armour.Moved("helm"));
        Assert.Equal(3, armour.Locked);
    }

    [Fact]
    public void A_hero_left_at_zero_can_be_ransomed_by_hand()
    {
        var plan = Scenario.BusyTown().Plan();
        int gold = plan.Totals.GoldAfter;
        Assert.Equal(-1, plan.Decrease("prisoner:lord_x").After);
        Assert.Equal(3000, plan.Row("prisoner:lord_x").GoldDelta);
        Assert.Equal(gold + 3000, plan.Totals.GoldAfter);
        Assert.Equal(EditBlock.AllSold, plan.Row("prisoner:lord_x").DecreaseBlock);
    }

    [Fact]
    public void A_row_with_nothing_on_offer_says_so()
    {
        var plan = new Scenario().Party(10).Food("grain", held: 22).Plan(); // target 20, sold only above 25
        var row = plan.Row("food:grain");
        Assert.Equal(0, row.Change);
        Assert.Equal(EditBlock.NoneEligible, row.IncreaseBlock);
        Assert.Equal(EditBlock.None, row.DecreaseBlock);

        var unknown = Record.Exception(() => plan.Increase("food:nothing"));
        Assert.IsType<ArgumentException>(unknown);
    }

    // ── Reset ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Reset_returns_a_row_to_the_suggestion()
    {
        var plan = new Scenario().Village().Party(10).Food("grain", held: 5, market: 100, buy: 10).Plan();
        string before = plan.Describe();
        plan.Increase("food:grain", EditSize.Five);
        Assert.True(plan.IsEdited);
        var reset = plan.Reset("food:grain");
        Assert.Equal(15, reset.After);
        Assert.False(plan.IsEdited);
        Assert.Equal(before, plan.Describe());

        plan.Decrease("food:grain", EditSize.All);
        plan.Decrease("food:grain", EditSize.All); // −5: crossing back needs zero first
        Assert.Equal(15, plan.Reset("food:grain").After);
        Assert.False(plan.Reset("food:grain").Moved);
    }

    [Fact]
    public void Reset_takes_back_only_what_no_other_row_took_since_and_ResetAll_restores_everything()
    {
        var plan = SharedStock().Plan();
        string suggestion = plan.Describe();
        plan.Decrease("mounts:upgrade:horse", EditSize.All);   // 2 → 0
        plan.Increase("mounts:riding", EditSize.All);          // 1 → all 3 on offer
        Assert.Equal(3, plan.Row("mounts:riding").Change);

        var reset = plan.Reset("mounts:upgrade:horse");
        Assert.Equal(0, reset.After);
        Assert.Equal(EditBlock.NeededByAnotherRow, reset.Block);
        Assert.True(plan.Row("mounts:upgrade:horse").IsEdited);

        plan.ResetAll();
        Assert.False(plan.IsEdited);
        Assert.Equal(suggestion, plan.Describe());
    }

    // ── Live re-pricing ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void In_a_town_editing_one_row_re_prices_another_row_of_the_same_category()
    {
        var s = new Scenario().Party(10, footmen: 10).Upgrade("recruit", 10, ("horse", 2))
            .Mount("hunter", "horse", market: 20, buy: 200);
        s.Oracle.Slope = 0.00005; // +1% per horse bought (store value 200)
        var plan = s.Plan();
        var riding = plan.Row("mounts:riding");
        var upgrade = plan.Row("mounts:upgrade:horse");
        Assert.Equal(9, riding.Change);                      // 11 − 2 pledged
        Assert.Equal(2, upgrade.Change);
        Assert.Equal(218, upgrade.UnitPriceMin);              // after the 9 riding horses: 200 + 2 × 9
        Assert.Equal(-(218 + 220), upgrade.GoldDelta);

        plan.Decrease("mounts:riding");
        Assert.Equal(2, upgrade.Change);                      // not re-planned…
        Assert.Equal(-(216 + 218), upgrade.GoldDelta);        // …but re-priced
        Assert.Equal(-Enumerable.Range(0, 8).Sum(k => 200 + 2 * k), riding.GoldDelta);

        plan.Increase("mounts:riding", EditSize.Five);
        Assert.Equal(-(226 + 228), upgrade.GoldDelta);
        Assert.Equal(100_000 + riding.GoldDelta + upgrade.GoldDelta, plan.Totals.GoldAfter);
    }

    [Fact]
    public void Rows_of_other_categories_keep_their_prices_and_villages_stay_flat()
    {
        foreach (var village in new[] { false, true })
        {
            var s = new Scenario().Party(10).Food("grain", market: 100, buy: 10).Food("fish", market: 100, buy: 12);
            if (village) s.Village();
            else s.Oracle.Slope = 0.001;
            var plan = s.Plan();
            var fish = plan.Row("food:fish");
            int fishGold = fish.GoldDelta;
            Assert.Equal(10, fish.Change);                   // balanced: 10 + 10
            plan.Increase("food:grain", EditSize.Five);
            Assert.Equal(15, plan.Row("food:grain").Change);
            Assert.Equal(fishGold, fish.GoldDelta);
            if (village)
            {
                Assert.Equal(-150, plan.Row("food:grain").GoldDelta);
                Assert.Equal(10, plan.Row("food:grain").UnitPriceMax);
            }
        }
    }

    [Fact]
    public void Town_prices_climbing_past_the_max_buy_stop_a_row()
    {
        var s = new Scenario().Party(1).Food("grain", market: 100, buy: 100);
        s.Settings.FoodPerMan = 3;
        s.Oracle.Slope = 0.001; // 100, 110, 120, 130 — the max buy is 120
        var plan = s.Plan();
        Assert.Equal(3, plan.Row("food:grain").Change);
        Assert.Equal(100, plan.Row("food:grain").MaxBuy);   // the static bound…
        var result = plan.Increase("food:grain", EditSize.All);
        Assert.Equal(3, result.After);                      // …the walk stops sooner
        Assert.Equal(EditBlock.PriceLimit, result.Block);
        Assert.Equal(EditBlock.PriceLimit, plan.Row("food:grain").IncreaseBlock);
    }

    [Fact]
    public void Town_prices_falling_below_the_min_sell_stop_a_sale()
    {
        var s = new Scenario().Party(10).Food("grain", held: 100, buy: 100, sell: 100);
        s.Oracle.Slope = 0.001; // 100, 90, 80, 70 — the min sell is 80
        var plan = s.Plan();
        Assert.Equal(-3, plan.Row("food:grain").Change);
        Assert.Equal(EditBlock.BelowMinSellPrice, plan.Decrease("food:grain").Block);
    }

    [Fact]
    public void An_edit_never_takes_stock_another_row_has()
    {
        var plan = SharedStock().Plan();
        var riding = plan.Row("mounts:riding");
        Assert.Equal(1, riding.Change);
        Assert.Equal(3, riding.MaxBuy);
        Assert.Equal(EditBlock.NeededByAnotherRow, riding.IncreaseBlock);
        var result = plan.Increase("mounts:riding", EditSize.All);
        Assert.False(result.Moved);
        Assert.Equal(EditBlock.NeededByAnotherRow, result.Block);
        Assert.Equal(2, plan.Row("mounts:upgrade:horse").Change);
    }

    [Fact]
    public void Lowering_a_sale_can_cut_a_buy_that_only_its_lower_price_allowed()
    {
        // Selling 3 hunters lowers the town's horse prices; the player then buys upgrade horses up to the max
        // buy (240). Taking the sale back raises the prices again, so the buy is cut to what still fits.
        var s = new Scenario().Party(10).Upgrade("recruit", 10, ("horse", 5))
            .Mount("hunter", "horse", held: 8, market: 10, buy: 200, sell: 100);
        s.Oracle.Slope = 0.0002;
        var plan = s.Plan();
        Assert.Equal(-3, plan.Row("mounts:riding").Change);   // 8 held, 5 reserved, footmen 0
        var more = plan.Increase("mounts:upgrade:horse", EditSize.All);
        Assert.Equal(9, more.After);                          // 176 … 240
        Assert.Equal(EditBlock.PriceLimit, more.Block);

        plan.Increase("mounts:riding", EditSize.All);         // the sale taken back
        Assert.Equal(0, plan.Row("mounts:riding").Change);
        Assert.Equal(6, plan.Row("mounts:upgrade:horse").Change); // 200 … 240
        Assert.True(plan.Transactions.All(t => t.UnitPrices.All(p => p <= 240)));
    }

    // ── The market's gold ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sales_stay_within_the_market_gold_after_edits()
    {
        var s = new Scenario().Village().Party(10).Gold(100_000, marketGold: 1_000)
            .Food("grain", held: 60, sell: 8)
            .Loot("rags", LootGroup.Armour, held: 200, sell: 8);
        s.Settings.SellLoot = true;
        var plan = s.Plan();
        Assert.Equal(-40, plan.Row("food:grain").Change);   // 320
        Assert.Equal(-85, plan.Row("loot:Armour").Change);  // + 680 = the market's 1,000

        Assert.Equal(EditBlock.MarketOutOfGold, plan.Row("food:grain").DecreaseBlock); // the rags need that gold
        Assert.Equal(EditBlock.MarketOutOfGold, plan.Decrease("loot:Armour", EditSize.All).Block);

        plan.Increase("loot:Armour", EditSize.Five);        // frees 40
        Assert.Equal(-45, plan.Decrease("food:grain", EditSize.Five).After);
        var more = plan.Decrease("food:grain");
        Assert.Equal(-45, more.After);
        Assert.Equal(EditBlock.MarketOutOfGold, more.Block);
        Assert.Equal(1_000, plan.Totals.MarketSales);
        Assert.False(plan.Totals.ExceedsMarketGold);
    }

    // ── The purse and the floors ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Breaking_a_floor_by_hand_is_flagged_never_blocked()
    {
        var plan = new Scenario().Party(10).Gold(1_500).Food("grain", market: 100, buy: 10).Plan();
        Assert.Equal(20, plan.Row("food:grain").Change);
        Assert.False(plan.Totals.BelowMinGoldAfterDeal);
        Assert.Equal(100, plan.Increase("food:grain", EditSize.All).After);
        Assert.Equal(500, plan.Totals.GoldAfter);
        Assert.True(plan.Totals.BelowMinGoldAfterDeal);
        Assert.False(plan.Totals.BelowMinGoldForHorses);   // no animal bought
        Assert.False(plan.Totals.CannotAfford);

        plan = new Scenario().Party(10).Gold(6_000).Pack("mule", market: 20, buy: 150).Plan();
        Assert.Equal(6, plan.Row("mounts:pack").Change);   // down to 5,100
        Assert.Equal(7, plan.Increase("mounts:pack").After);
        Assert.Equal(4_950, plan.Totals.GoldAfter);
        Assert.True(plan.Totals.BelowMinGoldForHorses);
        Assert.False(plan.Totals.BelowMinGoldAfterDeal);
    }

    [Fact]
    public void An_empty_purse_blocks_buying()
    {
        var plan = new Scenario().Party(10).Gold(300).Food("grain", market: 100, buy: 10).Plan();
        Assert.Equal(0, plan.Row("food:grain").Change);    // below MinGoldAfterDeal: the steward buys nothing
        var all = plan.Increase("food:grain", EditSize.All);
        Assert.Equal(30, all.After);                       // the player may — as far as the purse goes
        Assert.Equal(EditBlock.NotEnoughGold, all.Block);
        Assert.Equal(0, plan.Totals.GoldAfter);
        Assert.False(plan.Totals.CannotAfford);
        Assert.Equal(EditBlock.NotEnoughGold, plan.Row("food:grain").IncreaseBlock);
    }

    [Fact]
    public void Taking_back_income_may_leave_the_deal_unaffordable_and_says_so()
    {
        var plan = new Scenario().Party(10).Gold(300).Food("grain", market: 100, buy: 10)
            .Prisoner("looter", 5, 20).Plan();
        Assert.Equal(40, plan.Increase("food:grain", EditSize.All).After); // 300 + the ransom's 100
        Assert.Equal(0, plan.Increase("prisoner:looter", EditSize.All).After); // lowering always works
        Assert.Equal(-100, plan.Totals.GoldAfter);
        Assert.True(plan.Totals.CannotAfford);
        Assert.Equal(40, plan.Row("food:grain").Change);   // nothing is re-planned behind the player's back
        Assert.Equal(EditBlock.NotEnoughGold, plan.Row("food:grain").IncreaseBlock);
        Assert.True(plan.Row("prisoner:looter").CanDecrease);
    }

    // ── The tavern ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Wanderers_toggle_and_share_the_companion_slots_never_the_party_room()
    {
        var plan = Tavern().Plan();
        var arn = plan.Row("tavern:wanderer:w1");
        var bea = plan.Row("tavern:wanderer:w2");
        var merc = plan.Row("tavern:mercenaries");

        Assert.Equal(1, plan.Increase(arn.Id, EditSize.All).After);
        Assert.Equal(-700, arn.GoldDelta);
        Assert.Equal(700, plan.Totals.Spent);
        Assert.Equal(11, plan.Totals.MembersAfter);
        Assert.Equal(EditBlock.AllOnOffer, arn.IncreaseBlock);    // a wanderer is a 0/1 toggle
        Assert.Equal(EditBlock.CompanionLimit, bea.IncreaseBlock); // the one free slot is Arn's
        Assert.Equal(EditBlock.CompanionLimit, plan.Increase(bea.Id).Block);

        // Room 5 - but the party size limit is information, not a wall (round 3): all 20 on offer.
        var mercs = plan.Increase(merc.Id, EditSize.All);
        Assert.Equal(20, mercs.After);
        Assert.Equal(EditBlock.AllOnOffer, merc.IncreaseBlock);
        Assert.Equal(-2400, merc.GoldDelta);
        Assert.Equal(31, plan.Totals.MembersAfter);
        Assert.Equal(15, plan.Totals.PartySizeLimit);
        Assert.True(plan.Totals.OverPartyLimit);

        Assert.Equal(0, plan.Decrease(arn.Id).After);
        Assert.Equal(20, merc.Change);                             // stays as edited
        Assert.Equal(EditBlock.AllOnOffer, plan.Increase(merc.Id).Block);
        Assert.Equal(EditBlock.None, arn.IncreaseBlock);           // the slot is free again; the room never mattered
        Assert.Equal(EditBlock.None, bea.IncreaseBlock);
        Assert.Equal(30, plan.Totals.MembersAfter);
    }

    [Theory]
    [InlineData(700, false)] // vanilla wants MORE gold than the price
    [InlineData(701, true)]
    public void A_wanderer_needs_more_gold_than_his_price(int gold, bool hired)
    {
        var plan = Tavern().Gold(gold).Plan();
        var result = plan.Increase("tavern:wanderer:w1");
        Assert.Equal(hired, result.Moved);
        Assert.Equal(hired ? EditBlock.None : EditBlock.NotEnoughGold, result.Block);
    }

    [Fact]
    public void A_full_party_blocks_no_hire_and_the_totals_say_it_is_over()
    {
        var s = Tavern();
        s.Snap.Party.PartySizeLimit = 10;
        var plan = s.Plan();
        Assert.False(plan.Totals.OverPartyLimit);                  // 10/10: at the limit is not over
        Assert.Equal(EditBlock.None, plan.Row("tavern:mercenaries").IncreaseBlock);
        var hire = plan.Increase("tavern:wanderer:w1");
        Assert.True(hire.Moved);
        Assert.Equal(EditBlock.None, hire.Block);
        Assert.Equal(5, plan.Increase("tavern:mercenaries", EditSize.Five).After);
        Assert.Equal(16, plan.Totals.MembersAfter);                // 10 + Arn + 5 mercenaries
        Assert.True(plan.Totals.OverPartyLimit);
        Assert.Contains(plan.Transactions, t => t.Kind == TransactionKind.HireMercenaries && t.Count == 5);
        Assert.Contains(PlanReport.Full(plan), line => line.Contains("party 16/10 (over the limit)"));

        Assert.Equal(0, plan.Decrease("tavern:mercenaries", EditSize.All).After);
        Assert.Equal(0, plan.Decrease("tavern:wanderer:w1").After);
        Assert.False(plan.Totals.OverPartyLimit);
    }

    // ── Prisoners ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_dungeon_room_is_re_split_after_every_edit()
    {
        var s = new Scenario().Party(10).Prisoner("looter", 8, 20, influence: 1).Prisoner("bandit", 4, 50, influence: 2);
        s.Settings.DonatePrisonersWhenPossible = true;
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = 5 };
        var plan = s.Plan();
        var looter = plan.Row("prisoner:looter");
        Assert.Equal(4, plan.Row("prisoner:bandit").Prisoner!.DonateCount);
        Assert.Equal(1, looter.Prisoner!.DonateCount);
        Assert.Equal(7, looter.Prisoner.RansomCount);

        plan.Increase("prisoner:bandit", EditSize.All);   // the bandits stay
        Assert.Equal(5, looter.Prisoner.DonateCount);
        Assert.Equal(3, looter.Prisoner.RansomCount);
        Assert.Equal(60, looter.GoldDelta);
        Assert.Equal(5, looter.InfluenceDelta);
        Assert.Equal(-8, looter.Change);

        plan.Decrease("prisoner:bandit", EditSize.Five);  // clamped to the 4 held
        Assert.Equal(-4, plan.Row("prisoner:bandit").Change);
        Assert.Equal(1, looter.Prisoner.DonateCount);
    }

    [Fact]
    public void Without_ransom_the_dungeon_room_caps_the_prisoner_rows()
    {
        var s = new Scenario().Party(10).Prisoner("looter", 8, 20).Prisoner("bandit", 4, 50);
        s.Settings.RansomPrisoners = false;
        s.Settings.DonatePrisonersWhenPossible = true;
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = 5 };
        var plan = s.Plan();
        Assert.Equal(-1, plan.Row("prisoner:looter").Change);   // the bandits took 4 of the 5 places
        Assert.Equal(EditBlock.DungeonFull, plan.Decrease("prisoner:looter").Block);

        plan.Increase("prisoner:bandit", EditSize.All);
        Assert.Equal(-5, plan.Decrease("prisoner:looter", EditSize.All).After);
        Assert.Equal(EditBlock.DungeonFull, plan.Row("prisoner:looter").DecreaseBlock);
    }

    // ── Totals ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_footer_follows_every_edit()
    {
        var s = new Scenario().Party(10).Food("grain", held: 10, market: 100, buy: 10, weight: 2)
            .Loot("rags", LootGroup.Armour, held: 10, sell: 8, weight: 5);
        s.Settings.SellLoot = true;
        s.Snap.Party.DailyFoodUse = 0.5;
        var plan = s.Plan();
        Assert.Equal(20, plan.Totals.FoodUnitsAfter);

        plan.Increase("food:grain", EditSize.Five);
        plan.Increase("loot:Armour", EditSize.Five);
        var totals = plan.Totals;
        Assert.Equal(25, totals.FoodUnitsAfter);
        Assert.Equal(50.0, totals.FoodDaysAfter!.Value, 6);
        Assert.Equal(30, totals.WeightAdded);      // 15 grain × 2
        Assert.Equal(25, totals.WeightFreed);      // 5 rags × 5
        Assert.Equal(150, totals.Spent);
        Assert.Equal(40, totals.Earned);
        Assert.Equal(100_000 - 110, totals.GoldAfter);
        Assert.Equal(-110, totals.GoldChange);
    }

    // ── Transactions ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Transactions_run_prisoners_sales_buys_then_hires_with_stacks_counts_and_prices()
    {
        var s = Tavern()
            .Prisoner("looter", 8, 20)
            .Food("grain", held: 60, sell: 8)
            .Loot("rags", LootGroup.Armour, held: 10, sell: 8)
            .Pack("mule", market: 20, buy: 150);
        s.Settings.SellLoot = true;
        s.Settings.DonatePrisonersWhenPossible = true;
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = 3 };
        var plan = s.Plan();
        plan.Increase("tavern:wanderer:w1");
        plan.Increase("tavern:mercenaries", EditSize.Five);  // room 5 − Arn = 4, but the limit never clamps (round 3)

        var list = plan.Transactions;
        Assert.Equal(
            new[]
            {
                TransactionKind.Donate, TransactionKind.Ransom, TransactionKind.Sell, TransactionKind.Sell,
                TransactionKind.Buy, TransactionKind.HireWanderer, TransactionKind.HireMercenaries,
            },
            list.Select(t => t.Kind));
        Assert.Equal(("looter", 3, 3.0), (list[0].TroopId, list[0].Count, list[0].Influence));
        Assert.Equal(("looter", 5, 100), (list[1].TroopId, list[1].Count, list[1].Gold));
        Assert.Equal(("grain", 40, 320), (list[2].StackKey, list[2].Count, list[2].Gold));
        Assert.Equal(("rags", 10, 80), (list[3].StackKey, list[3].Count, list[3].Gold));
        Assert.Equal(("mule", 10, 1_500), (list[4].StackKey, list[4].Count, list[4].Gold));
        Assert.Equal(("w1", 1, 700), (list[5].HeroId, list[5].Count, list[5].Gold));
        Assert.Equal(("merc", 5, 600), (list[6].TroopId, list[6].Count, list[6].Gold));
        Assert.All(list.Where(t => t.Kind != TransactionKind.Donate), t => Assert.Equal(t.Count, t.UnitPrices.Count));
        Assert.Equal(plan.Totals.GoldChange,
            list.Sum(t => t.Kind is TransactionKind.Ransom or TransactionKind.Sell ? t.Gold : -t.Gold));
    }

    [Fact]
    public void Transactions_follow_the_edits_and_group_the_walk_by_category()
    {
        // Balanced buying interleaves grain and fish unit by unit; they are different categories, so the
        // executor gets one transaction each, with every unit's price in the walk's order.
        var s = new Scenario().Party(10).Food("grain", market: 100, buy: 10).Food("fish", market: 100, buy: 12);
        s.Oracle.Slope = 0.001;
        var plan = s.Plan();
        var buys = plan.Transactions;
        Assert.Equal(new[] { "grain", "fish" }, buys.Select(t => t.StackKey));
        Assert.Equal(plan.Row("food:grain").Tallies.Single().Gold, buys[0].Gold);
        Assert.Equal(buys[0].UnitPrices.OrderBy(p => p), buys[0].UnitPrices); // the town walk climbs

        plan.Decrease("food:fish", EditSize.All);
        Assert.Equal(new[] { "grain" }, plan.Transactions.Select(t => t.StackKey));
        Assert.Equal(10, plan.Transactions[0].Count);
    }

    // ── The round trip ───────────────────────────────────────────────────────────────────────────────

    public static TheoryData<string> Scenarios => new()
    {
        "busy town", "busy village", "poor village loot", "cheapest food", "pledge shrinks", "donations",
        "market gold", "price limit", "surplus sales",
    };

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void No_edits_walk_again_to_exactly_the_planners_plan(string name)
    {
        var plan = Build(name).Plan();
        string suggestion = plan.Describe();
        var transactions = Flatten(plan.Transactions);

        // Transactions = the planner's own tallies, stack by stack.
        foreach (var row in plan.Rows)
        {
            foreach (var tally in row.Tallies)
            {
                var kind = tally.Direction == Core.Pricing.TradeDirection.Buy ? TransactionKind.Buy : TransactionKind.Sell;
                var mine = plan.Transactions.Where(t => t.RowId == row.Id && t.StackKey == tally.Stack.Key && t.Kind == kind).ToList();
                Assert.Equal(tally.Count, mine.Sum(t => t.Count));
                Assert.Equal(tally.Gold, mine.Sum(t => t.Gold));
            }
            if (row.Prisoner != null)
            {
                Assert.Equal(row.Prisoner.RansomCount, plan.Transactions.Where(t => t.RowId == row.Id && t.Kind == TransactionKind.Ransom).Sum(t => t.Count));
                Assert.Equal(row.Prisoner.DonateCount, plan.Transactions.Where(t => t.RowId == row.Id && t.Kind == TransactionKind.Donate).Sum(t => t.Count));
            }
        }
        Assert.Equal(plan.Totals.MarketSales, plan.Transactions.Where(t => t.Kind == TransactionKind.Sell).Sum(t => t.Gold));

        // Walking the plan again changes nothing — every row, price, total and transaction.
        plan.ResetAll();
        Assert.Equal(suggestion, plan.Describe());
        Assert.Equal(transactions, Flatten(plan.Transactions));
        Assert.False(plan.IsEdited);
    }

    [Fact]
    public void Every_button_of_an_unedited_plan_knows_whether_it_works()
    {
        var plan = Scenario.BusyTown().Plan();
        string suggestion = plan.Describe();
        foreach (var row in plan.Rows)
        {
            _ = row.IncreaseBlock;
            _ = row.DecreaseBlock;
        }
        Assert.Equal(suggestion, plan.Describe()); // asking is a what-if: it never moves the plan
        Assert.Equal(EditBlock.None, plan.Row("loot:Armour").IncreaseBlock);   // sell less: always
        Assert.Equal(EditBlock.AllSold, plan.Row("loot:Armour").DecreaseBlock);
        Assert.Equal(EditBlock.None, plan.Row("tavern:wanderer:w1").IncreaseBlock);
        Assert.Equal(EditBlock.BuyOnly, plan.Row("tavern:wanderer:w1").DecreaseBlock);
    }

    // ── Scenarios ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Three hunters on offer, a riding row that needs one and an upgrade row that needs two.</summary>
    private static Scenario SharedStock() =>
        new Scenario().Village().Party(10, footmen: 2).Upgrade("recruit", 10, ("horse", 2))
            .Mount("hunter", "horse", market: 3, buy: 200);

    /// <summary>Two wanderers (700, 800), a band of 20 mercenaries at 120; room 5, one companion slot.</summary>
    private static Scenario Tavern()
    {
        var s = new Scenario().Party(10);
        s.Snap.Party.PartySizeLimit = 15;
        s.Snap.Party.CompanionSlotsFree = 1;
        s.Snap.Tavern = new TavernInfo
        {
            Wanderers =
            {
                new WandererForHire { HeroId = "w1", Name = "Arn", HirePrice = 700, DailyWage = 10 },
                new WandererForHire { HeroId = "w2", Name = "Bea", HirePrice = 800, DailyWage = 12 },
            },
            Mercenaries = new MercenaryOffer
            {
                TroopId = "merc", Name = "Blades", Available = 20, PricePerMan = 120, WagePerMan = 5, InParty = 2,
            },
        };
        return s;
    }

    private static Scenario Build(string name)
    {
        switch (name)
        {
            case "busy town":
                return Scenario.BusyTown();
            case "busy village":
            {
                var s = Scenario.BusyTown().Village();
                s.Oracle.Slope = 0;
                return s;
            }
            case "poor village loot":
            {
                var s = new Scenario().Village().Gold(100_000, marketGold: 300)
                    .Loot("sword", LootGroup.MeleeWeapons, held: 1, sell: 500)
                    .Loot("dagger", LootGroup.MeleeWeapons, held: 5, sell: 50)
                    .Loot("helm", LootGroup.Armour, held: 10, sell: 5, weight: 1)
                    .Loot("rags", LootGroup.Armour, held: 10, sell: 10, weight: 10);
                s.Settings.SellLoot = true;
                s.Settings.SellLootOrder = SellLootOrder.MostExpensive;
                return s;
            }
            case "cheapest food":
            {
                var s = new Scenario().Party(30).Gold(1_600)
                    .Food("grain", held: 3, market: 100, buy: 10).Food("fish", market: 100, buy: 12)
                    .Food("cheese", market: 5, buy: 9);
                s.Settings.FoodStrategy = FoodStrategy.Cheapest;
                s.Oracle.Slope = 0.002;
                return s;
            }
            case "pledge shrinks":
                return new Scenario().Party(10, footmen: 10).Gold(10_000).Upgrade("recruit", 10, ("war_horse", 5))
                    .Mount("hunter", "horse", market: 50, buy: 200)
                    .Mount("charger", "war_horse", market: 10, buy: 1500);
            case "donations":
            {
                var s = new Scenario().Party(10).Prisoner("looter", 8, 20).Prisoner("bandit", 4, 50)
                    .Prisoner("lord", 1, 2000, hero: true).Food("grain", market: 100, buy: 10);
                s.Settings.DonatePrisonersWhenPossible = true;
                s.Settings.RansomHeroPrisoners = true;
                s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = 6 };
                return s;
            }
            case "market gold":
            {
                var s = new Scenario().Village().Party(10).Gold(100_000, marketGold: 1_000)
                    .Food("grain", held: 60, sell: 8).Food("fish", held: 30, sell: 9)
                    .Pack("mule", held: 14, sell: 70)
                    .Loot("rags", LootGroup.Armour, held: 200, sell: 8)
                    .Loot("club", LootGroup.MeleeWeapons, held: 20, sell: 4);
                s.Settings.SellLoot = true;
                return s;
            }
            case "price limit":
            {
                var s = new Scenario().Party(10, footmen: 6).Upgrade("recruit", 6, ("horse", 3))
                    .Mount("hunter", "horse", market: 20, buy: 200).Mount("steppe", "horse", market: 5, buy: 230)
                    .Food("grain", market: 100, buy: 100);
                s.Oracle.Slope = 0.0003;
                return s;
            }
            case "surplus sales":
            {
                var s = new Scenario().Party(5, footmen: 2)
                    .Food("grain", held: 40, sell: 8).Food("fish", held: 25, sell: 11)
                    .Pack("mule", held: 13, sell: 60).Pack("camel", held: 2, sell: 90)
                    .Mount("hunter", "horse", held: 6, sell: 100).Mount("charger", "war_horse", held: 1, sell: 800);
                s.Oracle.Slope = 0.0004;
                return s;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(name));
        }
    }

    private static string Flatten(IEnumerable<PlanTransaction> transactions) =>
        string.Join("\n", transactions.Select(t =>
            $"{t.Kind} {t.RowId} {t.StackKey}{t.TroopId}{t.HeroId} ×{t.Count} [{string.Join(",", t.UnitPrices)}] {t.Influence}"));
}
