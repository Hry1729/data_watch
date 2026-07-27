# Python 工业协议与驱动包选型参考

> 本文对照 **Daq/Snet**、**HslCommunication 12.5.2** 与 **DataWatch collector** 的协议覆盖情况，整理 Python 生态中可用的现成库，供「可安装驱动包」（路径 A）选型使用。  
> 参考源码位置：`cankao/Daq-main`、`cankao/HslCommunication12.5.2 无`、`cankao/HslCommunication-master`。  
> 设计 spec：`docs/superpowers/specs/2026-07-21-collector-pluggable-drivers-design.md`

---

## 1. 说明与原则

### 1.1 选型优先级

- **优先**：Daq（Snet）与 HslCommunication **都支持**的协议。
- **路径 A**：在 Python 中实现 `BaseDriver` 驱动包，经 agent 安装 ZIP + 重启 collector 冷加载。
- **不做**：MQTT/Kafka 作为平台主输出；不把 Daq/Snet 的 .NET DLL 或 HSL C# 库直接当 ZIP 装进 collector。

### 1.2 成熟度标记

| 标记 | 含义 |
|------|------|
| ★★★★ | 生产常用、维护较好，适合优先做外置驱动包 |
| ★★★ | 可用，需真机验证 |
| ★★ | 小众/老旧/依赖重，或多数型号可走 Modbus |
| ★ | 几乎没有成熟 Python 库，需参考 HSL 自研或 C# sidecar |

### 1.3 DataWatch 已有协议

| 协议码 | 实现方式 | 说明 |
|--------|----------|------|
| `s7` / `snap7` / `siemens` | 内置 `S7Driver` + python-snap7 | 周期轮询 |
| `ge_srtp` | 内置 `GESRTPDriver` + 自研 gesrtp-py | 周期轮询 |
| `opcua` / `opc` | 内置 `OPCUADriver` + asyncua | 周期轮询 |
| `opcda_center` | 内置 + C# OPC DA proxy（agent 管） | 中心采集 |
| `http_push` / `opcda_sidecar` | `InterfaceIngestServer` | 外部推送，无轮询驱动 |

---

## 2. Daq + HSL 共有协议 → Python 能力对照

| 协议/品牌 | Daq (Snet) | HSL | DataWatch 现状 | Python 现成包 | 成熟度 | 驱动包建议 |
|-----------|------------|-----|----------------|---------------|--------|------------|
| **Modbus TCP/RTU** | ModbusOperate | ModbusTcpNet 等 | 计划试点 | **pymodbus** | ★★★★ | **第一期外置驱动首选** |
| **西门子 S7** | SiemensOperate | SiemensS7Net | ✅ 内置 | **python-snap7** | ★★★★ | 保持内置；HSL 可作地址/报文参考 |
| **OPC UA** | OpcUaClient | OpcUa | ✅ 内置 | **asyncua** | ★★★★ | 保持内置 |
| **GE SRTP** | GEOperate | GeSRTPNet | ✅ 内置 | 自研 **gesrtp-py**（仓库内） | ★★★ | 保持内置；HSL 可作联调参考 |
| **三菱 Melsec** | MitsubishiOperate | Melsec 多型号 | — | **pymelsec**、**pymcprotocol** | ★★★ | 二期高优先 |
| **欧姆龙** | OmronOperate | Fins/CIP/HostLink | — | **omronfins**、**fins** | ★★★ | FINS 以太网优先 |
| **Allen-Bradley** | AllenBradleyOperate | CIP/PCCC | — | **pycomm3**、**pylogix** | ★★★ | Logix 标签读写 |
| **倍福 Beckhoff** | — | Beckhoff Ads | — | **pyads** | ★★★ | 需 ADS 路由 |
| **台达 Delta** | DeltaOperate | Delta TCP/Serial | — | 多数 **pymodbus** | ★★★ | 常走 Modbus |
| **基恩士 Keyence** | KeyenceOperate | MC/Nano | — | **pymcprotocol** / Modbus | ★★ | 按型号选择 |
| **松下 Panasonic** | PanasonicOperate | MC/Mewtocol | — | **pymcprotocol** / Modbus | ★★ | 按型号选择 |
| **安川 Yaskawa** | YaskawaOperate | Memobus | — | 几乎无成熟开源 | ★ | 参考 HSL 自研或 sidecar |
| **汇川 Inovance** | InovanceOperate | 多系列 | — | **pymodbus** + inovance_tag_plc | ★★ | 寄存器用 Modbus |
| **麦格米特 MegMeet** | MegMeetOperate | TCP/Serial | — | **pymodbus** | ★★★ | 多数 Modbus |
| **永宏 Fatek** | FatekOperate | Fatek | — | **pymodbus** + modbus_wrapper | ★★★ | Modbus + Fatek 地址表 |
| **富士 Fuji** | FujiOperate | SPH/SPB | — | 专用库少 | ★★ | 先 Modbus |
| **LS 产电** | LSisOperate | XGT/Cnet | — | **pymodbus** / 少量 MC | ★★ | 看型号文档 |
| **信捷 XinJE** | XinJEOperate | 多系列 | — | **pymodbus** | ★★★ | 官方支持 Modbus TCP |
| **丰炜 Vigor** | VigorOperate | — | — | **pymodbus** | ★★★ | Modbus |
| **维控 WeCon** | WeConOperate | Modbus | — | **pymodbus** | ★★★ | Modbus |
| **横河 Yokogawa** | YokogawaOperate | YokogawaLink | — | 几乎无成熟 Python | ★ | 参考 HSL 或 OPC/网关 |
| **发那科 Fanuc** | FanucOperate | Fanuc CNC | — | **pyfanuc**、商业 SDK | ★★ | CNC/机器人 |
| **Cimon / Turck / Toyota 等** | 有 | 有 | — | 专用库极少 | ★ | 个案评估 |
| **OPC DA** | OpcDa* | OpcDa | ✅ C# sidecar | OpenOPC（COM） | — | 保持 sidecar 模式 |
| **自由协议 Freedom** | FreedomOperate | Freedom | — | 自研帧解析 | — | 按项目定制 |

