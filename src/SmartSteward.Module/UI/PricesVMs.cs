using System.Collections.Generic;
using System.Linq;
using SmartSteward.Adapter;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using TaleWorlds.Library;

namespace SmartSteward.UI
{
    /// <summary>
    /// The Prices tab (DESIGN §1.3): the two multipliers at the top, then the price book in collapsible groups — Food,
    /// and Horses with its sub-headers Pack animals · Mounts · War mounts · Noble horses — sell only (step 17: no buy
    /// column there, the sell placeholder always filled). Per item: Buy tick, max-buy base × the buy
    /// multiplier » final, Sell tick, min-sell base × the sell multiplier » final, ⟲. Every change goes through the
    /// settings service at once (saved to settings.json, Changed raised — the plan is re-made when the Suggestion tab
    /// shows again). A typed box keeps what the player typed; the rest of the row refreshes around it.
    /// </summary>
    public sealed class PricesTabVM : ViewModel
    {
        private readonly FloatSetting? _buyMultiplier;
        private readonly FloatSetting? _sellMultiplier;
        private GameVisit? _visit;
        private bool _built;
        private string _buyMultiplierText = "";
        private string _sellMultiplierText = "";
        private string _buyMultiplierColor = UiColors.Text;
        private string _sellMultiplierColor = UiColors.Text;
        private MBBindingList<PriceGroupVM> _groups = new MBBindingList<PriceGroupVM>();

        internal PricesTabVM()
        {
            // Step 20: the two boxes at the top are the HORSE multipliers (food got its own pair - Instructions tab and MCM
            // until step 21 gives the Food group boxes of its own); every line shows its own group's multiplier.
            _buyMultiplier = SettingsRegistry.Find(nameof(StewardSettings.HorseBuyPriceMultiplier)) as FloatSetting;
            _sellMultiplier = SettingsRegistry.Find(nameof(StewardSettings.HorseSellPriceMultiplier)) as FloatSetting;
            BuyMultiplierLabel = SettingLabel(_buyMultiplier);
            SellMultiplierLabel = SettingLabel(_sellMultiplier);
            BuyMultiplierHint = new HintVM(SettingHint(_buyMultiplier));
            SellMultiplierHint = new HintVM(SettingHint(_sellMultiplier));
            IntroText = UiText.S("ss_ui_prices_intro",
                "Final = your base price × the multiplier. An empty box uses the grey average price where auto-fill is on (Instructions tab, Prices). Unticked items are never bought or sold.");
            ColBuy = UiText.S("ss_ui_col_buy", "Buy");
            ColMaxBuy = UiText.S("ss_ui_col_max_buy", "Max buy price");
            ColSell = UiText.S("ss_ui_col_sell", "Sell");
            ColMinSell = UiText.S("ss_ui_col_min_sell", "Min sell price");
            ColItem = UiText.S("ss_ui_col_item", "Item");
        }

        internal static string SettingLabel(SettingDefinition? def) =>
            def == null ? "" : UiText.S("ss_set_" + def.Key, def.Label);

        internal static string SettingHint(SettingDefinition? def) =>
            def == null ? "" : UiText.S("ss_hint_" + def.Key, def.Hint);

        /// <summary>The first time the tab shows: every price-book item of the game, with this settlement's
        /// placeholders.</summary>
        internal void EnsureBuilt(GameVisit visit)
        {
            if (_built && _visit == visit)
                return;
            _visit = visit;
            _built = true;
            var items = PriceBookCatalog.Build(visit);
            var groups = new MBBindingList<PriceGroupVM>();

            var food = new PriceGroupVM(UiText.S("ss_ui_prices_food", "Food"));
            foreach (var item in items.Where(i => i.Group == PriceBookGroup.Food))
                food.Lines.Add(new PriceLineVM(item, this));
            if (food.Lines.Count > 0)
                groups.Add(food);

            var horses = new PriceGroupVM(UiText.S("ss_ui_prices_horses", "Horses"));
            AddSubGroup(horses, items, PriceBookGroup.PackAnimals, UiText.S("ss_ui_prices_pack", "Pack animals"));
            AddSubGroup(horses, items, PriceBookGroup.Mounts, UiText.S("ss_ui_prices_mounts", "Mounts"));
            AddSubGroup(horses, items, PriceBookGroup.WarMounts, UiText.S("ss_ui_prices_war_mounts", "War mounts"));
            AddSubGroup(horses, items, PriceBookGroup.NobleHorses, UiText.S("ss_ui_prices_noble", "Noble horses — sell only"));
            if (horses.Lines.Count > 0)
                groups.Add(horses);

            Groups = groups;
            ModLog.Info("window", "prices tab: " + items.Count + " items");
        }

