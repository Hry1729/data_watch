# Spec: Batch C — API Preview + MCP + Use Cases + Footer

**目标:** 实现剩余 4 个区块(本批次最大)
**复杂度:** 高
**预计代码量:** ~1200 行

---

## 文件清单

| 文件 | 操作 | 内容 |
|---|---|---|
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/ApiPreview.tsx` | 新建 | API 预览区(左列 + card-swap) |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/ApiCodeCard.tsx` | 新建 | 单个代码窗口子组件(可被 ApiPreview 内部 import) |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/McpIntegration.tsx` | 新建 | MCP 集成区(5 子区块) |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/UseCases.tsx` | 新建 | 6 tab 区块 |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Footer.tsx` | 新建 | 页脚 |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/ScrollToTop.tsx` | 新建 | 浮动回顶按钮 |

**依赖:** `../shared/icons.tsx`(全部), `../shared/types.ts`(CodeWindow, UseCase, MCPTab, CodeLanguage), `globals.css`(`.code-editor`, `.code-keyword` 等语法高亮)

---

## 1. ApiPreview (`ApiPreview.tsx`)

### 1.1 布局

```
<section className="scroll-mt-20">
  <div className="max-w-7xl mx-auto px-6 py-20 md:py-28">
    <div className="grid lg:grid-cols-2 gap-12 items-start">
      <LeftIntro />
      <CodeCardSwap />
    </div>
    <DeveloperFeatures />  // 底部 3 列小卡,跨整行
  </div>
</section>
```

### 1.2 LeftIntro — 标题 + 描述 + 语言 tabs + 3 bullet + 3 链接

```tsx
'use client';
import { useState } from 'react';
import { ArrowRightIcon } from '../shared/icons';
import type { CodeLanguage } from '../shared/types';

const LANGUAGES: { id: CodeLanguage; label: string }[] = [
  { id: 'python',     label: 'Python' },
  { id: 'javascript', label: 'JavaScript' },
  { id: 'curl',       label: 'cURL' },
  { id: 'java',       label: 'Java' },
  { id: 'c',          label: 'C' },
  { id: 'cpp',        label: 'C++' },
];

const BULLETS = [
  '统一的响应结构,跨所有平台保持一致',
  '自动分页、速率限制、重试处理',
  '类型化 SDK,带本地缓存与故障转移',
];

const LINKS = [
  { text: '查看 API 文档',   href: '#' },
  { text: '下载 SDK',         href: '#' },
  { text: '请求沙盒密钥',     href: '#' },
];

function LeftIntro() {
  const [active, setActive] = useState<CodeLanguage>('python');
  return (
    <div>
      <h2 className="text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight">
        查看 API 运行效果
      </h2>
      <p className="mt-4 text-muted-foreground">
        简洁一致的 REST 接口。HTTP 200,JSON 响应,一行代码就能拿到 16 个平台的真实数据。
      </p>
      <div className="mt-6 flex flex-wrap gap-2">
        {LANGUAGES.map(l => (
          <button
            key={l.id}
            type="button"
            onClick={() => setActive(l.id)}
            className={
              'rounded-md px-3 py-1.5 text-xs font-medium transition-colors ' +
              (active === l.id
                ? 'bg-inverse text-surface'
                : 'bg-surface text-muted-foreground border border-border hover:bg-muted')
            }
          >
            {l.label}
          </button>
        ))}
      </div>
      <ul className="mt-8 space-y-3 text-sm">
        {BULLETS.map(b => (
          <li key={b} className="flex items-start gap-2">
            <CheckIcon className="mt-0.5 h-4 w-4 shrink-0 text-accent" />
            <span>{b}</span>
          </li>
        ))}
      </ul>
      <div className="mt-8 flex flex-wrap gap-4">
        {LINKS.map(l => (
          <a key={l.text} href={l.href} className="inline-flex items-center gap-1 text-sm font-medium hover:underline">
            {l.text} <ArrowRightIcon className="w-4 h-4" />
          </a>
        ))}
      </div>
    </div>
  );
}
```

