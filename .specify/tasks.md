# Tasks: Data Watch Console 桌面应用

**Spec**: `.specify/spec.md` | **Plan**: `.specify/plan.md` | **Branch**: `001-data-watch-console`

---

## 架构边界（每任务实现前必查）

| 禁止 | 正确做法 |
|------|---------|
| ❌ Tauri Rust 启动/终止进程 | ✅ `agentClient.startProcess()` / `stopProcess()` / `restartProcess()` |
| ❌ Tauri Rust 读写配置文件 | ✅ `agentClient.getConfig()` / `putConfig()` |
| ❌ Tauri Rust 捕获 stdout/stderr | ✅ `useWebSocketLog` 通过 Agent WS 接收日志 |
| ❌ Redux / Zustand / React Query | ✅ React Context + useState + 自定义 Hook |
| ❌ WS URL 携带 token（query param） | ✅ 连接后第一帧发 `{ "type": "auth", "token": agentToken }` |
| ❌ VITE_USE_MOCK 生产包启用 | ✅ `MODE !== 'production' && VITE_USE_MOCK === 'true'` |

---

## 任务约定

- `[P]` = 可与同 Phase 其他 [P] 任务并行（不同文件，无依赖）
- 每任务格式：**文件 → 动作 → 验收**
- 密码脱敏强制要求：debug 日志中 `password` / `password_enc` 字段替换为 `[FILTERED]`

---

## Phase 0 — Tauri 清理与基础依赖

**目标**: 移除 Tauri greet 示例，建立前端目录骨架，搭建 Rust HTTP 代理。  
**Gate**: Phase 0 全部通过后才进入其他 Phase。T001 与 T011 可并行。

---

### T001 — app 初始化与目录结构 [P]

**文件**:
- 创建 `app/src/types/index.ts`
- 删除 `app/src/App.css`
- 修改 `app/src/App.tsx`（清空示例内容）
- 修改 `app/src/main.tsx`（移除 App.css import）
- 执行 pnpm 安装

**动作**:

1. 安装前端依赖：
   ```powershell
   pnpm --prefix app add react-router-dom lucide-react
   ```

2. 创建 `app/src/types/index.ts`，包含以下全部类型，不多不少：

   ```typescript
   // 认证状态（仅内存，不持久化）
   export interface AuthStore {
     agentToken: string;
     backendToken: string;
     backendRefreshToken: string;
   }

   // 模块运行状态
   export type ModuleStatusValue = 'running' | 'stopped' | 'crashed' | 'unknown';
   export interface ModuleStatus {
     name: 'collector' | 'writer';
     status: ModuleStatusValue;
     pid: number | null;
     uptime_seconds: number;
     memory_mb: number;
     cpu_percent: number;
     started_at: string | null;
     restart_count: number;
   }

   // WS 日志行
   export type LogLevel = 'DEBUG' | 'INFO' | 'WARN' | 'ERROR';
   export interface LogLine {
     timestamp: string;   // ISO 8601
     level: LogLevel;
     source: string;      // 保留字段；单模块页不渲染
     message: string;
   }

   // 连接配置（password 采用 partial update 语义，可选；详见 plan §4.6）
   export interface DbConfig {
     host: string;
     port: number;
     user: string;
     password?: string;
     database?: string;
   }
   export interface ProcessConfig {
     executable: string;
     working_dir: string;
   }
   export interface ConnectionConfig {
     redis: DbConfig;
     mysql: DbConfig;
     tdengine: DbConfig;
     collector: ProcessConfig;
     writer: ProcessConfig;
   }

   // 统一应用错误
   export type AppError =
     | { type: 'agent_unreachable' }
     | { type: 'unauthorized' }
     | { type: 'api_error'; status: number; message: string }
     | { type: 'network_error'; message: string };

   // TrendChart 渲染状态
   export type TrendChartState = 'empty' | 'loading' | 'error' | 'ready';

   // WS 连接状态
   export type WsStatus = 'connecting' | 'connected' | 'disconnected' | 'reconnecting';
   ```

3. 将 `app/src/App.tsx` 替换为最小骨架（暂不写路由，T003 完成）：
   ```tsx
   export default function App() {
     return <div className="p-4">DataWatch Console — 初始化中</div>;
   }
   ```

4. 删除 `app/src/App.css`，从 `main.tsx` 移除 `import './App.css'`。

**验收**:
- [ ] `pnpm --prefix app dev` 启动无报错，页面显示 "DataWatch Console — 初始化中"
- [ ] `pnpm --prefix app exec tsc --noEmit` 通过，types/index.ts 无类型错误
- [ ] `app/src/` 目录存在（或手动创建）：`types/` `contexts/` `hooks/` `pages/` `lib/`

---

### T011 — Tauri agent_request proxy [P]

**文件**:
- 修改 `app/src-tauri/Cargo.toml`
- 创建 `app/src-tauri/src/http_proxy.rs`
- 创建 `app/src-tauri/src/tray.rs`
- 修改 `app/src-tauri/src/lib.rs`

**动作**:

1. `Cargo.toml` 在 `[dependencies]` 追加：
   ```toml
   reqwest = { version = "0.12", features = ["json"] }
   ```

2. 创建 `http_proxy.rs`（目标地址硬编码，不接受 URL 参数；禁止 log Authorization 值）：
   ```rust
   use reqwest::Client;

   #[tauri::command]
   pub async fn agent_request(
       method: String,
       path: String,
       body: Option<String>,
       token: Option<String>,
   ) -> Result<(u16, String), String> {
       let url = format!("http://127.0.0.1:7070{}", path);
       let client = Client::new();

       let mut req = match method.as_str() {
           "GET"    => client.get(&url),
           "POST"   => client.post(&url),
           "PUT"    => client.put(&url),
           "DELETE" => client.delete(&url),
           m        => return Err(format!("unsupported method: {}", m)),
       };

       if let Some(t) = token {
           // 不 log token 值
           req = req.header("Authorization", format!("Bearer {}", t));
       }
       if let Some(b) = body {
           // 不 log body 中的 password 字段
           req = req.header("Content-Type", "application/json").body(b);
       }

       let resp = req.send().await
           .map_err(|e| format!("agent_unreachable: {}", e))?;
       let status = resp.status().as_u16();
       let text   = resp.text().await
           .map_err(|e| format!("read body failed: {}", e))?;

       Ok((status, text))
   }
   ```

3. 创建 `tray.rs`（MVP 空壳，Phase 8 再实现）：
   ```rust
   #[tauri::command]
   pub fn show_window(_window: tauri::Window) {}

   #[tauri::command]
   pub fn hide_to_tray(_window: tauri::Window) {}
   ```

4. 重写 `lib.rs`（移除 greet，注册两个模块的命令）：
   ```rust
   mod http_proxy;
   mod tray;

   #[cfg_attr(mobile, tauri::mobile_entry_point)]
   pub fn run() {
       tauri::Builder::default()
           .plugin(tauri_plugin_opener::init())
           .invoke_handler(tauri::generate_handler![
               http_proxy::agent_request,
               tray::show_window,
               tray::hide_to_tray,
           ])
           .run(tauri::generate_context!())
           .expect("error while running tauri application");
   }
   ```

