[CmdletBinding()]
param(
    [string]$Version = "1.0.3",
    [string]$Configuration = "Release",
    [string]$MpvRuntimeArchivePath = "",
    [switch]$SkipMpvRuntime,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir = Join-Path $Root "artifacts\publish\TorrWind"
$PortableDir = Join-Path $Root "artifacts\portable"
$ZipPath = Join-Path $PortableDir "TorrWind-$Version-win-x64-portable.zip"

if (-not $SkipPublish) {
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
}

if (-not (Test-Path $PublishDir)) {
    throw "Publish directory does not exist: $PublishDir"
}

New-Item -ItemType Directory -Path $PortableDir -Force | Out-Null
Remove-Item $ZipPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force

Write-Host "Created portable package $ZipPath"
