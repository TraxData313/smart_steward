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

    /// <summary>Why a tavern row cannot be raised (the row is greyed; the tooltip says why).</summary>
    public enum HireBlock
    {
        None,
        CompanionLimit,
        PartyFull,
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

        /// <summary>Unticked in "Prisoners to ransom" — never proposed, the player may still add by hand.</summary>
        public bool IsExcluded { get; internal set; }
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
    }

    /// <summary>
    /// One row of the Suggestion table (DESIGN §1.1). Change is signed: + buy / hire, − sell / ransom /
    /// donate. Names come from the snapshot; role and group rows carry no text (the window labels them
    /// through TextObject ids from <see cref="Role"/>, <see cref="CategoryId"/> and <see cref="LootGroup"/>).
    /// </summary>
    public sealed class PlanRow
    {
        private readonly List<StackTally> _tallies = new List<StackTally>();

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

        /// <summary>Locked units shown apart ("+3 locked") — never sold.</summary>
        public int Locked { get; internal set; }

        /// <summary>Loot only: unlocked pieces above <c>SellLootMaxItemValue</c> — never sold, not in Mine.</summary>
        public int OverValueCap { get; internal set; }

        /// <summary>Market stock (food: all of the item; role rows: eligible units on offer; tavern: on offer);
        /// null = "—".</summary>
        public int? Market { get; internal set; }

        /// <summary>The steward's proposal — what ⟲ resets to.</summary>
        public int SuggestedChange { get; internal set; }
        public int Change { get; internal set; }
        public int Result => Mine + Change;

        /// <summary>Clamps: Change stays within [−MaxSell, +MaxBuy].</summary>
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
        public IReadOnlyList<StackTally> Tallies => _tallies;

        /// <summary>Per-type breakdown (role rows and food rows).</summary>
        public IReadOnlyList<PlanRowLine> Breakdown { get; internal set; } = Array.Empty<PlanRowLine>();

        public bool HasChange => Change != 0;

        internal void Tally(ItemStack stack, TradeDirection direction, int price)
        {
            StackTally? tally = null;
            foreach (var t in _tallies)
                if (t.Direction == direction && string.Equals(t.Stack.Key, stack.Key, StringComparison.Ordinal))
                {
                    tally = t;
                    break;
                }
            if (tally == null)
            {
                tally = new StackTally(stack, direction);
                _tallies.Add(tally);
            }
            tally.Add(price);
        }

        /// <summary>Change, prices, gold and weight of an item row, from its tallies.</summary>
        internal void SumTallies()
        {
            int change = 0, gold = 0, min = 0, max = 0;
            double weight = 0;
            bool any = false;
            foreach (var t in _tallies)
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
