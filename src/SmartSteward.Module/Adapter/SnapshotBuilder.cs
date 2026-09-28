using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SmartSteward.Core.Snapshot;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CampaignBehaviors;
using TaleWorlds.CampaignSystem.ComponentInterfaces;
using TaleWorlds.CampaignSystem.Extensions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

namespace SmartSteward.Adapter
{
    /// <summary>
    /// Reads the live game into a <see cref="StewardSnapshot"/> at the party's town or village (DESIGN §5, PLAN
    /// step 6). Every call is from docs/RESEARCH.md (§1–§8, and the step 6 facts); the pure rules it applies live
    /// in Core's <see cref="GameRules"/>. Thin by design: no planning happens here.
    /// </summary>
    internal static class SnapshotBuilder
    {
        /// <summary>The snapshot of the party at <paramref name="settlement"/>; null (with the reason — player-facing, so
        /// through TextObject ids) anywhere the steward does not work — castles, hideouts, the open map.</summary>
        public static GameVisit? Build(Settlement? settlement, out string whyNot)
        {
            whyNot = "";
            var main = MobileParty.MainParty;
            var hero = Hero.MainHero;
            if (settlement == null || main == null || hero == null || Campaign.Current == null)
            {
                whyNot = UI.UiText.S("ss_why_no_settlement", "not in a settlement");
                return null;
            }
            bool isTown = settlement.IsTown;
            if (!isTown && !settlement.IsVillage)
            {
                whyNot = UI.UiText.S("ss_why_not_town_or_village", "not a town or a village");
                return null;
            }

            var snap = new StewardSnapshot
            {
                SettlementKind = isTown ? SettlementKind.Town : SettlementKind.Village,
                PlayerGold = hero.Gold,
                MarketGold = settlement.SettlementComponent?.Gold ?? 0,
            };
            var elements = new Dictionary<string, EquipmentElement>(StringComparer.Ordinal);

            snap.CanTrade = CanTradeNow(settlement, out string? closedReason);
            snap.TradeClosedReason = snap.CanTrade ? null : closedReason;
            ReadParty(snap, main);
            ReadItems(main.ItemRoster, snap.Inventory, elements, InventoryLocks());
            if (snap.CanTrade)
                ReadItems(settlement.ItemRoster, snap.Market, elements, null);
            else
                snap.MarketGold = 0; // no access, no market rows (DESIGN §3.5)
            ReadAverages(snap, settlement, elements, main);
            ReadUpgrades(snap, main);
            ReadPrisoners(snap, settlement, main, hero);
            if (isTown)
                ReadTavern(snap, settlement, main, hero);
            return new GameVisit(settlement, snap, elements);
        }

        /// <summary>The game lets the player trade here right now: the town's "Trade" / the village's "Buy products"
        /// gate (<c>SettlementAccessModel.CanMainHeroDoSettlementAction(…, Trade, …)</c> — war, crime, a raid, a
        /// village with nothing to sell), and never in a looted village.</summary>
        public static bool CanTradeNow(Settlement settlement) => CanTradeNow(settlement, out _);

        /// <summary><see cref="CanTradeNow(Settlement)"/>, and when closed, why: the game's own disabled text for the Trade
        /// option (<c>DefaultSettlementAccessModel.CanMainHeroTrade</c>: "You cannot trade with a hostile village.",
        /// "There are no available products right now.", "Village shop is not available right now.", the disguise
        /// perk…) — or ours where the game gives none (looted, being raided, anything else).</summary>
        public static bool CanTradeNow(Settlement settlement, out string? whyClosed)
        {
            whyClosed = null;
            try
            {
                if (settlement.IsVillage && settlement.Village.VillageState == Village.VillageStates.Looted)
                {
                    whyClosed = UI.UiText.S("ss_why_looted", "This village has been looted.");
                    return false;
                }
                if (Campaign.Current.Models.SettlementAccessModel.CanMainHeroDoSettlementAction(settlement,
                        SettlementAccessModel.SettlementAction.Trade, out _, out var disabledText))
                    return true;
                string? text = disabledText?.ToString();
                whyClosed = !string.IsNullOrWhiteSpace(text) ? text
                    : settlement.IsVillage && settlement.Village.VillageState == Village.VillageStates.BeingRaided
                        ? UI.UiText.S("ss_why_raided", "The village is being raided.")
                        : UI.UiText.S("ss_why_market_closed", "Trading is not possible here right now.");
                return false;
            }
            catch (Exception ex)
            {
                ModLog.Error("snapshot", "reading trade access", ex);
                whyClosed = UI.UiText.S("ss_why_market_closed", "Trading is not possible here right now.");
                return false;
            }
        }

