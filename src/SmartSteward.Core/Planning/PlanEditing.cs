using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartSteward.Core.Planning
{
    /// <summary>How far one click on [+] / [−] goes (DESIGN §1.1): click ±1, shift ±5, ctrl ±all. The window
    /// maps the game's own FiveStackModifier / EntireStackModifier hot keys onto it.</summary>
    public enum EditSize
    {
        One,
        Five,
        All,
    }

    /// <summary>
    /// Why a row's [+] or [−] cannot move it (the button greys out; the tooltip says why). Plain values — the
    /// window words them through TextObject ids.
    /// </summary>
    public enum EditBlock
    {
        /// <summary>The button works.</summary>
        None,

        /// <summary>Loot and prisoners are only ever sold / ransomed / donated — never bought.</summary>
        SellOnly,

        /// <summary>Tavern rows only hire.</summary>
        BuyOnly,

        /// <summary>Nothing on offer the row may buy (none in the market, not Buy-ticked, no price to judge it
        /// by, or dearer than its max buy price).</summary>
        NoneEligible,

        /// <summary>Every unit on offer the row may buy is already in the plan (a wanderer: already hired).</summary>
        AllOnOffer,

        /// <summary>Nothing the row may sell (none held, all locked, or not Sell-ticked).</summary>
        NothingToSell,

        /// <summary>Every unit the row may sell is already in the plan (locked ones never are).</summary>
        AllSold,

        /// <summary>The next unit costs more than the row's max buy price (price book or role cap) — a town's
        /// prices climb as the plan buys.</summary>
        PriceLimit,

        /// <summary>The next unit would fetch less than its min sell price — a town's prices fall as the plan sells.</summary>
        BelowMinSellPrice,

        /// <summary>The market cannot pay for another unit (DESIGN §3.4 — never a sale it cannot pay).</summary>
        MarketOutOfGold,

        /// <summary>The purse cannot pay for it (a wanderer: vanilla wants MORE gold than his price). The money
        /// floors never block — only an empty purse does.</summary>
        NotEnoughGold,

        /// <summary>What the market has left is already planned for another row (its stock, or the room under a
        /// price limit in the same category).</summary>
        NeededByAnotherRow,

        /// <summary>The clan's companion limit is reached.</summary>
        CompanionLimit,

        /// <summary>The party size limit is reached.</summary>
        PartyFull,

        /// <summary>Only donation is possible here and the dungeon has no more room.</summary>
        DungeonFull,
    }

    /// <summary>What one click did.</summary>
    public sealed class EditResult
    {
        internal EditResult(string rowId, int before, int asked, int after, EditBlock block)
        {
            RowId = rowId;
            Before = before;
            Asked = asked;
            After = after;
            Block = block;
        }

        public string RowId { get; }
        public int Before { get; }

        /// <summary>Where the click wanted to go (±1, ±5 — stopping at zero —, or the row's bound for "all").</summary>
        public int Asked { get; }
        public int After { get; }

        /// <summary>Why <see cref="After"/> stopped short of <see cref="Asked"/>; None when it got there.</summary>
        public EditBlock Block { get; }
        public bool Moved => After != Before;
    }

    /// <summary>
    /// The player's hand on the plan (DESIGN §1.1, PLAN step 4b) — the window only binds buttons to these and
    /// never does arithmetic of its own.
    /// </summary>
    /// <remarks>
    /// Every edit changes ONE row's quantity; every other row keeps its quantity (the steward does not re-plan
    /// around the player), but the whole plan is walked again (<see cref="PlanReplay"/>) so every price, total
    /// and flag that depends on it refreshes — town prices walk per item category, the market's gold and stock
    /// are shared. Rules [decided: Claude, 2026.09.27 — step 4b]:
    /// <list type="bullet">
    /// <item>Toward zero (buy less, sell less) always works.</item>
    /// <item>Away from zero goes as far as it can without taking what another row already has (stock, the
    ///   market's gold, the price room in a category): the last unit that fits, found by walking.</item>
    /// <item>Shift and ctrl steps stop at zero — one click never flips a row from selling to buying; a single
    ///   click crosses zero where the row can do both (food and the mount role rows).</item>
    /// <item>The money floors never block (a breach shows red); an empty purse does — a buy or hire the purse
    ///   cannot pay is refused (NotEnoughGold). Lowering income may still leave a deal unaffordable:
    ///   <see cref="PlanTotals.CannotAfford"/> then shows it.</item>
    /// <item>When lowering one row leaves another impossible (rare: a sale lowered in a category another row
    ///   buys in, so the price climbs past that row's limit), that row is cut to what the market allows.</item>
    /// </list>
    /// </remarks>
    public sealed partial class StewardPlan
    {
        private readonly Dictionary<(PlanRow, int), EditBlock> _blocks = new Dictionary<(PlanRow, int), EditBlock>();
        private IReadOnlyList<PlanTransaction>? _transactions;

        /// <summary>The player moved at least one row away from the steward's suggestion.</summary>
        public bool IsEdited => Rows.Any(r => r.IsEdited);

        /// <summary>[+] on a row: buy / hire more, or sell less. Throws for an unknown row id.</summary>
        public EditResult Increase(string rowId, EditSize size = EditSize.One) => Step(RowOrThrow(rowId), +1, size);

        /// <summary>[−] on a row: sell / ransom more, or buy less.</summary>
        public EditResult Decrease(string rowId, EditSize size = EditSize.One) => Step(RowOrThrow(rowId), -1, size);

        /// <summary>⟲ on a row: back to the steward's suggestion — as far as what other rows took since allows.</summary>
        public EditResult Reset(string rowId)
        {
            var row = RowOrThrow(rowId);
            return MoveTo(row, row.SuggestedChange, Math.Sign(row.SuggestedChange - row.Change));
        }

        /// <summary>
        /// Moves a row toward <paramref name="value"/> as far as the walk allows — clamped to the row's range, never
        /// taking what another row has, crossing zero through 0 (as ⟲ does). What a re-plan uses to carry the player's
        /// edits over (<see cref="PlanCarryOver"/>); a click uses <see cref="Increase"/> / <see cref="Decrease"/>.
        /// </summary>
        public EditResult SetChange(string rowId, int value)
        {
            var row = RowOrThrow(rowId);
            return MoveTo(row, value, Math.Sign(value - row.Change));
        }

        /// <summary>Every row back to the steward's suggestion — exactly the plan as it was made.</summary>
        public void ResetAll()
        {
            if (_inputs == null)
                return;
            foreach (var row in Rows)
                row.Change = row.SuggestedChange;
            Settle();
        }

        /// <summary>
        /// The executor's list (DESIGN §5, PLAN step 6) for the plan as it stands: donations and ransoms first
        /// (the ransom funds the buys), then every sale, then every purchase — in the walk's order, grouped by
        /// item category (which never changes a price: a town's price walks per category) —, then the hires,
        /// wanderers before mercenaries. Each with the stack / troop / hero, the count and the expected prices.
        /// </summary>
        public IReadOnlyList<PlanTransaction> Transactions
        {
            get
            {
                if (_transactions == null)
                    _transactions = _inputs == null
                        ? Array.Empty<PlanTransaction>()
                        : PlanTransaction.Build(Walk(r => r.Change));
                return _transactions;
            }
        }

        internal EditBlock BlockOf(PlanRow row, int direction)
        {
            if (_inputs == null)
                return EditBlock.None;
            if (_blocks.TryGetValue((row, direction), out var cached))
                return cached;
            EditBlock block;
            int current = row.Change;
            if (current * direction < 0)
                block = EditBlock.None; // toward zero always works
            else
            {
                var (min, max) = Bounds(row);
                int next = current + direction;
                block = next < min || next > max ? StaticBlock(row, direction) : Trial(row, next);
            }
            _blocks[(row, direction)] = block;
            return block;
        }

        private PlanRow RowOrThrow(string rowId) =>
            FindRow(rowId) ?? throw new ArgumentException("No row '" + rowId + "' in this plan.", nameof(rowId));

        private EditResult Step(PlanRow row, int direction, EditSize size)
        {
            int before = row.Change;
            int asked;
            switch (size)
            {
                case EditSize.One:
                    asked = before + direction;
                    break;
                case EditSize.Five:
                    asked = before + 5 * direction;
                    if (before != 0 && Math.Sign(asked) == -Math.Sign(before))
                        asked = 0; // a big step stops at zero
                    break;
                default:
                    var (min, max) = Bounds(row);
                    asked = before * direction < 0 ? 0 : direction > 0 ? max : min;
                    break;
            }
            return MoveTo(row, asked, direction);
        }

        private EditResult MoveTo(PlanRow row, int asked, int direction)
        {
            int before = row.Change;
            if (_inputs != null && asked != before)
            {
                var (min, max) = Bounds(row);
                int target = Math.Max(min, Math.Min(max, asked));
                if (before != 0 && Math.Sign(target) == -Math.Sign(before))
                    Lower(row, 0); // crossing zero (only ⟲ does): first back to zero
                if (target == 0 || (Math.Sign(target) == Math.Sign(row.Change) && Math.Abs(target) <= Math.Abs(row.Change)))
                    Lower(row, target);
                else
                    Raise(row, target);
            }
            int after = row.Change;
            var block = after == asked || direction == 0 ? EditBlock.None : BlockOf(row, Math.Sign(asked - after));
            return new EditResult(row.Id, before, asked, after, block);
        }

        /// <summary>Toward zero: always done.</summary>
        private void Lower(PlanRow row, int value)
        {
            if (row.Change == value)
                return;
            row.Change = value;
            Settle();
        }

        /// <summary>Away from zero: the furthest value toward <paramref name="target"/> that the walk can do with
        /// every other row intact (binary search; the current value always can).</summary>
        private void Raise(PlanRow row, int target)
        {
            int direction = Math.Sign(target - row.Change);
            int lo = row.Change, hi = target;
            while (lo != hi)
            {
                int mid = lo + direction * ((Math.Abs(hi - lo) + 1) / 2);
                if (Trial(row, mid) == EditBlock.None)
                    lo = mid;
                else
                    hi = mid - direction;
            }
            if (lo == row.Change)
                return;
            row.Change = lo;
            Settle();
        }

        /// <summary>Would the plan work with this row at <paramref name="value"/>? None = yes; else why not.</summary>
        private EditBlock Trial(PlanRow row, int value)
        {
            var outcome = Walk(r => ReferenceEquals(r, row) ? value : r.Change);
            var own = outcome.Of(row);
            if (own.IsShort)
                return own.Short == EditBlock.None ? EditBlock.NeededByAnotherRow : own.Short;
            foreach (var other in outcome.Rows)
                if (other.IsShort)
                    return SharedLimit(other.Short) ? other.Short : EditBlock.NeededByAnotherRow;
            if (value > 0 && (outcome.GoldAfter < 0 || outcome.HireUnaffordable))
                return EditBlock.NotEnoughGold;
            return EditBlock.None;
        }

        /// <summary>A limit every row shares, worth naming when the edit would push ANOTHER row past it; the
        /// rest (stock, a category's price room) reads "another row needs it".</summary>
        private static bool SharedLimit(EditBlock block) =>
            block == EditBlock.MarketOutOfGold || block == EditBlock.PartyFull
            || block == EditBlock.CompanionLimit || block == EditBlock.DungeonFull;

        /// <summary>Walks the plan at the rows' quantities, cuts any row the market can no longer serve in full,
        /// and writes the walk into the rows, the totals and the transactions.</summary>
        private void Settle()
        {
            WalkOutcome outcome;
            while (true)
            {
                outcome = Walk(r => r.Change);
                if (outcome.AllRealized)
                    break;
                foreach (var o in outcome.Rows)
                    if (o.IsShort)
                        o.Row.Change = o.Realized;
            }
            Apply(outcome);
        }

        private WalkOutcome Walk(Func<PlanRow, int> changeOf) => PlanReplay.Walk(_inputs!, Rows.ToList(), changeOf);

        private void Apply(WalkOutcome outcome)
        {
            foreach (var o in outcome.Rows)
            {
                var row = o.Row;
                switch (row.Type)
                {
                    case RowType.Prisoner:
                        PrisonerPlanner.Apply(row, new PrisonerPlanner.PrisonerMove(o.Donated, o.Ransomed));
                        break;
                    case RowType.Tavern:
                        row.Change = o.Realized;
                        row.GoldDelta = -o.Realized * row.Tavern!.UnitPrice;
                        row.UnitPriceMin = o.Realized > 0 ? row.Tavern.UnitPrice : 0;
                        row.UnitPriceMax = row.UnitPriceMin;
                        break;
                    default:
                        row.Book = o.Book;
                        PlanMath.RefreshItemRow(row);
                        break;
                }
            }
            Totals = PlanTotals.Compute(Rows, _inputs!.Snapshot, _inputs.Floors);
            Totals.HireUnaffordable = outcome.HireUnaffordable;
            _transactions = PlanTransaction.Build(outcome);
            _blocks.Clear();
        }

        /// <summary>The row's static range: food and the mount role rows buy and sell; loot and prisoners only
        /// sell; tavern rows only hire.</summary>
        private static (int Min, int Max) Bounds(PlanRow row)
        {
            switch (row.Type)
            {
                case RowType.Loot:
                case RowType.Prisoner:
                    return (-row.MaxSell, 0);
                case RowType.Tavern:
                    return (0, row.MaxBuy);
                default:
                    return (-row.MaxSell, row.MaxBuy);
            }
        }

        /// <summary>Why the next unit lies outside the row's static range.</summary>
        private EditBlock StaticBlock(PlanRow row, int direction)
        {
            if (direction > 0)
            {
                switch (row.Type)
                {
                    case RowType.Loot:
                    case RowType.Prisoner:
                        return EditBlock.SellOnly;
                    case RowType.Tavern:
                        var info = row.Tavern!;
                        if (row.MaxBuy == 0)
                            return info.Block == HireBlock.CompanionLimit ? EditBlock.CompanionLimit : EditBlock.PartyFull;
                        return info.Kind == TavernRowKind.Mercenaries && row.MaxBuy < (row.Market ?? 0)
                            ? EditBlock.PartyFull
                            : EditBlock.AllOnOffer;
                    default:
                        return row.MaxBuy == 0 ? EditBlock.NoneEligible : EditBlock.AllOnOffer;
                }
            }
            switch (row.Type)
            {
                case RowType.Tavern:
                    return EditBlock.BuyOnly;
                case RowType.Prisoner:
                    return !_inputs!.Ransom && row.MaxSell < row.Mine ? EditBlock.DungeonFull : EditBlock.AllSold;
                default:
                    return row.MaxSell == 0 ? EditBlock.NothingToSell : EditBlock.AllSold;
            }
        }
    }
}
