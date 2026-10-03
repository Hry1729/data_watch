# 联合测量：真实采集回放 + 生产 Writer（2026-09-27）

目的：把上一轮"**Redis 历史路径比 Writer 更早成为瓶颈**"从"较强假设"验成实测（或推翻）。
代码：`10a5d58` + 本轮两个已推送提交（`3bc1b10`、`6b3630c`）；本报告新增工具
`scripts/diagnose_joint_pipeline.py` **未提交**。
证据：`outputs/joint-20260927/`。业务容器未触碰；隔离环境自建、结束即删。

## 1. 事前写明的判定标准（避免事后找理由）

* **支持**：联合跑时 Redis 主线程接近饱和（≈1.0）且采集侧确认速率被压在负载之下，而 Writer 不出现持续积压。
* **推翻**：Writer 先积压（lag/PEL 增长、排空时间随负载上升），或 Redis 未饱和。
* 无论哪种，都先过对账（值/质量行数 + 抽样逐列 + PEL/DLQ），否则数字不进结论。
* 不宣称：100 台 × 1000 点、浏览器可见延迟、30% 余量、故障矩阵。

## 2. 方法（为什么必须新写一个工具）

现有两个脚本**不能并跑**：`validate_collector_ingest_capacity.py` 自己审计 Stream 且**假设条目不被消费**，
而 Writer 是 LANDED→XACK+XDEL；并跑会得到"帧缺失"的假失败。
`diagnose_writer_tdengine_capability.py` 的持续喂入是 **XADD-only**（刻意绕开历史 Lua 来隔离 Writer），
而联合测试要的正是**真实采集路径**（SQLite 持久队列 + 历史 Lua + latest 快车道）。

新工具 `scripts/diagnose_joint_pipeline.py` 三个模式，按依赖分布分容器跑：

| 模式 | 容器 | 作用 |
| --- | --- | --- |
| `plan` / `prep` | 宿主 / pipeline-runner | 生成计划（设备、点位、时序基线）；建 MySQL 映射与范围表、TD 库、Writer 运行目录 |
| `collector` | data-watch-collector-test（3.14t、GIL 关闭） | 用**（当时的）`DurableRedisWriter` + SQLite Outbox** 按目标速率喂入，并采样一条时间线 〔⚠️ **适用路径：已撤销的中心 Durable 方案**。该方案现非生产实现：中心默认分支已是 `RedisWriter` 直写、不构造 SQLite Outbox、不逐批 `WAITAOF`（`collector/main.py:181-192`）。原文「**生产**」措辞不成立；本行及本报告结论**仅适用于该旧路径**。现行口径见 `docs/03-数据与契约/DW-01-确认语义与持久化边界.md` §1/§4。〕 |
| `reconcile` | pipeline-runner（有 pymysql/taosws） | 读 TDengine 对账 + 核对 Stream/PEL/DLQ |

隔离环境：自建网络 + `redis:7.4`（`everysec`、`maxmemory 4gb`、`noeviction`）+ `mysql:8.4` + `tdengine 3.4.0.9`；
Writer 用**生产入口** `writer/main.py`（配置发现、consumer group、TD 连接、`shutdown_flag` 停机）。

## 3. 两轮运行（第一轮暴露了驱动缺陷）

| | 第 1 轮 | 第 2 轮（有效） |
| --- | ---: | ---: |
| 实际输入 | **104.51 帧/秒**（12,560 帧/120.18s） | **199.99 帧/秒**（24,000 帧/120.004s） |
| 目标 | 200 帧/秒 | 200 帧/秒 |
| `input_rate_ratio` | 0.5226 | **1.0**（`input_rate_reached=true`） |
| 点/秒 | 52,255 | **99,995** |
| Redis 主线程忙碌比 | 0.4266 | **0.7429** |
| 单位成本（Redis 主线程 ÷ 帧） | 4.08 ms/帧 | **3.71 ms/帧** |
| Writer 积压峰值 / PEL 峰值 | 100 / 88 帧 | **137 / 109 帧** |
| 采集侧确认 | 12,560 / 12,560 | **24,000 / 24,000** |
| 排空 | 2.014 s | 2.001 s |

