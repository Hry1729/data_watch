# 采集端 collector 设计文档
<!-- xiaohu-illustration:collector-readme-01-pump -->
![小狐配图：边缘采集泵](../../images/xiaohu-collector-readme-01-pump.png)

> collector 是平台的数据源头：按周期从现场 PLC/OPC 设备采集点位，解析为统一数据点后写入 Redis，供入库端与后端消费。本文覆盖其架构、核心组件、采集流水线、对外契约、配置与扩展方式。
>
> 上游全局视图见《01-总体设计/系统架构总览》；Redis 数据结构的完整契约见《03-数据与契约/数据库/Redis结构与字段》。

---

## 1. 概述

collector 是一个**单 OS 进程、多协程、多线程池**的采集服务，运行在边缘侧、贴近现场设备。它不是“每台设备一个进程”，也不是“单线程轮询”：主进程内由 asyncio 调度周期任务，并把 PLC 读、解析、Redis 写入等阻塞/耗 CPU 阶段交给有界线程池；运行时使用 free-threaded Python 3.14t，使这些显式线程池中的 Python 代码具备更好的多核并行空间。

> 进程边界说明：`data-watch-collector.exe` 自身是单进程部署形态；OPC DA proxy / sidecar 是由 agent 管理的外部辅助进程，不计入 collector 主进程，也不作为 collector 的子进程内存口径。

collector 的职责边界是：

- **采集**：按设备配置的扫描周期，通过协议驱动批量读取 PLC/OPC 点位；
- **解析**：把合并读块的原始字节/字按点位定义还原为具体值；
- **下发**：将结果写入 Redis 的实时态、滑动窗口与消费队列三种结构；
- **自治**：设备连接的建立、健康检查、断线重连、配置热更新，全部在进程内自管。

collector **只写 Redis**，不直接落库；入库由下游 writer 异步完成。两者通过 Redis 解耦，无代码级调用关系。

入口：`collector/main.py`（`main()`）。

---

## 2. 设计目标与范围

| 目标 | 实现要点 |
|------|----------|
| 高频、多设备并发采集 | 单进程部署 + asyncio 调度 + 有界线程池；free-threaded Python 3.14t 提供线程池多核并行空间 |
| 采集互不干扰 | 单设备 in-flight 串行；设备级熔断与独立重连 |
| 削峰、不堆积 | crc32 相位错开；周期补偿调度；三段有界池背压 |
| 配置可热更新 | 10 秒轮询 MySQL，按设备增/删/改增量重建，不停整机 |
| 数据不谎报 | generation 失效校验贯穿读/写；写序保证 latest 最后落 |

**不在本模块范围**：数据落库（writer）、对前端供数（backend）、进程启停管控（agent）。

---

## 3. 核心概念与术语

| 术语 | 含义 |
|------|------|
| **设备 Device** | 一台被采集的物理/逻辑设备，绑定一种协议与一组变量。`collector/device/device.py` |
| **变量 / 点位 Variable** | 设备上的一个采集点，含地址、数据类型、扫描周期、点位分组、是否入库 |
| **扫描任务 ScanTask** | 同一扫描周期下、同设备的一批变量 + 合并读块。一个设备可有多个周期 → 多个 ScanTask |
| **合并读块 ReadBlock** | 把连续地址的多个点位合并为一次批量读取，降低 I/O 次数。`collector/drivers/base.py` |
| **采集任务 CollectTask** | 调度的最小单位 = 一个设备的一个 ScanTask；构造时把扫描元数据冻结为不可变快照 |
| **点位分组 group_code** | 点位按业务分组组织，Redis 键以分组为粒度 |
| **generation 代次** | 设备的单调递增版本号，用于热重载时让旧周期数据失效 |
| **三类协议** | 普通轮询（s7/ge_srtp/opcua）、外部推送 external-push、中心采集 center（经代理进程） |

---

## 4. 架构与目录结构

