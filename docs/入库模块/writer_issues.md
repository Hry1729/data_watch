# Writer 模块问题清单（整合版）

> 整合来源：`writer_code_review_report.md`（前序报告）+ 本次逐行复审
> 创建日期：2026-05-22
> 修订：2026-05-22 复审后整合 —— 原 S1 拆分为 S1 + M6；新增 H1（畸形消息）、M5（XACK 失败）
> 完成：2026-05-23 14/15 项已闭合（仅 L5 env-var 推迟）。S1 经 P1/P2 二轮补漏；M5 经 P2 顺序修正；M4 顺带补齐 H1 在 drain 路径的 per-entry 兜底；L6 顺带新增 MySQL 自动发现 stream scope（原 `device_types`/`group_codes`/`stable_names` 静态配置已删除）。详见文末「总结」
> 评审范围：`writer/main.py`、`consumer.py`、`tdengine_writer.py`、`mysql_mapper.py`、`dlq.py`、`config/settings.toml`

---

## 问题汇总表

| 编号 | 级别 | 模块 | 简述 | 来源 | 置信度 |
|------|------|------|------|------|--------|
| S1 | 🔴 严重 | `tdengine_writer.py` | TDengine 断连后无重连，恢复后无法自愈，落库持续失败直至进程重启 | 两份报告重合 | 100% |
| S2 | 🔴 严重 | `consumer.py` | PEL 消息运行期间永久积压，`drain_pending` 仅启动调用一次 | 两份报告重合 | 95% |
| H1 | 🟠 高 | `consumer.py` | 单条畸形 Stream 消息（合法 JSON 但非对象）打崩整个 writer 主循环 | 本次复审新增 | 95% |
| M1 | 🟡 中 | `tdengine_writer.py` | `_ensure_stable` 只用第一列建表，其余字段逐条 ALTER STABLE，效率低且易引发 DDL 冲突 | 前序报告独有，本次核实 | 100% |
| M2 | 🟡 中 | `mysql_mapper.py` + `consumer.py` | `config_version` 不匹配时条目不 XACK 直接留 PEL，S2 修复后部分缓解 | 两份报告重合 | 95% |
| M3 | 🟡 中 | `consumer.py` + `mysql_mapper.py` | MySQL mapper cache miss 在事件循环线程中同步阻塞 | 本次独有 | 90% |
| M4 | 🟡 中 | `consumer.py` | fallback `_drain_with_xpending_xclaim` 无分页，大 PEL 只恢复第一批 | 本次独有 | 95% |
| M5 | 🟡 中 | `consumer.py` | `write_batch` 成功后 XACK 失败，行已从 buffer 清除但 PEL 残留，致重复写入 | 本次复审新增 | 75% |
| M6 | 🟡 中 | `consumer.py` | flush 持续失败时缺退避与日志降噪（从原 S1 拆出，非 CPU 满载根因） | 本次复审拆分 | 90% |
| L1 | 🟢 低 | `consumer.py` | buffer 满时先 sleep `flush_interval_s` 再 flush，多等一个间隔 | 本次独有 | 85% |
| L2 | 🟢 低 | `consumer.py` + `tdengine_writer.py` | `_flush` 与 `write_batch` 双重按 `child_table` 分组，冗余 | 本次独有 | 100% |
| L3 | 🟢 低 | `consumer.py` | `_get_delivery_count` 每条 entry 单独发一次 XPENDING_RANGE，N 条 = N 次 RTT | 本次独有 | 90% |
| L4 | 🟢 低 | `tdengine_writer.py` | `_describe_columns` 的 TAG 识别依赖硬编码列名 `device_code`（**注意：是有意兼容设计**） | 本次独有 | 70% |
| L5 | 🟢 低 | `config/settings.toml` | MySQL / TDengine 密码明文存储，无环境变量覆盖机制 | 本次独有 | — |
| L6 | 🟢 低 | `main.py` | 无配置校验层，键拼写错误直接 `KeyError` 崩溃，无可读提示 | 本次独有 | — |

---

## 修复进度

- [x] **S1** — TDengine 断连重连（2026-05-22 已实施，含 P1 持续重连 + P2 错误识别放宽）
- [x] **S2** — 定期 drain PEL（2026-05-22 已实施）
- [x] **H1** — 畸形消息隔离（2026-05-22 已实施）
- [x] **M5** — XACK 失败处理（2026-05-23 已实施，含 P2 顺序修正）
- [x] **M1** — 建表一次性含所有列（2026-05-23 已实施）
- [x] **M4** — fallback drain 分页 + 顺带 H1 drain 路径隔离（2026-05-23 已实施）
- [x] **M6** — flush 退避与降噪（2026-05-23 已实施）
- [x] **M3** — mapper 非阻塞（2026-05-23 已实施）
- [x] **M2** — config_version 不匹配降级（2026-05-23 已实施）
- [x] **L1** — buffer 满时直接 flush，去掉冗余 sleep（2026-05-23 已实施）
- [x] **L2** — `write_batch` 去掉重复分组（2026-05-23 已实施）
- [x] **L3** — `_get_delivery_count` 批量化（2026-05-23 已实施）
- [x] **L4** — `_describe_columns` TAG 识别改用已知 tag 集合（2026-05-23 已实施）
- [ ] **L5** — 密码支持环境变量覆盖（**推迟**：决定接受当前 settings.toml 明文方案；如需密钥外置作为独立 follow-up）
- [x] **L6** — 配置 required-key 校验 + Stream 范围 MySQL 自动发现（2026-05-23 已实施）

---

## 详细说明与解决方案

### S1：TDengine 断连后无重连，恢复后无法自愈

`connect()` 只在进程启动时建立 WebSocket 连接。运行中若发生网络瞬断或 TDengine 实例重启，`self._conn` 仍非 `None`（仅引用失效）。`_execute` / `_query` 调用失效连接会报错并返回 `False` / `[]`，落库持续失败。

`TDengineWriter` 既没有保存 `config` 副本，也没有任何重连方法，断网恢复后**无法自我修复**，落库失败一直持续到进程重启。

> **表述修正**：前序报告 BUG-1 称之为「CPU 100% 忙等死循环」，本次复审核实**该表述不准确**。`run_loop` 已有两处被动等待：buffer 未满时 `xreadgroup(block≈250ms)`、buffer 满时 `asyncio.sleep(flush_interval_s≈500ms)`。断连后实际表现为**以 250–500ms 固定间隔持续重试 + 错误日志高频刷新**，不是 CPU 满载空转。日志刷屏与重试压力的问题已单独拆分为 **M6**。

