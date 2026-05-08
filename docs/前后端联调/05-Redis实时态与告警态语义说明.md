# 实时态与历史态语义说明（Redis 暂不使用）

> 更新时间：2026-04-13
> 作用：统一当前项目中实时态、历史态与对外接口语义
> 说明：本文件路径沿用历史命名；当前实际架构中 Redis 暂不使用

---

## 一、当前架构结论

当前项目统一按以下事实执行：

1. **MySQL**：负责配置与主数据，如设备、点位、分组、报警规则、用户权限等。
2. **TDengine**：负责实时 + 历史时序数据。
3. **Redis**：当前阶段**暂不使用**，不作为运行时依赖，不参与联调闭环。

对前端与后端的直接影响：

- 前端不直接访问任何数据库，只通过后端 REST / WebSocket 获取数据。
- 后端实时初始化与历史查询都应基于 **MySQL + TDengine** 组合生成。
- WebSocket 对前端的事件结构仍由后端统一归一化输出。

---

## 二、角色边界

| 角色 | 职责 |
|------|------|
| `@database` | 定义 MySQL 主数据结构、TDengine 超级表/子表/查询口径 |
| `@collector` | 负责将采集值或测试假数据写入 TDengine |
| `@backend` | 负责查询 MySQL / TDengine，并向前端暴露 API / WebSocket |
| `@frontend` | 不直接访问数据库，只消费后端统一契约 |
| `@test` | 负责构造写入 MySQL / TDengine 的测试假数据与联调场景 |

---

## 三、统一规则

1. 时间字段统一使用 `YYYY-MM-DD HH:mm:ss`。
2. 状态字段统一使用稳定枚举，不允许同义词混用。
3. 前端不直接读取 TDengine 原始结构，只认后端转换后的 API / WebSocket 语义。
4. collector 或 Fake Data 可以使用内部写入结构，但所有对前端的 WebSocket 事件都必须由后端统一归一化。
5. 若 TDengine 中无最新值，后端必须返回结构化空态，而不是 `null`。
6. Redis 相关旧设计仅视为历史资料，当前联调与实现不得再以 Redis 为前提。
7. TDengine 当前统一采用“设备类型数据库 -> 设备超级表 -> 变量子表”模型，后端与采集实现必须基于该模型设计查询与写入逻辑。

---

## 四、实时态口径

### 4.1 实时数据来源

当前实时态不再单独缓存于 Redis，而是直接由 TDengine 中的最新样本提供。

典型流程：

1. MySQL 提供设备、点位、分组等静态配置。
2. collector 将采集值或测试假数据写入 TDengine。
3. backend 按设备类型定位数据库、按设备定位超级表、按点位定位变量子表，查询每个点位的最新记录并组装实时初始化响应。
4. backend 基于最新值变化或轮询结果，向前端输出 `snapshot / update / alarm / error / pong` 事件。

### 4.2 实时初始化语义

`GET /api/device-monitor/monitor/realtime-init` 应满足：

- 设备静态信息来自 MySQL；
- 点位最新值与更新时间来自 TDengine 变量子表；
- 若某设备暂无实时值，返回结构化空 `data: []`；
- 不以 Redis 是否存在作为前提条件。

### 4.3 WebSocket 语义

`WS /api/device-monitor/ws/realtime` 应满足：

- `snapshot`：后端按当前订阅设备查询 TDengine 最新值后统一输出；
- `update`：后端检测到点位新值后统一输出；
- `alarm`：后端基于规则判断或上游事件生成后统一输出；
- `error` / `pong`：由后端按连接与运行状态输出。

---

## 五、历史态口径

### 5.1 历史数据来源

历史查询直接来自 TDengine，不再经过 Redis 中转。

`GET /api/device-monitor/history/query` 应满足：

- 设备与点位元数据来自 MySQL；
- 时序明细、时间序列结果来自 TDengine 变量子表，必要时由后端聚合多表结果；
- 返回结构继续保持 `times + points[].values`；
- 无数据时返回空数组结构，不返回 `null`。

### 5.2 聚合与统计

工作台统计、趋势与历史图表允许基于以下来源：

- TDengine 原始时序表；
- TDengine 连续聚合视图；
- MySQL 中的业务主数据与告警记录。

---

## 六、联调原则

1. Fake Data 只能作为测试数据来源，必须写入真实 MySQL / TDengine。
2. 不允许使用 mock API、mock WebSocket、前端本地 mock 数据绕过真实链路。
3. 若 TDengine 缺值，后端必须返回结构化空态并保留页面可联调能力。
4. 若 MySQL 配置缺失，则视为主数据未就绪，应先补设备/点位 Seed。
5. Redis 暂不使用；若未来重新引入，必须先修订本文件、TODO 与相关设计文档后再实施。
