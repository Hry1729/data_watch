# Implementation Plan: Agent OPC DA Proxy 编排（SidecarManager）

**Branch**: `002-agent-opcda-proxy` | **Date**: 2026-06-05 | **Spec**: `specs/002-agent-opcda-proxy/spec.md`

**Input**: Feature specification from `specs/002-agent-opcda-proxy/spec.md`

## Summary

在 Agent 中新增 **SidecarManager**，为 collector 按需编排 OPC DA proxy（`OpcDaProxyService.exe`）的动态生命周期：检测 `opcda_center` 端点 → 生成粘性 endpoint→port 映射 → 差量拉起 proxy → 端口闸门 → 再启动 collector，并让 collector 支持 `--port-mapping` 显式读取映射。SidecarManager 与现有 `ProcessManager` 共用抽取出的 `ManagedProcessRunner` 监管内核，proxy 不进入公开 `/admin/processes`。技术核心：从 `process_manager.py` 抽取 PID/日志/存活/terminate 内核；新增检测、粘性映射、proxy 配置生成、两道闸门与编排时序；collector 端最小增量加 `--port-mapping`。

## Technical Context

**Language/Version**: Python 3.14（free-threaded `python3.14t`），与 agent / collector 现状一致

**Primary Dependencies**: Agent: FastAPI + Uvicorn、`psutil`、`toml`、`sqlalchemy`+`aiomysql`（检测查询）、Windows DPAPI（`agent/app/core/security.py` 解密 `password_enc`）。Proxy: `collector/sidecars/opcda_proxy_service/bin/OpcDaProxyService.exe`（.NET Framework 4.8 / x86，已编译）

**Storage**: 文件态——`<runtime_dir>/opcda_proxy/port_mapping.json`、`<runtime_dir>/opcda_proxy/proxy_{port}.json`、`<runtime_dir>/pids/proxy_{port}.pid`、`<runtime_dir>/logs/proxy_{port}/current.log`；检测数据源为 MySQL（`device` JOIN `comm_protocol`）

**Testing**: `pytest`（agent 与 collector 均已有 `tests/`）；proxy 编排用可控的假 exe（长驻监听指定端口）做集成测试，避免依赖真实 OPC

**Target Platform**: Windows（服务化部署，Agent 作为 Windows 服务）

**Project Type**: 多组件单仓库——Agent（FastAPI 服务）+ collector（PyInstaller exe）

**Performance Goals**: 端口闸门超时默认 10s；启动编排为低频运维操作，无吞吐要求

**Constraints**: 仅 PowerShell（Constitution §I）；Agent 仅监听 `127.0.0.1`；密码不得出现在日志/响应（§FR-A21）；proxy DCOM 连接昂贵脆弱，避免无谓重连

**Scale/Scope**: 端点数量级 ≤ 数十（端口段 9090–9200）；本特性聚焦 Phase 1 编排，不含安装包/服务注册

## Constitution Check

*GATE: Phase 0 前必须通过；Phase 1 设计后复检。*

| 原则 | 适用与符合性 |
|---|---|
| §I PowerShell（不可协商）| 所有命令用 PowerShell；脚本/测试用 `pwsh` 与专用工具 ✅ |
| §II 先诊断后实现（不可协商）| 已通读 `process_manager.py`/`factory.py`/`config.py`/`admin.py`/`check_protocols.py`/`device/manager.py`/`main.py`，本计划即"描述"环节，实现按 task 推进 ✅ |
| §V Agent 职责 | proxy 编排属 Agent 进程生命周期职责的延伸；不进 Tauri、不在 collector/writer 内做 ✅ |
| §VII 验收标准先行 | spec 已给 SC-P01~P06 + 每 US 可测 Acceptance；每 task 落 task 级验收 ✅ |
| §VIII 最小实现 | `ManagedProcessRunner` 抽取为复用所必需（非顺手重构）；不新增 collector readiness 协议（采"连接可观测"）；proxy 不暴露为公开进程 ✅ |
| §IV Tauri 边界 | 本特性不动 Tauri 层 ✅（不涉及）|

**违规**：无。`ManagedProcessRunner` 抽取见 Complexity Tracking 说明（受控、带回归测试）。

## Project Structure

### Documentation (this feature)

```text
specs/002-agent-opcda-proxy/
├── spec.md              # 已完成（review 通过）
├── plan.md              # 本文件
├── data-model.md        # 文件态/实体格式（本计划 Phase 1 产出，下一步补）
├── quickstart.md        # 编排时序与本地验证步骤（下一步补）
└── tasks.md             # /speckit.tasks 产出（本计划不创建）
```

### Source Code (repository root)

