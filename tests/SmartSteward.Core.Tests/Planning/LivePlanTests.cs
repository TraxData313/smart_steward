using System.Diagnostics;
using System.Reflection;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;
using Xunit.Abstractions;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// The live re-plan (PLAN step 15, DESIGN §1.1 — Anton 2026.09.28: "after I change the troops the mounts etc are not really
/// accurate for the new numbers I entered, so can you recalculate the food and mounts as I add more troops or remove
/// dynamically, so when I hit Do it at the end I won't see new suggestions for the new troop counts?"). A party-changing
/// edit (hires, prisoners) re-plans every untouched item row for the party after the deal; touched rows stay and go first.
/// </summary>
public class LivePlanTests
{
    private readonly ITestOutputHelper _output;

    public LivePlanTests(ITestOutputHelper output) => _output = output;

    // ── The acceptance test, in Anton's words ────────────────────────────────────────────────────────

    public static TheoryData<string, bool> Edits
    {
        get
        {
            var data = new TheoryData<string, bool>();
            foreach (var edit in new[] { "hire 20 foot mercenaries", "keep the prisoners, then ransom them all", "hire a wanderer", "a mix" })
                foreach (bool flat in new[] { false, true })
                    data.Add(edit, flat);
            return data;
        }
    }

    /// <summary>Plan → edit the party → Do it (the transactions applied to the snapshot) → a fresh plan of the resulting
    /// party proposes NOTHING new for food, pack animals, riding mounts or upgrade horses. <paramref name="flat"/>: the market's
    /// prices stay flat (a village's way); else they walk like a town's.</summary>
    [Theory]
    [MemberData(nameof(Edits))]
    public void After_Do_it_a_fresh_plan_of_the_new_party_has_nothing_new_to_suggest(string edit, bool flat)
    {
        var s = Camp(flat);
        var plan = s.Plan();
        Edit(plan, edit);
        Assert.False(plan.Totals.CannotAfford);
        Assert.True(plan.IsEdited);

        ApplyDoIt(s, plan);
        var fresh = s.Plan();
        _output.WriteLine(edit + (flat ? " (flat)" : " (town walk)") + ": food target " + plan.Facts.FoodTarget + " for "
                          + plan.Facts.FoodEaters + " eaters, riding target " + plan.Facts.RidingTarget + " for "
                          + plan.Facts.Footmen + " footmen");
        NothingNew(fresh, edit);

        // Prisoners the player kept are proposed for ransom again next time (the steward's default) — the player keeping
        // them again is the same party, and still nothing new.
        foreach (var kept in plan.Rows.Where(r => r.Type == RowType.Prisoner && r.IsTouched))
            if (fresh.FindRow(kept.Id) != null)
                fresh.SetChange(kept.Id, 0);
        NothingNew(fresh, edit + ", the prisoners kept again");
        // …because the re-plan planned for exactly this party.
        Assert.Equal(plan.Facts.FoodEaters, fresh.Facts.FoodEaters);
        Assert.Equal(plan.Facts.FoodTarget, fresh.Facts.FoodTarget);
        Assert.Equal(plan.Facts.Footmen, fresh.Facts.Footmen);
        Assert.Equal(plan.Facts.RidingTarget, fresh.Facts.RidingTarget);
        Assert.Equal(plan.Facts.UpgradeNeed, fresh.Facts.UpgradeNeed);
    }

    [Fact]
    public void Without_the_re_plan_the_same_Do_it_would_leave_new_suggestions()
    {
        // The control: the first plan carried out as it was, the 20 mercenaries hired by hand afterwards — the fresh plan
        // then wants food and horses for them. That is exactly what the live re-plan takes away.
        var s = Camp(flat: true);
        var plan = s.Plan();
        ApplyDoIt(s, plan);
        HireByHand(s, "merc", 20);
        var fresh = s.Plan();
        Assert.True(fresh.Row("food:grain").Change + fresh.Row("food:fish").Change + fresh.Row("food:cheese").Change > 0);
        Assert.True(fresh.Row("mounts:riding").Change > 0);
    }

