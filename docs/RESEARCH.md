# Smart Steward — Research (verified game API, v1.4.8)

PLAN step 2, 2026.09.27. Every fact below was read in the decompiled game (or in the game's own
data / DLLs), not recalled. **Never write game glue from memory — check here first, add what you
verify.**

**Source.** `..\reference\game-decompiled-1.4.8\` — decompiled fresh for this step with ilspycmd
8.2 from the installed game (v1.4.8 per `Modules\Native\SubModule.xml`, DLL timestamps
2026-09-18; see its `VERSION.txt`). The older shared `..\reference\game-decompiled\` differs from
it in only 4 CampaignSystem files (DestroyPartyAction, NotablesCampaignBehavior, NavigationCache,
MapEvent) — usable, but cite the 1.4.8 copy. Paths below are relative to
`game-decompiled-1.4.8\`; **`CS\`** = `TaleWorlds.CampaignSystem\`,
**`CSVM\`** = `TaleWorlds.CampaignSystem.ViewModelCollection\`. Game data: `Modules\SandBoxCore\
ModuleData\items\horses_and_others.xml`, `Modules\SandBoxCore\ModuleData\spnpccharacters.xml`.
MCM: `..\reference\MCMv5-5.12.3-decompiled\`.

---

## 1. Items — food, animals, categories (DESIGN terms, §2.1–§2.4)

| What | Exact API | File |
|---|---|---|
| Food | `bool ItemObject.IsFood { get; }` (XML `IsFood="true"`, or `InitializeTradeGood(..., isFood: true)`) | `TaleWorlds.Core\TaleWorlds.Core\ItemObject.cs` |
| Trade good | `bool ItemObject.IsTradeGood => ItemType == ItemTypeEnum.Goods` | same |
| Horse component | `HorseComponent ItemObject.HorseComponent`, `bool HasHorseComponent` | same |
| Rideable | `bool HorseComponent.IsRideable` (XML `is_mountable`) | `TaleWorlds.Core\TaleWorlds.Core\HorseComponent.cs` |
| Pack animal | `bool HorseComponent.IsPackAnimal` (XML `is_pack_animal`) | same |
| Mount | `bool HorseComponent.IsMount => IsRideable && !IsPackAnimal` | same |
| Livestock | `bool HorseComponent.IsLiveStock => !IsRideable && !IsPackAnimal`; `int MeatCount` | same |
| Category | `ItemCategory ItemObject.ItemCategory` (string id `category.StringId`); flags `IsTradeGood`, `IsAnimal` | `TaleWorlds.Core\TaleWorlds.Core\ItemCategory.cs` |
| Weight | `float ItemObject.Weight`; per element `EquipmentElement.GetEquipmentElementWeight()` | ItemObject.cs |
| Base value | `int ItemObject.Value` (horses have no XML value — the ItemValueModel computes it); with modifier `int EquipmentElement.ItemValue` | ItemObject.cs |
| Item type | `ItemObject.ItemTypeEnum ItemObject.ItemType` | ItemObject.cs |

**Vanilla food (9 types)** — `IsFood = true`: `grain`, `meat` (both built in code,
`CS\TaleWorlds.CampaignSystem\DefaultItems.cs`), `fish`, `cheese`, `butter`, `grape`,
`date_fruit`, `olives`, `beer` (XML). NOT food: `wine`, `oil` (explicit `IsFood="false"`). No War
Sails food items.

**Every animal category in vanilla data** (category ids from `TaleWorlds.Core\...\DefaultItemCategories.cs`,
items from `horses_and_others.xml`):

| Category id | Items | Flags | Role for the steward |
|---|---|---|---|
| `sumpter_horse` (= `DefaultItemCategories.PackAnimal`) | sumpter_horse, mule, saddle_horse, old_horse, pack_camel (+ unmountable mule/pack_camel variants, not merchandise) | rideable **and** pack | **Pack animal** |
| `horse` | aserai/battania/empire/khuzait/sturgia/vlandia_horse, hunter, camel (+ noble_camel, special_camel, tournament variants — not merchandise) | rideable, not pack | Mount — **also an upgrade requirement** (§4) |
| `war_horse` | charger, war_horse, war_camel, t2_* culture horses, desert/steppe_war_horse | rideable, not pack | Mount / war mount |
| `noble_horse` | t3_* culture horses, noble_horse_*, storm_charger | rideable, not pack | Mount (never an upgrade requirement) |
| `cow`, `sheep`, `hog`, `animal` (chicken, goose; cat & dog not merchandise) | — | not rideable, not pack | **Livestock** — not managed |

There are **no** separate camel/mule categories: camels sit in `horse`/`war_horse`/`sumpter_horse`.
War Sails adds no animals (only `walrus_tusk`, `whale_oil` trade goods).

**Average price (DESIGN §4.2).** The game's own "average" is in the inventory:
`InventoryLogic.InitializeCategoryAverages()` = mean of `Town.MarketData.GetPriceFactor(category)`
over **all towns except the current one**, read with `float GetAveragePriceFactorItemCategory(ItemCategory)`
(`CS\TaleWorlds.CampaignSystem.Inventory\InventoryLogic.cs`); `SPItemVM` compares it to the local
factor for the green/red deal colouring. So **average price = `item.Value × avg factor`**, which we
can compute ourselves: `Town.AllTowns` → `town.MarketData.GetPriceFactor(item.ItemCategory)`.
Note: even an item at exactly the average costs ≥ 106% of it — every purchase carries a base
trade penalty of 0.06 (§8).

---

## 2. Food (DESIGN §2.1)

- **Model:** `DefaultMobilePartyFoodConsumptionModel` (`CS\...GameComponents\`).
  `CalculateDailyBaseFoodConsumptionf(MobileParty, bool)` = `-(NumberOfAllMembers +
  NumberOfPrisoners / 2) / 20` (min 1 eater). **Prisoners eat half** (integer division).
  `NumberOfMenOnMapToEatOneFood => 20`. Perks then scale it in `CalculateDailyFoodConsumptionf`
  (Roguery.Promises for bandits, Athletics.Spartan, Steward.WarriorsDiet, Steward.PriceOfLoyalty
  epic, Scouting.Foragers in forest/steppe, Steward.StiffUpperLip in an army, siege perks);
  `LimitMax(-0.01)`.
- **Party readouts** (`CS\TaleWorlds.CampaignSystem.Party\MobileParty.cs`):
  `float FoodChange` (daily, negative, perks included), `float BaseFoodChange` (no perks),
  `int TotalFoodAtInventory => ItemRoster.TotalFood`, `float Food` (adds the fractional
  `RemainingFoodPercentage`), `int GetNumDaysForFoodToLast()` = `(TotalFood*100 +
  RemainingFoodPercentage) / (100 * -FoodChange)`.
  For the footer's "days after", recompute: `perkRatio = FoodChange / BaseFoodChange`, then
  `daily = (membersAfter + prisonersAfter/2)/20 × perkRatio`.
- **`ItemRoster.TotalFood` includes livestock**: each livestock head adds `HorseComponent.MeatCount`
  (`CS\TaleWorlds.CampaignSystem.Roster\ItemRoster.cs`, `OnRosterUpdated` / `CalculateCachedStats`).
  When real food runs out the party slaughters livestock (`FoodConsumptionBehavior.SlaughterLivestock`).
- **Which food is eaten:** `FoodConsumptionBehavior.MakeFoodConsumption` picks a **random food TYPE**
  (uniform over the roster elements that are food), removes 1 unit, repeats. Small stacks vanish
  as fast as big ones — each type loses ≈ daily/types per day. The Balanced ("fill the fewest")
  strategy matches this well.
- **Variety morale:** `DefaultPartyMoraleModel.CalculateFoodVarietyMoraleBonus` on
  `ItemRoster.FoodVariety` (count of roster elements with `IsFood`, livestock NOT counted):
  0–1 → −2, 2 → −1, 3 → 0, 4 → +1, 5 → +2, 6 → +3, 7 → +5, 8 → +6, 9 → +7, 10 → +8, 11 → +9,
  12+ → +10. Negative values are zeroed by Steward.WarriorsDiet (on land); positive values boosted
  by Steward.Gourmet. With the 9 vanilla foods the best reachable is **+7**.

---

## 3. Party speed, pack animals and mounts (DESIGN §2.2, §2.3)

`DefaultPartySpeedCalculatingModel.CalculateLandBaseSpeed` (`CS\...GameComponents\`):

- **Footmen** = `PartyBase.NumberOfMenWithoutHorse` = `MemberRoster.TotalManCount −
  NumberOfMenWithHorse` (`CS\...Party\PartyBase.cs`). `NumberOfMenWithHorse` sums every roster
  element whose `CharacterObject.IsMounted` — for troops that is their battle equipment; for a
  **hero** it is `Equipment[10].Item != null` (horse slot of the hero's battle equipment).
  **Heroes and wounded are included.**
- **Available mounts** = `ItemRoster.NumberOfMounts` (items with `HorseComponent.IsMount`). War
  horses, noble horses and camels count; **pack animals never carry footmen**.
- **Mounted footmen** = `min(footmen, NumberOfMounts)` → `+0.15 × mountedFootmen / totalMen`
  (×0.3 less in wet weather; Riding.NomadicTraditions adds). Cavalry: `+0.3 × withHorse / totalMen`.
- **Herd** = `NumberOfPackAnimals + NumberOfLivestockAnimals + max(0, NumberOfMounts −
  mountedFootmen)`. Penalty only when herd > total men: `max(−0.8, −0.3 × (herd − men) / men)`
  (Riding.Shepherd softens). **Surplus mounts are herd**, so a 10% buffer costs nothing until
  animals outnumber men.
- **Capacity** (`DefaultInventoryCapacityModel`): 10 base + 20 per healthy member + **20 per mount**
  + **100 per pack animal** (× Scouting.BeastWhisperer, Riding.DeeperSacks, Steward.ArenicosMules;
  Trade.CaravanMaster on the total). Over capacity → up to −0.4 ("Overburdened").
- **Animals weigh 0** in carried weight (`GetItemEffectiveWeight` returns 0 for any
  `HasHorseComponent`) — selling a horse frees no weight.
- **Quirk:** the incremental counters skip animals with an `ItemModifier` (lame, spirited…) —
  `OnRosterUpdated` counts pack/mount only when `ItemModifier == null`; a full recount
  (`CalculateCachedStats`, on load / `Clear()`) counts them. In-session the speed readout can be
  off by the modified animals.

---

## 4. Troop upgrades and war mounts (DESIGN §2.4)

- **The requirement lives on the TARGET troop**: `ItemCategory CharacterObject.UpgradeRequiresItemFromCategory`
  (XML `upgrade_requires` on the troop you upgrade INTO), checked by
  `DefaultPartyTroopUpgradeModel.DoesPartyHaveRequiredItemsForUpgrade(PartyBase, CharacterObject upgradeTarget)`
  (`CS\...GameComponents\DefaultPartyTroopUpgradeModel.cs`).
- **Vanilla data:** `ItemCategory.horse` on 21 troops, `ItemCategory.war_horse` on 23; `noble_horse`
  never. Upgrade-source shapes: 15 stacks → one target needing `war_horse`; 3 → one target needing
  `horse`; **12 stacks have a choice** (one foot target, one needing `horse` (9) or `war_horse` (3) —
  e.g. recruits → infantry or cavalry); 1 (steppe bandit raider) → two targets both `war_horse`.
- **XP cost:** `int CharacterObject.GetUpgradeXpCost(PartyBase party, int index)` → model
  `GetXpCostForUpgrade` (tier ladder 100/300/550/900/1300/1700/2100). Gold:
  `GetUpgradeGoldCost(PartyBase, int index)`.
- **"Can upgrade NOW" — the party screen's own count** (`CSVM\...Party\PartyCharacterVM.cs`,
  `InitializeUpgrades`): per stack and target *i*:
  `ready = target.Level >= troop.Level && Xp >= cost ? min(floor(Xp / cost), Number) : 0`
  (`Xp` = `TroopRosterElement.Xp`, pooled for the whole stack, **shared by both targets**), further
  capped by gold (`floor(gold / goldCost)`), by animals held of the category, and by the perk rule
  (bandit → non-bandit needs Leadership.VeteransRespect). Wounded men can upgrade.
- **Consumption:** `PartyScreenLogic.UpgradeTroop` → `RemoveItemFromItemRoster(category, n)`
  takes **the cheapest animals of that category first, LOCKED ones last** (`orderby Value` then
  `orderby locked`) — locked horses are *not* safe from upgrades.
- The main party is never auto-upgraded (`PartyUpgraderCampaignBehavior` skips `PartyBase.MainParty`).

---

## 5. Prisoners — ransom and donate (DESIGN §2.5)

- **Ransom value:** `int RansomValueCalculationModel.PrisonerRansomValue(CharacterObject prisoner, Hero sellerHero = null)`
  (`DefaultRansomValueCalculationModel`): troops = `recruitmentCost × 0.25` (+Roguery.Manhunter);
  heroes = recruitment cost + clan-tier/leader/gold terms × kingdom-size factor (+Roguery.RansomBroker).
  Call it with `Hero.MainHero` as vanilla does.
- **Where vanilla ransoms:** town menu `town` → option `town_backstreet` ("Go to the tavern
  district", condition `SettlementAccessModel.CanMainHeroAccessLocation(settlement, "tavern", ...)`)
  → menu `town_backstreet` → `sell_all_prisoners` ("Ransom your prisoners (N)") or
  `sell_some_prisoners` (party screen). Towns only; no faction condition beyond tavern access
  (`CS\...CampaignBehaviors\PlayerTownVisitCampaignBehavior.cs`).
- **Vanilla honours prisoner LOCKS:** `MobilePartyHelper.GetPlayerPrisonersPlayerCanSell()`
  (`CS\Helpers\MobilePartyHelper.cs`) skips every troop whose `StringId` is in
  `IViewDataTracker.GetPartyPrisonerLocks()`.
- **The action:** `SellPrisonersAction.ApplyForSelectedPrisoners(PartyBase.MainParty, null, TroopRoster prisoners)`
  (`CS\...Actions\SellPrisonersAction.cs`). Build the roster with
  `TroopRoster.CreateDummyTroopRoster()` + `AddToCounts(character, count, false, woundedCount)`.
  It removes the men, pays `Σ PrisonerRansomValue(c, MainHero) × n` to the player
  (`GiveGoldAction.ApplyBetweenCharacters(null, MainHero, sum)`), gives Roguery XP
  (`SkillLevelingManager.OnPrisonerSell`) and fires `OnPrisonerSold`.
  **Hero prisoners** with `buyerParty == null` are **released** (`EndCaptivityAction.ApplyByRansom(hero, null)`)
  and paid at their (large) hero value.
- **Donate — menu:** `town_keep` → `town_lords_hall_go_to_dungeon` → menu `town_keep_dungeon` →
  `town_prison_leave_prisoners` ("Donate prisoners"). Condition
  `game_menu_castle_leave_prisoners_on_condition`: settlement `IsFortification`,
  **`MapFaction == Hero.MainHero.MapFaction` AND `OwnerClan != Clan.PlayerClan`** (own-clan fiefs
  get "Manage prisoners" instead — and no influence), disabled when the dungeon is full
  (`Party.PrisonerSizeLimit <= Party.NumberOfPrisoners`). Dungeon access:
  `SettlementAccessModel.CanMainHeroEnterDungeon(settlement, out AccessDetails)` (limited access
  needs the bribe paid). **A mercenary qualifies**: his clan's `MapFaction` is the kingdom he serves.
- **Donate — what vanilla does** (`CS\Helpers\PartyScreenHelper.cs`, `OpenScreenAsDonatePrisoners`
  + `DonatePrisonersDoneHandler`): ensures a garrison (`settlement.AddGarrisonParty()` if
  `Town.GarrisonParty == null`), moves prisoners into `settlement.Party.PrisonRoster`, for heroes
  `EnterSettlementAction.ApplyForPrisoner(hero, settlement)`, then
  `CampaignEventDispatcher.Instance.OnPrisonerDonatedToSettlement(MobileParty.MainParty, FlattenedTroopRoster, settlement)`.
  Headless: move with `MainParty.PrisonRoster.AddToCounts(c, -n, false, -w)` and
  `settlement.Party.PrisonRoster.AddToCounts(c, n, false, w)` (heroes:
  `TransferPrisonerAction.Apply(heroChar, PartyBase.MainParty, settlement.Party)` +
  `EnterSettlementAction.ApplyForPrisoner`), then fire the event.
- **Influence:** `InfluenceGainCampaignBehavior` (listener) → per prisoner
  `PrisonerDonationModel.CalculateInfluenceGainAfterPrisonerDonation` = `0.2 × ransomValue^0.4`
  (a 50-denar recruit ≈ 1 influence) → `GainKingdomInfluenceAction.ApplyForDonatePrisoners`
  (×1.2 with the Military Coronae policy). Zero when settlement owner clan AND donor are the player's clan.
- **Room:** use `settlement.Party.PrisonerSizeLimit − settlement.Party.NumberOfPrisoners`. (Vanilla's
  donate screen passes `PrisonerSizeLimit − PrisonRoster.Count` — the *stack* count, a vanilla slip.)

---

## 6. Loot (DESIGN §2.6) — groups, locks, sell penalties

**Item-type groups** (`ItemObject.ItemTypeEnum`, TaleWorlds.Core `ItemObject.cs`):

| Group | ItemTypeEnum values |
|---|---|
| Armour | `HeadArmor`, `BodyArmor`, `LegArmor`, `HandArmor`, `ChestArmor`, `Cape`, `HorseHarness` |
| Melee weapons | `OneHandedWeapon`, `TwoHandedWeapon`, `Polearm` |
| Ranged | `Bow`, `Crossbow`, `Sling`, `Thrown`, `Arrows`, `Bolts`, `SlingStones`, `Pistol`, `Musket`, `Bullets` (last three unused in vanilla) |
| Shields | `Shield` |
| Trade goods | `Goods` with `!IsFood` |
| Never loot | `Horse`, `Animal` (managed / livestock), `Banner` (52 banner items carry banner effects), `Book`, `Invalid`, food |

Also never sell: `EquipmentElement.IsQuestItem`, `!ItemObject.IsTransferable`
(vanilla: only `stealth_throwing_stone`).

**Inventory LOCKS — exact** (`CS\IViewDataTracker.cs`, `CS\...CampaignBehaviors\ViewDataTrackerCampaignBehavior.cs`):
- Read: `Campaign.Current.GetCampaignBehavior<IViewDataTracker>().GetInventoryLocks()` →
  `IEnumerable<string>`. Stored in `_inventoryItemLocks`, **saved in the save game** (SyncData).
- Id format: `item.StringId + (modifier != null ? modifier.StringId : "")` — no separator; public
  helper `CampaignUIHelper.GetItemLockStringID(EquipmentElement)` (`CSVM\...\CampaignUIHelper.cs`).
  A lock is per item **+ modifier** (a "rusty" sword and a plain one lock separately).
- Only the player's side can be locked (`SPInventoryVM.ProcessLockItem`); the list is written back
  when the inventory screen closes (`SaveItemLockStates`). Vanilla's "transfer all" skips locked
  items (`!IsLocked`).

**Sell penalties** (`DefaultTradeItemPriceFactorModel.GetTradePenalty`): base 0.06; +0.5 at war with
the settlement; **selling equipment** (not trade good, not animal): +1.5 + 0.25×(tier−1)
(Crafting.ArtisanSmith, Trade.Appraiser, Roguery.ArmsDealer adjust); selling a mount or a pack
animal: +0.8; **at a village: +1.0 when selling, +0.1 when buying**. Sell price divides by
(1 + penalty) — equipment sells for 39% (tier 1) down to ~30% (tier 4) of its value in a town and
28% → ~23% in a village.

---

## 7. Tavern — wanderers and mercenaries (DESIGN §2.7)

**Wanderers for hire in this town** — no vanilla list API; the tavern placement rule
(`DefaultHeroAgentLocationModel.GetLocationForHero`, `CS\...GameComponents\`) is:
`settlement.HeroesWithoutParty` where `hero.IsWanderer` (Occupation.Wanderer), not a governor → the
"tavern" location. For hire, also require (vanilla dialogue `conversation_hero_hire_on_condition`,
`CS\...CampaignBehaviors\LordConversationsCampaignBehavior.cs`): `!hero.IsPlayerCompanion`,
`hero.PartyBelongedTo == null`, not prisoner; ImmersiveAI also checks `Clan == null`,
`CompanionOf == null`, alive.
- **Price:** `int CompanionHiringPriceCalculationModel.GetCompanionHiringPrice(Hero)` = half the
  town price of the hero's battle + civilian gear + 10 × level (Steward.PaidInPromise,
  Trade.GreatInvestor). Changes with the town's prices — read it at plan time AND at execute time.
- **Vanilla conditions:** `Hero.MainHero.Gold > price` (**strictly greater**) and
  `Clan.PlayerClan.Companions.Count < Clan.PlayerClan.CompanionLimit` (`int Clan.CompanionLimit` →
  `ClanTierModel.GetCompanionLimit`). **No party-size check** in vanilla.
- **Hire, no dialogue** (vanilla `conversation_companion_hire_on_consequence`):
  `GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, hero, price)`;
  `AddCompanionAction.Apply(Clan.PlayerClan, hero)`; `AddHeroToPartyAction.Apply(hero, MobileParty.MainParty)`.
  Plus `hero.SetHasMet()` (a dialogue normally sets it).
- **Daily wage:** `CharacterObject.TroopWage` → heroes `2 + 2 × Level`
  (Steward.PaidInPromise changes it inside `DefaultPartyWageModel.GetTotalWage`).
- **ImmersiveAI already does this** (`..\ImmersiveAI\src\ImmersiveAI.Module\ImmersiveChatBehavior.cs`):
  `BargainBlockReason(Hero, int price, bool byLetter)` (≈ line 399 — the validation: alive, not
  prisoner, `IsWanderer`, unbound, co-located via `IsCoLocated`, gold, companion limit) and
  `OnBargainSealed(Hero, int, bool)` (≈ line 573 — re-validates, then the same three vanilla calls,
  `MarkMetInWorldsEyes` = `SetHasMet()`). It hires with **no vanilla dialogue** (the talk is its own
  LLM chat; the hire itself is the three calls). Reuse the rule set and the re-check-at-execute
  habit; note it allows `Gold >= price` where vanilla needs `>`.

**Tavern mercenaries** (`CS\...CampaignBehaviors\RecruitmentCampaignBehavior.cs`):
- Data: `Campaign.Current.GetCampaignBehavior<RecruitmentCampaignBehavior>().GetMercenaryData(Town)` →
  `TownMercenaryData { CharacterObject TroopType; int Number; bool HasAvailableMercenary(); void ChangeMercenaryCount(int) }`.
- **Price per man:** `PartyWageModel.GetTroopRecruitmentCost(troopType, Hero.MainHero).RoundedResultNumber`.
- **Wage per man:** `troopType.TroopWage` → `GetCharacterWage`: tier 0..7+ = 1,2,3,5,8,12,17,23, ×1.5
  for `Occupation.Mercenary`.
- **Vanilla's own no-dialogue option exists:** `town_backstreet` / `recruit_mercenaries`
  ("Recruit N X (total)", index 2) buys `min(Number, Gold / price)` — but that path skips the
  recruit event. The dialogue path `BuyMercenaries()` is the complete one:
  `data.ChangeMercenaryCount(-n)`; `MobileParty.MainParty.AddElementToMemberRoster(troopType, n)`;
  `GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, n × price)`;
  `CampaignEventDispatcher.Instance.OnUnitRecruited(troopType, n)` (→ Leadership XP,
  FamousCommander XP, statistics). **Mirror `BuyMercenaries`.** Vanilla never checks party size.
- **Party size:** `PartyBase.PartySizeLimit` vs `MemberRoster.TotalManCount` (heroes count).

**Encyclopedia links (Anton 2026.09.27)** (`CS\TaleWorlds.CampaignSystem.Encyclopedia\EncyclopediaManager.cs`):
- `Campaign.Current.EncyclopediaManager.GoToLink(string link)` with `hero.EncyclopediaLink`
  (`"Hero-<id>"`) or `troop.EncyclopediaLink` (`CharacterObject`, unit page). Valid pages:
  heroes unless `HiddenInEncyclopedia`; units with Occupation Soldier/Mercenary/Bandit/Gangster/
  CaravanGuard — tavern mercenaries qualify.
- The link runs through a callback set by `GauntletMapEncyclopediaView` (SandBox.GauntletUI) — only
  while the **MapScreen** exists (it does, in a settlement menu). It opens `EncyclopediaData` as a
  `GauntletLayer("EncyclopediaBar", 310)` on `ScreenManager.TopScreen` and calls `TrySetFocus`.
- **Focus goes by layer order:** `ScreenManager.TrySetFocus` only moves focus if the new layer's
  order ≥ the focused one's. TrainingBattles' windows use order **4500** — at 4500 the encyclopedia
  would draw UNDER our window and never get focus. Either open our window **below 310** (menus are
  100, overlays/map bar 202–206, army management 300 — e.g. 305), or hide our layer while it is open.
- Knowing it is open / closed: `MapScreen.Instance.EncyclopediaScreenManager.IsEncyclopediaOpen`
  (SandBox.View); close fires `EncyclopediaPageChangedEvent(EncyclopediaPages.None)` on
  `Game.Current.EventManager` (register with `RegisterEvent<EncyclopediaPageChangedEvent>`). On it,
  `ScreenManager.TrySetFocus(ourLayer)`; ignore our Escape handler while the encyclopedia is open.

---

## 8. Markets and prices (DESIGN §3, §4.1, §4.2)

**Stock and gold:** `Settlement.ItemRoster` (= `Party.ItemRoster`; one element per item +
modifier), `SettlementComponent.Gold` (`Town.Gold`, `Village.Gold`),
`SettlementComponent.ChangeGold(int)` (clamps at 0).

**Price the inventory screen uses — use exactly this:**
`IMarketData.GetPrice(EquipmentElement, MobileParty tradingParty, bool isSelling, PartyBase merchantParty)`
called by `InventoryLogic.GetItemPrice(EquipmentElement, bool isBuying)` as
`MarketData.GetPrice(element, MobileParty.MainParty, !isBuying, settlement.Party)`.
MarketData = `settlement.Town.MarketData` (`TownMarketData`) or `settlement.Village.MarketData`
(`VillageMarketData`). **Do NOT use `Town.GetItemPrice(item, party, isSelling)`**: it passes no
merchant, so the village penalty, the war penalty and the Scouting network perks vanish — it is
the AI's price (`SellItemsAction`), not the player's.

**The formula** (`DefaultTradeItemPriceFactorModel`, `CS\...GameComponents\`):
```
bpf = Pow(demand / (0.1·supply + (inStoreValue + (selling ? item.Value : 0))·0.04 + 2),
          category.IsAnimal ? 0.3 : 0.6)
      clamped to [0.1, 10] if category.IsTradeGood else [0.8, 1.3]
