# 行为圣经 — https://tikhub.io/zh

**编写日期:** 2026-08-30
**目标:** Phase 1 交互扫描的所有发现,所有 builder 必须读这份文档

---

## 1. 全局滚动行为

| 行为 | 描述 | 触发 | 实现 |
|---|---|---|---|
| **页面级原生滚动** | 整页原生滚动,无 Lenis / Locomotive / scroll-snap | 默认 | `window.scrollTo()` 即可 |
| **锚点跳转** | 所有 `<section class="scroll-mt-20">` 是导航锚点 | 点击 nav 链接 | 浏览器原生 + `scroll-mt-20` 顶部偏移 80px |
| **无 scroll-driven 切换** | 整页所有交互都是**点击/悬停驱动**,**不是 scroll-driven** | — | 重要:不要给任何区块加 IntersectionObserver 切换 |

---

## 2. 顶部导航 / Header

### 2.1 静态行为

- 始终在页面顶部(不 sticky)
- 背景透明,继承 body 背景
- 不在滚动时变化(无缩小/加阴影)

### 2.2 子菜单(产品 / 解决方案 / 开发者)

- `<button>` 触发(非 `<a>`)
- 悬停或点击展开下拉菜单
- **builder 实现占位下拉**(产品/解决方案/开发者 各 4-6 个链接),不需要精确内容

### 2.3 搜索按钮

- 文本 "搜索" + 右侧 `⌘K` 键提示徽章
- 点击**实现为占位**,无效果
- 移动端隐藏 `⌘K` 提示

### 2.4 暗黑模式切换

- 按钮,无 aria-label 指示当前状态
- 浅色模式时显示月亮图标;深色模式时显示太阳图标
- 点击切换 `html.classList` 上的 `.dark`
- **必须实现并测试两种模式**

### 2.5 语言选择

- 按钮,文本 "CN 中文" + chevron
- 点击展开语言列表(中/英等)
- **实现为占位下拉**,只显示 CN 中文

### 2.6 登录 + 预约通话

- "登录" → `<a>` 跳 `https://user.tikhub.io/`
- "预约通话" → `<a>` 跳 Google Calendar `https://calendar.app.google/AQLMWVdfAksrWeHh9`,`target="_blank"`

---

## 3. Hero 区块

### 3.1 旋转徽章(关键动画)

- 在 H1 上方,圆角胶囊 `rounded-full`
- 文本每 ~2-3 秒轮换一次
- 观察到的文案:
  - "监控 YouTube 频道分析"
  - "分析 Instagram 互动趋势"
  - "大规模提取 TikTok 用户数据"
  - "通过 API"
- 实现: `useState + setInterval`,切换时用 `key={text}` + Tailwind 动画
- **必须保留旋转行为**

### 3.2 标题渐变特效

- H1 = "社交数据基础设施 为 AI 时代而生"
- 其中 "**为**" 和 "**AI 时代而生**" 有蓝紫渐变(`bg-clip-text text-transparent`)
- "为" 周围有虚线方框(corner brackets,4 个角)
- 渐变色: `#6366F1` → `#8B5CF6`

### 3.3 CTA 按钮(4 个)

- 顺序: 快速开始 / 数据 API / 数据集 / 入驻市场
- 第 1 个(快速开始)是**黑底白字 primary**(`bg-inverse text-surface`)
- 第 2-4 个是**白底黑字 outline**(`bg-surface border`)
- 第 4 个(入驻市场)的"新"标签是**靛紫底**(`bg-accent text-white`)
- 每个按钮带 → 箭头

### 3.4 统计徽章(4 个)

- 单行,中间用 `·` 分隔
- 文本: "16 个平台" / "1,000+ 接口" / "10亿+ 数据集记录" / "低至 $0.001/次请求"
- 纯文本,无图标

---

## 4. 平台 marquee(关键交互)

### 4.1 双行反向滚动

