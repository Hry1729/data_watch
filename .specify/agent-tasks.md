# Tasks: DataWatch Agent

**Spec**: `.specify/agent-spec.md` | **Plan**: `.specify/agent-plan.md`
**Branch**: `001-data-watch-console`

---

## 架构边界（每任务实现前必查）

| 禁止 | 正确做法 |
|------|---------|
| ❌ Agent 内嵌 Tauri / 被 Tauri 启动 | ✅ Agent 是独立进程，单独命令行或 Windows Service 启动 |
| ❌ 密码明文写入 config.toml | ✅ `password_enc` 字段存 DPAPI 加密后的 base64 |
| ❌ 日志或响应中打印密码 / JWT token 值 | ✅ 日志脱敏 Filter；响应仅返回 success/message |
| ❌ GET /admin/config 返回任何 password 字段 | ✅ `ConnectionConfigOut` 模型中不含 password |
| ❌ `PUT /admin/config { password: "" }` 被接受 | ✅ Pydantic validator 在入口处返回 400 |
| ❌ 启动子进程不设置 cwd | ✅ 每次 start 必须设置 `cwd = working_dir` |
| ❌ WS 推送的日志行缺字段 | ✅ 每行必须包含全部四字段：`timestamp`、`level`、`source`、`message`（FR-A17）|
| ❌ WS 不验证 auth 帧就推送日志 | ✅ 收到 auth 帧 → JWT 验证通过 → 才加入订阅者集合 |

---

## 任务约定

- `[P]` = 可与同 Phase 其他 [P] 任务并行（不同文件）
- 每任务格式：**文件 → 动作 → 验收**
- 验收命令使用 PowerShell（Windows 环境）
- Agent 工作目录：`D:\python\device_alarm\data_watch\agent\`（待确认，以实际路径为准）

---

## Phase 0 — 骨架与基础

**目标**: FastAPI 应用可启动；config.toml 可加载；JWT 工具函数可用；所有后续任务的基础设施就绪。
**Gate**: Phase 0 全部通过后才进入其他 Phase。A001 与 A002 可并行。

---

### A001 — 项目骨架与依赖 [P]

**文件**:
- 创建 `agent/requirements.txt`
- 创建 `agent/main.py`
- 创建 `agent/app/factory.py`
- 创建 `agent/app/core/exceptions.py`
- 创建 `agent/config.toml.example`

**动作**:

1. 创建 `requirements.txt`（固定版本，不用范围）：
   ```
   fastapi==0.111.0
   uvicorn[standard]==0.29.0
   pydantic==2.7.1
   python-jose[cryptography]==3.3.0
   # 注：密码验证使用 hashlib.pbkdf2_hmac（标准库），无需 passlib
   pymysql==1.1.1
   redis==5.0.4
   pywin32==306
   psutil==5.9.8
   toml==0.10.2
   ```

2. 创建 `main.py`：
   ```python
   import uvicorn
   from app.factory import create_app

   app = create_app()

   if __name__ == "__main__":
       uvicorn.run(
           "main:app",
           host="127.0.0.1",
           port=7070,
           log_level="info",
       )
   ```

3. 创建 `app/factory.py`：
   ```python
   from fastapi import FastAPI
   from contextlib import asynccontextmanager

   @asynccontextmanager
   async def lifespan(app: FastAPI):
       # Phase 3: 初始化 ProcessManager
       # Phase 6: 初始化 LogManager
       yield
       # Phase 7: 优雅停止子进程

   def create_app() -> FastAPI:
       app = FastAPI(title="DataWatch Agent", version="0.1.0", lifespan=lifespan)
       # 注册 router（各 Phase 补充）
       return app
   ```

4. 创建 `app/core/exceptions.py`（统一错误格式）：
   ```python
   from fastapi import Request
   from fastapi.responses import JSONResponse

   async def validation_exception_handler(request: Request, exc):
       return JSONResponse(status_code=422, content={"detail": str(exc)})
   ```

5. 创建 `config.toml.example`（模板，注释说明所有字段）。

**验收**:
- [ ] `cd agent && pip install -r requirements.txt` 无错误
- [ ] `python main.py` 启动，控制台输出 `Uvicorn running on http://127.0.0.1:7070`
- [ ] `Invoke-RestMethod http://127.0.0.1:7070/docs` 返回 OpenAPI 文档 HTML（FastAPI 自带）

---

### A002 — 配置加载 + JWT 工具 [P]

