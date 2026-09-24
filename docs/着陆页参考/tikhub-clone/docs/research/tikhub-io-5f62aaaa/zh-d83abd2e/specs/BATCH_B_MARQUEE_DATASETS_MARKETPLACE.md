# Spec: Batch B — Platform Marquee + Datasets + Marketplace

**目标:** 实现页面中间 3 个区块
**复杂度:** 中
**预计代码量:** ~600 行

---

## 文件清单

| 文件 | 操作 | 内容 |
|---|---|---|
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/PlatformMarquee.tsx` | 新建 | 平台 marquee(双行反向) |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Datasets.tsx` | 新建 | 数据集区 |
| `src/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Marketplace.tsx` | 新建 | 市场与合作伙伴 |

**依赖文件(已就位):**
- `../shared/icons.tsx` 提供 PLATFORMS 数组(14 平台)
- `../shared/types.ts` 提供 Platform 类型
- `src/app/globals.css` 提供 `animate-scroll-left/right` 关键帧

**不要修改:** `globals.css`、`shared/*`、`page.tsx`

---

## 1. PlatformMarquee (`PlatformMarquee.tsx`)

### 1.1 整体结构

```
<>
  <SectionHeader title="16 个平台 — 1 个 API" ... />
  <div className="relative overflow-hidden w-[100dvw] max-w-[100vw] -ml-[50vw] left-1/2 space-y-3 mask-fade-edges">
    <MarqueeRow direction="left" />
    <MarqueeRow direction="right" />
  </div>
</>
```

### 1.2 关键样式

- `mask-fade-edges` 是 globals.css 已定义的工具类(左右各 80px 淡出)
- 容器撑满视口宽: `w-[100dvw] max-w-[100vw] -ml-[50vw] left-1/2` —— 这让 marquee "突破" 父容器宽度限制,产生视觉冲击
- 行间距: `space-y-3` (12px)

### 1.3 MarqueeRow 子组件

```tsx
function MarqueeRow({ direction }: { direction: 'left' | 'right' }) {
  // 把 PLATFORMS 数组重复两次(无缝循环)
  const items = [...PLATFORMS, ...PLATFORMS];
  return (
    <div className="flex hover-pause animate-scroll-left">  {/* left */}
      {items.map((p, i) => <PlatformCard key={`${p.id}-${i}`} platform={p} />)}
    </div>
  );
}
```

**实现细节:**
- 两行,各 PLATFORMS × 2 复制 = 28 张卡片
- 第一行 `direction='left'`,动画用 `animate-scroll-left`
- 第二行 `direction='right'`,动画用 `animate-scroll-right`
- 关键帧已定义: `scroll-left` 从 0 到 -50%, `scroll-right` 从 -50% 到 0(双复制接缝)

**为了视觉对称,可以让两行显示不同的平台集合(可选):**
- 行 1: TikTok/Douyin/Instagram/YouTube/X/Rednote/Bilibili
- 行 2: Weibo/Kuaishou/WeChat/Lemon8/Zhihu/Reddit/LinkedIn + 重复
- 但简单做法是直接用完整 PLATFORMS × 2

### 1.4 PlatformCard 子组件

```tsx
function PlatformCard({ platform }: { platform: Platform }) {
  const Icon = platform.icon;
  return (
    <a
      href="#"
      className="group flex-shrink-0 w-[220px] md:w-[250px] lg:w-[280px] px-2 md:px-3 transition-transform hover:-translate-y-1"
    >
      <div className="bg-surface/50 backdrop-blur-sm rounded-xl border border-accent/15 shadow-[0_0_12px_rgba(99,102,241,0.08)] p-5 h-full transition-all group-hover:shadow-[0_0_20px_rgba(99,102,241,0.18)] group-hover:bg-surface/70 group-hover:border-accent/30">
        <div className="flex items-start gap-3">
          <div className="w-10 h-10 rounded-lg bg-card border border-border/40 flex items-center justify-center shrink-0 text-foreground transition-transform group-hover:scale-110">
            <Icon className="w-5 h-5" />
          </div>
          <div className="min-w-0 flex-1">
            <h3 className="text-sm font-semibold truncate">{platform.name}</h3>
            <p className="text-[11px] text-muted-foreground/80 mb-1">{platform.subtitle}</p>
            <p className="text-xs text-muted-foreground line-clamp-2">{platform.description}</p>
          </div>
        </div>
      </div>
    </a>
  );
}
```

### 1.5 SectionHeader(本区块上方标题)

```tsx
function SectionHeader() {
  return (
    <div className="max-w-6xl mx-auto px-6 pt-20 md:pt-28 pb-10 text-center">
      <p className="text-sm text-muted-foreground">社媒数据,触手可及</p>
      <h2 className="mt-2 text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight">
        16 个平台 · 1 个 API
      </h2>
      <p className="mt-3 text-muted-foreground">
        一套 API 密钥、一份文档、一致的响应结构。覆盖所有主流社交平台。
      </p>
    </div>
  );
}
```