**验收**:
- [ ] `cargo build` 在 `app/src-tauri/` 无编译错误
- [ ] `pnpm --prefix app tauri dev` 启动无 panic
- [ ] 浏览器 DevTools Console 执行：
  ```javascript
  window.__TAURI__.core.invoke('agent_request', { method: 'GET', path: '/ping' })
  ```
  返回被 reject 的 Promise（含 "agent_unreachable" 字样），而非 JS 异常或 undefined

---

## Phase 1 — App 壳、路由、AuthContext

**前提**: T001 + T011 全部通过。T002 完成后，T003 与 T004 可并行。

---

### T002 — shadcn/ui 与 Tailwind 基础样式

**文件**:
- 确认/修改 `app/src/index.css`
- 确认 `app/vite.config.ts`
- 执行 shadcn 组件安装

**动作**:

1. 确认 `index.css` 包含（若缺失则添加）：
   ```css
   @layer base {
     * { @apply border-border outline-ring/50; }
     html { @apply font-sans; min-height: 100vh; }
     body { @apply bg-background text-foreground; min-height: 100vh; }
     #root { min-height: 100vh; }
   }
   ```

2. 安装后续任务需要的 shadcn 组件（一次性安装，避免后续任务中断）：
   ```powershell
   # toast 已废弃，改用 sonner（实际执行命令）
   cd app
   pnpm dlx shadcn@latest add input label card badge alert-dialog dialog switch skeleton table tabs separator
   pnpm dlx shadcn@latest add sonner
   ```
   > **注**：`toast` 组件已被 shadcn 废弃，所有 Toast 通知改用 `sonner`（`src/components/ui/sonner.tsx`）。

3. 确认 `app/src/components/ui/` 目录有对应 .tsx 文件。

**验收**:
- [x] 在 App.tsx 临时渲染 `<Button>Hello</Button>` 后 `tsc --noEmit` 通过（无样式丢失，视觉验收需 `pnpm dev`）
- [x] 页面背景色为 `#F7F9FC`（tauri.conf.json backgroundColor 已配置）✅
- [x] 恢复 App.tsx 原内容 ✅
- [x] `components/ui/` 已有：button / input / label / card / badge / alert-dialog / dialog / switch / skeleton / table / tabs / separator / sonner（共 13 个）

---

### T003 — AppLayout + MainNavigation [P]

**文件**:
- 创建 `app/src/components/AppLayout.tsx`
- 创建 `app/src/components/ConnectionStatusBar.tsx`
- 修改 `app/src/App.tsx`（引入路由和 Layout）

**动作**:

1. 创建 `ConnectionStatusBar.tsx`：
   ```tsx
   const SERVICES = ['Redis', 'MySQL', 'TDengine'] as const;
   export function ConnectionStatusBar() {
     return (
       <div className="flex items-center gap-2">
         {SERVICES.map(s => (
            <div key={s} className="flex items-center gap-1 text-xs text-muted-foreground">
              <span className="w-2 h-2 rounded-full bg-gray-400" />
              {s}
            </div>
         ))}
       </div>
     );
   }
   ```
   后续修订：当前实现已接入 `useConnectionHealth()`，每 10 秒轮询 `GET /viewer/health`；绿色/蓝色/红色/灰色分别表示已连接/检测中/断开/未知。

2. 创建 `AppLayout.tsx`，使用 React Router `<Outlet />`：
   - **顶部导航栏**（高度 72px，设计文档 §2.1，无左侧导航栏）：三个分段 Tab：`总览 / Collector / Writer`
   - 右侧：`<ConnectionStatusBar />` + `<Settings>` 图标按钮（`navigate('/settings')`）
   - **当 `location.pathname === '/settings'` 时**：整个顶部导航栏不渲染（FR-014）
   - 内容区：`<Outlet />`

3. 重写 `App.tsx`，引入 BrowserRouter + Routes：
   ```tsx
   // 无 AppLayout 的路由（FR-015）
   <Route path="/bootstrap" element={<div>Bootstrap Placeholder</div>} />
   <Route path="/login"     element={<div>Login Placeholder</div>} />
   <Route path="/error"     element={<div>Error Placeholder</div>} />

   // 有 AppLayout 的路由
   <Route element={<AppLayout />}>
     <Route index         element={<div>Overview Placeholder</div>} />
     <Route path="/collector" element={<div>Collector Placeholder</div>} />
     <Route path="/writer"    element={<div>Writer Placeholder</div>} />
     <Route path="/settings"  element={<div>Settings Placeholder</div>} />
   </Route>

   <Route path="*" element={<Navigate to="/login" />} />
   ```

**验收**:
- [x] `tsc --noEmit` 通过，无类型错误
- [ ] 访问 `/` 显示顶部三个导航项 + 顶部连接状态点（绿/蓝/红/灰）— 需 `pnpm dev` 目视确认
- [ ] 访问 `/bootstrap` 无 AppLayout（无左侧导航）— 需目视确认
- [ ] 访问 `/login` 无 AppLayout — 需目视确认
- [ ] 访问 `/settings` 有 AppLayout 外框但左侧导航和连接状态点不渲染 — 需目视确认
- [ ] 点击导航项路由正确切换 — 需目视确认

---

### T004 — AuthContext + 路由守卫 [P]

**文件**:
- 创建 `app/src/contexts/AuthContext.tsx`
- 修改 `app/src/App.tsx`（AuthProvider 包裹 + AuthGuard）

**动作**:

1. 创建 `AuthContext.tsx`：
   ```tsx
   interface AuthContextValue {
     auth: AuthStore | null;   // null = 未登录
     login(store: AuthStore): void;
     logout(): void;
   }
   export const AuthContext = createContext<AuthContextValue>(null!);

   export function AuthProvider({ children }: { children: ReactNode }) {
     const [auth, setAuth] = useState<AuthStore | null>(null);
     const navigate = useNavigate();
     const isLoggingOutRef = useRef(false);  // 防并发 401 重复跳转

     function login(store: AuthStore) {
       setAuth(store);
       isLoggingOutRef.current = false;
     }

     function logout() {
       if (isLoggingOutRef.current) return;
       isLoggingOutRef.current = true;
       setAuth(null);
       navigate('/login', { replace: true });
     }

     return (
       <AuthContext.Provider value={{ auth, login, logout }}>
         {children}
       </AuthContext.Provider>
     );
   }

   export const useAuth = () => useContext(AuthContext);
   ```

2. `AuthGuard` 组件（可内联在 `AppLayout.tsx` 中）：
   - 如 `auth === null` 且路由不在 `['/login', '/bootstrap', '/error']` 白名单 → `<Navigate to="/login" replace />`

3. `App.tsx` 用 `<AuthProvider>` 包裹整个 `<BrowserRouter>` 内的内容（AuthProvider 内部需要 useNavigate，须在 BrowserRouter 内部）。

**验收**:
- [x] `tsc --noEmit` 通过，无类型错误
- [x] 代码中无 localStorage / sessionStorage / cookie / indexedDB（grep 零命中）
- [x] AuthGuard：未登录时渲染 `<Navigate to="/login" replace />`
- [x] PublicOnlyRoute：已登录时渲染 `<Navigate to="/" replace />`
- [x] /bootstrap、/error 路由在 AuthGuard 之外，不受限制
- [x] logout() 含 isLoggingOutRef 幂等保护，重复调用无副作用
- [ ] 直接访问 `/` 时跳转到 `/login` — 需 `pnpm dev` 目视确认
- [ ] `login()` 后访问 `/` 正常显示 — 需目视确认
- [ ] 刷新页面后 auth 为 null — 需目视确认

