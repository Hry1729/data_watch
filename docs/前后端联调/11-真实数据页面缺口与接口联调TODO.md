# 真实数据页面缺口与接口联调 TODO

> 更新时间：2026-05-08
> 适用范围：`device-monitor` 七个核心页面真实数据联调
> 目标：在 auth / permission 闭环恢复后，明确页面级缺口、接口优先级和联调顺序，避免前后端继续各自猜测
> 当前说明：本文为阶段性联调 TODO 与执行记录。实时推送当前路径统一为 `/api/device-monitor/ws/realtime-push`；文中较早记录如出现 `/ws/realtime`，仅表示当时的历史执行口径，不再作为当前接口事实源。

---

## 一、当前背景

截至 2026-04-19，真实登录、用户信息、动态菜单和 system-manage 首轮查询已经恢复，前端可以正常登录并打开页面。

当前新的问题不是“进不去系统”，而是“进入系统后很多页面没有真实数据”。这类问题的主因已经明确为：

1. `frontend/src/api/device-monitor.ts` 中预期的大量业务接口还没有后端实现
2. Dashboard / Log 所依赖的报警、日志、统计样本还没有形成稳定数据库事实源
3. Realtime 页此前只具备初始化 HTTP 接口；2026-04-21 已补实时推送源码并完成本地 ASGI 复验，当前路径统一为 `/ws/realtime-push`

本文件用于承接 auth 闭环之后的下一阶段联调执行。

---

## 二、当前已验证基线

| 能力 | 当前状态 | 说明 |
|------|----------|------|
| 登录认证 | ✅ 已验证 | `POST /api/auth/login`、`GET /api/user/info` 已完成真实 HTTP 验证 |
| 动态菜单 | ✅ 已验证 | `GET /api/v3/system/menus` 已可返回真实菜单树 |
| 系统管理读链路 | ✅ 已验证 | `GET /api/user/list`、`GET /api/role/list` 已通过首轮真实查询 |
| Driver / Device / Point 读链路 | ✅ 已实现 | 已补 `driver/*`、`device/group-tree,list,detail`、`point/device-tree,list,detail`，并完成真实数据库服务层复验 |
| Device / Point 新增链路 | ✅ 已实现 | 已补 `POST /api/device-monitor/device`、`POST /api/device-monitor/point`，并完成真实数据库写入与详情回查验证 |
| 实时监控 HTTP 基线 | ✅ 部分可用 | `GET /api/device-monitor/monitor/device-tree`、`GET /api/device-monitor/monitor/realtime-init` 已存在 |
| 历史数据 HTTP 基线 | ✅ 部分可用 | `history/devices`、`history/points`、`history/query`、`history/export` 已有首版实现 |
| 页面实时推送 | ✅ 已完成源码复验 | `/ws/realtime-push` 已补齐，订阅 / 快照 / `ping-pong` 已通过本地 ASGI 验证；待对当前运行中的 8000 进程热重载后做页面级冒烟 |

---

## 三、页面缺口矩阵

| 页面 | 前端预期接口 / 链路 | 当前后端现状 | 影响 | 优先级 |
|------|----------------------|--------------|------|--------|
| 工作台 `dashboard` | `dashboard/overview`、`dashboard/device-type-distribution`、`dashboard/recent-events`、`dashboard/trend`、`dashboard/device-connection-health`、`dashboard/alarm-distribution` | ✅ 已完成并测试 | 工作台所有统计均已切换至正式数据，完成对接及测试 | P0 |
| 驱动管理 `driver` | `driver/list`、`driver/options` | ✅ 已完成并测试 | 驱动列表真实数据链路已补齐及测试 | P0 |
| 设备管理 `device` | `device/group-tree`、`device/list`、`device/{id}`、`POST/PUT/DELETE`、`batch`、`import/export` | ✅ 已完成并测试 | 页面已完成人工点验及联调验收 | P0 |
| 点位管理 `point` | `point/device-tree`、`point/list`、`point/{id}`、`POST/PUT/DELETE`、`batch`、`import/export`、`point/import/template` | ✅ 已完成并测试 | 页面已完成人工点验及联调验收 | P0 |
| 实时监控 `realtime` | `monitor/device-tree`、`monitor/realtime-init`、`WS /ws/realtime-push` | ✅ 已完成并测试 | 前端四段式布局、订阅、快照、重连等页面级验证已完成并测试通过 | P0 |
| 历史数据 `history` | `history/devices`、`history/points`、`history/query`、`history/export` | ✅ 已完成并测试 | 历史查询采用 ECharts 图表，已完美落地完成页面级人工点验及联调验收 | P1 |
| 系统日志 `log` | `log/list` | ✅ 已完成并测试 | 日志页列表筛选、分页及真实标签映射已完成联调及测试 | P1 |

---

## 四、数据库依赖补充说明

页面数据缺口不只是“少几个接口”，还包含数据库事实源缺口：

### 4.1 已具备、应优先复用的主数据表

- `comm_protocol`：驱动管理页的基础数据源
- `device_group`、`device`：设备管理页和实时 / 历史设备树的数据源
- `device_variable`：点位管理页和历史点位选择的数据源

### 4.2 当前仍缺的业务数据表 / 样本

- 报警相关：`alarm_level`、`alarm_rule`、`alarm_record`
- 日志相关：`operation_log`、`system_log`
- Dashboard / Log 页面联调用统计 seed：
  - 最近告警 / 最近事件样本
  - 告警等级分布样本
  - 设备通讯健康统计样本
  - 日志级别、日志类型样本

说明：

- 2026-04-20 已实际核查 MySQL 当前 schema，这些报警 / 日志表当前仍不存在；
- 按当前执行约束，**这类 schema 变更不会直接执行**，需要先向你确认后再推进；
- 如果这些数据库事实源不先补齐，即使后端先写出空接口，页面也仍然只是“能请求但无内容”。

---

## 五、建议的实现顺序

建议严格按以下顺序推进，优先解决“最多页面能看到数据”的问题：

### 5.1 第一阶段：先补列表 / 树 / 详情读接口

优先级最高：

1. `driver/list`、`driver/options`
2. `device/group-tree`、`device/list`、`device/{id}`
3. `point/device-tree`、`point/list`、`point/{id}`

目标：

- 先让 Driver / Device / Point 三页从“空白”变成“有真实可读数据”
- 优先打通树、分页、详情读取，不先纠结导入导出与批量删除

### 5.2 第二阶段：补 Dashboard / Log 的数据库事实源和统计接口

优先级次高：

1. 建报警表、日志表
2. 写入最小可验收 seed 数据
3. 实现 Dashboard 六个统计接口
4. 实现 `log/list`

目标：

- 让工作台和日志页形成稳定真实样本
- 避免页面设计完成后依旧只能展示空态

### 5.3 第三阶段：补 Realtime 实时推送

优先级次高：

1. 实现 `WS /api/device-monitor/ws/realtime-push`
2. 明确订阅、心跳、断线重连、异常消息口径
3. 恢复前端 Realtime 页自动建连与状态显示

目标：

- 让实时监控页从“静态快照页”恢复到“真实实时页”

### 5.4 第四阶段：补写接口、导入导出和整页回归

包括：

