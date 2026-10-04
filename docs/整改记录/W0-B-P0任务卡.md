# W0-B P0 任务卡（批次 A：B5 / B6 / B7 / B11 / B15 / B25 / B41 / S3）

- **执行 HEAD**：`6845e800990f8352f46655036741bd79ca25eed1`（`git rev-parse HEAD` 实测，与清单文档基线一致）
- **分支**：`main`；工作区仅清单文档未跟踪（`git status --porcelain` 实测）
- **本文件性质**：只读定位 + 复现设计。**未修改任何源码、配置、测试文件。**
- **行号来源**：全部为 read/grep 工具在当前 HEAD 上**实际读到的行号**。
  ⚠️ 不要用 `Get-Content | Measure-Object -Line` 复核行号——本机实测它会给出偏小的行数（例如 `cli/tests/test_update_manager.py` 实测 96 行，Measure-Object 报 72）。以 read 工具与 `Select-String` 的行号为准，两者一致。
- **环境事实**（本机实测，与 `隔离测试环境.md` §4 一致，务必遵守）：本机有**正在运行的现场栈**（`data-watch-redis` :6379、`data-watch-mysql` :3306、含业务数据）。`ingest/app/config.py:40-45`、`backend/app/core/config.py:168-169` 默认即指向现场栈。本卡所有复现设计均**不需要**连现场栈；需要库的条目一律写「纯函数/单元（temp SQLite 或 fake）」，需要隔离库的明确标注 16379/13306 + 哨兵凭据。

## 复现所需环境总览

| 条目 | 是否需 Redis | 是否需 MySQL | 是否需 Docker | 是否需文件系统/权限 | 是否需真实 PLC |
|---|---|---|---|---|---|
| B5 | 否（纯单元） | 否 | 否 | temp 目录（SQLite） | 否 |
| B6 | 否（纯单元） | 否 | 否 | temp 目录（SQLite） | 否 |
| B7 | 否（纯单元可复现） | 否（集成级才需 13306） | 否 | temp | 否 |
| B11 | 否（本机假监听器即可） | 否 | 否 | 需 DPAPI + 回环端口 | 否 |
| B15 | 否 | 否 | 否 | 需文件系统（config.toml） | 否 |
| B25 | 否（纯单元） | 否 | 否 | temp 目录 + 本地文件锁 | 否 |
| B41 | 否 | 否 | 否 | 需真实 PowerShell 子进程（temp 内即可，不需管理员） | 否 |
| S3 | 否 | 否 | 否 | 需回环端口 | 否 |

**真实 PLC 一律不允许**，本卡无任何条目需要它。

---

# B5｜同批已 ACK 帧因后续 session 异常被再次迁移重传

## 1. 真实代码位置

| 文件 : 函数/类 : 实际行号 | 作用 |
|---|---|
| `edge/app/uploader.py:223-394` : `Uploader._upload_records` | 单个批次的 ACK 解析与终态落库 |
| `edge/app/uploader.py:307` | `completions: list[OutboxCompletion] = []` —— **确认结果只在内存里累积** |
| `edge/app/uploader.py:308-339` | 遍历 records：`ack`/`duplicate`/`stale_config_warning` → 追加 `"sent"`（321-325）；`rejected_consumed` → 追加 `"dead"`（326-339） |
| `edge/app/uploader.py:340-360` | **`seq_reject`/`commit_unknown` 分支**：347-352 对**整个 records**（不是仅未确认项）调 `_retry_records`；359 `await self._abandon_and_switch()`；360 `return False` —— **307 起累积的 `completions` 被直接丢弃，从未落库** |
| `edge/app/uploader.py:361-375` | `session_obsolete` 分支：371/375 `return False` —— 同样丢弃已累积的 completions |
| `edge/app/uploader.py:384-388` | `complete_batch(completions, lease_owner)` —— **只有 for 循环正常走完才会执行** |
| `edge/app/uploader.py:460-502` : `Uploader._retry_records` | 对传入的 records 全部 `mark_retry_batch`，或超限时 `complete_batch(..., "dead")`（493-501） |
| `edge/app/uploader.py:504-506` : `_abandon_and_switch` | `prepare_session_switch()` → `_complete_session_switch()` |
| `edge/app/outbox.py:542-602` : `OutboxStore.prepare_session_switch` | 570-577 选取**所有** `state IN ('pending','retry_wait','inflight')` 的行；581-597 重写 `upload_session_id` 并重排 `local_seq`。**没有排除"已 ACK"的行；`attempt_count` 也不重置** |
| `edge/app/outbox.py:616-626` : `complete_session_switch` | 清除 pending 标记 |
| `ingest/app/redis_store.py:68-74` | `INGEST_BATCH_LUA` 会话高水位：`local_seq <= hw` → `"duplicate"`；`local_seq > hw+1` → `"seq_reject"` + `break` |
| `ingest/app/service.py:56-65` : `IngestService.ingest` | 服务端组装 `results`，命中 `seq_reject`/`commit_unknown`/`session_obsolete` 即 `break` → 响应 = 「已 ACK 的前缀 + 出错那一项」 |
| `ingest/app/schemas.py:150` | `data["durability"] = "redis-aof"`（`edge/app/uploader.py:322` 用它判耐久） |

**与文档行号的差异**：文档 B5（清单 203-215 行）**未给出任何行号**，只给了文件名与函数名（`edge/app/uploader.py`、`edge/app/outbox.py`、`ingest/app/redis_store.py`），这些**都准确**。差异在于**遗漏**：文档说 outbox 侧是 `complete_batch / complete_session_switch`，但真正的迁移逻辑在 **`prepare_session_switch`（outbox.py:542-602）**，`complete_batch` 只是接收方。

## 2. 当前代码事实

```python
# edge/app/uploader.py:307-388（节选，行号即真实行号）
307  completions: list[OutboxCompletion] = []
308  for record in records:
      ...
321      if status in {"ack", "duplicate", "stale_config_warning"}:
325          completions.append(OutboxCompletion(record, "sent"))
326      elif status == "rejected_consumed":
333          completions.append(OutboxCompletion(record, "dead", ...))
340      elif status in {"seq_reject", "commit_unknown"}:
347          quarantined = await self._retry_records(
348              records,          # ← 整个批次，含上面已确认 "sent" 的项
349              status, 200, quarantine_after_max=True,
350          )
359          await self._abandon_and_switch()
360          return False          # ← completions 在此被丢弃
      ...
384  await self._store_call(self._store.complete_batch, completions, self._lease_owner)
```

```python
# edge/app/outbox.py:570-597（节选）
570  pending_rows = connection.execute(
574      "FROM outbox WHERE state IN ('pending', 'retry_wait', 'inflight') ORDER BY local_seq"
577  ).fetchall()
581  for local_seq, row in enumerate(pending_rows, start=1):
582      connection.execute("UPDATE outbox SET upload_session_id=?, local_seq=?, state='pending', ...")
```

## 3. 原判断是否仍成立

**成立（已实证，非推测）**。

依据 —— 我实际运行了一个最小复现（temp SQLite + `httpx.MockTransport`，不碰现场栈）：批次 2 条，服务端对 `local_seq=1` 返回 `ack`+`durability=redis-aof`、对 `local_seq=2` 返回 `seq_reject`。实测输出：

```
stats: {'pending': 2} | session switched: True
  row {'local_seq': 1, 'state': 'pending', 'attempt_count': 1, upload_session_id': <新会话>} | new_session: True
  row {'local_seq': 2, 'state': 'pending', 'attempt_count': 1, upload_session_id': <新会话>} | new_session: True
```

`local_seq=1` **已被中心 ACK**，却仍被 `mark_retry_batch` 加一次 `attempt_count`，再被 `prepare_session_switch` 迁到新会话并 `state='pending'` → 必然重传。这就是「同批已 ACK 帧因后续 session 异常被再次迁移重传」。

补充证据：既有测试 `edge/tests/test_uploader.py:147-192`（`test_seq_reject_switches_session_without_discarding_backlog`）**已经把当前（有缺陷的）行为固化成断言**（183 行 `assert store.stats() == {"pending": 2}`、188-189 行断言两条都在新会话）。该用例的 mock 只对 `items[0]` 返回 `seq_reject`，没有构造「ACK 前缀 + 后续异常」的混合响应，所以它没暴露 B5，反而会阻碍修复。

## 4. 最小反例 / 复现设计

- **注入的故障**：服务端响应改为「混合 ACK」——同一批次里 `local_seq=1` 返回 `{"status":"ack","durability":"redis-aof"}`，`local_seq=2` 返回 `{"status":"seq_reject"}`。
- **输入**：`OutboxStore(tmp/"edge.db")` + `configure_assignment("QC100",7,100,1)` + `append_envelope(...)` 两条；`session_switch` 路径返回 `{"status":"switched"}`。
- **调用**：`await Uploader(store, settings, client=<MockTransport>, signer=FakeSigner()).run_cycle()`。
- **判定复现成功的现象**：`local_seq=1`（已被 ACK 那条）最终 `state != 'sent'`，且其 `upload_session_id` 等于切换后的新会话 ID（即被迁移、会被重传）。
- **修复后的期望**：`local_seq=1` 为 `sent` 且保留**旧**会话 ID；只有 `local_seq=2` 进入新会话（被重排为 `local_seq=1`）。
- **最合适的现有测试文件（已确认存在）**：`edge/tests/test_uploader.py`（335 行）。建议直接改造 `test_seq_reject_switches_session_without_discarding_backlog`（147-192 行）为混合 ACK 版本；另可扩展 `edge/tests/test_session_reconcile.py`（19 行，存在）。
- **环境**：纯单元（temp SQLite + httpx MockTransport）。不需要 Redis/MySQL/Docker/PLC。

## 5. 最小修改候选方向（不改，仅描述）

在 `edge/app/uploader.py:340-375` 两个「不确定」分支里，**先落库已确定的 completions**（调用 `self._store.complete_batch(completions, self._lease_owner)` 并清空列表），再执行 `_retry_records` / `_abandon_and_switch`。同时需评估 `_retry_records`（460-502）目前接收「整个批次」，应改为只处理未确定项。第二处候选：`edge/app/outbox.py:570-577` 的 `pending_rows` 查询是否需要显式排除 `sent`/`dead`（当前靠状态集合天然排除，但一旦上游改动就可能回归）。

## 6. 验收要求映射（文档 215 行 → 可执行断言）

1. 「混合 ACK+commit_unknown」→ 断言 ACK 项 `state='sent'`，未知项被重试/迁移，两者身份（`batch_id`+旧会话 ID）不混。
2. 「ACK 应答丢失」→ 断言未收到 ACK 的项仍在本地（`state IN ('pending','retry_wait','inflight')`），且重放走既有 nonce/`duplicate` 语义。
3. 「确认后本地崩溃」→ 在 `complete_batch` 前注入异常，断言重启后记录仍为未确认且可重放（不得变 `sent`）。
4. 「确定 ACK 的项不再作为未确认行迁移」→ 断言 `sent` 行的 `upload_session_id` 保持不变。
5. 「未确认载荷保全」→ 断言 `stats()` 中各状态计数之和 == 写入条数（数据守恒）。

