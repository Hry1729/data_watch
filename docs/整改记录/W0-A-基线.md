# W0-A 基线报告（只读诊断）

- 生成者：W0-A 基线 agent
- 仓库根：`D:\python\device_alarm\data_watch`
- 执行方式：只读诊断；本轮唯一写入的文件即本报告
- 依据清单：`docs\Data_Watch_综合审查与Agent整改执行清单_6845e80.md`
  - §2.1 架构与数据边界（第 7–15 行）
  - §2.3 推荐执行顺序（第 27–37 行）
  - D39 条目（第 1384–1396 行）
  - §7 E1（第 1490–1496 行）

> **诚实性声明**：本报告每条结论都附 `文件:行号` 或真实命令输出。凡未实测成功的项目均显式标注「未确认」并列出试过的命令，不编造通过结果。

---

## 1. Git 基线

### 1.1 实测输出

```text
$ git rev-parse HEAD
6845e800990f8352f46655036741bd79ca25eed1

$ git rev-parse --abbrev-ref HEAD
main

$ git log -1 --format='%H%n%ci%n%s'
6845e800990f8352f46655036741bd79ca25eed1
2026-10-02 15:02:07 +0800
chore: 忽略本地测试/诊断辅助件（仅本地保留，不上传）

$ git status --porcelain
?? "docs/Data_Watch_综合审查与Agent整改执行清单_6845e80.md"
?? "docs/整改记录/"

$ git diff --stat            # （空）
$ git diff --cached --stat   # （空）
$ git rev-parse --abbrev-ref --symbolic-full-name '@{u}'
origin/main
$ git rev-list --left-right --count HEAD...'@{u}'
0	0
```

### 1.2 结论

| 项 | 值 |
|---|---|
| HEAD 完整哈希 | `6845e800990f8352f46655036741bd79ca25eed1` |
| 分支名 | `main`（跟踪 `origin/main`，与上游 **0 领先 / 0 落后**） |
| 文档所称基线 | `6845e800990f8352f46655036741bd79ca25eed1` |
| **是否等于文档基线** | **是，逐字符完全一致** |

### 1.3 工作区差异与需保护的未提交内容

- **已跟踪文件：零改动**。`git diff --stat` 与 `git diff --cached --stat` 均为空，无暂存内容。没有「已修改未提交」的源码/配置/测试文件。
- **未跟踪（untracked，需要保护，不得删除）**：
  1. `docs/Data_Watch_综合审查与Agent整改执行清单_6845e80.md` —— 用户提供的整改清单本体，**唯一事实源**，未纳入版本控制。
  2. `docs/整改记录/` —— 本轮整改的新目录，当前含 `隔离测试环境.md`、`README.md`（其他 agent 产出）。
- **Stash 栈（4 条，必须原样保留，严禁 `stash pop/drop/clear`）**：

```text
$ git stash list
stash@{0}: On main: docs-leftover-backup
stash@{1}: On main: synced-agent-docs-backup
stash@{2}: On main: stage1-uncommitted-before-sync
stash@{3}: On main: codex: preserve generated pycache before merging 002
```

> **给后续 agent 的硬约束**：不得执行 `git stash` 任何子命令、`checkout`、`reset`、`clean`、`rebase`。工作区虽无已跟踪改动，但 4 条 stash 与 2 项未跟踪资产属于用户财产。

---

## 2. 生产运行入口清单

### 2.1 入口总表

| # | 组件 | 文件 | 函数 / 类 | 启动方式 | 默认 writer / storage 实现 |
|---|---|---|---|---|---|
| 1 | 中心 Collector | `collector/main.py` | `CollectorApp.__init__` → `main()` | `python main.py`（cwd=`collector/`）；或 `collector\run.bat` | **`RedisWriter`**（`storage.redis_writer`），`storage_path="redis_only"` |
| 2 | 边缘 Collector | `edge/collector_main.py` | `main()` + `_writer_factory()` | `python edge/collector_main.py` | **`EdgeOutboxWriter(EdgeStorageClient(), metrics)`** |
| 3 | 边缘 Uploader | `edge/uploader_main.py` | `main()` / `settings_from_env()` | `python edge/uploader_main.py`（需 3 个环境变量） | `EdgeStorageClient()`（HTTP 到本机 Edge Storage） |
| 4 | 边缘 Storage | `edge/storage_main.py` | `main()` → `StorageEngine` + `StorageHttpServer` | `python edge/storage_main.py --host --port` | SQLite `edge.db`（`StorageEngine`，单写者） |
| 5 | Writer | `writer/main.py` | `_connect_writers()` → `StreamConsumer` | `python main.py`（cwd=`writer/`，文档字符串第 7 行明示） | **`TDengineWriter`**（Redis Stream → TDengine） |
| 6 | Ingest | `ingest/main.py` | 模块级 `app` (`ingest.app.main:app`) | uvicorn，见 §2.3 | `MySQLIngestRepository` + Redis（AOF 确认） |
| 7 | Backend | `backend/main.py` → `backend/app/main.py` | `create_app()` | `python3.14t.exe -m uvicorn main:app --host 0.0.0.0 --port 8000` | MySQL（配置/权限）+ TDengine（历史）+ Redis（实时） |
| 8 | Agent | `agent/main.py` | `create_app()` + `uvicorn.run` | `python agent/main.py`；绑定 `127.0.0.1:<cfg.agent.port>` | 进程编排（`ProcessManager`）；不直接持有 writer |
| 9 | CLI | `cli/main.py` | `typer.Typer` 应用 `app`；入口 `cli.main:main` | `data-watch ...`（`pyproject.toml` `[project.scripts]`） | HTTP 客户端，无 writer |

### 2.2 中心 vs 边缘 writer 绑定的关键证据

**中心入口默认 `RedisWriter`（不构造 SQLite Outbox / Durable / 每批 WAITAOF）** —— `collector/main.py:181-196`：

```python
181:        if writer_factory is None:
182:            # 中心式采集：解析/分组后**直接批量写 Redis**（latest / window / Stream）。
183:            # 中心路径不经本地 SQLite、不做持久回放、不维护采样 receipt/session/seq、
184:            # 不逐批等 WAITAOF —— 只有分布式边缘采集才走
185:            # EdgeStorage(本地 SQLite) → Uploader → 中心 Ingest → Redis。
186:            # DurableRedisWriter 仅保留供历史诊断与遗留处置，不是边缘实现。
187:            redis_client = get_redis(config)
188:            self.redis_writer = RedisWriter(
189:                redis_client,
190:                config,
191:                metrics=self.performance_metrics,
192:            )
193:        else:
194:            # writer_factory 边界必须保留：edge/collector_main.py 正是通过它注入
195:            # EdgeOutboxWriter(EdgeStorageClient(), metrics)。
196:            self.redis_writer = writer_factory(config, self.performance_metrics)
```

`collector/main.py:52` 导入面仅有 `from storage.redis_writer import RedisWriter`（**无** `DurableRedisWriter`、**无** `CollectorRetryOutbox` 导入）。

