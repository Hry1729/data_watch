# 实现计划：Data Watch Console 桌面应用

**Branch**: `001-data-watch-console` | **Date**: 2026-05-25 | **Spec**: `.specify/spec.md`

---

## 1. 架构方案

### 1.1 系统全景

```
┌────────────────────────────────────────────────────────────────┐
│  Tauri 桌面窗口（WebView2）                                     │
│  React + TypeScript + shadcn/ui                                │
│                                                                │
│  ┌──────────────────────────────────┐                          │
│  │  agent-client.ts                 │                          │
│  │  invoke('agent_request', ...)    │──────→ Rust http_proxy   │
│  │  mock-agent.ts (VITE_USE_MOCK)   │                          │
│  └──────────────────────────────────┘                          │
│  ┌──────────────────────────────────┐                          │
│  │  WS: new WebSocket(ws://127....) │──────→ Agent WS 直连     │
│  └──────────────────────────────────┘                          │
│  ┌──────────────────────────────────┐                          │
│  │  backend-client.ts               │                          │
│  │  fetch(VITE_BACKEND_URL/api/...) │──────→ Backend HTTP 直连 │
│  └──────────────────────────────────┘                          │
└────────────────────────────────────────────────────────────────┘
         │ invoke                      │ HTTP/WS
         ▼                             ▼
┌──────────────────┐      ┌────────────────────────────────────┐
│ Tauri Rust       │      │ DataWatch Agent 127.0.0.1:7070     │
│ http_proxy.rs    │─────▶│ Python FastAPI / Windows Service   │
│ tray.rs          │      │                                    │
│ main.rs          │      │  ├── 进程管理 (collector/writer)   │
└──────────────────┘      │  ├── 配置读写 (config.toml+DPAPI) │
                          │  ├── 日志推送 (WS /viewer/logs/)   │
                          │  ├── Auth (MySQL sys_user)         │
                          │  └── 健康检查 (TODO)               │
                          └────────────────────────────────────┘
                                        │
                          ┌─────────────┼─────────────┐
                          ▼             ▼             ▼
                       Redis         MySQL        TDengine
```

### 1.2 通信路径决策

| 通信类型 | 路径 | 原因 |
|---------|------|------|
| Agent HTTP | WebView → `invoke('agent_request')` → Rust → Agent | 解决 CORS；统一附加 Bearer token |
| Agent WS | WebView → `new WebSocket(ws://127.0.0.1:7070/...)` 直连 | Tauri Rust 不代理 WS；浏览器 WS API 天然支持 |
| Backend HTTP | WebView → `fetch(VITE_BACKEND_URL/api/...)` 直连 | Backend 在不同域名/端口；CSP=null，无限制 |

**WebSocket 鉴权**：连接建立后发送第一帧 auth 消息（不在 URL 暴露 token）：
```json
{ "type": "auth", "token": "<agentToken>" }
```
Agent 收到 auth 帧验证通过后开始推送日志；验证失败则关闭连接。

### 1.3 关键约束确认（宪章合规）

| 宪章条款 | 本计划处理方式 |
|---------|-------------|
| IV: Tauri Rust 仅 http_proxy + tray | `src-tauri/src/` 只有 3 个文件：`main.rs`, `http_proxy.rs`, `tray.rs` |
| IV: 禁止 Rust 启动/终止进程 | 进程控制全部通过 Agent HTTP API |
| IV: 禁止 Rust 读写配置文件 | 配置通过 `PUT/GET /admin/config` |
| IV: 禁止 Rust 捕获 stdout/stderr | 日志通过 Agent WS 推送 |
| VI: 数据流约束 | 前端不直接读写 Redis/TDengine |

---

## 2. 前端模块划分

### 2.1 目录结构（实现后）

