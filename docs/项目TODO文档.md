# 工业设备数据采集与监控系统 - 项目 TODO 文档

> 版本：2026-06-03 app1.0 后续推进版  
> 说明：本文档已按当前仓库、存档审查记录和 app1.0 文档重新校准。旧版 2026-04-21 的“最小闭环开发”口径已完成或过期，当前 TODO 只保留仍需要推进、验证或增强的事项。

---

## 一、任务状态说明

| 状态 | 含义 |
|------|------|
| `DONE` | 已完成并有代码或文档证据，不再作为 TODO 推进 |
| `TODO` | 尚未完成，后续应安排实施 |
| `VERIFY` | 代码或方案已落地，但需要现场、真实服务或安装包验证 |
| `DEFER` | 明确延后，不阻塞 app1.0 / 当前部署 |
| `OBSOLETE` | 旧口径已被新架构替代，不再作为当前任务 |

---

## 二、当前基线速览（2026-06-03）

| 模块 | 当前状态 | 事实源 | 当前结论 |
|------|----------|--------|----------|
| 采集模块 collector | 主链路已完成：MySQL 加载、协议适配、S7/GE/OPC UA/OPC DA、Redis latest/window/stream、系统日志、PyInstaller 打包均已落地 | `docs/采集模块/采集模块整体结构设计.md`、`docs/存档/审查报告/项目上线前审查报告_2026-05-11.md`、`docs/存档/审查报告/0520审查报告.md` | 当前重点转为现场 PLC 点表核验、长稳压测、质量规则接入 |
| 入库模块 writer | Redis Stream -> TDengine 宽表写入、DLQ、system_log、打包配置已落地 | `docs/入库模块/入库模块代码设计逻辑.md`、`docs/入库模块/入库模块日志方案.md`、`docs/存档/审查报告/0521审查报告.md` | 当前重点转为现场写入压测、少量日志增强 |
| 后端 backend | TurboAPI 主体、认证、设备/点位、实时监控、历史、Dashboard、日志、module-monitor/rates 已落地 | `docs/后端/API接口文档.md`、`docs/设计/技术方案.md`、`docs/app监控/采集入库页真实数据接入方案.md` | 当前重点转为生产配置、容器环境、接口文档同步维护 |
| Vue 前端 frontend | 核心业务页面已完成联调，实时监控、历史、日志、Dashboard 等已落地 | `docs/前端/页面设计文档.md`、`docs/存档/审查报告/项目上线前审查报告_2026-05-11.md` | 当前重点转为现场体验修正与性能细节 |
| DataWatch Agent | FastAPI Agent 已完成 Bootstrap、Auth、进程启停、配置、健康检查、实时日志 tail、PID/flag 管理 | `docs/存档/审查报告/DataWatch Agent 代码审查记录.md`、`docs/app监控/DataWatch-Console-设计方案.md` | 当前重点转为 Windows Service / 安装包 / 远程安全策略 |
| Data Watch Console app | app1.0 已提交：Tauri + React、亮/暗/跟随系统、Agent + Backend 双数据源、模块监控、日志实时/历史、module-monitor/rates 图表 | `docs/app监控/DataWatch-Console-设计方案.md`、`docs/设计/Data Watch Console 页面设计说明.md`、`docs/app监控/采集入库页真实数据接入方案.md` | 当前重点转为本机/目标机安装验证与 UI 小修 |
| 文档体系 | 设计、技术方案、app 监控、暗色模式、运维文档已同步 app1.0 | `docs/README.md`、`docs/设计/技术方案.md`、`docs/app监控/*.md` | 当前 TODO 文档是新的推进入口 |

---

## 三、近期已完成事项（不再作为 TODO）

