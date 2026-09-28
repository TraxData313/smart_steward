using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Settings;

/// <summary>
/// The live settings (load, reload when the file changed, save on change, change notice) over an
/// in-memory file — the Module's FileSettingsStorage only moves the same text to and from the disk.
/// </summary>
public class SettingsServiceTests
{
    private sealed class MemoryStorage : ISettingsStorage
    {
        public string? Text;
        public string? Backup;
        public int Writes;
        public bool FailRead;
        public bool FailWrite;
        private int _version;

        public string? ReadText() => FailRead ? throw new IOException("locked by an editor") : Text;

        public void WriteText(string text)
        {
            if (FailWrite) throw new UnauthorizedAccessException("read-only");
            Text = text;
            Writes++;
            _version++;
        }

        public void WriteBackup(string text) => Backup = text;

        public string? Stamp() => Text == null ? null : _version + ":" + Text.Length;

        /// <summary>The player edits (or deletes) the file outside the game.</summary>
        public void EditOutside(string? text)
        {
            Text = text;
            _version++;
        }
    }

    private static readonly string Defaults = SettingsFile.Generate(new StewardSettings());

    private readonly MemoryStorage _storage = new();
    private readonly List<string> _log = new();
    private readonly SettingsService _service;
    private int _changes;

    public SettingsServiceTests()
    {
        _service = new SettingsService(_storage, _log.Add);
        _service.Changed += () => _changes++;
    }

    private static SettingDefinition Def(string key) => SettingsRegistry.Find(key)!;

    // ── Loading ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void No_file_yet_writes_the_defaults()
    {
        _service.Load();
        Assert.Equal(Defaults, _storage.Text);
        Assert.Null(_storage.Backup);
        Assert.Equal(Defaults, SettingsFile.Generate(_service.Current));
        Assert.Contains(_log, l => l.Contains("no settings file yet"));
        Assert.Equal(0, _changes); // the values did not change: they were the defaults all along
    }

