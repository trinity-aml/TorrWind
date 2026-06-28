[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir = Join-Path $Root "artifacts\publish\TorrWind"
$AppProject = Join-Path $Root "src\TorrWind.App\TorrWind.App.csproj"
$ServiceProject = Join-Path $Root "src\TorrWind.Service\TorrWind.Service.csproj"

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
    "-p:DebugSymbols=false"
)

dotnet restore $AppProject -r $Runtime -p:UseSharedCompilation=false -p:NuGetAudit=false
dotnet restore $ServiceProject -r $Runtime -p:UseSharedCompilation=false -p:NuGetAudit=false

dotnet publish $AppProject @common -o $PublishDir
dotnet publish $ServiceProject @common -o $PublishDir

Copy-Item (Join-Path $Root "README.md") $PublishDir -Force
Copy-Item (Join-Path $Root "LICENSE") $PublishDir -Force

Write-Host "Published TorrWind to $PublishDir"