## 7. 关联与冲突

同文件条目：**B6**（同 `edge/app/outbox.py` 与 `edge/app/uploader.py`）、**B25**（同 `edge/app/uploader.py`、`edge/uploader_main.py`）。三者同为热点文件 `edge/app/outbox.py` + `edge/app/uploader.py`（见 `README.md` 热点表，归属 W1-E1）→ **B5/B6/B25 不能并行改同一文件**，须串行或由同一 agent 一起改。另需注意 B5 修复会**证伪** `edge/tests/test_uploader.py:183` 的既有断言。

## 8. 本地可跑性（实际运行过）

- 命令：`python -m pytest edge/tests/test_uploader.py edge/tests/test_outbox.py edge/tests/test_session_reconcile.py edge/tests/test_storage_service.py edge/tests/test_uploader_main.py -q`
- **实测结果：29 passed, 0 failed（3.16s）**。解释器为本机 `python`（3.14.6 GIL build）。
- 证据等级：**单元**。

---

# B6｜网络重试与永久失败隔离共用 attempt_count

## 1. 真实代码位置

| 文件 : 函数/类 : 实际行号 | 作用 |
|---|---|
| `edge/app/outbox.py:33` | 表定义 `attempt_count INTEGER NOT NULL DEFAULT 0` —— **全表只有一个计数器** |
| `edge/app/outbox.py:496-540` : `mark_retry_batch` | **513** `attempt_count=attempt_count + 1`，对**任何**调用原因都自增；只额外记录 `last_error`（514）/`http_status`（516） |
| `edge/app/uploader.py:46` | `max_attempts: int = 10` |
| `edge/app/uploader.py:40` | `max_batch_items: int = 500`（文档"500 条"的来源） |
| `edge/app/uploader.py:44-45` | `retry_base_ms=500`、`retry_max_ms=30_000`（文档"两分钟"的来源） |
| `edge/app/uploader.py:460-502` : `_retry_records` | **471-474** 隔离判定：`quarantine_after_max and (record.attempt_count + 1 >= max_attempts)` → `dead`；479-482 退避 `min(base * 2**attempt_count, retry_max_ms)` |
| `edge/app/uploader.py:268-273` | `quarantine_after_max=True`（HTTP 400 契约拒绝） |
| `edge/app/uploader.py:347-352` | `quarantine_after_max=True`（`seq_reject`/`commit_unknown`） |
| `edge/app/uploader.py:241, 260, 284, 304, 318, 374, 382` | 传输类失败（网络异常、429/5xx、畸形 ACK、缺失 ACK、未知状态、session_obsolete）走 `_retry_records` 但**不带** `quarantine_after_max` —— 仍会自增同一个 `attempt_count` |
| `edge/app/outbox.py:635-721` : `maintenance` | **649-656** `DELETE FROM outbox WHERE (state='sent' AND acked_at < sent_cutoff) OR (state='dead' AND dead_at < retention_cutoff)` —— dead 延迟清理 |
| `edge/app/outbox.py:581-597` | `prepare_session_switch` 重排 session 时**不重置** `attempt_count` |

**与文档行号的差异**：文档 B6（清单 217-229 行）同样**未给行号**，函数名准确。需强调文档未点明的关键事实：`mark_retry_batch` 的**唯一**调用方语义由调用点决定，而调用点用同一个 `attempt_count` 同时承担「传输尝试计数」和「坏数据隔离预算」。

## 2. 当前代码事实

```python
# edge/app/uploader.py:485-502（节选）
485  if retries:
486      await self._store_call(self._store.mark_retry_batch, retries, self._lease_owner,
490                             error=error, http_status=http_status)
493  if quarantined:
494      await self._store_call(self._store.complete_batch,
496          [OutboxCompletion(record, "dead", error) for record in quarantined], ...)
```
```python
# edge/app/outbox.py:509-519（节选）
510  UPDATE outbox SET state='retry_wait',
513      attempt_count=attempt_count + 1,
514      next_retry_at=?, last_error=?, http_status=?, ...
```

## 3. 原判断是否仍成立

**成立（已实证）**。文档三条子判断逐条核对：

1. 「计数污染成立」→ **成立**，实测：连续两次 503 后 `attempt_count` 由 0→1→2；把该项置为 9（模拟长期断网历史）后，**单次 HTTP 400** 即把它判 `dead`。实测输出：
   ```
   after 503 cycle 1: state=retry_wait attempt_count=1
   after 503 cycle 2: state=retry_wait attempt_count=2
   after ONE HTTP 400 with prior transport attempt_count=9 -> {'dead': 1}
   row: {'state': 'dead', 'last_error': 'http_400_contract_rejected', 'http_status': 503}
   ```
   注意最后一行 `http_status=503`：**dead 行留下的是历史传输失败的状态码**，说明两类失败共用同一字段，取证会误导。
2. 「"两分钟/500 条"不是固定结果」→ **成立**，且可解释来源：`max_attempts=10` + `retry_max_ms=30_000` 使退避总和约 `0.5+1+2+4+8+16+30×4 = 151.5s ≈ 2.5min`，`max_batch_items=500`。但实际耗时取决于 claim 节奏与负载，**不是固定结果**。
3. 「dead 延迟清理仍可能造成不可恢复的丢失」→ **成立（代码级）**：`outbox.py:651-656` 对 `dead` 行按 `retention_days`（默认 7，`uploader.py:47`）执行 `DELETE`；`edge/app/uploader.py:172-176` 每次维护调用都会执行。未运行崩溃/清理实验，故标为代码级确认。

## 4. 最小反例 / 复现设计

- **注入的故障**：(a) 连续 N 次 HTTP 503（传输失败）；(b) 随后一次 HTTP 400（契约拒绝）。为免除等待退避，每轮后直接 `UPDATE outbox SET next_retry_at=0`。
- **输入**：`OutboxStore(tmp/"edge.db")` + seed 1 条。
- **判定复现成功的现象**：在「历史传输失败已把 `attempt_count` 推到 9」之后，**一次** HTTP 400 就使该行 `state='dead'`，而该行载荷本身从未被中心判定为非法。
- **修复后的期望**：传输失败不消耗隔离预算 → 同一输入下该项应进入 `retry_wait`（或按新的分类计数），而非 `dead`。
- **最合适的现有测试文件（已确认存在）**：`edge/tests/test_uploader.py`（已有 `test_server_error_retries_same_session:104`、`test_network_error_is_logged_and_retried:124`）；`edge/tests/test_outbox.py`（已有 `test_maintenance_keeps_dead_for_retention_but_removes_sent_after_sixty_seconds`）。
- **环境**：纯单元（temp SQLite + MockTransport）。不需要 Redis/MySQL/Docker/PLC。

## 5. 最小修改候选方向（不改）

`edge/app/outbox.py`：为「传输尝试」与「协议/数据拒绝」拆出独立列（例如新增 `transport_attempt_count` / `dead_candidate_count`，或让 `mark_retry_batch` 接受 `counts_toward_quarantine: bool`）。`edge/app/uploader.py:471-474` 的判定改为只读隔离计数器。`outbox.py:635-721` 的 dead 保留/删除规则需与人工恢复路径一起明确（文档要求「不自动扩大删除权限」）。

## 6. 验收要求映射（文档 229 行 → 断言）

1. 「长时间断网后恢复再遇一次序列异常，好数据不能因历史网络次数被判 dead」→ 断言：N 次 503 后接一次 `seq_reject`，该行**不**为 `dead`。
2. 「真正非法项有独立证据」→ 断言 `dead` 行的判定依据来自协议/校验拒绝，且记录中不含残留的传输 `http_status`。
3. 「检查延迟清理前后的可恢复性」→ 在 `maintenance(retention_days=...)` 前后断言 `dead` 行在保留期内始终可读、可人工重放。

## 7. 关联与冲突

**B5、B25**（同 `edge/app/outbox.py` + `edge/app/uploader.py`，同为热点文件，W1-E1）。注意 B6 的计数器改动会**改变 B5 复现用例的 `attempt_count` 观察值**（B5 复现里我观察到 ACK 项 `attempt_count=1`）→ B5、B6 一起改时须同步更新断言。同受影响的既有断言：`edge/tests/test_uploader.py:117`、`edge/tests/test_outbox.py:222` 起的 maintenance 用例。

## 8. 本地可跑性（实际运行过）

- 命令同 B5（含 `edge/tests/test_outbox.py`）；**实测 29 passed, 0 failed**。
- 证据等级：**单元**。

---

# B7｜新点位/配置已发布而 Ingest 缓存仍旧，导致永久拒收

## 1. 真实代码位置

**控制面（backend）**

| 文件 : 函数 : 实际行号 | 作用 |
|---|---|
| `backend/app/services/catalog.py:2061-2068` : `_bump_assigned_node_config_versions` | `UPDATE ... SET config_version = config_version + 1`（2068）。调用点：**686**、**730**、**2059** |
| `backend/app/services/catalog.py:2019, 2036, 2052, 2243` | 点位/分组相关的 `config_version` 自增 |
| `backend/app/api/routes/distributed_nodes.py:22` | `_CONTROL_INVALIDATION_CHANNEL = "ingest:control:invalidate"` |
| `backend/app/api/routes/distributed_nodes.py:227-235` : `_publish_control_invalidation` | 发布失效通知；**234-235 吞掉所有异常**（只 `logger.exception`）→ 通知丢失静默 |
| `backend/app/api/routes/distributed_nodes.py:70-72, 122, 155-157, 193` | 仅有的 4 个发布点，payload 的 `scope` 只出现 `"node"` 与 `"assignments"` |
| `backend/app/api/routes/distributed_nodes.py:161-174` : `update_node_settings` | 改节点设置后**不发布**任何失效 |
| `backend/app/services/catalog.py` 全局 | **grep `invalidate|publish|_CONTROL_INVALIDATION` 未命中任何一处** → 点位/版本变更路径**完全不发失效通知** |

**数据面（ingest）**

