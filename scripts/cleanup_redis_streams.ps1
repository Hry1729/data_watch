[CmdletBinding()]
param(
    [string]$RedisContainer = "data-watch-redis",
    [string]$ConsumerGroup = "writer_cg",
    [string]$StreamPattern = "rt:stream:*",
    [string]$RedisPassword = $env:REDIS_PASSWORD,
    [switch]$NoRedisAuth,
    [int]$DrainTimeoutSeconds = 600,
    [int]$PollIntervalSeconds = 2
)

$ErrorActionPreference = "Stop"
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

function Write-Step([string]$Message) {
    Write-Host "`n== $Message ==" -ForegroundColor Cyan
}

function Read-Confirmation([string]$Prompt, [string]$Expected) {
    $answer = Read-Host $Prompt
    if ($answer -cne $Expected) {
        throw "Confirmation did not match '$Expected'; no Redis data was changed."
    }
}

function Invoke-Redis {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$RedisArguments,
        [switch]$AllowFailure
    )

    $dockerArguments = @("exec")
    if ($RedisPassword) {
        $dockerArguments += @("--env", "REDISCLI_AUTH=$RedisPassword")
    }
    $dockerArguments += @($RedisContainer, "redis-cli", "--no-auth-warning", "--raw")
    $dockerArguments += $RedisArguments

    $output = & docker @dockerArguments 2>&1
    $exitCode = $LASTEXITCODE
    $text = (($output | ForEach-Object { "$_" }) -join "`n").Trim()
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        throw "redis-cli failed (exit $exitCode): $text"
    }
    return [pscustomobject]@{
        ExitCode = $exitCode
        Text = $text
    }
}

function Get-StreamKeys {
    $result = Invoke-Redis -RedisArguments @("--scan", "--pattern", $StreamPattern)
    if (-not $result.Text) { return @() }
    return @(
        $result.Text -split "`r?`n" |
            Where-Object { $_ -like "rt:stream:*" } |
            Sort-Object -Unique
    )
}


function Get-StreamState([string]$StreamKey) {
    $lengthResult = Invoke-Redis -RedisArguments @("XLEN", $StreamKey)
    $groupResult = Invoke-Redis -RedisArguments @("XINFO", "GROUPS", $StreamKey) -AllowFailure
    if ($groupResult.ExitCode -ne 0) {
        return [pscustomobject]@{
            Key = $StreamKey
            Length = [long]$lengthResult.Text
            Pending = $null
            Lag = $null
            Safe = $false
            Reason = "cannot read consumer groups: $($groupResult.Text)"
        }
    }

    $lines = if ($groupResult.Text) { @($groupResult.Text -split "`r?`n") } else { @() }
    $groups = @()
    $currentGroup = $null
    for ($index = 0; $index + 1 -lt $lines.Count; $index += 2) {
        $field = $lines[$index]
        $value = $lines[$index + 1]
        if ($field -eq "name") {
            if ($null -ne $currentGroup) { $groups += ,$currentGroup }
            $currentGroup = @{}
        }
        if ($null -ne $currentGroup) { $currentGroup[$field] = $value }
    }
    if ($null -ne $currentGroup) { $groups += ,$currentGroup }
    $group = $groups | Where-Object { $_["name"] -eq $ConsumerGroup } | Select-Object -First 1
    if (-not $group) {
        return [pscustomobject]@{
            Key = $StreamKey
            Length = [long]$lengthResult.Text
            Pending = $null
            Lag = $null
            Safe = $false
            Reason = "consumer group '$ConsumerGroup' does not exist"
        }
    }

    $pending = [long]$group["pending"]
    $lagText = $group["lag"]
    $lag = if ($lagText -match "^\d+$") { [long]$lagText } else { $null }
    $safe = $pending -eq 0 -and $null -ne $lag -and $lag -eq 0
    return [pscustomobject]@{
        Key = $StreamKey
        Length = [long]$lengthResult.Text
        Pending = $pending
        Lag = $lag
        Safe = $safe
        Reason = if ($safe) { "caught up" } elseif ($null -eq $lag) { "lag is unavailable" } else { "writer has not caught up" }
    }
}

function Get-AllStreamStates([string[]]$Keys) {
    return @($Keys | ForEach-Object { Get-StreamState -StreamKey $_ })
}

function Show-StreamStates([object[]]$States) {
    $States |
        Select-Object Key, Length, Pending, Lag, Safe, Reason |
        Format-Table -AutoSize |
        Out-Host
}

