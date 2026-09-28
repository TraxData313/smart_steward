using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Planning;

public class PackAnimalPlannerTests
{
    [Fact]
    public void Buys_the_cheapest_eligible_until_the_target()
    {
        var plan = new Scenario()
            .Pack("mule", market: 4, buy: 120)
            .Pack("sumpter_horse", market: 20, buy: 150)
            .Plan();
        var row = plan.Row("mounts:pack");
        Assert.Equal(MountRole.Pack, row.Role);
        Assert.Equal(10, row.Target);
        Assert.Equal(10, row.Change);
        Assert.Equal(4, row.Moved("mule"));
        Assert.Equal(6, row.Moved("sumpter_horse"));
        Assert.Equal("120–150", $"{row.UnitPriceMin}–{row.UnitPriceMax}");
        Assert.Equal(-(4 * 120 + 6 * 150), row.GoldDelta);
        Assert.Equal(0, row.WeightDelta); // animals weigh nothing
    }

    [Fact]
    public void The_role_cap_blocks_dear_animals_even_when_the_book_allows()
    {
        var s = new Scenario().Pack("mule", market: 20, buy: 350); // book max 420, cap 300
        Assert.Equal(0, s.Plan().Row("mounts:pack").Change);
        Assert.Equal(0, s.Plan().Row("mounts:pack").Market); // none eligible on offer

        s.Settings.PackAnimalMaxPrice = 0; // no cap: the book decides
        Assert.Equal(10, s.Plan().Row("mounts:pack").Change);
    }

    [Fact]
    public void An_empty_base_is_covered_by_the_role_cap_only()
    {
        var s = new Scenario().Pack("mule", market: 20, buy: 200, noAverage: true);
        Assert.Equal(10, s.Plan().Row("mounts:pack").Change);

        s.Settings.PackAnimalMaxPrice = 0; // neither a book price nor a cap → never bought
        Assert.Equal(0, s.Plan().Row("mounts:pack").Change);
    }

    [Fact]
    public void Modified_animals_count_as_held_but_are_never_bought()
    {
        var plan = new Scenario()
            .Pack("mule", held: 4, modifier: "lame")
            .Pack("sumpter_horse", market: 20, modifier: "lame")
            .Plan();
        var row = plan.Row("mounts:pack");
        Assert.Equal(4, row.Mine);
        Assert.Equal(0, row.Change);
        Assert.Equal(0, row.MaxBuy);
    }

    [Fact]
    public void Surplus_is_sold_most_expensive_first_and_locked_ones_never()
    {
        var s = new Scenario()
            .Pack("sumpter_horse", held: 8, sell: 70)
            .Pack("mule", held: 6, sell: 60);
        var row = s.Plan().Row("mounts:pack");
        Assert.Equal(-4, row.Change);
        Assert.Equal(-4, row.Moved("sumpter_horse"));

        s.Snap.Inventory.Single(i => i.Key == "sumpter_horse").IsLocked = true;
        row = s.Plan().Row("mounts:pack");
        Assert.Equal(-4, row.Moved("mule"));
        Assert.Equal(8, row.Locked);
        Assert.Equal(6, row.MaxSell);
    }

    [Fact]
    public void SellPackAnimalSurplus_off_keeps_them()
    {
        var s = new Scenario().Pack("mule", held: 30);
        s.Settings.SellPackAnimalSurplus = false;
        Assert.Equal(0, s.Plan().Row("mounts:pack").Change);
    }

    [Fact]
    public void Animal_purchases_stop_at_MinGoldForHorses()
    {
        var plan = new Scenario().Gold(5_200).Pack("mule", market: 20, buy: 150).Plan();
        Assert.Equal(1, plan.Row("mounts:pack").Change);
        Assert.Equal(5_050, plan.Totals.GoldAfter);
        Assert.False(plan.Totals.BelowMinGoldForHorses);
    }

    [Fact]
    public void Animals_answer_to_the_higher_of_the_two_floors()
    {
        var s = new Scenario().Gold(6_200).Pack("mule", market: 20, buy: 150);
        s.Settings.MinGoldAfterDeal = 6_000;
        Assert.Equal(1, s.Plan().Row("mounts:pack").Change);
    }

    [Fact]
    public void Pack_animals_off_means_no_pack_row()
    {
        var s = new Scenario().Pack("mule", held: 3, market: 20);
        s.Settings.PackAnimalsEnabled = false;
        Assert.Null(s.Plan().FindRow("mounts:pack"));
    }
}

public class MountPlannerTests
{
    private static Scenario Footmen(int footmen) => new Scenario().Party(Math.Max(10, footmen), footmen);