```
app/src/
│
├── types/
│   └── index.ts                    # 共享类型（见 2.2）
│
├── lib/
│   ├── utils.ts                    # shadcn 工具（已有）
│   ├── agent-client.ts             # Agent HTTP client（Tauri invoke）
│   ├── backend-client.ts           # Backend HTTP client（fetch）
│   └── mock-agent.ts              # Mock 数据层（VITE_USE_MOCK=true）
│
├── contexts/
│   └── AuthContext.tsx             # AuthStore（内存，不持久化）
│
├── hooks/
│   ├── useModuleStatus.ts          # 轮询 GET /viewer/processes/{name}/metrics
│   ├── useConnectionHealth.ts      # 顶部连接状态 10 秒轮询 GET /viewer/health
│   └── useWebSocketLog.ts          # WS 日志流（5000行限制、自动重连）
│
├── components/
│   ├── ui/                         # shadcn 原生组件（已有）
│   ├── AppLayout.tsx               # Shell：侧边栏 + 连接状态 + 齿轮入口
│   ├── ModuleCard.tsx              # 模块状态卡片（状态pill + 控制按钮）
│   ├── TrendChart.tsx              # 趋势图（Recharts + 4态 + 1/5/10分钟切换）
│   ├── LogPanel.tsx                # 实时日志（自动滚动开关 + 断线Banner）
│   └── ConnectionStatusBar.tsx     # 顶部连接状态点（绿/蓝/红/灰）
│
└── pages/
    ├── BootstrapPage.tsx           # MySQL 配置向导（三步序列）
    ├── LoginPage.tsx               # 双认证登录（Agent + Backend）
    ├── OverviewPage.tsx            # 两个 ModuleCard + 两个 TrendChart
    ├── CollectorPage.tsx           # Collector 详情 + 指标 + 任务表 + 日志
    ├── WriterPage.tsx              # Writer 详情 + 指标 + 任务表 + 日志
    └── SettingsPage.tsx            # 配置管理（内部导航）
```

### 2.2 核心类型定义（types/index.ts）

```typescript
// AuthStore（内存，不持久化）
export interface AuthStore {
  agentToken: string;
  backendToken: string;
  backendRefreshToken: string;
}

// 模块运行状态
export interface ModuleStatus {
  name: 'collector' | 'writer';
  status: 'running' | 'stopped' | 'crashed' | 'unknown';
  pid: number | null;
  uptime_seconds: number;
  memory_mb: number;
  cpu_percent: number;
  started_at: string | null;
  restart_count: number;
}

// WS 日志行
export interface LogLine {
  timestamp: string;           // ISO 8601
  level: 'DEBUG' | 'INFO' | 'WARN' | 'ERROR';
  source: string;              // 保留，但单模块页不渲染
  message: string;
}

// 连接配置（密码字段采用 partial update 语义，详见 §4.x 密码契约）
export interface ConnectionConfig {
  redis: { host: string; port: number; password?: string; };
  mysql: { host: string; port: number; user: string; password?: string; database: string; };
  tdengine: { host: string; port: number; user: string; password?: string; database: string; };
  collector: { executable: string; working_dir: string; };
  writer: { executable: string; working_dir: string; };
}

// TrendChart 状态
export type TrendChartState = 'empty' | 'loading' | 'error' | 'ready';

// 统一应用错误类型
export type AppError =
  | { type: 'agent_unreachable' }
  | { type: 'unauthorized' }
  | { type: 'api_error'; status: number; message: string }
  | { type: 'network_error'; message: string };
```

### 2.3 模块职责边界

| 模块 | 职责 | 不包含 |
|------|------|--------|
| `agent-client.ts` | Agent HTTP 调用封装；401 检测；密码字段过滤 | 业务逻辑、组件渲染 |
| `backend-client.ts` | Backend HTTP 调用；401 检测 | Agent 调用、进程管理 |
| `mock-agent.ts` | 覆盖全部 API 端点；WS mock | 真实 HTTP 请求 |
| `AuthContext.tsx` | login/logout；AuthStore 内存存储；401 响应触发 logout | 持久化、token 刷新 |
| `useModuleStatus.ts` | 3 秒轮询，返回 ModuleStatus | 进程控制操作 |
| `useWebSocketLog.ts` | WS 连接、断线重连、5000行截断 | 日志渲染、业务逻辑 |
| `AppLayout.tsx` | 页面骨架、导航切换、设置页显隐 | 数据获取 |