**注意:** `LeftIntro` 接收 `active` 状态是**不行的** — 需要把状态提升到 `ApiPreview` 顶层,然后传给 `LeftIntro` 和 `CodeCardSwap`。

**修正:** `ApiPreview` 顶层 `useState<CodeLanguage>('python')`,LeftIntro 接收 `active`/`onChange` props,CodeCardSwap 接收 `active` prop。

### 1.3 CodeCardSwap — 3 卡片堆叠

参考 `BEHAVIORS.md §6.3`:
- 3 张代码窗口卡片堆叠
- 顶部的卡片全显示,下面 2 张只露 35-40px 边缘
- 卡片背景 `bg-[#1E1E2E]`(globals.css 有 `.code-editor` 但这里直接用 raw 颜色)
- macOS 圆点 + 文件名标签
- 自动轮播(可选实现:用 `useEffect` 每 4 秒轮换;或简化为静态展示 3 张)

```tsx
'use client';
import { useEffect, useState } from 'react';
import type { CodeWindow, CodeLanguage } from '../shared/types';

const CODE_WINDOWS: CodeWindow[] = [
  {
    id: 'tiktok',
    filename: 'tiktok_example.py',
    platform: 'TikTok',
    samples: {
      python: `import tikhub\n\napi = tikhub.TikHub(api_key="<YOUR_KEY>")\nvideo = api.tiktok.video.get("https://www.tiktok.com/@user/video/123")\nprint(video.stats)`,
      javascript: `import { TikHub } from "tikhub";\n\nconst api = new TikHub({ apiKey: "<YOUR_KEY>" });\nconst video = await api.tiktok.video.get("https://...");\nconsole.log(video.stats);`,
      curl: `curl -X GET "https://api.tikhub.io/tiktok/video?url=https://..." \\\n  -H "Authorization: Bearer <YOUR_KEY>"`,
      java: `TikHub api = TikHub.builder().apiKey("<KEY>").build();\nVideo v = api.tiktok().video().get("https://...");\nSystem.out.println(v.stats());`,
      c: `// C SDK 示例 - 简化\ntikhub_client_t* c = tikhub_new("<KEY>");\ntikhub_tiktok_video_t* v = tikhub_tiktok_video_get(c, "https://...");`,
      cpp: `auto client = tikhub::Client("<KEY>");\nauto video = client.tiktok().video().get("https://...");\nstd::cout << video.stats << std::endl;`,
    },
  },
  {
    id: 'instagram',
    filename: 'instagram_example.py',
    platform: 'Instagram',
    samples: {
      python: `import tikhub\n\napi = tikhub.TikHub(api_key="<YOUR_KEY>")\npost = api.instagram.post.get("https://www.instagram.com/p/CODE/")\nprint(post.likes, post.comments)`,
      javascript: `import { TikHub } from "tikhub";\nconst post = await api.instagram.post.get("https://www.instagram.com/p/CODE/");\nconsole.log(post.likes, post.comments);`,
      curl: `curl "https://api.tikhub.io/instagram/post?url=..." -H "Authorization: Bearer <KEY>"`,
      java: `Post p = api.instagram().post().get("https://...");`,
      c: `// C SDK Instagram 调用占位`,
      cpp: `auto post = client.instagram().post().get("https://...");`,
    },
  },
  {
    id: 'twitter',
    filename: 'x_twitter_example.py',
    platform: 'X / Twitter',
    samples: {
      python: `import tikhub\n\napi = tikhub.TikHub(api_key="<YOUR_KEY>")\ntweet = api.twitter.tweet.get("https://x.com/user/status/123")\nprint(tweet.metrics)`,
      javascript: `const tweet = await api.twitter.tweet.get("https://x.com/user/status/123");\nconsole.log(tweet.metrics);`,
      curl: `curl "https://api.tikhub.io/twitter/tweet?url=..." -H "Authorization: Bearer <KEY>"`,
      java: `Tweet t = api.twitter().tweet().get("https://...");`,
      c: `// C SDK Twitter 调用占位`,
      cpp: `auto tweet = client.twitter().tweet().get("https://...");`,
    },
  },
];

