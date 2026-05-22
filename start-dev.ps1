param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendDir = Join-Path $repoRoot "backend"
$frontendDir = Join-Path $repoRoot "frontend"

function Resolve-ExistingPath {
    param(
        [string[]]$Candidates
    )

    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }
        if (Test-Path -LiteralPath $candidate) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }

    return $null
}

function Resolve-CommandPath {
    param(
        [string[]]$Candidates
    )

    foreach ($candidate in $Candidates) {
        $command = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($null -ne $command) {
            return $command.Source
        }
    }

    return $null
}

function Test-PortOccupied {
    param(
        [int]$Port
    )

    try {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
        $listener.Start()
        $listener.Stop()
        return $false
    } catch {
        return $true
    }
}

function New-EncodedCommand {
    param(
        [string]$ScriptText
    )

    return [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($ScriptText))
}

$powershellExe = Join-Path $env:SystemRoot "System32\WindowsPowerShell\v1.0\powershell.exe"
$pythonExe = Resolve-ExistingPath @(
    "C:\Users\Hry\.local\bin\python3.14t.exe",
    (Join-Path $backendDir ".venv-314t\Scripts\python.exe"),
    "C:\Users\Hry\AppData\Local\Python\pythoncore-3.14-64\python.exe"
)

if (-not $pythonExe) {
    throw "Unable to locate a Python executable for the backend."
}

$frontendRunner = Resolve-CommandPath @("pnpm.cmd", "pnpm", "npm.cmd", "npm")

if (-not $frontendRunner) {
    throw "Unable to locate pnpm or npm for the frontend."
}

$backendPyPaths = @(
    (Join-Path $backendDir ".py314t-site"),
    "C:\Users\Hry\AppData\Local\Python\pythoncore-3.14-64\Lib\site-packages"
) | Where-Object { Test-Path -LiteralPath $_ }

$backendPyPathValue = [string]::Join(";", $backendPyPaths)
$backendTitle = "Device Monitor Backend"
$frontendTitle = "Device Monitor Frontend"

$backendScript = @"
`$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath '$backendDir'
`$Host.UI.RawUI.WindowTitle = '$backendTitle'
if ('$backendPyPathValue') {
    `$env:PYTHONPATH = '$backendPyPathValue'
}
`$env:PYTHONUTF8 = '1'
`$env:PYTHONIOENCODING = 'utf-8'
& '$pythonExe' -m uvicorn main:app --host 127.0.0.1 --port 8000 --reload
"@

if ([IO.Path]::GetFileName($frontendRunner).ToLower().StartsWith("pnpm")) {
    $frontendLaunch = "& '$frontendRunner' dev"
} else {
    $frontendLaunch = "& '$frontendRunner' run dev"
}

$frontendScript = @"
`$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath '$frontendDir'
`$Host.UI.RawUI.WindowTitle = '$frontendTitle'
$frontendLaunch
"@

$started = @()
$skipped = @()

if ($Force -or -not (Test-PortOccupied -Port 8000)) {
    Start-Process -FilePath $powershellExe -WorkingDirectory $backendDir -ArgumentList @(
        "-NoExit",
        "-ExecutionPolicy",
        "Bypass",
        "-EncodedCommand",
        (New-EncodedCommand -ScriptText $backendScript)
    ) | Out-Null
    $started += "backend: http://127.0.0.1:8000"
} else {
    $skipped += "backend skipped because port 8000 is already occupied"
}

if ($Force -or -not (Test-PortOccupied -Port 5173)) {
    Start-Process -FilePath $powershellExe -WorkingDirectory $frontendDir -ArgumentList @(
        "-NoExit",
        "-ExecutionPolicy",
        "Bypass",
        "-EncodedCommand",
        (New-EncodedCommand -ScriptText $frontendScript)
    ) | Out-Null
    $started += "frontend: http://127.0.0.1:5173"
} else {
    $skipped += "frontend skipped because port 5173 is already occupied"
}

if ($started.Count -gt 0) {
    Write-Host "Started:"
    $started | ForEach-Object { Write-Host "  - $_" }
}

if ($skipped.Count -gt 0) {
    Write-Host "Skipped:"
    $skipped | ForEach-Object { Write-Host "  - $_" }
    Write-Host "Force start commands:"
    Write-Host "  - CMD: .\\start-dev.cmd -Force"
    Write-Host "  - PowerShell: .\\start-dev.ps1 -Force"
}
