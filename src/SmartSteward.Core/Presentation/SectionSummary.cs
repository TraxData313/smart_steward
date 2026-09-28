using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Pricing;

namespace SmartSteward.Core.Presentation
{
    /// <summary>
    /// The Suggestion tab's foldable sections (PLAN step 18 — Anton 2026.09.28: "each section header expands/collapses;
    /// collapsed = ONE summary line"): one per job, in DESIGN §1.1's order. The troops section's two halves ("Recruits on
    /// offer", "Your troops") fold together as <see cref="Troops"/>. The names are also the keys of the remembered state
    /// (<see cref="WindowState"/>) — never rename one.
    /// </summary>
    public enum SectionGroup
    {
        Tavern,
        Troops,
        Food,
        Mounts,
        ArmourAndWeapons,
        Prisoners,
    }

    public static class SectionGroups
    {
        /// <summary>Every group, in the table's order.</summary>
        public static IReadOnlyList<SectionGroup> All { get; } = new[]
        {
            SectionGroup.Tavern, SectionGroup.Troops, SectionGroup.Food, SectionGroup.Mounts, SectionGroup.ArmourAndWeapons,
            SectionGroup.Prisoners,
        };

        /// <summary>The group a plan section folds with.</summary>
        public static SectionGroup Of(PlanSectionKind kind)
        {
            switch (kind)
            {
                case PlanSectionKind.Tavern: return SectionGroup.Tavern;
                case PlanSectionKind.Recruits:
                case PlanSectionKind.Troops: return SectionGroup.Troops;
                case PlanSectionKind.Food: return SectionGroup.Food;
                case PlanSectionKind.Mounts: return SectionGroup.Mounts;
                case PlanSectionKind.ArmourAndWeapons: return SectionGroup.ArmourAndWeapons;
                default: return SectionGroup.Prisoners;
            }
        }

        /// <summary>The group's name in the state file (the enum name).</summary>
        public static string Key(SectionGroup group) => group.ToString();