| 文件 : 函数 : 实际行号 | 作用 |
|---|---|
| `ingest/app/repository.py:53-70` : `MySQLIngestRepository.__init__` | 三个缓存：`_public_key_cache`(64)、`_assignment_cache`(65-67)、`_mapping_cache`(68-70)；**58** `control_cache_ttl_seconds: float = 1.0` |
| `ingest/app/repository.py:99` | `control_cache_ttl_seconds=settings.control_cache_ttl_ms / 1000` 覆盖默认 |
| `ingest/app/config.py:30` | `control_cache_ttl_ms: int = 5000`（76-77 校验 >0） |
| `ingest/app/repository.py:303-306` : `_cache_expiry` | `monotonic() + ttl * (0.8~1.2)` 抖动 |
| `ingest/app/repository.py:108-126` : `invalidate_control_cache` | `scope="node"`(115-121) 清公钥，且**仅当** `event.get("assignments")` 为真才清 `_assignment_cache`；**从不清 `_mapping_cache`**。`_mapping_cache` 只在 `scope="all"`(110-114) 或 `scope="mappings"`(125-126) 下被清，而**生产端从不发这两个 scope** |
| `ingest/app/repository.py:237-267` : `_assignment_rows` / **269-301** : `_mapping_point_sets` | 缓存命中（`cached[0] > now`）即 `result[key] = ...` **直接返回，不查库**；**无 force/权威重读参数** |
| `ingest/app/repository.py:378-379` | `if mapped_point_ids.intersection(point_ids) != point_ids: return ValidationDecision.reject("malformed_envelope:unknown_var_id")` |
| `ingest/app/repository.py:381-383` | `if item.config_version > current_version: return ValidationDecision.reject("unknown_config_version")` |
| `ingest/app/repository.py:384-385` | `if item.config_version < current_version: return ValidationDecision.accept("stale_config_warning")` |
| `ingest/app/repository.py:34-40` : `ValidationDecision.reject` | 必定 `action="consume_reject"`、`status_override="rejected_consumed"` |
| `ingest/app/service.py:129-138` : `_resolve_status` | 驳回 → `rejected_consumed` |
| `ingest/app/main.py:75-76` | pub/sub 回调接到 `repository.invalidate_control_cache` |
| `edge/app/uploader.py:326-339` | `rejected_consumed` → `OutboxCompletion(record, "dead", ...)` |
| `edge/app/outbox.py:367` | `claim_batch` 只选 `state IN ('pending','retry_wait','inflight')` → **`dead` 永不重试** |

**与文档行号的差异**：文档 B7（清单 231-243 行）**未给行号**。文档列了 `backend/app/services/catalog.py` 与 `api/routes/catalog.py` 作为「点位事务/版本更新」位置——**文件正确，但结论不完整且会误导**：catalog 侧**根本没有任何缓存失效发布**，问题不止是「通知丢失」。文档称 `ingest/app/repository.py` 与 `service.py` 做「版本/永久映射校验」——正确（见上表 378-385）。

## 2. 当前代码事实

```python
# ingest/app/repository.py:368-386（节选）
368  if not row:
369      return ValidationDecision.reject("unknown_device")
...
378  if mapped_point_ids.intersection(point_ids) != point_ids:
379      return ValidationDecision.reject("malformed_envelope:unknown_var_id")
381  current_version = int(row.get("current_config_version") or 0)
382  if item.config_version > current_version:
383      return ValidationDecision.reject("unknown_config_version")
```
```python
# ingest/app/repository.py:115-121（节选）
115  if scope == "node":
116      node_uuid = str(event.get("node_uuid") or "")
117      if node_uuid:
118          self._public_key_cache.pop(node_uuid, None)
119      if event.get("assignments"):
120          self._assignment_cache.clear()
121      return          # ← 不触碰 _mapping_cache
```

## 3. 原判断是否仍成立

**成立，且比文档描述更严重。**

- 文档子判断「缓存窗口竞态成立」→ **成立**：缓存命中路径（`repository.py:244-246`、`276-278`）不与任何版本比对，只按 TTL 过期。
- 文档子判断「窗口结束不代表窗口内被判 dead 的帧自动恢复」→ **成立（代码级）**：`rejected_consumed` → `edge/app/uploader.py:326-339` → `dead`；`edge/app/outbox.py:367` 使 `dead` 永不被 claim。TTL 过后缓存虽会刷新，但已判 dead 的帧不会回来。
- **超出文档的发现**：失效通知的发布点只有 `distributed_nodes.py` 的 4 处，`scope` 只用 `node`/`assignments`；**`_mapping_cache` 在生产中永远不会被显式失效**（`scope="mappings"` 无人发布），而新点位映射变化正是 B7 的场景之一 → 该缓存只能等 TTL（默认 `5000ms × 1.2 = 6s`）自然过期，期间的合法帧被判 `rejected_consumed`。
- 文档要求「不可先通知后提交」→ **成立**：`distributed_nodes.py` 各路由均先 `await Service(...)`（已提交）再 `await _publish_control_invalidation(...)`，顺序正确；但 `catalog.py` 路径**两者都没有**。
- **未确认**：我没有运行 ingest↔backend 的跨进程集成，因此「一次真实 publish→invalidate 的端到端时延」与「TTL 抖动 0.8~1.2 对竞态窗口的实际影响」未实测。

## 4. 最小反例 / 复现设计

**（A）纯单元复现（不需要 MySQL、不需要 Redis、不需要 Docker）**

既有测试 `ingest/tests/test_repository.py:154-195` 已示范了正确手法：`MySQLIngestRepository(None)` + `repository._fetchall = <fake>`。照此：

- **注入的故障**：让 fake `_fetchall` 在第一次调用时返回 `current_config_version=1`（旧缓存/旧版本），随后（模拟"控制面已发布新版本"）改为返回 `current_config_version=2`，但**不调用** `invalidate_control_cache`。
- **输入**：`item = _item(seq=1, device_code="QC100", point_id=1)` 且 `config_version=2`。
- **判定复现成功的现象**：第二次 `await repository.validate_items(node_uuid, [item])` 仍返回 `status_override == "rejected_consumed"`，且 fake `_fetchall` 的**调用次数没有增加**（证明走的是缓存命中路径，未做权威重读）。
- **同样的手法测映射缓存**：第一次让 `point_storage_mapping` 返回 `point_id=1`，第二次让库"返回"新增的 `point_id=2` 但不清缓存 → 仍 `rejected_consumed`（`repository.py:378-379`）。
- **修复后的期望**：驳回前对新版本/未知点位做一次受控权威重读 → 第二次应返回 `accept`（或至少不返回 `rejected_consumed`）。

**（B）发布侧复现（纯单元，零依赖）**

- `backend/app/tests/test_distributed_routes.py:11-26` 已示范用 monkeypatch 捕获 `redis.publish`。
- **判定现象**：对 `catalog` 的点位/版本变更入口（`backend/app/services/catalog.py` 的调用方）做同样捕获，断言 **`publish` 从未被调用** → 直接证明「控制面成功提交后没有统一发布缓存失效」。

- **最合适的现有测试文件（已确认存在）**：`ingest/tests/test_repository.py`（277 行，含 `test_control_invalidation_clears_requested_cache_scope:263`）、`ingest/tests/test_service.py`（181 行）、`backend/app/tests/test_distributed_routes.py`、`backend/app/tests/test_catalog_edge_revision.py`。
- **环境**：纯单元即可。若要「隔离集成」级证据（真实 MySQL 缓存失效）：必须显式设 `MYSQL_PORT=13306`、`MYSQL_DATABASE=device_watch_test`、哨兵密码（`隔离测试环境.md` §4），并**自带 MySQL 守卫**（该文件 §5.2：全仓库没有 MySQL 侧测试守卫）。**绝不使用默认 3306/`device_watch`。**

## 5. 最小修改候选方向（不改）

1. `backend/app/services/catalog.py`：在版本自增成功提交后统一触发失效发布（复用 `distributed_nodes._publish_control_invalidation`），并新增 `scope="mappings"` 的发布点。
2. `ingest/app/repository.py:108-126`：`scope="node"` 分支补上 `_mapping_cache` 的清理（或改为按事件粒度清）。
3. `ingest/app/repository.py:360-386`：在返回 `unknown_var_id` / `unknown_config_version` 之前增加一次**受控的权威重读**（对 miss 的 key 显式清缓存后重查），仅对该次判定生效。
4. `backend/app/api/routes/distributed_nodes.py:227-235`：异常不能只记日志——至少要有可观测计数，供「处理通知丢失」判定。

## 6. 验收要求映射（文档 243 行 → 断言）

1. 「人为保留旧缓存并立即上传新点」→ 断言合法帧**不**为 `rejected_consumed`。
2. 「修改后新版本」→ 断言 `config_version` 变更新帧被 `accept`，而非 `unknown_config_version`。
3. 「通知丢失」→ 断言即使 `publish` 抛异常/丢失，合法帧也不会被永久判死（即不依赖通知才能正确判定）。
4. 「多 worker 场景」→ 断言每个 worker 的缓存都能独立失效（`invalidate_control_cache` 是进程内 dict，注意多 worker 下 pub/sub 的送达）。
5. 「真正越权或非法点仍拒绝」→ 断言不存在于映射表的 `point_id`、`node_uuid` 不匹配的帧仍为 `rejected_consumed`。

## 7. 关联与冲突

热点文件 `backend/app/services/catalog.py`、`backend/app/api/routes/catalog.py`、`backend/app/api/routes/distributed_nodes.py`、`ingest/app/repository.py`、`ingest/app/service.py` —— 按 `README.md` 热点表**全部归属 W1-I1**，与 B5/B6/B25/B11/B15/B41/S3 **无文件重叠** → B7 可与其它条并行。

## 8. 本地可跑性（实际运行过）

- `python -m pytest ingest/tests/test_service.py -q` → **6 passed**。
- `python -m pytest ingest/tests/test_repository.py -q` → **1 failed, 8 passed**。
  - 失败项 `test_parent_schema_preflight_marks_workers_ready` **与 B7 无关**：该用例在 `ingest/tests/test_repository.py:57` `import ingest.main`，而 `ingest.main` 依赖 `turboapi`；本机 GIL 版 Python 3.14.6 下 turboapi 抛 `ImportError: TurboAPI requires a free-threading Python build`。
  - 换 free-threading 解释器 `python3.14t` 跑同一文件 → collection 阶段 `Windows fatal exception: access violation` 崩溃（实测）。
  - 两个解释器依赖互补缺失（实测 `importlib.util.find_spec`）：`python.exe`(GIL) 有 `msgspec`、缺 `turboapi`；`python3.14t` 有 `turboapi`、缺 `msgspec` → **该文件在本机无法完整跑通**。
- `ingest/tests/test_repository.py:32` 用 `monkeypatch.setattr(repository_module.aiomysql, "create_pool", ...)`（已核对），故上述运行**未连接现场 MySQL**，符合 `隔离测试环境.md` §5.4。
- 证据等级：**单元**（未做隔离集成）。

---

# B11｜Agent 健康检查把已有凭据用于可覆写的新目标

## 1. 真实代码位置

