using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// Step 26 (DESIGN §2.9, RESEARCH §29 — Anton 2026.10.01): the steward reads the player's quests and KEEPS what they ask for —
/// never sold, ransomed, donated or dismissed — and buys a food a quest asks for up to the need, like a goal of his. A goal he
/// typed wins; the switch "Keep what your quests need" turns it off.
/// </summary>
public class QuestPlanTests
{
    private static Scenario Quest(Scenario s, string questId, string title, QuestNeedKind kind, int amount, string what,
        params string[] ids)
    {
        s.Snap.QuestNeeds.Add(new QuestNeed
        {
            QuestId = questId,
            Title = title,
            Kind = kind,
            Amount = amount,
            What = what,
            Ids = ids.ToList(),
        });
        return s;
    }

    private static Scenario Grain(Scenario s, int amount = 120) =>
        Quest(s, "q_grain", "Ryibelet Needs Grain Seeds", QuestNeedKind.Items, amount, "Grain", "grain");

    // ── Food ─────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_quest_keeps_its_grain_from_the_surplus_sale()
    {
        // Target 20 (10 men, 40 days), two kinds: share 10. 150 grain of which a quest keeps 120: grain counts 30 + 10 = 40, the
        // fish 30 — the steward sells the 30 free grain and the fish down to its share, never a grain the quest wants.
        var s = Grain(new Scenario().Party(10).Food("grain", held: 150).Food("fish", held: 30));
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(-30, grain.Change);
        Assert.Equal(120, grain.Result);
        Assert.Equal(-20, plan.Row("food:fish").Change);
        Assert.False(grain.IsTouched); // no ⟲: the row follows the quest, not a goal of yours
        Assert.Equal(120, grain.Quest!.Kept);

        LivePlanTests.ApplyDoIt(s, plan);
        AssertNothingNew(s.Plan());
    }

    [Fact]
    public void The_switch_off_reads_no_quest()
    {
        var s = Grain(new Scenario().Party(10).Food("grain", held: 150).Food("fish", held: 30));
        s.Settings.QuestGoalsEnabled = false;
        var plan = s.Plan();
        Assert.True(plan.Row("food:grain").Result < 120);
        Assert.Null(plan.Row("food:grain").Quest);
    }

    [Fact]
    public void A_quest_need_buys_the_grain_like_a_goal_of_yours_and_Do_it_leaves_nothing_new()
    {
        // 20 grain held, the quest asks 120: the row buys 100, first — and counts only its share (10) toward the days, so the
        // fish still gets its 10.
        var s = Grain(new Scenario().Party(10).Food("grain", held: 20, market: 200).Food("fish", market: 200));
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(100, grain.Change);
        Assert.True(grain.Quest!.Buys);
        Assert.False(grain.IsTouched);
        Assert.Equal(10, plan.Row("food:fish").Change);
        Assert.Contains(plan.Transactions, t => t.Kind == TransactionKind.Buy && t.StackKey == "grain" && t.Count == 100);

        var goal = RowGoal.Of(grain);
        Assert.True(goal.IsQuest);
        Assert.False(goal.IsYours);
        Assert.Equal(120, goal.Value);

        LivePlanTests.ApplyDoIt(s, plan);
        var fresh = s.Plan();
        AssertNothingNew(fresh);
        Assert.False(fresh.Row("food:grain").Quest!.Buys); // held = need: the steward's row again, kept from sale
    }

    [Fact]
    public void Nothing_is_bought_when_what_you_hold_satisfies_the_need()
    {
        var s = Grain(new Scenario().Party(10).Food("grain", held: 130, market: 200).Food("fish", held: 10, market: 200));
        var plan = s.Plan();
        Assert.True(plan.Row("food:grain").Change <= 0);
        Assert.False(plan.Row("food:grain").Quest!.Buys);
    }

    [Fact]
    public void A_quest_need_answers_to_the_purse_floor_switch()
    {
        var s = Grain(new Scenario().Party(10).Gold(1_500).Food("grain", held: 20, market: 200));
        s.Settings.ManualGoalsKeepPurseFloor = true; // the real default: buys stop at MinGoldAfterDeal (1,000)
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(50, grain.Change); // 500 denari above the floor at 10 a unit
        var goal = RowGoal.Of(grain);
        Assert.True(goal.IsQuest);
        Assert.Equal(GoalShort.PurseFloor, goal.Short);
    }

