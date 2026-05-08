@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo AGV801-AGV810 OPC DA Full Subscription (Benchmark Mode)
echo ============================================================
echo.
echo Config: %~dp0opcda_push_config_agv801_810_full_benchmark.json
echo.
echo Benchmark mode (postEnabled=false):
echo   - No HTTP push, only OPC DA subscription
echo   - Use this to verify OPC connectivity and performance
echo.
echo Press Ctrl+C to stop.
echo ============================================================
echo.

.\bin\OpcDaPushService.exe --config opcda_push_config_agv801_810_full_benchmark.json
