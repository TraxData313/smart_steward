using TaleWorlds.Localization;

namespace SmartSteward.UI
{
    /// <summary>
    /// Every player-facing text of the Party Steward window goes through a <see cref="TextObject"/> with a string id
    /// (<c>{=ss_ui_…}English</c>), so translators can replace it later (PLAN step 9 gathers the ids into a strings
    /// file). Numbers are formatted by Core's <c>UiFormat</c> and put in as variables.
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
    }
}
