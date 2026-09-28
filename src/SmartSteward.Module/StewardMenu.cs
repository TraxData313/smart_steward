using System;
using System.Linq;
using SmartSteward.UI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;

namespace SmartSteward
{
    /// <summary>
    /// "Party Steward" in the town and village menus (DESIGN §6), right after Trade — opens the window. Always there
    /// while the mod is enabled (ModEnabled), the Full-autonomous steward included. The arrival popup, the leave
    /// warning and autonomy are <see cref="StewardTriggers"/> (PLAN step 8).
    /// </summary>
    internal static class StewardMenu
    {
        public const string OptionId = "smart_steward_open";

        /// <summary>From the behavior's OnSessionLaunched, after vanilla built its menus (RESEARCH §10).</summary>
        public static void AddMenus(CampaignGameStarter starter)
        {
            foreach (var menuId in new[] { "town", "village" })
                starter.AddGameMenuOption(menuId, OptionId, "{=ss_menu}Party Steward", OnCondition, OnConsequence,
                    false, IndexAfter(menuId, "trade"));
            ModLog.Info("campaign", "Party Steward added to the town and village menus");
        }

        /// <summary>The index right after <paramref name="optionId"/> in the menu's live list; -1 (the end) when it is
        /// not there.</summary>
        public static int IndexAfter(string menuId, string optionId)
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

        /// <summary>Re-reads the current menu's options (gold, stock and prisoners changed under it).</summary>
        public static void RefreshCurrentMenu()
        {
            try
            {
                var context = Campaign.Current?.CurrentMenuContext;
                if (context?.GameMenu != null)
                    Campaign.Current!.GameMenuManager.RefreshMenuOptions(context);
            }
            catch (Exception ex)
            {
                ModLog.Error("menu", "refreshing the menu", ex);
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

        private static void OnConsequence(MenuCallbackArgs args)
        {
            try
            {
                StewardWindow.Open(MobileParty.MainParty?.CurrentSettlement ?? Settlement.CurrentSettlement,
                    "the Party Steward menu entry");
            }
            catch (Exception ex)
            {
                ModLog.Error("window", "menu entry", ex);
            }
        }
    }
}
