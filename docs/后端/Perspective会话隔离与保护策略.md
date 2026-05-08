# Perspective 会话隔离与保护策略

> 文档状态：v1 设计冻结草案  
> 适用范围：`/api/device-monitor/history/*` 与 `WS /api/device-monitor/ws/perspective`  
> 部署前提：单机部署后端（单个 backend 进程）  
> 更新时间：2026-04-19

---

## 1. 文档目标

本文用于固定历史数据页接入 Perspective 后的后端隔离与保护策略，确保以下目标成立：

- 历史数据页采用“会话级隔离为主，连接级附着为辅”。
- 同一历史页标签刷新后可恢复原会话。
- 同账号多标签页互不共享 Table，避免相互覆盖或串数据。
- `GET /api/device-monitor/history/query` 现有响应契约保持不变。
- Perspective 仅作为历史查询结果的临时分析副本，不替代 TDengine。

---

## 2. 核心结论

### 2.1 Table 存放位置

- 服务端 `Perspective Table` 存放在部署服务器上的 `backend` Python 进程内存中。
- 客户端浏览器通过 WebSocket 同步一份本地副本，由 `<perspective-viewer>` 渲染。
- `Perspective Table` 不写入 MySQL，不写入 TDengine，也不作为持久化数据源。

### 2.2 数据源边界

- `TDengine` 是历史时序数据的唯一事实数据源。
- `Perspective Table` 只是后端查询 TDengine 后生成的临时分析副本。
- `backend` 重启后，所有内存中的 `Perspective Table` 都会丢失；这是可接受行为，因为它们本质上是可重建缓存。

### 2.3 当前隔离粒度

- 会话粒度采用“**每历史页标签独立**”。
- 同标签页刷新后，允许恢复原会话与原 Table。
- 同一登录账号打开多个历史页标签时，不共享同一张 Table。

---

## 3. 数据流与生命周期

### 3.1 固定数据路径

历史查询链路固定为：

```text
TDengine
  -> backend 查询结果
  -> backend 内存中的 Perspective Table
  -> WebSocket 同步
  -> 浏览器本地 Perspective 副本
  -> perspective-viewer 渲染
```

### 3.2 生命周期说明

1. 用户进入历史页。
2. 前端为当前标签页生成或恢复 `perspectiveSessionId`。
3. 前端建立 `WS /api/device-monitor/ws/perspective`，并附带当前 `perspectiveSessionId` 与新的 `connectionId`。
4. 后端按 `ownerKey + perspectiveSessionId` 查找或创建会话。
5. 用户发起 `GET /api/device-monitor/history/query` 查询。
6. 后端查询 TDengine，返回原有 HTTP 数据，并将结果写入当前会话对应的 `Perspective Table`。
7. 后端通过 WebSocket 推送 `snapshot` 到当前会话附着的连接。
8. 浏览器内的 Perspective Worker/Viewer 使用副本执行透视、筛选、排序和图表切换。

### 3.3 重启与清理

- `backend` 重启后，所有 Perspective 会话和内存 Table 一律视为失效。
- 用户重新连接时，如果找不到原会话，前端应创建新空会话，并提示用户重新查询。

---

## 4. 会话模型与连接附着关系

### 4.1 标识定义

| 标识 | 生成方 | 存储位置 | 用途 |
|---|---|---|---|
| `perspectiveSessionId` | 前端历史页 | `sessionStorage` | 标识当前标签页历史分析会话 |
| `connectionId` | 前端页面加载时 | 内存态 | 标识当前 WebSocket 连接 |
| `ownerKey` | 后端 | 服务端内存态 | 标识当前登录主体 |

### 4.2 会话主键

服务端 Perspective 会话所有权主键固定为：

```text
ownerKey + perspectiveSessionId
```

说明：

- `ownerKey` 默认取后端识别到的登录主体。
- 如果当前阶段仅能稳定获取 `Authorization`，则允许先对其做哈希后作为过渡 `ownerKey`。
- `perspectiveSessionId` 由前端在历史页进入时创建，并保存在 `sessionStorage`，因此同标签页刷新后仍可复用。

### 4.3 会话与连接关系

