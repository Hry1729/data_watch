# Redis 实时缓冲层与点位分组设计方案 v0.6

> 适用项目：自动化码头设备数据采集、实时监控与 TDengine 历史入库  
> 当前规模：305 台设备，每台设备约 1000 个点位  
> 扩展目标：支持单设备 5000、10000 甚至更多点位  
> 核心策略：MySQL 固化点位分组；Redis 负责实时数据；TDengine 负责历史数据；每 3072 点一个 TDengine 分组  
> 本版调整（v0.5 → v0.6）：window key 增加 scan_rate_ms 区分不同采集周期、补充质量码 q 写入规则、明确 collector 写 Stream 时过滤 STRING/is_storage=0 点位、新增死信队列（DLQ）规范（16.8 节）、补充 Consumer Group 初始化步骤、新增 MySQL 事务原子性要求、新增 WebSocket 推送机制说明（17.6 节）、新增 Redis AOF 持久化配置要求、明确子表不存在时不得 XACK（14.5.4 节）、Stream values 明确为 JSON 字符串并补充 XADD 示例、新增点位必须 SELECT FOR UPDATE 锁定分组行（10.1 节）、WebSocket 按 device+group 服务端合并订阅约束（17.6.1 节）

---

## 一、方案目标

本方案用于解决高频设备数据采集场景下，Redis 实时缓存、TDengine 批量入库、MySQL 配置管理之间的协同问题。

当前系统计划采用以下职责划分：

| 组件 | 职责 |
|---|---|
| MySQL | 设备、点位、点位分组、采集配置、列映射等主数据 |
| Redis | 最新值、短窗口、待入库 Stream 队列 |
| TDengine | 历史时序数据 |
| collector | 采集设备数据，写 Redis |
| td_writer | 从 Redis Stream 消费数据，批量写 TDengine |
| TurboAPI | 读取 MySQL / Redis / TDengine，对前端提供接口 |
| 前端 | 实时监控读取 Redis 链路，历史查询读取 TDengine 链路 |

Redis 不作为长期历史数据库使用，只承担实时态与入库缓冲职责。长期历史数据统一进入 TDengine。

---

## 二、修正后的数据流程图

### 2.1 总体数据流

```text
PLC / OPC / 其他设备数据源
        ↓
数据采集服务 collector
        ↓
Redis 实时缓冲层
        ├── latest：最新值缓存
        ├── window：短窗口缓存
        └── stream：待入库队列
        │
        ├──────────────→ TurboAPI / WebSocket
        │                        ↓
        │                 前端实时监控页面
        │
        └──────────────→ TDengine 入库模块 td_writer
                                 ↓
                          TDengine 历史时序库
                                 ↓
                          TurboAPI 历史查询接口
                                 ↓
                          前端历史曲线 / 报表 / 分析页面
```

### 2.2 实时数据链路

实时页面不直接从 TDengine 取最新值，而是从 Redis 取。

```text
collector
   ↓
Redis latest / window
   ↓
TurboAPI / WebSocket
   ↓
前端实时监控
```

实时链路使用 Redis 的原因：

1. 支持 100ms 级别刷新。
2. 避免前端实时页面频繁查询 TDengine。
3. TDengine 写入抖动不会影响前端实时展示。
4. 前端只订阅当前页面需要的设备和点位分组。

### 2.3 历史数据链路

历史查询仍然从 TDengine 获取。

```text
collector
   ↓
Redis Stream
   ↓
td_writer 批量消费
   ↓
TDengine
   ↓
TurboAPI 历史查询接口
   ↓
前端历史曲线 / 趋势分析
```

---

## 三、设备命名规则

设备编码参考现有设备表和导入表，统一使用小写英文前缀 + 编号。

| 设备类型 | 设备编码前缀 | device_code 示例 | device_name 示例 |
|---|---|---|---|
| 桥吊 | qc | qc100 | 桥吊100 |
| 轨道吊 | asc | asc7011 | 轨道吊701L |
| AGV | agv | agv801 | AGV801 |

设备编码作为系统内部稳定编码，建议后续不随意修改。

前端显示名称使用 `device_name`。

---

## 四、现有 MySQL 表结构分析

本次优化原则是：

```text
保留现有 device 和 device_variable 主体结构，尽量不重命名、不推翻已有字段，只补充分组必须字段和分组表。
```

### 4.1 现有 device 表字段

当前 `device` 表已有字段如下：

| 字段名 | 说明 |
|---|---|
| id | 设备主键 |
| device_code | 设备编码，如 qc100、asc7011、agv801 |
| device_name | 设备名称 |
| type_id | 设备类型 ID |
| template_id | 模板 ID |
| group_id | 设备所属区域或业务分组 ID |
| enable | 是否启用 |
| status | 设备状态 |
| protocol_id | 协议 ID |
| endpoint | 主连接地址 |
| endpoint_bkp | 备用连接地址 |
| rack | PLC rack |
| slot | PLC slot |
| scan_cycle | 默认扫描周期 |
| template_last_sync_version | 模板最近同步版本 |
| template_sync_time | 模板同步时间 |
| create_user | 创建人 |
| create_time | 创建时间 |
| update_user | 更新人 |
| update_time | 更新时间 |

### 4.2 现有 device_variable 表字段

当前 `device_variable` 表已有字段如下：

| 字段名 | 说明 |
|---|---|
| id | 点位主键 |
| device_id | 所属设备 ID |
| var_seq | 点位序号 |
| var_name | 点位名称 |
| io_type | IO 类型，如 I / Q / M / DB 等 |
| var_type | 点位数据类型，如 BOOL、INT、REAL、STRING 等 |
| var_address | 点位地址 |
| var_offset | 地址偏移 |
| var_bit | 位偏移 |
| unit_id | 单位 ID |
| description | 点位描述 |
| scan_rate | 采集周期 |
| is_alarm | 是否报警点位 |
| is_storage | 是否入库 |
| create_user | 创建人 |
| create_time | 创建时间 |
| update_user | 更新人 |
| update_time | 更新时间 |

### 4.3 本次优化原则

本次不建议把现有字段全部改成新字段，例如：

```text
不强制把 enable 改成 enabled
不强制把 scan_rate 改成 period_ms
不强制把 var_address 改成 address
不强制把 var_type 改成 data_type
不强制把 create_time 改成 created_at
```

原因是已有系统、页面、接口、导入逻辑可能已经依赖这些字段。

因此本方案采用"保留原字段 + 少量新增字段 + 新增分组表"的方式。

---

## 五、推荐的最小改动方案

本次建议做三类调整：

```text
1. 新增 device_point_group 表
2. device_variable 表新增少量分组映射字段
3. device 表可选增加设备级配置版本字段
```

其中第 1、2 项为建议必须项，第 3 项为可选优化项。

---

## 六、新增设备点位分组表 device_point_group

### 6.1 表用途

`device_point_group` 用于记录每台设备有哪些点位组，以及每个组当前点位和列位使用情况。

它解决以下问题：

1. 每台设备每 3072 个点位自动生成一个新组。
2. 记录每个组当前启用点位数量。
3. 记录每个组停用、软删除点位数量。
4. 记录每个组已经使用过多少 TDengine 列位。
5. 点位新增、删除、启停时同步维护分组数量。
6. 采集模块、后端、前端、入库模块都可以根据该表找到设备点位分组情况。

### 6.2 表名

```text
device_point_group
```

### 6.3 推荐字段

| 字段名 | 说明 | 是否必须 | 示例 |
|---|---|---|---|
| id | 分组主键 | 是 | 1 |
| device_id | 设备 ID | 是 | 1 |
| group_no | 分组序号 | 是 | 1 |
| group_code | 分组编码 | 是 | g001 |
| group_name | 分组名称 | 否 | 第1组 |
| max_point_count | 每组最大点位数 | 是 | 3072 |
| active_point_count | 当前启用点位数 | 是 | 1000 |
| disabled_point_count | 当前停用点位数 | 是 | 0 |
| deleted_point_count | 当前软删除点位数 | 是 | 0 |
| used_column_count | 已使用过的 TDengine 列位数，只增不减 | 是 | 1000 |
| next_column_index | 下一个可分配列位序号 | 是 | 1001 |
| status | 分组状态 | 是 | active / full / disabled |
| config_version | 分组配置版本 | 是 | 1 |
| create_user | 创建人 | 否 | admin |
| create_time | 创建时间 | 否 | - |
| update_user | 更新人 | 否 | admin |
| update_time | 更新时间 | 否 | - |

### 6.4 计数字段不变式

为方便后续校验数据一致性，分组计数字段应满足以下不变式：

```text
active_point_count + disabled_point_count + deleted_point_count <= used_column_count
```

说明：