---

## 3. Tauri Rust 模块划分

### 3.1 文件清单（严格仅3个）

```
app/src-tauri/src/
├── main.rs           # 入口（已有，保持不变）
├── lib.rs            # run() 函数：注册命令，构建 Builder（重写）
├── http_proxy.rs     # agent_request command（新建）
└── tray.rs           # 托盘命令（新建，Phase 5 实现）
```

> **注**: 当前 `lib.rs` 包含 `greet` 示例命令，需完全重写为 `agent_request` + 托盘命令。

### 3.2 http_proxy.rs 契约

```rust
// 函数签名
#[tauri::command]
async fn agent_request(
    method: String,       // "GET" | "POST" | "PUT" | "DELETE"
    path: String,         // "/bootstrap/status" 等（不含 host）
    body: Option<String>, // JSON 字符串
    token: Option<String>,// Agent JWT（不含 "Bearer " 前缀）
) -> Result<(u16, String), String>
// 返回: Ok((status_code, response_body_string))
//       Err(error_message)
```

**安全规则（不可违反）**：
- 禁止在任何 log 输出中记录 `Authorization` 头的值
- 禁止在任何 log 输出中记录请求体中含 `password` 的字段值
- 目标地址硬编码为 `http://127.0.0.1:7070`，不接受来自前端的 URL 参数

**Cargo.toml 追加依赖**（http_proxy.rs 需要）：
```toml
[dependencies]
tauri = { version = "2", features = [] }
tauri-plugin-opener = "2"
serde = { version = "1", features = ["derive"] }
serde_json = "1"
reqwest = { version = "0.12", features = ["json"] }    # 新增
tokio = { version = "1", features = ["full"] }          # 新增（Tauri 2 已集成，确认即可）
```

### 3.3 tray.rs 契约（Phase 5，暂不实现）

```rust
#[tauri::command]
fn show_window(window: tauri::Window) { ... }

#[tauri::command]
fn hide_to_tray(window: tauri::Window) { ... }
```

MVP 阶段 `tray.rs` 文件存在但命令为空壳，不影响其他功能。

### 3.4 lib.rs 重写结构

```rust
mod http_proxy;
mod tray;

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

---

## 4. Agent API 适配层

### 4.1 agent-client.ts 接口设计

```typescript
// 根据环境变量选择真实/mock实现
export const agentClient: AgentClient = 
  import.meta.env.VITE_USE_MOCK === 'true' ? mockAgent : realAgent;

// 统一接口（真实 + mock 共同实现）
export interface AgentClient {
  // Bootstrap
  getBootstrapStatus(): Promise<{ in_bootstrap: boolean }>;
  putBootstrapConfig(config: MySQLConfig): Promise<void>;
  postBootstrapTestConnection(target: 'mysql'): Promise<{ success: boolean; message?: string }>;
  postBootstrapFinish(): Promise<void>;

  // Auth
  agentLogin(userName: string, password: string): Promise<{ token: string }>;

  // Viewer
  getViewerStatus(): Promise<{ agent: string }>;
  getProcessMetrics(name: 'collector' | 'writer'): Promise<ModuleStatus>;
  getProcessStatus(name: 'collector' | 'writer'): Promise<{ status: string }>;

  // Admin
  startProcess(name: 'collector' | 'writer'): Promise<void>;
  stopProcess(name: 'collector' | 'writer'): Promise<void>;
  restartProcess(name: 'collector' | 'writer'): Promise<void>;
  getConfig(): Promise<ConnectionConfig>;
  putConfig(config: Partial<ConnectionConfig>): Promise<void>;

