@echo off
setlocal
cd /d "%~dp0"

if not exist "bin\OpcDaProbe.exe" (
  echo bin\OpcDaProbe.exe not found. Run build.ps1 first.
  exit /b 1
)

bin\OpcDaProbe.exe --server ZPMC.OPCServer --channel OPD_Server --device AGV801 --group ViewMain --csv ..\..\..\data\AGV_OPC\agv\ViewMain.csv --skip 310 --limit 10 --probe-count 10 --source cache --read-delay-ms 500
