# Redis 缓冲层与 writer 模块设计方案

> **版本**: v1.0
> **日期**: 2026-05-19
> **背景**: TDengine 写入压测证明每 100ms 写入一次不可持续；500ms 批量写 5 行或 1s 批量写 10 行均可稳定运行。本文档描述引入 Redis 缓冲层和独立 writer 入库模块的完整改造方案。

---

## 一、问题与动机

### 1.1 原有架构的问题

```
collector 100ms 采集 → 直写 TDengine
```

- TDengine 每 100ms 接受一次写入，单设备点位多时压力过大
- 采集链路与存储链路耦合，TDengine 写入抖动会反向拖慢采集任务
- 无法支持前端 100ms 级实时刷新，因为 TDengine 写入延迟不稳定

### 1.2 压测结论

| 写入频率 | 批量大小 | 结论 |
|---|---|---|
| 100ms / 次 | 1~2 行 | ❌ 不可持续 |
| 500ms / 次 | 5 行 | ✅ 稳定 |
| 1s / 次 | 10 行 | ✅ 稳定 |

---

## 二、新架构总览

```
PLC 设备
   ↓ snap7 / GE SRTP / OPC
collector（100ms 采集，写 Redis）
   ├─ rt:{device_code}:latest   → Hash，覆盖式，最新点位快照
   ├─ rt:{device_code}:window   → List，定长滚动，最近 N 帧
   └─ rt:stream                 → Redis Stream，待入库队列

writer（独立进程，每 500ms/1s 消费 Stream）
   └─ 批量写入 TDengine 宽表历史

TurboAPI backend
   ├─ 实时接口  → 读 Redis latest / window
   └─ 历史接口  → 读 TDengine（不变）

前端
   ├─ 实时监控页 → 接口/WS 数据来源改为 Redis（接口格式不变）
   └─ 历史查询页 → 不变
```

### 三库职责划分（更新后）

| 数据库 | 职责 | 写入方 | 读取方 |
|---|---|---|---|
| MySQL | 设备配置、主数据、权限、报警、日志 | backend | backend / collector |
| Redis | 100ms 实时快照、最近 N 帧窗口、待入库 Stream | collector | backend（实时）/ writer（入库） |
| TDengine | 宽表历史时序（500ms/1s 批量落库） | writer | backend（历史） |

---

## 三、Redis Key 设计

### 3.1 latest — 设备实时快照

```
Key:   rt:{device_code}:latest
Type:  Hash
Field: {var_seq}
Value: JSON 字符串，包含 value 和 ts
TTL:   30s（collector 持续刷新时自动续期）
```

示例：

```
rt:qc100:latest
  "30"   → {"value": 1.0, "ts": "2026-05-19T13:00:00.100"}
  "11894"→ {"value": 5230.0, "ts": "2026-05-19T13:00:00.100"}
```

backend 读取方式：`HGETALL rt:{device_code}:latest`

### 3.2 window — 最近 N 帧短窗口

```
Key:   rt:{device_code}:window
Type:  List
Value: JSON 字符串，整帧数据（所有点位的一次采集结果）
长度:  保留最近 10 帧（LPUSH + LTRIM 0 9）
TTL:   60s
```

collector 写入：每次采集后 LPUSH 最新帧，LTRIM 保持不超过 10 条。

backend 读取方式：`LRANGE rt:{device_code}:window 0 -1`

### 3.3 rt:stream — 待入库队列

```
Key:    rt:stream
Type:   Redis Stream
Entry:  XADD rt:stream * device_code qc100 payload <JSON>
Group:  writer-group（writer 以 Consumer Group 方式消费）
```

JSON payload 字段：

```json
{
  "device_code": "qc100",
  "device_type_code": "QC",
  "ts": "2026-05-19T13:00:00.100",
  "points": {
    "30": 1.0,
    "11894": 5230.0,
    "11900": 3120.0
  }
}
```

写入成功后 writer 执行 XACK，写入失败不 ACK，依赖 Stream pending 机制自动重试。

---

## 四、collector 改造

### 4.1 改动范围

当前调用链：

```
DataAggregator → BatchWriter.write() → TDengine
```

改造后调用链：

```
DataAggregator → RedisWriter.write()
                    ├─ HSET  rt:{device_code}:latest
                    ├─ LPUSH + LTRIM  rt:{device_code}:window
                    └─ XADD  rt:stream
```

`BatchWriter`（TDengine 直写）在阶段 1 保留，双写过渡；阶段 3 移除。

### 4.2 新增模块

在 `collector/storage/` 目录下新增：

```
collector/storage/
├── redis_writer.py     # 新增：封装 latest / window / stream 写入
├── batch_writer.py     # 保留（阶段1双写，阶段3移除）
└── tdengine.py         # 不变
```

`RedisWriter` 职责：
- 接收 `DataPoint` 列表（数据结构不变）
- 批量写 `latest` Hash（HSET，多个 field 一次写）
- 写 `window` List（LPUSH + LTRIM）
- 追加 `rt:stream`（XADD）

### 4.3 Redis 连接配置

在 `collector/config/settings.toml` 中新增：

```toml
[redis]
host = "127.0.0.1"
port = 6379
db = 0
password = ""
```

---

## 五、writer 模块设计（新增独立进程）

### 5.1 定位

`writer` 是独立于 `collector` 和 `backend` 的第三个独立进程，职责单一：

> **从 Redis Stream 消费待入库数据 → 批量写入 TDengine**

### 5.2 目录结构

