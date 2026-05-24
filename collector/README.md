# 工业数据采集系统

## 项目简介

这是一个专为工业自动化设备设计的高性能数据采集系统，支持多种工业协议（如S7、GE SRTP、OPC UA；Modbus规划中），并支持通过 32 位 C# sidecar 接入 AGV OPC DA 数据源。collector 负责实时采集并写入 Redis（latest / window / stream），由独立 writer 进程批量写入 TDengine。

## 主要特性

- 🔌 **多协议支持** - 支持S7、GE SRTP、OPC UA（Modbus规划中），AGV 32 位 OPC DA 通过 C# x86 sidecar + HTTP ingest 接入
- 🚀 **高性能** - 基于asyncio的高并发架构，支持大规模设备连接
- 💾 **Redis输出** - 写入 latest / window / stream，支撑实时监控与异步入库
- 🌐 **接口采集** - 支持 HTTP Push 接入外部数据源
- 🔧 **灵活配置** - 支持数据库配置和热重载
- 🛡️ **高可靠** - 熔断器、重试机制、自动重连等容错设计

## 系统架构

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  MySQL拓扑   │────▶│ DeviceManager│────▶│ TaskScheduler│
└──────────────┘     └──────────────┘     └──────┬───────┘
                                                  │
                                                  ▼
                                           ┌──────────────┐
                                           │ TaskExecutor │
                                           └──────┬───────┘
                                                  │
┌──────────────┐     ┌─────────────────────┐      │
│ HTTP / OPCDA │────▶│ InterfaceIngestServer│─────┤
└──────────────┘     └─────────────────────┘      ▼
                                           ┌──────────────┐
                                           │ Redis Writer │
                                           │latest/window/│
                                           │    stream    │
                                           └──────┬───────┘
                                                  ▼
                                           独立 writer 入库 TDengine
```

## 快速开始

### 环境要求

- Python 3.8+
- MySQL 8.0+（用于设备配置管理）
- Redis 5.0+（实时缓冲与待入库 Stream）
- .NET Framework / MSBuild（仅 AGV OPC DA sidecar 需要，且必须 x86 编译）

### 安装依赖

```bash
# 创建虚拟环境（首次）
python -m venv .venv

# 激活虚拟环境
# Windows CMD:
.venv\Scripts\activate.bat
# Windows PowerShell:
.venv\Scripts\Activate.ps1
# Linux/Mac:
source .venv/bin/activate

# 安装依赖
pip install -r requirements.txt
```

### 配置数据库

1. 创建数据库：
```sql
CREATE DATABASE device_watch CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
```

2. 导入表结构：
```bash
mysql -u username -p device_watch < device_watch.sql
```

### 配置系统

编辑 `config/settings.toml` 文件（默认读取此路径）：

```toml
[runtime]
log_level = "INFO"
enable_fake_bootstrap = false

[mysql]
host = "127.0.0.1"
port = 3306
user = "root"
password = "password"
database = "device_watch"

