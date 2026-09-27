using System;
using System.Collections.Generic;

namespace SmartSteward.Core.Settings
{
    /// <summary>How the food planner picks the next food type to buy (DESIGN §2.1).</summary>
    public enum FoodStrategy
    {
        /// <summary>Variety first: the allowed type the party holds the fewest of; ties → the cheapest.</summary>
        Balanced,

        /// <summary>Always the cheapest allowed type.</summary>
        Cheapest,
    }

    /// <summary>The order pieces leave a loot group (DESIGN §2.6) — it matters when the market cannot
    /// pay for everything, or the player sells only part of a group.</summary>
    public enum SellLootOrder
    {
        /// <summary>The cheapest pieces first — the most weight for the market's money.</summary>
        Cheapest,

        /// <summary>The lowest price per kg first (weightless pieces last).</summary>
        LowestPricePerKg,

        /// <summary>The dearest pieces first — vanilla's habit.</summary>
        MostExpensive,
    }

    /// <summary>
    /// One item's row of the price book as the PLAYER changed it (DESIGN §1.3). Only overrides are
    /// stored: a null field means "the default" — ticks default to on, bases to the auto-filled
    /// placeholder (or empty when auto-fill is off for the item's group).
    /// </summary>
    public sealed class PriceBookEntry
    {
        /// <summary>Buy tick — the steward may buy this item. Null = default (ticked).</summary>
        public bool? Buy { get; set; }

        /// <summary>Typed base of the max buy price (final = base × BuyPriceMultiplier). Null = placeholder.</summary>
        public int? BuyBase { get; set; }

        /// <summary>Sell tick — the steward may sell this item. Null = default (ticked).</summary>
        public bool? Sell { get; set; }

        /// <summary>Typed base of the min sell price (final = base × SellPriceMultiplier). Null = placeholder.</summary>
        public int? SellBase { get; set; }

        /// <summary>True when nothing is overridden — such an entry need not be stored.</summary>
        public bool IsEmpty => Buy == null && BuyBase == null && Sell == null && SellBase == null;
    }

    /// <summary>
    /// Every V1 setting of DESIGN §7 with exactly its default — the property names ARE the §7 keys
    /// (a test reads the table and holds them together). A plain POCO: no I/O, no ranges; step 5
    /// wraps a registry, the settings file and MCM around it. The planners stay robust to odd values
    /// (negative targets count as 0) but never clamp the object itself.
    /// </summary>
    public sealed class StewardSettings
    {
        // ── General ──────────────────────────────────────────────────────────────────────────────
        /// <summary>Master switch. Off → the planner returns an empty plan.</summary>
        public bool ModEnabled { get; set; } = true;
        public bool AutoPopupOnTownEnter { get; set; } = true;
        public bool AutoPopupOnVillageEnter { get; set; } = true;
        /// <summary>Auto-open only when the plan has a change (<c>StewardPlan.HasChanges</c>).</summary>
        public bool PopupOnlyWithChanges { get; set; } = true;
        public bool WarnIfNotReviewed { get; set; } = true;
        public bool AutoExecute { get; set; } = false;

        // ── Money (DESIGN §3) ────────────────────────────────────────────────────────────────────
        /// <summary>No purchase takes the purse below this.</summary>
        public int MinGoldAfterDeal { get; set; } = 1000;
        /// <summary>No ANIMAL purchase takes the purse below this (food answers only to MinGoldAfterDeal).</summary>
        public int MinGoldForHorses { get; set; } = 5000;

        // ── Food (DESIGN §2.1) ───────────────────────────────────────────────────────────────────
        public bool FoodEnabled { get; set; } = true;
        /// <summary>Food units kept per eater: target = ceil(eaters × FoodPerMan).</summary>
        public double FoodPerMan { get; set; } = 2.0;
        /// <summary>Prisoners count as eaters — half each, like the game.</summary>
        public bool FoodCountPrisoners { get; set; } = true;
        public FoodStrategy FoodStrategy { get; set; } = FoodStrategy.Balanced;
        public bool SellFoodSurplus { get; set; } = true;
        /// <summary>Surplus is sold only above target × (1 + this/100), and then back down to the target.</summary>
        public int FoodSurplusTolerancePercent { get; set; } = 25;

