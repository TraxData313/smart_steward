using System;
using System.Collections.Generic;
using SmartSteward.Core.Pricing;
using SmartSteward.Core.Snapshot;

namespace SmartSteward.Core.Planning
{
    /// <summary>The Suggestion tab's sections in DESIGN §1.1's order. (LATER, not V1: Others.)</summary>
    public enum PlanSectionKind
    {
        Tavern,
        Food,
        Mounts,
        ArmourAndWeapons,
        Prisoners,
    }

    /// <summary>The table's Type column.</summary>
    public enum RowType
    {
        Tavern,
        Food,
        Pack,
        Mount,
        WarMount,
        Loot,
        Prisoner,
    }

    /// <summary>The mount role rows of DESIGN §1.1.1.</summary>
    public enum MountRole
    {
        /// <summary>Pack animals.</summary>
        Pack,

        /// <summary>Riding mounts — every mount not reserved for an upgrade.</summary>
        Riding,

        /// <summary>Mounts of one upgrade category (<see cref="PlanRow.CategoryId"/>) reserved for upgrades.</summary>
        Upgrade,
    }

    public enum TavernRowKind
    {
        Wanderer,
        Mercenaries,
    }

    /// <summary>Why a tavern row could not be raised when the plan was made (the live reason, after the player's
    /// clicks, is <see cref="PlanRow.IncreaseBlock"/>).</summary>
    public enum HireBlock
    {
        None,

        /// <summary>The clan's companion limit is reached (vanilla enforces it). The party size limit never blocks a
        /// hire (Anton 2026.09.28, playtest round 3).</summary>
        CompanionLimit,
    }

    /// <summary>Units one row moved from one stack in one direction, with what they cost or fetched.</summary>
    public sealed class StackTally
    {
        internal StackTally(ItemStack stack, TradeDirection direction)
        {
            Stack = stack;
            Direction = direction;
        }

        public ItemStack Stack { get; }
        public TradeDirection Direction { get; }
        public int Count { get; private set; }

        /// <summary>Sum of the unit prices (always positive).</summary>
        public int Gold { get; private set; }
        public int MinPrice { get; private set; }
        public int MaxPrice { get; private set; }

        internal void Add(int price)
        {
            MinPrice = Count == 0 ? price : Math.Min(MinPrice, price);
            MaxPrice = Count == 0 ? price : Math.Max(MaxPrice, price);
            Count++;
            Gold += price;
        }
    }

    /// <summary>One line of a role row's per-type breakdown (the collapsed ▸ of DESIGN §1.1.1).</summary>
    public sealed class PlanRowLine
    {
        internal PlanRowLine(ItemStack stack)
        {
            StackKey = stack.Key;
            ItemId = stack.ItemId;
            Name = stack.Name;
            ModifierId = stack.ModifierId;
        }

        public string StackKey { get; }
        public string ItemId { get; }
        public string Name { get; }
        public string? ModifierId { get; }

        /// <summary>Units of this stack the row holds (for role rows: the units in this role).</summary>
        public int Mine { get; internal set; }
        public int Change { get; internal set; }
        public int GoldDelta { get; internal set; }
        public int UnitPriceMin { get; internal set; }
        public int UnitPriceMax { get; internal set; }

        /// <summary>Units on offer the row may buy; null when the stack is not on the market.</summary>
        public int? Market { get; internal set; }
    }

    /// <summary>Prisoner row facts: ransom gold and donation influence per man, and the split.</summary>
    public sealed class PrisonerRowInfo
    {
        public int RansomValue { get; internal set; }
        public double InfluencePerMan { get; internal set; }
        public bool IsHero { get; internal set; }
        public int RansomCount { get; internal set; }
        public int DonateCount { get; internal set; }
    }

    /// <summary>Tavern row facts.</summary>
    public sealed class TavernRowInfo
    {
        public TavernRowKind Kind { get; internal set; }

        /// <summary>Hire price (wanderer) or price per man (mercenaries).</summary>
        public int UnitPrice { get; internal set; }
        public int DailyWage { get; internal set; }
        public HireBlock Block { get; internal set; }
        public string? SkillTag { get; internal set; }

        /// <summary>Mercenaries: what one man adds to the load at sea (his horse, when the troop rides — War Sails).</summary>
        public double SeaWeightPerMan { get; internal set; }

        /// <summary>The man hired rides a horse of his own (<c>CharacterObject.IsMounted</c>) — else he is one more footman
        /// the steward mounts (the live re-plan, step 15).</summary>
        public bool IsMounted { get; internal set; }

        /// <summary>Mercenaries: the kinds of upgrade horse the troop's upgrades need (never ready now — new men).</summary>
        public IReadOnlyList<string> UpgradeCategories { get; internal set; } = Array.Empty<string>();
    }

