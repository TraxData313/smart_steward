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

    /// <summary>What the steward does with prisoners (DESIGN §2.5 — Anton 2026.09.28, playtest round 4: "A toggle ransom/donate
    /// lords. Then a toggle to offload them to ransom for money or to dungeon for Influence").</summary>
    public enum PrisonerChoice
    {
        /// <summary>Keep them — the steward proposes nothing (the player may still ransom by hand).</summary>
        Keep,

        /// <summary>Ransom them for denari at the town's ransom broker.</summary>
        Ransom,

        /// <summary>Donate them to a friendly town's dungeon for influence (where the game allows it; else the others are
        /// ransomed and the lords kept).</summary>
        Donate,
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

        /// <summary>Typed base of the max buy price (final = base × the group's buy multiplier: food or horses). Null = placeholder.</summary>
        public int? BuyBase { get; set; }

        /// <summary>Sell tick — the steward may sell this item. Null = default (ticked).</summary>
        public bool? Sell { get; set; }

        /// <summary>Typed base of the min sell price (final = base × the group's sell multiplier). Null = placeholder.</summary>
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
        /// <summary>The Full-autonomous steward (DESIGN §6): plan and carry out on arrival, no window, no popup, no
        /// warning, a message-log report — under <see cref="AutonomousMinGold"/>. Replaces the old AutoExecute key (a
        /// settings file that still has it carries its value over once).</summary>
        public bool AutonomousSteward { get; set; } = false;
        /// <summary>Inventory locks guard food and animals too (DESIGN §7, Anton 2026.09.28 — playtest round 2). Off (the
        /// default): locked food, pack animals and mounts are counted and sold as surplus like any other — the locks keep
        /// guarding armour and weapons only (<c>Planning.LockRule</c>). On: locked food and animals are never sold, the
        /// old way.</summary>
        public bool LocksProtectFoodAndHorses { get; set; } = false;

        // ── Money (DESIGN §3) ────────────────────────────────────────────────────────────────────
        /// <summary>No purchase takes the purse below this.</summary>
        public int MinGoldAfterDeal { get; set; } = 1000;
        /// <summary>No ANIMAL purchase takes the purse below this (food answers only to MinGoldAfterDeal).</summary>
        public int MinGoldForHorses { get; set; } = 5000;
        /// <summary>The purse floor while the steward acts alone: both floors above rise to it
        /// (<c>Planning.MoneyFloors</c>).</summary>
        public int AutonomousMinGold { get; set; } = 100000;

        // ── Food (DESIGN §2.1) ───────────────────────────────────────────────────────────────────
        public bool FoodEnabled { get; set; } = true;
        /// <summary>The purse (before the deal) food needs to be managed at all — below it the steward neither buys nor sells
        /// food (Anton 2026.09.28, round 4; <c>Planning.JobThresholds</c>). 0 = always.</summary>
        public int FoodMinDenari { get; set; } = 2000;
        /// <summary>Days of food kept for the party after the deal, at the game's own rate (Anton 2026.09.28 — replaces
        /// FoodPerMan): target = ceil(days × daily use per eater × eaters) (<c>Planning.FoodGoal</c>).</summary>
        public int FoodDays { get; set; } = 40;
        /// <summary>Prisoners count as eaters — half each, like the game.</summary>
        public bool FoodCountPrisoners { get; set; } = true;
        public FoodStrategy FoodStrategy { get; set; } = FoodStrategy.Balanced;
        public bool SellFoodSurplus { get; set; } = true;
        /// <summary>Surplus is sold only above target × (1 + this/100), and then back down to the target.</summary>
        public int FoodSurplusTolerancePercent { get; set; } = 25;

        // ── Prices (DESIGN §1.3) ─────────────────────────────────────────────────────────────────
        /// <summary>FOOD: final max buy = base × this (range 0.1–10). Round 4 (Anton 2026.09.28: "food prices multipliers set from
        /// 0.5 to 2, grain costs 10 up to 20 id be happy to buy"): food has multipliers of its own.</summary>
        public double FoodBuyPriceMultiplier { get; set; } = 2.0;
        /// <summary>FOOD: final min sell = base × this (range 0–10).</summary>
        public double FoodSellPriceMultiplier { get; set; } = 0.5;
        /// <summary>HORSES (pack animals, riding, war and noble horses): final max buy = base × this (range 0.1–10) — the old
        /// BuyPriceMultiplier, renamed in round 4 (a settings file's old key carries over once).</summary>
        public double HorseBuyPriceMultiplier { get; set; } = 1.2;
        /// <summary>HORSES: final min sell = base × this (range 0–10) — the old SellPriceMultiplier.</summary>
        public double HorseSellPriceMultiplier { get; set; } = 0.8;
        public bool AutoFillFoodPrices { get; set; } = true;
        public bool AutoFillPackAndMountPrices { get; set; } = true;
        public bool AutoFillWarMountPrices { get; set; } = false;
        /// <summary>Per item id — only the player's overrides (ticks flipped, bases typed).</summary>
        public Dictionary<string, PriceBookEntry> PriceBook { get; set; } =
            new Dictionary<string, PriceBookEntry>(StringComparer.Ordinal);

        // ── Pack animals (DESIGN §2.2) ───────────────────────────────────────────────────────────
        public bool PackAnimalsEnabled { get; set; } = true;
        /// <summary>The purse (before the deal) pack animals need to be managed at all (round 4).</summary>
        public int PackAnimalsMinDenari { get; set; } = 2000;
        public int PackAnimalsTarget { get; set; } = 10;
        /// <summary>Role cap per pack animal (0 = none; NOT scaled by the multiplier).</summary>
        public int PackAnimalMaxPrice { get; set; } = 300;
        public bool SellPackAnimalSurplus { get; set; } = true;

        // ── Mounts (DESIGN §2.3) ─────────────────────────────────────────────────────────────────
        public bool MountsEnabled { get; set; } = true;
        /// <summary>The purse (before the deal) riding horses need to be managed at all — noble horses sold and lame riding horses
        /// replaced too (round 4).</summary>
        public int MountsMinDenari { get; set; } = 5000;
        /// <summary>Horses kept for the footmen = ceil(footmen × this / 100) — the war horses kept count among them, riding
        /// horses fill the rest (Anton 2026.09.28, step 17: <c>Planning.MountGoal</c>).</summary>
        public int MountsPer100Footmen { get; set; } = 110;
        /// <summary>Role cap per footman's mount (0 = none; not scaled).</summary>
        public int MountMaxPrice { get; set; } = 500;
        public bool SellMountSurplus { get; set; } = true;
        /// <summary>Noble horses are never bought and are sold unless LOCKED — a lock always keeps one (Anton 2026.09.28, step
        /// 17): they are for the player and his companions, never an upgrade requirement.</summary>
        public bool SellNobleHorses { get; set; } = true;
        /// <summary>Sell the party's badly modified horses (lame, old) and buy healthy ones in their place (Anton 2026.09.28,
        /// step 17). Off: they are kept and counted — a lame horse still carries a footman. Never bought either way.</summary>
        public bool ReplaceLameHorses { get; set; } = true;

        // ── War mounts (DESIGN §2.4) ─────────────────────────────────────────────────────────────
        /// <summary>Manage war horses at all; off = a war horse is a plain riding horse to the steward.</summary>
        public bool WarMountsEnabled { get; set; } = true;
        /// <summary>The purse (before the deal) war horses need to be managed at all (round 4).</summary>
        public int WarHorsesMinDenari { get; set; } = 20000;
        /// <summary>War horses (the <c>war_horse</c> category) the party keeps — a plain number, bought up to it and sold above
        /// it; they count among the horses for the footmen (Anton 2026.09.28, step 17 — replaces the automatic upgrade count:
        /// WarMountsHorseTarget, WarMountsWarHorseTarget, WarMountsExtra and WarMountsCountAsMounts are retired). Default 10 since
        /// round 4 (Anton 2026.09.28; was 0) — kept only once the purse reaches WarHorsesMinDenari.</summary>
        public int WarMountsToKeep { get; set; } = 10;
        /// <summary>Role cap per war horse (0 = none; not scaled).</summary>
        public int WarMountMaxPrice { get; set; } = 2000;
        public bool SellWarMountSurplus { get; set; } = true;

        // ── Prisoners (DESIGN §2.5) ──────────────────────────────────────────────────────────────
        /// <summary>Captured lords: Keep (default), Ransom, or Donate — a lord is never ransomed when Donate is chosen: where the
        /// game forbids donating, or the dungeon is full, he is kept (round 4).</summary>
        public PrisonerChoice LordPrisonerAction { get; set; } = PrisonerChoice.Keep;
        /// <summary>Every other prisoner: Keep, Ransom (default) or Donate — where the game forbids donating, or the dungeon is
        /// full, the rest are ransomed (round 4). Replaces RansomPrisoners, RansomHeroPrisoners and DonatePrisonersWhenPossible
        /// (an old settings file's values carry over once).</summary>
        public PrisonerChoice PrisonerAction { get; set; } = PrisonerChoice.Ransom;
        // (PrisonersExcluded is retired — ransom is all or none: step 12.)

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

        /// <summary>The troops section (DESIGN §2.8, step 16): the recruits on offer, then the party's own troops.</summary>
        public bool ShowTroops { get; set; } = true;
    }
}
