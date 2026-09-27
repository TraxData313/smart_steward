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
    /// <item>SELL: food surplus (most-held type first), pack surplus, riding surplus, loot (all groups
    ///   interleaved by SellLootOrder) — each unit only if the market can still pay for it.</item>
    /// <item>BUY in priority order: food (under MinGoldAfterDeal), pack animals, riding mounts, upgrade horses
    ///   (under both floors). No kind is both sold and bought in one visit.</item>
    /// <item>Tavern — rows at 0, outside the chain.</item>
    /// </list>
    /// </remarks>
    public static class StewardPlanner
    {
        public static StewardPlan Plan(StewardSnapshot snapshot, StewardSettings settings, IPriceOracle oracle)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (oracle == null) throw new ArgumentNullException(nameof(oracle));
            if (snapshot.Party == null) snapshot.Party = new PartyInfo();

            var ctx = new PlanContext(snapshot, settings, oracle);
            var facts = new PlanFacts();
            if (!settings.ModEnabled)
                return Assemble(ctx, facts, new List<PlanRow>(), (a, b) => 0);

            // 1. Prisoners first: their gold funds the buys, and the food target counts only those who stay.
            var prisonerRows = PrisonerPlanner.Plan(ctx, out int prisonersAfter);

            var food = new FoodPlanner(ctx, prisonersAfter);
            var pack = new PackAnimalPlanner(ctx);
            var mounts = new MountPlanner(ctx);
            var loot = new LootPlanner(ctx);

            // 2. Sell first — the proceeds fund the buys.
            food.PlanSells();
            pack.PlanSells();
            mounts.PlanSells();
            loot.PlanSells();

            // 3. Buy in priority order under the floors.
            food.PlanBuys();
            pack.PlanBuys();
            mounts.PlanBuys();

            // 4. The tavern: offered, never proposed.
            var tavernRows = TavernPlanner.Plan(ctx);

            food.Finish();
            pack.Finish();
            mounts.Finish();
            loot.Finish();

            facts.FoodEaters = food.Eaters;
            facts.FoodTarget = food.Target;
            facts.FoodSellAbove = food.SellAbove;
            facts.PackTarget = pack.Target;
            facts.Footmen = mounts.Footmen;
            facts.RidingTarget = mounts.RidingTarget;
            facts.RidingCounted = mounts.RidingCounted;
            facts.UpgradeNeed = mounts.UpgradeNeed;
            facts.UpgradeReserved = mounts.UpgradeReserved;

            var rows = new List<PlanRow>();
            rows.AddRange(tavernRows);
            rows.AddRange(food.Rows);
            if (pack.Row != null) rows.Add(pack.Row);
            rows.AddRange(mounts.Rows);
            rows.AddRange(loot.Rows);
            rows.AddRange(prisonerRows);
            return Assemble(ctx, facts, rows, loot.SellOrder);
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
            var totals = PlanTotals.Compute(rows, ctx.Snapshot, ctx.Settings);
            var inputs = new PlanInputs(ctx.Snapshot, ctx.Settings, ctx.Oracle,
                ctx.Settings.FoodStrategy == FoodStrategy.Balanced, lootOrder,
                PrisonerPlanner.CanRansom(ctx), PrisonerPlanner.CanDonate(ctx), PrisonerPlanner.DungeonRoom(ctx));
            return new StewardPlan(sections, totals, facts, inputs);
        }
    }
}
