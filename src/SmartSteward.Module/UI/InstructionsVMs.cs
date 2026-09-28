using SmartSteward.Adapter;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using TaleWorlds.Library;

namespace SmartSteward.UI
{
    /// <summary>
    /// The Instructions tab (DESIGN §1.2): every setting of the registry grouped as §7 — a checkbox, a number box
    /// (with its range) or a button that cycles an enum — with live notes after two kinds of box: the food goal's per-soul
    /// figure, and the horses for this party beside "Horses per 100 footmen" and "War horses to keep" (step 17; the old
    /// "troops ready to upgrade" line went with the upgrade counting, the "Prisoners to ransom" tick-list in step 12).
    /// Labels and tooltips are MCM's (the same <c>ss_set_</c> / <c>ss_hint_</c> ids). Every change goes through the
    /// settings service at once (clamped, saved, MCM shows the same value); the plan is re-made when the Suggestion
    /// tab shows again.
    /// </summary>
    public sealed class InstructionsTabVM : ViewModel
    {
        private MBBindingList<SettingGroupVM> _groups = new MBBindingList<SettingGroupVM>();
        private GameVisit? _visit;

        internal InstructionsTabVM()
        {
            // Round 4 (Anton 2026.09.28): the clicks' hint lives here now - "the players will see it once there and not cram
            // space anymore in the working tab".
            ShortcutText = UiText.S("ss_ui_shortcuts", "Click ±1  ·  Shift ±5  ·  Ctrl all  ·  names in gold open the Encyclopedia");
            IntroText = UiText.S("ss_ui_instructions_intro",
                "Your standing orders to the steward. Changes are saved at once - to settings.json, and Mod Options shows them too. Hover a name for what it does.");
        }

        /// <summary>Builds the groups (again after Do it: the party — its footmen, its food rate — may have changed).</summary>
        internal void EnsureBuilt(GameVisit visit)
        {
            if (_visit == visit && _groups.Count > 0)
                return;
            _visit = visit;
            var groups = new MBBindingList<SettingGroupVM>();
            foreach (var group in SettingEdit.Groups())
            {
                var vm = new SettingGroupVM(UiText.S("ss_grp_" + group.Key.Replace(" ", ""), SettingsRegistry.GroupLabel(group.Key)));
                foreach (var def in group.Value)
                {
                    // The food goal in days shows what it means per man for this party, live (Anton 2026.09.28); the horses
                    // per 100 footmen and the war horses to keep show the horses they mean for it (step 17).
                    System.Func<StewardSettings, string>? note = null;
                    if (def.Key == nameof(StewardSettings.FoodDays))
                        note = settings => FoodDaysNote(def, settings, visit);
                    else if (def.Key == nameof(StewardSettings.MountsPer100Footmen) || def.Key == nameof(StewardSettings.WarMountsToKeep))
                        note = settings => MountsNote(settings, visit);
                    vm.Settings.Add(new SettingLineVM(def, this, note));
                }
                groups.Add(vm);
            }
            Groups = groups;
        }

        /// <summary>After the box: "(110 horses = 100 riding + 10 war, for 100 footmen)" — what the two numbers mean for this
        /// party's footmen right now (before the deal), <see cref="MountGoal"/> (Anton 2026.09.28, step 17). The range stays in
        /// the tooltip: the note alone fills the line's room (~400 px at the notes' font).</summary>
        private static string MountsNote(StewardSettings settings, GameVisit visit)
        {
            int footmen = System.Math.Max(0, visit.Snapshot.Party?.Footmen ?? 0);
            int war = MountGoal.War(settings);
            int riding = MountGoal.Riding(settings, footmen);
            return UiText.S4("ss_ui_mounts_note", "({TOTAL} horses = {RIDING} riding + {WAR} war, for {FOOTMEN} footmen)",
                       "TOTAL", UiFormat.Money(riding + war), "RIDING", UiFormat.Money(riding), "WAR", UiFormat.Money(war),
                       "FOOTMEN", UiFormat.Money(footmen));
        }