**文件**:
- 创建 `agent/app/core/config.py`
- 创建 `agent/app/core/security.py`
- 创建 `agent/app/core/deps.py`
- 创建 `agent/config.toml`（不提交，加入 .gitignore）

**动作**:

1. **`config.py`** — AgentConfig 加载/保存：
   ```python
   import toml, secrets
   from pathlib import Path

   CONFIG_PATH = Path(__file__).parent.parent.parent / "config.toml"

   def load_config() -> dict:
       if not CONFIG_PATH.exists():
           cfg = _default_config()
           save_config(cfg)
           return cfg
       return toml.load(CONFIG_PATH)

   def save_config(cfg: dict) -> None:
       with open(CONFIG_PATH, "w") as f:
           toml.dump(cfg, f)

   def _default_config() -> dict:
       return {
           "agent": {
               "host": "127.0.0.1",
               "port": 7070,
               "jwt_secret": secrets.token_hex(32),  # 首次生成
               "jwt_expire_hours": 8,
               "bootstrap_done": False,
               "log_level": "INFO",
           },
           "redis": {"host": "127.0.0.1", "port": 6379, "password_enc": ""},
           "mysql": {"host": "127.0.0.1", "port": 3306, "user": "root",
                     "database": "device_watch", "password_enc": ""},
           "tdengine": {"host": "127.0.0.1", "port": 6041, "user": "root",
                        "database": "device_data", "password_enc": ""},
           "collector": {"executable": "", "working_dir": ""},
           "writer": {"executable": "", "working_dir": ""},
       }
   ```

2. **`security.py`** — JWT + DPAPI：
   ```python
   import base64
   from jose import jwt, JWTError
   from datetime import datetime, timedelta, timezone

   # DPAPI（Windows 专用）
   def dpapi_encrypt(plaintext: str) -> str:
       import win32crypt
       encrypted = win32crypt.CryptProtectData(plaintext.encode(), None, None, None, None, 0)
       return base64.b64encode(encrypted).decode()

   def dpapi_decrypt(ciphertext_b64: str) -> str:
       if not ciphertext_b64:
           return ""
       import win32crypt
       data = base64.b64decode(ciphertext_b64)
       # CryptUnprotectData 返回 (description: str, plaintext: bytes)
       _description, plaintext_bytes = win32crypt.CryptUnprotectData(data, None, None, None, 0)
       return plaintext_bytes.decode("utf-8")

   # JWT
   def create_access_token(subject: str, secret: str, expire_hours: int) -> str:
       expire = datetime.now(timezone.utc) + timedelta(hours=expire_hours)
       return jwt.encode({"sub": subject, "exp": expire}, secret, algorithm="HS256")

   def verify_token(token: str, secret: str) -> str:
       """返回 subject；失败抛出 JWTError"""
       payload = jwt.decode(token, secret, algorithms=["HS256"])
       return payload["sub"]
   ```

3. **`deps.py`** — FastAPI Depends：
   ```python
   from fastapi import Depends, HTTPException, Header
   from .config import load_config
   from .security import verify_token
   from jose import JWTError

   def get_current_user(authorization: str = Header(...)) -> str:
       if not authorization.startswith("Bearer "):
           raise HTTPException(401, "Invalid token")
       token = authorization.removeprefix("Bearer ")
       cfg = load_config()
       try:
           return verify_token(token, cfg["agent"]["jwt_secret"])
       except JWTError:
           raise HTTPException(401, "Token invalid or expired")
   ```

**验收**:
- [ ] `python -c "from app.core.config import load_config; print(load_config()['agent']['bootstrap_done'])"` 输出 `False`
- [ ] 运行后 `config.toml` 存在，`agent.jwt_secret` 为非空 hex 字符串
- [ ] `python -c "from app.core.security import create_access_token, verify_token; t=create_access_token('admin','testsecret',8); print(verify_token(t,'testsecret'))"` 输出 `admin`

---

## Phase 1 — Bootstrap API

**前提**: Phase 0 全部通过。

---

### A003 — Bootstrap Router

**文件**:
- 创建 `agent/app/routers/bootstrap.py`
- 修改 `agent/app/factory.py`（注册 router）

**动作**:

1. 实现四个端点，遵守 spec US-A1 全部验收项：
   - `GET /bootstrap/status` → 无鉴权，返回 `BootstrapStatusResponse`
   - `PUT /bootstrap/config` → `bootstrap_done=false` 时允许，完成后 403；写入 mysql 配置（DPAPI 加密密码）
   - `POST /bootstrap/test-connection { "target": "mysql" }` → 请求体只含 target 字段，使用已写入 config.toml 的 MySQL 配置测试连接，不接收 host/port 等连接参数，不写 config；`bootstrap_done=true` 后 403
   - `POST /bootstrap/finish` → 设置 `bootstrap_done=true` 写入 config；`bootstrap_done=true` 后 403

