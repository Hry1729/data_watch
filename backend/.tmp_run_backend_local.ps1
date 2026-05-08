$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$env:PYTHONPATH = '.py314t-site'
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'
& 'C:\Users\Hry\.local\bin\python3.14t.exe' -m uvicorn main:app --host 127.0.0.1 --port 8000
