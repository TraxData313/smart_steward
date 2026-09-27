# Smart Steward — Design

The contract for the build. Source: `concept.txt` (Anton, 2026.09.27). Where the concept left a
choice open, the choice made here is marked **[decided: Claude, date]** so Anton can overturn it;
open questions for Anton are listed on the board under NOT FULLY DECIDED.

Terms: **food** = any item the game treats as food; **pack animal** = a pack-animal horse item
(sumpter horse, mule…); **mount** = a riding animal that is not a pack animal; **war mount** = a
mount of the category a troop upgrade requires (war horse, noble horse, war camel… — exact
categories per `RESEARCH.md`); **loot** = everything else in the party inventory.

---

## 1. The Party Steward window

One Gauntlet window, opened from the settlement menu (§6) or automatically on arrival. Title
"Party Steward". Two tabs: **Suggestion** and **Instructions**.

### 1.1 Suggestion tab — the table

Built for glancing, not reading: fixed columns, aligned numbers, colour for direction (buy =
green-ish, sell = red-ish, untouched = grey). One row per item (or prisoner troop) the steward
touches, plus rows for ticked item types the market has but the plan left at 0 (so the player
can add by hand). Grouped by type in this order: Food, Pack animals, Mounts, War mounts,
Prisoners, Loot, Tavern (§2.7).

**Header line, at the very top of the tab** (Anton, 2026.09.27): the **total money change** if
every queued action is confirmed — e.g. `Gold 12,400 → 10,930  (−1,470)` — green when the deal
earns, red when it costs; updates live with every click.

| Column | Content |
|---|---|
| Mine | how many the party holds now |
| Change | `[-] +7 [+] [⟲]` — the steward's proposal, editable. Negative = sell |
| Result | `27` — Mine + Change |
| Price | per-unit price and the row total: `11 ea · 77` (buy) or `+240` (sell) |
| Market | how many the settlement has in stock |
| Item | item name (prisoners: troop name) |
| Type | Food / Pack / Mount / War mount / Prisoner / Loot |

Buttons on Change:
- click **±1**, **shift+click ±5**, **ctrl+click ±all** (all = up to market stock or budget when
  buying; everything held when selling).
- **⟲ reset** returns the row to the steward's suggestion.
- Clamped: can never sell more than held, never buy more than the market has.
- Every edit re-prices the row (marginal prices, §4.1) and refreshes the footer live.
- A row the player edits stays as edited — the steward does not re-plan other rows around it
  **[decided: Claude, 2026.09.27 — predictable beats clever]**.

Footer: gold now → gold after · spent / earned · food after (units and ≈ days) · weight freed
or added · buttons **Do it** and **Not now**. When the player's edits break a money floor (§3),
the footer shows it in red, but **Do it** still works — the player's hand overrides the steward.

### 1.2 Instructions tab — the settings

Every setting from §7, grouped as in §7, editable in place (checkboxes, number steppers). Plus
two tick-lists that do not fit MCM:
- **Food to keep**: every food item in the game, ticked = the steward may buy/keep it.
- **Prisoners to ransom**: every troop type, ticked = the steward may ransom it.

Changes save to the settings file at once (and MCM shows them — same values, §8).

---

## 2. What the steward plans

Planning happens when the window opens (and re-plans when Instructions change). The planner is
pure Core logic fed a snapshot of the party and the market (§5).

### 2.1 Food — enough, varied, fairly priced

- **Target** = `ceil(eaters × FoodPerMan)`. Eaters = party members (+ prisoners if
  `FoodCountPrisoners`). `FoodPerMan` default **2.0** (Anton's number — at vanilla consumption
  of about 1 food per 20 men per day that is ~40 days; the window shows the days).
- **Days** shown in the footer use the game's own daily consumption for the party.
- **Allowed** food = ticked in *Food to keep* AND unit price ≤ `FoodMaxPricePercent` (default
  **120**) of that item's average price AND ≤ `FoodMaxUnitPrice` (default **100** denars).
