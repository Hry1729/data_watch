# Data Watch 性能与可靠性修复报告（2026-09-25）

> ## ⚠️ 本报告已被 `docs/performance-review-remediation-20260925.md` 更正，请以该文为准
>
> 本报告撰写于独立审查之前，其中以下结论**已被撤回或修正**，不要再引用：
>
> 1. **"跳过历史回放中重复的 latest 段"这一优化存在正确性漏洞，已整体停用**（回退到基线）。
>    组级最大时间戳**不能**证明帧内每个点都已被覆盖：同组不同 tick 点集可不同、各点可带各自时间戳、
>    快车道会按组延后条目，于是同组另一批点的成功帧会把水位推高，被延后的那批点再也不会被历史补齐。
>    本报告中"Lua CPU −47%、回放 ×1.51"等收益声明因此**一并作废**。
> 2. **"一档都没到"已撤回**。该判断来自一个算错的帧数公式
>    （每帧有 3 个点不进历史 Stream，却仍除以标称点数）。
>    按修正后的独立逻辑帧计数重跑：**10 点档 1.034、100 点档 1.025，均达到额定输入**。
> 3. **"余量为零"已撤回**，正确说法是**"尚未验证 30% 余量"**。
> 4. **"Lua CPU 省 47%"口径混用**：正确数字是历史 EVALSHA 部分 −46.6%、全部 Lua −37.4%，
>    且两次运行工作量不等量、计时在生产结束后采集，不能换算成"稳态占用 0.85 个核"。
> 5. **对旧报告"198 帧/秒"的纠偏过头**：它是"含排空的整体完成吞吐"，
>    旧报告本身与 SQLite 接收速率分列，并未混为一谈；只是不能单独用来验收持续容量。
> 6. **"快车道溢出/失败时不写水位，那些帧仍会走全量执行"是错的**，详见第 1 点。
> 7. **"本轮没有引入新的故障面"已撤回**：receipt 状态与历史补偿条件都被改过，必须重跑故障恢复。
> 8. **"接收侧独立跑不动"限定**：该数字来自自造数据的测试脚本，未经真实采集执行器，
>    不能证明生产 Collector 接收模块的独立上限。
> 9. **"Redis 单实例就是天花板"限定**：串行执行是架构约束，但不能表述为"Redis 已无优化空间"。
> 10. **交付状态**：本报告"未提交、未推送"已失效；本文件及其证据已提交推送。
>
> 仍成立的部分：帧宽扫描曲线、Writer 三处问题的存在性、1 GiB 本地队列上限、
> 故障恢复与浏览器延迟未验证。但 Writer 的描述需按更正文档收窄。

基线提交：`a03c951c621709205518b95f141a4fff728fd108`（`main`）。
本报告只覆盖本轮实际做过的诊断、修改与测量；未做提交或推送（需另行授权）。

- 诊断全文（数据链路、逐条核实、重复处理与边界、分阶段计划）：`docs/performance-diagnosis-20260925.md`
- 原始结果：`outputs/performance-20260925/`
- 上轮报告更正说明：`docs/performance-optimization-validation-20260924.md` 顶部

---

## 1. 确认的根因

### 1.1 已由代码确认（不是推断）