        /// <summary>The player may enter this location (<c>CanMainHeroAccessLocation</c>) — "tavern" is the tavern
        /// district: the ransom broker, the wanderers and the mercenaries.</summary>
        public static bool CanAccess(Settlement settlement, string locationId)
        {
            try
            {
                return Campaign.Current.Models.SettlementAccessModel.CanMainHeroAccessLocation(settlement, locationId,
                    out _, out _);
            }
            catch (Exception ex)
            {
                ModLog.Error("snapshot", "reading access to " + locationId, ex);
                return false;
            }
        }

        /// <summary>Donating is allowed here right now (DESIGN §2.5, <see cref="GameRules.DonateAllowed"/>).</summary>
        public static bool DonateAllowedNow(Settlement settlement)
        {
            var hero = Hero.MainHero;
            return GameRules.DonateAllowed(settlement.IsTown,
                settlement.MapFaction != null && settlement.MapFaction == hero.MapFaction,
                settlement.OwnerClan == Clan.PlayerClan,
                DungeonAccess(settlement));
        }

        /// <summary>Free places in the dungeon: <c>PrisonerSizeLimit − NumberOfPrisoners</c> (men, not stacks —
        /// RESEARCH §5).</summary>
        public static int DungeonRoom(Settlement settlement) =>
            settlement.Party == null ? 0 : Math.Max(0, settlement.Party.PrisonerSizeLimit - settlement.Party.NumberOfPrisoners);

        public static int CompanionSlotsFree()
        {
            var clan = Clan.PlayerClan;
            return clan == null ? 0 : Math.Max(0, clan.CompanionLimit - clan.Companions.Count);
        }

        /// <summary>A wanderer sitting in this town's tavern, free to hire (the tavern placement rule of
        /// <c>DefaultHeroAgentLocationModel</c>, vanilla's hire conditions and ImmersiveAI's checks — RESEARCH §7).</summary>
        public static bool IsWandererForHire(Hero? hero, Settlement settlement) =>
            hero != null && hero != Hero.MainHero && hero.IsAlive && !hero.IsPrisoner && hero.IsWanderer
            && !hero.IsPlayerCompanion && hero.CompanionOf == null && hero.Clan == null && hero.PartyBelongedTo == null
            && hero.CurrentSettlement == settlement
            && (hero.GovernorOf == null || hero.GovernorOf != settlement.Town);

        /// <summary>The inventory's lock ids (item + modifier), saved in the save game (RESEARCH §6).</summary>
        public static HashSet<string> InventoryLocks() =>
            ReadLocks(t => t.GetInventoryLocks());

        /// <summary>The party screen's prisoner locks (troop ids) — vanilla's ransom skips them (RESEARCH §5).</summary>
        public static HashSet<string> PrisonerLocks() =>
            ReadLocks(t => t.GetPartyPrisonerLocks());

        /// <summary>A one-line summary of a snapshot for the log.</summary>
        public static string Describe(StewardSnapshot s)
        {
            var inv = CultureInfo.InvariantCulture;
            string Kinds(List<ItemStack> stacks) => string.Join(", ",
                stacks.GroupBy(x => x.Kind).OrderBy(g => g.Key)
                    .Select(g => g.Key + " " + g.Count().ToString(inv) + "/" + g.Sum(x => x.Count).ToString(inv)));
            return s.SettlementKind + (s.CanTrade ? "" : " (NO TRADE: " + (s.TradeClosedReason ?? "no reason given") + ")")
                   + ", gold " + s.PlayerGold.ToString("N0", inv)
                   + ", market gold " + s.MarketGold.ToString("N0", inv) + "; party " + s.Party.Members.ToString(inv)
                   + "/" + s.Party.PartySizeLimit.ToString(inv) + " (footmen " + s.Party.Footmen.ToString(inv)
                   + ", companion slots " + s.Party.CompanionSlotsFree.ToString(inv) + ", food/day "
                   + s.Party.DailyFoodUse.ToString("0.##", inv) + ", livestock food " + s.Party.LivestockFoodUnits.ToString(inv)
                   + "); inventory stacks/units [" + Kinds(s.Inventory) + "]; market [" + Kinds(s.Market) + "]; averages "
                   + s.AveragePrices.Count.ToString(inv) + "; upgrade stacks " + s.Upgrades.Count.ToString(inv)
                   + "; prisoners " + s.Prisoners.Sum(p => p.Count).ToString(inv) + " in " + s.Prisoners.Count.ToString(inv)
                   + " stacks (ransom " + (s.Prison.CanRansom ? "yes" : "no") + ", donate "
                   + (s.Prison.DonateAllowed ? "yes, room " + s.Prison.DungeonRoom.ToString(inv) : "no") + "); tavern "
                   + (s.Tavern == null ? "none"
                       : s.Tavern.Wanderers.Count.ToString(inv) + " wanderers, "
                         + (s.Tavern.Mercenaries == null ? "no band" : s.Tavern.Mercenaries.Available.ToString(inv) + " " + s.Tavern.Mercenaries.Name));
        }

