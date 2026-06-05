# Feature Specification: Agent OPC DA Proxy 编排（SidecarManager）

**Feature Branch**: `002-agent-opcda-proxy`

**Created**: 2026-06-05

**Status**: Draft

**Input**: User description: "Agent SidecarManager orchestrates OPC DA proxy lifecycle and sticky port mapping for collector"

**Dependency**:
- `.specify/agent-spec.md`（DataWatch Agent，FR-A07~A12 进程管理内核）
- `docs/运维/打包.md` Phase 1（本特性的设计来源）
- `collector/sidecars/opcda_proxy_service/`（已编译的 `bin/OpcDaProxyService.exe`，.NET x86）
- `collector/check_protocols.py`、`collector/device/manager.py`（现有端口映射逻辑）
- Constitution §II（先诊断后实现）、§V（Agent 职责）、§VIII（最小实现）

---

## 概述

中心采集模式下，64 位 Python collector 无法直接访问 32 位 OPC DA COM 组件，必须经由 `OpcDaProxyService.exe`（.NET x86，TCP 桥）中转。开发期 `run.bat` 通过 `check_protocols.py` 检测协议、生成 `port_mapping.json` 并拉起 proxy；但**打包部署后 Agent 作为唯一中枢**，不再走 `run.bat`，这套编排能力缺失。

本特性在 Agent 中新增 **SidecarManager**，负责 OPC DA proxy 的动态生命周期编排，并让 collector 支持显式端口映射入参，使「app → Agent → 按需拉起 proxy → 启动 collector」的链路在服务化部署下成立。

**核心约束（贯穿全特性）**：proxy 建立 OPC DA 的 DCOM 连接是最脆弱、最昂贵的环节（Session 0 / 服务账户 / Identity）。凡"反复重连 OPC"的设计都要避免——能保持 proxy 在线就不重启它。

---

## User Scenarios & Testing *(mandatory)*

### User Story 1 - 启动 collector 时按需拉起 proxy（Priority: P1）

运维人员在 app 上点"启动 collector"。Agent 先检测是否存在 opcda_center 设备；有则按端点生成粘性端口映射、差量拉起所需 proxy，等端口就绪后再启动 collector，并显式传入配置与映射路径；无 OPC DA 则直接启动 collector。

**Why this priority**: 这是整个服务化部署的主链路，没有它，打包后的 OPC DA 中心采集完全无法运行。

**Independent Test**: 在含 ≥1 个 `opcda_center` 设备的库上，仅通过 Agent API 启动 collector，可观察到 proxy 进程先起、端口先就绪、collector 后起且能读到点位——独立可验。

**Acceptance Scenarios**:

1. **Given** 库中存在 1 个启用的 `opcda_center` 设备（endpoint=E1），且无 proxy 运行，**When** `POST /admin/processes/collector/start`，**Then** Agent 先在 `9090` 起 proxy_9090、写 `port_mapping.json`(E1→9090)，待 `127.0.0.1:9090` 可连接后才启动 collector，启动顺序不可颠倒。
2. **Given** 库中存在 2 个启用的 `opcda_center` 端点（E1、E2），**When** 启动 collector，**Then** Agent 起 proxy_9090(E1)/proxy_9091(E2)，`port_mapping.json` 含两条，两端口均 ready 后才启 collector。
3. **Given** 库中无 `opcda_center` 设备，**When** 启动 collector，**Then** Agent 不拉起任何 proxy，直接启动 collector。
4. **Given** Agent 启动 collector，**Then** 命令行显式包含 `--config <ProgramData 下 settings.toml>` 与 `--port-mapping <runtime 下 port_mapping.json>`，不依赖工作目录推断。

---

### User Story 2 - Agent/服务重启后认领已存活 proxy（Priority: P1）

Agent 作为 Windows 服务被重启后，此前拉起的 proxy 进程可能仍存活。SidecarManager 启动时必须从 PID 文件重建注册表，认领存活 proxy，而不是重复拉起造成端口冲突。

