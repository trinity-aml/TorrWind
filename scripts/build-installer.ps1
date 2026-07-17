[CmdletBinding()]
param(
    [string]$Version = "1.0.6",
    [string]$Configuration = "Release",
    [string]$InnoCompilerPath = "",
    [string]$WinePrefix = "",
    [string]$MpvRuntimeArchivePath = "",
    [switch]$SkipMpvRuntime,
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $true

$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
$InnoScript = Join-Path $Root "installers\windows\TorrWind.iss"

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

function Find-WindowsIscc {
    $isccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
    if ($null -ne $isccCommand) {
        return $isccCommand.Source
    }

    $defaultPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (Test-Path $defaultPath) {
        return $defaultPath
    }

    return $null
}

function Find-WineIscc {
    if (-not [string]::IsNullOrWhiteSpace($WinePrefix)) {
        $prefixes = @($WinePrefix)
    } elseif (-not [string]::IsNullOrWhiteSpace($env:WINEPREFIX)) {
        $prefixes = @($env:WINEPREFIX)
    } else {
        $prefixes = @(
            (Join-Path $HOME ".wine-inno"),
            (Join-Path $HOME ".wine")
        )
    }

    foreach ($prefix in $prefixes) {
        $candidates = @(
            (Join-Path $prefix "drive_c\InnoSetup6\ISCC.exe"),
            (Join-Path $prefix "drive_c\Program Files (x86)\Inno Setup 6\ISCC.exe"),
            (Join-Path $prefix "drive_c\Program Files\Inno Setup 6\ISCC.exe")
        )

        foreach ($candidate in $candidates) {
            if (Test-Path $candidate) {
                $script:ResolvedWinePrefix = $prefix
                return $candidate
            }
        }
    }

    return $null
}

$isccPath = if (-not [string]::IsNullOrWhiteSpace($InnoCompilerPath)) {
    $InnoCompilerPath
} elseif ($IsWindows) {
    Find-WindowsIscc
} else {
    Find-WineIscc
}

if ($null -eq $isccPath) {
    throw "ISCC.exe was not found. Install Inno Setup 6, add it to PATH, or pass -InnoCompilerPath."
}

if ($IsWindows) {
    & $isccPath "/DAppVersion=$Version" $InnoScript
} else {
    $wineCommand = Get-Command "wine" -ErrorAction SilentlyContinue
    $winePathCommand = Get-Command "winepath" -ErrorAction SilentlyContinue

    if ($null -eq $wineCommand -or $null -eq $winePathCommand) {
        throw "wine and winepath are required to run Inno Setup on Linux."
    }

    if ([string]::IsNullOrWhiteSpace($WinePrefix) -and -not [string]::IsNullOrWhiteSpace($script:ResolvedWinePrefix)) {
        $WinePrefix = $script:ResolvedWinePrefix
    }

    if (-not [string]::IsNullOrWhiteSpace($WinePrefix)) {
        $env:WINEPREFIX = $WinePrefix
    }

    $innoScriptWindowsPath = (& $winePathCommand.Source -w $InnoScript).Trim()
    & $wineCommand.Source $isccPath "/DAppVersion=$Version" $innoScriptWindowsPath
}

Write-Host "Installer output is in artifacts\installer"
