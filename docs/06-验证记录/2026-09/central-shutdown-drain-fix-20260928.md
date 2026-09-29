# 中心停机丢样窗口：根因与最小修复（2026-09-28）

本轮范围：修复**正常停机丢样窗口**，并补齐确定性回归。不重设计中心架构、不改分布式契约、不改业务持久化。

## 1. 真正定位的根因

`collector/collector/scheduler.py` 的 `TaskScheduler.stop()` 旧实现：

```python
self._running = False
for task in self._loop_tasks:
    task.cancel()          # ← 立刻取消设备循环
    await task
```

设备循环在 `while self._running:` 内执行 `await execute_func(task)`（`_run_loop` / `_run_device_loop`
→ `_execute_due_tasks`）。`execute_func` 内部顺序是"驱动读取成功 → 解析/分组 → 写 Redis
（`write_window_stream` / `write_latest`）"。**立刻 cancel 的取消点可能落在读取成功之后、
写 Redis 完成之前**，于是这一帧被丢弃。

复现证据（`outputs/central-restore-20260928/shutdown-cancel-repro.json`）：

| 场景 | read_success | writer_calls |
| --- | ---: | --- |
| `scheduler_stop_cancel`（立刻 cancel） | 1 | **[]** ← 丢样 |
| `natural_current_cycle_drain`（等当前周期） | 1 | `["window_stream", "latest"]` |

**该缺陷在基线 `983ba9a` 之前就存在**，与本轮"中心恢复 RedisWriter"无关。

补充核实当前修复还有两个缺口：第一次排空超时后，`stop()` 取消任务再无界 `gather`，
协程若延迟或抑制取消会卡住；验证工具曾先改 `_running` 并等循环结束，因而绕开生产 stop。
`collector/main.py` 的资源关闭顺序是先停调度、再关接口/执行器/Redis，无需重排。

## 2. 最小源码改动

1. `collector/collector/scheduler.py`
   * `__init__`：新增 `_drain_timeout = 10.0`、`_cancel_timeout = 1.0`、`_stop_requested`。
   * `stop(*, drain_timeout: Optional[float] = None) -> dict`：
     置 `_running=False`（循环在下一轮开始前退出 → **不再新增采样**）→
     `asyncio.wait(loops, timeout=timeout)` 等**已在途**的周期把解析与存储跑完 →
     仅当超时才 `cancel()`，并 `increment("scheduler_drain_timeout_count")` + `logger.error`；
     取消后再有界等待 1 秒。如果协程延迟或抑制取消仍在运行，记录
     `scheduler_cancel_timeout_count` 并抛 `RuntimeError`，保留任务引用。
     `CollectorApp.shutdown()` 因此不会继续关闭仍可能被这些任务使用的 Redis/执行器；
     此时是**明确失败**，不能宣称已经排空或进程可安全继续服务。
     Python 无法强制终止忽略取消的协程；这种故障需要进程监督器处理，
     本修复保证 `stop()` 有界报错，不保证该异常进程自然退出。
     任务均已退出时返回 `{"drained": bool, "timed_out": int, "drain_timeout_s": float}`。
   * 异常取消能力保留：超时路径仍然 cancel；**不承诺**强杀/断电不丢样。
2. `collector/main.py`
   * `shutdown()`：`await self.scheduler.stop(drain_timeout=SHUTDOWN_DRAIN_TIMEOUT)`，
     未排空时记 error 日志（明确诊断），并保留后续资源关闭顺序不变。
3. 新增 `collector/tests/test_scheduler_shutdown_drain.py`（5 例，**确定性同步点**）：
   在途写入必须完成、stop 后不再新增采样、阻塞任务有界超时且留诊断、
   抑制取消时有界失败并保留存活循环、重复 stop 与 execute 抛错后清理安全。

## 3. 回归与隔离验证

```powershell
$env:PYTHONPATH='D:\python\device_alarm\data_watch\collector;D:\python\device_alarm\data_watch'
collector\.venv-pack\Scripts\python.exe -m pytest collector/tests/test_scheduler_shutdown_drain.py collector/tests/test_central_legacy_outbox.py collector/tests/test_central_redis_only_default.py collector/tests/test_central_runtime_redis_only.py -q
# 25 passed, 2 skipped
collector\.venv-pack\Scripts\python.exe -m pytest collector/tests -q
# 619 passed, 26 skipped, 1 failed
py -3.14 -m pytest edge/tests -q
# 43 passed
py -3.14 -m pytest writer/tests -q
# 184 passed
```

