using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

public class LootPlannerTests
{
    private static Scenario Selling()
    {
        var s = new Scenario();
        s.Settings.SellLoot = true;
        return s;
    }

    [Fact]
    public void SellLoot_is_opt_in()
    {
        var s = new Scenario().Loot("rags", LootGroup.Armour, held: 5, sell: 10);
        Assert.Null(s.Plan().Section(PlanSectionKind.Other));

        s.Settings.SellLoot = true;
        s.Settings.SellLootEquipment = false;
        Assert.Null(s.Plan().Section(PlanSectionKind.Other));
    }

    [Fact]
    public void One_row_per_group_in_the_design_order_selling_every_sellable_piece()
    {
        var plan = Selling()
            .Loot("shield", LootGroup.Shields, held: 2, sell: 30)
            .Loot("sword", LootGroup.MeleeWeapons, held: 3, sell: 50)
            .Loot("rags", LootGroup.Armour, held: 5, sell: 10, weight: 4)
            .Loot("bow", LootGroup.Ranged, held: 1, sell: 40)
            .Plan();
        var rows = plan.Section(PlanSectionKind.Other)!.Rows;
        Assert.Equal(new[] { "loot:Armour", "loot:MeleeWeapons", "loot:Ranged", "loot:Shields" }, rows.Select(r => r.Id));
        var armour = plan.Row("loot:Armour");
        Assert.Equal(RowType.Loot, armour.Type);
        Assert.Equal(5, armour.Mine);
        Assert.Equal(-5, armour.Change);
        Assert.Equal(0, armour.Result);
        Assert.Equal(50, armour.GoldDelta);
        Assert.Equal(-20, armour.WeightDelta);
        Assert.Null(armour.Market);
        Assert.Null(armour.PriceBook); // never in the price book
        Assert.Null(armour.BuyLane);   // never bought
        Assert.Equal(50 + 150 + 40 + 60, plan.Totals.Earned);
    }

    [Fact]
    public void Locked_pieces_are_never_sold_nor_counted_as_sellable()
    {
        var row = Selling()
            .Loot("rags", LootGroup.Armour, held: 41, sell: 10)
            .Loot("heirloom", LootGroup.Armour, held: 3, sell: 900, locked: true)
            .Plan().Row("loot:Armour");
        Assert.Equal(41, row.Mine);   // "41 (+3 locked)"
        Assert.Equal(3, row.Locked);
        Assert.Equal(-41, row.Change);
        Assert.Equal(41, row.MaxSell);
    }

    [Fact]
    public void A_group_of_only_locked_pieces_has_no_row()
    {
        var plan = Selling().Loot("heirloom", LootGroup.Armour, held: 3, sell: 900, locked: true).Plan();
        Assert.Null(plan.FindRow("loot:Armour"));
    }

    [Fact]
    public void Pieces_above_SellLootMaxItemValue_are_never_sold()
    {
        var s = Selling()
            .Loot("rags", LootGroup.Armour, held: 5, sell: 10, value: 30)
            .Loot("plate", LootGroup.Armour, held: 2, sell: 500, value: 1500);
        s.Settings.SellLootMaxItemValue = 1000;
        var row = s.Plan().Row("loot:Armour");
        Assert.Equal(5, row.Mine);
        Assert.Equal(2, row.OverValueCap);
        Assert.Equal(-5, row.Change);
    }

    [Theory]
    [InlineData(SellLootOrder.Cheapest, 10, 5, 60)]         // helmets first: 50 gold, then 5 rags
    [InlineData(SellLootOrder.LowestPricePerWeight, 0, 10, 100)] // rags are 1/kg, helmets 5/kg
    [InlineData(SellLootOrder.MostExpensive, 0, 10, 100)]
    public void SellLootOrder_decides_what_a_poor_market_takes(SellLootOrder order, int helms, int rags, double kg)
    {
        var s = Selling().Gold(100_000, marketGold: 100)
            .Loot("helm", LootGroup.Armour, held: 10, sell: 5, weight: 1)
            .Loot("rags", LootGroup.Armour, held: 10, sell: 10, weight: 10);
        s.Settings.SellLootOrder = order;
        var plan = s.Plan();
        var row = plan.Row("loot:Armour");
        Assert.Equal(-helms, row.Moved("helm"));
        Assert.Equal(-rags, row.Moved("rags"));
        Assert.Equal(kg, plan.Totals.WeightFreed);
        Assert.Equal(100, plan.Totals.MarketSales);
        Assert.False(plan.Totals.ExceedsMarketGold);
    }

