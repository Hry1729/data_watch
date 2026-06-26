/**
 * 项目文档清单与原文加载器
 *
 * 这是「项目文档」功能的唯一数据源：
 * - 决定左侧多级菜单结构、总览卡片、阅读路由；
 * - Markdown 原文通过 Vite 的 import.meta.glob 在构建期从仓库根 `docs/` 直接读入；
 * - 排除 `_archive/` 旧文档。
 *
 * 维护方式：新增/调整文档只改本文件的 docSections；
 * 后续要给某篇配封面图，把图片放到 `src/assets/images/docs/`，
 * 在对应 doc 上补 `cover` 字段即可（见下方 DocMeta.cover 注释）。
 */

/** 单篇文档元数据 */
export interface DocMeta {
  /** 路由用的 ASCII 短链，全局唯一 */
  slug: string
  /** 文档标题（卡片标题 / 阅读页标题） */
  title: string
  /** 一句话简介（卡片副标题） */
  intro: string
  /** 相对 docs/ 的文件路径，用于取原文，如 '01-总体设计/系统架构总览.md' */
  file: string
  /**
   * 封面图（可选）。占位阶段留空 → 自动用分类色渐变占位图。
   * 后续放真实图片：import cover from '@imgs/docs/xxx.png'，然后 cover 字段填该变量。
   */
  cover?: string
}

/** 叶子分类（菜单可点项 / 一组卡片） */
export interface DocCategory {
  type: 'category'
  /** 路由末段 + 查找键，全局唯一且为 ASCII */
  key: string
  title: string
  /** 菜单与卡片占位图用的图标（remix icon） */
  icon: string
  /** 占位封面与卡片强调色 */
  color: string
  docs: DocMeta[]
}

/** 分组（菜单目录，下面再挂若干叶子分类） */
export interface DocGroup {
  type: 'group'
  key: string
  title: string
  icon: string
  children: DocCategory[]
}

export type DocSection = DocCategory | DocGroup

/**
 * 文档树（顺序即菜单/总览展示顺序）
 *
 * 说明：docs/README.md 与 docs/03-数据与契约/README.md 是纯目录索引，
 * 其职责已由本「项目文档」总览页与多级菜单承担，故不单独做成卡片。
 */
