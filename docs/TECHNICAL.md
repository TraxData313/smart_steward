# Smart Steward — technical notes

The player-facing page is the [README](../README.md) and the Steam page
[tools/STEAM-DESCRIPTION.bbcode](../tools/STEAM-DESCRIPTION.bbcode). This file is for building and changing the mod.

## Building

- **.NET 8 SDK** (builds the net472 module and the netstandard2.0 core — no Visual Studio needed).
- Bannerlord and [MCM v5](https://steamcommunity.com/sharedfiles/filedetails/?id=2859238197) installed:
  their DLLs are referenced from the install, never copied. If yours live elsewhere than the Steam defaults
  in `Directory.Build.props`, create a git-ignored `Directory.Build.props.user` beside it:
  ```xml
  <Project><PropertyGroup>
    <GameFolder>D:\Games\Mount &amp; Blade II Bannerlord</GameFolder>
    <McmBinFolder>D:\Games\...\2859238197\bin\Win64_Shipping_Client</McmBinFolder>
  </PropertyGroup></Project>
  ```

```powershell
dotnet build -c Release          # warning-free
dotnet test                      # the Core's unit tests
powershell -ExecutionPolicy Bypass -File tools\deploy.ps1         # install as "Smart Steward (dev)" (quit the game first)
powershell -ExecutionPolicy Bypass -File tools\check-soft-deps.ps1  # still loads without MCM?
powershell -ExecutionPolicy Bypass -File tools\check-gui.ps1        # the window's prefab vs this game
powershell -ExecutionPolicy Bypass -File tools\package.ps1          # all of it + dist\SmartSteward and a zip
```

## Release images

```powershell
python tools\make_screenshots.py   # Toni's F12 shots -> Screenshots\01..07_*.jpg, cropped to the window (gallery order)
python tools\make_thumbnail.py     # Screenshots\preview_thumbnail.jpg (Workshop preview) + Screenshots\cover.jpg (README)
```

Uploading is [tools/WORKSHOP-UPLOAD.md](../tools/WORKSHOP-UPLOAD.md) (the game's own Workshop uploader).

## The docs

| File | What it holds |
|---|---|
| [DESIGN.md](DESIGN.md) | the spec — every feature, every setting with its default, the algorithms |
| [RESEARCH.md](RESEARCH.md) | verified game-API facts from the decompiled v1.4.8 code, and the gotchas |
| [PLAYTEST.md](PLAYTEST.md) | the in-game checklists, step by step |
| [TASKS_TODO.md](../TASKS_TODO.md) / [TASKS_DONE.md](../TASKS_DONE.md) | the board / the real changelog |
| [CLAUDE.md](../CLAUDE.md) | how the work is done here, and the repository layout |
| [concept.txt](../concept.txt) | the original idea, in the author's words |

## Translations

Welcome. Every player-facing text is in
[module/ModuleData/Languages/std_SmartSteward.xml](../module/ModuleData/Languages/std_SmartSteward.xml) (English,
the game's own strings format). Copy it into `Languages\XX\`, translate the texts, add a `language_data.xml` —
the file's header says exactly how. The English there is generated from the code, so edit a translation's
copy, never the source.

## Logs and settings

`Documents\Mount and Blade II Bannerlord\Configs\SmartSteward\` holds `settings.json` (every key commented: meaning,
default, range; delete it to go back to defaults) and `smart_steward.log` (what was planned and done — attach it to a
bug report).
