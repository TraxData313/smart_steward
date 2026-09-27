using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// Prisoners (DESIGN §2.5): towns only. One row per troop, most valuable first. Locked prisoners are never
    /// ransomed nor donated and get no row (like vanilla's "Ransom your prisoners"). Excluded troops and — unless
    /// RansomHeroPrisoners — heroes get a row at 0, so the player may still add them by hand.
    /// </summary>
    /// <remarks>
    /// With DonatePrisonersWhenPossible and donating allowed here, prisoners fill the dungeon's room first —
    /// the most valuable first, since influence per prisoner grows with the ransom value and the room is what
    /// runs out — and the rest are ransomed (when RansomPrisoners). Ransom gold is paid by the game, not the
    /// market, so the market's purse never limits it; the executor ransoms BEFORE trading so the gold funds
    /// the buys. The split (<see cref="Split"/>) is shared with the plan editor, which re-splits after every
    /// click.
    /// </remarks>
    internal static class PrisonerPlanner
    {
        /// <summary>Where the moved prisoners of one row go.</summary>
        internal readonly struct PrisonerMove
        {
            public PrisonerMove(int donated, int ransomed)
            {
                Donated = donated;
                Ransomed = ransomed;
            }

            public int Donated { get; }
            public int Ransomed { get; }
            public int Moved => Donated + Ransomed;
        }

        public static bool CanRansom(PlanContext ctx) =>
            ctx.IsTown && ctx.Settings.RansomPrisoners && (ctx.Snapshot.Prison ?? new PrisonInfo()).CanRansom;

        public static bool CanDonate(PlanContext ctx) =>
            ctx.IsTown && ctx.Settings.DonatePrisonersWhenPossible && (ctx.Snapshot.Prison ?? new PrisonInfo()).DonateAllowed;

        public static int DungeonRoom(PlanContext ctx) => Math.Max(0, (ctx.Snapshot.Prison ?? new PrisonInfo()).DungeonRoom);

        public static List<PlanRow> Plan(PlanContext ctx, out int prisonersAfter)
        {
            var settings = ctx.Settings;
            var all = (ctx.Snapshot.Prisoners ?? new List<PrisonerStack>()).Where(p => p != null && p.Count > 0).ToList();
            prisonersAfter = all.Sum(p => p.Count);

            var rows = new List<PlanRow>();
            bool ransom = CanRansom(ctx);
            bool donate = CanDonate(ctx);
            if (!ransom && !donate)
                return rows;

            var excluded = new HashSet<string>(settings.PrisonersExcluded ?? new List<string>(), StringComparer.Ordinal);
            int room = DungeonRoom(ctx);
            var wanted = new List<int>();
            foreach (var prisoner in all.Where(p => !p.IsLocked)
                         .OrderByDescending(p => p.RansomValue).ThenBy(p => p.TroopId, StringComparer.Ordinal))
            {
                bool isExcluded = excluded.Contains(prisoner.TroopId);
                bool proposed = !isExcluded && (!prisoner.IsHero || settings.RansomHeroPrisoners);
                wanted.Add(proposed ? prisoner.Count : 0);
                rows.Add(new PlanRow("prisoner:" + prisoner.TroopId, PlanSectionKind.Prisoners, RowType.Prisoner)
                {
                    Name = prisoner.Name,
                    TroopId = prisoner.TroopId,
                    Mine = prisoner.Count,
                    MaxSell = ransom ? prisoner.Count : Math.Min(prisoner.Count, room),
                    Prisoner = new PrisonerRowInfo
                    {
                        RansomValue = prisoner.RansomValue,
                        InfluencePerMan = prisoner.InfluencePerMan,
                        IsHero = prisoner.IsHero,
                        IsExcluded = isExcluded,
                    },
                });
            }

            var moves = Split(wanted, ransom, donate, room);
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                Apply(row, moves[i]);
                row.SuggestedChange = row.Change;
                prisonersAfter -= moves[i].Moved;
                ctx.Gold += row.GoldDelta;
            }
            return rows;
        }

        /// <summary>
        /// The rows' wanted prisoners (in the rows' order, most valuable first) → donated and ransomed: donations
        /// fill the dungeon's room first, the rest is ransomed when ransoming is possible here. Without ransom a
        /// row may move fewer than it wants (the room ran out).
        /// </summary>
        public static PrisonerMove[] Split(IReadOnlyList<int> wanted, bool ransom, bool donate, int room)
        {
            var moves = new PrisonerMove[wanted.Count];
            for (int i = 0; i < wanted.Count; i++)
            {
                int want = Math.Max(0, wanted[i]);
                int donated = donate ? Math.Min(want, room) : 0;
                room -= donated;
                moves[i] = new PrisonerMove(donated, ransom ? want - donated : 0);
            }
            return moves;
        }

        /// <summary>Change, gold, influence and prices of a prisoner row from its move.</summary>
        public static void Apply(PlanRow row, PrisonerMove move)
        {
            var info = row.Prisoner!;
            row.Change = -move.Moved;
            row.GoldDelta = move.Ransomed * info.RansomValue;
            row.InfluenceDelta = move.Donated * info.InfluencePerMan;
            row.UnitPriceMin = move.Ransomed > 0 ? info.RansomValue : 0;
            row.UnitPriceMax = row.UnitPriceMin;
            info.RansomCount = move.Ransomed;
            info.DonateCount = move.Donated;
        }
    }
}
