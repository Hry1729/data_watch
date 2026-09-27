# Data Watch 交接复核与整改报告（2026-09-26）

复核对象：`https://github.com/Hry1729/data_watch`，工作区 `D:\python\device_alarm\data_watch`。
代码版本：`44ecbdd`（`main`，未提交任何改动、未推送、未部署、未改生产配置与业务数据）。
业务容器 `MyRedis` / `MyTdengine` / `MyMySQL` / `data-watch-backend` 全程未读写、未重启。

**结论标签**（全文严格区分）：
`[源码确认]` 从当前工作区源码直接读出的机制；`[离线反例]` 离线夹具上可复现的失败/捕获；
`[真实集成测量]` 隔离容器里真实 Redis/TDengine/MySQL 上跑出的原始数据；
`[未验证推断]` 机制解释，尚无直接证据。

## 0. 结论摘要

| 任务 | 结论 | 关键数字 |
| --- | --- | --- |
| 1 映射重载后旧转换结果失效 | **已闭环** | 6 个反例测试；撤销修复 4 失败 / 2 通过，修复后 6 全过；writer 套件 182 passed |
| 2 历史迁移遗留台账恢复 | **已闭环** | 6 个以旧版本真实表名 `collector_rejected_gaps__v2` 造的夹具；撤销修复 6 失败，修复后 44 passed |
| 3 全链路验收口径统一 | **部分闭环**：正确性/身份/新增点/latest/恢复/稳态已统一并由反证证明；**延迟项判 fail** | 6000/6000 值行、583800/583800 质量行逐格对账通过；4/4 故障注入被抓；采样→提交 P99 1500ms > 1000ms 预算 |
| 4 额定输入下的处理余量 | **已闭环**：受控积压真形成，余量按额定输入测得 | 200 点档 296.67 帧/秒 → 余量 **48.3%**；500 点档 279.08 帧/秒 → 余量 **39.5%**；旧报告"3%"不成立 |

本轮**新发现并如实记录**的三件事（都不是"工具能跑"层面的问题）：

1. **历史落库路径端到端延迟超预算**：采样时刻→TDengine 提交确认 P99 = 1500ms，主因是
   `WAITAOF 1 0 2000`（`[源码确认]`，`collector/storage/durable_redis_writer.py:424`）遇上
   `appendfsync everysec`，每批要等下一次 fsync，`redis_aof_batch_ms` p50 = 1000ms、
   p99 = 2000ms；同轮 `latest` 快车道 Redis 往返 P99 只有 10ms。旧脚本从不测这项。
2. **额定输入下"生产期确认速率"低于输入**：200 点档 196.58/200 = 0.983，500 点档
   188.45/200 = 0.942，积压因此在额定输入下缓慢累积（旧脚本用"停采后排空"掩盖了这一点）。
3. **运行中新增点位会让积压台阶式抬升一次**（约 +50~60 帧 = 0.5~0.6s），之后持平；
   这属于一次性成本，不是持续增长。趋势判定已据此区分"台阶"与"持续增长"。

---

## 任务 1：映射重载后旧 prepared 结果必须失效

### (1) 复现与根因
`[源码确认]` `writer/consumer.py` 的快车道条件是"`prepared.converted_columns is not None` 就复用"。
而 `converted_columns` 是按某一次映射快照算出来的：批量预备阶段用当时的 `var_map` 转换，
若这批后续条目触发映射重载（`unmapped_var_ids and cache_hit` → `mapper.reload()`），
同一批里靠后的条目已经看到重载后的缓存、不会再卸装映射，于是继续复用**旧快照**的列。
后果：新增点位在重载后的同批/后续条目里被漏写，或按旧列名错写；空 `converted_columns`
还可能让条目"看起来没事"而被提前 ACK。

### (2) 改动文件与关键实现
- `writer/consumer.py`
  - `_PreparedPayload` 增加 `conversion_var_map: dict | None`：把"这份转换结果是按哪一份
    `var_map` 算的"绑在结果上，空字典也要绑（`if var_map is not None`）。
  - `_handle_entry` 快车道改为 `prepared.converted_columns is not None and
    prepared.conversion_var_map is mapping.var_map`：比对的是**对象身份**，映射一重载就
    产生新字典，身份不匹配 → 重算。到达快车道意味着 `unmapped_var_ids` 为空，覆盖性有保证。
- 语义未改：坏值 → DLQ 后 ACK/XDEL；未知点位 → 保持 pending 不 ACK；非法新点仍在隔离路径。

