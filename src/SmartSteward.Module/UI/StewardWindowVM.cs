using System;
using System.Linq;
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
    /// Instructions), Do all (the button read "Do it" until step 29) and Close. Every command is wrapped (<see cref="Guard"/>) — a bug closes the window
    /// with a log line instead of reaching Gauntlet, which rethrows it into the game (RESEARCH §14). Getters only
    /// return fields.
    /// <para>Re-planning [decided: Claude, 2026.09.27 — step 7]: a settings change (Prices or Instructions tab, or
    /// the file) marks the plan stale; showing the Suggestion tab again plans afresh on the same snapshot and carries
    /// the player's edited rows over (<see cref="PlanCarryOver"/>). Do all looks at the world again (a new snapshot)
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
            CloseText = UiText.S("ss_ui_close", "Close"); // was "Not now" until round 3 (Anton 2026.09.28)
            DoItText = UiText.S("ss_ui_do_all", "Do all"); // step 29: "Do it" -> "Do all" beside every line's own Do
            DoItHint = new HintVM();
            Suggestion = new SuggestionTabVM(RefreshDoIt, part => Guard("do part " + part.Id, () => RunPart(part)));
            Suggestion.SetPlan(plan, visit);
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

        /// <summary>The window is on screen (StewardWindow.Open, after the popup's verdict).</summary>
        internal void OnShown() => Guard("shown", Suggestion.MarkShown);

        /// <summary>The settlement the window plans for.</summary>
        internal Settlement Settlement => _settlement;

        /// <summary>Would the arrival popup open on this plan (DESIGN §6: market open, rows, and — with PopupOnlyWithChanges
        /// — something to suggest)?</summary>
        internal PopupVerdict PopupVerdict(bool onlyWithChanges)
        {
            var plan = Suggestion.Plan;
            return plan == null
                ? Core.Presentation.PopupVerdict.NothingPlanned
                : ArrivalPopup.Decide(plan, _visit.Snapshot.CanTrade, onlyWithChanges);
        }

        /// <summary>Every frame while open: the window closes itself when the party is no longer where it planned.</summary>
        internal void Tick()
        {
            var main = MobileParty.MainParty;
            if (main == null || main.CurrentSettlement != _settlement)
            {
                ModLog.Info("window", "the party left " + _settlement.Name + " - closing");
                StewardWindow.Close();
                return;
            }
            // A Goal box the player left commits here, outside the widget's own event (step 23).
            Guard("goal", Suggestion.FlushGoal);
        }

        /// <summary>Escape inside a text box: a Goal box shows its goal again (the Prices and Instructions boxes saved every
        /// key already).</summary>
        internal void CancelTyping() => Guard("cancel typing", Suggestion.CancelGoalTyping);

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
            if (!StewardWindow.IsCurrent(this))
            {
                OnFinalize(); // a window that is gone (or of an earlier campaign) never re-plans — let go
                return;
            }
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
            Suggestion.SetPlan(plan, _visit);
            RefreshDoIt();
        }

        private void RefreshDoIt()
        {
            var plan = Suggestion.Plan;
            bool can = plan != null && PlanFooter.CanExecute(plan);
            CanDoIt = can;
            DoItHint.Text = plan == null ? ""
                : can ? UiText.S("ss_ui_do_all_hint", "Carry out the whole table at once - every line's Do in one click.")
                : plan.Totals.CannotAfford ? UiLabels.Warning(PlanWarning.CannotAfford, 0, 0)
                : UiText.S("ss_ui_nothing_to_do", "Nothing to do.");
        }

        // ── commands ───────────────────────────────────────────────────────────────────────────────────────

        public void ExecuteSelectSuggestion() => Guard("tab", () => SelectTab(0));

        public void ExecuteSelectPrices() => Guard("tab", () => SelectTab(1));

        public void ExecuteSelectInstructions() => Guard("tab", () => SelectTab(2));

        /// <summary>"Close" and Escape: close, nothing done.</summary>
        public void ExecuteClose() => Guard("close", () =>
        {
            Suggestion.FlushGoal(); // the click took the focus from a Goal box: that goal stands
            StewardWindow.Close();
        });

        /// <summary>Do all: the executor runs the plan's transactions (step 6), the result shows, and the window plans
        /// afresh on a new snapshot.</summary>
        public void ExecuteDoIt() => Guard("do all", DoIt);

        private void SelectTab(int tab)
        {
            Suggestion.FlushGoal();
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
            Suggestion.FlushGoal(); // a goal typed and left by this very click goes into the deal
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
                ModLog.Info("window", "after Do all: no plan - " + whyNot);
                StewardWindow.Close();
                return;
            }
            _visit = visit;
            _planStale = false;
            Suggestion.SetPlan(PlanFor(visit, "after Do all"), visit);
            Suggestion.SetStatus(summary);
            RefreshDoIt();
        }

        /// <summary>
        /// "Do" on a section's title line or on a line that is a deal of its own (PLAN step 27, DESIGN §1.1 "Do just this part"):
        /// the executor runs ONLY that part's transactions of the plan as it stands (Core <see cref="StewardPlan.DealOf"/>: the
        /// player's edits and goals included, cut where the purse alone stops them) through Do all's own path and checks; no setting
        /// and no goal changes. Then the window plans afresh on a new snapshot and STAYS open, the player's touched rows of every
        /// OTHER part put back (<see cref="PlanCarryOver.Capture(StewardPlan, PlanPart)"/>).
        /// </summary>
        private void RunPart(PlanPart part)
        {
            Suggestion.FlushGoal(); // a goal typed and left by this very click goes into the part
            ReplanIfStale();
            var plan = Suggestion.Plan;
            if (plan == null)
                return;
            var deal = plan.DealOf(part);
            if (!deal.CanRun)
            {
                ModLog.Info("window", "part " + part.Id + ": not run - " + deal.Block);
                return; // greyed: the button should not have fired
            }
            var carry = PlanCarryOver.Capture(plan, part);
            ModLog.Info("window", "part " + part.Id + ": " + deal.Transactions.Count + " of " + deal.Planned + " transactions"
                                  + (deal.CutUnits > 0 ? ", " + deal.CutUnits + " units cut (" + deal.CutBy + " " + deal.Floor + ")" : "")
                                  + ", expected " + deal.Gold + " denari; carrying " + carry.Edits.Count + " edits of the other parts");
            var report = PlanExecutor.Execute(plan, _visit, deal.Transactions, part.Id);
            ModLog.Info("execute", report.LogLines().Select(line => "part " + part.Id + ": " + line));
            string summary = Summary(report, UiLabels.PartName(part, plan));
            InformationManager.DisplayMessage(new InformationMessage(summary));
            StewardMenu.RefreshCurrentMenu();

            var visit = SnapshotBuilder.Build(_settlement, out string whyNot);
            if (visit == null)
            {
                ModLog.Info("window", "after part " + part.Id + ": no plan - " + whyNot);
                StewardWindow.Close();
                return;
            }
            _visit = visit;
            _planStale = false;
            var fresh = PlanFor(visit, "after part " + part.Id);
            if (!carry.IsEmpty)
            {
                int applied = carry.ApplyTo(fresh);
                ModLog.Info("plan", "carried " + applied + " of " + carry.Edits.Count + " edited rows of the other parts over");
            }
            Suggestion.SetPlan(fresh, visit);
            Suggestion.SetStatus(summary);
            RefreshDoIt();
        }

        /// <summary>The player's one line about a run, through TextObjects: "Steward: 14 of 16 done. Denari 12,400 » 10,930." — a
        /// part's run (step 27) names the part: "Steward (Prisoners): 2 of 2 done. …".</summary>
        private static string Summary(ExecutionReport report, string? part = null)
        {
            if (report.Abort != null)
                return part == null
                    ? UiText.S("ss_ui_done_abort", "Steward: nothing was done - see smart_steward.log.")
                    : UiText.S1("ss_ui_part_done_abort", "Steward ({PART}): nothing was done - see smart_steward.log.", "PART", part);
            var line = part == null
                ? UiText.T("ss_ui_done", "Steward: {DONE} of {ALL} done. Denari {BEFORE} » {AFTER}.")
                : UiText.T("ss_ui_part_done", "Steward ({PART}): {DONE} of {ALL} done. Denari {BEFORE} » {AFTER}.")
                    .SetTextVariable("PART", part);
            string text = line
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
