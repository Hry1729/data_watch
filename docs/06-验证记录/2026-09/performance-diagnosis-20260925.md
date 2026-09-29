# Data Watch 性能与可靠性修复：第一阶段诊断（2026-09-25）

基线提交：`a03c951c621709205518b95f141a4fff728fd108`（`main`，工作区干净）。
本文件只做阅读与诊断，不包含实施改动。所有结论都标注证据强度：

- **已由代码确认**：可直接在仓库源码中指出行号。
- **已有测试支持**：本机已跑过并留下原始结果。
- **仍需实验验证**：只有推断或间接证据。

原始资料：`docs/06-验证记录/2026-09/performance-optimization-validation-20260924.md`、
`outputs/performance-20260924/`、`docs/06-验证记录/2026-09/*20260924.json`、`C:\Users\myrh\Downloads\Data_Watch_性能与入库修复方案.md`。

---

## 1. 实际生产启动入口与数据链路

### 1.1 进程入口

| 角色 | 入口 | 说明 |
| --- | --- | --- |
| Collector（集中式） | `collector/main.py:753 async def main()` → `:916 if __name__ == "__main__"` | Windows 侧采集进程 |
| Collector（边缘） | `edge/collector_main.py:27`、`edge/uploader_main.py:79`、`edge/storage_main.py:35` | 边缘三段式 |
| Writer | `writer/main.py:455 async def main()` → `:699 if __name__ == "__main__"` | 单进程 4 协程 |
| Ingest | `ingest/main.py:41` | HTTP 接入路径 |
| Backend | `deploy/docker-compose.yml:50 backend`（`backend/`） | Docker |
| Frontend | `deploy/docker-compose.yml:70 frontend` | nginx 静态资源 |
| Redis / TDengine | `deploy/docker-compose.yml:4,25`，配置 `deploy/redis.conf`、`deploy/taos.cfg` | Docker，`profiles: [infra]` |

Writer 配置实际生效值见 `writer/config/settings.toml`：`consumer_workers = 4`、
`stream_shard_count = 4`、`batch_count = 1000`、`max_buffer_rows = 10000`、
`pending_min_idle_ms = 30000`。

### 1.2 数据链路（集中式）

```
PLC/驱动
  └─ collector/collector/executor.py:466 _write_to_redis(device, scan_task, points)
       ├─ 按 group_code 分组；构造 frame["values"]（全部点）
       ├─ 构造 stream_values（仅 is_storage 且 非 STRING 且 value 非 None）   :520-524
       ├─ 构造 stream_qualities（键集合与 stream_values 一致）               :526-530
       ├─ :532 DurableRedisWriter.write_window_stream(...)   ← 只把入参暂存到 thread-local
       └─ :538 DurableRedisWriter.write_latest(...)          ← 触发真正的落地
            │
            ├─(A) 本地持久接收：CollectorRetryOutbox.record_failure(...)
            │     collector/storage/collector_outbox.py:188
            │     单写者线程 :247 _write_loop → :309 _commit_batch（BEGIN IMMEDIATE…COMMIT）
            │     事务提交后 record_failure 才返回（:237 request.done.wait()）
            │
            ├─(B) 实时 latest 快车道：DurableRedisWriter._queue_latest(...)
            │     durable_redis_writer.py:237 → :272 _latest_loop → :296 EVAL LATEST_LUA
            │     只写 rt:latest:* 并 PUBLISH rt:change；不删除任何持久数据
            │
            └─(C) 历史回放：:414 _replay_loop → outbox.replay_pending(self)
                  collector_outbox.py:365 → durable_redis_writer.py:332 write_durable_frames
                  → :377 _deliver_window：同一连接 pipeline(EVALSHA…) + 末尾一次 WAITAOF
                  → 确认成功后才 collector_outbox.py:399-408 BEGIN IMMEDIATE … DELETE … COMMIT

Redis：
  rt:stream:{device_type}:{group}:{shard}   ← (C) 写；Writer 消费
  rt:latest:{device}:{group}                ← (B) 与 (C) 都写
  rt:window:{device}:{group}:{scan_rate}    ← (C) 写（最近 10 帧）
  rt:change                                 ← (B)(C) PUBLISH

Writer（单进程单事件循环，4 个协程）：
  writer/main.py:599 asyncio.create_task(consumer.run_loop(partition))
  writer/consumer.py:411 XREADGROUP → :442 _handle_entry → :1115 self._buffer.append
  → :1143 _flush → tdengine_writer.write_multi_table → :1218 ACK → :1348 XDEL

Backend：backend/app/services/ws_push.py:395 SUBSCRIBE rt:change
  → :442 wait_for(event.wait(), timeout=5s) → :361 HGETALL/HMGET rt:latest
  → :470 只在 (v,q) 变化时推 diff → WebSocket
Frontend：frontend/src/views/device_monitor/realtime/index.vue:1029 setInterval(flushPendingValues,100)
  ；:1031 setInterval(flushPendingChart, refreshRate)（默认 250ms，:374/:485）
```

