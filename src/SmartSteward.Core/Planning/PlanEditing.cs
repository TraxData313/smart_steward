using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Settings;

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

        /// <summary>Nothing the row may sell (none held, all guarded by a lock — <see cref="LockRule"/> —, or not Sell-ticked).</summary>
        NothingToSell,

        /// <summary>Every unit the row may sell is already in the plan (units a lock guards never are).</summary>
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

        /// <summary>The clan's companion limit is reached. (The party size limit never blocks — round 3: the footer shows
        /// the party after the deal against it.)</summary>
        CompanionLimit,

        /// <summary>Only donation is possible here and the dungeon has no more room.</summary>
        DungeonFull,

        /// <summary>A troop row's [+]: no notable here offers this troop to the player (step 16).</summary>
        NotOnOfferHere,

        /// <summary>A troop row's [−]: none of these men in the party that may be dismissed (step 16).</summary>
        NoneToDismiss,

        /// <summary>A troop row's [−]: every man of the type is already dismissed in the plan (step 16).</summary>
        AllDismissed,

        /// <summary>The Your troops line's [+]: nobody is dropped, so nobody comes back (step 20).</summary>
        NothingDropped,

        /// <summary>The Recruits line's [−]: no recruit is queued to give back (step 20).</summary>
        NothingRecruited,

        /// <summary>A troop row under the Recruits line: men of this type are being dismissed under Your troops — each line
        /// moves only its own side of a row (step 21, the window's view of mockup choice 8).</summary>
        DismissingThisType,

        /// <summary>A troop row under the Your troops line: men of this type are being recruited under Recruits (step 21).</summary>
        RecruitingThisType,

        /// <summary>A goal row's [+] (round 5): the next one would take the purse below the floor your goals keep
        /// (ManualGoalsKeepPurseFloor — MinGoldAfterDeal for food, the animal floor for horses; AutonomousMinGold while autonomous).</summary>
        PurseFloor,

        /// <summary>A goal row (round 5): its job waits for its activation threshold and your goals wait with it
        /// (ManualGoalsWaitForThresholds) — a typed goal is still kept.</summary>
        WaitsForThreshold,
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

        /// <summary>A goal row (round 5): its goal after the edit — null when the row follows the policy (after ⟲).</summary>
        public int? Goal { get; internal set; }

        /// <summary>A goal row: why its Result stops short of the goal (a typed goal the market cannot reach).</summary>
        public GoalShort GoalShort { get; internal set; }
    }

    /// <summary>
    /// The player's hand on the plan (DESIGN §1.1, PLAN step 4b) — the window only binds buttons to these and
    /// never does arithmetic of its own.
    /// </summary>
    /// <remarks>
    /// Every edit changes ONE row's quantity and puts the player's hand on it (<see cref="PlanRow.IsTouched"/>); the whole
    /// plan is walked again (<see cref="PlanReplay"/>) so every price, total and flag that depends on it refreshes — town
    /// prices walk per item category, the market's gold and stock are shared.
    /// <para>THE LIVE RE-PLAN (Anton 2026.09.28, step 15 — "recalculate the food and mounts as I add more troops or remove,
    /// so when I hit Do it I won't see new suggestions"): an edit of a row that changes the PARTY after the deal (a hire, a
    /// prisoner kept or ransomed — <see cref="PartyAfter.ChangesParty"/>) plans the steward's rows again for that party:
    /// every UNTOUCHED item row (food, pack animals, riding mounts, upgrade horses, armour &amp; weapons) takes what the
    /// planners make of it now, with the touched rows pinned and walked first (<see cref="PlanPins"/>). Any other edit keeps
    /// every other row's quantity (predictable beats clever — the step-4b rule, still true for them). ⟲ hands a row back to
    /// the steward and "Reset all" hands every row back — both re-plan (with nothing touched, that is exactly the first
    /// plan).</para>
    /// Rules [decided: Claude, 2026.09.27 — step 4b]:
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
        private readonly List<GoalEdit> _goalEdits = new List<GoalEdit>();
        private IReadOnlyList<PlanTransaction>? _transactions;

        // ── The goals (round 5, DESIGN §1.1 "THE GOAL") ───────────────────────────────────────────────────────

        /// <summary>
        /// A goal typed for a food or pack / riding / war row: kept as typed (clamped to 0 … <see cref="ManualGoals.MaxGoal"/>)
        /// even when the market cannot reach it — the Result then stops short and says why (<see cref="EditResult.GoalShort"/>).
        /// The steward re-plans every row it owns around it (the policy fills the rest); the edit waits in
        /// <see cref="TakeGoalEdits"/> for the window to save. Throws for a row that takes no goal.
        /// </summary>
        public EditResult SetGoal(string rowId, int goal)
        {
            var row = RowOrThrow(rowId);
            if (!row.TakesGoal)
                throw new ArgumentException("Row '" + rowId + "' takes no goal.", nameof(rowId));
            int before = row.Change;
            int value = ManualGoals.Clamp(goal);
            if (_inputs == null)
                return new EditResult(row.Id, before, value - row.Mine, before, EditBlock.None);
            StoreGoal(row, value);
            var now = FindRow(rowId) ?? row;
            return new EditResult(row.Id, before, value - now.Mine, now.Change, EditBlock.None)
            {
                Goal = now.ManualGoal,
                GoalShort = now.Result == value ? GoalShort.None : now.GoalShort,
            };
        }

        /// <summary>The goal edits made since the last call (a click, a typed goal, a ⟲ on a goal row), oldest first — the window
        /// saves them (<c>SettingsService.SaveQuietly</c>, no second re-plan) and they are forgotten here.</summary>
        public IReadOnlyList<GoalEdit> TakeGoalEdits()
        {
            var edits = _goalEdits.ToList();
            _goalEdits.Clear();
            return edits;
        }

        /// <summary>The plan's own copy of the standing goals (row id → goal), as the planner reads them.</summary>
        public IReadOnlyDictionary<string, int> Goals =>
            _inputs?.Goals ?? (IReadOnlyDictionary<string, int>)new Dictionary<string, int>();

        /// <summary>Keeps a goal in the plan's copy, queues it for saving, and re-plans with it.</summary>
        private void StoreGoal(PlanRow row, int? goal)
        {
            var goals = _inputs!.Goals;
            bool had = goals.TryGetValue(row.Id, out int old);
            if (goal == null ? had : !had || old != goal.Value)
            {
                if (goal == null) goals.Remove(row.Id);
                else goals[row.Id] = goal.Value;
                _goalEdits.Add(new GoalEdit(row.Id, goal));
            }
            row.IsTouched = goal != null;
            Replan();
        }

        /// <summary>The player moved at least one row away from the steward's suggestion.</summary>
        public bool IsEdited => Rows.Any(r => r.IsEdited);

        /// <summary>[+] on a row: buy / hire more, or sell less. Throws for an unknown row id.</summary>
        public EditResult Increase(string rowId, EditSize size = EditSize.One) => Step(RowOrThrow(rowId), +1, size);

        /// <summary>[−] on a row: sell / ransom more, or buy less.</summary>
        public EditResult Decrease(string rowId, EditSize size = EditSize.One) => Step(RowOrThrow(rowId), -1, size);

        /// <summary>⟲ on a row: the player's hand leaves it — the steward plans it again (a re-plan, with the other touched
        /// rows pinned: as far as what they took allows). <see cref="EditResult.Asked"/> is the steward's last suggestion for
        /// the row; the block says why it could not get back there.</summary>
        public EditResult Reset(string rowId)
        {
            var row = RowOrThrow(rowId);
            int before = row.Change, asked = row.SuggestedChange;
            if (_inputs == null)
                return new EditResult(row.Id, before, asked, before, EditBlock.None);
            if (row.TakesGoal)
                StoreGoal(row, null); // round 5: ⟲ gives the row back to the Instructions policy — the goal is gone
            else
            {
                row.IsTouched = false;
                Replan();
            }
            var now = FindRow(rowId); // an upgrade row nobody needs any more is gone after the re-plan
            int after = now?.Change ?? 0;
            var block = now == null || after == asked ? EditBlock.None : BlockOf(now, Math.Sign(asked - after));
            return new EditResult(row.Id, before, asked, after, block);
        }

        /// <summary>
        /// Moves a row toward <paramref name="value"/> as far as the walk allows — clamped to the row's range, never
        /// taking what another row has, crossing zero through 0 — and puts the player's hand on it. A click uses
        /// <see cref="Increase"/> / <see cref="Decrease"/>.
        /// </summary>
        public EditResult SetChange(string rowId, int value)
        {
            var row = RowOrThrow(rowId);
            return MoveTo(row, value, Math.Sign(value - row.Change));
        }

        /// <summary>Every row handed back to the steward — exactly the plan as it was made (a re-plan with nothing touched). The
        /// standing goals stay (round 5, [Claude's call]): they are settings like a typed price, each goes by its own ⟲.</summary>
        public void ResetAll()
        {
            if (_inputs == null)
                return;
            foreach (var row in Rows)
                row.IsTouched = false;
            ClearTroopLines();
            Replan();
        }

        /// <summary>
        /// Puts the player's hand back on rows of a NEW plan (a settings re-plan, <see cref="PlanCarryOver"/>): each found
        /// row is set to its quantity (clamped to the row's range) and touched, then the steward plans its own rows around
        /// them in one re-plan — the touched rows walk first, so what the new limits no longer allow is cut. Returns how
        /// many rows were found.
        /// </summary>
        public int Restore(IEnumerable<KeyValuePair<string, int>> touched)
        {
            if (touched == null) throw new ArgumentNullException(nameof(touched));
            int found = 0;
            foreach (var edit in touched)
            {
                var row = FindRow(edit.Key);
                if (row == null || row.TakesGoal) // a goal row's hand is its goal, which the settings carry (round 5)
                    continue;
                var (min, max) = Bounds(row);
                row.Change = Math.Max(min, Math.Min(max, edit.Value));
                row.IsTouched = true;
                found++;
            }
            if (found > 0 && _inputs != null)
                Replan();
            return found;
        }

        /// <summary>
        /// The executor's list (DESIGN §5, PLAN step 6) for the plan as it stands: donations and ransoms first
        /// (the ransom funds the buys), then the dismissals (step 16), then every sale, then every purchase — in the walk's
        /// order, grouped by item category (which never changes a price: a town's price walks per category) —, then the
        /// hires, wanderers before mercenaries, then the recruits (step 16). Each with the stack / troop / hero, the count
        /// and the expected prices.
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
            if (row.TakesGoal && GoalWaits(row))
                block = EditBlock.WaitsForThreshold; // round 5: your goals wait with the steward — nothing would move
            else if (current * direction < 0)
                block = EditBlock.None; // toward zero always works
            else
            {
                var (min, max) = Bounds(row);
                int next = current + direction;
                block = next < min || next > max ? StaticBlock(row, direction)
                    : row.Type == RowType.Troop ? TroopBlock(row, direction)
                    : Trial(row, next);
            }
            _blocks[(row, direction)] = block;
            return block;
        }

        /// <summary>
        /// A troop row's live block within its range, without a trial walk (a big party lists dozens of troop types, and the
        /// window asks every row after every click). Exact, because troop rows come last in the walk and touch no market:
        /// one more dismissal needs nothing (free, no stock); one more recruit only needs the purse — the trial's own rule
        /// (<see cref="Unaffordable"/>) on the walk as it stands, the price further on. <c>PlanEditingTests</c> hold it to
        /// <see cref="Trial"/>.
        /// </summary>
        private EditBlock TroopBlock(PlanRow row, int direction)
        {
            if (direction < 0)
                return EditBlock.None;
            int giveWay = 0;
            foreach (var other in Rows)
            {
                if (other.WalksFirst || ReferenceEquals(other, row) || PartyAfter.ChangesParty(other))
                    continue;
                foreach (var tally in other.Tallies)
                    if (tally.Direction == Pricing.TradeDirection.Buy)
                        giveWay += tally.Gold;
            }
            int goldAfter = Totals.GoldAfter - (row.Troop?.UnitPrice ?? 0);
            return goldAfter + giveWay < 0 || (Totals.HireUnaffordable && giveWay == 0)
                ? EditBlock.NotEnoughGold
                : EditBlock.None;
        }

        /// <summary>A goal of this row would wait for its job's threshold (ManualGoalsWaitForThresholds and the job waiting).</summary>
        private bool GoalWaits(PlanRow row) =>
            _inputs!.Settings.ManualGoalsWaitForThresholds && row.StartsAtDenari != null;

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
            bool couldAfford = !Totals.CannotAfford;
            if (_inputs != null && asked != before)
            {
                var (min, max) = Bounds(row);
                int target = Math.Max(min, Math.Min(max, asked));
                if (before != 0 && Math.Sign(target) == -Math.Sign(before))
                    Lower(row, 0); // crossing zero (SetChange): first back to zero
                if (target == 0 || (Math.Sign(target) == Math.Sign(row.Change) && Math.Abs(target) <= Math.Abs(row.Change)))
                    Lower(row, target);
                else
                    Raise(row, target);
                if (PartyAfter.IsHireRow(row) && row.Change > before && couldAfford && Totals.CannotAfford)
                    FitPurse(row, before);
            }
            int after = row.Change;
            var block = after == asked || direction == 0 ? EditBlock.None : BlockOf(row, Math.Sign(asked - after));
            return new EditResult(row.Id, before, asked, after, block)
            {
                Goal = row.ManualGoal,
                GoalShort = row.ManualGoal == null || row.Result == row.ManualGoal ? GoalShort.None : row.GoalShort,
            };
        }

        /// <summary>Toward zero: always done.</summary>
        private void Lower(PlanRow row, int value)
        {
            if (row.Change == value)
                return;
            row.Change = value;
            Commit(row);
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
            Commit(row);
        }

        /// <summary>The row moved: the player's hand is on it now. A row that changes the party after the deal re-plans the
        /// steward's rows for that party (the live re-plan); any other edit is walked with every other row as it is.</summary>
        private void Commit(PlanRow row)
        {
            if (row.TakesGoal)
            {
                // Round 5: a click on a food or pack / riding / war row IS a goal edit — the new Result becomes the goal, and
                // the steward re-plans its rows around it (the policy fills the rest). Should the re-plan land short of it (the
                // trial's picture of the market is the plan as it stood), the goal follows the Result it got.
                int goal = Math.Max(0, row.Result);
                StoreGoal(row, goal);
                var now = FindRow(row.Id);
                if (now != null && now.Result != goal && now.ManualGoal == goal && !now.GoalWaits)
                    StoreGoal(now, Math.Max(0, now.Result));
                return;
            }
            row.IsTouched = true;
            if (PartyAfter.ChangesParty(row))
                Replan();
            else
                Settle();
        }

        /// <summary>
        /// A hire the purse turned out not to pay once the steward re-planned (its own sales shrink with a bigger party — more
        /// mouths, less food surplus to sell): step back to the most hires the re-planned deal pays. Rare; each step is a
        /// re-plan.
        /// </summary>
        private void FitPurse(PlanRow row, int before)
        {
            int lo = before, hi = row.Change; // lo pays (the deal could afford it), hi does not
            while (hi - lo > 1)
            {
                int mid = lo + (hi - lo) / 2;
                row.Change = mid;
                Replan();
                if (Totals.CannotAfford) hi = mid;
                else lo = mid;
            }
            if (row.Change != lo)
            {
                row.Change = lo;
                Replan();
            }
        }

        /// <summary>
        /// Plans the steward's rows again for the plan as it stands (the live re-plan, step 15): the planner runs on the same
        /// snapshot, settings and price cache with every touched row pinned (<see cref="PlanPins"/>) — the party rows define
        /// the party after the deal, the touched rows walk first —, the rows adopt what it made of them, and the walk settles
        /// the prices, totals and transactions.
        /// </summary>
        private void Replan()
        {
            if (_inputs == null)
                return;
            var planned = StewardPlanner.Plan(_inputs.Snapshot, _inputs.Settings, _inputs.Oracle, _inputs.Mode,
                PlanPins.From(Rows, new Pricing.MarketState(_inputs.Oracle, _inputs.Snapshot.MarketGold)), _inputs.Goals);
            Adopt(planned);
            Settle();
        }

        /// <summary>Would the plan work with this row at <paramref name="value"/>? None = yes; else why not. The row is
        /// walked as touched — it will be, once moved.</summary>
        internal EditBlock Trial(PlanRow row, int value)
        {
            var outcome = Walk(r => ReferenceEquals(r, row) ? value : r.Change, r => r.WalksFirst || ReferenceEquals(r, row));
            var own = outcome.Of(row);
            if (own.IsShort)
                return own.Short == EditBlock.None ? EditBlock.NeededByAnotherRow : own.Short;
            // A goal row's edit re-plans the steward's rows (round 5): they give way — only the player's other rows count.
            foreach (var other in outcome.Rows)
                if (other.IsShort && (!row.TakesGoal || other.Row.WalksFirst))
                    return SharedLimit(other.Short) ? other.Short : EditBlock.NeededByAnotherRow;
            if (value > 0 && Unaffordable(outcome, row))
                return EditBlock.NotEnoughGold;
            return EditBlock.None;
        }

        /// <summary>
        /// The purse cannot pay the plan with this row moved. A row that changes the party re-plans the steward's rows, whose
        /// purchases give way to the player's hand (the pinned rows' gold comes first, <see cref="PlanPins"/>) — so for it
        /// only the player's own spending counts; <see cref="FitPurse"/> catches the rare rest.
        /// </summary>
        private static bool Unaffordable(WalkOutcome outcome, PlanRow row)
        {
            if (!PartyAfter.ChangesParty(row) && !row.TakesGoal)
                return outcome.GoldAfter < 0 || outcome.HireUnaffordable;
            int giveWay = 0;
            foreach (var o in outcome.Rows)
            {
                if (o.Row.WalksFirst || ReferenceEquals(o.Row, row) || PartyAfter.ChangesParty(o.Row))
                    continue;
                foreach (var tally in o.Book.Tallies)
                    if (tally.Direction == Pricing.TradeDirection.Buy)
                        giveWay += tally.Gold;
            }
            return outcome.GoldAfter + giveWay < 0 || (outcome.HireUnaffordable && giveWay == 0);
        }

        /// <summary>A limit every row shares, worth naming when the edit would push ANOTHER row past it; the
        /// rest (stock, a category's price room) reads "another row needs it".</summary>
        private static bool SharedLimit(EditBlock block) =>
            block == EditBlock.MarketOutOfGold || block == EditBlock.CompanionLimit || block == EditBlock.DungeonFull;

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

        private WalkOutcome Walk(Func<PlanRow, int> changeOf, Func<PlanRow, bool>? touchedOf = null) =>
            PlanReplay.Walk(_inputs!, Rows.ToList(), changeOf, touchedOf);

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
                    case RowType.Troop:
                        row.Change = o.Realized;
                        TroopPlanner.ApplyGold(row);
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

        /// <summary>The row's static range: food and the mount role rows buy and sell (the noble and lame horse rows only sell —
        /// they have nothing to buy, MaxBuy 0); loot and prisoners only sell; tavern rows only hire; troop rows recruit (up to
        /// what is on offer) and dismiss (down to the men held).</summary>
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
                        return row.MaxBuy == 0 && row.Tavern!.Block == HireBlock.CompanionLimit
                            ? EditBlock.CompanionLimit
                            : EditBlock.AllOnOffer;
                    case RowType.Troop:
                        return row.MaxBuy == 0 ? EditBlock.NotOnOfferHere : EditBlock.AllOnOffer;
                    default:
                        // Noble and lame horses are only ever sold (step 17).
                        if (row.Role == MountRole.Noble || row.Role == MountRole.Lame)
                            return EditBlock.SellOnly;
                        return row.MaxBuy == 0 ? EditBlock.NoneEligible : EditBlock.AllOnOffer;
                }
            }
            switch (row.Type)
            {
                case RowType.Tavern:
                    return EditBlock.BuyOnly;
                case RowType.Troop:
                    return row.MaxSell == 0 ? EditBlock.NoneToDismiss : EditBlock.AllDismissed;
                case RowType.Prisoner:
                    return !_inputs!.Ransom && row.MaxSell < row.Mine ? EditBlock.DungeonFull : EditBlock.AllSold;
                default:
                    return row.MaxSell == 0 ? EditBlock.NothingToSell : EditBlock.AllSold;
            }
        }
    }
}