```
collector/
├── main.py                  # 入口：配置加载 + CollectorApp 装配 + 生命周期
├── collector/               # 采集核心
│   ├── scheduler.py         # TaskScheduler 周期调度
│   ├── task.py              # CollectTask / ScanTaskSnapshot 任务与快照
│   ├── executor.py          # TaskExecutor 采集执行（读→解析→写）
│   └── bounded_executor.py  # BoundedExecutor 有界线程池（背压）
├── device/
│   ├── device.py            # Device / ScanTask / Variable 模型，读块合并
│   └── manager.py           # DeviceManager 设备/驱动生命周期、generation、重连、端口映射
├── drivers/                 # 驱动**契约与注册表**（驱动实现不在本目录）
│   ├── base.py              # BaseDriver / DriverSample / ReadBlock / MISSING_RESULT
│   ├── context.py           # DriverContext（注入配置、日志、指标等）
│   ├── manifest.py          # driver.toml 清单解析与校验
│   └── registry.py          # DriverRegistry：发现/校验/加载受控驱动包（唯一驱动入口）
│                            # 说明：s7_driver.py / ge_srtp_driver.py / opcua_driver.py /
│                            # opcda_center_driver.py 等**旧内置驱动文件已不存在**；
│                            # 实现现位于 packages/<id>/（见下方“驱动包”一节），
│                            # 由 registry 按 DATAWATCH_DATA_ROOT/drivers（或仓库 packages/）发现。
├── sidecars/                # C# 边车（OPC DA 经 Windows COM）
│   ├── opcda_proxy_service/ # 代理服务（center 采集）
│   ├── opcda_push_service/  # 推送服务（external-push）
│   └── opcda_sidecar/       # 探针
├── storage/
│   ├── redis_client.py      # Redis 连接池
│   └── redis_writer.py      # RedisWriter 三结构写入
├── core/                    # 配置、日志、重试、系统日志、OPC DA 映射等
├── monitor/                 # health / performance / ingest_server
└── data_queue/types.py      # DataPoint 等数据类型
```

组件协作关系：

```mermaid
flowchart TD
    MAIN[CollectorApp<br/>main.py] --> DM[DeviceManager]
    MAIN --> SCH[TaskScheduler]
    MAIN --> EXE[TaskExecutor]
    MAIN --> RW[RedisWriter]
    SCH -->|每周期触发| EXE
    EXE -->|读| DM
    DM -->|工厂创建| DRV[协议驱动 BaseDriver]
    DRV -->|批量读| PLC[(PLC / OPC)]
    EXE -->|解析后写| RW
    RW --> REDIS[(Redis)]
    EXE -.parse 池/redis 池.-> POOL[BoundedExecutor]
    DM -.PLC 读池.-> POOL2[BoundedThreadPoolExecutor]
    ING[InterfaceIngestServer<br/>HTTP Push] -->|external-push 数据| RW
```

### 4.1 分层边界：核心 / 驱动 / 协议库

采集链路分三层，职责不要混谈：

| 层 | 谁 | 负责什么 | 不负责什么 |
|----|----|----------|------------|
| **采集核心** | `TaskScheduler` / `TaskExecutor` / `DeviceManager` / `RedisWriter` | 周期调度、错峰、单设备 in-flight、有界池背压、generation、读块合并编排、解析后写 Redis | 不直接调用 snap7 / 协议厂商 SDK |
| **协议驱动** | `BaseDriver` 实现（如 `S7Driver`） | `connect` / `disconnect` / `read(blocks)` / `write` / 可选 `ping`；把统一接口翻译成具体协议读写 | 不实现调度、不写 Redis、不管跨设备并发策略 |
| **协议库** | snap7、自研 GE SRTP、OPC UA 客户端等 | 驱动**内部**调用的底层读写（单点、连续段、连接） | 不被调度器直接引用 |

调用关系固定为：

```text
TaskScheduler
  → TaskExecutor.execute
      → driver.read(read_blocks)     # 只认 BaseDriver
          → 驱动内部再调 snap7 / 其它协议库
```

因此：调度器调度的是「驱动接口上的函数」，不是「snap7 的某个 API」。新增协议时实现并注册一个 `BaseDriver` 即可接入同一套并发与入库链路；协议库细节封装在驱动内部。

地址合并（连续点合成 `ReadBlock`）当前在核心的 `Device.build_scan_tasks`；解析还原在 `TaskExecutor._parse_result`。驱动侧主要交付「按块读回原始值」。

---

## 4.2 质量码体系

collector 从 v0.1.0 起支持 **质量码（Quality Codes）** 端到端传递：驱动读取点位数据时，除了返回数值和时间戳，还返回该数据点的质量状态，经 Redis Stream 透传给 writer 入库到 TDengine。

### 质量码定义

平台质量码是**冻结常量**，定义在 `collector/core/quality_codes.py`，并与 `writer/quality_codes.py`、
`backend/app/core/quality_codes.py` **同值**（三处必须一致，改动需三处同步）：

