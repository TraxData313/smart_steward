using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SmartSteward.Core.Planning;

namespace SmartSteward.Core.Execution
{
    /// <summary>What the executor did with ONE planned transaction: the units done at their real prices, and why
    /// it stopped short when it did.</summary>
    public sealed class TransactionOutcome
    {
        private readonly List<int> _unitPrices = new List<int>();

        public TransactionOutcome(PlanTransaction transaction)
        {
            Transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        }

        public PlanTransaction Transaction { get; }

        /// <summary>Units (men, heroes) actually moved.</summary>
        public int Done { get; private set; }

        /// <summary>The real price of each unit done, in order (item trades, hires, ransom per man).</summary>
        public IReadOnlyList<int> UnitPrices => _unitPrices;

        /// <summary>Gold that really changed hands for the units done — always positive, like
        /// <see cref="PlanTransaction.Gold"/>. Set from the game's purse where it pays in one go (ransom).</summary>
        public int Gold { get; private set; }

        /// <summary>Influence expected for the donations done (the game grants it through its own event).</summary>
        public double Influence =>
            Transaction.Count > 0 ? Transaction.Influence * Done / Transaction.Count : 0;

        public int Skipped => Math.Max(0, Transaction.Count - Done);

        /// <summary>Why fewer than planned were done (None when all were).</summary>
        public SkipReason Reason { get; private set; }

        /// <summary>Free text for the log (an exception message, the price found, the reason in the game's words).</summary>
        public string? Detail { get; private set; }

        /// <summary>
        /// Real minus expected gold over the units done (item trades: the plan's walk vs the game's; hires: the
        /// live price vs the planned one). 0 = the simulation was exact.
        /// </summary>
        public int Drift
        {
            get
            {
                int expected = 0;
                var planned = Transaction.UnitPrices;
                for (int i = 0; i < Done && i < planned.Count; i++)
                    expected += planned[i];
                return Gold - expected;
            }
        }

        public void AddUnit(int price)
        {
            _unitPrices.Add(price);
            Done++;
            Gold += price;
        }

        /// <summary>Several units paid in one go (a ransom, a mercenary band): the total is what the purse moved.</summary>
        public void AddUnits(int count, int totalGold)
        {
            if (count <= 0) return;
            int each = count > 0 ? totalGold / count : 0;
            for (int i = 0; i < count; i++)
                _unitPrices.Add(each);
            Done += count;
            Gold += totalGold;
        }

        /// <summary>Records why the transaction stopped; the first reason given sticks.</summary>
        public void Stop(SkipReason reason, string? detail = null)
        {
            if (Reason != SkipReason.None || reason == SkipReason.None)
                return;
            Reason = reason;
            Detail = detail;
        }

        /// <summary>Everything undone (the trade batch was reset): no units, no gold.</summary>
        public void RollBack(string? detail = null)
        {
            if (Done == 0 && Reason != SkipReason.None)
                return;
            _unitPrices.Clear();
            Done = 0;
            Gold = 0;
            Reason = SkipReason.RolledBack;
            Detail = detail;
        }
    }

    /// <summary>One run of the executor: the gold before and after, and every transaction's outcome — written to
    /// smart_steward.log line by line, and summed up in one line for the player.</summary>
    public sealed class ExecutionReport
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public ExecutionReport(int goldBefore)
        {
            GoldBefore = goldBefore;
            GoldAfter = goldBefore;
        }

        public int GoldBefore { get; }
        public int GoldAfter { get; set; }
        public List<TransactionOutcome> Outcomes { get; } = new List<TransactionOutcome>();

        /// <summary>A whole-run problem (not in the settlement any more, no plan…); null when none.</summary>
        public string? Abort { get; set; }

        public TransactionOutcome Add(PlanTransaction transaction)
        {
            var outcome = new TransactionOutcome(transaction);
            Outcomes.Add(outcome);
            return outcome;
        }

        public int Planned => Outcomes.Count;
        public int FullyDone => Outcomes.Count(o => o.Done >= o.Transaction.Count);
        public int CutShort => Outcomes.Count(o => o.Done > 0 && o.Done < o.Transaction.Count);
        public int NotDone => Outcomes.Count(o => o.Done == 0 && o.Transaction.Count > 0);

