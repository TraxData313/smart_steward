# Smart Steward — Design

The contract for the build. Source: `concept.txt` (Anton, 2026.09.27). Where the concept left a
choice open, the choice made here is marked **[decided: Claude, date]** so Anton can overturn it;
open questions for Anton are listed on the board under NOT FULLY DECIDED.

## Release scope **[Anton 2026.09.27]**

**V1 (the first Steam release)** = the Party Steward window with its three tabs, the triggers and
settings, and exactly five jobs:
1. **Tavern** — wanderers and mercenaries (§2.7)
2. **Food** (§2.1)
3. **Horses** — pack animals, riding mounts, upgrade and war horses (§2.2–2.4)
4. **Armour & weapons selling** — in bulk groups (§2.6)
5. **Prisoners** — ransom, or send to a friendly jail (§2.5) (Anton re-added them the same day)

**LATER** (designed here so a later update starts from a spec, but NOT built for V1 — marked
*LATER* where it appears): the price book's **Others** (trade goods, §1.3.1). Build steps must not implement LATER parts; keep the code open for them
(e.g. a section list the window renders, not hard-wired sections).

Terms: **food** = any item the game treats as food; **pack animal** = a pack-animal horse item
(sumpter horse, mule…); **mount** = a riding animal that is not a pack animal; **war mount** = a
mount of the category a troop upgrade requires (war horse, noble horse, war camel… — exact
categories per `RESEARCH.md`); **loot** = everything else in the party inventory.

**[research 2026.09.27]** The exact game definitions (RESEARCH §1):
- **food** = `ItemObject.IsFood` — vanilla has 9: grain, meat, fish, cheese, butter, grape, date
  fruit, olives, beer (wine and oil are NOT food). Livestock (cow, sheep, hog, chicken, goose) is
  not food to the steward, although the game counts its meat in the party's food stock.
- **pack animal** = `HorseComponent.IsPackAnimal` = item category `sumpter_horse`: sumpter horse,
  mule, saddle horse, old horse, pack camel. They are rideable but never carry footmen.
- **mount** = `HorseComponent.IsMount` (rideable, not a pack animal) = categories `horse`,
  `war_horse`, `noble_horse` (camels included — there is no camel category).
- **war mount** = a mount whose category an upgrade target requires. Vanilla requires only
  **`horse`** (21 troops) and **`war_horse`** (23 troops); `noble_horse` is never required. So a
  plain `horse` can be both a footman's mount and an upgrade horse (see §2.4).

---

## 1. The Party Steward window

One Gauntlet window, opened from the settlement menu (§6) or automatically on arrival. Title
"Party Steward". Three tabs: **Suggestion**, **Prices** (§1.3) and **Instructions**.

### 1.1 Suggestion tab — the table

Built for glancing, not reading: fixed columns, aligned numbers, colour for direction (buy =
green-ish, sell = red-ish, untouched = grey). One row per item (or prisoner troop) the steward
touches, plus rows for ticked item types the market has but the plan left at 0 (so the player
can add by hand).

**Section order** **[Anton 2026.09.27]** — each section under its own header row:
1. **Tavern** — wanderers, then mercenaries (§2.7)
2. **Food** — one row per food item (variety matters, so food stays itemised)
3. **Mounts** — ROLE rows, not one row per horse type (§1.1.1)
4. **Armour & weapons** — the loot group rows (§2.6)
5. **Prisoners** (§2.5) — last, with the other selling **[decided: Claude, 2026.09.27]**
6. *(LATER — not V1)* **Others** — the trade goods the player ticked in the Prices tab (§1.3.1)

#### 1.1.1 Mount rows are grouped by role **[Anton 2026.09.27 — "don't show each type of mount"]**

One row per role, so a single `[+]` or `[-]` does the right thing without the player choosing
horse types:

| Role row | Holds | `[+]` buys | `[-]` sells |
|---|---|---|---|
| Pack animals | pack animals | the CHEAPEST eligible pack animal on the market | the MOST EXPENSIVE surplus one |
| Riding mounts | mounts not reserved for upgrades | the cheapest eligible mount | the most expensive unreserved one |
| Upgrade horses (`horse`) | `horse`-category mounts reserved for upgrades | the cheapest eligible `horse` | the most expensive surplus one |
| War horses (`war_horse`) | `war_horse` mounts reserved for upgrades | the cheapest eligible `war_horse` | the most expensive surplus one |

"Eligible" = buy-ticked in the Prices tab, priced within its own max (§1.3) AND within the role cap
(`PackAnimalMaxPrice`, `MountMaxPrice`, `WarMountMaxPrice`). Each step re-walks the marginal
prices (§4.1), so the next `[+]` picks the next cheapest. The row shows the total and the unit
range (`3 × 180–240 = 630`); Market = eligible units on offer. A small `▸` expands the row into its
per-type breakdown for the curious — collapsed by default. The shift/ctrl steps work as everywhere.