**修复 — `tdengine_writer.py`：保存 config，执行失败时检测并重连**

```python
def __init__(self) -> None:
    self._conn: Any = None
    self._database: str = "device_data"
    self._lock = asyncio.Lock()
    self._col_cache: dict[str, set[str]] = {}
    self._config: dict | None = None       # 新增：保存供重连使用
    self._reconnecting: bool = False       # 新增：重入保护标志

async def connect(self, config: dict) -> None:
    self._config = config                  # 新增：入口处保存
    ...

# 模块级辅助：集中匹配连接类异常关键词（P2）
_CONNECTION_ERROR_KEYWORDS = (
    "closed", "connection", "timeout", "timed out",
    "broken pipe", "eof", "reset", "refused", "unavailable",
)
def _is_connection_error(exc: Exception) -> bool:
    msg = str(exc).lower()
    return any(kw in msg for kw in _CONNECTION_ERROR_KEYWORDS)

async def _execute(self, sql: str) -> bool:
    if self._conn is None:
        logger.error("[tdengine] not connected, cannot execute SQL")
        if self._config is not None and not self._reconnecting:
            await self._try_reconnect()    # P1：断连后持续重连，不再永久卡死
        return False
    ...
    except Exception as exc:
        logger.error("[tdengine] execute failed: %s | SQL: %.200s", exc, sql)
        if (
            self._config is not None
            and not self._reconnecting     # 重入保护：防止重连期间递归
            and _is_connection_error(exc)  # P2：放宽连接异常识别
        ):
            await self._try_reconnect()
        return False

async def _try_reconnect(self) -> None:
    """重连前后用 _reconnecting 标志包裹，避免 connect() 内部 _execute 失败时递归触发。"""
    self._reconnecting = True
    logger.info("[tdengine] attempting reconnect...")
    try:
        if self._conn is not None:
            try:
                self._conn.close()
            except Exception:
                pass
            self._conn = None
        await self.connect(self._config)
        logger.info("[tdengine] reconnect succeeded")
    except Exception as exc:
        logger.error("[tdengine] reconnect failed: %s", exc)
    finally:
        self._reconnecting = False
```

**置信度**：100%

**修复状态**：✅ 已实施（2026-05-22，`writer/tdengine_writer.py`），分两轮：

**第一轮 — 主体重连**
- `__init__` 新增 `self._config` 与 `self._reconnecting` 两个字段。
- `connect()` 入口保存 `self._config = config`。
- 新增 `_try_reconnect()`，置于 `_query()` 之后；用 try/finally 维护 `_reconnecting` 标志。
- **重入保护**：`_reconnecting` 标志避免 `connect()` 内部经 `_ensure_database` / `_preload_stables` 再次调用 `_execute()` 时递归触发重连（栈溢出）。

**第二轮 — 补漏（复审发现 P1 / P2）**
- **P1（关键）**：第一轮只在 `_execute()` 的「执行异常」分支重连。若首次重连失败，`_conn` 留为 `None`，之后所有 `_execute()` 走 `_conn is None` 分支直接 return，再不触发重连 → 仍需人工重启。已修复：`_conn is None` 分支也调用 `_try_reconnect()`（带 `not self._reconnecting` 保护）后再 return False。
- **P2**：连接异常关键词从仅 `closed` / `connection` 放宽。新增模块级 `_is_connection_error(exc)`，集中匹配 `closed / connection / timeout / timed out / broken pipe / eof / reset / refused / unavailable`，替换 `_execute()` 异常分支的内联判断。
- 重连后当前 SQL 仍返回 `False`、不立即重放，交由 consumer buffer 下轮 retry —— 避免不确定的重复执行。
- 两轮均通过 `python -m py_compile` 语法校验。

> 已知未覆盖点（不在本步范围）：`_query()` 与 `_add_column()` 自身的连接失败不触发重连，但其后的 `_execute()` 会兜底触发，可接受。

---

### S2：PEL 消息运行期间永久积压（`drain_pending` 仅启动时调用一次）

`drain_pending` 只在进入 `while True` 主循环前执行一次，之后主循环只读 `">"` 新消息，不再扫描 PEL。

触发场景：
- `config_version` 瞬间不匹配 → 条目留 PEL（见 M2）
- 写入成功但 XACK 失败 → 条目留 PEL（见 M5）
- 多节点场景中消费者节点崩溃 → 崩溃节点的 PEL 无人认领，直到服务重启

**修复 — `consumer.py`：在主循环中每 60s 执行一次 drain**

```python
# __init__ 新增
self._last_pending_drain: float = time.monotonic()
self._pending_drain_interval_s: float = 60.0

# run_loop 的 while True 顶部新增
if time.monotonic() - self._last_pending_drain >= self._pending_drain_interval_s:
    for sk in stream_keys:
        await self.drain_pending(sk, self._consumer_name)
    self._last_pending_drain = time.monotonic()
```

**置信度**：95%（60s 间隔合理，可按需配置化）

**修复状态**：✅ 已实施（2026-05-22，`writer/consumer.py`）。实测改动：
- `StreamConsumer.__init__`：在 `_last_flush` 之后新增 `_last_pending_drain`（初值 `time.monotonic()`）与 `_pending_drain_interval_s = 60.0`。
- `run_loop` 的 `while True` 顶部新增：距上次 drain 满 60s 时，对所有 `stream_keys` 逐个 `await drain_pending(...)`，完成后刷新 `_last_pending_drain`。
- 启动时的一次性 drain（`run_loop` 进入主循环前）保留不变。
- `drain_pending` 自带 try/except，单次 drain 失败不会冲垮主循环。
- `python -m py_compile` 语法校验通过。

> 说明：`_pending_drain_interval_s` 暂为硬编码 60.0，后续如需可提升为 `settings.toml` 配置项。

---

### H1：单条畸形 Stream 消息打崩整个 writer 主循环

**位置**：`consumer.py` `run_loop`（约 129–135 行）、`_handle_entry`（约 310–315 行）

`run_loop` 中对每条消息的处理：

```python
for entry_id, entry_data in entries:
    self._handle_entry(...)        # 外层无 per-entry try/except
```

