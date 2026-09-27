using System;
using System.Linq;
using SmartSteward.Core.Planning;
using SmartSteward.Adapter;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace SmartSteward
{
    /// <summary>
    /// TEMPORARY (PLAN step 6 — step 8 removes it): "Party Steward (debug)" in the town and village menus. It builds
    /// the snapshot, plans, writes the full plan to smart_steward.log and shows a vanilla inquiry with a compact
    /// summary and Execute / Cancel — the whole adapter end to end, before the real window (step 7) exists. A
    /// second town entry, "(debug, + tavern hires)", adds one wanderer and the whole mercenary band through the plan
    /// editor, so the hire executors can be tried too.
    /// </summary>
    internal static class DebugDoor
    {
        private const string OptionId = "smart_steward_debug";
        private const string TavernOptionId = "smart_steward_debug_tavern";

        /// <summary>Called from the behavior's OnSessionLaunched — vanilla has built its menus by then (RESEARCH §10);
        /// the entries go right after Trade, or at the end when Trade is not found.</summary>
        public static void AddMenus(CampaignGameStarter starter)
        {
            foreach (var menuId in new[] { "town", "village" })
                starter.AddGameMenuOption(menuId, OptionId, "{=ss_dbg_menu}Party Steward (debug)",
                    OnCondition, OnConsequence, false, IndexAfter(menuId, "trade"));
            starter.AddGameMenuOption("town", TavernOptionId, "{=ss_dbg_menu_tavern}Party Steward (debug, + tavern hires)",
                OnCondition, OnTavernConsequence, false, IndexAfter("town", OptionId));
            ModLog.Info("debug", "debug door added to the town and village menus");
        }

        private static int IndexAfter(string menuId, string optionId)
        {
            try
            {
                var options = Campaign.Current?.GameMenuManager?.GetGameMenu(menuId)?.MenuOptions?.ToList();
                if (options == null)
                    return -1;
                int index = options.FindIndex(o => o.IdString == optionId);
                return index < 0 ? -1 : index + 1;
            }
            catch
            {
                return -1;
            }
        }

        private static bool OnCondition(MenuCallbackArgs args)
        {
            args.optionLeaveType = GameMenuOption.LeaveType.Manage;
            try
            {
                return SettingsHost.Current.ModEnabled;
            }
            catch
            {
                return false;
            }
        }

        private static void OnConsequence(MenuCallbackArgs args) => Open(withTavernHires: false);

        private static void OnTavernConsequence(MenuCallbackArgs args) => Open(withTavernHires: true);

        private static void Open(bool withTavernHires)
        {
            try
            {
                SettingsHost.ReloadIfChanged();
                var settings = SettingsHost.Current;
                var settlement = MobileParty.MainParty?.CurrentSettlement ?? Settlement.CurrentSettlement;
                var visit = SnapshotBuilder.Build(settlement, out string whyNot);
                if (visit == null)
                {
                    ModLog.Info("plan", "debug door: no plan - " + whyNot);
                    InformationManager.DisplayMessage(new InformationMessage("Smart Steward: " + whyNot + "."));
                    return;
                }
                ModLog.Info("plan", "debug door at " + visit.Settlement.Name + ": " + SnapshotBuilder.Describe(visit.Snapshot));
                ModLog.Info("plan", visit.Oracle.SelfCheck(visit.Snapshot));

                var plan = StewardPlanner.Plan(visit.Snapshot, settings, visit.Oracle);
                if (withTavernHires)
                    AddTavernHires(plan);
                foreach (var line in PlanReport.Full(plan))
                    ModLog.Info("plan", line);

                bool any = plan.Transactions.Count > 0;
                InformationManager.ShowInquiry(new InquiryData(
                    new TextObject("{=ss_dbg_title}Party Steward (debug)").ToString(),
                    PlanReport.Compact(plan),
                    true, any,
                    any ? new TextObject("{=ss_dbg_execute}Execute").ToString() : new TextObject("{=ss_dbg_close}Close").ToString(),
                    new TextObject("{=ss_dbg_cancel}Cancel").ToString(),
                    () => { if (any) Execute(plan, visit); },
                    () => ModLog.Info("plan", "debug door: cancelled")), pauseGameActiveState: true);
            }
            catch (Exception ex)
            {
                ModLog.Error("plan", "debug door", ex);
                InformationManager.DisplayMessage(new InformationMessage("Smart Steward: the steward failed - see smart_steward.log."));
            }
        }

        /// <summary>The steward never proposes a hire (DESIGN §2.7); for the debug door, the player's hand does it:
        /// +1 on the first wanderer who can be hired, "all" on the mercenary band.</summary>
        private static void AddTavernHires(StewardPlan plan)
        {
            var tavern = plan.Section(PlanSectionKind.Tavern);
            if (tavern == null)
            {
                ModLog.Info("plan", "debug door: no tavern rows here");
                return;
            }
            var wanderer = tavern.Rows.FirstOrDefault(r => r.Tavern?.Kind == TavernRowKind.Wanderer && r.CanIncrease);
            if (wanderer != null)
                ModLog.Info("plan", "debug door: hire " + wanderer.Id + " -> " + plan.Increase(wanderer.Id).After);
            var band = tavern.Rows.FirstOrDefault(r => r.Tavern?.Kind == TavernRowKind.Mercenaries);
            if (band != null)
            {
                var result = plan.Increase(band.Id, EditSize.All);
                ModLog.Info("plan", "debug door: hire " + band.Id + " -> " + result.After + (result.Block == EditBlock.None ? "" : " (" + result.Block + ")"));
            }
        }

        private static void Execute(StewardPlan plan, GameVisit visit)
        {
            try
            {
                var report = PlanExecutor.Execute(plan, visit);
                foreach (var line in report.LogLines())
                    ModLog.Info("execute", line);
                InformationManager.DisplayMessage(new InformationMessage(report.Summary()));
            }
            catch (Exception ex)
            {
                ModLog.Error("execute", "debug door", ex);
                InformationManager.DisplayMessage(new InformationMessage("Smart Steward: the steward failed - see smart_steward.log."));
            }
            try
            {
                var context = Campaign.Current?.CurrentMenuContext;
                if (context?.GameMenu != null)
                    Campaign.Current!.GameMenuManager.RefreshMenuOptions(context);
            }
            catch (Exception ex)
            {
                ModLog.Error("execute", "refreshing the menu", ex);
            }
        }
    }
}
