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

        /// <summary>The plan's <see cref="StewardPlan.Layout"/> the table was built for — a live re-plan that adds or removes a
        /// row bumps it, and the table is built again (step 15).</summary>
        private int _layout;

        private MBBindingList<SectionVM> _sections = new MBBindingList<SectionVM>();
        private string _headerText = "";
        private string _headerColor = UiColors.Muted;
        private string _footerMoneyText = "";
        private string _footerFoodText = "";
        private string _footerWeightText = "";
        private string _footerOverText = "";
        private bool _hasFooterOver;
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

        /// <summary>The window is on screen: the wanderers it lists are learned about (round 3, <see cref="Adapter.TavernKnowledge"/>).</summary>
        private bool _shown;

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
            Words = new SummaryWords
            {
                Kind = UiText.S("ss_ui_sum_kind", "kind"),
                Kinds = UiText.S("ss_ui_sum_kinds", "kinds"),
                Sold = UiText.S("ss_ui_sum_sold", "sold"),
                Hired = UiText.S("ss_ui_sum_hired", "hired"),
                Recruited = UiText.S("ss_ui_sum_recruited", "recruited"),
                Dismissed = UiText.S("ss_ui_sum_dismissed", "dismissed"),
                Ransomed = UiText.S("ss_ui_sum_ransomed", "ransomed"),
                ToDungeon = UiText.S("ss_ui_sum_to_dungeon", "to the dungeon"),
                Influence = UiText.S("ss_ui_sum_influence", "influence"),
                Days = UiText.S("ss_ui_sum_days", "days"),
                Kg = UiText.S("ss_ui_sum_kg", "kg"),
                NoChange = UiText.S("ss_ui_sum_no_change", "no change"),
                NobodyHired = UiText.S("ss_ui_sum_nobody_hired", "nobody hired"),
                NoTroopChange = UiText.S("ss_ui_sum_no_troop_change", "nobody recruited or dismissed"),
                NothingSold = UiText.S("ss_ui_sum_nothing_sold", "nothing sold"),
                NobodyRansomed = UiText.S("ss_ui_sum_nobody_ransomed", "nobody ransomed"),
            };
        }

        /// <summary>The words of a folded section's line (step 18) — Core builds the line, the words come from TextObjects.</summary>
        internal SummaryWords Words { get; }

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
            BuildSections(plan);
            Refresh();
            if (_shown)
                Adapter.TavernKnowledge.LearnAboutListed(_settlement, plan);
        }

        /// <summary>The table's sections and rows for the plan as it is; rows that were open (▸) stay open.</summary>
        private void BuildSections(StewardPlan plan)
        {
            var open = new HashSet<string>(StringComparer.Ordinal);
            foreach (var section in Sections)
                foreach (var row in section.Rows)
                    if (row.IsExpanded)
                        open.Add(row.RowId);
            var sections = new MBBindingList<SectionVM>();
            var heads = new HashSet<SectionGroup>();
            foreach (var section in plan.Sections)
                sections.Add(new SectionVM(section, plan, this, heads.Add(SectionGroups.Of(section.Kind)), open));
            Sections = sections;
            _layout = plan.Layout;
        }

        /// <summary>
        /// A section header was clicked (step 18 — Anton 2026.09.28): its group folds to one summary line or unfolds, and the
        /// window remembers it — across windows, towns and restarts (<see cref="WindowStateHost"/>, never the save). The troops
        /// section's two halves fold together.
        /// </summary>
        internal void ToggleGroup(SectionVM clicked)
        {
            bool collapse = !clicked.IsCollapsed;
            WindowStateHost.SetCollapsed(clicked.Group, collapse);
            foreach (var section in Sections)
                if (section.Group == clicked.Group)
                    section.SetCollapsed(collapse);
            ModLog.Info("window", (collapse ? "folded " : "unfolded ") + clicked.Group);
        }

        /// <summary>The window is on screen now (not an arrival popup that stayed shut): the player sees the tavern
        /// section's wanderers — as the tavern district would show them, the game learns about them (RESEARCH §20).</summary>
        internal void MarkShown()
        {
            _shown = true;
            Adapter.TavernKnowledge.LearnAboutListed(_settlement, _plan);
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

            string money = UiText.S2("ss_ui_footer_money", "Spent {SPENT}  ·  earned {EARNED}",
                "SPENT", UiFormat.Money(t.Spent), "EARNED", UiFormat.Money(t.Earned));
            if (t.InfluenceGained > 0.05)
                money += "  ·  " + UiText.S1("ss_ui_footer_influence", "Influence {INF}", "INF", UiFormat.SignedInfluence(t.InfluenceGained));
            FooterMoneyText = money;
            FooterFoodText = t.FoodDaysAfter == null
                ? UiText.S2("ss_ui_footer_food_nodays", "Food {NOW} » {AFTER}",
                    "NOW", UiFormat.Money(t.FoodUnitsNow), "AFTER", UiFormat.Money(t.FoodUnitsAfter))
                : UiText.S3("ss_ui_footer_food", "Food {NOW} » {AFTER}  (~{DAYS} days)",
                    "NOW", UiFormat.Money(t.FoodUnitsNow), "AFTER", UiFormat.Money(t.FoodUnitsAfter), "DAYS", UiFormat.Days(t.FoodDaysAfter));
            // The party after the deal against its size limit — information, never a wall (round 3): red when over.
            FooterPartyText = UiText.S2("ss_ui_footer_party", "Party {AFTER}/{LIMIT}",
                "AFTER", UiFormat.Money(t.MembersAfter), "LIMIT", UiFormat.Money(t.PartySizeLimit));
            FooterPartyColor = t.OverPartyLimit ? UiColors.Warning : UiColors.Text;
            RefreshWeightLine(t);

            var floors = plan.Floors; // the floors the flags were computed against
            var warnings = PlanFooter.Warnings(t);
            WarningText = string.Join("   ", warnings.Select(w => UiLabels.Warning(w, floors.All, floors.Animals)));
            HasWarnings = warnings.Count > 0;
            bool closed = MarketClosedText.Length > 0;
            IsEmpty = !PlanFooter.ShowsTable(plan, closed); // a closed market keeps the rows you can still act on (step 16)
            EmptyText = closed ? MarketClosedText : _nothingToDoText;
            HasMarketNotice = closed && !IsEmpty;
            CanResetAll = plan.IsEdited;
        }

        /// <summary>The weight line (round 3), like the food line: the load now, the change and the load after, then the
        /// capacity AFTER the deal — land, and sea when the party has ships (War Sails) — and, in red beside it, how far the
        /// deal takes the load over a capacity. Without a capacity read (a failed read) only the change shows.</summary>
        private void RefreshWeightLine(PlanTotals t)
        {
            var c = t.Carry;
            if (!c.Known)
            {
                FooterWeightText = UiText.S1("ss_ui_footer_weight", "Weight {KG} kg", "KG", UiFormat.SignedWeight(t.WeightChange));
                FooterOverText = "";
                HasFooterOver = false;
                return;
            }
            string line = UiFormat.SignedWeight(t.WeightChange) == "0"
                ? UiText.S1("ss_ui_footer_load_same", "Weight {NOW} kg", "NOW", UiFormat.Kg(c.WeightNow))
                : UiText.S3("ss_ui_footer_load", "Weight {NOW} {CHANGE} kg » {AFTER} kg",
                    "NOW", UiFormat.Kg(c.WeightNow), "CHANGE", UiFormat.SignedWeight(t.WeightChange), "AFTER", UiFormat.Kg(c.WeightAfter));
            line += "  ·  " + (c.ShowSea
                ? UiText.S2("ss_ui_footer_capacity_sea", "capacity land {LAND} / sea {SEA}",
                    "LAND", UiFormat.Kg(c.CapacityLandAfter), "SEA", UiFormat.Kg(c.CapacitySeaAfter))
                : UiText.S1("ss_ui_footer_capacity", "capacity {LAND}", "LAND", UiFormat.Kg(c.CapacityLandAfter)));
            if (c.SeaLoadDiffers)
                line += " " + UiText.S1("ss_ui_footer_sea_load", "({KG} kg at sea)", "KG", UiFormat.Kg(c.WeightAtSeaAfter));
            FooterWeightText = line;

            var over = new List<string>();
            if (c.OverLand > 0)
                over.Add(c.ShowSea
                    ? UiText.S1("ss_ui_footer_over_land", "{KG} over on land", "KG", UiFormat.KgOver(c.OverLand))
                    : UiText.S1("ss_ui_footer_over", "{KG} over", "KG", UiFormat.KgOver(c.OverLand)));
            if (c.OverSea > 0)
                over.Add(UiText.S1("ss_ui_footer_over_sea", "{KG} over at sea", "KG", UiFormat.KgOver(c.OverSea)));
            FooterOverText = string.Join("  ·  ", over);
            HasFooterOver = over.Count > 0;
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
            var facts = plan.Facts;
            var result = direction > 0 ? plan.Increase(row.Id, size) : plan.Decrease(row.Id, size);
            ModLog.Info("window", (direction > 0 ? "[+] " : "[-] ") + size + " " + row.Id + ": " + result.Before + " -> "
                                  + result.After + (result.Block == EditBlock.None ? "" : " (" + result.Block + ")"));
            AfterEdit(facts);
        }

        internal void Reset(PlanRow row)
        {
            var plan = _plan;
            if (plan == null)
                return;
            var facts = plan.Facts;
            var result = plan.Reset(row.Id);
            ModLog.Info("window", "reset " + row.Id + ": " + result.Before + " -> " + result.After + " (the steward's again)");
            AfterEdit(facts);
        }

        public void ExecuteResetAll() => StewardWindowVM.Guard("reset all", () =>
        {
            if (_plan == null)
                return;
            var facts = _plan.Facts;
            _plan.ResetAll();
            ModLog.Info("window", "reset all");
            AfterEdit(facts);
        });

        /// <summary>After any edit: a live re-plan (step 15) is logged with the party it planned for, a table whose rows
        /// changed is built again, then everything reads the plan.</summary>
        private void AfterEdit(PlanFacts factsBefore)
        {
            var plan = _plan;
            if (plan != null && !ReferenceEquals(plan.Facts, factsBefore))
            {
                var f = plan.Facts;
                ModLog.Info("window", "re-planned for the party after the deal: food target " + f.FoodTarget + " for " + f.FoodEaters
                                      + " eaters (was " + factsBefore.FoodTarget + "), " + f.Footmen + " footmen, horses to keep "
                                      + f.MountTarget + " (was " + factsBefore.MountTarget + "), riding target " + f.RidingTarget
                                      + ", war horses " + f.WarTarget);
            }
            if (plan != null && plan.Layout != _layout)
                BuildSections(plan);
            SetStatus("");
            Refresh();
            _onPlanChanged();
        }

        /// <summary>A wanderer's, the mercenary troop's or a troop row's Encyclopedia page (DESIGN §2.7, §2.8).</summary>
        internal void OpenLink(PlanRow row)
        {
            string? link = null;
            if (row.Tavern?.Kind == TavernRowKind.Wanderer && row.HeroId != null)
            {
                Adapter.TavernKnowledge.Learn(_settlement, row.HeroId); // a known hero's page, never "???" (round 3)
                link = _settlement?.HeroesWithoutParty.FirstOrDefault(h => h != null && h.StringId == row.HeroId)?.EncyclopediaLink;
            }
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

        /// <summary>The part of the load over a capacity after the deal: "+120 over at sea" (red).</summary>
        [DataSourceProperty]
        public string FooterOverText
        {
            get => _footerOverText;
            set { if (value != _footerOverText) { _footerOverText = value; OnPropertyChangedWithValue(value, nameof(FooterOverText)); } }
        }

        [DataSourceProperty]
        public bool HasFooterOver
        {
            get => _hasFooterOver;
            set { if (value != _hasFooterOver) { _hasFooterOver = value; OnPropertyChangedWithValue(value, nameof(HasFooterOver)); } }
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

    /// <summary>
    /// One section of the Suggestion table: its header row and its rows. Step 18 (Anton 2026.09.28): a click on the header
    /// folds the section to ONE line — the section's name and what it will do with its gold (Core <see cref="SectionSummary"/>)
    /// — and unfolds it again; the fold is remembered per section (<see cref="WindowStateHost"/>). The troops section's two
    /// halves are one group: folded, the first of them (the group's head) shows the line and the other hides.
    /// </summary>
    public sealed class SectionVM : ViewModel
    {
        private readonly PlanSection _section;
        private readonly StewardPlan _plan;
        private readonly SuggestionTabVM _tab;
        private readonly string _sectionTitle;
        private string _titleText;
        private string _detailText = "";
        private string _summaryText = "";
        private bool _isCollapsed;

        internal SectionVM(PlanSection section, StewardPlan plan, SuggestionTabVM tab, bool isGroupHead, ISet<string>? open = null)
        {
            _section = section;
            _plan = plan;
            _tab = tab;
            Group = SectionGroups.Of(section.Kind);
            IsGroupHead = isGroupHead;
            _sectionTitle = UiLabels.Section(section.Kind);
            _titleText = _sectionTitle;
            _isCollapsed = WindowStateHost.IsCollapsed(Group);
            ToggleHint = new HintVM();
            Rows = new MBBindingList<SuggestionRowVM>();
            foreach (var row in section.Rows)
                Rows.Add(new SuggestionRowVM(row, tab) { IsExpanded = open != null && open.Contains(row.Id) });
        }

        /// <summary>The fold group (the troops' two halves share one).</summary>
        internal SectionGroup Group { get; }

        /// <summary>The first section of its group in the table — the one that shows the folded line.</summary>
        internal bool IsGroupHead { get; }

        internal void SetCollapsed(bool collapsed)
        {
            IsCollapsed = collapsed;
            Refresh();
        }

        internal void Refresh()
        {
            ToggleHint.Text = _isCollapsed
                ? UiText.S("ss_ui_unfold_hint", "Show this section's rows. The steward remembers it, even after a restart.")
                : UiText.S("ss_ui_fold_hint", "Fold this section to one line. The steward remembers it, even after a restart.");
            if (_isCollapsed)
            {
                // Folded: the rows are hidden and not refreshed (every row's live buttons cost a trial walk — step 9).
                TitleText = Group == SectionGroup.Troops ? UiText.S("ss_ui_sec_troops_group", "Troops") : _sectionTitle;
                SummaryText = IsGroupHead ? SectionSummary.Of(_plan, Group, _tab.Words) : "";
                DetailText = "";
                return;
            }
            TitleText = _sectionTitle;
            SummaryText = "";
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
                    // Step 17: every horse kept counts for the footmen - war, noble and lame ones kept too (Anton 2026.09.28).
                    DetailText = UiText.S2("ss_ui_sec_mounts_detail", "{FOOTMEN} men on foot  ·  {TOTAL} horses to keep",
                        "FOOTMEN", UiFormat.Money(facts.Footmen), "TOTAL", UiFormat.Money(facts.MountTarget));
                    break;
                case PlanSectionKind.Recruits:
                    DetailText = UiText.S("ss_ui_sec_recruits_detail", "[+] recruits  ·  [–] dismisses yours");
                    break;
                case PlanSectionKind.Troops:
                    DetailText = UiText.S("ss_ui_sec_troops_detail", "[–] dismisses, the wounded first");
                    break;
                default:
                    DetailText = "";
                    break;
            }
        }

        // ── commands ─────────────────────────────────────────────────────────────────────────────────────

        public void ExecuteToggle() => StewardWindowVM.Guard("fold " + Group, () => _tab.ToggleGroup(this));

        // ── bound properties ─────────────────────────────────────────────────────────────────────────────

        [DataSourceProperty]
        public string TitleText
        {
            get => _titleText;
            set { if (value != _titleText) { _titleText = value; OnPropertyChangedWithValue(value, nameof(TitleText)); } }
        }

        [DataSourceProperty] public string HeadingColor => UiColors.Heading;

        [DataSourceProperty] public string TextColor => UiColors.Text;

        [DataSourceProperty] public HintVM ToggleHint { get; }

        [DataSourceProperty] public MBBindingList<SuggestionRowVM> Rows { get; }

        [DataSourceProperty]
        public string DetailText
        {
            get => _detailText;
            set { if (value != _detailText) { _detailText = value; OnPropertyChangedWithValue(value, nameof(DetailText)); } }
        }

        /// <summary>The folded line: what the section will do and its gold (empty while unfolded).</summary>
        [DataSourceProperty]
        public string SummaryText
        {
            get => _summaryText;
            set { if (value != _summaryText) { _summaryText = value; OnPropertyChangedWithValue(value, nameof(SummaryText)); } }
        }

        /// <summary>Folded to one line (remembered per section — step 18).</summary>
        [DataSourceProperty]
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (value != _isCollapsed)
                {
                    _isCollapsed = value;
                    OnPropertyChangedWithValue(value, nameof(IsCollapsed));
                    OnPropertyChanged(nameof(IsExpanded));
                    OnPropertyChanged(nameof(ShowSection));
                }
            }
        }

        /// <summary>Unfolded: the rows and the header's small print show.</summary>
        [DataSourceProperty] public bool IsExpanded => !_isCollapsed;

        /// <summary>A folded group shows only its head (the troops' second half hides).</summary>
        [DataSourceProperty] public bool ShowSection => IsGroupHead || !_isCollapsed;
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
            IsLink = row.Type == RowType.Tavern || row.Type == RowType.Troop; // troop names open their unit page (step 16)
            IsPlainName = !IsLink;
            IncreaseHint = new HintVM();
            DecreaseHint = new HintVM();
            ResetHint = new HintVM(UiText.S("ss_ui_reset_hint", "Your number - the steward plans around it. Click to hand the row back to the steward."));
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
            CanReset = _row.IsTouched; // the ⟲ shows on the rows the player's hand is on (step 15)

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
            if (_row.Role == MountRole.Noble)
                parts.Add(UiText.S("ss_ui_detail_noble", "yours and your companions' - sold unless locked"));
            if (_row.Role == MountRole.Lame)
                parts.Add(UiText.S("ss_ui_detail_lame", "sold - healthy ones take their place"));
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
            var troop = _row.Troop;
            if (troop != null)
            {
                parts.Add(UiText.S1("ss_ui_detail_wage_each", "{WAGE} a day each", "WAGE", UiFormat.Money(troop.DailyWage)));
                if (troop.Wounded > 0)
                    parts.Add(UiText.S1("ss_ui_detail_wounded", "{N} wounded", "N", UiFormat.Money(troop.Wounded)));
            }
            return string.Join("  ·  ", parts);
        }

        /// <summary>The plan row's id (kept open across a table rebuild).</summary>
        internal string RowId => _row.Id;

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
