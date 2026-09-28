using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// Tavern (DESIGN §2.7): towns only. The steward never proposes a hire — every row starts at 0 and the
    /// player clicks. A wanderer is a 0/1 row, blocked only when the clan's companion limit is reached (vanilla
    /// enforces it); the mercenary row is clamped to what is on offer. The party size limit never blocks a hire —
    /// vanilla has no such check either; the footer shows the party after the deal against it (Anton 2026.09.28,
    /// playtest round 3). Hires sit outside the money floors' chain (a breach only shows red).
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

            if (settings.ShowWanderers)
            {
                foreach (var wanderer in (tavern.Wanderers ?? new List<WandererForHire>())
                             .Where(w => w != null)
                             .OrderBy(w => w.Name, StringComparer.Ordinal)
                             .ThenBy(w => w.HeroId, StringComparer.Ordinal))
                {
                    var block = ctx.Snapshot.Party.CompanionSlotsFree <= 0 ? HireBlock.CompanionLimit : HireBlock.None;
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
                rows.Add(new PlanRow("tavern:mercenaries", PlanSectionKind.Tavern, RowType.Tavern)
                {
                    Name = mercenaries.Name,
                    TroopId = mercenaries.TroopId,
                    Mine = Math.Max(0, mercenaries.InParty),
                    Market = mercenaries.Available,
                    MaxBuy = mercenaries.Available,
                    Tavern = new TavernRowInfo
                    {
                        Kind = TavernRowKind.Mercenaries,
                        UnitPrice = mercenaries.PricePerMan,
                        DailyWage = mercenaries.WagePerMan,
                        SeaWeightPerMan = Math.Max(0, mercenaries.SeaWeightPerMan),
                    },
                });
            }
            return rows;
        }
    }
}