        /// <summary>A name from the state file, case-insensitive; false for anything unknown.</summary>
        public static bool TryParse(string? key, out SectionGroup group)
        {
            foreach (var g in All)
            {
                if (string.Equals(Key(g), (key ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    group = g;
                    return true;
                }
            }
            group = SectionGroup.Tavern;
            return false;
        }
    }

    /// <summary>The words of the folded lines — English by default; the Module fills them from TextObjects (ids
    /// <c>ss_ui_sum_*</c>) so a translation replaces them. Numbers are formatted by <see cref="UiFormat"/>.</summary>
    public sealed class SummaryWords
    {
        public string Kind { get; set; } = "kind";
        public string Kinds { get; set; } = "kinds";
        public string Sold { get; set; } = "sold";
        public string Hired { get; set; } = "hired";
        public string Recruited { get; set; } = "recruited";
        public string Dismissed { get; set; } = "dismissed";
        public string Ransomed { get; set; } = "ransomed";
        public string ToDungeon { get; set; } = "to the dungeon";
        public string Influence { get; set; } = "influence";
        public string Days { get; set; } = "days";
        public string Kg { get; set; } = "kg";
        public string NoChange { get; set; } = "no change";
        public string NobodyHired { get; set; } = "nobody hired";
        public string NoTroopChange { get; set; } = "nobody recruited or dismissed";
        public string NothingSold { get; set; } = "nothing sold";
        public string NobodyRansomed { get; set; } = "nobody ransomed";
    }

    /// <summary>
    /// The ONE line a folded section shows (PLAN step 18, Anton 2026.09.28): what the section will do and its gold, read from
    /// the plan as it stands — so it follows every click. Nothing queued → a short neutral line. Examples:
    /// <list type="bullet">
    /// <item>Tavern — <c>+3 hired –1,450</c> / <c>nobody hired</c></item>
    /// <item>Troops — <c>+12 recruited –640 · 3 dismissed</c> / <c>nobody recruited or dismissed</c></item>
    /// <item>Food — <c>+29 (5 kinds), 12 sold –510 · 64 » 71 days</c> / <c>no change · 64 days</c></item>
    /// <item>Mounts — <c>+10 (2 kinds), 3 sold –1,200</c> / <c>no change</c></item>
    /// <item>Armour &amp; weapons — <c>41 sold +2,132 · –380 kg</c> / <c>nothing sold</c></item>
    /// <item>Prisoners — <c>12 ransomed, 4 to the dungeon +980 +3.2 influence</c> / <c>nobody ransomed</c></item>
    /// </list>
    /// The same shape as the autonomous steward's report (<see cref="Execution.AutonomousReport"/>): moves, then the gold.
    /// </summary>
    public static class SectionSummary
    {
        public static string Of(StewardPlan plan, SectionGroup group, SummaryWords? words = null)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            words ??= new SummaryWords();
            var rows = plan.Sections.Where(s => SectionGroups.Of(s.Kind) == group).SelectMany(s => s.Rows).ToList();
            switch (group)
            {
                case SectionGroup.Tavern: return Tavern(rows, words);
                case SectionGroup.Troops: return Troops(rows, words);
                case SectionGroup.Food: return Food(plan, rows, words);
                case SectionGroup.Mounts: return Mounts(rows, words);
                case SectionGroup.ArmourAndWeapons: return Loot(rows, words);
                default: return Prisoners(rows, words);
            }
        }

        private static string Tavern(List<PlanRow> rows, SummaryWords words)
        {
            int hired = rows.Sum(r => Math.Max(0, r.Change));
            if (hired == 0)
                return words.NobodyHired;
            return WithGold("+" + UiFormat.Money(hired) + " " + words.Hired, rows.Sum(r => r.GoldDelta));
        }

        private static string Troops(List<PlanRow> rows, SummaryWords words)
        {
            int recruited = rows.Sum(r => Math.Max(0, r.Change));
            int dismissed = rows.Sum(r => Math.Max(0, -r.Change));
            if (recruited == 0 && dismissed == 0)
                return words.NoTroopChange;
            var parts = new List<string>();
            if (recruited > 0)
                parts.Add(WithGold("+" + UiFormat.Money(recruited) + " " + words.Recruited, rows.Sum(r => r.GoldDelta)));
            if (dismissed > 0)
                parts.Add(UiFormat.Money(dismissed) + " " + words.Dismissed);
            return string.Join(Separator, parts);
        }

        private static string Food(StewardPlan plan, List<PlanRow> rows, SummaryWords words)
        {
            var t = plan.Totals;
            string moves = Moves(rows, words);
            string days = t.FoodDaysAfter == null ? ""
                : moves.Length == 0 || UiFormat.Days(t.FoodDaysNow) == UiFormat.Days(t.FoodDaysAfter)
                    ? UiFormat.Days(t.FoodDaysAfter) + " " + words.Days
                    : UiFormat.Days(t.FoodDaysNow) + " " + UiFormat.Arrow + " " + UiFormat.Days(t.FoodDaysAfter) + " " + words.Days;
            string head = moves.Length == 0 ? words.NoChange : WithGold(moves, rows.Sum(r => r.GoldDelta));
            return days.Length == 0 ? head : head + Separator + days;
        }

        private static string Mounts(List<PlanRow> rows, SummaryWords words)
        {
            string moves = Moves(rows, words);
            return moves.Length == 0 ? words.NoChange : WithGold(moves, rows.Sum(r => r.GoldDelta));
        }

        private static string Loot(List<PlanRow> rows, SummaryWords words)
        {
            int sold = rows.Sum(r => Math.Max(0, -r.Change));
            if (sold == 0)
                return words.NothingSold;
            string text = WithGold(UiFormat.Money(sold) + " " + words.Sold, rows.Sum(r => r.GoldDelta));
            double weight = rows.Sum(r => r.WeightDelta);
            if (UiFormat.SignedWeight(weight) != "0")
                text += Separator + UiFormat.SignedWeight(weight) + " " + words.Kg;
            return text;
        }

        private static string Prisoners(List<PlanRow> rows, SummaryWords words)
        {
            int ransomed = rows.Sum(r => r.Prisoner?.RansomCount ?? 0);
            int donated = rows.Sum(r => r.Prisoner?.DonateCount ?? 0);
            if (ransomed == 0 && donated == 0)
                return words.NobodyRansomed;
            var moves = new List<string>();
            if (ransomed > 0) moves.Add(UiFormat.Money(ransomed) + " " + words.Ransomed);
            if (donated > 0) moves.Add(UiFormat.Money(donated) + " " + words.ToDungeon);
            string text = WithGold(string.Join(", ", moves), rows.Sum(r => r.GoldDelta));
            double influence = rows.Sum(r => r.InfluenceDelta);
            if (influence >= 0.05)
                text += " " + UiFormat.SignedInfluence(influence) + " " + words.Influence;
            return text;
        }

        /// <summary>Item rows: <c>+29 (5 kinds), 12 sold</c> — units bought (and how many different items), units sold, from
        /// the rows' tallies; empty when nothing moves.</summary>
        private static string Moves(List<PlanRow> rows, SummaryWords words)
        {
            int bought = 0, sold = 0;
            var kinds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
                foreach (var tally in row.Tallies)
                {
                    if (tally.Count <= 0)
                        continue;
                    if (tally.Direction == TradeDirection.Buy)
                    {
                        bought += tally.Count;
                        kinds.Add(tally.Stack.ItemId);
                    }
                    else
                        sold += tally.Count;
                }
            var moves = new List<string>();
            if (bought > 0)
                moves.Add("+" + UiFormat.Money(bought) + " (" + UiFormat.Money(kinds.Count) + " "
                          + (kinds.Count == 1 ? words.Kind : words.Kinds) + ")");
            if (sold > 0)
                moves.Add(UiFormat.Money(sold) + " " + words.Sold);
            return string.Join(", ", moves);
        }

        /// <summary>Between the parts of a line: <c> · </c>.</summary>
        internal const string Separator = " " + UiFormat.Dot + " ";

        private static string WithGold(string text, int gold) => gold == 0 ? text : text + " " + UiFormat.SignedMoney(gold);
    }
}