| 文件 : 函数 : 实际行号 | 作用 |
|---|---|
| `agent/app/routers/admin.py:358-379` : `health_check`（`POST /health/check`） | **361** `Depends(get_current_admin)`；**363** `cfg = load_config()`；**368** `target_cfg = dict(cfg.get(request.target) or {})` ← 取**已存盘**配置（含 `password_enc`）；**369-372** 用请求体逐键覆盖（`exclude_unset=True` + `if value is not None`）—— **host/port 可被覆盖，而 `password_enc` 原样保留**；**379** `await asyncio.to_thread(checkers[request.target], target_cfg)` |
| `agent/app/schemas/models.py:62-67` : `HealthCheckConfig` | `host/port/user/password/database` 全部 `Optional` |
| `agent/app/schemas/models.py:70-72` : `HealthCheckRequest` | `target: Literal["redis","mysql","tdengine"]`、`config: HealthCheckConfig \| None` |
| `agent/app/services/health_checker.py:17-20` : `_decrypt_password` | `password` 键存在且非 None → 用它；否则 `dpapi_decrypt(password_enc)` |
| `agent/app/services/health_checker.py:38-57` : `check_redis` | **41-48** 用 `config["host"]/["port"]` + 解密后的 password 建连接并 `ping()` |
| `agent/app/services/health_checker.py:60-85` : `check_mysql` | **63-73** 用解密后的 password 连 `config["host"]/["port"]` |
| `agent/app/services/health_checker.py:88-108` : `check_tdengine` | **95-96** `base64(user:password)`；**99** `url=f"http://{host}:{port}/rest/sql"`；**108** `urllib.request.urlopen(request, timeout=...)` |
| `agent/app/core/deps.py:35-57` : `get_current_admin` | 管理员 JWT 校验（B11 的前提条件） |
| `agent/app/routers/bootstrap.py:169-188` : `bootstrap_test_connection` | 同样调用这三个 checker，但**凭据由调用方显式传入**（176-182），且受「bootstrap 未完成」状态约束 |
| `agent/app/routers/viewer.py:39-63` : `viewer_health` | 读取存储配置（**未逐行通读，仅 grep 命中 50-52 行**） |

**与文档行号的差异**：文档 B11（清单 287-299 行）**未给行号**，所列文件 `agent/app/routers/admin.py`、`agent/app/services/health_checker.py` 与函数名**均准确**。文档遗漏了 `bootstrap.py:169-188` 与 `viewer.py:39-63` 这两个共用 checker 的调用点（前者显式传密码、后者不接收覆盖，风险不同）。

## 2. 当前代码事实

```python
# agent/app/routers/admin.py:363-379
363  cfg = load_config()
365  if not (cfg.get("agent") or {}).get("bootstrap_done", False):
366      raise HTTPException(status_code=400, detail="Bootstrap must be completed first")
368  target_cfg = dict(cfg.get(request.target) or {})      # 含 password_enc
369  if request.config is not None:
370      for key, value in request.config.model_dump(exclude_unset=True).items():
371          if value is not None:
372              target_cfg[key] = value                    # 只覆盖显式提供的键
374  checkers = {"redis": check_redis, "mysql": check_mysql, "tdengine": check_tdengine}
379  return await asyncio.to_thread(checkers[request.target], target_cfg)
```

## 3. 原判断是否仍成立

**成立（代码级确定）。**

- 文档子判断「有管理员身份及目标可达等前提的凭据使用边界问题成立」→ **成立**：请求只需提供 `host`/`port`，`password_enc` 便从磁盘沿用并被解密后发往该 host/port（`health_checker.py:17-20` → 41-48 / 63-73 / 95-96）。路由确需 `get_current_admin`（`admin.py:361`），与文档「有管理员身份…前提」一致。
- 文档子判断「不同数据库协议不都明文传输密码」→ **部分成立**：Redis AUTH 与 MySQL 握手（`mysql_native_password`/`caching_sha2_password`）非全明文；但 **TDengine 路径确为明文 Basic**（`health_checker.py:95-96`，base64 非加密），且走 `http://`。
- **我额外实测到的一条（文档未提，见「处理方式：防重定向泄漏」）**：`urllib.request` **会把 `Authorization` 头转发到 302 跳转后的另一个 host:port**。我用本机两个回环端口做了纯 stdlib 实验（一个返回 302、一个记录收到的头），实测 sink 收到 `Basic <base64(root:SENTINEL_PW)>`。含义：`check_tdengine`（`health_checker.py:98-108`）会把**存储的 TDengine 凭据**随重定向送给攻击者控制的主机，而目标 host/port 恰好可由 `HealthCheckConfig` 完全控制 → 这条比「沿用旧凭据」更容易被利用。
  - **诚实标注**：上述重定向转发是 **stdlib 行为实测**；「项目代码在真实调用路径下同样泄漏」我**未运行**（未启动 agent 服务），属**未确认**。
- **未确认**：空/无密码场景、TLS、非 HTTP 目标等边界未测。

## 4. 最小反例 / 复现设计

- **注入的故障**：让被检查目标指向一个**本机假监听器**，而不是已配置的真实实例。
- **输入**：在隔离的 `DATAWATCH_DATA_ROOT` 下造 `config.toml`，写入 `[agent] bootstrap_done = true` 与 `[redis] host="127.0.0.1", port=6379, password_enc=dpapi_encrypt("SENTINEL_PW")`（`win32crypt` 本机**已确认 PRESENT**，可加密）；请求 `POST /health/check` body `{"target":"redis","config":{"host":"127.0.0.1","port":<假监听器端口>}}`。
- **判定复现成功的现象**：假监听器收到的字节中出现哨兵口令 `SENTINEL_PW`（redis-py 建连时会发 `AUTH`）。这同时证明「改 host/port 拿到了原存储凭据」。
- **修复后的期望**：目标与已存配置不一致时，监听器**收不到**任何存储凭据（应 4xx 或要求显式临时凭据）。
- **反向断言**：合法已配置目标（host/port 未变）仍应返回 `success=true`。
- **重定向子复现**：约 20 行 stdlib 脚本（两个 `http.server`：一个 302 到另一个不同端口，一个记录 `Authorization`），断言跳转后 sink 收到的 `Authorization` 为空；对当前代码则断言**不为空**（即暴露）。
- **最合适的现有测试文件（已确认存在）**：`agent/tests/test_admin.py`（333+ 行）、`agent/tests/test_admin_auth.py`（**24** 行处用 `"jwt_secret": "test-secret"`，**166** 行 monkeypatch `app.core.deps.load_config`）。
- **环境**：文件系统 + DPAPI + 回环端口即可；**不需要真实 Redis/MySQL/Docker**（假监听器即可）。
- **本地可跑性**：**不可跑**，详见下节。

## 5. 最小修改候选方向（不改）

`agent/app/routers/admin.py:363-379`：当 `request.config` 提供了 `host`/`port` 且与存盘值不同时，不得沿用 `password_enc`；应要求显式临时 `password`，或拒绝。`agent/app/services/health_checker.py:88-108`：改用不自动跟随重定向的 opener（或对 3xx 直接失败），并限制协议/地址/端口。审计输出不得含秘密（`check_*` 只返回 `success/message`，目前不泄漏口令，需保持）。

## 6. 验收要求映射（文档 299 行 → 断言）

1. 「以隔离接收端和哨兵凭据测试」→ 断言监听器捕获的字节不含哨兵值。
2. 「改变 host/port 不会拿到原存储凭据」→ 断言上一条在 `host`/`port`/`user`/`database` 任一被覆盖时都成立。
3. 「合法已配置目标检查正常」→ 断言未覆盖配置时 `success=true`。
4. 「审计记录不含秘密」→ 断言响应与日志中不出现明文口令/`password_enc` 原文。
5. （补充）「防重定向泄漏」→ 断言跨源 302 后不携带 `Authorization`。

## 7. 关联与冲突

热点文件 `agent/app/routers/admin.py`、`agent/app/services/health_checker.py` 归属 **W1-A1**，与 **B15**（`agent/app/core/config.py`、`security.py`、`deps.py`）同属 W1-A1 → **B11 与 B15 由同一 agent 串行处理**。与其它条目无文件重叠。

## 8. 本地可跑性（实际运行过）

- 命令：`python -m pytest agent/tests/test_admin.py agent/tests/test_admin_auth.py -q`
- **实测结果：collection error，0 通过。** 具体报错链：
  `agent/tests/conftest.py:11` → `from app.services.process_manager import ...` → `agent/app/services/process_manager.py:12` → `import psutil` → `ModuleNotFoundError: No module named 'psutil'`。
- 进一步实测（`importlib.util.find_spec`，两个解释器都比对过）：
  | 包 | `python.exe` (GIL, 3.14.6) | `python3.14t.exe` (free-threading) |
  |---|---|---|
  | `jose` | MISSING | MISSING |
  | `psutil` | MISSING | MISSING |
  | `toml` | MISSING | MISSING |
  | `fastapi` | MISSING | MISSING |
  | `msgspec` | PRESENT | MISSING |
  | `turboapi` | PRESENT（import 会抛 GIL 错误） | PRESENT |
  | `httpx`/`cryptography`/`aiomysql`/`pymysql`/`redis`/`win32crypt`/`pytest`/`pytest_asyncio` | PRESENT | PRESENT |
  → 这 4 个包**都在 `agent/requirements.txt` 里**（`python-jose[cryptography]==3.5.0`、`psutil==7.2.2`、`toml==0.10.2`、`fastapi==0.136.3`），**属本机环境缺依赖，不是代码问题**。W1-A1 需要先建一个装齐 `agent/requirements.txt` 的隔离 venv 才能跑 B11/B15 的测试。
- 证据等级：**本轮为 0（不可跑）**；B11 的结论为**静态代码级 + stdlib 行为实测**。

---

# B15｜已存在 Agent 配置可保留空 jwt_secret

## 1. 真实代码位置

| 文件 : 函数 : 实际行号 | 作用 |
|---|---|
| `agent/app/core/config.py:92-97` : `_load_config_unlocked` | **93** `if not CONFIG_PATH.exists():` → **94-96** `_default_config()` + 落盘；**97** `return toml.load(CONFIG_PATH)` ← **对已存在配置零校验** |
| `agent/app/core/config.py:140` | `"jwt_secret": secrets.token_hex(32)` —— 仅在 `_default_config()` 里生成 |
| `agent/app/core/config.py:44` | `CONFIG_PATH = data_root() / "config.toml"` |
| `agent/app/core/config.py:26` | dev 模式 `data_root()` 回退到 agent 包目录 → dev 下 `agent/config.toml` 一旦存在即被采用 |
| `agent/config.toml.example:9` | `jwt_secret = ""`，注释写「首次启动时自动随机生成，32 字节 hex」**与实际行为不符**（只有文件不存在才生成；照抄模板反而得到空密钥） |
| `agent/app/core/security.py:94-113` : `create_access_token` | **109-113** `jwt.encode(payload, secret, algorithm="HS256")` —— 对 `secret` 无最小值/非空校验 |
| `agent/app/core/security.py:116-143` : `verify_token` | **122-128** `jwt.decode(token, secret, algorithms=["HS256"], issuer=..., audience=...)` —— 同样无校验 |
| `agent/app/core/deps.py:15-32` : `get_current_user` | **26** `verify_token(token, cfg["agent"]["jwt_secret"])` |
| `agent/app/core/deps.py:35-57` : `get_current_admin` | **47** 同上 + `required_roles={"R_SUPER","R_ADMIN"}` |
| `agent/app/routers/auth.py:42` | `secret=cfg["agent"]["jwt_secret"]`（签发） |
| `agent/app/routers/viewer.py:160` | `verify_token(frame.get("token",""), load_config()["agent"]["jwt_secret"])`（WebSocket 帧内校验） |

