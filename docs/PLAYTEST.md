# Playtest checklists

Short lists for Anton, one section per build step that needs the game. The log is
`Documents\Mount and Blade II Bannerlord\Configs\SmartSteward\smart_steward.log` — send it with any report.

## Step 6 — the debug door (removed in step 8 — run these checks through the window's Do it)

Deploy with `tools\deploy.ps1` (game closed), enable "Smart Steward (dev)", load a save. For a lively test, turn on
`SellLoot` in Mod Options (or settings.json) first.

1. **Enter a town** with some food, horses, loot and prisoners. The town menu has **Party Steward (debug)** right
   after Trade. Click it.
2. **Read the popup**: gold now -> after, then the rows it would change (food, mounts, armour & weapons, prisoners).
   Does it make sense for your party? (Food toward 40 days (~2 per man), pack animals toward 10, a horse per footman + 10%,
   upgrade horses for troops ready to upgrade, loot sold cheapest first, prisoners ransomed.)
3. **Compare prices**: press **Cancel**, open the vanilla Trade screen, and check a few first-unit prices against the
   popup's (the log also has a `price self-check` line — it should say *all equal*).
4. **Execute**: open the door again, press **Execute**. A message sums it up ("Steward: N of N done…").
   Check: gold changed by about the popup's amount; the inventory has the new food/horses and lost the sold loot;
   the prisoners are gone and the gold came in; locked armour and weapons were not sold. Any "cut short" or "skipped"? The log
   says why.
5. **Tavern**: in a town with wanderers or mercenaries, use **Party Steward (debug, + tavern hires)** — it adds the
   first wanderer and the whole mercenary band. Execute, then check the clan screen (new companion) and the party
   (mercenaries, gold).
6. **Village**: repeat 1–4 in a village (a horse ranch is best). Prices there do not move as you buy.
7. **Edge cases if you have time**: a town at war with you or where you are a criminal (no trade rows expected);
   a looted village (no entry); the same door twice in a row (the second plan should be almost empty).

## Step 7 — the window

Deploy (game closed), load a save, enter a town. The town menu has **Party Steward** right after Trade. If anything
looks broken, press Escape — and send the log.

1. **It opens and closes.** Click Party Steward: a dark window "Party Steward — <town>" with three tabs. Escape
   closes it; so does **Close**. Open it again — same plan. Nothing in the game moved.
2. **Suggestion tab — read it.** At the very top: `Gold 12,400 » 10,930 (–1,470)`, green if the deal earns, red if
   it costs. Sections in this order: Tavern, Food, Mounts, Armour & weapons, Prisoners. Columns line up: Mine | [–]
   change [+] | Result | Price | Market | Item | Type. Buys green, sells red, untouched grey.
3. **Click the buttons.** [+] / [–] move a row by 1, **Shift**-click by 5, **Ctrl**-click all the way (and stop at
   zero first). Every click re-prices the row, the header line and the footer. The round ⟲ icon appears on an edited
   row and puts it back; **Reset all** (bottom left) resets everything.
4. **Greyed buttons say why.** Hover a greyed [+] or [–] (e.g. [+] on a loot row, [–] on a wanderer): a tooltip
   gives the reason. Hover **Do it** when it is grey.
5. **Mount rows.** Pack animals / Riding mounts / Upgrade horses show one row each; the small arrow before the name
   opens the per-horse breakdown.
6. **Tavern names.** A wanderer's or the mercenaries' name is gold — click it: the Encyclopedia opens OVER the
   window. Close it (its X or Escape): you are back in the Party Steward window, which still answers the keyboard
   (Escape closes it; Shift-click still steps by 5). This is the part most worth checking.
7. **Footer.** Spent / earned, food now » after (~days), weight change; red lines when you go below your gold floors
   (Do it still works) or cannot afford the deal (Do it greys out).
8. **Do it.** Hire a wanderer or buy some food by hand, press Do it. A message and a gold line under the footer say
   what happened; the table re-plans from the new state (usually almost empty now). Check gold, inventory, party.
