using TaleWorlds.Localization;

namespace SmartSteward.UI
{
    /// <summary>
    /// Every player-facing text of the mod goes through a <see cref="TextObject"/> with a string id
    /// (<c>{=ss_…}English</c>), so translators can replace it: the ids and their English live in
    /// <c>module\ModuleData\Languages\std_SmartSteward.xml</c> (the vanilla strings format), and the Core test
    /// <c>StringsFileTests</c> holds that file to the code. Keep the English ONE string literal per call (no
    /// concatenation) so the test can read it; numbers are formatted by Core's <c>UiFormat</c> and put in as
    /// variables.
    /// </summary>
    internal static class UiText
    {
        public static TextObject T(string id, string english) => new TextObject("{=" + id + "}" + english);

        public static string S(string id, string english) => T(id, english).ToString();

        /// <summary>A text with one variable: <c>S1("ss_ui_x", "Food {N}", "N", "12")</c>.</summary>
        public static string S1(string id, string english, string name, string value) =>
            T(id, english).SetTextVariable(name, value).ToString();

        public static string S2(string id, string english, string name1, string value1, string name2, string value2) =>
            T(id, english).SetTextVariable(name1, value1).SetTextVariable(name2, value2).ToString();

        public static string S3(string id, string english, string name1, string value1, string name2, string value2,
            string name3, string value3) =>
            T(id, english).SetTextVariable(name1, value1).SetTextVariable(name2, value2).SetTextVariable(name3, value3)
                .ToString();

        public static string S4(string id, string english, string name1, string value1, string name2, string value2,
            string name3, string value3, string name4, string value4) =>
            T(id, english).SetTextVariable(name1, value1).SetTextVariable(name2, value2).SetTextVariable(name3, value3)
                .SetTextVariable(name4, value4).ToString();
    }
}