    [Fact]
    public void Riding_target_is_footmen_times_MountsPer100Footmen()
    {
        var plan = Footmen(10)
            .Mount("aserai_horse", "horse", market: 20, buy: 250)
            .Mount("hunter", "horse", market: 5, buy: 200)
            .Plan();
        Assert.Equal(11, plan.Facts.RidingTarget);
        var row = plan.Row("mounts:riding");
        Assert.Equal(11, row.Change);
        Assert.Equal(5, row.Moved("hunter")); // the cheapest first
        Assert.Equal(6, row.Moved("aserai_horse"));
        Assert.Equal(25, row.Market);
    }

    [Fact]
    public void Riding_mounts_are_capped_by_MountMaxPrice()
    {
        var plan = Footmen(10).Mount("noble", "noble_horse", market: 20, buy: 600).Plan();
        Assert.Equal(0, plan.Row("mounts:riding").Change);
        Assert.Equal(0, plan.Row("mounts:riding").Market); // nothing eligible on offer…
        Assert.Equal(0, plan.Row("mounts:riding").MaxBuy); // …so a [+] has nothing to buy
    }

    [Fact]
    public void Reserved_upgrade_horses_count_toward_the_riding_target()
    {
        var s = Footmen(10).Upgrade("recruit", 10, ("war_horse", 3))
            .Mount("charger", "war_horse", held: 3, sell: 800)
            .Mount("hunter", "horse", market: 50, buy: 200);
        var plan = s.Plan();
        Assert.Equal(3, plan.Facts.UpgradeReserved["war_horse"]);
        Assert.Equal(3, plan.Row("mounts:upgrade:war_horse").Mine);
        Assert.Equal(0, plan.Row("mounts:riding").Mine);
        Assert.Equal(8, plan.Row("mounts:riding").Change);

        s.Settings.WarMountsCountAsMounts = false;
        Assert.Equal(11, s.Plan().Row("mounts:riding").Change);
    }

    [Fact]
    public void Reservation_takes_unlocked_before_locked_and_the_cheapest_first()
    {
        var s = Footmen(0).Upgrade("recruit", 10, ("war_horse", 2))
            .Mount("charger", "war_horse", held: 1, buy: 1000, locked: true)
            .Mount("war_a", "war_horse", held: 1, buy: 1200)
            .Mount("war_b", "war_horse", held: 1, buy: 1500)
            .Mount("war_c", "war_horse", held: 1, buy: 1300);
        s.Settings.SellMountSurplus = false;
        var plan = s.Plan();
        var upgrade = plan.Row("mounts:upgrade:war_horse");
        Assert.Equal(2, upgrade.Mine);
        Assert.Equal(0, upgrade.Locked);
        Assert.Equal(new[] { "war_a", "war_c" }, upgrade.Breakdown.Where(l => l.Mine > 0).Select(l => l.StackKey));
        var riding = plan.Row("mounts:riding");
        Assert.Equal(2, riding.Mine); // the locked charger and the dearest war_b
        Assert.Equal(1, riding.Locked);
    }

    [Fact]
    public void Need_counts_a_stack_once_at_its_best_horse_target()
    {
        var plan = Footmen(0)
            .Upgrade("recruit", 10, (null, 6), ("horse", 6))                // foot OR horse → counts as horse
            .Upgrade("raider", 5, ("war_horse", 3), ("war_horse", 4))       // one XP pool → 4, not 7
            .Upgrade("veteran", 2, ("war_horse", 5))                        // never more than the stack
            .Upgrade("spearman", 8, ((string?)null, 8))                     // needs no animal
            .Plan();
        Assert.Equal(6, plan.Facts.UpgradeNeed["horse"]);
        Assert.Equal(6, plan.Facts.UpgradeNeed["war_horse"]);
        Assert.Equal(2, plan.Facts.UpgradeNeed.Count);
    }

    [Fact]
    public void Extra_goes_on_every_category_in_play_and_each_kind_has_its_own_fixed_number()
    {
        var s = Footmen(0).Upgrade("recruit", 10, ("horse", 4)).Upgrade("rider", 10, ("war_horse", 0));
        s.Settings.WarMountsExtra = 2;
        var plan = s.Plan();
        Assert.Equal(6, plan.Facts.UpgradeNeed["horse"]);
        Assert.Equal(2, plan.Facts.UpgradeNeed["war_horse"]);
        Assert.Equal(4, plan.Facts.UpgradeReady["horse"]);
        Assert.Equal(0, plan.Facts.UpgradeReady["war_horse"]);

        // Round 1: "10" meant for war horses must not buy 10 plain horses too.
        s.Settings.WarMountsWarHorseTarget = 10;
        plan = s.Plan();
        Assert.Equal(6, plan.Facts.UpgradeNeed["horse"]);      // still automatic (+ the spares)
        Assert.Equal(10, plan.Facts.UpgradeNeed["war_horse"]); // fixed: no spares on top

        s.Settings.WarMountsHorseTarget = 0;
        plan = s.Plan();
        Assert.Equal(0, plan.Facts.UpgradeNeed["horse"]);
        Assert.DoesNotContain(plan.Rows, r => r.Id == "mounts:upgrade:horse");

        s.Settings.WarMountsEnabled = false;
        plan = s.Plan();
        Assert.Empty(plan.Facts.UpgradeNeed);
        Assert.DoesNotContain(plan.Rows, r => r.Role == MountRole.Upgrade);
    }

