using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The steward: snapshot + settings + the game's prices → a <see cref="StewardPlan"/>. Pure and
    /// deterministic (every loop runs in a fixed order, ties broken by ordinal ids).
    /// </summary>
    /// <remarks>
    /// The canonical walk — the order units move in, so every price is the true marginal price (DESIGN §3,
    /// §4.1). The plan editor (<see cref="PlanReplay"/>) walks the rows' lanes in this same order, with the
    /// same picking rules (<see cref="PlanWalk"/>) — change one, change both:
    /// <list type="number">
    /// <item>Prisoners — ransom gold (paid by the game, not the market) and donations.</item>
    /// <item>SELL: food surplus (most-held type first), the animals (lame horses, pack surplus, noble horses, war surplus,
    ///   riding surplus — <see cref="PlanReplay.AnimalSellRank"/>), loot (all groups interleaved by SellLootOrder) — each unit
    ///   only if the market can still pay for it.</item>
    /// <item>BUY in priority order: food (under MinGoldAfterDeal), pack animals, riding mounts, war horses
    ///   (under both floors). No kind is both sold and bought in one visit — except the lame horses the healthy ones
    ///   replace (step 17, ReplaceLameHorses).</item>
    /// <item>Tavern — rows at 0, outside the chain; never in an autonomous plan (DESIGN §6).</item>
    /// <item>Troops (step 16) — recruit and dismiss rows at 0, outside the chain like the tavern; never autonomous.</item>
    /// </list>
    /// <see cref="PlanMode.Autonomous"/> raises the floors to AutonomousMinGold (<see cref="MoneyFloors"/>).
    /// <para>Step 17: when the steward buys war horses while the riding row has a surplus, the plan is made ONCE MORE with
    /// those war horses pledged (<see cref="MountPlanner.PledgeHint"/>), so the riding surplus is sold against the war horses
    /// the party will have after the deal — Anton's "100 riding + 10 war", reached in one visit.</para>
    /// <para>The live re-plan (step 15, DESIGN §1.1): <see cref="StewardPlan"/> plans again with the rows the player's hand
    /// is on PINNED (<see cref="PlanPins"/>) — the hires and prisoners define the party after the deal
    /// (<see cref="PartyAfter"/>), and in every phase above the pinned rows walk first, then the steward plans its own rows
    /// with what they left, by the same rules. With nothing pinned it is exactly the plan above.</para>
    /// </remarks>
    public static class StewardPlanner
    {
        public static StewardPlan Plan(StewardSnapshot snapshot, StewardSettings settings, IPriceOracle oracle,
            PlanMode mode = PlanMode.Window) => Plan(snapshot, settings, oracle, mode, PlanPins.None);

        internal static StewardPlan Plan(StewardSnapshot snapshot, StewardSettings settings, IPriceOracle oracle,
            PlanMode mode, PlanPins pins)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (oracle == null) throw new ArgumentNullException(nameof(oracle));
            if (snapshot.Party == null) snapshot.Party = new PartyInfo();

            // One price cache for the planner AND the editor that re-walks the plan after every click: a plan's
            // prices are a snapshot (nothing trades while it is planned or edited), and the game's price model is
            // not free — the same quotes come up again and again (PLAN step 9, review area 5). A re-plan reuses the
            // plan's own cache.
            var cache = oracle as CachingPriceOracle ?? new CachingPriceOracle(oracle);
            var plan = PlanOnce(snapshot, settings, cache, mode, pins, 0, out int pledge);
            return pledge > 0 ? PlanOnce(snapshot, settings, cache, mode, pins, pledge, out _) : plan;
        }

        private static StewardPlan PlanOnce(StewardSnapshot snapshot, StewardSettings settings, CachingPriceOracle cache,
            PlanMode mode, PlanPins pins, int warPledge, out int pledgeHint)
        {
            pledgeHint = 0;
            var ctx = new PlanContext(snapshot, settings, cache, mode, pins, warPledge);
            var facts = new PlanFacts();
            if (!settings.ModEnabled)
                return Assemble(ctx, facts, new List<PlanRow>(), (a, b) => 0);

            // 0. The tavern: offered, never proposed (every row at 0 — or at the player's number in a re-plan) - and not
            //    even offered to the autonomous steward, which never hires (DESIGN §6). Its hires are the party after the
            //    deal the steward feeds and mounts; they are paid after the trades, but the steward's buys leave their gold.
            var tavernRows = mode == PlanMode.Autonomous ? new List<PlanRow>() : TavernPlanner.Plan(ctx);
            //    The troops (step 16) the same way: recruits and dismissals only by the player's hand, never autonomous.
            var troopRows = mode == PlanMode.Autonomous ? new List<PlanRow>() : TroopPlanner.Plan(ctx);
            ctx.Party = PartyAfter.Of(snapshot, PartyAfter.MovesOf(tavernRows.Concat(troopRows)));

            // 1. Prisoners first: their gold funds the buys, and the food target counts only those who stay.
            var prisonerRows = PrisonerPlanner.Plan(ctx, out int prisonersAfter);

            var food = new FoodPlanner(ctx, prisonersAfter);
            var lame = new LameHorsePlanner(ctx);
            var pack = new PackAnimalPlanner(ctx, lame);
            var mounts = new MountPlanner(ctx, lame);
            var loot = new LootPlanner(ctx);

            // 2. Sell first — the proceeds fund the buys. In every phase the player's pinned rows go first; the animals in
            //    the rank order lame · pack · noble · war · riding (PlanReplay.AnimalSellRank).
            food.PlanSells();
            lame.PlanPinnedSells();
            pack.PlanPinnedSells();
            mounts.PlanPinnedSells();
            lame.PlanSells();
            pack.PlanSells();
            mounts.PlanSells();
            loot.PlanSells();

            // 3. Buy in priority order under the floors.
            food.PlanBuys();
            pack.PlanPinnedBuys();
            mounts.PlanPinnedBuys();
            pack.PlanBuys();
            mounts.PlanBuys();

            food.Finish();
            lame.Finish();
            pack.Finish();
            mounts.Finish();
            loot.Finish();
            pledgeHint = mounts.PledgeHint;

            facts.FoodEaters = food.Eaters;
            facts.FoodTarget = food.Target;
            facts.FoodSellAbove = food.SellAbove;
            facts.PackTarget = pack.Target;
            facts.Footmen = mounts.Footmen;
            facts.MountTarget = mounts.MountTarget;
            facts.RidingTarget = mounts.RidingTarget;
            facts.WarTarget = mounts.WarTarget;
            facts.Waiting = Waiting(ctx);

            var rows = new List<PlanRow>();
            rows.AddRange(tavernRows);
            rows.AddRange(troopRows);
            rows.AddRange(food.Rows);
            if (pack.Row != null) rows.Add(pack.Row);
            rows.AddRange(mounts.Rows);
            if (lame.Row != null) rows.Add(lame.Row);
            rows.AddRange(loot.Rows);
            rows.AddRange(prisonerRows);
            return Assemble(ctx, facts, rows, loot.SellOrder);
        }

        /// <summary>The switched-on jobs the purse before the deal does not switch on yet (round 4).</summary>
        private static IReadOnlyList<KeyValuePair<ManagedJob, int>> Waiting(PlanContext ctx)
        {
            var s = ctx.Settings;
            var waiting = new List<KeyValuePair<ManagedJob, int>>();
            foreach (var job in JobThresholds.All)
            {
                bool enabled = job == ManagedJob.Food ? s.FoodEnabled
                    : job == ManagedJob.PackAnimals ? s.PackAnimalsEnabled
                    : job == ManagedJob.Mounts ? s.MountsEnabled
                    : s.WarMountsEnabled;
                if (enabled && !ctx.JobActive(job))
                    waiting.Add(new KeyValuePair<ManagedJob, int>(job, JobThresholds.Of(job, s)));
            }
            return waiting;
        }

        private static StewardPlan Assemble(PlanContext ctx, PlanFacts facts, List<PlanRow> rows,
            Comparison<ItemStack> lootOrder)
        {
            var sections = new List<PlanSection>();
            foreach (PlanSectionKind kind in Enum.GetValues(typeof(PlanSectionKind)))
            {
                var inSection = rows.Where(r => r.Section == kind).ToList();
                if (inSection.Count > 0)
                    sections.Add(new PlanSection(kind, inSection));
            }
            var totals = PlanTotals.Compute(rows, ctx.Snapshot, ctx.Floors);
            var inputs = new PlanInputs(ctx.Snapshot, ctx.Settings, ctx.Mode, ctx.Floors, ctx.Oracle,
                ctx.Settings.FoodStrategy == FoodStrategy.Balanced, lootOrder,
                PrisonerPlanner.CanRansom(ctx), PrisonerPlanner.CanDonate(ctx), PrisonerPlanner.DungeonRoom(ctx));
            return new StewardPlan(sections, totals, facts, inputs);
        }
    }
}