function CodeCardSwap({ active }: { active: CodeLanguage }) {
  const [topIdx, setTopIdx] = useState(0);
  useEffect(() => {
    const t = setInterval(() => setTopIdx(i => (i + 1) % CODE_WINDOWS.length), 4500);
    return () => clearInterval(t);
  }, []);
  return (
    <div className="relative h-[480px] md:h-[520px]">
      {CODE_WINDOWS.map((w, i) => {
        const offset = (i - topIdx + CODE_WINDOWS.length) % CODE_WINDOWS.length;
        return (
          <ApiCodeCard
            key={w.id}
            window={w}
            language={active}
            offset={offset}
            isTop={offset === 0}
          />
        );
      })}
      <div className="absolute bottom-4 left-1/2 -translate-x-1/2 flex gap-1.5 z-30">
        {CODE_WINDOWS.map((_, i) => (
          <button
            key={i}
            type="button"
            onClick={() => setTopIdx(i)}
            aria-label={`切换到第 ${i + 1} 个示例`}
            className={
              'h-1.5 rounded-full transition-all ' +
              (i === topIdx ? 'w-6 bg-foreground' : 'w-1.5 bg-foreground/30 hover:bg-foreground/50')
            }
          />
        ))}
      </div>
    </div>
  );
}
```

### 1.4 ApiCodeCard (`ApiCodeCard.tsx`)

```tsx
import type { CodeWindow, CodeLanguage } from '../shared/types';

export function ApiCodeCard({
  window: w, language, offset, isTop,
}: { window: CodeWindow; language: CodeLanguage; offset: number; isTop: boolean }) {
  // offset 0 = 顶部,1 = 第二张,2 = 第三张
  const transforms = [
    'translateY(0px) scale(1)',
    'translateY(40px) scale(0.97)',
    'translateY(70px) scale(0.94)',
  ];
  return (
    <div
      className="absolute inset-x-0 rounded-2xl overflow-hidden border border-white/10 bg-[#1E1E2E] text-[#cdd6f4] shadow-2xl transition-all duration-500"
      style={{
        transform: transforms[offset],
        opacity: offset === 0 ? 1 : 0.92,
        zIndex: 30 - offset,
      }}
    >
      {/* macOS 圆点 */}
      <div className="flex items-center gap-1.5 px-4 py-3 border-b border-white/5">
        <span className="h-3 w-3 rounded-full bg-[#f38ba8]" />
        <span className="h-3 w-3 rounded-full bg-[#f9e2af]" />
        <span className="h-3 w-3 rounded-full bg-[#a6e3a1]" />
        <span className="ml-4 text-xs text-[#9399b2] font-mono">{w.filename}</span>
      </div>
      {/* 代码区 */}
      <pre className="p-5 font-mono text-xs leading-relaxed overflow-x-auto h-[400px]">
        <code>{w.samples[language]}</code>
      </pre>
    </div>
  );
}
```

**注意:**
- 颜色 hex 直接写在 className/style 里,**不**依赖 globals.css 的 `.code-editor`(因为这里需要更精确的颜色控制)
- `font-mono` 用 Geist Mono
- 代码内容用 monospace 字体显示

### 1.5 DeveloperFeatures — 底部 3 列小卡

```tsx
const FEATURES = [
  { title: '快速接入', desc: '一行代码获取数据,5 分钟集成到你的应用。' },
  { title: '全面文档', desc: '覆盖所有 1,000+ 接口,带 curl/Python/JS 示例。' },
  { title: '高可用架构', desc: '99.95% SLA,多区域部署,自动故障转移。' },
];

