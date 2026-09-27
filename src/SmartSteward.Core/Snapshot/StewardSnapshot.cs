using System;
using System.Collections.Generic;

namespace SmartSteward.Core.Snapshot
{
    public enum SettlementKind
    {
        Town,
        Village,
    }

    /// <summary>
    /// Everything the planners need, read from the game at one moment (DESIGN §5). Plain data — the
    /// Module fills it in step 6; tests build it by hand. Nothing here references the game.
    /// </summary>
    public sealed class StewardSnapshot
    {
        public SettlementKind SettlementKind { get; set; } = SettlementKind.Town;

        /// <summary>The game lets the player trade here (<c>SettlementAction.Trade</c> access). False →
        /// no market rows at all: no food, horses or loot (DESIGN §3.5).</summary>
        public bool CanTrade { get; set; } = true;

        public int PlayerGold { get; set; }

        /// <summary>The settlement's purse — the most it can pay for what the party sells.</summary>
        public int MarketGold { get; set; }

        public PartyInfo Party { get; set; } = new PartyInfo();

        /// <summary>The party's items, one stack per item + modifier (as the game's roster keeps them).</summary>
        public List<ItemStack> Inventory { get; set; } = new List<ItemStack>();

        /// <summary>The settlement's stock, one stack per item + modifier.</summary>
        public List<ItemStack> Market { get; set; } = new List<ItemStack>();

        /// <summary>Price-book placeholders (DESIGN §4.2), per item id — ONLY food and animals. The Module
        /// never computes them for armour or weapons, and Core never reads them for anything else.</summary>
        public Dictionary<string, AveragePrices> AveragePrices { get; set; } =
            new Dictionary<string, AveragePrices>(StringComparer.Ordinal);

        /// <summary>Every party stack that has an upgrade target (ready or not) — the war-mount need.</summary>
        public List<UpgradeStack> Upgrades { get; set; } = new List<UpgradeStack>();

        public List<PrisonerStack> Prisoners { get; set; } = new List<PrisonerStack>();

        public PrisonInfo Prison { get; set; } = new PrisonInfo();

        /// <summary>The tavern's offer; null when there is no tavern here or no access to it.</summary>
        public TavernInfo? Tavern { get; set; }
    }

    /// <summary>The party itself.</summary>
    public sealed class PartyInfo
    {
        /// <summary><c>MemberRoster.TotalManCount</c> — heroes and wounded included.</summary>
        public int Members { get; set; }

        /// <summary><c>PartyBase.NumberOfMenWithoutHorse</c> — heroes and wounded included (RESEARCH §3).</summary>
        public int Footmen { get; set; }

        public int PartySizeLimit { get; set; }

        /// <summary><c>Clan.CompanionLimit</c> minus the clan's companions.</summary>
        public int CompanionSlotsFree { get; set; }

        /// <summary>Food eaten per day now, positive (<c>−MobileParty.FoodChange</c>, perks included).</summary>
        public double DailyFoodUse { get; set; }

        /// <summary>Food the game counts in livestock meat (<c>TotalFood</c> minus the food items) — in the
        /// footer's days, never in the steward's target (livestock is not food to the steward).</summary>
        public int LivestockFoodUnits { get; set; }

        /// <summary>Free places under the party size limit (never negative).</summary>
        public int Room => Math.Max(0, PartySizeLimit - Members);
    }

    public enum ItemKind
    {
        /// <summary>Everything the steward leaves alone in V1: livestock, trade goods that are not food,
        /// banners, books, quest items, non-transferable items.</summary>
        Other,

        /// <summary><c>ItemObject.IsFood</c>.</summary>
        Food,

        /// <summary><c>HorseComponent.IsPackAnimal</c> (category sumpter_horse).</summary>
        PackAnimal,

        /// <summary><c>HorseComponent.IsMount</c> — rideable, not a pack animal; its role comes from its category.</summary>
        Mount,

        /// <summary>Sellable equipment in one of the loot groups (<see cref="ItemStack.LootGroup"/>).</summary>
        Equipment,
    }

    /// <summary>The loot groups of DESIGN §2.6, in display order.</summary>
    public enum LootGroup
    {
        None,
        Armour,
        MeleeWeapons,
        Ranged,
        Shields,
    }

    /// <summary>
    /// One roster element: an item + modifier, on the party's side or the market's. A lock id is item +
    /// modifier (RESEARCH §6), so a stack is locked whole or not at all.
    /// </summary>
    public sealed class ItemStack
    {
        /// <summary>Unique per item + modifier, and the SAME for the party's and the market's stack of one
        /// element — the price oracle and the executor map it back to the game's EquipmentElement.</summary>
        public string Key { get; set; } = "";

