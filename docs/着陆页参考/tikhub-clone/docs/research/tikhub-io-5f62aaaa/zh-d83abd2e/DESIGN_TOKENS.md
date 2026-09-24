# 设计 Tokens — https://tikhub.io/zh

**编写日期:** 2026-08-30
**目标:** 提取的色彩 / 字体 / 间距 / 圆角 / 阴影

---

## 1. 颜色 Tokens

### 1.1 浅色模式 (`:root`)

| Token | 值 | 用途 |
|---|---|---|
| `--background` | `oklch(0.99 0 0)` ≈ `#FAFAF9` | 页面背景 |
| `--foreground` | `oklch(0.20 0 0)` ≈ `#1A1A1A` | 主文字 |
| `--card` | `oklch(1 0 0)` = `#FFFFFF` | 卡片背景 |
| `--card-foreground` | `oklch(0.20 0 0)` ≈ `#1A1A1A` | 卡片文字 |
| `--popover` | `oklch(1 0 0)` = `#FFFFFF` | 弹层背景 |
| `--popover-foreground` | `oklch(0.20 0 0)` ≈ `#1A1A1A` | 弹层文字 |
| `--primary` | `oklch(0.20 0 0)` ≈ `#1A1A1A` | 主按钮背景 |
| `--primary-foreground` | `oklch(0.99 0 0)` ≈ `#FAFAF9` | 主按钮文字 |
| `--secondary` | `oklch(0.97 0 0)` ≈ `#F5F5F4` | 次按钮背景 |
| `--secondary-foreground` | `oklch(0.20 0 0)` ≈ `#1A1A1A` | 次按钮文字 |
| `--muted` | `oklch(0.97 0 0)` ≈ `#F5F5F4` | 静默背景 |
| `--muted-foreground` | `oklch(0.50 0 0)` ≈ `#6B6B6B` | 静默文字 |
| `--accent` | `oklch(0.55 0.20 285)` ≈ `#6366F1` | **主紫调强调色** |
| `--accent-foreground` | `oklch(0.99 0 0)` ≈ `#FFFFFF` | 强调色上的文字 |
| `--destructive` | `oklch(0.577 0.245 27.325)` ≈ `#EF4444` | 危险色 |
| `--border` | `oklch(0.92 0 0)` ≈ `#E5E5E5` | 边框 |
| `--input` | `oklch(0.92 0 0)` ≈ `#E5E5E5` | 输入框边框 |
| `--ring` | `oklch(0.70 0 0)` ≈ `#B3B3B3` | 聚焦环 |
| `--radius` | `0.625rem` (10px) | 基础圆角 |

### 1.2 暗色模式 (`.dark`)

| Token | 值 | 用途 |
|---|---|---|
| `--background` | `oklch(0.14 0 0)` ≈ `#0F0F0F` | 页面背景 |
| `--foreground` | `oklch(0.98 0 0)` ≈ `#FAFAFA` | 主文字 |
| `--card` | `oklch(0.20 0 0)` ≈ `#1A1A1A` | 卡片背景 |
| `--card-foreground` | `oklch(0.98 0 0)` ≈ `#FAFAFA` | 卡片文字 |
| `--primary` | `oklch(0.92 0 0)` ≈ `#EAEAEA` | 主按钮背景(暗模式) |
| `--primary-foreground` | `oklch(0.20 0 0)` ≈ `#1A1A1A` | 主按钮文字 |
| `--muted` | `oklch(0.27 0 0)` ≈ `#2A2A2A` | 静默背景 |
| `--muted-foreground` | `oklch(0.71 0 0)` ≈ `#A8A8A8` | 静默文字 |
| `--accent` | `oklch(0.55 0.20 285)` ≈ `#6366F1` | 强调色(不变) |
| `--border` | `oklch(1 0 0 / 10%)` ≈ `rgba(255,255,255,0.1)` | 边框(暗色) |

### 1.3 TikHub 专用 Token(在 globals.css 追加)

