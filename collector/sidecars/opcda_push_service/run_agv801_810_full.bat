@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo AGV801-AGV810 OPC DA Full Subscription Mode
echo ============================================================
echo.
echo Config: %~dp0opcda_push_config_agv801_810_full.json
echo.
echo This configuration includes:
echo   - 10 AGV devices (AGV801-AGV810)
echo   - 9 groups per AGV:
echo     * Control (100 points)
echo     * Fault (560 points)
echo     * ViewMain (482 points)
echo     * ViewPage1-6 (100-101 points each)
echo   - Total: ~17460 points per cycle
echo.
echo Subscription mode settings:
echo   - UpdateRate: 500ms (OPC checks for changes every 500ms)
echo   - PushInterval: 500ms (push cached data every 500ms)
echo   - ReportInterval: 5000ms (stats every 5 seconds)
echo.
echo Press Ctrl+C to stop.
echo ============================================================
echo.

.\bin\OpcDaPushService.exe --config opcda_push_config_agv801_810_full.json