- **Buy**: while held < target and budget allows — pick the allowed type the party holds the
  FEWEST of (variety first: every distinct food type lifts morale), ties → cheapest; buy one;
  repeat. `FoodStrategy = Balanced` (default) or `Cheapest` (always the cheapest type).
- **Sell surplus** (`SellFoodSurplus`, default on): only when held > target ×
  (1 + `FoodSurplusTolerancePercent`/100) (default **25**) — sell back down to the target,
  most-held type first (keeps variety). Unticked food types are never auto-sold (the player may
  be carrying them on purpose) **[decided: Claude, 2026.09.27]**.

### 2.2 Pack animals — keep X

- **Target** = `PackAnimalsTarget` (default **10**). Buy the cheapest under
  `PackAnimalMaxPrice` (default **300**) until the target is met.
- **Sell surplus** (`SellPackAnimalSurplus`, default on) above the target, most expensive first.

### 2.3 Mounts — horses for the footmen

- **Footmen** = party troops (not heroes? — per RESEARCH: count what the game's speed model
  counts) who ride no horse.
- **Target** = `ceil(footmen × MountsPer100Footmen / 100)` (default **110** → a 10% buffer).
- War mounts held (§2.4) count toward this target when `WarMountsCountAsMounts` (default on —
  the game lets footmen ride any riding animal in the inventory).
- **Buy** the cheapest mounts priced ≤ `MountMaxPrice` (default **500**).
- **Sell surplus** (`SellMountSurplus`, default on) above target, **most expensive first**, but
  never a war mount reserved for upgrades.

### 2.4 War mounts — ready for the upgrades

- **Needed** per war-mount category = number of troops that can upgrade NOW to a tier that
  requires that category, + `WarMountsExtra` (default **0**). If `WarMountsManualTarget` ≥ 0 it
  replaces the automatic count (default **-1** = automatic).
- **Buy** the cheapest of the needed category priced ≤ `WarMountMaxPrice` (default **2000**).
- **Sell surplus** (`SellWarMountSurplus`, default on) above what is needed, most expensive first.

### 2.5 Prisoners — ransom or donate

- Only in towns (villages have no ransom broker). `RansomPrisoners` default on.
- Every ticked prisoner troop type is proposed for ransom; heroes (lords) never unless
  `RansomHeroPrisoners` (default off).
- `DonatePrisonersWhenPossible` (default off): when the settlement belongs to the player's
  faction and the game allows donating there, prisoners go to the garrison prison (influence)
  instead of being ransomed (gold). Rules of "allowed" per RESEARCH (mercenary case included).

### 2.6 Loot — sell the rest, mass first

- `SellLoot` default **off** (opt-in). When on, the loot kinds allowed by `SellLootEquipment`
  (weapons, armour, shields, ammo — default on) and `SellLootTradeGoods` (non-food trade goods —
  default off) are proposed for sale.
- Never sold: items LOCKED in the inventory screen, food, animals (handled above), items above
  `SellLootMaxItemValue` per unit (default **0** = no cap).
- **Mass first** (`SellLootMassFirst`, default on): when the market cannot pay for everything
  (a village with ~1k gold), sell in order of LOWEST price per kg first — the goal is to unload
  the most weight for the money the market has (vanilla sells the most expensive first, freeing
  1 kg of armour where 200 kg of rags were worth the same). Off = most expensive first.

### 2.7 Tavern — wanderers and mercenaries (Anton, 2026.09.27)

