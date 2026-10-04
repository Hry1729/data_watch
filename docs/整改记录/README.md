# 整改执行台账（批次 A）

本目录是《Data_Watch_综合审查与Agent整改执行清单_6845e80.md》整改过程的证据台账。结构约定：

| 文件 | 内容 | 负责人 |
|---|---|---|
| `README.md`（本文件） | 台账索引、条目状态总表、分工与波次 | 协调者 |
| `隔离测试环境.md` | 环境事实源：现场栈禁区、隔离环境连接参数 | 协调者 |
| `W0-A-基线.md` | Git 基线、生产运行入口、D39 确认矩阵、测试执行方式 | W0-A |
| `W0-B-P0任务卡.md` | 批次 A 各条目在当前 HEAD 的真实定位与复现设计 | W0-B |
| `<编号>.md` | 单条目执行记录（文档 §8 格式） | 对应修复 Agent |
| `S3-*.md` | S3 本机信任边界核查记录 | S3 Agent |
| `W1-验收-*.md` | 独立验收 Agent 的反证记录 | 验收 Agent |

## 执行纪律（所有 Agent 必须遵守）

1. 只改自己负责的条目涉及的代码；**禁止顺手重构、禁止优化相邻代码、禁止删除既有死代码**。
2. 同一时刻一个热点文件只能有一个 Agent 写入。热点文件归属见下表。
3. 每条改动必须走完整循环：**重新定位 → 最小反例/证据 → 最小修改 → 回归 → 文档同步 → 记录未覆盖边界**。
4. 复现不成立时，提交明确反证，**不得为了完成条目强行造 Bug**。
5. 测试结论必须标注等级：**纯函数 / 单元 / 真实入口 / 隔离集成 / 目标机验收**。
6. 禁止用以下手段换绿色结果：恢复 MAXLEN 随意裁剪、跳过质量行、减少采样负载、去掉鉴权、删除正确性断言。
7. 不 commit、不 push、不部署、不迁移业务数据；不 reset/clean/stash；保留用户所有未提交改动与历史证据。
8. 不访问真实 PLC；不碰 `data-watch-*` 现场容器（见 `隔离测试环境.md`）。

## 热点文件唯一负责人

| 热点文件 | 归属 | 涉及条目 |
|---|---|---|
| `edge/app/outbox.py`、`edge/app/uploader.py`、`edge/uploader_main.py` | W1-E1 | B5, B6, B25 |
| `edge/app/storage_service.py`、`edge/storage_main.py` | W1-S3 | S3 |
| `agent/app/routers/admin.py`、`agent/app/services/health_checker.py` | W1-A1 | B11 |
| `agent/app/core/config.py`、`security.py`、`deps.py` | W1-A1 | B15 |
| `backend/app/services/catalog.py`、`api/routes/catalog.py`、`api/routes/distributed_nodes.py` | W1-I1 | B7 |
| `ingest/app/repository.py`、`ingest/app/service.py` | W1-I1 | B7 |
| `cli/update_manager.py` | W1-C1 | B41 |

## 批次 A 条目状态总表

状态取值：`待开始` / `进行中` / `已修并验证` / `已存在修复` / `不成立有反证` / `仅文档已修` / `待目标环境验证` / `需架构决策`

| 编号 | 问题 | 优先级 | 归属 Agent | 状态 | 证据等级 |
|---|---|---|---|---|---|
| B5 | 同批已 ACK 帧因后续 session 异常被再次迁移重传 | P0 | W1-E1 → **W1-E1b 接手** | 代码已实现，待独立验证 + 报告 | 单元（待验） |
| B6 | 网络重试与永久失败隔离共用 attempt_count | P0 | W1-E1 → W1-E1b → 独立验收 agent | ⚠️ **计数分离已修并验证**；`dead` 人工恢复/保留规则**未定义**（残留，见下） | 单元 + 隔离集成 |
| B7 | 新点位已发布而 Ingest 缓存仍旧导致永久拒收 | P0 | W1-I1 → 验收发现遗漏 → 补齐轮 | ✅ **已修并验证（含补齐轮）**：原覆盖 2 个 reason，补齐后 4 个（`unknown_var_id` / `unknown_config_version` / `unknown_device` / `device_type_mismatch`）；6 条用例已移植进仓库 | 单元 + 隔离集成 |
| B11 | Agent 健康检查把已有凭据用于可覆写的新目标 | P0 | W1-A1 | ✅ 已修并验证 | 隔离集成 |
| B15 | 已存在 Agent 配置可保留空 jwt_secret | P0 | W1-A1 | ✅ 已修并验证（agent 253→301） | 单元 + 真实入口 |
| B25 | Uploader 在获得单实例锁前执行 recover_inflight | P0 | W1-E1 | ✅ 已修并验证（4 passed） | 单元 |
| B41 | CLI 更新 swap 前半失败时 catch 可删除仍有效的 current | P0 | W1-C1 → 验收发现 R9/R10/R11 → 后续补齐 | ✅ **已修并验证（含 R9/R10/R11 补齐）**（cli 98 → **124 passed**） | 隔离集成 |
| S3 | Edge Storage 本机 HTTP IPC 未认证即可改变 Outbox/配置状态 | P1 | W1-S3 | ✅ 已修并验证（edge 43→56，6 类攻击全 401） | 隔离集成 + 真实入口 |
| D39 | DW-01 仍把已撤销的中心 SQLite+WAITAOF 写成当前实现 | P1 | D39 agent | ✅ 已修完（DW-01 重建 + `:40` + §8.5 + 同文件同类清扫 3 冲突/10 补注/穷尽性自证） | 仅文档已修 |

**批次 A 剩余唯一未完成项：B6**（见「事件记录：W1-E1 中途失败」）。B6 的落地阻断已解除（S3 交付后释放了 `storage_client.py`/`storage_service.py`），W1-E1b 已获授权按**路线 B** 落地。

### 现场栈安全基线复核结果（整改全程）

| 项目 | 开工前基线 | 复核（W1 全部交付后） | 判定 |
|---|---|---|---|
| `data-watch-frontend` | `Up 17 minutes` | `Up About an hour` | ✅ 未重启 |
| `data-watch-backend` | `Up 17 minutes` | `Up About an hour` | ✅ 未重启 |
| `data-watch-redis` | `Up 17 minutes` | `Up About an hour` | ✅ 未重启 |
| `data-watch-mysql` | `Up 17 minutes` | `Up About an hour` | ✅ 未重启 |
| `data-watch-tdengine` | `Up 17 minutes` | `Up About an hour` | ✅ 未重启 |
| 现场 Redis `db0` 键数 | **10** | **10**（`keys=10, expires=0`） | ✅ 零写入 |

**结论：整改全程对现场栈零写入。** 另：S3 开发中途生成的仓库内产物 `edge/data/storage-ipc-tokens.json` 已由其本人删除，`Test-Path edge\data` 实测为 `False`，仓库无残留。

**D39 新发现（未处理，待批准）：** `docs/01-总体设计/分布式部署架构方案.md` **§8.5 `:726-733`** 是同源但**更严重**的反向冲突 —— `:728` 写「ACK 含义 = Redis XADD 成功」、`:733` 写「**不承诺强 ACK**：第一阶段**不实现**等待副本/持久化确认」，而 `ingest/app/redis_store.py:365-368` 的 `numlocal>=1` 门禁**已经实现**。该位置未被清单与 W0-A §3.4 收录，D39 agent 刻意未改。协调者已独立复核该冲突成立。**注意**：批准后必须与已修的 `:40` 一起改，否则同一文件内会留下「`:40` 已更正、§8.5 仍错误」的自相矛盾。

## 本轮新增的安全约束（2026-06-12 核实）

