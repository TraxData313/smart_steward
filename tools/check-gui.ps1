# The prefab gate: checks module\GUI\Prefabs\*.xml against THIS game version before the game ever loads them.
# Usage (Windows PowerShell - it loads the game's .NET Framework DLLs by reflection):
#   powershell -ExecutionPolicy Bypass -File tools\check-gui.ps1 [-Configuration Release] [-GameFolder <path>]
#
# A broken prefab is found only when the movie loads, in game, and some mistakes crash it (docs/RESEARCH.md §14):
# an unknown brush name gives the widget a NULL brush; a VM getter or command that throws is rethrown by
# Gauntlet's InvokeWithLog. Others fail silently (an unknown attribute is ignored, an unknown widget tag becomes a
# plain Widget, a missing binding shows nothing). This script checks, for every element and attribute:
#   - the XML is well-formed;
#   - every tag is a widget class of the game's Gauntlet assemblies, or a prefab (custom widget) name;
#   - every attribute names a real property of that widget (dotted paths too: Brush.FontSize), and literal values
#     parse as the property's type (enums, numbers, colours);
#   - every Brush exists in Native / SandBoxCore / SandBox (always loaded in a campaign) and every sprite it draws,
#     and every Sprite attribute, lives in an ALWAYS-LOADED sprite category (or one listed in -LoadedCategories);
#   - every binding (@Prop, {DataSource}, Command.X="Method") resolves on the view model that is in context there,
#     walking DataSource and ItemTemplate the way Gauntlet does, against the BUILT SmartSteward.dll.
# It prints every brush and sprite it verified. Exit 0 = all good, 1 = something would break or silently fail.
param(
    [string]$Configuration = "Release",
    [string]$GameFolder = "",
    # Sprite categories our window loads itself (UIResourceManager.LoadSpriteCategory). None so far.
    [string[]]$LoadedCategories = @(),
    # Another folder of prefabs to check (a negative control); default module\GUI\Prefabs.
    [string]$PrefabDir = ""
)

$ErrorActionPreference = "Stop"
if ($PSVersionTable.PSEdition -eq "Core") {
    throw "Run this with Windows PowerShell (powershell.exe): it loads the game's .NET Framework assemblies."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$moduleProject = Join-Path $repoRoot "src\SmartSteward.Module\SmartSteward.Module.csproj"
if (-not $GameFolder) {
    $GameFolder = "$(& dotnet msbuild $moduleProject -nologo -getProperty:GameFolder)".Trim()
}
$gameBin = Join-Path $GameFolder "bin\Win64_Shipping_Client"
$outDir = Join-Path $repoRoot "src\SmartSteward.Module\bin\$Configuration"
$prefabDir = if ($PrefabDir) { $PrefabDir } else { Join-Path $repoRoot "module\GUI\Prefabs" }
if (-not (Test-Path (Join-Path $outDir "SmartSteward.dll"))) { throw "Build first: no SmartSteward.dll in $outDir." }
if (-not (Test-Path $prefabDir)) { throw "No prefabs in $prefabDir." }

# Prefab file -> the root view model its movie is loaded with (LoadMovie(name, vm)).
$rootViewModels = @{
    "SmartStewardWindow" = "SmartSteward.UI.StewardWindowVM"
}
# The modules a campaign always loads — only their brushes are safe to use.
$brushModules = @("Native", "SandBoxCore", "SandBox")

# ── assemblies ───────────────────────────────────────────────────────────────────────────────────────────────
$searchDirs = @($outDir, $gameBin,
    (Join-Path $GameFolder "Modules\Native\bin\Win64_Shipping_Client"),
    (Join-Path $GameFolder "Modules\SandBoxCore\bin\Win64_Shipping_Client"),
    (Join-Path $GameFolder "Modules\SandBox\bin\Win64_Shipping_Client"))
$resolver = {
    param($sender, $resolveArgs)
    $name = (New-Object System.Reflection.AssemblyName $resolveArgs.Name).Name
    foreach ($dir in $searchDirs) {
        $path = Join-Path $dir "$name.dll"
        if (Test-Path $path) { return [System.Reflection.Assembly]::LoadFrom($path) }
    }
    return $null
}.GetNewClosure()
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)

function Get-LoadableTypes([System.Reflection.Assembly]$assembly) {
    try { return $assembly.GetTypes() }
    catch [System.Reflection.ReflectionTypeLoadException] { return $_.Exception.Types | Where-Object { $_ -ne $null } }
}