**边缘入口注入 `EdgeOutboxWriter`** —— `edge/collector_main.py:17-33`：

```python
17: from storage.edge_outbox_writer import EdgeOutboxWriter
19: def _writer_factory(_config, metrics):
20:     return EdgeOutboxWriter(EdgeStorageClient(), metrics)
27: async def main():
28:     from main import main as collector_main
30:     return await collector_main(
31:         writer_factory=_writer_factory,
32:         config_manager_factory=_config_manager_factory,
33:     )
```

即：中心与边缘共用 `collector/main.py`，**差异完全由 `writer_factory` 注入点决定**，这就是 D39 要求「文档每种部署形态能映射实际生产入口」的代码锚点。

### 2.3 Ingest 启动方式与 Dockerfile 实际命令

- 本地/脚本启动（`ingest/main.py:41-56`）：`uvicorn.run("ingest.app.main:app", host=settings.host, port=settings.port, workers=settings.workers)`；多 worker 时先 `_prepare_schema_for_workers()` 跑 MySQL schema 预热（第 47–48 行）。
- **`ingest/Dockerfile:15` 的实际启动命令**：

```dockerfile
CMD ["python", "-m", "uvicorn", "ingest.main:app", "--host", "0.0.0.0", "--port", "8010"]
```

- 基础镜像 `python:3.14-slim`（`ingest/Dockerfile:1`）——注意这是 **GIL 版 3.14 镜像**，而 TurboAPI 要求 free-threading；这是 §4 中 ingest 测试分裂的同一个根因，属「未确认能否在容器内正常运行」的疑点，留给后续批次。

### 2.4 边缘 Storage 的启动与单写者形态

`edge/storage_main.py:41-72`：`StorageEngine(edge_database_path(), max_queue=..., shutdown_timeout_seconds=...)` → `engine.start()` → `StorageHttpServer((args.host, args.port), engine)`，随后**单线程 `server.handle_request()` 循环**（第 69–70 行）。

- 默认绑定 `127.0.0.1`（第 24 行 `DATAWATCH_EDGE_STORAGE_HOST` 默认 `127.0.0.1`），默认端口 `7073`（第 25 行）。
- 主循环内 `server.timeout = 0.5`（第 48 行）。

---

## 3. D39 确认矩阵（本轮核心产出）

D39 原文（`docs\Data_Watch_综合审查与Agent整改执行清单_6845e80.md:1390`）：

> **判断：**本轮综合额外保留的重要漂移。当前中心 RedisWriter 直写；分布式 Ingest 仍有同连接 Lua+WAITAOF，不能一并删除。

下面逐条以代码为证据确认。

### 3.1 链路一：中心路径 —— PLC/OPC → Collector → Redis → Writer → TDengine

**结论：中心入口已不构造 SQLite Outbox、不构造 DurableRedisWriter、不做 Durable 回放、不逐批 WAITAOF。确认为「纯 Redis 直写」。**

| 待确认事实 | 结论 | 证据 |
|---|---|---|
| 默认 writer 是 `RedisWriter` | ✅ 是 | `collector/main.py:188` |
| 默认分支不导入 `DurableRedisWriter` | ✅ 未导入 | `collector/main.py:52`（仅导入 `RedisWriter`） |
| 默认分支不构造 `CollectorRetryOutbox` | ✅ 未构造 | `collector/main.py:181-192` 无 outbox 构造；`writer_factory is None` 分支仅 `get_redis()` + `RedisWriter()` |
| 不启动 Durable 回放线程 | ✅ 证据在测试中固化 | `collector/tests/test_central_runtime_redis_only.py:187` `assert _durable_threads() == []` |
| 无 receipt Lua / WAITAOF 调用 | ✅ 证据在测试中固化 | 同文件 `:192` `assert after_waitaof == before_waitaof` |
| `DurableRedisWriter` 仍在仓库中（仅供遗留诊断） | ⚠️ 存在但非生产路径 | `collector/storage/durable_redis_writer.py:190`（`class DurableRedisWriter(RedisWriter)`）、`:467`（`pipe.execute_command("WAITAOF", 1, 0, 2000)`） |

**实测证据（我在本轮真实执行）**：

```powershell
# workdir = D:\python\device_alarm\data_watch
python -m pytest collector/tests/test_central_redis_only_default.py `
                 collector/tests/test_central_runtime_redis_only.py -q -p no:cacheprovider
