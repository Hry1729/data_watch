@echo off
setlocal
chcp 65001 >nul

title DataWatch Redis Stream Cleanup

set "SCRIPT_DIR=%~dp0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%cleanup_redis_streams.ps1"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%EXIT_CODE%"=="0" (
  echo Cleanup did not complete. Review the message above.
) else (
  echo Cleanup completed successfully.
)
echo Press any key to close this window...
pause >nul
exit /b %EXIT_CODE%
