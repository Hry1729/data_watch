@echo off
setlocal
set "ROOT=%~dp0"
set "PYTHONPATH=%ROOT%vendor;%PYTHONPATH%"
if not defined PYTHON set "PYTHON=python"

%PYTHON% --version >nul 2>nul
if errorlevel 1 (
  echo Python was not found. Install/copy a compatible Windows Python, or set PYTHON to python.exe.
  echo Example: set PYTHON=C:\Python314\python.exe
  exit /b 1
)

%PYTHON% -c "import sys, taosws, sqlalchemy, aiomysql, pymysql, greenlet; print(sys.version); print('offline dependency check: OK')"
exit /b %ERRORLEVEL%