---

## Phase 2 — Bootstrap + Login 静态 UI

**前提**: Phase 1 全部通过。T005 与 T006 可并行。

---

### T005 — BootstrapPage 静态 UI [P]

**文件**:
- 创建 `app/src/pages/BootstrapPage.tsx`
- 修改 `app/src/App.tsx`（替换 Bootstrap 占位）

**动作**:

1. 创建 `BootstrapPage.tsx`，包含以下 UI 元素（T014 接入前无真实 API 调用）：

   - 页面标题「DataWatch 初始化向导」（无主导航，FR-015）
   - MySQL 配置表单（`<form>`）：
     - 主机（text，默认 "localhost"）
     - 端口（number，默认 3306，范围 1-65535）
     - 用户名（text）
     - 密码（`type="password"`，**不得**提供显示明文的切换开关）
     - 数据库名（text）
   - 「测试连接」按钮：`variant="outline"`；点击 → 按钮进入 loading（禁用）→ 1.5 秒后还原（静态模拟，T014 前）
   - 「测试成功」状态标识：绿色勾 + 文字「MySQL 连接成功」（仅测试通过后显示）
   - 「完成初始化」按钮：`variant="default"`；**仅测试通过后 enabled**（FR-002）；点击 → loading → 1 秒后静态跳转（T014 接入前 navigate('/login')）
   - 所有字段必填，端口为数字（HTML5 validation 即可）

**验收**:
- [x] `tsc --noEmit` 通过，无类型错误
- [x] BootstrapPage.tsx 内无 fetch / invoke / agent / AppLayout / NavLink 引用
- [x] 密码字段 `type="password"`，无明文切换开关
- [x] 「完成初始化」默认 `disabled`（`testPassed = false`）
- [x] 表单默认值：host=127.0.0.1 / port=3306 / user=root / database=device_watch
- [x] 状态区域默认显示「等待测试连接」badge
- [x] 无 AppLayout、无连接状态点、无主导航（FR-015）
- [ ] 点击「测试连接」→ 1.5 秒后显示「MySQL 连接成功」→ 「完成初始化」变 enabled — 需 `pnpm dev` 目视确认
- [ ] 修改字段后测试状态重置为 idle — 需目视确认

---

### T006 — LoginPage 静态 UI [P]

**文件**:
- 创建 `app/src/pages/LoginPage.tsx`
- 修改 `app/src/App.tsx`（替换 Login 占位）

**动作**:

1. 创建 `LoginPage.tsx`（T014 接入前无真实 API 调用）：

   - 居中卡片布局
   - 标题「DataWatch Console」+ 副标题「请登录以继续」
   - 用户名输入框（text，autocomplete="username"）
   - 密码输入框（`type="password"`，**不得**提供明文切换，autocomplete="current-password"）
   - 错误信息区域（`<p className="text-destructive text-sm">`，初始不渲染）
   - 「登录」按钮：点击 → loading（禁用）→ 2 秒后清除（静态，T014 前）
   - 无主导航（FR-015）

**验收**:
- [x] `tsc --noEmit` 通过，无类型错误
- [x] grep 确认：LoginPage.tsx 内无 /auth/login、fetch、invoke 调用
- [x] grep 确认：无 localStorage / sessionStorage
- [x] 密码字段 `type="password"`，无明文切换
- [x] 未输入用户名或密码时登录按钮 `disabled`（`canSubmit = username && password`）
- [x] 无 AppLayout、无主导航、无连接状态点（FR-015）
- [x] 错误提示区域预留（`error` state，默认 null 不渲染）
- [x] Agent 地址提示：`http://127.0.0.1:7070`
- [ ] 页面视觉渲染 — 需 `pnpm dev` 目视确认

---

## Phase 3 — 主布局静态页面

**前提**: Phase 2 全部通过。**T007 / T008 / T009 / T010 可全部并行**。

---

### T007 — OverviewPage 静态 UI [P]

**文件**:
- 创建 `app/src/components/ModuleCard.tsx`
- 创建 `app/src/components/TrendChart.tsx`
- 创建 `app/src/pages/OverviewPage.tsx`
- 修改 `app/src/App.tsx`（替换 Overview 占位）

**动作**:

**ModuleCard.tsx** — 接受 Props（T015 接入真实数据前使用静态 Props）：
```typescript
interface ModuleCardProps {
  name: 'collector' | 'writer';
  status?: ModuleStatus | null;   // null = 加载中
  operating?: boolean;            // 操作进行中（全部按钮 disabled）
  onStart?:   () => void;
  onStop?:    () => void;
  onRestart?: () => void;
  onViewLog?: () => void;
}
```
渲染要素：
- 状态 pill：running=绿色/stopped=灰色/crashed=红色/unknown=灰色
- 字段行：PID、CPU %、内存 MB、运行时长（`formatUptime(seconds)` → `HH:MM:SS`）
- 四个按钮（T015 前 handler 均为 noop，operating=false 时 enabled）：
  - 「启动」：`variant="outline"`（stopped 时高亮）
  - 「停止」：`variant="outline"`（running 时高亮）
  - 「重启」：`variant="outline"`
  - 「日志」：`variant="ghost"`

**TrendChart.tsx** — 支持四态：
```typescript
interface TrendChartProps {
  title: string;
  unit: string;
  state: TrendChartState;
  data?: number[];
}
```
各态渲染：
- `empty`：浅色虚线矩形区域（`border-dashed border-muted`）+ 中央文字「数据接入中」+ 副文字「-- {unit}」
- `loading`：居中旋转 spinner（`animate-spin`）
- `error`：居中红色图标 + 「数据加载失败」
- `ready`：使用 Recharts 渲染折线/面积图；标题右侧显示当前值；右上角支持 1/5/10 分钟窗口切换

**OverviewPage.tsx** — 静态数据，T015 前使用：
```typescript
const mockStatus: ModuleStatus = {
  name: 'collector', status: 'running',
  pid: 12345, uptime_seconds: 7261,
  memory_mb: 48.2, cpu_percent: 2.3,
  started_at: '2026-05-25T08:00:00Z', restart_count: 0,
};
```
布局：两张 `<ModuleCard>` 横排 + 两张 `<TrendChart state="empty">` 横排。

**验收**:
- [x] `tsc --noEmit` 通过，无类型错误
- [x] OverviewPage / ModuleSummaryCard 内无 fetch / invoke / agentClient / viewer/trends 调用
- [x] Collector 卡片：status=running，内存 186MB，CPU 2.4%，运行时长 03:42:18，stream rt:stream:agv:main:*
- [x] Writer 卡片：status=running，内存 142MB，CPU 1.1%，运行时长 03:41:55，tdengine device_data/sd{type}
- [x] 两个 TrendChart 固定 state="empty"，显示"数据接入中"，单位 pts/min / rows/min
- [x] 状态 pill：运行中=绿色（#16A05D / #EAF8EF）
- [x] 运行时长格式化函数 formatUptime()，13338s → "03:42:18"
- [x] 无左侧导航栏（页面在 AppLayout 内，使用顶部导航）
- [ ] 视觉渲染确认 — 需 `pnpm dev` 目视

---

### T008 — CollectorPage 静态 UI [P]

