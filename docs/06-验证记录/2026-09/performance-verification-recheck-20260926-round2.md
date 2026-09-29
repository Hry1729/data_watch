# Data Watch 复核整改 · 第二轮（冻结证据 + 定向实验）2026-09-26

第一轮报告（四个任务的整改与隔离验证）仍在 `docs/06-验证记录/2026-09/performance-verification-recheck-20260926.md`。
本轮针对评审意见只做四件事，**不重做四个任务**：

1. 把本轮改动固化成**可复核证据包**（含未跟踪新文件、原始 JSON、测试输出、环境与校验值）；
2. 补上任务 2 的**内容覆盖证明**（行数不能作为删除残留的充分依据）；
3. 把映射快照"发布后不可原地修改"写成**接口约束 + 测试**；
4. 只追两个问题：**普通生产期确认速率略低于输入是否构成长期增长**（队列守恒），
   以及**历史确认延迟发生在当前批持久确认还是"上一批阻塞下一批投递"**（逐批时间线）。

评审认可的部分（任务 1、2 方向、任务 3 正确性加强、任务 4 实验结构）保持不变，不再改动实现。

---

## 0. 与第一轮报告的差异（避免两份报告互相矛盾）

第一轮报告 `performance-verification-recheck-20260926.md` 仍作为四个任务的整改记录，
但下面几处以**本轮为准**：

| 第一轮写法 | 第二轮更正 |
| --- | --- |
| 任务 2「残留行数 > 正式表则报错，否则丢弃」 | 仅比行数**不足**以免删数据：改为必须先通过**内容覆盖证明**（等量不同内容、少量独有记录都会保留并报错） |
| 「P99 1500ms > 1000ms 预算」（用直方图分位数） | 1500ms 是**桶上界**，不是精确 P99；改判为**阈值内样本占比**，并输出样本计数与桶区间 |
| 「延迟由 AOF 批等待造成」 | 客户端只能测到一次流水线往返（脚本执行 + WAITAOF 等待**不可拆**）；拆分靠 Redis 侧 `commandstats`/`INFO cpu`，归因见 §5/§6 |
| 「latest 快车道往返 10ms」可代表实时显示 | 只代表该次 Redis 调用；实时显示新鲜度仍是**未测** |
| 任务 4「余量 48.3% / 39.5%」 | 数值保留，但仅限**指定负载、指定有效区间**下的追平余量，不扩大为全系统验收 |

## 1. 状态标记（与评审意见对齐）
| 任务 | 可认可的部分 | 本轮状态 |
| --- | --- | --- |
| 1 映射 reload 后旧转换结果失效 | 绑定实际使用的映射对象；撤销修复后有失败反例；缓存发布约束已写进接口说明并有测试 | **本地验证通过，待固定版本复核** |
| 2 旧台账残留恢复 | 用旧版本真实表名；保留历史元数据与歧义数据；本轮补齐"内容覆盖证明" | **本地验证通过，待固定版本复核** |
| 3 统一全链路验收 | 唯一规则源、全字段对账、动态新增点、故障注入校验失败 | **正确性部分通过；历史确认延迟未通过当前测试预算；生产形态与浏览器未验收** |
| 4 受控积压与额定追平 | 明确暂停回放、预置 6000 帧、暂停期确认不前进、额定输入下追平 | **工具与两档追平实验本地通过；持续运行容量仍未通过整体验收** |

两个层次必须分开写：

* 「**测试工具能够正确测量余量**」——可以关闭（本轮补了守恒与时间线，工具本身也做了反例验证）。
* 「**Data Watch 已满足持续容量、延迟与 30% 余量**」——**不能关闭**（见 §5 结论与未验证清单）。

---

## 1. 任务 2 补正：行数不是删除残留的充分依据

### (1) 复现与根因
评审指出的形态在修复前的代码里真实存在：`_recover_interrupted_migration` 在正式表存在时
只比较**行数**（残留 ≤ 正式 → 直接 `DROP`）。于是：

* 正式表 1 行 A、残留 1 行 B（行数相同、内容不同）→ 残留里的历史被删；
* 正式表 3 行、残留 2 行且其中 1 行是正式表没有的 → 那 1 行独有历史被删。

