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
   Dismiss men who are ready to upgrade: the upgrade-horse row's "needed for upgrades" drops (the Instructions tab's "Troops
   ready to upgrade now" counts the party as it is, before the deal). Recruits whose upgrade needs a horse bring the upgrade-horse row if you keep a fixed number.
6. **Do it.** Recruit some, dismiss some, **Do it**: the party screen shows the new men and the dismissed ones gone (wounded
   first); the recruit screen shows those slots empty; the gold dropped by the recruits' price; your Leadership gains XP
   for the recruits (as from vanilla's recruit screen — check the character screen). The window plans afresh: nothing new for food and
   horses.
7. **Settings.** Instructions tab (Tavern group) or MCM: **Show the troops** off hides the section; the Full-autonomous
   steward never recruits or dismisses.
8. **Speed.** A big party lists many troop types: clicks should feel as quick as before.
