using System.Diagnostics;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;
using Xunit.Abstractions;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// PLAN step 9, review area 5: a click on a BIG plan must not stutter. The window re-reads every row after a click —
/// its numbers, both live button blocks (a trial walk each) and the footer — so the cost of one click is the edit
/// plus that refresh. The scenario is a late-game party after a few battles: hundreds of loot pieces in ~250 kinds,
/// every food, many horse types, a full prison and a tavern.
/// </summary>
public class PlanPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public PlanPerformanceTests(ITestOutputHelper output) => _output = output;

    internal static Scenario BigTown()
    {
        var s = new Scenario().Party(180, footmen: 120).Gold(250_000, marketGold: 40_000);
        s.Settings.SellLoot = true;
        s.Oracle.Slope = 0.00005;
        string[] foods = { "grain", "meat", "fish", "cheese", "butter", "grape", "date_fruit", "olives", "beer" };
        for (int i = 0; i < foods.Length; i++)
            s.Food(foods[i], held: 20 + i * 5, market: 150, buy: 10 + i * 3, sell: 7 + i * 2);
        for (int i = 0; i < 4; i++)
            s.Pack("pack" + i, held: i, market: 15, buy: 120 + 20 * i, sell: 60 + 10 * i);
        for (int i = 0; i < 12; i++)
            s.Mount("horse" + i, "horse", held: 3, market: 12, buy: 200 + 15 * i, sell: 100 + 7 * i);
        for (int i = 0; i < 8; i++)
            s.Mount("war_horse" + i, "war_horse", held: 1, market: 6, buy: 900 + 60 * i, sell: 450 + 30 * i, noAverage: true);
        for (int i = 0; i < 4; i++)
            s.Mount("noble" + i, "noble_horse", held: 1, market: 2, buy: 2500 + 100 * i, sell: 1200, noAverage: true);
        for (int i = 0; i < 3; i++)
            s.Mount("horse" + i, "horse", held: 2, buy: 30, sell: 12, modifier: "lame_horse", priceFactor: Scenario.Lame);
        s.Settings.AutoFillWarMountPrices = true;
        s.Settings.WarMountsToKeep = 12; // step 17: a plain number of war horses (the upgrade stacks are gone)
        var groups = new[] { LootGroup.Armour, LootGroup.MeleeWeapons, LootGroup.Ranged, LootGroup.Shields };
        for (int i = 0; i < 250; i++)
            s.Loot("loot" + i, groups[i % 4], held: 1 + i % 5, sell: 5 + (i * 37) % 400, weight: 1 + i % 9,
                category: "cat" + (i % 12));
        for (int i = 0; i < 20; i++)
            s.Prisoner("prisoner" + i, 3 + i % 7, 20 + 15 * i);
        s.Snap.Tavern = new TavernInfo
        {
            Wanderers =
            {
                new WandererForHire { HeroId = "w1", Name = "Arn", HirePrice = 700, DailyWage = 10 },
                new WandererForHire { HeroId = "w2", Name = "Bea", HirePrice = 800, DailyWage = 12 },
                new WandererForHire { HeroId = "w3", Name = "Cid", HirePrice = 1200, DailyWage = 14 },
            },
            Mercenaries = new MercenaryOffer { TroopId = "merc", Name = "Blades", Available = 20, PricePerMan = 100 },
        };
        // Step 16: a late-game party's 30 troop types, 6 of them on offer here too, plus 4 more volunteers' types.
        for (int i = 0; i < 30; i++)
            s.Troop("unit" + i, inParty: 2 + i % 4, wounded: i % 3, onOffer: i < 6 ? 3 : 0, price: 20 + 10 * i,
                mounted: i % 3 == 0);
        for (int i = 0; i < 4; i++)
            s.Troop("volunteer" + i, onOffer: 2 + i, price: 15 + 5 * i);
        return s;
    }

    /// <summary>What the window does after every click (step 21: SuggestionTabVM.Refresh + RefreshDoIt): the spreadsheet's view
    /// with EVERY part open — the worst case: every row's cells, notes and both live blocks, every breakdown line, the troop
    /// lines' blocks —, the weight table, the footer's warnings and Do it. Folded parts cost less (their rows are not asked).</summary>
    internal static int RefreshLikeTheWindow(StewardPlan plan)
    {
        int n = 0;
        var view = SheetView.Build(plan);
        foreach (var section in view.Sections)
        {
            n += section.Cells.Denari.Length;
            foreach (var item in section.Items)
                n += item.Note.Length + item.Cells.Denari.Length + (int)item.IncreaseBlock + (int)item.DecreaseBlock;
        }
        n += view.Sheet.Weights.Count;
        n += PlanFooter.Warnings(plan.Totals).Count;
        n += PlanFooter.CanExecute(plan) ? 1 : 0;
        return n;
    }

    [Fact]
    public void A_click_on_a_big_plan_stays_well_under_a_frame_budget()
    {
        var s = BigTown();
        var clock = Stopwatch.StartNew();
        var plan = s.Plan();
        RefreshLikeTheWindow(plan);
        long open = clock.ElapsedMilliseconds;
        int openCalls = s.Oracle.Calls.Count;
        Assert.True(openCalls < 2_500, "opening asked the game for " + openCalls + " prices");

        int pieces = plan.Rows.Where(r => r.Type == RowType.Loot).Sum(r => r.Mine);
        Assert.True(pieces >= 600, "the scenario should hold hundreds of loot pieces, has " + pieces);
        Assert.True(plan.Rows.Count() >= 30);

        // A mix of the clicks a player makes: loot and food back and forth, a horse, a prisoner, a hire, a reset.
        var clicks = new List<Action>
        {
            () => plan.Decrease("loot:Armour"),
            () => plan.Increase("loot:Armour", EditSize.Five),
            () => plan.Increase("food:grain"),
            () => plan.Decrease("food:fish", EditSize.Five),
            () => plan.Increase("mounts:riding"),
            () => plan.Decrease("prisoner:prisoner3"),
            () => plan.Increase("tavern:mercenaries", EditSize.Five),
            () => plan.Decrease("loot:MeleeWeapons", EditSize.All),
            () => plan.Increase("troops:volunteer2", EditSize.All),
            () => plan.Decrease("troops:unit12", EditSize.All),
            () => plan.ResetAll(),
        };
        var times = new List<long>();
        var calls = new List<int>();
        foreach (var click in clicks)
        {
            int before = s.Oracle.Calls.Count;
            clock.Restart();
            click();
            RefreshLikeTheWindow(plan);
            times.Add(clock.ElapsedMilliseconds);
            calls.Add(s.Oracle.Calls.Count - before);
        }
        _output.WriteLine("open (plan + first refresh): " + open + " ms, " + openCalls + " game price calls; rows "
                          + plan.Rows.Count() + ", loot pieces " + pieces);
        _output.WriteLine("clicks (edit + refresh), ms: " + string.Join(", ", times));
        _output.WriteLine("game price calls per click (cache misses): " + string.Join(", ", calls));

        // Measured 2026.09.27 (Release, this machine): open ~58 ms, clicks <= ~21 ms, 0-2 new game price calls a
        // click — before step 9's fixes the clicks took up to ~48 ms and the open asked the game ~4,850 prices
        // (now ~1,420). A frame is ~16 ms: a click may cost about one, never a visible hitch. Limits are generous
        // for a slower machine; the game price calls are counted exactly.
        Assert.True(times.Max() < 100, "slowest click " + times.Max() + " ms");
        Assert.True(open < 400, "opening took " + open + " ms");
        // Step 15: a party-changing click re-plans (the planner again, from the warm cache), and the player's rows walk
        // first — both reach price points the first walk never needed: a few dozen new game prices at most, microseconds.
        Assert.True(calls.Max() <= 60, "a click asked the game for " + calls.Max() + " new prices");
    }
}
