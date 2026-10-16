[CmdletBinding()]
param(
    [ValidateSet('menu', 'check', 'baseline', 'workers8', 'batch400', 'capture', 'restore')][string]$Action = 'menu',
    [string]$SettingsPath = 'C:\ProgramData\DataWatch\writer\config\settings.toml',
    [string]$OutputRoot = 'D:\DataWatchWriterTests'
)
$ErrorActionPreference = 'Stop'
$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot).TrimEnd('\')
if ([IO.Path]::GetPathRoot($OutputRoot) -ne 'D:\') { throw '日志和报告目录必须位于 D 盘。' }
$logDirectory = Join-Path $OutputRoot 'operation-logs'
$launcherLog = $null
function Write-LauncherLog([string]$Message) {
    [IO.File]::AppendAllText($launcherLog, ([DateTimeOffset]::Now.ToString('o') + ' ' + $Message + "`r`n"), (New-Object Text.UTF8Encoding($false)))
}
function Quote-NativeArgument([string]$Value) {
    return '"' + [regex]::Replace([regex]::Replace($Value, '(\\*)"', '$1$1\"'), '(\\+)$', '$1$1') + '"'
}
function Write-ConfigPermissions {
    $directory = Split-Path -Parent $SettingsPath
    foreach ($path in @($SettingsPath, $directory, (Split-Path -Parent $directory), (Split-Path -Parent (Split-Path -Parent $directory)))) {
        try {
            $item = Get-Item -LiteralPath $path -Force
            $acl = Get-Acl -LiteralPath $path
            Write-LauncherLog ('path=' + $path + '; attributes=' + $item.Attributes + '; owner=' + $acl.Owner + '; acl=' + $acl.Sddl)
        } catch { Write-LauncherLog ('无法读取权限：' + $path) }
    }
}
function Assert-ConfigWritable {
    if (-not (Test-Path -LiteralPath $SettingsPath -PathType Leaf)) { throw "找不到配置文件：$SettingsPath" }
    $directory = Split-Path -Parent $SettingsPath
    $probe = Join-Path $directory ('writer-test-permission-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    $created = $false
    try {
        $stream = [IO.File]::Open($probe, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $created = $true
        $stream.Dispose()
        $stream = [IO.File]::Open($SettingsPath, [IO.FileMode]::Open, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
        $stream.Dispose()
    } catch {
        $failure = $_.Exception.GetBaseException()
        Write-LauncherLog ('写权限检测失败；hresult=' + $failure.HResult + '; ' + $failure.Message)
        Write-ConfigPermissions
        throw '配置目录或文件不可写。未修改配置；详细权限已记录到 D 盘日志。'
    } finally { if ($created) { [IO.File]::Delete($probe) } }
    Write-LauncherLog '配置目录创建/删除临时文件和配置文件写访问检查通过；未改配置内容。'
}
function Invoke-WriterAction([string]$Selected) {
    Write-LauncherLog ('action=' + $Selected)
    if ($Selected -eq 'check') {
        Write-ConfigPermissions
        Assert-ConfigWritable
        Write-Host '权限检查通过，配置内容未改变。' -ForegroundColor Green
        return
    }
    if ($Selected -ne 'capture') {
        if (Get-Process -Name 'data-watch-writer' -ErrorAction SilentlyContinue) {
            throw '请先在 App 中停止 Writer，再选择参数设置或恢复。'
        }
        Assert-ConfigWritable
    } elseif (-not (Get-Process -Name 'data-watch-writer' -ErrorAction SilentlyContinue)) {
        throw 'Writer 尚未启动。请先在 App 中启动，等待约 60 秒，再选择采集报告。'
    }
    & (Join-Path $scriptDirectory 'writer_test.ps1') -Mode $Selected -SettingsPath $SettingsPath -OutputRoot $OutputRoot -DurationSeconds 300
    Write-Host '本步完成。' -ForegroundColor Green
    if ($Selected -ne 'capture') { Write-Host '请在 App 中启动 Writer，等待约 60 秒，再选择采集报告。' }
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
try {
    if (-not $isAdmin -and $Action -ne 'check') {
        Write-Host '正在请求管理员权限，请在 Windows 权限提示中选择“是”。'
        $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $MyInvocation.MyCommand.Path,
            '-Action', $Action, '-SettingsPath', $SettingsPath, '-OutputRoot', $OutputRoot)
        $quoted = ($arguments | ForEach-Object { Quote-NativeArgument $_ }) -join ' '
        Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe') -ArgumentList $quoted -Verb RunAs -WindowStyle Normal | Out-Null
        return
    }
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    $launcherLog = Join-Path $logDirectory ('launcher-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6) + '.log')
    Write-LauncherLog ('PowerShell=' + $PSVersionTable.PSVersion + '; user=' + $identity.Name + '; elevated=' + $isAdmin + '; settings=' + $SettingsPath)
    foreach ($name in @('writer_test.ps1', 'diagnose_pipeline.ps1', 'pipeline_diagnostics.py', 'writer_data_check.py')) {
        if (-not (Test-Path -LiteralPath (Join-Path $scriptDirectory $name) -PathType Leaf)) { throw "缺少配套文件：$name。请完整解压新包。" }
    }
    Write-Host "配置：$SettingsPath"
    Write-Host "管理员权限：$isAdmin；日志和报告：$OutputRoot"
    if ($Action -ne 'menu') { Invoke-WriterAction $Action; return }
    while ($true) {
        Write-Host ''
        Write-Host '1  设置基线：4 Worker / 每流 200 帧（先在 App 停止 Writer）'
        Write-Host '2  采集当前组报告：5 分钟（先在 App 启动 Writer，等 60 秒）'
        Write-Host '3  设置 8 Worker / 每流 200 帧（先停止 Writer）'
        Write-Host '4  设置 4 Worker / 每流 400 帧（先停止 Writer）'
        Write-Host '5  恢复测试前的原配置（先停止 Writer）'
        Write-Host '6  检查配置写权限；0  退出'
        $choice = Read-Host '输入数字并回车'
        if ($choice -eq '0') { return }
        $selected = @{ '1' = 'baseline'; '2' = 'capture'; '3' = 'workers8'; '4' = 'batch400'; '5' = 'restore'; '6' = 'check' }[$choice]
        if (-not $selected) { Write-Host '请输入 0 到 6。'; continue }
        try { Invoke-WriterAction $selected }
        catch {
            Write-LauncherLog ('失败：' + $_.Exception.GetBaseException().Message)
            Write-Host ('本步失败：' + $_.Exception.GetBaseException().Message) -ForegroundColor Red
            Write-Host "请先处理失败原因；需要帮助时回传 $logDirectory"
        }
    }
} catch {
    if ($launcherLog) { Write-LauncherLog ('启动失败：' + $_.Exception.GetBaseException().Message) }
    Write-Host ('启动失败：' + $_.Exception.GetBaseException().Message) -ForegroundColor Red
    if ($launcherLog) { Write-Host "日志：$launcherLog" }
    throw
}
