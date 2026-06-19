param(
    [ValidateRange(1, 86400)]
    [int]$DurationSeconds = 1800,

    [ValidateRange(1, 3600)]
    [int]$SampleIntervalSeconds = 5,

    [string]$OutputRoot = "D:\data_watch_release\performance-baselines",
    [int]$RandomSeed = 20260619,
    [string[]]$ProcessNames = @("data-watch-collector", "data-watch-writer"),
    [string]$RedisContainer = "data-watch-redis",
    [string]$RedisConsumerGroup = "writer_cg",
    [ValidateRange(1, 3600)]
    [int]$RedisDetailEverySamples = 6,
    [string]$TdengineContainer = "data-watch-tdengine",
    [string]$CollectorLog = "",
    [string]$WriterLog = ""
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$OutputDir = Join-Path $OutputRoot $Timestamp
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

if (-not $CollectorLog -or -not $WriterLog) {
    $DataRoot = $env:DATAWATCH_DATA_ROOT
    if (-not $DataRoot) {
        $DataRoot = Join-Path $env:ProgramData "DataWatch"
    }
    if (-not $CollectorLog) {
        $CollectorLog = Join-Path $DataRoot "collector\logs\current.log"
    }
    if (-not $WriterLog) {
        $WriterLog = Join-Path $DataRoot "writer\logs\current.log"
    }
}

function Invoke-NativeText {
    param([scriptblock]$Command)
    try {
        return ((& $Command 2>&1) | Out-String).Trim()
    }
    catch {
        return "unavailable: $($_.Exception.Message)"
    }
}

function Get-PropertyValue {
    param($InputObject, [string]$Name)
    $Property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $Property) { return $null }
    return $Property.Value
}

$GitCommit = Invoke-NativeText { git -C $RepoRoot rev-parse HEAD }
$GitStatus = Invoke-NativeText { git -C $RepoRoot status --short }
$DockerVersion = Invoke-NativeText { docker version --format "{{json .}}" }
$ComposeVersion = Invoke-NativeText { docker compose version }
$OsInfo = Get-CimInstance Win32_OperatingSystem
$CpuInfo = @(Get-CimInstance Win32_Processor)
$ComputerInfo = Get-CimInstance Win32_ComputerSystem

$Manifest = [ordered]@{
    schema_version = 1
    captured_at = (Get-Date).ToString("o")
    duration_seconds = $DurationSeconds
    sample_interval_seconds = $SampleIntervalSeconds
    random_seed = $RandomSeed
    repository_root = $RepoRoot
    git_commit = $GitCommit
    git_status = $GitStatus
    command = $MyInvocation.Line
    process_names = $ProcessNames
    redis_container = $RedisContainer
    redis_consumer_group = $RedisConsumerGroup
    redis_detail_every_samples = $RedisDetailEverySamples
    tdengine_container = $TdengineContainer
    collector_log = $CollectorLog
    writer_log = $WriterLog
    machine = [ordered]@{
        name = $env:COMPUTERNAME
        os = $OsInfo.Caption
        os_version = $OsInfo.Version
        logical_processors = $ComputerInfo.NumberOfLogicalProcessors
        memory_bytes = [int64]$ComputerInfo.TotalPhysicalMemory
        cpu = @($CpuInfo | ForEach-Object {
            [ordered]@{
                name = $_.Name
                cores = $_.NumberOfCores
                logical_processors = $_.NumberOfLogicalProcessors
            }
        })
    }
    docker_version = $DockerVersion
    docker_compose_version = $ComposeVersion
}
$Manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDir "manifest.json") -Encoding utf8

$PythonCandidates = @(
    (Join-Path $RepoRoot "collector\.venv\Scripts\python.exe"),
    (Join-Path $RepoRoot "writer\.venv-pack\Scripts\python.exe")
)
foreach ($Python in $PythonCandidates) {
    if (-not (Test-Path -LiteralPath $Python)) { continue }
    $Name = Split-Path -Leaf (Split-Path -Parent (Split-Path -Parent $Python))
    Invoke-NativeText { & $Python -VV } |
        Set-Content -LiteralPath (Join-Path $OutputDir "$Name-python.txt") -Encoding utf8
    Invoke-NativeText { & $Python -m pip freeze --all } |
        Set-Content -LiteralPath (Join-Path $OutputDir "$Name-pip-freeze.txt") -Encoding utf8
}

$ProcessCsv = Join-Path $OutputDir "process-samples.csv"
$DiskCsv = Join-Path $OutputDir "disk-samples.csv"
$DockerStats = Join-Path $OutputDir "docker-stats.jsonl"
$RedisInfo = Join-Path $OutputDir "redis-info.log"
$RedisStreamsCsv = Join-Path $OutputDir "redis-streams.csv"
$Deadline = (Get-Date).AddSeconds($DurationSeconds)
$SampleNumber = 0

