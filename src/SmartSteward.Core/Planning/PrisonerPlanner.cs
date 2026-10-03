using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// Prisoners (DESIGN §2.5): towns, and (step 34) castles — donations only, no ransom broker there. One row per troop — lowest tier first, the lords last (round 4: "add the Tier to the
    /// prisoners too and make them sorted like the troops lower->higher tier"). Locked prisoners are never ransomed nor
    /// donated and get no row (like vanilla's "Ransom your prisoners"). Two actions (round 4, Anton 2026.09.28 — they replace
    /// RansomPrisoners / RansomHeroPrisoners / DonatePrisonersWhenPossible): <c>LordPrisonerAction</c> for the lords (Keep by
    /// default) and <c>PrisonerAction</c> for the others (Ransom by default), each Keep | Ransom | Donate — all or none; a
    /// Keep row sits at 0, so the player may still move it by hand.
    /// </summary>
    /// <remarks>
    /// Where each row's prisoners go (<see cref="PrisonerRowInfo.ToDungeonFirst"/>, <see cref="PrisonerRowInfo.MayRansom"/>) —
    /// the steward's and the player's alike: a Donate row where the game allows donating here fills the dungeon's room first —
    /// the most valuable first across the rows, since influence per prisoner grows with the ransom value and the room is what
    /// runs out —, and what does not fit is ransomed, except a lord (Anton: "Donate where the game does not allow it → others
    /// are ransomed, lords are kept"); every other row is ransomed (where the ransom broker is open to the player). Ransom gold
    /// is paid by the game, not the market, so the market's purse never limits it; the executor ransoms BEFORE trading so the
    /// gold funds the buys. The split (<see cref="Split"/>) is shared with the plan editor, which re-splits after every click.
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

        /// <summary>The ransom broker is open to the player here (a town, the tavern district).</summary>
        public static bool CanRansom(PlanContext ctx) => ctx.IsTown && (ctx.Snapshot.Prison ?? new PrisonInfo()).CanRansom;

        /// <summary>The game lets the player donate prisoners here (own faction, not own clan, dungeon access).</summary>
        public static bool CanDonate(PlanContext ctx) => ctx.HasDungeon && (ctx.Snapshot.Prison ?? new PrisonInfo()).DonateAllowed;

        public static int DungeonRoom(PlanContext ctx) => Math.Max(0, (ctx.Snapshot.Prison ?? new PrisonInfo()).DungeonRoom);

        public static List<PlanRow> Plan(PlanContext ctx, out int prisonersAfter)
        {
            var settings = ctx.Settings;
            var all = (ctx.Snapshot.Prisoners ?? new List<PrisonerStack>()).Where(p => p != null && p.Count > 0).ToList();
            prisonersAfter = all.Sum(p => p.Count);

            var rows = new List<PlanRow>();
            bool ransom = CanRansom(ctx);
            bool donate = CanDonate(ctx);
            // Step 34: a castle keeps its rows even where donating is forbidden — every one at 0, nowhere to go (no ransom broker
            // in a castle) — so the window shows the prisoners and says why; a town without broker or dungeon has no rows.
            if (!ransom && !donate && !ctx.IsCastle)
                return rows;

            int room = DungeonRoom(ctx);
            var wanted = new List<int>();
            foreach (var prisoner in all.Where(p => !p.IsLocked).OrderBy(p => p.IsHero ? 1 : 0).ThenBy(p => p.Tier)
                         .ThenBy(p => p.Name, StringComparer.Ordinal).ThenBy(p => p.TroopId, StringComparer.Ordinal))
            {
                var action = prisoner.IsHero ? settings.LordPrisonerAction : settings.PrisonerAction;
                var info = new PrisonerRowInfo
                {
                    RansomValue = prisoner.RansomValue,
                    InfluencePerMan = prisoner.InfluencePerMan,
                    IsHero = prisoner.IsHero,
                    Tier = Math.Max(0, prisoner.Tier),
                    Action = action,
                    ToDungeonFirst = action == Settings.PrisonerChoice.Donate && donate,
                    MayRansom = ransom && !(prisoner.IsHero && action == Settings.PrisonerChoice.Donate),
                };
                // Step 26: the prisoners a quest keeps (a landowner's laborers, a lord's rival) are never ransomed nor donated.
                int free = Math.Max(0, prisoner.Count - ctx.Quests.Prisoner(prisoner.TroopId));
                var row = new PlanRow("prisoner:" + prisoner.TroopId, PlanSectionKind.Prisoners, RowType.Prisoner)
                {
                    Name = prisoner.Name,
                    TroopId = prisoner.TroopId,
                    Mine = prisoner.Count,
                    MaxSell = info.MayRansom ? free : info.ToDungeonFirst ? Math.Min(free, room) : 0,
                    Prisoner = info,
                    Quest = ctx.Quests.ForPrisoner(prisoner.TroopId),
                };
                // A live re-plan keeps the player's own number of prisoners to move (a touched row, PlanPins); the steward moves
                // them all or none by the row's action.
                wanted.Add(ctx.Pins.TryGet(row.Id, out int pin)
                    ? Math.Max(0, Math.Min(row.MaxSell, -pin))
                    : action == Settings.PrisonerChoice.Keep ? 0 : row.MaxSell);
                rows.Add(row);
            }

            var moves = Split(wanted, rows.Select(r => r.Prisoner!).ToList(), room);
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
        /// The rows' wanted prisoners → donated and ransomed, index by index: the rows whose prisoners go to the dungeon first
        /// fill its room — the MOST VALUABLE first across them (ransom value, then troop id; whatever the table's order) — and
        /// what does not fit is ransomed where the row may ransom (a lord to donate never is); every other row is ransomed
        /// where it may. A row may move fewer than it wants (the room ran out, no broker).
        /// </summary>
        public static PrisonerMove[] Split(IReadOnlyList<int> wanted, IReadOnlyList<PrisonerRowInfo> rows, int room)
        {
            var moves = new PrisonerMove[wanted.Count];
            var donated = new int[wanted.Count];
            foreach (int i in Enumerable.Range(0, wanted.Count).Where(i => rows[i].ToDungeonFirst)
                         .OrderByDescending(i => rows[i].RansomValue).ThenBy(i => rows[i].TroopId ?? "", StringComparer.Ordinal))
            {
                donated[i] = Math.Min(Math.Max(0, wanted[i]), Math.Max(0, room));
                room -= donated[i];
            }
            for (int i = 0; i < wanted.Count; i++)
            {
                int rest = Math.Max(0, wanted[i]) - donated[i];
                moves[i] = new PrisonerMove(donated[i], rows[i].MayRansom ? rest : 0);
            }
            return moves;
        }

        /// <summary>Change, gold, influence and prices of a prisoner row from its move.</summary>
        public static void Apply(PlanRow row, PrisonerMove move)
        {
            var info = row.Prisoner!;
            row.Change = -move.Moved;
            info.TroopId = row.TroopId;
            row.GoldDelta = move.Ransomed * info.RansomValue;
            row.InfluenceDelta = move.Donated * info.InfluencePerMan;
            row.UnitPriceMin = move.Ransomed > 0 ? info.RansomValue : 0;
            row.UnitPriceMax = row.UnitPriceMin;
            info.RansomCount = move.Ransomed;
            info.DonateCount = move.Donated;
        }
    }
}