| 日期 | 事项 | 状态 | 证据 |
|------|------|------|------|
| 2026-05-11 | 上线前 P0/P1/P2 问题闭环：点位编辑、100ms 采集、S7 校验、I/Q/M 与 DB 合并、聚合器、缓存 TTL、TDengine 测试桩、前端 WS/历史页问题均修复 | DONE | `docs/存档/审查报告/项目上线前审查报告_2026-05-11.md` |
| 2026-05-14 | 采集模块核心链路复审完成；GE 批量读取与 2-word 还原、dry-run、Snap7 补项等进入已实现状态 | DONE | `docs/存档/采集模块现存问题与整改方案-2026-05-14.md` |
| 2026-05-19 ~ 2026-05-21 | TDengine 旧直写/变量子表口径迁移为 Redis Stream -> writer -> `device_data` 宽表模型；writer 关键问题关闭 | DONE | `docs/存档/审查报告/0520审查报告.md`、`docs/存档/审查报告/0521审查报告.md` |
| 2026-05-24 | 上线前审查整改闭环，文档标记“所有 P0/P1/P2 问题已全部修复，可进入上线准备” | DONE | `docs/存档/审查报告/项目上线前审查报告_2026-05-11.md` |
| 2026-05-28 ~ 2026-05-31 | DataWatch Agent 完成骨架、Bootstrap、Auth、ProcessManager、viewer/admin/config/health/logs 等核心能力 | DONE | `docs/存档/审查报告/DataWatch Agent 代码审查记录.md` |
| 2026-05-31 | writer `system_log` 落库、Agent 实时日志 tail、app 实时/历史日志 Tab 完成并通过真实环境验收 | DONE | `docs/入库模块/入库模块日志方案.md` |
| 2026-06-02 | Data Watch Console app1.0 完成：模块控制、配置、健康状态、暗色模式、module-monitor/rates、图表视觉优化 | DONE | `docs/app监控/DataWatch-Console-设计方案.md`、`docs/app监控/采集入库页真实数据接入方案.md` |
| 2026-06-03 | app1.0 相关设计文档、技术方案、README 与暗色模式文档已同步 | DONE | `docs/README.md`、`docs/设计/技术方案.md`、`docs/superpowers/*` |
| 2026-06-03 | 登录页缺陷修复：手输账号无法登录（ElSelect allow-create 未写入 v-model）、登录失败静默无提示、登录成功滑块空引用崩溃；前端静态 404 catch-all 与后端动态 `Exception404` 重名冲突修复（路由 `No match` 告警消除、path 级 404 兜底恢复） | DONE | `docs/前端/登录与路由缺陷修复-2026-06-03.md` |

---

## 四、当前 P0 待办：先完成现场可验收闭环

| ID | 任务 | 状态 | 负责人建议 | 验收标准 | 参考 |
|----|------|------|------------|----------|------|
| P0-001 | 目标机全链路安装验证：MySQL / Redis / TDengine / Backend / Agent / Console / collector / writer | TODO | 运维 + 全栈 | 目标机从冷启动到登录 Console，能启动/停止 collector 与 writer，实时监控和历史查询可用 | `docs/运维/项目Docker镜像部署与迁移说明.md`、`docs/运维/本地开发启动说明.md` |
| P0-002 | 真实 PLC 轨道吊/桥吊点表现场核验 | VERIFY | collector | 典型 S7 DB/I/Q/M、GE `%R/%AI/%AQ/%I/%Q/%M` 点位在实时监控页持续变化，字节序/位序与现场已知值一致 | `docs/存档/采集模块现存问题与整改方案-2026-05-14.md` |
| P0-003 | collector 与 writer 生产打包复验 | TODO | collector / writer | 重新 PyInstaller 打包后，Agent 能以配置工作目录启动两个 exe，日志与 PID 状态正常 | `docs/运维/项目Docker镜像部署与迁移说明.md` |
| P0-004 | app1.0 安装包复验 | TODO | app | 新机器安装 Console 后默认窗口 1280x800，登录、配置、健康状态、图表、日志、模块控制均可用 | `docs/app监控/DataWatch-Console-设计方案.md` |
| P0-005 | 容器后端 CORS / Backend URL 生产配置固化 | TODO | backend / 运维 | Tauri dev 与安装版均可登录 Backend；容器环境不再出现 OPTIONS/POST 已通但 app 报“后端不可达” | `docs/运维/本地开发启动说明.md` |

---

## 五、当前 P1 待办：稳定性与可观测性增强

| ID | 任务 | 状态 | 负责人建议 | 验收标准 | 参考 |
|----|------|------|------------|----------|------|
| P1-001 | writer 历史日志视图分页或虚拟滚动 | TODO | app / backend | app 历史日志不再固定一次渲染 200 行；大日志量下不卡顿 | `docs/入库模块/入库模块日志方案.md` |
| P1-002 | writer `mysql_mapper.py` 失败事件插桩 | DEFER | writer | MySQL 映射失败进入 `system_log`，且有状态化/节流，不刷库 | `docs/入库模块/入库模块日志方案.md` |
| P1-003 | DEBUG 写入侧门控 | DEFER | backend / writer / collector | DEBUG 事件只在 `log_retention_policy.debug_db_enabled` 或有效期内落库 | `docs/设计/系统日志总体设计方案.md`、`docs/入库模块/入库模块日志方案.md` |
| P1-004 | module-monitor/rates 趋势持久化评估 | DEFER | backend | 后端重启后可保留趋势窗口，或明确仍接受内存滚动缓冲 | `docs/app监控/采集入库页真实数据接入方案.md` |
| P1-005 | 采集长稳压测与告警阈值 | TODO | collector / test | 真实或仿真设备下连续运行 24h，采集延迟、Redis 写入、writer lag、DLQ 均可观测 | `docs/存档/采集模块现存问题与整改方案-2026-05-14.md` |
| P1-006 | 数据质量规则接入主链路 | DEFER | collector / backend | 质量规则结果进入实时态或告警态，不作为 TDengine 历史宽表列 | `collector/core/data_quality.py`、`docs/采集模块/采集模块代码设计逻辑.md` |