[interface_ingest]
enabled = true
host = "0.0.0.0"
port = 8090
path = "/ingest"
api_key = ""
max_batch_size = 1000
```

> 设备与点位配置不再从 TOML 的 `[[devices]]` / `[[variables]]` 读取，
> 启动时会强制从 MySQL 的 `device`、`device_variable` 表加载。
>
> 请确保：
> - `device_variable.is_storage = 1`（需要采集并入库的点位）
>
> 采集周期同样来自 MySQL：
> - 设备默认周期：`device.scan_cycle`
> - 点位周期（优先）：`device_variable.scan_rate`
>
> 连接参数也来自 MySQL（而不是 TOML）：
> - 协议与默认端口：`comm_protocol.protocol` / `comm_protocol.port`
> - 设备端点与机架槽位：`device.endpoint` / `device.rack` / `device.slot`
> - AGV 32 位 OPC DA 设备请使用 `protocol=opcda_sidecar` 或 `protocol=http_push`
> - 直接 `protocol=opcda` 会被启动校验拒绝，因为 64 位 Python collector 不能直接加载 32 位 OPC DA COM
>
> 若 MySQL 暂未就绪，可临时将 `runtime.enable_fake_bootstrap=true`：
> - 会注入 `QC_FAKE100` / `ASC_FAKE200` 假拓扑并写入示例点位
> - 所有相关日志包含 `[FAKE-DATA]` 标识
> - 验证 SQL 见 `docs/fake_data_verify.sql`
> - 清理脚本见 `docs/fake_data_cleanup.sql`

也可以通过环境变量指定配置文件：

```bash
set COLLECTOR_CONFIG=D:\python\device_alarm\data_watch\collector\config\settings.toml
```

项目也提供了联调模板配置 `config/settings.fake_bootstrap.toml`（已启用 `enable_fake_bootstrap=true`），
可在 MySQL 未就绪阶段临时用于打通 collector 采集与 Redis 写入链路验证。

> 如果项目目录从中文路径迁移到 `D:\python\device_alarm\data_watch`，建议重建 `collector\.venv`。Windows venv 启动器可能仍保存旧路径，`run.bat` 会尝试回退到系统 Python，但回退解释器必须已经安装采集模块依赖。

### 启动服务

```bash
# 确保已激活虚拟环境
.venv\Scripts\activate.bat

# 启动 collector
python main.py
```

或使用一键启动脚本（自动激活虚拟环境）：

```bash
run.bat
```

### OPC DA 服务启动

OPC DA 是 32 位 COM 组件，需要通过代理进程访问。系统支持两种模式：

#### 模式一：中心采集模式（推荐）

在中心服务器运行代理进程，通过 DCOM 远程连接 OPC VM：

```bash
# 1. 启动 OPC DA 代理服务（在 collector/sidecars/opcda_proxy_service 目录）
cd collector/sidecars/opcda_proxy_service

# 编译（首次）
.\build.ps1

# 启动单个代理
.\bin\OpcDaProxyService.exe --config proxy_vm221.json

# 或启动所有代理（多个 VM）
.\start_all_proxies.bat

# 2. 启动 collector
cd ../../..
.venv\Scripts\activate.bat
python main.py
```

**代理配置文件** (`proxy_vm*.json`)：

```json
{
  "listenPort": 9090,
  "maxConnections": 20,
  "updateRateMs": 500,
  "connectionTimeoutMs": 10000
}
```

| 字段 | 说明 | 默认值 |
|------|------|--------|
| `listenPort` | TCP 监听端口 | 9090 |
| `maxConnections` | 最大并发连接数 | 20 |
| `updateRateMs` | OPC 组更新速率（毫秒） | 500 |
| `connectionTimeoutMs` | DCOM 连接超时（毫秒） | 10000 |

#### 模式二：边缘推送模式

在 OPC VM 上部署 sidecar，本地 COM 连接后 HTTP 推送到中心：

```bash
# 在 OPC VM 上执行
cd collector/sidecars/opcda_push_service

# 编译（首次）
.\build.ps1

# 配置（编辑 opcda_push_config.json）
# - 设置 collectorUrl 为中心服务器地址
# - 设置 serverProgId 为 OPC Server ProgID

# 启动
.\run.bat
```

#### 端口自动分配

中心采集模式启动时，collector 会自动为每个 VM 分配代理端口：

- 第一个 VM → 端口 9090
- 第二个 VM → 端口 9091
- 以此类推...

如果端口被占用，会自动跳过到下一个可用端口。

#### 数据库配置

确保 `comm_protocol` 表配置了 OPC Server：

```sql
-- 配置 OPC DA 协议和 OPC Server ProgID
INSERT INTO comm_protocol (id, protocol, port, server, description)
VALUES 
    (3, 'OPC-DA-CENTER', 9090, 'ZPMC.OPCServer', 'ZPMC OPC DA 远程采集'),
    (4, 'OPC-DA-SIDECAR', 0, 'ZPMC.OPCServer', 'ZPMC OPC DA 边缘推送')
ON DUPLICATE KEY UPDATE server = VALUES(server);