$gauntlet = [System.Reflection.Assembly]::LoadFrom((Join-Path $gameBin "TaleWorlds.GauntletUI.dll"))
$widgetBase = $gauntlet.GetType("TaleWorlds.GauntletUI.BaseTypes.Widget", $true)
$widgetTypes = @{}
$widgetDlls = @(
    (Join-Path $gameBin "TaleWorlds.GauntletUI.dll"),
    (Join-Path $gameBin "TaleWorlds.GauntletUI.ExtraWidgets.dll"),
    (Join-Path $gameBin "TaleWorlds.MountAndBlade.GauntletUI.Widgets.dll"),
    (Join-Path $GameFolder "Modules\Native\bin\Win64_Shipping_Client\TaleWorlds.MountAndBlade.GauntletUI.dll"),
    (Join-Path $GameFolder "Modules\SandBox\bin\Win64_Shipping_Client\SandBox.GauntletUI.dll"))
foreach ($dll in $widgetDlls) {
    if (-not (Test-Path $dll)) { continue }
    foreach ($t in Get-LoadableTypes ([System.Reflection.Assembly]::LoadFrom($dll))) {
        if ($t.IsClass -and -not $t.IsAbstract -and $widgetBase.IsAssignableFrom($t)) { $widgetTypes[$t.Name] = $t }
    }
}

$library = [System.Reflection.Assembly]::LoadFrom((Join-Path $gameBin "TaleWorlds.Library.dll"))
$viewModelBase = $library.GetType("TaleWorlds.Library.ViewModel", $true)
$colorType = $library.GetType("TaleWorlds.Library.Color", $true)
$ours = [System.Reflection.Assembly]::LoadFrom((Join-Path $outDir "SmartSteward.dll"))
$ourTypes = Get-LoadableTypes $ours

# ── game GUI data ────────────────────────────────────────────────────────────────────────────────────────────
$prefabNames = @{}
Get-ChildItem (Join-Path $GameFolder "Modules") -Directory | ForEach-Object {
    $p = Join-Path $_.FullName "GUI\Prefabs"
    if (Test-Path $p) { Get-ChildItem $p -Recurse -Filter *.xml | ForEach-Object { $prefabNames[$_.BaseName] = $true } }
}
Get-ChildItem $prefabDir -Recurse -Filter *.xml | ForEach-Object { $prefabNames[$_.BaseName] = $true }

$categories = @{}   # category -> always loaded
$spriteCategory = @{} # sprite (generic / nine-region / part) name -> category
foreach ($module in "Native", "SandBoxCore", "SandBox") {
    foreach ($file in Get-ChildItem (Join-Path $GameFolder "Modules\$module\GUI") -Filter "*SpriteData.xml" -ErrorAction SilentlyContinue) {
        $xml = New-Object System.Xml.XmlDocument
        $xml.Load($file.FullName)
        foreach ($c in $xml.SelectNodes("//SpriteCategory")) {
            $categories[$c.SelectSingleNode("Name").InnerText] = ($c.SelectSingleNode("AlwaysLoad") -ne $null)
        }
        $parts = @{}
        foreach ($p in $xml.SelectNodes("//SpritePart")) {
            $parts[$p.SelectSingleNode("Name").InnerText] = $p.SelectSingleNode("CategoryName").InnerText
            $spriteCategory[$p.SelectSingleNode("Name").InnerText] = $p.SelectSingleNode("CategoryName").InnerText
        }
        foreach ($s in $xml.SelectNodes("//GenericSprite | //NineRegionSprite")) {
            $part = $s.SelectSingleNode("SpritePartName").InnerText
            if ($parts.ContainsKey($part)) { $spriteCategory[$s.SelectSingleNode("Name").InnerText] = $parts[$part] }
        }
    }
}

$brushes = @{}      # name -> @{ Module; Base; Sprites }
foreach ($module in $brushModules) {
    foreach ($file in Get-ChildItem (Join-Path $GameFolder "Modules\$module\GUI\Brushes") -Filter *.xml -ErrorAction SilentlyContinue) {
        $xml = New-Object System.Xml.XmlDocument
        try { $xml.Load($file.FullName) } catch { continue }
        foreach ($b in $xml.SelectNodes("//Brush")) {
            $sprites = @($b.SelectNodes(".//*[@Sprite]") | ForEach-Object { $_.GetAttribute("Sprite") } | Where-Object { $_ })
            $brushes[$b.GetAttribute("Name")] = @{ Module = $module; File = $file.Name; Base = $b.GetAttribute("BaseBrush"); Sprites = $sprites }
        }
    }
}

# ── checks ───────────────────────────────────────────────────────────────────────────────────────────────────
$problems = New-Object System.Collections.Generic.List[string]
$usedBrushes = @{}
$usedSprites = @{}
$bindingCount = 0

