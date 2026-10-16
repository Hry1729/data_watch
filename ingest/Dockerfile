FROM data-watch-python314t:latest

ENV PYTHONUTF8=1 PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1

WORKDIR /app

COPY ingest/requirements.txt ./requirements.txt
RUN python3.14t -m pip install --no-cache-dir --upgrade pip \
    && python3.14t -m pip install --no-cache-dir -r requirements.txt

COPY data_watch_shared ./data_watch_shared
COPY ingest ./ingest

# 导入入口后 GIL 必须仍关闭；依赖若把解释器拉回 GIL，构建直接失败。
RUN python3.14t -c "import sys, ingest.main; assert not sys._is_gil_enabled(), 'ingest import enabled the GIL'"

EXPOSE 8010

# 入口是 ingest/main.py：uvicorn.run("ingest.app.main:app", workers=...)。
# workers>1 时先跑 schema 预热。不要改回直接 uvicorn ingest.main:app。
CMD ["python3.14t", "-m", "ingest.main"]
