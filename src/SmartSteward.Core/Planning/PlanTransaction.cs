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

        /// <summary>Men of the party let go (step 16) — vanilla's party screen without the screen: the wounded first, no gold,
        /// no event.</summary>
        Dismiss,

        /// <summary>Volunteers recruited from the settlement's notables (step 16) — vanilla's recruit screen without the
        /// screen: the slot emptied, the man added, <c>OnUnitRecruited</c>, the gold paid.</summary>
        Recruit,
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
            HonoursLock = kind == TransactionKind.Sell && row.LocksGuard;
        }

        /// <summary>The first <paramref name="count"/> units of <paramref name="source"/> (a part run alone, PLAN step 27: the purse
        /// stopped it there) — the same stack / troop / hero, its first prices, the influence in proportion.</summary>
        private PlanTransaction(PlanTransaction source, int count)
        {
            Kind = source.Kind;
            RowId = source.RowId;
            Count = count;
            UnitPrices = source.UnitPrices.Take(count).ToArray();
            Gold = UnitPrices.Sum();
            Influence = source.Count > 0 ? source.Influence * count / source.Count : 0;
            StackKey = source.StackKey;
            ItemId = source.ItemId;
            ModifierId = source.ModifierId;
            TroopId = source.TroopId;
            HeroId = source.HeroId;
            HonoursLock = source.HonoursLock;
        }

        /// <summary>This transaction with only its first <paramref name="count"/> units (1 … <see cref="Count"/>).</summary>
        internal PlanTransaction Cut(int count) =>
            count >= Count ? this : new PlanTransaction(this, Math.Max(0, count));

        public TransactionKind Kind { get; }
        public string RowId { get; }

        /// <summary>Sell / Buy: the element (item + modifier) — the key the executor maps back to the game.</summary>
        public string? StackKey { get; }
        public string? ItemId { get; }
        public string? ModifierId { get; }

        /// <summary>
        /// Sell: a lock on this stack in the inventory stops the sale at the click (<see cref="LockRule"/>) — armour and
        /// weapons always, food and animals only with LocksProtectFoodAndHorses; false = the stack is sold locked or not
        /// (the plan already counted it). Frozen at plan time from the row, like the rest of the plan.
        /// </summary>
        public bool HonoursLock { get; }

        /// <summary>Donate / Ransom / HireMercenaries / Dismiss / Recruit: the troop.</summary>
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

            // The troops section's dismissals (step 16) right after the prisoners: men leave before any goods move — free,
            // and nothing in the trade depends on them [decided: Claude, 2026.09.28 — step 16].
            foreach (var o in outcome.Rows.Where(o => o.Row.Type == RowType.Troop && o.Realized < 0))
                list.Add(new PlanTransaction(TransactionKind.Dismiss, o.Row, -o.Realized, Array.Empty<int>(), 0, null));

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

            // The recruits last (step 16): after the trades (their proceeds fund them) and the tavern's hires — the table's
            // order, tavern above troops [decided: Claude, 2026.09.28 — step 16].
            foreach (var o in outcome.Rows.Where(o => o.Row.Type == RowType.Troop && o.Realized > 0))
                list.Add(new PlanTransaction(TransactionKind.Recruit, o.Row, o.Realized,
                    Enumerable.Repeat(o.Row.Troop!.UnitPrice, o.Realized).ToArray(), 0, null));
            return list;
        }

        private static PlanTransaction ItemTrade(TradeDirection direction, WalkStep first, List<int> prices) =>
            new PlanTransaction(direction == TradeDirection.Sell ? TransactionKind.Sell : TransactionKind.Buy,
                first.Row, prices.Count, prices.ToArray(), 0, first.Stack);
    }
}