        /// <summary>The player's one line: "Steward: 14 of 16 done, 1 cut short, 1 skipped. Gold 12,400 -> 10,930."</summary>
        public string Summary()
        {
            var parts = new List<string> { FullyDone.ToString(Inv) + " of " + Planned.ToString(Inv) + " done" };
            if (CutShort > 0) parts.Add(CutShort.ToString(Inv) + " cut short");
            if (NotDone > 0) parts.Add(NotDone.ToString(Inv) + " skipped");
            string text = "Steward: " + string.Join(", ", parts) + ". Gold " + Money(GoldBefore) + " -> " + Money(GoldAfter) + ".";
            if (Abort != null) text = "Steward: nothing done - " + Abort;
            else if (CutShort + NotDone > 0) text += " Details in smart_steward.log.";
            return text;
        }

        /// <summary>One log line per transaction, then the gold line.</summary>
        public IEnumerable<string> LogLines()
        {
            if (Abort != null)
                yield return "ABORTED: " + Abort;
            foreach (var o in Outcomes)
                yield return Describe(o);
            yield return "gold " + Money(GoldBefore) + " -> " + Money(GoldAfter)
                         + " (" + Signed(GoldAfter - GoldBefore) + "); " + FullyDone.ToString(Inv) + " of "
                         + Planned.ToString(Inv) + " done, " + CutShort.ToString(Inv) + " cut short, "
                         + NotDone.ToString(Inv) + " skipped";
        }

        /// <summary>"Sell grain x12: done 12, +144 gold (plan +144)" / "Buy hunter x3: done 2 of 3, -420 gold
        /// (plan -420 for those) - stopped: NotEnoughGold".</summary>
        public static string Describe(TransactionOutcome o)
        {
            var t = o.Transaction;
            string what = t.Kind + " " + Subject(t) + " x" + t.Count.ToString(Inv) + " [" + t.RowId + "]";
            string done = o.Done >= t.Count ? "done " + o.Done.ToString(Inv) : "done " + o.Done.ToString(Inv) + " of " + t.Count.ToString(Inv);
            string money;
            switch (t.Kind)
            {
                case TransactionKind.Donate:
                    money = "+" + o.Influence.ToString("0.##", Inv) + " influence";
                    break;
                case TransactionKind.Ransom:
                case TransactionKind.Sell:
                    money = "+" + Money(o.Gold) + " gold";
                    break;
                default:
                    money = "-" + Money(o.Gold) + " gold";
                    break;
            }
            string line = what + ": " + done + ", " + money;
            if (t.Kind != TransactionKind.Donate && o.Done > 0)
            {
                int drift = o.Drift;
                line += drift == 0 ? " (as planned)" : " (planned " + Money(o.Gold - drift) + ", drift " + Signed(drift) + ")";
            }
            if (t.IsItemTrade && o.UnitPrices.Count > 0)
                line += " unit prices " + Range(o.UnitPrices);
            if (o.Reason != SkipReason.None)
                line += " - " + (o.Reason == SkipReason.RolledBack ? "rolled back" : "stopped: " + o.Reason)
                        + (string.IsNullOrEmpty(o.Detail) ? "" : " (" + o.Detail + ")");
            return line;
        }

        private static string Subject(PlanTransaction t)
        {
            if (t.IsItemTrade)
                return string.IsNullOrEmpty(t.ModifierId) ? t.ItemId ?? "?" : t.ItemId + "/" + t.ModifierId;
            if (t.Kind == TransactionKind.HireWanderer)
                return t.HeroId ?? "?";
            return t.TroopId ?? "?";
        }

        private static string Range(IReadOnlyList<int> prices)
        {
            int min = prices.Min(), max = prices.Max();
            return min == max ? min.ToString(Inv) : min.ToString(Inv) + ".." + max.ToString(Inv);
        }

        internal static string Money(int value) => value.ToString("N0", Inv);

        internal static string Signed(int value) => (value >= 0 ? "+" : "-") + Math.Abs(value).ToString("N0", Inv);
    }
}
