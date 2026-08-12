[CmdletBinding()]
param(
    [string]$Executable = "$env:LOCALAPPDATA\DataWatch\CLI\current\data-watch.exe",
    [switch]$SkipBackend
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $Executable)) { throw "CLI executable not found: $Executable" }

$version = & $Executable version | ConvertFrom-Json
if (-not $version.ok -or -not $version.data.version) { throw "Version smoke test failed" }

$processInfo = New-Object System.Diagnostics.ProcessStartInfo
$processInfo.FileName = [IO.Path]::GetFullPath($Executable)
$processInfo.Arguments = "devices get"
$processInfo.UseShellExecute = $false
$processInfo.CreateNoWindow = $true
$processInfo.RedirectStandardOutput = $true
$processInfo.RedirectStandardError = $true
$process = [System.Diagnostics.Process]::Start($processInfo)
$usageError = $process.StandardError.ReadToEnd()
$process.WaitForExit()
if ($process.ExitCode -ne 2) { throw "Usage error exit code must be 2" }
$usage = $usageError | ConvertFrom-Json
if ($usage.error.type -ne "usage_error") { throw "Usage error is not JSON" }

if (-not $SkipBackend) {
    $health = & $Executable system health | ConvertFrom-Json
    if (-not $health.ok) { throw "Backend health smoke test failed" }
}

Write-Host "Smoke test passed for DataWatch CLI $($version.data.version)"
