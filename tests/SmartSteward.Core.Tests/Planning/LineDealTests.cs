using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// "Do" on every line (PLAN step 29 — Anton 2026.10.01: "can I have that Do button next to every line too, so that say I want to
/// just update one specific food"): a line's deal is its own row's transactions only (a troop type on its own side, a breakdown
/// line its own stack), the other rows are untouched, the purse floors still hold, and the window's re-plan afterwards puts the
/// player's hand back on everything the line did not do.
/// </summary>
public class LineDealTests
{
    /// <summary>Step 27's town plus a second food, a second prisoner type, a second good and a troop type both on offer and held.</summary>
    private static Scenario Town()
    {
        var s = new Scenario().Party(20, footmen: 10).Gold(30_000, marketGold: 100_000)
            .Food("grain", held: 5, market: 100, buy: 10)
            .Food("fish", market: 50, buy: 14)
            .Pack("mule", held: 3, market: 20, buy: 140)
            .Mount("hunter", "horse", held: 2, market: 30, buy: 210)
            .Loot("rags", LootGroup.Armour, held: 30, sell: 8)
            .Goods("wool", held: 12, sell: 20)
            .Goods("salt", held: 6, sell: 30)
            .Prisoner("looter", 8, 20)
            .Prisoner("bandit", 5, 30)
            .Prisoner("lord_x", 1, 3000, hero: true)
            .Troop("recruit", inParty: 3, onOffer: 6, price: 20, tier: 1)
            .Troop("peasant", inParty: 4, tier: 0);
        s.Settings.SellLoot = true;
        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        s.Oracle.Slope = 0.0001;
        s.Snap.Tavern = new TavernInfo
        {
            Wanderers = { new WandererForHire { HeroId = "w1", Name = "Arn", HirePrice = 700, DailyWage = 10 } },
            Mercenaries = new MercenaryOffer { TroopId = "merc", Name = "Blades", Available = 8, PricePerMan = 100 },
        };
        return s;
    }

    private static string Key(PlanTransaction t) =>
        $"{t.Kind}|{t.RowId}|{t.StackKey}|{t.TroopId}|{t.HeroId}|{t.Count}|{string.Join(",", t.UnitPrices)}";

    private static List<string> Keys(IEnumerable<PlanTransaction> list) => list.Select(Key).ToList();

    private static List<string> PlanKeys(StewardPlan plan, Func<PlanTransaction, bool> pick) => Keys(plan.Transactions.Where(pick));

    private static string RowsOf(StewardPlan plan, Func<PlanRow, bool> pick) =>
        string.Join("; ", plan.Rows.Where(pick).Select(r => r.Id + " " + r.Change + " " + r.GoldDelta));

    // ── The filter: one line, only its own transactions ──────────────────────────────────────────────

    [Fact]
    public void One_food_kind_alone_carries_only_its_own_trades()
    {
        var plan = Town().Plan();
        var before = Keys(plan.Transactions);
        Assert.True(plan.Row("food:grain").Change > 0);
        Assert.True(plan.Row("food:fish").Change > 0);

        var grain = plan.DealOf(PlanPart.Row("food:grain"));
        Assert.True(grain.CanRun);
        Assert.Equal(PlanKeys(plan, t => t.RowId == "food:grain"), Keys(grain.Transactions));
        Assert.DoesNotContain(grain.Transactions, t => t.RowId != "food:grain");
        Assert.Equal(plan.Row("food:grain").Change, grain.Bought);
        Assert.Equal(plan.Row("food:grain").GoldDelta, grain.Gold);
        Assert.Equal(0, grain.CutUnits);

        // The two kinds together are the Food section; asking changes nothing in the plan.
        var fish = plan.DealOf(PlanPart.Row("food:fish"));
        Assert.Equal(Keys(plan.DealOf(PlanPart.Food).Transactions).OrderBy(k => k),
            Keys(grain.Transactions.Concat(fish.Transactions)).OrderBy(k => k));
        Assert.Equal(before, Keys(plan.Transactions));
    }

