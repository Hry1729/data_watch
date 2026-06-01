# Feature Specification: Data Watch Console 桌面应用

**Feature Branch**: `001-data-watch-console`

**Created**: 2026-05-25

**Status**: Draft

**Input**: User description: "开发 Data Watch Console 桌面应用，管理 collector 和 writer 两个模块"

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Bootstrap 首次配置向导 (Priority: P1)

首次安装后，管理员打开应用，发现 MySQL 尚未配置。应用自动检测到 Agent 处于 bootstrap 状态，显示配置向导页。管理员填写 MySQL 连接参数，测试连接成功后完成初始化，应用跳转到登录页。

**Why this priority**: 没有 Bootstrap 流程，任何其他页面都无法使用。它是整个应用的启动门禁。

**Independent Test**: 在 Agent 未配置 MySQL 的环境下启动应用，能完成 MySQL 配置并进入登录页，即为可测试的 MVP。

**Acceptance Scenarios**:

1. **Given** Agent 返回 `GET /bootstrap/status` → `{ in_bootstrap: true }`，**When** 应用启动，**Then** 显示 Bootstrap 向导页，不显示主导航、登录表单或任何其他页面内容。
2. **Given** Bootstrap 向导页已填写 MySQL 连接参数，**When** 用户点击「测试连接」，**Then** 调用 `POST /bootstrap/test-connection { target: "mysql" }`，成功时显示绿色勾，失败时显示错误信息（不含密码明文）。
3. **Given** 测试连接已成功，**When** 用户点击「完成初始化」，**Then** 调用 `PUT /bootstrap/config` 写入配置，再调用 `POST /bootstrap/finish`，随后跳转登录页。
4. **Given** 「完成初始化」按钮，**When** 测试连接尚未通过，**Then** 按钮处于禁用状态，不可点击。
5. **Given** Agent 返回 `{ in_bootstrap: false }`，**When** 应用启动，**Then** 直接显示登录页，不经过 Bootstrap 向导。

---

### User Story 2 — 登录认证 (Priority: P1)

管理员或运维人员打开应用，输入 MySQL 系统账号的 username 和 password，点击登录。应用同时向 Agent 和 Backend 发起登录请求，两者均成功后进入总览页。

**Why this priority**: 所有运维操作和数据查看均需登录态，无登录则无法进入任何主功能页。

**Independent Test**: 输入有效账号后能进入总览页，输入无效账号显示错误，即为独立可测试。

**Acceptance Scenarios**:

1. **Given** 登录页已填写有效 username 和 password，**When** 用户点击「登录」，**Then** 并发调用 `POST /auth/login`（Agent）和 `POST /api/auth/login`（Backend），两者均成功时将 agentToken、backendToken、backendRefreshToken 写入 AuthStore，随后跳转总览页。
2. **Given** Agent 登录成功但 Backend 登录失败，**When** 登录完成，**Then** 留在登录页，显示「Backend 连接失败」错误信息，不跳转，不写入 AuthStore。
3. **Given** Agent 登录失败（密码错误/账号停用），**When** 登录完成，**Then** 留在登录页，显示对应错误信息，不跳转，不写入 AuthStore。
4. **Given** 登录页，**When** 密码输入框处于焦点，**Then** 密码字符始终以掩码显示，不以明文呈现。
5. **Given** 已登录状态，**When** JWT 过期（8 小时后），**Then** 下次 API 调用返回 401 时，应用跳回登录页并清除本地 token。

---

### User Story 3 — 总览页：模块状态一览 (Priority: P1)

运维人员打开总览页，一眼看到 collector 和 writer 的运行状态、资源占用、运行时长，以及三个外部服务（Redis/MySQL/TDengine）的连接状态，并可直接对模块执行启动、停止、重启操作。

**Why this priority**: 总览页是日常运维的主界面，用于快速判断系统健康状态。

**Independent Test**: 打开应用能看到两个模块的实时状态卡片和控制按钮，即为独立可测试。

**Acceptance Scenarios**:

1. **Given** 总览页已加载，**When** 页面渲染完成，**Then** 显示 collector 和 writer 各一张概要卡片，卡片包含：状态 pill（running/stopped/error）、PID、CPU %、内存 MB、运行时长（HH:MM:SS 格式）。
2. **Given** 某模块处于 running 状态，**When** 用户点击「停止」，**Then** 调用 `POST /admin/processes/{name}/stop`，按钮显示 loading，操作完成后状态 pill 更新为 stopped；操作前弹出确认对话框。
3. **Given** 某模块处于 stopped 状态，**When** 用户点击「启动」，**Then** 调用 `POST /admin/processes/{name}/start`，按钮显示 loading，操作完成后状态 pill 更新为 running。
4. **Given** 某模块处于 running 状态，**When** 用户点击「重启」，**Then** 弹出确认对话框；**When** 用户确认，**Then** 调用 `POST /admin/processes/{name}/restart`，按钮显示 loading，操作完成后状态 pill 更新为 running；操作失败时显示错误提示，状态 pill 不被乐观更新。
5. **Given** 总览页，**When** 页面加载，**Then** 顶部右侧显示 Redis、MySQL、TDengine 三个连接状态点，并每 10 秒轮询 `GET /viewer/health` 更新；颜色语义为绿色=已连接、蓝色=检测中、红色=断开、灰色=未知。
6. **Given** 总览页已加载，**When** 页面显示，**Then** 采集点趋势图和入库点趋势图各一个，按 Backend module-rates 数据渲染折线/面积图；无数据时显示空态。
7. **Given** 总览页，**When** 用户点击某卡片的「日志」按钮，**Then** 跳转到对应模块详情页（Collector 页或 Writer 页），并自动聚焦日志面板（滚动到日志区域，短暂高亮面板边框）。

---

### User Story 4 — Collector 详情页 (Priority: P2)

运维人员进入 Collector 页，查看 collector 进程的完整信息：进程详情、采集指标、采集任务列表，以及实时滚动的日志流。

**Why this priority**: 是 collector 的专属运维界面，依赖登录和总览页的导航基础。

**Independent Test**: Collector 页能独立显示进程状态和实时日志，即为可测试。

**Acceptance Scenarios**:

1. **Given** Collector 页已加载，**When** 页面渲染，**Then** 顶部卡片显示：命令路径、工作目录、PID、运行时长、内存 MB、CPU %、状态 pill，以及启动/停止/重启/打开日志四个操作按钮；「打开日志」点击后滚动并聚焦到当前页面内实时日志面板，短暂高亮日志面板边框。
2. **Given** Collector 页已加载，**When** 页面渲染，**Then** 显示四个指标卡片：采集周期（ms）、设备连接数（在线/总数）、采集点位（总数）、Redis 写入速率（pts/min）。
3. **Given** Collector 页已加载，**When** 页面渲染，**Then** 采集任务表展示列：设备、分组、协议、周期、点位数、最新时间、质量；表格支持竖向滚动。
4. **Given** Collector 页日志面板，**When** 页面加载，**Then** 通过 `WS /viewer/logs/collector/tail` 建立 WebSocket 连接（握手时附带 Agent JWT），实时追加日志行；连接失败时显示错误提示。
5. **Given** 日志面板有新日志持续写入，**When** 用户关闭「自动滚动」开关，**Then** 日志停止自动滚动，用户可自由上下翻阅历史；重新打开开关后恢复自动滚动。
6. **Given** 日志面板，**When** 累计日志行数超过 5000 行，**Then** 自动丢弃最旧的行，保持最大 5000 行，避免内存溢出。
7. **Given** 日志面板每行，**When** 渲染，**Then** 显示：时间戳、级别 chip（INFO/WARN/ERROR 各自对应不同颜色）、消息文本；不含 Authorization 头或密码明文。

---

### User Story 5 — Writer 详情页 (Priority: P2)

运维人员进入 Writer 页，查看 writer 进程信息、Stream 消费指标、消费任务列表，以及实时日志。

**Why this priority**: 与 Collector 页对称，是 writer 的专属运维界面。

**Independent Test**: Writer 页能独立显示 Stream 消费状态和实时日志，即为可测试。

**Acceptance Scenarios**:

1. **Given** Writer 页已加载，**When** 页面渲染，**Then** 顶部卡片显示：命令路径、工作目录、PID、运行时长、内存 MB、CPU %、状态 pill，以及启动/停止/重启/打开日志按钮；「打开日志」点击后滚动并聚焦到当前页面内实时日志面板，短暂高亮日志面板边框。
2. **Given** Writer 页已加载，**When** 页面渲染，**Then** 显示四个指标卡片：Stream Lag（条）、PEL Pending（条）、批量写入速率（rows/min）、DLQ 积压量（条）。
3. **Given** Writer 页已加载，**When** 页面渲染，**Then** Stream 消费任务表展示列：Stream Key、Lag、PEL、ACK 状态；表格支持竖向滚动。
4. **Given** Writer 页日志面板，**When** 页面加载，**Then** 通过 `WS /viewer/logs/writer/tail` 建立 WebSocket 连接（握手时附带 Agent JWT），实时追加日志行。
5. **Given** 日志面板，**When** 累计日志行数超过 5000 行，**Then** 自动丢弃最旧的行，保持最大 5000 行。

---

### User Story 6 — 设置页：配置管理 (Priority: P2)

管理员进入设置页，修改 Redis/MySQL/TDengine 连接参数或 collector/writer 启动路径，保存后可选择立即重启对应模块使配置生效。

**Why this priority**: 现场部署时修改连接参数的核心运维操作。

**Independent Test**: 能修改并保存 Redis 配置，不重启模块，即为独立可测试。

**Acceptance Scenarios**:

1. **Given** 用户点击设置按钮，**When** 设置页打开，**Then** 主导航（总览/Collector/Writer）隐藏，设置页内部导航（基础/数据库/Collector/Writer）显示；顶部连接状态点隐藏。
2. **Given** 设置页「数据库」标签，**When** 渲染，**Then** Redis、MySQL、TDengine 各占一行配置卡片，每行包含：主机、端口、用户名、密码（掩码）、数据库名（如适用）、连接状态标签（初始显示「未知」灰色；点击「测试」后更新为绿色「已连接」或红色「断开」）、「测试」按钮；不自动轮询状态。
3. **Given** 某配置行点击「测试」，**When** 请求完成，**Then** 调用 Agent `POST /admin/health/check { target: "redis"|"mysql"|"tdengine" }`，先显示「测试中」状态，成功后更新该行状态为绿色「已连接」，失败时更新为红色「断开」并显示错误信息（不含密码明文）。
4. **Given** 修改了任意连接参数后点击「保存」，**When** 请求完成，**Then** 调用 `PUT /admin/config`，成功后弹出提示：「配置已保存。是否立即重启 Collector？」「是否立即重启 Writer？」两个独立确认选项。
5. **Given** 保存成功弹出重启确认，**When** 用户确认重启某模块，**Then** 调用对应的 `POST /admin/processes/{name}/restart`，操作结果反馈给用户。
6. **Given** 设置页「Collector」标签，**When** 渲染，**Then** 显示可执行文件路径、工作目录两个可编辑输入项，各附带「选择文件/目录」按钮。
7. **Given** 任何密码输入框，**When** 用户未主动编辑，**Then** 显示 `****` 占位符，不显示实际密码；用户输入新值时才覆盖。

---

### Edge Cases

- Agent 服务未启动时应用打开：显示「无法连接到 Agent (127.0.0.1:7070)」错误页，提供重试按钮，不崩溃。
- 模块启动后 10 秒内进程意外退出：状态卡片更新为 error 状态，显示「进程意外退出」提示，不自动重启（默认关闭自动重启）。
- 设置页密码字段：从 Agent 获取的配置中密码始终为 `****`；用户若不修改密码，保存时不传递密码字段（或传 `****` 由 Agent 忽略）。
- WebSocket 连接断开（网络抖动）：日志面板显示「连接已断开，正在重连…」，5 秒后自动重连，重连成功后继续追加日志。
- 5000 行日志上限触发：静默丢弃最旧行，不弹出警告，不影响当前显示的最新日志。
- 同时点击启动和停止：操作进行中按钮置灰，避免并发操作冲突。

---

## Requirements *(mandatory)*

### Functional Requirements

