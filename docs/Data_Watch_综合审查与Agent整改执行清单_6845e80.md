# Data Watch 综合审查与 Agent 整改执行清单

## 2026-10-06 执行补记

本次用户报告的修改、补充 NSSM/S7 两项 P1 和优先四项 P2 已分模块提交；独立补查发现六项漏修，五项已修复并复核，登录限流容量策略仍待用户选择。下列本轮涉及条目更新为“修复已提交”；不批量改变其他历史条目的状态，也不把候选版本称为全部整改完成。源代码提交、独立复核修正、测试边界、回退及待办详见 [整改提交与核验记录](06-验证记录/2026-10/2026-10-06-整改提交与核验记录.md)。

**HEAD 全量验收依据：**提交本补记后运行 `scripts/regression.ps1`，读取 `artifacts/regression/*-final-head-20261007-r3/summary.json`。仅当对应完整 SHA、干净工作区、全部八模块退出 0 且 `overallOk=true` 时，认定 HEAD 全量通过；先前工作区结果不代替它。被中断/主动停止的旧执行保留输出且不计通过，补漏及直接调用兼容修复后重新完整执行。Agent/Backend 两个管理员符号链接用例已实际通过。登录容量策略、正式驱动签名和现场验收仍待完成。

原附录 B 中“本轮实际执行”描述的是原始审查阶段；2026-10-06 的实施事实以此补记与新验证记录为准。

---

## 2. 开始整改前必须守住的边界

### 2.1 架构与数据边界

- **中心路径：**PLC/OPC → Collector → Redis → Writer → TDengine；当前默认使用 `RedisWriter`。不接回采样 SQLite Outbox、Durable 回放或每批 WAITAOF。中心 Redis 返回不自动等于 AOF 或 TDengine 完整确认。
- **分布式路径：**Edge Collector → Edge Storage/SQLite → Uploader → Ingest → Redis → Writer → TDengine。保留本地先存、单写者/有序上传、现行 Ingest AOF 确认边界；不得用旧“清空重来”文档删除未确认载荷。
- **现行事实源：**MySQL 管配置/权限/永久映射，Redis 管实时/缓冲，TDengine 管历史。本文不授权改成 PostgreSQL、取消 Redis 或改变当前值宽表/质量窄表模型。
- **正确性：**不把未知提交当未提交或成功；不因网络重试次数终结好数据；不以组级最大时间戳跳过旧帧；旧代/旧时间样本不得覆盖新 latest；逻辑帧值/质量分段全部完成或明确持久失败隔离后，才能按既有契约处理确认。
- **安全：**生产采集只读 PLC；人和 CLI/AI 仍经平台认证授权；不把管理员服务 token 任意扩散；不记录真实密码/token，不绕过鉴权方便测试。

上述边界不承诺中心无限断网无损，也不承诺端到端严格恰好一次。需要扩大保证时，必须另做容量、可靠性和迁移方案，不能藏在一次 Bug 修复中。

### 2.2 执行纪律

开始时只读输出 `git rev-parse HEAD`、分支、工作区差异和当前运行入口；保留所有用户未提交修改、stash 和工作树。不自动切分支覆盖改动、不 reset/clean，不删除历史证据。HEAD 与本文不同，要先检查涉及文件差异，不要求退回旧版本。

每个任务依次经历：**重新定位 → 最小反例/证据 → 最小修改 → 回归 → 文档同步 → 记录未覆盖边界**。复现不成立时提交明确证据，不为了完成条目强行造 Bug。测试通过必须注明是纯函数、单元、真实入口、隔离集成还是目标机验收。

测试使用独立端口、数据库/容器/目录与哨兵凭据。未经明确授权不得访问 PLC、删业务 Stream/PEL/DLQ/Outbox、重建业务容器/卷、改生产账户或防火墙。不得恢复 MAXLEN 随意裁剪、跳过质量行、减少采样负载、去掉鉴权或删除正确性断言来换绿色结果。

本文授权范围仅是供用户交给 Agent 的整改依据，不是替用户授权生产部署。实际执行默认不推送、不部署、不迁移业务数据；提交/推送边界按用户另外的授权执行。

### 2.3 推荐执行顺序

| 批次 | 任务组 | 出口条件 |
|---|---|---|
| A | 基线与 D39 架构确认；复现 P0：B5/B6/B7/B11/B15/B25/B41；同时检查 S3 的本机信任边界 | 先确认真实可触发路径，明确保护措施，不能带着错误架构继续开发。 |
| B | 小范围确定性修复 B1/B4/B10/B17/B37/B38/B42；对应 D1/D4/D6/D12–D15 | 真实入口/兼容层测试，不仅 mock 绕过生产装配。 |
| C | Collector 生命周期 B3/B32–B36/B46；Agent/Backend 阻塞 B16/B20；Frontend B39/B40；会话 B2/B24 | 并发、取消、重连、卸载、变更权限等边界测试通过。 |
| D | Writer B8/B28–B31 与 D8–D11；安全配置 B9/B13/B14/B18/B21/B22/B45/B47–B50 | 数据保全、资源有界、权限边界和版本兼容均可复验。 |
| E | 其余文档/配置与 S1/S2/S4/S5/S6 的条件验证 | 更新当前规则；不支持/待定项明确保留，不把未测写成完成。 |

批次不是按文件强制并行。`writer/consumer.py`、`backend/app/main.py`、`collector/device/manager.py`、`agent/app/routers/admin.py`、实时页等共享热点应由单一负责人整合，避免多个 Agent 同时改同一状态机。文档随对应代码批次同步，不拖到最后一次性“按印象更新”。

---

## 3. 主编号总览

表中“动作类型”不等于状态。所有条目进入执行前均需在执行 HEAD 复定位；无充分证据的项目先验证。

| 编号 | 问题 | 优先级 | 动作类型 | 另一报告条目 |
|---|---|---|---|---|
| B1 | CLI 显式失败退出码在生产入口丢失 | P1 | 修复 | #1 |
| B2 | Perspective REST 与 WebSocket 的 owner 标识不一致 | P1 | 修复 | #2 |
| B3 | 热替换设备后旧重连任务仍持有旧 Device | P1 | 修复 | #3 |
| B4 | Modbus 地址校验与驱动支持范围不一致 | P1 | 修复 | #4 |
| B5 | 同批已 ACK 帧因后续 session 异常被再次迁移重传 | P0 | 修复 | #5 |
| B6 | 网络重试与永久失败隔离共用 attempt_count | P0 | 修复 | #6 |
| B7 | 新点位/配置已发布而 Ingest 缓存仍旧，导致永久拒收 | P0 | 修复 | #7 |
| B8 | Writer 超限 DLQ 文档与真实隔离/重试机制不一致 | P1 | 修复 | #8 |
| B9 | TDengine 端口发布到全部宿主网卡 | P1 | 配置核验 | #9 |
| B10 | 实时点位卡片把 Good=0 标红 | P1 | 修复 | #10 |
| B11 | Agent 健康检查把已有凭据用于可覆写的新目标 | P0 | 修复 | #11 |
| B12 | 边缘引导保存中心 Redis/TD 凭据，与旧架构宣称冲突 | P1 | 契约决策 | #12 |
| B13 | Agent service token 经父环境扩散到受管子进程 | P1 | 修复 | #13 |
| B14 | Tauri Agent HTTP 代理缺少规范化目标约束 | P1 | 修复 | #14 |
| B15 | 已存在 Agent 配置可保留空 jwt_secret | P0 | 修复 | #15 |
| B16 | Agent 异步请求里运行同步数据库、密码计算和 sidecar 等待 | P1 | 修复 | #16、#17 |
| B17 | realtime-init 对空字符串数值直接转换导致 500 | P1 | 修复 | #18 |
| B18 | 未经可信代理校验的来源 IP 用于登录/日志限流 | P1 | 修复 | #19 |
| B19 | Dashboard 在无记录/部分查询错误时返回合成告警 | P1 | 修复 | #20 |
| B20 | 创建用户和重置密码在 Backend 事件循环同步哈希 | P1 | 修复 | #21 |
| B21 | 被授予角色管理权的用户可越过可授予权限边界 | P1 | 修复 | #22 |
| B22 | 数据范围规则未进入实时订阅授权链 | P1 | 契约与权限修复 | #23 |
| B23 | 历史单点查询的 NULL 行占用分页预算 | P2 | 查询契约优化 | #24 |
| B24 | Perspective 非 WebSocketDisconnect 异常没有统一 detach | P1 | 修复 | #25 |
| B25 | Uploader 在获得单实例锁前执行 recover_inflight | P0 | 修复 | #26 |
| B26 | 验签前未知节点查询和无容量上限缓存可放大资源消耗 | P1 | 修复 | #27 |
| B27 | INGEST_STREAM_MAXLEN 被读取但提交 Lua 固定使用 0 | P1 | 配置与文档治理 | #28 |
| B28 | 某 stable 补列失败扩大到同批该 stable 全部行 | P1 | 修复 | #29 |
| B29 | 永久映射缺失导致重复 MySQL reload 和 PEL 长期滞留 | P1 | 修复 | #30 |
| B30 | Redis 6.2 的 XAUTOCLAIM 两元素响应不兼容三元素解包 | P2 | 修复 | #31 |
| B31 | XACK 后 XDEL 前崩溃留下已确认 Stream 残留 | P1 | 修复 | #32 |
| B32 | S7 热更新未复用完整地址/数据类型组合校验 | P1 | 修复 | #33 |
| B33 | GE 热重载取消 await 后底层共享连接读仍在执行 | P1 | 修复 | #34 |
| B34 | 不同来源的 None 样本对应的失败与缺失语义不一致 | P1 | 数据语义修复 | #35 |
| B35 | generation 的同步 RLock 包住存储写，事件循环可能争锁 | P1 | 修复 | #36 |
| B36 | S7 健康检查/断开与 PLC 读取共用有界池 | P1 | 修复 | #37 |
| B37 | 恢复监控状态未保存/校验 group_code | P1 | 修复 | #38 |
| B38 | 监控页跳转的 deviceId query 未参与实时页初始化 | P1 | 修复 | #38 |
| B39 | 异步初始化在卸载后重建 WS/定时器；KeepAlive 模式差异 | P1 | 修复 | #39 |
| B40 | 业务快照固定键持久化且登出不清导致同浏览器跨账号残留 | P1 | 修复 | #40 |
| B41 | CLI 更新 swap 前半失败时 catch 可删除仍有效的 current | P0 | 修复 | #41 |
| B42 | 参数错误分类和 varSeqs[] 兼容别名重复展开 | P1 | 修复 | #42、#43 |
| B43 | CLI dry-run 未约束更新副作用；改密 dry-run 误报成功与脱敏遗漏 | P1 | 修复 | #44、#45 |
| B44 | CLI watch 成功连接后未恢复连续重连预算 | P2 | 修复 | #46 |
| B45 | CLI 发布接口路径片段允许精确 .. | P1 | 修复 | #47 |
| B46 | deadline 统计语义重复；同地址合并丢失类型语义 | P1 | 修复 | #48、#49 |
| B47 | Backend 直连端口绕过网关；导出路径不在挂载持久层 | P1 | 修复 | #50 |
| B48 | 构建上下文未完整排除秘密和大体积本地产物 | P1 | 修复 | #51 |
| B49 | 前端固定锁屏加密 key 被当作安全边界 | P2 | 边界与实现核验 | #52 |
| B50 | 安装器未显式落实 ProgramData ACL，服务 token 生成缺少安全随机约束 | P1 | 修复 | #53 |
| D1 | 质量码枚举、DriverSample 例子及文档内部矛盾 | P1 | 改文档 | #55、#56、#57 |
| D2 | Stream v1/v2 的质量缺失和兼容规则写错 | P1 | 改契约与测试 | #58 |
| D3 | quality_status 列、质量窄表和 ADR-0005 描述冲突 | P1 | 改文档 | #59 |
| D4 | values/qualities 数组描述、timestamp 示例及 DLQ 示例不一致 | P1 | 改契约 | #60、#61、#62 |
| D5 | latest 的条件 epoch 字段和 Redis 控制状态清单缺项 | P1 | 改契约 | #63、#64 |
| D6 | 首次建立 Redis 消费组的 0 与 $ 不一致 | P1 | 脚本与文档修复 | #65 |
| D7 | DLQ failed_at 示例为 ISO，而生产为 epoch 毫秒 | P2 | 改文档 | #66 |
| D8 | Writer 三态说明遗漏隔离、退避和 FAILED_INCOMPLETE | P1 | 改契约 | #67 |
| D9 | pending drain 默认值与重试/容量配置说明漂移 | P1 | 配置文档治理 | #68、#69 |
| D10 | Writer 新 scope 自动发现及物理子表来源说明不充分 | P2 | 改文档 | #70、#71 |
| D11 | TDengine TAG DDL 长度/类型及质量子表大小写说明不一致 | P1 | 脚本与文档治理 | #72、#73 |
| D12 | HTTP 成功 code 和业务错误 HTTP 状态描述不一致 | P1 | 契约修复 | #74、#75 |
| D13 | 分页字段及用户/角色/菜单 API 方法路径混写 | P1 | 改契约 | #76 |
| D14 | 历史主查询被描述为 POST JSON，与 GET/query/header 不符 | P1 | 改契约 | #77 |
| D15 | WS 触发机制、条件字段、缺参错误及 pong 说明不完整 | P1 | 契约与局部行为修复 | #78、#79、#80、#81 |
| D16 | MySQL “当前 26 表”快照缺少新列与分布式结构 | P1 | 改文档 | #82 |
| D17 | sent 记录 60 秒 fallback 与 Uploader 24 小时默认混淆 | P1 | 配置文档治理 | #83 |
| D18 | 磁盘 95% 时旧文档要求丢积压，与当前保护策略冲突 | P1 | 改契约 | #84 |
| D19 | session 切换“清空重来”与当前迁移未确认行不一致 | P1 | 改契约 | #85 |
| D20 | 文档描述主动 429 限流，而 Ingest 本身未接通 | P2 | 能力补齐或文档修正 | #86 |
| D21 | Ingest Docker 启动绕过 worker 公式，health 展示的是配置值 | P2 | 启动与文档修复 | #87 |
| D22 | Collector 文档旧内置驱动路径和硬编码行号失效 | P2 | 改文档 | #88 |
| D23 | 调度文档仍用每任务循环解释当前每设备统一循环 | P2 | 改文档 | #89 |
| D24 | 熔断器说明未区分初次连接、重连和错误类型 | P2 | 文档与异常契约修复 | #90 |
| D25 | OPC 代理端口动态分配和权威 mapping 两种模式混写 | P2 | 改文档 | #91 |
| D26 | 实时页未使用文档所写 composable；缓存路由生命周期差异 | P2 | 文档与前端治理 | #92 |
| D27 | 浏览器持久化白名单和实际业务/偏好缓存不一致 | P1 | 安全文档与测试 | #93 |
| D28 | Landing 实际菜单位置可能与 PRODUCT 不同 | P3 | 待核验 | #94 |
| D29 | README 同时展示 Apache 许可又称没有 LICENSE | P3 | 改文档 | #95 |
| D30 | 5 协议叙事与五个驱动包、HTTP Push 接入方式混数 | P2 | 产品文档治理 | #96 |
| D31 | PRODUCT 构建入口错误且仍描述单安装包 | P2 | 改文档 | #97 |
| D32 | Compose 的 MySQL 服务清单与离线镜像目录说明存在版本差异 | P2 | 版本与文档治理 | #98 |
| D33 | 外置驱动安全文档否认已有的包签名校验 | P1 | 改安全文档 | #99 |
| D34 | 手动生成 Agent token 指引未优先说明安装器已有值 | P2 | 改运维文档 | #100 |
| D35 | 开发脚本优先查作者个人绝对解释器路径 | P2 | 脚本与文档修复 | #54、#101 |
| D36 | Agent 编排文档的路径、Writer 参数与 Redis DB 注入不准确 | P2 | 文档与配置接线 | #102、#103、#104、#105 |
| D37 | 文档否认子进程求和，监控实现却统计进程树 | P2 | 改指标文档 | #106 |
| D38 | 通用 NSSM 安装脚本默认中心角色，与固定角色包混淆 | P2 | 脚本与文档修复 | #107 |
| D39 | DW-01 仍把已撤销的中心 SQLite+WAITAOF 写成当前实现 | P1 | 优先改架构文档 | 新增补充 |
| S1 | TDengine 字符串反斜线转义是否影响合法 tag 值 | P2 | 验证优先 | #108 |
| S2 | TDengine 错误子串启发式可能误分连接/数据错误 | P2 | 验证优先 | #109 |
| S3 | Edge Storage 本机 HTTP IPC 未认证即可改变 Outbox/配置状态 | P1 | 本机安全修复 | #110 |
| S4 | 无时区历史字符串在不同时区浏览器解释不同 | P2 | 时间契约验证 | #111 |
| S5 | Backend healthz 不检查三库是否可读 | P3 | 探针用途核验 | #112 |
| S6 | 镜像缺 perspective-python 而开关默认 true | P3 | 能力验证 | #113 |