# => 10 passed, 2 skipped in 0.68s
```

其中 `test_central_runtime_redis_only.py` 把 `DurableRedisWriter.__init__` 与 `CollectorRetryOutbox.__init__` 替换成「调用即抛 `AssertionError`」（该文件 `:81-85`），构造**真实 `CollectorApp` 且不传 `writer_factory`**（`:100`），仍能完整跑完一轮采集 → 解析 → 分组 → Redis 写入 → 关闭。这是「中心不构造 Durable/Outbox」的**运行时反证**，强于静态断言。

> 2 skipped 的原因：该测试需要显式提供隔离 Redis（`DW_CENTRAL_TEST_REDIS_URL`，见 §4.5），未配置即跳过，**不会触碰现场 Redis**。

### 3.2 链路二：分布式路径 —— Edge Collector → Edge Storage/SQLite → Uploader → Ingest → Redis → Writer → TDengine

**结论：边缘仍使用 `EdgeOutboxWriter`，仍为本地 SQLite 先存 + 单写者 + 有序上传。确认无漂移。**

| 待确认事实 | 结论 | 证据 |
|---|---|---|
| 边缘 writer 是 `EdgeOutboxWriter` | ✅ 是 | `edge/collector_main.py:20` |
| `EdgeOutboxWriter` 落到 SQLite Outbox | ✅ 是 | `collector/storage/edge_outbox_writer.py:12` `class EdgeOutboxWriter`，`:17` 持有 `store: OutboxStore \| EdgeStorageClient` |
| SQLite Outbox 单写者 | ✅ 是 | `edge/app/storage_service.py:145-148`（`StorageEngine` 持有 `OutboxStore`，`outbox = OutboxStore(self.database_path)`）；`edge/storage_main.py:69-70` 单线程 `handle_request` 循环 |
| Storage 是 SQLite 唯一写者，Collector/Uploader 只经 IPC | ✅ 是 | `edge/storage_main.py:47`（HTTP IPC）；`docs/04-运维部署/README.md:577`「Collector 和 Uploader 不直接执行 SQLite 写事务」 |
| Uploader 有序领取 + 逐项 ACK | ✅ 是 | `edge/app/uploader.py:223` `_upload_records()`、`:307` `completions`、`:325` `OutboxCompletion(record, "sent")` |
| Uploader 单实例（禁止双写者） | ✅ 是 | `edge/uploader_main.py:98` `with SingleInstanceLock(settings.lock_path):` |
| `OutboxStore` 存在于边缘模块 | ✅ 是 | `edge/app/outbox.py:108` `class OutboxStore`、`:85` `class OutboxRecord`、`:101` `class OutboxCompletion` |

**实测证据**：

```powershell
python -m pytest edge/tests -q -p no:cacheprovider
# => 43 passed in 4.39s
```

其中 `edge/tests/test_realtime_upload.py:15` 直接 `EdgeOutboxWriter(store)`（真实 `OutboxStore(tmp_path/"edge.db")`），`edge/tests/test_uploader.py` 覆盖有序 ACK、session 切换不丢积压、仅内存 ACK 时保留本地积压（`test_memory_only_ack_retains_local_backlog`）。edge 全部测试使用 `tmp_path` 临时 SQLite，**不连接任何外部服务**。

### 3.3 链路三：Ingest 的 AOF 边界（同连接先 Lua 后 WAITAOF）

**结论：确认成立。`ingest/app/redis_store.py` 的批次提交仍在同一个 pipeline 连接里先执行 Lua、再 `WAITAOF 1 0 2000`，并在确认不足时抛 `CommitUnknownError`。**

确切行号与代码片段（`ingest/app/redis_store.py:355-374`）：

```python
355:        try:
356:            async with self._client.pipeline(transaction=False) as pipe:
357:                pipe.eval(
358:                    INGEST_BATCH_LUA, len(keys), *keys,
359:                    first_item.upload_session_id, "0",  # never trim unconsumed data
360:                    msgspec.json.encode(payload).decode("utf-8"),
361:                    str(STREAM_TRIM_EVERY_ITEMS), str(self._settings.nonce_ttl_seconds),
362:                )
363:                # EVAL reserves a new nonce even for duplicate batches. The
364:                # same connection's AOF offset includes all preceding writes.
365:                pipe.execute_command("WAITAOF", 1, 0, 2000)
366:                result, persisted = await pipe.execute()
367:                if not isinstance(persisted, (list, tuple)) or int(persisted[0]) < 1:
368:                    raise CommitUnknownError("Redis AOF persistence not confirmed")
369:        except RedisError as exc:
370:            raise CommitUnknownError(str(exc)) from exc
371:        statuses = msgspec.json.decode(_decode_result(result))
372:        if statuses == ["nonce_replay"]:
373:            raise NonceReplayError("nonce already used")
374:        return [RedisProcessResult(status=str(status)) for status in statuses]
```

关键要点：
- `pipeline(transaction=False)`（第 356 行）：**非事务** pipeline，但**同一条连接**。
- Lua (`pipe.eval`) 与 `WAITAOF` 在同一 pipeline 内入队，`await pipe.execute()` 一次往返（第 366 行）——正是 D39 与 E1 所说的「同连接 Lua+WAITAOF，客户端不可拆」。
- 确认判据是 `persisted[0] >= 1`（第 367 行），即 `WAITAOF numlocal >= 1`；不满足即 `CommitUnknownError`，**不降级为内存确认**。
- `redis://` 版本要求 7.2+（`WAITAOF` 为 Redis 7.2 引入），与 `docs\03-数据与契约\DW-01-确认语义与持久化边界.md:20` 一致。

**与中心路径的对照**：中心 `RedisWriter` 不执行 `WAITAOF`（§3.1）；`WAITAOF` 仅存在于 `ingest/app/redis_store.py:365`（分布式在线路径）与 `collector/storage/durable_redis_writer.py:467`（**已撤销**的中心遗留路径）。

**因此 D39 的处置方向确认为：绝不能全文替换成「平台不使用 WAITAOF」，必须拆分中心与分布式两套口径。**

### 3.4 仍与 D39 冲突的文档清单

#### 3.4.1 主要冲突源：`docs/03-数据与契约/DW-01-确认语义与持久化边界.md`

该文件标题为「## 当前实现」（第 3 行），但第 5、8、14、23、27 行描述的是**已撤销的中心 SQLite + WAITAOF 方案**，被当作当前实现陈述。逐条：

| 行号 | 问题原句 | 语境 | 判定 |
|---|---|---|---|
| **5** | 「集中式 Collector 的轮询和 HTTP 采集先写本地 SQLite 队列，使用 WAL + synchronous=FULL。……后台独立线程批量重放，因此重启后即使没有新采样也能排空积压。」 | **中心** | ❌ **冲突**。中心 `collector/main.py:181-192` 已不构造任何 SQLite 队列；仅有 `RedisWriter` 直写。该行为已撤销的中心 Durable 方案。 |
| **6** | 「中心接收确认选择 **Redis 持久接收**，不是 TDengine 最终入库确认。Ingest 在同一非事务 pipeline 连接上执行 Lua 与 `WAITAOF 1 0 2000`；……」 | **分布式**（措辞误置于「中心接收」） | ⚠️ **事实正确、语境错位**。`WAITAOF` 确在 Ingest（`ingest/app/redis_store.py:356-365`），但 Ingest 属**分布式链路**；「中心式」部署并无 Ingest。需按 D39 拆分为两种部署形态矩阵。 |
| **8** | 「集中式 Redis 出口也在同一连接中对批量帧执行 Lua + WAITAOF，全部成功后才删除本地队列记录。原帧 ID 用于 Redis 重放去重，去重记录保留 7 天；……」 | **中心** | ❌ **冲突**。这是 `DurableRedisWriter` 的行为（`collector/storage/durable_redis_writer.py:467`），中心默认路径已不构造该类（`collector/main.py:181-192`，并由 `collector/tests/test_central_runtime_redis_only.py:81-85` 运行时反证）。 |
| **14** | 「集中式队列默认最多 50,000 条，数据库页面最多 1 GiB，并在磁盘可用空间低于 256 MiB 时拒绝新样本。」 | **中心** | ❌ **冲突**。该容量参数属 `CollectorRetryOutbox`（`collector/storage/collector_outbox.py:100`），中心已不构造。 |
| **20** | 「Redis **7.2 或更新版本**，开启 AOF，持久化目录放在可靠持久卷中，ACL 允许 WAITAOF。仓库配置使用 everysec，但成功确认还额外等待实际 AOF fsync。」 | 未区分形态（读作全平台） | ⚠️ **范围过宽**。仅分布式 Ingest 需 `WAITAOF` 授权；中心直写不等待 AOF fsync。 |
| **23** | 「Collector 队列位于 DATAWATCH_DATA_ROOT（兼容 DATAWATCH_HOME）下 runtime/collector-retry.db；默认 C:\ProgramData\DataWatch。」 | **中心** | ❌ **冲突（遗留）**。中心不再创建 `collector-retry.db`；该路径现仅出现在遗留诊断语境。 |
| **27** | 「单元回归覆盖 AOF 超时保留、重启原样重放、并发重放互斥、满载不逐出、实时历史分离和新旧 ACK 兼容。」 | **中心** | ❌ **冲突**。「重启原样重放/并发重放互斥/满载不逐出」是中心 Outbox 的回归项，对当前中心入口不再适用。 |

> 另：`docs/03-数据与契约/DW-01-确认语义与持久化边界.md` 中完全**没有**「Superseded / 已撤销 / 历史方案」任何标记，也**没有**区分「中心式」与「分布式」两个部署形态的小节 —— 这正是 D39 处理方式要求「拆分中心与分布式确认矩阵……标记旧中心 Durable 方案 Superseded」的具体缺口。

