# Data Watch AI 调用与 CLI 实施方案

## 1. 文档目的

本文给出 Data Watch 项目面向人工运维、自动化脚本和 AI Agent 的 CLI 完整建设方案，覆盖目标架构、权限模型、功能范围、命令与输出契约、实施阶段、测试验收、部署发布和风险控制。

本文是一份实施基线。后续编码应以本文确定的接口边界、权限原则和阶段范围为准，避免 CLI、桌面端和 Backend 形成三套不同的业务逻辑。

配套架构图：[CLI 与 AI 调用架构图](./cli-ai-architecture.html)。

## 2. 结论摘要

CLI 建设可行性高，现有 Backend 已具备绝大部分查询和管理 API，不需要改变 Collector、Writer、Agent 或前端核心逻辑。

推荐采用以下总体方案：

1. CLI 作为独立 Python HTTP 客户端，通过 Nginx 统一入口调用 Backend API。
2. CLI 与桌面端共享登录接口、JWT Token、三级角色和权限码体系。
3. CLI 只负责参数解析、标识解析、权限提示、请求编排和稳定输出，不复制 Backend 业务逻辑。
4. Backend 是权限判断和业务规则的最终权威，CLI 侧权限过滤只用于改善体验和约束 AI。
5. 首先交付只读诊断能力，再开放配置修改和删除类高风险命令。
6. CLI 命令和 JSON 契约稳定后，再增加 MCP Server，避免同时维护两套尚未稳定的工具定义。

预计完整交付需要 15 至 22 个工程日，可按阶段独立上线。

## 3. 当前现状与真实差距

### 3.1 已具备能力

- Backend 已提供登录、用户信息、设备、点位、实时数据、历史数据、仪表盘、模块监控、边缘节点、日志、驱动和系统管理 API。
- Backend 已实现 JWT 登录和基于 permission code 的接口鉴权。
- 项目已定义 `R_USER`、`R_ADMIN`、`R_SUPER` 三级默认角色。
- Nginx 已代理 `/api/`、实时 WebSocket 和 `/healthz`。
- `cli/main.py` 已有 Typer 命令骨架和 stdout/stderr 分离约定。
- 前端 `backend-client.ts` 已验证 Backend 统一响应包络和 Bearer Token 调用方式。

### 3.2 当前未闭环差距

1. Backend 会返回 Refresh Token，但当前路由中没有配套的 Token 刷新接口；访问令牌失效后 CLI 需要重新登录。
2. 设备点位告警引擎尚未实现；CLI 禁止调用 `recent-events` 和 `alarm-distribution` 的 synthetic 联调数据提供告警能力。
3. 实时订阅路由仍检查 `monitor:view` 或 `realtime:view`；已定义的 `realtime:subscribe`、`realtime:refresh` 尚未被该路由消费。
4. M4 MCP Server 按项目决策延期，待 CLI 其余能力稳定后单独设计。
5. M5 独立 EXE、安装包、签名以及生产 Nginx/HTTPS 验收尚未实施。

### 3.3 M0-M3 实施状态

- M0 已完成：`/api/user/info` 在保留 `buttons` 的同时返回完整 `permissions`；两者分别对应 `auth_mark` 和 `permission_code`，不保证等价。
- M1 已完成：真实 HTTP 客户端、Profile、Windows Credential Manager、登录/注销、权限发现、稳定 JSON 和退出码已落地。
- M2 已完成：只读目录、设备运行状态、历史查询、实时读取/订阅、系统日志告警、模块/驱动/节点/日志诊断和会话级 Resolver 缓存已落地。
- M3 已完成：设备、点位、历史导出、驱动、边缘节点、采集配置、日志以及用户/角色/菜单管理命令已落地。
- Backend 已新增 `device_monitor:collector_config:update` 专用编辑权限；默认授予 `R_ADMIN` 和 `R_SUPER`，不授予 `R_USER`。
- 当前自动化回归基线：CLI 71 项通过，Backend 105 项通过。

## 4. 建设目标与非目标

### 4.1 建设目标

- 为人工运维提供可发现、可组合、可脚本化的命令行工具。
- 为 AI Agent 提供稳定 JSON、明确退出码和受权限约束的工具入口。
- 覆盖项目已有 Backend API 的主要查询和管理能力。
- 保持 CLI、桌面端和 Backend 的鉴权、字段语义和错误行为一致。
- 支持中心部署、开发直连和后续独立 EXE 发布。
- 为后续 MCP Server 提供可复用的客户端和命令服务层。

### 4.2 非目标

- CLI 不直接访问 MySQL、Redis 或 TDengine。
- CLI 不直接操作 Collector、Writer 或 Edge Agent 的本地文件和进程。
- CLI 不重写 Backend 的权限判断、设备校验、删除约束或驱动生命周期逻辑。
- 第一阶段不开放删除、卸载、清理日志和权限授权等高风险动作。
- 第一阶段不同时实现 MCP Server。

## 5. 设计原则

### 5.1 Backend 权威

CLI 可以在本地判断命令是否可用，但所有请求仍必须由 Backend 再次鉴权。不能将“命令未显示”视为安全措施。

