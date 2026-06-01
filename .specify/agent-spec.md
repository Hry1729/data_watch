# Feature Specification: DataWatch Agent

**Feature Branch**: `001-data-watch-console`
**Created**: 2026-05-27
**Status**: Draft
**Dependency**: `.specify/spec.md`（前端 Console）、`.specify/plan.md`、`docs/设计/Data Watch Console 页面设计说明.md`

---

## 概述

DataWatch Agent 是一个**本机 HTTP + WebSocket 服务**，监听 `127.0.0.1:7070`，作为 Data Watch Console 桌面应用与底层系统之间的唯一代理层：

- Bootstrap：首次安装时初始化 MySQL 连接配置
- 认证：查询 `sys_user` 表（PBKDF2-SHA256），签发 Agent JWT（HS256，有效期 8 小时）
- 进程管理：以正确工作目录启动/停止/重启 collector / writer，采集运行指标
- 配置读写：读写 `config.toml`（密码 DPAPI 加密），暴露为 REST API
- 健康检查：按需测试 Redis / MySQL / TDengine 连接
- 日志流：tail collector / writer 各自写入的日志文件（`logs/collector/current.log`、`logs/writer/current.log`，相对 `working_dir`），通过内存 LogBuffer 向 WebSocket 订阅者推送最近 100 行 + 实时新行

Agent 是**独立本机进程**，不嵌入 Tauri。生产部署时推荐安装为 Windows Service。

---

## User Stories

### US-A1 — Bootstrap 首次配置 (Priority: P1)

首次安装后，Agent 启动时处于 bootstrap 模式（MySQL 未配置）。管理员打开 Console，填写 MySQL 连接参数，测试成功后完成初始化，跳转登录页。

**Acceptance Scenarios**:

1. **Given** Agent 刚启动且 `bootstrap_done = false`，**When** `GET /bootstrap/status`，**Then** 返回 `{ "in_bootstrap": true }`，HTTP 200。
2. **Given** `in_bootstrap: true`，**When** `PUT /bootstrap/config { host, port, user, password, database }`，**Then** Agent 将 MySQL 配置写入 `config.toml`（password DPAPI 加密），返回 200。
3. **Given** `PUT /bootstrap/config` 已成功写入 config，**When** `POST /bootstrap/test-connection { "target": "mysql" }`（仅传 target 字段，无连接参数），**Then** Agent 使用已写入 config.toml 的 MySQL 配置测试连接，成功返回 `{ "success": true }`，失败返回 `{ "success": false, "message": "..." }`（消息不含密码明文）。
4. **Given** 测试成功后，**When** `POST /bootstrap/finish`，**Then** Agent 将 `bootstrap_done = true` 写入 `config.toml`，返回 `{}`，HTTP 200；之后 `GET /bootstrap/status` 返回 `{ "in_bootstrap": false }`。
5. **Given** `in_bootstrap: false`，**When** 调用 `PUT /bootstrap/config` 或 `POST /bootstrap/finish`，**Then** 返回 403 Forbidden，防止意外覆盖。
6. **Given** `POST /bootstrap/test-connection`，**Then** 仅测试连接，不修改任何配置。

---

### US-A2 — 用户认证 (Priority: P1)

初始化完成后，管理员在登录页输入账号密码，Agent 查询 MySQL 用户表签发 JWT。

> **Auth 表已确认**：表 `sys_user`，用户名列 `user_name`，密码列 `password_hash`（算法 PBKDF2-SHA256，格式 `pbkdf2_sha256$600000$<salt>$<digest_hex>`），状态列 `status = "1"` 为启用，`deleted = false` 为有效账号，且须分配至少一个 `sys_role.enabled = true` 的角色方可登录。

**Acceptance Scenarios**:

1. **Given** MySQL 已配置，用户存在且密码验证通过，**When** `POST /auth/login { "username": "admin", "password": "..." }`，**Then** 返回 `{ "token": "<JWT>" }`，HTTP 200。JWT 使用 HS256，有效期 8 小时（可配置）。
2. **Given** 用户名不存在或密码不匹配，**When** `POST /auth/login`，**Then** 返回 401 `{ "detail": "用户名或密码错误" }`；不区分"用户不存在"与"密码错误"。
3. **Given** 有效 JWT，**When** 调用任何 `/viewer/*` 或 `/admin/*` 接口，**Then** 请求正常处理。
4. **Given** JWT 过期或无效，**When** 调用受保护接口，**Then** 返回 401；前端收到后触发 `logout()` 并跳转登录页。
5. **Given** Agent 重启，**When** 前端用重启前的 JWT 访问（未过期、jwt_secret 未变），**Then** 请求正常处理。