1. `used_column_count` 只增不减，表示历史上已经分配过的列位数量。
2. 点位软删除后，原列位不回收，因此三类点位数量之和可能小于 `used_column_count`。
3. `next_column_index = used_column_count + 1`，除非经过离线迁移任务重建映射。
4. 日常增删改查不允许复用历史列位。

### 6.5 不建议冗余存储的字段

以下字段可以通过 `device.device_code`、设备类型和 `group_code` 规则自动推导，不一定要存在数据库中：

| 可推导内容 | 推导规则 |
|---|---|
| Redis latest key | `rt:latest:{device_code}:{group_code}` |
| Redis window key | `rt:window:{device_code}:{group_code}:{scan_rate_ms}` |
| Redis stream key | `rt:stream:{device_type}:{group_code}:{shard_no}` |
| TDengine 超级表 | `st_{device_type}_{group_code}` |
| TDengine 子表 | `t_{device_code}_{group_code}` |

为了最小改动，建议先不把这些 key 和表名冗余保存到 MySQL 中，而是在后端、采集模块和入库模块中通过统一工具方法生成。

---

## 七、device_variable 表最小新增字段

### 7.1 必须新增字段

为了让每个点位明确归属到某个分组，并能对应 TDengine 宽表列，`device_variable` 表建议新增以下字段：

| 字段名 | 说明 | 示例 |
|---|---|---|
| point_group_id | 点位分组 ID，对应 device_point_group.id | 1 |
| group_index | 组内列位序号 | 1 |
| td_column_name | TDengine 列名 | v0001 |

### 7.2 建议新增字段

为了支持软删除和配置重载，建议增加：

| 字段名 | 说明 | 示例 |
|---|---|---|
| enable | 点位是否启用 | 1 |
| deleted | 是否软删除 | 0 |

说明：

- 当前 `device` 表已有 `enable`，但 `device_variable` 表没有点位级启停字段。
- 如果不加点位级 `enable`，后续只能通过物理删除或其他方式控制点位是否采集，不利于历史映射稳定。
- 点位删除建议采用软删除，不建议物理删除。

### 7.3 不建议新增的字段

为了最小改动，以下字段暂时不建议新增：

| 字段名 | 原因 |
|---|---|
| device_code | 已可通过 device_id 关联 device 表获得 |
| device_type | 已可通过 device.type_id 或 device_code 前缀获得 |
| group_code | 已可通过 point_group_id 关联 device_point_group 获得 |
| redis_field_name | 可直接使用 device_variable.id 作为 Redis Hash field |
| period_ms | 已有 scan_rate 字段 |
| storage_enabled | 已有 is_storage 字段 |
| alarm_enabled | 已有 is_alarm 字段 |

### 7.4 字段复用关系

| 现有字段 | 在本方案中的含义 |
|---|---|
| device_variable.id | 点位唯一 ID，也可作为 Redis Hash field |
| var_seq | 原始点位顺序，可作为初次导入排序依据 |
| scan_rate | 采集周期 |
| is_alarm | 是否参与报警 |
| is_storage | 是否进入 TDengine |
| var_type | 点位数据类型 |
| var_address / var_offset / var_bit | 点位采集地址 |

---

## 八、device 表是否需要调整

### 8.1 必须调整项

`device` 表本次不强制新增字段。

设备点位分组情况通过 `device_point_group` 表维护，不建议把所有分组数量直接塞到 `device` 表里。

### 8.2 可选新增字段

如果希望采集模块快速判断某台设备配置是否变更，可以在 `device` 表增加：

| 字段名 | 说明 |
|---|---|
| variable_config_version | 设备点位配置版本号 |

当该设备任意点位新增、修改、删除、启停时：

```text
variable_config_version = variable_config_version + 1
```

该字段不是必须项。也可以只通过 `device_point_group.config_version` 判断分组级配置是否变化。

---

## 九、3072 点分组规则

### 9.1 分组原则

每台设备独立维护点位分组。

```text
每台设备每 3072 个点位一个组
```

示例：

```text
qc100
  ├── g001：第 1 ~ 3072 个点位
  ├── g002：第 3073 ~ 6144 个点位
  └── g003：第 6145 ~ 9216 个点位
```

当前每台设备约 1000 个点位，也从一开始建立 `g001`。

### 9.2 group_code 规则

分组编码固定为：

```text
g001
g002
g003
```

生成规则：

```text
group_code = 'g' + 三位序号
```

### 9.3 group_index 规则

组内序号从 1 开始，最大到 3072。

```text
group_index = 1       -> td_column_name = v0001
group_index = 1000    -> td_column_name = v1000
group_index = 3072    -> td_column_name = v3072
```

超过 3072 后自动进入下一组。

### 9.4 为什么选择 3072

TDengine 宽表需要保留 timestamp 列，同时未来可能需要补充统计列、诊断列或兼容不同版本限制。3072 是 `1024 × 3`，比接近理论上限的 4000 更稳，能为后续扩展预留足够列空间。

本项目统一约定：

```text
max_point_count = 3072
```

该值一旦用于生产建表，不建议频繁调整。

---

## 十、点位新增、修改、删除时的分组更新规则

### 10.1 新增点位

新增点位流程：

```text
1. 开启 MySQL 事务
2. 锁定对应 device_point_group 行（防并发重复分配列位）：
   SELECT * FROM device_point_group
   WHERE device_id = ? AND group_code = ?
   FOR UPDATE;
3. 读取 used_column_count 和 next_column_index（锁定后读取，保证值最新）
4. 判断 used_column_count 是否小于 3072
5. 如果小于 3072，则继续使用该组
6. 如果等于 3072，则创建下一组（同样先 FOR UPDATE 锁定新组行）
7. 分配 group_index = next_column_index
8. 生成 td_column_name = v + 四位序号
9. 写入 device_variable
10. 更新 device_point_group 数量
11. 递增 config_version
12. 提交事务
```

**并发安全说明：**

```text
如果两个请求同时新增点位，都在事务外读取 next_column_index=1001，
会各自分配出两个 v1001，导致列位冲突，TDengine 宽表数据错乱。

FOR UPDATE 锁定 device_point_group 行后，第二个事务在第一个提交前阻塞，
保证 next_column_index 读取的是最新值，不会出现重复分配。

此锁约束同样适用于点位启停和软删除操作（涉及计数字段更新时均需 FOR UPDATE）。
```

新增后更新分组表：

```text
active_point_count = active_point_count + 1
used_column_count = used_column_count + 1
next_column_index = next_column_index + 1
config_version = config_version + 1
```

如果新增后 `used_column_count = 3072`：

```text
status = full
```

### 10.2 修改点位

普通修改不改变分组数量。

普通修改包括：

```text
var_name
io_type
var_type
var_address
var_offset
var_bit
unit_id
description
scan_rate
is_alarm
is_storage
```

普通修改后只需要：

```text
对应 device_point_group.config_version + 1
```

不修改：

```text
active_point_count
disabled_point_count
deleted_point_count
used_column_count
next_column_index
group_index
td_column_name
```

### 10.3 点位启用 / 停用

如果新增了 `device_variable.enable` 字段，则启用和停用需要影响启用/停用计数。

点位停用：

```text
device_variable.enable = 0
active_point_count = active_point_count - 1
disabled_point_count = disabled_point_count + 1
config_version = config_version + 1
```

点位重新启用：

```text
device_variable.enable = 1
active_point_count = active_point_count + 1
disabled_point_count = disabled_point_count - 1
config_version = config_version + 1
```

说明：

```text
停用不等于删除。
停用点位可以重新启用。
停用不会释放 TDengine 列位。
```

### 10.4 删除点位

删除点位建议采用软删除。

```text
device_variable.deleted = 1
device_variable.enable = 0
```

删除启用点位时：

```text
active_point_count = active_point_count - 1
deleted_point_count = deleted_point_count + 1
config_version = config_version + 1
```

删除已停用点位时：

```text
disabled_point_count = disabled_point_count - 1
deleted_point_count = deleted_point_count + 1
config_version = config_version + 1
```

不修改：

```text
used_column_count
next_column_index
group_index
td_column_name
```

### 10.5 查询点位

查询点位不更新分组表。

查询时通过以下关系获得分组信息：

```text
device_variable.point_group_id -> device_point_group.id
```

或者：

```text
device.id -> device_point_group.device_id
```

用于展示：

```text
qc100
  g001：1000 / 3072
  g002：120 / 3072
```

---

## 十一、为什么不复用已删除点位的列位

不建议点位删除后立即复用原来的 `group_index` 和 `td_column_name`。

例如：

```text
v0001 -> 起升速度
v0002 -> 起升高度
v0003 -> 小车位置
```

如果删除 `v0002` 后，新点位复用 `v0002`，历史数据中同一列在不同时期代表不同含义，会造成历史查询和故障追溯混乱。

因此采用以下规则：

```text
点位可以软删除
列位不回收
列位不重排
新增点位继续使用 next_column_index
```