```text
agent/app/services/
├── process_manager.py         # 改：抽取监管内核为 ManagedProcessRunner，ProcessManager 复用之（行为不变）
├── managed_process_runner.py  # 新：共享监管内核（PID 文件、日志重定向、存活校验、terminate/kill）
├── sidecar_manager.py         # 新：proxy 动态编排（检测、粘性映射、差量启停、认领、端口闸门）
└── opcda_detect.py            # 新：从 Agent MySQL 配置检测 opcda_center 端点 + 粘性端口分配 + 一致性校验

agent/app/
├── factory.py                 # 改：lifespan 构建 SidecarManager；Agent 启动时认领存活 proxy
├── routers/admin.py           # 改：collector start/stop/restart 走编排（writer 不变）
└── core/config.py             # 改（按需）：collector settings.toml 路径来源、runtime_dir 取值

agent/tests/
├── test_managed_process_runner.py   # 新
├── test_sidecar_manager.py          # 新（认领/差量/端口闸门，用假 proxy exe）
├── test_opcda_detect.py             # 新（粘性分配、stale 不复用、一致性校验）
└── test_process_manager.py          # 改/保：抽取后回归

collector/
├── main.py                    # 改：新增 --port-mapping 参数（及 COLLECTOR_PORT_MAPPING）
├── device/manager.py          # 改：EndpointPortMapper 支持显式 mapping_file + 权威只读模式
└── tests/test_device_manager.py     # 改/补：显式路径加载 + 权威模式
```

**Structure Decision**: 沿用现有多组件布局。Agent 侧新增 3 个 service 模块 + 1 个内核抽取；collector 侧仅 2 处最小增量。不新增顶层目录。

## 实现路径（按组件，供 /speckit.tasks 细化）

### A. 抽取 `ManagedProcessRunner`（FR-P10）
- 从 `process_manager.py` 抽取：PID 文件读写/校验（`_read_pid_file`/`_write_pid_file`/`_verify_alive`，pid+create_time）、子进程 spawn（stdout/stderr→`logs/{name}/current.log`、`CREATE_NO_WINDOW`）、terminate→kill 兜底、日志路径管理。
- `ProcessManager` 改为持有/调用 `ManagedProcessRunner`，**对外行为不变**（FR-A07~A12 全保留），以 `test_process_manager.py` 回归护航。
- `shutdown_flag` 优雅停止协议**留在 ProcessManager**（collector/writer 专用），不下沉到 runner——proxy 不用它。

### B. `SidecarManager`（FR-P09/P11/P12/P14/P15/P16/P17/P18）
- 持有 `ManagedProcessRunner`；proxy 实例键 `proxy_{port}`，**不**注册进 `factory.py` 的 `modules` 字典、不经 `admin._require_known`。
- 启停：start = 生成 `proxy_{port}.json` + runner.spawn(`OpcDaProxyService.exe --config ...`)；stop = runner 直接 terminate（短超时 kill），**不写 shutdown_flag**（FR-P11）。
- 日志落 `logs/proxy_{port}/current.log`（FR-P12）。
- **认领**（FR-P15）：Agent 启动时扫 `pids/proxy_*.pid` → pid+create_time 校验 → 存活加回注册表、死亡清理；不靠硬编码名单。
- **端口闸门**（FR-P18）：spawn 后轮询 `127.0.0.1:{port}` 可连接，超时默认 10s；失败抛带 `endpoint/port/proxy 日志路径` 的错误。

### C. 检测 + 粘性映射 + 一致性（FR-P01/P02/P03/P04/P05/P13）`opcda_detect.py`
- 检测：用 Agent `config.toml` 的 `[mysql]`（DPAPI 解密）查 `device JOIN comm_protocol WHERE enable=1`，协议归一化复用 `check_protocols.py` 的 `CENTER_PROTOCOLS` 语义（FR-P02）。
- 粘性分配（FR-P03/P04）：读旧 `port_mapping.json`；已存在 endpoint 沿用旧端口；新 endpoint 取 `max(现有∪历史端口)+1`（9090 起），**不复用 stale 端口**；已删除 endpoint 标记 stale 但端口不回收。
- 写 `<runtime_dir>/opcda_proxy/port_mapping.json`（FR-P05）。
- 一致性（FR-P13）：比对 Agent MySQL 与 collector `settings.toml` 的 `host/port/database`，不一致则拒绝启动 collector 并返回配置不一致错误。

### D. 编排时序（FR-P14/P16/P17）+ admin 路由接入
- 新增编排入口（建议 `SidecarManager.prepare_for_collector_start()` 返回 ok/error，及 `on_collector_stop(mode)`）。
- `admin.py` 的 `start_process("collector")`：先 `prepare_for_collector_start()`（检测→映射→差量起 proxy→端口闸门），通过后再 `pm.start("collector")`，并确保 collector spec 的 `args` 含 `--config`/`--port-mapping`（见 E + 开放点 O1）。writer 路径不变。
- `restart`（FR-P16）：差量幂等——端点集合未变保持 proxy，仅 bounce collector；仅对变化端点动 proxy。
- `stop`（FR-P17）：先停 collector；"停模块"可停 proxy，"重启"不无条件停 proxy。
- 无 `opcda_center` 端点：跳过 proxy，直接 `pm.start("collector")`（US1-3）。

