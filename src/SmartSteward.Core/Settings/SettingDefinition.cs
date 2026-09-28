using System;
using System.Collections.Generic;
using System.Globalization;

namespace SmartSteward.Core.Settings
{
    /// <summary>What a setting holds — decides the control (MCM, Instructions tab) and the file syntax.</summary>
    public enum SettingKind
    {
        Bool,
        Int,
        Float,
        Enum,

        /// <summary>The price book (DESIGN §1.3): item id → the player's overrides. File + Prices tab only.</summary>
        PriceBook,
    }

    /// <summary>
    /// One setting of DESIGN §7 as the three views see it — the settings file, MCM and the Instructions
    /// tab all walk <see cref="SettingsRegistry.All"/>, so they can never drift apart. The value itself
    /// lives in <see cref="StewardSettings"/>; a definition only knows how to read and write it there.
    /// The DEFAULT is read from a fresh <see cref="StewardSettings"/>, so the POCO's initialisers stay the
    /// one place a default is written (a test holds both to the §7 table).
    /// </summary>
    public abstract class SettingDefinition
    {
        protected SettingDefinition(string key, SettingKind kind, string group, string label, string help)
        {
            Key = key;
            Kind = kind;
            Group = group;
            Label = label;
            Help = help;
        }

        /// <summary>The §7 key — the file's key, the StewardSettings property, MCM's property id.</summary>
        public string Key { get; }

        public SettingKind Kind { get; }

        /// <summary>The §7 group (General, Money, Food…) — MCM groups and file sections follow it.</summary>
        public string Group { get; }

        /// <summary>Short UI label (MCM, Instructions tab).</summary>
        public string Label { get; }

        /// <summary>What the setting does, for players — one or two plain sentences. The default and the
        /// range are added by <see cref="Hint"/> and the file's comment, never written into this text.</summary>
        public string Help { get; }

        /// <summary>A plain value with its own control (checkbox, number, dropdown). The price book is edited in the
        /// Party Steward window's Prices tab and is never in MCM.</summary>
        public bool IsScalar => Kind != SettingKind.PriceBook;

        /// <summary>The current value, boxed (bool, int, double, the enum or the dictionary).</summary>
        public abstract object GetValue(StewardSettings settings);

        /// <summary>The value a fresh <see cref="StewardSettings"/> holds.</summary>
        public object DefaultValue => GetValue(new StewardSettings());

        /// <summary>The default as the settings file writes it: <c>true</c>, <c>1000</c>, <c>2.0</c>, <c>"Balanced"</c>.</summary>
        public abstract string DefaultFileText { get; }

        /// <summary>The allowed values as the settings file's comment states them.</summary>
        public abstract string RangeFileText { get; }

        /// <summary>The default as a player reads it in a tooltip: on/off, 1,000, the enum's label.</summary>
        public abstract string DefaultUiText { get; }

        /// <summary>The range for a tooltip — null where the control shows it (checkbox, dropdown).</summary>
        public virtual string? RangeUiText => null;

        /// <summary>The tooltip (MCM hint, Instructions tab): the help, then the default and the range.</summary>
        public string Hint =>
            Help.Replace("\n", " ") + " Default: " + DefaultUiText + "." + (RangeUiText == null ? "" : " Range: " + RangeUiText + ".");

        internal static string FormatInt(long value) => value.ToString(CultureInfo.InvariantCulture);

        internal static string FormatIntUi(long value) => value.ToString("#,0", CultureInfo.InvariantCulture);
    }

    public sealed class BoolSetting : SettingDefinition
    {
        private readonly Func<StewardSettings, bool> _get;
        private readonly Action<StewardSettings, bool> _set;

        public BoolSetting(string key, string group, string label, string help,
            Func<StewardSettings, bool> get, Action<StewardSettings, bool> set)
            : base(key, SettingKind.Bool, group, label, help)
        {
            _get = get;
            _set = set;
        }

        public bool Default => _get(new StewardSettings());

        public bool Get(StewardSettings settings) => _get(settings);

        public void Set(StewardSettings settings, bool value) => _set(settings, value);

        public override object GetValue(StewardSettings settings) => Get(settings);

        public override string DefaultFileText => Default ? "true" : "false";

        public override string RangeFileText => "true or false";

        public override string DefaultUiText => Default ? "on" : "off";
    }

    public sealed class IntSetting : SettingDefinition
    {
        private readonly Func<StewardSettings, int> _get;
        private readonly Action<StewardSettings, int> _set;

        public IntSetting(string key, string group, string label, string help, int min, int max,
            Func<StewardSettings, int> get, Action<StewardSettings, int> set)
            : base(key, SettingKind.Int, group, label, help)
        {
            Min = min;
            Max = max;
            _get = get;
            _set = set;
        }

        public int Min { get; }

        public int Max { get; }

        public int Default => _get(new StewardSettings());

        public int Get(StewardSettings settings) => _get(settings);

        /// <summary>Writes the value clamped into [Min, Max].</summary>
        public void Set(StewardSettings settings, int value) => _set(settings, Clamp(value));

        public int Clamp(long value) => (int)Math.Max(Min, Math.Min(Max, value));

        public override object GetValue(StewardSettings settings) => Get(settings);

        public override string DefaultFileText => FormatInt(Default);

        public override string RangeFileText => FormatInt(Min) + " to " + FormatInt(Max);

        public override string DefaultUiText => FormatIntUi(Default);

        public override string RangeUiText => FormatIntUi(Min) + " to " + FormatIntUi(Max);
    }

    public sealed class FloatSetting : SettingDefinition
    {
        private readonly Func<StewardSettings, double> _get;
        private readonly Action<StewardSettings, double> _set;

