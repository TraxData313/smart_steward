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

        /// <summary>While <see cref="CanTrade"/> is false: why, in the game's own words (the disabled Trade option's text —
        /// war, crime, a raid, nothing on offer…), or the Module's when the game gives none. Shown at the top of the
        /// window and written to the log (playtest round 1).</summary>
        public string? TradeClosedReason { get; set; }

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

        public List<PrisonerStack> Prisoners { get; set; } = new List<PrisonerStack>();

        public PrisonInfo Prison { get; set; } = new PrisonInfo();

        /// <summary>The tavern's offer; null when there is no tavern here or no access to it.</summary>
        public TavernInfo? Tavern { get; set; }

        /// <summary>The troops section (step 16, DESIGN §2.8): one entry per troop type that is on offer here to the player
        /// (the notables' volunteers the game lets him take) or that the party holds as regulars — never every troop in the
        /// game. Heroes are never in it.</summary>
        public List<TroopStack> Troops { get; set; } = new List<TroopStack>();
        /// <summary>The party's load and carrying capacity now, on land and (with ships) at sea, and what one more member,
        /// mount, pack animal or prisoner changes them by — the footer's weight line (round 3, RESEARCH §19).</summary>
        public CarryInfo Carry { get; set; } = new CarryInfo();
    }

    /// <summary>
    /// The party's load and carrying capacity as the game's <c>InventoryCapacityModel</c> counts them (RESEARCH §19) —
    /// the numbers NOW, read from the model itself (so a mod's model is honoured), and the per-unit rates of the vanilla
    /// formula (perks included) that let Core move them with the deal: <see cref="Planning.CarryTotals"/>. All zero =
    /// not read (tests, a failed read): the footer then shows the weight change only.
    /// </summary>
    public sealed class CarryInfo
    {
        /// <summary>Load on land now: <c>CalculateTotalWeightCarried(party, false)</c> — items; animals weigh nothing.</summary>
        public double WeightNow { get; set; }

        /// <summary>Capacity on land now: <c>CalculateInventoryCapacity(party, false)</c>.</summary>
        public double CapacityLandNow { get; set; }

        /// <summary>Land capacity of one more healthy member (a hire): 20 × (1 + Arenicos' Horses) × (1 + Caravan Master).</summary>
        public double LandPerMember { get; set; }

        /// <summary>Land capacity of one more mount (any <c>IsMount</c> animal): 20 × (1 + Caravan Master).</summary>
        public double LandPerMount { get; set; }

        /// <summary>Land capacity of one more pack animal: 100 × (1 + the pack perks) × (1 + Caravan Master).</summary>
        public double LandPerPackAnimal { get; set; }

        /// <summary>Land capacity of one more healthy prisoner — 20 × (1 + Caravan Master) with Forced Labor, else 0.</summary>
        public double LandPerPrisoner { get; set; }

        /// <summary><c>PrisonRoster.TotalHealthyCount</c> — the prisoners Forced Labor counts (the wounded go first when
        /// prisoners leave, so a ransom lowers this only once no wounded are left).</summary>
        public int HealthyPrisoners { get; set; }

        /// <summary>The party owns ships (War Sails): the sea part of the line is shown.</summary>
        public bool HasShips { get; set; }

        /// <summary>Load at sea now: <c>CalculateTotalWeightCarried(party, true)</c> — under War Sails animals weigh (mount
        /// 50, pack animal 30, livestock 20 kg, perks aside) and so does each mounted troop's horse (50 kg).</summary>
        public double WeightAtSeaNow { get; set; }

        /// <summary>Capacity at sea now: <c>CalculateInventoryCapacity(party, true)</c> — base and members, no animals, plus
        /// every ship's cargo (<c>Ship.InventoryCapacity</c>, War Sails).</summary>
        public double CapacitySeaNow { get; set; }

        /// <summary>Sea capacity of one more healthy member: 20 (no perk multiplies it at sea).</summary>
        public double SeaPerMember { get; set; }

        /// <summary>Sea capacity of one more healthy prisoner — 20 with Forced Labor (the game checks the party's LIVE
        /// at-sea state for that perk), else 0.</summary>
        public double SeaPerPrisoner { get; set; }

        /// <summary>The model was read (a capacity above 0) — else the footer shows the weight change only.</summary>
        public bool Known => CapacityLandNow > 0;
    }

    /// <summary>The party itself.</summary>
    public sealed class PartyInfo
    {
        /// <summary><c>MemberRoster.TotalManCount</c> — heroes and wounded included.</summary>
        public int Members { get; set; }

        /// <summary><c>PartyBase.NumberOfMenWithoutHorse</c> — heroes and wounded included (RESEARCH §3).</summary>
        public int Footmen { get; set; }

        /// <summary><c>PartyBase.PartySizeLimit</c> — information only: nothing is blocked by it (round 3); the footer
        /// shows the party after the deal against it, red when over.</summary>
        public int PartySizeLimit { get; set; }

        /// <summary><c>Clan.CompanionLimit</c> minus the clan's companions.</summary>
        public int CompanionSlotsFree { get; set; }

        /// <summary>Food eaten per day now, positive (<c>−MobileParty.FoodChange</c>, perks included).</summary>
        public double DailyFoodUse { get; set; }

        /// <summary>Food the game counts in livestock meat (<c>TotalFood</c> minus the food items) — in the
        /// footer's days, never in the steward's target (livestock is not food to the steward).</summary>
        public int LivestockFoodUnits { get; set; }

        /// <summary>Head of livestock (cows, sheep, hogs… — <c>HorseComponent.IsLiveStock</c>): the steward never trades them,
        /// but the game's herd counts every one (the footer's herd line, step 18, RESEARCH §23).</summary>
        public int LivestockAnimals { get; set; }

        /// <summary>The parties attached to the player's army party — the game's speed model pools their men, footmen and
        /// animals with the party's own for the herd (RESEARCH §23). All zero outside an army.</summary>
        public AttachedParties Attached { get; set; } = new AttachedParties();
    }

    /// <summary>What the parties attached to the player's (army) party bring to the herd rule — read from the game as they
    /// are; a deal never moves them (RESEARCH §23: <c>CalculateLandBaseSpeed</c> adds each attached party's men, footmen,
    /// mounts and herd animals to the leader's).</summary>
    public sealed class AttachedParties
    {
        /// <summary><c>MemberRoster.TotalManCount</c> summed.</summary>
        public int Men { get; set; }

        /// <summary><c>Party.NumberOfMenWithoutHorse</c> summed.</summary>
        public int Footmen { get; set; }

        /// <summary><c>ItemRoster.NumberOfMounts</c> summed.</summary>
        public int Mounts { get; set; }

        /// <summary><c>ItemRoster.NumberOfPackAnimals</c> summed.</summary>
        public int PackAnimals { get; set; }

        /// <summary><c>ItemRoster.NumberOfLivestockAnimals</c> summed.</summary>
        public int Livestock { get; set; }
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

        /// <summary>Null or empty = a plain item. Modified animals (lame, old…) count as held, but are never bought
        /// (DESIGN §2.2–§2.4, step 17).</summary>
        public string? ModifierId { get; set; }

        /// <summary>The modifier's <c>ItemModifier.PriceMultiplier</c> — what the modifier does to the item's worth
        /// (<c>EquipmentElement.ItemValue</c> = value × this); 1 for a plain item. Below 1 = a BAD modifier, the game's own
        /// test (<c>BattleCampaignBehavior</c>'s Metallurgy perk, RESEARCH §22): vanilla's lame horse 0.1, old horse 0.2.
        /// A stack's min sell price is scaled by it, so a lame horse is judged against a lame horse's worth.</summary>
        public double ModifierPriceFactor { get; set; } = 1.0;

        public ItemKind Kind { get; set; }

        /// <summary>The item category id (price walk key; the role of a mount).</summary>
        public string CategoryId { get; set; } = "";

        /// <summary>Only for <see cref="ItemKind.Equipment"/>; see <see cref="LootGroups.FromItemType"/>.</summary>
        public LootGroup LootGroup { get; set; }

        public int Count { get; set; }

        /// <summary>Party side only: locked in the inventory screen. Armour and weapons: never sold, not even counted as
        /// sellable; food and animals: managed like the rest unless LocksProtectFoodAndHorses (<c>Planning.LockRule</c>).</summary>
        public bool IsLocked { get; set; }

        /// <summary>Carried weight of one unit on land as the game counts it (animals weigh 0).</summary>
        public double UnitWeight { get; set; }

        /// <summary>Weight of one unit at sea (<c>GetItemEffectiveWeight(…, isCurrentlyAtSea: true)</c>) — under War Sails
        /// animals weigh too (mount 50, pack 30 kg, perks aside). Read only when the party has ships.</summary>
        public double UnitWeightAtSea { get; set; }

        /// <summary><c>EquipmentElement.ItemValue</c> — modifier included (the loot value cap).</summary>
        public int UnitValue { get; set; }

        /// <summary><c>ItemObject.Value</c> — how far one unit moves its category's in-store value in a
        /// town's price walk, and the order upgrades consume animals in (RESEARCH §4, §8).</summary>
        public int StoreValueStep { get; set; }

        public bool IsModified => !string.IsNullOrEmpty(ModifierId);

        /// <summary>A modifier that makes the item worth less than a plain one (<see cref="GameRules.IsBadModifier"/>) — for a
        /// horse: lame or old (step 17: never bought; with ReplaceLameHorses sold and replaced by a healthy one).</summary>
        public bool HasBadModifier => IsModified && GameRules.IsBadModifier(ModifierPriceFactor);
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

        /// <summary><c>CharacterObject.IsMounted</c> of the hero — a horse in his battle equipment's horse slot (RESEARCH §3).
        /// Not mounted = one more footman after the hire (the live re-plan, step 15).</summary>
        public bool IsMounted { get; set; }
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

        /// <summary>What one man adds to the load at sea — his horse, when the troop rides (War Sails weighs every mounted
        /// troop's horse at sea); 0 on foot or without ships.</summary>
        public double SeaWeightPerMan { get; set; }

        /// <summary><c>CharacterObject.IsMounted</c> of the troop type — the game's own "man with a horse"
        /// (<c>PartyBase.NumberOfMenWithHorse</c>): a troop whose default formation class is cavalry or horse archer (its XML
        /// <c>default_group</c>), RESEARCH §3. Not mounted = every man hired is one more footman who needs a riding mount (the
        /// live re-plan, step 15). The same field for the recruits of step 16.</summary>
        public bool IsMounted { get; set; }
    }

    /// <summary>
    /// One troop type of the troops section (step 16, DESIGN §2.8, RESEARCH §21): what the party holds of it and what the
    /// settlement's notables offer the player of it. The party's regulars come from <c>MemberRoster</c> (heroes never); the
    /// offer is vanilla's recruit screen: every notable who <c>CanHaveRecruits</c>, each of the six <c>VolunteerTypes</c>
    /// slots holding a troop that <c>HeroHelper.HeroCanRecruitFromHero(MainHero, notable, index)</c> opens to him.
    /// </summary>
    public sealed class TroopStack
    {
        public string TroopId { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary><c>CharacterObject.Tier</c> — the game's tier (0–6 in vanilla: <c>ceil((level − 5) / 5)</c> clamped to
        /// <c>MaxCharacterTier</c>, RESEARCH §24): shown before the troop's name and the troops section's order (step 18).</summary>
        public int Tier { get; set; }

        /// <summary>Men of this type in the party now, wounded included (<c>TroopRosterElement.Number</c>) — the row's Mine.</summary>
        public int InParty { get; set; }

        /// <summary>Of <see cref="InParty"/>, the wounded — a dismissal takes them first, like vanilla's party screen.</summary>
        public int Wounded { get; set; }

        /// <summary>The party screen lets these men go: a regular that is not bound to a quest
        /// (<c>!IsNotTransferableInPartyScreen</c>). False → the row's [−] is never live.</summary>
        public bool CanDismiss { get; set; }

        /// <summary>Volunteers of this type the player may recruit here now (0 = not on offer): the notables' slots the game's
        /// volunteer model opens to him, only where the game's recruit gate (<c>SettlementAction.RecruitTroops</c>) is open.</summary>
        public int OnOffer { get; set; }

        /// <summary>The recruitment cost per man — <c>PartyWageModel.GetTroopRecruitmentCost(troop, MainHero)</c>, perks included;
        /// the same for every man of the type, whichever notable offers him.</summary>
        public int PricePerMan { get; set; }

        /// <summary><c>CharacterObject.TroopWage</c> — the daily wage of one man.</summary>
        public int WagePerMan { get; set; }

        /// <summary><c>CharacterObject.IsMounted</c> — the game's "man with a horse" (RESEARCH §3); not mounted = a footman.</summary>
        public bool IsMounted { get; set; }

        /// <summary>What one man adds to the load at sea — his horse, when the type rides (War Sails); 0 on foot or without ships.</summary>
        public double SeaWeightPerMan { get; set; }
    }
}
