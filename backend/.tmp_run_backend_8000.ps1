$ErrorActionPreference = 'Stop'
$env:PYTHONPATH = 'D:\python\device_alarm\data_watch\backend\.py314t-site;C:\Users\Hry\AppData\Local\Python\pythoncore-3.14-64\Lib\site-packages'
$env:PYTHONUTF8 = '1'
$env:PYTHONIOENCODING = 'utf-8'
Set-Location 'D:\python\device_alarm\data_watch\backend'
& 'C:\Users\Hry\.local\bin\python3.14t.exe' -m uvicorn main:app --host 127.0.0.1 --port 8000
