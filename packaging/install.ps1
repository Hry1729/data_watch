[CmdletBinding()]
param(
    [string]$ApiUrl = "http://127.0.0.1:8000",
    [string]$InstallDirectory = "$env:LOCALAPPDATA\DataWatch\CLI",
    [switch]$SkipHealthCheck,
    [switch]$SkipPathUpdate
)

$ErrorActionPreference = "Stop"
$sourceApp = Join-Path $PSScriptRoot "app"
$sourceExe = Join-Path $sourceApp "data-watch.exe"
if (-not (Test-Path $sourceExe)) {
    throw "Installation package is incomplete: app\data-watch.exe is missing"
}

$root = [IO.Path]::GetFullPath($InstallDirectory)
$current = Join-Path $root "current"
$temporary = Join-Path $root "installing"
$previous = Join-Path $root "previous"

New-Item -ItemType Directory -Path $root -Force | Out-Null
if (Test-Path $temporary) { Remove-Item -LiteralPath $temporary -Recurse -Force }
Copy-Item -LiteralPath $sourceApp -Destination $temporary -Recurse

& (Join-Path $temporary "data-watch.exe") version | ConvertFrom-Json | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Packaged CLI failed its version check" }

if (Test-Path $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
try {
    if (Test-Path $current) { Move-Item -LiteralPath $current -Destination $previous }
    Move-Item -LiteralPath $temporary -Destination $current
}
catch {
    if (-not (Test-Path $current) -and (Test-Path $previous)) {
        Move-Item -LiteralPath $previous -Destination $current
    }
    throw
}

Copy-Item -LiteralPath (Join-Path $PSScriptRoot "uninstall.ps1") -Destination $root -Force
if (-not $SkipPathUpdate) {
    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $pathEntries = @($userPath -split ";" | Where-Object { $_ })
    if ($pathEntries -notcontains $current) {
        $newPath = (@($pathEntries) + $current) -join ";"
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
    }
    $env:Path = "$current;$env:Path"
}

& (Join-Path $current "data-watch.exe") config set-url $ApiUrl | ConvertFrom-Json | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to configure Backend URL" }

if (-not $SkipHealthCheck) {
    & (Join-Path $current "data-watch.exe") system health | ConvertFrom-Json | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "CLI is installed, but Backend health check failed: $ApiUrl"
    }
}

$version = (& (Join-Path $current "data-watch.exe") version | ConvertFrom-Json).data.version
Write-Host "DataWatch CLI $version installed in $current"
Write-Host "Open a new PowerShell window, then run: data-watch doctor"
