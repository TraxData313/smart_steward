using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>
/// The spreadsheet as the window shows it (PLAN step 21 — the mockup Anton approved on 2026.09.28): the lines on screen with
/// the folds applied, every cell's text and colour (zeros blank, denari green in / red out), the notes after the names, the
/// Denari tooltips and every button's live state — including "each line moves only its own side of a troop row".
/// </summary>
public class SheetViewTests
{
    private const string M = "–";

    private static SheetView Open(StewardPlan plan) => SheetView.Build(plan);

    private static SheetView Everyday(StewardPlan plan)
    {
        var state = new WindowState();
        return SheetView.Build(plan, null, state.IsFolded);
    }

    [Fact]
    public void Troops_open_shows_the_tavern_the_two_lines_and_their_rows_in_order()
    {
        var troops = Open(SuggestionSheetTests.Lycaron().Plan).Section(SheetGroup.Troops)!;
        Assert.Equal(new[]
        {
            SheetItemKind.Row, SheetItemKind.Row, SheetItemKind.Recruits, SheetItemKind.Row, SheetItemKind.Row,
            SheetItemKind.YourTroops, SheetItemKind.Row, SheetItemKind.Row, SheetItemKind.Row,
        }, troops.Items.Select(i => i.Kind));
        Assert.Equal(new[] { "row:troops:vigla@recruit", "row:troops:recruit@recruit" },
            troops.Items.Skip(3).Take(2).Select(i => i.Key));
        Assert.Equal(new[] { "row:troops:peasant@dismiss", "row:troops:recruit@dismiss", "row:troops:archer@dismiss" },
            troops.Items.Skip(6).Select(i => i.Key));
        Assert.True(troops.Items.Skip(3).Where(i => i.Kind == SheetItemKind.Row).All(i => i.Indented));
        Assert.False(troops.Items[2].Indented || troops.Items[5].Indented);
        Assert.Equal(new[] { SheetFolds.Troops }, troops.FoldKeys);         // step 33: the section's own fold
        Assert.True(troops.IsOpen);
        Assert.Equal(troops.Items.Count, troops.Items.Select(i => i.Key).Distinct().Count()); // keys are unique
    }

    [Fact]
    public void A_type_held_and_on_offer_shows_each_side_under_its_own_line()
    {
        // Lycaron: 6 Imperial Recruits held (2 wounded), 15 on offer; the Your troops line dropped one of them.
        var troops = Open(SuggestionSheetTests.Lycaron().Plan).Section(SheetGroup.Troops)!;
        var asRecruit = troops.Items.Single(i => i.Key == "row:troops:recruit@recruit");
        Assert.Equal(("T1 Imperial Recruit", M, "0", M, "15"), (asRecruit.Name, asRecruit.Mine, asRecruit.Change, asRecruit.Result, asRecruit.Market));
        Assert.Equal("17 each", asRecruit.Note);
        Assert.Equal("", asRecruit.Cells.Party);                               // its dismissal shows on the other side
        Assert.Equal(EditBlock.DismissingThisType, asRecruit.IncreaseBlock);   // Recruits never touches a row being dismissed
        Assert.Equal(EditBlock.NothingRecruited, asRecruit.DecreaseBlock);

        var asYours = troops.Items.Single(i => i.Key == "row:troops:recruit@dismiss");
        Assert.Equal(("T1 Imperial Recruit", "6", M + "1", "5", ""), (asYours.Name, asYours.Mine, asYours.Change, asYours.Result, asYours.Market));
        Assert.Equal("2 wounded - they go first", asYours.Note);
        Assert.Equal(M + "1", asYours.Cells.Party);
        Assert.Equal(UiColors.Sell, asYours.ChangeColor);
        Assert.Equal(EditBlock.None, asYours.IncreaseBlock);                   // toward zero: takes the dismissal back
        Assert.True(asYours.IsLink && asYours.CanReset);

        var archer = troops.Items.Single(i => i.Key == "row:troops:archer@dismiss");
        Assert.Equal(EditBlock.NothingDropped, archer.IncreaseBlock);          // nobody dropped: [+] has nothing to bring back
        Assert.Equal(EditBlock.None, archer.DecreaseBlock);
    }

