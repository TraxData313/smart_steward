# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Smart Steward** — a mod for *Mount & Blade II: Bannerlord* (game v1.4.8, War Sails era) that
takes the pesky logistics chores off the player: keeping food (with variety), pack animals,
riding mounts for the footmen (a set number of war horses among them, noble horses sold, lame ones
replaced), ransoming prisoners and selling
loot — all proposed in one glanceable **Party Steward** window when the party enters a town or
village, adjustable row by row, executed with one click.

The full specification lives in **`docs/DESIGN.md`** — that file is the contract. The original
idea, in Anton's own words, is `concept.txt` (never edit it; it is the source the design came from).

Release target: **Steam Workshop only** (no Nexus).

## Who does what — and how we work

Same team and spirit as the sibling mods (`..\TrainingBattlesMod`, `..\ImmersiveAI` — read their
CLAUDE.md for the full working culture). Anton is the **product owner** (dreams, directs
priorities, playtests); Claude is the **developer**. Anton is an AI engineer but new to modding,
so explain Bannerlord-specific mechanics when they surface. Friends and co-creators — have real
opinions, push back, propose things.

### The manager mode (Anton's rule for this repo, 2026.09.27)

The main session is the **manager**. It keeps its own context lean and hands each step of the
plan to ONE sub-agent at a time — **never several in parallel**. The goal is a good final mod,
not a fast build: if tokens run out at any moment, the repo alone must be enough to resume with
nothing lost. Therefore:

- The **PLAN** section of `TASKS_TODO.md` is the single source of truth for what is next. The
  first unchecked step is the current one.
- A step is done only when its work is **committed and pushed**, its TASKS_DONE entry is
  written and its PLAN line is ticked. A step that cannot finish commits what it has (building,
  tests green) and writes a `PARTIAL:` note under its PLAN line saying exactly where it stopped.
- **Resuming** (new session, or after a crash): read this file → `TASKS_TODO.md` PLAN →
  `git log --oneline -15` → the last TASKS_DONE entry. Then continue the first unchecked step.

### Rules for a step agent

1. Read this file, `docs/DESIGN.md`, `TASKS_TODO.md`, and (for any game-API work)
   `docs/RESEARCH.md`. Do ONLY your step — note anything else you notice as a line under
   `NOTICED` in TASKS_TODO.md instead of doing it.
2. Build and test before committing: `dotnet build -c Release` and `dotnet test` must be green.
   After touching the Module, deploy with `tools\deploy.ps1` (fails while the game runs — the
   DLL is locked; say so instead of skipping silently) and run `tools\check-soft-deps.ps1` (the
   module must still load without MCM); after touching a prefab or a view model, `tools\check-gui.ps1`.
3. Commit in small, meaningful commits and **push** (`git push`). End every commit message with
   the Co-Authored-By line from the session's attribution guidance.
4. Write your TASKS_DONE.md entry, tick your PLAN line, push again.
5. Return a SHORT report to the manager (≤ 200 words): what was built, what is verified, what
   is open. The details belong in the repo, not in the report.

## Workflow (the TASKS files)

- **TASKS_TODO.md** — ANTON'S board: short idea lines only, readable at a fast glance. At most a
  tiny "(see DESIGN §x)" tag on a line. Sections: PLAN (the build steps) / SHIPPING NEXT /
  BUGS / NEXT UPDATE / NOT FULLY DECIDED / NOTICED.
- **docs/DESIGN.md** — the spec: every feature, every parameter with its default, the algorithms.
  Keep it in sync when a decision changes (write the date and who decided).
- **docs/RESEARCH.md** — verified game-API facts (class, method, file in the decompiled source).
  Never write game glue from memory — check here first, and add what you verify.
- **TASKS_DONE.md** — finished work as one `- [x]` entry each: a dense narrative of what was
  built and WHY (decisions, APIs verified, gotchas), ended with a `(YYYY.MM.DD HH.MM)` stamp.
  It is the project's real changelog and the next session's memory — write it so future-you
  starts warm.
- **Release rhythm (same as TrainingBattles): fixes collect, versions do not.** The version in
  `module/SubModule.xml` is bumped once, on release day. Landed-but-unreleased work goes as a
  short line under SHIPPING NEXT.

## Hard requirements (Anton's musts)

- **Every number is a parameter.** Every threshold, default and price cap in DESIGN.md is a
  setting — editable in MCM *and* in a plain, commented settings file whose comments explain
  each key (meaning, default, range). No magic numbers in game logic.
