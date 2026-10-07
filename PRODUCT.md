# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

- **Frontend**: Vue 3 + TypeScript + Vite 5（art-design-pro admin 模板）
- **Backend**: free-threaded CPython 3.14t（解释器路径见 `backend/.py314t-site`）
- **Edge runtime**: Python 边缘数据面（Storage / Uploader）；Tauri 桌面端（`app/`）负责现场运维
- **Data plane**: Redis（latest / window / stream / DLQ）+ TDengine（历史事实）+ MySQL（拓扑 / 映射 / 权限）
- **CLI**: `data-watch`（`cli/`，基于 Typer），输出永远是 JSON / JSONL
- **打包**: Inno Setup 三角色安装包（centralized / edge / distributed-center，见 `installer/build.ps1` 与 `installer/build-roles.ps1`）
- **部署**: Windows 优先；中心式 + 分布式边缘双路径
- **环境**: 凭据统一通过 `DATAWATCH_<DB>_<FIELD>` env 注入，agent DPAPI 落 `ProgramData`

## Users

- **首要：AI 智能体 / 算法工程师**——`data-watch` CLI + MCP 是他们调用工业数据的主入口。论文与着陆页以此为首要叙事对象。
- **次要：现场研发 / 运维工程师**——直接操作 CLI、阅读日志、定位 NACK 风暴。
- **再次：决策者 / CIO / 评委**——论文与着陆页的说服对象；功能与创新是论据。
- 三类用户共用同一份 `data-watch` CLI：智能体通过 MCP 调、人工通过 bash 调、脚本通过 JSON 输出消费。

## Product Purpose

把自动化码头的工业现场数据（PLC / OPC / HTTP 推送）转换为**结构化、可治理、对 AI 友好的实时数据流**。让"AI 工程师"无需懂现场协议就能消费工业实时数据，让"现场工程师"无需写 AI 集成就能把数据交给智能体。

成功 = 在洋山四期自动化码头等真实工业现场跑通 5 协议统一接入、跨空洞合并、边-云协同、CLI 即 AI 入口四条核心创新，并支撑下游数据消费方（Web 业务 / Tauri 现场 / CLI+AI）。

## Positioning

**5 协议统一受控接入**是主轴：同一个 `data-watch` CLI、同一种 JSON 数据契约、同一个质量码体系覆盖 S7 / GE SRTP / OPC UA / OPC DA / Modbus TCP 五个协议族（各对应一个受控驱动包）；HTTP Push 与 OPC DA sidecar 是外部推送接入方式，共用同一契约但不计入驱动包。邻居产品要抄这个机制，必须重做协议驱动框架、重新建立命令-驱动-数据契约的解耦——这是工程护城河而非营销词。

支撑定位的次级叙事（不喧宾夺主）：跨空洞合并的工程指标、边-云协同的部署灵活性、CLI 即 AI 入口的前瞻性。

## Operating Context

- **物理环境**: 洋山四期自动化码头 305 台 PLC、100ms 采集周期，跨空洞合并工程实测 4s → 0.6s（这些是工程指标，不是营销 KPI，**不着陆页正面宣传**）
- **现场约束**: 现场侧 agent 单进程可独立运行；不绑死某一家云或某一种硬件；网络抖动 / 设备断连 / 中心短暂不可用时保留明确交接边界
- **数据契约**: 输出永远是 JSON / JSONL；质量码 Good / Uncertain / Bad 贯穿驱动层、Redis、Writer、TDengine、Backend、前端
- **运维脚本**: 所有操作留痕、可回放；命令以受控驱动包注册，运行期可审计
- **凭据安全**: 前端禁持久化敏感数据（密码/token）；仅白名单 `data-watch-theme` 主题 key 与登录账号名（非密码）例外

## Capabilities and Constraints

