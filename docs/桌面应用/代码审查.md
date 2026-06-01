# DataWatch Agent 代码审查记录

审查流程：Claude 规划协调 → Codex 审查 → Claude 复审 → 修复 → 记录

---

## Phase 0 · A001+A002 骨架与基础（2026-05-28）

| # | 级别 | 文件 | 问题 | 解决方法 |
|---|------|------|------|---------|
| 1 | 高/安全 | `exceptions.py` | Pydantic v2 `exc.errors()` 含 `input` 字段，校验失败时密码/token 可能回显响应 | 过滤只保留 `type/loc/msg`，丢弃 `input` |
| 2 | 高/JWT | `security.py` | `verify_token` 未显式检查 `exp`/`sub`，缺失时抛 `KeyError` 变 500 而非 401 | 显式检查两字段，缺失主动抛 `JWTError` |
| 3 | 中/契约 | `deps.py` | `Header(...)` 必填语义，缺 `Authorization` 返回 422 而非 401 | 改 `Header(default=None)` 自行判断，返回 401 + `WWW-Authenticate: Bearer` |
| 4 | 中/契约 | `deps.py` | `except JWTError` 漏掉 `KeyError`，缺 `sub` 变 500 | 改为 `except (JWTError, KeyError)` |
| 5 | 中/安全 | `security.py` | `verify_password` 未限制 iterations，超大值导致 CPU DoS，0/负数抛未捕获异常 | 加范围检查：`1 ≤ iterations ≤ 10_000_000`，超出返回 False |

**推迟至 Phase 7**：config.toml ACL/原子写、load_config schema 校验、dpapi_decrypt 异常归一化、服务端日志。

---

## Phase 1 · A003 Bootstrap Router（2026-05-29）

| # | 级别 | 文件 | 问题 | 解决方法 |
|---|------|------|------|---------|
| 1 | 高/安全 | `routers/bootstrap.py` | `PUT /bootstrap/config` 写入 `password_enc` 后未删除可能残留的 `password` 明文字段，导致明文密码随 `save_config` 落盘 | 写入前调用 `cfg["mysql"].pop("password", None)` |
| 2 | 中/契约 | `schemas/models.py` | `TestConnectionRequest` 未禁止额外字段，可接受 `{"target":"mysql","host":"x"}` 违反"只接收 target"契约 | 加 `extra="forbid"`：`class TestConnectionRequest(BaseModel, extra="forbid")` |

---

## Phase 2 · A004 Auth Router（2026-05-29）

| # | 级别 | 文件 | 问题 | 解决方法 |
|---|------|------|------|---------|
| 1 | 高/安全 | `routers/auth.py` | 检查顺序：先判 `status != '1'` 再 `verify_password` → 禁用账户错密码返 403、正常账户错密码返 401，差异化响应构成账号枚举侧信道 | 把密码验证提到 status 判断之前；顺序改为：deleted/不存在 → password → status → roles |

**Codex 建议但 Claude 驳回**：
- `verify_password(password, None)` 防御：DB 层 `sys_user.password_hash NOT NULL` 已保证不可能，按"不防御不可能场景"驳回

**实际字段确认**：`sys_role.role_code`（不是文档里的 `role_key`）；Phase 2 仅检查 `r.enabled = 1`，role_code 推迟到 Phase 3 admin 中间件使用。

---

## Stage 0 · collector/writer shutdown_flag 支持（2026-05-29）

> 目的：让 collector/writer 主循环支持外部 `shutdown_flag` 文件触发优雅退出，为 Stage 2 ProcessManager 调 stop 做前置。改动只加 helper、不动主循环内部。

| # | 级别 | 文件 | 问题 | 解决方法 |
|---|------|------|------|---------|
| 1 | 低/lint | `collector/main.py` | Codex 新增 import 时遗留重复 `import os`（顶部 import 区出现两次） | 删除 Codex 新加的那行，保留原有 `import os` |
| 2 | 中/运行时 | `writer/main.py` | `consumer_task` / `watchdog_task` 在 `try` 块内 `create_task`，若 create_task 异常 → `finally` 的 `watchdog_task.cancel()` 触发 NameError，掩盖原异常 | 把两个 `asyncio.create_task` 调用挪到 `try:` **外** |