---

### US-A3 — 进程状态监控 (Priority: P1)

运维人员打开总览页，查看 collector / writer 的运行状态、PID、CPU、内存、运行时长。

**Acceptance Scenarios**:

1. **Given** collector 进程正在运行，**When** `GET /viewer/processes/collector/metrics`，**Then** 返回完整 `ModuleStatus`：name, status="running", pid, uptime_seconds, memory_mb, cpu_percent, started_at（ISO 8601 + 时区），restart_count。
2. **Given** collector 进程未运行，**When** `GET /viewer/processes/collector/metrics`，**Then** 返回 `{ status: "stopped", pid: null, uptime_seconds: 0, memory_mb: 0.0, cpu_percent: 0.0, started_at: null, restart_count: <当前值> }`。
3. **Given** collector 以非零退出码崩溃，**When** `GET /viewer/processes/collector/metrics`，**Then** status = "crashed"。
4. **Given** `GET /viewer/processes/{name}/status`，**Then** 返回 `{ "status": "running" | "stopped" | "crashed" | "unknown" }`（轻量接口）。
5. **Given** `GET /viewer/status`，**Then** 返回 `{ "agent": "running" }`，HTTP 200（Agent 存活心跳）。
6. **Given** name 不是 `collector` 或 `writer`，**When** 调用任何进程接口，**Then** 返回 422 Unprocessable Entity。

---

### US-A4 — 进程控制 (Priority: P1)

运维人员在总览页点击启动/停止/重启按钮，Agent 管理子进程生命周期。

**Acceptance Scenarios**:

1. **Given** collector 未运行，**When** `POST /admin/processes/collector/start`，**Then** Agent 以 `cwd = config.collector.working_dir` 启动子进程，捕获 stdout/stderr，返回 `{}`，HTTP 200；随后 `GET /metrics` 返回 status="running"。
2. **Given** collector 正在运行，**When** `POST /admin/processes/collector/stop`，**Then** Agent 先发优雅停止（Windows：CTRL_BREAK_EVENT），10 秒超时后 force kill；返回 `{}`，HTTP 200。
3. **Given** `POST /admin/processes/collector/restart`，**Then** Agent 执行 stop（同上）再 start，restart_count + 1，返回 `{}`，HTTP 200。
4. **Given** `executable` 路径不存在，**When** `POST start`，**Then** 返回 `{ "detail": "executable not found: <path>" }`，HTTP 400。
5. **Given** collector 已在运行，**When** 再次 `POST start`，**Then** 返回 `{ "detail": "collector is already running" }`，HTTP 409。

---

### US-A5 — 配置读写 (Priority: P1)

管理员打开设置页，读取并修改 Redis / MySQL / TDengine 连接参数及 collector / writer 启动路径。

**Acceptance Scenarios**:

1. **Given** `config.toml` 存在，**When** `GET /admin/config`，**Then** 返回 `ConnectionConfig`，所有 `password` / `password_enc` 字段**不出现在响应中**（非 `"****"`，是完全省略），HTTP 200。
2. **Given** `PUT /admin/config { "redis": { "host": "10.0.0.1" } }`，**Then** Agent 仅更新 redis.host，其余字段保留，写入 `config.toml`，返回 `{}`，HTTP 200。
3. **Given** `PUT /admin/config { "mysql": { "password": "new-pass" } }`，**Then** Agent DPAPI 加密后写入 `config.toml`，原有其余 mysql 字段不变。
4. **Given** `PUT /admin/config` 中 mysql section 无 `password` key，**Then** 保留 `config.toml` 中原有加密密码，不修改（C3 规则）。
5. **Given** `PUT /admin/config { "mysql": { "password": "" } }`，**Then** 返回 `{ "detail": "password 不允许为空串；如需保留原密码请省略 password 字段" }`，HTTP 400（C5 规则）。

---

### US-A6 — 健康检查 (Priority: P2)

管理员在设置页点击「测试」按钮，验证指定数据库连接。