buy  = max(1, Ceiling(element.ItemValue · bpf · (1 + penalty)))
sell = max(1, Floor  (element.ItemValue · bpf / (1 + penalty)))
```
`(supply, demand, inStoreValue)` = `town.MarketData.GetCategoryData(item.ItemCategory)` →
`ItemData { float Supply; float Demand; int InStore; int InStoreValue }`. `penalty` from
`GetTradePenalty(item, clientParty, merchant, isSelling, …)` (§6 numbers + perks; the Aserai
culture feat −10%).

**Unit-by-unit walk — towns: YES, per CATEGORY.** The inventory screen edits the LIVE rosters;
every `AddToCounts` fires `RosterUpdatedEvent` → `Town.OnInventoryUpdated` →
`TownMarketData.OnTownInventoryUpdated` → `AddNumberInStore(category, ±1, item.Value)` — so after
each unit `InStoreValue` moves by `item.Value` and the next unit is priced anew. It is per
**category**: buying one charger raises every `war_horse` price. **Villages: flat** —
`VillageMarketData.GetPrice` reads the trade-bound town's category data
(`(village.TradeBound ?? nearest town).Town.MarketData`), which a village trade never touches.
Supply and demand are not moved by the player's trade (only by the campaign's own economy ticks).

**How to simulate it** (exact, and mod-friendly): call the model itself with a hypothetical store
value — `Campaign.Current.Models.TradeItemPriceFactorModel.GetPrice(EquipmentElement, MobileParty.MainParty,
settlement.Party, isSelling, inStoreValue, supply, demand)` — stepping `inStoreValue` by
`∓item.Value` per unit (towns only). Core stays pure through a small price-oracle interface
(`Price(itemId, isSelling, categoryStoreValueDelta)`); the Module implements it with the model,
tests with the formula above.

**Inside one InventoryLogic session** a buy-back is priced at the last opposite transfer
(`TransactionHistory.GetLastTransfer`) — irrelevant if the plan never buys and sells the same item.

**Villages in v1.4.8 (DESIGN §2.6/§3):** menu `village` option `trade` ("Buy products") has a null
consequence; the attribute handler `[GameMenuEventHandler("village","trade",OnConsequence)]` in
`PlayerTownVisitCampaignBehavior` opens `InventoryScreenHelper.OpenScreenAsTrade(village.ItemRoster, village)` —
the **full two-way trade screen: you can sell at a village**. Stock = the village's produce
(`VillageGoodProductionCampaignBehavior`, from `DefaultVillageTypes`: horse ranches produce culture
horses, t2/t3 horses, sumpter horses, mules, saddle horses, old horses, hunters, chargers; desert
ranches camels/war camels/pack camels; farms cows/sheep/hogs/butter/cheese; fishers fish, etc.)
plus whatever players sold there. Access: `SettlementAccessModel.CanMainHeroDoSettlementAction(settlement,
SettlementAction.Trade, out bool, out TextObject)` — the same gate the town's `trade` option uses.

**Merchant gold:** `InventoryLogic.DoneLogic` pays the player `min(sale total, merchant gold)` —
vanilla lets you oversell and simply pays less. The planner must never propose a sale beyond the
market's gold (DESIGN §3.4 already says so).

---

## 9. Executing trades like vanilla (DESIGN §5)

**Recommended: a headless `InventoryLogic`** — the same object the trade screen uses, without the
screen (`CS\TaleWorlds.CampaignSystem.Inventory\InventoryLogic.cs`, `CS\Helpers\InventoryScreenHelper.cs`):
```
var logic = new InventoryLogic(settlement.Party);           // owner = MainParty / PlayerCharacter
logic.Initialize(settlement.ItemRoster, PartyBase.MainParty.ItemRoster, PartyBase.MainParty.MemberRoster,
    isTrading: true, isSpecialActionsPermitted: true, CharacterObject.PlayerCharacter,
    InventoryScreenHelper.InventoryCategoryType.None, marketData /* Town/Village MarketData */,
    useBasePrices: false, InventoryScreenHelper.InventoryMode.Trade);