export const docSections: DocSection[] = [
  {
    type: 'category',
    key: 'overall-design',
    title: '总体设计',
    icon: 'ri:focus-3-line',
    color: '#5D87FF',
    docs: [
      {
        slug: 'arch-overview',
        title: '系统架构总览',
        intro: '全局架构、数据流、模块协作、部署拓扑与核心设计约束。',
        file: '01-总体设计/系统架构总览.md'
      },
      {
        slug: 'adr',
        title: '架构决策记录（ADR）',
        intro: 'Redis 解耦、四层结构、free-threaded、多表批写等核心决策。',
        file: '01-总体设计/架构决策记录.md'
      }
    ]
  },
  {
    type: 'group',
    key: 'modules',
    title: '模块设计',
    icon: 'ri:stack-line',
    children: [
      {
        type: 'category',
        key: 'mod-agent',
        title: 'Agent 边缘代理',
        icon: 'ri:robot-2-line',
        color: '#13DEB9',
        docs: [
          {
            slug: 'mod-agent-readme',
            title: '边缘代理 agent 设计文档',
            intro: 'FastAPI 本机服务：进程管控、凭据保管、首装引导、sidecar 管理。',
            file: '02-模块设计/agent/README.md'
          },
          {
            slug: 'mod-agent-detail',
            title: '边缘进程编排与凭据',
            intro: '边缘定位、双根路径、DPAPI 凭据、优雅停机、OPC DA proxy 编排。',
            file: '02-模块设计/agent/边缘进程编排与凭据.md'
          }
        ]
      },
      {
        type: 'category',
        key: 'mod-app',
        title: 'App 桌面端',
        icon: 'ri:computer-line',
        color: '#539BFF',
        docs: [
          {
            slug: 'mod-app-readme',
            title: '桌面端 app 设计文档',
            intro: 'Tauri + React 运维监控台：双数据源、首装零门槛、进程可管控。',
            file: '02-模块设计/app/README.md'
          },
          {
            slug: 'mod-app-detail',
            title: '控制台页面与双数据源',
            intro: '页面结构、启动流程、agent/backend 双源、双 token、Rust 转发、降级。',
            file: '02-模块设计/app/控制台页面与双数据源.md'
          }
        ]
      },
      {
        type: 'category',
        key: 'mod-backend',
        title: 'Backend 中心后端',
        icon: 'ri:server-line',
        color: '#FA896B',
        docs: [
          {
            slug: 'mod-backend-readme',
            title: '中心后端 backend 设计文档',
            intro: 'TurboAPI 服务：三源统一、实时历史双通道、鉴权权限、可观测。',
            file: '02-模块设计/backend/README.md'
          },
          {
            slug: 'mod-backend-detail',
            title: '服务分层与实时历史通道',
            intro: '启动装配、ASGI、路由分层、RBAC、WS 实时、历史查询与 Perspective。',
            file: '02-模块设计/backend/服务分层与实时历史通道.md'
          }
        ]
      },
      {
        type: 'category',
        key: 'mod-collector',
        title: 'Collector 采集端',
        icon: 'ri:wifi-line',
        color: '#FFAE1F',
        docs: [
          {
            slug: 'mod-collector-readme',
            title: '采集端 collector 设计文档',
            intro: '单进程多协程：周期采集、协议驱动、Redis 写出、自治重连。',
            file: '02-模块设计/collector/README.md'
          },
          {
            slug: 'mod-collector-detail',
            title: '调用链与协议设计',
            intro: '启动链路、配置加载、并发模型、周期调度、协议读取、结果写出。',
            file: '02-模块设计/collector/调用链与协议设计.md'
          }
        ]
      },
      {
        type: 'category',
        key: 'mod-frontend',
        title: 'Frontend Web 前端',
        icon: 'ri:window-line',
        color: '#49BEFF',
        docs: [
          {
            slug: 'mod-frontend-readme',
            title: 'Web 前端 frontend 设计文档',
            intro: 'Art Design Pro + Vue3 业务控制台：配置、实时、历史、系统管理。',
            file: '02-模块设计/frontend/README.md'
          },
          {
            slug: 'mod-frontend-detail',
            title: '页面权限与实时历史设计',
            intro: '页面到 API 映射、RBAC 动态路由、WS 实时、Perspective 历史、错误态。',
            file: '02-模块设计/frontend/页面权限与实时历史设计.md'
          }
        ]
      },
      {
        type: 'category',
        key: 'mod-writer',
        title: 'Writer 入库端',
        icon: 'ri:database-line',
        color: '#13DEB9',
        docs: [
          {
            slug: 'mod-writer-readme',
            title: '入库端 writer 设计文档',
            intro: 'Stream 消费、TDengine 批写、至少一次、坏数据隔离 DLQ。',
            file: '02-模块设计/writer/README.md'
          },
          {
            slug: 'mod-writer-detail',
            title: '可靠入库与 TDengine 写入',
            intro: '消息生命周期、PEL 恢复、ACK 语义、DLQ、多表批写、schema 演进。',
            file: '02-模块设计/writer/可靠入库与TDengine写入.md'
          }
        ]
      }
    ]
  },
  {
    type: 'group',
    key: 'data-contract',
    title: '数据与契约',
    icon: 'ri:file-list-3-line',
    children: [
      {
        type: 'category',
        key: 'contract',
        title: '契约',
        icon: 'ri:exchange-line',
        color: '#5D87FF',
        docs: [
          {
            slug: 'contract-api',
            title: 'API 契约',
            intro: 'Backend / Agent REST：通用包络、鉴权、分页、错误语义、门禁。',
            file: '03-数据与契约/契约/API契约.md'
          },
          {
            slug: 'contract-ws',
            title: 'WebSocket 协议',
            intro: '实时 ws_push 与历史 Perspective：连接、鉴权、消息格式、状态机。',
            file: '03-数据与契约/契约/WebSocket协议.md'
          }
        ]
      },
      {
        type: 'category',
        key: 'database',
        title: '数据库',
        icon: 'ri:database-2-line',
        color: '#FA896B',
        docs: [
          {
            slug: 'db-mysql',
            title: 'MySQL 结构与字段',
            intro: 'device_watch 库：配置、告警、RBAC、日志、跨库映射、字段语义。',
            file: '03-数据与契约/数据库/MySQL结构与字段.md'
          },
          {
            slug: 'db-redis',
            title: 'Redis 流与点位分组',
            intro: '实时缓冲与入库队列：latest/window/stream/dlq、分片、消费组。',
            file: '03-数据与契约/数据库/Redis结构与字段.md'
          },
          {
            slug: 'db-tdengine',
            title: 'TDengine 结构与字段',
            intro: 'device_data 库：超级表、子表、列规则、schema 演进、写查语义。',
            file: '03-数据与契约/数据库/TDengine结构与字段.md'
          }
        ]
      }
    ]
  },
  {
    type: 'category',
    key: 'ops',
    title: '运维部署',
    icon: 'ri:tools-line',
    color: '#539BFF',
    docs: [
      {
        slug: 'ops-readme',
        title: '运维部署概述',
        intro: '部署形态、运行环境、开发启动、镜像部署、安装包打包、离线镜像、升级。',
        file: '04-运维部署/README.md'
      },
      {
        slug: 'ops-troubleshooting',
        title: '问题排查',
        intro: 'Web/API/WS/agent/collector/writer/OPC DA 常见故障定位路径。',
        file: '04-运维部署/问题排查.md'
      }
    ]
  }
]