**Acceptance Scenarios**:

1. **Given** Redis 连接正常，**When** `POST /admin/health/check { "target": "redis" }`，**Then** 返回 `{ "success": true }`，HTTP 200。
2. **Given** MySQL 连接失败，**When** `POST /admin/health/check { "target": "mysql" }`，**Then** 返回 `{ "success": false, "message": "Connection refused" }`，消息不含密码。
3. **Given** target 为 `tdengine`，**Then** 测试 TDengine 连接，格式同上。
4. **Given** target 不是三者之一，**Then** 返回 422 Unprocessable Entity。
5. **Given** Bootstrap 未完成（config 未初始化），**When** 调用 health check，**Then** 返回 400，提示需先完成 Bootstrap。

---

### US-A7 — 实时日志流 (Priority: P1)

运维人员进入 Collector 或 Writer 详情页，日志面板实时显示该模块的最新日志。

**Acceptance Scenarios**:

1. **Given** 前端建立 `WS /viewer/logs/collector/tail`，**When** 连接打开后发送第一帧 `{ "type": "auth", "token": "<JWT>" }`，**Then** Agent 验证 JWT，通过后开始推送。
2. **Given** JWT 验证失败或无 auth 帧，**When** 超过 10 秒，**Then** Agent 以 close code `4001` 关闭连接，不推送任何数据。
3. **Given** JWT 验证通过，**Then** Agent 先推送最近 100 行历史日志（tail），再实时追加新行。
4. **Given** collector 产生新 stdout/stderr，**Then** Agent 将其格式化为 `{ "timestamp": "...", "level": "INFO", "source": "collector", "message": "..." }` JSON 推送给所有已鉴权的 WS 订阅者。
5. **Given** 日志行无法解析为 JSON，**Then** 以 `level="INFO"` 降级包装原始文本推送。
6. **Given** name 不是 `collector` 或 `writer`，**When** 建立 WS 连接，**Then** Agent 立即以 close code `1008`（Policy Violation）关闭连接。

---

## Requirements

### Functional Requirements

**Bootstrap**
- **FR-A01**: Bootstrap 模式（`bootstrap_done=false`）下，仅 `/bootstrap/*`、`/auth/login`、`/viewer/status` 可无 JWT 访问；所有 `/viewer/processes/*` 和 `/admin/*` 必须要求 JWT。
- **FR-A02**: Bootstrap 完成后（`bootstrap_done=true`），`PUT /bootstrap/config` 和 `POST /bootstrap/finish` 必须返回 403。
- **FR-A03**: `POST /bootstrap/test-connection` 请求体**只接收 `{ "target": "mysql" }`**，不接收 host / port 等连接参数；Agent 使用已通过 `PUT /bootstrap/config` 写入 `config.toml` 的 MySQL 配置执行实际测试，不写入任何配置。

**认证**
- **FR-A04**: JWT 密钥（`jwt_secret`）在 Agent 首次启动时随机生成（32 字节，hex 编码），写入 `config.toml`，后续复用；重新生成密钥会使所有现有 token 失效。
- **FR-A05**: JWT 有效期默认 8 小时，由 `config.toml` 中 `agent.jwt_expire_hours` 控制。
- **FR-A06**: 登录验证查询 `sys_user` 表，条件 `user_name = ?` + `deleted = false` + `status = "1"`；使用 PBKDF2-SHA256（`pbkdf2_sha256$600000$<salt>$<digest_hex>`）比对 `password_hash` 字段；通过后还须确认该用户在 `sys_user_role + sys_role` 中存在至少一个 `enabled = true` 的角色。认证失败统一返回 401（不区分"用户不存在"与"密码错误"）；账号禁用返回 403。

