using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SmartSteward.Core;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Tests;

/// <summary>
/// DESIGN §9: every player-facing text goes through a TextObject string id, and translators need the ids with their
/// English in one file. <c>module\ModuleData\Languages\std_SmartSteward.xml</c> is that file, in the game's own
/// strings format (the vanilla modules keep their English source as <c>Languages\std_*.xml</c>; a translation is a
/// copy in <c>Languages\XX\</c> named by that folder's <c>language_data.xml</c>). These tests hold the file to the
/// code: every <c>UiText</c> call and <c>{=ss_…}</c> literal of the Module, and the ids the Module builds from the
/// settings registry (MCM and the Instructions tab: <c>ss_set_</c>, <c>ss_hint_</c>, <c>ss_opt_</c>,
/// <c>ss_grp_</c>). After changing a text, regenerate the file: set <c>SS_WRITE_STRINGS=1</c> and run
/// <c>dotnet test --filter StringsFileTests</c> (PLAN step 9).
/// </summary>
public class StringsFileTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string StringsPath =
        Path.Combine(RepoRoot, "module", "ModuleData", "Languages", "std_SmartSteward.xml");
    private static readonly string ModuleSource = Path.Combine(RepoRoot, "src", "SmartSteward.Module");

    /// <summary>The id families the Module builds at run time from the registry — nothing else may be built.</summary>
    private static readonly string[] DynamicFamilies = { "ss_set_", "ss_hint_", "ss_opt_", "ss_grp_" };

    private const string Literal = "\"((?:[^\"\\\\]|\\\\.)*)\"";

    // UiText.S("ss_x", "English" …  (the English must be ONE literal, then ',' or ')')
    private static readonly Regex UiTextCall = new Regex(
        @"UiText\.(?:S|S1|S2|S3|S4|T)\(\s*""(ss_[A-Za-z0-9_]+)""\s*,\s*" + Literal + @"\s*[,)]", RegexOptions.Compiled);

    // Any UiText call whose id is a whole literal (not "ss_family_" + …) — each must match UiTextCall.
    private static readonly Regex UiTextAnyLiteralId = new Regex(@"UiText\.(?:S|S1|S2|S3|S4|T)\(\s*""(ss_[A-Za-z0-9_]+)""(?!\s*\+)",
        RegexOptions.Compiled);

    // Any other place an id is built the same way (McmBridge.Text("ss_set_" + …)).
    private static readonly Regex BuiltId = new Regex(@"""(ss_[a-z]+_)""\s*\+", RegexOptions.Compiled);

    // "{=ss_x}English" — a TextObject literal (the menu entry, the loaded message, MCM's title).
    private static readonly Regex TextObjectLiteral = new Regex(@"""\{=(ss_[A-Za-z0-9_]+)\}((?:[^""\\]|\\.)*)""",
        RegexOptions.Compiled);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SmartSteward.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("SmartSteward.sln not found above " + AppContext.BaseDirectory);
    }

    private static string Unescape(string csharp)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < csharp.Length; i++)
        {
            char c = csharp[i];
            if (c != '\\') { sb.Append(c); continue; }
            char next = csharp[++i];
            if (next == '"' || next == '\\') sb.Append(next);
            else throw new InvalidOperationException("unexpected escape \\" + next + " in a player-facing text: " + csharp);
        }
        return sb.ToString();
    }

    /// <summary>Every id the code uses, with its English; throws on an id used with two different texts.</summary>
    internal static SortedDictionary<string, string> ExpectedStrings(List<string>? problems = null)
    {
        problems ??= new List<string>();
        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        void Add(string id, string english, string where)
        {
            if (map.TryGetValue(id, out var known))
            {
                if (known != english)
                    problems.Add(id + " has two texts: \"" + known + "\" and \"" + english + "\" (" + where + ")");
                return;
            }
            if (string.IsNullOrWhiteSpace(english))
                problems.Add(id + " has no English (" + where + ")");
            map[id] = english;
        }

        // 1. The Module's literal texts.
        foreach (var file in Directory.GetFiles(ModuleSource, "*.cs", SearchOption.AllDirectories)
                     .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                                 && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            string name = Path.GetFileName(file);
            // Doc comments may quote ids as examples; only code counts.
            string code = string.Join("\n", File.ReadAllLines(file).Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal)));

            var full = UiTextCall.Matches(code).Select(m => m.Index).ToHashSet();
            foreach (Match m in UiTextCall.Matches(code))
                Add(m.Groups[1].Value, Unescape(m.Groups[2].Value), name);
            foreach (Match m in UiTextAnyLiteralId.Matches(code))
                if (!full.Contains(m.Index))
                    problems.Add(name + ": " + m.Groups[1].Value + " - write its English as ONE string literal");
            foreach (Match m in BuiltId.Matches(code))
                if (!DynamicFamilies.Contains(m.Groups[1].Value))
                    problems.Add(name + ": an id built as \"" + m.Groups[1].Value + "\" + … - not a known family");
            foreach (Match m in TextObjectLiteral.Matches(code))
            {
                string english = Unescape(m.Groups[2].Value);
                if (english.Length > 0)
                    Add(m.Groups[1].Value, english, name);
                else if (m.Groups[1].Value != "ss_mcm_title")
                    problems.Add(name + ": \"{=" + m.Groups[1].Value + "}\" + … - write its English in the literal");
            }
        }

        // 2. The ids built from the settings registry (MCM, the Instructions and Prices tabs).
        Add("ss_mcm_title", ModInfo.Name, "McmBridge");
        foreach (var def in SettingsRegistry.All)
        {
            if (def.IsScalar)
            {
                Add("ss_set_" + def.Key, def.Label, "registry");
                Add("ss_hint_" + def.Key, def.Hint, "registry");
            }
            if (def is EnumSetting e)
                for (int i = 0; i < e.Names.Count; i++)
                    Add("ss_opt_" + def.Key + "_" + e.Names[i], e.Labels[i], "registry");
        }
        foreach (var group in SettingsRegistry.All.Select(d => d.Group).Distinct())
            Add("ss_grp_" + group.Replace(" ", ""), SettingsRegistry.GroupLabel(group), "registry");
        return map;
    }

    internal static string Render(SortedDictionary<string, string> strings)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n");
        sb.Append("<!--\n");
        sb.Append("  Smart Steward - every player-facing text by its string id, in English: the SOURCE for translations.\n");
        sb.Append("  The game reads English from the code itself ({=id}English); this file is what a translator starts from.\n");
        sb.Append("\n");
        sb.Append("  To translate (the vanilla way): copy this file to Languages\\XX\\std_SmartSteward_xx.xml, set the tag below to\n");
        sb.Append("  the game's language id (e.g. \"Deutsch\" - see Modules\\Native\\ModuleData\\Languages\\XX\\language_data.xml),\n");
        sb.Append("  translate every text=\"...\" (keep {VARIABLES} as they are), and add Languages\\XX\\language_data.xml:\n");
        sb.Append("    <LanguageData id=\"Deutsch\">\n");
        sb.Append("      <LanguageFile xml_path=\"XX/std_SmartSteward_xx.xml\" />\n");
        sb.Append("    </LanguageData>\n");
        sb.Append("\n");
        sb.Append("  Generated from the code by tests\\SmartSteward.Core.Tests\\StringsFileTests.cs (SS_WRITE_STRINGS=1) - do not\n");
        sb.Append("  edit the English here: change it in the code and regenerate.\n");
        sb.Append("-->\n");
        sb.Append("<base xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" type=\"string\">\n");
        sb.Append("  <tags>\n");
        sb.Append("    <tag language=\"English\" />\n");
        sb.Append("  </tags>\n");
        sb.Append("  <strings>\n");
        foreach (var pair in strings)
            sb.Append("    <string id=\"").Append(pair.Key).Append("\" text=\"").Append(XmlAttribute(pair.Value)).Append("\" />\n");
        sb.Append("  </strings>\n");
        sb.Append("</base>\n");
        return sb.ToString();
    }

    private static string XmlAttribute(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    [Fact]
    public void Every_text_of_the_code_has_one_id_and_one_english()
    {
        var problems = new List<string>();
        var strings = ExpectedStrings(problems);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
        Assert.True(strings.Count > 150, "only " + strings.Count + " strings found - is the scan still reading the Module?");
        Assert.All(strings.Keys, id => Assert.Matches(new Regex("^ss_[A-Za-z0-9_]+$"), id));
    }

    [Fact]
    public void The_strings_file_holds_exactly_the_codes_texts()
    {
        var expected = ExpectedStrings();
        string rendered = Render(expected);
        if (Environment.GetEnvironmentVariable("SS_WRITE_STRINGS") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StringsPath)!);
            File.WriteAllText(StringsPath, rendered, new UTF8Encoding(false));
        }
        Assert.True(File.Exists(StringsPath), StringsPath + " is missing - run with SS_WRITE_STRINGS=1");

        // Read it the way the game does (an XML document): ids, texts, the English tag.
        var doc = XDocument.Load(StringsPath);
        Assert.Equal("base", doc.Root!.Name.LocalName);
        Assert.Equal("English", doc.Root.Element("tags")?.Element("tag")?.Attribute("language")?.Value);
        var inFile = doc.Root.Element("strings")!.Elements("string")
            .Select(e => (Id: e.Attribute("id")!.Value, Text: e.Attribute("text")!.Value)).ToList();
        Assert.Equal(inFile.Count, inFile.Select(s => s.Id).Distinct().Count());

        var missing = expected.Keys.Except(inFile.Select(s => s.Id)).ToList();
        var extra = inFile.Select(s => s.Id).Except(expected.Keys).ToList();
        var changed = inFile.Where(s => expected.TryGetValue(s.Id, out var e) && e != s.Text).Select(s => s.Id).ToList();
        Assert.True(missing.Count + extra.Count + changed.Count == 0,
            "std_SmartSteward.xml is out of date - regenerate with SS_WRITE_STRINGS=1. Missing: " + string.Join(", ", missing)
            + "; not in the code: " + string.Join(", ", extra) + "; English changed: " + string.Join(", ", changed));
        Assert.Equal(rendered, File.ReadAllText(StringsPath).Replace("\r\n", "\n"));
    }

    [Fact]
    public void No_language_folder_is_shipped_half_done()
    {
        // A translation folder must name its file in language_data.xml, or the game never reads it.
        var languages = Path.GetDirectoryName(StringsPath)!;
        if (!Directory.Exists(languages))
            return;
        foreach (var folder in Directory.GetDirectories(languages))
        {
            var data = Path.Combine(folder, "language_data.xml");
            Assert.True(File.Exists(data), folder + " has no language_data.xml");
            var files = XDocument.Load(data).Root!.Elements("LanguageFile").Select(e => e.Attribute("xml_path")!.Value).ToList();
            Assert.NotEmpty(files);
            foreach (var path in files)
                Assert.True(File.Exists(Path.Combine(languages, path)), data + " names a missing " + path);
        }
    }
}
