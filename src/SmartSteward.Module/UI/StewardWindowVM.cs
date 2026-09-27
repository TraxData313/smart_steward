using System;
using SmartSteward.Adapter;
using SmartSteward.Core.Planning;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace SmartSteward.UI
{
    /// <summary>
    /// The Party Steward window's root view model (DESIGN §1): the title, the three tabs (Suggestion, Prices,
    /// Instructions) and the buttons. Every command is wrapped — a bug closes the window with a log line instead
    /// of reaching Gauntlet, which rethrows (RESEARCH §14). Getters only return fields.
    /// </summary>
    public sealed class StewardWindowVM : ViewModel
    {
        private readonly Settlement _settlement;
        private GameVisit _visit;
        private StewardPlan _plan;

        private string _titleText = "";
        private bool _isSuggestionSelected = true;
        private bool _isPricesSelected;
        private bool _isInstructionsSelected;

        private StewardWindowVM(Settlement settlement, GameVisit visit, StewardPlan plan)
        {
            _settlement = settlement;
            _visit = visit;
            _plan = plan;
            _titleText = UiText.S1("ss_ui_title", "Party Steward — {SETTLEMENT}", "SETTLEMENT",
                settlement.Name?.ToString() ?? "");
            SuggestionTabText = UiText.S("ss_ui_tab_suggestion", "Suggestion");
            PricesTabText = UiText.S("ss_ui_tab_prices", "Prices");
            InstructionsTabText = UiText.S("ss_ui_tab_instructions", "Instructions");
            CloseText = UiText.S("ss_ui_not_now", "Not now");
        }

        /// <summary>Snapshot + plan for the settlement the party stands in; null (and a message) where the steward
        /// cannot work.</summary>
        internal static StewardWindowVM? Create(Settlement? settlement)
        {
            SettingsHost.ReloadIfChanged();
            settlement ??= MobileParty.MainParty?.CurrentSettlement ?? Settlement.CurrentSettlement;
            var visit = SnapshotBuilder.Build(settlement, out string whyNot);
            if (visit == null)
            {
                ModLog.Info("window", "no plan - " + whyNot);
                InformationManager.DisplayMessage(new InformationMessage(UiText.S1("ss_ui_no_plan",
                    "Smart Steward: nothing to plan here ({WHY}).", "WHY", whyNot)));
                return null;
            }
            var plan = PlanFor(visit);
            return new StewardWindowVM(visit.Settlement, visit, plan);
        }

        /// <summary>Plans with the settings in effect and writes the whole plan to the log (DESIGN §8).</summary>
        private static StewardPlan PlanFor(GameVisit visit)
        {
            ModLog.Info("plan", "window at " + visit.Settlement.Name + ": " + SnapshotBuilder.Describe(visit.Snapshot));
            ModLog.Info("plan", visit.Oracle.SelfCheck(visit.Snapshot));
            var plan = StewardPlanner.Plan(visit.Snapshot, SettingsHost.Current, visit.Oracle);
            foreach (var line in PlanReport.Full(plan))
                ModLog.Info("plan", line);
            return plan;
        }

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

        // ── commands ───────────────────────────────────────────────────────────────────────────────────────

        public void ExecuteSelectSuggestion() => Guard("tab", () => SelectTab(0));

        public void ExecuteSelectPrices() => Guard("tab", () => SelectTab(1));

        public void ExecuteSelectInstructions() => Guard("tab", () => SelectTab(2));

        /// <summary>"Not now" and Escape: close, nothing done.</summary>
        public void ExecuteClose() => Guard("close", StewardWindow.Close);

        private void SelectTab(int tab)
        {
            IsSuggestionSelected = tab == 0;
            IsPricesSelected = tab == 1;
            IsInstructionsSelected = tab == 2;
        }

        /// <summary>Runs a command; any exception is logged and closes the window with a message — it never
        /// reaches Gauntlet (which would rethrow it into the game).</summary>
        internal static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                ModLog.Error("window", what, ex);
                InformationManager.DisplayMessage(new InformationMessage(UiText.S("ss_ui_failed",
                    "Smart Steward: something went wrong - the window closed. See smart_steward.log.")));
                StewardWindow.Close();
            }
        }

        // ── bound properties ───────────────────────────────────────────────────────────────────────────────

        [DataSourceProperty]
        public string TitleText
        {
            get => _titleText;
            set
            {
                if (value != _titleText)
                {
                    _titleText = value;
                    OnPropertyChangedWithValue(value, nameof(TitleText));
                }
            }
        }

        [DataSourceProperty]
        public string SuggestionTabText { get; }

        [DataSourceProperty]
        public string PricesTabText { get; }

        [DataSourceProperty]
        public string InstructionsTabText { get; }

        [DataSourceProperty]
        public string CloseText { get; }

        [DataSourceProperty]
        public bool IsSuggestionSelected
        {
            get => _isSuggestionSelected;
            set
            {
                if (value != _isSuggestionSelected)
                {
                    _isSuggestionSelected = value;
                    OnPropertyChangedWithValue(value, nameof(IsSuggestionSelected));
                }
            }
        }

        [DataSourceProperty]
        public bool IsPricesSelected
        {
            get => _isPricesSelected;
            set
            {
                if (value != _isPricesSelected)
                {
                    _isPricesSelected = value;
                    OnPropertyChangedWithValue(value, nameof(IsPricesSelected));
                }
            }
        }

        [DataSourceProperty]
        public bool IsInstructionsSelected
        {
            get => _isInstructionsSelected;
            set
            {
                if (value != _isInstructionsSelected)
                {
                    _isInstructionsSelected = value;
                    OnPropertyChangedWithValue(value, nameof(IsInstructionsSelected));
                }
            }
        }
    }
}