### (3) 新增反例/故障注入测试
`writer/tests/test_consumer_mapping_refresh.py`（6 个）：
同批新增点不漏、只含新点的帧不得空 ACK、同批后续条目不得复用旧转换、重载后非法新点仍进隔离
（断言 `xack(stream, "writer", "1-0")`）、映射缺失保持 pending、映射未变时仍走快车道
（`_convert_values` 恰好被调用 1 次，证明省解析收益未被牺牲）。

### (4) 实际命令与结果
```powershell
python -m pytest writer/tests/test_consumer_mapping_refresh.py -q         # 6 passed
git stash push -- writer/consumer.py; python -m pytest ... -q; git stash pop
                                                                          # 4 failed, 2 passed
python -m pytest writer/tests -q                                          # 182 passed
```

### (5) 原始证据
本轮命令输出（终端，未落盘）；测试文件本身是长期证据。
先前的全链路回归：`outputs/recheck-20260926-task34/pipeline-10dev-100pt-final.json` 的
`correctness=mapping_update=pass`，说明新增点位在真实链路上逐帧正确。

### (6) 边界
已闭环：新增点不漏写、旧结果不复用、坏值/未知点/ACK 语义保持。
未验证：映射重载在**多 worker** 下的一致性（生产入口形态），见任务 3 的覆盖差异说明。

### (7) 下一步
无。若要进一步收紧，可在 `_prepare_payload_batch` 里记录快照版本号而非对象身份——
当前用身份即可，因为 `MysqlMapper.load()` 每次都构造新字典。

---

## 任务 2：识别真实历史迁移遗留（`__v2` 表）

### (1) 复现与根因
`[源码确认]` 旧版本 `4d86b6d` 的迁移把正式表重命名为 `collector_rejected_gaps__v2`，
而当前恢复代码只认 `collector_rejected_gaps__migrating`。于是带 `__v2` 残留的库打开后：
正式表为空、`gaps(run_id)` 查不到历史、`__v2` 里的行谁也看不见——历史静默丢失。

### (2) 改动文件与关键实现
- `collector/storage/reject_gap_log.py`
  - 新增 `_LEGACY_V2_TABLE = "collector_rejected_gaps__v2"` 与
    `_LEFTOVER_TABLES = (_MIGRATING_TABLE, _LEGACY_V2_TABLE, _REBUILD_TABLE)`；
    `_leftover_tables(conn)` 带行数扫描全部残留。
  - `_recover_interrupted_migration` 规则：
    正式表缺失 + 恰好一个非空残留 → 从它重建（`_rebuild_from`），再清理其余空白残留；
    正式表缺失 + 多个非空残留 → `RuntimeError("ambiguous rejection-gap recovery ...")`，
    **保留全部数据**、不建空表假称恢复成功；
    正式表存在 + 残留行数多于正式表 → `RuntimeError("conflicting rejection-gap ledgers ...")`；
    空白残留不参与权威性判断，也不阻塞从非空残留恢复。
  - `_rebuild_from` 用真实 DDL 重建并保留 `run_id` / `session_id` / `resolved_at`（缺失才补
    unknown），保证结构不完整的历史残留也能读出来；重建目标与源表不同名，避免删掉源表。
  - 删除因本次改动而失去引用的 `_TABLE_V2_REBUILD`（只清理自己造成的孤儿）。

### (3) 新增反例/故障注入测试
`collector/tests/test_collector_outbox.py` 新增 `_make_legacy_v2_leftover(...)`（用**旧版本真实
表名与 DDL**，不是新代码自己的临时表）+ 6 个用例：真实元数据恢复、结构残缺补 unknown、
多个非空残留保留并报错、空白残留不阻塞、残留行数多于正式表时不许删、恢复后 `user_version`
与重复打开一致。

### (4) 实际命令与结果
```powershell
python -m pytest collector/tests/test_collector_outbox.py -q -k "legacy_v2 or ambiguous or leftover or user_version"
#   撤销修复：6 failed, 1 passed, 37 deselected
#   修复后：  7 passed, 37 deselected
python -m pytest collector/tests/test_collector_outbox.py -q      # 44 passed
python -m pytest collector/tests -q --ignore=collector/tests/test_opcua_driver.py
#   81 failed, 506 passed, 24 skipped —— 与改动前基线同一集合（逐个名字比对无差异）
```
其中 `collector/tests/test_collector_receipt_v2.py::test_outbox_owner_lock_rejects_second_process_and_crash_releases`
单独用"只回退 `durable_redis_writer.py`"的 A/B 复核过：**不带**本次改动同样失败（子进程
stdout 为空），属既有环境问题，非本轮回归。