    [Fact]
    public void The_two_troop_lines_carry_the_bulk_buttons_blocks()
    {
        var (_, plan) = SuggestionSheetTests.Lycaron();
        var troops = Open(plan).Section(SheetGroup.Troops)!;
        var recruits = troops.Items.Single(i => i.Kind == SheetItemKind.Recruits);
        Assert.Equal(("Recruits", M, "0", M, "18"), (recruits.Name, recruits.Mine, recruits.Change, recruits.Result, recruits.Market));
        Assert.Equal(plan.RecruitBestBlock, recruits.IncreaseBlock);
        Assert.Equal(EditBlock.NothingRecruited, recruits.DecreaseBlock);
        Assert.Equal(SheetFolds.Recruits, recruits.FoldKey);

        var yours = troops.Items.Single(i => i.Kind == SheetItemKind.YourTroops);
        Assert.Equal(("Your troops", "9", M + "3", "6"), (yours.Name, yours.Mine, yours.Change, yours.Result));
        Assert.Equal("dropping 2 T0 Empire Peasant, 1 T1 Imperial Recruit", yours.Note);
        Assert.Equal(EditBlock.None, yours.IncreaseBlock);                     // ReAddDropped: three to bring back
        Assert.Equal(EditBlock.None, yours.DecreaseBlock);
        Assert.True(yours.CanReset);
        Assert.Equal(M + "3", yours.Cells.Party);
    }

    [Fact]
    public void The_everyday_view_folds_the_long_lists_and_keeps_the_lines()
    {
        var view = Everyday(SuggestionSheetTests.Lycaron().Plan);
        var troops = view.Section(SheetGroup.Troops)!;
        Assert.Equal(new[] { SheetItemKind.Row, SheetItemKind.Row, SheetItemKind.Recruits, SheetItemKind.YourTroops },
            troops.Items.Select(i => i.Kind));
        Assert.True(troops.IsOpen);                                            // step 33: the section open, its lines' rows folded
        Assert.False(troops.Items[2].IsOpen);

        var food = view.Section(SheetGroup.Food)!;
        Assert.Empty(food.Items);                                              // the title line alone
        Assert.False(food.IsOpen);
        Assert.Equal("+" + "12", food.Cells.Land);                                // …which still carries the subtotal

        var prisoners = view.Section(SheetGroup.Prisoners)!;
        Assert.Equal(new[] { SheetItemKind.Lords, SheetItemKind.OtherPrisoners }, prisoners.Items.Select(i => i.Kind));

        var other = view.Section(SheetGroup.Other)!;
        Assert.True(other.IsOpen);
        Assert.Equal(new[] { "Armour", "Melee weapons", "Ranged", "Shields", "Other goods" }, other.Items.Select(i => i.Name));
        Assert.Equal(SheetFolds.OtherGoods, other.Items.Last().FoldKey);
        Assert.False(other.Items.Last().IsOpen);                               // its ▸ closed
    }

    [Fact]
    public void Troops_folded_is_its_title_line_alone_and_its_lines_folds_are_kept_underneath()
    {
        // Step 33 (Anton 2026.10.02: "That Troops dropdown never folds up into one line, it folds and unfolds the sub lines").
        var plan = SuggestionSheetTests.Lycaron().Plan;
        var state = new WindowState();
        state.SetFolded(SheetFolds.YourTroops, false);                         // Your troops' rows open, Recruits' folded
        state.SetFolded(SheetFolds.Troops, true);

        var troops = SheetView.Build(plan, null, state.IsFolded).Section(SheetGroup.Troops)!;
        Assert.Empty(troops.Items);                                            // the title line alone - no tavern, no lines
        Assert.False(troops.IsOpen);
        Assert.Equal(new[] { SheetFolds.Troops }, troops.FoldKeys);            // a click opens the section, nothing else
        Assert.Equal(PlanPart.Troops, troops.Part);                            // Deal group still works on the folded title
        Assert.NotNull(troops.Deal);
        Assert.Equal(plan.DealOf(PlanPart.Troops).CanRun, troops.Deal!.CanRun);
        Assert.Equal((M + "2,040", "+1"), (troops.Cells.Denari, troops.Cells.Party)); // the subtotal stays

        state.SetFolded(SheetFolds.Troops, false);                             // opened again: the lines as they were
        troops = SheetView.Build(plan, null, state.IsFolded).Section(SheetGroup.Troops)!;
        Assert.True(troops.IsOpen);
        Assert.Equal(new[]
        {
            SheetItemKind.Row, SheetItemKind.Row, SheetItemKind.Recruits, SheetItemKind.YourTroops,
            SheetItemKind.Row, SheetItemKind.Row, SheetItemKind.Row,
        }, troops.Items.Select(i => i.Kind));
        Assert.False(troops.Items[2].IsOpen);
        Assert.True(troops.Items[3].IsOpen);
    }

