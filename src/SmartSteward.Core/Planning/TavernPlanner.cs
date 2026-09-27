using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// Tavern (DESIGN §2.7): towns only. The steward never proposes a hire — every row starts at 0 and the
    /// player clicks. A wanderer is a 0/1 row, blocked when the clan's companion limit or the party size limit
    /// is reached; the mercenary row is clamped to what is on offer and to the party's room. Hires sit outside
    /// the money floors' chain (a breach only shows red).
    /// </summary>
    internal static class TavernPlanner
    {
        public static List<PlanRow> Plan(PlanContext ctx)
        {
            var settings = ctx.Settings;
            var tavern = ctx.Snapshot.Tavern;
            var rows = new List<PlanRow>();
            if (!ctx.IsTown || tavern == null || !settings.ShowTavern)
                return rows;

            int room = ctx.Snapshot.Party.Room;
            if (settings.ShowWanderers)
            {
                foreach (var wanderer in (tavern.Wanderers ?? new List<WandererForHire>())
                             .Where(w => w != null)
                             .OrderBy(w => w.Name, StringComparer.Ordinal)
                             .ThenBy(w => w.HeroId, StringComparer.Ordinal))
                {
                    var block = ctx.Snapshot.Party.CompanionSlotsFree <= 0 ? HireBlock.CompanionLimit
                        : room <= 0 ? HireBlock.PartyFull
                        : HireBlock.None;
                    rows.Add(new PlanRow("tavern:wanderer:" + wanderer.HeroId, PlanSectionKind.Tavern, RowType.Tavern)
                    {
                        Name = wanderer.Name,
                        HeroId = wanderer.HeroId,
                        Mine = 0,
                        Market = null,
                        MaxBuy = block == HireBlock.None ? 1 : 0,
                        Tavern = new TavernRowInfo
                        {
                            Kind = TavernRowKind.Wanderer,
                            UnitPrice = wanderer.HirePrice,
                            DailyWage = wanderer.DailyWage,
                            Block = block,
                            SkillTag = wanderer.SkillTag,
                        },
                    });
                }
            }

            var mercenaries = tavern.Mercenaries;
            if (settings.ShowMercenaries && mercenaries != null && mercenaries.Available > 0)
            {
                var block = room <= 0 ? HireBlock.PartyFull : HireBlock.None;
                rows.Add(new PlanRow("tavern:mercenaries", PlanSectionKind.Tavern, RowType.Tavern)
                {
                    Name = mercenaries.Name,
                    TroopId = mercenaries.TroopId,
                    Mine = Math.Max(0, mercenaries.InParty),
                    Market = mercenaries.Available,
                    MaxBuy = Math.Min(mercenaries.Available, room),
                    Tavern = new TavernRowInfo
                    {
                        Kind = TavernRowKind.Mercenaries,
                        UnitPrice = mercenaries.PricePerMan,
                        DailyWage = mercenaries.WagePerMan,
                        Block = block,
                    },
                });
            }
            return rows;
        }
    }
}