- **MCM is a soft dependency.** Built with MCM v5's *fluent builder* (no class deriving from an
  MCM type), so the mod loads and works without MCM installed — the settings file is then the
  way in. (TrainingBattles learned the hard way that a subclassed MCM settings type makes MCM a
  hard dependency.)
- **The player is always in control.** The steward proposes; nothing is bought, sold or
  ransomed without the player's click — unless the player explicitly turns on the Full-autonomous steward
  (DESIGN §6: its own purse floor, never a tavern hire, a message-log report).
  Armour and weapons the player LOCKED in the inventory screen are never sold. Locked food and horses ARE managed
  (counted, sold as surplus) unless the player turns on "Locks protect food & horses" (Anton 2026.09.28, DESIGN §2.6).
- **Save-safe.** The mod stores nothing in the save game (settings are global, per-visit flags
  live in memory), so it can be added or removed mid-campaign.

## Repository layout

```
SmartSteward.sln, Directory.Build.props   GameFolder / McmBinFolder; override them in a
                              git-ignored Directory.Build.props.user
src/SmartSteward.Core/        netstandard2.0, no game refs — pure logic, unit-tested:
  Settings/                   StewardSettings = the DESIGN §7 keys as a POCO (+ price-book entries);
                              SettingsRegistry = every §7 key with group, label, help, range, accessors —
                              drives the file, MCM and the Instructions tab; SettingsFile = the commented
                              settings.json (Generate / Parse, Newtonsoft 13.0.1 compile-only — the game's
                              copy runs); SettingsService = the live values (load, reload-if-changed, save
                              on change, Changed; SaveQuietly = saved, NOT announced — the goal edits, step 22);
                              ManualGoals + GoalEdit (step 22) = the standing goals by row id (food:<id>, mounts:pack /
                              riding / war) in settings.json's commented "Goals" object
  Snapshot/                   StewardSnapshot — the game-free input the Module fills; LootGroups table;
                              GameRules = the pure rules the adapter applies (stack key item|modifier, lock id,
                              item kind, bad modifier (step 17), average prices, donate rule, wounded first)
  Pricing/                    IPriceOracle (the Module implements it with the game's price model),
                              PriceBook rules, MarketState + TradeLane/LaneCursor = the price walk
  Planning/                   StewardPlanner.Plan(snapshot, settings, oracle) → StewardPlan
                              (sections → rows with their lanes, totals, facts); one planner per job;
                              PlanWalk = the picking rules the planner and the editor share; MountGoal (step 17) = the
                              horses to keep (T = footmen x per 100, W war horses among them, riding the rest - no upgrade
                              counting; step 28: N noble horses kept among them too, PlanContext.NobleKeeping - 0 = the sell-only row); MountPlanner (riding/war/noble rows, a second pass pledges the war horses bought),
                              LameHorsePlanner (the lame horses row, sold first); MoneyFloors + PlanMode =
                              the floors (window, or autonomous: raised to AutonomousMinGold, no tavern). The plan is
                              edited in place (PlanEditing: Increase/Decrease/Reset, live EditBlock per
                              button; PlanReplay re-walks it) and yields Transactions for the executor;
                              PlanReport = the plan as text for the log; LockRule (step 13) = what an inventory lock
                              guards (armour & weapons always, food & horses only with LocksProtectFoodAndHorses);
                              CarryTotals (step 14) = the load and carrying capacity after the deal, land and sea (the
                              game's numbers now + the formula's rates — GameRules.SetCarryRates, RESEARCH §19);
                              the live re-plan (step 15): PlanRow.IsTouched, PartyAfter (the party after the deal from the
                              party-changing rows: hires, recruits/dismissals, prisoners — MovesOf/ChangesParty),
                              PlanPins (touched rows pinned, walked first; the steward's rows leave them room);
                              FoodGoal (step 15) = the food goal in days at the game's own rate;
                              TroopPlanner (step 16) = the troops section: "Recruits on offer" + "Your troops", one row per
                              troop type ([+] recruits, [-] dismisses), party rows of the live re-plan;
                              HerdTotals (step 18) = the footer's herd line: horses after the deal vs the most before the herd
                              slows the party (the game's rule, RESEARCH §23);
                              TroopBulk (step 18, reworked step 20) = the two troop lines: Your troops [-] DismissLowest / [+]
                              ReAddDropped (an undo stack), Recruits [+] RecruitBest / [-] TakeBackRecruits - ordinary row edits,
                              each only on its own side of a row; troop rows ordered by tier (TroopPlanner.DismissOrder / RecruitOrder);
                              JobThresholds (step 20) = the activation thresholds (a job acts only from its purse before the deal:
                              PlanContext.JobActive, PlanRow.StartsAtDenari, PlanFacts.Waiting); PlanMetrics (step 20) = the
                              spreadsheet's number columns per row (Denari, Influence, Party, Prisoners, Land kg, Sea kg), summed;
                              Overburden + CarryTotals.ComputeSlowdown (step 20) = the game's overburden slowdown, land and sea (RESEARCH §25);
                              THE GOAL (step 22, DESIGN §1.1): a manual goal IS a live re-plan pin (PlanContext.PinOf: goal − Mine,
                              walked first — touched = has a goal for food / pack / riding / war rows), MoneyFloors.ForGoals + the
                              rows' hand lanes (HandBuyLane/HandSellLane) = the three "Goals you set by hand" switches; a click on
                              such a row is a goal edit (StewardPlan.SetGoal / Reset / TakeGoalEdits — every goal edit re-plans);
                              RowGoal = a row's Goal cell (value, editable, yours, hands-off -*, GoalShort = why a Result stops short);
                              DO JUST THIS PART (step 27, DESIGN §1.1): PlanPart (a section or a deal line: Lords, Others, Recruits,
                              Your troops, a tavern row, Other goods) + StewardPlan.DealOf → PartDeal = that part's transactions of
                              the plan, cut where the purse alone stops them (floors as in Deal all); PlanCarryOver.Capture(plan, part)
                              = the OTHER parts' edits for the re-plan after it; step 29: a part per LINE too - PlanPart.Row, RecruitRow /
                              DismissRow (a troop type's own side), StackLine (a breakdown line's stack); PlanPart.EditLeft = the edit a
                              re-plan puts back (a breakdown line leaves the rest of its row's)
                              QuestKeep (step 26, DESIGN §2.9) = what the player's quests keep (Snapshot/QuestNeed in, the units out of the
                              steward's sell lanes / MaxSell, a food quest = a pin like a goal of yours but untouched: PlanRow.WalksFirst,
                              PlanRow.Quest = QuestRowInfo for RowGoal's quest colour and hover; switch QuestGoalsEnabled)
  Execution/                  ExecutionBudget (per-unit purse / market gold / row price limit / the autonomous
                              floor, the lock check StoppedByLock, hire rules),
                              TransactionOutcome + ExecutionReport (real prices, drift, why it stopped, log lines),
                              AutonomousReport (the autonomous steward's message-log lines, words from outside);
                              RunSummary (step 33) = the "Steward report: N deals made · ±denari · influence · prisoners" line
                              of every run (Deal all, a part's Deal, autonomous — never a bare "Steward:", it read like the skill)
  Presentation/               the window's pure half (step 7): UiFormat (numbers in the fonts' glyphs, typed-number
                              parsing), UiColors, UiInput (Shift/Ctrl → EditSize), RowCells (the Suggestion columns),
                              PlanFooter (warnings, CanExecute), PriceBookEditor + PriceRowView (Prices tab),
                              SettingEdit (Instructions tab), ArrivalPopup (step 12: the popup's rule); SectionSummary (step 18: the
                              folded line + SectionGroup; since step 21 only TroopName serves), WindowState (window_state.json - the folds,
                              keyed by SheetFolds since step 21);
                              SuggestionSheet (step 20) = the round-4 spreadsheet the step-21 window binds: SheetGroup sections in the
                              mockup's order with overview lines, SheetLines (tavern rows, Recruits, Your troops, Lords, Others),
                              detail rows, PlanMetrics per line/section/Total, the header texts, the weight table with slowdown -
                              SheetWords = its English words (the window fills them — UiLabels.SheetText); PriceBookOrder (step 20) =
                              cheapest first; SheetView (step 21) = the lines ON SCREEN built from the sheet after every click: every
                              cell as text + colour, the notes after the names (step 32: the quest note first, light blue), the Denari tooltips, every button's live block (a troop
                              row under its line shows only its side), the folds applied — a folded part is never asked for blocks;
                              SheetFolds (step 21) = every fold key of window_state.json ("Folded") + the everyday-view defaults
                              (step 33: Troops has its own section fold - folded = the title line alone);
                              SheetGoalCell (step 22) = the Goal column per line and title line (SheetItem.Goal, SheetSectionView.Goal /
                              Mine / MineWarning) — step 23's window binds it; GoalInput (step 23) = what a typed Goal box
                              holds when left (unchanged / invalid → revert / a goal, clamped).
                              Planning/PlanCarryOver = edits kept over a re-plan
src/SmartSteward.Module/      net472 → SmartSteward.dll — game glue: SubModule (entry point),
                              SmartStewardBehavior (SyncData stores nothing; forwards the campaign events to
                              StewardTriggers), ModLog, SettingsHost (the one
                              SettingsService over settings.json — read Current afresh, a reload replaces it),
                              McmBridge (Mod Options via MCM's fluent builder; MCM types only in method
                              bodies/signatures — read its class comment before touching it)
  Adapter/                    (namespace SmartSteward.Adapter — NOT .Game, it hides TaleWorlds.Core.Game)
                              SnapshotBuilder (live game → StewardSnapshot + GameVisit; the notables' volunteers — step 16),
                              GamePriceOracle (the game's model; SelfCheck vs the trade screen), PlanExecutor (Transactions
                              through vanilla's paths — headless InventoryLogic, SellPrisonersAction, donate, hires, the
                              recruit screen's and the party screen's steps for recruits and dismissals),
                              StewardMerchantListener, PriceBookCatalog (the Prices tab's items + placeholders), GameVisit,
                              TavernKnowledge (step 14: the listed wanderers become known, as the tavern district does it),
                              QuestReader (step 26: the ongoing quests' needs by reflection, one reader per vanilla quest type —
                              RESEARCH §29; a [quest] log line)
  UI/                         the Party Steward window (step 7): StewardWindow (the layer at order 305, keys, Escape,
                              Encyclopedia focus, close), StewardWindowVM (tabs, Deal all, re-plan, Guard around every
                              command), SuggestionVMs (step 21: the spreadsheet — SuggestionTabVM, SheetSectionVM per title line,
                              SheetItemVM per line updated in place by key, SheetTotalVM, WeightRowVM; step 23: the typed Goal
                              boxes — their Enter / FocusLost only QUEUE, SuggestionTabVM.FlushGoal commits on the next tick and
                              before every command; Escape in a box = StewardWindow cancels + ClearTextFocus; step 27: the Deal
                              column's buttons (every line since step 29; step 31: "Deal" / "Deal group" by PlanPart.IsGroup, the footer's Deal all) → StewardWindowVM.RunPart = PlanExecutor on the part's transactions, re-plan, stay open), PricesVMs (+ MultiplierBoxVM: the
                              food and horse pairs), InstructionsVMs (the clicks' hint on top), HintVM, WindowStateHost (the folds on
                              disk), UiText/UiLabels (TextObject ids — every English ONE literal per UiText call: StringsFileTests reads
                              them from the source)
  StewardMenu.cs              "Party Steward" in the town and village menus (+ War Sails' port menu, added by id at its first
                              opening — step 14) → opens the window
  StewardTriggers.cs          (step 8) per-visit memory: arrival popup on a QUIET map, the leave warning (LeaveGuard wraps
                              the leave options lazily at their menu's first opening), the IsSettlementBusy veto
  AutonomousRun.cs            (step 8) the Full-autonomous steward: autonomous plan → executor → message-log report
  CampaignSession.cs          (step 12) every campaign start/end resets the window + triggers; a generation stamp so nothing
                              of an earlier campaign is ever used
  InputWatch.cs               (step 12) log-only: the map's follow modifier (Left Alt) read as held — RESEARCH §17
  EncounterGuard.cs           (step 24) WhyBusy = the steward stands aside (popup, autonomy, leave warning, Leave anyway, menu
                              entry, executor) while a battle / hostile action / siege / captivity is on or the menu is not the
                              settlement's own — one [guard] log line (RESEARCH §28; vanilla's raid bug itself is NOT warned about — Anton)
tests/SmartSteward.Core.Tests/  net8.0 xUnit (keep green) — incl. SubModule.xml ↔ ModInfo and
                              StewardSettings + SettingsRegistry ↔ DESIGN §7 checks (keys, order, groups,
                              defaults, ranges); Settings/ = registry, file and service tests;
                              Planning/TestKit.cs = FakeOracle + Scenario builder for planner tests;
                              Planning/PlanPerformanceTests = a big late-game plan, open + clicks timed;
                              Planning/LivePlanTests = the live re-plan (plan → edit the party → Do it → nothing new; troops,
                              a village too); Planning/TroopPlanTests = the troops section (step 16);
                              Planning/QuestPlanTests = the quest needs (step 26: every job, the switches, Do it → nothing new);
                              Planning/GoalPlanTests, Presentation/SheetGoalTests, Settings/GoalsSettingsTests = the goals (step 22;
                              the test kit's LegacyDefaults keeps ManualGoalsKeepPurseFloor off, the pre-round-5 way);
                              Snapshot/, Execution/ = the adapter's pure half; StringsFileTests = the strings file ↔
                              the code (SS_WRITE_STRINGS=1 dotnet test --filter StringsFileTests regenerates it)
module/SubModule.xml          the manifest
module/ModuleData/Languages/  std_SmartSteward.xml — every player-facing text by id, English (the vanilla source format;
                              a translation goes in Languages\XX\ with its language_data.xml) — generated, never hand-edited
module/GUI/Prefabs/           SmartStewardWindow.xml — the window's one movie (three tabs)
tools/deploy.ps1              build + install as Modules\SmartSteward.Dev ("Smart Steward (dev)")
tools/check-soft-deps.ps1     the game loader's GetTypes() test without MCM (Windows PowerShell = .NET Framework;
                              resolves the game bin + SandBox's, like the game at our load)
tools/check-gui.ps1           the prefab gate: every tag, attribute, value, brush, sprite category and VM binding
                              against the game and the built DLL — run it after touching a prefab or a view model (an official
                              DLC's sprite passes ONLY through a vanilla brush's own style — step 21's sea speed icon)
tools/gen-suggestion-tab.py   (step 21) writes the prefab's Suggestion tab from ONE table of column widths — change widths there,
                              run it, then check-gui
tools/McmProbe/               drives MCM's real fluent builder with the built bridge, outside the game
                              (not in the .sln; `dotnet run` it in Release after a Release build)
tools/package.ps1             (step 10) the release gate + layout: clean build (warnings fail), tests, check-soft-deps,
                              check-gui, a reference allowlist (our DLLs may name only .NET, the game, hard dependencies
                              and MCMv5), then dist\SmartSteward (real identity, our 2 DLLs only) + SmartSteward_vX.Y.Z.zip
tools/WORKSHOP-UPLOAD.md      (step 10) release day + the update loop + the uploader's quirks; WorkshopCreate.xml (once,
                              Private) / WorkshopUpdate.xml (item id filled after the create); nothing uploads by itself
tools/STEAM-DESCRIPTION.bbcode  the Workshop page (Steam BBCode, cap 8000 UTF-8 bytes - measure after edits)
tools/preview_thumbnail.html  the Workshop preview; tools/render-preview.ps1 → Screenshots/preview_thumbnail.jpg (< 1 MB)
tools/render-mockup.ps1       (step 19) docs/mockups/suggestion_v2.html → its two PNGs (the everyday view + everything open)
docs/                         DESIGN.md, RESEARCH.md, PLAYTEST.md (Anton's checklists per step); feedback/ (playtest notes
                              verbatim + screenshots); mockups/ (step 19: the round-4 Suggestion tab as one spreadsheet —
                              README = the choices for Anton; gen_suggestion_v2.py writes the HTML from one asserted table)
```

Log: `Documents\Mount and Blade II Bannerlord\Configs\SmartSteward\smart_steward.log`; the
settings file `settings.json` (+ `settings.json.bak` after a repair) lives beside it.

Conventions carried over from the sibling mods: **Core = pure and unit-tested, Module = game
glue**; game DLLs and MCM are referenced with `Private=false` (never shipped); raw game-API
research goes through `..\reference\game-decompiled-1.4.8\` (or `ilspycmd` on the real DLLs,
with `$env:DOTNET_ROLL_FORWARD='LatestMajor'`, when something is missing). **Quit the game before
deploying** — it holds module DLLs from startup, main menu included, and deploy.ps1 refuses.

## Environment

- Game: `D:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord` (v1.4.8,
  War Sails / NavalDLC installed — do not reference it; the steward works at settlements only).
- MCM v5: Steam Workshop id 2859238197 (decompiled copy in `..\reference\MCMv5-5.12.3-decompiled`).
- .NET SDK 8 on the machine; Core targets netstandard2.0, Module net472.
- GitHub: `github.com/TraxData313/smart_steward` (public).