第一轮是**单线程串行提交**，撑不到 200 帧/秒 → 变成"驱动限速"，**不能用于饱和判定**；
它只用来做下面第 6 节单位成本的交叉验证。第二轮改为与容量脚本一致的**并发提交**
（每 tick 每设备一个任务、`ThreadPoolExecutor`），并把 `input_rate_ratio` / `input_rate_reached`
写进结果，这类"驱动限速冒充系统限速"的问题以后不会再悄悄通过。

## 4. 第 2 轮（有效轮）的完整结果

采集侧（`joint-collector.json`）：提交 24,000 帧 = 200 帧/秒 × 120 s；
`durable_frames_accepted` 24,000；`history_frames_confirmed` 24,000（窗口内全部确认；⚠️ 这两个 `durable_*` 指标仅适用于**已撤销的中心 Durable 路径**，非现行中心式）；
`latest_fastlane_sent_groups` 21,017（快车道按组合并，少发约 12%）；
Outbox 峰值 500 帧、最老积压峰值 2,446 ms；结束时 Outbox 0、排空 2.0 s。〔⚠️ 本段 `Outbox` / `durable_*` / `accepted_frames` 均为**已撤销的中心 Durable 方案**（`DurableRedisWriter` + SQLite Outbox）的指标：现行中心式由 `RedisWriter` 直写、**无本地队列**、**不逐批 `WAITAOF`**（`collector/main.py:181-192`），故本报告的吞吐/延迟结论**仅适用于该旧路径**，不得当作现行中心式结论。〕

Redis 侧（同一窗口，`INFO cpu` 主线程差值）：**89.15 CPU 秒 / 120.004 秒 = 0.7429**（未饱和）；
进程忙碌比 0.7442（单线程执行，二者一致）。

Writer 侧（生产入口，`writer-joint-run2.log`）：

| 量 | 值 |
| --- | --- |
| 值行成功 / 失败 | 24,000 / **0** |
| 质量行成功 | **11,923,200**（= 期望值，见第 5 节） |
| flush 次数 / 失败批次 | 463 / **0** |
| `td_execute_ms` | 平均 21.0，p95 50，p99 100，max 99.1 |
| `flush_ms` | 平均 70.1，p95 200，p99 200，max 182.7 |
| `rows_per_flush` | 平均 51.8，max 122 |
| 峰值缓冲行 | 122 |
| 停机 | `shutdown_flag` → 退出码 **0**（"Writer process stopped"） |

Writer 侧压力只能这样**粗略**看：463 次 flush、`flush_ms` 平均 70 ms → 累计 ≈32 s / 120 s。
**这是由 flush 耗时推导的占用，不是真实 CPU 时间**（本轮没有采 Writer 进程的 CPU 时间增量）；
期间 Stream 积压始终 ≤ 137 帧 → **Writer 未接近自己的上限**。
真实 CPU 时间增量从阶梯轮起强制采集（见 `diagnose_joint_pipeline.py` 的 `--mode collector` 输出字段
`writer_process_cpu_seconds`）。

## 4.1 计数边界：生产结束 ≠ 排空结束（必须分开记）

各计数来自不同环节，**不得互相替代**；本轮与阶梯轮统一按下列口径记录：

