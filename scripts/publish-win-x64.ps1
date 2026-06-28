[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [bool]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir = Join-Path $Root "artifacts\publish\TorrWind"

Remove-Item $PublishDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $PublishDir | Out-Null

$common = @(
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", $SelfContained.ToString().ToLowerInvariant(),
    "-p:PublishSingleFile=false",
    "-p:UseSharedCompilation=false",
    "-p:NuGetAudit=false"
)

dotnet restore (Join-Path $Root "TorrWind.sln")

dotnet publish (Join-Path $Root "src\TorrWind.App\TorrWind.App.csproj") @common -o $PublishDir
dotnet publish (Join-Path $Root "src\TorrWind.Service\TorrWind.Service.csproj") @common -o $PublishDir

Copy-Item (Join-Path $Root "README.md") $PublishDir -Force
Copy-Item (Join-Path $Root "LICENSE") $PublishDir -Force

Write-Host "Published TorrWind to $PublishDir"