| 码值 | 常量名 | 含义 | 典型场景 |
|------|--------|------|----------|
| 0 | `QUALITY_GOOD` | 正常 | 值有效 |
| 2 | `QUALITY_UNCERTAIN` | 不确定 | 值可用但可信度下降 |
| 5 | `QUALITY_BAD` | 通用坏值 | 无法取得有效数据 |
| 6 | `QUALITY_STALE` | 陈旧值 | 保留了上一有效值且已过期（展示须带 stale/age） |
| 7 | `QUALITY_NOT_CONNECTED` | 未连接 | 驱动未建立连接 |
| 8 | `QUALITY_CONFIG_DECODE_ERROR` | 配置/解码错误 | 地址与数据类型不匹配、协议帧解不出 |
| 1 / 3 / 4 | （保留码 `RESERVED_QUALITY_CODES`） | 允许出现在接口中，但**不**擅自业务命名 | — |

两个判定函数**语义不同**，不要混用：

- `is_known_quality(code)`：是否属于上述**具名**业务码（不含保留码），且必须是**严格非 bool 的 int**；
- `is_valid_quality(code)`：**存储契约**允许 `0..255`。

> **协议原始质量码 ≠ 平台质量码**：协议原始码（如 OPC DA 的 quality bit field）放在
> `DriverSample.protocol_quality`，**仅供诊断**，不参与平台质量判定、当前不写入 Redis Stream；
> 平台质量码放在 `DriverSample.quality`。

### 数据结构

驱动返回的采集样本由 `DriverSample` 表示（`collector/drivers/base.py`，`@dataclass(frozen=True, slots=True)`）：

```python
@dataclass(frozen=True, slots=True)
class DriverSample:
    value: Any                                    # 采集值（数值/字符串/布尔）
    quality: int = QUALITY_GOOD                   # 平台质量码（默认 0=GOOD）
    source_timestamp: Optional[datetime] = None   # 协议源时间；缺失时回落采集时刻
    protocol_quality: Optional[int] = None        # 协议原始质量，仅供诊断
```

**字段只有以上四个**：没有 `var_id`、也没有 `timestamp`（点位由调用方按 variable 关联，时间用
`source_timestamp`）。写成 `DriverSample(var_id=..., timestamp=...)` 会直接抛
`TypeError: unexpected keyword argument 'var_id'`。

`value=None` 的语义（与执行器 `collector/collector/executor.py` 一致）：

- **本轮无新样本**（例如增量型驱动本轮无变化）：返回 `MISSING_RESULT` 哨兵，**不要**用 `None`
  表达；`MISSING_RESULT` 会发出一个显式的 `value=None + QUALITY_BAD` 标记点；
- **读取失败**：返回 `DriverSample(value=None, quality=<坏质量>)`（`QUALITY_BAD` /
  `QUALITY_STALE` / `QUALITY_NOT_CONNECTED` / `QUALITY_CONFIG_DECODE_ERROR`）。执行器会
  **计数并告警**（可见），但**不伪造**点位值；
- **裸 `None`**（未包 `DriverSample`）：会被归一化为 `QUALITY_BAD` 并告警——它**不等于**
  "无新样本"，语义同上一条；
- **合法零值**（`0` / `0.0` / `False` / `""`）与 `None` **互不相同**：零值保持 `QUALITY_GOOD`
  并原样写入。

### Redis Stream 契约

同一设备/分组 frame 的 ts 取该帧所有点 timestamp 的最大值；latest 中每个点的 t 保留自身 timestamp。驱动提供 source_timestamp 时作为该点时间，否则使用本轮采集时刻。

collector 写入 Redis Stream 时，使用 **contract v2**（`collector/storage/redis_writer.py`）：

```json
{
  "contract_version": 2,
  "device_code": "PLC001",
  "timestamp": 1722349200.123,
  "values": [123, 456],
  "qualities": [0, 193]  // 新增字段：与 values 一一对应
}
```

- `values` 和 `qualities` 数组长度必须相等；
- 若驱动返回的 sample 未设置 quality，默认为 `0`（GOOD）；
- writer 读取 stream 时，若 `contract_version < 2` 或缺少 `qualities` 字段，兼容填充 `0`。

### 驱动实现指南