| 计数 | 来源 | 口径 |
| --- | --- | --- |
| 计划帧 `planned_frames` | `tier.devices × ticks` | 该档"打算发多少帧"（用于判断驱动是否达速） |
| 实际尝试帧 `attempted_frames` | 驱动侧自计 | 真正调用写入的帧（未达速时 < 计划帧） |
| 本地持久接收帧 `accepted_frames` | `durable_frames_accepted` | SQLite 事务提交成功、责任已落到本地队列 〔适用路径：已撤销的中心 Durable 方案；现行中心式无本地队列〕 |
| Redis 交付确认帧 `redis_confirmed_frames` | `history_frames_confirmed` | 交付 Redis 且**本地清理已提交**（与删除同事务） |
| TD 完整确认帧 `td_confirmed_rows` | 对账阶段读 TDengine | 值表行数（一帧一行）；质量行按 **tick 逐帧**统计 |
| 拒收帧 `outbox_rejected_frames` | 采集侧指标 | 本地队列满等原因被拒（**不为 0 就是数据缺口**，必须报出）〔适用路径：已撤销的中心 Durable 方案；现行中心式无本地队列，不存在该拒收口径〕 |

**时间边界**：以上计数都要分两个时刻报——`_at_feed_end`（生产窗口结束那一刻）与
`_after_drain`（排空结束）。`after_drain` 的确认量包含排空窗口内完成的帧，**不得**计入生产窗口吞吐。
CPU 与吞吐统计一律在**对账之前**结束；对账（分页审计/TD 查询）不得进入这些时间窗口。
本轮 run2 恰好两个时刻都等于 24,000（排空仅 2.0 s、常驻积压小），但阶梯轮会显式分开记录。

**Writer 的 CPU 口径**：必须用**真实 CPU 时间增量**（容器 cgroup `cpu.stat` 的 `usage_usec` 差值 ÷ 墙钟），
不得由 flush 耗时推导——本报告 §4 里那个"≈32 s / 120 s"就是推导值，只作粗略参考。



`joint-reconcile.json`：**status = pass，problems = 空**

* 值行 24,000 / 期望 24,000（每设备恰好 1,200 行）
* 质量行 11,923,200 / 期望 11,923,200 —— 期望值按**逐 tick** 统计（时有时无 NULL 的点在某些 tick
  不进历史）；若按"帧数 × 列数"会多算 4,800 行，这正是第一版公式的错误
* Stream 排空后 backlog = 0、PEL = 0；DLQ = 0
* `rt:latest` 哈希完整（抽样 2 台设备，均 500 点齐全）
* 抽样全字段：每设备 3 帧（2+1）逐列核对，**其余行只核对行数与身份** —— 不是全量逐列

## 6. 单位成本交叉验证（本轮最有价值的副产物）

实测 Redis 主线程成本：**3.71 ms / 500 点帧**（第 2 轮）、4.08 ms（第 1 轮）。
微基准 `latest-cost-500pt-v3.json` 的分解预测：历史帧（读到快车道已写、比较后跳过）1.361 ms
＋ 快车道写入 2.489 ms = **3.85 ms** —— 与联合实测相差 **4%**。

并且 `commandstats` 直接证明了两条路径**没有重复写**：本窗口 `hset` ≈ 每帧 500 次 latest 写入
（外加每帧 2 次回执行），即**每个点每帧只写一次**；
发生两遍的是**读取/解码/比较**（补偿段先 HMGET + 解码 + 比时间戳，发现快车道已写入更新的 t 后跳过）。
这同时印证了微基准里"补偿段大部分是读开销、而非重复写"的判断。

## 7. 假设的裁决（严格限定在本次条件内）

* **本次条件下没有观察到 Redis 饱和**：20 设备 × 500 点 × 100ms（=100,000 点/秒）时
  Redis 主线程忙碌比 **0.743**，Writer 积压峰值 137 帧、未出现持续积压。
  **0.743 是"本次条件"下的实测值，不是对历史 0.93 的直接更正**——两次运行的负载口径、
  Stream 是否被消费、以及脚本采样方式都不同，不能相互替代。
