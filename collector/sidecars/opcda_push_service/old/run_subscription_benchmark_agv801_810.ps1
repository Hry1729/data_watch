$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $root "bin\OpcDaPushService.exe"
$build = Join-Path $root "build.ps1"
$src = Join-Path $root "OpcDaPushService.cs"
$config = Join-Path $root "opcda_subscription_benchmark_agv801_810.json"
$logDir = Join-Path $root "logs"

if (-not (Test-Path $config)) {
    throw "Benchmark config not found: $config"
}

if ((-not (Test-Path $exe)) -or ((Get-Item $src).LastWriteTimeUtc -gt (Get-Item $exe).LastWriteTimeUtc)) {
    & $build
}

New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$log = Join-Path $logDir "agv801_810_subscription_benchmark_$stamp.log"

Write-Host "Starting OPC DA subscription benchmark"
Write-Host "  exe   : $exe"
Write-Host "  config: $config"
Write-Host "  log   : $log"
Write-Host "Press Ctrl+C to stop. The service will unadvise callbacks and release groups."

& $exe --config $config *> $log
$exitCode = $LASTEXITCODE

Write-Host "Benchmark stopped. Exit code: $exitCode"
Write-Host "Log: $log"
exit $exitCode
