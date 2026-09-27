# Playtest checklists

Short lists for Anton, one section per build step that needs the game. The log is
`Documents\Mount and Blade II Bannerlord\Configs\SmartSteward\smart_steward.log` — send it with any report.

## Step 6 — the debug door (temporary, step 8 removes it)

Deploy with `tools\deploy.ps1` (game closed), enable "Smart Steward (dev)", load a save. For a lively test, turn on
`SellLoot` in Mod Options (or settings.json) first.

1. **Enter a town** with some food, horses, loot and prisoners. The town menu has **Party Steward (debug)** right
   after Trade. Click it.
2. **Read the popup**: gold now -> after, then the rows it would change (food, mounts, armour & weapons, prisoners).
   Does it make sense for your party? (Food toward 2 per man, pack animals toward 10, a horse per footman + 10%,
   upgrade horses for troops ready to upgrade, loot sold cheapest first, prisoners ransomed.)
3. **Compare prices**: press **Cancel**, open the vanilla Trade screen, and check a few first-unit prices against the
   popup's (the log also has a `price self-check` line — it should say *all equal*).
4. **Execute**: open the door again, press **Execute**. A message sums it up ("Steward: N of N done…").
   Check: gold changed by about the popup's amount; the inventory has the new food/horses and lost the sold loot;
   the prisoners are gone and the gold came in; locked items were not sold. Any "cut short" or "skipped"? The log
   says why.
5. **Tavern**: in a town with wanderers or mercenaries, use **Party Steward (debug, + tavern hires)** — it adds the
   first wanderer and the whole mercenary band. Execute, then check the clan screen (new companion) and the party
   (mercenaries, gold).
6. **Village**: repeat 1–4 in a village (a horse ranch is best). Prices there do not move as you buy.
7. **Edge cases if you have time**: a town at war with you or where you are a criminal (no trade rows expected);
   a looted village (no entry); the same door twice in a row (the second plan should be almost empty).

## Step 7 — the window

Deploy (game closed), load a save, enter a town. The town menu has **Party Steward** right after Trade (the debug
entries follow it until step 8). If anything looks broken, press Escape — and send the log.

1. **It opens and closes.** Click Party Steward: a dark window "Party Steward — <town>" with three tabs. Escape
   closes it; so does **Not now**. Open it again — same plan. Nothing in the game moved.
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
   Sell loot, change Food per man, cycle Food buying. In the Prisoners group, untick a prisoner under "Prisoners to
   ransom" — back on Suggestion his row stays at 0. Mod Options (if you have MCM) and settings.json show the same values.
11. **Village.** Repeat 2–3 and 8 in a village (no tavern, no prisoners there).
12. **Look.** Anything clipped, overlapping, unreadable, or in a wrong colour? A screenshot helps most.