9. **Prices tab.** Food and Horses (Pack animals · Mounts · War mounts), each group folds with a click on its name.
   Untick Buy on grain; type a max buy base (the grey average disappears, the final after » changes); ⟲ clears it.
   Change the buy multiplier at the top — every final follows. Back on Suggestion: the plan follows your changes, but
   rows you edited by hand keep your number (as far as the new limits allow).
10. **Instructions tab.** Every setting from Mod Options, grouped the same way; hover a name for its help. Tick
   Sell loot, change Keep food for (days), cycle Food buying. In the Prisoners group, untick a prisoner under "Prisoners to
   ransom" — back on Suggestion his row stays at 0. Mod Options (if you have MCM) and settings.json show the same values.
11. **Village.** Repeat 2–3 and 8 in a village (no tavern, no prisoners there).
12. **Look.** Anything clipped, overlapping, unreadable, or in a wrong colour? A screenshot helps most.

## Step 8 — triggers & autonomy

Deploy (game closed), load a save made OUTSIDE a town — a save loaded inside a town is no arrival (no popup there, by
design). Defaults: the window pops up in towns and villages, only when there is something to suggest; the leave
question is on; the autonomous steward is off. Mod Options (or settings.json) change them.

1. **Arrival popup.** Ride into a town where the steward has work (low food, footmen without horses, prisoners). Once
   the town menu is up, the Party Steward window opens by itself. Close it, visit the Trade screen and the tavern
   district and come back: it does NOT open again this visit. The log says `arrival popup at …`.
2. **Timing.** Over several arrivals: did it ever open on top of an incident ("As you enter…"), a quest conversation, a
   tutorial or a message box? It should wait until they are gone. Report any bad moment.
3. **Nothing to do → nothing opens.** Arrive where the plan is empty: no window. Untick "Only open with suggestions":
   the next arrival opens it anyway.
4. **Village and the switches.** Same in a village. Untick "Open on entering a village": no popup there, towns still
   pop. A looted village: nothing at all.
5. **Leave question.** Untick "Open on entering a town", enter a town with suggestions and click Leave: *"Your steward
   has suggestions you haven't looked at."* **Review** opens the window (Leave then leaves without asking); **Leave
   anyway** leaves at once (Escape does the same). No incident should pop while the question is up.
6. **No question when looked at, or nothing to say.** Open Party Steward from the menu (even just Close), then
   Leave: no question. A town where the steward has nothing to suggest: no question.
7. **Ships (War Sails).** In a port town go to the port and **Set sail** with unreviewed suggestions: the same
   question (Review opens the window right there). At a coastal village, **Set Sail** asks too.
8. **Full-autonomous steward.** Tick General → **Full-autonomous steward**. Its floor, Money → **Keep while
   autonomous**, is 100,000: below it the steward only sells and ransoms (tick Sell loot to see it); set it to e.g.
   1,000 to watch it buy. Enter a town: no window, no question on leaving — just a line in the message log (bottom
   left) like `Steward at Sargot: food +24 (2 kinds) –310 · mounts +3 –540 · armour & weapons 41 sold +2,130 ·
   prisoners 12 ransomed +980 · gold 312,400 » 314,660`. Check gold and inventory match. Never a tavern hire.
   Nothing to do → no line. The Party Steward entry still opens the window.
9. **Old settings file.** A settings.json that still said `"AutoExecute"` now says `"AutonomousSteward"` with the
   same value (the log: `"AutoExecute" is now AutonomousSteward`).
10. **Master switch.** Untick General → Steward on: no menu entry, no popup, no question, no autonomy. Tick it back.
11. **The debug entries are gone** from the town and village menus.

## Step 9 — the review pass

Deploy (game closed). Mostly invisible fixes — these points check nothing got worse.

1. **Big clicks.** After a few battles (lots of loot, Loot → Sell loot ticked), open Party Steward in a town and click
   [–]/[+] on the loot, food and horse rows quickly: each click answers at once, no stutter.
2. **▸ breakdown.** Open a horse row's ▸: its kinds show; click [+]/[–] on that row while it is open — the lines follow.
   Close it and open it again: still right.