`_handle_entry` 内的 `json.loads` 有 try/except，但**只能拦截「不合法 JSON」**。合法但不是对象的 JSON（`[]`、`"abc"`、`123`）能顺利通过：

```python
try:
    raw_values: dict[str, str] = json.loads(values_raw)   # "[]" 不报错，得到 list
except Exception as exc:
    ...
    return

for var_id, str_val in raw_values.items():   # list 无 .items() → AttributeError
```

`AttributeError` 在 `_handle_entry` 内无人接住，沿 `run_loop` → `main()` 冒泡。`main()` 只 catch `CancelledError` / `KeyboardInterrupt`，进程退出。**单条坏数据即可杀死整个 writer。**

**修复（两层，建议都加）**：

1. `_handle_entry` 内 `json.loads` 后校验类型：

```python
try:
    raw_values = json.loads(values_raw)
except Exception as exc:
    logger.warning("[consumer] invalid values JSON for entry %s: %s", entry_id, exc)
    self._ack_single(stream_key, entry_id)
    return
if not isinstance(raw_values, dict):
    logger.warning("[consumer] values is not a JSON object for entry %s: %r", entry_id, raw_values)
    self._ack_single(stream_key, entry_id)   # 畸形消息：XACK 丢弃（或改为入 DLQ）
    return
```

2. `run_loop` 中对 `_handle_entry` 调用加 per-entry 兜底，防止任何意外异常杀死主循环：

```python
for entry_id, entry_data in entries:
    try:
        self._handle_entry(
            stream_key=stream_key,
            entry_id=entry_id,
            entry_data=entry_data,
            delivery_count=1,
        )
    except Exception as exc:
        logger.error(
            "[consumer] unexpected error handling entry %s, skipping: %s", entry_id, exc
        )
        # 不 XACK：留 PEL，后续 drain 重试，连续失败达上限后进 DLQ
```

**置信度**：95%（崩溃路径明确；改动小、收益高）

**修复状态**：✅ 已实施（2026-05-22，`writer/consumer.py`）。实测改动：
- **第 1 层（源头校验）**：`_handle_entry` 中 `json.loads` 成功后新增 `isinstance(raw_values, dict)` 校验。非 dict（`[]`、`123`、`"abc"` 等合法 JSON 但非对象）→ warning + `_ack_single` 丢弃 + return，避免后续 `raw_values.items()` 抛 `AttributeError`。此层覆盖 `_handle_entry` 全部调用方（含 drain 路径）。
- **第 2 层（兜底隔离）**：`run_loop` 中对每条 `_handle_entry` 调用包 per-entry `try/except`。任何意外异常仅记 error 并跳过该条，不杀主循环；出错条目不 XACK，留 PEL 由后续 drain 重试、达上限进 DLQ。
- drain 路径（`_drain_with_xautoclaim` / `_drain_with_xpending_xclaim`）的 `_handle_entry` 调用已被 `drain_pending` 外层 try/except 保护，未额外加 try/except。
- `python -m py_compile` 语法校验通过。

---

### M1：`_ensure_stable` 只用第一列建表，其余字段逐条 ALTER STABLE

`_ensure_stable` 建表时只将 `initial_columns` 的第一个列放入 `CREATE STABLE` DDL，其余列由 `_ensure_columns` 逐条发 `ALTER STABLE ... ADD COLUMN`。

50 个点位 → 49 条串行 DDL。TDengine 对同一 stable 连续 DDL 变更极不友好，易触发 `Table schema is changing` 冲突，导致部分 ALTER 被拒绝，写入重试。

**修复 — `tdengine_writer.py`：`CREATE STABLE` 时一次性包含所有初始列**

```python
async def _ensure_stable(self, stable: str, initial_columns: list[str] | None = None) -> None:
    if stable in self._col_cache:
        return

    valid_cols = [col for col in initial_columns or [] if col]
    if not valid_cols:
        logger.info(
            "[tdengine] stable %s.%s has no columns to create with",
            self._database, stable,
        )
        return

    db = _quote(self._database)
    stbl = _quote(stable)
    cols_sql = ", ".join(f"{_quote(col)} DOUBLE" for col in valid_cols)  # 一次性所有列
    sql = (
        f"CREATE STABLE IF NOT EXISTS {db}.{stbl} "
        f"(`ts` TIMESTAMP, {cols_sql}) "
        f"TAGS (`device_code` VARCHAR(32))"
    )
    ...
```

**置信度**：100%

**修复状态**：✅ 已实施（2026-05-23，`writer/tdengine_writer.py` `_ensure_stable`）。实测改动：
- 把 `first_col = next((col for col in initial_columns or [] if col), "")` 改为 `valid_cols = [col for col in initial_columns or [] if col]`；空集时维持原日志和早退行为。
- `CREATE STABLE` SQL 列定义从单列 `` `first_col` DOUBLE `` 改为 `cols_sql = ", ".join(f"{_quote(col)} DOUBLE" for col in valid_cols)`，一次性包含全部初始列。
- 加一行 WHY 注释说明意图，防止后续维护者改回单列建表。
- `python -m py_compile` 通过。

**影响面**：
- 仅影响 **首次创建未知 stable** 的路径（生产中常见于新设备类型上线、首次写入）。原来 50 个点位 → 1 条 CREATE + 49 条 ALTER（极易触发 `Table schema is changing`）；现在 → 1 条 CREATE 完成。
- 已存在的 stable 走 `CREATE STABLE IF NOT EXISTS` no-op，行为不变。
- 后续 PLC 配置新增点位（已有 stable 上加列）仍走 `_ensure_columns` → `_add_column` 单列 ALTER，**不在 M1 范围**。这是预期内的，因为「为已有 stable 加少量新列」与「首次建表的 50 列爆发」是不同场景。
- 上游 `_write_table_rows` 已对 `all_cols` 用 `seen_cols` 去重，本步无需额外去重。

---

### M2：`config_version` 不匹配时条目不 XACK，直接留 PEL

```python
# mysql_mapper.py
if mapping.config_version != stream_config_version:
    return None

# consumer.py
if mapping is None:
    logger.warning("... leaving entry pending")
    return   # 既不写 TDengine，也不 XACK
```

版本不匹配（例如 MySQL 主从延迟导致的短暂不一致）时，条目进入 PEL 且在当次运行中不再被主循环消费。**S2 修复（定期 drain）是本问题的必要前提**：60s 内 drain 会重新 claim 这条消息，此时版本通常已对齐。

