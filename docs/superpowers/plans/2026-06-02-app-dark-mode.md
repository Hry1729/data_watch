# App 暗色模式 + 跟随系统 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 `app/` 前端增加「亮 / 暗 / 跟随系统」三态主题切换,并将全应用 172 处硬编码颜色迁移到语义 token,保证亮色像素级不变、暗色全页面可读。

**Architecture:** 自写轻量 `ThemeProvider`(不用 next-themes),在 `<html>` 上 toggle `.dark` 类;Tailwind v4 已有 `@custom-variant dark`。Token 层先把 `:root` 调成与现有硬编码调色盘一致(亮色不变),再补 `.dark` 与新增业务 token。颜色迁移分波进行,每波过 `tsc` + `vite build` + grep + 目视 + Codex 审查门后才进入下一波。

**Tech Stack:** Tauri 2 + Vite 7 + React 19 + Tailwind v4 + shadcn + lucide-react + recharts + sonner。

**Spec:** `docs/superpowers/plans/../specs/2026-06-02-app-dark-mode-design.md`(同 repo,§3 token 映射表、§8 分波顺序、§9 验收标准为本计划权威依据)。

---

## ⚠️ 关于「测试」的适配说明(必读)

本项目**无单元测试框架**(`package.json` 仅 `build: "tsc && vite build"`,无 vitest/jest)。本任务是视觉颜色迁移,不存在可断言的纯函数行为。因此本计划**不套用 writing-plans 的逐任务红/绿 TDD**,而是把每个任务的「验证步骤」替换为 spec §9 定义的真实验收门:

1. **类型门**:`pnpm --dir app exec tsc --noEmit` 通过(0 error)。
2. **构建门**:`pnpm --dir app build` 通过。
3. **grep 门**:该波涉及文件内 `#[0-9A-Fa-f]{6}` 残留 = 0(允许 spec §8.9 例外)。
4. **目视门**:`pnpm --dir app dev` 下亮/暗逐页扫一遍,无白底黑字残留、无视觉回归。
5. **Codex 审查门**(项目强制工作流):每波落地后,用 Codex MCP 审查本波 diff → 修复 → 自审,通过才进入下一波。贴全上下文、禁整文件覆盖、由 Claude 落盘。

> 命令一律 PowerShell。含中文路径加引号。逐波提交,严禁一次性全改完。

---

## 文件结构

**新增**
| 文件 | 职责 |
|---|---|
| `app/src/contexts/ThemeContext.tsx` | 自写 ThemeProvider + useTheme,管理 `theme`/`resolvedTheme`/`setTheme`,toggle `<html>.dark`,监听 OS 主题 |
| `app/src/components/ThemeToggle.tsx` | 顶栏单键三态循环按钮(Sun/Moon/Monitor) |
| `app/src/hooks/useChartColors.ts` | 读 `resolvedTheme` 返回图表亮/暗两套色对象,喂给 TrendChart |

**修改**
| 文件 | 改动 |
|---|---|
| `app/src/index.css` | Token 层:调 `:root` 值匹配现调色盘 + 补 `.dark` + 新增 brand/success/warning/destructive-soft |
| `app/index.html` | `<head>` 加防白闪内联脚本 |
| `app/src/main.tsx` | `<ThemeProvider>` 包裹 `<App/>` |
| `app/src/components/AppLayout.tsx` | 挂载 `<ThemeToggle/>` + 迁移自身硬编码色(nav 的 `#1677FF`/`bg-white`) |
| `app/src/components/ui/sonner.tsx` | `useTheme` import 从 next-themes 改为 `@/contexts/ThemeContext` |
| 13 个含硬编码色文件 | 颜色迁移(见各波) |
| `app/package.json` + lockfile | 移除 next-themes |

---

## Task 1: Token 层(`index.css`)

**Files:**
- Modify: `app/src/index.css`(`@theme inline` 注册别名 + `:root` 调值 + `.dark` 补值)

**原则:** `:root` 现为 shadcn 纯灰阶(`--background: oklch(1 0 0)` 纯白等),与实际硬编码调色盘对不上。把 `:root` 调成现调色盘的值 → 亮色像素级不变;`.dark` 给暗色对应值;新增 brand/success/warning 业务 token。**不动 `--primary`**。

