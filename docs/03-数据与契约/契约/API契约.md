# API 契约

> 本文汇总平台对外的 HTTP 接口契约：backend 的业务 REST（供 Web 前端与桌面端）与 agent 的本机管控 REST（供桌面端）。本文定义通用包络、鉴权、分页、错误语义、接口分组与变更门禁。

---

## 1. 契约边界

```mermaid
flowchart LR
    FE[frontend Vue Web] -->|/api/* REST| BE[backend]
    FE -->|/api/device-monitor/ws/*| BE
    APP[app Tauri] -->|backend_request| BE
    APP -->|agent_request| AG[agent 127.0.0.1:7070]
    BE --> RDS[(Redis)]
    BE --> TD[(TDengine)]
    BE --> MY[(MySQL)]
    AG --> COL[collector]
    AG --> WR[writer]
```

- frontend 只调用 backend，不直接访问 agent、Redis、TDengine、MySQL。
- app 同时调用 backend 与 agent。
- agent 只监听本机，服务桌面端与边缘进程管控。
- backend 是业务 API 事实源，前端类型和 mock 不能反向定义接口契约。

---

## 2. backend 通用约定

### 2.1 基址

```text
/api
```

部署方式：

- 开发：frontend 通过 Vite proxy 或 `VITE_API_URL` 指向 backend；
- 镜像：frontend nginx 将 `/api/` 反代到 backend:8000；
- app：从 agent `runtime-config` 读取 backend base_url 后经 Rust `backend_request` 调用。

### 2.2 响应包络

普通 JSON API 返回：

```json
{
  "code": 0,
  "msg": "success",
  "data": {}
}
```

约定：

- 客户端业务数据取 `data`。
- 列表接口 `data` 中包含 `list` 与 `total`，或按接口语义返回数组。
- 文件导出/下载接口可以不走 JSON 包络，按 Blob/文件流处理。

### 2.3 鉴权

```http
Authorization: Bearer <token>
```

注意：backend 登录返回的 token 字符串已经带 `Bearer ` 前缀，调用端只做规范化，不重复拼接。

### 2.4 状态码与错误语义

| HTTP | 语义 | 客户端处理 |
|------|------|------------|
| 200 | 请求成功，业务结果看 `code` | 正常渲染 |
| 400 | 参数错误/业务校验失败 | 展示错误信息，保留用户输入 |
| 401 | 未登录、token 失效或签名错误 | 清理敏感内存态，跳登录 |
| 403 | 已登录但权限不足 | 显示无权限，不重复请求 |
| 404 | 路由或资源不存在 | 显示空态/不存在 |
| 409 | 状态冲突 | 提示刷新或重新操作 |
| 500 | 服务端异常 | 展示错误，保留排障 trace/log |
| 503 | 依赖不可用/降级 | 显示 degraded，不伪装成空数据 |

### 2.5 分页

请求参数：

```text
pageNo=1&pageSize=20
```

兼容旧字段时可接受 `current/size`，但新文档和新页面统一使用 `pageNo/pageSize`。

响应示例：

```json
{
  "code": 0,
  "msg": "success",
  "data": {
    "list": [],
    "total": 0,
    "pageNo": 1,
    "pageSize": 20
  }
}
```

---

## 3. backend 接口分组

### 3.1 鉴权与账户

| 方法 | 路径 | 用途 | 主要调用方 |
|------|------|------|------------|
| POST | `/api/auth/login` | 登录，返回 token / refreshToken / 用户信息 | frontend/app |
| POST | `/api/auth/logout` | 登出 | frontend |
| GET | `/api/user/info` | 当前用户信息、角色、权限、按钮标记 | frontend |
| POST | `/api/user/password/change` | 修改密码 | frontend |

登录响应示例：

```json
{
  "code": 0,
  "msg": "success",
  "data": {
    "token": "Bearer eyJ...",
    "refreshToken": "Bearer eyJ...",
    "userInfo": {
      "id": 1,
      "userName": "Super",
      "roles": ["R_SUPER"],
      "buttons": ["device_monitor:realtime:view"]
    }
  }
}
```

### 3.2 系统管理（用户/角色/菜单）

| 方法 | 路径 | 用途 |
|------|------|------|
| GET/POST | `/api/user` | 用户列表/新增 |
| GET/PUT/DELETE | `/api/user/{id}` | 用户详情/更新/删除 |
| PATCH | `/api/user/{id}/status` | 启停用户 |
| PATCH | `/api/user/{id}/password/reset` | 重置密码 |
| GET/POST | `/api/role` | 角色列表/新增 |
| GET/PUT/DELETE | `/api/role/{id}` | 角色详情/更新/删除 |
| PATCH | `/api/role/{id}/status` | 启停角色 |
| GET/POST | `/api/v3/system/menu` | 菜单树/新增菜单 |
| PUT/DELETE | `/api/v3/system/menu/{id}` | 更新/删除菜单 |
| POST | `/api/v3/system/menu/{id}/permissions` | 维护菜单权限 |

字段注意：角色编码字段为 `role_code`。

### 3.3 设备监控域 `/api/device-monitor`

#### 仪表盘

| 路径 | 用途 |
|------|------|
| `/dashboard/overview` | 概览统计 |
| `/dashboard/device-type-distribution` | 设备类型分布 |
| `/dashboard/recent-events` | 最近事件 |
| `/dashboard/trend` | 趋势 |
| `/dashboard/device-connection-health` | 连接健康 |
| `/dashboard/alarm-distribution` | 告警分布 |

#### 设备 / 点位 / 驱动

