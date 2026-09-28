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
- [x] 12. Playtest round 1 fixes — the six BUGS lines marked (R1), the save-load bug first
  - [ ] Deploy (the game was running at the end of step 12), then playtest the fixes — checklist in docs/PLAYTEST.md "Round 1 fixes"
- [x] 13. Playtest round 2 fixes — the BUGS lines marked (R2)
  - [ ] Deploy (the game was running at the end of step 13), then playtest — checklist in docs/PLAYTEST.md "Round 2 fixes"
- [x] 14. Round 3, small — party limit never blocks hires (show 99/96), steward also on docking into a port, weight line now + change → after with land/sea capacity (red when over), the "Not now" button renamed "Close"
  - [ ] Deploy (the game was running at the end of step 14), then playtest — checklist in docs/PLAYTEST.md "Round 3 — small"
- [x] 15. Round 3, core — LIVE RE-PLAN: untouched food/mount/upgrade-horse rows follow every party change in the window (hires, recruits, dismissals, ransoms); edited rows stay — so Do it leaves nothing new to suggest (+ the food goal in days)
  - [ ] Deployed (the game was closed at the end of step 15 — the install carries steps 12–15), then playtest — checklist in docs/PLAYTEST.md "Round 3 — live re-plan"
- [x] 16. Round 3, big — the TROOPS section: recruits on offer here + my troops, recruit and dismiss (see NEXT UPDATE lines marked R3)
  - [ ] Deployed (the game was closed at the end of step 16 — the install carries steps 12–16), then playtest — checklist in docs/PLAYTEST.md "Round 3 — troops"

SHIPPING NEXT (done in main, NOT released yet):
- Every load starts the steward clean (window, visit, pending popup dropped); the log tells a stuck Left Alt and where each window open came from
- No arrival popup where you cannot trade or the steward has no rows
- A closed market says why in the window ("Market closed: …", the game's own words) and in the log
- Upgrade horses set per kind: "Horses for upgrades" / "War horses for upgrades" (+ spares), the live ready count beside them; the old single number resets to automatic
- Prisoners: ransom all or none — the "Prisoners to ransom" list is gone ("Include lords" stays)
- Prices tab: the item name is the first column
- Locked food and horses are managed too (counted, sold as surplus) — locks keep guarding armour & weapons; "Locks protect food & horses" brings the old way back
- The party size limit never blocks a hire — the footer shows the party after the deal (Party 99/96), red when over
- Docking at a port (War Sails) brings the steward too: the arrival popup / autonomous run on docking, "Party Steward" in the port menu
- Weight line like the food: "Weight 1,000 +120 kg » 1,120 kg · capacity land 1,500 / sea 1,000" — the capacity after the deal, the part over it in red
- The "Not now" button is "Close"
- A wanderer's name opens a complete Encyclopedia page (the steward's tavern section counts as the tavern district)
- Live re-plan: hires and prisoners kept or ransomed re-plan the food and horses at once — your own rows (⟲ shown) stay and go first
- Food goal in days: "Keep food for [40] days (~2.0 per soul)" at your party's own rate, perks included; an old "Food per man" converts once (× 20)
- Troops section right after the Tavern (towns and villages): the recruits the notables offer you, then your own troops — [+] recruits, [-] dismisses (the wounded first), past the party limit; food and horses follow
- A closed market keeps the table for what you can still do there (troops, wanderers), the reason above it

