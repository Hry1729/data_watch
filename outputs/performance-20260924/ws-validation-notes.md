# WebSocket 性能与无 GIL 验证（2026-09-24）

## 原始结果

- `ws-baseline-pre-hmget.json`：镜像 `data-watch-backend:gil-check-20260924`，镜像 ID `sha256:9c06479f6c0083e61a0b79ca6c2d6bf74e38033eca22308e8a4e648930799a3a`。SQLAlchemy 已升级至 2.0.54；实时读取仍使用整组 HGETALL，redis-py 默认连接池为 100。100 台同时读取时出现 `Too many connections`。
- `ws-optimized-r2.json`：镜像 `data-watch-backend:gil-check-20260924-r2`，镜像 ID `sha256:f870dba0587982497db288e7918dcb66a9e7ad8cf1658397912076c83f1f35e6`。加入 Redis 连接池 256 与按订阅点并集 HMGET。
- `ws-optimized-r3.json`：镜像 `data-watch-backend:gil-check-20260924-r3`，镜像 ID `sha256:28d0feff4cf0dc38fa22f0edec63ef4c28d1177ded1ce5358c917457bc6f6736`。在 r2 基础上加入订阅变更期间丢弃旧范围读取的竞态保护；本次运行的 Redis 连接池告警数为 0。

| 镜像 | 交付/提交 | p50 | p95 | p99 | 无通知兜底 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 初版 | 1067/1200 | 514.50 ms | 703.17 ms | 707.54 ms | 4965.37 ms |
| r2 | 1200/1200 | 32.35 ms | 104.58 ms | 106.28 ms | 4950.56 ms |
| r3 | 1200/1200 | 25.31 ms | 108.04 ms | 108.42 ms | 4961.35 ms |

这些是单次合成运行结果，不能视为稳定 SLA 或生产容量承诺。初版到 r2 同时改变了连接池上限和 Redis 读取范围，不能将改善单独归因于其中一项。r2 与 r3 的个位数毫秒差异也不能解释为竞态保护的性能收益或损失。

## 测量范围与边界

测试脚本是 `backend/scripts/realtime_docker_validation.py`，仅允许连接独立 Docker 网络 `dw-perf-20260924` 的 `perf-mysql`、`perf-redis`，并强制 MySQL 库为 `dw_perf_test`、Redis DB 为 15。脚本创建 100 台设备，每台 Redis latest 哈希有 1000 个字段；MySQL 为每台配置 1 个真实可订阅点，其余 999 个 Redis 字段为干扰数据。100 条主 WebSocket 连接各订阅该设备的 1 个点，另有 1 条同目标连接用于验证共享读取。每轮每台只更新这 1 个选中点，12 轮、轮间隔 250 ms，共提交 1200 次更新。未测试每台 1000 个点同时变化的负载。

时延从测试进程调用 Redis HSET+PUBLISH 批处理之前的单调时钟开始，到同一进程中的 WebSocket 客户端收到对应值的时刻结束；它包含 Redis 写入、通知、后端读取/调度、WebSocket 发送和本地客户端接收，但不包含 PLC 采集、collector、TDengine 入库、网络跨主机传输或浏览器绘图。分位数只计算实际收到的更新；初版的 133 次未逐条交付可能由最新值合并及连接池告警共同造成，不能当作数据丢失率。 `t` 是脚本在写入 Redis 时生成的墙钟时间戳，不是 PLC 原始采样时间。

另验证无 `rt:change` 通知时约 5 秒周期兜底、同目标两连接每次通知复用一次 Redis 读取、非选中点不泄露、旧客户端收到完整 1000 字段快照与含 `t` 的差分、撤权后 403 并断开、发送超时后断开。撤权使用测试身份替身，慢客户端采用在真实 WebSocket 路由内注入 1 秒发送延迟（发送超时 0.25 秒），不能代表真实 TCP 背压；TD 健康探测也使用测试替身。服务器以 `lifespan=off` 启动以隔离路由负载，因此没有验证完整生产启动、真实登录/权限库或 TDengine 健康、真实浏览器和 PLC。

镜像构建时完整导入 `main` 并断言 Python 3.14t 的 GIL 保持关闭；r3 测试输出也为 `python_gil_enabled=false`。升级 SQLAlchemy 2.0.54 的依据是[官方发行说明](https://github.com/sqlalchemy/sqlalchemy/releases/tag/rel_2_0_54)，其中明确修复 Cython 扩展导入时重新启用 GIL 的问题。当前源码在独立无 GIL 测试容器中执行 `python3.14t -m pytest -p no:cacheprovider app/tests -q`：179 passed、6 subtests passed。该容器名为 `dw-perf-backend-test-20260924`，仅连接隔离网络，仓库只读挂载在 `/workspace`。