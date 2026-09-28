# 中心采集直写 Redis 恢复与隔离验证

中心默认直写 Redis 已恢复，实际运行与回归通过；四轮隔离对照表明恢复后达到本轮吞吐门槛，Redis 主线程开销明显下降。按用户要求，后续观察在约五分半停止。本轮隔离容器、网络和测试数据库均已清理，原始证据保留；业务容器保持运行，未部署、提交或推送。另发现基线已有的停机取消丢样风险，复现证据及验证边界见文末。

## 范围

基线 HEAD 为 `983ba9a`。接手时已有中心默认 RedisWriter 的未提交修改、两个中心测试，以及 `central-runtime-fix-20260927.md`；这些有效修改予以保留。图片及其他无关未跟踪文件未修改。

中心链路：Collector（调度、读取、解析、分组）→ RedisWriter → Redis latest/window/Stream → Writer → TDengine。

分布式链路保持 EdgeOutboxWriter + EdgeStorageClient → SQLite/Outbox → Uploader → 中心 Ingest → Redis。DurableRedisWriter 不是边缘存储实现，保留它供历史诊断与遗留数据处置。

恢复后的中心沿用原直写语义：Redis 写入失败记录 `redis_write_error_count`，不使用采样本地队列补偿；Redis 服务端 AOF 与客户端 WAITAOF 是不同机制。本轮没有关闭或修改业务 Redis 的持久化配置。

## 根因与源码修改

此前中心默认被改成 DurableRedisWriter，使正常采样经过 SQLite、回放和确认。恢复保留当前数据契约与下游修复，仅更换中心默认存储构造。

此前 600 秒运行测试超时源于先等待常驻 start() 返回再关闭；当前测试已使用后台 start → 等待数据 → stop → 等待退出。驱动加载失败源于测试覆盖激活根目录；当前测试已复制 S7 激活包，仅替换设备连接、读取与断开。

本轮生产修改仅 `collector/main.py`：保留 RedisWriter 默认构造；增加中心构造前的遗留采样队列检查，修正 Durable 与边缘实现的注释。没有修改中心调度器、执行器、RedisWriter、Writer 或分布式生产代码。

遗留检查使用原默认路径：`DATAWATCH_DATA_ROOT`，其次 `DATAWATCH_HOME`，否则 `C:\ProgramData\DataWatch`，下接 `runtime/collector-retry.db`。

- 无库且没有未完成日志：直接启动，不创建目录或数据库。
- 已识别的 v1/v2 空队列：只读放行。
- 有任意待处理行、非空 WAL/rollback journal、未知库或读取错误：阻断中心启动，显示路径与原因。
- 使用只读 immutable 连接，不恢复日志，不创建 SQLite 旁文件，不删除、迁移或回放数据。
- 注入 writer_factory 的边缘入口不执行这项中心检查。

检查是切换时的静态检查，不是旧中心进程的互斥锁；正式切换仍需先停止旧中心采集，避免检查后旧进程再次写入。独立拒收缺口台账未自动处理。本轮没有检查或处置业务遗留数据，也没有部署。

## 回归证据

结果位于 `outputs/central-restore-20260928/`：

| 验证 | 结果 | 文件 |
| --- | --- | --- |
| Collector 全量 | 615 passed，24 skipped | collector.xml |
| 最终中心定向（含之后补充的两个日志/权限场景） | 22 passed | central-final.xml |
| Edge 全量 | 43 passed | edge.xml |
| Writer 全量 | 184 passed | writer.xml |

Collector 使用本机 Python 3.14t 及此前本地隔离依赖目录。Edge/Writer 在本机 3.14t 首次收集时于 cryptography 原生模块导入发生 access violation，改用普通 Python 3.14 后完成全部回归；没有为此修改生产代码或安装全局依赖。Collector 有一条第三方 asyncua 弃用提示。

中心实际运行使用本轮专用 Redis 7.4，开启 AOF/everysec。两个运行场景覆盖 bootstrap 与模拟 S7 I/O；保留真实 CollectorApp、默认选择、驱动发现/规划、调度、解析分组和 RedisWriter。检查实际 latest/window/Stream 值、质量码、v2 契约、通知、无采样 SQLite/回放线程和正常退出。该 Redis 容器已清理。

## 性能验证

四轮 120 秒正式对照已完成。按用户交接班前停止的要求，恢复后稳定性测试在 329.856 秒（约 5 分 30 秒）停止，不继续原定 30 分钟测试。当前结果不替代长期稳定性验收。

新的 `scripts/validate_central_pipeline.py` 使用真实中心入口及生产 Writer；旧 Durable 专用容量工具保留，不作为恢复后中心模式的性能证据。

对照口径：同一批 30 台设备、每台 500 个 INT 点、100ms 周期、4 个 Stream 分片、一个 Writer worker；独立 Redis 7.4 开启 AOF/everysec，独立 MySQL/TDengine。A 只覆盖为 `983ba9a` 的原 `collector/main.py`；B 为当前默认 RedisWriter；其他源码相同且逐轮保存 SHA-256、镜像 ID、Python/GIL 状态。设备 connect/read/disconnect/ping 为模拟 I/O，其余驱动发现、S7 规划解码、调度、执行、分组、存储及生产 Writer 均真实运行。

