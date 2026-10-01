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
    /// The Prices tab (DESIGN §1.3): the multipliers at the top — Food's own pair and the horses' pair (round 4, step 21) —,
    /// then the price book in collapsible groups, cheapest first inside each (step 20) — Food, and Horses with its sub-headers
    /// Pack animals · Mounts · War mounts · Noble horses — sell only (step 17: no buy column there, the sell placeholder always
    /// filled). Per item: Buy tick, max-buy base × its group's buy multiplier » final, Sell tick, min-sell base × its group's
    /// sell multiplier » final, ⟲. Every change goes through the settings service at once (saved to settings.json, Changed
    /// raised — the plan is re-made when the Suggestion tab shows again). A typed box keeps what the player typed; the rest of
    /// the row refreshes around it.
    /// </summary>
    public sealed class PricesTabVM : ViewModel
    {
        private GameVisit? _visit;
        private bool _built;
        private MBBindingList<PriceGroupVM> _groups = new MBBindingList<PriceGroupVM>();

        internal PricesTabVM()
        {
            // Round 4 (Anton 2026.09.28: "food prices multipliers set from 0.5 to 2"): food has a pair of its own (step 20); the
            // step-7 pair became the horses' - both pairs sit at the top now (step 21).
            FoodLabel = UiText.S("ss_ui_prices_food", "Food");
            HorsesLabel = UiText.S("ss_ui_prices_horses", "Horses");
            string buy = UiText.S("ss_ui_prices_buy_times", "buy ×");
            string sell = UiText.S("ss_ui_prices_sell_times", "sell ×");
            FoodBuy = new MultiplierBoxVM(nameof(StewardSettings.FoodBuyPriceMultiplier), buy);
            FoodSell = new MultiplierBoxVM(nameof(StewardSettings.FoodSellPriceMultiplier), sell);
            HorseBuy = new MultiplierBoxVM(nameof(StewardSettings.HorseBuyPriceMultiplier), buy);
            HorseSell = new MultiplierBoxVM(nameof(StewardSettings.HorseSellPriceMultiplier), sell);
            IntroText = UiText.S("ss_ui_prices_intro_pairs",
                "Final = your base price × its group's multiplier. An empty box uses the grey average price where auto-fill is on (Instructions tab, Prices). Unticked items are never bought or sold. Cheapest first.");
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
            AddSubGroup(horses, items, PriceBookGroup.NobleHorses, NobleTitle(SettingsHost.Current));
            if (horses.Lines.Count > 0)
                groups.Add(horses);

            Groups = groups;
            ModLog.Info("window", "prices tab: " + items.Count + " items");
        }

        /// <summary>"Noble horses — sell only", or "Noble horses" while some are kept (step 28: they are bought then too).</summary>
        internal static string NobleTitle(StewardSettings settings) =>
            PriceBook.IsSellOnly(PriceBookGroup.NobleHorses, settings)
                ? UiText.S("ss_ui_prices_noble", "Noble horses — sell only")
                : UiText.S("ss_ui_prices_noble_kept", "Noble horses");

        private void AddSubGroup(PriceGroupVM group, List<PriceBookItem> items, PriceBookGroup kind, string title)
        {
            var sub = items.Where(i => i.Group == kind).ToList();
            if (sub.Count == 0)
                return;
            group.Lines.Add(PriceLineVM.SubHeader(title, kind));
            foreach (var item in sub)
                group.Lines.Add(new PriceLineVM(item, this));
        }

        /// <summary>Every row reads the settings again. <paramref name="includeTypedTexts"/>: also overwrite the edit
        /// boxes (when the tab is shown) — never while the player may be typing in one.</summary>
        internal void RefreshAll(bool includeTypedTexts)
        {
            var settings = SettingsHost.Current;
            if (includeTypedTexts)
                foreach (var box in new[] { FoodBuy, FoodSell, HorseBuy, HorseSell })
                    box.RefreshText(settings);
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

        // ── bound properties ───────────────────────────────────────────────────────────────────────────

        [DataSourceProperty] public string FoodLabel { get; }
        [DataSourceProperty] public string HorsesLabel { get; }
        [DataSourceProperty] public MultiplierBoxVM FoodBuy { get; }
        [DataSourceProperty] public MultiplierBoxVM FoodSell { get; }
        [DataSourceProperty] public MultiplierBoxVM HorseBuy { get; }
        [DataSourceProperty] public MultiplierBoxVM HorseSell { get; }
        [DataSourceProperty] public string IntroText { get; }
        [DataSourceProperty] public string ColBuy { get; }
        [DataSourceProperty] public string ColMaxBuy { get; }
        [DataSourceProperty] public string ColSell { get; }
        [DataSourceProperty] public string ColMinSell { get; }
        [DataSourceProperty] public string ColItem { get; }
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string HeadingColor => UiColors.Heading;

        [DataSourceProperty]
        public MBBindingList<PriceGroupVM> Groups
        {
            get => _groups;
            set { if (value != _groups) { _groups = value; OnPropertyChangedWithValue(value, nameof(Groups)); } }
        }
    }

    /// <summary>One multiplier box at the top of the Prices tab (the food pair and the horse pair — step 21): its setting's
    /// value, typed in place; a number saves at once (clamped by the registry), anything else turns the box red.</summary>
    public sealed class MultiplierBoxVM : ViewModel
    {
        private readonly FloatSetting? _def;
        private string _valueText = "";
        private string _valueColor = UiColors.Text;

        internal MultiplierBoxVM(string key, string label)
        {
            _def = SettingsRegistry.Find(key) as FloatSetting;
            Key = key;
            LabelText = label;
            Hint = new HintVM(PricesTabVM.SettingLabel(_def) + ". " + PricesTabVM.SettingHint(_def));
        }

        internal string Key { get; }

        /// <summary>The box shows the saved value again (when the tab is shown — never while the player may be typing in it).</summary>
        internal void RefreshText(StewardSettings settings)
        {
            if (_def == null)
                return;
            string text = UiFormat.Decimal(_def.Get(settings));
            if (text != _valueText)
            {
                _valueText = text;
                OnPropertyChangedWithValue(text, nameof(ValueText));
            }
            ValueColor = UiColors.Text;
        }

        private void Typed(string text)
        {
            if (_def == null)
                return;
            bool ok = UiFormat.TryParseDecimal(text, out double value);
            ValueColor = ok ? UiColors.Text : UiColors.Warning;
            if (ok)
                SettingsHost.Service.Set(_def, value); // clamped by the registry; Changed refreshes the rows
        }

        [DataSourceProperty] public string LabelText { get; }
        [DataSourceProperty] public HintVM Hint { get; }

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
                StewardWindowVM.Guard("multiplier " + Key, () => Typed(text));
            }
        }

        [DataSourceProperty]
        public string ValueColor
        {
            get => _valueColor;
            set { if (value != _valueColor) { _valueColor = value; OnPropertyChangedWithValue(value, nameof(ValueColor)); } }
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

        private PriceLineVM(string subHeader, PriceBookGroup group)
        {
            IsSubHeader = true;
            _subGroup = group;
            _subHeaderText = subHeader;
            NameText = "";
            ItemId = "";
            ResetHint = new HintVM();
        }

        internal PriceLineVM(PriceBookItem item, PricesTabVM tab)
        {
            _item = item;
            _tab = tab;
            IsItem = true;
            _hasBuy = !PriceBook.IsSellOnly(item.Group, SettingsHost.Current); // noble horses: only while kept (step 17, 28)
            _subHeaderText = "";
            NameText = item.Name;
            ItemId = item.ItemId;
            ResetHint = new HintVM(UiText.S("ss_ui_prices_reset_hint", "Clear your prices - the grey average applies again"));
        }

        internal static PriceLineVM SubHeader(string title, PriceBookGroup group) => new PriceLineVM(title, group);

        /// <summary>The group a sub-header heads (its title follows the settings — the noble horses', step 28).</summary>
        private readonly PriceBookGroup _subGroup;
        private string _subHeaderText;
        private bool _hasBuy;

        internal string ItemId { get; }

        internal void Refresh(StewardSettings settings, bool includeTypedTexts)
        {
            if (_item == null)
            {
                if (IsSubHeader && _subGroup == PriceBookGroup.NobleHorses)
                    SetText(ref _subHeaderText, PricesTabVM.NobleTitle(settings), nameof(SubHeaderText));
                return;
            }
            bool hasBuy = !PriceBook.IsSellOnly(_item.Group, settings); // step 28: the noble horses' buy column while kept
            if (hasBuy != _hasBuy)
            {
                _hasBuy = hasBuy;
                OnPropertyChangedWithValue(hasBuy, nameof(HasBuy));
            }
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

        /// <summary>The buy half of the row shows (every group but the noble horses — sell only, step 17 — unless some are kept,
        /// step 28).</summary>
        [DataSourceProperty] public bool HasBuy => _hasBuy;
        [DataSourceProperty] public string SubHeaderText => _subHeaderText;
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