Collector 唯一失败仍是 `test_collector_receipt_v2.py::test_outbox_owner_lock_rejects_second_process_and_crash_releases`：
Windows 子进程退出后的 owner.lock 间歇释放失败。既有报告记录基线 3/10、当前 8/15；本轮失败栈相同，
未修改 Durable Outbox 或该测试。不能因此称 Collector 全量“全通过”。
两个中心运行例因未设置 `DW_CENTRAL_TEST_REDIS_URL` 跳过；下面的独立 Docker 验证运行了真实中心入口。
定向与全量有重叠，计数不相加。

`scripts/validate_central_pipeline.py` 现由 `app.stop()` 触发生产关闭，等待 `CollectorApp.start()` 主任务退出；
不再预先设置 `scheduler._running` 或手动等待 `_loop_tasks`、单独关闭执行器。
采集端检查本轮 `feed.stop` 文件，主机端在采样阶段收到首次 Ctrl+C 也写该文件。结果记录实际运行时长、停止原因、
计划时长完整性、关闭完整性和三个进程退出状态；提前停止即使数据对账一致仍判为计划未完成。
每次运行使用唯一容器/网络名称和 owner 标记，退出时只清理匹配本轮 owner 的资源。
独立 MySQL 曾在 socket 已就绪而 TCP 尚未就绪时导致一轮准备失败；就绪检查已改为 TCP，重试通过。

证据目录：`outputs/central-shutdown-verify-20260928/`。

| 场景 | 实际采样 | 结果 | 证据 |
| --- | ---: | --- | --- |
| 2 台 × 10 点正常冒烟 | 10.002 秒 | 19.90 帧/秒，200 帧对账一致，正常停机，三个进程退出码 0 | `944656-smoke-2x10-B/` |
| 2 台 × 10 点提前停止 | 10.045 秒（原计划 45 秒） | 200 帧对账一致；`stop_reason=requested`、`planned_duration_complete=false`，工具退出码 1；三个进程退出码 0 | `53c1ec-smoke-2x10-B/` |
| 30 台 × 500 INT 点 × 100ms | 300.005 秒 | **通过**：297.49 帧/秒，89,247 帧；真实停机、三个进程退出码 0 | `7339ca-soak-B/` |

五分钟轮逐设备 `reads = accepted = TD rows`，总质量行 44,623,500、非零质量码 0，
30 台 latest 均为 500 点；Stream、PEL、DLQ 最终均为 0。596 个抽样帧的精确采样时间戳
全部在 TD 查询确认。达速率为目标 300 帧/秒的 99.16%，高于原定 95% 门槛；
Redis 主线程平均 0.231 核，Collector 1.841 核，Writer 0.343 核。
TD 确认时间**上界** p95 为 392.72ms。读取、Redis 写入、执行池拒收及调度排空超时计数均为 0。
值内容只抽查前两台首尾行的三列；没有逐行逐字段核对。

## 5. 部署前事项

1. 旧采集进程停止方式与业务遗留 Outbox 的**只读核查 + 保全/授权处置**（本轮无授权）。
2. `scripts/validate_collector_ingest_capacity.py`、`validate_collector_free_thread.py`、
   `diagnose_joint_pipeline.py`、`test_capacity_margin_verdicts.py` 仍直接实例化 `DurableRedisWriter`
   —— 其结果**不得**再被称为"中心式性能"。
3. TD 延迟探针从实际写出调用记录少量抽样帧的精确采样 `ts`，监测端按该 `ts` 查询 TD，
   以 250ms 轮询发现时间计算**抽样帧 TD 确认时间上界**；这不是全量完整帧确认，
   也不是精确提交延迟。`latest` 往返亦不等于浏览器显示延迟。
4. Docker Linux + 模拟 I/O 的结果不能直接作为现场 Windows/PLC 容量认证。

## 6. 边界与现场

* 未推送、未部署、未提交；`HEAD` 仍为 `983ba9a`，`main` 仍为 `6b3630c`，4 个 stash 未动。
* 工作区相关改动：`M collector/collector/scheduler.py`、`M collector/main.py`、
  `?? collector/tests/test_scheduler_shutdown_drain.py`、`?? scripts/validate_central_pipeline.py`、
  本报告。`main.py` 仍含上一轮中心恢复 RedisWriter 的未提交改动；
  `docs/images/*` 与本轮无关，未触碰。原 4 个 stash 保留。
* 业务对象（MyRedis/MyTdengine/MyMySQL/data-watch-backend/现有采集进程）未操作。
  本轮独立容器、网络及测试数据库已清理；对应原始证据保留。
* 旧五分钟证据 `outputs/central-verify-20260928/2bd29c-smoke-30x500-B/` 仍有效：
  297.55 帧/秒、89,267 帧、Redis 主线程 0.245 核、抽样 TD 确认上界 p95 397.23ms。
  但旧工具先排空循环再停机，**不能用它证明生产停机修复**；本节新五分钟轮验证了真实停机路径。