3. **Autonomous floor.** With the Full-autonomous steward on, set Money → Keep while autonomous a little below your
   gold (e.g. gold − 500) and enter a town where it wants horses or food: your gold never ends below that number.
4. **Log.** After a session, `smart_steward.log` has no `ERROR` lines (search for it) — send it if it does.

## Round 1 fixes (step 12)

Deploy (game closed). Send `smart_steward.log` AND the game's newest `C:\ProgramData\Mount and Blade II
Bannerlord\logs\rgl_log_*.txt` with any report.

1. **Save → load, no restart (the "cannot enter towns" bug).** Load save A. Visit a village, open Party Steward, close
   it; visit a town, open it again. Now load save B from the in-game menu (Escape → Load) — no restart. Enter a town,
   then a village: both enter normally. Repeat the whole round twice (B → A, then again through Exit to main menu →
   Load). The log has `steward state reset (session N)` at every load.
   **If it happens again:** before anything else, tap **Left Alt** once and click the town again. A click with Left Alt
   held asks for a parley instead of travelling — the game then says "Your clan tier is not high enough to request a
   meeting" on hostile towns and does nothing at all elsewhere; a Left Alt stuck after Alt+Tab looks exactly like the
   bug. Say whether the tap cured it. The log says `the map's follow modifier … has read as held` when the game believes
   Alt is down.
2. **No popup with nothing to do.** Ride into a village that sells nothing (the Trade/Buy products option greyed) or a
   town where you cannot trade: no window pops up, even with "Only open with suggestions" unticked. The log says `no
   popup at … - the market is closed (arrival popup)`. A place with suggestions still pops up. Each `opened at …` line
   now ends with its door: `(arrival popup)`, `(the Party Steward menu entry)` or `(Review on the leave question)`.
3. **Closed market says why.** In such a village or town open Party Steward from the menu: instead of an empty table
   it says `Market closed: …` in the game's words (e.g. "There are no available products right now.", "You cannot
   trade with a hostile village."). In a town where you cannot trade but hold prisoners to ransom, the line sits at the
   top right and the table shows below it.
4. **Upgrade horses per kind.** Your old settings.json's "Upgrade horses to keep" is gone: the log says
   `"WarMountsManualTarget" (10) is retired and ignored`, and Instructions → War mounts shows **Horses for upgrades** and
   **War horses for upgrades** (both -1 = automatic), **Spare upgrade horses (each kind)**, and under them the live line
   `Troops ready to upgrade now: N for a horse, M for a war horse` — compare with the party screen. Set War horses for
   upgrades = 10 and look at the Suggestion tab: the war-horse row aims at 10, the plain-horse row stays at its automatic
   count. Are the three labels clear without reading the tooltips?
5. **Prisoners: all or none.** Instructions → Prisoners has no "Prisoners to ransom" list any more. In a town with
   prisoners every troop is proposed for ransom; lords only with "Include lords". An old settings.json's
   `PrisonersExcluded` is ignored (the log says `is retired and ignored` once).
6. **Prices tab.** The item name is the first column, lined up under the Pack animals / Mounts / War mounts
   sub-headers; Buy, Max buy, Sell, Min sell and ⟲ follow. Long names fit? Nothing overlaps?

## Round 2 fixes (step 13)

Deploy (game closed). Send `smart_steward.log` with any report.

1. **Locked food and horses are managed.** In the inventory lock a food you hold plenty of and a spare horse or mule
   (the lock button on the item's row). Ride into a town where both are surplus (food above ~2.5 per man, more pack
   animals than "Pack animals to keep", or more riding horses than your footmen need). The Suggestion tab sells them —
   the most-held food first, the dearest animal first — with no "(+N locked)" beside Mine. Press **Do it**: they are
   sold (the log says `selling … although locked`), and gold and the market's goods change as on any sale. The lock
   stays in the game's list, so the same item bought back shows locked again — fine.
2. **The switch.** Instructions → General → **Locks protect food & horses** on (or MCM, or settings.json
   `"LocksProtectFoodAndHorses": true`). Open the steward again with the same locked food and horse: they are counted
   (no food or horse bought for them) but never sold, not even with [-] — their rows show `(+N locked)`, the way it was
   before round 2. Turn it off again afterwards.
