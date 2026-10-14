# Windows PowerShell 5.1 / PowerShell 7. No host Python installation required.
[CmdletBinding()]
param(
    [ValidateRange(1, 3600)][int]$DurationSeconds = 180,
    [ValidateRange(5, 120)][int]$SampleIntervalSeconds = 10,
    [string]$RuntimeRoot = '',
    [string]$BackendContainer = 'data-watch-backend',
    [string]$RedisContainer = 'data-watch-redis',
    [string]$TdengineContainer = 'data-watch-tdengine',
    [string]$RedisConsumerGroup = '',
    [ValidateRange(1, 256)][int]$MaxStreams = 64,
    [string]$OutputRoot = ''
)

$ErrorActionPreference = 'Stop'
# Script location is available after parameter binding, including Windows PowerShell.
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $OutputRoot) { $OutputRoot = Join-Path $scriptDirectory 'pipeline-diagnostics' }
$helper = Join-Path $scriptDirectory 'pipeline_diagnostics.py'
if (-not (Test-Path -LiteralPath $helper)) { throw 'Keep pipeline_diagnostics.py beside this script.' }
$docker = (Get-Command docker -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
$sourceBytes = [IO.File]::ReadAllBytes($helper)
$sourceHex = ([BitConverter]::ToString($sourceBytes)).Replace('-', '')
$utf8 = New-Object Text.UTF8Encoding($false)
$bootstrap = 'import sys; exec(bytes.fromhex(sys.stdin.readline().strip()))'

function Invoke-DiagnosticCommand([string[]]$Arguments, [string]$InputText = '', [int]$TimeoutSeconds = 25) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $docker
    # Windows native argument quoting. Input JSON travels via stdin, never as code.
    $quoted = foreach ($argument in $Arguments) {
        '"' + [regex]::Replace([regex]::Replace($argument, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
    }
    $info.Arguments = $quoted -join ' '
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.StandardOutputEncoding = $utf8
    $info.StandardErrorEncoding = $utf8
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    try {
        $null = $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if ($InputText) {
            $write = $process.StandardInput.WriteAsync($InputText)
            if (-not $write.Wait($TimeoutSeconds * 1000)) { $process.Kill(); throw 'Diagnostic stdin timed out.' }
        }
        $process.StandardInput.Close()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) {
            $process.Kill()
            throw 'Diagnostic command timed out. Business containers were not stopped.'
        }
        if ($process.ExitCode -ne 0) { throw 'Diagnostic command failed. Check Docker, container name and Python runtime.' }
        return $stdout.GetAwaiter().GetResult().Trim()
    }
    finally { $process.Dispose() }
}

function Invoke-Helper([string]$Mode, [string]$Data = '') {
    $arguments = @('exec', '-i', $BackendContainer, $containerPython, '-c', $bootstrap, $Mode,
                  '--max-streams', [string]$MaxStreams)
    if ($RedisConsumerGroup) { $arguments += @('--group', $RedisConsumerGroup) }
    # ASCII wire data also works under Windows PowerShell's legacy console encodings.
    $asciiData = [regex]::Replace($Data, '[^\x00-\x7f]', { param($match) '\u{0:x4}' -f [int][char]$match.Value })
    $text = Invoke-DiagnosticCommand -Arguments $arguments -InputText ($sourceHex + "`n" + $asciiData)
    return ($text | ConvertFrom-Json)
}

# Only execute fixed version queries; never inspect/export container environment variables.
$containerPython = $null
foreach ($candidate in @('python3.14t', 'python3', 'python')) {
    try {
        $null = Invoke-DiagnosticCommand -Arguments @('exec', $BackendContainer, $candidate, '-V')
        $containerPython = $candidate
        break
    } catch { }
}
if (-not $containerPython) { throw 'Backend container unavailable. Start it or use -BackendContainer with its actual name.' }

$roots = @()
if ($RuntimeRoot) {
    $roots = @([IO.Path]::GetFullPath($RuntimeRoot))
} else {
    if ($env:DATAWATCH_DATA_ROOT) { $roots += Join-Path $env:DATAWATCH_DATA_ROOT 'runtime' }
    foreach ($name in @('DataWatch', 'DataWatch Distributed Center', 'DataWatch Edge')) {
        $roots += Join-Path (Join-Path $env:ProgramData $name) 'runtime'
    }
    $roots = @($roots | Select-Object -Unique | Where-Object { Test-Path -LiteralPath $_ })
}
$logFiles = @(
    foreach ($root in $roots) {
        foreach ($module in @('collector', 'writer', 'storage', 'uploader', 'ingest')) {
            $path = Join-Path $root "logs\$module\current.log"
            if (Test-Path -LiteralPath $path) { [pscustomobject]@{ module = $module; path = $path } }
        }
    }
)

function Get-LogTime([string]$Line) {
    if ($Line -match '(\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:[.,]\d{3,6})?)') {
        $parsed = [DateTimeOffset]::MinValue
        if ([DateTimeOffset]::TryParse($Matches[1].Replace(',', '.'), [Globalization.CultureInfo]::InvariantCulture,
            [Globalization.DateTimeStyles]::AssumeLocal, [ref]$parsed)) { return $parsed.ToUniversalTime().ToString('o') }
    }
    return $null
}

function Get-LogSample($Log) {
    # Bounded tail; shared handles allow the Agent to rotate/truncate its logs.
    $stream = $null
    try {
        $stream = [IO.File]::Open($Log.path, [IO.FileMode]::Open, [IO.FileAccess]::Read,
                                 [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
        $offset = [Math]::Max(0, $stream.Length - 4MB)
        $null = $stream.Seek($offset, [IO.SeekOrigin]::Begin)
        $bytes = New-Object byte[] ([int]($stream.Length - $offset))
        $read = $stream.Read($bytes, 0, $bytes.Length)
        $lines = $utf8.GetString($bytes, 0, $read) -split "`r?`n"
        if ($offset -gt 0) { $lines = @($lines | Select-Object -Skip 1) }
        $snapshots = @()
        $warnings = @()
        $performanceLines = @($lines | Where-Object { $_ -match 'Performance snapshot: |\[performance(?:-aggregate)?\]' } | Select-Object -Last 12)
        $warningLines = @($lines | Where-Object { $_ -match '\b(WARNING|WARN|ERROR|CRITICAL)\b' } | Select-Object -Last 100)
        foreach ($line in $performanceLines) {
            if ($line -match '(?:Performance snapshot: |\[performance(?:-aggregate)?\] )(\{.*\})\s*$') {
                try {
                    $payload = $Matches[1] | ConvertFrom-Json
                    $safe = [ordered]@{}
                    foreach ($field in @('consumer_name', 'timings', 'counters', 'gauges', 'runtime', 'distributions', 'workers', 'aggregate')) {
                        if ($payload.PSObject.Properties[$field]) { $safe[$field] = $payload.$field }
                    }
                    $snapshots += [pscustomobject]@{ at = Get-LogTime $line; payload = $safe }
                } catch { }
            }
        }
        foreach ($line in $warningLines) {
                # Keep time and a category; never export SQL text, frame payloads or secrets.
                $reason = 'other'
                if ($line -match '(?i)timeout|timed out|超时') { $reason = 'timeout' }
                elseif ($line -match '(?i)connection|broken pipe|unreachable|连接') { $reason = 'connection' }
                elseif ($line -match '(?i)0x0219|statement too long|sql too long|permanent size') { $reason = 'sql_size' }
                elseif ($line -match '(?i)schema|column|stable|mapping') { $reason = 'schema_or_mapping' }
                elseif ($line -match '(?i)saturated|capacity reached|dropping cycle') { $reason = 'executor_full' }
                elseif ($line -match '(?i)flush failed|backing off') { $reason = 'write_backoff' }
                $hash = [Security.Cryptography.SHA256]::Create()
                try { $fingerprint = ([BitConverter]::ToString($hash.ComputeHash($utf8.GetBytes($line)))).Replace('-', '') }
                finally { $hash.Dispose() }
                $warnings += [pscustomobject]@{ at = Get-LogTime $line; reason = $reason; fingerprint = $fingerprint }
        }
        return [pscustomobject]@{ module = $Log.module; path = $Log.path;
            snapshots = @($snapshots | Select-Object -Last 12); warnings = @($warnings | Select-Object -Last 100) }
    } catch {
        return [pscustomobject]@{ module = $Log.module; path = $Log.path; error = 'log_unavailable'; snapshots = @(); warnings = @() }
    } finally { if ($stream) { $stream.Dispose() } }
}

$output = Join-Path $OutputRoot ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6))
$null = New-Item -ItemType Directory -Path $output -Force
$output = [IO.Path]::GetFullPath($output)
$startedAt = [DateTimeOffset]::UtcNow.ToString('o')
$clock = [Diagnostics.Stopwatch]::StartNew()
$samples = New-Object 'Collections.Generic.List[object]'
Write-Host "Observing the live pipeline for $DurationSeconds seconds. Keep collection and Writer running."
Write-Host "Output: $output"
do {
    $cycleStart = $clock.Elapsed.TotalSeconds
    $row = [ordered]@{ at = [DateTimeOffset]::UtcNow.ToString('o'); elapsed_seconds = $cycleStart }
    try { $row.probe = Invoke-Helper 'sample' } catch { $row.probe_error = 'probe_unavailable' }
    $row.logs = @($logFiles | ForEach-Object { Get-LogSample $_ })
    $row.processes = @(
        Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -match '^data-watch-.*(collector|writer|storage|uploader|ingest|agent)$' } |
        ForEach-Object {
            $cpu = $null
            $start = $null
            try { $cpu = $_.TotalProcessorTime.TotalSeconds; $start = $_.StartTime.ToUniversalTime().ToString('o') } catch { }
            [pscustomobject]@{ name = $_.ProcessName; pid = $_.Id; started_at = $start;
                cpu_seconds = $cpu; rss_bytes = $_.WorkingSet64 }
        }
    )
    try {
        $os = Get-CimInstance Win32_OperatingSystem
        $row.host = [ordered]@{ free_memory_bytes = [int64]$os.FreePhysicalMemory * 1024;
            total_memory_bytes = [int64]$os.TotalVisibleMemorySize * 1024;
            logical_processors = [Environment]::ProcessorCount }
        $row.disk = @(Get-CimInstance Win32_PerfFormattedData_PerfDisk_PhysicalDisk |
            Where-Object { $_.Name -eq '_Total' } |
            Select-Object DiskReadBytesPersec, DiskWriteBytesPersec, AvgDisksecPerRead, AvgDisksecPerWrite, CurrentDiskQueueLength)
        $row.disk_raw = @(Get-CimInstance Win32_PerfRawData_PerfDisk_PhysicalDisk |
            Where-Object { $_.Name -eq '_Total' } |
            Select-Object AvgDisksecPerRead, AvgDisksecPerRead_Base, AvgDisksecPerWrite, AvgDisksecPerWrite_Base, Frequency_PerfTime)
    } catch { $row.host_error = 'host_metrics_unavailable' }
    try {
        $stats = Invoke-DiagnosticCommand -Arguments @('stats', '--no-stream', '--format', '{{json .}}', $RedisContainer, $TdengineContainer, $BackendContainer)
        $row.docker = @($stats -split "`r?`n" | Where-Object { $_ } | ForEach-Object { $_ | ConvertFrom-Json })
    } catch { $row.docker_error = 'container_metrics_unavailable' }
    $samples.Add([pscustomobject]$row)
    [IO.File]::AppendAllText((Join-Path $output 'samples.jsonl'), (($row | ConvertTo-Json -Depth 35 -Compress) + "`n"), $utf8)
    Write-Host ("Captured sample {0}, elapsed {1:N0}s" -f $samples.Count, $clock.Elapsed.TotalSeconds)
    if ($clock.Elapsed.TotalSeconds -ge $DurationSeconds -and $samples.Count -ge 2) { break }
    $sleepMs = [Math]::Min([Math]::Max(0, ($cycleStart + $SampleIntervalSeconds - $clock.Elapsed.TotalSeconds) * 1000),
                         [Math]::Max(0, ($DurationSeconds - $clock.Elapsed.TotalSeconds) * 1000))
    if ($sleepMs -gt 0) { Start-Sleep -Milliseconds ([int]$sleepMs) }
} while ($true)

$inputData = [ordered]@{ started_at = $startedAt; ended_at = [DateTimeOffset]::UtcNow.ToString('o');
    runtime_roots = $roots; samples = @($samples.ToArray()) }
try {
    $result = Invoke-Helper 'report' ($inputData | ConvertTo-Json -Depth 35 -Compress)
    [IO.File]::WriteAllText((Join-Path $output 'summary.json'), ($result.summary | ConvertTo-Json -Depth 40), $utf8)
    [IO.File]::WriteAllText((Join-Path $output 'report.md'), $result.markdown, (New-Object Text.UTF8Encoding($true)))
    Write-Host "Done. Read report.md, then send this output folder for analysis."
    Write-Host (Join-Path $output 'report.md')
} catch {
    [IO.File]::WriteAllText((Join-Path $output 'report.md'), "Report generation failed: Backend container unavailable. samples.jsonl was preserved.", $utf8)
    Write-Warning "Report unavailable. Samples are preserved at $output"
    exit 2
}