function Test-Sprite([string]$sprite, [string]$where) {
    if (-not $spriteCategory.ContainsKey($sprite)) { $problems.Add("$where : sprite '$sprite' does not exist"); return }
    $cat = $spriteCategory[$sprite]
    $script:usedSprites[$sprite] = $cat
    if (-not $categories[$cat] -and $LoadedCategories -notcontains $cat) {
        $problems.Add("$where : sprite '$sprite' is in category '$cat', which is not always loaded")
    }
}

function Test-Brush([string]$brush, [string]$where) {
    $seen = @{}
    $name = $brush
    while ($name) {
        if ($seen.ContainsKey($name)) { break }
        $seen[$name] = $true
        if (-not $brushes.ContainsKey($name)) {
            $problems.Add("$where : brush '$name' is not defined in $($brushModules -join ' / ') - the widget would get a NULL brush")
            return
        }
        $b = $brushes[$name]
        $script:usedBrushes[$name] = "$($b.Module)\$($b.File)"
        foreach ($s in $b.Sprites) { Test-Sprite $s "$where (brush $name)" }
        $name = $b.Base
    }
}

function Get-PropertyPath([Type]$type, [string[]]$parts) {
    $current = $type
    foreach ($part in $parts) {
        $prop = $current.GetProperty($part, [System.Reflection.BindingFlags]"Instance,Public")
        if ($prop -eq $null) { return $null }
        $current = $prop.PropertyType
    }
    return $current
}

function Test-LiteralValue([Type]$type, [string]$value, [string]$where) {
    if ($type.IsEnum) {
        if (-not [Enum]::GetNames($type).Contains($value)) { $problems.Add("$where : '$value' is not a $($type.Name) ($([Enum]::GetNames($type) -join ', '))") }
    }
    elseif ($type -eq [bool]) {
        if ($value -ne "true" -and $value -ne "false") { $problems.Add("$where : '$value' is not true/false") }
    }
    elseif ($type -eq [int]) {
        $n = 0
        if (-not [int]::TryParse($value, [ref]$n)) { $problems.Add("$where : '$value' is not a whole number") }
    }
    elseif ($type -eq [float]) {
        $f = 0.0
        if (-not [float]::TryParse($value, [System.Globalization.NumberStyles]::Float, [System.Globalization.CultureInfo]::InvariantCulture, [ref]$f)) {
            $problems.Add("$where : '$value' is not a number")
        }
    }
    elseif ($type -eq $colorType) {
        if ($value -notmatch '^#[0-9A-Fa-f]{8}$') { $problems.Add("$where : '$value' is not a #RRGGBBAA colour") }
    }
}

function Get-VmMember([Type]$vm, [string]$name) {
    return $vm.GetProperty($name, [System.Reflection.BindingFlags]"Instance,Public")
}

function Get-ItemType([Type]$listType) {
    $t = $listType
    while ($t -ne $null) {
        if ($t.IsGenericType) { return $t.GetGenericArguments()[0] }
        $t = $t.BaseType
    }
    return $null
}