| Token | 值 | 用途 |
|---|---|---|
| `--surface` | `oklch(1 0 0)` = `#FFFFFF` | "白面"层级 |
| `--surface-foreground` | `oklch(0.20 0 0)` ≈ `#1A1A1A` | 表面文字 |
| `--inverse` | `oklch(0.20 0 0)` ≈ `#1A1A1A` | 反色(深色背景) |
| `--inverse-foreground` | `oklch(0.99 0 0)` ≈ `#FAFAF9` | 反色上的文字 |
| `--muted-soft` | `oklch(0.97 0 0)` ≈ `#F5F5F4` | 柔和静默 |

**Tailwind 暴露:**
```
bg-surface, text-surface-foreground,
bg-inverse, text-inverse-foreground,
bg-muted-soft
```

### 1.4 强调色(紫/靛)

| 名字 | OKLab | 用途 |
|---|---|---|
| accent 主 | `oklab(0.585387 0.0253055 -0.202469)` ≈ `#6366F1` | 主紫 |
| accent 浅 | `oklch(0.55 0.20 285 / 0.1)` | 紫底背景 |
| accent 描边 | `oklch(0.55 0.20 285 / 0.15)` | 紫边框 |
| accent hover 描边 | `oklch(0.55 0.20 285 / 0.3)` | 紫边框 hover |
| accent shadow | `rgba(99,102,241,0.08)` | 卡片阴影 |
| accent shadow hover | `rgba(99,102,241,0.18)` | 卡片阴影 hover |
| accent gradient 起点 | `#6366F1` | 标题渐变 |
| accent gradient 终点 | `#8B5CF6` | 标题渐变 |

### 1.5 代码高亮色 (dark editor `#1E1E2E`)

| 名字 | OKLab / Hex | 用途 |
|---|---|---|
| 红色 | `#F38BA8` | 关键字 |
| 紫色 | `#CBA6F7` | 类型/修饰符 |
| 灰色 | `#9399B2` | 注释/分隔符 |
| 绿色 | `#A6E3A1` | 字符串/值 |
| 黄色 | `#F9E2AF` | 函数/方法 |
| 蓝色 | `#89B4FA` | 变量 |
| 品红 | `#F5C2E7` | 数字 |
| 暗灰 | `#6C7086` | 标点 |
| 浅灰 | `#BAC2DE` | 普通文字 |

### 1.6 渐变背景

```css
--gradient-purple-soft: linear-gradient(135deg, 
  oklab(0.585387 0.0253055 -0.202469 / 0.05) 0%, 
  transparent 50%);
--gradient-title: linear-gradient(135deg, 
  #6366F1 0%, 
  #8B5CF6 50%, 
  #EC4899 100%);
--gradient-edge-fade-left: linear-gradient(to right, 
  var(--background) 0%, 
  transparent 100%);
--gradient-edge-fade-right: linear-gradient(to left, 
  var(--background) 0%, 
  transparent 100%);
```

---

## 2. 字体 Tokens

### 2.1 字体家族

```css
--font-sans: var(--font-geist-sans), "Noto Sans SC", "PingFang SC", 
            -apple-system, BlinkMacSystemFont, "Segoe UI", 
            "Microsoft YaHei", sans-serif;
--font-mono: var(--font-geist-mono), "JetBrains Mono", "Fira Code", 
             ui-monospace, "Cascadia Code", Menlo, monospace;
```

### 2.2 字号 / 行高

| 元素 | 字号 | 行高 | 字重 | letter-spacing |
|---|---|---|---|---|
| h1 (Hero) | `clamp(2.5rem, 5vw, 4.5rem)` ≈ 40-72px | 1.1 | 700 | -0.02em |
| h2 (section) | `clamp(2rem, 4vw, 3rem)` ≈ 32-48px | 1.2 | 700 | -0.02em |
| h3 (sub) | `clamp(1.5rem, 3vw, 2rem)` ≈ 24-32px | 1.3 | 600 | -0.01em |
| h4 (mini) | `1.25rem` (20px) | 1.4 | 600 | normal |
| p (lead) | `1.125rem` (18px) | 1.6 | 400 | normal |
| p (body) | `1rem` (16px) | 1.6 | 400 | normal |
| p (small) | `0.875rem` (14px) | 1.5 | 400 | normal |
| caption | `0.75rem` (12px) | 1.4 | 500 | 0.01em |
| label | `0.6875rem` (11px) | 1.3 | 600 | 0.05em uppercase |