1. **现场栈禁区**：`data-watch-redis` / `data-watch-mysql` / `data-watch-tdengine` / backend / frontend 全部禁止写入或重建。详见 `隔离测试环境.md` §1。
2. **隔离环境已就绪**：Redis `127.0.0.1:16379`（哨兵密码，7.4.11，AOF 开）、MySQL `127.0.0.1:13306`（哨兵密码，库 `device_watch_test`）。ingest 集成测试所需的预置标记已写入隔离 Redis 的 DB 1。
3. **默认值即现场**：`ingest/app/config.py:40-45` 与 `backend/app/core/config.py:168-169` 的默认连接参数直接指向现场栈（含 root + 空密码）。**不显式设置环境变量就跑测试 = 连现场。** 环境变量契约见 `隔离测试环境.md` §4。
4. **现有 Redis 守卫不足以证明隔离**：`ingest/tests/redis_test_guard.py` 只要求回环 + 非零 DB，而现场 Redis 有 16 个 DB，`...:6379/15` 能通过守卫。不得以「守卫通过」作为隔离证据。
5. **backend 测试全部是 mock 级**：39 个测试文件均使用 `AsyncMock`/`FakeSession`/`monkeypatch`，不连任何库。因此 backend 侧条目只能给「单元」级证据；B7 若要「隔离集成」级证据必须新建测试。

## 证据等级定义（每条结论必须标注其中一个）

| 等级 | 含义 |
|---|---|
| 纯函数 | 隔离进程内复制纯逻辑复现，无项目装配 |
| 单元 | 项目测试套件中的 mock/fake 级测试 |
| 真实入口 | 通过真实生产入口（子进程/真实 CLI/真实 ASGI app）验证 |
| 隔离集成 | 连接隔离环境中的真实 Redis/MySQL/TDengine 验证 |
| 目标机验收 | 在现场目标机/真实 PLC 上验收（本轮不授权，一律标为未覆盖） |

## 进行中的设计裁决（W1 期间）

### B6 的计数器分离与 IPC 边界（已裁决，**已落地并验证**）

**问题（W1-E1 提出，协调者已独立核实）：** 实现「传输尝试 / 协议拒绝 / 数据错误」三类计数分离时，Uploader 要按协议拒绝计数判定 dead，就必须能**读回**该计数。当时实测确认：**跨 Edge IPC 的每行计数字段只有 `attempt_count` 一个**（`outbox.py:33/36`、`storage_client.py:282/293`、`storage_service.py:376/387`，schema 中不存在 `reject_count`）。

**裁决与实际落地：** 最初我倾向「不需要 IPC 读回」的设计，但 E1b 论证后改为**路线 B（回读计数）** 更符合单一事实源（不需新增 IPC 参数、策略仍只在 `UploaderSettings`、计数只在 `reject_count` 一列）。最终实现：`reject_count` 全链路接通 —— 建列 + 补列迁移 + `OutboxRecord` 字段 + `claim` SELECT + IPC 双向序列化；`uploader.py` 的 `_quarantine_budget_exhausted()` **只看 `reject_count`**，`None`（旧版对端未回传）时**显式降级**为 `attempt_count` 语义并**告警一次**，不静默当成 0。

**当时的文件所有权阻塞已解除：** 两文件曾由 **W1-S3 独占**（IPC 认证），故 B6 的 IPC 部分一度标为「待落地」；S3 交付后即由 W1-E1b 落地，并由**独立验收 agent**（非修复者）反证通过。

**回注反证证明该修复非空转：** 把判定改回旧语义 `record.attempt_count + 1`，自建独立脚本从 `state=pending` 变为 `state=dead`（exit 1）；5 组回注全部「变红 → 字节还原 + sha256 一致」。反向检查确认**真正非法的载荷仍会被隔离**（400 契约拒绝 ×3 → `dead`，`last_error=http_400_contract_rejected`），没有矫枉过正。

**拒绝的中间状态（仍然有效）：** 绝不允许「HTTP 生产路径读到 0、单元测试直连 OutboxStore 是另一套行为」的静默分裂。

**B6 的残留未完成部分（如实记录）：** 清单 227 行要求「**明确定义 `dead` 的人工恢复、保留与删除规则**」，这一半**尚未完成** —— 仓库内没有人工重放入口，`dead` 记录仅由 `maintenance` 按 7 天自动删除。计数的**分离**已修并验证；`dead` 的**恢复/保留契约**仍待定义（需用户决策）。

## 独立验收的实质产出（G3：验收发现了修复者漏掉的东西）

独立验收不是走过场 —— B7 组的验收者查出**两处与修复者报告不符**，我已逐条独立核实并确认成立：

### 1. 修复者的 TTL 口径错了 5 倍（已确认）

`B7.md` 称残留冷却窗口"默认 5000ms×1.2≈6s"。实际**生产走的是 `IngestSettings.from_env()`**，其默认值在 `ingest/app/config.py:61-62` 是 `os.getenv("INGEST_CONTROL_CACHE_TTL_MS", "30000")` = **30000ms**；仓库自己的 `ingest/tests/test_config.py:49` 就断言 `30_000`，且全仓库无环境变量覆盖。

`config.py:30` 的 `5000` **只是 dataclass 字段默认值，生产不经过它**。修复者读了字段默认值而非 `from_env()` 默认值。

⇒ **残留窗口上界应≈30s（缓存侧≈36s），report 低估 5 倍。** 已要求补齐 agent 在 `B7.md` 中更正，并注明原先读错的原因，避免下一个人再犯。

### 2. 未披露缺口：`unknown_device` 不在可重读集合（已确认）

`ingest/app/repository.py:25`：`_REREADABLE_REASONS = frozenset({_UNKNOWN_VAR_ID, _UNKNOWN_CONFIG_VERSION})` —— **只覆盖两个 reason**。而 `:496` 会产生 `unknown_device` 拒绝，assignment 缺失被**负面缓存**；控制面**补建 assignment** 且失效通知丢失时**不会触发重读**，合法帧仍被判 `rejected_consumed` —— 而 `edge` 会把它记为 `dead` 且**永不重试**，即真实数据丢失。

**这直接违反 B7 自己的验收条件「合法帧不得 `rejected_consumed`」。** 修复者当初只登记了 `device_type_mismatch`，漏了 `unknown_device`。

### 3. 验收者同时证实了修复者的关键主张（不是只挑错）

- **反向检查通过**：映射外 point_id、`config_version` 领先、`node_uuid` 不匹配、`device_type` 不匹配**全部仍被拒绝**，且是在**证明"确实发生过权威重读"**的前提下（对应命名空间读 +1、另一命名空间 0 增量）——「为消除误拒而删掉拒绝逻辑」这一最危险的假修复**已被排除**；`_validate_item_row` 等 6 个关键函数与 HEAD **逐字节相同**，"判定规则一字未改"为真。
- **残留冷却窗口真实存在**：被 `dead` 永不 `claim` ⇒ 是**有界但真实的数据丢失**，不是"仅延迟"。这个定性正确。
- 8 并发冷 miss → 8/8 读，**与 HEAD 完全相同**（既有 miss stampede，非 B7 引入、也未修）。

**协调者的判断：** 把 `unknown_device` 与 `device_type_mismatch` 纳入既有重读机制，是 B7 范围内的**补齐**（同一机制的既有 reason 集合扩展），不是架构变更；真正需要架构决策的是**残留冷却窗口**（要把缓存依赖型驳回改成可重试状态）。已派补齐 agent 执行，并要求它先复现、再最小补齐、再做反向检查、最后更正报告。

## 独立验收发现汇总（本批次最重要的产出）

**独立验收不是走过场。** 截至当前，验收层查出的问题全部成立，其中数条是**修复者自述里漏掉或说错**的：

