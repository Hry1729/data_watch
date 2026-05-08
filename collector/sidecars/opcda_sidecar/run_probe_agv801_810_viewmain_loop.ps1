$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root

$Probe = Join-Path $Root "bin\OpcDaProbe.exe"
$Csv = Join-Path $Root "..\..\..\data\AGV_OPC\agv\ViewMain.csv"
$LogDir = Join-Path $Root "logs"

$Server = "ZPMC.OPCServer"
$Channel = "OPD_Server"
$Group = "ViewMain"
$ItemFormat = "{channel}.{device}.{group}.{tag}"
$Source = "cache"
$ReadDelayMs = 200
$ReadIntervalMs = 1000
$DeviceTimeoutSeconds = 120
$Devices = 801..810 | ForEach-Object { "AGV$_" }

if (-not (Test-Path $Probe)) {
    Write-Host "bin\OpcDaProbe.exe not found. Run .\build.ps1 first." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $Csv)) {
    Write-Host "CSV not found: $Csv" -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$RunStamp = Get-Date -Format "yyyyMMdd_HHmmss"
$SummaryLog = Join-Path $LogDir "agv801_810_viewmain_loop_$RunStamp.summary.log"
$FullLog = Join-Path $LogDir "agv801_810_viewmain_loop_$RunStamp.full.log"

function Write-Summary {
    param([string]$Message)
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff') $Message"
    Write-Host $line
    Add-Content -Path $SummaryLog -Value $line -Encoding UTF8
}

Write-Summary "Starting AGV801-AGV810 ViewMain full-point continuous read."
Write-Summary "Probe=$Probe"
Write-Summary "Csv=$Csv"
Write-Summary "Server=$Server Channel=$Channel Group=$Group ItemFormat=$ItemFormat Source=$Source ReadDelayMs=$ReadDelayMs"
Write-Summary "Mode=per-device-fixed-interval devices=$($Devices.Count) ReadIntervalMs=$ReadIntervalMs DeviceTimeoutSeconds=$DeviceTimeoutSeconds"
Write-Summary "Full output log=$FullLog"
Write-Summary "Console shows statistics only. Per-point probe output is saved to the full log."
Write-Summary "Press Ctrl+C to stop."

function Get-ReadStats {
    param([object[]]$Output)

    $readLines = @($Output | Where-Object { $_ -match "^Read " })
    $total = $readLines.Count
    $ok = @($readLines | Where-Object { $_ -match "^Read OK" }).Count
    $fail = @($readLines | Where-Object { $_ -match "^Read FAIL" }).Count
    $nullValue = @($readLines | Where-Object { $_ -match "value=<null>" }).Count
    $goodQuality = @($readLines | Where-Object { $_ -match "qualityGood=True" }).Count
    $badQuality = @($readLines | Where-Object { $_ -match "qualityGood=False" }).Count

    return [pscustomobject]@{
        Total = $total
        Ok = $ok
        Fail = $fail
        NullValue = $nullValue
        GoodQuality = $goodQuality
        BadQuality = $badQuality
    }
}

function Get-SuccessPair {
    param(
        [string]$Line,
        [string]$Prefix
    )

    if ($Line -match "$([regex]::Escape($Prefix))\s+(\d+)/(\d+)") {
        return [pscustomobject]@{
            Ok = [int]$Matches[1]
            Total = [int]$Matches[2]
        }
    }

    return [pscustomobject]@{
        Ok = 0
        Total = 0
    }
}

function New-ProbeArgumentList {
    param(
        [string]$ServerArg,
        [string]$ChannelArg,
        [string]$DeviceArg,
        [string]$GroupArg,
        [string]$CsvArg,
        [string]$ItemFormatArg,
        [string]$SourceArg,
        [int]$ReadDelayMsArg
    )

    return @(
        "--server", $ServerArg,
        "--channel", $ChannelArg,
        "--device", $DeviceArg,
        "--group", $GroupArg,
        "--csv", $CsvArg,
        "--item-format", $ItemFormatArg,
        "--skip", "0",
        "--limit", "0",
        "--probe-count", "10",
        "--source", $SourceArg,
        "--read-delay-ms", ([string]$ReadDelayMsArg)
    )
}

function Get-ProcessOutput {
    param(
        [string]$StdoutPath,
        [string]$StderrPath
    )

    $output = @()
    if (Test-Path $StdoutPath) {
        $output += @(Get-Content -Path $StdoutPath -Encoding UTF8)
    }
    if (Test-Path $StderrPath) {
        $stderrLines = @(Get-Content -Path $StderrPath -Encoding UTF8)
        foreach ($line in $stderrLines) {
            $output += "STDERR: $line"
        }
    }

    return $output
}

function Start-ProbeRun {
    param([pscustomobject]$State)

    $State.RunIndex++
    $runIndex = $State.RunIndex
    $deviceStart = Get-Date
    $safeDevice = $State.Device -replace "[^A-Za-z0-9_-]", "_"
    $stdoutPath = Join-Path $LogDir ("agv801_810_viewmain_loop_{0}_{1}_run_{2}.stdout.tmp" -f $RunStamp, $safeDevice, $runIndex)
    $stderrPath = Join-Path $LogDir ("agv801_810_viewmain_loop_{0}_{1}_run_{2}.stderr.tmp" -f $RunStamp, $safeDevice, $runIndex)
    $argList = New-ProbeArgumentList `
        -ServerArg $Server `
        -ChannelArg $Channel `
        -DeviceArg $State.Device `
        -GroupArg $Group `
        -CsvArg $Csv `
        -ItemFormatArg $ItemFormat `
        -SourceArg $Source `
        -ReadDelayMsArg $ReadDelayMs

    $State.Started = $deviceStart
    $State.NextDue = $deviceStart.AddMilliseconds($ReadIntervalMs)
    $State.StdoutPath = $stdoutPath
    $State.StderrPath = $stderrPath
    $State.StartError = $null

    try {
        $process = Start-Process `
            -FilePath $Probe `
            -ArgumentList $argList `
            -WorkingDirectory $Root `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -WindowStyle Hidden `
            -PassThru

        $State.Process = $process
        Write-Summary (
            "{0} run={1} start pid={2} nextDue={3}" -f
            $State.Device,
            $runIndex,
            $process.Id,
            $State.NextDue.ToString("HH:mm:ss.fff")
        )
    }
    catch {
        $State.Process = $null
        $State.StartError = $_.Exception.Message
        Write-Summary "$($State.Device) run=$runIndex start failed: $($State.StartError)"
    }
}

function Complete-ProbeRun {
    param(
        [pscustomobject]$State,
        [bool]$TimedOut
    )

    $runIndex = $State.RunIndex
    if ($State.StartError) {
        $output = @("Process start failed: $($State.StartError)")
        $exitCode = -998
    }
    else {
        $output = @(Get-ProcessOutput -StdoutPath $State.StdoutPath -StderrPath $State.StderrPath)
        $exitCode = if ($TimedOut) { -999 } elseif ($State.Process -ne $null) { $State.Process.ExitCode } else { -1 }
    }
    $deviceElapsed = [math]::Round(((Get-Date) - $State.Started).TotalSeconds, 3)
    $scheduleLagMs = [math]::Round(((Get-Date) - $State.NextDue).TotalMilliseconds)

    Add-Content -Path $FullLog -Value "" -Encoding UTF8
    Add-Content -Path $FullLog -Value "===== $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff') $($State.Device) run=$runIndex end timedOut=$TimedOut exit=$exitCode elapsed=${deviceElapsed}s nextDue=$($State.NextDue.ToString('HH:mm:ss.fff')) scheduleLagMs=$scheduleLagMs =====" -Encoding UTF8
    $output | Add-Content -Path $FullLog -Encoding UTF8

    foreach ($tempFile in @($State.StdoutPath, $State.StderrPath)) {
        if ($tempFile -and (Test-Path $tempFile)) {
            Remove-Item -LiteralPath $tempFile -Force
        }
    }

    $addLine = $output | Select-String -Pattern "AddItems success:" | Select-Object -Last 1
    $readLine = $output | Select-String -Pattern "Read success:" | Select-Object -Last 1
    $addStats = Get-SuccessPair -Line $addLine.Line -Prefix "AddItems success:"
    $readStatsSummary = Get-SuccessPair -Line $readLine.Line -Prefix "Read success:"
    $readStats = Get-ReadStats -Output $output

    Write-Summary (
        "{0} run={1} end exit={2} elapsed={3}s nextDue={4} lagMs={5} add={6}/{7} read={8}/{9} readLines={10} null={11} goodQuality={12} badQuality={13} failLines={14}" -f
        $State.Device,
        $runIndex,
        $exitCode,
        $deviceElapsed,
        $State.NextDue.ToString("HH:mm:ss.fff"),
        $scheduleLagMs,
        $addStats.Ok,
        $addStats.Total,
        $readStatsSummary.Ok,
        $readStatsSummary.Total,
        $readStats.Total,
        $readStats.NullValue,
        $readStats.GoodQuality,
        $readStats.BadQuality,
        $readStats.Fail
    )

    $State.Process = $null
    $State.Started = $null
    $State.StdoutPath = $null
    $State.StderrPath = $null
    $State.StartError = $null
}

$deviceStates = @()
$initialDue = Get-Date
foreach ($device in $Devices) {
    $deviceStates += [pscustomobject]@{
        Device = $device
        RunIndex = 0
        Process = $null
        Started = $null
        NextDue = $initialDue
        StdoutPath = $null
        StderrPath = $null
        StartError = $null
    }
}

try {
    while ($true) {
        $now = Get-Date

        foreach ($state in $deviceStates) {
            if ($state.Process -eq $null) {
                continue
            }

            $timedOut = (($now - $state.Started).TotalSeconds -ge $DeviceTimeoutSeconds)
            if ($state.Process.HasExited -or $timedOut) {
                if ($timedOut -and -not $state.Process.HasExited) {
                    try {
                        $state.Process.Kill()
                        $state.Process.WaitForExit()
                    }
                    catch {
                        Write-Summary "$($state.Device) run=$($state.RunIndex) kill failed: $($_.Exception.Message)"
                    }
                    Write-Summary "$($state.Device) run=$($state.RunIndex) timeout after ${DeviceTimeoutSeconds}s"
                }
                Complete-ProbeRun -State $state -TimedOut $timedOut
            }
        }

        $now = Get-Date
        foreach ($state in $deviceStates) {
            if ($state.Process -eq $null -and $now -ge $state.NextDue) {
                Start-ProbeRun -State $state
            }
        }

        Start-Sleep -Milliseconds 50
    }
}
finally {
    Write-Summary "Stopping; terminating running probe processes."
    foreach ($state in $deviceStates) {
        if ($state.Process -ne $null -and -not $state.Process.HasExited) {
            try {
                $state.Process.Kill()
                $state.Process.WaitForExit()
            }
            catch {
                Write-Summary "$($state.Device) run=$($state.RunIndex) stop kill failed: $($_.Exception.Message)"
            }
        }
    }
}