可选进阶修复：版本不匹配时降级使用旧版缓存（而非完全丢弃），降低数据延迟：

```python
# get_or_load 中，版本不匹配时尝试降级
if mapping.config_version != stream_config_version:
    old_cached = self._cache.get(key)
    if old_cached:
        logger.warning(
            "[mapper] version mismatch, using stale cached mapping (version %s)",
            old_cached.config_version,
        )
        return old_cached   # 降级：用旧版本，数据不丢，映射可能稍旧
    return None
```

**注意**：降级方案需确认业务上旧版映射不会导致列名错误写入（列被删减的情况）。

**置信度**：95%（根因确定；降级方案需业务确认）

**修复状态**：✅ 已实施（2026-05-23，`writer/mysql_mapper.py` `get_or_load`），采用降级方案：
- 版本不匹配时，若 `_cache[key]` 存在已缓存的旧映射 → 记 warning 标明 fallback 版本 → 返回旧映射。
- 若**无缓存可降级**（首次见该设备 + 立即遇到版本不匹配）→ 维持原行为返回 `None`，由 S2 在 60s 后 drain 重试，对齐后正常处理。
- 未更新 `_cache`：旧映射的版本号原样保留，下一次同设备同 stream_version 调用仍会重试 MySQL，对齐后才更新缓存（避免把不匹配的状态写入缓存形成持久错位）。
- `python -m py_compile` 通过。

**安全性论证**：
- TDengine schema 在本系统**只增不减**（仅 ALTER ADD，无 DROP）。旧映射的 `td_column_name` 在 TDengine 上必然仍存在，写入合法。
- 新版本新增的变量在 `_handle_entry` 的 `for var_id, str_val in raw_values.items()` 循环中找不到 `col_name` → `continue` 跳过。短期内丢失新增字段数据，可接受；S2 drain 触发版本对齐后该批进入正常处理。
- 已 deleted / disabled 的旧变量在 MySQL 查询时已被 `WHERE dv.deleted = 0 AND dv.enable = 1` 过滤；旧缓存若仍含该 var_id，相当于写入「业务上已废弃但物理仍存在」的列。对比当前行为（条目永久卡 PEL → DLQ → 数据彻底丢失），降级是更弱的副作用。

**与 S2 的关系**：
- S2 修「长窗口」（PEL 滞留消息有定期回收机制）。
- M2 修「短窗口」（短暂版本不匹配也能立即处理，不再每条都依赖 60s drain）。
- 两者互补：M2 减少进入 PEL 的频率，S2 兜底回收偶发 PEL 滞留（如首次见新设备 + 即时版本不匹配的情况）。

---

### M3：MySQL mapper cache miss 在事件循环线程中同步阻塞

`_handle_entry` 是同步函数，直接在事件循环中调用 `mapper.get_or_load()` → `load()` → `pymysql.cursor.execute()`。

- **cache hit**（版本不变的已知设备）：纯 dict 查找，无影响
- **cache miss**（版本变更 / 新设备）：发起真实 MySQL 同步查询，阻塞整个 asyncio 事件循环，期间无法处理其他 await

**修复 — 将 mapper 调用移到 executor**

方案一：`_handle_entry` 改为 async，mapper 调用走 `run_in_executor`：

```python
# run_loop 中改为 await
for entry_id, entry_data in entries:
    await self._handle_entry_async(stream_key, entry_id, entry_data, delivery_count=1)

async def _handle_entry_async(self, stream_key, entry_id, entry_data, delivery_count):
    ...
    mapping = await asyncio.get_running_loop().run_in_executor(
        None, self._mapper.get_or_load, device_code, group_code, config_version
    )
    ...
```

方案二（更简单）：在 `MysqlMapper.load()` 内部做 `run_in_executor`（需传入 loop）。

**置信度**：90%（缺陷确定；低负载或高 cache 命中率下影响较小，高负载或频繁版本变更时显著）

**修复状态**：✅ 已实施（2026-05-23，`writer/consumer.py`），采用方案一（`_handle_entry` 异步化）：
- `_handle_entry` 函数签名 `def` → `async def`。
- 函数内 `self._mapper.get_or_load(...)` 改为 `await asyncio.get_running_loop().run_in_executor(None, self._mapper.get_or_load, device_code, group_code, config_version)`，把同步 pymysql 查询挪到线程池。cache hit 路径开销可忽略（executor 调用即使 mapper 内部立即返回也几乎不耗时）；cache miss 时事件循环可继续调度其他 await。
- 三处调用方都加 `await`：
  - `run_loop` 主消费循环（H1 第 2 层 try/except 内）
  - `_drain_with_xautoclaim`
  - `_drain_with_xpending_xclaim`
- `asyncio` 已在文件顶部 import，无需新增。
- `python -m py_compile` 通过；`grep _handle_entry` 验证三处调用点全部带 await。

**未触碰的范围（不在 M3 内）**：
- `MysqlMapper` 自身保持同步实现；只在 consumer 侧的调用边界做 executor 切换。
- `_ack_single` / `_ack_ids` / `_retry_pending_acks` 等 Redis 同步调用仍在事件循环线程执行。M3 只针对 MySQL 阻塞这一具体场景；Redis 调用通常 < 1ms，若后续要全面非阻塞化可作为单独 follow-up。
- 同步与异步混用的语义没有变化：`_handle_entry` 内的 `_ack_single` 仍是同步直调，正确性不受影响。

---

### M4：`_drain_with_xpending_xclaim`（Redis < 6.2 fallback）无分页

`_drain_with_xautoclaim`（Redis >= 6.2）有正确的 while 分页循环，通过 `next_id` 分批处理所有 PEL 条目。

fallback 路径 `_drain_with_xpending_xclaim` 只调用一次 `xpending_range(count=batch_count)`，PEL 超过 `batch_count`（200）条时，其余条目本次启动无法被 reclaim，需再次重启才能处理。

**修复 — `consumer.py`：给 fallback 路径加分页循环**

