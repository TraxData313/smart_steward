using System;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// How many horses the steward keeps for the footmen (DESIGN §2.3–§2.4) — Anton 2026.09.28, step 17: "don't worry about
    /// the mounts needing upgrades … the other soldiers just draw from my mounts … make it simpler for now". No troop's
    /// upgrade is counted any more:
    /// <list type="bullet">
    /// <item>Horses to keep <c>T = ceil(footmen × MountsPer100Footmen / 100)</c> — the footmen of the party after the deal.</item>
    /// <item>War horses to keep <c>W = WarMountsToKeep</c>, a plain number (the <c>war_horse</c> category only); they carry
    ///   footmen until an upgrade takes them, so they COUNT toward T.</item>
    /// <item>Riding horses fill the rest: <c>R = max(0, T − W)</c>. Anton's example: 100 footmen at 110 per 100, keep 10 war →
    ///   100 riding + 10 war = 110 horses. Upgrading men with the war horses turns them into cavalry: the footmen drop and
    ///   the numbers settle by themselves.</item>
    /// </list>
    /// Shared by the planner (<see cref="MountPlanner"/>) and the Instructions tab's live hint beside the two settings.
    /// </summary>
    public static class MountGoal
    {
        /// <summary>The war horse category — the only war mount (vanilla's upgrades ask for it or for a plain horse).</summary>
        public const string WarHorse = "war_horse";

        /// <summary>The noble horse category — never an upgrade requirement (RESEARCH §4): the player's and his companions'
        /// own horses — never bought, sold unless locked.</summary>
        public const string NobleHorse = "noble_horse";

        /// <summary>T: the horses kept for <paramref name="footmen"/> (0 when riding horses are not managed).</summary>
        public static int Total(StewardSettings settings, int footmen)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return settings.MountsEnabled
                ? PlanMath.Ceiling(Math.Max(0, footmen) * (double)Math.Max(0, settings.MountsPer100Footmen) / 100.0)
                : 0;
        }

        /// <summary>W: the war horses kept (0 when war horses are not managed).</summary>
        public static int War(StewardSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            return settings.WarMountsEnabled ? Math.Max(0, settings.WarMountsToKeep) : 0;
        }

        /// <summary>R: the riding horses that fill the rest — <c>max(0, T − W)</c>.</summary>
        public static int Riding(StewardSettings settings, int footmen) => Math.Max(0, Total(settings, footmen) - War(settings));

        /// <summary>Is this held mount a noble horse (sold unless locked)?</summary>
        public static bool IsNoble(ItemStack stack) =>
            stack != null && stack.Kind == ItemKind.Mount && string.Equals(stack.CategoryId, NobleHorse, StringComparison.Ordinal);

        /// <summary>Is this mount a war horse to the steward — the war_horse category while war horses are managed (off: a
        /// plain riding horse)?</summary>
        public static bool IsWar(ItemStack stack, StewardSettings settings) =>
            stack != null && settings != null && settings.WarMountsEnabled && stack.Kind == ItemKind.Mount
            && string.Equals(stack.CategoryId, WarHorse, StringComparison.Ordinal);

        /// <summary>Is this mount a riding horse to the steward — every mount that is neither noble nor a (managed) war horse:
        /// vanilla's <c>horse</c> category (camels included) and any category a mod adds.</summary>
        public static bool IsRiding(ItemStack stack, StewardSettings settings) =>
            stack != null && stack.Kind == ItemKind.Mount && !IsNoble(stack) && !IsWar(stack, settings);
    }
}