logic.SetInventoryListener(new OurMerchantListener(settlement.SettlementComponent)); // GetGold/SetGold → SettlementComponent.Gold/ChangeGold
logic.TotalAmountChange = _ => { };                          // MUST be set: invoked without a null check
logic.AddTransferCommand(TransferCommand.Transfer(n, InventorySide.PlayerInventory, InventorySide.OtherInventory,
    new ItemRosterElement(element, n), EquipmentIndex.None, EquipmentIndex.None, CharacterObject.PlayerCharacter)); // sell
logic.AddTransferCommand(TransferCommand.Transfer(n, InventorySide.OtherInventory, InventorySide.PlayerInventory, ...)); // buy
bool ok = logic.DoneLogic();                                 // false if the player cannot pay
```
(`InventoryListener` is a public abstract class — `GetGold`, `SetGold`, `GetTraderName`,
`GetOppositeParty`, `OnTransaction`; vanilla's `MerchantInventoryListener` is private, so write the
5-line twin.) `DoneLogic` then does exactly what the screen does: the unit-by-unit price walk on
the live rosters, `GiveGoldAction` for the player (capped by merchant gold), the merchant's gold,
the Trade.TrickleDown perk, and **`CampaignEventDispatcher.OnPlayerInventoryExchange(bought, sold, true)`**.

**What we would lose by bypassing it:** `OnPlayerInventoryExchange` drives
`TradeSkillCampaignBehavior` — **Trade XP comes only from profit** on items bought earlier (it keeps
an average purchase price per item; buying records it, selling above it pays XP via
`SkillLevelingManager.OnTradeProfitMade`) — plus quests that watch purchases (HeadmanNeedsGrain,
LordNeedsHorses, VillageNeedsTools…) and the tutorial. Alternatives: `SellItemsAction.Apply` is the
AI path (wrong prices, town trade tax, no event); raw roster edits + `GiveGoldAction` would need
the event fired by hand (`CampaignEventDispatcher.Instance.OnPlayerInventoryExchange` is public).
The headless logic keeps all of it for free.

Other executors: ransom §5, donate §5, wanderer and mercenary hire §7.

---

## 10. Arrival, menus and leaving (DESIGN §6)

- **Events** (`CS\TaleWorlds.CampaignSystem\CampaignEvents.cs`, all `IMbEvent`, subscribe with
  `.AddNonSerializedListener(this, handler)`): `SettlementEntered (MobileParty, Settlement, Hero)`,
  `AfterSettlementEntered`, `BeforeSettlementEnteredEvent`, `OnSettlementLeftEvent (MobileParty, Settlement)`,
  `GameMenuOpened (MenuCallbackArgs)` (fired by `GameMenu.RunOnInit` **every time a menu is
  (re)entered** — returning from the trade screen or the tavern district re-fires it for `town`),
  `AfterGameMenuInitializedEvent`, `GameMenuOptionSelectedEvent (GameMenu, GameMenuOption)`.
- **Menu ids:** town main menu `town` (leave option `town_leave`, isLeave); village `village`
  (leave options `leave`, and at sea `leave_set_sail`, `leave_at_sea`); looted village
  `village_looted`; tavern district `town_backstreet`; keep `town_keep`; dungeon `town_keep_dungeon`;
  castles `castle` (non-goal). War Sails adds `town`/`port` → menu `port_menu` with `sail_option`
  ("Set sail", isLeave) — reachable by id without referencing the DLC.
- **Add an option:** `CampaignGameStarter.AddGameMenuOption(string menuId, string optionId, string optionText,
  GameMenuOption.OnConditionDelegate condition, GameMenuOption.OnConsequenceDelegate consequence,
  bool isLeave = false, int index = -1, bool isRepeatable = false, object relatedObject = null)`.
  `index` = insertion position in the menu's list *at registration time*
  (`GameMenu.AddOption` → `List.Insert`). Vanilla registers `town`/`village` in
  `PlayerTownVisitCampaignBehavior`'s `OnSessionLaunched`; register ours in our own
  `OnSessionLaunched` and compute the index from the live list
  (`Campaign.Current.GameMenuManager.GetGameMenu("town").MenuOptions`, find `trade`, +1).
  Icon: set `args.optionLeaveType = GameMenuOption.LeaveType.Manage` (or `Trade`) in the condition.
- **Intercepting LEAVE — no Harmony needed:** `GameMenuOption.OnConsequence` is a **public field**
  (`public OnConsequenceDelegate OnConsequence;`, `CS\TaleWorlds.CampaignSystem.GameMenus\GameMenuOption.cs`).
  After vanilla registered, fetch the option (`GetGameMenu(id).MenuOptions.First(o => o.IdString == …)`)
  and wrap: keep the original delegate, replace with ours (show the inquiry, call the original on
  "Leave anyway"). `RunConsequence` calls `OnConsequence(args)` then `menuContext.OnConsequence(option)`
  (the attribute-handler dispatch); no attribute handler exists on the leave options, so the wrap
  is complete. Wrap once per session (after load the game menus are rebuilt).
- **Leaving by clicking the map: impossible while a settlement menu is open.**
  `MapScreen.HandleLeftMouseButtonClick` moves the party only `if (!MapState.AtMenu)`, and WASD
  movement is gated by `!IsInMenu` (SandBox.View `MapScreen.cs`, `MapCameraView.cs`). Other exits
  exist (Return to Army, a siege starting, prison break, retirement) — not worth warning for.
- **When to auto-open on arrival:** `SettlementEntered` (party == `MobileParty.MainParty`) → mark
  "arrived, not yet shown"; `GameMenuOpened` with `args.MenuContext.GameMenu.StringId` of `town` or
  `village` → "open on next tick"; open from `OnApplicationTick` when `ScreenManager.TopScreen` is
  the MapScreen, `Campaign.Current.CurrentMenuContext` is still that menu and no encyclopedia /
  conversation / inquiry is up. Clear the flags on `OnSettlementLeftEvent`. (Pattern reasoned from
  the code, **not yet proven in game** — verify in PLAN step 8.)
- **Leave warning popup:** `InformationManager.ShowInquiry(new InquiryData(title, text, true, true,
  "Review", "Leave anyway", onReview, onLeave), pauseGameActiveState: true)` (TaleWorlds.Library).

---

## 11. The Gauntlet window (DESIGN §1)

**Working pattern (TrainingBattles `src\TrainingBattles.Module\UI\TrainingWindow.cs`, verified
against 1.4.8 `TaleWorlds.Engine.GauntletUI\...\GauntletLayer.cs`):**
`var layer = new GauntletLayer(string name, int localOrder, bool shouldClear = false);`
`GauntletMovieIdentifier movie = layer.LoadMovie(string movieName, ViewModel vm);`
`layer.InputRestrictions.SetInputRestrictions();` `layer.IsFocusLayer = true;`
`ScreenManager.TopScreen.AddLayer(layer); ScreenManager.TrySetFocus(layer);`
Close: `IsFocusLayer = false; InputRestrictions.ResetInputRestrictions(); host.RemoveLayer(layer);`
(add `layer.ReleaseMovie(movie)` — TrainingBattles skips it). Escape polled from the application
tick via `layer.Input.IsKeyReleased(InputKey.Escape)`. Prefab XML in `module\GUI\Prefabs\`,
movie name = file name; no Harmony, no view classes. Only native brushes/sprites.

**Sprites:** `ui_group1` (StdAssets\*, General\Icons\Coin@2x, BlankWhiteSquare_9, popups) is
`AlwaysLoad` in `Modules\Native\GUI\NativeSpriteData.xml`; SandBox categories (`ui_inventory`,
`ui_partyscreen`, `ui_clan`…) are not — load with `UIResourceManager.LoadSpriteCategory(name)` if a
sprite from them is used.

**Shift / Ctrl clicks — the vanilla way:** the screen polls every frame
`vm.IsFiveStackModifierActive = layer.Input.IsHotKeyDown("FiveStackModifier");`
`vm.IsEntireStackModifierActive = layer.Input.IsHotKeyDown("EntireStackModifier");`
(`SandBox.GauntletUI\...\GauntletInventoryScreen.cs` `OnFrameTick`, `GauntletPartyScreen.cs`), and
the VM's click reads the flags. The hot keys belong to `GenericCampaignPanelsGameKeyCategory`
(TaleWorlds.MountAndBlade) — **FiveStackModifier = Left/Right Shift, EntireStackModifier =
Left/Right Ctrl**, exactly Anton's ±5 / ±all — and must be registered on our layer:
`layer.Input.RegisterHotKeyCategory(HotKeyManager.GetCategory("GenericCampaignPanelsGameKeyCategory"))`
(plus `"GenericPanelGameKeyCategory"` for Exit/Confirm). Respects player rebinding.
(Raw alternative: `TaleWorlds.InputSystem.Input.IsKeyDown(InputKey.LeftShift)`.)

**Tabs to copy:** `Modules\SandBox\GUI\Prefabs\Clan\ClanScreen.xml` (lines ~205–240): a row of
`ButtonWidget` with `Brush="Header.Tab.Left"` / `"Header.Tab.Center"` / `"Header.Tab.Right"`,
`Command.Click="SetSelectedCategory" CommandParameter.Click="0"`, `IsSelected="@IsMembersSelected"`,
`UpdateChildrenStates="true"`, a `TextWidget Brush="Clan.TabControl.Text"`; each page is a panel with
`IsVisible="@Is…Selected"`. The `Header.Tab.*` brushes (`Modules\Native\GUI\Brushes\Standard.xml`)
use `StdAssets\page_button_*` sprites — `ui_group1`, always loaded. No TabControl widget needed.

**Item icon in a row (optional):** `new ItemImageIdentifierVM(ItemObject item, string bannerCode = "")`
(`TaleWorlds.Core.ViewModelCollection\...ImageIdentifiers\ItemImageIdentifierVM.cs`; the 1.4.x
split — `CharacterImageIdentifierVM` etc. are siblings) bound to
`<ImageIdentifierWidget DataSource="{ImageIdentifier}" ImageId="@Id" AdditionalArgs="@AdditionalArgs" TextureProviderName="@TextureProviderName" />`
(as in `Modules\SandBox\GUI\Prefabs\Inventory\InventoryItemTuple.xml`). Cost: one rendered thumbnail
texture per row, loaded asynchronously; call `OnFinalize()` on the VMs when the window closes.
Fine for ~30 rows.

---

## 12. Settings, MCM and the soft dependency (DESIGN §7, §8)

- **Newtonsoft.Json** ships in the game: `bin\Win64_Shipping_Client\Newtonsoft.Json.dll`,
  assembly **13.0.0.0**, file 13.0.1. Reference it with `Private=false`; do not ship a copy.
  **Verified with the real DLL:** `JObject.Parse` and `JsonConvert.DeserializeObject` accept `//`
  line comments, `/* */` blocks and trailing comments by default. **But `JsonTextWriter.WriteComment`
  writes `/* … */`** — to rewrite the file with a `//` line above every key, build the text by hand
  (values serialized with `JsonConvert.ToString` / `SerializeObject`).