### 5.2 默认只读

首版面向 AI 的默认能力只包括查询、诊断和导出。所有修改命令应显式调用，删除和清理命令应增加资源级确认参数。

### 5.3 JSON 优先

默认输出 JSON。表格只用于人工调试，CSV 只用于数据导出，持续订阅使用 JSON Lines。

### 5.4 标识对人友好

外部参数优先使用设备编码、点位编码和节点 UUID。CLI 内部负责解析 Backend 所需的数据库 ID 和 `varSeq`。

### 5.5 单一入口

生产环境统一访问 Nginx，开发环境允许直连 Backend。CLI 不访问 React 页面，也不依赖 Tauri。

### 5.6 最小权限

AI 使用独立账号，并根据任务分配 `R_USER` 或专用只读角色。生产环境不应默认向 AI 提供 `R_SUPER` Token。

## 6. 目标架构

### 6.1 生产调用链

```text
人工操作员 / AI Agent
        |
        v
Data Watch CLI
        |
        | HTTPS / WSS, Bearer Token
        v
Nginx :8080
        |
        | /api/*, /api/device-monitor/ws/*
        v
Backend :8000
        |
        +---- MySQL：账号、角色、权限、设备和点位配置、日志
        +---- Redis：实时值、窗口、队列和模块健康指标
        +---- TDengine：历史时序数据
```

Nginx 位于 frontend 容器中，但它在此处承担统一网关角色。CLI 并不调用前端代码。

### 6.2 开发调用链

```text
CLI -> http://127.0.0.1:8000 -> Backend
```

开发直连只用于本机调试。生产默认地址应为：

```text
http://<center-host>:8080
```

配置 HTTPS 后应使用：

```text
https://<center-host>
```

### 6.3 组件职责

| 组件 | 职责 |
|---|---|
| CLI 命令层 | 命令注册、参数校验、帮助和确认交互 |
| API Client | HTTP/WS、Token、超时、统一响应解析和下载 |
| Config | 多环境 Profile、服务地址、默认输出格式 |
| Credential Store | 安全保存 Access Token 和 Refresh Token |
| Permission Registry | 命令到 permission code/role 的映射 |
| Resolver | 设备名称/业务分组到设备、IO 地址/点位名称到 `varSeq` 和采集组的解析 |
| Output | JSON、JSONL、table、CSV 和错误输出 |
| Backend | 最终鉴权、业务校验、数据访问和审计 |

## 7. 登录、Token 与配置方案

### 7.1 登录流程

```text
1. CLI 读取当前 Profile 的 base_url。
2. POST /api/auth/login，JSON 请求体携带 userName/password。
3. Backend 返回 access token 和 refresh token。
4. CLI 调用 GET /api/user/info 获取用户、角色和权限。
5. CLI 将凭据写入安全存储，将非敏感配置写入配置文件。
6. 后续 HTTP 请求将登录返回的 Token 规范化后放入 Authorization Header。
7. WebSocket 不在 URL 中携带 Token；连接成功后 5 秒内发送认证消息。
```

WebSocket 认证消息：

```json
{"type":"auth","token":"Bearer <jwt>"}
```

Backend 登录接口返回的 Token 原值已经包含 `Bearer ` 前缀，CLI 应原样放入认证消息的 `token` 字段，不得重复拼接。服务端解析器同时兼容带前缀和裸 JWT，但 CLI 规范固定使用登录响应原值。

### 7.2 配置优先级

从高到低：

1. 单次命令参数，如 `--profile`、`--api-url`。
2. 环境变量，如 `DATAWATCH_API_URL`、`DATAWATCH_TOKEN`。
3. 用户配置文件。
4. 默认值 `http://127.0.0.1:8000`，仅用于开发。

### 7.3 建议配置文件

Windows 默认位置：

```text
%APPDATA%\DataWatch\cli-config.json
```

示例：

```json
{
  "currentProfile": "production",
  "profiles": {
    "development": {
      "baseUrl": "http://127.0.0.1:8000",
      "output": "json",
      "timeoutSeconds": 15
    },
    "production": {
      "baseUrl": "https://data-watch.example.com",
      "output": "json",
      "timeoutSeconds": 30
    }
  }
}
```

配置文件不得保存明文密码。Token 优先使用 Windows Credential Manager；自动化环境可通过 `DATAWATCH_TOKEN` 注入。

### 7.4 Token 过期策略

第一阶段收到 401 后清理失效 Access Token，并返回明确的“需要重新登录”错误。

第二阶段补充 `/api/auth/refresh` 后，可使用 Refresh Token 自动续期一次。自动续期失败时不得无限重试。

## 8. 三级权限模型

### 8.1 默认角色定位

| 角色 | 定位 | 默认能力 |
|---|---|---|
| `R_USER` | 只读用户 | 仪表盘、实时、历史查看、模块健康、日志查看 |
| `R_ADMIN` | 设备运维管理员 | 全部设备监控权限，排除日志清理、日志策略和 DEBUG 开关 |
| `R_SUPER` | 系统超级管理员 | 全部设备监控和系统管理权限 |

### 8.2 CLI 使用方式