- [ ] **Step 1: 在 `@theme inline` 注册新别名**

在 `index.css` 的 `@theme inline { … }` 末尾(`--radius-*` 之前任意处)追加:

```css
    --color-brand: var(--brand);
    --color-brand-foreground: var(--brand-foreground);
    --color-success: var(--success);
    --color-success-foreground: var(--success-foreground);
    --color-success-soft: var(--success-soft);
    --color-warning: var(--warning);
    --color-warning-foreground: var(--warning-foreground);
    --color-warning-soft: var(--warning-soft);
    --color-destructive-soft: var(--destructive-soft);
```

- [ ] **Step 2: 调 `:root` 值匹配现调色盘 + 新增亮色 token**

把 `:root` 中下列键改为现硬编码调色盘对应值(其余键如 `--primary`/`--secondary`/chart 等**保持不动**):

```css
    --background: #F7F9FC;
    --foreground: #111827;
    --card: #FFFFFF;
    --card-foreground: #111827;
    --popover: #FFFFFF;
    --popover-foreground: #111827;
    --muted-foreground: #6B7280;
    --border: #DDE3ED;
    --input: #DDE3ED;
    --destructive: #E5483F;
    /* 新增业务 token(亮色取现硬编码值) */
    --brand: #1677FF;
    --brand-foreground: #FFFFFF;
    --success: #16A05D;
    --success-foreground: #FFFFFF;
    --success-soft: #EAF8EF;
    --warning: #D97706;
    --warning-foreground: #FFFFFF;
    --warning-soft: #FFF8E1;
    --destructive-soft: #FEEEEE;
```

> `--destructive` 现为 `oklch(0.577 0.245 27.325)`,改为 `#E5483F` 以对齐现错误色;若目视发现 Button destructive 变化过大,回退此一行即可(其余不受影响)。

- [ ] **Step 3: `.dark` 补新增 token 的暗色值**

`.dark` 已有的 shadcn 暗色键保持不动,**追加**新增 token 的暗色值(降饱和软底压暗、前景提亮;以下为建议初值,目视阶段可微调):

```css
    --brand: #4793FF;
    --brand-foreground: #0B1220;
    --success: #34D399;
    --success-foreground: #052E1A;
    --success-soft: #10271C;
    --warning: #FBBF24;
    --warning-foreground: #2A1A05;
    --warning-soft: #2A2008;
    --destructive-soft: #2D1414;
```

- [ ] **Step 4: 验证门**

```powershell
pnpm --dir app exec tsc --noEmit
pnpm --dir app build
```
Expected: 两条均 0 error 通过。此时**亮色应与改动前完全一致**(token 值 = 原硬编码值,只是页面仍在用硬编码,尚未引用 token——本波只建地基)。

- [ ] **Step 5: Codex 审查门**

将 `index.css` 的 diff(贴全上下文)交 Codex MCP 审查:重点核对亮色值是否与硬编码调色盘一一对应、`@theme inline` 别名命名是否与 spec §3.2 一致。审查反馈由 Claude 落盘修复。

- [ ] **Step 6: Commit**

```powershell
git add app/src/index.css
git commit -m "feat(app/theme): token 层调亮色匹配现调色盘并补暗色与业务 token"
```

---

## Task 2: 接线 + 切换控件

**Files:**
- Create: `app/src/contexts/ThemeContext.tsx`
- Create: `app/src/components/ThemeToggle.tsx`
- Modify: `app/index.html`(防白闪脚本)
- Modify: `app/src/main.tsx`(ThemeProvider 包裹)
- Modify: `app/src/components/AppLayout.tsx`(挂载 ThemeToggle)
- Modify: `app/src/components/ui/sonner.tsx`(import 重定向)

- [ ] **Step 1: 创建 `ThemeContext.tsx`**

`app/src/contexts/ThemeContext.tsx`(与 `AuthContext.tsx` 同目录同构):

