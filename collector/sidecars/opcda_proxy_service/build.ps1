# OPC DA 代理服务编译脚本
# 编译为 x86 目标（32位）以支持 OPC DA COM 组件

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = ".\bin"
)

$ErrorActionPreference = "Stop"

# 编译目标
$TargetFramework = "net48"
$Platform = "x86"

# 源文件
$SourceFiles = @(
    "OpcDaProxyService.cs",
    "OpcConnectionManager.cs"
)

# 输出程序集名称
$OutputAssembly = "$OutputDir\OpcDaProxyService.exe"

Write-Host "=== OPC DA Proxy Service Build Script ===" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration"
Write-Host "Platform: $Platform"
Write-Host "Target Framework: $TargetFramework"
Write-Host ""

# 创建输出目录
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    Write-Host "Created output directory: $OutputDir"
}

# 查找 C# 编译器
$cscCandidate = Get-ChildItem -Path "$env:WINDIR\Microsoft.NET\Framework" -Filter "csc.exe" -Recurse |
    Where-Object { $_.FullName -like "*\Framework\v4.0*" -or $_.FullName -like "*\Framework\v4.*" } |
    Select-Object -First 1

if ($cscCandidate) {
    $cscPath = $cscCandidate.FullName
} else {
    # 尝试使用 .NET Framework 4.8 路径
    $cscPath = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
    if (-not (Test-Path $cscPath)) {
        Write-Error "Cannot find csc.exe. Please install .NET Framework 4.x Developer Pack."
        exit 1
    }
}

Write-Host "Using compiler: $cscPath"

# 构建编译参数
$references = @(
    "System.dll",
    "System.Core.dll",
    "System.Data.dll",
    "System.Xml.dll",
    "System.Web.Extensions.dll"
)

$buildArgs = @(
    "/target:exe",
    "/platform:$Platform",
    "/out:$OutputAssembly",
    "/optimize+",
    "/define:TRACE"
)

if ($Configuration -ieq "Debug") {
    $buildArgs += "/debug:full"
}

# 添加引用
foreach ($ref in $references) {
    $buildArgs += "/reference:$ref"
}

# 添加源文件
foreach ($src in $SourceFiles) {
    if (Test-Path $src) {
        $buildArgs += $src
    } else {
        Write-Error "Source file not found: $src"
        exit 1
    }
}

Write-Host ""
Write-Host "Compiling..." -ForegroundColor Yellow
Write-Host "  $($buildArgs -join ' ')"
Write-Host ""

# 执行编译
$result = & $cscPath @buildArgs 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded!" -ForegroundColor Green
    Write-Host "Output: $OutputAssembly"

    # 复制配置文件
    if (Test-Path "opcda_proxy_config.json") {
        Copy-Item "opcda_proxy_config.json" "$OutputDir\opcda_proxy_config.json" -Force
        Write-Host "Config copied: $OutputDir\opcda_proxy_config.json"
    }
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host $result
    exit 1
}

Write-Host ""
Write-Host "=== Build Complete ===" -ForegroundColor Cyan
