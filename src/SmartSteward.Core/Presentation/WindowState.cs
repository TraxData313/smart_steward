using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Presentation
{
    /// <summary>
    /// What the Party Steward window remembers between visits and game restarts (PLAN step 18 — Anton 2026.09.28: a folded
    /// section "stays collapsed until I expand it"): which Suggestion-tab sections are folded. Kept in its own small file
    /// beside the settings — <c>Configs\SmartSteward\window_state.json</c> (<see cref="ModInfo.WindowStateFileName"/>) —,
    /// never in the save and never in MCM.
    /// </summary>
    /// <remarks>
    /// Why its own file and not a key in settings.json [decided: Claude, 2026.09.28 — step 18]: settings.json is Anton's
    /// settings — generated from the registry, every key a §7 parameter with a range, a change raises the settings service's
    /// <c>Changed</c> (the window re-plans on it) and a key it does not know is logged and backed up as a problem. A fold is
    /// not a setting: it must not re-plan, must not show up in MCM or the Instructions tab, and writing it on every click
    /// must never touch (or risk) the settings file. Deleting settings.json to reset the settings keeps the folds; deleting
    /// this file unfolds everything.
    /// <para>Reading never fails: no file, an empty or broken one → every section unfolded (the default); unknown names are
    /// dropped. <see cref="Generate"/> writes plain ASCII with comments, CRLF, in the table's order — deterministic.</para>
    /// </remarks>
    public sealed class WindowState
    {
        /// <summary>The one key of the file.</summary>
        public const string CollapsedKey = "CollapsedSections";

        private readonly HashSet<SectionGroup> _collapsed = new HashSet<SectionGroup>();

        /// <summary>The folded sections, in the table's order.</summary>
        public IReadOnlyList<SectionGroup> Collapsed => SectionGroups.All.Where(_collapsed.Contains).ToList();

        public bool IsCollapsed(SectionGroup group) => _collapsed.Contains(group);

        /// <summary>Folds or unfolds a section; true when that changed anything (then the file should be written).</summary>
        public bool SetCollapsed(SectionGroup group, bool collapsed) =>
            collapsed ? _collapsed.Add(group) : _collapsed.Remove(group);

        /// <summary>
        /// The state in a file's text. Null or blank = no file yet (all unfolded, no problem); anything it cannot read =
        /// all unfolded and <paramref name="problem"/> says why (for the log); a name it does not know is dropped and named
        /// there too. Never throws.
        /// </summary>
        public static WindowState Parse(string? text, out string? problem)
        {
            problem = null;
            var state = new WindowState();
            if (string.IsNullOrWhiteSpace(text))
                return state;
            try
            {
                var root = ReadObject(text!);
                JToken? list = null;
                foreach (var property in root.Properties())
                    if (string.Equals(property.Name, CollapsedKey, StringComparison.OrdinalIgnoreCase))
                        list = property.Value;
                if (list == null)
                    return state;
                if (!(list is JArray array))
                {
                    problem = CollapsedKey + " is not a list [ ... ] - every section unfolded";
                    return state;
                }
                var unknown = new List<string>();
                foreach (var item in array)
                {
                    string name = item.Type == JTokenType.String ? (string)item! : item.ToString(Formatting.None);
                    if (SectionGroups.TryParse(name, out var group))
                        state._collapsed.Add(group);
                    else
                        unknown.Add(name);
                }
                if (unknown.Count > 0)
                    problem = "unknown section name(s) ignored: " + string.Join(", ", unknown);
                return state;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is InvalidCastException)
            {
                problem = "unreadable (" + ex.Message + ") - every section unfolded";
                return new WindowState();
            }
        }

        /// <summary>The file's text: a short header, then the one key with its comment.</summary>
        public string Generate()
        {
            const string nl = SettingsFile.NewLine;
            var sb = new StringBuilder();
            sb.Append("// Smart Steward - what the Party Steward window remembers between visits and game restarts.").Append(nl);
            sb.Append("// Not a setting (those are in settings.json): the window writes this file whenever you fold or").Append(nl);
            sb.Append("// unfold a section of the Suggestion tab. Delete it to unfold every section. Never stored in a save.").Append(nl);
            sb.Append('{').Append(nl);
            sb.Append("  // The Suggestion tab's sections shown folded to one summary line. Names: ")
                .Append(string.Join(", ", SectionGroups.All.Select(SectionGroups.Key))).Append('.').Append(nl);
            sb.Append("  \"").Append(CollapsedKey).Append("\": [")
                .Append(string.Join(", ", Collapsed.Select(g => "\"" + SectionGroups.Key(g) + "\"")))
                .Append(']').Append(nl);
            sb.Append('}').Append(nl);
            return sb.ToString();
        }

        private static JObject ReadObject(string text)
        {
            using (var reader = new JsonTextReader(new StringReader(text)))
            {
                reader.DateParseHandling = DateParseHandling.None;
                var load = new JsonLoadSettings { CommentHandling = CommentHandling.Ignore };
                var token = JToken.ReadFrom(reader, load);
                while (token.Type == JTokenType.Comment) token = JToken.ReadFrom(reader, load);
                if (!(token is JObject root))
                    throw new FormatException("the file must be one { ... } object");
                return root;
            }
        }
    }
}
