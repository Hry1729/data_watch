# Target-only configuration and observation. Uses the installed Backend Python.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('baseline', 'workers8', 'batch400', 'restore', 'capture')][string]$Mode,
    [Parameter(Mandatory = $true)][string]$SettingsPath,
    [string]$RuntimeRoot = '',
    [string]$BackendContainer = 'data-watch-backend',
    [string]$RedisConsumerGroup = '',
    [ValidateRange(120, 3600)][int]$DurationSeconds = 300,
    [string]$OutputRoot = ''
)
$ErrorActionPreference = 'Stop'
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$logRoot = if ($OutputRoot) { [IO.Path]::GetFullPath($OutputRoot) } else { $scriptDirectory }
$logDirectory = Join-Path $logRoot 'operation-logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$operationLog = Join-Path $logDirectory ('test-' + $Mode + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6) + '.log')
function Write-Operation([string]$Message) {
    [IO.File]::AppendAllText($operationLog, ([DateTimeOffset]::Now.ToString('o') + ' ' + $Message + "`r`n"), (New-Object Text.UTF8Encoding($false)))
}
trap {
    Write-Operation ('failed: ' + $_.Exception.GetBaseException().Message)
    Write-Host "Operation failed. Log: $operationLog"
    throw $_
}
Write-Operation ('started; PowerShell=' + $PSVersionTable.PSVersion + '; mode=' + $Mode + '; settings=' + $SettingsPath)
Write-Host "Operation log: $operationLog"
$settings = (Resolve-Path -LiteralPath $SettingsPath).Path
$statePath = $settings + '.writer-test-state.json'
$utf8 = New-Object Text.UTF8Encoding($false)
function Writer-Section([string]$Text) {
    $sectionMatches = [regex]::Matches($Text, '(?ms)^[ \t]*\[writer\][ \t]*(?:#[^\r\n]*)?\r?\n(?<body>.*?)(?=^[ \t]*\[|\z)')
    if ($sectionMatches.Count -ne 1) { throw 'Expected exactly one [writer] table.' }
    return $sectionMatches[0].Groups['body']
}
function Set-WriterKey([string]$Text, [string]$Key, [int]$Value) {
    $body = Writer-Section $Text
    $pattern = '(?m)^[ \t]*' + [regex]::Escape($Key) + '[ \t]*=[^\r\n]*'
    $keyMatches = [regex]::Matches($body.Value, $pattern)
    if ($keyMatches.Count -gt 1) { throw "Duplicate Writer key: $Key" }
    $line = "$Key = $Value"
    if ($keyMatches.Count) { $updated = [regex]::Replace($body.Value, $pattern, $line) }
    else {
        $newline = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
        $updated = $line + $newline + $body.Value
    }
    return $Text.Remove($body.Index, $body.Length).Insert($body.Index, $updated)
}
function Writer-Int([string]$Text, [string]$Key, [int]$Default) {
    $body = Writer-Section $Text
    $match = [regex]::Match($body.Value, '(?m)^[ \t]*' + [regex]::Escape($Key) + '[ \t]*=[ \t]*(\d+)[ \t]*(?:#[^\r\n]*)?\r?$')
    if ($match.Success) { return [int]$match.Groups[1].Value }
    if ([regex]::IsMatch($body.Value, '(?m)^[ \t]*' + [regex]::Escape($Key) + '[ \t]*=')) { throw "Cannot parse Writer key: $Key" }
    return $Default
}
function Assert-Stopped {
    if (Get-Process -Name 'data-watch-writer' -ErrorAction SilentlyContinue) {
        throw 'Stop Writer from the app before changing parameters. Other roles should keep running.'
    }
}
function Replace-Settings([string]$Text) {
    $temporary = $settings + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    [IO.File]::WriteAllText($temporary, $Text, $utf8)
    [IO.File]::Replace($temporary, $settings, [NullString]::Value)
}
if ($Mode -ne 'capture') {
    Assert-Stopped
    if ($Mode -eq 'restore') {
        if (-not (Test-Path -LiteralPath $statePath)) { throw 'No test baseline exists to restore.' }
        $state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ((Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash -ne $state.last_profile_sha256) {
            throw 'Settings changed outside this test. Preserve that edit before restoring.'
        }
        $restoreTemporary = $settings + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
        Copy-Item -LiteralPath $state.backup_path -Destination $restoreTemporary
        [IO.File]::Replace($restoreTemporary, $settings, [NullString]::Value)
        Remove-Item -LiteralPath $statePath
        Write-Operation ('original settings restored; backup=' + $state.backup_path)
        Write-Host "Original settings restored. Local backup retained at: $($state.backup_path)"
        Write-Host 'Start Writer from the app.'
        return
    }
    if (Test-Path -LiteralPath $statePath) {
        $state = Get-Content -LiteralPath $statePath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ((Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash -ne $state.last_profile_sha256) {
            throw 'Settings changed outside this test; refusing to overwrite them.'
        }
    } else {
        $backup = $settings + '.writer-original-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
        Copy-Item -LiteralPath $settings -Destination $backup
        $backupAcl = Get-Acl -LiteralPath $backup
        $accessOnly = [Security.AccessControl.AccessControlSections]::Access
        $backupAcl.SetSecurityDescriptorSddlForm((Get-Acl -LiteralPath $settings).GetSecurityDescriptorSddlForm($accessOnly), $accessOnly)
        Set-Acl -LiteralPath $backup -AclObject $backupAcl
        $state = [pscustomobject]@{ backup_path = $backup; last_profile_sha256 = (Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash }
        [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json), $utf8)
    }
    $text = [IO.File]::ReadAllText($state.backup_path)
    $workers = if ($Mode -eq 'workers8') { 8 } else { 4 }
    $batch = if ($Mode -eq 'batch400') { 400 } else { 200 }
    $text = Set-WriterKey $text 'consumer_workers' $workers
    $text = Set-WriterKey $text 'batch_count' $batch
    Replace-Settings $text
    $state.last_profile_sha256 = (Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash
    [IO.File]::WriteAllText($statePath, ($state | ConvertTo-Json), $utf8)
    Write-Operation ('profile applied; workers=' + $workers + '; batch_count=' + $batch)
    Write-Host "Applied $Mode : workers=$workers, batch_count=$batch. Other settings retained from the original file."
    Write-Host 'Start Writer from the app, wait about 60 seconds, then run capture.'
    return
}
if (-not (Get-Process -Name 'data-watch-writer' -ErrorAction SilentlyContinue)) { throw 'Writer is stopped. Start Writer from the app, wait about 60 seconds, then run capture.' }
if (-not $RuntimeRoot) {
    $dataRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $settings))
    $RuntimeRoot = Join-Path $dataRoot 'runtime'
}
if (-not (Test-Path -LiteralPath (Join-Path $RuntimeRoot 'logs\writer\current.log'))) {
    throw 'Writer log not found. Set -RuntimeRoot to the Agent runtime directory.'
}
if (-not $OutputRoot) { $OutputRoot = Join-Path $scriptDirectory 'target-results' }
$text = [IO.File]::ReadAllText($settings)
$workers = Writer-Int $text 'consumer_workers' 4
$batch = Writer-Int $text 'batch_count' 200
$runRoot = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) ('w' + $workers + '-bc' + $batch + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6))
New-Item -ItemType Directory -Path $runRoot | Out-Null
$docker = (Get-Command docker -CommandType Application -ErrorAction Stop | Select-Object -First 1).Source
function Invoke-ContainerPython([string[]]$Arguments, [string]$InputText = '', [int]$TimeoutSeconds = 180) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $docker
    $info.Arguments = (@('exec', '-i', $BackendContainer) + $Arguments | ForEach-Object {
        '"' + [regex]::Replace([regex]::Replace($_, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
    }) -join ' '
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardInput = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $process = New-Object Diagnostics.Process
    $process.StartInfo = $info
    try {
        $null = $process.Start()
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if ($InputText) { $process.StandardInput.Write($InputText) }
        $process.StandardInput.Close()
        if (-not $process.WaitForExit($TimeoutSeconds * 1000)) { $process.Kill(); throw 'Data check timed out.' }
        if ($process.ExitCode -ne 0) { throw 'Container Python unavailable.' }
        return $stdout.GetAwaiter().GetResult().Trim()
    } finally { $process.Dispose() }
}
$python = $null
foreach ($candidate in @('python3.14t', 'python3', 'python')) {
    try { $null = Invoke-ContainerPython -Arguments @($candidate, '-V') -TimeoutSeconds 15; $python = $candidate; break } catch { }
}
function Invoke-DataCheck([string]$Operation, $Reference = $null) {
    if (-not $python) { return [pscustomobject]@{ available = $false; status = 'not_tested'; reason = 'container_python_unavailable' } }
    $source = [BitConverter]::ToString([IO.File]::ReadAllBytes((Join-Path $scriptDirectory 'writer_data_check.py'))).Replace('-', '')
    $inputText = $source + "`n"
    if ($null -ne $Reference) { $inputText += ($Reference | ConvertTo-Json -Depth 25 -Compress) }
    $inputText = [regex]::Replace($inputText, '[^\x00-\x7f]', { param($match) '\u{0:x4}' -f [int][char]$match.Value })
    try {
        return (Invoke-ContainerPython -Arguments @($python, '-c', 'import sys; exec(bytes.fromhex(sys.stdin.readline().strip()))', $Operation) -InputText $inputText | ConvertFrom-Json)
    } catch { return [pscustomobject]@{ available = $false; status = 'not_tested'; reason = 'data_check_unavailable' } }
}
Write-Host 'Taking a small read-only frame reference; point values stay in memory.'
$reference = Invoke-DataCheck 'snapshot'
$diagnosticArgs = @{ RuntimeRoot = $RuntimeRoot; BackendContainer = $BackendContainer; DurationSeconds = $DurationSeconds; OutputRoot = $runRoot }
if ($RedisConsumerGroup) { $diagnosticArgs.RedisConsumerGroup = $RedisConsumerGroup }
& (Join-Path $scriptDirectory 'diagnose_pipeline.ps1') @diagnosticArgs
$reportDirectory = @(Get-ChildItem -LiteralPath $runRoot -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'summary.json') })
if ($reportDirectory.Count -ne 1) { throw 'No unique successful diagnostic report was produced; captured samples are preserved.' }
$reportPath = $reportDirectory[0].FullName
Write-Host 'Checking sampled values and quality after the timed observation.'
$dataCheck = Invoke-DataCheck 'verify' $reference
$reference = $null
[IO.File]::WriteAllText((Join-Path $reportPath 'data-check.json'), ($dataCheck | ConvertTo-Json -Depth 15), $utf8)
$profile = [ordered]@{ consumer_workers = $workers; batch_count = $batch; max_buffer_rows = (Writer-Int $text 'max_buffer_rows' 2000);
    settings_sha256 = (Get-FileHash -LiteralPath $settings -Algorithm SHA256).Hash; captured_at = [DateTimeOffset]::UtcNow.ToString('o') }