每个命令声明以下元数据：

```python
CommandPolicy(
    permissions_any={"device_monitor:device:view"},
    roles_any=set(),
    risk="read",
)
```

执行流程：

1. 未登录命令仅允许 `config`、`auth login`、`system health` 和 `version`。
2. 登录后读取账号权限，生成可用命令列表。
3. 命令执行前进行本地预检查，不满足时返回退出码 4。
4. Backend 对实际接口再次进行权限校验。
5. 如果角色或授权已变化，以 Backend 的 403 结果为准并刷新本地能力缓存。

### 8.3 权限发现接口

M0 已扩展 `/api/user/info`，在保留 `buttons` 的同时返回完整权限码。

- `buttons` 来自 `auth_mark`，只用于前端按钮和页面操作标记。
- `permissions` 来自 `permission_code`，用于 Backend 接口鉴权和 CLI 本地预检查。
- 两套字段不等价。例如菜单新增的 permission code 是 `system:menu:create`，对应 auth mark 是 `add`。

```json
{
  "userId": 3,
  "userName": "viewer",
  "roles": ["R_USER"],
  "permissions": [
    "device_monitor:dashboard:view",
    "device_monitor:realtime:view",
    "device_monitor:history:view",
    "device_monitor:log:view"
  ],
  "buttons": []
}
```

CLI 必须读取 `permissions`，不能读取 `buttons` 进行权限判断，也不能只根据三个角色写死权限，因为项目支持自定义角色和动态授权。

### 8.4 角色能力矩阵

| 功能域 | `R_USER` | `R_ADMIN` | `R_SUPER` |
|---|---:|---:|---:|
| 登录、个人信息、改密 | 是 | 是 | 是 |
| 仪表盘、系统运行告警 | 查看 | 查看 | 查看 |
| 设备运行状态 | 查看 | 查看 | 查看 |
| 设备和点位配置 | 否 | 管理 | 管理 |
| 实时数据 | 查看/订阅 | 查看/订阅 | 查看/订阅 |
| 历史数据 | 查看 | 查看/导出 | 查看/导出 |
| 模块健康 | 查看 | 查看/配置 | 查看/配置 |
| 驱动 | 否 | 查看/管理 | 查看/管理 |
| 边缘节点 | 否 | 管理 | 管理 |
| 日志 | 查看 | 查看/导出 | 全部 |
| 用户、角色、菜单 | 否 | 否 | 管理 |

### 8.5 设备状态与设备管理的区别

`R_USER` 当前没有 `device_monitor:device:view`，不能访问设备配置管理列表。CLI 应将以下两类命令分开：

- `devices status`：使用 `GET /api/device-monitor/module-monitor/device-status` 获取设备 ID、编码、名称、分组、启停状态和采集连接状态，允许只读用户调用。
- `devices list/get`：基于设备管理目录，需要 `device_monitor:device:view`。

字段语义明确区分：`deviceStatus` 来自 `device.enable`，表示 enabled/disabled；`connectStatus` 由 Collector metrics 中最后采集时间计算，表示 online/unstable/offline，并同时返回 `lastCollectTime` 和 `statusSource`。Redis 活跃度不可用时返回 `connectStatus=unknown`，不得把管理启停状态解释为在线状态。

## 9. CLI 命令规范

### 9.1 基本形式

```powershell
data-watch-cli [全局选项] <命令组> <动作> [参数]
```

开发环境保留模块调用方式：

```powershell
python -m cli.main <命令组> <动作>
```

### 9.2 全局选项

| 参数 | 说明 |
|---|---|
| `--profile <name>` | 使用指定连接配置 |
| `--api-url <url>` | 单次覆盖服务地址 |
| `--format json|table|csv|jsonl` | 输出格式 |
| `--timeout <seconds>` | 请求超时 |
| `--pretty/--compact` | JSON 美化或紧凑输出 |
| `--trace-id` | 输出 Backend 返回的请求追踪标识 |
| `--no-color` | 禁用 stderr 彩色输出 |

默认值为 `--format json --compact`，保证 AI 稳定解析。

### 9.3 通用命令

| 功能 | 命令 | 权限 | 阶段 |
|---|---|---|---|
| 设置服务地址 | `config set-url <url>` | 无 | M1 |
| 查看配置 | `config show` | 无 | M1 |
| 新建 Profile | `config profile create <name> --url <url>` | 无 | M1 |
| 切换 Profile | `config profile use <name>` | 无 | M1 |
| 登录 | `auth login --username <name> --password-stdin` | 无 | M1 |
| 当前账号 | `auth whoami` | 已登录 | M1 |
| 可用命令 | `auth capabilities` | 已登录 | M1 |
| 退出 | `auth logout` | 已登录 | M1 |
| 修改密码 | `auth change-password` | 已登录 | M3 |
| 服务健康 | `system health` | 无 | M1 |
| CLI 版本 | `version` | 无 | M1 |

### 9.4 仪表盘和系统运行告警