/* ------------------------------------------------------------------ *
 * 原文加载：构建期把 docs 目录下所有 markdown 以裸字符串内联进来
 * ------------------------------------------------------------------ */

// 路径相对本文件（frontend/src/views/project-docs/） → 仓库根 docs/
// 负向模式在 glob 层排除 _archive，避免旧文档被读入打包
const rawModules = import.meta.glob(
  ['../../../../docs/**/*.md', '!../../../../docs/**/_archive/**'],
  {
    query: '?raw',
    import: 'default',
    eager: true
  }
) as Record<string, string>

/** 相对 docs/ 的文件路径 → 原文 */
const contentByPath: Record<string, string> = {}
for (const [absKey, content] of Object.entries(rawModules)) {
  const marker = '/docs/'
  const idx = absKey.indexOf(marker)
  if (idx === -1) continue
  const rel = absKey.slice(idx + marker.length)
  // 双保险：再次排除归档目录
  if (rel.includes('_archive/')) continue
  contentByPath[rel] = content
}

/** 取某篇文档原文（找不到返回空串） */
export function getDocContent(file: string): string {
  return contentByPath[file] ?? ''
}

/* ------------------------------------------------------------------ *
 * 封面自动绑定：约定式 drop-in
 * 把图片放到 src/assets/images/docs/，按文件名（去扩展名）匹配：
 *   <slug>.webp        优先用于该篇，如 db-mysql.webp
 *   <categoryKey>.webp 作为该分类下各篇的兜底，如 database.webp
 * 都没有则回退 SVG 横幅占位。支持 png/jpg/jpeg/webp/svg。
 * ------------------------------------------------------------------ */
const coverModules = import.meta.glob('../../assets/images/docs/*.{png,jpg,jpeg,webp,svg}', {
  eager: true,
  query: '?url',
  import: 'default'
}) as Record<string, string>

const coverByName: Record<string, string> = {}
for (const [path, url] of Object.entries(coverModules)) {
  const name = (path.split('/').pop() || '').replace(/\.[^.]+$/, '')
  if (name) coverByName[name] = url
}

/** 取封面图 URL：slug 优先 → 分类名兜底 → 无（用占位） */
export function getDocCover(slug: string, categoryKey?: string): string | undefined {
  return coverByName[slug] ?? (categoryKey ? coverByName[categoryKey] : undefined)
}

/* ------------------------------------------------------------------ *
 * 查询辅助
 * ------------------------------------------------------------------ */

/** 拍平出全部叶子分类（含其所属分组标题，便于面包屑/总览分组） */
export interface FlatCategory extends DocCategory {
  groupTitle?: string
}

export function getFlatCategories(): FlatCategory[] {
  const list: FlatCategory[] = []
  for (const section of docSections) {
    if (section.type === 'category') {
      list.push(section)
    } else {
      for (const child of section.children) {
        list.push({ ...child, groupTitle: section.title })
      }
    }
  }
  return list
}

/** 按分类 key 查叶子分类 */
export function getCategoryByKey(key: string): FlatCategory | undefined {
  return getFlatCategories().find((c) => c.key === key)
}

/** 全部文档（带所属分类信息） */
export interface DocWithCategory extends DocMeta {
  categoryKey: string
  categoryTitle: string
  categoryIcon: string
  categoryColor: string
}

export function getAllDocs(): DocWithCategory[] {
  const list: DocWithCategory[] = []
  for (const cat of getFlatCategories()) {
    for (const doc of cat.docs) {
      list.push({
        ...doc,
        cover: doc.cover ?? getDocCover(doc.slug, cat.key),
        categoryKey: cat.key,
        categoryTitle: cat.title,
        categoryIcon: cat.icon,
        categoryColor: cat.color
      })
    }
  }
  return list
}

/** 按 slug 查文档（含分类信息） */
export function getDocBySlug(slug: string): DocWithCategory | undefined {
  return getAllDocs().find((d) => d.slug === slug)
}

/** 相对 docs/ 的文件路径 → slug（用于文档内 .md 互链跳转） */
export function getSlugByFile(file: string): string | undefined {
  return getAllDocs().find((d) => d.file === file)?.slug
}
