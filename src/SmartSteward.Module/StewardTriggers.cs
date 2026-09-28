using System;
using System.Linq;
using SandBox.View.Map;
using SmartSteward.Adapter;
using SmartSteward.Core.Planning;
using SmartSteward.UI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.ScreenSystem;

namespace SmartSteward
{
    /// <summary>
    /// When the steward acts by itself (DESIGN §6, PLAN step 8): the arrival popup, the Full-autonomous steward and
    /// the leave warning. The behavior forwards the campaign events here; <see cref="Tick"/> runs every application
    /// frame (SubModule). Everything lives in memory — per visit, never in the save.
    /// <list type="bullet">
    /// <item><b>Arrival</b> = <c>SettlementEntered</c> for the main party, then the first <c>town</c> / <c>village</c>
    ///   menu opening (it re-fires on every return to the menu — RESEARCH §10 — so the visit remembers it is done) — or,
    ///   docking at a town by sea under War Sails, the first <c>port_menu</c> opening (round 3, RESEARCH §18: the sea
    ///   arrival goes <c>naval_town_outside</c> → <c>port_menu</c> and never shows the town menu until the player walks
    ///   in). The port menu also gets the "Party Steward" entry (<see cref="StewardMenu.EnsurePortEntry"/>).</item>
    ///   Nothing happens inside the menu event itself: the action waits until the map screen is QUIET for a few
    ///   frames (<see cref="IsQuiet"/> — the menu up, no inquiry, conversation, incident, encyclopedia, escape menu,
    ///   management screen), then opens the window or, autonomous, plans and carries out.</item>
    /// <item><b>Leave warning</b>: the leave options' consequences are wrapped (<see cref="LeaveGuard"/>) the first
    ///   time their menu opens — lazily, so War Sails' port menu (added in its OnAfterSessionLaunched, after every
    ///   OnSessionLaunched) is covered whatever the load order.</item>
    /// <item><b>ModEnabled off</b>: no popup, no autonomy, and the wraps let every leave straight through.</item>
    /// </list>
    /// </summary>
    internal static class StewardTriggers
    {
        /// <summary>Frames the map must stay quiet before the steward opens anything — the menu's own layer and a
        /// same-frame incident or inquiry get their turn first.</summary>
        private const int QuietFramesNeeded = 3;

        /// <summary>A "Leave anyway" is carried out within this many frames or dropped — a late leave must never
        /// surprise the player on a later visit to the menu.</summary>
        private const int LeaveRequestFrames = 60;

        /// <summary>One stay in one town or village.</summary>
        private sealed class Visit
        {
            public Visit(Settlement settlement) => Settlement = settlement;

            public Settlement Settlement { get; }

            /// <summary>The party walked in (SettlementEntered) — a campaign loaded inside a town is no arrival.</summary>
            public bool Arrived;

            /// <summary>The town/village menu showed after the arrival; the arrival action waits for a quiet map.</summary>
            public bool ArrivalPending;

            /// <summary>The arrival action ran (popup, autonomy — or nothing to do).</summary>
            public bool ArrivalDone;

            /// <summary>The Party Steward window was opened during this visit — the leave warning stays silent.</summary>
            public bool Reviewed;
        }

        /// <summary>A popup still waiting for a quiet map after this many frames says in the log what holds it up.</summary>
        private const int WaitReportFrames = 600;

        private static Visit? _visit;
        private static int _quietFrames;
        private static int _waitFrames;
        private static Settlement? _openRequest;
        private static LeaveGuard? _leaveRequest;
        private static int _leaveRequestFrames;

        /// <summary>The <see cref="CampaignSession.Generation"/> this state belongs to (stamped by <see cref="Reset"/>).</summary>
        private static int _generation;

        /// <summary>The settlement whose leave warning is up this frame: its "leaving" incident must not roll for a
        /// click that did not leave (see <see cref="OnIsSettlementBusy"/>).</summary>
        private static Settlement? _warnedThisFrame;