| 功能 | 命令 | 权限 | 阶段 |
|---|---|---|---|
| 系统概览 | `dashboard overview` | `dashboard:view` | M2 |
| 指标趋势 | `dashboard trend --range 24h --granularity 1h` | `dashboard:view` | M2 |
| 设备类型分布 | `dashboard device-types --top 10` | `dashboard:view` | M2 |
| 连接健康 | `dashboard connection-health --range 24h` | `dashboard:view` | M2 |
| 边缘链路健康 | `dashboard pipeline-health` | `dashboard:view` | M2 |
| 系统告警汇总 | `system-alerts summary` | `dashboard:view` | M2 |
| 系统告警日志 | `system-alerts list --level ERROR` | `log:view` | M2 |

`system-alerts summary` 使用 Dashboard overview 中真实的近 24 小时 `systemAlertTotal`；`system-alerts list` 使用日志接口合并查询 `system_log` 与 `operation_log`，`--level` 仅允许 WARN、ERROR 或 CRITICAL。系统运行告警是高等级日志，不具有设备点位告警的确认、恢复和处置状态机。

当前项目未实现设备点位告警引擎，包括阈值越限、通讯中断、状态位告警、告警确认和恢复状态机。CLI 暂不提供 `alarms list`、`alarms distribution` 或 `events list --type alarm`，也不得调用或输出 Dashboard 的 synthetic 告警数据。设备点位告警必须独立立项，完成真实规则评估和 `alarm_record` 写入链路后再接入 CLI。

### 9.5 设备

| 功能 | 命令 | 权限 | 阶段 |
|---|---|---|---|
| 设备运行状态 | `devices status [--device 桥吊100]` | `monitor:view` 或 `realtime:view` | M2 |
| 设备列表 | `devices list [--status online]` | `device:view` | M2 |
| 设备详情 | `devices get <设备名称、业务分组、编码或 ID>` | `device:view` | M2 |
| 设备分组树 | `devices groups` | `device:view` | M2 |
| 创建设备 | `devices create --file device.json` | `device:create` | M3 |
| 修改设备 | `devices update <code-or-id> --file device.json` | `device:update` | M3 |
| 启用设备 | `devices enable <code-or-id>` | `device:update` | M3 |
| 停用设备 | `devices disable <code-or-id>` | `device:update` | M3 |
| 导入设备 | `devices import --file devices.xlsx` | `device:import` | M3 |
| 导出设备 | `devices export --output devices.xlsx` | `device:export` | M3 |
| 删除设备 | `devices delete <code-or-id> --confirm <code>` | `device:delete` | M3 |
| 批量删除 | `devices delete-batch --ids 1,2,3 --confirm batch` | `device:delete` | M3 |

表中的短权限名均以 `device_monitor:` 为前缀。

### 9.6 点位

| 功能 | 命令 | 权限 | 阶段 |
|---|---|---|---|
| 解析业务点位 | `points resolve I101 [--device 轨道吊701陆侧]` | `point:view`、`history:view`、`monitor:view` 或 `realtime:view` | M2 |
| 点位列表 | `points list --device QC100` | `point:view` | M2 |
| 点位详情 | `points get <point-id>` | `point:view` | M2 |
| 点位组 | `points groups --device 桥吊100` | `device:view` | M2 |
| 设备变量 | `points variables --device QC100` | `device:view` | M2 |
| 创建点位 | `points create --file point.json` | `point:create` | M3 |
| 修改点位 | `points update <point-id> --file point.json` | `point:update` | M3 |
| 启用点位 | `points enable <point-id>` | `point:update` | M3 |
| 停用点位 | `points disable <point-id>` | `point:update` | M3 |
| 导入点位 | `points import --file points.xlsx` | `point:import` | M3 |
| 导入模板 | `points template --device QC100 --output template.xlsx` | `point:export` | M3 |
| 导出点位 | `points export --device 桥吊100 --output points.xlsx` | `point:export` | M3 |
| 删除点位 | `points delete <point-id> --confirm <point-id>` | `point:delete` | M3 |
| 批量删除 | `points delete-batch --ids 1,2,3 --confirm batch` | `point:delete` | M3 |

### 9.7 实时数据

| 功能 | 命令 | 权限 | 阶段 |
|---|---|---|---|
| 可监控设备树 | `realtime tree` | `monitor:view` 或 `realtime:view` | M2 |
| 按 IO 地址查询 | `realtime get --point DB1702.DBW0` | `monitor:view` 或 `realtime:view` | M2 |
| 按点位名称查询 | `realtime get --device 桥吊100 --point CraneID` | 同上 | M2 |
| 持续订阅单点 | `realtime watch --device QC100 --point CraneID` | `monitor:view` 或 `realtime:view` | M2 |


`realtime watch` 使用 WebSocket 并输出 JSON Lines，每行一个完整事件。当前路由实际检查 `device_monitor:monitor:view` 或 `device_monitor:realtime:view`，任一满足即可；`realtime:subscribe` 虽已定义并授予默认角色，但当前没有被该路由消费。是否切换到专用订阅权限属于后续独立兼容性决策。

CLI 必须连接不含 Token 的 WebSocket URL，连接成功后发送 `{"type":"auth","token":"<登录响应 token 原值>"}`，再发送 subscribe 消息。禁止使用 `?token=`，避免 Token 进入 URL、代理访问日志和诊断记录。