### 1.3 谁读哪些键

- `rt:latest:*`：`backend/app/services/ws_push.py:361`、`backend/app/services/monitor.py:146`、
  `backend/app/services/device_activity.py:14`。
- `rt:window:*`：只有 `backend/app/services/monitor.py:182 get_realtime_window`。
- 因此两条路径都要保持正确：latest 缺更新会让实时页显示旧值；window 缺帧会让"实时窗口/最近 10 帧"接口缺数据。

---

## 2. 对上轮审查意见的逐条核实

标记：**✔ 已由代码确认** / **◐ 已有测试支持** / **? 仍需实验验证**。

### 2.1 上轮已修复、本轮不应推倒重做的部分

| 项 | 结论 |
| --- | --- |
| C01 本地 SQLite 成组提交、事件唤醒、有界写入队列 | ✔ `collector_outbox.py:92-97` 常量、`:247 _write_loop` 5ms 提交窗口、`:181 _pending_event` 唤醒、`:223-231` 有界等待 |
| C01 单 owner 独占锁、进程强杀后可重开 | ✔ `collector_outbox.py:147 _acquire_owner`（保留锁文件，避免换 inode） |
| C01 独立 latest 快车道（新帧不必等历史队列） | ✔ `durable_redis_writer.py:189,237,272`；注意它**不删除**任何持久数据 |
| C01 同连接一次 WAITAOF | ✔ `durable_redis_writer.py:377-393`，`WAITAOF` 与 EVALSHA 同 pipeline 同连接 |
| C01 EVALSHA 脚本缓存 | ✔ `durable_redis_writer.py:361-364`（脚本 40 字节摘要） |
| C02 receipt 去重协议 | ✔ `FRAME_LUA_V2:69-111`：session 维度 `high` 连续水位 + 最多 1024 个 `g:<seq>` 乱序间隙，不再每帧一个 7 天 TTL key |
| R01 实时通知 | ✔ 三条写路径都 `PUBLISH rt:change`：`redis_writer.py:73`、`durable_redis_writer.py:53,137,161`、`ingest/app/redis_store.py:171` |
| W01 数值+质量 SQL 字节预算 | ◐ 已含质量与 INSERT 头：`writer/tdengine_writer.py`；上轮实测 200帧×1000点最大 SQL 3120248 < 3145728 字节 |
| W03 多 Stream 全局容量 | ✔ 轮换读 scope 与全局剩余容量：`consumer.py:289 _next_read_scope` |
| W05 缓冲并发访问 | ✔ 已不是问题：`consumer.py:1153-1154` 在任何 await 之前完成 owner 交换；`_flush` 有 `:1144 _flush_lock`；发现任务只往 `_queued_stream_keys` 追加（`main.py:198` → `consumer.py:241`） |
| W06 每 worker 指标 | ✔ `writer/main.py:588-589` 每分片独立 metrics；`:395-397` 汇总求和而非覆盖 |

### 2.2 仍存在的问题

#### A. Collector→Redis 回放容量不足（阻塞级）

