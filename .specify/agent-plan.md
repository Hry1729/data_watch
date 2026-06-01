# 实现计划：DataWatch Agent

**Branch**: `001-data-watch-console` | **Date**: 2026-05-27 | **Spec**: `.specify/agent-spec.md`

---

## 1. 架构全景

```
┌──────────────────────────────────────────────────────────────────┐
│  Data Watch Console（Tauri WebView2）                             │
│  React → invoke('agent_request') → Rust HTTP Proxy               │
│  React → new WebSocket(ws://127.0.0.1:7070/viewer/logs/*/tail)  │
└────────────────────────────┬─────────────────────────────────────┘
                             │ HTTP + WS（127.0.0.1:7070）
                             ▼
┌──────────────────────────────────────────────────────────────────┐
│  DataWatch Agent（Python FastAPI + Uvicorn，监听 127.0.0.1:7070）   │
│                                                                  │
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────────────────┐│
│  │ Bootstrap   │  │ Auth（JWT）  │  │ ProcessManager          ││
│  │ Router      │  │ Router       │  │  collector / writer     ││
│  └─────────────┘  └──────────────┘  └─────────────────────────┘│
│  ┌─────────────┐  ┌──────────────┐  ┌─────────────────────────┐│
│  │ Admin       │  │ Viewer       │  │ LogManager              ││
│  │ Router      │  │ Router       │  │  LogBuffer + WS fanout  ││
│  └─────────────┘  └──────────────┘  └─────────────────────────┘│
│                                                                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ ConfigManager（config.toml + DPAPI 加密）                   │ │
│  └────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────┘
         │ subprocess.Popen         │ pymysql / redis / taos-ws
         ▼                          ▼
  [collector.exe]   [writer.exe]   Redis / MySQL / TDengine
```

---

## 2. 模块划分与文件结构

```
agent/
├── main.py                        # 入口：uvicorn 启动，注册 shutdown hook
├── config.toml                    # 运行时配置（gitignore）
├── config.toml.example            # 配置模板
├── requirements.txt
│
├── app/
│   ├── __init__.py
│   ├── factory.py                 # create_app()：注册 router、lifespan
│   │
│   ├── core/
│   │   ├── config.py              # AgentConfig：load / save / partial_update
│   │   ├── security.py            # JWT sign/verify + DPAPI encrypt/decrypt
│   │   ├── deps.py                # FastAPI Depends：get_current_user, get_config
│   │   └── exceptions.py          # 统一异常处理器
│   │
│   ├── schemas/
│   │   └── models.py              # Pydantic v2 请求/响应模型
│   │
│   ├── services/
│   │   ├── process_manager.py     # 进程生命周期 + psutil 指标
│   │   ├── log_manager.py         # 日志捕获 + 环形缓冲 + WS 广播
│   │   └── health_checker.py      # Redis / MySQL / TDengine 连接测试
│   │
│   └── routers/
│       ├── bootstrap.py           # GET/PUT/POST /bootstrap/*
│       ├── auth.py                # POST /auth/login
│       ├── viewer.py              # GET /viewer/status + /viewer/processes/* + WS /viewer/logs/*
│       └── admin.py               # POST /admin/processes/* + GET/PUT /admin/config + POST /admin/health/check
│
├── runtime/                       # 运行时状态目录（gitignore）
│   ├── pids/{name}.pid            # 子进程 PID 记录（TOML，含 create_time 校验）
│   ├── {name}.shutdown_flag       # 停止标志文件（stop 时写入，停止后删除）
│   └── state/process_state.json   # restart_count 等持久化状态（JSON）
│
├── logs/                          # 日志文件目录（gitignore）
│   ├── collector/collector.log
│   └── writer/writer.log
│
└── service/
    └── install_service.py         # Windows Service 安装脚本（Phase 7）
```

### 模块职责边界

| 模块 | 职责 | 不包含 |
|------|------|--------|
| `core/config.py` | config.toml 加载/保存/partial_update | 业务逻辑、HTTP 路由 |
| `core/security.py` | JWT HS256 签发/验证、DPAPI 加密/解密 | 数据库查询 |
| `core/deps.py` | FastAPI Depends 依赖注入（JWT 验证、config 获取）| 无 |
| `services/process_manager.py` | 启动/停止/重启子进程；psutil 指标采集；ProcessState 维护 | 配置读写、日志写入 |
| `services/log_manager.py` | 日志文件 tail 线程；LogBuffer；WS 订阅者广播 | 进程生命周期管理 |
| `services/health_checker.py` | 按需测试 Redis/MySQL/TDengine 连接 | 配置持久化 |
| `routers/*` | HTTP/WS 路由、请求验证、调用 service | 不直接访问数据库（健康检查除外）|