**进程管理**
- **FR-A07**: 启动子进程时必须设置 `cwd = working_dir`（来自 config）；`working_dir` 为空时返回 400。
- **FR-A08**: 日志来源为 **Method B（日志文件 tail）**：collector / writer 进程使用 `RotatingFileHandler` 自写日志文件（路径 `logs/{name}/current.log`，相对各自 `working_dir`）；Agent 以后台线程 seek-to-end + 循环 readline 监控文件新增行，写入内存 LogBuffer（maxlen=100）并向 WS 订阅者广播；Agent 本身不额外写 RotatingFileHandler（文件轮转由子进程负责）。
- **FR-A09**: 进程 CPU %、内存 MB 使用 `psutil` 采集；CPU 由后台监控循环（asyncio `health_check_loop`，每 2s，复用持久 `psutil.Process` 调 `cpu_percent(interval=None)` 非阻塞）更新缓存，`GET /metrics` 直接返回缓存值，避免每次请求阻塞。**注意：CPU/内存仅反映被跟踪 pid 本身，不含其子进程树**；若 collector/writer 的启动器属于「父进程派生真实 worker 子进程」型（如 uv venv 的 `python.exe` shim、PyInstaller onefile 引导器），被跟踪的父 pid 会显示近 0 占用——生产部署必须让 ProcessManager 跟踪真实 worker 进程（用真解释器 / onedir 打包 / 直接指向 worker exe）。
- **FR-A10**: 子进程退出后，status 根据退出码更新：0 → `stopped`，非零 → `crashed`。
- **FR-A11**: 停止进程采用 **shutdown_flag 文件协议**：① Agent 在 `runtime/{name}.shutdown_flag` 写入停止标志；② 轮询子进程最多 10 秒等待其自行退出；③ 超时后 `terminate()`，仍未退出再 `kill()` 强制结束；停止完成后删除 pid 文件与 shutdown_flag。**约束：collector / writer 必须在主循环中监听各自的 `runtime/{name}.shutdown_flag` 文件，检测到标志后执行优雅退出（flush 缓冲、关闭连接）并自行结束进程**；未实现该监听的子进程只能被 terminate/kill 强杀，无法保证优雅退出。须在真实 collector/writer exe 上验收 shutdown_flag 优雅退出与超时强杀两条路径。
- **FR-A12**: `restart_count` 持久化到 `runtime/state/process_state.json`（JSON，按模块名记录），Agent 重启后保留；`restart()` 成功后该计数 +1。`GET /metrics`、`GET /status` 各状态分支（running / stopped / crashed）均返回该持久化值。

**配置**
- **FR-A13**: `GET /admin/config` 响应中所有 password / password_enc 字段**必须省略**（不返回 `****` 或任何占位符）。
- **FR-A14**: 密码在 `config.toml` 中使用 DPAPI 加密存储（Windows）；config.toml 与 Windows 用户账户和机器绑定，不可直接跨机迁移。
- **FR-A15**: `PUT /admin/config` 对 password 字段执行 partial update，严格遵守 C1–C6（见 `.specify/plan.md §4.6`）。
- **FR-A16**: `PUT /admin/config` 中 password 值为空串时返回 400（C5 规则）。

**日志**
- **FR-A17**: 日志行 JSON 格式固定：`{ "timestamp": <ISO 8601 with tz>, "level": <"DEBUG"|"INFO"|"WARN"|"ERROR">, "source": <"collector"|"writer">, "message": <string> }`。
- **FR-A18**: WS 连接建立后，前端 10 秒内未发 auth 帧，Agent 以 close code `4001` 主动关闭。
- **FR-A19**: WS 历史 tail 最多推送最近 100 行，避免首次连接大量传输。
- **FR-A20**: WS 订阅者集合中，某个连接断开时静默移除，不影响其他订阅者。

**安全**
- **FR-A21**: 任何 logging / print 输出不得包含密码明文或 JWT token 值。
- **FR-A22**: Agent 仅监听 `127.0.0.1`（loopback），不对外网绑定。
- **FR-A23**: 错误响应消息不暴露内部堆栈、文件路径或密码值。

---

## Key Entities

- **BootstrapState**: 布尔值（`bootstrap_done`），存于 `config.toml`。Agent 启动时加载；`true` 后不可逆（需手动删除 config 才能重新 bootstrap）。
- **AgentConfig**: `config.toml` 的内存表示，包含 agent、redis、mysql、tdengine、collector、writer 六个 section。
- **ProcessState**: 进程状态通过两类磁盘文件维护（**无内存 Popen 句柄**，Agent 重启后仍可追踪子进程、判定 crashed）：`runtime/pids/{name}.pid`（TOML，记录 `pid / create_time / started_at / module`，以 `psutil` create_time 校验防 PID 复用误判）；`runtime/state/process_state.json`（JSON，记录每模块 `restart_count`）。运行时 `status / pid / memory_mb / cpu_percent / uptime` 由 `psutil` 实时查询。
- **LogBuffer**: 每个模块对应一个内存环形缓冲区（最近 100 行），WS 建立时先推 tail，再追加新行。滚动日志文件同步写入。
- **WsSubscriberSet**: 每个模块一个集合，存储已鉴权的 WebSocket 连接；子进程产生新输出时广播给全部连接。

