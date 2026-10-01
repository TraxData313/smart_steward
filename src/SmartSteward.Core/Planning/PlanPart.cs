using System;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>What one "Do" button carries out (PLAN step 27, DESIGN §1.1 "Do just this part").</summary>
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
    }

    /// <summary>
    /// A part of the plan that runs on its own (PLAN step 27 — Anton 2026.10.01: "i dont want to do the full steward but want to
    /// ransom my prisoners, so add that button where I can do specific deals separately"): a section's title line, or one of the
    /// lines that is a deal of its own (Lords, Others, Recruits, Your troops, each tavern row, Other goods). Item-level rows (one
    /// food, one troop type) are no part — the table stays glanceable. Which transactions belong to it:
    /// <see cref="Includes"/>; the deal it makes alone: <see cref="StewardPlan.DealOf"/>.
    /// </summary>
    public sealed class PlanPart : IEquatable<PlanPart>
    {
        private PlanPart(PlanPartKind kind, string? rowId = null)
        {
            Kind = kind;
            RowId = rowId;
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

        public PlanPartKind Kind { get; }

        /// <summary>The tavern row (<see cref="PlanPartKind.TavernRow"/>); null otherwise.</summary>
        public string? RowId { get; }

        /// <summary>For the log: <c>Food</c>, <c>TavernRow:tavern:w1</c>.</summary>
        public string Id => RowId == null ? Kind.ToString() : Kind + ":" + RowId;

        /// <summary>A section's title line (not one of its lines).</summary>
        public bool IsSection => Kind <= PlanPartKind.Other;

        /// <summary>Does this transaction of <paramref name="row"/> belong to the part? A troop row is split by side: its recruits
        /// belong to Recruits, its dismissals to Your troops.</summary>
        public bool Includes(PlanRow row, TransactionKind kind)
        {
            if (row == null) return false;
            switch (Kind)
            {
                case PlanPartKind.Recruits:
                    return row.Type == RowType.Troop && kind == TransactionKind.Recruit;
                case PlanPartKind.YourTroops:
                    return row.Type == RowType.Troop && kind == TransactionKind.Dismiss;
                default:
                    return Covers(row);
            }
        }

        /// <summary>
        /// Is the player's edit on <paramref name="row"/> carried out by this part — so a re-plan after it must NOT put the edit back
        /// (<see cref="PlanCarryOver.Capture(StewardPlan, PlanPart)"/>)? A troop row by the side its change is on.
        /// </summary>
        public bool CoversEdit(PlanRow row)
        {
            if (row == null) return false;
            switch (Kind)
            {
                case PlanPartKind.Recruits:
                    return row.Type == RowType.Troop && row.Change > 0;
                case PlanPartKind.YourTroops:
                    return row.Type == RowType.Troop && row.Change < 0;
                default:
                    return Covers(row);
            }
        }

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
                    return row.Type == RowType.Tavern && string.Equals(row.Id, RowId, StringComparison.Ordinal);
                case PlanPartKind.OtherGoods:
                    return row.Type == RowType.Loot && row.LootGroup == LootGroup.OtherGoods;
                default:
                    return false;
            }
        }

        public bool Equals(PlanPart? other) =>
            other != null && other.Kind == Kind && string.Equals(other.RowId, RowId, StringComparison.Ordinal);

        public override bool Equals(object? obj) => Equals(obj as PlanPart);

        public override int GetHashCode() => ((int)Kind * 397) ^ (RowId == null ? 0 : StringComparer.Ordinal.GetHashCode(RowId));

        public override string ToString() => Id;
    }
}