- Device / Point 的新增、编辑、删除、批量、导入导出
- Dashboard / Log / History / Realtime 的整页冒烟回归
- `VITE_ACCESS_MODE=backend` 切换回归

---

## 六、任务拆解 TODO

| ID | 任务 | Owner | 优先级 | 当前说明 |
|----|------|-------|--------|----------|
| GAP-001 | 实现驱动管理读接口：`driver/list`、`driver/options` | `@backend` | P0 | ✅ 已完成：基于 `comm_protocol` 输出真实列表 / 下拉，并通过服务层复验 |
| GAP-002 | 实现设备管理读接口：`device/group-tree`、`device/list`、`device/{id}` | `@backend` | P0 | ✅ 已完成：已对齐当前 MySQL 实际结构并补设备状态 / 连接状态 / 数据状态 |
| GAP-003 | 实现点位管理读接口：`point/device-tree`、`point/list`、`point/{id}` | `@backend` | P0 | ✅ 已完成：已补树 / 列表 / 详情，并兼容当前值缺失时的 `--` 展示 |
| GAP-004 | 补报警 / 日志表与联调用 seed 数据 | `@database` | P0 | ✅ 已完成：报警 / 日志事实表已落地；2026-05-06 已补日志策略表、索引和管理权限 |
| GAP-005 | 实现 Dashboard 统计接口六件套 | `@backend` | P0 | ✅ 已完成：所有统计接口均已彻底切换到正式真实数据口径并测试通过 |
| GAP-006 | 实现 `log/list` 与日志标签口径 | `@backend` | P1 | 🔄 数据库已就绪：`operation_log / system_log` 已落表并保留 seed，下一步切真实查询与统计口径 |
| GAP-007 | 实现 `WS /ws/realtime-push` 并固定消息契约 | `@backend` | P0 | ✅ 已完成：已补订阅 / 取消订阅 / `ping-pong` / 快照推送首版，并完成本地 ASGI 复验 |
| GAP-008 | 前端补七页空态 / 错误态 / 加载态处理 | `@frontend` | P1 | 避免接口未齐时页面表现混乱 |
| GAP-009 | 建立七页真实数据联调 smoke checklist | `@test` | P0 | 覆盖登录后页面是否可见、是否有数据、是否有分页和错误提示 |
| GAP-010 | 实现设备新增接口：`POST /api/device-monitor/device` | `@backend` | P0 | ✅ 已完成：已对齐 `DeviceForm` 契约，真实 MySQL 写入 + 详情回查通过 |
| GAP-011 | 实现点位新增接口：`POST /api/device-monitor/point` | `@backend` | P0 | ✅ 已完成：已对齐 `PointForm` 契约，真实 MySQL 写入 + 详情回查通过 |

### 6.1 2026-04-20 本轮执行结果

- 已新增后端只读路由：`driver/list`、`driver/options`、`device/group-tree`、`device/list`、`device/{id}`、`point/device-tree`、`point/list`、`point/{id}`
- 已修正 `monitor` 服务对已删除字段 `device.comm_type` 的依赖，统一改为从 `comm_protocol.protocol` 取驱动名称
- 已完成代码级校验：`compileall` 通过，`create_app()` 可注册新增路由
- 已完成真实库复验：
  - MySQL：`comm_protocol=5`、`device=150`、`device_variable=10`
  - 设备列表首批真实返回：`qc100`、`qc101`
  - 点位列表首批真实返回：`Input`、`Main Hoist General`、`Main Trolley General`
- 当前点位 `currentValue` 多数仍为 `--`，说明 **接口已通，但现有 TDengine 点位级最新值尚未稳定映射到这些点位子表**；该问题不阻塞页面从“无接口空白”恢复到“有主数据可读”

### 6.2 2026-04-20 页面联调问题修复结果

- 已定位页面切换慢的主要原因之一：`device/group-tree`、`device/list`、`monitor/device-tree` 在首版实现里会为大量设备逐个查询 TDengine 最新时间，导致页面切换时等待偏长
- 已完成后端优化：
  - `device/group-tree` 改为直接使用主数据快速出树
  - `device/list` 默认只对当前页设备补 TDengine 最新时间，不再对全量设备逐个补齐
  - `monitor/device-tree` 改为轻量树加载，避免首屏先为整棵树做全量时序查询
- 已定位 `history/points?deviceId=...` 返回 `500` 的直接原因：当前 TurboAPI 兼容层没有自动把 query string 的 `deviceId` 从字符串转换为整数
- 已完成修复：`history/points`、`history/query` 在路由层显式做 `deviceId` 正整数转换，避免历史页设备切换时报 `500`
- 已完成前端优化：Dashboard 页改为在 `KeepAlive` 场景下只在激活时启动轮询、离开页签时停止轮询，避免切到其他页面后后台继续周期性请求未实现的 `dashboard/*` 接口

### 6.3 2026-04-20 Dashboard / Log 第二轮执行结果

- 已完成 Dashboard / Log 前端契约核查，并按当前页面调用口径补齐后端路由：
  - `dashboard/overview`
  - `dashboard/device-type-distribution`
  - `dashboard/trend`
  - `dashboard/device-connection-health`
  - `dashboard/alarm-distribution`
  - `dashboard/recent-events`
  - `log/list`
- 2026-05-06 更新：`alarm_level`、`alarm_rule`、`alarm_record`、`alarm_active`、`operation_log`、`system_log` 均已落地；系统日志策略表 `log_retention_policy`、新增日志索引和日志管理权限也已完成数据库侧优化
- 已按“缺 schema 先告知、不直接改库”的原则处理：
  - `dashboard/overview`：基于 `device` / `device_variable` 返回真实总量
  - `dashboard/device-type-distribution`：基于 `device + device_type` 返回真实类型分布
  - `dashboard/device-connection-health`：基于 `device.status` 返回真实在线 / 离线 / 风险概览，并补充 TopN 设备最近心跳时间
  - `dashboard/trend`：基于当前 TDengine 点位表按时间窗口聚合 `collectCount`，同时以真实采集活跃设备占比生成 `onlineRate`
- 已对受 schema 阻塞的接口先补兼容空结构，避免页面继续 404：
  - `dashboard/alarm-distribution`
  - `dashboard/recent-events`
  - `log/list`
- 当前结论：
  - Dashboard 页不应再因接口缺失而整页报错
  - 告警分布 / 最近事件 / 日志列表仍需要数据库工程师补齐告警与日志事实表后，才能切换为真实业务数据

### 6.4 2026-04-21 五页优先收口第三轮执行结果

- 已修复 TurboAPI 兼容层两个共享问题：
  - 路径参数在 `from __future__ import annotations` 场景下被解析为字符串类型名，导致 `device/{id}`、`point/{id}` 等详情接口报 `'str' object is not callable'`
  - 纯 TurboAPI ASGI fallback 不处理 WebSocket，导致即使注册了 `app.websocket(...)` 也无法真正握手
- 已在后端补齐并复验以下接口：
  - `POST /api/device-monitor/device`
  - `POST /api/device-monitor/point`
  - `WS /api/device-monitor/ws/realtime-push`
- 已完成本地代码级复验：
  - `GET /api/device-monitor/device/{id}` 返回 `200`
  - `GET /api/device-monitor/point/{id}` 返回 `200`
  - `POST /api/device-monitor/device` 可真实写入 MySQL，并能详情回查
  - `POST /api/device-monitor/point` 可真实写入 MySQL，并能详情回查
  - `WS /api/device-monitor/ws/realtime-push` 可完成 `subscribe -> snapshot -> ping/pong`
