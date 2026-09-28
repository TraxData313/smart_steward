using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Presentation;

namespace SmartSteward.Core.Execution
{
    /// <summary>The steward's jobs as the autonomous report names them, in DESIGN §1.1's section order (the tavern is
    /// never autonomous).</summary>
    public enum StewardJob
    {
        Food,
        Mounts,
        ArmourAndWeapons,
        Prisoners,
    }

    /// <summary>What one job really did in one autonomous run — summed from the executor's outcomes (real counts,
    /// real gold), not from the plan.</summary>
    public sealed class JobResult
    {
        internal JobResult(StewardJob job) => Job = job;

        public StewardJob Job { get; }

        /// <summary>Item units bought.</summary>
        public int Bought { get; internal set; }

        /// <summary>Different items bought (food: the variety).</summary>
        public int KindsBought { get; internal set; }

        /// <summary>Item units sold.</summary>
        public int Sold { get; internal set; }

        public int Ransomed { get; internal set; }
        public int Donated { get; internal set; }

        /// <summary>Net gold: earned (sales, ransom) minus spent (purchases).</summary>
        public int Gold { get; internal set; }

        public double Influence { get; internal set; }

        public bool DidSomething => Bought + Sold + Ransomed + Donated > 0;
    }

    /// <summary>The words of the report — English by default; the Module fills them from TextObjects (ids
    /// <c>ss_auto_*</c>) so a translation replaces them. Numbers are formatted here (<see cref="UiFormat"/>: en-dash
    /// minus, » arrow — the glyphs the game's fonts carry).</summary>
    public sealed class ReportWords
    {
        public string Food { get; set; } = "food";
        public string Mounts { get; set; } = "mounts";
        public string ArmourAndWeapons { get; set; } = "armour & weapons";
        public string Prisoners { get; set; } = "prisoners";
        public string Kind { get; set; } = "kind";
        public string Kinds { get; set; } = "kinds";
        public string Sold { get; set; } = "sold";
        public string Ransomed { get; set; } = "ransomed";
        public string Donated { get; set; } = "donated";
        public string Influence { get; set; } = "influence";
        public string Gold { get; set; } = "gold";
        public string CutShort { get; set; } = "cut short";
        public string Skipped { get; set; } = "skipped";
        public string NothingDone { get; set; } = "nothing was done";
        public string SeeLog { get; set; } = "see smart_steward.log";

        public string JobName(StewardJob job)
        {
            switch (job)
            {
                case StewardJob.Food: return Food;
                case StewardJob.Mounts: return Mounts;
                case StewardJob.ArmourAndWeapons: return ArmourAndWeapons;
                default: return Prisoners;
            }
        }
    }

    /// <summary>
    /// The Full-autonomous steward's report (DESIGN §6): after the deal, a short summary for the game's message log —
    /// one entry per job that did something, then the purse:
    /// <c>Steward at Sargot: food +24 (5 kinds) –310 · mounts +3 –540 · armour &amp; weapons 41 sold +2,130 · prisoners
    /// 12 ransomed +980 · gold 312,400 » 314,660</c>; skipped or cut-short transactions get a line of their own
    /// (<c>Steward: 2 skipped — see smart_steward.log</c>); nothing happened → no lines at all. The full detail goes to
    /// the log file (<see cref="ExecutionReport.LogLines"/>).
    /// </summary>
    public sealed class AutonomousReport
    {
        private AutonomousReport(List<JobResult> jobs, int goldBefore, int goldAfter, int cutShort, int skipped, bool aborted)
        {
            Jobs = jobs;
            GoldBefore = goldBefore;
            GoldAfter = goldAfter;
            CutShort = cutShort;
            Skipped = skipped;
            Aborted = aborted;
        }

        /// <summary>The jobs that did something, in job order.</summary>
        public IReadOnlyList<JobResult> Jobs { get; }

        public int GoldBefore { get; }
        public int GoldAfter { get; }

        /// <summary>Transactions done only in part.</summary>
        public int CutShort { get; }

        /// <summary>Transactions not done at all.</summary>
        public int Skipped { get; }

        /// <summary>The run never started (the party had left, …).</summary>
        public bool Aborted { get; }

        /// <summary>Nothing done and nothing went wrong — no message.</summary>
        public bool IsEmpty => Jobs.Count == 0 && CutShort + Skipped == 0 && !Aborted;

        /// <summary>Sums the executor's outcomes per job; a transaction's job is its row's section in the plan (the
        /// kind decides when the row is unknown).</summary>
        public static AutonomousReport From(StewardPlan plan, ExecutionReport report)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (report == null) throw new ArgumentNullException(nameof(report));