| # | 根因 | 位置 |
| --- | --- | --- |
| R1 | 历史回放 Lua 对每个点做 `HMGET` → `cjson.decode` → 比较 → `cjson.encode` → `HSET`，1000 点即每帧最多 1000 次 HMGET + 2000 次 cjson 调用 | `collector/storage/durable_redis_writer.py` 的 `FRAME_LUA`/`FRAME_LUA_V2` |
| R2 | 这段工作在稳态下是重复劳动：实时快车道已用同一批点、同一 `t` 写过 latest，历史帧再读出来比较一遍，几乎永不写 | 快车道 `LATEST_LUA` 与历史脚本写同一 `rt:latest` |
| R3 | Redis 单线程执行 Lua，`stream_shard_count` 只增加 key 数量，不增加执行实例 | `data_watch_shared/stream_envelope.py:15-22` |
| R4 | 测试判定失真：`target_met` 只表示"输入按时提交完成"，与容量无关；`replay_frames_per_second_total` 用"已回放帧数 ÷ 生产+排空总时长"，其近似等于提交速率（生产期内只回放约 880/9900 帧） | `scripts/validate_collector_free_thread.py`（旧脚本，未改动，保留追溯） |
| R5 | 旧测试的正确性检查只覆盖每帧少数点位，未覆盖全部历史字段 | 同上；`scripts/validate_docker_pipeline.py` 只查 `p1`/`pN` 且质量恒为 192 |
| R6 | Writer：一条永久 RETRY 行留在 buffer 队首，触发整 worker 循环退避最多 10s，并占用容量抑制该 worker **所有** stream 的读取；不同 stream 的行还会被打进同一条 SQL | `writer/consumer.py:1249`、`:478-493`、`:222`、`:293`；`writer/tdengine_writer.py:870-887` |
| R7 | Writer：PEL 回收路径不传 `prepared`，JSON/float 解析落在 4 个 worker 共享的事件循环上 | `writer/consumer.py:583`、`:667`、`:830`、`:929-942` |
| R8 | Writer：一条逻辑帧被拆成数值+质量多段时，多段 BAD_DATA 会被降级为 RETRY，永远进不了 DLQ | `writer/tdengine_writer.py:618` |
| R9 | 后端对 `rt:latest` 做 `HGETALL` 后逐字段 `json.loads`；往 latest 哈希里写任何非 JSON 字段都会让该设备 latest 读取整体失败 | `backend/app/services/monitor.py:149-153`（本轮实现水位时据此确定存放位置） |
| R10 | Collector 本地队列有 1 GiB 上限（`PRAGMA max_page_count = 1000*1024*1024/page_size`），触顶时**拒绝新帧**而不是写满磁盘 | `collector/storage/collector_outbox.py:172`；本轮实测在 100 设备×1000 点、积压 955.8MB 时复现 `database or disk is full` |

### 1.2 已由本轮实测支持

- **Lua 逐点 latest 段约占宽帧 Lua 执行时间的 45%**：100 设备×1000 点，同样的 15 秒额定输入，
  该段从执行路径移除后 Redis 侧 `EVALSHA` 累计 CPU 由 **16.44s 降到 8.78s（-47%）**，
  回放确认速率由 **177.7 升到 268.3 帧/秒（×1.51）**。见 §4。
- **回放能力存在最优帧宽**：100 设备下生产期内回放确认速率在每帧 100 点时最高（905 帧/秒），
  到 500 点骤降到 182 帧/秒。窄帧被每帧固定成本拖累，宽帧被逐点成本拖累（§4 曲线）。
- **100 设备×1000 点时接收侧本身也不达标**：2 CPU 容器内输入速率只有 639–656 帧/秒，
  连 1000 帧/秒的额定输入都产生不出来。该档有**两个**独立瓶颈。

### 1.3 上轮审查意见的核实结论（不照单全收）

