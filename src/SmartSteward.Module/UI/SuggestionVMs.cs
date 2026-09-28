using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.Core.Settings;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace SmartSteward.UI
{
    /// <summary>
    /// The Suggestion tab (DESIGN §1.1): the gold line at the very top, the sections in DESIGN order, each row an
    /// aligned table line — Mine | [−] change [+] [⟲] | Result | Price | Market | Item | Type — and the footer. Every
    /// button binds to the plan editor (Core, step 4b) and never computes: a click edits the plan, then every row,
    /// the header and the footer read the plan again.
    /// </summary>
    public sealed class SuggestionTabVM : ViewModel
    {
        private readonly Action _onPlanChanged;
        private StewardPlan? _plan;
        private Settlement? _settlement;

        private MBBindingList<SectionVM> _sections = new MBBindingList<SectionVM>();
        private string _headerText = "";
        private string _headerColor = UiColors.Muted;
        private string _footerMoneyText = "";
        private string _footerFoodText = "";
        private string _footerWeightText = "";
        private string _footerPartyText = "";
        private string _footerPartyColor = UiColors.Text;
        private string _warningText = "";
        private bool _hasWarnings;
        private string _statusText = "";
        private bool _hasStatus;
        private bool _isEmpty;
        private bool _canResetAll;
        private string _emptyText = "";
        private string _marketClosedText = "";
        private bool _hasMarketNotice;
        private readonly string _nothingToDoText;

        internal SuggestionTabVM(Action onPlanChanged)
        {
            _onPlanChanged = onPlanChanged;
            ColMine = UiText.S("ss_ui_col_mine", "Mine");
            ColChange = UiText.S("ss_ui_col_change", "Change");
            ColResult = UiText.S("ss_ui_col_result", "Result");
            ColPrice = UiText.S("ss_ui_col_price", "Price");
            ColMarket = UiText.S("ss_ui_col_market", "Market");
            ColItem = UiText.S("ss_ui_col_item", "Item");
            ColType = UiText.S("ss_ui_col_type", "Type");
            _nothingToDoText = UiText.S("ss_ui_empty", "Nothing for the steward to do here.");
            _emptyText = _nothingToDoText;
            ShortcutText = UiText.S("ss_ui_shortcuts", "Click ±1  ·  Shift ±5  ·  Ctrl all  ·  names in gold open the Encyclopedia");
            ResetAllText = UiText.S("ss_ui_reset_all", "Reset all");
        }

        internal StewardPlan? Plan => _plan;

        /// <summary>Shows a (new) plan: the sections and rows are built afresh. A closed market (the snapshot's
        /// <c>TradeClosedReason</c>, the game's own words) is said at the top — or, with nothing to do, instead of the
        /// table (playtest round 1).</summary>
        internal void SetPlan(StewardPlan plan, Adapter.GameVisit visit)
        {
            _plan = plan;
            _settlement = visit.Settlement;
            var snap = visit.Snapshot;
            MarketClosedText = snap.CanTrade ? ""
                : UiText.S1("ss_ui_market_closed", "Market closed: {REASON}", "REASON", snap.TradeClosedReason ?? "");
            var sections = new MBBindingList<SectionVM>();
            foreach (var section in plan.Sections)
                sections.Add(new SectionVM(section, plan, this));
            Sections = sections;
            Refresh();
        }

        /// <summary>After any edit: every row, the header and the footer read the plan again.</summary>
        internal void Refresh()
        {
            var plan = _plan;
            if (plan == null)
                return;
            foreach (var section in Sections)
                section.Refresh();

            var t = plan.Totals;
            HeaderText = UiText.S3("ss_ui_header", "Gold {NOW} » {AFTER}   ({CHANGE})",
                "NOW", UiFormat.Money(t.GoldNow), "AFTER", UiFormat.Money(t.GoldAfter), "CHANGE", UiFormat.SignedMoney(t.GoldChange));
            HeaderColor = UiColors.ForGoldChange(t.GoldChange);

            FooterMoneyText = UiText.S2("ss_ui_footer_money", "Spent {SPENT}  ·  earned {EARNED}",
                "SPENT", UiFormat.Money(t.Spent), "EARNED", UiFormat.Money(t.Earned));
            FooterFoodText = t.FoodDaysAfter == null
                ? UiText.S2("ss_ui_footer_food_nodays", "Food {NOW} » {AFTER}",
                    "NOW", UiFormat.Money(t.FoodUnitsNow), "AFTER", UiFormat.Money(t.FoodUnitsAfter))
                : UiText.S3("ss_ui_footer_food", "Food {NOW} » {AFTER}  (~{DAYS} days)",
                    "NOW", UiFormat.Money(t.FoodUnitsNow), "AFTER", UiFormat.Money(t.FoodUnitsAfter), "DAYS", UiFormat.Days(t.FoodDaysAfter));
            // The party after the deal against its size limit — information, never a wall (round 3): red when over.
            FooterPartyText = UiText.S2("ss_ui_footer_party", "Party {AFTER}/{LIMIT}",
                "AFTER", UiFormat.Money(t.MembersAfter), "LIMIT", UiFormat.Money(t.PartySizeLimit));
            FooterPartyColor = t.OverPartyLimit ? UiColors.Warning : UiColors.Text;
            string weight = UiText.S1("ss_ui_footer_weight", "Weight {KG} kg", "KG", UiFormat.SignedWeight(t.WeightChange));
            if (t.InfluenceGained > 0.05)
                weight += "  ·  " + UiText.S1("ss_ui_footer_influence", "Influence {INF}", "INF", UiFormat.SignedInfluence(t.InfluenceGained));
            FooterWeightText = weight;

            var floors = plan.Floors; // the floors the flags were computed against
            var warnings = PlanFooter.Warnings(t);
            WarningText = string.Join("   ", warnings.Select(w => UiLabels.Warning(w, floors.All, floors.Animals)));
            HasWarnings = warnings.Count > 0;
            bool closed = MarketClosedText.Length > 0;
            IsEmpty = plan.Sections.Count == 0 || (closed && !plan.HasChanges);
            EmptyText = closed ? MarketClosedText : _nothingToDoText;
            HasMarketNotice = closed && !IsEmpty;
            CanResetAll = plan.IsEdited;
        }

        internal void SetStatus(string text)
        {
            StatusText = text ?? "";
            HasStatus = !string.IsNullOrEmpty(StatusText);
        }

        // ── edits (called by the rows) ───────────────────────────────────────────────────────────────────

        internal void Edit(PlanRow row, int direction)
        {
            var plan = _plan;
            if (plan == null)
                return;
            var size = StewardWindow.CurrentEditSize;
            var result = direction > 0 ? plan.Increase(row.Id, size) : plan.Decrease(row.Id, size);
            ModLog.Info("window", (direction > 0 ? "[+] " : "[-] ") + size + " " + row.Id + ": " + result.Before + " -> "
                                  + result.After + (result.Block == EditBlock.None ? "" : " (" + result.Block + ")"));
            AfterEdit();
        }

        internal void Reset(PlanRow row)
        {
            var plan = _plan;
            if (plan == null)
                return;
            var result = plan.Reset(row.Id);
            ModLog.Info("window", "reset " + row.Id + ": " + result.Before + " -> " + result.After);
            AfterEdit();
        }

        public void ExecuteResetAll() => StewardWindowVM.Guard("reset all", () =>
        {
            if (_plan == null)
                return;
            _plan.ResetAll();
            ModLog.Info("window", "reset all");
            AfterEdit();
        });

        private void AfterEdit()
        {
            SetStatus("");
            Refresh();
            _onPlanChanged();
        }

        /// <summary>A wanderer's or the mercenary troop's Encyclopedia page (DESIGN §2.7).</summary>
        internal void OpenLink(PlanRow row)
        {
            string? link = null;
            if (row.Tavern?.Kind == TavernRowKind.Wanderer && row.HeroId != null)
                link = _settlement?.HeroesWithoutParty.FirstOrDefault(h => h != null && h.StringId == row.HeroId)?.EncyclopediaLink;
            else if (row.TroopId != null)
                link = MBObjectManager.Instance?.GetObject<CharacterObject>(row.TroopId)?.EncyclopediaLink;
            ModLog.Info("window", "encyclopedia for " + row.Id + ": " + (link ?? "no link"));
            StewardWindow.OpenEncyclopedia(link);
        }

        // ── bound properties ─────────────────────────────────────────────────────────────────────────────

        [DataSourceProperty] public string ColMine { get; }
        [DataSourceProperty] public string ColChange { get; }
        [DataSourceProperty] public string ColResult { get; }
        [DataSourceProperty] public string ColPrice { get; }
        [DataSourceProperty] public string ColMarket { get; }
        [DataSourceProperty] public string ColItem { get; }
        [DataSourceProperty] public string ColType { get; }
        [DataSourceProperty] public string ShortcutText { get; }
        [DataSourceProperty] public string ResetAllText { get; }
        [DataSourceProperty] public string WarningColor => UiColors.Warning;
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string StatusColor => UiColors.Heading;

        [DataSourceProperty]
        public MBBindingList<SectionVM> Sections
        {
            get => _sections;
            set { if (value != _sections) { _sections = value; OnPropertyChangedWithValue(value, nameof(Sections)); } }
        }

        [DataSourceProperty]
        public string HeaderText
        {
            get => _headerText;
            set { if (value != _headerText) { _headerText = value; OnPropertyChangedWithValue(value, nameof(HeaderText)); } }
        }

        [DataSourceProperty]
        public string HeaderColor
        {
            get => _headerColor;
            set { if (value != _headerColor) { _headerColor = value; OnPropertyChangedWithValue(value, nameof(HeaderColor)); } }
        }

        [DataSourceProperty]
        public string FooterMoneyText
        {
            get => _footerMoneyText;
            set { if (value != _footerMoneyText) { _footerMoneyText = value; OnPropertyChangedWithValue(value, nameof(FooterMoneyText)); } }
        }

        [DataSourceProperty]
        public string FooterFoodText
        {
            get => _footerFoodText;
            set { if (value != _footerFoodText) { _footerFoodText = value; OnPropertyChangedWithValue(value, nameof(FooterFoodText)); } }
        }

        [DataSourceProperty]
        public string FooterWeightText
        {
            get => _footerWeightText;
            set { if (value != _footerWeightText) { _footerWeightText = value; OnPropertyChangedWithValue(value, nameof(FooterWeightText)); } }
        }

        /// <summary>"Party 99/96" — the party after the deal against its size limit.</summary>
        [DataSourceProperty]
        public string FooterPartyText
        {
            get => _footerPartyText;
            set { if (value != _footerPartyText) { _footerPartyText = value; OnPropertyChangedWithValue(value, nameof(FooterPartyText)); } }
        }

        /// <summary>Red when the party after the deal is over its size limit.</summary>
        [DataSourceProperty]
        public string FooterPartyColor
        {
            get => _footerPartyColor;
            set { if (value != _footerPartyColor) { _footerPartyColor = value; OnPropertyChangedWithValue(value, nameof(FooterPartyColor)); } }
        }

        [DataSourceProperty]
        public string WarningText
        {
            get => _warningText;
            set { if (value != _warningText) { _warningText = value; OnPropertyChangedWithValue(value, nameof(WarningText)); } }
        }

        [DataSourceProperty]
        public bool HasWarnings
        {
            get => _hasWarnings;
            set { if (value != _hasWarnings) { _hasWarnings = value; OnPropertyChangedWithValue(value, nameof(HasWarnings)); } }
        }

        [DataSourceProperty]
        public string StatusText
        {
            get => _statusText;
            set { if (value != _statusText) { _statusText = value; OnPropertyChangedWithValue(value, nameof(StatusText)); } }
        }

        [DataSourceProperty]
        public bool HasStatus
        {
            get => _hasStatus;
            set { if (value != _hasStatus) { _hasStatus = value; OnPropertyChangedWithValue(value, nameof(HasStatus)); } }
        }

        [DataSourceProperty]
        public bool IsEmpty
        {
            get => _isEmpty;
            set { if (value != _isEmpty) { _isEmpty = value; OnPropertyChangedWithValue(value, nameof(IsEmpty)); OnPropertyChanged(nameof(ShowTable)); } }
        }

        /// <summary>The table shows unless the tab is empty (nothing planned, or a closed market with nothing to do).</summary>
        [DataSourceProperty] public bool ShowTable => !_isEmpty;

        /// <summary>"Nothing for the steward to do here." — or, at a closed market, why it is closed.</summary>
        [DataSourceProperty]
        public string EmptyText
        {
            get => _emptyText;
            set { if (value != _emptyText) { _emptyText = value; OnPropertyChangedWithValue(value, nameof(EmptyText)); } }
        }

        /// <summary>"Market closed: …" (empty while the market is open).</summary>
        [DataSourceProperty]
        public string MarketClosedText
        {
            get => _marketClosedText;
            set { if (value != _marketClosedText) { _marketClosedText = value; OnPropertyChangedWithValue(value, nameof(MarketClosedText)); } }
        }

        /// <summary>The closed-market line shows at the top (the table shows too — there is something to do).</summary>
        [DataSourceProperty]
        public bool HasMarketNotice
        {
            get => _hasMarketNotice;
            set { if (value != _hasMarketNotice) { _hasMarketNotice = value; OnPropertyChangedWithValue(value, nameof(HasMarketNotice)); } }
        }

        [DataSourceProperty]
        public bool CanResetAll
        {
            get => _canResetAll;
            set { if (value != _canResetAll) { _canResetAll = value; OnPropertyChangedWithValue(value, nameof(CanResetAll)); } }
        }
    }

    /// <summary>One section of the Suggestion table: its header row and its rows.</summary>
    public sealed class SectionVM : ViewModel
    {
        private readonly PlanSection _section;
        private readonly StewardPlan _plan;
        private string _detailText = "";

        internal SectionVM(PlanSection section, StewardPlan plan, SuggestionTabVM tab)
        {
            _section = section;
            _plan = plan;
            TitleText = UiLabels.Section(section.Kind);
            Rows = new MBBindingList<SuggestionRowVM>();
            foreach (var row in section.Rows)
                Rows.Add(new SuggestionRowVM(row, tab));
        }

        internal void Refresh()
        {
            foreach (var row in Rows)
                row.Refresh();
            var facts = _plan.Facts;
            switch (_section.Kind)
            {
                case PlanSectionKind.Food:
                    DetailText = UiText.S2("ss_ui_sec_food_detail", "target {TARGET} for {EATERS} eaters",
                        "TARGET", UiFormat.Money(facts.FoodTarget), "EATERS", UiFormat.Money(facts.FoodEaters));
                    break;
                case PlanSectionKind.Mounts:
                    DetailText = UiText.S2("ss_ui_sec_mounts_detail", "{FOOTMEN} men on foot  ·  riding target {TARGET}",
                        "FOOTMEN", UiFormat.Money(facts.Footmen), "TARGET", UiFormat.Money(facts.RidingTarget));
                    break;
                default:
                    DetailText = "";
                    break;
            }
        }

        [DataSourceProperty] public string TitleText { get; }

        [DataSourceProperty] public string HeadingColor => UiColors.Heading;

        [DataSourceProperty] public MBBindingList<SuggestionRowVM> Rows { get; }

        [DataSourceProperty]
        public string DetailText
        {
            get => _detailText;
            set { if (value != _detailText) { _detailText = value; OnPropertyChangedWithValue(value, nameof(DetailText)); } }
        }
    }

    /// <summary>
    /// One table row. Its numbers come from Core's <see cref="RowCells"/>, its buttons from the plan editor's live
    /// blocks (a greyed button's tooltip says why), its words from TextObjects.
    /// </summary>
    public sealed class SuggestionRowVM : ViewModel
    {
        private readonly PlanRow _row;
        private readonly SuggestionTabVM _tab;

        private string _mineText = "";
        private string _changeText = "";
        private string _changeColor = UiColors.Muted;
        private string _resultText = "";
        private string _priceText = "";
        private string _marketText = "";
        private string _detailText = "";
        private bool _canIncrease;
        private bool _canDecrease;
        private bool _canReset;
        private bool _isExpanded;
        private bool _hasBreakdown;
        private MBBindingList<BreakdownLineVM> _breakdown = new MBBindingList<BreakdownLineVM>();

        internal SuggestionRowVM(PlanRow row, SuggestionTabVM tab)
        {
            _row = row;
            _tab = tab;
            NameText = UiLabels.RowName(row);
            TypeText = UiLabels.Type(row.Type);
            IsLink = row.Type == RowType.Tavern;
            IsPlainName = !IsLink;
            IncreaseHint = new HintVM();
            DecreaseHint = new HintVM();
            ResetHint = new HintVM(UiText.S("ss_ui_reset_hint", "Back to the steward's suggestion"));
            LinkHint = new HintVM(UiText.S("ss_ui_link_hint", "Open in the Encyclopedia"));
            ExpandHint = new HintVM(UiText.S("ss_ui_expand_hint", "Show the kinds of animal in this row"));
        }

        internal void Refresh()
        {
            var cells = RowCells.Of(_row);
            string mine = cells.Mine;
            if (cells.Locked > 0)
                mine += " " + UiText.S1("ss_ui_mine_locked", "(+{N} locked)", "N", UiFormat.Money(cells.Locked));
            if (cells.OverValueCap > 0)
                mine += " " + UiText.S1("ss_ui_mine_kept", "(+{N} kept)", "N", UiFormat.Money(cells.OverValueCap));
            MineText = mine;
            ChangeText = cells.Change;
            ChangeColor = cells.Color;
            ResultText = cells.Result;
            string price = cells.Price;
            if (cells.Influence != null)
                price = (price.Length > 0 ? price + "  " : "")
                        + UiText.S1("ss_ui_price_influence", "{INF} influence", "INF", cells.Influence);
            PriceText = price;
            MarketText = cells.Market;
            DetailText = Detail(cells);

            var increase = _row.IncreaseBlock;
            var decrease = _row.DecreaseBlock;
            CanIncrease = increase == EditBlock.None;
            CanDecrease = decrease == EditBlock.None;
            IncreaseHint.Text = UiLabels.Block(increase);
            DecreaseHint.Text = UiLabels.Block(decrease);
            CanReset = _row.IsEdited;

            HasBreakdown = cells.HasBreakdown;
            RefreshBreakdown();
        }

        /// <summary>The ▸ lines exist only while the row is open: Gauntlet builds a widget row for every item of a
        /// list whether it shows or not, and every click refreshes every row (PLAN step 9, review area 5).</summary>
        private void RefreshBreakdown()
        {
            if (!(HasBreakdown && IsExpanded))
            {
                if (Breakdown.Count > 0)
                    Breakdown = new MBBindingList<BreakdownLineVM>();
                return;
            }
            var lines = new MBBindingList<BreakdownLineVM>();
            foreach (var line in _row.Breakdown)
                lines.Add(new BreakdownLineVM(line));
            Breakdown = lines;
        }

        /// <summary>The small grey words after the name: targets, the weight a sale frees, a wanderer's skills and wage.</summary>
        private string Detail(RowCells cells)
        {
            var parts = new List<string>();
            if (_row.Target != null)
                parts.Add(UiText.S1("ss_ui_detail_target", "target {N}", "N", UiFormat.Money(_row.Target.Value)));
            if (_row.Need != null)
                parts.Add(UiText.S1("ss_ui_detail_need", "needed for upgrades {N}", "N", UiFormat.Money(_row.Need.Value)));
            if (cells.WeightFreed != null)
                parts.Add(UiText.S1("ss_ui_detail_frees", "{KG} kg", "KG", cells.WeightFreed));
            var p = _row.Prisoner;
            if (p != null)
            {
                if (p.IsHero) parts.Add(UiText.S("ss_ui_detail_lord", "lord"));
                if (p.DonateCount > 0)
                    parts.Add(UiText.S1("ss_ui_detail_donated", "{N} to the dungeon", "N", UiFormat.Money(p.DonateCount)));
            }
            var t = _row.Tavern;
            if (t != null)
            {
                if (!string.IsNullOrEmpty(t.SkillTag)) parts.Add(t.SkillTag!);
                parts.Add(t.Kind == TavernRowKind.Mercenaries
                    ? UiText.S1("ss_ui_detail_wage_each", "{WAGE} a day each", "WAGE", UiFormat.Money(t.DailyWage))
                    : UiText.S1("ss_ui_detail_wage", "{WAGE} a day", "WAGE", UiFormat.Money(t.DailyWage)));
            }
            return string.Join("  ·  ", parts);
        }

        // ── commands ─────────────────────────────────────────────────────────────────────────────────────

        public void ExecuteIncrease() => StewardWindowVM.Guard("[+] " + _row.Id, () => _tab.Edit(_row, +1));

        public void ExecuteDecrease() => StewardWindowVM.Guard("[-] " + _row.Id, () => _tab.Edit(_row, -1));

        public void ExecuteReset() => StewardWindowVM.Guard("reset " + _row.Id, () => _tab.Reset(_row));

        public void ExecuteToggleBreakdown() => StewardWindowVM.Guard("breakdown " + _row.Id, () =>
        {
            IsExpanded = !IsExpanded;
            RefreshBreakdown();
        });

        public void ExecuteOpenLink() => StewardWindowVM.Guard("link " + _row.Id, () => _tab.OpenLink(_row));

        // ── bound properties ─────────────────────────────────────────────────────────────────────────────

        [DataSourceProperty] public string NameText { get; }
        [DataSourceProperty] public string TypeText { get; }
        [DataSourceProperty] public bool IsLink { get; }
        [DataSourceProperty] public bool IsPlainName { get; }
        [DataSourceProperty] public string LinkColor => UiColors.Link;
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public HintVM IncreaseHint { get; }
        [DataSourceProperty] public HintVM DecreaseHint { get; }
        [DataSourceProperty] public HintVM ResetHint { get; }
        [DataSourceProperty] public HintVM LinkHint { get; }
        [DataSourceProperty] public HintVM ExpandHint { get; }

        [DataSourceProperty]
        public string MineText
        {
            get => _mineText;
            set { if (value != _mineText) { _mineText = value; OnPropertyChangedWithValue(value, nameof(MineText)); } }
        }

        [DataSourceProperty]
        public string ChangeText
        {
            get => _changeText;
            set { if (value != _changeText) { _changeText = value; OnPropertyChangedWithValue(value, nameof(ChangeText)); } }
        }

        [DataSourceProperty]
        public string ChangeColor
        {
            get => _changeColor;
            set { if (value != _changeColor) { _changeColor = value; OnPropertyChangedWithValue(value, nameof(ChangeColor)); } }
        }

        [DataSourceProperty]
        public string ResultText
        {
            get => _resultText;
            set { if (value != _resultText) { _resultText = value; OnPropertyChangedWithValue(value, nameof(ResultText)); } }
        }

        [DataSourceProperty]
        public string PriceText
        {
            get => _priceText;
            set { if (value != _priceText) { _priceText = value; OnPropertyChangedWithValue(value, nameof(PriceText)); } }
        }

        [DataSourceProperty]
        public string MarketText
        {
            get => _marketText;
            set { if (value != _marketText) { _marketText = value; OnPropertyChangedWithValue(value, nameof(MarketText)); } }
        }

        [DataSourceProperty]
        public string DetailText
        {
            get => _detailText;
            set { if (value != _detailText) { _detailText = value; OnPropertyChangedWithValue(value, nameof(DetailText)); } }
        }

        [DataSourceProperty]
        public bool CanIncrease
        {
            get => _canIncrease;
            set { if (value != _canIncrease) { _canIncrease = value; OnPropertyChangedWithValue(value, nameof(CanIncrease)); } }
        }

        [DataSourceProperty]
        public bool CanDecrease
        {
            get => _canDecrease;
            set { if (value != _canDecrease) { _canDecrease = value; OnPropertyChangedWithValue(value, nameof(CanDecrease)); } }
        }

        [DataSourceProperty]
        public bool CanReset
        {
            get => _canReset;
            set { if (value != _canReset) { _canReset = value; OnPropertyChangedWithValue(value, nameof(CanReset)); } }
        }

        [DataSourceProperty]
        public bool HasBreakdown
        {
            get => _hasBreakdown;
            set { if (value != _hasBreakdown) { _hasBreakdown = value; OnPropertyChangedWithValue(value, nameof(HasBreakdown)); OnPropertyChanged(nameof(IsCollapsedIndicator)); OnPropertyChanged(nameof(IsExpandedIndicator)); } }
        }

        [DataSourceProperty]
        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (value != _isExpanded) { _isExpanded = value; OnPropertyChangedWithValue(value, nameof(IsExpanded)); OnPropertyChanged(nameof(IsCollapsedIndicator)); OnPropertyChanged(nameof(IsExpandedIndicator)); } }
        }

        /// <summary>The ▸ sprite (a collapsed row with a breakdown).</summary>
        [DataSourceProperty] public bool IsCollapsedIndicator => _hasBreakdown && !_isExpanded;

        /// <summary>The ▾ sprite (an expanded row).</summary>
        [DataSourceProperty] public bool IsExpandedIndicator => _hasBreakdown && _isExpanded;

        [DataSourceProperty]
        public MBBindingList<BreakdownLineVM> Breakdown
        {
            get => _breakdown;
            set { if (value != _breakdown) { _breakdown = value; OnPropertyChangedWithValue(value, nameof(Breakdown)); } }
        }
    }

    /// <summary>One kind of animal inside a mount role row (the ▸ breakdown, DESIGN §1.1.1).</summary>
    public sealed class BreakdownLineVM : ViewModel
    {
        internal BreakdownLineVM(PlanRowLine line)
        {
            var cells = RowCells.Of(line);
            MineText = cells.Mine;
            ChangeText = cells.Change;
            ChangeColor = cells.Color;
            ResultText = cells.Result;
            PriceText = cells.Price;
            MarketText = cells.Market;
            NameText = string.IsNullOrEmpty(line.Name) ? line.ItemId : line.Name;
        }

        [DataSourceProperty] public string MineText { get; }
        [DataSourceProperty] public string ChangeText { get; }
        [DataSourceProperty] public string ChangeColor { get; }
        [DataSourceProperty] public string ResultText { get; }
        [DataSourceProperty] public string PriceText { get; }
        [DataSourceProperty] public string MarketText { get; }
        [DataSourceProperty] public string NameText { get; }
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
    }
}