**文件**:
- 创建 `app/src/components/LogPanel.tsx`
- 创建 `app/src/pages/CollectorPage.tsx`
- 修改 `app/src/App.tsx`（替换 Collector 占位）

**动作**:

**LogPanel.tsx** — Props（T016 接入真实 WS 前使用静态数据）：
```typescript
interface LogPanelProps {
  lines: LogLine[];
  wsStatus: WsStatus;
  autoScroll: boolean;
  onAutoScrollChange: (v: boolean) => void;
  ref?: React.Ref<HTMLDivElement>;  // 用于 scrollIntoView
}
```
渲染要素：
- `wsStatus !== 'connected'` 时顶部黄色 Banner：「连接已断开，正在重连…」
- 右上角：shadcn `<Switch>` 「自动滚动」
- 自动滚动：`autoScroll = true` 时，每次 `lines` 变化后 `containerRef.current?.scrollTo(0, 9999)`
- 日志行（每行一个 `<div>`）：
  - 时间戳：`<span className="text-muted-foreground text-xs">{timestamp}</span>`
  - 级别 chip：`<Badge>` 颜色映射：
    ```
    DEBUG → bg-gray-100  text-gray-600
    INFO  → bg-blue-100  text-blue-700
    WARN  → bg-amber-100 text-amber-700
    ERROR → bg-red-100   text-red-700
    ```
  - 消息文本：等宽字体（`font-mono text-sm`）
  - **不渲染 `source` 字段**（FR-009 单模块页约定）

**CollectorPage.tsx** — 四个区块（所有数据静态）：

1. **进程摘要卡**：同 ModuleCard 字段（命令路径、工作目录额外显示）+ 同样四个按钮  
   「打开日志」按钮行为：
   ```typescript
   const logPanelRef = useRef<HTMLDivElement>(null);
   // onClick:
   logPanelRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
   // 短暂高亮：添加 ring-2 ring-primary class，300ms 后移除
   ```

2. **指标卡片行**（四个 Card，静态数值）：
   - 采集周期：`1000 ms`
   - 设备连接数：`12 / 15`
   - 采集点位：`3,840 pts`
   - Redis 写入速率：`256 pts/min`

3. **采集任务表**（shadcn `<Table>`，2 行静态数据）：
   列：设备 / 分组 / 协议 / 周期 / 点位数 / 最新时间 / 质量  
   表格最大高度固定（`max-h-48 overflow-y-auto`）

4. **`<LogPanel>`**（静态 3 条 LogLine，wsStatus="connected"）：
   ```typescript
   const mockLines: LogLine[] = [
     { timestamp: '2026-05-25T10:00:00.001+08:00', level: 'INFO',  source: 'collector', message: 'Collector started' },
     { timestamp: '2026-05-25T10:00:01.234+08:00', level: 'WARN',  source: 'collector', message: 'Device AGV-02 timeout' },
     { timestamp: '2026-05-25T10:00:02.567+08:00', level: 'ERROR', source: 'collector', message: 'Redis connection refused' },
   ];
   ```

**验收**:
- [x] `tsc --noEmit` 通过，无类型错误
- [x] CollectorPage / LogPanel 内无 agentClient / fetch / invoke / WebSocket 调用
- [x] 顶部详情卡：命令 / 工作目录 / PID=4820 / 运行时长 03:42:18 / 内存 186MB / CPU 2.4%
- [x] 四个指标卡：采集周期 100ms / 设备连接 18/18 / 采集点位 3,264 / Redis 写入 12,480 pts/min
- [x] 采集任务表：5 行静态数据，7 列（设备/分组/协议/周期/点位/最新时间/质量）
- [x] 任务表 max-h-48 overflow-y-auto 支持纵向滚动
- [x] TrendChart state="empty" 显示"数据接入中"
- [x] LogPanel：5 条静态日志（INFO/INFO/INFO/WARN/ERROR），无 source 字段渲染
- [x] LogPanel forwardRef 供父组件 scrollIntoView
- [x] "打开日志"按钮调用 scrollIntoView + 0.6s 高亮边框
- [x] 日志级别 chip 颜色：DEBUG灰/INFO蓝/WARN橙/ERROR红
- [ ] 视觉渲染确认 — 需 `pnpm dev` 目视

---

### T009 — WriterPage 静态 UI [P]

**文件**:
- 创建 `app/src/pages/WriterPage.tsx`
- 修改 `app/src/App.tsx`（替换 Writer 占位）

**动作**:

结构与 CollectorPage 完全对称，内容差异：

1. **进程摘要卡**：同结构，name="writer"
2. **指标卡片行**（静态数值）：
   - Stream Lag：`142 条`
   - PEL Pending：`8 条`
   - 批量写入速率：`512 rows/min`
   - DLQ 积压量：`0 条`
3. **消费任务表**（2 行静态数据）：
   列：Stream Key / Lag / PEL / ACK 状态
4. **`<LogPanel>`**（3 条 writer 日志，wsStatus="connected"）

「打开日志」按钮同 CollectorPage 实现（scrollIntoView + 高亮）。

**验收**:
- [x] `/writer` 显示完整静态布局，结构与 CollectorPage 对称
- [x] 指标卡片内容为 Writer 专有字段（Stream Lag / PEL Pending / 批量写入 / DLQ）
- [x] App.tsx 替换 WriterPlaceholder → WriterPage，无 TS 报错
- [x] WriterPage.tsx 无 agentClient/fetch/invoke/WebSocket 调用

---

### T010 — SettingsPage 静态 UI [P]

**文件**:
- 创建 `app/src/pages/SettingsPage.tsx`
- 修改 `app/src/App.tsx`（替换 Settings 占位）

**动作**:

1. **页面骨架**（设置页不使用 AppLayout 的侧边导航，AppLayout 在 `/settings` 路由下已隐藏主导航）：
   - 顶部：`← 返回` 按钮（`navigate(-1)`，默认回总览）+ 标题「设置」
   - Esc 关闭：`useEffect(() => { const fn = (e) => { if (e.key === 'Escape') navigate(-1); }; document.addEventListener('keydown', fn); return () => document.removeEventListener('keydown', fn); }, [])` 

2. **内部标签导航**（shadcn `<Tabs>`）：基础 / 数据库 / Collector / Writer

3. **「数据库」标签**：  
   三行配置卡片（Redis / MySQL / TDengine），每行：
   - 服务名标签 + 连接状态点（初始灰色「未知」，FR-GAP-D 决策）
   - 输入字段：主机 / 端口 / 用户名 / 密码（`type="password"`，未修改时 `value=""`、`placeholder="保持原值"`；详见 plan §4.6 partial update 契约）/ 数据库名（仅 MySQL/TDengine）
    - 「测试」按钮：点击 → 调用 `agentClient.checkHealth(target)` / `POST /admin/health/check`；各行独立状态

4. **「基础」标签**：暂留空（「暂无配置项」提示文字）

5. **「Collector」/「Writer」标签**：
   - 可执行文件路径（text input）+ 「选择文件」按钮（T017 前为 noop）
   - 工作目录（text input）+ 「选择目录」按钮（T017 前为 noop）

6. **底部按钮行**（固定在页面底部）：
   - 「保存」：`variant="default"`，T017 前 noop
   - 「保存并重启 Collector」：`variant="default"`（MVP 静态显示，T017 按状态条件渲染）
   - 「保存并重启 Writer」：`variant="default"`（同上）

