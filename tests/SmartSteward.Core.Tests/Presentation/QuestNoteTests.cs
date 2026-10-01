using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>
/// Step 32 (Anton 2026.10.01: "can you add some info, maybe next to the name 'Grain' -> 'Grain (100 needed for quest)'"): every
/// line a quest touches says it right after its name, in the quest colour — <c>120 needed for quest</c>, several quests summed,
/// <c>… for quest, held</c> when what the party holds covers it — before the grey note (<c>· 10 each</c>). Title lines do not.
/// </summary>
public class QuestNoteTests
{
    private static Scenario Quest(Scenario s, string questId, string title, QuestNeedKind kind, int amount, string what,
        params string[] ids)
    {
        s.Snap.QuestNeeds.Add(new QuestNeed { QuestId = questId, Title = title, Kind = kind, Amount = amount, What = what, Ids = ids.ToList() });
        return s;
    }

    private static Scenario Grain(Scenario s, int amount = 120) =>
        Quest(s, "q_grain", "Ryibelet Needs Grain Seeds", QuestNeedKind.Items, amount, "Grain", "grain");

    private static SheetItem Item(SheetView view, string key) =>
        view.Sections.SelectMany(s => s.Items).Single(i => i.Key == key);

    [Fact]
    public void One_quest_shows_its_need_after_the_name_and_the_price_follows_it()
    {
        // 20 grain held, the quest asks 120: the row buys 100 — "120 needed for quest · 10 each".
        var s = Grain(new Scenario().Party(10).Food("grain", held: 20, market: 200).Food("fish", market: 200));
        var grain = Item(SheetView.Build(s.Plan()), "row:food:grain");
        Assert.Equal("120 needed for quest", grain.QuestNote);
        Assert.Equal("10 each", grain.Note);                       // the grey note is unchanged
        Assert.Equal("· 10 each", grain.NoteAfterQuest);           // drawn after the blue part, joined by the dot
        Assert.Equal("120 needed for quest · 10 each", grain.FullNote);
        Assert.Equal("Your quests ask for:\nRyibelet Needs Grain Seeds – 120 Grain, you hold 20", grain.QuestNoteHint);

        var fish = Item(SheetView.Build(s.Plan()), "row:food:fish");
        Assert.Equal("", fish.QuestNote);                          // no quest: as ever
        Assert.Equal(fish.Note, fish.NoteAfterQuest);
        Assert.Equal(fish.Note, fish.FullNote);
        Assert.Equal("", fish.QuestNoteHint);
    }

    [Fact]
    public void Two_quests_on_one_line_are_summed()
    {
        var s = new Scenario().Party(10).Food("grain", held: 20, market: 0);
        Quest(s, "q1", "Ryibelet Needs Grain Seeds", QuestNeedKind.Items, 50, "Grain", "grain");
        Quest(s, "q2", "Army Needs Supply", QuestNeedKind.Items, 30, "Grain", "grain");
        var grain = Item(SheetView.Build(s.Plan()), "row:food:grain");
        Assert.Equal("80 needed for quests", grain.QuestNote);
        Assert.Equal("Your quests ask for:\nRyibelet Needs Grain Seeds – 50 Grain, you hold 20\nArmy Needs Supply – 30 Grain, you hold 0",
            grain.QuestNoteHint);
    }

    [Fact]
    public void A_need_you_already_hold_says_held()
    {
        var s = Grain(new Scenario().Party(10).Food("grain", held: 130, market: 200).Food("fish", held: 10, market: 200));
        var grain = Item(SheetView.Build(s.Plan()), "row:food:grain");
        Assert.Equal("120 for quest, held", grain.QuestNote);
        Assert.Equal("Your quests ask for:\nRyibelet Needs Grain Seeds – 120 Grain", grain.QuestNoteHint);
    }