```
writer/
├── main.py             # 启动入口，初始化并运行消费循环
├── consumer.py         # Redis Stream Consumer Group 消费逻辑
├── batch_writer.py     # TDengine 批量写入（复用 collector 的逻辑，或共享包）
├── config/
│   └── settings.toml   # Redis / TDengine 连接配置
└── requirements.txt
```

### 5.3 消费流程

```
启动 → 创建/加入 Consumer Group（writer-group）
     ↓
循环（每 500ms）
     ↓
XREADGROUP rt:stream writer-group writer-1 COUNT 100
     ↓
反序列化 payload → 组装 DataPoint 列表
     ↓
BatchWriter.write(points)  → 批量写 TDengine
     ↓
写入成功 → XACK rt:stream writer-group {id}
写入失败 → 不 ACK，等待下次重试（消息留在 pending）
     ↓
XDEL 过期已 ACK 消息（定期清理）
```

### 5.4 关键参数

```toml
[writer]
flush_interval_ms = 500    # 消费循环间隔（ms）
batch_count = 100          # 每次最多消费条数
max_retry = 3              # 单条消息重试上限
pending_timeout_ms = 5000  # Pending 超时后重新投递
stream_max_len = 10000     # Stream 最大保留条数（MAXLEN）
```

### 5.5 与 collector 的代码复用

| 组件 | 共享方式 |
|---|---|
| `TDengineConnection` | 直接 import `collector/storage/tdengine.py`（或提取公共包） |
| `BatchWriter` | 同上 |
| `DataPoint` 数据结构 | 同上 |
| `settings.toml` 格式 | 各自独立配置文件，字段格式对齐 |

> 阶段 1 先直接 import，后续有需要再提取公共包。

---

## 六、backend 改造

### 6.1 改动范围

**实时接口**（改）：

| 方法 | 现在 | 改后 |
|---|---|---|
| `get_realtime_init()` | 逐点查 TDengine `LAST()` | 读 `rt:{device_code}:latest` Hash |
| `_fetch_device_latest_collect_time()` | 查 TDengine `LAST(ts)` | 读 latest Hash 中任意点位的 `ts` 字段 |
| `get_realtime_snapshots()` | 同上 | 同上 |

**历史接口**（不变）：`history.py` 继续直接查 TDengine。

**Dashboard**（不变）：聚合统计继续查 TDengine。

### 6.2 新增模块

在 `backend/app/db/` 下新增：

```
backend/app/db/
├── mysql.py       # 不变
├── tdengine.py    # 不变
└── redis.py       # 新增：aioredis 连接封装
```

`redis.py` 提供：
- `get_device_latest(device_code)` → 返回该设备所有点位最新值的 dict
- `get_device_window(device_code)` → 返回最近 N 帧列表

### 6.3 接口响应格式不变

`GET /api/device-monitor/monitor/realtime-init` 的响应结构保持不变，前端无感知。

---

## 七、前端改造

### 7.1 接口层（无需改动）

`realtime-init` 接口 URL 和响应 JSON 字段不变，前端无需修改。

### 7.2 WebSocket 层（待评估）

如果当前 WS 推送逻辑是后端定时查 TDengine 推送，则需要改为读 Redis latest / window 推送。

具体改动量待查看 `backend/app/api/routes/monitor.py` 后确认。

---

## 八、分阶段执行计划

### 阶段 1：collector 接入 Redis（双写过渡）

目标：
- collector 同时写 Redis 和 TDengine（保留旧逻辑）
- 验证 Redis key 结构和数据正确性

涉及文件：
- `collector/storage/redis_writer.py`（新增）
- `collector/main.py` 或聚合器出口（微调，增加 RedisWriter 调用）
- `collector/config/settings.toml`（新增 redis 配置段）

风险：低

### 阶段 2：writer 模块启动

目标：
- writer 独立进程运行，从 `rt:stream` 消费并批量写 TDengine
- 验证 TDengine 数据与原先直写路径一致

涉及文件：
- `writer/` 目录（全新）

风险：低（不影响已有服务）

### 阶段 3：backend 切换读 Redis + collector 停止直写 TDengine

目标：
- `MonitorService` 实时接口改为读 Redis
- collector 移除 `BatchWriter` 对 TDengine 的直写调用
- 前端实时页面验证数据正常

涉及文件：
- `backend/app/db/redis.py`（新增）
- `backend/app/services/monitor.py`（改造 3 个方法）
- `backend/main.py` 或 lifespan（新增 Redis 连接初始化）
- `collector/storage/batch_writer.py`（调用方移除或禁用直写）

风险：中（需联调，关注 Redis 数据格式与 backend 读取逻辑对齐）

---

## 九、关键约束与注意事项

1. **Redis TTL**：`latest` 设为 30s，`window` 设为 60s。collector 停止采集后，超时自动过期，backend 读到空值应返回 `connectStatus: offline`。

2. **Stream 长度控制**：`XADD rt:stream MAXLEN ~ 10000`，防止积压过多无法消费的消息撑爆内存。

3. **writer 重启恢复**：writer 使用 Consumer Group，重启后可从 pending 中重新消费未 ACK 的消息，不丢数据。

4. **TDengine 宽表模型不变**：writer 的写入目标仍是现有宽表结构，点位列名规则（`设备类型+var_seq`）不变。

5. **`quality` 字段**：TDengine 历史宽表已确认不保存 `quality`，Redis payload 中也不需要携带 quality。

---

## 十、变更记录

| 日期 | 版本 | 变更内容 |
|---|---|---|
| 2026-05-19 | v1.0 | 初始版本，基于压测结论设计 Redis 缓冲层与 writer 独立入库模块 |