**Codex 建议但 Claude 驳回**：
- watchdog 协程内非 `CancelledError` 异常会被 `finally` 的 `await watchdog_task` 重抛、阻断后续 cleanup → 异常源 `flag_path.exists()` 已被 `try/except OSError` 兜底，`asyncio.sleep` 不抛非 CancelledError；watchdog 正常退出是 return，cancel 退出是 CancelledError（已捕获）；实际不可能走到该路径
- collector watchdog 在 signal handler 注册前启动 → Codex 自评低风险，signal 注册本身不抛异常，跳过

**验证**：
- `py_compile collector/main.py writer/main.py` SYNTAX OK
- `python collector/main.py --help` 包含 `--runtime-dir` 选项
- 隔离测试 `_resolve_runtime_dir`：arg / `DATAWATCH_HOME` / 默认 三档优先级全过
- 隔离测试 `_watch_shutdown_flag`：touch flag → 1.5s 内 collector `app.stop()` 被调；writer `consumer_task` 被 cancel
- 端到端（真实 collector/writer 启停）推迟至 Stage 2 ProcessManager 完成后

---

## Stage 2.1 · ProcessManager 骨架 start/stop/status（2026-05-29）

> 范围：新建 `agent/app/services/process_manager.py`（ProcessManager 类）；改 `agent/app/core/config.py`（`agent.runtime_dir`、collector/writer 的 `args`）；改 `agent/app/factory.py`（lifespan 实例化 `app.state.pm`）。**不**做：健康检查协程（Stage 2.2）、HTTP 接口（Stage 2.3）、stdout 日志重定向（Stage 3）、自动重启。

| # | 级别 | 文件 | 问题 | 解决方法 |
|---|------|------|------|---------|
| 1 | 高/正确性 | `services/process_manager.py` | `start()` 中 `_write_pid_file` 失败 → 子进程已 spawn、PID 文件未落、Agent 无法追踪 → 残留僵尸进程 | 捕获 `OSError` → `terminate()`/`kill()` 已起的子进程 → 抛 `ProcessManagerError` |
| 2 | 中/API 一致性 | `services/process_manager.py` | `stop()` 中 `flag.write_text` 失败抛裸 `OSError`，破坏 public API 错误类型一致性 | 包装为 `ProcessManagerError` |
| 3 | 低/防御 | `services/process_manager.py` | `stop()` stale PID 分支 `_flag_file().unlink(missing_ok=True)` 仍可能因文件锁/权限抛 `OSError` | 加 `try/except OSError` + `logger.warning` |
| 4 | 低/代码卫生 | `factory.py` | `# Phase 3: 初始化 ProcessManager` 注释已实现却未删 | 删该行；保留并改写 LogManager 占位为 "Stage 3 将在此处初始化 LogManager（日志 tail）" |

**Codex 建议但 Claude 驳回**：
- `stop()` terminate 失败仍清理 PID 文件 → 应保留 PID + 抛错：Windows terminate 失败概率极低；Stage 2.2 健康检查会发现错乱状态报警；MVP 范围按"不过度防御"驳回
- `_verify_alive` 未显式拒绝 `pid <= 0` / 未捕获 `TypeError`：PID 文件由 Agent 自己 `toml.dumps` 写，pid 类型由 `subprocess.Popen` 给（`int`），不会出现字符串或负数；按"不防御不可能场景"驳回

**关键设计决策**（哥哥拍板）：
- **executable 形态**：MVP 用 `python.exe + main.py + args` 命令组合；Stage 8 切 PyInstaller 后只改 `config.toml` 字段、不动 ProcessManager
- **重启策略**：MVP 默认关闭自动重启（设计文档 §5.3）
- **健康检查间隔**：固定 5s（写代码常量，不配置化）
- **PID 三重校验简化为双校验**：MVP 用 `pid + create_time`（psutil 提供），**不**对进程名（Python 模式下都是 `python.exe`，无区分意义）；切 PyInstaller 后可再加进程名校验

