using System;
using System.Linq;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>What one "Do" button carries out (PLAN step 27, DESIGN §1.1 "Do just this part"; every line since step 29).</summary>
    public enum PlanPartKind
    {
        /// <summary>The Troops section: every tavern hire, every recruit and every dismissal.</summary>
        Troops,
        Food,

        /// <summary>The Horses section: pack, riding, war, noble and lame horse rows.</summary>
        Horses,
        Prisoners,

        /// <summary>The Other section: the loot groups and the Other goods.</summary>
        Other,

        /// <summary>The Lords line: the captured lords moved (ransomed / donated).</summary>
        Lords,

        /// <summary>The Others line: every prisoner who is not a lord.</summary>
        OtherPrisoners,

        /// <summary>The Recruits line: only the recruits (a troop row's + side).</summary>
        Recruits,

        /// <summary>The Your troops line: only the dismissals (a troop row's − side).</summary>
        YourTroops,

        /// <summary>One tavern row: a wanderer, or the mercenaries (<see cref="PlanPart.RowId"/>).</summary>
        TavernRow,

        /// <summary>The Other goods line (wool, salt, pottery…).</summary>
        OtherGoods,

        /// <summary>Step 29: one plan row, every transaction of it — a food, a horse role, a loot group, a prisoner type.</summary>
        Row,

        /// <summary>Step 29: one troop type under Recruits — only its recruits.</summary>
        RecruitRow,

        /// <summary>Step 29: one troop type under Your troops — only its dismissals.</summary>
        DismissRow,

        /// <summary>Step 29: one breakdown line of a row (a horse breed under its role, one good under Other goods) — that row's
        /// trades of that stack (<see cref="PlanPart.StackKey"/>).</summary>
        StackLine,
    }

    /// <summary>
    /// A part of the plan that runs on its own (PLAN step 27 — Anton 2026.10.01: "i dont want to do the full steward but want to
    /// ransom my prisoners, so add that button where I can do specific deals separately"): a section's title line, or one of the
    /// lines that is a deal of its own (Lords, Others, Recruits, Your troops, each tavern row, Other goods). Since step 29 (Anton
    /// 2026.10.01: "can I have that Do button next to every line too, so that say I want to just update one specific food") every
    /// line is a part too: one row (<see cref="Row"/>), one troop type's side (<see cref="RecruitRow"/> / <see cref="DismissRow"/>),
    /// one breakdown line (<see cref="StackLine"/>). Which transactions belong to it: <see cref="Includes"/>; the deal it makes
    /// alone: <see cref="StewardPlan.DealOf"/>.
    /// </summary>
    public sealed class PlanPart : IEquatable<PlanPart>
    {
        private PlanPart(PlanPartKind kind, string? rowId = null, string? stackKey = null)
        {
            Kind = kind;
            RowId = rowId;
            StackKey = stackKey;
        }

        public static readonly PlanPart Troops = new PlanPart(PlanPartKind.Troops);
        public static readonly PlanPart Food = new PlanPart(PlanPartKind.Food);
        public static readonly PlanPart Horses = new PlanPart(PlanPartKind.Horses);
        public static readonly PlanPart Prisoners = new PlanPart(PlanPartKind.Prisoners);
        public static readonly PlanPart Other = new PlanPart(PlanPartKind.Other);
        public static readonly PlanPart Lords = new PlanPart(PlanPartKind.Lords);
        public static readonly PlanPart OtherPrisoners = new PlanPart(PlanPartKind.OtherPrisoners);
        public static readonly PlanPart Recruits = new PlanPart(PlanPartKind.Recruits);
        public static readonly PlanPart YourTroops = new PlanPart(PlanPartKind.YourTroops);
        public static readonly PlanPart OtherGoods = new PlanPart(PlanPartKind.OtherGoods);

        /// <summary>One tavern row (a wanderer, the mercenaries) by its row id.</summary>
        public static PlanPart TavernRow(string rowId) =>
            new PlanPart(PlanPartKind.TavernRow, rowId ?? throw new ArgumentNullException(nameof(rowId)));

        /// <summary>Step 29: one plan row — every transaction of it.</summary>
        public static PlanPart Row(string rowId) =>
            new PlanPart(PlanPartKind.Row, rowId ?? throw new ArgumentNullException(nameof(rowId)));

        /// <summary>Step 29: one troop row's recruits (the row under Recruits).</summary>
        public static PlanPart RecruitRow(string rowId) =>
            new PlanPart(PlanPartKind.RecruitRow, rowId ?? throw new ArgumentNullException(nameof(rowId)));

        /// <summary>Step 29: one troop row's dismissals (the row under Your troops).</summary>
        public static PlanPart DismissRow(string rowId) =>
            new PlanPart(PlanPartKind.DismissRow, rowId ?? throw new ArgumentNullException(nameof(rowId)));

        /// <summary>Step 29: one breakdown line — the trades of <paramref name="rowId"/> in the stack <paramref name="stackKey"/>.</summary>
        public static PlanPart StackLine(string rowId, string stackKey) =>
            new PlanPart(PlanPartKind.StackLine, rowId ?? throw new ArgumentNullException(nameof(rowId)),
                stackKey ?? throw new ArgumentNullException(nameof(stackKey)));

        public PlanPartKind Kind { get; }

        /// <summary>The row of a one-row part (a tavern row, <see cref="PlanPartKind.Row"/>, a troop side, a breakdown line); null
        /// for a section or a line of many rows.</summary>
        public string? RowId { get; }

        /// <summary>The stack of a <see cref="PlanPartKind.StackLine"/>; null otherwise.</summary>
        public string? StackKey { get; }

        /// <summary>For the log: <c>Food</c>, <c>TavernRow:tavern:w1</c>, <c>Row:food:grain</c>, <c>StackLine:mounts:riding/hunter|</c>.</summary>
        public string Id => RowId == null ? Kind.ToString() : Kind + ":" + RowId + (StackKey == null ? "" : "/" + StackKey);

        /// <summary>A section's title line (not one of its lines).</summary>
        public bool IsSection => Kind <= PlanPartKind.Other;

        /// <summary>A part of ONE row (step 29's lines and the tavern rows) — its hover names the line.</summary>
        public bool IsSingleRow => RowId != null;

        /// <summary>Does this transaction of <paramref name="row"/> belong to the part? A troop row is split by side: its recruits
        /// belong to Recruits, its dismissals to Your troops; a breakdown line takes only its own stack.</summary>
        public bool Includes(PlanRow row, PlanTransaction tx)
        {
            if (row == null || tx == null) return false;
            switch (Kind)
            {
                case PlanPartKind.Recruits:
                    return row.Type == RowType.Troop && tx.Kind == TransactionKind.Recruit;
                case PlanPartKind.YourTroops:
                    return row.Type == RowType.Troop && tx.Kind == TransactionKind.Dismiss;
                case PlanPartKind.RecruitRow:
                    return IsMine(row) && tx.Kind == TransactionKind.Recruit;
                case PlanPartKind.DismissRow:
                    return IsMine(row) && tx.Kind == TransactionKind.Dismiss;
                case PlanPartKind.StackLine:
                    return IsMine(row) && string.Equals(tx.StackKey, StackKey, StringComparison.Ordinal);
                default:
                    return Covers(row);
            }
        }

        /// <summary>
        /// The player's edit on <paramref name="row"/> that is still to do after this part ran — what a re-plan after it puts back
        /// (<see cref="PlanCarryOver.Capture(StewardPlan, PlanPart)"/>): 0 when the part carried the edit out, the row's whole
        /// change when the part does not touch the row. A troop row by the side its change is on; a breakdown line leaves the
        /// row's other lines (the row's change less the line's).
        /// </summary>
        public int EditLeft(PlanRow row)
        {
            if (row == null) return 0;
            switch (Kind)
            {
                case PlanPartKind.Recruits:
                    return row.Type == RowType.Troop && row.Change > 0 ? 0 : row.Change;
                case PlanPartKind.YourTroops:
                    return row.Type == RowType.Troop && row.Change < 0 ? 0 : row.Change;
                case PlanPartKind.RecruitRow:
                    return IsMine(row) && row.Change > 0 ? 0 : row.Change;
                case PlanPartKind.DismissRow:
                    return IsMine(row) && row.Change < 0 ? 0 : row.Change;
                case PlanPartKind.StackLine:
                    if (!IsMine(row)) return row.Change;
                    var line = row.Breakdown.FirstOrDefault(l => string.Equals(l.StackKey, StackKey, StringComparison.Ordinal));
                    return row.Change - (line?.Change ?? 0);
                default:
                    return Covers(row) ? 0 : row.Change;
            }
        }

        /// <summary>Is the player's edit on <paramref name="row"/> carried out by this part, all of it?</summary>
        public bool CoversEdit(PlanRow row) => row != null && row.Change != 0 && EditLeft(row) == 0;

        private bool IsMine(PlanRow row) => string.Equals(row.Id, RowId, StringComparison.Ordinal);

        private bool Covers(PlanRow row)
        {
            switch (Kind)
            {
                case PlanPartKind.Troops:
                    return row.Type == RowType.Tavern || row.Type == RowType.Troop;
                case PlanPartKind.Food:
                    return row.Type == RowType.Food;
                case PlanPartKind.Horses:
                    return row.Section == PlanSectionKind.Mounts;
                case PlanPartKind.Prisoners:
                    return row.Type == RowType.Prisoner;
                case PlanPartKind.Other:
                    return row.Type == RowType.Loot;
                case PlanPartKind.Lords:
                    return row.Type == RowType.Prisoner && (row.Prisoner?.IsHero ?? false);
                case PlanPartKind.OtherPrisoners:
                    return row.Type == RowType.Prisoner && !(row.Prisoner?.IsHero ?? false);
                case PlanPartKind.TavernRow:
                    return row.Type == RowType.Tavern && IsMine(row);
                case PlanPartKind.OtherGoods:
                    return row.Type == RowType.Loot && row.LootGroup == LootGroup.OtherGoods;
                case PlanPartKind.Row:
                    return IsMine(row);
                default:
                    return false;
            }
        }

        public bool Equals(PlanPart? other) =>
            other != null && other.Kind == Kind && string.Equals(other.RowId, RowId, StringComparison.Ordinal)
            && string.Equals(other.StackKey, StackKey, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as PlanPart);

        public override int GetHashCode() =>
            ((int)Kind * 397) ^ (RowId == null ? 0 : StringComparer.Ordinal.GetHashCode(RowId))
            ^ (StackKey == null ? 0 : StringComparer.Ordinal.GetHashCode(StackKey) * 31);

        public override string ToString() => Id;
    }
}
