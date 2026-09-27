using SmartSteward.Core.Planning;
using SmartSteward.Core.Pricing;

namespace SmartSteward.Core.Execution
{
    /// <summary>Why the executor did fewer units of a transaction than the plan asked (PLAN step 6). The world is
    /// re-read at the click; what no longer fits is skipped and logged, never forced.</summary>
    public enum SkipReason
    {
        None,

        /// <summary>A sale: the party no longer holds the units. A prisoner: no longer in the party's prison.</summary>
        NotHeld,

        /// <summary>A purchase: the market no longer has the units. A hire: no longer on offer (the wanderer left,
        /// the mercenary band changed or is gone).</summary>
        NotOnOffer,

        /// <summary>Locked in the inventory / party screen since the plan was made — never sold or ransomed.</summary>
        Locked,

        /// <summary>The market cannot pay for the next unit (DESIGN §3.4: never a sale the market cannot pay).</summary>
        MarketOutOfGold,

        /// <summary>The purse cannot pay (a wanderer: vanilla needs MORE gold than his price).</summary>
        NotEnoughGold,

        /// <summary>The live price fails the row's own limit (max buy / min sell) — the plan never trades past it.</summary>
        PriceLimit,

        /// <summary>The game no longer allows it here (trade access, the ransom broker, donating, the tavern).</summary>
        NotAllowedHere,

        DungeonFull,
        PartyFull,
        CompanionLimit,

        /// <summary>The game's own logic refused (the trade screen's DoneLogic said no, a transfer did not move).</summary>
        GameRefused,

        /// <summary>An exception — logged with its stack; the transaction is left out.</summary>
        Error,

        /// <summary>Done, then undone: another trade of the same batch failed, so the whole trade was reset to how
        /// it was (the party never keeps goods it did not pay for).</summary>
        RolledBack,
    }

    /// <summary>
    /// The purse and the market's purse as the executor goes, unit by unit — the same two rules the planner and
    /// the editor walk by (DESIGN §3): a sale needs the market to still be able to pay for it (gross sales, not net
    /// — the planner's own rule, stricter than vanilla's payout cap), a purchase needs the purse to pay for it, and
    /// every unit honours its row's price limit. The floors (MinGoldAfterDeal…) are the plan's business, not the
    /// executor's: the player saw them and clicked.
    /// </summary>
    public sealed class ExecutionBudget
    {
        public ExecutionBudget(int purse, int marketGold)
        {
            Purse = purse;
            MarketGoldLeft = marketGold;
        }

        /// <summary>The player's gold as the trades go (gold now + sales − purchases so far).</summary>
        public int Purse { get; private set; }

        /// <summary>What the market can still pay for items it buys.</summary>
        public int MarketGoldLeft { get; private set; }

        public int Sales { get; private set; }
        public int Purchases { get; private set; }

        /// <summary>May the next unit move at <paramref name="price"/>? <paramref name="limit"/> is the row lane's
        /// price limit for this stack (buy: max, sell: min; null = none).</summary>
        public SkipReason Check(TradeDirection direction, int price, int? limit)
        {
            if (direction == TradeDirection.Sell)
            {
                if (limit != null && price < limit.Value) return SkipReason.PriceLimit;
                if (price > MarketGoldLeft) return SkipReason.MarketOutOfGold;
                return SkipReason.None;
            }
            if (limit != null && price > limit.Value) return SkipReason.PriceLimit;
            if (price > Purse) return SkipReason.NotEnoughGold;
            return SkipReason.None;
        }

        /// <summary>Books one unit moved at <paramref name="price"/>.</summary>
        public void Record(TradeDirection direction, int price)
        {
            if (direction == TradeDirection.Sell)
            {
                Purse += price;
                MarketGoldLeft -= price;
                Sales += price;
            }
            else
            {
                Purse -= price;
                Purchases += price;
            }
        }

        /// <summary>The direction of an item transaction (Sell / Buy).</summary>
        public static TradeDirection DirectionOf(PlanTransaction transaction) =>
            transaction.Kind == TransactionKind.Sell ? TradeDirection.Sell : TradeDirection.Buy;

        /// <summary>
        /// The price limit the plan walked this transaction's stack by — its row's sell or buy lane (price book ×
        /// multiplier, and the role cap for animals). Null when the stack has none (loot sells at any price; a
        /// row or stack the plan no longer has).
        /// </summary>
        public static int? PriceLimitOf(StewardPlan plan, PlanTransaction transaction)
        {
            if (plan == null || transaction == null || !transaction.IsItemTrade)
                return null;
            var row = plan.FindRow(transaction.RowId);
            var lane = transaction.Kind == TransactionKind.Sell ? row?.SellLane : row?.BuyLane;
            if (lane == null)
                return null;
            foreach (var laneStack in lane.Stacks)
                if (string.Equals(laneStack.Stack.Key, transaction.StackKey, System.StringComparison.Ordinal))
                    return laneStack.PriceLimit;
            return null;
        }
    }
}
