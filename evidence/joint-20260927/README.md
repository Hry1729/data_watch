# 证据目录 · 联合测量（2026-09-27）

配套报告：`docs/joint-measurement-20260927.md`。工具：`scripts/diagnose_joint_pipeline.py`（**未提交**）。
基线 `10a5d58` + 已推送提交 `3bc1b10`、`6b3630c`。所有运行都在自建隔离环境（网络 + redis:7.4 +
mysql:8.4 + tdengine 3.4.0.9），业务容器未触碰，运行结束已删除自建容器/网络/卷。

## 逐文件状态

| 文件 | 状态 | 说明 |
| --- | --- | --- |
| `joint-plan.json` | valid | 有效轮计划：20 设备 × 500 点 × 100ms = 200 帧/秒 = 100,000 点/秒，120 s |
| `joint-collector.json` | valid | **有效轮**采集侧：`input_rate_ratio=1.0`、确认 24,000/24,000、Redis 主线程 0.7429、216 采样 |
| `joint-reconcile.json` | valid | 独立对账：`status=pass`，值行 24,000、质量行 11,923,200、PEL 0、DLQ 0、latest 2/2 |
| `writer-joint-run2.log` | valid | 生产入口 Writer 日志（UTF-16LE）：0 失败、退出码 0 |
| `joint-collector-run1-driver-limited.json` | valid（另用） | 第 1 轮：驱动只到 104.51 帧/秒（`input_rate_ratio=0.52`），**不作饱和判定**，仅用于单位成本交叉验证 |
| `joint-plan-run1.json` | valid（另用） | 第 1 轮计划 |
| `extract_writer_timings.py` | valid | 读 Writer 日志的计时提取器（BOM 嗅探 + 正则） |

## 关键口径

* **输入达标自证**：结果里的 `input_rate_ratio` / `input_rate_reached` 就是为"驱动限速冒充系统限速"
  设的；低于 0.95 的轮次不得用于饱和结论（第 1 轮正是这样被识别出来的）。
* **忙碌比**来自 `INFO cpu` 的 `used_cpu_sys+user_main_thread` 差值 ÷ 窗口秒数（Redis 命令执行是单线程）；
  同时给出进程忙碌比做对照，本次两者一致（0.7429 / 0.7442）。
* **单位成本** = 主线程 CPU 秒 ÷ 帧数，与微基准 `latest-cost-500pt-v3.json` 的分解对比（3.71 vs 3.85 ms）。
* **对账范围**：值/质量行数为全量计数；**逐列全字段只抽每设备 3 帧**，其余只核对行数与身份。
  质量行期望必须**逐 tick** 统计（时有时无 NULL 的点在某些 tick 不进历史）。
* **未实测饱和**：0.743 是**本次条件**下的实测值（非对历史 0.93 的更正，也非上限）；
  134.6k 点/秒只是线性外推，**不是实测上限**。

## 密钥与业务数据检查（推送前已做）

* 本证据集**已排除** `runtime-joint/config/settings.toml`（含 `password` 字段）；
  密钥扫描仅命中 `plan_token`（8 位随机运行标识）与本说明文字，无任何真实口令值/连接串。
* 无业务对象引用：不含 `MyRedis`/`MyTdengine`/`MyMySQL`/`data-watch-backend`、
  无真实设备号或业务库名；设备号形如 `jtb35f74f300`（本轮合成），库名 `dwjnt_*`。
* `outputs/` 被 `.gitignore:85` 忽略；本目录在 `evidence/` 下，是**唯一**纳入版本库的副本。