- ◐ **输入约 1000 帧/秒，回放约 200–250 帧/秒（1000 点宽帧）**：`outputs/performance-20260924/collector-ft-validation-notes.md:7-8`、
  `collector-ft-profile-final-hmget-3s.json`。生产 10 秒后 outbox 仍积压 9020/3000 帧。
- ✔ **Redis 执行 Lua 是单线程的，脚本内做逐点 JSON 工作**：
  `durable_redis_writer.py:116-135`（FRAME_LUA_V2 与 FRAME_LUA 的 `flush_latest`）对每个点执行
  `HMGET` → `cjson.decode(old)` → 比较 `t` → `cjson.encode(value)` → `HSET`。
  1000 点即每帧约 1000 次 HMGET + 最多 2000 次 cjson 调用。
- ◐ **这段工作在生产稳态下是重复劳动**：实时快车道（B）已经用同样的点、同样的 `t` 写过 latest。
  `docs/06-验证记录/2026-09/collector-frame-lua-decode-profile-20260924.json` 的隔离微基准显示移除该段
  （`skip_latest_*`）把 `eval_ms_mean` 从 2.632ms 降到 0.930ms，即该段约占 Lua 执行时间的三分之二。
  该 JSON 的 `method` 明确写了这是"预置更新的 latest"稳态模拟，且注明 skip 变体"unsafe in production"——
  本轮不能直接删，必须先证明覆盖。
- ◐ **前半段与 AOF 确认各占约一半**：`collector-ft-profile-attribution.md:7-12`。
  WAITAOF 命令 CPU 统计（21µs/11µs）不含阻塞等待墙钟（9.811s/7.527s），不能用它推断持久化很快。
- ✔ **固定成本使窄帧更差**：本轮基线（见 §4）100 设备下，输入都在约 1000 帧/秒，
  生产期内回放确认只有 10 点 620 帧/秒、50 点 871 帧/秒、200 点 751 帧/秒。
  窄帧每帧要做同样的 Lua 固定工作（`TYPE`、`cjson.decode(ARGV[3])`、`LINDEX`、`LTRIM`），
  摊薄在更少的点上，因此存在一个最优帧宽。
- ✔ **每帧把同一批点值序列化多次**：`build_stream_fields` 产出扁平 `fields` 数组
  （`ARVG[1]`）、`latest_fields_json`（`ARGV[2]`）、`frame_json`（`ARGV[3]`，内含同样的 value），
  在 `durable_redis_writer.py:355-360` 一并发送。上轮 5 秒运行 5000 帧线缆字节
  422.86 MB（`collector-ft-profile-attribution.md:9`），约 84KB/帧。

#### B. 单实例就是天花板（架构约束）

- ? **仅靠增加客户端线程或 Stream 数量无法解决**：`stream_shard_count` 只改变 key 数量
  （`stream_envelope.py:15-22`），所有命令仍在同一个 Redis 上串行执行 Lua。
  上轮 4 CPU 对照只有 209.91/250.06 帧/秒（`collector-ft-validation-notes.md:8`），
  与 2 CPU 差别不大，支持"不是客户端 CPU 受限"的判断；但仍需正式实验确认。
- ✔ 真正的分片必须处理 session/seq/receipt/恢复归属：receipt 水位现在是
  `collector:receipt:v2:<session_id>`（`durable_redis_writer.py:353`），
  session 由本地 SQLite 决定（`collector_outbox.py:121`）。跨 Redis 实例分片必须回答
  "某 session 归哪个实例、恢复时谁负责重放"，否则会出现同一 session 被两个实例分别确认。

#### C. Writer：坏数据拖住整个 worker（已由代码确认）

- ✔ `consumer.py:1249 self._buffer = failed_rows + self._buffer` 让 RETRY 行留在队首；
  `:478-493` 随后 `backoff = min(1.0 * self._consecutive_flush_fail, 10.0)` 并
  `await asyncio.sleep(backoff)` —— 睡的是整个 worker 循环。
  这些行还占用容量（`:222 occupied = len(self._buffer) + self._inflight_rows`），
  于是 `:293 available <= 0 → return {}, 0` 抑制该 worker **所有** stream 的读取。
  且 `tdengine_writer.py:870-887` 会把不同 stream 的行打进同一条 SQL，故障面无隔离。
