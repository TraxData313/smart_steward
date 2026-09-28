using System;
using System.Collections.Generic;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>
    /// What the party's upgrades need, per kind of horse (DESIGN §2.4): the game's upgrades ask for an item CATEGORY
    /// on the troop upgraded into — vanilla uses <see cref="Horse"/> and <see cref="WarHorse"/> only (RESEARCH §4); a
    /// mod may add others. Shared by the planner (the need it buys toward) and the Instructions tab (the live count
    /// beside the two targets).
    /// </summary>
    public sealed class UpgradeNeeds
    {
        /// <summary>The plain horse an upgrade may need (the game's "horse" category).</summary>
        public const string Horse = "horse";

        /// <summary>The war horse an upgrade may need (the game's "war_horse" category).</summary>
        public const string WarHorse = "war_horse";

        private readonly SortedSet<string> _inPlay;
        private readonly Dictionary<string, int> _ready;

        private UpgradeNeeds(SortedSet<string> inPlay, Dictionary<string, int> ready)
        {
            _inPlay = inPlay;
            _ready = ready;
        }

        /// <summary>The categories some troop of the party upgrades into (ready or not), in ordinal order.</summary>
        public IReadOnlyCollection<string> InPlay => _inPlay;

        /// <summary>Troops ready to upgrade NOW into a unit that needs <paramref name="category"/> — the party screen's
        /// count; a stack is counted once, at its best horse-needing target (both targets share one XP pool, and a
        /// foot-or-horse recruit counts as needing the horse).</summary>
        public int ReadyFor(string category) => _ready.TryGetValue(category, out var n) ? n : 0;

        /// <summary>Reads the upgrade stacks of a snapshot.</summary>
        public static UpgradeNeeds Of(StewardSnapshot snapshot) => Of(snapshot, Array.Empty<PartyMove>());

        /// <summary>The upgrade stacks after the deal's party moves (<see cref="PartyAfter"/>, step 15): men who join are never
        /// ready (no XP yet) but put their troop's kinds of upgrade horse in play; men who leave a stack take its size — and
        /// so at most its ready count — down with them.</summary>
        internal static UpgradeNeeds Of(StewardSnapshot? snapshot, IReadOnlyCollection<PartyMove> moves)
        {
            var inPlay = new SortedSet<string>(StringComparer.Ordinal);
            var ready = new Dictionary<string, int>(StringComparer.Ordinal);
            var leaving = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var move in moves)
            {
                if (move.Men > 0)
                {
                    foreach (var category in move.UpgradeCategories)
                        if (!string.IsNullOrEmpty(category))
                            inPlay.Add(category);
                }
                else if (move.Men < 0 && move.TroopId != null)
                    leaving[move.TroopId] = (leaving.TryGetValue(move.TroopId, out var n) ? n : 0) - move.Men;
            }

            foreach (var stack in snapshot?.Upgrades ?? new List<UpgradeStack>())
            {
                if (stack?.Targets == null)
                    continue;
                int size = Math.Max(0, stack.Count);
                if (leaving.TryGetValue(stack.TroopId, out var gone))
                    size = Math.Max(0, size - gone);
                UpgradeTarget? best = null;
                foreach (var target in stack.Targets)
                {
                    if (target == null || string.IsNullOrEmpty(target.RequiredCategoryId))
                        continue;
                    inPlay.Add(target.RequiredCategoryId!);
                    if (target.ReadyCount > 0 && (best == null || target.ReadyCount > best.ReadyCount
                            || (target.ReadyCount == best.ReadyCount
                                && string.CompareOrdinal(target.RequiredCategoryId, best.RequiredCategoryId) < 0)))
                        best = target;
                }
                if (best != null)
                {
                    int count = Math.Min(best.ReadyCount, size);
                    ready[best.RequiredCategoryId!] = (ready.TryGetValue(best.RequiredCategoryId!, out var r) ? r : 0) + count;
                }
            }
            return new UpgradeNeeds(inPlay, ready);
        }

        /// <summary>The player's fixed number for a kind: <c>WarMountsHorseTarget</c> for horses,
        /// <c>WarMountsWarHorseTarget</c> for war horses; -1 (automatic) for them by default and always for a category a
        /// mod adds.</summary>
        public static int ManualTarget(StewardSettings settings, string category)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (string.Equals(category, Horse, StringComparison.Ordinal)) return settings.WarMountsHorseTarget;
            if (string.Equals(category, WarHorse, StringComparison.Ordinal)) return settings.WarMountsWarHorseTarget;
            return -1;
        }

        /// <summary>How many horses of <paramref name="category"/> the steward keeps for upgrades: the player's fixed
        /// number for that kind when ≥ 0, else the troops ready now + <c>WarMountsExtra</c>. Only for a category in
        /// play — nobody upgrades into the others, so nothing is kept for them.</summary>
        public int NeedFor(StewardSettings settings, string category)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.WarMountsEnabled || !_inPlay.Contains(category))
                return 0;
            int manual = ManualTarget(settings, category);
            return manual >= 0 ? manual : ReadyFor(category) + Math.Max(0, settings.WarMountsExtra);
        }

        /// <summary><see cref="NeedFor"/> for every category in play (empty when war mounts are not managed).</summary>
        public Dictionary<string, int> Need(StewardSettings settings)
        {
            var need = new Dictionary<string, int>(StringComparer.Ordinal);
            if (settings != null && settings.WarMountsEnabled)
                foreach (var category in _inPlay)
                    need[category] = NeedFor(settings, category);
            return need;
        }
    }
}
