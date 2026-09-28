using System;
using System.Globalization;
using SandBox.View.Map;
using TaleWorlds.CampaignSystem.GameState;
using TaleWorlds.Core;
using TaleWorlds.ScreenSystem;

namespace SmartSteward
{
    /// <summary>
    /// A log-only watch on the campaign map's "follow modifier" (playtest round 1, PLAN step 12). The map treats a click
    /// with <c>MapFollowModifier</c> held (Left Alt by default, a controller's LB) as a PARLEY request: on a hostile
    /// town it shows why no parley is possible ("Your clan tier is not high enough to request a meeting."), on any
    /// other town or village it does nothing — the party never travels (SandBox.View <c>SettlementVisual.OnMapClick</c>,
    /// <c>MapScreen.HandleLeftMouseButtonClick</c>; RESEARCH §17). A key the game still believes held — typically Left
    /// Alt after an Alt+Tab that came back by a mouse click — therefore looks exactly like "towns and villages can no
    /// longer be entered" until the key is tapped or the game restarts. The steward never touches input; this only
    /// writes to the log when the map has read the modifier as held for a while, and when it lets go, so the next
    /// report says at once whether that was it.
    /// </summary>
    internal static class InputWatch
    {
        /// <summary>Held this long on the open map (no menu up) before the log says so — a player holding Alt to read a
        /// tooltip lets go sooner.</summary>
        private const float ReportAfterSeconds = 5f;

        private const string FollowModifier = "MapFollowModifier";

        private static float _heldFor;
        private static bool _reported;
        private static bool _broken;

        public static void Reset()
        {
            _heldFor = 0f;
            _reported = false;
        }

        /// <summary>Every application frame (SubModule). Samples only while the campaign map itself takes clicks — the
        /// map screen on top, ready, no settlement menu — and pauses (keeps its count) otherwise.</summary>
        public static void Tick(float dt)
        {
            if (_broken)
                return;
            try
            {
                if (!(ScreenManager.TopScreen is MapScreen map) || !map.IsReady
                    || !(Game.Current?.GameStateManager?.ActiveState is MapState mapState) || mapState.AtMenu)
                    return;
                bool held = map.Input != null && map.Input.IsHotKeyDown(FollowModifier);
                if (!held)
                {
                    if (_reported)
                        ModLog.Info("input", "the map's follow modifier is released again (it read as held for "
                                             + Seconds(_heldFor) + " s)");
                    Reset();
                    return;
                }
                _heldFor += dt;
                if (!_reported && _heldFor >= ReportAfterSeconds)
                {
                    _reported = true;
                    ModLog.Info("input", "the map's follow modifier (MapFollowModifier, Left Alt by default) has read as held for "
                                         + Seconds(_heldFor) + " s - while it is held, a click on a town or village asks for a "
                                         + "parley instead of travelling there (no entering). A key stuck after Alt+Tab is freed "
                                         + "by one tap of Left Alt.");
                }
            }
            catch (Exception ex)
            {
                _broken = true; // a diagnostic must never cost a frame twice
                ModLog.Error("input", "watching the map's follow modifier - watch switched off", ex);
            }
        }

        private static string Seconds(float s) => s.ToString("0", CultureInfo.InvariantCulture);
    }
}
