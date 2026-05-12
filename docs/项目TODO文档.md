# 工业设备自动采集与诊断系统 - 项目TODO文档

> 版本：2026-04-21 五页优先收口推进版
> 说明：本文档已按当前仓库现状重新整理，重点解决“任务状态失真、依赖顺序不清、测试后置、事实源不唯一”等问题。

---

## 任务状态说明

- `✅ 已完成`：代码或文档已落地，当前基线已确认。
- `🔄 进行中`：已有代码或方案基础，但还需联调、补测试或补验收。
- `📋 未开始`：尚未启动。
- `⏸️ 阻塞`：存在外部依赖或前置条件未满足。
- `📝 计划中`：暂未进入本阶段执行。

---

## 一、当前项目基线速览（2026-04-19）

| 模块 | 当前判断 | 关键证据 | 当前结论 |
|------|----------|----------|----------|
| 采集模块 | 已有较多核心代码 | `collector/core/`、`collector/device/`、`collector/collector/`、`collector/storage/`、`collector/monitor/` | 进入“补链路、补验收、补联调”阶段 |
| 前端模块 | 7 个核心页面和 API 适配层已存在 | `frontend/src/views/device_monitor/`、`frontend/src/api/device-monitor.ts`、`frontend/src/types/api/device-monitor.d.ts` | 进入“对齐后端契约、完成联调”阶段 |
| 后端模块 | 已完成 TurboAPI 骨架，并补起 monitor / history / perspective / auth / system-manage 首版能力；真实 HTTP 登录已验证 | `backend/app/`、`backend/app/api/routes/`、`backend/app/services/` | 进入“补 device-monitor 业务接口、补页面真实数据、补 WS / 统计能力”阶段 |
| 测试体系 | 主要集中在采集侧 | `collector/tests/*.py` | 需要补契约测试、联调测试、CI 与模拟环境 |
| 文档体系 | 设计文档、API 文档、前端规范已较完整 | `docs/后端/API接口文档.md`、`docs/前端/*.md`、`docs/采集模块/*.md` | 需要转入“跟随实现迭代维护”模式 |

---

## 二、执行优先级建议

**第一优先级：先闭环，再铺开。**

建议优先完成这一条链路：

1. 设备树 / 设备列表
2. 实时初始化接口
3. WebSocket 实时推送
4. 前端实时监控页面联调

原因：

- 这条链路同时覆盖采集、后端、前端、时序存储与测试。
- 比先做历史数据或报表更容易暴露协议、接口、状态同步问题。
- 完成后可以作为后续历史查询、Dashboard、报警能力的模板。

**执行原则补充：**

- 当前联调阶段默认采用“最小闭环优先”顺序。
- 在实时监控闭环未完成前，不建议优先铺开历史查询结构优化、Dashboard 高级统计、导入导出增强或大范围样式细节优化。
- 当前默认采用 `M2-A 联调可验收` 与 `M2-B 环境增强验收` 两层验收策略，真实 PLC 未就绪时不阻塞 M2-A，但 TDengine 是当前实时与历史链路的基础依赖。
- 对前端的 WebSocket 事件格式由后端统一归一化，collector 与 Fake Data 只作为上游来源。
- 当前联调阶段禁止使用 mock API / mock WebSocket / mock 页面数据；测试假数据必须写入本地 MySQL / TDengine 等真实服务链路，再由后端统一对外提供接口和 WebSocket 事件。
- 详细说明见：`docs/前后端联调/07-最小闭环优先执行说明.md`

---

## 三、治理与协作基线

> **负责智能体**：`@coordinator`
> **协作智能体**：`@doc`, `@backend`, `@frontend`, `@collector`, `@test`
>
> **开始任务前必读**：
> 1. `docs/智能体分工方案.md`
> 2. `docs/后端/API接口文档.md`
> 3. `docs/设计方案/数据库结构v3.md`
> 4. `docs/前后端联调/README.md`

| ID | 任务描述 | 状态 | 负责人 | 优先级 | 说明 |
|----|----------|------|--------|--------|------|
| GOV-001 | 校准 TODO 与仓库真实状态 | ✅ | `@coordinator` | P0 | 已按当前仓库重新整理 |
| GOV-002 | 固定唯一事实源路径和 Owner | ✅ | `@coordinator` | P0 | 数据库、API、前端适配层职责已明确 |
| GOV-003 | 建立 Design / Implement / Review 三段式责任模型 | ✅ | `@coordinator` | P0 | 见《智能体分工方案》 |
| GOV-004 | 冻结 v1 REST / WebSocket 契约基线 | 🔄 | `@backend` | P0 | 以 `docs/后端/API接口文档.md` 为准 |
| GOV-005 | 落地交接清单和 WIP 限制 | ✅ | `@coordinator` | P1 | 防止多人并行冲突 |
| GOV-006 | 建立本地模拟环境策略（PLC / TDengine / MySQL） | 🔄 | `@test` | P0 | 已明确“真实服务 + 测试假数据、禁用 mock 数据”原则，待补 PLC / TDengine runbook |
| GOV-007 | 建立契约 / 数据库 / WS 变更评审机制 | 📋 | `@coordinator` | P1 | 变更先改事实源再改代码 |
| GOV-008 | 建立 v1 接口契约冻结与变更门禁 | ✅ | `@coordinator` | P0 | 已落地到 `docs/前后端联调/01-v1接口契约冻结与变更门禁.md` |
| GOV-009 | 建立前端契约映射清单 | ✅ | `@coordinator` | P0 | 已落地到 `docs/前后端联调/02-前端契约映射清单.md` |
| GOV-010 | 建立前端设计适配规则 | ✅ | `@coordinator` | P0 | 已落地到 `docs/前后端联调/03-前端设计适配规则.md` |
| GOV-011 | 建立路由 / 菜单 / 页面 / 权限矩阵 | ✅ | `@coordinator` | P0 | 已落地到 `docs/前后端联调/04-路由菜单页面权限矩阵.md` |
| GOV-012 | 统一实时态与历史态语义说明，明确 Redis 暂不使用 | ✅ | `@coordinator` | P0 | 已修正 `docs/前后端联调/05-Redis实时态与告警态语义说明.md` 为现网口径说明 |
| GOV-013 | 建立联调环境矩阵与验收口径 | ✅ | `@coordinator` | P0 | 已落地到 `docs/前后端联调/06-联调环境矩阵与验收口径.md` |
| GOV-014 | 建立“最小闭环优先”执行说明 | ✅ | `@coordinator` | P0 | 已落地到 `docs/前后端联调/07-最小闭环优先执行说明.md` |
| GOV-015 | 明确 M2-A/M2-B 分层验收与 WS 统一归一化原则 | ✅ | `@coordinator` | P0 | 已同步到联调治理文档与 TODO 执行原则 |

---

## 四、基础设施与数据库模块

> **负责智能体**：`@database`
> **协作智能体**：`@backend`, `@collector`, `@test`
>
> **开始任务前必读**：
> 1. `docs/设计方案/数据库结构v3.md`
> 2. `docs/数据库/TDengine TSDB Python Connector.md`
> 3. `collector/storage/tdengine.py`

| ID | 任务描述 | 状态 | 负责人 | 优先级 | 说明 |
|----|----------|------|--------|--------|------|
| DB-001 | 核对 MySQL 主库模型与当前业务域边界 | 🔄 | `@database` | P0 | 先对齐设备、点位、报警、日志、用户等主表 |
| DB-002 | 输出 MySQL 初始化 DDL | 📋 | `@database` | P0 | 后端起盘前必须具备 |
| DB-003 | 输出数据库迁移脚本基线 | 📋 | `@database` | P1 | 避免后续手工改表 |
| DB-004 | 固定 TDengine 最新值查询口径与实时快照语义 | 🔄 | `@database` | P0 | 当前已明确实时与历史统一由 TDengine 提供 |
| DB-005 | 固定 TDengine 超级表/子表命名与保留策略 | 🔄 | `@database` | P0 | 需与采集写入实现对齐 |
| DB-006 | 输出 Demo / Seed 数据方案 | 🔄 | `@database` | P1 | 已完成 STEP-003 最小集设计，并在 STEP-004 细化为落库清单与校验模板 |
| DB-007 | 固定 auth / permission 8 张表结构与索引 | 🔄 | `@database` | P0 | 已在后端 ORM 与文档落地，待补正式 DDL / migration |
| DB-008 | 输出 auth 种子数据与本地联调账号基线 | 🔄 | `@database` | P0 | 已内置 `Super / Admin / User` 与角色菜单权限矩阵，待数据库脚本化 |

---

## 五、数据采集模块

> **负责智能体**：`@collector`
> **协作智能体**：`@database`, `@backend`, `@test`
>
> **开始任务前必读**：
> 1. `docs/采集模块/采集模块整体结构设计.md`
> 2. `docs/采集模块/采集模块代码设计逻辑.md`
> 3. `collector/main.py`
> 4. `collector/drivers/base.py`
>
> **当前代码基线**：
> - 已有配置、缓存、数据质量、连接池、插件系统等实现；
> - 已有 `S7`、`GE-SRTP`、`OPC UA` 驱动代码；
> - 已有 `task/scheduler/executor`、`buffer/aggregator`、`tdengine/batch_writer`、`monitor` 系列代码；
> - 当前重点不再是“从零建文件”，而是“补完闭环、验证行为、补测试与联调”。

| ID | 任务描述 | 状态 | 负责人 | 优先级 | 说明 |
|----|----------|------|--------|--------|------|
| CL-001 | 配置、校验、日志、异常基础能力验收 | ✅ | `@collector` | P0 | 相关代码已落地 |
| CL-002 | 重试、熔断、自动重连链路完善 | 🔄 | `@collector` | P0 | 与 `device/manager.py` 强相关 |
| CL-003 | S7 / GE-SRTP 驱动基线维护 | 🔄 | `@collector` | P0 | 基线已实现；2026-05-07 复审新增 DWORD/BOOLEAN 类型口径、GE 现场 smoke 与健康检查补强建议，仍需真机或模拟环境验证 |
| CL-004 | OPC UA 驱动补完与验证 | 🔄 | `@collector` | P1 | 代码存在，验收不足 |
| CL-005 | 任务模型、调度器、执行器闭环完善 | 🔄 | `@collector` | P0 | 当前已形成基础链路 |
| CL-006 | 缓冲队列、聚合去重、时间窗口、批量策略验收 | 🔄 | `@collector` | P0 | 2026-05-07 复审发现 TDengine 最终写失败后已 flush 批次未重入队，需修复后再做故障注入验收 |
| CL-007 | TDengine WebSocket 连接与批量写入验收 | 🔄 | `@collector` | P0 | 重点关注重连、建库建表、异常恢复；测试 mock 需同步 `_stable_exists/_table_exists` 查询链路 |
| CL-008 | 监控 API、健康检查、外部接口采集服务验收 | 🔄 | `@collector` | P1 | 已有代码，需统一验收口径 |
| CL-009 | 建立 fake data 启动与 smoke runbook | 📋 | `@collector` | P0 | 联调前必须补齐 |
| CL-010 | 为关键链路补充模块级测试与冒烟脚本 | 🔄 | `@collector` | P0 | 2026-05-07 pytest 临时跑法结果 69 passed / 7 failed；需修复 venv 路径、cache/aggregator/TDengine mock 测试后恢复绿色 |
| CL-011 | 测试前复审新增 P1/P2 收口 | 🔄 | `@collector` | P0 | 新增报告 `docs/采集模块/collector测试前代码审查报告_2026-05-07.md`；P1 包括 OPC DA center 端口映射、聚合器失败批次保留、数据类型口径、opc_server 必填、venv 路径修复 |