```python
async def _drain_with_xpending_xclaim(self, stream_key: str, consumer_name: str) -> None:
    loop = asyncio.get_running_loop()
    cursor = "-"
    while True:
        pending_entries = await loop.run_in_executor(
            None,
            lambda: self._r.xpending_range(
                stream_key, self._group_name,
                min=cursor, max="+", count=self._batch_count,
            ),
        )
        if not pending_entries:
            break

        eligible_ids = [
            p["message_id"]
            for p in pending_entries
            if p.get("time_since_delivered", 0) >= self._pending_min_idle_ms
        ]
        if eligible_ids:
            claimed = await loop.run_in_executor(
                None,
                lambda: self._r.xclaim(
                    stream_key, self._group_name, consumer_name,
                    min_idle_time=self._pending_min_idle_ms,
                    message_ids=eligible_ids,
                ),
            )
            for entry_id, entry_data in claimed:
                dc = next(
                    (p["times_delivered"] for p in pending_entries if p["message_id"] == entry_id),
                    1,
                )
                self._handle_entry(stream_key, entry_id, entry_data, dc)

        last_id = pending_entries[-1]["message_id"]
        cursor = f"({last_id}"                      # exclusive，下一页从此 ID 之后开始
        if len(pending_entries) < self._batch_count:
            break                                   # 已是最后一页

    if self._buffer:
        await self._flush()
```

**置信度**：95%

**修复状态**：✅ 已实施（2026-05-23，`writer/consumer.py`）。实测改动：

**主修 — `_drain_with_xpending_xclaim` 加分页**
- 函数体整体改为 `while True` 循环 + `cursor` 游标分页。游标从 `"-"` 起，每轮取 `xpending_range(min=cursor, max="+", count=batch_count)`。
- 下页游标用 `f"({last_id}"`（Redis 区间排他下界语法），从上一页最后一条之后开始。
- 终止条件：本轮取空 `break`，或本轮 `len(pending_entries) < batch_count` 即已是最后一页 `break`。
- 处理逻辑保持：先按 `pending_min_idle_ms` 过滤 `eligible_ids`，再 `xclaim`，再 `_handle_entry`。
- **每页 flush 一次**（仅 `claimed` 非空时调 `await self._flush()`），与 `_drain_with_xautoclaim` 对齐，避免大 PEL 灌满 buffer；外层尾部 flush 因此移除。

**顺带 — H1 drain 路径兜底（你在 H1 关闭时建议「M4/M5 顺手补」，M5 不涉 drain，本步补齐）**
- `_drain_with_xautoclaim` 与新版 `_drain_with_xpending_xclaim` 的 `_handle_entry` 调用都包了 per-entry `try/except`。
- 异常处理与 H1 第 2 层一致：`logger.error` 跳过该条；**不 XACK**，留 PEL 由下一轮 drain 重试，达 `pending_max_retry` 上限后进 DLQ。
- 这样无论新消息路径还是 drain 路径，单条意外异常都不再中断本批次或整轮 drain。

- `python -m py_compile` 通过。

---

### M5：`write_batch` 成功后 XACK 失败，行已清除但 PEL 残留，致重复写入

**位置**：`consumer.py` `_flush`（约 385 行）、`_ack_rows`（约 399 行）、`_ack_ids`（约 408 行）

```python
ok = await self._writer.write_batch(rows)
if ok:
    self._ack_rows(buffered_rows)   # 成功的行不进 failed_rows
else:
    failed_rows.extend(buffered_rows)
...
self._buffer = failed_rows          # 已 ack 的行被清除
```

`_ack_ids` 的 `xack` 若抛异常，只打 warning：

```python
try:
    self._r.xack(stream_key, self._group_name, *chunk)
except Exception as exc:
    logger.warning("[consumer] xack failed for %s: %s", stream_key, exc)
```

此时这批行已写入 TDengine、已从 `self._buffer` 移除，但 Redis PEL 中并未 ACK。**S2 修复（定期 drain）上线后**，这些条目空闲超过 `pending_min_idle_ms`（30s）会被 reclaim，再次进入 `_handle_entry` → 二次写 TDengine。

**影响评估**：TDengine 对同 `(child_table, ts)` 重写是 last-write-wins，大概率**不损坏数据**，主要代价是重复 I/O 与 PEL 反复回收。故定级 **MEDIUM**。

**修复方向（重要：不要把已成功写入的行重新放回普通 buffer —— 那样反而更容易重复写）**：

- **方案一（推荐）**：单独维护一个 ACK 重试集合，记录 XACK 失败的 `(stream_key, entry_id)`，下一轮循环只重试 `XACK`，**不重新走写入路径**：

```python
# __init__
self._ack_retry: dict[str, set[str]] = {}   # stream_key → 待重试 entry_id 集合

# _ack_ids 失败分支
except Exception as exc:
    logger.warning("[consumer] xack failed for %s: %s", stream_key, exc)
    self._ack_retry.setdefault(stream_key, set()).update(chunk)

# run_loop 每轮尝试清空 _ack_retry，仅做 XACK，不写 TDengine
```

- **方案二**：显式接受「至少一次 + TDengine 幂等」语义，在文档与代码注释中写明，并将 XACK 失败日志提升为 error 级 + 增加监控计数，便于发现重复放大。

**置信度**：75%

**修复状态**：✅ 已实施（2026-05-23，`writer/consumer.py`），采用方案一（独立 ACK 重试集合）：
- `StreamConsumer.__init__` 新增 `self._ack_retry: dict[str, set[str]] = {}`，按 `stream_key` 索引待重试的 entry_id 集合。
- `_ack_ids` 失败分支：将当前 chunk 全部加入 `_ack_retry[stream_key]`。
- `_ack_single` 失败分支：将单条 entry_id 加入 `_ack_retry[stream_key]`。
- 新增 `_retry_pending_acks()`（置于 `_ack_single` 之后）：遍历 `_ack_retry`，**仅做 XACK，不重走写入路径**；XACK 成功的 entry_id 用 `difference_update` 从集合移除，整组空则删除 key；本轮某 chunk 失败即 break 等下一轮。
- `run_loop` 每轮**先**调用 `_retry_pending_acks()`，**再**走 S2 周期性 drain。空集时仅一次 dict 判空，开销可忽略。
- **P2 顺序修正（复审发现）**：初稿把 retry 放在 drain 之后。当某批已落库但 XACK 失败的条目刚好满足 `pending_min_idle_ms`（30s）时，drain 会先 reclaim 触发一次重写，再轮到 retry XACK —— 与 M5 「避免重复写入」目标相悖。已对调，retry 必须早于 drain。
- `python -m py_compile` 两轮均通过。

**语义保证**：行已成功落 TDengine，本步仅补 XACK 关闭 PEL 占位。**不重新走写入路径**，杜绝「S2 drain → 重写」造成的额外 I/O 与 PEL 反复回收。XACK 是 idempotent，即使在重试前已被 drain 一次也安全。

