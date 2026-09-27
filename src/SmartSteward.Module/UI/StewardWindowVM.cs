using System;
using SmartSteward.Adapter;
using SmartSteward.Core.Execution;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace SmartSteward.UI
{
    /// <summary>
    /// The Party Steward window's root view model (DESIGN §1): the title, the three tabs (Suggestion, Prices,
    /// Instructions), Do it and Not now. Every command is wrapped (<see cref="Guard"/>) — a bug closes the window
    /// with a log line instead of reaching Gauntlet, which rethrows it into the game (RESEARCH §14). Getters only
    /// return fields.
    /// <para>Re-planning [decided: Claude, 2026.09.27 — step 7]: a settings change (Prices or Instructions tab, or
    /// the file) marks the plan stale; showing the Suggestion tab again plans afresh on the same snapshot and carries
    /// the player's edited rows over (<see cref="PlanCarryOver"/>). Do it looks at the world again (a new snapshot)
    /// and plans afresh with no carry-over — the edits were carried out.</para>
    /// </summary>
    public sealed class StewardWindowVM : ViewModel
    {
        private readonly Settlement _settlement;
        private GameVisit _visit;
        private bool _planStale;
        private bool _subscribed;

        private string _titleText = "";
        private bool _isSuggestionSelected = true;
        private bool _isPricesSelected;
        private bool _isInstructionsSelected;
        private bool _canDoIt;

        private StewardWindowVM(Settlement settlement, GameVisit visit, StewardPlan plan)
        {
            _settlement = settlement;
            _visit = visit;
            _titleText = UiText.S1("ss_ui_title", "Party Steward — {SETTLEMENT}", "SETTLEMENT",
                settlement.Name?.ToString() ?? "");
            SuggestionTabText = UiText.S("ss_ui_tab_suggestion", "Suggestion");
            PricesTabText = UiText.S("ss_ui_tab_prices", "Prices");
            InstructionsTabText = UiText.S("ss_ui_tab_instructions", "Instructions");
            CloseText = UiText.S("ss_ui_not_now", "Not now");
            DoItText = UiText.S("ss_ui_do_it", "Do it");
            DoItHint = new HintVM();
            Suggestion = new SuggestionTabVM(RefreshDoIt);
            Suggestion.SetPlan(plan, settlement);
            Prices = new PricesTabVM();
            Instructions = new InstructionsTabVM();
            RefreshDoIt();
            SettingsHost.Service.Changed += OnSettingsChanged;
            _subscribed = true;
        }

        /// <summary>Snapshot + plan for the settlement the party stands in; null (and a message, unless
        /// <paramref name="quiet"/>) where the steward cannot work.</summary>
        internal static StewardWindowVM? Create(Settlement? settlement, bool quiet = false)
        {
            SettingsHost.ReloadIfChanged();
            settlement ??= MobileParty.MainParty?.CurrentSettlement ?? Settlement.CurrentSettlement;
            var visit = SnapshotBuilder.Build(settlement, out string whyNot);
            if (visit == null)
            {
                ModLog.Info("window", "no plan - " + whyNot);
                if (!quiet)
                    InformationManager.DisplayMessage(new InformationMessage(UiText.S1("ss_ui_no_plan",
                        "Smart Steward: nothing to plan here ({WHY}).", "WHY", whyNot)));
                return null;
            }
            var plan = PlanFor(visit, "window");
            return new StewardWindowVM(visit.Settlement, visit, plan);
        }

        /// <summary>Plans with the settings in effect and writes the whole plan to the log (DESIGN §8).</summary>
        private static StewardPlan PlanFor(GameVisit visit, string why)
        {
            ModLog.Info("plan", why + " at " + visit.Settlement.Name + ": " + SnapshotBuilder.Describe(visit.Snapshot));
            ModLog.Info("plan", visit.Oracle.SelfCheck(visit.Snapshot));
            var plan = StewardPlanner.Plan(visit.Snapshot, SettingsHost.Current, visit.Oracle);
            ModLog.Info("plan", PlanReport.Full(plan));
            return plan;
        }

        /// <summary>The settlement the window plans for.</summary>
        internal Settlement Settlement => _settlement;

        /// <summary>The steward suggests something (PopupOnlyWithChanges opens the window only then).</summary>
        internal bool HasChanges => Suggestion.Plan?.HasChanges ?? false;

        /// <summary>Every frame while open: the window closes itself when the party is no longer where it planned.</summary>
        internal void Tick()
        {
            var main = MobileParty.MainParty;
            if (main == null || main.CurrentSettlement != _settlement)
            {
                ModLog.Info("window", "the party left " + _settlement.Name + " - closing");
                StewardWindow.Close();
            }
        }

        public override void OnFinalize()
        {
            base.OnFinalize();
            if (_subscribed)
            {
                SettingsHost.Service.Changed -= OnSettingsChanged;
                _subscribed = false;
            }
        }

        // ── settings changes and re-planning ───────────────────────────────────────────────────────────

        /// <summary>The settings service changed a value (our tabs, MCM, a reloaded file): the plan is stale. The tab
        /// on show follows at once — the Suggestion tab re-plans, the others refresh everything but the box being
        /// typed in; a tab shown later refreshes whole when selected.</summary>
        private void OnSettingsChanged()
        {
            _planStale = true;
            Guard("settings changed", () =>
            {
                if (IsSuggestionSelected)
                    ReplanIfStale();
                else if (IsPricesSelected)
                    Prices.RefreshAll(includeTypedTexts: false);
                else if (IsInstructionsSelected)
                    Instructions.RefreshAll(includeTexts: false);
            });
        }

        /// <summary>Plans again on the same snapshot (nothing traded since) and carries the player's edits over.</summary>
        private void ReplanIfStale()
        {
            if (!_planStale)
                return;
            _planStale = false;
            var old = Suggestion.Plan;
            var carry = old == null ? null : PlanCarryOver.Capture(old);
            var plan = PlanFor(_visit, "re-plan (settings changed)");
            if (carry != null && !carry.IsEmpty)
            {
                int applied = carry.ApplyTo(plan);
                ModLog.Info("plan", "carried " + applied + " of " + carry.Edits.Count + " edited rows over");
            }
            Suggestion.SetPlan(plan, _settlement);
            RefreshDoIt();
        }

        private void RefreshDoIt()
        {
            var plan = Suggestion.Plan;
            bool can = plan != null && PlanFooter.CanExecute(plan);
            CanDoIt = can;
            DoItHint.Text = can || plan == null ? ""
                : plan.Totals.CannotAfford ? UiLabels.Warning(PlanWarning.CannotAfford, 0, 0)
                : UiText.S("ss_ui_nothing_to_do", "Nothing to do.");
        }

        // ── commands ───────────────────────────────────────────────────────────────────────────────────────

        public void ExecuteSelectSuggestion() => Guard("tab", () => SelectTab(0));

        public void ExecuteSelectPrices() => Guard("tab", () => SelectTab(1));

        public void ExecuteSelectInstructions() => Guard("tab", () => SelectTab(2));

        /// <summary>"Not now" and Escape: close, nothing done.</summary>
        public void ExecuteClose() => Guard("close", StewardWindow.Close);

        /// <summary>Do it: the executor runs the plan's transactions (step 6), the result shows, and the window plans
        /// afresh on a new snapshot.</summary>
        public void ExecuteDoIt() => Guard("do it", DoIt);

        private void SelectTab(int tab)
        {
            if (tab == 1)
            {
                Prices.EnsureBuilt(_visit);
                Prices.RefreshAll(includeTypedTexts: true);
            }
            else if (tab == 2)
            {
                Instructions.EnsureBuilt(_visit);
                Instructions.RefreshAll(includeTexts: true);
            }
            IsSuggestionSelected = tab == 0;
            IsPricesSelected = tab == 1;
            IsInstructionsSelected = tab == 2;
            if (tab == 0)
                ReplanIfStale();
        }

        private void DoIt()
        {
            ReplanIfStale();
            var plan = Suggestion.Plan;
            if (plan == null || !PlanFooter.CanExecute(plan))
                return;
            var report = PlanExecutor.Execute(plan, _visit);
            ModLog.Info("execute", report.LogLines());
            string summary = Summary(report);
            InformationManager.DisplayMessage(new InformationMessage(summary));
            StewardMenu.RefreshCurrentMenu();

            var visit = SnapshotBuilder.Build(_settlement, out string whyNot);
            if (visit == null)
            {
                ModLog.Info("window", "after Do it: no plan - " + whyNot);
                StewardWindow.Close();
                return;
            }
            _visit = visit;
            _planStale = false;
            Suggestion.SetPlan(PlanFor(visit, "after Do it"), _settlement);
            Suggestion.SetStatus(summary);
            RefreshDoIt();
        }

        /// <summary>The player's one line about a run, through TextObjects: "Done: 14 of 16. Gold 12,400 » 10,930."</summary>
        private static string Summary(ExecutionReport report)
        {
            if (report.Abort != null)
                return UiText.S("ss_ui_done_abort", "Steward: nothing was done - see smart_steward.log.");
            string text = UiText.T("ss_ui_done", "Steward: {DONE} of {ALL} done. Gold {BEFORE} » {AFTER}.")
                .SetTextVariable("DONE", UiFormat.Money(report.FullyDone))
                .SetTextVariable("ALL", UiFormat.Money(report.Planned))
                .SetTextVariable("BEFORE", UiFormat.Money(report.GoldBefore))
                .SetTextVariable("AFTER", UiFormat.Money(report.GoldAfter))
                .ToString();
            if (report.CutShort + report.NotDone > 0)
                text += " " + UiText.S2("ss_ui_done_partly", "{CUT} cut short, {SKIPPED} skipped - see smart_steward.log.",
                    "CUT", UiFormat.Money(report.CutShort), "SKIPPED", UiFormat.Money(report.NotDone));
            return text;
        }

        /// <summary>Runs a command, a two-way setter or a settings callback; any exception is logged and closes the
        /// window (on the next tick) with a message — it never reaches Gauntlet, which would rethrow it into the game.</summary>
        internal static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                ModLog.Error("window", what, ex);
                try
                {
                    InformationManager.DisplayMessage(new InformationMessage(UiText.S("ss_ui_failed",
                        "Smart Steward: something went wrong - the window closed. See smart_steward.log.")));
                }
                catch
                {
                    // the message is a courtesy
                }
                StewardWindow.RequestClose();
            }
        }

        // ── bound properties ───────────────────────────────────────────────────────────────────────────────

        [DataSourceProperty] public SuggestionTabVM Suggestion { get; }

        [DataSourceProperty] public PricesTabVM Prices { get; }

        [DataSourceProperty] public InstructionsTabVM Instructions { get; }

        [DataSourceProperty] public string SuggestionTabText { get; }

        [DataSourceProperty] public string PricesTabText { get; }

        [DataSourceProperty] public string InstructionsTabText { get; }

        [DataSourceProperty] public string CloseText { get; }

        [DataSourceProperty] public string DoItText { get; }

        [DataSourceProperty] public HintVM DoItHint { get; }

        [DataSourceProperty]
        public string TitleText
        {
            get => _titleText;
            set { if (value != _titleText) { _titleText = value; OnPropertyChangedWithValue(value, nameof(TitleText)); } }
        }

        [DataSourceProperty]
        public bool CanDoIt
        {
            get => _canDoIt;
            set { if (value != _canDoIt) { _canDoIt = value; OnPropertyChangedWithValue(value, nameof(CanDoIt)); } }
        }

        [DataSourceProperty]
        public bool IsSuggestionSelected
        {
            get => _isSuggestionSelected;
            set { if (value != _isSuggestionSelected) { _isSuggestionSelected = value; OnPropertyChangedWithValue(value, nameof(IsSuggestionSelected)); } }
        }

        [DataSourceProperty]
        public bool IsPricesSelected
        {
            get => _isPricesSelected;
            set { if (value != _isPricesSelected) { _isPricesSelected = value; OnPropertyChangedWithValue(value, nameof(IsPricesSelected)); } }
        }

        [DataSourceProperty]
        public bool IsInstructionsSelected
        {
            get => _isInstructionsSelected;
            set { if (value != _isInstructionsSelected) { _isInstructionsSelected = value; OnPropertyChangedWithValue(value, nameof(IsInstructionsSelected)); } }
        }
    }
}