    /// <summary>One food alone answers to the same floor as inside Do all: a ransom that would have paid for it does not.</summary>
    [Fact]
    public void One_food_kind_alone_still_stops_at_the_purse_floor()
    {
        var s = new Scenario().Party(40).Gold(1_200)
            .Food("grain", market: 200, buy: 10)
            .Food("fish", market: 200, buy: 12)
            .Prisoner("lord_x", 1, 3000, hero: true);
        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        var plan = s.Plan();
        int planned = plan.Row("food:grain").Change;
        Assert.True(planned * 10 > 200, "the whole deal buys more grain than 200 denari: " + planned);

        var grain = plan.DealOf(PlanPart.Row("food:grain"));
        Assert.True(grain.CanRun);
        Assert.Equal(PartBlock.PurseFloor, grain.CutBy);
        Assert.Equal(1_000, grain.Floor);
        Assert.Equal(20, grain.Bought); // 1,200 - 1,000 = 200 denari of grain at 10
        Assert.Equal(planned - 20, grain.CutUnits);
        Assert.All(grain.Transactions, t => Assert.Equal("food:grain", t.RowId));
    }

    [Fact]
    public void A_troop_type_does_only_its_own_side()
    {
        var plan = Town().Plan();
        plan.Increase("troops:recruit", EditSize.Five);
        plan.Decrease("troops:peasant", EditSize.Five);
        Assert.Equal(5, plan.Row("troops:recruit").Change);

        var recruitUnderRecruits = plan.DealOf(PlanPart.RecruitRow("troops:recruit"));
        Assert.Equal(new[] { TransactionKind.Recruit }, recruitUnderRecruits.Transactions.Select(t => t.Kind).Distinct());
        Assert.All(recruitUnderRecruits.Transactions, t => Assert.Equal("troops:recruit", t.RowId));
        Assert.Equal(5, recruitUnderRecruits.Recruited);
        Assert.Equal(-5 * 20, recruitUnderRecruits.Gold);

        // The same type under Your troops has nothing to do (it is being recruited, not dismissed).
        Assert.Equal(PartBlock.NothingToDo, plan.DealOf(PlanPart.DismissRow("troops:recruit")).Block);

        var peasantUnderYours = plan.DealOf(PlanPart.DismissRow("troops:peasant"));
        Assert.Equal(new[] { TransactionKind.Dismiss }, peasantUnderYours.Transactions.Select(t => t.Kind).Distinct());
        Assert.Equal(4, peasantUnderYours.Dismissed);
        Assert.Equal(PartBlock.NothingToDo, plan.DealOf(PlanPart.RecruitRow("troops:peasant")).Block);
    }

    [Fact]
    public void One_prisoner_type_alone()
    {
        var plan = Town().Plan();
        var looters = plan.DealOf(PlanPart.Row("prisoner:looter"));
        Assert.Equal(PlanKeys(plan, t => t.RowId == "prisoner:looter"), Keys(looters.Transactions));
        Assert.Equal(8, looters.Ransomed);
        Assert.Equal(8 * 20, looters.Gold);
        // The bandits and the lord stay for their own lines.
        Assert.DoesNotContain(looters.Transactions, t => t.TroopId != "looter");
        Assert.Equal(5, plan.DealOf(PlanPart.Row("prisoner:bandit")).Ransomed);
        Assert.Equal(8 + 5, plan.DealOf(PlanPart.OtherPrisoners).Ransomed);
    }

    [Fact]
    public void One_other_good_alone_and_one_loot_group_alone()
    {
        var plan = Town().Plan();
        var goods = plan.Row("loot:OtherGoods");
        var wool = goods.Breakdown.Single(l => l.ItemId == "wool");
        var salt = goods.Breakdown.Single(l => l.ItemId == "salt");
        Assert.Equal(-12, wool.Change);
        Assert.Equal(-6, salt.Change);

        var woolDeal = plan.DealOf(PlanPart.StackLine("loot:OtherGoods", wool.StackKey));
        Assert.Equal(PlanKeys(plan, t => t.RowId == "loot:OtherGoods" && t.StackKey == wool.StackKey), Keys(woolDeal.Transactions));
        Assert.Equal(12, woolDeal.Sold);
        Assert.DoesNotContain(woolDeal.Transactions, t => t.ItemId != "wool");
        Assert.Equal(6, plan.DealOf(PlanPart.StackLine("loot:OtherGoods", salt.StackKey)).Sold);

        var rags = plan.DealOf(PlanPart.Row("loot:Armour"));
        Assert.Equal(PlanKeys(plan, t => t.RowId == "loot:Armour"), Keys(rags.Transactions));
        Assert.Equal(30, rags.Sold);
    }