**验收**:
- [x] `/settings` 不显示左侧导航和连接状态点（来自 AppLayout 的条件渲染）
- [x] 按 Esc 键返回（`navigate(-1)`）
- [x] 点击「← 返回」正常返回
- [x] 数据库标签三行配置卡片（Redis/MySQL/TDengine），密码 type=password，未修改时表单为空（partial update 契约）
- [x] 状态初始灰色「未知」，点击测试后按 Agent 健康检查结果显示「已连接」或「断开」，各行独立
- [x] 标签切换内容正确替换（基础/数据库/Collector/Writer）
- [x] 保存策略为页面内卡片，不是底部固定栏
- [x] App.tsx 替换 SettingsPlaceholder → SettingsPage，pnpm tsc --noEmit 0 报错
- [x] 无 agentClient/fetch/invoke/WebSocket/localStorage 调用

---

## Phase 4 — AgentClient、BackendClient、mock-agent

**前提**: T011（Rust 层）完成。T012 与 T013 可并行。  
**注**: Phase 4 可与 Phase 2/3 并行开发（不同文件），但 Phase 5/6 严格依赖 Phase 4。

---

### T012 — Agent API client + Backend client [P]

**文件**:
- 创建 `app/src/lib/agent-client.ts`
- 创建 `app/src/lib/backend-client.ts`
- 创建 `app/.env.development`

**动作**:

1. 创建 `app/.env.development`（加入 .gitignore，不提交密码）：
   ```
   VITE_USE_MOCK=true
   VITE_BACKEND_URL=http://localhost:8080
   ```

2. 创建 `AgentClient` 接口 + `realAgent` 实现（`agent-client.ts`）：

   **接口**（实现文件中 export interface AgentClient）：
   ```typescript
   export interface AgentClient {
     getBootstrapStatus():    Promise<{ in_bootstrap: boolean }>;
     putBootstrapConfig(cfg: Omit<DbConfig,'password'> & {password:string}): Promise<void>;
     postBootstrapTestConnection(target: 'mysql'): Promise<{ success: boolean; message?: string }>;
     postBootstrapFinish():   Promise<void>;
     agentLogin(u: string, p: string): Promise<{ token: string }>;
     getViewerStatus():       Promise<{ agent: string }>;
     getProcessMetrics(name: 'collector'|'writer'): Promise<ModuleStatus>;
     getProcessStatus(name:  'collector'|'writer'): Promise<{ status: string }>;
     startProcess(name:   'collector'|'writer'): Promise<void>;
     stopProcess(name:    'collector'|'writer'): Promise<void>;
     restartProcess(name: 'collector'|'writer'): Promise<void>;
     getConfig():    Promise<ConnectionConfig>;
     putConfig(cfg:  Partial<ConnectionConfig>): Promise<void>;
     checkHealth(target: 'redis'|'mysql'|'tdengine'): Promise<{ success: boolean; message?: string }>;
   }
   ```

   **realAgent 内部辅助**：
   ```typescript
   async function agentFetch<T>(
     method: string, path: string, body?: object, token?: string
   ): Promise<T> {
     // 日志脱敏：只在 debug 模式 log，password 字段替换为 [FILTERED]
     const safeBody = body ? JSON.stringify(body).replace(/"password(_enc)?":"[^"]*"/g, '"password":"[FILTERED]"') : undefined;
     if (import.meta.env.DEV) console.debug(`[agent] ${method} ${path}`, safeBody);

     let result: [number, string];
     try {
       result = await invoke<[number, string]>('agent_request', { method, path, body: safeBody, token });
     } catch (e) {
       throw { type: 'agent_unreachable' } satisfies AppError;
     }
     const [status, text] = result;
     if (status === 401) throw { type: 'unauthorized' } satisfies AppError;
     if (status >= 500) throw { type: 'api_error', status, message: '服务异常' } satisfies AppError;
     return JSON.parse(text) as T;
   }
   ```

   **VITE_USE_MOCK 选择逻辑**（生产包强制 false）：
   ```typescript
   import { mockAgent } from './mock-agent';

   const useMock =
     import.meta.env.MODE !== 'production' &&
     import.meta.env.VITE_USE_MOCK === 'true';

   export const agentClient: AgentClient = useMock ? mockAgent : realAgent;
   ```

3. 创建 `BackendClient` + `realBackend`（`backend-client.ts`）：
   ```typescript
   export interface BackendClient {
     login(userName: string, password: string): Promise<{
       token: string;
       refreshToken: string;
     }>;
   }

   // fetch 直接到 VITE_BACKEND_URL（不经 Tauri invoke）
   async function backendFetch<T>(method: string, path: string, body?: object): Promise<T> {
     const url = `${import.meta.env.VITE_BACKEND_URL}${path}`;
     const resp = await fetch(url, {
       method,
       headers: { 'Content-Type': 'application/json' },
       body: body ? JSON.stringify(body) : undefined,
     });
     if (resp.status === 401) throw { type: 'unauthorized' } satisfies AppError;
     const json = await resp.json();
     return json.data as T;   // 后端统一格式 { code, msg, data }
   }

   export const backendClient: BackendClient = { ... };
   ```

**验收**:
- [x] `pnpm exec tsc --noEmit` 通过，无类型错误
- [x] `.env.development` VITE_USE_MOCK=true，agentClient 指向 mockAgent
- [x] password 字段脱敏正则：`/"password(_enc)?":"[^"]*"/g` → `[FILTERED]`
- [x] 无 console.log token / password 明文
- [x] .env.development 已加入 app/.gitignore
- [ ] 生产构建 dist 中不含 mockAgent（待 T013 完成后验证）

---

### T013 — Mock Agent API [P]

**文件**:
- 创建 `app/src/lib/mock-agent.ts`

**动作**:

实现 `AgentClient` 接口全部方法 + BackendClient mock + WS mock。

**辅助函数**：
```typescript
const delay = (ms = Math.random() * 500 + 300) => new Promise(r => setTimeout(r, ms));
```

**内部状态**（模块作用域变量，跨调用保持）：
```typescript
let _inBootstrap = true;
const _processStatus: Record<string, ModuleStatusValue> = {
  collector: 'running',
  writer: 'stopped',
};
```

**端点实现规范**：

| 方法 | 返回 / 行为 |
|------|------------|
| `getBootstrapStatus()` | `{ in_bootstrap: _inBootstrap }` |
| `putBootstrapConfig()` | void（记录入参但不验证） |
| `postBootstrapTestConnection('mysql')` | `{ success: true }` |
| `postBootstrapFinish()` | `_inBootstrap = false` |
| `agentLogin()` | `{ token: 'mock-agent-jwt-dev' }` |
| `getViewerStatus()` | `{ agent: 'running' }` |
| `getProcessMetrics(name)` | 完整 `ModuleStatus`，status 取 `_processStatus[name]` |
| `getProcessStatus(name)` | `{ status: _processStatus[name] }` |
| `startProcess(name)` | `_processStatus[name] = 'running'`，延迟 800ms |
| `stopProcess(name)` | `_processStatus[name] = 'stopped'`，延迟 800ms |
| `restartProcess(name)` | `_processStatus[name] = 'running'`，延迟 1200ms |
| `getConfig()` | 完整 `ConnectionConfig`，所有密码 `"****"` |
| `putConfig()` | void |
| `checkHealth(target)` | `{ success: true }` |