| # | 条目 | 发现 | 性质 | 处置 |
|---|---|---|---|---|
| 1 | B7 | 残留窗口 TTL 口径**错 5 倍**（报告称 ~6s，实际 `from_env()` 默认 **30000ms** → ~30s） | 事实错误 | 补齐 agent 更正报告 |
| 2 | B7 | `unknown_device` 分支**完全未覆盖**（`_REREADABLE_REASONS` 仅 2 个 reason）→ 合法帧仍 `rejected_consumed`，edge 记 `dead` 永不重试 = **真实数据丢失**，违反 B7 自身验收条件 | 漏掉的分支 | 补齐 agent 纳入既有机制 |
| 3 | B7 | `device_type_mismatch` 同样未纳入重读集合 | 漏掉的分支 | 同上 |
| 4 | B41 | **R11：崩溃恢复在真实 CLI 入口不可达** —— `current` 缺失时 `_require_windows_install` 先抛 `CliError`，脚本顶部 reconcile 走不到；`_launch_swap` 全仓库仅 `apply`/`rollback` 两个调用点。修复者的"崩溃后重启"用例是**直接调 PowerShell 脚本**、绕过 CLI 入口，故未暴露 | **功能缺口** | B41 后续 agent 修复 |
| 5 | B41 | **R9**：注入写 `update:swapped` 失败 → `current` 是**从未通过自检的新版本**，推翻报告"任何失败路径下 current 一定是旧版本" | 过度声称 | B41 后续 agent 处理 |
| 6 | B41 | **R10**：reconcile 先永久删 `failed`(OLD)，`previous` 已半截 → `current` 消失 | 边界缺陷 | 同上 |
| 7 | B41 | `apply()` 仍提前返回 `updateStarted:true`，与清单字面"不再提前报"不符 | 未满足字面要求 | 明确处置或说明 |
| 8 | D39 | 自证"被替换原文逐字保留、一条未删"对 §0 冻结项第 10 行（原 `:40`）**不成立**（该处就地改写、无副本） | 自证不实 | D39 后续 agent 更正 |
| 9 | D39 | DW-01 仍留易变行号且已漂移（`:330`→`:331`），与其"只引函数名"的声称矛盾 | 声称不实 | 同上 |
| 10 | B11 | **R3：代理泄漏** —— 启动前设 `HTTP_PROXY` 后 **TDengine 存储口令被发给代理**（`proxy_requests=1, direct=0`） | **可复现凭据泄漏** | ✅ **已修并验证**（`_NoProxyHandler`，仅影响该 opener） |
| 11 | B11 | **R1：明文 `password` 配置形态**时绑定检查失效，凭据跟随新目标 | **可复现凭据泄漏** | ✅ **已修并验证**（`_has_stored_credential`：密文或非空明文均算） |
| 12 | B11 | `_HOST_PATTERN` 用 `$` 而非 `\Z` → 尾随换行 host 被接受（无逃逸，非泄漏） | 声称不实 + 正则应收紧 | ✅ **已修并验证**（`$`→`\Z`，含 `_IPV6_PATTERN`） |
| 13 | S3 | **`_is_loopback("")` 空串绕过** ⇒ `--host ""` 未拒、实测监听 **0.0.0.0**，从非回环地址请求 `/healthz` 得 **HTTP 200**，违反「不要将 Storage 开到全部网卡」；**且修复者自己的用例 `test_storage_ipc_auth_s3.py:473` 把 `""` 断言为回环（测试把缺陷当成期望）** | **安全缺口 + 测试编码缺陷** | ✅ **已修并验证**：空串/纯空白一律 `False`；`--host ""` → rc=2、无监听、两端拒连、端口可再绑定；那条断言**改为 ∈ 非回环组（未删除）**；`docs/04-运维部署/README.md:589` 已同步 |
| 14 | S3 | **动作白名单缺 `outbox.upload_interval`** ⇒ `scripts/simulate_edge_collector.py:271` 收到 **403** → 未捕获异常、**rc=1 崩溃**；修复者称该脚本"不受影响"**被证伪** | **功能缺口** | ✅ **已修并验证**：选 (b)（改脚本用显式 `role="uploader"` 只读客户端，**不吞异常**；为脚本放宽 collector 白名单会削弱最小权限模型）；改后 **rc=0、generated=10 envelopes**；**全量调用点审计**（角色×动作，含 `scripts/`）collector 5/5、uploader 全内、agent 4/4 → **无第二处遗漏** |
| 15 | B25 | **锁路径与 `edge_database_path()` 不同源**：同 `DATAWATCH_EDGE_DB` + 分指 `DATAWATCH_DATA_ROOT` → 两实例各持己锁同时存活；B 把 A 的**未过期 live 租约**改成 `pending` ⇒ **违反 B25 验收要求第 3 条** | **验收要求被实测违反** | ✅ **已修并验证**：选**同源**（`lock_path = edge_database_path().parent/"uploader.lock"`，默认部署行为不变，且不像"拒绝启动"那样牺牲一种本可用配置）；反向（真实入口）：B 被同一把锁挡住 → `rc=1 another uploader instance is running`、无 `Recovered`、outbox 逐行不变、A 存活；**回注旧派生 → 新用例变红**，注入已删、sha256 复原。**第 2 例"测试把缺陷当期望"**：`test_uploader_main.py:39` 已改为断言**锁随库走**（其余断言未动） |
| 16 | S3 | 客户端把 **401/403 折叠成 "unavailable"**，无法区分认证失败与服务不可达 | 可观测性（P2） | ✅ edge 侧**已修并验证**（新增 `StorageUnauthorized(StorageUnavailable)` 子类、向后兼容；401/403 立即抛出、不重试不折叠；反向：无凭据→401、越权→403 且消息不含 `unavailable`、无人监听→`StorageUnavailable` 且非该子类）。**agent 侧** `agent/services/edge_storage_client.py:100-101` 原为遗留，**已授权修复中** |
| 17 | B25/S3 | 报告不实：`B25.md` 称 `uploader.py` 改动 `+9/-1`，实际 **+71/-12**（同文件含 B5/B6）；`S3.md` 改动表**遗漏** `storage_service.py`/`storage_client.py` 里的 B6（`reject_count`/`http_status`）改动 | 声称不实 | ✅ **两份报告均已更正**（并注明 `edge_storage_client.py` 的越界改动"经协调者批准"） |
| 18 | B41 | **R11：崩溃恢复在真实 CLI 入口不可达**（详见上文） | 功能缺口 | ✅ **已修并验证**：脚本加 `[switch]$ReconcileOnly` + `_recover_interrupted_swap()` 在 `_require_windows_install` **之前**恢复并重新校验；`apply()` 现真正走到 `_launch_swap`、`current=OLD`、`state=update:recovered`；反向控制证明前置校验未放宽 |
| 19 | B41 | **R9**：`current` 变成从未通过自检的新版本 | 过度声称被推翻 | ✅ **已修并验证**：`catch` 改为「阶段值 + 目录状态」交叉判定 + 阶段写入改临时文件原子替换；改后 `current=OLD`、`failed=NEW` |
| 20 | B41 | **R10**：reconcile 先永久删 `failed`(OLD) → `current` 消失 | 边界缺陷 | ✅ **已修并验证**：`failed` 回收移入 `try`，并改为「确认 `$StagedApp` 完整**且** `current` 完整」才删；改后 `current=OLDER`、`failed=OLD` 都在 |

**B41 后续的工程质量亮点（值得记）：**

