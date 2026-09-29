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
- [x] 17. Horses simplified (Anton's decisions 2026.09.28 — see the NOT FULLY DECIDED answers):
  total horses = footmen × MountsPer100Footmen/100; a plain "War mounts to keep" NUMBER (no automatic upgrade counting,
  no plain-horse upgrade reserve); regular mounts fill the rest (e.g. 100 footmen at 110 + keep 10 war → 100 mounts + 10 war);
  noble horses never bought, sold unless locked; lame horses never bought, "Replace lame horses with healthy ones" (default ON)
  - [ ] Deploy (the game was running at the end of step 17), then playtest — checklist in docs/PLAYTEST.md "Horses simplified"
- [x] 18. Collapsible sections in the Suggestion tab (Anton 2026.09.28): each section header expands/collapses; collapsed = ONE summary
  line (what the section will do + its gold, e.g. "Food  +29 (5 kinds) −510 · 64 → 71 days"); the state is REMEMBERED per section
  across windows, towns and game restarts (stays collapsed until Anton expands it) — kept in our settings folder, never in the save
  + FOOTER (same step, Anton 2026.09.28): the weight line splits in TWO — "Land: weight now +chg → after · capacity now → after"
    and "Sea: …" (pack horses raise land capacity but add weight at sea) — each part over its capacity in red;
  + a HORSES line: "Horses 110 / 200 before the herd slows you" — the game's real herding threshold (verify the speed model), red when over
  + TROOPS (Anton 2026.09.28): tier before every troop name ("T1 Vlandian Recruit"); MY troops ordered by tier, LOWEST on top;
    the COLLAPSED Troops line carries [-] [+] of its own: [-] dismisses from the lowest tier up, [+] recruits the highest tier on
    offer first (shift/ctrl steps as usual), and the line says what it does ("dismissing 1 T1 Vlandian Recruit, 1 T1 Imperial Peasant")
  - [ ] Deploy (the game was running at the end of step 18), then playtest — checklist in docs/PLAYTEST.md "Step 18"
- [x] 19. Round 4 MOCKUP — the Suggestion tab as one spreadsheet (docs/feedback/2026-09-28-round4.md), an HTML mockup for Anton to approve BEFORE the build
  - [x] Anton: look at docs/mockups/*.png and answer the 10 "Choices for Anton" in docs/mockups/README.md → APPROVED 2026.09.28 ("beautiful"), all ten stand + two changes: "Souls" → Party (members only) + a Prisoners column; the Market column far LEFT (README, DESIGN §1.1)
- [x] 20. Round 4 CORE — activation thresholds (food 2k, pack 2k, mounts 5k, war 20k), war horses 10, food multipliers ×2.0/×0.5,
  prisoner Lords/Others Keep|Ransom|Donate, the "Other goods" bulk line, per-row Denari/Souls/Land kg/Sea kg + section overviews
  + Total, troop aggregate lines (recruit best first, dismiss lowest first, re-add in reverse), overburden slowdown per terrain
  - [ ] Deployed at the end of step 20 (the game was closed — the install carries steps 12–20), then playtest — checklist in docs/PLAYTEST.md "Step 20" (the new window is step 21)
- [x] 21. Round 4 WINDOW — build the approved spreadsheet: sections with overview lines, combined Troops, Prisoners lines, Other,
  Total row, header denari + influence, footer weight table with the vanilla speed icon, colours by meaning, "denari" everywhere,
  hint moved to Instructions, Prices tab cheapest first, prisoner tiers + Encyclopedia
  - [ ] Deployed at the end of step 21 (the game was closed — the install carries steps 12–21), then playtest — checklist in docs/PLAYTEST.md "Step 21 — the spreadsheet"
- [x] 22. Round 5 CORE — the GOAL (docs/feedback/2026-09-28-round5.md): a Goal per line (troops = party limit, prisoners/other 0,
  food + pack/riding/war typed by hand = STANDING orders in settings.json until ⟲, the policy fills the rest, "-*" below a job's
  threshold), [-]/[+] on food & horse rows edit the goal, "Goals you set by hand" switches (thresholds / purse floor / price caps),
  weight without "kg" — DESIGN first, then Core + settings + tests
  - [ ] Deployed at the end of step 22 (the game was closed — the install carries steps 12–22), then playtest — checklist in docs/PLAYTEST.md "Step 22" (the Goal column is step 23)
- [x] 23. Round 5 WINDOW — columns Item · Market · Goal · Mine · Change · Result · Denari · Party · Prisoners · Land weight · Sea weight;
  typed goal boxes with ⟲, the "-*" hover, troops Mine red over the limit, the Instructions info lines + the new group; deploy + PLAYTEST
  - [ ] Deployed at the end of step 23 (the game was closed — the install carries steps 12–23), then playtest — checklist in docs/PLAYTEST.md "Round 5 — the Goal"
- [x] 24. BUG — a village raid won in battle ends in capture ("DefenderVictory"; docs/feedback/2026-09-29-raid-capture-bug.md):
  find whether the steward touches the encounter (LeaveGuard, the arrival popup, the menu entry, autonomy) during a hostile action; fix + log; can save "fst2" be repaired
  → VANILLA: "Leave..." out of a hostile action makes you the village's defender; fst2: Leave + re-enter, then raid (RESEARCH §28)
  - [ ] Deployed 2026.09.29 by the manager after step 24 (the install carries steps 12–24), then playtest — checklist in docs/PLAYTEST.md "Step 24"

SHIPPING NEXT (done in main, NOT released yet):
- Every load starts the steward clean (window, visit, pending popup dropped); the log tells a stuck Left Alt and where each window open came from
- No arrival popup where you cannot trade or the steward has no rows
- A closed market says why in the window ("Market closed: …", the game's own words) and in the log
- ~~Upgrade horses set per kind~~ → Horses simplified: "Horses per 100 footmen" keeps T horses, "War horses to keep" (a plain number, default 0) among them, riding horses the rest — no upgrade counting; the old upgrade-horse settings are retired (logged once)
- Noble horses: never bought, sold unless you lock them ("Sell noble horses"); their own sell-only row, and "Noble horses — sell only" in the Prices tab
- Lame and old horses: never bought; "Replace lame horses" (on) sells them and buys healthy ones in their place
- Prisoners: ransom all or none — the "Prisoners to ransom" list is gone ("Include lords" stays)
- Prices tab: the item name is the first column
- Locked food and horses are managed too (counted, sold as surplus) — locks keep guarding armour & weapons; "Locks protect food & horses" brings the old way back
- The party size limit never blocks a hire — the footer shows the party after the deal (Party 99/96), red when over
- Docking at a port (War Sails) brings the steward too: the arrival popup / autonomous run on docking, "Party Steward" in the port menu
- ~~Weight line like the food: "Weight 1,000 +120 kg » 1,120 kg · capacity land 1,500 / sea 1,000"~~ → ~~two lines "Land: …" / "Sea: …"~~ → the footer's weight table (step 21)
- The "Not now" button is "Close"
- A wanderer's name opens a complete Encyclopedia page (the steward's tavern section counts as the tavern district)
- Live re-plan: hires and prisoners kept or ransomed re-plan the food and horses at once — your own rows (⟲ shown) stay and go first
- Food goal in days: "Keep food for [40] days (~2.0 per soul)" at your party's own rate, perks included; an old "Food per man" converts once (× 20)
- Troops section right after the Tavern (towns and villages): the recruits the notables offer you, then your own troops — [+] recruits, [-] dismisses (the wounded first), past the party limit; food and horses follow
- A closed market keeps the table for what you can still do there (troops, wanderers), the reason above it
- ~~Click a section's name to fold it to one line~~ → fold a section to its title line (its subtotals) — it stays folded across towns and restarts (window_state.json, never the save)
- Herd: "110 / 200 before the herd slows you" — the game's own rule, the party after the deal, red when over (the Horses title line since step 21)
- Troops show their tier ("T1 Vlandian Recruit"): yours lowest tier first, recruits on offer highest first; the folded Troops line has its own [-] (lowest tier out) and [+] (best recruits in)
- Jobs switch on as you get richer: food and pack animals from 2,000 denari, riding horses from 5,000, war horses from 20,000 (settings; below it the steward leaves the job alone) — and 10 war horses kept by default
- Food has price multipliers of its own (×2.0 buy / ×0.5 sell — grain at 10 bought up to 20); the old pair are the horse multipliers now (your values carried over)
- Prisoners: "Captured lords" (Keep) and "Other prisoners" (Ransom) — each Keep / Ransom / Donate; donating where the game forbids it ransoms the others and keeps the lords; prisoners by tier, lowest first (your old switches carried over)
- "Armour & weapons" is now "Other", with an Other goods row: wool, salt, pottery, jewelry… sold in bulk, cheapest first ("Sell other goods")
- Prices tab: cheapest first inside each group
- Troops: "Your troops" [-] drops the lowest tier first and [+] brings them back in reverse; "Recruits" [+] hires the best tier first (the lines of the new window, step 21)
- The Suggestion tab is ONE spreadsheet: ~~Market · Item · Mine · Change · Result · Denari · Party · Prisoners · Land kg · Sea kg~~ (round 5's order below) — every section's title line is its subtotal, a Total line under the table that never scrolls
- Header "Denari 69,358 » 89,189 (+19,831)" + small green influence; colours by meaning (green in, red out), zeros blank
- Troops in one section: each wanderer, the mercenaries, Recruits and Your troops — each opens its rows by tier (a type on offer that you hold shows under both, each side on its own)
- Prisoners: "Lords" and "Others" lines with Keep | Ransom | Donate — the toggle is your standing order (Donate greyed where the game forbids it); prisoner rows show their tier, lowest first, names open the Encyclopedia
- Footer = only the weight table: Land / Sea × before · change · after · capacity · left · slowdown, with the game's own speed icon (the horse; the ship at sea)
- Every fold remembered (sections, Recruits, Your troops, each horse row, Other goods); the first window opens in the everyday view
- "denari" in every money text; the "Click ±1 · Shift ±5 · Ctrl all" hint moved to the top of the Instructions tab; the Prices tab has Food's own multiplier boxes beside the horses'
- Goals: a click on a food, pack, riding or war horse row is a standing order kept in settings.json for every town until its ⟲ — the steward plans around it (Reset all keeps them); the autonomous steward obeys them too
- Instructions: "Goals you set by hand" — your goals wait for the thresholds (off), keep the purse floors (on), obey the price limits (on)
- Weight, not kg: "Land weight" / "Sea weight", "lowest price per weight"
- The Goal column: Item · Market · Goal · Mine · Change · Result · … — type a goal for a food, pack animals, riding or war horses (Enter or click away), gold with its ⟲ while yours; "–*" below a threshold, hover says why
- Troops title: Goal = party size limit, Mine red when you are over it; each title's overview on its own row under the name
- A Result short of its goal says why on hover; Escape in a text box leaves the box, a second Escape closes the window
- Instructions: a line on Food and the horse groups ("… follows these rules until you type its goal"), and the goal line in the hint on top
- The steward stands aside during any fight or hostile action (no popup, leave question, autonomy or Do it; the menu entry greys)

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

- [ ] Pack animals: "keep enough to carry my load + a margin" as an alternative to a fixed number (the weight line now knows capacity)

NEXT UPDATE (V1 = tavern, food, horses, armour & weapons selling, prisoners — Anton 2026.09.27):
- [ ] Others in the price book — wood, jewelry, metal… bought below / sold above your price, hold-up-to cap (see DESIGN §1.3.1)

NOT FULLY DECIDED (Anton's calls — defaults already chosen, see DESIGN):
- [x] Recruits on offer: HIGHEST tier on top (what you take first; the folded [+] takes them first too) — or lowest first like your troops? (DESIGN §2.8, step 18) → Anton 2026.09.28: approved with the mockup (choice 8: best tier first)
- [x] Food 2 per man = ~40 days of food — heavy on the cart; keep, or think in days? → Anton 2026.09.28: DAYS — "Keep food for [40] days (~2.0 per soul)" (DESIGN §2.1)
- [x] Pack animals: fixed 10 by default — or scale with party size? → Anton 2026.09.28: fixed 10 now; "enough to carry my load + margin" goes to NEXT UPDATE
- [x] Shift/Ctrl steps stop at zero — one click never flips a row from selling to buying (DESIGN §1.1) → Anton 2026.09.28: yes
- [x] Livestock (cows, sheep…) — the game counts it as food; proposed: steward ignores it (see RESEARCH "Design impact") → Anton 2026.09.28: yes
- [x] Recruits that can go foot OR cavalry — count them as needing a horse? → Anton 2026.09.28: MOOT — no automatic upgrade counting at all any more (step 17)
- [x] Plain "horse" mounts double as upgrade horses — reserve them for upgrades first? → Anton 2026.09.28: MOOT — plain-horse upgrades just draw from the riding mounts (step 17)
- [x] Prisoners locked in the party screen — never ransom them, like vanilla? → Anton 2026.09.28: yes
- [x] Lame/spirited (modified) horses → Anton 2026.09.28: a bad horse still carries a footman (keep it for the speed); NEVER buy one; new switch "Replace lame horses with healthy ones" default ON (step 17)
- [x] Sell multiplier default 0.8 (sell only at ≥ average sell price − 20%) — right number? (DESIGN §1.3) → Anton 2026.09.28: yes, 0.8
- [x] Role caps (mount 500 / pack 300 / war 2000) stay fixed — or scale with the buy multiplier too? → Anton 2026.09.28: fixed (0 switches a cap off)
- [x] Noble horses sit with the war horses in the Prices tab → Anton 2026.09.28: NO — noble horses are never used for upgrades (player/companion horses only): never bought, sold unless LOCKED (step 17)
- [x] Donating fills the dungeon with the most valuable prisoners first (most influence) — ok? (DESIGN §2.5) → Anton 2026.09.28: yes

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
- [ ] Step 24: the capture's game log shows "Resolving: Enlisted" (twice) — some loaded mod asks for an "Enlisted" assembly that is not in the load order; harmless here, worth a look in the sibling mods
- [x] Step 9: PlanReport.Compact and ExecutionReport.Summary lost their last caller with the debug door (tests only) — dropped (step 9)
- [ ] Step 9 (left on purpose): a click on a huge plan still costs ~20 ms — one trial walk per live button; incremental walks could cut it, measure in game first (PlanPerformanceTests)
- [x] Step 9 (left on purpose): surplus riding horses are kept while an upgrade horse is on offer, even when the floors will not let the steward buy it — "never sell and buy mounts in one visit" taken strictly → step 17: the rule is gone (the riding surplus is sold against the war horses the plan buys)
- [x] Step 10: README — translations welcome: copy module/ModuleData/Languages/std_SmartSteward.xml into Languages\XX (how-to in the file's header) — README + Steam page
- [ ] Release day (after step 11): tools\WORKSHOP-UPLOAD.md — bump v1.0.0, package.ps1, create Private, paste the page, flip Public
- [ ] Step 10: the Workshop preview is a drawn stand-in of the window — after the playtest put a real shot in tools\preview_thumbnail.html, re-render (tools\render-preview.ps1)
- [ ] Step 10: the release ships no PDBs (like TrainingBattles) — with them a player's log would carry line numbers (~100 KB); Anton's call
- [ ] TrainingBattles' package.ps1 zips with Compress-Archive → backslash entry names (seen in TrainingBattles_v1.4.0.zip); ours writes '/' — port it there
- [ ] Step 12: a stuck Left Alt (after Alt+Tab) blocks entering towns in vanilla too — tell the player in game ("tap Left Alt")? now log-only (InputWatch); Anton's call
- [ ] Step 13: with "Locks protect food & horses" on, a food / animal row shows `40 (+40 locked)` — its Mine already counts the locked units (loot's Mine does not), so "+" reads as extra; say "(40 locked)" there?
- [x] Step 14: the footer grew a line (the weight line) — the table is ~30 px (one row) shorter; check the look at 1080p → step 21 replaced the footer (weight table + pinned Total)
- [ ] Step 14: unverified whether a party docked at a port counts as IsCurrentlyAtSea (the party screen's capacity uses that live flag) — compare with the footer's land / sea in game
- [x] Step 15: a village has no tavern and no ransom, so the live re-plan's promise is tested in towns (walking and flat prices) — step 16: add a village case with recruits to LivePlanTests (done: 3 village cases)
- [ ] Step 16: with PopupOnlyWithChanges OFF the arrival popup now opens at nearly every open market — the "Your troops" rows count as rows (at 0 they never count as changes); fine by that switch's meaning?
- [ ] Step 17: the Steam page still says "A row you edit stays as you set it — the steward does not re-balance the others around it" — stale since step 15's live re-plan
- [ ] Step 17: War horses to keep defaults to 0 (Anton's number) — so every unlocked war horse a party holds is proposed for sale on the first visit after the update; say so in the release notes?
- [ ] Step 17: a lame horse only counts for the game's speed from the next load (the roster's live counter skips modified animals, RESEARCH §22) — the steward counts it at once; harmless, but the party screen's speed may disagree until a reload
- [ ] Step 18: the Steam page and README say nothing of folding sections, the herd line or tiers — add a line each at release (Steam page 6,066 of 8,000 bytes)
- [x] Step 18: the footer grew again (146 → 176 px, the land / sea / herd lines) — the table is one more row shorter; folding sections gives it back — check the look at 1080p → step 21 replaced the footer
- [x] Step 20: SuggestionSheet's words (SheetWords) are English defaults in Core — step 21 must fill them from TextObjects (ss_ui_sheet_*) like SummaryWords, so the strings file gathers them → UiLabels.SheetText (step 21)
- [x] Step 20: the Prices tab's two top boxes are the HORSE multipliers now; the food pair is only in Instructions/MCM until step 21 gives the Food group boxes of its own → both pairs on the top line (step 21)
- [x] Step 20: the old window shows no prisoner tiers and keeps its step-18 footer; its folded Troops line [-]/[+] follow the new "own side only" rule — all replaced by step 21
- [x] Step 20: the spreadsheet's folds (Recruits ▸, Your troops ▸, the role rows' ▸, Other goods ▸) need keys in window_state.json — step 21 (SectionGroup still names the old sections; "ArmourAndWeapons" reads as Other) → SheetFolds, key "Folded" (step 21)
- [ ] Step 20: an army's SEA slowdown adds the attached parties' live TotalWeightCarried (their land weight while in a settlement) — an approximation, the game weighs them at sea
- [ ] Step 20: the mockup's Horses line says "92 on foot, 101 horses to keep" but the rule gives ceil(92 × 110 / 100) = 102 — the mockup's numbers were drawn by hand; the Core is right
- [ ] Release day: the Steam page and README still say "Armour & weapons", "Ransom prisoners / Include lords / Donate", one buy/sell multiplier — update with the round-4 names
- [ ] Step 21: a food / horse / loot row the deal does not move shows no unit price (the mockup drew "11 each") — the next unit's price is known only to a trial walk, one more per row per click; add a cached quote if Anton misses it
- [ ] Step 21: RowCells, SectionSummary.Of + SummaryWords and UiColors.ForGoldChange are unused by the window since step 21 (tests only, TroopName still serves) — drop them in a cleanup
- [ ] Step 21: the Your troops note ("dropping 2 T0 Empire Peasant, 1 T1 …") can clip at three long type names in the 426 px Item cell — Core's Men() counts past three types; maybe past two
- [ ] Step 21: "names in gold open the Encyclopedia" keeps "gold" — the colour of the names, not the money (every money text says denari)
- [ ] Step 21: a closed market's notice (560 px, right of the header line) may touch a long centred Denari header — check at 1080p
- [ ] Step 21: never seen in game — the Denari-cell tooltip rides a HintWidget on a plain Widget (the Prices tab labels' pattern); check it shows
- [x] Step 22: until step 23 shows the Goal column, a click on a food / horse row silently becomes a standing order (saved in settings.json) — the ⟲ is its only sign; do not release between 22 and 23 → step 23 shows it (gold goal + ⟲ in the Goal column)
- [ ] Step 22: food and horse clicks re-plan now (the policy fills the rest): the benchmark's clicks went 0–15 → 0–30 ms (ResetAll the slowest) — fine under the 100 ms budget; measure in game with a big party
- [ ] Step 22: a steward's riding row short of its target may show no reason when the war-horse pledge simulation cut its buys (GoalShort None) — rare; say if a Result stops short without a hover
- [ ] Step 22: SectionSummary's SummaryWords.Kg still says "kg" — dead code since step 21 (see the RowCells / SectionSummary cleanup line), never shown
- [ ] Release day: the Steam page and README say nothing of goals and still say "A row you edit stays as you set it" — describe the standing goals and "Goals you set by hand"
- [ ] Step 23: the horse role rows' note still says "keep 10" — the Goal column says the same now; drop the note (Core SheetView.Note) if Anton finds it doubled
- [ ] Step 23: the title lines grew a second row (38 → 54 px: the overview under the name) — ~1 line less on screen with every section open; Anton's look at 1080p
- [ ] Step 23: never seen in game — the Goal box's Enter / FocusLost commands (vanilla binds TextEntered only in the banner builder), its tooltip as the box's child, the Result hover; check them first
- [ ] Step 23: Escape in ANY text box now only leaves the box (Prices, Instructions too) — a second Escape closes; say if that is unwelcome
- [ ] Step 23: a very large typed goal walks one price per unit (like Ctrl on the row): 400 grain on the benchmark's plan = ~620 game prices, ~19 ms — watch the first click in game with a huge market
