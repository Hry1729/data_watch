# Data Watch Console

Data Watch Console 是 DataWatch 的桌面运维与设备监控控制台，技术栈为 Tauri + React + TypeScript + Tailwind + shadcn/ui。

应用不直接拉起 collector / writer。进程管控、配置、健康检查与日志尾随由本机 DataWatch Agent 完成；业务数据、工作台、设备与系统管理走中心 Backend。

更完整的设计说明见：`docs/02-模块设计/app/README.md`。

## Runtime Dependencies

| Service | Default URL | Purpose |
|---|---|---|
| DataWatch Agent | `http://127.0.0.1:7070` | Bootstrap、Agent 登录、进程管控、配置、健康检查、日志 WebSocket |
| Backend | 运行期由 agent `runtime-config` 注入 | 业务 API、工作台、设备监控、系统管理、模块指标 |

Agent / Backend 的 HTTP 均经 Tauri Rust 代理（`agent_request` / `backend_request` / `backend_download`），不受 WebView CORS 限制。

## Local Development

`app/.env.development` 示例：

```env
VITE_USE_MOCK=false
VITE_BACKEND_URL=http://127.0.0.1:8000
```

启动顺序：

```powershell
# 1. 启动 Backend，确认 /api/auth/login 可用

# 2. 启动 Agent
cd D:\python\device_alarm\data_watch\agent
.\.venv\Scripts\python.exe main.py

# 3. 启动桌面端
cd D:\python\device_alarm\data_watch\app
pnpm tauri dev
```

Tauri 开发态 WebView 源一般为 `http://localhost:1420`；打包后的桌面应用不再使用该端口。

## Auth Flow

两步登录：

1. Agent `POST /auth/login` → `agentToken`
2. Backend `POST /api/auth/login` → `backendToken` / `backendRefreshToken`

两者都成功后，再拉取：

- `GET /api/user/info`（含 `roles`）
- 当前用户菜单树

并计算 `allowedPaths`（菜单映射 + 桌面专有页 + 管理员控制台），写入内存态 `AuthStore`（不落盘）。

`AuthContext.login()` 会调用 `setAgentAuthToken(agentToken)`，供 agent-client 在 hooks 未显式传 token 时带上 Bearer。

### 可见性规则（摘要）

- **桌面专有**：`/collection/collector`、`/collection/writer`、`/settings` 任意登录用户可见
- **管理员附加**：`R_SUPER` / `R_ADMIN` 额外可见 `/dashboard/console`
- 其余页面按后端菜单 component → 桌面 path 映射过滤

## Navigation Shell

- 左侧分组侧栏（可折叠，状态持久化）
- 顶栏：页面标题、刷新、连接状态、设置、主题
- 进入 **设置** 时，左侧导航替换为设置分类（基础 / 数据库 / Collector / Writer）；「返回」后还原业务导航

## WebSocket Logs

```text
ws://127.0.0.1:7070/viewer/logs/{collector|writer}/tail
```

打开后首帧：

```json
{ "type": "auth", "token": "<agentToken>" }
```

## Module Metrics and Charts

Collector / Writer 业务指标来自 Backend module-monitor；Agent 负责进程与 OS 级状态。图表支持 empty / loading / error / ready。

## Top Connection Health

顶栏 Redis / MySQL / TDengine 状态点每 10s 轮询 Agent `GET /viewer/health`。WebView 隐藏时暂停，可见时立即刷新。设置页「测试连接」走 `POST /admin/health/check`。

## Password Config Contract

- `GET /admin/config` 不得返回数据库密码明文
- `PUT /admin/config`：省略 `password` 表示保持原密码；非空则更新；空字符串非法
- 设置页仅在用户编辑密码字段时提交 password

## Common Issues

| Symptom | Likely Cause | Check |
|---|---|---|
| Agent 错误页 | Agent 未监听 `127.0.0.1:7070` | `Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status` |
| 登录后立刻退回登录 | 受保护 Agent API 401 | 确认登录后调用了 `setAgentAuthToken` |
| 超管没有「控制台」 | 未重新登录 / 角色非 R_SUPER\|R_ADMIN | 看 `user.roles` 与 `allowedPaths` |
| 设置顶栏找不到分类 | 已改为侧栏设置菜单 | 进入设置后看左侧「基础/数据库/…」 |

## Build

```powershell
cd D:\python\device_alarm\data_watch\app
pnpm exec tsc --noEmit
pnpm tauri build
```

产品名：`Data Watch Console`。