| 审查项 | 核实结论 |
| --- | --- |
| W03 多 Stream COUNT 误当全局容量 | **已修复**，`consumer.py:289 _next_read_scope` 分配全局剩余容量；不再复现 |
| W05 动态发现与 flush 并发访问缓冲区 | **已不是问题**：`consumer.py:1153-1154` 在任何 await 之前完成 owner 交换，`_flush` 有 `:1144 _flush_lock`；发现任务只往 `_queued_stream_keys` 追加（`main.py:198`）。审查报告自己的结论也是 NOT CONFIRMED |
| W06 每 worker 指标互相覆盖 | **已不是问题**：`main.py:588-589` 每分片独立 metrics，`:395-397` 汇总求和 |
| C02 receipt 7 天 key 膨胀 | **已修复**：改为 session 维度 `high` 水位 + 最多 1024 个乱序间隙（`FRAME_LUA_V2:82-111`） |
| W01 SQL 字节预算 | **已修复**且本轮未改动；上轮实测最大 SQL 3120248 < 3145728 字节 |
| W04 4 个 worker 不是 4 个 CPU 执行体 | **确认**：`writer/main.py:599-604` 是同一事件循环的 4 个协程，SQL 组包共用一个全局 4 线程池（`tdengine_writer.py:42`） |
| W06 失败隔离 | **确认未修复**（见 R6/R7/R8） |
| R01 实时通知 + UI 解耦 | **已修复**：`rt:change` 三路径都发布；数值固定 100ms、图表按用户设置（默认 250ms）解耦 |
| "实时 108ms 达标" | **不能算显示延迟达标**：代码里没有任何"发送完成→浏览器应用→绘制"的计时（`ws_push.py:160-162` 未计时，前端无 `performance.now`）。108ms 只是 WebSocket 收包 |

---

## 2. 本轮实际修改

一次只改一件事。除 P0 测试与文档外，生产语义只动了一处。

### 2.1 注入缺失的口径指标（P0）

- `collector/storage/collector_outbox.py`：`_commit_batch` 增加 `durable_frames_accepted` 计数器。
- 理由：`stream_points_written` 的口径是"**已回放到 Redis 的点值数**"
  （`durable_redis_writer.py` 的 `write_durable_frames`）；durable 路径覆盖了 `write_window_stream`
  且不调用 `super()`，所以 `redis_writer.py:209` 的本地累加在该路径上不生效。
  结果是旧脚本用 `stream_points_written // points` 当作"已确认帧数"，
  实际上它统计的是回放，不是本地持久接收。两者必须分开。
- 验证：`collector/tests/test_collector_outbox.py::test_local_acceptance_and_redis_replay_are_counted_separately`。

### 2.2 历史回放跳过"已被快车道确认覆盖"的 latest 段（P1，唯一的语义相关改动）

- `collector/storage/durable_redis_writer.py`：
  - `LATEST_LUA`（快车道）：写 latest 成功后，在 **receipt 哈希**（`KEYS[2]`）里记下
    `l:<device>:<group>` = 本次写入的最大采样时间。水位与 latest 在同一次 EVAL 内，
    不存在"水位已推进但 latest 未写"的中间态。
    **水位没有写进 latest 哈希**——理由见 R9。
  - `FRAME_LUA_V2`（v2 历史）：先算 `frame.ts`，若存在水位且 `frame.ts <= 水位`，
    跳过逐点 latest 段；否则**原样执行**。
  - `FRAME_LUA`（旧 UUID 协议）：**完全没有改动**，仍是 4 键布局，永远执行全量 latest 段。
  - `_queue_latest`：增加会话维度。会话不同（重装/回滚）时不把两个 session 的点值合并进
    同一份载荷，避免水位代表混合状态。
  - 快车道恢复：重启时先从 Redis 取回本会话已确认水位，避免重复写、也避免水位从 0 重推。
- **为什么可以跳过（不属于放松可靠性）**：
  1. 快车道每次入队的是该帧**全部点**（`executor.py:496-503`），是历史段所写点集的超集；
  2. 两者携带同一 `t`，所以逐点守卫的判定等价；
  3. 跳过只在"水位存在且不早于本帧"时发生；水位缺失（Redis 被清、首次启动、快车道从未成功、
     旧协议）一律回退全量执行；
  4. 快车道溢出/失败时**不写水位**，那些帧仍会走全量 latest 段；
  5. 历史回放本身仍是权威路径，SQLite 保留与 receipt 去重语义**完全未动**。
