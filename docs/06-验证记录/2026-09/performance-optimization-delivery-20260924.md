# Data Watch 性能与入库修复交付记录

> 后续状态已更新：请优先阅读 [本机 Docker 优化与验证续报](performance-optimization-validation-20260924.md)。本文件保留当时的阶段记录。


日期：2026-09-24。源码基线：`47c105cec9b4c2ee5d7fb8ff15694868697c70fc`。

> 本轮代码修复与离线验证已完成，整份方案的现场验收及迁移项目尚未完成。源码基线与方案一致；实际运行安装包的构建提交未核实。已有未提交修改保留。本次不提交 Git、不部署或重启现场服务。

本轮分工：3个GPT-6 Sol（xhigh）负责Writer、采集存储、后端/前端实时链路；1个GPT-6 Luna（max）负责receipt只读诊断；主agent进行交叉审查、独立复测、监控口径修复与报告汇总。

## 已复现的关键问题

- Writer 只预算数值 SQL，遗漏质量 SQL：同一设备 200 帧 × 1000 点产生 6,244,691 字节 SQL，超过 3,145,728 字节预算。
- XREADGROUP 的 COUNT 对每个 Stream 生效；多流合计可超过消费者剩余容量。
- 动态发现任务直接处理 pending，可与 flush 交错，旧缓冲引用覆盖新增记录。
- 集中式采集逐帧 SQLite FULL 提交，空队列按 1 秒轮询；实时状态等待历史投递。
- 两条采集写入路径缺少 rt:change，后端与前端都存在秒级刷新等待。
- 未知 Stream lag 被汇总为零并标为正常，可能掩盖无法观测的积压。

## 离线复现与复测方式

以下命令仅在隔离的模拟数据库执行端上调用真实 Writer 组包实现，不连接 TDengine/Redis：

```powershell
python scripts/benchmark_writer_packing.py --devices 1 --points 1000 --frames 200
python scripts/benchmark_writer_packing.py --devices 100 --points 1000 --frames 2
```

固定原始采样时间间隔 100ms；每帧包含完整数值与质量映射。输出明确区分预期物理行数、SQL 字节数和唯一逻辑帧结果。工具计时是组包加模拟执行时间，不是数据库吞吐。

## receipt 空间治理：迁移条件与设计

现有 `collector:receipt:<UUID>` 的七天 TTL 本轮不缩短、不清理。每帧一键的增长风险仍需治理；仅减少键寿命会让仍在本地队列中的旧帧失去重试去重凭证。固定七天本身也不保证无限期本地积压的幂等性。

建议下一阶段采用 producer/session + 单调序列号与确认水位协议，满足以下条件后再迁移：

1. 每个新会话使用不可复用身份；序列分配与帧入队在同一 SQLite 事务提交。重装、启动和从备份回滚不能复用已发放身份/序列组合。
2. Redis 原子脚本在历史写入成功时推进该会话去重水位。出现乱序时使用有界间隙集合；间隙达到上限应施加背压，不能跳过缺口推进水位。
3. 水位写入必须与对应历史 Stream 写入一起得到同连接 WAITAOF 确认。确认响应丢失仍使用原始会话/序列重放。
4. 不按短 TTL 删除仍可能重试的会话水位。会话退休、确认范围和备份可回放期限需形成明确契约；历史水位的清理不能仅根据“本机当前队列为空”。
5. 旧 UUID 帧在迁移期继续走旧回执路径；双协议可读，新协议启用及回滚均需版本门禁。旧队列排空之前不能清理旧 receipt。
6. 必测重装、会话切换、旧备份回滚、乱序、并发实例、响应丢失、WAITAOF失败和幂等重放。没有这些证据前，不把设计称为已完成的去重迁移。

## 仍需现场验证的边界