### Daq 独有或扩展能力（本期不纳入 collector 主链路）

| 类别 | 代表 | 说明 |
|------|------|------|
| 消息中间件 | MQTT、Kafka、RabbitMQ、NetMQ | Daq `IMq`；DataWatch 主输出为 Redis → TDengine |
| 电力规约 | PQDIF（DLT645 等） | 专项场景 |
| 数据库采集 | SqlServer/MySQL/Oracle/SQLite | Daq `DBOperate` |
| 模拟/调试 | Sim 虚拟地址 | 联调可用，非生产采集 |

### HSL 独有或扩展能力（参考）

| 类别 | 代表 | 说明 |
|------|------|------|
| OpenProtocol | 拧紧枪等 | 汽车产线 |
| Sick / Toledo | 仪表称重 | 专项设备 |
| Knx / IDCard | 楼宇、读卡 | 非典型 PLC |
| Redis/MQTT 等 | 中间件 | 非采集主链路 |

---

## 3. 按 Python 实现难度分档（路径 A 推荐顺序）

### 3.1 第一档：现成库成熟，适合做成外置驱动包

| 协议 | 推荐包 | PyPI / 仓库 | 备注 |
|------|--------|-------------|------|
| Modbus TCP/RTU/ASCII | pymodbus | `pip install pymodbus` | 异步 API，与 collector asyncio 契合；第一期试点 |
| 三菱 MC 3E/4E | pymelsec | `pip install pymelsec` | 二进制/ASCII，默认端口 5007 |
| 三菱 MC（备选） | pymcprotocol | GitHub: senrust/pymcprotocol | 另一实现，部分 Keyence/Panasonic |
| Allen-Bradley Logix | pycomm3 | `pip install pycomm3` | EtherNet/IP + CIP 标签 |
| AB（备选） | pylogix | `pip install pylogix` | 纯 Python，ARM 友好 |
| 倍福 TwinCAT ADS | pyads | `pip install pyads` | 依赖 ADS 库，Linux 可带 adslib |
| OPC UA | asyncua | `pip install asyncua` | 已内置 |

### 3.2 第二档：多数设备走 Modbus，一个 `modbus_tcp` 驱动可覆盖多品牌