#### 3.4.2 反向冲突：把 Ingest 说成「仅内存 ACK」（D39 明确要求核对）

| 文件:行号 | 问题原句 | 语境 | 判定 |
|---|---|---|---|
| `docs/01-总体设计/分布式部署架构方案.md:40` | 「**ACK durability 语义** \| **"中心已接收"**：Redis XADD 成功即 ACK，不等 Writer/TDengine 落盘；Redis AOF everysec 可能丢失 ~1s 数据（可接受）；outbox 最大离线期参数；含容量公式+内存预算」 | **分布式** | ❌ **反向冲突**。现行 Ingest 并非「XADD 成功即 ACK」：`ingest/app/redis_store.py:365-368` 在同连接执行 `WAITAOF 1 0 2000` 并要求 `numlocal >= 1`，否则 `CommitUnknownError`。该句把已实现的 AOF 持久确认**降级描述**为内存确认。全文无 `WAITAOF` 命中（见 §3.4.4 计数），确认该文件完全未对齐现行 AOF 边界。 |

#### 3.4.3 把 `DurableRedisWriter` 标为「生产」的历史测量文档

| 文件:行号 | 问题原句 | 语境 | 判定 |
|---|---|---|---|
| `docs\joint-measurement-20260927.md:27` | 「\| `collector` \| data-watch-collector-test（3.14t、GIL 关闭） \| 用**生产 `DurableRedisWriter` + SQLite Outbox** 按目标速率喂入，并采样一条时间线 \|」 | **中心**（误标为生产） | ❌ **冲突**。`DurableRedisWriter` 已撤销，不是生产中心实现。该句会给读者「中心 = Durable + SQLite Outbox」的错误印象。 |
| `docs\joint-measurement-20260927.md:55,57,89,92` | 「`durable_frames_accepted` 24,000；`history_frames_confirmed` 24,000……」「Outbox 峰值 500 帧……」「本地持久接收帧 …… SQLite 事务提交成功」「`outbox_rejected_frames`」 | **中心** | ⚠️ **未注明适用路径**。D39 处理方式要求「旧性能报告注明适用路径」；这些指标名（`durable_*`、`outbox_*`）已被撤销的中心路径专有。 |
| `docs\joint-ladder-20260927.md:43` | 「每档 AOF 写入量大（40 台档 423 MB/250s）、批大小偏小（均值 13.6–15.6 帧）且每批都要 `WAITAOF`。」 | **中心**（历史档位结论） | ⚠️ **未注明适用路径**。若此结论被当成本轮中心直写的性能结论即错误。 |
| `docs\joint-ladder-20260927.md:64` | 「**我自己引入又撤回的**：为规避 40 台档 ENOSPC，把 outbox 改到**宿主 Windows 绑定挂载**，」 | **中心** | ⚠️ 历史记录，已自述「撤回」，风险较低，但同属旧中心 Durable 语境。 |

#### 3.4.4 全量 grep 命中统计（`docs/`，已排除 `_archive`）

搜索模式 `WAITAOF|Durable|Outbox|outbox`，按文件聚合命中数（**仅列与中心/分布式口径相关的仓库文档**；`docs/着陆页参考/tikhub-clone/node_modules/` 下的 Next.js 官方文档属第三方文档，与 D39 无关，已剔除）：

| 命中数 | 文件 | 性质 |
|---|---|---|
| 51 | `docs\01-总体设计\分布式部署架构方案.md` | 分布式为主（见 :40 反向冲突） |
| 36 | `docs\06-验证记录\2026-09\performance-diagnosis-20260925.md` | 历史，旧中心 Durable |
| 26 | `docs\Data_Watch_综合审查与Agent整改执行清单_6845e80.md` | **本轮清单本体**（:9、:1390、:1394、:1396 已是正确拆分口径，属事实源，不需改） |
| 17 | `docs\06-验证记录\2026-09\performance-verification-recheck-20260926.md` | 历史，旧中心 Durable |
| 15 | `docs\06-验证记录\2026-09\performance-verification-recheck-20260926-round2.md` | 历史，旧中心 Durable |
| 13 | `docs\06-验证记录\2026-09\performance-review-remediation-20260925.md` | 历史，旧中心 Durable |
| 13 | `docs\06-验证记录\2026-09\bounded-inflight-replay-design-20260927.md` | 历史，旧中心 Durable 回放设计 |
| 11 | `docs\06-验证记录\2026-09\acceptance-run2-20260926.md` | 历史，旧中心 Durable |
| 11 | `docs\06-验证记录\2026-09\performance-fix-report-20260925.md` | 历史，旧中心 Durable |
| 10 | `docs\论文\data watch\DataWatch_工业数据底座论文.md` | ✅ **口径正确**（:39/:65/:89「中心化部署由 Collector 直接进入 Redis」；:156「中心化路径……直接进入 Redis」） |
| 9 | `docs\06-验证记录\2026-09\directed-diagnosis-20260927.md` | 历史，旧中心 Durable |
| 8 | `docs\01-总体设计\分布式系统技术架构.md` | ✅ **口径正确**（全部为边缘 outbox 语境，:108、:220、:379） |
| 7 | `docs\06-验证记录\2026-09\central-restore-20260928.md` | ✅ 已记录中心直写（:13「恢复后的中心沿用原直写语义……不使用采样本地队列补偿」） |
| 7 | `docs\04-运维部署\三角色安装包验收清单.md` | ✅ 边缘语境 |
| 7 | `docs\02-模块设计\edge\边缘运行时实现说明.md` | ✅ 边缘语境 |
| 7 | `docs\01-总体设计\系统架构总览.md` | ✅ **口径正确**（:41 明确「中心式由 Collector 直接写 Redis；分布式边缘……SQLite outbox」） |
| 6 | `docs\06-验证记录\2026-09\central-runtime-fix-20260927.md` | ✅ 该文件正是**撤销 Durable** 的记录（:21「将 DurableRedisWriter 与 CollectorRetryOutbox 构造器替换为调用即失败」） |
| 5 | `docs\06-验证记录\2026-09\central-shutdown-drain-fix-20260928.md` | ✅ 中心直写语境 |
| 5 | `docs\04-运维部署\README.md` | ✅ 边缘角色语境（:7、:480、:577） |
| 5 | `docs\joint-measurement-20260927.md` | ⚠️ 见 §3.4.3 |
| 3 | `docs\03-数据与契约\DW-01-确认语义与持久化边界.md` | ❌ **主冲突源**，见 §3.4.1 |
| 2 | `docs\joint-ladder-20260927.md` | ⚠️ 见 §3.4.3 |
| 1 | `docs\06-验证记录\2026-09\reliability-review-delivery.md:33` | ⚠️ 未区分形态（「必须使用 Redis 7.2+、AOF 和允许 WAITAOF 的 ACL」读作全平台要求） |
| 1 | `docs\06-验证记录\2026-09\docker-backend-validation-20260924.md:42` | 历史，旧中心 |
| 1 | `docs\02-模块设计\shared\StreamEnvelope共享契约.md` | 待后续核实（1 处，低风险） |
| 1 | `docs\02-模块设计\backend\分布式节点控制面实现说明.md` | 待后续核实（1 处，低风险） |

