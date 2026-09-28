using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// What a lock in the inventory screen protects from the steward (DESIGN §2.6, §7 — Anton 2026.09.28, playtest
    /// round 2: "food and horses even if locked, manage them"). The game itself only honours a lock in the trade
    /// screen's "transfer all" (RESEARCH §6), so this is the steward's own rule, and the planner and the executor
    /// share it: a guarded stack is never sold and not counted as sellable; an unguarded one is managed as if it were
    /// not locked.
    /// </summary>
    public static class LockRule
    {
        /// <summary>
        /// True when a lock keeps a stack of this kind from sale: armour and weapons (and anything the steward does
        /// not manage) always; food, pack animals and mounts only with <see cref="StewardSettings.LocksProtectFoodAndHorses"/>
        /// — off (the default), they are counted and sold as surplus locked or not.
        /// </summary>
        public static bool Guards(ItemKind kind, bool locksProtectFoodAndHorses)
        {
            switch (kind)
            {
                case ItemKind.Food:
                case ItemKind.PackAnimal:
                case ItemKind.Mount:
                    return locksProtectFoodAndHorses;
                default:
                    return true;
            }
        }

        /// <inheritdoc cref="Guards(ItemKind, bool)"/>
        public static bool Guards(ItemKind kind, StewardSettings settings) =>
            Guards(kind, settings == null || settings.LocksProtectFoodAndHorses);

        /// <summary>The stack is locked AND its lock guards it — the steward never sells it. A NOBLE horse's lock always
        /// guards it, whatever <see cref="StewardSettings.LocksProtectFoodAndHorses"/> says (Anton 2026.09.28, step 17: "we are
        /// selling them if the player didn't lock them") — the lock is how the player keeps his own horse.</summary>
        public static bool IsGuarded(ItemStack stack, StewardSettings settings) =>
            stack != null && stack.IsLocked && (Guards(stack.Kind, settings) || MountGoal.IsNoble(stack));
    }
}