    [Fact]
    public void The_order_runs_across_groups_so_the_market_gold_goes_to_the_preferred_pieces()
    {
        var plan = Selling().Gold(100_000, marketGold: 30)
            .Loot("rags", LootGroup.Armour, held: 5, sell: 10)
            .Loot("club", LootGroup.MeleeWeapons, held: 5, sell: 4)
            .Plan();
        Assert.Equal(-5, plan.Row("loot:MeleeWeapons").Change); // 20
        Assert.Equal(-1, plan.Row("loot:Armour").Change);       // + 10 = 30
    }

    [Fact]
    public void A_group_stops_at_its_first_piece_the_market_cannot_pay()
    {
        // MostExpensive: the sword does not fit into 300, so the group sells nothing — "−N" is always the
        // first N pieces of the order.
        var s = Selling().Gold(100_000, marketGold: 300)
            .Loot("sword", LootGroup.MeleeWeapons, held: 1, sell: 500)
            .Loot("dagger", LootGroup.MeleeWeapons, held: 5, sell: 50);
        s.Settings.SellLootOrder = SellLootOrder.MostExpensive;
        Assert.Equal(0, s.Plan().Row("loot:MeleeWeapons").Change);
    }

    [Fact]
    public void Selling_in_a_town_walks_the_price_down()
    {
        var s = Selling().Loot("rags", LootGroup.Armour, held: 3, sell: 100, value: 100);
        s.Oracle.Slope = 0.001; // each piece sold (store value 100) takes 10% off the next
        var row = s.Plan().Row("loot:Armour");
        Assert.Equal(100 + 90 + 80, row.GoldDelta);
        Assert.Equal(80, row.UnitPriceMin);
        Assert.Equal(100, row.UnitPriceMax);
    }

    [Fact]
    public void No_trade_access_means_no_loot_rows()
    {
        var s = Selling().Loot("rags", LootGroup.Armour, held: 5, sell: 10);
        s.Snap.CanTrade = false;
        Assert.Null(s.Plan().Section(PlanSectionKind.Other));
    }

    [Fact]
    public void Other_items_are_left_alone()
    {
        var s = Selling();
        s.Snap.Inventory.Add(new ItemStack
        {
            Key = "wine", ItemId = "wine", Name = "wine", Kind = ItemKind.Other, CategoryId = "wine", Count = 20,
        });
        Assert.Empty(s.Plan().Rows.Where(r => r.Tallies.Any(t => t.Stack.Key == "wine")));
    }
}

public class PrisonerPlannerTests
{
    [Fact]
    public void Towns_ransom_every_ticked_prisoner_most_valuable_first()
    {
        var plan = new Scenario().Prisoner("looter", 10, 20).Prisoner("infantry", 3, 60).Plan();
        var rows = plan.Section(PlanSectionKind.Prisoners)!.Rows;
        Assert.Equal(new[] { "prisoner:infantry", "prisoner:looter" }, rows.Select(r => r.Id));
        var looter = plan.Row("prisoner:looter");
        Assert.Equal(RowType.Prisoner, looter.Type);
        Assert.Equal(-10, looter.Change);
        Assert.Equal(200, looter.GoldDelta);
        Assert.Equal(20, looter.UnitPriceMin);
        Assert.Equal(10, looter.Prisoner!.RansomCount);
        Assert.Equal(380, plan.Totals.Earned);
        Assert.Equal(0, plan.Totals.MarketSales); // the broker pays, not the market
        Assert.Equal(0, plan.Totals.PrisonersAfter);
    }

    [Fact]
    public void Villages_and_towns_without_tavern_access_ransom_nothing()
    {
        var s = new Scenario().Village().Prisoner("looter", 10, 20);
        Assert.Null(s.Plan().Section(PlanSectionKind.Prisoners));

        s = new Scenario().Prisoner("looter", 10, 20);
        s.Snap.Prison.CanRansom = false;
        Assert.Null(s.Plan().Section(PlanSectionKind.Prisoners));

        s = new Scenario().Prisoner("looter", 10, 20);
        s.Settings.PrisonerAction = PrisonerChoice.Keep; // round 4: kept - the row stays at 0 for a manual click
        Assert.Equal(0, s.Plan().Row("prisoner:looter").Change);
        Assert.Equal(10, s.Plan().Row("prisoner:looter").MaxSell);
    }