**入口流程**
- **FR-001**: 应用启动时必须先调用 `GET /bootstrap/status`，根据返回值决定显示 Bootstrap 向导页或登录页。
- **FR-002**: Bootstrap 向导页必须在测试连接通过后才允许点击「完成初始化」。
- **FR-003**: 登录必须同时向 Agent（`POST /auth/login`）和 Backend（`POST /api/auth/login`，返回 `data.token` / `data.refreshToken`）发起认证请求；**两者均成功才进入总览页**，任一失败则留在登录页显示错误，不写入 AuthStore。agentToken、backendToken、backendRefreshToken 分别存储于 AuthStore。

**运维操作**
- **FR-004**: 所有进程启动、停止、重启操作必须通过 Agent HTTP API 完成，禁止 Tauri Rust 层直接操控进程。
- **FR-005**: 进程控制操作必须在执行前弹出确认对话框（停止/重启），启动不需要确认。
- **FR-006**: 操作进行中相关按钮必须显示 loading 状态并禁用，操作完成后恢复。

**实时日志**
- **FR-007**: 日志 WebSocket 连接建立后，前端必须立即发送第一帧鉴权消息 `{ "type": "auth", "token": "<agentToken>" }`；Agent 验证通过后开始推流，验证失败则关闭连接；未鉴权连接必须被拒绝。
- **FR-008**: 日志面板最多保留 5000 行，超出后丢弃最旧行。
- **FR-009**: 日志行必须显示时间戳、级别 chip（颜色区分 DEBUG/INFO/WARN/ERROR）、消息文本。
- **FR-010**: 日志内容不得包含 Authorization 头、password 字段或任何密码明文。

**配置管理**
- **FR-011**: 密码字段在界面上始终以掩码显示；从 Agent 读取的配置密码固定返回 `****`。
- **FR-012**: 配置保存必须通过 `PUT /admin/config`，Tauri 不直接读写任何配置文件。
- **FR-013**: 保存配置后，根据模块运行状态给出不同提示：
  - 模块 **running**：显示「保存成功，是否重启 {模块名} 使配置生效？」，提供「重启」和「稍后手动重启」两个选项；设置页同时提供「保存并重启」主推按钮。
  - 模块 **stopped**：显示「保存成功，配置将在下次启动时生效。」，提供「立即启动」次要按钮。
  - 模块状态 **unknown**：显示「保存成功，无法确认模块状态，请到模块页手动重启。」

**导航与布局**
- **FR-014**: 设置页打开后必须隐藏主导航（总览/Collector/Writer）和连接状态点；关闭设置页后恢复。
- **FR-015**: 登录页和 Bootstrap 向导页不显示任何主导航或连接状态。

### Key Entities

- **ModuleStatus**: 模块运行状态快照，包含 name、status（running/stopped/crashed）、pid、uptime_seconds、memory_mb、cpu_percent、started_at、restart_count。来源：`GET /viewer/processes/{name}/metrics`。
- **AuthStore**: 前端登录态，包含 agentToken（Agent JWT）、backendToken（Backend access token）、backendRefreshToken（Backend refresh token）。**仅存于内存（React Context/state），不持久化到 localStorage、磁盘或任何配置文件**。应用关闭后重新打开须重新登录。
- **ConnectionConfig**: Agent 连接配置，包含 MySQL/Redis/TDengine 的 host、port、user、password、database 等字段。**密码 partial update 契约**：(1) `GET /admin/config` 不返回 password 字段（可缺失）；(2) `PUT /admin/config` 采用 partial update 语义——请求体**不包含** password key 表示保留原密码，**包含** password 表示用该值更新；(3) 前端**禁止**发送 `password: ""` 表达保留；(4) 本阶段不支持清空密码，若未来需要清空必须新增显式字段或独立接口。前端表单中密码框 password 缺失时显示为空，用户不输入则提交不带 password key。
- **LogLine**: 日志行，WebSocket 每条消息对应一行日志，统一 JSON 格式：
  ```json
  {
    "timestamp": "2026-05-25T10:26:10.123+08:00",
    "level": "INFO",
    "source": "collector",
    "message": "Redis stream append rt:stream:agv:main:0 count=256"
  }
  ```
  level 枚举：`DEBUG | INFO | WARN | ERROR`。前端收到纯文本或未知格式时降级为 `level=INFO, message=raw_text`。
  **`source` 字段渲染规则**：Collector 页和 Writer 页的日志面板**不渲染 source 列**（source 固定单一，展示无意义）；未来若实现全局日志页则显示 source chip。`source` 字段保留在数据结构中。