以下品牌在 Daq/HSL 中虽有独立 Operate 类，但 Python 侧通常**不必单独写协议栈**，在 Modbus 驱动上扩展站号/地址方言即可：

- 汇川 Inovance
- 信捷 XinJE
- 丰炜 Vigor
- 维控 WeCon
- 台达 Delta
- 麦格米特 MegMeet
- 永宏 Fatek（可参考 modbus_wrapper 的 Fatek 地址映射表）
- 部分 LS 产电、松下、基恩士型号

HSL `ModbusTcpNet` 的富文本地址（如 `s=2;x=4;100`）可作为 `driver.toml` 与点位地址文档的参考，不必照搬进 collector 核心。

### 3.3 第三档：有库但较弱，需真机，二期个案做

| 协议 | 包 | 风险 |
|------|-----|------|
| 欧姆龙 FINS | omronfins / fins | 型号、UDP/TCP 差异 |
| GE SRTP | 自研 gesrtp-py | 已有；继续用 HSL GeSRTPNet 对照字序 |
| 发那科 CNC | pyfanuc | FOCAS/授权，偏 CNC 非通用 PLC |
| 汇川标签协议 | inovance_tag_plc | 依赖 pythonnet，与 free-threaded 环境需评估 |

### 3.4 第四档：Python 几乎没有好库

建议：**参考 HSL 源码自研**，或 **C# sidecar + HTTP 推送到 InterfaceIngestServer**（类似 OPC DA）。

- 安川 Memobus
- 横河 Yokogawa Link
- Cimon、Yamatake、Turck 等小众 PLC
- OpenProtocol（拧紧枪）

**不推荐**：将 `pip install HslCommunication`（官方 Python 移植）嵌入 collector 产品——功能远少于 .NET，仍有商业授权语义，仅适合本地试验对照。

---

## 4. 架构差异：Daq / HSL / DataWatch

| 维度 | Daq (Snet) | HslCommunication | DataWatch collector |
|------|------------|------------------|---------------------|
| 语言 | C# (.NET 10) | C# (.NET 4.5.1+) | Python 3.14t |
| 协议抽象 | `IDaq` / `IMq` | `IReadWriteNet` | `BaseDriver` |
| 地址模型 | `Address` + `AddressDetails` | 富文本字符串 `"s=2;100"` | MySQL `io_address` + `ReadBlock` 合并 |
| 调度 | 订阅/事件 `SubscribeAsync` | 应用自行 `Read` | `TaskScheduler` → `driver.read(blocks)` |
| 扩展 | ZIP + AssemblyLoadContext | NuGet / 改 C# | ZIP + Python 驱动包 |
| 输出 | MQ / OPC UA Server / SQLite 等 | 库本身不负责落库 | 只写 Redis |

**调用边界（不变式）**：

```text
TaskScheduler → TaskExecutor.execute → driver.read(read_blocks)
                                      → 驱动内部再调 pymodbus / snap7 等
```

调度器**不直接**调用协议库 API。详见 `docs/02-模块设计/collector/README.md` §4.1。

---

## 5. 与参考项目的关系

### 5.1 Daq (Snet.Iot.Daq)

- 插件热插拔：ZIP + `IDaq` / `IMq`，`AssemblyLoadContext`。
- 协议实现多在 **Snet.* NuGet**，不在 Daq 仓库源码内。
- **可学**：装包体验、统一读写参数、自动组包（AutoPack）。
- **不可直接用**：.NET 插件无法装入 Python collector。

### 5.2 HslCommunication 12.5.2

- 完整 C# 协议源码：`HslCommunication/Profinet/*`、`ModBus/*`。
- 统一入口：`IReadWriteNet.Read(address, length)`。
- 本地目录 `HslCommunication12.5.2 无` 为**已去除授权校验的破解版**（`Program.IsActive = true`、`NetworkBase` 中 `if(false)` 等），适合本地看协议实现；**不宜**作为产品依赖或提交到公司仓库。
- **正版激活**（官方 Demo）：`HslCommunication.Authorization.SetAuthorizationCode(...)`，菜单 **Active(&A)**，写入 `active.txt`。
- **价值**：Modbus / GE / S7 等**报文与地址语义对照**，用于写 Python 驱动，非运行时依赖。