    [Fact]
    public void A_quest_need_waits_with_the_steward_under_the_thresholds_switch_but_still_keeps()
    {
        var s = Grain(new Scenario().Party(10).Gold(1_500).Food("grain", held: 20, market: 200));
        s.Settings.FoodMinDenari = 5_000;
        s.Settings.ManualGoalsWaitForThresholds = true;
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(0, grain.Change);
        var goal = RowGoal.Of(grain);
        Assert.True(goal.IsQuest);
        Assert.False(goal.HandsOff); // the quest's 120, not "-*"
        Assert.Equal(120, goal.Value);
        Assert.Equal(GoalShort.Threshold, goal.Short);

        s.Settings.ManualGoalsWaitForThresholds = false; // the default: like your goal, it acts below the threshold
        Assert.Equal(100, s.Plan().Row("food:grain").Change);
    }

    [Fact]
    public void A_goal_you_typed_wins_and_the_hover_says_it_is_below_the_quest()
    {
        var s = Grain(new Scenario().Party(10).Food("grain", held: 150).Food("fish", held: 30));
        s.Settings.Goals["food:grain"] = 50;
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(50, grain.Result); // your goal sells below the quest's 120
        var goal = RowGoal.Of(grain);
        Assert.True(goal.IsYours);
        Assert.False(goal.IsQuest);
        Assert.True(goal.BelowQuest);
        var cell = SheetView.GoalCell(grain, plan);
        Assert.StartsWith("Below what your quests need:\nRyibelet Needs Grain Seeds", cell.QuestHint);
    }

    [Fact]
    public void A_click_on_a_quest_row_is_a_goal_edit_and_may_go_below_the_need()
    {
        var s = Grain(new Scenario().Party(10).Food("grain", held: 150).Food("fish", held: 30));
        var plan = s.Plan();
        Assert.Equal(120, plan.Row("food:grain").Result);
        plan.Decrease("food:grain");
        var grain = plan.Row("food:grain");
        Assert.Equal(119, grain.Result);
        Assert.Equal(119, grain.ManualGoal);
        Assert.True(grain.IsTouched);
    }

    [Fact]
    public void Two_quests_asking_for_grain_add_up_and_the_hover_names_both()
    {
        var s = new Scenario().Party(10).Food("grain", held: 20, market: 0);
        Quest(s, "q1", "Ryibelet Needs Grain Seeds", QuestNeedKind.Items, 50, "Grain", "grain");
        Quest(s, "q2", "Army Needs Supply", QuestNeedKind.Items, 30, "Grain", "grain");
        var plan = s.Plan();
        var grain = plan.Row("food:grain");
        Assert.Equal(80, grain.Quest!.FoodGoal);
        Assert.Equal(0, grain.Change); // nothing on the market: kept, never sold
        var cell = SheetView.GoalCell(grain, plan);
        Assert.True(cell.IsQuest);
        Assert.Equal("80", cell.Text);
        Assert.Equal("Kept for your quests:\nRyibelet Needs Grain Seeds – 50 Grain, you hold 20\nArmy Needs Supply – 30 Grain, you hold 0",
            cell.QuestHint);
    }

    [Fact]
    public void A_party_change_re_plans_around_the_quest_and_Do_it_leaves_nothing_new()
    {
        var s = Grain(new Scenario().Party(10).Food("grain", held: 20, market: 200).Food("fish", market: 200)
            .Troop("recruit", onOffer: 5, price: 20));
        var plan = s.Plan();
        plan.Increase("troops:recruit", EditSize.All);
        Assert.Equal(5, plan.Row("troops:recruit").Change);
        Assert.Equal(100, plan.Row("food:grain").Change); // still the quest's 120
        Assert.True(plan.Row("food:grain").Quest!.Buys);
        Assert.True(plan.Row("food:fish").Change > 10);   // the steward's own kind follows the bigger party

        LivePlanTests.ApplyDoIt(s, plan);
        AssertNothingNew(s.Plan());
    }

    [Fact]
    public void The_autonomous_steward_obeys_the_quest_under_its_own_floor()
    {
        var s = Grain(new Scenario().Party(10).Gold(100_500).Food("grain", held: 20, market: 200));
        var plan = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Equal(50, plan.Row("food:grain").Change); // AutonomousMinGold 100,000 always holds
    }