**Why this priority**: 服务化部署后这是必然遇到的场景；不处理会导致端口冲突、双实例、collector 连错进程。

**Independent Test**: 手工保留一个运行中的 proxy，重启 Agent，验证 Agent 不再起第二个相同端口的 proxy、且 `status` 能正确反映该 proxy——独立可验。

**Acceptance Scenarios**:

1. **Given** `runtime/pids/proxy_9090.pid` 存在且对应进程存活（pid+create_time 校验通过），**When** Agent 启动，**Then** SidecarManager 将其认领回注册表，不重复拉起。
2. **Given** `runtime/pids/proxy_9091.pid` 存在但进程已死（create_time 不匹配或进程不存在），**When** Agent 启动，**Then** SidecarManager 清理该 pid/state，不认领。
3. **Given** Agent 重启后已认领 proxy_9090，**When** 随后启动 collector 且端点集合未变，**Then** 不重启 proxy_9090，直接复用。
4. **Given** 认领逻辑，**Then** 通过枚举 `proxy_*.pid` 实现，不依赖任何硬编码模块名清单。

---

### User Story 3 - 端口粘性分配与差量重启（Priority: P1）

同一 endpoint 始终映射到同一端口；重启 collector 时，端点集合未变就保持 proxy 在线、只重启 collector，避免反复重建昂贵的 DCOM 连接。

**Why this priority**: 差量重启直接降低现场偶发 DCOM 权限/Identity 问题的暴露概率；粘性端口是差量成立的前提。

**Independent Test**: 增删一个设备后触发重启，验证未变端点的端口不漂移、其 proxy 不被重启——独立可验。

**Acceptance Scenarios**:

1. **Given** 旧 `port_mapping.json` 含 E1→9090、E2→9091，**When** 重新生成映射且 E1/E2 仍在，**Then** E1 仍为 9090、E2 仍为 9091（沿用旧端口，不按查询顺序重排）。
2. **Given** 新增端点 E3，**When** 生成映射，**Then** E3 追加为下一个可用端口（9092），E1/E2 端口不变。
3. **Given** 删除端点 E2，**When** 生成映射，**Then** E2 标记为 stale、其 proxy 可被停止，但 9091 短期**不回收**给其它端点。
4. **Given** 端点集合未变，**When** 重启 collector，**Then** 仅停/起 collector，proxy 保持在线不重启。
5. **Given** 仅 E3 为新增，**When** 重启 collector，**Then** 仅为 E3 新起 proxy_9092，E1/E2 的 proxy 不动。

---

### User Story 4 - 两道关卡：端口闸门（阻断）+ 连接可观测（Priority: P1）

启动链路设两道关卡：**端口闸门**——proxy 端口未就绪时**同步阻断** `start`、collector 不被启动；**连接可观测**——collector 启动后若连 OPC 失败，须在日志与 Agent 状态中可定位，但**不同步阻断** `start`（`start` 在端口闸门通过、collector 进程拉起后即返回）。

**Why this priority**: OPC DA/DCOM 故障在现场高发；没有清晰错误，排查成本极高，且会出现"proxy 显示就绪、collector 默默采不到数"的盲区。

**Independent Test**: 故意让 proxy 端口起不来（或 OPC server 不可达），验证两道闸门分别给出带定位字段的错误——独立可验。

**Acceptance Scenarios**:

1. **Given** proxy 启动后在超时（默认 10s）内 `127.0.0.1:{port}` 仍不可连接，**When** 端口闸门检查，**Then** Agent **不启动 collector**，返回错误含 `endpoint`、`port`、proxy 日志路径。
2. **Given** proxy 端口已 ready 但其连接的 OPC server 不可达，**When** collector 启动后发 connect/read，**Then** `POST start` 在 collector 进程拉起后即返回成功（不等待 OPC 结果）；collector 连接失败被捕获，错误在 collector 日志与 `GET status` 中可见，至少含 `endpoint`、`port`、`serverProgId`、`channel`、proxy 日志路径、collector 日志路径。
3. **Given** 端口闸门失败，**Then** 不产生"半启动"状态：collector 进程不被拉起。