- 一个 Perspective 会话可以先后附着多个连接，但同一时刻仅允许一个活跃页面实例占用。
- 每次页面加载都会生成新的 `connectionId`，只用于当前连接附着，不参与持久会话归属。
- 刷新页面时，旧连接断开，新连接可以携带原 `perspectiveSessionId` 重新附着。
- 同账号不同历史页标签必须使用不同的 `perspectiveSessionId`，不得共享 Table。

### 4.4 浏览器复制标签处理

浏览器复制标签可能复制 `sessionStorage`，导致两个页面持有相同的 `perspectiveSessionId`。对此固定处理策略如下：

- 服务端检测到同一 `ownerKey + perspectiveSessionId` 被第二个活跃页面实例占用时，返回 `session_conflict`。
- 收到 `session_conflict` 的页面必须立即生成新的 `perspectiveSessionId` 并重连。
- 旧页面保留原会话，新页面切换为新的独立会话。

---

## 5. 对外接口补充规范

> 本节为补充规范，不改变现有历史查询响应结构。

### 5.1 HTTP：`GET /api/device-monitor/history/query`

保持现有返回结构：

```text
times + points[].values
```

在不改变响应契约的前提下，补充以下请求头：

| Header | 必填 | 说明 |
|---|---|---|
| `X-Perspective-Session-Id` | 是 | 当前标签页 Perspective 会话标识 |
| `X-Perspective-Query-Version` | 是 | 当前查询版本号，用于抑制旧查询结果覆盖新结果 |

处理要求：

- 后端完成 TDengine 查询后，仍按当前接口返回原始历史数据。
- 同时将查询结果写入当前会话对应的 `Perspective Table`。
- 只有当前会话的最新 `queryVersion` 允许落表并广播。

### 5.2 WebSocket：`WS /api/device-monitor/ws/perspective`

连接参数补充如下：

| 参数 | 必填 | 说明 |
|---|---|---|
| `perspectiveSessionId` | 是 | Perspective 会话标识 |
| `connectionId` | 是 | 当前 WebSocket 连接标识 |

### 5.3 WebSocket 事件类型

服务端事件固定为以下五类：

| 事件 | 说明 |
|---|---|
| `snapshot` | 当前会话数据快照 |
| `session_restored` | 连接成功恢复到已有会话 |
| `session_conflict` | 检测到同会话冲突，需要客户端换新会话 |
| `error` | 查询、同步或会话校验失败 |
| `pong` | 心跳响应 |

---

## 6. 默认保护策略矩阵

| 保护项 | 默认值 | 处理策略 | 置信度 |
|---|---|---|---|
| 最大时间范围 | `24h` | 超出直接拒绝查询，返回明确错误 | `0.98` |
| 单次最大点位数 | `20` | 超出直接拒绝查询 | `0.85` |
| 单次最大 Perspective 标准化记录数 | `200,000` | 超出拒绝同步，提示缩小时间范围或减少点位 | `0.78` |
| 单用户最大活跃历史会话数 | `3` | 超过后拒绝新建，或回收最老空闲会话 | `0.82` |
| 单机最大活跃 Perspective 会话数 | `100` | 超限先淘汰最老空闲会话，再拒绝新会话 | `0.80` |
| 空闲清理 TTL | `15 分钟` | 无查询且无连接则销毁会话与 Table | `0.92` |
| 断线保留时间 | `5 分钟` | 最后一个连接断开后保留会话，供刷新恢复 | `0.93` |
| 心跳策略 | `15s ping / 45s 超时` | 超时判定为失联并触发断开流程 | `0.90` |
| 会话归属校验 | 不允许跨 owner 复用 | 不同 owner 复用同一 `perspectiveSessionId` 时直接拒绝 | `0.96` |
| 查询版本抑制 | 仅最新 `queryVersion` 生效 | 旧查询结果到达后直接丢弃，不更新 Table | `0.95` |
| 复制标签冲突保护 | 开启 | 冲突时返回 `session_conflict`，新标签自动换会话 | `0.84` |
| 后端重启恢复策略 | 自动重建空会话 | 前端收到“会话不存在”后重建空会话，并要求用户重新查询 | `0.94` |

---