function DeveloperFeatures() {
  return (
    <div className="mt-16 grid grid-cols-1 md:grid-cols-3 gap-4">
      {FEATURES.map(f => (
        <div key={f.title} className="rounded-2xl border border-border bg-card p-6">
          <h3 className="font-semibold">{f.title}</h3>
          <p className="mt-2 text-sm text-muted-foreground">{f.desc}</p>
        </div>
      ))}
    </div>
  );
}
```

---

## 2. McpIntegration (`McpIntegration.tsx`)

### 2.1 5 子区块总览

```
<section className="scroll-mt-20">
  <div className="max-w-7xl mx-auto px-6 py-20 md:py-28 space-y-20">
    <SubA_DemoTabs />
    <SubB_McpServers />
    <SubC_Capabilities />
    <SubD_ChromeExtension />
    <SubE_TdiyxBadge />
  </div>
</section>
```

### 2.2 SubA — 4 tab 演示

参考 `BEHAVIORS.md §7.1-7.2`:
- 4 个 tab: TikTok 204 / Instagram 82 / Twitter 13 / "全部 16 平台"(链接)
- 切换 tab 更新左列 `claude_desktop_config.json` 代码 + 右列文字

```tsx
'use client';
import { useState } from 'react';
import { ArrowRightIcon } from '../shared/icons';
import type { MCPTab } from '../shared/types';

const MCP_TABS: MCPTab[] = [
  {
    id: 'tiktok', label: 'TikTok', count: 204,
    serverKey: 'tikhub-tiktok',
    url: 'https://mcp.tikhub.io/tiktok/mcp',
    configJson: `{\n  "mcpServers": {\n    "tikhub-tiktok": {\n      "command": "npx",\n      "args": ["-y", "@tikhub/mcp", "--platform=tiktok"],\n      "env": { "TIKHUB_API_KEY": "<YOUR_KEY>" }\n    }\n  }\n}`,
    rightTitle: 'Claude Desktop',
    rightDescription: '把 TikTok 实时数据接入 Claude Desktop。204 个工具覆盖视频、用户、评论、趋势。',
    rightStats: [
      { label: 'MCP 工具', value: '204' },
      { label: '平台 MCP', value: '16' },
      { label: '集成时间', value: '3 min' },
    ],
  },
  // ... instagram, twitter 同样结构
];
```

(完整数据由 builder 提取原站,这里只列 1 个示例)

### 2.3 左列代码块

```tsx
<div className="rounded-2xl bg-muted-soft border border-border overflow-hidden">
  <div className="px-4 py-2.5 border-b border-border bg-muted/40 flex items-center gap-2">
    <FileTextIcon className="w-4 h-4 text-muted-foreground" />
    <span className="text-xs font-mono text-muted-foreground">~/Library/Application Support/Claude/claude_desktop_config.json</span>
  </div>
  <pre className="p-5 font-mono text-xs leading-relaxed overflow-x-auto">
    <code>{activeTab.configJson}</code>
  </pre>
</div>
```

### 2.4 SubB — 16 MCP 服务器 + YouTube 占位

左列:文字 + 端点模式 `mcp.tikhub.io/{platform}/mcp` + 5 个徽章(Cursor/Cline/Cherry Studio/LangChain/LangGraph)

右列:YouTube 视频占位 + 跳转链接(不用 iframe)

```tsx
<a
  href="https://www.youtube.com/watch?v=IsstyDED2nc"
  target="_blank"
  rel="noopener noreferrer"
  className="group relative block aspect-video rounded-2xl overflow-hidden bg-inverse border border-border"