        /// <summary>The leave options the steward guards, by menu (RESEARCH §10; War Sails' port verified in
        /// NavalDLC.dll's NavalTransitionCampaignBehavior).</summary>
        private static readonly (string Menu, string Option)[] LeaveOptions =
        {
            ("town", "town_leave"),
            ("village", "leave"),
            ("village", "leave_set_sail"),
            ("village", "leave_at_sea"),
            ("port_menu", "sail_option"),
        };

        // ── campaign events (forwarded by SmartStewardBehavior) ─────────────────────────────────────────

        /// <summary>A new campaign session: nothing carries over from the last one (the menus were rebuilt too). Stamps
        /// the state with the current <see cref="CampaignSession.Generation"/>.</summary>
        public static void Reset()
        {
            _visit = null;
            _quietFrames = 0;
            _waitFrames = 0;
            _openRequest = null;
            _leaveRequest = null;
            _warnedThisFrame = null;
            _generation = CampaignSession.Generation;
            LeaveGuard.Reset();
        }

        /// <summary>What the triggers hold right now, for the campaign reset's log line ("" = nothing).</summary>
        internal static string Describe()
        {
            var parts = new System.Collections.Generic.List<string>();
            var visit = _visit;
            if (visit != null)
                parts.Add("the visit at " + visit.Settlement?.Name
                          + (visit.ArrivalPending ? " (arrival popup pending)" : "") + (visit.Reviewed ? " (reviewed)" : ""));
            if (_openRequest != null)
                parts.Add("a Review request");
            if (_leaveRequest != null)
                parts.Add("a Leave anyway");
            return string.Join(", ", parts);
        }

        /// <summary>Drops state stamped by an earlier campaign — the hooks reset it on every start and end already; this
        /// makes it impossible to act on a settlement or menu of a previous campaign whatever the game's order.</summary>
        private static void EnsureCurrent()
        {
            if (_generation == CampaignSession.Generation)
                return;
            string held = Describe();
            Reset();
            if (held.Length > 0)
                ModLog.Info("trigger", "state of an earlier campaign found and dropped: " + held);
        }

        public static void OnSettlementEntered(MobileParty? party, Settlement? settlement)
        {
            if (party == null || party != MobileParty.MainParty)
                return;
            EnsureCurrent();
            if (settlement == null || !(settlement.IsTown || settlement.IsVillage))
            {
                if (settlement != null)
                    ModLog.Info("trigger", "entered " + settlement.Name + " - not a town or a village");
                _visit = null;
                return;
            }
            _visit = new Visit(settlement) { Arrived = true };
            _quietFrames = 0;
            _waitFrames = 0;
            ModLog.Info("trigger", "arrived at " + settlement.Name);
        }

        public static void OnSettlementLeft(MobileParty? party, Settlement? settlement)
        {
            if (party == null || party != MobileParty.MainParty)
                return;
            EnsureCurrent();
            if (_visit != null)
                ModLog.Info("trigger", "left " + _visit.Settlement.Name + (_visit.Reviewed ? " (reviewed)" : ""));
            _visit = null;
            _openRequest = null;
            _leaveRequest = null;
        }

        /// <summary>Every menu (re)opening: guard its leave options (once), give War Sails' port menu its entry (once),
        /// and mark the arrival as due when this is the first town / village / port menu after coming in.</summary>
        public static void OnGameMenuOpened(MenuCallbackArgs args)
        {
            var menu = args?.MenuContext?.GameMenu;
            if (menu == null)
                return;
            EnsureCurrent();
            string id = menu.StringId;
            foreach (var (menuId, optionId) in LeaveOptions)
                if (menuId == id)
                    LeaveGuard.Wrap(menu, optionId);
            if (id == StewardMenu.PortMenuId)
                StewardMenu.EnsurePortEntry(menu);

            var visit = _visit;
            if (visit != null && visit.Arrived && !visit.ArrivalDone && !visit.ArrivalPending && IsArrivalMenu(id, visit.Settlement)
                && MobileParty.MainParty?.CurrentSettlement == visit.Settlement)
            {
                visit.ArrivalPending = true;
                _quietFrames = 0;
                _waitFrames = 0;
                ModLog.Info("trigger", "the " + id + " menu is up at " + visit.Settlement.Name
                                       + " - the arrival waits for a quiet map");
            }
        }