-- 配置设备使用 OPC DA 协议
UPDATE device SET 
  protocol_id = 3,  -- OPC-DA-CENTER
  endpoint = '172.27.18.221'  -- OPC VM 地址
WHERE device_code = 'AGV801';
```

## 核心组件

### 1. 设备管理器 (DeviceManager)

负责设备的注册、连接和状态管理：

```python
from device.manager import DeviceManager
from core.config import CollectorConfig

# 创建设备管理器
config = CollectorConfig()
manager = DeviceManager(config)

# 注册设备
manager.register_devices()

# 连接所有设备
await manager.connect_all()
```

### 2. 数据采集调度器与执行器

管理采集任务的调度和执行：

```python
from collector.executor import TaskExecutor
from collector.scheduler import TaskScheduler

# 创建调度器和执行器
scheduler = TaskScheduler()
executor = TaskExecutor(device_manager=device_manager, redis_writer=redis_writer)

# 添加任务
scheduler.add_tasks(tasks)

# 启动调度
await scheduler.start(executor.execute)
```

#### 调度策略

调度器采用**智能间隔**策略，根据执行时间动态调整：

| 执行时间 | 调度行为 |
|----------|----------|
| 执行时间 < scan_rate | 等待剩余时间后发送下一次请求 |
| 执行时间 >= scan_rate | 立即发送下一次请求（不额外等待） |

> 从 MySQL 加载的 `device_variable.scan_rate`（单位毫秒）会在 `core/config_db.py` 的 `_normalize_scan_rate()` 中转换为秒（float）：≥100 的值视为毫秒，`100ms → 0.1s`，`500ms → 0.5s`，支持亚秒采集。

设计示例（scan_rate = 500ms）：

```
正常情况（执行耗时 100ms）:
|-- 执行 100ms --|-- 等待 400ms --|-- 执行 100ms --|-- 等待 400ms --|
0ms            100ms            500ms           600ms           1000ms

慢请求情况（执行耗时 800ms）:
|---------- 执行 800ms ----------|-- 立即执行下一次 --|
0ms                              800ms                 800ms+
                                  (不额外等待)
```

这种策略的优点：
- **正常情况**：严格按固定周期采集
- **慢请求**：不会造成请求堆积，也不会额外增加等待时间
- **自适应**：网络延迟时自动调整，恢复后自动回到正常周期

### 3. Redis 输出

collector 不直接连接 TDengine。采集结果统一写入 Redis：

- `latest`：设备/点位最新值，供实时监控读取
- `window`：短窗口数据，供实时页面和轻量分析使用
- `stream`：待入库数据，由独立 writer 消费并写入 TDengine 宽表

### 4. HTTP Push 接入

外部数据源通过 `/ingest` 推送后，同样进入 Redis latest / window / stream。

## 外部模块接口采集（HTTP Push）

系统支持通过 HTTP 接口直接推送数据点到采集链路，服务端会将数据写入 Redis latest / window / stream。

- 默认端口：`8090`
- 推送路径：`/ingest`
- 健康检查：`/ingest/health`
- 适用场景：AGV 32 位 OPC DA sidecar、外部算法模块、其它无法直接由 collector 驱动读取的数据源

单点示例：

```bash
curl -X POST http://127.0.0.1:8090/ingest \
  -H "Content-Type: application/json" \
  -d "{\"device_id\":\"EXT001\",\"variable_id\":\"temp\",\"value\":36.5,\"timestamp\":\"2026-04-05T10:30:00+08:00\"}"
```

批量示例：

```bash
curl -X POST http://127.0.0.1:8090/ingest \
  -H "Content-Type: application/json" \
  -d "{\"points\":[{\"device_id\":\"EXT001\",\"variable_id\":\"temp\",\"value\":36.5},{\"device_id\":\"EXT001\",\"variable_id\":\"pressure\",\"value\":1.23}]}"
