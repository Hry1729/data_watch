# Run on the target in an elevated Windows PowerShell after stopping Writer in the app.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$WriterDirectory,
    [string]$RollbackBackup = ''
)
$ErrorActionPreference = 'Stop'
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
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
    if (Test-Path -LiteralPath $target) { Move-Item -LiteralPath $target -Destination $saved }
    try { Move-Item -LiteralPath $backup -Destination $target }
    catch {
        if (-not (Test-Path -LiteralPath $target) -and (Test-Path -LiteralPath $saved)) {
            Move-Item -LiteralPath $saved -Destination $target
        }
        throw
    }
    Write-Host "Writer restored. Replaced version retained at: $saved"
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
Move-Item -LiteralPath $target -Destination $backup
try { Move-Item -LiteralPath $stage -Destination $target }
catch {
    if (-not (Test-Path -LiteralPath $target)) { Move-Item -LiteralPath $backup -Destination $target }
    throw
}
Write-Host "Installed source revision: $($manifest.source_revision)"
Write-Host "Previous Writer retained at: $backup"
Write-Host 'Start Writer from the app. Backend, Collector and database images do not need replacement.'