  // Health（预留，MVP 走 mock）
  checkHealth(target: 'redis' | 'mysql' | 'tdengine'): Promise<{ success: boolean; message?: string }>;
}
```

### 4.2 backend-client.ts 接口设计

```typescript
export interface BackendClient {
  login(userName: string, password: string): Promise<{
    token: string;
    refreshToken: string;
  }>;
  // 后续业务接口按需扩展
}
```

Backend URL 通过 `.env` 配置：
```
VITE_BACKEND_URL=http://localhost:8080   # 开发环境
```

### 4.5 VITE_USE_MOCK 使用规则

| 场景 | 值 | 方式 |
|------|---|------|
| 本地开发（无 Agent） | `true` | 开发者在 shell 中显式设置：`$env:VITE_USE_MOCK="true"` |
| 本地开发（有真实 Agent） | `false`（默认） | 不设置该变量即可 |
| CI 构建 | `false`（默认） | 不设置 |
| 生产打包（`pnpm build`） | **禁止** `true` | `agent-client.ts` 在 `mode === 'production'` 时忽略该变量，强制走 realAgent |

**实现要点**：`agent-client.ts` 选择逻辑：
```typescript
const useMock =
  import.meta.env.MODE !== 'production' &&
  import.meta.env.VITE_USE_MOCK === 'true';

export const agentClient: AgentClient = useMock ? mockAgent : realAgent;
```

> 未设置 `VITE_USE_MOCK` 时 `import.meta.env.VITE_USE_MOCK` 为 `undefined`，`=== 'true'` 为 `false`，自动走 realAgent。生产包额外加 `MODE !== 'production'` 双重保险。

### 4.3 realAgent 实现要点

```typescript
// 内部辅助函数
async function agentFetch(
  method: string,
  path: string,
  body?: object,
  token?: string
): Promise<unknown> {
  const bodyStr = body ? JSON.stringify(body) : undefined;
  const [status, responseStr] = await invoke<[number, string]>('agent_request', {
    method, path, body: bodyStr, token
  });
  
  if (status === 401) throw { type: 'unauthorized' };
  if (status >= 500) throw { type: 'api_error', status, message: '服务异常' };
  
  return JSON.parse(responseStr);
}
```

**密码过滤**：发送前将请求体中 `password` 字段替换为 `[FILTERED]` 再记录 console.debug。

### 4.4 mock-agent.ts 端点覆盖

| 端点 | Mock 响应 | 模拟延迟 |
|------|---------|---------|
| GET /bootstrap/status | `{ in_bootstrap: true }` 初始；finish 后切 false | 400ms |
| PUT /bootstrap/config | `{ success: true }` | 600ms |
| POST /bootstrap/test-connection | `{ success: true }` 或模拟失败 | 800ms |
| POST /bootstrap/finish | `{ success: true }` | 500ms |
| POST /auth/login | `{ token: "mock-agent-jwt-xxx" }` | 500ms |
| POST /api/auth/login | `{ code: 200, msg: "success", data: { token: "mock-backend-token", refreshToken: "mock-refresh-xxx" } }` | 500ms |
| GET /viewer/processes/{name}/metrics | ModuleStatus mock | 300ms |
| GET /viewer/processes/{name}/status | `{ status: "running" }` | 300ms |
| POST /admin/processes/{name}/start/stop/restart | `{ success: true }` + 延迟后 status 变化 | 1000ms |
| GET /admin/config | ConnectionConfig（密码全 "****"） | 400ms |
| PUT /admin/config | `{ success: true }` | 600ms |
| POST /admin/health/check | `{ success: true }` | 600ms |
| GET /viewer/health | 三个服务的 `connected/checking/disconnected/unknown` 状态 | 300ms |
| WS /viewer/logs/{name}/tail | 每秒推送 2–4 条 LogLine JSON | — |

**WS Mock 实现**：mock-agent.ts 导出 `createMockWebSocket(name)` 函数，返回一个行为与原生 WebSocket 相同的模拟对象，通过 `setInterval` 定时触发 `onmessage`。

### 4.6 密码字段 partial update 契约

`PUT /admin/config` 中 `DbConfig.password` 采用 partial update 语义，前后端必须严格遵守：

| 编号 | 规则 |
|------|------|
| C1 | `GET /admin/config` 返回的 `DbConfig` **不包含** `password` 字段（不回传任何形式的密码） |
| C2 | `PUT /admin/config` 采用 partial update 语义（仅对 `password` 字段生效） |
| C3 | 请求体中**不包含** `password` 字段 → Agent **保留**原密码 |
| C4 | 请求体中**包含** `password` 字段 → Agent **用该值更新**密码 |
| C5 | 前端**禁止**发送 `password: ""` 表达保留语义（必须省略 key） |
| C6 | 本阶段**不支持**清空密码；如未来需要清空，必须新增显式字段或独立接口，不得复用本约定 |

**前端实现要点**：

- 类型：`DbConfig.password` 为 `string?`（可选）
- `configToForm`：`cfg.X.password ?? ""`，缺失时表单显示空串
- `buildPayload / dbSection`：用户未修改密码（`dirty === false`）时构造的 section 对象**不出现** `password` 属性；修改时通过 spread 加入 `password: f.password`
- **禁止**写 `password: dirty ? f.password : ""`（违反 C5）
- mock-agent.ts 必须如实模拟：`_config` 初值不带 password；`putConfig` 仅当请求体含 `password` 时才更新内部存储

**密码脱敏规则保持不变**（见 §6.3）：debug 日志中 `password` / `password_enc` 字段值仍由 sanitize 正则替换为 `[FILTERED]`。

---

## 5. 状态管理方案

### 5.1 原则

**不引入** Redux / Zustand / React Query。  
使用 React 内置原语（Context + useState + useEffect）+ 自定义 Hook。理由：应用规模固定（6 个页面），数据来源单一（Agent），无跨页面共享的复杂服务端缓存需求。

### 5.2 状态分层

```
┌─────────────────────────────────────────────────────┐
│  AuthContext（全局）                                  │
│  agentToken | backendToken | backendRefreshToken      │
│  login() | logout()                                  │
└─────────────────────────────────────────────────────┘
         │ useContext(AuthContext)