- 测试：`collector/tests/test_collector_latest_watermark.py`（5 个假 Redis 语义用例 +
  2 个真实 Lua 用例 + 2 个会话/恢复用例）。真实 Lua 用例覆盖：
  水位覆盖→跳过但 stream/window 照旧；更新的帧→真写 latest；
  水位缺失→回退；latest 哈希不得混入 `l:` 字段；旧协议始终写 latest；receipt `high` 仍正确推进。

### 2.3 测试脚本（P0）

- 新增 `scripts/validate_collector_ingest_capacity.py`，取代旧脚本的判定部分：
  - 六项独立判定：`input_on_schedule` / `durable_received` / `sustained_replay` /
    `steady_state` / `data_correctness` / `catchup_margin`，每项 pass | fail | not_tested；
    退出码只在**全部通过**时为 0，不再用 `target_met` 暗示容量达标。
  - **持续回放**在额定输入仍在进行时统计（`replayed_fps_during_production`），
    不再只看停采后排空。
  - 连续窗口给出输入速率、回放确认速率、outbox 条数与最老年龄，并给出
    `growing / shrinking / flat` 趋势；过短窗口（<0.2s）直接丢弃，避免采样抖动变成天文速率。
  - `--catchup-seconds`：保持额定输入观察旧积压是否持续减少。
  - 测试数据由 (device, point, tick) 共同决定，覆盖 NULL、布尔、负值、质量码
    变化（GOOD/UNCERTAIN/BAD）与恢复、非存储点、STRING 点；抽样帧做**全字段**对账
    （envelope 字段 + 全部 values + 全部 qualities），另对抽样设备做 latest 全点位与质量核对。
  - 默认**保留**证据；`--cleanup` / `--cleanup-always` 才删除本次唯一 token 的键。

### 2.4 文档更正（P0）

- `docs/performance-optimization-validation-20260924.md` 顶部加更正说明：
  原文"全部正确/核对通过"的实际检查范围是行数、身份、每帧 2 个点值、latest 全点；
  未覆盖 NULL/布尔/负值/非存储点/STRING 点/质量变化/点位映射，且两个脚本都未经过
  `executor.py` 的过滤代码。**历史数字与原始 JSON 一字未改。**

### 2.5 未改动

`writer/`、`backend/`、`frontend/` 全部未动。旧脚本 `validate_collector_free_thread.py` 保留原样以便追溯。

---

## 3. 回归

环境：`data-watch-collector-test:20260925`（Python 3.14t、GIL 关闭、
msgspec 0.21.1 / redis 8.0.0 / pytest 8.4.2 / pytest-asyncio 1.2.0），
digest `sha256:2a6845f986ad9d51fb5903e2b8f32752d3e86163558f95fe0e5419df2c7b17af`。

| 范围 | 基线 `a03c951` | 本轮改动后 |
| --- | ---: | ---: |
| Collector 测试套件（排除 4 个需 Windows 驱动依赖的模块） | 97 failed / **437 passed** / 18 skipped | 97 failed / **447 passed** / 18 skipped |

- 两次运行在**同一个容器镜像、同一套依赖、同一次会话**内完成；基线代码是把工作区源码复制一份、
  再用 `git show a03c951:<path>` 还原三个被改文件、并删除新增测试文件后得到的，因此是可比的。
- 那 97 个失败在**基线代码上同样失败**，原因是 `packages/*/driver.toml` 声明 `os=('windows',)`
  而容器是 Linux（`data_watch_driver_manifest.py:216`），另有 `cryptography` 的 Linux 编译扩展缺失；
  与本轮改动无关。
- 新增通过 10 项 = 水位与口径回归用例 10 个；
  **失败集合完全一致，没有任何回归**。
- 存储专项（`test_collector_outbox` + `test_collector_latest_watermark` +
  `test_collector_receipt_v2` + `test_redis_writer`）：**61 passed，18 skipped**。
  其中 `test_collector_latest_watermark.py` 的 2 个真实 Lua 用例在真实 Redis 上执行（非 mock）。