**仍需修改的文档（D39 口径）汇总：**

1. **必改（P1，D39 指定）**：`docs\03-数据与契约\DW-01-确认语义与持久化边界.md` —— 第 5、8、14、23、27 行为中心冲突；第 6、20 行需按形态拆分；需新增「中心式 / 分布式」确认矩阵并给旧中心 Durable 方案打 `Superseded`。
2. **反向必改**：`docs\01-总体设计\分布式部署架构方案.md:40` —— 把 Ingest 的 AOF 确认误述为「XADD 成功即 ACK」。
3. **应补注适用路径**：`docs\joint-measurement-20260927.md`（:27、:55、:57、:89、:92）、`docs\joint-ladder-20260927.md`（:43、:64）、`docs\06-验证记录\2026-09\reliability-review-delivery.md:33`。
4. **可选（历史归档，建议加「已被 central-runtime-fix-20260927 撤销」横幅）**：`docs\06-验证记录\2026-09\` 下 13 个旧中心 Durable 记录（performance-diagnosis-20260925、performance-verification-recheck-20260926、-round2、performance-review-remediation-20260925、bounded-inflight-replay-design-20260927、acceptance-run2-20260926、performance-fix-report-20260925、directed-diagnosis-20260927、performance-optimization-validation-20260924、performance-optimization-delivery-20260924、test-readiness-review-20260926、performance-three-package-delivery-20260925、docker-backend-validation-20260924）。

> **明确无需修改**：`docs\01-总体设计\系统架构总览.md:41`、`docs\01-总体设计\分布式系统技术架构.md`、`docs\论文\data watch\DataWatch_工业数据底座论文.md`、`docs\04-运维部署\README.md`、`docs\04-运维部署\三角色安装包验收清单.md`、`docs\02-模块设计\edge\边缘运行时实现说明.md` 已正确区分两形态。**不得**按「平台不使用 WAITAOF」全文替换。

---

## 4. 测试执行方式盘点（实测）

### 4.0 解释器结论（后续所有 agent 的关键输入）

本机 `py -0p`（实测输出）：

```text
 -V:3.14t *       D:\Program Files\python\python3.14.6\python3.14t.exe
 -V:3.14          D:\Program Files\python\python3.14.6\python.exe
```

两个解释器版本号**都是 3.14.6**，但 ABI 不同（实测）：

| 解释器 | 路径 | `SOABI` | `Py_GIL_DISABLED` | `sys._is_gil_enabled()` | pytest |
|---|---|---|---|---|---|
| 系统 python | `D:\Program Files\python\python3.14.6\python.exe` | `cp314-win_amd64` | 未设 | **True**（GIL 开启） | 9.1.1 |
| 系统 3.14t | `D:\Program Files\python\python3.14.6\python3.14t.exe` | `cp314t-win_amd64` | `1` | **False**（free-threading） | 9.1.1 |
| collector venv | `collector\.venv-pack\Scripts\python.exe` | **`cp314t-win_amd64`** | — | **False** | 9.1.1 |
| agent venv | `agent\.venv-pack\Scripts\python.exe` | `cp314-win_amd64` | — | True | **无 pytest** |
| writer venv | `writer\.venv-pack\Scripts\python.exe` | `cp314-win_amd64` | — | True | **无 pytest** |

**关于 `.venv-pack` 是打包 venv 还是测试 venv 的明确回答：**

- `collector\.venv-pack` **既是打包 venv，也是 collector 的正式开发/测试解释器**，不是「只能打包」。依据：
  1. 创建命令即为 free-threaded 解释器：`collector\README.md:53` → `python3.14t -m venv .venv-pack`。
  2. 实测其 `SOABI=cp314t-win_amd64`、GIL 关闭、已装 pytest 9.1.1 与全部采集依赖。
  3. 仓库文档把它登记为**测试命令的解释器**：`collector\tests\README.md:8` → `..\.venv-pack\Scripts\python.exe -m pytest`。
  4. 历史验证记录同样用它跑测试：`docs\06-验证记录\2026-09\central-shutdown-drain-fix-20260928.md:61,63`。
  5. 它确实也装了 PyInstaller（供 `collector\tests\bench_ge_srtp.spec:3` 等基准/diag 构建），但这不排斥其测试用途。
  6. **它是必需的**，不是可选：`packages\*\driver.toml` 声明 `python_abi='cp314t'`，用 cp314 跑会 ABI 校验失败并导致 81 个测试失败（见 §4.1）。
- `agent\.venv-pack` 与 `writer\.venv-pack` **是纯打包 venv**：cp314 + 装有 PyInstaller 依赖（`altgraph`、`ordlookup`、`win32ctypes`、`PyInstaller`），且**未安装 pytest**，实测 `No module named pytest`。不应用作测试解释器。
- `backend\.py314t-site` **不是解释器，是依赖目录**（site-packages）。按 `.gitignore:17` 注释，它是项目基础设施，含 `cryptography 48.0.0`、`redis 7.4.0`、`pytest 9.0.3`、`pytest_asyncio 1.3.0`、`greenlet`、`tzdata` 等，供 free-threaded 解释器通过 `PYTHONPATH` 使用。**仓库根不存在 `.venv-pack`**（实测 `Test-Path '.venv-pack'` → `False`）。

**各模块该用哪个解释器（最终结论）：**

| 模块 | 应使用的解释器 | 理由 |
|---|---|---|
| `cli` | 系统 `python.exe`（cp314） | 纯 HTTP 客户端，无 ABI 约束；98 passed |
| `collector` | **`collector\.venv-pack\Scripts\python.exe`（cp314t）** | 驱动清单强制 `cp314t`；cp314 下 81 failed |
| `writer` | 系统 `python.exe`（cp314） | 184 passed |
| `edge` | 系统 `python.exe`（cp314） | 43 passed |
| `ingest` | **分裂**：见 §4.4 | 无单一解释器可跑全量 |
| `agent` | 系统 `python.exe` **+ `PYTHONPATH=agent\.venv-pack\Lib\site-packages`** | agent venv 有全部运行依赖（含 psutil）但无 pytest |
| `backend` | **`python3.14t.exe` + `PYTHONPATH=backend\.py314t-site` + `-Xgil=0`** | TurboAPI 要求 free-threading；`-Xgil=0` 抵消 sqlalchemy 的 GIL 回退 |
| `frontend` | Node.js `v24.18.0`（`D:\Program Files\node\node.exe`） | `tsx --test` + `vue-tsc`/`vite` |

**两个必须知道的坑（实测踩到）：**

1. **在 3.14t 上必须先加载 `backend\.py314t-site`，否则硬崩**。系统 site-packages 里的 `cryptography` 是 cp314（GIL）轮子；在 3.14t 下 import 会触发
   `Windows fatal exception: access violation`（栈落在 `cryptography\hazmat\primitives\_serialization.py:10`）。只要 `PYTHONPATH` 里 `.py314t-site` 优先，即恢复正常。
2. **backend 需要 `-Xgil=0`**。否则 `app\tests\test_free_threading.py::test_backend_import_does_not_enable_gil` 失败（`AssertionError: GIL was enabled before app import`），因为 `sqlalchemy.cyextension.collections` 会回退开启 GIL（pytest 会打出对应 RuntimeWarning）。加 `-Xgil=0` 后 204 passed。

### 4.1 `collector`

```powershell
# workdir = D:\python\device_alarm\data_watch
& '.\collector\.venv-pack\Scripts\python.exe' -m pytest collector/tests -q -p no:cacheprovider
# 实测 => 1 failed, 619 passed, 26 skipped, 1 warning in 131.94s (0:02:11)

