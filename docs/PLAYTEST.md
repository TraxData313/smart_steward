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