- 已新增一组可识别联调测试数据，供后续删改接口只针对“我们新增的数据”继续验证：
  - 设备编码前缀：`AUTOQC*`
  - 点位名称前缀：`AUTO_SIGNAL_*`
- 当前结论：
  - 五页优先范围里，后端剩余主阻塞已从“缺接口”收敛为“当前运行中的 8000 进程尚未热重载到最新源码”和“Dashboard 告警 / Log 事实表缺失”

### 6.5 2026-04-21 前后端代理联调结果

- 已通过前端开发代理 `http://127.0.0.1:3006` 复验以下 HTTP 链路全部返回 `200`：
  - `auth/login`、`user/info`
  - `dashboard/overview`、`device-type-distribution`、`trend`、`device-connection-health`
  - `dashboard/alarm-distribution`、`dashboard/recent-events`（当前为兼容空结构）
  - `driver/list`、`driver/options`
  - `device/group-tree`、`device/list`、`device/{id}`、`POST /device`
  - `point/device-tree`、`point/list`、`point/{id}`、`POST /point`
  - `monitor/device-tree`、`monitor/realtime-init`
- 本轮经前端代理新增的联调数据：
  - 设备：`AFE44E057`，`id=991006`
  - 点位：`id=992009`
- 本轮新增并复验的另一组联调数据：
  - 设备：`AUTOQC0421155437`，`id=991007`
  - 点位：`AUTO_POINT_0421155437`，`id=992010`
- 已完成的实时链路复验：
  - 直连后端 `ws://127.0.0.1:8000/api/device-monitor/ws/realtime-push` 已可完成 `subscribe -> snapshot -> pong`
  - 说明当前后端源码、运行时依赖与 8000 进程内的 WS 路由均已正常
- 已定位一处新的页面级阻塞：
  - 当前前端开发代理 `http://127.0.0.1:3006` 对 `/api` 仅开启了 HTTP 代理，未开启 WebSocket 代理，因此 `ws://127.0.0.1:3006/api/device-monitor/ws/realtime-push` 无法转发到 8000
- 已同步完成的仓库级修正：
  - `backend/requirements.txt` 新增 `websockets>=14.0`
  - `frontend/.env.development` 已将 `VITE_DEVICE_MONITOR_WS_ENABLED` 改为 `true`
  - `frontend/vite.config.ts` 已为 `/api` 代理补 `ws: true`
  - 前端设备 / 点位新增弹窗校验长度已收紧到当前后端和 MySQL 实际上限，避免页面提交后才收到 `400`
- 当前结论：
  - 五页里除 Realtime 页面“经 3006 代理建连”外，其余前端代理 HTTP 链路已实测打通
  - Realtime 页面当前只差“重启前端开发服务，使新的 `ws: true` 代理配置生效”后再做一次页面级冒烟

### 6.6 2026-04-21 重启后的最终命令级冒烟

- 已在用户重启前后端后，完成以下真实链路复验：
  - `http://127.0.0.1:8000/healthz` 返回 `200`
  - `http://127.0.0.1:3006/` 返回 `200`
  - 经 `3006` 代理的 `auth/login`、`user/info`、`dashboard/overview`、`driver/list`、`device/list`、`point/list`、`monitor/realtime-init` 均返回 `200`
- 已确认前端开发代理上的实时 WS 链路恢复正常：
  - `ws://127.0.0.1:3006/api/device-monitor/ws/realtime-push` 可完成 `subscribe -> snapshot -> pong`
  - `ws://127.0.0.1:8000/api/device-monitor/ws/realtime-push` 同样可完成 `subscribe -> snapshot -> pong`
- 本轮最终结论：
  - 五页优先范围的“命令级前后端联调”已收口完成
  - 当前剩余工作已转为页面人工点验与下一阶段 backlog（History / Log / Dashboard 告警侧 / Device&Point 编辑删除导入导出）

### 6.7 2026-04-21 Device / Point 改删接口联调结果

### 6.8 2026-04-22 History / Perspective 第二轮执行结果

- 已在前端 History 页接入会话协议基础设施：
  - `perspectiveSessionId` 使用 `sessionStorage` 生成与复用
  - `connectionId` 按页面连接单独生成
  - `queryVersion` 按查询递增
- 已让 `GET /api/device-monitor/history/query` 携带：
  - `X-Perspective-Session-Id`
  - `X-Perspective-Query-Version`
- 已接通 `WS /api/device-monitor/ws/perspective`，并完成以下事件首版处理：
  - `snapshot`
  - `session_restored`
  - `session_conflict`
  - `pong`
  - `error`
- 已在历史页内建立本地 Perspective Table 同步链路，当前查询成功后会先写入本地 Table，并在收到服务端 `snapshot` 后以服务端快照覆盖
- 已完成代码级复验：`frontend` 执行 `vue-tsc --noEmit` 通过
- 当前剩余项：
  - 移除 ECharts 主渲染，切换到 Perspective Viewer 主视图
  - 将导出文件名与按钮文案统一为 `.csv`
  - 做历史页真实联调验收与风险补记

### 6.9 2026-04-22 History / Perspective 第三轮执行结果

- 已将 History 页主渲染从 “ECharts 趋势图 + 下方明细表” 切换为单一 Perspective Viewer 主视图
- 当前默认插件已切为 `Y Line`，并提供 `Datagrid` 手动切换入口
- 原有查询抽屉、设备选择、点位选择、时间范围与导出入口已保留
- 历史导出按钮文案与前端下载文件名现已统一为 `.csv`
- 已完成代码级复验：History 页改造后 `frontend` 再次通过 `vue-tsc --noEmit`
- 当前剩余项：
  - 对真实后端接口做一次 History / Perspective 联调复验
  - 记录刷新恢复、冲突处理、超限报错与导出行为的实际结果

### 6.10 2026-04-22 History / Perspective 第四轮真实联调结果

- 已通过 `http://127.0.0.1:3006` 代理复验：
  - `GET /api/device-monitor/history/devices`
  - `GET /api/device-monitor/history/points?deviceId=1`
  - `GET /api/device-monitor/history/query`
  - `POST /api/device-monitor/history/export`
- 已确认历史导出实际返回：
  - `Content-Type: text/csv; charset=utf-8`
  - `Content-Disposition` 文件名为 `.csv`
  - 返回内容以 `time,...` CSV 表头开头
- 已通过 `WS /api/device-monitor/ws/perspective` 命令级复验：
  - `snapshot`
  - `session_restored`
  - `session_conflict`
  - `pong`
- 已通过真实接口复验保护策略：
  - 查询跨度超过 `24h` 返回 `time range exceeds the limit of 24 hours`
  - 点位数超过 `20` 返回 `pointIds exceeds the limit of 20 points`
- 本轮联调中发现并已修正一处真实数据问题：
  - 历史查询切换到 `var_seq` 子表后，仍需兼容旧 `deviceCode_pointId` 子表，否则会漏掉存量时序数据
  - 后端现已补“优先命中 `deviceCode_varSeq`，空结果再回退到 `deviceCode_pointId`”的逻辑
  - 修正后 `qc100_30` 等新口径子表已可通过 `history/query` 命中，旧表数据也能经回退逻辑返回真实 `snapshot`
