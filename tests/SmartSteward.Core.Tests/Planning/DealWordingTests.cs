using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// "Deal" / "Deal group" (PLAN step 31 — Anton 2026.10.01: "rename that Do button over say the food group as Do group helping
/// people know what they are for each"): a section's title line and the lines that hold a group say "Deal group"
/// (<see cref="PlanPart.IsGroup"/>), every line of one thing says "Deal".
/// </summary>
public class DealWordingTests
{
    private static Scenario Town()
    {
        var s = new Scenario().Party(20, footmen: 10).Gold(30_000, marketGold: 100_000)
            .Food("grain", held: 5, market: 100, buy: 10)
            .Pack("mule", held: 3, market: 20, buy: 140)
            .Mount("hunter", "horse", held: 2, market: 30, buy: 210)
            .Loot("rags", LootGroup.Armour, held: 30, sell: 8)
            .Goods("wool", held: 12, sell: 20)
            .Prisoner("looter", 8, 20)
            .Prisoner("lord_x", 1, 3000, hero: true)
            .Troop("recruit", inParty: 3, onOffer: 6, price: 20, tier: 1)
            .Troop("peasant", inParty: 4, tier: 0);
        s.Settings.SellLoot = true;
        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        s.Snap.Tavern = new TavernInfo
        {
            Wanderers = { new WandererForHire { HeroId = "w1", Name = "Arn", HirePrice = 700, DailyWage = 10 } },
            Mercenaries = new MercenaryOffer { TroopId = "merc", Name = "Blades", Available = 8, PricePerMan = 100 },
        };
        return s;
    }

    [Fact]
    public void Group_parts_are_the_sections_and_the_five_group_lines()
    {
        foreach (var p in new[] { PlanPart.Troops, PlanPart.Food, PlanPart.Horses, PlanPart.Prisoners, PlanPart.Other,
                     PlanPart.Lords, PlanPart.OtherPrisoners, PlanPart.Recruits, PlanPart.YourTroops, PlanPart.OtherGoods })
            Assert.True(p.IsGroup, p.Id);
        foreach (var p in new[] { PlanPart.TavernRow("tavern:wanderer:w1"), PlanPart.Row("food:grain"),
                     PlanPart.RecruitRow("troops:recruit"), PlanPart.DismissRow("troops:peasant"),
                     PlanPart.StackLine("loot:OtherGoods", "wool|") })
            Assert.False(p.IsGroup, p.Id);
    }

    [Fact]
    public void The_sheet_says_Deal_group_exactly_on_the_titles_and_the_group_lines()
    {
        var view = SheetView.Build(Town().Plan()); // everything open
        Assert.All(view.Sections, v => Assert.True(v.Part!.IsGroup, v.Part.Id));

        var items = view.Sections.SelectMany(v => v.Items).ToList();
        var groups = items.Where(i => i.Part!.IsGroup).Select(i => i.Key).OrderBy(k => k).ToList();
        Assert.Equal(new[] { "line:lords", "line:others", "line:recruits", "line:yours", "row:loot:OtherGoods" }, groups);

        // A food, a loot group, a prisoner type, a troop type, a tavern row and a breakdown line say "Deal".
        foreach (var key in new[] { "row:food:grain", "row:loot:Armour", "row:prisoner:looter", "row:tavern:wanderer:w1" })
            Assert.False(items.Single(i => i.Key == key).Part!.IsGroup, key);
        Assert.All(items.Where(i => i.Kind == SheetItemKind.SubLine), i => Assert.False(i.Part!.IsGroup, i.Key));
        Assert.Contains(items, i => i.Kind == SheetItemKind.SubLine);
    }
}