        private void AddSubGroup(PriceGroupVM group, List<PriceBookItem> items, PriceBookGroup kind, string title)
        {
            var sub = items.Where(i => i.Group == kind).ToList();
            if (sub.Count == 0)
                return;
            group.Lines.Add(PriceLineVM.SubHeader(title));
            foreach (var item in sub)
                group.Lines.Add(new PriceLineVM(item, this));
        }

        /// <summary>Every row reads the settings again. <paramref name="includeTypedTexts"/>: also overwrite the edit
        /// boxes (when the tab is shown) — never while the player may be typing in one.</summary>
        internal void RefreshAll(bool includeTypedTexts)
        {
            var settings = SettingsHost.Current;
            if (includeTypedTexts)
            {
                BuyMultiplierText = UiFormat.Decimal(settings.HorseBuyPriceMultiplier);
                SellMultiplierText = UiFormat.Decimal(settings.HorseSellPriceMultiplier);
                BuyMultiplierColor = UiColors.Text;
                SellMultiplierColor = UiColors.Text;
            }
            foreach (var group in Groups)
                foreach (var line in group.Lines)
                    line.Refresh(settings, includeTypedTexts);
        }

        // ── edits ──────────────────────────────────────────────────────────────────────────────────────

        internal void ToggleBuy(PriceLineVM line)
        {
            bool now = !line.BuyTicked;
            SettingsHost.Service.Update(s => PriceBookEditor.SetBuy(s, line.ItemId, now));
            ModLog.Info("window", "price book " + line.ItemId + ": buy " + (now ? "on" : "off"));
            line.Refresh(SettingsHost.Current, includeTypedTexts: false);
        }

        internal void ToggleSell(PriceLineVM line)
        {
            bool now = !line.SellTicked;
            SettingsHost.Service.Update(s => PriceBookEditor.SetSell(s, line.ItemId, now));
            ModLog.Info("window", "price book " + line.ItemId + ": sell " + (now ? "on" : "off"));
            line.Refresh(SettingsHost.Current, includeTypedTexts: false);
        }

        /// <summary>A key typed in a base box: saved when it reads as a whole number (or empty = back to the
        /// placeholder); otherwise the box turns red and nothing is saved.</summary>
        internal void TypedBase(PriceLineVM line, bool buy, string text)
        {
            if (!PriceRowView.TryReadBase(text, out int? value))
            {
                line.SetBaseValid(buy, false);
                return;
            }
            line.SetBaseValid(buy, true);
            SettingsHost.Service.Update(s =>
            {
                if (buy) PriceBookEditor.SetBuyBase(s, line.ItemId, value);
                else PriceBookEditor.SetSellBase(s, line.ItemId, value);
            });
            line.Refresh(SettingsHost.Current, includeTypedTexts: false);
        }

        internal void ResetBases(PriceLineVM line)
        {
            SettingsHost.Service.Update(s => PriceBookEditor.ClearBases(s, line.ItemId));
            ModLog.Info("window", "price book " + line.ItemId + ": bases cleared");
            line.Refresh(SettingsHost.Current, includeTypedTexts: true);
        }

        private void TypedMultiplier(FloatSetting? def, string text, bool buy)
        {
            if (def == null)
                return;
            bool ok = UiFormat.TryParseDecimal(text, out double value);
            if (buy) BuyMultiplierColor = ok ? UiColors.Text : UiColors.Warning;
            else SellMultiplierColor = ok ? UiColors.Text : UiColors.Warning;
            if (ok)
                SettingsHost.Service.Set(def, value); // clamped by the registry; Changed refreshes the rows
        }