>
  <img
    src="https://img.youtube.com/vi/IsstyDED2nc/maxresdefault.jpg"
    alt="TikHub MCP 演示视频"
    className="w-full h-full object-cover transition-transform group-hover:scale-105"
  />
  <div className="absolute inset-0 bg-black/30 flex items-center justify-center">
    <div className="w-20 h-20 rounded-full bg-white/90 flex items-center justify-center transition-transform group-hover:scale-110">
      <PlayIcon className="w-10 h-10 text-black ml-1" />
    </div>
  </div>
  <div className="absolute bottom-4 right-4 px-3 py-1.5 bg-red-600 text-white text-xs font-semibold rounded">
    在 YouTube 上观看 ↗
  </div>
</a>
```

### 2.5 SubC — 4 能力卡片 (2x2)

```tsx
const CAPABILITIES = [
  { title: '公开视频数据分析', desc: '视频元数据、播放量、点赞、分享、评论、标签、音乐等。' },
  { title: '评论互动分析',     desc: '评论文本、用户、点赞、回复、情感倾向、时间线。' },
  { title: '公开账号数据分析', desc: '粉丝、内容、增长曲线、活跃度、地理分布。' },
  { title: '深度趋势报告',     desc: '平台趋势、热门话题、爆款内容、行业洞察。' },
];

<div className="grid grid-cols-1 md:grid-cols-2 gap-4">
  {CAPABILITIES.map(c => (
    <div key={c.title} className="rounded-2xl border border-border bg-card p-6">
      <h3 className="font-semibold">{c.title}</h3>
      <p className="mt-2 text-sm text-muted-foreground">{c.desc}</p>
    </div>
  ))}
</div>
```

### 2.6 SubD — Chrome 扩展

```tsx
<div className="grid lg:grid-cols-2 gap-12 items-center">
  <div>
    <h2 className="text-3xl font-bold tracking-tight">Chrome 扩展:边看边抓</h2>
    <p className="mt-4 text-muted-foreground">
      安装我们的 Chrome 扩展,直接在浏览器里调用 TikHub API。无需复制粘贴、无需切换工具。
    </p>
    <div className="mt-6 flex flex-wrap gap-2">
      {/* 5 个平台徽章 */}
      {['TikTok', 'Douyin', 'Instagram', 'X', 'Bilibili'].map(p => (
        <span key={p} className="rounded-full bg-muted px-3 py-1 text-xs font-medium">{p}</span>
      ))}
    </div>
    <div className="mt-6 flex flex-wrap gap-2">
      <span className="rounded-full border border-accent/30 bg-accent/10 px-3 py-1 text-xs text-accent">API 密钥安全</span>
      <span className="rounded-full border border-accent/30 bg-accent/10 px-3 py-1 text-xs text-accent">无追踪</span>
    </div>
    <div className="mt-6 flex flex-wrap gap-3">
      <a className="inline-flex items-center gap-2 rounded-lg bg-inverse text-surface px-4 py-2.5 text-sm font-medium">添加到 Chrome</a>
      <a className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-4 py-2.5 text-sm font-medium">了解更多</a>
    </div>
  </div>
  <div>{/* 占位 Chrome 扩展 UI 截图 - 用一个简化的 div mockup */}</div>
</div>
```

(Chrome 截图用 `<div>` mockup,不用真实图片)

### 2.7 SubE — TDIYX 徽章

5 个圆形字母徽章(T/D/I/Y/X)横排:

```tsx
const TDIYX = [
  { letter: 'T', color: 'bg-pink-500' },
  { letter: 'D', color: 'bg-blue-500' },
  { letter: 'I', color: 'bg-purple-500' },
  { letter: 'Y', color: 'bg-red-500' },
  { letter: 'X', color: 'bg-black' },
];

<div className="flex items-center gap-4 flex-wrap">
  {TDIYX.map(b => (
    <div key={b.letter} className={`h-12 w-12 rounded-full ${b.color} text-white flex items-center justify-center text-xl font-bold`}>
      {b.letter}
    </div>
  ))}
  <div>
    <p className="font-semibold">990+ 工具覆盖 16 个平台</p>
    <p className="text-sm text-muted-foreground">一个 API 密钥即可使用</p>
  </div>
  <a href="#" className="ml-auto inline-flex items-center gap-1 text-sm font-medium text-accent hover:underline">
    查看完整设置指南 <ArrowRightIcon className="w-4 h-4" />
  </a>