$summary = Get-Content -LiteralPath (Join-Path $reportPath 'summary.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$writerLogs = @($summary.logs | Where-Object { $_.module -eq 'writer' -and $_.fresh })
$profile.effective_consumer_workers = if ($writerLogs.Count -eq 1) { $writerLogs[0].writer_timing.worker_count } else { $null }
if ($profile.effective_consumer_workers -ne $workers) { Write-Warning 'Effective Worker count differs or is unknown. Do not treat this round as the requested Worker configuration.' }
$profile.writer_processes = @(foreach ($writerProcess in Get-Process -Name 'data-watch-writer' -ErrorAction SilentlyContinue) {
    $entry = [ordered]@{ pid = $writerProcess.Id; executable_sha256 = $null; source_revision = $null; native_sha256 = $null }
    try {
        $executable = $writerProcess.Path
        if ($executable) {
            $entry.executable_sha256 = (Get-FileHash -LiteralPath $executable -Algorithm SHA256).Hash
            $installedRoot = Split-Path -Parent $executable
            $installedRelease = Join-Path $installedRoot 'writer-release.json'
            if (Test-Path -LiteralPath $installedRelease) {
                $entry.source_revision = (Get-Content -LiteralPath $installedRelease -Raw -Encoding UTF8 | ConvertFrom-Json).source_revision
            }
            $entry.native_sha256 = (Get-FileHash -LiteralPath (Join-Path $installedRoot '_internal\taosws\_taosws.pyd') -Algorithm SHA256).Hash
        }
    } catch { $entry.version_check = 'unavailable' }
    [pscustomobject]$entry
})
[IO.File]::WriteAllText((Join-Path $reportPath 'test-profile.json'), ($profile | ConvertTo-Json -Depth 8), $utf8)
Write-Operation ('capture completed; results=' + $runRoot + '; data_check=' + $dataCheck.status)
Write-Host "Data spot-check status: $($dataCheck.status). This does not prove all historical data or exactly-once writes."
Write-Host "Send the complete results folder: $runRoot"