面向用户和 AI 的实时命令不要求输入内部 `groupCode`。CLI 先按 IO 地址或点位名称解析目标点位，再从点位映射自动取得设备和采集组；兼容用的 `--group` 参数不在帮助中展示。

### 9.8 历史数据

| 功能 | 命令 | 权限 | 阶段 |
|---|---|---|---|
| 历史设备 | `history devices` | `history:view` | M2 |
| 设备类型 | `history device-types` | `history:view` | M2 |
| 历史点位 | `history points --device 轨道吊701海侧` | `history:view` | M2 |
| 历史查询 | `history query --point CraneID --start <time> --end <time>` | `history:view` | M2 |
| 趋势聚合 | `history chart --interval 1m --aggregate avg ...` | `history:view` | M2 |
| 单点原始数据 | `history raw --device ASC701S --point DB1702.DBW0 ...` | `history:view` | M2 |
| 多点矩阵 | `history matrix --point CraneID --point I101 --align 1s ...` | `history:view` | M2 |
| 创建导出任务 | `history export ...` | `history:export` | M3 |
| 导出进度 | `history export-status <task-id>` | `history:export` | M3 |
| 下载导出 | `history export-download <task-id> --output history.csv` | `history:export` | M3 |

多点参数允许重复。`history query/chart/raw/matrix/export` 中的 `--device` 可省略；CLI 会按 IO 地址、点位名称或变量序号解析 `varSeq` 并推断设备。多个点位必须属于同一设备，否则返回结构化错误。

Resolver 对设备支持现场称呼、设备名称、业务分组名称、设备类型、设备编码和 ID；`桥吊/QC`、`轨道吊/ASC`、`陆侧/L`、`海侧/S` 按确定性规则归一化且大小写不敏感。对点位支持 IO 地址、点位名称和变量序号。匹配顺序为精确、前缀、包含；只有最佳候选唯一时才自动选择。若多个桥吊存在同名点位，CLI 返回设备名称、业务分组、点位名称和 IO 地址组成的候选列表，AI 必须结合上下文或追问用户，不得默认选择第一项。

Resolver 使用进程内会话缓存，按查询词和可选设备提示键控，在一次命令内复用设备、点位和采集组映射。普通 CLI 每次启动是新进程，不承诺跨进程缓存；未来常驻 MCP Server 可直接复用该缓存。

### 9.9 模块健康

| 功能 | 命令 | 权限 | 阶段 |
|---|---|---|---|
| 汇总健康状态 | `modules status` | `monitor:view` 或 `realtime:view` | M2 |
| 采集器指标 | `modules collector` | 同上 | M2 |
| 写入器指标 | `modules writer` | 同上 | M2 |
| 速率趋势 | `modules rates` | 同上 | M2 |
| 采集配置 | `modules collector-config show` | 同上 | M2 |
| 修改 Stream 上限 | `modules collector-config set --stream-maxlen 100000` | `collector_config:update` | M3 |

`modules status` 是 CLI 聚合命令，应同时检查 Backend、Collector、Writer 和边缘链路，输出 `healthy`、`degraded` 或 `down` 以及原因数组。

### 9.10 驱动

| 功能 | 命令 | 权限 | 阶段 |
|---|---|---|---|
| 驱动列表 | `drivers list` | `driver:view` | M2 |
| 驱动选项 | `drivers options` | `driver:view` | M2 |
| 注册驱动 | `drivers register --file driver.json` | `driver:manage` | M3 |
| 卸载驱动 | `drivers uninstall <name> --confirm <name>` | `driver:manage` | M3 |
| 卸载状态 | `drivers uninstall-status <name>` | `driver:view` | M3 |

### 9.11 边缘节点

| 功能 | 命令 | 权限 | 阶段 |
|---|---|---|---|
| 节点列表 | `nodes list` | `R_ADMIN` 或 `R_SUPER` | M2 |
| 分配占用 | `nodes occupancy` | 同上 | M2 |
| 节点设备 | `nodes assignments <node-uuid>` | 同上 | M2 |
| 分配设备 | `nodes assign <node-uuid> --device AGV801 --confirm <node-uuid>` | 同上 | M3 |
| 启用节点 | `nodes enable <node-uuid> --confirm <node-uuid>` | 同上 | M3 |
| 停用节点 | `nodes disable <node-uuid> --confirm <node-uuid>` | 同上 | M3 |
| 节点设置 | `nodes settings <node-uuid> --file settings.json` | 同上 | M3 |
| 注册码 | `nodes registration-code --name edge-01 --expires 30` | 同上 | M3 |
| 同步设备状态 | `nodes sync-device-status --confirm sync` | 同上 | M3 |

边缘节点自己的注册、心跳、配置拉取和密钥轮换属于机器协议，不进入普通 AI CLI 命令集。

### 9.12 日志