> 已知未覆盖点（不在本步范围）：`_ack_retry` 当前不限制大小；若 Redis 长时间完全不可用，集合会单调增长。但同期 `xreadgroup` 也会持续失败（已有 `await asyncio.sleep(1)` 退避），生产中 Redis 全断的窗口期通常短于 OOM 阈值。如需更稳，可后续加上限 + 溢出告警。

---

### M6：flush 持续失败时缺退避与日志降噪（从原 S1 拆出）

**位置**：`consumer.py` `run_loop` flush 段（约 144–150 行）

> 本问题原为前序报告 S1 的「修复 B」，按复审意见拆为独立问题。它是**健壮性 / 降噪改进**，不是导致 CPU 满载的根因（主循环已有 250–500ms 被动等待，见 S1 表述修正）。

S1 修复重连后，flush 失败应转为瞬态。但在重连尚未成功的窗口内，每轮循环仍会调用 `_flush` 并失败，按 250–500ms 间隔重试，每次失败产生一条 error 日志，长时间断连会刷屏。

**修复 — `consumer.py`：flush 失败后退避 + 日志降噪**

```python
# run_loop flush 段
if buffer_size >= self._max_buffer_rows or (
    buffer_size > 0 and now - self._last_flush >= self._flush_interval_s
):
    await self._flush()
    if len(self._buffer) > 0:          # flush 后仍有数据 → 本次存在失败
        self._consecutive_flush_fail += 1
        await asyncio.sleep(min(1.0 * self._consecutive_flush_fail, 10.0))  # 递增退避，上限 10s
        # 日志降噪：连续失败时降频记录（如每 10 次记一条），避免刷屏
    else:
        self._consecutive_flush_fail = 0
```

**置信度**：90%

**修复状态**：✅ 已实施（2026-05-23，`writer/consumer.py`）。实测改动：
- `StreamConsumer.__init__` 新增 `self._consecutive_flush_fail: int = 0`。
- `run_loop` flush 块在 `await self._flush()` 之后检查 `self._buffer`：
  - **仍非空（本次有失败）**：`_consecutive_flush_fail += 1`，退避 `min(1.0 * n, 10.0)` 秒；只在第 1 次和每 10 次记一条 warning，避免刷屏。
  - **已清空且之前 > 0（恢复）**：记一条 info 标记恢复，计数归零。
- `python -m py_compile` 通过。

**取舍说明（写盘侧 denoise 的范围）**：
- S1 关闭时复审提及「`_conn is None` 每轮一条 error + 一组 reconnect 日志」也属 M6 范围。
- 本步的递增退避把 flush 频率从原先 ~250–500ms 拉到 1–10s。写盘侧 `tdengine_writer._execute()` 的 `not connected` / `attempting reconnect` / `reconnect failed` 三组日志随着 flush 频率同步降到 ~1.5 条/秒以下，频次问题基本被退避吸收。
- 因此本步**仅做 consumer 侧退避 + 降噪**，写盘侧结构性 denoise（例如「连续 `not connected` 仅记首条 + 每 N 条」）未做。若实测仍嫌吵，可作为单独 follow-up。

---

### L1：buffer 满时先 sleep `flush_interval_s` 再 flush

```python
# consumer.py
else:   # buffer 满
    await asyncio.sleep(self._flush_interval_s)   # 先睡 500ms

# 之后才到 flush 判断，触发 flush
```

buffer 已满时本应立即 flush，当前逻辑导致最多额外等待 500ms 才真正落库。

**修复**：buffer 满时直接 flush 后 `continue`：

```python
else:
    await self._flush()
    continue
```

**置信度**：85%

**修复状态**：✅ 已实施（2026-05-23，`writer/consumer.py`），采用更简洁的「让底部 flush 块接管」方案，未用文档原稿的 `continue` 写法：
- 删除 `else` 分支里的 `await asyncio.sleep(self._flush_interval_s)`。
- 不加 `continue` —— 让控制流自然落到底部 flush 块，借 `buffer_size >= max_buffer_rows` 条件立即触发 `_flush()`。
- warning 文案从 `pausing xreadgroup until flush clears` 改为 `flushing immediately without reading new messages`，反映新语义。
- 失败时由 M6 的退避机制兜底，避免「buffer 满 + flush 失败」时空转。
- 收益：buffer 刚到上限时少等最多 `flush_interval_s`（默认 500ms）。
- `python -m py_compile` 通过。

> 与文档原稿差异：原稿 `await self._flush() + continue` 会绕过底部 flush 块，丢失 M6 的「flush 失败计数 + 退避 + recovered 日志」逻辑。让底部块统一处理更稳。

---

### L2：`_flush` 与 `write_batch` 双重按 `child_table` 分组

`_flush` 先按 `child_table` 分组，对每个 table 调用一次 `write_batch(rows)`（此时 rows 全是同一 child_table）。`write_batch` 内部再次按 `child_table` 分组，结果只有一个 key，逻辑冗余。

**修复**：`_flush` 中直接调用 `_write_table_rows`，或去掉 `write_batch` 内部的重分组。

**置信度**：100%

**修复状态**：✅ 已实施（2026-05-23，`writer/tdengine_writer.py`），采用「去掉 `write_batch` 内部重分组」方案：
- 全量代码梳理（grep）确认 `write_batch` 在仓库内仅被 `consumer._flush` 调用，且 `_flush` 在 `for child_table, buffered_rows in by_table.items():` 循环里逐表构建 rows 后调用 —— 全部调用方都已保证单 child_table。
- `write_batch` 简化为：`if not rows: return True; async with self._lock: return await self._write_table_rows(rows[0]["child_table"], rows)`。
- 模块顶部 docstring 与函数 docstring 同步收紧契约：「Caller MUST group rows by child_table before calling」。
- `_write_table_rows` 本身就以 `first = rows[0]` 取 stable/device_code，原本就假设同表，新契约只是把这一假设外显。
- 未触碰 `consumer._flush`：其按表分组 + 按表 ack/fail 跟踪是为了保留「部分表成功部分表失败」时的精确 ack 语义，不应合并到 writer 侧。
- `python -m py_compile` 通过。

---

### L3：`_get_delivery_count` 每条 entry 单独发一次 XPENDING_RANGE