---

## 3. 配置文件 Schema

```toml
# config.toml — DataWatch Agent 配置
# 密码字段以 DPAPI 加密后的 base64 字符串存储（password_enc）
# config.toml 与 Windows 用户账户 + 机器绑定，不可跨机直接复制

[agent]
host               = "127.0.0.1"
port               = 7070
jwt_secret         = ""            # 首次启动时随机生成，32 字节 hex
jwt_expire_hours   = 8
bootstrap_done     = false
log_level          = "INFO"        # Agent 自身日志级别

[redis]
host               = "127.0.0.1"
port               = 6379
password_enc       = ""            # DPAPI 加密后的 base64；空串=无密码

[mysql]
host               = "127.0.0.1"
port               = 3306
user               = "root"
database           = "device_watch"
password_enc       = ""            # DPAPI 加密后的 base64

[tdengine]
host               = "127.0.0.1"
port               = 6041
user               = "root"
database           = "device_data"
password_enc       = ""            # DPAPI 加密后的 base64

[collector]
executable         = "collector.exe"
working_dir        = ""            # 必须配置，否则 start 返回 400

[writer]
executable         = "writer.exe"
working_dir        = ""            # 必须配置，否则 start 返回 400
```

---

## 4. API 详细设计

### 4.1 请求/响应模型（Pydantic）

```python
# schemas/models.py

# Bootstrap
class BootstrapStatusResponse(BaseModel):
    in_bootstrap: bool

class BootstrapConfigRequest(BaseModel):
    host: str
    port: int = Field(ge=1, le=65535)
    user: str
    password: str = Field(min_length=1)     # Bootstrap 时 password 必填
    database: str

class TestConnectionRequest(BaseModel):
    target: Literal["mysql"]                # Bootstrap 阶段只测 mysql

class HealthCheckRequest(BaseModel):
    target: Literal["redis", "mysql", "tdengine"]

class HealthCheckResponse(BaseModel):
    success: bool
    message: str | None = None

# Auth
class LoginRequest(BaseModel):
    username: str
    password: str

class LoginResponse(BaseModel):
    token: str

# Viewer
class ViewerStatusResponse(BaseModel):
    agent: str = "running"

class ServiceHealthItem(BaseModel):
    status: Literal["connected", "checking", "disconnected", "unknown"]
    latencyMs: int | None = None
    message: str | None = None
    checkedAt: str | None = None

class ViewerHealthResponse(BaseModel):
    redis: ServiceHealthItem
    mysql: ServiceHealthItem
    tdengine: ServiceHealthItem

class ModuleStatusResponse(BaseModel):
    name: Literal["collector", "writer"]
    status: Literal["running", "stopped", "crashed", "unknown"]
    pid: int | None
    uptime_seconds: int
    memory_mb: float
    cpu_percent: float
    started_at: str | None             # ISO 8601 with timezone
    restart_count: int

class ProcessStatusResponse(BaseModel):
    status: str

# Config — GET 响应（无密码）
class DbConfigOut(BaseModel):
    host: str
    port: int
    user: str | None = None
    database: str | None = None
    # password / password_enc 字段永远不出现

class ProcessConfigOut(BaseModel):
    executable: str
    working_dir: str

class ConnectionConfigOut(BaseModel):
    redis: DbConfigOut
    mysql: DbConfigOut
    tdengine: DbConfigOut
    collector: ProcessConfigOut
    writer: ProcessConfigOut

# Config — PUT 请求（Partial，password 可选）
class DbConfigIn(BaseModel):
    host: str | None = None
    port: int | None = None
    user: str | None = None
    password: str | None = None         # 不传 = 保留；传 "" = 400 错误
    database: str | None = None

    @model_validator(mode="after")
    def reject_empty_password(self):
        if self.password == "":
            raise ValueError("password 不允许为空串；如需保留原密码请省略 password 字段")
        return self

class ProcessConfigIn(BaseModel):
    executable: str | None = None
    working_dir: str | None = None

class ConnectionConfigIn(BaseModel):
    redis: DbConfigIn | None = None
    mysql: DbConfigIn | None = None
    tdengine: DbConfigIn | None = None
    collector: ProcessConfigIn | None = None
    writer: ProcessConfigIn | None = None
```

### 4.2 端点行为规范

