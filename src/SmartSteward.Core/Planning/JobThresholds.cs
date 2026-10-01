using System;
using SmartSteward.Core.Settings;

namespace SmartSteward.Core.Planning
{
    /// <summary>The steward's jobs that switch on with the purse (Anton 2026.09.28, playtest round 4).</summary>
    public enum ManagedJob
    {
        /// <summary>Food — <see cref="StewardSettings.FoodMinDenari"/>.</summary>
        Food,

        /// <summary>Pack animals (and their lame ones) — <see cref="StewardSettings.PackAnimalsMinDenari"/>.</summary>
        PackAnimals,

        /// <summary>Riding horses, the noble horses sold and the lame riding horses replaced — <see cref="StewardSettings.MountsMinDenari"/>.</summary>
        Mounts,

        /// <summary>War horses (and their lame ones) — <see cref="StewardSettings.WarHorsesMinDenari"/>.</summary>
        WarHorses,

        /// <summary>The noble horses KEPT (and their lame ones) — <see cref="StewardSettings.NobleHorsesMinDenari"/> (step 28, Anton
        /// 2026.10.01). Only while noble horses are kept (<see cref="StewardSettings.NobleHorsesToKeep"/> &gt; 0, or a goal of
        /// yours); with none kept the noble horses are sold with the riding horses (<see cref="Mounts"/>), as since step 17.</summary>
        NobleHorses,
    }

    /// <summary>
    /// The activation thresholds (DESIGN §3 — Anton 2026.09.28, playtest round 4: "i want it in such a way that the players turn
    /// it on and with the default settings they will be happy like that, as they become richer those start activating and
    /// helping them"). A job ACTS only when the purse at planning — before this visit's deal, the snapshot's gold — is at least
    /// its threshold; below it the steward's own side of the job does nothing: no buy, no sell. Its rows stay in the table
    /// (the player may still buy or sell by hand) with <see cref="PlanRow.StartsAtDenari"/> set, and the plan's facts list the
    /// waiting jobs for the section's overview ("starts at 20,000 denari").
    /// </summary>
    /// <remarks>
    /// [decided: Claude, 2026.09.28 — step 20] How they relate to the floors: the thresholds only SWITCH a job on; the money
    /// floors (MinGoldAfterDeal, MinGoldForHorses — raised to AutonomousMinGold while autonomous) still CAP what an active job
    /// spends. So with 2,500 denari and the defaults, food is on and may spend down to 1,000; pack animals are on but may not
    /// buy (the 5,000 animal floor) — they may still sell a surplus; riding and war horses wait. The purse before the deal is
    /// the test, so a sale in this visit never switches a job on halfway through a plan, and a live re-plan (same snapshot)
    /// keeps the same jobs on. 0 = always on.
    /// </remarks>
    public static class JobThresholds
    {
        /// <summary>Every job in the table's order.</summary>
        public static ManagedJob[] All { get; } =
            { ManagedJob.Food, ManagedJob.PackAnimals, ManagedJob.Mounts, ManagedJob.WarHorses, ManagedJob.NobleHorses };

        /// <summary>The purse a job needs before the deal to act (0 = always).</summary>
        public static int Of(ManagedJob job, StewardSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            switch (job)
            {
                case ManagedJob.Food: return Math.Max(0, settings.FoodMinDenari);
                case ManagedJob.PackAnimals: return Math.Max(0, settings.PackAnimalsMinDenari);
                case ManagedJob.Mounts: return Math.Max(0, settings.MountsMinDenari);
                case ManagedJob.NobleHorses: return Math.Max(0, settings.NobleHorsesMinDenari);
                default: return Math.Max(0, settings.WarHorsesMinDenari);
            }
        }

        /// <summary>Does the job act with <paramref name="purseBeforeDeal"/> denari?</summary>
        public static bool IsActive(ManagedJob job, StewardSettings settings, int purseBeforeDeal) =>
            purseBeforeDeal >= Of(job, settings);
    }
}