如果未来确实要压缩列位，只能通过离线迁移任务完成，不能在日常增删改查中自动执行。

---

## 十二、Redis Key 与数据结构设计

Redis 按设备和分组存储实时数据。

### 12.1 latest 最新值缓存

```text
rt:latest:{device_code}:{group_code}
```

示例：

```text
rt:latest:qc100:g001
rt:latest:asc7011:g001
rt:latest:agv801:g001
```

Redis 类型：

```text
Hash
```

Hash field 推荐使用：

```text
device_variable.id
```

Hash value 推荐使用 JSON 小对象，不使用 `value|timestamp|quality` 这种分隔符格式。

示例：

```text
10001 -> {"v":"12.5","t":1710000000100,"q":1}
10002 -> {"v":"1","t":1710000000100,"q":1}
```

字段说明：

| 字段 | 说明 |
|---|---|
| v | 采集值，统一按字符串保存，后端根据 var_type 转换 |
| t | 采集时间戳 |
| q | 质量码，可选，1 表示正常，0 表示异常 |

质量码 q 写入规则：

| 场景 | q 值 |
|---|---|
| 采集成功，值有效 | 1 |
| PLC 通信异常，无法读取值 | 0 |
| 协议层质量位为 bad | 0 |

说明：

```text
collector 在采集成功时写 q=1，通信错误或 PLC 报告 quality bad 时写 q=0。
backend 读取 latest 时，如果某点位 q=0，可在响应中标记该点位为"通信异常"状态。
q 字段为可选，consumer 端未收到 q 时默认视为 q=1。
```

采用 JSON 的原因：

1. 兼容 STRING 类型点位，避免值中包含分隔符导致解析错误。
2. 字段含义清晰，后续可扩展质量码、来源、异常标记。
3. 当前 30 万点规模下，Redis 内存压力可接受。

### 12.2 latest 清理策略

`rt:latest:{device_code}:{group_code}` 不设置短 TTL，因为前端实时初始化需要随时读取最新值。

但以下场景必须执行显式清理：

```text
1. 设备 enable = 0，且确认不再采集
2. 设备被软删除或退役
3. 点位分组被禁用
4. 运维执行设备配置清理任务
```

清理范围：

```text
rt:latest:{device_code}:*
rt:window:{device_code}:*:*
```

说明：

```text
latest 不依赖 TTL 自动过期，而由设备管理操作或运维清理任务主动删除。
这样可以避免设备短暂离线时 latest 被误删，也能避免设备退役后 Redis 长期残留脏 key。
```

### 12.3 window 短窗口缓存

```text
rt:window:{device_code}:{group_code}:{scan_rate_ms}
```

示例：

```text
rt:window:qc100:g001:100
rt:window:qc100:g001:1000
rt:window:asc7011:g001:500
```

Redis 类型：

```text
List
```

用途：

```text
保存最近 5~10 帧数据
服务前端最近 500ms / 1s 实时趋势展示
```

**为什么 key 中包含 scan_rate_ms：**

同一分组内可能存在多种采集周期（如 100ms、500ms、1000ms）。若所有周期的帧共用同一个 List，LTRIM 10 帧后实际只覆盖 1 秒的 100ms 数据，而 1000ms 点位在 10 帧中最多出现 1 次，前端读取时会误判为断点。按 scan_rate_ms 分 key 后，每个周期独立保留 N 帧，语义清晰，前端可按需请求对应周期的趋势。

保留规则：

```text
100ms 数据建议保留 5~10 帧
500ms 数据建议保留 4~10 帧
1s 数据建议保留 5~10 帧
```

window 不作为首屏默认返回数据，按需独立接口查询。

#### 12.3.1 window 帧格式契约

`rt:window:{device_code}:{group_code}:{scan_rate_ms}` 中每个 List 元素表示一帧短窗口数据。

推荐格式：

```json
{
  "device_code": "qc100",
  "group_code": "g001",
  "scan_rate": 100,
  "ts": 1710000000100,
  "values": {
    "10001": "12.5",
    "10002": "1",
    "10003": "0"
  }
}
```

字段说明：

| 字段 | 说明 |
|---|---|
| device_code | 设备编码 |
| group_code | 点位分组 |
| scan_rate | 当前帧对应采集周期 |
| ts | 采集时间戳 |
| values | 点位值集合，key 为 device_variable.id，value 为采集值 |

约束：

```text
1. window 帧格式与 Stream entry 的 values 结构保持一致，便于复用解析逻辑。
2. window 不需要 config_version，因为它只服务实时短窗口展示，不用于历史入库。
3. window 不保存长期历史，只保留最近 N 帧。
4. window 写入后必须执行 LTRIM，避免 List 无限增长。
```

#### 12.3.2 window List 写入方向与 LTRIM 规范

写入方向统一使用 `LPUSH`，最新帧始终在 List 头部（index 0）。

collector 写入流程：

```text
步骤 1：LPUSH rt:window:{device_code}:{group_code}:{scan_rate_ms} <frame_json>
步骤 2：LTRIM rt:window:{device_code}:{group_code}:{scan_rate_ms} 0 9
```

说明：

```text
LPUSH 将新帧插入头部，最新帧在 index 0。
LTRIM 0 9 保留最近 10 帧，第 11 帧起自动丢弃。
如需保留 5 帧，则 LTRIM 0 4。
每次 LPUSH 后必须立即 LTRIM，不可省略。
scan_rate_ms 对应本批次采集周期，单位毫秒，如 100、500、1000。
```

TurboAPI 读取方式：

```text
LRANGE rt:window:{device_code}:{group_code}:{scan_rate_ms} 0 N-1
```

示例：读取 qc100 g001 分组 100ms 周期最近 5 帧：

```text
LRANGE rt:window:qc100:g001:100 0 4
```

返回顺序：index 0 为最新帧，index N-1 为最旧帧。

约束：

```text
1. collector、TurboAPI 必须统一使用 LPUSH 写入，不允许混用 RPUSH。
2. 读取方向固定为 LRANGE 0 N-1，index 0 为最新。
3. 前端展示时注意帧顺序，如需时间升序展示，应在后端或前端反转。
```

### 12.4 stream 待入库队列

```text
rt:stream:{device_type}:{group_code}:{shard_no}
```

示例：

```text
rt:stream:qc:g001:0
rt:stream:asc:g001:0
rt:stream:asc:g001:1
rt:stream:agv:g001:0
```

分片规则：

```text
shard_no = hash(device_code) % shard_count
```

同一台设备的同一个分组，应稳定进入同一个 Stream 分片。

### 12.5 shard_count 固化规则

建议默认：

```text
shard_count = 4
```

该值应固化在系统配置表或配置文件中，例如：

```text
redis_stream_shard_count = 4
```

约束：

```text
1. 所有 collector、td_writer、后端工具方法必须使用同一份 shard_count 配置。
2. shard_count 不允许运行中直接修改。
3. 如果后续确实需要从 4 改成 8，必须执行迁移方案，而不是直接改配置。
4. 迁移期间 td_writer 必须同时消费旧 shard 和新 shard，直到旧 Stream 清空。
```

### 12.6 Stream MAXLEN / XTRIM 规则

Stream 必须设置长度上限，避免 TDengine 异常或入库模块停止时 Redis 无限膨胀。

推荐追加时使用近似裁剪：

```text
XADD rt:stream:{device_type}:{group_code}:{shard_no} MAXLEN ~ 100000 * ...
```

说明：

```text
MAXLEN 使用 ~ 近似裁剪，不使用精确裁剪。
近似裁剪性能更好，避免每次写入都触发高成本整理。
```

建议初始值：

| 设备类型 | 单 Stream MAXLEN 建议 |
|---|---:|
| qc | 50000 ~ 100000 |
| asc | 100000 ~ 300000 |
| agv | 100000 ~ 300000 |

同时监控：

```text
Stream 长度
Consumer Group pending 数量
最早未 ACK 消息时间
td_writer 入库延迟
```

---

## 十三、Redis Stream 消息契约

### 13.1 每条 Stream entry 的含义

每条 Stream entry 表示：

```text
某一台设备、某一个分组、某一个采集周期批次、某一个采集时间戳下的一批点位值。
```

不采用"一个点位一条 Stream 消息"的方式。

### 13.2 消息字段

Stream entry 必须包含以下字段：

```json
{
  "device_code": "qc100",
  "device_type": "qc",
  "group_code": "g001",
  "scan_rate": 100,
  "ts": 1710000000100,
  "config_version": 12,
  "values": {
    "10001": "12.5",
    "10002": "1",
    "10003": "0"
  }
}
```

字段说明：

