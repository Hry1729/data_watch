# 设计文档:App 暗色模式 + 跟随系统

- 日期:2026-06-02
- 范围:`app/`(Tauri + Vite + React 19 + Tailwind v4 + shadcn)
- 目标:为前端应用增加暗色模式,支持「亮 / 暗 / 跟随系统」三态切换

---

## 1. 背景与现状诊断

暗色模式的 **CSS 基础设施已经齐全**:

- `app/src/index.css` 中已定义 `:root`(亮色)与 `.dark`(暗色)两套语义变量(`--background`、`--foreground`、`--card`、`--border` 等)。
- 已配置 `@custom-variant dark (&:is(.dark *))`。
- `next-themes` 已在依赖中,但**未接线**——当前仅 `src/components/ui/sonner.tsx` 使用了它的 `useTheme()`。

**核心障碍:172 处硬编码十六进制颜色**,分布在 13 个文件,通过内联 `style={{...}}` 或 Tailwind 任意值类(如 `text-[#111827]`)写死,**不会随主题切换**。

| 文件 | 数量 | 文件 | 数量 |
|---|---|---|---|
| SettingsPage.tsx | 64 | CollectorPage.tsx | 13 |
| TrendChart.tsx | 23 | BootstrapPage.tsx | 13 |
| LogPanel.tsx | 11 | WriterPage.tsx | 11 |
| LoginPage.tsx | 9 | ModuleDbLogPanel.tsx | 8 |
| App.tsx | 7 | ModuleSummaryCard.tsx | 7 |
| ConnectionStatusBar.tsx | 4 | AppLayout.tsx | 1 |
| OverviewPage.tsx | 1 | | |

因此「加暗色模式」实质是两件事:**(a) 接线 + 切换控件**(小);**(b) 把 172 处硬编码颜色迁移到语义 token**(主要工作量)。

---

## 2. 已确认的决策

| 决策点 | 结论 |
|---|---|
| 范围 | **完整暗色**:全部 172 处硬编码颜色迁移到语义 token,保证全应用暗色下正确显示 |
| 控件位置 | **AppLayout 顶栏**单个图标按钮(紧挨设置按钮) |
| 控件形态 | **单键三态循环**:`亮 → 暗 → 跟随系统 → 亮…` |
| 跟随系统 | 通过 `matchMedia('(prefers-color-scheme: dark)')` 自动响应 OS 主题 |
| 亮色外观 | **像素级不变**——靠调 `:root` token 值去匹配现有调色盘,避免引入视觉回归 |
| 品牌色策略 | **不动 `--primary`**(shadcn 基础语义,牵动 Button/Tabs/Dialog),新增独立业务 token |
| Theme 实现 | **自写轻量 ThemeProvider,不用 `next-themes`**(Vite/Tauri 环境,几十行即可,依赖更少、可控) |

### 已知取舍
- `/settings` 页时顶栏整体隐藏(`AppLayout.tsx:20`,FR-014),因此**在设置页内够不到切换钮**。短期可接受(用户返回后切换);后续如需要,可在设置页「基础」补主题选项。

---

## 3. Token 层设计(`app/src/index.css`)

**关键认知**:现有 `:root` 是 shadcn 纯灰阶默认值(背景纯白、无品牌蓝、无绿/橙状态色),与实际硬编码调色盘**对不上**。直接迁移会改变亮色外观。

方案:**调 `:root` 的值去匹配当前调色盘 → 亮色保持不变**,再补齐 `.dark` 对应值,并新增缺失的业务语义 token。

### 3.1 颜色 → token 映射表

