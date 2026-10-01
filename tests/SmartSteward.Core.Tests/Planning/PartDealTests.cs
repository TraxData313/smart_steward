using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// "Do just this part" (PLAN step 27 — Anton 2026.10.01: "i dont want to do the full steward but want to ransom my prisoners, so
/// add that button where I can do specific deals separately"): a part's transactions are the plan's own (edits and goals as
/// they stand, the plan's prices), the other parts' are untouched, the purse alone may cut a purchase at its floor, and the
/// window's re-plan afterwards (a fresh plan + the carry-over of the OTHER parts' edits) leaves the untouched parts as they were.
/// </summary>
public class PartDealTests
{
    /// <summary>A town where every part has something to do, the market rich enough that no sale waits on another's gold.</summary>
    private static Scenario Town()
    {
        var s = new Scenario().Party(20, footmen: 10).Gold(30_000, marketGold: 100_000)
            .Food("grain", held: 5, market: 100, buy: 10)
            .Food("fish", market: 50, buy: 14)
            .Pack("mule", held: 3, market: 20, buy: 140)
            .Mount("hunter", "horse", held: 2, market: 30, buy: 210)
            .Loot("rags", LootGroup.Armour, held: 30, sell: 8)
            .Goods("wool", held: 12, sell: 20)
            .Prisoner("looter", 8, 20)
            .Prisoner("lord_x", 1, 3000, hero: true)
            .Troop("recruit", onOffer: 6, price: 20, tier: 1)
            .Troop("peasant", inParty: 4, tier: 0);
        s.Settings.SellLoot = true;
        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        s.Oracle.Slope = 0.0001;
        s.Snap.Tavern = new TavernInfo
        {
            Wanderers =
            {
                new WandererForHire { HeroId = "w1", Name = "Arn", HirePrice = 700, DailyWage = 10 },
                new WandererForHire { HeroId = "w2", Name = "Bea", HirePrice = 800, DailyWage = 12 },
            },
            Mercenaries = new MercenaryOffer { TroopId = "merc", Name = "Blades", Available = 8, PricePerMan = 100 },
        };
        return s;
    }

    private static string Key(PlanTransaction t) =>
        $"{t.Kind}|{t.RowId}|{t.StackKey}|{t.TroopId}|{t.HeroId}|{t.Count}|{string.Join(",", t.UnitPrices)}";

    private static List<string> Keys(IEnumerable<PlanTransaction> list) => list.Select(Key).ToList();

    /// <summary>The plan's transactions of the rows matching <paramref name="pick"/>, in plan order.</summary>
    private static List<string> PlanKeys(StewardPlan plan, Func<PlanRow, PlanTransaction, bool> pick) =>
        Keys(plan.Transactions.Where(t => pick(plan.Row(t.RowId), t)));

    // ── The filter ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Prisoners_only_ransoms_exactly_the_plans_prisoner_rows()
    {
        var plan = Town().Plan();
        var before = Keys(plan.Transactions);
        var deal = plan.DealOf(PlanPart.Prisoners);

        Assert.True(deal.CanRun);
        Assert.All(deal.Transactions, t => Assert.Equal(TransactionKind.Ransom, t.Kind));
        Assert.Equal(PlanKeys(plan, (r, _) => r.Type == RowType.Prisoner), Keys(deal.Transactions));
        Assert.Equal(9, deal.Ransomed);
        Assert.Equal(8 * 20 + 3000, deal.Gold);
        Assert.Equal(0, deal.CutUnits);
        Assert.Equal(before, Keys(plan.Transactions)); // asking changes nothing in the plan
    }

    [Fact]
    public void Lords_and_Others_split_the_prisoners()
    {
        var plan = Town().Plan();
        var lords = plan.DealOf(PlanPart.Lords);
        var others = plan.DealOf(PlanPart.OtherPrisoners);
        Assert.Equal(new[] { "prisoner:lord_x" }, lords.Transactions.Select(t => t.RowId));
        Assert.Equal(new[] { "prisoner:looter" }, others.Transactions.Select(t => t.RowId));
        Assert.Equal(3000, lords.Gold);
        Assert.Equal(160, others.Gold);
    }

    [Fact]
    public void Food_only_carries_the_plans_food_trades_at_the_plans_prices()
    {
        var plan = Town().Plan();
        var deal = plan.DealOf(PlanPart.Food);

        Assert.True(deal.CanRun);
        Assert.NotEmpty(deal.Transactions);
        Assert.Equal(PlanKeys(plan, (r, _) => r.Type == RowType.Food), Keys(deal.Transactions));
        Assert.Equal(plan.Rows.Where(r => r.Type == RowType.Food).Sum(r => r.GoldDelta), deal.Gold);
        Assert.Equal(plan.Rows.Where(r => r.Type == RowType.Food).Sum(r => Math.Max(0, r.Change)), deal.Bought);
    }