Towns only. Shown as rows so the player can hire without walking to the tavern or talking.
The steward never proposes a hire by itself — every tavern row starts at 0; the player clicks.
- **Wanderers** (companions for hire sitting in this town's tavern): one row each — name,
  hire price, daily wage (and their best skills as a short tag, if cheap to show). Change is
  `[-] 0 [+]` — a 0/1 toggle. **Do it** hires them outright (gold paid, joins the clan and the
  party) with no dialogue. Blocked (greyed, tooltip says why) when the clan's companion limit
  or the party size limit is reached.
- **Mercenaries** (the tavern's mercenary band): one row — troop name, how many are on offer,
  price per man, daily wage per man. Change is `[-] 0 [+]` with the usual shift/ctrl steps,
  clamped to what is on offer and to the party size limit.
- Tavern hires count in the header total and the footer like any purchase, but sit OUTSIDE the
  money floors' priority chain (the player chose them by hand; a floor breach shows red, §1.1).
- `ShowTavern` (default **on**) shows the section; `ShowWanderers`, `ShowMercenaries` (default
  **on**) toggle its halves.

---

## 3. Money — the order and the floors

1. **Sell first** (surplus food, surplus animals, prisoners, loot) — the proceeds fund the buys.
2. **Buy in priority order**: Food → Pack animals → Mounts → War mounts.
3. **Floors**:
   - `MinGoldAfterDeal` (default **1000**): no purchase takes the purse below this.
   - `MinGoldForHorses` (default **5000**): no ANIMAL purchase (pack, mount, war mount) takes the
     purse below this. Food only answers to `MinGoldAfterDeal` — food outranks horses.
4. The market's own gold limits what the steward can sell there (villages especially) — the
   planner never proposes a sale the market cannot pay for.

---

## 4. Pricing

### 4.1 Marginal prices
Prices come from the game's own price function for this party at this settlement (trade perks,
supply and demand included). If the game's price moves as units change hands, the planner
walks unit by unit so the proposed total is the true total. RESEARCH decides how the game does it.

### 4.2 "Average price"
Per item, the reference for `FoodMaxPricePercent` is the item's average/base value as the game
defines it (per RESEARCH — e.g. `ItemObject.Value` or the market's category average).

---

## 5. The snapshot (Core input) and the executor (Module)

The Module reads a plain snapshot for Core: party members (with footmen/riders split, upgrade-
ready troops and their required war-mount category), prisoners (with ransom value), inventory
(item id, count, weight, type, locked flag), market (item id, stock, buy price walk, sell price
walk), market gold, player gold, daily food consumption, settlement kind (town/village), whether
donating prisoners is allowed, the tavern (wanderers with hire price and wage; the mercenary
troop, count on offer, price and wage), the party size limit and room for companions. Core returns a **StewardPlan**: rows (item or troop, change,
unit prices, total), and footer numbers. The Module's executor performs a plan with the game's
own trade/ransom/donate actions so gold, stock, prices and skill XP behave as in vanilla.

---

## 6. When the window appears

- **Settlement menu entry**: "Party Steward" in the town menu and the village menu — always
  there when the mod is enabled.
- `AutoPopupOnTownEnter` (default **on**), `AutoPopupOnVillageEnter` (default **on**): open the
  window on arrival — only when the plan is not empty (`PopupOnlyWithChanges`, default on).
- `WarnIfNotReviewed` (default **on**): leaving a town/village where the steward had a
  non-empty plan the player never opened asks: "Your steward has suggestions you haven't looked
  at. Review / Leave anyway".
- `AutoExecute` (default **off**) **[decided: Claude, 2026.09.27 — optional hands-off mode]**:
  carry the plan out on arrival without the window, then post a one-line summary message.
- `ModEnabled` (default on) master switch.

---

## 7. Settings (every number is a parameter)

| Group | Key | Default | Meaning |
|---|---|---|---|
| General | ModEnabled | true | master switch |
| General | AutoPopupOnTownEnter | true | open the window when entering a town |
| General | AutoPopupOnVillageEnter | true | open the window when entering a village |
| General | PopupOnlyWithChanges | true | auto-open only when there is something to do |
| General | WarnIfNotReviewed | true | ask before leaving with unreviewed suggestions |
| General | AutoExecute | false | do it without asking, then report |
| Money | MinGoldAfterDeal | 1000 | purse floor for all purchases |
| Money | MinGoldForHorses | 5000 | purse floor for animal purchases |
| Food | FoodEnabled | true | manage food |
| Food | FoodPerMan | 2.0 | food units kept per eater |
| Food | FoodCountPrisoners | true | prisoners count as eaters |
| Food | FoodMaxPricePercent | 120 | skip food above this % of its average price |
| Food | FoodMaxUnitPrice | 100 | never pay more per food unit |
| Food | FoodStrategy | Balanced | Balanced (variety first) or Cheapest |
| Food | SellFoodSurplus | true | sell food above target + tolerance |
| Food | FoodSurplusTolerancePercent | 25 | how far above target before selling |
| Food | FoodExcluded | [] | item ids unticked in *Food to keep* |
| Pack | PackAnimalsEnabled | true | manage pack animals |
| Pack | PackAnimalsTarget | 10 | pack animals to keep |
| Pack | PackAnimalMaxPrice | 300 | never pay more per pack animal |
| Pack | SellPackAnimalSurplus | true | sell above target, most expensive first |
| Mounts | MountsEnabled | true | manage riding mounts for footmen |
| Mounts | MountsPer100Footmen | 110 | mounts kept per 100 footmen |
| Mounts | MountMaxPrice | 500 | never pay more per mount |
| Mounts | WarMountsCountAsMounts | true | war mounts held count toward the footmen's mounts |
| Mounts | SellMountSurplus | true | sell above target, most expensive first |
| War mounts | WarMountsEnabled | true | manage war mounts for upgrades |
| War mounts | WarMountsManualTarget | -1 | -1 = count upgrade-ready troops; ≥0 = keep exactly this |
| War mounts | WarMountsExtra | 0 | buffer on top of the automatic count |
| War mounts | WarMountMaxPrice | 2000 | never pay more per war mount |
| War mounts | SellWarMountSurplus | true | sell above need, most expensive first |
| Prisoners | RansomPrisoners | true | ransom prisoners in towns |
| Prisoners | RansomHeroPrisoners | false | include lords |
| Prisoners | DonatePrisonersWhenPossible | false | donate to own garrison (influence) instead |
| Prisoners | PrisonersExcluded | [] | troop ids unticked in *Prisoners to ransom* |
| Loot | SellLoot | false | sell other items |
| Loot | SellLootEquipment | true | weapons, armour, shields, ammo |
| Loot | SellLootTradeGoods | false | non-food trade goods |
| Loot | SellLootMaxItemValue | 0 | never auto-sell items worth more per unit (0 = no cap) |
| Loot | SellLootMassFirst | true | sell lowest price-per-kg first when the market's gold is short |
| Tavern | ShowTavern | true | show the tavern section in towns |
| Tavern | ShowWanderers | true | list wanderers for hire |
| Tavern | ShowMercenaries | true | list the tavern's mercenaries |

Keys are final names for the settings file and code; UI labels can be friendlier.

---

## 8. Settings storage

- One global file: `Documents\Mount and Blade II Bannerlord\Configs\SmartSteward\settings.json`,
  created on first run. JSON with a `//` comment line above every key (meaning, default, range) —
  read with Newtonsoft (ships with the game, tolerates comments). Rewritten with comments on
  every save; unknown keys ignored, missing keys get defaults, out-of-range values clamped.
- One settings **registry** in code (key, type, default, min, max, group, label, help) drives
  all three views — the file, MCM (fluent builder, soft dependency) and the Instructions tab —
  so they can never drift apart.
- A log at `Configs\SmartSteward\smart_steward.log` (what was planned, what was executed) for
  bug reports.

---

## 9. Non-goals for V1

- No per-save settings; no Harmony unless RESEARCH proves a hook is impossible without it.
- No trading for profit (buy low / sell high between towns) — that is a different mod.
- Castles have no market: nothing happens there.
- All player-facing text goes through TextObject string ids (English only at release; other
  languages can be added by translators later).
