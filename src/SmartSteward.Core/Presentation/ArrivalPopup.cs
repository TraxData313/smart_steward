using SmartSteward.Core.Planning;

namespace SmartSteward.Core.Presentation
{
    /// <summary>Why the arrival popup opens or stays shut (DESIGN §6).</summary>
    public enum PopupVerdict
    {
        /// <summary>The window pops up.</summary>
        Open,

        /// <summary>The game does not let the player trade here (war, crime, a raid, a village with nothing on offer):
        /// whatever else the plan holds, a popup at a closed market is noise — the menu entry still opens the window.</summary>
        MarketClosed,

        /// <summary>The plan has no row at all.</summary>
        NothingPlanned,

        /// <summary>PopupOnlyWithChanges and no row moves anything.</summary>
        NoChanges,
    }

    /// <summary>
    /// The arrival popup's rule (DESIGN §6, [decided: Claude, 2026.09.28 — step 12, playtest round 1]): it opens only
    /// when the market is open AND the plan has rows AND — with PopupOnlyWithChanges — at least one row moves something.
    /// The "Party Steward" menu entry is not a popup and always opens.
    /// </summary>
    public static class ArrivalPopup
    {
        public static PopupVerdict Decide(bool marketOpen, bool hasRows, bool hasChanges, bool onlyWithChanges)
        {
            if (!marketOpen)
                return PopupVerdict.MarketClosed;
            if (!hasRows)
                return PopupVerdict.NothingPlanned;
            if (onlyWithChanges && !hasChanges)
                return PopupVerdict.NoChanges;
            return PopupVerdict.Open;
        }

        public static PopupVerdict Decide(StewardPlan plan, bool marketOpen, bool onlyWithChanges)
        {
            bool hasRows = false;
            foreach (var _ in plan.Rows)
            {
                hasRows = true;
                break;
            }
            return Decide(marketOpen, hasRows, plan.HasChanges, onlyWithChanges);
        }

        /// <summary>The verdict for the log: "the market is closed", "nothing planned", "nothing to suggest".</summary>
        public static string Describe(PopupVerdict verdict)
        {
            switch (verdict)
            {
                case PopupVerdict.MarketClosed: return "the market is closed";
                case PopupVerdict.NothingPlanned: return "nothing planned";
                case PopupVerdict.NoChanges: return "nothing to suggest";
                default: return "opens";
            }
        }
    }
}
