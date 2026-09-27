PLAN (the build, one step at a time — the first unchecked line is the current step; see CLAUDE.md "manager mode"):
- [x] 1. Bootstrap — concept → DESIGN.md, CLAUDE.md, this board, public GitHub repo
- [ ] 2. Research — verify every game API the design needs → docs/RESEARCH.md (no code)
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

NOT FULLY DECIDED (Anton's calls — defaults already chosen, see DESIGN):
- [ ] Food 2 per man = ~40 days of food — heavy on the cart; keep, or think in days?
- [ ] Pack animals: fixed 10 by default — or scale with party size?
- [ ] Auto-execute mode (no window, just a report) — wanted at all? built as an off-by-default option
- [ ] Edited rows stay as edited; the steward does not re-balance the rest around them

NOTICED (things spotted during a step, left for later):
