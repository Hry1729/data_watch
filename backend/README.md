# Backend

python3.14t.exe -m uvicorn main:app --host 0.0.0.0 --port 8000 --reload

This directory contains the backend service code for the project.

Current stack:

- Python 3.14t
- TurboAPI (FastAPI-compatible)
- SQLAlchemy
- aiomysql
- taospy / taosrest

Current skeleton:

- `backend/main.py` - ASGI entrypoint
- `backend/app/main.py` - app factory
- `backend/app/api/` - API routers
- `backend/app/core/` - config, logging, exceptions, responses
- `backend/app/db/` - MySQL and TDengine client helpers
- `backend/app/services/` - monitor read orchestration for MySQL + TDengine

Current routes:

- `GET /healthz`
- `GET /api/device-monitor/system/ping`
- `GET /api/device-monitor/monitor/device-tree`
- `GET /api/device-monitor/monitor/realtime-init`

Runtime config:

- Reads environment variables first
- Falls back to `backend/.env` when present
- Falls back to `backend/.env.example` for the current local baseline
- Falls back to `collector/config/settings.toml` for shared defaults
- TDengine queries currently prefer `taosws` when available and fall back to `taosrest`

Docker build:

- `Dockerfile` strips `perspective-python` and `taos-ws-py` from `requirements.txt` before `pip install`, because neither builds on free-threaded Python 3.14t.
- `taospy` / `taosrest` remain installed, so inside the image TDengine access automatically uses the REST connector (`taosrest`). This is the same optional-capability degradation as `perspective-python`: functional, just without the `taosws` WebSocket connection pool.

Suggested local run command:

```powershell
$env:PYTHONPATH='D:\python\device_alarm\data_watch\backend\.py314t-site;C:\Users\Hry\AppData\Local\Python\pythoncore-3.14-64\Lib\site-packages'
$env:PYTHONUTF8='1'
$env:PYTHONIOENCODING='utf-8'
& 'C:\Users\Hry\.local\bin\python3.14t.exe' -m uvicorn main:app --host 0.0.0.0 --port 8000 --reload
```
