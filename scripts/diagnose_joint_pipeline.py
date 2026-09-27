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
            "shard_count": args.shard_count, "base_ts": int(time.time() * 1000) + 60_000,
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


def mode_collector(args) -> int:
    """用生产采集路径按目标速率喂入，并采样一条时间线（在 3.14t 容器里跑）。"""
    import redis  # noqa: PLC0415
    from types import SimpleNamespace  # noqa: PLC0415
    from data_watch_shared.acceptance import build_frame  # noqa: PLC0415
    from data_watch_shared.stream_envelope import stream_key  # noqa: PLC0415
    from storage.durable_redis_writer import DurableRedisWriter  # noqa: PLC0415

    plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    devices, points = plan["devices"], plan["points"]
    period_ms, base_ts = plan["period_ms"], plan["base_ts"]
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

    def submit(device: str, tick: int) -> None:
        frame = build_frame(device, points, tick, period_ms, base_ts)
        collector.write_window_stream(
            device, plan["device_type"], plan["group"], period_ms, frame["ts"], 1,
            {"ts": frame["ts"], "values": frame["window_values"]},
            frame["stream_values"], frame["stream_qualities"])
        collector.write_latest(device, plan["group"], frame["latest"])

    samples = []
    started = time.monotonic()
    cpu_before = _redis_cpu(client)
    tick = 0
    next_sample = started
    try:
        # 必须并发提交：单线程串行构造 500 点/帧撑不到 200 帧/秒（实测只有 104.5），
        # 会变成"驱动限速"而不是"被测系统限速"。与容量脚本一致：每 tick 每设备一个任务。
        with ThreadPoolExecutor(max_workers=min(32, len(devices))) as pool:
            while time.monotonic() - started < plan["seconds"]:
                for future in [pool.submit(submit, device, tick) for device in devices]:
                    future.result()
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
                        **stream})
                    next_sample = now + 0.5
                target = started + tick * period_ms / 1000.0
                sleep_for = target - time.monotonic()
                if sleep_for > 0:
                    time.sleep(sleep_for)
        submit_seconds = time.monotonic() - started
        # 停止喂入，等本地队列排空（采集侧确认）
        drain_started = time.monotonic()
        drained_at = None
        while time.monotonic() - drain_started < args.drain_seconds:
            if collector.outbox.stats()["count"] == 0:
                drained_at = time.monotonic() - drain_started
                break
            time.sleep(0.5)
        metrics = collector._metrics.snapshot()
        cpu_after = _redis_cpu(client)
        stats_after = client.info("commandstats")
        result = {
            "mode": "collector", "tier": plan["tier"], "plan_token": plan["token"],
            "submit_seconds": round(submit_seconds, 3),
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
        }
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
                        plan["period_ms"], plan["base_ts"])["stream_values"])
    quality_rows_expected *= len(plan["devices"])
    counts = read_counts(td_config, plan["devices"], plan["points"], ticks,
                         expected_quality_rows=quality_rows_expected)
    counts["expected_value_rows"] = collector_report["confirmed_frames"]
    samples = [read_sample_fields(td_config, device, columns, ticks, plan["base_ts"],
                                  plan["period_ms"], sample_ticks=args.sample_ticks)
               for device in plan["devices"][:2]]
    client = redis.Redis(host=args.redis_host, port=6379, db=args.redis_db,
                         decode_responses=True, socket_timeout=30)
    stream = _stream_state(client, collector_report["stream_keys"])
    dlq = sum(client.llen(key) for key in client.scan_iter("rt:dlq:*"))
    latest_ok = 0
    for device in plan["devices"][:2]:
        if client.hlen(f"rt:latest:{device}:{plan['group']}") == plan["points"]:
            latest_ok += 1
    problems = []
    if counts["value_rows"] != counts["expected_value_rows"]:
        problems.append(f"值行 {counts['value_rows']} != 期望 {counts['expected_value_rows']}")
    if counts["quality_rows"] != counts["expected_quality_rows"]:
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
              "latest_hash_ok_devices": f"{latest_ok}/2",
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
                        choices=("plan", "prep", "collector", "reconcile"))
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
    args = parser.parse_args()
    if args.mode == "plan":
        return mode_plan(args)
    if args.mode == "prep":
        return mode_prep(args)
    if args.mode == "collector":
        return mode_collector(args)
    return mode_reconcile(args)


if __name__ == "__main__":
    raise SystemExit(main())