</div>
```

---

## 3. UseCases (`UseCases.tsx`)

### 3.1 6 tab 数据

```tsx
import { BotIcon, DatabaseIcon, FileTextIcon, LayersIcon, LightbulbIcon, WorkflowIcon } from '../shared/icons';
import type { UseCase } from '../shared/types';

const USE_CASES: UseCase[] = [
  {
    id: 'agent',
    label: 'AI 智能体',
    icon: BotIcon,
    title: '让 AI 智能体直接消费社交数据',
    description: '构建能读取实时社交数据并据此行动的自主智能体。MCP 协议让 Claude、Cursor、Cherry Studio 等 5 分钟内接入。',
    example: {
      label: '示例',
      text: 'Claude + TikHub MCP → 智能体根据实时趋势起草内容策略,自动检查合规,并提交到发布队列。',
    },
    link: { text: '探索 AI 智能体', href: '#' },
  },
  {
    id: 'rag',
    label: 'RAG 与检索',
    icon: DatabaseIcon,
    title: '用新鲜、结构化的社交内容驱动 RAG',
    description: '检索今日帖子、评论、趋势,让你的 LLM 在回答中引用实时来源,而不是 2021 年的训练数据。',
    example: {
      label: '示例',
      text: '检索今日热门 → 嵌入 + 重排 → 在回答中以引用形式给出实时来源链接。',
    },
    link: { text: '探索 RAG 与检索', href: '#' },
  },
  {
    id: 'training',
    label: '训练数据',
    icon: FileTextIcon,
    title: '为模型微调准备高质量社交语料',
    description: '10亿+ 记录,涵盖 16 个平台,字段文档化,使用条款清晰。直接下载 CSV/Parquet 用于微调。',
    example: {
      label: '示例',
      text: '下载 1M 条 Reddit 评论 + 元数据 → 清洗 → 微调 LLaMA 风格分类器,识别金融情绪。',
    },
    link: { text: '浏览数据集', href: '#' },
  },
  {
    id: 'saas',
    label: 'SaaS 产品',
    icon: LayersIcon,
    title: '为你的 SaaS 添加社交数据能力',
    description: '营销、舆情、内容运营、社群管理 — 4 行代码集成社交数据,让你的产品立刻具备市场差异化。',
    example: {
      label: '示例',
      text: '营销 SaaS → 客户输入品牌名 → 自动拉取跨平台提及、情感、热门话题、竞品对比。',
    },
    link: { text: '查看集成案例', href: '#' },
  },
  {
    id: 'content',
    label: '内容智能',
    icon: LightbulbIcon,
    title: '内容选题、爆款分析、跨平台分发',
    description: '追踪你所在品类的爆款,自动生成选题建议,跨平台同步分发,把内容生产从一周缩短到一天。',
    example: {
      label: '示例',
      text: '监控 3 个对标账号 → 识别增长最快的 10 个视频 → 提取共同特征 → 给出 5 个选题方向。',
    },
    link: { text: '探索内容智能', href: '#' },
  },
  {
    id: 'automation',
    label: '自动化',
    icon: WorkflowIcon,
    title: '用工作流串联社交数据与下游动作',
    description: '当某个事件触发(帖子爆款、负面评论、用户增长),自动执行下一步:通知、写入数据库、调度内容。',
    example: {
      label: '示例',
      text: 'YouTube 视频突破 100K 播放 → 触发 → 抓取评论 + 情感分析 → 写入 Notion + 推送 Slack。',
    },
    link: { text: '查看自动化模板', href: '#' },
  },
];
```

### 3.2 主组件

```tsx
'use client';
import { useState } from 'react';
import { ArrowRightIcon, CheckIcon } from '../shared/icons';
import { USE_CASES } from './use-cases-data';  // 或内联