---

## 六、后端服务模块

> **负责智能体**：`@backend`
> **协作智能体**：`@database`, `@frontend`, `@collector`, `@test`
>
> **开始任务前必读**：
> 1. `docs/后端/API接口文档.md`
> 2. `docs/设计方案/标准后端架构.md`
> 3. `docs/设计方案/数据库结构v3.md`
>
> **当前代码基线**：
> - `backend/` 已完成最小 TurboAPI 项目骨架，并接入 `system`、`monitor/device-tree`、`monitor/realtime-init`、`ws/realtime`、`history/*`、`auth`、`system-manage`、`driver/*`、`device/*`、`point/*` 首版路由。
> - 当前已完成 Python 3.14t 下的本机启动、真实 HTTP `login / user/info / menus / user-list / role-list` 验证，以及 `driver / device / point` 读链路、`device / point` 新增链路与 `ws/realtime` 的本地 ASGI + 8000 直连 + 3006 代理复验；告警 / 日志事实表已落地；collector 已接入系统运行日志落库，当前主剩余项转为后端 Log 真实查询 / 统计 / 清理 / DEBUG 管理接口接入和页面人工点验。

| ID | 任务描述 | 状态 | 负责人 | 优先级 | 说明 |
|----|----------|------|--------|--------|------|
| BE-001 | 建立 TurboAPI（FastAPI compatible）项目结构与启动入口 | ✅ | `@backend` | P0 | 已完成 Python 3.14t 本机启动验证，`healthz/system/ping` 可稳定返回 |
| BE-002 | 建立配置、MySQL、TDengine、统一响应与异常处理 | ✅ | `@backend` | P0 | 已完成配置回退、数据库客户端封装、统一响应与异常处理，并通过首轮真实接口验证 |
| BE-003 | 按数据库事实源输出 ORM / Schema 基线 | 📋 | `@backend` | P0 | 设备、点位、报警、日志优先 |
| BE-004 | 实现设备管理 API（组树 / 列表 / 详情 / CRUD / 导入导出） | 🔄 | `@backend` | P0 | 2026-04-21 已完成组树 / 列表 / 详情 + `POST / PUT / DELETE / batch delete`；2026-04-22 已修复导入 `rows` JSON 字段映射兼容并通过 3006 代理实测，当前剩余页面人工点验 |
| BE-005 | 实现实时初始化与 WebSocket 推送 | ✅ | `@backend` | P0 | 2026-05-09 已完成载荷兼容性修复（支持对象/数组双口径）与推送去重逻辑优化，命令级与页面级联调均已通过 |
| BE-006 | 实现历史、日志、Dashboard API | ✅ | `@backend` | P1 | 历史查询(ECharts)已完美落地；Dashboard 统计全面切换为真实数据；日志列表与清理相关接口也已联调测试通过 |
| BE-007 | 建立 API 契约校验与示例载荷 | 📋 | `@backend` | P0 | 与 `@frontend`, `@test` 联动 |
| BE-008 | 实现 Perspective 会话管理与历史分析 WebSocket 通道 | 🔄 | `@backend` | P0 | 2026-04-19 已启动后端基础接入：依赖、配置、会话管理骨架与 WS 基线路由先行，待补历史查询落表与前端联调 |
| BE-009 | 实现真实数据登录与当前用户信息接口 | ✅ | `@backend` | P0 | 已完成真实 HTTP 验证，`Super / User` 登录和 `user/info` 均可正常返回 |
| BE-010 | 实现 backend 动态菜单与 system-manage 权限接口 | ✅ | `@backend` | P0 | CRUD 接口已开发完成并测试通过，目前在人工点验测试阶段 |
| BE-011 | 实现驱动管理 API（`driver/list`、`driver/options`） | ✅ | `@backend` | P0 | 2026-04-20 已基于 `comm_protocol` 完成真实只读闭环，并通过 compileall + 服务层复验 |
| BE-012 | 实现点位管理 API（设备树 / 列表 / 详情 / CRUD / 导入导出） | 🔄 | `@backend` | P0 | 2026-04-21 已完成设备树 / 列表 / 详情 + `POST / PUT / DELETE / batch delete`；2026-04-22 已修复导入 `rows` JSON 字段映射兼容并通过 3006 代理实测，当前剩余页面人工点验 |
| BE-013 | 实现 Dashboard 统计 API（overview / trend / distribution / events / health） | ✅ | `@backend` | P0 | 所有统计接口均已彻底切换到正式真实数据口径并完成测试 |
| BE-014 | 实现日志列表 API 与真实标签口径 | 🔄 | `@backend` | P1 | 2026-05-06 已确认 `operation_log / system_log / log_retention_policy` 已落地，并完成日志查询索引和日志权限增量优化；collector 已接入启动/关闭、设备状态、采集摘要、TDengine 写入摘要与异常日志落库；后端代码写入当前受 backend 目录权限阻塞，待放开后切 `log/list` 真实查询并补统计、清理、策略、DEBUG 与客户端上报接口 |

---

## 七、前端开发模块

> **负责智能体**：`@frontend`
> **协作智能体**：`@backend`, `@doc`, `@test`
>
> **特别注意**：前端基于 **ArtDesignPro**，不得切换到其他模板体系。
>
> **开始任务前必读**：
> 1. `docs/前端/开发必读文档.md`
> 2. `docs/前端/页面设计文档.md`
> 3. `docs/前端/ArtDesignPro项目结构.md`
> 4. `docs/前端/路由和菜单.md`
> 5. `frontend/src/api/device-monitor.ts`
> 6. `frontend/src/types/api/device-monitor.d.ts`
>
> **当前代码基线**：
> - `dashboard / driver / device / point / realtime / history / log` 页面代码均已存在；
> - 当前重点是替换临时假设、对齐最终 API 契约、补联调与错误处理；
> - `frontend/src/store/` 才是当前状态管理目录。

| ID | 任务描述 | 状态 | 负责人 | 优先级 | 说明 |
|----|----------|------|--------|--------|------|
| FE-001 | Dashboard 页面按最终后端契约完成联调 | ✅ | `@frontend` | P0 | 页面已完成联调和验收测试 |
| FE-002 | Driver 页面完成列表、分页、搜索联调 | ✅ | `@frontend` | P1 | 页面已完成联调和验收测试 |
| FE-003 | Device 页面完成树、列表、编辑、导入导出联调 | ✅ | `@frontend` | P0 | 页面已完成联调和验收测试 |
| FE-004 | Point 页面完成树、详情、编辑、导入导出联调 | ✅ | `@frontend` | P0 | 页面已完成联调和验收测试 |
| FE-005 | Realtime 页面完成 WS 建连、重连、状态恢复 | ✅ | `@frontend` | P0 | 2026-05-09 已完成：1) 四段式新布局重构；2) 兼容后端对象载荷；3) Perspective 改为增量 update 模式修复渲染中断并提升性能；4) 修复容器初始化 race condition |
| FE-006 | History 页面历史查询图表联调 (ECharts) | ✅ | `@frontend` | P1 | 历史查询已确认采用 ECharts 图表，已完美落地并完成联调测试 |
| FE-007 | Log 页面完成筛选、分页、颜色标识联调 | ✅ | `@frontend` | P1 | 页面已完成联调和验收测试 |
| FE-008 | 统一 API wrapper / type / router / store 约束 | ✅ | `@frontend` | P0 | 防止继续漂移 |
| FE-009 | 建立前端冒烟数据回放和错误场景验证 | ✅ | `@frontend` | P1 | 与 `@test` 协作 |
| FE-010 | 恢复真实数据登录链路并验证 frontend 模式 `roles/buttons` | ✅ | `@frontend` | P0 | 已完成登录恢复与 `roles/buttons` 基础验证，当前阻塞已转移到页面业务数据 |
| FE-011 | 调整 device-monitor 前端路由角色基线，允许 `R_USER` 进入只读页面 | ✅ | `@frontend` | P0 | 已开始按 dashboard / realtime / history / log 只读开放收口 |
| FE-012 | 菜单接口验收通过后切换 `VITE_ACCESS_MODE=backend` | 📋 | `@frontend` | P0 | 切换前必须先通过 `GET /api/v3/system/menus` 验收 |

> 2026-04-20 补充进展：
> - 已修复历史页 `history/points` / `history/query` 的 `deviceId` query 参数类型问题，避免前端设备切换时报 `500`
> - 已优化 `device/group-tree`、`device/list`、`monitor/device-tree` 的首屏读取策略，减少因全量 TDengine 最新值查询导致的页面切换等待
> - 已修复 Dashboard 在 `KeepAlive` 下离开页面后仍持续轮询 `dashboard/*` 的问题，降低后台 404 噪音与无效请求
> - 2026-04-25 已做一轮页面切换性能收口：Dashboard 激活后 10s 内复用现有数据避免重复触发 6 组统计请求；Realtime 去掉 WebSocket 建连后的重复 `realtime-init` 拉取；History 改为按需初始化 Perspective Viewer，避免一进页就加载 wasm；Point 在带设备路由参数进入时避免“先全量列表、再按设备筛选”的重复首屏请求
> - 2026-04-25 已做第二轮“秒开优先”收口：`device-monitor.ts` 不再顶层引入 `xlsx`，Device / Point / History 的 `file-saver` 改为点击时动态加载，History 的 Perspective runtime 与 wasm 改为查询时懒加载，Dashboard 的 `echarts` 改为异步加载且先拉 `overview` 再补图表，Device / Point / History / Dashboard 均补了 SVG loading 占位，同时开发环境默认关闭 `VITE_ENABLE_VUE_DEVTOOLS`
> - 2026-04-25 已补第三轮切页反馈优化：路由切换超过 0.5 秒时在主内容区显示 SVG loading，保留侧边导航与顶部栏可见，避免首次打开慢页时“点击无反应”；同时继续把 Device / Point 的弹窗子模块改为异步加载，减少首次打开页面时被一并编译的体积

---

## 八、测试与基础设施模块

> **负责智能体**：`@test`
> **协作智能体**：所有开发智能体
>
> **开始任务前必读**：
> 1. `collector/tests/conftest.py`
> 2. `collector/tests/test_*.py`
> 3. `docs/后端/API接口文档.md`
>
> **当前代码基线**：
> - 已存在 `test_device_manager.py`、`test_data_aggregator.py`、`test_error_handler.py`、`test_cache.py`、`test_data_quality.py`、`test_s7_driver.py`、`test_integration.py`、`test_interface_ingest.py` 等测试文件；
> - 目前缺口在于：契约测试、前后端联调测试、性能压测、本地模拟环境、CI 质量门禁。

| ID | 任务描述 | 状态 | 负责人 | 优先级 | 说明 |
|----|----------|------|--------|--------|------|
| TEST-001 | 采集侧现有单测基线梳理与验收 | 🔄 | `@test` | P0 | 先确认现有测试覆盖范围 |
| TEST-002 | 建立“模块 Owner 同步补单测”的执行规则 | 🔄 | `@test` | P0 | 测试左移 |
| TEST-003 | 建立 REST / WS 契约测试 | 📋 | `@test` | P0 | 与 `@backend`, `@frontend` 强耦合 |
| TEST-004 | 建立采集到存储链路的集成测试 | 🔄 | `@test` | P0 | 当前已有部分基础 |
| TEST-005 | 建立前后端联调冒烟测试 | 📋 | `@test` | P0 | 后端骨架完成后立即接入 |
| TEST-006 | 建立 PLC / TDengine / MySQL 联调环境 | 📋 | `@test` | P0 | 当前最大短板 |
| TEST-007 | 建立性能测试方案与压测脚本 | 📋 | `@test` | P1 | 聚焦高并发采集与 WS 推送 |
| TEST-008 | 接入 CI、覆盖率报告与质量门禁 | 📋 | `@test` | P1 | 形成可回归机制 |
| TEST-009 | 建立 auth `login / user/info / menus` 冒烟测试 | 🔄 | `@test` | P0 | 已完成人工验证，待沉淀为自动化 smoke case |
| TEST-010 | 验证 `Super / Admin / User` 三档账号菜单与按钮权限矩阵 | 🔄 | `@test` | P0 | 已完成首轮手工差异验证，待补 frontend/backend 双模式回归 |
| TEST-011 | 建立 device-monitor 页面数据缺口回归清单 | 📋 | `@test` | P0 | 覆盖 dashboard / driver / device / point / realtime / history / log 七页真实数据可见性 |

