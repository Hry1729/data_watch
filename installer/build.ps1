# DataWatch 边缘机一体安装包 — 一键发版构建
# 依次：tauri build → PyInstaller(agent/collector/writer) → proxy build(csc/net48-x86) → ISCC
# 产出：installer\Output\DataWatch-Setup-<MyAppVersion>.exe
#
# 用法：
#   pwsh -File installer\build.ps1
#   pwsh -File installer\build.ps1 -Iscc "D:\ruanjian\Inno Setup 6\ISCC.exe"
#
# 前置（gitignore 外部件，需预先放好；详见 docs\运维\DataWatch目标机部署手册.md）：
#   installer\win64\nssm.exe
#   installer\webview2\MicrosoftEdgeWebview2Setup.exe
#   agent\.venv-pack / collector\.venv-pack / writer\.venv-pack（含 pyinstaller）
[CmdletBinding()]
param(
    # ISCC.exe 路径；留空则按注册表自动探测 Inno Setup 6
    [string]$Iscc = ""
)

$ErrorActionPreference = "Stop"

# 仓库根 = 本脚本所在 installer\ 的上级
$RepoRoot = Split-Path -Parent $PSScriptRoot

function Assert-LastExit([string]$What) {
    if ($LASTEXITCODE -ne 0) { throw "$What 失败 (exit $LASTEXITCODE)" }
}

function Resolve-Iscc([string]$Override) {
    if ($Override) {
        if (Test-Path $Override) { return $Override }
        throw "指定的 ISCC 不存在: $Override"
    }
    $keys = @(
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup*_is1',
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup*_is1',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup*_is1'
    )
    foreach ($k in $keys) {
        foreach ($p in (Get-ItemProperty $k -ErrorAction SilentlyContinue)) {
            if ($p.InstallLocation) {
                $exe = Join-Path $p.InstallLocation 'ISCC.exe'
                if (Test-Path $exe) { return $exe }
            }
        }
    }
    $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    throw "找不到 ISCC.exe，请安装 Inno Setup 6 或用 -Iscc 指定路径"
}

# ── 开工前检查：ISCC + gitignore 外部静态件 ──
Write-Host "=== [0/6] 开工前检查 ===" -ForegroundColor Cyan
$isccExe = Resolve-Iscc $Iscc
Write-Host "ISCC: $isccExe"

$preflight = @(
    'installer\win64\nssm.exe',
    'installer\webview2\MicrosoftEdgeWebview2Setup.exe',
    'agent\config.toml.example',
    'installer\templates\collector-settings.toml.example',
    'installer\templates\writer-settings.toml.example'
)
foreach ($rel in $preflight) {
    if (-not (Test-Path (Join-Path $RepoRoot $rel))) {
        throw "缺少安装器静态依赖: $rel（见部署手册 §四）"
    }
}

# ── [1/6] app: tauri build（beforeBuildCommand 自动跑 pnpm build）──
Write-Host "`n=== [1/6] app: tauri build ===" -ForegroundColor Cyan
Push-Location (Join-Path $RepoRoot 'app')
try {
    pnpm install --frozen-lockfile; Assert-LastExit 'pnpm install'
    pnpm tauri build;               Assert-LastExit 'pnpm tauri build'
} finally { Pop-Location }

# ── [2-4/6] agent / collector / writer: PyInstaller ──
$pyTargets = @(
    @{ N = '2'; Dir = 'agent';     Spec = 'data-watch-agent.spec' },
    @{ N = '3'; Dir = 'collector'; Spec = 'data-watch-collector.spec' },
    @{ N = '4'; Dir = 'writer';    Spec = 'data-watch-writer.spec' }
)
foreach ($t in $pyTargets) {
    Write-Host "`n=== [$($t.N)/6] $($t.Dir): PyInstaller ===" -ForegroundColor Cyan
    $dir = Join-Path $RepoRoot $t.Dir
    $pyi = Join-Path $dir '.venv-pack\Scripts\pyinstaller.exe'
    if (-not (Test-Path $pyi)) { throw "缺少打包环境: $($t.Dir)\.venv-pack（见部署手册 §四）" }
    Push-Location $dir
    try {
        & $pyi $t.Spec --noconfirm; Assert-LastExit "pyinstaller $($t.Spec)"
    } finally { Pop-Location }
}

# ── [5/6] opcda proxy: build.ps1（依赖相对路径，须在 proxy 目录内运行）──
Write-Host "`n=== [5/6] opcda proxy: build.ps1 ===" -ForegroundColor Cyan
$proxyDir = Join-Path $RepoRoot 'collector\sidecars\opcda_proxy_service'
Push-Location $proxyDir
try {
    & (Join-Path $proxyDir 'build.ps1'); Assert-LastExit 'proxy build.ps1'
} finally { Pop-Location }
if (-not (Test-Path (Join-Path $proxyDir 'bin\OpcDaProxyService.exe'))) {
    throw 'proxy 构建未产出 bin\OpcDaProxyService.exe'
}

# ── [6/6] ISCC: 编译一体安装包 ──
Write-Host "`n=== [6/6] ISCC: DataWatch.iss ===" -ForegroundColor Cyan
$iss = Join-Path $RepoRoot 'installer\DataWatch.iss'
& $isccExe $iss; Assert-LastExit 'ISCC'

# 版本号取自 .iss，避免与 build 脚本漂移
$ver = (Select-String -Path $iss -Pattern '#define\s+MyAppVersion\s+"([^"]+)"').Matches[0].Groups[1].Value
$setup = Join-Path $RepoRoot ("installer\Output\DataWatch-Setup-{0}.exe" -f $ver)
if (-not (Test-Path $setup)) { throw "ISCC 未产出 $setup" }

Write-Host "`n=== DONE ===" -ForegroundColor Green
Get-Item $setup | Select-Object Name, @{N='SizeMB';E={[math]::Round($_.Length/1MB,1)}}, LastWriteTime | Format-List
