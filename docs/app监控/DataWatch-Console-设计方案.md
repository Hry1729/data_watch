# DataWatch Console 桌面应用设计方案

> 版本：v1.6  
> 创建日期：2026-05-24  
> 状态：app1.0 实现基线（已落地并提交）  
> 适用范围：`app/` 目录下 Tauri 桌面应用工程

---

## 一、背景与目标

### 1.1 背景

当前 collector（采集模块）与 writer（入库模块）均需手动在命令行启动，缺乏统一的管理界面和生命周期保障。现有 Web 前端只面向业务监控（设备、点位、历史数据），不覆盖两个 Python 模块的运维管控需求。

### 1.2 目标

提供一个基于 Tauri 的桌面应用，分两个版本发布：

- **Server Edition（服务端版本）**：部署在运行 collector/writer 的服务器上，安装后可一键启动、停止、监控两个模块，并修改其连接配置。仅管理员角色可操作进程和配置。
- **Client Edition（用户端版本）**：安装在任意机器上，连接远程 Agent 和 Backend，只读查看模块运行状态及业务监控数据，无法操控进程。

### 1.3 核心约束

- 桌面窗口关闭、崩溃、升级不能影响 collector/writer 的持续运行。
- 权限边界由 Agent 后端强制执行，不依赖前端隐藏。
- 认证复用本项目已有 MySQL 账号体系（`sys_user` / `sys_role`），不另建账号系统。
- 敏感配置（数据库密码）不得以明文落地任何文件或日志，MVP 即使用 DPAPI 加密。

---

## 二、总体架构

### 2.1 架构图

