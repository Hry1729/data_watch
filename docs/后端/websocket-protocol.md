# WebSocket 通道协议说明

本篇文档详细阐述了后端的两个 WebSocket 通道（实时监控数据推送通道、Perspective 可视化数据通道）的工作机制与交互协议规范。

---

## 一、 实时监控推送通道 (`/ws/realtime-push`)
*源码参考：[monitor.py](file:///D:/python/device_alarm/data_watch/backend/app/api/routes/monitor.py#L90) (WebSocket 路由入口) | [ws_push.py](file:///D:/python/device_alarm/data_watch/backend/app/services/ws_push.py) (消息推送管理服务)*

该通道用于实现设备实时监控点位数据的低延迟推送，支持多设备多分组订阅、增量（Diff）推送与离线检测。

### 1. 握手与连接路由
- **WebSocket 完整路由**: `/api/device-monitor/ws/realtime-push`

### 2. JWT Token 认证方法
连接建立阶段，服务端对客户端身份与权限（必须包含 `device_monitor:monitor:view` 或 `device_monitor:realtime:view`）进行校验。认证支持以下三种方式（优先级从高到低）：
1. **URL Query 传参**:
   客户端握手时在 URL 中附加 Token 参数，如：`/api/device-monitor/ws/realtime-push?token=bearer+eyJhbGciOi...`
2. **HTTP Headers 传参**:
   在握手 HTTP 请求头部包含 `Authorization: Bearer <Token>`
3. **消息交互认证 (落后降级方式)**:
   若握手请求中未附带有效凭证，连接建立后，服务端将启动一个 **5.0 秒超时**的等待。客户端必须在 5 秒内向服务端发送首条 JSON 格式认证消息，例如：
   ```json
   {
     "type": "auth",
     "token": "bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
   }
   ```
   *注：若 5.0 秒内未收到该消息或认证失败，服务端将向客户端发送异常 JSON 后关闭连接：*
   ```json
   {
     "type": "error",
     "message": "认证超时",
     "code": 401
   }
   ```

### 3. 客户端订阅/取消消息格式
通道连通并完成认证后，客户端可以通过发送 JSON 格式消息进行控制：

- **订阅设备数据**:
  ```json
  {
    "action": "subscribe",
    "device_code": "dev_001",
    "group_code": "g001" // 可选，默认为 "g001"
  }
  ```
- **取消订阅设备数据**:
  ```json
  {
    "action": "unsubscribe",
    "device_code": "dev_001",
    "group_code": "g001" // 可选，默认为 "g001"
  }
  ```
- **心跳探测 (Ping)**:
  ```json
  {
    "action": "ping"
  }
  ```
  服务端接收后会向客户端响应纯文本格式内容：`"pong"`。

### 4. 服务端推送数据格式
服务端每 **0.1 秒**轮询一次 Redis 缓存 (`rt:latest:<device_code>:<group_code>`)。在不同场景下，服务端向客户端发送的 JSON 结构如下：

- **场景 A: 首次订阅成功 (快照推送 - snapshot)**:
  订阅建立后，服务端会立即读取当前 Redis 中的最新全量数据，并检查 TDengine 数据库连通性作为健康参考，向订阅连接发送快照：
  ```json
  {
    "type": "snapshot",
    "device_code": "dev_001",
    "group_code": "g001",
    "values": {
      "var_001": {
        "val": 45.2,
        "ts": 1680000000.0,
        "status": "good"
      },
      "var_002": {
        "val": 0.62,
        "ts": 1680000000.0,
        "status": "good"
      }
    },
    "tdengineConnected": true // TDengine 连通状况 (缓存 TTL 为 30 秒)
  }
  ```
- **场景 B: 设备离线通知 (offline)**:
  当服务端轮询 Redis 发现该设备分组数据完全清空时，广播设备离线状态：
  ```json
  {
    "type": "offline",
    "device_code": "dev_001"
  }
  ```
- **场景 C: 数据值变动增量推送 (diff)**:
  当服务端检测到最新读取的 Redis 数据项与上一周期快照有差异时（基于 `v != prev.get(k)` 比较），仅打包变动了的数据项进行差分推送：
  ```json
  {
    "type": "diff",
    "device_code": "dev_001",
    "group_code": "g001",
    "values": {
      "var_001": {
        "val": 46.1, // 新的变动值
        "ts": 1680000000.1,
        "status": "good"
      }
    }
  }
  ```

---

## 二、 Perspective 可视化通道 (`/ws/perspective`)
*源码参考：[perspective.py 路由](file:///D:/python/device_alarm/data_watch/backend/app/api/routes/perspective.py#L13) (WebSocket 路由入口) | [perspective.py 服务](file:///D:/python/device_alarm/data_watch/backend/app/services/perspective.py) (会话管理隔离核心逻辑)*

该通道服务于前端复杂图表/表格的多标签页协同与历史查询结果的实时缓存共享。

### 1. 握手与连接路由
- **WebSocket 完整路由**: `/api/device-monitor/ws/perspective` (从配置项 `settings.perspective_ws_route` 提取)
- **必需 Query 参数**:
  - `perspectiveSessionId` (str): 页面可视会话唯一标识，用于在多页面或多标签页中对齐会话
  - `connectionId` (str): 当前物理连接/实例唯一标识

### 2. JWT 认证与 Owner 识别
握手时服务端在连接头部中解析 `authorization`。
- 服务端会取其值（若空则为 `"anonymous"`）计算 SHA-256 哈希，截取前 24 位生成用户的 `owner_key`，作为隔离会话的标识。

### 3. Session 创建与恢复机制
客户端连接成功后，服务端调用 `attach_connection` 逻辑：
- **Session 恢复成功**:
  若 `perspectiveSessionId` 对应的 Session 在服务端中已存在（并未过期被清理），且当前未被其他活跃连接独占，则恢复该 Session，向客户端发送通知：
  ```json
  {
    "type": "session_restored",
    "sessionId": "session_uuid_xxx"
  }
  ```
  若该已恢复的会话中存有此前查询的历史数据记录数（`recordCount > 0`），服务端会立刻向客户端主动推送该数据快照（快照结构见下文）。
- **Session 冲突处理**:
  如果被恢复的 Session 关联的 `active_connection_id` 非空，且当前有其他的活跃物理连接（`connection_ids`），说明该可视会话已被另一个页面实例独占。服务端会响应冲突消息后关闭连接（状态码 `1008`）：
  ```json
  {
    "type": "session_conflict",
    "sessionId": "session_uuid_xxx",
    "message": "Perspective session already occupied by another active page instance"
  }
  ```
- **会话绑定**:
  连接无冲突时，服务端把该 WebSocket 传输管道通过 `bind_transport` 绑定到该会话的连接映射中，随后进入心跳循环。支持客户端发送 `{"action": "ping"}` 控制帧，回复 `{"type": "pong"}`。

### 4. 联动历史查询数据同步机制
此 WebSocket 并不单独承担大量历史数据的查询计算。其核心是与 **HTTP 历史数据同步查询接口 (`/api/device-monitor/history/query`)** 协同工作，逻辑如下：

1. **客户端发起 HTTP 查询**:
   客户端通过 `/api/device-monitor/history/query` 接口查询历史数据时，如果在 Request Headers 里带上了 `X-Perspective-Session-Id` 和 `X-Perspective-Query-Version`，将激活 Perspective 快照机制。
2. **安全与性能限制检查**:
   历史查询服务在处理带有 Perspective 标识的请求时，会强制执行硬性指标验证：
   - 点位数限制: `len(var_seqs)` 不得超过 `perspective_max_points_per_query`（默认最多 **20** 个点位）
   - 查询时间跨度: 不得超过 `perspective_max_range_hours`（默认最多 **24** 小时）
   - 记录数大小: 查询数据行总数不能超过 `perspective_max_records_per_query`（默认最多 **200,000** 条记录）
   *注：若超出限制，历史查询接口将抛出 400 ApiError 错误。*
3. **数据快照自动同步**:
   查询成功后，历史查询服务自动调用 Session Manager 的 `store_snapshot`，把所有历史记录存入该 Session 结构，并立刻通过其绑定的 WebSocket 活跃传输通道（通过 `publish_snapshot`）向前端推送包含全量历史记录的快照 JSON：
   ```json
   {
     "type": "snapshot",
     "sessionId": "session_uuid_xxx",
     "queryVersion": 1, // 查询版本号
     "recordCount": 1500, // 数据行数
     "records": [
       {
         "ts": "2026-05-23 15:00:00.000",
         "deviceName": "1#空压机",
         "deviceCode": "dev_001",
         "pointName": "采集温度",
         "varSeq": 1,
         "val": 45.2,
         "status": "good"
       }
     ],
     "timestamp": "2026-05-23 15:30:20"
   }
   ```

### 5. 并发与容量回收限制

为防范内存泄露，服务端针对 Perspective 的并发会话与周期生存时间进行了严格限制：

- **全局/Owner 并发会话限制**:
  - 全局最多共存会话数: `max_sessions_global`，默认 **100** 个。
  - 每个 Owner 最多共存会话数: `max_sessions_per_owner`，默认 **3** 个。
  *注：新建会话时若超出容量，会优先逐出（Evict）当前无活跃连接的空闲会话中，按时间线最久更新的（LRU）会话。若逐出后仍超限，拒绝连接（状态码 `1013`）。*
- **断开保留 TTL (`session_restore_ttl_seconds`)**:
  当检测到客户端 WebSocket 异常断开（页面刷新或网络抖动），会话中的活跃连接数清空。该 Session 与数据会暂时保存在内存中，默认保留 **300 秒 (5分钟)**。若客户端在此时间内未恢复重连，该 Session 将被标记为过期。
- **闲置回收 TTL (`session_idle_ttl_seconds`)**:
  对于全局任意无连接的会话，若其闲置时间超过 **900 秒 (15分钟)**，或者其超过断开保留 TTL (已达到 `retain_until` 时间线)，服务端会在下一次清理周期（每次 attach 或 prune 时触发）中将其物理销毁，释放内存。
