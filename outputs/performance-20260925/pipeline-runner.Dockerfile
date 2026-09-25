# 完整入库链路（含 TDengine）验证的 runner 镜像（2026-09-25）
#
# 状态：**本镜像未能建成，完整链路验证本轮未执行。**
#
# 原因：`taosws`（TDengine 的 WebSocket 连接器，writer/tdengine_writer.py 依赖）
# 不在 PyPI 上分发——`pip install taosws` 报 "from versions: none"。
# 本机已有的是 Windows 版（`writer/.venv-pack/.../taosws/_taosws.pyd`，
# 12.6 MB 的原生扩展），无法在 Linux 容器里加载。
#
# 要真正跑通需要下列任一项：
#   1. 从 TDengine 官方仓库/安装包取得 Linux amd64 的 taosws wheel，作为构建输入；
#   2. 复用上轮那台已带 taosws 的合并测试镜像（本轮未获取到该镜像的构建方式）；
#   3. 改用 TDengine REST 接口的写入实现（属于代码改动，不在本轮范围）。
#
# 因此 `scripts/validate_docker_pipeline.py`（合成输入→SQLite→Redis→MySQL→TDengine）
# 本轮没有执行，报告与汇总中不得声称"完整入库链路已验证"。
#
# 构建命令保留如下，待拿到 Linux taosws wheel 后可直接复用：
#   docker build -f outputs/performance-20260925/pipeline-runner.Dockerfile \
#     -t data-watch-pipeline-runner:20260925 .
FROM python:3.12-slim

RUN pip install --no-cache-dir \
      "redis==8.0.0" \
      "pymysql==1.1.1" \
      "psutil==6.1.0" \
      "pytest==8.4.2" \
  && python -c "import redis, pymysql, psutil; print('pipeline runner base ok')"

# taosws 需从 TDengine 官方渠道提供 Linux wheel；见文件头说明。
COPY outputs/performance-20260925/wheels/ /tmp/wheels/
RUN if ls /tmp/wheels/*.whl >/dev/null 2>&1; then \
      pip install --no-cache-dir /tmp/wheels/*.whl && \
      python -c "import taosws; print('taosws ok')"; \
    else \
      echo "taosws wheel not provided; full TDengine pipeline validation not possible"; \
    fi

WORKDIR /workspace
