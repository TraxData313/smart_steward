using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Settings;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>One quest need as the plan served it: the need, and how many units the party holds for it (kept).</summary>
    public sealed class QuestNeedState
    {
        internal QuestNeedState(QuestNeed need)
        {
            Need = need;
        }

        public QuestNeed Need { get; }

        /// <summary>Units the party holds for this need — kept from sale, ransom, donation and dismissal (≤ the amount asked).</summary>
        public int Kept { get; internal set; }

        /// <summary>The party holds fewer than the quest asks for.</summary>
        public bool IsShort => Kept < Need.Amount;
    }

    /// <summary>
    /// What the player's quests keep on one row (step 26, DESIGN §2.9) — the Goal cell's quest colour and hover. Set by the planners;
    /// null on a row no quest touches.
    /// </summary>
    public sealed class QuestRowInfo
    {
        internal QuestRowInfo(int kept, int? foodGoal, IReadOnlyList<QuestNeedState> quests)
        {
            Kept = kept;
            FoodGoal = foodGoal;
            Quests = quests;
        }

        /// <summary>Units of this row the quests keep — never sold, ransomed, donated or dismissed by the steward.</summary>
        public int Kept { get; }

        /// <summary>A food row: what the quests ask of this food — an automatic goal, bought toward while held below it; null else.</summary>
        public int? FoodGoal { get; }

        /// <summary>A food row held below <see cref="FoodGoal"/>: the row buys toward it, walked first like a goal of yours.</summary>
        public bool Buys { get; internal set; }

        /// <summary>The quests behind it, each with what the party holds for it.</summary>
        public IReadOnlyList<QuestNeedState> Quests { get; }

        /// <summary>What the quests want this row to end at, at least: the food goal, or the units kept.</summary>
        public int Need => Math.Max(Kept, FoodGoal ?? 0);
    }

    /// <summary>
    /// The player's quest needs over the party (step 26, DESIGN §2.9, RESEARCH §29): which held units each need KEEPS — the units the
    /// steward's lanes leave out (never sold, ransomed, donated or dismissed) — and the food goals the quests ask for. Built once per
    /// planning run from the snapshot; pure and deterministic (the needs served in quest-id order, ties by ids).
    /// </summary>
    /// <remarks>
    /// Which units [Claude's call, DESIGN §2.9]: items — the units a lock already guards first (they are never sold anyway), then the
    /// LEAST valuable (every quest takes any modifier: a lame horse is kept before a healthy one); troops — the HIGHEST tier first
    /// (the gang pays more per tier); prisoners — locked ones first, then the MOST valuable (the laborers quest pays 5 × each one's
    /// ransom value). Two needs of the same thing add up — each keeps its own.
    /// </remarks>
    internal sealed class QuestKeep
    {
        public static readonly QuestKeep None = new QuestKeep();

        private readonly Dictionary<string, int> _items = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _troops = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _prisoners = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Per need: the keys it keeps units of (item stack keys, troop ids).</summary>
        private readonly List<(QuestNeedState State, HashSet<string> Keys)> _served = new List<(QuestNeedState, HashSet<string>)>();

        private QuestKeep()
        {
        }

        public QuestKeep(StewardSnapshot snapshot, StewardSettings settings)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.QuestGoalsEnabled || snapshot.QuestNeeds == null)
                return;
            var needs = snapshot.QuestNeeds
                .Where(n => n != null && n.Amount > 0 && n.Ids != null && n.Ids.Count > 0)
                .OrderBy(n => n.QuestId ?? "", StringComparer.Ordinal)
                .ThenBy(n => n.Kind)
                .ThenBy(n => string.Join("|", n.Ids), StringComparer.Ordinal)
                .ToList();
            if (needs.Count == 0)
                return;

            var inventory = (snapshot.Inventory ?? new List<ItemStack>()).Where(s => s != null && s.Count > 0).ToList();
            var troops = (snapshot.Troops ?? new List<TroopStack>()).Where(t => t != null && !string.IsNullOrEmpty(t.TroopId))
                .GroupBy(t => t.TroopId, StringComparer.Ordinal)
                .Select(g => new { Id = g.Key, Count = g.Sum(t => Math.Max(0, t.InParty)), Tier = g.Max(t => t.Tier) })
                .ToList();
            var prisoners = (snapshot.Prisoners ?? new List<PrisonerStack>()).Where(p => p != null && !string.IsNullOrEmpty(p.TroopId))
                .GroupBy(p => p.TroopId, StringComparer.Ordinal)
                .Select(g => new
                {
                    Id = g.Key,
                    Count = g.Sum(p => Math.Max(0, p.Count)),
                    Locked = g.Any(p => p.IsLocked),
                    Value = g.Max(p => p.RansomValue),
                })
                .ToList();

            foreach (var need in needs)
            {
                var ids = new HashSet<string>(need.Ids.Where(i => !string.IsNullOrEmpty(i)), StringComparer.Ordinal);
                var state = new QuestNeedState(need);
                var keys = new HashSet<string>(StringComparer.Ordinal);
                int left = need.Amount;
                switch (need.Kind)
                {
                    case QuestNeedKind.Items:
                        foreach (var stack in inventory.Where(s => ids.Contains(s.ItemId))
                                     .OrderBy(s => LockRule.IsGuarded(s, settings) ? 0 : 1)
                                     .ThenBy(s => s.UnitValue)
                                     .ThenBy(s => s.Key, StringComparer.Ordinal))
                            left -= Take(_items, stack.Key, stack.Count, left, keys);
                        break;
                    case QuestNeedKind.Troops:
                        foreach (var troop in troops.Where(t => ids.Contains(t.Id)).OrderByDescending(t => t.Tier)
                                     .ThenBy(t => t.Id, StringComparer.Ordinal))
                            left -= Take(_troops, troop.Id, troop.Count, left, keys);
                        break;
                    case QuestNeedKind.Prisoners:
                        foreach (var prisoner in prisoners.Where(p => ids.Contains(p.Id)).OrderBy(p => p.Locked ? 0 : 1)
                                     .ThenByDescending(p => p.Value).ThenBy(p => p.Id, StringComparer.Ordinal))
                            left -= Take(_prisoners, prisoner.Id, prisoner.Count, left, keys);
                        break;
                }
                state.Kept = need.Amount - left;
                _served.Add((state, keys));
            }
        }

        /// <summary>Keeps up to <paramref name="wanted"/> of the units of one key not yet kept by an earlier need.</summary>
        private static int Take(Dictionary<string, int> kept, string key, int count, int wanted, HashSet<string> keys)
        {
            if (wanted <= 0)
                return 0;
            kept.TryGetValue(key, out int already);
            int take = Math.Min(wanted, Math.Max(0, count - already));
            if (take <= 0)
                return 0;
            kept[key] = already + take;
            keys.Add(key);
            return take;
        }

        /// <summary>No quest keeps anything (the switch off, no needs).</summary>
        public bool IsEmpty => _served.Count == 0;

        /// <summary>Every need, as served.</summary>
        public IReadOnlyList<QuestNeedState> Needs => _served.Select(s => s.State).ToList();

        /// <summary>Units of a held stack the quests keep.</summary>
        public int Item(ItemStack stack) =>
            stack != null && _items.TryGetValue(stack.Key, out int kept) ? Math.Min(kept, stack.Count) : 0;

        /// <summary>Units of a held stack the steward may sell: its count minus what the quests keep.</summary>
        public int Free(ItemStack stack) => Math.Max(0, stack.Count - Item(stack));

        /// <summary>Men of a troop type the quests keep.</summary>
        public int Troop(string troopId) => troopId != null && _troops.TryGetValue(troopId, out int kept) ? kept : 0;

        /// <summary>Prisoners of a troop (or a hero's character) the quests keep.</summary>
        public int Prisoner(string troopId) => troopId != null && _prisoners.TryGetValue(troopId, out int kept) ? kept : 0;

        /// <summary>What the quests ask of one food: the sum of the item needs naming exactly this item (Headman Needs Grain, Army Needs
        /// Supply) — an automatic goal; 0 when none.</summary>
        public int FoodGoal(string itemId) =>
            _served.Where(s => s.State.Need.Kind == QuestNeedKind.Items && s.State.Need.Ids.Count == 1
                               && string.Equals(s.State.Need.Ids[0], itemId, StringComparison.Ordinal))
                .Sum(s => s.State.Need.Amount);

        /// <summary>A row of held item stacks (a food row with its item id): what the quests keep on it, and which quests; null when
        /// none touches it.</summary>
        public QuestRowInfo? ForStacks(IEnumerable<ItemStack> stacks, string? foodItemId = null)
        {
            if (IsEmpty)
                return null;
            var held = (stacks ?? Enumerable.Empty<ItemStack>()).Where(s => s != null).ToList();
            var keys = new HashSet<string>(held.Select(s => s.Key), StringComparer.Ordinal);
            int kept = held.Sum(Item);
            int? foodGoal = null;
            if (foodItemId != null)
            {
                int goal = FoodGoal(foodItemId);
                if (goal > 0)
                    foodGoal = goal;
            }
            var quests = _served.Where(s => s.State.Need.Kind == QuestNeedKind.Items
                                            && (s.Keys.Overlaps(keys)
                                                || (foodItemId != null && s.State.Need.Ids.Contains(foodItemId))))
                .Select(s => s.State).ToList();
            return kept == 0 && foodGoal == null ? null : new QuestRowInfo(kept, foodGoal, quests);
        }

        /// <summary>A troop row: the men the quests keep, and which quests.</summary>
        public QuestRowInfo? ForTroop(string troopId) => ForKey(QuestNeedKind.Troops, troopId, Troop(troopId));

        /// <summary>A prisoner row: the prisoners the quests keep, and which quests.</summary>
        public QuestRowInfo? ForPrisoner(string troopId) => ForKey(QuestNeedKind.Prisoners, troopId, Prisoner(troopId));

        private QuestRowInfo? ForKey(QuestNeedKind kind, string id, int kept)
        {
            if (kept <= 0 || id == null)
                return null;
            var quests = _served.Where(s => s.State.Need.Kind == kind && s.Keys.Contains(id)).Select(s => s.State).ToList();
            return new QuestRowInfo(kept, null, quests);
        }
    }
}
