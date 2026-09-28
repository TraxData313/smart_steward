using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Settings;

/// <summary>
/// Round 5 (DESIGN §1.1 "THE GOAL", §7 "Goals you set by hand", §8): the standing goals in settings.json — written with
/// comments, read tolerantly — the three switches, the "weight not kg" rename, and the quiet save of the Suggestion tab.
/// </summary>
public class GoalsSettingsTests
{
    private static string Defaults => SettingsFile.Generate(new StewardSettings());

    private static string WithGoals(string goalsJson) =>
        Defaults.Replace("  \"Goals\": {}", "  \"Goals\": " + goalsJson);

    [Fact]
    public void The_switches_default_as_Anton_asked()
    {
        var s = new StewardSettings();
        Assert.False(s.ManualGoalsWaitForThresholds); // your goal is your order
        Assert.True(s.ManualGoalsKeepPurseFloor);
        Assert.True(s.ManualGoalsObeyPriceCaps);
        Assert.Empty(s.Goals);
        Assert.Equal("Goals you set by hand", SettingsRegistry.GroupLabel(SettingsRegistry.Goals));
        Assert.Equal(new[] { "ManualGoalsWaitForThresholds", "ManualGoalsKeepPurseFloor", "ManualGoalsObeyPriceCaps", "Goals" },
            SettingsRegistry.InGroup(SettingsRegistry.Goals).Select(d => d.Key));
    }

    [Fact]
    public void Goals_are_written_sorted_under_their_comment_and_read_back()
    {
        var s = new StewardSettings();
        s.Goals["mounts:war"] = 15;
        s.Goals["food:grain"] = 60;
        s.Goals["mounts:pack"] = 0; // a goal of 0 is a goal (sell them all), not "none"
        var text = SettingsFile.Generate(s);
        Assert.Contains("  // Goals: The goals you set in the Suggestion tab", text);
        Assert.Contains("  \"Goals\": {\r\n    \"food:grain\": 60,\r\n    \"mounts:pack\": 0,\r\n    \"mounts:war\": 15\r\n  }", text);

        var parsed = SettingsFile.Parse(text);
        Assert.Empty(parsed.Problems);
        Assert.Equal(3, parsed.Settings.Goals.Count);
        Assert.Equal(60, parsed.Settings.Goals["food:grain"]);
        Assert.Equal(0, parsed.Settings.Goals["mounts:pack"]);
        Assert.Equal(text, SettingsFile.Generate(parsed.Settings));
    }

    [Fact]
    public void Junk_in_the_goals_is_dropped_with_a_line_each_and_the_rest_kept()
    {
        var parsed = SettingsFile.Parse(WithGoals(
            "{ \"food:grain\": 40, \"mounts:noble\": 3, \"grain\": 5, \"food:fish\": \"lots\", \"food:meat\": 12.5, "
            + "\"mounts:riding\": 250000, \"food:beer\": -4, \"food:olives\": null, \"mounts:war\": 7.0 }"));
        Assert.False(parsed.Unreadable);
        var goals = parsed.Settings.Goals;
        Assert.Equal(40, goals["food:grain"]);
        Assert.Equal(ManualGoals.MaxGoal, goals["mounts:riding"]); // clamped
        Assert.Equal(0, goals["food:beer"]); // clamped
        Assert.Equal(7, goals["mounts:war"]); // a whole float is a whole number
        Assert.False(goals.ContainsKey("mounts:noble"));
        Assert.False(goals.ContainsKey("grain"));
        Assert.False(goals.ContainsKey("food:fish"));
        Assert.False(goals.ContainsKey("food:meat"));
        Assert.False(goals.ContainsKey("food:olives")); // null = no goal, no problem
        Assert.Equal(6, parsed.Problems.Count);
        Assert.Contains(parsed.Problems, p => p.Contains("Goals[\"mounts:noble\"]: not a row that takes a goal"));
        Assert.Contains(parsed.Problems, p => p.Contains("Goals[\"food:fish\"]: expected a whole number"));
        Assert.Contains(parsed.Problems, p => p.Contains("Goals[\"mounts:riding\"]: 250000 is outside 0 to 100000 - using 100000"));
        Assert.True(parsed.LosesSomething); // the service keeps a .bak
    }

    [Fact]
    public void Goals_that_are_not_an_object_fall_back_to_none()
    {
        var parsed = SettingsFile.Parse(WithGoals("[ 1, 2 ]"));
        Assert.Empty(parsed.Settings.Goals);
        Assert.Contains(parsed.Problems, p => p.Contains("Goals: expected { row id: goal, ... }"));
        Assert.Empty(SettingsFile.Parse(WithGoals("null")).Problems);
    }