**内置驱动** 已支持质量码：
- **S7 驱动**：通信失败返回 `QUALITY_BAD` / 地址与类型不匹配返回 `QUALITY_CONFIG_DECODE_ERROR`；
- **GE SRTP 驱动**：读取失败返回坏质量（按失败类型取 `QUALITY_BAD` / `QUALITY_NOT_CONNECTED`）；
- **OPC UA/DA 驱动**：平台质量码写 `quality`，协议原始状态码放 `protocol_quality`（仅诊断）。

**外置驱动** 若需支持质量码：
1. 导入（模块路径以运行期 `sys.path` 为准，`collector/` 即包根）：
   `from drivers.base import DriverSample` 与
   `from core.quality_codes import QUALITY_GOOD, QUALITY_BAD, ...`；
   ——**不存在** `collector.drivers.base`，也**不存在** `Q_GOOD` / `Q_BAD_*` 这类名称；
   按旧写法会分别得到 `ModuleNotFoundError` / `ImportError`（已在项目环境实测）；
2. `read()` 方法返回 `List[DriverSample]`，为每个样本设置 `quality` 字段；
3. 读取失败时返回 `quality=<坏质量常量>`（如 `QUALITY_BAD` / `QUALITY_NOT_CONNECTED`）+
   `value=None`，而不是抛异常或返回空列表；"本轮无新样本"用 `MISSING_RESULT` 表达。

详见：[外置驱动包制作指南](./外置驱动包制作指南.md) § 质量码支持。

### 下游传递

- **Writer**：从 Redis Stream 读取 `qualities` 数组，写入 TDengine **质量窄表** `sq{device_type}`（schema: `ts TIMESTAMP, var_id BIGINT COMPOSITE KEY, quality TINYINT`），与值宽表 `sd{device_type}` 平行存储；
- **Backend**：从 TDengine 质量窄表读取历史质量码，从 Redis `latest` 读取实时质量码（`{"v", "t", "q"}`），透传给前端；
- **Frontend**：在实时监控、历史曲线、点位列表中显示质量状态（Good / Uncertain / Bad），用颜色或图标区分。

---

## 5. 关键组件

### 5.1 CollectorApp（装配与生命周期）

`collector/main.py`。在构造期完成全部组件装配并注入依赖：性能指标、系统日志、专用 PLC 读线程池、DeviceManager、TaskScheduler、RedisWriter、parse/redis 有界池、TaskExecutor、健康检查、OPC DA 映射器、可选 HTTP 接入服务。

启动顺序（`start()` `collector/main.py`）：ping Redis → 注册设备 → 连接所有设备 → 为每设备每 ScanTask 建 CollectTask 并入调度 → 启动调度器 → 启动 HTTP 接入 → 健康检查 → 配置热更新轮询 → 性能日志循环。

停机（`shutdown()`）：取消热更新/性能/健康任务 → 停调度器 → 停接入服务 → 完整 drain parse/redis 池 → 断开设备 → 关 PLC 读池 → 关连接池。Parse/Redis 任务可能已持有成功读取的样本，因此正常停机不按超时取消；尚未产出样本的 PLC 读池受 `SHUTDOWN_DRAIN_TIMEOUT` 上限约束，超时会记录未完成 job 数。

> **停机信号**：除信号量外，collector 还监听 `runtime/collector.shutdown_flag` 文件（`_watch_shutdown_flag` `collector/main.py`）。agent 通过写该文件来停止 collector 进程；启动时会先清理上一轮残留 flag。

### 5.2 DeviceManager（设备与驱动生命周期）

`collector/device/manager.py`。负责设备注册、驱动创建、连接、健康检查、重连、generation 管理、center 协议端口映射。

- **驱动工厂** `_create_driver` (`collector/device/manager.py`)：按协议名映射到内置驱动（s7 / ge_srtp / opcua / opcda_center）。**新增协议在此注册。**
- **连接保护**：每设备一个熔断器 `CircuitBreaker`（5 次失败 OPEN、60s 恢复）+ `with_retry`（3 次指数退避）+ 全局连接信号量（并发 10）。
- **重连**：`schedule_reconnect` (`collector/device/manager.py`) 启动每设备重连协程，延迟按 `_next_reconnect_delay` 指数退避（30s→上限 300s）叠加 0.2 抖动，避免重连风暴。
- **generation 机制**：`_activate_device`/`_advance_device_generation`/`invalidate_device` 维护每设备单调代次，配 per-device `threading.RLock`。`is_current_generation` / `run_if_current_generation` 保证热重载后旧周期不写、且 invalidate 与写入不交错。