```
┌──────────────────────────── Server 机器 ──────────────────────────────┐
│                                                                         │
│  C:\ProgramData\DataWatch\                                              │
│  ├── config.toml      （连接参数，密码 DPAPI 加密）                     │
│  ├── agent_tokens.toml（废弃，改用 MySQL 账号，见第四节）               │
│  ├── logs\            （collector / writer / agent 日志）               │
│  ├── pids\            （collector.pid / writer.pid）                    │
│  └── runtime\         （临时状态文件）                                  │
│                                                                         │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │             DataWatch Agent（Windows 服务，独立进程）              │  │
│  │                                                                    │  │
│  │  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐   │  │
│  │  │ AuthService  │  │ ProcessMgr   │  │ LogTailer            │   │  │
│  │  │ 查 MySQL     │  │ start/stop   │  │ 文件 tail + SSE/WS   │   │  │
│  │  │ sys_user/    │  │ PID 跟踪     │  │                      │   │  │
│  │  │ sys_role     │  │ 健康检查     │  └──────────────────────┘   │  │
│  │  └──────────────┘  └──────────────┘                             │  │
│  │                                                                    │  │
│  │  ┌──────────────────────────────────────────────────────────┐   │  │
│  │  │  HTTP API（默认 127.0.0.1:7070）                          │   │  │
│  │  │  POST /auth/login   → JWT（Admin 或 Viewer 级别）         │   │  │
│  │  │  GET  /admin/*      → 需 Admin JWT（R_SUPER / R_ADMIN）   │   │  │
│  │  │  GET  /viewer/*     → 需 Viewer JWT（任意有效用户）        │   │  │
│  │  └──────────────────────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│          │ 管理                                                           │
│  ┌───────┴──────┐  ┌────────────────┐                                  │
│  │ collector.exe│  │  writer.exe    │                                  │
│  └──────────────┘  └────────────────┘                                  │
│                                                                          │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │  DataWatch Console - Server UI（可选安装，Tauri 桌面端）          │  │
│  │  调用 Agent API（Admin JWT）+ Backend API（业务数据）             │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│                                                                          │
│  ┌──────────────────┐                                                   │
│  │ Backend API      │  ← 已有的 TurboAPI/FastAPI 服务，端口 8000        │
│  └──────────────────┘                                                   │
└──────────────────────────────────────────────────────────────────────────┘

┌──────────────────── Client 机器 ───────────────────┐
│                                                      │
│  AppData\Local\DataWatchClient\settings.toml         │
│  （存 Agent 地址、Backend 地址，不含密码）           │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │  DataWatch Console - Client UI（Tauri 桌面端）  │  │
│  │                                                  │  │
│  │  POST /auth/login → 获取 Viewer JWT             │  │
│  │  GET /viewer/*    → 模块状态/日志/指标           │  │
│  │  Backend API      → 设备/历史/告警等业务数据     │  │
│  └────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

### 2.2 分层职责

| 层 | 组件 | 职责 |
|---|---|---|
| 进程托管层 | DataWatch Agent | 独立 Windows 服务；管理 collector/writer 生命周期；提供认证和 API |
| 业务数据层 | Backend API | 已有；提供设备/点位/历史/报警接口 |
| 桌面 UI 层 | Tauri Console App | 纯 UI 壳；所有数据通过 HTTP 调 Agent / Backend 获取；不直接操作进程 |

---

## 三、首次安装 Bootstrap 模式

### 3.1 问题

首次安装完成后，`config.toml` 中数据库密码为空，Agent 无法连接 MySQL，因此无法通过 `sys_user` 校验账号。此时 Server UI 进不去登录页，也就无法进入 ConfigEditor 填密码——形成"先有鸡还是先有蛋"的死锁。

### 3.2 Bootstrap 模式设计

Agent 启动时检测当前是否处于 bootstrap 状态（MySQL 连接未配置或连接失败），若是则进入 **bootstrap 模式**：

```
Agent 启动
  ├── 尝试连接 MySQL（使用 config.toml 中现有配置）
  │     ├── 成功 → 进入正常模式（关闭 bootstrap 接口）
  │     └── 失败或密码为空 → 进入 bootstrap 模式
  │
  └── bootstrap 模式
        ├── 只监听 127.0.0.1（本机访问，不对外暴露）
        ├── 只开放 bootstrap 专用接口（见 3.3）
        ├── 所有 /admin/* /viewer/* /auth/* 返回 503 + "bootstrap_required"
        └── 配置写入并验证通过后，Agent 自动退出 bootstrap，热重载进入正常模式
```

**安全约束**：bootstrap 接口只监听 `127.0.0.1`，不受影响的远程 Client 会收到明确的错误提示，提示管理员先完成服务器端配置。

### 3.3 Bootstrap 专用接口

```
GET  /bootstrap/status
     → { in_bootstrap: true/false, reason: "mysql_unreachable" | "password_empty" | ... }

PUT  /bootstrap/config
     → 写入 MySQL/TDengine/Redis 连接参数（DPAPI 加密密码落盘）
     → 请求体与 /admin/config 相同格式

POST /bootstrap/test-connection
     → { target: "mysql" | "tdengine" | "redis" }
     → 使用当前 config.toml（已含刚写入的配置）测试连通性
     → 成功返回 { ok: true }，失败返回 { ok: false, error: "..." }

POST /bootstrap/finish
     → 触发 Agent 热重载：关闭 bootstrap 模式，连接 MySQL，开放正常接口
     → 前提：/bootstrap/test-connection?target=mysql 必须已返回 ok: true
```

### 3.4 Server UI Bootstrap 流程

```
Server UI 启动
  → 调 GET /bootstrap/status
  → in_bootstrap == true：
      显示"首次配置"向导页（独立于登录页）
      ┌─────────────────────────────────────────────────┐
      │  首次配置向导                                     │
      │  1. 填写 MySQL 连接（host/port/db/user/password）│
      │  2. [测试连接] → 成功显示绿色勾                  │
      │  3. 填写 TDengine / Redis（可选，可跳过）        │
      │  4. [完成配置] → POST /bootstrap/finish          │
      │  5. 跳转到正常登录页                             │
      └─────────────────────────────────────────────────┘
  → in_bootstrap == false：
      直接显示正常登录页
```

---

## 四、认证与权限模型

### 4.1 认证方案：复用 MySQL 账号体系

Agent 不维护独立账号，直接查询本项目 MySQL 中的 `sys_user` / `sys_role` 表完成认证。

**登录流程：**

```
客户端
  POST /auth/login  { username, password }
        ↓
  Agent AuthService
    1. 查 sys_user，校验密码（与 Backend 使用相同算法，实现前先确认）
    2. 查 sys_role，获取用户角色列表（字段以实际库 role_key 为准，见 4.2 说明）
    3. 判断权限等级：
       - 用户被停用（status != '1'）→ 401
       - 角色包含管理员角色编码       → 颁发 Admin JWT
       - 其他有效用户                 → 颁发 Viewer JWT
    4. 返回 { agent_jwt, role_level }（有效期可配，默认 8h）
        ↓
客户端持有 JWT，后续请求带 Authorization: Bearer <token>
```

### 4.2 sys_role 角色编码确认

> ⚠️ **实现前必须核实**：文档中 `R_SUPER` / `R_ADMIN` 是占位符，以 `sys_role` 表中实际的 `role_key`（或对应字段名）为准。  
> 进入开发前，查询一次 `SELECT role_key FROM sys_role` 确认真实编码，更新到代码常量中，不要硬编码文档里的占位符。

| 角色（sys_role.role_key 示例） | JWT 等级 | 可访问接口 |
|---|---|---|
| 管理员角色（以实际库值为准） | Admin | `/admin/*` + `/viewer/*` |
| 其他有效用户 | Viewer | `/viewer/*` 只读 |
| 被停用用户（status != '1'） | 拒绝 | — |

> **关键**：Server UI 和 Client UI 使用完全相同的登录入口 `POST /auth/login`。Admin API 在 Agent 侧强制校验 JWT 等级，前端是否显示按钮不影响权限控制。

### 4.3 双 token 登录体验

Agent 和 Backend 是两个独立服务，各自维护 token，但用户只需登录一次：

```
登录页（Server UI / Client UI 共用）
  用户输入 username + password
        ↓
  并发发出两个请求：
    ① POST Agent  /auth/login   → agent_jwt（Viewer 或 Admin）
    ② POST Backend /api/auth/login → backend_token
        ↓
  两个请求均成功 → 存入 auth store，进入主界面
  Agent 登录成功但 Backend 失败 → 只显示模块运维页，业务监控页提示"Backend 连接失败"
  Agent 登录失败 → 提示错误，不进入
```

前端 auth store 分别保存两个 token：

```typescript
interface AuthStore {
  agentJwt: string | null       // 用于所有 Agent API 请求
  backendToken: string | null   // 用于所有 Backend API 请求
  roleLevel: 'admin' | 'viewer' | null
}
```

### 4.4 Agent 端 JWT 实现说明

- Agent 使用自己的签名密钥（启动时生成，存入 `C:\ProgramData\DataWatch\agent_jwt_secret`，文件 ACL 仅 SYSTEM 和服务账户可读），不复用 Backend 的 JWT 密钥。
- JWT payload 包含：`user_id`、`username`、`role_level`（admin/viewer）、`exp`。
- MVP 不实现 JWT 黑名单（logout 时前端清除本地 token 即可，token 8h 自然过期）。

---

## 五、DataWatch Agent 详细设计

### 5.1 定位与部署

Agent 是一个独立的轻量 HTTP 服务，使用 Python + FastAPI 实现（复用项目已有技术栈），由安装脚本通过 NSSM 注册为 Windows 服务，开机自启。

- **默认监听地址**：`127.0.0.1:7070`（Server UI 本机访问）
- **远程访问模式**：修改 `config.toml` 中 `agent_host = "0.0.0.0"` 开启远程访问（Client 需要时配置）
- **数据目录**：默认 `C:\ProgramData\DataWatch\`；可在安装时通过环境变量 `DATAWATCH_HOME` 指定自定义目录，适配不能写 C 盘固定路径的现场

### 5.2 服务数据目录

所有 Agent 运行数据放在 `C:\ProgramData\DataWatch\`（或 `DATAWATCH_HOME` 指向的目录），而非用户目录，原因是 Agent 以 Windows 服务账户（LocalSystem 或 NSSM 指定账户）运行，该账户通常无权访问当前登录用户的 `AppData\Local`。

```
C:\ProgramData\DataWatch\
├── config.toml           连接参数（数据库密码 DPAPI 加密）
├── agent_jwt_secret      JWT 签名密钥（文件 ACL：仅服务账户）
├── logs\
│   ├── agent\            Agent 自身日志
│   ├── collector\        collector 的 stdout/stderr（滚动）
│   └── writer\           writer 的 stdout/stderr（滚动）
├── pids\
│   ├── collector.pid
│   └── writer.pid
└── runtime\
    ├── collector.shutdown_flag   优雅退出信号文件（见 5.5）
    └── writer.shutdown_flag
```

Client 侧本地设置（不含敏感数据）：

```
AppData\Local\DataWatchClient\
└── settings.toml         Agent 地址、Backend 地址（无密码）
```

### 5.3 进程管理机制

**启动流程：**

```
start_process(name):
  1. 检查 PID 文件是否存在且进程存活 → 若是则返回"已运行"
  2. 读取 config.toml 构造启动参数
  3. spawn 子进程，stdout/stderr 重定向到 logs/{name}/ 目录的滚动日志文件
  4. 写 PID 文件（包含 pid + 进程名 + 启动时间戳）
  5. 记录启动时间，重置重启计数
```

**停止流程（优雅退出，见 5.5）：**

```
stop_process(name):
  1. 写 runtime/{name}.shutdown_flag 文件（优雅退出信号）
  2. 等待进程自行退出（最多 10s，每 500ms 检查一次 PID 存活）
  3. 超时 → TerminateProcess，记录"强制终止"日志
  4. 删除 PID 文件和 shutdown_flag 文件
```

**PID 接管（Agent 重启后恢复监控）：**

```
Agent 启动时：
  对每个 PID 文件：
    1. 读取 pid + 进程名 + 启动时间戳
    2. 用 psutil 查询 pid 对应进程：
       - 进程不存在                     → 清除 PID 文件，标记 stopped
       - 进程存在但名称/启动时间不匹配   → 判定为 PID 复用，清除 PID 文件，标记 stopped
       - 进程存在且校验通过             → 纳入监控（记录 handle，继续健康检查）
```

> 注意：不存在"恢复 Child handle"的说法。Agent 重启后只能通过 PID + 进程名 + 启动时间戳三重校验判断进程是否仍为自己之前启动的实例，防止 PID 复用误判。

**健康检查（定时任务，每 5s）：**

```
对每个被管理进程：
  - 检查 PID 是否存活（psutil.pid_exists + 名称比对）
  - 若进程意外退出：
      1. 清除 PID 文件
      2. 更新状态为 crashed
      3. 递增重启计数
      4. 推送 process:crashed 事件到所有已连接的 SSE 客户端
      5. （可选）自动重启逻辑（配置项控制，默认关闭）
```

### 5.4 日志管理机制

Agent 不通过 pipe 实时读 stdout，改为：

- **写入**：collector/writer 的 stdout/stderr 被 spawn 时重定向到 `logs/{name}/` 下的滚动文件（按天切割，保留 7 天）
- **读取**：Agent 通过文件 seek 实现 tail，以 WebSocket 推送新增行到前端
- **历史查询**：`GET /viewer/logs/{name}/recent?lines=200` 返回最近 N 行

```
日志文件滚动规则：
  - 按天切割：collector_2026-05-24.log
  - 保留最近 7 天
  - 单文件最大 100MB（超限则切割新文件）
```

**WebSocket 日志鉴权（必须执行，不可省略）：**

```
WS /viewer/logs/{name}/tail

握手阶段鉴权（二选一，以实现方便为准）：
  方式 A：Query param  ws://host:7070/viewer/logs/collector/tail?token=<jwt>
  方式 B：首条消息鉴权  连接建立后，客户端立即发送 { "auth": "<jwt>" }，
                         Agent 收到后校验，校验失败立即关闭连接

校验内容：
  1. JWT 签名合法
  2. JWT 未过期
  3. role_level 为 viewer 或 admin（任意已登录用户）

鉴权失败：Agent 发送 { "error": "unauthorized" } 后关闭连接，不推送任何日志行。
```

**日志内容脱敏规则（Agent + 日志模块共同执行）：**

- Agent HTTP proxy 层：不记录请求/响应中的 `Authorization` header、`password`、`password_enc` 字段
- Agent 自身日志：不打印 config.toml 内容；启动参数中若含密码（如 DSN 字符串），替换为 `***`
- collector/writer 日志：启动时不打印数据库连接字符串的密码部分

### 5.5 优雅退出机制

`/internal/shutdown` HTTP 接口成本较高（需要 collector/writer 各自内置 HTTP 服务），**作为后续增强**，MVP 不依赖它。

**MVP 优雅退出方案（控制文件）：**

collector/writer 在主循环中定期检查 `%DATAWATCH_HOME%\runtime\{name}.shutdown_flag` 文件是否存在，若存在则执行清理并退出。这只需在 Python 主循环中加一行检查，无需额外 HTTP 服务。

> **实施前置子任务**：collector/writer 需支持通过命令行参数 `--runtime-dir <path>` 或环境变量 `DATAWATCH_HOME` 获知 runtime 目录，并在主循环中轮询 shutdown_flag。这是阶段 2（ProcessManager）的前置条件，必须在 Agent 调 stop 之前就绪。

**后续增强（阶段 N+1）：**

collector/writer 各自暴露本地 HTTP 端口（如 `127.0.0.1:7071/7072`），提供 `GET /internal/shutdown` 接口，Agent 优先调接口，降级到控制文件，再降级到强杀。

### 5.6 指标接口（MVP 范围）

MVP 阶段 `/viewer/processes/{name}/metrics` 只返回 Agent 可直接获取的进程级指标，业务指标（采集成功率、队列深度、写入延迟）后续从 Redis / Backend 接入后再添加。

**MVP 指标字段：**

```json
{
  "name": "collector",
  "status": "running",
  "pid": 12345,
  "uptime_seconds": 3600,
  "restart_count": 0,
  "memory_mb": 128.5,
  "cpu_percent": 2.3,
  "started_at": "2026-05-24 08:00:00"
}
```

**后续扩展（非 MVP）：**

```json
{
  "business": {
    "collect_success_rate": 99.5,
    "queue_depth": 0,
    "tdengine_write_latency_ms": 12
  }
}
```

### 5.7 API 清单

**Bootstrap 接口（无需 token，仅 127.0.0.1 可访问，bootstrap 模式下开放）：**

```
GET  /bootstrap/status              { in_bootstrap, reason }
PUT  /bootstrap/config              写入连接参数（DPAPI 加密）
POST /bootstrap/test-connection     { target } → { ok, error? }
POST /bootstrap/finish              退出 bootstrap，切换到正常模式
```

**认证接口（无需 token）：**

```
POST /auth/login          { username, password } → { agent_jwt, role_level }
GET  /auth/me             当前用户信息（验证 token 有效性）
```

**Viewer 接口（任意有效用户，Viewer JWT 即可）：**

```
GET  /viewer/status                      所有进程状态快照
GET  /viewer/processes/{name}/status     单进程状态
GET  /viewer/processes/{name}/metrics    进程级指标（MVP 范围，见 5.6）
GET  /viewer/logs/{name}/recent          最近 N 行日志
WS   /viewer/logs/{name}/tail            实时日志流（WebSocket，握手必须鉴权，见 5.4）
```

**Admin 接口（管理员角色，Admin JWT）：**

```
POST /admin/processes/{name}/start      启动进程
POST /admin/processes/{name}/stop       停止进程
POST /admin/processes/{name}/restart    重启进程
GET  /admin/config                      读取连接配置（密码固定返回 "****"）
PUT  /admin/config                      写入连接配置（Agent DPAPI 加密落盘）
POST /admin/logs/{name}/clear           清空日志文件
```

---

## 六、配置文件与密码安全

### 6.1 config.toml 结构

```toml
[agent]
host = "127.0.0.1"    # 改为 "0.0.0.0" 开启远程访问
port = 7070
log_level = "INFO"

[mysql]
host = "127.0.0.1"
port = 3306
database = "device_watch"
user = "root"
# password_enc：Windows DPAPI (Machine scope) 加密后的 Base64 字符串
# 读取时调用 CryptUnprotectData 解密；写入时调用 CryptProtectData 加密
password_enc = "AQAAANCMnd8BFdERjHoAwE..."

[tdengine]
host = "127.0.0.1"
port = 6030
user = "root"
password_enc = "AQAAANCMnd8BFdERjHoAwE..."

[redis]
host = "127.0.0.1"
port = 6379
# 无密码时 password_enc 留空或不填
```

### 6.2 密码加密策略

**MVP 即使用 DPAPI，不存在"短期明文"方案。**

| 策略 | 实现 | 说明 |
|---|---|---|
| **密码加密（必须）** | Windows DPAPI，Machine scope | 与机器绑定，同机器任意服务账户均可解密；迁移机器需重新配置密码 |
| **文件访问控制（辅助）** | NTFS ACL | `config.toml` 权限设为仅服务账户和 SYSTEM 可读；ACL 作为额外防线，不作为主方案 |
| **接口不返回明文** | Agent 强制 | `GET /admin/config` 中所有密码字段固定返回 `"****"`，绝不透传原文 |
| **日志脱敏** | Agent 和 collector/writer 均执行 | 启动参数、配置加载日志中密码字段替换为 `***`；`Authorization` header 不记录 |

**Machine scope DPAPI 安全边界（直白说明）：**

- **防护对象**：普通用户、远程攻击者拿到文件后无法解密
- **不防护**：本机 Windows 管理员账户可以调用相同 API 解密
- **结论**：对内网工业现场已足够；如需更高安全等级，后续升级到 Windows Credential Manager 或专用密钥管理服务

本项目 Agent 以 Windows 服务方式运行，推荐 **Machine scope DPAPI**（`CryptProtectData` 传入 `CRYPTPROTECT_LOCAL_MACHINE` 标志），使同机器上任何服务账户均可解密，避免服务账户变更导致无法解密的问题。

---

## 七、远程访问安全

Client 机器远程连接 Agent 时，按场景分两种模式：

| 模式 | 适用场景 | 配置 |
|---|---|---|
| **测试模式（HTTP）** | 内部局域网，可信环境 | `agent_host = "0.0.0.0"`，HTTP 明文，依赖物理网络隔离 |
| **生产模式** | 正式现场，有安全要求 | 三选一：① HTTPS（Agent 配置 TLS 证书）② VPN（Client 通过 VPN 访问局域网）③ IP allowlist（Agent 只接受指定 IP 的连接） |

> 当前 MVP 实现测试模式即可，生产模式作为后续增强项，在文档中标记为 TODO。

---

## 八、Tauri Console App 设计

### 8.1 工程结构

```
app/
├── src-tauri/
│   ├── Cargo.toml                ← features: server / client
│   │                               server 版额外依赖: reqwest (HTTP 调 Agent), tray
│   └── src/
│       ├── main.rs
│       ├── http_proxy.rs         ← 代理前端对 Agent 的 HTTP 请求（避免 CORS）
│       └── tray.rs               ← 系统托盘（仅 server feature 编译）
├── src/
│   ├── views/
│   │   ├── server/               ← Server 版专属（Admin 权限用户才能看到）
│   │   │   ├── ProcessControl.vue
│   │   │   ├── LogTerminal.vue
│   │   │   └── ConfigEditor.vue
│   │   ├── shared/               ← 两版本共用（Viewer 权限）
│   │   │   ├── ModuleStatus.vue
│   │   │   ├── MetricsPanel.vue
│   │   │   └── LogViewer.vue
│   │   └── monitor/              ← 业务监控（复用 Web 前端逻辑）
│   │       ├── Realtime.vue
│   │       ├── History.vue
│   │       └── Dashboard.vue
│   ├── api/
│   │   ├── agent.ts              ← Agent API 封装（含 JWT 管理）
│   │   └── backend.ts            ← Backend API 封装（复用 Web 前端）
│   ├── stores/
│   │   └── auth.ts               ← 登录态、JWT、role_level 管理
│   └── router/
│       └── index.ts              ← 根据 VITE_EDITION + role_level 控制路由权限
├── .env.server                   VITE_EDITION=server，Agent=localhost:7070
└── .env.client                   VITE_EDITION=client，Agent 地址用户配置
```

### 8.2 两版本差异对照

| 功能 | Server Edition | Client Edition |
|---|---|---|
| 进程控制（启停/重启） | ✅ 仅 Admin 用户可见 | ❌ |
| 配置编辑 | ✅ 仅 Admin 用户可见 | ❌ |
| 模块状态查看 | ✅ | ✅ |
| 进程指标查看 | ✅ | ✅ |
| 日志查看（只读） | ✅ | ✅ |
| 业务监控（设备/历史/告警） | ✅ | ✅ |
| 系统托盘 | ✅ | ❌ |
| 安装包含 collector/writer | ✅ | ❌ |
| Agent 地址 | 固定 `localhost:7070` | 用户配置远程 IP:Port |
| 安装包大小（估算） | ~80–200 MB | ~10–30 MB |

### 8.3 登录与权限路由

两个版本共享同一个登录页面：

```
LoginPage
  → POST Agent /auth/login
  → 成功：存储 JWT + role_level 到 auth store
  → role_level == 'admin' 且 VITE_EDITION == 'server'：显示进程控制菜单
  → role_level == 'viewer' 或 VITE_EDITION == 'client'：只显示只读菜单
  → 失败（停用账户/密码错误）：显示错误信息
```

### 8.4 主要页面说明

**ProcessControl 页面（Server Edition，Admin 用户）**

- 每个模块的启动/停止/重启按钮
- 顶部一键全部启动 / 停止
- 操作中按钮 loading；操作前弹确认框
- 状态卡片：运行状态、uptime、重启次数、内存/CPU（来自 `/viewer/processes/{name}/metrics`）

**LogTerminal 页面（Server Edition，Admin 用户）**

- 分 Tab 切换 collector / writer 日志
- WebSocket 实时追加（连接 Agent `/viewer/logs/{name}/tail`）
- 关键词过滤、级别过滤
- 最多保留最近 5000 行（防内存膨胀）
- 暂停/恢复滚动开关；一键复制

**ConfigEditor 页面（Server Edition，Admin 用户）**

- 表单编辑 MySQL / TDengine / Redis 连接参数
- 密码字段固定显示 `****`，编辑时可输入新值覆盖
- 保存调 `PUT /admin/config`，Agent DPAPI 加密后落盘
- 保存成功后提示"配置已更新，重启模块后生效"

**ModuleStatus / LogViewer 页面（两版本共用，所有登录用户）**

- 只读展示进程状态、指标、最近日志
- 无启停/配置操作入口

### 8.5 Tauri IPC 设计

Server Edition Rust 层职责：系统托盘 + HTTP 请求代理（解决 Tauri WebView 与 Agent 之间的 CORS 问题）。

**Tauri Commands：**

```typescript
// HTTP 代理（server feature）
invoke('agent_request', {
  method: 'GET' | 'POST' | 'PUT',
  path: '/viewer/status',
  body?: object,
  token: string
})

// 托盘控制（server feature）
invoke('show_window')
invoke('hide_to_tray')
```

**Rust 侧依赖：**

- `reqwest`：HTTP 调 Agent（非 tauri-plugin-shell，Agent 是独立 HTTP 服务，不是子进程）
- Tauri v2 tray API：系统托盘（无需 tauri-plugin-shell）
- `tauri-plugin-shell` 不引入（Tauri 不再直接 spawn 进程）

**HTTP proxy 日志脱敏（Rust 层必须执行）：**

- `http_proxy.rs` 不得将请求的 `Authorization` header 写入任何日志
- 请求体中若含 `password`、`password_enc` 字段，不写日志
- 响应体不做额外日志记录（由 Agent 侧记录即可）

> Client Edition 中，Vue 前端直接通过 `fetch` 调 Agent HTTP API（Agent 已开启远程访问），不需要 Tauri 代理层。

---

## 九、安装包与安装流程

### 9.1 包含内容

| 产物 | Server Edition | Client Edition |
|---|---|---|
| Tauri Console UI | ✅ | ✅ |
| datawatch-agent.exe | ✅（PyInstaller 打包） | ❌ |
| collector.exe | ✅（PyInstaller 打包） | ❌ |
| writer.exe | ✅（PyInstaller 打包） | ❌ |
| NSSM.exe（服务注册工具） | ✅ | ❌ |
| install-agent-service.ps1 | ✅ | ❌ |

> **"Tauri sidecar"术语说明**：collector.exe / writer.exe / agent.exe 由安装脚本复制到安装目录并注册服务，不使用 Tauri sidecar 机制（Tauri sidecar 用于 UI 进程直接管理子进程的场景，本方案已改为 Agent 管理，因此不适用）。

### 9.2 Server 机器安装步骤

```
1. 运行 DataWatch-Console-Server-Setup.exe（管理员权限）
2. 安装脚本执行：
   a. 创建 %DATAWATCH_HOME%（默认 C:\ProgramData\DataWatch\）目录，设置 NTFS ACL
   b. 复制 collector.exe / writer.exe / datawatch-agent.exe 到安装目录
   c. 使用 NSSM 注册 DataWatch Agent 为 Windows 服务（自动启动）
   d. 写入初始 config.toml（密码留空，Agent 启动后进入 bootstrap 模式）
   e. 生成 JWT 签名密钥写入 agent_jwt_secret（ACL 保护）
3. Agent 服务自动启动，监听 127.0.0.1:7070，进入 bootstrap 模式
4. 打开 DataWatch Console Server UI
5. 首次配置向导：填写 MySQL 连接参数 → 测试连接 → 完成配置
6. bootstrap 退出后，使用 MySQL 管理员账号登录
7. 一键启动 collector 和 writer
```

### 9.3 Client 机器安装步骤

```
1. 运行 DataWatch-Console-Client-Setup.exe（普通权限）
2. 首次打开：输入 Agent 地址（http://IP:7070）、Backend 地址（http://IP:8000）
3. 登录（使用 MySQL 账号，任意有效用户均可）
4. 进入只读监控主界面
```

---

## 十、PyInstaller 打包方案

### 10.1 打包命令（示例）

```powershell
# collector 打包
cd D:\python\device_alarm\data_watch\collector
.\.venv\Scripts\pyinstaller.exe `
  --onefile `
  --name collector `
  --exclude-module tkinter `
  --exclude-module test `
  main.py

# writer 打包（路径待确认）
cd D:\python\device_alarm\data_watch\writer
.\.venv\Scripts\pyinstaller.exe `
  --onefile `
  --name writer `
  --exclude-module tkinter `
  main.py

# Agent 打包
cd D:\python\device_alarm\data_watch\agent
.\.venv\Scripts\pyinstaller.exe `
  --onefile `
  --name datawatch-agent `
  main.py
```

### 10.2 可执行文件放置

打包产物放入安装包资源目录（不是 `src-tauri/binaries/`，因为不用 Tauri sidecar 机制）：

```
app/src-tauri/resources/
├── collector.exe
├── writer.exe
└── datawatch-agent.exe
```

由安装脚本在安装时负责复制到目标目录。

---

## 十一、构建流程

### 11.1 构建命令（明确合并版）

```powershell
# 构建 Server Edition（三个参数同时指定）
$env:VITE_EDITION = "server"
pnpm tauri build `
  --features server `
  --config src-tauri/tauri.server.conf.json

# 构建 Client Edition
$env:VITE_EDITION = "client"
pnpm tauri build `
  --features client `
  --config src-tauri/tauri.client.conf.json
```

### 11.2 Cargo features 说明

```toml
# app/src-tauri/Cargo.toml
[features]
server = []   # 编译进 HTTP 代理层 + 托盘逻辑；reqwest 依赖激活
client = []   # 纯 UI 壳；无代理、无托盘

[dependencies]
reqwest = { version = "0.12", features = ["json"], optional = true }
```

### 11.3 tauri.conf.json 分割说明

```
app/src-tauri/
├── tauri.server.conf.json   包含：resources（3 个 exe）、托盘配置、窗口标题"DataWatch Console - Server"
└── tauri.client.conf.json   包含：无 resources、无托盘、窗口标题"DataWatch Console"
```

---

## 十二、实施顺序

> 核心原则：**鉴权框架在第一阶段建立**；Bootstrap 模式先于正常登录；Viewer API 先于 Admin API；进程控制先于配置管理。

| 阶段 | 内容 | 关键子任务 | 产出 | 预估工时 |
|---|---|---|---|---|
| **0. 前置子任务** | collector/writer 支持 shutdown_flag 检查 | 主循环中检查 `%DATAWATCH_HOME%\runtime\{name}.shutdown_flag`；支持 `--runtime-dir` 参数或 `DATAWATCH_HOME` 环境变量 | 优雅退出机制就绪 | 0.5 天 |
| **1. Agent 基础 + 认证 + Bootstrap** | FastAPI 骨架；Bootstrap 模式（首次配置向导接口）；MySQL 账号查询（确认 role_key 实际值）；JWT 颁发；`/auth/login` + `/auth/me`；`/viewer/status` | 先确认 sys_role.role_key 真实字段值 | Agent 可 bootstrap 配置，可登录，可查状态 | 2.5 天 |
| **2. ProcessManager** | PID 文件管理（三重校验）；进程启动/停止（控制文件优雅退出 + 超时强杀）；健康检查（psutil）；crashed 事件推送 | 依赖阶段 0 | 可启停 collector/writer | 2 天 |
| **3. 日志 tail** | 日志文件滚动写入；WS `/viewer/logs/{name}/tail`（握手鉴权必须实现）；`/viewer/logs/{name}/recent` | WS 鉴权不可省略 | 可看实时日志 | 1 天 |
| **4. Admin 启停接口** | `/admin/processes/{name}/start\|stop\|restart`；Admin JWT 校验中间件 | | Server UI 可操控进程 | 1 天 |
| **5. 配置读写** | config.toml DPAPI 加密读写；`/admin/config` GET/PUT；密码固定返回 `****` | DPAPI 第一版即实现 | ConfigEditor 可用 | 1 天 |
| **6. Server UI** | Bootstrap 向导页；双 token 登录页；Dashboard + ProcessControl + LogTerminal + ConfigEditor；Tauri HTTP 代理（含日志脱敏）；系统托盘 | 自动重启开关默认关闭 | Server Edition 可用 | 3 天 |
| **7. Client UI** | 连接配置页；ModuleStatus + LogViewer；业务监控页复用 Web 前端；双 token 登录 | | Client Edition 可用 | 2 天 |
| **8. PyInstaller 脚本** | collector / writer / agent 打包自动化；安装脚本（NSSM 注册服务，DATAWATCH_HOME 支持） | | 两个安装包可构建 | 1.5 天 |
| **9. 构建脚本** | build:server / build:client 命令；CI 输出两个安装包 | | CI 自动化 | 0.5 天 |
| **总计** | | | | **~14 天** |

**里程碑：**
- 阶段 0–1 完成 → Bootstrap + 登录闭环，可验证认证流程
- 阶段 0–5 完成 → Agent 后台服务可独立运行，可通过 curl 全量操控
- 阶段 6 完成 → Server Edition 现场可用
- 阶段 7 完成 → Client Edition 可分发给运维监控人员
- 阶段 8–9 完成 → 正式安装包发布

**后续增强（不在当前版本范围）：**

- collector/writer 内置 `/internal/shutdown` HTTP 接口，实现更完善的优雅退出
- 业务指标接入（从 Redis/Backend 获取采集成功率、队列深度等）
- Agent 远程访问 TLS（HTTPS / VPN / IP allowlist）
- 自动重启策略可配（最多 3 次 + 指数退避，通过 Server UI 开关控制，默认关闭）
- DPAPI → Windows Credential Manager 升级

---

## 十三、风险与缓解

| 风险 | 等级 | 缓解措施 |
|---|---|---|
| PyInstaller 打包体积过大 | 中 | `--exclude-module tkinter,test`；精简 venv 依赖；目标 collector/writer < 80MB |
| Agent 本身崩溃 | 中 | Windows 服务配置失败自动重启（NSSM restart 策略）；Agent 自身异常记入日志 |
| DPAPI Machine scope 被同机器其他管理员读取 | 低 | ACL 限制文件可读范围；DPAPI 作为防线一，ACL 作为防线二 |
| 机器迁移后 config.toml 密码无法解密 | 低 | 迁移文档写明"迁移后需重新在 ConfigEditor 输入密码" |
| PID 复用导致误判进程存活 | 低 | 三重校验（PID + 进程名 + 启动时间）；误判概率极低 |
| 日志终端被高频采集日志刷爆 | 中 | LogTerminal 固定最大行数 5000；暂停滚动时停止追加；前端限速渲染 |
| Client Viewer JWT 泄露 | 中 | Viewer JWT 只有只读权限；支持用户从 Login 页重新登录更换 token；JWT 有效期 8h |
| Agent 与 Tauri UI 版本不兼容 | 低 | Agent `/viewer/status` 响应包含版本号；UI 启动时校验，版本不符提示升级 |

---

## 十四、遗留决策项

| 决策 | 选项 | 建议 | 优先级 |
|---|---|---|---|
| **sys_role.role_key 实际值** | 以真实库为准 | 阶段 1 开始前查库确认，写入代码常量 | **阶段 1 前必须** |
| Agent 服务账户类型 | LocalSystem / 专用服务账户 | 专用服务账户（更安全，DPAPI 绑定明确） | 安装脚本设计前确定 |
| Client 远程访问生产方案 | HTTPS / VPN / IP allowlist | VPN 最简单（不改 Agent 代码）| 上生产前确定 |
| 自动重启策略 | MVP 关闭 / 后续可配 | MVP 默认关闭，Server UI 提供开关；正式现场再开启"最多 3 次 + 退避" | 阶段 6 前确定 |
| JWT 黑名单（logout 撤销） | 内存黑名单 / Redis 黑名单 / 不实现 | MVP 不实现（8h 自然过期）| 正式发布前确定 |
| WS 鉴权方式 | Query param token / 首条消息鉴权 | 两种均可，见 5.4；以团队实现方便为准 | 阶段 3 前确定 |

---

## 十五、当前实现补充（2026-05-31）

本节记录当前 `app/` 实际实现与联调过程中确认的运行契约。若与前文旧设计描述冲突，以本节和 `.specify/` 中的当前 spec-kit 文档为准。

### 15.1 当前技术栈

当前桌面应用实现为：

```text
Tauri v2 + React + TypeScript + Tailwind CSS + shadcn/ui
```

不再使用 Vue / shadcn-vue。Tauri Rust 层仍只承担窗口能力与 Agent HTTP 代理，不直接启动 collector/writer，不直接读写配置，不捕获 stdout/stderr。

### 15.2 本地开发环境变量

`app/.env.development` 当前联调推荐值：

```env
VITE_USE_MOCK=false
VITE_BACKEND_URL=http://127.0.0.1:8000
```

Agent 默认监听：

```text
http://127.0.0.1:7070
```

Tauri dev 模式下 WebView origin 通常是：

```text
http://localhost:1420
```

打包后的桌面应用不再使用 `1420` 端口。生产安装包的 Backend CORS origin 需要在发布前单独确认，或后续改为由 Tauri/Agent 代理 Backend 请求。

### 15.3 登录与 token 传播

登录流程为双认证：

1. `POST /auth/login` 调 Agent，返回 `agentToken`。
2. `POST /api/auth/login` 调 Backend，返回 `backendToken` 和 `backendRefreshToken`。
3. 两者均成功后才进入主界面。

前端登录态只存于 React 内存：

```text
agentToken
backendToken
backendRefreshToken
```

不写 `localStorage`、`sessionStorage`、磁盘或配置文件。

当前实现中，`AuthContext.login()` 会同步调用 `setAgentAuthToken(agentToken)`，把 Agent JWT 写入 `agent-client.ts` 的模块级内存变量。这样页面 Hook 后续调用 `agentClient.getProcessMetrics()` 等受保护 Agent API 时，即使没有显式传 token，也会由 `agent_request` 自动带上 Bearer token。

`AuthContext.logout()` 会清空 AuthContext，同时调用 `setAgentAuthToken(null)`，避免退出后继续携带旧 Agent JWT。

### 15.4 Backend CORS 约束

Agent HTTP 请求路径：

```text
WebView -> invoke("agent_request") -> Tauri Rust -> Agent
```

该路径不受 WebView CORS 影响。

Backend 请求路径当前为：

```text
WebView -> fetch(VITE_BACKEND_URL + "/api/...") -> Backend
```

因此 Backend 必须允许 Tauri dev origin。开发环境至少需要：

```text
http://localhost:1420
http://127.0.0.1:1420
```

如果 Backend 日志显示：

```text
OPTIONS /api/auth/login 204
POST /api/auth/login 200
```

但桌面应用仍提示“后端服务不可达”，优先检查实际 `POST` 响应是否缺少 `Access-Control-Allow-Origin`。浏览器/WebView 会因为 CORS 拒绝把响应交给 JS，前端表现为 `network_error`。

TurboAPI 或自定义 ASGI 分发层需要在 OPTIONS 预检响应和实际业务响应上同时补齐 CORS 头，不能只依赖普通中间件。

### 15.5 日志 WebSocket 鉴权

日志 WS 直连 Agent：

```text
ws://127.0.0.1:7070/viewer/logs/{collector|writer}/tail
```

token 不放在 URL query 中。连接建立后，前端立即发送首帧：

```json
{ "type": "auth", "token": "<agentToken>" }
```

Agent 需在鉴权通过后再开始推送日志，鉴权失败则关闭连接。

### 15.6 配置密码 partial update 契约

数据库密码字段遵循 partial update 契约：

| 场景 | 契约 |
|---|---|
| `GET /admin/config` | 不返回任何数据库 `password` 字段 |
| `PUT /admin/config` 未包含 `password` key | 保留原密码 |
| `PUT /admin/config` 包含非空 `password` | 更新密码 |
| `PUT /admin/config` 包含空字符串 `password: ""` | 视为非法请求 |

设置页密码输入框默认空白。只有用户实际修改密码字段时，前端才会在请求体中带上 `password`。

### 15.7 已修复的联调问题

| 问题 | 原因 | 当前处理 |
|---|---|---|
| 登录成功后闪回登录页 | 登录后受保护 Agent API 未携带 Agent JWT，返回 401 触发 logout | `AuthContext.login()` 同步调用 `setAgentAuthToken(agentToken)` |
| Backend 已返回 200 但 app 报“后端服务不可达” | WebView CORS 拒绝实际 POST 响应 | Backend 自定义 ASGI 响应路径需为 OPTIONS 和实际响应都补 CORS 头 |
| 日志面板被持续日志撑高 | 日志区域缺少稳定高度和内部滚动约束 | 日志卡片改为固定布局并让日志内容区内部滚动 |

### 15.8 采集/入库业务指标与图表接入

采集页、入库页和总览页的业务指标由 Backend 提供，Agent 继续只负责进程与 OS 级指标。

| 区域 | 当前实现 |
|---|---|
| Collector 即时指标 | Backend `module-monitor/collector` 返回采集周期、设备连接数、采集点位与任务表 |
| Writer 即时指标 | Backend `module-monitor/writer` 返回 Stream Lag、PEL、DLQ 与 Stream 表 |
| 速率/趋势图 | Backend module-rates 提供运行期时间序列；app 使用 Recharts 渲染折线/面积图 |
| 图表交互 | 当前值显示在标题右侧；右上角支持 1 分钟 / 5 分钟 / 10 分钟窗口切换 |

### 15.9 顶部连接状态点

顶部 Redis / MySQL / TDengine 状态点不再固定灰色。app 通过 Agent 聚合接口轮询：

```text
GET /viewer/health
```

轮询策略：

- 每 10 秒刷新一次。
- 窗口隐藏时暂停。
- 窗口重新可见时立即刷新。

颜色语义：

| 颜色 | 状态 | 含义 |
|---|---|---|
| 绿色 | `connected` | 连接正常 |
| 蓝色 | `checking` | 正在检测 |
| 红色 | `disconnected` | 检测失败 |
| 灰色 | `unknown` | 未知或无可用结果 |

设置页数据库行的「测试」按钮仍使用 `POST /admin/health/check`，属于手动单项检测，不参与顶部自动轮询。

### 15.10 app1.0 发布基线

app1.0 当前落地形态为单一 `Data Watch Console` 桌面应用：

- Tauri v2 + React + shadcn/ui + Tailwind CSS。
- 默认窗口尺寸为 `1280 x 800`。
- 支持亮色 / 暗色 / 跟随系统三态主题，主题选择仅持久化 `data-watch-theme`，不持久化任何 token。
- 运维动作走 Agent：进程状态、启停/重启、配置读写、健康检查、日志 WebSocket。
- 业务指标走 Backend：collector / writer 即时指标、Stream 状态、速率和趋势图。
- Backend 请求当前仍由 WebView `fetch` 直连，因此开发环境需要 Backend 放行 `http://localhost:1420` 与 `http://127.0.0.1:1420`。
- 打包后的正式应用不依赖 `1420` 端口；`1420` 仅是 `pnpm tauri dev` 时的 Vite/WebView 开发源。

历史章节中关于 Server Edition / Client Edition 的分版设计可作为后续演进方向；app1.0 当前以本节和 15.1-15.9 为准。

---

## 十六、变更记录

| 日期 | 版本 | 内容 |
|---|---|---|
| 2026-05-24 | v1.0 | 初稿，基于 Tauri 直接管理子进程的方案 |
| 2026-05-24 | v1.1 | 重构为 Agent 架构；提升 Agent 为独立后台服务；增加权限模型、日志文件机制、DPAPI 密码加密策略、Windows 优雅退出方案 |
| 2026-05-24 | v1.2 | 修正服务数据目录为 ProgramData；认证改为复用 MySQL sys_user/sys_role 账号体系；DPAPI 由短期方案提升为 MVP 必须项；PID 接管语义修正为三重校验；优雅退出降级为控制文件方案，/internal/shutdown 转后续增强；指标范围收窄为进程级指标；鉴权框架提前至阶段 1；移除 tauri-plugin-shell 依赖；构建命令三参数合并；Remote Access 增加测试/生产模式区分 |
| 2026-05-24 | v1.3 | 新增第三节 Bootstrap 模式（首次安装鸡蛋问题解法）；认证节补充双 token 登录体验与 AuthStore 接口定义；Agent 节补充 DATAWATCH_HOME 可配目录、WS 握手鉴权（方式 A/B）、日志脱敏规则；实施顺序新增 Stage 0 前置子任务（shutdown_flag）；整体置信度提升至 ~95% |
| 2026-05-31 | v1.4 | 补充当前 app 实现：React + shadcn/ui、`.env.development` 联调值、Agent token 内存同步、Backend CORS 约束、WS 首帧鉴权、密码 partial update、登录闪回与 CORS 联调问题处理 |
| 2026-06-01 | v1.5 | 补充 app 业务指标与图表接入：Backend module-monitor/module-rates、Recharts 图表、1/5/10 分钟切换；顶部 Redis/MySQL/TDengine 状态点改为 `GET /viewer/health` 10 秒轮询，颜色语义为绿/蓝/红/灰 |
| 2026-06-03 | v1.6 | 收口 app1.0 发布基线：单一 Console 应用、1280x800 默认窗口、三态主题、Agent/Backend 双数据源职责、开发端口与 CORS 说明 |
