@echo off
setlocal
title QC100 Demo - Ctrl+C to stop
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dpn0.ps1" %*
set "demoExitCode=%ERRORLEVEL%"
echo.
echo Demo stopped. Press any key to close this window.
pause >nul
exit /b %demoExitCode%