---

## 八-A、Perspective 接入专项 TODO（2026-04-19 启动）

> **负责智能体**：`@coordinator`
> **协作智能体**：`@backend`, `@frontend`, `@test`, `@doc`
>
> **开始任务前必读**：
> 1. `docs/设计方案/Perspective集成设计.md`
> 2. `docs/设计方案/技术方案.md`
> 3. `docs/后端/Perspective会话隔离与保护策略.md`
> 4. `frontend/src/views/device_monitor/history/index.vue`
> 5. `frontend/src/api/device-monitor.ts`
> 6. `frontend/src/types/api/device-monitor.d.ts`

| ID | 任务描述 | 状态 | 负责人 | 优先级 | 说明 |
|----|----------|------|--------|--------|------|
| PERS-001 | 拆解 Perspective 接入 todoplan 并同步项目 TODO | ✅ | `@coordinator` | P0 | 已完成专项拆解，并将执行清单写回项目 TODO |
| PERS-002 | 落地后端基础接入：依赖、配置、会话管理骨架、WS 基线路由 | ✅ | `@backend` | P0 | 已完成 `perspective-python` 依赖声明、配置项补齐、会话管理骨架与 `WS /api/device-monitor/ws/perspective` 基线路由接入 |
| PERS-003 | 补齐历史查询后端真实能力：`history/devices / points / query / export` | 🔄 | `@backend` | P0 | 2026-04-22 已补 `var_code` 子表空结果回退到 `deviceCode_pointId` 子表，当前 `history/query` 与 `history/export` 已完成命令级真实复验，待页面人工点验 |
| PERS-004 | 打通历史查询结果到 Perspective Table 的标准化落表链路 | 🔄 | `@backend` | P0 | 2026-04-22 已完成真实 `snapshot` 推送复验：前端会话 Header -> 后端标准化 records -> `WS /ws/perspective` 快照链路可返回真实记录 |
| PERS-005 | 实现 Perspective 会话保护策略：会话恢复、冲突检测、TTL、queryVersion 抑制 | 🔄 | `@backend` | P0 | 已落地 query range / point count / record count / queryVersion / session restore 首版保护，待补剩余联调验收 |
| PERS-006 | 前端接入 Perspective 依赖与客户端封装 | 🔄 | `@frontend` | P0 | 2026-04-22 已完成 TypeScript 基线恢复，并接上 `sessionStorage` 会话标识、`queryVersion` Header、`WS /ws/perspective` 生命周期与 `snapshot/session_restored/session_conflict/pong` 首版处理；同日已按官方 demo 思路补 `perspective-server.wasm` / `perspective-viewer.wasm` 显式初始化，修复 `Missing perspective-client.wasm` 阻塞 |
| PERS-007 | 重构 History 页面：移除 ECharts 主渲染，切换为 Perspective Viewer | ⏸️ | `@frontend` | P0 | 架构已调整：历史查询现已确认采用 ECharts 图表，已完美落地并完成测试，Perspective Viewer 方案作废/暂存 |
| PERS-008 | 补齐联调验收与回退兜底 | 🔄 | `@test` | P0 | 2026-04-22 已完成命令级复验：`snapshot / session_restored / session_conflict / pong`、超 24h 拒绝、超 20 点拒绝、CSV 导出；同日补记当前 TDengine 样本覆盖主要集中在 `qc100 / pointId=1(Input)`，其余设备/点位页面返回空结果可能是样本不足而非接口异常，待补页面人工点验与样本覆盖说明 |

---

## 九、文档与运维模块

> **负责智能体**：`@doc`
> **协作智能体**：所有智能体

| ID | 任务描述 | 状态 | 负责人 | 优先级 | 说明 |
|----|----------|------|--------|--------|------|
| DOC-001 | 更新智能体分工方案 | ✅ | `@doc` | P0 | 已完成本轮重构 |
| DOC-002 | 更新项目 TODO 基线文档 | ✅ | `@doc` | P0 | 已按仓库现状校准 |
| DOC-003 | 维护 API 契约签收说明与示例载荷 | 🔄 | `@doc` | P0 | 与 `@backend` 同步 |
| DOC-004 | 编写本地开发与联调指南 | 📋 | `@doc` | P1 | 覆盖 collector / backend / frontend / MySQL / TDengine |
| DOC-005 | 编写部署、运维与故障排查手册 | 📋 | `@doc` | P1 | 上线前必须完成 |
| DOC-006 | 编写里程碑验收清单 | 📋 | `@doc` | P1 | 供 `@coordinator` 使用 |

---

## 十、里程碑与推荐顺序

| 里程碑 | 目标 | 状态 | 关键交付物 |
|--------|------|------|------------|
| M0 | 完成治理基线与事实源收敛 | 🔄 | 更新后的 TODO、分工方案、统一契约入口 |
| M1 | 建立后端可运行骨架并冻结 v1 契约 | ✅ | TurboAPI 基线、数据库连接、统一响应、契约示例 |
| M2 | 完成“实时监控”最小闭环 | 🔄 | 设备树 / 实时初始化 / WebSocket / 前端实时页 |
| M3 | 完成历史、日志、Dashboard 主功能联调 | 🔄 | 历史查询、日志查询、工作台统计接口与页面 |
| M4 | 完成集成测试、压测、CI 基线 | 📋 | 契约测试、联调测试、模拟环境、CI 规则 |
| M5 | 完成部署与上线准备 | 📋 | 部署文档、运维手册、验收清单 |

---

## 十一、当前阻塞项与风险

### 11.1 当前阻塞项

| ID | 阻塞描述 | 影响范围 | 负责人 | 状态 | 说明 |
|----|----------|----------|--------|------|------|
| BLOCK-001 | TDengine 当前表结构语义存在设计问题，需数据库方案统一修正 | 正式采集落地、实时语义一致性、后续 WS 设计 | `@database` | ⏸️ | 当前已通过假数据脚本时间戳错峰临时绕开，不阻塞首轮前后端 HTTP 联调；后续需由数据库设计师统一修正文档与结构 |
| BLOCK-002 | 缺少统一模拟环境与假数据方案 | 采集验证、联调、压测 | `@test` | ⏸️ | 工业现场资源不可长期依赖 |
| BLOCK-003 | 已解决：前端开发代理缺少 WebSocket 转发，曾导致 Realtime 页面无法经 3006 自动建连 | 设备监控 7 页真实联调 | `@backend`, `@frontend` | ✅ | 已补 `frontend/vite.config.ts -> /api.ws=true` 并在前后端重启后复验通过；`ws://127.0.0.1:3006/api/device-monitor/ws/realtime` 已可完成 `subscribe / snapshot / pong` |
| BLOCK-004 | 已解决：告警 / 日志业务表与统计 seed 曾缺失 | Dashboard、Log、验收测试 | `@database`, `@backend`, `@test` | ✅ | `alarm_level / alarm_rule / alarm_record / alarm_active / operation_log / system_log` 已落地；2026-05-06 已新增 `log_retention_policy`、日志索引和日志权限，剩余工作转为后端真实查询与清理管理接口接入 |

### 11.2 风险清单

| ID | 风险描述 | 风险等级 | 影响 | 应对措施 | Owner |
|----|----------|----------|------|----------|-------|
| RISK-001 | 前后端再次各自演化导致契约漂移 | 高 | 联调失败、页面返工 | 锁定 `docs/后端/API接口文档.md` 为唯一契约源 | `@backend` |
| RISK-002 | TODO 再次脱离仓库事实 | 高 | 排期与资源判断失真 | 由 `@coordinator` 统一维护并阶段校准 | `@coordinator` |
| RISK-003 | 缺少 PLC / TDengine / MySQL 联调环境 | 高 | 无法稳定验收 | 提前建设 fake data、fixture、smoke script | `@test` |
| RISK-004 | 采集链路已有代码但无统一验收口径 | 中 | “写了很多代码但不可交付” | 以闭环任务和测试结果驱动验收 | `@collector`, `@test` |
| RISK-005 | 后端起盘过慢导致前端空转 | 中 | 阶段进度失衡 | 优先只做最小闭环相关接口 | `@backend`, `@coordinator` |

---

## 十二、分步设计推进记录