    [Fact]
    public void A_horse_role_and_one_breed_under_it()
    {
        var plan = Town().Plan();
        var role = plan.Rows.First(r => r.Section == PlanSectionKind.Mounts && r.Breakdown.Any(l => l.Change != 0));
        var line = role.Breakdown.First(l => l.Change != 0);

        var whole = plan.DealOf(PlanPart.Row(role.Id));
        Assert.Equal(PlanKeys(plan, t => t.RowId == role.Id), Keys(whole.Transactions));
        var breed = plan.DealOf(PlanPart.StackLine(role.Id, line.StackKey));
        Assert.Equal(PlanKeys(plan, t => t.RowId == role.Id && t.StackKey == line.StackKey), Keys(breed.Transactions));
        Assert.Equal(Math.Abs(line.Change), breed.Bought + breed.Sold);
        Assert.Equal(PartBlock.NothingToDo, plan.DealOf(PlanPart.StackLine(role.Id, "no-such-stack")).Block);
    }

    /// <summary>Every line's own deal, put together, is the whole deal — nothing lost, nothing done twice.</summary>
    [Fact]
    public void Every_line_together_is_the_whole_deal()
    {
        var plan = Town().Plan();
        plan.Increase("tavern:wanderer:w1");
        plan.Increase("troops:recruit", EditSize.Five);
        plan.Decrease("troops:peasant", EditSize.Five);
        var parts = new List<PlanPart>();
        foreach (var row in plan.Rows)
        {
            if (row.Type == RowType.Troop)
            {
                parts.Add(PlanPart.RecruitRow(row.Id));
                parts.Add(PlanPart.DismissRow(row.Id));
            }
            else if (row.Type == RowType.Tavern)
                parts.Add(PlanPart.TavernRow(row.Id));
            else
                parts.Add(PlanPart.Row(row.Id));
        }
        var all = parts.SelectMany(p => plan.DealOf(p).Transactions).Select(Key).OrderBy(k => k);
        Assert.Equal(Keys(plan.Transactions).OrderBy(k => k), all);
    }

    // ── The re-plan afterwards ───────────────────────────────────────────────────────────────────────

    /// <summary>One food alone, then the fresh plan: the other kinds, the horses and Other are planned on the world the grain
    /// left — the horses and Other exactly as they were; the fish still to buy (the days goal is shared, so the steward may move
    /// its split — fine, DESIGN §1.1); nothing new to buy for the grain.</summary>
    [Fact]
    public void After_one_food_the_rest_is_still_to_do()
    {
        var s = Town();
        var plan = s.Plan();
        string horses = RowsOf(plan, r => r.Section == PlanSectionKind.Mounts);
        string other = RowsOf(plan, r => r.Type == RowType.Loot);
        int fishBefore = plan.Row("food:fish").Change;

        LivePlanTests.ApplyTransactions(s, plan.DealOf(PlanPart.Row("food:grain")).Transactions);
        var fresh = s.Plan();

        Assert.True(fresh.Row("food:grain").Change <= 0, "no more grain to buy: " + fresh.Row("food:grain").Change);
        Assert.Equal(fishBefore, fresh.Row("food:fish").Change);
        Assert.Equal(horses, RowsOf(fresh, r => r.Section == PlanSectionKind.Mounts));
        Assert.Equal(other, RowsOf(fresh, r => r.Type == RowType.Loot));
    }