export function UseCases() {
  const [active, setActive] = useState(USE_CASES[0].id);
  const tab = USE_CASES.find(u => u.id === active)!;
  const Icon = tab.icon;
  return (
    <section className="scroll-mt-20">
      <div className="max-w-7xl mx-auto px-6 py-20 md:py-28">
        <div className="text-center mb-12">
          <p className="text-sm text-muted-foreground">用 16 个平台,只需 1 个 API 密钥</p>
          <h2 className="mt-2 text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight">
            你能构建什么
          </h2>
        </div>
        <div className="grid lg:grid-cols-[280px_1fr] gap-8">
          {/* tab 列表 */}
          <div className="flex lg:flex-col gap-2 overflow-x-auto lg:overflow-visible">
            {USE_CASES.map(u => {
              const TabIcon = u.icon;
              const isActive = u.id === active;
              return (
                <button
                  key={u.id}
                  type="button"
                  onClick={() => setActive(u.id)}
                  className={
                    'flex items-center gap-3 rounded-xl px-4 py-3 text-left transition-colors whitespace-nowrap ' +
                    (isActive
                      ? 'bg-card border border-border shadow-sm'
                      : 'hover:bg-muted/50 text-muted-foreground')
                  }
                >
                  <span className={
                    'flex h-8 w-8 items-center justify-center rounded-lg ' +
                    (isActive ? 'bg-accent/15 text-accent' : 'text-muted-foreground')
                  }>
                    <TabIcon className="h-4 w-4" />
                  </span>
                  <span className="text-sm font-medium">{u.label}</span>
                </button>
              );
            })}
          </div>
          {/* 内容 */}
          <div>
            <div className="flex items-center gap-3 mb-4">
              <div className="h-12 w-12 rounded-xl bg-accent/15 text-accent flex items-center justify-center">
                <Icon className="h-6 w-6" />
              </div>
              <h3 className="text-2xl md:text-3xl font-semibold tracking-tight">{tab.title}</h3>
            </div>
            <p className="text-muted-foreground">{tab.description}</p>
            <div className="mt-6 rounded-xl bg-muted-soft border border-border p-5">
              <p className="text-xs font-medium text-muted-foreground uppercase tracking-wide">{tab.example.label}</p>
              <p className="mt-2 text-sm">{tab.example.text}</p>
            </div>
            <a href={tab.link.href} className="mt-6 inline-flex items-center gap-1 text-sm font-medium text-accent hover:underline">
              {tab.link.text} <ArrowRightIcon className="w-4 h-4" />
            </a>
          </div>
        </div>
      </div>
    </section>
  );
}
```

**注意:**
- `USE_CASES` 数据可以放在组件文件内,或提取为 `use-cases-data.ts`(如果你觉得太长)
- 默认激活第 0 个(id='agent')
- 移动端 tab 列表变横向滚动

---

## 4. Footer (`Footer.tsx`)

### 4.1 整体

```
<footer className="bg-inverse text-inverse-foreground">
  <div className="max-w-7xl mx-auto px-6 py-16">
    <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-8 mb-12">
      <BrandColumn />
      <LinkColumn title="产品" items={...} />
      <LinkColumn title="平台 API" items={...} />
      <LinkColumn title="开发者" items={...} />
      <LinkColumn title="公司" items={...} />
      <LinkColumn title="法律" items={...} />
    </div>
    <div className="border-t border-white/10 pt-8">
      <p className="text-sm text-white/60">免责声明段</p>
      <div className="mt-4 flex flex-wrap items-center justify-between gap-4">
        <p className="text-sm text-white/60">© 2026 TikHub. 保留所有权利。</p>
        <div className="flex items-center gap-4 text-sm text-white/60">
          <a href="mailto:team@tikhub.io">team@tikhub.io</a>
          <a href="#">Discord</a>
        </div>
      </div>
    </div>
  </div>