    [Fact]
    public void A_file_already_in_shape_is_read_and_left_alone()
    {
        var text = SettingsFile.Generate(SettingsFileTests.EverythingChanged());
        _storage.EditOutside(text);
        _service.Load();

        Assert.Equal(0, _storage.Writes);
        Assert.Equal(text, SettingsFile.Generate(_service.Current));
        Assert.Empty(_log);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void An_old_file_missing_new_keys_is_completed_without_a_backup()
    {
        _storage.EditOutside("{ \"MountMaxPrice\": 650 }");
        _service.Load();

        Assert.Equal(650, _service.Current.MountMaxPrice);
        Assert.Null(_storage.Backup);
        Assert.Contains("\"MountMaxPrice\": 650,", _storage.Text);
        Assert.Contains("\"FoodDays\": 40,", _storage.Text);
        Assert.Contains(_log, l => l.Contains("had no ModEnabled"));
    }

    [Fact]
    public void Values_out_of_range_are_clamped_logged_backed_up_and_rewritten()
    {
        var original = "{ \"HorseBuyPriceMultiplier\": 50, \"Colour\": \"red\" }";
        _storage.EditOutside(original);
        _service.Load();

        Assert.Equal(10.0, _service.Current.HorseBuyPriceMultiplier);
        Assert.Equal(original, _storage.Backup);
        Assert.Contains("\"HorseBuyPriceMultiplier\": 10.0,", _storage.Text);
        Assert.DoesNotContain("Colour", _storage.Text);
        Assert.Contains(_log, l => l.Contains("HorseBuyPriceMultiplier") && l.Contains("above the maximum"));
        Assert.Contains(_log, l => l.Contains("unknown key \"Colour\""));
    }

    [Fact]
    public void An_unreadable_file_is_kept_as_the_backup_and_replaced_by_the_defaults()
    {
        _storage.EditOutside("{ \"ModEnabled\": false, oops");
        _service.Load();

        Assert.Equal("{ \"ModEnabled\": false, oops", _storage.Backup);
        Assert.Equal(Defaults, _storage.Text);
        Assert.True(_service.Current.ModEnabled);
        Assert.Contains(_log, l => l.Contains("unreadable") && l.Contains(".bak"));
    }

    [Fact]
    public void A_file_that_cannot_be_opened_is_never_written_over()
    {
        _storage.EditOutside(SettingsFile.Generate(new StewardSettings { MountMaxPrice = 700 }));
        _service.Load();
        _storage.FailRead = true;
        _storage.EditOutside("{ }");

        _service.Load();

        Assert.Equal(700, _service.Current.MountMaxPrice); // kept
        Assert.Equal("{ }", _storage.Text);                // untouched
        Assert.Null(_storage.Backup);
        Assert.Contains(_log, l => l.Contains("could not be read") && l.Contains("locked by an editor"));
    }

    [Fact]
    public void A_failed_write_is_logged_and_the_values_still_apply()
    {
        _service.Load();
        _storage.FailWrite = true;

        Assert.True(_service.Set(Def("MinGoldAfterDeal"), 2500));

        Assert.Equal(2500, _service.Current.MinGoldAfterDeal);
        Assert.Contains(_log, l => l.Contains("could not be written") && l.Contains("read-only"));
        Assert.Equal(1, _changes);
    }

    // ── Reloading when the file changed on disk ─────────────────────────────────────────────

    [Fact]
    public void Nothing_is_reloaded_while_the_file_is_unchanged()
    {
        _service.Load();
        _service.Set(Def("PackAnimalsTarget"), 14); // our own write must not count as an outside edit
        _changes = 0;

        Assert.False(_service.ReloadIfChanged());
        Assert.Equal(0, _changes);
        Assert.Equal(14, _service.Current.PackAnimalsTarget);
    }

    [Fact]
    public void An_edit_outside_the_game_is_picked_up()
    {
        _service.Load();
        var before = _service.Current;
        _storage.EditOutside(SettingsFile.Generate(new StewardSettings { SellLoot = true }));

        Assert.True(_service.ReloadIfChanged());

        Assert.True(_service.Current.SellLoot);
        Assert.NotSame(before, _service.Current); // readers always go through Current
        Assert.Equal(1, _changes);
        Assert.False(_service.ReloadIfChanged());
    }

    [Fact]
    public void Deleting_the_file_resets_everything()
    {
        _storage.EditOutside(SettingsFile.Generate(new StewardSettings { AutonomousSteward = true }));
        _service.Load();
        _changes = 0;
        _storage.EditOutside(null);

        Assert.True(_service.ReloadIfChanged());

        Assert.False(_service.Current.AutonomousSteward);
        Assert.Equal(Defaults, _storage.Text);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void An_old_AutoExecute_file_is_carried_over_once_without_a_backup()
    {
        var old = Defaults.Replace("  \"AutonomousSteward\": false,", "  \"AutoExecute\": true,");
        _storage.EditOutside(old);

        _service.Load();

        Assert.True(_service.Current.AutonomousSteward);
        Assert.Equal(SettingsFile.Generate(new StewardSettings { AutonomousSteward = true }), _storage.Text);
        Assert.Null(_storage.Backup);                 // nothing was lost
        Assert.Contains(_log, l => l.Contains("\"AutoExecute\" is now AutonomousSteward"));

        // Once: the file now has the new name, so the next load finds nothing to carry over or rewrite.
        _log.Clear();
        int writes = _storage.Writes;
        _service.Load();
        Assert.True(_service.Current.AutonomousSteward);
        Assert.Equal(writes, _storage.Writes);
        Assert.Empty(_log);
    }

    // ── Changing values ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Each_kind_of_value_is_set_saved_and_announced()
    {
        _service.Load();

        Assert.True(_service.Set(Def("SellLoot"), true));
        Assert.True(_service.Set(Def("MountMaxPrice"), 650));
        Assert.True(_service.Set(Def("HorseBuyPriceMultiplier"), 1.5f)); // MCM hands over floats
        Assert.True(_service.Set(Def("FoodStrategy"), 1));
        Assert.True(_service.Set(Def("SellLootOrder"), "mostexpensive"));

        var s = _service.Current;
        Assert.True(s.SellLoot);
        Assert.Equal(650, s.MountMaxPrice);
        Assert.Equal(1.5, s.HorseBuyPriceMultiplier);
        Assert.Equal(FoodStrategy.Cheapest, s.FoodStrategy);
        Assert.Equal(SellLootOrder.MostExpensive, s.SellLootOrder);
        Assert.Equal(5, _changes);
        Assert.Equal(SettingsFile.Generate(s), _storage.Text);
        Assert.Contains("\"SellLootOrder\": \"MostExpensive\"", _storage.Text);
    }

    [Fact]
    public void Setting_the_same_value_again_writes_nothing()
    {
        _service.Load();
        var writes = _storage.Writes;

        Assert.False(_service.Set(Def("ModEnabled"), true));
        Assert.False(_service.Set(Def("HorseBuyPriceMultiplier"), 1.2));
        Assert.False(_service.Set(Def("FoodStrategy"), FoodStrategy.Balanced));

        Assert.Equal(writes, _storage.Writes);
        Assert.Equal(0, _changes);
    }

    [Fact]
    public void Values_set_out_of_range_are_clamped()
    {
        _service.Load();
        _service.Set(Def("WarMountsToKeep"), 5000);
        _service.Set(Def("HorseSellPriceMultiplier"), -3.0);
        Assert.Equal(500, _service.Current.WarMountsToKeep);
        Assert.Equal(0.0, _service.Current.HorseSellPriceMultiplier);
    }

    [Fact]
    public void A_value_of_the_wrong_kind_is_refused_and_logged()
    {
        _service.Load();
        Assert.False(_service.Set(Def("ModEnabled"), "yes"));
        Assert.False(_service.Set(Def("MinGoldAfterDeal"), true));
        Assert.True(_service.Current.ModEnabled);
        Assert.Equal(2, _log.Count(l => l.Contains("cannot take")));
    }

    [Fact]
    public void Price_book_edits_are_normalised_saved_and_announced()
    {
        _service.Load();

        Assert.True(_service.Update(s =>
        {
            s.PriceBook["grain"] = new PriceBookEntry { BuyBase = -3, Sell = false };
            s.PriceBook["fish"] = new PriceBookEntry(); // nothing overridden
        }));

        var book = _service.Current.PriceBook;
        Assert.Equal(0, book["grain"].BuyBase);
        Assert.False(book["grain"].Sell);
        Assert.False(book.ContainsKey("fish"));
        Assert.Contains("\"grain\": { \"BuyBase\": 0, \"Sell\": false }", _storage.Text);
        Assert.Equal(1, _changes);
    }

    [Fact]
    public void A_failing_edit_restores_the_saved_values()
    {
        _service.Load();
        _service.Set(Def("PackAnimalsTarget"), 12);

        Assert.False(_service.Update(s =>
        {
            s.PackAnimalsTarget = 99;
            throw new InvalidOperationException("boom");
        }));

        Assert.Equal(12, _service.Current.PackAnimalsTarget);
        Assert.Contains(_log, l => l.Contains("an edit failed"));
    }

    [Fact]
    public void A_failing_listener_does_not_stop_the_others()
    {
        _service.Load();
        var reached = false;
        _service.Changed += () => throw new InvalidOperationException("window gone");
        _service.Changed += () => reached = true;

        _service.Set(Def("AutonomousSteward"), true);

        Assert.True(reached);
        Assert.Equal(1, _changes);
        Assert.Contains(_log, l => l.Contains("change listener failed"));
    }
}
