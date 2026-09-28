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
    /// The Suggestion tab as ONE spreadsheet (PLAN step 21 — the mockup Anton approved on 2026.09.28, docs/mockups/README.md,
    /// DESIGN §1.1): the denari header, the columns Item · Market · Goal · Mine · Change · Result · Denari · Party · Prisoners ·
    /// Land weight · Sea weight (round 5, step 23 — the Goal a typed box on the food and horse role rows), the sections Troops ·
    /// Food · Horses · Prisoners · Other whose title line is their subtotal, the lines under them as the folds leave them. Core's <see cref="SheetView"/> decides every line, cell, colour and button state; this VM
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
        private bool _hasSeaRow;
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

        /// <summary>A Goal box the player left (Enter or focus lost) — committed on the next tick or before the next command,
        /// never inside the widget's own event (a re-plan may rebuild the very list the box sits in).</summary>
        private readonly List<(SheetItemVM Item, bool Entered)> _pendingGoals = new List<(SheetItemVM, bool)>();

        /// <summary>The Goal box being typed in (between its FocusGained and FocusLost).</summary>
        private SheetItemVM? _typingGoal;

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
            // "Weight, not kg" (Anton 2026.09.28, round 5): the game writes weight with no unit.
            ColLand = UiText.S("ss_ui_col_land_weight", "Land weight");
            ColSea = UiText.S("ss_ui_col_sea_weight", "Sea weight");
            ColGoal = UiText.S("ss_ui_col_goal", "Goal"); // round 5: where the line should end (step 23 lays it out)
            HeaderLabel = UiText.S("ss_ui_header_denari", "Denari");
            _nothingToDoText = UiText.S("ss_ui_empty", "Nothing for the steward to do here.");
            _emptyText = _nothingToDoText;
            ResetAllText = UiText.S("ss_ui_reset_all", "Reset all");
            Total = new SheetTotalVM(UiText.S("ss_ui_total", "Total"));
            LandRow = new WeightRowVM(UiText.S("ss_ui_weight_land", "Land"));
            SeaRow = new WeightRowVM(UiText.S("ss_ui_weight_sea", "Sea"));
            WeightBefore = UiText.S("ss_ui_weight_before", "before");
            WeightChange = UiText.S("ss_ui_weight_change", "change");
            WeightAfter = UiText.S("ss_ui_weight_after", "after");
            WeightCapacity = UiText.S("ss_ui_weight_capacity", "capacity");
            WeightLeft = UiText.S("ss_ui_weight_left", "left");
            WeightSlowdown = UiText.S("ss_ui_weight_slowdown", "slowdown");
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

            // The pinned Total line and the footer's weight table (round 4: the spent / earned / influence / food / party / herd
            // lines are gone from the footer - they live in the header and the sections now).
            Total.Update(view, sheet.ShowSea);
            var weights = sheet.Weights;
            if (weights.Count > 0)
                LandRow.Update(weights[0]);
            HasSeaRow = weights.Count > 1;
            if (weights.Count > 1)
                SeaRow.Update(weights[1]);

            var t = plan.Totals;
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

        internal void SetStatus(string text)
        {
            StatusText = text ?? "";
            HasStatus = !string.IsNullOrEmpty(StatusText);
        }

        // ── the typed Goal boxes (round 5, PLAN step 23) ──────────────────────────────────────────────────

        internal void GoalFocusGained(SheetItemVM item) => _typingGoal = item;

        /// <summary>The player left a Goal box: Enter (<paramref name="entered"/>) or a click elsewhere. Queued — see
        /// <see cref="FlushGoal"/>.</summary>
        internal void QueueGoal(SheetItemVM item, bool entered)
        {
            if (_typingGoal == item && !entered)
                _typingGoal = null;
            int at = _pendingGoals.FindIndex(p => p.Item == item);
            if (at >= 0)
                _pendingGoals[at] = (item, _pendingGoals[at].Entered || entered);
            else
                _pendingGoals.Add((item, entered));
        }

        /// <summary>A Goal box is being typed in.</summary>
        internal bool IsTypingGoal => _typingGoal != null;

        /// <summary>Escape inside a Goal box: the box shows its goal again and nothing is committed (the window stays open —
        /// a second Escape closes it).</summary>
        internal void CancelGoalTyping()
        {
            var item = _typingGoal;
            _typingGoal = null;
            _pendingGoals.RemoveAll(p => p.Item == item);
            if (item != null)
            {
                item.EndTyping();
                item.RevertGoal();
                ModLog.Info("window", "goal typing cancelled on " + item.Key);
            }
        }

        /// <summary>
        /// Commits the Goal box the player left: a changed whole number becomes the row's standing goal
        /// (<see cref="StewardPlan.SetGoal"/>, the plan re-plans around it) and is saved quietly; anything else — the same text,
        /// letters, a minus, an empty box — puts the box back as it was (Core <see cref="GoalInput"/>). Runs every tick and
        /// before every other command, so a click elsewhere lands after the goal it took the focus from.
        /// </summary>
        internal void FlushGoal()
        {
            if (_pendingGoals.Count == 0)
                return;
            var pending = _pendingGoals.ToList();
            _pendingGoals.Clear();
            bool entered = false;
            foreach (var (item, enter) in pending)
            {
                if (enter && _typingGoal == item)
                    _typingGoal = null;
                entered |= enter;
                item.EndTyping();
                CommitGoal(item);
            }
            if (entered)
                StewardWindow.ClearTextFocus(); // Enter leaves the box, as in a spreadsheet
        }

        private void CommitGoal(SheetItemVM vm)
        {
            var plan = _plan;
            var item = vm.Item;
            var row = item.Row;
            if (plan == null || row == null || !item.Goal.Editable || item.Kind != SheetItemKind.Row)
            {
                vm.RevertGoal();
                return;
            }
            switch (GoalInput.Read(vm.GoalText, vm.ShownGoalText, out int goal))
            {
                case GoalTyped.Unchanged:
                    vm.RevertGoal(); // the hands-off mark comes back after an empty visit
                    return;
                case GoalTyped.Invalid:
                    ModLog.Info("window", "goal typed on " + row.Id + " is not a number of 0 or more: '" + vm.GoalText + "' - kept "
                                          + vm.ShownGoalText);
                    vm.RevertGoal();
                    return;
            }
            var facts = plan.Facts;
            var result = plan.SetGoal(row.Id, goal);
            ModLog.Info("window", "goal typed " + row.Id + " = " + goal + ": change " + result.Before + " -> " + result.After
                                  + (result.GoalShort == GoalShort.None ? "" : " (short: " + result.GoalShort + ")"));
            AfterEdit(facts);
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
            FlushGoal();
            if (_plan == null)
                return;
            var facts = _plan.Facts;
            _plan.ResetAll();
            ModLog.Info("window", "reset all");
            AfterEdit(facts);
        });

        /// <summary>
        /// The Lords / Others toggle (mockup choice 7): Keep | Ransom | Donate IS the setting (<c>LordPrisonerAction</c> /
        /// <c>PrisonerAction</c>) — saved to settings.json like the Instructions tab, MCM shows it too. The settings service
        /// announces the change and the window re-plans at once, the player's touched rows carried over (step 7's rule).
        /// </summary>
        internal void SetPrisonerAction(SheetItem item, PrisonerChoice choice)
        {
            if (item.Choice == null || item.Choice == choice)
                return;
            if (choice == PrisonerChoice.Donate && !item.DonateAllowed)
                return; // greyed: the game does not allow donating here
            string key = item.Kind == SheetItemKind.Lords ? nameof(StewardSettings.LordPrisonerAction) : nameof(StewardSettings.PrisonerAction);
            if (!(SettingsRegistry.Find(key) is EnumSetting def))
                return;
            ModLog.Info("window", "prisoner toggle: " + key + " " + item.Choice + " -> " + choice);
            SettingsHost.Service.Set(def, choice); // Changed → StewardWindowVM re-plans the tab on show
        }

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
            SaveGoals(plan);
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

        /// <summary>
        /// Round 5 (DESIGN §1.1 "THE GOAL"): a click (or ⟲) on a food or pack / riding / war row edited its standing goal — the plan
        /// re-planned itself already, so the goals are saved to settings.json QUIETLY (no Changed: announcing would re-plan the
        /// window a second time). They hold in every town until their ⟲.
        /// </summary>
        private static void SaveGoals(StewardPlan? plan)
        {
            if (plan == null)
                return;
            var edits = plan.TakeGoalEdits();
            if (edits.Count == 0)
                return;
            SettingsHost.Service.SaveQuietly(s =>
            {
                foreach (var edit in edits)
                    edit.ApplyTo(s);
            });
            ModLog.Info("window", "goals saved: " + string.Join(", ", edits));
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

        [DataSourceProperty] public string ColGoal { get; }
        [DataSourceProperty] public string HeaderLabel { get; }
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

        /// <summary>The pinned Total line (mockup choice 3): every row once, in every column.</summary>
        [DataSourceProperty] public SheetTotalVM Total { get; }

        /// <summary>The footer's weight table (round 4): Land, and Sea with ships.</summary>
        [DataSourceProperty] public WeightRowVM LandRow { get; }
        [DataSourceProperty] public WeightRowVM SeaRow { get; }
        [DataSourceProperty] public string WeightBefore { get; }
        [DataSourceProperty] public string WeightChange { get; }
        [DataSourceProperty] public string WeightAfter { get; }
        [DataSourceProperty] public string WeightCapacity { get; }
        [DataSourceProperty] public string WeightLeft { get; }
        [DataSourceProperty] public string WeightSlowdown { get; }
        [DataSourceProperty] public string TextColor => UiColors.Text;

        [DataSourceProperty]
        public bool HasSeaRow
        {
            get => _hasSeaRow;
            set { if (value != _hasSeaRow) { _hasSeaRow = value; OnPropertyChangedWithValue(value, nameof(HasSeaRow)); } }
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
        private string _goalText = "";
        private string _goalColor = UiColors.Text;
        private string _mineText = "";
        private string _mineColor = UiColors.Text;
        private string _resultText = "";
        private string _resultColor = UiColors.Text;

        internal SheetSectionVM(SuggestionTabVM tab, SheetSectionView view)
        {
            _tab = tab;
            _view = view;
            Group = view.Group;
            TitleText = UiLabels.SheetSection(view.Group);
            ToggleHint = new HintVM();
            GoalHint = new HintVM();
            OverviewHint = new HintVM();
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
            // Round 5: the title line's Goal (Troops = the party size limit, the others the sum of their lines), Mine (red when
            // the party is over its limit now) and Result (red past a limit, like the overview: the party, the herd).
            GoalText = view.Goal.Text;
            GoalColor = view.Goal.HandsOff ? UiColors.Muted : UiColors.Text;
            GoalHint.Text = view.Goal.Hint;
            MineText = view.Mine;
            MineColor = UiColors.ForLimit(view.MineWarning);
            ResultText = view.Result;
            ResultColor = UiColors.ForLimit(view.OverviewWarning);
            OverviewHint.Text = view.Overview.Length == 0 ? ""
                : view.OverviewNote.Length == 0 ? view.Overview : view.Overview + " " + UiFormat.Dot + " " + view.OverviewNote;
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
            _tab.FlushGoal();
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

        [DataSourceProperty] public HintVM GoalHint { get; }

        /// <summary>The whole overview and its note (the overview line is cut at the table's width).</summary>
        [DataSourceProperty] public HintVM OverviewHint { get; }

        [DataSourceProperty]
        public string GoalText
        {
            get => _goalText;
            set { if (value != _goalText) { _goalText = value; OnPropertyChangedWithValue(value, nameof(GoalText)); } }
        }

        [DataSourceProperty]
        public string GoalColor
        {
            get => _goalColor;
            set { if (value != _goalColor) { _goalColor = value; OnPropertyChangedWithValue(value, nameof(GoalColor)); } }
        }

        [DataSourceProperty]
        public string MineText
        {
            get => _mineText;
            set { if (value != _mineText) { _mineText = value; OnPropertyChangedWithValue(value, nameof(MineText)); } }
        }

        /// <summary>Red when the party is over its size limit now (Anton, round 5).</summary>
        [DataSourceProperty]
        public string MineColor
        {
            get => _mineColor;
            set { if (value != _mineColor) { _mineColor = value; OnPropertyChangedWithValue(value, nameof(MineColor)); } }
        }

        [DataSourceProperty]
        public string ResultText
        {
            get => _resultText;
            set { if (value != _resultText) { _resultText = value; OnPropertyChangedWithValue(value, nameof(ResultText)); } }
        }

        [DataSourceProperty]
        public string ResultColor
        {
            get => _resultColor;
            set { if (value != _resultColor) { _resultColor = value; OnPropertyChangedWithValue(value, nameof(ResultColor)); } }
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
        private bool _hasToggle;
        private PrisonerChoice _choice;
        private bool _canDonate;
        private string _goalText = "";
        private string _goalColor = UiColors.Text;
        private bool _isGoalBox;
        private bool _isGoalPlain;
        private bool _canResetGoal;
        private bool _canResetInChange;

        /// <summary>The Goal box has the focus: an update must not write over what the player is typing.</summary>
        private bool _editing;

        private readonly string _goalYoursHint;
        private readonly string _goalStewardHint;

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
            KeepText = UiText.S("ss_ui_toggle_keep", "Keep");
            RansomText = UiText.S("ss_ui_toggle_ransom", "Ransom");
            DonateText = UiText.S("ss_ui_toggle_donate", "Donate");
            KeepHint = new HintVM(UiText.S("ss_ui_toggle_keep_hint",
                "Keep them: the steward proposes nothing (you may still ransom by hand below). Your standing order - saved like the Instructions tab."));
            RansomHint = new HintVM(UiText.S("ss_ui_toggle_ransom_hint",
                "Ransom them for denari at the ransom broker. Your standing order - saved like the Instructions tab."));
            DonateHint = new HintVM();
            GoalHint = new HintVM();
            ResultHint = new HintVM();
            GoalResetHint = new HintVM(UiText.S("ss_ui_goal_reset_hint",
                "Your goal - it holds in every town. Click to give the row back to the rules in the Instructions tab."));
            _goalYoursHint = UiText.S("ss_ui_goal_yours_hint",
                "Your goal: the steward aims this row at it in every town, until you reset it. Type a new number and press Enter to change it.");
            _goalStewardHint = UiText.S("ss_ui_goal_steward_hint",
                "The steward's goal, from your Instructions. Type a number and press Enter to set your own - it then holds in every town.");
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

            // The Goal column (round 5): a typed box on the food and pack / riding / war rows, plain text elsewhere; the ⟲ of a goal
            // row sits beside its box (the Change column keeps the ⟲ of every other row); the Result says why it stops short.
            var goal = item.Goal;
            IsGoalBox = goal.Editable && item.Kind == SheetItemKind.Row;
            IsGoalPlain = !IsGoalBox && !IsSubLine;
            if (!_editing)
            {
                SetGoalText(goal.Text);
                GoalColor = GoalColorOf(goal);
            }
            GoalHint.Text = goal.HandsOff ? goal.Hint : !IsGoalBox ? "" : goal.IsYours ? _goalYoursHint : _goalStewardHint;
            CanResetGoal = IsGoalBox && item.CanReset;
            CanResetInChange = item.CanReset && !IsGoalBox;
            ResultHint.Text = goal.ShortText;

            HasToggle = item.Choice != null;
            if (item.Choice != null)
            {
                Choice = item.Choice.Value;
                CanDonate = item.DonateAllowed;
                DonateHint.Text = item.DonateAllowed
                    ? UiText.S("ss_ui_toggle_donate_hint",
                        "Donate them to this town's dungeon for influence, the most valuable first - what does not fit is ransomed (lords kept). Your standing order - saved like the Instructions tab.")
                    : UiText.S("ss_ui_toggle_donate_off", "The game does not let you donate prisoners here.");
            }
        }

        // ── commands ─────────────────────────────────────────────────────────────────────────────────────

        // Every command first commits a Goal box the click took the focus from (FlushGoal), then acts on the line as it stands.

        public void ExecuteIncrease() => StewardWindowVM.Guard("[+] " + Key, () => { _tab.FlushGoal(); _tab.Edit(_item, +1); });

        public void ExecuteDecrease() => StewardWindowVM.Guard("[-] " + Key, () => { _tab.FlushGoal(); _tab.Edit(_item, -1); });

        /// <summary>⟲ — in the Change column, or beside a goal of yours in the Goal column (the row back to the policy).</summary>
        public void ExecuteReset() => StewardWindowVM.Guard("reset " + Key, () => { _tab.FlushGoal(); _tab.Reset(_item); });

        public void ExecuteToggleFold() => StewardWindowVM.Guard("fold " + Key, () =>
        {
            _tab.FlushGoal();
            if (_item.FoldKey != null)
                _tab.Fold(new[] { _item.FoldKey }, _item.IsOpen);
        });

        public void ExecuteOpenLink() => StewardWindowVM.Guard("link " + Key, () =>
        {
            _tab.FlushGoal();
            if (_item.IsLink && _item.Row != null)
                _tab.OpenLink(_item.Row);
        });

        public void ExecuteKeep() => StewardWindowVM.Guard("keep " + Key, () => { _tab.FlushGoal(); _tab.SetPrisonerAction(_item, PrisonerChoice.Keep); });

        public void ExecuteRansom() => StewardWindowVM.Guard("ransom " + Key, () => { _tab.FlushGoal(); _tab.SetPrisonerAction(_item, PrisonerChoice.Ransom); });

        public void ExecuteDonate() => StewardWindowVM.Guard("donate " + Key, () => { _tab.FlushGoal(); _tab.SetPrisonerAction(_item, PrisonerChoice.Donate); });

        // ── the Goal box (round 5): the widget's own events only queue — the tab commits on its next tick ──

        /// <summary>The box got the focus: typing starts (the hands-off mark clears, so a number can be typed at once).</summary>
        public void ExecuteGoalFocusGained() => StewardWindowVM.Guard("goal focus " + Key, () =>
        {
            _editing = true;
            _tab.GoalFocusGained(this);
            if (_item.Goal.HandsOff)
                SetGoalText("");
            GoalColor = UiColors.Text;
        });

        /// <summary>A click elsewhere took the focus: the typed goal commits (or reverts).</summary>
        public void ExecuteGoalFocusLost() => StewardWindowVM.Guard("goal focus lost " + Key, () => _tab.QueueGoal(this, entered: false));

        /// <summary>Enter: the typed goal commits and the box lets go.</summary>
        public void ExecuteGoalEntered() => StewardWindowVM.Guard("goal entered " + Key, () => _tab.QueueGoal(this, entered: true));

        /// <summary>The line as Core last built it.</summary>
        internal SheetItem Item => _item;

        /// <summary>What the Goal box showed before the player typed (Core's text).</summary>
        internal string ShownGoalText => _item.Goal.Text;

        /// <summary>Typing is over (committed, reverted or cancelled): the next update may write the box again.</summary>
        internal void EndTyping() => _editing = false;

        /// <summary>The box shows Core's goal again, in its colour.</summary>
        internal void RevertGoal()
        {
            SetGoalText(_item.Goal.Text);
            GoalColor = GoalColorOf(_item.Goal);
        }

        private void SetGoalText(string text)
        {
            if (text == _goalText)
                return;
            _goalText = text;
            OnPropertyChangedWithValue(text, nameof(GoalText));
        }

        /// <summary>Gold for a goal of yours, grey for the hands-off mark, plain for the steward's.</summary>
        private static string GoalColorOf(SheetGoalCell goal) =>
            goal.IsYours ? UiColors.Yours : goal.HandsOff ? UiColors.Muted : UiColors.Text;

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

        /// <summary>The Change column as text only (a main line without buttons or toggle).</summary>
        [DataSourceProperty] public bool HasChangeOnly => !_hasSpinner && !_hasToggle && !IsSubLine;

        /// <summary>The Lords / Others line: Keep | Ransom | Donate in the Change column (mockup choice 7).</summary>
        [DataSourceProperty]
        public bool HasToggle
        {
            get => _hasToggle;
            set
            {
                if (value == _hasToggle) return;
                _hasToggle = value;
                OnPropertyChangedWithValue(value, nameof(HasToggle));
                OnPropertyChanged(nameof(HasChangeOnly));
                OnPropertyChanged(nameof(IsKeep));
                OnPropertyChanged(nameof(IsRansom));
                OnPropertyChanged(nameof(IsDonate));
            }
        }

        private PrisonerChoice Choice
        {
            get => _choice;
            set
            {
                if (value == _choice) return;
                _choice = value;
                OnPropertyChanged(nameof(IsKeep));
                OnPropertyChanged(nameof(IsRansom));
                OnPropertyChanged(nameof(IsDonate));
            }
        }

        /// <summary>The chosen button is lit gold.</summary>
        [DataSourceProperty] public bool IsKeep => _hasToggle && _choice == PrisonerChoice.Keep;
        [DataSourceProperty] public bool IsRansom => _hasToggle && _choice == PrisonerChoice.Ransom;
        [DataSourceProperty] public bool IsDonate => _hasToggle && _choice == PrisonerChoice.Donate;

        /// <summary>Keep and Ransom are always there to choose (the order stands for the towns ahead).</summary>
        [DataSourceProperty] public bool CanKeep => true;
        [DataSourceProperty] public bool CanRansom => true;

        /// <summary>Donate greys where the game forbids it here (mockup choice 7).</summary>
        [DataSourceProperty]
        public bool CanDonate
        {
            get => _canDonate;
            set { if (value != _canDonate) { _canDonate = value; OnPropertyChangedWithValue(value, nameof(CanDonate)); } }
        }

        [DataSourceProperty] public string KeepText { get; }
        [DataSourceProperty] public string RansomText { get; }
        [DataSourceProperty] public string DonateText { get; }
        [DataSourceProperty] public HintVM KeepHint { get; }
        [DataSourceProperty] public HintVM RansomHint { get; }
        [DataSourceProperty] public HintVM DonateHint { get; }

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

        // ── the Goal column (round 5) ─────────────────────────────────────────────────────────────────────

        /// <summary>The goal as the box / cell shows it; two-way on the box (the widget writes every key — nothing commits
        /// until Enter or a click elsewhere). While typing, a text that is no goal turns red.</summary>
        [DataSourceProperty]
        public string GoalText
        {
            get => _goalText;
            set
            {
                if (value == _goalText) return;
                _goalText = value ?? "";
                OnPropertyChangedWithValue(_goalText, nameof(GoalText));
                if (_editing)
                    GoalColor = _goalText.Trim().Length == 0 || GoalInput.Read(_goalText, "\u0001", out _) == GoalTyped.Goal
                        ? UiColors.Text
                        : UiColors.Warning;
            }
        }

        [DataSourceProperty]
        public string GoalColor
        {
            get => _goalColor;
            set { if (value != _goalColor) { _goalColor = value; OnPropertyChangedWithValue(value, nameof(GoalColor)); } }
        }

        /// <summary>A typed box: food and the pack / riding / war rows.</summary>
        [DataSourceProperty]
        public bool IsGoalBox
        {
            get => _isGoalBox;
            set { if (value != _isGoalBox) { _isGoalBox = value; OnPropertyChangedWithValue(value, nameof(IsGoalBox)); } }
        }

        /// <summary>The goal as plain text (every other main line; empty on the troop lines).</summary>
        [DataSourceProperty]
        public bool IsGoalPlain
        {
            get => _isGoalPlain;
            set { if (value != _isGoalPlain) { _isGoalPlain = value; OnPropertyChangedWithValue(value, nameof(IsGoalPlain)); } }
        }

        /// <summary>⟲ beside a goal of yours.</summary>
        [DataSourceProperty]
        public bool CanResetGoal
        {
            get => _canResetGoal;
            set { if (value != _canResetGoal) { _canResetGoal = value; OnPropertyChangedWithValue(value, nameof(CanResetGoal)); } }
        }

        /// <summary>⟲ in the Change column: the player's hand on a row that takes no goal (a troop, a prisoner, a loot group).</summary>
        [DataSourceProperty]
        public bool CanResetInChange
        {
            get => _canResetInChange;
            set { if (value != _canResetInChange) { _canResetInChange = value; OnPropertyChangedWithValue(value, nameof(CanResetInChange)); } }
        }

        /// <summary>The Goal cell's hover: the hands-off reason, or what typing a goal does.</summary>
        [DataSourceProperty] public HintVM GoalHint { get; }

        /// <summary>The Goal column's ⟲ hover.</summary>
        [DataSourceProperty] public HintVM GoalResetHint { get; }

        /// <summary>The Result's hover when it stops short of the goal: <c>Short of the goal: …</c>.</summary>
        [DataSourceProperty] public HintVM ResultHint { get; }
    }

    /// <summary>The pinned Total line (mockup choice 3): "Total", the party and prisoners before » after, and every row once in
    /// every number column — never scrolls.</summary>
    public sealed class SheetTotalVM : ViewModel
    {
        private string _totalText = "";
        private string _denariText = "";
        private string _denariColor = UiColors.Muted;
        private string _influenceText = "";
        private string _partyText = "";
        private string _prisonersText = "";
        private string _landText = "";
        private string _seaText = "";
        private bool _showSea;

        internal SheetTotalVM(string title)
        {
            TitleText = title;
        }

        internal void Update(SheetView view, bool showSea)
        {
            TotalText = view.Sheet.TotalText;
            var c = view.Total;
            DenariText = c.Denari;
            DenariColor = c.DenariColor;
            InfluenceText = c.Influence;
            PartyText = c.Party;
            PrisonersText = c.Prisoners;
            LandText = c.Land;
            SeaText = c.Sea;
            ShowSea = showSea;
        }

        [DataSourceProperty] public string TitleText { get; }
        [DataSourceProperty] public string HeadingColor => UiColors.Heading;
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string InfluenceColor => UiColors.Buy;

        /// <summary><c>party 103 » 104 · prisoners 52 » 0</c>.</summary>
        [DataSourceProperty]
        public string TotalText
        {
            get => _totalText;
            set { if (value != _totalText) { _totalText = value; OnPropertyChangedWithValue(value, nameof(TotalText)); } }
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
    }

    /// <summary>
    /// One row of the footer's weight table (round 4 — Anton 2026.09.28: "in cols again, before, change, after, capacity,
    /// capacity left, try adding a col slowdown … the vanilla icons that show the speed"): Core's <see cref="WeightTableRow"/>
    /// as text; "left" red when the load is over, the slowdown red when there is one (the game's Overburdened, RESEARCH §25).
    /// </summary>
    public sealed class WeightRowVM : ViewModel
    {
        private string _beforeText = "";
        private string _changeText = "";
        private string _afterText = "";
        private string _capacityText = "";
        private string _leftText = "";
        private string _leftColor = UiColors.Text;
        private string _slowdownText = "";
        private string _slowdownColor = UiColors.Text;

        internal WeightRowVM(string label)
        {
            LabelText = label;
            SlowdownHint = new HintVM();
        }

        internal void Update(WeightTableRow row)
        {
            BeforeText = row.BeforeText;
            ChangeText = row.ChangeText;
            AfterText = row.AfterText;
            CapacityText = row.CapacityText;
            LeftText = row.LeftText;
            LeftColor = UiColors.ForLimit(row.Over);
            SlowdownText = row.SlowdownText;
            bool slowed = row.Known && row.SpeedLoss > 0;
            SlowdownColor = UiColors.ForLimit(slowed);
            SlowdownHint.Text = !row.Known ? ""
                : slowed
                    ? UiText.S2("ss_ui_weight_slow_hint",
                        "{SLOW}: the game's Overburdened - the load after the deal is {OVER} over the capacity.",
                        "SLOW", row.SlowdownText, "OVER", UiFormat.Kg(-row.Left))
                    : UiText.S("ss_ui_weight_fast_hint", "Within the capacity: the load after the deal does not slow the party.");
        }

        [DataSourceProperty] public string LabelText { get; }
        [DataSourceProperty] public HintVM SlowdownHint { get; }
        [DataSourceProperty] public string MutedColor => UiColors.Muted;
        [DataSourceProperty] public string TextColor => UiColors.Text;

        [DataSourceProperty]
        public string BeforeText
        {
            get => _beforeText;
            set { if (value != _beforeText) { _beforeText = value; OnPropertyChangedWithValue(value, nameof(BeforeText)); } }
        }

        [DataSourceProperty]
        public string ChangeText
        {
            get => _changeText;
            set { if (value != _changeText) { _changeText = value; OnPropertyChangedWithValue(value, nameof(ChangeText)); } }
        }

        [DataSourceProperty]
        public string AfterText
        {
            get => _afterText;
            set { if (value != _afterText) { _afterText = value; OnPropertyChangedWithValue(value, nameof(AfterText)); } }
        }

        [DataSourceProperty]
        public string CapacityText
        {
            get => _capacityText;
            set { if (value != _capacityText) { _capacityText = value; OnPropertyChangedWithValue(value, nameof(CapacityText)); } }
        }

        [DataSourceProperty]
        public string LeftText
        {
            get => _leftText;
            set { if (value != _leftText) { _leftText = value; OnPropertyChangedWithValue(value, nameof(LeftText)); } }
        }

        [DataSourceProperty]
        public string LeftColor
        {
            get => _leftColor;
            set { if (value != _leftColor) { _leftColor = value; OnPropertyChangedWithValue(value, nameof(LeftColor)); } }
        }

        [DataSourceProperty]
        public string SlowdownText
        {
            get => _slowdownText;
            set { if (value != _slowdownText) { _slowdownText = value; OnPropertyChangedWithValue(value, nameof(SlowdownText)); } }
        }

        [DataSourceProperty]
        public string SlowdownColor
        {
            get => _slowdownColor;
            set { if (value != _slowdownColor) { _slowdownColor = value; OnPropertyChangedWithValue(value, nameof(SlowdownColor)); } }
        }
    }
}
