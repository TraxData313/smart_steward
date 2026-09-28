using System;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// The food goal in DAYS (Anton 2026.09.28: "you are showing the food as days to last very nicely, it is better than my
    /// idea food barrels/soldier — make the instruction for the steward as DAYS to last as the goal, and in brackets show the
    /// food barrels per soldier that requires"). The steward keeps <c>FoodDays</c> days of food for the party AFTER the deal,
    /// at the game's own rate: what the party eats a day now (<c>−MobileParty.FoodChange</c>, perks included) per eater, where
    /// the game counts a prisoner as half an eater (RESEARCH §2), times the eaters the deal leaves (DESIGN §2.1).
    /// </summary>
    public static class FoodGoal
    {
        /// <summary>Vanilla's rate: one food feeds 20 men for a day (<c>NumberOfMenOnMapToEatOneFood</c>, RESEARCH §2) —
        /// used when the game's own rate was not read.</summary>
        public const double VanillaPerEaterPerDay = 1.0 / 20.0;

        /// <summary>How many days vanilla's rate makes of the old "food per man" number (1 food lasts a man 20 days) — the
        /// one-time migration of an old settings file's <c>FoodPerMan</c>.</summary>
        public const int DaysPerFoodPerMan = 20;

        /// <summary>Food one eater eats a day, as the game counts it now: the party's daily use (perks included) over its
        /// eaters — members + prisoners / 2 (integer halves, at least 1). Vanilla's 1/20 when the daily use is unknown.</summary>
        public static double PerEaterPerDay(StewardSnapshot snapshot)
        {
            var party = snapshot?.Party ?? new PartyInfo();
            if (party.DailyFoodUse <= 0)
                return VanillaPerEaterPerDay;
            int prisoners = 0;
            foreach (var p in snapshot!.Prisoners ?? new System.Collections.Generic.List<PrisonerStack>())
                if (p != null && p.Count > 0)
                    prisoners += p.Count;
            return party.DailyFoodUse / PlanTotals.GameEaters(Math.Max(0, party.Members), prisoners);
        }

        /// <summary>The bracket beside the goal: the food units the goal keeps per soul — days × what one eater eats a day
        /// (40 days at vanilla's rate ≈ 2.0).</summary>
        public static double PerSoul(StewardSettings settings, StewardSnapshot snapshot) =>
            Math.Max(0, settings.FoodDays) * PerEaterPerDay(snapshot);

        /// <summary>The food target: ceil(days × what one eater eats a day × eaters) — slop-safe
        /// (<see cref="PlanMath.Ceiling"/>).</summary>
        public static int Target(int days, double perEaterPerDay, int eaters) =>
            PlanMath.Ceiling(Math.Round(Math.Max(0, days) * Math.Max(0, perEaterPerDay), 6) * Math.Max(0, eaters));
    }
}