| 角色 | 当前硬编码 | 目标 token | 备注 |
|---|---|---|---|
| 页面底色 | `#F7F9FC` | `--background` | 调 `:root` 值匹配 |
| 卡片/浮层 | `#FFFFFF` | `--card` / `--popover` | |
| 边框/输入框 | `#DDE3ED` | `--border` / `--input` | |
| 主文字 | `#111827` | `--foreground` | |
| 次文字 | `#6B7280` / `#475569` / `#374151` | `--muted-foreground` | 较深的几档统一到 foreground/muted-foreground |
| 占位/最弱 | `#9CA3AF` | `text-muted-foreground/70` | 用不透明度修饰 |
| **品牌蓝(动作)** | `#1677FF` / `#0F67D8` | **新增 `--brand` / `--brand-foreground`** | 不动 `--primary` |
| **成功** | `#16A05D` + 软底 `#EAF8EF` | **新增 `--success` / `--success-foreground` / `--success-soft`** | |
| **警告** | `#D97706` / `#B45309` + 软底 `#FFF8E1` / `#FFFBEB` | **新增 `--warning` / `--warning-foreground` / `--warning-soft`** | |
| 错误 | `#E5483F` + 软底 `#FEEEEE` / `#FEF2F2` | 复用 `--destructive` + **新增 `--destructive-soft`** | |

> 状态色的多档深浅(如 `#92400E` 文字 / `#FFFBEB` 软底 / `#B91C1C` / `#FCA5A5` 边框)归并到上面各自的 `*-soft`(软背景)+ `*-foreground`(其上文字)成对 token。

### 3.2 新增 token 清单

在 `@theme inline` 中注册对应 `--color-*` 别名;在 `:root` 与 `.dark` 中各给一套值:

```
--brand / --brand-foreground
--success / --success-foreground / --success-soft
--warning / --warning-foreground / --warning-soft
--destructive-soft        (--destructive 已存在)
```

亮色取现有硬编码值;暗色取对应的降饱和/提亮版本(具体值在实现阶段定,遵循暗色下软背景压暗、前景提亮的原则)。

---

## 4. 自写 ThemeProvider(替代 next-themes)

新建 `app/src/contexts/ThemeContext.tsx`(与现有 `AuthContext.tsx` 同目录,遵循既有结构)。

### 4.1 接口

```tsx
type Theme = 'light' | 'dark' | 'system'
type ResolvedTheme = 'light' | 'dark'

// Context 暴露
{
  theme: Theme            // 用户选择
  resolvedTheme: ResolvedTheme  // system 解析后的实际明暗
  setTheme: (t: Theme) => void
}

// 使用
<ThemeProvider defaultTheme="system" storageKey="data-watch-theme">
  <App />
</ThemeProvider>

const { theme, resolvedTheme, setTheme } = useTheme()
```

### 4.2 内部逻辑

1. 初值:读 `localStorage[storageKey]`,缺省回退 `defaultTheme`(`system`)。
2. effect:根据 `theme` 计算 `resolvedTheme`(`system` 时取 `matchMedia('(prefers-color-scheme: dark)').matches`),并在 `<html>` 上 `classList.toggle('dark', resolvedTheme === 'dark')`。
3. 仅当 `theme === 'system'` 时,监听 `matchMedia` 的 `change` 事件,OS 主题变化时实时更新。
4. `setTheme` 写回 `localStorage`。

### 4.3 防白闪脚本(`app/index.html`)

自写方案需手动补 FOUC 防护:在 `index.html` 的 `<head>` 加一小段内联 `<script>`,在 React 挂载前读 `localStorage` + `matchMedia`,提前给 `<html>` 挂 `.dark`。否则深色用户每次启动会闪一下白屏。

---

## 5. 切换控件(`app/src/components/ThemeToggle.tsx`)

- 用 `useTheme()`;点击在 `light → dark → system → light` 间循环 `setTheme`。
- 图标(`lucide-react` 已在用):`Sun`(light)/ `Moon`(dark)/ `Monitor`(system)。
- 用 `mounted` 守卫避免首帧渲染不一致。
- 挂载在 `AppLayout.tsx` 右侧控件区(`AppLayout.tsx:44-55`),紧挨设置按钮。

---

## 6. sonner 连带改造

`src/components/ui/sonner.tsx` 当前从 `next-themes` 导入 `useTheme`(第 1、7 行)。