    // ── The party after the deal ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Hiring_foot_mercenaries_feeds_and_mounts_them()
    {
        var plan = Camp(flat: true).Plan();
        Assert.Equal(40, plan.Facts.FoodEaters);        // 40 men; the lord stays: 1/2 = 0
        Assert.Equal(80, plan.Facts.FoodTarget);
        Assert.Equal(30, plan.Facts.Footmen);
        Assert.Equal(33, plan.Facts.RidingTarget);
        int food = FoodChange(plan);
        int riding = plan.Row("mounts:riding").Change;

        Assert.Equal(20, plan.Increase("tavern:mercenaries", EditSize.All).After);
        Assert.Equal(60, plan.Facts.FoodEaters);
        Assert.Equal(120, plan.Facts.FoodTarget);
        Assert.Equal(50, plan.Facts.Footmen);
        Assert.Equal(55, plan.Facts.RidingTarget);
        Assert.Equal(food + 40, FoodChange(plan));
        Assert.Equal(riding + 22, plan.Row("mounts:riding").Change);
        Assert.Equal(60, plan.Totals.MembersAfter);
        Assert.False(plan.Row("mounts:riding").IsTouched); // the steward's row, following the party

        plan.Decrease("tavern:mercenaries", EditSize.All);  // the hire taken back: the steward follows back
        Assert.Equal(food, FoodChange(plan));
        Assert.Equal(riding, plan.Row("mounts:riding").Change);
    }

    [Fact]
    public void Mounted_men_eat_but_bring_their_own_horse()
    {
        var s = Camp(flat: true);
        s.Snap.Tavern!.Mercenaries!.IsMounted = true;
        var plan = s.Plan();
        int riding = plan.Row("mounts:riding").Change;
        plan.Increase("tavern:mercenaries", EditSize.All);
        Assert.Equal(60, plan.Facts.FoodEaters);
        Assert.Equal(30, plan.Facts.Footmen);
        Assert.Equal(riding, plan.Row("mounts:riding").Change);

        // A wanderer rides when his battle equipment has a horse (Bea); Arn walks.
        plan.Increase("tavern:wanderer:w2");
        Assert.Equal(30, plan.Facts.Footmen);
        plan.Increase("tavern:wanderer:w1");
        Assert.Equal(31, plan.Facts.Footmen);
    }

    [Fact]
    public void Prisoners_kept_eat_half_a_ration_and_ransomed_ones_stop_eating()
    {
        var plan = Camp(flat: true).Plan();
        Assert.Equal(40, plan.Facts.FoodEaters);                     // looters and bandits ransomed, the lord kept
        plan.Increase("prisoner:looter", EditSize.All);               // keep the 12 looters
        Assert.Equal(40 + (12 + 1) / 2, plan.Facts.FoodEaters);      // the game halves the whole prison, integer
        plan.Increase("prisoner:bandit", EditSize.All);               // and the 5 bandits
        Assert.Equal(40 + (12 + 5 + 1) / 2, plan.Facts.FoodEaters);
        Assert.Equal(2 * (40 + 9), plan.Facts.FoodTarget);
        plan.Decrease("prisoner:lord_x");                            // ransom the lord too
        Assert.Equal(40 + (12 + 5) / 2, plan.Facts.FoodEaters);
    }