2. 在 `factory.py` 注册：
   ```python
   from app.routers import bootstrap
   app.include_router(bootstrap.router)
   ```

**验收**:
- [ ] `Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status` 返回 `{ "in_bootstrap": true }`
- [ ] `Invoke-RestMethod -Method PUT http://127.0.0.1:7070/bootstrap/config -Body '{"host":"127.0.0.1","port":3306,"user":"root","password":"123456","database":"device_watch"}' -ContentType 'application/json'` 返回 200
- [ ] `config.toml` 中 `mysql.password_enc` 非空，且不是 `"123456"` 明文
- [ ] `Invoke-RestMethod -Method POST http://127.0.0.1:7070/bootstrap/finish` 返回 200
- [ ] 再次 `Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status` 返回 `{ "in_bootstrap": false }`
- [ ] 再次 `Invoke-RestMethod -Method PUT http://127.0.0.1:7070/bootstrap/config ...` 返回 403

---

## Phase 2 — Auth API

**前提**: Phase 0 通过，MySQL 用户表存在且有测试账号。
> **Auth 表已确认**：表 `sys_user`，用户名列 `user_name`，密码列 `password_hash`（PBKDF2-SHA256，与 `backend/app/core/security.py` 一致），`status = "1"` + `deleted = false` + 至少一个 `sys_role.enabled = true` 的已分配角色。

---

### A004 — Auth Router

**文件**:
- 创建 `agent/app/routers/auth.py`
- 修改 `agent/app/factory.py`（注册 router）

**动作**:

1. `POST /auth/login`：
   - 实现逻辑（已确认）：
     ```python
     # 表 sys_user，用户名列 user_name，密码列 password_hash，状态列 status
     # 1. SELECT * FROM sys_user WHERE user_name = %s LIMIT 1
     # 2. 检查 user is not None and not user.deleted → 否则 401
     # 3. 检查 user.status == "1" → 否则 403（账号禁用）
     # 4. verify_password(request.password, user.password_hash) → 否则 401
     #    算法：PBKDF2-SHA256，格式 pbkdf2_sha256$600000$<salt_hex>$<digest_hex>
     #    复用 backend/app/core/security.py 的 verify_password 实现（纯 hashlib）
     # 5. 查 sys_user_role JOIN sys_role WHERE user_id=? AND sys_role.enabled=true → 无角色则 403
     ```
   - 成功返回 `{ "token": "<JWT>" }`；失败返回 401
   - 不在 log 中打印密码或 token 值

2. `GET /viewer/status`（放在此处或 viewer router 均可）：
   - 无需 JWT，返回 `{ "agent": "running" }`

**验收**:
- [ ] `Invoke-RestMethod -Method POST http://127.0.0.1:7070/auth/login -Body '{"username":"admin","password":"correctpass"}' -ContentType 'application/json'` 返回含 `token` 字段的响应
- [ ] 错误密码返回 401
- [ ] `Invoke-RestMethod http://127.0.0.1:7070/viewer/status` 返回 `{ "agent": "running" }`
- [ ] Agent 日志文件中无 token 或 password 明文

---

## Phase 3 — 进程管理

**前提**: Phase 0 通过。

---

### A005 — ProcessManager 服务 [P]（✅ 已完成，基线：PID 文件 + shutdown_flag）

**文件**:
- 创建 `agent/app/services/process_manager.py`

**基线说明**：不在内存持有 `Popen` 句柄；进程信息落盘到 `runtime/`，Agent 重启后仍可追踪子进程并判定 `crashed`。`ProcessManager(runtime_dir, modules)` 构造，由 `factory.py` lifespan 初始化为 `app.state.pm`。

**动作**:

1. 实现 `ModuleSpec` / `ProcessInfo` dataclass 与 `ProcessManager`：
   - `start(name)` → `subprocess.Popen(cwd=working_dir, creationflags=CREATE_NO_WINDOW)`（Method B：日志由子进程自写文件，不捕获 stdout/stderr）；写 `runtime/pids/{name}.pid`（pid + psutil create_time + started_at）；已存活时幂等返回 running；executable / working_dir 为空抛 `ProcessManagerError`
   - `stop(name)` → **shutdown_flag 协议**：① 写 `runtime/{name}.shutdown_flag` → ② 轮询子进程最多 10s 等待其自行退出 → ③ 仍存活 `terminate()`，再超时 `kill()` → ④ 删除 pid 文件与 flag
   - `restart(name)` → `stop()` + `start()`，并在 `runtime/state/process_state.json` 中 `restart_count += 1`
   - `status(name)` → 读 pid 文件 + psutil create_time 校验：无文件 → stopped，进程消失 → crashed，存活 → running（实时 memory/cpu/uptime）；各分支 `restart_count` 取自 `process_state.json`
   - `health_check_loop()` → 每 5s 比对 status，running→crashed 记 error 日志

2. 在 `factory.py` lifespan 中初始化 `app.state.pm` 并启动 `health_check_loop`。

**子进程侧约束（FR-A11，待 collector/writer 实现）**：collector / writer 必须在主循环监听 `runtime/{name}.shutdown_flag`，检测到后优雅退出（flush 缓冲、关闭连接）并自行结束；未实现前 stop 只能走 terminate/kill 强杀。

**验收**（单元层面，无需 HTTP）:
- [x] DPAPI 往返测试通过：`python -c "from app.core.security import dpapi_encrypt, dpapi_decrypt; assert dpapi_decrypt(dpapi_encrypt('test-123')) == 'test-123'; print('OK')"`
- [x] 以 dummy 可执行体（如 python 长驻脚本）构造 `ProcessManager(runtime_dir, modules)`，`await pm.start('collector')` 后 `pm.status('collector').status == "running"`，pid 文件存在
- [x] `await pm.stop('collector')` 后 `pm.status('collector').status == "stopped"`，pid 文件被删除
- [ ] 在真实 collector.exe 上验收 shutdown_flag 优雅退出（< 10s 自行退出）与超时强杀（不监听 flag 时 > 10s 触发 terminate/kill）两条路径 ← **依赖 collector/writer 实现 flag 监听后才能验收**

---

### A006 — Viewer 进程路由 [P]

**文件**:
- 创建 `agent/app/routers/viewer.py`
- 修改 `agent/app/factory.py`（注册 router）

**动作**:

实现两个端点，需要 JWT（`Depends(get_current_user)`）：
- `GET /viewer/processes/{name}/metrics` → `process_manager.get_metrics(name)` → `ModuleStatusResponse`
- `GET /viewer/processes/{name}/status` → `ProcessStatusResponse`

name 校验：`Literal["collector", "writer"]`，不匹配时 FastAPI 自动返回 422。

**验收**:
- [ ] 无 JWT 时返回 401
- [ ] 有效 JWT 时 `GET /viewer/processes/collector/metrics` 返回含 `status/pid/uptime_seconds/memory_mb/cpu_percent/started_at/restart_count` 的响应
- [ ] `GET /viewer/processes/invalid/metrics` 返回 422

---

### A006-fix — Viewer 修正（CPU 后台采样 + 未知模块名 422）

**前提**: A005 + A006 完成。**独立于 A007**，可后续单独处理。

**背景**: 当前实现两处偏离基线，集中在此修正：
1. `viewer.py` 对未知模块名返回 **404**，spec US-A3#6 / plan §4.2 要求 **422**。
2. CPU 采样直接 `cpu_percent(interval=None)`，未实现 FR-A09 要求的「后台线程每 2 秒缓存」，首次/久未调用返回 0.0。

**文件**:
- 修改 `agent/app/routers/viewer.py`（404 → 422）
- 修改 `agent/app/services/process_manager.py`（CPU 后台采样缓存）

**动作**:
1. `viewer.py` 两个端点未知 name 改为 `HTTPException(422)`（或路径用 `Literal` 触发 422）。
2. `ProcessManager` 增后台采样：进程运行期间每 2s `cpu_percent(interval=None)` 写入 `_cpu_cache`，`status()` 读缓存。

**验收**（已通过）:
- [x] `GET /viewer/processes/invalid/metrics` 与 `/status` 均返回 **422**（非 404）
- [x] 起 CPU 烧负载进程，监控循环（2s）跑数秒后 `status().cpu_percent` 反映真实占用（实测 97.8），非恒为 0.0

**验证中发现（已写入 spec FR-A09 / plan §8）**：uv 创建的 venv `python.exe` 是 shim，会派生孙进程，导致被跟踪父 pid 的 CPU 恒为 0。生产须让 ProcessManager 跟踪真实 worker 进程，否则 CPU/内存指标失真。

