using System;
using SmartSteward.Adapter;
using SmartSteward.Core.Execution;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;
using SmartSteward.UI;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

namespace SmartSteward
{
    /// <summary>
    /// The Full-autonomous steward (DESIGN §6): on arrival — once per visit, on a quiet map (<see cref="StewardTriggers"/>)
    /// — plan with the autonomous floors (<see cref="PlanMode.Autonomous"/>: AutonomousMinGold, no tavern), carry the
    /// plan out through the same executor as Deal all, and sum it up in the message log. No window, no popup, no warning.
    /// The whole plan and every transaction go to smart_steward.log.
    /// </summary>
    internal static class AutonomousRun
    {
        public static void Run(Settlement settlement)
        {
            try
            {
                string? busy = EncounterGuard.WhyBusy(settlement, checkMenu: true);
                if (busy != null)
                {
                    EncounterGuard.LogAside("the autonomous steward at " + settlement.Name, busy);
                    return;
                }
                var visit = SnapshotBuilder.Build(settlement, out string whyNot);
                if (visit == null)
                {
                    ModLog.Info("auto", "no plan at " + settlement.Name + " - " + whyNot);
                    return;
                }
                var settings = SettingsHost.Current;
                ModLog.Info("auto", "autonomous steward at " + visit.Settlement.Name + ": " + SnapshotBuilder.Describe(visit.Snapshot));
                ModLog.Info("auto", visit.Oracle.SelfCheck(visit.Snapshot));
                var plan = StewardPlanner.Plan(visit.Snapshot, settings, visit.Oracle, PlanMode.Autonomous);
                ModLog.Info("auto", "floors while autonomous: all purchases " + plan.Floors.All + ", animals "
                    + Math.Max(plan.Floors.All, plan.Floors.Animals));
                ModLog.Info("plan", PlanReport.Full(plan));
                if (!PlanFooter.CanExecute(plan))
                {
                    ModLog.Info("auto", "nothing to do");
                    return; // nothing happened -> no message (DESIGN §6)
                }

                var report = PlanExecutor.Execute(plan, visit);
                ModLog.Info("execute", report.LogLines());
                var summary = AutonomousReport.From(plan, report);
                // Step 33: "Steward report at X:" - the old "Steward at X:" read like the Steward skill had changed (Anton).
                var lines = summary.Lines(
                    UiText.S1("ss_report_head_at", "Steward report at {SETTLEMENT}:", "SETTLEMENT", visit.Settlement.Name?.ToString() ?? ""),
                    UiText.S("ss_report_head_jobs", "Steward report, by job:"),
                    Words(),
                    UiLabels.SummaryWords());
                foreach (var line in lines)
                {
                    ModLog.Info("auto", line);
                    InformationManager.DisplayMessage(new InformationMessage(line));
                }
                StewardMenu.RefreshCurrentMenu();
            }
            catch (Exception ex)
            {
                ModLog.Error("auto", "the autonomous steward at " + settlement.Name, ex);
                InformationManager.DisplayMessage(new InformationMessage(UiText.S("ss_auto_failed",
                    "Smart Steward: the autonomous steward failed - see smart_steward.log.")));
            }
        }

        /// <summary>The report's words through TextObject ids (PLAN step 9 gathers them into the strings file).</summary>
        private static ReportWords Words() => new ReportWords
        {
            Food = UiText.S("ss_auto_food", "food"),
            Mounts = UiText.S("ss_auto_mounts", "mounts"),
            Other = UiText.S("ss_auto_other", "other"),
            Prisoners = UiText.S("ss_auto_prisoners", "prisoners"),
            Kind = UiText.S("ss_auto_kind", "kind"),
            Kinds = UiText.S("ss_auto_kinds", "kinds"),
            Sold = UiText.S("ss_auto_sold", "sold"),
            Ransomed = UiText.S("ss_auto_ransomed", "ransomed"),
            Donated = UiText.S("ss_auto_donated", "donated"),
            Influence = UiText.S("ss_auto_influence", "influence"),
        };
    }
}
