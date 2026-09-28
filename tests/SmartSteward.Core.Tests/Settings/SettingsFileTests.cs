using Newtonsoft.Json.Linq;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests.Settings;

/// <summary>
/// The commented settings file (DESIGN §8): what is written, and how forgiving the reading is — a
/// hand-edited file never breaks the game, it only falls back, clamps and says so.
/// </summary>
public class SettingsFileTests
{
    private static readonly string Defaults = SettingsFile.Generate(new StewardSettings());

    /// <summary>A settings object with EVERY value away from its default, plus a price book and a prisoner list.</summary>
    internal static StewardSettings EverythingChanged()
    {
        var s = new StewardSettings();
        foreach (var def in SettingsRegistry.All.Where(d => d.IsScalar))
        {
            var value = SettingsRegistryTests.OtherValue(def);
            switch (def)
            {
                case BoolSetting b: b.Set(s, (bool)value); break;
                case IntSetting n: n.Set(s, (int)value); break;
                case FloatSetting f: f.Set(s, (double)value); break;
                case EnumSetting e: e.SetIndex(s, (int)value); break;
            }
        }
        s.PriceBook["grain"] = new PriceBookEntry { BuyBase = 12, Sell = false };
        s.PriceBook["t2_battania_horse"] = new PriceBookEntry { Buy = true, BuyBase = 800, Sell = true, SellBase = 400 };
        s.PriceBook["mule"] = new PriceBookEntry { Buy = false };
        s.PriceBook["odd \"id\" \\ with quotes"] = new PriceBookEntry { SellBase = 0 };
        return s;
    }

    /// <summary>The defaults' file with one key's value line replaced by <paramref name="valueJson"/>.</summary>
    private static string WithValue(string key, string valueJson)
    {
        var lines = Defaults.Split(SettingsFile.NewLine);
        var index = Array.FindIndex(lines, l => l.StartsWith("  \"" + key + "\": ", StringComparison.Ordinal));
        Assert.True(index >= 0, key + " not in the file");
        var comma = lines[index].EndsWith(",", StringComparison.Ordinal) ? "," : "";
        lines[index] = "  \"" + key + "\": " + valueJson + comma;
        return string.Join(SettingsFile.NewLine, lines);
    }

    private static SettingsParseResult ParseOne(string key, string valueJson) => SettingsFile.Parse(WithValue(key, valueJson));

    // ── Writing ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_defaults_file_reads_back_as_the_defaults_with_nothing_to_report()
    {
        var parsed = SettingsFile.Parse(Defaults);
        Assert.False(parsed.Unreadable);
        Assert.Empty(parsed.Problems);
        Assert.Empty(parsed.MissingKeys);
        Assert.Equal(Defaults, SettingsFile.Generate(parsed.Settings));
    }

    [Fact]
    public void Every_value_survives_a_round_trip()
    {
        var changed = EverythingChanged();
        var text = SettingsFile.Generate(changed);
        var parsed = SettingsFile.Parse(text);

        Assert.False(parsed.Unreadable);
        Assert.Empty(parsed.Problems);
        foreach (var def in SettingsRegistry.All.Where(d => d.IsScalar))
        {
            Assert.Equal(def.GetValue(changed), def.GetValue(parsed.Settings));
            Assert.NotEqual(def.DefaultValue, def.GetValue(parsed.Settings));
        }
        Assert.Equal(text, SettingsFile.Generate(parsed.Settings));
    }

    [Fact]
    public void The_price_book_survives_a_round_trip()
    {
        var parsed = SettingsFile.Parse(SettingsFile.Generate(EverythingChanged())).Settings.PriceBook;
        Assert.Equal(4, parsed.Count);
        Assert.Equal((null, 12, false, null), Fields(parsed["grain"]));
        Assert.Equal((true, 800, true, 400), Fields(parsed["t2_battania_horse"]));
        Assert.Equal((false, null, null, null), Fields(parsed["mule"]));
        Assert.Equal((null, null, null, 0), Fields(parsed["odd \"id\" \\ with quotes"]));

        static (bool?, int?, bool?, int?) Fields(PriceBookEntry e) => (e.Buy, e.BuyBase, e.Sell, e.SellBase);
    }

