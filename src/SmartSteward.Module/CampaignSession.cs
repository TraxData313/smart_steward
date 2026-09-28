using System;
using SmartSteward.UI;

namespace SmartSteward
{
    /// <summary>
    /// The campaign the steward's in-memory state belongs to (playtest round 1, PLAN step 12). Every campaign start and
    /// end — a new game, a save loaded from the main menu or from inside a running campaign, the exit to the main menu —
    /// bumps <see cref="Generation"/> and drops everything the steward holds: the window, the visit and its pending
    /// popup / Review / Leave anyway, the input watch. The holders also stamp what they make with the generation and
    /// refuse anything stamped by an earlier one, so no object of a previous campaign (a settlement, a menu option, a
    /// map screen) can ever be used, whatever order the game calls the hooks in. The mod stores nothing in the save,
    /// so this is all there is to reset.
    /// </summary>
    internal static class CampaignSession
    {
        /// <summary>Changes on every campaign start and end.</summary>
        public static int Generation { get; private set; }

        /// <summary>A campaign starts (SubModule.OnGameStart with a campaign starter).</summary>
        public static void Begin() => Bump("campaign start");

        /// <summary>A game ends (SubModule.OnGameEnd).</summary>
        public static void End() => Bump("game end");

        private static void Bump(string when)
        {
            Generation++;
            string held = "";
            try
            {
                held = StewardTriggers.Describe();
                if (StewardWindow.IsOpen)
                    held = (held.Length > 0 ? held + ", " : "") + "the window";
            }
            catch
            {
                // the description is a courtesy
            }
            Reset(StewardWindow.Close, "closing the window");
            Reset(StewardTriggers.Reset, "resetting the triggers");
            Reset(InputWatch.Reset, "resetting the input watch");
            ModLog.Info("campaign", when + ": steward state reset (session " + Generation + ")"
                                    + (held.Length > 0 ? " - dropped " + held : " - nothing was held"));
        }

        private static void Reset(Action reset, string what)
        {
            try
            {
                reset();
            }
            catch (Exception ex)
            {
                ModLog.Error("campaign", what, ex);
            }
        }
    }
}