- 本机 Docker daemon 不可用，普通 Python 环境无 taosws；未运行真实 Redis Lua/WAITAOF 与 TDengine 集成压测。
- 部分后端模块依赖 free-threading Python；本机普通 Python 导入 TurboAPI 失败，默认 free-threading Python 加载加密库有原生崩溃。独立可运行的服务测试不等同于完整后端启动验证。
- 未进行真实断电、进程强杀、PLC断网、实际安装包回归或固定负载下30–60分钟稳态试验。
- 不宣称达到100设备下实时链路P95≤200ms、P99≤300ms，或采样到TD确认P99≤2s。这些仍是候选现场验收目标。
- 多历史批次并行确认需要 claim/lease/批次身份及连接偏移隔离；单owner顺序重放保持现有安全边界。
- CPU多进程分片需 profile 证据和Windows spawn生命周期设计；质量仅变化写入需完整历史查询/缺口/回滚迁移方案。本轮不改变质量语义。

## 最终变更与测试结果

### 代码交付范围

| 方案项 | 本轮实现 | 状态/剩余事项 |
| --- | --- | --- |
| C01 | SQLite单写者5ms窗口成组提交；排队256帧/8MiB；提交后确认；事件唤醒；outbox条数/载荷字节/年龄指标；历史单owner最大1024帧并受字节预算限制；独立latest合并队列及重启恢复 | 核心优化完成；未引入并行历史owner/claim/lease，未实现跨时钟回滚epoch |
| C02 | 有界只读receipt内存/TTL诊断；迁移契约见上文 | 容量增长机制仍存在，未应用receipt压缩迁移 |
| R01 | 两条采集写入路径发rt:change；后端100ms最小推送间隔、5秒兜底；点集合过滤；显式新鲜度能力协商兼容旧客户端；数值100ms/图表默认250ms；有界串行图表写入 | 代码与离线回归完成；浏览器端到端延迟未实测 |
| W01 | 数值/质量/前缀/分隔符统一UTF-8预算；同表多行合并；流式分块；重试仍受预算限制；整逻辑帧完成后才确认 | 代码与离线回归完成；真实TDengine执行未测 |
| W02 | 保留完整质量语义；统计SQL字节、已完整提交逻辑帧对应的数值与质量行数 | 未测数据库磁盘成本；计数不是含重试的实际物理写入次数 |
| W03 | 多流共享COUNT容量并轮转；在途行/估计载荷字节计入预算；超额投递显式记录ID；pending回收限条数/页数并保留游标 | 核心修复完成；Redis响应本身和Python对象内存并非硬字节上限；未建立全链路硬时间预算 |
| W04 | SQL按块交给4线程执行单元；记录parse/build/执行器等待/TD调用/ACK耗时 | JSON解析等仍有事件循环CPU工作；未做多进程扩容及现场CPU profile |
| W05 | 动态发现只排队配置；consumer负责接入；active/inflight交换、完整结果校验、异常/取消恢复 | 代码与并发回归完成 |
| W06 | schema/尺寸问题保留在PEL，不拖住正常批次；基础设施/未知失败不按坏数据DLQ；独立worker指标与身份；异常退出明确取消其余worker | 尚无全面按热度分片/在线迁移；完整重试队列公平调度、持续总量面板及全链路event-loop-lag/最老数据年龄未补齐 |
| PR0/PR6 | 修正未知lag口径；补离线真实组包负载；取证脚本补XINFO lag及未知pending；修复详细采样间隔=1失效 | 完整现场取证、版本指纹核实、故障注入和安装包回归未完成 |

### 关键默认值与含义

- `REALTIME_WS_MIN_PUSH_INTERVAL_MS=100`；`REALTIME_WS_FULL_SYNC_INTERVAL_SECONDS=5`。
- 前端现有保存的刷新率迁移为图表刷新率，保留用户选择；数值/质量使用独立100ms刷新。
- 最新采样时间是所选点Redis latest源时间`t`的最大值。latest图表为可见值变化，不代表完整100ms采样序列或历史已入库。
- 本地队列单帧超过8MiB时明确拒收并记录缺口，不清除既有积压；队列上限不能保证无限故障时继续无损接收新采样。
- latest刷新队列上限1024组/8MiB，溢出明确计数并降级为历史投递；不提前删除SQLite历史帧。
- 历史Redis常规批次预算4MiB（含保守协议估算），旧单帧例外仍受16MiB有限上限约束；超上限保留并报错，需要人工诊断，不能声称无阻塞自动恢复。
- Writer `max_buffer_bytes` 默认32MiB，用于载荷估计背压，不等同于进程RSS硬限制。
- `committed_value_rows` / `committed_quality_rows` 对应完整逻辑帧结果，不含局部成功后重试产生的额外物理写入；`sql_bytes`用于已尝试SQL的字节观测。