# 对照：错误解释器（系统 cp314）
python -m pytest collector/tests -q --ignore=collector/tests/test_opcua_driver.py
# 实测 => 81 failed, 538 passed, 26 skipped in 20.38s
```

- **可跑：是**（cp314t）。
- 85% 的失败根因已定位（cp314 下）：`data_watch_driver_manifest.DriverManifestError: python_abi='cp314t' 与运行时 'cp314' 不匹配`，来自 `packages\{s7,opcua,modbus_tcp,ge_srtp,opcda_center}\driver.toml`；样例错误输出见 `collector\tests\test_s7_addressing.py` 运行日志，栈顶为 `data_watch_driver_manifest.py:209`。
- cp314t 下**唯一失败**：`collector/tests/test_collector_receipt_v2.py::test_outbox_owner_lock_rejects_second_process_and_crash_releases`（断言在 `test_collector_receipt_v2.py:286`，期望 `ready` 的子进程锁行为）。该失败与 `docs\06-验证记录\2026-09\performance-verification-recheck-20260926.md:308` 记录的既有失败（子进程 owner-lock 用例）一致，**属既有基线失败，非本轮引入**。
- 另 1 个收集错误需单独忽略：`collector/tests/test_opcua_driver.py` → `ImportError: cannot import name 'x509' from 'cryptography.hazmat.bindings._rust'`（`packages/opcua/vendor/cryptography` 与运行时不匹配）。历史文档也用 `--ignore=collector/tests/test_opcua_driver.py` 规避（`docs\06-验证记录\2026-09\performance-verification-recheck-20260926.md:116`）。
- **外部服务需求：无（默认全部隔离）**。真实 Redis 用例均带显式守卫，未设环境变量即 skip：
  - `collector/tests/test_central_runtime_redis_only.py:34-42` → 需 `DW_CENTRAL_TEST_REDIS_URL`，且**断言端口 != 6379**（第 41 行「必须显式指定隔离 Redis 端口，禁止业务默认端口」）。
  - `collector/tests/test_collector_receipt_v2.py:108-112` → 需 `DW_TEST_REDIS_URL`。
  - `collector/tests/test_collector_latest_compensation.py:295-299` → 需 `DATAWATCH_TEST_REDIS_URL`。
  - 其余大量使用 `FakeRedis`/`AsyncMock`（实测 `collector/tests` 内 `FakeRedis|fake_redis|AsyncMock` 命中 102 处）。
  - **本轮 131.94s 全量运行中的 26 个 skip 即包含上述用例，未写现场 Redis。**
- MySQL 集成用例需 `RUN_DB_INTEGRATION=1`（`collector/tests/conftest.py:18-22` 注册的 `integration` marker 说明为「requires a real MySQL database and explicit RUN_DB_INTEGRATION=1」），未设置即不跑。

### 4.2 `writer`

```powershell
# workdir = D:\python\device_alarm\data_watch
python -m pytest writer/tests -q -p no:cacheprovider
# 实测 => 184 passed in 1.01s
```

- **可跑：是**（系统 cp314）。
- 收集：`184 tests collected in 0.12s`。
- **外部服务需求：无**。测试用内存/桩对象验证 TDengine SQL 构造（如 `writer/tests/test_tdengine_writer.py::test_multi_table_packs_many_subtables_into_one_statement` 等），不连真实 TDengine。

### 4.3 `edge`

```powershell
# workdir = D:\python\device_alarm\data_watch
python -m pytest edge/tests -q -p no:cacheprovider
# 实测 => 43 passed in 4.39s
```

- **可跑：是**（系统 cp314）。
- 收集：`43 tests collected in 0.14s`。
- **外部服务需求：无**。`edge/tests/conftest.py` 存在；各用例用 `OutboxStore(tmp_path/"edge.db")` 临时 SQLite 与假 HTTP 传输，不连 Redis/MySQL/TDengine。

### 4.4 `ingest` —— ⚠️ 无单一解释器可跑通全量（**未确认**完整可跑）

三次实测，全部失败或部分失败：

```powershell
# (a) 系统 cp314，排除 test_http.py
python -m pytest ingest/tests -q -p no:cacheprovider --ignore=ingest/tests/test_http.py
# 实测 => 1 failed, 50 passed, 5 skipped in 0.28s
#   失败：ingest/tests/test_repository.py::test_parent_schema_preflight_marks_workers_ready
#         ImportError: TurboAPI requires a free-threading Python build (no-GIL). Current: Python 3.14.6 (GIL enabled)

# (b) 3.14t + .py314t-site
$env:PYTHONPATH='D:\python\device_alarm\data_watch\backend\.py314t-site'
& 'D:\Program Files\python\python3.14.6\python3.14t.exe' -m pytest ingest/tests -q
# 实测 => 4 errors during collection in 0.38s
#   ModuleNotFoundError: No module named 'msgspec'
#   （test_http.py / test_redis_store.py / test_redis_store_integration.py / test_service.py）

# (c) 3.14t + .py314t-site + cp314 用户 site-packages（试图补齐 msgspec）
$env:PYTHONPATH='...\backend\.py314t-site;C:\Users\myrh\AppData\Roaming\Python\Python314\site-packages'
& '...python3.14t.exe' -Xgil=0 -m pytest ingest/tests -q
# 实测 => 4 errors during collection in 0.31s
#   ModuleNotFoundError: No module named 'msgspec._core'
#   （msgspec 的 C 扩展是 cp314 轮子，free-threaded 解释器无法加载）