### 5.3 TaskScheduler（周期调度）

`collector/collector/scheduler.py`。**当前模型是"每设备一个统一循环"，不是"每任务一个循环"**：
`start()` 先按 `device.device_id` 把 `CollectTask` 分组，再对每台设备调用 `_run_device_loop(tasks, ...)`；
只有该设备**仅有一个任务**时才走 `_run_phased_loop`。同一设备的多个扫描周期（不同 `scan_rate`）
共存于**同一个**循环里。

关键策略：

1. **相位错开**（`_initial_phase_seconds`）：初始相位 = `crc32(device_id) % 周期`，把设备首发时刻打散，
   避免齐发尖峰。
2. **同设备单 in-flight**（`_try_begin_device_execution`）：同一设备同一时刻只允许一个采集在飞行，
   与 PLC 单连接不可并发一致；被挡下的周期计入 `device_inflight_skip_count`，且设备门会**轮换**
   被跳过的任务，避免高频任务长期抢占低频任务。
3. **仅"同时到期"才合并**：每轮循环收集本轮到期的任务（`due`），经 `_execute_due_tasks` 交给
   `TaskExecutor.execute_many`；**只有同时到期的同一设备任务**才可能合并成一次 PLC 读取。
   并且合并还受语义约束——**同地址不同 `data_type` 时不合并**（各任务各自解码，见 §5.4 与 B46.2），
   因为合并会丢失类型语义。
4. **周期补偿 + 三类互斥计数**（`_run_device_loop` / `_run_loop`）：执行耗时 < 周期则补足等待，
   否则立即进入下一轮（不堆积）。deadline 相关计数**互不重叠、可对账**：
   - `deadline_late_start_count`：到期且**确实开始执行**、但滞后超阈值（迟到）；
   - `device_inflight_skip_count`：到期但因**设备仍在执行上一周期**而未启动新执行（跳过）；
   - `deadline_miss_count`：因上一周期超时而被跳过的**完整周期数**（漏周期）。
5. **取消与停机**：`reload_device_tasks` 只取消**该设备**的循环并重装任务，不影响其他设备；
   停机会有界关闭三个线程池并报告未完成任务数。

线程模型：collector 是**单 OS 进程**，设备循环是 asyncio 协程（不占额外进程）；真正的并行只来自
三个**显式有界线程池**（PLC 读 / parse / redis）。自由线程 Python 3.14t 的作用是让这些池**不再
受 GIL 限制**，收益来自显式 executor 并行，**不存在"自动多核加速"**。

热重载用 `reload_device_tasks` (`collector/collector/scheduler.py`)：取消该设备旧循环、清状态、注册新任务，不影响其他设备。

### 5.4 TaskExecutor（采集执行）

`collector/collector/executor.py`。单周期采集的核心流水线 `execute()` (`collector/collector/executor.py`)：

1. **generation 校验** → 失效则丢弃（计 `stale_generation_drop_count`）；
2. **连接检查** → 未连接则抑制日志并调度重连；
3. **批量读** `driver.read(read_blocks)`（PLC 读池）→ 池满（`ExecutorSaturatedError`）视为背压、丢本周期、不标设备错误、不触发重连；
4. **解析** `_parse_result`（parse 池）→ S7 合并块用块级缓存，同块只转一次 bytearray（O(N²)→O(N)，即历史 CPU 热点的修复点）；
5. **写 Redis** `_write_to_redis`（redis 池）→ 先 `write_window_stream` 合并写，再 `write_latest`。

值还原：`_extract_mapped_s7_value` (`collector/collector/executor.py`) 按 byte/bit 偏移与类型（BYTE/INT/WORD/REAL/DINT/DWORD/BOOL）解码；`_extract_mapped_ge_value` (`collector/collector/executor.py`) 按 word 偏移与字序解码 GE 寄存器/离散量。bool 统一转 `"1"/"0"`，避免下游 `float()` 解析失败。

> **写序不变式**：window+stream 必须先于 latest 落库。latest 是"本批次已落库"的信号，若先刷新 latest 而 stream 失败，下游会误判批次完整。

### 5.5 BoundedExecutor（有界池背压）

