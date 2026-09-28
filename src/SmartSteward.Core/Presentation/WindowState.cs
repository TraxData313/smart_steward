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
    /// section "stays collapsed until I expand it"; step 21: every fold of the spreadsheet, <see cref="SheetFolds"/>): which
    /// parts of the Suggestion tab are folded. Kept in its own small file beside the settings —
    /// <c>Configs\SmartSteward\window_state.json</c> (<see cref="ModInfo.WindowStateFileName"/>) —, never in the save and never
    /// in MCM.
    /// </summary>
    /// <remarks>
    /// Why its own file and not a key in settings.json [decided: Claude, 2026.09.28 — step 18]: settings.json is Anton's
    /// settings — generated from the registry, every key a §7 parameter with a range, a change raises the settings service's
    /// <c>Changed</c> (the window re-plans on it) and a key it does not know is logged and backed up as a problem. A fold is
    /// not a setting: it must not re-plan, must not show up in MCM or the Instructions tab, and writing it on every click
    /// must never touch (or risk) the settings file. Deleting settings.json to reset the settings keeps the folds; deleting
    /// this file brings back the everyday view (<see cref="SheetFolds.FoldedByDefault"/>).
    /// <para>The key is <c>"Folded"</c> (step 21). A step-18/20 file has only <c>"CollapsedSections"</c> (the old sections):
    /// it is read once as the defaults with those sections folded and the rest of the old sections open; the next fold writes
    /// the new key. Reading never fails: no file, an empty or broken one → the defaults; unknown names are dropped.
    /// <see cref="Generate"/> writes plain ASCII with comments, CRLF, in the table's order — deterministic.</para>
    /// </remarks>
    public sealed class WindowState
    {
        /// <summary>The file's key (step 21).</summary>
        public const string FoldedKey = "Folded";

        /// <summary>The step-18/20 key — read, never written.</summary>
        public const string OldCollapsedKey = "CollapsedSections";

        private readonly HashSet<string> _folded = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>The everyday view: <see cref="SheetFolds.FoldedByDefault"/>.</summary>
        public WindowState()
        {
            foreach (var key in SheetFolds.FoldedByDefault)
                _folded.Add(key);
        }

        /// <summary>The folded parts, in the table's order.</summary>
        public IReadOnlyList<string> Folded => SheetFolds.All.Where(_folded.Contains).ToList();

        public bool IsFolded(string key) => key != null && _folded.Contains(key);

        /// <summary>Folds or unfolds a part; true when that changed anything (then the file should be written). Unknown keys
        /// are refused (false).</summary>
        public bool SetFolded(string key, bool folded)
        {
            string? known = SheetFolds.Find(key);
            if (known == null)
                return false;
            return folded ? _folded.Add(known) : _folded.Remove(known);
        }

        /// <summary>
        /// The state in a file's text. Null or blank = no file yet (the defaults, no problem); anything it cannot read = the
        /// defaults and <paramref name="problem"/> says why (for the log); a name it does not know is dropped and named there
        /// too. Never throws.
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
                JToken? list = null, old = null;
                foreach (var property in root.Properties())
                {
                    if (string.Equals(property.Name, FoldedKey, StringComparison.OrdinalIgnoreCase))
                        list = property.Value;
                    else if (string.Equals(property.Name, OldCollapsedKey, StringComparison.OrdinalIgnoreCase))
                        old = property.Value;
                }
                var unknown = new List<string>();
                if (list != null)
                {
                    if (!(list is JArray array))
                    {
                        problem = FoldedKey + " is not a list [ ... ] - the everyday view";
                        return new WindowState();
                    }
                    state._folded.Clear();
                    foreach (var name in Names(array))
                    {
                        string? key = SheetFolds.Find(name);
                        if (key != null) state._folded.Add(key);
                        else unknown.Add(name);
                    }
                }
                else if (old != null)
                {
                    if (!(old is JArray array))
                    {
                        problem = OldCollapsedKey + " is not a list [ ... ] - the everyday view";
                        return new WindowState();
                    }
                    // The step-18/20 file: its sections as they were (named = folded, the others open); the rest default.
                    foreach (var key in SheetFolds.OldSectionKeys)
                        state._folded.Remove(key);
                    foreach (var name in Names(array))
                    {
                        var keys = SheetFolds.FromOldSection(name);
                        if (keys == null) unknown.Add(name);
                        else foreach (var key in keys) state._folded.Add(key);
                    }
                }
                if (unknown.Count > 0)
                    problem = "unknown name(s) ignored: " + string.Join(", ", unknown);
                return state;
            }
            catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is InvalidCastException)
            {
                problem = "unreadable (" + ex.Message + ") - the everyday view";
                return new WindowState();
            }
        }

        private static IEnumerable<string> Names(JArray array) =>
            array.Select(item => item.Type == JTokenType.String ? (string)item! : item.ToString(Formatting.None));

        /// <summary>The file's text: a short header, then the one key with its comment.</summary>
        public string Generate()
        {
            const string nl = SettingsFile.NewLine;
            var sb = new StringBuilder();
            sb.Append("// Smart Steward - what the Party Steward window remembers between visits and game restarts.").Append(nl);
            sb.Append("// Not a setting (those are in settings.json): the window writes this file whenever you fold or").Append(nl);
            sb.Append("// unfold a part of the Suggestion tab. Delete it for the everyday view. Never stored in a save.").Append(nl);
            sb.Append('{').Append(nl);
            sb.Append("  // The folded parts of the Suggestion tab. Names: ").Append(string.Join(", ", SheetFolds.All)).Append('.')
                .Append(nl);
            sb.Append("  \"").Append(FoldedKey).Append("\": [")
                .Append(string.Join(", ", Folded.Select(k => "\"" + k + "\"")))
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
