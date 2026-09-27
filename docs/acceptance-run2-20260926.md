# Data Watch 隔离测试执行记录（2026-09-26 run2）

范围：按《Data Watch 测试准入复核（2026-09-26）》给出的顺序执行第 1–5 步。
**本记录不是容量验收，也不代表可部署**：30% 余量、故障矩阵、百台千点、生产入口与浏览器联合负载均未做。

代码版本：`44ecbdd495240fcf8d916ea8e29ada88a0c3d7cc` + 当前未提交工作区。
**工作区与评审留下的 `outputs/recheck-20260926-task34/review-ready/` 快照逐文件哈希一致**
（17 个文件全部 SAME），因此本轮结果绑定的是评审确认过的那份实现，而不是基线 checkout。

源哈希与环境记录：`outputs/acceptance-20260926-run2/under-test.txt`。

---

## 1. 文件哈希 / 解释器 ABI / 依赖 / Docker 核验

| 项 | 结果 |
| --- | --- |
| 基线 | `44ecbdd495240fcf8d916ea8e29ada88a0c3d7cc`，分支 `main` |
| 工作区 vs `review-ready` 快照 | 17/17 文件 `SAME`（逐个 SHA256 比对） |
| `review-ready/SHA256SUMS.txt` 自校验 | 21/21 OK |
| 补丁可应用性 | `git apply --check` 对未跟踪新文件报 "already exists"（工作区已含这些文件，属预期；补丁面向干净基线） |
| 解释器 | 宿主 `python3.14t` = Python 3.14.6，**GIL 关闭**（`sys._is_gil_enabled() == False`），`cache_tag=cpython-314` |
| 依赖路径 | `outputs/performance-20260924/python314t-test-deps`、`backend/.py314t-site` 均存在 |
| Docker | 客户端/服务端均 29.6.1 |
| 业务容器 | `MyRedis`/`MyTdengine`/`MyMySQL`/`data-watch-backend` 均为 Up；**本轮未接触它们**（仅只读 `docker ps`） |

### 独立复验（按评审给的命令与 ABI/依赖路径）

| 套件 | 结果 | 评审记录 |
| --- | --- | --- |
| `collector/tests`（python3.14t，`-p no:cacheprovider`） | **594 passed, 24 skipped, 1 warning**，188.31s | 594 passed, 24 skipped, 1 warning，235.78s |
| `writer/tests` | **184 passed** | 184 passed |
| `scripts` | **81 passed** | 81 passed |

跳过的 24 项为需要真实服务（Redis/MySQL URL 环境变量）的用例，**不计为通过**。
结论：我此前在宿主 GIL 解释器下看到的 81 项失败，在正确的 3.14t ABI + 依赖路径下**不复现**，
与评审判断一致；该分类问题就此关闭。

---

## 2. 隔离环境与短冒烟

环境（唯一命名，本轮自建）：网络 `dwv-acct2`；Redis `dwv-acct2-redis`
（`redis:7.4`，`--appendonly yes --appendfsync everysec --maxmemory 8gb --maxmemory-policy noeviction`，
容器 `--memory 10g` 留出 cgroup 余量，避免 maxmemory≈cgroup 限制导致 OOM 杀进程）；
镜像 `data-watch-collector-test:20260925`（python3.14t）、`data-watch-pipeline-runner:20260925`。

冒烟：2 设备 × 8 点 × 100ms × 40s（覆盖 NULL/布尔/负值/质量变化/非存储/STRING/时有时无 NULL），
输出写到宿主挂载目录 `outputs/acceptance-20260926-run2/`。