| Step ID | 日期 | 范围 | 状态 | 本步产出 | 待审阅点 |
|---------|------|------|------|----------|----------|
| STEP-001 | 2026-04-13 | M2-A 第一步设计：设备树 / 设备列表 / 实时初始化 / WebSocket / Realtime 页面 | ✅ 已完成 | 首轮已完成 Realtime 页面状态与最小闭环设计拆分，但其中 Redis 假设已在 STEP-002 中纠偏 | 无 |
| STEP-002 | 2026-04-13 | 架构纠偏：统一 MySQL / TDengine 口径，移除 Redis 运行依赖 | ✅ 已完成 | 明确 MySQL 仅负责配置/主数据，TDengine 同时负责实时与历史时序数据，Redis 暂不使用；已同步修正 TODO、联调治理文档和旧设计文档 | 无 |
| STEP-003 | 2026-04-13 | 设计首批 MySQL / TDengine 测试假数据集与 Realtime 接口落点 | ✅ 已完成 | 明确 device/group/point 等 MySQL 最小主数据集、TDengine 在线/离线/异常/过期样本集、`device-tree` / `realtime-init` / `ws/realtime` 的查询落点与测试数据落库顺序；前端已同步首轮字段依赖、状态覆盖与联调顺序 | 无 |
| STEP-004 | 2026-04-13 | 细化首批测试假数据落库清单、校验清单与执行前置条件 | ✅ 已完成 | 新增 `docs/前后端联调/08-首批测试假数据落库清单.md`，明确 MySQL / TDengine 测试 ID 段、最小落库表、推荐样本、落库顺序、接口校验点与前端联调顺序；已同步更新 `docs/数据库/fake_data_verify.sql` 与 `docs/数据库/fake_data_cleanup.sql` 为当前真实链路模板 | 无 |
| STEP-005 | 2026-04-13 | 对齐当前 collector DDL / ORM / TDengine 命名规则与联调落库模板 | ✅ 已完成 | 基于 `collector/device_watch.sql`、`collector/core/config_db.py`、`collector/storage/tdengine.py` 回写联调模板：补充 `variable_group`、明确 `device.id / device_code / device_variable.id` 键映射、修正 TDengine 当前子表命名为 `device_code`、修正验证/清理 SQL 中的字段与表名 | 无 |
| STEP-006 | 2026-04-13 | 扩展首批联调状态覆盖，并收敛为可执行 seed SQL / 执行说明 | ✅ 已完成 | 将首轮测试集从 2 台设备扩展为 3 台设备，拆分 `update`、`stale/offline`、`alarm` 状态覆盖；已新增 `docs/数据库/fake_data_seed_mysql.sql`、`docs/数据库/fake_data_seed_tdengine.sql`、`docs/前后端联调/09-首批测试假数据执行说明.md`，并更新 `08` 清单、验证 SQL、清理 SQL 与联调索引；当前联调样本命名已统一为 `qc100`、`asc7011`、`agv801` 及 `qc100_1`、`asc7011_85`、`agv801_100` 等子表规则 | 无 |
| STEP-007 | 2026-04-13 | 核对真实 MySQL live schema，并将首批测试数据实际写入本机 MySQL / TDengine | ✅ 已完成 | 已确认本机 `device_watch` live schema 与关键 seed 字段兼容，真实外键仅存在于 `device` 与 `plc_connection_log`；已将首批测试主数据写入 MySQL，并以 `qc100`、`asc7011`、`agv801` 这 3 台代表设备组织 TDengine 联调样本；同时补充了可执行的拆分版 verify/cleanup 脚本，并确认 `taos.exe -f` 需经 ASCII 路径执行 | 无 |
| STEP-008 | 2026-04-13 | 建立 backend 最小 TurboAPI 骨架与启动入口 | ✅ 已完成 | 已在 `backend/` 下创建最小 TurboAPI 骨架：应用工厂、启动入口、system 路由、配置加载、MySQL/TDengine 客户端封装、统一响应/异常处理、`requirements.txt`、`.env.example` 与 `README.md`；后端代码统一收口在 `backend/` | 无 |
| STEP-009 | 2026-04-13 | 实现 `device-tree / realtime-init` 两个最小读接口，并补齐本地启动前置兜底 | ✅ 已完成（待用户审阅） | 已新增 `monitor` 路由与服务层：基于 MySQL 输出设备树、基于 MySQL + TDengine 输出实时初始化快照；同时补充 TDengine 异步查询助手、`.env/.env.example` 配置回退，以及 Windows 控制台 UTF-8 兜底，便于后续用本机 Python 3.14 启动验证 | 是否进入下一步“启动本地后端并验证真实接口，再切前端代理进行联调” |
| STEP-010 | 2026-04-14 | 使用 Python 3.14t 完成本地后端启动与首轮真实接口验证 | ✅ 已完成（待用户审阅） | 已在 Python 3.14t 下通过真实 HTTP 请求验证 `healthz`、`system/ping`、`monitor/device-tree`、`monitor/realtime-init` 均可返回 `200`；同时确认当前 TDengine 样本已跨日导致设备状态均为 `offline`，且三台测试设备当前各有 1 个点位返回空值，需要刷新测试样本后再推进前端联调 | 是否进入下一步“刷新 TDengine 实时样本并切前端代理做页面联调” |
| STEP-011 | 2026-04-14 | 切换前端开发代理到本地后端并完成首轮 HTTP 联调验证 | ✅ 已完成 | 已将 `frontend/.env.development` 的开发代理目标从 Apifox mock 切到 `http://127.0.0.1:8000`，并在开发环境临时关闭 Realtime 页 WS 自动建连；实际验证 `http://127.0.0.1:3006/api/device-monitor/monitor/device-tree` 与 `realtime-init?deviceId=991001` 可经前端代理返回真实后端数据 | 是否进入下一步“补齐 `/ws/realtime` 并恢复实时页 WS 订阅联调” |
| STEP-012 | 2026-04-14 | TDengine 数据模型纠偏：从“设备一表”切换为“设备类型分库 + 设备超级表 + 变量子表” | ✅ 已完成（待用户审阅） | 已完成核心设计文档口径修订，并重写 `docs/数据库` 下的 TDengine seed / verify / cleanup SQL 模板；旧“设备子表混存多变量”的执行口径已标记失效，后续待用户审阅通过后再修改实际写入 Python 脚本与运行时代码 | 是否进入下一步“修改 TDengine 写入脚本与 collector / backend 实现” |
| STEP-013 | 2026-04-14 | 生成 TDengine 全量设备建库脚本，覆盖三类设备数据库、全量设备超级表与抽样测试子表 | ✅ 已完成（待用户审阅） | 已新增 `docs/数据库/tdengine_full_device_schema.sql` 与 `docs/数据库/TDengine全量设备建库说明.md`，脚本会创建 `qc` / `asc` / `agv` 三个数据库、QC 29 台 / ASC 121 台 / AGV 155 台的全部设备超级表，并为每类设备额外创建 1-2 台测试设备的变量子表；当前 ASC 名单按“`asc7011` + `asc7021/asc7022` ... `asc7611/asc7612`”生成以匹配 121 台总量 | 是否确认 ASC 全量编码口径无误，并进入 collector 建表/写入逻辑改造 |
| STEP-014 | 2026-04-18 | collector 适配最新 MySQL live schema，并切换 TDengine 写入口径到变量子表模型 | ✅ 已完成（待用户审阅） | 已改造 `collector/core/config.py`、`collector/core/config_db.py`、`collector/storage/tdengine.py`、`collector/storage/batch_writer.py`、`collector/main.py`：运行时补充 `device.id` 与 `var_seq`，TDengine 初始化改为”设备类型数据库 -> 设备超级表 -> 变量子表”，子表名统一由 `device_code + '_' + var_seq` 派生，写入改为按变量子表分组且不再通过篡改时间戳规避冲突；同时补充了针对新存储模型的单元测试 | 是否进入下一步”用本机 MySQL/TDengine 真实验证 collector 新链路” |
| STEP-015 | 2026-04-18 | Perspective 集成设计：历史数据页面可视化增强 | ✅ 已完成 | 已创建 `docs/设计方案/Perspective集成设计.md`，明确采用客户端/服务器复制模式，仅改造历史数据页面，实时监控页保持不变；已同步更新技术方案到 v2 版本 | 是否进入下一步”后端 Perspective Server 实现” |
| STEP-016 | 2026-04-19 | Perspective 接入执行拆解与后端基础改造启动 | ✅ 已完成（待用户审阅） | 已完成 `docs/后端/Perspective会话隔离与保护策略.md`，并同步项目 TODO；后端已补 Perspective 依赖声明、配置项、会话管理骨架与 `WS /api/device-monitor/ws/perspective` 基线路由 | 是否进入下一步“补齐历史查询真实后端并打通查询结果落表 Perspective Table” |
| STEP-017 | 2026-04-19 | Perspective 第二步：补齐历史查询后端首版与快照推送链路 | ✅ 已完成（待用户审阅） | 已新增 `history/devices`、`history/points`、`history/query`、`history/export` 首版后端路由与服务层；`history/query` 已支持 `X-Perspective-Session-Id` / `X-Perspective-Query-Version`，并可将标准化 records 写入会话快照后通过 `WS /api/device-monitor/ws/perspective` 推送 `snapshot` | 是否进入下一步“真实接口复验 + 前端 Perspective 客户端接入准备” |
| STEP-018 | 2026-04-19 | Auth / Permission 真实数据闭环第一步：补齐数据库模型、bootstrap、登录与菜单接口 | ✅ 已完成（待用户审阅） | 已在后端补齐 auth 8 张表 ORM、默认账号 / 角色 / 菜单 / 权限种子、`login / logout / user/info` 与 `system-manage` 首版接口，并同步开始更新数据库 / API / 项目 TODO 文档 | 是否进入下一步“真实登录复验 + 前端切换验收” |
| STEP-019 | 2026-04-19 | 真实 HTTP 登录验证通过，问题重心转移到页面业务数据缺口 | ✅ 已完成 | 已确认 `login / user/info / menus / user-list / role-list` 可真实返回，前端登录恢复；同时识别出 `driver / device / point / dashboard / log / ws` 为当前页面无数据主因，并将后续动作转入接口补齐阶段 | 是否进入下一步“按页面缺口优先级补业务接口与真实统计数据” |
| STEP-020 | 2026-04-20 | Dashboard / Log 第二轮收口：补真实概览接口并为缺 schema 接口加兼容兜底 | ✅ 已完成 | 已新增 `dashboard` / `log` 路由与服务层：`overview`、`device-type-distribution`、`trend`、`device-connection-health` 已接入当前 MySQL / TDengine 真实读链路；当时 `alarm-distribution`、`recent-events`、`log/list` 对缺表做兼容兜底。2026-05-06 起告警 / 日志表已落地，后续应切真实查询口径 | 进入 Dashboard 告警侧与日志页真实数据收口 |
| STEP-021 | 2026-04-21 | 五页优先收口第三轮：修共享兼容层、补 Device/Point 新增接口与 Realtime WS 首版 | ✅ 已完成（待用户审阅） | 已修复 TurboAPI 兼容层的路径参数类型问题与 WebSocket ASGI fallback 缺口；补齐 `POST /api/device-monitor/device`、`POST /api/device-monitor/point`、`WS /api/device-monitor/ws/realtime`，并通过本地 ASGI 对真实 MySQL 写入、详情回查、`subscribe -> snapshot -> ping-pong` 完成复验 | 是否进入下一步“重载当前运行中的后端进程，并对 Dashboard / Driver / Device / Point / Realtime 五页做页面级真实联调” |
| STEP-022 | 2026-04-21 | 五页前后端代理联调：验证 3006 -> 8000 实际链路并定位 Realtime WS 运行时阻塞 | ✅ 已完成（待用户审阅） | 已通过前端代理实测 `Dashboard / Driver / Device / Point / Realtime-Init` 的 HTTP 链路全部返回 `200`，并成功经 3006 代理新增测试设备与点位；同时定位到当前 Python 3.14t 运行环境缺少 `uvicorn` WebSocket 运行时依赖（`websockets` / `wsproto` 均未安装），导致 `ws/realtime` 握手回退为 HTTP `404`；仓库已补 `backend/requirements.txt`、开启前端 `VITE_DEVICE_MONITOR_WS_ENABLED`，并收紧新增弹窗长度校验 | 是否进入下一步“安装 WebSocket 运行时依赖、重启前后端，并复验 Realtime 页面自动建连” |
| STEP-023 | 2026-04-21 | 五页前后端收尾联调：复验 8000 直连 WS，修正 3006 开发代理缺少 WS 转发的问题 | ✅ 已完成（待用户审阅） | 已确认 `ws://127.0.0.1:8000/api/device-monitor/ws/realtime` 可完成 `subscribe -> snapshot -> pong`，说明后端运行时依赖与路由均已恢复；同时定位到 `frontend/vite.config.ts` 的 `/api` 代理缺少 `ws: true`，仓库已修正 | 是否进入下一步“重启前端开发服务，并完成 Realtime 页面最终冒烟” |
| STEP-024 | 2026-04-21 | 五页前后端最终命令级冒烟：在前后端重启后复验 3006 代理与 8000 直连全链路 | ✅ 已完成（待用户审阅） | 已确认 `3006` 与 `8000` 均可访问；经 `3006` 代理的 `login / user/info / dashboard / driver / device / point / realtime-init` 均返回 `200`；`ws://127.0.0.1:3006/api/device-monitor/ws/realtime` 与 `ws://127.0.0.1:8000/api/device-monitor/ws/realtime` 均可完成 `subscribe -> snapshot -> pong`，五页优先范围的命令级联调已收口 | 是否进入下一步“做页面人工点验，或转入下一阶段接口实现” |
| STEP-025 | 2026-04-21 | 五页优先补齐改删能力：完成 Device / Point 编辑删除接口与真实联调验证 | ✅ 已完成（待用户审阅） | 已补齐 `device` / `point` 的 `PUT`、单删与批量删（设备批量删、点位批量删后端）接口；点位页已补单条删除入口；并仅使用联调新增数据完成“设备编辑、点位编辑、点位单删、设备单删级联点位、设备批量删”真实验证 | 是否进入下一步“补前五页导入导出，或转做 Dashboard 告警 / Log” |
| STEP-026 | 2026-04-21 | 五页导入导出与 Dashboard 告警侧收口：补 xlsx 模板导入导出、导入前说明与 synthetic alarm 兜底 | ✅ 已完成（待用户审阅） | 已补齐 `device/export`、`device/import`、`point/export`、`point/import` 的 `.xlsx` 模板/导出/部分成功能力；前端 Device / Point 页新增导入前格式说明、模板下载与失败明细提示，并改为本地读取 `.xlsx` 后以 `rows` JSON 调导入接口；Dashboard 的 `recent-events / alarm-distribution` 已改为真实优先、无可用告警样本时自动返回后端 synthetic alarm 数据 | 是否进入下一步“做页面级导入导出冒烟 + Dashboard 告警展示验收，或继续推进前五页人工点验” |
| STEP-027 | 2026-04-22 | History / Perspective 第一步：恢复前端 TypeScript 基线并清理依赖解析阻塞 | ✅ 已完成（待用户审阅） | 已将 `frontend/tsconfig.json` 切到 `moduleResolution=bundler`，修复 `history/index.vue` 的 Checkbox / tuple 类型错误，并补最小 `@wangeditor/editor-for-vue` 声明兜底；当前 `frontend` 执行 `vue-tsc --noEmit` 已重新通过，为后续 History 会话协议接入和主渲染切换清空基线阻塞 | 是否进入下一步“接入 History 的 Perspective 会话协议并打通前端 WS 生命周期” |
| STEP-028 | 2026-04-22 | History / Perspective 第二步：接入前端会话协议并打通 WS 生命周期 | ✅ 已完成（待用户审阅） | 已在 History 页引入 `sessionStorage` 级 `perspectiveSessionId`、页面级 `connectionId`、递增 `queryVersion`，并让 `history/query` 携带 `X-Perspective-Session-Id` / `X-Perspective-Query-Version`；同时已接通 `WS /api/device-monitor/ws/perspective`，完成 `snapshot / session_restored / session_conflict / pong` 首版处理与本地 Perspective Table 同步，`frontend` 再次通过 `vue-tsc --noEmit` | 是否进入下一步“移除 ECharts 主渲染并将 History 页切换到 Perspective Viewer 主视图” |
| STEP-029 | 2026-04-22 | History / Perspective 第三步：切换主渲染到 Perspective Viewer 并统一导出格式 | ✅ 已完成（待用户审阅） | 已将 History 页从“ECharts 趋势图 + 下方表格”切换为单一 Perspective Viewer 主视图，默认插件改为 `Y Line`，并提供 `Datagrid` 切换；同时保留原查询抽屉与导出入口，并将历史导出文件名统一为 `.csv`，`frontend` 在改造后继续通过 `vue-tsc --noEmit` | 是否进入下一步“做真实联调复验并补记录剩余风险” |
| STEP-030 | 2026-04-22 | History / Perspective 第四步：完成命令级真实联调复验并修正历史数据子表回退 | ✅ 已完成（待用户审阅） | 已通过 `3006 -> 8000` 实际链路复验 `history/query`、`history/export`、`WS /ws/perspective`；其中 `snapshot / session_restored / session_conflict / pong`、超 24h 拒绝、超 20 点拒绝均已返回符合预期的真实结果；同时在复验中发现历史查询优先按 `var_code` 命中空子表会漏掉真实 `deviceCode_pointId` 表，现已补回退逻辑并确认 `qc100_1` 可返回真实记录 | 是否进入下一步“请你刷新前端页面并做一次人工点验，我再根据结果继续收尾” |
| STEP-031 | 2026-04-22 | History / Perspective 第五步：修复页面查询失败表现、查询成功收起抽屉与操作区对齐 | ✅ 已完成（待用户审阅） | 已在 History 页补 `perspective-viewer` 元素就绪等待，并将渲染入表时间字段改为 `datetime`；同时为 `Y Line` 渲染失败补自动回退 `Datagrid`，查询 HTTP 成功后即收起抽屉，并修正右上按钮区对齐，避免“数据已返回但页面仍报查询失败”的体验问题 | 是否进入下一步“请你刷新页面复验 History 查询、抽屉收起和顶部按钮对齐表现” |
| STEP-032 | 2026-04-22 | History / Perspective 第六步：修复查询持续 loading 与 HTTP/WS 双路径并发渲染卡住问题 | ✅ 已完成（待用户审阅） | 已将 History 页的 Perspective 渲染改为串行队列，避免 `history/query` 成功回包与 `WS snapshot` 同时触发表格重建时互相删除 Table 导致页面卡在“查询中”；同时改为 HTTP 查询成功后立即结束 loading，仅保留后续渲染独立进行 | 是否进入下一步“请你刷新页面再次复验，确认不再一直显示查询中” |
| STEP-033 | 2026-04-22 | History / Perspective 第七步：确认当前“无数据”现象与 TDengine 样本覆盖范围有关 | ✅ 已完成（待用户审阅） | 已通过 `3006 -> 8000` 真实查询复核：`deviceId=1(qc100)` + `pointId=1(Input)` 在 `2026-04-15 00:00:00 ~ 23:59:59` 可返回 2 条记录；同设备的 `pointId=2/3` 及 `deviceId=2..5` 的 `pointId=1` 目前均返回 0 条。当前历史页若显示“无数据”，更可能是所选设备/点位缺少样本，而不是前后端链路再次断开 | 是否进入下一步“统一补齐更多设备/点位的 TDengine 假数据覆盖，减少页面误判为故障” |
| STEP-034 | 2026-04-22 | History / Perspective 第八步：修复 `perspective-viewer` 初始化等待可能长期挂起的问题 | ✅ 已完成（待用户审阅） | 已将 History 页的 `perspective-viewer` 初始化从无限等待 `whenDefined()` 改为“动态补加载 + 短超时兜底”，避免浏览器运行时未及时注册自定义元素时，查询已成功但页面始终停留在空状态 | 是否进入下一步“请你刷新页面再用 `qc100/Input/2026-04-15` 组合复验，确认视图能真正渲染出来” |
| STEP-035 | 2026-04-22 | History / Perspective 第九步：参考官方 demo 修复 Perspective wasm 资源缺失 | ✅ 已完成（待用户审阅） | 已阅读 `perspective-connect-demo-main`，确认官方示例使用 CDN 入口自动初始化 wasm；结合当前 npm/Vite 接入，现已在 History 页显式初始化 `@finos/perspective` 的 `perspective-server.wasm` 与 `@finos/perspective-viewer` 的 `perspective-viewer.wasm`，用于修复浏览器报错 `Missing perspective-client.wasm` | 是否进入下一步“请你刷新前端后再次验证 History 图表是否已真正渲染” |
| STEP-036 | 2026-04-22 | History / Perspective 第十步：修复 `perspective-viewer` 重复注册与 Table 销毁冲突 | ✅ 已完成（待用户审阅） | 已为 History 页补上 `perspective-viewer` wasm 初始化的全局单例守卫，避免重复执行 `customElements.define('perspective-viewer', ...)`；同时在每次重建查询结果前先销毁旧 Viewer，再删除旧 Table，修复 `Cannot delete table with views`；`frontend` 已再次通过 `vue-tsc --noEmit` | 是否进入下一步“请你刷新页面后再次验证 History 查询是否已正常渲染” |
| STEP-037 | 2026-04-22 | History / Perspective 第十一步：修复快照回写时将查询时间缩成样本时间的问题 | ✅ 已完成（待用户审阅） | 已调整 History 页 `snapshot` 同步逻辑：当当前快照属于本次查询版本时，优先保留用户原始查询的 `startTime / endTime`，不再把时间范围自动改写为首末样本点时间；`frontend` 已再次通过 `vue-tsc --noEmit` | 是否进入下一步“请你刷新页面后再次验证查询时间范围是否保持为你手动选择的区间” |
| STEP-038 | 2026-04-22 | History / Perspective 第十二步：修复 Viewer 空白与 Query 标签被快照噪声覆盖的问题 | ✅ 已完成（待用户审阅） | 已调整 History 页渲染顺序：先显示结果容器、等待 DOM 可见后再创建 `perspective-viewer`，并在挂载时补显式宽高、`flush()` 与 `resize(true)`，修复“无报错但 Viewer 空白”；同时保留当前本地查询版本，不再让右上角 Query 标签被后续快照反复改写；`frontend` 已再次通过 `vue-tsc --noEmit` | 是否进入下一步“请你刷新页面后再次验证 History 表格/图表是否已正常显示” |
| STEP-039 | 2026-04-22 | History / Perspective 第十三步：修复历史值被错误归一化与点位子表映射不准的问题 | ✅ 已完成（待用户审阅） | 已修正后端 `history` 服务的 TDengine 子表解析逻辑，避免像 `11901` 这类点位编码被错误解析成裸表名导致查不到 `qc100_11901`；同时调整 `BOOL` 历史值归一化，仅对真实 `0/1` 布尔值做折算，避免把 `12.83` 这类模拟量误压成 `1`；已通过 `compileall` 语法复验 | 是否进入下一步“请你重启后端后再次验证 History 显示值是否与 TDengine 真实值一致” |
| STEP-040 | 2026-04-24 | varSeq 基线专项审计：盘点仓库内 `var_code / pointId / pointIds / point_id` 残留并分类 | ✅ 已完成（待用户审阅） | 已按“`device_variable/template_variable` 业务键统一收口到 `var_seq`、TDengine 子表统一为 `device_code + '_' + var_seq`、QC 主显示点为 `30 / 11894 / 11900`”的新基线完成仓库审计：当前高优先级残留主要集中在 `history` 查询契约、`point` CRUD 路由/服务、`realtime/dashboard` 载荷字段以及前端 `history/realtime` 类型与页面状态；同时已区分“必须尽快切换的业务契约残留”与“可暂留的旧子表/旧 TAG 兼容回退、以及告警/日志表中的关系型 `point_id` 外键” | 是否进入下一步“按 P0 运行时残留顺序逐步改造接口契约并同步刷新文档” |
| STEP-041 | 2026-04-24 | varSeq 收口第一步：完成 History 查询/导出/Viewer 的业务键迁移 | ✅ 已完成（待用户审阅） | 已将 History 的主业务键从 `pointId(s)` 切到 `varSeq(s)`：后端 `history/points` 现返回 `varSeq`，`history/query` 与 `history/export` 主契约改为 `varSeqs`，返回值与 Perspective snapshot 记录统一改为 `varSeq`；前端 History 页的选择状态、查询参数、结果映射和本地 Perspective Table 也已同步改造。同时保留后端对旧 `pointIds` 入参与旧 TDengine 子表 / `variable_id` TAG 的兼容回退，避免存量历史数据联调回归；已通过 `uv run python -m compileall backend/app/api/routes/history.py backend/app/services/history.py` 与 `frontend/node_modules/vue-tsc/bin/vue-tsc.js --noEmit` 复验 | 是否进入下一步“重启后端后优先做 History 页面真实联调，再继续切 Realtime / Dashboard 的 varSeq 口径” |
| STEP-042 | 2026-04-24 | varSeq 收口第二步：完成 Realtime 载荷迁移、Dashboard 趋势选点清理与 Point 载荷补正 | ✅ 已完成（待用户审阅） | 已将 `monitor/realtime-init` 与 `WS /ws/realtime` 的点位载荷主键统一改为 `varSeq`，前端 Realtime 页的变化检测状态也同步从 `pointId` 切到 `varSeq`；同时将 Dashboard 趋势选点改为仅选取合法 `var_seq` 点位，内部保留 `legacy_point_id` 仅用于旧 TDengine 子表 / `variable_id` TAG 兼容回退；另外已补齐 Point 的前端类型与表单字段，确保 `PointListItem / PointForm / 点位详情/编辑弹窗` 显式承载 `varSeq`。当前 Point 详情/编辑/删除路由仍保留 `{id}` 技术定位键，因为 `varSeq` 仅在设备内唯一，若要彻底切换需单独设计复合定位契约（如 `deviceId + varSeq`） | 是否进入下一步“重启后端后做 Realtime 真机联调，再单独评审 Point CRUD 的复合定位契约设计” |
| STEP-043 | 2026-04-24 | varSeq 收口第三步：补齐 Point CRUD 复合定位兼容与点位导入字段映射 | ✅ 已完成（待用户审阅） | 已为 `GET/PUT/DELETE /api/device-monitor/point/{id}` 增加 `deviceId + varSeq` 复合定位兼容，后端优先按“设备内业务键”解析点位，仍保留 `{id}` 技术定位键回退；前端 Point API 已同步在详情/删除/编辑链路附带复合定位信息，编辑弹窗会保留原 `locatorDeviceId / locatorVarSeq` 以支持修改点位序号；同时修正点位导入前端漏传“点位序号(varSeq)”的问题，并让点位详情抽屉走真实详情接口；已通过后端 `compileall` 与前端 `vue-tsc --noEmit` 复验 | 是否进入下一步“在你当前已启动的 3006 / 8000 环境下做 Point 页真实联调冒烟，并继续扫剩余页面异常” |
| STEP-044 | 2026-04-24 | 五页联调扫尾：修复 Realtime 设备树状态字段映射与 Driver 未使用状态显示 | ✅ 已完成（待用户审阅） | 已在前端补齐 Realtime 设备树节点的 `connectStatus` 类型与映射，修复设备树一直读 `status` 导致节点默认离线的问题；同时修正 Driver 页状态文案，将后端返回的 `unused` 正确显示为“未使用”，避免全部驱动都被前端误标成“可用”；已通过 `frontend/node_modules/vue-tsc/bin/vue-tsc.js --noEmit` 复验，并再次通过 `3006` 代理确认实时树与驱动列表真实返回正常 | 是否进入下一步“继续做页面级人工冒烟，还是先由我汇总当前剩余问题与风险” |
| STEP-045 | 2026-04-24 | Point 模板导入：在导入弹窗新增“从模板导入”，按设备类型从 MySQL 模板点位追加导入 | ✅ 已完成（待用户审阅） | 已新增 `POST /api/device-monitor/point/import/template`：前端导入说明弹窗现支持“下载模板 / 从模板导入 / 继续导入”三种动作；后端会基于当前所选设备优先选择其已绑定启用模板，否则回退到该设备类型下最新启用模板，并仅将模板中当前设备尚不存在的 `var_seq / var_address` 追加到 `device_variable`；已通过后端 `compileall` 与前端 `vue-tsc --noEmit` 复验 | 是否进入下一步“由你在页面上选中设备后实际点一次‘从模板导入’，我们再根据结果继续收口” |
| STEP-046 | 2026-04-25 | 前六页性能收口第一步：减少重复初始化与重型首屏加载 | ✅ 已完成（待用户审阅） | 已完成一轮最小性能修正：Dashboard 现在会在 `KeepAlive` 重新激活后的 10 秒内复用现有数据，避免短时间重复触发 6 组统计请求；Realtime 去掉 WebSocket 建连后的重复 `realtime-init` 拉取；History 改为首屏不主动初始化 Perspective Viewer，仅在收到快照或实际查询时再加载 wasm；Point 在带 `deviceId/deviceCode` 路由参数进入时，不再先请求一次“全量点位列表”再二次按设备筛选。已通过 `frontend/node_modules/vue-tsc/bin/vue-tsc.js --noEmit` 复验 | 是否进入下一步“请你在 3006 前端中再次切换 Dashboard / Point / Realtime / History 体感确认是否明显变快” |
| STEP-047 | 2026-04-25 | 前六页性能收口第二步：拆共享重依赖并补页面壳 loading 占位 | ✅ 已完成（待用户审阅） | 已完成第二轮“秒开优先”收口：共享 `frontend/src/api/device-monitor.ts` 已移除顶层 `xlsx` 依赖，导入解析改为调用时动态加载；Device / Point / History 导出改为懒加载 `file-saver`；History 新增 `perspective-runtime.ts`，仅在真实查询时动态加载 Perspective runtime、插件和 wasm；Dashboard 顶层 `echarts` 改为异步加载，并调整为先拉 `overview`、再补趋势/分布/健康/近期事件；同时为 Dashboard / Device / Point / History 增加 SVG loading 占位，开发环境通过 `VITE_ENABLE_VUE_DEVTOOLS=false` 默认关闭 Vue DevTools。已通过 `D:\\ruanjian\\node.js\\node.exe .\\node_modules\\vue-tsc\\bin\\vue-tsc.js --noEmit` 复验 | 是否进入下一步“请你再切换 Dashboard / Device / Point / History 体感确认首开是否进一步变快；若仍慢，再继续做关键路由预热或后端分页下钻” |
| STEP-048 | 2026-04-25 | 前六页性能收口第三步：补长耗时切页内容区反馈，并继续缩小首次打开模块负担 | ✅ 已完成（待用户审阅） | 已为路由切换补“超过 0.5 秒才显示”的主内容区 SVG loading，判断仍接入 `router beforeEach / afterEach`，但反馈层渲染在 `ArtPageContent` 内，保留侧边导航与顶部栏可见；同时把 Device / Point 的对话框与详情抽屉模块改为 `defineAsyncComponent` 懒加载，避免首次进入页面时一并编译。已通过 `D:\\ruanjian\\node.js\\node.exe .\\node_modules\\vue-tsc\\bin\\vue-tsc.js --noEmit` 复验 | 是否进入下一步“进入页面设计改造；性能预热先记录为待办，不继续执行” |
| STEP-049 | 2026-04-26 21:46 | collector 生产接入前安全修复：PLC 只读安全、S7 正式地址、写入可靠性、重连状态闭环 | ✅ 已完成（待小范围真机冒烟） | 已按真实接入前审计结论修复 S7 `ping()` 禁止调用 `plc_run`、新增 S7 `%I/%Q/%M/%MB/%MW/%MD` 与标准 `DBX/DBB/DBW/DBD` 地址支持、移除旧式 `DB100.0` 兼容、为 S7/GE/OPC 读操作增加同连接串行锁；已将 S7/GE/OPC UA 内置驱动统一收口为只读，OPC UA `write()` 也改为拒绝写入；已改造 `DataAggregator -> BatchWriter` 为受管理写入任务，补部分子表失败重试/报错、重复时间戳/非数值统计、停机 flush/drain；已接入设备连接/断开/异常/重连的 MySQL `device.status` 与 `plc_connection_log` 写回；已修复错误处理器、按协议校验 S7/GE/OPC 地址，并更新 collector README/采集模块地址与只读说明。已通过 `python3.14t.exe -m compileall main.py core device drivers collector data_queue storage monitor tests`、S7/GE 地址规则脚本、OPC UA 只读脚本；用户本机 collector `.venv` 已通过重点 pytest：`39 passed`；`enable=0` idle 启动成功，确认 devices=0、tasks=0、queue=0、aggregator_cache=0，TDengine WS、HTTP ingest、HealthChecker 正常启动；同时已修复 pytest 中 `asyncio.iscoroutinefunction` 弃用警告 | 下一步仅剩真实 PLC 接入前的小范围只读冒烟：建议只启用 1 台设备 + 少量 `%I/%M/DBD` 测试点，观察连接日志、采集读数、TDengine 子表写入与设备状态回写 |
| STEP-050 | 2026-04-26 23:02 | AGV 32 位 OPC DA 接入方案评估与外部推送模式前置改造 | 🔄 待边车服务化 | 已确认 AGV 的 ZPMC 自研 OPC 数据源是 32 位 OPC DA，不能直接由当前 64 位 Python 进程稳定加载 32 位 COM/OPC DA 组件；推荐采用“32 位 Windows 边车采集进程/服务 + 本机 HTTP/gRPC/ZeroMQ 转发给 collector”的隔离方案，collector 主进程继续保持 64 位 Python 与现有 TDengine/MySQL 链路。Rust 可以读取 OPC DA 的前提仍是编译成 32 位 Windows 程序并通过 COM/DCOM 调用，不能解决单进程 32/64 位混载问题；更适合做 32 位边车服务，而不是被 64 位 Python 直接嵌入调用。已新增文档 `docs/采集模块/AGV_OPC_DA_接入方案.md`；代码侧已支持 `protocol=opcda_sidecar/http_push` 外部推送设备：参与拓扑与 TDengine target 初始化，但不创建 PLC driver、不创建周期采集任务；直接 `protocol=opcda` 会在启动校验阶段明确拒绝。已新增 x86 C# OPC DA 探针并编译成功，确认 `ZPMC.OPCServer` 可连接，自动识别 ItemID 格式为 `OPD_Server.<AGV设备>.ViewMain.<Tag>`；AGV801 与 AGV802 的 `AddItems/Read` 均成功，但返回值均为 `<null>`、质量码 `0x0000`，倾向于 OPC Server 模拟/点位质量未给出有效值，而不是客户端连接或 ItemID 问题 | 推荐优先级：A. 32 位 C#/.NET Framework OPC DA 边车（置信度 0.85）；B. 32 位 Python/pywin32 边车（0.70）；C. 32 位 Rust 边车（0.55）；D. 让 ZPMC 提供 OPC UA 网关/桥接（0.80，取决于现场支持）；下一步把探针升级为常驻边车服务并对接 collector HTTP ingest |
| STEP-051 | 2026-04-26 23:56 | AGV OPC DA tag 批量推送 sidecar 基线落地 | 🔄 待中心映射入口 | 已在 `collector/sidecars/opcda_push_service/` 新增 C#/.NET Framework x86 推送服务基线，支持配置 `device/group/csv/points`，按 `OPD_Server.<AGV>.ViewMain.<Tag>` 读取本地 OPC DA Item，并以 `opcda_tag_batch_v1` 批量推送 `device_id / group / tag / value / quality / timestamp` 到中心；该模式不要求 sidecar 携带中心 `variable_id`，后续由中心按 `device_id + group + tag` 统一换算到 `device_variable.id / var_seq`。已新增 `build.ps1`、`run.bat`、`opcda_push_config.example.json` 和 README，并通过 x86 C# 编译验证；同时更新 AGV OPC DA 接入方案、collector README 与采集模块结构/代码逻辑文档。 | 下一步中心 collector 需新增 `opcda_tag_batch_v1` 接收/映射入口；生产部署前还需补 Windows Service/任务计划安装脚本、本地失败队列、日志滚动和按 MySQL 拉取点位配置 |
| STEP-052 | 2026-05-06 | collector 系统日志接入 | ✅ 已完成 | 已新增 collector 异步系统日志写入器，复用 MySQL `system_log` 与 `log_retention_policy`；已接入采集服务启动/关闭、TDengine 连接、设备上线/离线/异常状态、采集读取失败、每分钟采集摘要、TDengine 写入失败、每分钟存储摘要和异常堆栈；DEBUG 日志默认不落库，受策略表开关控制。已通过 `compileall` 与轻量 smoke 检查；相关 pytest 可启动但暴露既有 TDengine mock 与聚合器旧期望问题，不属于本次日志接入新增故障。 | 后续对接后端日志列表、统计、策略、清理和 DEBUG 管理接口后，在前端系统日志页做端到端点验 |
| STEP-053 | 2026-05-07 | collector 测试前全面复审 | ✅ 已完成 P1 修复 | 已新增 `docs/采集模块/collector测试前代码审查报告_2026-05-07.md`，复审主链路、S7/GE/OPC、sidecar、聚合、TDengine、日志与测试。语法编译通过；pytest 临时跑法 69 passed / 7 failed。已完成 P1-1（OPC DA center 端口映射）、P1-2（聚合器失败批次保留）、P1-3（数据类型口径统一）、P1-4（opc_server 必填校验）、P1-5（venv 路径修复后重新安装依赖）。P2 问题已记录：modbus 放行、DSN 特殊字符、毫秒周期上取整 | 进入长时间系统测试；S7/GE 需做小范围真机/模拟 smoke |
| STEP-054 | 2026-05-08 | 日志系统 UI 问题修复 | ✅ 已完成 | 已修复日志终端三个问题：1) 暂停计数器死代码（暂停后不再计数）；2) pending 列表重复累计（pending_count 改为实时计算）；3) 分页滚动跳回顶部（保持当前滚动位置）。已通过语法复验与功能点验 | 继续做日志列表真实数据联调 |
| STEP-055 | 2026-05-08 | 实时监控页面重新设计 | 🔄 待 Perspective 联调 | 已按 `docs/前端/实时监控核心页面.md` 完成页面重新设计：删除旧设备树和实时数据面板，改用顶部控制栏（设备选择按钮+点位选择长框+操作按钮）、中央 Perspective 趋势图（折线图/状态图切换）、右侧点位卡片容器（实时监测点）、底部系统状态栏的四段式布局。当前待后端依赖安装完成后启动联调 | 下一步：修复后端虚拟环境，安装依赖，启动联调 |