    [Fact]
    public void Hired_troops_put_their_kind_of_upgrade_horse_in_play_never_ready()
    {
        // Nobody upgrades into a war horse here; the player keeps a fixed 4 of them "for upgrades" — only once some troop
        // needs one. Mercenaries whose upgrade needs a war horse bring the kind in play (not ready: 0 + the fixed number).
        var s = new Scenario().Party(10).Gold(100_000).Mount("charger", "war_horse", market: 10, buy: 1500, noAverage: true);
        s.Settings.WarMountsWarHorseTarget = 4;
        s.Snap.Tavern = new TavernInfo
        {
            Mercenaries = new MercenaryOffer
            {
                TroopId = "merc", Name = "Squires", Available = 6, PricePerMan = 80, IsMounted = true,
                UpgradeCategories = { "war_horse" },
            },
        };
        var plan = s.Plan();
        Assert.Null(plan.FindRow("mounts:upgrade:war_horse"));
        int layout = plan.Layout;

        plan.Increase("tavern:mercenaries");
        var row = plan.Row("mounts:upgrade:war_horse");             // a new row — the window rebuilds its table
        Assert.NotEqual(layout, plan.Layout);
        Assert.Equal(4, row.Need);
        Assert.Equal(4, row.Change);
        Assert.Equal(0, plan.Facts.UpgradeReady["war_horse"]);      // new men are never ready

        plan.Decrease("tavern:mercenaries");
        Assert.Null(plan.FindRow("mounts:upgrade:war_horse"));      // nobody needs one again: the steward's row goes
    }

    [Fact]
    public void Men_who_leave_a_stack_take_its_ready_count_down_with_them()
    {
        // Step 16's dismissals yield the same party moves: 10 recruits, 6 ready for a horse; 7 leave → at most 3 ready.
        var snap = new StewardSnapshot
        {
            Upgrades =
            {
                new UpgradeStack { TroopId = "recruit", Count = 10, Targets = { new UpgradeTarget { RequiredCategoryId = "horse", ReadyCount = 6 } } },
            },
        };
        Assert.Equal(6, UpgradeNeeds.Of(snap).ReadyFor("horse"));
        var after = UpgradeNeeds.Of(snap, new[] { new PartyMove("recruit", -7, isMounted: false, null) });
        Assert.Equal(3, after.ReadyFor("horse"));
        var party = PartyAfter.Of(new StewardSnapshot { Party = new PartyInfo { Members = 10, Footmen = 10 } },
            new[] { new PartyMove("recruit", -7, isMounted: false, null), new PartyMove("rider", 4, isMounted: true, null) });
        Assert.Equal(7, party.Members);
        Assert.Equal(3, party.Footmen);
    }

    // ── Touched and untouched rows ───────────────────────────────────────────────────────────────────

    [Fact]
    public void A_touched_row_keeps_its_number_and_the_steward_plans_the_rest_around_it()
    {
        var plan = Camp(flat: true).Plan();
        plan.Increase("food:cheese", EditSize.Five);                 // the player's cheese: +5 more than suggested
        int cheese = plan.Row("food:cheese").Change;
        int others = plan.Row("food:grain").Change + plan.Row("food:fish").Change;
        Assert.True(plan.Row("food:cheese").IsTouched);

        plan.Increase("tavern:mercenaries", EditSize.All);           // 20 more eaters: +40 food
        Assert.Equal(cheese, plan.Row("food:cheese").Change);        // the player's number stays…
        Assert.Equal(others + 40 - 5, plan.Row("food:grain").Change + plan.Row("food:fish").Change); // …the steward fills the rest
        Assert.Equal(plan.Facts.FoodTarget, plan.Totals.FoodUnitsAfter);
    }

    [Fact]
    public void An_edit_that_does_not_change_the_party_re_plans_nothing()
    {
        var plan = Camp(flat: true).Plan();
        int fish = plan.Row("food:fish").Change;
        var facts = plan.Facts;
        plan.Increase("food:grain", EditSize.Five);
        Assert.Equal(fish, plan.Row("food:fish").Change);            // predictable beats clever (step 4b) — still
        Assert.Same(facts, plan.Facts);
    }