    [Fact]
    public void An_old_file_without_the_goals_gets_them_empty_and_the_switches_at_their_defaults()
    {
        var lines = Defaults.Split("\r\n").Where(l => !l.Contains("\"ManualGoals") && !l.StartsWith("  \"Goals\"")).ToArray();
        var parsed = SettingsFile.Parse(string.Join("\r\n", lines));
        Assert.Empty(parsed.Problems);
        Assert.Contains("Goals", parsed.MissingKeys);
        Assert.Contains("ManualGoalsKeepPurseFloor", parsed.MissingKeys);
        Assert.True(parsed.Settings.ManualGoalsKeepPurseFloor);
        Assert.Empty(parsed.Settings.Goals);
    }

    [Fact]
    public void Only_food_and_the_pack_riding_and_war_rows_take_a_goal()
    {
        Assert.True(ManualGoals.IsGoalKey("food:grain"));
        Assert.True(ManualGoals.IsGoalKey("mounts:pack"));
        Assert.True(ManualGoals.IsGoalKey("mounts:riding"));
        Assert.True(ManualGoals.IsGoalKey("mounts:war"));
        foreach (var key in new[] { "food:", "mounts:noble", "mounts:lame", "loot:Armour", "prisoner:looter", "grain", "", " food:x" })
            Assert.False(ManualGoals.IsGoalKey(key), key);

        var s = new StewardSettings();
        Assert.True(ManualGoals.Set(s, "food:grain", 250_000));
        Assert.Equal(ManualGoals.MaxGoal, ManualGoals.Get(s, "food:grain"));
        Assert.False(ManualGoals.Set(s, "mounts:noble", 3));
        Assert.True(ManualGoals.Set(s, "food:grain", null));
        Assert.Null(ManualGoals.Get(s, "food:grain"));
    }

    [Fact]
    public void The_log_line_names_the_goals()
    {
        var s = new StewardSettings { ManualGoalsKeepPurseFloor = false };
        s.Goals["mounts:war"] = 15;
        s.Goals["food:grain"] = 60;
        Assert.Equal("ManualGoalsKeepPurseFloor=false, Goals: food:grain=60, mounts:war=15", SettingsRegistry.DescribeNonDefaults(s));
    }

    [Fact]
    public void The_old_lowest_price_per_kg_is_read_as_per_weight_and_no_kg_is_left_in_the_texts()
    {
        var parsed = SettingsFile.Parse(Defaults.Replace("\"SellLootOrder\": \"Cheapest\"", "\"SellLootOrder\": \"LowestPricePerKg\""));
        Assert.Empty(parsed.Problems);
        Assert.Equal(SellLootOrder.LowestPricePerWeight, parsed.Settings.SellLootOrder);
        Assert.Contains("\"SellLootOrder\": \"LowestPricePerWeight\"", SettingsFile.Generate(parsed.Settings));
        // "weight, not kg" (Anton 2026.09.28): the game writes weight with no unit.
        Assert.DoesNotContain("kg", Defaults, StringComparison.OrdinalIgnoreCase);
        foreach (var def in SettingsRegistry.All)
            Assert.DoesNotContain("kg", def.Label + def.Help + string.Join(" ", (def as EnumSetting)?.Labels ?? Array.Empty<string>()),
                StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_quiet_save_writes_the_file_but_announces_nothing()
    {
        var storage = new MemoryStorage();
        var log = new List<string>();
        var service = new SettingsService(storage, log.Add);
        service.Load();
        int changed = 0;
        service.Changed += () => changed++;

        Assert.True(service.SaveQuietly(s => ManualGoals.Set(s, "food:grain", 60)));
        Assert.Equal(0, changed);
        Assert.Equal(60, service.Current.Goals["food:grain"]);
        Assert.Contains("\"food:grain\": 60", storage.Text);
        Assert.False(service.ReloadIfChanged()); // our own write is no change on disk

        Assert.False(service.SaveQuietly(s => ManualGoals.Set(s, "food:grain", 60))); // the same value: nothing written
        Assert.True(service.SaveQuietly(s => new GoalEdit("food:grain", null).ApplyTo(s)));
        Assert.Empty(service.Current.Goals);
        Assert.Equal(0, changed);

        // A goal typed by hand into the file reaches the service at the next reload, announced like any file edit.
        storage.Text = storage.Text!.Replace("\"Goals\": {}", "\"Goals\": { \"mounts:war\": 12 }");
        storage.Touch();
        Assert.True(service.ReloadIfChanged());
        Assert.Equal(12, service.Current.Goals["mounts:war"]);
        Assert.Equal(1, changed);
    }

    private sealed class MemoryStorage : ISettingsStorage
    {
        private int _version;
        public string? Text { get; set; }

        public string? ReadText() => Text;

        public void WriteText(string text)
        {
            Text = text;
            _version++;
        }

        public void WriteBackup(string text)
        {
        }

        public string? Stamp() => Text == null ? null : _version + ":" + Text.Length;

        public void Touch() => _version++;
    }
}