┌────────┴──────────────────────────────────────────┐
│  页面级 useState                                   │
│  OverviewPage: { collectorStatus, writerStatus }   │
│  CollectorPage: { status, metrics, tasks }         │
│  WriterPage: { status, metrics, tasks }            │
│  SettingsPage: { config, dirtyFields, saveState }  │
└────────────────────────────────────────────────────┘
         │ useHook
┌────────┴──────────────────────────────────────────┐
│  自定义 Hook                                       │
│  useModuleStatus(name) → { data, loading, error }  │
│  useWebSocketLog(name) → { lines, wsStatus, clear }│
└────────────────────────────────────────────────────┘
```

### 5.3 AuthContext 规范

```typescript
interface AuthContextValue {
  auth: AuthStore | null;           // null = 未登录
  login(store: AuthStore): void;    // 写入内存
  logout(): void;                   // 清除内存 → navigate('/login')
}
```

- **401 触发**：`agent-client.ts` 和 `backend-client.ts` 中捕获 401，调用 `AuthContext.logout()`
- **并发 401 保护**：`logout()` 内部加标志位，已在跳转中则忽略重复调用

### 5.4 useModuleStatus 规范

```typescript
// 轮询间隔：3000ms
// 页面隐藏时（document.hidden）暂停轮询，恢复时立即刷新
// 返回最新一次成功数据 + loading + error
function useModuleStatus(name: 'collector' | 'writer'): {
  data: ModuleStatus | null;
  loading: boolean;
  error: AppError | null;
  refetch: () => void;
}
```

### 5.5 useWebSocketLog 规范

```typescript
// WS 地址：ws://127.0.0.1:7070/viewer/logs/{name}/tail
// 鉴权：连接后立即发第一帧 { "type": "auth", "token": agentToken }
//        Agent 验证失败则关闭连接（前端捕获 onclose，显示断线 Banner）
// 断线：5 秒后重连（指数退避，上限 5s），重连成功后重新发 auth 帧
// 行数：超过 5000 行时 lines.slice(-5000)
function useWebSocketLog(name: 'collector' | 'writer'): {
  lines: LogLine[];
  wsStatus: 'connecting' | 'connected' | 'disconnected' | 'reconnecting';
  clear: () => void;
}
```

---

## 6. 错误处理方案

### 6.1 错误层次

```
用户操作
  └─→ 组件调用 agentClient.xxx()
        └─→ agent-client.ts 捕获底层错误，映射为 AppError
              ├── type: 'agent_unreachable'  → 启动页：显示错误页 + 重试按钮
              ├── type: 'unauthorized'       → AuthContext.logout() → 跳登录页
              ├── type: 'api_error'          → 组件内显示 Toast / inline 错误
              └── type: 'network_error'      → 组件内显示 Toast