---

## API 契约摘要

| 方法 | 路径 | JWT 要求 | 描述 |
|------|------|---------|------|
| GET | /bootstrap/status | 无 | Bootstrap 状态查询 |
| PUT | /bootstrap/config | 无（bootstrap 中）/ 403（完成后）| 写入 MySQL 初始配置 |
| POST | /bootstrap/test-connection | 无（bootstrap 中）/ 403（完成后）| 测试 MySQL 连接 |
| POST | /bootstrap/finish | 无（bootstrap 中）/ 403（完成后）| 标记 bootstrap 完成 |
| POST | /auth/login | 无 | 登录，获取 Agent JWT |
| GET | /viewer/status | 无 | Agent 存活心跳 |
| GET | /viewer/health | JWT | Redis/MySQL/TDengine 聚合健康快照 |
| GET | /viewer/processes/{name}/metrics | JWT | 进程完整指标 |
| GET | /viewer/processes/{name}/status | JWT | 进程状态（轻量）|
| POST | /admin/processes/{name}/start | JWT | 启动进程 |
| POST | /admin/processes/{name}/stop | JWT | 停止进程 |
| POST | /admin/processes/{name}/restart | JWT | 重启进程 |
| GET | /admin/config | JWT | 读取配置（无密码）|
| PUT | /admin/config | JWT | 更新配置（partial update）|
| POST | /admin/health/check | JWT | 按需健康检查 |
| WS | /viewer/logs/{name}/tail | JWT（第一帧）| 实时日志流 |

---

## Success Criteria

- **SC-A01**: 命令行启动 Agent 到 `GET /bootstrap/status` 可用，响应时间 < 3 秒。
- **SC-A02**: Bootstrap 全流程（填写 MySQL → 测试 → 完成）< 30 秒，无需查阅文档。
- **SC-A03**: `POST start` 到 `GET /metrics` 返回 status="running"，响应时间 < 5 秒（正常环境）。
- **SC-A04**: WS 建立连接并通过 auth 后，历史 tail 在 1 秒内全部推送到前端。
- **SC-A05**: 密码明文不出现在 config.toml、任何日志文件、任何 HTTP 响应体中。
- **SC-A06**: `POST stop` 后进程在 15 秒内停止（10 秒优雅 + 5 秒 force kill margin）。

---

## Exclusions（明确不做）

| 排除项 | 说明 |
|--------|------|
| GET /viewer/trends/* | 趋势图数据端点，待后续设计 |
| 自动重启策略 | 崩溃后不自动重启；restart_count 仅统计手动重启 |
| 远程多客户端 | Agent 仅服务 127.0.0.1，不对外网暴露 |
| Token 刷新接口 | MVP 不实现；401 统一跳登录页 |
| Tauri 内嵌 Agent | Agent 是独立进程，Tauri 仅做 HTTP proxy |
| 密码清空操作 | 当前契约不支持；需独立接口，不复用 partial update |
| Backend refreshToken 续签 | 由 Backend 自身负责，Agent 不介入 |
| JWT 黑名单 / 强制失效 | logout 清除前端内存即可，Agent 不维护黑名单 |

---

## 已确认事项

所有实现前阻断项已确认，可进入实现阶段。

| 项 | 确认结果 |
|----|---------|
| **Auth 用户表** | 表 `sys_user`；用户名列 `user_name`；密码列 `password_hash` |
| **密码哈希** | PBKDF2-SHA256，格式 `pbkdf2_sha256$600000$<salt_hex>$<digest_hex>`（与 `backend/app/core/security.py` 一致）|
| **账号有效条件** | `deleted = false` + `status = "1"` + 至少一个 `sys_role.enabled = true` 的已分配角色 |
| **角色过滤** | 无特定 role_code 限制；任意已分配且启用的角色均可登录 Agent |
| **日志来源** | Method B（文件 tail）；路径 `{working_dir}/logs/{name}/current.log` |
| **HTTP 框架** | FastAPI + Uvicorn |