| 字段 | 说明 |
|---|---|
| device_code | 设备编码，如 qc100 |
| device_type | 设备类型，如 qc、asc、agv |
| group_code | 点位分组，如 g001 |
| scan_rate | 本批次点位采集周期 |
| ts | 采集时间戳 |
| config_version | 该设备该分组采集时使用的配置版本 |
| values | 点位值集合 |

`values` 的 key 统一使用：

```text
device_variable.id
```

`values` 的 value 只保存采集值本身，质量码如有需要可单独扩展。

**Redis Stream 字段序列化规范：**

Redis Stream 是 field-value 扁平结构，`values` 字段必须序列化为 JSON 字符串后写入，不能展开为多个独立 field。

XADD 实际写入格式示例：

```text
XADD rt:stream:qc:g001:0 MAXLEN ~ 100000 *
  device_code  qc100
  device_type  qc
  group_code   g001
  scan_rate    100
  ts           1710000000100
  config_version 12
  values       {"10001":"12.5","10002":"1","10003":"0"}
```

说明：

```text
values 字段是 JSON 字符串，不是嵌套对象。
collector 写入时执行 json.dumps(values_dict)。
td_writer 消费时执行 json.loads(entry["values"])。
collector 和 td_writer 必须统一此约定，否则解析会失败。
```

### 13.3 values 全量写入规则

本方案明确采用：

```text
按 scan_rate 批次全量写入，不做变更过滤。
```

也就是说，同一设备、同一分组、同一采集周期下，只要本周期采集任务触发，就将该批次中的所有点位值写入同一条 Stream entry。

示例：

```text
qc100 / g001 / scan_rate=100ms
本批次包含 300 个 100ms 点位
则 values 中包含这 300 个点位的完整值
```

不采用只写变化点的方式。

原因：

1. TDengine 每一行保持相对完整的时间切面。
2. 历史查询逻辑简单，不需要大量依赖 FILL(PREV) 还原状态。
3. td_writer 组装宽表 SQL 更直接。
4. 故障追溯时，同一时间点的数据语义更清晰。

说明：

```text
全量写入是指"同一 scan_rate 批次内全量"，不是整台设备所有点位全量。
不同 scan_rate 的点位可以进入不同批次。
```

**Stream values 的点位过滤规则：**

collector 写入 Stream entry 的 `values` 时，必须过滤以下两类点位：

```text
1. device_variable.var_type = STRING
2. device_variable.is_storage = 0
```

这两类点位可以写入 `latest` 和 `window`（供前端实时展示），但不写入 Stream entry 的 `values`，原因如下：

```text
1. STRING 类型点位不进入 TDengine 宽表，td_writer 无法写入对应列。
2. is_storage=0 的点位不需要历史入库，进入 Stream 浪费存储空间。
3. 在 collector 侧过滤，避免 td_writer 每次消费时重复判断。
```

td_writer 侧的过滤（section 16.6）作为双重保护，不依赖 collector 侧已过滤。

---

## 十四、TDengine 表设计

TDengine 按设备类型和点位分组建立超级表。

### 14.1 超级表命名

```text
st_{device_type}_{group_code}
```

示例：

```text
st_qc_g001
st_asc_g001
st_agv_g001
```

### 14.2 子表命名

```text
t_{device_code}_{group_code}
```

示例：

```text
t_qc100_g001
t_asc7011_g001
t_agv801_g001
```

### 14.3 列名规则

TDengine 宽表列名统一使用：

```text
v0001 ~ v3072
```

点位真实含义通过 MySQL 映射表解释。

映射关系：

```text
device_variable.point_group_id -> device_point_group.id
device_variable.group_index    -> TDengine 列位序号
device_variable.td_column_name -> TDengine 列名
```

示例：

| device_id | point_group_id | group_index | td_column_name | var_name |
|---:|---:|---:|---|---|
| 1 | 1 | 1 | v0001 | 起升速度 |
| 1 | 1 | 2 | v0002 | 起升高度 |
| 1 | 1 | 3 | v0003 | 小车位置 |

### 14.4 TDengine 列类型策略

本方案建议 TDengine 历史宽表的点位值列统一使用数值类型。

推荐策略：

```text
v0001 ~ v3072 统一使用 DOUBLE
```

原因：

```text
1. 建表规则简单，所有设备类型、所有分组使用统一宽表模板。
2. BOOL、INT、REAL 等数值类点位均可映射为 DOUBLE 保存。
3. td_writer 组装 SQL 时不需要为每个列位动态处理不同 TDengine 列类型。
4. 后续按 device_variable.var_type 在后端做展示层类型还原。
```

约束：

```text
1. STRING 类型点位默认不进入 TDengine 宽表历史。
2. STRING 类型点位如需实时展示，可以写入 Redis latest/window。
3. STRING 类型点位如确需历史保存，应单独设计事件表或字符串专用表，不混入本宽表。
4. device_variable.is_storage = 1 的点位原则上应为 BOOL / INT / DINT / FLOAT / REAL / DOUBLE 等可数值化类型。
```

推荐校验规则：

```text
当 var_type = STRING 且 is_storage = 1 时，系统应在配置保存阶段提示或禁止保存。
```

### 14.5 TDengine 建表时机

本方案采用**初始化脚本预建**策略，不依赖 td_writer 首次写入时自动建表。

#### 14.5.1 策略选择

| 方案 | 优点 | 缺点 |
|---|---|---|
| 初始化脚本预建（本方案采用） | 建表延迟可控；权限可控；异常易排查；td_writer 启动即可写入 | 需要维护初始化脚本 |
| td_writer 首次写入自动建表 | 无需额外脚本 | 第一条数据写入时引入建表延迟；建表权限需开放给 td_writer；异常排查较难 |

#### 14.5.2 初始化脚本职责

系统部署时，由初始化脚本完成以下操作：

```text
1. 按设备类型创建 STable（超级表）
   CREATE STABLE IF NOT EXISTS st_qc_g001 (ts TIMESTAMP, v0001 DOUBLE, v0002 DOUBLE, ..., v3072 DOUBLE) TAGS (device_code NCHAR(32));
   CREATE STABLE IF NOT EXISTS st_asc_g001 (...);
   CREATE STABLE IF NOT EXISTS st_agv_g001 (...);

2. 按设备编码和分组创建子表
   CREATE TABLE IF NOT EXISTS t_qc100_g001 USING st_qc_g001 TAGS ('qc100');
   CREATE TABLE IF NOT EXISTS t_asc7011_g001 USING st_asc_g001 TAGS ('asc7011');
   CREATE TABLE IF NOT EXISTS t_agv801_g001 USING st_agv_g001 TAGS ('agv801');
```

约束：

```text
1. 初始化脚本必须幂等（IF NOT EXISTS），可重复执行不报错。
2. 初始化脚本执行完成后，td_writer 才允许启动消费 Stream。
3. 初始化脚本应纳入部署流程，不依赖手动执行。
```

#### 14.5.3 新增设备后的建表流程

当系统新增设备时（如新增一台 qc200），不需要重启 td_writer，但需要触发建表操作。

推荐流程：

```text
1. 在 MySQL 中新增设备记录和点位分组记录
2. 执行建表操作（手动触发或由管理后台调用）：
   CREATE TABLE IF NOT EXISTS t_qc200_g001 USING st_qc_g001 TAGS ('qc200');
3. 确认子表创建成功后，collector 开始采集该设备数据
4. td_writer 无需重启，自动处理新设备的 Stream 消息
```

说明：

```text
STable 已在初始化时创建，新增设备只需创建子表。
新设备的 STable 如果是新设备类型，则需要先创建 STable 再创建子表。
建议由 TurboAPI 管理后台的"新增设备"操作自动触发建表，而不是纯手动执行。
```

#### 14.5.4 td_writer 建表保护

td_writer 在写入前，可以做一次轻量校验：

```text
如果目标子表不存在，记录告警日志并跳过该批次写入，不崩溃。
由运维人工排查后补建子表，再由 td_writer 重试 pending 消息。
```

**强制约束：目标子表不存在时，td_writer 不得执行 XACK。**

```text
目标子表不存在属于配置异常，不是正常处理完成。
如果此时执行 XACK，消息将从 pending 中永久清除，数据无法找回。
正确行为：保留消息在 pending 中，等运维补建子表后，由重启或 XAUTOCLAIM 认领重试。
```

原因：

```text
td_writer 不负责建表，遇到表不存在时应告警而非自动建表。
自动建表容易掩盖配置问题（如设备分组未初始化），导致后续数据混乱。
```

---

## 十五、采集模块匹配规则

采集模块启动时，从 MySQL 加载：

```text
device
device_point_group
device_variable
```

采集任务按照以下层级组织：

```text
设备
  ↓
点位分组
  ↓
采集周期 scan_rate
  ↓
PLC 地址连续区间
```

示例：

```text
qc100
  g001
    scan_rate = 100ms
    scan_rate = 500ms
    scan_rate = 1000ms
```