function Wait-ForWriterDrain([string[]]$Keys) {
    $deadline = (Get-Date).AddSeconds($DrainTimeoutSeconds)
    do {
        $states = Get-AllStreamStates -Keys $Keys
        Clear-Host
        Write-Host "DataWatch Redis Stream safe cleanup" -ForegroundColor Cyan
        Write-Host "Waiting for Writer: pending=0 and lag=0 for every Stream."
        Show-StreamStates -States $states
        if (($states | Where-Object { -not $_.Safe }).Count -eq 0) {
            return $states
        }
        if ((Get-Date) -ge $deadline) {
            throw "Writer did not catch up within $DrainTimeoutSeconds seconds; no Redis data was changed."
        }
        Start-Sleep -Seconds $PollIntervalSeconds
    } while ($true)
}

Write-Host "DataWatch Redis Stream safe cleanup" -ForegroundColor Cyan
Write-Host "This tool only trims '$StreamPattern'. It never runs FLUSHDB."

docker info *> $null
if ($LASTEXITCODE -ne 0) {
    throw "Docker is unavailable. Start Docker Desktop and try again."
}

$containerRunning = docker inspect -f "{{.State.Running}}" $RedisContainer 2>$null
if ($LASTEXITCODE -ne 0 -or $containerRunning.Trim() -ne "true") {
    throw "Redis container '$RedisContainer' is not running."
}

if (-not $NoRedisAuth -and -not $RedisPassword) {
    $securePassword = Read-Host "Redis password (press Enter if authentication is disabled)" -AsSecureString
    if ($securePassword.Length -gt 0) {
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
        try {
            $RedisPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
        } finally {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
        }
    }
}

$ping = Invoke-Redis -RedisArguments @("PING")
if ($ping.Text -ne "PONG") {
    throw "Redis authentication or connectivity check failed: $($ping.Text)"
}

Write-Step "Stop data producers"
Write-Host "Distributed center: stop Ingest. Centralized: stop Collector."
Write-Host "Keep Writer running so it can finish all queued entries."
Read-Confirmation -Prompt "After the producer is stopped, type DRAIN" -Expected "DRAIN"

$keys = @(Get-StreamKeys)
if ($keys.Count -eq 0) {
    Write-Host "No matching Stream keys were found; nothing to clean." -ForegroundColor Yellow
    exit 0
}

Write-Step "Wait for Writer to catch up"
$drainedStates = Wait-ForWriterDrain -Keys $keys
$totalBefore = ($drainedStates | Measure-Object -Property Length -Sum).Sum
Write-Host "All $($keys.Count) Streams are caught up; $totalBefore entries can be removed." -ForegroundColor Green

Write-Step "Stop Writer and freeze Redis Stream input"
Write-Host "Stop Writer in the DataWatch console. Do not restart Ingest/Collector yet."
Read-Confirmation -Prompt "After Writer is stopped, type STOPPED" -Expected "STOPPED"

Start-Sleep -Seconds 2
$finalKeys = @(Get-StreamKeys)
if (Compare-Object -ReferenceObject $keys -DifferenceObject $finalKeys) {
    throw "The Stream key set changed after the drain check; no Redis data was changed."
}
$finalStates = Get-AllStreamStates -Keys $keys
Show-StreamStates -States $finalStates
$unsafe = @($finalStates | Where-Object { -not $_.Safe })
if ($unsafe.Count -gt 0) {
    throw "At least one Stream is no longer caught up; no Redis data was changed."
}
$totalFinal = ($finalStates | Measure-Object -Property Length -Sum).Sum
if ($totalFinal -ne $totalBefore) {
    throw "Stream length changed after Writer was stopped ($totalBefore -> $totalFinal); no Redis data was changed."
}

Write-Step "Final confirmation"
Write-Host "Ready to remove $totalFinal acknowledged entries from $($keys.Count) Streams." -ForegroundColor Yellow
Read-Confirmation -Prompt "Type CLEAN to execute XTRIM MAXLEN 0" -Expected "CLEAN"

$reportRows = @()
foreach ($key in $keys) {
    $before = (Get-StreamState -StreamKey $key).Length
    $trimResult = Invoke-Redis -RedisArguments @("XTRIM", $key, "MAXLEN", "0")
    $after = [long](Invoke-Redis -RedisArguments @("XLEN", $key)).Text
    $reportRows += [pscustomobject]@{
        Stream = $key
        Before = $before
        Removed = [long]$trimResult.Text
        After = $after
    }
}

$purgeResult = Invoke-Redis -RedisArguments @("MEMORY", "PURGE") -AllowFailure
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$reportPath = Join-Path $PSScriptRoot "redis-stream-cleanup-$timestamp.csv"
$reportRows | Export-Csv -LiteralPath $reportPath -NoTypeInformation -Encoding utf8

Write-Step "Cleanup result"
$reportRows | Format-Table -AutoSize | Out-Host
if ($purgeResult.ExitCode -ne 0) {
    Write-Host "MEMORY PURGE was not supported or failed; this does not affect Stream cleanup." -ForegroundColor Yellow
}
Write-Host "Report: $reportPath"
Write-Host "Restart Writer first, then restart Ingest/Collector." -ForegroundColor Green