- **主动拒绝了一个更宽的判据**：对 R9 它没有采用"与阶段值无关"的宽判据，理由是那会在手工/异常 `-StagedApp` 场景**重现 R3 类缺陷** —— 即"多修一点"反而引入新缺陷，它选择窄判据。
- **恢复逻辑仍只有 PowerShell 一处实现**（未在 Python 侧复制一份），并说明了为何 `rollback()` 不能盲目让脚本重跑（会把刚恢复的 `current` 又搬进 `failed`）。
- **反向控制用例**：无状态文件且无 `current`、或恢复不出 `current` 时**仍抛 `CliError`** —— 证明前置校验没有被放宽、恢复不了时不伪造成功。
- **主动披露一次未复现的间歇失败**：既有崩溃注入用例退出码非 97；之后 12 次全量 + 10 次 `-k crash` + 160 次脚本级压力**均未复现**，根因未定位，且**不涉及版本丢失**。已如实登记为未定位项而非掩盖。
- **`updateStarted` 的处置有据**：它选 (b) 并引用 `W0-B-P0任务卡.md:698`，我核实该处**确实明示允许**等价路线（"或该字段语义改为『已提交待验证』并带独立验证入口"）。**但这暴露了我两份文档前后不一致** —— `W1-验收协议.md:108` 按字面写死。已在协议中就地更正并注明责任在协调者。
- 残留（已登记）：`current` 同时是 CLI 自身安装位置，本修复的**可达性前提是"用户仍能启动 CLI"**；彻底解法需命令层 `recover` 子命令或安装脚本 shim，超出授权文件范围，未做。

| 21 | S3 | **`_is_loopback` 反向过度拦截** —— 我假设 `127.1`/`localhost.`/`[::1]` 三项都被过度拦截 | 我（协调者）的假设**部分错误** | ✅ **已修并经独立验收证实，但只有 1 项成立**：实测仅 **`localhost.`**（DNS 根形式）是真过度拦截，已修；`127.1`/`[::1]`/`127.0.0.1.` 经 `getaddrinfo`/`bind` 实测报 `gaierror`，放行也绑不上，故按实测拒绝、**不为迎合我而放宽** |
| 22 | B25 | 锁文件每次失败 **append 1 字节**（`SingleInstanceLock.__enter__` 以 `"a+b"` 无条件 `write(b"0")`，且在 `msvcrt.locking` 之前） | 轻微资源问题（P2） | ✅ **已修并经独立验收证实**：守卫移出 `os.name` 分支，与 `collector_outbox.py:166-176` 归一化后**逐字节同构**；验收者把争锁失败从仓内 5 次**加到 20 次**，size **恒为 1**；跨进程互斥/释放后可再获取/kill 持锁子进程后可立即恢复均成立；内容恒 `b"0"` |
| 23 | S3 | **`::1` / `0.0.0.0` 之外的 bind 失败也会挂死**（端口占用 WinError 10013、`EADDRNOTAVAIL` 10049 实测 6s 仍存活、rc=None；根因 `engine.start()` 起非守护线程，异常在 start() 之后、try/finally 之前） | **要求未满足**（不只待验证） | ✅ **已修并经独立验收证实**：bind 移到 `engine.start()` 之前 + `_bind_error_message()` 分类；验收者 9 次运行**全部 ≤0.23s 自行退出、无挂死**，三类文案在断言层**互斥且各只命中一类**，四个合法回环写法仍 `healthz=200`、非回环全不可达 |
| 24 | S3 / agent | `test_s3_storage_ipc_token.py:136,148` 以 `match="Edge Storage unavailable"` 断言，导致 401/403 消息**保留不准确前缀** | **第 3 例"测试固化了不准确的期望"** | ✅ **已修并经独立验收证实**：断言强度**真的上升**（`RuntimeError` → 严格子类 `StorageUnauthorized` + 要求 `HTTP 401`）；验收者确认 `"unavailable" not in` **有判别力**（旧文案满足 `match="HTTP 401"` 却会被它判死，非恒真）；并自加反例 **HTTP 500 → `StorageUnavailable`，未过度归类** |

### A/B/C 三项的独立验收结论（批次 A 最后一块）

**裁决：A 证实｜B 证实｜C 证实**（真实入口 + 隔离集成；B 的 POSIX 分支因本机为 Windows 未覆盖）。

**"快照是否真的落地"结论（用观察者 14:47:39 快照对照）**：A **在快照前已落地**；B 快照时是**半成品**（守卫仍在 `os.name=="nt":` 内，快照 seq122 实读铁证），最终 14:50:53 移出分支；C 快照时**完全没落地**（无 `_bind_error_message`），最终 14:59:37 / 15:00:39 落地。⇒ **三项最终都真的落地，且 B/C 的落地发生在快照之后** —— 这恰好解释了我为何会在 14:47 误判为"未应用"。

**验收者查出的自述不符（4 条，均已记录）**：
1. C 耗时报告称 1.23s，实测 **0.21–0.23s**（结论同、数字无法复现）；
2. 报告演示用 `192.168.1.99`、仓内回归用 `192.0.2.1`，同分类但非同场景；
3. A 的"与 edge 侧同口径"**非逐字一致**（agent 侧无 `role=` 段）；
4. **未声明改动**：`docs/04-运维部署/README.md` 14:57:41 被改（内容正是 C 的三类分类）—— 属 S3 授权文件，但**不在 A/B/C 自述清单内**。

**验收者的方法学自查（值得记入方法库）**：它第一版 C④ 探针的两条 FAIL **全是自身探针错误** —— 探错 `127.0.0.2` 地址；"任意公网地址可连接"实为**本机透明代答伪影**（`192.0.2.1`/`203.0.113.9`/`198.51.100.7` 在无人监听端口**全部 connect 成功**）。**若沿用该判据会凭空造出假缺陷**，它主动披露了这点。

**仍未覆盖**：`engine.start()` 自身失败（DB 初始化）**仍可能挂死，未改未验**（与修复前同类）；B 的 POSIX `flock` 分支无法本机执行；跨账户 ACL / 目标机；IPC 入口 IPv6 支持（只登记）。

**edge 遗留修复轮的正面证据（不是只挑错）：**

- P1-A 反向：`--host ""` → **rc=2**、`netstat` 无监听、回环与非回环**均拒连**、端口可再绑定；空 `DATAWATCH_EDGE_STORAGE_HOST` 同样被拒。
- P1-B 反向：脚本 **rc=0**、`generated=10 envelopes`、真实写入 outbox 行（**不是把 403 吞掉**）。
- P1-C 反向（真实入口）：B 被同一把锁挡住 → `rc=1 another uploader instance is running`、**无 `Recovered`**、outbox 逐行不变、A 存活、rootB 下**无第二把锁**。
- P2-D 反向：无凭据 → 401；collector 越权调 `config.apply` → 403 且**消息不含 `unavailable`**；确定无人监听的端口 → `StorageUnavailable` 且**非该子类**。
- 回归（串行）：edge **84 → 89 passed / 0 failed**；`test_storage_ipc_auth_s3.py` 13→17；agent `test_s3_storage_ipc_token.py` 4 passed 未变；`collector/tests/test_collector_outbox.py` **51 passed**（新跑，覆盖 `import storage_client`）。无 skip/xfail、无放宽断言。

**edge 组验收者同时完成的正面验证（不是只挑错）：**