    [Fact]
    public void The_players_rows_take_the_stock_and_price_room_first()
    {
        // 30 hunters on offer. The player's upgrade row wants 20 of them; the steward's riding row gets what is left — and
        // a hire that adds footmen cannot take the player's horses away.
        var s = new Scenario().Village().Party(10, footmen: 10).Gold(100_000).Upgrade("recruit", 10, ("horse", 2))
            .Mount("hunter", "horse", market: 30, buy: 200);
        s.Snap.SettlementKind = SettlementKind.Town; // a town's tavern, flat prices
        s.Snap.Tavern = new TavernInfo { Mercenaries = new MercenaryOffer { TroopId = "merc", Name = "Spears", Available = 30, PricePerMan = 50 } };
        var plan = s.Plan();
        Assert.Equal(20, plan.SetChange("mounts:upgrade:horse", 20).After);
        plan.Increase("tavern:mercenaries", EditSize.All);           // 40 footmen → 44 riding wanted
        Assert.Equal(20, plan.Row("mounts:upgrade:horse").Change);   // the player's, untouched by the re-plan
        Assert.Equal(10, plan.Row("mounts:riding").Change);          // what the market has left
        Assert.Equal(EditBlock.NeededByAnotherRow, plan.Row("mounts:riding").IncreaseBlock);
        Assert.Equal(44, plan.Facts.RidingTarget);
    }

    [Fact]
    public void Hires_come_first_for_the_purse_and_the_stewards_buys_give_way()
    {
        // 6,000 gold: the steward buys food down to MinGoldAfterDeal (1,000). Hiring 40 men at 100 still works — the
        // steward's food gives way to the player's hires (they are paid first) — and the floor holds when it can.
        var s = new Scenario().Party(10).Gold(6_000).Food("grain", market: 1_000, buy: 10);
        s.Snap.Tavern = new TavernInfo { Mercenaries = new MercenaryOffer { TroopId = "merc", Name = "Spears", Available = 40, PricePerMan = 100 } };
        var plan = s.Plan();
        Assert.Equal(20, plan.Row("food:grain").Change);
        Assert.Equal(40, plan.Increase("tavern:mercenaries", EditSize.All).After); // 4,000 of hires
        Assert.Equal(100, plan.Row("food:grain").Change);           // 50 men × 2 — 1,000 of food: exactly to the floor
        Assert.Equal(1_000, plan.Totals.GoldAfter);
        Assert.False(plan.Totals.BelowMinGoldAfterDeal);

        plan.Decrease("tavern:mercenaries", EditSize.All);
        Assert.Equal(20, plan.Row("food:grain").Change);
    }

    [Fact]
    public void A_hire_the_re_planned_deal_cannot_pay_steps_back_to_what_it_can()
    {
        // No gold, 300 grain to sell. Every man hired eats: the more hired, the less surplus the steward sells — so the
        // purse that paid for the hires before the re-plan no longer does after it. The click stops where it still pays.
        var s = new Scenario().Party(10).Gold(0, marketGold: 100_000).Food("grain", held: 300, sell: 10);
        s.Snap.Tavern = new TavernInfo { Mercenaries = new MercenaryOffer { TroopId = "merc", Name = "Spears", Available = 40, PricePerMan = 100 } };
        var plan = s.Plan();
        Assert.Equal(-280, plan.Row("food:grain").Change);
        var hire = plan.Increase("tavern:mercenaries", EditSize.All);
        Assert.False(plan.Totals.CannotAfford);
        Assert.True(hire.After is > 0 and < 40);
        // one more would not pay: (300 − 2 × (10 + n + 1)) × 10 < 100 × (n + 1)
        int n = hire.After;
        Assert.True((300 - 2 * (10 + n + 1)) * 10 < 100 * (n + 1));
        Assert.True((300 - 2 * (10 + n)) * 10 >= 100 * n);
        Assert.Equal(-(300 - 2 * (10 + n)), plan.Row("food:grain").Change);
    }

