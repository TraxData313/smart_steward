using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.ObjectSystem;

namespace SmartSteward.UI
{
    /// <summary>
    /// The Suggestion tab as ONE spreadsheet (PLAN step 21 — the mockup Anton approved on 2026.09.28, docs/mockups/README.md,
    /// DESIGN §1.1): the denari header, the columns Market · Item · Mine · Change · Result · Denari · Party · Prisoners · Land kg
    /// · Sea kg, the sections Troops · Food · Horses · Prisoners · Other whose title line is their subtotal, the lines under
    /// them as the folds leave them. Core's <see cref="SheetView"/> decides every line, cell, colour and button state; this VM
    /// only copies them and turns clicks into plan edits. After every click the view is built again and each line's VM is
    /// updated in place while its key stays — a fold inserts or removes only the lines it opens or closes.
    /// </summary>
    public sealed class SuggestionTabVM : ViewModel
    {
        private readonly Action _onPlanChanged;
        private StewardPlan? _plan;
        private Settlement? _settlement;

        private MBBindingList<SheetSectionVM> _sections = new MBBindingList<SheetSectionVM>();
        private string _headerFlowText = "";
        private string _headerChangeText = "";
        private string _headerChangeColor = UiColors.Muted;
        private string _headerInfluenceText = "";
        private bool _showSea;
        private string _footerMoneyText = "";
        private string _footerFoodText = "";
        private string _footerLandText = "";
        private string _footerLandOverText = "";
        private bool _hasFooterLandOver;
        private string _footerSeaText = "";
        private string _footerSeaOverText = "";
        private bool _hasFooterSeaOver;
        private bool _hasSeaLine;
        private string _footerHerdText = "";
        private string _footerHerdColor = UiColors.Text;
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
            Words = UiLabels.SheetText();
            ColMarket = UiText.S("ss_ui_col_market", "Market");
            ColItem = UiText.S("ss_ui_col_item", "Item");
            ColMine = UiText.S("ss_ui_col_mine", "Mine");
            ColChange = UiText.S("ss_ui_col_change", "Change");
            ColResult = UiText.S("ss_ui_col_result", "Result");
            ColDenari = UiText.S("ss_ui_col_denari", "Denari");
            ColParty = UiText.S("ss_ui_col_party", "Party");
            ColPrisoners = UiText.S("ss_ui_col_prisoners", "Prisoners");
            ColLand = UiText.S("ss_ui_col_land_kg", "Land kg");
            ColSea = UiText.S("ss_ui_col_sea_kg", "Sea kg");
            HeaderLabel = UiText.S("ss_ui_header_denari", "Denari");
            _nothingToDoText = UiText.S("ss_ui_empty", "Nothing for the steward to do here.");
            _emptyText = _nothingToDoText;
            ShortcutText = UiText.S("ss_ui_shortcuts", "Click ±1  ·  Shift ±5  ·  Ctrl all  ·  names in gold open the Encyclopedia");
            ResetAllText = UiText.S("ss_ui_reset_all", "Reset all");
            LandLabel = UiText.S("ss_ui_footer_label_land", "Land:");
            SeaLabel = UiText.S("ss_ui_footer_label_sea", "Sea:");
        }

        /// <summary>The spreadsheet's words (Core builds the texts, the words come from TextObjects).</summary>
        internal SheetWords Words { get; }

        internal StewardPlan? Plan => _plan;

        /// <summary>Shows a (new) plan. A closed market (the snapshot's <c>TradeClosedReason</c>, the game's own words) is said at
        /// the top — or, with nothing to do, instead of the table (playtest round 1).</summary>
        internal void SetPlan(StewardPlan plan, Adapter.GameVisit visit)
        {
            _plan = plan;
            _settlement = visit.Settlement;
            var snap = visit.Snapshot;
            MarketClosedText = snap.CanTrade ? ""
                : UiText.S1("ss_ui_market_closed", "Market closed: {REASON}", "REASON", snap.TradeClosedReason ?? "");
            Refresh();
            if (_shown)
                Adapter.TavernKnowledge.LearnAboutListed(_settlement, plan);
        }

        /// <summary>The window is on screen now (not an arrival popup that stayed shut): the player sees the tavern
        /// section's wanderers — as the tavern district would show them, the game learns about them (RESEARCH §20).</summary>
        internal void MarkShown()
        {
            _shown = true;
            Adapter.TavernKnowledge.LearnAboutListed(_settlement, _plan);
        }