## 7. 保护策略说明

### 7.1 时间范围限制

- 历史查询默认最大允许跨度为 `24h`。
- 若查询跨度超过 `24h`，后端不执行 TDengine 查询，直接返回参数错误。
- 前端应提示用户缩小时间范围后重试。

### 7.2 点位数量限制

- 单次最多允许查询 `20` 个点位。
- 该限制同时用于控制 TDengine 查询压力和 Perspective 渲染负载。

### 7.3 大结果集保护

- 后端应先估算标准化记录数：

```text
标准化记录数 = 时间点数量 × 点位数量
```

- 超过 `200,000` 时，不进入 Perspective 同步流程。
- 可继续保留后续优化空间，例如分页历史表或异步导出，但本阶段默认直接拒绝。

### 7.4 会话容量控制

- 单个登录主体最多保留 `3` 个活跃历史会话。
- 单机部署场景下，整个 backend 最多保留 `100` 个活跃 Perspective 会话。
- 需要回收时，优先回收“最老且空闲”的会话，避免影响正在使用中的会话。

### 7.5 会话清理与恢复

- 连接全部断开后，不立即销毁会话，而是保留 `5 分钟`，便于同标签页刷新恢复。
- 若 `15 分钟` 内无连接且无查询，则视为彻底空闲，销毁其 Table 与元数据。

### 7.6 查询版本抑制

- 同一会话内，每次历史查询都应携带递增的 `queryVersion`。
- 后端只接受当前会话最新版本的查询结果写入 Table。
- 较旧请求即使后返回，也不得覆盖更新的查询结果。

---

## 8. 异常与恢复策略

### 8.1 会话恢复成功

触发条件：

- 同标签页刷新
- 原会话仍在 `5 分钟` 保留期内
- `ownerKey + perspectiveSessionId` 校验通过

处理结果：

- 服务端返回 `session_restored`
- 新连接附着到原会话
- 原 Table 可继续使用

### 8.2 会话冲突

触发条件：

- 服务端检测到同一 Perspective 会话已被另一个活跃页面实例占用

处理结果：

- 服务端返回 `session_conflict`
- 前端立即生成新的 `perspectiveSessionId`
- 前端重建新连接并进入新会话

### 8.3 会话不存在

触发条件：

- backend 重启
- 会话已过期被清理
- 客户端持有旧 `perspectiveSessionId`

处理结果：

- 服务端返回 `error`
- 前端创建空新会话
- 前端提示用户重新查询历史数据

---

## 9. 验收场景

以下场景应作为 Perspective 会话隔离保护策略的最低验收基线：

1. 同标签页刷新后，可恢复到原会话并收到 `session_restored`。
2. 同账号打开两个历史页标签，两个标签互不串数据。
3. 复制标签后，第二个标签能识别冲突并自动换会话。
4. 查询时间范围超过 `24h` 时被拒绝。
5. 点位数超过 `20` 时被拒绝。
6. 大结果集超过 `200,000` 标准化记录时被拒绝同步。
7. 断线后在 `5 分钟` 内刷新可恢复，超过 `5 分钟` 需重查。
8. `queryVersion` 较旧的查询结果不会覆盖较新的查询结果。
9. 空闲 `15 分钟` 后服务端会自动清理 Table。
10. 单用户超过 `3` 个会话、单机超过 `100` 会话时，保护策略按本文执行。

---

## 10. 当前默认前提

- 当前系统为**单机部署后端**，不考虑多实例共享内存或跨节点同步。
- 历史页当前仍保留 `GET /api/device-monitor/history/query` 作为查询入口，本轮只补充 Perspective 会话机制，不改原返回结构。
- 当前前端已具备登录态与 `Authorization`，可作为 `ownerKey` 绑定的基础来源。
- 本文中的“会话级隔离”定义为：**每个历史页标签独立会话，刷新可恢复，不跨标签共享**。

---

## 11. 后续文档联动建议

本文完成后，后续如进入实现阶段，建议再同步更新以下文档：

- `docs/设计方案/Perspective集成设计.md`
- `docs/后端/API接口文档.md`

本次先以本文作为后端 Perspective 会话隔离与保护策略的独立规范说明。