function Test-Element([System.Xml.XmlElement]$el, [Type]$vm, [string]$path) {
    $tag = $el.LocalName
    if ($tag -eq "Children") {
        foreach ($c in $el.ChildNodes) { if ($c -is [System.Xml.XmlElement]) { Test-Element $c $vm $path } }
        return
    }
    $where = "$path/$tag"
    $idAttr = $el.GetAttribute("Id")
    if ($idAttr) { $where = "$where#$idAttr" }

    $widgetType = $null
    if ($widgetTypes.ContainsKey($tag)) { $widgetType = $widgetTypes[$tag] }
    elseif (-not $prefabNames.ContainsKey($tag)) { $problems.Add("$where : '$tag' is neither a widget class nor a prefab of this game") }

    # DataSource first: it changes the view model the rest of this element binds to.
    $context = $vm
    $ds = $el.GetAttribute("DataSource")
    $itemVm = $null
    if ($ds) {
        if ($ds -notmatch '^\{([A-Za-z0-9_]+)\}$') { $problems.Add("$where : DataSource '$ds' - only {Property} is supported by this check") }
        else {
            $script:bindingCount++
            $prop = Get-VmMember $vm $Matches[1]
            if ($prop -eq $null) { $problems.Add("$where : DataSource {$($Matches[1])} - no public property on $($vm.Name)") }
            else {
                $context = $prop.PropertyType
                $itemVm = Get-ItemType $prop.PropertyType
            }
        }
    }

    foreach ($attr in $el.Attributes) {
        $name = $attr.Name
        $value = $attr.Value
        if ($name -eq "DataSource" -or $name -eq "Id") { continue }
        if ($name.StartsWith("Command.")) {
            $script:bindingCount++
            $method = $context.GetMethod($value, [System.Reflection.BindingFlags]"Instance,Public")
            if ($method -eq $null) { $problems.Add("$where : $name='$value' - no public method on $($context.Name)") }
            elseif ($method.GetParameters().Count -ne 0) { $problems.Add("$where : $name='$value' - command methods here take no parameters") }
            continue
        }
        if ($name.StartsWith("CommandParameter.") -or $name.StartsWith("Parameter.")) { continue }
        $target = $null
        if ($widgetType -ne $null) {
            $target = Get-PropertyPath $widgetType $name.Split('.')
            if ($target -eq $null) { $problems.Add("$where : '$name' is not a property of $tag (Gauntlet would ignore it)") }
        }
        if ($value.StartsWith("@")) {
            $script:bindingCount++
            $prop = Get-VmMember $context $value.Substring(1)
            if ($prop -eq $null) { $problems.Add("$where : $name='$value' - no public property on $($context.Name)") }
            elseif ($target -ne $null -and $target -ne $prop.PropertyType -and -not ($target -eq $colorType -and $prop.PropertyType -eq [string])) {
                $problems.Add("$where : $name is $($target.Name) but $($context.Name).$($value.Substring(1)) is $($prop.PropertyType.Name)")
            }
            continue
        }
        if ($value.StartsWith("!") -or $value.StartsWith("*")) { $problems.Add("$where : $name='$value' - constants and parameters are not used in our prefabs"); continue }
        if ($name -eq "Brush") { Test-Brush $value $where; continue }
        if ($name -eq "Sprite" -or $name.EndsWith(".Sprite")) { Test-Sprite $value $where; continue }
        if ($target -ne $null) { Test-LiteralValue $target $value "$where @$name" }
    }

    foreach ($child in $el.ChildNodes) {
        if (-not ($child -is [System.Xml.XmlElement])) { continue }
        if ($child.LocalName -eq "ItemTemplate") {
            if ($itemVm -eq $null) { $problems.Add("$where : ItemTemplate without a DataSource list"); continue }
            foreach ($t in $child.ChildNodes) { if ($t -is [System.Xml.XmlElement]) { Test-Element $t $itemVm "$where[item]" } }
        }
        elseif ($child.LocalName -eq "Children") { Test-Element $child $context $where }
        else { $problems.Add("$where : unexpected child <$($child.LocalName)> (widgets go under <Children>)") }
    }
}

$files = Get-ChildItem $prefabDir -Recurse -Filter *.xml
foreach ($file in $files) {
    $xml = New-Object System.Xml.XmlDocument
    try { $xml.Load($file.FullName) }
    catch { $problems.Add("$($file.Name) : not well-formed XML - $($_.Exception.Message)"); continue }
    if (-not $rootViewModels.ContainsKey($file.BaseName)) { $problems.Add("$($file.Name) : no root view model known - add it to `$rootViewModels"); continue }
    $vmType = $ourTypes | Where-Object { $_.FullName -eq $rootViewModels[$file.BaseName] } | Select-Object -First 1
    if ($vmType -eq $null) { $problems.Add("$($file.Name) : $($rootViewModels[$file.BaseName]) is not in SmartSteward.dll"); continue }
    if (-not $viewModelBase.IsAssignableFrom($vmType)) { $problems.Add("$($file.Name) : $($vmType.Name) is not a ViewModel") }
    $window = $xml.SelectSingleNode("/Prefab/Window")
    if ($window -eq $null) { $problems.Add("$($file.Name) : no <Prefab><Window>"); continue }
    foreach ($root in $window.ChildNodes) { if ($root -is [System.Xml.XmlElement]) { Test-Element $root $vmType $file.BaseName } }
}

Write-Host "Checked $($files.Count) prefab(s) against $GameFolder ($($widgetTypes.Count) widget classes, $($brushes.Count) brushes)."
Write-Host "Brushes used:"
$usedBrushes.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host ("  {0,-40} {1}" -f $_.Name, $_.Value) }
Write-Host "Sprites used:"
$usedSprites.GetEnumerator() | Sort-Object Name | ForEach-Object { Write-Host ("  {0,-50} {1}" -f $_.Name, $_.Value) }
Write-Host "Bindings checked: $bindingCount"
if ($problems.Count -gt 0) {
    Write-Host "FAIL - $($problems.Count) problem(s):"
    $problems | ForEach-Object { Write-Host "  $_" }
    exit 1
}
Write-Host "OK - every tag, attribute, brush, sprite and binding checks out."