    // ── Horses and pack animals ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_quest_keeps_its_horses_and_the_steward_sells_only_the_rest()
    {
        // No footmen: every riding horse is surplus — but the lord wants 2 Aserai horses.
        var s = new Scenario().Party(10).Mount("aserai_horse", "horse", held: 5).Mount("palfrey", "horse", held: 2, sell: 200);
        Quest(s, "q_horses", "Lord Needs Horses", QuestNeedKind.Items, 2, "Aserai Horse", "aserai_horse");
        var plan = s.Plan();
        var riding = plan.Row("mounts:riding");
        Assert.Equal(2, riding.Result);
        Assert.Equal(-3, riding.Moved("aserai_horse"));
        Assert.Equal(-2, riding.Moved("palfrey"));
        var goal = RowGoal.Of(riding);
        Assert.True(goal.IsQuest);
        Assert.Equal(2, goal.Value);

        LivePlanTests.ApplyDoIt(s, plan);
        AssertNothingNew(s.Plan());
    }

    [Fact]
    public void A_lame_horse_is_kept_for_the_quest_before_a_healthy_one()
    {
        // Every quest takes any modifier (RESEARCH §29): the cheapest units are kept, so the dear ones stay free to sell.
        var s = new Scenario().Party(10).Mount("aserai_horse", "horse", held: 3)
            .Mount("aserai_horse", "horse", held: 2, buy: 30, sell: 15, modifier: "lame", priceFactor: Scenario.Lame);
        Quest(s, "q_horses", "Lord Needs Horses", QuestNeedKind.Items, 2, "Aserai Horse", "aserai_horse");
        var plan = s.Plan();
        Assert.Equal(0, plan.Row("mounts:lame").Change); // both lame ones kept for the lord
        Assert.Equal(2, plan.Row("mounts:lame").Quest!.Kept);
        Assert.Equal(-3, plan.Row("mounts:riding").Change);
    }

    [Fact]
    public void Draught_animals_keep_the_pack_row_above_its_target()
    {
        var s = new Scenario().Party(10).Pack("mule", held: 12);
        s.Settings.PackAnimalsTarget = 5;
        Quest(s, "q_draught", "Village Needs Draught Animals", QuestNeedKind.Items, 10, "Mule", "mule");
        var plan = s.Plan();
        var pack = plan.Row("mounts:pack");
        Assert.Equal(-2, pack.Change);
        var goal = RowGoal.Of(pack);
        Assert.True(goal.IsQuest);
        Assert.Equal(10, goal.Value); // the quest's 10 over the target 5
    }

    [Fact]
    public void A_pack_goal_you_typed_may_sell_the_quests_animals()
    {
        var s = new Scenario().Party(10).Pack("mule", held: 12);
        s.Settings.Goals["mounts:pack"] = 4;
        Quest(s, "q_draught", "Village Needs Draught Animals", QuestNeedKind.Items, 10, "Mule", "mule");
        Assert.Equal(4, s.Plan().Row("mounts:pack").Result);
    }

    // ── Other: loot and goods ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_gangs_axes_and_the_villages_tools_are_never_sold()
    {
        var s = new Scenario().Party(10).Loot("axe", LootGroup.MeleeWeapons, held: 6, sell: 20)
            .Loot("sword", LootGroup.MeleeWeapons, held: 4, sell: 30).Goods("tools", held: 5, sell: 30);
        s.Settings.SellLoot = true;
        Quest(s, "q_axes", "Gang Leader Needs Weapons", QuestNeedKind.Items, 4, "one-handed axes", "axe");
        Quest(s, "q_tools", "Ryibelet Needs Tools", QuestNeedKind.Items, 5, "Tools", "tools");
        var plan = s.Plan();
        var melee = plan.Row("loot:MeleeWeapons");
        Assert.Equal(10, melee.Mine);
        Assert.Equal(-6, melee.Change);
        Assert.Equal(-2, melee.Moved("axe"));
        Assert.Equal(4, RowGoal.Of(melee).Value);
        Assert.True(RowGoal.Of(melee).IsQuest);
        var goods = plan.Row("loot:OtherGoods");
        Assert.Equal(0, goods.Change);
        Assert.Equal(EditBlock.NothingToSell, goods.DecreaseBlock); // kept apart like a locked piece

        LivePlanTests.ApplyDoIt(s, plan);
        Assert.All(s.Plan().Rows.Where(r => r.Type == RowType.Loot), r => Assert.Equal(0, r.Change));
    }