---

### A007 — Admin 进程路由 [P]

**文件**:
- 创建 `agent/app/routers/admin.py`（后续 Phase 续写 config/health 部分）
- 修改 `agent/app/factory.py`（注册 router）

**动作**:

实现三个端点，需要 JWT。**路由层只调用 `app.state.pm` 的方法，不重复编写进程逻辑**：
- `POST /admin/processes/{name}/start` → `pm.start(name)`
- `POST /admin/processes/{name}/stop` → `pm.stop(name)`
- `POST /admin/processes/{name}/restart` → `pm.restart(name)`（成功后 `restart_count` 经 `process_state.json` +1）

错误处理：
- 未知模块名（非 collector/writer）→ **422**
- executable / working_dir 为空（ProcessManagerError）→ 400
- 已在运行时 start → 409（路由用 `pm.status(name)` 守卫）

**验收**（已通过：FastAPI `TestClient` 注入 dummy 配置 PM + ProcessManager 单测）:
- [x] start 返回 200，随后 GET /metrics 返回 status="running"
- [x] 再次 start 返回 409
- [x] `POST /stop` 后 GET /metrics 返回 status="stopped"
- [x] `POST /restart` 返回 200，restart_count 加 1（持久化于 `process_state.json`）
- [x] 无 JWT → 401；未知模块名 → 422；executable/working_dir 缺失 → 400

**审查遗留（已知限制，本次未处理）**：
- `restart()` 非整体原子（stop 释放锁后再 start 取锁），`start` 的 409 依赖路由层 `status()` 守卫——严格并发下存在 TOCTOU。Agent 按 spec 仅服务单客户端（见 Exclusions「远程多客户端」），串行请求下无影响；如需强一致需将 start/stop 拆为无锁内核并让 restart 整体持锁，另行立项。

---

## Phase 4 — Config API

**前提**: Phase 0 通过。

---

### A008 — Admin Config Router

**文件**:
- 修改 `agent/app/routers/admin.py`（追加 config 端点）

**动作**:

1. `GET /admin/config`：
   - 加载 config.toml
   - 构建 `ConnectionConfigOut`（所有 `password_enc` 字段**不出现**在响应中）
   - 需要 JWT

2. `PUT /admin/config`：
   - 接收 `ConnectionConfigIn`（Pydantic 模型，password 空串时 validator 返回 400）
   - 调用 `apply_db_patch()` 对各 section 做 partial update
   - 调用 `save_config()` 写入 config.toml
   - 需要 JWT

**验收**（已通过：FastAPI `TestClient` + 临时 CONFIG_PATH）:
- [x] `GET /admin/config` 响应中不含任何 `password` / `password_enc` 字段
- [x] `PUT { "redis": { "host": "10.0.0.5" } }` 后 redis.host 更新，其余不变
- [x] `PUT { "mysql": { "password": "newpass" } }` 后 password_enc 更新（非明文，可 dpapi_decrypt 回 newpass），其余 mysql 字段不变
- [x] `PUT { "mysql": { "password": "" } }` 返回 400，消息含"不允许为空串"（C5）
- [x] `PUT { "mysql": {} }`（无 password key）后 password_enc 不变（C3）

**实现说明**：C5 空串改在端点返回 400（移除了 `DbConfigIn.model_post_init` 的 422 拒绝）；partial 用 `model_dump(exclude_unset=True)` 区分"未提供"与"提供"；`password: null` 视为不更新（清空密码不经此接口）。

**加固完成**：`update_config` 已改为 `mutate_config()`，在 `threading.RLock` 内完成 load→mutate→save；`save_config()` 使用同目录临时文件 + `os.replace()` 原子替换，避免并发 PUT 丢更新和半写 TOML。

---

## Phase 5 — 健康检查 API

**前提**: Phase 0 + Phase 4 通过。

---

### A009 — Health Check Router

**文件**:
- 创建 `agent/app/services/health_checker.py`
- 修改 `agent/app/routers/admin.py`（追加 health 端点）

**动作**:

1. `health_checker.py`：
   ```python
   async def check_redis(cfg: dict) -> tuple[bool, str | None]:
       import redis as Redis
       try:
           password = dpapi_decrypt(cfg["redis"]["password_enc"]) or None
           r = Redis.Redis(host=cfg["redis"]["host"], port=cfg["redis"]["port"],
                          password=password, socket_connect_timeout=3)
           r.ping()
           return True, None
       except Exception as e:
           return False, "Connection failed"  # 不暴露 e 详情

   async def check_mysql(cfg: dict) -> tuple[bool, str | None]:
       # pymysql 连接测试

   async def check_tdengine(cfg: dict) -> tuple[bool, str | None]:
       # taosws 或 http connector 测试
   ```