- ✔ 另一处：`tdengine_writer.py:618` 把多段 BAD_DATA 一律降级为 RETRY，
  拆分的坏帧因此永远进不了 DLQ，只会一直重试。

#### D. Writer：回收路径在事件循环上解码（已由代码确认）

- ✔ 新消息路径已把解码移出事件循环（`consumer.py:435-439` → `_prepare_payload_batch`），
  但 **PEL 回收路径没有**：`consumer.py:583`（XAUTOCLAIM）与 `:667`（XPENDING+XCLAIM）、
  以及 `:284` 的过度投递回收都不传 `prepared`，于是走 `:830 raw_values = json.loads(values_raw)`
  与 `:929-942` 的逐点 `float()`，全部在 4 个 worker 共享的同一个事件循环上执行。
- ✔ 4 个 worker 不是 4 个 CPU 执行体：`writer/main.py:599-604` 是同一进程同一事件循环的
  4 个协程；SQL 组包共用一个全局 `ThreadPoolExecutor(max_workers=4)`
  （`tdengine_writer.py:42`）。

#### E. Writer：确认后取消会重写已提交帧（已由代码确认，窗口较窄）

- ✔ `consumer.py:1218` 已经 XACK，但 `:1250 completed = True` 在
  `:1244 await self._run_blocking(self._r.hincrby, ...)` 之后。
  若在这两点之间被取消，`:1254-1255 self._buffer = buffered + self._buffer`
  会把已经确认过的行重新排回缓冲，进而再次写 TDengine；Redis 侧没有二次去重。

#### F. 实时显示链路（已由代码确认）

- ✔ `rt:change` 通知已实现且是主路径；`:442` 有 5 秒兜底。上轮"无通知兜底约 4.96 秒"与代码一致。
- ✔ 数值与图表已解耦：数值固定 100ms（`index.vue:1029`），图表按用户设置默认 250ms（`:374,:1031,:485`）。
- ✔ diff 只发 (v,q) 变化的点（`ws_push.py:470-476`），新鲜度单独走 `freshness`（`:477-494`）。
- ✔ **没有任何代码测量"发送完成→浏览器应用→绘制"**：`ws_push.py:160-162` 的 send 未计时，
  服务器 ASGI send（`backend/app/main.py:230`）未计时，`frontend/src` 内没有
  `performance.now` / `requestAnimationFrame` 计时。上轮 108ms 只到 WebSocket 收包。
- ✔ `frontend/src/composables/useRealtimeWs.ts` 是死代码（从未被 import），
  且是不发 `point_ids`/`supports_freshness` 的分叉客户端；若将来接上会退回整组订阅与旧协议。

#### G. 测试判定与数据对账不足（本轮 P0）

见 §5.1，全部为**已由代码确认**，并已用最小探针实测确认其中一条。

---

## 3. 当前"重复处理"发生在哪里、边界在哪

### 3.1 三条路径写同一份 latest