```tsx
import { createContext, useContext, useEffect, useState, type ReactNode } from "react";

type Theme = "light" | "dark" | "system";
type ResolvedTheme = "light" | "dark";

interface ThemeContextValue {
  theme: Theme;
  resolvedTheme: ResolvedTheme;
  setTheme: (t: Theme) => void;
}

const ThemeContext = createContext<ThemeContextValue | undefined>(undefined);

const MEDIA = "(prefers-color-scheme: dark)";

function systemResolved(): ResolvedTheme {
  return window.matchMedia(MEDIA).matches ? "dark" : "light";
}

export function ThemeProvider({
  children,
  defaultTheme = "system",
  storageKey = "data-watch-theme",
}: {
  children: ReactNode;
  defaultTheme?: Theme;
  storageKey?: string;
}) {
  const [theme, setThemeState] = useState<Theme>(
    () => (localStorage.getItem(storageKey) as Theme) || defaultTheme,
  );
  const [resolvedTheme, setResolvedTheme] = useState<ResolvedTheme>(() =>
    theme === "system" ? systemResolved() : theme,
  );

  useEffect(() => {
    const resolved = theme === "system" ? systemResolved() : theme;
    setResolvedTheme(resolved);
    document.documentElement.classList.toggle("dark", resolved === "dark");
  }, [theme]);

  useEffect(() => {
    if (theme !== "system") return;
    const mql = window.matchMedia(MEDIA);
    const onChange = () => {
      const resolved = systemResolved();
      setResolvedTheme(resolved);
      document.documentElement.classList.toggle("dark", resolved === "dark");
    };
    mql.addEventListener("change", onChange);
    return () => mql.removeEventListener("change", onChange);
  }, [theme]);

  const setTheme = (t: Theme) => {
    localStorage.setItem(storageKey, t);
    setThemeState(t);
  };

  return (
    <ThemeContext.Provider value={{ theme, resolvedTheme, setTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

export function useTheme() {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used within ThemeProvider");
  return ctx;
}
```

- [ ] **Step 2: `index.html` 加防白闪脚本**

在 `app/index.html` 的 `<head>` 内、`</head>` 之前插入(React 挂载前抢先挂 `.dark`):

```html
    <script>
      (function () {
        try {
          var t = localStorage.getItem("data-watch-theme") || "system";
          var dark = t === "dark" || (t === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);
          if (dark) document.documentElement.classList.add("dark");
        } catch (e) {}
      })();
    </script>
```

- [ ] **Step 3: `main.tsx` 包裹 ThemeProvider**

```tsx
import React from "react";
import ReactDOM from "react-dom/client";
import "./index.css";
import App from "./App";
import { ThemeProvider } from "./contexts/ThemeContext";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <ThemeProvider defaultTheme="system" storageKey="data-watch-theme">
      <App />
    </ThemeProvider>
  </React.StrictMode>,
);
```

- [ ] **Step 4: 创建 `ThemeToggle.tsx`**

`app/src/components/ThemeToggle.tsx`:

```tsx
import { useEffect, useState } from "react";
import { Sun, Moon, Monitor } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useTheme } from "@/contexts/ThemeContext";

const NEXT = { light: "dark", dark: "system", system: "light" } as const;
const LABEL = { light: "亮色", dark: "暗色", system: "跟随系统" } as const;

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);
  if (!mounted) return <div className="h-7 w-7" />;

  const Icon = theme === "light" ? Sun : theme === "dark" ? Moon : Monitor;
  return (
    <Button
      variant="ghost"
      size="icon"
      className="h-7 w-7"
      onClick={() => setTheme(NEXT[theme])}
      aria-label={`主题：${LABEL[theme]}（点击切换）`}
      title={`主题：${LABEL[theme]}`}
    >
      <Icon className="h-4 w-4" />
    </Button>
  );
}
```

- [ ] **Step 5: `AppLayout.tsx` 挂载 ThemeToggle**

在 `AppLayout.tsx` 顶部 import:`import { ThemeToggle } from "@/components/ThemeToggle";`
在右侧控件区(`<ConnectionStatusBar />` 与设置 `<Button>` 之间)插入 `<ThemeToggle />`:

```tsx
          <div className="ml-auto flex items-center gap-3">
            <ConnectionStatusBar />
            <ThemeToggle />
            <Button
              variant="ghost"
              size="icon"
              className="h-7 w-7"
              onClick={() => navigate("/settings")}
              aria-label="设置"
            >
              <Settings className="h-4 w-4" />
            </Button>
          </div>
```

> 注:AppLayout nav 的硬编码色(`border-[#1677FF] text-[#1677FF] bg-white`,第 33 行)留待 Task 3 外壳波统一迁移,本波只挂控件。