### (5) 原始证据
上述命令输出；修复前的复现记录（`available=True`、正式表 0 行、`gaps(run-real)=0`、
`__v2` 仍有 3 行）见本轮终端输出。

### (6) 边界
已闭环：历史 `__v2` 残留可恢复、真实元数据保留、歧义/冲突不静默丢数、重复打开一致。
未验证：真实现场库里可能存在的**其它**历史表名（本轮只覆盖代码里出现过的两种 + 重建表）。

### (7) 下一步
若现场还有更早的自定义表名，建议先只读导出 `sqlite_master` 清单再决定是否扩展白名单——
在白名单之外盲目猜测表名会带来误删风险。

---

## 任务 3：全链路验收口径统一

### (1) 复现与根因
`[源码确认]` 旧 `scripts/validate_docker_pipeline.py`：历史值只查 `p1`/`pN`；质量常量 192；
`latest_correct` 只看点 1 的时间戳；不同设备生成完全相同的值序列（跨设备误归属查不出来）；
`steady_state_passed` 主要看生产结束时的 outbox 年龄/条数；直接构造 `StreamConsumer` 与生产
`writer/main.py` 启动不等价；两个脚本各自定义"通过"。

### (2) 改动文件与关键实现
- **新增 `data_watch_shared/acceptance.py`**（唯一规则源）：三态判定 `pass|fail|not_tested`、
  `verdict()`/`not_tested()`/`overall_status()`（**未测不得折算成通过**）、
  `correctness_verdict()`；期望模型按 **(设备, 点位, 采样序号)** 生成（`device_seed` 用 crc32，
  设备间不撞车），覆盖 NULL / 布尔 / 负值 / 质量变化 / 非存储 / STRING / 时有时无的 NULL；
  `build_frame()` 同时给出 `stream_values`/`stream_qualities`/`latest`；
  `reconcile_value_rows()` / `reconcile_quality_rows()` 做身份、重复、遗漏、逐单元格对账；
  `window_analysis()` 按阶段分窗、速率用**总增量÷总时长**、趋势只看尾段（台阶 ≠ 持续增长）。
- **重写 `scripts/validate_docker_pipeline.py`**：真实链路 合成输入 → Collector SQLite 持久队列
  → Redis Stream → Writer → MySQL 映射 → TDengine；各项独立判定：`correctness`、`mapping_update`、
  `latest`、`verifier_negative_controls`、`steady_state`、`latency`、`recovery`，`margin` 显式
  记 `not_tested` 并指向任务 4 的脚本周。运行中把新点位映射写进 MySQL（提交后再发该点），
  逐帧核对"新列从此刻起正确、写入前必须为 NULL"。延迟给出**构成拆分**（提交/AOF 批/写入端
  flush/TDengine 执行）。排空完成用"已持久提交 == 已确认投递"的计数器不变量判定，并要求
  已领取未确认在途为 0。
- **`scripts/validate_collector_ingest_capacity.py`** 改用同一份共享规则（不再有第二份
  "通过"定义），并新增 `measured_status`（本次实际尝试项）与严格 `overall_status`。

### (3) 新增反例/故障注入测试
`scripts/test_pipeline_acceptance.py`（20 个）与 `scripts/test_capacity_margin_verdicts.py`（23 个）
共 43 个；关键反证：
- 故意改中间点、把期望值改成 NULL（双向）、点与列错开一位、跨设备搬行、重复行、缺列 →
  校验器必须报出（正常数据必须无问题，作为对照）；
- 质量写成恒定 192、质量行重复/遗漏 → 必须报出；
- `overall_status` 不得把 `not_tested` 折算成通过；
- 窗口聚合必须是总增量÷总时长（不能对窗口速率取算术平均）；
- 趋势：台阶式抬升后持平 → `flat`；持续上升 → `growing`；持续下降 → `shrinking`；
- 峰值类指标只取本阶段（预置积压阶段不得污染"生产期最大积压"）。

### (4) 实际命令与结果（真实集成测量）
```powershell
docker run --rm --name dwv-t34a-pipeline6 --network dwv-t34a --cpus 4 --memory 4g `
  -v "D:\python\device_alarm\data_watch:/workspace" -w /workspace `
  data-watch-pipeline-runner:20260925 python /workspace/scripts/validate_docker_pipeline.py `
  --devices 10 --points 100 --seconds 60 --period-ms 100 --drain-seconds 300 `
  --point-batch 100 --latest-devices 3 --mapping-update-fraction 0.5 `
  --output /workspace/outputs/recheck-20260926-task34/pipeline-10dev-100pt-final.json