    [Fact]
    public void Zeros_are_blank_and_denari_are_green_in_red_out()
    {
        var view = Open(SuggestionSheetTests.Lycaron().Plan);
        var troops = view.Section(SheetGroup.Troops)!;
        var wanderer = troops.Items[0];
        Assert.Equal(("", "", "", ""), (wanderer.Cells.Denari, wanderer.Cells.Party, wanderer.Cells.Land, wanderer.Cells.Influence));
        Assert.Equal("444 to hire", wanderer.Note);
        Assert.Equal(M, wanderer.Market);

        var pikes = troops.Items[1];
        Assert.Equal((M + "2,040", UiColors.Sell, "+4"), (pikes.Cells.Denari, pikes.Cells.DenariColor, pikes.Cells.Party));
        Assert.Equal("mercenaries · 510 each", pikes.Note);
        Assert.Equal("4 × 510 = " + M + "2,040", pikes.DenariHint);
        Assert.Equal(UiColors.Buy, pikes.ChangeColor);

        var lords = view.Section(SheetGroup.Prisoners)!.Items.Single(i => i.Kind == SheetItemKind.Lords);
        Assert.Equal(("+14,868", UiColors.Buy, M + "2", ""), (lords.Cells.Denari, lords.Cells.DenariColor, lords.Cells.Prisoners, lords.Cells.Party));
        Assert.Equal((M + "2,040", "+1"), (troops.Cells.Denari, troops.Cells.Party)); // the title line is the subtotal
    }

    [Fact]
    public void Prisoner_lines_carry_their_toggle_and_the_rows_their_tiers()
    {
        var view = Open(SuggestionSheetTests.Lycaron().Plan);
        var prisoners = view.Section(SheetGroup.Prisoners)!;
        var lords = prisoners.Items.Single(i => i.Kind == SheetItemKind.Lords);
        Assert.Equal((PrisonerChoice.Ransom, true, false), (lords.Choice, lords.DonateAllowed, lords.HasSpinner));
        var others = prisoners.Items.Single(i => i.Kind == SheetItemKind.OtherPrisoners);
        Assert.Equal(PrisonerChoice.Donate, others.Choice);
        Assert.EndsWith("influence", others.Cells.Influence);
        Assert.Equal(M + "50", others.Cells.Prisoners);

        var rows = prisoners.Items.Where(i => i.Kind == SheetItemKind.Row).ToList();
        Assert.Equal("T1 looter", rows[0].Name);
        Assert.True(rows[0].IsLink);
        Assert.Equal("tolun", rows[^1].Name);                                  // a lord by name, last
        Assert.Equal("lord · ransom 8,210", rows[^1].Note);
        Assert.Contains(rows, r => r.Note.EndsWith("to the dungeon"));         // the dungeon's rows say so
        Assert.Contains(rows, r => r.Note.StartsWith("ransom ") && r.Note.EndsWith(" each"));
        Assert.EndsWith("influence", prisoners.Cells.Influence);
    }

    [Fact]
    public void Donate_greys_where_the_game_forbids_it_and_the_lines_keep_their_choice()
    {
        var (s, _) = SuggestionSheetTests.Lycaron();
        s.Snap.Prison!.DonateAllowed = false;
        var prisoners = Open(s.Plan()).Section(SheetGroup.Prisoners)!;
        var others = prisoners.Items.Single(i => i.Kind == SheetItemKind.OtherPrisoners);
        Assert.False(others.DonateAllowed);
        Assert.Equal(PrisonerChoice.Donate, others.Choice);                  // the order stands; here the others are ransomed
        Assert.Equal("50 ransomed", others.Note);
    }

