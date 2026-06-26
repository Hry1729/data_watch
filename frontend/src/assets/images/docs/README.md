# 项目文档封面图

把封面图放到本目录，**按文件名自动绑定**到对应卡片，无需改任何代码。
支持 `.webp` / `.png` / `.jpg` / `.jpeg` / `.svg`。建议 **~800×400（2:1）、WebP、<120KB**。

绑定优先级：`<slug>.*` > `<分类名>.*` > 渐变横幅占位。
- 想精配某一篇：用该篇 **slug** 命名，如 `db-mysql.webp`
- 想一张图覆盖某分类全部篇：用 **分类名** 命名，如 `database.webp`

## 单篇 slug（精配用）

| slug | 文档 | 分类名（兜底用） |
|---|---|---|
| `arch-overview` | 系统架构总览 | `overall-design` |
| `adr` | 架构决策记录 | `overall-design` |
| `mod-agent-readme` | 边缘代理 设计文档 | `mod-agent` |
| `mod-agent-detail` | 边缘进程编排与凭据 | `mod-agent` |
| `mod-app-readme` | 桌面端 设计文档 | `mod-app` |
| `mod-app-detail` | 控制台页面与双数据源 | `mod-app` |
| `mod-backend-readme` | 中心后端 设计文档 | `mod-backend` |
| `mod-backend-detail` | 服务分层与实时历史通道 | `mod-backend` |
| `mod-collector-readme` | 采集端 设计文档 | `mod-collector` |
| `mod-collector-detail` | 调用链与协议设计 | `mod-collector` |
| `mod-frontend-readme` | Web 前端 设计文档 | `mod-frontend` |
| `mod-frontend-detail` | 页面权限与实时历史设计 | `mod-frontend` |
| `mod-writer-readme` | 入库端 设计文档 | `mod-writer` |
| `mod-writer-detail` | 可靠入库与 TDengine 写入 | `mod-writer` |
| `contract-api` | API 契约 | `contract` |
| `contract-ws` | WebSocket 协议 | `contract` |
| `db-mysql` | MySQL 结构与字段 | `database` |
| `db-redis` | Redis 流与点位分组 | `database` |
| `db-tdengine` | TDengine 结构与字段 | `database` |
| `ops-readme` | 运维部署概述 | `ops` |
| `ops-troubleshooting` | 问题排查 | `ops` |

## 分类名（批量兜底用）

`overall-design` · `mod-agent` · `mod-app` · `mod-backend` · `mod-collector` · `mod-frontend` · `mod-writer` · `contract` · `database` · `ops`
