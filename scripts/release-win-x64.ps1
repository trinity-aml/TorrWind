[CmdletBinding()]
param(
    [string]$Version = "1.0.3",
    [string]$Configuration = "Release",
    [string]$MpvRuntimeArchivePath = "",
    [switch]$SkipMpvRuntime
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$InstallerPath = Join-Path $Root "artifacts\installer\TorrWind-$Version-win-x64.exe"
$PortablePath = Join-Path $Root "artifacts\portable\TorrWind-$Version-win-x64-portable.zip"
$ChecksumPath = Join-Path $Root "artifacts\TorrWind-$Version-SHA256SUMS.txt"

$publishArgs = @{
    Configuration = $Configuration
    Version = $Version
}

if (-not [string]::IsNullOrWhiteSpace($MpvRuntimeArchivePath)) {
    $publishArgs["MpvRuntimeArchivePath"] = $MpvRuntimeArchivePath
}

if ($SkipMpvRuntime) {
    $publishArgs["SkipMpvRuntime"] = $true
}

& (Join-Path $PSScriptRoot "publish-win-x64.ps1") @publishArgs
& (Join-Path $PSScriptRoot "package-win-x64.ps1") -Version $Version -Configuration $Configuration -SkipPublish
& (Join-Path $PSScriptRoot "build-installer.ps1") -Version $Version -Configuration $Configuration -SkipPublish

$artifacts = @($InstallerPath, $PortablePath)
foreach ($artifact in $artifacts) {
    if (-not (Test-Path $artifact)) {
        throw "Expected artifact was not created: $artifact"
    }
}

$lines = foreach ($artifact in $artifacts) {
    $hash = (Get-FileHash -Algorithm SHA256 $artifact).Hash.ToLowerInvariant()
    $relativePath = [System.IO.Path]::GetRelativePath($Root, $artifact).Replace("\", "/")
    "$hash  $relativePath"
}

$lines | Set-Content -Path $ChecksumPath -Encoding ascii

Write-Host "Release artifacts:"
foreach ($artifact in $artifacts) {
    Write-Host "  $artifact"
}
Write-Host "Checksums: $ChecksumPath"