    // ── Prisoners ────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_landowners_laborers_are_kept_the_most_valuable_first()
    {
        var s = new Scenario().Party(10).Prisoner("looter", 10, ransom: 20).Prisoner("sea_raider", 5, ransom: 50);
        Quest(s, "q_laborers", "Landowner Needs Manual Laborers", QuestNeedKind.Prisoners, 8, "bandits", "looter", "sea_raider");
        var plan = s.Plan();
        Assert.Equal(0, plan.Row("prisoner:sea_raider").Change); // the 5 dearest kept (the quest pays 5 x their ransom)
        Assert.Equal(-7, plan.Row("prisoner:looter").Change);    // 3 looters kept, 7 ransomed
        var goal = RowGoal.Of(plan.Row("prisoner:looter"));
        Assert.True(goal.IsQuest);
        Assert.Equal(3, goal.Value);
        Assert.Equal(GoalShort.None, goal.Short);
    }

    [Fact]
    public void The_rival_a_lord_wants_is_never_ransomed()
    {
        var s = new Scenario().Party(10).Prisoner("lord_rival", 1, ransom: 5_000, hero: true);
        s.Settings.LordPrisonerAction = PrisonerChoice.Ransom;
        Quest(s, "q_rival", "Derthert Wants Caladog Captured", QuestNeedKind.Prisoners, 1, "Caladog", "lord_rival");
        var plan = s.Plan();
        Assert.Equal(0, plan.Row("prisoner:lord_rival").Change);
        Assert.Equal(EditBlock.AllSold, plan.Row("prisoner:lord_rival").DecreaseBlock);
        var cell = SheetView.GoalCell(plan.Row("prisoner:lord_rival"), plan);
        Assert.True(cell.IsQuest);
        Assert.Equal("Kept for your quests:\nDerthert Wants Caladog Captured – 1 Caladog", cell.QuestHint);
    }

    // ── Troops ───────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dismissing_never_takes_the_garrison_troops_or_the_gangs_bandits()
    {
        var s = new Scenario().Party(40).Troop("imperial_trained", inParty: 15, tier: 3)
            .Troop("looter", inParty: 6, tier: 0).Troop("mountain_bandit", inParty: 4, tier: 2);
        Quest(s, "q_garrison", "Lucon Needs Garrison Troops in Lageta", QuestNeedKind.Troops, 10, "Imperial Trained Infantry",
            "imperial_trained");
        Quest(s, "q_gang", "Gang Needs Recruits", QuestNeedKind.Troops, 5, "bandits", "looter", "mountain_bandit");
        var plan = s.Plan();
        plan.DismissLowest(EditSize.All);
        Assert.Equal(-5, plan.Row("troops:imperial_trained").Change);
        Assert.Equal(-5, plan.Row("troops:looter").Change);         // 1 looter kept with the 4 mountain bandits (highest tier first)
        Assert.Equal(0, plan.Row("troops:mountain_bandit").Change);
        var goal = RowGoal.Of(plan.Row("troops:imperial_trained"));
        Assert.True(goal.IsQuest);
        Assert.Equal(10, goal.Value);
        Assert.Null(RowGoal.Of(new Scenario().Troop("x", inParty: 3).Plan().Row("troops:x")).Value); // no quest: empty as ever
    }

    // ── The pieces ───────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_need_with_nothing_held_keeps_nothing_and_shows_nowhere()
    {
        var s = new Scenario().Party(10).Mount("palfrey", "horse", held: 2);
        Quest(s, "q_horses", "Lord Needs Horses", QuestNeedKind.Items, 5, "Aserai Horse", "aserai_horse");
        var plan = s.Plan();
        Assert.Null(plan.Row("mounts:riding").Quest);
        Assert.Equal(-2, plan.Row("mounts:riding").Change);
    }

    [Fact]
    public void Bad_needs_are_ignored()
    {
        var s = new Scenario().Party(10).Food("grain", held: 150).Food("fish", held: 30);
        Quest(s, "q0", "Zero", QuestNeedKind.Items, 0, "Grain", "grain");
        Quest(s, "q1", "No ids", QuestNeedKind.Items, 50, "Grain");
        s.Snap.QuestNeeds.Add(null!);
        var plan = s.Plan();
        Assert.Null(plan.Row("food:grain").Quest);
    }

    private static void AssertNothingNew(StewardPlan plan)
    {
        foreach (var row in plan.Rows.Where(r => r.Type is RowType.Food or RowType.Pack or RowType.Mount or RowType.WarMount
                     or RowType.Loot))
            Assert.True(row.Change == 0, "the fresh plan still suggests " + row.Id + " " + row.Change);
    }
}
