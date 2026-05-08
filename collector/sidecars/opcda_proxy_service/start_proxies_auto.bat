@echo off
setlocal EnableExtensions EnableDelayedExpansion
REM Auto-start OPC DA proxy services for endpoints exported by collector\check_protocols.py.

cd /d "%~dp0"

if not exist "bin\OpcDaProxyService.exe" (
    echo Building OpcDaProxyService...
    powershell -ExecutionPolicy Bypass -File build.ps1
    if errorlevel 1 (
        echo [ERROR] Build failed!
        exit /b 1
    )
)

set "PROGID=%OPCDA_SERVER_PROGID%"
if not defined PROGID set "PROGID=ZPMC.OPCServer"

set "CHANNEL=%OPCDA_CHANNEL%"
if not defined CHANNEL set "CHANNEL=OPD_Server"

set "RUNTIME_DIR=runtime"
if not exist "%RUNTIME_DIR%" mkdir "%RUNTIME_DIR%"

set /a STARTED=0

for /L %%N in (0,1,99) do (
    if defined VM_ENDPOINT_%%N (
        set "ENDPOINT=!VM_ENDPOINT_%%N!"
        set "PORT=!VM_PORT_%%N!"
        if not defined PORT set /a PORT=9090+%%N
        set "CONFIG=%RUNTIME_DIR%\proxy_!PORT!.json"

        >"!CONFIG!" echo {
        >>"!CONFIG!" echo   "listenPort": !PORT!,
        >>"!CONFIG!" echo   "updateRateMs": 500,
        >>"!CONFIG!" echo   "connectionTimeoutMs": 10000,
        >>"!CONFIG!" echo   "opcServers": [
        >>"!CONFIG!" echo     {
        >>"!CONFIG!" echo       "vmHost": "!ENDPOINT!",
        >>"!CONFIG!" echo       "serverProgId": "!PROGID!",
        >>"!CONFIG!" echo       "channel": "!CHANNEL!",
        >>"!CONFIG!" echo       "devices": []
        >>"!CONFIG!" echo     }
        >>"!CONFIG!" echo   ]
        >>"!CONFIG!" echo }

        echo [INFO] Starting OPC DA proxy: endpoint=!ENDPOINT!, port=!PORT!
        start /min "OPC Proxy !ENDPOINT!:!PORT!" "%CD%\bin\OpcDaProxyService.exe" --config "%CD%\!CONFIG!"
        set /a STARTED+=1
    )
)

if "!STARTED!"=="0" (
    echo [WARN] No VM_ENDPOINT_N variables were provided; no proxy started.
    exit /b 0
)

echo [INFO] !STARTED! proxy service(s) started in background.
exit /b 0
