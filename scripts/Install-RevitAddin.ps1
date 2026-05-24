param(
    [ValidateSet("2021", "2022")]
    [string]$RevitVersion = "2021",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root "artifacts\$RevitVersion\$Configuration"
$addinRoot = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
$target = Join-Path $addinRoot "MyInstrumentsForRevit"
$manifestSource = Join-Path $root "deploy\Revit$RevitVersion\MyInstrumentsForRevit.addin"
$manifestTarget = Join-Path $addinRoot "MyInstrumentsForRevit.addin"
$legacyManifestTarget = Join-Path $addinRoot "ContextFilter.addin"
$legacyTarget = Join-Path $addinRoot "ContextFilter"

if (-not (Test-Path $source)) {
    throw "Build output not found: $source"
}

if (-not (Test-Path $manifestSource)) {
    throw "Manifest not found: $manifestSource"
}

New-Item -ItemType Directory -Path $target -Force | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $target -Recurse -Force

$manifestContent = Get-Content -Path $manifestSource -Raw
$manifestContent = $manifestContent.Replace("%APPDATA%", $env:APPDATA)
Set-Content -Path $manifestTarget -Value $manifestContent -Encoding UTF8

if (Test-Path $legacyManifestTarget) {
    Remove-Item -Path $legacyManifestTarget -Force
}

if (Test-Path $legacyTarget) {
    Remove-Item -Path $legacyTarget -Recurse -Force
}

Write-Host "Installed MyInstrumentsForRevit for Revit $RevitVersion from $source"
Write-Host "Manifest: $manifestTarget"