    [Fact]
    public void A_bought_food_says_its_price_and_the_tooltip_its_limit()
    {
        var food = Open(SuggestionSheetTests.Lycaron().Plan).Section(SheetGroup.Food)!;
        var olives = food.Items.Single(i => i.Key == "row:food:olives");
        Assert.EndsWith(" each", olives.Note);
        Assert.StartsWith("8 × ", olives.DenariHint);
        Assert.Contains("your max ", olives.DenariHint);
        Assert.Equal("+8", olives.Cells.Land);
        var grain = food.Items.Single(i => i.Key == "row:food:grain");
        Assert.Equal(("", "", "0"), (grain.Note, grain.Cells.Denari, grain.Change)); // untouched: never priced, blank cells
        Assert.Equal(UiColors.Muted, grain.ChangeColor);
    }

    [Fact]
    public void A_horse_role_row_opens_to_its_kinds_with_their_own_numbers()
    {
        var s = new Scenario().Party(20, footmen: 20).Gold(50_000)
            .Mount("palfrey", "horse", held: 2, market: 20, buy: 210).Mount("steppe", "horse", market: 10, buy: 230);
        foreach (var stack in s.Snap.Market.Concat(s.Snap.Inventory)) stack.UnitWeightAtSea = 50;
        var plan = s.Plan();
        var closed = SheetView.Build(plan, null, key => key == SheetFolds.RidingHorses).Section(SheetGroup.Horses)!;
        var riding = closed.Items.Single(i => i.Key == "row:mounts:riding");
        Assert.Equal(("Riding horses", SheetFolds.RidingHorses, false), (riding.Name, riding.FoldKey, riding.IsOpen));

        var open = SheetView.Build(plan).Section(SheetGroup.Horses)!;
        var subs = open.Items.Where(i => i.Kind == SheetItemKind.SubLine && i.Row!.Id == "mounts:riding").ToList();
        var ridingOpen = open.Items.Single(i => i.Key == "row:mounts:riding");
        Assert.NotEmpty(subs);
        Assert.All(subs, sub => Assert.True(sub.Indented && !sub.HasSpinner));
        int bought = plan.Row("mounts:riding").Change;
        Assert.True(bought > 0);
        Assert.Equal(SheetCellTexts.Kg(bought * 50), ridingOpen.Cells.Sea);
        Assert.Equal(bought, subs.Sum(sub => int.Parse(sub.Change.Replace("+", "").Replace(M, "-"))));
        Assert.Equal("", ridingOpen.Cells.Land);                            // animals weigh nothing on land
    }

    [Fact]
    public void The_total_and_the_header_read_like_the_mockup()
    {
        var (_, plan) = SuggestionSheetTests.Lycaron();
        var view = Open(plan);
        var t = plan.Totals;
        Assert.Equal(UiFormat.SignedMoney(t.GoldChange), view.Total.Denari);
        Assert.Equal(UiColors.Buy, view.Total.DenariColor);
        Assert.Equal("+1", view.Total.Party);
        Assert.Equal(M + "52", view.Total.Prisoners);
        Assert.StartsWith("69,358 » ", view.Sheet.DenariFlowText);
        Assert.Equal("(" + UiFormat.SignedMoney(t.GoldChange) + ")", view.Sheet.DenariChangeText);
        Assert.Equal(t.GoldChange, view.Sheet.DenariChange);
    }

    [Fact]
    public void Nothing_moves_the_header_shows_the_purse_alone()
    {
        var sheet = SuggestionSheet.Of(new Scenario().Party(10).Gold(5_000).Food("grain", held: 20).Plan());
        Assert.Equal(("5,000", "", 0), (sheet.DenariFlowText, sheet.DenariChangeText, sheet.DenariChange));
    }

    [Fact]
    public void A_waiting_job_says_so_on_its_row()
    {
        var s = new Scenario().Party(10).Gold(1_000).Food("grain", held: 5, market: 50);
        s.Settings.FoodMinDenari = 2_000;
        var grain = Open(s.Plan()).Section(SheetGroup.Food)!.Items.Single();
        Assert.Equal("starts at 2,000 denari", grain.Note);
    }
}
