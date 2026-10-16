<#
.SYNOPSIS
    Build the patched taos-ws-py wheel the writer ships with.

.DESCRIPTION
    taos-ws-py 0.7.0 never releases the GIL around its blocking TDengine calls:
    Connection::execute / query / the module-level connect run a synchronous
    WebSocket round trip with the interpreter lock held. The writer's consumers
    share one asyncio event loop, so each native call freezes the whole process
    -- a stalled connect froze it for the full 14 s measured by
    scripts/validate_taosws_gil_release.py.

    vendor/taos-ws-py holds the upstream source of the exact connector version
    that is installed (see vendor/taos-ws-py/PROVENANCE.md) and
    vendor/patches holds the delta. This script copies the pristine source to a
    scratch directory, applies the patch there, and builds with the upstream
    Cargo.lock, so the wheel is reproducible from this repository alone.

    It never writes into writer/.venv-pack; installer/build-roles.ps1 installs
    the wheel this produces.
#>
[CmdletBinding()]
param(
    [string]$OutputDirectory,
    [string]$Python
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Split-Path -Parent $PSScriptRoot
$SourceDir = Join-Path $RepoRoot 'vendor\taos-ws-py'
$PatchFile = Join-Path $RepoRoot 'vendor\patches\0001-release-gil-around-blocking-calls.patch'
$BuildRoot = Join-Path $PSScriptRoot '.build'
$ScratchDir = Join-Path $BuildRoot 'taos-ws-py-build'
$BuildEnvDir = Join-Path $BuildRoot 'taosws-build'
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $BuildRoot 'wheels' }
# Resolve now: a relative path would otherwise be reinterpreted against the
# scratch copy while maturin runs, and the wheel would land somewhere else.
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
# Any CPython >= 3.7 works: the wheel is abi3 (cp37-abi3-win_amd64). Prefer the
# interpreter the writer is packaged with; the fallback keeps this script usable
# on a host that only has the repo's build interpreter.
if (-not $Python) {
    foreach ($candidate in @(
        (Join-Path $RepoRoot 'writer\.venv-pack\Scripts\python.exe'),
        'D:\Program Files\python\python3.14.6\python.exe'
    )) {
        if (Test-Path -LiteralPath $candidate) { $Python = $candidate; break }
    }
}

foreach ($required in @($SourceDir, $PatchFile, $Python)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Missing build input: $required" }
}

Write-Host '[taos-ws-py] Preparing build environment' -ForegroundColor Cyan
$buildPython = Join-Path $BuildEnvDir 'Scripts\python.exe'
$maturin = Join-Path $BuildEnvDir 'Scripts\maturin.exe'
$cmake = Join-Path $BuildEnvDir 'Scripts\cmake.exe'
if (-not (Test-Path -LiteralPath $maturin) -or -not (Test-Path -LiteralPath $cmake)) {
    & $Python -m venv $BuildEnvDir
    if ($LASTEXITCODE -ne 0) { throw 'Failed to create the build virtual environment.' }
    & $buildPython -m pip install --disable-pip-version-check maturin cmake
    if ($LASTEXITCODE -ne 0) { throw 'Failed to install maturin/cmake.' }
}

Write-Host "[taos-ws-py] Applying $(Split-Path $PatchFile -Leaf)" -ForegroundColor Cyan
if (Test-Path -LiteralPath $ScratchDir) { Remove-Item -Recurse -Force $ScratchDir }
Copy-Item -Recurse -Force $SourceDir $ScratchDir
# git apply needs a repository to resolve the patch paths against; the scratch
# copy gets a throwaway one, committed at the pristine state so the patch lands
# on exactly the revision it was generated from.
& git -C $ScratchDir init --quiet
& git -C $ScratchDir add -A
& git -C $ScratchDir -c user.email=build@localhost -c user.name=build `
    commit --quiet -m 'vendored taos-ws-py, pristine'
if ($LASTEXITCODE -ne 0) { throw 'Failed to stage the scratch source.' }
& git -C $ScratchDir apply $PatchFile
if ($LASTEXITCODE -ne 0) { throw "Patch did not apply cleanly: $PatchFile" }

Write-Host '[taos-ws-py] Building wheel' -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
# Drop earlier connector wheels first: a stale one with a newer timestamp would
# otherwise be the one picked up below and installed over the fresh build.
Get-ChildItem -LiteralPath $OutputDirectory -Filter 'taos_ws_py-*.whl' -File |
    Remove-Item -Force
# cmake.exe comes from the build venv, so the host does not need it on PATH.
# aws-lc-sys otherwise wants NASM on Windows; its prebuilt objects avoid that.
# The cargo target directory lives outside the scratch copy so repeated builds
# (every role package build) reuse the compiled dependency tree.
$savedCmake = $env:CMAKE
$savedNasm = $env:AWS_LC_SYS_PREBUILT_NASM
$savedTarget = $env:CARGO_TARGET_DIR
$env:CMAKE = $cmake
$env:AWS_LC_SYS_PREBUILT_NASM = '1'
$env:CARGO_TARGET_DIR = Join-Path $BuildRoot 'taos-ws-py-target'
Push-Location $ScratchDir
try {
    & $maturin build --release --interpreter $Python --out $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'maturin build failed.' }
} finally {
    Pop-Location
    $env:CMAKE = $savedCmake
    $env:AWS_LC_SYS_PREBUILT_NASM = $savedNasm
    $env:CARGO_TARGET_DIR = $savedTarget
}

$wheels = @(Get-ChildItem -LiteralPath $OutputDirectory -Filter 'taos_ws_py-*.whl' -File)
if ($wheels.Count -ne 1) {
    throw "Expected exactly one connector wheel in $OutputDirectory, found $($wheels.Count)."
}
$wheel = $wheels[0]
$hash = (Get-FileHash -LiteralPath $wheel.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "[taos-ws-py] $($wheel.FullName)" -ForegroundColor Green
Write-Host "[taos-ws-py] sha256 $hash" -ForegroundColor Green
Write-Output $wheel.FullName
