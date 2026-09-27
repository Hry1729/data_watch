# 证据 · 联合链路阶梯加压（2026-09-27）

配套报告：`docs/joint-ladder-20260927.md`。分支：`test/joint-ladder-20260927`。
工具：`scripts/diagnose_joint_pipeline.py`（本目录另存一份当次使用的 `ladder_driver.py`）。

## 逐档文件

| 档 | 内容 | 状态 |
| --- | --- | --- |
| `tier-20dev` | 20 台筛查（100k 点/秒，300s） | valid |
| `tier-25dev` | 25 台（125k 点/秒） | valid |
| `tier-30dev` | 30 台（150k 点/秒，5 分钟筛查） | valid |
| `tier-40dev` | 40 台（200k 点/秒）**越界档** | valid（采集窗口因本地队列写满提前停止） |
| `soak-30dev` | **30 台 30 分钟长测**（最高可持续档） | valid |
| `baseline-20dev-rerun` | 20 台基线复跑（环境漂移检查） | valid |

每档含 `plan.json`（设备/点位/时序基线/目标速率）、`collector.json`（采集侧时间线与计数）、
`reconcile.json`（TDengine 对账）、`summary.json`（停止判据与汇总）。

## 必须知道的三件事

1. **对账 `status` 字段不可直接采信**：代码用了"喂入结束"的确认数当期望基线（长测：525,069），
   而正确基线是"排空结束"（540,000）。**数据本身正确**——值行 = 排空后确认数、质量行 = 逐 tick 期望值，
   两者完全一致（长测 540,000 / 268,272,000）。该 bug 已修在
   `scripts/diagnose_joint_pipeline.py`（`expected_value_rows` 改用 `redis_confirmed_after_drain`）。
   **无法事后复算**：每档测完即删库（保护 TDengine vnode）、Redis 逐档 flush，
   因此更正只能由当时记录的原始数字推出。
2. **本目录外还有作废轮**：为规避 40 台档 ENOSPC 曾把 outbox 放到宿主 Windows 绑定挂载，
   SQLite 在 virtiofs 上把驱动拖到 46 fps（Redis 仅 0.19 忙）→ 那次长测与基线**整体作废并重跑**；
   作废轮的文件未纳入本目录（已在工作区删除）。
3. **口令说明**：`ladder_driver.py` 里出现 `password='taosdata'`，这是**一次性隔离 TDengine 容器**
   的默认口令（`root/taosdata`），不是业务凭据；含口令字段的 Writer `settings.toml` **未纳入**本目录。
   扫描确认无业务容器名/设备号/库名引用（设备号形如 `jtc1c6f80800`，库名 `dwjnt_*`）。

## 关键口径

* **计数边界**：`planned / attempted / accepted_at_feed_end / accepted_after_drain /
  redis_confirmed_at_feed_end / redis_confirmed_after_drain / rejected_frames` 分列，不得混用。
* **CPU**：Redis 主线程/进程取自 `INFO cpu` 的窗口差值；Writer 为容器 cgroup `usage_usec` 真实增量
  （**不是**由 flush 耗时推导）；窗口边界一律取"喂入结束"。
* **延迟**：`redis_visible_lag_ms`（采样→Redis 可见）、`latest_lag_ms`（采样→latest 新鲜度）、
  `td_confirm_lag_ms`（采样→TD 完整确认，由 tdlag 模式在 runner 容器并发采样）。
  基准时间在**窗口开始**时取，避免启动耗时变成假延迟。
* **抽样范围**：逐列全字段只抽每设备 3 帧；`latest` 末态检查覆盖**全部设备**；质量行期望按 tick 统计。