        private static void ReadParty(StewardSnapshot snap, MobileParty main)
        {
            snap.Party.Members = main.MemberRoster.TotalManCount;
            // The speed model's footmen: every member (heroes and wounded too) without a horse (RESEARCH §3).
            snap.Party.Footmen = main.Party.NumberOfMenWithoutHorse;
            snap.Party.PartySizeLimit = main.Party.PartySizeLimit;
            snap.Party.CompanionSlotsFree = CompanionSlotsFree();
            snap.Party.DailyFoodUse = Math.Max(0, -main.FoodChange);
            int livestockMeat = 0;
            var roster = main.ItemRoster;
            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                var horse = element.EquipmentElement.Item?.HorseComponent;
                if (horse != null && horse.IsLiveStock && element.Amount > 0)
                    livestockMeat += element.Amount * horse.MeatCount; // what ItemRoster.TotalFood adds for it
            }
            snap.Party.LivestockFoodUnits = livestockMeat;
        }

        private static void ReadItems(ItemRoster roster, List<ItemStack> into, Dictionary<string, EquipmentElement> elements,
            HashSet<string>? locks)
        {
            for (int i = 0; i < roster.Count; i++)
            {
                var element = roster.GetElementCopyAtIndex(i);
                var el = element.EquipmentElement;
                var item = el.Item;
                if (item == null || element.Amount <= 0)
                    continue;
                string itemId = item.StringId;
                string? modifierId = el.ItemModifier?.StringId;
                string key = GameRules.StackKey(itemId, modifierId);
                var horse = item.HorseComponent;
                var group = LootGroups.FromItemType(item.ItemType.ToString());
                var kind = GameRules.Classify(item.IsFood, horse != null, horse != null && horse.IsPackAnimal,
                    horse != null && horse.IsMount, group, el.IsQuestItem, item.IsTransferable);
                into.Add(new ItemStack
                {
                    Key = key,
                    ItemId = itemId,
                    Name = el.GetModifiedItemName()?.ToString() ?? itemId,
                    ModifierId = modifierId,
                    Kind = kind,
                    CategoryId = item.ItemCategory?.StringId ?? "",
                    LootGroup = kind == ItemKind.Equipment ? group : LootGroup.None,
                    Count = element.Amount,
                    IsLocked = locks != null && locks.Contains(GameRules.LockId(itemId, modifierId)),
                    // DefaultInventoryCapacityModel.GetItemEffectiveWeight: animals weigh nothing carried
                    UnitWeight = horse != null ? 0 : el.GetEquipmentElementWeight(),
                    UnitValue = el.ItemValue,
                    // TownMarketData.OnTownInventoryUpdated moves InStoreValue by Item.Value per unit
                    StoreValueStep = item.Value,
                });
                if (!elements.ContainsKey(key))
                    elements[key] = el;
            }
        }