- **MCM v5 fluent builder** (`MCM.Abstractions.FluentBuilder`, `MCM.Common`):
  `ISettingsBuilder? BaseSettingsBuilder.Create(string id, string displayName)` (null until MCM's
  service provider exists — built in `MCMSubModule.OnBeforeInitialModuleScreenSetAsRoot`; call ours
  from our own `OnBeforeInitialModuleScreenSetAsRoot`, MCM loads before us).
  `ISettingsBuilder`: `SetFolderName(string)`, `SetSubFolder(string)`, `SetFormat(string)` (default
  **"memory"** — MCM writes no file of its own; our settings.json stays the only store),
  `SetUIVersion(int)`, `SetSubGroupDelimiter(char)`, `SetOnPropertyChanged(PropertyChangedEventHandler)`,
  `CreateGroup(string name, Action<ISettingsPropertyGroupBuilder>)`, `CreatePreset(...)`,
  `WithoutDefaultPreset()`, `FluentGlobalSettings BuildAsGlobal()` → `.Register()` / `.Unregister()`.
  `ISettingsPropertyGroupBuilder`: `SetGroupOrder(int)`,
  `AddBool(string id, string name, IRef @ref, Action<ISettingsPropertyBoolBuilder>?)`,
  `AddInteger(string id, string name, int min, int max, IRef @ref, Action<ISettingsPropertyIntegerBuilder>?)`,
  `AddFloatingInteger(string id, string name, float min, float max, IRef @ref, Action<…>?)`,
  `AddDropdown(string id, string name, int selectedIndex, IRef @ref, Action<…>?)`, `AddText`, `AddButton`,
  `AddToggle`. Property builders: `SetOrder(int)`, `SetRequireRestart(bool)`, `SetHintText(string)`,
  numbers `AddValueFormat(string)`.
  **Value binding:** `new ProxyRef<T>(Func<T> getter, Action<T>? setter)` (`MCM.Common.ProxyRef`) —
  the getter/setter talk straight to our registry, so MCM, the file and the Instructions tab share
  one set of values. Dropdowns need a `MCM.Common.Dropdown<string>` object as the ref value
  (MCM changes its `SelectedIndex`) — relevant for `FoodStrategy` and `SellLootOrder`.