        /// <summary>After any edit or fold: the spreadsheet is built again from the plan and every line reads it.</summary>
        internal void Refresh()
        {
            var plan = _plan;
            if (plan == null)
                return;
            var view = SheetView.Build(plan, Words, WindowStateHost.IsFolded);
            var sheet = view.Sheet;
            HeaderFlowText = sheet.DenariFlowText;
            HeaderChangeText = sheet.DenariChangeText;
            HeaderChangeColor = UiColors.ForMoney(sheet.DenariChange);
            HeaderInfluenceText = sheet.InfluenceText;
            ShowSea = sheet.ShowSea;
            SyncSections(view);

            var t = plan.Totals;
            string money = UiText.S2("ss_ui_footer_money", "Spent {SPENT}  ·  earned {EARNED}",
                "SPENT", UiFormat.Money(t.Spent), "EARNED", UiFormat.Money(t.Earned));
            FooterMoneyText = money;
            FooterFoodText = t.FoodDaysAfter == null
                ? UiText.S2("ss_ui_footer_food_nodays", "Food {NOW} » {AFTER}",
                    "NOW", UiFormat.Money(t.FoodUnitsNow), "AFTER", UiFormat.Money(t.FoodUnitsAfter))
                : UiText.S3("ss_ui_footer_food", "Food {NOW} » {AFTER}  (~{DAYS} days)",
                    "NOW", UiFormat.Money(t.FoodUnitsNow), "AFTER", UiFormat.Money(t.FoodUnitsAfter), "DAYS", UiFormat.Days(t.FoodDaysAfter));
            FooterPartyText = UiText.S2("ss_ui_footer_party", "Party {AFTER}/{LIMIT}",
                "AFTER", UiFormat.Money(t.MembersAfter), "LIMIT", UiFormat.Money(t.PartySizeLimit));
            FooterPartyColor = t.OverPartyLimit ? UiColors.Warning : UiColors.Text;
            RefreshCarryLines(t);
            FooterHerdText = UiText.S2("ss_ui_footer_herd", "Horses {HORSES} / {ROOM} before the herd slows you",
                "HORSES", UiFormat.Money(t.Herd.Horses), "ROOM", UiFormat.Money(t.Herd.Room));
            FooterHerdColor = t.Herd.SlowsParty ? UiColors.Warning : UiColors.Text;

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

        /// <summary>The sections keep their VMs while the same sections show; a section that comes or goes rebuilds the list.</summary>
        private void SyncSections(SheetView view)
        {
            var list = Sections;
            bool same = list.Count == view.Sections.Count;
            for (int i = 0; same && i < list.Count; i++)
                same = list[i].Group == view.Sections[i].Group;
            if (same)
            {
                for (int i = 0; i < list.Count; i++)
                    list[i].Update(view.Sections[i]);
                return;
            }
            var fresh = new MBBindingList<SheetSectionVM>();
            foreach (var section in view.Sections)
                fresh.Add(new SheetSectionVM(this, section));
            Sections = fresh;
        }

        private void RefreshCarryLines(PlanTotals t)
        {
            var c = t.Carry;
            if (!c.Known)
            {
                FooterLandText = UiText.S1("ss_ui_footer_carry_change_only", "weight {KG} kg", "KG", UiFormat.SignedWeight(t.WeightChange));
                FooterLandOverText = "";
                HasFooterLandOver = false;
                HasSeaLine = false;
                return;
            }
            FooterLandText = CarryLine(c.WeightNow, t.WeightChange, c.WeightAfter, c.CapacityLandNow, c.CapacityLandAfter);
            FooterLandOverText = c.OverLand > 0 ? Over(c.OverLand) : "";
            HasFooterLandOver = c.OverLand > 0;
            HasSeaLine = c.ShowSea;
            if (!c.ShowSea)
                return;
            FooterSeaText = CarryLine(c.WeightAtSeaNow, c.WeightAtSeaAfter - c.WeightAtSeaNow, c.WeightAtSeaAfter,
                c.CapacitySeaNow, c.CapacitySeaAfter);
            FooterSeaOverText = c.OverSea > 0 ? Over(c.OverSea) : "";
            HasFooterSeaOver = c.OverSea > 0;
        }

        private static string CarryLine(double now, double change, double after, double capacityNow, double capacityAfter)
        {
            string weight = UiFormat.SignedWeight(change) == "0"
                ? UiText.S1("ss_ui_footer_carry_same", "weight {NOW} kg", "NOW", UiFormat.Kg(now))
                : UiText.S3("ss_ui_footer_carry", "weight {NOW} {CHANGE} » {AFTER} kg",
                    "NOW", UiFormat.Kg(now), "CHANGE", UiFormat.SignedWeight(change), "AFTER", UiFormat.Kg(after));
            string capacity = UiFormat.Kg(capacityNow) == UiFormat.Kg(capacityAfter)
                ? UiText.S1("ss_ui_footer_cap", "capacity {NOW}", "NOW", UiFormat.Kg(capacityAfter))
                : UiText.S2("ss_ui_footer_cap_change", "capacity {NOW} » {AFTER}",
                    "NOW", UiFormat.Kg(capacityNow), "AFTER", UiFormat.Kg(capacityAfter));
            return weight + "  " + UiFormat.Dot + "  " + capacity;
        }

        private static string Over(double kg) => UiText.S1("ss_ui_footer_over", "{KG} over", "KG", UiFormat.KgOver(kg));

        internal void SetStatus(string text)
        {
            StatusText = text ?? "";
            HasStatus = !string.IsNullOrEmpty(StatusText);
        }

        // ── clicks (called by the lines) ─────────────────────────────────────────────────────────────────

        /// <summary>[+] / [−] on a line: a row edit (click 1, shift 5, ctrl all — the editor stops at zero, so a troop row under
        /// its line never crosses to the other side), or a troop line's bulk step (step 20's TroopBulk).</summary>
        internal void Edit(SheetItem item, int direction)
        {
            var plan = _plan;
            if (plan == null)
                return;
            if ((direction > 0 ? item.IncreaseBlock : item.DecreaseBlock) != EditBlock.None)
                return; // greyed: the button should not have fired
            var size = StewardWindow.CurrentEditSize;
            var facts = plan.Facts;
            string sign = direction > 0 ? "[+] " : "[-] ";
            switch (item.Kind)
            {
                case SheetItemKind.Recruits:
                case SheetItemKind.YourTroops:
                    bool recruits = item.Kind == SheetItemKind.Recruits;
                    var results = recruits
                        ? direction > 0 ? plan.RecruitBest(size) : plan.TakeBackRecruits(size)
                        : direction > 0 ? plan.ReAddDropped(size) : plan.DismissLowest(size);
                    ModLog.Info("window", (recruits ? "recruits line " : "your troops line ") + sign + size + ": "
                                          + (results.Count == 0 ? "nothing to move"
                                              : string.Join(", ", results.Select(r => r.RowId + " " + r.Before + " -> " + r.After
                                                                                      + (r.Block == EditBlock.None ? "" : " (" + r.Block + ")")))));
                    break;
                case SheetItemKind.Row:
                    var row = item.Row;
                    if (row == null)
                        return;
                    var result = direction > 0 ? plan.Increase(row.Id, size) : plan.Decrease(row.Id, size);
                    ModLog.Info("window", sign + size + " " + row.Id + (item.Side == TroopSide.None ? "" : " (" + item.Side + " side)")
                                          + ": " + result.Before + " -> " + result.After
                                          + (result.Block == EditBlock.None ? "" : " (" + result.Block + ")"));
                    break;
                default:
                    return;
            }
            AfterEdit(facts);
        }

        /// <summary>⟲ on a row hands it back to the steward; on a troop line, every row the player moved on that line's side.</summary>
        internal void Reset(SheetItem item)
        {
            var plan = _plan;
            if (plan == null)
                return;
            var facts = plan.Facts;
            if (item.Kind == SheetItemKind.Row && item.Row != null)
            {
                var result = plan.Reset(item.Row.Id);
                ModLog.Info("window", "reset " + item.Row.Id + ": " + result.Before + " -> " + result.After + " (the steward's again)");
            }
            else if (item.Line != null && (item.Kind == SheetItemKind.Recruits || item.Kind == SheetItemKind.YourTroops))
            {
                bool recruits = item.Kind == SheetItemKind.Recruits;
                var touched = item.Line.Details
                    .Where(r => r.IsTouched && (recruits ? r.Change >= 0 : r.Change <= 0)).Select(r => r.Id).ToList();
                foreach (var id in touched)
                    plan.Reset(id);
                ModLog.Info("window", "reset the " + (recruits ? "recruits" : "your troops") + " line: " + string.Join(", ", touched));
            }
            else
                return;
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

        /// <summary>A ▸ or a section title: fold or open it, remembered across windows, towns and restarts (window_state.json,
        /// never the save — step 18, every fold since step 21).</summary>
        internal void Fold(IReadOnlyList<string> keys, bool fold)
        {
            foreach (var key in keys)
                WindowStateHost.SetFolded(key, fold);
            ModLog.Info("window", (fold ? "folded " : "opened ") + string.Join(", ", keys));
            Refresh();
        }

        /// <summary>After any edit: a live re-plan (step 15) is logged with the party it planned for, then everything reads the
        /// plan again (lines that came or went are inserted or removed by the sync).</summary>
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
            SetStatus("");
            Refresh();
            _onPlanChanged();
        }

        /// <summary>A wanderer's, the mercenary troop's, a troop's or a prisoner's Encyclopedia page (DESIGN §2.5, §2.7, §2.8).</summary>
        internal void OpenLink(PlanRow row)
        {
            string? link = null;
            if (row.Tavern?.Kind == TavernRowKind.Wanderer && row.HeroId != null)
            {
                Adapter.TavernKnowledge.Learn(_settlement, row.HeroId); // a known hero's page, never "???" (round 3)
                link = _settlement?.HeroesWithoutParty.FirstOrDefault(h => h != null && h.StringId == row.HeroId)?.EncyclopediaLink;
            }
            else if (row.TroopId != null)
                link = MBObjectManager.Instance?.GetObject<CharacterObject>(row.TroopId)?.EncyclopediaLink; // a lord's → his hero page
            ModLog.Info("window", "encyclopedia for " + row.Id + ": " + (link ?? "no link"));
            StewardWindow.OpenEncyclopedia(link);
        }

        // ── bound properties ─────────────────────────────────────────────────────────────────────────────

        [DataSourceProperty] public string ColMarket { get; }
        [DataSourceProperty] public string ColItem { get; }
        [DataSourceProperty] public string ColMine { get; }
        [DataSourceProperty] public string ColChange { get; }
        [DataSourceProperty] public string ColResult { get; }
        [DataSourceProperty] public string ColDenari { get; }
        [DataSourceProperty] public string ColParty { get; }
        [DataSourceProperty] public string ColPrisoners { get; }
        [DataSourceProperty] public string ColLand { get; }
        [DataSourceProperty] public string ColSea { get; }
        [DataSourceProperty] public string HeaderLabel { get; }
        [DataSourceProperty] public string ShortcutText { get; }
        [DataSourceProperty] public string ResetAllText { get; }
        [DataSourceProperty] public string WarningColor => UiColors.Warning;
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string HeadingColor => UiColors.Heading;
        [DataSourceProperty] public string InfluenceColor => UiColors.Buy;
        [DataSourceProperty] public string StatusColor => UiColors.Heading;

        [DataSourceProperty]
        public MBBindingList<SheetSectionVM> Sections
        {
            get => _sections;
            set { if (value != _sections) { _sections = value; OnPropertyChangedWithValue(value, nameof(Sections)); } }
        }

        /// <summary>The header after "Denari": <c>69,358 » 89,189</c>.</summary>
        [DataSourceProperty]
        public string HeaderFlowText
        {
            get => _headerFlowText;
            set { if (value != _headerFlowText) { _headerFlowText = value; OnPropertyChangedWithValue(value, nameof(HeaderFlowText)); } }
        }

        /// <summary><c>(+19,831)</c> — green in, red out.</summary>
        [DataSourceProperty]
        public string HeaderChangeText
        {
            get => _headerChangeText;
            set { if (value != _headerChangeText) { _headerChangeText = value; OnPropertyChangedWithValue(value, nameof(HeaderChangeText)); } }
        }

        [DataSourceProperty]
        public string HeaderChangeColor
        {
            get => _headerChangeColor;
            set { if (value != _headerChangeColor) { _headerChangeColor = value; OnPropertyChangedWithValue(value, nameof(HeaderChangeColor)); } }
        }

        /// <summary>The small green <c>+11.2 influence</c> (empty when none).</summary>
        [DataSourceProperty]
        public string HeaderInfluenceText
        {
            get => _headerInfluenceText;
            set { if (value != _headerInfluenceText) { _headerInfluenceText = value; OnPropertyChangedWithValue(value, nameof(HeaderInfluenceText)); } }
        }

        /// <summary>The party has ships (War Sails): the Sea kg column shows.</summary>
        [DataSourceProperty]
        public bool ShowSea
        {
            get => _showSea;
            set { if (value != _showSea) { _showSea = value; OnPropertyChangedWithValue(value, nameof(ShowSea)); } }
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

        [DataSourceProperty] public string LandLabel { get; }
        [DataSourceProperty] public string SeaLabel { get; }

        [DataSourceProperty]
        public string FooterLandText
        {
            get => _footerLandText;
            set { if (value != _footerLandText) { _footerLandText = value; OnPropertyChangedWithValue(value, nameof(FooterLandText)); } }
        }

        [DataSourceProperty]
        public string FooterLandOverText
        {
            get => _footerLandOverText;
            set { if (value != _footerLandOverText) { _footerLandOverText = value; OnPropertyChangedWithValue(value, nameof(FooterLandOverText)); } }
        }

        [DataSourceProperty]
        public bool HasFooterLandOver
        {
            get => _hasFooterLandOver;
            set { if (value != _hasFooterLandOver) { _hasFooterLandOver = value; OnPropertyChangedWithValue(value, nameof(HasFooterLandOver)); } }
        }

        [DataSourceProperty]
        public bool HasSeaLine
        {
            get => _hasSeaLine;
            set { if (value != _hasSeaLine) { _hasSeaLine = value; OnPropertyChangedWithValue(value, nameof(HasSeaLine)); } }
        }

        [DataSourceProperty]
        public string FooterSeaText
        {
            get => _footerSeaText;
            set { if (value != _footerSeaText) { _footerSeaText = value; OnPropertyChangedWithValue(value, nameof(FooterSeaText)); } }
        }

        [DataSourceProperty]
        public string FooterSeaOverText
        {
            get => _footerSeaOverText;
            set { if (value != _footerSeaOverText) { _footerSeaOverText = value; OnPropertyChangedWithValue(value, nameof(FooterSeaOverText)); } }
        }

        [DataSourceProperty]
        public bool HasFooterSeaOver
        {
            get => _hasFooterSeaOver;
            set { if (value != _hasFooterSeaOver) { _hasFooterSeaOver = value; OnPropertyChangedWithValue(value, nameof(HasFooterSeaOver)); } }
        }

        [DataSourceProperty]
        public string FooterHerdText
        {
            get => _footerHerdText;
            set { if (value != _footerHerdText) { _footerHerdText = value; OnPropertyChangedWithValue(value, nameof(FooterHerdText)); } }
        }

        [DataSourceProperty]
        public string FooterHerdColor
        {
            get => _footerHerdColor;
            set { if (value != _footerHerdColor) { _footerHerdColor = value; OnPropertyChangedWithValue(value, nameof(FooterHerdColor)); } }
        }

        [DataSourceProperty]
        public string FooterPartyText
        {
            get => _footerPartyText;
            set { if (value != _footerPartyText) { _footerPartyText = value; OnPropertyChangedWithValue(value, nameof(FooterPartyText)); } }
        }

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
    /// One section of the spreadsheet: its title line — ▸/▾, the name, the overview and the subtotal in every number column
    /// (mockup choice 3) — and the lines under it. A click on the title folds or opens the section (Troops: both troop lines,
    /// mockup choice 8), remembered in window_state.json.
    /// </summary>
    public sealed class SheetSectionVM : ViewModel
    {
        private readonly SuggestionTabVM _tab;
        private SheetSectionView _view;
        private string _overviewText = "";
        private string _overviewColor = UiColors.Text;
        private string _overviewNote = "";
        private bool _hasFold;
        private bool _isOpen;
        private string _denariText = "";
        private string _denariColor = UiColors.Muted;
        private string _influenceText = "";
        private string _partyText = "";
        private string _prisonersText = "";
        private string _landText = "";
        private string _seaText = "";
        private bool _showSea;

        internal SheetSectionVM(SuggestionTabVM tab, SheetSectionView view)
        {
            _tab = tab;
            _view = view;
            Group = view.Group;
            TitleText = UiLabels.SheetSection(view.Group);
            ToggleHint = new HintVM();
            Items = new MBBindingList<SheetItemVM>();
            Update(view);
        }

        internal SheetGroup Group { get; }

        internal void Update(SheetSectionView view)
        {
            _view = view;
            OverviewText = view.Overview;
            OverviewColor = UiColors.ForLimit(view.OverviewWarning);
            OverviewNote = view.OverviewNote;
            HasFold = view.HasFold;
            IsOpen = view.IsOpen;
            ToggleHint.Text = !view.HasFold ? ""
                : view.IsOpen
                    ? UiText.S("ss_ui_fold_hint", "Fold this section to its line. The steward remembers it, even after a restart.")
                    : UiText.S("ss_ui_unfold_hint", "Show this section's rows. The steward remembers it, even after a restart.");
            var c = view.Cells;
            DenariText = c.Denari;
            DenariColor = c.DenariColor;
            InfluenceText = c.Influence;
            PartyText = c.Party;
            PrisonersText = c.Prisoners;
            LandText = c.Land;
            SeaText = c.Sea;
            ShowSea = _tab.ShowSea;
            SyncItems(view.Items);
        }

        /// <summary>Lines whose key stays are updated in place; a fold removes the lines it closes and inserts the ones it opens
        /// (Gauntlet builds widgets only for those); an order that changed rebuilds the list.</summary>
        private void SyncItems(IReadOnlyList<SheetItem> items)
        {
            var list = Items;
            bool same = list.Count == items.Count;
            for (int i = 0; same && i < list.Count; i++)
                same = list[i].Key == items[i].Key;
            if (!same)
            {
                var wanted = new HashSet<string>(items.Select(i => i.Key), StringComparer.Ordinal);
                for (int i = list.Count - 1; i >= 0; i--)
                    if (!wanted.Contains(list[i].Key))
                        list.RemoveAt(i);
                for (int i = 0; i < items.Count; i++)
                {
                    if (i < list.Count && list[i].Key == items[i].Key)
                        continue;
                    bool later = false;
                    for (int j = i + 1; j < list.Count && !later; j++)
                        later = list[j].Key == items[i].Key;
                    if (later)
                    {
                        // The order changed (a re-plan moved rows): build the list again.
                        list.Clear();
                        foreach (var item in items)
                            list.Add(new SheetItemVM(_tab, item));
                        return;
                    }
                    list.Insert(i, new SheetItemVM(_tab, items[i]));
                }
            }
            for (int i = 0; i < list.Count; i++)
                list[i].Update(items[i]);
        }

        public void ExecuteToggle() => StewardWindowVM.Guard("fold " + Group, () =>
        {
            if (_view.HasFold)
                _tab.Fold(_view.FoldKeys, _view.IsOpen);
        });

        // ── bound properties ─────────────────────────────────────────────────────────────────────────────

        [DataSourceProperty] public string TitleText { get; }
        [DataSourceProperty] public string HeadingColor => UiColors.Heading;
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string InfluenceColor => UiColors.Buy;
        [DataSourceProperty] public string TextColor => UiColors.Text;
        [DataSourceProperty] public HintVM ToggleHint { get; }
        [DataSourceProperty] public MBBindingList<SheetItemVM> Items { get; }

        [DataSourceProperty]
        public string OverviewText
        {
            get => _overviewText;
            set { if (value != _overviewText) { _overviewText = value; OnPropertyChangedWithValue(value, nameof(OverviewText)); } }
        }

        /// <summary>Red when the overview's number is past a limit (the party over its limit, the herd slowing the party).</summary>
        [DataSourceProperty]
        public string OverviewColor
        {
            get => _overviewColor;
            set { if (value != _overviewColor) { _overviewColor = value; OnPropertyChangedWithValue(value, nameof(OverviewColor)); } }
        }

        [DataSourceProperty]
        public string OverviewNote
        {
            get => _overviewNote;
            set { if (value != _overviewNote) { _overviewNote = value; OnPropertyChangedWithValue(value, nameof(OverviewNote)); } }
        }

        [DataSourceProperty]
        public bool HasFold
        {
            get => _hasFold;
            set { if (value != _hasFold) { _hasFold = value; OnPropertyChangedWithValue(value, nameof(HasFold)); OnPropertyChanged(nameof(IsOpenIndicator)); OnPropertyChanged(nameof(IsClosedIndicator)); } }
        }

        [DataSourceProperty]
        public bool IsOpen
        {
            get => _isOpen;
            set { if (value != _isOpen) { _isOpen = value; OnPropertyChangedWithValue(value, nameof(IsOpen)); OnPropertyChanged(nameof(IsOpenIndicator)); OnPropertyChanged(nameof(IsClosedIndicator)); } }
        }

        /// <summary>▾ — the section is (partly) open.</summary>
        [DataSourceProperty] public bool IsOpenIndicator => _hasFold && _isOpen;

        /// <summary>▸ — the section is folded.</summary>
        [DataSourceProperty] public bool IsClosedIndicator => _hasFold && !_isOpen;

        [DataSourceProperty]
        public string DenariText
        {
            get => _denariText;
            set { if (value != _denariText) { _denariText = value; OnPropertyChangedWithValue(value, nameof(DenariText)); } }
        }

        [DataSourceProperty]
        public string DenariColor
        {
            get => _denariColor;
            set { if (value != _denariColor) { _denariColor = value; OnPropertyChangedWithValue(value, nameof(DenariColor)); } }
        }

        [DataSourceProperty]
        public string InfluenceText
        {
            get => _influenceText;
            set { if (value != _influenceText) { _influenceText = value; OnPropertyChangedWithValue(value, nameof(InfluenceText)); } }
        }

        [DataSourceProperty]
        public string PartyText
        {
            get => _partyText;
            set { if (value != _partyText) { _partyText = value; OnPropertyChangedWithValue(value, nameof(PartyText)); } }
        }

        [DataSourceProperty]
        public string PrisonersText
        {
            get => _prisonersText;
            set { if (value != _prisonersText) { _prisonersText = value; OnPropertyChangedWithValue(value, nameof(PrisonersText)); } }
        }

        [DataSourceProperty]
        public string LandText
        {
            get => _landText;
            set { if (value != _landText) { _landText = value; OnPropertyChangedWithValue(value, nameof(LandText)); } }
        }

        [DataSourceProperty]
        public string SeaText
        {
            get => _seaText;
            set { if (value != _seaText) { _seaText = value; OnPropertyChangedWithValue(value, nameof(SeaText)); } }
        }

        [DataSourceProperty]
        public bool ShowSea
        {
            get => _showSea;
            set { if (value != _showSea) { _showSea = value; OnPropertyChangedWithValue(value, nameof(ShowSea)); } }
        }
    }

    /// <summary>
    /// One line of the spreadsheet — a row, a troop line, a prisoner line or a breakdown line (Core <see cref="SheetItem"/>).
    /// Its cells and button states are Core's; its commands edit the plan through the tab.
    /// </summary>
    public sealed class SheetItemVM : ViewModel
    {
        private readonly SuggestionTabVM _tab;
        private SheetItem _item;

        private string _nameText = "";
        private bool _isLink;
        private string _noteText = "";
        private bool _isIndented;
        private bool _hasExpander;
        private bool _isExpanderOpen;
        private string _marketText = "";
        private string _mineText = "";
        private string _changeText = "";
        private string _changeColor = UiColors.Muted;
        private string _resultText = "";
        private string _denariText = "";
        private string _denariColor = UiColors.Muted;
        private string _influenceText = "";
        private string _partyText = "";
        private string _prisonersText = "";
        private string _landText = "";
        private string _seaText = "";
        private bool _showSea;
        private bool _hasSpinner;
        private bool _canIncrease;
        private bool _canDecrease;
        private bool _canReset;

        internal SheetItemVM(SuggestionTabVM tab, SheetItem item)
        {
            _tab = tab;
            _item = item;
            Key = item.Key;
            IsSubLine = item.Kind == SheetItemKind.SubLine;
            IsLine = item.Kind != SheetItemKind.Row && item.Kind != SheetItemKind.SubLine;
            IncreaseHint = new HintVM();
            DecreaseHint = new HintVM();
            DenariHint = new HintVM();
            FoldHint = new HintVM();
            ResetHint = new HintVM(IsLine
                ? UiText.S("ss_ui_reset_line_hint", "Hand the rows you moved on this line back to the steward.")
                : UiText.S("ss_ui_reset_hint", "Your number - the steward plans around it. Click to hand the row back to the steward."));
            LinkHint = new HintVM(UiText.S("ss_ui_link_hint", "Open in the Encyclopedia"));
            Update(item);
        }

        /// <summary>The line's identity (<see cref="SheetItem.Key"/>): kept while it stays on screen.</summary>
        internal string Key { get; }

        internal void Update(SheetItem item)
        {
            _item = item;
            NameText = item.Name;
            IsLink = item.IsLink;
            NoteText = item.Note;
            IsIndented = item.Indented;
            HasExpander = item.FoldKey != null;
            IsExpanderOpen = item.IsOpen;
            FoldHint.Text = item.FoldKey == null ? ""
                : item.IsOpen ? UiText.S("ss_ui_fold_line_hint", "Fold these rows. The steward remembers it.")
                : UiText.S("ss_ui_unfold_line_hint", "Show the rows behind this line. The steward remembers it.");
            MarketText = item.Market;
            MineText = item.Mine;
            ChangeText = item.Change;
            ChangeColor = item.ChangeColor;
            ResultText = item.Result;
            var c = item.Cells;
            DenariText = c.Denari;
            DenariColor = c.DenariColor;
            InfluenceText = c.Influence;
            PartyText = c.Party;
            PrisonersText = c.Prisoners;
            LandText = c.Land;
            SeaText = c.Sea;
            DenariHint.Text = item.DenariHint;
            ShowSea = _tab.ShowSea;
            HasSpinner = item.HasSpinner;
            CanIncrease = item.IncreaseBlock == EditBlock.None;
            CanDecrease = item.DecreaseBlock == EditBlock.None;
            IncreaseHint.Text = item.Kind == SheetItemKind.Recruits && CanIncrease
                ? UiText.S("ss_ui_recruits_plus_hint", "Recruit the best tier on offer first. Shift 5, Ctrl all.")
                : item.Kind == SheetItemKind.YourTroops && CanIncrease
                    ? UiText.S("ss_ui_troops_plus_hint", "Bring back the men you dropped, the last dropped first. Shift 5, Ctrl all.")
                    : UiLabels.Block(item.IncreaseBlock);
            DecreaseHint.Text = item.Kind == SheetItemKind.YourTroops && CanDecrease
                ? UiText.S("ss_ui_troops_minus_hint", "Dismiss from the lowest tier up, the wounded first. Shift 5, Ctrl all.")
                : item.Kind == SheetItemKind.Recruits && CanDecrease
                    ? UiText.S("ss_ui_recruits_minus_hint", "Give back the last recruits. Shift 5, Ctrl all.")
                    : UiLabels.Block(item.DecreaseBlock);
            CanReset = item.CanReset;
        }

        // ── commands ─────────────────────────────────────────────────────────────────────────────────────

        public void ExecuteIncrease() => StewardWindowVM.Guard("[+] " + Key, () => _tab.Edit(_item, +1));

        public void ExecuteDecrease() => StewardWindowVM.Guard("[-] " + Key, () => _tab.Edit(_item, -1));

        public void ExecuteReset() => StewardWindowVM.Guard("reset " + Key, () => _tab.Reset(_item));

        public void ExecuteToggleFold() => StewardWindowVM.Guard("fold " + Key, () =>
        {
            if (_item.FoldKey != null)
                _tab.Fold(new[] { _item.FoldKey }, _item.IsOpen);
        });

        public void ExecuteOpenLink() => StewardWindowVM.Guard("link " + Key, () =>
        {
            if (_item.IsLink && _item.Row != null)
                _tab.OpenLink(_item.Row);
        });

        // ── bound properties ─────────────────────────────────────────────────────────────────────────────

        /// <summary>A full line (32 px) — else a small breakdown line.</summary>
        [DataSourceProperty] public bool IsMainLine => !IsSubLine;
        [DataSourceProperty] public bool IsSubLine { get; }

        /// <summary>An aggregate or prisoner line (its name reads white, no link).</summary>
        [DataSourceProperty] public bool IsLine { get; }

        [DataSourceProperty] public string LinkColor => UiColors.Link;
        [DataSourceProperty] public string TextColor => UiColors.Text;
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string InfluenceColor => UiColors.Buy;
        [DataSourceProperty] public HintVM IncreaseHint { get; }
        [DataSourceProperty] public HintVM DecreaseHint { get; }
        [DataSourceProperty] public HintVM ResetHint { get; }
        [DataSourceProperty] public HintVM LinkHint { get; }
        [DataSourceProperty] public HintVM FoldHint { get; }
        [DataSourceProperty] public HintVM DenariHint { get; }

        [DataSourceProperty]
        public string NameText
        {
            get => _nameText;
            set { if (value != _nameText) { _nameText = value; OnPropertyChangedWithValue(value, nameof(NameText)); } }
        }

        [DataSourceProperty]
        public bool IsLink
        {
            get => _isLink;
            set { if (value != _isLink) { _isLink = value; OnPropertyChangedWithValue(value, nameof(IsLink)); OnPropertyChanged(nameof(IsPlainName)); } }
        }

        [DataSourceProperty] public bool IsPlainName => !_isLink;

        [DataSourceProperty]
        public string NoteText
        {
            get => _noteText;
            set { if (value != _noteText) { _noteText = value; OnPropertyChangedWithValue(value, nameof(NoteText)); } }
        }

        [DataSourceProperty]
        public bool IsIndented
        {
            get => _isIndented;
            set { if (value != _isIndented) { _isIndented = value; OnPropertyChangedWithValue(value, nameof(IsIndented)); } }
        }

        [DataSourceProperty]
        public bool HasExpander
        {
            get => _hasExpander;
            set { if (value != _hasExpander) { _hasExpander = value; OnPropertyChangedWithValue(value, nameof(HasExpander)); OnPropertyChanged(nameof(IsExpanderClosed)); OnPropertyChanged(nameof(IsExpanderOpenShown)); } }
        }

        [DataSourceProperty]
        public bool IsExpanderOpen
        {
            get => _isExpanderOpen;
            set { if (value != _isExpanderOpen) { _isExpanderOpen = value; OnPropertyChangedWithValue(value, nameof(IsExpanderOpen)); OnPropertyChanged(nameof(IsExpanderClosed)); OnPropertyChanged(nameof(IsExpanderOpenShown)); } }
        }

        /// <summary>▸ (closed).</summary>
        [DataSourceProperty] public bool IsExpanderClosed => _hasExpander && !_isExpanderOpen;

        /// <summary>▾ (open).</summary>
        [DataSourceProperty] public bool IsExpanderOpenShown => _hasExpander && _isExpanderOpen;

        [DataSourceProperty]
        public string MarketText
        {
            get => _marketText;
            set { if (value != _marketText) { _marketText = value; OnPropertyChangedWithValue(value, nameof(MarketText)); } }
        }

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
        public string DenariText
        {
            get => _denariText;
            set { if (value != _denariText) { _denariText = value; OnPropertyChangedWithValue(value, nameof(DenariText)); } }
        }

        [DataSourceProperty]
        public string DenariColor
        {
            get => _denariColor;
            set { if (value != _denariColor) { _denariColor = value; OnPropertyChangedWithValue(value, nameof(DenariColor)); } }
        }

        [DataSourceProperty]
        public string InfluenceText
        {
            get => _influenceText;
            set { if (value != _influenceText) { _influenceText = value; OnPropertyChangedWithValue(value, nameof(InfluenceText)); } }
        }

        [DataSourceProperty]
        public string PartyText
        {
            get => _partyText;
            set { if (value != _partyText) { _partyText = value; OnPropertyChangedWithValue(value, nameof(PartyText)); } }
        }

        [DataSourceProperty]
        public string PrisonersText
        {
            get => _prisonersText;
            set { if (value != _prisonersText) { _prisonersText = value; OnPropertyChangedWithValue(value, nameof(PrisonersText)); } }
        }

        [DataSourceProperty]
        public string LandText
        {
            get => _landText;
            set { if (value != _landText) { _landText = value; OnPropertyChangedWithValue(value, nameof(LandText)); } }
        }

        [DataSourceProperty]
        public string SeaText
        {
            get => _seaText;
            set { if (value != _seaText) { _seaText = value; OnPropertyChangedWithValue(value, nameof(SeaText)); } }
        }

        [DataSourceProperty]
        public bool ShowSea
        {
            get => _showSea;
            set { if (value != _showSea) { _showSea = value; OnPropertyChangedWithValue(value, nameof(ShowSea)); } }
        }

        /// <summary>[−] n [+] (rows and the troop lines).</summary>
        [DataSourceProperty]
        public bool HasSpinner
        {
            get => _hasSpinner;
            set { if (value != _hasSpinner) { _hasSpinner = value; OnPropertyChangedWithValue(value, nameof(HasSpinner)); OnPropertyChanged(nameof(HasChangeOnly)); } }
        }

        /// <summary>The Change column as text only (a main line without buttons).</summary>
        [DataSourceProperty] public bool HasChangeOnly => !_hasSpinner && !IsSubLine;

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
    }
}
