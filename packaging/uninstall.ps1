[CmdletBinding()]
param(
    [string]$InstallDirectory = "$env:LOCALAPPDATA\DataWatch\CLI",
    [switch]$PurgeUserData,
    [switch]$SkipPathUpdate
)

$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath($InstallDirectory)
$current = Join-Path $root "current"
if (-not $SkipPathUpdate) {
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $pathEntries = @(
        $userPath -split ";" |
            Where-Object { $_ -and $_.TrimEnd("\") -ne $current.TrimEnd("\") }
    )
    [Environment]::SetEnvironmentVariable("Path", ($pathEntries -join ";"), "User")
}

if ($PurgeUserData) {
    $configRoot = Join-Path $env:APPDATA "DataWatch"
    if (Test-Path $configRoot) { Remove-Item -LiteralPath $configRoot -Recurse -Force }
}

if (Test-Path $root) { Remove-Item -LiteralPath $root -Recurse -Force }
Write-Host "DataWatch CLI uninstalled. Open a new PowerShell window to refresh PATH."
