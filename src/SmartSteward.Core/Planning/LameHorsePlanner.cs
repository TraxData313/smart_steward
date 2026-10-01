using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The "Lame horses" row (DESIGN §1.1.1, §2.2–§2.4 — Anton 2026.09.28, step 17: "the whole idea is to speed up the infantry,
    /// so if a lame horse gives the bonus — keep it; never buy them as they don't look good; add a button to sell and replace
    /// them with healthy ones, default ON"). With <c>ReplaceLameHorses</c> every held horse or pack animal with a BAD modifier
    /// (<see cref="ItemStack.HasBadModifier"/>: lame, old) is sold — sell only, the dearest first — and the pack, riding and war
    /// rows, which do not count it, buy healthy ones to keep their targets. That is the one exception to "never sell and buy
    /// the same kind in one visit" (DESIGN §2.2), and on purpose.
    /// </summary>
    /// <remarks>
    /// Which stacks: a bad modifier, not a noble horse (the noble row sells those anyway), its role managed (pack animals /
    /// riding horses / war horses switched on), not guarded by a lock (<see cref="LockRule"/>) and Sell-ticked in the price book.
    /// Everything else — the switch off, a guarded lock, an unticked item — stays in its role row, counted as held: a lame
    /// horse still carries a footman. A lame horse the market cannot take this visit (gold, min sell price) still counts
    /// toward its role's target (<see cref="LeftOf"/>) — its role row plans after this row sells. The row walks FIRST among the
    /// animal sales (<see cref="PlanReplay.AnimalSellRank"/>).
    /// </remarks>
    internal sealed class LameHorsePlanner
    {
        public const string RowId = "mounts:lame";

        private readonly PlanContext _ctx;
        private readonly List<ItemStack> _stacks = new List<ItemStack>();
        private readonly HashSet<string> _keys = new HashSet<string>(StringComparer.Ordinal);
        private readonly WalkLine? _sell;
        private readonly int? _pinned;

        public LameHorsePlanner(PlanContext ctx)
        {
            _ctx = ctx;
            var settings = ctx.Settings;
            if (!settings.ReplaceLameHorses || !ctx.Snapshot.CanTrade)
                return;
            foreach (var stack in ctx.Inventory(ItemKind.PackAnimal).Concat(ctx.Inventory(ItemKind.Mount)))
                if (Replaces(ctx, stack))
                {
                    _stacks.Add(stack);
                    _keys.Add(stack.Key);
                }
            if (_stacks.Count == 0)
                return;

            // Step 26: a lame horse a quest keeps (any modifier counts for the quest - the cheapest are kept first) is never sold;
            // unsold, it still counts toward its role (LeftOf).
            var sellLane = new TradeLane(TradeDirection.Sell, LanePick.MostExpensive,
                _stacks.Select(s => new LaneStack(s, ctx.Quests.Free(s), ctx.MinSellOf(s))));
            Row = new PlanRow(RowId, PlanSectionKind.Mounts, RowType.Mount)
            {
                Role = MountRole.Lame,
                Mine = _stacks.Sum(s => s.Count),
                // A lock that guards keeps the horse in its role row, so none here is guarded; a lock set after the plan is
                // honoured at the click when locks guard horses at all.
                LocksGuard = ctx.LockGuards(ItemKind.Mount),
                Market = null,
                MaxSell = sellLane.Capacity,
                SellLane = sellLane,
                Quest = ctx.Quests.ForStacks(_stacks),
            };
            _sell = new WalkLine(Row, sellLane, book: Row.Book);
            if (ctx.Pins.TryGet(Row.Id, out int pin))
                _pinned = pin;
        }

        /// <summary>The row; null when there is no bad horse to replace (or the switch is off).</summary>
        public PlanRow? Row { get; }

        /// <summary>Does the lame row hold this stack (its role rows then do not count it)?</summary>
        public bool Holds(ItemStack stack) => _keys.Contains(stack.Key);

        /// <summary>Units of a kind in this row the plan has NOT sold (so far) — they still count toward their role's target.</summary>
        public int LeftOf(ItemKind kind)
        {
            if (Row == null)
                return 0;
            int held = _stacks.Where(s => s.Kind == kind).Sum(s => s.Count);
            int sold = Row.Tallies.Where(t => t.Direction == TradeDirection.Sell && t.Stack.Kind == kind).Sum(t => t.Count);
            return Math.Max(0, held - sold);
        }

        /// <summary>Would the lame row take this held stack? (<see cref="LameHorsePlanner"/>'s remarks.)</summary>
        public static bool Replaces(PlanContext ctx, ItemStack stack)
        {
            var settings = ctx.Settings;
            if (!settings.ReplaceLameHorses || stack == null || !stack.HasBadModifier || MountGoal.IsNoble(stack))
                return false;
            // Round 4: only while the role's job acts (its threshold met) - else its role row, which buys nothing either,
            // keeps and counts it.
            bool managed = stack.Kind == ItemKind.PackAnimal ? settings.PackAnimalsEnabled && ctx.JobActive(ManagedJob.PackAnimals)
                : MountGoal.IsWar(stack, settings) ? settings.WarMountsEnabled && ctx.JobActive(ManagedJob.WarHorses)
                : stack.Kind == ItemKind.Mount && settings.MountsEnabled && ctx.JobActive(ManagedJob.Mounts);
            return managed && !ctx.IsGuarded(stack) && ctx.Book(stack)?.SellTicked == true;
        }

        /// <summary>The player's own number on this row (a touched row in a live re-plan) — first among the animal sales.</summary>
        public void PlanPinnedSells()
        {
            if (Row != null && _pinned < 0)
                PlanWalk.WalkLane(_ctx.Walk, new WalkLine(Row, Row.SellLane!, Row.Mine, -_pinned.Value, Row.Book), int.MaxValue,
                    () => _ctx.Market.MarketGoldLeft);
        }

        /// <summary>Every one of them, the dearest first, while the market can pay (and above its min sell price).</summary>
        public void PlanSells()
        {
            if (Row == null || _sell == null || _pinned != null)
                return;
            if (PlanWalk.WalkLane(_ctx.Walk, _sell, Row.Mine, _ctx.AnimalSellCeiling) < Row.Mine) // the goal is 0 (round 5)
                Row.GoalShort = GoalReasons.Now(_sell.Cursor, _ctx.Market, _ctx.AnimalSellCeiling());
        }

        public void Finish()
        {
            if (Row != null)
                PlanMath.FinishItemRow(Row, _stacks.Select(s => new KeyValuePair<ItemStack, int>(s, s.Count)));
        }
    }
}