---

## 六、当前 P2 待办：发布与安全增强

| ID | 任务 | 状态 | 负责人建议 | 验收标准 | 参考 |
|----|------|------|------------|----------|------|
| P2-001 | Agent 注册为 Windows Service / NSSM 安装脚本 | TODO | agent / 运维 | 目标机开机自动启动 Agent，服务账户权限、日志目录、配置目录均正确 | `docs/app监控/DataWatch-Console-设计方案.md` |
| P2-002 | Agent 远程访问安全策略 | DEFER | agent / 运维 | 若开启远程 Agent，必须有 TLS/VPN/IP allowlist 或等价隔离，不裸露管理端口 | `docs/app监控/DataWatch-Console-设计方案.md` |
| P2-003 | Agent `/internal/shutdown` 增强评估 | DEFER | agent / collector / writer | 若未来需要更优雅停机，再评估 collector/writer 内置本地 HTTP shutdown；当前 shutdown_flag 已可用 | `docs/app监控/DataWatch-Console-设计方案.md` |
| P2-004 | 文档版本治理 | TODO | doc | `docs/README.md`、设计文档、运维文档、API 文档与实际代码保持同步；旧口径进入存档而非留在当前事实源 | `docs/README.md` |

---

## 七、已过期口径（不要再作为当前 TODO）

| 旧口径 | 当前结论 | 替代事实源 |
|--------|----------|------------|
| TDengine `LAST()` 作为实时监控入口 | OBSOLETE。实时态改走 Redis latest/window，TDengine 只管历史宽表 | `docs/设计/Redis实时缓冲层与点位分组设计方案_v0_6.md`、`docs/设计/技术方案.md` |
| 每变量一个 TDengine 子表 | OBSOLETE。当前为 `device_data` 单库，`qc/asc/agv` 超级表，每设备一个子表，点位为宽列 | `docs/入库模块/入库模块代码设计逻辑.md` |
| collector 直接写 TDengine 历史 | OBSOLETE。collector 写 Redis，writer 从 Redis Stream 批量写 TDengine | `docs/采集模块/采集模块整体结构设计.md`、`docs/入库模块/入库模块代码设计逻辑.md` |
| 304000 子表 / 全量 100ms 写入作为当前目标 | OBSOLETE。该压测是历史上限探索，不作为当前生产验收目标 | `docs/存档/TDengine写入压测优化建议-2026-05-18.md` |
| Data Watch Console 分 Server Edition / Client Edition 作为 app1.0 现状 | OBSOLETE。app1.0 当前是单一 Console 应用；分版仅作为后续演进设想 | `docs/app监控/DataWatch-Console-设计方案.md` |
| app 图表和连接状态为占位 | OBSOLETE。module-monitor/rates、Recharts、10s health 轮询已接入 | `docs/app监控/采集入库页真实数据接入方案.md`、`docs/设计/Data Watch Console 页面设计说明.md` |

---

## 八、推荐执行顺序

1. 先做 `P0-001` 到 `P0-005`：确保目标机能完整安装、启动、登录、采集、入库、查看。
2. 再做 `P1-005`：用真实设备或稳定仿真源跑长稳，确认现场点位值、采集周期、Redis lag、writer lag、DLQ。
3. 之后做 `P1-001` / `P1-002` / `P1-003`：补日志可观测性细节。
4. 最后做 `P2-001` / `P2-002`：把 Agent 服务化和远程安全策略固化为生产安装方案。

---

## 九、维护规则

- 新增任务必须写清：范围、验收标准、参考文档。
- 完成任务时必须补证据：提交号、文档路径、测试命令或现场验收记录。
- 如果任务只是历史方案中的旧口径，应放到“已过期口径”，不要留在当前待办表。
- app / agent / backend / collector / writer 任一模块发生架构变化时，优先更新对应事实源文档，再更新本 TODO。