`_drain_with_xautoclaim` 在拿到 N 条 claimed entry 后，对每条分别调用一次 `xpending_range(min=id, max=id, count=1)` 查询 delivery count，共 N 次 RTT。

**修复**：批量查询，一次 `xpending_range(min=first_id, max=last_id, count=N)` 取回所有计数后构建字典：

```python
# 批量取 delivery count
if claimed_entries:
    first_id = claimed_entries[0][0]
    last_id = claimed_entries[-1][0]
    pending_info = self._r.xpending_range(
        stream_key, self._group_name, min=first_id, max=last_id, count=len(claimed_entries)
    )
    dc_map = {p["message_id"]: int(p.get("times_delivered", 1)) for p in pending_info}
    for entry_id, entry_data in claimed_entries:
        self._handle_entry(stream_key, entry_id, entry_data, dc_map.get(entry_id, 1))
```

**置信度**：90%

**修复状态**：✅ 已实施（2026-05-23，`writer/consumer.py`）。
- `_drain_with_xautoclaim` 对本批 claimed entries 先收集 entry_id 列表。
- 新增 `_get_delivery_counts(stream_key, entry_ids)`，用一次 `xpending_range(min=first_id, max=last_id, count=len(entry_ids))` 构建 `entry_id → times_delivered` 字典。
- 处理单条 pending entry 时改为 `delivery_counts.get(entry_id, 1)`，查不到时兜底为 1。
- 旧的单条 `_get_delivery_count` 已替换，无残留调用。
- `python -m py_compile` 通过。

---

### L4：`_describe_columns` 的 TAG 识别依赖硬编码列名

```python
note = " ".join(str(item or "") for item in row[3:]).upper()
if col_name.lower() == "device_code" or "TAG" in note:
    continue
```

> **修正说明**：初版报告建议「仅保留 `'TAG' in note`、删除 `device_code` 硬判断」。复审核实后**撤回该建议**。代码注释明确写明：DESCRIBE 输出的 tag 元数据在不同 connector / TDengine 版本下格式不一致，部分版本**不含显式 TAG marker**，`device_code` 硬排除正是为兼容这种情况而有意保留的兜底。

当前真实风险仅在于：**未来若给 stable 新增第二个 tag**，且该 tag 名字不是 `device_code`、note 中又无 "TAG" 关键字，会被误识别为数据列。

**修复方向**：保留双重检查，不要删减。可选地把名字兜底改为「已知 tag 名集合」而非单一硬编码，并补注释说明意图：

```python
# 主判断：note 含 TAG（语义正确）；兜底：已知 tag 名（兼容无 TAG marker 的 connector）
_KNOWN_TAGS = {"device_code"}
if col_name.lower() in _KNOWN_TAGS or "TAG" in note:
    continue
```

**置信度**：70%（依赖 TDengine 具体版本的 DESCRIBE 输出格式，需在现场环境验证）

**修复状态**：✅ 已实施（2026-05-23，`writer/tdengine_writer.py`）。
- 新增模块级 `_KNOWN_TAG_COLUMNS = {"device_code"}`，把兼容兜底从内联硬编码改为显式集合。
- `_describe_columns` 保留双重判断：`col_name.lower() in _KNOWN_TAG_COLUMNS or "TAG" in note`。
- 行为不变：仍兼容 DESCRIBE 无显式 TAG marker 的现场 connector；后续若新增 tag，只需扩展 `_KNOWN_TAG_COLUMNS`。
- `python -m py_compile` 通过。

---

### L5：密码明文存储

`config/settings.toml` 中 MySQL 和 TDengine 的 `password` 字段明文存储。

**修复**：支持环境变量覆盖：

```python
# main.py 中
mc["password"] = os.getenv("MYSQL_PASSWORD", mc.get("password", ""))
td["password"] = os.getenv("TDENGINE_PASSWORD", td.get("password", ""))
```

**修复状态**：⏸ 推迟（2026-05-23 决定）。当前 `config/settings.toml` 仍以明文存储 MySQL / TDengine 密码。若后续部署对密钥外置有要求，可按上方 `os.getenv("MYSQL_PASSWORD", ...)` 模板补回 `main.py`，改动量小、与现有 `_validate_config` 兼容。

> 关联：本次同期实施了 stream 范围的 MySQL 自动发现（详见 L6 修复状态）。该工作虽与 L5 「shared config」概念相关，但**不解决密码外置问题**，两者独立。

---

### L6：无配置校验层

`main.py` 直接用 `config["writer"]["flush_interval_ms"]` 等访问配置，任何键缺失或拼写错误都会抛出 `KeyError`，无可读错误信息。

**修复**：入口做 required-key 检查：

```python
REQUIRED_KEYS = {
    "redis": ["host", "port", "db", "stream_shard_count", "group_name", "consumer_name"],
    "mysql": ["host", "port", "user", "password", "database"],
    "tdengine": ["host", "port", "user", "password", "database"],
    "writer": ["flush_interval_ms", "batch_count", "max_buffer_rows",
               "pending_min_idle_ms", "pending_max_retry"],
}
for section, keys in REQUIRED_KEYS.items():
    for key in keys:
        if key not in config.get(section, {}):
            logger.error("Missing config key: [%s] %s", section, key)
            sys.exit(1)
```

**修复状态**：✅ 已实施（2026-05-23，`writer/main.py`）。
- 新增 `_REQUIRED_CONFIG_KEYS`，覆盖 `redis` / `mysql` / `tdengine` / `writer` 四个 section 的必需键。
- 新增 `_validate_config(config)`，启动时在取 `config["..."]` 前统一检查缺失 section/key。
- 发现缺失项时输出 `Invalid config: missing required config key(s): ...` 并 `sys.exit(1)`，避免裸 `KeyError`。
- `writer.device_types` / `writer.group_codes` 不再作为必填配置：`writer/main.py` 启动时从 MySQL 的 `device_variable -> device_point_group -> device -> device_type` 自动发现 `(device_type, group_code)`，再生成 `rt:stream:{device_type}:{group_code}:{shard}`。
- 自动发现结果同时生成 `stable_names = ["sd{device_type}", ...]` 注入给 `TDengineWriter.connect()` 预热 schema 缓存，避免继续手工维护 stable 列表。
- `python -m py_compile` 通过。

---

## 附：设计文档与实际代码的背离

`入库模块代码设计逻辑.md` 描述的是一个**已被替换的旧设计**，与现有代码存在以下核心矛盾：

