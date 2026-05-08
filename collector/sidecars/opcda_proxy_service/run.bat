@echo off
REM OPC DA Proxy Service 启动脚本

cd /d "%~dp0"

if not exist "bin\OpcDaProxyService.exe" (
    echo Building OpcDaProxyService...
    powershell -ExecutionPolicy Bypass -File build.ps1
    if errorlevel 1 (
        echo Build failed!
        pause
        exit /b 1
    )
)

echo Starting OPC DA Proxy Service...
echo Listening on port 9090
echo.

bin\OpcDaProxyService.exe --config opcda_proxy_config.json

pause
