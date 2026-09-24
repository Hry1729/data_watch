# /clone-website 输出计划 — https://tikhub.io/zh

**编写日期:** 2026-08-30
**会话语言:** 中文(用户偏好)
**项目路径(应用根):** `D:\python\device_alarm\data_watch\docs\着陆页参考\tikhub-clone`

---

## 1. 标识符

| 字段 | 值 |
|---|---|
| 规范化 origin | `https://tikhub.io` |
| 规范化 pathname | `/zh` |
| origin SHA-256 前 8 字符 | `5f62aaaa` |
| pathname SHA-256 前 8 字符 | `d83abd2e` |
| **`<site-key>`** | `tikhub-io-5f62aaaa` |
| **`<page-key>`** | `zh-d83abd2e` |

---

## 2. 目标 URL → 目标路由

只有一个 URL(单页面克隆),**遵循"first fresh-template"默认**:保留 locale 路径,所以目标 Next.js 路由为 `/zh`。

| 源 URL | 目标 Next.js 路由 | 目标文件 |
|---|---|---|
| `https://tikhub.io/zh` | `/zh` | `src/app/zh/page.tsx`(新建) |

**保留不动的现有路由:** `src/app/page.tsx`(默认脚手架占位符,不删)。这避免了替换模板占位符,并保留未来添加英文 `/` 路由的空间。

---

## 3. 命名空间根目录(全部新建)

| 用途 | 路径 |
|---|---|
| 产物根(规范、清单) | `docs/research/tikhub-io-5f62aaaa/zh-d83abd2e/` |
| 截图根 | `docs/design-references/tikhub-io-5f62aaaa/zh-d83abd2e/` |
| 组件根(本页面专用) | `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/` |
| 共享同站组件 | `src/components/sites/tikhub-io-5f62aaaa/shared/` |
| 资源根(本页面专用) | `public/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/` 等 |
| 共享同站资源 | `public/sites/tikhub-io-5f62aaaa/shared/` |
| 资源下载脚本(唯一命名) | `scripts/download-assets-tikhub-io-5f62aaaa-zh-d83abd2e.mjs` |
| 共享同站图标模块 | `src/components/sites/tikhub-io-5f62aaaa/shared/icons.tsx` |

**碰撞检查:** 整个项目里上述每个路径都为空(无现存同目录),无碰撞。

---

## 4. 页面拓扑(从快照提取,11 个区域)

| # | 区域 | 关键内容 | 交互模型 | 复杂度 |
|---|---|---|---|---|
| 1 | Header / 导航 | Logo、产品/解决方案/开发者下拉、定价/企业版链接、搜索按钮、暗黑模式切换、语言选择、登录、预约通话 | 静态 + 滚动 | 中 |
| 2 | Hero | "社交数据基础设施 为 AI 时代而生" 标题、副标题、4 个 CTA、统计徽章 (16 平台 / 1,000+ 接口 / 10亿+ 记录 / $0.001) | 静态 | 低 |
| 3 | 支持的平台轮播 | 14 个平台卡片(TikTok/Douyin/Instagram/YouTube/X/Rednote/Bilibili/Weibo/Kuaishou/WeChat/Lemon8/Zhihu/Reddit/LinkedIn),在快照中出现 4 轮 → **是连续横向滚动 marquee** | **滚动/时间驱动** | 中 |
| 4 | 信任统计 | "30,000+ 开发者" / "3亿+ 每日请求" / "16 平台" / "10亿+ 记录" / GDPR · CCPA | 静态 | 低 |
| 5 | API 预览(代码示例) | "查看 API 运行效果" + 语言选择 tabs (Python/JavaScript/cURL/Java/C/C++) + 3 个代码块 (TikTok/Instagram/X)+ 文件树 (tiktok_example, instagram_example, x_twitter_example) | **点击驱动 tab** | 高 |
| 6 | MCP 集成与 Chrome 扩展 | 4 个标签(TikTok 204 / Instagram 82 / Twitter 13 / 全部 16 平台)+ `claude_desktop_config.json` 代码块 + 嵌入 YouTube 视频 + 990+ 工具统计 + 4 个能力子标题 + Chrome 扩展说明 | **点击驱动 tab** | 高 |
| 7 | 数据集 | "10亿+ 记录" 标题 + 3 张数据集示例图 + 定价表 (100K-500K $150 / 20M+ $8,000) | 静态 | 中 |
| 8 | 市场与合作伙伴 | "2026 年 8 月上线" + 3 列 (AI 智能体 / API / 网络数据采集) + TikHub 10% vs 行业 20% 对比 | 静态 | 中 |
| 9 | 你能构建什么 | 6 个 tab (AI 智能体 / RAG 与检索 / 训练数据 / SaaS 产品 / 内容智能 / 自动化) + tab 内容 (h3 + 描述 + 示例) | **点击驱动 tab** | 高 |
| 10 | CTA 收尾 | "社媒数据不会自己变成结构化" + $0.05 免费额度 + 2 个 CTA | 静态 | 低 |
| 11 | Footer | Logo + 描述 + 社交链接 + 5 列导航 (产品/平台 API/开发者/公司/法律) + 免责声明 + 版权 + Scroll-to-top 按钮 | 静态 | 中 |

