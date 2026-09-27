PLAN (the build, one step at a time — the first unchecked line is the current step; see CLAUDE.md "manager mode"):
- [x] 1. Bootstrap — concept → DESIGN.md, CLAUDE.md, this board, public GitHub repo
- [x] 2. Research — verify every game API the design needs → docs/RESEARCH.md (no code)
- [ ] 3. Scaffold — solution, Core/Module/tests, SubModule.xml, deploy.ps1; empty module loads in game
- [ ] 4. Core planners + unit tests — food, pack, mounts, war mounts, prisoners, loot, tavern, money floors
- [ ] 5. Settings — one registry → commented settings.json + MCM (fluent, soft) + log file
- [ ] 6. Game adapter — snapshot from the game, executor through the game's own actions (debug menu door)
- [ ] 7. Party Steward window — Suggestion table (±1/±5/±all, reset, header total) + Instructions tab
- [ ] 8. Triggers — menu entries, popup on arrival, leave warning, auto-execute
- [ ] 9. Review pass — whole-mod code review, fix what it finds, strings through TextObject ids
- [ ] 10. Packaging — package.ps1, Steam description + workshop files, README
- [ ] 11. Anton's first playtest

SHIPPING NEXT (done in main, NOT released yet):

BUGS:

NEXT UPDATE:
- [ ] Others in the price book — wood, jewelry, metal… bought below / sold above your price, hold-up-to cap (see DESIGN §1.3.1)

NOT FULLY DECIDED (Anton's calls — defaults already chosen, see DESIGN):
- [ ] Food 2 per man = ~40 days of food — heavy on the cart; keep, or think in days?
- [ ] Pack animals: fixed 10 by default — or scale with party size?
- [ ] Auto-execute mode (no window, just a report) — wanted at all? built as an off-by-default option
- [ ] Edited rows stay as edited; the steward does not re-balance the rest around them
- [ ] Livestock (cows, sheep…) — the game counts it as food; proposed: steward ignores it (see RESEARCH "Design impact")
- [ ] Recruits that can go foot OR cavalry — count them as needing a horse? proposed: yes (DESIGN §2.4)
- [ ] Plain "horse" mounts double as upgrade horses — reserve them for upgrades first? proposed: yes
- [ ] Prisoners locked in the party screen — never ransom them, like vanilla? proposed: yes
- [ ] Lame/spirited (modified) horses — count them as mounts, never buy them? proposed: yes
- [ ] Sell multiplier default 0.8 (sell only at ≥ average sell price − 20%) — right number? (DESIGN §1.3)
- [ ] Role caps (mount 500 / pack 300 / war 2000) stay fixed — or scale with the buy multiplier too?
- [ ] Prisoners' place in the Suggestion tab: last, after armour & weapons?

NOTICED (things spotted during a step, left for later):
- [ ] Old ..\reference\game-decompiled differs from v1.4.8 in 4 files — step 2 made ..\reference\game-decompiled-1.4.8
- [ ] TrainingBattles' TrainingWindow.Close() never calls ReleaseMovie — add it in our window (step 7)
- [ ] Vanilla's donate screen sizes the dungeon room by prisoner stacks, not men — we use NumberOfPrisoners
