# Smart Steward on the Steam Workshop

Release target: **Steam Workshop only** (no Nexus). The upload is Anton's call on release day — nothing in
this repo runs it by itself.

The upload path is Bannerlord's **own official uploader** —
`TaleWorlds.MountAndBlade.SteamWorkshop.exe` in the game's `bin\Win64_Shipping_Client`.
It rides the **already-logged-in Steam client**: no SteamCMD, no password, no Steam Guard.
Just have Steam running and logged in. (Proven twice: ImmersiveAI and TrainingBattles ship this way.)

Item: **not created yet** — `WorkshopCreate.xml` makes it (once), then `WorkshopUpdate.xml` forever.

## Release day — the first upload (once)

1. **Playtest done, version bumped.** Anton's playtest (PLAN step 11) is green. Bump `<Version>` in
   `module\SubModule.xml` (v0.1.0 → **v1.0.0**) — the release rhythm: the version moves only today.
2. **Package a clean build** (from the repo root):
   ```powershell
   powershell -ExecutionPolicy Bypass -File tools\package.ps1
   ```
   Clean build (warnings fail it), tests, the three gates (loads without MCM · prefab vs this game ·
   references only what every player has), then `dist\SmartSteward` from scratch — **that folder is what
   gets uploaded** — plus `dist\SmartSteward_v1.0.0.zip`. It carries the REAL module identity
   (`SmartSteward` / "Smart Steward"), unlike deploy.ps1's `.Dev` copy.
3. **The preview image** `Screenshots\preview_thumbnail.jpg` must exist and be **under 1 MB** (Steam's cap).
   It is rendered from `tools\preview_thumbnail.html` (the one-liner is in that file's header). Better still:
   after the playtest, put a real in-game shot of the Party Steward window into the HTML's art slot, or
   upload real screenshots on the item page (step 6) — the rendered ledger is a stand-in.
4. **Check `tools\WorkshopCreate.xml`**: the game-version `<Tag>` against
   `<game>\bin\Win64_Shipping_Client\Version.xml` (v1.4.8 when this was written). Visibility is **Private**.
5. **Run the uploader** from a scratch folder (it drops `steam_workshop_uploader.txt` in the working
   directory), Steam open:
   ```powershell
   cd $env:TEMP
   & "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" "C:\Users\Trax\Documents\BannerlordMods\smart_steward\tools\WorkshopCreate.xml"
   ```
   Success = **"Item created. Item ID is …"** + **"Uploading done!"** in the output (the exit code lies —
   see the quirks). Write the id down.
6. **On the item page** (Owner Controls — Anton's hands):
   - paste `tools\STEAM-DESCRIPTION.bbcode` into the description (Steam caps it at **8000 UTF-8 bytes** —
     the file is measured under it; measure again after any edit: `[Text.Encoding]::UTF8.GetByteCount(...)`);
   - add screenshots / the window in action;
   - do **not** add MCM as a *Required item* — it is optional, the mod runs on its settings file alone;
   - look it over, then flip **Private → Public**.
7. **Close the loop in the repo**: put the id into `tools\WorkshopUpdate.xml` (`FILL_IN_AFTER_CREATE`) and
   into the "Item:" line above, the page link into README.md; TASKS_DONE entry; `git tag v1.0.0`; push.
   **Never run `WorkshopCreate.xml` again** — it would make a second item.

## The update loop (every later release, 3 steps)

1. **Package**: `powershell -ExecutionPolicy Bypass -File tools\package.ps1` (after bumping the version —
   the script refuses to overwrite an existing `dist\SmartSteward_<version>.zip`).
2. **Edit `tools\WorkshopUpdate.xml`**: `<ChangeNotes Value="…"/>` = what changed, in the players' words
   (the item's Change Notes tab; the SHIPPING NEXT lines of TASKS_TODO.md are the raw material). Keep the
   previous notes in the comment below it. Bump the game-version `<Tag>` if the supported game moved.
3. **Run the uploader** (Steam open, from a scratch folder):
   ```powershell
   cd $env:TEMP
   & "C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.SteamWorkshop.exe" "C:\Users\Trax\Documents\BannerlordMods\smart_steward\tools\WorkshopUpdate.xml"
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
- `WorkshopUpdate.xml` touches the files, the change notes and the tags — **not** the title, description
  or visibility (it has no `<ItemDescription>`/`<Visibility>`), so page edits are never stomped.
- Tags must be the Workshop's own filter names — Type: `Utility`, `UI` (others: Graphical Enhancement,
  Map Pack, Partial/Total Conversion, Sound, Troops, Weapons and Armour); Setting: `Native`; Game Mode:
  `Singleplayer`; Compatible Version: `v1.4.8` (the list on the Workshop browse page, 2026.09.28).

## Sanity check after upload

Steam re-downloads the item on its own schedule — the local copy under
`steamapps\workshop\content\261550\<id>` can show the old files for a while; that is not a failed upload.
Then: enable the plain **"Smart Steward"** in the launcher (not "Smart Steward (dev)" — never both at once:
the two carry the same DLL), load a save, look for "Smart Steward loaded." and open the Party Steward in a
town. That is the exact build subscribers get.