**附加的全局 UI:**
- 嵌入式 YouTube 视频(`youtube-nocookie.com/embed/IsstyDED2nc`)
- 暗黑模式切换
- Scroll-to-top 浮动按钮
- 中英文双语切换

---

## 5. 共享基础文件变更(Phase 2)

按照技能要求,以下文件将被**修改**(均不删除已有路由 / token):

| 文件 | 修改内容 | 原因 |
|---|---|---|
| `src/app/layout.tsx` | 添加中文字体支持(可能用 Noto Sans SC 或继承 Geist 的中文 fallback),更新 `lang="zh"`、metadata 标题/描述 | 中文站点 lang 标签和元数据必须为中文 |
| `src/app/globals.css` | 追加 TikHub 颜色 token、字体 token、共享 keyframes;保留 shadcn 默认 token | 全局颜色 / 字体基础 |
| `src/app/page.tsx` | **不动** | 保留脚手架占位符(用户后续可选择性删除) |
| `src/app/zh/page.tsx` | **新建** | 装载本克隆的所有区块组件 |
| `src/app/zh/layout.tsx` | **新建(可选)** | 如果 `lang="zh"` 不便直接放到根 layout,可路由级覆盖 |

**无任何现有路由被删除或替换。**

---

## 6. 实施策略

### 阶段 2(基础,顺序执行,由我完成,不分发)
1. 字体 + design token 合并到 `globals.css`
2. 写共享类型 `src/components/sites/tikhub-io-5f62aaaa/shared/types.ts`
3. 下载共享 SVG 图标到 `shared/icons.tsx`
4. 下载所有图片到 `public/sites/.../zh-d83abd2e/images/`
5. 跑 `npm run build` 验证基础无破坏

### 阶段 3(每个区块,提取 → 写规范 → 分发)
- **简单区块**(Hero / 统计 / CTA / Footer):1 个 builder,1 个规范文件
- **复杂区块**(API 预览 / MCP / 你能构建什么):3+ 个子组件,各 1 个规范文件 + 1 个区块 wrapper
- **平台轮播**:1 个 builder + 1 个规范(因核心是 marquee,数据可硬编码)
- 每个 builder 在 worktree 分支工作
- 每完成 1 个区块 merge 一次,跑 `npm run build` 验证

### 阶段 4(装配)
- `src/app/zh/page.tsx` 导入所有区块,按拓扑顺序装配

### 阶段 5(视觉 QA)
- 1440px / 390px 视口下截图对比
- 测试 marquee、tab 切换、暗黑模式

---

## 7. 范围外(明确不做)

- 不克隆子路由(`/pricing`、`/enterprise`、`/datasets` 等) — 它们是单独的页面
- 不克隆英文 `/` 路由
- 不实现真实后端 / 真实 API 调用
- 不实现真实搜索 / 真实暗黑模式持久化
- 不做 SEO 优化
- 不做可访问性审计(除默认 shadcn 外)

---

## 8. 风险与开放问题

1. **YouTube 嵌入** — 原始站点用 `youtube-nocookie.com`,克隆时是放外链 iframe 还是占位符?(建议占位 + 链接)
2. **平台轮播** — 快照里出现 4 轮卡片,说明是无限滚动 marquee 还是 3-4 屏轮播?需要交互扫描确认
3. **多 tab 区块的初始激活态** — 默认显示哪个 tab?需要逐个点击验证
4. **中文字体** — 站点用了什么字体?是 Noto Sans SC、Source Han 还是 Geist 的中文 fallback?需要提取
5. **暗黑模式** — 是否需要在本次克隆里实现,还是只克隆浅色?需要确认
6. **图片资源** — 数据集 / Logo / 嵌入的 `tikhub.io/logo.jpeg` 等是否需要真实下载到本项目?按规范"Real Content, Real Assets"是必须的

---

## 9. 待您确认的事项

| # | 问题 | 默认值 |
|---|---|---|
| A | 目标路由用 `/zh` 还是替换 `/`? | `/zh`(保留脚手架) |
| B | 暗黑模式本次要克隆吗? | 是(视觉 QA 必需) |
| C | YouTube 视频用占位 + 跳转,还是 `<iframe>` 嵌入? | 占位 + 跳转(避免第三方跟踪) |
| D | 资源(图片、字体)都下载到本地 `public/sites/...`? | 是(规范强制) |
| E | 中文字体(用 `next/font/google` 加载 Noto Sans SC) | 是 |
| F | 工作流:基础由我顺序做,区块由我分发到 worktree 的 builder agent? | 是 |
| G | 工作流跑完后我提交 1 个 commit 还是每个区块 1 个 commit? | 每区块 1 个 commit(便于回滚) |

---

**这份计划批准后,我会:**
1. 创建上面列出的 6 个新目录
2. 写 `download-assets-tikhub-io-5f62aaaa-zh-d83abd2e.mjs` 资源下载脚本
3. 跑 `npm run build` 确认当前状态
4. 进入 Phase 1 完整侦察(截图 + 交互扫描 + 资源清单)
5. 写 `BEHAVIORS.md` 和 `PAGE_TOPOLOGY.md`
6. 把所有发现给您过目,然后进入 Phase 2 基础构建
