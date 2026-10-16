# 标签与 deploy/python314t.Dockerfile 的 PYTHON_VERSION（3.14.4）对齐。
# CI 与本机构建都打这个标签；不要改回 :latest，那个标签在 CI 里不存在。
ARG BASE_IMAGE=data-watch-python314t:3.14t-3.14.4
FROM ${BASE_IMAGE}

ENV PYTHONUTF8=1 PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1 PYTHON_GIL=0

WORKDIR /app

COPY ingest/requirements.txt ./requirements.txt
RUN python3.14t -m pip install --no-cache-dir --upgrade pip \
    && python3.14t -m pip install --no-cache-dir -r requirements.txt

COPY data_watch_shared ./data_watch_shared
COPY ingest ./ingest

# 导入入口后 GIL 必须仍关闭；依赖若把解释器拉回 GIL，构建直接失败。
RUN python3.14t -c "import os, sys, ingest.main; assert os.environ.get('PYTHON_GIL') == '0'; assert not sys._is_gil_enabled(), 'ingest import enabled the GIL'"

EXPOSE 8010

# 入口是 ingest/main.py：uvicorn.run("ingest.app.main:app", workers=...)。
# workers>1 时先跑 schema 预热。不要改回直接 uvicorn ingest.main:app。
CMD ["python3.14t", "-m", "ingest.main"]