| 检查项 | 结果 |
| --- | --- |
| 守恒逐样本残差 | `identity_residual_frames=0`、`nonzero_or_missing_residual_samples=0`、`tolerance_frames=0`、`queue_conservation=pass`（80 个采样点） |
| 进度留档落宿主 | `smoke-2dev-8pt.json.progress.jsonl` 存在，6 个事件：`started` → 4×`running` → `complete` |
| 退出码与 verdicts 一致 | `measured_status=pass` ⇒ 退出码 **0** ✓ |
| 其他判定 | `input_on_schedule`/`durable_received`/`sustained_replay`/`steady_state`/`data_correctness`/`post_stop_drain` 均 pass；`margin`/`degraded`/`seeded` 为 `not_tested`（未跑，不计通过） |
| 自检脚本 | `python outputs/acceptance-20260926-run2/check_run.py <json> --exit-code N` → `selfcheck: pass` |

---

## 3. 20 设备 × 500 点 × 100ms × 30 分钟 Collector 容量长测

`--seconds 1800 --drain-seconds 120 --sample-every-ticks 20 --audit-stride 301 --redis-memory-guard-mb 6000`，
输出 `soak-30min-20dev-500pt.json`（新目录），进度 `soak-30min-20dev-500pt.json.progress.jsonl`（63 个事件，落宿主）。

| 判定 | 状态 | 关键数字 |
| --- | --- | --- |
| input_on_schedule | pass | 360,000/360,000 帧，1800.000s |
| durable_received | pass | SQLite 持久提交 360,000 帧 = 生产输入 |
| **sustained_replay** | **fail** | 稳态确认 **199.93 帧/秒** vs 目标 200.00（比值 1.000，**差 0.035%**）；生产期累计确认 359,520 |
| steady_state | pass | 趋势 `flat`；898 个稳态窗口；窗口内最大积压 822 帧 / 最老 4119ms |
| **queue_conservation** | **pass** | 容差 0、残差 0、**900 个采样点全部为 0**；`sawtooth_or_converging`；30 分钟净增长 **+0.046 帧/秒**，尾段 1/4 均值 **−0.163 帧/秒** |
| data_correctness | pass | 身份/缺失/重复**全量**核对 360,000 帧：缺失 0、重复 0、非法身份 0；**完整字段按 stride 抽样**核对 1198 帧 |
| post_stop_drain | pass | 停采后 outbox=0，剩余 480 帧排空 2.663s |
| 退出码 | 1 | 与 `measured_status=fail` 一致（唯一 fail 项是 `sustained_replay`） |
| 资源 | — | Redis 峰值 4.44 GB（< 8gb maxmemory，未触发内存护栏）；stop_reason=`duration`；容器 RSS 峰值 139 MB |

### 3.1 严格判定 fail 的性质必须说清

* 生产期确认 359,520 帧 = 提交 360,000 − **仍在本地队列的 480 帧**（结束时 outbox=480，排空 2.663s 完成）。
  差额**不是丢帧**，而是稳态积压本身；守恒检查独立证明 900 个采样点残差全为 0、30 分钟净增长≈0。
* `sustained_replay` 用"窗口内确认量 ÷ 窗口时长"计算，按定义扣除了这段常驻积压，于是落在 199.93。
  规则要求 ≥ 1.000，**按规则记为 fail**；我不会把它改写成通过，也不会据此宣称"容量达标"。
* 该 fail 与"是否丢数据/是否持续积压"是两件事，两个判定分开报。

### 3.2 500 点档的瓶颈与 200 点档**不是同一个**

| 量 | 200 点档（上一轮 30 分钟长测） | 500 点档（本次 30 分钟长测） |
| --- | --- | --- |
| Redis 单线程 CPU 忙碌比 | 0.277 | **0.930** |
| 批次周期 `cycle_ms` p50 | 1001 | **2000** |
| 每批帧数 p50 | 200 | **400** |
| 确认往返 `deliver_ms` p50 | 967 | **1845** |
| `claim_gap_ms` p50 | 23 | 93 |
| 批内最老帧排队 p50 | 955 | 1986 |
| `waitaof` 自身 CPU | 4436µs / 1814 次 | 2219µs / 1071 次 |
| Lua 执行（`evalsha`+`eval`） | 425s / 1798s = 0.24 | 1542s / 1798s = **0.86** |

