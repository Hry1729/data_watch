# Collector 无 GIL 接收与 Redis AOF 回放独立实测（2026-09-24）

原始结果为 `collector-ft-2cpu.json`、`collector-ft-4cpu.json`，极小规模协议探测为 `collector-ft-pilot.json`。执行脚本是 `scripts/validate_collector_free_thread.py`。测试使用现有隔离容器 `dw-perf-collector-test-20260924` 的 Python 3.14t 和正确 cp314t 依赖，GIL 始终关闭；容器原有限制为 2 CPU，4 CPU 对照结束后已恢复为 2 CPU。仅接入独立 Docker 网络 `dw-perf-20260924` 的 Redis `perf-redis` DB 14；没有启动 TDengine、Writer、PLC 或 MySQL 调用链。

| 场景 | 目标 / 实际帧 | SQLite 接收 | 生产结束队列 | 回放总速率 / 生产后速率 | 排空时长 | 峰值 RSS |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| 2 CPU，10 秒 | 10000 / 9900 | 983.96 帧/秒 | 9020 帧，700.24 MB payload | 198.17 / 226.10 帧/秒 | 39.895 秒 | 232.72 MB |
| 4 CPU，3 秒 | 3000 / 3000 | 998.00 帧/秒 | 2822 帧，218.43 MB payload | 209.91 / 250.06 帧/秒 | 11.285 秒 | 230.07 MB |

2 CPU 在 10 秒到时只完成 99/100 个周期，因此“目标帧数达成”为 false，但 9900 个已接受帧的正确性完全通过。4 CPU 对照较短，不能把两个总速率之差严格归因于 CPU 核数。两次均在生产结束后留下大部分帧待回放，说明在此千点帧形态和隔离 Redis AOF 配置下，接收接近每秒 1000 帧，Redis 历史回放只有约每秒 200–250 帧，持续输入会使 SQLite 队列增长。这是在正确 Python 3.14t、GIL 关闭、没有 Writer/TD 竞争的环境中观察到的，排除了旧 Python 3.12 合并测试运行时作为唯一解释。此测试仍未拆分 Lua 每点处理和 WAITAOF 等待各自耗时，不能进一步断言两者的相对责任。

2 CPU 的 `outbox_accept_ms` 平均 20.219 ms、p95 所在固定桶上界 40 ms；`outbox_commit_ms` 平均 9.379 ms、p95 桶上界 30 ms；`redis_aof_batch_ms` 平均 1850.698 ms，24 批，最大 3668.477 ms。4 CPU 对应均值为 19.868 ms、8.879 ms、1389.733 ms，9 个 AOF 批次。指标来自项目内固定桶 `PerformanceMetrics`：p50/p95/p99 是桶上界，不是原始样本精确分位数；`redis_aof_batch_ms` 包含同一 pipeline 的 EVALSHA 帧处理和 WAITAOF 持久化确认，不能单独解释为 fsync 时间。RSS 取测试 Python 进程，Redis 内存峰值 2 CPU 为 318.90 MB、4 CPU 为 148.91 MB，包含隔离 Redis 中其他已有测试数据。

输入按每设备每 100 ms 生成 1000 个字符串值、1000 个质量码和 1000 个 latest 值；每帧经真实 DurableRedisWriter 完整提交 SQLite，后台以原始时间戳回放到 Redis Stream 并经 WAITAOF 确认。测试受 10 秒/3 秒时限和 400 MiB Redis 内存护栏约束，未无限追加历史。回放排空后逐条遍历本次唯一 device_type 的 Stream：2 CPU 历史 9900/9900、4 CPU 3000/3000，均无重复或坏帧；每台设备所有 1000 个 latest 字段的值、质量和最终时间戳均正确；两次最终 SQLite 行数为 0。验证 JSON 已刷盘后，脚本只清理本次唯一 Stream、latest、window 和 v2 session receipt 键及自身临时 SQLite；清理后本次键剩余 0，Redis DB 14 内存回落约 54 MB。

测试衡量合成数据进入 Collector 本地 SQLite 及回放到 Redis AOF 的能力，不包含真实 PLC 扫描、采集驱动解码、跨主机网络、TDengine 入库、浏览器或下游消费；不要把接收速率视为完整入库速率。10 秒测试的实际帧数不足 10000，不能宣称达成严格的每秒 1000 帧持续目标。