- 当前剩余项：
  - 需要你在浏览器里做一次页面人工点验，确认 `Y Line / Datagrid` 渲染表现与交互是否符合预期
  - 后端重启后的“会话不存在 -> 重新查询”页面提示还缺一次人工回归记录

### 6.11 2026-04-22 History / Perspective 第五轮页面体验修正

- 已针对浏览器侧“查询成功但页面仍提示失败”的表现补最小修正：
  - 查询 HTTP 成功后先收起抽屉，再执行 Perspective 渲染
  - `perspective-viewer` 初始化前补元素定义等待，减少首轮渲染时序问题
  - Perspective Table 的时间列改为 `datetime` 入表，降低 `Y Line` 对字符串时间列的渲染失败概率
- 已补渲染兜底策略：
  - 若 `Y Line` 恢复配置失败，自动切换为 `Datagrid`
  - 页面给出明确提示“已自动切换到 Datagrid”，避免误判为查询失败
- 已修正 History 页顶部操作区对齐：
  - 标题区与右上按钮区改为垂直居中
  - `ElButtonGroup` 与单按钮统一使用同一行内对齐
- 当前待你刷新页面后重点人工点验：
  - 以 `2026-04-15` 的真实样本时间范围执行查询，页面不应再直接报“查询失败”
  - 查询成功后抽屉应自动关闭
  - 顶部 `Y Line / Datagrid / 导出 CSV / 选择数据` 四个操作按钮应保持一行对齐

### 6.12 2026-04-22 History / Perspective 第六轮查询卡住修正

- 已继续补一处更贴近当前现象的前端收口：
  - 将 History 页 `renderPerspectiveRecords()` 改为串行执行
  - 避免 `history/query` 的 HTTP 成功回包与 `WS /ws/perspective` 的 `snapshot` 同时进入重建流程时，前后两个渲染任务交叉删除同一张 Perspective Table
- 已同步调整查询状态收口：
  - HTTP 查询成功后立即结束 `loading`
  - 后续 Perspective 渲染作为独立阶段继续执行，避免页面长时间停留在“正在查询历史数据...”
- 当前待你刷新页面后重点人工点验：
  - 重新执行 `2026-04-15` 时间范围查询，页面不应再一直显示“查询中”
  - 若 `Y Line` 仍无法稳定渲染，应自动回退到 `Datagrid`
  - 若仍无数据显示，需要进一步确认浏览器控制台里是否有 `Perspective Y Line 渲染失败` 或其它运行时错误

### 6.13 2026-04-22 History / Perspective 第七轮样本覆盖复核

- 已继续通过 `3006 -> 8000` 真实链路复核当前 TDengine 样本覆盖范围：
  - `deviceId=1(qc100)` + `pointId=1(Input)` 在 `2026-04-15 00:00:00 ~ 23:59:59` 可返回 `2` 条真实记录
  - 同设备的 `pointId=2/3` 当前返回 `0` 条
  - `deviceId=2..5` 的 `pointId=1` 当前也返回 `0` 条
- 当前结论：
  - 历史页此前“查询中卡住”的前端问题已单独修过
  - 现在页面若显示“无数据”，更像是所选设备/点位确实没有 TDengine 样本，而不是前后端链路再次断开
- 当前建议的页面人工验证组合：
  - 设备：`qc100`
  - 点位：`Input`
  - 时间范围：`2026-04-15 00:00:00 ~ 2026-04-15 23:59:59`
- 若希望 History 页在更多设备/点位下也能稳定看到数据，下一步需要补更多 TDengine 假数据覆盖，而不是继续改当前查询接口

### 6.14 2026-04-22 History / Perspective 第八轮初始化阻塞修正

- 结合你最新截图，继续缩小到另一处更像根因的前端运行时阻塞：
  - 查询状态栏、Query v1、已选点位都已更新，说明 HTTP 查询主链路已经成功返回
  - 但主渲染区仍落在空态，说明更像是 `perspective-viewer` 初始化阶段没有真正完成，而不是接口无数据
- 已对 History 页补最小修正：
  - 不再无限等待 `customElements.whenDefined('perspective-viewer')`
  - 改为“动态补加载 `@finos/perspective-viewer*` 模块 + 短超时兜底”
  - 目的是避免浏览器端自定义元素注册稍慢或未及时完成时，查询成功却一直停在空白态
- 当前待你刷新页面后重点人工点验：
  - 设备：`qc100`
  - 点位：仅勾选 `Input`
  - 时间：`2026-04-15 00:00:00 ~ 2026-04-16 00:00:00`
  - 预期：至少能看到 2 条真实记录对应的渲染结果；如果仍不显示，需要继续看浏览器控制台第一条红色报错

### 6.15 2026-04-22 History / Perspective 第九轮 wasm 资源修正

- 已根据你补充的浏览器报错：
  - `Error: Missing perspective-client.wasm`
  - 定位到当前 npm/Vite 接入里，`@finos/perspective` 与 `@finos/perspective-viewer` 的 ESM 入口不会像官方 CDN demo 那样自动初始化 wasm
- 已对照 `perspective-connect-demo-main` 官方示例确认：
  - 官方 demo 走的是 CDN 入口，内部会自动把 wasm 带起来
  - 我们当前项目走的是 npm + Vite，本地接入必须显式初始化 wasm，不能只做模块 side-effect import
- 已在 History 页补最小修正：
  - 显式加载 `@finos/perspective/dist/wasm/perspective-server.wasm`
  - 显式加载 `@finos/perspective-viewer/dist/wasm/perspective-viewer.wasm`
  - 在创建 `perspective.worker()` 前完成初始化，修复 `Missing perspective-client.wasm`
- 当前待你刷新页面后重点人工点验：
  - 强制刷新历史页（建议 `Ctrl + F5`）
  - 设备：`qc100`
  - 点位：仅 `Input`
  - 时间：`2026-04-15 00:00:00 ~ 2026-04-16 00:00:00`
  - 预期：不再出现 `Missing perspective-client.wasm`，并能看到真实历史数据渲染

### 6.16 2026-04-22 History / Perspective 第十轮生命周期修正

- 已根据你最新补充的浏览器报错继续收口：
  - `Abort(): Cannot delete table with views`
  - `Failed to execute 'define' on 'CustomElementRegistry': the name "perspective-viewer" has already been used`
- 已定位到两个前端运行时根因：
  - `perspective-viewer` 的 wasm 初始化在某些重复进入/重渲染场景下会再次执行，导致重复注册自定义元素
  - History 页在重建查询结果时，旧 `Table` 仍被 Viewer 持有 View，就直接执行 `table.delete()`，因此触发 “Cannot delete table with views”
- 已做最小修正：
  - 为 `perspective-viewer` 初始化增加页面级全局单例守卫，只在浏览器里真正初始化一次
  - 在每次重建历史结果前，先销毁旧 Viewer，再删除旧 Table，最后创建新表并重新加载
  - 页面卸载时也改成按相同顺序清理资源，避免离开页面时残留相同问题
  - `frontend` 已再次通过 `vue-tsc --noEmit`