    [Fact]
    public void Upgrade_horses_are_the_cheapest_of_their_category_under_WarMountMaxPrice()
    {
        var s = Footmen(0).Upgrade("recruit", 10, ("war_horse", 3))
            .Mount("charger", "war_horse", market: 5, buy: 1500)
            .Mount("t2_horse", "war_horse", market: 5, buy: 1800)
            .Mount("hunter", "horse", market: 5, buy: 200);
        var row = s.Plan().Row("mounts:upgrade:war_horse");
        Assert.Equal(RowType.WarMount, row.Type);
        Assert.Equal(3, row.Need);
        Assert.Equal(3, row.Moved("charger"));
        Assert.Equal(10, row.Market); // war horses on offer — never the plain hunter

        s.Settings.WarMountMaxPrice = 1_000;
        Assert.Equal(0, s.Plan().Row("mounts:upgrade:war_horse").Change);
    }

    [Fact]
    public void Upgrade_horses_about_to_be_bought_are_pledged_to_the_footmen()
    {
        var s = Footmen(10).Upgrade("recruit", 10, ("war_horse", 5))
            .Mount("hunter", "horse", market: 50, buy: 200)
            .Mount("charger", "war_horse", market: 10, buy: 1500);
        var plan = s.Plan();
        Assert.Equal(6, plan.Row("mounts:riding").Change);   // 11 − 5 pledged
        Assert.Equal(5, plan.Row("mounts:upgrade:war_horse").Change);

        s.Settings.WarMountsCountAsMounts = false;
        plan = s.Plan();
        Assert.Equal(11, plan.Row("mounts:riding").Change);
        Assert.Equal(5, plan.Row("mounts:upgrade:war_horse").Change);
    }

    [Theory]
    [InlineData(13_700, 6, 5)]  // enough for all: the pledge holds
    [InlineData(10_000, 9, 2)]  // two upgrade horses affordable → the footmen get the rest as riding mounts
    [InlineData(8_000, 11, 0)]  // footmen first: every riding mount before any upgrade horse
    public void The_pledge_shrinks_to_the_upgrade_horses_the_purse_allows(int gold, int riding, int upgrade)
    {
        var plan = Footmen(10).Gold(gold).Upgrade("recruit", 10, ("war_horse", 5))
            .Mount("hunter", "horse", market: 50, buy: 200)
            .Mount("charger", "war_horse", market: 10, buy: 1500)
            .Plan();
        Assert.Equal(riding, plan.Row("mounts:riding").Change);
        Assert.Equal(upgrade, plan.Row("mounts:upgrade:war_horse").Change);
        Assert.True(plan.Totals.GoldAfter >= 5_000);
    }

    [Fact]
    public void Riding_surplus_goes_most_expensive_first()
    {
        var s = Footmen(5)
            .Mount("hunter", "horse", held: 6, sell: 100)
            .Mount("noble", "noble_horse", held: 1, sell: 2000)
            .Mount("charger", "war_horse", held: 1, sell: 800);
        var row = s.Plan().Row("mounts:riding");
        Assert.Equal(8, row.Mine);
        Assert.Equal(-2, row.Change);                  // 8 held, target 6
        Assert.Equal(-1, row.Moved("noble"));
        Assert.Equal(-1, row.Moved("charger"));
        Assert.Equal(2800, row.GoldDelta);

        s.Snap.Inventory.Single(i => i.Key == "noble").IsLocked = true;
        row = s.Plan().Row("mounts:riding");
        Assert.Equal(-1, row.Moved("charger"));
        Assert.Equal(-1, row.Moved("hunter"));

        s.Settings.SellMountSurplus = false;
        Assert.Equal(0, s.Plan().Row("mounts:riding").Change);
    }