        /// <summary>The menus an arrival shows in: the town's and the village's own, and — docked at a town by sea under War
        /// Sails — the port's (RESEARCH §18). Once per visit whichever comes first: a town entered by land never shows the
        /// port first, and walking from the port into the town (or back) is the same visit.</summary>
        private static bool IsArrivalMenu(string? menuId, Settlement settlement) =>
            menuId == "town" || menuId == "village" || (menuId == StewardMenu.PortMenuId && settlement.IsTown);

        /// <summary>The window opened at <paramref name="settlement"/> — the player has seen the suggestions.</summary>
        public static void MarkReviewed(Settlement? settlement)
        {
            if (settlement == null)
                return;
            EnsureCurrent();
            if (_visit == null || _visit.Settlement != settlement)
                _visit = new Visit(settlement); // e.g. a campaign loaded inside the town: no arrival, but a visit
            _visit.Reviewed = true;
            _visit.ArrivalPending = false;       // the player opened it himself before the popup: nothing to pop
        }

        /// <summary>Vanilla asks "is this settlement busy?" before rolling an incident. While the leave warning is up
        /// (the click that did not leave), the answer for the "leaving" incidents is yes; the real leave ("Leave
        /// anyway" re-runs the menu option the vanilla way) rolls them as usual.</summary>
        public static void OnIsSettlementBusy(Settlement settlement, object asker, ref int priority)
        {
            if (_warnedThisFrame != null && settlement == _warnedThisFrame && asker != null
                && asker.GetType().Name == "IncidentsCampaignBehaviour")
                priority = Math.Max(priority, 1);
        }

        // ── the frame tick ──────────────────────────────────────────────────────────────────────────────

        /// <summary>Every application frame (SubModule). Cheap when nothing is pending.</summary>
        public static void Tick()
        {
            _warnedThisFrame = null;
            EnsureCurrent();
            if (_leaveRequest != null)
            {
                RunLeaveRequest();
                return;
            }
            var visit = _visit;
            bool arrival = visit != null && visit.ArrivalPending && !visit.ArrivalDone;
            if (!arrival && _openRequest == null)
            {
                _quietFrames = 0;
                _waitFrames = 0;
                return;
            }
            try
            {
                var settlement = _openRequest ?? visit!.Settlement;
                string? blocker = QuietBlocker(settlement);
                if (blocker != null)
                {
                    _quietFrames = 0;
                    if (++_waitFrames == WaitReportFrames)
                        ModLog.Info("trigger", "still waiting for a quiet map at " + settlement.Name + ": " + blocker);
                    return;
                }
                if (++_quietFrames < QuietFramesNeeded)
                    return;
                _quietFrames = 0;
                _waitFrames = 0;

                if (_openRequest != null)
                {
                    var target = _openRequest;
                    _openRequest = null;
                    ModLog.Info("trigger", "review requested at " + target.Name + " - opening the window");
                    StewardWindow.Open(target, "Review on the leave question");
                    return;
                }
                visit!.ArrivalPending = false;
                visit.ArrivalDone = true;
                RunArrival(visit.Settlement);
            }
            catch (Exception ex)
            {
                ModLog.Error("trigger", "the arrival / review tick", ex);
                if (visit != null)
                {
                    visit.ArrivalPending = false;
                    visit.ArrivalDone = true;
                }
                _openRequest = null;
            }
        }

