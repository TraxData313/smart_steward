# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

**Smart Steward** — a mod for *Mount & Blade II: Bannerlord* (game v1.4.8, War Sails era) that
takes the pesky logistics chores off the player: keeping food (with variety), pack animals,
riding mounts for the footmen, war mounts for pending upgrades, ransoming prisoners and selling
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
  Items the player LOCKED in the inventory screen are never sold.
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
                              on change, Changed)
  Snapshot/                   StewardSnapshot — the game-free input the Module fills; LootGroups table;
                              GameRules = the pure rules the adapter applies (stack key item|modifier, lock id,
                              item kind, upgrade-ready count, average prices, donate rule, wounded first)
  Pricing/                    IPriceOracle (the Module implements it with the game's price model),
                              PriceBook rules, MarketState + TradeLane/LaneCursor = the price walk
  Planning/                   StewardPlanner.Plan(snapshot, settings, oracle) → StewardPlan
                              (sections → rows with their lanes, totals, facts); one planner per job;
                              PlanWalk = the picking rules the planner and the editor share; MoneyFloors + PlanMode =
                              the floors (window, or autonomous: raised to AutonomousMinGold, no tavern). The plan is
                              edited in place (PlanEditing: Increase/Decrease/Reset, live EditBlock per
                              button; PlanReplay re-walks it) and yields Transactions for the executor;
                              PlanReport = the plan as text for the log
  Execution/                  ExecutionBudget (per-unit purse / market gold / row price limit / the autonomous
                              floor, hire rules),
                              TransactionOutcome + ExecutionReport (real prices, drift, why it stopped, log lines),
                              AutonomousReport (the autonomous steward's message-log lines, words from outside)
  Presentation/               the window's pure half (step 7): UiFormat (numbers in the fonts' glyphs, typed-number
                              parsing), UiColors, UiInput (Shift/Ctrl → EditSize), RowCells (the Suggestion columns),
                              PlanFooter (warnings, CanExecute), PriceBookEditor + PriceRowView (Prices tab),
                              SettingEdit + PrisonerTicks (Instructions tab). Planning/PlanCarryOver = edits kept over a re-plan
src/SmartSteward.Module/      net472 → SmartSteward.dll — game glue: SubModule (entry point),
                              SmartStewardBehavior (SyncData stores nothing; forwards the campaign events to
                              StewardTriggers), ModLog, SettingsHost (the one
                              SettingsService over settings.json — read Current afresh, a reload replaces it),
                              McmBridge (Mod Options via MCM's fluent builder; MCM types only in method
                              bodies/signatures — read its class comment before touching it)
  Adapter/                    (namespace SmartSteward.Adapter — NOT .Game, it hides TaleWorlds.Core.Game)
                              SnapshotBuilder (live game → StewardSnapshot + GameVisit), GamePriceOracle (the
                              game's model; SelfCheck vs the trade screen), PlanExecutor (Transactions through
                              vanilla's paths — headless InventoryLogic, SellPrisonersAction, donate, hires),
                              StewardMerchantListener, PriceBookCatalog (the Prices tab's items + placeholders), GameVisit
  UI/                         the Party Steward window (step 7): StewardWindow (the layer at order 305, keys, Escape,
                              Encyclopedia focus, close), StewardWindowVM (tabs, Do it, re-plan, Guard around every
                              command), SuggestionVMs, PricesVMs, InstructionsVMs, HintVM, UiText/UiLabels (TextObject ids —
                              every English ONE literal per UiText call: StringsFileTests reads them from the source)
  StewardMenu.cs              "Party Steward" in the town and village menus → opens the window
  StewardTriggers.cs          (step 8) per-visit memory: arrival popup on a QUIET map, the leave warning (LeaveGuard wraps
                              the leave options lazily at their menu's first opening), the IsSettlementBusy veto
  AutonomousRun.cs            (step 8) the Full-autonomous steward: autonomous plan → executor → message-log report
tests/SmartSteward.Core.Tests/  net8.0 xUnit (keep green) — incl. SubModule.xml ↔ ModInfo and
                              StewardSettings + SettingsRegistry ↔ DESIGN §7 checks (keys, order, groups,
                              defaults, ranges); Settings/ = registry, file and service tests;
                              Planning/TestKit.cs = FakeOracle + Scenario builder for planner tests;
                              Planning/PlanPerformanceTests = a big late-game plan, open + clicks timed;
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
                              against the game and the built DLL — run it after touching a prefab or a view model
tools/McmProbe/               drives MCM's real fluent builder with the built bridge, outside the game
                              (not in the .sln; `dotnet run` it in Release after a Release build)
tools/package.ps1             (step 10) clean release layout + zip for the Workshop upload
docs/                         DESIGN.md, RESEARCH.md, PLAYTEST.md (Anton's checklists per step)
```

Log: `Documents\Mount and Blade II Bannerlord\Configs\SmartSteward\smart_steward.log`; the
settings file `settings.json` (+ `settings.json.bak` after a repair) lives beside it.

Conventions carried over from the sibling mods: **Core = pure and unit-tested, Module = game
glue**; game DLLs and MCM are referenced with `Private=false` (never shipped); raw game-API
research goes through `..\reference\game-decompiled-1.4.8\` (or `ilspycmd` on the real DLLs,
with `$env:DOTNET_ROLL_FORWARD='LatestMajor'`, when something is missing). **Quit the game before
deploying** — it holds module DLLs from startup, main menu included, and deploy.ps1 refuses.

## Environment

- Game: `C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (v1.4.8,
  War Sails / NavalDLC installed — do not reference it; the steward works at settlements only).
- MCM v5: Steam Workshop id 2859238197 (decompiled copy in `..\reference\MCMv5-5.12.3-decompiled`).
- .NET SDK 8 on the machine; Core targets netstandard2.0, Module net472.
- GitHub: `github.com/TraxData313/smart_steward` (public).