**jwt_secret 的全部出现点**（grep 实测）：`config.py:140`、`deps.py:26`、`deps.py:47`、`routers/auth.py:42`、`routers/viewer.py:160`，外加测试 `agent/tests/test_admin_auth.py:24`、`:166`。**没有任何一处做非空/强度校验。**

**与文档行号的差异**：文档 B15（清单 343-355 行）**未给行号**，所列 `agent/config.toml.example`、`agent/app/core/config.py`、`agent/app/core/security.py`、`agent/app/core/deps.py` **全部准确**；文档遗漏了 `routers/auth.py:42` 与 `routers/viewer.py:160` 两个消费点（后者是 WebSocket 路径，容易被漏测）。

## 2. 当前代码事实

```python
# agent/app/core/config.py:92-97
92  def _load_config_unlocked() -> dict:
93      if not CONFIG_PATH.exists():
94          cfg = _default_config()      # 含 secrets.token_hex(32)
95          _save_config_unlocked(cfg)
96          return cfg
97      return toml.load(CONFIG_PATH)    # ← 空 jwt_secret 原样返回
```
```python
# agent/config.toml.example:6-9
 6  [agent]
 9  jwt_secret         = ""            # 首次启动时自动随机生成，32 字节 hex
```

## 3. 原判断是否仍成立

**成立（条件性缺陷，代码级确定）。**

- 文档子判断「条件性缺陷成立」→ **成立**：条件是「配置文件**已存在**且其 `jwt_secret` 为空/弱值」。`_load_config_unlocked`（93-97）只看文件是否存在，存在即 `toml.load` 直接返回（97），没有任何 `jwt_secret` 检查；`deps.py:26/47` 拿到空串照样传给 `verify_token`（`security.py:122-128`），而 `security.py` 也不校验密钥长度。
- 文档子判断「配置不存在时自动生成随机密钥的路径正常」→ **成立**：`config.py:93-96` + `140`。
- 文档判断里「不同…明文」等表述不适用本条；本条的关键放大因素是 `agent/config.toml.example:9` **主动示范空值**，而注释说会自动生成 → 运维照抄模板即落入空密钥路径。
- **未确认（必须由 W1-A1 实测）**：**「空密钥能否真的签出 token 并通过校验」我没有运行验证**——本机 `jose` 缺失（两个解释器都 MISSING，见 B11 §8），`agent/app/core/security.py:9` 在模块顶层 `from jose import JWTError, jwt`，因此连 import 都做不到。「python-jose 对空 HMAC 密钥不做长度检查」是我的**推断**，不是实测。请务必用装齐 `agent/requirements.txt` 的隔离 venv 实测后再下结论。

## 4. 最小反例 / 复现设计

- **前置**：需要一个能 `import jose` 的隔离 venv（本机两个解释器都不行）。
- **注入的故障**：不注入故障，直接构造「已存在且 `jwt_secret=""`」的配置。
- **输入**：
  1. 临时目录 `D` 下写 `config.toml`，含 `[agent] jwt_secret = ""`、`bootstrap_done = true`；设 `DATAWATCH_DATA_ROOT=D`；调用 `load_config()`，断言返回的 `jwt_secret == ""`（证明不触发自动生成）。
  2. `token = create_access_token("admin", "", 8, ["R_SUPER"])`，再 `verify_token(token, "")`。
- **判定复现成功的现象**：第 2 步**不抛异常并返回 `"admin"`**；或经真实入口 `get_current_admin(f"Bearer {token}")`（`deps.py:35-57`）返回 `"admin"` 而非抛 401/403 —— 即「服务接受空密钥伪造的 token」。
- **修复后的期望**：服务拒绝以空/弱密钥启动认证（启动即失败或 5xx/配置错误），或首装时原子生成并落盘安全随机值；`verify_token(token, "")` 必须失败。
- **其余场景**（无配置、正常已配、损坏配置、并发首装、重启）按文档 355 行逐条覆盖。
- **最合适的现有测试文件（已确认存在）**：`agent/tests/test_admin_auth.py`（**24** 行 `"jwt_secret": "test-secret"`、**166** 行 monkeypatch `app.core.deps.load_config`）—— 这是最小扩展点；`agent/tests/test_config_paths.py`（存在）适合覆盖 `data_root()`/`CONFIG_PATH` 路径逻辑。
- **环境**：纯函数（`security.py` 只需 jose）+ 文件系统（temp config.toml）；**不需要 Redis/MySQL/Docker/PLC**。

## 5. 最小修改候选方向（不改）

`agent/app/core/config.py:92-97`：加载已存在配置时校验 `agent.jwt_secret` 非空且满足最小强度，否则抛错或原子生成并落盘（注意与 `_CONFIG_LOCK` 配合，避免并发首装竞态、避免静默频繁轮换导致存量会话失效）。`agent/config.toml.example:9` 的注释与取值需与实际行为一致。`agent/app/core/security.py:94-143` 可加密钥长度下限。变更密钥须明确提示会话失效（文档 353 行）。

## 6. 验收要求映射（文档 355 行 → 断言）

1. 「无配置」→ 断言自动生成 32 字节 hex 且落盘，重启后不变（密钥稳定）。
2. 「空密钥」→ 断言以空/弱密钥启动认证**失败**，且空密钥签出的 token 被拒（401）。
3. 「正常已配」→ 断言合法存量配置**保持稳定**，不改写、不轮换密钥。
4. 「损坏配置」→ 断言损坏的 config.toml 不被静默当作空密钥接受。
5. 「并发首装与重启」→ 断言并发首装只产生一个密钥，`_CONFIG_LOCK`（`config.py:45`、`74-75`）生效。
6. （补充）WebSocket 路径 → 断言 `viewer.py:160` 同样拒绝空密钥 token。

## 7. 关联与冲突

**B11**（同属 W1-A1 热点文件组）。`agent/app/core/deps.py` 同时被 B11（`get_current_admin`）与 B15（`jwt_secret` 读取）触碰 → 二者**不能并行写** `deps.py`。`config.py` 的改动会影响 `agent/tests/test_config_paths.py` 与 `test_admin_auth.py` 的既有 monkeypatch 方式。

## 8. 本地可跑性（实际运行过）

- 命令：`python -m pytest agent/tests/test_admin.py agent/tests/test_admin_auth.py -q`
- **实测结果：collection error，0 通过**（`psutil` 缺失，链条见 B11 §8）。
- 另实测 `jose` 在 `python.exe` 与 `python3.14t.exe` 上**均 MISSING** → `agent/app/core/security.py` 无法 import → **B15 的复现脚本本轮完全无法运行**。
- 证据等级：**本轮为 0（不可跑）**；B15 结论为**静态代码级**，「空密钥可伪造 token」标为**未确认（推断）**。

---

# B25｜Uploader 在获得单实例锁前执行 recover_inflight

## 1. 真实代码位置

| 文件 : 函数 : 实际行号 | 作用 |
|---|---|
| `edge/uploader_main.py:79-104` : `main()` | **88** `store = EdgeStorageClient()`；**89** `uploader = Uploader(store, settings, signer=signer)`（**有副作用的构造**）；**98** `with SingleInstanceLock(settings.lock_path):`；**99** `await uploader.run_forever()` |
| `edge/uploader_main.py:55` | `lock_path=data_root / "edge" / "uploader.lock"`（本地文件锁，与 store 无关） |
| `edge/app/uploader.py:91-122` : `Uploader.__init__` | **122** `self._store.recover_inflight(self._lease_owner)` ← **在构造器里、且在任何锁之前**；同处还创建 `httpx.AsyncClient`（**103-107**）与 `RequestSigner.from_path`（**109**） |
| `edge/app/uploader.py:572-615` : `SingleInstanceLock` | Windows 走 `msvcrt.locking(..., LK_NBLCK, 1)`（**588**）；失败在 **593-596** 抛 `RuntimeError("another uploader instance is running")` |
| `edge/app/outbox.py:331-343` : `recover_inflight` | `UPDATE outbox SET state='pending', lease_owner=NULL, inflight_at=NULL, lease_until=NULL WHERE state='inflight' AND (lease_owner IS NULL OR lease_owner <> ? OR lease_until < ?)` —— **条件里的 `lease_owner <> ?` 使任意新 owner 都能把别人的 inflight 行抢回 pending** |
| `edge/app/outbox.py:376-377` : `claim_batch` | `if row["state"] == "inflight" and row["lease_owner"] != lease_owner: break` —— 说明设计上**本来**想用 owner 拦住跨实例抢占，但被 `recover_inflight` 提前抹平 |
| `edge/app/outbox.py:461-468` | `complete_batch` 在丢失租约时抛 `RuntimeError("outbox terminal transition lost lease")` |
| `edge/app/storage_service.py:216-217` | `outbox.recover` 动作服务端分派 |
| `edge/app/storage_client.py` | `recover_inflight` 经 HTTP 打到 edge-storage（**远端进程**，不是本地文件） |

**与文档行号的差异**：文档 B25（清单 483-495 行）**未给行号**，给出的三处位置（`edge/uploader_main.py`：构造与锁顺序；`edge/app/uploader.py`：构造期 recover_inflight；`edge/app/outbox.py`：租约恢复）**全部准确**，我读到的行号分别为 **89 vs 98**、**122**、**331-343**。补充：`recover_inflight` 是**跨进程 HTTP 调用**（`EdgeStorageClient`），所以「失败实例改了状态」是改了 **edge-storage 进程持有的共享 outbox**，影响面比本地构造器更大。

## 2. 当前代码事实

```python
# edge/uploader_main.py:88-99
 88  store = EdgeStorageClient()
 89  uploader = Uploader(store, settings, signer=signer)   # __init__ 里就 recover_inflight
 ...
 98  with SingleInstanceLock(settings.lock_path):          # 锁在构造之后才拿
 99      await uploader.run_forever()
```
```python
# edge/app/uploader.py:122（构造器末尾）
122  self._store.recover_inflight(self._lease_owner)
```

## 3. 原判断是否仍成立

**成立（已实证）。**

- 文档子判断「确定的初始化顺序风险」→ **成立**：`uploader_main.py:89` 早于 `98`，顺序确定。
- 文档子判断「改变租约不是直接删除载荷」→ **成立**：`recover_inflight`（`outbox.py:331-343`）只改 `state`/`lease_*`，不删行。
- 文档处理方式「锁失败不得改变存储状态」→ **当前违反**，实测证据（temp SQLite，未取锁）：
  ```
  owner-A claimed: 1 | stats: {'inflight': 1}
  after SECOND Uploader() constructed (no lock acquired yet): stats: {'pending': 1}
  ```
  即：**仅仅是构造第二个 `Uploader`（从未尝试取锁）就把 owner-A 的 inflight 行改成了 pending**。后果链：败方进程随后可 claim 同一批数据（`claim_batch` 的 owner 拦截已被抹平）→ 重复上传；胜方在 `complete_batch` 时命中 `outbox.py:461-468` 抛 `RuntimeError("outbox terminal transition lost lease")`。
- **未确认**：我未运行双进程真实抢锁实验，因此「胜方抛 lost lease 的实际概率」与「两进程并发写同一 SQLite 的锁竞争表现」未实测；上述后果链为**代码级推导**。

