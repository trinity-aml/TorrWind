[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.6",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true,
    [string]$MpvRuntimeArchivePath = "",
    [switch]$SkipMpvRuntime
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir = Join-Path $Root "artifacts\publish\TorrWind"
$AppProject = Join-Path $Root "src\TorrWind.App\TorrWind.App.csproj"
$ServiceProject = Join-Path $Root "src\TorrWind.Service\TorrWind.Service.csproj"

function Convert-ToFileVersion {
    param([string]$InputVersion)

    $normalized = $InputVersion -replace "^[vV]", ""
    $coreVersion = ($normalized -split "-", 2)[0]
    $parts = $coreVersion.Split(".")
    if ($parts.Count -eq 3) {
        return "$($parts[0]).$($parts[1]).$($parts[2]).0"
    }

    return $coreVersion
}

$FileVersion = Convert-ToFileVersion $Version

Remove-Item $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $PublishDir | Out-Null

$common = @(
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", $SelfContained.ToString().ToLowerInvariant(),
    "--no-restore",
    "-p:PublishSingleFile=false",
    "-p:UseSharedCompilation=false",
    "-p:NuGetAudit=false",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-p:Version=$Version",
    "-p:AssemblyVersion=$FileVersion",
    "-p:FileVersion=$FileVersion",
    "-p:AssemblyInformationalVersion=$Version"
)

dotnet restore $AppProject -r $Runtime -p:UseSharedCompilation=false -p:NuGetAudit=false
dotnet restore $ServiceProject -r $Runtime -p:UseSharedCompilation=false -p:NuGetAudit=false

dotnet publish $AppProject @common -o $PublishDir
dotnet publish $ServiceProject @common -o $PublishDir

Copy-Item (Join-Path $Root "README.md") $PublishDir -Force
Copy-Item (Join-Path $Root "README.ru.md") $PublishDir -Force
Copy-Item (Join-Path $Root "LICENSE") $PublishDir -Force

$DocsSourceDir = Join-Path $Root "docs"
$DocsDestinationDir = Join-Path $PublishDir "docs"
if (Test-Path $DocsSourceDir) {
    New-Item -ItemType Directory -Path $DocsDestinationDir -Force | Out-Null
    Copy-Item (Join-Path $DocsSourceDir "*.md") $DocsDestinationDir -Force
}

if (-not $SkipMpvRuntime) {
    $mpvArgs = @{
        DestinationDir = Join-Path $PublishDir "Runtime\mpv"
    }

    if (-not [string]::IsNullOrWhiteSpace($MpvRuntimeArchivePath)) {
        $mpvArgs["SourceArchivePath"] = $MpvRuntimeArchivePath
    }

    & (Join-Path $PSScriptRoot "install-mpv-runtime.ps1") @mpvArgs
} else {
    Write-Host "Skipping bundled mpv runtime."
}

Write-Host "Published TorrWind to $PublishDir"
