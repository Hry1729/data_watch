@echo off
setlocal EnableExtensions
REM Collector startup script.
REM Detect OPC DA center/edge protocols before starting the collector.

cd /d "%~dp0"

set "PYTHON_EXE="
set "NEED_CENTER=0"
set "NEED_SIDECAR=0"
set "OPCDA_CENTER_COUNT=0"
set "OPCDA_SIDECAR_COUNT=0"

REM Prefer the project venv, but fall back when the venv launcher cannot run
REM from this path.
if exist ".venv\Scripts\python.exe" (
    ".venv\Scripts\python.exe" -V >nul 2>&1
    if not errorlevel 1 (
        set "PYTHON_EXE=.venv\Scripts\python.exe"
    ) else (
        echo [WARN] .venv Python is not runnable from this path.
        echo [WARN] This usually happens when the path contains non-ASCII characters.
        echo [WARN] Please recreate venv in an ASCII-only path:
        echo [WARN]   1. Move collector folder to a path without Chinese characters
        echo [WARN]   2. Run: python -m venv .venv
        echo [WARN]   3. Run: .venv\Scripts\pip install -r requirements.txt
        echo [WARN] Falling back to system Python.
    )
)

if not defined PYTHON_EXE (
    where python3.14t.exe >nul 2>&1
    if not errorlevel 1 set "PYTHON_EXE=python3.14t.exe"
)

if not defined PYTHON_EXE (
    where python.exe >nul 2>&1
    if not errorlevel 1 set "PYTHON_EXE=python.exe"
)

if not defined PYTHON_EXE (
    echo [ERROR] Python runtime not found. Please create .venv or install python3.14t.exe.
    exit /b 1
)

REM Detect protocols from MySQL and import the generated SET commands.
echo [INFO] Checking device protocols...
set "PROTO_ENV=%TEMP%\collector_protocol_%RANDOM%%RANDOM%.bat"
set "PROTO_ERR=%TEMP%\collector_protocol_%RANDOM%%RANDOM%.err"

"%PYTHON_EXE%" check_protocols.py -o batch > "%PROTO_ENV%" 2> "%PROTO_ERR%"
if errorlevel 1 (
    echo [WARN] Protocol detection failed. Collector will continue without auto-starting OPC DA helpers.
    if exist "%PROTO_ERR%" type "%PROTO_ERR%"
) else (
    call "%PROTO_ENV%"
    for %%A in ("%PROTO_ERR%") do if %%~zA gtr 0 type "%PROTO_ERR%"
)

if exist "%PROTO_ENV%" del "%PROTO_ENV%" >nul 2>&1
if exist "%PROTO_ERR%" del "%PROTO_ERR%" >nul 2>&1

REM Start OPC DA proxy services only when center collection devices exist.
if "%NEED_CENTER%"=="1" (
    echo [INFO] Center collection devices detected: %OPCDA_CENTER_COUNT%.

    if exist "sidecars\opcda_proxy_service\start_proxies_auto.bat" (
        pushd sidecars\opcda_proxy_service
        call start_proxies_auto.bat
        if errorlevel 1 (
            popd
            echo [ERROR] Failed to start OPC DA proxy services.
            exit /b 1
        )
        popd
        timeout /t 2 /nobreak >nul
    ) else (
        echo [WARN] OPC DA proxy service not found at sidecars\opcda_proxy_service
    )
) else (
    echo [INFO] No center collection mode devices found, skipping proxy services.
)

if "%NEED_SIDECAR%"=="1" (
    echo [INFO] Edge push mode devices detected: %OPCDA_SIDECAR_COUNT%.
    echo [INFO] Note: Sidecar push services should be started on OPC VMs, not here.
)

REM Start collector main process.
echo [INFO] Starting collector...
"%PYTHON_EXE%" main.py %*