### (2) 改动
`collector/storage/reject_gap_log.py`
* 新增 `_content_covered_by_official()`：按**共同列**、用 NULL 安全的 `IS` 比较，
  以**重数**（`GROUP BY` + `COUNT(*)`）判定"残留的每一行是否都能在正式表里找到"；
  没有共同列 → 视为未覆盖（宁可报错保留）。
* 删除非空残留前必须先通过覆盖证明；不通过则 `RuntimeError`，**保留全部数据**并报出行数。
* 行数更多（`conflicting`）的快速拦截保留，作为覆盖证明之外的额外保险。

### (3) 反例与对照（离线故障注入）
`collector/tests/test_collector_outbox.py` 新增：

* `test_leftover_with_same_row_count_but_different_content_is_not_deleted`（等量不同内容）
* `test_leftover_with_few_rows_absent_from_official_is_not_deleted`（少量独有记录）
* `test_fully_covered_leftover_is_still_discarded`（反向对照：真被覆盖时仍正常丢弃）

**A/B 反证**（只把覆盖证明退化成"行数少就删"，其余不动）：
`2 failed, 1 passed` —— 两个数据丢失用例报错，反向对照仍通过，说明测试确实钉住的是覆盖条件本身。
恢复覆盖证明后 `47 passed`。

### (4) 边界
已闭环：等量不同内容、少量独有记录、完全覆盖三种形态。
未验证：现场是否存在**既非新旧临时表名**的第三张残留表（白名单外不猜表名）。

---

## 2. 任务 1 补正：映射快照发布后不得原地修改

### (1) 约束（写进接口说明）
`writer/mysql_mapper.py` 的 `GroupMapping.var_map` 上明确写出：

> 发布后不得原地修改：`load()` 每次都新建 dict，`reload()` 整体替换缓存对象。
> `writer/consumer.py` 的快路径用 `conversion_var_map is mapping.var_map` 判断
> "已算好的转换结果是否还对得上当前映射"，一旦允许 `clear()`/`update()`，
> 身份判断失效（旧转换结果被误判为有效 → 漏点或错列）。

### (2) 测试固定
`writer/tests/test_mysql_mapper_permanent_mapping.py::test_published_mapping_snapshot_is_replaced_not_mutated_in_place`：
连续两次 `reload()`，断言旧快照对象**内容冻结**、新快照是新对象、
缓存指向新对象（`reload` 后 `get_cached` 即新对象）。

### (3) 边界
多 Worker 一致性不在本轮范围：按评审意见留给**生产启动形态**（`writer/main.py` 入口）
的验证，本轮不做全局版本系统。

---

## 3. 任务 3 补正：延迟判定改用"阈值内样本占比"，并拆开三个指标

### (1) 根因
`TimingHistogram.p99_ms` 返回的是**命中桶的上界**（`collector/monitor/performance.py`：
`value = max_ms if isinf(upper_bound) else upper_bound`）。所以"P99 = 1500ms"只说明
p99 落在 `[1000,1500]` 桶里，既可能高估也可能掩盖真实超标。

### (2) 改动
* `data_watch_shared/acceptance.py` 新增 `latency_threshold_report()`：
  输出**总样本数 / ≤预算样本数 / >预算样本数 / 阈值内占比 / 分位数所在桶区间 / 最大值**，
  以"阈值内占比 ≥ 99%"判定（等价于原 P99 ≤ 预算，但用真实样本计数）；
  无样本时 `not_tested`（未测 ≠ 通过）。
* `scripts/validate_docker_pipeline.py` 记录**逐帧原始样本**（不再只留直方图），
  判定改用上述报告；`latency_breakdown` 明确标注"各阶段属于不同样本，不得相加"。
* 三个延迟指标分开输出，不再混用：

| 指标 | 起点→终点 | 本轮状态 |
| --- | --- | --- |
| 实时显示新鲜度 | 采样发生 → 页面数值真正更新 | **未测**（`not_tested`，只测到 Redis/latest 往返） |
| 历史入库确认延迟 | 采样发生 → TDengine 完整帧确认 | **按 1000ms 测试预算判定**（见 §4） |
| Redis 持久交付延迟 | 本地持久接收 → Redis 持久确认 | 已测（`redis_aof_batch_ms` + 批次时间线） |