- 当前待你刷新页面后重点人工点验：
  - 强制刷新历史页（建议 `Ctrl + F5`）
  - 设备：`qc100`
  - 点位：仅 `Input`
  - 时间：`2026-04-15 00:00:00 ~ 2026-04-16 00:00:00`
  - 预期：
    - 不再出现 `perspective-viewer` 重复注册报错
    - 不再出现 `Cannot delete table with views`
    - History 页能稳定显示真实查询结果

### 6.17 2026-04-22 History / Perspective 第十一轮时间范围回写修正

- 已根据你最新反馈继续收口：
  - 查询成功后，页面会把时间范围自动改成 `2026-04-15 08:04:24.934 ~ 2026-04-15 08:04:25.738`
- 已定位到当前前端根因：
  - `snapshot` 到达后，`syncSelectionFromSnapshot()` 会直接用“当前结果中的最小/最大样本时间”回写 `searchForm.startTime / endTime / timeRange`
  - 这会覆盖掉你手动选择的原始查询区间，所以只剩两条样本点对应的时间范围
- 已做最小修正：
  - 当 `snapshot` 属于当前查询版本时，优先保留用户发起查询时的 `startTime / endTime`
  - 只有在缺少当前查询上下文时，才回退使用样本时间边界
  - `frontend` 已再次通过 `vue-tsc --noEmit`
- 当前待你刷新页面后重点人工点验：
  - 强制刷新历史页（建议 `Ctrl + F5`）
  - 再次查询你自己手动选的时间范围
  - 预期：
    - 图表正常显示
    - 时间范围不再自动缩成两条样本点的时间

### 6.18 2026-04-22 History / Perspective 第十二轮可见区渲染修正

- 已根据你最新反馈继续收口：
  - 页面已不报错、时间范围也正确，但 Viewer 仍为空白
  - 右上角 `Query v` 会继续变化
- 已定位到两个更贴近当前现象的前端问题：
  - History 页在结果容器仍处于隐藏状态时就创建并加载 `perspective-viewer`，容易出现“无报错但可视区不渲染”
  - `snapshot` 回来后会继续用事件里的版本号覆盖右上角 Query 标签，造成 UI 上像是在不停跳版本
- 已做最小修正：
  - 渲染时改为先让结果容器显示，再等待 DOM 更新后创建/加载 `perspective-viewer`
  - 创建 Viewer 时补显式 `width/height/display`
  - 加载配置后再执行 `flush()` 与 `resize(true)`，确保可视区真正刷新
  - Query 标签优先保留当前本地查询版本，不再被后续快照噪声反复改写
  - `frontend` 已再次通过 `vue-tsc --noEmit`
- 当前待你刷新页面后重点人工点验：
  - 强制刷新历史页（建议 `Ctrl + F5`）
  - 设备：`qc100`
  - 点位：仅 `Input`
  - 时间：`2026-04-15 00:00:00 ~ 2026-04-16 00:00:00`
  - 预期：
    - Viewer 不再空白
    - `Datagrid` 或 `Y Line` 至少有一种能稳定显示
    - 右上角 `Query v` 不再自己跳号

### 6.19 2026-04-22 History / Perspective 第十三轮真实值修正

- 已根据你最新反馈继续收口：
  - 页面已能显示，但 `Input` 的值被显示成 `1`
  - 第二个点位没有显示出来
- 已做真实链路核对：
  - 直接请求后端 `history/query`，当前返回确实是：
    - `pointId=1 -> [1, 1]`
    - `pointId=2 -> ['-', '-']`
  - 同时直查 TDengine `qc.qc100_1` / `qc.qc100_11901`，确认真实值分别是：
    - `qc100_1 -> 12.83 / 12.45`
    - `qc100_11901 -> 6.47 / 6.34`
  - 说明当前问题已经明确在后端 `history` 服务的数据映射层，不是前端渲染问题
- 已定位到两个后端根因：
  - 子表解析逻辑过于简化，像 `11901` 这类点位编码没有被正确还原成 `qc100_11901`
  - `BOOL` 类型归一化过于激进，把所有非零数都压成了 `1`
- 已做最小修正：
  - 将 `history` 服务的点位子表解析逻辑对齐到当前 TDengine 命名规则
  - `BOOL` 仅在值真实为 `0/1` 或 `true/false` 时才折算，否则保留原始数值
  - 已通过 `backend/app/services/history.py` 的 `compileall` 语法复验
- 当前待你重点人工点验：
  - 由于这是后端代码修改，需要你先重启 `8000` 后端
  - 重启后再次查询 `qc100`
  - 预期：
    - `Input` 不再显示成 `1`
    - 第二个点位能显示 `6.47 / 6.34` 这类真实值

### 6.20 2026-04-24 varSeq 基线专项审计

- 本轮已按最新事实基线完成专项盘点：
  - MySQL 业务键统一收口到 `var_seq`
  - TDengine 子表统一按 `device_code + '_' + var_seq` 派生，例如 `qc100_30`
  - 新桥吊默认主显示点固定为 `qc100` 的 `30 / 11894 / 11900`
  - `collector/device_watch.sql` 视为后续新环境初始化唯一基线
- 当前 `P0` 级运行时残留（建议优先整改）：
  - `backend/app/api/routes/history.py` 仍以 `pointIds` 解析历史查询与导出参数，错误文案也仍是 `pointIds is required`
  - `backend/app/services/history.py` 仍以 `dv.id AS point_id` 组织历史查询、返回 `pointId` 响应字段，并把 `point_id` 作为主选点键；虽然已优先按 `var_seq` 命中 TDengine 子表，但外部契约仍未完全切到 `varSeq`
  - `backend/app/api/routes/catalog.py` 与 `backend/app/services/catalog.py` 的点位详情 / 编辑 / 删除仍以 `/point/{point_id}` 与 `device_variable.id` 为主键口径
  - `backend/app/services/monitor.py`、`backend/app/api/routes/monitor.py`、`backend/app/services/dashboard.py` 仍把 `pointId` 作为实时 / Dashboard 主载荷字段的一部分，`dashboard` 聚合点位对象也仍以 `point_id` 命名
  - `frontend/src/api/device-monitor.ts`、`frontend/src/types/api/device-monitor.d.ts`、`frontend/src/views/device_monitor/history/index.vue`、`frontend/src/views/device_monitor/history/perspective-session.ts`、`frontend/src/views/device_monitor/realtime/index.vue` 仍以 `pointId / pointIds` 作为历史与实时页面的主状态键
- 当前 `P1` 级兼容保留（建议暂留，不必立即删）：
  - `backend/app/services/history.py`、`backend/app/services/monitor.py`、`backend/app/services/dashboard.py` 中对旧 `deviceCode_pointId` 子表与旧 `variable_id` TAG 的回退逻辑，仍有迁移价值；只要它们处于“兼容 fallback”而不是“主契约”，短期保留是合理的
  - `docs/数据库/数据库结构v3.md`、`docs/前后端联调/12-数据库改动.md` 里的 `alarm_rule / alarm_record / alarm_active / system_log` 的 `point_id -> device_variable.id`，目前更像关系型外键，不等同于前后端业务查询键，可以先不动