`collector/collector/bounded_executor.py`。collector 用三类有界池隔离不同阶段：**PLC 读池**（阻塞型 snap7/GE 读，避免与 Redis 写抢线程）、**parse 池**、**redis 池**。池满即抛 `ExecutorSaturatedError`，上层据此丢弃当前周期而非排队堆积——以丢单周期换取系统不雪崩。

### 5.6 InterfaceIngestServer（外部推送接入）

`collector/monitor/ingest_server.py`。对 external-push 协议（如 OPC DA push、HTTP push）提供 HTTP 接入端点：外部进程把数据推入，经 OPC DA 映射器转换后走同一条 RedisWriter 链路。这类设备在 collector 内无轮询任务、无 PLC 驱动。

---

## 6. 主要流程
<!-- xiaohu-illustration:collector-readme-02-shards -->
![小狐配图：分组分片盒](../../images/xiaohu-collector-readme-02-shards.png)

### 6.1 单周期采集流水线

同一设备本轮**同时到期**的多个任务会先合批（`_execute_due_tasks` → `execute_many`）；仅当读块可安全
合并时共用**一次** PLC 读，然后**逐任务**解析与写入（各自的变量与分组身份保持不变）。不能合并时
（例如同地址不同 `data_type`）退回逐任务独立读取。

```mermaid
sequenceDiagram
    participant SCH as TaskScheduler._run_device_loop
    participant EXE as TaskExecutor.execute_many
    participant DRV as Driver
    participant RW as RedisWriter
    SCH->>EXE: execute_many(同时到期的 tasks)
    Note over EXE: generation 校验 / 设备在途门<br/>可按语义合并则合成一次读块
    EXE->>DRV: read(merged_blocks)  [PLC 读池]
    DRV-->>EXE: {block_addr: 原始值}
    loop 每个任务（身份与分组不变）
        EXE->>EXE: _parse_result  [parse 池, 块级缓存]
        EXE->>RW: write_window_stream + write_latest  [redis 池]
    end
    RW-->>EXE: ok
    EXE-->>SCH: 完成 / 逐任务异常计数
```

> 单任务设备走 `_run_phased_loop` + `execute(task)`，是同一条流水线的单任务分支。

### 6.2 配置热更新

`_config_reload_loop` (`collector/main.py`)：每 10 秒查 MySQL 的设备配置版本表，对比得出 added / removed / changed：

- **新增** → `add_device` + 连接 + 注册任务；
- **删除/禁用** → `invalidate_device`（先 tombstone）+ 取消调度 + `remove_device`；
- **版本变更** → 若设备连接字段变了则整设备重建，否则仅重建变量与任务。

每轮以一次完整快照为权威，刷新内存配置、OPC DA 映射器与接入元数据；单台失败不中断整轮。

### 6.3 协议分流

```mermaid
flowchart TD
    REG[register_devices] --> Q{协议类型}
    Q -->|普通轮询 s7/ge_srtp/opcua| P1[建驱动 + 建 ScanTask 轮询]
    Q -->|external-push| P2[无驱动 无轮询<br/>数据经 ingest_server 推入]
    Q -->|center opcda_center| P3[分配代理端口<br/>经本机代理进程远程采集]
```

---

## 7. 对外接口与契约

### 7.1 Redis 写出（主输出）

`collector/storage/redis_writer.py`。三种结构（键名以 device_code / group_code 为粒度）：

| 结构 | 键 | 内容 |
|------|-----|------|
| latest（Hash） | `rt:latest:{device_code}:{group_code}` | 各点位最新值 `{v, t, q}`，无 TTL |
| window（List） | `rt:window:{device_code}:{group_code}:{scan_rate_ms}` | 最近 10 帧完整帧 |
| stream（Stream） | `rt:stream:{device_type}:{group_code}:{shard}` | 入库消费队列，`shard=crc32(device_code)%N`；过滤 STRING 与 is_storage=0 |

序列化用 `msgspec`；window 与 stream 合并进一个非事务 pipeline（每设备每周期 Redis 往返 3→2）。完整字段定义见《03-数据与契约/数据库/Redis结构与字段》。

### 7.2 其他接口

