using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Tests.Planning;

/// <summary>
/// Step 34 — the steward in a castle (Anton 2026.10.03: "could you make it work in castles for the prisoners donations
/// there"; DESIGN §2.5, §9; RESEARCH §31): a castle has a dungeon but no market, no ransom broker, no tavern, no recruits —
/// the plan holds the Prisoners alone; Donate fills the dungeon most valuable first as in a town, lords too; a Ransom row has
/// nowhere to go there, so it stays.
/// </summary>
public class CastlePlanTests
{
    /// <summary>A castle where the steward may donate, the snapshot full of things it must leave alone there.</summary>
    private static Scenario Castle(int room = 100, bool allowed = true, DonateBlock block = DonateBlock.None)
    {
        var s = Scenario.BusyTown(); // food, horses, loot, a tavern, 8 looters + a lord
        s.Troop("recruit", inParty: 5, onOffer: 3, tier: 1);
        s.Snap.SettlementKind = SettlementKind.Castle;
        s.Snap.Prison = new PrisonInfo { CanRansom = true, DonateAllowed = allowed, DungeonRoom = room, DonateBlock = block };
        s.Prisoner("raider", 6, 44);
        return s;
    }

    [Fact]
    public void A_castle_plans_the_prisoners_alone_whatever_the_snapshot_holds()
    {
        var s = Castle();
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        var plan = s.Plan();
        Assert.Equal(new[] { PlanSectionKind.Prisoners }, plan.Sections.Select(x => x.Kind));
        Assert.All(plan.Transactions, t => Assert.Equal(TransactionKind.Donate, t.Kind));
        Assert.Empty(plan.Facts.Waiting); // no market job waits for the purse at a castle

        var sheet = SuggestionSheet.Of(plan);
        Assert.Equal(new[] { SheetGroup.Prisoners }, sheet.Sections.Select(x => x.Group));
        Assert.True(sheet.IsCastle);
        Assert.True(sheet.DonateAllowedHere);
        Assert.False(sheet.RansomAllowedHere);
    }

    [Fact]
    public void Donate_fills_the_castles_dungeon_most_valuable_first_and_the_rest_stays()
    {
        var s = Castle(room: 10);
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        var plan = s.Plan();
        // The raiders (44 each) go before the looters (20); no broker in a castle, so what does not fit is kept.
        Assert.Equal(6, plan.Row("prisoner:raider").Prisoner!.DonateCount);
        Assert.Equal(4, plan.Row("prisoner:looter").Prisoner!.DonateCount);
        Assert.Equal(0, plan.Row("prisoner:looter").Prisoner!.RansomCount);
        Assert.Equal(-4, plan.Row("prisoner:looter").Change);
        Assert.Equal(0, plan.Totals.GoldChange);
        Assert.True(plan.Totals.InfluenceGained > 0);
    }

    [Fact]
    public void A_lord_is_donated_to_a_castle_like_to_a_town()
    {
        var s = Castle();
        s.Settings.LordPrisonerAction = PrisonerChoice.Donate;
        s.Settings.PrisonerAction = PrisonerChoice.Keep;
        var plan = s.Plan();
        Assert.Equal(1, plan.Row("prisoner:lord_x").Prisoner!.DonateCount);
        Assert.Equal(0, plan.Row("prisoner:looter").Change);
    }

    [Fact]
    public void Ransom_does_nothing_in_a_castle_and_Keep_stays_Keep()
    {
        var s = Castle();
        s.Settings.PrisonerAction = PrisonerChoice.Ransom;
        s.Settings.LordPrisonerAction = PrisonerChoice.Keep;
        var plan = s.Plan();
        Assert.All(plan.Rows, r => Assert.Equal(0, r.Change));
        Assert.All(plan.Rows, r => Assert.Equal(0, r.MaxSell)); // not even by hand: there is no broker
        Assert.False(plan.HasChanges);
        Assert.Empty(plan.Transactions);
    }

    [Fact]
    public void Where_the_castle_forbids_donating_the_rows_stay_at_zero_and_the_window_says_why()
    {
        var s = Castle(allowed: false, block: DonateBlock.YourClansFief);
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        s.Settings.LordPrisonerAction = PrisonerChoice.Donate;
        var plan = s.Plan();
        Assert.NotEmpty(plan.Rows);
        Assert.All(plan.Rows, r => Assert.Equal(0, r.Change));
        Assert.All(plan.Rows, r => Assert.Equal(0, r.MaxSell));

        var view = SheetView.Build(plan);
        var lines = view.Section(SheetGroup.Prisoners)!.Items.Where(i => i.Choice != null).ToList();
        Assert.NotEmpty(lines);
        Assert.All(lines, i => Assert.False(i.DonateAllowed));
        Assert.All(lines, i => Assert.Equal(DonateBlock.YourClansFief, i.DonateBlock));
        Assert.All(lines, i => Assert.True(i.InCastle));

        Assert.Equal("A castle has no market - the steward only donates prisoners here. Donating is not possible here: "
                     + "it is your own clan's (manage its prisoners in the dungeon).", CastleNotice.Of(s.Snap));
        Assert.Equal(PopupVerdict.NothingToDonate, ArrivalPopup.Decide(plan, marketOpen: false, onlyWithChanges: false));
    }

