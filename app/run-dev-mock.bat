@echo off
rem Dev server with mock data, for UI preview (ASCII only).
cd /d "%~dp0"
set "VITE_USE_MOCK=true"
set "VITE_DATAWATCH_ROLE=centralized"
node node_modules\vite\bin\vite.js --port 1420 --strictPort
