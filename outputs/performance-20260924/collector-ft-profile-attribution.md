# 专属 Redis 中 Collector 回放瓶颈分段定位（2026-09-24）

原始数据：`collector-ft-profile-pilot.json`、`collector-ft-profile-5s.json`、`collector-ft-profile-no-rewrite-3s.json`。执行脚本为 `scripts/validate_collector_free_thread.py`，仅扩展既有测试脚本的 `--redis-host` 与 `--profile-phases`，未修改生产类。专属容器 `dw-perf-redis-profile-20260924` 使用 Redis 7.4.9，隔离网络别名 `perf-redis-profile`，标签 `datawatch.test=performance-20260924`，限制 2 CPU / 768 MiB；AOF appendfsync 为 everysec，服务端 maxmemory 为 512 MiB。Collector 测试容器为 Python 3.14t、GIL 关闭、2 CPU。

测试专用子类借用**同一条 Redis 原生连接**，将整批 EVALSHA 和末尾 WAITAOF 一次打包发送，再顺序读取所有 EVALSHA 回应及 WAITAOF 回应，所有批次的 WAITAOF 回应均为 `[1,0]`。这保留了生产正常路径“同连接写入后确认”的顺序与确认语义；测试实现未复刻生产的 NOSCRIPT 重试分支，整个定位过程中没有出现该错误。分段时间包括：打包、socket 发送、EVALSHA 回应读取，以及 WAITAOF 回应等待。socket 发送可能因 Redis 执行 Lua 而产生背压，不能把发送时间直接称为网络传输时间；WAITAOF 回应等待包含 Redis everysec 调度、持久化确认及响应传输，**不是单次 fsync 系统调用耗时**。

| 场景 | 历史帧 | 同连接批次与包体 | WAITAOF 前墙钟 | WAITAOF 回应墙钟 | 回放总速率 |
| --- | ---: | ---: | ---: | ---: | ---: |
| AOF everysec，5 秒 | 5000/5000 | 13 批，422.86 MB | 12.017 秒（55.1%） | 9.811 秒（44.9%） | 204.70 帧/秒 |
| 禁止自动 AOF rewrite，3 秒 | 3000/3000 | 8 批，253.53 MB | 7.415 秒（49.6%） | 7.527 秒（50.4%） | 182.68 帧/秒 |

5 秒运行中，专属 Redis 的 INFO commandstats 增量：`EVALSHA` 5000 次、执行累计 9.562 秒；latest 快车道 `EVAL` 774 次、执行累计 2.447 秒，合计 12.009 秒，与 WAITAOF 前墙钟 12.017 秒接近。3 秒禁止自动重写运行中，`EVALSHA` 3000 次累计 5.956 秒，`EVAL` 499 次累计 1.486 秒，合计 7.442 秒，与 WAITAOF 前墙钟 7.415 秒接近。Redis Lua 对每帧的千点 JSON 处理和发送背压构成前半段主要成本；AOF 确认等待占另一半。两者共同限制回放，不能归因于单独一项，也不能归因于旧 Python 3.12 合并 runner 的 GIL 或 Writer/TD 竞争。

`cmdstat_waitaof.usec` 在 5 秒运行增量仅 21 微秒、3 秒运行仅 11 微秒，而对应同连接响应等待分别累计 9.811 秒和 7.527 秒。因此 INFO 的该计数**不包含阻塞等待墙钟**，不能拿它推断持久化等待很短。主机打包累计分别仅 47 ms、20 ms；socket 发送墙钟分别 10.917 秒、6.604 秒，但因它与服务端 Lua 执行重叠，不能独立量化纯包体网络开销。

首次读取专属实例时 `aof_rewrites=4` 是累计值，未在 5 秒运行前采样，不能声称其中四次均发生于该次运行。禁用自动 rewrite 后的 3 秒运行前后该累计值保持 4，且 AOF everysec 始终开启，因此这组 49.6%/50.4% 分段不受新的自动重写干扰。两组都逐帧验证 Redis Stream 无重复、每台设备 1000 个 latest 字段正确、SQLite 归零，报告刷盘后仅清理本次唯一前缀数据，Redis DB 14 最终无测试键。

这是单机隔离 Docker 网络中的合成 100 台设备、每台每帧 1000 点负载；不包括真实 PLC、TDengine 写入、下游 Writer 或浏览器。两次运行时长不同，不能把帧速差当作严格的 AOF rewrite 性能对照。分段结果支持继续分别优化 Lua 千点处理/大批传输与 AOF 确认策略，并在相同确认语义下重新测量。