using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Snapshot;
using SmartSteward.Core.Tests.Planning;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>
/// The window's remembered folds (PLAN step 18 — Anton 2026.09.28: "the state is REMEMBERED per section across windows,
/// towns and game restarts … kept in our settings folder, never in the save"; step 21: every fold of the spreadsheet):
/// window_state.json's text in and out. No file, unknown or broken → the approved mockup's everyday view, never an
/// exception; a step-18/20 file is read once in its own terms.
/// </summary>
public class WindowStateTests
{
    private static readonly string[] Everyday =
    {
        SheetFolds.Recruits, SheetFolds.YourTroops, SheetFolds.Food, SheetFolds.PackAnimals, SheetFolds.RidingHorses,
        SheetFolds.WarHorses, SheetFolds.NobleHorses, SheetFolds.LameHorses, SheetFolds.Prisoners, SheetFolds.OtherGoods,
    };

    [Fact]
    public void No_file_means_the_everyday_view_of_the_mockup()
    {
        foreach (var text in new[] { null, "", "   \r\n", "{ }" })
        {
            var state = WindowState.Parse(text, out var problem);
            Assert.Null(problem);
            Assert.Equal(Everyday, state.Folded);
            Assert.False(state.IsFolded(SheetFolds.Horses));   // Horses and Other open, as drawn
            Assert.False(state.IsFolded(SheetFolds.Troops));   // Troops open: its lines show, their rows folded
            Assert.False(state.IsFolded(SheetFolds.Other));
        }
    }

    [Fact]
    public void A_fold_survives_the_round_trip_in_the_tables_order()
    {
        var state = new WindowState();
        Assert.True(state.SetFolded(SheetFolds.Food, false));
        Assert.True(state.SetFolded(SheetFolds.Other, true));
        Assert.False(state.SetFolded(SheetFolds.Other, true));        // already folded: nothing to write
        Assert.False(state.SetFolded("Nonsense", true));               // unknown: refused
        foreach (var key in new[] { SheetFolds.PackAnimals, SheetFolds.RidingHorses, SheetFolds.WarHorses, SheetFolds.NobleHorses,
                     SheetFolds.LameHorses, SheetFolds.OtherGoods, SheetFolds.Prisoners })
            state.SetFolded(key, false);

        string text = state.Generate();
        Assert.Contains("\"Folded\": [\"Recruits\", \"YourTroops\", \"Other\"]", text);

        var back = WindowState.Parse(text, out var problem);
        Assert.Null(problem);
        Assert.Equal(new[] { SheetFolds.Recruits, SheetFolds.YourTroops, SheetFolds.Other }, back.Folded);
        Assert.Equal(text, back.Generate());                            // deterministic
    }

    [Fact]
    public void An_empty_list_means_everything_open()
    {
        var state = WindowState.Parse("{ \"Folded\": [] }", out var problem);
        Assert.Null(problem);
        Assert.Empty(state.Folded);
    }

    [Fact]
    public void The_file_is_plain_ascii_with_comments_and_crlf()
    {
        var state = new WindowState();
        state.SetFolded(SheetFolds.Other, true);
        string text = state.Generate();
        Assert.All(text, c => Assert.True(c < 128, "non-ASCII character in window_state.json"));
        Assert.StartsWith("// Smart Steward", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
        Assert.Contains("Never stored in a save", text);
        foreach (var key in SheetFolds.All)                              // every name the player may write is listed
            Assert.Contains(key, text);
    }

    [Fact]
    public void Names_read_case_insensitively_and_unknown_ones_are_dropped_and_named()
    {
        var state = WindowState.Parse("{ \"folded\": [\"food\", \"Mounts\", \"OTHERGOODS\", 7] }", out var problem);
        Assert.Equal(new[] { SheetFolds.Food, SheetFolds.OtherGoods }, state.Folded);
        Assert.NotNull(problem);
        Assert.Contains("Mounts", problem);
        Assert.Contains("7", problem);
    }

    [Fact]
    public void A_step_18_file_keeps_its_sections_as_they_were()
    {
        // Step 18/20: "Troops" folded both halves, "Mounts" was the horses, "Tavern" folds nothing now; the sections it does
        // not name were open. The ▸ breakdowns it never knew stay at the everyday view (closed).
        var state = WindowState.Parse("{ \"CollapsedSections\": [\"Tavern\", \"Troops\", \"Mounts\", \"ArmourAndWeapons\"] }",
            out var problem);
        Assert.Null(problem);
        Assert.True(state.IsFolded(SheetFolds.Recruits) && state.IsFolded(SheetFolds.YourTroops));
        Assert.True(state.IsFolded(SheetFolds.Troops));                  // step 33: the section to its one line, as then
        Assert.True(state.IsFolded(SheetFolds.Horses) && state.IsFolded(SheetFolds.Other));
        Assert.False(state.IsFolded(SheetFolds.Food));
        Assert.False(state.IsFolded(SheetFolds.Prisoners));
        Assert.True(state.IsFolded(SheetFolds.RidingHorses) && state.IsFolded(SheetFolds.OtherGoods));
        Assert.Contains("\"Folded\"", state.Generate());               // the next write uses the new key only
        Assert.DoesNotContain("CollapsedSections", state.Generate());

        var empty = WindowState.Parse("{ \"CollapsedSections\": [] }", out problem);
        Assert.Null(problem);
        Assert.False(empty.IsFolded(SheetFolds.Food) || empty.IsFolded(SheetFolds.Recruits) || empty.IsFolded(SheetFolds.Troops));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ \"Folded\": [\"Food\" ")]
    [InlineData("[\"Food\"]")]
    [InlineData("{ \"Folded\": \"Food\" }")]
    [InlineData("{ \"CollapsedSections\": \"Food\" }")]
    public void A_broken_file_gives_the_everyday_view_and_says_why(string text)
    {
        var state = WindowState.Parse(text, out var problem);
        Assert.Equal(Everyday, state.Folded);
        Assert.NotNull(problem);
    }

    [Fact]
    public void Comments_and_other_keys_are_fine()
    {
        var state = WindowState.Parse("// hello\r\n{\r\n  /* mine */ \"Other\": 3,\r\n  \"Folded\": [\"Horses\",],\r\n}\r\n",
            out var problem);
        Assert.Null(problem);
        Assert.Equal(new[] { SheetFolds.Horses }, state.Folded);
    }

    [Fact]
    public void The_horse_role_rows_and_the_other_goods_row_have_their_own_folds()
    {
        var s = new Scenario().Party(20, footmen: 20).Gold(50_000).Mount("hunter", "horse", held: 3, market: 10, buy: 200)
            .Pack("mule", held: 2, market: 5, buy: 100).Goods("wool", held: 4, sell: 22)
            .Loot("mail", LootGroup.Armour, held: 2, sell: 300);
        s.Settings.SellLoot = true;
        var plan = s.Plan();
        Assert.Equal(SheetFolds.RidingHorses, SheetFolds.OfRow(plan.Row("mounts:riding")));
        Assert.Equal(SheetFolds.PackAnimals, SheetFolds.OfRow(plan.Row("mounts:pack")));
        Assert.Equal(SheetFolds.OtherGoods, SheetFolds.OfRow(plan.Row("loot:OtherGoods")));
        Assert.Null(SheetFolds.OfRow(plan.Row("loot:Armour")));
        Assert.Equal(SheetFolds.Troops, SheetFolds.OfSection(SheetGroup.Troops));   // step 33
        Assert.Equal(SheetFolds.Horses, SheetFolds.OfSection(SheetGroup.Horses));
    }
}
