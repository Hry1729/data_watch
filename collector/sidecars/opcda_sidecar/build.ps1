$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $root "bin"
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"

if (-not (Test-Path $csc)) {
    throw "32-bit .NET Framework C# compiler not found: $csc"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$outExe = Join-Path $outDir "OpcDaProbe.exe"
$src = Join-Path $root "OpcDaProbe.cs"

& $csc `
    /nologo `
    /platform:x86 `
    /target:exe `
    "/out:$outExe" `
    "$src"

if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

Write-Host "Built:" $outExe