## 4. 最小反例 / 复现设计

- **注入的故障**：不需要注入。利用真实的「第二实例启动」时序。
- **输入**：`OutboxStore(tmp/"edge.db")` + seed 1 条 → `store.claim_batch(target_seq=store.snapshot_target_seq(), lease_owner="owner-A", lease_ms=30000, max_items=10, max_bytes=10**6)`。
- **判定复现成功的现象**：随后**仅**执行 `Uploader(store, cfg, signer=FakeSigner())`（构造函数即触发 `recover_inflight`，不调用 `run_forever`、不获取 `SingleInstanceLock`），`store.stats()` 由 `{'inflight': 1}` 变为 `{'pending': 1}` → 未持锁者改写了存储状态。
- **修复后的期望**：未获得单实例锁的实例，`stats()` 必须保持 `{'inflight': 1}`；胜出实例仍能正常恢复**过期/自身**的租约。
- **进程级版本（更接近验收）**：两个真实子进程指向同一个 temp outbox 与同一个 `lock_path`，断言败方进程退出码非 0（`SingleInstanceLock.__enter__` 抛 `RuntimeError`）**且** outbox 中 inflight 行数不变。
- **最合适的现有测试文件（已确认存在）**：`edge/tests/test_uploader_main.py`（55 行，目前**只测 `settings_from_env`**，未覆盖锁与构造顺序）、`edge/tests/test_outbox.py`（含 `test_recover_inflight_and_session_switch:104`）。建议在 `test_uploader_main.py` 新增用例：monkeypatch `Uploader` 构造、用真实 `SingleInstanceLock` 抢占，断言锁失败时 `recover_inflight` 未被调用。
- **环境**：纯单元（temp SQLite + 本地文件锁）；进程级版本只需两个子进程，仍不需要 Redis/MySQL/Docker/PLC。

## 5. 最小修改候选方向（不改）

`edge/uploader_main.py:88-99`：把 `SingleInstanceLock` 提到 `Uploader(...)` 构造**之前**（或把 `recover_inflight` 从 `Uploader.__init__`（`uploader.py:122`）移到 `run_forever` 的显式 `start()`/带围栏的初始化里，在锁内调用）。`edge/app/uploader.py` 的构造器应无副作用（`httpx.AsyncClient`、`RequestSigner.from_path` 也建议外移）。`edge/app/outbox.py:331-343` 的 `lease_owner <> ?` 条件是否应收紧为「仅回收过期租约」需一并评估（涉 B5 语义）。

## 6. 验收要求映射（文档 495 行 → 断言）

1. 「两个进程争同一 outbox：失败实例无状态写」→ 断言败方构造/退出前后 `SELECT state, COUNT(*)` 完全一致。
2. 「胜出实例正常恢复旧崩溃租约」→ 断言过期的 inflight 行被恢复为 pending。
3. 「正常运行实例的 inflight 不被重置」→ 断言持有未过期租约的行在第二实例启动后仍为 inflight 且 `lease_owner` 不变。

## 7. 关联与冲突

**B5、B6**：同属热点文件 `edge/app/outbox.py` + `edge/app/uploader.py` + `edge/uploader_main.py`（W1-E1）。**B25 修 `Uploader.__init__` 会直接触碰 B5 复现里 `completions` 所在的方法之外的同文件同区域**，且 `recover_inflight` 的 `lease_owner` 条件与 B5 的迁移语义相关 → 三条必须**同一 agent 串行/一并处理**。

## 8. 本地可跑性（实际运行过）

- 命令：`python -m pytest edge/tests/test_uploader_main.py edge/tests/test_outbox.py -q`（含在 B5 那批 5 个文件里）
- **实测结果：29 passed, 0 failed**（5 个文件合计）。注意：现有 `test_uploader_main.py` 的 2 个用例**不覆盖 B25**，29 passed 不代表 B25 被覆盖。
- 证据等级：**单元**（复现脚本为隔离进程内纯函数级）。

---

# B41｜CLI 更新 swap 前半失败时 catch 可删除仍有效的 current

## 1. 真实代码位置

`cli/update_manager.py`（266 行）。

| 位置（Python 侧） : 实际行号 | 作用 |
|---|---|
| `_SWAP_SCRIPT` 字符串 | **227-266** |
| `_launch_swap` | **188-224**；**195-196** 把脚本写到 `<install_root>/update-<hex>.ps1`；**197-214** 拼参数（含 `-InstallRoot`/`-StagedApp`/`-ParentProcessId`/`-ExpectedVersion`/`-Rollback`）；**215-223** `subprocess.Popen(..., DETACHED_PROCESS)`；**224** `return process.pid` |
| `UpdateManager.apply` | **72-93**；**87-92** `_launch_swap(..., rollback=False)`；**93** `return {..., "updateStarted": True, "updaterProcessId": process_id}` |
| `UpdateManager.rollback` | **95-106**；**98** 要求 `previous/data-watch.exe` 存在；**100-105** `_launch_swap(..., rollback=True)` |
| `_require_windows_install` | **179-185**；**180** 要求 `install_root/current/data-watch.exe` 存在（**被更新的正是"正在运行的 current"**） |

**`_SWAP_SCRIPT` 内部真实行号**（行号从 `cli/update_manager.py:227` 起算）：

| 行号 | 语句 |
|---|---|
| 235 | `$ErrorActionPreference = "Stop"` |
| 236 | `Wait-Process -Id $ParentProcessId -ErrorAction SilentlyContinue` |
| 237-239 | `$current` / `$previous` / `$failed` 路径 |
| **240** | `if (Test-Path $failed) { Remove-Item -LiteralPath $failed -Recurse -Force }` ← **在 `try` 之外** |
| **241** | `try {` |
| 242-244 | Rollback 分支：`Move-Item $current -> $failed`；`Move-Item $previous -> $current` |
| **246** | `if (Test-Path $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }` ← **先删掉旧备份** |
| **247** | `Move-Item -LiteralPath $current -Destination $previous` |
| **248** | `Move-Item -LiteralPath $StagedApp -Destination $current` |
| 250-254 | 运行 `$current\data-watch.exe version` 并比对 `$ExpectedVersion` |
| 255 | Rollback 成功路径回收 `$failed` |
| **256** | `} catch {` |
| **257** | `if (Test-Path $current) { Remove-Item -LiteralPath $current -Recurse -Force }` ← **无条件先删 current** |
| 258-262 | 才按 `$Rollback` / `Test-Path $failed` / `Test-Path $previous` 决定是否恢复 |
| **263** | `exit 1` |
| 265 | 删除自身脚本 |

**与文档行号的差异**：文档 B41（清单 707-719 行）**未给行号**；所列 `cli/update_manager.py：_SWAP_SCRIPT / _launch_swap / apply / rollback` **全部准确**（227-266 / 188-224 / 72-93 / 95-106）。文档未点明两个放大器：**`apply` 只返回 PID 就报 `updateStarted: True`（93 行）**，以及 **`previous` 在 247 之前就被 246 删掉**。

## 2. 当前代码事实

```powershell
# cli/update_manager.py:246-263（节选，行号=文件真实行号）
246      if (Test-Path $previous) { Remove-Item -LiteralPath $previous -Recurse -Force }
247      Move-Item -LiteralPath $current -Destination $previous
248      Move-Item -LiteralPath $StagedApp -Destination $current
     ...
250      $result = & (Join-Path $current "data-watch.exe") version | ConvertFrom-Json
     ...
256  } catch {
257      if (Test-Path $current) { Remove-Item -LiteralPath $current -Recurse -Force }
258      if ($Rollback -and (Test-Path $failed)) {
259          Move-Item -LiteralPath $failed -Destination $current
260      } elseif (Test-Path $previous) {
261          Move-Item -LiteralPath $previous -Destination $current
262      }
263      exit 1
264  }
```

## 3. 原判断是否仍成立

**成立（已用故障注入实证）。**

文档子判断「具体失败序列成立；不是每次升级都删整安装根」→ **两条都成立**，且比文档更精确：
- **成立的是**：catch 的**第一条语句（257）无条件删除 `$current`**，而恢复（258-262）**取决于 `$failed`/`$previous` 是否恰好存在**。当失败发生在 247 且 `$previous` 不存在时（246 已把它删掉，或本就无 previous），catch 会删掉唯一的可运行版本，且**恢复分支永不执行**。
- **「不是每次升级都删整安装根」也对**：安装根本身不会被删，只是 `current` 会消失；`staging-*` 与 `draft` 脚本仍在。
- **实测证据**（temp 目录 + 向脚本文本注入 `Move-Item` 抛异常，不触碰仓库文件；详见 §4）：
  ```
  exit=1
  current exists : False
  current exe    : False
  previous exists: False
  failed exists  : False
    <root>\staging-9.0.0
    <root>\staging-9.0.0\app
    <root>\staging-9.0.0\app\data-watch.exe
  ```
  即：唯一的 old 版本（`current`，含 `data-watch.exe` 与 `keep.txt`）被删，无任何可运行版本残留。
- **未确认（重要）**：让 247 **自然**失败的具体触发条件我没有实测（未做文件占用/权限/跨卷实验）。因此「注入式复现」是确定的，而「现实中多大比例会命中该序列」标为**未确认**。另外，`apply()` 在脚本执行前就返回 `updateStarted: True`（`update_manager.py:93`），所以用户看到的是「升级已开始」，而失败只能靠之后查 `current` 是否存在才发现——这一点是**代码级确定**。

## 4. 最小反例 / 复现设计

- **注入的故障（无需文件锁、无需管理员，完全确定）**：PowerShell **函数优先于 cmdlet**，故在脚本的 param 块之后插入
  `function Move-Item { param([Parameter(ValueFromRemainingArguments=$true)]$Rest) throw 'INJECTED Move-Item failure' }`
  即可让 **247 行的第一次 `Move-Item`** 必然抛异常。