(标题文字可以根据设计略调,但要保持"平台数量"和"1 个 API" 的核心信息)

### 1.6 暗色模式适配
- `bg-surface/50` 在暗色模式下是 `oklch(0.18 0 0 / 0.5)` = 透明深色,正常
- `border-accent/15` 在暗色模式下用 `--accent` 调整后的颜色
- 文本 `text-muted-foreground` 暗色模式自动变浅灰
- 不需要写 `.dark` 特殊样式

### 1.7 移动端
- 卡片宽度 `w-[220px]`(已响应)
- 其他正常

---

## 2. Datasets (`Datasets.tsx`)

### 2.1 整体结构

```
<section className="scroll-mt-20">
  <div className="max-w-7xl mx-auto px-6 py-20 md:py-28">
    <div className="text-center mb-12">
      <p className="text-sm text-muted-foreground">数据集</p>
      <h2 className="mt-2 text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight">
        10亿+ 记录
      </h2>
      <p className="mt-3 text-muted-foreground max-w-2xl mx-auto">
        从 14 个平台收集 2,000 万+ 个数据集,可立即下载用于训练、微调或研究。
      </p>
    </div>
    <div className="grid lg:grid-cols-2 gap-12 items-center">
      <DatasetImagesStack />
      <DatasetTableAndLinks />
    </div>
  </div>
</section>
```

### 2.2 DatasetImagesStack — 3 张堆叠

**关键效果:** 3 张数据集图轻微旋转,逐层偏移,创造"层叠卡片"视觉。

```tsx
function DatasetImagesStack() {
  const images = [
    { src: '/sites/.../tiktok-video-dataset-export.webp', alt: 'YouTube 视频数据集', rotate: -2 },
    { src: '/sites/.../social-profile-dataset-fields.webp', alt: 'Instagram Post dataset', rotate: 1.5 },
    { src: '/sites/.../social-engagement-dataset.webp', alt: 'TikTok Product dataset', rotate: -0.5 },
  ];
  return (
    <div className="relative h-80 md:h-96">
      {images.map((img, i) => (
        <div
          key={i}
          className="absolute inset-x-0 rounded-2xl overflow-hidden border border-border bg-card shadow-2xl"
          style={{
            transform: `rotate(${img.rotate}deg) translateY(${i * 16}px)`,
            zIndex: images.length - i,
          }}
        >
          <Image src={img.src} alt={img.alt} width={800} height={400} className="w-full h-auto" />
        </div>
      ))}
    </div>
  );
}
```

**注意:**
- 使用 Next.js `<Image>` 组件
- 图片路径:`/sites/tikhub-io-5f62aaaa/zh-d83abd2e/images/<file>.webp`
- z-index 顶层 (i=0) 最高,所以"最前"的图是 tiktok-video
- 旋转角度: -2 / 1.5 / -0.5(轻微)

### 2.3 DatasetTableAndLinks — 定价表

```tsx
function DatasetTableAndLinks() {
  const tiers = [
    { records: '100K–500K', startsAt: '$150', perThousand: '$2.00' },
    { records: '500K–5M',   startsAt: '$600', perThousand: '$1.20' },
    { records: '5M–20M',    startsAt: '$3,000', perThousand: '$0.60' },
    { records: '20M+',      startsAt: '$8,000', perThousand: '$0.40' },
  ];
  return (
    <div>
      <h3 className="text-2xl font-semibold">浏览我们的数据集</h3>
      <p className="mt-3 text-muted-foreground">
        所有数据集附带 CSV/Parquet 下载,字段文档化,使用条款清晰,商业用途友好。
      </p>
      <div className="mt-6 rounded-2xl border border-border bg-card overflow-hidden">
        <table className="w-full text-sm">
          <thead className="bg-muted text-muted-foreground">
            <tr>
              <th className="px-4 py-3 text-left font-medium">记录数</th>
              <th className="px-4 py-3 text-left font-medium">起价</th>
              <th className="px-4 py-3 text-left font-medium">每千条</th>
            </tr>
          </thead>
          <tbody>
            {tiers.map((t, i) => (
              <tr key={i} className="border-t border-border">
                <td className="px-4 py-3">{t.records}</td>
                <td className="px-4 py-3 font-medium">{t.startsAt}</td>
                <td className="px-4 py-3 text-muted-foreground">{t.perThousand}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="mt-6 flex flex-wrap gap-3">
        <a href="#" className="inline-flex items-center gap-2 text-sm font-medium text-accent hover:underline">
          浏览全部数据集 <ArrowRightIcon className="w-4 h-4" />
        </a>
        <a href="#" className="inline-flex items-center gap-2 text-sm font-medium text-muted-foreground hover:underline">
          申请定制数据集 <ArrowRightIcon className="w-4 h-4" />
        </a>
      </div>
    </div>
  );
}
```

(完整 4 行定价表,比原站多 1 行,更完整;如要严格 1:1 复制可只保留 2 行)

---

