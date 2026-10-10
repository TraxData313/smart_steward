# Renders docs\mockups\suggestion_v2.html to two PNGs beside it (PLAN step 19, the round-4 mockup):
#   suggestion_v2_folded.png   - the everyday view at the window's real size (1580 x 960 + the dimmed edge)
#   suggestion_v2_expanded.png - every section and group open, the table unrolled (the page measures its own height)
# Usage: powershell -ExecutionPolicy Bypass -File tools\render-mockup.ps1
# Same approach as make_thumbnail.py: headless Edge (or Chrome) with its own profile folder, so an open browser is never touched.
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $repoRoot "docs\mockups"
$html = Join-Path $dir "suggestion_v2.html"
if (-not (Test-Path $html)) { throw "Missing $html" }

$browser = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw "No Edge or Chrome found - open $html in a browser and screenshot it by hand." }

$profileDir = Join-Path $env:TEMP "smart_steward_mockup_profile"
$url = "file:///" + ($html -replace "\\", "/")
$width = 1640
$common = @("--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run", "--force-device-scale-factor=1",
    "--user-data-dir=`"$profileDir`"")

function Shot([string]$hash, [int]$height, [string]$png) {
    if (Test-Path $png) { Remove-Item $png -Force }
    $arguments = $common + @("--window-size=$width,$height", "--screenshot=`"$png`"", "`"$url$hash`"")
    Start-Process -FilePath $browser -ArgumentList $arguments -Wait -WindowStyle Hidden
    if (-not (Test-Path $png)) { throw "The browser wrote no screenshot ($browser)." }
    Write-Host ("Rendered {0} ({1:N0} bytes, {2} x {3})" -f $png, (Get-Item $png).Length, $width, $height)
}

# The everyday view: the real window (960) + 30 px of the dimmed world around it.
Shot "" 1020 (Join-Path $dir "suggestion_v2_folded.png")

# Everything open: ask the page how tall it is (its script writes data-h on <html>), then shoot at that height.
$dom = Join-Path $env:TEMP "smart_steward_mockup_dom.html"
$arguments = $common + @("--window-size=$width,1020", "--dump-dom", "`"$url#expanded`"")
Start-Process -FilePath $browser -ArgumentList $arguments -Wait -WindowStyle Hidden -RedirectStandardOutput $dom
$match = [regex]::Match((Get-Content $dom -Raw), 'data-h="(\d+)"')
Remove-Item $dom -Force
if (-not $match.Success) { throw "The page did not report its height (data-h) - is its script intact?" }
Shot "#expanded" ([int]$match.Groups[1].Value) (Join-Path $dir "suggestion_v2_expanded.png")