        /// <summary>After the days box: "days (~2.0 per soul)  ·  1–365" — the food units the goal keeps per man for this
        /// party at the game's own rate, perks included (<see cref="FoodGoal.PerSoul"/>).</summary>
        private static string FoodDaysNote(SettingDefinition def, StewardSettings settings, GameVisit visit) =>
            UiText.S2("ss_ui_food_days_note", "days (~{PER} per soul)  ·  {RANGE}",
                "PER", UiFormat.Decimal(FoodGoal.PerSoul(settings, visit.Snapshot), 1), "RANGE", SettingEdit.Range(def));

        /// <summary>Every line reads the settings again; <paramref name="includeTexts"/> also rewrites the number
        /// boxes (when the tab is shown — never while the player may be typing in one).</summary>
        internal void RefreshAll(bool includeTexts)
        {
            var settings = SettingsHost.Current;
            foreach (var group in Groups)
                foreach (var line in group.Settings)
                    line.Refresh(settings, includeTexts);
        }

        // ── edits ──────────────────────────────────────────────────────────────────────────────────────

        internal void Toggle(SettingLineVM line)
        {
            if (line.Definition is BoolSetting b)
            {
                SettingsHost.Service.Set(b, !b.Get(SettingsHost.Current));
                ModLog.Info("window", "setting " + b.Key + " = " + b.Get(SettingsHost.Current));
            }
            line.Refresh(SettingsHost.Current, includeTexts: false);
        }

        internal void Cycle(SettingLineVM line)
        {
            if (line.Definition is EnumSetting e)
            {
                SettingsHost.Service.Set(e, SettingEdit.NextIndex(e, SettingsHost.Current));
                ModLog.Info("window", "setting " + e.Key + " = " + e.Names[e.GetIndex(SettingsHost.Current)]);
            }
            line.Refresh(SettingsHost.Current, includeTexts: false);
        }

        internal void Typed(SettingLineVM line, string text)
        {
            var def = line.Definition;
            if (def == null)
                return;
            if (!SettingEdit.TryRead(def, text, out object value))
            {
                line.SetValid(false);
                return;
            }
            line.SetValid(true);
            SettingsHost.Service.Set(def, value); // clamped into the registry's range, saved
        }

        [DataSourceProperty] public string IntroText { get; }

        /// <summary>The Suggestion tab's clicks, told once at the top (moved here in round 4).</summary>
        [DataSourceProperty] public string ShortcutText { get; }
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string HeadingColor => UiColors.Heading;

        [DataSourceProperty]
        public MBBindingList<SettingGroupVM> Groups
        {
            get => _groups;
            set { if (value != _groups) { _groups = value; OnPropertyChangedWithValue(value, nameof(Groups)); } }
        }
    }

    /// <summary>One §7 group of the Instructions tab.</summary>
    public sealed class SettingGroupVM : ViewModel
    {
        internal SettingGroupVM(string title)
        {
            TitleText = title;
            Settings = new MBBindingList<SettingLineVM>();
        }

        [DataSourceProperty] public string TitleText { get; }
        [DataSourceProperty] public string HeadingColor => UiColors.Heading;
        [DataSourceProperty] public MBBindingList<SettingLineVM> Settings { get; }
    }

    /// <summary>One line: a setting (checkbox / number box / enum button) or a small heading (the ready-to-upgrade
    /// count).</summary>
    public sealed class SettingLineVM : ViewModel
    {
        private readonly InstructionsTabVM? _tab;
        private readonly System.Func<StewardSettings, string>? _rangeOf;
        private string _rangeText = "";
        private bool _isOn;
        private string _valueText = "";
        private string _valueColor = UiColors.Text;
        private string _enumText = "";