        public string ItemId { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>Null or empty = a plain item. Modified animals (lame, spirited…) count as held, but are
        /// never bought.</summary>
        public string? ModifierId { get; set; }

        public ItemKind Kind { get; set; }

        /// <summary>The item category id (price walk key; the role of a mount).</summary>
        public string CategoryId { get; set; } = "";

        /// <summary>Only for <see cref="ItemKind.Equipment"/>; see <see cref="LootGroups.FromItemType"/>.</summary>
        public LootGroup LootGroup { get; set; }

        public int Count { get; set; }

        /// <summary>Party side only: locked in the inventory screen — never sold, not even counted as sellable.</summary>
        public bool IsLocked { get; set; }

        /// <summary>Carried weight of one unit as the game counts it (animals weigh 0).</summary>
        public double UnitWeight { get; set; }

        /// <summary><c>EquipmentElement.ItemValue</c> — modifier included (the loot value cap).</summary>
        public int UnitValue { get; set; }

        /// <summary><c>ItemObject.Value</c> — how far one unit moves its category's in-store value in a
        /// town's price walk, and the order upgrades consume animals in (RESEARCH §4, §8).</summary>
        public int StoreValueStep { get; set; }

        public bool IsModified => !string.IsNullOrEmpty(ModifierId);
        public int LockedCount => IsLocked ? Count : 0;
        public int UnlockedCount => IsLocked ? 0 : Count;
    }

    /// <summary>Placeholder prices of one item (DESIGN §4.2): what one unit costs / fetches in an
    /// AVERAGE town, trade penalty included.</summary>
    public sealed class AveragePrices
    {
        public AveragePrices()
        {
        }

        public AveragePrices(int buy, int sell)
        {
            Buy = buy;
            Sell = sell;
        }

        public int Buy { get; set; }
        public int Sell { get; set; }
    }

    /// <summary>A party troop stack with its upgrade targets. Both targets share the stack's XP pool, so a
    /// stack is counted once (RESEARCH §4).</summary>
    public sealed class UpgradeStack
    {
        public string TroopId { get; set; } = "";
        public int Count { get; set; }
        public List<UpgradeTarget> Targets { get; set; } = new List<UpgradeTarget>();
    }

    public sealed class UpgradeTarget
    {
        public string TroopId { get; set; } = "";

        /// <summary><c>UpgradeRequiresItemFromCategory</c> of the TARGET troop; null/empty = needs no animal.</summary>
        public string? RequiredCategoryId { get; set; }

        /// <summary>The party screen's count: <c>min(floor(stackXp / xpCost), stack size)</c> when the target's
        /// level ≥ the troop's — NOT capped by the animals held (those are what the steward fills).</summary>
        public int ReadyCount { get; set; }
    }

    public sealed class PrisonerStack
    {
        public string TroopId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Count { get; set; }

        /// <summary>Gold per man from the ransom broker (<c>PrisonerRansomValue(c, MainHero)</c>).</summary>
        public int RansomValue { get; set; }

        /// <summary>Influence per man if donated (<c>0.2 × ransom^0.4</c>, policies included — the Module's number).</summary>
        public double InfluencePerMan { get; set; }

        public bool IsHero { get; set; }

        /// <summary>Locked in the party screen — never ransomed nor donated, like vanilla (RESEARCH §5).</summary>
        public bool IsLocked { get; set; }
    }

    public sealed class PrisonInfo
    {
        /// <summary>A town whose tavern district (the ransom broker) the player may enter.</summary>
        public bool CanRansom { get; set; }

        /// <summary>DESIGN §2.5: own faction, not own clan, dungeon access.</summary>
        public bool DonateAllowed { get; set; }

        /// <summary><c>PrisonerSizeLimit − NumberOfPrisoners</c> of the settlement.</summary>
        public int DungeonRoom { get; set; }
    }

    public sealed class TavernInfo
    {
        public List<WandererForHire> Wanderers { get; set; } = new List<WandererForHire>();

        /// <summary>The tavern's mercenary band; null when there is none.</summary>
        public MercenaryOffer? Mercenaries { get; set; }
    }

    public sealed class WandererForHire
    {
        public string HeroId { get; set; } = "";
        public string Name { get; set; } = "";
        public int HirePrice { get; set; }
        public int DailyWage { get; set; }

        /// <summary>Optional short tag of the best skills, when cheap to show.</summary>
        public string? SkillTag { get; set; }
    }

    public sealed class MercenaryOffer
    {
        public string TroopId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Available { get; set; }
        public int PricePerMan { get; set; }
        public int WagePerMan { get; set; }

        /// <summary>How many of this troop the party already has (the row's Mine).</summary>
        public int InParty { get; set; }
    }
}