### E. collector `--port-mapping`（FR-P06/P07/P08）
- `main.py`：`argparse` 增 `--port-mapping`（默认 None；回退 `COLLECTOR_PORT_MAPPING` 环境变量）。
- `device/manager.py`：`EndpointPortMapper` 增 `mapping_file` 参数（默认现有 `PORT_MAPPING_FILE`，消除模块级硬编码读取）；当显式路径提供时为**权威只读模式**——只读不探测、缺失 endpoint 视为错误（避免回到端口探测漂移）。
- 未提供 `--port-mapping` 时**完全保持现状**（FR-P07：`run.bat`+`check_protocols.py` 开发流不受影响）。
- `--config` 已支持（`main.py:472`），Agent 显式传（FR-P08）。

### F. 两道闸门落地（FR-P18/P19）
- 端口闸门：见 B（同步阻断 start）。
- 连接可观测（FR-P19，非阻断）：`start` 在 collector 进程拉起后即返回；collector 连 OPC 失败经其日志 + Agent `GET status` 暴露，错误含 `endpoint/port/serverProgId/channel/proxy 日志/collector 日志`。collector 侧确保失败日志含这些定位字段（落在 collector OPC driver/ipc client 的错误日志）。

## Data Model（实体与文件格式，详见 data-model.md）

- `port_mapping.json`：`{ "<endpoint|localhost>": <port:int>, ... }`，Agent 写 / collector 读（与现有 `EndpointPortMapper` 格式兼容）。
- `proxy_{port}.json`：`{ listenPort, updateRateMs, connectionTimeoutMs, opcServers:[{ vmHost, serverProgId, channel, devices:[] }] }`（与现有 proxy 配置格式一致）。
- `proxy_{port}.pid`：TOML，`{ pid, create_time, started_at, module="proxy_{port}" }`（复用 ProcessManager PID 格式）。
- 端点集合 diff、ProxyInstance 运行态、ManagedProcessRunner 见 spec Key Entities。

## Testing Strategy

- **单元**：`EndpointPortMapper` 粘性/权威模式；`opcda_detect` 粘性分配 + stale 不复用 + 一致性校验；`SidecarManager` 认领（pid+create_time）、差量、端口闸门超时。
- **集成**：用一个**假 proxy 桩**（长驻 TCP 监听指定端口、可控延迟/不监听）覆盖：US1 顺序、US2 认领不重复、US3 差量、US4 端口闸门超时阻断。
- **回归**：`ProcessManager` 抽取后 `test_process_manager.py` 全绿（行为不变）。
- **手工/验收**：SC-P06（服务账户上下文下真实 OPC DA/DCOM 读到点位）须在目标机以服务账户实测，不可单元化。

## 待 /speckit.tasks 细化的任务分组（预览）

1. 抽取 `ManagedProcessRunner` + ProcessManager 回归（A）
2. `opcda_detect`：检测 + 粘性映射 + 一致性（C）
3. `SidecarManager`：启停 + 日志 + 认领 + 端口闸门（B）
4. 编排接入 admin（start/stop/restart 差量）（D）
5. collector `--port-mapping` + 权威模式（E）
6. 连接可观测错误字段补齐（F/FR-P19）
7. 集成测试（假 proxy）+ 服务账户 DCOM 验收清单（Testing）

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| 抽取 `ManagedProcessRunner`（触碰既有 `process_manager.py`）| proxy 需复用 PID/日志/存活/terminate 内核（FR-P10）；裸 `subprocess.Popen` 会丢失日志捕获——而日志正是 SC-P06 DCOM 排查的唯一手段 | 「为 proxy 另写一套」被否：会重复 ~150 行 Windows 进程追踪并丢失日志/崩溃检测；以回归测试约束行为不变 |

## 开放点决议（review 已拍板）

- **O1（collector 配置入口）→ 定：Agent 按约定自动拼 args**。Agent 启动 collector 时，按 `working_dir`/`runtime_dir` 约定自动拼 `--config`（`C:\ProgramData\DataWatch\collector\config\settings.toml`）与 `--port-mapping`（`<runtime_dir>\opcda_proxy\port_mapping.json`），**不**暴露到 `/admin/config` API/UI、不动 `apply_process_patch`（`config.py:78` 保持只写 `executable/working_dir`）。路径属部署约定、非用户可调（§VIII 最小改动）。
- **O2（writer 同治）→ 定：不**。本特性只编排 collector；writer 维持现状，不经 SidecarManager。
- **O3（假 proxy 测试桩）→ 定：Python 监听脚本**。集成测试用可控的 Python TCP 监听桩（跨环境、免编译），不写 .NET 假 exe。