        internal SettingLineVM(SettingDefinition def, InstructionsTabVM tab, System.Func<StewardSettings, string>? rangeOf = null)
        {
            Definition = def;
            _tab = tab;
            _rangeOf = rangeOf;
            LabelText = PricesTabVM.SettingLabel(def);
            Hint = new HintVM(PricesTabVM.SettingHint(def));
            IsBool = def.Kind == SettingKind.Bool;
            IsNumber = def.Kind == SettingKind.Int || def.Kind == SettingKind.Float;
            IsEnum = def.Kind == SettingKind.Enum;
            _rangeText = SettingEdit.Range(def);
            EnumHint = new HintVM(IsEnum ? UiText.S("ss_ui_enum_hint", "Click for the next choice") : "");
            Refresh(SettingsHost.Current, includeTexts: true);
        }

        private SettingLineVM(string label, string hint)
        {
            LabelText = label;
            Hint = new HintVM(hint);
            EnumHint = new HintVM();
            IsHeading = true;
        }

        internal static SettingLineVM Heading(string label, string hint) => new SettingLineVM(label, hint);

        internal SettingDefinition? Definition { get; }

        internal void Refresh(StewardSettings settings, bool includeTexts)
        {
            switch (Definition)
            {
                case BoolSetting b:
                    IsOn = b.Get(settings);
                    break;
                case EnumSetting e:
                    int index = e.GetIndex(settings);
                    EnumText = UiText.S("ss_opt_" + e.Key + "_" + e.Names[index], e.Labels[index]);
                    break;
                case IntSetting _:
                case FloatSetting _:
                    if (_rangeOf != null)
                        RangeText = _rangeOf(settings); // a live note after the box (the food goal's per-soul figure)
                    if (includeTexts)
                    {
                        string text = SettingEdit.Text(Definition, settings);
                        if (text != _valueText)
                        {
                            _valueText = text;
                            OnPropertyChangedWithValue(text, nameof(ValueText));
                        }
                        ValueColor = UiColors.Text;
                    }
                    break;
            }
        }

        internal void SetValid(bool valid) => ValueColor = valid ? UiColors.Text : UiColors.Warning;

        public void ExecuteToggle() => StewardWindowVM.Guard("setting toggle " + Definition?.Key, () => _tab?.Toggle(this));

        public void ExecuteCycle() => StewardWindowVM.Guard("setting cycle " + Definition?.Key, () => _tab?.Cycle(this));

        [DataSourceProperty] public string LabelText { get; }
        [DataSourceProperty] public HintVM Hint { get; }
        [DataSourceProperty] public HintVM EnumHint { get; }
        [DataSourceProperty] public bool IsBool { get; }
        [DataSourceProperty] public bool IsNumber { get; }
        [DataSourceProperty] public bool IsEnum { get; }
        [DataSourceProperty] public bool IsHeading { get; }
        [DataSourceProperty] public bool IsControlRow => !IsHeading;
        /// <summary>The muted words after a number box: its range — or, for the food goal, what it means per man now.</summary>
        [DataSourceProperty]
        public string RangeText
        {
            get => _rangeText;
            set { if (value != _rangeText) { _rangeText = value; OnPropertyChangedWithValue(value, nameof(RangeText)); } }
        }
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string HeadingColor => UiColors.Heading;

        [DataSourceProperty]
        public bool IsOn
        {
            get => _isOn;
            set { if (value != _isOn) { _isOn = value; OnPropertyChangedWithValue(value, nameof(IsOn)); } }
        }

        [DataSourceProperty]
        public string ValueText
        {
            get => _valueText;
            set
            {
                if (value == _valueText) return;
                _valueText = value ?? "";
                OnPropertyChangedWithValue(_valueText, nameof(ValueText));
                string text = _valueText;
                StewardWindowVM.Guard("setting " + Definition?.Key, () => _tab?.Typed(this, text));
            }
        }

        [DataSourceProperty]
        public string ValueColor
        {
            get => _valueColor;
            set { if (value != _valueColor) { _valueColor = value; OnPropertyChangedWithValue(value, nameof(ValueColor)); } }
        }

        [DataSourceProperty]
        public string EnumText
        {
            get => _enumText;
            set { if (value != _enumText) { _enumText = value; OnPropertyChangedWithValue(value, nameof(EnumText)); } }
        }
    }
}
