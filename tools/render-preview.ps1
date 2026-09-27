# Renders tools\preview_thumbnail.html to Screenshots\preview_thumbnail.jpg - the Steam Workshop preview image.
# Usage: powershell -ExecutionPolicy Bypass -File tools\render-preview.ps1
# Headless Edge (or Chrome) takes a 1024 x 1024 screenshot; it is saved as JPEG and must stay under Steam's 1 MB cap.
param(
    [int]$Quality = 90
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$html = Join-Path $PSScriptRoot "preview_thumbnail.html"
$outDir = Join-Path $repoRoot "Screenshots"
$jpg = Join-Path $outDir "preview_thumbnail.jpg"
$png = Join-Path $env:TEMP "smart_steward_preview.png"

$browser = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $browser) { throw "No Edge or Chrome found - open tools\preview_thumbnail.html at 1024 x 1024 and screenshot it by hand." }

if (Test-Path $png) { Remove-Item $png -Force }
# Its own profile folder, so a browser the user has open is never touched.
$profileDir = Join-Path $env:TEMP "smart_steward_preview_profile"
$url = "file:///" + ($html -replace "\\", "/")
$arguments = @("--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run", "--force-device-scale-factor=1",
    "--window-size=1024,1024", "--user-data-dir=`"$profileDir`"", "--screenshot=`"$png`"", "`"$url`"")
Start-Process -FilePath $browser -ArgumentList $arguments -Wait -WindowStyle Hidden
if (-not (Test-Path $png)) { throw "The browser wrote no screenshot ($browser)." }

Add-Type -AssemblyName System.Drawing
$image = [System.Drawing.Image]::FromFile($png)
try {
    if ($image.Width -ne 1024 -or $image.Height -ne 1024) { throw "The screenshot is $($image.Width) x $($image.Height), not 1024 x 1024." }
    New-Item -ItemType Directory -Force $outDir | Out-Null
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object { $_.MimeType -eq "image/jpeg" }
    $encoderParams = New-Object System.Drawing.Imaging.EncoderParameters 1
    $encoderParams.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality, [long]$Quality)
    $image.Save($jpg, $codec, $encoderParams)
}
finally { $image.Dispose() }
Remove-Item $png -Force

$size = (Get-Item $jpg).Length
if ($size -ge 1MB) { throw "preview_thumbnail.jpg is $size bytes - Steam's preview cap is 1 MB; lower -Quality." }
Write-Host ("Rendered {0} ({1:N0} bytes, 1024 x 1024)" -f $jpg, $size)