2. `POST /admin/health/check { "target": "redis"|"mysql"|"tdengine" }`：
   - Bootstrap 未完成时返回 400
   - 调用对应 check 函数
   - 返回 `{ "success": bool, "message": str | null }`
   - 需要 JWT

**验收**（已通过：TestClient + 临时 config + 本地 stub）:
- [x] 无 JWT → 401
- [x] `bootstrap_done=false` 调用 → 400（"Bootstrap must be completed first"）
- [x] `target` 非 redis/mysql/tdengine → 422
- [x] redis/mysql 指向死端口 → `{ success:false, message:"...health check failed" }`，message 不含密码
- [x] tdengine 指向返回 `{"code":0}` 的 stub → `{ success:true }`；200 但 `code!=0`/非 JSON → `{ success:false }`
- [x] 预埋密码明文未出现在任何响应文本中

**后续修订（2026-06-01）**:
- [x] `GET /viewer/health` 已实现为聚合健康快照，复用 `health_checker.py` 的 Redis/MySQL/TDengine 检测逻辑。
- [x] 响应字段：`redis/mysql/tdengine.status` ∈ `connected | checking | disconnected | unknown`，并可返回 `latencyMs/message/checkedAt`。
- [x] 前端顶部状态栏每 10 秒轮询该接口；窗口隐藏时暂停，恢复可见时立即刷新。

**实现说明**：检查走当前 `config.toml`，`password_enc` 经 DPAPI 解密；阻塞型检查（redis/pymysql/urllib，超时 3s）由端点经 `asyncio.to_thread` 投入线程池，不阻塞事件循环。TDengine 无 Python driver，改用**标准库 urllib** 调 REST `POST /rest/sql`（Basic Auth），并解析 body 判 `code==0 || status=="succ"`（仅 HTTP 200 会误报）。失败 message 为静态通用串，绝不含密码/连接串。

---

## Phase 6 — WebSocket 日志

**前提**: Phase 3（ProcessManager）通过。

---

### A010 — LogManager 服务 [P]

**文件**:
- 创建 `agent/app/services/log_manager.py`

**动作**:

1. `LogManager` 单例（Method B：日志文件 tail）：
   ```python
   import asyncio, threading, time
   from collections import deque
   from pathlib import Path

   class LogManager:
       _buffers: dict[str, deque]          # name → deque(maxlen=100)
       _subscribers: dict[str, set]        # name → set of asyncio.Queue

       def attach(self, name: str, working_dir: str) -> None:
           """进程启动后调用；监控 {working_dir}/logs/{name}/current.log"""
           log_path = Path(working_dir) / "logs" / name / "current.log"
           thread = threading.Thread(
               target=self._tail_loop,
               args=(name, log_path),
               daemon=True,
           )
           thread.start()

       def _tail_loop(self, name: str, log_path: Path) -> None:
           """后台线程：seek to end，循环 readline；子进程退出后自然结束"""
           # 等待文件出现（子进程可能稍后才创建文件）
           for _ in range(30):
               if log_path.exists():
                   break
               time.sleep(0.5)

           if not log_path.exists():
               return  # 文件始终未出现，退出

           with open(log_path, "r", encoding="utf-8", errors="replace") as f:
               f.seek(0, 2)             # seek to end，不推送历史（历史由文件已有内容决定）
               while True:
                   line = f.readline()
                   if line:
                       log_entry = self._parse_line(name, line.rstrip())
                       self._buffers[name].append(log_entry)
                       self._broadcast(name, log_entry)
                   else:
                       # 检查进程是否已停止（process_manager 状态）
                       from app.services.process_manager import process_manager
                       if process_manager.get_status(name) != "running":
                           break
                       time.sleep(0.1)   # 等待新行

       def get_history(self, name: str) -> list:
           return list(self._buffers[name])

       def subscribe(self, name: str) -> asyncio.Queue:
           q = asyncio.Queue()
           self._subscribers[name].add(q)
           return q

       def unsubscribe(self, name: str, q: asyncio.Queue) -> None:
           self._subscribers[name].discard(q)
   ```

2. `_parse_line`：尝试从行首解析 `DEBUG|INFO|WARN|WARNING|ERROR|CRITICAL`；`WARNING` → `WARN`，`CRITICAL` → `ERROR`；无法解析则 fallback `"INFO"`。