- **MCM identity:** module id `Bannerlord.MBOptionScreen` (v5.12.3 installed, depends on Harmony,
  ButterLib, UIExtenderEx), runtime assembly **`MCMv5`**. Declare in SubModule.xml
  `<DependedModuleMetadata id="Bannerlord.MBOptionScreen" order="LoadBeforeThis" optional="true" />`
  (TrainingBattles does). Detect with
  `AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "MCMv5")`.
- **Why the soft dependency can still break** (`TaleWorlds.MountAndBlade\...\Module.cs`,
  `AddSubModule` → `CollectModuleAssemblyTypes`): the loader calls `moduleAssembly.GetTypes()`; any
  `ReflectionTypeLoadException` returns `AssemblyLoadResult.CriticalError` and **the whole module
  fails to load** (unchanged in 1.4.8). TrainingBattles' AI_NOTES.md: loading a type resolves its
  base type, interfaces and field types; method bodies are JIT-compiled lazily. So: no class derives
  from an MCM type (the fluent builder guarantees that), **no field of an MCM type** (hold the built
  `FluentGlobalSettings` as `object`), and beware **compiler-generated classes** — a lambda that
  *captures* an MCM-typed local becomes a display class with an MCM-typed field; cached non-capturing
  lambdas become static delegate fields typed `Action<ISettingsPropertyGroupBuilder>`. Their "STILL
  OPEN" issue is exactly this for a subclassed settings class; their `tools\AssemblyGuard` scans a
  built DLL's metadata for forbidden types in base types / interfaces / fields and is the right gate.