    [Fact]
    public void A_line_with_a_note_of_its_own_joins_the_two()
    {
        // The pack row's own note ("keep 5 · …") follows the quest's.
        var s = new Scenario().Party(10).Pack("mule", held: 12);
        s.Settings.PackAnimalsTarget = 5;
        Quest(s, "q_draught", "Village Needs Draught Animals", QuestNeedKind.Items, 10, "Mule", "mule");
        var pack = Item(SheetView.Build(s.Plan()), "row:mounts:pack");
        Assert.Equal("10 for quest, held", pack.QuestNote);
        Assert.StartsWith("keep 5", pack.Note);
        Assert.Equal("· " + pack.Note, pack.NoteAfterQuest);
        Assert.Equal("10 for quest, held · " + pack.Note, pack.FullNote);
    }

    [Fact]
    public void A_breakdown_line_shows_the_quest_on_its_own_breed_only()
    {
        var s = new Scenario().Party(10).Mount("aserai_horse", "horse", held: 5).Mount("palfrey", "horse", held: 2, sell: 200);
        Quest(s, "q_horses", "Lord Needs Horses", QuestNeedKind.Items, 2, "Aserai Horse", "aserai_horse");
        var view = SheetView.Build(s.Plan()); // everything open
        Assert.Equal("2 for quest, held", Item(view, "row:mounts:riding").QuestNote);
        var subs = view.Sections.SelectMany(x => x.Items).Where(i => i.Kind == SheetItemKind.SubLine && i.Row?.Id == "mounts:riding").ToList();
        Assert.Equal("2 for quest, held", subs.Single(i => i.StackKey!.StartsWith("aserai_horse")).QuestNote);
        Assert.Equal("", subs.Single(i => i.StackKey!.StartsWith("palfrey")).QuestNote);
    }

    [Fact]
    public void Prisoner_and_troop_lines_carry_their_rows_quests_but_title_lines_do_not()
    {
        var s = new Scenario().Party(40).Prisoner("looter", 10, ransom: 20).Prisoner("sea_raider", 5, ransom: 50)
            .Troop("imperial_trained", inParty: 15, tier: 3).Troop("looter_t", inParty: 6, tier: 0);
        Quest(s, "q_laborers", "Landowner Needs Manual Laborers", QuestNeedKind.Prisoners, 8, "bandits", "looter", "sea_raider");
        Quest(s, "q_garrison", "Lucon Needs Garrison Troops in Lageta", QuestNeedKind.Troops, 10, "Imperial Trained Infantry",
            "imperial_trained");
        Quest(s, "q_gang", "Gang Needs Recruits", QuestNeedKind.Troops, 4, "bandits", "looter_t");
        var view = SheetView.Build(s.Plan());

        // One need served from two prisoner rows shows the quest's 8 on both — the hover says what each holds.
        Assert.Equal("8 for quest, held", Item(view, "row:prisoner:looter").QuestNote);
        Assert.Equal("8 for quest, held", Item(view, "row:prisoner:sea_raider").QuestNote);
        Assert.Equal("8 for quest, held", Item(view, "line:others").QuestNote);   // the Others line: its rows' quests, once
        Assert.All(view.Sections.SelectMany(x => x.Items).Where(i => i.Key == "line:lords"), i => Assert.Equal("", i.QuestNote));

        Assert.Equal("10 for quest, held", Item(view, "row:troops:imperial_trained@dismiss").QuestNote);
        Assert.Equal("4 for quest, held", Item(view, "row:troops:looter_t@dismiss").QuestNote);
        Assert.Equal("14 for quests, held", Item(view, "line:yours").QuestNote); // two quests summed, once each
        Assert.All(view.Sections.SelectMany(x => x.Items).Where(i => i.Key == "line:recruits"), i => Assert.Equal("", i.QuestNote));
    }

    [Fact]
    public void No_quests_no_note_and_the_words_are_the_windows()
    {
        Assert.Equal("", SheetView.QuestNote(null));
        Assert.Equal("", SheetView.QuestNoteHint(Array.Empty<QuestNeedState>()));
        var s = Grain(new Scenario().Party(10).Food("grain", held: 20, market: 200));
        var words = new SheetWords { QuestNoteNeeded = "pour la quête", QuestAskFor = "Vos quêtes :" };
        var grain = Item(SheetView.Build(s.Plan(), words), "row:food:grain");
        Assert.Equal("120 pour la quête", grain.QuestNote);
        Assert.StartsWith("Vos quêtes :\n", grain.QuestNoteHint);
    }
}