* 预算来源显式写入结果（`LATENCY_BUDGET_ORIGIN`）：1000ms 来自现场"页面显示超过 1 秒"的
  观察，**不是**已约定的"历史入库确认延迟"产品 SLA；本轮**不放宽**该预算，也不把它升级成全局 SLA。

### (3) 反例测试
`scripts/test_pipeline_acceptance.py`：
* 9900/10000 在阈值内 → `pass`，并断言 p99 落在 `[0,500]` 桶（桶上界 ≠ 精确 P99）；
* 9800/10000 → `fail`，断言 p99 桶区间是 `[1000,1500]`（只报桶上界会说糊"其实 98% 在 1000ms 内"）；
* 空样本 → `not_tested`。

---

## 4. 两个定向问题的测量工具（本轮新增）

### (1) 队列守恒对账
`data_watch_shared/acceptance.py::queue_conservation()`，口径写在结果里：

```
A = 本地持久提交的逻辑帧数（cumulative accepted）
D = 已完成可靠交付并提交本地清理的帧数（cumulative confirmed）
Q = 仍在本地责任范围内的帧数 = A − D
对账式：A − D == 队列 API 条数 + 已领取未清理(claimed)
```

**claimed 必须是独立观测**：本轮在 `collector/storage/collector_outbox.py` 增加
`claimed_frames`（投递前置位、清理提交或失败后归零，并同步 `outbox_claimed_frames` 指标）。
第一版实现用 `A − D − 队列条数` 反推在途量，被自己的测试抓出**残差恒为 0**（差额被自动吸收），
已改成独立观测；`scripts/test_pipeline_acceptance.py` 里有"缺独立观测必须判 fail"的用例。

输出的判定：`counter_residual`（计数自洽）、`identity_residual`（独立观测闭合）、
逐窗口 `net_growth_fps`（总增量÷总时长）、`classification`（尾部仍在增长才叫
`sustained_growth`，否则 `sawtooth_or_converging`）。

### (2) 逐批时间线
`collector/storage/durable_redis_writer.py` 记录最近 512 个回放批次的单调时钟：
`claim_at`、`write_sent_at`、`aof_confirmed_at`、`frames`、`bytes`、
`queue_frames_at_claim`、`queue_oldest_age_ms_at_claim`、`prev_claim_at`、`prev_aof_confirmed_at`；
`data_watch_shared/acceptance.py::batch_timeline_analysis()` 汇总为
`deliver_ms`（一次流水线往返 = 脚本执行 + WAITAOF 等待，客户端**不可拆**）、
`cycle_ms`、`claim_gap_ms`（上一批确认返回到本批领取的间隔）、
`queue_oldest_age_ms_at_claim`（排队那一段），并给出 `dominant_wait` 判定：

* `previous_batch_confirmation_blocks_next_claim`：claim_gap ≈ 0 且确认往返占满周期
  → 新帧实际是在本地队列里等上一批的持久确认；
* `frames_wait_a_full_cycle_in_local_queue`；
* `current_batch_confirmation`。

**不把各阶段独立 P99 相加当作端到端构成**；确认往返内部"脚本执行 vs AOF 等待"的拆分
交给 Redis 侧计数（`commandstats` 的 `evalsha`/`waitaof`、`INFO cpu` 忙碌比），
脚本已采集这些量级。

### (3) 结果自绑定（未提交工作区如何复核）
两个脚本的 JSON 现在都写入 `provenance`：`script_sha256`、`acceptance_sha256`、`git_head`。
复核者重算这两个哈希即可确认"证据与实现对得上"；
第一轮的旧 JSON 没有这个字段，报告里逐文件标注了它的产生版本。

---

## 5. 30 分钟额定输入长测（实验 A）

配置：20 设备 × 200/500 点 × 100ms = 额定 200 帧/秒，容器 2 CPU、python3.14t（GIL 关闭），
隔离 Redis 7.4（`appendfsync everysec`、`maxmemory 8gb noeviction`，本轮为容纳 30 分钟未消费的
stream 从 4gb 提到 8gb），**不预置积压**（纯生产期，回放正常消费）。
原始证据：`soak-30min-20dev-200pt-rated.json`、`soak-30min-20dev-500pt-rated.json`（含 900 个窗口）。

