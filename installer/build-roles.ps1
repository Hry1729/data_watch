[CmdletBinding()]
param(
    [ValidateSet('all', 'centralized', 'edge', 'distributed-center')]
    [string]$Role = 'all',
    [string]$Iscc = ''
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ManifestPath = Join-Path $PSScriptRoot 'roles.json'
$Manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding utf8 | ConvertFrom-Json
$BuildSources = @('agent', 'app', 'collector', 'writer', 'edge', 'ingest', 'data_watch_shared', 'packages', 'installer', 'vendor', ':(top,glob)*.py')
$SourceRevision = (& git -C $RepoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source revision.' }
& git -C $RepoRoot diff --quiet HEAD -- @BuildSources
if ($LASTEXITCODE -ne 0) { throw 'Commit tracked build inputs before building release installers.' }
$RoleNames = if ($Role -eq 'all') {
    @('centralized', 'edge', 'distributed-center')
} else {
    @($Role)
}

$IssByRole = @{
    centralized = 'DataWatchCentralized.iss'
    edge = 'DataWatchEdge.iss'
    'distributed-center' = 'DataWatchDistributedCenter.iss'
}

function Assert-LastExit([string]$What) {
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit $LASTEXITCODE)" }
}

function Resolve-Iscc([string]$Override) {
    if ($Override) {
        if (Test-Path -LiteralPath $Override) { return (Resolve-Path $Override).Path }
        throw "ISCC not found: $Override"
    }
    $keys = @(
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup*_is1',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup*_is1',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup*_is1'
    )
    foreach ($key in $keys) {
        foreach ($item in (Get-ItemProperty $key -ErrorAction SilentlyContinue)) {
            if (-not $item.InstallLocation) { continue }
            $candidate = Join-Path $item.InstallLocation 'ISCC.exe'
            if (Test-Path -LiteralPath $candidate) { return $candidate }
        }
    }
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    throw 'ISCC.exe not found. Install Inno Setup 6 or pass -Iscc.'
}

function Require-Path([string]$RelativePath) {
    $path = Join-Path $RepoRoot $RelativePath
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing build dependency: $RelativePath" }
}

function Install-Requirements([string]$Python, [string]$Requirements) {
    & $Python -m pip install --disable-pip-version-check -r $Requirements
    Assert-LastExit "pip install $Requirements"
}

function Build-PythonTarget(
    [string]$Python,
    [string]$PyInstaller,
    [string]$WorkingDirectory,
    [string]$Spec
) {
    Push-Location $WorkingDirectory
    try {
        & $PyInstaller $Spec --noconfirm --clean
        Assert-LastExit "PyInstaller $Spec"
    } finally {
        Pop-Location
    }
}

function Build-Sidecar {
    $sidecarDir = Join-Path $RepoRoot 'collector\sidecars\opcda_proxy_service'
    Push-Location $sidecarDir
    try {
        & (Join-Path $sidecarDir 'build.ps1')
        Assert-LastExit 'OPC DA proxy build'
    } finally {
        Pop-Location
    }
}

Write-Host '=== DataWatch role package build ===' -ForegroundColor Cyan
$isccExe = Resolve-Iscc $Iscc
Require-Path 'installer\win64\nssm.exe'
Require-Path 'installer\webview2\MicrosoftEdgeWebview2Setup.exe'
Require-Path 'installer\templates\collector-settings.toml.example'
Require-Path 'installer\templates\writer-settings.toml.example'

$needsCentralized = $RoleNames -contains 'centralized'
$needsEdge = $RoleNames -contains 'edge'
$needsDistributedCenter = $RoleNames -contains 'distributed-center'

$agentPython = Join-Path $RepoRoot 'agent\.venv-pack\Scripts\python.exe'
$agentPyi = Join-Path $RepoRoot 'agent\.venv-pack\Scripts\pyinstaller.exe'
$collectorPython = Join-Path $RepoRoot 'collector\.venv-pack\Scripts\python.exe'
$collectorPyi = Join-Path $RepoRoot 'collector\.venv-pack\Scripts\pyinstaller.exe'
$writerPython = Join-Path $RepoRoot 'writer\.venv-pack\Scripts\python.exe'
$writerPyi = Join-Path $RepoRoot 'writer\.venv-pack\Scripts\pyinstaller.exe'
$requiredPackExecutables = @($agentPython, $agentPyi, $collectorPython, $collectorPyi)
if ($needsCentralized -or $needsDistributedCenter) {
    $requiredPackExecutables += @($writerPython, $writerPyi)
}
foreach ($path in $requiredPackExecutables) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing pack environment executable: $path" }
}

