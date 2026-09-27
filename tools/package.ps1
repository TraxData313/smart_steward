# Builds Smart Steward and assembles the CLEAN release layout for the Steam Workshop under dist\SmartSteward.
# Usage (Windows PowerShell - the gates load the game's .NET Framework DLLs):
#   powershell -ExecutionPolicy Bypass -File tools\package.ps1 [-Configuration Release] [-GameFolder <path>] [-Force] [-AllowWarnings]
#
# The same files deploy.ps1 puts into the game, but under the REAL module identity (Id "SmartSteward", name
# "Smart Steward" - no .Dev re-badge) and from scratch every time, so no stale file of an old build rides along.
# In order, and ANY failure stops it before dist is touched:
#   1. a clean Release build of the whole solution, warnings = errors (-AllowWarnings to let them pass);
#   2. every unit test;
#   3. tools\check-soft-deps.ps1 - the game loader's own GetTypes() test without MCM;
#   4. tools\check-gui.ps1 - the prefab against this game version and the built view models;
#   5. the REFERENCE gate (below) - every assembly our DLLs name must be one every player has;
#   6. dist\SmartSteward: SubModule.xml, bin\Win64_Shipping_Client\ (our two DLLs, nothing else), GUI, ModuleData;
#      every required piece checked, every XML parsed;
#   7. dist\SmartSteward_<version>.zip, the version read from module\SubModule.xml (refused if it already exists:
#      the version is bumped on release day - see CLAUDE.md - so an existing zip is a release's; -Force overwrites).
# The upload itself is tools\WORKSHOP-UPLOAD.md - never run from here.
param(
    [string]$Configuration = "Release",
    # Empty = read GameFolder from Directory.Build.props (and its git-ignored .user override).
    [string]$GameFolder = "",
    [switch]$Force,
    [switch]$AllowWarnings
)

$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSEdition -eq "Core") {
    throw "Run this with Windows PowerShell (powershell.exe): its gates need .NET Framework, like the game."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "SmartSteward.sln"
$moduleProject = Join-Path $repoRoot "src\SmartSteward.Module\SmartSteward.Module.csproj"
$manifestPath = Join-Path $repoRoot "module\SubModule.xml"
$distRoot = Join-Path $repoRoot "dist"
$moduleDir = Join-Path $distRoot "SmartSteward"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"
$outDir = Join-Path $repoRoot "src\SmartSteward.Module\bin\$Configuration"

# What ships, and nothing more. Game, SandBox, MCM and Newtonsoft DLLs come from the player's own install.
$shippedDlls = @("SmartSteward.dll", "SmartSteward.Core.dll")
$requiredFiles = @(
    "SubModule.xml",
    "bin\Win64_Shipping_Client\SmartSteward.dll",
    "bin\Win64_Shipping_Client\SmartSteward.Core.dll",
    "GUI\Prefabs\SmartStewardWindow.xml",
    "ModuleData\Languages\std_SmartSteward.xml"
)
# Optional modules our DLL may name. Each one MUST also be refused by check-soft-deps.ps1, which proves the module
# still loads without it. Anything else outside the game, the hard dependencies and .NET is a release blocker.
$optionalAssemblies = @("MCMv5")

function Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }

# ── 0. where things are, and which version this is ──────────────────────────────────────────────────────────
if (-not $GameFolder) {
    $GameFolder = "$(& dotnet msbuild $moduleProject -nologo -getProperty:GameFolder)".Trim()
    if ($LASTEXITCODE -ne 0 -or -not $GameFolder) { throw "Could not read GameFolder from Directory.Build.props." }
}
if (-not (Test-Path (Join-Path $GameFolder "Modules\Native\SubModule.xml"))) {
    throw "No Bannerlord install at '$GameFolder' - set GameFolder in Directory.Build.props.user or pass -GameFolder."
}
$gameBin = Join-Path $GameFolder "bin\Win64_Shipping_Client"

[xml]$manifest = Get-Content $manifestPath -Raw
$moduleId = $manifest.Module.Id.value
$moduleName = $manifest.Module.Name.value
$version = $manifest.Module.Version.value
if ($moduleId -ne "SmartSteward" -or $moduleName -ne "Smart Steward") {
    throw "module\SubModule.xml says Id '$moduleId' / Name '$moduleName' - the release must be 'SmartSteward' / 'Smart Steward'."
}
if ($version -notmatch '^v\d+\.\d+\.\d+$') { throw "module\SubModule.xml Version '$version' is not vX.Y.Z." }
$zipPath = Join-Path $distRoot "SmartSteward_$version.zip"
if ((Test-Path $zipPath) -and -not $Force) {
    throw ("dist\SmartSteward_$version.zip already exists - bump the version in module\SubModule.xml for the " +
           "release (CLAUDE.md: release rhythm), or pass -Force to overwrite.")
}
$gameVersion = "unknown"
try { $gameVersion = ([xml](Get-Content (Join-Path $gameBin "Version.xml") -Raw)).Version.Singleplayer.Value } catch { }
Write-Host "Packaging Smart Steward $version (game $gameVersion at $GameFolder)"

