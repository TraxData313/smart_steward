using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Presentation
{
    /// <summary>
    /// The Instructions tab's pure half (DESIGN §1.2): every scalar setting of the registry, grouped as §7, and how
    /// a typed value becomes a setting. Values go through <see cref="SettingsService.Set"/>, which clamps them into
    /// the registry's range and saves; this class only reads the player's text.
    /// </summary>
    public static class SettingEdit
    {
        /// <summary>The groups in §7 order with their scalar settings in registry order — every scalar exactly once.
        /// (The price book lives in the Prices tab.)</summary>
        public static IReadOnlyList<KeyValuePair<string, IReadOnlyList<SettingDefinition>>> Groups()
        {
            var groups = new List<KeyValuePair<string, IReadOnlyList<SettingDefinition>>>();
            foreach (var group in SettingsRegistry.Groups)
            {
                var defs = SettingsRegistry.InGroup(group).Where(d => d.IsScalar).ToList();
                if (defs.Count > 0)
                    groups.Add(new KeyValuePair<string, IReadOnlyList<SettingDefinition>>(group, defs));
            }
            return groups;
        }

        /// <summary>A number setting as its box shows it: <c>1000</c>, <c>-1</c>, <c>1.2</c>, <c>2</c>.</summary>
        public static string Text(SettingDefinition def, StewardSettings settings)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            switch (def)
            {
                case IntSetting i: return i.Get(settings).ToString(System.Globalization.CultureInfo.InvariantCulture);
                case FloatSetting f: return UiFormat.Decimal(f.Get(settings), f.Decimals);
                default: return "";
            }
        }

        /// <summary>The range beside a number box: <c>0–1,000,000</c>, <c>0.1–10</c>; "" for other kinds.</summary>
        public static string Range(SettingDefinition def)
        {
            switch (def)
            {
                case IntSetting i: return UiFormat.Money(i.Min) + UiFormat.RangeDash + UiFormat.Money(i.Max);
                case FloatSetting f: return UiFormat.Decimal(f.Min, f.Decimals) + UiFormat.RangeDash + UiFormat.Decimal(f.Max, f.Decimals);
                default: return "";
            }
        }

        /// <summary>A typed value for a number setting: a whole number for an int setting, a decimal (either
        /// separator) for a float one. False when it is not a number — nothing is saved. Out-of-range values read
        /// fine; the service clamps them.</summary>
        public static bool TryRead(SettingDefinition def, string? text, out object value)
        {
            value = 0;
            switch (def)
            {
                case IntSetting _:
                    if (!UiFormat.TryParseWhole(text, out long whole))
                        return false;
                    value = whole;
                    return true;
                case FloatSetting _:
                    if (!UiFormat.TryParseDecimal(text, out double d))
                        return false;
                    value = d;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>The next value of an enum setting (a click on its button cycles).</summary>
        public static int NextIndex(EnumSetting def, StewardSettings settings) =>
            (def.GetIndex(settings) + 1) % Math.Max(1, def.Names.Count);
    }
}