- Writer / backend / 前端本轮未改动，未重跑（上轮结果仍适用）。

---

## 4. 前后对照（固定环境）

**环境**：专属网络 `dw-perf-20260925`；Redis `dw-perf-redis-20260925`（`redis:7.4.9`，
AOF `appendfsync everysec`，`maxmemory 4gb` + `noeviction`，4 CPU / 6 GiB）；
Collector 容器 2 CPU / 2 GiB；未启动 TDengine / Writer / 前端。
负载恒为 100 设备、100ms 采样；输入均约 1000 帧/秒（1000 点档除外，见下）。
`--drain-seconds` 足够大，排空时间即真实排空时间。所有运行 `data_correctness=pass`、`duplicate_frames=0`。

### 4.1 帧宽扫描（改动前）

| 每帧点数 | 输入帧/秒 | 生产期内回放确认帧/秒 | 回放/输入 | 停采后排空 | 积压趋势 | 窗口内最大积压 |
| ---: | ---: | ---: | ---: | ---: | --- | ---: |
| 10 | 999.7 | 648.5 | 0.648 | 2.0s | shrinking | 1989 |
| 50 | 999.8 | 870.9 | 0.871 | 2.2s | shrinking | 1836 |
| 100 | 999.7 | 905.0 | 0.905 | 1.2s | shrinking | 2100 |
| 200 | 999.7 | 824.8 | 0.825 | 2.7s | growing | 3736 |
| 500 | 999.6 | 182.5 | 0.183 | 25.4s | growing | 12244 |
| 1000 | 639.7 | 177.7 | 0.178 | 28.5s | growing | 6997 |

### 4.2 单因素对照：只加"跳过重复 latest 段"

> **本节数据对应的实现已停用，仅作历史记录，不得作为收益声明。**
> 该优化存在正确性漏洞（见更正文档第一节），停用后回放能力回落到基线水平。
> 下方"Lua CPU"为 EVALSHA 与 EVAL 的合计执行计时；更正后的正确口径见更正文档第四节。

同样的输入、同样的容器与 Redis，只改变这一处代码：

| 每帧点数 | 时长 | 回放确认帧/秒 前 → 后 | 变化 | Redis Lua CPU（EVALSHA+EVAL） 前 → 后 | 变化 |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 100 | 15s | 905.0 → 872.8 | −3.6% | 7.77s → 6.27s | −19% |
| 200 | 15s | 824.8 → 908.0 | **+10.1%** | 11.99s → 10.00s | −17% |
| 500 | 15s | 182.5 → 416.6 | **×2.28** | 28.12s → 14.96s | **−47%** |
| 1000 | 15s | 177.7 → 268.3 | **×1.51** | 20.28s → 12.68s | −37% |
| 200 | 60s | （未测）→ 955.9（趋势 flat） | — | — | — | 每帧 1000 点档：253.4 帧/秒（30s，趋势 growing） |

解读：

- 窄帧（100/200 点）收益在噪声范围内，**不能声称提速**；该档瓶颈本来就不在 Lua 逐点段。
- 宽帧收益显著且方向一致：**Lua CPU 几乎减半，回放速率提升 1.5–2.3 倍**。
- **但没有达标**：100 设备×每台 1000 点在 2 CPU 下，回放只有约 253–268 帧/秒，
  且输入侧也只能产生约 640–656 帧/秒。该档仍不满足 1000 帧/秒持续输入。

### 4.3 当前容量结论（固定环境，非目标机上限）

> **本表已作废，请使用更正文档第三节 3.3 的重跑结果。**
> 本表的回放帧数由错误的换算公式得出（每帧 3 个点不进历史 Stream），
> 系统性低估完成率：例如 10 点档真实完成率 100% 时本表最高只能显示 70%。
> 按修正后的独立逻辑帧计数重跑：**10 点档 1.034、100 点档 1.025（均 pass）**，
> 200 点档 0.941、500 点档 0.228（fail）。
> 因此本表"一档都没到"与"余量为零"的结论**均已撤回**。

