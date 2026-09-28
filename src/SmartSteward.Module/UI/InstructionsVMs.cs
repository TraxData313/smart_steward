using System.Linq;
using SmartSteward.Adapter;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace SmartSteward.UI
{
    /// <summary>
    /// The Instructions tab (DESIGN §1.2): every setting of the registry grouped as §7 — a checkbox, a number box
    /// (with its range) or a button that cycles an enum — and the Prisoners group's "Prisoners to ransom" tick-list.
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
            IntroText = UiText.S("ss_ui_instructions_intro",
                "Your standing orders to the steward. Changes are saved at once - to settings.json, and Mod Options shows them too. Hover a name for what it does.");
        }

        /// <summary>Builds the groups (again after Do it: the prisoners held may have changed).</summary>
        internal void EnsureBuilt(GameVisit visit)
        {
            if (_visit == visit && _groups.Count > 0)
                return;
            _visit = visit;
            var settings = SettingsHost.Current;
            var groups = new MBBindingList<SettingGroupVM>();
            foreach (var group in SettingEdit.Groups())
            {
                var vm = new SettingGroupVM(UiText.S("ss_grp_" + group.Key.Replace(" ", ""), SettingsRegistry.GroupLabel(group.Key)));
                foreach (var def in group.Value)
                {
                    vm.Settings.Add(new SettingLineVM(def, this));
                    if (def.Key == nameof(StewardSettings.WarMountsWarHorseTarget))
                        vm.Settings.Add(ReadyToUpgradeLine(visit)); // the live need beside the two targets (round 1)
                }
                if (group.Key == SettingsRegistry.Prisoners)
                {
                    var list = SettingsRegistry.Find(nameof(StewardSettings.PrisonersExcluded));
                    vm.Settings.Add(SettingLineVM.Heading(
                        UiText.S("ss_ui_prisoners_to_ransom", "Prisoners to ransom"),
                        list == null ? "" : UiText.S("ss_hint_" + list.Key, list.Hint)));
                    var held = visit.Snapshot.Prisoners.Where(p => p != null).Select(p => p.TroopId).ToList();
                    var rows = PrisonerTicks.Rows(held, settings);
                    if (rows.Count == 0)
                        vm.Settings.Add(SettingLineVM.Heading(UiText.S("ss_ui_no_prisoners", "(no prisoners now)"), ""));
                    foreach (var troopId in rows)
                        vm.Settings.Add(SettingLineVM.Prisoner(troopId, TroopName(troopId, visit), this));
                }
                groups.Add(vm);
            }
            Groups = groups;
        }

        /// <summary>"Troops ready to upgrade now: 4 for a horse, 10 for a war horse" — what "automatic" (-1) keeps, before
        /// the spares.</summary>
        private static SettingLineVM ReadyToUpgradeLine(GameVisit visit)
        {
            var needs = UpgradeNeeds.Of(visit.Snapshot);
            return SettingLineVM.Heading(
                UiText.S2("ss_ui_ready_to_upgrade", "Troops ready to upgrade now: {HORSES} for a horse, {WAR_HORSES} for a war horse",
                    "HORSES", UiFormat.Money(needs.ReadyFor(UpgradeNeeds.Horse)),
                    "WAR_HORSES", UiFormat.Money(needs.ReadyFor(UpgradeNeeds.WarHorse))),
                UiText.S("ss_ui_ready_to_upgrade_hint",
                    "What an automatic (-1) kind keeps for upgrades right now, before the spares - counted like the party screen."));
        }

        private static string TroopName(string troopId, GameVisit visit)
        {
            var held = visit.Snapshot.Prisoners.FirstOrDefault(p => p != null && p.TroopId == troopId);
            if (held != null && !string.IsNullOrEmpty(held.Name))
                return held.Name;
            try
            {
                return MBObjectManager.Instance?.GetObject<CharacterObject>(troopId)?.Name?.ToString() ?? troopId;
            }
            catch
            {
                return troopId;
            }
        }

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
            string? troopId = line.TroopId;
            if (troopId != null)
            {
                bool tick = !line.IsOn;
                SettingsHost.Service.Update(s => PrisonerTicks.SetRansom(s, troopId, tick));
                ModLog.Info("window", "prisoners to ransom: " + troopId + (tick ? " ticked" : " unticked"));
            }
            else if (line.Definition is BoolSetting b)
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
        [DataSourceProperty] public string MutedColor => UiColors.Muted;

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

    /// <summary>One line: a setting (checkbox / number box / enum button), a prisoner tick, or a small heading.</summary>
    public sealed class SettingLineVM : ViewModel
    {
        private readonly InstructionsTabVM? _tab;
        private bool _isOn;
        private string _valueText = "";
        private string _valueColor = UiColors.Text;
        private string _enumText = "";

        internal SettingLineVM(SettingDefinition def, InstructionsTabVM tab)
        {
            Definition = def;
            _tab = tab;
            LabelText = PricesTabVM.SettingLabel(def);
            Hint = new HintVM(PricesTabVM.SettingHint(def));
            IsBool = def.Kind == SettingKind.Bool;
            IsNumber = def.Kind == SettingKind.Int || def.Kind == SettingKind.Float;
            IsEnum = def.Kind == SettingKind.Enum;
            RangeText = SettingEdit.Range(def);
            EnumHint = new HintVM(IsEnum ? UiText.S("ss_ui_enum_hint", "Click for the next choice") : "");
            Refresh(SettingsHost.Current, includeTexts: true);
        }

        private SettingLineVM(string label, string hint, string? troopId, InstructionsTabVM? tab)
        {
            _tab = tab;
            TroopId = troopId;
            LabelText = label;
            Hint = new HintVM(hint);
            EnumHint = new HintVM();
            RangeText = "";
            IsBool = troopId != null;
            IsHeading = troopId == null;
            if (troopId != null)
                Refresh(SettingsHost.Current, includeTexts: true);
        }

        internal static SettingLineVM Heading(string label, string hint) => new SettingLineVM(label, hint, null, null);

        internal static SettingLineVM Prisoner(string troopId, string name, InstructionsTabVM tab) =>
            new SettingLineVM(name, UiText.S("ss_ui_prisoner_tick_hint", "Ticked: the steward may ransom (or donate) this troop."),
                troopId, tab);

        internal SettingDefinition? Definition { get; }

        /// <summary>A line of the "Prisoners to ransom" list: the troop id.</summary>
        internal string? TroopId { get; }

        internal void Refresh(StewardSettings settings, bool includeTexts)
        {
            if (TroopId != null)
            {
                IsOn = PrisonerTicks.IsTicked(settings, TroopId);
                return;
            }
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

        public void ExecuteToggle() => StewardWindowVM.Guard("setting toggle " + (Definition?.Key ?? TroopId), () => _tab?.Toggle(this));

        public void ExecuteCycle() => StewardWindowVM.Guard("setting cycle " + Definition?.Key, () => _tab?.Cycle(this));

        [DataSourceProperty] public string LabelText { get; }
        [DataSourceProperty] public HintVM Hint { get; }
        [DataSourceProperty] public HintVM EnumHint { get; }
        [DataSourceProperty] public bool IsBool { get; }
        [DataSourceProperty] public bool IsNumber { get; }
        [DataSourceProperty] public bool IsEnum { get; }
        [DataSourceProperty] public bool IsHeading { get; }
        [DataSourceProperty] public bool IsControlRow => !IsHeading;
        [DataSourceProperty] public string RangeText { get; }
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