        /// <summary>
        /// The price book's placeholders (DESIGN §4.2) for FOOD AND ANIMALS ONLY — never armour or weapons: the
        /// item's value × its category's mean price factor over the other towns (the inventory's own average,
        /// <c>InventoryLogic.InitializeCategoryAverages</c>), run through the trade penalty of each side with NO
        /// merchant — an average town at peace (no village, war or network terms).
        /// </summary>
        private static void ReadAverages(StewardSnapshot snap, Settlement settlement, Dictionary<string, EquipmentElement> elements,
            MobileParty main)
        {
            var factors = new Dictionary<ItemCategory, float>();
            foreach (var stack in snap.Inventory.Concat(snap.Market))
            {
                if (stack.Kind != ItemKind.Food && stack.Kind != ItemKind.PackAnimal && stack.Kind != ItemKind.Mount)
                    continue;
                if (snap.AveragePrices.ContainsKey(stack.ItemId) || !elements.TryGetValue(stack.Key, out var el))
                    continue;
                var average = AverageOf(el.Item, settlement, main, factors);
                if (average != null)
                    snap.AveragePrices[stack.ItemId] = average;
            }
        }

        /// <summary>One item's placeholder prices (DESIGN §4.2) — the snapshot's and the Prices tab's (step 7) come from
        /// here, so they always agree. <paramref name="factors"/> caches each category's mean factor. Null for an item
        /// without a category. Callers pass FOOD AND ANIMALS ONLY.</summary>
        internal static AveragePrices? AverageOf(ItemObject? item, Settlement settlement, MobileParty main,
            Dictionary<ItemCategory, float> factors)
        {
            var category = item?.ItemCategory;
            if (item == null || category == null)
                return null;
            var model = Campaign.Current.Models.TradeItemPriceFactorModel;
            if (!factors.TryGetValue(category, out float factor))
            {
                var here = settlement.IsVillage ? settlement.Village?.Bound?.Town : settlement.Town;
                factor = GameRules.MeanFactor(Town.AllTowns.Where(t => t != here)
                    .Select(t => t.MarketData.GetPriceFactor(category)));
                factors[category] = factor;
            }
            float buyPenalty = model.GetTradePenalty(item, main, null, false, 0f, 0f, 0f);
            float sellPenalty = model.GetTradePenalty(item, main, null, true, 0f, 0f, 0f);
            return new AveragePrices(
                GameRules.AverageBuyPrice(item.Value, factor, buyPenalty),
                GameRules.AverageSellPrice(item.Value, factor, sellPenalty));
        }

        /// <summary>Every troop stack with upgrade targets, with the party screen's ready count per target
        /// (RESEARCH §4) — the requirement sits on the TARGET troop.</summary>
        private static void ReadUpgrades(StewardSnapshot snap, MobileParty main)
        {
            var model = Campaign.Current.Models.PartyTroopUpgradeModel;
            foreach (var element in main.MemberRoster.GetTroopRoster())
            {
                var troop = element.Character;
                if (troop == null || troop.IsHero || element.Number <= 0)
                    continue;
                var targets = troop.UpgradeTargets;
                if (targets == null || targets.Length == 0)
                    continue;
                var stack = new UpgradeStack { TroopId = troop.StringId, Count = element.Number };
                for (int i = 0; i < targets.Length; i++)
                {
                    var target = targets[i];
                    if (target == null)
                        continue;
                    bool allowed = model.DoesPartyHaveRequiredPerksForUpgrade(main.Party, troop, target, out _);
                    if (allowed && troop.Culture != null && troop.Culture.IsBandit)
                        allowed = model.CanPartyUpgradeTroopToTarget(main.Party, troop, target);
                    int cost = troop.GetUpgradeXpCost(main.Party, i);
                    stack.Targets.Add(new UpgradeTarget
                    {
                        TroopId = target.StringId,
                        RequiredCategoryId = target.UpgradeRequiresItemFromCategory?.StringId,
                        ReadyCount = GameRules.UpgradeReadyCount(target.Level, troop.Level, element.Xp, cost,
                            element.Number, allowed),
                    });
                }
                snap.Upgrades.Add(stack);
            }
        }

