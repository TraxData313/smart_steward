using System;
using System.Collections.Generic;
using System.Linq;
using SmartSteward.Core.Planning;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Presentation
{
    /// <summary>
    /// Everything that folds in the round-4 Suggestion tab (PLAN step 21, the mockup Anton approved on 2026.09.28) — each a
    /// key of <c>window_state.json</c> (<see cref="WindowState"/>), remembered across windows, towns and restarts:
    /// <list type="bullet">
    /// <item>the sections' detail rows — <see cref="Food"/>, <see cref="Horses"/>, <see cref="Prisoners"/>, <see cref="Other"/>
    ///   (folded, a section shows its title line and the lines that always stay: Lords and Others);</item>
    /// <item>the two troop lines' rows — <see cref="Recruits"/>, <see cref="YourTroops"/> (the Troops title opens and closes
    ///   both: mockup choice 8);</item>
    /// <item>a row's per-type breakdown (▸) — the five horse role rows and the Other goods row.</item>
    /// </list>
    /// The names are the file's words — never rename one.
    /// </summary>
    public static class SheetFolds
    {
        public const string Recruits = "Recruits";
        public const string YourTroops = "YourTroops";
        public const string Food = "Food";
        public const string Horses = "Horses";
        public const string Prisoners = "Prisoners";
        public const string Other = "Other";
        public const string PackAnimals = "PackAnimals";
        public const string RidingHorses = "RidingHorses";
        public const string WarHorses = "WarHorses";
        public const string NobleHorses = "NobleHorses";
        public const string LameHorses = "LameHorses";
        public const string OtherGoods = "OtherGoods";

        /// <summary>Every key, in the table's order.</summary>
        public static IReadOnlyList<string> All { get; } = new[]
        {
            Recruits, YourTroops, Food, Horses, PackAnimals, RidingHorses, WarHorses, NobleHorses, LameHorses, Prisoners, Other,
            OtherGoods,
        };

        /// <summary>
        /// Folded when nothing is remembered yet (no file): the approved mockup's everyday view (suggestion_v2_folded.png) — the
        /// long lists (Recruits, Your troops, Food, the prisoner rows) folded, Horses and Other open, every ▸ breakdown closed.
        /// [decided: Claude, 2026.09.28 — step 21; until then no file meant every section open.]
        /// </summary>
        public static IReadOnlyList<string> FoldedByDefault { get; } = new[]
        {
            Recruits, YourTroops, Food, PackAnimals, RidingHorses, WarHorses, NobleHorses, LameHorses, Prisoners, OtherGoods,
        };

        /// <summary>The section's own fold (Troops has none: its title folds both troop lines).</summary>
        public static string? OfSection(SheetGroup group)
        {
            switch (group)
            {
                case SheetGroup.Food: return Food;
                case SheetGroup.Horses: return Horses;
                case SheetGroup.Prisoners: return Prisoners;
                case SheetGroup.Other: return Other;
                default: return null;
            }
        }

        /// <summary>The fold of a row's ▸ breakdown: the horse role rows and the Other goods row; null for any other row.</summary>
        public static string? OfRow(PlanRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (row.Role != null)
            {
                switch (row.Role.Value)
                {
                    case MountRole.Pack: return PackAnimals;
                    case MountRole.Riding: return RidingHorses;
                    case MountRole.War: return WarHorses;
                    case MountRole.Noble: return NobleHorses;
                    case MountRole.Lame: return LameHorses;
                }
            }
            return row.Type == RowType.Loot && row.LootGroup == LootGroup.OtherGoods ? OtherGoods : null;
        }

        /// <summary>A key as the file writes it, case-insensitive; null for anything unknown.</summary>
        public static string? Find(string? name)
        {
            string n = (name ?? "").Trim();
            return All.FirstOrDefault(k => string.Equals(k, n, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The step-18/20 file's section names (<c>"CollapsedSections"</c>: Tavern, Troops, Food, Mounts, Other — once
        /// ArmourAndWeapons —, Prisoners) as today's keys: Troops folded both halves (now Recruits + Your troops), Mounts is
        /// Horses; the Tavern has no fold any more (its lines always show) — known, nothing. Null for a name it never had.
        /// </summary>
        public static IReadOnlyList<string>? FromOldSection(string? name)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "tavern": return Array.Empty<string>();
                case "troops": return new[] { Recruits, YourTroops };
                case "food": return new[] { Food };
                case "mounts": return new[] { Horses };
                case "other":
                case "armourandweapons": return new[] { Other };
                case "prisoners": return new[] { Prisoners };
                default: return null;
            }
        }

        /// <summary>The old file's sections that meant something then (for the migration: named = folded, else open).</summary>
        internal static IReadOnlyList<string> OldSectionKeys { get; } = new[] { Recruits, YourTroops, Food, Horses, Other, Prisoners };
    }
}