结论：**200 点档**的 1 秒往返与 `appendfsync everysec` 的落盘节奏吻合（Redis 仅 28% 忙）；
**500 点档**下 Redis 单线程已 93% 忙、往返变成约 1.85 秒，**瓶颈变成了每帧 Lua 执行成本**
（约 4.3ms/帧 × 400 帧 ≈ 1.7s），此时"等落盘"不再是可分离的主导项。
所以 AOF 归因实验只在**窄帧**配置下才是有效探针（见 §4）。

---

## 4. 单因素对照：`appendfsync` everysec vs always（同一隔离 Redis）

固定相同资源（2 CPU / 4GB 客户端容器）与相同负载（20 设备 × 200 点 × 100ms = 200 帧/秒 × 300s），
只切换隔离实例的 `appendfsync`；两次都保留 AOF 与 `WAITAOF` 可靠确认语义；结束后恢复 `everysec`。
**这是归因诊断，不是配置建议**，也没有改动业务 Redis。

> 结果见 §4.1（本次运行输出 `ab-appendfsync-everysec.json` / `ab-appendfsync-always.json`）。

### 4.1 结果（第一对：everysec → always）

| 量（p50） | everysec | always | 变化 |
| --- | ---: | ---: | ---: |
| 确认往返 `deliver_ms` | 958.9 ms | **346.7 ms** | **−63.8%** |
| 批次周期 `cycle_ms` | 1001.4 ms | 370.0 ms | −63.1% |
| 批内最老帧排队 `queue_oldest_age_ms_at_claim` | 970 ms | 320 ms | −67.0% |
| 每批帧数 `frames` | 200 | 80 | −60.0% |
| `claim_gap_ms` | 27.0 ms | 18.1 ms | −32.8% |
| Redis CPU 忙碌比 | 0.454 | 0.373 | — |
| Lua 执行合计（`evalsha`+`eval`） | 116.6 s | 95.2 s | −18% |
| `waitaof` 自身 CPU | 微秒级 | 微秒级 | — |
| **稳态确认速率** | **200.00 帧/秒** | **187.15 帧/秒** | **−6.4%** |
| 周期 p95 / max | 8154.7 / 13698.2 ms（always 侧） | — | 见下 |
| 队列守恒 | pass，残差 0，`sawtooth_or_converging`，净增长 −0.111 帧/秒 | pass（自洽），残差 0，**`sustained_growth`，净增长 +12.75 帧/秒** | — |
| 结束时积压 / 最老 | 360 帧 / 2.0 s | **4160 帧 / 20.7 s**，排空 10.0 s | — |

### 4.2 两个结论（必须分开）

**结论一：AOF 等待归因成立（这就是评审要求补的实验 B）。**
`everysec` 下 959 ms 的中位确认往返，在只改 `appendfsync` 后降到 347 ms（−64%），
而同一对的 **Lua 执行时间只变了 18%**、`waitaof` 自身 CPU 仍是微秒级。
因此这约 1 秒确实主要是**等下一次 AOF 落盘**，不是 Redis 执行脚本慢，也不是 `WAITAOF` 命令本身耗时——
这正是不允许"仅凭 WAITAOF 自身 CPU 计数推断刷盘等待耗时"的原因：
必须靠单因素对照才能把它们分开。这条只对**窄帧**配置成立；500 点档的瓶颈已是 Redis 单线程 CPU（§3.2）。

**结论二：`always` 不是解法，反而更差。**
`always` 把中位延迟砍掉 64%，却把长尾拉爆（周期 p95 **8.15 s**、max **13.7 s**），
稳态确认速率掉到 **187.15 帧/秒 < 200**，本地队列以 **+12.75 帧/秒** 持续增长
（结束时积压 4,160 帧 / 最老 20.7 秒，排空 10 秒）。
即：**把"等落盘的锯齿"换成了"逐次落盘的多秒停顿"，牺牲了持续吞吐。**
因此本实验只用于归因；**不得**据此建议把业务 Redis 改成 `always`，也没有改动业务配置。

### 4.3 顺序效应检验（反序再跑一对）