- **TrendChart**: 趋势图组件，必须支持四种状态：`empty`（无数据）、`loading`（加载中）、`error`（接口异常）、`ready`（正常渲染）。当前实现使用 Recharts 渲染折线/面积图；标题右侧显示当前值，右上角提供 1 分钟 / 5 分钟 / 10 分钟时间窗口切换。

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 首次启动应用（Agent 处于 bootstrap 状态）到完成 MySQL 配置进入登录页，全程在 2 分钟内完成，无需查阅文档。
- **SC-002**: 登录成功后，总览页在 3 秒内显示 collector 和 writer 的实时状态（含 PID、CPU、内存）。
- **SC-003**: 点击启动/停止/重启按钮到页面状态更新完成，响应时间不超过 10 秒。
- **SC-004**: 日志 WebSocket 连接建立后，日志行在 1 秒内出现在面板中（依赖 Agent 推送延迟）。
- **SC-005**: 在日志高频写入（>100 行/秒）场景下，前端页面不卡顿（帧率不低于 30fps），5000 行上限机制正常触发。
- **SC-006**: 密码明文不出现在：日志面板、浏览器控制台、任何网络请求的明文响应体中。
- **SC-007**: WebSocket 连接断开后 5 秒内自动重连，重连成功率 ≥ 95%（局域网环境）。

---

## Assumptions

- 本 spec 仅覆盖 Server Edition（管理员本机操作），不覆盖 Client Edition（远程只读监控）。
- 认证角色以 `sys_role` 表实际 `role_key` 值为准，开发前须查库确认。
- 应用运行在 Windows 10/11，Tauri WebView2 已安装。
- 不实现自动重启策略（默认关闭），不实现 JWT 黑名单（logout 清除本地 token 即可）。

## Clarification Decisions（已拍板，2026-05-25）

### Q1 — 前端与 Agent 并行开发，前端先用 Mock

前端和 Agent 并行开发。前端必须实现两层：

```
src/lib/agent-client.ts   真实 Agent HTTP API client（生产使用）
src/lib/mock-agent.ts     mock 数据（开发期默认启用）
```

开发期通过环境变量（`VITE_USE_MOCK=true`）切换 mock/真实，不修改业务代码。
mock 必须覆盖以下全部端点：

```
GET  /bootstrap/status
PUT  /bootstrap/config
POST /bootstrap/test-connection
POST /bootstrap/finish
POST /auth/login                    （Agent JWT 登录）
POST /api/auth/login                （Backend token 登录，返回 data.token / data.refreshToken）
GET  /viewer/status
GET  /viewer/processes/{name}/metrics
GET  /viewer/processes/{name}/status
POST /admin/processes/{name}/start
POST /admin/processes/{name}/stop
POST /admin/processes/{name}/restart
GET  /admin/config
PUT  /admin/config
POST /admin/health/check            （设置页单项测试）
GET  /viewer/health                 （顶部连接状态点，10 秒轮询）
WS   /viewer/logs/{name}/tail
```

### Q2 — Token 仅内存保存，401 驱动过期跳转

- `AuthStore`（agentToken、backendToken、backendRefreshToken）存于 React Context，**不落盘、不写 localStorage**。
- 应用关闭后重新打开须重新登录，这是预期行为。
- 过期主策略：API 返回 401 → 清除 AuthStore → 跳转登录页。
- 过期辅策略（非必须）：前端可解析 JWT `exp` 字段提前展示过期倒计时提示，但不作为 MVP 强制项。
- Backend token / refreshToken 续签策略：MVP 不实现自动续签，401 统一跳登录页。

### Q3 — 趋势图接入 module-rates，组件支持四态

- 趋势图卡片不隐藏，`TrendChart` 组件必须支持 `empty | loading | error | ready` 四种状态。
- `ready` 状态使用 Backend module-rates 数据源渲染 Recharts 折线/面积图。
- 当前值显示在标题右侧，右上角提供 1 分钟 / 5 分钟 / 10 分钟时间窗口切换。
- 无数据或接口不可用时分别进入 `empty` / `error` 状态，不显示硬编码假数据。

