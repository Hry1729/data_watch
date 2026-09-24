# 最终 HMGET 历史 Lua 千点帧 profile（2026-09-24）

原始结果：`collector-ft-profile-final-hmget-3s.json`。冻结后的 `collector/storage/durable_redis_writer.py` 在两版历史 Lua 中以 256 字段为一块调用 HMGET，latest 快车道保持原实现。测试使用专属 Redis 7.4.9 容器 `dw-perf-redis-profile-20260924`，AOF everysec、自动 AOF rewrite 暂停；本轮前后累计 `aof_rewrites=5`，无新重写。Collector 容器为 Python 3.14t、GIL 关闭、2 CPU。测试后已将自动重写配置恢复为 100，采集容器恢复 2 CPU / 无网络，专属 Redis 容器停止但保留文件系统数据。

唯一一次最终运行：100 台设备 × 每帧 1000 点 × 100 ms 调度 × 3 秒，3000/3000 帧在 3.006 秒接收（997.85 帧/秒），生产结束 SQLite 积压 2978 帧；再经 9.408 秒排空，回放总速率 241.64 帧/秒，生产后回放 316.53 帧/秒。逐条遍历 Redis Stream 验证历史 3000/3000、无重复/坏帧；所有设备全部 1000 个 latest 点正确，最终 SQLite 行数及待发快车道均为 0，报告刷盘后清理的本次 Redis 键剩余 0。所有 8 个批次在**同一条原生 Redis 连接**收到 WAITAOF 确认 `[1,0]`。

与同样暂停自动重写、同样 2 CPU、同样 100×1000×100 ms×3 秒的旧逐点 HGET 结果 `collector-ft-profile-no-rewrite-3s.json` 对比：

| 指标 | 旧逐点 HGET | 最终 256 字段 HMGET |
| --- | ---: | ---: |
| Redis INFO EVALSHA 累计执行时间 | 5.956 秒 | 4.889 秒 |
| WAITAOF 前同连接批次墙钟 | 7.415 秒 | 6.238 秒 |
| WAITAOF 回应等待墙钟 | 7.527 秒 | 4.687 秒 |
| 同连接批次总墙钟 | 14.942 秒 | 10.925 秒 |
| 回放总速率 | 182.68 帧/秒 | 241.64 帧/秒 |
| 生产后排空时长 | 13.417 秒 | 9.408 秒 |

历史 EVALSHA 执行累计时间下降约 17.9%，WAITAOF 前墙钟下降约 15.9%，与 HMGET 减少千点读取命令数的预期一致。回放总速率本次高约 32.3%，但每秒 AOF 调度时机、批次大小、背景 IO 均会波动；不能把总速率或 WAITAOF 等待的全部改善直接归因于 HMGET。最终运行仍有 3000 帧中 2978 帧在生产末待回放，说明此环境下持续每秒约 1000 帧的历史处理能力尚未达到。Redis INFO 的 `cmdstat_waitaof.usec` 本轮增量只有 12 微秒，仍不包含 WAITAOF 阻塞等待的 4.687 秒墙钟。

测量只覆盖合成帧到真实 SQLite / Redis Stream / Redis AOF 的 Collector 路径，测试专用计时子类复刻正常同连接 EVALSHA+WAITAOF 顺序；不包含 PLC、TDengine、下游 Writer 或浏览器。测试中的 `WAITAOF回应等待` 是确认等待墙钟，不能等同单次 fsync 系统调用时间。脚本与独立容器均未修改生产代码或原有服务。