    [Fact]
    public void Lords_are_kept_by_default_but_their_row_stays_for_a_manual_click()
    {
        var s = new Scenario().Prisoner("lord_vlandia", 1, 4000, hero: true);
        var row = s.Plan().Row("prisoner:lord_vlandia");
        Assert.Equal(0, row.Change);
        Assert.Equal(1, row.MaxSell);
        Assert.True(row.Prisoner!.IsHero);

        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        Assert.Equal(4000, s.Plan().Row("prisoner:lord_vlandia").GoldDelta);
    }

    [Fact]
    public void Ransom_is_all_or_none_every_troop_is_proposed()
    {
        // Step 12 (round 1): no per-troop exclusions any more - every unlocked non-lord prisoner goes.
        var s = new Scenario().Prisoner("looter", 10, 20).Prisoner("sea_raider", 4, 40);
        var plan = s.Plan();
        Assert.Equal(-4, plan.Row("prisoner:sea_raider").Change);
        Assert.Equal(-10, plan.Row("prisoner:looter").Change);
        Assert.Equal(0, plan.Totals.PrisonersAfter);

        s.Settings.PrisonerAction = PrisonerChoice.Keep; // none - the rows stay at 0 (round 4)
        Assert.All(s.Plan().Rows.Where(r => r.Type == RowType.Prisoner), r => Assert.Equal(0, r.Change));
    }

    [Fact]
    public void Locked_prisoners_are_never_proposed_and_get_no_row()
    {
        var plan = new Scenario().Prisoner("looter", 10, 20, locked: true).Prisoner("bandit", 2, 30).Plan();
        Assert.Null(plan.FindRow("prisoner:looter"));
        Assert.Equal(10, plan.Totals.PrisonersAfter);
    }

    [Fact]
    public void Donation_fills_the_dungeon_most_valuable_first_and_ransoms_the_rest()
    {
        var s = new Scenario().Prisoner("infantry", 3, 100, influence: 1.5).Prisoner("looter", 4, 50, influence: 1.0);
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        s.Snap.Prison.DonateAllowed = true;
        s.Snap.Prison.DungeonRoom = 5;
        var plan = s.Plan();
        var infantry = plan.Row("prisoner:infantry");
        var looter = plan.Row("prisoner:looter");
        Assert.Equal(3, infantry.Prisoner!.DonateCount);
        Assert.Equal(0, infantry.GoldDelta);
        Assert.Equal(2, looter.Prisoner!.DonateCount);
        Assert.Equal(2, looter.Prisoner!.RansomCount);
        Assert.Equal(-4, looter.Change);
        Assert.Equal(100, looter.GoldDelta);
        Assert.Equal(3 * 1.5 + 2 * 1.0, plan.Totals.InfluenceGained, 6);
    }

    [Fact]
    public void Donation_needs_the_setting_and_the_permission()
    {
        var s = new Scenario().Prisoner("looter", 4, 50);
        s.Snap.Prison.DungeonRoom = 10;
        s.Snap.Prison.DonateAllowed = true;
        Assert.Equal(4, s.Plan().Row("prisoner:looter").Prisoner!.RansomCount); // setting off

        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        s.Snap.Prison.DonateAllowed = false; // e.g. the player's own fief
        Assert.Equal(0, s.Plan().Row("prisoner:looter").Prisoner!.DonateCount);
        Assert.Equal(4, s.Plan().Row("prisoner:looter").Prisoner!.RansomCount); // the others are ransomed instead
    }

    [Fact]
    public void Without_ransom_only_the_dungeon_room_is_used()
    {
        var s = new Scenario().Prisoner("looter", 8, 50);
        s.Snap.Prison.CanRansom = false; // no ransom broker open to the player here
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        s.Snap.Prison.DonateAllowed = true;
        s.Snap.Prison.DungeonRoom = 5;
        var plan = s.Plan();
        Assert.Equal(-5, plan.Row("prisoner:looter").Change);
        Assert.Equal(5, plan.Row("prisoner:looter").MaxSell);
        Assert.Equal(3, plan.Totals.PrisonersAfter);
    }
}

