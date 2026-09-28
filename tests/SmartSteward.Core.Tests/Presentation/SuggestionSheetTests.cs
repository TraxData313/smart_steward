using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>
/// The Suggestion tab as one spreadsheet (PLAN step 20, the mockup Anton approved on 2026.09.28 with two changes — "Souls" is
/// Party, members only, plus a Prisoners column): the metrics per row, section and Total, the overview lines, the aggregate
/// lines and the weight table. The acceptance case is the mockup's own Lycaron.
/// </summary>
public class SuggestionSheetTests
{
    private const string M = "–";
    private const string D = " · ";

    /// <summary>The mockup's deal at Lycaron (docs/mockups/README.md "The deal drawn"), as far as the Core decides it.</summary>
    private static (Scenario S, StewardPlan Plan) Lycaron()
    {
        var s = new Scenario().Party(103, footmen: 92).Gold(69_358, marketGold: 100_000)
            // Food: six kinds held (188), Meat on the market; Olives and Date Fruit bought by hand.
            .Food("grain", held: 62, market: 140, buy: 11).Food("fish", held: 46, market: 35, buy: 13)
            .Food("cheese", held: 34, market: 12, buy: 42).Food("butter", held: 28, market: 20, buy: 27)
            .Food("beer", held: 16, market: 8, buy: 52).Food("olives", held: 2, market: 24, buy: 31)
            .Food("date_fruit", market: 9, buy: 56).Food("meat", market: 6, buy: 64)
            // Troops: two T0 peasants, six T1 recruits (two wounded; on offer too), an archer; Vigla recruits on offer.
            .Troop("peasant", inParty: 2, tier: 0, name: "Empire Peasant")
            .Troop("recruit", inParty: 6, onOffer: 15, price: 17, tier: 1, wounded: 2, name: "Imperial Recruit")
            .Troop("archer", inParty: 1, tier: 2, name: "Imperial Archer")
            .Troop("vigla", onOffer: 3, price: 38, tier: 2, name: "Imperial Vigla Recruit")
            // Prisoners: 50 in nine types, two lords.
            .Prisoner("nomad", 8, 24).Prisoner("looter", 12, 16).Prisoner("tribal", 6, 48).Prisoner("sea_raider", 9, 44)
            .Prisoner("hunter", 5, 90).Prisoner("spearman", 4, 120).Prisoner("horse_archer", 2, 150).Prisoner("lancer", 3, 140)
            .Prisoner("heavy_horse_archer", 1, 200).Prisoner("tolun", 1, 8_210, hero: true).Prisoner("sechen", 1, 6_658, hero: true)
            // Other: 36 pieces in four groups, 13 goods.
            .Loot("mail", LootGroup.Armour, held: 14, sell: 300).Loot("axe", LootGroup.MeleeWeapons, held: 11, sell: 150)
            .Loot("bow", LootGroup.Ranged, held: 6, sell: 120).Loot("shield", LootGroup.Shields, held: 5, sell: 76)
            .Goods("wool", held: 4, sell: 22).Goods("salt", held: 5, sell: 40).Goods("pottery", held: 4, sell: 210);
        foreach (var p in s.Snap.Prisoners)
            p.Tier = p.TroopId switch
            {
                "nomad" or "looter" => 1, "tribal" or "sea_raider" => 2, "hunter" or "spearman" => 3,
                "horse_archer" or "lancer" => 4, "heavy_horse_archer" => 5, _ => 0,
            };
        s.Snap.Party.PartySizeLimit = 101;
        s.Snap.Party.PrisonerSizeLimit = 60;
        s.Snap.Party.DailyFoodUse = 5.8;
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = true, DungeonRoom = 10 };
        s.Snap.Tavern = new TavernInfo
        {
            Wanderers = { new WandererForHire { HeroId = "temeon", Name = "Temeon the Shipwright", HirePrice = 444, DailyWage = 20 } },
            Mercenaries = new MercenaryOffer { TroopId = "hired_pike", Name = "Hired Pike", Available = 4, PricePerMan = 510 },
        };
        s.Settings.FoodDays = 10;             // a target far below what is held: the steward itself buys and sells no food
        s.Settings.SellFoodSurplus = false;
        s.Settings.MountsEnabled = false;     // the horses are a test of their own
        s.Settings.PackAnimalsEnabled = false;
        s.Settings.WarMountsEnabled = false;
        s.Settings.SellLoot = true;
        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        s.Settings.PrisonerAction = PrisonerChoice.Donate;