BUGS:
- [x] (R3) Wanderer's Encyclopedia page shows ??? until the tavern district is visited
- [x] (R3) Party size limit must NOT block mercenaries or recruits — hire past it, just show the party after the deal (e.g. 99/96, red when over)
- [x] (R3) Docking into a port (War Sails) must trigger the steward too — popup + menu entry at the port, not only the town menu
- [x] (R2) Locked food and horses are left alone — Anton wants them managed anyway; locks keep guarding armour & weapons (new switch LocksProtectFoodAndHorses, default off)
- [x] (R1) Prices tab: Item name must be the FIRST (leftmost) column
- [x] (R1) Upgrade horses unclear: "to keep" = 10 bought 10 horses AND 10 war horses — split per kind, clear labels, show the live need
- [x] (R1) Drop the "Prisoners to ransom" tick-list (+ PrisonersExcluded): ransom all or none, the lords switch is enough
- [x] (R1) Arrival popup opened at a village with NO TRADE and nothing to do (Hiblet) — must stay shut (the log: the popup stayed shut, the window came from the menu entry; now also never at a closed market)
- [x] (R1) Closed market (war, crime…) shows an empty table — say the game's reason in the window instead
- [x] (R1) TOP: after loading a save in the same game session, clicking towns/villages no longer entered them ("clan tier not high enough to request a meeting" shown) — a game RESTART cured it. Found: the game read Left Alt as HELD (Alt+Tab) → every click asked for a parley; not the mod — tap Left Alt (RESEARCH §17)

NEXT UPDATE — ROUND 3, building now (Anton 2026.09.28):
- [x] (R3) TROOPS section right after the Tavern: the recruits on offer in this town/village (volunteers of its notables you may take) at the top, then the troops you have — one row per troop type, Mine / [-] [+] / Result / price / on offer; [+] recruits, [-] dismisses; never every troop in the game; starts at 0 like the tavern; names open the Encyclopedia
- [x] (R3) Live re-plan (Anton: "after I change the troops the mounts etc are not accurate… so when I hit Do it I won't see new suggestions"): untouched rows follow the party after the deal; touched rows stay
- [x] (R3) Weight line like the food: "1,000 +120 kg → 1,120 kg · capacity land 1,500 / sea 1,000" — capacity AFTER the deal (pack animals and troops add to it), the part over a capacity in red ("+120 over at sea")
- [x] (R3) The "Not now" button next to Do it is just "Close"
- [x] (R3) Party limit is info, not a wall — header/footer shows the party after the deal (99/96), red when over

NEXT UPDATE (V1 = tavern, food, horses, armour & weapons selling, prisoners — Anton 2026.09.27):
- [ ] Others in the price book — wood, jewelry, metal… bought below / sold above your price, hold-up-to cap (see DESIGN §1.3.1)

NOT FULLY DECIDED (Anton's calls — defaults already chosen, see DESIGN):
- [x] Food 2 per man = ~40 days of food — heavy on the cart; keep, or think in days? → Anton 2026.09.28: DAYS — "Keep food for [40] days (~2.0 per soul)" (DESIGN §2.1)
- [ ] Pack animals: fixed 10 by default — or scale with party size?
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
- [ ] Playtest: Mod Options page (10 groups, 46 settings) never seen in game yet — gold sliders run 0–1,000,000 (Keep while autonomous 0–10,000,000), are they usable?
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
- [ ] Step 12: a stuck Left Alt (after Alt+Tab) blocks entering towns in vanilla too — tell the player in game ("tap Left Alt")? now log-only (InputWatch); Anton's call
- [ ] Step 13: with "Locks protect food & horses" on, a food / animal row shows `40 (+40 locked)` — its Mine already counts the locked units (loot's Mine does not), so "+" reads as extra; say "(40 locked)" there?
- [ ] Step 14: the footer grew a line (the weight line) — the table is ~30 px (one row) shorter; check the look at 1080p
- [ ] Step 14: unverified whether a party docked at a port counts as IsCurrentlyAtSea (the party screen's capacity uses that live flag) — compare with the footer's land / sea in game
- [x] Step 15: a village has no tavern and no ransom, so the live re-plan's promise is tested in towns (walking and flat prices) — step 16: add a village case with recruits to LivePlanTests (done: 3 village cases)
- [ ] Step 16: with PopupOnlyWithChanges OFF the arrival popup now opens at nearly every open market — the "Your troops" rows count as rows (at 0 they never count as changes); fine by that switch's meaning?
