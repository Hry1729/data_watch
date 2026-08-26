# Central Ingest 实现说明

> 对应《分布式部署架构方案》阶段 1-3。本文描述当前代码，不作为另一套契约来源。

## 1. 接口与启动门禁

- `POST /ingest`：接收同一 upload session 的有序 UploadItem 批次，返回逐项 ACK。
- `POST /ingest/session_switch`：执行 active session CAS。
- `GET /ingest/session/{node_uuid}`：查询中心 active session。
- `GET /healthz`：进程健康检查，并显示 worker、控制缓存 TTL、MySQL pool 上限等运行参数。

Ingest 通过 ASGI lifespan 做启动门禁：连接 MySQL 后取得 advisory lock，幂等创建或升级 DB6 分布式表，再全量重建 staging 并校验永久 `point_storage_mapping`。缺失、冲突、孤儿或物理坐标差异会保留 staging、令启动失败且不开放健康接口；校验通过后才提升缺失映射并接收流量。该流程仅随分布式中心 Ingest 执行，不影响中心式角色，也不自动执行旧 DB1-DB5 人工迁移。

## 2. HTTP 与认证

请求使用 Ed25519 签名，签名消息覆盖 HTTP 方法、路径、时间戳、nonce 和规范 JSON 摘要。中心从 MySQL 读取 approved 节点的 current key 与未过期 previous key；节点吊销后两把钥立即失效。

认证分成两步：

1. 先验证时间戳、节点公钥和签名，不提前占用 nonce。
2. 可解析的 UploadBatch 把 `SET NX EX nonce` 与 session/high_watermark/Stream/latest 提交放进同一个 Lua。相同请求重试只会得到 `nonce_replay`，不存在“nonce 已占用但业务未执行”的窗口。

签名正确但 JSON/envelope 无法解析时，没有可提交的业务批次，接口仍单独占用同一 nonce key 后返回 400。session 查询与切换接口沿用“验证后立即占 nonce”的包装流程。

HTTP JSON 编解码使用 `msgspec`；兼容层缓存路由 handler 元数据，不在每个请求重复执行反射。

## 3. 业务校验与控制面缓存

业务校验包括节点状态、设备归属、assignment epoch、迁移时间窗、永久点位映射、StreamEnvelope 版本、时间戳和质量码。质量码范围为 `0..255`。旧 config version 在永久 mapping 可解码时继续消费并返回 `stale_config_warning`；真正非法数据返回 `rejected_consumed` 并推进合法序号。

公钥、assignment 和 mapping 使用进程内缓存：

- Backend 管理事务提交后通过 Redis `ingest:control:invalidate` 发布精确或全量失效事件。
- 每个 Ingest worker 订阅失效事件；默认 30 秒带抖动 TTL 只作 Pub/Sub 丢失后的兜底。
- 节点 `last_seen` 最多每 5 秒提交一次，并在后台异步写 MySQL，不阻塞上传 ACK。
- 每个 worker 的 MySQL pool 上限默认 5，可用 `INGEST_MYSQL_POOL_MAXSIZE` 覆盖。

## 4. Redis 批量原子写入

一个 HTTP 批次只执行一次 EVAL。Lua 在一个原子操作内完成：

1. nonce 防重放占位。
2. active session 围栏。
3. 批内 high_watermark 严格序号裁决。
4. `duplicate`、`seq_reject`、`rejected_consumed` 和 `commit_unknown` 语义。
5. StreamEnvelope v1/v2 独立字段 XADD；`values` 与 `qualities` 已在应用层预编码为 Writer 所需 JSON 字符串。
6. latest 按时间戳和 assignment epoch 防倒退更新，旧 `{v,t,q}` 缺少 epoch 时按 `e=0`。Lua 先在批内合并每个设备的最新点位，再以 128 个字段为一组执行 `HMGET/HSET`，避免对每个点位分别执行 Redis Hash 命令。
7. 每个接纳批次合并为一次 `PUBLISH rt:change` 数组；Backend 同时兼容旧单对象负载。
8. 每个 Stream 在 Redis Hash `ingest:stream_trim_item_counts` 中独立累计接纳条目。达到 5000 时才执行一次 `XTRIM MAXLEN ~` 并保留余数，低频 Stream 不会因错过全局计数点而失去裁剪。

`stream_maxlen` 是 Redis 近似裁剪目标，实际长度可在下一次本 Stream 裁剪前短暂高于目标。旧 session 的 high_watermark TTL 由 `INGEST_MAX_OFFLINE_RETENTION_DAYS` 决定，安装配置初始值 7 天，并与边缘 `max_offline_retention_days` 一致。第一阶段仅支持单实例 Redis；Redis Cluster 跨槽兼容另行设计。

## 5. 进程与容量默认

未显式配置 `INGEST_WORKERS` 时，默认 worker 数为：

`min(8, max(2, ceil(physical_cpu_count / 2)))`

物理核心数由 `psutil.cpu_count(logical=False)` 获取；不可用时按逻辑线程数折半估算。8 个物理核心默认 4 个 worker，`INGEST_WORKERS` 可显式覆盖。该值只是目标部署的起点，不替代压测。1000 QPS、30 分钟本机共享资源基线已通过；2000 QPS、5 分钟测试达到 1999.99 QPS 且零错误，但本机共享资源下 HTTP P99 为 125.64ms，未通过 100ms 延迟门禁。最终突发 P99 必须由独立压测机在目标中心资源上复核，详见 [Ingest容量压测](../../04-运维部署/Ingest容量压测.md)。

## 6. Writer 边界

Ingest 不写 TDengine。中心式和分布式中心共用同一个 Writer，消费相同 StreamEnvelope，通过永久 `point_storage_mapping` 解码并写 TDengine。Redis XADD 成功表示“中心已接收”，不等价于 TDengine 已落盘。