**WS Mock**（`createMockWebSocket` 导出）：
```typescript
export function createMockWebSocket(_name: string): WebSocket {
  // 返回与 WebSocket API 兼容的对象
  // 模拟行为：
  // 1. 50ms 后触发 onopen
  // 2. onopen 后开始接受 auth 帧（忽略内容，简化验证）
  // 3. 每 400ms 向 onmessage 推送一条随机 LogLine（交替 INFO/WARN）
  // 4. close() 后停止推送
}
```

**Backend mock 导出**（供 backend-client.ts 中 mock 分支使用）：
```typescript
export const mockBackend = {
  async login(_u: string, _p: string) {
    await delay();
    return { token: 'mock-backend-token-dev', refreshToken: 'mock-refresh-dev' };
  },
};
```

**验收**:
- [x] pnpm tsc --noEmit 通过，0 报错
- [x] mockAgent 完整实现 14 个 AgentClient 方法，含内存状态（_inBootstrap / _processStatus / _config）
- [x] mockBackend 实现 login，username 非空即成功
- [x] createMockWebSocket：50ms 触发 onopen，每 400ms 推 LogLine，close() 清理定时器无泄漏
- [x] agent-client / backend-client 改为动态 import，if(_useMock) 分支由 Vite 在生产构建消除
- [x] 生产构建验证：dist 中不含 mockAgent / mockBackend / createMockWebSocket / mock token 字符串
- [x] grep：无 localStorage / sessionStorage / /viewer/trends / token|password 明文 console.log

---

## Phase 5 — 状态接入

**前提**: Phase 1（AuthContext）+ Phase 3（静态页面）+ Phase 4（clients）全部通过。  
**T015 与 T016 可并行**（依赖 T014 完成）。

---

### T014 — 接入登录与 Bootstrap 流程

**文件**:
- 修改 `app/src/App.tsx`（启动流程 + 全屏错误页）
- 修改 `app/src/pages/BootstrapPage.tsx`（接入真实/mock API）
- 修改 `app/src/pages/LoginPage.tsx`（接入真实/mock API）

**动作**:

1. **App.tsx 启动流程**（在 `<AuthProvider>` 内部，组件挂载时执行）：
   ```
   挂载 → setStartupState('loading')
        → agentClient.getBootstrapStatus()
              ├── 抛出 agent_unreachable → navigate('/error')
              ├── { in_bootstrap: true }  → navigate('/bootstrap')
              └── { in_bootstrap: false } → auth ? navigate('/') : navigate('/login')
   ```
   在 `/` 根路由 index 上设置 `<AuthGuard>`，未登录时强制跳 `/login`。

2. **全屏错误页**（嵌入 App.tsx 或独立组件）：
   - 文本：「无法连接到 Agent」副文本：`127.0.0.1:7070`
   - 「重试」按钮：重新调用 `getBootstrapStatus()`

3. **BootstrapPage 接入**：
   - 「测试连接」→ `agentClient.postBootstrapTestConnection('mysql')` → 成功绿勾 / 失败 inline 红色错误（错误文字不含密码）
   - 「完成初始化」→ 顺序执行：
     ```
     await agentClient.putBootstrapConfig(formData)
     await agentClient.postBootstrapTestConnection('mysql')
     await agentClient.postBootstrapFinish()
     navigate('/login')
     ```
     任一步失败 → 显示 inline 错误，停止序列

4. **LoginPage 接入**：
   ```typescript
   // 并发调用
   const [agentResult, backendResult] = await Promise.allSettled([
     agentClient.agentLogin(username, password),
     backendClient.login(username, password),
   ]);

   if (agentResult.status === 'rejected' || backendResult.status === 'rejected') {
     // 任一失败 → 显示对应错误，不调用 auth.login()
     const msg = agentResult.status === 'rejected'
       ? 'Agent 认证失败'
       : 'Backend 连接失败';
     setError(msg);
     return;
   }

   auth.login({
     agentToken:          agentResult.value.token,
     backendToken:        backendResult.value.token,
     backendRefreshToken: backendResult.value.refreshToken,
   });
   navigate('/');
   ```

**验收**（对应 spec US1 / US2 全部 Acceptance Scenarios）:
- [x] pnpm tsc --noEmit 通过，0 报错
- [x] App.tsx AppStartup：启动时调 getBootstrapStatus，loading 屏 → in_bootstrap:true 跳 /bootstrap / false 交 AuthGuard / 异常跳 /error
- [x] AgentErrorPage：无法连接到 Agent + 127.0.0.1:7070 + 重试按钮
- [x] BootstrapPage：测试连接 → PUT config + POST test-connection；完成初始化 → POST finish + GET status + navigate('/login')
- [x] 完成初始化在测试未通过时 disabled
- [x] LoginPage：agentLogin → backendLogin → AuthContext.login → navigate('/')
- [x] 任一登录失败 → 停留 /login + inline 错误文案，不写入 AuthContext
- [x] agent_unreachable → navigate('/error')
- [x] AuthGuard / PublicOnlyRoute 行为不变（未登录跳 /login，已登录跳 /）
- [x] 无 localStorage/sessionStorage；无 token/password 明文 console.log

---

### T015 — 接入进程状态和控制按钮 [P]

**文件**:
- 创建 `app/src/hooks/useModuleStatus.ts`
- 修改 `app/src/pages/OverviewPage.tsx`（接入轮询 + 按钮）
- 修改 `app/src/pages/CollectorPage.tsx`（摘要卡接入轮询 + 按钮）
- 修改 `app/src/pages/WriterPage.tsx`（同上）

**动作**:

1. **`useModuleStatus.ts`**：
   ```typescript
   function useModuleStatus(name: 'collector' | 'writer') {
     // 每 3000ms 调用 agentClient.getProcessMetrics(name)
     // 页面隐藏（document.visibilityState === 'hidden'）时暂停 interval
     // 恢复后立即触发一次请求
     // 返回 { data: ModuleStatus | null, loading: boolean, error: AppError | null, refetch }
   }
   ```

2. **控制按钮逻辑**（OverviewPage / CollectorPage / WriterPage 共用，可提取 `useProcessControl` hook）：
   - 维护 `operating: boolean` 状态，操作中所有按钮 `disabled`（FR-006）
   - 「启动」：直接调用 `agentClient.startProcess(name)`，无确认（FR-005）
   - 「停止」：shadcn `<AlertDialog>` 确认 → 调用 `agentClient.stopProcess(name)`
   - 「重启」：shadcn `<AlertDialog>` 确认 → 调用 `agentClient.restartProcess(name)`
   - 操作成功后调用 `refetch()` 立即刷新状态
   - 操作失败后：Toast 错误（shadcn toast/sonner），状态 pill **不被乐观更新**（FR-006）

3. **OverviewPage「日志」按钮**：`navigate('/collector')` 或 `navigate('/writer')`（US3-AC7 要求自动聚焦日志面板，通过 URL state 传参：`navigate('/collector', { state: { focusLog: true } })`，Collector 页读取并触发 scrollIntoView）