---

## 4. 代码、行为与运行风险

### B1｜CLI 显式失败退出码在生产入口丢失

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L13；另一报告 #1。

**判断：**核心缺陷成立；不是所有失败路径都退出 0。用法错误另有处理。

**代码位置 / 文档位置：**cli/main.py：生产 main / app(standalone_mode=False)、execute / execute_stream；cli/tests/test_main.py。旧报告参考 165–239、681–706 行。

**处理方式：**明确接收并转换 Typer/Click 返回的退出状态；保持现有异常映射与 JSON/JSONL 输出，成功回调的任意业务返回值不能误当退出码。

**验收要求：**用独立子进程调用真实入口，覆盖成功、参数错、认证、权限、网络、超时、服务异常和中断；同时断言退出码、stdout 单一包络和 stderr。不能仅用 CliRunner 替代生产入口验证。

### B2｜Perspective REST 与 WebSocket 的 owner 标识不一致

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L14；另一报告 #2。

**判断：**后端会话键不匹配成立；普通 REST 历史查询不因此失效，当前 UI 是否实际使用该通道需确认。

**代码位置 / 文档位置：**backend/app/services/history.py：历史快照 owner 生成；backend/app/api/routes/perspective.py：WS owner；backend/app/services/perspective.py：会话键；frontend/src/views/device_monitor/history/perspective-session.ts。

**处理方式：**统一按认证后的稳定用户 ID 派生 owner，并统一 REST/WS 调用契约；盘点现行 UI 调用方。不可把原始 token 当稳定身份，不可为消除冲突取消 owner 隔离。

**验收要求：**同用户 token 轮换前后 REST 快照可由正确 WS 会话读取；不同用户同 sessionId 仍隔离；连接恢复与普通 REST 查询均通过。

### B3｜热替换设备后旧重连任务仍持有旧 Device

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L15；另一报告 #3。

**判断：**竞态成立；可能阻挡新代设备重连或重新连接旧对象，不是必然永久失联。

**代码位置 / 文档位置：**collector/device/manager.py：swap_device / remove_device / schedule_reconnect / _reconnect_tasks；collector/monitor/health.py。

**处理方式：**交换设备时按 device_id 与 generation 取消并收尾旧重连任务，清理旧 driver；重连提交前再验证当前代。等待旧任务时不得持有阻塞全局的锁。

**验收要求：**旧地址不可达、随后恢复、新设备再次断连、连续两次热替换均覆盖；旧代不能更新当前状态或遗留连接，新代可继续重连。

### B4｜Modbus 地址校验与驱动支持范围不一致

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L16；另一报告 #4。

**判断：**冷启动合法地址被误拒成立；热更新失败一般保留旧配置，不能概括为必使进程退出。

**代码位置 / 文档位置：**collector/core/config_validator.py：按协议分发的地址校验；packages/modbus_tcp/modbus_tcp/address.py；collector/main.py：冷启动校验与候选设备装配。

**处理方式：**让 Modbus 校验复用该驱动的地址规则，并区分协议不存在、设备配置错误和全局配置错误；不要通过跳过所有校验解决。全局 fail-fast 与单设备隔离边界需明确。

**验收要求：**HR10、holding:10 等合法地址通过；错误区域/偏移/类型仍被拒；多设备中一个无效设备的处理符合明确的隔离规则。

### B5｜同批已 ACK 帧因后续 session 异常被再次迁移重传

**优先级 / 动作：**P0 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L17；另一报告 #5。

**判断：**确认结果丢失路径成立；需同批先成功、后命中 commit_unknown/seq_reject 等分支。

**代码位置 / 文档位置：**edge/app/uploader.py：ACK 遍历、completions、session 切换分支；edge/app/outbox.py：complete_batch / complete_session_switch；ingest/app/redis_store.py：session 高水位去重。

**处理方式：**先持久提交已明确确认的项，再处理未确认/未知项；未知结果优先在原身份下重试或对账。跨 session 恢复必须保留原帧身份和可审计重复策略；不得清空 outbox、跳过高水位之前所有数据或把未知结果视为未提交。

**验收要求：**混合 ACK+commit_unknown、ACK 应答丢失、确认后本地崩溃、切换中崩溃均注入；确定 ACK 的项不再作为未确认行迁移；未确认载荷保全；重放按已声明的至少一次/幂等边界对账。

### B6｜网络重试与永久失败隔离共用 attempt_count

**优先级 / 动作：**P0 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L18；另一报告 #6。

**判断：**计数污染成立；“两分钟/500 条”不是固定结果。dead 延迟清理仍可能造成不可恢复的丢失。

**代码位置 / 文档位置：**edge/app/outbox.py：mark_retry_batch / attempt_count / maintenance；edge/app/uploader.py：quarantine_after_max 分支与失败分类。

**处理方式：**分离传输尝试、协议拒绝和确认不可恢复的数据错误计数；429/5xx/断网不得消耗坏数据隔离预算。明确定义 dead 的人工恢复、保留与删除规则，不自动扩大删除权限。

**验收要求：**长时间断网后恢复再遇一次序列异常，好数据不能因历史网络次数被判 dead；真正非法项有独立证据；检查延迟清理前后的可恢复性。

### B7｜新点位/配置已发布而 Ingest 缓存仍旧，导致永久拒收

**优先级 / 动作：**P0 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L19；另一报告 #7。

**判断：**缓存窗口竞态成立；窗口结束不代表窗口内被判 dead 的帧自动恢复。

**代码位置 / 文档位置：**backend/app/services/catalog.py 与 api/routes/catalog.py：点位事务/版本更新；backend/app/api/routes/distributed_nodes.py：失效发布；ingest/app/repository.py 与 service.py：版本/永久映射校验。

**处理方式：**控制面成功提交后统一发布缓存失效；永久拒绝前对新版本/未知点位做一次受控权威重读，避免把缓存落后判为非法。处理通知丢失、并发 miss 与版本竞争；不可先通知后提交。

**验收要求：**人为保留旧缓存并立即上传新点、修改后新版本、通知丢失、多 worker 场景；合法帧不得 rejected_consumed；真正越权或非法点仍拒绝。

### B8｜Writer 超限 DLQ 文档与真实隔离/重试机制不一致

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L20；另一报告 #8。

**判断：**缺少 pending_max_retry 运行规则成立；不能据次数把数据库故障视为坏数据。持续失败可能占满可用容量。

**代码位置 / 文档位置：**writer/consumer.py：配置读取、_handle_entry、_flush、隔离/退避/容量；writer/tdengine_writer.py：错误分类；docs/02-模块设计/writer/README.md。

**处理方式：**区分数据毒丸、schema 未就绪、连接故障、结果未知；为长期问题提供有界、公平、可持久恢复的隔离。是否新增终结策略须先写契约；不得单纯按次数 ACK/丢弃，或恢复已废弃的随意裁剪。与 B28/B29/D8/D9 一起设计。

**验收要求：**坏行+好行混合、MySQL/TD 长时间故障与恢复；确认好行可推进、资源有界、未确认原始帧可恢复，日志/指标区分原因。

### B9｜TDengine 端口发布到全部宿主网卡

**优先级 / 动作：**P1 / 配置核验。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L21；另一报告 #9。

**判断：**Compose 映射成立；默认密码、实际可达性和未授权读写未由仓库证明。“无密码”不等于使用默认凭据。

**代码位置 / 文档位置：**deploy/docker-compose.yml：tdengine.ports；deploy/taos.cfg；deploy/backend.env.example；目标机账户、防火墙与有效 Compose 配置。

**处理方式：**先确认部署角色与合法访问拓扑，再采用回环或受控地址、网段 ACL 和最小权限账户；外部服务场景不能盲目改成回环而切断远端采集/查询。只生成配置建议，生产改密/防火墙变更另行授权。

**验收要求：**隔离环境验证授权端能访问、未授权网段和错误账户被拒；记录实际绑定和认证配置。不得在现场开展破坏性扫描或使用真实凭据外传。

### B10｜实时点位卡片把 Good=0 标红

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L22；另一报告 #10。

**判断：**确定成立，范围是所查实时卡片。

**代码位置 / 文档位置：**frontend/src/views/device_monitor/realtime/index.vue：q===0 卡片样式及错误注释；collector/core/quality_codes.py；backend/app/core/quality_codes.py。

**处理方式：**按当前平台质量码建立共享展示映射，Good/Uncertain/Bad/Stale/未知值分别标注；同步前端注释，不修改平台码迎合旧 UI。

**验收要求：**覆盖已定义码、保留码、质量缺失；用文字/图标配合颜色，正常值不再红色，异常与未知不显示为正常。

### B11｜Agent 健康检查把已有凭据用于可覆写的新目标

**优先级 / 动作：**P0 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L28；另一报告 #11。

**判断：**有管理员身份及目标可达等前提的凭据使用边界问题成立；不同数据库协议不都明文传输密码。

**代码位置 / 文档位置：**agent/app/routers/admin.py：健康检查路由；agent/app/services/health_checker.py：目标合并/DPAPI 解密/check_tdengine 等。

**处理方式：**目标变化时不得隐式沿用存储凭据；绑定凭据与获准目标，或要求新目标显式临时凭据及授权。限定数据库协议/地址/端口并防重定向泄漏。

**验收要求：**以隔离接收端和哨兵凭据测试；改变 host/port 不会拿到原存储凭据；合法已配置目标检查正常，审计记录不含秘密。

### B12｜边缘引导保存中心 Redis/TD 凭据，与旧架构宣称冲突

**优先级 / 动作：**P1 / 契约决策。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L29；另一报告 #12。

**判断：**保存行为成立但为 DPAPI 密文；新版边缘文档又明确要求三库连接，属于架构/文档冲突，不是已证实明文泄漏。