    /// <summary>A line's run carries the player's hand on every OTHER row over; a troop type's run keeps its other side; a
    /// breakdown line leaves the rest of its row's edit (the row's change less the line's).</summary>
    [Fact]
    public void After_a_line_the_other_edits_are_carried_over()
    {
        var s = Town();
        var plan = s.Plan();
        plan.Increase("tavern:wanderer:w1");
        plan.Increase("prisoner:looter", EditSize.Five); // keep 5 of the 8 looters
        plan.Decrease("troops:peasant", EditSize.Five);

        var carry = PlanCarryOver.Capture(plan, PlanPart.Row("prisoner:looter"));
        Assert.DoesNotContain(carry.Edits, e => e.Key == "prisoner:looter");
        Assert.Contains(carry.Edits, e => e.Key == "tavern:wanderer:w1" && e.Value == 1);
        Assert.Contains(carry.Edits, e => e.Key == "troops:peasant" && e.Value == -4);

        // A troop type run under Recruits leaves its dismissal edit (another type's) alone; run under Your troops it takes it.
        Assert.Contains(PlanCarryOver.Capture(plan, PlanPart.RecruitRow("troops:peasant")).Edits, e => e.Key == "troops:peasant");
        Assert.DoesNotContain(PlanCarryOver.Capture(plan, PlanPart.DismissRow("troops:peasant")).Edits, e => e.Key == "troops:peasant");

        // Apply the looters alone, re-plan: the hire and the dismissal are back as they were, the looters are the steward's again.
        LivePlanTests.ApplyTransactions(s, plan.DealOf(PlanPart.Row("prisoner:looter")).Transactions);
        var fresh = s.Plan();
        carry.ApplyTo(fresh);
        Assert.Equal(1, fresh.Row("tavern:wanderer:w1").Change);
        Assert.Equal(-4, fresh.Row("troops:peasant").Change);
        Assert.False(fresh.Row("prisoner:looter").IsTouched);
    }

    [Fact]
    public void A_breakdown_line_leaves_the_rest_of_its_rows_edit()
    {
        var plan = Town().Plan();
        plan.Increase("loot:OtherGoods", EditSize.Five); // sell 5 fewer goods: the row is the player's now
        var goods = plan.Row("loot:OtherGoods");
        Assert.True(goods.IsTouched);
        var line = goods.Breakdown.First(l => l.Change != 0);
        var part = PlanPart.StackLine("loot:OtherGoods", line.StackKey);

        Assert.Equal(goods.Change - line.Change, part.EditLeft(goods));
        var carry = PlanCarryOver.Capture(plan, part);
        int left = goods.Change - line.Change;
        if (left == 0)
            Assert.DoesNotContain(carry.Edits, e => e.Key == "loot:OtherGoods");
        else
            Assert.Contains(carry.Edits, e => e.Key == "loot:OtherGoods" && e.Value == left);
    }

    // ── The sheet: a button on every line ───────────────────────────────────────────────────────────

    [Fact]
    public void The_sheet_puts_a_part_on_every_line()
    {
        var plan = Town().Plan();
        plan.Increase("troops:recruit", EditSize.Five);
        plan.Decrease("troops:peasant", EditSize.Five);
        var view = SheetView.Build(plan); // everything open
        var items = view.Sections.SelectMany(v => v.Items).ToList();

        Assert.All(items, i => Assert.NotNull(i.Part));
        Assert.All(items, i => Assert.NotNull(i.Deal));

        PlanPart PartOfKey(string key) => items.Single(i => i.Key == key).Part!;
        Assert.Equal(PlanPart.Row("food:grain"), PartOfKey("row:food:grain"));
        Assert.Equal(PlanPart.Row("loot:Armour"), PartOfKey("row:loot:Armour"));
        Assert.Equal(PlanPart.Row("prisoner:looter"), PartOfKey("row:prisoner:looter"));
        Assert.Equal(PlanPart.RecruitRow("troops:recruit"), PartOfKey("row:troops:recruit@recruit"));
        Assert.Equal(PlanPart.DismissRow("troops:peasant"), PartOfKey("row:troops:peasant@dismiss"));
        // Step 27's lines keep their parts.
        Assert.Equal(PlanPart.Recruits, PartOfKey("line:recruits"));
        Assert.Equal(PlanPart.OtherGoods, PartOfKey("row:loot:OtherGoods"));
        Assert.Equal(PlanPart.TavernRow("tavern:wanderer:w1"), PartOfKey("row:tavern:wanderer:w1"));
        // A breakdown line (one good under Other goods) by its stack.
        var sub = items.First(i => i.Kind == SheetItemKind.SubLine && i.Row?.Id == "loot:OtherGoods");
        Assert.Equal(PlanPartKind.StackLine, sub.Part!.Kind);
        Assert.Equal(sub.StackKey, sub.Part.StackKey);
        Assert.True(sub.Deal!.CanRun);

        // A line with nothing to do is greyed: the recruit type under Your troops (it is being recruited, not dismissed).
        var held = items.Single(i => i.Key == "row:troops:recruit@dismiss");
        Assert.Equal(PlanPart.DismissRow("troops:recruit"), held.Part);
        Assert.Equal(PartBlock.NothingToDo, held.Deal!.Block);
    }
}