```

### AGV OPC DA Sidecar

AGV 的 ZPMC 自研 OPC 数据源属于 32 位 OPC DA，collector 主进程保持 64 位 Python，不直接加载 OPC DA COM。正式设计为：

```text
ZPMC.OPCServer (32 位 OPC DA)
  -> C#/.NET Framework x86 sidecar
  -> HTTP POST /ingest
  -> Redis latest / window / stream
  -> 独立 writer 模块批量写入 TDengine 宽表
```

当前 `sidecars/opcda_sidecar/` 已提供只读探针，用于验证 OPC Server 与 ItemID：

```powershell
cd D:\python\device_alarm\data_watch\collector\sidecars\opcda_sidecar
.\build.ps1
.\bin\OpcDaProbe.exe --server ZPMC.OPCServer --channel OPD_Server --device AGV801 --group ViewMain --csv ..\..\..\data\AGV_OPC\agv\ViewMain.csv --limit 20 --probe-count 10 --source cache --read-delay-ms 2000
```

生产推送服务基线位于 `sidecars/opcda_push_service/`：

```powershell
cd D:\python\device_alarm\data_watch\collector\sidecars\opcda_push_service
.\build.ps1
copy opcda_push_config.example.json opcda_push_config.json
.\run.bat
```

`opcda_push_service` 支持配置多台 AGV、多组 group、CSV 点位模板和单独补充点位。推送中心时可以只带本地 `tag`，不带 `variable_id`，由中心按 `device_id + group + tag` 统一换算。

已验证的 ItemID 格式为：

```text
OPD_Server.<AGV设备>.ViewMain.<Tag>
```

例如：

```text
OPD_Server.AGV801.ViewMain.Tag30041800
```

安全边界：

- sidecar 只读 OPC DA Item，不写 Item，不发送控制指令。
- sidecar 不直接写 TDengine，只推送到中心 HTTP 接收接口。
- MySQL 中 AGV 设备仍使用 `protocol=opcda_sidecar` 或 `protocol=http_push` 注册拓扑，collector 不为其创建 PLC driver 或周期采集任务。

## 测试

运行测试套件：

```bash
# 运行所有测试
pytest

# 运行特定测试
pytest tests/test_device_manager.py

# 运行接口采集测试
pytest tests/test_interface_ingest.py -q
```

## 性能优化

### 地址合并

系统自动将连续地址合并为批量读取块，减少通信次数：

```python
# 原始地址列表示例（S7 标准 DB 地址）
addresses = ["DB100.DBB0", "DB100.DBB1", "DB100.DBB2", "DB101.DBB0"]

# 合并为（后续优化方向；当前生产优先保持一变量一读取块，并保留 DBB/DBW/DBD 语义）
blocks = [
    ReadBlock(address="DB100.DBB0", count=1, data_type="BYTE"),
    ReadBlock(address="DB100.DBB1", count=1, data_type="BYTE"),
    ReadBlock(address="DB100.DBB2", count=1, data_type="BYTE"),
    ReadBlock(address="DB101.DBB0", count=1, data_type="BYTE")
]
```

## 故障排查

### 常见问题

1. **设备连接失败**
   - 检查网络连接
   - 验证协议和端口配置
   - 查看详细错误日志

2. **数据写入失败**
   - 检查 Redis 连接状态
   - 验证数据格式
   - 确认独立 writer 正常消费 Redis Stream

3. **性能问题**
   - 检查采集周期和点位数量
   - 优化扫描周期
   - 监控系统资源使用

### 日志配置

```yaml
logging:
  version: 1
  disable_existing_loggers: false
  formatters:
    standard:
      format: "%(asctime)s [%(levelname)s] %(name)s: %(message)s"
  handlers:
    console:
      class: logging.StreamHandler
      level: INFO
      formatter: standard
  loggers:
    collector:
      level: INFO
      handlers: [console]
```

## 贡献指南

1. Fork 项目
2. 创建功能分支
3. 提交更改
4. 推送到分支
5. 创建 Pull Request

## 许可证

MIT License

## 联系方式

- 项目地址：[GitHub Repository]
- 问题反馈：[Issues]
- 开发团队：[Development Team]


