using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// The Other section's "Other goods" line (PLAN step 20, Anton 2026.09.28 — round 4: "Armour and Weapons can you make to Other
/// and add a line there that combines all other stuff that I have not pinned, like coal, jewery etc"): every unlocked trade good
/// that is not food, an animal or equipment, sold in bulk in SellLootOrder like the loot groups.
/// </summary>
public class OtherGoodsTests
{
    private static Scenario Sale()
    {
        var s = new Scenario().Goods("wool", held: 4, sell: 22).Goods("salt", held: 5, sell: 40).Goods("pottery", held: 4, sell: 210)
            .Loot("rags", LootGroup.Armour, held: 3, sell: 8);
        s.Settings.SellLoot = true;
        return s;
    }

    [Fact]
    public void Trade_goods_are_their_own_kind_and_group()
    {
        Assert.Equal(LootGroup.OtherGoods, LootGroups.FromItemType("Goods"));
        Assert.Equal(ItemKind.Goods, GameRules.Classify(false, false, false, false, LootGroup.OtherGoods, false, true));
        Assert.Equal(ItemKind.Food, GameRules.Classify(true, false, false, false, LootGroup.OtherGoods, false, true)); // grain
        Assert.Equal(ItemKind.Other, GameRules.Classify(false, false, false, false, LootGroup.OtherGoods, true, true)); // quest
        Assert.Equal(ItemKind.Other, GameRules.Classify(false, true, false, false, LootGroup.None, false, true)); // livestock
        Assert.False(LootGroups.IsEquipment(LootGroup.OtherGoods));
        Assert.True(LootGroups.IsEquipment(LootGroup.Shields));
    }

    [Fact]
    public void Every_unlocked_good_is_sold_in_one_line_in_the_Other_section()
    {
        var plan = Sale().Plan();
        var goods = plan.Row("loot:OtherGoods");
        Assert.Equal(PlanSectionKind.Other, goods.Section);
        Assert.Equal(LootGroup.OtherGoods, goods.LootGroup);
        Assert.Equal(13, goods.Mine);
        Assert.Equal(-13, goods.Change);
        Assert.Equal(4 * 22 + 5 * 40 + 4 * 210, goods.GoldDelta);
        Assert.Equal(-130, goods.WeightDelta);
        Assert.Equal(new[] { "loot:Armour", "loot:OtherGoods" }, plan.Section(PlanSectionKind.Other)!.Rows.Select(r => r.Id));
        Assert.Equal(3, goods.Breakdown.Count); // its ▸: one line per good
    }

    [Fact]
    public void Locked_goods_are_never_sold_and_the_value_cap_keeps_the_dear_ones()
    {
        var s = Sale();
        s.Snap.Inventory.Single(i => i.ItemId == "salt").IsLocked = true;
        s.Settings.SellLootMaxItemValue = 100; // pottery is worth 420
        var goods = s.Plan().Row("loot:OtherGoods");
        Assert.Equal(4, goods.Mine);
        Assert.Equal(5, goods.Locked);
        Assert.Equal(4, goods.OverValueCap);
        Assert.Equal(-4, goods.Change);
    }

    [Fact]
    public void The_line_has_its_own_switch_under_SellLoot()
    {
        var s = Sale();
        Assert.True(new StewardSettings().SellLootOtherGoods);
        s.Settings.SellLootOtherGoods = false;
        Assert.Null(s.Plan().FindRow("loot:OtherGoods"));

        s.Settings.SellLootOtherGoods = true;
        s.Settings.SellLootEquipment = false;
        var plan = s.Plan();
        Assert.Null(plan.FindRow("loot:Armour"));
        Assert.NotNull(plan.FindRow("loot:OtherGoods"));

        s.Settings.SellLoot = false;
        Assert.Null(s.Plan().Section(PlanSectionKind.Other));
    }

    [Fact]
    public void Goods_sell_with_the_loot_cheapest_first_within_the_markets_gold()
    {
        var s = Sale().Gold(100_000, marketGold: 200);
        var plan = s.Plan();
        // Cheapest first across the lines: rags 3 × 8 = 24, wool 4 × 22 = 88, then 2 salt (80) — 192 of the 200.
        Assert.Equal(-3, plan.Row("loot:Armour").Change);
        Assert.Equal(-6, plan.Row("loot:OtherGoods").Change);
        Assert.Equal(-4, plan.Row("loot:OtherGoods").Moved("wool"));
        Assert.Equal(-2, plan.Row("loot:OtherGoods").Moved("salt"));
    }

    [Fact]
    public void The_line_walks_again_to_exactly_the_planners_plan()
    {
        var s = Sale().Gold(100_000, marketGold: 500);
        s.Oracle.Slope = 0.0002;
        var plan = s.Plan();
        string before = plan.Describe();
        plan.Decrease("loot:OtherGoods");
        plan.ResetAll();
        Assert.Equal(before, plan.Describe());
    }

    [Fact]
    public void Do_it_sells_the_goods_through_the_same_sell_transactions()
    {
        var plan = Sale().Plan();
        var sells = plan.Transactions.Where(t => t.Kind == TransactionKind.Sell).ToList();
        Assert.Contains(sells, t => t.ItemId == "pottery" && t.Count == 4);
        Assert.All(sells.Where(t => t.RowId == "loot:OtherGoods"), t => Assert.True(t.HonoursLock)); // a lock set later keeps it
    }

    [Fact]
    public void An_old_window_state_file_folds_the_Other_section_under_its_old_name()
    {
        var state = WindowState.Parse("{ \"CollapsedSections\": [\"ArmourAndWeapons\"] }", out var problem);
        Assert.Null(problem);
        Assert.True(state.IsCollapsed(SectionGroup.Other));
        Assert.Contains("\"Other\"", state.Generate());
    }
}