### 独立复测结果

| 负载 | 修复前 | 修复后 |
| --- | --- | --- |
| 同设备200帧，每帧1000点及质量 | 1块；最大6,244,691字节；超预算 | 2块；最大3,133,946字节；200帧唯一完整结果 |
| 100设备，各2帧，每帧1000点及质量 | 3块；最大3,083,971字节 | 3块；最大3,083,971字节；200帧唯一完整结果 |

两组均以3,145,728字节为SQL预算。结果来自真实组包逻辑与模拟执行端；不据此计算生产吞吐或P95/P99。

### 测试记录

- Writer：146 passed，主agent已独立复跑。
- Collector storage：44 passed，主agent已独立复跑。
- 后端实时推送+监控口径+设备状态：24 passed，6 subtests passed，主agent已独立复跑。
- scripts工具：12 passed，主agent已独立复跑。
- 前端：19 passed；`vue-tsc --noEmit`、生产构建通过（实时链路子agent执行）。
- PowerShell取证脚本：语法解析通过；4种lag解析样本通过；详细间隔1/2/6的调度验证通过。
- Collector更广调用链：28 passed / 16 failed。现有驱动声明cp314t，而测试解释器为cp314，导致驱动注册前置失败；这些失败未忽略或计为通过。
- 后端路由撤权回归与dashboard完整测试：受TurboAPI/free-threading运行时限制，未通过收集，不计入通过结果。
- 目标Vue文件全量ESLint仍有既有格式与未使用符号问题；没有为了本任务改动无关格式。

### 复测入口

```powershell
python -m pytest writer/tests -q
$env:PYTHONPATH = 'D:/python/device_alarm/data_watch/collector;D:/python/device_alarm/data_watch'
python -m pytest collector/tests/test_collector_outbox.py collector/tests/test_redis_writer.py -q
$env:PYTHONPATH = 'D:/python/device_alarm/data_watch/backend'
python -m pytest backend/app/tests/test_writer_group_metrics.py backend/app/tests/test_module_monitor_device_status.py backend/app/tests/test_ws_push.py -q
python -m pytest scripts/test_inspect_receipt_capacity.py scripts/test_benchmark_writer_packing.py scripts/test_summarize_performance_snapshots.py -q
```

receipt现场诊断需通过安全渠道在当前会话配置`REDIS_URL`；不要把口令写入命令历史。随后运行：

```powershell
python scripts/inspect_receipt_capacity.py --max-keys 1000 --max-scan-calls 20
```

诊断只读SCAN/MEMORY USAGE/TTL，连接和读取超时5秒。`observed_bytes`是已观测字节累计值；`scan_complete`仅表示本轮游标走到0，并非原子快照或精确容量。截断结果不能外推为全量。错误时只输出通用提示并以非零状态退出。本次只运行该工具的fake客户端测试，没有调用现场Redis。


## 后续验证更新：本机Docker后端（2026-09-24）

上一轮所述Docker daemon不可用及后端测试收集阻塞已通过本机Docker临时容器绕开：挂载当前源码、使用镜像内Linux Python 3.14.4 free-threading并对齐Asia/Shanghai时区，后端完整测试175项通过（另6个子测试），实际应用入口及未登录HTTP拒绝响应验证通过。SQLAlchemy扩展会使GIL重新启用，需后续核查。此次未启动数据库相关lifespan，也未完成真实Redis/TDengine全链路测试。详见 [Docker后端验证记录](docker-backend-validation-20260924.md)。