### Q4 — 顶部健康状态 10 秒轮询

- 顶部 Redis/MySQL/TDengine 状态点调用：
  ```
  GET  /viewer/health               返回三个服务的连接状态（用于总览状态点）
  ```
- 轮询策略：每 10 秒一次，窗口隐藏时暂停，恢复可见时立即刷新。
- 颜色语义：绿色 `connected`，蓝色 `checking`，红色 `disconnected`，灰色 `unknown`。
- 设置页「测试」按钮仍使用：
  ```
  POST /admin/health/check          { target: "redis"|"mysql"|"tdengine" }
  ```
  该接口是手动测试，不自动轮询。

### Q5 — 日志 WS 单行 JSON，格式已定

每条 WebSocket message 对应一行日志，JSON 格式：

```json
{
  "timestamp": "2026-05-25T10:26:10.123+08:00",
  "level": "INFO",
  "source": "collector",
  "message": "Redis stream append rt:stream:agv:main:0 count=256"
}
```

- `level` 枚举：`DEBUG | INFO | WARN | ERROR`
- 一条 WS message = 一行日志，不批量
- 前端收到纯文本或解析失败时降级：`level=INFO, message=raw_text`

### Q6 — 保存配置后重启提示按模块状态区分

| 模块状态 | 保存后提示 | 可选操作 |
|----------|-----------|---------|
| running | 「保存成功，是否重启 {模块} 使配置生效？」 | 「重启」/ 「稍后手动」 |
| stopped | 「保存成功，配置将在下次启动时生效。」 | 「立即启动」（次要按钮） |
| unknown | 「保存成功，无法确认模块状态，请到模块页手动操作。」 | 无操作按钮 |

设置页按钮布局：「保存」始终可用；「保存并重启 Collector」/ 「保存并重启 Writer」仅在对应模块 running 时显示为主推按钮。

### Q8 — Backend 登录接口定义（GAP-E 解决，2026-05-25）

Backend 登录接口固定为：
```
POST /api/auth/login
Request:  { userName: string, password: string }
Response: { code: 200, msg: "success", data: { token: string, refreshToken: string } }
```

- `data.token` → `AuthStore.backendToken`
- `data.refreshToken` → `AuthStore.backendRefreshToken`
- Agent JWT（`POST /auth/login` 返回）→ `AuthStore.agentToken`
- **两者均成功才进入总览页**；任一失败留在登录页，不写入 AuthStore
- Mock 必须同时覆盖 `POST /auth/login`（Agent）和 `POST /api/auth/login`（Backend）

### Q9 — 设置页入口与关闭方式（GAP-G 解决，2026-05-25）

**入口**：普通页面（总览/Collector/Writer）顶部右上角显示齿轮图标按钮，点击进入设置页。

**进入设置页后**：
- 隐藏左侧主导航（总览/Collector/Writer）和顶部连接状态点
- 显示左上角返回按钮、「设置」标题、设置内部标签导航（基础/数据库/Collector/Writer）

**关闭设置页**：
- 点击左上角返回按钮，或按 Esc 键
- 返回进入设置页前的页面；若来源不明则默认返回总览页
- **不允许**通过点击普通主导航关闭设置页（设置页内主导航不显示，无此交互路径）

### Q7 — Bootstrap 只配置 MySQL，Redis/TDengine 登录后在设置页补

- Bootstrap 向导页**只填写 MySQL 连接参数**（host、port、user、password、database）。
- 完成条件（必须按序执行）：
  1. `PUT /bootstrap/config`（写入 MySQL 配置）
  2. `POST /bootstrap/test-connection { target: "mysql" }`（测试成功）
  3. `POST /bootstrap/finish`（初始化完成）
  4. `GET /bootstrap/status` 返回 `{ in_bootstrap: false }`
- Bootstrap 完成后跳转登录页；总览页顶部 Redis/TDengine 状态点此时可能显示 unknown，引导用户去设置页配置。
- **不要求 Redis/TDengine 在 Bootstrap 阶段可用**。