- [ ] **Step 6: `sonner.tsx` import 重定向**

把第 1 行 `import { useTheme } from "next-themes"` 改为:

```tsx
import { useTheme } from "@/contexts/ThemeContext"
```

其余不动:我们的 `useTheme()` 暴露的 `theme`(`light|dark|system`)正好喂给 Sonner 的 `theme` prop。

- [ ] **Step 7: 验证门**

```powershell
pnpm --dir app exec tsc --noEmit
pnpm --dir app build
pnpm --dir app dev
```
Expected: tsc/build 0 error。dev 下顶栏出现主题钮,单击循环 `亮→暗→跟随系统`,`<html>` class 随之增删 `dark`;暗色下 sonner toast 跟随变暗;重启 dev 保留上次选择且无首屏白闪。(此时多数页面因仍用硬编码色,暗色下仍是白底——正常,后续波迁移。)

- [ ] **Step 8: Codex 审查门**

ThemeContext 的 effect 依赖/清理、防闪脚本 key 与 storageKey 一致性、StrictMode 双挂载下 matchMedia 监听是否泄漏——交 Codex 审查后由 Claude 落盘修复。

- [ ] **Step 9: Commit**

```powershell
git add app/src/contexts/ThemeContext.tsx app/src/components/ThemeToggle.tsx app/index.html app/src/main.tsx app/src/components/AppLayout.tsx app/src/components/ui/sonner.tsx
git commit -m "feat(app/theme): 接入自写 ThemeProvider、三态切换钮与防白闪脚本"
```

---

## 颜色迁移映射速查(Task 3–7 共用)

每波对该波文件做机械替换,两种形态:

**A. Tailwind 任意值类 → 语义类**

| 硬编码 | 替换为 |
|---|---|
| `bg-[#F7F9FC]` / `bg-white` / `bg-[#FFFFFF]` | `bg-background`(页底) / `bg-card`(卡片浮层) |
| `text-[#111827]` / `text-[#0F172A]` | `text-foreground` |
| `text-[#6B7280]` / `text-[#475569]` / `text-[#374151]` | `text-muted-foreground` |
| `text-[#9CA3AF]`(占位/最弱) | `text-muted-foreground/70` |
| `border-[#DDE3ED]` | `border-border` |
| `text-[#1677FF]` / `bg-[#1677FF]` / `border-[#1677FF]`(动作蓝) | `text-brand` / `bg-brand` / `border-brand` |
| `text-[#16A05D]` / 软底 `bg-[#EAF8EF]`(成功) | `text-success` / `bg-success-soft` |
| `text-[#D97706]`/`#B45309` / 软底 `bg-[#FFF8E1]`/`#FFFBEB`(警告) | `text-warning` / `bg-warning-soft` |
| `text-[#E5483F]` / 软底 `bg-[#FEEEEE]`/`#FEF2F2`(错误) | `text-destructive` / `bg-destructive-soft` |

**B. 内联 `style={{...}}`**

- 优先改成 `className` 语义类(如 `style={{ background:"#FFFFFF" }}` → 加 `className="bg-card"`,删该 style 键)。
- 必须保留内联(动态计算)处:值改用 `var(--card)` / `var(--foreground)` 等。

**状态色多档归并:** 各类深浅档(如 `#92400E` 文字 / `#FFFBEB` 软底 / `#B91C1C` / `#FCA5A5` 边框)统一归到对应 `*-soft`(软背景)+ `*-foreground` / `text-*`(其上文字)成对 token。

**逐文件定位命令(PowerShell):** 用 Grep 工具按文件搜 `#[0-9A-Fa-f]{6}`,或:
```powershell
Select-String -Path "app\src\<文件>" -Pattern "#[0-9A-Fa-f]{6}" -AllMatches
```

> **TrendChart.tsx 不在机械迁移之列**,单独走 Task 7(`useChartColors`)。

---

## Task 3: 外壳波(App.tsx / ConnectionStatusBar.tsx)

**Files:**
- Modify: `app/src/App.tsx`(7 处)
- Modify: `app/src/components/ConnectionStatusBar.tsx`(4 处)
- Modify: `app/src/components/AppLayout.tsx`(nav 残留:`#1677FF`×2 + `bg-white`,第 33 行)

