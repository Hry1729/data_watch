# 完整入库链路（含 TDengine）验证的 runner 镜像（2026-09-25）
#
# 依赖说明：TDengine 的 WebSocket 连接器在 PyPI 上的**发行名是 `taos-ws-py`**，
# 导入名才是 `taosws`（对应 `writer/requirements.txt` 的 `taos-ws-py`）。
# 之前误按导入名 `taosws` 去装，才会得到 "from versions: none"——
# 那是包名写错，不是"不在 PyPI 分发"。
#
# 构建：
#   docker build -f outputs/performance-20260925/pipeline-runner.Dockerfile \
#     -t data-watch-pipeline-runner:20260925 .
FROM python:3.12-slim

RUN pip install --no-cache-dir \
      "redis==8.0.0" \
      "taos-ws-py==0.7.0" \
      "pymysql==1.1.1" \
      "msgspec==0.21.1" \
      "psutil==6.1.0" \
      "pytest==8.4.2" \
  && python -c "import redis, pymysql, msgspec, psutil, taosws; print('pipeline runner ok (taosws importable)')"

WORKDIR /workspace