### 2.3 字体加载

**Next.js `next/font/google`:**
- Geist Sans (default) → `--font-geist-sans`
- Geist Mono → `--font-geist-mono`
- Noto Sans SC → **必须** (中文,CJK fallback 链)

**添加到 `src/app/layout.tsx`:**
```tsx
import { Geist, Geist_Mono, Noto_Sans_SC } from "next/font/google";

const notoSansSC = Noto_Sans_SC({
  weight: ["400", "500", "600", "700"],
  subsets: ["latin"],
  variable: "--font-noto-sans-sc",
});
```

---

## 3. 间距 Tokens

| 名字 | 值 | 用途 |
|---|---|---|
| `space-0` | `0` | — |
| `space-1` | `0.25rem` (4px) | 极小间距 |
| `space-2` | `0.5rem` (8px) | 标签间 |
| `space-3` | `0.75rem` (12px) | 紧密 |
| `space-4` | `1rem` (16px) | 默认 |
| `space-5` | `1.25rem` (20px) | 略松 |
| `space-6` | `1.5rem` (24px) | 卡片内 padding |
| `space-8` | `2rem` (32px) | 区块内 |
| `space-10` | `2.5rem` (40px) | 区块标题到内容 |
| `space-12` | `3rem` (48px) | 子区块间 |
| `space-16` | `4rem` (64px) | section 上下 padding |
| `space-20` | `5rem` (80px) | 顶部偏移 (`scroll-mt-20`) |
| `space-24` | `6rem` (96px) | 大区块 |

**section 默认:**
```css
section.scroll-mt-20 { 
  padding-top: clamp(3rem, 6vw, 5rem); 
  padding-bottom: clamp(3rem, 6vw, 5rem);
}
```

**container:**
```css
.max-w-7xl { max-width: 80rem; }  /* 1280px */
.max-w-6xl { max-width: 72rem; }  /* 1152px */
.max-w-3xl { max-width: 48rem; }  /* 768px */
```

**水平 padding:**
```css
.px-6 { padding-left: 1.5rem; padding-right: 1.5rem; }
@media (min-width: 768px) { .md\:px-8 { padding-left: 2rem; padding-right: 2rem; } }
```

---

## 4. 圆角 Tokens

| Token | 值 | 用途 |
|---|---|---|
| `--radius-sm` | `0.375rem` (6px) | 小标签 |
| `--radius-md` | `0.5rem` (8px) | 输入框 |
| `--radius-lg` | `0.625rem` (10px) | 按钮 |
| `--radius-xl` | `0.875rem` (14px) | 卡片 |
| `--radius-2xl` | `1.125rem` (18px) | 大卡片 |
| `--radius-3xl` | `1.375rem` (22px) | 巨型卡片 / 区 |
| `--radius-full` | `9999px` | 圆形 / 胶囊 |

**实际观察到的:**
- 按钮: `rounded-lg` (10px)
- 输入框: `rounded-md` (8px)
- 卡片: `rounded-xl` (14px) 或 `rounded-2xl` (16-18px)
- 暗色区(市场/页脚): `rounded-3xl` (22-24px)
- 徽章: `rounded-full`

---

## 5. 阴影 Tokens

```css
--shadow-card: 0 0 12px rgba(99, 102, 241, 0.08);
--shadow-card-hover: 0 0 20px rgba(99, 102, 241, 0.18);
--shadow-code: 0 25px 50px -12px rgba(0, 0, 0, 0.25);
--shadow-modal: 0 10px 40px rgba(0, 0, 0, 0.15);
--shadow-floating: 0 4px 20px rgba(0, 0, 0, 0.1);
```

**实际观察到的:**
- 平台卡片: `shadow-[0_0_12px_rgba(99,102,241,0.08)]`
- 平台卡片 hover: `shadow-[0_0_20px_rgba(99,102,241,0.18)]`
- 代码窗口: 默认无,但滚动后浮在 `bg-card` 上有 25 像素阴影
- 浮动 scroll-to-top: 默认深色圆