</footer>
```

### 4.2 列数据(虚构,内容合理即可)

```tsx
const COLUMNS = [
  { title: '产品', items: ['数据 API', '数据集', 'MCP 服务', '市场', '合作伙伴'] },
  { title: '平台 API', items: ['TikTok API', 'Instagram API', 'X / Twitter API', 'YouTube API', '小红书 API'] },
  { title: '开发者', items: ['API 文档', 'SDK 下载', 'API 状态', '更新日志', '速率限制', '错误码', 'Webhooks', 'MCP 指南', '代码示例', '社区'] },
  { title: '公司', items: ['关于我们', '博客', '招聘', '新闻', '联系方式', '品牌资源', '案例研究'] },
  { title: '法律', items: ['服务条款', '隐私政策'] },
];
```

### 4.3 BrandColumn

```tsx
function BrandColumn() {
  return (
    <div className="col-span-2 md:col-span-3 lg:col-span-1">
      <a href="/" className="inline-flex items-center gap-2">
        <Image src="/sites/.../logo.jpeg" width={32} height={32} alt="TikHub" className="rounded" />
        <span className="text-lg font-semibold">TikHub</span>
      </a>
      <p className="mt-4 text-sm text-white/60">
        社交数据基础设施,为 AI 时代而生。
      </p>
      <div className="mt-4 flex items-center gap-3">
        <a href="#" className="text-white/60 hover:text-white"><XTwitterIcon className="h-5 w-5" /></a>
        <a href="#" className="text-white/60 hover:text-white"><LinkedinIcon className="h-5 w-5" /></a>
        <a href="#" className="text-white/60 hover:text-white">{/* Discord 简化 */}💬</a>
      </div>
    </div>
  );
}
```

### 4.4 LinkColumn

```tsx
function LinkColumn({ title, items }: { title: string; items: string[] }) {
  return (
    <div>
      <h4 className="text-sm font-semibold text-white">{title}</h4>
      <ul className="mt-4 space-y-2">
        {items.map(item => (
          <li key={item}>
            <a href="#" className="text-sm text-white/60 hover:text-white transition-colors">{item}</a>
          </li>
        ))}
      </ul>
    </div>
  );
}
```

### 4.5 免责声明

"本服务仅提供对公开可访问的社交媒体数据的接口。所有商标和内容归各自平台所有。使用前请阅读各平台的服务条款。"

### 4.6 移动端
- 列数: 2 列(每个 LinkColumn 占 1 列)
- BrandColumn 跨 2 列

---

## 5. ScrollToTop (`ScrollToTop.tsx`)

```tsx
'use client';
import { useEffect, useState } from 'react';
import { ArrowUpIcon } from '../shared/icons';

export function ScrollToTop() {
  const [show, setShow] = useState(false);
  useEffect(() => {
    const onScroll = () => setShow(window.scrollY > 600);
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);
  if (!show) return null;
  return (
    <button
      type="button"
      onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
      className="fixed bottom-6 right-6 z-50 flex h-11 w-11 items-center justify-center rounded-full bg-inverse text-inverse-foreground shadow-lg transition-transform hover:scale-110"
      aria-label="回到顶部"
    >
      <ArrowUpIcon className="h-5 w-5" />
    </button>
  );
}
```

---

## 验证

1. `npm run typecheck` 通过
2. `npm run build` 通过
3. 提交 commit
4. 报告文件清单 + 验证结果

**特别注意:**
- `ApiPreview` 需要 `'use client'` 因为有 `useState`
- `McpIntegration` 需要 `'use client'` 因为有 tab state
- `UseCases` 需要 `'use client'` 因为有 tab state
- `ScrollToTop` 需要 `'use client'` 因为有 window listener
- `Footer` 可以是 server component

**不要:**
- 不要触碰 `globals.css`、`shared/*`
- 不要重命名已存在的组件
- 不要实现页面装配(`page.tsx`)