### 5.3 能否复用 Daq / HSL 插件？

| 方案 | 可行性 | 说明 |
|------|--------|------|
| Daq ZIP 插件直接装进 collector | ❌ | C# / `IDaq`，运行时不同 |
| HSL DLL / C# 源码嵌进 collector | ❌ | Python 进程不兼容；pythonnet 过重 |
| HSL 源码当协议参考书 | ✅ | 写 pymodbus / 自研帧解析时对照 |
| C# sidecar + HTTP push | ⚠️ 旁路 | 个别难协议，非主路径（已有 OPC DA 先例） |
| Python 自研驱动包 + `BaseDriver` | ✅ | 已定路径 A |

---

## 6. 常用 pip 包速查

```bash
pip install pymodbus          # Modbus TCP/RTU/ASCII
pip install python-snap7      # 西门子 S7（collector 已用）
pip install asyncua           # OPC UA（collector 已用）
pip install pymelsec          # 三菱 MC 3E/4E
pip install pycomm3           # Allen-Bradley EtherNet/IP
pip install pyads             # 倍福 TwinCAT ADS
pip install omronfins         # 欧姆龙 FINS（包名以 PyPI 为准）
# pip install HslCommunication  # 官方 Python 版，不推荐嵌入产品
```

| 包名 | 协议 | 维护概况 |
|------|------|----------|
| pymodbus | Modbus | 活跃，3.x 异步，Python ≥3.10 |
| python-snap7 | S7 | 3.x 起纯 Python，无原生 snap7 依赖 |
| asyncua | OPC UA | 活跃，asyncio 优先 |
| pymelsec | 三菱 MC | 3E/4E，需真机验证 4E |
| pymcprotocol | 三菱 MC 等 | 备选 |
| pycomm3 | AB EtherNet/IP | 活跃，Logix/CompactLogix |
| pylogix | AB | 纯 Python，备选 |
| pyads | Beckhoff ADS | 依赖 ADS 动态库 |
| ge-ethernet-SRTP | GE SRTP | 第三方；项目内已有自研实现 |

---

## 7. 落地建议（与 spec 对齐）

### 7.1 已规划（第一期）

1. **驱动注册表 + ZIP 冷加载**（agent 安装后重启 collector）
2. **驱动管理页**合并安装/卸载（保留原有列表列）
3. **Modbus TCP** 外置驱动包（`pymodbus`）
4. 内置 s7 / ge_srtp / opcua / opcda_center 行为回归不变

### 7.2 建议二期队列（按现场设备清单调整）

| 顺序 | 协议 | Python 包 | 理由 |
|------|------|-----------|------|
| 1 | 三菱 Melsec MC | pymelsec | Daq/HSL 均有，独立协议栈 |
| 2 | Allen-Bradley | pycomm3 | 码头/自动化常见 |
| 3 | 欧姆龙 FINS | omronfins | 以太网 FINS 为主时 |
| 4 | 倍福 ADS | pyads | TwinCAT 现场 |

### 7.3 不必单独做驱动的品牌

在 **modbus_tcp** 驱动上扩展配置即可：汇川、信捷、台达、丰炜、维控、麦格米特、永宏等。

---

## 8. 本地参考工程启动（可选）

### 8.1 Daq

- 预编译： [GitHub Releases](https://github.com/shunnet/Daq/releases) + .NET 10 Desktop Runtime
- 源码：`cankao/Daq-main`，需 .NET 10 SDK，`dotnet build Snet.Iot.Daq.sln`

### 8.2 HslCommunication Demo

- 预编译：`cankao/HslCommunicationDemo-v12.9.1/HslCommunicationDemo.exe`
- 源码：`cankao/HslCommunication12.5.2 无/Hsl.sln`，Visual Studio 2022，启动项目 `HslCommunicationDemo`

---

## 9. 文档维护

- 新增外置驱动包时，在本表补充「DataWatch 现状」列与验收说明。
- 协议地址约定写入各驱动的 `driver.toml` 说明及 `docs/03-数据与契约`（若涉及设备协议码）。
- 重大选型变更同步更新 `docs/superpowers/specs/2026-07-21-collector-pluggable-drivers-design.md`。

---

*整理日期：2026-07-21*