**Manifest, loading and the save — verified in step 3 (2026.09.27):**
- **The vanilla game and launcher never read `DependedModuleMetadatas`** (a BUTR/BLSE extension).
  `TaleWorlds.ModuleManager\...\ModuleInfo.cs` `LoadWithFullPath` reads only
  `DependedModules/DependedModule` (`Id`, `DependentVersion`, **`Optional`**),
  `ModulesToLoadAfterThis` and `IncompatibleModules`. The launcher
  (`TaleWorlds.MountAndBlade.Launcher.Library.dll`, decompiled for this check, `LauncherModsVM`)
  builds the `_MODULES_` order by `MBMath.TopologySort` over `ModuleHelper.GetDependentModulesOf`
  — the `DependedModules` that are installed, optional ones included. So an `Optional="true"`
  dependency **sorts before us when present**, is **not required** (`AreAllDependenciesOfModulePresent`
  skips it) and is **not auto-enabled** (`ChangeIsSelectedOf`). Our SubModule.xml therefore lists
  MCM (and StoryMode) as optional in BOTH blocks; the metadata-only form TrainingBattles uses leaves
  MCM's load order to the player under the vanilla launcher.
- The launcher's start-up warning lists a "dependent version mismatch" for every `DependedModule`
  without `DependentVersion` (`ApplicationVersion.Empty` ≠ installed version) — cosmetic, inside the
  unofficial-DLL warning every DLL mod gets anyway; the sibling mods live with it too.