**代码位置 / 文档位置：**app/src/pages/EdgeBootstrapPage.tsx；agent/app/routers/bootstrap.py；agent/app/core/config.py；docs/02-模块设计/edge/边缘运行时实现说明.md。

**处理方式：**盘点每种角色真正使用哪些库。保留现行账户校验所需链路；对无业务用途的 Redis/TD 凭据提出移除及兼容迁移方案。跨角色删字段/改变登录方式须先明确需求，不能直接清掉用户现存配置。

**验收要求：**列出角色×凭据×消费者矩阵；确认 Edge Collector/Uploader 不直连中心 Redis，更新引导/文档/迁移且不会中断合法登录。

### B13｜Agent service token 经父环境扩散到受管子进程

**优先级 / 动作：**P1 / 修复。**执行状态：修复已提交 `68cbda16`；预检子环境及凭据过滤回归通过，HEAD 全量依据见执行补记。**

**来源映射：**初始报告材料摘录 L30；另一报告 #13。

**判断：**继承路径成立；token 仅能访问接受该服务身份的接口，不等于所有 admin 权限。

**代码位置 / 文档位置：**agent/app/services/managed_process_runner.py：launch 环境组装；sidecar_manager.py：proxy 启动；agent/app/core/deps.py；agent/app/routers/admin.py。

**处理方式：**对子进程使用最小环境白名单或显式删除非必要服务令牌；对每个模块注入确需字段。明确服务身份权限范围，不通过放开 JWT 校验补救。

**验收要求：**以替身进程输出环境变量名及哨兵值验证，Collector/Writer/Proxy 不继承无关令牌；必要 DB 配置仍正确且不进入日志。

### B14｜Tauri Agent HTTP 代理缺少规范化目标约束

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L31；另一报告 #14。

**判断：**条件性风险成立。初始报告写的是 @evil.com/x，不是 /@evil.com/x；复核报告对后者的反驳没有反驳原例。

**代码位置 / 文档位置：**app/src-tauri/src/http_proxy.rs：agent_request / URL 拼接与令牌注入；app/src/lib/agent-client.ts：正常封装；Tauri IPC 权限配置。

**处理方式：**先解析/构造固定 origin，再校验无 userinfo、目标 host/port 处于允许集合、path 为站内路径；重定向后不得携带 token 离开该目标。约束须在 Rust 侧执行，不能只依赖 TS 封装。

**验收要求：**使用实际 Rust URL 库测试 @、/@、//、绝对 URL、编码和重定向；正常路径可用，恶意 IPC 参数不能把哨兵 token 发往其他 origin。

### B15｜已存在 Agent 配置可保留空 jwt_secret

**优先级 / 动作：**P0 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L32；另一报告 #15。

**判断：**条件性缺陷成立；配置不存在时自动生成随机密钥的路径正常。

**代码位置 / 文档位置：**agent/config.toml.example；agent/app/core/config.py：load_config；agent/app/core/security.py：JWT 签验；agent/app/core/deps.py。

**处理方式：**对既有配置进行强制密钥校验，空/弱示例值不得启动认证服务；在明确首装场景原子生成并保存安全随机值。变更密钥导致会话失效应明确提示，不静默频繁轮换。

**验收要求：**无配置、空密钥、正常已配、损坏配置、并发首装与重启；服务不接受空密钥伪造 token，合法存量配置保持稳定。

### B16｜Agent 异步请求里运行同步数据库、密码计算和 sidecar 等待

**优先级 / 动作：**P1 / 修复。**执行状态：修复已提交 `68cbda16`；工作线程与生命周期串行/取消回归通过，HEAD 全量依据见执行补记。**

**来源映射：**初始报告材料摘录 L33；另一报告 #16、#17。

**判断：**阻塞点成立；副作用局限同 Agent 事件循环，10 秒只是单 proxy 等待值。

**代码位置 / 文档位置：**B16.1 agent/app/routers/auth.py：_mysql_roles 中 PyMySQL 和 verify_password；B16.2 agent/app/routers/admin.py：_collector_extra_args；B16.3 agent/app/services/sidecar_manager.py：prepare_for_collector_start。

**处理方式：**分别将数据库/密码和 sidecar 阻塞单元移入有界执行资源；连接与查询均设超时，超时后明确收尾。不得只写 async 包装同步调用，也不得制造无界线程或后台重复启动 proxy。

**验收要求：**数据库慢连/慢查询、密码高并发、多 endpoint proxy 启动；同进程 health/API 仍可响应，队列受限，取消不会遗留连接和错误进程归属。

### B17｜realtime-init 对空字符串数值直接转换导致 500

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L37；另一报告 #18。

**判断：**数值类型及匹配点位分支成立；坏质量码本身不足以触发。

**代码位置 / 文档位置：**backend/app/services/monitor.py：int(float(value))/float(value)；api/routes/monitor.py：realtime-init；上游 latest 生产路径。

**处理方式：**统一值与质量归一化，处理空串、None、非有限数、无效数值和损坏载荷；返回明确无值/坏质量，不能转换成零值掩盖异常。

**验收要求：**BOOL/INT/REAL/STRING 各类型与空/非法/缺失质量组合；单坏点不导致整页 500，也不污染其他正常点。

### B18｜未经可信代理校验的来源 IP 用于登录/日志限流

**优先级 / 动作：**P1 / 修复。**执行状态：补漏修复已提交 `faa1753a`；client 日志复用可信来源解析，Backend 回归通过，HEAD 全量依据见执行补记。**

**来源映射：**初始报告材料摘录 L38；另一报告 #19。

**判断：**来源伪造风险成立；登录和日志是独立限流器，“无限猜测”夸大。

**代码位置 / 文档位置：**backend/app/api/routes/auth.py：_resolve_client_ip；backend/app/core/login_limiter.py；backend/app/core/log.py；backend/app/main.py：HeaderParser/CompatRequest；deploy/nginx.conf。

**处理方式：**依据连接对端和可信代理链解析来源；查明 query/header 同名参数优先级，禁止查询参数伪装安全头。账号维度与来源维度结合限流；网关清洗转发头，不能直接信任最左项。

**验收要求：**经网关/直连、伪造 XFF/query、可信和不可信代理、并发请求；来源不能任意变换绕过账号保护，正常用户不被错误共享桶封锁。

### B19｜Dashboard 在无记录/部分查询错误时返回合成告警

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L39；另一报告 #20。

**判断：**生产未隔离的合成数据路径成立；并非所有 MySQL 故障都能越过鉴权返回假数据。

**代码位置 / 文档位置：**backend/app/services/dashboard.py：recent events / alarm distribution 合成分支；对应 Dashboard 前端错误/空态。

**处理方式：**真实无告警返回空结果；依赖或查询失败返回明确降级/错误；演示数据只能在显式非生产模式使用并清晰标记。不得为了“有图”填假告警。

**验收要求：**有记录、零记录、表缺失、查询错误、数据库不可用逐项测试；生产绝无合成业务告警，正常空态不报数据库错误。

### B20｜创建用户和重置密码在 Backend 事件循环同步哈希

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L40；另一报告 #21。

**判断：**阻塞点成立；固定约 1 秒和全站停顿未实测。

**代码位置 / 文档位置：**backend/app/services/system_manage.py：create_user / reset_user_password；backend/app/core/password_worker.py；backend/app/core/security.py。

**处理方式：**复用现有有界密码工作单元，明确超载错误和数据库事务边界；保持哈希强度，不通过减少安全轮次追求响应速度。

**验收要求：**并发建用户/改密/重置下其他轻量请求能响应；超载被拒且无半完成用户、明文密码日志或不一致 token_version。

### B21｜被授予角色管理权的用户可越过可授予权限边界

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L41；另一报告 #22。

**判断：**有前置管理权限的潜伏风险成立，不是匿名或默认普通账号提权。

**代码位置 / 文档位置：**backend/app/api/routes/system_manage.py：角色更新/授权；backend/app/services/system_manage.py：update_role / update_role_grant；R_SUPER 保护逻辑。

**处理方式：**明确授权者可管理哪些角色、可授予哪些权限；向 service 传递当前主体，保护内置超级角色及最后一个有效超级管理员；统一更新、授权、删除、禁用路径。

**验收要求：**委派管理员不能给自身角色增加超出授权范围的权限；合法超级管理员可操作，不能删除/锁死最后恢复入口。

### B22｜数据范围规则未进入实时订阅授权链

**优先级 / 动作：**P1 / 契约与权限修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L42；另一报告 #23。

**判断：**执行缺口成立；范围表并非完全不读，角色页面会读取。当前实时 WS 已有身份和功能权限校验。

**代码位置 / 文档位置：**backend/app/api/routes/monitor.py：WS subscribe；backend/app/services/monitor.py：设备/组校验；backend/app/services/ws_push.py；SystemManageService 的 dataScopes。

**处理方式：**先明确数据范围是否已对用户承诺。若启用，统一服务端执行用户×设备×分组范围并应用 REST/WS/导出等相关读取；若仍骨架，应纠正文档和 UI 不得伪称已生效。无范围时的默认策略需明确，不能暗自全拒或全开。

**验收要求：**窄范围账号不能读取范围外设备，授权变更后既有 WS 在规定时间内重新鉴权；仅改前端隐藏不算完成。

### B23｜历史单点查询的 NULL 行占用分页预算

**优先级 / 动作：**P2 / 查询契约优化。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L43；另一报告 #24。

**判断：**查询行为成立；缺失样本是否保留是接口语义问题，不直接等于丢数据。

**代码位置 / 文档位置：**backend/app/services/history.py：get_raw_data 及宽表点位查询；writer/tdengine_writer.py：多组宽表写入。

**处理方式：**区分有效样本明细、对齐时间轴和质量/空洞审计模式；有效样本接口可按契约过滤 NULL，时间轴模式保留缺口并标注。禁止给所有历史 SQL 统一加 IS NOT NULL。

**验收要求：**多组不同频率写入、NULL 与坏质量、多页游标；有效值可完整分页，缺口审计不被过滤，时间游标不重复/漏点。

### B24｜Perspective 非 WebSocketDisconnect 异常没有统一 detach

**优先级 / 动作：**P1 / 修复。**执行状态：修复已提交 `faa1753a`；finally 释放会话，Backend 回归通过，HEAD 全量依据见执行补记。**

**来源映射：**初始报告材料摘录 L44；另一报告 #25。

**判断：**清理缺口成立；残留可持续冲突，但不是服务端自身必然死循环。

**代码位置 / 文档位置：**backend/app/api/routes/perspective.py：WS 生命周期；backend/app/services/perspective.py：detach / active_connection / TTL。

**处理方式：**连接身份建立后用 finally 幂等清理，并验证 detach 的连接 ID，防旧连接清理掉新连接；异常与 TTL 修复均保持 owner 隔离。

**验收要求：**鉴权后发送错误、业务异常、异常断开、正常断开、旧连接延迟清理、新连接重入；无残留活跃锁，也不误删有效会话。

### B25｜Uploader 在获得单实例锁前执行 recover_inflight

**优先级 / 动作：**P0 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L48；另一报告 #26。

**判断：**确定的初始化顺序风险；改变租约不是直接删除载荷。

**代码位置 / 文档位置：**edge/uploader_main.py：Uploader 构造与 SingleInstanceLock 顺序；edge/app/uploader.py：构造期 recover_inflight；edge/app/outbox.py：租约恢复。

**处理方式：**先获得唯一运行权，再执行任何 outbox 状态恢复和维护；将有副作用初始化移出无围栏构造器。锁失败不得改变存储状态。

**验收要求：**两个进程争同一 outbox：失败实例无状态写；胜出实例正常恢复旧崩溃租约；正常运行实例的 inflight 不被重置。

### B26｜验签前未知节点查询和无容量上限缓存可放大资源消耗

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L49；另一报告 #27。

**判断：**缓存过期不主动淘汰和唯一 UUID 增长风险成立；重复同 UUID 不一定每次查库。

**代码位置 / 文档位置：**ingest/app/security.py：verify；ingest/app/repository.py：get_approved_node_public_keys / 公钥缓存；ingest/app/main.py：上传前置校验。

**处理方式：**为正/负缓存设容量和 TTL 淘汰，合并同 key 并发 miss；对不可认证请求采用有界工作与合理限流。保留公钥轮换/吊销失效，不把旧授权永久缓存。

**验收要求：**大量唯一未知 UUID、重复 UUID、并发冷 miss、节点吊销/换钥；缓存和查询并发有上限，正常节点不被持续挤出。

### B27｜INGEST_STREAM_MAXLEN 被读取但提交 Lua 固定使用 0

**优先级 / 动作：**P1 / 配置与文档治理。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L50；另一报告 #28。

**判断：**失效配置成立；代码刻意不裁剪未消费数据，不能直接视为应恢复的功能。

**代码位置 / 文档位置：**ingest/app/config.py；ingest/app/redis_store.py：process_batch / pipe.eval 的 maxlen 参数；相关运维模板。

**处理方式：**明确废弃或拒绝该无效配置，迁移文档到安全容量策略；保持未确认 Stream 数据不被按长度任意裁剪。对已确认残留的清理另见 B31。

**验收要求：**配置正值不会悄悄产生与说明相反的行为；Writer 停止时触达容量边界必须显式背压/告警，不能丢未确认帧来制造稳定。

### B28｜某 stable 补列失败扩大到同批该 stable 全部行

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L54；另一报告 #29。

**判断：**成立于单次 write_multi_table 输入中的相同 stable；不是全系统所有设备所有历史。

