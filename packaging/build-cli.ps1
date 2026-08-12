[CmdletBinding()]
param(
    [string]$OutputDirectory = "artifacts",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$outputRoot = Join-Path $root $OutputDirectory
$workRoot = Join-Path $root "build\cli"
$stageRoot = Join-Path $outputRoot "staging"

Push-Location $root
try {
    if (-not $SkipTests) {
        python -m pytest cli\tests -q
        if ($LASTEXITCODE -ne 0) { throw "CLI tests failed" }
    }

    $version = python -c "from cli.version import CLI_VERSION; print(CLI_VERSION)"
    if ($LASTEXITCODE -ne 0 -or -not $version) { throw "Cannot resolve CLI version" }
    $packageName = "data-watch-cli-$version-windows-x64"
    $packageRoot = Join-Path $outputRoot $packageName

    python -m PyInstaller --noconfirm --clean `
        --distpath $stageRoot `
        --workpath $workRoot `
        (Join-Path $root "packaging\data-watch-cli.spec")
    if ($LASTEXITCODE -ne 0) { throw "PyInstaller build failed" }

    if (Test-Path $packageRoot) {
        Remove-Item -LiteralPath $packageRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $packageRoot | Out-Null
    Copy-Item -LiteralPath (Join-Path $stageRoot "data-watch") `
        -Destination (Join-Path $packageRoot "app") -Recurse
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "install.ps1") -Destination $packageRoot
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "uninstall.ps1") -Destination $packageRoot
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot "smoke-test.ps1") -Destination $packageRoot

    $archivePath = "$packageRoot.zip"
    if (Test-Path $archivePath) { Remove-Item -LiteralPath $archivePath -Force }
    Compress-Archive -Path (Join-Path $packageRoot "*") -DestinationPath $archivePath

    $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    [ordered]@{
        version = $version
        platform = "windows-x64"
        archive = (Split-Path $archivePath -Leaf)
        sha256 = $hash
    } | ConvertTo-Json | Set-Content -LiteralPath "$archivePath.sha256.json" -Encoding utf8

    Write-Host "Built: $archivePath"
    Write-Host "SHA256: $hash"
}
finally {
    Pop-Location
}