        var plan = s.Plan();
        plan.SetChange("food:olives", 8);
        plan.SetChange("food:date_fruit", 4);
        plan.SetChange("tavern:mercenaries", 4);
        plan.DismissLowest();
        plan.DismissLowest();
        plan.DismissLowest();
        return (s, plan);
    }

    [Fact]
    public void The_sections_come_in_the_mockups_order()
    {
        var sheet = SuggestionSheet.Of(Lycaron().Plan);
        Assert.Equal(new[] { SheetGroup.Troops, SheetGroup.Food, SheetGroup.Prisoners, SheetGroup.Other },
            sheet.Sections.Select(s => s.Group));
    }

    [Fact]
    public void Troops_the_title_says_men_after_the_deal_over_the_limit_in_red()
    {
        var troops = SuggestionSheet.Of(Lycaron().Plan).Section(SheetGroup.Troops)!;
        Assert.Equal("104/101", troops.Overview);
        Assert.Equal("men after the deal / party limit", troops.OverviewNote);
        Assert.True(troops.OverviewWarning);
        Assert.Equal(new[] { SheetLineKind.Row, SheetLineKind.Row, SheetLineKind.Recruits, SheetLineKind.YourTroops },
            troops.Lines.Select(l => l.Kind));
        Assert.Equal(new PlanMetrics(-2_040, 0, +1, 0, 0, 0), troops.Metrics); // 4 pikes in, 3 men out
    }

    [Fact]
    public void Troops_the_two_lines_say_what_they_do()
    {
        var troops = SuggestionSheet.Of(Lycaron().Plan).Section(SheetGroup.Troops)!;
        var recruits = troops.Lines.Single(l => l.Kind == SheetLineKind.Recruits);
        Assert.Equal("18 on offer" + D + "[+] takes the best tier first", recruits.Text);
        Assert.Null(recruits.Mine);
        Assert.Equal(18, recruits.Market);
        Assert.Equal(new[] { "troops:vigla", "troops:recruit" }, recruits.Details.Select(r => r.Id));

        var yours = troops.Lines.Single(l => l.Kind == SheetLineKind.YourTroops);
        Assert.Equal("dropping 2 T0 Empire Peasant, 1 T1 Imperial Recruit", yours.Text);
        Assert.Equal(9, yours.Mine);
        Assert.Equal(-3, yours.Change);
        Assert.Equal(6, yours.Result);
        Assert.Equal(-3, yours.Metrics.Party);
        Assert.Equal(new[] { "troops:peasant", "troops:recruit", "troops:archer" }, yours.Details.Select(r => r.Id));
    }

    [Fact]
    public void Food_the_overview_reads_like_the_mockup()
    {
        var food = SuggestionSheet.Of(Lycaron().Plan).Section(SheetGroup.Food)!;
        Assert.Equal("7/8 kinds" + D + "avg 29 ± 19 per kind" + D + "min 4 date_fruit" + D + "188 » 200 (+12)" + D + "~32 » 42 days",
            food.Overview);
        Assert.Equal(12, food.Metrics.LandKg);  // 12 units (the test kit weighs food 1 kg)
        Assert.Equal(8, food.Details.Count);
    }

    [Fact]
    public void Prisoners_the_overview_and_the_Lords_and_Others_lines()
    {
        var sheet = SuggestionSheet.Of(Lycaron().Plan);
        var prisoners = sheet.Section(SheetGroup.Prisoners)!;
        Assert.Equal("52/60" + D + "2 lords" + D + "avg T2.0 ± 1.1" + D + "T1–T5", prisoners.Overview);

        var lords = prisoners.Lines.Single(l => l.Kind == SheetLineKind.Lords);
        Assert.Equal(PrisonerChoice.Ransom, lords.Action);
        Assert.Equal("2 lords" + D + "2 ransomed", lords.Text);
        Assert.Equal(new PlanMetrics(8_210 + 6_658, 0, 0, -2, 0, 0), lords.Metrics); // Party 0, Prisoners −2

        var others = prisoners.Lines.Single(l => l.Kind == SheetLineKind.OtherPrisoners);
        Assert.Equal(PrisonerChoice.Donate, others.Action);
        Assert.Equal("10 to the dungeon (full), 40 ransomed", others.Text);
        Assert.Equal(-50, others.Change);
        Assert.Equal(0, others.Metrics.Party);
        Assert.Equal(-50, others.Metrics.Prisoners);
        Assert.True(others.Metrics.Influence > 0);
        Assert.True(sheet.DonateAllowedHere && sheet.RansomAllowedHere);
        Assert.Equal("prisoner:looter", prisoners.Details.First().Id);    // lowest tier first (T1, ties by name)…
        Assert.Equal("prisoner:tolun", prisoners.Details.Last().Id);      // …the lords last
    }

    [Fact]
    public void Other_the_overview_counts_pieces_and_goods_and_names_the_order()
    {
        var other = SuggestionSheet.Of(Lycaron().Plan).Section(SheetGroup.Other)!;
        Assert.Equal("49 sold: 36 pieces, 13 goods" + D + "cheapest first", other.Overview);
        Assert.Equal(-(36 * 5 + 130), other.Metrics.LandKg);
    }

    [Fact]
    public void The_Total_is_every_row_once_and_agrees_with_the_plans_totals()
    {
        var (_, plan) = Lycaron();
        var sheet = SuggestionSheet.Of(plan);
        var t = plan.Totals;
        Assert.Equal(t.GoldChange, sheet.Total.Denari);
        Assert.Equal(t.InfluenceGained, sheet.Total.Influence, 6);
        Assert.Equal(t.MembersAfter - t.MembersNow, sheet.Total.Party);
        Assert.Equal(t.PrisonersAfter - t.PrisonersNow, sheet.Total.Prisoners);
        Assert.Equal(t.WeightChange, sheet.Total.LandKg, 6);
        var sum = sheet.Sections.Aggregate(new PlanMetrics(), (a, s) => a + s.Metrics);
        Assert.Equal(sheet.Total, sum);
        Assert.Equal("party 103 » 104" + D + "prisoners 52 » 0", sheet.TotalText);
        Assert.StartsWith("69,358 » ", sheet.DenariText);
        Assert.EndsWith("influence", sheet.InfluenceText);
    }

    [Fact]
    public void Party_counts_members_only_and_Prisoners_the_prisoners()
    {
        // The coordinator's examples (Anton's approval, 2026.09.28): ransoming 50 is Party 0 / Prisoners −50; recruiting 4 is
        // Party +4 / Prisoners 0.
        var plan = new Scenario().Prisoner("looter", 50, 20).Troop("recruit", onOffer: 10, price: 5).Plan();
        plan.RecruitBest(EditSize.Five);
        plan.TakeBackRecruits();
        var ransom = PlanMetrics.Of(plan.Row("prisoner:looter"));
        Assert.Equal((0, -50, 1_000), (ransom.Party, ransom.Prisoners, ransom.Denari));
        var recruit = PlanMetrics.Of(plan.Row("troops:recruit"));
        Assert.Equal((4, 0, -20), (recruit.Party, recruit.Prisoners, recruit.Denari));
    }

    [Fact]
    public void Sea_kg_weighs_the_animals_and_a_mounted_mans_horse_the_land_does_not()
    {
        var s = new Scenario().Party(10, footmen: 10).Mount("hunter", "horse", market: 30, buy: 200)
            .Troop("rider", onOffer: 5, price: 40, mounted: true, seaWeight: 50);
        foreach (var stack in s.Snap.Market) stack.UnitWeightAtSea = 50;
        var plan = s.Plan();
        plan.RecruitBest(EditSize.Five); // 5 riders: no footmen among them
        var horses = PlanMetrics.Of(plan.Row("mounts:riding"));
        Assert.Equal(0, horses.LandKg);
        Assert.Equal(11 * 50, horses.SeaKg);
        Assert.Equal(5 * 50, PlanMetrics.Of(plan.Row("troops:rider")).SeaKg);
    }

    [Fact]
    public void Horses_the_overview_is_the_herd_line_the_footmen_and_the_waiting_jobs()
    {
        var s = new Scenario().Party(100, footmen: 100).Gold(8_000).Mount("hunter", "horse", held: 110, buy: 200);
        s.Settings.WarHorsesMinDenari = 20_000;
        s.Settings.WarMountsToKeep = 10;
        var horses = SuggestionSheet.Of(s.Plan()).Section(SheetGroup.Horses)!;
        Assert.Equal("110 / 200 before the herd slows you" + D + "100 on foot, 110 horses to keep" + D
                     + "war horses start at 20,000 denari", horses.Overview);
        Assert.False(horses.OverviewWarning);
    }

    [Fact]
    public void A_waiting_food_job_says_when_it_starts()
    {
        var s = new Scenario().Party(10).Gold(1_000).Food("grain", held: 5, market: 50);
        s.Settings.FoodMinDenari = 2_000;
        Assert.EndsWith("starts at 2,000 denari", SuggestionSheet.Of(s.Plan()).Section(SheetGroup.Food)!.Overview);
    }

    [Fact]
    public void Nothing_to_do_reads_quietly()
    {
        var s = new Scenario().Party(10).Food("grain", held: 20).Loot("rags", LootGroup.Armour, held: 3, sell: 8)
            .Troop("peasant", inParty: 2, name: "Peasant");
        s.Settings.SellLoot = true;
        s.Gold(100_000, marketGold: 0); // the market cannot pay: nothing sold
        var sheet = SuggestionSheet.Of(s.Plan());
        Assert.Equal("nothing sold" + D + "cheapest first", sheet.Section(SheetGroup.Other)!.Overview);
        Assert.Equal("[" + M + "] drops the lowest tier first",
            sheet.Section(SheetGroup.Troops)!.Lines.Single(l => l.Kind == SheetLineKind.YourTroops).Text);
        Assert.Equal("party 10", sheet.TotalText);
        Assert.Equal("100,000", sheet.DenariText);
        Assert.Equal("", sheet.InfluenceText);
    }
}