- **具体步骤**：
  1. `import cli.update_manager as m`，取 `m._SWAP_SCRIPT`；在 `$ErrorActionPreference = "Stop"` 之后插入上述函数，另存为 temp 下的 `swap_injected.ps1`。
  2. 造 `<root>\current\data-watch.exe`（内容 `OLD-BINARY`）+ `<root>\current\keep.txt`；造 `<root>\staging-9.0.0\app\data-watch.exe`（`NEW-BINARY`）。**故意不造 `previous`**。
  3. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File swap_injected.ps1 -InstallRoot <root> -StagedApp <root>\staging-9.0.0\app -ParentProcessId 999999 -ExpectedVersion 9.0.0`
- **判定复现成功的现象**：`exit=1` **且** `<root>\current` 不存在、`<root>\previous` 不存在 → 失败后找不到任何可运行版本（与 §3 实测输出一致）。
- **修复后的期望**：失败后 `current`（旧版本）仍然存在且可运行；或 `current` 为新版本且通过版本校验；任何情况下至少存在一份通过校验的可运行版本。
- **更贴近真实的注入（供 W1-C1 选用，我未做）**：在 `previous` 内放一个以 `FileShare.None` 打开的文件，使 246 行的 `Remove-Item` 失败 → 观察 catch 删掉仍有效的 `current` 后，`Move-Item $previous -> $current` 是否把**部分删除**的 old 版本搬回来（即"恢复了一个不完整的版本"）。
- **最合适的现有测试文件（已确认存在）**：`cli/tests/test_update_manager.py`（**96 行**）——**目前只覆盖 `_verify_manifest`、`_safe_extract`、`download`（4 个用例），完全没有 `_SWAP_SCRIPT`/`_launch_swap`/`apply` 的测试**。因此 B41 需要**新建**沙盒用例（把 §4 步骤 1-3 写成 pytest 用例，读 `_SWAP_SCRIPT` 做字符串注入 + `subprocess.run(["powershell.exe", ...])`），无需 `_require_windows_install` 的真实安装根。
- **环境**：文件系统 + 真实 PowerShell 子进程 + 权限；在 `tmp_path` 内即可完成，**不需要管理员、不需要 Redis/MySQL/Docker/PLC**。

## 5. 最小修改候选方向（不改）

把 catch（257-262）改为「只撤销**已完成**的步骤」：删除 `$current` 之前必须先确认 replacement 可用（例如先 `Move-Item $previous -> $current`，成功后再清理）；禁止「无条件先删 current」。用显式阶段状态记录已完成的移动（例如在 `$InstallRoot` 写 `swap-phase` 标记），崩溃重启后可据此判断该回滚到哪一步。`UpdateManager.apply`（`update_manager.py:72-93`）在返回 `updateStarted: True` 前应至少有同步校验或结果回报渠道。文档要求「先对临时目录设计失败恢复，再变更生产脚本」→ 建议按此顺序。

## 6. 验收要求映射（文档 719 行 → 断言）

1. 「逐个 Move-Item/校验/清理步骤注入失败」→ 对 243/244/247/248/257/260/261 逐点注入，断言每种失败后都至少存在一个通过 `data-watch.exe version` 校验的可运行版本。
2. 「文件被占用」→ 断言被占用时 `current` 不被删除。
3. 「权限不足」→ 断言脚本以非 0 退出，且不改变 `current`。
4. 「崩溃后重启」→ 断言崩溃点之后重新运行脚本能找到并选对可运行版本（阶段状态可恢复）。
5. 「始终能找到可运行版本，失败不报升级成功」→ 断言失败路径下 `UpdateManager.apply` 不以 `updateStarted: True` 收尾（或该字段语义改为"已提交待验证"并带独立验证入口）。

## 7. 关联与冲突

热点文件 `cli/update_manager.py` 归属 **W1-C1**，与其它 7 条**无文件重叠** → B41 可完全并行。但 `cli/tests/test_update_manager.py` 会被 B41 独占修改。

## 8. 本地可跑性（实际运行过）

- 命令：`python -m pytest cli/tests/test_update_manager.py -q`
- **实测结果：4 passed, 0 failed**（0.35s）。**注意：这 4 个用例都不覆盖 B41 的代码路径**（`_SWAP_SCRIPT`/`_launch_swap`/`apply` 零覆盖），所以 4 passed **不能**作为 B41 的回归基线。
- B41 的复现我按 §4 走的是**独立 PowerShell 沙盒实验**（结果见 §3），不是 pytest。
- 证据等级：复现为**纯函数/脚本级故障注入**（真实 PowerShell 子进程）；现有测试为**单元**。

---

# S3｜Edge Storage 本机 HTTP IPC 未认证即可改变 Outbox/配置状态

## 1. 真实代码位置

| 文件 : 函数/类 : 实际行号 | 作用 |
|---|---|
| `edge/storage_main.py:22-32` : `_parser` | **24** `--host` 默认 `os.environ.get("DATAWATCH_EDGE_STORAGE_HOST", "127.0.0.1")`（**默认回环**）；**25** `--port` 默认 7073 |
| `edge/storage_main.py:36` | `args = _parser().parse_args()` —— `--host 0.0.0.0` **没有任何拒绝/校验逻辑** |
| `edge/storage_main.py:47` | `server = StorageHttpServer((args.host, args.port), engine)` |
| `edge/app/storage_service.py:309-314` : `StorageHttpServer` | `ThreadingHTTPServer` 子类，`daemon_threads = True` |
| `edge/app/storage_service.py:317-366` : `StorageRequestHandler` | **321-328** `do_GET`（`/healthz`→200；`/v1/status`→`_execute("status",{})`）；**330-343** `do_POST`（仅允许 `/v1/command`，只校验 `Content-Length` 与 JSON 形状）；**345-355** `_execute`；**357-363** `_write_json`。**全类无任何身份/凭据/ACL/origin 校验** |
| `edge/app/storage_service.py:197-284` : `StorageEngine._dispatch` | 未认证可达的动作见下表 |
| `edge/app/storage_client.py:23-24` : `storage_base_url` | 默认 `http://127.0.0.1:7073` |
| `edge/app/storage_client.py:251-272` : `EdgeStorageClient._command` | **258-262** `post("/v1/command", content=encoded, headers={"Content-Type": "application/json"})` —— **只发 Content-Type，不携带任何凭据** |

**未认证即可执行的动作**（`_dispatch` 行号）：

| 行号 | action | 影响 |
|---|---|---|
| 204-208 | `frame.commit` | 凭空写入/污染 outbox 与 local_latest |
| 209-215 | `latest.write` | 改写实时值 |
| **216-217** | `outbox.recover` | 重置 inflight（与 B25 叠加） |
| 218-219 | `outbox.upload_interval` | 读取 |
| 220-221 | `outbox.snapshot` | 读取 |
| **222-223** | `outbox.claim` | 抢占他人 inflight 批次 |
| **224-234** | `outbox.complete` | **把任意行置 `sent`/`dead`** → 未确认数据被未授权 complete |
| **235-250** | `outbox.retry` | 改 `attempt_count`/`next_retry_at`（与 B6 叠加） |
| 251-252 | `session.pending` | 读取 |
| **253-254** | `session.prepare` | 强制会话切换、重排 local_seq（与 B5 叠加） |
| **255-262** | `session.complete` | 确认会话切换 |
| 263-264 | `session.current` | 读取 |
| **265-266** | `maintenance` | 触发清理，参数 `retention_days`/`sent_retention_ms` 由调用方给 |
| 267-268 | `config.revision` | 读取 |
| **269-270** | `config.apply` | 改写配置快照 |
| 271-273 | `config.refresh_schedule` | 改调度 |
| 274-281 | `device.status` | 改设备状态 |

**与文档行号的差异**：文档 S3（清单 1431-1443 行）**未给行号**；所列 `edge/storage_main.py`（绑定地址）、`edge/app/storage_service.py`（`/v1/command` 与 outbox/session/config 分派）、`edge/app/storage_client.py` **全部准确**。文档说「默认回环监听不是用户/进程身份认证」——**正确**；我额外核到的是：`--host` 可任意覆盖且**无守卫**（`storage_main.py:24` 只是默认值，36 行直接采信），以及 `outbox.complete` 能直接把未确认载荷置 `sent`。

## 2. 当前代码事实

```python
# edge/app/storage_service.py:330-343
330  def do_POST(self) -> None:
331      if self.path != "/v1/command":
332          self._write_json(HTTPStatus.NOT_FOUND, {"ok": False, "error": "not found"})
333          return
334      try:
335          length = int(self.headers.get("Content-Length") or 0)
336          if length <= 0 or length > 32 * 1024 * 1024:
337              raise ValueError("invalid request size")
338          body = msgspec.json.decode(self.rfile.read(length))
341          self._execute(str(body.get("action") or ""), dict(body.get("payload") or {}))
```
```python
# edge/app/storage_client.py:256-262（节选）
257      with self._request_lock:
258          response = self._client.post(
259              "/v1/command", content=encoded,
261              headers={"Content-Type": "application/json"},   # ← 无凭据
262          )
```

## 3. 原判断是否仍成立

**成立（代码级确定，无需运行）。**

- 文档子判断「服务端缺少认证已由代码确认」→ **成立**：`StorageRequestHandler`（`storage_service.py:317-366`）通篇未读取任何自定义 header，未做 token/HMAC/uid/ACL 校验。
- 文档子判断「默认回环监听不是用户/进程身份认证」→ **成立**：`storage_main.py:24` 默认 `127.0.0.1`，但同文件 **36** 行无条件采用 `args.host`，`--host 0.0.0.0` 可直接把 IPC 开到全部网卡（无 warn、无拒绝）。
- 文档子判断「实际可达性取决于主机策略」→ **成立**（本机 Windows 下是否还有其他本机账户/进程可达，我未做系统级验证，标为**未确认**）。
- 文档验收「未确认数据不会被未授权 complete」→ **当前违反**，`outbox.complete`（224-234）无身份即可把行置 `sent`。
- **未确认**：我没有启动 Storage 服务做端到端未认证调用（按协调者指示，S3 只给静态结论 + 复现设计，不新建测试、不启动服务）。因此「一次真实未认证 `outbox.complete` 成功」为**代码级推导**，非实测。
- **附加观察（与文档「不要复用拥有广泛管理权的 Agent service token」相关）**：`agent/app/core/deps.py:12,66-71` 的 `DATAWATCH_AGENT_SERVICE_TOKEN` 是 Agent 面向「驱动管理」的凭据；S3 若直接改为复用该 token，会扩大其权限面——文档的告警方向正确。

## 4. 最小反例 / 复现设计

- **注入的故障**：不需要注入故障——用**完全不带任何 header**（只带 `Content-Type`）的 HTTP 请求打 IPC。
- **输入**：
  1. `StorageEngine(tmp/"edge.db")` + `engine.start()`，`StorageHttpServer(("127.0.0.1", <随机端口>), engine)`。
  2. 先用合法路径（或直接 `OutboxStore`）写入并 claim 一行，使其 `state='inflight'`、`lease_owner='owner-A'`。
  3. 发 `POST /v1/command`，body 为
     `{"action":"outbox.complete","payload":{"lease_owner":"attacker","completions":[{"record":{"batch_id":...,"upload_session_id":...,"local_seq":...,"assignment_epoch":...,"payload_b64":...},"state":"sent"}]}}`。
- **判定复现成功的现象**：(a) 无身份请求返回 **HTTP 200 `{"ok":true,...}`**；(b) 该行 `state` 由 `inflight` 变为 **`sent`**（未确认载荷被未授权置为已确认）。
- **修复后的期望**：无身份请求返回 401/403 且行状态不变；合法 Collector/Uploader/Agent 各自的权限分别可测。
- **同族补充断言**：无凭据的 `{"action":"config.apply", ...}` 应被拒（当前会成功）；`--host 0.0.0.0` 应被拒绝或需要显式确认。
- **最合适的现有测试文件（已确认存在）**：`edge/tests/test_storage_service.py`（170 行）——其中 `test_storage_serializes_concurrent_frames_and_batch_ack`（52-119 行）已经是「真实 `ThreadingHTTPServer` + httpx 打 `/v1/command`」的写法，可直接照搬做未认证注入。`edge/tests/test_uploader.py:62-100` 里 `_record_payload` 的字段形状也可复用。
- **环境**：文件系统（temp SQLite）+ 本机回环端口；**不需要 Redis/MySQL/Docker/PLC**。
- **按协调者指示**：本轮**不新建测试**，复现留给 W1-S3。

