param(
    [switch]$Force,
    # 显式指定后端解释器，优先于一切自动探测。也可用环境变量 DATAWATCH_PYTHON。
    [string]$PythonExe = $env:DATAWATCH_PYTHON
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

function Test-FreeThreadedPython {
    param(
        [string]$Exe
    )

    # 后端依赖 TurboAPI，它要求 free-threading(no-GIL) 构建。
    if (-not $Exe) {
        return $false
    }
    try {
        $probe = & $Exe -c "import sys; print('t' if hasattr(sys, '_is_gil_enabled') and not sys._is_gil_enabled() else 'g')" 2>$null
        return (([string]($probe | Select-Object -First 1)).Trim() -eq 't')
    } catch {
        return $false
    }
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

# 解释器解析顺序（不再绑定任何作者个人路径）：
#   1) -PythonExe / DATAWATCH_PYTHON 显式指定
#   2) 仓库内虚拟环境
#   3) py 启动器的 3.14t（由启动器报告实际解释器路径）
#   4) PATH 上的 python3.14t
#   5) 标准安装根下的 Python3* 目录（与会话用户名无关）
$pythonCandidates = @()
if ($PythonExe) {
    $pythonCandidates += $PythonExe
}
$pythonCandidates += (Join-Path $backendDir ".venv-314t\Scripts\python.exe")

$pyLauncher = Resolve-CommandPath @("py.exe", "py")
if ($pyLauncher) {
    $launched = & $pyLauncher -3.14t -c "import sys; print(sys.executable)" 2>$null
    if ($launched) {
        # 只取首行并去首尾空白：路径中间可能含空格（例如 "D:\Program Files\..."），不得整体删空白。
        $launchedPath = ([string]($launched | Select-Object -First 1)).Trim()
        if ($launchedPath) {
            $pythonCandidates += $launchedPath
        }
    }
}

$pythonCandidates += Resolve-CommandPath @("python3.14t.exe", "python3.14t")

foreach ($installRoot in @($env:ProgramFiles, ${env:ProgramFiles(x86)}, (Join-Path $env:LOCALAPPDATA "Programs\Python"))) {
    if (-not [string]::IsNullOrWhiteSpace($installRoot) -and (Test-Path -LiteralPath $installRoot)) {
        Get-ChildItem -LiteralPath $installRoot -Directory -Filter "Python3*" -ErrorAction SilentlyContinue |
            ForEach-Object { $pythonCandidates += (Join-Path $_.FullName "python3.14t.exe") }
    }
}

$pythonExe = Resolve-ExistingPath $pythonCandidates

if (-not $pythonExe) {
    throw "Unable to locate a Python executable for the backend. Pass -PythonExe <path> or set DATAWATCH_PYTHON. The backend requires a free-threading (3.14t/no-GIL) build."
}

if (-not (Test-FreeThreadedPython -Exe $pythonExe)) {
    Write-Warning "Selected interpreter '$pythonExe' is not a free-threading (no-GIL) build. The backend depends on TurboAPI which requires 3.14t; startup will likely fail. Pass -PythonExe <path to python3.14t.exe> explicitly."
} else {
    Write-Host "Backend interpreter: $pythonExe (free-threading)"
}

$frontendRunner = Resolve-CommandPath @("pnpm.cmd", "pnpm", "npm.cmd", "npm")

if (-not $frontendRunner) {
    throw "Unable to locate pnpm or npm for the frontend."
}

# 后端依赖目录：仓库内的 .py314t-site 是项目基础设施。可用 DATAWATCH_BACKEND_PYTHONPATH
# 追加额外目录（分号分隔），用于本机自建的 free-threading 依赖集，避免写死任何个人路径。
$backendPyPaths = @(
    (Join-Path $backendDir ".py314t-site")
)
if (-not [string]::IsNullOrWhiteSpace($env:DATAWATCH_BACKEND_PYTHONPATH)) {
    $backendPyPaths += ($env:DATAWATCH_BACKEND_PYTHONPATH -split ';')
}
$backendPyPaths = $backendPyPaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) }

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