- **补做了修复者自述未覆盖的 4 进程并发争锁**：恰好 1 胜出、3 个 rc=1，outbox 逐行 / `edge.db` sha256 / mtime **三者全不变**；追加 3 个真实败方仍全不变。
- **崩溃窗口注入**：在「取锁后、恢复前」注入 sleep → 窗口内 3 条仍 `inflight` 且锁确实被占用 → 硬杀 → 重启真实入口**恢复 3 条、零丢失**。
- **S3 无凭据 13 类动作 + 三步链 `recover→claim→complete(sent)` 全 401、库零变化**；坏令牌 6 种全 401；越权矩阵 3 角色 × 18 动作仅白名单内 200；令牌 3 份互异、不引用 Agent service token、文件 ACL 仅 3 个显式 ACE 无继承。
- **回注反证**：B25 改回旧行为 → 其 4 用例 **4 failed**；S3 去掉认证 → 13 用例 **8 failed** ⇒ 测试非空转。全程在 `%TEMP%` 影子树，仓库文件 sha256 前后一致。
- 回归：edge **43 → 84 passed / 0 failed**（串行跑两次一致）；既有测试仅 `test_storage_service.py` 被改且 **assert 15→15**（未削弱）；无 skip/xfail。
- 一处它**无法独立核实**并如实说明的：S3 越界改 `agent/app/services/edge_storage_client.py` 是否获批准 —— **确实是我批准的**（见上文归属表），它没有轻信双方说法，这是正确态度。

**B11 残留修复的判别力证据（非空转证明）：** 把修复前的 `admin.py`/`health_checker.py` 回退成 `%TEMP%` 副本重跑 → R1 泄漏 300 字节 / R2 `accepted=True` / R3 `proxy=1`，**证明新用例在修复前确实失败**。另：host 变体矩阵 **22 项端到端全部 400 且两端 0 字节**；17 项 `_target_address` 期望 0 mismatch；既有 B11 测试文件 sha256 与验收记录**逐字节一致**（无放宽/删除/跳过）；拒绝日志只记 target+字段名，400 文案不含口令。回归：agent `305 → 346 passed`（+41 新文件），三文件合跑 89 passed。

**B11 有意保留的范围边界（已登记）：** `check_backend` 仍走默认 `urlopen`（受代理影响，但**该请求不含任何凭据**）。

### 验收层同时证实了修复者的关键主张（不是只挑错）

- **B7 反向检查通过**：映射外 point_id / `config_version` 领先 / `node_uuid` 不匹配 / `device_type` 不匹配**全部仍被拒**，且是在**证明"重读确实发生过"**的前提下（对应命名空间读 +1、另一命名空间 0 增量）；`_validate_item_row` 等 6 个函数与 HEAD **逐字节相同** → 「为消除误拒而删掉拒绝逻辑」这个最危险的假修复**已排除**。
- **B41 最强反例独立复现**：`previous\data-watch.exe` 被独占 → 修复前 `current` 删空、`previous` 仅剩 exe、**一个可运行版本都不剩**；修复后 `current=OLD` 完整。24 次脚本执行、修复前后同台对比。
- **B41 安全围栏确实拦得住**：`_assert_temp_root` 实测挡住 System32、`C:\ProgramData`、默认安装根 `%LOCALAPPDATA%\DataWatch\CLI`；18/18 与 8/8 实验根全在 `%TEMP%`；对 `C:\ProgramData\DataWatch Edge` 零读写、连 resolve 都避开。
- **B15「32 字符边界」判定为可接受、非残留缺口**：验收者实测 4 个 create/verify 调用点**全部经 `load_config`**，除 `config.py` 外全仓库无人读写 `jwt_secret`，也无第二处 `toml.load(CONFIG_PATH)` → 绕过路径**当前不可达**。属应记账的**纵深防御缺口**；建议后续把 `test_admin_auth.py` 的 `"test-secret"` 哨兵改长即可放开加密边界下限。
- **D39 归属判断成立**：3 个 `M` 文件 diff 全为 S3 令牌主题，D39 关键词命中 **0**；13 个归档、4 个「口径已正确」文件、`docs/论文` 全部**字节未改** → 我此前那条表述不严谨的约束**未造成实际越界**。
- **B6 / B5 已由非修复者独立验证**（B6 含 5 组回注反证 + 反向检查；B5 含 5 处调用回注 + 字节还原）。

### 验收层也被反向纠正过一次：`stream_maxlen` 归因（已核实）

D39 后续 agent **推翻了验收者自己的一条归因**，协调者已逐条实读核实：

验收者说"不按 MAXLEN 裁剪"仅在 `stream_maxlen=0` 时成立，暗示成因是配置值。实际（我实测确认）：

| 证据 | 事实 |
|---|---|
| `ingest/app/config.py:48` | `stream_maxlen=int(os.getenv("INGEST_STREAM_MAXLEN", "0"))` —— 读入设置 |
| `ingest/app/redis_store.py:37` | Lua 读 `tonumber(ARGV[2])` |
| `ingest/app/redis_store.py:357-359` | 唯一 `eval` 提交点把 `ARGV[2]` **硬编码为 `"0"`**（注释 `never trim unconsumed data`）⇒ **`INGEST_STREAM_MAXLEN` 从未传入脚本、完全不生效** |
| `installer/templates/collector-settings.toml.example:39` | `stream_maxlen = 50000` |
| `installer/DataWatchRole.iss:206` | `CopyIfAbsent(... templates\collector-settings.toml.example → collector\config\settings.toml)` ⇒ **中心式角色确实铺这个 50000 模板** |

⇒ **装好的中心机器默认就在按 50000 裁剪**，原文不只是"缺限定条件"，而是**说反了**。D39 已在 DW-01 `:22` 加条件、`:58-78` 新增条件限定块（含「B27 落地须同步」）。

**交叉验证的意外收获**：D39 从**文档侧**独立走到了清单 **B27**（「`INGEST_STREAM_MAXLEN` 被读取但提交 Lua 固定使用 0」）的同一结论。两条独立路径互相印证，B27 的判断因此更可信。

**授权范围外的同类残余（D39 仅上报未改，已登记）**：`docs/06-验证记录/2026-09/reliability-review-delivery.md:11`、`docs/04-运维部署/README.md:387`、`docs/04-运维部署/Ingest容量压测.md:124-125` —— 三处均默认 `INGEST_STREAM_MAXLEN` 生效。

**处置决定：不现在改，与 B27 一起改。** 理由：这三处的正确措辞取决于 B27 的落地结论（使配置真正生效、还是明确废弃该配置并迁移文档）。现在改可能白改或改错，且会与 B27 的文档同步重复。**已登记为 B27 的关联文档项，移交批次 D。**

## 波次记录

| 波次 | 范围 | 状态 |
|---|---|---|
| W0 | 基线、入口清单、D39 确认、P0 任务卡 | ✅ 完成（`W0-A-基线.md` 585 行、`W0-B-P0任务卡.md`） |
| W1 | 批次 A 的 7 个 P0 最小修复 + S3 边界核查 + D39 文档 | ✅ 完成（期间 3 个 agent 中途失败，产出均抢救成功） |
| W1-验收 | 独立反证（按模块不相交分组并行：edge / agent / backend+ingest / cli+docs） | 🔄 进行中 |
| W6 | 集成回归与交付汇总 | 🔄 串行回归已完成，见下 |

### 批次 A 串行全量回归结果（2026-10-03，所有修改型 agent 停止后执行）

**执行方式**：脚本 `D:\python\device_alarm\dw-test-env\regression.ps1`，八个模块**严格串行**、逐个跑（并发已被本轮实测两次证明会制造假失败）。**跑了两次**：第一次在 W1 修复轮后，第二次在全部遗留补齐后（下表为**最终**一次）。