**代码位置 / 文档位置：**writer/tdengine_writer.py：_ensure_columns / write_multi_table / unready_stables；writer/consumer.py：schema retry 隔离。

**处理方式：**按实际缺列及行依赖隔离，尽可能让只依赖既有列的合法行推进；对 schema 故障有界退避。不要把 schema 未就绪按次数判为 BAD_DATA。

**验收要求：**一个 stable 旧列好行+新增失败列行+另一 stable 好行；好行可推进，失败原帧保留；列恢复后完整补齐值/质量再确认。

### B29｜永久映射缺失导致重复 MySQL reload 和 PEL 长期滞留

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L55；另一报告 #30。

**判断：**持续缺映射时成立；映射补齐后可恢复，不是每帧永远访问数据库。

**代码位置 / 文档位置：**writer/consumer.py：未知 var_id reload / _handle_entry；writer/mysql_mapper.py：get_cached / reload；point_storage_mapping。

**处理方式：**按设备/组/版本或失效事件合并 reload，对缺失映射设置短期负缓存与退避并可快速失效；保全原消息，不因暂时缺映射 ACK 掉。

**验收要求：**连续缺映射高频输入下 MySQL 请求有界；补齐映射后及时恢复；新点不被陈旧负缓存永久屏蔽，其他设备继续推进。

### B30｜Redis 6.2 的 XAUTOCLAIM 两元素响应不兼容三元素解包

**优先级 / 动作：**P2 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L56；另一报告 #31。

**判断：**版本条件下成立。Redis 7.0 才增加第三元素；当前 Compose 固定 7.4，不是当前默认部署必现。

**代码位置 / 文档位置：**writer/consumer.py：drain_pending / XAUTOCLAIM 解包；writer/requirements.txt；Redis 版本部署文档。

**处理方式：**选择并明确支持矩阵：兼容 6.2/7+ 响应，或在启动时拒绝低于已承诺最低版本的服务端。不可因格式错误静默停止恢复；分布式 WAITAOF 最低版本要求另行遵守。

**验收要求：**两/三元素格式及客户端实际解析测试；unknown-command 回退、空 PEL、被删条目均覆盖；支持矩阵与启动自检一致。

### B31｜XACK 后 XDEL 前崩溃留下已确认 Stream 残留

**优先级 / 动作：**P1 / 修复。**执行状态：多组删除重试容量补漏已提交 `3bb2ea0c`；独立复核及 Writer 回归通过，HEAD 全量依据见执行补记。**

**来源映射：**初始报告材料摘录 L57；另一报告 #32。

**判断：**非原子窗口成立；主要是清理恢复缺口，不能描述成已落库数据丢失。

**代码位置 / 文档位置：**writer/consumer.py：_ack_retry / _delete_retry / XACK / XDEL；消费组拓扑；docs/02-模块设计/writer/可靠入库与TDengine写入.md。

**处理方式：**确认单/多消费组假设后，采用可恢复终态清理或安全组合确认/删除；多组必须保护其他组的未处理消息。不可用 XTRIM 清空所有历史来替代清理恢复。

**验收要求：**在 XACK 成功与 XDEL 执行之间杀掉测试进程，重启后可清理已终态残留；未 ACK 与其他组待处理 entry 完整保留。

### B32｜S7 热更新未复用完整地址/数据类型组合校验

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L61；另一报告 #33。

**判断：**冷热校验不一致成立；规划器仍做部分地址校验，影响范围由点和任务决定。

**代码位置 / 文档位置：**collector/main.py：配置重载；collector/device/manager.py：候选设备构造；collector/core/config_validator.py；packages/s7/s7/planner.py。

**处理方式：**把类型宽度、地址区和偏移校验放到可复用协议校验接口，冷启动与热更新走同一规则；候选无效保留完整旧代，不发布部分新配置。

**验收要求：**%MW10 + REAL 等错配冷/热均拒绝；合法 INT/REAL/BOOL 合并读正常；热更新失败不会让旧设备失效或读邻近字节。

### B33｜GE 热重载取消 await 后底层共享连接读仍在执行

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L62；另一报告 #34。

**判断：**条件性并发风险成立，尤其未配置逻辑 read_timeout 的路径；不能以 await 已取消证明线程已停。

**代码位置 / 文档位置：**collector/collector/scheduler.py：reload_device_tasks；collector/collector/executor.py：_read_blocks；packages/ge_srtp/ge_srtp/driver.py 及 transport/connection.py。

**处理方式：**对同一连接的底层 I/O 生命周期建立明确串行与退出确认；取消后保留在途状态至真实读结束，或关闭/替换连接并隔离旧代。不能只加协程锁又在取消时提前释放。

**验收要求：**慢同步读期间同连接热重载、换连接配置、无超时/有超时、停机；同一 socket 不出现两个底层读，迟到结果不覆盖新代。

### B34｜不同来源的 None 样本对应的失败与缺失语义不一致

**优先级 / 动作：**P1 / 数据语义修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L63；另一报告 #35。

**判断：**None 被跳过及部分缺失块生成 BAD 路径均存在；不应把 None 一概视为正常或一概强制历史入库。

**代码位置 / 文档位置：**collector/drivers/base.py：DriverSample/MISSING_RESULT 契约；collector/collector/executor.py：归一化与 _parse_result；backend 实时新鲜度展示。

**处理方式：**区分没有新样本、通信失败、解码失败、合法零值；明确各情形对 latest/质量/时间/历史的处理。读取失败必须可见，旧值保留时带 stale/age；保持坏值不伪造为 0。

**验收要求：**裸值 None、DriverSample(None)、缺失块、带坏质量有效值、常量未变化、设备断连与恢复；页面不把旧好值显示为当前有效采样。

### B35｜generation 的同步 RLock 包住存储写，事件循环可能争锁

**优先级 / 动作：**P1 / 修复。**执行状态：修复已提交 `84367950`；工作线程代际检查/发布、热替换和回滚复核通过，HEAD 全量依据见执行补记。**

**来源映射：**初始报告材料摘录 L64；另一报告 #36。

**判断：**当前中心默认 RedisWriter 直写，锁内可能有 Redis 网络 I/O；旧基线 Durable 的反驳不适用于当前路径。

**代码位置 / 文档位置：**collector/device/manager.py：run_if_current_generation / invalidate_device / is_current_generation；collector/collector/executor.py：_write_if_current；collector/main.py：默认 RedisWriter。

**处理方式：**缩短同步临界区并设计同设备写入/代次切换的明确串行边界，避免 event loop 同步等待慢 I/O 锁。不得简单把写移出锁导致检查通过后旧代仍提交；不得把 SQLite/Durable 接回中心。

**验收要求：**人为延迟 Redis 写并触发失效/重载：其他设备调度仍推进，旧代不能在新代生效后覆盖 latest，停机有明确界限。

### B36｜S7 健康检查/断开与 PLC 读取共用有界池

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L65；另一报告 #37。

**判断：**资源竞争风险成立；误判与泄漏依赖饱和和调用时序。

**代码位置 / 文档位置：**packages/s7/s7/driver.py：ping / disconnect；collector/collector/bounded_executor.py；collector/device/manager.py：健康/连接清理。

**处理方式：**区分执行资源拥塞与设备故障，预留或隔离生命周期清理能力；底层同 client 操作仍须串行。池满不能立即断言 PLC 故障，清理不能无期限被采集任务饿死。

**验收要求：**池满但 PLC 健康、长读中断连、重连、停机；不触发误报重连风暴，最终连接资源有可证明的释放。

### B37｜恢复监控状态未保存/校验 group_code

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L69；另一报告 #38。

**判断：**刷新恢复错误成立；运行中不是所有订阅都硬编码 g001。

**代码位置 / 文档位置：**frontend/src/views/device_monitor/realtime/index.vue：STORAGE_KEY、save/loadStateFromStorage、activeGroupCode、syncSubscription。

**处理方式：**按设备+分组完整保存恢复身份；恢复前验证分组和点位仍存在且已授权。不合法时选择明确可用组或提示，不能默默订阅 g001。与 B40 的缓存策略一致。

**验收要求：**g002/非默认分组恢复、分组被删、点位迁组、换账号、旧缓存迁移；卡片与订阅目标一致。

### B38｜监控页跳转的 deviceId query 未参与实时页初始化

**优先级 / 动作：**P1 / 修复。**执行状态：修复已提交 `aba3a10a`；路由设备优先级回归通过，HEAD 全量依据见执行补记。**

**来源映射：**初始报告材料摘录 L70；另一报告 #38。

**判断：**参数忽略成立；最终设备可能是首台或旧缓存设备。

**代码位置 / 文档位置：**frontend/src/views/device_monitor/monitor/index.vue：跳转；frontend/src/views/device_monitor/realtime/index.vue：loadDeviceTree/onMounted/selectDevice。

**处理方式：**定义优先级：显式合法路由目标优先于会话恢复，再到默认选择；加载时使用取消/代次守卫避免旧响应覆盖新目标。

**验收要求：**有效/无效/未授权 deviceId、同组件 query 变化、缓存旧设备、快速连续跳转，均定位正确或明确报错。

### B39｜异步初始化在卸载后重建 WS/定时器；KeepAlive 模式差异

**优先级 / 动作：**P1 / 修复。**执行状态：修复已提交 `aba3a10a`、`17eb2a2`；卸载/停用、等待后写入守卫及弹窗兼容回归通过，HEAD 全量依据见执行补记。**

**来源映射：**初始报告材料摘录 L71；另一报告 #39。

**判断：**异步卸载竞态成立。静态实时路由 keepAlive=true，Backend 菜单种子为 false；有效运行路由需查证。

**代码位置 / 文档位置：**frontend/src/views/device_monitor/realtime/index.vue：onMounted/onUnmounted/timer/WS；frontend/src/router/modules/device-monitor.ts；backend/app/services/auth_bootstrap.py；art-page-content。

**处理方式：**引入 disposed/初始化代次和可取消请求，创建资源前后均验证存活；清理幂等。按产品规则处理 activated/deactivated，避免静态/动态菜单产生不同后台活动策略。关闭 socket 是否需显式 unsubscribe 按服务端断连清理契约确定。

**验收要求：**每个 await 边界卸载/切路由，重复进入、KeepAlive 两种配置、登出期间加载；最终无多余连接/定时器，恢复只生成一个订阅。

### B40｜业务快照固定键持久化且登出不清导致同浏览器跨账号残留

**优先级 / 动作：**P1 / 修复。**执行状态：修复已提交 `aba3a10a`；登出清理与写回保护回归通过，HEAD 全量依据见执行补记。**

**来源映射：**初始报告材料摘录 L72；另一报告 #40。

**判断：**残留风险成立；不是远程匿名读库或后端授权绕过。

**代码位置 / 文档位置：**frontend/src/views/device_monitor/realtime/index.vue：实时快照；history/index.vue：历史缓存；frontend/src/store/modules/user.ts：logOut；路由重置。

**处理方式：**明确业务数据存储白名单；默认不长期保留敏感快照，必要缓存按用户/环境命名并设置过期、登出清理和授权检查。先终止异步写回再清理，防登出后快照重新写入。

**验收要求：**账号 A 查询后退出，B 登录、浏览器刷新、权限收回、多个标签页；B 不见 A 数据，token/密码不被持久化。

### B41｜CLI 更新 swap 前半失败时 catch 可删除仍有效的 current

**优先级 / 动作：**P0 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L73；另一报告 #41。

**判断：**具体失败序列成立；不是每次升级都删整安装根。

**代码位置 / 文档位置：**cli/update_manager.py：_SWAP_SCRIPT / _launch_swap / apply / rollback。

**处理方式：**以显式阶段状态记录完成的移动，验证新版本可用前保留至少一份完整旧版本；catch 只撤销已完成步骤，不先删仍有效 current。先对临时目录设计失败恢复，再变更生产脚本。

**验收要求：**逐个 Move-Item/校验/清理步骤注入失败、文件被占用、权限不足、崩溃后重启；始终能找到可运行版本，失败不报升级成功。

### B42｜参数错误分类和 varSeqs[] 兼容别名重复展开

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L77；另一报告 #42、#43。

**判断：**B42.1 部分裸强转会 500，非全接口；B42.2 六个唯一 varSeqs[] 会变为十二个，复核报告否认这一点不正确。

**代码位置 / 文档位置：**backend/app/main.py：CompatQueryParams / 参数绑定；backend/app/api/routes/history.py：_extract_var_seqs / raw-matrix 限额；backend/app/schemas/auth.py：数字解析。

**处理方式：**B42.1 将合法业务参数校验失败转为统一参数错误，不吞掉真实内部异常。B42.2 在兼容层或消费层选定唯一数组规范化责任，稳定去重，保留参数顺序并按规范点位数执行限额。

**验收要求：**仅 varSeqs、仅 varSeqs[]、两者混用、重复、空值、非法值、6/10/11 个唯一点均覆盖；六个唯一括号参数不得被当十二个。必须走真实兼容层而不是只 mock query_params。

### B43｜CLI dry-run 未约束更新副作用；改密 dry-run 误报成功与脱敏遗漏

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L78；另一报告 #44、#45。

**判断：**B43.1 更新副作用成立；B43.2 当前改密命令并未输出新旧密码，但脱敏集合漏字段且会返回 passwordChanged=true。

**代码位置 / 文档位置：**cli/commands/update.py；cli/update_manager.py；cli/session.py：request/_redact；cli/commands/account.py：change_password。