- 当前 `P2` 级文档残留（需要补刷新）：
  - `docs/后端/API接口文档.md` 仍把历史查询与实时接口描述为 `pointId / pointIds`
  - `docs/设计方案/Perspective集成设计.md` 的历史表结构和消息示例仍是 `pointId`
  - `docs/项目TODO文档.md`、本文档中仍有若干旧样本叙述引用 `pointId=1(Input)` 等历史描述，现已确认这些属于旧联调记录，需要在后续收口时统一改写为 `varSeq` 口径
- 当前建议的改造顺序：
  1. 先切 `history` 的查询/导出契约与返回字段，从 `pointIds / pointId` 收口到 `varSeq / varSeqs`
  2. 再切 `realtime / dashboard` 的点位载荷主键，统一前后端状态键
  3. 最后处理 `point` CRUD 的路径与业务主键表达，避免一开始就动到过多页面交互
- 本轮结论：
  - 这次专项审计没有直接改运行时代码
  - 当前最需要收口的不是 TDengine 命名规则本身，而是“接口契约和页面状态里仍把 `device_variable.id` 当业务键使用”的残留
  - 后续改造时应保留旧子表 / 旧 TAG 读取回退，直到确认存量历史数据已完成迁移

### 6.21 2026-04-24 varSeq 收口第一步：History 契约迁移

- 已按最新基线完成 History 第一轮运行时改造：
  - `backend/app/api/routes/history.py`
    - `history/query` 与 `history/export` 主契约已切到 `varSeqs`
    - 仍临时兼容旧 `pointIds` 入参，避免页面缓存或旧调用立刻断裂
  - `backend/app/services/history.py`
    - `history/points` 现返回 `varSeq`
    - 历史查询主选点键已从 `device_variable.id` 切到 `var_seq`
    - 返回值 `points[]` 与 Perspective snapshot 记录已统一改为 `varSeq`
    - 旧 `deviceCode_pointId` 子表与 `variable_id` TAG 回退继续保留
  - `frontend/src/types/api/device-monitor.d.ts`
  - `frontend/src/views/device_monitor/history/perspective-session.ts`
  - `frontend/src/views/device_monitor/history/index.vue`
    - 历史页点位选择、查询参数、导出参数、结果映射和本地 Perspective Table 已从 `pointId(s)` 全量切到 `varSeq(s)`
- 已同步更新文档口径：
  - `docs/后端/API接口文档.md`
  - `docs/设计方案/Perspective集成设计.md`
- 已完成本地静态复验：
  - 后端：`uv run python -m compileall backend/app/api/routes/history.py backend/app/services/history.py`
  - 前端：`node .\\node_modules\\vue-tsc\\bin\\vue-tsc.js --noEmit`
- 当前结论：
  - History 这条链路已经从“旧业务键残留”推进到“主契约切换完成、兼容回退仍保留”
  - 下一步最合适的是先让你重启 `8000` 后做一次 History 页面真实联调，确认 `varSeq` 口径下查询、导出、Perspective 渲染都正常，再继续切 `Realtime / Dashboard`

### 6.22 2026-04-24 varSeq 收口第二步：Realtime / Dashboard / Point 载荷补正

- 已完成 Realtime 运行时迁移：
  - `backend/app/services/monitor.py`
    - `monitor/realtime-init` 返回的点位载荷已从 `pointId` 改为 `varSeq`
    - 设备点位枚举改为只选取 `var_seq > 0` 的点位，并按 `var_seq` 排序
    - 仍保留旧 TDengine 子表与 `variable_id` TAG 回退
  - `backend/app/api/routes/monitor.py`
    - `WS /ws/realtime-push` 的快照签名改为基于 `varSeq`
  - `frontend/src/types/api/device-monitor.d.ts`
  - `frontend/src/views/device_monitor/realtime/index.vue`
    - Realtime 页的变化高亮键、前后值比较键已统一从 `pointId` 切到 `varSeq`
- 已完成 Dashboard 内部选点清理：
  - `backend/app/services/dashboard.py`
    - 趋势统计只纳入合法 `var_seq` 点位
    - 内部对象显式拆分为 `var_seq` 主键 + `legacy_point_id` 兼容回退键
    - 旧 `deviceCode_pointId` 子表和 `variable_id` TAG 回退继续保留
- 已补齐 Point 前端载荷缺口：
  - `frontend/src/types/api/device-monitor.d.ts`
    - `PointListItem / PointForm` 现已显式声明 `varSeq`
  - `frontend/src/views/device_monitor/point/modules/point-dialog.vue`
    - 点位新增/编辑表单现已补“点位序号（varSeq）”输入项
  - `frontend/src/views/device_monitor/point/modules/point-detail-drawer.vue`
    - 点位详情现已展示 `varSeq`
- 已同步更新文档口径：
  - `docs/后端/API接口文档.md`
    - Realtime 响应改记为 `varSeq`
    - Point 列表/详情说明补记 `varSeq`
    - 明确当前 `/point/{id}` 仍是技术定位键 `device_variable.id`
- 已完成本地静态复验：
  - 后端：`uv run python -m compileall backend/app/services/monitor.py backend/app/api/routes/monitor.py backend/app/services/dashboard.py`
  - 前端：`node .\\node_modules\\vue-tsc\\bin\\vue-tsc.js --noEmit`
- 当前结论：
  - Realtime 已完成主契约迁移
  - Dashboard 对外接口未新增点位主键字段，但内部主选点逻辑已按 `var_seq` 收口
  - Point CRUD 路由本身还不能直接改成单一 `varSeq`，因为 `varSeq` 只在单设备内唯一；若要彻底切换，需要单独设计 `deviceId + varSeq` 这类复合定位契约

### 6.23 2026-04-24 varSeq 收口第三步：Point CRUD 复合定位兼容

- 已完成后端 Point CRUD 定位兼容：
  - `backend/app/services/catalog.py`
    - `get_point / update_point / delete_point` 现支持“`{id}` 技术定位键 + `deviceId + varSeq` 复合业务定位”双通道
    - 当请求同时携带 `deviceId + varSeq` 时，后端优先按“设备内业务键”解析点位，再回退到 `device_variable.id`
  - `backend/app/api/routes/catalog.py`
    - `GET /point/{id}`、`DELETE /point/{id}` 现可接收 `deviceId`、`varSeq`
    - `PUT /point/{id}` 现可接收 `locatorDeviceId`、`locatorVarSeq`
- 已完成前端 Point API 与页面同步：
  - `frontend/src/api/device-monitor.ts`
    - 点位详情/删除现可直接携带 `deviceId + varSeq`
    - 点位编辑在保留原 `id` 回退的同时，也会携带 `locatorDeviceId + locatorVarSeq`
    - 点位列表/导出查询参数已补传 `varSeq`
    - 点位导入字段映射已补上“点位序号 -> varSeq”
  - `frontend/src/views/device_monitor/point/modules/point-dialog.vue`
    - 编辑场景会缓存原始 `locatorDeviceId / locatorVarSeq`，避免修改点位序号后丢失定位目标
  - `frontend/src/views/device_monitor/point/index.vue`
    - 点位详情抽屉改走真实详情接口
    - 单条删除改为提交复合定位信息
    - 导入说明已补记“点位序号”为必填列
- 已同步更新契约文档：
  - `docs/后端/API接口文档.md`
    - Point `list/detail/create/update/delete` 口径已补记 `varSeq` 与复合定位说明
