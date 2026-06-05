---
description: "Task list — 002 Agent OPC DA Proxy 编排（SidecarManager）"
---

# Tasks: Agent OPC DA Proxy 编排（SidecarManager）

**Input**: `specs/002-agent-opcda-proxy/` 的 spec.md + plan.md

**Prerequisites**: plan.md（已通过 review）、spec.md（US1~US5、FR-P01~P20、SC-P01~P06）

**Tests**: 含测试任务（plan「Testing Strategy」已明确要求单元+集成+回归）。

**Organization**: 按 User Story 分组；`ManagedProcessRunner` 抽取与 `start(extra_args=)` 通道为 US1~4 共同底座，单列 Foundational 阻塞层。

## Format: `[ID] [P?] [Story] Description`

- **[P]**: 不同文件、无依赖，可并行
- **[Story]**: 所属 US（US1~US5）；Foundational/Setup/Polish 不带 Story
- 每条含明确文件路径

## 工作流约束（每个 task 通用）

- §I 仅 PowerShell；§II 先诊断后改；§VIII 最小改动。
- **每个 task（或逻辑组）代码完成 → Codex MCP 审查 → 修复 → 自审 → 通过 task 验收 → `/speckit.git.commit`**，再进下一个（项目 Codex 工作流）。
- 含测试的 task：先写测试并确认 FAIL（或对回归类先确认基线 PASS），再实现。

---

## Phase 1: Setup（共享基础）

- [ ] T001 [P] 新建假 proxy 测试桩 `agent/tests/fixtures/fake_proxy.py`：参数化 `--port`，可选 `--delay-ready`（延迟监听）/`--never-listen`（不监听），用于 US1/US2/US4 集成测试（plan O3：Python 桩）。
- [ ] T002 [P] **搭 agent 测试脚手架（agent 现状无 `tests/`、requirements 无 pytest）**：dev 依赖加 `pytest`+`pytest-asyncio`（建议 `agent/requirements-dev.txt`），建 `agent/tests/` + `conftest.py`；测试 fixtures：临时 `runtime_dir`（tmp_path 下 `pids/`、`opcda_proxy/`、`logs/`）与假 collector/proxy spec helper。

---

## Phase 2: Foundational（阻塞所有 US，必须先完成）

**⚠️ 完成前任何 US 不可开工。** 含监管内核抽取 + per-start args 通道（点2）。

- [ ] T003 **为现状 ProcessManager 写表征(characterization)测试** `agent/tests/test_process_manager.py`（agent 此前无此测试）：覆盖 start/stop/restart/status/PID 文件/restart_count 等当前行为，**在抽取前即 PASS**，作为 T005/T006 重构的回归网。
- [ ] T004 [P] 写 `agent/tests/test_managed_process_runner.py` → 先 FAIL：覆盖 spawn、PID 读写/校验、存活、terminate→kill、以及 **per-start `extra_args` 覆盖**（cmd = executable + spec.args + extra_args）。
- [ ] T005 抽取监管内核：新建 `agent/app/services/managed_process_runner.py`，迁移 `process_manager.py` 的 PID 读写/校验（pid+create_time）、子进程 spawn（stdout/stderr→`logs/{name}/current.log`、`CREATE_NO_WINDOW`）、terminate→kill、日志路径管理（FR-P10）。
- [ ] T006 `process_manager.py` 改为持有/调用 runner，并将 `start(name)` 扩为 **`start(name, extra_args: list[str] | None = None)`**——`cmd = [executable, *spec.args, *extra_args]`（点2：不写回 config、不扩 `/admin/config`）；`shutdown_flag` 优雅停止保留在 ProcessManager（FR-A11）；保持 `test_process_manager.py` 全绿。

**Checkpoint**: 监管内核 + extra_args 通道就绪，US 可开工。

---

## Phase 3: User Story 1 — 启动 collector 按需拉起 proxy（P1）🎯 MVP

**Goal**: 主链路——检测→粘性映射→差量起 proxy→端口闸门（三条件）→起 collector（经 extra_args 拼 --config/--port-mapping）；无 OPC DA 直接起。

**Independent Test**: 含 1~2 个 `opcda_center` 端点的库 + 假 proxy，经 Agent 启动 collector，验证 proxy 先 ready、collector 后 running、顺序在日志可见。

### Tests（先写、先 FAIL）

- [ ] T007 [P] [US1] `agent/tests/test_opcda_detect.py`：检测、粘性 `max(现有∪历史)+1`、**stale 端点保留在 mapping 文件→不复用**、一致性校验。
- [ ] T008 [P] [US1] `agent/tests/test_sidecar_manager.py`：用假 proxy 验证 proxy 启动 + **端口闸门三条件**（可连接 / 监听属主 pid==受管 proxy pid / proxy 存活）；含"无关进程占端口 → 判失败、不漂移"。
- [ ] T009 [P] [US1] `collector/tests/test_device_manager.py`：`EndpointPortMapper` 显式 `mapping_file` 加载 + 权威只读模式（**只对自己拥有的 endpoint 查端口；缺失报错、不探测；忽略文件中多余 stale 项**）。