    /// <summary>
    /// One row of the Suggestion table (DESIGN §1.1). Change is signed: + buy / hire, − sell / ransom /
    /// donate. Names come from the snapshot; role and group rows carry no text (the window labels them
    /// through TextObject ids from <see cref="Role"/>, <see cref="CategoryId"/> and <see cref="LootGroup"/>).
    /// </summary>
    public sealed class PlanRow
    {
        internal PlanRow(string id, PlanSectionKind section, RowType type)
        {
            Id = id;
            Section = section;
            Type = type;
        }

        /// <summary>Stable id, e.g. <c>food:grain</c>, <c>mounts:riding</c>, <c>mounts:upgrade:war_horse</c>,
        /// <c>loot:Armour</c>, <c>prisoner:looter</c>, <c>tavern:wanderer:&lt;hero&gt;</c>, <c>tavern:mercenaries</c>.</summary>
        public string Id { get; }
        public PlanSectionKind Section { get; }
        public RowType Type { get; }

        /// <summary>Item, troop or hero name; empty for role and group rows.</summary>
        public string Name { get; internal set; } = "";
        public string? ItemId { get; internal set; }
        public string? TroopId { get; internal set; }
        public string? HeroId { get; internal set; }
        public string? CategoryId { get; internal set; }
        public LootGroup LootGroup { get; internal set; }
        public MountRole? Role { get; internal set; }

        /// <summary>What the party holds now (loot groups: the SELLABLE pieces; role rows: units in the role).</summary>
        public int Mine { get; internal set; }

        /// <summary>Locked units whose lock guards them (<see cref="LockRule"/>), shown apart ("+3 locked") — never sold.
        /// Food and animal rows: 0 unless LocksProtectFoodAndHorses (their locked units are managed like the rest).</summary>
        public int Locked { get; internal set; }

        /// <summary>Item rows: a lock in the inventory keeps this row's stacks from sale (<see cref="LockRule"/>) — loot
        /// always, food and animals only with LocksProtectFoodAndHorses. The executor re-checks the live locks of such a
        /// row's sales (<see cref="PlanTransaction.HonoursLock"/>) and sells the others locked or not.</summary>
        public bool LocksGuard { get; internal set; }

        /// <summary>Loot only: unlocked pieces above <c>SellLootMaxItemValue</c> — never sold, not in Mine.</summary>
        public int OverValueCap { get; internal set; }

        /// <summary>Market stock (food: all of the item; role rows: eligible units on offer; tavern: on offer);
        /// null = "—".</summary>
        public int? Market { get; internal set; }

        /// <summary>The steward's proposal for this row — for an untouched row its quantity, kept up to date by the live
        /// re-plan; for a touched row the steward's last word before the player's hand took over.</summary>
        public int SuggestedChange { get; internal set; }

        /// <summary>The quantity the plan will do — the steward's, or the player's after an edit
        /// (<see cref="StewardPlan.Increase"/>, <see cref="StewardPlan.Decrease"/>, <see cref="StewardPlan.Reset"/>).</summary>
        public int Change { get; internal set; }
        public int Result => Mine + Change;

        /// <summary>
        /// The player's hand is on this row (Anton 2026.09.28, step 15): a click moved it, so it keeps its quantity and the
        /// steward plans around it — first in its phase of the walk (<see cref="PlanPins"/>). An untouched row belongs to
        /// the steward: a party-changing edit (a hire, a prisoner kept or ransomed) re-plans it for the party after the
        /// deal. ⟲ clears the touch and hands the row back to the steward; the ⟲ shows exactly on touched rows.
        /// </summary>
        public bool IsTouched { get; internal set; }

        /// <summary>The player's hand is on this row (⟲ is live) — <see cref="IsTouched"/>. It stays so even when clicks bring
        /// it back to the steward's number; only ⟲ hands it back.</summary>
        public bool IsEdited => IsTouched;

        /// <summary>Why a [+] cannot move this row right now (<see cref="EditBlock.None"/> = it can) — live:
        /// recomputed after every edit, since rows share the market's stock, gold and prices, the purse and the
        /// party's room.</summary>
        public EditBlock IncreaseBlock => Owner?.BlockOf(this, +1) ?? EditBlock.None;

        /// <summary>Why a [−] cannot move this row right now (<see cref="EditBlock.None"/> = it can).</summary>
        public EditBlock DecreaseBlock => Owner?.BlockOf(this, -1) ?? EditBlock.None;

        public bool CanIncrease => IncreaseBlock == EditBlock.None;
        public bool CanDecrease => DecreaseBlock == EditBlock.None;

        /// <summary>Clamps: Change stays within [−MaxSell, +MaxBuy]. For item rows MaxBuy = units on offer whose
        /// first price passes their limit, MaxSell = units the sell lane holds; the walk may stop sooner as
        /// town prices climb past a limit.</summary>
        public int MaxBuy { get; internal set; }
        public int MaxSell { get; internal set; }

        /// <summary>Lowest and highest unit price over the units the change moves (0 when none).</summary>
        public int UnitPriceMin { get; internal set; }
        public int UnitPriceMax { get; internal set; }