---

### User Story 5 - 停止 collector 时按策略处理 proxy（Priority: P2）

停止 collector 时，按"用户主动停模块"还是"重启 collector"区分是否停 proxy。

**Why this priority**: 影响 proxy 在线连续性；归在 P2，因主链路（P1）不依赖它即可演示。

**Independent Test**: 分别触发"停模块"与"重启"，验证 proxy 一停一留——独立可验。

**Acceptance Scenarios**:

1. **Given** collector 正在运行且关联 proxy_9090，**When** 用户明确"停止 collector 模块"，**Then** 先停 collector，再按策略停 proxy_9090。
2. **Given** collector 正在运行，**When** "重启 collector"，**Then** 先停 collector，但**不无条件**停 proxy；端点未变则 proxy 保持在线。

---

### Edge Cases

- proxy 启动后 ready 闸门超时：collector 不启动，返回可定位错误（US4-1）。
- Agent 重启时 sticky port 被**非 Agent 认领的无关进程**占用：**启动 collector 失败并返回明确错误（端口被占用，含 endpoint/port）**，不自动漂移端口（维持粘性原则与 SC-P03）。注意区分：PID 文件自身 pid+create_time 校验失败仅清理该 stale 记录，不触发端口重分配。
- 端点的 endpoint 为空（本地 OPC，vmHost=""）：仍分配一个端口与一个本地 proxy 实例，映射键用约定值（如 `localhost`）。
- 多端点中部分 proxy ready、部分超时：任一必需 proxy 未 ready 即整体失败，collector 不启动；已起的 proxy 是否回收由停止策略决定。
- proxy 运行期（非启动期）崩溃：本特性暂定**仅上报、不自动重启**（见 Exclusions）。
- Agent 自己的 MySQL 配置与 collector 的 settings.toml 指向不同 MySQL 实例：属实质错配，**拒绝启动 collector**（见 FR-P13）。

---

## Requirements *(mandatory)*

### Functional Requirements

**检测与配置来源**

- **FR-P01**: Agent MUST 在启动 collector 前检测当前启用的 `opcda_center` 端点集合，数据来源为 **Agent 自身 `config.toml` 的 `[mysql]` 配置**（password 经 DPAPI 解密），查询 `device JOIN comm_protocol WHERE device.enable=1` 且协议属中心采集集合。
- **FR-P02**: 协议别名归一化 MUST 与 `collector/check_protocols.py` 的 `CENTER_PROTOCOLS` 集合保持一致（`opcda_center` 及其别名）。

**粘性端口映射**

- **FR-P03**: 端口分配 MUST 粘性：已存在 endpoint 沿用旧 `port_mapping.json` 中的端口；新 endpoint 分配**历史映射中从未使用过**的端口，取 `max(现有端口 ∪ 历史端口) + 1`（9090 起），**短期不复用 stale 端口**；不得按数据库查询顺序整体重排。
- **FR-P04**: 已删除 endpoint MUST 标记 stale（可停其 proxy），但其端口短期**不回收**给其它 endpoint，避免端口漂移；端口回收为后续独立工作。
- **FR-P05**: Agent MUST 将 endpoint→port 映射写入 collector 可读取的 `<agent.runtime_dir>\opcda_proxy\port_mapping.json`（`runtime_dir` 取自 Agent `config.toml`，默认 `C:\ProgramData\DataWatch\runtime`，即默认完整路径 `C:\ProgramData\DataWatch\runtime\opcda_proxy\port_mapping.json`），并通过 `--port-mapping` 传给 collector。

**collector 侧改造**