        // ── bound properties ───────────────────────────────────────────────────────────────────────────

        [DataSourceProperty] public string BuyMultiplierLabel { get; }
        [DataSourceProperty] public string SellMultiplierLabel { get; }
        [DataSourceProperty] public HintVM BuyMultiplierHint { get; }
        [DataSourceProperty] public HintVM SellMultiplierHint { get; }
        [DataSourceProperty] public string IntroText { get; }
        [DataSourceProperty] public string ColBuy { get; }
        [DataSourceProperty] public string ColMaxBuy { get; }
        [DataSourceProperty] public string ColSell { get; }
        [DataSourceProperty] public string ColMinSell { get; }
        [DataSourceProperty] public string ColItem { get; }
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string HeadingColor => UiColors.Heading;

        [DataSourceProperty]
        public string BuyMultiplierText
        {
            get => _buyMultiplierText;
            set
            {
                if (value == _buyMultiplierText) return;
                _buyMultiplierText = value ?? "";
                OnPropertyChangedWithValue(_buyMultiplierText, nameof(BuyMultiplierText));
                string text = _buyMultiplierText;
                StewardWindowVM.Guard("buy multiplier", () => TypedMultiplier(_buyMultiplier, text, buy: true));
            }
        }

        [DataSourceProperty]
        public string SellMultiplierText
        {
            get => _sellMultiplierText;
            set
            {
                if (value == _sellMultiplierText) return;
                _sellMultiplierText = value ?? "";
                OnPropertyChangedWithValue(_sellMultiplierText, nameof(SellMultiplierText));
                string text = _sellMultiplierText;
                StewardWindowVM.Guard("sell multiplier", () => TypedMultiplier(_sellMultiplier, text, buy: false));
            }
        }

        [DataSourceProperty]
        public string BuyMultiplierColor
        {
            get => _buyMultiplierColor;
            set { if (value != _buyMultiplierColor) { _buyMultiplierColor = value; OnPropertyChangedWithValue(value, nameof(BuyMultiplierColor)); } }
        }

        [DataSourceProperty]
        public string SellMultiplierColor
        {
            get => _sellMultiplierColor;
            set { if (value != _sellMultiplierColor) { _sellMultiplierColor = value; OnPropertyChangedWithValue(value, nameof(SellMultiplierColor)); } }
        }

        [DataSourceProperty]
        public MBBindingList<PriceGroupVM> Groups
        {
            get => _groups;
            set { if (value != _groups) { _groups = value; OnPropertyChangedWithValue(value, nameof(Groups)); } }
        }
    }

    /// <summary>A collapsible group of the price book (Food, Horses).</summary>
    public sealed class PriceGroupVM : ViewModel
    {
        private bool _isExpanded = true;

        internal PriceGroupVM(string title)
        {
            TitleText = title;
            Lines = new MBBindingList<PriceLineVM>();
        }

        public void ExecuteToggle() => StewardWindowVM.Guard("prices group", () => IsExpanded = !IsExpanded);

        [DataSourceProperty] public string TitleText { get; }
        [DataSourceProperty] public string HeadingColor => UiColors.Heading;
        [DataSourceProperty] public MBBindingList<PriceLineVM> Lines { get; }

