# Data Watch 8 模块串行全量回归（可审查、留证据）
#
# 用法：
#   pwsh -NoProfile -File scripts\regression.ps1                      # 全部 8 个模块
#   pwsh -NoProfile -File scripts\regression.ps1 -Modules cli,writer   # 只跑部分模块
#   pwsh -NoProfile -File scripts\regression.ps1 -Label pre-release    # 给本次证据打标签
#
# 与旧版（D:\python\device_alarm\dw-test-env\regression.ps1）相比，本版修正了证据留存问题：
#   1. **保存每个模块的真实退出码**，任一模块失败 ⇒ 脚本整体退出码非 0（旧版只看最后一行文本，恒 0）；
#   2. **完整输出**按 `<时间戳>-<冻结SHA>[-标签]\<模块>.log` 留存，**不覆盖**历史证据；
#   3. 记录**解释器/关键依赖版本、通过/跳过/失败数量与跳过原因**，并输出机器可读 summary.json；
#   4. 严格**串行**执行（并发会造成假失败）。
#
# 注意：本脚本只读仓库与本地 venv，不触碰任何运行中的容器/业务数据。

[CmdletBinding()]
param(
    [string[]]$Modules = @('cli', 'collector', 'writer', 'edge', 'ingest', 'agent', 'backend', 'frontend'),
    [string]$Label = '',
    [string]$LogRoot = 'D:\python\device_alarm\dw-test-env\regression-logs'
)

$ErrorActionPreference = 'Continue'
$root = 'D:\python\device_alarm\data_watch'

# 现场常用 `pwsh -File scripts\regression.ps1 -Modules cli,writer` 调用；`-File` 不会把
# `cli,writer` 拆成数组，因此这里容忍逗号/空格分隔的写法，避免把参数误报成"未知模块"。
$Modules = @(
    $Modules | ForEach-Object { $_ -split '[,;\s]+' } | Where-Object { $_ }
)
$pyT = 'D:\Program Files\python\python3.14.6\python3.14t.exe'
$collectorPy = Join-Path $root 'collector\.venv-pack\Scripts\python.exe'
$agentSite = Join-Path $root 'agent\.venv-pack\Lib\site-packages'
$backendSite = Join-Path $root 'backend\.py314t-site'

