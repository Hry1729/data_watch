# 工业数据采集与监控平台 - 文档中心

> 自动化码头设备数据采集与监控平台的全部文档汇总。

---

## 目录结构

```
docs/
├── 设计/              # 全局/跨模块设计文档（长期有效）
├── 采集模块/          # 采集模块设计与接入方案（当前有效）
├── 入库模块/          # 入库模块设计与落库机制（当前有效）
├── 后端/              # 后端开发文档
├── 前端/              # 前端开发文档
├── 数据库/            # 数据库设计与参考文档
├── 运维/              # 环境搭建与部署指南
├── 存档/              # 历史快照、审查报告、已完成任务（只读参考）
│   └── 审查报告/
├── Perspective/       # Perspective 官方参考文档（勿动）
├── tdengine_docs_en/  # TDengine 官方英文文档（勿动）
├── tdengine_examples/ # TDengine 官方示例代码（勿动）
├── 前后端联调/        # 联调规范（勿动）
├── 架构图.png
├── 架构图.pen
├── 项目TODO文档.md    # 项目任务跟踪（活跃维护）
└── README.md
```

---

## 设计/

全局与跨模块设计文档，是理解系统全貌的入口。

| 文档 | 说明 |
|------|------|
| `概要设计说明书v3.md` | **核心文档**。项目概述、系统总体架构、TDengine/MySQL/Redis 分层存储设计 |
| `技术方案.md` | 技术选型与实现方案，含 Redis 缓冲层与 TDengine 批量写入流程 |
| `标准后端架构.md` | 后端分层架构规范 |
| `Redis实时缓冲层与点位分组设计方案_v0_6.md` | Redis 快照/窗口/Stream 精细缓冲方案 |
| `系统日志总体设计方案.md` | 系统日志模块总体设计 |
| `Perspective集成设计.md` | Perspective 数据可视化组件集成方案 |

---

## 采集模块/

当前有效的采集模块设计与设备接入方案。

| 文档 | 说明 |
|------|------|
| `采集模块整体结构设计.md` | **核心架构**。目录布局与全链路生命周期设计 |
| `采集模块代码设计逻辑.md` | **核心逻辑**。驱动细节、去重机制、Redis Stream 写入、异步调度 |
| `AGV_OPC_DA_接入方案.md` | AGV 设备 OPC DA 接入方案 |
| `AGV_OPC_DA_远程采集方案.md` | AGV OPC DA 远程采集网络与部署方案 |
| `OPC_DA_中心采集实施计划.md` | OPC DA 中心化采集实施计划 |

---

## 入库模块/

当前有效的入库模块设计。

| 文档 | 说明 |
|------|------|
| `入库模块代码设计逻辑.md` | **核心逻辑**。Redis Stream 消费、TDengine 动态写入与扩展列、可靠性与死信队列机制 |

---

## 后端/