        // ── Prices (DESIGN §1.3) ─────────────────────────────────────────────────────────────────
        /// <summary>Final max buy = base × this (range 0.1–10).</summary>
        public double BuyPriceMultiplier { get; set; } = 1.2;
        /// <summary>Final min sell = base × this (range 0–10).</summary>
        public double SellPriceMultiplier { get; set; } = 0.8;
        public bool AutoFillFoodPrices { get; set; } = true;
        public bool AutoFillPackAndMountPrices { get; set; } = true;
        public bool AutoFillWarMountPrices { get; set; } = false;
        /// <summary>Per item id — only the player's overrides (ticks flipped, bases typed).</summary>
        public Dictionary<string, PriceBookEntry> PriceBook { get; set; } =
            new Dictionary<string, PriceBookEntry>(StringComparer.Ordinal);

        // ── Pack animals (DESIGN §2.2) ───────────────────────────────────────────────────────────
        public bool PackAnimalsEnabled { get; set; } = true;
        public int PackAnimalsTarget { get; set; } = 10;
        /// <summary>Role cap per pack animal (0 = none; NOT scaled by the multiplier).</summary>
        public int PackAnimalMaxPrice { get; set; } = 300;
        public bool SellPackAnimalSurplus { get; set; } = true;

        // ── Mounts (DESIGN §2.3) ─────────────────────────────────────────────────────────────────
        public bool MountsEnabled { get; set; } = true;
        /// <summary>Riding target = ceil(footmen × this / 100).</summary>
        public int MountsPer100Footmen { get; set; } = 110;
        /// <summary>Role cap per footman's mount (0 = none; not scaled).</summary>
        public int MountMaxPrice { get; set; } = 500;
        /// <summary>Horses reserved for upgrades count toward the footmen's riding target.</summary>
        public bool WarMountsCountAsMounts { get; set; } = true;
        public bool SellMountSurplus { get; set; } = true;

        // ── War mounts (DESIGN §2.4) ─────────────────────────────────────────────────────────────
        public bool WarMountsEnabled { get; set; } = true;
        /// <summary>-1 = count upgrade-ready troops; ≥ 0 = keep exactly this many per upgrade category.</summary>
        public int WarMountsManualTarget { get; set; } = -1;
        /// <summary>Buffer on top of the automatic count.</summary>
        public int WarMountsExtra { get; set; } = 0;
        /// <summary>Role cap per upgrade horse (0 = none; not scaled).</summary>
        public int WarMountMaxPrice { get; set; } = 2000;
        public bool SellWarMountSurplus { get; set; } = true;

        // ── Prisoners (DESIGN §2.5) ──────────────────────────────────────────────────────────────
        public bool RansomPrisoners { get; set; } = true;
        /// <summary>Heroes (lords) are proposed at all — ransom or donation.</summary>
        public bool RansomHeroPrisoners { get; set; } = false;
        public bool DonatePrisonersWhenPossible { get; set; } = false;
        /// <summary>Troop ids unticked in the Instructions tab's "Prisoners to ransom".</summary>
        public List<string> PrisonersExcluded { get; set; } = new List<string>();

        // ── Loot (DESIGN §2.6) ───────────────────────────────────────────────────────────────────
        public bool SellLoot { get; set; } = false;
        /// <summary>Weapons, armour, shields, ammo — all four V1 loot groups.</summary>
        public bool SellLootEquipment { get; set; } = true;
        /// <summary>Pieces worth more per unit are never sold (0 = no cap).</summary>
        public int SellLootMaxItemValue { get; set; } = 0;
        public SellLootOrder SellLootOrder { get; set; } = SellLootOrder.Cheapest;

        // ── Tavern (DESIGN §2.7) ─────────────────────────────────────────────────────────────────
        public bool ShowTavern { get; set; } = true;
        public bool ShowWanderers { get; set; } = true;
        public bool ShowMercenaries { get; set; } = true;
    }
}