改造:第 1 行 import 改为从 `@/contexts/ThemeContext` 导入自写的 `useTheme`。我们的 `useTheme()` 暴露的 `theme`(`'light'|'dark'|'system'`)正好喂给 Sonner 的 `theme` prop,行为与现状一致。其余 `var(--popover)` 等 token 引用自动跟随主题,无需改动。

---

## 7. 颜色迁移(172 处 / 13 文件)

机械替换,两种形态:
- Tailwind 任意值:`text-[#111827]` → 语义类 `text-foreground`、`border-[#DDE3ED]` → `border-border` 等。
- 内联 `style={{ background:"#FFFFFF" }}` → 优先换成 `className="bg-card"`;必须保留内联(动态计算)处用 `var(--card)`。

### TrendChart 特殊处理
recharts 的颜色是 JS 常量 / 内联 SVG 属性喂入,**CSS `var()` 在 SVG 呈现属性中不解析**(Chromium/WebView2)。方案:新建 `useChartColors()`,读 `resolvedTheme` 返回 `{ line, grid, axis, tooltipBg, tooltipBorder, text, muted }` 的亮/暗两套色对象,喂给图表组件。这是 23 处颜色中唯一不能纯靠 CSS 的部分。

---

## 8. 执行顺序(分波,每波独立验证)

> 强约束:**严禁一次性全改完**。每波改完执行 `tsc` + `vite build` + 目视检查,通过后再进入下一波。

1. **Token 层**:`index.css` 补全/调值(亮色保持不变 + 暗色 + 新增 token)。
2. **接线 + 控件**:`ThemeContext.tsx` + `main.tsx` 包裹 + `index.html` 防闪脚本 + `ThemeToggle.tsx` + `AppLayout` 挂载 + `sonner.tsx` 重定向。
3. **外壳**:App.tsx / ConnectionStatusBar.tsx。
4. **主页面**:OverviewPage / CollectorPage / WriterPage。
5. **日志卡**:LogPanel / ModuleDbLogPanel / ModuleSummaryCard。
6. **入口页**:LoginPage / BootstrapPage。
7. **设置页**:SettingsPage(最大,64 处)。
8. **图表**:TrendChart + `useChartColors`。
9. **依赖清理 + 终验**:`pnpm remove next-themes`;`pnpm build` 通过;`pnpm dev` 三态手测;切 OS 主题验证跟随;grep 确认无残留硬编码色(图表 hook 常量、阴影 rgba 除外)。

---

## 9. 验收标准

- [ ] 亮色模式与改动前**像素级一致**(无视觉回归)。
- [ ] 顶栏单键可循环 `亮 → 暗 → 跟随系统`,图标随状态变化。
- [ ] 选择「跟随系统」时,切换 OS 主题,应用实时跟随。
- [ ] 重启应用后保留上次选择(localStorage),且无首屏白闪。
- [ ] 全部页面(Overview / Collector / Writer / Settings / Login / Bootstrap)、日志卡、图表、toast 在暗色下均清晰可读、无白底黑字残留。
- [ ] `pnpm build`(`tsc && vite build`)通过。
- [ ] `next-themes` 已从 `package.json` 移除。
- [ ] grep 全仓 `#[0-9A-Fa-f]{6}`,仅余图表 hook 内的有意常量与阴影 rgba。

---

## 10. 涉及文件清单

**新增**
- `app/src/contexts/ThemeContext.tsx`
- `app/src/components/ThemeToggle.tsx`
- `app/src/hooks/useChartColors.ts`(或置于 TrendChart 内)

**修改**
- `app/index.html`(防闪脚本)
- `app/src/index.css`(token 层)
- `app/src/main.tsx`(ThemeProvider 包裹)
- `app/src/components/AppLayout.tsx`(挂载 ThemeToggle)
- `app/src/components/ui/sonner.tsx`(import 重定向)
- 上述 13 个含硬编码颜色的文件(颜色迁移)
- `app/package.json` / lockfile(移除 next-themes)