采集模块写 Redis 时，必须根据点位所属分组写入对应 key：

```text
rt:latest:{device_code}:{group_code}
rt:window:{device_code}:{group_code}
rt:stream:{device_type}:{group_code}:{shard_no}
```

采集模块不直接写 TDengine。

### 15.1 latest 写入规则

collector 每次采集到点位值后，写入：

```text
rt:latest:{device_code}:{group_code}
```

field：

```text
device_variable.id
```

value：

```json
{"v":"12.5","t":1710000000100,"q":1}
```

### 15.2 window 写入规则

collector 按设备、分组、采集周期写入短窗口帧，每个 scan_rate 写入独立的 key。

```text
步骤 1：LPUSH rt:window:{device_code}:{group_code}:{scan_rate_ms} <frame_json>
步骤 2：LTRIM rt:window:{device_code}:{group_code}:{scan_rate_ms} 0 9
```

window 只保留最近 N 帧，不保存长期历史。

### 15.3 stream 写入规则

collector 按以下维度生成 Stream entry：

```text
device_code + group_code + scan_rate + ts
```

同一批次内的点位全量写入 `values`。

---

## 十六、入库模块 td_writer 匹配规则

入库模块从 Redis Stream 消费数据，并按设备类型、设备分组写入 TDengine。

### 16.1 消费规则

```text
rt:stream:{device_type}:{group_code}:{shard_no}
```

示例：

```text
rt:stream:qc:g001:0  -> 写入 st_qc_g001 / t_qc100_g001
rt:stream:asc:g001:1 -> 写入 st_asc_g001 / t_asc7011_g001
```

### 16.2 Consumer Group 规则

`td_writer` 必须使用 Redis Consumer Group 消费 Stream。

统一约定：

```text
Consumer Group 名称：td_writer_cg
Consumer 名称：td_writer_{device_type}_{group_code}_{shard_no}_{instance_no}
```

消费方式：

```text
XREADGROUP GROUP td_writer_cg <consumer_name> COUNT <batch_size> BLOCK <timeout> STREAMS <stream_key> >
```

确认方式：

```text
TDengine 写入成功后执行 XACK。
```

使用 Consumer Group 的原因：

```text
1. 支持 td_writer 重启后的断点续读。
2. 支持 pending 消息重试。
3. 支持多个 td_writer 实例分工消费。
4. 支持监控每个 Stream 的积压和未确认消息。
```

约束：

```text
collector 只负责 XADD。
td_writer 负责 XREADGROUP、写 TDengine、XACK。
不允许使用普通 XREAD 替代可靠消费链路。
```

### 16.3 td_writer 重启后的 pending 消息处理

td_writer 重启后，必须先处理上次未 ACK 的 pending 消息，再切换到正常消费模式。

#### 16.3.1 pending 消息产生原因

```text
td_writer 从 Stream 读取消息（XREADGROUP）后，消息进入 pending 状态。
如果 TDengine 写入失败或 td_writer 进程意外退出，消息未被 XACK，则一直保留在 pending 列表中。
重启后如果不处理 pending，这批消息将永远积压，不会自动重试。
```

#### 16.3.2 重启处理流程

td_writer 启动时必须执行以下流程：

```text
步骤 1：查询当前 Consumer Group 的 pending 消息
        XPENDING <stream_key> td_writer_cg - + <count>

步骤 2：如果存在超过阈值时间（如 30s）未 ACK 的 pending 消息，执行认领
        XAUTOCLAIM <stream_key> td_writer_cg <consumer_name> <min-idle-time> 0-0 COUNT <count>
        （Redis 6.2+ 支持 XAUTOCLAIM）

步骤 3：对认领到的消息，重新执行 TDengine 写入

步骤 4：写入成功后执行 XACK

步骤 5：重复步骤 1~4，直到 pending 列表为空或无超时消息

步骤 6：切换到正常消费模式，执行 XREADGROUP ... >
```

对于 Redis 版本低于 6.2 的环境，可以用 `XPENDING` + `XCLAIM` 替代 `XAUTOCLAIM`：

```text
步骤 2（替代）：
  XPENDING <stream_key> td_writer_cg - + <count>     # 获取 pending 消息列表
  XCLAIM <stream_key> td_writer_cg <consumer_name> <min-idle-time> <message_id>  # 逐条认领
```

约束：

```text
1. td_writer 启动时，必须先完成 pending 处理再进入正常消费，不可跳过。
2. min-idle-time 建议设置为 30000（30 秒），避免误认领仍在正常写入的消息。
3. pending 处理中，如果 TDengine 仍不可用，应记录告警日志，等待 TDengine 恢复后重试，不允许丢弃 pending 消息。
4. 如果同一条消息反复重试失败超过阈值次数（如 5 次），应转入死信队列或记录告警，由人工介入处理。
```

### 16.4 ACK 规则

入库模块必须遵守：

```text
TDengine 写入成功后，才 ACK Redis Stream。
```

不允许读取 Stream 后立即 ACK。

如果 TDengine 写入失败：

```text
1. 不 ACK 当前消息
2. 记录错误日志
3. 保留消息进入 pending
4. 后续由重试逻辑处理
```

### 16.5 config_version 重载策略

`td_writer` 本地会缓存以下映射：

```text
device_code + group_code -> TDengine 子表
device_variable.id -> td_column_name
device_variable.id -> var_type
```

每条 Stream entry 都必须携带 `config_version`。

当 `td_writer` 收到消息时，应执行以下判断：

```text
1. 读取消息中的 device_code、group_code、config_version
2. 查询本地缓存中该 device_code + group_code 的 config_version
3. 如果版本一致，直接使用本地列映射写 TDengine
4. 如果版本不一致，必须先从 MySQL 重新加载该设备该分组的列映射
5. 重载成功后，再根据新映射写 TDengine
6. 不允许使用旧映射直接写入
```

该规则是强约束。

原因：

```text
如果点位新增、删除、停用或列映射变化后，td_writer 仍使用旧映射，可能把数据写入错误列，导致历史数据污染。
```

### 16.6 入库字段过滤

是否入库优先使用现有字段：

```text
device_variable.is_storage
```

规则：

| is_storage | 说明 |
|---:|---|
| 1 | 写入 TDengine |
| 0 | 只用于实时显示或内部判断，不写 TDengine |

### 16.7 批量入库策略

推荐按以下策略批量写入：

```text
每 500ms 或 1s 消费一批 Redis Stream 消息
按 device_type / group_code / device_code / ts 归并
组装 TDengine 批量写入语句
写入成功后 ACK
```

### 16.8 死信队列（DLQ）规范

当同一条 Stream 消息重试次数超过 `td_writer_pending_max_retry`（默认 5 次）时，不再重试，转入死信队列。

#### 16.8.1 DLQ Key

```text
rt:dlq:{device_type}:{group_code}
```

示例：

```text
rt:dlq:qc:g001
rt:dlq:asc:g001
```

Redis 类型：List（LPUSH 写入）

#### 16.8.2 DLQ 消息格式

```json
{
  "entry_id": "1710000000100-0",
  "stream_key": "rt:stream:qc:g001:0",
  "device_code": "qc100",
  "group_code": "g001",
  "retry_count": 5,
  "last_error": "TDengine write timeout",
  "failed_at": 1710000060000,
  "payload": { "...原始 Stream entry values..." }
}
```

#### 16.8.3 DLQ 处理规则

```text
1. td_writer 将消息写入 DLQ 后，执行 XACK 将原 Stream 消息从 pending 中清除。
2. DLQ 条目不自动重放，必须由运维人员手动确认后决定是否重放或丢弃。
3. 每次有新消息进入 DLQ，td_writer 必须记录 ERROR 级别日志并触发告警通知。
4. 重放方式：运维确认后，将消息重新 XADD 到对应 Stream，再 LPOP 清除 DLQ 条目。
5. DLQ 不设 TTL，条目保留直到人工处理。
```

---

## 十七、后端接口匹配规则

### 17.1 实时初始化接口

前端打开设备实时页面时：

```text
/api/device-monitor/realtime-init?device_code=qc100&group_code=g001
```

后端读取：

```text
MySQL：device、device_point_group、device_variable
Redis：rt:latest:qc100:g001
```

返回给前端：

```text
设备信息
点位分组信息
点位清单
最新值 latest
```

注意：

```text
realtime-init 默认只返回 latest，不返回 window。
```

原因：

```text
如果首屏同时返回 1000 点 latest + 10 帧 window，数据量会明显变大。
后续扩展到 3072 点/组时，首屏压力更高。
```

### 17.2 短窗口接口

window 单独按需请求：

```text
/api/device-monitor/realtime-window?device_code=qc100&group_code=g001&scan_rate_ms=100&limit=5
```

后端读取：

```text
LRANGE rt:window:qc100:g001:100 0 4
```