- **Module DLLs are locked for the game's whole life:** `Module.cs` loads each SubModule's `DLLName`
  from `<module>\bin\Win64_Shipping_Client\` through `AssemblyLoader.LoadFrom` → `Assembly.LoadFrom`
  (from start-up, main menu included). Referenced DLLs (SmartSteward.Core.dll) resolve from the same
  folder. `tools\deploy.ps1` refuses to run while it cannot open the installed DLL exclusively.
- **Empty SyncData is save-safe:** `CampaignBehaviorDataStore.SaveBehaviorData` files every behavior
  under its `StringId` (= class name) as a vanilla `BehaviorSaveData` (`Dictionary<string, object>`);
  empty SyncData → an empty record, no type of ours in the save, and a save loads fine without the
  mod. On load, a missing id falls back to the first saved key that *contains* the class name — keep
  the behavior's name unique (`SmartStewardBehavior`).
- Checked outside the game: `Assembly.LoadFrom` + `GetTypes()` on the built SmartSteward.dll under
  .NET Framework, with the game's bin resolvable and MCM not, succeeds (the loader's own call);
  the DLL references no MCMv5.

---

## Gotchas (one line each)

1. **Old decompile ≠ 1.4.8** in 4 files — cite `game-decompiled-1.4.8`.
2. **Town.GetItemPrice is the AI price** (no merchant → no village/war penalty). Use
   `MarketData.GetPrice(el, MainParty, isSelling, settlement.Party)` or the model.
3. **Prices walk per CATEGORY in towns, flat in villages.**
4. **The sell formula already counts the unit being sold** into `inStoreValue` (`+transferValue`).
5. **Upgrade requirement is on the target troop**; vanilla uses only `horse` and `war_horse`.
6. **Upgrades eat the cheapest animal of the category first; locked ones LAST, not never.**
7. **Pack animals are rideable but never carry footmen**; war/noble horses and camels do.
8. **Animals weigh 0**; each pack animal adds ~100 capacity, each mount 20.
9. **Modified animals** (lame, spirited…) are skipped by the in-session mount/pack counters.
10. **Livestock is food** to the game (`TotalFood` += MeatCount) and herd to the speed model, but
    not food variety.
11. **Prisoners eat half**; food is eaten one random TYPE at a time.
12. **Headless InventoryLogic: set `TotalAmountChange`** or the first transfer throws.
13. **DoneLogic pays at most the merchant's gold** — overselling silently loses money.
14. **Trade XP only from profit** on items bought before (via `OnPlayerInventoryExchange`).
15. **Hero prisoners ransomed at the broker are freed**, paid at hero value.
16. **Donating in your own clan's fief is not offered and gives no influence**; mercenaries can donate.
17. **Vanilla donate-room uses the prisoner STACK count** — use `NumberOfPrisoners`.
18. **Vanilla hires companions and mercenaries past the party size limit** (our design blocks it).
19. **Companion hire needs `Gold > price`** (strict) and a free companion slot.
20. **Tavern menu recruit skips `OnUnitRecruited`** — mirror the dialogue path `BuyMercenaries`.
21. **`GameMenuOpened` re-fires on every return to the menu** — keep a per-visit flag.
22. **Map clicks cannot leave a settlement menu** — only leave options need wrapping.
23. **Menu option index = insertion index at registration time.**
24. **Layer 4500 blocks the encyclopedia (310)** — focus follows layer order.
25. **Inventory locks are item + modifier ids**, saved in the save game, written when the
    inventory screen closes.
26. **Loader: one bad type = whole module unloaded** (MCM fields, closures, base types).
27. **Newtonsoft writes `/* */` comments**; hand-write the `//` file.
28. **Settlement gates:** trade needs `CanMainHeroDoSettlementAction(…, Trade, …)`, the tavern
    district `CanMainHeroAccessLocation(…, "tavern", …)`, donation the dungeon access.