    [Fact]
    public void SellWarMountSurplus_off_keeps_every_horse_of_an_upgrade_category()
    {
        var s = Footmen(5).Upgrade("recruit", 5, ("war_horse", 0))
            .Mount("hunter", "horse", held: 6, sell: 100)
            .Mount("charger", "war_horse", held: 2, sell: 800);
        Assert.Equal(-2, s.Plan().Row("mounts:riding").Moved("charger"));

        s.Settings.SellWarMountSurplus = false;
        var row = s.Plan().Row("mounts:riding");
        Assert.Equal(0, row.Moved("charger"));
        Assert.Equal(-2, row.Moved("hunter"));
    }

    [Fact]
    public void Mounts_are_never_sold_in_a_visit_that_buys_upgrade_horses()
    {
        var s = Footmen(5).Upgrade("recruit", 5, ("war_horse", 2))
            .Mount("hunter", "horse", held: 10, sell: 100)
            .Mount("charger", "war_horse", market: 5, buy: 1500);
        var plan = s.Plan();
        Assert.Equal(0, plan.Row("mounts:riding").Change);
        Assert.Equal(2, plan.Row("mounts:upgrade:war_horse").Change);

        s.Snap.Market.Clear(); // no upgrade horse on offer → the surplus goes
        plan = s.Plan();
        Assert.Equal(-4, plan.Row("mounts:riding").Change); // 10 held, target 6
    }

    [Fact]
    public void Modified_mounts_count_but_are_never_bought()
    {
        var plan = Footmen(10)
            .Mount("hunter", "horse", held: 4, modifier: "lame")
            .Mount("aserai_horse", "horse", market: 20, modifier: "spirited")
            .Plan();
        var row = plan.Row("mounts:riding");
        Assert.Equal(4, row.Mine);
        Assert.Equal(0, row.Change);
    }

    [Fact]
    public void Riding_and_upgrade_buys_of_one_category_share_the_town_walk()
    {
        var s = Footmen(2).Upgrade("recruit", 5, ("horse", 2))
            .Mount("hunter", "horse", market: 50, buy: 200);
        s.Settings.WarMountsCountAsMounts = false;
        s.Settings.BuyPriceMultiplier = 2; // room above the average for the walk to climb
        s.Oracle.Slope = 0.0005; // +10% per horse (store value 200)
        var plan = s.Plan();
        var riding = plan.Row("mounts:riding");
        var upgrade = plan.Row("mounts:upgrade:horse");
        Assert.Equal(3, riding.Change);                 // ceil(2 × 1.1)
        Assert.Equal(2, upgrade.Change);
        Assert.Equal(new[] { 200, 220, 240 }.Sum(), -riding.GoldDelta);
        Assert.Equal(new[] { 260, 280 }.Sum(), -upgrade.GoldDelta); // priced after the riding buys
    }

    [Fact]
    public void Village_mounts_are_flat_priced()
    {
        var row = Footmen(10).Village().Mount("hunter", "horse", market: 50, buy: 200).Plan().Row("mounts:riding");
        Assert.Equal(11, row.Change);
        Assert.Equal(-2200, row.GoldDelta);
    }

    [Fact]
    public void Mounts_off_keeps_the_upgrade_rows()
    {
        var s = Footmen(10).Upgrade("recruit", 10, ("war_horse", 2))
            .Mount("hunter", "horse", market: 50, buy: 200)
            .Mount("charger", "war_horse", market: 5, buy: 1500);
        s.Settings.MountsEnabled = false;
        var plan = s.Plan();
        Assert.Null(plan.FindRow("mounts:riding"));
        Assert.Equal(2, plan.Row("mounts:upgrade:war_horse").Change);
    }

    [Fact]
    public void Role_rows_break_down_per_type()
    {
        var plan = Footmen(10)
            .Mount("hunter", "horse", held: 3, market: 5, buy: 200)
            .Mount("aserai_horse", "horse", market: 20, buy: 250)
            .Plan();
        var lines = plan.Row("mounts:riding").Breakdown;
        var hunter = lines.Single(l => l.StackKey == "hunter");
        Assert.Equal(3, hunter.Mine);
        Assert.Equal(5, hunter.Change);
        Assert.Equal(5, hunter.Market);
        Assert.Equal(-1000, hunter.GoldDelta);
        Assert.Equal(3, lines.Single(l => l.StackKey == "aserai_horse").Change);
    }

    [Fact]
    public void Noble_and_war_horses_have_no_placeholder_by_default()
    {
        // War-mount group items only have a price when the player types one (or turns auto-fill on);
        // a riding mount without a base is still bought under MountMaxPrice.
        var s = Footmen(10).Mount("t2_horse", "war_horse", market: 20, buy: 450);
        Assert.Equal(11, s.Plan().Row("mounts:riding").Change);

        s.Settings.PriceBook["t2_horse"] = new PriceBookEntry { BuyBase = 300 }; // 360 < 450
        Assert.Equal(0, s.Plan().Row("mounts:riding").Change);
    }
}