| 功能 | 命令 | 权限 | 阶段 |
|---|---|---|---|
| 日志列表 | `logs list --level ERROR --device 轨道吊701海侧` | `log:view` | M2 |
| 日志统计 | `logs stats` | `log:view` | M2 |
| TDengine 日志文件 | `logs tdengine-files` | `log:view` | M2 |
| TDengine 日志尾部 | `logs tdengine-tail --file <name> --lines 200` | `log:view` | M2 |
| 清理策略 | `logs policy show` | `log:view` | M2 |
| 修改策略 | `logs policy set --file policy.json` | `log:policy` | M3 |
| 开启 DEBUG | `logs debug enable --minutes 30 --reason <text>` | `log:debug` | M3 |
| 关闭 DEBUG | `logs debug disable` | `log:debug` | M3 |
| 手工清理 | `logs cleanup --before <time> --confirm cleanup` | `log:cleanup` | M3 |

### 9.13 用户、角色和菜单

这些命令默认仅 `R_SUPER` 可用，但最终仍以 permission code 为准。

| 功能域 | 命令 |
|---|---|
| 用户 | `users list/get/create/update/enable/disable/reset-password/delete` |
| 角色 | `roles list/options/get/create/update/enable/disable/delete` |
| 角色授权 | `roles grants get/set` |
| 菜单 | `menus current/list/create/update/delete` |
| 菜单权限 | `menus permissions list/create/update/delete` |

上述命令进入 M3。创建和更新使用明确选项处理常用字段，同时支持 `--file payload.json` 处理复杂结构。

## 10. AI 调用契约

### 10.1 标准成功输出

```json
{
  "ok": true,
  "command": "devices.status",
  "data": {},
  "meta": {
    "profile": "production",
    "requestId": "optional",
    "durationMs": 83
  }
}
```

### 10.2 标准错误输出

stdout 保持为空，stderr 输出机器可解析 JSON：

```json
{
  "ok": false,
  "error": {
    "type": "permission_denied",
    "code": 403,
    "message": "当前账号不能执行 devices.delete",
    "requiredPermissions": ["device_monitor:device:delete"]
  }
}
```

### 10.3 退出码

| 退出码 | 含义 |
|---:|---|
| 0 | 成功 |
| 2 | 命令或参数错误 |
| 3 | 未登录、Token 失效 |
| 4 | 权限不足 |
| 5 | 网络、DNS、TLS 或连接失败 |
| 6 | Backend API 业务错误 |
| 7 | 部分成功 |
| 8 | 超时或任务未完成 |

### 10.4 输出纪律

- stdout 只输出业务 JSON、JSONL 或明确指定的 CSV。
- 日志、进度、警告和错误只进入 stderr。
- JSON 字段使用固定 camelCase，不随 table 中文列名变化。
- 默认不输出 Token、密码、密钥、完整 Authorization Header。
- `--format table` 只供人工使用，不作为 AI 契约。
- 持续订阅收到 Ctrl+C 时正常关闭 WebSocket，并返回退出码 0。

### 10.5 查询保护

- 列表默认 `pageSize=50`，允许设置上限。
- 历史 raw 单页遵循 Backend 最大 5000 条限制。
- 历史 matrix 最多 10 个点位、单页最多 500 行。
- AI 默认历史查询时间范围建议不超过 24 小时。
- 对大范围查询优先使用 `chart` 聚合或异步 `export`。

## 11. 高风险命令控制

### 11.1 风险分级

| 等级 | 示例 | 控制方式 |
|---|---|---|
| read | 查询状态、实时、历史、日志 | 权限检查即可 |
| write | 创建设备、修改点位、节点分配 | 权限检查、审计日志 |
| destructive | 删除、卸载、日志清理 | 权限、资源级确认、审计 |
| privileged | 用户授权、角色授权、密码重置 | 超级管理员、确认、审计 |

### 11.2 确认规则

交互终端可以要求输入资源编码。非交互 AI 调用必须显式提供 `--confirm <resource>`，且确认值必须和目标资源匹配。

不建议提供全局 `--yes` 绕过所有确认。批量操作只能使用专用的 `--confirm batch`，并在执行前输出目标数量到 stderr。

### 11.3 审计

所有写操作应由 Backend 记录操作人、角色、命令对应动作、资源标识、请求 ID、结果和失败原因。CLI 不应成为唯一审计来源。

## 12. 代码结构建议

目标结构：

```text
cli/
  __init__.py
  main.py                 # Typer 根应用与全局选项
  client.py               # HTTP/WS Client 和响应解析
  config.py               # Profile 与环境变量
  credentials.py          # Token 安全存储
  permissions.py          # 命令权限注册和预检查
  output.py               # JSON/table/CSV/JSONL
  resolvers.py            # device code、point code 解析
  errors.py               # CLI 错误类型与退出码
  commands/
    auth.py
    system.py
    dashboard.py
    devices.py
    points.py
    realtime.py
    history.py
    modules.py
    drivers.py
    nodes.py
    logs.py
    users.py
    roles.py
    menus.py
  tests/
    test_client.py
    test_auth.py
    test_permissions.py
    test_output_contract.py
    test_resolvers.py
    test_commands_*.py
```

第一阶段可以先保留较少模块；当命令组超过五个后再按领域拆分，避免 `main.py` 持续膨胀。

