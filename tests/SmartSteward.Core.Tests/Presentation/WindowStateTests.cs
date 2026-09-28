using SmartSteward.Core.Presentation;

namespace SmartSteward.Core.Tests.Presentation;

/// <summary>
/// The window's remembered folds (PLAN step 18 — Anton 2026.09.28: "the state is REMEMBERED per section across windows,
/// towns and game restarts … kept in our settings folder, never in the save"): window_state.json's text in and out.
/// Unknown or broken → every section unfolded, never an exception.
/// </summary>
public class WindowStateTests
{
    [Fact]
    public void No_file_means_every_section_unfolded()
    {
        foreach (var text in new[] { null, "", "   \r\n" })
        {
            var state = WindowState.Parse(text, out var problem);
            Assert.Null(problem);
            Assert.Empty(state.Collapsed);
            Assert.All(SectionGroups.All, g => Assert.False(state.IsCollapsed(g)));
        }
    }

    [Fact]
    public void A_fold_survives_the_round_trip_in_the_tables_order()
    {
        var state = new WindowState();
        Assert.True(state.SetCollapsed(SectionGroup.Prisoners, true));
        Assert.True(state.SetCollapsed(SectionGroup.Food, true));
        Assert.True(state.SetCollapsed(SectionGroup.Troops, true));
        Assert.False(state.SetCollapsed(SectionGroup.Food, true));       // already folded: nothing to write
        Assert.True(state.SetCollapsed(SectionGroup.Troops, false));
        Assert.False(state.SetCollapsed(SectionGroup.Mounts, false));

        string text = state.Generate();
        Assert.Contains("\"CollapsedSections\": [\"Food\", \"Prisoners\"]", text);

        var back = WindowState.Parse(text, out var problem);
        Assert.Null(problem);
        Assert.Equal(new[] { SectionGroup.Food, SectionGroup.Prisoners }, back.Collapsed);
        Assert.Equal(text, back.Generate());                            // deterministic
    }

    [Fact]
    public void The_file_is_plain_ascii_with_comments_and_crlf()
    {
        var state = new WindowState();
        state.SetCollapsed(SectionGroup.ArmourAndWeapons, true);
        string text = state.Generate();
        Assert.All(text, c => Assert.True(c < 128, "non-ASCII character in window_state.json"));
        Assert.StartsWith("// Smart Steward", text);
        Assert.DoesNotContain("\n", text.Replace("\r\n", ""));
        Assert.Contains("Never stored in a save", text);
        // Every name the player may write is listed in the file itself.
        foreach (var g in SectionGroups.All)
            Assert.Contains(SectionGroups.Key(g), text);
    }

    [Fact]
    public void Names_read_case_insensitively_and_unknown_ones_are_dropped_and_named()
    {
        var state = WindowState.Parse("{ \"collapsedsections\": [\"food\", \"Horses\", \"TAVERN\", 7] }", out var problem);
        Assert.Equal(new[] { SectionGroup.Tavern, SectionGroup.Food }, state.Collapsed);
        Assert.NotNull(problem);
        Assert.Contains("Horses", problem);
        Assert.Contains("7", problem);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{ \"CollapsedSections\": [\"Food\" ")]
    [InlineData("[\"Food\"]")]
    [InlineData("{ \"CollapsedSections\": \"Food\" }")]
    public void A_broken_file_unfolds_everything_and_says_why(string text)
    {
        var state = WindowState.Parse(text, out var problem);
        Assert.Empty(state.Collapsed);
        Assert.NotNull(problem);
    }

    [Fact]
    public void Comments_and_other_keys_are_fine()
    {
        var state = WindowState.Parse("// hello\r\n{\r\n  /* mine */ \"Other\": 3,\r\n  \"CollapsedSections\": [\"Mounts\",],\r\n}\r\n",
            out var problem);
        Assert.Null(problem);
        Assert.Equal(new[] { SectionGroup.Mounts }, state.Collapsed);
    }
}
