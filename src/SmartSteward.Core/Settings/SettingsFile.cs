using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartSteward.Core.Settings
{
    /// <summary>What <see cref="SettingsFile.Parse"/> made of a settings file.</summary>
    public sealed class SettingsParseResult
    {
        internal SettingsParseResult(StewardSettings settings) => Settings = settings;

        /// <summary>The settings the file asks for, every value valid: defaults where the file said
        /// nothing usable, clamped numbers where it said too much.</summary>
        public StewardSettings Settings { get; }

        /// <summary>The text is not a JSON object at all (empty, broken, an array…) — <see cref="Settings"/>
        /// are then all defaults, and the caller keeps the text as a backup before writing a fresh file.</summary>
        public bool Unreadable { get; internal set; }

        /// <summary>Why the text is unreadable (the JSON reader's message, with line and position).</summary>
        public string? Error { get; internal set; }

        /// <summary>One line per value the file could not have as written — clamped, wrong type, unknown key,
        /// a bad price-book entry — each naming the key, the line and what was used instead.</summary>
        public List<string> Problems { get; } = new List<string>();

        /// <summary>Registered keys the file does not mention (they got their defaults).</summary>
        public List<string> MissingKeys { get; } = new List<string>();

        /// <summary>One line per old key whose value was carried over to its new name (AutoExecute →
        /// AutonomousSteward). Nothing is lost, so no backup — the rewritten file simply has the new name.</summary>
        public List<string> Renamed { get; } = new List<string>();

        /// <summary>One line per retired key the file still had (<see cref="SettingsFile.RetiredKeys"/>): its value is
        /// dropped on purpose and the line says what took its place. Not a problem — no backup; the rewritten file
        /// simply no longer has it.</summary>
        public List<string> Retired { get; } = new List<string>();

        /// <summary>The file said something the settings could not keep — worth a backup before rewriting it.</summary>
        public bool LosesSomething => Unreadable || Problems.Count > 0;
    }

    /// <summary>
    /// The settings file (DESIGN §8): <c>Configs\SmartSteward\settings.json</c>, JSON with a <c>//</c>
    /// comment block above every key. Pure text in, text out — the Module only reads and writes the disk.
    /// <para><b>Writing</b> is by hand (Newtonsoft's writer only emits <c>/* */</c> comments, RESEARCH §12):
    /// a header, then the registry group by group, each key under its help, its default and its range.
    /// <b>Reading</b> uses Newtonsoft (the game's 13.0.1), which skips <c>//</c> and <c>/* */</c> comments and
    /// tolerates trailing commas. It never throws: missing keys get defaults, unknown keys are ignored,
    /// numbers out of range are clamped, wrong types fall back to the default — each noted in
    /// <see cref="SettingsParseResult.Problems"/> for the log.</para>
    /// </summary>
    public static class SettingsFile
    {
        /// <summary>Windows line ends — the file is opened by players in any editor.</summary>
        public const string NewLine = "\r\n";

        private const int CommentWidth = 100;

        /// <summary>Keys that were renamed (old → new, old names case-insensitive): a file that still has the old name
        /// keeps its value under the new one, once — the rewrite drops the old name.
        /// AutoExecute became the Full-autonomous steward in PLAN step 8 (DESIGN §6).</summary>
        public static readonly IReadOnlyDictionary<string, string> RenamedKeys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["AutoExecute"] = nameof(StewardSettings.AutonomousSteward),
            };

        /// <summary>A key whose value changes its unit on the way to its new name.</summary>
        public sealed class KeyConversion
        {
            internal KeyConversion(string newKey, Func<double, double> convert, string how)
            {
                NewKey = newKey;
                Convert = convert;
                How = how;
            }

            public string NewKey { get; }

            /// <summary>Old value → new value (then read like any value of the new key: rounded, clamped).</summary>
            public Func<double, double> Convert { get; }

            /// <summary>The log's words for the conversion.</summary>
            public string How { get; }
        }

        /// <summary>Keys replaced by a key in another unit (old names case-insensitive): a file that still has the old one
        /// gets its value converted into the new key, once — logged, not a problem (nothing is lost), and the rewrite has
        /// only the new key. The new key written too wins. FoodPerMan (food units per man) became FoodDays (days of food)
        /// — Anton 2026.09.28: at vanilla's rate one food lasts a man 20 days, so 2.0 per man = 40 days.</summary>
        public static readonly IReadOnlyDictionary<string, KeyConversion> ConvertedKeys =
            new Dictionary<string, KeyConversion>(StringComparer.OrdinalIgnoreCase)
            {
                ["FoodPerMan"] = new KeyConversion(nameof(StewardSettings.FoodDays),
                    perMan => perMan * Planning.FoodGoal.DaysPerFoodPerMan,
                    "x " + Planning.FoodGoal.DaysPerFoodPerMan + " - one food lasts a man about 20 days at the game's rate"),
            };

        /// <summary>Keys that are gone (old names case-insensitive → what the log says instead): a file that still has one
        /// loses its value on purpose, once — logged, not a problem, and the rewrite drops it (playtest round 1, step 12).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> RetiredKeys =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // One number for both kinds kept 10 horses AND 10 war horses: each kind now has its own, both automatic.
                ["WarMountsManualTarget"] = "upgrade horses are now set per kind (" + nameof(StewardSettings.WarMountsHorseTarget)
                                            + ", " + nameof(StewardSettings.WarMountsWarHorseTarget)
                                            + ") - both start automatic (-1)",
                // The "Prisoners to ransom" tick-list: ransom is all or none now, "Include lords" the one choice left.
                ["PrisonersExcluded"] = "every prisoner may be ransomed now; " + nameof(StewardSettings.RansomHeroPrisoners)
                                        + " still decides the lords",
            };

        /// <summary>The explanation at the top of the file.</summary>
        public static readonly string[] Header =
        {
            "Smart Steward - settings",
            "",
            "How to edit: change a value and save. The steward reads this file again when you reopen the",
            "Party Steward window (or load a campaign) - or edit it while the game is closed.",
            "To reset everything: delete this file. The game writes a fresh one with every default.",
            "",
            "Lines starting with // are comments. Values are true/false, numbers, or \"text\" in quotes.",
            "A number out of range is moved into range, a value of the wrong kind goes back to its default,",
            "and each fix is noted in smart_steward.log in this folder. A file that cannot be read at all is",
            "kept as settings.json.bak and replaced with a fresh one.",
            "",
            "The same settings are in the Party Steward's Instructions tab and, with Mod Configuration Menu",
            "(MCM), under Mod Options. A change made there is written here at once.",
        };

        // ── Writing ──────────────────────────────────────────────────────────────────────────────

        /// <summary>The whole file for <paramref name="settings"/>: header, then every registered key in
        /// registry order under its comment block. Deterministic — the same settings give the same text,
        /// so comparing texts tells whether anything changed.</summary>
        public static string Generate(StewardSettings settings)
        {
            var sb = new StringBuilder();
            foreach (var line in Header) Comment(sb, "", line);
            sb.Append('{').Append(NewLine);

            var all = SettingsRegistry.All;
            string? group = null;
            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def.Group != group)
                {
                    group = def.Group;
                    if (i > 0) sb.Append(NewLine);
                    Comment(sb, "  ", "===== " + SettingsRegistry.GroupLabel(group) + " =====");
                }
                sb.Append(NewLine);
                foreach (var line in CommentBlock(def)) Comment(sb, "  ", line);
                sb.Append("  ").Append(JsonConvert.ToString(def.Key)).Append(": ").Append(ValueText(def, settings));
                if (i < all.Count - 1) sb.Append(',');
                sb.Append(NewLine);
            }
            sb.Append('}').Append(NewLine);
            return sb.ToString();
        }

        /// <summary>The comment lines above a key: "Label: help" wrapped, then "Default: … Range: …".</summary>
        public static IEnumerable<string> CommentBlock(SettingDefinition def)
        {
            foreach (var paragraph in (def.Label + ": " + def.Help).Split('\n'))
                foreach (var line in Wrap(paragraph, CommentWidth))
                    yield return line;
            yield return "Default: " + def.DefaultFileText + "   Allowed: " + def.RangeFileText;
        }

        private static void Comment(StringBuilder sb, string indent, string text)
        {
            sb.Append(indent).Append("//");
            if (text.Length > 0) sb.Append(' ').Append(text);
            sb.Append(NewLine);
        }

        /// <summary>A value as the file writes it (<c>true</c>, <c>2.0</c>, <c>"Balanced"</c>, the price book object…).</summary>
        internal static string ValueText(SettingDefinition def, StewardSettings settings)
        {
            switch (def)
            {
                case BoolSetting b:
                    return b.Get(settings) ? "true" : "false";
                case IntSetting n:
                    return SettingDefinition.FormatInt(n.Get(settings));
                case FloatSetting f:
                    return f.Format(f.Get(settings));
                case EnumSetting e:
                    return JsonConvert.ToString(e.Names[e.GetIndex(settings)]);
                case PriceBookSetting p:
                    return PriceBookText(p.Get(settings));
                default:
                    throw new InvalidOperationException("no file syntax for " + def.Key);
            }
        }

        private static string PriceBookText(Dictionary<string, PriceBookEntry> book)
        {
            var ids = book.Where(kv => kv.Value != null && !kv.Value.IsEmpty && !string.IsNullOrWhiteSpace(kv.Key))
                .Select(kv => kv.Key).OrderBy(id => id, StringComparer.Ordinal).ToList();
            if (ids.Count == 0) return "{}";

            var sb = new StringBuilder();
            sb.Append('{').Append(NewLine);
            for (int i = 0; i < ids.Count; i++)
            {
                var entry = book[ids[i]];
                var fields = new List<string>();
                if (entry.Buy.HasValue) fields.Add("\"Buy\": " + (entry.Buy.Value ? "true" : "false"));
                if (entry.BuyBase.HasValue) fields.Add("\"BuyBase\": " + SettingDefinition.FormatInt(entry.BuyBase.Value));
                if (entry.Sell.HasValue) fields.Add("\"Sell\": " + (entry.Sell.Value ? "true" : "false"));
                if (entry.SellBase.HasValue) fields.Add("\"SellBase\": " + SettingDefinition.FormatInt(entry.SellBase.Value));
                sb.Append("    ").Append(JsonConvert.ToString(ids[i])).Append(": { ")
                    .Append(string.Join(", ", fields)).Append(" }");
                if (i < ids.Count - 1) sb.Append(',');
                sb.Append(NewLine);
            }
            sb.Append("  }");
            return sb.ToString();
        }

        /// <summary>Greedy word wrap; a word longer than the width gets a line of its own.</summary>
        internal static IEnumerable<string> Wrap(string text, int width)
        {
            var line = new StringBuilder();
            foreach (var word in text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    yield return line.ToString();
                    line.Clear();
                }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            if (line.Length > 0) yield return line.ToString();
        }

        // ── Reading ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Reads a settings file's text. Never throws — see the class comment for the rules.</summary>
        public static SettingsParseResult Parse(string? text)
        {
            var result = new SettingsParseResult(new StewardSettings());
            if (string.IsNullOrWhiteSpace(text))
            {
                result.Unreadable = true;
                result.Error = "the file is empty";
                return result;
            }

            JObject root;
            try
            {
                root = ReadObject(text!);
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is FormatException)
            {
                result.Unreadable = true;
                result.Error = ex.Message;
                return result;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var renamed = new List<(JProperty Old, SettingDefinition New)>();
            var converted = new List<(JProperty Old, SettingDefinition New, KeyConversion How)>();
            foreach (var property in root.Properties())
            {
                var def = SettingsRegistry.Find(property.Name);
                if (def == null)
                {
                    if (RenamedKeys.TryGetValue(property.Name.Trim(), out var newKey) && SettingsRegistry.Find(newKey) is { } renamedTo)
                        renamed.Add((property, renamedTo));
                    else if (ConvertedKeys.TryGetValue(property.Name.Trim(), out var conversion)
                             && SettingsRegistry.Find(conversion.NewKey) is { } convertedTo)
                        converted.Add((property, convertedTo, conversion));
                    else if (RetiredKeys.TryGetValue(property.Name.Trim(), out var instead))
                        result.Retired.Add(At(property) + "\"" + property.Name.Trim() + "\" (" + Describe(property.Value)
                            + ") is retired and ignored - " + instead);
                    else
                        result.Problems.Add(At(property) + "unknown key \"" + property.Name + "\" ignored");
                    continue;
                }
                if (!seen.Add(def.Key))
                    result.Problems.Add(At(property) + def.Key + " is written twice - the later one counts");
                Read(def, property.Value, result);
            }
            // An old key carries its value over to its new name — unless the new name is written too (it wins).
            foreach (var (old, def) in renamed)
            {
                if (seen.Contains(def.Key))
                {
                    result.Problems.Add(At(old) + "\"" + old.Name + "\" (the old name of " + def.Key + ") ignored - "
                        + def.Key + " is set");
                    continue;
                }
                seen.Add(def.Key);
                int problems = result.Problems.Count;
                Read(def, old.Value, result);
                if (result.Problems.Count == problems)
                    result.Renamed.Add(At(old) + "\"" + old.Name + "\" is now " + def.Key + " - its value "
                        + ValueText(def, result.Settings) + " carried over");
            }
            // An old key in another unit is converted into its new key — unless the new key is written too (it wins).
            foreach (var (old, def, how) in converted)
            {
                if (seen.Contains(def.Key))
                {
                    result.Problems.Add(At(old) + "\"" + old.Name + "\" (replaced by " + def.Key + ") ignored - "
                        + def.Key + " is set");
                    continue;
                }
                seen.Add(def.Key);
                if (!(old.Value.Type == JTokenType.Integer || old.Value.Type == JTokenType.Float)
                    || double.IsNaN((double)old.Value) || double.IsInfinity((double)old.Value))
                {
                    WrongType(def, old.Value, "a number", result);
                    continue;
                }
                double value = how.Convert((double)old.Value);
                int problems = result.Problems.Count;
                Read(def, new JValue(Math.Round(value, MidpointRounding.AwayFromZero)), result);
                if (result.Problems.Count == problems)
                    result.Renamed.Add(At(old) + "\"" + old.Name + "\" (" + Describe(old.Value) + ") is now " + def.Key + " = "
                        + ValueText(def, result.Settings) + " (" + how.How + ")");
            }
            foreach (var def in SettingsRegistry.All)
                if (!seen.Contains(def.Key))
                    result.MissingKeys.Add(def.Key);
            return result;
        }

        private static JObject ReadObject(string text)
        {
            using (var reader = new JsonTextReader(new StringReader(text)))
            {
                // Ids are plain strings: never turn one that looks like a date into a DateTime.
                reader.DateParseHandling = DateParseHandling.None;
                reader.FloatParseHandling = FloatParseHandling.Double;
                var load = new JsonLoadSettings
                {
                    CommentHandling = CommentHandling.Ignore,
                    LineInfoHandling = LineInfoHandling.Load,
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Replace,
                };
                var token = JToken.ReadFrom(reader, load);
                while (token.Type == JTokenType.Comment) token = JToken.ReadFrom(reader, load);
                if (!(token is JObject root))
                    throw new FormatException("the file must be one { ... } object, not " + Describe(token));
                while (reader.Read())
                    if (reader.TokenType != JsonToken.Comment)
                        throw new FormatException("there is more text after the closing } (line "
                            + reader.LineNumber.ToString(CultureInfo.InvariantCulture) + ")");
                return root;
            }
        }

        private static void Read(SettingDefinition def, JToken token, SettingsParseResult result)
        {
            var settings = result.Settings;
            switch (def)
            {
                case BoolSetting b:
                    if (token.Type == JTokenType.Boolean) b.Set(settings, (bool)token);
                    else WrongType(def, token, "true or false", result);
                    break;

                case IntSetting n:
                    if (!TryWholeNumber(token, out var whole))
                    {
                        WrongType(def, token, "a whole number", result);
                        break;
                    }
                    var clamped = n.Clamp(whole);
                    if (clamped != whole) Clamped(def, token, SettingDefinition.FormatInt(clamped), whole < n.Min, result);
                    n.Set(settings, clamped);
                    break;

                case FloatSetting f:
                    if (!TryNumber(token, out var number))
                    {
                        WrongType(def, token, "a number", result);
                        break;
                    }
                    if (number < f.Min || number > f.Max)
                        Clamped(def, token, f.Format(f.Normalize(number)), number < f.Min, result);
                    f.Set(settings, number);
                    break;

                case EnumSetting e:
                    // A string JValue's ToString() is the bare text, no quotes.
                    var index = token.Type == JTokenType.String ? e.IndexOf(token.ToString()) : -1;
                    if (index < 0) WrongType(def, token, e.RangeFileText, result);
                    else e.SetIndex(settings, index);
                    break;

                case PriceBookSetting p:
                    ReadPriceBook(p, token, result);
                    break;
            }
        }

        private static void ReadPriceBook(PriceBookSetting def, JToken token, SettingsParseResult result)
        {
            if (!(token is JObject book))
            {
                WrongType(def, token, "{ item id: { ... } }", result);
                return;
            }
            var target = def.Get(result.Settings);
            foreach (var item in book.Properties())
            {
                var id = item.Name.Trim();
                if (id.Length == 0)
                {
                    result.Problems.Add(At(item) + def.Key + ": an entry with an empty item id was dropped");
                    continue;
                }
                if (!(item.Value is JObject fields))
                {
                    result.Problems.Add(At(item) + def.Key + "[\"" + id + "\"]: expected { ... }, found "
                        + Describe(item.Value) + " - entry dropped");
                    continue;
                }
                var entry = new PriceBookEntry();
                foreach (var field in fields.Properties())
                {
                    var name = field.Name.Trim();
                    var where = def.Key + "[\"" + id + "\"]." + name;
                    if (Is(name, "Buy") || Is(name, "Sell"))
                    {
                        bool? tick = null;
                        if (field.Value.Type == JTokenType.Boolean)
                            tick = (bool)field.Value;
                        else if (field.Value.Type != JTokenType.Null)
                            result.Problems.Add(At(field) + where + ": expected true or false, found "
                                + Describe(field.Value) + " - left at the default");
                        if (Is(name, "Buy")) entry.Buy = tick;
                        else entry.Sell = tick;
                    }
                    else if (Is(name, "BuyBase") || Is(name, "SellBase"))
                    {
                        int? price = null;
                        if (field.Value.Type != JTokenType.Null)
                        {
                            if (!TryWholeNumber(field.Value, out var raw))
                            {
                                result.Problems.Add(At(field) + where + ": expected a whole number, found "
                                    + Describe(field.Value) + " - left at the default");
                            }
                            else
                            {
                                price = (int)Math.Max(0, Math.Min(PriceBookSetting.MaxBase, raw));
                                if (price.Value != raw)
                                    result.Problems.Add(At(field) + where + ": " + raw.ToString(CultureInfo.InvariantCulture)
                                        + " is outside 0 to " + PriceBookSetting.MaxBase.ToString(CultureInfo.InvariantCulture)
                                        + " - using " + price.Value.ToString(CultureInfo.InvariantCulture));
                            }
                        }
                        if (Is(name, "BuyBase")) entry.BuyBase = price;
                        else entry.SellBase = price;
                    }
                    else
                    {
                        result.Problems.Add(At(field) + where + ": unknown field ignored (Buy, BuyBase, Sell, SellBase)");
                    }
                }
                if (!entry.IsEmpty) target[id] = entry;
            }
        }

        private static bool Is(string name, string field) => string.Equals(name, field, StringComparison.OrdinalIgnoreCase);

        /// <summary>An integer token, or a float that is whole (110.0). Huge values saturate at long's limits
        /// (then get clamped like any other number).</summary>
        private static bool TryWholeNumber(JToken token, out long value)
        {
            value = 0;
            if (token.Type == JTokenType.Integer)
            {
                var raw = ((JValue)token).Value;
                if (raw is BigInteger big)
                    value = big.Sign < 0 ? long.MinValue : long.MaxValue;
                else
                    value = Convert.ToInt64(raw, CultureInfo.InvariantCulture);
                return true;
            }
            if (token.Type == JTokenType.Float)
            {
                var d = (double)token;
                if (double.IsNaN(d) || double.IsInfinity(d) || Math.Floor(d) != d) return false;
                value = d <= long.MinValue ? long.MinValue : d >= long.MaxValue ? long.MaxValue : (long)d;
                return true;
            }
            return false;
        }

        private static bool TryNumber(JToken token, out double value)
        {
            value = 0;
            if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float) return false;
            var raw = ((JValue)token).Value;
            value = raw is BigInteger big ? (double)big : Convert.ToDouble(raw, CultureInfo.InvariantCulture);
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void WrongType(SettingDefinition def, JToken token, string expected, SettingsParseResult result) =>
            result.Problems.Add(At(token) + def.Key + ": expected " + expected + ", found " + Describe(token)
                + " - using the default " + def.DefaultFileText);

        private static void Clamped(SettingDefinition def, JToken token, string used, bool below, SettingsParseResult result) =>
            result.Problems.Add(At(token) + def.Key + ": " + Describe(token) + " is "
                + (below ? "below the minimum" : "above the maximum") + " - using " + used);

        /// <summary>"line 12: " — where a hand-edited file went wrong.</summary>
        private static string At(JToken token)
        {
            var info = (IJsonLineInfo)token;
            return info.HasLineInfo() ? "line " + info.LineNumber.ToString(CultureInfo.InvariantCulture) + ": " : "";
        }

        private static string Describe(JToken token)
        {
            string text;
            switch (token.Type)
            {
                case JTokenType.Object: return "an object { ... }";
                case JTokenType.Array: return "a list [ ... ]";
                case JTokenType.Null: return "null";
                case JTokenType.String: text = JsonConvert.ToString((string?)token); break;
                default: text = token.ToString(Formatting.None); break;
            }
            return text.Length <= 40 ? text : text.Substring(0, 37) + "...";
        }
    }
}