### 5.1 200 点档（30 分钟）——问题一有答案了

| 观测 | 数值 | 说明 |
| --- | --- | --- |
| 实际接收 | 200.00 帧/秒（1800s，360,000 帧） | `input_on_schedule=pass` |
| 生产期稳态确认 | **200.08 帧/秒（比值 1.000）** | `sustained_replay=pass`；**推翻第一轮 196.58/200=0.983 的"缺口"** |
| 本地队列 | 围绕 60–420 帧波动，首 380 → 末 240，30 分钟净变化 **−72 帧** | `classification=sawtooth_or_converging` |
| 守恒对账（修正后） | 残差中位数 **0**，最大绝对值 200（计数器与队列条数采样时点差一个批次边界） | 原始字段可复算，见 `analyze_soak.py` |
| 积压趋势 | `flat`（四段均值 295/285/303/305） | 不是持续增长 |
| 数据正确性 | `data_correctness=pass`，审计 1198 帧、重复 0、问题 0 | 排空 2.0s，SQLite 0 行 |

**结论（问题一）**：在额定 200 帧/秒、200 点档连续 30 分钟下，本地持久队列**围绕约 1–2 秒工作量
波动，不持续增长**；第一轮观察到的"生产期确认速率低于输入 1.7%"是**短窗口/边界效应**，
不是持续容量缺口。队列的锯齿周期与下面的一致：约每秒一次批量确认。

### 5.2 问题二：延迟是"本批持久确认"还是"上一批阻塞下一批"

逐批时间线（每批 = 一次回放投递，1800s 内 1814 批，恰为每秒约 1 批）：

| 量 | p50 | p95 | max | 含义 |
| --- | ---: | ---: | ---: | --- |
| `cycle_ms` | 1001.0 | 1059.2 | 1174.0 | 批次周期 ≈ 1 秒（AOF `everysec` 节奏） |
| `deliver_ms`（一次流水线往返） | 966.9 | 1016.0 | 1153.6 | 与 1 秒周期几乎相同 |
| `claim_gap_ms`（上次确认→本次领取） | **23.4** | 34.2 | 42.6 | 确认一返回就立刻领取，回放没有别的慢点 |
| `queue_oldest_age_ms_at_claim` | **955** | 1024 | 1132 | 批内最老帧已在本地队列等了将近一整个周期 |
| `write_ms`（领取→发出写命令） | 10.3 | 13.0 | 19.2 | 客户端侧开销很小 |
| `frames` | 200 | 220 | 240 | 每批正好约 1 秒的输入量 |

判定：`dominant_wait = previous_batch_confirmation_blocks_next_claim`。

**同时用 Redis 侧计数排除了"Redis 太忙"这一解释**：

* Redis 实例 CPU 忙碌比 **0.277**（单线程，1798s 内 497s CPU）——远未饱和；
* `waitaof` 1814 次调用共 4436 **微秒**（2.4µs/次）——命令本身不耗时，
  客户端那 ~967ms 是在**等下一次 `appendfsync everysec` 落盘**；
* `evalsha` 360,000 次共 155.8s（433µs/帧）→ 一个 200 帧批次约 87ms 是真实脚本执行，
  只占该批往返的约 9%。

**结论（问题二）**：两者是同一现象的两面。延迟结构是
「帧在本地队列等 ~0–1 个周期」+「所属批次等 ~1 个 AOF 落盘周期」，
而"等一轮"之所以发生，正是因为**上一批的持久确认把下一批的投递推迟到了下一次领取**
（claim_gap 只有 23ms，说明回放没有空闲）。因此：

* **不能**把延迟只归因于"当前批的 AOF 等待"——新帧的排队时间由上一批造成；
* **也不能**说 Redis 执行 Lua 是瓶颈——它只占约 28% 单核；
* 真正的时间去处是 `WAITAOF` 与 `appendfsync everysec` 的 1 秒落盘节奏
  （唯一未经单因素对照直接证实的一环，见 §6）。

### 5.3 500 点档（30 分钟）——**未完成（环境中断，不是测量失败）**