| 路径 | 写什么 | 失败时的行为 | 属于可靠性边界吗 |
| --- | --- | --- | --- |
| (B) 实时快车道 `LATEST_LUA` | `rt:latest` + `PUBLISH` | 溢出或失败只累计 `latest_fastlane_deferred_frames` 并丢弃在途条目，日志说明"历史队列仍权威" | **否**，允许失败 |
| (C) 历史回放 `FRAME_LUA(_V2)` 的 latest 段 | `rt:latest` + `PUBLISH` | 随整个窗口一起失败，SQLite 保留原帧 | **是**，是权威路径 |
| (C') `rt:window` LPUSH/LTRIM | 最近 10 帧 | 同上 | 是 |

(B) 与 (C) 是**稳态下的重复**：(B) 已经用同样的点、同样的 `t` 写了 latest，
(C) 随后又把同一批点读出来比较再写一次。因为逐点守卫是
`tonumber(cjson.decode(old).t or 0) < tonumber(value.t)`，
(C) 的比较在稳态下几乎总是假，所以它做的是**纯粹的读+解码+编码**，不产生写。

覆盖关系（可作为"跳过"的正确性依据）：

1. (B) 每次收到一帧都调用 `write_latest`，入队的是该帧**全部点**（`executor.py:496-503` 的 `fields`），
   即 (B) 覆盖的点集合是 (C) 覆盖点集合的**超集**（(C) 写的是同一份 `latest_fields_json`）。
2. 两者携带相同的 `t`（都取 `ts_ms = max(field["t"])`），因此 (C) 的逐点守卫判定等价。
3. (B) 只在"已确认写入成功"时才推进水位；溢出/失败时**不**写。
   因此不能用"最近一次 (B) 成功的时间"去跳过 (C)，只能用 (B) **明确确认过**的水位。

### 3.2 可靠确认与本地删除的边界

边界很清楚，且**不包含**实时快车道：

```
record_failure(...)            必须在 SQLite 事务真正提交后才返回   collector_outbox.py:237
  ↓
replay_pending(...)            单 owner（:368 _replay_lock 非阻塞获取）
  ↓
write_durable_frames(...)      组包，逐帧算线缆字节，超过单个/整窗上限就报错并保留
  ↓
_deliver_window(...)           同一连接 pipeline(EVALSHA…) 末尾 WAITAOF 1 0 2000
  ↓
WAITAOF 返回 [offset>=1] 才算确认；否则抛错，容忍 NOSCRIPT 后整体重放      :392-404
  ↓
BEGIN IMMEDIATE … DELETE … COMMIT   只有到这里才本地删除                    collector_outbox.py:400-408
```

- 在 Redis 已接受与 DELETE 已提交之间崩溃：帧仍在 SQLite，重放时由 receipt 水位去重
  （`FRAME_LUA_V2` 的 `seq <= high` 或 `g:<seq>` 命中即 `duplicate`，不再 `XADD`，
  仅 `HINCRBY fence` 推进本连接写偏移以便 WAITAOF 有效）。→ Stream 不会出现重复。
- 任何确认失败/连接断开：抛错 → 保留 SQLite 帧，下一轮重放。**不会**关闭 AOF、不会丢弃待确认数据。
- 实时快车道不参与删除：它不读也不写 SQLite，只是"尽快让 UI 看到新值"。

### 3.3 因此，(C) 的 latest 段可以安全跳过的条件

必须同时满足（缺一即回到全量执行）：

1. Redis 中存在 (B) 写下的、**按 (device, group) 维度**的已确认水位；
2. 历史帧的 `ts` 不新于该水位；
3. 水位缺失（Redis 被清、首次启动、快车道从未成功）时一律执行全量 latest 段。

这样：快车道落后、溢出、断网、重启等情况下，(C) 仍然照原样恢复 latest；
只有"已经确认被 (B) 覆盖"的帧才会跳过。旧写入路径（`redis_writer.py` 非 durable、
`edge_outbox_writer.py`、`ingest/app/redis_store.py`）不含这段逻辑，因此不受影响。

---

## 4. 本轮新增的可复现基线（进行中）

新增 `scripts/validate_collector_ingest_capacity.py`，它取代旧脚本的判定部分，把
**输入按时 / 持久接收 / 持续回放 / 稳态 / 数据正确性 / 追平余量**分开判定，
并在额定输入仍在进行时统计回放确认速率（而不是只看停采后排空）。

环境：专属 Redis `dw-perf-redis-20260925`（`redis:7.4.9`，AOF `everysec`，
`maxmemory 640mb` + `noeviction`，2 CPU / 1g），Collector 容器 `data-watch-collector-test:20260925`
（Python 3.14t、GIL 关闭、2 CPU），专属网络 `dw-perf-20260925`。
未启动 TDengine / Writer，未接触任何已有业务容器。

初步结果（100 设备、100ms、15 秒、输入恒为约 1000 帧/秒）：见
`outputs/performance-20260925/baseline-100dev-*-15s.json` 与 §5 的对照表（扫点完成后补齐）。

---

## 5. 分阶段计划

原则：一次只改一个因素；每步都有独立可复核的证据；不放松可靠性语义。

### P0 先修正测试判定与数据对账（不改生产语义）

| # | 改哪些文件 | 改什么 | 为什么 | 怎么验收 |
| --- | --- | --- | --- | --- |
| P0-1 | 新增 `scripts/validate_collector_ingest_capacity.py` | 六项独立判定（pass/fail/not_tested）；连续窗口的输入速率、回放确认速率、outbox 条数与最老年龄；趋势（growing/shrinking/flat）；生产期内回放速率；`--catchup-seconds` 测"保持额定输入时旧积压是否减少"；按 (device, point, tick) 生成数据并做全字段抽样对账（NULL/布尔/负值/质量变化/非存储点/STRING 点/点位映射）；默认保留证据 | 旧脚本 `target_met` 只描述"输入是否按时提交完成"，无法说明容量达标；旧脚本只查每帧两个点值 | 小负载冒烟（4 设备×8 点×2s）能同时给出六项判定；构造已知错误（如改质量码）能被 `data_correctness` 抓到 |
| P0-2 | `scripts/validate_docker_pipeline.py` | 全字段对账替代"只查 p1/pN"；质量码改为变化的合法值；`steady_state_passed` 增加趋势判定与"保持输入时积压下降"档 | 同上，全链路侧 | 同上 |
| P0-3 | `collector/storage/collector_outbox.py` | 增加 `durable_frames_accepted` 计数器 | 现在没有任何指标表示"本地持久接收了多少帧"：`stream_points_written` 的口径是**已回放到 Redis 的点值数**（`durable_redis_writer.py:375`；durable 路径覆盖了 `write_window_stream` 且不调用 `super()`，所以 `redis_writer.py:209` 的本地累加在该路径上不生效） | 新计数器在排空后等于输入帧数；`collector/tests/test_collector_outbox.py` 全绿 |
| P0-4 | `docs/06-验证记录/2026-09/performance-optimization-validation-20260924.md` | 把"全部正确"等过强表述改为实际检查范围（行数、身份、每帧 2 个点值、latest 全点）；保留原始 JSON 不动 | 交付要求：不夸大正确性范围 | 文档改动只涉及结论表述范围，不改历史数字 |

### P1 降低 Collector→Redis 每帧成本

| # | 改哪些文件 | 改什么 | 为什么 | 怎么验收 |
| --- | --- | --- | --- | --- |
| P1-0 | （仅测试）`scripts/validate_collector_ingest_capacity.py --profile-phases` | 固定环境的可复现基线 + Redis 侧 `EVAL/EVALSHA` CPU、线缆字节、WAITAOF 分段时间 | 一次只改一个因素需要基线 | 同一命令重复两次，主要指标差异可解释 |
| P1-1 | `collector/storage/durable_redis_writer.py`（新增 `FRAME_LUA_V3`）、`collector/storage/collector_outbox.py`（不改协议） | 给历史帧加"已确认水位"检查：快车道成功写入后盖 (device, group) 水位；历史 Lua 在 `frame.ts <= watermark` 时跳过 latest 段与 `cjson.decode(ARGV[2])`；水位不存在则全量执行 | 消除稳态下最贵且重复的一段（约占 Lua 三分之二） | 单因素对照：同环境同负载下 `replayed_fps_during_production` 与 `evalsha_cpu_s` 前后对比；`windows.trend` 从 growing 变 flat/shrinking；数据正确性仍 pass；四类场景回归：快车道正常、快车道溢出、Redis 重启丢水位、SQLite 有积压重启 |
| P1-2 | `collector/storage/durable_redis_writer.py` | 去掉每次重放都重复发送的冗余载荷（`ARGV[2]` 与 `ARGV[3]` 的重复点值），改由既有字段推导，或分批下推 | 每帧省一份点值序列化与一次 cjson 解码 | 线缆字节/帧下降；正确性不变；receipt 去重与 window 语义不变 |
| P1-3 | （仅测试） | 用真实 Redis `EVAL` CPU 与帧宽扫描定位"每帧固定成本 vs 每点成本"，给出曲线 | 说明为什么窄帧更差，并据此判断单实例能否达标 | 曲线可重复；据此决定是否需要分片 |
| P1-4 | 仅在 P1-1..P1-3 后仍不足时 | 分片方案（多 Redis 实例）设计文档 + session/seq/receipt/恢复归属 | 单实例 Lua 串行执行是硬上限 | 不写代码，先给有数据支撑的设计与验收 |

### P1 独立验证 Writer 持续容量

| # | 改哪些文件 | 改什么 | 为什么 | 怎么验收 |
| --- | --- | --- | --- | --- |
| P1-5 | 新增独立 Writer 验证脚本（`writer/benchmarks/` 下） | 用生产 `writer/main.py` 启动流程（真实 worker、动态发现、监控任务），直接向 Stream 注入足量可核对数据，绕过上游限速 | 上轮只用 200 帧一次写入推算，且注入不走生产启动流程 | 持续确认帧数、值/质量写入量、SQL 组包与执行耗时、各 worker 积压、DLQ |
| P1-6 | `writer/consumer.py`（失败隔离） | 让单个持续失败的 schema/点位范围不阻塞同 worker 其它 stream：有界、公平、可追踪的失败队列替代整循环 sleep | 已确认的 C 项 | 注入一条永久坏行，其余 stream 的写入速率不塌陷；坏行进入有界隔离区并可追踪 |
| P1-7 | `writer/consumer.py`（回收路径解码下放） | 把 PEL 回收路径的 JSON/float 解析放到执行单元（与新消息路径一致） | 已确认的 D 项，影响事件循环延迟 | `event_loop_lag_ms` P99 下降；回收吞吐不降 |
| P1-8 | `writer/consumer.py`（确认后取消） | 把 `completed = True` 提到 ACK 之前，或让 ACK 与缓冲回收在同一不可中断段落内 | 已确认的 E 项，避免重启时重写已提交帧 | 取消注入测试：不出现重复写入 TDengine |

### P1 联合负载、故障与最终长测

| # | 内容 | 验收 |
| --- | --- | --- |
| P1-9 | 冻结提交/镜像/依赖/配置；后台跑完整采集+历史回放+入库，同时浏览器订阅若干点 | 报告记录提交哈希、镜像 digest、依赖版本 |
| P1-10 | 分别测 WebSocket 收包、页面数值应用、图表绘制延迟（补前端埋点） | 三项分开给出 P95/P99，不再用收包时间代表显示延迟 |
| P1-11 | `appendfsync everysec + WAITAOF` 下的四类故障：确认前中断、确认超时、确认成功但 DELETE 未提交、带积压重启 | 恢复后重复/遗漏/点值/质量逐项核对；容器强杀不宣称等价于硬件断电 |
| P1-12 | 逐级提负载，目标档持续 30–60 分钟 | 稳态有界且不持续上升；处理旧积压有至少 30% 余量；未达标就如实报瓶颈 |

### 暂不做（需要另立方案）

- W02"质量仅变更写入"：需要初始基线、变坏/恢复、定期检查点、乱序补传、历史 as-of 查询、
  幂等与可回滚迁移。**本轮不实施**，也不放松质量语义。
- 前端既有 100ms 数值刷新：已达标，不动。

---

## 6. 需要确认或注意的事项

1. 上轮报告里的"回放速率"（198.17/226.1 帧/秒）是**生产+排空全程平均**，
   不是持续回放能力；本轮已用"生产期内确认速率"替代，两者不可直接比较。上轮数字本身没算错，
   但作为容量结论容易被误读。
2. 现场 Redis 使用 `maxmemory 3gb` + `noeviction`（`deploy/redis.conf:11-12`）；
   撞上限会直接拒写。本轮隔离环境用更小的上限做护栏，绝对数字不可直接搬到现场。
3. 本机结果不是目标机容量上限；目标机 CPU、内存、磁盘与实际分组点数仍需实测。