Collector 性能对照运行于 Docker Linux/Python 3.14t。仅临时驱动副本的 OS 清单和匹配的测试目录摘要适配为 Linux，仓库驱动包不变。旧模式 SQLite 位于容器原生文件系统，避免 Windows 共享目录开销干扰。该实验验证架构相对开销与此隔离环境的持续能力，不等价于现场 Windows/PLC 的硬件容量认证。

CPU 取采样窗口内差值，排空不计入；Writer CPU 以 feed 标志采样，边界有约 250ms 轮询误差。每秒记录采集指标、本地队列、Stream/PEL 和 latest 新鲜度。TD 延迟探针核对真实采样时间戳是否出现，250ms 轮询使数值为确认时间上界；不是精确提交时刻。全量核对每设备读取数、接受数和 TD 值行数、质量总行数及非零质量码数量，检查 latest 点数/首点末态、Stream/PEL/DLQ；值内容只抽查前两台首尾行的三列，不宣称全字段逐行核对。

每轮吞吐门槛为目标 300 帧/秒的 95%；始终同时报告实际帧率。未达速的旧模式只记该设定负载下未通过，不据此外推最大容量。调度迟到单独报告，不与实际读写错误混为一谈。四轮顺序 A/B/B/A 各 120 秒，随后仅恢复后 B 做稳定性观察，已按用户要求提前停止。

### 正式短测结果

证据：`outputs/central-ab-20260928/summary-abba-a52cff.json` 与同前缀各轮目录。

| 轮次 | 实际帧/秒 | 达速率 | Redis 主线程/核 | Collector/核 | Writer/核 | TD 确认上界 p95/ms | 判定 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| A1 旧模式 | 283.21 | 94.40% | 0.947 | 1.922 | 0.285 | 3564.50 | 吞吐未达标；完整性通过 |
| B1 恢复后 | 297.32 | 99.11% | 0.268 | 2.140 | 0.403 | 392.26 | 通过 |
| B2 恢复后 | 297.34 | 99.11% | 0.282 | 2.066 | 0.394 | 417.34 | 通过 |
| A2 旧模式 | 284.05 | 94.68% | 0.946 | 1.897 | 0.281 | 3975.39 | 吞吐未达标；完整性通过 |

同一设定负载下，恢复后 Redis 主线程平均占用从 0.9465 核降为 0.275 核，下降约 **70.9%**；按实际帧率归一化，Redis CPU 从约 3.34ms/帧降至 0.92ms/帧，下降约 **72.3%**。这些不是整机 CPU 降幅：本轮 Collector 与 Writer CPU 没有下降，分别见表。

### 约五分钟观察与提前停止

证据：`outputs/central-ab-20260928/de37e7-soak-B/early-stop.json`、`partial-reconcile.json` 及同目录日志。实际 feed 时长为 329.856 秒。正在运行的进程已载入 1800 秒时长且没有提前正常结束接口，因此通过 Docker 停止采集容器，15 秒宽限后退出码 137；主持进程退出 1，不再启动后续任务。Writer 与监测进程退出码均为 0。

停止后外部只读对账：TD 值数据 **98,130 帧**，质量数据 **49,065,000 行**，等于 98,130 × 500；30 台设备各 3,271 帧，latest 各 500 点。Redis Stream backlog、PEL、DLQ 均为零；656 个延迟探针全部确认，确认时间上界 p95 为 423.46ms。此轮没有完整 `collector.json`，不宣称读取数与写入数全量一致，不给出完整窗口 CPU、达速率或完整长测通过结论；上述性能比较仅依据已经正常完成的四轮短测。

A1/A2 本地队列峰值为 1,692/1,858 帧；B 没有采样 Outbox。Stream 峰值 A 为 278/277 帧，B 为 60/139 帧。调度迟到计数 A 为 2,989/2,807，B 为 0/12；四轮均无读写/线程池拒收错误，逐设备 reads=accepted=TD，最终质量、latest、Stream/PEL/DLQ 对账通过，三个测试进程退出码全为 0。整组工具退出码为 1，原因是两轮 A 未达到预置吞吐门槛，不是工具执行失败。

已经完成 30 台 × 500 个 INT 点 × 100ms 的 15 秒冒烟：297.71 帧/秒（目标 300 的 99.24%），4,467 帧逐设备对账一致，质量数据 2,233,500 行且非零质量码为零；Redis Stream、PEL、DLQ 排空。Redis 主线程平均 0.276 核，Collector 1.637 核。原始结果：`outputs/central-ab-20260928/38fcf1-smoke-30x500-B/`。该结果仅为短冒烟，不代表长期容量。

测试工具调试记录均保留：首次复用目录读到了上轮探针，已改为每次唯一运行目录，污染轮位于 `smoke-invalid-stale-probes-30x500`，不用于性能结论。第一轮旧模式正式短测发现停机时读数与入库数相差一帧，结果标失败；没有将它改判为完整性通过。

独立最小复现确认：既有 `TaskScheduler.stop()` 取消采样协程时，可能发生在 `read_success_count` 已增加、解析后续尚未写入 Redis 的窗口。此问题在基线代码也存在，本轮未修改生产调度器。性能工具在计时结束后先禁止新增调度并有界等待当前循环自然完成，再正常关闭、排空 Durable；仍要求每设备 reads = accepted = TD rows。这个基准收尾方式不能用来声称生产强制停机没有丢样风险。修正后的 2 台 × 10 点四轮 A/B/B/A 全部对账通过，证据为 `summary-abba-479b6d.json`。