### Implementation

- [ ] T010 [US1] `agent/app/services/opcda_detect.py`：用 Agent `config.toml` 的 `[mysql]`（**`pymysql` + `dpapi_decrypt`，与 `health_checker.py` 一致，非 sqlalchemy/aiomysql**）查 `device JOIN comm_protocol WHERE enable=1`，协议归一化复用 `CENTER_PROTOCOLS` 语义（FR-P01/P02）。
- [ ] T011 [US1] `opcda_detect.py`：粘性分配 + **历史持久化策略（点4）**——读旧 `port_mapping.json`（视为历史超集）；已存在 endpoint 沿用旧端口；新端点取 `max(文件内所有端口)+1`；**已删除 endpoint 的条目继续保留在文件中**（不删除→历史不丢、端口不复用）；写 `<runtime_dir>/opcda_proxy/port_mapping.json`（FR-P03/P04/P05）。
- [ ] T012 [US1] `opcda_detect.py`：FR-P13 一致性校验（Agent MySQL vs collector `settings.toml` 的 host/port/database），不一致返回配置不一致错误、拒绝启动 collector。
- [ ] T013 [US1] **proxy exe 路径解析（点3）**：`sidecar_manager.py`（或 `opcda_detect`/小工具）按部署约定解析 `OpcDaProxyService.exe`（依次找：安装目录 `sidecars/opcda_proxy_service/bin/`、`collector/sidecars/opcda_proxy_service/bin/`），**缺失返回明确错误**（含期望路径），不硬编码散落。
- [ ] T014 [US1] `agent/app/services/sidecar_manager.py`：生成 `proxy_{port}.json`（含 vmHost/serverProgId/channel）+ `runner.spawn(<解析到的 exe> --config ...)`；proxy 实例键 `proxy_{port}`，**不**进 `factory.modules`/`admin._require_known`（FR-P09）；日志落 `logs/proxy_{port}/current.log`（FR-P12）。
- [ ] T015 [US1] `sidecar_manager.py`：**端口闸门（合并原 T026，点1）**——spawn 后轮询直至同时满足或超时（默认 10s）：① `127.0.0.1:{port}` 可连接；② 经 `psutil.net_connections` 确认该端口 LISTEN 属主 pid == 本次 spawn 的 proxy pid；③ proxy 进程仍存活。任一不满足且超时 → **不启动 collector**，错误含 `endpoint/port/proxy 日志路径`；无关进程占用 sticky port（②不满足）→ 同样判失败、**不漂移端口**（守 SC-P03 / spec Edge Cases）。
- [ ] T016 [US1] `collector/main.py`：argparse 增 `--port-mapping`（默认 None；回退 `COLLECTOR_PORT_MAPPING`）（FR-P06）。
- [ ] T017 [US1] `collector/device/manager.py`：`EndpointPortMapper` 增 `mapping_file` 参数（默认现 `PORT_MAPPING_FILE`）+ 权威只读模式；未给 `--port-mapping` 时行为不变（FR-P07）。
- [ ] T018 [US1] `sidecar_manager.py`：`prepare_for_collector_start()`——检测→粘性映射→差量起缺失 proxy→端口闸门（T015），返回 ok/error（FR-P14）。
- [ ] T019 [US1] `agent/app/routers/admin.py`：`start_process("collector")` 先 `prepare_for_collector_start()`，通过后 `pm.start("collector", extra_args=[...])`——按约定**自动拼** `--config`/`--port-mapping`（O1，经 T006 的 extra_args 通道，不动 `apply_process_patch`）；无 `opcda_center` 直接起（US1-3）；writer 路径不变。
- [ ] T020 [US1] `agent/app/factory.py`：lifespan 构建 `SidecarManager`（注入 `runtime_dir` 与 collector 配置约定路径）。

**Checkpoint**: US1 端到端（假 proxy）可验，构成 MVP（SC-P01）。

---

## Phase 4: User Story 2 — Agent 重启认领存活 proxy（P1）

**Goal**: 重启 Agent 后认领仍存活的 proxy，不重复拉起。

**Independent Test**: 保留一个运行中假 proxy，重建 SidecarManager，验证不再起同端口第二实例。

- [ ] T021 [P] [US2] `agent/tests/test_sidecar_manager.py`：认领（pid+create_time 校验、死亡清理、不重复拉起）。
- [ ] T022 [US2] `sidecar_manager.py`：启动认领——扫 `pids/proxy_*.pid` → 校验 → 存活加回注册表/死亡清理（FR-P15，不依赖硬编码名单）。
- [ ] T023 [US2] `factory.py`：Agent 启动时调用认领（构建 SidecarManager 后）。

**Checkpoint**: 服务重启端口冲突 0 次（SC-P02）。

---

## Phase 5: User Story 3 — 端口粘性与差量重启（P1）

**Goal**: 端点不变保持 proxy 在线、只 bounce collector；增删端点端口不漂移。

**Independent Test**: 增/删 1 个设备后重启，验证未变端点端口不变、其 proxy 不重启。

