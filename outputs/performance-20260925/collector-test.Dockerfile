# 本轮（2026-09-25）性能验证专用 Collector 镜像。
# 基于仓库既有 deploy/python314t.Dockerfile 构建的无 GIL 解释器镜像，
# 额外固化测试脚本所需依赖，避免每次运行联网安装造成版本漂移。
#
# 构建：
#   docker build -f outputs/performance-20260925/collector-test.Dockerfile \
#     -t data-watch-collector-test:20260925 .
FROM data-watch-python314t:latest

RUN pip3.14t install --no-cache-dir "msgspec==0.21.1" "redis==8.0.0" \
      "pytest==8.4.2" "pytest-asyncio==1.2.0" \
  && python3.14t -c "import sys, msgspec, redis; \
assert not sys._is_gil_enabled(), 'GIL must be disabled'; \
print('collector test image ok: msgspec', msgspec.__version__, 'redis', redis.__version__)"

WORKDIR /workspace