**处理方式：**统一全局 dry-run 对本地写与远端写的定义。更新仅输出计划，不下载落盘/解包/换目录；改密只返回 dryRun 计划而不是成功。补全 oldPassword/newPassword 等大小写规范化敏感字段，并测试所有输出路径。

**验收要求：**更新/回滚 dry-run 前后文件系统不变；改密服务请求未发送且不返回真实成功；stdout/stderr/异常/计划中不含哨兵密码。

### B44｜CLI watch 成功连接后未恢复连续重连预算

**优先级 / 动作：**P2 / 修复。**执行状态：修复已提交 `a20309c8`；连续失败预算与实时输出回归通过，HEAD 全量依据见执行补记。**

**来源映射：**初始报告材料摘录 L79；另一报告 #46。

**判断：**单次 watch 生命周期内累计成立；不是跨命令终生计数。

**代码位置 / 文档位置：**cli/realtime.py：RealtimeWatcher.events / reconnects；cli/commands/realtime.py。

**处理方式：**明确限制连续失败还是全会话次数；推荐在成功认证并稳定收流后重置连续失败预算，保留退避/抖动与退出能力，避免短暂连上即重置形成无限紧密风暴。

**验收要求：**多个相隔较久的断线、连续认证失败、网络抖动、idle timeout、用户取消；预算与文档一致。

### B45｜CLI 发布接口路径片段允许精确 ..

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L80；另一报告 #47。

**判断：**路径约束缺口成立，但读的是固定清单及其声明文件，不等于任意文件读取。

**代码位置 / 文档位置：**backend/app/api/routes/cli_releases.py：channel/platform/version 校验和路径拼接；CLI_RELEASE_DIR。

**处理方式：**拒绝 .、..、分隔符/编码绕过；规范化后验证路径仍在发布根内并处理符号链接；下载按受验证清单且单文件名限定。保留本来公开的版本查询用途，不用全局加管理员权限掩盖路径错误。

**验收要求：**临时发布目录内构造上下级同名 manifest、..、编码、symlink；不能读出根，合法发布可查询/下载且签名校验正常。

### B46｜deadline 统计语义重复；同地址合并丢失类型语义

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L81；另一报告 #48、#49。

**判断：**B46.1 特定路径可能双计，需定义事件；B46.2 同地址不同 data_type 合并存在解码风险。

**代码位置 / 文档位置：**collector/collector/scheduler.py：_run_loop/_run_device_loop；collector/collector/executor.py：execute_many 的 blocks_by_address；packages/s7/s7/planner.py 及 driver.py。

**处理方式：**分别修复：给迟到/跳过/漏周期明确计数定义；合并键包含协议读取语义，或统一原始字节读后独立解码。count 更大不证明可覆盖不同类型语义。

**验收要求：**可控时钟验证每类 deadline 计数；同地址 S7 Timer/Counter 不同解码类型、相同类型不同 count、不同周期合并均保持结果与未合并基线一致。

### B47｜Backend 直连端口绕过网关；导出路径不在挂载持久层

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L82；另一报告 #50。

**判断：**两个独立配置问题成立；直连不等于绕过 Backend 鉴权。

**代码位置 / 文档位置：**B47.1 deploy/docker-compose.yml：backend.ports；deploy/nginx.conf。B47.2 backend/app/services/history.py：get_repo_root/导出目录；task_manager.py：tasks_file；Compose volumes。

**处理方式：**B47.1 根据真实部署限制直连或确保同等认证/限流，不能切断合法开发路径。B47.2 引入统一导出根配置，让文件及 tasks 元数据都落入受保护持久目录；兼容现有任务，迁移另行授权。

**验收要求：**隔离容器替换后导出状态/文件可恢复或明确过期；文件 owner 权限不变；直连/网关功能与安全校验一致。单纯重建镜像未替换容器不能证明持久化。

### B48｜构建上下文未完整排除秘密和大体积本地产物

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L83；另一报告 #51。

**判断：**忽略规则缺口成立；deploy/backend.env 不属于 Backend 的 ../backend 构建上下文，不能断言已进 Backend 镜像。

**代码位置 / 文档位置：**.dockerignore；backend/.dockerignore；frontend/.dockerignore；backend/Dockerfile；frontend/Dockerfile；deploy/docker-compose.yml：build.context。

**处理方式：**逐构建上下文建立白名单/忽略规则，区分送入 builder、COPY 入层和运行时 env_file；阻止 backend/.env、虚拟环境、target、诊断证据等意外进入构建。检查 Dockerfile 专用 ignore 的优先级。

**验收要求：**放入非真实哨兵秘密执行隔离构建/上下文检查，最终镜像及层不含哨兵，必要源码不被误排除；不得提交真实凭据。

### B49｜前端固定锁屏加密 key 被当作安全边界

**优先级 / 动作：**P2 / 边界与实现核验。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L84；另一报告 #52。

**判断：**固定客户端 key 存在；不是 Backend 服务端密钥。

**代码位置 / 文档位置：**frontend/.env：VITE_LOCK_ENCRYPT_KEY；frontend/src/components/core/layouts/art-screen-lock/index.vue；构建 env 模式。

**处理方式：**明确锁屏只是客户端交互保护，不能宣称保护持久化秘密；敏感状态清除由真实认证策略负责。禁止仅换一个仍公开的固定 key 就声称已修安全问题。

**验收要求：**构建配置/锁屏流程不保存可恢复密码或依赖隐藏前端 key；解锁/登出行为符合认证策略，文档不夸大加密保护。

### B50｜安装器未显式落实 ProgramData ACL，服务 token 生成缺少安全随机约束

**优先级 / 动作：**P1 / 修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L85；另一报告 #53。

**判断：**明文 token 文件、未设专用 ACL、使用 Random() 属实；实际 Users 权限/可预测性未在目标机验证。

**代码位置 / 文档位置：**installer/DataWatchRole.iss：Dirs / GenerateToken / RegisterRoleService；旧 installer/DataWatch.iss；agent/app/core/deps.py；安全边界文档。

**处理方式：**采用平台安全随机源；为各角色数据根和 token 文件显式设置服务账户/管理员最小 ACL，处理继承和升级存量目录。不要打印 token 或扩大普通用户访问以方便联调。轮换流程同步 Backend/Agent。

**验收要求：**隔离 Windows 普通用户、管理员和服务账户访问矩阵；新装/升级/非默认服务账户/三角色共存；普通用户不可读改 token，服务可正常启动。


---

## 5. 文档、配置与架构契约

### D1｜质量码枚举、DriverSample 例子及文档内部矛盾

**优先级 / 动作：**P1 / 改文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L93；另一报告 #55、#56、#57。

**判断：**旧枚举和 README 示例确定过时。制作指南字段正确不能证明 README 正确；None 本身可构造，错误在 var_id/timestamp 及旧常量。

**代码位置 / 文档位置：**docs/02-模块设计/collector/README.md：§4.2，约 155–205 行；docs/01-总体设计/系统架构总览.md：§5.5；collector/drivers/base.py；三处 quality_codes.py；外置驱动包制作指南。

**处理方式：**按当前平台码及合法值范围更新枚举、导入、示例和注释，区别协议原始质量；示例只用实际支持字段。不要为兼容错误文档给 DriverSample 硬加 var_id/timestamp。说明 None 的业务语义与 B34 对齐。

**验收要求：**文档示例在项目环境可执行；旧常量不残留为当前规范；平台码与协议码明确分开。若旧常量未定义会先 NameError/ImportError，常量可解析后多余参数 TypeError，二者都记录。

### D2｜Stream v1/v2 的质量缺失和兼容规则写错

**优先级 / 动作：**P1 / 改契约与测试。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L94；另一报告 #58。

**判断：**v1 自动补 Good=0 的文档不符；v2 缺 qualities 的处置受有效存储列、键集及 DLQ 成功与否影响。

**代码位置 / 文档位置：**data_watch_shared/stream_envelope.py；writer/consumer.py：版本/values/qualities 校验；writer/tdengine_writer.py；Redis 契约及 Collector README。

**处理方式：**用精确版本矩阵描述 v1 不产生质量行、v2 对象键集约束和错误状态；不能将未知质量自动视为 Good。非法 envelope 在何层拒绝、DLQ 失败何时不 ACK 必须清楚。

**验收要求：**v1/v2、未知版本、缺字段、空列、键不一致、DLQ 不可用的示例与消费测试一致。

### D3｜quality_status 列、质量窄表和 ADR-0005 描述冲突

**优先级 / 动作：**P1 / 改文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L95；另一报告 #59。

**判断：**架构图列名错误成立；ADR 的旧语句范围不清，不能擅自解释为已明确仅否决宽表附列。

**代码位置 / 文档位置：**docs/01-总体设计/系统架构总览.md；架构决策记录.md：ADR-0005；docs/03-数据与契约/数据库/TDengine结构与字段.md；writer/quality_schema.py。

**处理方式：**明确值宽表+独立质量窄表，质量列为 quality TINYINT UNSIGNED、var_id 为复合键；以补充/替代 ADR 解释演进，保留历史并标记适用版本。

**验收要求：**所有“当前模型”图表和 DDL 一致；不把 v1 没质量等同于平台完全不存质量；历史 ADR 与现行方案的关系明确。

### D4｜values/qualities 数组描述、timestamp 示例及 DLQ 示例不一致

**优先级 / 动作：**P1 / 改契约。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L99；另一报告 #60、#61、#62。

**判断：**对象/数组冲突成立；timestamp 错例在 Collector README，Redis 主表已写 ts，不能因查到另一份正确文档而否定问题。

**代码位置 / 文档位置：**docs/03-数据与契约/数据库/Redis结构与字段.md；docs/02-模块设计/collector/README.md：Stream 示例；data_watch_shared/stream_envelope.py；writer/dlq.py。

**处理方式：**统一为 var_id 键控 JSON 对象、Stream 时间字段 ts 与毫秒单位；明确内层 JSON 字符串与 HTTP JSON 对象层级；DLQ 原帧例子必须与生产编码一致。

**验收要求：**从文档样例经真实编码/解码路径往返，点位身份、时间、质量保持一致；示例不混用秒浮点 timestamp。

### D5｜latest 的条件 epoch 字段和 Redis 控制状态清单缺项

**优先级 / 动作：**P1 / 改契约。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L100；另一报告 #63、#64。

**判断：**主清单不完整；e 是 Ingest 路径扩展，rt:change 是 Pub/Sub 通道而非持久键。

**代码位置 / 文档位置：**docs/03-数据与契约/数据库/Redis结构与字段.md；ingest/app/redis_store.py；collector/storage/redis_writer.py；backend/app/services/ws_push.py。

**处理方式：**列明中心/分布式生产者字段差异、缺 e=0 的兼容规则；补 session/high-watermark/nonce/失效通道与 TTL/所有者/清理权限。不得把控制高水位误用为历史完整性证明。

**验收要求：**主契约可覆盖实际使用键与通道，清理指引不会误删恢复状态；三字段和四字段 latest 均有合法示例。

### D6｜首次建立 Redis 消费组的 0 与 $ 不一致

**优先级 / 动作：**P1 / 脚本与文档修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L101；另一报告 #65。

**判断：**已补核：scripts/init_redis_consumer_groups.py 当前明确使用 $，Writer 使用 0；首次创建者会决定既有积压的起读位置。

**代码位置 / 文档位置：**scripts/init_redis_consumer_groups.py：main/xgroup_create(id="$"，约 108 行)；writer/consumer.py：建组；Redis结构与字段.md。

**处理方式：**统一首次建组策略，正常生产保护已存在的历史积压；将跳到最新作为需明确授权的专门操作，或弃用旧初始化脚本。已存在组不得销毁重建或随意 SETID。

**验收要求：**先有消息再建组、空 stream、已有组及 pending 三场景；合法历史不被静默跳过；重复执行不改既有游标。

### D7｜DLQ failed_at 示例为 ISO，而生产为 epoch 毫秒

**优先级 / 动作：**P2 / 改文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L102；另一报告 #66。

**判断：**示例与生产构造器不一致成立；无正式类型声明更需要补齐。

**代码位置 / 文档位置：**writer/dlq.py：payload 构造；Redis结构与字段.md；docs/02-模块设计/writer/可靠入库与TDengine写入.md。

**处理方式：**固定字段类型/单位和展示时区，更新两份样例；若必须兼容旧存量，消费者显式识别而非猜测数字是秒还是毫秒。

**验收要求：**示例解析与生产载荷一致，外部排障脚本不会出现 1000 倍时间偏差。

### D8｜Writer 三态说明遗漏隔离、退避和 FAILED_INCOMPLETE

**优先级 / 动作：**P1 / 改契约。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L106；另一报告 #67。

**判断：**文档落后成立；并非所有 RETRY 立即隔离，RETRY 不是终态。

**代码位置 / 文档位置：**writer/consumer.py：_flush/_readmit_isolated；writer/tdengine_writer.py：_merge_part_status；writer README 与可靠入库说明。

**处理方式：**按实际阶段画新状态机：普通重试、schema/size 隔离、持久失败隔离、全部值与质量分段成功才 LANDED；解释已经成功的 SQL 不会被 RETRY 自动回滚。

**验收要求：**状态文档与 B8/B28 的最终实现一致，每个状态都有 ACK、载荷归属、恢复/终结条件。

### D9｜pending drain 默认值与重试/容量配置说明漂移

**优先级 / 动作：**P1 / 配置文档治理。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L107；另一报告 #68、#69。