    [Fact]
    public void Reset_hands_a_row_back_and_Reset_all_restores_the_first_plan()
    {
        var plan = Camp(flat: false).Plan();
        string first = plan.Describe();
        plan.Increase("food:cheese", EditSize.Five);
        plan.Increase("tavern:mercenaries", EditSize.All);
        plan.Increase("prisoner:bandit", EditSize.All);
        Assert.NotEqual(first, plan.Describe());

        var reset = plan.Reset("food:cheese");                       // the steward's again: it follows the new party
        Assert.False(plan.Row("food:cheese").IsTouched);
        Assert.Equal(plan.Facts.FoodTarget, plan.Totals.FoodUnitsAfter);
        Assert.NotEqual(reset.Before, reset.After);

        plan.ResetAll();
        Assert.False(plan.IsEdited);
        Assert.Equal(first, plan.Describe());
    }

    [Fact]
    public void A_settings_re_plan_carries_the_hires_and_plans_for_them()
    {
        var s = Camp(flat: true);
        var plan = s.Plan();
        plan.Increase("tavern:mercenaries", EditSize.All);
        var carry = PlanCarryOver.Capture(plan);
        s.Settings.MountsPer100Footmen = 120;                         // a setting changes: the window plans again
        var fresh = s.Plan();
        carry.ApplyTo(fresh);
        Assert.Equal(20, fresh.Row("tavern:mercenaries").Change);
        Assert.Equal(60, fresh.Facts.FoodEaters);
        Assert.Equal(60, fresh.Facts.RidingTarget);                   // 50 footmen × 1.2
    }