        /// <summary>Null when the map screen shows the town/village menu of <paramref name="settlement"/> (or War Sails'
        /// port menu there — an arrival by sea, or a Review asked at the port) and nothing else asks for the player: no inquiry,
        /// conversation, map incident (pending or open), encyclopedia, escape menu, army / town management, recruitment,
        /// options, cheats, marriage or heir popup — and our own window is closed. Otherwise what holds it up (for the
        /// log).</summary>
        private static string? QuietBlocker(Settlement settlement)
        {
            if (Campaign.Current == null)
                return "no campaign";
            if (StewardWindow.IsOpen)
                return "the window is open";
            if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState))
                return "not on the campaign map (" + (Game.Current?.GameStateManager?.ActiveState?.GetType().Name ?? "no state") + ")";
            if (!mapState.AtMenu)
                return "no menu up";
            if (mapState.NextIncident != null)
                return "a map incident is pending";
            string? menuId = mapState.MenuContext?.GameMenu?.StringId;
            if (!IsArrivalMenu(menuId, settlement))
                return "the menu is " + (menuId ?? "none");
            if (MobileParty.MainParty?.CurrentSettlement != settlement)
                return "the party is at " + (MobileParty.MainParty?.CurrentSettlement?.Name?.ToString() ?? "no settlement");
            if (InformationManager.IsAnyInquiryActive())
                return "an inquiry is up";
            var conversations = Campaign.Current.ConversationManager;
            if (conversations != null && (conversations.IsConversationFlowActive || conversations.IsConversationInProgress))
                return "a conversation is running";
            if (!(ScreenManager.TopScreen is MapScreen map))
                return "the top screen is " + (ScreenManager.TopScreen?.GetType().Name ?? "none");
            if (!map.IsReady || !map.IsInMenu)
                return "the map screen is not ready";
            if (map.IsEscapeMenuOpened || map.IsInBattleSimulation || map.IsInTownManagement || map.IsInHideoutTroopManage
                || map.IsInArmyManagement || map.IsInRecruitment || map.IsInCampaignOptions
                || map.IsMarriageOfferPopupActive || map.IsMapCheatsActive || map.IsMapIncidentActive
                || map.IsHeirSelectionPopupActive)
                return "a map panel or popup is open";
            var encyclopedia = map.EncyclopediaScreenManager;
            return encyclopedia != null && encyclopedia.IsEncyclopediaOpen ? "the encyclopedia is open" : null;
        }

        // ── arrival ─────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Once per visit, on a quiet map: the Full-autonomous steward acts, or the window pops up (the
        /// town/village switch, and only with suggestions when PopupOnlyWithChanges).</summary>
        private static void RunArrival(Settlement settlement)
        {
            SettingsHost.ReloadIfChanged();
            var settings = SettingsHost.Current;
            if (!settings.ModEnabled)
                return;
            if (IsLootedVillage(settlement))
            {
                ModLog.Info("trigger", settlement.Name + " is looted - nothing to do");
                return;
            }
            if (settings.AutonomousSteward)
            {
                AutonomousRun.Run(settlement);
                return;
            }
            bool popup = settlement.IsTown ? settings.AutoPopupOnTownEnter : settings.AutoPopupOnVillageEnter;
            if (!popup)
                return;
            ModLog.Info("trigger", "arrival popup at " + settlement.Name
                + (settings.PopupOnlyWithChanges ? " (only with suggestions)" : ""));
            StewardWindow.Open(settlement, "arrival popup", asPopup: true, onlyWithChanges: settings.PopupOnlyWithChanges,
                quiet: true);
        }

        private static bool IsLootedVillage(Settlement settlement) =>
            settlement.IsVillage && settlement.Village?.VillageState == Village.VillageStates.Looted;

        // ── leave warning ──────────────────────────────────────────────────────────────────────────────

        /// <summary>The guard asks: should leaving now show the warning? Yes only when the mod and the warning are on,
        /// the steward is not autonomous, the window was never opened this visit, and the steward — planning afresh
        /// right now — has something to suggest. Any failure answers no: our bug must never block a leave.</summary>
        internal static bool ShouldWarnOnLeave(out Settlement? settlement)
        {
            settlement = null;
            try
            {
                EnsureCurrent();
                var here = MobileParty.MainParty?.CurrentSettlement;
                if (here == null || !(here.IsTown || here.IsVillage) || IsLootedVillage(here))
                    return false;
                SettingsHost.ReloadIfChanged();
                var settings = SettingsHost.Current;
                if (!settings.ModEnabled || !settings.WarnIfNotReviewed || settings.AutonomousSteward)
                    return false;
                if (_visit != null && _visit.Settlement == here && _visit.Reviewed)
                    return false;
                var visit = SnapshotBuilder.Build(here, out string whyNot);
                if (visit == null)
                {
                    ModLog.Info("leave", "leaving " + here.Name + ": no plan - " + whyNot);
                    return false;
                }
                var plan = StewardPlanner.Plan(visit.Snapshot, settings, visit.Oracle);
                int rows = plan.Rows.Count(r => r.HasChange);
                ModLog.Info("leave", "leaving " + here.Name + " unreviewed: " + (rows > 0
                    ? rows + " suggestion rows (gold " + plan.Totals.GoldNow + " -> " + plan.Totals.GoldAfter + ") - asking"
                    : "nothing to suggest"));
                settlement = here;
                return rows > 0;
            }
            catch (Exception ex)
            {
                ModLog.Error("leave", "checking the steward's suggestions before leaving", ex);
                return false;
            }
        }

        /// <summary>"Your steward has suggestions you haven't looked at." — Review opens the window, Leave anyway
        /// carries out the leave the player clicked (the vanilla way, next frame).</summary>
        internal static void ShowLeaveWarning(Settlement settlement, LeaveGuard guard)
        {
            _warnedThisFrame = settlement;
            InformationManager.ShowInquiry(new InquiryData(
                UiText.S("ss_leave_title", "Party Steward"),
                UiText.S("ss_leave_text", "Your steward has suggestions you haven't looked at."),
                true, true,
                UiText.S("ss_leave_review", "Review"),
                UiText.S("ss_leave_anyway", "Leave anyway"),
                () => OnReview(settlement),
                () => OnLeaveAnyway(guard)), pauseGameActiveState: true);
        }

        private static void OnReview(Settlement settlement)
        {
            ModLog.Info("leave", "the player chose Review");
            _leaveRequest = null;
            _openRequest = settlement;
            _quietFrames = 0;
        }

        private static void OnLeaveAnyway(LeaveGuard guard)
        {
            ModLog.Info("leave", "the player chose Leave anyway");
            _openRequest = null;
            _leaveRequest = guard;
            _leaveRequestFrames = LeaveRequestFrames;
        }

        /// <summary>Carries out a "Leave anyway" once the inquiry is gone and the same menu still shows; dropped when
        /// the menu changed or it waited too long.</summary>
        private static void RunLeaveRequest()
        {
            var guard = _leaveRequest!;
            try
            {
                if (--_leaveRequestFrames <= 0)
                {
                    _leaveRequest = null;
                    ModLog.Info("leave", "Leave anyway dropped - the menu never came back");
                    return;
                }
                if (InformationManager.IsAnyInquiryActive())
                    return;
                if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState) || !mapState.AtMenu)
                    return;
                var context = mapState.MenuContext;
                if (context?.GameMenu?.StringId != guard.MenuId)
                {
                    _leaveRequest = null;
                    ModLog.Info("leave", "Leave anyway dropped - the menu is " + (context?.GameMenu?.StringId ?? "gone"));
                    return;
                }
                _leaveRequest = null;
                guard.LeaveNow(context);
            }
            catch (Exception ex)
            {
                _leaveRequest = null;
                ModLog.Error("leave", "carrying out Leave anyway", ex);
            }
        }
    }

    /// <summary>
    /// One wrapped leave option (RESEARCH §10): <c>GameMenuOption.OnConsequence</c> is a public field, so the original
    /// delegate is kept and ours takes its place — no Harmony. Each option object is wrapped once (a session's menus
    /// are rebuilt on load, so a new session wraps its new options). Leaving with an unreviewed plan shows the warning
    /// instead; "Leave anyway" re-runs the option through vanilla's own path (<c>RunConsequencesOfMenuOption</c>) with
    /// the guard stepping aside, so everything vanilla does on a leave click — the attribute handlers, the
    /// GameMenuOptionSelected event (incidents, quests, tutorials) — happens exactly once, at the real leave.
    /// </summary>
    internal sealed class LeaveGuard
    {
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<GameMenuOption, LeaveGuard> Wrapped =
            new System.Runtime.CompilerServices.ConditionalWeakTable<GameMenuOption, LeaveGuard>();

        /// <summary>True while "Leave anyway" re-runs the option: every guard lets it through.</summary>
        private static bool _bypass;

        private readonly GameMenuOption _option;
        private readonly GameMenuOption.OnConsequenceDelegate? _original;

        /// <summary>The campaign this option was wrapped in: a guard of an earlier one only ever passes through.</summary>
        private readonly int _generation;

        private bool _saidStale;

        private LeaveGuard(string menuId, GameMenuOption option)
        {
            MenuId = menuId;
            _option = option;
            _original = option.OnConsequence;
            _generation = CampaignSession.Generation;
        }

        public string MenuId { get; }

        public string OptionId => _option.IdString;

        public static void Reset() => _bypass = false;

        /// <summary>Wraps the option <paramref name="optionId"/> of <paramref name="menu"/> — once per option object;
        /// a menu without it (no War Sails, a mod removed it) is skipped.</summary>
        public static void Wrap(GameMenu menu, string optionId)
        {
            try
            {
                var option = menu.MenuOptions?.FirstOrDefault(o => o.IdString == optionId);
                if (option == null || Wrapped.TryGetValue(option, out _))
                    return;
                var guard = new LeaveGuard(menu.StringId, option);
                option.OnConsequence = guard.Consequence;
                Wrapped.Add(option, guard);
                ModLog.Info("leave", "guarding " + menu.StringId + "/" + optionId);
            }
            catch (Exception ex)
            {
                ModLog.Error("leave", "wrapping " + menu.StringId + "/" + optionId, ex);
            }
        }

        /// <summary>The option's new consequence. Vanilla's own consequence runs untouched (its exceptions are
        /// vanilla's); only the steward's check is guarded.</summary>
        private void Consequence(MenuCallbackArgs args)
        {
            if (_generation != CampaignSession.Generation)
            {
                if (!_saidStale)
                {
                    _saidStale = true;
                    ModLog.Info("leave", MenuId + "/" + OptionId + " was wrapped in an earlier campaign - passing straight through");
                }
                _original?.Invoke(args);
                return;
            }
            if (!_bypass && StewardTriggers.ShouldWarnOnLeave(out var settlement) && settlement != null)
            {
                try
                {
                    StewardTriggers.ShowLeaveWarning(settlement, this);
                    return;
                }
                catch (Exception ex)
                {
                    ModLog.Error("leave", "showing the leave warning - leaving as clicked", ex);
                }
            }
            _original?.Invoke(args);
        }

        /// <summary>"Leave anyway": the click again, the vanilla way, with every guard standing aside.</summary>
        public void LeaveNow(MenuContext context)
        {
            var menu = context.GameMenu;
            int index = menu.MenuOptions.ToList().IndexOf(_option);
            _bypass = true;
            try
            {
                if (index >= 0)
                {
                    ModLog.Info("leave", "leaving through " + MenuId + "/" + OptionId);
                    Campaign.Current.GameMenuManager.RunConsequencesOfMenuOption(context, index);
                }
                else
                {
                    ModLog.Info("leave", MenuId + "/" + OptionId + " is no longer in the menu - calling its consequence");
                    _original?.Invoke(new MenuCallbackArgs(context, _option.Text));
                }
            }
            finally
            {
                _bypass = false;
            }
        }
    }
}
