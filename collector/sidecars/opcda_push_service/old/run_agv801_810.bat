@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo AGV801-AGV810 OPC DA Push Service
echo ============================================================
echo.
echo Config: %~dp0opcda_push_config_agv801_810.json
echo.
echo This service reads from 10 AGV devices in a single process:
echo   - Single OPC DA connection (shared)
echo   - 10 OPC Groups (one per device)
echo   - ~480 points per device = ~4800 total points
echo   - Poll interval: 1000ms
echo.
echo Press Ctrl+C to stop.
echo ============================================================
echo.

.\bin\OpcDaPushService.exe --config opcda_push_config_agv801_810.json