- **HTTP 接入**（external-push）：`InterfaceIngestServer` 暴露的接入端点（host/port/path/api_key 可配）。
- **设备状态回写**：经 `status_reporter` 回写 MySQL 设备在线状态（注入自 `config_db_manager.update_device_status`）。
- **停机 flag**：`runtime/collector.shutdown_flag` 文件（agent → collector 停机信号）。
- **端口映射（两种模式，勿混用）**：实现为 `collector/device/manager.py` 的 `EndpointPortMapper`。
  - **动态分配模式**（未显式指定映射文件）：从 `base_port`（默认 9090）起向上分配到 `max_port`
    （默认 9200），用真实 socket **探测端口可用性**并跳过被占用端口；若默认共享文件
    `collector/port_mapping.json` 存在则先加载以**粘性复用**旧端口，分配后**写回**该文件。
    此模式下映射文件的归属是 collector 自己。
  - **权威只读模式**（显式传入 `--port-mapping` 或环境变量 `COLLECTOR_PORT_MAPPING`）：
    **只读**该文件，**不探测、不分配、不写回**；`reload()` 也只重读该文件。
    **fail-fast 边界**：文件缺失、无法解析、不是 JSON 对象、或任一条目的端口值非法
    （非 int / bool / 越界 1..65535）都会抛 `PortMappingError` **并终止**——**绝不静默退化成
    动态分配**（那会导致端口漂移）；请求的 endpoint 不在权威映射中同样抛错，而不是新分配一个。
  - **生产环境归属**：由 **Agent** 的 `agent/app/services/sidecar_manager.py` 生成并保管
    `runtime/opcda_proxy/port_mapping.json`（端口归属与分配是 Agent 的职责），collector 只读消费。
    因此权威模式**不做可用性探测**是刻意的职责划分，不是缺口；端口值只做类型与范围校验。
    动态模式仅用于没有 Agent 编排的场景（如单机调试）。

---

## 8. 配置说明

配置来源分两层（`main.py` `main()`）：

1. **运行参数**：`config/settings.toml`（或 `COLLECTOR_CONFIG`）——日志、Redis/MySQL 连接、扫描与池参数、`interface_ingest`、`enable_fake_bootstrap` 等。
2. **设备与点位拓扑**：以 **MySQL 为准**，启动时由 `ConfigDBManager.load_config` 加载并**覆盖** TOML 中的 devices/variables。

数据库凭据为单一来源，由 agent 经环境变量（`DATAWATCH_<DB>_<FIELD>`）注入；settings.toml 仅作开发期回退。详见《03-数据与契约/数据库/MySQL结构与字段》与《04-运维部署》。

命令行：`--dry-run`（只做配置预检，输出设备/点位/合并块统计，不启动采集）、`--config`、`--runtime-dir`、`--port-mapping`。

MySQL 不可用且 `enable_fake_bootstrap=true` 时，回退为假拓扑（`_build_fake_topology` `collector/main.py`）用于联调验证 Redis 链路。

---

## 9. 设计决策与权衡

| 决策 | 理由 / 权衡 |
|------|-------------|
| 只写 Redis、与 writer 解耦 | 采集与入库互不阻塞，单侧故障不扩散；代价是端到端多一跳缓冲 |
| 自由线程 Python 3.14t | collector 仍是单进程部署，但 PLC 读、parse、Redis 写等显式线程池不再受传统 GIL 限制；收益来自明确的 executor 并行，不是 event loop 自动变多核 |
| msgspec 序列化 | orjson 在自由线程下不兼容；msgspec 兼容且高效 |
| 三段有界池 + 丢周期背压 | 宁可丢单个采集周期，也不让任务堆积引发雪崩与重连风暴 |
| generation 失效协议 | 热重载/下线瞬间，旧周期的迟到数据不得污染新配置下的 Redis |
| 单设备 in-flight 串行 | PLC 单连接不可并发；串行是正确性约束而非性能选择 |
| crc32 相位错开 / 稳定分片 | 内置 `hash()` 受 `PYTHONHASHSEED` 影响每进程不同，crc32 跨进程稳定 |
| center 端口权威只读 | agent 生成权威映射，collector 只读、缺失即报错，避免动态分配漂移 |
| 外置驱动 `datawatch_ext` 合成包 | 隔离驱动自身模块名；**不是**第三方依赖多版本隔离（pymodbus 仍进全局 `sys.modules`，一进程单版本） |
| `CONFIG_ERROR` 与热重载回滚 | 单设备非法配置不拖垮进程；热重载失败保留上一代 tasks，禁止先删旧再报错 |
| Modbus `unit_id`=`slot`（仅 None→1） | 合法范围 `[1,247]`；禁止 `slot or 1` 吞掉 `0` |

---