# (d) 单独跑 turboapi 依赖用例（3.14t + .py314t-site）
& '...python3.14t.exe' -Xgil=0 -m pytest ingest/tests/test_repository.py -q
# 实测 => 1 failed, 8 passed in 0.15s
#   失败：test_parent_schema_preflight_marks_workers_ready → 内部仍 import msgspec 而失败
#   注：同次运行的 stdout 显示 "✅ TurboAPI: Python 3.14.6 free-threading active"，即 TurboAPI 在 3.14t 下确实可用
```

**根因**：`ingest/requirements.txt` 同时要求 `turboapi>=1.0.27`（**只支持 free-threading**）与 `msgspec==0.21.1`（**当前只有 cp314 轮子**，位于 `C:\Users\myrh\AppData\Roaming\Python\Python314\site-packages`，而 free-threaded 的用户目录是 `Python314t`，故 3.14t 看不到）。二者在本机互相排斥。

**结论与建议（供后续 agent，需用户决策，本轮不擅自安装依赖）**：
- `ingest/tests/test_http.py`、`ingest/tests/test_repository.py::test_parent_schema_preflight_marks_workers_ready`：**当前无法运行，未确认**。
- 其余 ingest 用例可在 cp314 下运行（`50 passed, 5 skipped`）。
- 修复需二选一：(i) 在 `Python314t` 用户站点安装 free-threading 版 `msgspec`；或 (ii) 为 cp314 提供 free-threading 版 TurboAPI。属环境建设，**超出本轮只读范围**。
- **外部服务需求**：
  - MySQL：`ingest/tests/test_repository.py`、`test_migrations.py` 针对 MySQL 逻辑；默认配置指向现场栈（见 §4.5），**运行前必须设隔离 `MYSQL_*`**。
  - Redis：有守卫文件 `ingest/tests/test_redis_test_guard.py`（**确认存在**）+ `ingest/tests/redis_test_guard.py`。守卫规则（`ingest/tests/redis_test_guard.py:20-31`）：必须是 `redis/rediss`、**禁止 query/fragment**、主机须在 `{127.0.0.1, ::1, localhost}`（除非 `INGEST_TEST_REDIS_ALLOW_REMOTE`）、**DB 必须是非零且为数字**。文件头明示：「This guard is not proof of isolation. The fixture also requires an externally provisioned marker and never executes FLUSHDB or FLUSHALL.」（第 3-4 行）
  - 集成用例文件：`ingest/tests/test_redis_store_integration.py`（在 cp314 下被 skip）。

### 4.5 ⚠️ 安全前提：默认配置直指现场栈（所有 agent 必读）

**默认连接参数确实指向正在运行的现场栈，跑测试前必须显式覆盖：**

- `ingest/app/config.py:40-45`（`IngestSettings.from_env`）：
  ```python
  redis_url=os.getenv("INGEST_REDIS_URL", "redis://127.0.0.1:6379/0"),
  mysql_host=os.getenv("MYSQL_HOST", "127.0.0.1"),
  mysql_port=int(os.getenv("MYSQL_PORT", "3306")),
  mysql_user=os.getenv("MYSQL_USER", "root"),
  mysql_password=os.getenv("MYSQL_PASSWORD", ""),
  mysql_database=os.getenv("MYSQL_DATABASE", "device_watch"),
  ```
- `backend/app/core/config.py:168-169`：
  ```python
  mysql_user=str(read_setting("MYSQL_USER", mysql_defaults.get("user", "root"))),
  mysql_password=str(read_setting("MYSQL_PASSWORD", mysql_defaults.get("password", ""))),
  ```

即 `root` + **空密码** + 库 `device_watch` + Redis `6379/0` = **现场栈**。

**隔离环境（据 `docs\整改记录\隔离测试环境.md` §2）**：Redis `127.0.0.1:16379`（密码 `TESTONLY_sentinel_redis_pw`）；MySQL `127.0.0.1:13306`（`root` / `TESTONLY_sentinel_root_pw` / 库 `device_watch_test`）。**只读观察**（`docker ps`、端口绑定）允许；**严禁** `stop/rm/down/restart/prune/重建`，严禁对现场栈写操作。

**本轮实际执行的安全性核验**：我在 4.1 跑的 `collector` 全量套件**未触碰现场栈**，依据是三条硬守卫（§4.1 末）——所有真实 Redis 用例都因缺少 `DW_CENTRAL_TEST_REDIS_URL` / `DW_TEST_REDIS_URL` / `DATAWATCH_TEST_REDIS_URL` 而 skip（计入 26 skipped），且 `_redis_target()` 额外断言端口 ≠ 6379。`writer`/`edge`/`cli`/`agent`/`backend` 套件均使用桩对象与 `tmp_path`，无外部服务依赖（backend/ingest 的 MySQL 用例在未设 `RUN_DB_INTEGRATION` / 未提供隔离连接时不会写入）。

### 4.6 `agent`

```powershell
# workdir = D:\python\device_alarm\data_watch
$env:PYTHONPATH = 'D:\python\device_alarm\data_watch\agent\.venv-pack\Lib\site-packages'
python -m pytest agent/tests -q -p no:cacheprovider
# 实测 => 253 passed, 1 skipped, 1 warning in 46.40s
```

- **可跑：是**（系统 cp314 + agent venv 依赖目录）。
- 若不注入 `PYTHONPATH`：`ImportError while loading conftest 'agent\tests\conftest.py'` → `app\services\process_manager.py:12: import psutil` → `ModuleNotFoundError: No module named 'psutil'`。psutil 仅存在于 `agent\.venv-pack\Lib\site-packages\psutil`（实测无全局安装，两个解释器都 `No module named 'psutil'`）。
- 直接使用 `agent\.venv-pack\Scripts\python.exe` **不行**：它有 psutil 但 `No module named pytest`。
- 配置：`agent\pytest.ini` → `asyncio_mode = auto`、`testpaths = tests`。
- **外部服务需求：无（默认）**。可选外部依赖通过环境变量显式开启：`agent/tests/test_driver_lifecycle.py:16` 与 `test_driver_signatures.py:83` 使用 `DATAWATCH_TEST_COLLECTOR_EXE`（未设则走「missing.exe」路径）。无 Redis/MySQL/TDengine 依赖。
- 1 skipped 为 `test_driver_install.py` 相关（依赖外部 exe 的一条）。

### 4.7 `backend`

```powershell
# workdir = D:\python\device_alarm\data_watch\backend
$env:PYTHONPATH = 'D:\python\device_alarm\data_watch\backend\.py314t-site'
& 'D:\Program Files\python\python3.14.6\python3.14t.exe' -Xgil=0 -m pytest app/tests -q -p no:cacheprovider
# 实测 => 204 passed, 6 subtests passed in 4.21s
```

- **可跑：是**（3.14t + 依赖目录 + `-Xgil=0`）。
- 反例 1（不加 `-Xgil=0`）：`1 failed, 203 passed, 6 subtests passed` —— 失败 `app\tests\test_free_threading.py::test_backend_import_does_not_enable_gil`（`app\tests\test_free_threading.py:14`）。
- 反例 2（用系统 cp314）：`26 errors during collection, 60 tests collected`（TurboAPI 拒绝 GIL 版解释器）。
- 测试目录为 `backend/app/tests/`（**不是** `backend/tests/`），共 40 个 `test_*.py`。
- **外部服务需求：无（默认）**。用例普遍使用桩/假客户端；涉及 TDengine 的用例为探测/日志类（如 `test_history_tdengine_probe.py`、`test_tdengine_query_budget.py`）。若后续需要真实 MySQL/TDengine，**必须先设 §4.5 的隔离 `MYSQL_*` / `TDENGINE_*`**。

### 4.8 `cli`

```powershell
# workdir = D:\python\device_alarm\data_watch   ← 必须在仓库根，否则报 No module named 'cli'
python -m pytest cli/tests -q -p no:cacheprovider
# 实测 => 98 passed in 2.23s
```

- **可跑：是**（系统 cp314，**cwd 必须是仓库根**）。
- 反例：workdir 设为 `cli\` 时 → `22 errors during collection`，全部为 `ModuleNotFoundError: No module named 'cli'`（如 `cli\tests\test_client.py:8: from cli.client import BackendClient`）。`cli` 是顶层包，需仓库根在 `sys.path`（`python -m pytest` 会把 cwd 入 path）。
- **外部服务需求：无**。全部为 HTTP 客户端桩测试。

### 4.9 `frontend`

```powershell
# workdir = D:\python\device_alarm\data_watch\frontend
npm test      # = tsx --test tests/*.test.ts
# 实测 => ℹ tests 27 / ℹ pass 27 / ℹ fail 0 / ℹ duration_ms 2047.85

