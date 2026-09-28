using SmartSteward.Core.Planning;

namespace SmartSteward.Core.Presentation
{
    /// <summary>
    /// The window's colours as Gauntlet reads them (<c>#RRGGBBAA</c>, bound to <c>Brush.FontColor</c>). DESIGN §1.1:
    /// colour for direction — buy / hire green-ish, sell / ransom red-ish, untouched grey; the header line green when
    /// the deal earns, red when it costs; floor breaches red.
    /// </summary>
    public static class UiColors
    {
        /// <summary>Normal text (the popup description colour).</summary>
        public const string Text = "#F1E9DAFF";

        /// <summary>Buying or hiring: something comes in.</summary>
        public const string Buy = "#9FD27FFF";

        /// <summary>Selling, ransoming, donating: something goes out.</summary>
        public const string Sell = "#E8906EFF";

        /// <summary>Untouched rows, column heads, small print.</summary>
        public const string Muted = "#8E8A80FF";

        /// <summary>A grey placeholder in an empty price box (the average price).</summary>
        public const string Placeholder = "#6E695FFF";

        /// <summary>Warnings: a money floor breached, a deal the purse cannot pay.</summary>
        public const string Warning = "#E5574AFF";

        /// <summary>Section and group headings (the popup title's gold).</summary>
        public const string Heading = "#E4C59BFF";

        /// <summary>A clickable name (opens the Encyclopedia).</summary>
        public const string Link = "#F2C35CFF";

        /// <summary>A goal the player typed (round 5): gold, beside its ⟲ — his standing order, not the steward's.</summary>
        public const string Yours = "#F2C35CFF";

        /// <summary>By the sign of a row's change: + green, − red, 0 grey.</summary>
        public static string ForChange(int change) => change > 0 ? Buy : change < 0 ? Sell : Muted;

        /// <summary>The header's gold line: green when the deal earns, red when it costs, grey when even.</summary>
        public static string ForGoldChange(int goldChange) => goldChange > 0 ? Buy : goldChange < 0 ? Warning : Muted;

        /// <summary>Colours by meaning (round 4, mockup choice 6 — Anton 2026.09.28: "green for positive change, red for
        /// negative/bad"): denari (and influence) coming in green, going out red; nothing grey.</summary>
        public static string ForMoney(long denari) => denari > 0 ? Buy : denari < 0 ? Sell : Muted;

        /// <summary>A number past a limit (the party over its size limit, a load over its capacity, a slowdown): red; else plain.</summary>
        public static string ForLimit(bool over) => over ? Warning : Text;

        /// <summary>A row: its direction's colour, or grey when it moves nothing.</summary>
        public static string ForRow(PlanRow row) => ForChange(row?.Change ?? 0);
    }

    /// <summary>The game's modifier keys → how far one click goes (DESIGN §1.1): Ctrl = all, Shift = 5, else 1.
    /// Ctrl wins when both are held (as in the inventory).</summary>
    public static class UiInput
    {
        public static EditSize EditSizeFor(bool fiveStackHeld, bool entireStackHeld) =>
            entireStackHeld ? EditSize.All : fiveStackHeld ? EditSize.Five : EditSize.One;
    }
}