参数说明：

```text
scan_rate_ms：指定请求哪个采集周期的窗口。可选，默认值为该设备该分组最快采集周期。
limit：返回帧数上限，默认 10。
```

返回帧顺序：index 0 为最新帧，index N-1 为最旧帧。

如前端需要时间升序展示，后端在返回前反转帧列表。

用于趋势小窗、短时波形等功能。

### 17.3 点位分组接口

```text
/api/device/{device_code}/point-groups
```

返回示例：

```text
qc100
  g001：1000 / 3072
  g002：120 / 3072
```

### 17.4 分组点位接口

```text
/api/device/{device_code}/variables?group_code=g001
```

用于前端按组加载点位清单。

### 17.5 历史查询接口

历史查询从 TDengine 取数。

前端可以传：

```text
device_code
var_id
start_time
end_time
```

后端根据 `var_id` 查询 MySQL 映射，得到：

```text
point_group_id
group_code
td_column_name
td_child_table_name
```

然后查询 TDengine。

#### 17.5.1 FILL(PREV) 使用说明

本系统采用"按 scan_rate 批次全量写入"策略，不同采集周期的点位进入不同批次，导致 TDengine 中每行只包含同一批次采集周期的点位值，其他列为 NULL。

查询单点位历史数据时，如果该点位的采集周期较长（如 1000ms），而时间范围内存在大量其他周期的行，则查询结果中该列会出现大量 NULL，导致历史曲线断点。

**处理规则：**

```text
查询单点位历史数据时，建议使用 FILL(PREV) 填充 NULL，
避免因多 scan_rate 批次导致曲线断点。
```

示例 SQL：

```sql
SELECT ts, v0001
FROM t_qc100_g001
WHERE ts BETWEEN '2024-03-10 00:00:00' AND '2024-03-10 01:00:00'
FILL(PREV)
```

说明：

```text
FILL(PREV) 将 NULL 值用前一个非 NULL 值填充，适合设备状态类点位。
如果时间范围起始点没有历史值，FILL(PREV) 无法填充第一个 NULL，前端需处理此边界情况。
对于报警分析、精确计量等场景，如果不应填充 NULL，应明确传入 FILL(NONE) 或不使用 FILL。
```

**建议：**

```text
历史查询接口提供 fill_mode 参数，默认值为 PREV。
前端可按需传入 PREV / NONE / LINEAR 等模式。
```

### 17.6 WebSocket 实时推送机制

#### 17.6.1 推送触发方式

后端采用定时轮询 Redis 方式推送，不依赖 Redis Pub/Sub，减少架构依赖：

```text
后端 WebSocket 推送循环（per device_code + group_code，不是 per 客户端会话）：
  每 100ms 读取 rt:latest:{device_code}:{group_code}
  与上次快照对比，提取有变化的点位（diff）
  将 diff 广播给所有订阅该 device_code + group_code 的客户端
```

**服务端合并订阅约束（重要）：**

```text
后端必须按 device_code + group_code 合并订阅，同一设备分组只维护一个轮询任务。
不允许每个 WebSocket 客户端连接各自独立轮询 Redis。

正确模型：
  客户端 A 订阅 qc100/g001 → 注册到服务端推送任务 [qc100/g001]
  客户端 B 订阅 qc100/g001 → 注册到同一服务端推送任务 [qc100/g001]
  → 只有 1 个 HGETALL 每 100ms，广播给 A 和 B

错误模型：
  客户端 A 订阅 qc100/g001 → 各自发起 HGETALL
  客户端 B 订阅 qc100/g001 → 各自发起 HGETALL
  → 并发用户多时 Redis 压力线性放大，100 个客户端 = 100 × 10 次/s = 1000 次/s HGETALL
```

#### 17.6.2 客户端订阅粒度

客户端以 device_code + group_code 为最小订阅单元：

```json
{
  "action": "subscribe",
  "device_code": "qc100",
  "group_code": "g001"
}
```

#### 17.6.3 推送频率

| 场景 | 推送间隔 |
|---|---|
| 实时监控页（设备在线） | 100ms |
| 设备离线（latest 中所有点位 t 超过 30s 未更新） | 停止推送，发送一次离线状态通知 |
| 后台非活跃标签页 | 前端通过 Page Visibility API 自行降频 |

#### 17.6.4 推送数据格式

首次订阅推送完整 latest 快照：

```json
{
  "type": "snapshot",
  "device_code": "qc100",
  "group_code": "g001",
  "values": {
    "10001": {"v": "12.5", "t": 1710000000100, "q": 1},
    "10002": {"v": "1",    "t": 1710000000100, "q": 1}
  }
}
```

后续推送只发送有变化的点位（diff）：

```json
{
  "type": "diff",
  "device_code": "qc100",
  "group_code": "g001",
  "values": {
    "10001": {"v": "13.1", "t": 1710000000200, "q": 1}
  }
}
```

离线通知：

```json
{
  "type": "offline",
  "device_code": "qc100",
  "group_code": "g001"
}
```

---

## 十八、前端页面匹配规则

### 18.1 实时页面

实时页面数据来自 Redis，不来自 TDengine。

页面加载流程：

```text
1. 查询设备基本信息
2. 查询设备点位分组列表
3. 默认加载 g001
4. 调用 realtime-init 获取 latest
5. 如需趋势小窗，单独调用 realtime-window
6. WebSocket 订阅当前设备和当前分组
```

### 18.2 历史页面

历史页面数据来自 TDengine。

页面加载流程：

```text
1. 查询设备点位清单
2. 用户选择点位
3. 后端根据 var_id 找到 group_code 和 td_column_name
4. 查询 TDengine（默认使用 FILL(PREV)）
5. 返回历史曲线数据
```

### 18.3 分组展示方式

当前 1000 点阶段：

```text
默认显示 g001
```

未来超过 3072 点后：

```text
g001
g002
g003
```

前端通过分组页签、分组下拉框或功能分区加载数据。

---

## 十九、配置版本与重载机制

### 19.1 分组级版本

`device_point_group.config_version` 用于标识该组点位配置是否变化。

以下操作需要递增版本：

```text
新增点位
修改点位采集配置
启用点位
停用点位
软删除点位
修改 scan_rate
修改 is_storage
修改 is_alarm
```

**事务原子性要求：**

以上所有操作必须在同一个 MySQL 事务中完成，包括：

```text
1. 点位数据的 INSERT / UPDATE / 软删除
2. device_point_group.config_version + 1
3. device_point_group 计数字段更新（active_point_count / disabled_point_count / deleted_point_count）
```

事务提交成功后，collector 和 td_writer 才会感知到版本变化。不允许在事务外单独递增 config_version，否则可能出现版本已变更但 MySQL 数据未更新的中间状态，导致 td_writer 重载到不完整的映射。

### 19.2 设备级版本

如果 `device` 表增加 `variable_config_version`，则该设备任意分组发生变化时也递增。

采集模块可以通过设备级版本快速判断是否需要重载该设备配置。

### 19.3 采集模块重载

采集模块不建议整体重启。

推荐流程：

```text
1. 定期检查配置版本
2. 发现某设备或某分组版本变化
3. 只重载对应设备 / 对应分组
4. 重建对应采集任务
5. 不影响其他设备采集
```

### 19.4 td_writer 重载

`td_writer` 不只依赖定期检查版本，还必须在消费 Stream entry 时校验消息版本。

强制规则：

```text
Stream entry.config_version != 本地缓存 config_version
=> 先重新加载 MySQL 映射
=> 再写 TDengine
```

不允许版本不一致时继续使用旧映射。

---

## 二十、当前阶段初始化建议

当前阶段按照 305 台设备、每台约 1000 点落地。

初始化流程：

```text
1. 保留现有 device 表结构
2. 保留现有 device_variable 主体字段
3. 新增 device_point_group 表
4. device_variable 新增 point_group_id、group_index、td_column_name、enable、deleted
5. 每台设备创建 g001 分组
6. max_point_count 固定为 3072
7. 按 device_id + var_seq 或 id 顺序给点位分配 group_index
8. 生成 td_column_name：v0001 ~ v1000
9. 更新 device_point_group.active_point_count
10. 更新 device_point_group.used_column_count
11. 更新 device_point_group.next_column_index
12. 初始化 disabled_point_count = 0
13. 初始化 deleted_point_count = 0
14. 初始化 config_version = 1
15. 执行 TDengine 初始化脚本，预建所有 STable 和子表
16. 为每个 Stream 分片创建 Consumer Group（按当前设备类型和 shard_count=4 依次执行）：
    XGROUP CREATE rt:stream:{device_type}:{group_code}:{shard_no} td_writer_cg $ MKSTREAM
    示例：
    XGROUP CREATE rt:stream:qc:g001:0 td_writer_cg $ MKSTREAM
    XGROUP CREATE rt:stream:qc:g001:1 td_writer_cg $ MKSTREAM
    XGROUP CREATE rt:stream:qc:g001:2 td_writer_cg $ MKSTREAM
    XGROUP CREATE rt:stream:qc:g001:3 td_writer_cg $ MKSTREAM
    XGROUP CREATE rt:stream:asc:g001:0 td_writer_cg $ MKSTREAM
    ...（asc、agv 各 4 个分片）
17. 确认 TDengine 建表和 Consumer Group 创建完成后，启动 td_writer
```

