# Builds Smart Steward and installs it into the game's Modules folder as the DEV module.
# Usage: powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 [-Configuration Release] [-GameFolder <path>]
#
# The dev install wears its own identity - folder and Id "SmartSteward.Dev", shown as
# "Smart Steward (dev)" in the launcher. Same convention as TrainingBattles.Dev / ImmersiveAI.Dev:
# it can sit beside a Steam Workshop copy and be picked deliberately (enable only ONE of the two).
# Settings and log are shared: Documents\Mount and Blade II Bannerlord\Configs\SmartSteward.
param(
    [string]$Configuration = "Release",
    # Empty = read GameFolder from Directory.Build.props (and its git-ignored .user override).
    [string]$GameFolder = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$moduleProject = Join-Path $repoRoot "src\SmartSteward.Module\SmartSteward.Module.csproj"

if (-not $GameFolder) {
    $GameFolder = "$(& dotnet msbuild $moduleProject -nologo -getProperty:GameFolder)".Trim()
    if ($LASTEXITCODE -ne 0 -or -not $GameFolder) { throw "Could not read GameFolder from Directory.Build.props." }
}
if (-not (Test-Path (Join-Path $GameFolder "Modules\Native\SubModule.xml"))) {
    throw "No Bannerlord install at '$GameFolder' - set GameFolder in Directory.Build.props.user or pass -GameFolder."
}

$moduleDir = Join-Path $GameFolder "Modules\SmartSteward.Dev"
$binDir = Join-Path $moduleDir "bin\Win64_Shipping_Client"

# The game loads module DLLs with Assembly.LoadFrom at startup and holds them until it quits -
# main menu included. A locked DLL means the game is running: stop here, loudly, before building.
if (Test-Path $binDir) {
    foreach ($dll in Get-ChildItem $binDir -Filter *.dll) {
        try { [System.IO.File]::Open($dll.FullName, 'Open', 'ReadWrite', 'None').Dispose() }
        catch {
            throw ("$($dll.Name) is locked - Bannerlord is running (it holds module DLLs from startup " +
                   "until it quits, main menu included). Quit the game, then deploy again.")
        }
    }
}

dotnet build $moduleProject -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

# A fresh bin every time: exactly what this build produced, no stale DLLs of renamed projects.
if (Test-Path $binDir) { Remove-Item $binDir -Recurse -Force }
New-Item -ItemType Directory -Force $binDir | Out-Null
$outDir = Join-Path $repoRoot "src\SmartSteward.Module\bin\$Configuration"
foreach ($name in "SmartSteward", "SmartSteward.Core") {
    Copy-Item (Join-Path $outDir "$name.dll") $binDir -Force
    # PDBs give the log's stack traces file and line numbers (dev install only).
    $pdb = Join-Path $outDir "$name.pdb"
    if (Test-Path $pdb) { Copy-Item $pdb $binDir -Force }
}

# The manifest, re-badged as the dev module. Only the FIRST Id/Name (the module's own) change;
# fail loudly if the patterns stop matching because SubModule.xml was reformatted.
$manifest = Get-Content (Join-Path $repoRoot "module\SubModule.xml") -Raw
$devManifest = ([regex]'<Id value="SmartSteward" />').Replace($manifest, '<Id value="SmartSteward.Dev" />', 1)
$devManifest = ([regex]'<Name value="Smart Steward" />').Replace($devManifest, '<Name value="Smart Steward (dev)" />', 1)
if ($devManifest -notmatch '<Id value="SmartSteward.Dev" />' -or $devManifest -notmatch '<Name value="Smart Steward \(dev\)" />') {
    throw "Could not re-badge module\SubModule.xml as the dev module - its Id/Name lines changed shape."
}
[System.IO.File]::WriteAllText((Join-Path $moduleDir "SubModule.xml"), $devManifest, (New-Object System.Text.UTF8Encoding $false))

# Module data folders ride along when they exist (prefabs from step 7, strings from step 9).
# Remove-then-copy: Copy-Item onto an existing folder would nest GUI\GUI inside it.
foreach ($folder in "GUI", "ModuleData") {
    $source = Join-Path $repoRoot "module\$folder"
    $dest = Join-Path $moduleDir $folder
    if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
    if (Test-Path $source) { Copy-Item $source $dest -Recurse }
}

Write-Host "Deployed to $moduleDir as 'Smart Steward (dev)' - enable it in the launcher."
Get-ChildItem $moduleDir -Recurse -File | ForEach-Object { Write-Host ("  " + $_.FullName.Substring($moduleDir.Length + 1)) }