- **FR-P06**: collector MUST 新增 `--port-mapping <path>` 命令行参数（及等价环境变量 `COLLECTOR_PORT_MAPPING`），显式指定端口映射文件，覆盖现有 `device/manager.py` 中相对模块路径的硬编码读取。
- **FR-P07**: 当 `--port-mapping` 未提供时，collector MUST 保持现有开发期行为不变（`run.bat` + `check_protocols.py` 流程不受影响），即本参数为**增量**能力。
- **FR-P08**: Agent 启动 collector MUST 显式传 `--config <ProgramData 下 settings.toml>`，不依赖进程工作目录推断配置位置。

**SidecarManager 与进程监管**

- **FR-P09**: Agent MUST 新增 `SidecarManager` 管理动态 proxy 实例（命名 `proxy_{port}`）；proxy **不得**进入公开的 `/admin/processes/{name}` modules 集合。
- **FR-P10**: `SidecarManager` 与现有 `ProcessManager` MUST 复用同一进程监管内核（共用 `ManagedProcessRunner` 或等价工具类），复用：stdout/stderr 重定向到日志文件、PID 记录、存活/状态判断、terminate/kill 兜底、日志路径管理。**不得**为 proxy 复刻一套裸 `subprocess.Popen`。
- **FR-P11**: proxy 停止 MUST 直接 `terminate`（短超时后 `kill`），**不得**套用 collector/writer 的 `shutdown_flag` 协议（.NET proxy 不读该标志）。
- **FR-P12**: proxy 子进程日志 MUST 落盘到 Agent 日志区（如 `runtime/logs/proxy_{port}/current.log`），供 OPC DA/DCOM 故障排查。

**一致性与校验**

- **FR-P13**: Agent MUST 校验"用于检测的 MySQL（Agent config）"与"collector settings.toml 的 MySQL"指向同一实例；`host`/`port`/`database` 任一不同时**拒绝启动 collector 并返回配置不一致错误**，避免检测端点的数据源与 collector 实际采集的数据源错配。

**编排流程**

- **FR-P14（启动 collector）**: 顺序 MUST 为 ① 检测端点 → ② 粘性更新 mapping → ③ 写 `port_mapping.json` → ④ 对比现有 proxy 集合，仅差量启动缺失端点的 proxy（并按需停已删除端点的 proxy）→ ⑤ 等所有必需 proxy 端口 ready（超时即失败）→ ⑥ 才启动 collector（带 `--config`/`--port-mapping`）。顺序不可颠倒。
- **FR-P15（Agent 启动认领）**: SidecarManager 启动 MUST ① 读旧 `port_mapping.json` → ② 扫描 `runtime/pids/proxy_*.pid` → ③ pid+create_time 校验 → ④ 存活者加回注册表 → ⑤ 死亡者清理 pid/state；不依赖硬编码模块名。
- **FR-P16（重启 collector）**: MUST 差量、幂等：端点集合未变则保持 proxy 在线、仅 bounce collector；仅对变化的端点动 proxy。
- **FR-P17（停止 collector）**: 先停 collector；"用户明确停模块"可停对应 proxy，"重启 collector"则不无条件停 proxy。

**两道错误闸门**

- **FR-P18（端口闸门）**: proxy 启动后 MUST 检查 `127.0.0.1:{port}` 可连接，超时默认 10s；任一必需 proxy 未 ready 则**不启动 collector**，错误含 `endpoint`、`port`、proxy 日志路径；不得产生半启动状态。
- **FR-P19（连接可观测，非阻断）**: collector 连接 OPC 失败（DCOM/OPCEnum/ProgID/Identity）MUST 在 collector 日志与 Agent `GET status` 中可见，错误至少含 `endpoint`、`port`、`serverProgId`、`channel`、proxy 日志路径、collector 日志路径。本关卡为**可观测式**，**不**同步阻断 `POST start`——`start` 在端口闸门（FR-P18）通过、collector 进程拉起后即返回；OPC 连接失败属运行期可观测故障。

**安全/规范**

- **FR-P20**: 任何 logging/返回不得包含 MySQL/DPAPI 解密后的密码明文（沿用 agent-spec FR-A21）。