---

## 6. 边框 Tokens

| 名字 | 值 | 用途 |
|---|---|---|
| `border-default` | `1px solid var(--border)` | 标准 |
| `border-accent-soft` | `1px solid rgb(99 102 241 / 0.15)` | 紫淡边框 |
| `border-accent-medium` | `1px solid rgb(99 102 241 / 0.3)` | 紫中边框 |
| `border-card-soft` | `1px solid rgb(0 0 0 / 0.04)` | 卡片内边框 |

**Tailwind 暴露:**
```
border-accent/15
border-accent/30
border-border/40
```

---

## 7. 透明度 / 模糊

| 名字 | 值 | 用途 |
|---|---|---|
| `bg-surface/50` | `rgba(255,255,255,0.5)` | 平台卡片半透明 |
| `bg-surface/70` | `rgba(255,255,255,0.7)` | 平台卡片 hover |
| `backdrop-blur-sm` | `blur(4px)` | 平台卡片毛玻璃 |
| `bg-card/40` | 浅色 | 平台卡 icon 容器 |

---

## 8. 断点

```css
sm: 640px
md: 768px
lg: 1024px
xl: 1280px
2xl: 1536px
```

**实际响应式行为:**
- `md:` (768px): 文本大小跳,横向 grid 变 2 列
- `lg:` (1024px): 大区块从单列变 2 列,导航展开完整

---

## 9. 关键 Tailwind 工具类汇总

```css
/* 平台卡片 */
.bg-surface\/50
.backdrop-blur-sm
.rounded-xl
.border.border-accent\/15
.shadow-\[0_0_12px_rgba\(99\,102\,241\,0\.08\)\]

/* 平台卡片 hover */
.hover\:shadow-\[0_0_20px_rgba\(99\,102\,241\,0\.18\)\]
.hover\:bg-surface\/70
.hover\:border-accent\/30

/* 图标容器 */
.w-10.h-10
.rounded-lg
.bg-card
.border.border-border\/40

/* 主按钮 (黑底) */
.bg-inverse
.text-surface

/* 紫色徽章 */
.bg-accent\/10
.text-accent
.bg-accent\/20

/* 章节容器 */
.scroll-mt-20
.max-w-7xl
.mx-auto
.px-6

/* 暗色区 */
.bg-inverse
.text-white
.rounded-3xl
```

---

## 10. 关键 CSS 动画

```css
@keyframes scroll-left {
  from { transform: translateX(0); }
  to { transform: translateX(-50%); }
}

@keyframes scroll-right {
  from { transform: translateX(-50%); }
  to { transform: translateX(0); }
}

.animate-scroll-left { animation: scroll-left 40s linear infinite; }
.animate-scroll-right { animation: scroll-right 35s linear infinite; }
.hover\:\[animation-play-state\:paused\]:hover { 
  animation-play-state: paused; 
}

@keyframes text-reveal {
  from { opacity: 0; transform: translateY(8px); }
  to { opacity: 1; transform: translateY(0); }
}

.animate-text-reveal {
  animation: text-reveal 0.4s ease-out;
}

@keyframes card-swap-up {
  0% { transform: translateY(0); opacity: 1; z-index: 30; }
  40% { transform: translateY(-30px); opacity: 0; z-index: 10; }
  60% { transform: translateY(40px); opacity: 0; z-index: 5; }
  100% { transform: translateY(0); opacity: 1; z-index: 20; }
}
```

---

## 11. Z-index 层级

| 层 | z | 用途 |
|---|---|---|
| 1 | 0 | 区块背景层 |
| 2 | 10 | Hero / 平台卡片包装 |
| 3 | 20 | 浮动卡片 |
| 4 | 30 | card-swap 顶部卡 |
| 5 | 40 | 下拉菜单 |
| 6 | 50 | 模态 |
| 7 | 60 | 提示 |
| 8 | 100 | scroll-to-top 按钮 |