        private static void ReadPrisoners(StewardSnapshot snap, Settlement settlement, MobileParty main, Hero hero)
        {
            bool isTown = settlement.IsTown;
            var locks = PrisonerLocks();
            var models = Campaign.Current.Models;
            var kingdom = Clan.PlayerClan?.Kingdom;
            bool coronae = kingdom != null && kingdom.ActivePolicies.Contains(DefaultPolicies.MilitaryCoronae);
            foreach (var element in main.PrisonRoster.GetTroopRoster())
            {
                var c = element.Character;
                if (c == null || element.Number <= 0 || (c.IsHero && c.HeroObject == hero))
                    continue;
                float influence = isTown
                    ? models.PrisonerDonationModel.CalculateInfluenceGainAfterPrisonerDonation(main.Party, c, settlement)
                    : 0f;
                snap.Prisoners.Add(new PrisonerStack
                {
                    TroopId = c.StringId,
                    Name = c.Name?.ToString() ?? c.StringId,
                    Count = element.Number,
                    RansomValue = models.RansomValueCalculationModel.PrisonerRansomValue(c, hero),
                    InfluencePerMan = GameRules.DonationInfluence(influence, kingdom != null, coronae),
                    IsHero = c.IsHero,
                    IsLocked = locks.Contains(c.StringId),
                });
            }
            snap.Prison = new PrisonInfo
            {
                // the ransom broker lives in the tavern district (town_backstreet)
                CanRansom = isTown && CanAccess(settlement, "tavern"),
                DonateAllowed = isTown && DonateAllowedNow(settlement),
                DungeonRoom = isTown ? DungeonRoom(settlement) : 0,
            };
        }

        private static void ReadTavern(StewardSnapshot snap, Settlement settlement, MobileParty main, Hero hero)
        {
            if (!CanAccess(settlement, "tavern"))
                return;
            var tavern = new TavernInfo();
            var hiring = Campaign.Current.Models.CompanionHiringPriceCalculationModel;
            foreach (var h in settlement.HeroesWithoutParty)
            {
                if (!IsWandererForHire(h, settlement))
                    continue;
                tavern.Wanderers.Add(new WandererForHire
                {
                    HeroId = h.StringId,
                    Name = h.Name?.ToString() ?? h.StringId,
                    HirePrice = hiring.GetCompanionHiringPrice(h),
                    DailyWage = h.CharacterObject.TroopWage,
                    SkillTag = SkillTag(h),
                });
            }

            var data = Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>()?.GetMercenaryData(settlement.Town);
            if (data != null && data.HasAvailableMercenary())
            {
                var troop = data.TroopType;
                tavern.Mercenaries = new MercenaryOffer
                {
                    TroopId = troop.StringId,
                    Name = troop.Name?.ToString() ?? troop.StringId,
                    Available = data.Number,
                    PricePerMan = Campaign.Current.Models.PartyWageModel.GetTroopRecruitmentCost(troop, hero).RoundedResultNumber,
                    WagePerMan = troop.TroopWage,
                    InParty = main.MemberRoster.GetTroopCount(troop),
                };
            }
            snap.Tavern = tavern;
        }

        private static string? SkillTag(Hero hero)
        {
            try
            {
                return GameRules.SkillTag(Skills.All.Select(s =>
                    new KeyValuePair<string, int>(s.Name?.ToString() ?? s.StringId, hero.GetSkillValue(s))));
            }
            catch
            {
                return null; // a courtesy tag — never worth a failed snapshot
            }
        }

        private static HashSet<string> ReadLocks(Func<IViewDataTracker, IEnumerable<string>> read)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                var tracker = Campaign.Current?.GetCampaignBehavior<IViewDataTracker>();
                if (tracker != null)
                    foreach (var id in read(tracker) ?? Enumerable.Empty<string>())
                        if (id != null)
                            set.Add(id);
            }
            catch (Exception ex)
            {
                ModLog.Error("snapshot", "reading locks", ex);
            }
            return set;
        }

        /// <summary>Dungeon access for a donation (<c>CanMainHeroEnterDungeon</c>): full access, or limited access
        /// with the bribe paid — vanilla's <c>game_menu_go_dungeon_on_condition</c>.</summary>
        private static bool DungeonAccess(Settlement settlement)
        {
            try
            {
                var models = Campaign.Current.Models;
                models.SettlementAccessModel.CanMainHeroEnterDungeon(settlement, out var details);
                if (details.AccessLevel == SettlementAccessModel.AccessLevel.FullAccess)
                    return true;
                return details.AccessLevel == SettlementAccessModel.AccessLevel.LimitedAccess
                       && settlement.BribePaid >= models.BribeCalculationModel.GetBribeToEnterDungeon(settlement);
            }
            catch (Exception ex)
            {
                ModLog.Error("snapshot", "reading dungeon access", ex);
                return false;
            }
        }
    }
}
