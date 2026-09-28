using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The troops section (step 16, DESIGN §2.8 — Anton 2026.09.28: "list me all the recruits available in the town — don't
    /// worry about the troop limit, just show me that I'm above it — and add all my troops like with the goods, but don't
    /// list every possible troop: list what is on offer at the top and what I have, so I can manage them, drop some of mine
    /// and recruit new"). Towns and villages. One row per troop type:
    /// <list type="bullet">
    /// <item><b>Recruits</b> (top): every type the settlement's notables offer the player now — Mine = the party's men of the
    /// type; [+] recruits up to what is on offer, [−] dismisses the party's own men of the type.</item>
    /// <item><b>Your troops</b> (below): every other regular type in the party the party screen lets go — [−] dismisses.</item>
    /// </list>
    /// Every row starts at 0 — the steward never recruits or dismisses by itself (the autonomous steward never even lists
    /// the section); in a live re-plan a row starts at the player's own number (<see cref="PlanPins"/>). The party size limit
    /// never blocks: the footer shows the party after the deal against it.
    /// </summary>
    /// <remarks>
    /// Order within each half — by TIER (Anton 2026.09.28, PLAN step 18: "MY troops ordered by tier, LOWEST on top"): the
    /// party's own troops lowest tier first — what a dismissal takes first sits on top —, the recruits on offer HIGHEST tier
    /// first — what Anton takes [decided: Claude, 2026.09.28 — step 18; Anton can flip it] —; ties by name (ordinal), then
    /// id. (Step 16 ordered both halves by name.) <see cref="DismissOrder"/> and <see cref="RecruitOrder"/> are the same
    /// orders for the folded Troops line's [−] and [+] (<see cref="StewardPlan.DismissLowest"/>).
    /// </remarks>
    internal static class TroopPlanner
    {
        /// <summary>The row id of a troop type.</summary>
        public static string RowId(string troopId) => "troops:" + troopId;

        public static List<PlanRow> Plan(PlanContext ctx)
        {
            var rows = new List<PlanRow>();
            if (!ctx.Settings.ShowTroops)
                return rows;
            var types = (ctx.Snapshot.Troops ?? new List<TroopStack>())
                .Where(t => t != null && !string.IsNullOrEmpty(t.TroopId))
                .GroupBy(t => t.TroopId, StringComparer.Ordinal)
                .Select(Merge)
                .Where(t => t.OnOffer > 0 || (t.InParty > 0 && t.CanDismiss))
                .OrderBy(t => t.OnOffer > 0 ? 0 : 1)
                .ThenBy(t => t.OnOffer > 0 ? -t.Tier : t.Tier)
                .ThenBy(t => t.Name, StringComparer.Ordinal)
                .ThenBy(t => t.TroopId, StringComparer.Ordinal);
            foreach (var t in types)
            {
                bool onOffer = t.OnOffer > 0;
                rows.Add(Pinned(ctx, new PlanRow(RowId(t.TroopId), onOffer ? PlanSectionKind.Recruits : PlanSectionKind.Troops,
                    RowType.Troop)
                {
                    Name = string.IsNullOrEmpty(t.Name) ? t.TroopId : t.Name,
                    TroopId = t.TroopId,
                    Mine = Math.Max(0, t.InParty),
                    Market = onOffer ? t.OnOffer : (int?)null,
                    MaxBuy = Math.Max(0, t.OnOffer),
                    MaxSell = t.CanDismiss ? Math.Max(0, t.InParty) : 0,
                    Troop = new TroopRowInfo
                    {
                        Tier = Math.Max(0, t.Tier),
                        UnitPrice = onOffer ? Math.Max(0, t.PricePerMan) : 0,
                        DailyWage = Math.Max(0, t.WagePerMan),
                        OnOffer = Math.Max(0, t.OnOffer),
                        Wounded = Math.Max(0, Math.Min(t.Wounded, t.InParty)),
                        IsMounted = t.IsMounted,
                        SeaWeightPerMan = Math.Max(0, t.SeaWeightPerMan),
                    },
                }));
            }
            return rows;
        }

        /// <summary>The Recruits line's rows and its [+] order (step 18): the types on offer, highest tier first, ties by name, then id.</summary>
        internal static List<PlanRow> RecruitOrder(IEnumerable<PlanRow> rows) =>
            rows.Where(r => r.Type == RowType.Troop && r.Troop != null && r.MaxBuy > 0)
                .OrderByDescending(r => r.Troop!.Tier)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .ThenBy(r => r.Id, StringComparer.Ordinal)
                .ToList();

        /// <summary>The Your troops line's rows and its [−] order (step 18 — Anton: "dismisses from the lowest tier up"; step 20:
        /// the line lists every type the party holds and may dismiss, on offer here or not — mockup choice 8): lowest tier first,
        /// ties by name, then id.</summary>
        internal static List<PlanRow> DismissOrder(IEnumerable<PlanRow> rows) =>
            rows.Where(r => r.Type == RowType.Troop && r.Troop != null && r.MaxSell > 0)
                .OrderBy(r => r.Troop!.Tier)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .ThenBy(r => r.Id, StringComparer.Ordinal)
                .ToList();

        /// <summary>One entry per troop type (a hand-built snapshot might repeat one): counts add up, the facts are the first's.</summary>
        private static TroopStack Merge(IEnumerable<TroopStack> same)
        {
            var list = same.ToList();
            if (list.Count == 1)
                return list[0];
            var first = list[0];
            return new TroopStack
            {
                TroopId = first.TroopId,
                Name = first.Name,
                Tier = first.Tier,
                InParty = list.Sum(t => Math.Max(0, t.InParty)),
                Wounded = list.Sum(t => Math.Max(0, t.Wounded)),
                CanDismiss = list.All(t => t.CanDismiss || t.InParty <= 0),
                OnOffer = list.Sum(t => Math.Max(0, t.OnOffer)),
                PricePerMan = list.Max(t => t.PricePerMan),
                WagePerMan = first.WagePerMan,
                IsMounted = first.IsMounted,
                SeaWeightPerMan = first.SeaWeightPerMan,
            };
        }

        /// <summary>Every troop row starts at 0 — or, in a live re-plan, at the player's own number (the party after the deal
        /// the steward plans the food and horses for).</summary>
        private static PlanRow Pinned(PlanContext ctx, PlanRow row)
        {
            if (ctx.Pins.TryGet(row.Id, out int change))
            {
                row.Change = Math.Max(-row.MaxSell, Math.Min(row.MaxBuy, change));
                ApplyGold(row);
            }
            return row;
        }

        /// <summary>A troop row's gold and prices from its quantity: recruits cost the price per man, dismissals nothing.</summary>
        internal static void ApplyGold(PlanRow row)
        {
            int recruited = Math.Max(0, row.Change);
            int price = row.Troop?.UnitPrice ?? 0;
            row.GoldDelta = -recruited * price;
            row.UnitPriceMin = recruited > 0 ? price : 0;
            row.UnitPriceMax = row.UnitPriceMin;
        }
    }
}
