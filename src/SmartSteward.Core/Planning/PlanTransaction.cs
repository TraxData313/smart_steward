using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    public enum TransactionKind
    {
        /// <summary>Prisoners to the settlement's dungeon (influence, no gold).</summary>
        Donate,

        /// <summary>Prisoners to the ransom broker (gold from the game, not the market).</summary>
        Ransom,

        /// <summary>Items from the party to the market.</summary>
        Sell,

        /// <summary>Items from the market to the party.</summary>
        Buy,

        HireWanderer,
        HireMercenaries,
    }

    /// <summary>
    /// One thing the executor does (PLAN step 6), in <see cref="StewardPlan.Transactions"/> order. The
    /// expected prices are the plan's walk; the game walks the real ones at the click, and the executor
    /// re-checks gold, stock and limits then.
    /// </summary>
    public sealed class PlanTransaction
    {
        private PlanTransaction(TransactionKind kind, PlanRow row, int count, IReadOnlyList<int> unitPrices,
            double influence, ItemStack? stack)
        {
            Kind = kind;
            RowId = row.Id;
            Count = count;
            UnitPrices = unitPrices;
            Gold = unitPrices.Sum();
            Influence = influence;
            StackKey = stack?.Key;
            ItemId = stack?.ItemId;
            ModifierId = stack?.ModifierId;
            TroopId = row.TroopId;
            HeroId = row.HeroId;
        }

        public TransactionKind Kind { get; }
        public string RowId { get; }

        /// <summary>Sell / Buy: the element (item + modifier) — the key the executor maps back to the game.</summary>
        public string? StackKey { get; }
        public string? ItemId { get; }
        public string? ModifierId { get; }

        /// <summary>Donate / Ransom / HireMercenaries: the troop.</summary>
        public string? TroopId { get; }

        /// <summary>HireWanderer: the hero.</summary>
        public string? HeroId { get; }

        public int Count { get; }

        /// <summary>The expected price of each unit, in order (a town's price walks unit by unit); empty for a
        /// donation.</summary>
        public IReadOnlyList<int> UnitPrices { get; }

        /// <summary>The expected total, always positive: paid to the party (Ransom, Sell) or by it (Buy, hires).</summary>
        public int Gold { get; }

        /// <summary>Donate: the influence expected.</summary>
        public double Influence { get; }

        public bool IsItemTrade => Kind == TransactionKind.Sell || Kind == TransactionKind.Buy;

        internal static IReadOnlyList<PlanTransaction> Build(WalkOutcome outcome)
        {
            var list = new List<PlanTransaction>();

            foreach (var o in outcome.Rows.Where(o => o.Row.Type == RowType.Prisoner))
            {
                var info = o.Row.Prisoner!;
                if (o.Donated > 0)
                    list.Add(new PlanTransaction(TransactionKind.Donate, o.Row, o.Donated, Array.Empty<int>(),
                        o.Donated * info.InfluencePerMan, null));
                if (o.Ransomed > 0)
                    list.Add(new PlanTransaction(TransactionKind.Ransom, o.Row, o.Ransomed,
                        Enumerable.Repeat(info.RansomValue, o.Ransomed).ToArray(), 0, null));
            }

            foreach (var direction in new[] { TradeDirection.Sell, TradeDirection.Buy })
            {
                // The walk's order, grouped by item category in order of first appearance: a town's price walks
                // per category, so moving whole categories past each other never changes a price, and the
                // executor gets one transaction per stack wherever the walk only interleaved categories.
                var steps = outcome.Steps.Where(s => s.Direction == direction).ToList();
                var rank = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var step in steps)
                    if (!rank.ContainsKey(step.Stack.CategoryId))
                        rank[step.Stack.CategoryId] = rank.Count;
                var ordered = steps.Select((s, i) => (Step: s, Index: i))
                    .OrderBy(x => rank[x.Step.Stack.CategoryId]).ThenBy(x => x.Index)
                    .Select(x => x.Step);

                WalkStep? runStart = null;
                var prices = new List<int>();
                foreach (var step in ordered)
                {
                    if (runStart != null && (!ReferenceEquals(runStart.Value.Row, step.Row)
                                             || !string.Equals(runStart.Value.Stack.Key, step.Stack.Key, StringComparison.Ordinal)))
                    {
                        list.Add(ItemTrade(direction, runStart.Value, prices));
                        prices = new List<int>();
                    }
                    if (prices.Count == 0)
                        runStart = step;
                    prices.Add(step.Price);
                }
                if (runStart != null && prices.Count > 0)
                    list.Add(ItemTrade(direction, runStart.Value, prices));
            }

            foreach (var kind in new[] { TavernRowKind.Wanderer, TavernRowKind.Mercenaries })
                foreach (var o in outcome.Rows.Where(o => o.Row.Type == RowType.Tavern && o.Row.Tavern!.Kind == kind))
                    if (o.Realized > 0)
                        list.Add(new PlanTransaction(
                            kind == TavernRowKind.Wanderer ? TransactionKind.HireWanderer : TransactionKind.HireMercenaries,
                            o.Row, o.Realized, Enumerable.Repeat(o.Row.Tavern!.UnitPrice, o.Realized).ToArray(), 0, null));
            return list;
        }

        private static PlanTransaction ItemTrade(TradeDirection direction, WalkStep first, List<int> prices) =>
            new PlanTransaction(direction == TradeDirection.Sell ? TransactionKind.Sell : TransactionKind.Buy,
                first.Row, prices.Count, prices.ToArray(), 0, first.Stack);
    }
}