| 端点 | 请求 | 成功响应 | 错误响应 |
|------|------|---------|---------|
| GET /bootstrap/status | — | `BootstrapStatusResponse` 200 | — |
| PUT /bootstrap/config | `BootstrapConfigRequest` | `{}` 200 | 403（完成后）；422（校验失败）|
| POST /bootstrap/test-connection | `{ target: "mysql" }` | `HealthCheckResponse` 200 | 403（完成后）；422 |
| POST /bootstrap/finish | — | `{}` 200 | 403（完成后）|
| POST /auth/login | `LoginRequest` | `LoginResponse` 200 | 401（认证失败）；503（MySQL 不可达）|
| GET /viewer/status | — | `ViewerStatusResponse` 200 | — |
| GET /viewer/health | — | `ViewerHealthResponse` 200 | 401 |
| GET /viewer/processes/{name}/metrics | — | `ModuleStatusResponse` 200 | 401；422（name 无效）|
| GET /viewer/processes/{name}/status | — | `ProcessStatusResponse` 200 | 401；422 |
| POST /admin/processes/{name}/start | — | `{}` 200 | 400（路径不存在/working_dir 为空）；409（已运行）|
| POST /admin/processes/{name}/stop | — | `{}` 200 | — |
| POST /admin/processes/{name}/restart | — | `{}` 200 | 400（同 start）|
| GET /admin/config | — | `ConnectionConfigOut` 200 | 401 |
| PUT /admin/config | `ConnectionConfigIn` | `{}` 200 | 400（空密码 C5）；401；422 |
| POST /admin/health/check | `HealthCheckRequest` | `HealthCheckResponse` 200 | 400（bootstrap 未完成）；401；422 |

---

## 5. 进程管理设计

### ProcessManager（基线：PID 文件 + shutdown_flag，无内存 Popen 句柄）

**设计要点**：不在内存持有 `subprocess.Popen` 句柄。进程信息落盘到 `runtime/`，使 Agent 重启后仍能追踪子进程并正确判定 `crashed`。

```python
@dataclass(frozen=True)
class ModuleSpec:
    name: str
    executable: str
    args: list[str]
    working_dir: str

@dataclass
class ProcessInfo:
    name: str
    status: Literal["running", "stopped", "crashed", "unknown"]
    pid: int | None
    started_at: str | None
    uptime_seconds: int
    memory_mb: float = 0.0
    cpu_percent: float = 0.0
    restart_count: int = 0

class ProcessManager:
    # runtime_dir/pids/{name}.pid           子进程 PID 记录（TOML）
    # runtime_dir/{name}.shutdown_flag       停止标志
    # runtime_dir/state/process_state.json   restart_count 持久化

    async def start(self, name) -> ProcessInfo:
        # 1. 若已有 pid 文件且 create_time 校验存活 → 幂等返回 running（409 由路由层用 status() 守卫）
        # 2. executable / working_dir 为空 → ProcessManagerError（路由映射 400）
        # 3. subprocess.Popen(cmd, cwd=working_dir, creationflags=CREATE_NO_WINDOW)
        #    Method B：不捕获 stdout/stderr，日志由子进程自写文件
        # 4. 写 pid 文件（pid + psutil create_time + started_at）；写盘失败则回收子进程

    async def stop(self, name, timeout=10.0) -> ProcessInfo:
        # shutdown_flag 协议（FR-A11）：
        # 1. 写 runtime/{name}.shutdown_flag
        # 2. 轮询子进程最多 timeout 秒，等待其检测 flag 后自行退出
        # 3. 仍存活 → proc.terminate()，再超时 → proc.kill()
        # 4. 删除 pid 文件与 shutdown_flag

    async def restart(self, name) -> ProcessInfo:
        # stop() → start() → process_state.json 中 restart_count += 1

    def status(self, name) -> ProcessInfo:
        # 读 pid 文件 → psutil create_time 校验：
        #   无 pid 文件        → stopped
        #   pid 文件存在但进程消失 → crashed
        #   存活               → running（实时 memory/cpu/uptime）
        # 所有分支 restart_count 取自 process_state.json
```

**CPU 采样**：当前 `status()` 直接调用 `psutil.Process.cpu_percent(interval=None)`（读自上次调用以来的增量），首次或久未调用会返回 0.0。FR-A09 要求的「后台线程每 2 秒缓存」尚未实现，列为 **A006-fix**。

**子进程退出监控**：`health_check_loop` 后台协程每 5 秒比对各模块 `status()`，由 running 变为 crashed 时记录 error 日志（依据 pid 文件 + create_time 判定，不依赖内存句柄）。

**子进程侧约束（FR-A11）**：collector / writer 必须在主循环监听 `runtime/{name}.shutdown_flag`，检测到后优雅退出。未实现监听的子进程只能被 terminate/kill 强杀。