| 模块 | W0-A 基线 | 第一次（W1 后） | **最终** | 判定 |
|---|---|---|---|---|
| cli | 98 passed | 117 | **124 passed** | ✅ +26 |
| collector | 619 / 26 skipped / **1 failed** | 619 / 26 / **1 failed** | 619 / 26 / **1 failed**（同一个既有 owner-lock 项） | ✅ 未变 |
| writer | 184 passed | 184 | **184 passed** | ✅ 未变 |
| edge | 43 passed | 84 | **96 passed** | ✅ +53 |
| ingest | 50 / **1 failed** / 5 skipped | 62 / 1 / 6 | 68 / **1 failed** / 6 skipped（同一个既有 TurboAPI/msgspec ABI 项） | ✅ +18 |
| agent | 253 passed / 1 skipped | 305 / 1 | **347 passed** / 1 skipped | ✅ +94 |
| backend | 204 passed / 6 subtests | 221 / 6 | **221 passed** / 6 subtests | ✅ +17 |
| frontend | 27 tests / 0 fail | 27 / 0 | **27 / 0** | ✅ 未变（批次 A 未改前端） |

**结论：零回归。** 唯二失败均为 W0-A 在开工前就已记录的既有失败（collector 的 owner-lock 用例、ingest 的 TurboAPI free-threading 项），**无任何新增失败**，所有通过数单调不减。