建议新增独立依赖文件：

```text
cli/requirements.txt
```

核心依赖：

```text
typer
rich
httpx
websockets
keyring
```

测试依赖使用 `pytest`。HTTP 测试优先使用 `httpx.MockTransport`，不依赖真实数据库。

## 13. 分阶段实施方案

### M0：Backend 契约准备（已完成）

范围：

- 扩展 `/api/user/info` 返回 `permissions`，同时保留 `buttons`。
- 用不等价样例验证 `auth_mark` 和 `permission_code` 分别返回。
- 记录实时 WebSocket 当前使用 `monitor:view` 或 `realtime:view`，本阶段不改变路由行为。
- 固定 WebSocket CLI 使用连接后认证消息，并发送登录响应 Token 原值。
- 确认统一响应格式和 401/403 语义。
- 决定 Refresh Token 是补刷新接口还是第一阶段暂不使用。

验收：三个默认角色访问同一接口时，结果符合权限矩阵。

### M1：CLI 基础能力（已完成）

范围：

- 实现真实 `httpx` Client。
- 实现 Profile、环境变量和配置文件。
- 实现登录、退出、whoami、capabilities。
- 实现 Token 安全存储和 Authorization Header。
- 实现统一 JSON、错误和退出码。
- 实现 `system health` 和 `version`。
- 建立 MockTransport 单元测试。

验收：CLI 可通过 Nginx 和 Backend 直连两种方式登录，并稳定解析 `{code,msg,data}`。

### M2：只读查询与诊断 MVP（已完成）

范围：

- 仪表盘、真实系统告警统计和高等级日志查询；不接入设备点位 synthetic 告警。
- 设备运行状态、设备/点位只读目录，并区分启停状态与实时连接状态。
- 实时单次查询和使用认证消息的 WebSocket 订阅。
- 历史 query/chart/raw/matrix。
- 模块 collector/writer/rates/status。
- 驱动只读、节点只读、日志只读。
- 设备业务名称/分组和 IO 地址/点位名称 Resolver，以及进程内会话缓存。
- 三级权限命令过滤。
- 为 Collector 配置修改设计专用编辑权限，避免沿用查看权限。

验收：`R_USER` 能完成查询和诊断工作，但不能进入配置管理；AI 能只依赖 stdout JSON 完成解析。

### M3：管理与高风险命令（已完成）

范围：

- 设备和点位增删改、导入导出。
- 历史异步导出。
- 驱动注册和卸载。
- 边缘节点注册、配置和设备分配。
- 日志策略、DEBUG 和清理。
- 用户、角色、菜单和权限管理。
- 高风险确认、下载、上传和审计验证。

验收：`R_ADMIN` 和 `R_SUPER` 的边界符合权限矩阵，所有 destructive 命令均无法被无确认调用。

### M4：AI 工具化与 MCP（延期，待独立子方案）

范围：

- 固化所有命令 JSON Schema。
- 增加 `commands describe <command>` 供 AI 获取参数定义。
- 增加面向 AI 的默认分页、时间范围和结果摘要。
- 先产出独立 MCP 子方案，明确 tool schema、CLI 命令到 MCP tool 的映射、权限发现和错误模型。
- 在同一 Client/Service 层上实现 MCP Server。
- MCP 工具列表根据当前账号权限动态生成。
- 为只读和写操作设置不同的工具暴露策略。
- 单独设计 WebSocket 订阅在 MCP 中的长任务、取消、背压和异步流式输出协议。

验收：CLI 和 MCP 对同一查询返回等价数据和错误语义。

### M5：打包、部署与发布，2 至 3 个工程日

范围：

- 使用 PyInstaller 生成 `data-watch-cli.exe`。
- 提供版本信息、构建信息和校验值。
- 加入 Windows 安装包或独立 ZIP 发布物。
- 完成 Nginx、HTTPS、代理超时和 WebSocket 验证。
- 编写安装、升级、卸载和问题排查文档。

验收：在无 Python 环境的 Windows 目标机上可直接运行，并通过中心 Nginx 完成登录、查询和实时订阅。

## 14. 测试方案

### 14.1 单元测试

- URL 和 query 参数编码。
- `{code,msg,data}` 成功与业务错误解析。
- 401、403、404、500、超时和网络错误映射。
- Token 读取、遮盖和清理。
- 设备、点位 Resolver 的精确匹配、唯一模糊匹配、歧义、不存在和跨设备多点场景。
- JSON stdout 与 stderr 隔离。
- 高风险确认值匹配。

### 14.2 权限矩阵测试

至少准备三个测试账号：

| 场景 | `R_USER` | `R_ADMIN` | `R_SUPER` |
|---|---:|---:|---:|
| `devices status` | 0 | 0 | 0 |
| `devices create` | 4 | 0 | 0 |
| `history export` | 4 | 0 | 0 |
| `nodes list` | 4 | 0 | 0 |
| `logs cleanup` | 4 | 4 | 0 |
| `users list` | 4 | 4 | 0 |

同时直接绕过 CLI 调用 Backend，确认后端仍返回正确的 403。

### 14.3 集成测试