---

## 十三、变更记录

| 日期 | 变更内容 | 变更人 |
|------|----------|--------|
| 2026-05-08 | 完成 STEP-055：实时监控页面按设计文档重新设计，删除旧布局，改用顶部控制栏+中央趋势图+右侧点位卡片+底部状态栏的四段式布局 | `@frontend` |
| 2026-05-08 | 完成 STEP-054：日志系统 UI 问题修复，包括暂停计数器、pending 重复累计、分页滚动跳回顶部 | `@frontend` |
| 2026-05-08 | 完成 STEP-053 P1 系列修复：OPC DA center 端口映射、聚合器失败批次保留、数据类型口径统一、opc_server 必填校验、venv 路径修复 | `@collector` |
| 2026-05-07 | 完成 collector 测试前全面复审：新增 `collector测试前代码审查报告_2026-05-07.md`，记录 P1/P2 待修复项与 pytest 69 passed / 7 failed 状态 | `@collector` |
| 2026-05-06 | 完成 collector 系统日志接入：新增异步 `SystemLogWriter`，复用 `system_log / log_retention_policy`，接入启动关闭、设备状态、采集读取失败/摘要、TDengine 写入失败/摘要与异常栈；DEBUG 默认不落库并受策略开关控制，已通过 compileall 与轻量 smoke 检查 | `@collector` |
| 2026-04-26 | 完成 STEP-049：collector 已完成生产接入前安全修复代码收口，重点包括 PLC 只读边界、S7 正式地址与协议化校验、受管理 TDengine 批量写入、停机 flush/drain、设备状态/重连日志闭环；已通过 compileall、轻量脚本校验、用户本机重点 pytest（39 passed）与 `enable=0` idle 启动复验；同时清理 collector 内 `asyncio.iscoroutinefunction` 弃用 API 残留 | `@collector` |
| 2026-04-26 | 新增 STEP-050：记录 AGV 32 位 OPC DA 接入评估，初步推荐采用 32 位边车服务隔离 OPC DA COM 位数问题，再通过 HTTP/gRPC/ZeroMQ 转发到 64 位 collector | `@collector` |
| 2026-04-26 | 推进 STEP-050：新增 AGV OPC DA 接入方案文档，并在 collector 中补 `opcda_sidecar/http_push` 外部推送设备模式；直接 `opcda` 协议会启动前拒绝，避免 64 位 Python 误连 32 位 OPC DA | `@collector` |
| 2026-04-26 | 推进 STEP-050：新增并编译 x86 C# OPC DA 探针，已在本机 ZPMC OPC Server 上验证 `OPD_Server.AGV801/AGV802.ViewMain.Tag...` 路径可 `AddItems/Read`，但当前返回 `<null>` 与质量码 `0x0000`，初判为 OPC Server 模拟/点位质量状态问题 | `@collector` |
| 2026-04-26 | 新增 STEP-051：落地 AGV OPC DA tag 批量推送 sidecar 基线，新增 `collector/sidecars/opcda_push_service/`，支持按 `device/group/csv/points` 配置读取 OPC DA 并推送 `opcda_tag_batch_v1`，中心后续按 `device_id + group + tag` 统一换算点位 | `@collector` |
| 2026-04-21 | 完成 STEP-026：已补齐 Device / Point 的 `.xlsx` 模板导入导出、导入前说明与部分成功反馈；并将 Dashboard 的 `recent-events / alarm-distribution` 从兼容空结构升级为“真实优先 + synthetic alarm 兜底” | `@coordinator` |
| 2026-04-22 | 完成 STEP-027：已恢复 History / Perspective 的前端 TypeScript 基线，`frontend` 重新通过 `vue-tsc --noEmit`；同时以最小声明方式兜住 `@wangeditor/editor-for-vue` 的 typings exports 问题，为后续历史页接入清空编译阻塞 | `@coordinator` |
| 2026-04-22 | 完成 STEP-028：已为 History 页接入 `perspectiveSessionId / connectionId / queryVersion` 与 `WS /api/device-monitor/ws/perspective` 首版生命周期，`history/query` 现可携带会话 Header 并接收 `snapshot / session_restored / session_conflict / pong`；同时保持当前 HTTP 查询契约不变，`frontend` 通过第二轮 `vue-tsc --noEmit` 复验 | `@coordinator` |
| 2026-04-22 | 完成 STEP-029：已将 History 页主渲染切换为 Perspective Viewer，移除 ECharts 主图方案，默认使用 `Y Line` 并支持切换到 `Datagrid`；同时统一历史导出文件名为 `.csv`，改造后 `frontend` 继续通过 `vue-tsc --noEmit` | `@coordinator` |
| 2026-04-22 | 完成 STEP-030：已完成 History / Perspective 的命令级真实联调复验，确认 `history/query`、`history/export`、`WS /ws/perspective` 的 `snapshot / session_restored / session_conflict / pong` 与超限保护均可工作；同时修正历史查询在 `var_code` 子表为空时未回退到真实 `deviceCode_pointId` 子表的问题，`qc100_1` 现已可返回真实时序记录 | `@coordinator` |
| 2026-04-22 | 完成 STEP-031：已修复 History 页“查询成功但 Perspective 渲染失败仍提示失败”的前端体验问题，新增 `perspective-viewer` 就绪等待、时间列 `datetime` 入表、`Y Line` 异常自动回退 `Datagrid`、查询成功自动收起抽屉与顶部操作按钮对齐修正 | `@coordinator` |
| 2026-04-22 | 完成 STEP-032：已将 History 页的 Perspective 重建改为串行执行，避免 `history/query` 返回与 `WS snapshot` 并发时互相删除 Table 导致页面长时间停留在“查询中”；同时在 HTTP 查询成功后立即结束 loading，减少前端被渲染阶段卡住的误感知 | `@coordinator` |
| 2026-04-22 | 完成 STEP-033：已复核当前历史页“无数据”现象与 TDengine 样本覆盖相关；现阶段已确认 `qc100 / Input(pointId=1)` 在 `2026-04-15` 有真实记录，而 `qc100` 的 `pointId=2/3` 与 `deviceId=2..5` 的 `pointId=1` 当前均为空，说明页面返回“无数据”在这些选择下符合现状 | `@coordinator` |
| 2026-04-22 | 完成 STEP-034：已将 History 页的 `perspective-viewer` 初始化从无限等待 `customElements.whenDefined()` 调整为“动态补加载 + 短超时兜底”，以修复查询成功但前端仍停留在空结果态的潜在运行时阻塞 | `@coordinator` |
| 2026-04-22 | 完成 STEP-035：已参考 `perspective-connect-demo-main` 的 wasm 初始化方式，补上 `perspective-server.wasm` 与 `perspective-viewer.wasm` 的显式加载，定位并修复浏览器报错 `Missing perspective-client.wasm` 的根因 | `@coordinator` |
| 2026-04-22 | 完成 STEP-036：已为 History 页的 `perspective-viewer` 初始化增加全局单例守卫，避免重复注册自定义元素；同时调整渲染重建顺序为“先销毁旧 Viewer、再删除旧 Table、最后创建新表”，修复 `Cannot delete table with views` 与 `Failed to execute 'define' on 'CustomElementRegistry'` 两类运行时错误，并通过 `vue-tsc --noEmit` 复验 | `@coordinator` |
| 2026-04-22 | 完成 STEP-037：已修复 History 页在接收 `snapshot` 后把时间范围自动缩成“首条样本时间 ~ 末条样本时间”的问题；现在当快照属于当前查询版本时，优先保留用户实际提交的查询区间，仅在缺少查询上下文时才回退到样本时间边界，并通过 `vue-tsc --noEmit` 复验 | `@coordinator` |
| 2026-04-22 | 完成 STEP-038：已修复 History 页“无报错但 Viewer 空白”的显示时序问题，改为结果容器可见后再创建 `perspective-viewer`，并补显式宽高、`flush()` 与 `resize(true)`；同时让右上角 Query 标签优先保留本地查询版本，避免被后续快照反复改写，并通过 `vue-tsc --noEmit` 复验 | `@coordinator` |
| 2026-04-22 | 完成 STEP-039：已修正后端历史查询的子表名解析与布尔值归一化逻辑，避免 `11901` 类点位落不到 `qc100_11901`，以及 `BOOL` 点位把 `12.83` 这类非 0/1 数值误压成 `1`；已通过 `backend/app/services/history.py` 的 `compileall` 语法复验 | `@coordinator` |
| 2026-04-24 | 完成 STEP-040：已围绕最新 `var_seq` 基线完成仓库专项审计，明确运行时仍残留的旧 `pointId / pointIds` 业务契约位置、可暂留的旧子表兼容回退，以及仍需刷新的文档口径，便于后续按 P0/P1/P2 顺序安全收口 | `@coordinator` |
| 2026-04-24 | 完成 STEP-041：已完成 History 页第一轮 `varSeq` 业务键迁移，历史点位列表、查询、导出、Perspective snapshot/本地 Table 与页面状态均已统一改为 `varSeq(s)`，同时保留后端对旧 `pointIds` 入参与旧 TDengine 读取链路的兼容回退；已通过后端 `compileall` 与前端 `vue-tsc --noEmit` 复验 | `@coordinator` |
| 2026-04-24 | 完成 STEP-042：已完成 Realtime 链路的 `varSeq` 迁移，并将 Dashboard 趋势内部选点显式收口到 `var_seq`；同时补齐 Point 前端类型/表单里的 `varSeq` 字段。当前 Point 详情/编辑/删除仍保留 `{id}` 技术定位键，后续若要完全切业务键，需要另行设计复合定位契约 | `@coordinator` |
| 2026-04-24 | 完成 STEP-043：已为 Point 详情/编辑/删除链路补 `deviceId + varSeq` 复合定位兼容，前端 Point API 与编辑弹窗同步保留原始 locator，以支持修改 `varSeq` 时仍准确命中原点位；同时修正点位导入前端漏传“点位序号(varSeq)”的问题，并让点位详情抽屉改走真实详情接口；已通过后端 `compileall` 与前端 `vue-tsc --noEmit` 复验 | `@coordinator` |
| 2026-04-24 | 完成 STEP-045：已为 Point 页导入说明弹窗新增“从模板导入”入口，并补齐 `POST /api/device-monitor/point/import/template`；后端现按当前所选设备优先使用其已绑定启用模板、否则回退到同设备类型最新启用模板，只追加设备上尚不存在的模板点位；已通过后端 `compileall` 与前端 `vue-tsc --noEmit` 复验 | `@coordinator` |
| 2026-04-25 | 完成 STEP-046：已围绕前六页页面切换卡顿做第一轮性能收口，收掉 Dashboard 短时间重复统计请求、Realtime 建连后的重复快照拉取、History 首屏强制加载 Perspective wasm，以及 Point 带路由参数进入时的重复列表请求；已通过 `frontend/node_modules/vue-tsc/bin/vue-tsc.js --noEmit` 复验 | `@coordinator` |
| 2026-04-25 | 完成 STEP-047：已围绕“秒开优先、查询可慢一点”完成第二轮前端性能收口，移除共享顶层 `xlsx`、将 Device / Point / History 的导出改为懒加载 `file-saver`、将 History 的 Perspective runtime 与 Dashboard 的 `echarts` 下沉为按需异步加载，并补 Dashboard / Device / Point / History 的 SVG loading 占位；同时新增 `VITE_ENABLE_VUE_DEVTOOLS=false` 控制开发环境默认关闭 Vue DevTools；已通过 `D:\\ruanjian\\node.js\\node.exe .\\node_modules\\vue-tsc\\bin\\vue-tsc.js --noEmit` 复验 | `@coordinator` |
| 2026-04-25 | 完成 STEP-048：已为路由切换补“超过 0.5 秒才显示”的主内容区 SVG loading，解决首次打开慢页时点击后长时间无反馈的问题，同时避免侧边导航与顶部栏被全屏遮挡；并将 Device / Point 页面里的 `Dialog / Drawer` 子模块改为异步加载，继续压缩首次进入页面的同步编译范围；已通过 `D:\\ruanjian\\node.js\\node.exe .\\node_modules\\vue-tsc\\bin\\vue-tsc.js --noEmit` 复验 | `@coordinator` |
| 2026-04-26 | 推进 STEP-049：collector 生产接入前安全修复已完成第一轮代码落地，覆盖 S7 安全 ping、正式地址解析、同连接串行读、TDengine 写入失败重试/显式失败、停机 flush/drain、设备状态与 `plc_connection_log` 写回、配置校验和文档口径更新；已通过 `python3.14t.exe -m compileall main.py core device drivers collector data_queue storage monitor tests`，pytest 待在 collector `.venv` 安装/可用环境中补跑 | `@coordinator` |
| 2026-04-21 | 完成 STEP-025：已补齐 `device` / `point` 的编辑与删除链路，并仅使用联调新增数据完成真实验证；其中设备支持单删与批量删、点位支持单删，且已验证“设备删除会级联清理其下点位配置” | `@coordinator` |
| 2026-05-06 | 完成系统日志数据库优化：保留旧数据，新增 `log_retention_policy`，补日志查询索引，新增日志导出 / 清理 / 策略 / DEBUG 权限，并新增 `system_log_optimization_*_2026_05_06.sql` 迁移与验证脚本 | `@coordinator` |
| 2026-04-21 | 完成 STEP-024：在前后端重启后复验 `3006` 代理与 `8000` 直连全链路，确认 `login / user/info / dashboard / driver / device / point / realtime-init` 经前端代理均返回 `200`，且 `ws://127.0.0.1:3006/api/device-monitor/ws/realtime` 与 `ws://127.0.0.1:8000/api/device-monitor/ws/realtime` 均可完成 `subscribe / snapshot / pong` | `@coordinator` |
| 2026-04-21 | 完成 STEP-023：已确认 `ws://127.0.0.1:8000/api/device-monitor/ws/realtime` 可真实完成 `subscribe / snapshot / pong`，并定位到前端开发代理缺少 WebSocket 转发；仓库已补 `frontend/vite.config.ts` 的 `/api.ws=true`，待前端重启后做页面级最终冒烟 | `@coordinator` |
| 2026-04-21 | 完成 STEP-022：已通过前端 3006 代理实测 Dashboard / Driver / Device / Point / Realtime-Init HTTP 链路，新增代理链路下的测试设备与点位，并定位到当前运行环境缺少 `websockets` / `wsproto` 导致 `ws/realtime` 握手失败；仓库已补 requirements、开启前端 WS 开关并收紧新增弹窗长度校验 | `@coordinator` |
| 2026-04-21 | 完成 STEP-021：已修复 TurboAPI 兼容层中的路径参数类型与 WebSocket ASGI fallback 问题，补齐 `POST /device`、`POST /point`、`WS /ws/realtime`，并完成基于真实 MySQL 的新增回查与本地 ASGI `snapshot / pong` 复验 | `@coordinator` |
| 2026-04-20 | 完成 STEP-020：已补齐 Dashboard / Log 第二轮后端接口；其中 `overview / distribution / trend / connection-health` 已接入当前真实数据；当时 `alarm-distribution / recent-events / log/list` 因事实表未落地先返回兼容空结构。该缺口已于 2026-05-06 解除，后续按真实查询接入推进 | `@coordinator` |
| 2026-04-19 | 完成 STEP-018：已补齐 auth / permission 真实数据闭环第一步，新增权限域数据库模型、bootstrap、登录与 system-manage 首版接口，并同步更新数据库 / API / 项目 TODO 文档 | `@coordinator` |
| 2026-04-19 | 完成 STEP-019：真实 HTTP 登录验证通过，前端登录恢复；当前主阻塞已从 auth 切换为 device-monitor 页面业务接口与真实统计数据缺口 | `@coordinator` |
| 2026-04-19 | 完成 STEP-017：已补齐历史查询四个后端首版接口，并打通 `history/query -> Perspective 会话快照 -> WS snapshot` 链路；同步更新专项 TODO 状态为推进中 | `@coordinator` |
| 2026-04-19 | 完成 STEP-016：已补 Perspective 会话隔离保护文档，并将 Perspective 接入拆解为专项 TODO；后端已完成依赖声明、配置补齐、会话管理骨架与 `WS /api/device-monitor/ws/perspective` 基线路由接入 | `@coordinator` |
| 2026-04-18 | 完成 STEP-015：完成 Perspective 集成设计，采用客户端/服务器复制模式，仅改造历史数据页面；已创建设计文档并更新技术方案到 v2 | `@coordinator` |
| 2026-04-18 | 完成 STEP-014：collector 已适配最新 MySQL live schema，并切换 TDengine 到变量子表写入模型；同时补充存储映射与批量写入单元测试 | `@coordinator` |
| 2026-04-14 | 完成 STEP-013：新增 TDengine 全量设备建库脚本与说明文档，覆盖 `qc` / `asc` / `agv` 三类数据库、全量设备超级表与抽样测试子表；其中 ASC 先按 121 台总量口径生成 `asc7011` + `asc7021/asc7022` ... `asc7611/asc7612` | `@coordinator` |
| 2026-04-14 | 完成 STEP-012：确认旧 TDengine“设备一表”模型存在同时间戳覆盖问题，统一切换为“设备类型分库 + 设备超级表 + 变量子表”；已同步修订核心设计文档，并重写 TDengine seed / verify / cleanup SQL 模板，等待用户审阅后再修改 Python 写入脚本与运行时代码 | `@coordinator` |
| 2026-04-14 | 完成 STEP-011：前端开发代理已切到本地后端，首轮前后端 HTTP 联调验证通过；同时在开发环境临时关闭 Realtime 页 WS 自动建连，避免 `/ws/realtime` 未完成前反复重连 | `@coordinator` |
| 2026-04-14 | 完成 STEP-010：在 Python 3.14t 下完成本地后端启动与首轮真实接口验证，并确认当前剩余问题已收敛到 TDengine 测试样本时效与点位覆盖 | `@coordinator` |
| 2026-04-13 | 完成 STEP-009：实现 `monitor/device-tree`、`monitor/realtime-init` 首版真实读链路，并补充 `.env` 回退、TDengine 查询助手与 Windows 控制台 UTF-8 启动兜底 | `@coordinator` |
| 2026-04-13 | 完成 STEP-008：在 `backend/` 下建立最小 TurboAPI 后端骨架，并补充 requirements、环境示例与启动说明 | `@coordinator` |
| 2026-04-13 | 完成 STEP-007：核对真实 MySQL live schema，完成本机 MySQL / TDengine 首批测试数据实际落库，并补充可执行的拆分版 verify/cleanup 脚本 | `@coordinator` |
| 2026-04-13 | 完成 STEP-006：扩展首批联调状态覆盖到在线/update、stale/offline、alarm 三类设备，并生成可执行 MySQL/TDengine seed SQL 与执行说明 | `@coordinator` |
| 2026-04-13 | 完成 STEP-005：按当前 collector DDL / ORM / TDengine 命名规则修正首批联调落库清单，并更新验证/清理 SQL 模板 | `@coordinator` |
| 2026-04-13 | 完成 STEP-004：新增首批测试假数据落库清单文档，并更新 MySQL / TDengine 验证与清理 SQL 模板 | `@coordinator` |
| 2026-04-13 | 完成 STEP-003：明确首批 MySQL / TDengine 测试假数据最小集、Realtime 接口查询落点、前端首轮联调字段依赖与顺序 | `@coordinator` |
| 2026-04-13 | 完成 STEP-002：统一当前实际架构为“MySQL 管配置/主数据，TDengine 管实时+历史时序数据，Redis 暂不使用”，并同步修正文档口径 | `@coordinator` |
| 2026-04-13 | 启动分步设计流程，完成 STEP-001：明确 M2-A 首轮设计基线，并补充“真实服务 + 测试假数据、禁用 mock 数据”原则 | `@coordinator` |
| 2026-04-13 | 新增“最小闭环优先执行说明”，明确实时监控闭环优先于历史、日志、统计等扩展模块 | `@doc` |
| 2026-04-13 | 补充 M2-A/M2-B 分层验收、WS 由后端统一归一化、前端设计冲突裁决与 Pencil 设计稿接入规则 | `@doc` |
| 2026-04-13 | 新增前后端联调治理套件，覆盖契约冻结、字段映射、设计适配、权限矩阵、实时/历史态语义和验收口径 | `@doc` |
| 2026-04-11 | 按当前仓库现状重构 TODO 文档，新增治理基线、闭环优先级、阻塞项与风险项 | `@doc` |



## 2026-05-06 系统日志页面问题追踪补充

| 项目 | 状态 | 说明 |
|------|------|------|
| LOG-FIX-001 日志页面服务器错误根因 | ✅ 已完成并测试 | 错误已修复，日志页面功能测试通过 |
| LOG-FIX-002 日志终端未连接 | ✅ 已完成并测试 | 前端已收口，后端复验及测试通过 |
| LOG-FIX-003 前端日志页细节 | ✅ 已完成并测试 | 已修正且功能测试通过 |
| LOG-FIX-004 清理接口风险 | ✅ 已完成并测试 | 风险已排除，接口清理功能测试通过 |