- **行 1 (h=120):** `animate-scroll-left`,28 张卡片(14 平台 × 2 复制)
- **行 2 (h=120):** `animate-scroll-right`,28 张卡片
- 行 1 平台: TikTok / Douyin / Instagram / YouTube / X / Twitter / Rednote / Bilibili
- 行 2 平台: Weibo / Kuaishou / WeChat / Lemon8 / Zhihu / Reddit / LinkedIn(以及行 1 的重复以保持滚动力)

### 4.2 实现细节

- 容器: `relative overflow-hidden w-[100dvw] max-w-[100vw] -ml-[50vw] left-1/2 space-y-3`
- 滚动 div: `flex animate-scroll-{left|right} hover:[animation-play-state:paused]`
- CSS `@keyframes`:
  - `from { transform: translateX(0) }` 到 `to { transform: translateX(-50%) }`
  - 速度: 每行约 30-40 秒一循环
- 悬停整个 marquee 时 `animation-play-state: paused`(重要 UX)

### 4.3 左右淡出渐变

- 左右两侧各有 `pointer-events-none absolute inset-y-0 w-32 z-10`
- 渐变: `linear-gradient(to right, var(--background) 0%, transparent 100%)`

### 4.4 平台卡片结构

```tsx
<a className="flex-shrink-0 w-[220px] md:w-[250px] lg:w-[280px] px-2 md:px-3 transition-transform hover:-translate-y-1">
  <div className="bg-surface/50 backdrop-blur-sm rounded-xl border border-accent/15 shadow-[0_0_12px_rgba(99,102,241,0.08)] p-5 h-full transition-all hover:shadow-[0_0_20px_rgba(99,102,241,0.18)] hover:bg-surface/70 hover:border-accent/30">
    <div className="flex items-start gap-3">
      <div className="w-10 h-10 rounded-lg bg-card border border-border/40 flex items-center justify-center shrink-0 text-foreground transition-transform group-hover:scale-110">
        <PlatformIcon />
      </div>
      <div className="min-w-0 flex-1">
        <h3 className="text-sm font-semibold truncate">TikTok API</h3>
        <p className="text-[11px] text-muted/80 mb-1">视频、用户、热门</p>
        <p className="text-xs text-muted-foreground line-clamp-2">通过我们开发者友好的 API 轻松访问...</p>
      </div>
    </div>
  </div>
</a>
```

### 4.5 14 个唯一平台

1. TikTok
2. Douyin
3. Instagram
4. YouTube
5. X / Twitter
6. Rednote (Xiaohongshu)
7. Bilibili
8. Weibo
9. Kuaishou
10. WeChat
11. Lemon8
12. Zhihu
13. Reddit
14. LinkedIn

---

## 5. 平台统计条

- 顶部 "深受全球数据团队与 AI 开发者信赖"
- 4 个大数字,水平排列 + 垂直分隔线
  - 30,000+ / 开发者与数据团队
  - 3亿+ / 每日请求处理量
  - 16 / 覆盖社媒平台
  - 10亿+ / 数据集记录交付量
- 最右侧:GDPR · CCPA 徽章(2 个圆形盾牌图标)
- 无交互

---

## 6. API 预览(代码示例,点击驱动)

### 6.1 布局

- 左列:标题 + 描述 + 语言选择 tabs + 3 个 bullet + 3 个链接(纵向 50%)
- 右列:card-swap 堆叠代码窗口(纵向 50%,占右侧全宽)

### 6.2 语言选择 tabs(点击)

- 6 个按钮: Python / JavaScript / cURL / Java / C / C++
- 当前激活(默认 Python)有黑底白字样式
- 切换语言**只更新右列代码块内容,不动卡片层叠**

### 6.3 card-swap 模式(关键)

- 3 张代码窗口卡片堆叠,只在右列显示
- 卡片背景: `!bg-[#1E1E2E]`
- 每张有 macOS 风格圆点(红黄绿)+ 文件名标签
- 顶部的卡片全显示,下面 2 张只露 35-40px 边缘
- 自动轮播:每 ~3-5 秒顶部卡片向后滑出,下一张顶上
- 3 个平台代码窗口:
  - `tiktok_example` / TikTok
  - `instagram_example` / Instagram
  - `x_twitter_example` / X / Twitter
