# Manager progress — Smart Steward release (2026-10-10)

## Task (Toni)
"Smart Steward is tested and finished — post it like ..\better-skirmisher-separation: I took a few F12 screenshots
of the GUI; go live, post it public on git and live on Steam." = explicit green light for GitHub release + PUBLIC Workshop item.

Recipe = ..\better-skirmisher-separation\.manager\progress.md Phases 4-5 (README as short main page, Unlicense line,
"read my top pinned" thank-you, AI-authorship line, cover + thumbnail, GitHub Release with dist zip, Workshop via the game's
TaleWorlds.MountAndBlade.SteamWorkshop.exe riding the logged-in Steam client, extra images + required items via Chrome,
then Public).

Facts found by the manager:
- Screenshots: C:\Program Files (x86)\Steam\userdata\258577504\760\remote\261550\screenshots\20261010*_1.jpg (13 shots, 20:52-20:59).
- Repo github.com/TraxData313/smart_steward is ALREADY PUBLIC. No GitHub release yet. SubModule.xml version v0.1.0.
- No Workshop item yet (WorkshopUpdate.xml ItemId = FILL_IN_AFTER_CREATE; its ModuleFolder path still says C:\Users\Trax — old machine).
- Existing: tools/package.ps1, STEAM-DESCRIPTION.bbcode, WORKSHOP-UPLOAD.md, WorkshopCreate/Update.xml, preview_thumbnail.html, render-preview.ps1.

## Steps
1. [x] Release prep: version v1.0.0, README rewritten in the sibling's style, Steam description refreshed to today's features,
       screenshots copied into the repo + chosen, cover/thumbnail from the real GUI, workshop XML paths fixed, package.ps1 green. Commit + push.
2. [x] GitHub release v1.0.0 with the dist zip.
3. [x] Steam: create the item (Private) with description, then extra screenshots via Chrome, then Public; README/WorkshopUpdate get the item id. Commit + push.
4. [x] Board: TASKS_DONE entry, TASKS_TODO SHIPPING NEXT cleared / release noted.

## Log
- Step 1 done 2026-10-10 (commits b3785f3, e3bdb14, 4de24d8, 49f94b8, pushed). Nothing published.
  Screenshots: of the 13 F12 shots, 5 (20:52-20:54) are battle scenes of another mod -> not used; 20:59:12 (Horses
  open) is a subset of 20:58:32 -> not used. 7 kept, cropped to the window alone (box 660,74,2778,1366 of 3440x1440:
  the message log showed a Windows user path + ImmersiveAI's key warning, and other mods' widgets sat around it),
  2118x1292, 249-282 KB each, in gallery order: Screenshots\01_suggestion_food, 02_suggestion_troops_horses,
  03_instructions_general_money, 04_instructions_goals_food, 05_instructions_prices_pack_animals,
  06_instructions_mounts, 07_instructions_prisoners_loot_tavern (.jpg). Rebuild: python tools\make_screenshots.py.
  Images: python tools\make_thumbnail.py (headless Edge renders tools\preview_thumbnail.html + new tools\cover.html
  with shot 01 as art) -> Screenshots\preview_thumbnail.jpg (1024x1024, 154 KB; checked full size + 256 px: title and
  tagline read, the table reads as "a ledger") and Screenshots\cover.jpg (1600x900, 184 KB, README). render-preview.ps1
  and the CSS stand-in ledger removed. Title "Smart Steward", tagline "Your steward, finally doing the steward's job.",
  strip food - horses - troops - prisoners - loot.
  README = short page like the sibling's (cover, authorship line, features, install GitHub latest / STEAM_WORKSHOP_URL
  placeholder, 2 shots, Unlicense, "read my top pinned", <details> -> docs\TECHNICAL.md = old build/docs/translations
  text). STEAM-DESCRIPTION.bbcode refreshed (authorship line, goals, quests, castles = donations only, sibling's
  thank-you wording, ends with the GitHub Releases download line): 7,118 UTF-8 bytes. MCM optional, no required items.
  Version v1.0.0 in module\SubModule.xml (ModInfo has no version; tests green inside package.ps1).
  Workshop: WorkshopCreate/Update.xml paths -> C:\Users\Asus ROG\Documents\GitHub\smart_steward; Create Image =
  Screenshots\preview_thumbnail.jpg, Visibility Private; new tools\make_workshop_update.py (sibling's; checks bytes,
  refuses until WorkshopUpdate's ItemId FILL_IN_AFTER_CREATE is numeric; tested with a dummy id: parses, <Tasks> first,
  description 7,117 bytes); WORKSHOP-UPLOAD.md = create Private -> description via the script -> gallery 01..07 by
  hand -> Public last.
  package.ps1 green (build, tests, soft-deps, check-gui, allowlist) -> dist\SmartSteward (5 files) +
  dist\SmartSteward_v1.0.0.zip = 262,493 bytes. Game was not running.
  For step 2: gh release create v1.0.0 with dist\SmartSteward_v1.0.0.zip (no tag exists yet). For step 3: uploader from
  a scratch folder (WORKSHOP-UPLOAD.md steps 5-8), then ItemId into WorkshopUpdate.xml + STEAM_WORKSHOP_URL in README;
  gallery images via Chrome on the item page (01..07 in order). TASKS_TODO SHIPPING NEXT untouched (step 4).
- HOLD 2026-10-10: Toni asked for an overview (screens + README) BEFORE going live. Files sent to him; waiting for his OK
  or changes before step 2 (GitHub release) and step 3 (Steam).