        [DataSourceProperty]
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (value == _isExpanded) return;
                _isExpanded = value;
                OnPropertyChangedWithValue(value, nameof(IsExpanded));
                OnPropertyChangedWithValue(!value, nameof(IsCollapsed));
            }
        }

        [DataSourceProperty] public bool IsCollapsed => !_isExpanded;
    }

    /// <summary>One line of a price group: a sub-header (Pack animals · Mounts · War mounts · Noble horses) or an item row.</summary>
    public sealed class PriceLineVM : ViewModel
    {
        private readonly PriceBookItem? _item;
        private readonly PricesTabVM? _tab;

        private bool _buyTicked;
        private bool _sellTicked;
        private string _buyBaseText = "";
        private string _sellBaseText = "";
        private string _buyPlaceholderText = "";
        private string _sellPlaceholderText = "";
        private string _buyMultiplierText = "";
        private string _sellMultiplierText = "";
        private string _buyFinalText = "";
        private string _sellFinalText = "";
        private string _buyBaseColor = UiColors.Text;
        private string _sellBaseColor = UiColors.Text;
        private bool _canReset;

        private PriceLineVM(string subHeader)
        {
            IsSubHeader = true;
            SubHeaderText = subHeader;
            NameText = "";
            ItemId = "";
            ResetHint = new HintVM();
        }

        internal PriceLineVM(PriceBookItem item, PricesTabVM tab)
        {
            _item = item;
            _tab = tab;
            IsItem = true;
            HasBuy = item.Group != PriceBookGroup.NobleHorses; // noble horses are never bought (step 17)
            SubHeaderText = "";
            NameText = item.Name;
            ItemId = item.ItemId;
            ResetHint = new HintVM(UiText.S("ss_ui_prices_reset_hint", "Clear your prices - the grey average applies again"));
        }

        internal static PriceLineVM SubHeader(string title) => new PriceLineVM(title);

        internal string ItemId { get; }

        internal void Refresh(StewardSettings settings, bool includeTypedTexts)
        {
            if (_item == null)
                return;
            var view = PriceRowView.Of(_item.ItemId, _item.Group, settings, _item.Averages);
            BuyTicked = view.BuyTicked;
            SellTicked = view.SellTicked;
            if (includeTypedTexts)
            {
                SetText(ref _buyBaseText, view.BuyBaseText, nameof(BuyBaseText));
                SetText(ref _sellBaseText, view.SellBaseText, nameof(SellBaseText));
                BuyBaseColor = UiColors.Text;
                SellBaseColor = UiColors.Text;
            }
            BuyPlaceholderText = view.BuyPlaceholder;
            SellPlaceholderText = view.SellPlaceholder;
            BuyMultiplierText = view.BuyMultiplier;
            SellMultiplierText = view.SellMultiplier;
            BuyFinalText = UiFormat.Arrow + " " + view.BuyFinal;
            SellFinalText = UiFormat.Arrow + " " + view.SellFinal;
            CanReset = view.HasTypedBase;
            NotifyPlaceholders();
        }

        internal void SetBaseValid(bool buy, bool valid)
        {
            if (buy) BuyBaseColor = valid ? UiColors.Text : UiColors.Warning;
            else SellBaseColor = valid ? UiColors.Text : UiColors.Warning;
        }

        private void SetText(ref string field, string value, string name)
        {
            if (value == field) return;
            field = value;
            OnPropertyChangedWithValue(value, name);
        }

        private void NotifyPlaceholders()
        {
            OnPropertyChangedWithValue(ShowBuyPlaceholder, nameof(ShowBuyPlaceholder));
            OnPropertyChangedWithValue(ShowSellPlaceholder, nameof(ShowSellPlaceholder));
        }

        // ── commands ─────────────────────────────────────────────────────────────────────────────────────

        public void ExecuteToggleBuy() => StewardWindowVM.Guard("buy tick " + ItemId, () => _tab?.ToggleBuy(this));

        public void ExecuteToggleSell() => StewardWindowVM.Guard("sell tick " + ItemId, () => _tab?.ToggleSell(this));

        public void ExecuteReset() => StewardWindowVM.Guard("prices reset " + ItemId, () => _tab?.ResetBases(this));

        // ── bound properties ─────────────────────────────────────────────────────────────────────────────

        [DataSourceProperty] public bool IsSubHeader { get; }
        [DataSourceProperty] public bool IsItem { get; }

        /// <summary>The buy half of the row shows (every group but the noble horses — sell only, step 17).</summary>
        [DataSourceProperty] public bool HasBuy { get; }
        [DataSourceProperty] public string SubHeaderText { get; }
        [DataSourceProperty] public string NameText { get; }
        [DataSourceProperty] public HintVM ResetHint { get; }
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string PlaceholderColor => UiColors.Placeholder;
        [DataSourceProperty] public string HeadingColor => UiColors.Heading;

        [DataSourceProperty]
        public string BuyBaseText
        {
            get => _buyBaseText;
            set
            {
                if (value == _buyBaseText) return;
                _buyBaseText = value ?? "";
                OnPropertyChangedWithValue(_buyBaseText, nameof(BuyBaseText));
                NotifyPlaceholders();
                string text = _buyBaseText;
                StewardWindowVM.Guard("max buy " + ItemId, () => _tab?.TypedBase(this, buy: true, text));
            }
        }

        [DataSourceProperty]
        public string SellBaseText
        {
            get => _sellBaseText;
            set
            {
                if (value == _sellBaseText) return;
                _sellBaseText = value ?? "";
                OnPropertyChangedWithValue(_sellBaseText, nameof(SellBaseText));
                NotifyPlaceholders();
                string text = _sellBaseText;
                StewardWindowVM.Guard("min sell " + ItemId, () => _tab?.TypedBase(this, buy: false, text));
            }
        }

        /// <summary>The grey average shows only in an empty box.</summary>
        [DataSourceProperty] public bool ShowBuyPlaceholder => _buyBaseText.Length == 0 && _buyPlaceholderText.Length > 0;

        [DataSourceProperty] public bool ShowSellPlaceholder => _sellBaseText.Length == 0 && _sellPlaceholderText.Length > 0;

        [DataSourceProperty]
        public bool BuyTicked
        {
            get => _buyTicked;
            set { if (value != _buyTicked) { _buyTicked = value; OnPropertyChangedWithValue(value, nameof(BuyTicked)); } }
        }

        [DataSourceProperty]
        public bool SellTicked
        {
            get => _sellTicked;
            set { if (value != _sellTicked) { _sellTicked = value; OnPropertyChangedWithValue(value, nameof(SellTicked)); } }
        }

        [DataSourceProperty]
        public string BuyPlaceholderText
        {
            get => _buyPlaceholderText;
            set { if (value != _buyPlaceholderText) { _buyPlaceholderText = value; OnPropertyChangedWithValue(value, nameof(BuyPlaceholderText)); } }
        }

        [DataSourceProperty]
        public string SellPlaceholderText
        {
            get => _sellPlaceholderText;
            set { if (value != _sellPlaceholderText) { _sellPlaceholderText = value; OnPropertyChangedWithValue(value, nameof(SellPlaceholderText)); } }
        }

        [DataSourceProperty]
        public string BuyMultiplierText
        {
            get => _buyMultiplierText;
            set { if (value != _buyMultiplierText) { _buyMultiplierText = value; OnPropertyChangedWithValue(value, nameof(BuyMultiplierText)); } }
        }

        [DataSourceProperty]
        public string SellMultiplierText
        {
            get => _sellMultiplierText;
            set { if (value != _sellMultiplierText) { _sellMultiplierText = value; OnPropertyChangedWithValue(value, nameof(SellMultiplierText)); } }
        }

        [DataSourceProperty]
        public string BuyFinalText
        {
            get => _buyFinalText;
            set { if (value != _buyFinalText) { _buyFinalText = value; OnPropertyChangedWithValue(value, nameof(BuyFinalText)); } }
        }

        [DataSourceProperty]
        public string SellFinalText
        {
            get => _sellFinalText;
            set { if (value != _sellFinalText) { _sellFinalText = value; OnPropertyChangedWithValue(value, nameof(SellFinalText)); } }
        }

        [DataSourceProperty]
        public string BuyBaseColor
        {
            get => _buyBaseColor;
            set { if (value != _buyBaseColor) { _buyBaseColor = value; OnPropertyChangedWithValue(value, nameof(BuyBaseColor)); } }
        }

        [DataSourceProperty]
        public string SellBaseColor
        {
            get => _sellBaseColor;
            set { if (value != _sellBaseColor) { _sellBaseColor = value; OnPropertyChangedWithValue(value, nameof(SellBaseColor)); } }
        }

        [DataSourceProperty]
        public bool CanReset
        {
            get => _canReset;
            set { if (value != _canReset) { _canReset = value; OnPropertyChangedWithValue(value, nameof(CanReset)); } }
        }
    }
}
