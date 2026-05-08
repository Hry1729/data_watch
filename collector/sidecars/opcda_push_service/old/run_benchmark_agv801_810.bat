@echo off
setlocal
cd /d "%~dp0"

echo ============================================================
echo AGV801-AGV810 OPC DA Benchmark (No HTTP Push)
echo ============================================================
echo.
echo Config: %~dp0opcda_push_config_agv801_810_benchmark.json
echo.
echo This benchmark mode:
echo   - Reads from 10 AGV devices
echo   - Does NOT push to collector (postEnabled=false)
echo   - Use this to verify OPC DA connectivity and measure raw read performance
echo.
echo Expected metrics:
echo   - 10 devices x ~480 points = ~4800 points per cycle
echo   - Single OPC connection, single process
echo   - Cycle time should be well under 1 second
echo.
echo Press Ctrl+C to stop.
echo ============================================================
echo.

.\bin\OpcDaPushService.exe --config opcda_push_config_agv801_810_benchmark.json