| 路径 | 用途 |
|------|------|
| `/driver/list`、`/driver/options` | 驱动与协议选项 |
| `/device/list`、`/device/{id}`、`/device`、`/device/batch` | 设备查询/详情/CRUD/批量操作 |
| `/device/import`、`/device/export`、`/device/group-tree` | 设备导入导出/树 |
| `/point/list`、`/point/{id}`、`/point`、`/point/batch` | 点位查询/详情/CRUD/批量操作 |
| `/point/import`、`/point/import/template`、`/point/export`、`/point/device-tree` | 点位导入导出/树 |

#### 实时监控

| 路径 | 用途 |
|------|------|
| `/monitor/device-tree` | 监控设备树 |
| `/monitor/realtime-init` | 实时初始快照 |
| `/realtime-window` | 短窗口历史 |
| `/device/{device_code}/point-groups` | 某设备点位分组 |

#### 历史数据

| 路径 | 用途 |
|------|------|
| `/history/devices`、`/history/device-types`、`/history/points` | 历史筛选元数据 |
| `/history/query` | 多点对齐原始表 |
| `/history/chart` | 降采样图表 |
| `/history/raw` | 分页原始 |
| `/history/raw-matrix` | 时间桶矩阵 |
| `/history/export`、`/history/export/{id}/status`、`/history/export/{id}/download` | 异步导出 |

历史查询请求示例：

```json
{
  "deviceCodes": ["QC100"],
  "varSeqs": [1, 2, 3],
  "startTime": "2026-06-25 10:00:00",
  "endTime": "2026-06-25 11:00:00",
  "interval": "auto",
  "aggregation": "avg",
  "perspectiveSessionId": "optional-session",
  "perspectiveQueryVersion": 3
}
```

#### 系统日志

| 路径 | 用途 |
|------|------|
| `/log/list`、`/log/stats` | 查询/统计 |
| `/log/policy`（GET/PUT） | 保留策略 |
| `/log/debug` | DEBUG 落库开关 |
| `/log/cleanup` | 手动清理 |
| `/log/client` | 前端日志上报（限流，免鉴权） |
| `/log/tdengine/files`、`/log/tdengine/tail` | TDengine 服务日志查看 |

#### 模块监控（桌面端消费）

| 路径 | 用途 |
|------|------|
| `/module-monitor/collector` | collector 数据链路指标 |
| `/module-monitor/writer` | writer 队列、pending、DLQ、入库指标 |
| `/module-monitor/rates` | 采集/入库速率趋势 |

---

## 4. agent 接口（127.0.0.1:7070）

agent 是本机边缘控制面，供 app 调用。

| 层 | 方法/路径 | 用途 |
|----|-----------|------|
| bootstrap | GET `/bootstrap/status` | 查询是否处于首装状态 |
| bootstrap | GET `/bootstrap/runtime-config` | 获取 backend base_url 等运行配置 |
| bootstrap | PUT `/bootstrap/config` | 写入 DB/backend 配置，密码 DPAPI 加密 |
| bootstrap | POST `/bootstrap/test-connection` | 测试 MySQL/Redis/TDengine 连通性 |
| bootstrap | POST `/bootstrap/finish` | 完成首装 |
| auth | POST `/auth/login` | 登录获取 agent token |
| viewer | GET `/viewer/status` | agent 总状态 |
| viewer | GET `/viewer/health` | 健康检查 |
| viewer | GET `/viewer/processes/{name}/status` | 进程状态 |
| viewer | GET `/viewer/processes/{name}/metrics` | 进程指标 |
| viewer | GET `/viewer/logs/{name}/tail` | 日志 tail |
| admin | POST `/admin/processes/{name}/start` | 启动进程 |
| admin | POST `/admin/processes/{name}/stop` | 停止进程 |
| admin | POST `/admin/processes/{name}/restart` | 重启进程 |
| admin | GET/PUT `/admin/config` | 读取/修改配置 |
| admin | POST `/admin/health/check` | 主动健康检查 |

`{name}` 当前为 `collector` 或 `writer`。

admin 接口是写操作，不应被前端隐藏按钮替代权限校验。

---

## 5. WebSocket

backend WebSocket 不在本文件展开，见 [WebSocket 协议](WebSocket协议.md)。核心端点：

- `/api/device-monitor/ws/realtime-push`：实时监控 snapshot/diff/offline；
- Perspective WebSocket：历史查询快照。

---

## 6. 接口变更门禁

任何影响前后端契约的变更，按以下顺序执行：

1. 先更新本文或 [WebSocket 协议](WebSocket协议.md)。
2. 标明兼容性：新增字段、字段改名、结构变化、错误语义变化。
3. backend 路由/service 更新。
4. frontend/app client 与页面状态更新。
5. 权限相关变更同步菜单/权限种子。
6. 示例请求/响应同步更新。
7. 验证真实服务，不以 mock 通过作为完成标准。

禁止：

- 前端 mock 或 TypeScript 类型先行成为事实源；
- 文档中保留已废弃路径但不标注 legacy；
- token 重复拼接 `Bearer `；
- 生产示例中出现真实密码。

---

## 相关文档

- [后端-backend](../02-模块设计/backend/README.md)
- [backend 服务分层与实时历史通道](../02-模块设计/backend/服务分层与实时历史通道.md)
- [边缘代理-agent](../02-模块设计/agent/README.md)
- [agent 边缘进程编排与凭据](../02-模块设计/agent/边缘进程编排与凭据.md)
- [WebSocket 协议](WebSocket协议.md)