3. **Armour and weapons keep their lock** — both ways of the switch. With **Sell loot** on, lock a piece of armour or a
   weapon: its group row shows `(+1 locked)`, the steward never proposes it, [-] all stops before it, and **Do it**
   leaves it in the inventory.

## Round 3 — small (step 14)

Deploy (game closed). Send `smart_steward.log` with any report.

1. **Party limit is information.** In a town with a mercenary band, fill the party close to its limit and open Party
   Steward. The footer shows `Party 94/96`; [+] (Shift, Ctrl) on the mercenaries goes past the limit — up to the whole band
   — and `Party 104/96` turns red. A wanderer can be ticked with a full party too (only the companion limit stops him).
   **Do it** hires them all; the party screen shows the party over its limit, like vanilla after a tavern hire.
2. **Docking into a port (War Sails).** Sail into a coastal town (ships in the party) and dock. The port menu ("You are at
   the port.") has **Party Steward** right after Trade, and the arrival popup comes on docking (same rules as a town:
   only with suggestions, never at a closed market). Walk into the town and back: no second popup. Set sail without
   opening it: the leave question comes. With the Full-autonomous steward on, docking runs it once (message-log report).
   Without War Sails nothing changes (the log says nothing about a port).
3. **Weight line.** The footer's second line reads like `Weight 1,000 +120 kg » 1,120 kg · capacity 1,500` — with ships
   `capacity land 1,500 / sea 1,000`, plus `(1,620 kg at sea)` when horses make the load at sea heavier. Buy pack animals:
   the land capacity climbs 100 each (more with pack perks); hire men: +20 each on land and at sea; sell mounts: −20 each.
   Push it over (Ctrl [+] on food, or dismiss the pack animals): `+120 over on land` / `+620 over at sea` shows in red and
   follows every click. Compare the numbers now with the party screen's capacity tooltip (land) — they should match.
4. **Close.** The button next to Do it says **Close** (Escape still closes too).
5. **Wanderer pages.** In a town you never visited the tavern district of, open Party Steward: the message log says
   "You've learned about …" for each listed wanderer. Click a wanderer's name: the Encyclopedia page is complete (no
   "???"). The mercenary troop's name opens its unit page as before.

## Round 3 — live re-plan (step 15)

Deploy (game closed). Send `smart_steward.log` with any report — every re-plan writes a line
`re-planned for the party after the deal: food target … for … eaters (was …), … footmen, riding target … (was …)`.

1. **Hires feed and mount themselves.** In a town with a mercenary band of foot soldiers, open Party Steward and note the
   Food section's "target N for M eaters" and the Mounts section's "men on foot · riding target". Ctrl [+] the band: both
   targets grow at once (2 food and 1.1 horses per man hired), and the food and riding rows buy more. [-] the band back:
   they shrink back. A mounted band (cavalry) grows the food only. A wanderer: +1 eater, +1 footman unless he rides.
2. **Prisoners.** [+] on a prisoner row (keep them instead of ransoming): the food target grows by half a man each; ransom
   them again: it shrinks.
3. **Your rows stay yours.** Change a food row by hand (its ⟲ appears — that marks it as yours), then hire: your row keeps
   its number, the other food rows make up the rest. Click your row back to where it was: the ⟲ stays until you click it.
   ⟲ hands the row back — it follows the party again. **Reset all** gives the first plan back exactly.
4. **The promise.** Hire men, keep or ransom prisoners, then **Do it**: the window plans afresh — the food and horse rows
   should show nothing new to buy (food within its surplus tolerance). Check the party screen: enough horses for the
   footmen (+10%), the food for your days.
5. **Money.** With a thin purse, hire many men: the steward's own food and horses give way to the hires (your hires come
   first), the floors still hold where they can. If the steward's food sales shrink with the bigger party so much that the
   deal no longer pays, the click stops at the most men it pays.