Write-Host '[python] Agent' -ForegroundColor Cyan
Install-Requirements $agentPython (Join-Path $RepoRoot 'agent\requirements.txt')
Build-PythonTarget $agentPython $agentPyi (Join-Path $RepoRoot 'agent') 'data-watch-agent.spec'

if ($needsCentralized) {
    Write-Host '[python] centralized Collector' -ForegroundColor Cyan
    Install-Requirements $collectorPython (Join-Path $RepoRoot 'collector\requirements.txt')
    Build-PythonTarget $collectorPython $collectorPyi (Join-Path $RepoRoot 'collector') 'data-watch-collector.spec'
}

if ($needsEdge) {
    Write-Host '[python] Edge Storage + Collector + Uploader' -ForegroundColor Cyan
    Install-Requirements $collectorPython (Join-Path $RepoRoot 'collector\requirements.txt')
    Install-Requirements $collectorPython (Join-Path $RepoRoot 'edge\requirements.txt')
    Build-PythonTarget $collectorPython $collectorPyi (Join-Path $RepoRoot 'edge') 'data-watch-edge-collector.spec'
    Build-PythonTarget $collectorPython $collectorPyi (Join-Path $RepoRoot 'edge') 'data-watch-uploader.spec'
    Build-PythonTarget $collectorPython $collectorPyi (Join-Path $RepoRoot 'edge') 'data-watch-edge-storage.spec'
}