第一次运行到约 25 分钟时，宿主 **Docker Desktop 被外部关闭**：`docker version` 报
`failed to connect to the docker API ... is the daemon running?`，`Get-Process *docker*` 无进程，
容器随之消失。长测脚本只在结束时写 JSON，因此这一档**没有留下可用结果**，如实记为未完成。

* 机器**没有重启**（`LastBootUpTime` 仍是 2026-09-24 11:49），停止的是 Docker Desktop 本身
  （发生在 07:29 之后，A1 的 JSON 已于 07:04 落盘）。
* **业务容器也因此全部处于停止状态**（`MyRedis`/`MyTdengine`/`MyMySQL`/`data-watch-backend`）。
  我没有停止它们，也无法在守护进程未运行时启动它们——是否恢复由使用者确认。
* 恢复守护进程后复跑（命令与 §5.1 相同，仅换 `--points 500` 与输出名）：
  ```powershell
  docker run --rm --name dwv-t34b-soak500 --network dwv-t34b --cpus 2 --memory 4g `
    -v "D:\python\device_alarm\data_watch:/workspace" -w /workspace `
    data-watch-collector-test:20260925 python3.14t `
    /workspace/scripts/validate_collector_ingest_capacity.py `
    --devices 20 --points 500 --seconds 1800 --period-ms 100 --drain-seconds 120 `
    --sample-every-ticks 20 --audit-stride 301 --latest-devices 3 `
    --redis-memory-guard-mb 7000 --cpu-limit 2 --redis-host perf-redis --allow-host 1 `
    --cleanup-always --outbox-dir /tmp `
    --output /workspace/outputs/recheck-20260926-task34/soak-30min-20dev-500pt-rated.json
  ```
  （复跑前需重建网络与隔离容器：`docker network create dwv-t34b`，Redis 用
  `--maxmemory 8gb --maxmemory-policy noeviction --appendonly yes --appendfsync everysec`，
  并保证脚本看到的 `--redis-host perf-redis` 是容器别名。）

## 6. 单因素对照（实验 B，隔离实例）

> **未运行**。计划（守护进程恢复后即可执行，约 11 分钟）：
>
> 1. `everysec` 档：与 §5.1 同参数，`--seconds 300`，输出 `ab-appendfsync-everysec.json`；
> 2. `docker exec dwv-t34b-redis redis-cli config set appendfsync always`（**只改隔离实例**）；
> 3. `always` 档：同参数，输出 `ab-appendfsync-always.json`；
> 4. `config set appendfsync everysec` 复原，并在两次运行的 JSON 里核对 `redis_config` 字段。
>
> 要回答的问题：§5.2 已用 Redis 侧计数把"脚本执行"（约 28% 单核）与"等落盘"分开：
> `waitaof` 自身只占 2.4µs/次，而一次流水线往返是 967ms，指向"等下一次 `everysec` 落盘"。
> 若 `always` 下 `deliver_ms` 明显下降，该归因成立；若基本不变，说明这一秒来自别处
> （客户端/网络、RDB/AOF 重写等），必须重新归因。
> **这是诊断对照，不是配置建议**：两组都保留 AOF 与 `WAITAOF` 可靠确认语义，
> 不靠关闭持久化提速，也不改业务 Redis 配置。

---

## 7. Collector 全量基线失败分类（81 项）

明细见 `outputs/recheck-20260926-task34/review-package/collector-baseline-classification.md`。

* **与本轮改动区域（存储/恢复/owner-lock）相关的只有 1 项**：
  `test_collector_receipt_v2.py::test_outbox_owner_lock_rejects_second_process_and_crash_releases`，
  已 A/B 反证为既有问题（只回退 `durable_redis_writer.py` 同样失败）。
* 76 项是**解释器 ABI 不匹配**：驱动包声明 `cp314t`，宿主运行时是 `cp314`（GIL），
  下游表现为"驱动未安装或未启用"、`scan_tasks[0]` 越界等；
  `test_executor` 的 14 项 `IndexError` 也属这一类（`s7` 驱动加载失败 → `scan_tasks` 为空）。
* 1 项是 vendored `cryptography` 与 Python 3.14 不兼容（`_rust.x509`）。
* 3 项待确认（`test_config_reload_loop` 正则、`test_device_manager` 的 `KeyError: 'proxy_port'`），
  与本轮改动无关，本轮不修。