**验证**：
- `py_compile process_manager.py / factory.py / config.py` SYNTAX OK
- 隔离行为测试 6 条全过（mock 子进程，不依赖真实 collector/writer）：
  - **A** 子进程不响应 flag → `stop(timeout=2s)` → terminate 强杀 → PID/flag 清理
  - **B** 子进程响应 flag → 优雅退出（无 terminate 日志）
  - **C** mock 启动后 1s 自死 → `status()` 报 `crashed`（PID 文件残留 + 进程不存在）
  - **D** 同名 `start()` 幂等（第二次返回相同 pid，不重复 spawn）
  - **E** 未配置 executable → `ProcessManagerError`
  - **F** unknown module → `ProcessManagerError`
- 端到端（真实 collector/writer 启停 + Agent 重启 PID 接管）推迟至 Stage 2.2 / Stage 2.3 完成后做

**留待 Stage 2.2/2.3**：
- 健康检查协程（5s 定时 + crashed 事件）
- Agent 重启后用双校验恢复监控
- per-name `asyncio.Lock`（Stage 2.3 暴露 HTTP 接口前加）
- `GET /viewer/processes/{name}/status` 与 `/metrics`

---

## Stage 2.2 · 健康检查协程（2026-05-29）

> 范围：`process_manager.py` 加 `_HEALTH_CHECK_INTERVAL = 5.0` 常量、`self._last_status` 字典、`async def health_check_loop()` 协程；`factory.py` lifespan 起 background task + shutdown 时 cancel。**不**做：crashed 事件 SSE 推送 / 自动重启 / HTTP 接口 / per-name 锁。

| # | 级别 | 文件 | 问题 | 解决方法 |
|---|------|------|------|---------|
| — | — | — | Codex 实施一次性通过，Claude 复审 + Codex 自审均**无必修问题** | — |

**Codex 自报偏离方案（Claude 接受）**：
- ERROR log 条件从"任何转换到 `crashed` 都打"**收窄**为 `prev == "running" and current == "crashed"`。理由：按设计文档 §5.3 "进程意外退出"语义，告警应仅针对"运行中突然死亡"的转换；Agent 启动时 PID 残留 + 进程已死的"上轮残留 crashed"不重复报警。Claude 复审后认同此为更准确的语义实现。

**Codex 自审建议改进（驳回）**：
- `factory.py` shutdown 时 `await health_task` 只接 `CancelledError`；若 health task 因非预期异常提前结束会重抛 → `health_check_loop` 内部 try/except 已覆盖所有异常源（单 module `status()` 失败 `continue`；外层只 re-raise `CancelledError`）；非预期异常实际不会发生。按"不防御不可能场景"驳回。

**验证**：
- `py_compile process_manager.py / factory.py` SYNTAX OK
- 隔离行为测试 4 条全过（mock 子进程 + 自定义 logging.Handler 捕获 log records）：
  - **G** loop 启动 + cancel + 生命周期 log（"health check started" / "stopped"）
  - **H** `start()` 真实 mock → 第 1 次 tick 记录 `running` → `psutil.Process(pid).kill()` → 第 2 次 tick → **ERROR log `module mock_h crashed (previous status: running)`**
  - **I** 第 1 次 tick 看到 crashed（伪 PID 文件 + 不存在的 pid）→ 静默不报警；`_last_status` 仍记录 `crashed` 防下次重复
  - **J** loop 可正常 `cancel()` + `await`

**留待 Stage 2.3**：
- `GET /viewer/processes/{name}/status` + `/metrics` HTTP 接口（含 `ProcessStatusResponse` / `ModuleStatusResponse` 接入）
- per-name `asyncio.Lock`（暴露 HTTP 前必加，防止并发 `start`/`stop` 竞态）
- 端到端真实启动 collector/writer（用环境变量 / 手动改 config.toml 配 python 解释器 + main.py 路径）

---

## Stage 2.3 · per-name Lock + ProcessInfo 扩展 + Viewer HTTP（2026-05-29）