**Loot is shown in GROUPS, not item by item** **[Anton 2026.09.27 — "group them to not spam
me"]**: one row per loot group (Armour, Melee weapons, Ranged, Shields — §2.6). A group
row: **Mine** = sellable pieces in the group, with the locked ones shown apart (e.g. `41 (+3
locked)`); **Change** = `−N` with `[-] [+]` (click ±1, shift ±5, ctrl all); **Price** = what those
N pieces fetch; the **weight they free** is shown on the row; Market = `—`; Item = the group name.

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
| Type | Food / Pack / Mount / War mount / Prisoner / Loot / Tavern |

Buttons on Change:
- click **±1**, **shift+click ±5**, **ctrl+click ±all** (all = up to market stock or budget when
  buying; everything held when selling). **[research 2026.09.27]** These are the game's own
  rebindable hot keys `FiveStackModifier` (Shift) and `EntireStackModifier` (Ctrl) — the same the
  inventory and party screens use (RESEARCH §11).
- **⟲ reset** returns the row to the steward's suggestion.
- Clamped: can never sell more than held, never buy more than the market has.
- Every edit re-prices the row (marginal prices, §4.1) and refreshes the footer live.
- A row the player edits stays as edited — the steward does not re-plan other rows around it
  **[decided: Claude, 2026.09.27 — predictable beats clever]**.
- **How an edit behaves** **[decided: Claude, 2026.09.27 — step 4b]** (Core: `StewardPlan.Increase` /
  `Decrease` / `Reset` / `ResetAll`, the rows' live `IncreaseBlock` / `DecreaseBlock`):
  - Every click walks the WHOLE plan again in the planner's own order and picking rules (§3, §4.1), each
    row at its quantity — a town row can re-price another row of the same item category; no other row's
    quantity moves.
  - Toward zero (buy less, sell less) always works. Away from zero goes as far as it can **without taking
    what another row already has** — the market's stock, its gold, the price room under a row's max in a
    shared category. The button greys with the reason: nothing eligible, all on offer / all sold, price limit,
    below min sell, market out of gold, not enough gold, needed by another row, companion limit, party full,
    dungeon full, sell-only / hire-only.
  - **Shift and Ctrl stop at zero**: one click never flips a row from selling to buying; a plain click crosses
    zero where the row does both (food, the mount role rows). Loot and prisoners only sell; tavern rows only hire.
  - The floors never block (red flags). An **empty purse does**: a buy or hire the purse cannot pay is refused
    (a wanderer needs MORE gold than his price, like vanilla). Taking income back (a ransom, a sale) may still
    leave the deal unaffordable — the footer shows it (`CannotAfford`) and **Do it** is disabled then.
  - Rarely, lowering one row makes another impossible (a sale taken back raises a category's price past the
    max of a row buying in it): that row is cut to what the market allows.
  - ⟲ returns a row to the suggestion as far as what other rows took since allows; reset-all restores the plan
    exactly.
  - Prisoners moved by hand join the same split as the steward's: the dungeon's room filled most valuable
    first, the rest ransomed.
- **The window as built** **[decided: Claude, 2026.09.27 — step 7]**:
  - **Re-planning keeps the player's hand.** A settings change (Prices or Instructions tab, MCM, the file) re-plans
    on the same snapshot when the Suggestion tab shows again; every row the player had edited is set back to its
    edited quantity by row id, as far as the new plan allows (new limits clamp it; a row the new plan no longer has
    is dropped); the other rows take the new suggestion. **Do it** looks at the world again and plans afresh with no
    carry-over — the edits were carried out. (Core `PlanCarryOver`, `StewardPlan.SetChange`.)
  - The Price cell reads `units × unit price = signed total` (`3 × 180–240 = –630`, `5 × 48 = +240`); a tavern row at
    0 shows the price to hire. The game's UI fonts have no → − ≈ ⟲ ▸ (RESEARCH §14): the header reads
    `Gold 12,400 » 10,930 (–1,470)`, the minus is an en dash, ⟲ and ▸ are the game's refresh and collapser icons.
  - A **Reset all** button sits bottom left of the Suggestion tab. The small grey words after a name carry the
    role's target, the upgrade need, the weight a loot sale frees, a wanderer's skills and wage.
  - Typed numbers (price bases, multipliers, settings) are saved on every key that leaves a valid number; anything
    else turns the box red and saves nothing. An empty price box = the placeholder again.

Footer: gold now → gold after · spent / earned · food after (units and ≈ days) · weight freed
or added · buttons **Do it** and **Not now**. When the player's edits break a money floor (§3),
the footer shows it in red, but **Do it** still works — the player's hand overrides the steward.

### 1.2 Instructions tab — the settings

Every setting from §7, grouped as in §7, editable in place (checkboxes, number steppers). Plus
one tick-list that does not fit MCM:
- **Prisoners to ransom**: every troop type, ticked = the steward may ransom it.
  **[decided: Claude, 2026.09.27 — step 7]** "Every troop type" is several hundred in vanilla, so the list shows the
  prisoners held now plus every troop unticked before (so it can be ticked back); the settings file still takes any id.

(The old *Food to keep* list moved into the Prices tab as the food rows' Buy ticks.)

Changes save to the settings file at once (and MCM shows them — same values, §8).

### 1.3 Prices tab — the price book **[Anton 2026.09.27]**

Anton: *"as I get richer I will stop caring for the price — I don't want to raise the max price
for each item, give me a multiplier."* So every item has its own base prices, and two global
multipliers scale them all.

Groups, each collapsible: **Food**, **Horses** (sub-headers Pack animals · Mounts · War mounts).
V1 has only these two **[Anton 2026.09.27]** — every other trade good (wood, jewelry, metal,
livestock, …) is left alone by the steward in V1; the *Others* group is designed below (§1.3.1)
for a later update. **Armour and weapons are
NEVER in the price book** **[Anton 2026.09.27]**: they are only ever SOLD, in bulk, as the loot
groups of §2.6 — never bought, no per-item prices, no averages computed for them at all.

One row per item:

| Buy | Max buy price | Sell | Min sell price | Item |
|---|---|---|---|---|
| ☑ | `[ 11 ] × 1.2 → 13` | ☑ | `[ 7 ] × 0.8 → 6` | Grain |

- **Buy tick** — the steward may buy this item. **Sell tick** — the steward may sell it.
- **Base prices** are editable. When empty they show a grey **placeholder**: the item's average
  buy price (Max buy) or average sell price (Min sell) — §4.2 — IF auto-fill is on for its group.
- **Final** = base × `BuyPriceMultiplier` (default **1.2** = average + 20%) or base ×
  `SellPriceMultiplier` (default **0.8** = average − 20%). Getting richer = raise one multiplier.
- The steward buys an item only at a marginal price ≤ its final max buy; it sells only at a
  marginal price ≥ its final min sell. An EMPTY base (no typed value, auto-fill off) means: buy —
  not bought unless a role cap covers it (animals, §2.2–2.4), sell — any price.
- A `⟲` per row clears the typed value back to the placeholder.
- **Auto-fill** — showing the average price of EVERY item would be a trader's cheat sheet, so
  auto-fill is per group: `AutoFillFoodPrices` (on), `AutoFillPackAndMountPrices` (on),
  `AutoFillWarMountPrices` (**off**) — and later `AutoFillOtherPrices` (**off**). With auto-fill off, the
  base stays empty until the player types one.
- **Defaults of the ticks**: food — Buy ☑ Sell ☑; pack animals and mounts — Buy ☑ Sell ☑;
  war mounts — Buy ☑ Sell ☑.
- **Which horses sit under "War mounts"** **[decided: Claude, 2026.09.27 — step 4]**: the Mounts
  sub-header holds the `horse` category (plain riding horses and camels); `war_horse`, `noble_horse`
  and any category a mod adds sit under War mounts — no auto-filled price by default. A final price
  is rounded to the nearest denar, halves up (`[11] × 1.2 → 13`).
- Only the player's changes are stored (§8); placeholders are live averages, recomputed each visit.
- **Which items the tab lists** **[decided: Claude, 2026.09.27 — step 7]**: every food, pack animal and riding animal
  of the game that is merchandise (livestock, quest and non-transferable items left out, as the steward classifies
  them), plus anything the party or this market holds; sorted by name within Food / Pack animals / Mounts / War
  mounts. Ticking an item back on or clearing a base removes the stored override.

#### 1.3.1 LATER (not V1) — the Others group **[Anton 2026.09.27: "leave wood, jewelry etc for later"]**

Kept here so the later update starts from a spec:
- A third price-book group, **Others**: every trade good that is not food, not an animal, not
  armour or weapons. Ticks default Buy ☐ Sell ☐ (opt-in: ticking either makes the item appear in
  the Suggestion tab's *Others* section). Placeholder only with `AutoFillOtherPrices` (default off).
- **Others can be traded** **[Anton 2026.09.27 — "if someone wants to be a trader"]**: an Others
  row has one extra number, **Hold up to** (default 0). Buy-ticked → the steward buys it while
  the marginal price ≤ its final max buy, up to *Hold up to* units held. Sell-ticked → it sells
  every unit while the marginal price ≥ its final min sell. Never both for one item in one visit:
  if the sell test passes the steward sells, otherwise it may buy. Others'
  purchases come LAST in the money chain (§3).

---

## 2. What the steward plans

Planning happens when the window opens (and re-plans when Instructions change). The planner is
pure Core logic fed a snapshot of the party and the market (§5).

### 2.1 Food — enough, varied, fairly priced

- **Target** = `ceil(eaters × FoodPerMan)`. Eaters = party members (+ prisoners if
  `FoodCountPrisoners`). `FoodPerMan` default **2.0** (Anton's number — at vanilla consumption
  of about 1 food per 20 men per day that is ~40 days; the window shows the days).
  **[research 2026.09.27]** The game feeds a prisoner **half** a man's ration
  (`(members + prisoners/2) / 20` per day), so a prisoner counts as **half an eater**; members
  include heroes and the wounded.
- **Days** shown in the footer use the game's own daily consumption for the party.
  **[research 2026.09.27]** = `MobileParty.FoodChange` (perks included); "days after" recomputes
  it for the members/prisoners the deal leaves (ransom and hires change it). The game's food
  stock also counts livestock meat — the days figure includes it (see NOT FULLY DECIDED).
- **[research 2026.09.27]** The game eats one **random food type** per unit consumed, so every
  type drains at the same pace whatever its stack size; variety morale runs −2 (0–1 types) … 0
  (3 types) … +7 (9 types, the vanilla maximum).
- **Allowed** food = Buy-ticked in the Prices tab AND marginal price ≤ its final max buy price
  (§1.3). **[Anton 2026.09.27]** replaces the old `FoodMaxPricePercent` (120) and
  `FoodMaxUnitPrice` (100) — the 120% lives on as `BuyPriceMultiplier` = 1.2.
- **Buy**: while held < target and budget allows — pick the allowed type the party holds the
  FEWEST of (variety first: every distinct food type lifts morale), ties → cheapest; buy one;
  repeat. `FoodStrategy = Balanced` (default) or `Cheapest` (always the cheapest type).
- **Sell surplus** (`SellFoodSurplus`, default on): only when held > target ×
  (1 + `FoodSurplusTolerancePercent`/100) (default **25**) — sell back down to the target,
  most-held type first (keeps variety), only Sell-ticked types, only at ≥ their final min sell
  price (§1.3).
- **[decided: Claude, 2026.09.27 — step 4]** The target counts only the prisoners who stay — those
  this visit ransoms or donates are not fed. Two types held equally when selling → the dearer goes first.

### 2.2 Pack animals — keep X

- **Target** = `PackAnimalsTarget` (default **10**). Buy the cheapest ELIGIBLE (§1.1.1: Buy-ticked,
  within its price-book max AND under `PackAnimalMaxPrice`, default **300**) until the target is met.
  Surplus is sold only at ≥ each animal's final min sell price, only Sell-ticked ones.
- **Sell surplus** (`SellPackAnimalSurplus`, default on) above the target, most expensive first.
- **[research 2026.09.27]** Each pack animal adds ~100 carrying capacity (perks raise it); animals
  weigh nothing themselves. Selling any mount or pack animal carries a +0.8 trade penalty (it
  fetches about half its buy price) — the planner never sells and buys the same kind in one visit.

### 2.3 Mounts — horses for the footmen

- **Footmen** = party troops (not heroes? — per RESEARCH: count what the game's speed model
  counts) who ride no horse.
  **[research 2026.09.27]** Footmen = the speed model's `PartyBase.NumberOfMenWithoutHorse`: every
  party member, **heroes and wounded included**, whose battle equipment has no horse (a hero is
  mounted when his battle-equipment horse slot is filled). Footmen ride any **mount** in the
  inventory (war and noble horses and camels too) but **never a pack animal**. Mounts beyond the
  footmen join the herd, which slows the party only once animals outnumber the men.
- **Target** = `ceil(footmen × MountsPer100Footmen / 100)` (default **110** → a 10% buffer).
- War mounts held (§2.4) count toward this target when `WarMountsCountAsMounts` (default on —
  the game lets footmen ride any riding animal in the inventory).
- **Buy** the cheapest ELIGIBLE mounts (§1.1.1 — price-book max AND ≤ `MountMaxPrice`, default
  **500**). Surplus sells under the same price-book rules as pack animals.
- **Sell surplus** (`SellMountSurplus`, default on) above target, **most expensive first**, but
  never a war mount reserved for upgrades.

### 2.4 War mounts — ready for the upgrades

- **Needed** per war-mount category = number of troops that can upgrade NOW to a tier that
  requires that category, + `WarMountsExtra` (default **0**). If `WarMountsManualTarget` ≥ 0 it
  replaces the automatic count (default **-1** = automatic).
- **[research 2026.09.27]** The requirement sits on the troop you upgrade INTO
  (`UpgradeRequiresItemFromCategory` of the target); vanilla uses `horse` and `war_horse` only.
  "Can upgrade now" = the party screen's own count per stack and target:
  `min(floor(stackXp / xpCost), stack size)` when the target's level ≥ the troop's. Both targets
  of a stack share one XP pool — count a stack once. 12 vanilla stacks (recruits) choose between a
  foot target and a horse-needing one (see NOT FULLY DECIDED). An upgrade consumes the **cheapest**
  animal of the category first and locked ones only last — so the reserved war mounts are the
  cheapest of their category.
- **Buy** the cheapest ELIGIBLE of the needed category (price-book max if set — war mounts have
  no auto-filled placeholder by default — AND ≤ `WarMountMaxPrice`, default **2000**).
- **Sell surplus** (`SellWarMountSurplus`, default on) above what is needed, most expensive first.
- **How a held mount gets its role** **[decided: Claude, 2026.09.27 — step 4]**:
  - Need per category counts a stack once, at its best horse-needing target (foot-or-horse recruits
    count — the board's proposal). `WarMountsExtra` goes on every category the party's troops upgrade
    into, even with nobody ready; `WarMountsManualTarget` applies to each such category.
  - Reserved = the held horses the upgrades would take (unlocked first, cheapest base value first, as
    vanilla consumes them); every other mount — war and noble horses too — is a riding mount.
  - With `WarMountsCountAsMounts`, the reserved horses AND the upgrade horses about to be bought count
    toward the footmen's target: the upgraded man takes his horse, so counting both would buy one horse
    too many per upgrade. When the purse cannot pay for all the upgrade horses, riding mounts fill the
    gap first (Mounts outrank War mounts).
  - Riding surplus is not sold in a visit that buys an upgrade horse (never sell and buy mounts in one
    visit — after the upgrade it is surplus for real). With war mounts managed and
    `SellWarMountSurplus` off, no horse of an upgrade category is ever sold.

### 2.5 Prisoners — ransom or donate

- Only in towns (villages have no ransom broker). `RansomPrisoners` default on.
- Every ticked prisoner troop type is proposed for ransom; heroes (lords) never unless
  `RansomHeroPrisoners` (default off).
- `DonatePrisonersWhenPossible` (default off): when the settlement belongs to the player's
  faction and the game allows donating there, prisoners go to the garrison prison (influence)
  instead of being ransomed (gold). Rules of "allowed" per RESEARCH (mercenary case included).
- **[research 2026.09.27]** (RESEARCH §5)
  - **Ransom** = the tavern district's ransom broker (`town_backstreet`); needs access to the
    tavern. Vanilla's "Ransom your prisoners" skips prisoners **locked** in the party screen. A
    ransomed lord is set free and paid at his (large) hero value.
  - **Donate allowed** = the town's faction is the player's faction **but its owner clan is not
    the player's clan** (own fiefs have no donation and give no influence), the player may enter
    the dungeon, and there is room (`PrisonerSizeLimit − NumberOfPrisoners`; donate up to it, ransom
    the rest). **A mercenary qualifies** (his faction is the kingdom he serves).
  - Influence per donated prisoner = `0.2 × ransomValue^0.4` (≈1 for a recruit), ×1.2 under
    Military Coronae. Castles also take donations but are a non-goal (§9).
- **[decided: Claude, 2026.09.27 — step 4]** Donations fill the room most valuable first (influence
  grows with the ransom value; the room is what runs out), the rest is ransomed. Heroes are not donated
  either unless `RansomHeroPrisoners`. Excluded troops and heroes keep a row at 0 (the player may add them
  by hand); locked prisoners get no row.

### 2.6 Loot — sold in groups, cheapest first

- `SellLoot` default **off** (opt-in). When on, the groups allowed by `SellLootEquipment`
  (weapons, armour, shields, ammo — default on) are proposed for sale.
- **[Anton 2026.09.27]** Non-food trade goods are no longer a loot group. In V1 the steward
  leaves them alone; LATER they become the *Others*
  of the price book (§1.3) — sold item by item, only the ones Sell-ticked there, each at ≥ its
  final min sell price. `SellLootTradeGoods` is gone. Within Others, when the market's gold runs
  short, `SellLootOrder` decides which go first.
- **Groups** **[Anton 2026.09.27]** — loot is planned and shown per group, one row each (§1.1).
  The mapping from the game's item type (`ItemObject.ItemType`, verified in v1.4.8 —
  **[research 2026.09.27]**):

  | Group | Item types | Needs |
  |---|---|---|
  | Armour | HeadArmor, BodyArmor, LegArmor, HandArmor, ChestArmor, Cape, HorseHarness | `SellLootEquipment` |
  | Melee weapons | OneHandedWeapon, TwoHandedWeapon, Polearm | `SellLootEquipment` |
  | Ranged (Anton's "firing") | Bow, Crossbow, Sling, Thrown, Arrows, Bolts, SlingStones (+ Pistol, Musket, Bullets — unused in vanilla) | `SellLootEquipment` |
  | Shields | Shield | `SellLootEquipment` |

- Never sold: items LOCKED in the inventory screen, food, animals (handled above), items above
  `SellLootMaxItemValue` per unit (default **0** = no cap).
  **[Anton 2026.09.27]** A LOCKED piece is never sold and **not even counted as sellable** (the row
  shows it apart as "+N locked"). **[research 2026.09.27]** Locks are read from
  `IViewDataTracker.GetInventoryLocks()`; a lock id is the item id + the modifier id, so a locked
  "rusty sword" does not lock a plain one (RESEARCH §6). Also never sold: livestock, banners,
  books, quest items, non-transferable items.
- **Order within a group** **[Anton 2026.09.27]** — `SellLootOrder` replaces the old boolean
  `SellLootMassFirst`: **`Cheapest`** (default — the cheapest pieces go first; it gets rid of the
  most weight for the market's money), `LowestPricePerKg`, `MostExpensive` (vanilla's habit). The
  order matters when the market cannot pay for everything (a village with ~1k gold) or the player
  sells only part of a group with `[-]`.
  *(Original rule, kept for the record: "Mass first — when the market cannot pay for everything,
  sell in order of LOWEST price per kg first … vanilla sells the most expensive first, freeing 1 kg
  of armour where 200 kg of rags were worth the same.")*
- **[decided: Claude, 2026.09.27 — step 4]** The order is fixed once per visit at the untouched
  market's sell price and runs ACROSS the groups, so a poor market's gold goes to the pieces the order
  prefers whatever their group. A group stops at its first piece the market can no longer pay for, so
  `−N` on a group row always means "the first N in order". Groups with nothing sellable get no row;
  `SellLoot` off hides the section.
- **[research 2026.09.27]** Selling equipment carries a heavy trade penalty (+1.5, more for high
  tiers): a piece fetches about a third of its value in a town (39% at tier 1) and **about a
  quarter in a village** (+1.0 more when selling there). Trade goods have only the base 0.06.

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
- **Names are clickable** **[Anton 2026.09.27]**: a wanderer's name opens that hero's Encyclopedia
  page; the mercenary troop's name opens that unit's Encyclopedia page. Closing the Encyclopedia
  returns to the Party Steward window as it was. **[research 2026.09.27]** Opened with
  `EncyclopediaManager.GoToLink(hero.EncyclopediaLink / troop.EncyclopediaLink)`; the Encyclopedia
  draws at layer order 310 and takes focus only from lower layers, so the window must sit below
  it (order ~305) and re-take focus when the Encyclopedia closes (RESEARCH §7).
- **[research 2026.09.27]** (RESEARCH §7)
  - Wanderers for hire = the town's party-less heroes with Occupation Wanderer that are not
    already the player's companions. Price = the game's `GetCompanionHiringPrice` (gear value + 10 ×
    level); daily wage = `2 + 2 × level`. Vanilla requires gold **greater than** the price and a
    free companion slot (`Clan.CompanionLimit`). Hire = pay, `AddCompanionAction`,
    `AddHeroToPartyAction` (the sibling ImmersiveAI hires the same way).
  - Mercenaries = the town's `TownMercenaryData` (one troop type + count). Price per man = the
    game's recruitment cost; wage = the troop's tier wage ×1.5 for mercenaries. Hired like the
    tavern dialogue does it (count, roster, gold, the recruit event that gives Leadership XP).
  - Vanilla itself never checks the party size limit for either — the block above is ours.

---

## 3. Money — the order and the floors

1. **Sell first** (surplus food, surplus animals, loot groups, prisoners; LATER ticked Others) — the
   proceeds fund the buys.
2. **Buy in priority order**: Food → Pack animals → Mounts → War mounts (LATER → Others, the
   price-book trading of §1.3.1, answering to `MinGoldAfterDeal`).
3. **Floors**:
   - `MinGoldAfterDeal` (default **1000**): no purchase takes the purse below this.
   - `MinGoldForHorses` (default **5000**): no ANIMAL purchase (pack, mount, war mount) takes the
     purse below this. Food only answers to `MinGoldAfterDeal` — food outranks horses.
   - While the Full-autonomous steward acts alone, both rise to `AutonomousMinGold` (default **100000**, §6).
4. The market's own gold limits what the steward can sell there (villages especially) — the
   planner never proposes a sale the market cannot pay for.
   **[research 2026.09.27]** Vanilla would let the sale go through and simply pay no more than
   the market's gold — overselling loses the goods for nothing, so this rule matters.
   **[decided: Claude, 2026.09.27 — step 4]** The limit applies to the item sales (food, animals,
   loot); ransom gold comes from the game, not the market. The executor ransoms before it trades, so
   the ransom funds the buys.
5. **[research 2026.09.27]** Villages trade both ways (the "Buy products" screen also buys from
   you); their stock is their produce (horse ranches sell horses, mules and sumpters). Trading
   anywhere needs the game's trade access (not at war, crime, etc.) — no access, no market rows.

---

## 4. Pricing

### 4.1 Marginal prices
Prices come from the game's own price function for this party at this settlement (trade perks,
supply and demand included). If the game's price moves as units change hands, the planner
walks unit by unit so the proposed total is the true total. RESEARCH decides how the game does it.

**[research 2026.09.27]** (RESEARCH §8) In a **town** the price moves after every unit, and it
moves per **item category**: each unit bought lowers the category's "in store" value and raises the
next price for every item of that category (buying one charger makes every war horse dearer);
selling does the reverse. In a **village** the price is flat for the whole visit (it comes from the
trade-bound town's figures, which a village trade never moves). Village prices carry +10% when
buying and roughly halve when selling. The planner walks with the game's own price model
(`TradeItemPriceFactorModel.GetPrice` with a hypothetical in-store value) through a price oracle.

### 4.2 "Average price"
Per item, the price book's placeholders (§1.3) are **[Anton 2026.09.27, refined by Claude]**:
- **Average buy price** = what the party would pay for one unit in an *average* town — the game's
  price model at the category's average price factor, buying side (so the ≥6% trade penalty is
  in it: `× 1.2` then really means "20% dearer than a typical town").
- **Average sell price** = the same, selling side. This matters: the game pays about **half** for
  an animal and about **a third** for equipment, so a min sell price measured against the BUY
  average would block every sale.

**[research 2026.09.27]** Average price = `item.Value × the average price factor of its category
over all other towns` — the game's own average, the one the inventory uses for its good/bad-deal
colours (`InventoryLogic.InitializeCategoryAverages`). Every purchase carries at least a 6% trade
penalty on top, so an exactly average item costs ~106% of it — which is why the placeholders
above run that factor through the price model rather than using the bare average.

**[decided: Claude, 2026.09.27 — step 6]** The trade penalty is taken with NO merchant — an average town at peace: no
village, war or scouting-network term, the player's own trade perks included — and the mean runs over the towns
actually summed (vanilla divides by the town count − 1 even when a castle-bound village excluded none).

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

**[research 2026.09.27]** (RESEARCH §9) The executor: **trade** through a headless
`InventoryLogic` (the trade screen's own logic, without the screen) — sells then buys, one
`DoneLogic` — which keeps the live price walk, both purses, the Trickle Down perk and the
`OnPlayerInventoryExchange` event that pays Trade XP and feeds quests; **ransom** through
`SellPrisonersAction`; **donate** by moving the prisoners and firing `OnPrisonerDonatedToSettlement`
(influence); **wanderers** and **mercenaries** as §2.7. Gold, limits and stock are checked again at
the click.

**[decided: Claude, 2026.09.27 — step 4b]** Core hands the executor `StewardPlan.Transactions`, in the order
to run them: donations and ransoms (the ransom funds the buys), every sale, every purchase (the walk's own
order, grouped by item category — which never changes a price, since a town's price walks per category), then
the wanderers, then the mercenaries; each with the stack (item + modifier) / troop / hero, the count and the
expected unit prices.

**[decided: Claude, 2026.09.27 — step 6]** How the executor behaves (`Module\Adapter\PlanExecutor`, Core `Execution\`):
- Every unit is checked again at the click, the way the plan walked it: its live price (the trade logic's own) must
  pass its row's limit (max buy / min sell), a sale must fit the market's remaining gold (gross sales, the planner's
  rule), a purchase the purse. What no longer fits — sold out, no longer held, locked since, no access any more, no
  room in the dungeon or the party, no companion slot — is skipped and logged; the rest goes through. The money
  floors are not checked again: the player saw them and clicked. **[decided: Claude, 2026.09.27 — step 9]** Except
  for the Full-autonomous steward, whom nobody watched: each of its purchases must still leave the purse at its floor
  (max(MinGoldAfterDeal, AutonomousMinGold) for food, the animal floor for the horses) at the LIVE price, so a price
  that moved since the plan can never take the chest below AutonomousMinGold.
- If the game's own trade logic fails after the goods moved (DoneLogic throws — nothing safe to undo), the moved trades
  are reported as cut short, never as done: the player's line and the autonomous report point to the log.
- All item trades run in ONE headless trade; if any of them fails with an error the whole trade is reset (vanilla's
  Cancel) — the party never keeps goods it did not pay for. Each prisoner row and each hire is its own transaction.
- Ransom: one vanilla call per prisoner row. Donation: vanilla's donate screen without the screen, one influence
  event for all. Moved prisoners take the wounded first.
- A wanderer is hired at his LIVE price (the trades may have moved the town's prices, and his gear's value with
  them); the log shows the difference. Mercenaries: what is still on offer, capped by the party's room and the purse.
- Every run is logged — gold before, each transaction with its real unit prices and its drift from the plan, gold
  after — and the player gets a one-line summary.

---

## 6. When the window appears

- **Settlement menu entry**: "Party Steward" in the town menu and the village menu — always
  there when the mod is enabled.
- `AutoPopupOnTownEnter` (default **on**), `AutoPopupOnVillageEnter` (default **on**): open the
  window on arrival — only when the plan is not empty (`PopupOnlyWithChanges`, default on).
- `WarnIfNotReviewed` (default **on**): leaving a town/village where the steward had a
  non-empty plan the player never opened asks: "Your steward has suggestions you haven't looked
  at. Review / Leave anyway".
- ~~`AutoExecute`~~ (the old optional hands-off mode, [decided: Claude, 2026.09.27]) — **SUPERSEDED by the
  Full-autonomous steward below; renamed in step 8.** A settings file that still has `AutoExecute` carries its value
  over to `AutonomousSteward` once (the rewritten file has the new name; the log says so).
- **Full-autonomous steward** **[Anton 2026.09.27]** — *"when the player becomes really, really
  rich and doesn't need to micromanage at all… never even having to see that window."*
  - `AutonomousSteward` (default **off**; label "Full-autonomous steward") replaces `AutoExecute`.
    On: on arrival at a town or village the steward plans and carries the plan out at once —
    no window, no arrival popup, no leave warning. Once per visit. The "Party Steward" menu entry
    stays, so the player can still open the window to look or adjust.
  - `AutonomousMinGold` (default **100000**, range 0–10,000,000): the purse floor while autonomous.
    The effective floors become max(`MinGoldAfterDeal`, this) for everything and
    max(`MinGoldForHorses`, this) for animals — the rich player's autonomy can never drain the
    chest below it. All other limits (price book, multipliers, role caps, targets) apply as usual,
    so "set the limits very high" is how the player loosens it.
  - The tavern is NEVER hired autonomously (its rows start at 0 by design, §2.7).
  - **Report**: after the deal, a short summary in the game's message log (bottom-left, the
    "battle log"), one line per job that did something, e.g.
    `Steward at Sargot: food +24 (5 kinds) −310 · mounts +3 −540 · armour & weapons 41 sold +2,130
    · prisoners 12 ransomed +980 · gold 312,400 → 314,660`. Skipped/cut-short items get one line
    ("2 skipped — see log"). Nothing happened → no message. The full detail goes to the log file.
- `ModEnabled` (default on) master switch: off = no menu entry, no popup, no warning (the wrapped leave options let
  every leave straight through), no autonomy.
- **How the triggers behave** **[decided: Claude, 2026.09.27 — step 8]** (`Module\StewardTriggers`, `AutonomousRun`;
  Core `Planning\MoneyFloors`, `Execution\AutonomousReport`):
  - **Arrival** = the party walked in (`SettlementEntered`) and the town/village menu showed. A campaign LOADED inside a
    town is no arrival: no popup, no autonomous run (the leave warning still works there). The popup or the autonomous
    run waits until the map is quiet — the menu up, no inquiry, conversation, map incident, encyclopedia, escape menu or
    management screen — so it never fights the game's own arrival popups (an "entering town" incident waits first).
  - **"Changes"** (PopupOnlyWithChanges, the leave warning) = at least one row that moves something; the tavern's rows at
    0 do not count. The leave warning plans afresh at the click, so trading by hand in vanilla's screens counts.
  - The warning's buttons: **Review** opens the window (also from War Sails' port); **Leave anyway** carries out the
    leave the player clicked, the vanilla way (the game's leaving incidents roll then, not on the click that asked).
    Escape on the question = Leave anyway — the player had clicked Leave. Opening the window at all (even Not now)
    counts as reviewed for the visit.
  - **The window ignores AutonomousMinGold**: with the autonomous steward on, the menu entry still opens the normal
    window with the normal floors — the player's own hand. Only the steward's own runs answer to the autonomous floor.
  - **The autonomous plan never even lists the tavern** (not only "never hires"): nothing to hire, nothing to report.
  - `AutonomousMinGold` sits in the **Money** group with the other floors; its range tops out at 10,000,000.
  - **The report** is one message line — jobs joined by ` · `, as the example above, in the fonts' glyphs (en-dash minus,
    `»`) — plus the trouble line `Steward: 1 cut short, 2 skipped — see smart_steward.log` (or `nothing was done` when the
    run could not start). The counts are the executor's real ones, not the plan's. The jobs: food (with the kinds
    bought), mounts (the whole horses section: pack, riding, upgrade), armour & weapons, prisoners (ransomed / donated,
    influence).
  - **Every campaign starts clean** **[decided: Claude, 2026.09.28 — step 12]**: every campaign start and end (a new game,
    a load from the main menu or from inside a running campaign, the exit to the main menu) closes the window and drops the
    visit, a pending popup, Review or Leave anyway (`Module\CampaignSession`); what the steward holds is stamped with its
    campaign and never used in another (a leave option wrapped in an earlier campaign only passes through).
- **[research 2026.09.27]** (RESEARCH §10)
  - Menu entries go into the `town` and `village` menus (right after Trade); arrival =
    `SettlementEntered`, then the next `town`/`village` menu opening (it re-fires on every return
    to the menu, so one popup per visit is tracked in memory); a looted village (`village_looted`)
    gets nothing.
  - The leave warning wraps the leave options' own actions — `town_leave`, the village `leave`,
    `leave_set_sail`, `leave_at_sea` and War Sails' port `sail_option` — no Harmony needed.
    **[step 8]** Wrapped lazily, the first time each menu opens (War Sails builds its port menu after every other
    mod's session launch — RESEARCH §15).
    Clicking the map cannot take the party out of a settlement menu, so no other door needs
    guarding (Return to Army, a siege, prison break are left alone).

---

## 7. Settings (every number is a parameter)

| Group | Key | Default | Range | Meaning |
|---|---|---|---|---|
| General | ModEnabled | true | — | master switch |
| General | AutoPopupOnTownEnter | true | — | open the window when entering a town |
| General | AutoPopupOnVillageEnter | true | — | open the window when entering a village |
| General | PopupOnlyWithChanges | true | — | auto-open only when there is something to do |
| General | WarnIfNotReviewed | true | — | ask before leaving with unreviewed suggestions |
| General | AutonomousSteward | false | — | "Full-autonomous steward": plan and carry out on arrival, report in the message log (§6) — replaces `AutoExecute` **[Anton 2026.09.27]** |
| Money | MinGoldAfterDeal | 1000 | 0–1,000,000 | purse floor for all purchases |
| Money | MinGoldForHorses | 5000 | 0–1,000,000 | purse floor for animal purchases |
| Money | AutonomousMinGold | 100000 | 0–10,000,000 | purse floor while autonomous: both floors above rise to it (§6) **[Anton 2026.09.27]** |
| Food | FoodEnabled | true | — | manage food |
| Food | FoodPerMan | 2.0 | 0.1–10 | food units kept per eater |
| Food | FoodCountPrisoners | true | — | prisoners count as eaters (half each, like the game) |
| Food | FoodStrategy | Balanced | Balanced / Cheapest | Balanced (variety first) or Cheapest |
| Food | SellFoodSurplus | true | — | sell food above target + tolerance |
| Food | FoodSurplusTolerancePercent | 25 | 0–500 | how far above target before selling |
| Prices | BuyPriceMultiplier | 1.2 | 0.1–10 | final max buy = base × this **[Anton]** |
| Prices | SellPriceMultiplier | 0.8 | 0–10 | final min sell = base × this **[Anton]** |
| Prices | AutoFillFoodPrices | true | — | placeholder = average price for food |
| Prices | AutoFillPackAndMountPrices | true | — | … for pack animals and riding mounts |
| Prices | AutoFillWarMountPrices | false | — | … for war mounts (off: trader's cheat sheet) |
| Prices | PriceBook | {} | bases 0–1,000,000 | per item id: buy tick, buy base, sell tick, sell base (LATER: hold-up-to for Others) — only the player's changes (file + Prices tab, not MCM) |
| Pack | PackAnimalsEnabled | true | — | manage pack animals |
| Pack | PackAnimalsTarget | 10 | 0–500 | pack animals to keep |
| Pack | PackAnimalMaxPrice | 300 | 0–100,000 | role cap: never pay more per pack animal (0 = none; NOT scaled by the multiplier) |
| Pack | SellPackAnimalSurplus | true | — | sell above target, most expensive first |
| Mounts | MountsEnabled | true | — | manage riding mounts for footmen |
| Mounts | MountsPer100Footmen | 110 | 0–300 | mounts kept per 100 footmen |
| Mounts | MountMaxPrice | 500 | 0–100,000 | role cap: never pay more for a footman's mount (0 = none; not scaled) |
| Mounts | WarMountsCountAsMounts | true | — | war mounts held count toward the footmen's mounts |
| Mounts | SellMountSurplus | true | — | sell above target, most expensive first |
| War mounts | WarMountsEnabled | true | — | manage war mounts for upgrades |
| War mounts | WarMountsManualTarget | -1 | -1–500 | -1 = count upgrade-ready troops; ≥0 = keep exactly this |
| War mounts | WarMountsExtra | 0 | 0–100 | buffer on top of the automatic count |
| War mounts | WarMountMaxPrice | 2000 | 0–100,000 | role cap: never pay more per upgrade horse (0 = none; not scaled) |
| War mounts | SellWarMountSurplus | true | — | sell above need, most expensive first |
| Prisoners | RansomPrisoners | true | — | ransom prisoners in towns |
| Prisoners | RansomHeroPrisoners | false | — | include lords |
| Prisoners | DonatePrisonersWhenPossible | false | — | donate to own garrison (influence) instead |
| Prisoners | PrisonersExcluded | [] | — | troop ids unticked in *Prisoners to ransom* |
| Loot | SellLoot | false | — | sell other items |
| Loot | SellLootEquipment | true | — | weapons, armour, shields, ammo |
| Loot | SellLootMaxItemValue | 0 | 0–1,000,000 | never auto-sell items worth more per unit (0 = no cap) |
| Loot | SellLootOrder | Cheapest | Cheapest / LowestPricePerKg / MostExpensive | order within a loot group: Cheapest / LowestPricePerKg / MostExpensive — replaces `SellLootMassFirst` **[Anton 2026.09.27]** |
| Tavern | ShowTavern | true | — | show the tavern section in towns |
| Tavern | ShowWanderers | true | — | list wanderers for hire |
| Tavern | ShowMercenaries | true | — | list the tavern's mercenaries |

Keys are final names for the settings file and code; UI labels can be friendlier.

**Ranges** **[decided: Claude, 2026.09.27 — step 5]** (only the two multipliers were Anton's): wide enough for any
play style, narrow enough that a typo cannot break a plan — gold up to one million, a role price cap up to
100,000, `FoodPerMan` at least 0.1 (0 would sell every ration as surplus). Decimal settings keep 2 places (MCM's
float slider and the file agree on the same number). A value outside its range is clamped wherever it comes from
(file, MCM, Instructions tab).

---

## 8. Settings storage

- One global file: `Documents\Mount and Blade II Bannerlord\Configs\SmartSteward\settings.json`,
  created on first run. JSON with a `//` comment line above every key (meaning, default, range) —
  read with Newtonsoft (ships with the game, tolerates comments). Rewritten with comments on
  every save; unknown keys ignored, missing keys get defaults, out-of-range values clamped.
  **[research 2026.09.27]** The game ships Newtonsoft.Json **13.0.1** (`bin\Win64_Shipping_Client`) —
  reference it, don't ship a copy. Reading `//` comments is verified with the real DLL; its writer
  only emits `/* */` comments, so the file is written as text by our own code.
- One settings **registry** in code (key, type, default, min, max, group, label, help) drives
  all three views — the file, MCM (fluent builder, soft dependency) and the Instructions tab —
  so they can never drift apart.
  **[research 2026.09.27]** MCM binds through `ProxyRef<T>(getter, setter)` straight onto the
  registry. Enum settings (`FoodStrategy`, `SellLootOrder`) become MCM dropdowns. The game refuses to
  load a module if any of its types cannot resolve — no field, base type or captured lambda may touch
  an MCM type (RESEARCH §12).
  **[research 2026.09.27 — step 5]** Format **"none"**, not the default "memory" (which throws on the
  first registration in MCM 5.12.3): MCM keeps and writes nothing, settings.json is the only store.
- **How it is built** **[decided: Claude, 2026.09.27 — step 5]**:
  - Core: `SettingsRegistry` (the 45 §7 keys in table order: 43 scalars + the price book + the prisoner
    list), `SettingsFile` (text in, text out), `SettingsService` (the live values). Module: `SettingsHost`
    (the one service over the disk) and `McmBridge`.
  - The file opens with a short header (how to edit, when it is re-read, delete to reset, where the
    fixes are logged) and has a `// ===== Group =====` heading per group; above every key sits
    `// Label: help` (wrapped at 100) and `// Default: … Allowed: …`. Plain ASCII, CRLF, written sorted
    and deterministic — comparing texts tells whether anything changed.
  - Read at module load, again at campaign start and when the Party Steward window opens **if the
    file changed on disk** (size + write time) — or was deleted (a fresh default file is written).
  - Reading never fails the game: missing keys → default; unknown keys → ignored; numbers clamped;
    wrong types → default; keys and enum names are case-insensitive; `//`, `/* */` and trailing commas
    are fine. Every fix is a log line with its line number. After reading, the file is rewritten
    whenever its text differs from what the values would write (new keys appear, fixes land); a file
    that lost something (unreadable, clamped, wrong type, unknown key) is first kept as
    `settings.json.bak`. A file that cannot even be opened (locked) is never written over.
  - Every change (MCM, later the Instructions and Prices tabs) goes through the service: clamped,
    saved at once (written beside and swapped in), `Changed` raised for the window. MCM's Default preset
    holds the registry defaults. The log gets one line of the non-default values at each campaign start.
- The **price book** (§1.3) lives in the same file as a `PriceBook` object keyed by item id,
  holding only what the player changed (ticks flipped, bases typed). It is edited in the Prices
  tab or by hand; MCM shows only the multipliers and the auto-fill switches.
- A log at `Configs\SmartSteward\smart_steward.log` (what was planned, what was executed) for
  bug reports.

---

## 9. Non-goals for V1

- No per-save settings; no Harmony unless RESEARCH proves a hook is impossible without it.
  **[research 2026.09.27]** None is: every hook the design needs exists without Harmony.
- No trade-route planning (where to sell what) — that is a different mod. **[Anton 2026.09.27]**
  LATER, the price book's Others (§1.3.1) will allow simple buy-below / sell-above trading at whatever town the
  party is in (§1.3); weapons and armour never.
- Castles have no market: nothing happens there.
- All player-facing text goes through TextObject string ids (English only at release; other
  languages can be added by translators later). **[step 9]** The ids and their English are gathered in
  `module\ModuleData\Languages\std_SmartSteward.xml` (the game's own strings format — the English source a
  translator copies into `Languages\XX\`); a test holds the file to the code, so it cannot drift.
