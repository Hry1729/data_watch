<#
.SYNOPSIS
  Load center-stack images from offline tars and bring up the full center stack
  (redis + tdengine + backend + frontend) defined in docker-compose.yml.

.DESCRIPTION
  Idempotent bring-up helper for a center side where docker compose manages the
  WHOLE stack. Run after a fresh deploy or accidental container deletion.
    - runtime image tar files are loaded from ..\docker-images\. Backend and
      frontend are prebuilt release images; target machines do not rebuild them.
    - redis config is pinned via redis.conf (mounted by docker-compose.yml).
    - TDengine database/stables are initialized by the current Writer; device
      child tables are materialized as data arrives. Do NOT use the legacy
      scripts/init_tdengine_v2.py (its table naming/schema is obsolete).
      See section 6 of the deployment guide linked from docs/README.md for restore
      when no Collector/Writer is available.

  NOTE: use this ONLY when compose owns redis/tdengine too. If the site already
  has its own redis/tdengine, do NOT run this — start just the app services with
  `docker compose up -d` instead.

  Named volumes (redis_data / tdengine_data) persist across container recreation.
  Before any recovery/rebuild, preserve the old data and log volumes and record
  their names. A fresh volume does not recover history. This script does not
  initialize MySQL business tables or verify database query readiness.

.EXAMPLE
  powershell.exe -ExecutionPolicy Bypass -File .\stack-up.ps1
#>

# Stop on cmdlet errors. Native exit codes are checked manually below.
$ErrorActionPreference = 'Stop'
if (Get-Variable -Name PSNativeCommandUseErrorActionPreference -ErrorAction SilentlyContinue) {
    $PSNativeCommandUseErrorActionPreference = $false
}

$DeployDir    = $PSScriptRoot
$ComposeFile  = Join-Path $DeployDir 'docker-compose.yml'
$AppImagesDir = Join-Path (Split-Path $DeployDir -Parent) 'docker-images' # preferred image tars
$BackendEnv   = Join-Path $DeployDir 'backend.env'

# Offline image archives expected in docker-images. File names do not include
# Docker tags such as ":latest".
$Images = @(
    @{ Name = 'redis';                 Tar = Join-Path $AppImagesDir 'redis.tar' },
    @{ Name = 'tdengine';              Tar = Join-Path $AppImagesDir 'tdengine.tar' },
    @{ Name = 'data-watch-backend';    Tar = Join-Path $AppImagesDir 'data-watch-backend.tar' },
    @{ Name = 'data-watch-frontend';   Tar = Join-Path $AppImagesDir 'data-watch-frontend.tar' }
)

Write-Host '== DataWatch center stack bring-up =='
Write-Host "compose: $ComposeFile"

if (-not (Test-Path $ComposeFile)) { throw "compose file not found: $ComposeFile" }

# backend reads its config via env_file; compose errors out without it.
if (-not (Test-Path $BackendEnv)) {
    throw "backend.env not found: $BackendEnv  (copy backend.env.example -> backend.env and fill it; see deploy doc '配置 backend.env')"
}

# Verify Docker engine is reachable.
docker info *> $null
if ($LASTEXITCODE -ne 0) { throw 'Docker is not available/running. Start Docker Desktop first.' }

# Validate and load each offline image archive. The script intentionally does
# not inspect or enforce Docker tags such as redis:latest; it only verifies the
# expected archive files exist and lets docker load import the tags stored in
# those archives.
foreach ($img in $Images) {
    $tarPath = $img.Tar
    if (-not (Test-Path $tarPath)) {
        throw "image archive '$($img.Name)' not found: $tarPath"
    }
    Write-Host "[load] $tarPath"
    docker load -i $tarPath
    if ($LASTEXITCODE -ne 0) { throw "docker load failed for $tarPath" }
}

# Bring up the whole stack. --profile infra (no service list) starts the
# profiled services (redis/tdengine) together with the default ones
# (backend/frontend).
Write-Host '[up]   redis + tdengine + backend + frontend (profile: infra)'
docker compose -f $ComposeFile --profile infra up -d --pull never
if ($LASTEXITCODE -ne 0) { throw 'docker compose up failed' }

Write-Host ''
Write-Host '== status =='
docker compose -f $ComposeFile --profile infra ps