    [Fact]
    public void The_price_book_is_written_sorted_without_empty_entries()
    {
        var s = new StewardSettings();
        s.PriceBook["wine"] = new PriceBookEntry { SellBase = 30 };
        s.PriceBook["beer"] = new PriceBookEntry { BuyBase = 20 };
        s.PriceBook["fish"] = new PriceBookEntry(); // nothing overridden — never written
        var text = SettingsFile.Generate(s);
        Assert.Contains(
            "  \"PriceBook\": {" + SettingsFile.NewLine
            + "    \"beer\": { \"BuyBase\": 20 }," + SettingsFile.NewLine
            + "    \"wine\": { \"SellBase\": 30 }" + SettingsFile.NewLine
            + "  },", text);
        Assert.DoesNotContain("fish", text);
        Assert.Contains("  \"PriceBook\": {},", Defaults);
    }

    [Fact]
    public void The_retired_prisoner_list_is_never_written()
    {
        Assert.DoesNotContain("PrisonersExcluded", Defaults);
        Assert.DoesNotContain("[", Defaults.Split(SettingsFile.NewLine).Where(l => !l.TrimStart().StartsWith("//")));
    }

    [Fact]
    public void Every_key_sits_under_its_help_default_and_range()
    {
        var lines = Defaults.Split(SettingsFile.NewLine);
        foreach (var def in SettingsRegistry.All)
        {
            var keyLine = Array.FindIndex(lines, l => l.StartsWith("  \"" + def.Key + "\": ", StringComparison.Ordinal));
            Assert.True(keyLine > 0, def.Key + " missing");
            Assert.Equal(1, lines.Count(l => l.StartsWith("  \"" + def.Key + "\": ", StringComparison.Ordinal)));

            var block = SettingsFile.CommentBlock(def).Select(c => "  // " + c).ToArray();
            Assert.Equal(block, lines[(keyLine - block.Length)..keyLine]);
            Assert.Equal("", lines[keyLine - block.Length - 1]); // a blank line above the block

            // Every word of the help is in the comment, and the default and range are stated.
            var comment = string.Join(" ", block.Select(l => l.Substring(5)));
            foreach (var word in def.Help.Split(new[] { ' ', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                Assert.Contains(word, comment);
            Assert.Contains("Default: " + def.DefaultFileText, comment);
            Assert.Contains("Allowed: " + def.RangeFileText, comment);
            Assert.All(block, l => Assert.True(l.Length <= 110, def.Key + ": comment line too long: " + l));
        }
    }

    [Fact]
    public void The_file_opens_with_its_explanation_and_group_headings()
    {
        Assert.StartsWith("// Smart Steward - settings" + SettingsFile.NewLine, Defaults);
        foreach (var line in SettingsFile.Header)
            Assert.Contains(("// " + line).TrimEnd(), Defaults);
        Assert.Contains("delete this file", Defaults);
        Assert.Contains("reopen the", Defaults);
        Assert.Contains("while the game is closed", Defaults);
        foreach (var group in SettingsRegistry.Groups)
            Assert.Contains("  // ===== " + SettingsRegistry.GroupLabel(group) + " =====", Defaults);
        Assert.EndsWith("}" + SettingsFile.NewLine, Defaults);
        Assert.True(Defaults.All(c => c < 128), "the file is plain ASCII");
    }

    [Fact]
    public void The_file_is_plain_JSON_with_comments_for_any_reader()
    {
        var json = JObject.Parse(Defaults); // Newtonsoft's own default reader, as a player's tool might use
        Assert.Equal(SettingsRegistry.All.Select(d => d.Key), json.Properties().Select(p => p.Name));
    }

    [Fact]
    public void Writing_odd_values_never_throws_and_reads_back_valid()
    {
        var s = new StewardSettings
        {
            HorseBuyPriceMultiplier = double.NaN,
            MinGoldAfterDeal = -40,
            PackAnimalsTarget = int.MaxValue,
            PriceBook = null!,
        };
        var parsed = SettingsFile.Parse(SettingsFile.Generate(s));
        Assert.False(parsed.Unreadable);
        Assert.Equal(1.2, parsed.Settings.HorseBuyPriceMultiplier);
        Assert.Equal(0, parsed.Settings.MinGoldAfterDeal);
        Assert.Equal(500, parsed.Settings.PackAnimalsTarget);
        Assert.Empty(parsed.Settings.PriceBook);
    }

    // ── Reading: forgiving where a player's hand is ─────────────────────────────────────────

    [Fact]
    public void A_hand_edited_file_is_read_with_trailing_commas_block_comments_and_any_case()
    {
        var text = @"
            // my notes
            {
              /* I like it cheap */
              ""foodstrategy"": ""cheapest"",
              ""FOODDAYS"": 60,
              ""PackAnimalsTarget"": 12.0,   // a whole number written as a decimal is fine
              ""PriceBook"": { ""grain"": { ""buybase"": 9, }, },
              ""prisonersexcluded"": [ ""looter"", ],   // retired in step 12 - ignored, logged
            }
            // the end
            ";
        var parsed = SettingsFile.Parse(text);
        Assert.False(parsed.Unreadable);
        Assert.Empty(parsed.Problems);
        Assert.Equal(FoodStrategy.Cheapest, parsed.Settings.FoodStrategy);
        Assert.Equal(60, parsed.Settings.FoodDays);
        Assert.Equal(12, parsed.Settings.PackAnimalsTarget);
        Assert.Equal(9, parsed.Settings.PriceBook["grain"].BuyBase);
        Assert.Single(parsed.Retired);
    }

    [Fact]
    public void Missing_keys_get_their_defaults()
    {
        var parsed = SettingsFile.Parse("{ \"MountMaxPrice\": 650 }");
        Assert.False(parsed.Unreadable);
        Assert.Empty(parsed.Problems);
        Assert.Equal(650, parsed.Settings.MountMaxPrice);
        Assert.Equal(SettingsRegistry.All.Count - 1, parsed.MissingKeys.Count);
        Assert.DoesNotContain("MountMaxPrice", parsed.MissingKeys);
        Assert.False(parsed.LosesSomething); // only new keys: nothing to back up

        var empty = SettingsFile.Parse("{}");
        Assert.Equal(SettingsRegistry.All.Select(d => d.Key), empty.MissingKeys);
        Assert.Equal(Defaults, SettingsFile.Generate(empty.Settings));
    }

    [Theory]
    [InlineData("{ \"FoodPerMan\": 2.5 }", 50)]
    [InlineData("{ \"foodperman\": 3 }", 60)]
    [InlineData("{ \"FoodPerMan\": 2.04 }", 41)]
    public void An_old_food_per_man_becomes_days_of_food_once(string text, int days)
    {
        // Anton 2026.09.28: the goal is days now. One food lasts a man 20 days at the game's rate, so per man × 20.
        var parsed = SettingsFile.Parse(text);
        Assert.Equal(days, parsed.Settings.FoodDays);
        Assert.Empty(parsed.Problems);
        Assert.False(parsed.LosesSomething);                   // nothing lost: no backup
        var note = Assert.Single(parsed.Renamed);
        Assert.Contains("is now FoodDays = " + days, note);
        Assert.DoesNotContain("FoodPerMan", SettingsFile.Generate(parsed.Settings)); // the rewrite has only the new key
    }

    [Fact]
    public void An_old_food_per_man_gives_way_to_food_days_and_is_read_like_any_value()
    {
        var both = SettingsFile.Parse("{ \"FoodDays\": 30, \"FoodPerMan\": 3 }");
        Assert.Equal(30, both.Settings.FoodDays);
        Assert.Contains(both.Problems, p => p.Contains("\"FoodPerMan\" (replaced by FoodDays) ignored"));

        var huge = SettingsFile.Parse("{ \"FoodPerMan\": 50 }");            // 1,000 days → the most, 365
        Assert.Equal(365, huge.Settings.FoodDays);
        Assert.Contains(huge.Problems, p => p.Contains("FoodDays") && p.Contains("above the maximum"));

        var words = SettingsFile.Parse("{ \"FoodPerMan\": \"lots\" }");    // not a number → the default
        Assert.Equal(40, words.Settings.FoodDays);
        Assert.Single(words.Problems);
    }

    [Fact]
    public void Unknown_keys_are_ignored_and_reported()
    {
        var parsed = SettingsFile.Parse("{ \"FoodPerMen\": 5, \"SellLootMassFirst\": true, \"FoodDays\": 30 }");
        Assert.Equal(30, parsed.Settings.FoodDays);
        Assert.Equal(2, parsed.Problems.Count);
        Assert.Contains(parsed.Problems, p => p.Contains("unknown key \"FoodPerMen\" ignored"));
        Assert.Contains(parsed.Problems, p => p.Contains("unknown key \"SellLootMassFirst\" ignored"));
        Assert.True(parsed.LosesSomething);
    }

    [Theory]
    [InlineData("MinGoldAfterDeal", "2000000", 1_000_000, "above the maximum")]
    [InlineData("MinGoldAfterDeal", "-5", 0, "below the minimum")]
    [InlineData("WarMountsToKeep", "-7", 0, "below the minimum")]
    [InlineData("MountsPer100Footmen", "301", 300, "above the maximum")]
    [InlineData("MountsPer100Footmen", "99999999999999999999999999", 300, "above the maximum")] // past long
    public void Whole_numbers_out_of_range_are_clamped_and_reported(string key, string json, int expected, string why)
    {
        var parsed = ParseOne(key, json);
        var def = (IntSetting)SettingsRegistry.Find(key)!;
        Assert.Equal(expected, def.Get(parsed.Settings));
        var problem = Assert.Single(parsed.Problems);
        Assert.Contains(key, problem);
        Assert.Contains(why, problem);
        Assert.Contains("using " + expected, problem);
        Assert.Matches(@"^line \d+: ", problem);
    }

    [Theory]
    [InlineData("HorseBuyPriceMultiplier", "0", 0.1, "below the minimum")]
    [InlineData("HorseSellPriceMultiplier", "12.5", 10.0, "above the maximum")]
    [InlineData("HorseSellPriceMultiplier", "-1", 0.0, "below the minimum")]
    [InlineData("HorseBuyPriceMultiplier", "1e9", 10.0, "above the maximum")]
    public void Decimals_out_of_range_are_clamped_and_reported(string key, string json, double expected, string why)
    {
        var parsed = ParseOne(key, json);
        Assert.Equal(expected, ((FloatSetting)SettingsRegistry.Find(key)!).Get(parsed.Settings));
        var problem = Assert.Single(parsed.Problems);
        Assert.Contains(key, problem);
        Assert.Contains(why, problem);
    }

    [Fact]
    public void Decimals_are_kept_to_two_places_silently()
    {
        var parsed = ParseOne("HorseBuyPriceMultiplier", "1.23456");
        Assert.Equal(1.23, parsed.Settings.HorseBuyPriceMultiplier);
        Assert.Empty(parsed.Problems);
    }

    [Theory]
    [InlineData("ModEnabled", "\"yes\"")]
    [InlineData("ModEnabled", "1")]
    [InlineData("ModEnabled", "null")]
    [InlineData("MinGoldAfterDeal", "\"1000\"")]
    [InlineData("MinGoldAfterDeal", "10.5")]
    [InlineData("MinGoldAfterDeal", "true")]
    [InlineData("MinGoldAfterDeal", "[ 1 ]")]
    [InlineData("HorseBuyPriceMultiplier", "\"2\"")]
    [InlineData("HorseBuyPriceMultiplier", "NaN")]
    [InlineData("HorseBuyPriceMultiplier", "{ }")]
    [InlineData("FoodStrategy", "1")]
    [InlineData("FoodStrategy", "\"Fancy\"")]
    [InlineData("SellLootOrder", "null")]
    public void Wrong_types_fall_back_to_the_default_and_are_reported(string key, string json)
    {
        var parsed = ParseOne(key, json);
        var def = SettingsRegistry.Find(key)!;
        Assert.False(parsed.Unreadable);
        Assert.Equal(def.DefaultValue, def.GetValue(parsed.Settings));
        var problem = Assert.Single(parsed.Problems);
        Assert.Contains(key + ": expected ", problem);
        Assert.Contains("using the default " + def.DefaultFileText, problem);
    }

    [Fact]
    public void A_key_written_twice_counts_the_later_one()
    {
        var parsed = SettingsFile.Parse("{ \"ModEnabled\": false, \"modenabled\": true }");
        Assert.True(parsed.Settings.ModEnabled);
        Assert.Contains("written twice", Assert.Single(parsed.Problems));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n  ")]
    [InlineData("not json at all")]
    [InlineData("[ 1, 2 ]")]
    [InlineData("\"just text\"")]
    [InlineData("42")]
    [InlineData("// only a comment")]
    [InlineData("{ \"ModEnabled\": }")]
    [InlineData("{ \"ModEnabled\": true")]
    [InlineData("{ \"ModEnabled\": true } { }")]
    [InlineData("{ \"ModEnabled\": true } trailing")]
    [InlineData("{ \"ModEnabled\" true }")]
    [InlineData("\u0000\u0001binary")]
    public void An_unreadable_file_gives_the_defaults_and_says_why(string text)
    {
        var parsed = SettingsFile.Parse(text);
        Assert.True(parsed.Unreadable);
        Assert.False(string.IsNullOrWhiteSpace(parsed.Error));
        Assert.True(parsed.LosesSomething);
        Assert.Equal(Defaults, SettingsFile.Generate(parsed.Settings));
    }

    [Fact]
    public void A_file_cut_off_halfway_is_unreadable()
    {
        var parsed = SettingsFile.Parse(Defaults.Substring(0, Defaults.Length / 2));
        Assert.True(parsed.Unreadable);
    }

    // ── Reading the price book and the prisoner list ────────────────────────────────────────

    [Fact]
    public void Bad_price_book_entries_are_dropped_or_fixed_and_reported()
    {
        var text = @"
            { ""PriceBook"": {
                ""grain"": { ""BuyBase"": -5, ""SellBase"": 2000000 },
                ""fish"": { ""Buy"": ""yes"", ""Sell"": false },
                ""olives"": { ""BuyBase"": 12.5, ""Colour"": ""green"" },
                ""meat"": 7,
                """": { ""Buy"": false },
                ""cheese"": { },
                ""butter"": { ""Buy"": null, ""BuyBase"": null },
                ""beer"": { ""BuyBase"": 20.0 }
            } }
            ";
        var parsed = SettingsFile.Parse(text);
        var book = parsed.Settings.PriceBook;

        Assert.Equal(0, book["grain"].BuyBase);                         // clamped up
        Assert.Equal(PriceBookSetting.MaxBase, book["grain"].SellBase); // clamped down
        Assert.Null(book["fish"].Buy);                                  // wrong type → default
        Assert.False(book["fish"].Sell);
        Assert.False(book.ContainsKey("olives"));                       // nothing valid left
        Assert.False(book.ContainsKey("meat"));                         // not an object
        Assert.False(book.ContainsKey(""));
        Assert.False(book.ContainsKey("cheese"));                       // empty: nothing to keep
        Assert.False(book.ContainsKey("butter"));                       // nulls mean default
        Assert.Equal(20, book["beer"].BuyBase);
        Assert.Equal(3, book.Count);

        Assert.Equal(7, parsed.Problems.Count);
        Assert.Contains(parsed.Problems, p => p.Contains("PriceBook[\"grain\"].BuyBase") && p.Contains("using 0"));
        Assert.Contains(parsed.Problems, p => p.Contains("PriceBook[\"grain\"].SellBase") && p.Contains("using 1000000"));
        Assert.Contains(parsed.Problems, p => p.Contains("PriceBook[\"fish\"].Buy: expected true or false"));
        Assert.Contains(parsed.Problems, p => p.Contains("PriceBook[\"olives\"].BuyBase: expected a whole number"));
        Assert.Contains(parsed.Problems, p => p.Contains("PriceBook[\"olives\"].Colour: unknown field ignored"));
        Assert.Contains(parsed.Problems, p => p.Contains("PriceBook[\"meat\"]: expected { ... }"));
        Assert.Contains(parsed.Problems, p => p.Contains("empty item id"));
    }

    [Fact]
    public void A_price_book_that_is_not_an_object_is_empty_and_reported()
    {
        var parsed = ParseOne("PriceBook", "[ \"grain\" ]");
        Assert.Empty(parsed.Settings.PriceBook);
        Assert.Contains("PriceBook: expected", Assert.Single(parsed.Problems));
    }

    [Fact]
    public void The_old_prisoner_list_is_dropped_and_logged_whatever_it_holds()
    {
        // Step 12 (round 1): ransom is all or none - an old file's tick-list is simply ignored.
        foreach (var json in new[] { "[ \"looter\", \"sea_raiders_boss\" ]", "\"looter\"", "[]" })
        {
            var parsed = SettingsFile.Parse(Defaults.Replace("  \"PrisonerAction\": \"Ransom\",",
                "  \"PrisonerAction\": \"Ransom\"," + SettingsFile.NewLine + "  \"PrisonersExcluded\": " + json + ","));
            Assert.Empty(parsed.Problems);
            Assert.False(parsed.LosesSomething);
            Assert.Contains("\"PrisonersExcluded\"", Assert.Single(parsed.Retired));
            Assert.Contains("every prisoner may be ransomed now", parsed.Retired[0]);
            Assert.DoesNotContain("PrisonersExcluded", SettingsFile.Generate(parsed.Settings));
        }
    }

    // ── Renamed keys (step 8: AutoExecute became the Full-autonomous steward) ─────────────────

    /// <summary>The defaults' file with the AutonomousSteward line written under the OLD name.</summary>
    private static string WithOldAutoExecute(string valueJson) =>
        Defaults.Replace("  \"AutonomousSteward\": false,", "  \"AutoExecute\": " + valueJson + ",");

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void The_old_AutoExecute_key_carries_its_value_over_to_AutonomousSteward(string json, bool expected)
    {
        var text = WithOldAutoExecute(json);
        Assert.DoesNotContain("AutonomousSteward", text.Split(SettingsFile.NewLine).Where(l => !l.TrimStart().StartsWith("//")));

        var parsed = SettingsFile.Parse(text);

        Assert.Equal(expected, parsed.Settings.AutonomousSteward);
        Assert.Empty(parsed.Problems);                 // nothing lost: no backup
        Assert.False(parsed.LosesSomething);
        Assert.DoesNotContain("AutonomousSteward", parsed.MissingKeys);
        var note = Assert.Single(parsed.Renamed);
        Assert.Contains("\"AutoExecute\" is now AutonomousSteward - its value " + json + " carried over", note);
        Assert.StartsWith("line ", note);
        // The rewrite has only the new name.
        var rewritten = SettingsFile.Generate(parsed.Settings);
        Assert.DoesNotContain("\"AutoExecute\"", rewritten);
        Assert.Empty(SettingsFile.Parse(rewritten).Renamed);
    }

    [Fact]
    public void The_old_key_name_is_case_insensitive_like_every_key()
    {
        var parsed = SettingsFile.Parse(Defaults.Replace("  \"AutonomousSteward\": false,", "  \"autoexecute\": true,"));
        Assert.True(parsed.Settings.AutonomousSteward);
        Assert.Single(parsed.Renamed);
    }

    [Fact]
    public void When_both_names_are_written_the_new_one_wins()
    {
        // The new key says false, the old one (written after it) says true: the new name counts.
        var text = Defaults.Replace("  \"AutonomousSteward\": false,", "  \"AutonomousSteward\": false," + SettingsFile.NewLine + "  \"AutoExecute\": true,");
        var parsed = SettingsFile.Parse(text);
        Assert.False(parsed.Settings.AutonomousSteward);
        Assert.Empty(parsed.Renamed);
        Assert.Contains("(the old name of AutonomousSteward) ignored - AutonomousSteward is set", Assert.Single(parsed.Problems));
    }

    [Fact]
    public void An_old_key_with_a_wrong_value_falls_back_like_the_new_one_would()
    {
        var parsed = SettingsFile.Parse(WithOldAutoExecute("\"yes\""));
        Assert.False(parsed.Settings.AutonomousSteward);
        Assert.Empty(parsed.Renamed);
        Assert.Contains("AutonomousSteward: expected true or false", Assert.Single(parsed.Problems));
    }

    // ── Retired keys (step 12, playtest round 1) ───────────────────────────────────────────────

    [Fact]
    public void The_old_WarMountsManualTarget_is_dropped()
    {
        // Anton's round-1 file: one number for both kinds bought 10 horses AND 10 war horses.
        var text = Defaults.Replace("  \"WarMountsToKeep\": 10,", "  \"WarMountsManualTarget\": 10,");
        Assert.DoesNotContain("\"WarMountsToKeep\"", text);

        var parsed = SettingsFile.Parse(text);

        Assert.Equal(10, parsed.Settings.WarMountsToKeep);
        Assert.Empty(parsed.Problems);
        Assert.False(parsed.LosesSomething); // dropped on purpose: logged, no backup
        var note = Assert.Single(parsed.Retired);
        Assert.StartsWith("line ", note);
        Assert.Contains("\"WarMountsManualTarget\" (10) is retired and ignored - the steward no longer counts troop upgrades", note);
        var rewritten = SettingsFile.Generate(parsed.Settings);
        Assert.DoesNotContain("WarMountsManualTarget\"", rewritten);
        Assert.Empty(SettingsFile.Parse(rewritten).Retired);
    }

    [Fact]
    public void The_upgrade_horse_keys_of_step_12_are_retired_by_step_17_and_logged_once()
    {
        // Anton 2026.09.28: "don't worry about the mounts needing upgrades ... make it simpler for now".
        var text = Defaults.Replace("  \"WarMountsToKeep\": 10,", "  \"WarMountsToKeep\": 10," + SettingsFile.NewLine
            + "  \"WarMountsHorseTarget\": -1," + SettingsFile.NewLine + "  \"WarMountsWarHorseTarget\": 10,"
            + SettingsFile.NewLine + "  \"WarMountsExtra\": 2," + SettingsFile.NewLine + "  \"WarMountsCountAsMounts\": false,");
        var parsed = SettingsFile.Parse(text);
        Assert.Equal(4, parsed.Retired.Count);
        Assert.Empty(parsed.Problems);
        Assert.False(parsed.LosesSomething);
        Assert.Equal(10, parsed.Settings.WarMountsToKeep);              // not converted: a plain number from now on
        Assert.Contains(parsed.Retired, n => n.Contains("\"WarMountsWarHorseTarget\" (10)") && n.Contains("WarMountsToKeep"));
        Assert.Contains(parsed.Retired, n => n.Contains("\"WarMountsCountAsMounts\"") && n.Contains("MountsPer100Footmen"));
        var rewritten = SettingsFile.Generate(parsed.Settings);
        foreach (var key in new[] { "WarMountsHorseTarget", "WarMountsWarHorseTarget", "WarMountsExtra", "WarMountsCountAsMounts" })
            Assert.DoesNotContain("\"" + key + "\"", rewritten);
        Assert.Empty(SettingsFile.Parse(rewritten).Retired);            // once: the rewrite drops them
    }

    [Fact]
    public void A_retired_key_is_case_insensitive_and_never_a_problem()
    {
        var parsed = SettingsFile.Parse(Defaults.Replace("  \"WarMountsToKeep\": 10,", "  \"WarMountsToKeep\": 10,"
            + SettingsFile.NewLine + "  \"warmountsmanualtarget\": 3,"));
        Assert.Single(parsed.Retired);
        Assert.Empty(parsed.Problems);
        Assert.Equal(10, parsed.Settings.WarMountsToKeep);
    }

    [Fact]
    public void Wrap_breaks_between_words_only()
    {
        var lines = SettingsFile.Wrap("aaa bbb ccc ddd", 7).ToList();
        Assert.Equal(new[] { "aaa bbb", "ccc ddd" }, lines);
        Assert.Equal(new[] { "abcdefghij" }, SettingsFile.Wrap("abcdefghij", 4));
        Assert.Empty(SettingsFile.Wrap("   ", 10));
    }
}