```

### 6.2 各页面错误处理规则

| 页面/操作 | 错误场景 | 处理方式 |
|-----------|---------|---------|
| 应用启动 | GET /bootstrap/status 失败 | 全屏错误页："无法连接 Agent (127.0.0.1:7070)"，重试按钮 |
| Bootstrap 测试连接 | 失败 | 输入框下方 inline 红色错误，不含密码明文 |
| 登录 | Agent 失败 | 表单下方红色提示，保留 username |
| 登录 | Backend 失败 | 表单下方红色提示，区分 Agent/Backend 来源 |
| 进程控制 | 操作失败 | Toast 错误，状态 pill 不被乐观更新 |
| 日志 WS | 断线 | 面板顶部 Banner："连接已断开，正在重连…(5s)" |
| 设置保存 | 失败 | 表单下方 inline 错误 |
| 401（任何 API） | token 过期 | 跳登录页，登录后自然恢复原操作 |

### 6.3 密码脱敏规则（强制）

- `agent-client.ts` 中在记录 console.debug 前，将请求体中 `password`, `password_enc` 字段替换为 `[FILTERED]`
- `LogPanel.tsx` 渲染消息文本时，**不**做额外过滤（日志内容由 Agent 控制，已是处理后的文本）
- 任何错误 Toast / inline 错误文字，不含原始请求体内容

---

## 7. 验证方案

### 7.1 开发期验证（Mock 模式）

`VITE_USE_MOCK` 使用规则（见第 4 节）：开发期显式设为 `true`，其他情况默认 `false`。

```powershell
# 开发期启动（需显式传 true）
$env:VITE_USE_MOCK = "true"; pnpm --prefix app dev
```

每个 Phase 完成后，按「实现阶段」各 Phase 的 **验收** 条目逐条手动验证；通过后提交。

### 7.2 成功标准验收清单（对应 spec SC-001~SC-007）

- [ ] SC-001：Bootstrap 全流程 < 2 分钟（mock）
- [ ] SC-002：总览页 < 3 秒显示模块状态
- [ ] SC-003：进程控制响应 < 10 秒（mock 延迟 1s）
- [ ] SC-004：WS 连接后日志 < 1 秒出现
- [ ] SC-005：> 100 行/秒不卡顿（mock 设 5 条/秒，5000 行阈值逻辑测试）
- [ ] SC-006：密码不出现在日志面板、console、网络请求明文响应
- [ ] SC-007：WS 断线 5 秒内重连（mock 断线：关闭 mock WS 定时器）

### 7.3 集成测试（接入真实 Agent 后）

1. 启动真实 Agent（127.0.0.1:7070），关闭 `VITE_USE_MOCK`
2. 依次验证：Bootstrap 真实写入配置 → 登录获取真实 JWT → 启停进程 → 查看实时日志

---

## 8. 风险清单

| ID | 风险 | 概率 | 影响 | 缓解措施 |
|----|------|------|------|---------|
| R-01 | **日志高频渲染性能**（> 100 行/秒时帧率 < 30fps） | 中 | 高 | 先用 `slice(-5000)` 截断；若仍卡顿引入 `@tanstack/react-virtual` 虚拟滚动 |
| R-02 | **并发 401 竞态**（两个请求同时 401，触发两次 logout+navigate） | 低 | 中 | `logout()` 内部加 `isLoggingOut` 标志位，重复调用幂等 |
| R-03 | **Backend URL 未定义**（Backend 在哪台机器、哪个端口未知） | 高 | 高 | 使用 `VITE_BACKEND_URL` 环境变量；mock 阶段不阻塞；正式联调前确认 |
| R-04 | **reqwest 在 Tauri v2 + Windows 的 TLS 兼容性**（Agent 若用 HTTPS） | 低 | 中 | Agent 目前为 HTTP（127.0.0.1 内部），无 TLS；若后续要 HTTPS 再处理 |
| R-05 | **设置页密码字段"不传递"逻辑**（`****` 传或不传由 Agent 决定，前端需一致） | 中 | 中 | `putConfig` 时：用户未修改的密码字段从请求体中删除（不传 `****`），由 Agent 保持原值 |
| R-06 | **趋势图数据源 TODO**（MVP 是 empty，但组件已必须支持 4 态） | 中 | 低 | `TrendChart` props 接受 `state: TrendChartState` + `data?`，MVP 传 `state="empty"`，不硬编码 |
| R-07 | **WS first-frame 鉴权竞态**（Agent 在收到 auth 帧前可能推送数据） | 低 | 低 | Agent 设计时必须等收到 auth 帧验证通过后才开始推流；前端在 onopen 后立即发 auth 帧 |
| R-08 | **Tauri 脚手架重写影响热更新**（当前 lib.rs 包含 greet 示例） | 低 | 低 | Phase 0 第一步即重写 lib.rs + http_proxy.rs，之后稳定 |

---

## 实现阶段（Phase 计划）

### Phase 0 — 基础设施（所有阶段的前提）

**目标**：类型系统、Tauri Rust 层、API 客户端、Mock 层、路由 Shell、AuthContext 全部就绪。

**任务**:

| # | 文件 | 变更 |
|---|------|------|
| F-01 | `src/types/index.ts` | 创建：AuthStore、ModuleStatus、LogLine、ConnectionConfig、AppError、TrendChartState |
| F-02 | `src/contexts/AuthContext.tsx` | 创建：React Context，内存存储 agentToken/backendToken/backendRefreshToken |
| F-03 | `src/lib/mock-agent.ts` | 创建：覆盖全部端点 + WS mock（createMockWebSocket）|
| F-04 | `src/lib/agent-client.ts` | 创建：VITE_USE_MOCK 分支；realAgent 通过 invoke；密码过滤 |
| F-05 | `src/lib/backend-client.ts` | 创建：fetch(VITE_BACKEND_URL/api/...)；401 检测 |
| F-06 | `src/App.tsx` | 重写：React Router v6；启动检 /bootstrap/status；AuthGuard |
| F-07 | `src-tauri/src/http_proxy.rs` | 创建：agent_request command |
| F-08 | `src-tauri/src/tray.rs` | 创建：空壳 show_window / hide_to_tray |
| F-09 | `src-tauri/src/lib.rs` | 重写：注册 agent_request + tray 命令，移除 greet |
| F-10 | `src-tauri/Cargo.toml` | 追加：reqwest 依赖 |
| F-11 | 命令行 | `pnpm add react-router-dom lucide-react` |

**验收**：`VITE_USE_MOCK=true pnpm --prefix app dev` 启动无报错，URL 自动跳转到 `/bootstrap`。

---

### Phase 1 — Bootstrap & Login（P1）

**任务**:

| # | 文件 | 内容 |
|---|------|------|
| P1-01 | `src/pages/BootstrapPage.tsx` | MySQL 表单；测试连接；按序 PUT→POST test→POST finish；完成跳登录 |
| P1-02 | `src/pages/LoginPage.tsx` | 双认证并发调用；任一失败留页；密码掩码；写 AuthContext |

**验收**：US1、US2 全部 Acceptance Scenarios 通过 mock 验证。

---

### Phase 2 — App Shell & 总览页（P1）

**任务**:

| # | 文件 | 内容 |
|---|------|------|
| P2-01 | `src/components/AppLayout.tsx` | 侧边栏导航；顶部右上角齿轮（进设置）；FR-014/015 导航显隐逻辑 |
| P2-02 | `src/components/ConnectionStatusBar.tsx` + `src/hooks/useConnectionHealth.ts` | 三个状态点；10 秒轮询 `GET /viewer/health`；绿色/蓝色/红色/灰色分别表示已连接/检测中/断开/未知 |
| P2-03 | `src/components/ModuleCard.tsx` | 状态 pill；PID/CPU/内存/运行时长；启动/停止/重启（确认框）/日志按钮 |
| P2-04 | `src/components/TrendChart.tsx` | 4 态组件；ready 状态使用 Recharts 渲染，支持 1/5/10 分钟切换 |
| P2-05 | `src/hooks/useModuleStatus.ts` | 3 秒轮询；页面隐藏时暂停 |
| P2-06 | `src/pages/OverviewPage.tsx` | 两张 ModuleCard + 两张 TrendChart；进程控制调用 |

**验收**：US3 全部 Acceptance Scenarios 通过（重启确认对话框、loading、状态更新）。

---

### Phase 3 — Collector & Writer 详情页（P2）

**任务**:

| # | 文件 | 内容 |
|---|------|------|
| P3-01 | `src/hooks/useWebSocketLog.ts` | WS 连接；5000行截断；5秒断线重连；wsStatus |
| P3-02 | `src/components/LogPanel.tsx` | 自动滚动开关；断线 Banner；级别 chip（DEBUG/INFO/WARN/ERROR 不同颜色）；scrollIntoView ref 导出 |
| P3-03 | `src/pages/CollectorPage.tsx` | 摘要卡 + 指标卡片行 + 采集任务表 + LogPanel；「打开日志」滚动聚焦 |
| P3-04 | `src/pages/WriterPage.tsx` | 摘要卡 + 指标卡片行 + 消费任务表 + LogPanel；「打开日志」滚动聚焦 |

**验收**：US4、US5 全部 Acceptance Scenarios 通过。

---

### Phase 4 — 设置页（P2）

**任务**:

| # | 文件 | 内容 |
|---|------|------|
| P4-01 | `src/pages/SettingsPage.tsx` | 内部标签导航（基础/数据库/Collector/Writer）；Esc 关闭；返回按钮 |
| P4-02 | SettingsPage 数据库标签 | 三行配置卡片；密码空白占位；测试按钮调用 `POST /admin/health/check`；状态初始灰色 |
| P4-03 | SettingsPage 保存逻辑 | PUT /admin/config；查模块状态；三种状态弹不同 Dialog（FR-013） |
| P4-04 | SettingsPage Collector/Writer 标签 | 可执行文件路径 + 工作目录；文件选择（Tauri dialog） |

**验收**：US6 全部 Acceptance Scenarios 通过。

---

## 实现顺序与依赖

```
Phase 0（基础设施，F-01~F-11）
   ↓ 全部完成才能继续
Phase 1.1（Bootstrap） → Phase 1.2（Login）   ← 串行，P1
   ↓
Phase 2（App Shell + 总览页）                  ← P1
   ↓
Phase 3.1（useWebSocketLog）
Phase 3.2（LogPanel）
   ↓（3.1+3.2完成后可并行）
Phase 3.3（CollectorPage）‖ Phase 3.4（WriterPage）   ← P2
   ↓
Phase 4（Settings）                            ← P2
```

每个 Phase 完成后执行：
1. `VITE_USE_MOCK=true pnpm --prefix app dev` 手动验收
2. `/speckit.git.commit` 提交当前阶段

---

## 未覆盖范围（明确排除）

| 排除项 | 原因 |
|--------|------|
| 趋势图长期历史持久化 | 当前只接入 app 运行期 module-rates 时间序列；持久化历史后续再做 |
| Backend refreshToken 自动续签 | MVP 不实现；401 统一跳登录 |
| Client Edition（远程只读） | 本 spec 不覆盖 |
| 自动重启策略 | 默认关闭 |
| 系统托盘完整功能（tray.rs） | Phase 5 再实现；MVP 提供空壳 |
| 自动化测试 | MVP 阶段不要求 |
