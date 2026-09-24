# Spec: Batch A — Header + Hero + Stats + CTA

**目标:** 实现页面前 4 个区块 + 暗黑模式切换
**复杂度:** 中
**预计代码量:** ~500 行

---

## 文件清单(本批次要新建/修改)

| 文件 | 操作 | 内容 |
|---|---|---|
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Header.tsx` | 新建 | 顶部导航 |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Hero.tsx` | 新建 | Hero 区块 |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/StatsStrip.tsx` | 新建 | 平台统计条 |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/CtaSection.tsx` | 新建 | CTA 收尾 |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/ThemeToggle.tsx` | 新建 | 暗黑模式切换按钮(子组件) |

**不要修改:** `layout.tsx`、`globals.css`、`shared/*`、`page.tsx`(本批次不创建)

---

## 1. Header (`Header.tsx`)

### 1.1 结构

```
<header className="relative z-10">
  <div className="max-w-7xl mx-auto px-6 h-16 flex items-center justify-between gap-4">
    <Logo />
    <nav className="hidden md:flex items-center gap-1">主 nav</nav>
    <div className="flex items-center gap-2">右侧工具</div>
  </div>
</header>
```

### 1.2 Logo
- `<a href="/">` 包 `<img src="/sites/tikhub-io-5f62aaaa/shared/logo.jpeg" width={32} height={32} />`
- Next.js `Image` 组件需要 alt='TikHub'

### 1.3 主 nav(5 项)
- 产品(button + 占位下拉)
- 解决方案(button + 占位下拉)
- 开发者(button + 占位下拉)
- 定价(`<a href="#">`)
- 企业版(`<a href="#">`)

**占位下拉规范:**
- 使用 `details` + `summary`,或受控 `useState`
- 每个下拉含 4-6 个链接,文字可虚构但要合理(如产品: 数据 API、数据集、MCP 服务、合作伙伴)

### 1.4 右侧工具(5 项)
- **搜索按钮:** 文本"搜索" + 右侧 `kbd` 徽章 `⌘K`(移动端隐藏 kbd)
- **暗黑模式切换:** `<ThemeToggle />`(单独子组件)
- **语言选择:** 按钮 "CN 中文" + ChevronDown,**占位**(点击无效果)
- **登录:** `<a href="https://user.tikhub.io/" target="_blank">登录</a>`
- **预约通话:** `<a href="https://calendar.app.google/AQLMWVdfAksrWeHh9" target="_blank" className="bg-inverse text-surface">预约通话</a>`

### 1.5 移动端
- md 以下:logo + 搜索图标 + ThemeToggle + 汉堡按钮(展开/收起主 nav)
- 主 nav 在 md 以下变垂直堆叠

### 1.6 注意
- Header 背景透明(不 sticky,继承 body 背景)
- 文字颜色 `text-foreground`
- 不要加阴影

---

## 2. Hero (`Hero.tsx`)

### 2.1 容器

```
<section className="relative z-10 bg-hero-purple-soft">
  <div className="max-w-6xl mx-auto px-6 py-20 md:py-28 text-center">
    [旋转徽章]
    [H1 标题]
    [副标题]
    [4 个 CTA]
    [4 个统计徽章]
  </div>
</section>
```

### 2.2 旋转徽章(关键)

```tsx
const ROTATING_BADGES = [
  '监控 YouTube 频道分析',
  '分析 Instagram 互动趋势',
  '大规模提取 TikTok 用户数据',
  '通过 API',
];

function RotatingBadge() {
  const [idx, setIdx] = useState(0);
  useEffect(() => {
    const t = setInterval(() => setIdx(i => (i + 1) % ROTATING_BADGES.length), 2500);
    return () => clearInterval(t);
  }, []);
  return (
    <div className="inline-flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1.5">
      <span className="relative w-2 h-2 rounded-full bg-emerald-500" />
      <span key={idx} className="text-xs font-medium animate-text-reveal">
        {ROTATING_BADGES[idx]}
      </span>
    </div>
  );
}
```

### 2.3 H1 标题

```tsx
<h1 className="mt-6 text-4xl md:text-5xl lg:text-6xl font-bold leading-[1.1] tracking-tight">
  社交数据基础设施{' '}
  <span className="relative inline-block">
    {/* 4 角虚线方框 */}
    <span className="absolute -inset-1.5 border-2 border-dashed border-foreground/30 rounded-lg pointer-events-none" aria-hidden />
    为
  </span>{' '}
  <span className="text-gradient-accent">AI 时代而生</span>
</h1>
```

### 2.4 副标题

- `text-lg md:text-xl text-muted-foreground mt-6`
- 文字:"为 AI 应用、代理和研究提供生产级社交媒体数据"

### 2.5 4 个 CTA(横排,mobile 2x2)

```tsx
<div className="mt-10 flex flex-wrap items-center justify-center gap-3">
  <a className="bg-inverse text-surface ...">快速开始 <ArrowRightIcon /></a>
  <a className="bg-surface border ...">数据 API <ArrowRightIcon /></a>
  <a className="bg-surface border ...">数据集 <ArrowRightIcon /></a>
  <a className="bg-surface border ...">
    入驻市场 <span className="ml-1 rounded bg-accent px-1.5 py-0.5 text-[10px] text-white">新</span>
    <ArrowRightIcon />
  </a>