# ── 1. clean build ──────────────────────────────────────────────────────────────────────────────────────────
Step "1/7 Clean $Configuration build"
$buildArgs = @("build", $solution, "-c", $Configuration, "--no-incremental", "--nologo")
if (-not $AllowWarnings) { $buildArgs += "-warnaserror" }
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) {
    throw ("Build failed" + $(if ($AllowWarnings) { "." } else { " (warnings count as errors here; -AllowWarnings lets them pass)." }))
}

# ── 2. tests ────────────────────────────────────────────────────────────────────────────────────────────────
Step "2/7 Unit tests"
& dotnet test $solution -c $Configuration --no-build --nologo
if ($LASTEXITCODE -ne 0) { throw "Tests failed." }

# ── 3 + 4. the game-loader gates (each its own Windows PowerShell process: they load assemblies for good) ───
Step "3/7 Soft-dependency gate (loads without MCM)"
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "check-soft-deps.ps1") -Configuration $Configuration -GameFolder $GameFolder
if ($LASTEXITCODE -ne 0) { throw "Soft-dependency gate failed: the module would not load for a player without MCM." }

Step "4/7 GUI gate (prefab vs this game and the built view models)"
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "check-gui.ps1") -Configuration $Configuration -GameFolder $GameFolder
if ($LASTEXITCODE -ne 0) { throw "GUI gate failed: the window's prefab would break or silently fail in game." }

# ── 5. the reference gate ───────────────────────────────────────────────────────────────────────────────────
# The War Sails lesson (TrainingBattles v1.3.0-1.3.3 would not start without the DLC): a DLL naming an assembly a
# player may not have is a release blocker unless it is deliberately optional. Every reference must resolve to:
# one of our shipped DLLs, .NET Framework, the game's bin, the bin of a HARD dependency in SubModule.xml (never an
# optional one - StoryMode, War Sails), or an $optionalAssemblies entry. A game assembly must also not be named at a
# higher version than the install has. (This is what TrainingBattles' AssemblyGuard adds over our GetTypes gate,
# without its metadata walk: check-soft-deps.ps1 already runs the loader's real GetTypes().)
Step "5/7 Reference gate (nothing a player might lack)"
$runtimeDir = [Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()
$hardModules = @($manifest.Module.DependedModules.DependedModule | Where-Object { $_.Optional -ne "true" } | ForEach-Object { $_.Id })
$providedDirs = @($gameBin) + @($hardModules | ForEach-Object { Join-Path $GameFolder "Modules\$_\bin\Win64_Shipping_Client" })
$problems = @()
foreach ($dll in $shippedDlls) {
    $path = Join-Path $outDir $dll
    if (-not (Test-Path $path)) { throw "The build made no $dll in $outDir." }
    # From bytes: nothing is locked, nothing is resolved.
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoad([IO.File]::ReadAllBytes($path))
    foreach ($ref in $assembly.GetReferencedAssemblies()) {
        $name = $ref.Name
        if ($shippedDlls -contains "$name.dll") { continue }
        if ($optionalAssemblies -contains $name) { Write-Host "  optional  $name ($dll) - proven soft by step 3"; continue }
        if (Test-Path (Join-Path $runtimeDir "$name.dll")) { continue }
        $found = $providedDirs | ForEach-Object { Join-Path $_ "$name.dll" } | Where-Object { Test-Path $_ } | Select-Object -First 1
        if (-not $found) {
            $problems += "$dll names $name $($ref.Version) - not in the game, a hard dependency or .NET: a player may not have it"
            continue
        }
        $have = [Reflection.AssemblyName]::GetAssemblyName($found).Version
        if ($ref.Version -gt $have) { $problems += "$dll names $name $($ref.Version) - the game has $have" }
        else { Write-Host "  game      $name $($ref.Version) ($dll)" }
    }
}
if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Host "FAIL $_" -ForegroundColor Red }
    throw "Reference gate failed."
}
Write-Host "OK   every reference is .NET, the game, a hard dependency ($($hardModules -join ', ')) or optional ($($optionalAssemblies -join ', '))"