```
负载：10 设备 × 100 点 × 100ms × 60s（6000 帧，约 100 帧/秒）。结果：

| 判定 | 状态 | 关键数字 |
| --- | --- | --- |
| correctness | pass | 值行 6000/6000；质量行 583800/583800；超级表 100 列；问题数 0 |
| mapping_update | pass | 第 300 tick（t=30.013s）插入点 101 映射；前 300 tick 全 NULL、后 300 tick 逐格正确 |
| latest | pass | 值/质量/时间戳/点集四项一致，无多余字段 |
| verifier_negative_controls | pass | 4/4 被抓：p53 实际 3916 vs 期望 16261；期望 NULL；错映射 p3 实际 −3000 vs 1.0；质量 192 常量 vs 实际 0 |
| steady_state | pass | 趋势 shrinking（四段均值 117→118→121→107），生产期最大积压 130 帧 / 最老 1247ms |
| recovery | pass | outbox=0、waiting_writes=0、已领取未确认=0、PEL=0、lag=0、buffer=0、inflight=0、DLQ=0 |
| **latency** | **fail** | 采样→TDengine 提交确认 P99 **1500ms** > 预算 1000ms；构成：提交 P99 27.2ms、AOF 批 p50 1000ms / p99 2000ms、写入端 flush P99 50ms、TDengine 执行 P99 20ms |
| margin | not_tested | 不在本脚本范围（见任务 4） |

`overall_status=fail`、`measured_status=fail`（仅由 latency 触发）。

### (5) 原始证据
`outputs/recheck-20260926-task34/pipeline-10dev-100pt-final.json`（完整样本序列 + 判定 + 反证明细）
与同名 `-console.log`。

### (6) 边界（覆盖差异，必须区分）
- 本脚本是**核心组件集成**形态：直接构造 `DurableRedisWriter + StreamConsumer`，不经过
  collector 设备驱动的分组/过滤（改由脚本按共享模型模拟），也**不经过 `writer/main.py`
  生产启动入口**（信号、多 worker、consumer group 初始化、日志/指标装配）。
  生产入口形态的覆盖由 `writer/tests/test_main_workers.py` 与部署冒烟负责，本脚本通过
  **不代表**生产启动形态已验证。
- 延迟统计的是"采样时刻→提交确认"，不是采样间隔；`latest`（实时展示）是独立快车道，
  本轮其 Redis 往返 P99 = 10ms，但**浏览器接收→应用→绘制**仍未测量。
- 未验证：额定规模（100 设备 × 1000 点）长测、可靠移交恢复、查询侧完整性过滤、宽帧提速。

### (7) 下一步（按优先级）
1. 延迟预算立项：`WAITAOF 1 0 2000` + `appendfsync everysec` 决定了下限，需要业务确认
   "历史落库"能接受 ~1.5s 还是必须收紧（可选：改用 `appendfsync always`、缩短确认窗口、
   或把实时展示完全交给 `latest` 并把历史 SLA 单独定义）。
2. 把 `correctness/mapping_update/latest/verifier_negative_controls/recovery/steady_state`
   接入常规回归（当前是脚本，尚未进 CI）。

---

## 任务 4：真实受控积压与额定余量

### (1) 复现与根因
`[源码确认]` + `[真实集成测量]` 上一轮 `margin-20dev-500pt-70pct.json` 的
`seeded_backlog.seconds=0.0`、`produced_frames=0`、`outbox_frames=320`：预置循环的条件是
"本地队列 < 目标帧数"，但回放线程从 `ping()` 起就一直在消费本地队列，条件一开始就不成立，
所谓"320 帧预置积压"其实只是**正常生产的尾部积压**。追平档又按额定输入的 70%（140 帧/秒）
运行，把净消减 6 帧/秒 ÷ 额定 200 帧/秒 当成"余量 3%"——输入被降了 30%，这个比值不是余量。

### (2) 改动文件与关键实现
- `collector/storage/durable_redis_writer.py`
  - `ping(start_replay=True)`：生产默认行为不变；
  - `pause_replay(timeout_s)` / `resume_replay()`：**同步**暂停——置位后等回放线程回到暂停点
    （`_replay_idle`），确保"在飞的那一批 WAITAOF"已经结束，否则预置积压数字会被回放吃掉。
- `scripts/validate_collector_ingest_capacity.py`
  - 阶段拆成：`production`（回放正常跑）→ 暂停回放 → `seed`（按正常持久接收路径预置有界积压，
    并记录 `replay_advanced_during_seed` 作反证）→ `rated_catchup`（恢复回放与额定输入**同时**
    开始，积压耗尽即停表）→ `post_stop_drain`（停采排空，单独判定）；
  - 判定项：`seeded_backlog`、`rated_catchup_margin`、`degraded_recovery`、`post_stop_drain`
    各自独立；`degraded_recovery` 只在 `--catchup-rate < 1` 时产生，且**绝不**参与额定余量；
  - 能力 = 窗口内实测确认帧数 ÷ 窗口时长；余量 = 能力 ÷ 额定输入 − 1；只看可处理积压
    （`outbox.count`），已领取未确认（`提交−已确认`）与本地在途写单独记录；
  - 有效区间不足（积压被太快处理完）→ `not_tested`，**不判为无余量**；
  - `measured_status`（本次实际尝试项）与严格 `overall_status` 分开，退出码看前者。

### (3) 新增反例/故障注入测试
`scripts/test_capacity_margin_verdicts.py`（23 个）覆盖：额定余量 ≥30% 才 pass、<30% fail、
输入未达额定即 fail、有效区间过短一律 `not_tested`、没有可处理积压 `not_tested`、
降速档存在时额定余量必须 `not_tested`、降速档单独命名且标注"不作为额定余量"、
预置积压未达标 fail、**暂停未生效（预置期间回放又前进）fail**、稳态与排空互不影响、
`attempted_verdict_names` 只统计实际跑过的档位、`pause_replay` 的同步语义与未启动时返回 False。

### (4) 实际命令与结果（真实集成测量）
```powershell
# 200 点档（20 设备 × 200 点 × 100ms = 200 帧/秒额定，容器 2 CPU / 2 GiB）
... --devices 20 --points 200 --seconds 20 --period-ms 100 --catchup-seconds 60 `
    --catchup-rate 1.0 --seed-backlog-frames 6000 --min-effective-seconds 5 ...
# 500 点档：仅把 --points 改为 500
```

