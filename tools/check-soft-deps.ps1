# The game loader's own test, run outside the game: can the built module load WITHOUT MCM?
# Usage (Windows PowerShell - it must be .NET Framework, like the game):
#   powershell -ExecutionPolicy Bypass -File tools\check-soft-deps.ps1 [-Configuration Release] [-GameFolder <path>]
#
# Bannerlord 1.4.8 loads a module DLL with Assembly.LoadFrom and calls GetTypes(); ONE type that cannot
# load (a base type, interface or field naming a missing assembly) unloads the WHOLE module (RESEARCH §12).
# This script does the same with the game's bin resolvable and MCMv5 refused, for SmartSteward.Core.dll
# and SmartSteward.dll. It also walks every STATIC method's signature: the game's
# CommandLineFunctionality.CollectCommandLineFunctions reflects over the static methods of every
# assembly that references TaleWorlds.Library, so no static method may name an MCM type either.
# Exit 0 = loads without MCM, 1 = it would not.
param(
    [string]$Configuration = "Release",
    [string]$GameFolder = ""
)

$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSEdition -eq "Core") {
    throw "Run this with Windows PowerShell (powershell.exe): the check needs .NET Framework, like the game."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$moduleProject = Join-Path $repoRoot "src\SmartSteward.Module\SmartSteward.Module.csproj"
if (-not $GameFolder) {
    $GameFolder = "$(& dotnet msbuild $moduleProject -nologo -getProperty:GameFolder)".Trim()
}
$gameBin = Join-Path $GameFolder "bin\Win64_Shipping_Client"
$outDir = Join-Path $repoRoot "src\SmartSteward.Module\bin\$Configuration"
if (-not (Test-Path (Join-Path $outDir "SmartSteward.dll"))) { throw "Build first: no SmartSteward.dll in $outDir." }
if (-not (Test-Path (Join-Path $gameBin "TaleWorlds.Library.dll"))) { throw "No game bin at $gameBin." }

# Resolve like the game does (its own bin, then the module's), and refuse MCM - as for a player without it.
$searchDirs = @($outDir, $gameBin)
$resolver = {
    param($sender, $resolveArgs)
    $name = (New-Object System.Reflection.AssemblyName $resolveArgs.Name).Name
    if ($name -eq "MCMv5") { return $null }
    foreach ($dir in $searchDirs) {
        $path = Join-Path $dir "$name.dll"
        if (Test-Path $path) { return [System.Reflection.Assembly]::LoadFrom($path) }
    }
    return $null
}.GetNewClosure()
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

$failed = $false
foreach ($dll in "SmartSteward.Core.dll", "SmartSteward.dll") {
    $assembly = [System.Reflection.Assembly]::LoadFrom((Join-Path $outDir $dll))
    try {
        $types = $assembly.GetTypes()
    }
    catch {
        $ex = $_.Exception
        while ($ex -and -not ($ex -is [System.Reflection.ReflectionTypeLoadException])) { $ex = $ex.InnerException }
        $failed = $true
        Write-Host "FAIL $dll - GetTypes() threw; the game would unload the module:"
        if ($ex) { $ex.LoaderExceptions | ForEach-Object { "       " + $_.Message } | Sort-Object -Unique | Write-Host }
        else { Write-Host ("       " + $_.Exception) }
        continue
    }
    $bad = @()
    foreach ($type in $types) {
        $flags = [System.Reflection.BindingFlags]"Static,Public,NonPublic,DeclaredOnly"
        foreach ($method in $type.GetMethods($flags)) {
            try { $null = $method.ReturnType; $null = $method.GetParameters() }
            catch { $bad += "$($type.FullName).$($method.Name)" }
        }
    }
    if ($bad.Count -gt 0) {
        $failed = $true
        Write-Host "FAIL $dll - static methods naming a missing assembly:"
        $bad | ForEach-Object { Write-Host "       $_" }
    }
    else {
        Write-Host "OK   $dll - $($types.Count) types load and every static signature resolves without MCM"
    }
}

$mcm = [AppDomain]::CurrentDomain.GetAssemblies() | Where-Object { $_.GetName().Name -eq "MCMv5" }
if ($mcm) { throw "MCMv5 got loaded after all ($($mcm.Location)) - the check proves nothing." }
if ($failed) { exit 1 }
Write-Host "The module loads without MCM."