- **builder 需要为这 3 个实现内容并实现 card-swap 动画**

### 6.4 代码高亮

- 9 种颜色:红 / 紫 / 灰 / 绿 / 黄 / 蓝 / 品红 / 暗灰
- dark theme syntax highlighting

---

## 7. MCP 集成(点击驱动 tab)

### 7.1 4 个 tab

- TikTok 204 / Instagram 82 / Twitter 13 / "全部 16 个平台 ↗" (最后一个是链接)
- 当前 TikTok 激活(深色背景)
- 数字是各平台的 MCP 工具数

### 7.2 tab 切换内容

- **左列**: `claude_desktop_config.json` 代码块(浅色背景),文件路径标头,深色代码,行号
- **右列**: 平台名 / 描述 / 3 个小统计
- 切换 tab 时,左列的 `mcpServers` key 名变化(从 `tikhub-tiktok` → `tikhub-instagram` → `tikhub-twitter`),URL 变化

### 7.3 第二子区块:16 个 MCP 服务器

- 左列:小标题"16 个专用社交媒体数据 MCP 服务器" + 描述 + 端点模式 + 原生客户端徽章
- 右列:YouTube 视频缩略图 + 视频下方的描述
- **YouTube 视频用占位缩略图 + 跳转链接**(不用 iframe)

### 7.4 第三子区块:4 个能力卡片

- 公开视频数据分析 / 评论互动分析 / 公开账号数据分析 / 深度趋势报告
- 2x2 网格,白底圆角

### 7.5 第四子区块:Chrome 扩展

- 右列:5 个平台徽章 + 2 个特性徽章 + 2 个按钮

### 7.6 第五子区块:TDIYX 徽章

- 5 个圆形字母徽章(T / D / I / Y / X)横排

---

## 8. 数据集(静态)

### 8.1 布局

- 标题 / 副标题居中
- 左列:3 张堆叠的数据集示例图(轻微旋转,逐层偏移)
  - 1: `tiktok-video-dataset-export.webp`
  - 2: `social-profile-dataset-fields.webp`
  - 3: `social-engagement-dataset.webp`
- 右列:定价表 + 链接

### 8.2 定价表

- 3 列: 记录数 / 起价 / 每千条
- 2 行数据: 100K-500K $150 $2.00 / 20M+ $8,000 $0.40

---

## 9. 市场与合作伙伴(暗色区块)

### 9.1 整块暗色

- 容器背景: `bg-inverse` = `rgb(26, 26, 26)`
- 圆角 24px
- 文字反白

### 9.2 左侧内容

- 顶部: "市场与合作伙伴" 紫字 + "2026 年 8 月上线" 紫胶囊徽章
- 大标题(白色)
- 描述
- 3 列小卡: AI 智能体 / API / 网络数据采集方案
- 2 个按钮: 提交您的产品 / 合作伙伴计划
- 底部: "现已开放早期提交审核——联系我们,在线前锁定您的位置。"

### 9.3 右侧:佣金对比卡

- "仅收取 10% 佣金" (h3)
- "对比约 20% 的行业标准佣金。"
- 2 行对比:
  - TikHub 10% (紫色进度条,约 50% 宽)
  - 行业标准 约 20% (灰色进度条,约 100% 宽)

---

## 10. 你能构建什么(6 个 tab,点击驱动)

### 10.1 tab 列表

- AI 智能体 / RAG 与检索 / 训练数据 / SaaS 产品 / 内容智能 / 自动化与工作流
- 默认 AI 智能体激活
- 垂直布局(左侧 tab,右侧内容)

### 10.2 tab 视觉

- 激活态:白底 + 浅边框 + 紫色图标背景
- 非激活态:透明底 + 灰色图标

### 10.3 每个 tab 的内容结构

```
[紫色图标]  [h3 标题]
[描述段]
[示例卡(灰底圆角)]
  标题: 示例
  内容: 一行描述
[链接: 探索此使用场景 →]
```