**验收**（已通过：async 单元 + 真实文件 tail）:
- [x] 写入 `{working_dir}/logs/collector/current.log` 后 `get_history("collector")` 非空
- [x] 行内正则解析等级：collector 格式 `[ERROR]`→ERROR、writer 格式 `WARNING`→WARN、无等级→INFO
- [x] 订阅者经 `asyncio.Queue`（tail 线程 `loop.call_soon_threadsafe` 投递）收到全部新行
- [x] 连写 150 行后 `len(get_history)` == 100（maxlen 生效）
- [x] `detach` 后该 name 状态移除、tail 线程退出；非法 name → ValueError

**集成约定（供 A011 / 进程管理，本次未实现）**：
- `attach(name, working_dir, loop)` 在 `ProcessManager.start` 成功后调用（loop 用 `asyncio.get_running_loop()`）；`detach(name)` 在 stop 后调用。
- WS 端点：先 `subscribe` 再推 `get_history`，`finally` 里 **只 `unsubscribe`，绝不 `detach`**（detach 属进程生命周期）。
- `detach` 含 `thread.join(timeout=1.0)`，async 路径调用须 `await asyncio.to_thread(log_manager.detach, name)`，避免阻塞事件循环。

**已知限制**：seek-to-end 只推新行、不回灌启动早期日志（符合 Method B）；高吞吐轮转（rename 后新文件快速增长 / 0.1s 窗口内截断重写）可能漏行，如需更强可加 `(st_dev, st_ino)` 文件身份检查。

---

### A011 — WebSocket Log Router

**文件**:
- 修改 `agent/app/routers/viewer.py`（追加 WS 端点）

**动作**:

```python
from fastapi import WebSocket, WebSocketDisconnect

@router.websocket("/viewer/logs/{name}/tail")
async def ws_log_tail(websocket: WebSocket, name: str):
    if name not in ("collector", "writer"):
        await websocket.close(code=1008)
        return

    await websocket.accept()

    # 10 秒 auth 超时
    try:
        auth_frame = await asyncio.wait_for(websocket.receive_json(), timeout=10.0)
    except asyncio.TimeoutError:
        await websocket.close(code=4001)
        return

    token = auth_frame.get("token", "")
    try:
        cfg = load_config()
        verify_token(token, cfg["agent"]["jwt_secret"])
    except Exception:
        await websocket.close(code=4001)
        return

    # 推送历史 tail
    for entry in log_manager.get_history(name):
        await websocket.send_text(entry.model_dump_json())

    # 实时推送
    q = log_manager.subscribe(name)
    try:
        while True:
            entry = await q.get()
            await websocket.send_text(entry.model_dump_json())
    except WebSocketDisconnect:
        log_manager.unsubscribe(name, q)
```

**验收**（已通过：TestClient + 捕获 app loop 做真实跨循环推送）:
- [x] 有效 token 鉴权后收到历史日志行（JSON）
- [x] test 线程 `call_soon_threadsafe` 投递一条 → WS 实时收到该行（真实跨事件循环）
- [x] 无效 token → close 4001；缺 token 字段 → 4001
- [x] 不发 auth 帧、超时 → 4001（验收用 `WS_AUTH_TIMEOUT_SECONDS` 缩短）
- [x] name 非法 → close 1008（accept 前关闭）
- [x] 客户端断开后 `_subscribers` 清空（finally 里 unsubscribe）
- [~] 多客户端隔离：按设计成立（每连接独立 asyncio.Queue，广播逐队列容错；A010 已验证 fanout），未做双连接显式用例

**实现说明**：WS 端点先 `subscribe` 再推历史、两者同处一个 `try/finally`（确保推历史期间断开也能 `unsubscribe`）；attach/detach 接在 admin 的 start/stop/restart 端点（`process_manager.py` 未改），`app.state.log_manager` 在 factory lifespan 建立、shutdown 经 `asyncio.to_thread` detach。

**已知限制**：① subscribe-first 在突发并发下可能重复若干条日志（无序号去重，tail UI 可接受）；② 进程**崩溃**（不经 stop 端点）时 tail 线程不会立即 detach，会继续 tail 原文件直到 restart 的 attach no-op 或 shutdown detach——修复需接 `health_check_loop`/状态迁移，触及 process_manager，与本次设计决定冲突，暂不做。

---

## Phase 7 — 运行时加固

**前提**: Phase 0–6 全部通过。

