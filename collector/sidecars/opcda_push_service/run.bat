@echo off
setlocal
cd /d "%~dp0"

if not exist "bin\OpcDaPushService.exe" (
  echo bin\OpcDaPushService.exe not found. Run build.ps1 first.
  exit /b 1
)

set CONFIG=opcda_push_config.json
if not exist "%CONFIG%" (
  echo opcda_push_config.json not found. Using opcda_push_config.example.json for local smoke run.
  set CONFIG=opcda_push_config.example.json
)

bin\OpcDaPushService.exe --config "%CONFIG%"