            var results = new Dictionary<StewardJob, JobResult>();
            var kinds = new Dictionary<StewardJob, HashSet<string>>();
            foreach (var o in report.Outcomes)
            {
                if (o.Done <= 0)
                    continue;
                var t = o.Transaction;
                var job = JobOf(plan, t);
                if (job == null)
                    continue; // a hire — never autonomous; the gold line still tells the truth
                if (!results.TryGetValue(job.Value, out var r))
                {
                    r = new JobResult(job.Value);
                    results[job.Value] = r;
                    kinds[job.Value] = new HashSet<string>(StringComparer.Ordinal);
                }
                switch (t.Kind)
                {
                    case TransactionKind.Buy:
                        r.Bought += o.Done;
                        r.Gold -= o.Gold;
                        kinds[job.Value].Add(t.ItemId ?? t.StackKey ?? t.RowId);
                        break;
                    case TransactionKind.Sell:
                        r.Sold += o.Done;
                        r.Gold += o.Gold;
                        break;
                    case TransactionKind.Ransom:
                        r.Ransomed += o.Done;
                        r.Gold += o.Gold;
                        break;
                    case TransactionKind.Donate:
                        r.Donated += o.Done;
                        r.Influence += o.Influence;
                        break;
                }
                r.KindsBought = kinds[job.Value].Count;
            }

            var jobs = results.Values.Where(r => r.DidSomething).OrderBy(r => r.Job).ToList();
            return new AutonomousReport(jobs, report.GoldBefore, report.GoldAfter, report.CutShort, report.NotDone,
                report.Abort != null);
        }

        private static StewardJob? JobOf(StewardPlan plan, PlanTransaction t)
        {
            var row = plan.FindRow(t.RowId);
            if (row != null)
            {
                switch (row.Section)
                {
                    case PlanSectionKind.Food: return StewardJob.Food;
                    case PlanSectionKind.Mounts: return StewardJob.Mounts;
                    case PlanSectionKind.ArmourAndWeapons: return StewardJob.ArmourAndWeapons;
                    case PlanSectionKind.Prisoners: return StewardJob.Prisoners;
                    case PlanSectionKind.Tavern:
                    case PlanSectionKind.Recruits:
                    case PlanSectionKind.Troops: return null; // the player's hand only — never in an autonomous plan
                }
            }
            switch (t.Kind)
            {
                case TransactionKind.Ransom:
                case TransactionKind.Donate:
                    return StewardJob.Prisoners;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The message-log lines: the jobs line (only when a job did something) and the trouble line (only when
        /// something was skipped, cut short or the run aborted). <paramref name="header"/> opens the jobs line
        /// ("Steward at Sargot:"), <paramref name="shortHeader"/> the trouble line ("Steward:").
        /// </summary>
        public IReadOnlyList<string> Lines(string header, string shortHeader, ReportWords? words = null)
        {
            words ??= new ReportWords();
            var lines = new List<string>();
            if (Jobs.Count > 0)
            {
                var parts = Jobs.Select(j => Describe(j, words)).ToList();
                parts.Add(words.Gold + " " + UiFormat.Money(GoldBefore) + " " + UiFormat.Arrow + " " + UiFormat.Money(GoldAfter));
                lines.Add(header + " " + string.Join(" " + UiFormat.Dot + " ", parts));
            }
            if (Aborted)
            {
                lines.Add(shortHeader + " " + words.NothingDone + " " + UiFormat.None + " " + words.SeeLog);
            }
            else if (CutShort + Skipped > 0)
            {
                var trouble = new List<string>();
                if (CutShort > 0) trouble.Add(UiFormat.Money(CutShort) + " " + words.CutShort);
                if (Skipped > 0) trouble.Add(UiFormat.Money(Skipped) + " " + words.Skipped);
                lines.Add(shortHeader + " " + string.Join(", ", trouble) + " " + UiFormat.None + " " + words.SeeLog);
            }
            return lines;
        }

        /// <summary><c>food +24 (5 kinds) –310</c>, <c>mounts +3, 2 sold –340</c>, <c>armour &amp; weapons 41 sold +2,130</c>,
        /// <c>prisoners 12 ransomed, 4 donated +980 +3.2 influence</c>.</summary>
        internal static string Describe(JobResult j, ReportWords words)
        {
            var moves = new List<string>();
            if (j.Bought > 0)
            {
                string bought = "+" + UiFormat.Money(j.Bought);
                if (j.Job == StewardJob.Food)
                    bought += " (" + UiFormat.Money(j.KindsBought) + " " + (j.KindsBought == 1 ? words.Kind : words.Kinds) + ")";
                moves.Add(bought);
            }
            if (j.Sold > 0) moves.Add(UiFormat.Money(j.Sold) + " " + words.Sold);
            if (j.Ransomed > 0) moves.Add(UiFormat.Money(j.Ransomed) + " " + words.Ransomed);
            if (j.Donated > 0) moves.Add(UiFormat.Money(j.Donated) + " " + words.Donated);

            string text = words.JobName(j.Job) + " " + string.Join(", ", moves);
            if (j.Gold != 0)
                text += " " + UiFormat.SignedMoney(j.Gold);
            if (j.Influence >= 0.05)
                text += " " + UiFormat.SignedInfluence(j.Influence) + " " + words.Influence;
            return text;
        }
    }
}