- **接入 5 协议**: S7（S7-300/400/1200/1500）、GE SRTP（RX3i / RX7i / 90-70）、OPC UA（IEC 62541）、OPC DA（通过独立 C# x86 sidecar / proxy 隔离 COM + DCOM 依赖）、Modbus TCP；HTTP Push（REST / Webhook）与 OPC DA sidecar 是外部推送接入方式，不重复计入驱动包
- **双部署**: 中心式（Collector → Redis → Writer → TDengine）+ 分布式边缘（Collector → Edge Storage → SQLite outbox → Uploader → Ingest → 中心）
- **受控驱动包**: 新协议以受控驱动包形式热插拔，不修改调度、缓存与上层业务
- **快照快取**: collector CPU 热点已 O(N²) → O(N) 块级缓存优化；瓶颈是 I/O + 算法而非语言
- **agent 单进程**: collector/writer 单进程 exe，无应用子进程（sidecar 模式，不做内存 children 求和）
- **DESIGN 锁**: 颜色保留青色 `--lp-accent: #43d9ff`（dark）/ `#0a87c2`（light）；字体 Inter + JetBrains Mono + 系统字；不引入新色 / 不替换字体
- **着落页不可呈现的指标**: 305 台 / 100ms / 4s→0.6s 等"凑数数据"不上 top；第一屏只放核心创新点

## Brand Commitments

- 品牌名: **Data Watch / DataWatch**（与 `data-watch` CLI 前缀对齐）
- 着落页身份: 产品介绍页（非营销页），第一屏直接上核心创新点
- 论文奖项: 2026 创新创效
- 着陆页路径: `/landing`（路由名 `Landing`），同时挂在菜单「项目文档」旁的着陆页入口
- 不在公开材料中暴露具体设备台数与采集周期（论文里写，但着陆页不强调）

## Evidence on Hand

- 论文: `docs/论文/`（含 `Data%20Watch汇报稿.docx`、PDF 单一来源在 `frontend/public/论文/`）
- 真实 CLI 命令族: `cli/commands/*.py`（devices / points / dashboard / diagnostics / history / realtime / infrastructure / system / contracts / update）
- CLI 主名: `data-watch`（`cli/main.py:49 name="data-watch"`）
- 论文里强调的 4 条核心创新：5 协议统一 / 跨空洞合并 / 边-云协同 / CLI 即 AI 入口

**禁止杜撰**:
- 没有 Python SDK、没有 MCP 命令——CLI 是 `data-watch` 唯一一等入口
- 没有 `drive / read / trace / replay / diag` 这类命令族（之前 CliDemo.vue 全是编造，已重写为真实命令）
- 不要在公开材料中编造"接入设备数 / 客户 / 价格 / 测试结论"

## Product Principles

1. **数据契约优先**: 协议层只暴露统一接口；新协议以受控驱动包接入；不改调度、缓存、入库与上层业务
2. **采集-治理分离**: 现场侧 agent 独立运行；同一种数据契约、同一种鉴权、可热升级；不绑死某一家云
3. **面向 AI 而非仅面向人**: 输出永远是 JSON / JSONL；CLI 是 AI 的一等入口，不发明私有 API
4. **工程指标可核验**: 跨空洞合并、性能热点、质量码传递——论文里写的指标必须可由第三方在目标机实测复验
5. **现场安全优先于开发体验**: 凭据走 DPAPI / env 注入；前端禁持久化敏感数据；所有操作留痕可回放

## Accessibility & Inclusion

- 双主题（dark / light）持久化主题 key `data-watch-theme`
- prefers-reduced-motion 降级所有动效（已在 `landing.scss` 全局守卫）
- 暗色模式不依赖纯黑背景（`--lp-bg: #0a0a0b` 而非 `#000000`）
- focus ring 走 3px solid 强调色（沿用 `art-design-pro` 全局规范）

## Open Decisions

- 是否将 `/landing` 设为默认首页：当前**非默认**，待哥哥拍板
- 是否对外发布 PDF 论文：单一来源在 `frontend/public/论文/`，分发策略未定
