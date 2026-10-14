# Run on the target in an elevated Windows PowerShell after stopping Writer in the app.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WriterDirectory,
    [string]$RollbackBackup = ''
)
$ErrorActionPreference = 'Stop'
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$logDirectory = Join-Path $scriptDirectory 'operation-logs'
New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
$operationLog = Join-Path $logDirectory ('update-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6) + '.log')
function Write-Operation([string]$Message) {
    [IO.File]::AppendAllText($operationLog, ([DateTimeOffset]::Now.ToString('o') + ' ' + $Message + "`r`n"), (New-Object Text.UTF8Encoding($false)))
}
trap {
    Write-Operation ('failed: ' + $_.Exception.GetBaseException().Message)
    Write-Host "Operation failed. Log: $operationLog"
    throw $_
}
Write-Operation ('started; PowerShell=' + $PSVersionTable.PSVersion + '; writer=' + $WriterDirectory)
Write-Host "Operation log: $operationLog"
$target = [IO.Path]::GetFullPath($WriterDirectory).TrimEnd('\')
$parent = Split-Path -Parent $target
$leaf = Split-Path -Leaf $target
if (-not $parent -or -not (Test-Path -LiteralPath $parent -PathType Container)) { throw 'Invalid Writer parent directory.' }
function Assert-Sibling([string]$Path) {
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ((Split-Path -Parent $full) -ne $parent) { throw 'Move must stay inside the selected Writer parent directory.' }
    if ((Test-Path -LiteralPath $full) -and ((Get-Item -LiteralPath $full).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Reparse-point directories are not supported.'
    }
}
function Assert-WriterStopped {
    foreach ($process in @(Get-Process -Name 'data-watch-writer' -ErrorAction SilentlyContinue)) {
        try { $path = $process.Path } catch { throw 'Cannot inspect Writer. Use an elevated PowerShell.' }
        if (-not $path) { throw 'Cannot inspect Writer. Use an elevated PowerShell.' }
        if ([IO.Path]::GetFullPath($path) -eq (Join-Path $target 'data-watch-writer.exe')) {
            throw 'Stop Writer from the app first; the Agent must record that Writer should remain stopped.'
        }
    }
}
function Move-WriterDirectory([string]$From, [string]$To) {
    Assert-Sibling $From
    Assert-Sibling $To
    Write-Operation "rename: $From -> $To"
    try {
        # Both paths are siblings: rename exactly, never nest into an existing directory.
        [IO.Directory]::Move($From, $To)
    } catch {
        $failure = $_.Exception.GetBaseException()
        Write-Operation ('rename failed; hresult=' + $failure.HResult + '; ' + $failure.Message)
        foreach ($path in @($From, $To, $parent)) {
            if (Test-Path -LiteralPath $path) {
                try {
                    $item = Get-Item -LiteralPath $path -Force
                    $acl = Get-Acl -LiteralPath $path
                    Write-Operation ('path=' + $path + '; attributes=' + $item.Attributes + '; owner=' + $acl.Owner + '; acl=' + $acl.Sddl)
                } catch { Write-Operation ('path inspection failed: ' + $path) }
            }
        }
        throw
    }
}
Assert-Sibling $target
Assert-WriterStopped
$suffix = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6)
if ($RollbackBackup) {
    $backup = [IO.Path]::GetFullPath($RollbackBackup).TrimEnd('\')
    Assert-Sibling $backup
    if ($backup -eq $target -or -not (Test-Path -LiteralPath (Join-Path $backup 'data-watch-writer.exe'))) {
        throw 'Select the previous Writer backup directory.'
    }
    $saved = Join-Path $parent ($leaf + '.before-rollback-' + $suffix)
    Assert-Sibling $saved
    if (Test-Path -LiteralPath $target) { Move-WriterDirectory $target $saved }
    try { Move-WriterDirectory $backup $target }
    catch {
        if (-not (Test-Path -LiteralPath $target) -and (Test-Path -LiteralPath $saved)) {
            Move-WriterDirectory $saved $target
            Write-Operation 'Rollback failed; original installed directory restored.'
        }
        throw
    }
    Write-Host "Writer restored. Replaced version retained at: $saved"
    Write-Operation 'rollback completed'
    Write-Host 'Start Writer from the app after restoring its test parameters.'
    return
}
if (-not (Test-Path -LiteralPath (Join-Path $target 'data-watch-writer.exe'))) { throw 'Writer executable not found in the selected directory.' }
$manifest = Get-Content -LiteralPath (Join-Path $scriptDirectory 'release-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$source = Join-Path $scriptDirectory 'payload\writer'
$files = @($manifest.files | Where-Object { $_.path.StartsWith('payload/writer/') })
if (-not $files.Count) { throw 'Writer payload manifest is empty.' }
foreach ($file in $files) {
    $path = [IO.Path]::GetFullPath((Join-Path $scriptDirectory $file.path))
    if (-not $path.StartsWith($source + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid payload path.' }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
        throw "Payload checksum mismatch: $($file.path)"
    }
}
$stage = Join-Path $parent ($leaf + '.stage-' + $suffix)
$backup = Join-Path $parent ($leaf + '.backup-' + $suffix)
Assert-Sibling $stage
Assert-Sibling $backup
New-Item -ItemType Directory -Path $stage | Out-Null
foreach ($item in Get-ChildItem -LiteralPath $source -Force) { Copy-Item -LiteralPath $item.FullName -Destination $stage -Recurse -Force }
# Keep any installation-local files; the usual settings live in ProgramData separately.
foreach ($item in Get-ChildItem -LiteralPath $target -Force) {
    if ($item.Name -notin @('_internal', 'data-watch-writer.exe', 'writer-release.json')) {
        Copy-Item -LiteralPath $item.FullName -Destination $stage -Recurse -Force
    }
}
Assert-WriterStopped
$ready = $stage + '.ready'
Assert-Sibling $ready
# Exercise the populated staging directory before moving the working installation.
Move-WriterDirectory $stage $ready
$stage = $ready
Assert-WriterStopped
Move-WriterDirectory $target $backup
try { Move-WriterDirectory $stage $target }
catch {
    if (-not (Test-Path -LiteralPath $target)) {
        Move-WriterDirectory $backup $target
        Write-Operation 'Update failed; original Writer restored.'
        Write-Host 'Update failed. The original Writer directory has been restored.'
    }
    throw
}
Write-Operation ('update completed; source_revision=' + $manifest.source_revision + '; backup=' + $backup)
Write-Host "Installed source revision: $($manifest.source_revision)"
Write-Host "Previous Writer retained at: $backup"
Write-Host 'Start Writer from the app. Backend, Collector and database images do not need replacement.'
