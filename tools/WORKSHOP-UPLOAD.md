# Smart Steward on the Steam Workshop

Release target: **Steam Workshop only** (no Nexus). The upload is Anton's call on release day — nothing in
this repo runs it by itself.

The upload path is Bannerlord's **own official uploader** —
`TaleWorlds.MountAndBlade.SteamWorkshop.exe` in the game's `bin\Win64_Shipping_Client`.
It rides the **already-logged-in Steam client**: no SteamCMD, no password, no Steam Guard.
Just have Steam running and logged in. (Proven twice: ImmersiveAI and TrainingBattles ship this way.)

Item: **3817263044** — https://steamcommunity.com/sharedfiles/filedetails/?id=3817263044 (created 2026-10-10). `WorkshopCreate.xml` made it (once — never again), then `WorkshopUpdate.xml` forever, through
`make_workshop_update.py` (same flow as `..\better-skirmisher-separation`, item 3811452468).

## Release day — the first upload (once)

1. **Version bumped** (done 2026-10-10): `module\SubModule.xml` is **v1.0.0** — the release rhythm: the
   version moves only on release day.
2. **Images** (done 2026-10-10; redo only with new shots):
   ```powershell
   python tools\make_screenshots.py   # Toni's F12 shots -> Screenshots\01..07_*.jpg, cropped to the window, < 1 MB each
   python tools\make_thumbnail.py     # Screenshots\preview_thumbnail.jpg (1024 square, the Workshop preview) + cover.jpg
   ```
   The numbers in the screenshot names are the gallery order (most telling first).
3. **Package a clean build** (from the repo root):
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\package.ps1
   ```
   Clean build (warnings fail it), tests, the three gates (loads without MCM · prefab vs this game ·
   references only what every player has), then `dist\SmartSteward` from scratch — **that folder is what
   gets uploaded** — plus `dist\SmartSteward_v1.0.0.zip` (the GitHub Release asset). It carries the REAL module
   identity (`SmartSteward` / "Smart Steward"), unlike deploy.ps1's `.Dev` copy. `-Force` rebuilds an existing zip.
4. **Check `tools\WorkshopCreate.xml`**: the game-version `<Tag>` against
   `<game>\bin\Win64_Shipping_Client\Version.xml` (v1.4.8). Visibility is **Private**; Image is the preview above.
5. **Create the item** from a scratch folder (the tool drops `steam_appid.txt` + `steam_workshop_uploader.txt`
   in the working directory), Steam open:
   ```powershell
   cd $env:TEMP
   & "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" "C:\Users\Asus ROG\Documents\GitHub\smart_steward\tools\WorkshopCreate.xml"
   ```
   Success = **"Item created. Item ID is …"** + **"Uploading done!"** in the output (the exit code lies —
   see the quirks). Put the id into `tools\WorkshopUpdate.xml` (`FILL_IN_AFTER_CREATE`) and the "Item:" line above.
6. **The description**, still Private — the full page from `STEAM-DESCRIPTION.bbcode` (measured under Steam's
   **8000 UTF-8 bytes**; the script checks again):
   ```powershell
   $task = python tools\make_workshop_update.py --notes "v1.0.0 - first release." --visibility Private
   & "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" $task
   ```
7. **On the item page** (Owner Controls — the uploader cannot do these): "Add/edit images & videos" → the
   gallery `Screenshots\01_…` to `07_…` in that order (each under 1 MB). Do **not** add MCM as a *Required item* —
   it is optional, the mod runs on its settings file alone; there is no other dependency.
8. **Public, last**: the step-6 command with `--visibility Public` (or `--no-description` to send only that).
9. **Close the loop in the repo**: the page link into README.md (`STEAM_WORKSHOP_URL`); TASKS_DONE entry; push.
   **Never run `WorkshopCreate.xml` again** — it would make a second item.

## The update loop (every later release, 3 steps)

1. **Package**: `powershell -ExecutionPolicy Bypass -File tools\package.ps1` (after bumping the version —
   the script refuses to overwrite an existing `dist\SmartSteward_<version>.zip`).
2. **Change notes** = what changed, in the players' words (the item's Change Notes tab; the SHIPPING NEXT lines
   of TASKS_TODO.md are the raw material). Keep them in the comment under `<ChangeNotes>` in `WorkshopUpdate.xml`.
   Bump the game-version `<Tag>` there if the supported game moved.
3. **Upload** (Steam open, from a scratch folder; `--no-description` keeps the page text as it is):
   ```powershell
   $task = python tools\make_workshop_update.py --notes "v1.0.1 - what changed."
   & "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" $task
   ```

## Uploader quirks (decompiled 2026.07.13 for ImmersiveAI — trust these)

- The root `<Tasks>` element must be the **first node** of the task file. An `<?xml?>`
  declaration or a comment above it makes the tool parse ZERO tasks and exit as if
  successful. Comments are only safe INSIDE `<GetItem>`/`<UpdateItem>`.
- The item **title** comes from the module's `SubModule.xml <Name>` ("Smart Steward"), not the task file.
- The tool ends by writing `steam_workshop_uploader.txt` to the working directory and then
  **crashing on a harmless press-any-key read** when run non-interactively (exit code 82,
  `System.InvalidOperationException: Cannot read keys…`). Judge success by **"Uploading done!"**
  in the output, never by the exit code. (`.gitignore` covers the droppings if it is run from the repo.)
- `WorkshopUpdate.xml` alone touches the files, the change notes and the tags — **not** the title, description
  or visibility; `make_workshop_update.py` adds `<ItemDescription>` (the bbcode — so edit the FILE, not the page,
  or pass `--no-description`) and `<Visibility>`. A description in an XML attribute needs its line breaks as
  `&#10;` (a raw newline becomes a space); the script does that. The task file can NOT add extra screenshots or
  required items (no AddItemPreviewFile / AddDependency in the exe) — those are item-page only.
- Tags must be the Workshop's own filter names — Type: `Utility`, `UI` (others: Graphical Enhancement,
  Map Pack, Partial/Total Conversion, Sound, Troops, Weapons and Armour); Setting: `Native`; Game Mode:
  `Singleplayer`; Compatible Version: `v1.4.8` (the list on the Workshop browse page, 2026.09.28).

## Sanity check after upload

Steam re-downloads the item on its own schedule — the local copy under
`steamapps\workshop\content\261550\<id>` can show the old files for a while; that is not a failed upload.
Then: enable the plain **"Smart Steward"** in the launcher (not "Smart Steward (dev)" — never both at once:
the two carry the same DLL), load a save, look for "Smart Steward loaded." and open the Party Steward in a
town. That is the exact build subscribers get.