6. **Food in days.** The Instructions tab's Food group reads `Keep food for (days) [40] days (~2.0 per soul)`; type 60: the
   bracket shows ~3.0 (less with Warrior's Diet — it is your party's own rate), and the Suggestion tab's food target follows.
   An old `settings.json` with `"FoodPerMan": 2.5` comes back as `"FoodDays": 50` (one log line, no `.bak`).
7. **Speed.** Clicks on the mercenaries or a prisoner row should feel as quick as any other click, even on a big party.

## Round 3 — troops (step 16)

Deployed at the end of step 16 (the game was closed). Send `smart_steward.log` with any report — the snapshot line ends
with `troops N types, on offer [5 imperial_recruit at 20, …], in the party … men (… wounded)`, every Do it logs
`Dismiss …` / `Recruit …` lines.

1. **What is listed.** In a town, open Party Steward: right after the Tavern come **Recruits on offer** (the troops the
   notables offer YOU — compare with the town's "Recruit troops" screen: the same types and counts you could click there,
   locked slots left out) and **Your troops** (every other regular troop type in your party, none of your companions). Mine =
   how many you have; Market = how many are on offer; the price at 0 is the price per man (same as the recruit screen).
   Every row starts at 0. Names in gold open the unit's Encyclopedia page.
2. **A village.** Do the same in a village: its headman's and rural notables' volunteers are listed. In a village with
   nothing on sale (market closed), the table still shows the troops, "Market closed: …" above it.
3. **Recruit past the limit.** Ctrl [+] a recruit row: it stops at what is on offer; Party goes past its limit (`Party
   104/96` in red) — nothing blocks it. With a thin purse [+] greys out ("You do not have the gold for it.").
4. **Dismiss.** [−] on a row of Your troops dismisses (Ctrl: all of them; Shift: 5); on a recruit row [−] dismisses your own
   men of that type. The row's detail says how many are wounded — they go first, so the weight line's capacity drops only
   after them. Party and the food/mount rows follow at once.
5. **The live re-plan.** Recruit foot soldiers: the food target and "men on foot · riding target" grow, the steward buys more.
   ~~Dismiss men who are ready to upgrade: the upgrade-horse row's "needed for upgrades" drops …~~ — step 17 removed the
   upgrade rows: see "Horses simplified" below (the header now reads "men on foot · horses to keep").
