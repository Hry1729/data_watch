# Data Watch Console

Data Watch Console is the desktop operations console for DataWatch. It is built with Tauri, React, TypeScript, Tailwind CSS, and shadcn/ui.

The app does not start collector or writer directly. Process control, configuration, health checks, and log tailing are handled by DataWatch Agent.

## Runtime Dependencies

Local development expects these services:

| Service | Default URL | Purpose |
|---|---|---|
| DataWatch Agent | `http://127.0.0.1:7070` | Bootstrap, Agent login, process control, config, health checks, log WebSocket |
| Backend | `http://127.0.0.1:8000` | Backend login and later business APIs |

Agent HTTP calls go through the Tauri Rust proxy command `agent_request`, so they are not affected by WebView CORS.

Backend calls currently use browser `fetch` directly from the WebView, so Backend must allow the Tauri dev origin.

## Local Development

Create or update `app/.env.development`:

```env
VITE_USE_MOCK=false
VITE_BACKEND_URL=http://127.0.0.1:8000
```

Then start services in this order:

```powershell
# 1. Start Backend separately and verify /api/auth/login is reachable.

# 2. Start Agent.
cd D:\python\device_alarm\data_watch\agent
.\.venv\Scripts\python.exe main.py

# 3. Start the desktop app.
cd D:\python\device_alarm\data_watch\app
pnpm tauri dev
```

In Tauri dev mode the WebView origin is usually `http://localhost:1420`. The packaged desktop app does not use port `1420`.

## Auth Flow

Login is a two-step flow:

1. `POST /auth/login` to Agent, returning `agentToken`.
2. `POST /api/auth/login` to Backend, returning `backendToken` and `backendRefreshToken`.

Both calls must succeed before entering the main app.

Tokens are stored in React memory only. They are not written to `localStorage`, `sessionStorage`, disk, or any config file.

After login, `AuthContext.login()` also calls `setAgentAuthToken(agentToken)`. This keeps the Agent token available to `agent-client.ts`, so protected Agent APIs such as `/viewer/processes/{name}/metrics` include the Bearer token even when page hooks call `agentClient` without passing a token explicitly. `logout()` clears this in-memory token.

## Backend CORS Requirement

Because Backend requests use WebView `fetch`, Backend must return CORS headers on both preflight and actual responses.

For local development, allow at least:

```text
http://localhost:1420
http://127.0.0.1:1420
```

If Backend logs show:

```text
OPTIONS /api/auth/login 204
POST /api/auth/login 200
```

but the app still displays "后端服务不可达", the most likely cause is that the actual `POST` response is missing `Access-Control-Allow-Origin`. PowerShell requests do not enforce CORS, so always verify with the WebView or a browser request that includes an `Origin` header.

For TurboAPI/custom ASGI dispatch, make sure CORS headers are added by the custom response path too, not only by framework middleware.

## WebSocket Logs

Log tailing connects directly to:

```text
ws://127.0.0.1:7070/viewer/logs/{collector|writer}/tail
```

The token is not placed in the URL. After `open`, the client immediately sends the first frame:

```json
{ "type": "auth", "token": "<agentToken>" }
```

## Module Metrics and Charts

Collector / Writer business metrics are fetched from Backend module-monitor APIs. Agent remains responsible only for process and OS-level status.

The chart cards use Recharts and support `empty`, `loading`, `error`, and `ready` states. In the ready state, the current value is shown beside the title, and the chart can switch between 1 minute, 5 minutes, and 10 minutes.

## Top Connection Health

The top-right Redis / MySQL / TDengine status dots poll Agent every 10 seconds:

```text
GET /viewer/health
```

The status colors are:

| Color | Status |
|---|---|
| Green | connected |
| Blue | checking |
| Red | disconnected |
| Gray | unknown |

Polling pauses while the WebView is hidden and refreshes immediately when it becomes visible again. The settings page "测试" buttons still use `POST /admin/health/check` for one-off checks.

## Password Config Contract

`GET /admin/config` must not return database password fields.

`PUT /admin/config` uses partial update semantics for passwords:

- no `password` key: keep the existing password
- non-empty `password`: update the password
- empty string password is invalid

The settings page only sends a password when the user edits that field.

## Common Issues

| Symptom | Likely Cause | Check |
|---|---|---|
| Agent error page | Agent is not running or not listening on `127.0.0.1:7070` | `Invoke-RestMethod http://127.0.0.1:7070/bootstrap/status` |
| Backend unreachable after Backend logs `POST 200` | Missing CORS headers on actual Backend response | Check `Access-Control-Allow-Origin` on the `POST` response |
| Login succeeds then returns to login page | Protected Agent API returned 401 | Confirm `setAgentAuthToken(agentToken)` is called after login and Agent JWT is valid |
| Correct password works in PowerShell but not in app | Backend URL or CORS mismatch | Check `VITE_BACKEND_URL` and allowed origins |

## Build

```powershell
cd D:\python\device_alarm\data_watch\app
pnpm exec tsc --noEmit
pnpm tauri build
```

The product name is `Data Watch Console`.
