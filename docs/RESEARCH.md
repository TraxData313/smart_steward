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
  **[corrected 2026.09.28 — step 15]** For a TROOP it is not the equipment: `BasicCharacterObject.IsMounted` returns
  `_isMounted = DefaultFormationClass.IsMounted()`, set once when the troop's XML is read (`default_group` → the
  formation class; `TroopClassExtensions.IsMounted` = `DefaultClass()` is Cavalry, or the class is HorseArcher — so
  HeavyCavalry/LightCavalry count, `Core\BasicCharacterObject.cs` Deserialize, `Core\TroopClassExtensions.cs`). Only a
  HERO is judged by his gear: `CharacterObject.IsMounted` overrides it with `Equipment[10]`, and a hero's `Equipment` is
  `HeroObject.BattleEquipment` (`CS\CharacterObject.cs`). The steward carries exactly that bool per hired troop type
  (`MercenaryOffer.IsMounted`, `WandererForHire.IsMounted` — step 16's recruits the same) and counts every man hired who is
  not mounted as a footman (DESIGN §1.1, the live re-plan). NB War Sails' sea weight looks at the equipment instead
  (`Equipment.Horse`, §19) — two different rules for "has a horse", each used where the game uses it.
- **Available mounts** = `ItemRoster.NumberOfMounts` (items with `HorseComponent.IsMount`). War
  horses, noble horses and camels count; **pack animals never carry footmen**.
- **Mounted footmen** = `min(footmen, NumberOfMounts)` → `+0.15 × mountedFootmen / totalMen`
  (×0.3 less in wet weather; Riding.NomadicTraditions adds). Cavalry: `+0.3 × withHorse / totalMen`.
- **Herd** = `NumberOfPackAnimals + NumberOfLivestockAnimals + max(0, NumberOfMounts −
  mountedFootmen)`. Penalty only when herd > total men: `max(−0.8, −0.3 × (herd − men) / men)`
  (Riding.Shepherd softens). **Surplus mounts are herd**, so a 10% buffer costs nothing until
  animals outnumber men. **[step 18]** Re-read line by line for the footer's herd line — exact rule, army pooling,
  land only: §23.
- **Capacity** (`DefaultInventoryCapacityModel`): 10 base + 20 per healthy member + **20 per mount**
  + **100 per pack animal** (× Scouting.BeastWhisperer, Riding.DeeperSacks, Steward.ArenicosMules;
  Trade.CaravanMaster on the total). Over capacity → up to −0.4 ("Overburdened"). The full formula, at sea too: §19.
- **Animals weigh 0** in carried weight (`GetItemEffectiveWeight` returns 0 for any
  `HasHorseComponent`) — selling a horse frees no weight.
- **Quirk:** the incremental counters skip animals with an `ItemModifier` (lame, old…) —
  `OnRosterUpdated` counts pack/mount only when `ItemModifier == null`; a full recount
  (`CalculateCachedStats`, on load / `Clear()`) counts them. In-session the speed readout can be
  off by the modified animals. **[step 17]** Which modifiers exist and what the steward does with them: §22.

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
- **[step 17 — Anton 2026.09.28]** The steward no longer reads any of this: no ready counts, no reservation. War horses are
  a plain number to keep (DESIGN §2.4); the facts above stay for the record (the snapshot's upgrade stacks, `ReadUpgrades`
  and `GameRules.UpgradeReadyCount` are gone).

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
- **A lock is a trade-SCREEN thing only** (verified step 13, 2026.09.28): `SPInventoryVM` reads `IsLocked` only in
  `TransferAll` (the skip above), when it builds the item rows (`IsItemLocked`) and in `ProcessLockItem`; the single-item
  sell (`ProcessSellItem` → `SellItem`) never checks it (the screen's widgets were not checked for a drag block).
  `InventoryLogic` (`CS\TaleWorlds.CampaignSystem.Inventory\InventoryLogic.cs`) and everything else in that namespace
  has no notion of locks at all, so the headless trade (§9) sells a locked stack like any other: the ONLY lock check is
  ours (`PlanExecutor.TradeOne` → Core `ExecutionBudget.StoppedByLock`). Selling a locked stack leaves its id in
  `_inventoryItemLocks` (nothing prunes it — the list is by id; `SPInventoryVM` marks a newly arrived player-side row
  from it), so the same item bought back later shows locked again.

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
  ("Set sail", isLeave) — reachable by id without referencing the DLC. A town reached by sea opens `port_menu`, never
  `town` (§18).
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
  the code, **not yet proven in game** — verify in PLAN step 8.) **Step 8 built it** with a stricter "quiet map"
  test and a few frames' patience (§15); still to be seen in game (PLAYTEST "Step 8").
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
  "memory" — **but see the step 5 facts below: use `"none"`**),
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

**MCM 5.12.3 and the loader — verified in step 5 (2026.09.27)** (MCMv5.dll IL via ilspycmd, the UI in
`Bannerlord.MBOptionScreen.v1.4.8.dll`, and `tools\McmProbe` driving the real builder):
- **The default "memory" format throws on the first registration.** `MemorySettingsFormat.Load` is
  `if (_settings.TryGetValue(key, out v) || settings != v) OverrideSettings(settings, v)` (checked in
  the IL) — with no stored copy `v` is null, and `OverrideValues(current, null)` dereferences it
  (`GetUnsortedSettingPropertyGroups` → `settings.DiscoveryType`). `RegisterSettings` has already
  added the settings, so the page may still show, but `Register()` throws. `MCMSubModule` registers
  `MemorySettingsFormat` **twice** and `NoneSettingsFormat` never; the implementation adds `json`/`json2`
  and `xml`. So **`SetFormat("none")`** matches no format: `Load` and `Save` are skipped (`?.`), MCM
  keeps and writes nothing, and a `Configs\ModSettings` folder is only touched for an empty FolderName
  (= its root). `json` would be worse: MCM's own file would override our values at every start.
- **The "Default" preset snapshots CURRENT values**: `DefaultSettingsBuilder`'s constructor creates
  preset `"default"`; `BuildAsGlobal` calls `SetPropertyValue(id, ref.Value)` for every property, and
  `SetPropertyValue` keeps the FIRST value per id. Filling that preset ourselves first
  (`CreatePreset("default", …)`) makes MCM's Default / reset buttons mean the registry defaults.
- **The UI writes live**: every control change is `URS.Do(action)` → `DoAction()` → `ref.Value = v`
  at once; Cancel is `UndoAll()` (through the same setter); Done calls `SaveSettings` (format save —
  a no-op for "none" — then `PropertyChanged("SAVE_TRIGGERED")`). So saving on every setter call is right.
- **The control type comes from `ProxyRef<T>`'s T** (`SettingsPropertyDefinition` ctor): bool, int,
  **float** (not double — values are read `value is float`), string, a dropdown when T is an `IList<>`
  with `SelectedIndex`, `Action` = button.