### Key Entities

- **OpcdaEndpointSet**: 当前启用的 `opcda_center` 端点集合，来自 MySQL `device`/`comm_protocol`；检测时计算，与上次对比得出新增/删除/不变。
- **PortMapping**: endpoint→port 的粘性映射，持久化于 `runtime/opcda_proxy/port_mapping.json`；Agent 写、collector 读。
- **ProxyInstance**: 动态 proxy 进程的运行态，键为 `proxy_{port}`；属性含 port、endpoint、pid、create_time、status、日志路径；由 SidecarManager 经 PID 文件维护（无内存 Popen 句柄依赖，支持 Agent 重启后认领）。
- **ManagedProcessRunner**: 进程监管内核（从 ProcessManager 抽取），被 ProcessManager 与 SidecarManager 共用。
- **ProxyConfig**: 每个 proxy 的运行配置 JSON（`runtime/opcda_proxy/proxy_{port}.json`），含 `listenPort`、`vmHost`、`serverProgId`、`channel` 等，由 Agent 生成、传给 `OpcDaProxyService.exe --config`。

---

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-P01**: 含 1 个 `opcda_center` 端点时，`POST start` 阶段只验证 proxy 先 ready、collector 后进入 running，且 proxy 必先于 collector 就绪（顺序在日志中可验证）；**首轮读到点位数据不在 `POST start` 同步范围**，归运行态/服务账户 DCOM 验收（见 SC-P06）。
- **SC-P02**: Agent 服务重启后，已存活 proxy 被认领，**不**出现第二个同端口 proxy（端口冲突 0 次）。
- **SC-P03**: 增/删 1 个设备后重启 collector，未变端点的端口保持不变（漂移 0 次），其 proxy 不被重启。
- **SC-P04**: proxy 端口 10s 内未 ready 时，collector 100% 不被启动，且错误信息包含 endpoint/port/proxy 日志路径三项。
- **SC-P05**: collector 连 OPC 失败时，错误信息在 collector 日志可定位到 endpoint/serverProgId/channel。
- **SC-P06**: 在目标 **Windows 服务账户上下文**下，proxy 能连通远程 OPC DA/DCOM 并实际读到点位（成败级验收，本特性内完成）。

---

## Assumptions

- 目标机已安装 .NET Framework 4.8 运行时；安装包携带**已编译**的 `OpcDaProxyService.exe`，不在目标机编译（无需 csc.exe / .NET SDK）。
- 运行目录遵循 `C:\ProgramData\DataWatch\runtime\`（Agent `config.toml` 的 `agent.runtime_dir`），proxy 相关产物置于其下 `opcda_proxy\`。
- 开发期 `run.bat` + `check_protocols.py` 流程保留不变；本特性面向 Agent/打包部署，新增能力为增量。
- Agent 已具备的进程监管能力（agent-spec FR-A07~A12：PID 文件、psutil 指标、健康循环、日志重定向）可被抽取/复用。
- collector 经由 `DeviceManager.get_port(endpoint)` 连接 proxy 的现有机制不变，仅改变端口映射来源（显式文件路径）。

---

## Exclusions（明确不做）

| 排除项 | 说明 |
|--------|------|
| proxy 运行期崩溃自动重启 | 暂定仅上报，不自动重启；是否复用 health loop 自动重启留待 Phase 1.5（与 agent-spec"崩溃不自动重启"一致）|
| 端口回收 | 已删除 endpoint 的端口短期不回收，避免漂移；回收策略后续独立做 |
| proxy 暴露为公开 admin 进程 | proxy 不进入 `/admin/processes/{name}`，不在 app 进程列表呈现 |
| 远程 DCOM 加固配置自动化 | DCOM 权限/Identity 仍按现场手工配置；本特性只负责在服务账户下验证连通性 |
| 安装包/服务注册（Inno Setup/NSSM）| 属打包计划后续 Phase，不在本特性内 |
