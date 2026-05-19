# Redis 实时态与历史态语义说明

> 更新时间：2026-05-19
> 作用：统一当前项目中 Redis 实时态、TDengine 历史态与对外接口语义
> 说明：2026-05-19 TDengine 压测后，本文件已修订为“Redis 承接实时短窗口，TDengine 承接历史落库”。

---

## 一、当前架构结论

当前项目统一按以下事实执行：

1. **MySQL**：负责配置与主数据，如设备、点位、分组、报警规则、用户权限等。
2. **Redis**：计划负责 100ms 实时监控所需的最新快照和最近 N 行短窗口。
3. **TDengine**：负责 500ms/1s 批量历史落库、历史查询和聚合统计。

对前端与后端的直接影响：

- 前端不直接访问任何数据库，只通过后端 REST / WebSocket 获取数据。
- 后端实时初始化与 WebSocket 优先基于 Redis 生成。
- 后端历史查询基于 MySQL + TDengine 生成。
- WebSocket 对前端的事件结构仍由后端统一归一化输出。

---

## 二、角色边界

| 角色 | 职责 |
|------|------|
| `@database` | 定义 MySQL 主数据结构、Redis 实时键语义、TDengine 宽表历史结构 |
| `@collector` | 负责采集设备数据，并写入 Redis 最新快照、短窗口与待入库 Stream |
| `@ingestion` | 负责从 Redis Stream 批量消费待入库数据，写入 TDengine，成功后 ACK |
| `@backend` | 负责查询 MySQL / Redis / TDengine，并向前端暴露 API / WebSocket |
| `@frontend` | 不直接访问数据库，只消费后端统一契约 |
| `@test` | 负责构造写入 MySQL / Redis / TDengine 的测试假数据与联调场景 |

---

## 三、统一规则

1. 时间字段统一使用毫秒精度时间戳或 `YYYY-MM-DD HH:mm:ss.SSS`，对外由后端统一格式化。
2. 状态字段统一使用稳定枚举，不允许同义词混用。
3. 前端不直接读取 Redis 或 TDengine 原始结构，只认后端转换后的 API / WebSocket 语义。
4. collector 可以使用内部缓存结构，但所有对前端的 WebSocket 事件都必须由后端统一归一化。
5. 若 Redis 中无最新值，后端可回退 TDengine 最近历史值或返回结构化空态，不能返回裸 `null`。
6. TDengine 当前统一采用“`device_data` -> `qc/asc/agv` 超级表 -> 设备子表 -> 点位列”宽表模型。
7. TDengine 历史宽表不保存 `quality`，异常/bad/离线等状态由 Redis 实时态、采集异常、规则引擎或告警事件承载。

---

## 四、实时态口径

### 4.1 实时数据来源

实时态由 Redis 提供，不再要求 TDengine 承受 100ms 小批量写入压力。

典型流程：

1. MySQL 提供设备、点位、分组等静态配置。
2. collector 周期采集设备点位。
3. collector 将当前设备最新值写入 Redis 最新快照。
4. collector 将最近 5 行、10 行或更多 100ms 帧写入 Redis 短窗口。
5. collector 同步追加一份完整帧到 Redis Stream，作为历史入库队列。
6. backend 从 Redis 读取快照/窗口，统一输出 `realtime-init` 和 WebSocket 事件。

### 4.2 推荐 Redis 键

首版建议：

```text
rt:latest:{device_code}
rt:window:{device_code}
td:stream:{device_type}
```

语义：

- `rt:latest:{device_code}`：设备最新快照，包含各点位最近值和时间。
- `rt:window:{device_code}`：设备最近 N 行短窗口，用于前端 100ms 监控与趋势图刷新。
- `td:stream:{device_type}`：待写入 TDengine 的可靠队列，按 `qc/asc/agv` 分流，入库模块写成功后 ACK。

窗口长度建议：

- 默认保留最近 10 行；
- 100ms 采集时对应约 1 秒窗口；
- 可按页面需求扩展到 50 行或 100 行，但必须设置长度上限和 TTL。

### 4.3 实时初始化语义

`GET /api/device-monitor/monitor/realtime-init` 应满足：

- 设备静态信息来自 MySQL；
- 点位最新值与更新时间优先来自 Redis；
- 若 Redis 缺值，可返回结构化空 `data: []`，或按配置回退 TDengine 最近历史值；
- 不直接暴露 Redis 键结构给前端。

### 4.4 WebSocket 语义

`WS /api/device-monitor/ws/realtime` 应满足：

- `snapshot`：后端按当前订阅设备读取 Redis 最新快照后统一输出；
- `update`：后端基于 Redis 最新帧或短窗口变化统一输出；
- `alarm`：后端基于规则判断或上游事件生成后统一输出；
- `error` / `pong`：由后端按连接与运行状态输出。

---

## 五、历史态口径

### 5.1 历史数据来源

历史查询直接来自 TDengine，不经过 Redis。

TDengine 当前模型：

```text
device_data -> qc/asc/agv -> device_code 子表 -> 点位列
```

`GET /api/device-monitor/history/query` 应满足：

- 设备与点位元数据来自 MySQL；
- 时序明细、时间序列结果来自 TDengine 宽表设备子表；
- 返回结构继续保持 `times + points[].values`；
- 无数据时返回空数组结构，不返回 `null`。

### 5.2 历史落库

collector 不建议每 100ms 小批量直接写 TDengine。

推荐：

- 默认：每 1000ms 写入每台设备 10 行；
- 可选：每 500ms 写入每台设备 5 行；
- 不推荐：每 100ms 写入每台设备 1 行。

### 5.3 聚合与统计

工作台统计、趋势与历史图表允许基于以下来源：

- TDengine 原始宽表时序数据；
- TDengine 连续聚合视图；
- MySQL 中的业务主数据与告警记录；
- Redis 仅用于当前实时短窗口，不作为历史统计事实源。

---

## 六、联调原则

1. Fake Data 只能作为测试数据来源，必须写入真实 MySQL / Redis / 入库模块 / TDengine 链路。
2. 不允许使用 mock API、mock WebSocket、前端本地 mock 数据绕过真实链路。
3. 若 Redis 缺值，后端必须返回结构化空态并保留页面可联调能力。
4. 若 TDengine 缺历史值，历史接口返回空序列，不影响实时页。
5. 若 MySQL 配置缺失，则视为主数据未就绪，应先补设备/点位 Seed。
6. Redis 的引入必须保留与 TDengine 批量落库的边界：Redis 负责短期实时态，TDengine 负责长期历史态。
7. collector 不直接写 TDengine；TDengine 入库由独立入库模块从 Redis Stream 批量消费完成。