* 计数：改动前 `81 failed / 506 passed / 24 skipped`；本轮结束 `81 failed / 511 passed / 24 skipped`
  （+5 为本轮新增用例：3 个覆盖证明 + 2 个 `claimed_frames`），**失败集合无新增**。
* 想在 `data-watch-collector-test:20260925`（3.14t）里复跑以彻底分离"环境 vs 缺陷"未成功：
  该测试镜像缺 `aiomysql`，三个文件在容器内直接收集失败。此限制已如实记录。

---

## 8. 可复核证据包

`outputs/recheck-20260926-task34/review-package/`

| 文件/目录 | 内容 |
| --- | --- |
| `changes.patch` | **完整补丁（含未跟踪新文件）**：`git diff HEAD` 在 `git add -N` 之后生成，含 6 个 `new file mode`（15 个文件） |
| `new-files/` | 6 个新增文件的可读副本（不必先打补丁就能看） |
| `baseline.txt` | 基线 commit/branch、工作区状态、宿主 Python/GIL、CPU、Docker 版本、镜像 ID、隔离容器 |
| `env/` | 宿主与容器 `pip freeze`、Redis/MySQL/TDengine 版本与关键配置（appendfsync/maxmemory/rewrite 阈值） |
| `evidence/` | 本轮全部原始 JSON 与容器控制台日志，含 30 分钟长测 `soak-30min-20dev-200pt-rated.json`（1.39MB，900 个窗口 + 512 条批次时间线） |
| `analyze_soak.py` | 长测聚合复算脚本（含守恒等式的修正版重算） |
| `test-logs/` | 各测试根的输出（scripts / writer / collector 改动相关 / collector 全量） |
| `collector-baseline-classification.md` + `collector-baseline-failures.txt` + `collector-baseline-reasons.txt` | 81 项失败的原因清单与分类 |
| `README.md` | 包内容说明、如何应用补丁与复算校验值、未完成实验的原因 |
| `SHA256SUMS.txt` | 包内文件（42 个）+ 15 个被改源文件的 SHA256 |

**注意**：`outputs/` 被 `.gitignore` 忽略，所以证据包本身不会进版本库；
补丁里**也不包含** `outputs/` 的内容——这正是"只导出 diff 不算材料齐全"的原因，
故把原始 JSON/日志/环境一并放进包里。

---

## 9. 仍未验证（不得写成已完成）

1. **500 点档 30 分钟长测**（Docker 被外部关闭而中断，已如实记录，命令已备好）；
2. **实验 B：`appendfsync` everysec vs always 单因素对照**（未运行）；
3. 浏览器接收 → 数值应用 → 绘制延迟（实时显示新鲜度）；
4. 可靠移交恢复（DLQ 响应丢失、重启故障注入）、查询侧完整性过滤/提交标记；
5. 额定规模（100 设备 × 1000 点）冻结版本长测与 30% 余量冻结验证；
6. 多 Worker 形态下的映射一致性；
7. 降速恢复档（`degraded_recovery` 仍为 `not_tested`）；
8. 宽帧提速；
9. 生产 `writer/main.py` 启动入口形态（本轮的链路脚本是核心组件集成形态）。

## 10. 本轮对第一轮结论的两处**自我更正**

1. **"生产期确认速率比输入低 1.7%"不成立**：30 分钟长测为 200.08/200.00 = 1.000，
   队列在 30 分钟内不增长（净 −72 帧，`sawtooth_or_converging`）。第一轮那个数来自
   20 秒窗口 + 预热边界，属测量窗口效应，已按长测结果更正。
2. **守恒等式写错了**：第一版把"已领取未清理"当成相加项，而 `outbox.count` 本来
   就包含这些行——长测的原始数据直接把这个错误暴露出来（残差恒等于 `−claimed`，
   最大绝对值 200 = 一个批次的帧数）。已改为 `A−D == 队列条数`，并把 `claimed` 作为
   其**在途子集**单独报告（含 `claimed ≤ count` 的子集校验），
   `scripts/test_pipeline_acceptance.py` 里有对应的 4 个用例钉住这个口径。
   附带说明：这个错误**不会**影响"队列是否增长"的结论（该结论只用 accepted/confirmed 的增量）。
