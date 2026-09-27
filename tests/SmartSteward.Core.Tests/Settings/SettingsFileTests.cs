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
        s.PrisonersExcluded.AddRange(new[] { "sea_raiders_boss", "looter" });
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
    public void The_prisoner_list_is_written_cleaned_and_sorted()
    {
        var s = new StewardSettings();
        s.PrisonersExcluded.AddRange(new[] { " looter ", "sea_raiders_boss", "looter", "", "   " });
        Assert.Contains("  \"PrisonersExcluded\": [ \"looter\", \"sea_raiders_boss\" ],", SettingsFile.Generate(s));
        Assert.Contains("  \"PrisonersExcluded\": [],", Defaults);
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
            FoodPerMan = double.NaN,
            MinGoldAfterDeal = -40,
            PackAnimalsTarget = int.MaxValue,
            PriceBook = null!,
            PrisonersExcluded = null!,
        };
        var parsed = SettingsFile.Parse(SettingsFile.Generate(s));
        Assert.False(parsed.Unreadable);
        Assert.Equal(2.0, parsed.Settings.FoodPerMan);
        Assert.Equal(0, parsed.Settings.MinGoldAfterDeal);
        Assert.Equal(500, parsed.Settings.PackAnimalsTarget);
        Assert.Empty(parsed.Settings.PriceBook);
        Assert.Empty(parsed.Settings.PrisonersExcluded);
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
              ""FOODPERMAN"": 3,
              ""PackAnimalsTarget"": 12.0,   // a whole number written as a decimal is fine
              ""PriceBook"": { ""grain"": { ""buybase"": 9, }, },
              ""PrisonersExcluded"": [ ""looter"", ],
            }
            // the end
            ";
        var parsed = SettingsFile.Parse(text);
        Assert.False(parsed.Unreadable);
        Assert.Empty(parsed.Problems);
        Assert.Equal(FoodStrategy.Cheapest, parsed.Settings.FoodStrategy);
        Assert.Equal(3.0, parsed.Settings.FoodPerMan);
        Assert.Equal(12, parsed.Settings.PackAnimalsTarget);
        Assert.Equal(9, parsed.Settings.PriceBook["grain"].BuyBase);
        Assert.Equal(new[] { "looter" }, parsed.Settings.PrisonersExcluded);
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

    [Fact]
    public void Unknown_keys_are_ignored_and_reported()
    {
        var parsed = SettingsFile.Parse("{ \"FoodPerMen\": 5, \"SellLootMassFirst\": true, \"FoodPerMan\": 4 }");
        Assert.Equal(4.0, parsed.Settings.FoodPerMan);
        Assert.Equal(2, parsed.Problems.Count);
        Assert.Contains(parsed.Problems, p => p.Contains("unknown key \"FoodPerMen\" ignored"));
        Assert.Contains(parsed.Problems, p => p.Contains("unknown key \"SellLootMassFirst\" ignored"));
        Assert.True(parsed.LosesSomething);
    }

    [Theory]
    [InlineData("MinGoldAfterDeal", "2000000", 1_000_000, "above the maximum")]
    [InlineData("MinGoldAfterDeal", "-5", 0, "below the minimum")]
    [InlineData("WarMountsManualTarget", "-7", -1, "below the minimum")]
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
    [InlineData("FoodPerMan", "0", 0.1, "below the minimum")]
    [InlineData("FoodPerMan", "12.5", 10.0, "above the maximum")]
    [InlineData("SellPriceMultiplier", "-1", 0.0, "below the minimum")]
    [InlineData("BuyPriceMultiplier", "1e9", 10.0, "above the maximum")]
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
        var parsed = ParseOne("BuyPriceMultiplier", "1.23456");
        Assert.Equal(1.23, parsed.Settings.BuyPriceMultiplier);
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
    [InlineData("FoodPerMan", "\"2\"")]
    [InlineData("FoodPerMan", "NaN")]
    [InlineData("FoodPerMan", "{ }")]
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
    public void The_prisoner_list_keeps_only_ids()
    {
        var parsed = ParseOne("PrisonersExcluded", "[ \"looter\", 5, null, \" looter \", \"\", \"2012-01-01T00:00:00\", { } ]");
        Assert.Equal(new[] { "2012-01-01T00:00:00", "looter" }, parsed.Settings.PrisonersExcluded); // a date-like id stays text
        Assert.Equal(3, parsed.Problems.Count);
        Assert.All(parsed.Problems, p => Assert.Contains("is not an id in quotes - dropped", p));
    }

    [Fact]
    public void A_prisoner_list_that_is_not_a_list_is_empty_and_reported()
    {
        var parsed = ParseOne("PrisonersExcluded", "\"looter\"");
        Assert.Empty(parsed.Settings.PrisonersExcluded);
        Assert.Contains("PrisonersExcluded: expected a list", Assert.Single(parsed.Problems));
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