以 `qc100` 为例：

```text
device_code = qc100
point_group = g001
max_point_count = 3072
active_point_count = 1000
disabled_point_count = 0
deleted_point_count = 0
used_column_count = 1000
next_column_index = 1001
config_version = 1

Redis latest = rt:latest:qc100:g001
Redis window = rt:window:qc100:g001:{scan_rate_ms}  （如 rt:window:qc100:g001:100）
Redis stream = rt:stream:qc:g001:0

TDengine stable = st_qc_g001
TDengine child table = t_qc100_g001
```

---

## 二十一、后续扩展场景

### 21.1 单设备超过 3072 点

当某设备 `g001` 达到：

```text
used_column_count = 3072
```

再次新增点位时自动创建：

```text
g002
```

新点位进入：

```text
rt:latest:{device_code}:g002
rt:window:{device_code}:g002:{scan_rate_ms}
rt:stream:{device_type}:g002:{shard_no}
t_{device_code}_g002
```

同时需要触发建表操作：

```text
CREATE TABLE IF NOT EXISTS t_{device_code}_g002 USING st_{device_type}_g002 TAGS ('{device_code}');
```

如果 `st_{device_type}_g002` 尚未创建，则先创建 STable：

```text
CREATE STABLE IF NOT EXISTS st_{device_type}_g002 (ts TIMESTAMP, v0001 DOUBLE, ..., v3072 DOUBLE) TAGS (device_code NCHAR(32));
```

### 21.2 单设备超过 6144 点

当 `g002` 也达到 3072 后，自动创建：

```text
g003
```

整体架构不变，只增加新的分组配置、Redis key 和 TDengine 子表。

### 21.3 shard_count 扩展

如果未来单 Stream 压力过大，需要把 `shard_count = 4` 扩展为 8 或更多，必须走迁移流程。

迁移原则：

```text
1. 不允许运行中直接修改 shard_count。
2. 先停止新增旧 shard 写入或进入双写/切换窗口。
3. td_writer 同时消费旧 shard 和新 shard。
4. 确认旧 shard 无积压后，再下线旧 shard。
5. 迁移过程需要记录配置版本和切换时间。
```

---

## 二十二、最终推荐表结构调整汇总

### 22.1 device 表

保留现有结构。

可选增加：

| 新增字段 | 说明 |
|---|---|
| variable_config_version | 设备点位配置版本号 |

### 22.2 新增 device_point_group 表

必须新增。

核心字段：

```text
id
device_id
group_no
group_code
group_name
max_point_count
active_point_count
disabled_point_count
deleted_point_count
used_column_count
next_column_index
status
config_version
create_user
create_time
update_user
update_time
```

计数不变式：

```text
active_point_count + disabled_point_count + deleted_point_count <= used_column_count
```

### 22.3 device_variable 表

建议新增：

```text
point_group_id
group_index
td_column_name
enable
deleted
```

继续复用已有字段：

```text
scan_rate      -> 采集周期
is_alarm       -> 是否报警点位
is_storage     -> 是否入 TDengine
var_type       -> 数据类型
var_address    -> 地址
var_offset     -> 地址偏移
var_bit        -> 位偏移
```

### 22.4 系统配置

建议新增系统级配置：

```text
redis_stream_shard_count = 4
redis_stream_maxlen_qc = 100000
redis_stream_maxlen_asc = 300000
redis_stream_maxlen_agv = 300000
point_group_max_point_count = 3072
td_writer_pending_min_idle_ms = 30000
td_writer_pending_max_retry = 5
```

其中：

```text
redis_stream_shard_count 不允许运行中直接修改。
point_group_max_point_count 建议生产确定后不再频繁调整。
td_writer_pending_min_idle_ms 控制 pending 消息的最小空闲时间阈值，超过后重新认领。
td_writer_pending_max_retry 控制同一条消息的最大重试次数，超过后转入死信队列并告警。
```

**Redis 持久化配置（必须设置）：**

```text
redis_aof_enabled = true       # 必须开启 AOF，保护 Stream 数据不因 Redis 崩溃丢失
redis_aof_fsync = everysec     # 每秒 fsync，平衡性能与可靠性
redis_rdb_enabled = true       # 开启 RDB 作为补充快照
```

说明：

```text
latest 和 window key 允许 Redis 崩溃后丢失（collector 重新采集后自动恢复）。
Stream 数据不允许丢失，必须开启 AOF 持久化。
否则 Redis 进程崩溃后 Stream 中未 ACK 的 pending 消息丢失，td_writer 重启后无 pending 可恢复。
AOF rewrite 期间写入性能会有短暂抖动，需纳入监控。
```

---

## 二十三、关键设计结论

本方案采用以下设计：

```text
前端实时数据来自 Redis，不来自 TDengine。
TDengine 只负责历史查询和历史分析。
MySQL 固化点位分组和点位到 TDengine 列的映射关系。
每台设备从 g001 开始建组。
每组最多 3072 个点位。
超过 3072 自动创建 g002。
超过 6144 自动创建 g003。
device_point_group 记录每个设备每个组的点位数量和列位使用情况。
device_variable 只补充分组映射字段，不大改原有结构。
点位删除采用软删除，列位不复用、不重排。
latest 使用 Redis Hash，value 使用 JSON 小对象。
latest 不设置短 TTL，设备停用、删除或退役时由管理操作显式清理 latest/window key。
window 使用 Redis List，key 格式为 rt:window:{device_code}:{group_code}:{scan_rate_ms}，同一分组不同采集周期写入独立 key。collector 使用 LPUSH 写入新帧（最新帧在 index 0），写入后必须 LTRIM，TurboAPI 使用 LRANGE 0 N-1 读取。
Stream entry 必须包含 device_code、group_code、ts、scan_rate、config_version、values。
Stream values 按 scan_rate 批次全量写入；collector 在写入 Stream values 时必须过滤 var_type=STRING 和 is_storage=0 的点位。
td_writer 必须使用 Redis Consumer Group 消费 Stream，Consumer Group 名称统一为 td_writer_cg。
td_writer 重启时必须先处理 pending 消息（XAUTOCLAIM / XPENDING + XCLAIM），再切换到正常消费模式。
td_writer 发现 config_version 不一致时，必须先重载 MySQL 映射，再写 TDengine。
TDengine 宽表点位列统一使用 DOUBLE；STRING 类型点位默认不入宽表历史。
TDengine 建表采用初始化脚本预建策略，td_writer 不负责建表，遇到表不存在时告警而非自动建表。
新增设备后，必须先执行子表建表操作，再启动采集，不依赖 td_writer 自动建表。
历史查询单点位数据时，默认使用 FILL(PREV) 填充 NULL，接口提供 fill_mode 参数供前端按需切换。
Redis Stream 默认 shard_count = 4，并固化配置，不允许运行中直接修改。
Redis Stream 追加时必须设置 MAXLEN ~ 近似裁剪。
Redis 必须开启 AOF 持久化（appendonly yes，fsync everysec），保护 Stream 数据不丢失。
每条 Stream 消息超过最大重试次数（td_writer_pending_max_retry）后转入死信队列 rt:dlq:{device_type}:{group_code}，并触发告警。
点位 config_version 变更操作必须在 MySQL 事务中原子完成，包括点位变更和 config_version 递增。
所有 Stream 分片均需在部署初始化时创建 Consumer Group（td_writer_cg），不依赖 td_writer 自动创建。
realtime-init 默认只返回 latest，window 按需单独查询，window 接口需传 scan_rate_ms 参数。
后端 WebSocket 推送采用定时轮询 Redis latest 方式，首次推送完整快照，后续推送 diff。
采集模块按 device + group + scan_rate 组织任务。
后端接口按 device_code + group_code 读取 Redis 实时数据。
历史查询根据 var_id 映射到 TDengine 表和列。
```

这套方案对现有表结构改动较小，同时能支持当前每台设备 1000 点，以及后续单设备 5000 点、10000 点以上的扩展需求。

---

## 附录：代码审查问题与修复记录（Codex + 人工复查）

> 本节记录两轮代码审查（Codex 自动审查 + 人工复查）发现的问题及其修复方式。
> 所有问题均已修复并提交，可作为验收依据。

---

### 第一轮（Codex 审查）发现的问题及修复

