# Phase 4 装配蓝图 — `src/app/zh/page.tsx`

**目标:** 装配 10 个区块到 `/zh` 路由
**预计代码量:** ~30 行(几乎是 import + 排序)

---

## 文件清单

| 文件 | 操作 | 内容 |
|---|---|---|
| `src/app/zh/page.tsx` | 新建 | 装配 10 个区块 |

**不创建:** `src/app/zh/layout.tsx`(不需要 — 根 layout 已覆盖字体和 metadata)

---

## 实现

```tsx
import { Header } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Header';
import { Hero } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Hero';
import { PlatformMarquee } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/PlatformMarquee';
import { StatsStrip } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/StatsStrip';
import { ApiPreview } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/ApiPreview';
import { McpIntegration } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/McpIntegration';
import { Datasets } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Datasets';
import { Marketplace } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Marketplace';
import { UseCases } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/UseCases';
import { CtaSection } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/CtaSection';
import { Footer } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/Footer';
import { ScrollToTop } from '@/components/sites/tikhub-io-5f62aaaa/zh-d83abd2e/components/ScrollToTop';

export default function ZhPage() {
  return (
    <>
      <Header />
      <main>
        <Hero />
        <PlatformMarquee />
        <StatsStrip />
        <ApiPreview />
        <McpIntegration />
        <Datasets />
        <Marketplace />
        <UseCases />
        <CtaSection />
      </main>
      <Footer />
      <ScrollToTop />
    </>
  );
}
```

---

## 装配顺序(从原站 Y 坐标映射)

| Y | 区块 | 组件 |
|---|---|---|
| 0 | Header | `<Header />` |
| 0-894 | Hero | `<Hero />` |
| 894-1140 | StatsStrip | `<PlatformMarquee />` 然后 `<StatsStrip />` |
| 1140-2172 | API 预览 | `<ApiPreview />` |
| 2172-4172 | MCP | `<McpIntegration />` |
| 4173-5144 | Datasets | `<Datasets />` |
| 5144-5972 | Marketplace | `<Marketplace />` |
| 5971-6800 | Use Cases | `<UseCases />` |
| 6800-7162 | CTA | `<CtaSection />` |
| 7162-7774 | Footer | `<Footer />` |
| 浮动 | ScrollToTop | `<ScrollToTop />` |

---

## 验证

1. `npm run typecheck` 通过
2. `npm run build` 通过
3. 用 `chrome-devtools` MCP 访问 `http://localhost:3000/zh`,截图对比
4. 测试 marquee、tab 切换、暗黑模式

---

## 不要

- 不要修改任何组件文件
- 不要添加新组件
- 不要把 Header 改成 client component(需要的话已在 Header.tsx 内部)