---

### A012 — 优雅停止与 SIGTERM 处理

**文件**:
- 修改 `agent/app/factory.py`（lifespan 补充 shutdown 逻辑）
- 修改 `agent/main.py`（捕获 SIGTERM）

**动作**:

1. 在 `lifespan` 的 shutdown 段：
   ```python
   async def shutdown():
       # 1. 停止 WS 广播（关闭所有订阅者队列）
       # 2. 优雅停止 collector（最多 10 秒）
       # 3. 优雅停止 writer（最多 10 秒）
       # 4. 总超时 25 秒后强制退出
   ```

2. 在 `main.py` 中注册 signal handler（Windows 用 `signal.SIGBREAK`）。

**验收**:
- [x] `factory.py` lifespan shutdown 段输出 "Agent shutting down..."，并在 25 秒总超时内按 collector → writer 顺序调用 `pm.stop()`
- [x] shutdown 后 detach collector/writer 日志 tail，并取消 `health_check_loop`
- [x] `main.py` 注册 `SIGTERM` 与 Windows `SIGBREAK` handler
- [x] `python -m compileall app main.py` 通过
- [ ] 真实 collector/writer 进程退出后不残留（依赖 collector/writer 实现 shutdown_flag 监听后做最终验收）

---

### A013 — Windows Service 支持（可选 MVP）

**文件**:
- 创建 `agent/service/install_service.py`

**动作**:

使用 NSSM（Non-Sucking Service Manager）注册方式（比 pywin32 servicemanager 更简单）：

```powershell
# 手动安装步骤（文档化，不脚本化强制安装）
nssm install DataWatchAgent "C:\Python311\python.exe"
nssm set DataWatchAgent AppParameters "D:\data_watch_deploy\agent\main.py"
nssm set DataWatchAgent AppDirectory "D:\data_watch_deploy\agent"
nssm set DataWatchAgent Start SERVICE_AUTO_START
nssm start DataWatchAgent
```

或 `pywin32` 方式，在 `install_service.py` 中实现 `SvcDoRun` / `SvcStop`。

**验收**:
- [x] `agent/service/install_service.py` 已创建，支持 `install/start/stop/restart/remove`，通过 NSSM 管理 `DataWatchAgent`
- [x] `python -m compileall service/install_service.py` 通过
- [ ] `Get-Service DataWatchAgent` 显示 Running（需目标机安装 NSSM 后执行）
- [ ] `Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status` 返回 200（需服务实际启动后验收）
- [ ] 重启 Windows 后服务自动启动（需部署环境验收）

---

## 依赖关系图

```
A001 [P] ─┐
A002 [P] ─┘  Phase 0 Gate
                │
       ┌────────┼────────┐
       ▼        ▼        ▼
     A003      A004    A005 [P]
  (Bootstrap) (Auth)  (ProcessMgr)
                │        │
                └────┬───┘
                     ▼
              A006 [P]  A007 [P]  A008
             (Viewer)  (Admin   (Config
             Proc)      Proc)    API)
                │         │
                └────┬────┘
                     ▼
             A009      A010 [P]
           (Health)   (LogMgr)
                          │
                          ▼
                        A011
                      (WS Log)
                          │
                    A012 + A013
                  (Runtime 加固)
```

---

## 联调验收清单（与前端 Console 对接）

完成 Phase 0–6 后，按以下顺序与 Console 联调：

| 步骤 | 操作 | 通过标准 |
|------|------|---------|
| 1 | 启动 Agent，Console 设置 `VITE_USE_MOCK=false`，`pnpm tauri dev` | Bootstrap 页正常显示 |
| 2 | Bootstrap 页填写 MySQL，点测试连接 | 返回绿色"已连接" |
| 3 | 点完成初始化，跳转登录页 | LoginPage 显示 |
| 4 | 输入账号密码，点登录 | 进入总览页，两个模块卡片显示状态 |
| 5 | 点击"启动" collector | 3 秒内卡片状态更新为 running |
| 6 | 进入 Collector 详情页 | 日志面板显示实时日志（WS 连接成功）|
| 7 | 进入设置页，点各行"测试" | Redis/MySQL/TDengine 状态更新 |
| 8 | 修改 Redis host，点保存 | GET /admin/config 反映新值 |
| 9 | 修改密码字段，点保存 | config.toml password_enc 更新，无明文 |
| 10 | 点"停止" collector | 状态更新为 stopped；日志面板断线 Banner 5 秒内消失（WS 重连）|