npm run build # = vue-tsc --noEmit && vite build
# 实测 => exit=0, ✓ built in 34.76s
```

- **前端确实有测试框架**（与任务书「预计没有」的预期相反）：Node 内置 test runner，经 `tsx` 运行；`frontend/tests/` 下 10 个 `*.test.ts`（`dashboard-loads`、`device-pagination`、`history-zoom`、`http-error-classification`、`import-limits`、`import-workbook`、`monitor-light`、`monitor-refresh-guard`、`password-change-flow`、`password-contract`）。
- 类型检查与构建同一条命令：`npm run build` = `vue-tsc --noEmit && vite build`（`frontend/package.json` scripts）。另有 `npm run lint` = `eslint`。
- 环境：Node `v24.18.0`；实测 `Test-Path frontend\node_modules` → **False**，但 `npm test` 与 `npm run build` 仍成功（依赖由 npm 全局/上级解析提供）。
- ⚠️ **本轮副作用披露**：执行 `npm run build` 生成了构建产物 `frontend\dist\`（该目录被 gitignore，`git status` 无新增改动）。除本报告外，这是本轮唯一的文件系统写入，**未触碰任何源码/配置/测试文件**。

### 4.10 汇总表（模块 → 解释器 → 命令 → 实测结果）

| 模块 | 解释器 | 实测命令（workdir 见备注） | 实测结果 | 可跑 |
|---|---|---|---|---|
| `cli` | 系统 `python.exe` (cp314) | `python -m pytest cli/tests -q`（仓库根） | **98 passed**，2.23s | ✅ |
| `collector` | `collector\.venv-pack\Scripts\python.exe` (cp314t) | `...\python.exe -m pytest collector/tests -q`（仓库根） | **619 passed, 26 skipped, 1 failed**，131.94s | ✅（1 既有失败） |
| `collector`（错误解释器对照） | 系统 `python.exe` (cp314) | `--ignore=collector/tests/test_opcua_driver.py` | 81 failed / 538 passed / 26 skipped | ❌ |
| `writer` | 系统 `python.exe` (cp314) | `python -m pytest writer/tests -q`（仓库根） | **184 passed**，1.01s | ✅ |
| `edge` | 系统 `python.exe` (cp314) | `python -m pytest edge/tests -q`（仓库根） | **43 passed**，4.39s | ✅ |
| `ingest` | **分裂**：cp314 与 3.14t 各跑一部分 | 见 §4.4 (a)–(d) | cp314: 50 passed/1 failed/5 skipped；3.14t: 4 collection errors | ⚠️ **部分，全量未确认** |
| `agent` | 系统 `python.exe` + `PYTHONPATH=agent\.venv-pack\Lib\site-packages` | `python -m pytest agent/tests -q`（仓库根） | **253 passed, 1 skipped**，46.40s | ✅ |
| `backend` | `python3.14t.exe` + `PYTHONPATH=backend\.py314t-site` + `-Xgil=0` | `...python3.14t.exe -Xgil=0 -m pytest app/tests -q`（`backend\`） | **204 passed, 6 subtests passed**，4.21s | ✅ |
| `frontend`（测试） | Node v24.18.0 | `npm test`（`frontend\`） | **27 pass / 0 fail**，2.05s | ✅ |
| `frontend`（类型检查+构建） | Node v24.18.0 | `npm run build`（`frontend\`） | **exit=0**，34.76s | ✅ |

---

## 5. 阻塞与不确定点（诚实清单）

| # | 事项 | 状态 | 试过的命令 / 依据 |
|---|---|---|---|
| 1 | `ingest` 全量测试无法在任一解释器下跑通 | **未确认** | §4.4 (a)(b)(c)(d) 四条命令，根因：TurboAPI 需 free-threading，msgspec 仅 cp314 轮子 |
| 2 | `ingest/tests/test_http.py` 无法收集 | **未确认** | cp314 → TurboAPI ImportError；3.14t → msgspec ModuleNotFoundError |
| 3 | `ingest/tests/test_repository.py::test_parent_schema_preflight_marks_workers_ready` 失败 | **失败（环境）** | 3.14t 下 `1 failed, 8 passed`；内部 `import msgspec` 失败 |
| 4 | `collector/tests/test_collector_receipt_v2.py::test_outbox_owner_lock_rejects_second_process_and_crash_releases` 失败 | **既有失败** | 与 `docs\06-验证记录\2026-09\performance-verification-recheck-20260926.md:308` 记录的既有失败集合一致，非本轮引入 |
| 5 | `collector/tests/test_opcua_driver.py` 收集错误 | **既有** | `ImportError: cannot import name 'x509' from 'cryptography.hazmat.bindings._rust'`（vendor cryptography 与运行时不匹配）；历史文档同样 `--ignore` 它 |
| 6 | `ingest/Dockerfile` 用 `python:3.14-slim`（GIL 版）而 Ingest 依赖 TurboAPI（需 free-threading） | **未确认能否在容器内正常运行** | 只读发现（`ingest/Dockerfile:1` vs `ingest/requirements.txt`）。未构建/未运行容器（禁止重建业务容器）。建议后续批次核实 |
| 7 | `docs\02-模块设计\shared\StreamEnvelope共享契约.md`、`docs\02-模块设计\backend\分布式节点控制面实现说明.md` 各 1 处 Outbox 命中未逐句判读 | **未确认** | 低风险（各 1 处），本轮时间盒内未展开 |
| 8 | `docs\06-验证记录\2026-09\` 下 13 个旧中心 Durable 记录是否需逐条加「Superseded」横幅 | **待用户决策** | D39 处理方式仅要求「旧性能报告注明适用路径」，未授权批量改写 |

### 本轮未做的写入（遵守只读约束）

- 未修改/创建/删除任何源码、配置、测试文件。
- 唯一例外与副作用：`npm run build` 生成了 `frontend\dist\` 构建产物（gitignored，不影响 git 状态）。除此之外仅写入本报告。
- 未执行任何 `git` 写操作（无 commit/push/stash/checkout/reset/clean/rebase）；未触碰 4 条 stash。
- 未连接现场 Redis/MySQL/TDengine，未访问真实 PLC，未对任何容器执行操作。