---

## 6. 日志管理设计

采用 **Method B（日志文件 tail）**：collector / writer 进程使用 `RotatingFileHandler` 自写日志文件，Agent 后台线程监控文件并推送新行。

### 数据流

```
collector 日志文件（由 collector / writer 自身写入，RotatingFileHandler）
  {working_dir}/logs/collector/current.log
  {working_dir}/logs/writer/current.log
          │
          ▼
[File Tail Thread：open → seek(0, 2) → 循环 readline]
          │
          ├──→ LogBuffer（deque maxlen=100，内存环形缓冲）
          │
          └──→ WS 广播（asyncio.Queue → 每个订阅者协程）
```

**约定**：
- 日志文件路径 = `{working_dir}/logs/{name}/current.log`（相对各自 `working_dir`）
- Agent 不另写 RotatingFileHandler；文件轮转由子进程负责
- 子进程未启动时 tail 线程不存在（attach 在 start() 后调用）
- 子进程退出后 tail 线程自然结束（读到 EOF 后 sleep 轮询，检测到 status != "running" 时退出）

### WS 订阅流程

```
前端建立连接
    │
    ├─ 启动 10 秒 auth 超时计时器
    │
    ├─ 收到 { "type": "auth", "token": "..." }
    │       ├─ JWT 验证失败 → close(4001)
    │       └─ 验证成功 → 加入 WsSubscriberSet
    │               │
    │               ├─ 推送 LogBuffer 历史 tail（最近 100 行）
    │               └─ 持续推送新行（由 Reader Thread 触发）
    │
    └─ 10 秒内未收到 auth 帧 → close(4001)
```

### 日志行格式

```json
{
  "timestamp": "2026-05-27T14:26:10.123+08:00",
  "level": "INFO",
  "source": "collector",
  "message": "Redis stream append rt:stream:agv:main:0 count=256"
}
```

**level 解析规则**：尝试从行首解析 `DEBUG|INFO|WARN|WARNING|ERROR|CRITICAL`，匹配则用对应枚举，`WARNING` → `WARN`，`CRITICAL` → `ERROR`；无法解析则 fallback `INFO`。

---

## 7. 安全设计

### JWT 密钥管理
- 首次启动时生成：`secrets.token_hex(32)`，写入 `config.toml[agent].jwt_secret`。
- 密钥不轮换（除非手动清空 config）；重新生成会使所有现有 token 立即失效。
- **不**在任何 log 中打印 `jwt_secret` 值。

### 密码加密（DPAPI）
```python
# security.py
import base64
import win32crypt  # pywin32

def dpapi_encrypt(plaintext: str) -> str:
    """返回 DPAPI 加密后的 base64 字符串"""
    data = plaintext.encode("utf-8")
    encrypted = win32crypt.CryptProtectData(data, None, None, None, None, 0)
    return base64.b64encode(encrypted).decode()

def dpapi_decrypt(ciphertext_b64: str) -> str:
    """解密 DPAPI base64 字符串，返回明文"""
    if not ciphertext_b64:
        return ""
    data = base64.b64decode(ciphertext_b64)
    # CryptUnprotectData 返回 (description: str, plaintext: bytes)
    _description, plaintext_bytes = win32crypt.CryptUnprotectData(
        data, None, None, None, 0
    )
    return plaintext_bytes.decode("utf-8")

# ─── 必须通过往返测试才能投入使用 ─────────────────────────────────────────
# python -c "
# from app.core.security import dpapi_encrypt, dpapi_decrypt
# orig = 'test-password-123'
# assert dpapi_decrypt(dpapi_encrypt(orig)) == orig, 'DPAPI round-trip FAILED'
# print('DPAPI round-trip OK')
# "
```

**已知限制**：DPAPI 密文与 Windows 用户账户 + 机器绑定。迁移到新机器时，必须在原机解密后重新在新机加密，或手动填写明文密码触发重新加密。

### 日志脱敏
- Python `logging` 使用自定义 Filter，替换 `password` / `password_enc` 字段值为 `[FILTERED]`。
- 不 log JWT token 值（只 log "JWT verified" / "JWT invalid" 等状态）。
- 错误响应中不返回原始 SQL 错误（捕获后替换为 `"Connection failed"` 或 `"Query failed"`）。

---

## 8. 运行时设计

### 虚拟环境（uv 创建，⚠️ 重要）

`agent/.venv` 由 **uv** 创建，**默认不带 pip**，直接 `python -m pip ...` 会报 `No module named pip`。

