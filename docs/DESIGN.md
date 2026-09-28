# Smart Steward — Design

The contract for the build. Source: `concept.txt` (Anton, 2026.09.27). Where the concept left a
choice open, the choice made here is marked **[decided: Claude, date]** so Anton can overturn it;
open questions for Anton are listed on the board under NOT FULLY DECIDED.

## Release scope **[Anton 2026.09.27]**

**V1 (the first Steam release)** = the Party Steward window with its three tabs, the triggers and
settings, and exactly five jobs:
1. **Tavern** — wanderers and mercenaries (§2.7)
2. **Food** (§2.1)
3. **Horses** — pack animals, riding mounts and war horses; noble horses sold, lame ones replaced (§2.2–2.4; simplified
   in step 17 **[Anton 2026.09.28]**: no troop upgrade is counted any more)
4. **Armour & weapons selling** — in bulk groups (§2.6)
5. **Prisoners** — ransom, or send to a friendly jail (§2.5) (Anton re-added them the same day)
6. **Troops** — the recruits on offer and your own troops: recruit and dismiss by hand (§2.8) **[Anton 2026.09.28, playtest
   round 3]**

**LATER** (designed here so a later update starts from a spec, but NOT built for V1 — marked
*LATER* where it appears): the price book's **Others** (trade goods, §1.3.1). Build steps must not implement LATER parts; keep the code open for them
(e.g. a section list the window renders, not hard-wired sections).

Terms: **food** = any item the game treats as food; **pack animal** = a pack-animal horse item
(sumpter horse, mule…); **mount** = a riding animal that is not a pack animal; **war mount** = a
mount of the `war_horse` category, kept to a plain number (step 17 — ~~a mount of the category a troop upgrade requires~~);
**noble horse** = a `noble_horse` mount — the player's and his companions' own, never bought, sold unless locked (step 17);
**lame horse** = any horse or pack animal with a BAD modifier (worth less than a plain one: lame, old — RESEARCH §22);
**loot** = everything else in the party inventory.

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
  plain `horse` can be both a footman's mount and an upgrade horse (see §2.4). **[step 17]** The steward no longer counts
  upgrades: a plain-horse upgrade draws on the riding horses, a war-horse one on the war horses kept (§2.3–§2.4).

---

## 1. The Party Steward window

One Gauntlet window, opened from the settlement menu (§6) or automatically on arrival. Title
"Party Steward". Three tabs: **Suggestion**, **Prices** (§1.3) and **Instructions**.

### 1.1 Suggestion tab — the table

Built for glancing, not reading: fixed columns, aligned numbers, colour for direction (buy =
green-ish, sell = red-ish, untouched = grey). One row per item (or prisoner troop) the steward
touches, plus rows for ticked item types the market has but the plan left at 0 (so the player
can add by hand).

