# 后端遗留整改方案（交付 Codex 执行）

> 背景：2026-06-14 一轮后端代码审查，9 个 bug + TDengine 查询重构 + docker-compose 时区已修复并提交（commit `0e9dad4`）。
> 本文档列出**当时刻意未动**的遗留项，供 Codex 按优先级实施。每项含：问题、根因定位、方案、验收、风险。
>
> 通用约束（沿用本仓规则）：
> - Windows 环境，命令用 PowerShell/CMD，不用 Bash；含中文路径加引号。
> - 先诊断再改；一次一个最小改动；改前说清改哪些文件、改什么、为什么；不顺手重构无关代码。
> - Python 在虚拟环境运行：解释器 `C:\Users\Hry\.local\bin\python3.14t.exe`，`PYTHONPATH` 含 `backend\.py314t-site`（参考 `start-dev.ps1`）。
> - 每步改完用 Codex 自审 + 编译校验：`python -m compileall -q <files>`。

---

## 优先级建议

`#1（安全收尾）` → `#2（数据源故障可见）` → `#10（TDengine 池化，需真机）` → `#11/#12（脆弱点，低优）`

---

## #1 泄露密钥收尾（安全，最高优；含运维动作）

### 问题
真实密码/旧 JWT secret 曾提交进 git，且部分明文仍在仓库中：
- `collector/config/settings.toml:12` —— MySQL 密码 `19990910`（明文，且被 collector 运行时读取，也被 backend `config.py` 当默认值读）。
- `deploy/backend.env.example` —— TDengine 密码 `Hry19990910` 等明文（第 ~23 行）。
- git 历史里仍存有 `19990910` / `Hry19990910` / 旧 JWT `device-monitor-dev-secret`。

backend 侧已处理：`backend/.env.example` 占位化、`config.py` 不再读 `.env.example`、JWT 启动自检、`backend/.env` 已 gitignore 并由用户填强随机 secret。**collector / deploy 侧尚未同款处理。**

### 方案（分两类）
**A. 代码/配置层（Codex 可做）**
1. `collector/config/settings.toml`：参照 backend 的做法拆分——
   - 先确认 collector 的配置加载逻辑（找读取 `settings.toml` 的代码，确认是否支持环境变量或 `.env`/本地覆盖文件）。
   - 提交进库的应是**模板**（占位符，如 `password = "changeme"`），真实值放在**不入库**的本地文件（如 `settings.local.toml` 或 `.env`），并把真实文件加进 `.gitignore`。
   - ⚠️ **不可直接占位化当前在用的 settings.toml**：collector 正在读它，会起不来。必须先建好"真实值的本地文件 + 加载逻辑支持"，再把模板占位化。
2. `deploy/backend.env.example`：把其中真实密码替换为占位符（它是模板，不该有真值）。
3. 全仓再 grep 一遍 `19990910` / `Hry19990910`，确认无其它残留明文（脚本 `scripts/`、docs 的示例里也有，按需占位或标注为示例）。

**B. 运维层（需用户在真实库执行，Codex 仅产出清单/脚本）**
1. 在真实 MySQL、TDengine 上**轮换 root 密码**；更新各服务的本地真实配置（backend `.env`、collector 本地配置、writer 配置）。
2. （可选，高成本）用 `git filter-repo` / BFG 清理 git 历史中的密钥；需团队协调强推，谨慎。

### 验收
- 仓库内 `git grep -n 19990910` / `Hry19990910` 无明文命中（仅模板占位或显式示例标注）。
- collector / writer / backend 在"真实值放本地、模板进库"后均能正常启动。
- 新建一份干净 clone，不放任何本地密钥文件时，服务以占位值启动会明确失败/告警，而非静默使用弱默认。

### 风险
- 占位化运行时配置若顺序错误会导致服务起不来 → 严格"先建本地真实配置+加载支持，后占位模板"。
- 历史清理是破坏性操作，需备份与协调。

---

## #2 数据源故障对上层可见（API 健壮性；涉及契约，需与前端确认）

### 问题
TDengine / Redis 整体宕机时，部分接口返回 `200 + 空数据/离线`，而非真实错误，排障困难。
（本轮已做"不再静默吞异常、留 WARN 痕迹"，但**响应仍是 200**。）

### 根因定位
- `backend/app/services/history.py:query_history`：`_fetch_point_series` 内部逐层 try/except 吞掉 TDengine 异常返回 `[]`；全库宕机时所有点位空 → 200 + 空趋势。
- `backend/app/services/monitor.py:_get_device_latest_from_redis`：Redis 故障返回 `{}`（已加 WARNING），上层仍当"离线"。
- 已有可复用的健康探针：`backend/app/services/ws_push.py:15 _check_tdengine()`（带 30s TTL 缓存）。