| 文档 | 说明 |
|------|------|
| [API接口文档.md](file:///D:/python/device_alarm/data_watch/docs/后端/API接口文档.md) | **核心契约**。v1.1 联调冻结基线，唯一接口事实源 |
| [Perspective会话隔离与保护策略.md](file:///D:/python/device_alarm/data_watch/docs/后端/Perspective会话隔离与保护策略.md) | Perspective WebSocket 会话隔离与资源保护策略 |
| [config-reference.md](file:///D:/python/device_alarm/data_watch/docs/后端/config-reference.md) | **配置参考**。涵盖所有环境变量默认值及多源配置优先级（env > .env > settings.toml） |
| [auth.md](file:///D:/python/device_alarm/data_watch/docs/后端/auth.md) | **认证与权限**。描述登录/登出流、所有 40 个权限码以及默认角色用户种子数据 |
| [api-reference.md](file:///D:/python/device_alarm/data_watch/docs/后端/api-reference.md) | **API 参考**。列出所有 80 个 HTTP 路由的详细请求/返回结构与权限要求 |
| [websocket-protocol.md](file:///D:/python/device_alarm/data_watch/docs/后端/websocket-protocol.md) | **WS 协议**。实时监控推送与 Perspective 历史分析通道协议规范 |
| [database-schema.md](file:///D:/python/device_alarm/data_watch/docs/后端/database-schema.md) | **数据库 Schema**。MySQL 配置库、TDengine 时序库及 Redis 实时缓冲物理模型 |
| [history-query-guide.md](file:///D:/python/device_alarm/data_watch/docs/后端/history-query-guide.md) | **历史数据查询**。规范基于 TDengine 和 Redis 的历史指标 4 种查询和降采样机制 |
| [architecture.md](file:///D:/python/device_alarm/data_watch/docs/后端/architecture.md) | **技术架构**。包含 EnhancedASGITurboAPI 设计、高并发数据流闭环与日志清理机制 |
| [walkthrough.md](file:///D:/python/device_alarm/data_watch/docs/后端/walkthrough.md) | **后端文档质检收口说明**。记录本轮文档开发时的并发质检流程、修复的十余处不一致缺陷以及测试结论 |


---

## 前端/

基于 Vue 3 + TypeScript + Element Plus（ArtDesignPro）技术栈。

| 文档 | 说明 |
|------|------|
| `开发必读文档.md` | **必读**。响应格式、菜单接口、权限模式等开发规范 |
| `ArtDesignPro项目结构.md` | 前端模板目录结构 |
| `路由和菜单.md` | 路由配置与菜单注册规则 |
| `权限说明.md` | 按钮级、路由级权限控制机制 |
| `环境变量配置.md` | `.env` 环境变量配置项说明 |
| `实时监控核心页面.md` | 实时监控页面专项设计文档 |
| `页面设计文档.md` | 各页面交互与视觉设计规范 |
| `系统日志前端设计文档.md` | 系统日志模块前端页面设计 |
| `useTable 组合式函数.md` | 表格组件组合式函数 API |
| `ArtSearchBar 搜索栏组件.md` | 搜索栏组件 API |
| `构建与部署.md` | 前端构建与部署说明 |
| `脚本说明.md` | 工具脚本使用说明 |
| `图标.md` | 图标使用规范 |
| `页面设计TODO.md` | 前端页面设计进度跟踪 |

---

## 数据库/

TDengine（时序数据）与 MySQL（配置数据）设计文档。

| 文档 | 说明 |
|------|------|
| `数据库结构v3.md` | **核心文档**。MySQL + Redis + TDengine 完整表结构设计 |
| `TDengine数据模型调整简述.md` | TDengine 宽表模型变更说明（旧点位子表 → 设备宽表） |
| `TDengine全量设备建库说明.md` | TDengine 建库/建超级表方案 |
| `TDengine TSDB Python Connector.md` | TDengine Python 连接器使用参考 |
| `数据库优化.md` | 数据库性能优化方案与索引调整 |
| `数据库开发TODO.md` | 数据库架构与 SQL 调优开发计划 |

---

## 运维/

环境搭建、启动与部署指南。

| 文档 | 说明 |
|------|------|
| `项目环境与启动说明.md` | **常用**。本地开发环境搭建与各服务手动启动步骤 |
| `项目Docker镜像部署与迁移说明.md` | Docker Compose 生产环境部署与迁移指南 |

---

## 存档/

历史快照与一次性文档，仅供回溯参考，不作为当前设计依据。

| 文档 | 说明 |
|------|------|
| `审查报告/项目上线前审查报告_2026-05-11.md` | 上线前全量功能审查报告 |
| `审查报告/collector代码审查报告.md` | Collector 第一轮代码审查 |
| `审查报告/collector代码审查报告_2026-05-07.md` | Collector 第二轮代码审查 |
| `审查报告/collector审查优化建议-2026-05-08.md` | Collector 优化建议清单 |
| `审查报告/sidecars审查报告.md` | Sidecar 模块代码审查 |
| `采集模块设计.md` | 采集模块早期设计文档（已被整体结构设计取代） |
| `采集模块现存问题与整改方案-2026-05-14.md` | 2026-05-14 问题整改快照 |
| `系统接入测试计划-2026-05-15.md` | 2026-05-15 测试计划快照 |
| `TDengine写入压测优化建议-2026-05-18.md` | 2026-05-18 压测记录 |
| `开发分工与任务清单（已开发完成）.md` | 早期开发分工规划（已完成） |
| `智能体分工方案.md` | AI 协作分工说明（历史参考） |

---

## 技术栈速览

| 层级 | 技术 |
|------|------|
| 数据采集 | OPC DA、Snap7 (S7)、GE SRTP、OPC UA，Python asyncio |
| 实时缓冲 | Redis（最新值快照 / 短窗口 / 待入库 Stream） |
| 时序存储 | TDengine 3.0+（设备宽表，writer 独立进程批量写入） |
| 配置存储 | MySQL 8.0+ |
| 后端服务 | TurboAPI + SQLAlchemy（Python 3.14 free-threading） |
| 前端展示 | Vue 3 + TypeScript + Element Plus（ArtDesignPro） |
| 数据可视化 | Perspective |
| 实时通信 | WebSocket |

---

## 新人阅读建议

1. **先读概要设计**：`设计/概要设计说明书v3.md` — 建立项目全貌
2. **再看数据库**：`数据库/数据库结构v3.md` — 理解数据模型
3. **按角色深入**：
   - 前端开发：`前端/开发必读文档.md` → `前端/ArtDesignPro项目结构.md`
   - 后端开发：`设计/标准后端架构.md` → `后端/API接口文档.md`
   - 采集开发：`采集模块/采集模块整体结构设计.md` → `采集模块/采集模块代码设计逻辑.md`
   - 入库开发：`入库模块/入库模块代码设计逻辑.md`
   - 运维部署：`运维/项目环境与启动说明.md` → `运维/项目Docker镜像部署与迁移说明.md`