**6 个 tab 都需要独立内容。** 已确认:
- AI 智能体: "构建能读取实时社交数据并据此行动的自主智能体..." / "Claude + TikHub MCP → 智能体根据实时趋势起草内容策略"
- RAG 与检索: "用新鲜、结构化的社交内容驱动检索增强生成..." / "检索今日帖子 + 评论 → 在回答中引用实时来源"

---

## 11. CTA 收尾(简单)

- 居中标题 "社媒数据不会自己变成结构化。"
- 副标题 "注册即送 $0.05 免费额度——足以测试任意端点。无需信用卡。"
- 2 个按钮: 免费开始(黑底)/ 预约通话(白底)
- 简单居中布局

---

## 12. 页脚

### 12.1 整体

- 暗色 `bg-inverse` = `rgb(26, 26, 26)`
- 文字白色 / 浅灰
- 顶部细线分隔

### 12.2 6 列布局

1. Logo + 描述 + 社交图标
2. 产品 (5 链接)
3. 平台 API (5 链接)
4. 开发者 (10 链接)
5. 公司 (7 链接)
6. 法律 (2 链接)

### 12.3 底部

- 免责声明段
- © 2026 TikHub. 保留所有权利。
- 邮箱 team@tikhub.io(链接)
- Discord 链接

---

## 13. 浮动 UI

### 13.1 Scroll-to-top 按钮

- 圆形,深色 `bg-inverse` 背景
- 白色上箭头
- 固定在右下角
- 仅在滚动后出现(从位置 ~700px 之后可见)
- 点击平滑滚回顶部

---

## 14. 暗色模式行为

- 切换: 点击顶栏月亮/太阳按钮
- 触发: `html.classList.toggle('dark')`
- token 重映射:所有 shadcn 颜色 token 自动切换
- 浅色 → 深色:
  - 背景: `rgb(250, 250, 249)` → 暗色
  - 卡片: `rgb(255, 255, 255)` → 暗色
  - 文字: 黑 → 白
  - `bg-inverse` 区块: 仍是深色
- 渐变文字、徽章、紫色强调色在两种模式下都保留

---

## 15. 移动端 (390px) 行为

- Header:logo + 搜索图标 + 汉堡菜单
- Hero:徽章 → 标题 → 副标题 → 2x2 按钮网格 → 4 个统计竖排
- 平台 marquee:每张卡片变窄 (`w-[220px]`),保持 2 行反向
- API Preview:左列右列堆叠
- MCP:Demo 视频变窄
- 数据集:3 张图从层叠变垂直堆叠
- 市场区块:左列右列堆叠
- Use Cases:tab 列表 + 内容从左右变上下
- Footer:6 列变 2 列或多列堆叠
- 断点: `md:` 768px, `lg:` 1024px

---

## 16. 性能 / 实现备注

- 平台 marquee 用纯 CSS 动画 (`@keyframes`)
- card-swap 可以用纯 CSS(translateY + opacity)或简单 useState
- Hero 旋转徽章用 `useState + setInterval`
- 没有 Lenis / 平滑滚动库
- 没有 IntersectionObserver 区块切换
- 唯一观察到的 useState 状态:语言 tab、MCP tab、use case tab
- 暗色模式用 `html.dark` class + CSS 变量

---

## 17. builder 必读警告

1. **不要给 marquee 加 JS 滚动控制** — 必须纯 CSS animation
2. **不要给任何区块加 scroll-driven 切换** — 全部点击驱动
3. **保持暗黑模式完整工作** — 切换按钮 + token 重映射都正确
4. **保留平台 marquee 双向反向** — 2 行不同方向是关键视觉
5. **保留 hero 旋转徽章** — 至少 4 个不同文案循环
6. **保留 hero 标题渐变 + 角标** — "为" 字周围虚线方框
7. **保留 card-swap 视觉** — 3 张代码卡堆叠,顶部卡占满,下方各露 35-40px
8. **保留 GDPR/CCPA 圆形盾牌徽章** — 不要简化为文字
9. **YouTube 视频用占位 + 跳转** — 不用 iframe
10. **MCP 4 个 tab 都要可点击切换内容**
11. **6 个 use case 都要有完整内容**
12. **市场区"在线前" 错别字保留** — 这是原站笔误