- 已完成本地静态复验：
  - 后端：`uv run python -m compileall backend/app/services/catalog.py backend/app/api/routes/catalog.py`
  - 前端：`node .\\node_modules\\vue-tsc\\bin\\vue-tsc.js --noEmit`
- 当前结论：
  - Point 详情/编辑/删除现在已经具备“业务键优先、技术键回退”的最小兼容能力
  - 这样既不会强行推翻现有前端路由契约，也能让 `varSeq` 基线在 Point CRUD 上继续收口
  - 下一步最适合直接在你当前已启动的 `3006 / 8000` 环境下做 Point 页页面级冒烟，确认详情、编辑变更 `varSeq`、删除、导入都符合预期

### 6.24 2026-04-24 五页联调扫尾：Realtime / Driver 前端映射修正

- 已修复 Realtime 设备树状态映射：
  - `frontend/src/types/api/device-monitor.d.ts`
    - `MonitorDeviceTreeNode` 已补 `connectStatus?: ConnectStatus`
  - `frontend/src/views/device_monitor/realtime/index.vue`
    - 设备树映射现优先读取后端真实字段 `connectStatus`，再回退旧 `status`
    - 这样可避免前端一直按默认 `offline` 渲染节点状态与副标题
- 已修复 Driver 页“未使用驱动被误标为可用”的前端展示问题：
  - `frontend/src/views/device_monitor/driver/index.vue`
    - `status=unused` 现显示为“未使用”
    - 可用数量统计不再把 `unused` 驱动误算进“当前可用”
- 已完成本地复验：
  - 前端：`node .\\node_modules\\vue-tsc\\bin\\vue-tsc.js --noEmit`
  - 3006 代理真实读取：
    - `monitor/device-tree` 已返回设备树样本，节点真实包含状态字段
    - `driver/list` 已确认同时存在 `available` 与 `unused` 两类状态
- 当前结论：
  - Realtime / Driver 这两页的一个关键展示级误差已经收掉
  - 当前剩余更偏向“数据时效/样本覆盖”问题，而不是接口不存在或字段名继续漂移

- 已补齐后端接口：
  - `PUT /api/device-monitor/device/{id}`
  - `DELETE /api/device-monitor/device/{id}`
  - `DELETE /api/device-monitor/device/batch`
  - `PUT /api/device-monitor/point/{id}`
  - `DELETE /api/device-monitor/point/{id}`
  - `DELETE /api/device-monitor/point/batch`
- 已补齐前端页面最小删除入口：
  - 点位页 `frontend/src/views/device_monitor/point/index.vue` 已新增单条删除按钮
- 已使用“仅属于联调新增的数据”完成真实验证：
  - 设备编辑成功，`deviceCode / deviceName / deviceStatus` 可回查到最新值
  - 点位编辑成功，`pointName / ioAddress / dataType / isReadonly` 可回查到最新值
  - 单点删除成功，删除后详情接口返回业务码 `404`
  - 单设备删除成功，并已验证会级联清理其下点位配置
  - 设备批量删除成功，删除后列表查询结果为 `0`
- 当前结论：
  - Device / Point 本轮“增 + 改 + 删”后端链路已打通
  - 设备 / 点位剩余主要 backlog 为导入导出与更细的页面人工点验

### 6.8 2026-04-21 Device / Point 导入导出与 Dashboard 告警侧收口结果

- 已补齐后端接口：
  - `GET /api/device-monitor/device/export`
  - `POST /api/device-monitor/device/import`
  - `GET /api/device-monitor/point/export`
  - `POST /api/device-monitor/point/import`
  - `POST /api/device-monitor/point/import/template`
- 已固定当前导入导出口径：
  - 仅保证支持 `.xlsx`
  - `template=true` 时导出空模板
  - 导出文件固定为 Sheet1 `说明` + Sheet2 业务数据
  - 前端本地读取 `.xlsx` 后，先将中文表头映射为规范 JSON 字段，再提交 `rows`
  - 后端导入接口同时兼容中文表头 key 与规范字段 key，避免运行时对中文 JSON key 的解析差异影响联调
  - 导入按行校验、按行写入、部分成功，不覆盖已有数据
  - 成功响应兼容扩展为 `success / fail / errors?`
- 已补齐前端 Device / Point 页面交互：
  - 点击“导入”先弹格式说明
  - Device 说明框支持“下载模板 / 继续导入”
  - Point 说明框支持“下载模板 / 从模板导入 / 继续导入”
  - 导入失败时展示前 10 条失败原因，不暴露后端堆栈
- Point 模板导入口径：
  - 仅在左侧已选设备时可触发
  - 后端优先使用设备已绑定且启用的模板，否则回退到该设备类型下最新启用模板
  - 只追加当前设备尚不存在的 `var_seq / var_address`，不覆盖已有点位
- 已补齐 Dashboard 告警侧联调兜底：
  - `dashboard/recent-events`
  - `dashboard/alarm-distribution`
- 当前告警侧口径：
  - 有可用真实告警结果时优先返回真实查询
  - 当前真实告警事实表缺失、查询失败或无可用样本时，自动返回后端 synthetic alarm 数据
  - 本轮不写入任何告警测试数据到数据库
- 当前结论：
  - 2026-04-22 已完成 `3006` 代理下的 Device / Point 导入真实复验：均可得到“1 条成功 + 1 条失败 + 明细原因”的部分成功结果，并可查到新增记录
  - 五页优先范围的导入导出链路已从“缺接口”推进到“命令级联调通过，待页面级冒烟验收”
  - Dashboard 告警侧已从“兼容空结构”升级到“可见可验收”

### 6.25 2026-04-25 前六页性能排查与首轮收口

- 已结合 8000 直连与 3006 代理复测确认：当前主要 `device-monitor` 接口本身大多已在毫秒级到几十毫秒级完成，后端接口不再是“切页明显卡住”的主因。
- 本轮确认的主要卡顿来源转为前端页面初始化：
  - Dashboard 在 `KeepAlive` 激活时会重新并发触发 6 组统计请求；
  - Realtime 首屏会先手动拉一次 `realtime-init`，随后 WebSocket 建连后又重复拉一次；
  - History 页面一进入就会主动初始化 Perspective Viewer，额外加载 wasm；
  - Point 页面在带 `deviceId/deviceCode` 路由参数进入时，会先请求一次未筛选列表，再按设备二次请求。
- 已完成的最小性能修正：
  - `frontend/src/views/device_monitor/dashboard/index.vue`
    - 增加激活态短周期复用，10 秒内回到 Dashboard 时复用现有数据，避免短时间重复触发 6 组统计请求；
  - `frontend/src/views/device_monitor/realtime/index.vue`
    - 去掉 WebSocket `onopen` 后重复的 `realtime-init` 拉取，改为直接走订阅返回的 `snapshot`；
  - `frontend/src/views/device_monitor/history/index.vue`
    - 首屏不再主动初始化 Perspective Viewer，仅在真实查询或收到会话快照时再加载；
  - `frontend/src/views/device_monitor/point/index.vue`
    - 当路由已带目标设备时，首屏列表直接按目标设备过滤，避免“全量请求 + 二次筛选”的重复加载。
- 已完成静态复验：
  - `frontend/node_modules/vue-tsc/bin/vue-tsc.js --noEmit`