#### P0-1：`create_point` 未分配 `point_group_id / group_index / td_column_name`

**问题**：新增点位时只插入旧字段，三个 v0.6 必需字段缺失，新点位进不了 Redis/TDengine 链路。  
**涉及文件**：`backend/app/services/catalog.py`  
**修复**：新增 `_allocate_point_slot_for_update()` 方法，在事务中 `SELECT FOR UPDATE` 锁定分组行，分配 `group_index`（= `next_column_index`）和 `td_column_name`（= `v{index:04d}`），INSERT 时写入三个字段，写入后调用 `_mark_point_created()` 更新分组计数和 `config_version`。

#### P0-2：`delete_point` 使用物理删除

**问题**：直接 `DELETE FROM device_variable`，不触发分组计数/版本更新，collector/writer 无法感知变更。  
**涉及文件**：`backend/app/services/catalog.py`  
**修复**：改为软删除（`deleted=1`），新增 `_soft_delete_points()` 方法，在事务中 `SELECT FOR UPDATE` 锁定 `device_point_group`，执行 UPDATE 将 `active_point_count / disabled_point_count / deleted_point_count` 及 `config_version` 一并递增。批量删除 `batch_delete_points` 同步改造。

#### P0-3：多处用 `dv.group_id` 代替正确字段 `dv.point_group_id`

**问题**：`monitor.py`、`catalog.py`、`history.py`、`config_db.py` 多处 JOIN 条件或查询字段写成 `dv.group_id`，在实际 schema 下会失败或静默返回空。  
**涉及文件**：上述四个文件  
**修复**：全部改为 `JOIN device_point_group AS dpg ON dpg.id = dv.point_group_id`，相关查询字段同步更正。

#### P1-1：Collector 只加载 `is_storage=1` 的点位

**问题**：`config_db.py` 加载阶段过滤 `is_storage == 1`，导致 `is_storage=0` 的点位进不了 Redis `latest` 和 `window`，违反设计要求（latest/window 应包含所有点位）。  
**涉及文件**：`collector/core/config_db.py`、`collector/collector/executor.py`  
**修复**：`config_db.py` 加载阶段只过滤 `enable=1, deleted=0`；`executor.py` 写 Stream 时才过滤 `is_storage=0` 和 `var_type=STRING`。

#### P1-2：Writer 版本不一致时仍 ACK，存在丢数据风险

**问题**：`mysql_mapper.py` 的 `load()` 出错返回 `None`，`consumer.py` 后续随即 XACK，消息在不确定映射状态下被消费。  
**涉及文件**：`writer/consumer.py`、`writer/mysql_mapper.py`  
**修复**：`get_or_load()` 检测 `config_version` 不一致时返回 `None`；`consumer.py` 收到 `None` 直接 `return`（不 ACK），消息保留为 pending，等待 XAUTOCLAIM 重新处理；DLQ 写入成功才 ACK；批量 flush 按子表分别 ACK，单表失败不影响其他表。

#### P1-3：`realtime-init` Redis 空时回退查 TDengine

**问题**：Redis key 不存在时接口 fallback 到 TDengine 旧查询，可能把历史陈旧值伪装成实时值。  
**涉及文件**：`backend/app/services/monitor.py`  
**修复**：`get_realtime_init()` 改为只读 Redis；`_get_device_latest_from_redis()` 返回空 dict 时，`latest_point_map` 保持为空，`_resolve_last_collect_time({})` 返回 `None`，`_build_connect_status(status, None)` 返回 `"offline"`，所有点位值填充为 `"-"`，符合设计规范"Redis key 不存在应标记离线"。

#### P1-4：前端实时页仍使用旧 WebSocket 协议

**问题**：页面仍连 `/ws/realtime`，发送 `deviceIds`，不发送 `device_code/group_code`；分组切换只改本地状态，不重新订阅。  
**涉及文件**：`frontend/src/api/device-monitor.ts`、`frontend/src/views/device_monitor/realtime/index.vue`  
**修复**：`getRealtimeWebSocketUrl()` 改为返回 `/ws/realtime-push`；订阅消息格式改为 `{action, device_code, group_code}`；`switchPointGroup()` 在 WebSocket 连接状态下发送 unsubscribe/subscribe；新增 `useRealtimeWs.ts` composable（含分组切换、断线重连、页面可见性恢复逻辑）。

#### P2-1：新接口前后端路径和字段名不一致

**问题**：前端请求 `/api/device/{deviceCode}/point-groups`，后端路由实际是 `/api/device-monitor/device/{device_code}/point-groups`；字段名 camelCase/snake_case 混用。  
**涉及文件**：`frontend/src/api/device-monitor.ts`、`frontend/src/types/api/device-monitor.d.ts`、`backend/app/api/routes/catalog.py`、`backend/app/services/catalog.py`  
**修复**：统一路径前缀为 `BASE_URL`（= `/api/device-monitor`）；后端 `get_device_point_groups()` 返回 `group_code / active_point_count`，前端类型定义 `PointGroup` 同步使用 `group_code / active_point_count`。

---

### 第二轮（人工复查）发现的问题及修复

#### P1-5：前端 `handleWsMessage` 未处理 `type: "offline"`

**问题**：`ws_push.py` 的 `_push_loop` 在 Redis key 不存在时向客户端推送 `{type: "offline", device_code: ...}`；`handleWsMessage` 只有 `snapshot / diff / update` 三个分支，没有 `offline` 分支。设备在监控期间掉线时，前端不更新任何离线提示，页面持续展示最后一次的旧值，无视觉反馈。  
**涉及文件**：`frontend/src/views/device_monitor/realtime/index.vue`  
**修复**：
1. 新增响应式变量 `deviceOffline = ref(false)`。
2. 在 `handleWsMessage` 顶部新增 `offline` 分支：验证 `device_code` 归属后，将 `deviceOffline` 置 `true`，清空 `realtimeValues` 和 `realtimeQuality`（避免展示陈旧值）。
3. 在 `snapshot/diff` 分支起始处将 `deviceOffline` 置 `false`（设备恢复后自动清除离线态）。
4. 在 `stopMonitor()` 和 `selectDevice()` 中重置 `deviceOffline`。
5. 离线横幅条件由 `!wsConnected` 扩展为 `!wsConnected || deviceOffline`，文字区分"WebSocket断开"和"设备数据离线"两种场景。

#### P2-2：`/ws/realtime-push` 不响应 `ping`

**问题**：前端每 15 秒发送 `{action: "ping"}` 心跳，`ws_realtime_push` 处理器只识别 `subscribe / unsubscribe`，不回复 `pong`，心跳机制完全失效。  
**涉及文件**：`backend/app/api/routes/monitor.py`  
**修复**：在消息循环中新增 `elif action == "ping": await websocket.send_text("pong")`，与前端 `if (event.data === 'pong') return` 检测逻辑对齐。

#### P2-3：旧端点 `/ws/realtime` 仍注册，违反设计规范 §17.6.1

**问题**：`register_realtime_websocket()` 仍被 `main.py` 注册，该端点使用每连接独立轮询模型（deviceIds 订阅），违反设计规范"不允许每个 WebSocket 客户端连接各自独立轮询 Redis"。前端已不使用该端点，但对外暴露可能导致误用。  
**涉及文件**：`backend/app/api/routes/monitor.py`、`backend/app/main.py`  
**修复**：删除 `register_realtime_websocket()` 函数及其全部辅助函数（`_load_realtime_snapshots`、`_normalize_device_ids`、`_build_snapshot_signature`、`_now_timestamp`）；从 `main.py` 的 import 和 `create_app()` 中移除对应调用；清理不再使用的 `asyncio / datetime / json` import 和 `DEFAULT_WS_POLL_INTERVAL` 常量。

#### P2-4：`MonitorService` 中残留不可达的 TDengine 查询死代码

**问题**：`get_realtime_init()` 重构为 Redis-only 后，以下五个方法在整个后端代码库中没有任何调用路径，属于死代码，且包含旧窄表 schema 查询（`WHERE var_seq = N`），与 v0.6 宽表 schema 不兼容：
- `get_realtime_snapshots()`
- `_fetch_latest_collect_times()`
- `_fetch_device_latest_collect_time()`
- `_fetch_latest_point_rows()`
- `_fetch_single_point_latest_row()`
- `_resolve_tdengine_database()`、`_resolve_point_subtable_name()`、`_normalize_identifier()`（仅被上述死代码调用）
- 模块级常量 `DEVICE_PREFIX_RE`、`IDENTIFIER_SANITIZE_RE`（仅被上述方法使用）

**涉及文件**：`backend/app/services/monitor.py`  
**修复**：删除全部上述方法和常量；移除孤立的 `import asyncio` 和 `import re`。`MonitorService` 现只保留 Redis/MySQL 链路，不再依赖 TDengine 旧 schema。
