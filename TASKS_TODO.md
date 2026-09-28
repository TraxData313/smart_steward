PLAN (the build, one step at a time — the first unchecked line is the current step; see CLAUDE.md "manager mode"):
- [x] 1. Bootstrap — concept → DESIGN.md, CLAUDE.md, this board, public GitHub repo
- [x] 2. Research — verify every game API the design needs → docs/RESEARCH.md (no code)
- [x] 3. Scaffold — solution, Core/Module/tests, SubModule.xml, deploy.ps1; empty module loads in game
- [x] 4. Core planners + unit tests — food, pack, mounts, war mounts, armour & weapons groups, prisoners, tavern, money floors
- [x] 4b. Plan editing model — row edits ±1/±5/all, reset, live re-pricing, totals (Core, tested)
- [x] 5. Settings — one registry → commented settings.json + MCM (fluent, soft) + log file
- [x] 6. Game adapter — snapshot from the game, executor through the game's own actions (debug menu door)
  - [ ] Playtest the executor: town, village, tavern hires — checklist in docs/PLAYTEST.md "Step 6" (the debug door is gone: use the window's Do it)
- [x] 7. Party Steward window — Suggestion table (±1/±5/±all, reset, header total) + Prices tab + Instructions tab
  - [ ] Playtest the window: tabs, clicks, tooltips, Encyclopedia round trip, Do it — checklist in docs/PLAYTEST.md "Step 7 — the window"
- [x] 8. Triggers — menu entries, popup on arrival, leave warning, FULL-AUTONOMOUS steward (AutoExecute → AutonomousSteward + AutonomousMinGold 100k, message-log report — DESIGN §6; update §7 with the code), remove the debug door
  - [ ] Playtest the triggers: arrival popup, leave question, ships, autonomy — checklist in docs/PLAYTEST.md "Step 8 — triggers & autonomy"
- [x] 9. Review pass — whole-mod code review, fix what it finds, strings through TextObject ids
  - [ ] Playtest the review fixes: big clicks, ▸ breakdown, autonomous floor, log — checklist in docs/PLAYTEST.md "Step 9"
- [x] 10. Packaging — package.ps1, Steam description + workshop files, README
- [ ] 11. Anton's first playtest — round 1 done 2026.09.28 (findings under BUGS)
- [ ] 12. Playtest round 1 fixes — the five BUGS lines marked (R1)

SHIPPING NEXT (done in main, NOT released yet):

BUGS:
- [ ] (R1) Prices tab: Item name must be the FIRST (leftmost) column
- [ ] (R1) Upgrade horses unclear: "to keep" = 10 bought 10 horses AND 10 war horses — split per kind, clear labels, show the live need
- [ ] (R1) Drop the "Prisoners to ransom" tick-list (+ PrisonersExcluded): ransom all or none, the lords switch is enough
- [ ] (R1) Arrival popup opened at a village with NO TRADE and nothing to do (Hiblet) — must stay shut
- [ ] (R1) Closed market (war, crime…) shows an empty table — say the game's reason in the window instead
- [ ] (R1) Town wouldn't let Anton in ("clan tier not high enough to request a meeting") — that is vanilla's town_outside menu (hostile/crime); confirm the mod cannot cause it

NEXT UPDATE (V1 = tavern, food, horses, armour & weapons selling, prisoners — Anton 2026.09.27):
- [ ] Others in the price book — wood, jewelry, metal… bought below / sold above your price, hold-up-to cap (see DESIGN §1.3.1)

NOT FULLY DECIDED (Anton's calls — defaults already chosen, see DESIGN):
- [ ] Food 2 per man = ~40 days of food — heavy on the cart; keep, or think in days?
- [ ] Pack animals: fixed 10 by default — or scale with party size?
- [ ] Edited rows stay as edited; the steward does not re-balance the rest around them
- [ ] Shift/Ctrl steps stop at zero — one click never flips a row from selling to buying (DESIGN §1.1)
- [ ] Livestock (cows, sheep…) — the game counts it as food; proposed: steward ignores it (see RESEARCH "Design impact")
- [ ] Recruits that can go foot OR cavalry — count them as needing a horse? proposed: yes (DESIGN §2.4)
- [ ] Plain "horse" mounts double as upgrade horses — reserve them for upgrades first? proposed: yes
- [ ] Prisoners locked in the party screen — never ransom them, like vanilla? proposed: yes
- [ ] Lame/spirited (modified) horses — count them as mounts, never buy them? proposed: yes
- [ ] Sell multiplier default 0.8 (sell only at ≥ average sell price − 20%) — right number? (DESIGN §1.3)
- [ ] Role caps (mount 500 / pack 300 / war 2000) stay fixed — or scale with the buy multiplier too?
- [ ] Noble horses sit with the war horses in the Prices tab (no auto-filled price) — ok? (DESIGN §1.3)
- [ ] Donating fills the dungeon with the most valuable prisoners first (most influence) — ok? (DESIGN §2.5)

NOTICED (things spotted during a step, left for later):
- [ ] Old ..\reference\game-decompiled differs from v1.4.8 in 4 files — step 2 made ..\reference\game-decompiled-1.4.8
- [ ] TrainingBattles' TrainingWindow.Close() never calls ReleaseMovie, and its prefabs carry MouseScrollAxis (gone in 1.4.8) — fix there; ours does both right (step 7)
- [ ] Vanilla's donate screen sizes the dungeon room by prisoner stacks, not men — we use NumberOfPrisoners
- [ ] TrainingBattles + ImmersiveAI list MCM only in DependedModuleMetadatas — the vanilla launcher ignores that; add an Optional DependedModule (RESEARCH §12)
- [x] Step 8: War Sails' port/sail menus must exist before the leave-wrap — wrapped lazily (War Sails builds them in OnAfterSessionLaunched: no load order could help)
- [ ] Playtest: Mod Options page (10 groups, 44 settings) never seen in game yet — gold sliders run 0–1,000,000 (Keep while autonomous 0–10,000,000), are they usable?
- [x] Step 8: remove the TEMPORARY debug door (DebugDoor.cs + its OnSessionLaunched hook in SmartStewardBehavior) — gone
- [ ] Step 7: a wanderer's hire price is re-read at Do it (the trades move the town's prices) — the row may show a slightly different number
- [x] Step 8: the "Party Steward" menu entry already exists (StewardMenu, step 7) — step 8 added the popup, leave warning and autonomy
- [x] Step 9: the window's texts carry ids ss_ui_* (+ MCM's ss_set_/ss_hint_/ss_opt_/ss_grp_, step 8's ss_auto_*/ss_leave_*) — gathered: module/ModuleData/Languages/std_SmartSteward.xml, held to the code by StringsFileTests
- [ ] Playtest: the window's look was never seen — column widths, font sizes, the Encyclopedia focus round trip (PLAYTEST Step 7)
- [x] Step 9: PlanReport.Compact and ExecutionReport.Summary lost their last caller with the debug door (tests only) — dropped (step 9)
- [ ] Step 9 (left on purpose): a click on a huge plan still costs ~20 ms — one trial walk per live button; incremental walks could cut it, measure in game first (PlanPerformanceTests)
- [ ] Step 9 (left on purpose): surplus riding horses are kept while an upgrade horse is on offer, even when the floors will not let the steward buy it — "never sell and buy mounts in one visit" taken strictly
- [x] Step 10: README — translations welcome: copy module/ModuleData/Languages/std_SmartSteward.xml into Languages\XX (how-to in the file's header) — README + Steam page
- [ ] Release day (after step 11): tools\WORKSHOP-UPLOAD.md — bump v1.0.0, package.ps1, create Private, paste the page, flip Public
- [ ] Step 10: the Workshop preview is a drawn stand-in of the window — after the playtest put a real shot in tools\preview_thumbnail.html, re-render (tools\render-preview.ps1)
- [ ] Step 10: the release ships no PDBs (like TrainingBattles) — with them a player's log would carry line numbers (~100 KB); Anton's call
- [ ] TrainingBattles' package.ps1 zips with Compress-Archive → backslash entry names (seen in TrainingBattles_v1.4.0.zip); ours writes '/' — port it there
