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

        /// <summary>The Other section (was "Armour &amp; weapons" until round 4): the loot groups and the other goods.</summary>
        Other,
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
        /// <summary>The Other section's job (round 4: armour &amp; weapons and the other goods).</summary>
        public string Other { get; set; } = "other";
        public string Prisoners { get; set; } = "prisoners";
        public string Kind { get; set; } = "kind";
        public string Kinds { get; set; } = "kinds";
        public string Sold { get; set; } = "sold";
        public string Ransomed { get; set; } = "ransomed";
        public string Donated { get; set; } = "donated";
        public string Influence { get; set; } = "influence";

        public string JobName(StewardJob job)
        {
            switch (job)
            {
                case StewardJob.Food: return Food;
                case StewardJob.Mounts: return Mounts;
                case StewardJob.Other: return Other;
                default: return Prisoners;
            }
        }
    }

    /// <summary>
    /// The Full-autonomous steward's report (DESIGN §6): after the deal, a short summary for the game's message log —
    /// since step 33 (Anton 2026.10.02: the old "Steward …" line read like the Steward SKILL had changed) the
    /// <see cref="RunSummary"/> line first, then one entry per job that did something:
    /// <c>Steward report at Sargot: 46 deals made · +2,240 denari · 12 prisoners ransomed</c> /
    /// <c>Steward report, by job: food +24 (5 kinds) –310 · mounts +3 –540 · other 41 sold +2,130 · prisoners 12 ransomed
    /// +980</c>; skipped or cut-short transactions end the first line (<c>· 2 skipped — see smart_steward.log</c>); nothing
    /// happened → no lines at all. The full detail goes to the log file (<see cref="ExecutionReport.LogLines"/>).
    /// </summary>
    public sealed class AutonomousReport
    {
        private AutonomousReport(List<JobResult> jobs, ExecutionReport report)
        {
            Jobs = jobs;
            Summary = RunSummary.From(report);
            GoldBefore = report.GoldBefore;
            GoldAfter = report.GoldAfter;
        }

        /// <summary>The run as one "Steward report:" line (step 33).</summary>
        public RunSummary Summary { get; }

        /// <summary>The jobs that did something, in job order.</summary>
        public IReadOnlyList<JobResult> Jobs { get; }

        public int GoldBefore { get; }
        public int GoldAfter { get; }

        /// <summary>Transactions done only in part.</summary>
        public int CutShort => Summary.CutShort;

        /// <summary>Transactions not done at all.</summary>
        public int Skipped => Summary.Skipped;

        /// <summary>The run never started (the party had left, …).</summary>
        public bool Aborted => Summary.Aborted;

        /// <summary>Nothing done and nothing went wrong — no message.</summary>
        public bool IsEmpty => Jobs.Count == 0 && Summary.Deals == 0 && !Summary.HasTrouble;

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
            return new AutonomousReport(jobs, report);
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
                    case PlanSectionKind.Other: return StewardJob.Other;
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
        /// The message-log lines (step 33): the <see cref="RunSummary"/> line opened by <paramref name="header"/> ("Steward report
        /// at Sargot:" — the trouble, if any, at its end), then the jobs line opened by <paramref name="jobsHeader"/> ("Steward
        /// report, by job:") when a job did something. Nothing happened → no lines.
        /// </summary>
        public IReadOnlyList<string> Lines(string header, string jobsHeader, ReportWords? words = null, SummaryWords? summaryWords = null)
        {
            words ??= new ReportWords();
            var lines = new List<string>();
            if (IsEmpty)
                return lines;
            lines.Add(Summary.Line(header, summaryWords));
            if (Jobs.Count > 0)
                lines.Add(jobsHeader + " " + string.Join(" " + UiFormat.Dot + " ", Jobs.Select(j => Describe(j, words))));
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