while ((Get-Date) -lt $Deadline) {
    $SampleNumber += 1
    $Now = (Get-Date).ToString("o")

    $ProcessRows = foreach ($Name in $ProcessNames) {
        foreach ($Process in @(Get-Process -Name $Name -ErrorAction SilentlyContinue)) {
            [pscustomobject]@{
                timestamp = $Now
                name = $Process.ProcessName
                pid = $Process.Id
                cpu_seconds = $Process.CPU
                working_set_bytes = $Process.WorkingSet64
                private_memory_bytes = $Process.PrivateMemorySize64
                thread_count = $Process.Threads.Count
                handle_count = $Process.HandleCount
                io_read_bytes = Get-PropertyValue $Process "IOReadBytes"
                io_write_bytes = Get-PropertyValue $Process "IOWriteBytes"
            }
        }
    }
    if ($ProcessRows) {
        $ProcessRows | Export-Csv -LiteralPath $ProcessCsv -NoTypeInformation -Append -Encoding utf8
    }

    try {
        Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk |
            Where-Object { $_.Name -eq "_Total" } |
            Select-Object @{n="timestamp";e={$Now}}, Name, DiskReadBytesPersec,
                DiskWriteBytesPersec, AvgDisksecPerRead, AvgDisksecPerWrite,
                CurrentDiskQueueLength |
            Export-Csv -LiteralPath $DiskCsv -NoTypeInformation -Append -Encoding utf8
    }
    catch {
        "${Now},disk_metrics_unavailable,$($_.Exception.Message)" |
            Add-Content -LiteralPath $DiskCsv -Encoding utf8
    }

    $Stats = Invoke-NativeText { docker stats --no-stream --format "{{json .}}" }
    if ($Stats) {
        foreach ($Line in ($Stats -split "`r?`n")) {
            [ordered]@{ timestamp = $Now; stats = $Line } | ConvertTo-Json -Compress |
                Add-Content -LiteralPath $DockerStats -Encoding utf8
        }
    }

    "===== $Now =====" | Add-Content -LiteralPath $RedisInfo -Encoding utf8
    Invoke-NativeText {
        docker exec $RedisContainer redis-cli --no-auth-warning INFO memory persistence stats
    } | Add-Content -LiteralPath $RedisInfo -Encoding utf8

    if ($SampleNumber % $RedisDetailEverySamples -eq 1) {
        $StreamKeys = @(
            Invoke-NativeText {
                docker exec $RedisContainer redis-cli --no-auth-warning --raw --scan --pattern "rt:stream:*"
            } -split "`r?`n" | Where-Object { $_ -like "rt:stream:*" }
        )
        foreach ($StreamKey in $StreamKeys) {
            $LengthText = Invoke-NativeText {
                docker exec $RedisContainer redis-cli --no-auth-warning --raw XLEN $StreamKey
            }
            $PendingLines = @(
                Invoke-NativeText {
                    docker exec $RedisContainer redis-cli --no-auth-warning --raw XPENDING $StreamKey $RedisConsumerGroup
                } -split "`r?`n"
            )
            [int64]$PendingCount = 0
            [void][int64]::TryParse($PendingLines[0], [ref]$PendingCount)
            $OldestPendingId = if ($PendingLines.Count -ge 2) { $PendingLines[1] } else { "" }
            $OldestPendingAgeMs = $null
            if ($OldestPendingId -match '^(\d+)-') {
                $NowMs = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
                $OldestPendingAgeMs = [Math]::Max(0, $NowMs - [int64]$Matches[1])
            }
            [pscustomobject]@{
                timestamp = $Now
                stream = $StreamKey
                xlen = $LengthText
                pending_count = $PendingCount
                oldest_pending_id = $OldestPendingId
                oldest_pending_age_ms = $OldestPendingAgeMs
            } | Export-Csv -LiteralPath $RedisStreamsCsv -NoTypeInformation -Append -Encoding utf8
        }
    }

    Start-Sleep -Seconds $SampleIntervalSeconds
}

if (Test-Path -LiteralPath $CollectorLog) {
    Copy-Item -LiteralPath $CollectorLog -Destination (Join-Path $OutputDir "collector-current.log") -Force
}
if (Test-Path -LiteralPath $WriterLog) {
    Copy-Item -LiteralPath $WriterLog -Destination (Join-Path $OutputDir "writer-current.log") -Force
}

[ordered]@{
    container = $RedisContainer
    inspect_output = Invoke-NativeText { docker inspect $RedisContainer }
} | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $OutputDir "redis-inspect.json") -Encoding utf8
[ordered]@{
    container = $TdengineContainer
    inspect_output = Invoke-NativeText { docker inspect $TdengineContainer }
} | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $OutputDir "tdengine-inspect.json") -Encoding utf8

Write-Host "Performance baseline captured: $OutputDir"