29. **The vanilla launcher ignores `DependedModuleMetadatas`** — optional load order needs
    `<DependedModule Id="…" Optional="true" />` too.
30. **Module DLLs stay locked while the game runs** (main menu too) — quit before deploying.

---

## Design impact

**Facts that corrected DESIGN.md** (edited there, marked `[research 2026.09.27]`): the terms
(food list, pack = `IsPackAnimal`, mount = `IsMount`, war-mount categories `horse`/`war_horse`);
§2.1 prisoners count as half an eater; §2.3 footmen = `NumberOfMenWithoutHorse` (heroes and
wounded included, pack animals don't carry them, surplus mounts are herd); §2.4 the party-screen
ready count and cheapest-first consumption; §2.5 where ransom/donate happen and the donate rule
(own faction, not own clan, mercenary allowed, dungeon room); §4.1 walk per category in towns,
flat in villages; §4.2 the average price; §5 the executors; §6 the leave-wrap and no map-click
exit; §8 Newtonsoft 13 and the `//` writing gotcha. Anton's two mid-step changes (tavern names →
encyclopedia; loot in groups with `SellLootOrder`) went in marked `[Anton 2026.09.27]`.

**Choices for Anton** (added to TASKS_TODO → NOT FULLY DECIDED; defaults proposed):
1. **Livestock and food** — the game counts cows/sheep/hogs as food (meat) and slaughters them when
   food runs out. Proposed: the steward counts only real food toward the target (livestock is
   neither bought, sold nor counted); the footer's "days" uses the game's number, livestock included.
2. **Recruits with a foot-or-horse choice** (12 vanilla stacks) — count them as needing a horse?
   Proposed: yes (better a spare horse than a stuck upgrade), setting `WarMountsCountChoiceTroops`.
3. **`horse`-category animals are both footmen's mounts and upgrade horses** — proposed: reserve
   what upgrade-ready troops need first (cheapest first, as vanilla consumes them), the rest count
   as mounts.
4. **Prisoner locks** — vanilla's ransom skips prisoners locked in the party screen. Proposed:
   honour them like item locks (never ransomed, not even proposed).
5. **Modified animals** (lame, spirited…) — proposed: count them as mounts / pack animals (the game
   does after a reload), never buy modified ones.

**Technical recommendations for later steps** (developer calls, no board line):
- Step 4/6: a price-oracle interface in Core; the Module implements it with
  `TradeItemPriceFactorModel.GetPrice` + hypothetical `inStoreValue`.
- Step 5: put all MCM contact in a small satellite `SmartSteward.Mcm.dll` loaded by hand when MCM is
  present (the TrainingBattles naval-satellite pattern — with the fluent builder there is no
  discovery-timing risk), or keep it bodies-only in the Module and gate the build with
  TrainingBattles' `AssemblyGuard`. Either way test once with MCM disabled.
- Step 6: executor = one headless `InventoryLogic` per visit (sells then buys, one `DoneLogic`),
  `SellPrisonersAction` for ransom, roster moves + `OnPrisonerDonatedToSettlement` for donation,
  the three companion calls, a `BuyMercenaries` twin. Re-check gold/limits at execute time.
- Step 7: window layer order **305** (below the encyclopedia), register the two hot-key categories,
  ReleaseMovie on close.
- Step 8: arrival = `SettlementEntered` + `GameMenuOpened` + next-tick open; leave warning by
  wrapping `OnConsequence` of `town/town_leave`, `village/leave`, `village/leave_set_sail`,
  `village/leave_at_sea`, `port_menu/sail_option`.

**Nothing in DESIGN.md is impossible in v1.4.8, and no hook needs Harmony.** Still to prove in
game: the arrival timing (step 8) and the encyclopedia round-trip (step 7).
