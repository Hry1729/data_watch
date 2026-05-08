@echo off
REM 启动所有 OPC DA 代理服务
REM 每个 OPC VM 对应一个独立的代理进程

cd /d "%~dp0"

echo ============================================================
echo OPC DA Proxy Services - Multi-VM Concurrent Collection
echo ============================================================
echo.

REM 检查编译是否存在
if not exist "bin\OpcDaProxyService.exe" (
    echo Building OpcDaProxyService...
    powershell -ExecutionPolicy Bypass -File build.ps1
    if errorlevel 1 (
        echo Build failed!
        pause
        exit /b 1
    )
)

echo Starting proxy services...
echo.

REM 启动本地代理（端口 9090）- 用于本地 OPC DA
REM start "OPC Proxy - Local" cmd /c "bin\OpcDaProxyService.exe --config proxy_local.json"

REM 启动 VM221 代理（端口 9090）
REM 注意：如果本地代理已启动，则 VM221 需要使用其他端口
REM 或者本地和 VM221 共用端口 9090（根据实际情况选择）
start "OPC Proxy - VM221" cmd /c "bin\OpcDaProxyService.exe --config proxy_vm221.json"

REM 启动 VM222 代理（端口 9091）
start "OPC Proxy - VM222" cmd /c "bin\OpcDaProxyService.exe --config proxy_vm222.json"

REM 启动 VM223 代理（端口 9092）
start "OPC Proxy - VM223" cmd /c "bin\OpcDaProxyService.exe --config proxy_vm223.json"

echo.
echo ============================================================
echo All proxy services started.
echo ============================================================
echo.
echo Ports:
echo   - VM221 (172.27.18.221): 9090
echo   - VM222 (172.27.18.222): 9091
echo   - VM223 (172.27.18.223): 9092
echo.
echo Check window titles for individual proxy status.
echo.
pause