$sha = (& git -C $root rev-parse --short HEAD).Trim()
$fullSha = (& git -C $root rev-parse HEAD).Trim()
$dirty = (& git -C $root status --porcelain | Measure-Object).Count -gt 0
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$suffix = if ($Label) { "-$Label" } else { '' }
$runDir = Join-Path $LogRoot "$stamp-$sha$suffix"
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$testTempRoot = Join-Path ([IO.Path]::GetTempPath()) ('dw-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $testTempRoot -Force | Out-Null

function Get-Version([string]$Exe, [string[]]$VersionArgs) {
    try { return ((& $Exe @VersionArgs 2>&1 | Select-Object -First 1) -as [string]).Trim() }
    catch { return 'unavailable' }
}

# 依赖版本：best-effort，取不到就标 unavailable（不因取版本失败影响回归）
function Get-PkgVersion([string]$Exe, [string]$Package) {
    try {
        $out = & $Exe -c "import importlib.metadata as m; print(m.version('$Package'))" 2>&1
        if ($LASTEXITCODE -ne 0) { return 'unavailable' }
        return ($out -as [string]).Trim()
    } catch { return 'unavailable' }
}

$envInfo = [ordered]@{
    frozenSha      = $sha
    fullSha        = $fullSha
    worktreeDirty  = $dirty
    startedAt      = (Get-Date -Format 's')
    host           = $env:COMPUTERNAME
    pyLauncher     = Get-Version 'python' @('-V')
    pyFreeThreaded = Get-Version $pyT @('-V')
    node           = Get-Version 'node' @('-v')
    npm            = Get-Version 'npm' @('-v')
}

$moduleSpec = @{
    cli       = @{ WorkDir = $root;              PyPath = $null;            Exe = 'python';           Args = @('-m', 'pytest', 'cli/tests', '-q', '-rs', '-p', 'no:cacheprovider') }
    collector = @{ WorkDir = $root;              PyPath = $null;            Exe = $collectorPy;       Args = @('-m', 'pytest', 'collector/tests', '-q', '-rs', '-p', 'no:cacheprovider') }
    writer    = @{ WorkDir = $root;              PyPath = $null;            Exe = 'python';           Args = @('-m', 'pytest', 'writer/tests', '-q', '-rs', '-p', 'no:cacheprovider') }
    edge      = @{ WorkDir = $root;              PyPath = $null;            Exe = 'python';           Args = @('-m', 'pytest', 'edge/tests', '-q', '-rs', '-p', 'no:cacheprovider') }
    ingest    = @{ WorkDir = $root;              PyPath = $backendSite;     Exe = $pyT;               Args = @('-m', 'pytest', 'ingest/tests', '-q', '-rs', '-p', 'no:cacheprovider') }
    agent     = @{ WorkDir = $root;              PyPath = $agentSite;       Exe = 'python';           Args = @('-m', 'pytest', 'agent/tests', '-q', '-rs', '-p', 'no:cacheprovider') }
    backend   = @{ WorkDir = (Join-Path $root 'backend'); PyPath = $backendSite; Exe = $pyT;        Args = @('-Xgil=0', '-m', 'pytest', 'app/tests', '-q', '-rs', '-p', 'no:cacheprovider') }
    frontend  = @{ WorkDir = (Join-Path $root 'frontend'); PyPath = $null;    Exe = 'npm';              Args = @('test') }
}

$results = @()
$failed = @()

foreach ($name in $Modules) {
    if (-not $moduleSpec.ContainsKey($name)) { throw "unknown module: $name" }
    $spec = $moduleSpec[$name]
    $logPath = Join-Path $runDir "$name.log"

    Write-Host "########## $name ##########"
    $savedPythonPath = $env:PYTHONPATH
    $savedPythonIOEncoding = $env:PYTHONIOENCODING
    $savedPythonUTF8 = $env:PYTHONUTF8
    $started = Get-Date
    $exitCode = 1
    $output = @()
    $locationPushed = $false
    try {
        $null = Get-Command $spec.Exe -ErrorAction Stop
        Push-Location $spec.WorkDir -ErrorAction Stop
        $locationPushed = $true
        if ($spec.PyPath) { $env:PYTHONPATH = $spec.PyPath } else { Remove-Item Env:\PYTHONPATH -ErrorAction SilentlyContinue }
        $env:PYTHONIOENCODING = 'utf-8'
        $env:PYTHONUTF8 = '1'
        $global:LASTEXITCODE = $null
        $runArgs = @($spec.Args)
        if ($name -ne 'frontend') { $runArgs += @('--basetemp', (Join-Path $testTempRoot $name)) }
        $output = & $spec.Exe @runArgs 2>&1
        if ($null -eq $global:LASTEXITCODE) { throw "Runner did not provide an exit code: $($spec.Exe)" }
        $exitCode = $global:LASTEXITCODE
    } catch {
        $output += $_ | Out-String
        $exitCode = 1
    } finally {
        if ($null -ne $savedPythonPath) { $env:PYTHONPATH = $savedPythonPath } else { Remove-Item Env:\PYTHONPATH -ErrorAction SilentlyContinue }
        if ($null -ne $savedPythonIOEncoding) { $env:PYTHONIOENCODING = $savedPythonIOEncoding } else { Remove-Item Env:\PYTHONIOENCODING -ErrorAction SilentlyContinue }
        if ($null -ne $savedPythonUTF8) { $env:PYTHONUTF8 = $savedPythonUTF8 } else { Remove-Item Env:\PYTHONUTF8 -ErrorAction SilentlyContinue }
        if ($locationPushed) { Pop-Location }
    }
    $elapsed = ((Get-Date) - $started).TotalSeconds

    $text = ($output | Out-String -Width 4096)
    $text | Out-File -FilePath $logPath -Encoding utf8

    function _Count([string]$pattern) {
        $m = [regex]::Matches($text, $pattern)
        $total = 0
        foreach ($hit in $m) { $total += [int]$hit.Groups[1].Value }
        return $total
    }
    # 两套 runner 的摘要格式不同，必须都认：
    #   pytest      : "127 passed in 25.7s" / "739 passed, 26 skipped in 314s"
    #   node --test : "ℹ tests 34" / "ℹ pass 34" / "ℹ fail 1" / "ℹ skipped 2"
    $passedCount = _Count '(\d+) passed'
    if ($passedCount -eq 0) { $passedCount = _Count 'pass (\d+)' }
    $failedCount = _Count '(\d+) failed'
    if ($failedCount -eq 0) { $failedCount = _Count 'fail (\d+)' }
    $skippedCount = _Count '(\d+) skipped'
    if ($skippedCount -eq 0) { $skippedCount = _Count 'skipped (\d+)' }
    $counts = [ordered]@{
        passed  = $passedCount
        failed  = $failedCount
        errors  = _Count '(\d+) error'
        skipped = $skippedCount
        tests   = _Count 'tests (\d+)'
    }
    $skipReasons = @($text -split "`r?`n" | Where-Object { $_ -match '^SKIPPED' } | ForEach-Object { $_.Trim() })

    $record = [ordered]@{
        module      = $name
        exitCode    = $exitCode
        ok          = ($exitCode -eq 0)
        seconds     = [math]::Round($elapsed, 1)
        log         = $logPath
        testTemp    = if ($name -eq 'frontend') { $null } else { Join-Path $testTempRoot $name }
        counts      = $counts
        skipReasons = $skipReasons
    }
    $results += [pscustomobject]$record
    if ($exitCode -ne 0) { $failed += $name }

    Write-Host ("  -> exit={0} passed={1} failed={2} skipped={3} ({4}s)" -f $exitCode, $counts.passed, $counts.failed, $counts.skipped, $record.seconds)
}

$summary = [ordered]@{
    frozenSha   = $sha
    fullSha     = $fullSha
    worktreeDirty = $dirty
    finishedAt  = (Get-Date -Format 's')
    modules     = $results
    failedModules = $failed
    overallOk   = ($failed.Count -eq 0)
    environment = $envInfo
    dependencies = [ordered]@{
        'cli/writer/edge (python)'  = [ordered]@{ pytest = Get-PkgVersion 'python' 'pytest'; redis = Get-PkgVersion 'python' 'redis'; pymysql = Get-PkgVersion 'python' 'pymysql'; typer = Get-PkgVersion 'python' 'typer' }
        'ingest/backend (py3.14t)'  = [ordered]@{ pytest = Get-PkgVersion $pyT 'pytest'; fastapi = Get-PkgVersion $pyT 'fastapi'; sqlalchemy = Get-PkgVersion $pyT 'sqlalchemy' }
        'collector venv'            = [ordered]@{ pytest = Get-PkgVersion $collectorPy 'pytest' }
    }
    note = 'passed/failed/skipped 由输出摘要解析；逐模块完整输出见同目录 <module>.log'
}

$summary | ConvertTo-Json -Depth 6 | Out-File -FilePath (Join-Path $runDir 'summary.json') -Encoding utf8

Write-Host ''
Write-Host "=== 回归汇总（证据目录：$runDir）==="
$results | Format-Table module, exitCode, ok, seconds -AutoSize | Out-String | Write-Host
Write-Host ("overallOk = {0}; failed = {1}" -f $summary.overallOk, ($failed -join ','))

if ($failed.Count -gt 0) { exit 1 }
exit 0