    [Fact]
    public void A_tavern_row_alone_hires_only_that_one()
    {
        var s = Town();
        var plan = s.Plan();
        plan.Increase("tavern:wanderer:w1");
        plan.Increase("tavern:mercenaries", EditSize.All);
        Assert.Equal(1, plan.Row("tavern:wanderer:w1").Change);

        var arn = plan.DealOf(PlanPart.TavernRow("tavern:wanderer:w1"));
        Assert.Equal(new[] { TransactionKind.HireWanderer }, arn.Transactions.Select(t => t.Kind));
        Assert.Equal("w1", arn.Transactions[0].HeroId);
        Assert.Equal(1, arn.Hired);
        Assert.Equal(-700, arn.Gold);

        var bea = plan.DealOf(PlanPart.TavernRow("tavern:wanderer:w2"));
        Assert.Equal(PartBlock.NothingToDo, bea.Block); // nobody asked to hire her

        var troops = plan.DealOf(PlanPart.Troops);
        Assert.Equal(1 + 8, troops.Hired);
    }

    [Fact]
    public void Recruits_and_Your_troops_take_only_their_own_side()
    {
        var plan = Town().Plan();
        plan.Increase("troops:recruit", EditSize.Five);
        plan.Decrease("troops:peasant", EditSize.Five);

        var recruits = plan.DealOf(PlanPart.Recruits);
        var yours = plan.DealOf(PlanPart.YourTroops);
        Assert.Equal(new[] { TransactionKind.Recruit }, recruits.Transactions.Select(t => t.Kind).Distinct());
        Assert.Equal(5, recruits.Recruited);
        Assert.Equal(new[] { TransactionKind.Dismiss }, yours.Transactions.Select(t => t.Kind).Distinct());
        Assert.Equal(4, yours.Dismissed);
        var troops = plan.DealOf(PlanPart.Troops);
        Assert.Equal(5, troops.Recruited);
        Assert.Equal(4, troops.Dismissed);
    }

    [Fact]
    public void Other_and_Other_goods()
    {
        var plan = Town().Plan();
        var other = plan.DealOf(PlanPart.Other);
        var goods = plan.DealOf(PlanPart.OtherGoods);
        Assert.Equal(PlanKeys(plan, (r, _) => r.Type == RowType.Loot), Keys(other.Transactions));
        Assert.Equal(PlanKeys(plan, (r, _) => r.Id == "loot:OtherGoods"), Keys(goods.Transactions));
        Assert.Equal(12, goods.Sold);
        Assert.True(other.Sold > goods.Sold);
    }

    [Fact]
    public void Every_part_together_is_the_whole_deal()
    {
        var plan = Town().Plan();
        plan.Increase("tavern:wanderer:w1");
        plan.Increase("troops:recruit", EditSize.Five);
        var all = new[] { PlanPart.Troops, PlanPart.Food, PlanPart.Horses, PlanPart.Prisoners, PlanPart.Other }
            .SelectMany(p => plan.DealOf(p).Transactions).Select(Key).OrderBy(k => k);
        Assert.Equal(Keys(plan.Transactions).OrderBy(k => k), all);
    }

    // ── The money: alone, a part may not afford what the whole deal pays for ─────────────────────────

    /// <summary>Little in the purse, a lord to ransom: inside Do it the ransom pays the food; the food alone stops at
    /// MinGoldAfterDeal (1,000) — the floor it answers to in Do it.</summary>
    [Fact]
    public void Food_alone_stops_at_the_purse_floor_the_ransom_would_have_lifted()
    {
        var s = new Scenario().Party(40).Gold(1_500)
            .Food("grain", market: 200, buy: 10)
            .Prisoner("lord_x", 1, 3000, hero: true);
        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        var plan = s.Plan();
        int planned = plan.Row("food:grain").Change;
        Assert.True(planned * 10 > 500, "the whole deal buys more grain than 500 denari: " + planned);

        var food = plan.DealOf(PlanPart.Food);
        Assert.True(food.CanRun);
        Assert.Equal(PartBlock.PurseFloor, food.CutBy);
        Assert.Equal(1_000, food.Floor);
        Assert.Equal(50, food.Bought); // 1,500 - 1,000 = 500 denari of grain at 10
        Assert.Equal(planned - 50, food.CutUnits);
        Assert.Equal(-500, food.Gold);

        // The prisoners alone are whole, and the plan itself is untouched by asking.
        Assert.Equal(0, plan.DealOf(PlanPart.Prisoners).CutUnits);
        Assert.Equal(planned, plan.Row("food:grain").Change);
    }