| 配置 | 达到额定输入 | 稳态有界 | 持续回放余量 | 判定 |
| --- | --- | --- | --- | --- |
| 100 设备 × 10 点 | 是 | 是（shrinking） | 无（0.65） | **不达标** |
| 100 设备 × 50 点 | 是 | 是（shrinking） | 无（0.87） | **不达标** |
| 100 设备 × 100 点 | 是 | 是（shrinking） | 无（0.87–0.91） | **不达标** |
| 100 设备 × 200 点（60s） | 是 | 是（**flat**） | 无（0.956） | **接近，未达标** |
| 100 设备 × 500 点 | 是 | 否（growing） | 无（0.42） | **不达标** |
| 100 设备 × 1000 点 | **否（接收侧仅 640–656）** | 否（growing） | 无（0.25） | **不达标** |

**没有任何一档达到"至少 30% 处理余量"的拟议验收目标。** 100 设备×200 点在 60 秒内趋势为 flat
且回放达到输入的 95.6%，是本机 2 CPU 下最接近达标的一档，但仍未通过 `sustained_replay`。

---

## 5. 验收状态（分项）

| 维度 | 状态 | 依据 |
| --- | --- | --- |
| 功能正确性 | **部分通过** | 本轮脚本对抽样帧做全字段对账（含 NULL/布尔/负值/质量变化/非存储/STRING/映射），全部 pass、无重复、latest 全点位正确。但抽样不是逐帧全量；TDengine 侧全字段对账仍未做 |
| 持续容量 | **未达标** | §4.3；最好一档（100×200×60s）回放为输入的 95.6%，无 30% 余量 |
| 故障恢复 | **本轮未新增验证** | 上轮 receipt v2 故障、TDengine 停机恢复、Redis 强杀 AOF 恢复的证据仍有效（`docs/receipt-v2-fault-validation-20260924.json`、`tdengine-fault-validation-20260924.json`）。本轮没有引入新的故障面，但**没有重跑**，也没有测硬件断电 |
| 浏览器体验 | **未验证** | 代码中不存在"发送完成→浏览器应用→绘制"的计时；上轮 108ms 只到 WebSocket 收包 |
| Writer 持续容量 | **未验证** | 本轮未改 writer，也未做独立注入测试；R6/R7/R8 已确认存在但未修 |
| 目标机 / 现场 | **未验证** | 全部为本机 Docker，非目标机上限；未接 PLC |

---

## 6. 复现命令

```powershell
# 环境（本轮创建，唯一命名）
docker network create dw-perf-20260925
docker build -f outputs/performance-20260925/collector-test.Dockerfile -t data-watch-collector-test:20260925 .
docker run -d --name dw-perf-redis-20260925 --network dw-perf-20260925 --network-alias perf-redis `
  --cpus 4 --memory 6g --label datawatch.test=performance-20260925 `
  redis:7.4 redis-server --appendonly yes --appendfsync everysec `
  --maxmemory 4gb --maxmemory-policy noeviction --auto-aof-rewrite-percentage 100 `
  --auto-aof-rewrite-min-size 256mb --save ''

# 容量验证（100 设备 × 200 点 × 60 秒）
docker run --rm --network dw-perf-20260925 --cpus 2 --memory 2g `
  -v "D:\python\device_alarm\data_watch:/workspace" data-watch-collector-test:20260925 `
  python3.14t /workspace/scripts/validate_collector_ingest_capacity.py `
  --devices 100 --points 200 --seconds 60 --period-ms 100 --drain-seconds 300 `
  --sample-every-ticks 10 --audit-stride 601 --latest-devices 3 --redis-memory-guard-mb 3000 `
  --cpu-limit 2 --redis-host perf-redis --allow-host 1 --cleanup-always `
  --outbox-dir /tmp --output /workspace/outputs/performance-20260925/repro.json

# 回归
docker run --rm --network dw-perf-20260925 `
  -e "PYTHONPATH=/workspace:/workspace/collector" `
  -e "DATAWATCH_TEST_REDIS_URL=redis://perf-redis:6379/14" `
  -v "D:\python\device_alarm\data_watch:/workspace" -w /workspace/collector `
  data-watch-collector-test:20260925 python3.14t -m pytest tests -q --no-header `
  --ignore=tests/test_config_db_integration.py --ignore=tests/test_config_db_scan_rate.py `
  --ignore=tests/test_config_reload_loop.py --ignore=tests/test_opcua_driver.py
```