> 范围：`process_manager.py` 加 `self._locks: dict[str, asyncio.Lock]`；`start()` 改 `async def`；`start/stop` 内 `async with self._locks[name]`；`ProcessInfo` 加 `memory_mb` / `cpu_percent` / `restart_count` 字段；`_info_running` 用 psutil 取内存/CPU；`routers/viewer.py` 加 `/viewer/processes/{name}/status` 和 `/metrics`，`Depends(get_current_user)`，未知 name 返 404。**不**做：admin start/stop API（Stage 4）/ crashed SSE / 自动重启 / PyInstaller 打包 / `asyncio.to_thread` 包装 spawn。

| # | 级别 | 文件 | 问题 | 解决方法 |
|---|------|------|------|---------|
| — | — | — | Codex 实施一次性通过，Claude 复审 + Codex 自审均**无必修问题** | — |

**Codex 自审建议（共识为"已知限制"，本阶段不修）**：
- `cpu_percent` 语义陷阱：`_info_running` 每次都 `psutil.Process(pid)` 创建新实例，`proc.cpu_percent(interval=None)` 首次返回 `0.0` → `/metrics` 长期看到 `cpu_percent=0.0`。要让 CPU 指标有意义，需在 PM 内缓存 `psutil.Process` 或 cpu 采样状态。**MVP 接受 cpu_percent=0**（业务指标 Stage N+ 接 Redis 时统一改进）。

**关键设计选择**：
- `subprocess.Popen` 保留同步（未用 `asyncio.to_thread`）：spawn 通常 <100ms，event loop 卡顿可接受；MVP 不引入 ThreadPool 调度开销
- `/viewer/status` 健康探针**保持无鉴权**：前端启动时检测 Agent 在线、无敏感信息；与设计文档 §5.7 "viewer 接口需登录"有微小偏差，按健康探针约定保留
- 未知 module name → 404；已知 name 未启动 → 200 + `status="stopped"`
- `ProcessInfo` 三新字段在 stopped/crashed 状态默认 `0.0/0.0/0`（dataclass default）；`start()` 早期路径不显式赋值

**验证**：
- `py_compile process_manager.py / viewer.py` SYNTAX OK
- 隔离单元测试 3 条全过：
  - **K** `await pm.start("mock_k")` → `pm.status("mock_k")` 返回 `memory_mb=3.9, cpu_percent=0.0, restart_count=0`（验证字段扩展 + 已知限制）
  - **L** `asyncio.gather(pm.start("mock_l"), pm.start("mock_l"))` 串行化：两次返回**同 pid**；`asyncio.gather(stop, stop)` 也串行 → 锁正确
  - **N** stopped 状态 → `memory_mb=0.0 / cpu_percent=0.0 / restart_count=0` 默认值正确
- Agent 重启后真实 HTTP smoke 6 条全过：
  - **1** 无 token 访问 `/viewer/processes/collector/status` → **401**（`WWW-Authenticate: Bearer`）
  - **2** Admin JWT + `/viewer/processes/collector/status` → 200 `{status: "stopped"}`
  - **3** `/viewer/processes/collector/metrics` → 完整 `ModuleStatusResponse`：`name=collector / status=stopped / pid=null / memory_mb=0 / cpu_percent=0 / restart_count=0`
  - **4** 不存在的 module name → **404**
  - **5** `/viewer/status`（健康探针，无鉴权）→ 200 `{agent: "running"}`
  - **6** `/viewer/processes/writer/metrics` → 200 stopped（PID 文件不存在）

**留待 Stage 4 / 后续**：
- Admin 启停 API（`POST /admin/processes/{name}/start|stop|restart` + Admin JWT role_level 中间件，需结合 `sys_role.role_code` R_SUPER/R_ADMIN 实时查 DB）
- 真实 collector/writer 端到端启停（手配 config.toml 的 `[collector]` `executable+args+working_dir` 字段；Stage 4 admin API 完成后做更顺）
- crashed 事件 SSE 推送（Stage 6）
- `cpu_percent` 缓存 `psutil.Process` 实例（业务指标接入时统一做）
- `/viewer/status` 是否纳入鉴权（设计文档 §5.7 vs 健康探针约定）由 Stage 6 客户端联调时定

---