    [Fact]
    public void A_buy_part_with_nothing_above_the_floor_greys()
    {
        var s = new Scenario().Party(40).Gold(1_000)
            .Food("grain", market: 200, buy: 10)
            .Prisoner("lord_x", 1, 3000, hero: true);
        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        var plan = s.Plan();
        Assert.True(plan.Row("food:grain").Change > 0);

        var food = plan.DealOf(PlanPart.Food);
        Assert.False(food.CanRun);
        Assert.Equal(PartBlock.PurseFloor, food.Block);
        Assert.Empty(food.Transactions);
    }

    [Fact]
    public void A_hire_the_purse_alone_cannot_pay_greys_with_not_enough_gold()
    {
        var s = new Scenario().Party(10).Gold(500).Prisoner("lord_x", 1, 3000, hero: true);
        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        s.Snap.Tavern = new TavernInfo
        {
            Wanderers = { new WandererForHire { HeroId = "w1", Name = "Arn", HirePrice = 700, DailyWage = 10 } },
            Mercenaries = new MercenaryOffer { TroopId = "merc", Name = "Blades", Available = 8, PricePerMan = 100 },
        };
        var plan = s.Plan();
        plan.Increase("tavern:wanderer:w1");
        plan.Increase("tavern:mercenaries", EditSize.All);
        Assert.False(plan.Totals.CannotAfford); // the ransom pays for both inside Do it

        var arn = plan.DealOf(PlanPart.TavernRow("tavern:wanderer:w1"));
        Assert.Equal(PartBlock.NotEnoughGold, arn.Block);
        var blades = plan.DealOf(PlanPart.TavernRow("tavern:mercenaries"));
        Assert.True(blades.CanRun);
        Assert.Equal(5, blades.Hired); // 500 / 100
        Assert.Equal(3, blades.CutUnits);
        Assert.Equal(PartBlock.NotEnoughGold, blades.CutBy);
    }

    [Fact]
    public void Nothing_to_do_greys()
    {
        var plan = new Scenario().Party(10).Plan();
        foreach (var part in new[] { PlanPart.Troops, PlanPart.Food, PlanPart.Horses, PlanPart.Prisoners, PlanPart.Other })
            Assert.Equal(PartBlock.NothingToDo, plan.DealOf(part).Block);
    }

    // ── The re-plan afterwards ────────────────────────────────────────────────────────────────────────

    private static string RowsOf(StewardPlan plan, Func<PlanRow, bool> pick) =>
        string.Join("; ", plan.Rows.Where(pick).Select(r => r.Id + " " + r.Change + " " + r.GoldDelta));

    /// <summary>Ransom alone → the window plans afresh on the world the ransom left: the food, the horses and the Other section are
    /// what they were (the plan had already planned them for the party without its prisoners), the prisoners are gone.</summary>
    [Fact]
    public void After_the_prisoners_alone_the_other_parts_are_as_they_were()
    {
        var s = Town();
        var plan = s.Plan();
        string food = RowsOf(plan, r => r.Type == RowType.Food);
        string horses = RowsOf(plan, r => r.Section == PlanSectionKind.Mounts);
        string other = RowsOf(plan, r => r.Type == RowType.Loot);

        LivePlanTests.ApplyTransactions(s, plan.DealOf(PlanPart.Prisoners).Transactions);
        var fresh = s.Plan();

        Assert.Equal(food, RowsOf(fresh, r => r.Type == RowType.Food));
        Assert.Equal(horses, RowsOf(fresh, r => r.Section == PlanSectionKind.Mounts));
        Assert.Equal(other, RowsOf(fresh, r => r.Type == RowType.Loot));
        Assert.DoesNotContain(fresh.Rows, r => r.Type == RowType.Prisoner && r.Change != 0);
    }