</div>
```

**按钮样式:**
- 主按钮:`inline-flex items-center gap-2 rounded-lg bg-inverse px-4 py-2.5 text-sm font-medium text-surface-foreground transition-transform hover:scale-105`
- 副按钮:`inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-4 py-2.5 text-sm font-medium transition-colors hover:bg-muted`

### 2.6 4 个统计徽章(单行,中间 `·`)

```tsx
<div className="mt-10 flex flex-wrap items-center justify-center gap-x-3 gap-y-2 text-xs text-muted-foreground">
  <span>16 个平台</span>
  <span aria-hidden>·</span>
  <span>1,000+ 接口</span>
  <span aria-hidden>·</span>
  <span>10亿+ 数据集记录</span>
  <span aria-hidden>·</span>
  <span>低至 $0.001/次请求</span>
</div>
```

---

## 3. StatsStrip (`StatsStrip.tsx`)

### 3.1 容器

```
<div className="border-y">
  <div className="max-w-7xl mx-auto px-6 py-12 grid lg:grid-cols-5 gap-6 items-center">
    <div className="lg:col-span-1">
      <p className="text-sm text-muted-foreground">深受全球数据团队与 AI 开发者信赖</p>
    </div>
    <div className="lg:col-span-3 grid grid-cols-2 md:grid-cols-4 gap-6">
      [4 个统计]
    </div>
    <div className="lg:col-span-1 flex items-center gap-2">
      [GDPR] [CCPA]
    </div>
  </div>
</div>
```

### 3.2 4 个统计

```tsx
function StatItem({ value, label }: { value: string; label: string }) {
  return (
    <div className="border-l border-border pl-4 first:border-l-0">
      <div className="text-2xl md:text-3xl font-bold tracking-tight">{value}</div>
      <div className="text-xs text-muted-foreground mt-1">{label}</div>
    </div>
  );
}

const STATS = [
  { value: '30,000+', label: '开发者与数据团队' },
  { value: '3亿+', label: '每日请求处理量' },
  { value: '16', label: '覆盖社媒平台' },
  { value: '10亿+', label: '数据集记录交付量' },
];
```

### 3.3 GDPR/CCPA 徽章

- 圆形,48-56px,`bg-accent/10 text-accent`
- 内容 `<ShieldIcon className="w-5 h-5" />` + 文字 "GDPR" / "CCPA"
- 横向两个:`<div className="flex items-center gap-2 rounded-full bg-accent/10 px-3 py-1.5"><ShieldIcon />GDPR</div>`

---

## 4. CtaSection (`CtaSection.tsx`)

### 4.1 容器

```
<section>
  <div className="max-w-3xl mx-auto px-6 py-20 md:py-28 text-center">
    <h2 className="text-3xl md:text-4xl font-bold tracking-tight">社媒数据不会自己变成结构化。</h2>
    <p className="text-muted-foreground mt-4">注册即送 $0.05 免费额度——足以测试任意端点。无需信用卡。</p>
    <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
      <a className="bg-inverse text-surface ...">免费开始 <ArrowRightIcon /></a>
      <a className="bg-surface border ...">预约通话 <ArrowRightIcon /></a>
    </div>
  </div>
</section>
```

---

## 5. ThemeToggle (`ThemeToggle.tsx`)

### 5.1 实现

```tsx
'use client';
import { useEffect, useState } from 'react';
import { MoonIcon, SunIcon } from '../shared/icons';

export function ThemeToggle() {
  const [dark, setDark] = useState(false);
  useEffect(() => {
    const isDark = document.documentElement.classList.contains('dark');
    setDark(isDark);
  }, []);
  function toggle() {
    const next = !dark;
    setDark(next);
    document.documentElement.classList.toggle('dark', next);
    try { localStorage.setItem('tikhub-theme', next ? 'dark' : 'light'); } catch {}
  }
  return (
    <button
      type="button"
      onClick={toggle}
      className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-border bg-surface transition-colors hover:bg-muted"
      aria-label="切换暗黑模式"
    >
      {dark ? <SunIcon className="h-4 w-4" /> : <MoonIcon className="h-4 w-4" />}
    </button>
  );
}
```

### 5.2 注意
- 组件名导出,不是默认导出
- 必须在 layout 之后 hydrate — 这是 client component,放在 `'use client'` 指令下
- localStorage 可用 try/catch 包裹(隐私模式下可能抛错)

---

## 6. 共享小图标复用

`Header.tsx` 和 `Hero.tsx` 都用 `ArrowRightIcon` 等 — 都从 `../shared/icons` 导入:

```tsx
import { ArrowRightIcon, ChevronDownIcon, SearchIcon } from '../shared/icons';
```

---

## 7. 验证

完成后:
1. `npm run typecheck` 必须通过
2. `npm run build` 必须通过
3. 提交到 worktree 分支
4. 报告:文件清单 + 行数 + 任何超出此规范的内容

**不要:**
- 不要实现 Header 的子菜单精确内容(用占位即可)
- 不要添加 Hero 背景以外的视觉元素
- 不要实现 Header 的"搜索命令面板"功能
- 不要触碰 `globals.css`(已就位)
- 不要触碰 `shared/*`(已就位)
- 不要创建 `page.tsx`(下个批次)
