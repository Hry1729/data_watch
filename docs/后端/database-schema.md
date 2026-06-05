# 数据库 Schema 设计说明 (初稿)

本文档整理了系统在 MySQL、TDengine 以及 Redis 中的详细数据模型与 Schema 结构，说明了各表的字段、类型及核心作用。

---

## 一、 MySQL 数据模型

系统在 MySQL 中存放元数据、业务配置、系统审计日志与运行策略。核心表分为用户权限、物理设备台账、报警规则记录以及运行运维日志四类。

*注：在当前后端项目中，仅用户权限与系统日志策略表（在 [models/](file:///D:/python/device_alarm/data_watch/backend/app/models/) 目录下）定义了对应的 ORM 实体（使用 SQLAlchemy）。物理设备台账表（`device`、`device_variable` 等）以及报警规则与记录类表在后端项目中**完全没有 ORM 实体**，底层逻辑直接在 Service 侧裸写 SQL 语句并拼装执行。*

### 1. 用户权限与数据安全类 (8张表)

*   **`sys_user` (系统用户表)**
    *   `id` (BigInteger, PK): 用户 ID，自增。
    *   `user_name` (String(64), Unique): 用户登录名，不可为空。
    *   `password_hash` (String(255)): 密码哈希值。
    *   `nick_name` (String(64)): 用户昵称。
    *   `email` (String(128)), `phone` (String(32)), `gender` (String(16)): 基本联系信息。
    *   `status` (String(16)): 用户状态 ("1" 为启用，"0" 为禁用)。
    *   `last_login_time` (DateTime), `last_login_ip` (String(64)): 最后登录审计。
    *   `deleted` (Boolean): 软删除标记。

*   **`sys_role` (系统角色表)**
    *   `id` (BigInteger, PK): 角色 ID。
    *   `role_code` (String(64), Unique): 角色标识码 (如 `admin`, `operator`)。
    *   `role_name` (String(64)): 角色显示名称。
    *   `enabled` (Boolean): 启用状态。

*   **`sys_user_role` (用户角色关联表)**
    *   `id` (BigInteger, PK): 自增主键。
    *   `user_id` (BigInteger, FK): 关联 `sys_user.id` (外键级联删除)。
    *   `role_id` (BigInteger, FK): 关联 `sys_role.id` (外键级联删除)。
    *   *约束*: `uq_sys_user_role_user_role` 联合唯一。

*   **`sys_menu` (系统菜单/路由表)**
    *   `id` (BigInteger, PK): 菜单 ID。
    *   `parent_id` (BigInteger, FK): 父菜单 ID (支持级联删除)。
    *   `name` (String(128), Unique): 路由标识名。
    *   `path` (String(255)): 页面路由路径。
    *   `component` (String(255)): 前端组件路径。
    *   `title_key` (String(255)): 国际化 Key 值。
    *   `menu_type` (String(32)): 节点类型 ("menu" / "button")。
    *   `enabled` (Boolean), `sort` (Integer): 控制显隐与排列顺序。

*   **`sys_permission` (系统权限表)**
    *   `id` (BigInteger, PK): 权限 ID。
    *   `permission_code` (String(128), Unique): 权限唯一标识编码。
    *   `permission_name` (String(128)): 权限名称。
    *   `menu_id` (BigInteger, FK): 隶属的菜单 ID (外键置空 `SET NULL`)。
    *   `auth_mark` (String(128)): 接口拦截鉴权标识。

*   **`sys_role_menu` & `sys_role_permission` (角色菜单关联表 & 角色权限关联表)**
    *   建立角色与菜单、按钮权限的多对多映射关系，外键均支持级联删除，以实现多级基于角色的访问控制 (RBAC)。

*   **`sys_data_permission` (数据权限表)**
    *   `id` (BigInteger, PK): 自增主键。
    *   `role_id` (BigInteger, FK): 关联 `sys_role.id`。
    *   `scope_type` (String(32)): 权限范围类型 ("all", "group" 等)。
    *   `scope_value` (String(128)): 权限具体值。

---

### 2. 物理模型与物理设备台账 (6张表)

*   **`comm_protocol` (通讯协议表)**
    *   `id` (BigInteger, PK): 协议 ID。
    *   `protocol` (String(20), Unique): 协议名称 (如 S7, Modbus, OPC_DA)。
    *   `port` (Integer): 默认驱动端口号。

*   **`device_type` (设备类型表)**
    *   `id` (BigInteger, PK): 类型 ID。
    *   `type_code` (String(50), Unique): 设备类型英文简称 (如 `qc`, `agv`)。
    *   `type_name` (String(100)): 类型名称 (如 “起重机”, “智能小车”)。

*   **`device_group` (设备物理分组表)**
    *   `id` (BigInteger, PK): 物理群组 ID。
    *   `group_code` (String(50), Unique): 群组编码 (如 `workshop_01`)。
    *   `group_name` (String(100)): 群组名称。

*   **`device` (设备台账主表)**
    *   `id` (BigInteger, PK): 设备唯一 ID。
    *   `device_code` (String(20), Unique): 唯一的物理设备编号 (如 `qc100`)。
    *   `device_name` (String(20)): 设备名称。
    *   `type_id` (BigInteger, FK): 关联 `device_type.id`。
    *   `group_id` (BigInteger, FK): 关联 `device_group.id`。
    *   `enable` (TinyInt): 物理设备采集开关 (0=关闭，1=启用)。
    *   `status` (TinyInt): 最新在线状态 (0=离线，1=在线)。
    *   `protocol_id` (BigInteger, FK): 绑定的采集协议，关联 `comm_protocol.id`。
    *   `endpoint` (String(20)): PLC/设备端点主 IP (如 `192.168.1.100`)。
    *   `rack` (Integer), `slot` (Integer): PLC 机架号与插槽号参数。
    *   `scan_cycle` (Integer): 默认设备扫描周期 (单位 ms)。
    *   `variable_config_version` (Integer): 设备点位配置的版本号，每次点位元数据被修改时自动递增，用于触发写入缓存刷新。

*   **`device_point_group` (设备点位分组表)**
    *   由于 TDengine 宽表单表列数限制 (本系统单表定义上限为 3072 列)，因此设备下关联的点位需要以“点位分组”形式进行管理。
    *   `id` (BigInteger, PK): 分组 ID。
    *   `device_id` (BigInteger): 关联 `device.id`。
    *   `group_code` (String(16)): 分组编码，默认主分组为 `g001`。
    *   `max_point_count` (Integer): 最大可用列上限数，固定为 3072。
    *   `used_column_count` (Integer): 累计分配的列数（含已软删除列，列位不回收，防止数据错乱）。
    *   `next_column_index` (Integer): 下一个可用列位序号，新增点位分配时从此字段自动累加。
    *   `config_version` (Integer): 本分组点位配置版本号，发生点位增减时自动递增，用于写入器加载映射时版本对齐。
    *   *约束*: `uq_device_group` (`device_id`, `group_code`) 联合唯一。

*   **`device_variable` (设备点位变量表)**
    *   `id` (BigInteger, PK): 变量主键 ID (即 `var_id`)。
    *   `device_id` (BigInteger, FK): 关联 `device.id`。
    *   `point_group_id` (BigInteger): 关联 `device_point_group.id`。
    *   `group_index` (Integer): 该点位在对应时序宽表中的列序号位置 (1 ~ 3072)。
    *   `td_column_name` (String(8)): 在 TDengine 物理表中的列字段名，由当前点位配置生成并持久化（如 `qc1`、`asc85`、`agv100`）。
    *   `var_seq` (Integer): 变量的物理业务序号（Modbus寄存器序列/PLC唯一逻辑键）。
    *   `var_name` (String(50)): 变量显示名称。
    *   `var_type` (String(20)): 物理数据类型 (如 `BOOL`, `REAL`, `DINT`, `STRING` 等)。
    *   `var_address` (String(50)): PLC 物理地址 (如 `DB100.DBX0.0`)。
    *   `is_alarm` (TinyInt), `is_storage` (TinyInt): 报警检测开关与持久化存储开关。
    *   `enable` (TinyInt), `deleted` (TinyInt): 启用与软删除标志。
    *   *约束*: `uq_device_variable_device_var_seq` 保证设备下 `var_seq` 唯一；`uq_device_variable_device_var_address` 保证设备下 `var_address` 唯一。

---

### 3. 报警规则记录类 (4张表)

*   **`alarm_level` (报警级别字典表)**
    *   `level_code` (String(10), Unique): 级别码 (如 `CRITICAL`, `WARN`)。
    *   `severity` (TinyInt): 严重程度数值，级数越大越严重。

*   **`alarm_rule` (报警规则定义表)**
    *   配置基于点位值的报警判断标准。
    *   `rule_code` (String(64), Unique): 报警规则唯一编码。
    *   `device_id` (BigInteger, FK), `point_id` (BigInteger, FK): 关联的具体设备与变量。
    *   `compare_op` (String(16)): 比较算子 (如 `>`, `<`, `=`)。
    *   `threshold_value` (String(128)), `recover_value` (String(128)): 阈值与恢复阈值。
    *   `deadband_value` (Decimal): 回差消除死区值。
    *   `hold_seconds` (Integer): 报警延迟触发过滤秒数（滤波消除抖动）。

*   **`alarm_active` (活动报警快照表)**
    *   实时存储当前正处于激活（未恢复）状态的报警记录。当报警发生时在此表生成快照，报警恢复时删除该快照并写回历史表中。

*   **`alarm_record` (历史报警事件表)**
    *   用来归档记录所有的报警历史事件。具有高频索引（例如在 `device_id` + `start_time` 上），包含记录的累积发生次数 `occur_count`，以及恢复时间 `recover_time`、确认人 `ack_by` 等业务信息。

---

### 4. 运行日志与运维配置类 (5张表)

*   **`operation_log` (用户操作审计表)**
    *   记录用户在 Web 前端发起的配置修改、设备增删等关键操作审计，包含 `detail_json` JSON 列存储修改前后对比。

*   **`system_log` (系统运行日志表)**
    *   存储后台服务运行异常、PLC 连接断开等系统运行事件日志，支持基于 `trace_id` 的调用链查询。

*   **`log_retention_policy` (日志清理保留策略表)**
    *   固定单行策略配置（ID = 1）。
    *   `enabled` (TinyInt): 清理调度器主开关。
    *   `retention_days` (Integer): 日志最大保留天数（默认 7 天）。
    *   `max_total_mb` (Integer): 日志表累积物理容量大小上限（默认 1024MB）。
    *   `cleanup_time` (String(8)): 每天自动执行清理的时间（默认 `"03:00"`）。
    *   `capacity_check_interval_minutes` (Integer): 表大小超限检测周期。
    *   `batch_size` (Integer): 单次事务删除日志的上限数量。
    *   `debug_db_enabled` (TinyInt): 生产环境 DEBUG 日志是否落库开关。
    *   `debug_enabled_until` (DateTime): 临时开启 DEBUG 日志落库的自动截止到期时间。
    *   `last_cleanup_result_json` (JSON): 上一次执行日志清理时的结果详情记录。
    *   `debug_reason` (String(255)): 开启调试模式的原因描述。
    *   `debug_enabled_by_id` (BigInteger), `debug_enabled_by_name` (String(64)): 开启调试的操作用户 ID 与名称。
    *   `last_cleanup_time` (DateTime): 上一次成功执行清理的时间。
    *   `create_by_id` (BigInteger), `create_by_name` (String(64)): 策略创建用户审计。
    *   `update_by_id` (BigInteger), `update_by_name` (String(64)): 策略最后更新用户审计。
    *   `create_time` (DateTime), `update_time` (DateTime): 创建与更新时间戳。

*   **`plc_connection_log` (PLC连接事件表)** 和 **`cross_db_task` (跨库双写重试异步任务表)**。

---

## 二、 TDengine 宽表架构

本系统采用 TDengine 作为海量工业时序数据的专用存储引擎。考虑到时序数据的超高吞吐，系统对各设备类型的点位采用了“宽表”的设计模式。

### 1. 超级表 (STable) 结构定义
超级表为某种设备类型的共用模板。库名为 `device_data`。超级表的命名格式为：
`sd{device_type}`（例如 `sdqc`），即以 `sd` 开头并紧跟设备类型英文名缩写，且不带物理分组后缀。

超级表设计规范如下：
*   **时序主键**：`ts TIMESTAMP`，代表数据上报的毫秒级时间戳。
*   **数据列 (Data Columns)**：采用纯扁平化设计，列名来自 MySQL `device_variable.td_column_name`，由当前设备点位配置决定。所有数据列类型均定义为 `DOUBLE`，用以容纳整型、浮点型、布尔型等各类数值，字符类型在入库前会被排除。
*   **标签列 (Tag Columns)**：
    *   `device_code NCHAR(32)`：用于过滤和区分具体物理设备的子表标签（也是子表的路由依据）。

### 2. 子表 (Table) 结构定义
对应每台具体设备，在 TDengine 中均有其独立的子表，属于对应的超级表。子表的命名格式为：
直接使用设备编码 `device_code` 本身（例如 `qc100`），即不带 `t_` 前缀，亦无物理分组后缀。

子表采用以下方式声明以实现自动建表：
```sql
CREATE TABLE IF NOT EXISTS device_data.qc100 
USING device_data.sdqc TAGS ('qc100')
```

### 3. `var_seq` 字段的作用与重要性

在 `device_variable` 表中，`var_seq` 作为关键字段拥有极高的架构意义：
1.  **物理采集唯一标识键**：`var_seq`（Canonical Sequence）作为点位的物理规范序号，在工业协议通信和边缘网关配置中充当物理点位的“门牌号”，保证同一个物理设备下的 `var_seq` 拥有全局唯一的防重约束（通过唯一性索引）。
2.  **物理存储与业务检索的解耦桥梁**：
    时序库（TDengine）的宽表中使用 `device_variable.td_column_name` 保存数据，这有助于实现动态建表和极速列扫描。然而，在应用层、前端监控报表、或历史数据查询的 API 接口中，用户是通过“物理点位序号 `var_seq`”（如点位 3、点位 150）来进行数据查询的。
    **`var_seq` 在这里完成了屏蔽底层数据库列改动的职责**：后端业务通过 `device_code` and `var_seq` 查询 MySQL 元数据，翻译得出对应的物理列 `td_column_name`，从而拼接出 TDengine 的 SQL 发起检索。因此，时序表的列位置发生重构时，业务层感知完全不受影响。

---

## 三、 Redis 三层缓冲及队列结构

Redis 被部署为边缘采集到中心存储的数据桥梁，用以提供低延迟读写和保护底层关系型与时序数据库。其核心设计由三层不同的缓冲结构构成：

### 1. 最新状态哈希缓 (rt:latest)
*   **数据结构**：`HSET` 哈希结构。
*   **Key 结构**：`rt:latest:{device_code}:{group_code}`（如 `rt:latest:qc100:g001`）。
*   **Field 与 Value**：
    *   `Field` 值为点位的 `var_id`（MySQL 点位的主键字符串）。
    *   `Value` 值为 JSON 序列化的值与品质字典：`{"v": "23.5", "t": 1716283728000, "q": 192}`。
        *   `v`: 点位数值的字符串表示。
        *   `t`: 设备采集到该帧的毫秒级 epoch 时间戳。
        *   `q`: 通信品质质量状态（Quality Code）。
*   **生命周期**：**永不过期**。只有在设备变更、软删除或点位重置时由管理服务进行显式删除。此层旨在为前端实时监控和报警逻辑提供毫秒级、免穿透的高频实时查询。

### 2. 短窗口历史列表缓 (rt:window)
*   **数据结构**：`LPUSH` 队列列表。
*   **Key 结构**：`rt:window:{device_code}:{group_code}:{scan_rate_ms}`
*   **逻辑机制**：
    *   每次采集器采集到一整帧设备状态时，将该完整帧的 JSON 字符串整体 `LPUSH` 到 List 中。
    *   紧接着对该队列执行 `LTRIM key 0 9`。
*   **控制界限**：**强制仅保留最近的 10 帧数据**（通常代表最近 10 分钟或几秒内的极速历史记录）。
*   **应用场景**：为前端极速曲线提供数据源。用户打开设备监控时，可以直接读取这 10 帧数据秒级绘制最近历史曲线，避免了对 TDengine 发起频繁的历史统计查询。

### 3. 落库异步流队列 (rt:stream)
*   **数据结构**：`XADD` 消息流（Redis Stream）结构。
*   **Key 结构**：`rt:stream:{device_type}:{group_code}:{shard_no}`
*   **多片 Sharding 与确定性分片设计**：
    为了在高频海量点位上报下规避 Redis 单线程在单个 Stream 队列上的并发写性能瓶颈，系统引入了分片（Sharding）架构：
    *   系统预先配置分片总数（如 `stream_shard_count = 4`）。
    *   为了确保特定设备的采集流始终能写入同一个 Stream 分片中（以方便单路消费者消费，且防止因 Python 解释器在每次重启时产生的随机 `PYTHONHASHSEED` 导致的分片漂移），分片号采用稳定的 CRC32 物理哈希计算：
        `shard_no = zlib.crc32(device_code.encode("utf-8")) % shard_count`。
*   **消息体 Payload**：
    *   `device_code`、`device_type`、`group_code`
    *   `ts`、`scan_rate`、`config_version`
    *   `values`：过滤了非必要落库数据（如 `is_storage = 0`）和字符类型后的实时点位数据 JSON 映射 `{var_id: string_value}`。
*   **持久性与防漏机制**：
    *   Stream 使用 `maxlen=stream_maxlen`（带有 `approximate=True` 的近似修剪）防止 Redis 内存暴涨。
    *   写入器（Writer）利用 `xreadgroup` 进行组消费。消费成功后发送 `XACK` 剔除 PEL 记录。
    *   若发生写入失败或网络抖动，未被 ACK 的消息会保留在 PEL（挂起消息列表）中，由消费服务内部的 `drain_pending`（支持 XAUTOCLAIM 及 XPENDING 备份路径）自动重新 claim 并发起重试或归入死信队列 (DLQ)，实现数据“零丢失”。

---

## 三、 相关源码参考

*   **后端 ORM 模型**：[models 目录](file:///D:/python/device_alarm/data_watch/backend/app/models/)
*   **采集器数据 ORM**：[config_db.py](file:///D:/python/device_alarm/data_watch/collector/core/config_db.py)
*   **时序库写入器与命名映射**：[tdengine_writer.py](file:///D:/python/device_alarm/data_watch/writer/tdengine_writer.py) | [mysql_mapper.py](file:///D:/python/device_alarm/data_watch/writer/mysql_mapper.py)
