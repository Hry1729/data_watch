"""联合测试：真实采集回放 + 生产 Writer 同时跑在同一个隔离 Redis 上。

为什么需要单独一个工具（而不是把现有两个脚本并跑）：
  * validate_collector_ingest_capacity.py 自己审计 Stream 且假设条目不被消费，
    而 Writer 是 LANDED→XACK+XDEL，会把条目删掉 → 并跑会得到假失败。
  * diagnose_writer_tdengine_capability.py 的持续喂入是 XADD-only（刻意绕开历史 Lua），
    联合测试要的恰恰是**真实采集路径**（SQLite 持久队列 + 历史 Lua + latest 快车道）。

三个模式（依赖分布不同，必须分开跑）：
  * prep      —— 在 pipeline-runner 容器（有 pymysql/taosws）：建 MySQL 映射/范围表、TD 库、
                 Writer 运行目录与 settings.toml，写出 joint-plan.json。
  * collector —— 在 data-watch-collector-test（3.14t、GIL 关闭，只需 redis+msgspec）：
                 用生产 DurableRedisWriter + SQLite Outbox 按目标速率喂入，采样一条时间线。
  * reconcile —— 在 pipeline-runner：读 TDengine 行数与抽样帧逐列对账，并核对 Stream/PEL/DLQ。

只读隔离环境；不触碰业务容器；不改生产代码。
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import statistics
import sys
import time
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / "collector"), str(ROOT / "writer")]


def provenance() -> dict:
    files = ["collector/storage/durable_redis_writer.py", "writer/main.py",
             "writer/consumer.py", "writer/tdengine_writer.py",
             "scripts/diagnose_joint_pipeline.py", "data_watch_shared/acceptance.py"]
    return {"file_sha256": {name: (hashlib.sha256((ROOT / name).read_bytes()).hexdigest()
                                   if (ROOT / name).exists() else None) for name in files},
            "python": sys.version.split()[0],
            "gil_enabled": getattr(sys, "_is_gil_enabled", lambda: None)()}


def mode_prep(args) -> int:
    """建 MySQL/TD 与 Writer 运行目录（在 runner 容器里跑）。"""
    import pymysql  # noqa: PLC0415
    import taosws  # noqa: PLC0415
    sys.path.insert(0, str(ROOT / "scripts"))
    from diagnose_writer_tdengine_capability import prepare_mysql, write_runtime

    plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    prepare_mysql(args.mysql_host, plan["mysql_database"], devices=plan["devices"],
                  device_type=plan["device_type"], group=plan["group"],
                  points=plan["points"], stable=plan["stable"])
    admin = {"host": args.td_host, "port": 6041, "user": "root", "password": "taosdata"}
    conn = taosws.connect(**admin)
    conn.execute(f"CREATE DATABASE IF NOT EXISTS {plan['mysql_database']}")
    conn.close()
    settings = write_runtime(Path(args.runtime), database=plan["mysql_database"],
                             stable=plan["stable"], device_type=plan["device_type"],
                             shard_count=plan["shard_count"], group=plan["group"],
                             consumer_workers=args.consumer_workers)
    plan["runtime_settings"] = str(settings)
    plan["prep_provenance"] = provenance()
    Path(args.plan).write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"mode": "prep", "plan": args.plan, "settings": str(settings)},
                     ensure_ascii=False))
    return 0


def mode_plan(args) -> int:
    """生成联合测试计划（设备/点位/时序基线），供三个模式共用。"""
    token = uuid4().hex[:8]
    devices = [f"jt{token}{index:02d}" for index in range(args.devices)]
    plan = {"token": token, "devices": devices, "device_type": f"jnt{token}",
            "group": "gjt", "stable": f"sdjnt{token}", "mysql_database": f"dwjnt_{token}",
            "points": args.points, "period_ms": args.period_ms, "seconds": args.seconds,
            # base_ts 必须是"现在"：若取未来时间，延迟 = now − ts 会变成负数
            # （第一版加了 60s 偏移，量出 −38s 的假延迟）。唯一性由 token/库名保证。
            "shard_count": args.shard_count, "base_ts": int(time.time() * 1000),
            "tier": {"devices": args.devices, "points": args.points,
                     "period_ms": args.period_ms,
                     "offered_fps": round(args.devices * 1000.0 / args.period_ms, 3),
                     "offered_points_per_second":
                         round(args.devices * 1000.0 / args.period_ms * args.points, 1)},
            "provenance": provenance()}
    Path(args.plan).write_text(json.dumps(plan, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in plan.items() if k != "provenance"},
                     ensure_ascii=False))
    return 0


def _redis_cpu(client) -> dict:
    info = client.info("cpu")
    main_sys = info.get("used_cpu_sys_main_thread")
    main_user = info.get("used_cpu_user_main_thread")
    return {"process_s": float(info["used_cpu_sys"]) + float(info["used_cpu_user"]),
            "main_s": (None if main_sys is None else float(main_sys) + float(main_user))}


def _stream_state(client, keys) -> dict:
    backlog = 0
    pending = 0
    for key in keys:
        backlog += client.xlen(key)
        try:
            for group in client.xinfo_groups(key):
                pending += int(group.get("pending", 0))
        except Exception:
            pass
    return {"backlog_frames": backlog, "pending_frames": pending}


def _describe(values: list[float]) -> dict:
    if not values:
        return {"count": 0}
    ordered = sorted(values)
    def q(frac):
        return ordered[min(len(ordered) - 1, max(0, int(len(ordered) * frac)))]
    return {"count": len(values), "p50": q(.50), "p95": q(.95), "max": ordered[-1],
            "provenance": "raw_samples_sorted_index"}


def _redis_persistence(client) -> dict:
    """AOF/RDB 状态与内存：每档前后各取一次，用于判断 AOF 重写是否干扰本档。"""
    info = client.info("persistence")
    memory = client.info("memory")
    return {
        "aof_current_size": info.get("aof_current_size"),
        "aof_base_size": info.get("aof_base_size"),
        "aof_rewrite_in_progress": info.get("aof_rewrite_in_progress"),
        "aof_last_bgrewrite_status": info.get("aof_last_bgrewrite_status"),
        "aof_last_write_status": info.get("aof_last_write_status"),
        "rdb_last_bgsave_status": info.get("rdb_last_bgsave_status"),
        "used_memory": memory.get("used_memory"),
        "maxmemory": memory.get("maxmemory"),
    }


def _safe_lag(client, keys, device: str, group: str) -> dict:
    """延迟采样失败时返回 None，绝不让测量异常打断采集轮。"""
    try:
        return _lag_samples(client, keys, f"rt:latest:{device}:{group}", "1")
    except Exception:
        return {"redis_visible_lag_ms": None, "latest_lag_ms": None}


def _lag_samples(client, keys, latest_key: str, latest_point: str) -> dict:
    """外部可测的三类延迟（不改生产代码）：

    * redis_visible_lag_ms —— 最新 stream 条目的采样时间戳 与 now 之差（采样→Redis 可见）
    * latest_lag_ms        —— rt:latest 某点的 t 与 now 之差（采样→latest 可见/新鲜度）
    * td_lag_ms            —— 由 tdlag 模式在 runner 容器采样（此处留空）
    """
    now_ms = time.time() * 1000
    redis_lag = None
    for key in keys:
        entries = client.xrevrange(key, count=1)
        if not entries:
            continue
        fields = entries[0][1]
        raw_ts = fields.get("ts")
        if raw_ts is None:
            continue
        lag = now_ms - float(raw_ts)
        redis_lag = lag if redis_lag is None else min(redis_lag, lag)
    latest_lag = None
    raw = client.hget(latest_key, latest_point)
    if raw:
        try:
            latest_lag = now_ms - float(json.loads(raw).get("t", 0))
        except Exception:
            latest_lag = None
    return {"redis_visible_lag_ms": None if redis_lag is None else round(redis_lag, 1),
            "latest_lag_ms": None if latest_lag is None else round(latest_lag, 1)}


def mode_tdlag(args) -> int:
    """在 runner 容器并发采样"采样→TD 完整确认"延迟（读 TD 主时间列 ts 的最新值）。

    **时间边界取法（仅适用于时间列）**：本目标版本（服务端 `3.4.0.9.community`；容器内 `taos`/`taosdump`
    `3.4.0.9`；驱动 `taos-ws-py 0.7.0`）对 `min(ts)`/`max(ts)` 报
    `Invalid parameter data type : min|max`，故时间边界用 **`last(ts)`（上界）/ `first(ts)`（下界）**，
    并与"相同过滤条件下 `ORDER BY ts ASC|DESC LIMIT 1`"的结果核对一致。

    **不要把数值字段的 `min/max` 一并替换成 `first/last`**：两者语义不同——
    数值列的 `min/max` 是**聚合极值**，`first/last` 是**按时间顺序的首/末值**，只在**时间列**上二者才等价。
    该结论只适用于本轮实测的上述版本，不要泛化到所有 3.4.x。

    停止条件：出现停止文件，或达到 --seconds（与采集窗口同长再加余量）。
    """
    import taosws  # noqa: PLC0415
    plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    td_config = {"host": args.td_host, "port": 6041, "user": "root",
                 "password": "taosdata", "database": plan["mysql_database"]}
    stop_file = Path(args.stop_file) if args.stop_file else None
    samples = []
    started = time.monotonic()
    conn = taosws.connect(**td_config)
    try:
        while time.monotonic() - started < args.seconds:
            now_ms = time.time() * 1000
            elapsed_ms = 0
            lag = None
            for device in plan["devices"][:3]:
                try:
                    # 注意：本版本 TDengine 不支持 cast(max(ts) as bigint)
                    # （报 `Invalid parameter data type : max`），用 last(ts) 并自行换算。
                    rows = list(conn.query(f"SELECT last(ts) FROM `{device}`"))
                    if not rows or rows[0][0] is None:
                        continue
                    value = rows[0][0]
                    if hasattr(value, "timestamp"):
                        ts_ms = value.timestamp() * 1000
                    else:
                        text = str(value).replace(" +", "+").strip()
                        ts_ms = datetime.fromisoformat(text).timestamp() * 1000
                    candidate = now_ms - ts_ms
                    lag = candidate if lag is None else min(lag, candidate)
                except Exception:
                    pass
            samples.append({"t_rel": round(time.monotonic() - started, 3),
                            "td_confirm_lag_ms": None if lag is None else round(lag, 1)})
            if stop_file is not None and stop_file.exists():
                break
            time.sleep(2.0)
    finally:
        conn.close()
    lags = [s["td_confirm_lag_ms"] for s in samples if s["td_confirm_lag_ms"] is not None]
    result = {"mode": "tdlag", "samples_total": len(samples), "samples": samples,
              "td_confirm_lag_ms_p50": (round(statistics.median(lags), 1) if lags else None),
              "td_confirm_lag_ms_max": (round(max(lags), 1) if lags else None),
              "devices_sampled": plan["devices"][:3]}
    Path(args.output).write_text(json.dumps(result, ensure_ascii=False, indent=2),
                                 encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "samples"},
                     ensure_ascii=False, indent=2))
    return 0


def mode_collector(args) -> int:
    """用生产采集路径按目标速率喂入，并采样一条时间线（在 3.14t 容器里跑）。"""
    import redis  # noqa: PLC0415
    from types import SimpleNamespace  # noqa: PLC0415
    from data_watch_shared.acceptance import build_frame  # noqa: PLC0415
    from data_watch_shared.stream_envelope import stream_key  # noqa: PLC0415
    from storage.durable_redis_writer import DurableRedisWriter  # noqa: PLC0415

    plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    devices, points = plan["devices"], plan["points"]
    period_ms = plan["period_ms"]
    # 基准时间必须在**窗口开始**时取：若沿用 plan 生成时刻的 base_ts，
    # 帧时间戳会比实际写入早出整段启动耗时，量出来的"延迟"就是启动伪影
    # （第一版正是这样量出 ~21s 的假延迟）。
    base_ts = int(time.time() * 1000)
    keys = sorted({stream_key(device, plan["device_type"], plan["group"],
                              plan["shard_count"]) for device in devices})
    client = redis.Redis(host=args.redis_host, port=6379, db=args.redis_db,
                         decode_responses=True, socket_timeout=30)
    assert client.ping(), "isolated Redis unreachable"
    collector = DurableRedisWriter(
        client, SimpleNamespace(redis=SimpleNamespace(
            stream_shard_count=plan["shard_count"], stream_maxlen=0,
            stream_warn_entries=50000)),
        outbox_path=Path(args.outbox_dir) / f"joint-{plan['token']}.sqlite")
    collector.ping()          # 生产默认行为：latest 快车道 + 历史回放都启动

    submit_failures: list[dict] = []
    stop_reason = {"reason": None}

    def submit(device: str, tick: int) -> None:
        # 提交失败（例如本地持久队列满、资源暂时不可用）必须**被记录成边界**，
        # 不能让异常冒泡把整轮采集打断——第一版就是这样丢掉了一档。
        try:
            frame = build_frame(device, points, tick, period_ms, base_ts)
            collector.write_window_stream(
                device, plan["device_type"], plan["group"], period_ms, frame["ts"], 1,
                {"ts": frame["ts"], "values": frame["window_values"]},
                frame["stream_values"], frame["stream_qualities"])
            collector.write_latest(device, plan["group"], frame["latest"])
        except Exception as exc:                      # noqa: BLE001
            if len(submit_failures) < 20:
                submit_failures.append({"tick": tick, "device": device,
                                        "error": f"{type(exc).__name__}: {exc}"})
            if stop_reason["reason"] is None:
                stop_reason["reason"] = f"submit_failed: {type(exc).__name__}: {exc}"

    samples = []
    started = time.monotonic()
    cpu_before = _redis_cpu(client)
    persistence_before = _redis_persistence(client)
    # commandstats 是**自 Redis 启动以来累计**的（FLUSHDB 不会重置统计），
    # 所以必须取窗口前后的差值，否则每档的数字是跨档累加、根本不可比。
    cmd_before = client.info("commandstats")
    tick = 0
    next_sample = started
    try:
        # 必须并发提交：单线程串行构造 500 点/帧撑不到 200 帧/秒（实测只有 104.5），
        # 会变成"驱动限速"而不是"被测系统限速"。与容量脚本一致：每 tick 每设备一个任务。
        with ThreadPoolExecutor(max_workers=min(32, len(devices))) as pool:
            while time.monotonic() - started < plan["seconds"]:
                for future in [pool.submit(submit, device, tick) for device in devices]:
                    future.result()
                if stop_reason["reason"] is not None:
                    break                              # 提交失败即停止加压，保留现场
                tick += 1
                now = time.monotonic()
                if now >= next_sample:
                    metrics = collector._metrics.snapshot()
                    counters = metrics["counters"]
                    stream = _stream_state(client, keys)
                    samples.append({
                        "t_rel": round(now - started, 3), "tick": tick,
                        "accepted_frames": counters.get("durable_frames_accepted", 0),
                        "confirmed_frames": counters.get("history_frames_confirmed", 0),
                        "latest_sent_groups": counters.get("latest_fastlane_sent_groups", 0),
                        "outbox": {k: v for k, v in collector.outbox.stats().items()
                                   if k in ("count", "oldest_age_ms", "claimed_frames",
                                            "waiting_writes")},
                        # 本地提交批大小是指标 gauge（不在 outbox.stats() 里）
                        "commit_batch_frames": (metrics.get("gauges") or {}).get(
                            "outbox_commit_batch_frames"),
                        # 三类延迟里的两类可在此外部测量（TD 那类由 tdlag 模式采）；
                        # 测量本身绝不能让整轮失败（异常降级为 null）。
                        **_safe_lag(client, keys, devices[0], plan["group"]),
                        **stream})
                    next_sample = now + 0.5
                target = started + tick * period_ms / 1000.0
                sleep_for = target - time.monotonic()
                if sleep_for > 0:
                    time.sleep(sleep_for)
        submit_seconds = time.monotonic() - started
        # ---- 窗口边界：CPU / 命令统计 / AOF 一律在**喂入结束**这一刻取 ----
        # 排在排空之后再取会让分子含排空、分母只算喂入，忙碌比被抬高
        # （40 台档正是这样量出 >1 的 1.101，排空还用了 64.7s）。
        metrics = collector._metrics.snapshot()
        cpu_after = _redis_cpu(client)
        stats_after = client.info("commandstats")
        persistence_after = _redis_persistence(client)
        window_calls = {}
        for name, value in stats_after.items():
            delta = int(value.get("calls", 0)) - int(cmd_before.get(name, {}).get("calls", 0))
            if delta:
                window_calls[name] = delta
        counters = metrics["counters"]
        feed_end = samples[-1] if samples else {}
        # ---- 停止喂入，等本地队列排空（采集侧确认）----
        drain_started = time.monotonic()
        drained_at = None
        while time.monotonic() - drain_started < args.drain_seconds:
            if collector.outbox.stats()["count"] == 0:
                drained_at = time.monotonic() - drain_started
                break
            time.sleep(0.5)
        metrics_after_drain = collector._metrics.snapshot()
        drain_counters = metrics_after_drain["counters"]
        window = max((feed_end.get("t_rel") or 0) - (samples[0].get("t_rel") or 0), 1e-9) if len(samples) > 1 else 1e-9
        result = {
            "mode": "collector", "tier": plan["tier"], "plan_token": plan["token"],
            "base_ts_used": base_ts,
            "submit_seconds": round(submit_seconds, 3),
            # ---- 计数边界（生产结束 vs 排空结束，分列不许混用）----
            "planned_frames": len(devices) * int(plan["seconds"] * 1000 / period_ms),
            "attempted_frames": tick * len(devices),
            "accepted_at_feed_end": feed_end.get("accepted_frames"),
            "accepted_after_drain": drain_counters.get("durable_frames_accepted", 0),
            "redis_confirmed_at_feed_end": feed_end.get("confirmed_frames"),
            "redis_confirmed_after_drain": drain_counters.get("history_frames_confirmed", 0),
            "rejected_frames": drain_counters.get("outbox_rejected_frames", 0),
            # 快车道合并比例 = 1 − 实际发送组数 / 帧数（越大说明合并得越多）
            "fastlane_merge_ratio": round(
                1 - counters.get("latest_fastlane_sent_groups", 0) / max(tick * len(devices), 1), 4),
            # 本窗口内的 Redis 命令调用（差值；commandstats 是自启动累计的，必须取差值）
            "inner_command_calls_in_window": window_calls,
            "batch_frames_mean": round(statistics.mean(
                [s.get("commit_batch_frames") or 0 for s in samples]), 2) if samples else None,
            "redis_persistence_before": persistence_before,
            "redis_persistence_after": persistence_after,
            "aof_bytes_written_in_window": (
                None if persistence_after.get("aof_current_size") is None
                or persistence_before.get("aof_current_size") is None else
                persistence_after["aof_current_size"] - persistence_before["aof_current_size"]),
            "submitted_frames": tick * len(devices),
            "submitted_fps": round(tick * len(devices) / submit_seconds, 2),
            # 驱动自证：实际输入是否达到目标。低于目标说明限速在驱动侧，本轮的
            # "是否饱和"结论无效（第一版单线程只到 104.5 fps，就是被这一项暴露的）。
            "input_rate_ratio": round(
                (tick * len(devices) / submit_seconds) / plan["tier"]["offered_fps"], 4),
            "input_rate_reached": (
                (tick * len(devices) / submit_seconds) >= plan["tier"]["offered_fps"] * 0.95),
            "accepted_frames": metrics["counters"].get("durable_frames_accepted", 0),
            "confirmed_frames": metrics["counters"].get("history_frames_confirmed", 0),
            "confirmed_fps_in_window": round(
                (samples[-1]["confirmed_frames"] - samples[0]["confirmed_frames"])
                / max(samples[-1]["t_rel"] - samples[0]["t_rel"], 1e-9), 2) if len(samples) > 1 else None,
            "latest_sent_groups": metrics["counters"].get("latest_fastlane_sent_groups", 0),
            "collector_drain_seconds": (round(drained_at, 3) if drained_at else None),
            "final_outbox": collector.outbox.stats(),
            "redis_cpu_main_thread_busy": (
                None if cpu_after["main_s"] is None else
                round((cpu_after["main_s"] - cpu_before["main_s"]) / submit_seconds, 4)),
            "redis_cpu_process_busy": round(
                (cpu_after["process_s"] - cpu_before["process_s"]) / submit_seconds, 4),
            "redis_commandstats_tail": {
                name: {"calls": int(value.get("calls", 0))}
                for name, value in stats_after.items()
                if any(key in name for key in ("evalsha", "hset", "hmget", "lindex", "xadd"))},
            "samples": samples, "samples_total": len(samples),
            "stream_keys": keys,
            "submit_failures": len(submit_failures),
            "submit_failure_samples": submit_failures,
            "feed_stop_reason": stop_reason["reason"],
            # 窗口内的延迟分位（原始样本排序取索引；窗口 = 喂入期间）
            "latency_in_window": {
                name: _describe([s[name] for s in samples if s.get(name) is not None])
                for name in ("redis_visible_lag_ms", "latest_lag_ms")},
        }
        # 稳态窗口（后半段）单独给一份，避免把起步阶段的填充算进"稳态"
        half = samples[len(samples) // 2:]
        result["latency_second_half"] = {
            name: _describe([s[name] for s in half if s.get(name) is not None])
            for name in ("redis_visible_lag_ms", "latest_lag_ms")}
        result["window_seconds"] = round(window, 3)
        Path(args.output).write_text(json.dumps(result, ensure_ascii=False, indent=2),
                                     encoding="utf-8")
        print(json.dumps({k: v for k, v in result.items() if k != "samples"},
                         ensure_ascii=False, indent=2))
    finally:
        collector.close()
        client.close()
    return 0


def mode_reconcile(args) -> int:
    """读 TDengine 对账 + 核对 Stream/PEL/DLQ（在 runner 容器里跑）。"""
    import redis  # noqa: PLC0415
    import taosws  # noqa: PLC0415
    sys.path.insert(0, str(ROOT / "scripts"))
    from diagnose_writer_tdengine_capability import read_counts, read_sample_fields
    from data_watch_shared.acceptance import tdengine_value_column  # noqa: PLC0415

    plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    collector_report = json.loads(Path(args.collector_output).read_text(encoding="utf-8"))
    # 时序基准以采集侧实际使用值为准（窗口开始时刻），保证 tick 推算一致
    base_ts_used = collector_report.get("base_ts_used", plan["base_ts"])
    ticks = int(collector_report["submitted_frames"] / len(plan["devices"]))
    td_config = {"host": args.td_host, "port": 6041, "user": "root",
                 "password": "taosdata", "database": plan["mysql_database"]}
    columns = sorted(tdengine_value_column(point) for point in range(1, plan["points"] + 1)
                     if point not in (1, 6, 7))  # NULL/非存储/STRING 点不进历史，无列
    # 质量行期望必须逐 tick 统计：时有时无 NULL 的点在某些 tick 不进历史，
    # 用"帧数 × 列数"会系统性多算（实测差 4,800 行 = 0.04%）。
    from data_watch_shared.acceptance import build_frame  # noqa: PLC0415
    quality_rows_expected = 0
    for tick in range(ticks):
        quality_rows_expected += len(
            build_frame(plan["devices"][0], plan["points"], tick,
                        plan["period_ms"], base_ts_used)["stream_values"])
    quality_rows_expected *= len(plan["devices"])
    problems = []
    missing_tables = []
    try:
        counts = read_counts(td_config, plan["devices"], plan["points"], ticks,
                             expected_quality_rows=quality_rows_expected)
    except Exception as exc:                     # noqa: BLE001
        # 拒收/提前停止会让个别设备没有子表；这属于"缺口"，要作为 problem 报出，
        # 不能让对账整体崩掉（否则一档饱和就丢掉整条阶梯的收尾）。
        missing_tables.append(f"read_counts: {type(exc).__name__}: {exc}")
        counts = {"value_rows": None, "quality_rows": None,
                  "expected_value_rows": collector_report.get("redis_confirmed_after_drain"),
                  "expected_quality_rows": quality_rows_expected}
    # 期望值必须用**排空结束**的确认数：喂入结束那一刻还有在途/未确认的帧，
    # 用它会系统性少算（长测档因此被误判 fail：期望 525,069 vs 实际 540,000）。
    counts["expected_value_rows"] = collector_report.get(
        "redis_confirmed_after_drain", collector_report["confirmed_frames"])
    samples = []
    for device in plan["devices"][:2]:
        try:
            samples.append(read_sample_fields(td_config, device, columns, ticks, base_ts_used,
                                              plan["period_ms"],
                                              sample_ticks=args.sample_ticks))
        except Exception as exc:                 # noqa: BLE001
            missing_tables.append(f"read_sample_fields({device}): {type(exc).__name__}: {exc}")
    client = redis.Redis(host=args.redis_host, port=6379, db=args.redis_db,
                         decode_responses=True, socket_timeout=30)
    stream = _stream_state(client, collector_report["stream_keys"])
    dlq = sum(client.llen(key) for key in client.scan_iter("rt:dlq:*"))
    latest_ok = 0
    for device in plan["devices"]:          # latest 末态检查覆盖全部设备
        if client.hlen(f"rt:latest:{device}:{plan['group']}") == plan["points"]:
            latest_ok += 1
    problems = list(missing_tables)
    if counts["value_rows"] is not None and counts["value_rows"] != counts["expected_value_rows"]:
        problems.append(f"值行 {counts['value_rows']} != 期望 {counts['expected_value_rows']}")
    if (counts["quality_rows"] is not None
            and counts["quality_rows"] != counts["expected_quality_rows"]):
        problems.append(f"质量行 {counts['quality_rows']} != 期望 {counts['expected_quality_rows']}")
    for item in samples:
        if item["problem_count"]:
            problems.append(f"{item['device']} 抽样全字段不符 {item['problem_count']} 处")
    if stream["pending_frames"]:
        problems.append(f"仍有未确认(PEL) {stream['pending_frames']} 帧")
    if dlq:
        problems.append(f"DLQ 非空 {dlq}")
    result = {"mode": "reconcile", "plan_token": plan["token"], "ticks": ticks,
              "rows": counts, "stream_after_drain": stream, "dlq_entries": dlq,
              "latest_hash_ok_devices": f"{latest_ok}/{len(plan['devices'])}",
              # Writer 真实 CPU：由驱动从容器 cgroup cpu.stat 取增量后传入（不得由 flush 耗时推导）
              "writer_process_cpu_seconds": args.writer_cpu_seconds,
              "writer_cpu_busy_ratio": (
                  None if args.writer_cpu_seconds is None else
                  round(args.writer_cpu_seconds / max(collector_report["window_seconds"], 1e-9), 4)),
              "latest_end_state": {device: client.hlen(f"rt:latest:{device}:{plan['group']}")
                                   for device in plan["devices"]},
              "full_field_sample": {"columns": len(columns), "results": samples,
                                    "coverage_note": "每设备仅抽样 3 帧逐列核对，其余只核对行数"},
              "problems": problems, "status": "pass" if not problems else "fail"}
    Path(args.output).write_text(json.dumps(result, ensure_ascii=False, indent=2),
                                 encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "full_field_sample"},
                     ensure_ascii=False, indent=2))
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(description="联合测试：真实采集 + 生产 Writer")
    parser.add_argument("--mode", required=True,
                        choices=("plan", "prep", "collector", "reconcile", "tdlag"))
    parser.add_argument("--plan", required=True)
    parser.add_argument("--output")
    parser.add_argument("--collector-output")
    parser.add_argument("--runtime", default=None)
    parser.add_argument("--devices", type=int, default=20)
    parser.add_argument("--points", type=int, default=500)
    parser.add_argument("--seconds", type=float, default=120)
    parser.add_argument("--period-ms", type=int, default=100)
    parser.add_argument("--shard-count", type=int, default=4)
    parser.add_argument("--consumer-workers", type=int, default=1)
    parser.add_argument("--sample-ticks", type=int, default=2)
    parser.add_argument("--drain-seconds", type=float, default=180)
    parser.add_argument("--redis-host", default="perf-redis")
    parser.add_argument("--redis-db", type=int, default=0)
    parser.add_argument("--mysql-host", default="perf-mysql")
    parser.add_argument("--td-host", default="perf-td")
    parser.add_argument("--outbox-dir", default="/tmp")
    parser.add_argument("--stop-file", default=None,
                        help="tdlag 模式：出现该文件即停止采样")
    parser.add_argument("--writer-cpu-seconds", type=float, default=None,
                        help="reconcile 模式：采集窗口内 Writer 容器真实 CPU 秒增量（cgroup 差值）")
    args = parser.parse_args()
    if args.mode == "plan":
        return mode_plan(args)
    if args.mode == "prep":
        return mode_prep(args)
    if args.mode == "collector":
        return mode_collector(args)
    if args.mode == "tdlag":
        return mode_tdlag(args)
    return mode_reconcile(args)


if __name__ == "__main__":
    raise SystemExit(main())