**A closed market says why** **[decided: Claude, 2026.09.28 — step 12, playtest round 1]**: when the game does not let
the player trade here, the tab shows `Market closed: <reason>` in the game's own words (the disabled Trade option's text:
war, crime, a raid, nothing on offer, disguise — or ours where the game gives none: looted, being raided) — in place of
the table when nothing can be done, else on the header line's right above it. The log's snapshot line carries the same
reason (`Village (NO TRADE: …)`). **[decided: Claude, 2026.09.28 — step 16]** "Nothing can be done" = no row moves AND no
button is live (Core `PlanFooter.ShowsTable`): the troops section has its own gate (a village with nothing on sale still
offers volunteers, dismissing needs none), so at a closed market the table stays for the rows you can still act on, the
reason above it. (Until step 16 a closed market hid every row at 0 — the tavern's too.)

**Section order** **[Anton 2026.09.27]** — each section under its own header row:
1. **Tavern** — wanderers, then mercenaries (§2.7)
2. **Troops** — right after the Tavern **[Anton 2026.09.28, playtest round 3]**, in two halves under their own header rows:
   **Recruits on offer** (the troop types the notables offer you here), then **Your troops** (the party's other regulars) (§2.8)
3. **Food** — one row per food item (variety matters, so food stays itemised)
4. **Mounts** — ROLE rows, not one row per horse type (§1.1.1)
5. **Armour & weapons** — the loot group rows (§2.6)
6. **Prisoners** (§2.5) — last, with the other selling **[decided: Claude, 2026.09.27]**
7. *(LATER — not V1)* **Others** — the trade goods the player ticked in the Prices tab (§1.3.1)

**Folded sections** **[Anton 2026.09.28 — PLAN step 18: "each section header expands/collapses; collapsed = ONE summary
line … the state is REMEMBERED per section across windows, towns and game restarts (stays collapsed until Anton expands it)
— kept in our settings folder, never in the save"]** (Core `Presentation\SectionSummary`, `WindowState`):
- A click on a section's header (its ▸/▾ collapser icon or its name) folds it: the rows hide and the header shows the
  section's name and ONE line of what it will do with its gold, live with every click. The troops section's two halves fold
  together as **Troops** (the folded line sits on the first of them). Lines, and the neutral line when nothing is queued:
  - Tavern — `+3 hired –1,450` / `nobody hired`
  - Troops — `dismissing 1 T1 Imperial Peasant, 1 T1 Vlandian Recruit · recruiting 2 T3 Vlandian Footman –160` (past three
    types a count: `recruiting 12 (5 types) –640`) / `nobody recruited or dismissed` — and the folded Troops line carries its
    own `[-] [+]` (§2.8)
  - Food — `+29 (5 kinds), 12 sold –510 · 64 » 71 days` / `no change · 64 days` (the days as the footer counts them)
  - Mounts — `+10 (2 kinds), 3 sold –1,200` / `no change`
  - Armour & weapons — `41 sold +2,132 · –380 kg` / `nothing sold`
  - Prisoners — `12 ransomed, 4 to the dungeon +980 +3.2 influence` / `nobody ransomed`
  The same shape as the autonomous steward's report (moves, then the gold); the words are TextObjects (`ss_ui_sum_*`).
- **Remembered per section** until the player unfolds it — across windows, towns and game restarts — in
  `Configs\SmartSteward\window_state.json` (§8), written on every fold; not in MCM, not in the Instructions tab, never in
  the save. No file, or one that cannot be read → every section unfolded (the default).
- A folded section's rows are not refreshed while folded (each row's live buttons cost a trial walk — a big plan clicks
  faster with sections folded); unfolding refreshes them.

#### 1.1.1 Mount rows are grouped by role **[Anton 2026.09.27 — "don't show each type of mount"]**

One row per role, so a single `[+]` or `[-]` does the right thing without the player choosing
horse types:

| Role row | Holds | `[+]` buys | `[-]` sells |
|---|---|---|---|
| Pack animals | pack animals | the CHEAPEST eligible pack animal on the market | the MOST EXPENSIVE surplus one |
| Riding mounts | the riding horses: every mount that is not a war or a noble horse (`horse`, camels, a mod's categories) | the cheapest eligible riding horse | the most expensive one |
| War horses | `war_horse` mounts — kept to `WarMountsToKeep` | the cheapest eligible `war_horse` | the most expensive one |
| Noble horses | `noble_horse` mounts — **sell only** (`SellNobleHorses`) | never (grey: sell only) | the most expensive one not LOCKED |
| Lame horses | lame and old horses and pack animals — **sell only** (`ReplaceLameHorses`) | never (grey: sell only) | the most expensive one |

**[Anton 2026.09.28, step 17 — "make it simpler for now"]** The two upgrade rows (`horse` / `war_horse` "reserved for
upgrades") are gone: war horses are one plain number, noble horses and lame horses got their own sell-only rows. The
table shows Pack · Riding · War · Noble · Lame; the rows SELL in the order lame · pack · noble · war · riding (the riding
surplus is counted once every other kept horse is known — `PlanReplay.AnimalSellRank`, the planner and the editor alike).
Ids: `mounts:pack`, `mounts:riding`, `mounts:war`, `mounts:noble`, `mounts:lame`.

"Eligible" = buy-ticked in the Prices tab, priced within its own max (§1.3) AND within the role cap
(`PackAnimalMaxPrice`, `MountMaxPrice`, `WarMountMaxPrice`) AND plain — a modified animal is never bought (lame and old
ones by Anton's rule, step 17; any other modifier since step 4). Each step re-walks the marginal
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
| Type | Food / Pack / Mount / War mount / Prisoner / Loot / Tavern / Troop |

Buttons on Change:
- click **±1**, **shift+click ±5**, **ctrl+click ±all** (all = up to market stock or budget when
  buying; everything held when selling). **[research 2026.09.27]** These are the game's own
  rebindable hot keys `FiveStackModifier` (Shift) and `EntireStackModifier` (Ctrl) — the same the
  inventory and party screens use (RESEARCH §11).
- **⟲ reset** returns the row to the steward's suggestion. **[step 15]** It hands the row back to the steward: the touch is
  cleared and the steward plans the row again, around the rows the player still holds (the live re-plan below).
- Clamped: can never sell more than held, never buy more than the market has.
- Every edit re-prices the row (marginal prices, §4.1) and refreshes the footer live.
- ~~A row the player edits stays as edited — the steward does not re-plan other rows around it
  **[decided: Claude, 2026.09.27 — predictable beats clever]**.~~ — **PARTLY OVERTURNED [Anton 2026.09.28, playtest round 3]**:
  *"after I change the troops the mounts etc are not really accurate for the new numbers I entered, so can you recalculate
  the food and mounts as I add more troops or remove dynamically, so when I hit Do it at the end I won't see new
  suggestions for the new troop counts?"* A row the player edits still stays as edited; but an edit that changes the PARTY
  after the deal now re-plans every row the player has not touched — **the live re-plan**, below. An edit that does not
  change the party still re-plans nothing (predictable beats clever holds there).
- **The live re-plan** **[Anton 2026.09.28, playtest round 3; how: decided: Claude, 2026.09.28 — step 15]**
  (Core `StewardPlan` editing, `PlanPins`, `PartyAfter`):
  - **Touched rows.** Every row has an explicit "touched by the player" state (`PlanRow.IsTouched`): a click that moves it
    touches it, and it stays touched — even when clicks bring it back to the steward's number — until ⟲. The ⟲ shows exactly
    on touched rows (that is the subtle mark of "yours"; no prefab change). Untouched rows are the steward's.
  - **What re-plans.** An edit of a party-changing row — a tavern hire (wanderers, mercenaries), a prisoner row (a prisoner
    ransomed or donated stops eating, one kept eats half a ration), and the troops section's recruit and dismiss rows (§2.8,
    step 16) — re-derives the
    party after the deal and runs the SAME planners in the SAME walk, order, money chain and floors on the same snapshot,
    with every touched row pinned at its quantity. Every UNTOUCHED item row — food, pack animals, riding mounts, war, noble
    and lame horses, and armour & weapons (whose share of the market's gold moves with the food sales) — takes what the planners
    make of it now. Other edits do not re-plan (keeps a click fast and predictable). ⟲ and "Reset all" re-plan too: ⟲ hands one
    row back, "Reset all" hands every row back — with nothing touched that is exactly the first plan.
  - **The party after the deal.** Members = now + hires (+ recruits − dismissals). Eaters = members + half the prisoners who
    stay (the game's integer halves). Footmen = now + every man hired who is not mounted, by the game's own rule
    (`CharacterObject.IsMounted`: a troop by its default formation class, cavalry / horse archer; a hero by his battle
    equipment's horse slot — RESEARCH §3), carried per troop type in the snapshot (the mercenary band, each wanderer, every
    troop type of §2.8). ~~New men are never ready to upgrade (no XP yet), but a troop whose upgrade needs a kind of
    horse puts that kind in play …; men who leave a stack take its ready count down with them~~ — **[step 17]** nothing
    counts upgrades any more: the horses to keep follow the footmen only (§2.3). The footer's facts (food target and
    eaters, footmen, horses to keep), the party line and the capacity follow.
  - **Precedence** (`PlanPins`): in every phase of the walk — food sales · animal sales · loot sales · food buys · animal
    buys — the player's touched rows go FIRST, then the steward's; so the player's rows take the market's stock, its gold and
    a category's price room before the steward's. And because a phase only sees what came before it, the steward's rows
    also leave room for the player's rows of later phases: its sales leave the market the gold the player's later sales need,
    its buys leave the purse what the player's later buys AND hires cost — then the money floors as ever. So hires come
    first for the purse: the steward's own food and horses give way to them (a [+] on a hire is judged with the steward's
    buys giving way; if the re-planned deal still cannot pay — a bigger party leaves less food surplus to sell — the click
    steps back to the most hires it pays). The plan editor's walk (`PlanReplay`) uses the same precedence, so every edit
    prices the player's rows first too; with no row touched it is the planner's own order.
  - **Rows may come and go.** ~~A kind of upgrade horse that comes into play brings its row~~ (step 17: the horse rows no
    longer depend on the troops); an untouched row the re-plan no longer has goes (a touched one stays). The window keeps
    its row objects and rebuilds its table only then (`StewardPlan.Layout`).
  - **A settings change** (Prices/Instructions tab, MCM, the file) re-plans as before and puts the player's touched rows back
    in one re-plan (`PlanCarryOver` → `StewardPlan.Restore`): the steward plans its rows around them — the hires included.
  - **The promise, as a test** (`LivePlanTests`): plan → edit the party → Do it → a fresh plan of the resulting party proposes
    nothing new for food, pack animals, riding mounts, war, noble or lame horses (food within its surplus tolerance).
  - **Speed**: only a party-changing click (or ⟲ / Reset all) re-plans; on the late-game benchmark a re-planning click costs
    15–26 ms (the re-plan itself 1–6 ms, the rest the window's usual refresh), ordinary clicks 14–19 ms (step 9: ≤ ~20 ms).
- **How an edit behaves** **[decided: Claude, 2026.09.27 — step 4b]** (Core: `StewardPlan.Increase` /
  `Decrease` / `Reset` / `ResetAll`, the rows' live `IncreaseBlock` / `DecreaseBlock`):
  - Every click walks the WHOLE plan again in the planner's own order and picking rules (§3, §4.1), each
    row at its quantity — a town row can re-price another row of the same item category; no other row's
    quantity moves.
  - Toward zero (buy less, sell less) always works. Away from zero goes as far as it can **without taking
    what another row already has** — the market's stock, its gold, the price room under a row's max in a
    shared category. The button greys with the reason: nothing eligible, all on offer / all sold, price limit,
    below min sell, market out of gold, not enough gold, needed by another row, companion limit,
    dungeon full, sell-only / hire-only. (~~party full~~ — gone since round 3: the party size limit never blocks, §2.7.)
  - **Shift and Ctrl stop at zero**: one click never flips a row from selling to buying; a plain click crosses
    zero where the row does both (food, the mount role rows). Loot and prisoners only sell; tavern rows only hire.
  - The floors never block (red flags). An **empty purse does**: a buy or hire the purse cannot pay is refused
    (a wanderer needs MORE gold than his price, like vanilla). Taking income back (a ransom, a sale) may still
    leave the deal unaffordable — the footer shows it (`CannotAfford`) and **Do it** is disabled then.
  - Rarely, lowering one row makes another impossible (a sale taken back raises a category's price past the
    max of a row buying in it): that row is cut to what the market allows.
  - ⟲ returns a row to the suggestion as far as what other rows took since allows; reset-all restores the plan
    exactly. **[step 15]** Both by a re-plan now: the row (every row) is the steward's again.
  - Prisoners moved by hand join the same split as the steward's: the dungeon's room filled most valuable
    first, the rest ransomed.
- **The window as built** **[decided: Claude, 2026.09.27 — step 7]**:
  - **Re-planning keeps the player's hand.** A settings change (Prices or Instructions tab, MCM, the file) re-plans
    on the same snapshot when the Suggestion tab shows again; every row the player had edited is set back to its
    edited quantity by row id, as far as the new plan allows (new limits clamp it; a row the new plan no longer has
    is dropped); the other rows take the new suggestion. **Do it** looks at the world again and plans afresh with no
    carry-over — the edits were carried out. (Core `PlanCarryOver`, `StewardPlan.SetChange`.) **[step 15]** "Edited" =
    touched; they are put back in one re-plan (`StewardPlan.Restore`), so the new suggestion is planned around them.
  - The Price cell reads `units × unit price = signed total` (`3 × 180–240 = –630`, `5 × 48 = +240`); a tavern row at
    0 shows the price to hire. The game's UI fonts have no → − ≈ ⟲ ▸ (RESEARCH §14): the header reads
    `Gold 12,400 » 10,930 (–1,470)`, the minus is an en dash, ⟲ and ▸ are the game's refresh and collapser icons.
  - A **Reset all** button sits bottom left of the Suggestion tab. The small grey words after a name carry the
    role's target, what a noble or lame row does, the weight a loot sale frees, a wanderer's skills and wage. The Mounts
    section's header line reads `100 men on foot · 110 horses to keep` (step 17).
  - Typed numbers (price bases, multipliers, settings) are saved on every key that leaves a valid number; anything
    else turns the box red and saves nothing. An empty price box = the placeholder again.

Footer: gold now → gold after · spent / earned · food after (units and ≈ days) · the party after the deal ·
the weight lines and the herd line · buttons **Do it** and **Close** (Escape = Close; the button read "Not now" until
**[Anton 2026.09.28, playtest round 3]**: "it is just Close").
**The weight on two lines** **[Anton 2026.09.28 — PLAN step 18: "the weight line splits in TWO … (pack horses raise land
capacity but add weight at sea) — each part over its capacity in red"]**, each like the food line:
- `Land:  weight 1,000 +120 » 1,120 kg · capacity 1,500 » 1,900` — the land load now, the change, after; the land capacity now
  and after the deal (just `capacity 1,500` when the deal does not move it); `+720 over` in red beside it when the load after
  is over the capacity after.
- `Sea:   weight 1,300 +420 » 1,720 kg · capacity 1,000` — the same at sea (the animals and the mounted men's horses weigh
  there, RESEARCH §19), only when the party has ships (War Sails, as step 14 decided); `+720 over` in red likewise.
- A failed capacity read: `Land:  weight +120 kg` alone. The sea line, the warnings and the status line stack below the land
  line and close up when hidden; the footer grew 146 → 176 px (the table ~one row shorter — folding sections gives it back).
**The herd line** **[Anton 2026.09.28 — PLAN step 18: "Horses 110 / 200 before the herd slows you" — the game's real herding
threshold, red when over]**, after the land line (the herd is a land rule): `Horses H / R before the herd slows you` — H = the
mounts and pack animals after the deal, R = the men after the deal + the mounts their footmen ride − the livestock the party
drives (`(20 livestock take room too)` is added when it drives any). The game slows a party only when its herd (pack
animals, livestock and the mounts nobody rides) OUTNUMBERS its men — equal is fine — and H ≤ R is exactly that (RESEARCH
§23; Core `Planning\HerdTotals`). An army's attached parties are pooled in, as the game does; prisoners are not men to it; at
sea there is no herd. Red when H > R; live with every click. Anton's example: 100 footmen, 110 horses → `Horses 110 / 200`.
~~**The weight line** **[Anton 2026.09.28, playtest round 3 — "like the food line"]**, on its own footer line:~~ (step 14; the
two lines above replace its one-line form — the rules below still hold)
`Weight 1,000 +120 kg » 1,120 kg · capacity land 1,500 / sea 1,000` — the load now, the change and the load after, then
the carrying capacity AFTER the deal: pack animals and mounts bought or sold and troops hired change it (the ransomed
prisoners too, with the Forced Labor perk). The sea capacity (the fleet's cargo + the men) shows only when the party has
ships (War Sails); without ships the line reads `capacity 1,500`. At sea the game also weighs every animal and every
mounted troop's horse, so when the load at sea differs the line adds `(1,620 kg at sea)`. Any part over a capacity shows
in red beside it — `+120 over on land`, `+620 over at sea` (without ships just `+120 over`), rounded up. It updates with
every click. **[decided: Claude, 2026.09.28 — step 14]** The numbers NOW are the game's own (its capacity model, so a mod
that changes it is honoured); the change is Core arithmetic at the vanilla formula's rates with the party's perks
(RESEARCH §19, `Planning.CarryTotals`). Nothing is blocked by it — being over capacity only slows the party, like vanilla.
If the capacity cannot be read, the line shows the weight change only (`Weight +120 kg`).
**The party after the deal** **[Anton 2026.09.28, playtest round 3]**: `Party 99/96` — the members after every hire,
recruit and dismissal in the plan (§2.8) against the party size limit, red when over, live with every click. Information, never a wall (§2.7). When the player's edits break a money floor (§3),
the footer shows it in red, but **Do it** still works — the player's hand overrides the steward.

### 1.2 Instructions tab — the settings

Every setting from §7, grouped as in §7, editable in place (checkboxes, number steppers). ~~The War
mounts group also shows the live count "Troops ready to upgrade now: N for a horse, M for a war horse" beside its two
targets (§2.4, step 12).~~ **[Anton 2026.09.28, step 17]** "Horses per 100 footmen" and "War horses to keep" each show
after their box what they mean for this party right now — `(110 horses = 100 riding + 10 war, for 100 footmen)` (the
party before the deal; Core `Planning\MountGoal`) — in place of the range, which stays in the tooltip (the note fills the
line's room).
- ~~**Prisoners to ransom**: a tick-list of troop types~~ — **REMOVED [Anton 2026.09.28, playtest round 1: "ransom all
  or none"]**. The `RansomPrisoners` switch and `RansomHeroPrisoners` (lords) are the whole choice; an old settings
  file's `PrisonersExcluded` is ignored (logged once).

(The old *Food to keep* list moved into the Prices tab as the food rows' Buy ticks.)

Changes save to the settings file at once (and MCM shows them — same values, §8).

### 1.3 Prices tab — the price book **[Anton 2026.09.27]**

Anton: *"as I get richer I will stop caring for the price — I don't want to raise the max price
for each item, give me a multiplier."* So every item has its own base prices, and two global
multipliers scale them all.

Groups, each collapsible: **Food**, **Horses** (sub-headers Pack animals · Mounts · War mounts · **Noble horses — sell
only**, step 17: no buy column there, the sell placeholder always filled).
V1 has only these two **[Anton 2026.09.27]** — every other trade good (wood, jewelry, metal,
livestock, …) is left alone by the steward in V1; the *Others* group is designed below (§1.3.1)
for a later update. **Armour and weapons are
NEVER in the price book** **[Anton 2026.09.27]**: they are only ever SOLD, in bulk, as the loot
groups of §2.6 — never bought, no per-item prices, no averages computed for them at all.

One row per item:

| Item | Buy | Max buy price | Sell | Min sell price |
|---|---|---|---|---|
| Grain | ☑ | `[ 11 ] × 1.2 → 13` | ☑ | `[ 7 ] × 0.8 → 6` |

(The item name is the FIRST column **[Anton 2026.09.28, playtest round 1]**; the `⟲` sits after the min sell price.)

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
  base stays empty until the player types one. **Noble horses always auto-fill** **[decided: Claude, 2026.09.28 — step
  17]**: they are only ever sold, so their average SELL price is no trader's cheat sheet — and without it a noble horse
  would go at any price, a pittance in a village (Anton: "sold at ≥ their min sell price like any horse").
- **A modified stack's min sell** **[decided: Claude, 2026.09.28 — step 17]** = its item's final min sell × the modifier's
  price factor (`ItemModifier.PriceMultiplier`: lame 0.1, old 0.2 — the game prices a lame horse at a tenth of a plain one):
  a lame horse is judged against a lame horse's worth, so the Lame horses row can sell it at all (`PriceBook.MinSellOf`).
- **Defaults of the ticks**: food — Buy ☑ Sell ☑; pack animals and mounts — Buy ☑ Sell ☑;
  war mounts — Buy ☑ Sell ☑; noble horses — Sell ☑ (no buy tick shown: never bought).
- **Which horses sit under "War mounts"** ~~**[decided: Claude, 2026.09.27 — step 4]**: the Mounts
  sub-header holds the `horse` category (plain riding horses and camels); `war_horse`, `noble_horse`
  and any category a mod adds sit under War mounts — no auto-filled price by default.~~ **[Anton 2026.09.28 — step 17:
  "noble horses are never used to upgrade troops; exclusively for you and your companions"]**: War mounts = `war_horse`
  only; `noble_horse` has its own sub-header (Noble horses — sell only); Mounts = `horse` and any category a mod adds (to
  the steward they are riding horses — nothing counts upgrades any more; `PriceBook.GroupOf`). A final price is rounded to
  the nearest denar, halves up (`[11] × 1.2 → 13`).
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

- ~~**Target** = `ceil(eaters × FoodPerMan)`.~~ Eaters = party members (+ prisoners if
  `FoodCountPrisoners`). ~~`FoodPerMan` default **2.0** (Anton's number — at vanilla consumption
  of about 1 food per 20 men per day that is ~40 days; the window shows the days).~~
- **The goal is DAYS** **[Anton 2026.09.28, playtest round 3 — "you are showing the food as days to last very nicely, it is
  better than my idea food barrels/soldier — can you make the instruction for the steward as DAYS to last as the goal, and in
  brackets show the food barrels per soldier that requires, like: keep food [40] days (keeps 2 barrels per soul)"]**:
  **Target** = `ceil(FoodDays × daily use per eater × eaters after the deal)`, `FoodDays` default **40**. The daily use per
  eater is the game's own: the party's `−MobileParty.FoodChange` (perks included) over its eaters now — members + prisoners/2,
  the game's integer halves, at least 1 (RESEARCH §2); vanilla's 1/20 when it was not read. So 40 days = 2 food per man at
  vanilla's rate (the old default, unchanged for a perkless party), and a Warrior's Diet party keeps less for the same days.
  The eaters are the party after the deal — hires, recruits and dismissals, prisoners kept (half each) or not — so the goal
  follows the live re-plan (§1.1). **[decided: Claude, 2026.09.28 — step 15]** `FoodDays` is a whole number of days, 1–365
  (0 would sell every ration as surplus); the Instructions tab shows after the box what it means for this party right now —
  `Keep food for (days) [40] days (~2.0 per soul)` (the fonts have no ≈); MCM and the settings file cannot know the party, so
  their help gives vanilla's equivalence (40 days ≈ 2 per man). The footer keeps its `(~N days)`. An old settings file's
  `FoodPerMan` becomes `FoodDays = FoodPerMan × 20` once (logged, not a problem; the rewritten file has only `FoodDays`;
  `FoodDays` written too wins) — `SettingsFile.ConvertedKeys`, Core `Planning\FoodGoal`.
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
- **[step 15]** The eaters are the party AFTER the deal: the men the plan hires eat from today, and the target follows every
  hire and every prisoner kept or ransomed live (§1.1, the live re-plan).
- **Locked food is managed** **[Anton 2026.09.28, playtest round 2]**: food LOCKED in the inventory screen counts as held
  and is sold as surplus by the rules above like any other (most-held type first, down to the target) — unless
  `LocksProtectFoodAndHorses` (default **off**; on = locked food is counted but never sold, the old way). See §2.6.

### 2.2 Pack animals — keep X

- **Target** = `PackAnimalsTarget` (default **10**). Buy the cheapest ELIGIBLE (§1.1.1: Buy-ticked,
  within its price-book max AND under `PackAnimalMaxPrice`, default **300**) until the target is met.
  Surplus is sold only at ≥ each animal's final min sell price, only Sell-ticked ones.
- **Sell surplus** (`SellPackAnimalSurplus`, default on) above the target, most expensive first.
- **[research 2026.09.27]** Each pack animal adds ~100 carrying capacity (perks raise it); animals
  weigh nothing themselves. Selling any mount or pack animal carries a +0.8 trade penalty (it
  fetches about half its buy price) — the planner never sells and buys the same kind in one visit.
  **The one exception [Anton 2026.09.28, step 17]**: the Lame horses row sells the lame and old ones while this row (and
  the riding and war rows) buy healthy ones in their place — that is what "Replace lame horses" asks for.
- **Lame pack animals** (a bad modifier, RESEARCH §22) are never bought; with `ReplaceLameHorses` (default **on**) they
  sit in the Lame horses row (sold) and do not count toward the target, so healthy ones replace them; one the market
  cannot take this visit (gold, min sell) still counts. Off: kept and counted, like any pack animal.
- **Locked pack animals are managed** **[Anton 2026.09.28, playtest round 2]**: counted as held and sold as surplus, most
  expensive first, locked or not — unless `LocksProtectFoodAndHorses` (§2.6).

### 2.3 Mounts — horses for the footmen

**[Anton 2026.09.28, step 17 — "don't worry about the mounts needing upgrades … the other soldiers just draw from my mounts
… make it simpler for now … this simplifies your whole deal with the upgrades that confused me."]** (Core
`Planning\MountGoal`, `MountPlanner`, `LameHorsePlanner`.)

- **Footmen** = party troops (not heroes? — per RESEARCH: count what the game's speed model
  counts) who ride no horse.
  **[research 2026.09.27]** Footmen = the speed model's `PartyBase.NumberOfMenWithoutHorse`: every
  party member, **heroes and wounded included**, whose battle equipment has no horse (a hero is
  mounted when his battle-equipment horse slot is filled). Footmen ride any **mount** in the
  inventory (war and noble horses and camels too) but **never a pack animal**. Mounts beyond the
  footmen join the herd, which slows the party only once animals outnumber the men.
  **[step 15]** The footmen of the party after the deal: every man the plan hires who is not mounted (the game's own
  `CharacterObject.IsMounted`, RESEARCH §3) needs a mount too — live with every hire, recruit and dismissal (§1.1).
- **Horses to keep** `T = ceil(footmen × MountsPer100Footmen / 100)` (default **110** → a 10% buffer). EVERY mount kept
  counts toward it — riding horses, the war horses kept (§2.4), noble horses kept (locked, or `SellNobleHorses` off) and
  lame ones kept (`ReplaceLameHorses` off, a guarding lock, or the market could not take them) — because a footman rides
  any of them. ~~War mounts held (§2.4) count toward this target when `WarMountsCountAsMounts`~~ (retired: they always do).
- **Riding horses fill the rest**: `R = T − the war, noble and lame horses kept after the deal` (with the war horses at
  their number: `R = max(0, T − W)`). **Anton's example**: 100 footmen at 110 per 100, keep 10 war → **100 riding + 10
  war = 110 horses**. When he upgrades men with those war horses they become cavalry, the footmen drop, and the numbers
  settle by themselves (the next town buys the war horses back up to W and sells the riding horses the smaller party no
  longer needs — test `After_an_upgrade_took_the_war_horses_the_numbers_settle_by_themselves`).
- **Buy** the cheapest ELIGIBLE riding horses (§1.1.1 — price-book max AND ≤ `MountMaxPrice`, default **500**; never a
  war or noble horse — a war horse bought as a riding horse would be a surplus war horse next visit — and never a modified
  one). Riding mounts outrank war horses for the purse (§3): when the purse cannot pay for every war horse short, riding
  horses fill the gap first — the steward pledges the war horses it will buy, and while fewer are affordable (or on offer)
  it buys more riding horses instead (the pledge simulation, kept from step 4).
- **Sell surplus** (`SellMountSurplus`, default on) above `R`, **most expensive first** — against the war horses the party
  will HAVE after the deal, those bought in this visit too **[decided: Claude, 2026.09.28 — step 17]**: when the steward
  buys war horses while the riding row has a surplus, the planner plans once more with those war horses pledged
  (`MountPlanner.PledgeHint` → `PlanContext.WarPledge`), so 110 riding + 0 war with "keep 10" becomes −10 riding, +10 war
  in ONE visit, and a fresh plan after Do it has nothing left (Anton's promise, §1.1). Should the second pass afford fewer
  war horses than the first (the riding sales' gold is the market's too), a footman may lack a horse until the next town.
  (The old rule "riding surplus is not sold in a visit that buys an upgrade horse" is gone: a riding and a war horse are
  different kinds now.) Never a riding horse bought in a visit that sells riding horses.
- **Locked mounts are managed** **[Anton 2026.09.28, playtest round 2]**: riding mounts and war horses LOCKED in the
  inventory count as held and their surplus is sold most expensive first, locked or not. With
  `LocksProtectFoodAndHorses` on, locked mounts are counted but never sold (§2.6). **A noble horse's lock always keeps it**
  (step 17, below).
- **Noble horses** **[Anton 2026.09.28, step 17 — "never used to upgrade troops; exclusively for you and your companions …
  we don't care about noble horses and we are selling them if the player didn't lock them"]**: the `noble_horse` category
  is **never bought**, and every one NOT LOCKED is sold (`SellNobleHorses`, default **on**; Mounts group) — a lock ALWAYS
  protects a noble horse, whatever `LocksProtectFoodAndHorses` says (`LockRule.IsGuarded`; the executor honours a lock
  set after the plan too). They sell at ≥ their final min sell price like any horse (the Prices tab's "Noble horses — sell
  only" sub-group always shows the sell placeholder, §1.3), so a village never gets one for a pittance. Their own row in
  the Suggestion tab: **Noble horses**, sell only. Kept (locked, the switch off, unticked, or below the min price) they
  carry footmen and count toward T.
- **Lame horses** **[Anton 2026.09.28, step 17 — "the whole idea is to speed up the infantry, so if a lame horse gives the
  bonus — keep it; never buy them as they don't look good; add a button to sell and replace them with healthy ones, default
  ON"]**: the steward **never buys** a horse or pack animal with a BAD modifier (`ItemModifier.PriceMultiplier < 1`, the
  game's own test — RESEARCH §22: vanilla's "Lame" and "Old"). `ReplaceLameHorses` (default **on**; Mounts group): every
  one the party holds — pack, riding or war; not a noble horse (that row sells it anyway), not one a lock guards, not one
  unticked for selling, and only for a role the steward manages — sits in its own **Lame horses** row (sell only, the
  dearest first, first among the animal sales) and is sold, while its role row, which does not count it, buys a healthy
  one: the explicit exception to "never sell and buy the same kind in one visit" (§2.2). One the market cannot take this
  visit still counts toward its role (it carries a footman). Off: they are kept and counted — a lame horse does carry a
  footman (RESEARCH §22: the speed model counts every `IsMount` animal, modified or not).

### 2.4 War mounts — a plain number to keep

- ~~**Needed** per war-mount category = number of troops that can upgrade NOW to a tier that requires that category, +
  `WarMountsExtra` …; `WarMountsHorseTarget` / `WarMountsWarHorseTarget` (-1 = automatic) …; reserved = the held horses
  the upgrades would take …; `WarMountsCountAsMounts` …~~ — **SUPERSEDED [Anton 2026.09.28, step 17]** (see §2.3's
  quote). The step-4/12/15 machinery (`UpgradeNeeds`: the party screen's ready count per stack, the plain-horse upgrade
  reserve, the kinds "in play", the per-kind fixed numbers and spares, the reservation in the game's consumption order,
  the "Upgrade horses (horse)" and "(war_horse)" rows, the Instructions tab's "Troops ready to upgrade now" line) is gone,
  and with it the snapshot's upgrade stacks and every troop's upgrade kinds. An old settings file's
  `WarMountsHorseTarget`, `WarMountsWarHorseTarget`, `WarMountsExtra` and `WarMountsCountAsMounts` (and round 1's
  `WarMountsManualTarget`) are retired — ignored and logged once, not converted (`SettingsFile.RetiredKeys`).
- **War horses to keep** `W = WarMountsToKeep` (default **10** since round 4 **[Anton 2026.09.28]** — **0** until then; 0–500;
  War mounts group, label "War horses to keep"; kept only once the purse reaches `WarHorsesMinDenari`, §3) — a
  plain NUMBER of mounts of the **`war_horse` category** (noble horses are NOT war mounts; a mod's category is a riding
  horse). The steward **buys up to it** — the cheapest ELIGIBLE (price-book max if set — war horses have no auto-filled
  placeholder by default — AND ≤ `WarMountMaxPrice`, default **2000**; never a modified one) — and **sells above it**, the
  dearest first, when `SellWarMountSurplus` (default on). They count toward T (§2.3): the riding horses fill the rest. With
  0 every unlocked war horse is surplus — set the number to what you mean to upgrade.
- **Upgrades draw on what is kept** — the game's own upgrade takes a horse of the category the target troop needs
  (RESEARCH §4): a plain-horse upgrade draws from the riding horses, a war-horse one from the war horses kept; the
  upgraded men ride as cavalry, the footmen drop, and the next plan follows (§2.3).
- `WarMountsEnabled` (default on, "Manage war horses"): off = a war horse is a plain riding horse to the steward (counted,
  bought and sold as one; no War horses row).
- **[research 2026.09.27]** The requirement sits on the troop you upgrade INTO
  (`UpgradeRequiresItemFromCategory` of the target); vanilla uses `horse` and `war_horse` only. An upgrade consumes the
  **cheapest** animal of the category first and locked ones only last (kept for the record — the steward no longer
  reserves any).

### 2.5 Prisoners — ransom or donate

- Only in towns (villages have no ransom broker). `RansomPrisoners` default on.
- Every prisoner is proposed for ransom — all or none **[Anton 2026.09.28, playtest round 1]**, no per-troop list;
  heroes (lords) never unless `RansomHeroPrisoners` (default off). Locked prisoners are skipped, as vanilla does.
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
  either unless `RansomHeroPrisoners`. Heroes keep a row at 0 (the player may add them by hand); locked prisoners get
  no row.

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

- Never sold: armour and weapons LOCKED in the inventory screen, food, animals (handled above), items above
  `SellLootMaxItemValue` per unit (default **0** = no cap).
  **[Anton 2026.09.27]** A LOCKED piece is never sold and **not even counted as sellable** (the row
  shows it apart as "+N locked"). **[research 2026.09.27]** Locks are read from
  `IViewDataTracker.GetInventoryLocks()`; a lock id is the item id + the modifier id, so a locked
  "rusty sword" does not lock a plain one (RESEARCH §6). Also never sold: livestock, banners,
  books, quest items, non-transferable items.
- **What a lock guards** **[Anton 2026.09.28, playtest round 2 — "food and horses even if locked, manage them"]**: locks
  keep guarding ARMOUR & WEAPONS exactly as above, whatever the settings. Food, pack animals, riding mounts and war
  horses are managed WHETHER LOCKED OR NOT — counted as held, sold as surplus by their own rules (§2.1–§2.4) — unless
  `LocksProtectFoodAndHorses` ("Locks protect food & horses", General, default **off**): on, locked food and animals are
  counted but never sold and shown apart as "+N locked", the old way. **A NOBLE horse's lock always guards it**
  **[Anton 2026.09.28, step 17]** — the lock is how the player keeps his own horse (§2.3).
  The planner and the executor share one rule (Core `Planning\LockRule`): each sale carries whether its lock guards it
  (`PlanTransaction.HonoursLock`), and at the click only a guarded sale of a stack locked now is skipped (§5). The game's
  own lock only works in the trade screen's "transfer all" (RESEARCH §6), so a locked stack sells through vanilla's
  headless trade like any other; the lock itself stays set (it is kept per item id, so a stack bought back is locked again).
  Upgrades by the game itself are unaffected — they take unlocked animals first, locked ones last (§2.4).
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
  party) with no dialogue. Blocked (greyed, tooltip says why) only when the clan's companion limit
  is reached — vanilla enforces that one too.
- **Mercenaries** (the tavern's mercenary band): one row — troop name, how many are on offer,
  price per man, daily wage per man. Change is `[-] 0 [+]` with the usual shift/ctrl steps,
  clamped to what is on offer.
- **The party size limit is information, not a wall** **[Anton 2026.09.28, playtest round 3 — "hire past it, just show
  the party after the deal"]**: no hire is ever clamped or blocked by it — not in the plan, not by the executor — exactly
  like vanilla. The footer shows `Party 99/96` (§1.1), red when the deal takes the party over its limit. (Until round 3 the
  steward blocked hires at the limit; the `party full` edit block and skip reason are gone.) Recruits (the troops section,
  §2.8) follow the same rule.
- Tavern hires count in the header total and the footer like any purchase, but sit OUTSIDE the
  money floors' priority chain (the player chose them by hand; a floor breach shows red, §1.1).
  **[step 15]** They come FIRST for the purse: the steward's own food and horses — planned live for the party with the hires
  (§1.1) — leave their gold, then answer to the floors.
- `ShowTavern` (default **on**) shows the section; `ShowWanderers`, `ShowMercenaries` (default
  **on**) toggle its halves.
- **Names are clickable** **[Anton 2026.09.27]**: a wanderer's name opens that hero's Encyclopedia
  page; the mercenary troop's name opens that unit's Encyclopedia page. Closing the Encyclopedia
  returns to the Party Steward window as it was.
- **The Tavern section is the tavern district** **[Anton 2026.09.28, playtest round 3: an unmet wanderer's page showed
  "???" until he opened the tavern district]**: vanilla learns about every hero in the tavern the moment the player stands
  in the district (RESEARCH §20). The steward does the same for the wanderers it lists — when the window is on screen (not
  for an arrival popup that stays shut), and again before a name opens the Encyclopedia — so the page is complete. The game
  says "You've learned about …" in the message log, as in the district. **[decided: Claude, 2026.09.28 — step 14]** At
  show, not only at the click: the window already shows their names, skills and prices. Mercenary pages are unit pages —
  never hidden. **[research 2026.09.27]** Opened with
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
  - Vanilla itself never checks the party size limit for either — and since round 3 neither do we.

### 2.8 Troops — recruits on offer and your troops (Anton, 2026.09.28 — playtest round 3)

Anton: *"since recruiting works so nice, list me all the recruits available in the town — don't worry about the troop
limit, just show me that I'm above it — and add all my troops like with the goods, but don't list every possible troop:
list what is on offer at the top and what I have, so I can manage them, drop some of mine and recruit new."*

The tavern's sibling, right after it (§1.1). **Towns and villages** (a village's headman and rural notables have volunteers
too); castles have no market and no steward. One row per troop type — never every troop in the game, never a hero:
- **Recruits on offer** (the first half, its own header row): every troop type the settlement's notables offer YOU now —
  the game's own recruit screen, exactly: each notable who can have recruits, each of his six volunteer slots the game's
  volunteer model opens to you (relation, faction, war, perks — RESEARCH §21), behind the game's "Recruit troops" gate (a
  hostile or raided village: none). Columns: **Mine** = the men of that type in your party; **Change** `[-] 0 [+]` — [+]
  recruits (click / shift / ctrl steps, clamped to what is on offer), [−] dismisses your own men of the type (down to −Mine);
  **Result**; **Price** = the price per man (the game's recruitment cost, your perks included) at 0, `3 × 20 = –60` when
  recruiting; **Market** = how many are on offer. Detail: the daily wage per man, the wounded.
- **Your troops** (the second half, its own header row): every other regular troop type in the party the party screen lets
  go (a quest-bound troop is not listed unless it is on offer too — and then never dismissed). Mine, Change `[-] 0 [+]` where
  [−] **dismisses** (clamped to the men held; [+] is grey: "No notable here offers you this troop."), Result; Market "—", no
  price (dismissing is free).
- **Every row starts at 0** — the steward never recruits or dismisses by itself; the Full-autonomous steward never even lists
  the section (like the tavern). Shift/Ctrl stop at zero; a plain click steps through it (a row on offer recruits and
  dismisses). ⟲ hands the row back (to 0).
- **The party size limit never blocks** — the footer's `Party 99/96` counts recruits in and dismissed men out, red when over
  (§1.1, §2.7). Only an empty purse stops a recruit (vanilla's rule: the cart's total ≤ your gold).
- **Party-changing rows** (§1.1, the live re-plan): a recruit or a dismissal re-plans every untouched row — the eaters, the
  footmen (a recruit on foot needs a horse; the game's own rule, RESEARCH §3) and with them the horses to keep (§2.3), the
  capacity. (~~and the upgrades: new men put their kind of upgrade horse in play; dismissing upgrade-ready men lowers the
  upgrade-horse need~~ — step 17: nothing counts upgrades any more.)
  Their gold comes first for the purse, like the hires: the steward's own buys give way.
- **Dismissing takes the WOUNDED first** **[decided: Claude, 2026.09.28 — step 16]**: vanilla's party screen does exactly
  that when you move men out of your party (RESEARCH §21), and it keeps the men who can fight now — and carry: healthy men
  add capacity, wounded do not, so the weight line's capacity drops only once no wounded of the type are left. (The brief
  said "healthy first unless research says otherwise"; research said otherwise.)
- ~~**Order within each half** **[decided: Claude, 2026.09.28 — step 16]**: by name — predictable, like the wanderers.~~ —
  **BY TIER [Anton 2026.09.28 — PLAN step 18: "MY troops ordered by tier, LOWEST on top"]**: **Your troops** lowest tier
  first (what a dismissal takes first sits on top); **Recruits on offer** HIGHEST tier first — they are what Anton takes
  **[decided: Claude, 2026.09.28 — step 18; Anton can flip it to lowest first]**; ties by name, then id (Core
  `TroopPlanner`, `DismissOrder` / `RecruitOrder`).
- **The tier before every name** **[Anton 2026.09.28 — step 18]**: `T1 Vlandian Recruit` — the game's own tier
  (`CharacterObject.Tier`: 0–6 in vanilla, RESEARCH §24; a tier-0 troop shows `T0`, where the game's screens show no icon).
  The `T` is a translatable word (`ss_ui_tier_prefix`); the folded Troops line uses the same names.
- **The folded Troops line's own `[-] [+]`** **[Anton 2026.09.28 — step 18: "[-] dismisses from the lowest tier up, [+]
  recruits the highest tier on offer first (shift/ctrl steps as usual), and the line says what it does"]** (Core
  `StewardPlan.DismissLowest` / `RecruitBest`, `DismissLowestBlock` / `RecruitBestBlock`): click 1 man, shift 5, ctrl every
  man.
  - `[-]` takes men out of the party after the deal, lowest tier first: the party's own troops (Your troops) from the lowest
    tier up, then the men of the types on offer by the same order; a type with recruits queued gives those back first (they
    are the men of that type the deal would bring). Greyed when nobody is left who may go.
  - `[+]` recruits the highest tier on offer first, spilling to the next type when one runs out; a type the purse cannot pay
    is passed for the next (a lower tier costs less); a type with dismissals queued takes those back first. Greyed with the
    reason (nobody offers a troop here, every volunteer already in the plan, not enough gold).
  - They are ORDINARY row edits underneath (each moves troop rows with `SetChange`): the rows are touched (⟲ shows when
    unfolded), the food and horses re-plan live, Do it runs the same Dismiss and Recruit transactions — no new executor path.
    The line then says what the section does: `dismissing 1 T1 Imperial Peasant, 1 T1 Vlandian Recruit · recruiting 2 T3
    Vlandian Footman –160`.
- **Names are clickable**: a troop's name opens its unit page in the Encyclopedia (like the mercenaries — unit pages are never
  hidden).
- `ShowTroops` (default **on**, the Tavern group — "Show the troops") shows the section.
- **Do it** **[decided: Claude, 2026.09.28 — step 16]** (the executor, §5; RESEARCH §21): **dismissals right after the
  prisoners** — men leave before any goods move; free, and nothing in the trade depends on them — through the party screen's
  own roster move (the wounded first; no gold, no event); **recruits last**, after the trades (their proceeds fund them) and
  the tavern's hires (the table's order), through the recruit screen's own steps (the notable's slot emptied, the man added,
  `OnUnitRecruited` — the Leadership XP — and the gold paid once per row). Everything is re-checked at the click: the recruit
  gate, the slots still holding the troop and still open to you (taken in the recruit screen's order), the live price, the
  purse; the men still held.

---

## 3. Money — the order and the floors

1. **Sell first** (surplus food, surplus animals, loot groups, prisoners; LATER ticked Others) — the
   proceeds fund the buys.
2. **Buy in priority order**: Food → Pack animals → Mounts → War mounts (LATER → Others, the
   price-book trading of §1.3.1, answering to `MinGoldAfterDeal`).
3. **Floors**:
   - **[step 15]** The player's own rows (touched rows and hires — and since step 16 recruits, §2.8) come before the chain:
     the steward's buys leave their gold first (§1.1, the live re-plan).
   - `MinGoldAfterDeal` (default **1000**): no purchase takes the purse below this.
   - `MinGoldForHorses` (default **5000**): no ANIMAL purchase (pack, mount, war mount) takes the
     purse below this. Food only answers to `MinGoldAfterDeal` — food outranks horses.
   - While the Full-autonomous steward acts alone, both rise to `AutonomousMinGold` (default **100000**, §6).
4. **Activation thresholds** **[Anton 2026.09.28, playtest round 4 — "war hourses to keep, default set it to 10, add another
   option min denari to have to start keeping war hourses 20k by default · same for the mouts … 5k · same for food keeping,
   have at least 2k before start auto managing · pack animals - have min 2k … i want it in such a way that the players turn
   it on and with the default settings they will be happy like that, as they become richer those start activating and
   helping them"]** (Core `Planning\JobThresholds`): a job ACTS only when the purse at planning — before this visit's deal —
   is at least its threshold: `FoodMinDenari` **2,000**, `PackAnimalsMinDenari` **2,000**, `MountsMinDenari` **5,000**
   (riding horses; the noble horses sold and the lame riding horses replaced go with them), `WarHorsesMinDenari` **20,000**
   (0 = always). Below it the steward's own side of the job does NOTHING — no buy, no sell (a surplus is kept too); lame
   horses of a waiting job stay counted in their role row. The rows stay in the table at 0 (`PlanRow.StartsAtDenari`), so
   the player can still buy or sell by hand; the plan's facts list the waiting jobs (`PlanFacts.Waiting`) and the section's
   overview says `starts at 20,000 denari`. **How they relate to the floors [decided: Claude, 2026.09.28 — step 20]**: the
   thresholds only SWITCH a job on; the floors (`MinGoldAfterDeal`, `MinGoldForHorses`, raised to `AutonomousMinGold` while
   autonomous) still CAP what an active job spends. So with 3,000 denari food acts and may spend down to 1,000, pack animals
   act but cannot buy (the 5,000 animal floor) — they still sell a surplus —, riding and war horses wait. The purse BEFORE the
   deal is the test: a ransom or a sale in the same visit never switches a job on halfway, and a live re-plan (same
   snapshot) keeps the same jobs on. The Full-autonomous steward answers to them too.
5. The market's own gold limits what the steward can sell there (villages especially) — the
   planner never proposes a sale the market cannot pay for.
   **[research 2026.09.27]** Vanilla would let the sale go through and simply pay no more than
   the market's gold — overselling loses the goods for nothing, so this rule matters.
   **[decided: Claude, 2026.09.27 — step 4]** The limit applies to the item sales (food, animals,
   loot); ransom gold comes from the game, not the market. The executor ransoms before it trades, so
   the ransom funds the buys.
6. **[research 2026.09.27]** Villages trade both ways (the "Buy products" screen also buys from
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

The Module reads a plain snapshot for Core: party members (with footmen/riders split; ~~upgrade-
ready troops and their required war-mount category~~ — gone in step 17), prisoners (with ransom value), inventory
(item id, count, weight, type, locked flag, **[step 17]** the modifier's price factor — below 1 = a lame or old horse),
market (item id, stock, buy price walk, sell price
walk), market gold, player gold, daily food consumption, settlement kind (town/village), whether
donating prisoners is allowed, the tavern (wanderers with hire price and wage; the mercenary
troop, count on offer, price and wage — **[step 15]** and for each, whether he rides (`IsMounted`)), the party size limit
(shown, never a block) and room for companions; **[step 16]** the troops (§2.8: per troop type the men held and wounded,
whether they may be dismissed, the volunteers on offer to the player, the price and wage per man, `IsMounted`). Core
returns a **StewardPlan**: rows (item or troop, change,
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
to run them: donations and ransoms (the ransom funds the buys), **[step 16]** the dismissals, every sale, every purchase
(the walk's own order, grouped by item category — which never changes a price, since a town's price walks per category),
then the wanderers, then the mercenaries, **[step 16]** then the recruits (§2.8); each with the stack (item + modifier) /
troop / hero, the count and the expected unit prices.

**[decided: Claude, 2026.09.27 — step 6]** How the executor behaves (`Module\Adapter\PlanExecutor`, Core `Execution\`):
- Every unit is checked again at the click, the way the plan walked it: its live price (the trade logic's own) must
  pass its row's limit (max buy / min sell), a sale must fit the market's remaining gold (gross sales, the planner's
  rule), a purchase the purse. What no longer fits — sold out, no longer held, locked since (a sale whose lock guards it: armour &
  weapons; food and animals only with `LocksProtectFoodAndHorses` — §2.6), no access any more, no
  room in the dungeon, no companion slot — is skipped and logged (the party size limit is no reason, §2.7); the rest goes through. The money
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
  them); the log shows the difference. Mercenaries: what is still on offer, capped by the purse (never by the party size limit).
- Every run is logged — gold before, each transaction with its real unit prices and its drift from the plan, gold
  after — and the player gets a one-line summary.

---

## 6. When the window appears

- **Settlement menu entry**: "Party Steward" in the town menu and the village menu — always
  there when the mod is enabled — and in War Sails' port menu (below).
- **Docking into a port (War Sails)** **[Anton 2026.09.28, playtest round 3]**: a party that sails into a town lands in
  War Sails' port menu, never the town menu (RESEARCH §18) — so the port counts as arriving: the arrival popup (or the
  Full-autonomous run) comes on docking, once per visit, with the town's rules (`AutoPopupOnTownEnter`, the closed-market
  and empty-plan rules); walking on into the town, or back to the port, is the same visit. The port menu gets the "Party
  Steward" entry right after its Trade (the port trades with the town's own market and access — RESEARCH §18). The leave
  warning already guards the port's "Set sail" — at sea the town menu has no Leave, so that is the only way out. Soft
  dependency: the menu is looked up by id when it opens; without War Sails nothing of this runs.
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
  - **No popup at a closed market or on an empty plan** **[decided: Claude, 2026.09.28 — step 12, playtest round 1]**: the
    arrival popup opens only when the game lets the player trade here, the plan has at least one row, and — with
    `PopupOnlyWithChanges` — something moves (Core `Presentation\ArrivalPopup`). A closed market wins even with
    `PopupOnlyWithChanges` off: the window could only say why it is closed. The menu entry always opens the window. (Round
    1's Hiblet — a village with nothing on offer — had logged "nothing to suggest - not opened"; the window seen two
    seconds later came from the Party Steward menu entry, the only door that did not name itself in the log then. Every
    open now logs its door.)
  - The warning's buttons: **Review** opens the window (also from War Sails' port); **Leave anyway** carries out the
    leave the player clicked, the vanilla way (the game's leaving incidents roll then, not on the click that asked).
    Escape on the question = Leave anyway — the player had clicked Leave. Opening the window at all (even just Close)
    counts as reviewed for the visit.
  - **The window ignores AutonomousMinGold**: with the autonomous steward on, the menu entry still opens the normal
    window with the normal floors — the player's own hand. Only the steward's own runs answer to the autonomous floor.
  - **The autonomous plan never even lists the tavern** (not only "never hires"): nothing to hire, nothing to report.
    **[step 16]** Nor the troops section (§2.8): the steward never recruits or dismisses by itself.
  - `AutonomousMinGold` sits in the **Money** group with the other floors; its range tops out at 10,000,000.
  - **The report** is one message line — jobs joined by ` · `, as the example above, in the fonts' glyphs (en-dash minus,
    `»`) — plus the trouble line `Steward: 1 cut short, 2 skipped — see smart_steward.log` (or `nothing was done` when the
    run could not start). The counts are the executor's real ones, not the plan's. The jobs: food (with the kinds
    bought), mounts (the whole horses section: pack, riding, war, noble, lame), armour & weapons, prisoners (ransomed / donated,
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
| General | LocksProtectFoodAndHorses | false | — | "Locks protect food & horses": off = locked food, pack animals and mounts are managed like the rest (counted, sold as surplus); on = left alone like locked armour & weapons (§2.6) **[Anton 2026.09.28, playtest round 2]** |
| Money | MinGoldAfterDeal | 1000 | 0–1,000,000 | purse floor for all purchases |
| Money | MinGoldForHorses | 5000 | 0–1,000,000 | purse floor for animal purchases |
| Money | AutonomousMinGold | 100000 | 0–10,000,000 | purse floor while autonomous: both floors above rise to it (§6) **[Anton 2026.09.27]** |
| Food | FoodEnabled | true | — | manage food |
| Food | FoodMinDenari | 2000 | 0–1,000,000 | "Manage food from (denari)": the purse before the deal food needs to be managed at all — below it the steward neither buys nor sells food (0 = always; §3 activation thresholds) **[Anton 2026.09.28, round 4]** |
| Food | FoodDays | 40 | 1–365 | "Keep food for (days)": days of food kept for the party after the deal, at the game's own rate (≈ 2 per man at vanilla's) — replaces `FoodPerMan` (2.0, 0.1–10: food units per eater; an old file's value converts once, × 20) **[Anton 2026.09.28]** |
| Food | FoodCountPrisoners | true | — | prisoners count as eaters (half each, like the game) |
| Food | FoodStrategy | Balanced | Balanced / Cheapest | Balanced (variety first) or Cheapest |
| Food | SellFoodSurplus | true | — | sell food above target + tolerance |
| Food | FoodSurplusTolerancePercent | 25 | 0–500 | how far above target before selling |
| Prices | BuyPriceMultiplier | 1.2 | 0.1–10 | final max buy = base × this **[Anton]** |
| Prices | SellPriceMultiplier | 0.8 | 0–10 | final min sell = base × this **[Anton]** |
| Prices | AutoFillFoodPrices | true | — | placeholder = average price for food |
| Prices | AutoFillPackAndMountPrices | true | — | … for pack animals and riding mounts |
| Prices | AutoFillWarMountPrices | false | — | … for war horses (off: trader's cheat sheet); noble horses always show their sell average (sell only, step 17) |
| Prices | PriceBook | {} | bases 0–1,000,000 | per item id: buy tick, buy base, sell tick, sell base (LATER: hold-up-to for Others) — only the player's changes (file + Prices tab, not MCM) |
| Pack | PackAnimalsEnabled | true | — | manage pack animals |
| Pack | PackAnimalsMinDenari | 2000 | 0–1,000,000 | "Manage pack animals from (denari)": the purse before the deal pack animals need (§3) **[Anton 2026.09.28, round 4]** |
| Pack | PackAnimalsTarget | 10 | 0–500 | pack animals to keep |
| Pack | PackAnimalMaxPrice | 300 | 0–100,000 | role cap: never pay more per pack animal (0 = none; NOT scaled by the multiplier) |
| Pack | SellPackAnimalSurplus | true | — | sell above target, most expensive first |
| Mounts | MountsEnabled | true | — | manage riding mounts for footmen |
| Mounts | MountsMinDenari | 5000 | 0–1,000,000 | "Manage riding horses from (denari)": the purse before the deal riding horses need — noble horses sold and lame riding horses replaced only then too (§3) **[Anton 2026.09.28, round 4]** |
| Mounts | MountsPer100Footmen | 110 | 0–300 | horses kept per 100 footmen — the war horses kept count among them, riding horses fill the rest **[Anton 2026.09.28, step 17]** |
| Mounts | MountMaxPrice | 500 | 0–100,000 | role cap: never pay more for a footman's mount (0 = none; not scaled) |
| Mounts | SellMountSurplus | true | — | sell above target, most expensive first |
| Mounts | SellNobleHorses | true | — | "Sell noble horses": never bought; sold (at ≥ min sell) unless LOCKED — a lock always keeps one **[Anton 2026.09.28, step 17]** |
| Mounts | ReplaceLameHorses | true | — | "Replace lame horses": sell the badly modified (lame, old) horses and pack animals and buy healthy ones; off = kept and counted **[Anton 2026.09.28, step 17]** |
| War mounts | WarMountsEnabled | true | — | manage war horses (off: a war horse is a plain riding horse) |
| War mounts | WarHorsesMinDenari | 20000 | 0–1,000,000 | "Manage war horses from (denari)": the purse before the deal war horses need (§3) **[Anton 2026.09.28, round 4]** |
| War mounts | WarMountsToKeep | 10 | 0–500 | "War horses to keep" (default 0 until round 4 — **[Anton 2026.09.28]**: 10): a plain number of `war_horse` mounts, bought up to it and sold above it; they count toward the horses per 100 footmen — replaces WarMountsHorseTarget, WarMountsWarHorseTarget, WarMountsExtra and WarMountsCountAsMounts (retired, logged once) **[Anton 2026.09.28, step 17]** |
| War mounts | WarMountMaxPrice | 2000 | 0–100,000 | role cap: never pay more per war horse (0 = none; not scaled) |
| War mounts | SellWarMountSurplus | true | — | sell above the number to keep, most expensive first |
| Prisoners | RansomPrisoners | true | — | ransom prisoners in towns |
| Prisoners | RansomHeroPrisoners | false | — | include lords |
| Prisoners | DonatePrisonersWhenPossible | false | — | donate to own garrison (influence) instead |
| Loot | SellLoot | false | — | sell other items |
| Loot | SellLootEquipment | true | — | weapons, armour, shields, ammo |
| Loot | SellLootMaxItemValue | 0 | 0–1,000,000 | never auto-sell items worth more per unit (0 = no cap) |
| Loot | SellLootOrder | Cheapest | Cheapest / LowestPricePerKg / MostExpensive | order within a loot group: Cheapest / LowestPricePerKg / MostExpensive — replaces `SellLootMassFirst` **[Anton 2026.09.27]** |
| Tavern | ShowTavern | true | — | show the tavern section in towns |
| Tavern | ShowWanderers | true | — | list wanderers for hire |
| Tavern | ShowMercenaries | true | — | list the tavern's mercenaries |
| Tavern | ShowTroops | true | — | "Show the troops": the troops section in towns and villages — recruits on offer, then your troops (§2.8) **[Anton 2026.09.28, playtest round 3]** |

Keys are final names for the settings file and code; UI labels can be friendlier.

**Ranges** **[decided: Claude, 2026.09.27 — step 5]** (only the two multipliers were Anton's): wide enough for any
play style, narrow enough that a typo cannot break a plan — gold up to one million, a role price cap up to
100,000, `FoodPerMan` at least 0.1 (0 would sell every ration as surplus; since 2026.09.28 `FoodDays` at least 1 for the same
reason). Decimal settings keep 2 places (MCM's
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
  - Core: `SettingsRegistry` (the §7 keys in table order — 47 since step 17: 46 scalars + the price book; 51 since step 20's
    four activation thresholds; the
    prisoner list is gone since step 12, the four upgrade-horse keys since step 17), `SettingsFile` (text in, text out),
    `SettingsService` (the live values). Module: `SettingsHost`
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
- **The window's remembered state** **[decided: Claude, 2026.09.28 — PLAN step 18]**: which Suggestion sections are folded
  (§1.1) lives in its OWN small file beside the settings, `Configs\SmartSteward\window_state.json` (Core `WindowState`,
  Module `UI\WindowStateHost`) — `// comments` explaining it, then `"CollapsedSections": ["Food", "Mounts"]` (names: Tavern,
  Troops, Food, Mounts, ArmourAndWeapons, Prisoners); written on every fold or unfold (beside and swapped in, like the
  settings), read at the first window and again whenever it changed on disk. Why not a key in settings.json: a fold is not
  a setting — settings.json is generated from the §7 registry, a change there raises the service's `Changed` and re-plans
  the open window, shows in MCM and the Instructions tab, and an unknown key is a logged problem with a `.bak`; a fold must
  do none of that, and writing it on every click must never put the settings file at risk. Deleting settings.json keeps the
  folds; deleting window_state.json unfolds everything. Unreadable, a wrong shape or unknown names → every section unfolded
  (unknown names dropped), one log line; never an exception. Never in the save, never in MCM.

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