public class TavernPlannerTests
{
    private static Scenario WithTavern(int available = 20)
    {
        var s = new Scenario();
        s.Snap.Tavern = new TavernInfo
        {
            Wanderers =
            {
                new WandererForHire { HeroId = "w2", Name = "Zoe", HirePrice = 900, DailyWage = 12 },
                new WandererForHire { HeroId = "w1", Name = "Arn", HirePrice = 700, DailyWage = 10, SkillTag = "Bow" },
            },
            Mercenaries = new MercenaryOffer
            {
                TroopId = "merc", Name = "Hired blades", Available = available, PricePerMan = 120, WagePerMan = 5, InParty = 2,
            },
        };
        return s;
    }

    [Fact]
    public void Tavern_rows_start_at_zero_wanderers_then_mercenaries()
    {
        var plan = WithTavern().Plan();
        var rows = plan.Section(PlanSectionKind.Tavern)!.Rows;
        Assert.Equal(new[] { "tavern:wanderer:w1", "tavern:wanderer:w2", "tavern:mercenaries" }, rows.Select(r => r.Id));
        Assert.All(rows, r => Assert.Equal(0, r.Change));
        Assert.False(plan.HasChanges);
        var arn = plan.Row("tavern:wanderer:w1");
        Assert.Equal(1, arn.MaxBuy);
        Assert.Equal(700, arn.Tavern!.UnitPrice);
        Assert.Equal("Bow", arn.Tavern.SkillTag);
        var merc = plan.Row("tavern:mercenaries");
        Assert.Equal(2, merc.Mine);
        Assert.Equal(20, merc.Market);
        Assert.Equal(20, merc.MaxBuy);
        Assert.Equal(5, merc.Tavern!.DailyWage);
    }

    [Fact]
    public void Villages_and_towns_without_a_tavern_have_no_tavern_rows()
    {
        Assert.Null(WithTavern().Village().Plan().Section(PlanSectionKind.Tavern));
        Assert.Null(new Scenario().Plan().Section(PlanSectionKind.Tavern));
    }

    [Fact]
    public void Wanderers_are_blocked_by_the_companion_limit_never_by_the_party_size()
    {
        var s = WithTavern();
        s.Snap.Party.CompanionSlotsFree = 0;
        var row = s.Plan().Row("tavern:wanderer:w1");
        Assert.Equal(HireBlock.CompanionLimit, row.Tavern!.Block);
        Assert.Equal(0, row.MaxBuy);

        // Round 3 (Anton 2026.09.28): the party size limit is information, not a wall.
        s.Snap.Party.CompanionSlotsFree = 1;
        s.Snap.Party.PartySizeLimit = s.Snap.Party.Members;
        row = s.Plan().Row("tavern:wanderer:w1");
        Assert.Equal(HireBlock.None, row.Tavern!.Block);
        Assert.Equal(1, row.MaxBuy);
    }

    [Theory]
    [InlineData(20, 15, 20)] // past the room under the limit - the limit never clamps (round 3)
    [InlineData(3, 15, 3)]   // clamped to the offer
    [InlineData(20, 10, 20)] // a full party still hires
    [InlineData(20, 5, 20)]  // already over the limit
    public void Mercenaries_are_clamped_to_the_offer_only(int available, int sizeLimit, int max)
    {
        var s = WithTavern(available);
        s.Snap.Party.PartySizeLimit = sizeLimit; // 10 members
        var row = s.Plan().Row("tavern:mercenaries");
        Assert.Equal(max, row.MaxBuy);
        Assert.Equal(HireBlock.None, row.Tavern!.Block);
    }

    [Fact]
    public void Show_switches_hide_the_section_or_its_halves()
    {
        var s = WithTavern();
        s.Settings.ShowWanderers = false;
        Assert.Equal(new[] { "tavern:mercenaries" }, s.Plan().Section(PlanSectionKind.Tavern)!.Rows.Select(r => r.Id));

        s.Settings.ShowWanderers = true;
        s.Settings.ShowMercenaries = false;
        Assert.Equal(2, s.Plan().Section(PlanSectionKind.Tavern)!.Rows.Count);

        s.Settings.ShowTavern = false;
        Assert.Null(s.Plan().Section(PlanSectionKind.Tavern));
    }

    [Fact]
    public void An_empty_mercenary_band_has_no_row()
    {
        Assert.Null(WithTavern(available: 0).Plan().FindRow("tavern:mercenaries"));
    }
}
