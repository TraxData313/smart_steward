using SmartSteward.Core.Planning;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

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
    public void Surplus_is_sold_most_expensive_first_and_locked_ones_never_when_locks_protect_them()
    {
        var s = new Scenario()
            .Pack("sumpter_horse", held: 8, sell: 70)
            .Pack("mule", held: 6, sell: 60);
        var row = s.Plan().Row("mounts:pack");
        Assert.Equal(-4, row.Change);
        Assert.Equal(-4, row.Moved("sumpter_horse"));

        s.Snap.Inventory.Single(i => i.Key == "sumpter_horse").IsLocked = true;
        s.Settings.LocksProtectFoodAndHorses = true; // the old way; off (the default) sells it - InventoryLockTests
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


/// <summary>
/// The horses for the footmen (DESIGN §2.3–§2.4) — the step-17 model (Anton 2026.09.28: "don't worry about the mounts needing
/// upgrades … make it simpler for now"): T = ceil(footmen × MountsPer100Footmen / 100) horses, of which W = WarMountsToKeep
/// war horses, riding horses filling the rest; nothing counts troop upgrades.
/// </summary>
public class MountPlannerTests
{
    private static Scenario Footmen(int footmen) => new Scenario().Party(Math.Max(10, footmen), footmen);

    [Fact]
    public void Antons_example_100_footmen_at_110_keeping_10_war_horses_is_100_riding_and_10_war()
    {
        var s = Footmen(100)
            .Mount("hunter", "horse", market: 200, buy: 200)
            .Mount("charger", "war_horse", market: 20, buy: 1500);
        s.Settings.WarMountsToKeep = 10;
        var plan = s.Plan();
        Assert.Equal(110, plan.Facts.MountTarget);
        Assert.Equal(10, plan.Facts.WarTarget);
        Assert.Equal(100, plan.Facts.RidingTarget);
        Assert.Equal(100, plan.Row("mounts:riding").Change);
        Assert.Equal(10, plan.Row("mounts:war").Change);
        Assert.Equal(10, plan.Row("mounts:war").Target);
        Assert.Equal(RowType.WarMount, plan.Row("mounts:war").Type);
        Assert.Equal(MountRole.War, plan.Row("mounts:war").Role);
    }

    [Fact]
    public void With_no_war_horses_kept_every_horse_is_a_riding_horse()
    {
        var plan = Footmen(10)
            .Mount("aserai_horse", "horse", market: 20, buy: 250)
            .Mount("hunter", "horse", market: 5, buy: 200)
            .Plan();
        Assert.Equal(11, plan.Facts.MountTarget);
        Assert.Equal(0, plan.Facts.WarTarget);
        var row = plan.Row("mounts:riding");
        Assert.Equal(11, row.Change);
        Assert.Equal(11, row.Target);
        Assert.Equal(5, row.Moved("hunter")); // the cheapest first
        Assert.Equal(6, row.Moved("aserai_horse"));
        Assert.Equal(25, row.Market);
        Assert.Null(plan.FindRow("mounts:war")); // nothing held, none to keep: no row
    }

    [Fact]
    public void The_riding_surplus_is_sold_against_the_war_horses_bought_in_the_same_visit()
    {
        // 110 riding horses, no war horse; keep 10: the second pass sells the 10 riding horses the war horses replace, so the
        // party ends at 100 + 10 in one visit — and a fresh plan after Do it has nothing left to do.
        var s = Footmen(100)
            .Mount("hunter", "horse", held: 110, market: 50, buy: 200, sell: 100)
            .Mount("charger", "war_horse", market: 20, buy: 1500);
        s.Settings.WarMountsToKeep = 10;
        var plan = s.Plan();
        Assert.Equal(-10, plan.Row("mounts:riding").Change);
        Assert.Equal(10, plan.Row("mounts:war").Change);
        Assert.Equal(100, plan.Facts.RidingTarget);
    }

    [Fact]
    public void After_an_upgrade_took_the_war_horses_the_numbers_settle_by_themselves()
    {
        // Anton: "when I upgrade men with those war horses they become cavalry, the footmen drop, and the numbers settle".
        // 10 men upgraded: 90 footmen (T = 99), 100 riding horses, the 10 war horses gone → buy 10 war, sell 11 riding.
        var s = Footmen(90)
            .Mount("hunter", "horse", held: 100, market: 50, buy: 200, sell: 100)
            .Mount("charger", "war_horse", market: 20, buy: 1500);
        s.Settings.WarMountsToKeep = 10;
        var plan = s.Plan();
        Assert.Equal(99, plan.Facts.MountTarget);
        Assert.Equal(-11, plan.Row("mounts:riding").Change);
        Assert.Equal(10, plan.Row("mounts:war").Change);
        Assert.Equal(89, plan.Row("mounts:riding").Result);
    }

    [Fact]
    public void More_war_horses_than_footmen_need_leaves_no_riding_horse()
    {
        // W > T: 5 footmen want 6 horses, the player keeps 10 war horses — the war horses carry them all, the riding
        // horses go.
        var s = Footmen(5)
            .Mount("hunter", "horse", held: 4, sell: 100)
            .Mount("charger", "war_horse", market: 20, buy: 1500);
        s.Settings.WarMountsToKeep = 10;
        var plan = s.Plan();
        Assert.Equal(6, plan.Facts.MountTarget);
        Assert.Equal(10, plan.Row("mounts:war").Change);
        Assert.Equal(-4, plan.Row("mounts:riding").Change);
        Assert.Equal(0, plan.Facts.RidingTarget);
    }

    [Fact]
    public void War_horses_above_the_number_to_keep_are_sold_dearest_first_and_the_kept_ones_carry_footmen()
    {
        var s = Footmen(10)
            .Mount("charger", "war_horse", held: 3, sell: 800)
            .Mount("t2_horse", "war_horse", held: 2, sell: 600)
            .Mount("hunter", "horse", market: 50, buy: 200);
        s.Settings.WarMountsToKeep = 2;
        var plan = s.Plan();
        var war = plan.Row("mounts:war");
        Assert.Equal(5, war.Mine);
        Assert.Equal(-3, war.Change);
        Assert.Equal(-3, war.Moved("charger"));
        Assert.Equal(9, plan.Row("mounts:riding").Change); // 11 − the 2 war horses kept

        s.Settings.SellWarMountSurplus = false; // kept: all five carry footmen
        plan = s.Plan();
        Assert.Equal(0, plan.Row("mounts:war").Change);
        Assert.Equal(6, plan.Row("mounts:riding").Change);

        s.Settings.SellWarMountSurplus = true;
        s.Settings.WarMountsToKeep = 0; // the default: every unlocked war horse is sold
        Assert.Equal(-5, s.Plan().Row("mounts:war").Change);
    }

    [Theory]
    [InlineData(15_000, 1, 5)]  // enough for all: the 5 war horses and the 1 riding horse left
    [InlineData(10_000, 4, 2)]  // two war horses affordable → the footmen get the rest as riding horses
    [InlineData(6_300, 6, 0)]   // footmen first: every riding horse before any war horse
    public void Riding_horses_come_first_when_the_purse_cannot_pay_for_every_war_horse(int gold, int riding, int war)
    {
        var s = Footmen(5).Gold(gold)
            .Mount("hunter", "horse", market: 50, buy: 200)
            .Mount("charger", "war_horse", market: 10, buy: 1500);
        s.Settings.WarMountsToKeep = 5;
        var plan = s.Plan();
        Assert.Equal(riding, plan.Row("mounts:riding").Change);
        Assert.Equal(war, plan.Row("mounts:war").Change);
        Assert.True(plan.Totals.GoldAfter >= 5_000);
    }

    [Fact]
    public void War_horses_are_the_cheapest_under_WarMountMaxPrice_never_a_plain_horse()
    {
        var s = Footmen(0)
            .Mount("charger", "war_horse", market: 5, buy: 1500)
            .Mount("t2_horse", "war_horse", market: 5, buy: 1800)
            .Mount("hunter", "horse", market: 5, buy: 200);
        s.Settings.WarMountsToKeep = 3;
        var row = s.Plan().Row("mounts:war");
        Assert.Equal(3, row.Moved("charger"));
        Assert.Equal(10, row.Market); // war horses on offer — never the plain hunter

        s.Settings.WarMountMaxPrice = 1_000;
        Assert.Equal(0, s.Plan().Row("mounts:war").Change);
    }

    [Fact]
    public void A_riding_horse_is_never_a_war_horse_bought_cheap()
    {
        // A war horse under the riding cap would be a war horse next visit (and sold as surplus): the riding row buys only
        // riding horses — unless war horses are not managed at all, when a war horse is just a riding horse.
        var s = Footmen(10).Mount("t2_horse", "war_horse", market: 20, buy: 450);
        Assert.Equal(0, s.Plan().Row("mounts:riding").Change);
        Assert.Equal(0, s.Plan().Row("mounts:riding").MaxBuy);

        s.Settings.WarMountsEnabled = false;
        var plan = s.Plan();
        Assert.Equal(11, plan.Row("mounts:riding").Change);
        Assert.Null(plan.FindRow("mounts:war"));
    }

    [Fact]
    public void Riding_surplus_goes_most_expensive_first()
    {
        var s = Footmen(5)
            .Mount("hunter", "horse", held: 6, sell: 100)
            .Mount("steppe", "horse", held: 2, sell: 140);
        var row = s.Plan().Row("mounts:riding");
        Assert.Equal(8, row.Mine);
        Assert.Equal(-2, row.Change);                  // 8 held, target 6
        Assert.Equal(-2, row.Moved("steppe"));
        Assert.Equal(280, row.GoldDelta);

        // A locked horse is managed like the rest (playtest round 2) - unless locks protect food and horses.
        s.Snap.Inventory.Single(i => i.Key == "steppe").IsLocked = true;
        row = s.Plan().Row("mounts:riding");
        Assert.Equal(-2, row.Moved("steppe"));
        Assert.Equal(0, row.Locked);

        s.Settings.LocksProtectFoodAndHorses = true;
        row = s.Plan().Row("mounts:riding");
        Assert.Equal(-2, row.Moved("hunter"));
        Assert.Equal(2, row.Locked);

        s.Settings.SellMountSurplus = false;
        Assert.Equal(0, s.Plan().Row("mounts:riding").Change);
    }

    [Fact]
    public void Village_mounts_are_flat_priced()
    {
        var row = Footmen(10).Village().Mount("hunter", "horse", market: 50, buy: 200).Plan().Row("mounts:riding");
        Assert.Equal(11, row.Change);
        Assert.Equal(-2200, row.GoldDelta);
    }

    [Fact]
    public void Mounts_off_keeps_the_war_horses()
    {
        var s = Footmen(10)
            .Mount("hunter", "horse", market: 50, buy: 200)
            .Mount("charger", "war_horse", market: 5, buy: 1500);
        s.Settings.MountsEnabled = false;
        s.Settings.WarMountsToKeep = 2;
        var plan = s.Plan();
        Assert.Null(plan.FindRow("mounts:riding"));
        Assert.Equal(0, plan.Facts.MountTarget);
        Assert.Equal(2, plan.Row("mounts:war").Change);
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
    public void War_horses_have_no_placeholder_by_default_and_are_bought_under_their_cap()
    {
        // War horses only have a price when the player types one (or turns auto-fill on); without one the role cap decides.
        var s = Footmen(0).Mount("t2_horse", "war_horse", market: 20, buy: 450);
        s.Settings.WarMountsToKeep = 3;
        Assert.Equal(3, s.Plan().Row("mounts:war").Change);

        s.Settings.PriceBook["t2_horse"] = new PriceBookEntry { BuyBase = 300 }; // 360 < 450
        Assert.Equal(0, s.Plan().Row("mounts:war").Change);
    }

    [Fact]
    public void Nothing_counts_troop_upgrades_any_more()
    {
        // The upgrade-ready counting is gone (Anton 2026.09.28): the steward buys exactly what the footmen and the war horses
        // to keep ask for — plain-horse upgrades draw from the riding horses, war-horse ones from the war horses kept.
        var plan = Footmen(10).Mount("hunter", "horse", market: 50, buy: 200)
            .Mount("charger", "war_horse", market: 10, buy: 1500).Plan();
        Assert.Equal(11, plan.Row("mounts:riding").Change);
        Assert.Null(plan.FindRow("mounts:war"));
        Assert.DoesNotContain(plan.Rows, r => r.Id.StartsWith("mounts:upgrade", StringComparison.Ordinal));
    }
}

/// <summary>
/// Noble horses and lame horses (Anton 2026.09.28, step 17): noble horses are never bought and are sold unless LOCKED; lame
/// (badly modified) horses are never bought and, with "Replace lame horses" (default on), sold and replaced by healthy ones.
/// </summary>
public class NobleAndLameHorseTests
{
    private static Scenario Footmen(int footmen) => new Scenario().Party(Math.Max(10, footmen), footmen);

    // ── Noble horses ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Noble_horses_are_sold_unless_locked_and_a_lock_beats_LocksProtectFoodAndHorses_off()
    {
        var s = Footmen(10)
            .Mount("noble_a", "noble_horse", held: 2, sell: 2000)
            .Mount("noble_b", "noble_horse", held: 1, sell: 2500, locked: true)
            .Mount("hunter", "horse", market: 50, buy: 200);
        Assert.False(s.Settings.LocksProtectFoodAndHorses); // the default: locked food and horses are managed…
        var plan = s.Plan();
        var noble = plan.Row("mounts:noble");
        Assert.Equal(MountRole.Noble, noble.Role);
        Assert.Equal(3, noble.Mine);
        Assert.Equal(1, noble.Locked);               // …but a locked noble horse is always the player's own
        Assert.Equal(-2, noble.Change);
        Assert.Equal(0, noble.Moved("noble_b"));
        Assert.Equal(10, plan.Row("mounts:riding").Change); // the kept noble horse carries a footman: 11 − 1
        Assert.True(noble.LocksGuard);               // the executor honours a lock set after the plan too
        Assert.All(plan.Transactions.Where(t => t.RowId == "mounts:noble"), t => Assert.True(t.HonoursLock));

        s.Settings.LocksProtectFoodAndHorses = true;  // the same either way
        Assert.Equal(-2, s.Plan().Row("mounts:noble").Change);
    }

    [Fact]
    public void SellNobleHorses_off_keeps_them_and_they_carry_footmen()
    {
        var s = Footmen(10)
            .Mount("noble_a", "noble_horse", held: 2, sell: 2000)
            .Mount("hunter", "horse", market: 50, buy: 200);
        s.Settings.SellNobleHorses = false;
        var plan = s.Plan();
        Assert.Null(plan.FindRow("mounts:noble"));
        Assert.Equal(9, plan.Row("mounts:riding").Change);   // 11 − the 2 noble horses
        Assert.Equal(0, plan.Row("mounts:riding").Moved("noble_a"));
        Assert.Equal(9, plan.Facts.RidingTarget);
    }

    [Fact]
    public void Noble_horses_are_never_bought_not_even_cheap()
    {
        var s = Footmen(10)
            .Mount("noble_a", "noble_horse", market: 20, buy: 400)   // under the riding cap of 500
            .Mount("hunter", "horse", market: 20, buy: 450);
        s.Settings.WarMountsToKeep = 3;
        var plan = s.Plan();
        Assert.Equal(11, plan.Row("mounts:riding").Moved("hunter"));
        Assert.Equal(0, plan.Row("mounts:riding").Moved("noble_a"));
        Assert.Equal(0, plan.Row("mounts:war").Change);           // not as a war horse either
        Assert.Null(plan.FindRow("mounts:noble"));                // none held: no row
        Assert.DoesNotContain(plan.Transactions, t => t.ItemId == "noble_a");
    }

    [Fact]
    public void A_noble_horse_never_goes_below_its_min_sell_price_and_its_placeholder_is_always_filled()
    {
        // The Prices tab's noble horses always show their average sell price (they are only ever sold): the min sell is
        // 0.8 × 2,000 — a village offering 1,000 keeps the horse.
        var s = Footmen(0).Village().Mount("noble_a", "noble_horse", held: 1, buy: 4000, sell: 1000);
        s.Snap.AveragePrices["noble_a"] = new AveragePrices(4000, 2000);
        var noble = s.Plan().Row("mounts:noble");
        Assert.Equal(0, noble.Change);
        Assert.Equal(EditBlock.BelowMinSellPrice, noble.DecreaseBlock);
        Assert.Equal(EditBlock.SellOnly, noble.IncreaseBlock);    // sell only: nothing to buy

        var book = Core.Pricing.PriceBook.Resolve("noble_a", Core.Pricing.PriceBookGroup.NobleHorses, s.Settings,
            s.Snap.AveragePrices["noble_a"]);
        Assert.True(book.SellBaseIsPlaceholder);
        Assert.Equal(1600, book.FinalMinSell);
        Assert.False(s.Settings.AutoFillWarMountPrices);          // whatever the war horses' switch says
    }

    // ── Lame horses ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Lame_horses_are_never_bought_pack_riding_or_war()
    {
        var s = Footmen(10)
            .Mount("hunter", "horse", market: 20, buy: 30, modifier: "lame_horse", priceFactor: Scenario.Lame)
            .Mount("hunter", "horse", market: 20, buy: 200)
            .Mount("charger", "war_horse", market: 5, buy: 150, modifier: "lame_horse", priceFactor: Scenario.Lame)
            .Mount("charger", "war_horse", market: 5, buy: 1500)
            .Pack("mule", market: 10, buy: 20, modifier: "lame_horse", priceFactor: Scenario.Lame)
            .Pack("mule", market: 20, buy: 150);
        s.Settings.WarMountsToKeep = 2;
        var plan = s.Plan();
        Assert.Equal(9, plan.Row("mounts:riding").Moved("hunter"));     // 11 − the 2 war horses kept
        Assert.Equal(0, plan.Row("mounts:riding").Moved("hunterlame_horse"));
        Assert.Equal(2, plan.Row("mounts:war").Moved("charger"));
        Assert.Equal(10, plan.Row("mounts:pack").Moved("mule"));
        Assert.DoesNotContain(plan.Transactions, t => t.ModifierId != null);
        Assert.Equal(20, plan.Row("mounts:riding").Market);       // the lame ones are not on offer to the steward
    }

    [Fact]
    public void Lame_horses_are_sold_and_replaced_by_healthy_ones_with_the_switch_on()
    {
        var s = LameParty();
        Assert.True(s.Settings.ReplaceLameHorses);               // default ON (Anton)
        var plan = s.Plan();
        var lame = plan.Row("mounts:lame");
        Assert.Equal(MountRole.Lame, lame.Role);
        Assert.Equal(6, lame.Mine);                               // 4 lame hunters + 2 lame mules
        Assert.Equal(-6, lame.Change);
        Assert.Null(lame.Market);
        Assert.Equal(EditBlock.None, lame.IncreaseBlock);         // selling less always works…
        var riding = plan.Row("mounts:riding");
        Assert.Equal(7, riding.Mine);                             // the healthy ones only
        Assert.Equal(4, riding.Change);                           // the exception: sell lame, buy healthy, one visit
        Assert.Equal(8, plan.Row("mounts:pack").Mine);
        Assert.Equal(2, plan.Row("mounts:pack").Change);
        // The lame hunters sell at a lame horse's worth: the min sell is scaled by the modifier (0.8 × 100 × 0.1 = 8).
        Assert.Equal(-4, lame.Moved("hunterlame_horse"));
        Assert.Equal(40, lame.Tallies.Where(t => t.Stack.Kind == ItemKind.Mount).Sum(t => t.Gold));

        Assert.Equal(0, plan.Increase("mounts:lame", EditSize.All).After);
        Assert.Equal(EditBlock.SellOnly, plan.Row("mounts:lame").IncreaseBlock); // …but a lame horse is never bought
    }

    [Fact]
    public void With_the_switch_off_lame_horses_are_kept_and_carry_footmen()
    {
        var s = LameParty();
        s.Settings.ReplaceLameHorses = false;
        var plan = s.Plan();
        Assert.Null(plan.FindRow("mounts:lame"));
        Assert.Equal(11, plan.Row("mounts:riding").Mine);
        Assert.Equal(0, plan.Row("mounts:riding").Change);
        Assert.Equal(10, plan.Row("mounts:pack").Mine);
        Assert.Equal(0, plan.Row("mounts:pack").Change);
    }

    [Fact]
    public void A_lame_horse_the_market_will_not_take_still_counts()
    {
        var s = LameParty();
        s.Oracle.Set("hunterlame_horse", 30, 5);   // below its min sell of 8
        var plan = s.Plan();
        Assert.Equal(-2, plan.Row("mounts:lame").Change);          // only the mules go
        Assert.Equal(0, plan.Row("mounts:riding").Change);         // 7 healthy + 4 lame kept = 11
        Assert.Equal(7, plan.Facts.RidingTarget);                  // 11 − the 4 lame ones kept
    }

    [Fact]
    public void A_guarded_lock_keeps_a_lame_horse_in_its_row()
    {
        var s = LameParty();
        s.Settings.LocksProtectFoodAndHorses = true;
        s.Snap.Inventory.Single(i => i.Key == "hunterlame_horse").IsLocked = true;
        var plan = s.Plan();
        Assert.Equal(2, plan.Row("mounts:lame").Mine);             // only the mules
        Assert.Equal(11, plan.Row("mounts:riding").Mine);
        Assert.Equal(4, plan.Row("mounts:riding").Locked);
        Assert.Equal(0, plan.Row("mounts:riding").Change);
    }

    [Fact]
    public void Every_step_17_row_walks_again_to_exactly_the_planners_plan()
    {
        // The animal rows sell in their rank order — lame, pack, noble, war, riding — in the planner and the editor alike;
        // a town walk, so any other order would price differently.
        var s = LameParty()
            .Mount("noble_a", "noble_horse", held: 2, sell: 2000)
            .Mount("charger", "war_horse", held: 4, market: 10, buy: 1500, sell: 700)
            .Mount("steppe", "horse", held: 12, market: 30, buy: 260, sell: 120);
        s.Settings.WarMountsToKeep = 2;
        s.Oracle.Slope = 0.0001;
        var plan = s.Plan();
        string suggestion = plan.Describe();
        Assert.True(plan.Row("mounts:lame").Change < 0);
        Assert.True(plan.Row("mounts:noble").Change < 0);
        Assert.True(plan.Row("mounts:war").Change < 0);
        plan.ResetAll();
        Assert.Equal(suggestion, plan.Describe());
        Assert.Equal(new[] { "mounts:pack", "mounts:riding", "mounts:war", "mounts:noble", "mounts:lame" },
            plan.Sections.Single(x => x.Kind == PlanSectionKind.Mounts).Rows.Select(r => r.Id));
    }

    /// <summary>10 footmen (11 horses): 7 healthy + 4 lame hunters, 8 healthy + 2 lame mules (10 pack animals), healthy
    /// ones on offer.</summary>
    private static Scenario LameParty() =>
        Footmen(10)
            .Mount("hunter", "horse", held: 4, buy: 30, sell: 10, modifier: "lame_horse", priceFactor: Scenario.Lame)
            .Mount("hunter", "horse", held: 7, market: 50, buy: 200, sell: 100)
            .Pack("mule", held: 2, buy: 20, sell: 7, modifier: "lame_horse", priceFactor: Scenario.Lame)
            .Pack("mule", held: 8, market: 20, buy: 150, sell: 70);
}
