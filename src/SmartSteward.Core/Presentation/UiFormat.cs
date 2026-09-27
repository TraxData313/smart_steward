using System;
using System.Globalization;
using System.Text;

namespace SmartSteward.Core.Presentation
{
    /// <summary>
    /// How the Party Steward window writes numbers (PLAN step 7) — pure, so it is tested and the window never
    /// formats by hand. Only glyphs the game's UI fonts have are used: FiraSansExtraCondensed and Galahad (checked in
    /// their .fnt files, RESEARCH §14) carry – — × · » ~ but NOT → − ≈ ⟲ ▸, so the minus is an en dash, the arrow a
    /// guillemet. Words never appear here: the window wraps these numbers in TextObjects.
    /// </summary>
    public static class UiFormat
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The minus sign (en dash — the fonts have no U+2212).</summary>
        public const string Minus = "–";

        /// <summary>Between the two ends of a price range: <c>180–240</c>.</summary>
        public const string RangeDash = "–";

        public const string Times = "×";
        public const string Dot = "·";

        /// <summary>"Becomes": <c>12,400 » 10,930</c> (the fonts have no →).</summary>
        public const string Arrow = "»";

        /// <summary>An empty cell: the market column of a loot row, a price with no base.</summary>
        public const string None = "—";

        /// <summary><c>12,400</c>; negatives with the en-dash minus.</summary>
        public static string Money(long value) =>
            value < 0 ? Minus + (-value).ToString("#,0", Inv) : value.ToString("#,0", Inv);

        /// <summary><c>+1,470</c>, <c>–1,470</c>, <c>0</c>.</summary>
        public static string SignedMoney(long value) => value > 0 ? "+" + Money(value) : Money(value);

        /// <summary>A row's Change: <c>+7</c>, <c>–5</c>, <c>0</c>.</summary>
        public static string SignedCount(int value) => SignedMoney(value);

        /// <summary><c>11</c> or <c>180–240</c>.</summary>
        public static string Range(int min, int max) =>
            min == max ? Money(min) : Money(Math.Min(min, max)) + RangeDash + Money(Math.Max(min, max));

        /// <summary>The Price cell of a row that moves units: <c>3 × 180–240 = –630</c> (a purchase costs, a sale
        /// earns); empty when nothing moves.</summary>
        public static string PriceCell(int units, int min, int max, int gold)
        {
            if (units <= 0)
                return "";
            return Money(units) + " " + Times + " " + Range(min, max) + " = " + SignedMoney(gold);
        }

        /// <summary>A weight change in kg, one decimal at most: <c>–120.5</c>, <c>+3</c>, <c>0</c>.</summary>
        public static string SignedWeight(double kg)
        {
            double rounded = Math.Round(kg, 1, MidpointRounding.AwayFromZero);
            if (rounded == 0)
                return "0";
            string text = Math.Abs(rounded).ToString("#,0.#", Inv);
            return (rounded > 0 ? "+" : Minus) + text;
        }

        /// <summary>Influence gained: <c>+2.4</c>.</summary>
        public static string SignedInfluence(double value)
        {
            double rounded = Math.Round(value, 1, MidpointRounding.AwayFromZero);
            return (rounded >= 0 ? "+" : Minus) + Math.Abs(rounded).ToString("0.#", Inv);
        }

        /// <summary>Whole days the food lasts (rounded down, as the game's own party screen does); "—" when the
        /// party eats nothing.</summary>
        public static string Days(double? days) =>
            days == null ? None : Math.Floor(Math.Max(0, days.Value)).ToString("#,0", Inv);

        /// <summary>A multiplier or a decimal setting as the player reads and types it: <c>1.2</c>, <c>0.85</c>,
        /// <c>2</c> — no trailing zeros, a point for the decimal.</summary>
        public static string Decimal(double value, int decimals = 2) =>
            Math.Round(value, decimals, MidpointRounding.AwayFromZero).ToString("0." + new string('#', Math.Max(1, decimals)), Inv);

        /// <summary>A price-book base typed by the player: empty = no value (the placeholder shows); digits only,
        /// thousands separators (<c>, . ' space</c>) allowed. False for anything else (the box keeps the text, nothing
        /// is saved).</summary>
        public static bool TryParseBase(string? text, out int? value)
        {
            value = null;
            var digits = Digits(text, allowMinus: false, out bool negative, out bool empty);
            if (empty)
                return true;
            if (digits == null || negative)
                return false;
            value = (int)Math.Min(int.MaxValue, digits.Value);
            return true;
        }

        /// <summary>A whole-number setting typed by the player (a leading minus allowed: <c>-1</c>). False when
        /// empty or not a number.</summary>
        public static bool TryParseWhole(string? text, out long value)
        {
            value = 0;
            var digits = Digits(text, allowMinus: true, out bool negative, out bool empty);
            if (empty || digits == null)
                return false;
            value = negative ? -digits.Value : digits.Value;
            return true;
        }

        /// <summary>A decimal setting typed by the player: <c>1.2</c> or <c>1,2</c> (either separator, once).
        /// False when empty or not a number.</summary>
        public static bool TryParseDecimal(string? text, out double value)
        {
            value = 0;
            if (text == null)
                return false;
            string t = text.Trim().Replace(',', '.');
            if (t.Length == 0 || t.IndexOf('.') != t.LastIndexOf('.'))
                return false;
            foreach (char c in t)
                if (!(c >= '0' && c <= '9') && c != '.')
                    return false;
            if (t == ".")
                return false;
            return double.TryParse(t, NumberStyles.AllowDecimalPoint, Inv, out value);
        }

        /// <summary>The digits of a typed whole number (separators dropped, capped at 10^12); null when anything but
        /// digits, separators and one leading minus is in it.</summary>
        private static long? Digits(string? text, bool allowMinus, out bool negative, out bool empty)
        {
            negative = false;
            string t = (text ?? "").Trim();
            empty = t.Length == 0;
            if (empty)
                return null;
            if (t[0] == '-' || t[0] == '–')
            {
                if (!allowMinus)
                {
                    negative = true;
                    return null;
                }
                negative = true;
                t = t.Substring(1);
            }
            var sb = new StringBuilder();
            foreach (char c in t)
            {
                if (c >= '0' && c <= '9')
                    sb.Append(c);
                else if (c == ',' || c == '.' || c == '\'' || c == ' ' || c == ' ')
                    continue;
                else
                    return null;
            }
            if (sb.Length == 0)
                return null;
            string s = sb.ToString().TrimStart('0');
            if (s.Length == 0)
                return 0;
            if (s.Length > 12)
                return 1_000_000_000_000L;
            return long.Parse(s, Inv);
        }
    }
}
