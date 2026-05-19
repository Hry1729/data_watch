# TDengine 数据模型调整简述

> 更新时间：2026-05-19
> 适用范围：工业数据采集与监控平台当前 TDengine 设计基线

---

## 一、调整背景

历史上项目先后评估过两类 TDengine 模型：

```text
三库/设备超级表/每点位一个子表
```

以及：

```text
单库 -> 设备类型超级表 -> 设备子表 -> 多点位列
```

2026-05-18 至 2026-05-19 的压测结论表明，前一种“每点位一个子表”的模型会把表数量放大到 30 万级，100ms 高频写入时函数调用、stmt 绑定、taosAdapter 和 TDengine 调度开销过高，无法稳定达到目标吞吐。

因此当前正式口径调整为“宽表历史落库 + Redis 实时短窗口”：

- TDengine 负责历史时序落库和历史查询。
- Redis 计划负责前端 100ms 实时监控所需的最新快照和最近 N 行短窗口。
- MySQL 仍负责设备、点位、模板、协议等主数据。

---

## 二、当前统一模型

TDengine 当前统一采用：

```text
device_data 数据库 -> 设备类型超级表 -> 设备子表 -> 点位列
```

对应关系：

- 一个项目级 TDengine 数据库：`device_data`
- 一个设备类型对应一个超级表：`qc`、`asc`、`agv`
- 一台设备对应一个子表：如 `qc100`、`asc7011`、`agv801`
- 一个点位对应超级表中的一个数据列：如 `qc1`、`asc85`、`agv100`

示例：

```text
device_data
├── qc
│   ├── qc100
│   ├── qc101
│   └── qc128
├── asc
│   ├── asc7011
│   └── ...
└── agv
    ├── agv801
    └── ...
```

---

## 三、命名规则

### 1. 数据库名

固定为：

```text
device_data
```

### 2. 超级表名

按设备类型小写命名：

- `qc`
- `asc`
- `agv`

### 3. 子表名

与 MySQL `device.device_code` 保持一致，统一小写并规范为 TDengine 标识符。

示例：

- `qc100`
- `qc128`
- `asc7011`
- `agv801`

### 4. 点位列名

列名由设备类型和 MySQL `device_variable.var_seq` 生成：

```text
{device_type_lower}{var_seq}
```

示例：

- QC 的 `var_seq=1` -> `qc1`
- ASC 的 `var_seq=85` -> `asc85`
- AGV 的 `var_seq=100` -> `agv100`

说明：

- `var_seq` 是设备内点位序号，也是同设备类型宽表列的稳定来源。
- 不使用中文点位名、PLC 地址或 MySQL 自增主键作为 TDengine 列名。
- 后续点位新增时，只需根据 `device_variable` 追加缺失列。

---

## 四、表结构约定

### 1. 超级表字段

TDengine 历史表只保存点位数值，不再保存 `quality`。

```sql
CREATE DATABASE IF NOT EXISTS device_data;

CREATE STABLE IF NOT EXISTS device_data.qc (
    ts TIMESTAMP,
    qc1 DOUBLE,
    qc2 DOUBLE,
    ...
) TAGS (
    device_code VARCHAR(32)
);
```

### 2. 子表字段

子表继承所属超级表的全部点位列：

```sql
CREATE TABLE IF NOT EXISTS device_data.qc100
USING device_data.qc TAGS ('qc100');
```

说明：

- TDengine 超级表必须声明 TAGS，因此保留 `device_code` TAG。
- 业务上不依赖 TAG 区分点位，点位已经由列名表达。
- 某台设备没有对应点位时，该列可保持 `NULL`。

---

## 五、动态列策略

collector 初始化 TDengine 时必须以 MySQL `device_variable` 为事实源：

1. 读取启用设备与需要存储的点位。
2. 按设备类型归集所有 `var_seq`。
3. 对每个设备类型超级表检查已有列。
4. 对缺失点位执行 `ALTER STABLE ... ADD COLUMN ...`。
5. 为每台设备创建或确认对应子表存在。

示例：

```sql
ALTER STABLE device_data.qc ADD COLUMN qc1001 DOUBLE;
```

约束：

- 只追加缺失列，不删除已有列。
- 列类型首版统一使用 `DOUBLE`，布尔、整数等在写入侧转换为数值。
- 点位删除或停用不立即删 TDengine 列，避免影响历史查询和 schema 抖动。
- 若未来必须收缩列，应作为离线维护任务单独评审。

---

## 六、写入与查询口径

### 1. 实时写入

前端 100ms 实时监控不直接依赖 TDengine 小批量写入。

推荐链路：

```text
collector 采集
-> Redis 最新快照 / 最近 N 行短窗口
-> backend realtime-init / WebSocket
```

### 2. 历史落库

TDengine 采用批量写入：

```text
Redis / collector 批量缓冲
-> 每 500ms 写 5 行，或每 1000ms 写 10 行
-> TDengine 宽表设备子表
```

### 3. 历史查询

历史查询按设备子表和点位列定位：

```sql
SELECT ts, qc1, qc2
FROM device_data.qc100
WHERE ts >= ? AND ts < ?
ORDER BY ts;
```

---

## 七、quality 口径

当前 TDengine 历史宽表不再存储 `quality` 字段。

原因：

- 压测目标是高频海量点位写入，`quality` 成对列会使列数和写入负载翻倍。
- 当前业务判断中，数据异常、bad、离线、超时可由实时态、规则引擎、采集异常和数值本身识别。
- 历史库首要职责是保留数值时间序列。

后续如确实需要历史质量追溯，再单独评审是否新增：

- 独立质量超级表；
- 点位 `_quality` 成对列；
- 或告警 / 异常事件表。

---

## 八、压测结论摘要

本轮关键结论：

- 30.5 万点位子表模型平均吞吐约 8 万点/秒，无法满足 100ms 全量写入。
- 宽表模型在 305 台设备、每台 1000 点位、`1000ms x 10行` 下基本达到 305 万点/秒目标。
- 目标机 152 台设备测试中，`1000ms x 10行` 稳定，`500ms x 5行` 可用，`100ms x 1行` 明显不达标。

当前建议：

- 生产默认：Redis 承接 100ms 实时态与待入库 Stream，独立 TDengine 入库模块使用 `1000ms x 10行` 批量落历史。
- 如需更低历史延迟：可试用 `500ms x 5行`，但需要观察 lag 与内存。
- 不建议：每 100ms 直接向 TDengine 提交一批小写入。
