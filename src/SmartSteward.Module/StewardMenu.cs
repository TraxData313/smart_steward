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
    /// <para>War Sails' port (round 3): a party docking at a town by sea lands in the DLC's <c>port_menu</c> (RESEARCH
    /// §18), which trades with the town's own market — so the entry goes there too. War Sails builds that menu in its
    /// OnAfterSessionLaunched (after ours), so the entry is added the first time the menu opens
    /// (<see cref="EnsurePortEntry"/>), by id: without War Sails nothing ever happens, and nothing references the DLC.</para>
    /// </summary>
    internal static class StewardMenu
    {
        public const string OptionId = "smart_steward_open";

        /// <summary>War Sails' port menu (NavalDLC <c>NavalTransitionCampaignBehavior</c>) — looked up by id only.</summary>
        public const string PortMenuId = "port_menu";

        private const string EntryText = "{=ss_menu}Party Steward";

        /// <summary>From the behavior's OnSessionLaunched, after vanilla built its menus (RESEARCH §10).</summary>
        public static void AddMenus(CampaignGameStarter starter)
        {
            foreach (var menuId in new[] { "town", "village" })
                starter.AddGameMenuOption(menuId, OptionId, EntryText, OnCondition, OnConsequence,
                    false, IndexAfter(menuId, "trade"));
            ModLog.Info("campaign", "Party Steward added to the town and village menus");
        }

        /// <summary>Adds the entry to War Sails' port menu (right after its Trade) the first time that menu opens in a
        /// campaign session — the menus are rebuilt on every load, so "not there yet" is the per-session check. Called
        /// from <c>GameMenuOpened</c>, which runs before the menu's view is built (MenuContext.HandleStates: OnInit and
        /// the event, then OnMenuCreate), so the entry shows on this very opening. A fresh <c>CampaignGameStarter</c> over
        /// the campaign's own menu manager is the public way in (its constructor only stores the two managers).</summary>
        public static void EnsurePortEntry(GameMenu menu)
        {
            try
            {
                if (menu == null || menu.StringId != PortMenuId || menu.MenuOptions.Any(o => o.IdString == OptionId))
                    return;
                var campaign = Campaign.Current;
                if (campaign?.GameMenuManager == null)
                    return;
                new CampaignGameStarter(campaign.GameMenuManager, campaign.ConversationManager)
                    .AddGameMenuOption(PortMenuId, OptionId, EntryText, OnCondition, OnConsequence, false,
                        IndexAfter(PortMenuId, "trade"));
                ModLog.Info("campaign", "Party Steward added to War Sails' port menu");
            }
            catch (Exception ex)
            {
                ModLog.Error("campaign", "adding Party Steward to the port menu", ex);
            }
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
                bool port = args?.MenuContext?.GameMenu?.StringId == PortMenuId;
                StewardWindow.Open(MobileParty.MainParty?.CurrentSettlement ?? Settlement.CurrentSettlement,
                    port ? "the Party Steward entry at the port" : "the Party Steward menu entry");
            }
            catch (Exception ex)
            {
                ModLog.Error("window", "menu entry", ex);
            }
        }
    }
}
