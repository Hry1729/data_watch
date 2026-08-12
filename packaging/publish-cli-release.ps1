[CmdletBinding()]
param(
    [string]$Channel = "stable",
    [string]$Platform = "windows-x64",
    [string]$PrivateKey = "",
    [switch]$SkipBuild,
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not $PrivateKey) { $PrivateKey = Join-Path $PSScriptRoot "keys\cli-release-private.key" }
Push-Location $root
try {
    if (-not (Test-Path $PrivateKey)) {
        throw "Release private key not found: $PrivateKey"
    }
    $version = python -c "from cli.version import CLI_VERSION; print(CLI_VERSION)"
    $archive = Join-Path $root "artifacts\data-watch-cli-$version-windows-x64.zip"
    if (-not $SkipBuild) {
        & (Join-Path $PSScriptRoot "build-cli.ps1") -SkipTests:$SkipTests
    }
    if (-not (Test-Path $archive)) { throw "CLI archive not found: $archive" }

    python packaging\release-tool.py publish `
        --archive $archive `
        --private-key $PrivateKey `
        --release-root backend\cli-releases `
        --version $version `
        --channel $Channel `
        --platform $Platform
    if ($LASTEXITCODE -ne 0) { throw "CLI release publication failed" }
    Write-Host "Published DataWatch CLI $version to Backend release storage."
}
finally {
    Pop-Location
}