| 判定 | 200 点档 | 500 点档 |
| --- | --- | --- |
| seeded_backlog | pass：预置 5860 帧达成 6000/6000，29.3s，暂停生效（预置期间回放前进 **0** 帧） | pass：6000/6000，30.0s，前进 **0** 帧 |
| rated_catchup_margin | **pass：能力 296.67 帧/秒，余量 48.3%**，积压 6000→200，有效区间 true，已领取未确认 0 | **pass：能力 279.08 帧/秒，余量 39.5%**，积压 6000→1255，有效区间 true，已领取未确认 0 |
| sustained_replay | **fail：196.58/200 = 0.983** | **fail：188.45/200 = 0.942** |
| steady_state | pass：趋势 flat，最大积压 390 帧 | **fail：趋势 growing，最大积压 480 帧** |
| post_stop_drain | pass：outbox=0，2.0s | pass：outbox=0，2.4s |
| degraded_recovery | not_tested（本轮未跑降速档） | not_tested |
| overall / measured | fail / fail（由 sustained_replay 触发） | fail / fail |

对照旧结论：旧报告"净消减 6 帧/秒、余量 3%"→ 现在同一 20 设备档在**额定输入**下测得
能力 279~297 帧/秒、余量 **39.5%~48.3%**。旧数字既测错了区间（只有尾部积压）、又算错了
分母（用降速后的净消减比额定输入）。

### (5) 原始证据
`outputs/recheck-20260926-task34/capacity-20dev-200pt-rated-final.json`、
`capacity-20dev-500pt-rated-final.json`，以及两轮失败中间态
（`capacity-20dev-200pt-rated.json`：回放全程暂停；
`capacity-20dev-200pt-rated-pause-fix.json`：只置位暂停、`replay_advanced_during_seed=200`）。
旧证据 `outputs/performance-20260925/margin-20dev-500pt-70pct.json` 未被改动或覆盖。

### (6) 边界
- 已闭环：受控积压真形成、暂停可验证、额定/降速/排空三类结论互不顶替、余量按额定输入测得。
- 本轮新增待办：**额定输入下生产期确认速率略低于输入**（200 点档 98.3%、500 点档 94.2%），
  积压以约 2~12 帧/秒缓慢累积。机制 `[未验证推断]`：每秒一批的 AOF 节奏下，批间固定开销
  （Lua 执行 + SQLite 认领/删除）占比随每帧点值数上升而变大，于是稳态吞吐略低于输入；
  同轮实测的"有积压时能力 279~297 帧/秒"支持"总量能力足够、是节奏问题"这一解释。
