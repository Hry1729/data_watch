param(
    [double]$Duration = 0,
    [double]$Period = 12,
    [double]$Noise = 12,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot 'simulate_qc100.py'
docker cp $sourcePath 'data-watch-backend:/tmp/simulate_qc100.py'
if ($LASTEXITCODE -ne 0) { throw 'Cannot copy script. Start data-watch-backend first.' }
$runArgs = @('--duration', $Duration.ToString([cultureinfo]::InvariantCulture), '--period', $Period.ToString([cultureinfo]::InvariantCulture), '--noise', $Noise.ToString([cultureinfo]::InvariantCulture))
if ($DryRun) { $runArgs += '--dry-run' }
# Forward Ctrl+C to Python so no background writer remains after exit.
docker exec -it data-watch-backend python3.14t /tmp/simulate_qc100.py @runArgs
if ($LASTEXITCODE -ne 0) { throw 'Demo stopped. See the error above.' }
