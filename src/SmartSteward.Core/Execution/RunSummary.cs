using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;

namespace SmartSteward.Core.Execution
{
    /// <summary>The words of the "Steward report:" line — English by default; the Module fills them from TextObjects
    /// (ids <c>ss_report_*</c>) so a translation replaces them. A <c>Func</c> takes the number, formatted here (<see cref="UiFormat"/>).</summary>
    public sealed class SummaryWords
    {
        public string DealsOne { get; set; } = "1 deal made";
        public Func<string, string> DealsMany { get; set; } = n => n + " deals made";
        public string DealsNone { get; set; } = "no deals made";

        /// <summary>The number signed: <c>+3,120 denari</c>, <c>–540 denari</c>.</summary>
        public Func<string, string> Denari { get; set; } = n => n + " denari";

        /// <summary>The number signed, one decimal: <c>+2.4 influence</c>.</summary>
        public Func<string, string> Influence { get; set; } = n => n + " influence";

        public string RansomedOne { get; set; } = "1 prisoner ransomed";
        public Func<string, string> RansomedMany { get; set; } = n => n + " prisoners ransomed";
        public string DonatedOne { get; set; } = "1 prisoner donated";
        public Func<string, string> DonatedMany { get; set; } = n => n + " prisoners donated";
        public Func<string, string> CutShort { get; set; } = n => n + " cut short";
        public Func<string, string> Skipped { get; set; } = n => n + " skipped";
        public string NothingDone { get; set; } = "nothing was done";
        public string SeeLog { get; set; } = "see smart_steward.log";
    }

    /// <summary>
    /// One run of the executor as the player reads it in the game's message log (PLAN step 33, Anton 2026.10.02: "the line
    /// in the battle log 'Steward: 44 of 44...' is confusing, like that something changed with my Steward skill, make it
    /// 'Steward report: deals made, gold influence change, prisoners ransomed/donated'"):
    /// <c>Steward report: 44 deals made · +3,120 denari · +2.4 influence · 9 prisoners ransomed</c> — the deals that moved
    /// anything, the purse's real change, the donations' influence, the prisoners ransomed and donated (each only when not
    /// zero), then the trouble (<c>2 cut short, 1 skipped — see smart_steward.log</c>). The same line for Deal all, a part's
    /// Deal (its head names the part) and the Full-autonomous steward (its head names the place). Counts are the
    /// executor's real ones, not the plan's.
    /// </summary>
    public sealed class RunSummary
    {
        private RunSummary(ExecutionReport report)
        {
            var moved = report.Outcomes.Where(o => o.Done > 0).ToList();
            Deals = moved.Count;
            Denari = report.GoldAfter - report.GoldBefore;
            Influence = moved.Where(o => o.Transaction.Kind == TransactionKind.Donate).Sum(o => o.Influence);
            Ransomed = moved.Where(o => o.Transaction.Kind == TransactionKind.Ransom).Sum(o => o.Done);
            Donated = moved.Where(o => o.Transaction.Kind == TransactionKind.Donate).Sum(o => o.Done);
            CutShort = report.CutShort;
            Skipped = report.NotDone;
            Aborted = report.Abort != null;
        }

        /// <summary>Transactions that moved anything (done in full or in part).</summary>
        public int Deals { get; }

        /// <summary>The purse after minus before — what really changed.</summary>
        public int Denari { get; }

        /// <summary>The donations' influence.</summary>
        public double Influence { get; }

        /// <summary>Prisoners (men) ransomed.</summary>
        public int Ransomed { get; }

        /// <summary>Prisoners (men) donated to the dungeon.</summary>
        public int Donated { get; }

        public int CutShort { get; }
        public int Skipped { get; }

        /// <summary>The run never started.</summary>
        public bool Aborted { get; }

        public bool HasTrouble => Aborted || CutShort + Skipped > 0;

        public static RunSummary From(ExecutionReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            return new RunSummary(report);
        }

        /// <summary>The line: <paramref name="head"/> ("Steward report:", "Steward report (Prisoners):", "Steward report at
        /// Sargot:") and the summary joined by <c> · </c>.</summary>
        public string Line(string head, SummaryWords? words = null)
        {
            words ??= new SummaryWords();
            string seeLog = " " + UiFormat.None + " " + words.SeeLog;
            if (Aborted)
                return head + " " + words.NothingDone + seeLog;

            var parts = new List<string>
            {
                Deals == 0 ? words.DealsNone : Deals == 1 ? words.DealsOne : words.DealsMany(UiFormat.Money(Deals)),
            };
            if (Denari != 0) parts.Add(words.Denari(UiFormat.SignedMoney(Denari)));
            if (Math.Abs(Influence) >= 0.05) parts.Add(words.Influence(UiFormat.SignedInfluence(Influence)));
            if (Ransomed > 0) parts.Add(Ransomed == 1 ? words.RansomedOne : words.RansomedMany(UiFormat.Money(Ransomed)));
            if (Donated > 0) parts.Add(Donated == 1 ? words.DonatedOne : words.DonatedMany(UiFormat.Money(Donated)));
            if (CutShort + Skipped > 0)
            {
                var trouble = new List<string>();
                if (CutShort > 0) trouble.Add(words.CutShort(UiFormat.Money(CutShort)));
                if (Skipped > 0) trouble.Add(words.Skipped(UiFormat.Money(Skipped)));
                parts.Add(string.Join(", ", trouble) + seeLog);
            }
            return head + " " + string.Join(" " + UiFormat.Dot + " ", parts);
        }
    }
}