- **A dropdown pick never calls the setter**: `SetSelectedIndexAction` reads `ref.Value` and sets
  `SelectedIndex` on THAT object (via `SelectedIndexWrapper`, reflection through Harmony's AccessTools2).
  Keep one `Dropdown<string>` per setting and listen to its `PropertyChanged("SelectedIndex")`; presets
  (`OverrideValues`) do call the setter with a whole dropdown.
- Property builders are stored per group **by display name** (`Properties[name]`) — labels must be
  unique within a group; presets address properties by **id**. Group paths split on the sub-group
  delimiter `/`. Display names, group names, hints and dropdown labels all go through `TextObject`
  (`{=id}` works; `{X}` is a variable — no braces in texts). The hint shows as `Name: hint`.
- **What `GetTypes()` really resolves** (.NET Framework, canary classes built into SmartSteward.dll and
  run through `tools\check-soft-deps.ps1`): a class **deriving** from an MCM type → `GetTypes()` throws
  (the module would be unloaded). A reference-type **field** of an MCM type (`Dropdown<string>`) and a
  cached lambda (`<>c` static field typed `Action<ISettingsPropertyGroupBuilder>`) do **not** — field
  types of reference type are not resolved at type load. The "no MCM fields, no lambdas" rule above is
  kept anyway: any reflective scan that reads fields would hit them.
- **The game reflects over our static methods**: `CommandLineFunctionality.CollectCommandLineFunctions`
  (TaleWorlds.Library) runs `GetMethods(Static | Public | NonPublic)` + attribute lookup on every type of
  every assembly referencing TaleWorlds.Library. Keep MCM types out of STATIC method signatures (the
  check script resolves every static signature).
- **Newtonsoft**: Core compiles against the NuGet package 13.0.1 (assembly 13.0.0.0, token
  30ad4fe6b2a6aeed — the game's exact identity) with `PrivateAssets="all"`, so no copy reaches the
  Module's output; at runtime the game's own DLL answers (the probe ran Core's file code on it). Its
  reader also accepts trailing commas; `JsonTextReader.DateParseHandling = None` keeps date-like ids text.

---

## 13. The game adapter — verified in step 6 (2026.09.27)

Read in `game-decompiled-1.4.8` while writing `src\SmartSteward.Module\Adapter\` (and, for the popup text,
`TaleWorlds.TwoDimension.dll` decompiled with ilspycmd). Every method body of the built DLL was JIT-compiled
out of game against the real game DLLs (`RuntimeHelpers.PrepareMethod`, .NET Framework) — all resolve.

**Headless trade — the details `InventoryLogic` hides** (`CS\...Inventory\InventoryLogic.cs`, `CS\Helpers\InventoryScreenHelper.cs`):
- `Initialize(...)` installs a **`FakeInventoryListener`** (gold 0) — call `SetInventoryListener` AFTER it, as
  `OpenScreenAsTrade` does, or the merchant has no purse. Vanilla builds the logic as
  `new InventoryLogic(settlementComponent.Owner)` and `Initialize(settlement.ItemRoster, PartyBase.MainParty.ItemRoster,
  PartyBase.MainParty.MemberRoster, isTrading: true, isSpecialActionsPermitted: true, CharacterObject.PlayerCharacter,
  InventoryCategoryType.None, marketData, useBasePrices: false, InventoryMode.Trade)`.
- `TransactionDebt`'s setter calls `TotalAmountChange(...)` only when the value changes (Initialize sets 0 → no
  call), but every transfer does — set it before the first command.
- `TransferItem` moves `Amount` units one by one, pricing each with `GetItemPrice` BEFORE it moves (so the town's
  in-store value walks per unit), and **does nothing at all** when the from-roster holds fewer than `Amount`
  (`DoesTransferItemExist`) or the element is a quest item — no error. The executor sends one unit per command and
  checks the roster moved.
- `GetItemPrice(element, isBuying)` = `MarketData.GetPrice(element, OwnerParty, !isBuying, OtherParty)`, except a
  buy-back: when the last transfer of the SAME element went the other way, the last price is reused.
- `DoneLogic()` returns false only while previewing or when `Hero.Gold − TotalAmount < 0`; otherwise it pays the
  player `min(−TotalAmount, merchant gold)` — the NET of sales and buys —, the merchant through the listener's
  `SetGold`, Trickle Down (towns, ≥ 10,000 of trade goods bought), then `OnPlayerInventoryExchange(bought, sold, true)`.
  It needs `Settlement.CurrentSettlement` (the party must still be in the settlement).
- `Reset(fromCancel: true)` (vanilla's Cancel) = `Clear()` + re-`Add` of the backups on the LIVE rosters.
  `ItemRoster.Clear()` fires `RosterUpdatedEvent(default, 0)` → `TownMarketData.OnTownInventoryUpdated` → `ClearStores()`
  (InStore/InStoreValue zeroed for every category), then every element re-added walks them back — the category data
  is rebuilt from the roster. Safe to undo a half-done batch.
- Vanilla's `MerchantInventoryListener` is private: GetGold/SetGold → `SettlementComponent.Gold` / `ChangeGold(gold −
  Gold)` (clamps at 0), `GetOppositeParty`/`GetTraderName` → `Owner`; its `OnTransaction` throws and is never called.
- A roster holds ONE element per item + modifier: `FindIndexOfElement` → `EquipmentElement.IsEqualTo` compares only
  `Item` and `ItemModifier`. So `item|modifier` is a unique stack key on each side.

**Access and settlement state:**
- Trade (`DefaultSettlementAccessModel.CanMainHeroTrade`): a village trades only when `VillageState == Normal` AND its
  roster has items — a village with gold but nothing on offer allows **no trade at all** (not even selling); raided →
  no trade; hostile → disabled. It reads `Settlement.CurrentSettlement.Village` — call it while in the settlement.
  Use the return value (`MenuHelper.SetOptionProperties`: shown-but-disabled is still "cannot").
- Dungeon (`CanMainHeroEnterKeepInternal`): own clan or same faction → FullAccess; LimitedAccess needs
  `BribePaid ≥ BribeCalculationModel.GetBribeToEnterDungeon`.
- `Town.AllTowns` holds towns only (castles are a separate list). `InventoryLogic.InitializeCategoryAverages` excludes
  the current town (a village: `Village.Bound.Town`, which may be a CASTLE — then nothing is excluded) yet always
  divides by `AllTowns.Count − 1`. We divide by the towns actually summed.
- `GetTradePenalty(item, MainParty, merchant: null, …)` is null-safe: no settlement, war or network terms — our
  "average town at peace" for the placeholders.

**Party reads:** `MobileParty.FoodChange` (daily, negative); livestock food = Σ `Amount × HorseComponent.MeatCount`
over `IsLiveStock` elements (what `ItemRoster.TotalFood` adds); `EquipmentElement.GetEquipmentElementWeight()` is
per roster unit (a consumable's whole stack); animals carry 0 (`GetItemEffectiveWeight`). Upgrade-ready
(`PartyCharacterVM.InitializeUpgrades`): `DoesPartyHaveRequiredPerksForUpgrade(party, troop, target, out _)` false →
0; bandit cultures also need `CanPartyUpgradeTroopToTarget` (else the button is disabled); `Character.Culture.IsBandit`.

**Prisoners:**
- Vanilla's donate screen moves prisoners with `AddToCounts` on BOTH live rosters (heroes too — an owned roster calls
  `OnHeroRemoved`/`OnHeroAdded`), then `DonatePrisonersDoneHandler` runs `EnterSettlementAction.ApplyForPrisoner` for
  heroes and fires ONE `OnPrisonerDonatedToSettlement(MainParty, FlattenedTroopRoster, settlement)`
  (`FlattenedTroopRoster.Add(troop, number, wounded)`). Influence = Σ model value, then
  `GainKingdomInfluenceAction.ApplyForDonatePrisoners` × 1.2 under Military Coronae (`Kingdom.ActivePolicies.Contains(
  DefaultPolicies.MilitaryCoronae)`); nothing without a kingdom.
- `TroopRoster.AddToCounts(c, n, insertAtFront, wounded)`: `Number` includes the wounded; a dummy roster
  (`CreateDummyTroopRoster`) has no owner party. `SellPrisonersAction` pays `PrisonerRansomValue(c,
  sellerParty.LeaderHero)` × n through `GiveGoldAction` to the main hero; a hero is freed (`ApplyByRansom`).

**Tavern:** `TownMercenaryData.HasAvailableMercenary()` = `TroopType != null && Number > 0`; `GetMercenaryData(town)`
creates an empty record when none; `ChangeMercenaryCount` fires `OnMercenaryNumberChangedInTown`. Vanilla's hire
dialogue: `conversation_hero_hire_on_condition` (not a player companion, Occupation Wanderer, not a prisoner,
`PartyBelongedTo == null`) and `Hero.MainHero.Gold > GetCompanionHiringPrice(hero)` (strict). The tavern placement
rule: `HeroesWithoutParty` ∧ (`IsWanderer` ∨ `IsPlayerCompanion`) ∧ not governor of this town.

**Menus and popups:** `CampaignGameStarter.AddGameMenuOption` creates the menu if it does not exist yet
(`GetPresumedGameMenu`); `GameMenu.AddOption` inserts at `index` when `0 ≤ index ≤ count`, else appends.
`Campaign.Current.GameMenuManager.GetGameMenu(id).MenuOptions` (`IdString`), `RefreshMenuOptions(Campaign.Current.
CurrentMenuContext)` → `Handler.OnMenuRefresh()`. `InquiryData(title, text, affirmativeShown, negativeShown,
affirmativeText, negativeText, Action affirmative, Action negative, …)`; `InformationManager.ShowInquiry(data,
pauseGameActiveState, prioritize)`. The single-query popup's text is a **`RichTextWidget`** inside a scrollable panel
(max 480 px, then it scrolls): `RichTextParser` (TaleWorlds.TwoDimension) treats `<` as the start of a tag, a lone `>`
as plain text, `\n` as a new line and drops `\r` — never put `<` in popup text.

---

## 14. The Party Steward window — verified in step 7 (2026.09.27)

Read in `game-decompiled-1.4.8` plus `TaleWorlds.GauntletUI.PrefabSystem` / `.Data` / `TaleWorlds.Localization`
(decompiled with ilspycmd for this step), the game's `Modules\*\GUI` data and `GUI\GauntletUI\Fonts\*.fnt`. The gate is
**`tools\check-gui.ps1`** (every tag, attribute, literal value, brush, sprite category and view-model binding of our
prefabs against the game's DLLs, brush/sprite XML and the built SmartSteward.dll); the game's own
`WidgetPrefab.LoadFrom` also parses our file out of game.

**What a prefab mistake does** (so the checker knows what matters):
- Unknown **brush** name → `BrushFactory.GetBrush` returns **null** and the widget gets a null brush (render crash
  territory). Only Native / SandBoxCore / SandBox brushes are always there in a campaign.
- Unknown **widget tag** → `WidgetFactory.CreateBuiltinWidget` makes a plain `Widget` + `Debug.FailedAssert`.
- Unknown **attribute** → silently ignored (`WidgetExtensions.SetWidgetAttributeFromStringAux` returns when the property
  path does not resolve); a bad literal (enum name, number) throws inside and is caught with a FailedAssert.
- Missing **VM property** → the binding reads null; missing **command** → `ViewModel.ExecuteCommand` does nothing.
- A VM **getter, setter or command that throws** → `MethodInfo.InvokeWithLog` logs and **rethrows** into the game. Every
  command / two-way setter of ours runs inside `StewardWindowVM.Guard`; a failure closes the window on the next tick.
- An element's **own bindings resolve against its own `DataSource`** (`GauntletView.RefreshBinding` uses the view-model
  path including it): `IsVisible="@IsExpanded"` on a `<ListPanel DataSource="{Breakdown}">` binds to the LIST → wrap
  the list in a Widget that carries the visibility.
- `ScrollablePanel` has **no `MouseScrollAxis`** in 1.4.8 (TrainingBattles' prefabs carry it as a dead attribute; the
  wheel scrolls anyway).

**Layer life cycle** (vanilla map views, e.g. `GauntletMapCampaignOptionsView`): `new GauntletLayer(name, order) {
IsFocusLayer = true }`, `LoadMovie`, `Input.RegisterHotKeyCategory(...)`, `InputRestrictions.SetInputRestrictions()`,
`screen.AddLayer`, `ScreenManager.TrySetFocus`; close = `ResetInputRestrictions`, `RemoveLayer`, `TryLoseFocus`.
`RemoveLayer` finalizes the layer, and `GauntletLayer.OnFinalize` asserts on a movie never released → **`ReleaseMovie`
first**. `TrySetFocus` compares `InputRestrictions.Order`: at 305 we sit under the Encyclopedia (310).

**Keys**: `Input.IsHotKeyDown("FiveStackModifier" | "EntireStackModifier")` (GenericCampaignPanelsGameKeyCategory) and
`IsHotKeyReleased("Exit")` (GenericPanelGameKeyCategory = Escape) — only for categories registered on the layer.
`IsHotKeyReleased` fires only when the key went down while the layer had key permission, so the Encyclopedia's own
Escape does not leak into us; two guard frames after it closes anyway.

**Encyclopedia**: `EncyclopediaData` fires `EncyclopediaPageChangedEvent` with the page kind for every page it opens
(Hero, Unit, lists…) and `None` on close — the open/closed flag needs nothing else. `EncyclopediaManager.GoToLink(type,
id)` dereferences the page before its null check → always try/catch. Hero link = `Hero.EncyclopediaLink`, unit link =
`CharacterObject.EncyclopediaLink`.

**Disabled buttons and tooltips**: a disabled widget gets no mouse events (`EventManager.AnyWidgetsAt` /
`CollectEnableWidgetsAt` skip disabled children), so a tooltip on a greyed button uses vanilla's wrapper: a Widget
holding the button and a `HintWidget IsDisabled="true"` — `HintWidget` relays its PARENT's HoverBegin/HoverEnd to
`ExecuteBeginHint` → `MBInformationManager.ShowHint(text)` (TaleWorlds.Core).

**Text boxes**: `EditableTextWidget RealText="@X"` is two-way and writes on every key (`TextQueryPopup` uses it).
`IntegerInputTextWidget` cannot be empty (an int `IntText`; −1 shows "-1", deleting every digit gives 0), so the price
book's "empty = placeholder" boxes are EditableTextWidgets parsed by Core (`UiFormat.TryParseBase`).

**Fonts**: FiraSansExtraCondensed-Regular and Galahad (`GUI\GauntletUI\Fonts\*\*.fnt`) carry `– — × · » ± … • ‹ ›` but
**not** `→ − ≈ ⟲ ▸ ✓` (only FreeSerif-Dingbat has some). The window writes the minus as an en dash, "becomes" as `»`, and
draws ⟲ and ▸ as sprites.

**Always-loaded art we use** (all `ui_group1`): `ButtonSimpleBrush` (Native Brush.xml — a flat BlankWhiteSquare_9
button with Default/Hovered/Pressed/Disabled/Selected colour factors, any size), `RefreshButton.Flat` (a refresh icon =
our ⟲), `SPOptions.Checkbox.Empty/Full.Button`, `SPGeneral\SPOptions\collapser_indicator_closed/open` (▸ / ▾),
`Header.Tab.*` + `Clan.TabControl.Text` (SandBox Clan.xml), the Popup.* brushes. `Popup.Button.Base` has a Disabled style
(ColorFactor 0.5), `Popup.Button.Text` too (TextAlphaFactor 0.5).

**Game data for the Prices tab**: `TaleWorlds.CampaignSystem.Extensions.Items.All` (= `Campaign.Current.AllItems`),
`ItemObject.NotMerchandise`, `ItemCategory.GetName()`. Placeholders come from `SnapshotBuilder.AverageOf` — the snapshot's
own formula (§13), so the tab and the planner agree.

---

## 15. Triggers — verified in step 8 (2026.09.27)

Read in `game-decompiled-1.4.8` and War Sails' `NavalDLC.dll` (decompiled with ilspycmd for this step) while writing
`src\SmartSteward.Module\StewardTriggers.cs`. Every method of the built DLL JIT-compiled against the real game DLLs
(561, none failed); nothing here was run in game yet.

**Arrival order** (`CS\...Encounters\PlayerEncounter.cs` `Init`): for the main party walking into a town or village
(not under raid or siege) `EnterSettlement()` (→ `EnterSettlementAction.ApplyForParty` → `SettlementEntered`) runs
BEFORE `GameMenu.ActivateGameMenu(encounterMenu)` — also when the menu is `town_outside` (crime, war: the party is
"in" but the town menu comes later). `MobileParty.CurrentSettlement`'s setter also sets `LastVisitedSettlement` — so
while inside, LastVisitedSettlement IS the current settlement. `LeaveSettlementAction` fires `OnSettlementLeft`.
War Sails' `NavalEncounterMenuModel` returns the base model's menu for a normal arrival (its storyline aside): `town_outside`
→ `town` by land, but **`naval_town_outside` → `port_menu` by sea** — the town menu never shows (§18, found in round 3).

**`GameMenu.RunOnInit`** runs `OnInit(args)` FIRST — which may switch menus (the village menu's init switches a looted
village to `village_looted`) — and THEN fires `GameMenuOpened` with the ORIGINAL menu's args. So a `village` event can
arrive while `village_looted` shows: check the live menu (`MapState.MenuContext.GameMenu.StringId`) before acting.

**A menu click** (`GameMenu.RunMenuOptionConsequence`, reached through `MenuContext.InvokeConsequence` →
`GameMenuManager.RunConsequenceOfVirtualMenuOption` → `RunConsequencesOfMenuOption(context, index)`):
`option.RunConsequence(context)` = `OnConsequence(args)` then `menuContext.OnConsequence(option)` (attribute handlers),
and THEN `CampaignEventDispatcher.OnGameMenuOptionSelected(menu, option)` — **fired even when the wrapped consequence did
not leave.** Its listeners: `IncidentsCampaignBehaviour` (`town/town_leave`, `village/leave`, `castle/leave` → a
leaving incident with `DefaultIncidentModel.GetIncidentTriggerGlobalProbability()` = 0.5, cooldowns apply),
`ExtortionByDesertersIssueBehavior` (any `IsLeave` option → `TickDesertersPartyLogic`, which does nothing while the
party is still in the quest settlement), the tutorial system. `RunConsequencesOfMenuOption(context, index)` is public:
re-running an option by its index in `GameMenu.MenuOptions` is exactly the click (minus the repeat-object mapping).
`MenuCallbackArgs(MenuContext, TextObject)` is a public constructor.

**Incidents** (`IncidentsCampaignBehaviour`): `InvokeIncident` only sets `MapState.NextIncident`; `Campaign.RealTick`
starts it on the next campaign tick whatever the menu (`MapState.StartIncident` → MapScreen adds `MapIncidentView`, whose
`CreateLayout` synchronously pauses the engine, adds layer "MapIncidents" at order **203** and calls
`SetIsMapIncidentActive(true)`). Entering incidents roll on the first `town`/`village`/`castle` GameMenuOpened after a
`SettlementEntered` with no menu up — the same moment as our arrival. A leaving trigger is vetoed when
`LastVisitedSettlement.IsSettlementBusy(this)`: **`CampaignEvents.IsSettlementBusyEvent`** (`ReferenceIMBEvent<Settlement,
object, int>`, handler `(Settlement, object asker, ref int priority)`; > 0 = busy; vanilla quests use it for hideouts)
lets us answer "busy" for the click that only showed our warning (asker type `IncidentsCampaignBehaviour`).

**The leave options** (`PlayerTownVisitCampaignBehavior.AddGameMenus`, `OnSessionLaunched`): `town/town_leave` and
`village/leave` (condition: not at sea; in an army only for its leader), `village/leave_set_sail` (not at sea, the
village has a port; `SetSailAtPosition(PortPosition)` + `PlayerEncounter.Finish`), `village/leave_at_sea` (at sea), all
`isLeave`. Leaving = `game_menu_settlement_leave_on_consequence` (gate position, `PlayerEncounter.LeaveSettlement`,
`Finish`, `SetMoveModeHold`, `SignalAutoSave`). `village_looted` has its own `leave` / `leave_set_sail` (not guarded —
a looted village gets nothing). **War Sails** (`NavalDLC.CampaignBehaviors.NavalTransitionCampaignBehavior`) registers
in **`OnAfterSessionLaunched`** — after EVERY behavior's `OnSessionLaunched`, whatever the module order: `town/port`
("Go to the port", inserted at index 1) and `port_menu` with `leave_option` / `leave_option_isleave` ("Go to the town
center" — back to `town`, NOT a leave), `call_fleet`, `inspect_fleet`, `manage_fleet`, `repair_ships`, `trade`,
`enter_port`, `port_wait` and **`sail_option`** ("Set sail", isLeave → `SetSailAtPosition(PortPosition)` +
`PlayerEncounter.Finish(true)`). So the guard wraps lazily, at each menu's first `GameMenuOpened` — a load-before in
SubModule.xml could never make an eager wrap see the port menu.

**Is the map quiet?** (all public, SandBox.View `MapScreen` unless noted): `IsReady`, `IsInMenu` (the menu's view is
up), `IsEscapeMenuOpened`, `IsInBattleSimulation`, `IsInTownManagement`, `IsInHideoutTroopManage`, `IsInArmyManagement`,
`IsInRecruitment`, `IsInCampaignOptions`, `IsMarriageOfferPopupActive`, `IsMapCheatsActive`, `IsMapIncidentActive`,
`IsHeirSelectionPopupActive`, `EncyclopediaScreenManager.IsEncyclopediaOpen` (vanilla's `MapNavigationHelper` checks the
same set); `InformationManager.IsAnyInquiryActive()` (TaleWorlds.Library); `ConversationManager.IsConversationFlowActive`
/ `IsConversationInProgress`; `MapState.AtMenu`, `MapState.MenuContext`, `MapState.NextIncident`. Menu layers: the menu
"MapMenuView" **100**, its overlay "MapMenuOverlay" **202**, incidents **203**, our window 305.

## 16. Strings and translations — verified in step 9 (2026.09.27)

`TaleWorlds.Localization.dll` decompiled for this (ilspycmd, into `game-decompiled-1.4.8\TaleWorlds.Localization`), and
the installed modules' `ModuleData\Languages` folders read.

**What the game reads** (`LocalizedTextManager.LoadLocalizationXmls(loadedModules)`, `LanguageData.Deserialize`): for
every loaded module, every `language_data.xml` anywhere under `<module>\ModuleData\Languages` (recursive). Each holds
`<LanguageData id="Deutsch">` with `<LanguageFile xml_path="DE/…xml" />` lines — paths relative to the `Languages`
folder — merged by language id into the game's `LanguageData` (so a mod needs no `supported_iso`: Native's own file
gives the codes; `IsValid` is the union). Nothing else in `Languages` is read.

**English is the code.** A `TextObject("{=id}English")` shows its own English unless the current language's loaded
files have that id. The vanilla modules keep their English SOURCE as `Languages\std_*.xml` at the root (SandBox:
`std_SandBox_GauntletUI.xml` …), never named by a `language_data.xml` (SandBox's English one lists only a voice file):
the file is for translators. Translations: `Languages\DE\language_data.xml` + `DE\std_*_ger-DE.xml`.

**The strings format** (vanilla, both the English source and a translation):
`<base xmlns:xsi="…" xmlns:xsd="…" type="string"><tags><tag language="English" /></tags><strings>
<string id="…" text="…" /> …</strings></base>` — a translation's tag is the language id (`Deutsch`). Community mods
(ShowSkillLimit) ship the same with an `EN\language_data.xml` of their own; not needed, and not what vanilla does.

**Ours**: `module\ModuleData\Languages\std_SmartSteward.xml` — 229 ids (the window, the leave question, the autonomous
report, the menu entry, the settings' `ss_set_` / `ss_hint_` / `ss_opt_` / `ss_grp_` built from the registry for MCM
and the Instructions tab). Generated and held to the code by `StringsFileTests`; deploy.ps1 copies `ModuleData`.

## 17. Playtest round 1 — "cannot enter towns after a load" (2026.09.28)

**The symptom** (Anton, 2026.09.28): after a save was loaded in the same game session, clicking a town or a village no
longer took the party there; on a town the game said *"Your clan tier is not high enough to request a meeting."* A
restart cured it.

**Where that text comes from** (the only place in 1.4.8): `DefaultSettlementAccessModel.IsRequestMeetingOptionAvailable`
(clan tier < 3). It reaches the screen as a QUICK INFORMATION through `DefaultEncounterModel.CanMainHeroDoParleyWithParty`
← SandBox.View `SettlementVisual.OnMapClick(followModifierUsed: true)`: `MapScreen.HandleLeftMouseButtonClick` passes
`SceneLayer.Input.IsHotKeyDown("MapFollowModifier")`, and with it held a settlement click is a **parley request**, not a
move. The parley check only answers for a fortification AT WAR with the player (then the explanation shows); for every
other town and every village it returns false with no text — **nothing happens, the party does not move**.
`MBInformationManager.AddQuickInformation` also `Debug.Print`s its text, which is how it reached the game's log.

**The evidence**: `rgl_log_39992.txt` (the session) has the line three times — 09:33:03, 09:33:07, 09:33:15 — after the
load (save "fst3" at 09:32:21, exit to the main menu 09:32:27, loaded 09:32:35–09:32:48); no exception, no assert, no
failed load anywhere; our log shows no arrival after the load (the party never entered anything). **`MapFollowModifier` =
Left Alt** (and a controller's LB) — `MapHotKeyCategory`. `IsHotKeyDown` reads the engine's global key state
(`Key.IsDown` → `Input.IsKeyDown`), untouched by layers except the keys-allowed flag.

**The cause of the stuck key — inferred, not proven**: the same log shows Alt+Tabs (`OnGameWindowFocusChange: False`
09:29:43 → `True` 09:30:35, the last focus change before the quit at 09:33:19). Leaving by Alt+Tab and coming back by a
mouse click can leave the engine believing Left Alt is still down — the Alt release happened outside the window. (The
managed side only clears the layers' "last down keys" on a focus change — `ScreenManager.OnGameWindowFocusChange` →
`ResetLastDownKeys`; the key state itself lives in the native engine, not in the decompile.) One tap of Left Alt, or a
restart, frees it. The load was a coincidence: no map click to a settlement is in the log between the
Alt+Tab and the load, so the stuck state may well predate it.

**A closed market's words** (step 12, for the window): `CanMainHeroDoSettlementAction(…, Trade, out _, out disabledText)`
→ `DefaultSettlementAccessModel.CanMainHeroTrade` gives, for a village: being raided → false with NO text; hostile →
"You cannot trade with a hostile village."; nothing on offer but gold → "There are no available products right now.";
no gold either → "Village shop is not available right now."; for a town: disguised without Smuggler Connections →
"{PERK_NAME} perk required to trade while in disguise." A looted village is our own check (no market at all).

**Not the steward**: the mod never writes input (`Input.PressKey` / `ClearKeys` unused; it only reads Shift / Ctrl /
Escape on its own layer while its window is open — closed at 09:31:18), and no state of it survives a load (audit in
TASKS_DONE step 12). Since step 12 `InputWatch` writes one log line when the map has read `MapFollowModifier` as held for
5 s with no menu up, and one when it lets go — the next report says at once whether this was it.

## 18. War Sails' port — a town reached by sea (verified in step 14, 2026.09.28)

Read in `game-decompiled-1.4.8\NavalDLC\` (NavalDLC.dll decompiled with ilspycmd for this step, `$env:DOTNET_ROLL_FORWARD=
'LatestMajor'`) and the base game. Anton's round-3 finding: docking at a town never brought the steward — because a sea
arrival never shows the `town` menu.

**The sea arrival** (base game, not the DLC): `PlayerEncounter.Init` asks `EncounterGameMenuModel.GetEncounterMenu`;
`DefaultEncounterGameMenuModel` returns **`naval_town_outside`** for a town when `MobileParty.MainParty.IsCurrentlyAtSea`
(every branch — no siege, under siege without an active blockade, …), where a land arrival gets `town_outside`. War Sails'
`NavalEncounterMenuModel` passes the base model's answer through (only its storyline swaps menus). As on land,
`EnterSettlement()` — `SettlementEntered` — runs BEFORE the menu is activated (the settlement is not raided or besieged).
`naval_town_outside`'s init (`EncounterGameMenuBehavior.naval_town_outside_on_init`) then: hostile → "you will not be allowed
to dock" (stays); criminal (`game_menu_town_disguise_yourself_on_condition`) → "…not allowed to dock" (stays); under siege (not hostile,
no active blockade) → `game_menu_naval_town_outside_enter_on_consequence` (EnterSettlement if needed, then `port_menu` or
`join_siege_event`); otherwise **`GameMenu.SwitchToMenu("port_menu")`**. A village reached by sea is NOT special:
`village_outside`'s init switches straight to `village` (whose `leave_set_sail` / `leave_at_sea` §15 covers).

**`GameMenuOpened` on that path**: `MenuContext.SwitchToMenu` → `HandleStates` re-enters itself — the nested pass runs
`port_menu`'s `PreInit` → `RunOnInit` (its OnInit, then `GameMenuOpened`) → `OnMenuCreate`; back in the outer pass
`naval_town_outside`'s `GameMenuOpened` fires with args whose `MenuContext.GameMenu` is ALREADY `port_menu`. So a listener
reading `args.MenuContext.GameMenu.StringId` sees `port_menu` twice; the town menu never opens until the player picks "Go
to the town center" (`port_menu/leave_option` → `ActivateGameMenu("town")`). The steward's arrival was waiting for `town`.

**The port menu** (`NavalDLC.CampaignBehaviors.NavalTransitionCampaignBehavior.AddGameMenus`, registered in
`OnAfterSessionLaunched`): `port_menu` "You are at the port." with `leave_option` / `leave_option_isleave` ("Go to the town
center" — the second is the isLeave variant shown while the fleet is not docked; both go to `town`, NOT a leave),
`call_fleet`, `inspect_fleet`, `manage_fleet`, `repair_ships`, **`trade`**, `enter_port`, `port_wait`, **`sail_option`**
("Set sail", isLeave: `SetSailAtPosition(PortPosition)` + `PlayerEncounter.Finish()`). The town menu gets `town/port` ("Go
to the port", index 1; only for a town with `HasPort`).
- **Trade at the port = the town's market**: `trade_on_condition` asks `SettlementAccessModel.CanMainHeroDoSettlementAction(
  Settlement.CurrentSettlement, Trade, …)` — the very gate of the town's own Trade — and `trade_on_consequence` opens
  `InventoryScreenHelper.OpenScreenAsTrade(Settlement.CurrentSettlement.ItemRoster, …Town)`: the same roster, the same
  merchant. War Sails' `NavalDLCSettlementAccessModel.CanMainHeroDoSettlementAction` only adds "cannot WAIT in a village
  at sea"; Trade goes to the base model. So the snapshot, prices and executor are exactly the town's.
- The tavern, the ransom broker and the wanderers are the same settlement's (`Settlement.CurrentSettlement`; the party is
  inside it — `MobileParty.CurrentSettlement` is the town); the steward reads their access the same way (`"tavern"`).
- **Leaving at sea**: `town/town_leave`'s condition is `!MobileParty.MainParty.IsCurrentlyAtSea` — a party that came by sea
  has NO Leave in the town menu; its only way out is the port's **`sail_option`** (wrapped by the leave guard since step 8).

**Adding our entry**: `CampaignGameStarter` has a public constructor `(GameMenuManager, ConversationManager)` that only stores
the two; its `AddGameMenuOption(menuId, …, index)` goes through `GetPresumedGameMenu` (finds the menu, or creates an empty
placeholder that a later `AddGameMenu` initializes — so calling it for `port_menu` WITHOUT War Sails would leave an empty
menu behind: never do it blind). `GameMenu.AddOption` is internal. So the entry is added the first time `port_menu` opens
(`GameMenuOpened` comes before `OnMenuCreate`, so it shows on that very opening), after `trade`
(`IndexAfter(menuId, "trade")`), by id — no NavalDLC reference.

## 19. Carrying capacity and load, on land and at sea (verified in step 14, 2026.09.28)

For the footer's weight line (round 3). Base game `CS\...GameComponents\DefaultInventoryCapacityModel.cs`; War Sails
`NavalDLC\NavalDLC.GameComponents\NavalDLCInventoryCapacityModel.cs` (it wraps the default — `base.BaseModel`).
`MobileParty.InventoryCapacity` / `TotalWeightCarried` call the model with the party's LIVE `IsCurrentlyAtSea`.

**Capacity** — `CalculateInventoryCapacity(party, isCurrentlyAtSea, …, additionalManOnFoot, additionalSpareMounts,
additionalPackAnimals, includeFollowers)`; the default model IGNORES the three `additional…` parameters, so "the capacity
after the deal" cannot be asked of the game — it is recomputed from the pieces:
- `10` base (`Add(10)`) and a floor of 10 (`LimitMin(10)`).
- **members**: `NumberOfHealthyMembers × 2 (TroopsFactor) × 10 (GetItemAverageWeight)` = **20 kg per healthy member**;
  **Steward.ArenicosHorses** (PrimaryBonus 0.1) adds `Round(members × 0.1)` members — land only (`!isCurrentlyAtSea`);
  **Steward.ForcedLabor** adds `PrisonRoster.TotalHealthyCount` members (after Arenicos — not multiplied) when the party is
  not at sea NOW (`!mobileParty.IsCurrentlyAtSea` — the live flag, whichever capacity is asked).
- land only (`!isCurrentlyAtSea`): **mounts** `NumberOfMounts × 2 × 10` = **20 kg per mount** (every `IsMount` animal,
  ridden or not — the model's text says "Spare Mounts"); **pack animals** `NumberOfPackAnimals × 10 × 10` = **100 kg each** ×
  (1 + Scouting.BeastWhisperer SecondaryBonus 0.1 [secondary role] + Riding.DeeperSacks 0.2 + Steward.ArenicosMules 0.2);
  then **Trade.CaravanMaster** (PrimaryBonus 0.3) multiplies the whole land total.
- `ExplainedNumber`: `Add` adds to the base, `AddFactor` sums factors, `ResultNumber = base × (1 + Σ factors)` clamped.
- War Sails at sea adds **Σ `Ship.InventoryCapacity`** of the party's (and attached parties') ships (`ShipHull.InventoryCapacity
  × (1 + the upgrades' InventoryCapacityBonusMultiplier)`). A deal never changes the ships.
- `MobileParty.Ships` (base game, `Party.Ships`) is empty without War Sails — "has ships" is the soft test.

**Load** — `CalculateTotalWeightCarried` = Σ `GetItemEffectiveWeight(element, party, atSea) × amount`:
- default: any `HasHorseComponent` item weighs **0**, else `EquipmentElement.GetEquipmentElementWeight()`.
- War Sails AT SEA: a mount **50 kg** (Boatswain.NavalHorde secondary), a pack animal **30**, livestock **20**
  (Boatswain.Optimization), trade goods × Boatswain.GildedPurse; and `CalculateTotalWeightCarried` adds **50 kg per non-hero
  troop whose equipment has a horse** (`!Character.Equipment.Horse.IsEmpty`) — the cavalry's own horses are cargo at sea.

**How the steward uses it** (Module `SnapshotBuilder.ReadCarry`, Core `GameRules.SetCarryRates`, `Planning.CarryTotals`):
the NOW numbers come from the model itself (land, and with ships sea) — a modded model is honoured; each stack's unit
weight on land and at sea comes from `GetItemEffectiveWeight`; the per-unit rates (member, mount, pack animal, prisoner —
with the party's perks read by `MobileParty.HasPerk` exactly as the model reads them) move the capacity with the deal:
mounts and pack animals bought / sold (land), hires (land and sea), prisoners leaving (Forced Labor only; the wounded leave
first). A mounted mercenary's sea weight is MEASURED — (load at sea − the items' sea weight) ÷ mounted non-hero men — or,
with none mounted yet, the model's sea weight of the troop's own horse. Arenicos' rounding is left out (≤ one member).

## 20. Who the player "knows" — the tavern district and the Encyclopedia (verified in step 14, 2026.09.28)

Anton's round-3 finding: a wanderer's name clicked in the Tavern section opened an Encyclopedia page of "???"; after he
opened the town's tavern district menu, the same click showed the full page.

- **The Encyclopedia hides a hero** when `CampaignUIHelper.IsHeroInformationHidden` ("You haven't met this hero yet.") —
  `EncyclopediaHeroPageVM` sets `IsInformationHidden` from it — i.e. `!InformationRestrictionModel.DoesPlayerKnowDetailsOf(
  hero)`: `DefaultInformationRestrictionModel` answers false for a hero NOT of the player's clan, alive, not a kingdom's
  ruler, and **`!hero.IsKnownToPlayer`** (unless the fog-of-war cheat). A clanless wanderer is known only by that flag.
- **`Hero.IsKnownToPlayer`** (`CS\TaleWorlds.CampaignSystem\Hero.cs`) — public get/set; the setter, on a change, fires
  `CampaignEventDispatcher.OnPlayerLearnsAboutHero` → `HeroKnownInformationCampaignBehavior.OnPlayerLearnsAboutHero`:
  `UpdateHeroLocation` (last known closest settlement) and, for a hero outside the player's clan, the message-log line
  "You've learned about {HERO}." (`{=oSghSUxp}`). `HasMet` (`SetHasMet()`, a conversation) is a different, stronger flag —
  meeting sets known too (`OnPlayerMetHero`), knowing does not set met.
- **What the tavern district does** — no talk needed: `town_backstreet`'s init (`PlayerTownVisitCampaignBehavior.
  town_backstreet_on_init` → `UpdateMenuLocations`) puts the settlement's **"tavern"** location in
  `GameMenuManager.MenuLocations`; `HeroKnownInformationCampaignBehavior` listens to **`GameMenuOpened`** (`OnGameMenuChanged`)
  and, for every location in `MenuLocations`, `LearnAboutLocationCharacters`: each `LocationCharacter` that is a hero with
  `CurrentSettlement == Settlement.CurrentSettlement` gets `IsKnownToPlayer = true`. (The `town` menu itself lists center,
  arena and the houses — not the tavern; missions do the same on `AfterMissionStarted` for their location.) Wanderers sit in
  the tavern location: `HeroAgentSpawnCampaignBehavior` places every `HeroesWithoutParty` hero on `SettlementEntered` where
  `HeroAgentLocationModel.GetLocationForHero` says (a wanderer → "tavern", RESEARCH §7).
- **Unit pages** (`EncyclopediaUnitPageVM`, the mercenary troop's link) never consult the restriction model — only the hero
  page, hero tooltips (`HeroVM`, `HeroViewModel`, `TooltipRefresherCollection`) and the marriage popup do. Nothing to learn.
- **The steward** (`Module\Adapter\TavernKnowledge`): its Tavern section is the tavern district for the player, so it
  learns about the wanderers it LISTS the moment the window is on screen (not for an arrival popup that stays shut; the
  autonomous steward never lists the tavern), after every re-plan while open, and once more before a wanderer's name opens
  the Encyclopedia — through the same public setter, so vanilla's event, location and message follow. [decided: Claude,
  2026.09.28 — step 14] Why at show and not only at the click: vanilla reveals on standing in the district, before any
  click; the window names them, shows their skills and prices — the game's knowledge should match what is on screen.

---

## 21. Recruits and dismissals — the troops section (verified in step 16, 2026.09.28)

Anton's round-3 wish: the recruits on offer in the town (or village) at the top, the party's own troops below, recruit and
dismiss with the steward's buttons. Read in `game-decompiled-1.4.8` (`CSVM\...GameMenu.Recruitment\`, `CS\Helpers\`,
`CS\...GameComponents\DefaultVolunteerModel.cs`, `DefaultPartyWageModel.cs`, `DefaultSettlementAccessModel.cs`,
`CS\...Party\PartyScreenLogic.cs`, `CSVM\...Party\PartyVM.cs`).

**Where the volunteers live**
- On the **notables**: `public CharacterObject[] Hero.VolunteerTypes` (6 slots, `new CharacterObject[6]`; a null slot is
  empty). The settlement's notables are `Settlement.Notables`. A notable offers recruits when `Hero.CanHaveRecruits` →
  `VolunteerModel.CanHaveRecruits`: occupation Mercenary, Artisan, Merchant, Preacher, Headman, GangLeader or RuralNotable
  (enum values 2 and 17–22). **Towns and villages both** — a village's headman and rural notables have volunteers;
  castles have no notables. The slots refill daily (`RecruitmentCampaignBehavior`, `GetDailyVolunteerProductionProbability`).
- **Vanilla's recruit screen** (`RecruitmentVM.RefreshScreen`): for each notable with `CanHaveRecruits`,
  `HeroHelper.GetVolunteerTroopsOfHeroForRecruitment(notable)` (the 6 slots; empty unless `IsAlive`); a slot can be recruited
  when its troop is not null and **`HeroHelper.HeroCanRecruitFromHero(Hero.MainHero, notable, index)`** = `index <=
  VolunteerModel.MaximumIndexHeroCanRecruitFromHero(buyer, seller)` (`RecruitVolunteerVM` constructor).
- **Which slots the PLAYER may take** — `DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero` = min(6, base + relation
  + faction + (AI +1) + war + perks): base = 1 + the difficulty's `GetPlayerRecruitSlotBonus()` (+ Roguery.OneOfTheFamily for a
  gang leader in the player's fief with such a governor); relation with the notable: < 0 → −1, 0–4 → 0, ≥ 5 → 1, ≥ 10 → 2,
  ≥ 20 → 3, ≥ 40 → 4, ≥ 60 → 5, ≥ 80 → 6, ≥ 100 → 7; +1 when the notable's settlement is of the player's map faction; −1 at
  war with it (−2 for an AI; a minor-faction hero in a village: 0); perks Trade.ArtisanCommunity (merchants), Leadership.
  CombatTips (same culture), Charm.Firebrand (rural), Charm.FlexibleEthics (urban), Engineering.EngineeringGuilds (artisans).
  NB the player's check is `index <= max` while AI parties use `index < max` (and get +1): the same reach. The recruit
  screen's "needs relation N" tooltip loops `index < Max(…, i)` — a display off-by-one, not the rule.
- **The gate** — the menus' "Recruit troops" option (`town/recruit_volunteers`, `village/recruit_volunteers`,
  `PlayerTownVisitCampaignBehavior`) = `SettlementAccessModel.CanMainHeroDoSettlementAction(settlement, RecruitTroops, …)` →
  `DefaultSettlementAccessModel.CanMainHeroRecruitTroops`: a village at war with the player → "You cannot recruit troops
  from a hostile village."; a village not in `VillageStates.Normal` → no; a town → always (war lowers the slots through the
  volunteer model instead). It reads `Settlement.CurrentSettlement` — call it in the settlement. Trade access plays no part.

**The price per man** — `PartyWageModel.GetTroopRecruitmentCost(troop, Hero.MainHero).RoundedResultNumber`
(`DefaultPartyWageModel`, the recruit screen's `RecruitVolunteerTroopVM.Cost`): by level 10 (≤1) / 20 (≤6) / 50 (≤11) /
100 (≤16) / 200 (≤21) / 400 (≤26) / 600 (≤31) / 1000 (≤36) / 1500; + 150 (+500 from level 26) when the troop's equipment has
a horse; × 3 base for mercenary / gangster / caravan-guard occupations; factors: Throwing.HeadHunter (tier ≥ 2),
OneHanded.ChinkInTheArmor / TwoHanded.ShowOfStrength / Polearm.HardyFrontline (infantry), Bow.RenownedArcher /
Crossbow.Piercer (ranged), the Khuzait cultural feat (mounted), Steward.Frugal (party leader), Trade.SwordForBarter /
Charm.SlickNegotiator (mercenary kinds); at least 1. The same for every man of a type, whichever notable offers him — one
flat price per row. Wage: `CharacterObject.TroopWage`.

**What vanilla does when you recruit** (`RecruitmentVM.OnDone`): refuses when the cart's total > `Hero.MainHero.Gold`
(so total ≤ gold is fine); over the party limit it only asks "Over Limit" (Yes goes on). Then per man:
`notable.VolunteerTypes[index] = null`; `MobileParty.MainParty.MemberRoster.AddToCounts(troop, 1)`;
`CampaignEventDispatcher.Instance.OnUnitRecruited(troop, 1)`; then ONE `GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero,
null, total, disableNotification: true)` (the gold goes to nobody — no notable is paid) and a "gold removed" message.
`OnUnitRecruited` → `RecruitmentCampaignBehavior.OnUnitRecruited`: Leadership XP (`amount × tier × 2`), the Famous Commander
XP on the new men, bandit recruits' skill XP; `StatisticsCampaignBehavior` counts; the tutorial listens. The player path does
NOT fire `OnTroopRecruited` (that is the AI's `ApplyInternal`, whose listeners are the nameplate notice and the army overlay).
`OnPlayerStartRecruitment` fires when a man is put in the cart (a tutorial hook) — nothing to mirror.

**What vanilla does when you dismiss** (the normal party screen, `PartyScreenHelper.OpenPartyScreen`): `IsDismissMode = true`,
the right roster is the party's LIVE `MemberRoster`, the left one a dummy roster; moving men left = dismissing.
`PartyVM.OnTransferTroop` with `TransferHealthiesGetWoundedsFirst = false` moves **the wounded first** from the party's
side (`woundedNumber = min(wounded, amount)`); `PartyScreenLogic.TransferTroop` → `AddToCounts(troop, −n, false, −wounded)` on
the party's roster — its XP pool stays with the men who remain (so the stack's ready count is at most the men left; XP is
clamped to Number × the dearest upgrade only on the next XP change, `PartyBase.OnXpChanged`). Done → `DefaultDoneHandler`
(prisoner release/take only) — the dummy roster is simply dropped: **no gold, no event, no morale**. Only regulars that are
not `IsNotTransferableInPartyScreen` (quest-bound) may go (`TroopTransferableDelegate`); heroes follow other rules (the
steward never lists them). Healthy men carry (20 capacity each, §19), wounded do not; at sea War Sails weighs every mounted
non-hero man's horse, wounded too (`item.Number`).

**The steward** (DESIGN §2.8): `SnapshotBuilder.ReadTroops` + `VolunteerSlots` read exactly the recruit screen's offer behind
`CanRecruitNow` (the RecruitTroops gate) and the party's regulars; `PlanExecutor.Recruit` / `Dismiss` do exactly the above,
re-checked at Do it (the gate, the slot still holding the troop and still open to the player, the live price, the purse;
the men still held). [decided: Claude, 2026.09.28 — step 16] The recruit's gold is paid once per row with the notification
on (the window's summary names the gold too); the slots are taken in the recruit screen's order (notables in order, slots
0–5) — no price difference between notables.

---

## 22. Horse modifiers — lame, old, and the speed model (verified in step 17, 2026.09.28)

Anton 2026.09.28: "if a lame horse gives the bonus — keep it; never buy them as they don't look good; add a button to sell
and replace them with healthy ones". Read in `game-decompiled-1.4.8` and the installed game's data.

**Which horse modifiers exist** (`Modules\Native\ModuleData\item_modifiers.xml`, group `horse` in
`item_modifiers_groups.xml` — `no_modifier_loot_score="1"`, `no_modifier_production_score="1"`): only TWO are active in
v1.4.8, both BAD:

| Modifier id | Name | Group | price_factor | quality | horse_speed / maneuver / charge / hp | Where it comes from |
|---|---|---|---|---|---|---|
| `lame_horse` | "Lame {ITEMNAME}" | horse | **0.1** | poor | ×0.7 / ×0.9 / ×0.6 / ×0.8 | the MAIN HERO's horse badly hurt in a battle (`SandBox.Missions.MissionLogics.MountAgentLogic.OnAgentRemoved`: 20% when his mount dies, Riding.WellStraped raises it; a lame horse that "dies" again is lost); `loot_drop_score`/`production_drop_score` 0 — never looted or produced |
| `companion_horse` | "Old {ITEMNAME}" | companion | **0.2** | poor | ×0.8 / ×0.95 / ×0.8 / ×0.9 | a wanderer's own horse (`CompanionsCampaignBehavior.AdjustEquipmentModifiers` marks every wanderer's gear "Rusty"/"Worn"/"Old") — in the roster once the player strips it |

Every other horse modifier (Purebred ×4, Healthy ×3, Strong ×3, Lean ×2, Good Natured ×1.2, Badly Tempered ×0.8,
Stubborn ×0.7, Anaemic ×0.35) is COMMENTED OUT of the XML — a mod could bring them back. `CampaignData.LameHorseModifier
= "lame_horse"`. A lame horse heals: `CampaignBattleRecoveryBehavior.DailyTickParty` turns one lame horse of the roster into
a plain one per day with the Medicine.Veterinarian perk (a chance roll, not at sea).

**What "negative" means — the game's own test**: `ItemModifier.PriceMultiplier < 1f` (`BattleCampaignBehavior.
OnCollectLootItems`: the Engineering.Metallurgy perk strips exactly such modifiers from loot; `DefaultBattleRewardModel`
uses the same test). `ItemModifier.IsBeneficial()` is useless for horses (it reads only Damage/Speed/MissileSpeed/Armor/
HitPoints/StackCount — all 0 on a horse modifier); `ItemModifier.ItemQuality` (Poor/Inferior) agrees for the two active
ones but a mod's modifier may leave `quality` out. So the steward's rule is `PriceMultiplier < 1`
(`GameRules.IsBadModifier`, `ItemStack.ModifierPriceFactor` read by `SnapshotBuilder` from `el.ItemModifier.PriceMultiplier`).

**Price**: `EquipmentElement.ItemValue = round(Item.Value × ItemModifier.PriceMultiplier)` and
`DefaultTradeItemPriceFactorModel.GetPrice(EquipmentElement …)` = `ItemValue × priceFactor` — a lame horse trades at a
TENTH of a plain one, an old one at a fifth. The price walk of a town still moves the category's in-store value by
`Item.Value` (the plain item's, §8). The price book's average is per ITEM (the plain one), so the steward scales a
modified stack's min sell by its factor (`PriceBook.MinSellOf`) — else a lame horse could never be sold.

**Does a modified horse still carry a footman? Yes — by the game's rule, with an in-session quirk.** The speed model's
mounted footmen are `min(footmen, ItemRoster.NumberOfMounts)` (`DefaultPartySpeedCalculatingModel.AddCargoStats`), and
`NumberOfMounts` counts every `HorseComponent.IsMount` element in the FULL recount (`ItemRoster.CalculateCachedStats`: on
load via `Campaign.CalculateCachedStatsOnLoad` → `ItemRoster.CalculateCachedStatsOnLoad`, on `Clear()`, in the copy
constructor) — modifier or not. But the INCREMENTAL counter (`ItemRoster.OnRosterUpdated`, every `AddToCounts`) skips
any element with an `ItemModifier` (pack animals and mounts alike, §3's quirk): a lame horse added in-session does not
count until the next load, and one counted at load that leaves in-session is not subtracted (the count stays one too high
until the next load). The same counter feeds the capacity model (20 per mount) and the herd. So: a lame horse does give
the footman bonus, reliably from the next load on — Anton's "keep it if it gives the bonus" holds; the steward counts it
toward the horses to keep whenever it keeps one.

**Upgrades take any horse of the category**: `PartyScreenLogic.RemoveItemFromItemRoster(category, n)` — `where
Item.ItemCategory == category orderby Item.Value` (the PLAIN item's value) then locked last — a lame war horse is used
for an upgrade like a healthy one.

**The steward** (DESIGN §2.2–§2.4, step 17): never buys a modified animal (lame and old ones by Anton's rule; any other
modifier as since step 4); with `ReplaceLameHorses` (default on) the bad ones sit in the Lame horses row and are sold, and
their role rows buy healthy ones; off, they are kept and counted.

---

## 23. The herd — when animals slow the party (verified in step 18, 2026.09.28)

Anton's round-3 wish: `Horses 110 / 200 before the herd slows you` in the footer, "the game's real herding threshold". Read in
`game-decompiled-1.4.8`: `CS\...GameComponents\DefaultPartySpeedCalculatingModel.cs` (`CalculateLandBaseSpeed`,
`AddCargoStats`, `GetHerdingModifier`), `CS\...Roster\ItemRoster.cs`, War Sails'
`NavalDLC\NavalDLC.GameComponents\NavalDLCPartySpeedCalculationModel.cs`.

- **Men** `num = MobileParty.MemberRoster.TotalManCount` (heroes and wounded in; **prisoners never** — only their own
  "Prisoners" factor), + each attached party's `MemberRoster.TotalManCount` (an army leader's speed pools the army).
- **Mounts** `numberOfAvailableMounts = ItemRoster.NumberOfMounts` (every `HorseComponent.IsMount` animal — war, noble and
  camels too), + the attached parties'. **Footmen** `num5 = Party.NumberOfMenWithoutHorse` (+ attached; §3 for who counts).
  **Ridden** `num10 = min(footmen, mounts)` — a pack animal never carries a man.
- **Herd** `herdSize = NumberOfPackAnimals + NumberOfLivestockAnimals` (+ attached, `AddCargoStats`) `+ max(0, mounts −
  ridden)` — pack animals, livestock and the mounts nobody rides.
- **The threshold** (`GetHerdingModifier(num, herdSize)`): `herdSize −= men; if (herdSize <= 0) return 0` — the herd may EQUAL
  the men with no penalty; one more and the factor is `max(−0.8, −0.3 × (herd − men) / men)` (no men: −0.8), plus
  Riding.Shepherd's share of it back (`herdingModifier × Shepherd.PrimaryBonus`). Skipped for villager parties
  (`!IsVillager`) — never the player.
- **Land only**: War Sails' `CalculateBaseSpeed` returns `CalculateNavalBaseSpeed` while `IsCurrentlyAtSea` — ship speeds,
  crew, fleet size, overburdened; no herd at all (at sea the animals weigh instead, §19).
- **Counters**: `ItemRoster.NumberOfMounts/PackAnimals` skip modified animals in-session (`OnRosterUpdated`) but count them on
  a full recount (§3, §22); `NumberOfLivestockAnimals` counts every head.

**The line** (Core `Planning\HerdTotals`, DESIGN §1.1): with H = mounts + pack animals and R = men + ridden − livestock, H ≤ R
exactly when herd ≤ men (H − R = herd − men), so `Horses H / R before the herd slows you` is the game's own threshold said in
horses — R is the most horses the party may drive before it slows. Counted for the party AFTER the deal: members and footmen
through `PartyAfter` (hires, recruits, dismissals — a man on foot who joins adds one to both), mounts and pack animals from
the inventory ± the plan's animal tallies (lame and old ones count as mounts, as the steward counts them — the in-session
counter may disagree until a reload, §22), livestock as held (never traded); an army's attached parties added as read
(`SnapshotBuilder.ReadAttached`). Red when H > R. The log's footer line carries every number (`horses H of R before the herd
slows (herd … vs … men; …)`).

## 24. Troop tiers (verified in step 18, 2026.09.28)

For "T1 Vlandian Recruit" and the troops section's order (DESIGN §2.8). Read in `game-decompiled-1.4.8`:
- `CharacterObject.Tier => Campaign.Current.Models.CharacterStatsModel.GetTier(this)` (`CS\CharacterObject.cs`).
- `DefaultCharacterStatsModel.GetTier`: a hero → 0; a troop → `min(max(ceil((Level − 5) / 5), 0), MaxCharacterTier)`, and
  `MaxCharacterTier => 6` — so **0–6 in vanilla** (level ≤ 5 → T0, 6–10 → T1, 11–15 → T2, … 31+ → T6). A mod's stats model
  may change both; the steward shows whatever `Tier` says.
- The game's own screens draw the tier as an icon (`CampaignUIHelper.GetCharacterTierData`: `General\TroopTierIcons\
  icon_tier_{n}`, nothing for tier ≤ 0 or > 7); the steward writes `T{n}` — the fonts have the letters, and a text needs no
  sprite category. A T0 troop (looters, some bandits) reads `T0`.
- The snapshot already carried it since step 16 (`TroopStack.Tier`, read in `SnapshotBuilder.ReadTroops`); step 18 puts it on
  the row (`TroopRowInfo.Tier`) and in the log's offer list.

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
18. **Vanilla hires companions and mercenaries past the party size limit** — and since round 3 (2026.09.28) so do we: the
    limit is shown (`Party 99/96`), never a block.
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
31. **MCM's default "memory" format throws on first register** (5.12.3) — `SetFormat("none")`.
32. **MCM dropdown picks set `SelectedIndex` on the getter's object**, never call the setter.
33. **MCM float controls need `ProxyRef<float>`**; its Default preset snapshots the current values.
34. **`InventoryLogic.Initialize` installs a fake listener** — `SetInventoryListener` must come after it.
35. **A transfer the from-roster cannot cover does nothing, silently** — clip counts, check the roster moved.
36. **DoneLogic pays the NET (sales − buys) capped by merchant gold**; the planner caps GROSS sales.
37. **A village with nothing on offer allows no trade at all** — not even selling to it.
38. **Popup text is rich text**: `<` starts a tag; `>` and `\n` are fine.
39. **A Module namespace `SmartSteward.Game` hides `TaleWorlds.Core.Game`** inside `namespace SmartSteward` (CS0118) —
    the adapter lives in `SmartSteward.Adapter`.
40. **An unknown brush name gives the widget a NULL brush**; an unknown attribute is silently ignored — run check-gui.ps1.
41. **A VM getter / command that throws is rethrown into the game** (`InvokeWithLog`) — guard every one.
42. **An element's own bindings use its own DataSource** — `IsVisible` on a list element binds to the list.
43. **Disabled widgets get no hover** — a greyed button's tooltip needs the wrapper + `HintWidget IsDisabled="true"`.
44. **ReleaseMovie before RemoveLayer** — the layer's finalize asserts on a loaded movie.
45. **The UI fonts lack → − ≈ ⟲ ▸** — use – » and sprites.
46. **`ScrollablePanel.MouseScrollAxis` does not exist in 1.4.8** (TrainingBattles carries it, harmlessly).
47. **`IntegerInputTextWidget` cannot be empty** — use `EditableTextWidget` where "empty" means something.
48. **A PowerShell AssemblyResolve script block can recurse into a StackOverflow** while game code runs — preload the
    game DLLs instead when driving game code from a script.
49. **`GameMenuOpened` fires AFTER the menu's own init** — which may have switched to another menu; check the live menu.
50. **`GameMenuOptionSelected` fires even when a wrapped consequence did not leave** — veto the leaving incidents through
    `IsSettlementBusyEvent`, and re-run the real leave through `RunConsequencesOfMenuOption`.
51. **War Sails adds its menus in `OnAfterSessionLaunched`** — wrap its options lazily (at `GameMenuOpened`), never at
    session launch.
52. **An incident rolled on a menu event starts on the next campaign tick, inside the menu** — wait for
    `MapState.NextIncident == null` and `!MapScreen.IsMapIncidentActive` before opening anything.
53. **A module's `Languages\std_*.xml` is never loaded by itself** — only files a `language_data.xml` names are; English
    comes from the code's `{=id}English`, the root std file is the translators' source.
54. **A map click with Left Alt held asks for a PARLEY, not a move** — a Left Alt stuck after Alt+Tab looks like "towns
    and villages cannot be entered" ("clan tier not high enough to request a meeting" on hostile towns); tap Left Alt.
55. **`InventoryLogic` ignores inventory locks** — only the trade screen's "transfer all" honours them; a headless trade
    sells a locked stack like any other, so any lock rule is the mod's own check (§6).
56. **A town reached by sea never shows the `town` menu** — `naval_town_outside` switches straight to War Sails' `port_menu`
    (the town's own market); arrival logic keyed to `town` misses every docking (§18).
57. **`CampaignGameStarter.AddGameMenuOption` creates a menu that does not exist** (`GetPresumedGameMenu`) — add to a DLC's
    menu only when it is there (at its first opening), never blind (§18).
58. **The capacity model ignores its `additional…` parameters** — the capacity after a deal must be recomputed from the
    formula's pieces (§19); at sea War Sails weighs every animal AND every mounted troop's horse.
59. **An unknown clanless hero's Encyclopedia page is all "???"** — `Hero.IsKnownToPlayer`; vanilla sets it for the tavern's
    heroes when the tavern district menu opens (`GameMenuOpened` + `MenuLocations`), no talk needed (§20).
60. **A troop's "man with a horse" is its formation class, not its gear** — `CharacterObject.IsMounted` (what
    `NumberOfMenWithoutHorse` counts) = the XML `default_group` is cavalry / horse archer; only heroes are judged by the horse
    slot. War Sails' sea weight uses the gear instead (§3, §19).
61. **Volunteers live on the notables** (`Hero.VolunteerTypes[6]`), not the settlement — towns AND villages; the player may
    take slot `index <= MaximumIndexHeroCanRecruitFromHero` (`HeroHelper.HeroCanRecruitFromHero`; AI parties use `<`) (§21).
62. **The player's recruit path fires `OnUnitRecruited`, not `OnTroopRecruited`** — the latter is the AI's; the recruit gold
    goes to nobody (§21).
63. **Vanilla's party screen dismisses the WOUNDED first** (`TransferHealthiesGetWoundedsFirst` false) — no gold, no event;
    the stack's XP stays with the men who remain (§21).
64. **Only two horse modifiers are live in v1.4.8, both bad**: `lame_horse` (×0.1, the hero's horse hurt in battle)
    and `companion_horse` "Old" (×0.2, a wanderer's horse); the rest are commented out of `item_modifiers.xml` (§22).
65. **"Bad modifier" = `ItemModifier.PriceMultiplier < 1`** — the game's own test (Metallurgy); `IsBeneficial()` reads
    weapon stats only and says nothing about a horse (§22).
66. **A modified horse trades at `Item.Value × PriceMultiplier`** (a lame one at a tenth) but a town's price walk moves by
    the plain `Item.Value`, and the price book's average is the plain item's — scale a modified stack's min sell (§22).
67. **The herd may equal the men — only MORE slows** (`herdSize − men <= 0` → no factor); prisoners are not men to it, an
    army's attached parties pool in, and at sea (War Sails) there is no herd at all (§23).

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