`always` 在第一对里是后跑的，为排除时间/缓存/顺序影响，另跑一对**反序**：
`always` 先、`everysec` 后（`ab2-always-first.json`、`ab2-everysec-second.json`），参数完全相同。

| 量（p50） | 第一对 everysec | 第一对 always | 反序 always | 反序 everysec |
| --- | ---: | ---: | ---: | ---: |
| `deliver_ms` | 958.9 | 346.7 | 198.0 | 955.6 |
| `cycle_ms` | 1001.4 | 370.0 | 212.8 | 1001.0 |
| 每批帧数 | 200 | 80 | 40 | 200 |
| 稳态确认速率 | **200.00** | 187.15 | **134.65** | **200.20** |
| 队列守恒分类 / 净增长 | sawtooth / −0.111 | `sustained_growth` / +12.75 | `sustained_growth` / +65.39 | sawtooth\* / −0.242 |

**两种顺序下方向一致**：`always` 的中位确认往返都远低于 `everysec`（198–347 ms vs 956–959 ms），
而 `everysec` 都能稳住 200.0–200.2 帧/秒、`always` 只有 134.65–187.15 帧/秒且队列增长。
因此 §4.2 的两个结论不是顺序或缓存造成的。

\* 反序的 everysec 跑出 `sustained_growth` 但净增长为 −0.242 帧/秒：分类用的是**尾段 1/4**的
时间加权增长，300 秒窗口下尾段会翻向，**短窗口的增长分类不可靠**。
**结论以 30 分钟长测为准**（§3：900 个采样点残差全 0、净增长 +0.046 帧/秒、趋势 flat）；
300 秒对照只用于**延迟归因**，不用于增长判定。

---

## 5. 小规模完整链路 + 映射变化

`validate_docker_pipeline.py` 默认档：5 设备 × 100 点 × 100ms × 60s（预检 303,000 单元 < 2,000,000 上限），
隔离服务 `perf-redis` / `perf-td` / `perf-mysql`，输出 `pipeline-5dev-100pt.json`
与宿主进度文件 `pipeline-5dev-100pt.json.progress.jsonl`。**本脚本是核心组件集成形态，
不是生产 `writer/main.py` 入口**，不能替代生产多 worker 容量验证。

| 判定 | 状态 | 关键数字 |
| --- | --- | --- |
| correctness | pass | 值行 3000/3000、质量行 291900/291900、超级表 100 列、问题 0 |
| mapping_update | pass | 第 300 tick（t=30.016s）插入点 101（5 行映射）；前后逐帧核对通过 |
| latest | pass | 值/质量/时间戳/点集一致 |
| verifier_negative_controls | pass | 4/4 被抓（含"点与列错开一位"与"质量恒定 192"） |
| steady_state | pass | 趋势 shrinking（四段 69.2/79.2/81.9/58.1），生产期最大积压 115 帧 / 最老 2371ms |
| recovery | pass | 停采后排空：outbox=0、waiting=0、已领取未确认=0、PEL=0、lag=0、DLQ=0 |
| queue_conservation | pass | 容差 0、残差 0、`claimed_is_subset_of_queue=true` |
| **latency** | **fail** | 历史入库确认延迟：样本 3000，**阈值内 2008 / 超阈值 992 → 占比 0.6693**（要求 ≥0.99）；P99 桶区间 [1500,2000]ms；最大 1984.6ms；均值 837.5ms |
| margin | not_tested | 不在本脚本范围 |
| 退出码 | 1 | 与 `measured_status=fail` 一致（唯一 fail 项是 latency） |

延迟构成（各阶段独立统计、**不可相加**）：提交 p99 468ms、AOF 批 p50/p99 2000ms（桶上界）、
写入端 flush p99 50ms、TDengine 执行 p99 50ms。**实时显示新鲜度仍是 `not_tested`**
（本轮只测到 Redis/latest 往返），Redis 持久交付延迟无约定预算，因此也不判通过。

---