# ── 6. the module folder ────────────────────────────────────────────────────────────────────────────────────
Step "6/7 dist\SmartSteward"
if (Test-Path $moduleDir) { Remove-Item $moduleDir -Recurse -Force }
New-Item -ItemType Directory -Force $binDir | Out-Null
Copy-Item $manifestPath $moduleDir
foreach ($dll in $shippedDlls) { Copy-Item (Join-Path $outDir $dll) $binDir }
foreach ($folder in "GUI", "ModuleData") {
    $source = Join-Path $repoRoot "module\$folder"
    if (-not (Test-Path $source)) { throw "module\$folder is missing." }
    Copy-Item $source (Join-Path $moduleDir $folder) -Recurse
}

$missing = @($requiredFiles | Where-Object { -not (Test-Path (Join-Path $moduleDir $_)) })
if ($missing.Count -gt 0) { throw "dist\SmartSteward is missing: $($missing -join ', ')" }
$binFiles = @(Get-ChildItem $binDir -File | ForEach-Object { $_.Name } | Sort-Object)
if (($binFiles -join "|") -ne (($shippedDlls | Sort-Object) -join "|")) {
    throw "bin\Win64_Shipping_Client holds [$($binFiles -join ', ')] - it must hold exactly [$($shippedDlls -join ', ')]."
}
$dllName = $manifest.Module.SubModules.SubModule.DLLName.value
if ($shippedDlls -notcontains $dllName) { throw "SubModule.xml loads '$dllName', which is not shipped." }
foreach ($xmlFile in Get-ChildItem $moduleDir -Recurse -File -Filter *.xml) {
    try { [xml](Get-Content $xmlFile.FullName -Raw) | Out-Null }
    catch { throw "$($xmlFile.FullName.Substring($moduleDir.Length + 1)) is not well-formed XML: $($_.Exception.Message)" }
}
$stray = @(Get-ChildItem $moduleDir -Recurse -File | Where-Object { $_.Extension -notin ".xml", ".dll" })
if ($stray.Count -gt 0) {
    throw "Unexpected files in dist\SmartSteward (only .xml and our .dll ship): $(($stray | ForEach-Object { $_.Name }) -join ', ')"
}
Get-ChildItem $moduleDir -Recurse -File | ForEach-Object {
    Write-Host ("  {0,-55} {1,8:N0} bytes" -f $_.FullName.Substring($moduleDir.Length + 1), $_.Length)
}

# ── 7. the zip ──────────────────────────────────────────────────────────────────────────────────────────────
Step "7/7 Zip"
# One SmartSteward/ folder at the root, so it unpacks straight into Modules\. Entries are written one by one with
# forward slashes: under Windows PowerShell both Compress-Archive AND ZipFile.CreateFromDirectory write backslashes
# into entry names (the zip spec wants '/'; some unzippers then make flat files named "bin\Win64...") - the check
# below caught exactly that.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
$stream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew)
try {
    $archive = New-Object IO.Compression.ZipArchive($stream, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in Get-ChildItem $moduleDir -Recurse -File) {
            $entryName = "SmartSteward/" + $file.FullName.Substring($moduleDir.Length + 1).Replace("\", "/")
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entryName,
                [IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    finally { $archive.Dispose() }
}
finally { $stream.Dispose() }
$zipProblem = $null
$zip = [IO.Compression.ZipFile]::OpenRead($zipPath)
try {
    $entries = @($zip.Entries | ForEach-Object { $_.FullName })
    $bad = @($entries | Where-Object { -not $_.StartsWith("SmartSteward/") -or $_.Contains("\") })
    $expected = (Get-ChildItem $moduleDir -Recurse -File).Count
    if ($bad.Count -gt 0) { $zipProblem = "The zip has entries outside SmartSteward/ or with backslashes: $($bad -join ', ')" }
    elseif ($entries.Count -ne $expected) { $zipProblem = "The zip holds $($entries.Count) files, the folder $expected." }
}
finally { $zip.Dispose() }
# A bad zip must not stay behind: it would pass for a release and block the next run.
if ($zipProblem) { Remove-Item $zipPath -Force; throw $zipProblem }
Write-Host ("  {0} ({1:N0} bytes, {2} files)" -f $zipPath, (Get-Item $zipPath).Length, $entries.Count)

Write-Host ""
Write-Host "Packaged Smart Steward $version for game $gameVersion" -ForegroundColor Green
Write-Host "  Workshop folder: $moduleDir"
Write-Host "  Zip:             $zipPath"
if ($version -like "v0.*") {
    Write-Warning "Version $version - a first release is usually v1.0.0: bump module\SubModule.xml on release day, then package again."
}
Write-Host "  Upload: tools\WORKSHOP-UPLOAD.md (Anton's call - this script never uploads)."
