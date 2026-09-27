using System;
using System.Collections.Generic;

namespace SmartSteward.Core.Snapshot
{
    /// <summary>
    /// The game's item type → loot group table of DESIGN §2.6 (verified against v1.4.8's
    /// <c>ItemObject.ItemTypeEnum</c>, RESEARCH §6). The Module passes <c>item.ItemType.ToString()</c>,
    /// so the table lives — and is tested — here, not in game glue.
    /// </summary>
    public static class LootGroups
    {
        /// <summary>The V1 groups in display (and tie-break) order.</summary>
        public static readonly IReadOnlyList<LootGroup> All = new[]
        {
            LootGroup.Armour, LootGroup.MeleeWeapons, LootGroup.Ranged, LootGroup.Shields,
        };

        private static readonly Dictionary<string, LootGroup> ByItemType =
            new Dictionary<string, LootGroup>(StringComparer.Ordinal)
            {
                ["HeadArmor"] = LootGroup.Armour,
                ["BodyArmor"] = LootGroup.Armour,
                ["LegArmor"] = LootGroup.Armour,
                ["HandArmor"] = LootGroup.Armour,
                ["ChestArmor"] = LootGroup.Armour,
                ["Cape"] = LootGroup.Armour,
                ["HorseHarness"] = LootGroup.Armour,

                ["OneHandedWeapon"] = LootGroup.MeleeWeapons,
                ["TwoHandedWeapon"] = LootGroup.MeleeWeapons,
                ["Polearm"] = LootGroup.MeleeWeapons,

                ["Bow"] = LootGroup.Ranged,
                ["Crossbow"] = LootGroup.Ranged,
                ["Sling"] = LootGroup.Ranged,
                ["Thrown"] = LootGroup.Ranged,
                ["Arrows"] = LootGroup.Ranged,
                ["Bolts"] = LootGroup.Ranged,
                ["SlingStones"] = LootGroup.Ranged,
                ["Pistol"] = LootGroup.Ranged,
                ["Musket"] = LootGroup.Ranged,
                ["Bullets"] = LootGroup.Ranged,

                ["Shield"] = LootGroup.Shields,
            };

        /// <summary>The loot group of a game item type name; <see cref="LootGroup.None"/> for everything
        /// the steward never sells as loot (Horse, Animal, Goods, Banner, Book, Invalid…).</summary>
        public static LootGroup FromItemType(string? itemTypeName)
        {
            if (itemTypeName == null)
                return LootGroup.None;
            return ByItemType.TryGetValue(itemTypeName, out var group) ? group : LootGroup.None;
        }
    }
}