if ($needsCentralized -or $needsDistributedCenter) {
    Write-Host '[python] Writer' -ForegroundColor Cyan
    Install-Requirements $writerPython (Join-Path $RepoRoot 'writer\requirements.txt')
    # The stock taos-ws-py never releases the GIL around its blocking TDengine
    # calls, so each native call stalls the writer's shared event loop. Build
    # the patched connector from vendor/ and install it over the stock wheel.
    # The build reports the path it produced; do not re-derive it here, or a
    # leftover wheel in that directory could be installed instead.
    Write-Host '[python] Writer patched taos-ws-py' -ForegroundColor Cyan
    $taosWheelDirectory = Join-Path $RepoRoot 'installer\.build\wheels'
    $taosWheelBuild = & (Join-Path $PSScriptRoot 'build-patched-taos-ws-py.ps1') `
        -OutputDirectory $taosWheelDirectory -Python $writerPython
    $taosWheel = @($taosWheelBuild)[-1]
    if (-not $taosWheel -or -not (Test-Path -LiteralPath $taosWheel)) {
        throw "The patched taos-ws-py wheel was not produced: $taosWheel"
    }
    & $writerPython -m pip install --disable-pip-version-check --force-reinstall --no-deps $taosWheel
    Assert-LastExit 'patched taos-ws-py install'
    Build-PythonTarget $writerPython $writerPyi (Join-Path $RepoRoot 'writer') 'data-watch-writer.spec'
}

if ($needsDistributedCenter) {
    Write-Host '[python] Ingest' -ForegroundColor Cyan
    Install-Requirements $collectorPython (Join-Path $RepoRoot 'ingest\requirements.txt')
    Build-PythonTarget $collectorPython $collectorPyi (Join-Path $RepoRoot 'ingest') 'data-watch-ingest.spec'
}

if ($needsCentralized -or $needsEdge) {
    Write-Host '[native] OPC DA proxy' -ForegroundColor Cyan
    Build-Sidecar
}

Push-Location (Join-Path $RepoRoot 'app')
try {
    pnpm install --frozen-lockfile
    Assert-LastExit 'pnpm install'
} finally {
    Pop-Location
}

$outputs = @()
$releaseRecords = @()
$priorRole = $env:VITE_DATAWATCH_ROLE
try {
foreach ($roleName in $RoleNames) {
    $roleConfig = $Manifest.roles.$roleName
    Write-Host "[app] $roleName" -ForegroundColor Cyan
    $tauriOverride = Join-Path $PSScriptRoot ".build\tauri-$roleName.json"
    New-Item -ItemType Directory -Path (Split-Path $tauriOverride) -Force | Out-Null
    @{
        productName = $roleConfig.displayName
        identifier = $roleConfig.appIdentifier
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $tauriOverride -Encoding utf8NoBOM

    Push-Location (Join-Path $RepoRoot 'app')
    try {
        $env:VITE_DATAWATCH_ROLE = $roleName
        pnpm tauri build --no-bundle --config $tauriOverride --ci
        Assert-LastExit "$roleName Tauri build"
    } finally {
        Pop-Location
    }

    $iss = Join-Path $PSScriptRoot $IssByRole[$roleName]
    & $isccExe $iss
    Assert-LastExit "$roleName Inno Setup build"
    $setup = Join-Path $PSScriptRoot ("Output\{0}-0.1.0.exe" -f $roleConfig.outputName)
    if (-not (Test-Path -LiteralPath $setup)) { throw "Expected setup was not produced: $setup" }
    $outputs += Get-Item -LiteralPath $setup
    $releaseRecords += [ordered]@{
        role = $roleName
        file = (Split-Path $setup -Leaf)
        sourceRevision = $SourceRevision
        sizeBytes = (Get-Item -LiteralPath $setup).Length
        sha256 = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
        appSha256 = (Get-FileHash -LiteralPath (Join-Path $RepoRoot 'app\src-tauri\target\release\app.exe')).Hash.ToLowerInvariant()
        agentSha256 = (Get-FileHash -LiteralPath (Join-Path $RepoRoot 'agent\dist\data-watch-agent\data-watch-agent.exe')).Hash.ToLowerInvariant()
    }
}
} finally {
    $env:VITE_DATAWATCH_ROLE = $priorRole
}

if ((& git -C $RepoRoot rev-parse HEAD).Trim() -ne $SourceRevision) { throw 'Source revision changed during the build.' }
& git -C $RepoRoot diff --quiet HEAD -- @BuildSources
if ($LASTEXITCODE -ne 0) { throw 'Tracked build inputs changed during the build.' }
foreach ($record in $releaseRecords) {
    $actual = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot "Output\$($record.file)")).Hash.ToLowerInvariant()
    if ($actual -ne $record.sha256) { throw "Installer changed after build: $($record.file)" }
}
$outputDir = Join-Path $PSScriptRoot 'Output'
$checksums = Get-ChildItem -LiteralPath $outputDir -Filter 'DataWatch-*-Setup-*.exe' -File | Sort-Object Name | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant(), $_.Name
}
[IO.File]::WriteAllLines((Join-Path $outputDir 'SHA256SUMS.txt'), [string[]]$checksums, [Text.Encoding]::ASCII)
@{ schemaVersion = 1; builtAt = [DateTime]::UtcNow.ToString('o'); installers = @($releaseRecords) } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $outputDir 'release-manifest.json') -Encoding utf8NoBOM

Write-Host '=== DONE ===' -ForegroundColor Green
$outputs | Select-Object Name, @{N='SizeMB'; E={[math]::Round($_.Length / 1MB, 1)}}, LastWriteTime | Format-Table -AutoSize