**验收**（对应 spec US3-AC1~7 / US4-AC1 / US5-AC1）:
- [x] pnpm tsc --noEmit 通过，0 报错
- [x] useModuleStatus：3s 轮询 getProcessMetrics，document.hidden 暂停，切回立即补拉
- [x] useProcessControl：start 直接执行，stop/restart 弹 AlertDialog 确认后执行
- [x] 401 → logout() 触发（useModuleStatus + useProcessControl 均处理）
- [x] operating=true 时 ModuleSummaryCard 所有按钮 disabled（FR-006）
- [x] 操作失败 toast.error，不乐观更新状态 pill
- [x] OverviewPage、CollectorPage、WriterPage 全部接入真实轮询
- [x] OverviewPage 日志按钮 navigate('/{name}', { state: { focusLog: true } })
- [x] 无 localStorage/sessionStorage；注释中的 /viewer/trends 不是代码调用
- [x] App.tsx 加 <Toaster /> 使 toast 生效

---

### T016 — 接入日志 WebSocket [P]

**文件**:
- 创建 `app/src/hooks/useWebSocketLog.ts`
- 修改 `app/src/components/LogPanel.tsx`（接入 hook 返回值）
- 修改 `app/src/pages/CollectorPage.tsx`（传 logPanelRef 给 LogPanel）
- 修改 `app/src/pages/WriterPage.tsx`（同上）

**动作**:

1. **`useWebSocketLog.ts`**：

   ```typescript
   function useWebSocketLog(name: 'collector' | 'writer') {
     const { auth } = useAuth();
     const [lines, setLines] = useState<LogLine[]>([]);
     const [wsStatus, setWsStatus] = useState<WsStatus>('connecting');
     const wsRef = useRef<WebSocket | null>(null);
     const reconnectTimer = useRef<number | null>(null);

     function connect() {
       setWsStatus('connecting');
       const ws = useMock
         ? createMockWebSocket(name)
         : new WebSocket(`ws://127.0.0.1:7070/viewer/logs/${name}/tail`);

       ws.onopen = () => {
         // FR-007: 连接后立即发第一帧 auth 消息
         ws.send(JSON.stringify({ type: 'auth', token: auth?.agentToken ?? '' }));
         setWsStatus('connected');
       };

       ws.onmessage = (ev) => {
         let line: LogLine;
         try {
           line = JSON.parse(ev.data);
           if (!['DEBUG','INFO','WARN','ERROR'].includes(line.level)) {
             line = { ...line, level: 'INFO' };
           }
         } catch {
           line = { timestamp: new Date().toISOString(), level: 'INFO', source: name, message: String(ev.data) };
         }
         setLines(prev => [...prev, line].slice(-5000));  // FR-008
       };

       ws.onclose = (ev) => {
         // ev.code === 4001 → auth 失败，不重连
         if (ev.code !== 4001) {
           setWsStatus('reconnecting');
           reconnectTimer.current = window.setTimeout(() => {
             connect();
           }, 5000);
         } else {
           setWsStatus('disconnected');
         }
       };

       wsRef.current = ws;
     }

     // auth 拿到后才连接（auth 为 null 时跳过）
     useEffect(() => {
       if (!auth) return;
       connect();
       return () => {
         clearTimeout(reconnectTimer.current ?? 0);
         wsRef.current?.close();
       };
     }, [name, auth?.agentToken]);

     return { lines, wsStatus, clear: () => setLines([]) };
   }
   ```

2. **LogPanel 改造**：接收 `useWebSocketLog` 返回的 `lines` 和 `wsStatus`（替换静态数据，保留自动滚动逻辑）。

3. **CollectorPage / WriterPage**：将 `logPanelRef` 通过 `ref` 传给 LogPanel，「打开日志」调用 `scrollIntoView`。

**验收**（对应 spec US4-AC4/5/6/7 / US5-AC4/5 / SC-004/5/7）:
- [ ] 日志面板在 WS 连接建立后 1 秒内出现 mock 日志（SC-004）
- [ ] 关闭「自动滚动」后日志停止自动下滚（US4-AC5）
- [ ] mock 推送频率调高（改为每 5ms 一条），5000 行后旧行被丢弃（SC-005 逻辑验证）
- [ ] 手动调用 `ws.close()` 后面板顶部出现断线 Banner，5 秒后重新连接（SC-007）
- [ ] 日志行每行无 `source` 字段渲染

---

## Phase 6 — 设置页配置读写

**前提**: Phase 5 全部通过。

---

### T017 — 接入配置读写

**文件**:
- 修改 `app/src/pages/SettingsPage.tsx`

**动作**:

1. **页面挂载时加载配置**：
   ```typescript
   useEffect(() => { agentClient.getConfig().then(setConfig); }, []);
   ```
   加载中显示 `<Skeleton>` 占位（shadcn Skeleton）。

2. **表单状态管理**：
   ```typescript
   const [config, setConfig] = useState<ConnectionConfig | null>(null);
   const [dirtyFields, setDirtyFields] = useState<Set<string>>(new Set());
   // 用户修改字段时加入 dirtyFields
   ```

3. **密码字段规则**（FR-011）：
   - 初始值显示 `"****"`（来自 Agent 配置）
   - 用户聚焦密码字段时**清空**（`onFocus={() => field.value === '****' && setFieldValue('')}`），提示用户重新输入
   - 保存时：若字段未在 dirtyFields 中（或值仍为 `"****"`），从请求体中**删除**该字段

4. **「测试」按钮**（FR-GAP-C 决策：MVP 走 mock）：
   - 点击 → `setRowStatus(target, 'testing')`（当前行 spinner）
   - `agentClient.checkHealth(target)` → 成功 `setRowStatus(target, 'connected')` / 失败 `setRowStatus(target, 'error')`
   - 各行状态独立（`rowStatus: Record<'redis'|'mysql'|'tdengine', 'unknown'|'testing'|'connected'|'error'>`）

5. **「保存」逻辑**（FR-012 / FR-013）：
   ```typescript
   async function handleSave() {
     const dirtyConfig = buildDirtyConfig(config, dirtyFields);
     await agentClient.putConfig(dirtyConfig);

     // 查两个模块状态
     const [cStatus, wStatus] = await Promise.all([
       agentClient.getProcessStatus('collector'),
       agentClient.getProcessStatus('writer'),
     ]);

     // 根据状态弹 Dialog（两个模块各弹一次，或合并为一个 Dialog）
     showRestartDialog('collector', cStatus.status);
     showRestartDialog('writer', wStatus.status);
   }

   function showRestartDialog(name: string, status: string) {
     if (status === 'running') {
       // AlertDialog: 「是否重启 {name} 使配置生效？」
       // 确认 → agentClient.restartProcess(name)
     } else if (status === 'stopped') {
       // AlertDialog: 「配置将在下次启动时生效。」+ 「立即启动」按钮
     } else {
       // AlertDialog: 「无法确认模块状态，请到模块页手动操作。」
     }
   }
   ```

6. **「保存并重启 {name}」主推按钮**：
   - 仅当对应模块 status === 'running' 时显示
   - 点击 → `putConfig(dirtyConfig)` + `restartProcess(name)`，无额外确认

7. **文件选择按钮**（Collector/Writer 标签，FR-012 允许 Tauri dialog plugin）：
   ```powershell
   pnpm --prefix app add @tauri-apps/plugin-dialog
   # Cargo.toml 追加：tauri-plugin-dialog = "2"
   # tauri.conf.json plugins.dialog 配置
   ```
   ```typescript
   import { open } from '@tauri-apps/plugin-dialog';
   const path = await open({ multiple: false, filters: [{ name: 'Executable', extensions: ['exe'] }] });
   if (path) setFieldValue('executable', path);
   ```

**验收**（对应 spec US6 全部 Acceptance Scenarios）:
- [ ] 设置页打开后密码字段显示 "****"（US6-AC7）
- [ ] 只修改主机字段保存 → 请求体中无密码字段（Network tab 确认）（US6-AC7 延伸）
- [ ] 点击「测试」→ 该行状态更新，其他行不变（US6-AC2/3）
- [ ] 保存后根据 collector mock 状态（running）→ 弹出「是否重启 Collector」Dialog（US6-AC4）
- [ ] 将 collector mock 改为 stopped → 弹出「立即启动」Dialog（FR-013）
- [ ] 密码明文不出现在 DevTools Network 请求体（SC-006）
- [ ] 「选择文件」按钮打开 Tauri 系统文件对话框（US6-AC6）

---

### 契约修订记录 — 2026-05-26 密码 partial update

T017 完成后，前后端密码字段语义经过对齐，采纳「方案 B：字段省略」。详细契约见 plan §4.6。

变更范围：
- `app/src/types/index.ts`：`DbConfig.password` 由必选改为可选（`string?`）
- `app/src/lib/mock-agent.ts`：`_config` 初值不带 password；新增 `_passwords` 内部存储；`putConfig` 改为对每个 db section 单独合并并按 partial update 处理密码
- `app/src/pages/SettingsPage.tsx`：`configToForm` 用 `?? ""` 兜底；`dbSection` 去掉 `password: ""` 旧逻辑，未修改时不发 password key
- `.specify/spec.md`、`.specify/plan.md`、`.specify/tasks.md`：同步更新

不变更：
- `agent-client.ts` 接口签名
- `BootstrapPage.tsx`（Bootstrap 时密码必填，不走 partial update）
- 密码脱敏规则（sanitize 正则不变）

---

## Phase 7 — 收尾

**前提**: Phase 6 通过。T018 先完成，T019 最后执行。

---

### T018 — 空态 / 错误态 / 加载态

**文件**:
- 修改 `app/src/components/ModuleCard.tsx`（Skeleton + error 态）
- 修改 `app/src/pages/OverviewPage.tsx`（传 loading/error 给 ModuleCard）
- 确认 `app/src/components/TrendChart.tsx`（四态均已实现）

**动作**:

1. **ModuleCard loading 态**：`status === null` 时渲染 `<Skeleton>` 占位（与卡片等高）。

2. **ModuleCard error 态**：`error !== null` 时显示红色边框 + 「状态获取失败，正在重试…」文字。

3. **OverviewPage 传递 loading/error**：将 `useModuleStatus` 的 `loading` 和 `error` 透传给 ModuleCard。

4. **SettingsPage 加载 Skeleton**：`getConfig()` 进行中（loading=true）时渲染配置行 Skeleton。

5. **确认 TrendChart** `empty`/`loading`/`error`/`ready` 四态 UI 正常；`ready` 使用 Recharts 折线/面积图，标题右侧显示当前值，右上角支持 1/5/10 分钟切换。

**验收**:
- [ ] 将 mock `getProcessMetrics` 改为延迟 3 秒 → ModuleCard 显示 Skeleton 3 秒后恢复
- [ ] 将 mock `getProcessMetrics` 改为抛出错误 → ModuleCard 显示红色错误态
- [ ] 将 mock `getBootstrapStatus` 改为抛出 `agent_unreachable` → 全屏错误页正常显示
- [ ] TrendChart empty/loading/error/ready 各态均可通过 props 触发

---

### T019 — 打包与手动验收

**文件**: 无新代码文件（可选：创建 `docs/验收清单.md` 记录测试结果）

**动作**:

1. **生产构建验证**：
   ```powershell
   # 不设置 VITE_USE_MOCK（默认 false）
   pnpm --prefix app build
   # 验证产物中无 mock 代码（mock 应被 tree-shake）
   Select-String -Path "app\dist\assets\*.js" -Pattern "mockAgent|createMockWebSocket" -Quiet
   # 预期：无匹配（输出为空）
   ```

2. **逐项 SC 手动验收**（mock 模式下）：

   | 编号 | 验收项 | 操作步骤 | 通过标准 |
   |------|--------|---------|---------|
   | SC-001 | Bootstrap < 2 分钟 | 重置 `_inBootstrap = true`，走完向导 | 全程 < 2 分钟无需查文档 |
   | SC-002 | 总览页 < 3 秒显状态 | 登录后立即计时 | ModuleCard 内容 < 3 秒出现 |
   | SC-003 | 控制操作 < 10 秒响应 | 点击「启动」计时 | 状态 pill 更新 < 10 秒 |
   | SC-004 | WS 日志 < 1 秒出现 | 进 CollectorPage 计时 | 首条日志 < 1 秒 |
   | SC-005 | 高频日志不卡顿 | 将 mock 改为每 5ms 推一条，观察 5000 条触发 | 无明显卡顿，旧行被丢弃 |
   | SC-006 | 密码不出现在日志/console/网络 | DevTools 逐项检查 | 三处均无明文密码 |
   | SC-007 | WS 断线 5s 内重连 | 手动调用 `ws.close()`，计时 | 5 秒内 Banner 消失，日志恢复 |

3. **架构边界最终确认**：
   ```powershell
   # 确认 lib.rs 只注册三个命令
   Get-Content "app\src-tauri\src\lib.rs"
   # 预期：仅见 agent_request / show_window / hide_to_tray

   # 确认前端无直接进程操作
   Select-String -Path "app\src\**\*.tsx","app\src\**\*.ts" `
     -Pattern "spawn|exec|child_process|new Process" -Recurse
   # 预期：无匹配

   # 确认无 Redux/Zustand/React Query 引入
   Select-String -Path "app\src\**\*.tsx","app\src\**\*.ts" `
     -Pattern "from 'redux'|from 'zustand'|from '@tanstack/react-query'" -Recurse
   # 预期：无匹配
   ```

**验收**:
- [ ] `pnpm --prefix app build` 成功，无构建错误
- [ ] 生产包中无 `mockAgent` / `createMockWebSocket` 字样
- [ ] SC-001 ~ SC-007 全部手动通过
- [ ] lib.rs 确认仅有 3 个注册命令
- [ ] 无直接进程调用代码
- [ ] 无禁止的状态管理库

---

## 依赖关系图

```
T001 [P] ─┐
T011 [P] ─┘ Phase 0 Gate ─────────────────────────┐
                                                   │
                    T002 → T003 [P] ─┐             │
                           T004 [P] ─┘ Phase 1     │
                                       │           │
                           T005 [P] ─┐ │           │
                           T006 [P] ─┘ │ Phase 2   │
                                       │           │
                    T007 [P] ─┐        │           │
                    T008 [P] ─┤ Phase 3│           │
                    T009 [P] ─┤        │    T012 [P] ─┐
                    T010 [P] ─┘        │    T013 [P] ─┘ Phase 4
                                       │           │
                              T014 ←───┴───────────┘
                                │
                    T015 [P] ───┤ Phase 5
                    T016 [P] ───┘
                                │
                              T017    Phase 6
                                │
                    T018 ───────┤ Phase 7
                    T019 ───────┘
```

**关键约束**: Phase 4（T012/T013）可与 Phase 2/3 并行开发（不同文件），但 Phase 5（T014~T016）必须等待 Phase 4 **和** Phase 1（T004 AuthContext）都完成。