- 未验证：额定规模（100 设备 × 1000 点）长测、30% 余量的长时冻结版本冻结验证、
  降速恢复档（本轮未跑，判定如实为 `not_tested`）、真实硬件断电。

### (7) 下一步
1. 跑一轮 `--catchup-rate 0.7` 的降速档，把 `degraded_recovery` 从 `not_tested` 补成实测；
2. 针对"额定输入下 1.7%~5.8% 稳态缺口"做定向实验：把 AOF 批量上限/节奏（例如允许跨 fsync
   的多批流水、或把 `WAITAOF` 与认领解耦）作为变量，确认是否可消除累积漂移；
3. 长测（≥30 分钟）在同一负载下确认积压是否稳定在常数水平。

---

## 5. 测试与回归汇总

| 范围 | 命令 | 结果 |
| --- | --- | --- |
| 新：映射重载 | `python -m pytest writer/tests/test_consumer_mapping_refresh.py -q` | 6 passed（撤销修复 4 failed/2 passed） |
| 新：共享验收规则 | `python -m pytest scripts/test_pipeline_acceptance.py -q` | 20 passed |
| 新：容量判定口径 | `python -m pytest scripts/test_capacity_margin_verdicts.py -q` | 23 passed |
| 全部脚本测试 | `python -m pytest scripts -q` | 61 passed |
| Writer 套件 | `python -m pytest writer/tests -q` | 182 passed |
| Collector 改动相关 | `python -m pytest collector/tests/test_collector_outbox.py collector/tests/test_collector_latest_compensation.py collector/tests/test_collector_receipt_v2.py -q` | 59 passed, 20 skipped, 1 failed（既有：子进程 owner-lock 用例，A/B 复核与本轮改动无关） |
| Collector 全量 | `python -m pytest collector/tests -q --ignore=collector/tests/test_opcua_driver.py` | 81 failed / 506 passed / 24 skipped（与改动前基线同一失败集合） |

注意：`python -m pytest scripts writer/tests` 在同一次会话里会在收集阶段报
`ImportError: cannot import name '_connect_mapper_connections' from 'main'`——
`scripts/` 下有测试把 `collector/` 插进 `sys.path`，遮蔽了 `writer/main.py`。
**该问题与本次改动无关**（把我的两个新测试文件挪走后同样复现），因此两个测试根按仓库惯例分开跑。
`collector/tests/test_opcua_driver.py` 在宿主机 Python 3.14 上因 vendored `cryptography`
缺 `_rust.x509` 无法收集，排除后运行。

## 6. 证据位置与复现

- 新证据目录：`outputs/recheck-20260926-task34/`（含 `README.md` 记录环境、镜像、命令）
- 旧证据：`outputs/performance-20260925/` **未改动、未覆盖**
- 环境：网络 `dwv-t34a`；`redis:7.4`（`appendfsync everysec`、`noeviction`，4C/6G）；
  `tdengine/tsdb-amd64:3.4.0.9`；`mysql:8.4.11`；镜像
  `data-watch-pipeline-runner:20260925`、`data-watch-collector-test:20260925`（Python 3.14t）。
  本轮结束已删除全部自建容器与网络；`MyRedis`/`MyTdengine`/`MyMySQL`/`data-watch-backend`
  仍为 Up 8 hours，未被触碰。

## 7. 仍未闭环的边界（不得当成已完成）

1. **延迟 SLA 未达标**：历史落库 P99 1500ms > 1000ms（本轮新测出），需业务定标后处理。
2. **额定输入下的稳态确认速率缺口**（98.3% / 94.2%）：机制解释仍是推断，需定向实验。
3. **每帧分段结果与逻辑帧身份、错误码**：仍未实现。
4. **DLQ 可靠移交恢复论证**（响应丢失、重启故障注入）：未做。
5. **查询侧完整性过滤/提交标记**：未做。
6. **浏览器接收→数值应用→绘制延迟**：未做（本轮只到 Redis/latest 往返 10ms）。
7. **额定规模（100 设备 × 1000 点）冻结版本长测与 30% 余量冻结验证**：未做。
8. **宽帧提速**：未做。
9. **降速恢复档实测**（`degraded_recovery` 仍为 `not_tested`）。