## 6. 未完成 / 未验证（不得写成已完成）

1. 百台千点（100 设备 × 1000 点 × 100ms ≈ 1000 帧/秒）；完整链路高负载长测需要**流式全字段对账工具**，
   本轮的 2,000,000 单元上限会在连接服务前拒绝该规模，不许绕过。
2. 生产启动入口形态（`writer/main.py`、多 worker）与浏览器接收→应用→绘制延迟。
3. 30% 余量验收、故障矩阵（可靠移交/响应丢失/重启）、现场硬件验收。
4. 本轮两次长测都是 **20 设备**；`200 帧/秒 × 500 点`与`20 设备`的组合不等于"百台千点"。

## 7. 本轮新增的实质发现（供下一轮决策，不改变任何判定）

1. **"历史落库慢"的瓶颈随帧宽改变，不是单一原因**：
   200 点档 Redis 单线程仅 28% 忙、往返≈1 秒＝AOF `everysec` 落盘节奏（实验 B 证实）；
   500 点档 Redis 已 **93% 忙**、往返≈1.85 秒＝每帧 Lua 执行成本（约 4.3ms/帧 × 400 帧）。
   → 想改善延迟，必须先定**帧宽**，否则会优化错方向；窄帧与宽帧的整改手段不同。
2. **`always` 是陷阱**：它把中位延迟砍 64%，却把周期 p95 抬到 8.15 秒、持续速率压到
   134.65–187.15 帧/秒并让本地队列持续增长。**不能**把"等落盘"当成应当消除的浪费。
3. **严格判定与守恒判定的一致性**：500 点档 `sustained_replay` 按"≥1.000"记 fail
   （199.93/200.00），但同一次运行的守恒对账 900 个采样点残差全 0、30 分钟净增长≈0。
   两个判定测的是不同东西，**不要用其中一个去覆盖另一个**；对外表述时两者都要写。
4. **一处证据表述待改（本轮不改代码，避免使 `review-ready` 哈希失效）**：
   `data_correctness` 的判定文案是"Collector→Redis 全字段对账：全部检查通过"，
   而实际是**身份/缺失/重复全量核对 + 完整字段按 `--audit-stride` 抽样**
   （本次 500 点档：全量 360,000 帧，抽样 1,198 帧）。数字都在结果里
   （`audited_frames` / `total_frames_in_stream` / `audit_stride`），但**文案本身没写出抽样比例**。
   按准入复核的要求"抽样不能表述为全部历史点值逐项通过"，建议下一轮把比例直接写进判定文案。
   本记录的写法一律标注抽样范围。

## 8. 证据位置

`outputs/acceptance-20260926-run2/`：`under-test.txt`（源哈希/镜像/容器）、`check_run.py`（结果自检）、
`compare_ab.py`（A/B 复算）、`SHA256SUMS.txt`（本目录清单），以及

| 文件 | 内容 |
| --- | --- |
| `smoke-2dev-8pt.json` | 阶段 2 冒烟（守恒残差 0、退出码与 verdicts 一致） |
| `soak-30min-20dev-500pt.json` | 阶段 3：20 设备 × 500 点 × 100ms × 30 分钟（900 窗口、512 批次时间线） |
| `ab-appendfsync-everysec.json` / `ab-appendfsync-always.json` | 阶段 4 第一对（everysec → always） |
| `ab2-always-first.json` / `ab2-everysec-second.json` | 阶段 4 反序对（顺序效应检验） |
| `pipeline-5dev-100pt.json` | 阶段 5 小规模完整链路 + 运行中新增点位 |
| `*.progress.jsonl` | 各次运行的 30 秒周期进度留档（带 fsync，落宿主） |
| `*-console.log` | 容器原始输出 |

**进度文件不等于最终通过**：它只用于中断留档；判定一律以最终 JSON 的 `verdicts` 为准，
中断的运行按"未完成"处理（本轮没有中断的运行）。
旧证据 `outputs/recheck-20260926-task34/` 保留为历史，不再代表当前工作区。