        /// <summary>Gold effect of the row: + earned, − spent.</summary>
        public int GoldDelta { get; internal set; }

        /// <summary>Carried weight effect: + added, − freed.</summary>
        public double WeightDelta { get; internal set; }

        /// <summary>Influence the row gains (prisoner donations).</summary>
        public double InfluenceDelta { get; internal set; }

        /// <summary>Pack / riding rows: the role's target (riding counts as DESIGN §2.3 says).</summary>
        public int? Target { get; internal set; }

        /// <summary>Upgrade rows: the units needed for upgrades.</summary>
        public int? Need { get; internal set; }

        /// <summary>Food rows: the item's resolved price book row.</summary>
        public PriceBookPrices? PriceBook { get; internal set; }

        public PrisonerRowInfo? Prisoner { get; internal set; }
        public TavernRowInfo? Tavern { get; internal set; }

        /// <summary>What a [+] may buy — the walk data for re-pricing (null for rows that never buy items).</summary>
        public TradeLane? BuyLane { get; internal set; }

        /// <summary>What a [−] may sell — the walk data for re-pricing (null for rows that never sell items).</summary>
        public TradeLane? SellLane { get; internal set; }

        /// <summary>The units moved, per stack and direction, in the order first moved.</summary>
        public IReadOnlyList<StackTally> Tallies => Book.Tallies;

        /// <summary>Per-type breakdown (role rows and food rows).</summary>
        public IReadOnlyList<PlanRowLine> Breakdown { get; internal set; } = Array.Empty<PlanRowLine>();

        public bool HasChange => Change != 0;

        /// <summary>Where the row's units are booked; the editor swaps in the book of each re-walk.</summary>
        internal TallyBook Book { get; set; } = new TallyBook();

        /// <summary>Item rows: the party's stacks in this row and how many units of each (role rows: the units in
        /// the role) — the breakdown's Mine.</summary>
        internal IReadOnlyList<KeyValuePair<ItemStack, int>> HeldStacks { get; set; } =
            Array.Empty<KeyValuePair<ItemStack, int>>();

        /// <summary>The plan the row belongs to (its editor).</summary>
        internal StewardPlan? Owner { get; set; }

        /// <summary>
        /// Takes over what a re-plan made of this row (same id) — every number, lane and fact — so the window keeps its
        /// row objects across a live re-plan. Kept: the id, section and type (the same by id), the owner, the touch, and a
        /// touched row's <see cref="SuggestedChange"/> (the steward did not plan it). Every settable property must be listed
        /// here — <c>PlanRowTests</c> checks by reflection that none is forgotten.
        /// </summary>
        internal void AdoptFrom(PlanRow planned)
        {
            Name = planned.Name;
            ItemId = planned.ItemId;
            TroopId = planned.TroopId;
            HeroId = planned.HeroId;
            CategoryId = planned.CategoryId;
            LootGroup = planned.LootGroup;
            Role = planned.Role;
            Mine = planned.Mine;
            Locked = planned.Locked;
            LocksGuard = planned.LocksGuard;
            OverValueCap = planned.OverValueCap;
            Market = planned.Market;
            if (!IsTouched)
                SuggestedChange = planned.SuggestedChange;
            Change = planned.Change;
            MaxBuy = planned.MaxBuy;
            MaxSell = planned.MaxSell;
            UnitPriceMin = planned.UnitPriceMin;
            UnitPriceMax = planned.UnitPriceMax;
            GoldDelta = planned.GoldDelta;
            WeightDelta = planned.WeightDelta;
            InfluenceDelta = planned.InfluenceDelta;
            Target = planned.Target;
            Need = planned.Need;
            PriceBook = planned.PriceBook;
            Prisoner = planned.Prisoner;
            Tavern = planned.Tavern;
            BuyLane = planned.BuyLane;
            SellLane = planned.SellLane;
            Breakdown = planned.Breakdown;
            Book = planned.Book;
            HeldStacks = planned.HeldStacks;
        }

        /// <summary>Change, prices, gold and weight of an item row, from its tallies.</summary>
        internal void SumTallies()
        {
            int change = 0, gold = 0, min = 0, max = 0;
            double weight = 0;
            bool any = false;
            foreach (var t in Book.Tallies)
            {
                int sign = t.Direction == TradeDirection.Buy ? 1 : -1;
                change += sign * t.Count;
                gold -= sign * t.Gold;
                weight += sign * t.Count * t.Stack.UnitWeight;
                if (t.Count == 0) continue;
                min = any ? Math.Min(min, t.MinPrice) : t.MinPrice;
                max = any ? Math.Max(max, t.MaxPrice) : t.MaxPrice;
                any = true;
            }
            Change = change;
            GoldDelta = gold;
            WeightDelta = weight;
            UnitPriceMin = min;
            UnitPriceMax = max;
        }
    }
}
