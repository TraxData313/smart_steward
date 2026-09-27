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
    /// the buys.
    /// </remarks>
    internal static class PrisonerPlanner
    {
        public static List<PlanRow> Plan(PlanContext ctx, out int prisonersAfter)
        {
            var settings = ctx.Settings;
            var prison = ctx.Snapshot.Prison ?? new PrisonInfo();
            var all = (ctx.Snapshot.Prisoners ?? new List<PrisonerStack>()).Where(p => p != null && p.Count > 0).ToList();
            prisonersAfter = all.Sum(p => p.Count);

            var rows = new List<PlanRow>();
            bool ransom = ctx.IsTown && settings.RansomPrisoners && prison.CanRansom;
            bool donate = ctx.IsTown && settings.DonatePrisonersWhenPossible && prison.DonateAllowed;
            if (!ransom && !donate)
                return rows;

            var excluded = new HashSet<string>(settings.PrisonersExcluded ?? new List<string>(), StringComparer.Ordinal);
            int room = Math.Max(0, prison.DungeonRoom);
            foreach (var prisoner in all.Where(p => !p.IsLocked)
                         .OrderByDescending(p => p.RansomValue).ThenBy(p => p.TroopId, StringComparer.Ordinal))
            {
                bool isExcluded = excluded.Contains(prisoner.TroopId);
                bool proposed = !isExcluded && (!prisoner.IsHero || settings.RansomHeroPrisoners);
                int donated = proposed && donate ? Math.Min(prisoner.Count, room) : 0;
                room -= donated;
                int ransomed = proposed && ransom ? prisoner.Count - donated : 0;

                var row = new PlanRow("prisoner:" + prisoner.TroopId, PlanSectionKind.Prisoners, RowType.Prisoner)
                {
                    Name = prisoner.Name,
                    TroopId = prisoner.TroopId,
                    Mine = prisoner.Count,
                    Change = -(donated + ransomed),
                    MaxSell = ransom ? prisoner.Count : Math.Min(prisoner.Count, Math.Max(0, prison.DungeonRoom)),
                    GoldDelta = ransomed * prisoner.RansomValue,
                    InfluenceDelta = donated * prisoner.InfluencePerMan,
                    UnitPriceMin = ransomed > 0 ? prisoner.RansomValue : 0,
                    UnitPriceMax = ransomed > 0 ? prisoner.RansomValue : 0,
                    Prisoner = new PrisonerRowInfo
                    {
                        RansomValue = prisoner.RansomValue,
                        InfluencePerMan = prisoner.InfluencePerMan,
                        IsHero = prisoner.IsHero,
                        IsExcluded = isExcluded,
                        RansomCount = ransomed,
                        DonateCount = donated,
                    },
                };
                row.SuggestedChange = row.Change;
                rows.Add(row);
                prisonersAfter -= donated + ransomed;
                ctx.Gold += row.GoldDelta;
            }
            return rows;
        }
    }
}
