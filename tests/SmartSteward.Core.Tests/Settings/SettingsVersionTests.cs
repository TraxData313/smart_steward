using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Settings;

/// <summary>
/// The file's version marker and the one-time v1.0.0 → v1.0.1 migration (Anton 2026.10.14: loot selling on by default): a
/// version-1 file's <c>"SellLoot": false</c> is the old default, flipped on once; a version-2 file is the player's word.
/// </summary>
public class SettingsVersionTests
{
    private sealed class MemoryStorage : ISettingsStorage
    {
        public string? Text;
        public string? Backup;
        public string? ReadText() => Text;
        public void WriteText(string text) => Text = text;
        public void WriteBackup(string text) => Backup = text;
        public string? Stamp() => Text == null ? null : Text.Length + ":" + Text.GetHashCode();
    }

    /// <summary>A file as v1.0.0 wrote it: today's layout without the marker (its comment line and key).</summary>
    private static string V100File(StewardSettings settings)
    {
        var lines = SettingsFile.Generate(settings).Split(SettingsFile.NewLine).ToList();
        int key = lines.FindIndex(l => l.Contains("\"" + SettingsFile.VersionKey + "\""));
        Assert.True(key > 0);
        lines.RemoveRange(key - 1, 2);
        return string.Join(SettingsFile.NewLine, lines);
    }

    [Fact]
    public void Defaults_sell_loot_and_the_file_carries_the_marker()
    {
        Assert.True(new StewardSettings().SellLoot);
        var text = SettingsFile.Generate(new StewardSettings());
        Assert.Contains("  \"" + SettingsFile.VersionKey + "\": " + SettingsFile.FileVersion + ",", text);
        var parsed = SettingsFile.Parse(text);
        Assert.Equal(SettingsFile.FileVersion, parsed.FileVersion);
        Assert.Empty(parsed.Problems);
        Assert.Empty(parsed.Migrated);
        Assert.Empty(parsed.MissingKeys);
    }

    [Fact]
    public void An_old_file_with_loot_off_is_turned_on_once_and_marked()
    {
        var storage = new MemoryStorage { Text = V100File(new StewardSettings { SellLoot = false, FoodDays = 55 }) };
        var log = new List<string>();
        var service = new SettingsService(storage, log.Add);
        service.Load();

        Assert.True(service.Current.SellLoot);
        Assert.Equal(55, service.Current.FoodDays); // the rest is the player's, untouched
        Assert.Contains("\"" + SettingsFile.VersionKey + "\": 2", storage.Text);
        Assert.Contains("\"SellLoot\": true", storage.Text);
        Assert.Null(storage.Backup); // nothing lost, no backup
        Assert.Single(log, l => l.Contains("older version (1 -> 2)") && l.Contains("SellLoot was false"));
    }

    [Fact]
    public void A_marked_file_with_loot_off_stays_off()
    {
        // The player turned it off again after the migration — the rewritten file is version 2.
        var storage = new MemoryStorage { Text = SettingsFile.Generate(new StewardSettings { SellLoot = false }) };
        var log = new List<string>();
        var service = new SettingsService(storage, log.Add);
        service.Load();

        Assert.False(service.Current.SellLoot);
        Assert.DoesNotContain(log, l => l.Contains("older version"));

        // ...and again on the next load: the migration never comes back.
        service.Load();
        Assert.False(service.Current.SellLoot);
    }

    [Fact]
    public void Turning_it_off_after_the_migration_is_kept()
    {
        var storage = new MemoryStorage { Text = V100File(new StewardSettings { SellLoot = false }) };
        var service = new SettingsService(storage, _ => { });
        service.Load();
        Assert.True(service.Current.SellLoot);

        service.Set(SettingsRegistry.Find(nameof(StewardSettings.SellLoot))!, false);
        var reloaded = new SettingsService(storage, _ => { });
        reloaded.Load();
        Assert.False(reloaded.Current.SellLoot);
    }

    [Fact]
    public void No_file_gives_loot_on()
    {
        var storage = new MemoryStorage();
        var service = new SettingsService(storage, _ => { });
        service.Load();
        Assert.True(service.Current.SellLoot);
        Assert.Contains("\"SellLoot\": true", storage.Text);
        Assert.Contains("\"" + SettingsFile.VersionKey + "\": 2", storage.Text);
    }

    [Fact]
    public void An_old_file_with_loot_on_or_unwritten_is_just_marked()
    {
        var on = SettingsFile.Parse(V100File(new StewardSettings { SellLoot = true }));
        Assert.Equal(1, on.FileVersion);
        Assert.True(on.Settings.SellLoot);
        Assert.Empty(on.Migrated);

        var unwritten = SettingsFile.Parse("{ \"FoodDays\": 50 }");
        Assert.True(unwritten.Settings.SellLoot);
        Assert.Empty(unwritten.Migrated);
    }

    [Fact]
    public void A_broken_marker_is_a_problem_and_flips_nothing()
    {
        var parsed = SettingsFile.Parse("{ \"SettingsVersion\": \"two\", \"SellLoot\": false }");
        Assert.False(parsed.Settings.SellLoot);
        Assert.Empty(parsed.Migrated);
        Assert.Contains(parsed.Problems, p => p.Contains("SettingsVersion: expected a whole number"));
    }
}