代码版本 `a03c951` + 本轮工作区改动（`git status --short` 见 §2）。
镜像 digest `sha256:2a6845f986ad9d51fb5903e2b8f32752d3e86163558f95fe0e5419df2c7b17af`。

---

## 7. 尚未解决 / 下一步选择

1. **单实例 Redis 是硬天花板。** Lua 串行执行，每帧 1000 点的 Lua CPU 即使减半后仍需
   ~0.85 CPU 秒/秒。要支撑 1000 帧/秒，必须继续降低每帧 Lua 工作量或真正分片。
   下一步有数据支撑的两个方向：
   - **继续削减每帧 Lua 成本**：`ARGV[3]`（完整 frame JSON）在 Lua 里只用来取 `ts`，
     却要 `cjson.decode` 一个含全部点值的对象；改为直接传 `ts` 标量即可省掉这次解码。
     这是低风险、可单因素测量的一步。
   - **真分片**：多 Redis 实例。必须处理 session/seq/receipt/恢复归属——水位现在与
     receipt 同键，receipt 由本地 session 决定，跨实例分片必须回答"某 session 归哪个实例、
     恢复时谁负责重放"。**本报告不建议在没有这个设计前动手。**
2. **接收侧在 1000 点档也跟不上**（2 CPU 下 640–656 帧/秒）。需要单独 profile
   `executor._write_to_redis` 的 Python 侧成本，与 Redis 侧分开。此结论受 2 CPU 限制，
   目标机核数不同则不同。
3. **Writer 的三处已确认问题未修**（R6 坏数据阻塞整 worker、R7 回收路径在事件循环解码、
   R8 多段 BAD_DATA 降级为 RETRY）。修复前需要独立注入式验证，否则无法证明"局部失败不再拖住正常分片"。
4. **TDengine 侧全字段对账未做**。本轮只补齐了 Collector→Redis 的全字段对账。
5. **显示延迟未测**。需要在前端补埋点（接收→应用→绘制），并区分浏览器数值与图表。
6. **目标机与现场未验证**：真实 CPU/内存/磁盘、实际分组点数、安装包指纹、旧配置迁移、硬件断电。
7. **1 GiB 本地队列上限（R10）是一个运维风险**：按每帧 84KB 估算，
   100 设备×1000 点在持续积压约 2 分钟后就会开始**拒绝新帧**。当前实现会保留已有积压并记
   `outbox_rejected_frames`，但拒收的采样是否有显式缺口记录、现场如何告警，需要确认。

## 8. 环境与边界声明

- 未操作、停止或删除任何已有业务容器（`MyRedis`、`MyTdengine`、`MyMySQL`、`data-watch-backend`
  等本轮全程未触碰，也未读取现场 PLC 或业务数据库）。
- 本轮创建的测试对象均使用唯一命名与 `datawatch.test=performance-20260925` 标签，
  结束后按 §6 的 `--cleanup-always` 只删除本次 token 生成的键。
- 未降低 100ms 采样要求，未丢样、拒收、关闭 AOF、放松持久化或改变质量记录语义来制造达标结果。
- 未提交、未推送、未部署。