6. **Do it.** Recruit some, dismiss some, **Do it**: the party screen shows the new men and the dismissed ones gone (wounded
   first); the recruit screen shows those slots empty; the gold dropped by the recruits' price; your Leadership gains XP
   for the recruits (as from vanilla's recruit screen — check the character screen). The window plans afresh: nothing new for food and
   horses.
7. **Settings.** Instructions tab (Tavern group) or MCM: **Show the troops** off hides the section; the Full-autonomous
   steward never recruits or dismisses.
8. **Speed.** A big party lists many troop types: clicks should feel as quick as before.

## Horses simplified (step 17)

Anton 2026.09.28: no upgrade counting — a plain number of war horses, noble horses sold unless locked, lame horses
replaced. **Not deployed at the end of step 17** (the game was running): run `tools\deploy.ps1` with the game closed first.
Send `smart_steward.log` with any report — the plan's facts line reads `footmen F, horses to keep T (riding target R, war
horses W)`, the snapshot line `lame/old horses held N`, a re-plan `… footmen, horses to keep T (was …), riding target R,
war horses W`.

1. **The settings.** Instructions tab: the Mounts group has **Sell noble horses** (on) and **Replace lame horses** (on);
   the War mounts group has **War horses to keep** (0) — "Horses for upgrades", "War horses for upgrades", "Spare upgrade
   horses" and "Upgrade horses carry footmen" are gone, and so is the "Troops ready to upgrade now" line. After the boxes of
   "Horses per 100 footmen" and "War horses to keep" a grey note reads `(110 horses = 100 riding + 10 war, for 100
   footmen)` — type another number in either: the note follows at once. Mod Options shows the same (46 settings).
2. **An old settings file.** If your `settings.json` still had `WarMountsWarHorseTarget` etc., the log says once that each
   is retired and ignored; the rewritten file no longer has them and War horses to keep starts at 0 — set it again.
3. **Anton's example.** With ~100 footmen and War horses to keep = 10, a town with horses and war horses on offer: the
   Mounts section header reads `100 men on foot · 110 horses to keep`; Riding mounts "target 100", War horses "target 10".
   Do it, then open Party Steward again: nothing new to buy or sell for the horses.
4. **War horses at 0 (the default).** Holding war horses you did not lock: the War horses row proposes selling them (the
   dearest first) and the riding row buys cheaper horses for the footmen — set War horses to keep first if you want them.
   **Sell surplus war horses** off: they are kept and carry footmen (the riding row buys fewer).
5. **Upgrading settles by itself.** Keep 10 war horses, upgrade 10 men into cavalry in the party screen (they take the war
   horses), then enter a town: the steward buys 10 war horses back and sells the riding horses the smaller band of
   footmen no longer needs, in the same visit.
6. **Noble horses.** Hold a noble horse (a t3 culture horse, a noble camel…): a **Noble horses** row (sell only — [+] greys
   "Only ever sold…") proposes selling it, never below its min sell price (Prices tab → Horses → **Noble horses — sell
   only**: no buy column, a grey average in the sell box). Lock it in the inventory: it stays, whatever "Locks protect food
   & horses" says, and it counts as a horse for a footman. **Sell noble horses** off: kept and counted. A market selling
   noble horses: never bought, not even cheap.
7. **Lame horses.** Get a lame horse (your own horse "turned into a Lame …" in battle, then unequip it) or an old one (a
   companion's horse): a **Lame horses** row sells it ("sold - healthy ones take their place") and the riding (or pack)
   row buys a healthy one in the same visit. A lame horse fetches about a tenth of a healthy one — the row still sells it.
   **Replace lame horses** off: no row, it is kept and counted as a riding horse. A market with lame horses: never bought.
8. **Do it and the log.** The executor sells the noble and lame horses by their own stacks (`Sell 1 x t3_…|` / `Sell 1 x
   …|lame_horse`); a noble horse locked after the window opened is skipped (the log: `stopped: Locked`).

## Step 18 — folded sections, the land / sea lines, the herd line, troop tiers

Anton 2026.09.28. **Not deployed at the end of step 18** (the game was running): run `tools\deploy.ps1` with the game closed
first (the step-17 deploy line is still open too). Send `smart_steward.log` with any report — a fold logs `folded Food` /
`unfolded Food`, the troops line `troops line [-] One: troops:… 0 -> -1`, the plan's footer line `horses H of R before the
herd slows (herd … vs … men; …)`, the snapshot line `livestock N` and each offered troop's tier (`… T3 at 80`).

1. **Fold a section.** Click a section's name (or its small ▸/▾ icon): the rows hide and ONE line says what the section
   will do with its gold — e.g. Food `+29 (5 kinds) –510 · 64 » 71 days`, Armour & weapons `41 sold +2,132 · –380 kg`,
   Prisoners `12 ransomed +980`; nothing queued → `no change · 64 days`, `nothing sold`, `nobody hired`… Click again: the
   rows are back. The hover tooltip says it is remembered.
2. **Live line.** Fold Food, then hire mercenaries (Tavern unfolded): the folded Food line changes at once (more eaters,
   more food). Do it and Reset all keep the folds.
3. **Remembered.** Fold Food and Mounts, close the window, open it in another town, then quit and restart the game: both
   stay folded until you unfold them. `Configs\SmartSteward\window_state.json` lists them (`"CollapsedSections": ["Food",
   "Mounts"]`); delete the file and every section unfolds. Nothing is in the save, nothing in Mod Options.
4. **The troops fold together.** Fold "Recruits on offer" (or "Your troops"): both halves become one **Troops** line.
5. **The weight on two lines.** The footer reads `Land:  weight 1,000 +120 » 1,120 kg · capacity 1,500 » 1,900` — buy pack
   animals or hire men and the capacity after moves; `+720 over` in red when the load after is over it. With ships (War
   Sails) a second line `Sea:  weight … · capacity …` — pack animals bought raise the land capacity but add weight at sea.
   No ships: no sea line (the warnings move up). Compare the numbers with the party screen's weight tooltip.
6. **The herd line.** After the land line: `Horses 110 / 200 before the herd slows you` (100 footmen, 110 horses). Buy (or
   add) horses and pack animals until the first number passes the second: the line turns red — and the party screen's speed
   tooltip should show a "Herding" penalty then, and none while it is not red (equal is still fine). Cows or sheep in the
   inventory take room too: `(20 livestock take room too)`. In an army, the whole army's men and animals count.
7. **Tiers.** Every troop row reads `T1 Vlandian Recruit`: **Your troops** lowest tier on top, **Recruits on offer**
   highest tier on top (say if you want them lowest first too). Compare a few tiers with the party screen's tier icons.
8. **The folded Troops line's [-] [+].** Fold Troops: the line carries `[–] [+]`. `[–]` dismisses from the lowest tier up
   (your own troops first, then the men of the types on offer), `[+]` recruits the highest tier on offer first; Shift = 5,
   Ctrl = all. The line says what it does: `dismissing 1 T1 Imperial Peasant, 1 T1 Vlandian Recruit · recruiting 2 T3
   Vlandian Footman –160`. Unfold: those rows carry the ⟲, and the food and horses followed. Do it: the same as dismissing
   and recruiting row by row. With too little gold for a T3, `[+]` takes a cheaper tier; greyed buttons say why on hover.
9. **Speed.** With several sections folded, clicks should feel at least as quick as before (folded rows are not refreshed).

## Step 20 — round 4, the Core under the old window (deployed 2026.09.28)

Step 20 built the round-4 rules; the NEW spreadsheet window is step 21 — until then the old window shows them as it can.
The log (`smart_steward.log`) has the rest.

1. **Settings carried over.** Open `settings.json` (or the Instructions tab / Mod Options): the four "Manage … from (denari)"
   thresholds, "Food buy / sell price multiplier" (2.0 / 0.5), "Horse buy / sell price multiplier" (your old 1.2 / 0.8), "Captured
   lords" (Keep) and "Other prisoners" (Ransom), "Sell other goods". The log says once how your old multipliers and prisoner
   switches were carried over (`… is now HorseBuyPriceMultiplier`, `… now PrisonerAction = "Ransom"`).
2. **Jobs switch on with the purse.** With under 2,000 denari the steward proposes no food or pack animals; under 5,000 no
   riding horses (nor sells noble horses); under 20,000 no war horses — the rows stay at 0 and you can still click them. Richer:
   they act. War horses to keep is 10 now.
3. **Food prices.** Grain at ~10 on average is bought up to ~20 (the Prices tab's food lines show `× 2`, horses `× 1.2`).
4. **Prisoners.** Lords are kept unless you pick Ransom or Donate; the others are ransomed. Donate in your kingdom's town
   (not your clan's): the dungeon fills most valuable first, the rest are ransomed; lords to donate that do not fit stay.
5. **Other.** The "Armour & weapons" section is now "Other" and has an **Other goods** row (wool, salt, pottery, jewelry…) —
   everything that is not food, a horse or gear, sold cheapest first. Lock a good in the inventory: it is never sold.
6. **Prices tab.** Inside each group the cheapest item is on top.
7. **Slowdown in the log.** Overload the party (buy goods past your capacity) and open the window: the log's footer line says
   `(land speed -12%, -0.40)` — compare with the party speed tooltip's "Overburdened" line.

## Step 21 — the spreadsheet (deployed 2026.09.28)

The round-4 window as approved in the mockup (docs/mockups/suggestion_v2_folded.png — with Market far LEFT and a Prisoners
column). Close to the mockup, not a copy: more polish rounds follow — say what reads badly. The log has every click.

1. **The look.** Open the window in a town: `Denari 69,358 » 89,189 (+19,831)` on top (the change green / red, influence small
   and green), columns Market · Item · Mine · Change · Result · Denari · Party · Prisoners · Land kg · Sea kg (Sea only with
   ships). Do the columns line up — heads, title lines, rows, the Total? Anything cut off at 1080p?
2. **Title lines are subtotals.** Each section's line carries its sums in every number column; Troops says `104/101` (red when
   over the limit), Food / Horses / Prisoners / Other their overview. The pinned **Total** under the table never scrolls.
3. **Folds.** First window (no window_state.json yet): Recruits, Your troops, Food and the prisoner rows folded, Horses and
   Other open, every ▸ closed. Click titles and ▸s (a horse role row, Other goods, Recruits, Your troops, the Troops title
   folds both); close and reopen, restart the game — the folds stay.
4. **Troops.** Recruits [+] takes the best tier on offer, [−] gives the last back; Your troops [−] drops the lowest tier
   (`dropping 2 T0 …`), [+] brings them back in reverse; Shift 5, Ctrl all. Open both: a type you hold that is also on offer
   shows under both, each side on its own (the other side's button greys with why). Names open the Encyclopedia.
5. **Prisoners.** Lords / Others: Keep | Ransom | Donate — the chosen one lit gold. Click another: the rows change at once,
   and the Instructions tab / Mod Options show the same choice. In a town where you may not donate, Donate is grey (hover
   says why). Open the prisoner rows: tiers, lowest first, lords last, names open the Encyclopedia.
6. **Cells.** Denari green in / red out, zeros blank; hover a Denari cell of a bought food: `8 × 30–33 = –252 · your max 60`.
   A food, horse or loot row that does not move shows no unit price (known deviation — say if you miss it).
7. **Footer.** Only the weight table: Land (and Sea with ships) × before, change, after, capacity, left (red when over),
   slowdown with the game's horse icon (the ship at sea) — compare with the party speed tooltip's "Overburdened"; warnings
   and the last result in the box beside it. The spent / earned / food / party / herd lines are gone.
8. **Words.** Every money text says denari (greyed buttons' reasons, warnings, the Do it message, the autonomous message
   line). The `Click ±1 · Shift ±5 · Ctrl all` hint is at the top of the Instructions tab now.
9. **Prices tab.** The top line: `Food buy × [2] sell × [0.5]   Horses buy × [1.2] sell × [0.8]` — type in each; the food lines
   follow the food pair, the horse lines the horse pair. Cheapest first inside each group.
10. **Speed.** Open Your troops with a big party and click fast: no stutter (the benchmark says 0–15 ms a click).

## Step 22 — round 5, the goals' Core under the step-21 window (deployed 2026.09.28)

The Goal COLUMN is step 23; this install already plans with the goals. What you can try now:

1. **A click on a food or pack / riding / war row is a standing order.** Click [+] on Grain in one town, leave, enter another
   town: Grain aims at the same Result there (the ⟲ shows on it). `settings.json` has it under `"Goals"` (`"food:grain": 60`).
   The ⟲ gives the row back to the steward (the key goes from the file). Reset all keeps the goals — only their own ⟲ clears them.
2. **The steward fills the rest.** Raise one food: the steward's other food rows shrink so the days goal still holds; lower
   it and they grow.
3. **Goals you set by hand** — a new Instructions group (and in Mod Options): "Your goals wait for the thresholds" (off),
   "Your goals keep the purse floors" (on), "Your goals obey the price limits" (on). With the floor on, a food [+] greys at
   `Always keep` (hover: "…below the floor your goals keep") — until now a hand edit went below the floor with a red flag.
4. **Below a threshold** (e.g. under 2,000 denari for food) a goal you set still buys; switch "wait for the thresholds" on and
   it waits (its buttons grey, hover says why).
5. **Weight, not kg.** The column heads read `Land weight` / `Sea weight`; the slowdown hover and "lowest price per weight"
   have no kg left. An old settings.json with `"LowestPricePerKg"` is read and rewritten as `"LowestPricePerWeight"`.
6. **Hand-edit settings.json**: put `"Goals": { "mounts:war": 15 }` in while the game runs, reopen the window — the war horses
   aim at 15. A junk key (`"mounts:noble": 3`) is dropped with a line in smart_steward.log (and a settings.json.bak).