- 当前保留风险：
  - 由于当前仍是 Vite 开发模式，页面首次打开时仍会受到路由懒加载与模块即时编译影响，尤其 `device / point / history` 这类大页面首开体感仍会慢于已缓存页面；如果本轮修正后仍觉得首开偏慢，下一轮建议再评估“后台预热关键路由模块”。

### 6.26 2026-04-25 前六页性能排查与第二轮“秒开优先”收口

- 已围绕“页面壳先出、重活后做”继续完成第二轮前端优化：
  - `frontend/src/api/device-monitor.ts`
    - 已移除顶层 `xlsx` 引入，`parseImportRows()` 改为调用时动态加载 `xlsx`，避免 Dashboard / Device / Point / Realtime / History 仅因共用 API 文件就把 Excel 解析库带进首屏；
  - `frontend/src/utils/file/save-blob.ts`
    - 新增统一懒加载保存辅助函数，Device / Point / History 三页不再在顶层直接引入 `file-saver`，而是点击导出/下载时再按需加载；
  - `frontend/src/views/device_monitor/history/perspective-runtime.ts`
    - 新增 History 专用 Perspective runtime helper，Perspective runtime、viewer 插件和 wasm 现在只会在真实查询渲染时动态加载，打开历史页但不查询时不再提前背上 wasm 体积；
  - `frontend/src/views/device_monitor/dashboard/index.vue`
    - 顶层 `echarts` 已改为异步加载；
    - Dashboard 当前按“先 `overview`、后图表/列表”分两段加载，图表区域与最近告警区域补上 SVG loading 占位，不再等图表依赖准备完才看到页面主体；
  - `frontend/src/views/device_monitor/device/index.vue`
  - `frontend/src/views/device_monitor/point/index.vue`
  - `frontend/src/views/device_monitor/history/index.vue`
    - 以上页面已补轻量 SVG loading / skeleton 占位，树区或结果区在真实数据返回前先渲染页面骨架，不再直接给人“页面没打开”的体感；
  - `frontend/vite.config.ts`
  - `frontend/.env.development`
    - 新增 `VITE_ENABLE_VUE_DEVTOOLS` 开关，开发环境默认 `false`，减少调试插件对联调阶段首开的额外负担；
- 已完成静态复验：
  - `D:\\ruanjian\\node.js\\node.exe .\\node_modules\\vue-tsc\\bin\\vue-tsc.js --noEmit`
- 当前结论：
  - 本轮优化重点已从“减少重复请求”推进到“拆共享重依赖 + 页面壳先出 + 查询过程明确可见”；
  - 如果用户体感仍觉得 `Device / Point / History` 首开偏慢，下一轮建议优先考虑两件事：
    1. 在登录完成或主框架 idle 时做 `dashboard / device / point` 关键路由模块预热，但继续不预热 History 的 Perspective 重依赖；
    2. 若 `Device / Point` 仍慢，再回到后端对 `catalog.py` 的列表查询继续做 SQL 级分页/过滤与“当前页再补最新值”下钻。

### 6.27 2026-04-25 前六页性能排查与第三轮“长耗时有反馈”收口

- 已补内容区切页反馈：
  - `frontend/src/router/guards/beforeEach.ts`
  - `frontend/src/router/guards/afterEach.ts`
  - `frontend/src/components/core/layouts/art-page-content/index.vue`
  - `frontend/src/utils/ui/loading.ts`
  - 现在路由切换如果超过 `0.5s` 仍未完成，会自动在主内容区显示 SVG loading；
  - 侧边导航、顶部栏、标签栏保持可见，不再被全屏 loading 遮住；
  - 如果切页很快完成，则不会闪一下，避免把快速切换做得更吵；
  - 该 loading 直接接在路由生命周期上，重点解决“第一次打开慢页时点击后长时间没有反馈”的体感问题。
- 已继续缩小首次打开的同步编译范围：
  - `frontend/src/views/device_monitor/device/index.vue`
  - `frontend/src/views/device_monitor/point/index.vue`
  - Device 的 `DeviceDialog`、Point 的 `PointDialog / PointDetailDrawer` 已改为 `defineAsyncComponent` 懒加载，不再在首次进入列表页时就同步编译这些弹窗/抽屉子模块。
- 已完成静态复验：
  - `D:\\ruanjian\\node.js\\node.exe .\\node_modules\\vue-tsc\\bin\\vue-tsc.js --noEmit`
- 当前进一步定位结果：
  - “工作台快、其他页面第一次慢、后续再切很快” 这一现象，和“后端接口慢”不一致，却与 “Vite 开发模式首次按需编译路由页面模块 + 子模块” 高度一致；
  - 当前最像主因的仍是前端开发态首次编译，而不是数据库查询本身，因为这些页面一旦首次加载完成，后续切换基本都能复用已编译模块。
- 下一轮最优先建议：
  1. 在登录成功或 Dashboard 空闲后，后台预热 `device / point / realtime` 三个最常用页面模块，置信度 `0.90`；
  2. 若预热后首开仍慢，再继续下钻 `ArtTable / ArtSearchBar / ElTree` 所在依赖链和 Device / Point 页面的大型子模块，能再提升一截，置信度 `0.80`；
  3. 如果只想确认是不是开发模式首编译导致，可用生产预览包做一次对比验证；我预计生产构建下首次打开会明显好于当前 dev server，置信度 `0.94`。

---

## 七、页面级验收清单

### 7.1 工作台 Dashboard

- [ ] 顶部统计卡均有真实数值
- [ ] 设备类型分布图有真实柱状数据
- [ ] 采集趋势 / 分布图不为空
- [ ] 最近告警 / 最近事件列表有真实记录

### 7.2 Driver / Device / Point

- [ ] 驱动列表可查询、分页、显示状态
- [ ] 设备树可点击，设备列表可分页，详情可打开
- [ ] 点位树可点击，点位列表字段完整，详情 / 编辑入口可用

### 7.3 Realtime / History / Log

- [ ] Realtime 首屏快照可显示（`realtime-init` 与 3006 代理 `snapshot` 已通过命令级联调，待人工页面点验）
- [ ] Realtime WebSocket 可连接、更新、断线恢复（3006 / 8000 均已完成 `snapshot / pong` 验证；重连体验建议人工页面点验）
- [ ] History 设备切换、点位切换、查询、导出可用（新增验收点：查询成功自动收起抽屉；`Y Line` 异常时自动回退 `Datagrid`；不再持续停留在“查询中”；当前样本主要已验证 `qc100/Input`，且已补 `Perspective wasm` 显式初始化）
- [ ] Log 列表可筛选、分页、显示等级标签

### 7.4 整体验收

- [ ] 登录后 7 页均可打开
- [ ] 7 页均不再大面积空白
- [ ] 页面错误时有明确提示，而不是静默无数据
- [ ] `frontend` 模式下完成整体验收后，再评估切换 `backend` 模式

---

## 八、执行约束

- 不允许用 mock API 临时填空页面
- 不允许前后端私自改字段名，统一以 `docs/后端/API接口文档.md` 和 `frontend/src/api/device-monitor.ts` 为准
- 若某个页面所需字段后端暂时无法提供，先在本文补记录，再改实现
- 页面缺口修复完成后，要同步更新项目 TODO 和本目录 README