## 3. Marketplace (`Marketplace.tsx`)

### 3.1 整体结构

```
<section className="scroll-mt-20">
  <div className="max-w-7xl mx-auto px-6 py-20 md:py-28">
    <div className="bg-inverse text-inverse-foreground rounded-3xl p-8 md:p-12">
      <div className="grid lg:grid-cols-2 gap-12">
        <LeftContent />
        <RightCommissionCard />
      </div>
    </div>
  </div>
</section>
```

### 3.2 LeftContent

```tsx
function LeftContent() {
  return (
    <div>
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-medium text-accent">市场与合作伙伴</span>
        <span className="inline-flex items-center rounded-full bg-accent/20 px-2.5 py-0.5 text-xs font-medium text-accent">
          2026 年 8 月上线
        </span>
      </div>
      <h2 className="mt-4 text-3xl md:text-4xl lg:text-5xl font-bold tracking-tight">
        与 AI 时代一起 加速前进
      </h2>
      <p className="mt-4 text-white/70">
        把你的产品带给 30,000+ 开发者、3亿+ 每日请求,以及 16 个全球主流社交平台。
      </p>
      <div className="mt-8 grid grid-cols-1 sm:grid-cols-3 gap-4">
        {[
          { title: 'AI 智能体', desc: '为 AI 应用提供开箱即用的工具与数据。' },
          { title: 'API',       desc: '把你的服务通过我们分发到所有集成方。' },
          { title: '网络数据采集', desc: '共享数据集与爬取能力,加速研究。' },
        ].map(c => (
          <div key={c.title} className="rounded-xl bg-white/5 p-4 border border-white/10">
            <h3 className="font-semibold">{c.title}</h3>
            <p className="mt-1 text-sm text-white/60">{c.desc}</p>
          </div>
        ))}
      </div>
      <div className="mt-8 flex flex-wrap gap-3">
        <a href="#" className="inline-flex items-center gap-2 rounded-lg bg-white px-4 py-2.5 text-sm font-medium text-inverse transition-colors hover:bg-white/90">
          提交您的产品 <ArrowRightIcon className="w-4 h-4" />
        </a>
        <a href="#" className="inline-flex items-center gap-2 rounded-lg border border-white/20 bg-white/5 px-4 py-2.5 text-sm font-medium text-white transition-colors hover:bg-white/10">
          合作伙伴计划 <ArrowRightIcon className="w-4 h-4" />
        </a>
      </div>
      <p className="mt-6 text-sm text-white/60">
        现已开放早期提交审核——联系我们,在上线前锁定您的位置。
      </p>
    </div>
  );
}
```

**注意:**
- "上线前" 3 个字**保留**(原站可能是"在线前" 的笔误,但"上线前" 是通顺表达 — 如果你直接看到原站是"在线前" 才用"在线前",spec 假设是"上线前")

### 3.3 RightCommissionCard

```tsx
function RightCommissionCard() {
  return (
    <div className="rounded-2xl bg-white/5 p-8 border border-white/10">
      <h3 className="text-2xl md:text-3xl font-bold">仅收取 10% 佣金</h3>
      <p className="mt-2 text-white/70">对比约 20% 的行业标准佣金。</p>
      <div className="mt-8 space-y-4">
        <div>
          <div className="flex items-baseline justify-between text-sm">
            <span>TikHub</span>
            <span className="font-semibold">10%</span>
          </div>
          <div className="mt-1 h-2 rounded-full bg-white/10 overflow-hidden">
            <div className="h-full w-1/2 rounded-full bg-accent" />
          </div>
        </div>
        <div>
          <div className="flex items-baseline justify-between text-sm">
            <span>行业标准</span>
            <span className="font-semibold text-white/70">约 20%</span>
          </div>
          <div className="mt-1 h-2 rounded-full bg-white/10 overflow-hidden">
            <div className="h-full w-full rounded-full bg-white/40" />
          </div>
        </div>
      </div>
      <p className="mt-8 text-sm text-white/60">
        透明的佣金结构,每月 1 号结算。无隐藏费用,无强制促销。
      </p>
    </div>
  );
}
```

### 3.4 暗色模式
- `bg-inverse` 区块在两种模式下都是深色(在浅色模式是 dark,在深色模式是 darker)
- 内部白文字 `text-inverse-foreground`
- 不要写 `.dark` 特殊样式

---

## 验证(必做,顺序)

1. `cd D:\tikhub-batch-b && npm run typecheck` 必须通过
2. `cd D:\tikhub-batch-b && npm run build` 必须通过
3. 提交到 worktree 分支
4. 报告文件清单 + 行数 + 验证结果

**不要:**
- 不要触碰 `globals.css`(`animate-scroll-left/right` 已就位)
- 不要触碰 `shared/icons.tsx`(PLATFORMS 数组已就位)
- 不要实现 marquee 之外的任何新区块
- 不要重命名任何已存在文件

## 完成后

git commit 后报告。期望返回简洁报告(< 50 行)。