    /// <summary>The player's hand on the OTHER parts survives the run (<see cref="PlanCarryOver.Capture(StewardPlan, PlanPart)"/>):
    /// a hire and a smaller loot sale stay; the part's own edits were carried out and are not put back.</summary>
    [Fact]
    public void After_a_part_the_edits_on_the_other_parts_are_carried_over()
    {
        var s = Town();
        var plan = s.Plan();
        plan.Increase("tavern:wanderer:w1");
        plan.Increase("loot:Armour", EditSize.Five); // sell 5 rags fewer
        plan.Increase("prisoner:looter", EditSize.Five); // keep 5 of the 8 looters
        int rags = plan.Row("loot:Armour").Change;
        Assert.Equal(5, plan.Row("prisoner:looter").Result);

        var deal = plan.DealOf(PlanPart.Prisoners);
        var carry = PlanCarryOver.Capture(plan, PlanPart.Prisoners);
        Assert.DoesNotContain(carry.Edits, e => e.Key.StartsWith("prisoner:"));
        Assert.Contains(carry.Edits, e => e.Key == "tavern:wanderer:w1" && e.Value == 1);
        Assert.Contains(carry.Edits, e => e.Key == "loot:Armour" && e.Value == rags);

        LivePlanTests.ApplyTransactions(s, deal.Transactions);
        var fresh = s.Plan();
        carry.ApplyTo(fresh);
        Assert.Equal(1, fresh.Row("tavern:wanderer:w1").Change);
        Assert.True(fresh.Row("tavern:wanderer:w1").IsTouched);
        Assert.Equal(rags, fresh.Row("loot:Armour").Change);
        // The 5 looters kept are the steward's again on the fresh plan (the part ran; its edit is not put back).
        Assert.False(fresh.Row("prisoner:looter").IsTouched);
    }

    [Fact]
    public void A_troop_row_is_carried_by_the_side_the_part_did_not_run()
    {
        var plan = Town().Plan();
        plan.Increase("troops:recruit", EditSize.Five);
        plan.Decrease("troops:peasant", EditSize.Five);
        var afterRecruits = PlanCarryOver.Capture(plan, PlanPart.Recruits);
        Assert.Contains(afterRecruits.Edits, e => e.Key == "troops:peasant");
        Assert.DoesNotContain(afterRecruits.Edits, e => e.Key == "troops:recruit");
        var afterYours = PlanCarryOver.Capture(plan, PlanPart.YourTroops);
        Assert.Contains(afterYours.Edits, e => e.Key == "troops:recruit");
        Assert.DoesNotContain(afterYours.Edits, e => e.Key == "troops:peasant");
        Assert.Empty(PlanCarryOver.Capture(plan, PlanPart.Troops).Edits.Where(e => e.Key.StartsWith("troops:")));
    }

    /// <summary>Food alone, then the fresh plan: no food left to suggest, and the horses and Other as they were.</summary>
    [Fact]
    public void After_the_food_alone_nothing_new_for_food_and_the_rest_as_it_was()
    {
        var s = Town();
        var plan = s.Plan();
        string horses = RowsOf(plan, r => r.Section == PlanSectionKind.Mounts);
        string other = RowsOf(plan, r => r.Type == RowType.Loot);
        Assert.Contains(plan.Rows, r => r.Type == RowType.Food && r.Change != 0);

        LivePlanTests.ApplyTransactions(s, plan.DealOf(PlanPart.Food).Transactions);
        var fresh = s.Plan();

        Assert.DoesNotContain(fresh.Rows, r => r.Type == RowType.Food && r.Change != 0);
        Assert.Equal(horses, RowsOf(fresh, r => r.Section == PlanSectionKind.Mounts));
        Assert.Equal(other, RowsOf(fresh, r => r.Type == RowType.Loot));
    }

    // ── The sheet: which lines carry a button ─────────────────────────────────────────────────────────

    [Fact]
    public void The_sheet_puts_a_part_on_every_title_and_on_the_lines_that_are_deals_of_their_own()
    {
        var plan = Town().Plan();
        plan.Increase("tavern:wanderer:w1");
        var view = SheetView.Build(plan);
        Assert.Equal(new[] { PlanPart.Troops, PlanPart.Food, PlanPart.Horses, PlanPart.Prisoners, PlanPart.Other },
            view.Sections.Select(v => v.Part));
        Assert.All(view.Sections, v => Assert.NotNull(v.Deal));

        var withParts = view.Sections.SelectMany(v => v.Items).Where(i => i.Part != null).ToList();
        Assert.Equal(
            new[] { "row:tavern:wanderer:w1", "row:tavern:wanderer:w2", "row:tavern:mercenaries", "line:recruits", "line:yours",
                "line:lords", "line:others", "row:loot:OtherGoods" }.OrderBy(k => k),
            withParts.Select(i => i.Key).OrderBy(k => k));
        Assert.All(withParts, i => Assert.NotNull(i.Deal));
        // Item-level rows carry none: a food, a horse role, a loot group, a troop type under its line.
        Assert.DoesNotContain(view.Sections.SelectMany(v => v.Items),
            i => i.Part != null && (i.Row?.Type == RowType.Food || i.Row?.Id == "loot:Armour" || i.Side != TroopSide.None));
    }

    [Fact]
    public void A_folded_section_keeps_its_title_part()
    {
        var plan = Town().Plan();
        var view = SheetView.Build(plan, null, _ => true); // everything folded
        var food = view.Section(SheetGroup.Food)!;
        Assert.Empty(food.Items);
        Assert.True(food.Deal!.CanRun);
    }
}
