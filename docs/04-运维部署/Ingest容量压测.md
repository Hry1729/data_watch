# Ingest 容量压测

> 本手册用于分布式中心阶段 2 容量签署。压测只允许使用隔离 MySQL/Redis/TDengine 和专用测试节点，不得清空或写入生产库。

## 1. 门禁定义

按目标现场每个边缘节点的最快有效上传周期计算稳态请求率：

`steady_qps_estimate = sum(1000 / node.upload_interval_ms)`

稳态持续至少 30 分钟，目标 QPS 不低于估算值的 99%；突发持续至少 5 分钟，目标不低于稳态的 2 倍。两种场景同时要求：

- HTTP 服务端错误率 `< 0.1%`。
- HTTP 往返 P99 小于现场最小上传节拍；100ms 节拍对应 `P99 < 100ms`。
- 不出现容量导致的 `seq_reject`、`commit_unknown` 或持续重试。
- 测试结束后 Writer 的 Stream lag、PEL 收敛到 0，DLQ 无新增，TDengine 行数与 ACK 数对账一致。
- Redis 内存/连接数、Stream lag、PEL、Writer buffer 不持续增长；服务资源没有持续越过目标部署预算。

压测脚本的延迟范围是请求体序列化和签名完成后到 HTTP 响应读取完成，即报告中的 `latency_scope=http_round_trip`。

## 2. 环境隔离

容量签署必须采用两台机器：

| 主机 | 组件 |
|------|------|
| 目标中心机 | Ingest、Writer、隔离 Redis、隔离 MySQL、隔离 TDengine |
| 独立压测机 | `benchmark_ingest_capacity.py` 和 Python 运行时 |

压测机与中心机共机会争用 CPU、网络栈和事件循环，只能用于开发诊断，不能签署最终 P99。脚本当前使用 HTTP 持久连接，应在隔离测试网直连 Ingest 端口；不要把测试端口暴露到公网。

建议为本轮创建独立数据库名和容器名。任何 `FLUSHDB`、删库、清 Stream 操作前都要再次确认连接指向隔离实例。

## 3. 准备测试夹具

以下命令在仓库根目录执行。测试脚本的默认私钥仅适合隔离压测，生产节点不得复用。

```powershell
$Python = ".\collector\.venv-pack\Scripts\python.exe"
$KeyHex = "<64位十六进制测试私钥>"

& $Python .\scripts\seed_ingest_capacity_fixture.py `
  --host <隔离MySQL地址> `
  --port 3306 `
  --user <测试用户> `
  --password <测试密码> `
  --database device_watch_capacity `
  --nodes 200 `
  --node-offset 0 `
  --items-per-node 10 `
  --private-key-hex $KeyHex
```

夹具应生成 200 个 approved 节点、2000 台归属设备及永久 mapping。启动 Ingest 和 Writer 后，先检查：

- `GET /healthz` 正常，并记录 worker、控制缓存 TTL、MySQL pool 上限。
- Redis 与测试数据库为空或只含本轮夹具。
- Writer 发现预期 Stream scope，4 个消费分片均启动。
- TDengine 质量窄表为 `var_id COMPOSITE KEY + quality TINYINT UNSIGNED`。

## 4. 1000 QPS 稳态

在独立压测机执行：

```powershell
$Python = ".\collector\.venv-pack\Scripts\python.exe"
$KeyHex = "<与夹具一致的测试私钥>"

& $Python .\scripts\benchmark_ingest_capacity.py `
  --url http://<中心Ingest地址>:8010 `
  --nodes 100 `
  --node-offset 0 `
  --client-workers 2 `
  --items-per-batch 10 `
  --period-ms 100 `
  --duration-seconds 1800 `
  --warmup-seconds 10 `
  --phase-mode staggered `
  --min-qps 990 `
  --max-p99-ms 100 `
  --max-error-rate 0.001 `
  --private-key-hex $KeyHex
```

`staggered` 是产品实际模式：节点在自身周期内按确定性相位错峰。只有专门验证批量重启惊群时才使用 `--phase-mode synchronized`，该结果需单独标注，不能替代产品模式基线。

## 5. 2000 QPS 突发

在清理上一轮 session/high_watermark/Stream 后重新启动隔离环境，或使用另一组 100-199 节点，避免旧序号影响结果：

```powershell
& $Python .\scripts\benchmark_ingest_capacity.py `
  --url http://<中心Ingest地址>:8010 `
  --nodes 200 `
  --node-offset 0 `
  --client-workers 4 `
  --items-per-batch 10 `
  --period-ms 100 `
  --duration-seconds 300 `
  --warmup-seconds 10 `
  --phase-mode staggered `
  --min-qps 1980 `
  --max-p99-ms 100 `
  --max-error-rate 0.001 `
  --private-key-hex $KeyHex
```

`client-workers` 是压测机进程数。先确认压测机自身 CPU、网络和 socket 没有饱和；否则增加压测机资源，不能用客户端瓶颈否定服务端容量。

## 6. 同步观测与对账

压测期间至少每 10 秒采集：

- Ingest 进程 CPU、内存、worker 重启和 4xx/5xx。
- Redis `INFO commandstats`、`INFO memory`、连接数；各 Stream 的 `XLEN`、`XINFO GROUPS`、`XPENDING`。
- Writer 每分片 rows_written、buffer、flush 延迟、重连、ACK 重试。
- MySQL/TDengine CPU、连接数、慢查询或失败。
- `rt:dlq:*` 长度和 `commit_unknown`、`seq_reject` 计数。

测试停止后允许 Writer 继续追赶，直到全部 Stream lag/PEL 为 0。记录 ACK item 总数、TDengine 新增值行数和质量行数；预热数据必须单独计数，不能混进正式窗口。

## 7. Redis 与 Writer 容量注意事项

- `INGEST_STREAM_MAXLEN` 默认为 0，表示不主动裁剪 Stream，以保留待入库历史；压测必须持续观察 Redis 内存、Stream 长度和 Writer lag。
- 只有显式配置正数上限时，Ingest 才在 `ingest:stream_trim_item_counts` Hash 中按 Stream 独立累计；每接纳 5000 个 item 执行一次 `XTRIM MAXLEN ~`，随后保留计数余数。
- 正数上限是有损应急策略；Redis 使用近似裁剪，实际长度会短暂高于 maxlen，需按“maxlen + 裁剪间隔 + Redis 近似误差”预留内存。
- Writer 默认 `consumer_workers=4`、`flush_interval_ms=200`、`batch_count=1000`、`max_buffer_rows=10000`。每个 worker 使用独立 MySQL mapper 和 TDengine 连接。
- 增加 Ingest worker 会同时增加 Redis/MySQL 连接和控制缓存副本，必须联动核算。

## 8. 2026-08-02 开发机基线

| 场景 | 请求/QPS | 错误率 | HTTP P99 | 结果 |
|------|----------|--------|----------|------|
| 1000 QPS，30 分钟，共机 | 1,788,998 / 993.89 | 0.00559% | 73.44ms | Writer 最终 lag=0、PEL=0、DLQ=0；稳态通过 |
| 2000 QPS，5 分钟，共机 | 599,996 / 1999.99 | 0 | 125.64ms | 吞吐/可靠性通过，100ms P99 门禁未通过 |

第二行不是最终容量签署。独立压测机复核达到 `QPS >= 1980`、错误率 `< 0.1%`、`P99 < 100ms` 且 Writer 最终收敛后，才可关闭 2000 QPS 突发门禁。