**判断：**60 秒与默认 5 秒冲突、多项配置缺失成立；约 10 项不是已核实精确计数。

**代码位置 / 文档位置：**writer/consumer.py：配置读取；writer/config/settings.toml.example；docs/02-模块设计/writer/README.md。

**处理方式：**列出运行期读取参数、默认值、单位、上下限、环境覆盖及弃用项；不要保留“配置存在但无作用”的 pending_max_retry 承诺。与 B8 机制变更同步。

**验收要求：**自动对比/测试配置矩阵，未知或废弃配置有明确告警；模板默认和实际默认一致。

### D10｜Writer 新 scope 自动发现及物理子表来源说明不充分

**优先级 / 动作：**P2 / 改文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L108；另一报告 #70、#71。

**判断：**自动发现机制应补写；从永久 mapping 取 child_table 不意味着最终值必然不同于 device_code。

**代码位置 / 文档位置：**writer/main.py：scope watcher/discovery；writer/mysql_mapper.py：child_table 回退；writer README、可靠入库说明。

**处理方式：**说明自动发现默认周期和重启备用路径；区分命名约定与物理映射事实源；设备/点位编辑不能随意重分配历史物理列。

**验收要求：**新增 scope 在文档规定窗口被发现；历史消息使用原永久坐标，新旧配置混合不写错列。

### D11｜TDengine TAG DDL 长度/类型及质量子表大小写说明不一致

**优先级 / 动作：**P1 / 脚本与文档治理。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L109；另一报告 #72、#73。

**判断：**三处定义差异已补核；旧初始化脚本还使用 st_*/t_* 命名，不能把它当当前 Writer 自动建表路径。

**代码位置 / 文档位置：**docs/03-数据与契约/数据库/TDengine结构与字段.md：值表 VARCHAR(64)；writer/tdengine_writer.py：VARCHAR(32)；scripts/init_tdengine_v2.py：build_stable_sql NCHAR(32)、st_*/t_*；writer/quality_schema.py。

**处理方式：**以当前模型和永久映射统一文档；旧初始化脚本标注 legacy 并加防误用守卫，或制定独立迁移。质量子表名说明保留输入大小写/规范化的实际规则；禁止直接 ALTER 现场表或改物理名追平示例。

**验收要求：**当前初始化/Writer DDL 一致；legacy 路径不会误建当前结构；长度、字符范围、大小写由隔离测试明确。

### D12｜HTTP 成功 code 和业务错误 HTTP 状态描述不一致

**优先级 / 动作：**P1 / 契约修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L113；另一报告 #74、#75。

**判断：**成功包络实际 code=200；ApiError 默认 HTTP 200，但显式错误等路径不是一律 200。

**代码位置 / 文档位置：**backend/app/core/responses.py；backend/app/core/exceptions.py；backend/app/main.py：响应分发；docs/03-数据与契约/契约/API契约.md；各客户端响应判断。

**处理方式：**先发布真实接口矩阵，再决定是否迁移到标准 HTTP 语义；改响应必须同时更新 Web/App/CLI 与兼容测试，不能仅改公共异常默认值导致所有客户端失效。业务码与 HTTP 码分别列明。

**验收要求：**成功、认证、权限、参数、404、503/504、下载在各客户端一致处理；错误不伪装空列表，旧契约迁移有版本/兼容说明。

### D13｜分页字段及用户/角色/菜单 API 方法路径混写

**优先级 / 动作：**P1 / 改契约。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L114；另一报告 #76。

**判断：**系统列表确用 records/current；其他域可使用 list/pageNo。资源根 POST 仍有效，不可误删。

**代码位置 / 文档位置：**backend/app/api/routes/system_manage.py；backend/app/services/system_manage.py；API契约.md；frontend/src/api 及 cli 对应客户端。

**处理方式：**按方法+完整路径+请求/响应逐接口列：GET /api/user/list、/api/role/list，POST 资源根，GET 复数 menus 等。将分页统一与现状描述分开，不为文档一致一次重写所有 API。

**验收要求：**从接口契约样例执行各列表/创建/菜单调用；分页兼容别名明确，不能把 records/list 接错造成页面无数据。

### D14｜历史主查询被描述为 POST JSON，与 GET/query/header 不符

**优先级 / 动作：**P1 / 改契约。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L115；另一报告 #77。

**判断：**主查询时序图错误成立；导出创建是另一条 POST API，不能一概改 GET。

**代码位置 / 文档位置：**backend/app/api/routes/history.py：history/query、export、chart/raw/matrix；frontend/src/api/device-monitor.ts；WebSocket协议.md 及 API契约.md。

**处理方式：**按端点描述方法、设备/点位参数、时间范围、X-Perspective-* 请求头；同步图和可执行样例。

**验收要求：**文档样例能查询；数组参数经过 B42 修复后的真实兼容层；导出创建与主查询不再混淆。

### D15｜WS 触发机制、条件字段、缺参错误及 pong 说明不完整

**优先级 / 动作：**P1 / 契约与局部行为修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L116；另一报告 #78、#79、#80、#81。

**判断：**事件驱动与兜底说明需更新；文本 pong 已有兼容说明，不作为必须修复的运行 Bug。

**代码位置 / 文档位置：**backend/app/services/ws_push.py；backend/app/api/routes/monitor.py；docs/03-数据与契约/契约/WebSocket协议.md；Backend 设计文档。

**处理方式：**D15.1 说明 rt:change 唤醒、可配置最小推送间隔和默认全量兜底；D15.2 列明 point_ids/supports_freshness 及条件返回字段；D15.3 对缺参数决定明确错误并保持兼容；D15.4 固化实际 pong 格式和兼容规则，不为格式统一破坏客户端。

**验收要求：**事件到达/无事件兜底、能力协商有无、缺 device/group、非法目标、文本/JSON pong 均有契约测试。

### D16｜MySQL “当前 26 表”快照缺少新列与分布式结构

**优先级 / 动作：**P1 / 改文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L117；另一报告 #82。

**判断：**文档落后成立；token_version 是列，不应被统计成新表。

**代码位置 / 文档位置：**docs/03-数据与契约/数据库/MySQL结构与字段.md；backend/app/models/auth.py；auth_bootstrap.py；ingest/app/migrations.py；scripts/sql/db6_distributed_core.sql。

**处理方式：**按指定 schema 版本更新表/列/迁移清单，说明哪些角色执行哪些迁移。删除没有时间戳的“当前 live”宣称；不得根据过时文档删除字段或未经授权修改现场库。

**验收要求：**在隔离 schema 对比声明与模型/迁移，给出快照日期和覆盖范围；启动跳过迁移的前提明确，不编造现场总表数。

### D17｜sent 记录 60 秒 fallback 与 Uploader 24 小时默认混淆

**优先级 / 动作：**P1 / 配置文档治理。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L121；另一报告 #83。

**判断：**文档冲突成立；两者对应调用层级不同，并非都代表生产入口默认。

**代码位置 / 文档位置：**edge/app/uploader.py：UploaderSettings / maintenance 调用；edge/app/outbox.py：maintenance 默认；边缘运行时说明、DW-01。

**处理方式：**记录有效生产入口默认和直接维护 API 的 fallback；确认是否应统一默认，明确配置传播。sent 留存不是自动对账或完整持久性保证。

**验收要求：**经 Uploader 与直接 IPC 两条路径读取有效值；sent/dead/pending 清理规则分别测试，文档不混写。

### D18｜磁盘 95% 时旧文档要求丢积压，与当前保护策略冲突

**优先级 / 动作：**P1 / 改契约。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L122；另一报告 #84。

**判断：**应更新旧文档。当前保护未确认行，不代表所有终态记录永不清理。

**代码位置 / 文档位置：**edge/app/outbox.py：maintenance/磁盘保护；docs/02-模块设计/edge/边缘运行时实现说明.md；分布式架构材料。

**处理方式：**写清终态清理、未确认载荷保全、拒绝新写入、告警和人工恢复边界；不能恢复清空未上传数据的旧策略，不能宣称无限断网无损。

**验收要求：**隔离磁盘水位触发后不删除 pending/retry/inflight；终态按策略清理；新样本拒收显式计数，资源恢复后继续上传。

### D19｜session 切换“清空重来”与当前迁移未确认行不一致

**优先级 / 动作：**P1 / 改契约。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L123；另一报告 #85。

**判断：**旧文档应替代；保留未确认数据不是应删除的 Bug，B5 才是已确认结果处理缺口。

**代码位置 / 文档位置：**edge/app/outbox.py：prepare/complete_session_switch；edge/app/uploader.py；分布式部署架构方案.md 与分布式系统技术架构.md。

**处理方式：**按 B5 最终状态机更新 session 恢复文档，区分确定 ACK、未知、未发送及永久非法项；任何放弃业务数据的操作必须单独授权并记录。

**验收要求：**状态图、字段身份、崩溃恢复和实际测试一致；不把迁移后的 local_seq 当原始全局唯一帧 ID。

### D20｜文档描述主动 429 限流，而 Ingest 本身未接通

**优先级 / 动作：**P2 / 能力补齐或文档修正。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L124；另一报告 #86。

**判断：**仓库内服务/网关缺少相应主动限流；Uploader 能处理 429 不等于服务端已实现。

**代码位置 / 文档位置：**ingest/app/main.py；deploy/nginx.conf；edge/app/uploader.py；分布式技术架构及 Ingest 说明。

**处理方式：**确定限流实施位置和容量预算；实现时返回明确 429/重试语义并保护不同节点公平性，或如实标注外部网关职责/未实现能力。不能把请求体大小限制当速率限流。

**验收要求：**隔离压测达到限额时行为可预测、合法数据保留重试、无忙循环；指标能区分主动限流与故障 503。

### D21｜Ingest Docker 启动绕过 worker 公式，health 展示的是配置值

**优先级 / 动作：**P2 / 启动与文档修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L125；另一报告 #87。

**判断：**接线差异成立；Uvicorn 也可受 WEB_CONCURRENCY 等控制，不能称永远单 worker。

**代码位置 / 文档位置：**ingest/Dockerfile；ingest/main.py：__main__/uvicorn.run；ingest/app/config.py；ingest/app/main.py：healthz。

**处理方式：**统一受支持启动入口及 worker 配置优先级；分别展示 configured/observed workers，避免配置值冒充实测。处理多 worker schema 初始化门禁；不要把进程数量配置等同于已通过容量验收。

**验收要求：**Docker/脚本/Windows 打包入口分别验证实际进程数、schema 只执行受控初始化、健康报告与实测一致。

### D22｜Collector 文档旧内置驱动路径和硬编码行号失效

**优先级 / 动作：**P2 / 改文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L129；另一报告 #88。

**判断：**实际有多处漂移；未以精确盘点证明“15+”数量。

