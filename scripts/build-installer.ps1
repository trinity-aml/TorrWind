[CmdletBinding()]
param(
    [string]$Version = "0.1.0",
    [string]$Configuration = "Release",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$InnoScript = Join-Path $Root "installers\windows\TorrWind.iss"

if (-not $SkipPublish) {
    & (Join-Path $PSScriptRoot "publish-win-x64.ps1") -Configuration $Configuration
}

$isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
$isccPath = if ($null -ne $isccCommand) { $isccCommand.Source } else { $null }

if ($null -eq $isccPath) {
    $defaultPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $defaultPath) {
        $isccPath = $defaultPath
    }
}

if ($null -eq $isccPath) {
    throw "ISCC.exe was not found. Install Inno Setup 6 or add it to PATH."
}

& $isccPath "/DAppVersion=$Version" $InnoScript

Write-Host "Installer output is in artifacts\installer"