- [ ] **Step 1: 迁移三文件硬编码色**

按上方映射速查替换。AppLayout 第 33 行:
```tsx
                      ? "border-[#1677FF] text-[#1677FF] bg-white"
```
→
```tsx
                      ? "border-brand text-brand bg-card"
```

- [ ] **Step 2: grep 门**

```powershell
Select-String -Path "app\src\App.tsx","app\src\components\ConnectionStatusBar.tsx","app\src\components\AppLayout.tsx" -Pattern "#[0-9A-Fa-f]{6}" -AllMatches
```
Expected: 无输出(0 残留)。

- [ ] **Step 3: 验证门**

```powershell
pnpm --dir app exec tsc --noEmit
pnpm --dir app build
```
Expected: 0 error。dev 下亮色这三处与改动前一致;暗色下外壳(顶栏/状态栏/导航高亮)正确变暗。

- [ ] **Step 4: Codex 审查门**(本波 diff)→ 修复 → 自审。

- [ ] **Step 5: Commit**

```powershell
git add app/src/App.tsx app/src/components/ConnectionStatusBar.tsx app/src/components/AppLayout.tsx
git commit -m "refactor(app/theme): 外壳颜色迁移到语义 token"
```

---

## Task 4: 主页面波(Overview / Collector / Writer)

**Files:**
- Modify: `app/src/pages/OverviewPage.tsx`(1 处)
- Modify: `app/src/pages/CollectorPage.tsx`(13 处)
- Modify: `app/src/pages/WriterPage.tsx`(11 处)

- [ ] **Step 1: 按映射速查迁移三文件硬编码色。**

- [ ] **Step 2: grep 门**

```powershell
Select-String -Path "app\src\pages\OverviewPage.tsx","app\src\pages\CollectorPage.tsx","app\src\pages\WriterPage.tsx" -Pattern "#[0-9A-Fa-f]{6}" -AllMatches
```
Expected: 无输出。

- [ ] **Step 3: 验证门**

```powershell
pnpm --dir app exec tsc --noEmit
pnpm --dir app build
```
Expected: 0 error。dev 下三页亮色不变、暗色可读。

- [ ] **Step 4: Codex 审查门**(本波 diff)→ 修复 → 自审。

- [ ] **Step 5: Commit**

```powershell
git add app/src/pages/OverviewPage.tsx app/src/pages/CollectorPage.tsx app/src/pages/WriterPage.tsx
git commit -m "refactor(app/theme): 主页面颜色迁移到语义 token"
```

---

## Task 5: 日志卡波(LogPanel / ModuleDbLogPanel / ModuleSummaryCard)

**Files:**
- Modify: `app/src/components/LogPanel.tsx`(11 处)
- Modify: `app/src/components/ModuleDbLogPanel.tsx`(8 处)
- Modify: `app/src/components/ModuleSummaryCard.tsx`(7 处)

> 路径以实际为准——若组件不在 `components/` 根目录,先用 Grep 定位文件再迁移。

- [ ] **Step 1: 按映射速查迁移三文件硬编码色。** 日志/状态徽标的成功绿、警告橙、错误红软底优先归并到 `*-soft` + `*-foreground`。

- [ ] **Step 2: grep 门**

```powershell
Select-String -Path "app\src\components\LogPanel.tsx","app\src\components\ModuleDbLogPanel.tsx","app\src\components\ModuleSummaryCard.tsx" -Pattern "#[0-9A-Fa-f]{6}" -AllMatches
```
Expected: 无输出。

- [ ] **Step 3: 验证门**

```powershell
pnpm --dir app exec tsc --noEmit
pnpm --dir app build
```
Expected: 0 error。dev 下日志徽标/状态色在亮暗下均清晰。

- [ ] **Step 4: Codex 审查门**(本波 diff)→ 修复 → 自审。

- [ ] **Step 5: Commit**

```powershell
git add app/src/components/LogPanel.tsx app/src/components/ModuleDbLogPanel.tsx app/src/components/ModuleSummaryCard.tsx
git commit -m "refactor(app/theme): 日志卡颜色迁移到语义 token"
```

---

## Task 6: 入口页波(LoginPage / BootstrapPage)