## 5. 最小修改候选方向（不改）

`edge/app/storage_service.py:317-366`：为 `/v1/command`（以及 `/v1/status`）引入本机信任边界——专用最小权限身份（例如受 ACL 保护的本机文件中的 per-role token）或受 ACL 保护的命名管道/本机传输；`StorageRequestHandler` 需在 `_execute` 前校验，并按调用方角色限制可用 action（Collector 只需 `frame.commit`/`latest.write`/`device.status`；Uploader 需要 outbox/session 类；Agent 需要 config 类）。`edge/app/storage_client.py:251-272` 需带上凭据（分角色）。`edge/storage_main.py:24,36` 应限制 `--host`（拒绝非回环，或在非回环时要求显式授权标志）。**不要复用 `DATAWATCH_AGENT_SERVICE_TOKEN`**（`agent/app/core/deps.py:12,66-71`）。

## 6. 验收要求映射（文档 1443 行 → 断言）

1. 「普通本地账户/无身份进程无法更改 outbox/session/config」→ 断言无凭据请求对 `outbox.complete`/`outbox.retry`/`session.prepare`/`session.complete`/`config.apply`/`maintenance` 全部 401/403，且状态无变化。
2. 「合法 Collector/Uploader/Agent 权限分别测试」→ 断言每个角色的凭据只能执行其白名单 action，越权 action 被拒。
3. 「未确认数据不会被未授权 complete」→ 断言 `state IN ('pending','retry_wait','inflight')` 的行不会被非 Uploader 角色改为 `sent`/`dead`。
4. （补充）「不要将 Storage 开到全部网卡」→ 断言 `--host 0.0.0.0` 被拒绝或需显式授权。

## 7. 关联与冲突

热点文件 `edge/app/storage_service.py`、`edge/storage_main.py` 归属 **W1-S3**。与 B5/B6/B25 **无文件重叠**，但**语义强耦合**：S3 要为 IPC 加认证，会改变 `EdgeStorageClient`（`edge/app/storage_client.py`）的调用形状，而该客户端正是 `edge/app/uploader.py` 的 store 实现 → S3 与 W1-E1（B5/B6/B25）**必须协调 `storage_client.py` 的凭据注入**，否则会互相破坏。另注意 S3 的 action 白名单与 B25 的 `outbox.recover`、B6 的 `outbox.retry`、B5 的 `session.prepare` 直接相关。

## 8. 本地可跑性（实际运行过）

- 命令：`python -m pytest edge/tests/test_storage_service.py -q`（含在 B5 那批 5 个文件里）
- **实测结果：29 passed, 0 failed**（5 个文件合计）。现有用例覆盖 keepalive、并发序列化、关闭超时、队列背压，**未覆盖认证**（因为当前没有认证）。
- 本轮未运行/未新建针对 S3 的未认证调用实验（遵协调者指示）。
- 证据等级：**静态代码级**；现有测试为**单元**。

---

# 汇总：文档定位与真实行号的关系

**本批次 8 条在清单文档（203-215 / 217-229 / 231-243 / 287-299 / 343-355 / 483-495 / 707-719 / 1431-1443 行）中一律只给「文件 + 函数/类」而没有任何行号。** 因此不存在"文档行号与真实行号不符"的情况；逐条核对结果是：

| 条目 | 文档所列文件/函数 | 核对结果 |
|---|---|---|
| B5 | `edge/app/uploader.py`（ACK 遍历/completions/session 切换）、`edge/app/outbox.py`（complete_batch/complete_session_switch）、`ingest/app/redis_store.py`（高水位去重） | 文件与函数**均存在**；**遗漏**真正的迁移逻辑在 `outbox.py:542-602 prepare_session_switch`，`complete_batch` 只是接收方 |
| B6 | `edge/app/outbox.py`（mark_retry_batch/attempt_count/maintenance）、`edge/app/uploader.py`（quarantine_after_max 与失败分类） | **准确**（`outbox.py:496-540`/`513`/`635-721`，`uploader.py:460-502`） |
| B7 | `backend/app/services/catalog.py` 与 `api/routes/catalog.py`（点位事务/版本更新）、`api/routes/distributed_nodes.py`（失效发布）、`ingest/app/repository.py` 与 `service.py`（版本/永久映射校验） | 文件与函数**均存在**；但**结论误导**：`catalog.py` **完全不发布**缓存失效（只有 `distributed_nodes.py:70/122/155/193` 四个发布点，且从不发 `scope="mappings"`） |
| B11 | `agent/app/routers/admin.py`（健康检查路由）、`agent/app/services/health_checker.py`（目标合并/DPAPI 解密/check_tdengine） | **准确**（`admin.py:358-379`、`health_checker.py:17-20/38-108`）；**遗漏** `bootstrap.py:169-188`、`viewer.py:39-63` 两个共用调用点 |
| B15 | `agent/config.toml.example`、`agent/app/core/config.py:load_config`、`agent/app/core/security.py`、`agent/app/core/deps.py` | **准确**（`config.py:92-97/140`、`security.py:94-143`、`deps.py:26/47`）；**遗漏** `routers/auth.py:42`、`routers/viewer.py:160` 两个消费点 |
| B25 | `edge/uploader_main.py`（Uploader 构造与锁顺序）、`edge/app/uploader.py`（构造期 recover_inflight）、`edge/app/outbox.py`（租约恢复） | **准确**（`uploader_main.py:89` vs `98`、`uploader.py:122`、`outbox.py:331-343`） |
| B41 | `cli/update_manager.py`（`_SWAP_SCRIPT`/`_launch_swap`/`apply`/`rollback`） | **准确**（227-266 / 188-224 / 72-93 / 95-106）；**未点明** catch 无条件删 `current` 在 257 行、`previous` 在 246 行先被删、`apply` 在 93 行提前报成功 |
| S3 | `edge/storage_main.py`（绑定地址）、`edge/app/storage_service.py`（`/v1/command` 与 outbox/session/config 分派）、`edge/app/storage_client.py` | **准确**（`storage_main.py:24/47`、`storage_service.py:330-343`/`197-284`、`storage_client.py:251-272`）；**未点明** `--host` 可任意覆盖且无守卫（`storage_main.py:36`） |

---

# 本轮实际运行的测试（真实结果，未美化）

| 命令 | 结果 | 证据等级 |
|---|---|---|
| `python -m pytest edge/tests/test_uploader.py edge/tests/test_outbox.py edge/tests/test_session_reconcile.py edge/tests/test_storage_service.py edge/tests/test_uploader_main.py -q` | **29 passed, 0 failed**（3.16s） | 单元 |
| `python -m pytest ingest/tests/test_service.py -q` | **6 passed** | 单元 |
| `python -m pytest ingest/tests/test_repository.py -q` | **1 failed, 8 passed**（失败项 `test_parent_schema_preflight_marks_workers_ready`，与 B7 无关，原因为 `ingest.main` 依赖的 turboapi 在 GIL 版 3.14.6 下抛 `ImportError: TurboAPI requires a free-threading Python build`） | 单元 |
| `python3.14t -m pytest ingest/tests/test_repository.py -q` | **collection 阶段崩溃**：`Windows fatal exception: access violation` | — |
| `python -m pytest cli/tests/test_update_manager.py -q` | **4 passed**（但 4 个用例**零覆盖** B41 的 `_SWAP_SCRIPT`/`_launch_swap`/`apply`） | 单元 |
| `python -m pytest agent/tests/test_admin.py agent/tests/test_admin_auth.py -q` | **collection error，0 通过**（`psutil` 缺失；`jose`/`toml`/`fastapi` 在两个解释器上均 MISSING） | 不可跑 |

**独立故障注入实验（非 pytest，均为隔离进程/临时目录）**：

| 条目 | 实验 | 结果 |
|---|---|---|
| B5 | temp SQLite + MockTransport，ACK 前缀 + seq_reject 混合响应 | **复现**：ACK 项被迁到新会话且 `state='pending'` |
| B6 | 503×2 累加 attempt_count，再 400 | **复现**：一次 400 即 `dead`，`last_error=http_400_contract_rejected`、残留 `http_status=503` |
| B11 | 本机两端口 302 重定向 + 记录 Authorization | **实测**：`Authorization` 被转发到跳转后的 host:port（stdlib 行为，项目路径未运行） |
| B25 | claim(owner-A) 后仅构造第二个 `Uploader` | **复现**：`{'inflight':1}` → `{'pending':1}` |
| B41 | temp 沙盒 + 注入 `Move-Item` 抛异常 | **复现**：exit=1，`current`/`previous`/`failed` 全不存在，无可运行版本残留 |

---

# 未确认清单（请 W1 各 agent 优先补齐）

1. **B11**：项目代码真实调用路径下的**重定向凭据泄漏**未运行（只实测了 stdlib 转发行为）；空密码/非 HTTP 目标边界未测。
2. **B15**：**「空密钥能否真的签出并通过校验」未实测**（`jose` 缺失导致 `agent/app/core/security.py` 无法 import）。「python-jose 不做密钥长度检查」是推断，不是实测。B15 的整条结论目前属**静态代码级**。
3. **B15 / B11**：agent 模块的两个解释器都缺 `jose`/`psutil`/`toml`/`fastapi`（都在 `agent/requirements.txt` 里）→ **必须先建隔离 venv**，否则 B11/B15 任何测试都跑不了。
4. **B25**：未做双进程真实抢锁实验，「胜方抛 `lost lease` 的实际概率」与两进程并发写同一 SQLite 的表现未测。
5. **B41**：让脚本 247 行**自然**失败的具体触发条件未测（未做文件占用/权限/跨卷实验）；已完成的是**注入式**复现。「升级失败后用户是否真的感知不到」未做端到端验证。
6. **B7**：未做 backend↔ingest 跨进程集成（真实 `publish`→`invalidate` 时延、TTL 抖动 0.8~1.2 对竞态窗口的实际影响）。
7. **B6**：dead 行在保留期后的清理与人工恢复路径未做运行级验证（仅代码级）。
8. **S3**：未启动 Storage 服务做端到端未认证调用（按协调者指示仅给静态结论）；本机其他账户/进程的实际可达性未做系统级验证。
9. **B5**：ACK 应答丢失 + 本地崩溃、切换中崩溃三个注入场景本轮只做了「混合 ACK + seq_reject」，其余留待 W1-E1。
10. **统一**：所有结论的证据等级最高为**单元/纯函数/脚本级**，**没有任何目标机验收**；真实 PLC 一律未覆盖且本轮不授权。

---

*本文件由 W0-B 任务卡 agent 生成，仅做只读定位。未修改任何源码、配置或测试文件；所有复现均在系统临时目录或隔离进程内完成。*