### 方案
1. 抽一个共享的轻量健康探针（TDengine、Redis 各一），带短 TTL 缓存，避免每请求都探测。
2. `query_history`：当本次所有点位都无数据时，调用 TDengine 健康探针；探针为"不可达"则 `raise ApiError(503, "历史数据源不可用")`，否则维持 200 空结果（真·区间无数据）。部分点位失败仍返回部分数据（已留痕）。
3. 实时类接口（monitor / dashboard）：在响应里加 `degraded`/`tdengineConnected`/`redisConnected` 标志（ws_push 的 snapshot 已有 `tdengineConnected` 先例），让前端提示"数据源异常"而非"设备离线"。
4. **与前端确认契约**：503 码与 `degraded` 字段前端如何呈现。

### 验收
- 停掉 TDengine 后，历史查询返回 503（而非 200 空）；恢复后正常。
- 停掉 Redis 后，实时接口响应带 `redisConnected=false`/`degraded=true`。
- 正常"区间内无数据"仍是 200 空结果，不误报 503。

### 风险
- 契约变更影响前端，需联调。
- 健康探针别在热路径上无缓存频繁探测（沿用 TTL 缓存）。

---

## #10 TDengine 连接池化（性能；需真机验证，单独排期）

### 问题
`backend/app/db/tdengine.py:_fetch_all_sync`（约 77 行）每次查询都 `connect()` 新建连接、`finally` 关闭，无池化；历史/透视高频查询下建连开销成为吞吐瓶颈（并发还受 `_query_semaphore = Semaphore(5)` 限制）。

### 方案（择一，需真机压测）
- 维护一个按 database 维度的小连接池（或 thread-local 连接），复用 taosws 连接；注意 taosws 连接的线程安全性与 `asyncio.to_thread` 的配合。
- 或评估 taosws 是否提供官方连接池/长连接选项。
- 增加连接健康检查与失效重建。

### 验收
- 历史/透视高频查询的 P95 延迟与连接建立次数明显下降。
- 长时间运行无连接泄漏、无失效连接导致的查询失败。

### 风险
- taosws 连接并发/线程模型不当会引入偶发错误 → 必须在真实 TDengine 上压测验证，不能只靠单测。

---

## #11 SQLAlchemy 私有属性猴补丁（脆弱点，低优，仅加固）

### 问题
`backend/app/db/mysql.py:18` `engine.sync_engine.dialect._send_false_to_ping = True` 依赖 SQLAlchemy 私有实现，为 aiomysql 的 pre-ping 兼容而打；升级依赖可能静默失效。

### 方案
- `backend/requirements.txt` 把 `SQLAlchemy`、`aiomysql` 从 `>=` 收紧为可控范围（如 `~=` 或固定小版本），降低升级意外。
- 在该补丁处补注释说明触发条件与对应版本。
- 评估当前/更新版 SQLAlchemy 是否已原生兼容 aiomysql ping 签名，若是则移除补丁。

### 验收
- 依赖升级流程中有版本约束兜底；MySQL pre-ping 正常工作（断连后自动重连）。

### 风险
- 极低；改 requirements 后需重装并冒烟。

---

## #12 依赖 `_cleanups` 异常时不执行（潜在，当前不触发，最低优）

### 问题
`backend/app/main.py:_dispatch_route` 中依赖的 `_cleanups`（生成器型 Depends 的收尾）只在 handler 成功返回后执行；handler 抛异常时被跳过 → 将来引入 `yield` 型依赖会漏清理（如 DB session 不释放）。
当前代码库 `Depends` 仅用于 `auth_scheme`（非 yield），DB session 都走显式 `async with`，**暂不触发**。

### 方案
- 将 cleanup 执行移入 handler 调用的 `try/finally`，保证异常路径也执行。
- 或在引入首个 `yield` 型依赖时一并修复，并加回归测试。

### 验收
- 构造一个会抛异常的 `yield` 依赖路由，验证其 cleanup 仍被执行。

### 风险
- 低；改动在请求分发热路径，需保证异常语义不变（cleanup 异常不可掩盖原始异常）。

---

## 附：本轮已完成（仅供 Codex 了解上下文，勿重复）
commit `0e9dad4`：.env 安全化 + JWT 自检、离线推送去重、日志清理调度修正、MySQL 连接池释放、数据源异常留痕、TDengine 最后采集时间改查超级表、启动兜底日志 NameError、return-in-finally 死代码、删除 `tdengine_ws_dsn`、docker-compose tdengine 补 `TZ=Asia/Shanghai`。