        public FloatSetting(string key, string group, string label, string help, double min, double max, int decimals,
            Func<StewardSettings, double> get, Action<StewardSettings, double> set)
            : base(key, SettingKind.Float, group, label, help)
        {
            Min = min;
            Max = max;
            Decimals = decimals;
            _get = get;
            _set = set;
        }

        public double Min { get; }

        public double Max { get; }

        /// <summary>Values are kept rounded to this many decimals — so MCM's float slider (1.2f is
        /// 1.2000000476…) and the file always agree on the same number.</summary>
        public int Decimals { get; }

        public double Default => _get(new StewardSettings());

        public double Get(StewardSettings settings) => _get(settings);

        /// <summary>Writes the value clamped into [Min, Max] and rounded to <see cref="Decimals"/>; a NaN
        /// or infinity writes the default.</summary>
        public void Set(StewardSettings settings, double value) => _set(settings, Normalize(value));

        public double Normalize(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) value = Default;
            value = Math.Max(Min, Math.Min(Max, value));
            return Math.Round(value, Decimals, MidpointRounding.AwayFromZero);
        }

        /// <summary>The file's spelling: always a decimal point, at most <see cref="Decimals"/> places
        /// (<c>2.0</c>, <c>1.2</c>, <c>0.85</c>).</summary>
        public string Format(double value) =>
            Math.Round(value, Decimals, MidpointRounding.AwayFromZero)
                .ToString("0.0" + new string('#', Math.Max(0, Decimals - 1)), CultureInfo.InvariantCulture);

        public override object GetValue(StewardSettings settings) => Get(settings);

        public override string DefaultFileText => Format(Default);

        public override string RangeFileText => Format(Min) + " to " + Format(Max);

        public override string DefaultUiText => Format(Default);

        public override string RangeUiText => RangeFileText;
    }

    /// <summary>An enum setting, seen without its type parameter (the file and MCM work on names and
    /// indexes; MCM shows a dropdown of <see cref="Labels"/>).</summary>
    public abstract class EnumSetting : SettingDefinition
    {
        protected EnumSetting(string key, string group, string label, string help, IReadOnlyList<string> names,
            IReadOnlyList<string> labels)
            : base(key, SettingKind.Enum, group, label, help)
        {
            if (names.Count != labels.Count) throw new ArgumentException("one label per enum value", nameof(labels));
            Names = names;
            Labels = labels;
        }

        /// <summary>The enum's value names, in declaration order — the file's spelling.</summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary>The dropdown's text for each name, same order.</summary>
        public IReadOnlyList<string> Labels { get; }

        public abstract int DefaultIndex { get; }

        public abstract int GetIndex(StewardSettings settings);

        /// <summary>Writes the value at <paramref name="index"/>; an index out of range writes the default.</summary>
        public abstract void SetIndex(StewardSettings settings, int index);

        /// <summary>The index of <paramref name="name"/>, ignoring case; -1 when it is none of <see cref="Names"/>.</summary>
        public int IndexOf(string name)
        {
            for (int i = 0; i < Names.Count; i++)
                if (string.Equals(Names[i], name.Trim(), StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

        public override string DefaultFileText => "\"" + Names[DefaultIndex] + "\"";

        public override string RangeFileText
        {
            get
            {
                var quoted = new List<string>();
                foreach (var name in Names) quoted.Add("\"" + name + "\"");
                return "one of " + string.Join(", ", quoted);
            }
        }

        public override string DefaultUiText => Labels[DefaultIndex];
    }

    public sealed class EnumSetting<TEnum> : EnumSetting where TEnum : struct, Enum
    {
        private static readonly TEnum[] Values = (TEnum[])Enum.GetValues(typeof(TEnum));
        private readonly Func<StewardSettings, TEnum> _get;
        private readonly Action<StewardSettings, TEnum> _set;

        public EnumSetting(string key, string group, string label, string help, IReadOnlyList<string> labels,
            Func<StewardSettings, TEnum> get, Action<StewardSettings, TEnum> set)
            : base(key, group, label, help, Enum.GetNames(typeof(TEnum)), labels)
        {
            _get = get;
            _set = set;
        }

        public TEnum Get(StewardSettings settings) => _get(settings);

        public void Set(StewardSettings settings, TEnum value) => _set(settings, value);

        public override int DefaultIndex => Array.IndexOf(Values, _get(new StewardSettings()));

        public override int GetIndex(StewardSettings settings) => Math.Max(0, Array.IndexOf(Values, _get(settings)));

        public override void SetIndex(StewardSettings settings, int index) =>
            _set(settings, Values[index >= 0 && index < Values.Length ? index : DefaultIndex]);

        public override object GetValue(StewardSettings settings) => Get(settings);
    }

    /// <summary>The price book (DESIGN §1.3) — only the player's overrides, keyed by item id.</summary>
    public sealed class PriceBookSetting : SettingDefinition
    {
        /// <summary>A typed base price is kept within 0 … this.</summary>
        public const int MaxBase = 1_000_000;

        public PriceBookSetting(string key, string group, string label, string help)
            : base(key, SettingKind.PriceBook, group, label, help)
        {
        }

        public Dictionary<string, PriceBookEntry> Get(StewardSettings settings) =>
            settings.PriceBook ??= new Dictionary<string, PriceBookEntry>(StringComparer.Ordinal);

        public override object GetValue(StewardSettings settings) => Get(settings);

        public override string DefaultFileText => "{}";

        public override string RangeFileText =>
            "per item id, Buy and Sell true or false, BuyBase and SellBase 0 to " + FormatInt(MaxBase);

        public override string DefaultUiText => "empty";
    }
}