**现场栈安全基线同期复核（整改全程）：** 5 个容器 `Up 3 hours`、**从未重启**（`Up` 时长从 17 分钟单调增长），现场 Redis `db0: keys=10` 与开工前**一字不差** ⇒ **整改全程对现场数据零写入**。仓库内未残留任何回注工具或验收夹具（均在仓库外 `b7-verify\`、`%TEMP%`）。

### W0 的结论与对既有叙述的修正

- **8 条全部确认成立**（B5/B6/B7/B11/B15/B25/B41/S3），且 W0-B 对其中 3 条指出清单原文的定位**不完整或误导**：① B7 —— `catalog.py` **根本不发失效通知**（清单把它写成"版本更新侧"，会让人误以为只缺统一发布）；② B5 —— 真正的迁移逻辑在 `outbox.py:542-602 prepare_session_switch`，清单写的 `complete_batch/complete_session_switch` 只是接收方；③ B25/B41/S3 各自遗漏了关键放大器行。
- **修正我先前的错误结论：`frontend` 确实有测试框架**（不是"没有"）。`frontend/package.json` 的 `test` = `tsx --test tests/*.test.ts`，`frontend/tests/` 下有 13 个测试文件，实测 **27 pass / 0 fail**，`npm run build`（`vue-tsc --noEmit && vite build`）exit=0。因此**不需要**引入 vitest 脚手架（用户此前基于我的错误信息所做的"允许新增 vitest"决定已无必要）。
- **修正我先前的错误结论：`import sqlalchemy` 并不失败**。它在 free-threading 解释器下能导入成功，但会**自动打开 GIL** 并打印 `RuntimeWarning`。这恰好解释了 backend 测试为何必须加 `-Xgil=0`。
- **需保护的既有状态：`git stash list` 实测有 4 条 stash**（`docs-leftover-backup`、`synced-agent-docs-backup`、`stage1-uncommitted-before-sync`、`codex: preserve generated pycache before merging 002`），加上清单本体与 `docs/整改记录/` 两个未跟踪项。全程禁止对其做任何写操作。

## D39 架构确认：协调者独立复核结论（批次 A 出口条件）

以下由协调者直接读取源码确认，与 W0-A 的矩阵互为交叉验证（**真实入口**级证据）：

| 事实 | 证据 |
|---|---|
| 中心路径默认直写 `RedisWriter`，不等每批 WAITAOF | `collector/main.py:52`（import）、`:188`（`self.redis_writer = RedisWriter(`）、`:184-186`（注释明确「不逐批等 WAITAOF —— 只有分布式边缘采集才走」「DurableRedisWriter 仅保留供历史诊断与遗留处置」） |
| 中心路径**未**构造 SQLite Outbox | `collector/main.py:195` 处 `EdgeOutboxWriter(...)` 处于**注释状态**；`:20` 的 `sqlite3` 仅用于 `:117` 的**只读**表结构探查（`mode=ro&immutable=1`），不构成写入型 Outbox |
| 边缘路径使用 `EdgeOutboxWriter` | `edge/collector_main.py:17`（import）、`:20`（`return EdgeOutboxWriter(EdgeStorageClient(), metrics)`） |

**易被误读的陷阱（重要）：** `collector/main.py:20` 存在 `import sqlite3`。仅凭该 import 会误判「中心仍用 SQLite」；实际它只服务于 `:117` 的只读 schema 探查。修正 D39 文档时必须写清这一点，否则容易被后续读者再误读一次。

## 环境基线（本轮实测）

- `git rev-parse HEAD` = `6845e800990f8352f46655036741bd79ca25eed1`，与文档基线一致。
- 分支 `main`；工作区仅有本清单文档未跟踪，无用户未提交改动需要保护。
- 本机 Python 3.14.6；同时存在 3.14t（free-threading）与 3.14 两个解释器。
- Docker 29.6.1 可用；现场栈与隔离测试栈并行运行中（详见 `隔离测试环境.md`）。
- 八个模块（含 `frontend`）都有可运行的测试：`frontend` 用 `npm test`（`tsx --test`，27 pass），其余用 pytest；各模块的精确解释器与命令见 `隔离测试环境.md` §3.1（**照抄，别自己试**）。

## 事件记录：W1-E1 中途失败（已接手）

**发生了什么：** W1-E1 在完成 B25、写完 B5/B6 的代码与测试之后**失败退出，未留下任何收尾消息**，也没有写 `B5.md` / `B6.md`。工作区留下了未记录的改动。

**协调者的核查结论（不是采信自述，是我自己验的）：**

| 项目 | 结论 |
|---|---|
| 代码是否可编译 | ✅ `py_compile` 三个文件 exit=0，没有留下语法损坏 |
| 是否越界 | ✅ `reject_count` 只出现在 `edge/app/outbox.py`，**零命中** S3 独占的 `storage_client.py` / `storage_service.py` —— 没有越界 |
| B25 | ✅ 已完成：`test_outbox_b25_lock_order.py` 4 passed，`B25.md` 已落盘 |
| B5 | ✅ 已实现（`uploader.py` 新增 `_persist_confirmed()`，6 处 `records` → `records[index:]`），测试全通过；缺报告 |
| B6 | ❌ **半成品**。`reject_count` 列被写入但**全仓库从未被读取**；`edge/app/uploader.py:504-506` 判隔离用的仍是 `attempt_count` |
| edge 全量 | **1 failed, 70 passed**；唯一失败在 `test_outbox_b6_attempt_separation.py::test_only_protocol_verdicts_consume_the_quarantine_budget`（`KeyError: 'items'`，位于它自己的 mock handler） |

**关键判断：B6 描述的缺陷当前并没有被修复。** 隔离判定仍在混合计数上触发，新列是死代码。这正是 E1 自己此前预警过的"HTTP 路径与单元路径分裂"风险的另一面。已派 **W1-E1b** 接手：独立验证 B5、诚实归因那个失败用例、并给出 B6 两条落地路线的对比（**只写方案，不落地**），`B6.md` 的最终状态必须诚实标为「未完成 / 待落地」。

**B6 的落地被文件所有权阻塞：** 无论走哪条路线都要动 `storage_client.py` / `storage_service.py`（当前由 W1-S3 独占，S3 在做 IPC 认证）。**待 S3 交付后再排期落地**，不得并发修改。两条候选路线：**A 服务端判定**（`mark_retry_batch` 达阈值直接转 dead，需把 `max_attempts` 传进 IPC 写侧）；**B 回读计数**（IPC 补序列化 2 行 + `uploader.py:504` 改用 `reject_count`）。→ **S3 已交付，阻断解除**，已授权 W1-E1b 按**路线 B** 落地。

## 事件记录：W1-S3 中途失败（文档同步部分由协调者补完）

**发生了什么：** W1-S3 的**主体交付已完成并通过验收**（代码修复 + `S3.md`，6 类攻击改后全 401）。它在随后被我授权的**文档同步**环节失败退出，无收尾消息。

**协调者核查结论：**

| 项目 | 结论 |
|---|---|
| 主体交付 | ✅ 完整：`S3.md` 已落盘，代码修复经复现/反证双向验证 |
| 授权要改的 4 个运维文档 | ✅ **实际已全部改完**（`运维部署/README.md` 6/2、`三角色安装包验收清单.md` 4/2、`问题排查.md` 5/1、`边缘运行时实现说明.md` 1/1），协调者逐条复核内容与实现一致 |
| 授权要补的 ADR 条目 | ❌ 未做 → **由协调者本人补写为 `架构决策记录.md` 的 ADR-0015**（状态 Accepted，含背景/决策/放弃方案/后果/验证依据/相关七字段，内容以 `S3.md` 的实测证据为依据） |
| 仓库残留 | ✅ 已清理：`edge/data/storage-ipc-tokens.json` 已删，`Test-Path edge\data` = `False` |

**注意**：本机先后有 2 个 agent 中途失败（W1-E1 丢报告、W1-S3 丢 ADR），**两次的代码交付物都基本完好、可抢救**。经验：长任务 agent 失败是常态，因此每个条目的产出必须落盘为独立文件（`Bxx.md`），不能只留在 agent 的上下文里——这也是本轮台账设计的价值所在。

## 事件记录：协调者误判导致重复派发（合并写者冲突，已化解）

**发生了什么：** 我给 B7 补齐 agent（`1b96f886`）下达收尾指令（用例移植进仓库 + 文档更正）后，收到一条该 agent 的 `finished` 通知，其 closing message 是**上一轮的旧摘要**。我据此**误判"指令未被处理"**，于是又派了一个新 agent（`deb53b04`）做同一件事。

**实际是我错了**：那条 `finished` 是**上一轮**的收尾通知；我发出的 `send_message` 已经**在它的下一轮生效** —— 它恢复了工作，并在 13:57:47 写入了 6 条用例（`ingest/tests/test_b7_authoritative_reread.py` 327→471 行，`sha256 9F025E9B…`）。

**是 `deb53b04` 自己发现并上报了冲突**，而不是硬写：它检测到"文件在我读取后被重写"，主动停下并请求指定唯一写者。**这个行为避免了一次真实的文件互相覆盖。**

**处置：** 立即定 `1b96f886` 为唯一写者；令 `deb53b04` **停手**（不写任何文件、不跑同一模块测试，避免并发测试污染），保留它手里的 `ingest/app/repository.py` pristine 字节备份与 sha256 `A46B02D9…` 备用，后续可能转做独立验收者。

**教训（写给后续批次）：**
1. **`send_message` 显示"已送达"意味着该 agent 会在下一轮处理它**；此时**不得**再为同一任务派发第二个 agent。收到的 `finished` 通知可能对应**发信之前**的那一轮，closing message 因而看起来"没反映新指令"——这是假信号，不是未处理。
2. 判定"某任务是否真的做了"**只能靠核对落盘产物**（文件 mtime / sha256 / 内容），不能靠通知文本推断。我这次正是先核对了产物才发现自己误判。
3. 并发写者的**自救机制有效**：要求每个 agent 在写前核对 read-state（读取时记录 sha256/mtime，写前复查），能自动发现碰撞而不是盲写。此后派发同类收尾任务时应继续要求这一点。

**本次事件的实测收尾（`deb53b04` 主动补正，值得记录）：**
- 它诚实说明：停手指令到达**之前**，其回注反证已在**同一个原子块**内跑完（注入→红跑→`finally` 从字节备份还原），所以严格说不是"未做任何写入"，而是**一次已完整闭环、零残留的临时写入**。实测 `ingest/app/repository.py` sha256 = pristine `A46B02D9…02EEE`、`bytes=24593`（与字节备份同哈希）—— **零残留，证实成立**。
- **read-state 保护真的生效**：它那次对测试文件的 edit **因 read-state 冲突被拒、从未落盘**；测试文件始终是 `1b96f886` 的 `9F025E9B…`（mtime 13:57:47）。`B7.md`(13:55:16) 与 `W1-验收-B7.md`(13:40:55) 均未经它手。⇒ 并发写者机制**确实防止了一次互相覆盖**，不只是理论上有效。
- **一条关键的断言精确性**：它把两个 reason 从集合移除后跑目标测试文件，结果 **5 failed / 13 passed**。那 5 条变红的是：`test_missing_assignment_recovers_when_assignment_is_created`、`test_device_type_change_recovers_without_notification`、`test_missing_assignment_is_still_rejected_after_reread`、`test_genuine_device_type_mismatch_is_still_rejected_after_reread`、`test_missing_assignment_reread_is_bounded_to_one_per_window`。
  **第 6 条 `test_node_mismatch_stays_rejected_without_any_reread`（越权 0/0）未变红，且这是预期的** —— `stale_epoch` 本就不在重读集合内。⇒「移植的用例会变红」应表述为 **5 红 / 1 恒绿**，**不是 6 红**。这个区分很重要：把恒绿的那条也算作判别力证据，就是对回注反证的过度声称。
- 停机前实测 ingest 计数：**`1 failed, 68 passed, 6 skipped`**（基线 62 → 68，+6 即新增用例；唯一失败仍是既有 TurboAPI/msgspec ABI）。

## 协调者自身的错误模式（写给批次 B–E）

本轮协调者犯了 3 类错误，其中 2 类**重复发生**，后续批次必须防：

### 1. 文件授权清单两次列窄（已发生 2 次）

| 次 | 条目 | 我漏掉的文件 | 后果 |
|---|---|---|---|
| 1 | S3 | `agent/app/services/edge_storage_client.py`（清单原文第 1439 行**已明列**"Agent 的 Storage 客户端"） | 若无修复者上报，Agent 客户端会 401，修复不完整 |
| 2 | S3/B25（P1-C） | `edge/uploader_main.py`（锁路径由 `settings_from_env()` 派生）、`edge/app/paths.py` | 若无修复者上报，"同库不同锁"无法修 |

**两次都是修复者主动停手请示才暴露的**，而不是我自己发现。⇒ **教训：我给的文件清单只应作为"起点"，必须明确授权修复者在"修复点落在清单外"时停手请示，且不得因此视为其失职。** 本轮两位修复者都做对了，应继续保留这个要求。
**更根本的改法（批次 B–E 采用）：** 派发时不要只给文件清单，而要求**先只读确认修复点所在文件**、把清单作为"预期落点"而非"白名单"，并规定"清单外即停手报告"。

### 2. 重复派发同一任务，造成并发写者（已发生 **2** 次）

`send_message` 显示"已送达"即表示该 agent 会在下一轮处理；此时收到的 `finished` 通知可能对应**发信之前**那一轮，closing message 因而看似"未反映新指令"——**这是假信号**。

| 次 | 我的判断依据 | 真实情况 | 谁发现 |
|---|---|---|---|
| 1 | 只看了通知文本 | 指令**已**生效（它已写入 6 条用例） | 新 agent 上报文件被重写 |
| 2 | 通知文本 + **14:47 的落盘快照**（显示未应用） | 指令**正在**生效 —— 快照拍在它落盘**前几秒**，结论写下即过期 | 新 agent 上报时间戳（14:46–14:47:43 持续写入） |

**⇒ 修正后的规则（两次都因此踩坑，必须照此执行）：**
判定"某任务是否还要继续"需要**三个条件同时成立**，缺一不可：
1. **核对落盘产物**（mtime / sha256 / 内容）—— 但要注意**快照会过期**，若文件正被写，快照写下即失效；
2. **查 `list_agents` 确认原 agent 是否仍 `[running]`** —— 第 2 次我漏了这一步，而当时它明确显示 `running`；
3. **核对通知文本**仅作参考，**不得作为唯一依据**。
**最稳妥的做法：凡"续派同一任务"，先只读观察一次（文件 mtime 是否在变动 + agent 是否 running），确认静止后再派发。**

> 两次的教训都指向同一点：**我倾向于用单一信号下结论，而并发场景下任何单一信号都可能是错的。** 反过来看，两次都是**被派发的新 agent 自己发现碰撞并停手上报**才避免覆盖 —— 所以"写前核对 read-state + 发现碰撞即停手上报"这条要求必须继续保留在每次派发里。

### 3. 约束表述不严谨（已发生 1 次）

给 D39 的"这 6 个文件不得修改"本意是"不得改动其 D39 相关口径"，但我又单独授权 S3 改其中 3 个文件的另一主题，导致 D39 的"未出现于 git status"断言失效。⇒ **约束要写清"不得改动什么内容"，而不是"不得改动哪些文件"**；跨条目共享文件时，必须显式说明各条目在该文件内的分工。

> 三类错误的共同点：**都是协调层的问题，不是执行层的问题**，而且**都由被派发者发现**。这说明"独立验收 + 允许停手请示"这套机制不仅在查代码，也在补协调者的漏。

**协调者自纠：D39 的「不得修改」约束表述不严谨。** 我给 D39 的约束是「这 6 个「口径已正确」的文件不得修改」，本意是**不得改动它们的 D39 相关口径**；但我另外单独授权 W1-S3 去改其中 3 个文件（`边缘运行时实现说明.md`、`运维部署/README.md`、`三角色安装包验收清单.md`）的**另一个主题**（Storage IPC 令牌鉴权）。因此 D39 首轮「这 6 个文件在 git status 中均未出现」的断言**失效** —— W1-S3 报出了这个矛盾，未擅自回退，并已在 `D39.md` 更正，处理正确。**该更正应保留**，它本身是有价值的痕迹。

## 改动文件归属（最终 diff 复核用）

最终复核时，`git status` 里每一个 `M` 都必须能归到某个条目与某位负责人。当前归属如下：

| 归属 | 文件 |
|---|---|
| **B7**（W1-I1） | `ingest/app/repository.py`、`backend/app/api/routes/catalog.py` |
| **B25/B5/B6**（W1-E1 / E1b） | `edge/app/outbox.py`、`edge/app/uploader.py`、`edge/uploader_main.py` |
| **B15/B11**（W1-A1） | `agent/app/core/config.py`、`agent/app/core/security.py`、`agent/app/routers/admin.py`、`agent/app/services/health_checker.py`、`agent/config.toml.example` |
| **B41**（W1-C1） | `cli/update_manager.py`、`docs/04-运维部署/CLI安装与更新.md`、`docs/02-模块设计/cli/总体设计与架构.md` |
| **S3**（W1-S3） | `edge/app/storage_service.py`、`edge/app/storage_main.py`、`edge/app/storage_client.py`、`edge/tests/test_storage_service.py`、`agent/app/services/edge_storage_client.py`（**经批准越界**）、`docs/04-运维部署/README.md`、`docs/04-运维部署/三角色安装包验收清单.md`、`docs/04-运维部署/问题排查.md`、`docs/02-模块设计/edge/边缘运行时实现说明.md` |
| **D39**（D39 agent） | `docs/03-数据与契约/DW-01-确认语义与持久化边界.md`、`docs/01-总体设计/分布式部署架构方案.md`、`docs/joint-measurement-20260927.md`、`docs/joint-ladder-20260927.md`、`docs/06-验证记录/2026-09/reliability-review-delivery.md` |
| **协调者本人** | `docs/01-总体设计/架构决策记录.md`（ADR-0015，补 S3 未完成的授权项）、`docs/整改记录/**`（台账） |

**注意**：`docs/04-运维部署/README.md`、`三角色安装包验收清单.md`、`边缘运行时实现说明.md` 这 3 个文件同时出现在「D39 的无需修改清单」与「S3 的实际改动清单」里 —— 复审时应确认**它们的改动内容是 S3 的令牌主题**，D39 口径**一字未动**。这是我给 D39 的约束表述不严谨造成的表面冲突，不是越界。

## 批次 A 决策记录（用户裁定）

用户已就 5 项待决事项作出裁定。**这些裁定优先于任何 agent 的自述或我的建议**，并作为批次 B–E 的约束。

| # | 事项 | 用户裁定 | 对后续的约束 |
|---|---|---|---|
| 1 | B15 弱密钥启动拒绝 | **接受拒绝弱密钥启动，但升级前必须预检** | 补「升级前检查 + 安全迁移提示 + 回退演练」；**修复验收与生产升级放行分开**；安全要求保留，**不得为兼容继续接受弱密钥** |
| 2 | B6 dead 生命周期 | **不接受"没有恢复入口、却七天自动删除唯一载荷"作为最终方案** | 优先补：**查看、导出、受控重放、删除规则**；重放仍须校验**权限、时间窗、幂等性**；保留期由**业务与容量共同决定**并**配容量告警**；**不能简单改成无限保留** |
| 3 | B7 冷却窗口 | **保留安全拒绝；30 秒冷却"有条件接受"** | `stale_epoch`/`out_of_time_window` **不得**因恢复方便而放宽；冷却窗口须**对照现场允许的恢复时间**，并明确**等待原因**与**恢复触发条件**；**测试通过不能直接证明 30 秒适合业务** |
| 4 | S3 遗留 | **不要全部作为普通遗留放过去** | `data_root` 可能误触真实安装目录、安装器 ACL、**泄露凭据能否及时吊销** → 按实际风险列为**发布门槛**；IPv6 若现场不依赖可延期，但须**明确不支持范围** |
| 5 | B41 updateStarted | **可接受等价实现，但须正式更新协议** | "已提交待验证"必须**始终区别于"更新成功"**；写清 成功/失败/超时/重启/重复通知 的**状态转换**；核对**所有调用方**；然后才关闭该偏差 |

### 流程性裁定（同样生效）

1. **冻结优先**：进入 B–E 前先把已验证成果固定（见 `A-基线冻结.md`）。**不增加同时工作的 agent 数量**，而是稳步扩展。
2. **保留四段式，但缩小每批范围**：W0 基线 → 修复 → 独立验收 → 串行回归；每小批**先选 3–5 个相关问题**，优先**数据丢失、安全与恢复**，再做一般功能与文档。
3. **每条都要有针对性反证**，不能只看测试全绿。
4. **共享文件指定单一写入负责人**；写前核对版本；**碰撞或超范围立即协调**。
5. **每批固定验收版本**，避免几十项未提交修改叠加。
6. **既有失败必须核对具体错误**，不能只看失败数量仍为 2。
7. **用户推荐的推进顺序**：B6 保全规则与 S3 发布门槛 → A 最终基线冻结 → 下一小批确定性修复。

### 用户指出的两处我的过度声称（已接受并修正）

1. **"零写入现场栈"证据不足**：Redis 键数不变只覆盖**键数量**（值被改写不改变键数），容器未重启只说明**未重启**；本轮**未做内容级基线哈希**，故内容级完整性**无法事后证明**。已收窄为「过程约束」并写入 `A-基线冻结.md` §8；**B–E 改为开工前建立内容级基线**（导出并哈希关键键值集合、记录关键表校验和）。
2. **"落盘产物 + agent 是否 running"不能替代一致快照**：快照写下即可能过期；**冻结后若文件再变，依赖该文件的结论必须重验**。已写入 `A-基线冻结.md` §8。