- 解释器路径：`agent\.venv\Scripts\python.exe`。开发命令请显式用此路径，或先激活：`.\.venv\Scripts\Activate.ps1`。
- 装/管依赖（推荐，在 `agent\` 目录下）：
  ```powershell
  uv pip install -r requirements.txt
  uv pip install <package>
  ```
- 若必须用 pip：先引导一次 `& .\.venv\Scripts\python.exe -m ensurepip --upgrade`，之后才能 `python -m pip install ...`。
- 测试依赖：`httpx`（FastAPI `TestClient` 必需，**仅测试用**，运行时不需要，未列入 `requirements.txt`；本机已安装）。
- ⚠️ **CPU 指标陷阱**：venv 的 `python.exe`（uv 创建）是 **shim/trampoline**——它本身只等待，真正运行的是它派生的孙进程。若把 `collector/writer` 的 `executable` 指向该 venv `python.exe`（或 PyInstaller onefile 引导器），ProcessManager 跟踪到的是空转的父 pid，CPU 恒显示 ~0（内存同理）。生产请指向真实 worker：真解释器（如 `sys._base_executable`）、onedir 打包，或独立 worker exe。详见 spec FR-A09。

### 开发期启动

```powershell
cd agent
.\.venv\Scripts\python.exe main.py
# 或：
.\.venv\Scripts\uvicorn.exe app.factory:create_app --factory --host 127.0.0.1 --port 7070 --reload
```

### 生产期（Windows Service）

使用 `pywin32` + `servicemanager` 包装 uvicorn，或 `NSSM`（Non-Sucking Service Manager）注册。详见 `service/install_service.py`（Phase 7 实现）。

### 优雅停止

- SIGTERM / Service Stop → lifespan shutdown 输出 `Agent shutting down...`，在 25 秒总超时内按 collector → writer 顺序调用 `ProcessManager.stop()`；`stop()` 内部先写 shutdown_flag，10 秒内等待子进程自行退出，超时后 `terminate()` / `kill()`。
- 子进程停止流程结束后 detach collector/writer 日志 tail，并取消 `health_check_loop`；不因单个子进程停止失败而阻塞 Agent 自身退出。

---

## 9. 密码 Partial Update 实现（与前端 §4.6 一致）

```python
# core/config.py

def apply_db_patch(current: DbSection, patch: DbConfigIn) -> DbSection:
    """
    C3: patch 无 password key → 保留 current.password_enc
    C4: patch 有 password key（非空）→ DPAPI 重加密后更新
    C5: patch password = "" → 由 Pydantic validator 在入口处拒绝（400）
    """
    updated = current.copy()
    if patch.host is not None:       updated.host = patch.host
    if patch.port is not None:       updated.port = patch.port
    if patch.user is not None:       updated.user = patch.user
    if patch.database is not None:   updated.database = patch.database
    # password 仅在 patch 中显式提供时才更新
    if patch.password is not None:
        updated.password_enc = dpapi_encrypt(patch.password)
    # patch.password 为 None（未传）→ 保留 current.password_enc，不处理
    return updated
```

---

## 10. 依赖库

```
# HTTP 框架
fastapi>=0.111
uvicorn[standard]>=0.29

pydantic>=2.7
python-jose[cryptography]>=3.3     # JWT HS256（Agent 自签 token）
# 注：Backend 使用自实现 PBKDF2-SHA256（无 passlib），Agent verify_password 直接复用
# backend/app/core/security.py 中的 verify_password，无需额外依赖
pymysql>=1.1                       # MySQL 连接（auth + health check）
redis>=5.0                         # Redis health check
pywin32>=306                       # DPAPI（Windows Only）
psutil>=5.9                        # 进程指标
toml>=0.10                         # config.toml 读写
```

> **注**：`taosws`（TDengine WebSocket 驱动）需单独安装，版本与 TDengine Server 匹配。

---

## 11. Phase 计划

| Phase | 内容 | 前提 |
|-------|------|------|
| Phase 0 | 项目骨架 + 配置加载 + JWT 基础 | 无 |
| Phase 1 | Bootstrap API（A003）| Phase 0 |
| Phase 2 | Auth API（A004–A005）| Phase 0 |
| Phase 3 | ProcessManager + 进程控制 API（A006–A008）| Phase 0 |
| Phase 4 | Config API（A009）| Phase 0 + Phase 3 |
| Phase 5 | HealthCheck API（A010）| Phase 0 |
| Phase 6 | LogManager + WebSocket 日志（A011–A012）| Phase 3 |
| Phase 7 | 运行时加固（A013–A014）| Phase 0–6 全部完成 |
