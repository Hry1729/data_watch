---
description: "Quickstart — 002 Agent OPC DA Proxy 编排：目标机手工验收清单（重点 SC-P06 服务账户 DCOM 实测）"
---

# Quickstart / 手工验收：Agent OPC DA Proxy 编排

本清单覆盖 002 在**目标 Windows 机**上的端到端手工验收。自动化测试（agent/collector pytest）已覆盖编排逻辑；**唯一不可单元化、必须人工在目标机做的是 SC-P06：在 Windows 服务账户上下文下实测读到 OPC DA 点位**。DCOM/OPCEnum/Identity 问题只在真实服务账户上下文（Session 0）暴露，开发机交互式跑通**不算数**。

> 实现落点见 `docs/运维/打包.md` 的「002 实现落点与交叉引用」。

## 0. 前置条件

- [ ] 目标机已装 **.NET Framework 4.8 运行时**（proxy 是 32 位 .NET，无需 SDK/csc.exe）。
- [ ] 安装包已携带**已编译**的 `OpcDaProxyService.exe`，位于 `sidecars/opcda_proxy_service/bin/`（或开发态 `collector/sidecars/opcda_proxy_service/bin/`）。
- [ ] 目标 OPC DA 服务器可达（本机或远程 vmHost），ProgID 与 channel 已知（默认 `ZPMC.OPCServer` / `OPD_Server`）。
- [ ] Agent 以 **Windows 服务**方式安装并运行（NSSM 或原生服务）。**记录 Agent 服务的登录账户**（LocalSystem / 指定服务账户）—— SC-P06 验的就是这个账户的 DCOM 能力。
- [ ] MySQL 可达，`device JOIN comm_protocol` 中存在 `enable=1` 且协议为 `opcda_center` 的设备（否则走「无 OPC DA」分支，proxy 不会启动）。
- [ ] Agent `config.toml` 的 `[mysql]` 与 collector `settings.toml` 的 `[mysql]` **host/port/database 一致**（不一致会被一致性校验拒绝、返回配置不一致错误）。

## 1. 启动链路（US1 主链路，SC-P01）

1. [ ] 通过 app 或直接 `POST /admin/processes/collector/start`（带鉴权 token）启动 collector。
2. [ ] 观察 `runtime/opcda_proxy/port_mapping.json` 生成，endpoint→port 粘性映射（端口 9090 起）。
3. [ ] 每个 `opcda_center` endpoint 对应一个 `OpcDaProxyService.exe` 被拉起，日志在 `runtime/logs/proxy_{port}/current.log`。
4. [ ] **顺序正确**：proxy 端口 ready（端口闸门三条件通过）**之后**才出现 collector 进程；collector 命令行带 `--config` 与 `--port-mapping`。
5. [ ] 无 `opcda_center` 设备时：不启动任何 proxy，collector 直接启动且**不带** `--port-mapping`。

## 2. SC-P06 ★服务账户上下文 DCOM 实测（核心、必做）

> 目标：证明 **Agent 服务账户**能经 proxy 建立 OPC DA/DCOM 连接并**真实读到点位值**，而不仅是「proxy 进程在、端口在听」。

1. [ ] 确认 collector 已 running（经 Agent status，非桌面交互式手动起 proxy）。
2. [ ] 在 OPC 服务器侧用 `dcomcnfg` 确认：OPCEnum 与目标 OPC Server 的 **启动/访问权限**包含 Agent 服务账户；必要时配置 Identity（固定用户 vs 交互用户）。
3. [ ] 远程 vmHost 场景：确认防火墙放行 DCOM（135 + 动态端口范围）、两端账户/密码或域信任一致。
4. [ ] **实测读到点位**：通过实时监控 / TDengine 落库 / collector 日志，确认目标设备的点位**有真实数值刷新**（值随采集变化，而非全 0 / 全空 / 连接错误）。
5. [ ] 记录验证账户、目标 ProgID、vmHost、读到的样本点位与时间，作为 SC-P06 通过证据。

> ⚠️ 若仅在交互式桌面（你登录的会话）跑通、服务账户下读不到 —— 判 SC-P06 **未通过**，多半是 DCOM Identity / Session 0 / 账户权限问题。

## 3. 两道闸门（SC-P04 / SC-P05）

- [ ] **Proxy 端口闸门**：人为让某 proxy 起不来（如占用其 sticky 端口或 exe 缺失），启动 collector → collector **不启动**，错误含 `endpoint` / `port` / proxy 日志路径（API 返回 400）。
- [ ] **Collector 连接闸门（可观测、非阻断）**：proxy 端口 ready 但 OPC 连不通（如 ProgID 错/DCOM 拒绝）→ collector 会启动（非阻断），失败在 **collector 日志**可定位（含 `serverProgId` / `endpoint` / `proxy_port`），且 Agent `GET /viewer/processes/collector/metrics` 暴露 `log_path` 与 `proxy_logs[].log_path` 供排查。

## 4. 重启认领 / 差量 / 停止（US2 / US3 / US5 抽验）

- [ ] **US2 重启认领（SC-P02）**：collector 运行中重启 Agent 服务 → 仍存活的 proxy **不被重复拉起**、端口无冲突（看 `pids/proxy_*.pid` 与日志无二次启动）。
- [ ] **US3 差量（SC-P03）**：
  - 不改设备，`POST .../collector/restart` → 未变 endpoint 的 proxy **不重启**（pid 不变），只 bounce collector。
  - DB 增一个 `opcda_center` 设备后 restart → 只新增对应 proxy，旧端口不漂移（`port_mapping.json` 旧条目端口不变）。
  - DB 删一个设备后 restart → 该 proxy 被停，但 `port_mapping.json` **保留**其条目与端口（端点回归不漂移）。
- [ ] **US5 停止策略（FR-P17）**：
  - `POST .../collector/stop`（停模块）→ collector 停、其关联 proxy **全部停**。
  - `POST .../collector/restart`（重启）→ proxy **不被无条件停**。

## 5. 排错指引（呼应连接可观测）

- proxy 起不来：看 `runtime/logs/proxy_{port}/current.log` + Agent 返回的端口闸门错误（endpoint/port/日志路径）。
- OPC 连不通：看 collector 日志中 `serverProgId=… endpoint=… proxy_port=…` 的连接失败行；经 `GET /viewer/processes/collector/metrics` 拿 `log_path` / `proxy_logs`。
- 端口映射异常：检查 `runtime/opcda_proxy/port_mapping.json`（损坏会在 collector 启动时 fail-fast）。
- 配置不一致：Agent 与 collector 的 `[mysql]` host/port/database 必须一致，否则启动被拒。

## 验收结论

- [ ] US1~US5 抽验通过；两道闸门语义成立。
- [ ] **SC-P06：服务账户上下文下实测读到 OPC DA 点位**（核心，附证据）。
- [ ] agent / collector 全量 `pytest` 通过（见 T034）。