> 注：粘性端口（T011）与"占用不漂移"（T015）已在 US1 落地；本阶段聚焦差量重启。

- [ ] T024 [P] [US3] `agent/tests/test_sidecar_manager.py`：差量重启 + 粘性漂移（增删端点端口不漂移、未变 proxy 不重启）。
- [ ] T025 [US3] `sidecar_manager.py`：差量调整——端点集合 diff，仅对新增/删除端点动 proxy（FR-P16）。
- [ ] T026 [US3] `admin.py`：`restart_process("collector")` 走差量——端点不变只 bounce collector。

**Checkpoint**: 端口漂移 0 次（SC-P03）。

---

## Phase 6: User Story 4 — 端口闸门（阻断）+ 连接可观测（P1）

**Goal**: 端口闸门同步阻断 start（已在 T015）；OPC 连接失败可观测、非阻断，且"谁知道什么"职责清晰（点5）。

**Independent Test**: 假 proxy 不监听 → 端口闸门超时阻断 collector；OPC 不可达 → collector 启动后失败可定位。

- [ ] T027 [P] [US4] `agent/tests/test_sidecar_manager.py` + 相关：端口闸门超时 → collector 不启动、错误含 endpoint/port/proxy 日志路径；连接可观测字段。
- [ ] T028 [US4] **collector 侧**失败日志补 collector 已知的定位字段 `endpoint/port/serverProgId/channel`（落 `collector/core/opcda_ipc_client.py` 或 `collector/drivers/opcda_center_driver.py`）（点5）。
- [ ] T029 [US4] **Agent 侧**：`agent/app/schemas/models.py`（及 status model）补运行期错误摘要 + 日志路径（proxy 日志 / collector 日志），由 Agent `GET status` 组合暴露（点5；可观测、非阻断——`POST start` 拉起进程后即返回）。

**Checkpoint**: 两道闸门语义达成（SC-P04/P05）。

---

## Phase 7: User Story 5 — 停止 collector 的 proxy 策略（P2）

**Goal**: "停模块"可停 proxy；"重启"不无条件停 proxy。

**Independent Test**: 分别触发停模块/重启，验证 proxy 一停一留。

- [ ] T030 [P] [US5] `agent/tests/test_sidecar_manager.py`：停止策略（停模块停 proxy / 重启不停 proxy）。
- [ ] T031 [US5] `sidecar_manager.py` `on_collector_stop(mode)` + `admin.py` `stop_process("collector")` 接入（FR-P17）。

---

## Phase 8: Polish & 验收

- [ ] T032 [P] 按实现回填 `docs/运维/打包.md` 交叉引用（如有偏差）。
- [ ] T033 `specs/002-agent-opcda-proxy/quickstart.md`：服务账户上下文 DCOM 验收清单（SC-P06，手工，目标机以服务账户实测读到点位）。
- [ ] T034 全量回归 `pytest`（agent + collector），确认无破坏。

---

## Dependencies & Execution Order

- **Setup（P1）**: 无依赖。
- **Foundational（P2）**: 依赖 Setup；**阻塞所有 US**（runner 抽取 + extra_args 通道是 US1 编排前提）。
- **US1（P3）**: 依赖 Foundational；MVP，整条竖切（检测/映射/SidecarManager/端口闸门三条件/collector 入参/编排接入）。
- **US2（P4）**: 依赖 US1（SidecarManager + runner pid 机制）。
- **US3（P5）**: 依赖 US1（映射 + proxy 启停）。
- **US4（P6）**: 端口闸门在 US1（T015）已建；本阶段补失败路径测试 + 连接可观测；T028（collector 侧）可与 T029（Agent 侧）并行。
- **US5（P7）**: 依赖 US1（启停入口）。
- **Polish（P8）**: 依赖目标 US 完成。

### Within Each Story

- 测试先写、先 FAIL（回归类先确认基线 PASS）；工具/检测函数 → service → 路由接入；核心实现 → 集成。

### Parallel Opportunities

- T001/T002 并行；T003/T004 并行；各 US 的 `[P]` 测试并行；T028 与 T029 并行。

---

## Implementation Strategy

### MVP First

1. Phase 1 Setup → 2. Phase 2 Foundational（关键，阻塞）→ 3. Phase 3 US1 → **STOP & VALIDATE**（假 proxy 端到端）→ 可演示。

### Incremental

US1（MVP）→ US2 认领 → US3 差量/粘性 → US4 闸门 → US5 停止策略，逐个独立验收后再进下一个。每步 Codex 审查 + 通过验收才提交。

---

## Notes

- `[P]` = 不同文件、无依赖。
- `[Story]` 标签用于追溯到具体 US。
- 验证测试先 FAIL 再实现；每 task 或逻辑组提交一次。
- 避免：模糊 task、同文件冲突、破坏 US 独立性的跨故事依赖。
- 端口闸门必须"可连接 + 属主 pid 校验 + proxy 存活"三条件齐全，单测"可连接"会被无关进程占端口骗过（点1 教训）。
- SC-P06（服务账户 DCOM 实测）不可单元化，列入 T033 手工验收。