- GO 2026-10-10: Toni approved as is ("go, release it on GitHub and Steam"). Steps 2-4 cleared to run.
- THEN Toni stopped it (before any release): "not clear at a glance that it AUTOMATES food, horses, prisoners...".
  New step 1b before 2: (1) thumbnail tagline "Food, horses, troops, prisoners — handled in one click.", strip as verbs
  [Toni chose v1 2026-10-10 and said "go live on GitHub and Steam"; v2 images to be removed, script variant kept]
  BUYS FOOD · REPLACES HORSES · RANSOMS PRISONERS · SELLS LOOT, window art dimmed, gold glow on Deal all; (2) first line of
  Steam desc + README = "Automates your party's logistics..."; (3) try a variant thumbnail with a before→after line. Show Toni again before going live.
- Step 1b done 2026-10-10 (commit 1e925cf, pushed). Nothing published. Thumbnail + cover rebuilt by
  python tools\make_thumbnail.py [--variant v2]: art = the WHOLE real window (shot 01) at brightness 0.5, its real
  Deal all button (source box 1815,1200-2063,1252) drawn again from the same pixels undimmed, x1.7 (cover x1.6), in a
  gold glow; title, tagline "Food, horses, troops, prisoners — handled in one click.", verbs BUYS FOOD · REPLACES
  HORSES / RANSOMS PRISONERS · SELLS LOOT (two lines on the thumbnail, four stacked on the cover, which is now art left
  / words right). Screenshots\preview_thumbnail.jpg 150 KB, cover.jpg 149 KB; v2 = + "5 days of food → 40 days ·
  12 prisoners → +3,400 denari" -> preview_thumbnail_v2.jpg 160 KB, cover_v2.jpg 159 KB. v2 figures are EXAMPLE
  numbers, not from any shot (the shots' party was already stocked: food ~39 » 40 days, no prisoners). Checked full
  size and 256 px: tagline, verbs and Deal all read; the v2 line reads at 256 but is small. README first line (bold,
  under the cover), STEAM-DESCRIPTION.bbcode first line ([b]...[/b], 7,238 bytes) and WorkshopCreate.xml
  ItemDescription now open "Automates your party's logistics...". dist zip unchanged (package.ps1 ships only
  SubModule.xml, the 2 DLLs, GUI\Prefabs, ModuleData\Languages - no images/docs). Toni picks v1 or v2 before step 2;
  if v2, point WorkshopCreate's Image (and README) at the _v2 files.
- Step 2 done 2026-10-10: v2 images removed (commit 877f7a9, make_thumbnail.py --variant v2 kept; README/WorkshopCreate/
  WORKSHOP-UPLOAD already point at cover.jpg / preview_thumbnail.jpg). dist zip checked current (v1.0.0, no src/module change
  since its build at 49f94b8). GitHub release https://github.com/TraxData313/smart_steward/releases/tag/v1.0.0 - tag v1.0.0
  on 877f7a9, title "Smart Steward v1.0.0", Latest, asset SmartSteward_v1.0.0.zip = 262,493 bytes. Nothing on Steam yet.
- Step 3 done 2026-10-10 (commit c2ab80e + this log). Workshop item 3817263044, PUBLIC:
  https://steamcommunity.com/sharedfiles/filedetails/?id=3817263044 . D: drive is gone - the game now lives in
  C:\Program Files (x86)\Steam\steamapps\common (Version.xml v1.4.8); WORKSHOP-UPLOAD.md's uploader path updated
  (Directory.Build.props + CLAUDE.md still say D:, the git-ignored .user override has C:). Uploader from scratchpad upload dir:
  WorkshopCreate.xml -> "Item created. Item ID is 3817263044" + "Uploading done!" (790,472 B, exit-82 crash as usual);
  id into WorkshopUpdate.xml, README STEAM_WORKSHOP_URL -> the item link, pushed. make_workshop_update.py --notes
  "v1.0.0 - first release." --visibility Private -> "Uploading done!" (full bbcode description), then the same with
  --visibility Public -> "Uploading done!". Web API GetPublishedFileDetails: result 1, visibility 0 (public), title
  Smart Steward, file_size 790,472, app 261550, tags Utility/UI/Native/Singleplayer/v1.4.8, description = the bbcode,
  not banned. No agreement prompt. OPEN: gallery 01..07 NOT added - Claude in Chrome was not connected (no browsers);
  needs Toni by hand or a later Chrome session (item page > Owner Controls > Add/edit images & videos). No required items.
- Step 4 done 2026-10-10: TASKS_DONE entry for v1.0.0, SHIPPING NEXT cleared (release noted + the gallery line open), NOTICED: D: paths in Directory.Build.props / CLAUDE.md. Release complete.

## Phase 2 (Toni 2026-10-10, after release): gallery added by Toni on Steam by hand.
5. [x] README: remake / rearrange closer to ..\better-skirmisher-separation's README style. Commit + push.
6. [x] Toni's GitHub profile README (TraxData313/TraxData313): add Smart Steward in the style of its existing entries. Commit + push.
- Step 5 done (manager, by hand): README in the sibling's order - cover, made-by, one prose paragraph (automation first), bold Settings line, Install, licence, thank-you; screenshots 01-03 moved into a <details>.
- Step 6 done 2026-10-10: profile README (TraxData313/TraxData313, commit a9b775e, pushed) - Smart Steward as a third row of
  the Bannerlord mods table, same cell shape as the others (Workshop-linked preview_thumbnail.jpg, repo link, the
  "Automates your party's logistics..." pitch, <sub>New</sub>); the stale "On the way: Smart Steward" line under the table removed.
