[CmdletBinding()]
param(
    [string]$DestinationDir = "",
    [string]$SourceArchivePath = "",
    [string]$CacheDirectory = "",
    [string]$ReleaseApiUrl = "https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest",
    [string]$AssetNamePattern = "^mpv-x86_64-\d{8}-git-[^.]+\.7z$",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($DestinationDir)) {
    $DestinationDir = Join-Path $Root "artifacts\publish\TorrWind\Runtime\mpv"
}

if ([string]::IsNullOrWhiteSpace($CacheDirectory)) {
    $CacheDirectory = Join-Path $Root "artifacts\cache\mpv"
}

function New-GitHubHeaders {
    $headers = @{
        "Accept" = "application/vnd.github+json"
        "User-Agent" = "TorrWind-release-builder"
    }

    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
        $headers["Authorization"] = "Bearer $env:GITHUB_TOKEN"
    }

    return $headers
}

function Find-SevenZip {
    $commands = @("7z", "7zz", "7za")
    foreach ($command in $commands) {
        $resolved = Get-Command $command -ErrorAction SilentlyContinue
        if ($null -ne $resolved) {
            return $resolved.Source
        }
    }

    $candidates = @()
    if (-not [string]::IsNullOrWhiteSpace($env:ProgramFiles)) {
        $candidates += Join-Path $env:ProgramFiles "7-Zip\7z.exe"
    }

    if (-not [string]::IsNullOrWhiteSpace(${env:ProgramFiles(x86)})) {
        $candidates += Join-Path ${env:ProgramFiles(x86)} "7-Zip\7z.exe"
    }

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return $candidate
        }
    }

    return $null
}

function Get-MpvArchive {
    if (-not [string]::IsNullOrWhiteSpace($SourceArchivePath)) {
        if (-not (Test-Path $SourceArchivePath)) {
            throw "mpv runtime archive was not found: $SourceArchivePath"
        }

        return [pscustomobject]@{
            Path = (Resolve-Path $SourceArchivePath).Path
            AssetName = Split-Path $SourceArchivePath -Leaf
            AssetUrl = ""
            ReleaseTag = "local"
            Sha256 = ""
        }
    }

    New-Item -ItemType Directory -Path $CacheDirectory -Force | Out-Null

    $headers = New-GitHubHeaders
    Write-Host "Resolving mpv runtime from $ReleaseApiUrl"
    $release = Invoke-RestMethod -Uri $ReleaseApiUrl -Headers $headers
    $asset = @($release.assets | Where-Object { $_.name -match $AssetNamePattern } | Sort-Object name | Select-Object -First 1)

    if ($asset.Count -eq 0) {
        throw "No mpv runtime asset matched pattern '$AssetNamePattern' in release '$($release.tag_name)'."
    }

    $asset = $asset[0]
    $archivePath = Join-Path $CacheDirectory $asset.name
    $expectedSha256 = ""
    if ($asset.digest -match "^sha256:(.+)$") {
        $expectedSha256 = $Matches[1].ToLowerInvariant()
    }

    $needsDownload = $Force -or -not (Test-Path $archivePath)
    if (-not $needsDownload -and -not [string]::IsNullOrWhiteSpace($expectedSha256)) {
        $actualSha256 = (Get-FileHash -Algorithm SHA256 $archivePath).Hash.ToLowerInvariant()
        $needsDownload = $actualSha256 -ne $expectedSha256
    }

    if ($needsDownload) {
        Write-Host "Downloading $($asset.name)"
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $archivePath -Headers $headers
    } else {
        Write-Host "Using cached mpv runtime archive $archivePath"
    }

    if (-not [string]::IsNullOrWhiteSpace($expectedSha256)) {
        $actualSha256 = (Get-FileHash -Algorithm SHA256 $archivePath).Hash.ToLowerInvariant()
        if ($actualSha256 -ne $expectedSha256) {
            throw "mpv runtime SHA256 mismatch. Expected $expectedSha256, got $actualSha256."
        }
    }

    return [pscustomobject]@{
        Path = $archivePath
        AssetName = $asset.name
        AssetUrl = $asset.browser_download_url
        ReleaseTag = $release.tag_name
        Sha256 = $expectedSha256
    }
}

function Expand-MpvArchive {
    param(
        [string]$ArchivePath,
        [string]$ExtractDir
    )

    $sevenZip = Find-SevenZip
    if ($null -eq $sevenZip) {
        throw "7-Zip was not found. Install 7-Zip/p7zip before bundling the mpv runtime."
    }

    New-Item -ItemType Directory -Path $ExtractDir -Force | Out-Null
    & $sevenZip x "-o$ExtractDir" "-y" $ArchivePath
}

$archive = Get-MpvArchive
$extractDir = Join-Path ([System.IO.Path]::GetTempPath()) ("torrwind-mpv-" + [Guid]::NewGuid().ToString("N"))

try {
    Expand-MpvArchive -ArchivePath $archive.Path -ExtractDir $extractDir

    $mpvExe = Get-ChildItem -Path $extractDir -Filter "mpv.exe" -Recurse -File | Select-Object -First 1
    if ($null -eq $mpvExe) {
        throw "mpv.exe was not found in archive $($archive.Path)."
    }

    $sourceDir = $mpvExe.Directory.FullName
    Remove-Item $DestinationDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $DestinationDir -Force | Out-Null
    Copy-Item -Path (Join-Path $sourceDir "*") -Destination $DestinationDir -Recurse -Force

    $manifest = @(
        "TorrWind bundled mpv runtime",
        "Release tag: $($archive.ReleaseTag)",
        "Asset: $($archive.AssetName)",
        "Asset URL: $($archive.AssetUrl)",
        "SHA256: $($archive.Sha256)",
        ("Installed: " + [DateTimeOffset]::Now.ToString("O")),
        "Source: https://github.com/shinchiro/mpv-winbuild-cmake",
        "mpv project: https://github.com/mpv-player/mpv"
    )
    $manifest | Set-Content -Path (Join-Path $DestinationDir "TorrWind-mpv-runtime.txt") -Encoding utf8

    Write-Host "Installed mpv runtime to $DestinationDir"
} finally {
    Remove-Item $extractDir -Recurse -Force -ErrorAction SilentlyContinue
}