## 10. 已知问题与技术债

> 以下为代码注释与运行约束中明确标注的待办项，供技术债清理阶段参考（此处只记录，不在本文处理）。

- **GE SRTP 字序与码表待真机校准**：`_extract_mapped_ge_value` 当前仅支持 `word_order="low_high"`，REAL/DINT/DWORD 字序需现场已知值校验后固化（`collector/collector/executor.py`）。
- **S7 多变量批量读为"假批量"**：底层 snap7 的 `read_multi_vars` 实为循环单读；按 DB 号合并连续块为第一层优化，multi_vars 第二层暂缓。
- **OPC DA 依赖 Windows COM**：center/external-push 经 C# sidecar，受 Windows 平台与 COM 环境约束。

---

## 11. 扩展方式：新增一个协议驱动

> 前提见上文「4.1 分层边界」：你扩展的是**驱动层**，不是改调度器去直接调协议库。

### 11.1 外置驱动包（推荐，试点：Modbus TCP）

完整制作、打包与安装步骤见：[外置驱动包制作指南](外置驱动包制作指南.md)。

1. 在 `DATAWATCH_DATA_ROOT/drivers/<id>/` 放置 `driver.toml` + Python 包 + 可选 `vendor/` + `vendor_meta.toml`（版本/许可/哈希）。
2. 实现 `create_driver(context: DriverContext)`；声明 `batch_contiguous` 时提供 `build_read_plan` / `decode_read_results`。
3. collector 启动扫描注册表经 `datawatch_ext.<id>` 加载；导入期临时使用 vendor 路径，禁止永久污染 `sys.path`。
4. 非法地址/类型 → 设备 `CONFIG_ERROR`（禁连、禁采、禁重连），并暴露 `config_error`。

### 11.2 现有驱动（S7 / GE / OPC UA / OPC DA / Modbus TCP）

当前**所有**轮询驱动都以**受控驱动包**形式存在，位于 `packages/<id>/`（`s7`、`ge_srtp`、`opcua`、
`opcda_center`、`modbus_tcp`），由 `collector/drivers/registry.py` 的 `DriverRegistry` 统一发现、
校验与加载；`DeviceManager._create_driver` 只调用 `registry.create(...)`，**协议实现一律不写在
collector 内部**，因此**新增协议不需要改核心调度**。

改动既有驱动时的注意事项：

1. 驱动包内的 `driver.toml`（必需）与 `vendor_meta.toml`（**可选**：版本、许可、哈希）连同包内容
   共同参与发布来源校验：改动驱动包源码后必须**重新打包并更新目录记录**，否则注册表会以"官方包
   未通过发布来源校验"拒绝加载（这一点在本地实测过）。
2. 驱动类实现 `connect` / `disconnect` / `read(blocks)->{address: value}` / `write`，按需重写
   `ping`；同连接的批量读需持有 `_read_lock`；协议库只在该驱动类内部调用。
3. 外部推送或中心采集协议需在相应协议集合中登记（`EXTERNAL_PUSH_PROTOCOLS` /
   `CENTER_COLLECTION_PROTOCOLS`），以走对应分流分支。
4. 需要按地址合并块或自定义解析时，由驱动包提供 `build_read_plan` / `decode_read_results`
   （声明 `batch_contiguous`），**不要**在调度器里直接调协议库。
5. 补充对应单元测试（参考 `collector/tests/test_s7_driver.py`、`collector/tests/test_runtime_topology.py`）。

> **历史说明（已废弃，勿照做）**：旧文档曾要求在 `collector/drivers/` 下新建 `*_driver.py` 并在
> `DeviceManager._create_driver` 里注册协议名。该目录现在只保留契约与注册表（`base.py` /
> `context.py` / `manifest.py` / `registry.py`），上述做法已不适用。

---

## 相关文档

- [系统架构总览](../../01-总体设计/系统架构总览.md)
- [writer 入库端](../writer/README.md)（下游消费方）
- [调用链与协议设计](调用链与协议设计.md)（调用链、协议分流、读块合并、热更新）
- [外置驱动包制作指南](外置驱动包制作指南.md)（ZIP 清单、打包、安装与 checklist）
- [Redis 流与点位分组](../../03-数据与契约/数据库/Redis结构与字段.md)（Redis 结构完整契约）
- [数据库设计](../../03-数据与契约/数据库/MySQL结构与字段.md)（设备/点位配置表)