- Backend 直连 `127.0.0.1:8000`。
- Nginx 入口 `127.0.0.1:8080`。
- WebSocket 订阅、取消订阅和 Ctrl+C。
- 历史导出任务创建、轮询和下载。
- 设备或点位在查询过程中被删除的错误处理。
- Token 失效、账号禁用和角色授权变化。

### 14.4 打包验收

- 干净 Windows 环境运行 EXE。
- 中文设备名和路径正常显示。
- 配置文件路径包含中文用户名时正常工作。
- Windows Credential Manager 可读写和清理 Token。
- 杀毒软件和签名策略验证。

## 15. 部署方案

### 15.1 生产推荐

```powershell
data-watch-cli config profile create production --url https://data-watch.example.com
data-watch-cli config profile use production
data-watch-cli auth login --username ai_viewer --password-stdin
data-watch-cli auth capabilities
```

Nginx 继续作为统一入口，无需为 CLI 新增服务器。建议生产增加 HTTPS，并限制 Backend 的 `8000` 端口只允许内网或本机访问。

### 15.2 AI 运行账号

- 普通问答和诊断使用 `R_USER` 或专用只读角色。
- 设备配置自动化使用受控 `R_ADMIN` 账号。
- 不向通用 AI Agent 提供 `R_SUPER`。
- Token 通过环境变量或凭据存储注入，不写入 Prompt、命令参数或日志。
- 每个自动化主体使用独立账号，便于审计和吊销。

### 15.3 超时建议

| 请求类型 | 建议超时 |
|---|---:|
| 登录、列表、状态 | 15 秒 |
| 普通历史查询 | 30 秒 |
| 导入和导出创建 | 60 秒 |
| 文件下载 | 300 秒 |
| WebSocket | 长连接，心跳检测 |

## 16. 风险与应对

| 风险 | 影响 | 应对 |
|---|---|---|
| CLI 权限映射与 Backend 不一致 | 命令误显示或误拒绝 | Backend 返回 permissions，Backend 最终鉴权 |
| 普通用户无法访问设备管理目录 | 无法直接使用 `devices list` | 提供基于监控树的 `devices status` |
| Token 明文落盘 | 凭据泄露 | Windows Credential Manager，环境变量注入 |
| 历史查询数据量过大 | 超时或上下文过载 | 默认范围、分页、chart 聚合、异步导出 |
| AI 误执行删除和清理 | 数据或配置损失 | 分阶段开放、资源确认、独立账号、审计 |
| Nginx WebSocket 配置异常 | 实时订阅失败 | 复用现有 WS location 并做发布验收 |
| 设备点位告警未实现 | CLI 可能向 AI 输出 synthetic 假数据 | M2 禁止调用 synthetic 接口；告警引擎独立立项 |
| Refresh Token 无刷新接口 | 长任务中断 | M1 明确重新登录，后续补 refresh API |
| EXE 被安全软件拦截 | 无法部署 | 固定构建环境、签名、校验值和白名单流程 |

## 17. 验收标准

### 17.1 功能验收

- 可配置 Nginx 或 Backend 地址。
- 可登录、查看账号和退出。
- 可按权限发现和执行命令。
- 可查询设备状态、单点实时值、历史数据和模块健康。
- 可持续订阅实时数据。
- 管理员可执行授权范围内的管理命令。
- 超级管理员可执行系统管理和日志高风险命令。

### 17.2 契约验收

- 成功时 stdout 是唯一且合法的 JSON 文档，或明确指定的 JSONL/CSV。
- 失败时 stdout 为空，stderr 可解析，退出码稳定。
- 不在输出、配置文件或日志中泄露密码和 Token。
- 同一命令在直连 Backend 和通过 Nginx 时业务结果一致。
- CLI 不得输出 `FAKE-ALARM-*`、`synthetic=true` 或“联调假数据”等设备点位告警内容。

### 17.3 权限验收

- CLI 预检查和 Backend 403 结果一致。
- `R_USER` 不能执行任何配置修改、删除或导出权限外动作。
- `R_ADMIN` 不能执行日志清理和系统权限管理。
- `R_SUPER` 的高风险操作仍需要显式确认。
- 账号禁用和角色变更能够在下一次请求中生效。

### 17.4 发布验收

- Windows 无 Python 环境可运行。
- `--help`、`version` 和错误信息完整。
- 配置升级保持向后兼容。
- 发布物包含版本、SHA-256 和变更说明。

## 18. 推荐实施顺序

建议立即按以下顺序推进：

1. 先完成 M0，补齐 permissions 返回和权限契约测试。
2. 完成 M1，把现有模拟 `BackendClient` 替换为真实登录和 HTTP 调用。
3. 完成 M2，交付可供 AI 使用的只读查询和诊断 MVP。
4. 在真实环境运行一轮权限、数据量和输出契约验收。
5. 再进入 M3，逐项开放管理和高风险动作。
6. CLI 契约稳定后进入 M4，实现 MCP Server。
7. 最后完成 EXE、安装包、签名和生产发布。

该顺序能最早交付有价值的 AI 查询能力，同时把权限误配、误删除和接口契约变化控制在较小范围内。