**Files:**
- Modify: `app/src/pages/LoginPage.tsx`(9 处)
- Modify: `app/src/pages/BootstrapPage.tsx`(13 处)

- [ ] **Step 1: 按映射速查迁移两文件硬编码色。**

- [ ] **Step 2: grep 门**

```powershell
Select-String -Path "app\src\pages\LoginPage.tsx","app\src\pages\BootstrapPage.tsx" -Pattern "#[0-9A-Fa-f]{6}" -AllMatches
```
Expected: 无输出。

- [ ] **Step 3: 验证门**

```powershell
pnpm --dir app exec tsc --noEmit
pnpm --dir app build
```
Expected: 0 error。dev 下登录页/引导页亮暗均正确(注意:未登录时这两页也要能跟随主题)。

- [ ] **Step 4: Codex 审查门**(本波 diff)→ 修复 → 自审。

- [ ] **Step 5: Commit**

```powershell
git add app/src/pages/LoginPage.tsx app/src/pages/BootstrapPage.tsx
git commit -m "refactor(app/theme): 入口页颜色迁移到语义 token"
```

---

## Task 7: 设置页波(SettingsPage,64 处 + 控件接线)

**Files:**
- Modify: `app/src/pages/SettingsPage.tsx`

设置页 `/settings` 时顶栏整体隐藏(`AppLayout.tsx:20`),够不到顶栏切换钮,因此本波**必须**把页内「外观主题」segmented 控件接入同一 `ThemeContext`,否则出现「顶栏能切、设置页假状态」割裂。

- [ ] **Step 1: 按映射速查迁移 64 处硬编码色。** 含 `BasicTab` 第 401/403/407/411 行的 `#FFFFFF`/`#DDE3ED`/`#111827`/`#6B7280` 等。

- [ ] **Step 2: `BasicTab` 外观主题控件接入 ThemeContext**

文件顶部 import:`import { useTheme } from "@/contexts/ThemeContext";`

`BasicTab`(`SettingsPage.tsx:396`)删除主题本地假状态,改用 context。**`density` 保持不动**:

```tsx
const ZH_TO_THEME = { "浅色": "light", "深色": "dark", "跟随系统": "system" } as const;
const THEME_TO_ZH = { light: "浅色", dark: "深色", system: "跟随系统" } as const;

function BasicTab() {
  const { theme, setTheme } = useTheme();
  const [density, setDensity] = useState("标准");
  // ...
  // 外观主题 SegmentedGroup(原第 408 行):
  <SegmentedGroup
    options={["浅色", "深色", "跟随系统"]}
    value={THEME_TO_ZH[theme]}
    onChange={(zh) => setTheme(ZH_TO_THEME[zh as keyof typeof ZH_TO_THEME])}
  />
```

> 删除原 `const [theme, setTheme] = useState("跟随系统");`(第 397 行)。`density` 的 `useState("标准")` 与 SegmentedGroup 原样保留。

- [ ] **Step 3: grep 门**

```powershell
Select-String -Path "app\src\pages\SettingsPage.tsx" -Pattern "#[0-9A-Fa-f]{6}" -AllMatches
```
Expected: 无输出。

- [ ] **Step 4: 验证门**

```powershell
pnpm --dir app exec tsc --noEmit
pnpm --dir app build
```
Expected: 0 error。dev 下:设置页各标签亮暗均正确;「外观主题」选项随当前主题高亮;在设置页改选项 → 整个应用立即切主题 → 退出设置页顶栏钮状态与之一致(双向联动,无割裂)。

- [ ] **Step 5: Codex 审查门**(本波 diff)→ 修复 → 自审。

- [ ] **Step 6: Commit**

```powershell
git add app/src/pages/SettingsPage.tsx
git commit -m "refactor(app/theme): 设置页颜色迁移 + 外观主题控件接入 ThemeContext"
```

---

## Task 8: 图表波(useChartColors + TrendChart,23 处)

**Files:**
- Create: `app/src/hooks/useChartColors.ts`
- Modify: `app/src/components/TrendChart.tsx`

TrendChart 的 23 处分两类:**(a) 组件外壳**(卡片/文字/按钮/错误·加载态,如第 80/87/119/123/148/220/222 行)走普通映射速查;**(b) recharts SVG 属性 / tooltip / line 颜色**(第 15 行 `BLUE`、174 `CartesianGrid stroke`、181 `tick fill`、186 `cursor stroke`、80-81 tooltip 卡)集中由 `useChartColors()` 提供,不依赖 SVG 对 `var()` 的解析。

