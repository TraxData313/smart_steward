# Bug report — raid at a village ends in capture after a won fight (Anton, 2026.09.29)

Pasted by Anton into the manager session (found while playtesting another mod). Verbatim:

> Bug report for Smart Steward (from Anton, found while playtesting another mod, 2026-09-29):
>
> SYMPTOM: I raid a village (Kamshar, scene khuzait_village_a) and WIN the fight, but right after it
> "Renaud has been captured by Kamshar!", I'm released at once (menu_captivity_end_no_more_enemies)
> and my army is gone. Reproducible every time from my save "fst2". It still happens with Trax
> Combat Enhancements disabled, and I'm fairly sure it's Smart Steward.
>
> EVIDENCE (C:\ProgramData\Mount and Blade II Bannerlord\logs\rgl_log_21220.txt):
> - 20:12:04 hostile action on the village, encounter state Wait; 20:12:07 WaitingRemoval, then
>   "Player has entered … Kamshar" AGAIN - the encounter was torn down and restarted while I was at
>   the village (I did not back out on purpose). 20:12:11 saved fst2, 20:12:15 raided.
> - 20:16:54.933 CheckMissionEnded::ended -> "Player MapEvent BattleState: DefenderVictory", then
>   "You won the battle". Earlier normal raids log AttackerVictory (e.g. 14:21:40 in rgl_log_47924).
> - 20:17:49.143 captured -> released. Retry from fst2: 20:26:48 DefenderVictory, 20:27:09 captured.
> - The combat mission itself ended correctly: player victory, 97 of my men standing, 0 enemies.
>
> HYPOTHESIS: after a mission win, CampaignMissionComponent.OnMissionResultReady calls
> PlayerEncounter.SetPlayerVictorious(), which declares the side stored in PlayerEncounter.PlayerSide the
> winner. When an encounter is (re)started while the party is inside the settlement, PlayerEncounter.Init
> sets PlayerSide = Defender. So if something finishes and restarts the player's encounter at a village
> (e.g. opening or leaving the village menu, trading or buying food through the steward, calling
> PlayerEncounter.Finish / Start / EnterSettlement, or LeaveSettlement + re-enter), the raid that follows
> has me attacking while PlayerSide says Defender, so the village "wins" and MapEvent captures me. The
> save keeps that state.
>
> PLEASE:
> 1. Find every place Smart Steward touches PlayerEncounter, GameMenu, settlement enter/leave, or runs
>    on village entry / hourly while the player is inside a settlement (esp. during a hostile action or
>    the encounter's Wait state). Check the decompiled game (v1.4.8) for what PlayerEncounter.Init and
>    Finish do to PlayerSide.
> 2. Confirm by reproducing: raid a village with the steward on vs off (or its village hook off).
> 3. Fix: never finish, restart or re-enter the player's encounter while he is at a settlement,
>    especially during a hostile action / raid / MapEvent; skip or defer the steward's village action
>    until the encounter is idle. Log what it does so a repeat is visible.
> 4. Tell me whether save fst2 can be repaired (leaving to the map and re-entering should reset
>    PlayerSide), or whether I should load the save before it.

## Manager's first look (2026.09.29)
Smart Steward's own code never calls PlayerEncounter / EnterSettlement / LeaveSettlement / SwitchToMenu — its menu
touches are the "Party Steward" option (town, village, port) and the LeaveGuard wrapping village/leave,
leave_set_sail, leave_at_sea (+ the town's), whose "Leave anyway" re-runs the option via RunConsequencesOfMenuOption.
The executor's only settlement call is EnterSettlementAction.ApplyForPrisoner (donating prisoners, towns/castles).
The in-game test with Smart Steward OFF has not been done yet.