| 设计文档描述 | 实际代码 |
|---|---|
| `StreamConsumer` 继承 `threading.Thread`，多线程并发消费 | 纯 asyncio，单 consumer 实例 |
| `main.py` 使用 `dynaconf` 加载配置 | 使用 `tomllib`（标准库）或 `tomli` |
| `claim_pending` 在每次正常消费前调用 | 仅在 `run_loop` 启动前调用一次（即 S2 所描述的 bug） |
| 写入方法名 `write_points` | 实际为 `write_batch` |
| 3 × 1 × 4 = 12 独立消费线程 | 所有分片在单个 consumer 的 `xreadgroup` 中统一读取 |

设计文档需要整体重写以反映当前 asyncio 架构，否则会持续误导后续开发者。

---

## 建议执行顺序（历史记录）

> 以下为 2026-05-22 制定的优先级排序，实际执行顺序略有调整（如 M5 与 S2 紧邻、L1–L6 中部分顺带在 M4/L6 中完成）。完整结果见下方「总结」。

1. **S1**（TDengine 断连重连）— 最高风险，生产中 TDengine 任何闪断都会触发
2. **S2**（定期 drain PEL）— S2 修复后 M2 的严重程度大幅降低
3. **H1**（畸形消息隔离）— 改动小、收益高，可与 S2 同期或前后任意顺序
4. **M5**（XACK 失败处理）— 与 S2 同期落地：S2 的定期 drain 正是 M5 重复写入的触发条件
5. **M1**（建表一次性包含所有列）— 首次部署新设备类型时必然触发
6. **M4**（fallback drain 分页）— 补齐与 xautoclaim 路径的对称性
7. **M6**（flush 退避与降噪）— 依赖 S1，S1 修复后作为补充
8. **M2 / M3**（版本降级 / mapper 非阻塞）— 可视负载情况排期
9. **L1–L6** — 低优先级，单独处理

---

## 总结（2026-05-23 收口）

### 已闭合（14 / 15）

| 级别 | 编号 | 关键说明 |
|---|---|---|
| 🔴 严重 | S1 | 断连重连主体 + **P1**（`_conn is None` 分支也触发重连，避免首次重连失败后永久卡死）+ **P2**（`_is_connection_error()` 集中匹配 connection / timeout / broken pipe / eof / reset / refused / unavailable 等关键词） |
| 🔴 严重 | S2 | `_handle_entry`、xautoclaim 路径、xpending fallback 三处统一 `await` 化 |
| 🟠 高 | H1 | `_handle_entry` 内 `isinstance(dict)` 校验 + `run_loop` per-entry 兜底；drain 路径兜底见 M4 |
| 🟡 中 | M1 | `_ensure_stable` 一次性带全部初始列 |
| 🟡 中 | M2 | 版本不匹配时降级使用旧缓存映射；不污染 `_cache` |
| 🟡 中 | M3 | `_handle_entry` → async；mapper 调用走 `run_in_executor` |
| 🟡 中 | M4 | xpending fallback 加 cursor 分页 + 每页 flush；**顺带**给两条 drain 路径补 per-entry try/except（H1 follow-up） |
| 🟡 中 | M5 | 独立 `_ack_retry` 集合，仅补 XACK 不重写；**P2 顺序修正**：retry 早于 drain，关闭重写窗口 |
| 🟡 中 | M6 | `_consecutive_flush_fail` + 退避 1×n（≤10s）+ 首/每 10 次记录 + recovered 日志 |
| 🟢 低 | L1 | buffer 满时去掉冗余 `sleep`，让底部 flush 块接管（保留 M6 退避） |
| 🟢 低 | L2 | `write_batch` 去掉内部重分组，契约收紧为「单 child_table」 |
| 🟢 低 | L3 | xautoclaim 路径单条 XPENDING_RANGE → 单次批量 + dict 查 |
| 🟢 低 | L4 | 模块级 `_KNOWN_TAG_COLUMNS = {"device_code"}` 显式集合替代内联硬编码 |
| 🟢 低 | L6 | `_REQUIRED_CONFIG_KEYS` + `_validate_config()`；**顺带**新增 MySQL 自动发现 stream scope（`_STREAM_SCOPE_SQL` + `_discover_stream_scopes()`），原 `device_types`/`group_codes`/`stable_names` 静态配置已从 `settings.toml` 删除 |

### 已推迟（1）

- **L5**（密码 env var 覆盖）：未实施，接受当前 `settings.toml` 明文方案。模板可见 L5 详情段，如有需求作为独立 follow-up 单独提。

### 已记录、未处理（残留风险）

- TDengine 写盘侧日志结构性 denoise（M6 仅做 consumer 侧退避；写盘侧靠 flush 频率被动降噪到 ~1.5 条/秒）。
- `_ack_retry` 无大小上限（Redis 长时间全断时单调增长，恢复后 idempotent xack 清空）。
- `_drain_with_xpending_xclaim` 自身无独立 try/except（被外层 `drain_pending` 兜住，但 fallback 自身异常会从 fallback 分支冒出）。
- `_describe_columns` 的 TAG 识别若未来加非 `device_code` 名的新 tag 且 connector 不输出 TAG marker，需扩展 `_KNOWN_TAG_COLUMNS`。
- `MysqlMapper` 共享单一 PyMySQL 连接：当前 `_handle_entry` 是逐条 `await`、不并发；若后续把 entry 处理改为并发，需加锁或连接池。
- `_get_delivery_counts` 在多 writer 实例 + [first, last] 区间内有 N 条以上 pending 时可能返回非 claimed 条目，导致部分 claimed 条目的 delivery_count 默认为 1（不影响主流；可能让高 retry 条目晚进 DLQ）。
- **type_code 大小写约定**：`_STREAM_SCOPE_SQL` 用 `LOWER(dt.type_code)` 强制小写，collector 一侧必须保持小写写入 `rt:stream:{device_type}:...` 才能匹配。建议要么 collector 端也 `.lower()`、要么在 SQL 注释里写明此约定。
- `MysqlMapper` cache hit 也走 executor，多一次线程池切换。低优化，留作 follow-up。
- 设计文档 `入库模块代码设计逻辑.md` 描述旧 `threading.Thread` 架构（见上方「附」），与当前 asyncio 实现严重背离，建议单独重写。

---

*文档创建：2026-05-22 ｜ 复审整合修订：2026-05-22*