- [ ] **Step 1: 创建 `useChartColors.ts`**

`app/src/hooks/useChartColors.ts`:

```ts
import { useTheme } from "@/contexts/ThemeContext";

export interface ChartColors {
  line: string;
  grid: string;
  axis: string;        // 轴刻度文字 fill
  cursor: string;      // hover 竖线
  tooltipBg: string;
  tooltipBorder: string;
  text: string;        // tooltip 主文字
  muted: string;       // tooltip 次文字
}

const LIGHT: ChartColors = {
  line: "#2563EB",
  grid: "#EEF2F7",
  axis: "#9CA3AF",
  cursor: "#CBD5E1",
  tooltipBg: "#FFFFFF",
  tooltipBorder: "#E5E7EB",
  text: "#111827",
  muted: "#6B7280",
};

const DARK: ChartColors = {
  line: "#4793FF",
  grid: "#243044",
  axis: "#6B7686",
  cursor: "#3A465A",
  tooltipBg: "#1C2331",
  tooltipBorder: "#2D3748",
  text: "#E8EDF5",
  muted: "#9AA6B6",
};

export function useChartColors(): ChartColors {
  const { resolvedTheme } = useTheme();
  return resolvedTheme === "dark" ? DARK : LIGHT;
}
```

> 暗色值为建议初值,目视阶段可微调。这些常量是 spec §8.9 grep 的允许例外(图表常量)。

- [ ] **Step 2: TrendChart 外壳 hex 走普通映射** —— 卡片 `style` bg/border → `bg-card`/`border-border`;文字 `text-[#111827]`/`#6B7280`/`#9CA3AF` → `text-foreground`/`text-muted-foreground`/`text-muted-foreground/70`;按钮 active 态 `#F3F6FB`/`#FFFFFF`/`#111827`/`#6B7280` → `bg-accent`/`bg-card` + `text-foreground`/`text-muted-foreground`;占位虚线框 `#F7F9FC` → `bg-background`;错误态 `#FEF2F2`/`#FCA5A5`/`#E5483F` → `bg-destructive-soft`/`border-destructive`/`text-destructive`。

- [ ] **Step 3: TrendChart recharts 色改用 hook** —— 组件内 `const c = useChartColors();`,然后:
  - 第 15 行 `BLUE` 常量删除,line 的 `stroke`/gradient stop 用 `c.line`;
  - `<CartesianGrid stroke={c.grid} />`;
  - 轴 `tick={{ fontSize: 11, fill: c.axis }}`;
  - `cursor={{ stroke: c.cursor, strokeDasharray: "3 3" }}`;
  - 自定义 Tooltip 卡的 `background`/`border`/文字 → `c.tooltipBg`/`c.tooltipBorder`/`c.text`/`c.muted`。

- [ ] **Step 4: grep 门**

```powershell
Select-String -Path "app\src\components\TrendChart.tsx" -Pattern "#[0-9A-Fa-f]{6}" -AllMatches
```
Expected: 无输出(颜色常量已移入 `useChartColors.ts`)。

- [ ] **Step 5: 验证门**

```powershell
pnpm --dir app exec tsc --noEmit
pnpm --dir app build
```
Expected: 0 error。dev 下趋势图亮色与改动前一致;切暗色:折线/网格/轴/tooltip 全部跟随变暗,曲线清晰可读;切 OS 主题(system 模式)图表实时跟随。

- [ ] **Step 6: Codex 审查门**(本波 diff)→ 修复 → 自审。

- [ ] **Step 7: Commit**

```powershell
git add app/src/hooks/useChartColors.ts app/src/components/TrendChart.tsx
git commit -m "refactor(app/theme): 趋势图经 useChartColors 支持暗色"
```

---

## Task 9: 依赖清理 + 全局终验

**Files:**
- Modify: `app/package.json` + `app/pnpm-lock.yaml`

- [ ] **Step 1: 移除 next-themes**

```powershell
pnpm --dir app remove next-themes
```
Expected: `package.json` 不再含 `next-themes`,lockfile 同步更新。

