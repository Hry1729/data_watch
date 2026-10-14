[CmdletBinding()]
param([string]$OutputRoot = '')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$revision = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the committed source revision.' }
$inputs = @('writer', 'scripts/pipeline_diagnostics.py', 'scripts/diagnose_pipeline.ps1',
    'scripts/writer_data_check.py', 'scripts/writer_test.ps1', 'scripts/update_writer.ps1',
    'scripts/package_writer_target.ps1', 'vendor/taos-ws-py', 'vendor/patches',
    'docs/04-运维部署/Writer目标机验证.md')
& git -C $repoRoot diff --quiet HEAD -- @inputs
if ($LASTEXITCODE -ne 0) { throw 'Commit the release inputs before packaging.' }
$untracked = @(& git -C $repoRoot ls-files --others --exclude-standard -- @inputs)
if ($untracked.Count) { throw 'Release inputs contain untracked files; commit them before packaging.' }
if (-not $OutputRoot) { $OutputRoot = Join-Path $repoRoot 'artifacts\writer-release' }
$releaseRoot = Join-Path ([IO.Path]::GetFullPath($OutputRoot)) ('writer-target-' + $revision.Substring(0, 8))
if (Test-Path -LiteralPath $releaseRoot) { throw 'Release directory exists. Use a new OutputRoot.' }
$python = Join-Path $repoRoot 'writer\.venv-pack\Scripts\python.exe'
$wheel = Join-Path $repoRoot 'installer\.build\wheels\taos_ws_py-0.7.0-cp37-abi3-win_amd64.whl'
if (-not (Test-Path -LiteralPath $python) -or -not (Test-Path -LiteralPath $wheel)) { throw 'Writer build environment or patched wheel is missing.' }
$probe = @'
import hashlib,importlib.metadata as metadata,json,pathlib,sys,sysconfig,zipfile
import taosws._taosws as native
wheel=pathlib.Path(sys.argv[1])
with zipfile.ZipFile(wheel) as archive:
    entry=next(name for name in archive.namelist() if name.endswith('/_taosws.pyd'))
    wheel_native=hashlib.sha256(archive.read(entry)).hexdigest()
print(json.dumps({'python':sys.version.split()[0],
 'gil_enabled':getattr(sys,'_is_gil_enabled',lambda:None)(),
 'free_threaded_build':bool(sysconfig.get_config_var('Py_GIL_DISABLED')),
 'versions':{name:metadata.version(name) for name in ('taos-ws-py','redis','pymysql','pyinstaller')},
 'wheel_sha256':hashlib.sha256(wheel.read_bytes()).hexdigest(),
 'wheel_native_sha256':wheel_native,
 'installed_native_sha256':hashlib.sha256(pathlib.Path(native.__file__).read_bytes()).hexdigest()}))
'@
$runtimeJson = $probe | & $python -B - $wheel
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect Writer dependencies.' }
$runtime = $runtimeJson | ConvertFrom-Json
if ($runtime.installed_native_sha256 -ne $runtime.wheel_native_sha256) { throw 'Installed connector does not match the patched wheel.' }
if ($runtime.free_threaded_build) { throw 'This release requires the current ordinary Python ABI environment.' }
New-Item -ItemType Directory -Path $releaseRoot | Out-Null
$payloadRoot = Join-Path $releaseRoot 'payload'
$distRoot = Join-Path $releaseRoot 'build-dist'
$workRoot = Join-Path $repoRoot ('installer\.build\writer-field-' + $revision.Substring(0, 8))
Push-Location (Join-Path $repoRoot 'writer')
try {
    # Windows PowerShell 5.1 wraps native stderr INFO lines as error records.
    # Keep their full log, but decide build success using the native exit code.
    $savedErrorAction = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & $python -B -m PyInstaller 'data-watch-writer.spec' --noconfirm --clean --distpath $distRoot --workpath $workRoot *> (Join-Path $releaseRoot 'build.log')
        $buildExit = $LASTEXITCODE
    } finally { $ErrorActionPreference = $savedErrorAction }
    if ($buildExit -ne 0) { throw 'Writer build failed. See build.log.' }
} finally { Pop-Location }
New-Item -ItemType Directory -Path $payloadRoot | Out-Null
Move-Item -LiteralPath (Join-Path $distRoot 'data-watch-writer') -Destination (Join-Path $payloadRoot 'writer')
$packedNative = Join-Path $payloadRoot 'writer\_internal\taosws\_taosws.pyd'
if ((Get-FileHash -LiteralPath $packedNative -Algorithm SHA256).Hash.ToLowerInvariant() -ne $runtime.installed_native_sha256) {
    throw 'Packaged native connector differs from the verified input.'
}
$utf8 = New-Object Text.UTF8Encoding($false)
$sourceFiles = @('writer/main.py', 'writer/consumer.py', 'writer/performance.py', 'writer/tdengine_writer.py',
    'scripts/pipeline_diagnostics.py', 'scripts/writer_data_check.py',
    'vendor/patches/0001-release-gil-around-blocking-calls.patch', 'vendor/taos-ws-py/Cargo.lock')
$sourceHashes = [ordered]@{}
foreach ($path in $sourceFiles) { $sourceHashes[$path] = (Get-FileHash -LiteralPath (Join-Path $repoRoot $path) -Algorithm SHA256).Hash.ToLowerInvariant() }
$buildInfo = [ordered]@{ source_revision = $revision; built_at_utc = [DateTimeOffset]::UtcNow.ToString('o');
    runtime = $runtime; source_sha256 = $sourceHashes; performance_validation = 'pending_target_machine' }
[IO.File]::WriteAllText((Join-Path $payloadRoot 'writer\writer-release.json'), ($buildInfo | ConvertTo-Json -Depth 8), $utf8)
foreach ($name in @('update_writer.ps1', 'writer_test.ps1', 'diagnose_pipeline.ps1', 'pipeline_diagnostics.py', 'writer_data_check.py')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot ('scripts\' + $name)) -Destination $releaseRoot
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'docs\04-运维部署\Writer目标机验证.md') -Destination (Join-Path $releaseRoot 'README.md')
$files = @(Get-ChildItem -LiteralPath $releaseRoot -File -Recurse | Where-Object { $_.Name -ne 'build.log' } | ForEach-Object {
    [ordered]@{ path = $_.FullName.Substring($releaseRoot.Length + 1).Replace('\', '/'); bytes = $_.Length;
        sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
})
$manifest = [ordered]@{ source_revision = $revision; baseline_revision = '88339ca6';
    created_at_utc = [DateTimeOffset]::UtcNow.ToString('o'); files = $files; runtime = $runtime;
    validation = 'source/static/offline checks only; throughput and data acceptance pending target execution' }
[IO.File]::WriteAllText((Join-Path $releaseRoot 'release-manifest.json'), ($manifest | ConvertTo-Json -Depth 8), $utf8)
$zip = $releaseRoot + '.zip'
$deliveryItems = @(Get-ChildItem -LiteralPath $releaseRoot | Where-Object { $_.Name -notin @('build.log', 'build-dist') } | ForEach-Object { $_.FullName })
Compress-Archive -LiteralPath $deliveryItems -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zip + '.sha256'), ($hash + '  ' + (Split-Path -Leaf $zip) + "`n"), $utf8)
Write-Host "Release revision: $revision"
Write-Host "Delivery: $zip"
Write-Host "SHA256: $hash"
