using System;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Planning
{
    /// <summary>Who will carry the plan out — decides the floors and whether the tavern is offered.</summary>
    public enum PlanMode
    {
        /// <summary>The Party Steward window: the player looks, edits and clicks Do it (the normal floors).</summary>
        Window,

        /// <summary>The Full-autonomous steward (DESIGN §6): carried out on arrival with nobody looking — the floors
        /// rise to <see cref="StewardSettings.AutonomousMinGold"/> and the tavern is never offered.</summary>
        Autonomous,
    }

    /// <summary>
    /// The purse floors a plan answers to (DESIGN §3): <see cref="All"/> for every purchase (food included),
    /// <see cref="Animals"/> for animal purchases. While autonomous both rise to
    /// <see cref="StewardSettings.AutonomousMinGold"/> — max(MinGoldAfterDeal, AutonomousMinGold) and
    /// max(MinGoldForHorses, AutonomousMinGold) — so the rich player's autonomy can never drain the chest below it
    /// (DESIGN §6). Every other limit (price book, multipliers, role caps, targets) applies as usual.
    /// </summary>
    public sealed class MoneyFloors
    {
        public MoneyFloors(int all, int animals)
        {
            All = Math.Max(0, all);
            Animals = Math.Max(0, animals);
        }

        /// <summary>No purchase takes the purse below this (MinGoldAfterDeal, raised while autonomous).</summary>
        public int All { get; }

        /// <summary>No animal purchase takes the purse below this (MinGoldForHorses, raised while autonomous). An
        /// animal answers to both floors: the planner stops at max(All, Animals).</summary>
        public int Animals { get; }

        /// <summary>The floors in effect for <paramref name="mode"/>.</summary>
        public static MoneyFloors For(StewardSettings settings, PlanMode mode)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (mode != PlanMode.Autonomous)
                return new MoneyFloors(settings.MinGoldAfterDeal, settings.MinGoldForHorses);
            int autonomous = settings.AutonomousMinGold;
            return new MoneyFloors(Math.Max(settings.MinGoldAfterDeal, autonomous), Math.Max(settings.MinGoldForHorses, autonomous));
        }

        /// <summary>
        /// The floors the player's manual goals answer to (DESIGN §1.1 "THE GOAL", round 5): with <c>ManualGoalsKeepPurseFloor</c>
        /// (default on) the plan's own — food at <see cref="All"/>, animals at the higher of the two; off, none — except while
        /// autonomous, when <see cref="StewardSettings.AutonomousMinGold"/> always holds ("the rich player's autonomy never drains
        /// the chest"). Null = no floor.
        /// </summary>
        public static (int? Food, int? Animals) ForGoals(StewardSettings settings, PlanMode mode)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (settings.ManualGoalsKeepPurseFloor)
            {
                var floors = For(settings, mode);
                return (floors.All, Math.Max(floors.All, floors.Animals));
            }
            if (mode == PlanMode.Autonomous)
            {
                int autonomous = Math.Max(0, settings.AutonomousMinGold);
                return (autonomous, autonomous);
            }
            return (null, null);
        }
    }
}