- [ ] **Step 2: 确认无 next-themes 残留引用**

```powershell
Select-String -Path "app\src\**\*.ts","app\src\**\*.tsx" -Pattern "next-themes" -AllMatches
```
Expected: 无输出(`sonner.tsx` 已在 Task 2 重定向)。

- [ ] **Step 3: 全局 grep 终验**(spec §8.9 限定范围 + 例外)

```powershell
Select-String -Path "app\src\**\*.ts","app\src\**\*.tsx","app\src\**\*.css","app\index.html" -Pattern "#[0-9A-Fa-f]{6}" -AllMatches
```
Expected: 仅余 `useChartColors.ts` 内图表常量、阴影 `rgba(...)`、第三方生成样式。其余为 0。

- [ ] **Step 4: localStorage 安全终验**(仅一个白名单 key)

```powershell
Select-String -Path "app\src\**\*.ts","app\src\**\*.tsx" -Pattern "localStorage|sessionStorage" -AllMatches
```
Expected: 仅 `ThemeContext.tsx` 读写 `data-watch-theme`;无 token 等敏感数据持久化。

- [ ] **Step 5: 构建 + 三态手测终验**

```powershell
pnpm --dir app build
pnpm --dir app dev
```
逐项确认 §9 验收:亮色像素级一致;顶栏单键循环 `亮→暗→跟随系统` 图标随变;system 下切 OS 主题实时跟随;重启保留选择且无白闪;全部页面(Overview/Collector/Writer/Settings/Login/Bootstrap)+ 日志卡 + 图表 + toast 暗色清晰无白底黑字;设置页与顶栏联动一致。

- [ ] **Step 6: Codex 审查门**(全量 diff 终审)→ 修复 → 自审。

- [ ] **Step 7: Commit**

```powershell
git add app/package.json app/pnpm-lock.yaml
git commit -m "chore(app/theme): 移除 next-themes 依赖并完成暗色终验"
```

---

## 验收标准对照(spec §9)

| 验收项 | 落实任务 |
|---|---|
| 亮色像素级一致 | Task 1(:root=原值)+ 各迁移波目视门 |
| 顶栏单键循环三态 + 图标随变 | Task 2(ThemeToggle) |
| 跟随系统实时响应 OS | Task 2(matchMedia 监听) |
| 重启保留 + 无白闪 | Task 2(localStorage + index.html 防闪脚本) |
| 全页面/日志/图表/toast 暗色可读 | Task 3–8 |
| `pnpm build` 通过 | 每波验证门 + Task 9 |
| next-themes 已移除 | Task 9 |
| 设置页与顶栏联动一致 | Task 7 |
| 仅 `data-watch-theme` 一个持久化 key | Task 2 + Task 9 安全终验 |
| grep 无残留硬编码色(限范围+例外) | 各波 grep 门 + Task 9 |

---

## Self-Review 记录

- **Spec 覆盖**:§3 token→Task 1;§4 ThemeProvider/§4.3 防闪/§4.4 安全例外→Task 2/9;§5 控件→Task 2;§6 sonner→Task 2;§7 迁移+TrendChart→Task 3–8;§8 分波顺序→Task 1–9 一一对应;§9 验收→对照表。无遗漏。
- **类型一致**:`Theme`/`ResolvedTheme`/`useTheme()` 返回 `{theme,resolvedTheme,setTheme}` 在 Task 2/7/8 一致;`useChartColors(): ChartColors` 键(line/grid/axis/cursor/tooltipBg/tooltipBorder/text/muted)Task 8 内自洽。
- **占位扫描**:暗色 token 与图表暗色值标注为「建议初值,目视可微调」,均为具体值非 TODO。
- **测试适配**:无单测框架,逐波以 tsc+build+grep+目视+Codex 审查替代红/绿 TDD,已在顶部说明。

---

**Plan complete and saved to `docs/superpowers/plans/2026-06-02-app-dark-mode.md`. Two execution options:**

**1. Subagent-Driven(recommended)** —— 每个 Task 派新 subagent 实现,任务间双段审查,迭代快。

**2. Inline Execution** —— 本会话内逐波执行,带检查点。

**注:无论哪种,每波都须按本计划的 Codex 审查门(项目强制工作流)审查后再进入下一波。哥哥选哪种?**
