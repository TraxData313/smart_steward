using System;
using SmartSteward.UI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace SmartSteward
{
    /// <summary>
    /// The steward stands aside while the player's encounter is anything but a quiet settlement visit (PLAN step 24,
    /// RESEARCH §28): a battle or a MapEvent of the party or of the settlement, a hostile action being started
    /// (ForceRaid / ForceSupplies / ForceVolunteers), an encounter past its Begin state, a siege, captivity — or, where
    /// asked, a menu that is not the settlement's own (town, village, War Sails' port). Every place the steward acts asks
    /// here first: the arrival popup and the autonomous run, the leave warning and its "Leave anyway", the menu entry and
    /// the executor (Do it and autonomous). Read-only: it never touches the encounter or the menu.
    /// <para>The raid-capture report (2026.09.29) was vanilla's own bug — see <see cref="IsRaidSideFlipped"/> — but the
    /// guard makes sure the steward can never be part of such a chain, and its log line makes a repeat visible.</para>
    /// </summary>
    internal static class EncounterGuard
    {
        /// <summary>The settlement menus the steward lives in.</summary>
        private static readonly string[] SettlementMenus = { "town", "village", StewardMenu.PortMenuId };

        private static string? _lastLogged;

        /// <summary>Null when the steward may act at <paramref name="settlement"/> (null = any settlement the party is
        /// in); otherwise why not, for the log. <paramref name="checkMenu"/>: the current menu must be the settlement's
        /// own. A failing check answers "busy" — standing aside is the safe side.</summary>
        public static string? WhyBusy(Settlement? settlement, bool checkMenu)
        {
            try
            {
                if (Campaign.Current == null)
                    return "no campaign";
                var main = MobileParty.MainParty;
                var hero = Hero.MainHero;
                if (main == null || hero == null)
                    return "no main party";
                if (hero.IsPrisoner || hero.PartyBelongedToAsPrisoner != null)
                    return "the player is a prisoner";
                var mapEvent = main.MapEvent;
                if (mapEvent != null)
                    return "the party is in a " + mapEvent.EventType + " (map event " + mapEvent.State + ")";
                if (main.BesiegerCamp != null || main.SiegeEvent != null)
                    return "the party is in a siege";
                var encounter = PlayerEncounter.Current;
                if (encounter != null)
                {
                    var battle = PlayerEncounter.Battle;
                    if (battle != null)
                        return "the encounter has a " + battle.EventType + " (map event " + battle.State + ")";
                    if (encounter.ForceRaid || encounter.ForceSupplies || encounter.ForceVolunteers)
                        return "a hostile action is starting";
                    if (encounter.EncounterState != PlayerEncounterState.Begin)
                        return "the encounter is in state " + encounter.EncounterState;
                }
                if (settlement != null)
                {
                    if (main.CurrentSettlement != settlement)
                        return "the party is not in " + settlement.Name;
                    if (settlement.Party?.MapEvent != null)
                        return settlement.Name + " has a battle (" + settlement.Party.MapEvent.EventType + ")";
                    if (settlement.IsUnderRaid)
                        return settlement.Name + " is being raided";
                    if (settlement.IsUnderSiege)
                        return settlement.Name + " is under siege";
                }
                if (checkMenu)
                {
                    string? menu = CurrentMenuId();
                    if (menu == null || Array.IndexOf(SettlementMenus, menu) < 0)
                        return "the menu is " + (menu ?? "none");
                }
                return null;
            }
            catch (Exception ex)
            {
                return "the encounter check failed (" + ex.GetType().Name + ": " + ex.Message + ")";
            }
        }

        /// <summary>The menu on the campaign map right now (null: no menu, or not on the map).</summary>
        public static string? CurrentMenuId()
        {
            if (!(Game.Current?.GameStateManager?.ActiveState is MapState mapState) || !mapState.AtMenu)
                return null;
            return mapState.MenuContext?.GameMenu?.StringId;
        }

        /// <summary>One log line per stand-aside — the same one repeated (a frame tick asking again) is written once.</summary>
        public static void LogAside(string what, string why)
        {
            string line = what + ": the steward stands aside - " + why;
            if (line == _lastLogged)
                return;
            _lastLogged = line;
            ModLog.Info("guard", line);
        }

        /// <summary>The encounter is quiet again: the next stand-aside is logged even when it repeats the last.</summary>
        public static void Quiet() => _lastLogged = null;

        /// <summary>
        /// Vanilla's raid bug (RESEARCH §28): backing out of a village hostile action ("Leave..." in the encounter menu,
        /// <c>MenuHelper.EncounterLeaveConsequence</c>) finishes the encounter and starts a new one while the party is still
        /// INSIDE the village, so <c>PlayerEncounter.SetupFields</c> makes the player the village's DEFENDER. The next
        /// hostile action from that encounter makes a raid with the player as attacker, but a won battle declares
        /// <c>PlayerEncounter.PlayerSide</c> — the defender — the winner: DefenderVictory, the player captured. The state
        /// is saved with the game. True when the party stands in a village of another faction with that flipped side and
        /// no battle yet — leaving to the map and entering again sets it right.
        /// </summary>
        public static bool IsRaidSideFlipped(Settlement? settlement)
        {
            try
            {
                var main = MobileParty.MainParty;
                var encounter = Campaign.Current == null ? null : PlayerEncounter.Current;
                return settlement != null && settlement.IsVillage && main != null && encounter != null
                       && main.CurrentSettlement == settlement && PlayerEncounter.Battle == null && main.MapEvent == null
                       && encounter.PlayerSide == BattleSideEnum.Defender
                       && settlement.MapFaction != null && settlement.MapFaction != main.MapFaction;
            }
            catch (Exception ex)
            {
                ModLog.Error("guard", "checking the encounter's side", ex);
                return false;
            }
        }

        /// <summary>Says so once in the message log (and the log file) — the player can still leave and come back.</summary>
        public static void WarnRaidSideFlipped(Settlement settlement)
        {
            ModLog.Info("guard", "at " + settlement.Name + " the game's encounter has the player as the village's DEFENDER"
                + " (vanilla, after backing out of a hostile action) - a raid won here would count as lost; warned the player");
            InformationManager.DisplayMessage(new InformationMessage(UiText.S1("ss_guard_raid_side",
                "Smart Steward: the game would count a raid on {SETTLEMENT} as lost (a game bug after backing out of a hostile action). Leave the village and enter again before you raid it.",
                "SETTLEMENT", settlement.Name?.ToString() ?? ""), Colors.Red));
        }
    }
}