**代码位置 / 文档位置：**docs/02-模块设计/collector/README.md；collector/README.md；collector/drivers/registry.py；packages/*；驱动制作指南。

**处理方式：**更新到驱动包+registry 的实际发现/安装/运行路径，长期定位用文件+类/函数；旧目录仅作为历史说明并标记。

**验收要求：**文档引用文件存在，符号可定位，新增协议流程能按受控包机制完成；不误称需要改核心调度。

### D23｜调度文档仍用每任务循环解释当前每设备统一循环

**优先级 / 动作：**P2 / 改文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L130；另一报告 #89。

**判断：**当前运行模型应更新；仅同时到期且支持 execute_many 才会合并。

**代码位置 / 文档位置：**collector/collector/scheduler.py：start/_run_device_loop；collector/collector/executor.py：execute_many；Collector README。

**处理方式：**说明设备独立循环、不同 scan_rate、同时到期合并、错峰/背压/取消/迟到处理。区分单 OS 进程与任务内部线程池，不编造自动多核收益。

**验收要求：**图和时序可对应真实执行路径；同设备单在途与分组数据身份不变。

### D24｜熔断器说明未区分初次连接、重连和错误类型

**优先级 / 动作：**P2 / 文档与异常契约修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L131；另一报告 #90。

**判断：**指定重连/OPC DA 路径偏差成立；“整个熔断器完全不工作”错误。

**代码位置 / 文档位置：**collector/device/manager.py：use_circuit_breaker；collector/core/retry.py；packages/opcda_center/opcda_center/driver.py；Collector README。

**处理方式：**列明启用熔断的阶段、计数异常类型与恢复规则；决定是否修正 OPC DA 异常归一化，不能直接把所有重连接入熔断而造成设备无法恢复。

**验收要求：**初连连续失败、重连退避、OPC DA/其他驱动错误分别测试；文档阈值与适用路径一致。

### D25｜OPC 代理端口动态分配和权威 mapping 两种模式混写

**优先级 / 动作：**P2 / 改文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L132；另一报告 #91。

**判断：**两种模式均存在，不是只能保留一种。

**代码位置 / 文档位置：**collector/device/manager.py：EndpointPortMapper；agent/app/services/sidecar_manager.py；Collector 与 Agent 设计文档。

**处理方式：**说明没有显式文件时动态分配，传入权威 mapping 时只读并 fail-fast；生产由谁生成/保管文件、端口归属校验也要列明。

**验收要求：**动态模式、有效文件、缺项/损坏文件的结果与文档一致；不得因缺文件静默漂移到其他端口。

### D26｜实时页未使用文档所写 composable；缓存路由生命周期差异

**优先级 / 动作：**P2 / 文档与前端治理。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L136；另一报告 #92。

**判断：**实现与文档有偏差；静态 keepAlive=true 与后端种子 false 必须一起核对，不能任取其一推断现场。

**代码位置 / 文档位置：**frontend/src/composables/useRealtimeWs.ts；frontend/src/views/device_monitor/realtime/index.vue；frontend/src/router/modules/device-monitor.ts；backend/app/services/auth_bootstrap.py；前端设计文档。

**处理方式：**确定活动实现后统一连接/退避/可见性/关闭规则并修文档；B39 负责资源生命周期，不要求为遵循旧图强行全量重写页面。

**验收要求：**实际生产菜单/路由模式和组件连接行为可复验；未使用 helper 不再被写作活动链路。

### D27｜浏览器持久化白名单和实际业务/偏好缓存不一致

**优先级 / 动作：**P1 / 安全文档与测试。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L137；另一报告 #93。

**判断：**白名单不完整；remember-pwd 是偏好键，不能仅凭名字认定保存密码。

**代码位置 / 文档位置：**PRODUCT.md；docs/02-模块设计/frontend/页面权限与实时历史设计.md；frontend/src/utils/storage；登录页、实时/历史页、user store。

**处理方式：**产出键级清单：内容、敏感等级、owner、TTL、登出清理、兼容迁移。token/密码与偏好分开；业务快照按 B40 修复，统一 PRODUCT 和模块文档。

**验收要求：**自动检查关键持久化键与实际内容；登录退出/跨账号/旧版本缓存迁移都符合白名单。

### D28｜Landing 实际菜单位置可能与 PRODUCT 不同

**优先级 / 动作：**P3 / 待核验。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L138；另一报告 #94。

**判断：**仍需有效运行菜单证据，不能仅据静态注册下结论。

**代码位置 / 文档位置：**PRODUCT.md；frontend/src/store/modules/menu.ts；router/routes/asyncRoutes.ts；后端菜单接口与现场生效菜单。

**处理方式：**先只读采集实际菜单及路由来源，明确期望位置；不新增重排菜单或重置数据库种子来强行满足未证实的问题。若当前符合则标记无需修改。

**验收要求：**以可复验菜单 JSON/截图及角色权限确认，注明是否仅文档文字过时。

### D29｜README 同时展示 Apache 许可又称没有 LICENSE

**优先级 / 动作：**P3 / 改文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L142；另一报告 #95。

**判断：**确定的文档自相矛盾。

**代码位置 / 文档位置：**README.md：许可标识与当前边界；LICENSE。

**处理方式：**依维护者已采用的实际许可证修正描述，不能擅自替换许可证或扩大授权。

**验收要求：**根 README、许可证和分发说明相符，不残留无许可证的旧声明。

### D30｜5 协议叙事与五个驱动包、HTTP Push 接入方式混数

**优先级 / 动作：**P2 / 产品文档治理。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L143；另一报告 #96。

**判断：**清单需更新；Modbus 包存在不等于 B4 等全链路已验收，不能直接宣称六种完整产品协议。

**代码位置 / 文档位置：**PRODUCT.md；README.md；packages/driver-catalog/catalog.json；packages/modbus_tcp；HTTP Push/OPC DA sidecar 文档。

**处理方式：**按协议族、驱动包、别名、接入方式分层列清单，标明已实现/已集成/目标机已验收；产品介绍只使用有对应证据的能力。

**验收要求：**每个接入项都有真实入口和验收范围；不把同协议多别名重复计数，修复 B4 后再更新可用状态。

### D31｜PRODUCT 构建入口错误且仍描述单安装包

**优先级 / 动作：**P2 / 改文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L144；另一报告 #97。

**判断：**确定过时。

**代码位置 / 文档位置：**PRODUCT.md；installer/build.ps1；installer/build-roles.ps1；installer/roles.json；三角色 .iss 包装。

**处理方式：**更新实际构建入口及 centralized/edge/distributed-center 三类产物；区分独立 Docker/CLI 发布，不把镜像塞回角色包。

**验收要求：**所有命令路径存在，构建说明和产物命名可由脚本推导，角色组件清单一致。

### D32｜Compose 的 MySQL 服务清单与离线镜像目录说明存在版本差异

**优先级 / 动作：**P2 / 版本与文档治理。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L145；另一报告 #98。

**判断：**6b3630c 无 MySQL 服务；当前 6845e80 明确有 infra MySQL。复核 Agent 的旧版反驳不能关闭当前问题。

**代码位置 / 文档位置：**deploy/docker-compose.yml；deploy/stack-up.ps1；docs/01-总体设计/系统架构总览.md：部署章；docs/04-运维部署/README.md。

**处理方式：**按当前版本统一可选 infra 三库与应用服务清单；明确发布根 docker-images 与仓库目录的区别。目录可由发布流程生成，不以 Git 中不存在就判构建失败。

**验收要求：**仅只读 compose config/隔离发布演练验证清单与加载路径；不碰业务容器和命名卷。

### D33｜外置驱动安全文档否认已有的包签名校验

**优先级 / 动作：**P1 / 改安全文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L146；另一报告 #99。

**判断：**文档过时成立，6b3630c 与当前均有包签名验证调用。不能拿上传请求 Ed25519 或 Authenticode 来替代核查对象。

**代码位置 / 文档位置：**agent/app/services/driver_zip.py：validate_and_extract→data_watch_driver_signatures.verify_package_signature；docs/04-运维部署/安全边界.md：外置驱动 ZIP 条目。

**处理方式：**根据真实实现写清驱动包签名/目录身份/可信发布者与可选或强制规则；把驱动 ZIP 签名、网络请求签名、Windows EXE 代码签名分开。不要宣称所有包必需签名，除非代码门禁确已如此。

**验收要求：**合法受信包、被篡改包、未知发布者及未签名包按实际策略测试；文档不再笼统称不验签。

### D34｜手动生成 Agent token 指引未优先说明安装器已有值

**优先级 / 动作：**P2 / 改运维文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L147；另一报告 #100。

**判断：**流程说明不足；只有两端不一致才调用失败，并非手工生成本身错误。

**代码位置 / 文档位置：**installer/DataWatchRole.iss：token 文件/服务环境；Backend Agent 连接配置；docs/04-运维部署/README.md。

**处理方式：**指导安全获取/配置已有 token，提供协调轮换与失败回滚步骤；与 B13/B50 保持最小传播和 ACL。不在 MD、日志或提交中输出真实 token。

**验收要求：**新装/已有配置/轮换失败后恢复场景，两端凭据一致且旧凭据撤销可验证。

### D35｜开发脚本优先查作者个人绝对解释器路径

**优先级 / 动作：**P2 / 脚本与文档修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L148；另一报告 #54、#101。

**判断：**确定的开发可移植性问题；另一报告 #54 与 #101 是同一项，不能重复计为两个 Bug。

**代码位置 / 文档位置：**start-dev.ps1：Python candidates / PYTHONPATH；README 开发启动说明。

**处理方式：**显式传入或仓库虚拟环境优先，验证 3.14t/ABI 与依赖路径；移除作者个人路径作为默认权威。普通 CPython 回退须显式声明，不静默混装 site-packages。

**验收要求：**非作者用户名、无个人目录、有指定解释器/仓库 venv 等场景正确选择环境并给清晰错误。

### D36｜Agent 编排文档的路径、Writer 参数与 Redis DB 注入不准确

**优先级 / 动作：**P2 / 文档与配置接线。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L152；另一报告 #102、#103、#104、#105。

**判断：**多个子项成立，需逐项核对不能一改标题就关闭。

**代码位置 / 文档位置：**D36.1 process_manager.py / managed_process_runner.py：runtime/pids/state；D36.2 sidecar_manager.py：proxy 路径；D36.3 admin.py / factory.py：extra_args/静态 args；D36.4 admin.py：_ENV_CONN_FIELDS/_db_env；编排文档。

**处理方式：**以实际路径和调用链重写。Writer 静态 args 与 Agent 动态附加参数分开；DATAWATCH_REDIS_DB 若产品需支持非 0 库应从配置 schema 到客户完整接通，否则不要在文档承诺已下发。

**验收要求：**四个子项分别有断言：PID/flag 路径同源、proxy 找得到、Writer 有效 args 明确、DB index 在支持模式端到端一致。

### D37｜文档否认子进程求和，监控实现却统计进程树

**优先级 / 动作：**P2 / 改指标文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L153；另一报告 #106。

**判断：**明确不一致。

**代码位置 / 文档位置：**agent/app/services/process_manager.py：_process_tree/_sample_cpu/_info_running；docs/02-模块设计/agent/边缘进程编排与凭据.md。

**处理方式：**写明按实际后代统计 CPU/RSS、CPU 按逻辑核归一；单独管理的 sidecar 不自动属于某受管进程树。区分 RSS 求和与去重后物理内存，避免夸大精度。

**验收要求：**父+子进程样例与指标定义一致；PID 复用、子进程退出不累积错误缓存，文档单位明确。

### D38｜通用 NSSM 安装脚本默认中心角色，与固定角色包混淆

**优先级 / 动作：**P2 / 脚本与文档修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L154；另一报告 #107。

**判断：**缺角色变量时会回退 centralized；--name 可改服务名，正式角色包会显式注入 role。

**代码位置 / 文档位置：**agent/service/install_service.py；agent/app/core/role.py；installer/DataWatchRole.iss 及角色包装；三角色部署文档。

**处理方式：**区分开发辅助安装与正式角色安装；通用脚本显式接受/验证角色或警告不支持边缘，禁止依缺省值误部署。明确包内容固定不等于 role 是不可变安全边界。

**验收要求：**三角色安装环境、服务名/端口/数据根隔离、缺失/非法 role；不误启错误角色，不未经授权改已有服务。

### D39｜DW-01 仍把已撤销的中心 SQLite+WAITAOF 写成当前实现

**优先级 / 动作：**P1 / 优先改架构文档。**执行状态：待执行 HEAD 核验。**

**来源映射：**本轮综合额外补充。

**判断：**本轮综合额外保留的重要漂移。当前中心 RedisWriter 直写；分布式 Ingest 仍有同连接 Lua+WAITAOF，不能一并删除。

**代码位置 / 文档位置：**docs/03-数据与契约/DW-01-确认语义与持久化边界.md；collector/main.py：默认 writer 分支；edge/collector_main.py；ingest/app/redis_store.py：process_batch；边缘/架构/ACK 文档。

**处理方式：**拆分中心与分布式确认矩阵：采样完成、Redis 接受、AOF 持久确认、Writer 完整落库分别定义。标记旧中心 Durable 方案 Superseded，保持边缘 SQLite→Uploader→Ingest→Redis。核对其他文档仍称 Ingest 仅内存 ACK 的冲突。不得按旧文档把中心再接回 SQLite 或移除边缘现行 AOF 门禁。

**验收要求：**文档每种部署形态能映射实际生产入口；中心不构造 Durable/Outbox，边缘仍使用 EdgeOutboxWriter；AOF 超时、确认未知等语义准确，旧性能报告注明适用路径。


---

## 6. 疑点与条件验证

### S1｜TDengine 字符串反斜线转义是否影响合法 tag 值

**优先级 / 动作：**P2 / 验证优先。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L160；另一报告 #108。

**判断：**只处理单引号是代码事实；目标版本解析及实际损害仍需验证，未确认注入或整设备 DLQ。

**代码位置 / 文档位置：**writer/tdengine_writer.py：tag 字面量构造/_quality_fragment_for_rows；设备编码校验与永久映射。

**处理方式：**先查目标 TDengine/驱动版本语法与参数绑定能力，在隔离库测试可合法进入字段的特殊字符；如有缺陷统一安全编码。不得批量改设备编码或测试现场生产库。

**验收要求：**单引号/反斜线/组合字符、合法 Unicode、非法标识符各有输入范围及结果证据；只对已确认路径修复。

### S2｜TDengine 错误子串启发式可能误分连接/数据错误

**优先级 / 动作：**P2 / 验证优先。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L161；另一报告 #109。

**判断：**子串分类事实成立；具体真实错误误判未证实。

**代码位置 / 文档位置：**writer/tdengine_writer.py：_is_connection_error/_is_data_error；实际 taosws 异常对象和错误码。

**处理方式：**建立真实错误样本矩阵，优先稳定错误码/类型，文本作兼容兜底。未知结果仍可恢复，不为减少重试把未知错误一律 BAD_DATA。与 B8/D8 共用最终策略。

**验收要求：**连接断开、schema、数据越界、SQL 超长、未知和含关键字但不同类型的异常；分类不导致未确认数据被终结。

### S3｜Edge Storage 本机 HTTP IPC 未认证即可改变 Outbox/配置状态

**优先级 / 动作：**P1 / 本机安全修复。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L162；另一报告 #110。

**判断：**服务端缺少认证已由代码确认；默认回环监听不是用户/进程身份认证，实际可达性取决于主机策略。

**代码位置 / 文档位置：**edge/storage_main.py：绑定地址；edge/app/storage_service.py：/v1/command 与 outbox/session/config 分派；edge/app/storage_client.py；Agent 的 Storage 客户端。

**处理方式：**明确本机信任边界；为 IPC 引入专用最小权限身份或受 ACL 保护的本机传输，限制客户端可执行命令并保护凭据。不要复用拥有广泛管理权的 Agent service token，也不要将 Storage 开到全部网卡。

**验收要求：**普通本地账户/无身份进程无法更改 outbox/session/config；合法 Collector/Uploader/Agent 权限分别测试；未确认数据不会被未授权 complete。

### S4｜无时区历史字符串在不同时区浏览器解释不同

**优先级 / 动作：**P2 / 时间契约验证。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L163；另一报告 #111。

**判断：**跨时区风险有条件成立；旧 Safari 的具体行为未做版本实测。

**代码位置 / 文档位置：**backend/app/core/time_utils.py；backend/app/services/history.py：时间解析/格式化；frontend/src/views/device_monitor/history/index.vue 与 perspective-runtime.ts。

**处理方式：**建立线缆时间格式：epoch 毫秒或明确时区的 ISO；查询输入、返回值和显示层统一。迁移保留既有 Asia/Shanghai 输入语义，不能静默把历史时间整体偏移。

**验收要求：**Asia/Shanghai、America/Los_Angeles、UTC 及目标浏览器；同一瞬间查询/导出/图表一致；Safari 结论注明实测版本。

### S5｜Backend healthz 不检查三库是否可读

**优先级 / 动作：**P3 / 探针用途核验。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L164；另一报告 #112。

**判断：**行为已确认；当前运维文档已披露，作为 liveness 本身不构成缺陷。

**代码位置 / 文档位置：**backend/app/api/routes/system.py：healthz；docs/04-运维部署/README.md：健康检查说明；实际探针配置。

**处理方式：**检查是否被当成依赖就绪探针；需要时增加独立、有时间预算的 readiness，而非把慢重查询塞入每次 liveness。若未误用，仅完善命名/说明，不强改生产行为。

**验收要求：**依赖故障时存活与就绪信号按定义区分；探针不会放大数据库故障，不能用 healthz=200 冒充全链路验收。

### S6｜镜像缺 perspective-python 而开关默认 true

**优先级 / 动作：**P3 / 能力验证。**执行状态：待执行 HEAD 核验。**

**来源映射：**初始报告材料摘录 L165；另一报告 #113。

**判断：**依赖和配置现状成立；未发现必须调用该 Python 包的活动路径，不能直接断言功能故障。

**代码位置 / 文档位置：**backend/Dockerfile；backend/app/services/perspective.py：runtime_available；backend/app/core/config.py；前端 JS perspective-runtime.ts。

**处理方式：**先追实际 Python 运行时调用、JSON 快照管理与前端 JS runtime 三者关系；不盲装依赖破坏 3.14t ABI，也不无依据关闭可用功能。需要时改开关命名/降级说明。

**验收要求：**目标镜像下普通历史、JS 图表、可选 WS 快照分别验收；明确哪些能力真正依赖 Python 包，并保留 GIL/ABI 门禁。


---

## 7. 本轮争议核对的最小证据

### E1. 版本变化不是同基线反证

`6b3630c` 提交时间为 2026-09-27 05:46:59Z。该版本 `deploy/docker-compose.yml` 没有 MySQL 服务，中心采集曾使用 Durable 路径；当前 `6845e80` 的 Compose 已有 `mysql` 服务及 `infra` profile，中心入口默认 `RedisWriter`。旧版结论应保留为历史事实，不能代替当前审查。

当前分布式 `ingest/app/redis_store.py` 的批次提交仍在同一 pipeline 连接执行 Lua 后 `WAITAOF 1 0 2000`。因此修 DW-01 必须拆分中心和分布式，不能全文替换成“平台不使用 WAITAOF”。

### E2. 六个括号参数变十二个：已做源码片段逻辑复现

核对源：`backend/app/main.py::CompatQueryParams` 与 `backend/app/api/routes/history.py::_extract_var_seqs`。旧基线与当前可见代码均存在这两段行为：

```python
# 兼容层同时保存原键及不带 [] 的别名
self._values.setdefault(key, []).append(value)
if key.endswith("[]"):
    self._values.setdefault(key[:-2], []).append(value)

# 路由随后再拼接两套值
raw_values = [*query_params.getlist("varSeqs"),
              *query_params.getlist("varSeqs[]")]
```

本轮在隔离 Python 3.13.5 中复制这段纯逻辑，输入仅为六个唯一 `varSeqs[]`，结果为：

```text
input = [('varSeqs[]', '1'), ..., ('varSeqs[]', '6')]
raw_values = ['1','2','3','4','5','6','1','2','3','4','5','6']
len(raw_values) = 12
```

这是片段逻辑验证，不是目标 3.14t 下的 HTTP 集成测试。整改验收仍必须经过实际参数兼容层与真实路由。

### E3. DriverSample：错的是示例字段，不是 None 本身

旧基线 Collector README 与当前相同 blob 的示例包含：

```python
DriverSample(var_id=123, value=None, timestamp=time.time(), quality=Q_BAD_TIMEOUT)
```

实际 dataclass 字段只有 `value`、`quality`、`source_timestamp`、`protocol_quality`。本轮纯逻辑复现结果：

```text
DriverSample(value=None, quality=5) -> 可构造
DriverSample(var_id=123, value=None, timestamp=0, quality=5)
-> TypeError: DriverSample.__init__() got an unexpected keyword argument 'var_id'
```

测试把质量常量替换成已定义值以单独验证参数绑定；原样使用未定义旧常量还可能先发生 NameError/ImportError。不能写成“None 使 dataclass 构造失败”，也不能据制作指南中的正确示例关闭 README 中的错误。

同一个 README 的 Stream 示例仍使用 `timestamp` 秒浮点字段；Redis 结构主表已写 `ts` 不足以否认这处问题。

### E4. 驱动 ZIP 签名不是网络请求签名

`docs/04-运维部署/安全边界.md` 的“外置驱动安全边界”明确讨论 ZIP 包安装，写“但不验证代码签名”。`agent/app/services/driver_zip.py::validate_and_extract` 已调用：

```python
from data_watch_driver_signatures import verify_package_signature
signature_verified = verify_package_signature(package_root, parsed, catalog)
```

这段调用在 `6b3630c` 和 `6845e80` 均存在。它证明有驱动包签名校验能力；是否所有类别都强制验签需看完整策略。它既不是 Uploader 请求签名，也不证明 Windows EXE 有 Authenticode 签名。

### E5. 其他新增定位

`init_redis_consumer_groups.py::main` 的确使用 `xgroup_create(..., id="$", ...)`。`init_tdengine_v2.py::build_stable_sql` 使用 `NCHAR(32)` 且是 `st_*` / `t_*` 命名的旧初始化路径。两者都需要治理，但不得与当前 Writer 的有效结构混为同一条已执行路径。

实时页静态路由 `keepAlive=true`，Backend 菜单种子 `keep_alive=False`；实际模式与存量数据库菜单需要核验。因此 B39 的异步资源竞态可以确认，KeepAlive 对所有部署的影响不能一概而论。

### E6. 外部依赖语义依据

以下只用于语义边界，不代表已在项目目标环境运行验证：

- Redis 官方 `XAUTOCLAIM` 文档的 History 明确第三个响应元素自 7.0.0 增加，6.2 支持该命令不等于具有三元素响应。参考地址：`https://redis.io/docs/latest/commands/xautoclaim/`。
- Click 官方异常/退出码说明：`standalone_mode=False` 下显式退出可作为数值返回；生产入口必须正确传播。项目验收使用自身锁定的 Typer/Click 版本。参考地址：`https://click.palletsprojects.com/en/stable/exceptions/`。
- Uvicorn 官方 settings：worker 默认取 `WEB_CONCURRENCY`，未设置时为 1。因此 Dockerfile 未给 `--workers` 不等于所有环境永远单 worker。参考地址：`https://www.uvicorn.org/settings/`。

---

## 8. Agent 交付与销项格式

每个最小改动批次交付以下字段。不得只写“已修/测试通过”。文档条目也需要完成证据，例如示例可执行、字段与实现对应、旧决策已标替代。

```markdown
### Bxx / Dxx / Sxx 执行记录
- 执行起始 HEAD / 结束 HEAD：
- 原判断在该 HEAD 是否仍成立：
- 原代码定位（文件、函数、实际行号）：
- 复现或反证（命令、依赖版本、输入、实际输出）：
- 最小修改（文件与行为差异）：
- 回归范围和结果（失败/跳过如实列出）：
- 数据、权限、部署兼容影响：
- 文档同步位置：
- 回滚方式与数据迁移要求：
- 尚未覆盖的目标环境/故障场景：
- 最终状态：已修并验证 / 已存在修复 / 不成立有反证 /
  仅文档已修 / 待目标环境验证 / 需架构决策
```

所有时序/吞吐结论必须使用真实目标路径，注明实际输入负载是否达标。调用旧 `DurableRedisWriter` 的诊断脚本不得冒充当前中心式性能。应用返回、Redis 接受、AOF 持久确认、Writer 完整逻辑帧确认、浏览器显示时间不能混为同一指标。

上游停机、结果未知、重放、乱序、缓存版本竞态和 SQL 部分成功等测试，必须检查数据守恒与身份，不只看退出码或最终 backlog=0。依赖错误被吞成空数据、数据被删除导致 backlog 归零，都不算成功。

---

## 附录 A. 另一 Agent 113 条到稳定编号的完整映射

该表为覆盖核对，不增加缺陷数量。同一主编号可能包含多条子断言；执行时按其“处理方式/验收要求”全部完成。

| 另一报告编号 | 本文稳定编号 |
|---|---|
| #1 | B1 |
| #2 | B2 |
| #3 | B3 |
| #4 | B4 |
| #5 | B5 |
| #6 | B6 |
| #7 | B7 |
| #8 | B8 |
| #9 | B9 |
| #10 | B10 |
| #11 | B11 |
| #12 | B12 |
| #13 | B13 |
| #14 | B14 |
| #15 | B15 |
| #16 | B16 |
| #17 | B16 |
| #18 | B17 |
| #19 | B18 |
| #20 | B19 |
| #21 | B20 |
| #22 | B21 |
| #23 | B22 |
| #24 | B23 |
| #25 | B24 |
| #26 | B25 |
| #27 | B26 |
| #28 | B27 |
| #29 | B28 |
| #30 | B29 |
| #31 | B30 |
| #32 | B31 |
| #33 | B32 |
| #34 | B33 |
| #35 | B34 |
| #36 | B35 |
| #37 | B36 |
| #38 | B37、B38 |
| #39 | B39 |
| #40 | B40 |
| #41 | B41 |
| #42 | B42 |
| #43 | B42 |
| #44 | B43 |
| #45 | B43 |
| #46 | B44 |
| #47 | B45 |
| #48 | B46 |
| #49 | B46 |
| #50 | B47 |
| #51 | B48 |
| #52 | B49 |
| #53 | B50 |
| #54 | D35 |
| #55 | D1 |
| #56 | D1 |
| #57 | D1 |
| #58 | D2 |
| #59 | D3 |
| #60 | D4 |
| #61 | D4 |
| #62 | D4 |
| #63 | D5 |
| #64 | D5 |
| #65 | D6 |
| #66 | D7 |
| #67 | D8 |
| #68 | D9 |
| #69 | D9 |
| #70 | D10 |
| #71 | D10 |
| #72 | D11 |
| #73 | D11 |
| #74 | D12 |
| #75 | D12 |
| #76 | D13 |
| #77 | D14 |
| #78 | D15 |
| #79 | D15 |
| #80 | D15 |
| #81 | D15 |
| #82 | D16 |
| #83 | D17 |
| #84 | D18 |
| #85 | D19 |
| #86 | D20 |
| #87 | D21 |
| #88 | D22 |
| #89 | D23 |
| #90 | D24 |
| #91 | D25 |
| #92 | D26 |
| #93 | D27 |
| #94 | D28 |
| #95 | D29 |
| #96 | D30 |
| #97 | D31 |
| #98 | D32 |
| #99 | D33 |
| #100 | D34 |
| #101 | D35 |
| #102 | D36 |
| #103 | D36 |
| #104 | D36 |
| #105 | D36 |
| #106 | D37 |
| #107 | D38 |
| #108 | S1 |
| #109 | S2 |
| #110 | S3 |
| #111 | S4 |
| #112 | S5 |
| #113 | S6 |

D39 是额外补充，另一报告没有对应独立条目。另一报告 #54 与 #101 均映射 D35；#38 同时覆盖 B37/B38。以上映射没有遗漏 #1–#113。

## 附录 B. 材料与基线记录

**R0：**用户提供的《项目审查报告（对照文档）》，对话中完整材料 165 行；本文“初始报告材料摘录 Lx”使用该对话材料行号，不冒充仓库代码行号。

**R1：**用户提供的《Data Watch 工程师审查与专项核查最终复核》，粘贴文本含 113 个编号；自报基线 `6b3630c673a563dfcf95ecbe97bae1b71939e9e7`。本文引用的是它实际作出的判断，不认定其所有“只读/无改动”过程声明已经外部核验。

**C0：**上一轮固定 `6845e800990f8352f46655036741bd79ca25eed1` 的源码/文档核对，及本轮再次读取 main、历史提交、争议文件。代码与文档事实以可定位源文件为依据；跨环境后果只在有实测或明确控制流证据时升级。

**本轮实际执行：**只读 GitHub 获取；两项源码片段的隔离 Python 逻辑复现；Markdown 生成和编号完整性检查。未修改仓库，未创建提交，未推送，未部署，未操作业务数据。

**解释原则：**把“代码存在的问题”“需特定条件触发的影响”“文档落后”“已知设计限制”“尚未验证疑点”分开。既不以保守措辞掩盖真问题，也不以审查条目多制造已证实严重故障的印象。