    [Fact]
    public void Adopting_a_re_planned_row_takes_every_number_it_has()
    {
        // PlanRow.AdoptFrom must list every settable property — a new one forgotten there would stay stale after a re-plan.
        var plan = Scenario.BusyTown().Plan();
        var props = typeof(PlanRow).GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.SetMethod != null && p.Name is not ("Owner" or "IsTouched"))
            .ToList();
        Assert.Contains(props, p => p.Name == "HeldStacks");
        foreach (var planned in plan.Rows)
        {
            var live = new PlanRow(planned.Id, planned.Section, planned.Type);
            live.AdoptFrom(planned);
            foreach (var p in props)
                Assert.True(Equals(p.GetValue(planned), p.GetValue(live)), planned.Id + ": " + p.Name + " not adopted");
        }
    }

    [Fact]
    public void A_party_click_on_a_big_plan_stays_within_the_click_budget()
    {
        var s = PlanPerformanceTests.BigTown();
        s.Snap.Tavern!.Mercenaries!.UpgradeCategories.Add("horse");
        var plan = s.Plan();
        PlanPerformanceTests.RefreshLikeTheWindow(plan);
        var clicks = new List<Action>
        {
            () => plan.Increase("tavern:mercenaries", EditSize.Five),
            () => plan.Increase("tavern:mercenaries", EditSize.All),
            () => plan.Increase("tavern:wanderer:w1"),
            () => plan.Increase("prisoner:prisoner0", EditSize.All),
            () => plan.Decrease("tavern:mercenaries", EditSize.Five),
            () => plan.Reset("tavern:wanderer:w1"),
            () => plan.Increase("food:grain"),
            () => plan.ResetAll(),
        };
        var times = new List<long>();
        var edits = new List<double>();
        var clock = new Stopwatch();
        foreach (var click in clicks)
        {
            clock.Restart();
            click();
            edits.Add(clock.Elapsed.TotalMilliseconds);
            PlanPerformanceTests.RefreshLikeTheWindow(plan);
            times.Add(clock.ElapsedMilliseconds);
        }
        _output.WriteLine("party clicks (edit + re-plan + refresh), ms: " + string.Join(", ", times));
        _output.WriteLine("  of which the edit with its re-plan, ms: " + string.Join(", ", edits.Select(e => e.ToString("0.0"))));
        // Measured 2026.09.28 (Release, this machine): a re-planning click 15-26 ms (the re-plan itself a few ms; the rest is
        // the window's refresh, one trial walk per live button, as for every click since step 9 — ordinary clicks 14-19 ms).
        // Generous for a slower machine.
        Assert.True(times.Max() < 100, "slowest click " + times.Max() + " ms");
    }

    // ── The scenario and Do it ───────────────────────────────────────────────────────────────────────

    /// <summary>A town with everything a party change moves: 40 men (30 on foot), upgrades ready for horses and war horses,
    /// food, pack animals and horses on offer, prisoners, two wanderers (Arn on foot, Bea mounted) and 20 foot mercenaries
    /// whose upgrade needs a horse. Rich enough that no floor binds.</summary>
    private static Scenario Camp(bool flat)
    {
        var s = new Scenario().Party(40, footmen: 30).Gold(200_000, marketGold: 50_000)
            .Upgrade("recruit", 12, (null, 6), ("horse", 6))
            .Upgrade("footman", 8, ("war_horse", 3))
            .Food("grain", held: 30, market: 400, buy: 10, sell: 7)
            .Food("fish", held: 10, market: 300, buy: 14, sell: 10)
            .Food("cheese", market: 200, buy: 25, sell: 18)
            .Pack("mule", held: 6, market: 30, buy: 140, sell: 70)
            .Mount("hunter", "horse", held: 10, market: 80, buy: 210, sell: 100)
            .Mount("steppe", "horse", market: 60, buy: 260, sell: 120)
            .Mount("charger", "war_horse", held: 1, market: 20, buy: 1500, sell: 700)
            .Prisoner("looter", 12, 20)
            .Prisoner("bandit", 5, 45)
            .Prisoner("lord_x", 1, 3000, hero: true);
        s.Settings.AutoFillWarMountPrices = true;
        s.Snap.AveragePrices["charger"] = new AveragePrices(1500, 700);
        s.Snap.Tavern = new TavernInfo
        {
            Wanderers =
            {
                new WandererForHire { HeroId = "w1", Name = "Arn", HirePrice = 700, DailyWage = 10 },
                new WandererForHire { HeroId = "w2", Name = "Bea", HirePrice = 900, DailyWage = 12, IsMounted = true },
            },
            Mercenaries = new MercenaryOffer
            {
                TroopId = "merc", Name = "Spearmen", Available = 20, PricePerMan = 60, WagePerMan = 3,
                UpgradeCategories = { "horse" },
            },
        };
        s.Oracle.Slope = flat ? 0 : 0.00001;
        return s;
    }

    private static void Edit(StewardPlan plan, string edit)
    {
        switch (edit)
        {
            case "hire 20 foot mercenaries":
                Assert.Equal(20, plan.Increase("tavern:mercenaries", EditSize.All).After);
                break;
            case "keep the prisoners, then ransom them all":
                foreach (var id in new[] { "prisoner:looter", "prisoner:bandit" })
                    plan.Increase(id, EditSize.All);
                Assert.Equal(40 + 9, plan.Facts.FoodEaters);
                foreach (var id in new[] { "prisoner:looter", "prisoner:bandit", "prisoner:lord_x" })
                    plan.Decrease(id, EditSize.All);
                Assert.Equal(-18, plan.Rows.Where(r => r.Type == RowType.Prisoner).Sum(r => r.Change));
                break;
            case "hire a wanderer":
                Assert.Equal(1, plan.Increase("tavern:wanderer:w1").After);
                break;
            case "a mix":
                plan.Increase("food:cheese", EditSize.Five);
                plan.SetChange("tavern:mercenaries", 12);
                plan.Increase("tavern:wanderer:w2");
                plan.Increase("prisoner:bandit", EditSize.All);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(edit));
        }
    }

    private static int FoodChange(StewardPlan plan) => plan.Rows.Where(r => r.Type == RowType.Food).Sum(r => r.Change);

    /// <summary>No food, pack, riding or upgrade-horse row moves (food within its surplus tolerance: sold only above it).</summary>
    private static void NothingNew(StewardPlan plan, string what)
    {
        foreach (var row in plan.Rows.Where(r => r.Type is RowType.Food or RowType.Pack or RowType.Mount or RowType.WarMount))
            Assert.True(row.Change == 0, what + ": the fresh plan still suggests " + row.Id + " " + row.Change);
    }

    /// <summary>What the executor does to the world, done to the snapshot: every transaction of the plan.</summary>
    private static void ApplyDoIt(Scenario s, StewardPlan plan)
    {
        var snap = s.Snap;
        foreach (var tx in plan.Transactions)
        {
            switch (tx.Kind)
            {
                case TransactionKind.Donate:
                case TransactionKind.Ransom:
                    snap.Prisoners.Single(p => p.TroopId == tx.TroopId).Count -= tx.Count;
                    if (tx.Kind == TransactionKind.Ransom) snap.PlayerGold += tx.Gold;
                    break;
                case TransactionKind.Sell:
                    Move(snap.Inventory, snap.Market, tx.StackKey!, tx.Count);
                    snap.PlayerGold += tx.Gold;
                    snap.MarketGold -= tx.Gold;
                    break;
                case TransactionKind.Buy:
                    Move(snap.Market, snap.Inventory, tx.StackKey!, tx.Count);
                    snap.PlayerGold -= tx.Gold;
                    snap.MarketGold += tx.Gold;
                    break;
                case TransactionKind.HireWanderer:
                {
                    var hero = snap.Tavern!.Wanderers.Single(w => w.HeroId == tx.HeroId);
                    snap.Tavern.Wanderers.Remove(hero);
                    snap.Party.Members++;
                    if (!hero.IsMounted) snap.Party.Footmen++;
                    snap.Party.CompanionSlotsFree--;
                    snap.PlayerGold -= tx.Gold;
                    break;
                }
                case TransactionKind.HireMercenaries:
                {
                    var band = snap.Tavern!.Mercenaries!;
                    band.Available -= tx.Count;
                    band.InParty += tx.Count;
                    snap.Party.Members += tx.Count;
                    if (!band.IsMounted) snap.Party.Footmen += tx.Count;
                    snap.Upgrades.Add(new UpgradeStack
                    {
                        TroopId = band.TroopId,
                        Count = tx.Count,
                        Targets = band.UpgradeCategories.Select(c => new UpgradeTarget { RequiredCategoryId = c, ReadyCount = 0 }).ToList(),
                    });
                    snap.PlayerGold -= tx.Gold;
                    break;
                }
            }
        }
        snap.Inventory.RemoveAll(x => x.Count <= 0);
        snap.Market.RemoveAll(x => x.Count <= 0);
        snap.Prisoners.RemoveAll(p => p.Count <= 0);
    }

    /// <summary>Hires men by hand after the deal (the control case: no re-plan saw them).</summary>
    private static void HireByHand(Scenario s, string troop, int count)
    {
        var band = s.Snap.Tavern!.Mercenaries!;
        band.Available -= count;
        s.Snap.Party.Members += count;
        if (!band.IsMounted) s.Snap.Party.Footmen += count;
        s.Snap.Upgrades.Add(new UpgradeStack
        {
            TroopId = troop, Count = count,
            Targets = band.UpgradeCategories.Select(c => new UpgradeTarget { RequiredCategoryId = c }).ToList(),
        });
    }

    private static void Move(List<ItemStack> from, List<ItemStack> to, string key, int count)
    {
        var source = from.Single(x => x.Key == key);
        source.Count -= count;
        var target = to.FirstOrDefault(x => x.Key == key);
        if (target == null)
        {
            target = new ItemStack
            {
                Key = source.Key, ItemId = source.ItemId, Name = source.Name, ModifierId = source.ModifierId, Kind = source.Kind,
                CategoryId = source.CategoryId, LootGroup = source.LootGroup, UnitWeight = source.UnitWeight,
                UnitWeightAtSea = source.UnitWeightAtSea, UnitValue = source.UnitValue, StoreValueStep = source.StoreValueStep,
            };
            to.Add(target);
        }
        target.Count += count;
    }
}