    [Fact]
    public void A_full_dungeon_donates_nothing_and_keeps_the_popup_shut()
    {
        var s = Castle(room: 0, block: DonateBlock.DungeonFull);
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        var plan = s.Plan();
        Assert.False(plan.HasChanges);
        Assert.Equal(PopupVerdict.NothingToDonate, ArrivalPopup.Decide(plan, marketOpen: false, onlyWithChanges: true));
        Assert.EndsWith("its dungeon is full.", CastleNotice.Of(s.Snap));
    }

    [Fact]
    public void The_castle_popup_opens_only_with_a_donation_and_never_says_the_market_is_closed()
    {
        var s = Castle();
        s.Snap.CanTrade = false; // as the Module reads a castle: no market
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        Assert.Equal(PopupVerdict.Open, ArrivalPopup.Decide(s.Plan(), s.Snap.CanTrade, onlyWithChanges: true));

        s.Settings.PrisonerAction = PrisonerChoice.Keep;
        // Keep rows are rows, but a castle's window only ever donates: no popup, PopupOnlyWithChanges or not.
        Assert.Equal(PopupVerdict.NothingToDonate, ArrivalPopup.Decide(s.Plan(), s.Snap.CanTrade, onlyWithChanges: false));
        Assert.Equal("A castle has no market - the steward only donates prisoners here.", CastleNotice.Of(s.Snap));
    }

    [Fact]
    public void No_popup_when_every_prisoner_is_locked_kept_by_a_quest_or_on_Keep()
    {
        // Anton 2026.10.03: "no need to pop up if I dont have prisoners on me" - nor prisoners that cannot be donated here.
        var s = new Scenario().Party(10).Prisoner("looter", 5, 20, locked: true).Prisoner("raider", 3, 44)
            .Prisoner("lord_x", 1, 3000, hero: true);
        s.Snap.SettlementKind = SettlementKind.Castle;
        s.Snap.CanTrade = false;
        s.Snap.Prison = new PrisonInfo { DonateAllowed = true, DungeonRoom = 50 };
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        s.Settings.LordPrisonerAction = PrisonerChoice.Keep;
        s.Snap.QuestNeeds.Add(new QuestNeed { QuestId = "q", Title = "Laborers", Kind = QuestNeedKind.Prisoners, Ids = { "raider" }, What = "raiders", Amount = 3 });
        var plan = s.Plan();
        Assert.False(plan.HasChanges);
        Assert.Equal(PopupVerdict.NothingToDonate, ArrivalPopup.Decide(plan, s.Snap.CanTrade, onlyWithChanges: false));
    }

    [Fact]
    public void A_castle_without_prisoners_says_so()
    {
        var s = new Scenario().Party(10);
        s.Snap.SettlementKind = SettlementKind.Castle;
        s.Snap.Prison = new PrisonInfo { DonateAllowed = true, DungeonRoom = 50 };
        var plan = s.Plan();
        Assert.Empty(plan.Rows);
        Assert.Equal("A castle has no market - the steward only donates prisoners here. You hold no prisoners.",
            CastleNotice.Of(s.Snap));
        Assert.Null(CastleNotice.Of(new Scenario().Snap)); // a town has no castle notice
    }

    [Fact]
    public void The_autonomous_steward_donates_in_a_castle_and_nothing_else()
    {
        var s = Castle(room: 5);
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        var plan = StewardPlanner.Plan(s.Snap, s.Settings, s.Oracle, PlanMode.Autonomous);
        Assert.Equal(new[] { PlanSectionKind.Prisoners }, plan.Sections.Select(x => x.Kind));
        Assert.Equal(5, plan.Transactions.Where(t => t.Kind == TransactionKind.Donate).Sum(t => t.Count));
        Assert.All(plan.Transactions, t => Assert.Equal(TransactionKind.Donate, t.Kind));
        Assert.True(PlanFooter.CanExecute(plan));
    }

    [Fact]
    public void A_part_deal_of_the_prisoners_runs_the_donation()
    {
        var s = Castle(room: 3);
        s.Settings.PrisonerAction = PrisonerChoice.Donate;
        var plan = s.Plan();
        var deal = plan.DealOf(PlanPart.Prisoners);
        Assert.True(deal.CanRun);
        Assert.Equal(3, deal.Transactions.Sum(t => t.Count));
    }
}