* **不能把差异归因于审计开销**：已提交的容量脚本里，分页审计
  （`audit_stream_frames`，`validate_collector_ingest_capacity.py:878`）发生在**计时窗口之后**
  （drain 循环在 `:850-862` 结束，CPU 忙碌比只取窗口内的 samples）→ 审计不在 CPU 统计窗口内。
  因此历史 0.93 与本次 0.743 的差值**原因未确认**，需要另行核实（候选：当时 Stream 未被消费、
  在窗口内持续增长到 4.4 GB；或两次负载口径差异）。这是**未验证推断**，不是结论。
* **134.6k 点/秒只是线性外推**（100k ÷ 0.743），**不是实测上限**：本轮只测到一个点（100k），
  没有实测到饱和。饱和需要阶梯加压实测（本轮后续）。
* Writer 单独测量（有消费、无采集共享）能跟上 400k 点/秒（800 帧/秒 × 500 点）；
  本次联合运行中 Writer 未接近上限（积压 ≤137 帧、0 失败）。
* **对原目标的含义**：1M 点/秒 是本次外推值的 ≈7.4 倍。**是否"必须"降低每点 Redis 成本、
  以及换更强的单机是否无效，本轮都没有证据**——前者需要先实测到饱和点，后者需要单机规格对照实验，
  二者都撤回为未验证判断。
* 已确认可支撑的结构性结论只有一条：**同一批点的 latest 读取/解码/比较在两条路径上各做一遍
  （写入只有一遍）**，且其实测单位成本（3.71 ms/500 点帧）与微基准分解（3.85 ms）相差 4%。

## 8. 未测 / 边界（不得当成已完成）

1. **未实测饱和**：0.743 是观测值，1.0 是外推；要到饱和需要再加一档（如 40 设备 × 500 点 = 200k 点/秒）。
2. 只有 **1 个 consumer worker**；多 worker 扩展性未测。
3. 单个 Redis 实例、单机 TDengine/MySQL；跨实例/分片未测。
4. 本轮 MySQL 是**最小同构 schema**（没有 `system_log` 表，Writer 会记一条无害告警）；
   TDengine 为空库自建，不含真实历史数据。
5. 输入不经 `collector.executor` 的分组/过滤（工具直接构造 frame）；生产驱动侧的过滤语义由
   `collector/tests/test_executor.py` 覆盖。
6. 浏览器可见延迟、30% 余量、故障矩阵、现场硬件验收仍未做。

## 9. 证据与可复算

`outputs/joint-20260927/`：

| 文件 | 内容 |
| --- | --- |
| `joint-plan.json` | 本轮计划（设备、点位、时序基线、目标速率、provenance） |
| `joint-collector.json` | **有效轮**采集侧：输入达标自证、确认计数、Redis 主线程/进程忙碌比、216 个采样时间线、commandstats |
| `joint-reconcile.json` | 独立对账：值/质量行数、Stream/PEL/DLQ、latest 哈希、抽样逐列结果 |
| `writer-joint-run2.log` | 生产入口 Writer 原始日志（UTF-16LE；含性能聚合与停机） |
| `joint-collector-run1-driver-limited.json` / `joint-plan-run1.json` | 第 1 轮（驱动限速）证据，只作单位成本交叉验证 |
| `extract_writer_timings.py` | 从 Writer 日志提取计时的脚本（按 BOM 嗅探编码、正则提取） |
| `runtime-joint/config/settings.toml` | 本次 Writer 的有效配置（**含口令字段，外发前须脱敏**） |
| `SHA256SUMS.txt` | 顶层文件 + 本轮改动/新增源文件；原地校验结果见 README |

**下一步建议（二选一，都不急）**：
(a) 直接把 offered 提到 200k 点/秒（40 设备 × 500 点）复跑本工具，把"外推"变成"实测饱和"；
(b) 先做微基准指出的降单位成本候选（客户端预编码载荷，全写档 −0.16…−0.30 ms/500 点），
再复跑同一档看 Redis 忙碌比是否下降。
