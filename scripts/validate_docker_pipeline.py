"""完整入库链路隔离验证：合成输入 → Collector SQLite 持久队列 → Redis Stream
→ Writer → MySQL 点位映射 → TDengine 值表与质量窄表；latest 走独立快车道。

判定分成独立项，各自 pass | fail | not_tested，词表与总体口径来自
`data_watch_shared.acceptance`（与 Collector 容量脚本共用同一份规则，不再各写一份）：

  correctness    值表 / 质量窄表 / latest 的全字段对账：
                 身份（device+ts）、重复、遗漏、逐个单元格（含 NULL）、
                 非存储点与 STRING 点不得落值表列。
  mapping_update 运行中新增点位：新列必须出现且从那一刻起逐帧正确，
                 新增之前不得有非 NULL 值。
  steady_state   生产期预热后的积压趋势（不是"停采后能排空"）。
  latency        帧采样时间戳 → TDengine 提交确认 的端到端延迟。
  recovery       停采后排空（outbox / PEL / lag / 缓冲）与 DLQ。
  margin         处理余量：**不在本脚本范围内**，固定输出 not_tested 并指明
                 应由 Collector 容量脚本的受控积压实验回答。

覆盖差异（必须区分，不得混为一谈）：
  * 本脚本是"核心组件集成"形态：直接构造 DurableRedisWriter + StreamConsumer，
    不经过 collector 设备驱动的分组/过滤代码（脚本按共享模型自行模拟过滤），
    也不经过 writer/main.py 的生产启动入口（信号处理、多 worker、consumer group
    初始化、日志与指标装配）。
  * 生产启动入口的覆盖由 writer/tests/test_main_workers.py 与部署冒烟负责；
    本脚本通过**不代表**生产启动形态已验证。
  * `ts` 是采样时间戳，端到端延迟只统计"采样时刻 → 提交确认"，与采样间隔无关。

只读隔离环境：仅使用唯一命名的数据库/设备标识/Redis 键；不读取部署凭据，
不触碰任何已有业务容器。
"""
from __future__ import annotations

import argparse
import asyncio
from concurrent.futures import ThreadPoolExecutor
import json
import logging
import os
from pathlib import Path
import statistics
import sys
import time
from types import SimpleNamespace
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / "collector"), str(ROOT / "writer")]

import psutil
import pymysql
import redis
import taosws
from consumer import StreamConsumer
from data_watch_shared.acceptance import (
    LATENCY_BUDGET_ORIGIN,
    LATENCY_METRICS,
    NEVER_STORED,
    NOT_TESTED,
    PASS,
    batch_timeline_analysis,
    correctness_verdict,
    expected_latest_value,
    expected_quality,
    expected_quality_cell,
    expected_value_cell,
    build_frame,
    latency_threshold_report,
    not_tested,
    overall_status,
    queue_conservation,
    reconcile_quality_rows,
    reconcile_value_rows,
    tdengine_value_column,
    window_analysis,
)
from data_watch_shared.stream_envelope import stream_key
from mysql_mapper import MysqlMapper
from monitor.performance import TimingHistogram
from storage.durable_redis_writer import DurableRedisWriter
from tdengine_writer import TDengineWriter

# 历史入库确认延迟的**测试预算**（不是产品 SLA，来源见 LATENCY_BUDGET_ORIGIN）。
# 判定按"阈值内样本占比 ≥ 99%"，等价于原来的 P99 ≤ 预算，但不用直方图桶上界。
DEFAULT_P99_BUDGET_MS = 1000.0
MAX_FULL_FIELD_CELLS = 2_000_000


def append_progress(path: Path, event: dict) -> None:
    """Keep periodic evidence if the process is interrupted before its final report."""
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "a", encoding="utf-8") as output:
        output.write(json.dumps(event, ensure_ascii=False) + "\n")
        output.flush()
        os.fsync(output.fileno())


def validate_full_field_load(args) -> None:
    ticks = max(1, args.seconds * 1000 // args.period_ms)
    estimated_cells = args.devices * ticks * (args.points + 1)
    if estimated_cells > MAX_FULL_FIELD_CELLS:
        raise ValueError(
            f"全字段内存对账预计 {estimated_cells} 单元，超过 "
            f"{MAX_FULL_FIELD_CELLS} 上限；长测请用 Collector 容量脚本的分页对账")


def steady_state_verdict(windows: dict) -> dict:
    if (windows.get("trend") == "insufficient_windows"
            or not windows.get("steady_windows")):
        return not_tested(
            f"预热后有效窗口不足（{windows.get('steady_windows')} 个）：无法判定稳态；"
            "未测不等于通过。建议延长 --seconds 或降低采样间隔")
    if windows["trend"] == "growing":
        return {
            "status": "fail",
            "detail": (f"生产期积压趋势 growing（窗口内最大 "
                       f"{windows['max_outbox_frames']} 帧 / 最老 "
                       f"{windows['max_outbox_oldest_age_ms']}ms）；"
                       "持续增长说明处理跟不上输入"),
        }
    step_note = ("" if windows.get("trend") != "flat" else
                 "；台阶式抬升（例如运行中新增点位的加列）在尾段持平，"
                 "不算持续增长，但需关注其一次性抬升幅度")
    return {
        "status": PASS,
        "detail": (f"生产期积压趋势={windows['trend']}，稳态窗口 "
                   f"{windows['steady_windows']} 个，窗口内最大积压 "
                   f"{windows['steady_outbox_max']} 帧（最老 "
                   f"{windows['max_outbox_oldest_age_ms']}ms），"
                   f"四段均值 {windows.get('trend_segments')}{step_note}。"
                   "这是生产期趋势，不是停采后能否排空"),
    }


def provenance() -> dict:
    """把"这份结果由哪一版代码产生"绑进结果本身（未提交工作区用内容哈希标识）。"""
    import hashlib
    module = ROOT / "data_watch_shared" / "acceptance.py"
    return {
        "script_sha256": hashlib.sha256(Path(__file__).resolve().read_bytes()).hexdigest(),
        "acceptance_sha256": (hashlib.sha256(module.read_bytes()).hexdigest()
                              if module.exists() else None),
    }


def _timing(timings: dict, name: str, key: str):
    """取计时项。Writer 与 Collector 的直方图键名不同（`p99` vs `p99_ms`），都要兼容。"""
    entry = timings.get(name) or {}
    for candidate in (f"{key}_ms", key):
        if candidate in entry:
            return entry[candidate]
    return None


def percentile(values, fraction):
    return sorted(values)[min(len(values) - 1, int(len(values) * fraction))] if values else 0


def _q(identifier: str) -> str:
    return "`" + str(identifier).replace("`", "``") + "`"


# ----------------------------------------------------------------------
# TDengine 侧读取（只读）
# ----------------------------------------------------------------------

def _td_connect(host: str, port: int):
    return taosws.connect(f"taosws://root:taosdata@{host}:{port}")


def read_value_rows(conn, database: str, stable: str, devices: list[str], columns: list[str],
                    *, batch: int) -> tuple[list[dict], list[str], int]:
    """按列分批读取值表，拼成 [{"device_code", "ts", "cells"}]。

    分批是为了避免一条 SELECT 带上千列；每批都带 ts，按 ts 合并回同一行。
    返回 (rows, 实际存在的列, 稳定表行数)。
    """
    described = {str(row[0]) for row in conn.query(f"DESCRIBE {_q(database)}.{_q(stable)}")}
    total_rows = list(conn.query(f"SELECT COUNT(*) FROM {_q(database)}.{_q(stable)}"))[0][0]
    rows: list[dict] = []
    for device in devices:
        by_ts: dict[int, dict] = {}
        for start in range(0, len(columns), batch):
            group = [column for column in columns[start:start + batch]
                     if column in described]
            if not group:
                continue
            select = ", ".join(["cast(ts as bigint) AS ts"] + [_q(c) for c in group])
            sql = f"SELECT {select} FROM {_q(database)}.{_q(device)} ORDER BY ts"
            for record in conn.query(sql):
                ts = int(record[0])
                cells = by_ts.setdefault(ts, {})
                for index, column in enumerate(group, start=1):
                    cells[column] = record[index]
        for ts, cells in sorted(by_ts.items()):
            rows.append({"device_code": device, "ts": ts, "cells": cells})
    return rows, sorted(described), int(total_rows)


def read_quality_rows(conn, database: str, devices: list[str]) -> list[dict]:
    rows: list[dict] = []
    for device in devices:
        sql = (f"SELECT cast(ts as bigint) AS ts, var_id, quality "
               f"FROM {_q(database)}.{_q(device + '_q')} ORDER BY ts, var_id")
        try:
            records = list(conn.query(sql))
        except Exception:
            # 该设备没有任何质量行（不应发生：每个进入历史的帧都带质量）。
            continue
        for record in records:
            rows.append({"device_code": device, "ts": int(record[0]),
                         "var_id": int(record[1]), "quality": int(record[2])})
    return rows


def build_value_expectations(devices: list[str], points: int, ticks: int, period_ms: int,
                             base_ts: int, *, update_tick: int, new_point: int) -> dict:
    """(device, ts) → {列: 期望值|None}；列集为"曾经进过历史的点"。"""
    expectations: dict[tuple, dict] = {}
    for device in devices:
        for tick in range(ticks):
            points_now = points + (1 if tick >= update_tick else 0)
            cols = [tdengine_value_column(p) for p in range(1, points_now + 1)
                    if p not in NEVER_STORED]
            expectations[(device, base_ts + tick * period_ms)] = {
                column: expected_value_cell(device, int(column[1:]), tick)
                for column in cols
            }
    return expectations


def build_quality_expectations(devices: list[str], points: int, ticks: int, period_ms: int,
                               base_ts: int, *, update_tick: int) -> dict:
    expectations: dict[tuple, int] = {}
    for device in devices:
        for tick in range(ticks):
            points_now = points + (1 if tick >= update_tick else 0)
            for point in range(1, points_now + 1):
                wanted = expected_quality_cell(device, point, tick)
                if wanted is not None:
                    expectations[(device, base_ts + tick * period_ms, point)] = wanted
    return expectations


def verify_latest(r, device: str, group: str, points: int, tick: int, period_ms: int,
                  base_ts: int) -> list[str]:
    """latest 必须核对值、质量、时间戳与点集，而不是只看一个时间戳。"""
    problems: list[str] = []
    raw = r.hgetall(f"rt:latest:{device}:{group}")
    if len(raw) != points:
        problems.append(f"{device}: latest 字段数 {len(raw)} 期望 {points}")
    expected_ts = base_ts + tick * period_ms
    for point in range(1, points + 1):
        entry = json.loads(raw.get(str(point), "{}"))
        wanted_value = expected_latest_value(device, point, tick)
        wanted_quality = expected_quality(point, tick)
        if entry.get("v") != wanted_value:
            problems.append(f"{device} 点 {point} latest v={entry.get('v')!r} "
                            f"期望 {wanted_value!r}")
        if int(entry.get("t", -1)) != expected_ts:
            problems.append(f"{device} 点 {point} latest t={entry.get('t')!r} "
                            f"期望 {expected_ts}")
        if int(entry.get("q", -1)) != wanted_quality:
            problems.append(f"{device} 点 {point} latest q={entry.get('q')!r} "
                            f"期望 {wanted_quality}")
    extra = [key for key in raw if not key.isdigit() or not 1 <= int(key) <= points]
    if extra:
        problems.append(f"{device}: latest 出现多余字段 {sorted(extra)[:5]}")
    return problems


# ----------------------------------------------------------------------
# 校验器灵敏度反证：故意制造错误期望，校验器必须报出来
# ----------------------------------------------------------------------

def run_negative_controls(value_rows: list[dict], expectations: dict,
                          expected_columns: list[str],
                          quality_rows: list[dict], quality_expectations: dict,
                          devices: list[str], expect_ts: int) -> dict:
    """用"故意破坏的期望"证明校验器真的能抓到错，而不是恒返回空问题列表。

    这不是改数据，而是改期望：能报出来等价于"实际数据里出现这种破坏时会被抓到"。
    """
    controls: dict[str, dict] = {}

    def check(name: str, description: str, problems: list[str]) -> None:
        controls[name] = {
            "status": PASS if problems else "fail",
            "detail": description + ("；已捕获：" + problems[0] if problems
                                     else "；未捕获（校验器失灵）"),
        }

    device = devices[0]
    key = (device, expect_ts)
    wanted = expectations.get(key, {})
    middle_columns = [column for column in expected_columns
                      if wanted.get(column) is not None]
    middle = middle_columns[len(middle_columns) // 2] if middle_columns else None
    if middle is not None:
        broken = {key: dict(wanted)}
        broken[key][middle] = float(wanted[middle]) + 12345.0
        merged = dict(expectations)
        merged.update(broken)
        check("corrupted_mid_point",
              f"把中间点 {middle} 的期望值改掉后必须报错",
              reconcile_value_rows(value_rows, merged,
                                   expected_columns=expected_columns))

        nulled = dict(expectations)
        nulled[key] = dict(wanted)
        nulled[key][middle] = None
        check("null_instead_of_value",
              f"把 {middle} 的期望值改成 NULL（实际有值）后必须报错",
              reconcile_value_rows(value_rows, nulled,
                                   expected_columns=expected_columns))

        shifted_expectations = {}
        for row_key, cells in expectations.items():
            shifted = {}
            for column, value in cells.items():
                number = int(column[1:])
                shifted[tdengine_value_column(number + 1)] = value
            shifted_expectations[row_key] = shifted
        check("wrong_column_mapping",
              "把点与列错开一位（错映射）后必须报错",
              reconcile_value_rows(value_rows, shifted_expectations,
                                   expected_columns=expected_columns))
    else:
        controls["corrupted_mid_point"] = not_tested("没有可用的中间点样本")
        controls["null_instead_of_value"] = not_tested("没有可用的中间点样本")
        controls["wrong_column_mapping"] = not_tested("没有可用的中间点样本")

    if quality_expectations and quality_rows:
        constant = dict.fromkeys(quality_expectations, 192)
        check("quality_constant_192",
              "把期望质量全部改成恒定 192（实际随点与 tick 变化）后必须报错",
              reconcile_quality_rows(quality_rows, constant))
    else:
        controls["quality_constant_192"] = not_tested("没有可用的质量行样本")
    return controls


# ----------------------------------------------------------------------
# 主流程
# ----------------------------------------------------------------------

async def run(args):
    validate_full_field_load(args)
    token = uuid4().hex[:10]
    database = f"dw_pipeline_{token}"
    device_type, group = f"perf{token}", "g"
    stable = f"sd{device_type}"
    r = redis.Redis(host="perf-redis", decode_responses=True, socket_timeout=10)
    assert r.ping()
    mysql = pymysql.connect(host="perf-mysql", user="root", autocommit=True)
    devices = [f"pf{token}d{i}" for i in range(args.devices)]
    ticks = max(1, args.seconds * 1000 // args.period_ms)
    update_tick = min(ticks - 1, max(1, int(ticks * args.mapping_update_fraction)))
    new_point = args.points + 1
    with mysql.cursor() as cur:
        cur.execute(f"CREATE DATABASE `{database}`")
        cur.execute(f"USE `{database}`")
        cur.execute("CREATE TABLE point_storage_mapping (point_id BIGINT, td_column_name VARCHAR(32), "
                    "var_type VARCHAR(32), group_code VARCHAR(32), device_code VARCHAR(32), "
                    "device_type_code VARCHAR(32), stable_name VARCHAR(64), child_table VARCHAR(32))")
        for device in devices:
            cur.executemany("INSERT INTO point_storage_mapping VALUES (%s,%s,%s,%s,%s,%s,%s,%s)",
                            [(p, tdengine_value_column(p), "float", group, device,
                              device_type, stable, device)
                             for p in range(1, args.points + 1)])
    config = {
        "tdengine": {"host": "perf-td", "port": 6041, "user": "root", "password": "taosdata",
                     "database": database},
        "redis": {"group_name": "writer", "consumer_name": f"perf-{token}"},
        "writer": {"flush_interval_ms": 100, "batch_count": 200, "max_buffer_rows": 1000,
                   "max_buffer_bytes": 32 * 1024 * 1024, "pending_min_idle_ms": 1000,
                   "device_types": [device_type], "pending_drain_interval_s": 1},
    }
    commit_latency = TimingHistogram((100, 200, 300, 500, 1000, 1500, 2000, 3000, 5000,
                                      10000, float("inf")))
    # 原始样本：固定阈值目标必须按阈值内样本数判定，直方图分位数只是桶上界。
    commit_latency_samples: list[float] = []

    class MeasuredWriter(TDengineWriter):
        async def write_multi_table(self, rows):
            outcomes = await super().write_multi_table(rows)
            confirmed_ms = time.time() * 1000
            for indices, status, _ in outcomes:
                if status == "landed":
                    for index in indices:
                        value = confirmed_ms - rows[index]["ts"]
                        commit_latency.record(value)
                        commit_latency_samples.append(value)
            return outcomes

    td = MeasuredWriter()
    await td.connect(config)
    consumer = StreamConsumer(config, MysqlMapper(mysql), td, r)
    keys = sorted({stream_key(d, device_type, group, 4) for d in devices})
    consume_task = asyncio.create_task(consumer.run_loop(keys))
    collector = DurableRedisWriter(r, SimpleNamespace(redis=SimpleNamespace(
        stream_shard_count=4, stream_maxlen=0, stream_warn_entries=50000)),
        outbox_path=Path(f"/tmp/{database}.sqlite"))
    collector.ping()
    samples, submit_ms, pacing_ms, loop_lag_ms = [], [], [], []
    done = asyncio.Event()
    generated = 0
    started = time.monotonic()
    base_ts = int(time.time() * 1000)
    progress_path = Path(str(args.output) + ".progress.jsonl")
    append_progress(progress_path, {"token": token, "status": "started",
                                    "time_ms": int(time.time() * 1000),
                                    "database": database, "devices": args.devices,
                                    "points": args.points, "seconds": args.seconds,
                                    "period_ms": args.period_ms,
                                    "provenance": provenance()})

    def submit(device, tick, points_now):
        frame = build_frame(device, points_now, tick, args.period_ms, base_ts)
        before = time.monotonic()
        collector.write_window_stream(device, device_type, group, args.period_ms,
                                      frame["ts"], 1,
                                      {"ts": frame["ts"], "values": frame["window_values"]},
                                      frame["stream_values"], frame["stream_qualities"])
        collector.write_latest(device, group, frame["latest"])
        return (time.monotonic() - before) * 1000

    def state(phase="production", elapsed_s=None):
        pending, lag, unknown = 0, 0, 0
        for key in keys:
            for info in r.xinfo_groups(key):
                if info["name"] == "writer":
                    pending += info["pending"]
                    if info.get("lag") is None:
                        unknown += 1
                    else:
                        lag += info["lag"]
        writer_snapshot = consumer._metrics.snapshot()
        with collector.outbox._lock:
            collector_snapshot = collector._metrics.snapshot()
            outbox_stats = collector.outbox.stats()
        return {"phase": phase,
                "elapsed_s": (round(time.monotonic() - started, 3) if elapsed_s is None
                              else elapsed_s),
                "submitted": generated,
                # 已确认历史帧数用已提交值行数（一帧一行）；不能用采样时间戳推断。
                "replayed_frames": int(
                    writer_snapshot["counters"].get("committed_value_rows", 0)),
                "outbox": outbox_stats,
                "pending": pending, "lag": None if unknown else lag,
                "unknown_lag_streams": unknown,
                "rss_bytes": psutil.Process().memory_info().rss,
                "writer": writer_snapshot,
                "collector": collector_snapshot}

    # 监控采样只在生产阶段按 "production" 记账：生产结束后的采样（含对账期间的最后
    # 一次）必须换阶段名，否则输入增量为 0 的窗口会混进生产期窗口，把输入下限
    # 报成 0、并稀释积压趋势。
    monitor_phase = {"name": "production"}

    async def monitor():
        while not done.is_set():
            await asyncio.sleep(1)
            samples.append(await asyncio.to_thread(state, monitor_phase["name"]))
            if len(samples) % 30 == 0:
                append_progress(progress_path, {"token": token, "status": "in_progress",
                                                "time_ms": int(time.time() * 1000),
                                                "generated_frames": generated,
                                                "sample": samples[-1]})
                print(json.dumps({"progress": samples[-1]}, ensure_ascii=False), flush=True)

    async def loop_monitor():
        while not done.is_set():
            before = time.monotonic()
            await asyncio.sleep(.05)
            loop_lag_ms.append(max(0, (time.monotonic() - before - .05) * 1000))

    monitors = [asyncio.create_task(monitor()), asyncio.create_task(loop_monitor())]
    mapping_update = {"tick": update_tick, "point": new_point,
                      "rows_inserted": 0, "inserted_at_s": None}
    try:
        with ThreadPoolExecutor(max_workers=min(32, args.devices)) as pool:
            loop = asyncio.get_running_loop()
            tick = 0
            while time.monotonic() - started < args.seconds:
                # 运行中新增点位：先把映射写进 MySQL（提交），再开始发这个点。
                # 写入端必须自己 reload 才能看到新点；漏写会在这里的 TDengine 对账暴露。
                if tick == update_tick:
                    with mysql.cursor() as cur:
                        cur.executemany(
                            "INSERT INTO point_storage_mapping VALUES (%s,%s,%s,%s,%s,%s,%s,%s)",
                            [(new_point, tdengine_value_column(new_point), "float", group,
                              device, device_type, stable, device) for device in devices])
                    mapping_update["rows_inserted"] = args.devices
                    mapping_update["inserted_at_s"] = round(time.monotonic() - started, 3)
                points_now = args.points + (1 if tick >= update_tick else 0)
                pacing_ms.append(max(0, (time.monotonic() - started) * 1000
                                     - tick * args.period_ms))
                submit_ms.extend(await asyncio.gather(*[
                    loop.run_in_executor(pool, submit, device, tick, points_now)
                    for device in devices]))
                generated += len(devices)
                tick += 1
                if consume_task.done():
                    consume_task.result()
                await asyncio.sleep(max(0, started + tick * args.period_ms / 1000
                                        - time.monotonic()))
        production_s = time.monotonic() - started
        production_end = await asyncio.to_thread(state, "production_end", "production_end")
        append_progress(progress_path, {"token": token, "status": "in_progress",
                                        "time_ms": int(time.time() * 1000),
                                        "generated_frames": generated,
                                        "sample": production_end})
        monitor_phase["name"] = "post_production"

        def claimed_unconfirmed(snapshot: dict) -> int:
            """已被回放领取但还没确认删除的帧数（计数器口径）。

            `redis_batch_frames` 只是"上一批大小"的量表，批次结束后不会归零，
            不能当活性信号用；`已确认 == 已持久提交` 才是"没有在飞批次"的证据。
            """
            counters = snapshot["collector"]["counters"]
            return max(0, int(counters.get("durable_frames_accepted", 0))
                       - int(counters.get("history_frames_confirmed", 0)))

        def delivery_quiet(snapshot: dict) -> bool:
            """排空完成 = 本地队列空 + 本地在途写空 + 没有已领取未确认的帧。

            outbox.count 不含已被回放领取、正在 WAITAOF 途中的那一批，只看它会
            在还有几十帧没确认时误判"已排空"。
            """
            return (snapshot["outbox"]["count"] == 0
                    and snapshot["outbox"].get("waiting_writes", 0) == 0
                    and claimed_unconfirmed(snapshot) == 0)

        drain_deadline = time.monotonic() + args.drain_seconds
        while time.monotonic() < drain_deadline:
            final = await asyncio.to_thread(state, "drain")
            if (delivery_quiet(final) and final["pending"] == 0 and final["lag"] == 0
                    and not consumer._buffer and consumer._inflight_rows == 0):
                break
            if consume_task.done():
                consume_task.result()
            await asyncio.sleep(.2)
        final = await asyncio.to_thread(state, "drained")

        # ---- 数据侧核对（TDengine + latest + DLQ）----
        def verify_database():
            conn = _td_connect("perf-td", 6041)
            try:
                expected_columns = [tdengine_value_column(p)
                                    for p in range(1, new_point + 1)
                                    if p not in NEVER_STORED]
                value_rows, described, total_rows = read_value_rows(
                    conn, database, stable, devices, expected_columns,
                    batch=args.point_batch)
                quality_rows = read_quality_rows(conn, database, devices)
            finally:
                conn.close()
            return value_rows, described, int(total_rows), quality_rows

        value_rows, described, total_rows, quality_rows = await asyncio.to_thread(
            verify_database)
        value_expectations = build_value_expectations(
            devices, args.points, ticks, args.period_ms, base_ts,
            update_tick=update_tick, new_point=new_point)
        quality_expectations = build_quality_expectations(
            devices, args.points, ticks, args.period_ms, base_ts, update_tick=update_tick)

        problems = reconcile_value_rows(value_rows, value_expectations,
                                        expected_columns=expected_columns_of(args, new_point))
        problems += reconcile_quality_rows(quality_rows, quality_expectations)
        # 不该出现的列：非存储点/STRING 点不得在值超级表里建列。
        forbidden = sorted({column for column in described
                            if column.startswith("p") and column[1:].isdigit()
                            and int(column[1:]) in NEVER_STORED})
        if forbidden:
            problems.append(f"值超级表出现非存储/STRING 点的列 {forbidden}")
        missing_columns = sorted(set(expected_columns_of(args, new_point)) - set(described))
        if missing_columns:
            problems.append(f"值超级表缺少应有列 {missing_columns}")

        latest_problems: list[str] = []
        for device in devices[:args.latest_devices]:
            latest_problems.extend(verify_latest(r, device, group, new_point, ticks - 1,
                                                 args.period_ms, base_ts))
        problems += latest_problems

        # 运行中新增点位：列必须存在，且只从 update_tick 起有非 NULL 值。
        update_problems: list[str] = []
        column = tdengine_value_column(new_point)
        if column not in described:
            update_problems.append(f"新增点位列 {column} 未出现在值超级表")
        else:
            cells_by_identity = {(row["device_code"], row["ts"]): row["cells"]
                                 for row in value_rows}
            for device in devices:
                for tick in range(ticks):
                    ts = base_ts + tick * args.period_ms
                    actual = cells_by_identity.get((device, ts), {}).get(column)
                    if tick >= update_tick:
                        wanted = expected_value_cell(device, new_point, tick)
                        if actual is None or abs(float(actual) - float(wanted)) > 1e-9:
                            update_problems.append(
                                f"{device}@{ts} 新增点 {new_point}: 实际 {actual!r} 期望 {wanted}")
                    elif actual is not None:
                        update_problems.append(
                            f"{device}@{ts} 新增点 {new_point} 在映射写入前就有值 {actual!r}")
                    if len(update_problems) >= 10:
                        break

        controls = run_negative_controls(
            value_rows, value_expectations, expected_columns_of(args, new_point),
            quality_rows, quality_expectations, devices, base_ts + (ticks // 2) * args.period_ms)

        dlq_entries = sum(r.llen(k) for k in r.scan_iter(f"rt:dlq:{device_type}:*"))
        windows = window_analysis(samples, args.period_ms, args.devices,
                                  warmup_s=max(1.0, args.period_ms / 1000.0 * 10))
        latency = commit_latency.snapshot()
        latency_p99 = latency.get("p99_ms", latency.get("max_ms", 0))
        # 阈值内样本占比（真实计数）取代桶上界分位数作为判据。
        threshold = latency_threshold_report(
            commit_latency_samples, args.p99_budget_ms,
            buckets=(100, 200, 300, 500, 1000, 1500, 2000, 3000, 5000, 10000,
                     float("inf")))
        latency_metrics = {
            "realtime_display_freshness": {
                "definition": LATENCY_METRICS["realtime_display_freshness"],
                "status": NOT_TESTED,
                "detail": "本轮未测量浏览器接收→数值应用→绘制（只测到 Redis/latest 往返）",
            },
            "history_commit_latency": {
                "definition": LATENCY_METRICS["history_commit_latency"],
                "status": threshold["status"], "report": threshold,
            },
            "redis_durable_delivery_latency": {
                "definition": LATENCY_METRICS["redis_durable_delivery_latency"],
                # 已测量但没有约定的预算，因此不判 pass：只报数，不冒充"通过"。
                "status": NOT_TESTED,
                "detail": ("见 collector 计时 redis_aof_batch_ms 与 batch_timeline："
                           "该口径是本地持久接收→Redis 持久确认，未约定预算，"
                           "因此不判通过，也不能替代上一条"),
            },
        }

        verdicts = {
            "correctness": correctness_verdict(
                problems, checked="值表/质量表/latest 全字段对账"),
            "mapping_update": correctness_verdict(
                update_problems, checked="运行中新增点位逐帧核对"),
            "latest": correctness_verdict(latest_problems, checked="latest 值/质量/时间戳/点集"),
            "verifier_negative_controls": correctness_verdict(
                [name for name, item in controls.items() if item["status"] != PASS],
                checked="校验器灵敏度反证"),
        }
        verdicts["steady_state"] = steady_state_verdict(windows)
        # 延迟构成分开报：采样→本地持久、AOF 批量确认、TDengine 提交、写入端批处理。
        # 只给一个总数会让人误以为瓶颈在 TDengine。
        # 注意：各阶段是**不同样本、不同阶段**的独立统计，不能相加当作端到端构成；
        # 端到端只认逐帧的 sample→commit 样本（latency_threshold_report 用的是同一批样本）。
        writer_timings = final["writer"]["timings"]
        collector_timings = final["collector"]["timings"]
        latency_breakdown = {
            "collector_submit_ms_p99": round(percentile(submit_ms, .99), 3),
            "aof_batch_ms_p50": _timing(collector_timings, "redis_aof_batch_ms", "p50"),
            "aof_batch_ms_p99": _timing(collector_timings, "redis_aof_batch_ms", "p99"),
            "writer_flush_ms_p99": _timing(writer_timings, "flush_ms", "p99"),
            "td_execute_ms_p99": _timing(writer_timings, "td_execute_ms", "p99"),
            "sample_to_commit_ms_p99_bucket_upper": latency_p99,
            "note": ("各阶段独立统计，属于不同样本；不得相加当作端到端构成。"
                     "端到端判据用阈值内样本占比，不用桶上界分位数"),
        }
        verdicts["latency"] = {
            "status": threshold["status"],
            "detail": (f"历史入库确认延迟（采样时刻→TDengine 完整帧确认）："
                       f"样本 {threshold['total_samples']}，"
                       f"≤{args.p99_budget_ms}ms {threshold['within_budget_samples']}，"
                       f">{args.p99_budget_ms}ms {threshold['over_budget_samples']}，"
                       f"阈值内占比 {threshold['within_ratio']:.4f}"
                       f"（要求 ≥{threshold['required_within_ratio']}）；"
                       f"P99 所在桶区间 {threshold['p99_bucket_interval_ms']}ms，"
                       f"最大 {threshold['max_ms']}ms。预算来源：{LATENCY_BUDGET_ORIGIN}。"
                       f"构成（各阶段独立统计，不可相加）{latency_breakdown}"),
        }
        drained = (delivery_quiet(final) and final["pending"] == 0
                   and final["lag"] == 0 and not consumer._buffer
                   and consumer._inflight_rows == 0 and dlq_entries == 0)
        claimed_in_flight = claimed_unconfirmed(final)
        verdicts["recovery"] = {
            "status": PASS if drained else "fail",
            "detail": (f"停采后排空：outbox={final['outbox']['count']} "
                       f"waiting_writes={final['outbox'].get('waiting_writes')} "
                       f"已领取未确认投递={claimed_in_flight} "
                       f"PEL={final['pending']} lag={final['lag']} "
                       f"buffer={len(consumer._buffer)} inflight={consumer._inflight_rows} "
                       f"DLQ={dlq_entries}。注：停采后能排空不证明持续容量达标"),
        }
        verdicts["margin"] = not_tested(
            "处理余量不在本脚本范围内：额定输入下的余量必须由 Collector 容量脚本的"
            "受控积压实验（--seed-backlog-frames + 额定输入追平）回答；"
            "本脚本只覆盖完整链路的正确性与稳态，未测不等于无余量")

        # 队列守恒：把已持久提交 / 已确认清理 / 队列条数三个口径取出对账，
        # 不允许"差额消失"（第三路径必须显式列出）。
        conservation = queue_conservation([
            {"elapsed_s": sample.get("elapsed_s"),
             "accepted": sample["collector"]["counters"].get("durable_frames_accepted"),
             "confirmed": sample["collector"]["counters"].get("history_frames_confirmed"),
             "queue_count": sample["outbox"].get("count"),
             "claimed_frames": sample["outbox"].get("claimed_frames"),
             "waiting_writes": sample["outbox"].get("waiting_writes"),
             "oldest_age_ms": sample["outbox"].get("oldest_age_ms")}
            for sample in samples if sample.get("collector")])
        verdicts["queue_conservation"] = {
            "status": conservation["status"],
            "detail": conservation.get("detail", "本地队列 A/D/Q 守恒对账"),
        }
        measured = {name: item for name, item in verdicts.items() if name != "margin"}
        result = {
            "provenance": provenance(),
            "overall_status": overall_status(verdicts),
            "measured_status": overall_status(measured),
            "verdicts": verdicts,
            "latency_metrics": latency_metrics,
            "latency_threshold": threshold,
            "batch_timeline": batch_timeline_analysis(collector.delivery_timeline()),
            "conservation": conservation,
            "verifier_negative_controls": controls,
            "mode": "synthetic_inputs_real_sqlite_redis_mysql_tdengine",
            "coverage_note": (
                "核心组件集成形态：直接构造 DurableRedisWriter + StreamConsumer；"
                "不经过 collector 设备驱动的分组/过滤（改由脚本按共享模型模拟）"
                "与 writer/main.py 生产启动入口；生产入口由 "
                "writer/tests/test_main_workers.py 与部署冒烟覆盖"),
            "database": database, "stable": stable, "devices": args.devices,
            "points": args.points, "new_point": new_point,
            "period_ms": args.period_ms, "ticks": ticks,
            "production_seconds": production_s, "generated_frames": generated,
            "frames_per_second": generated / production_s,
            "value_rows": total_rows, "expected_value_rows": len(value_expectations),
            "quality_rows": len(quality_rows),
            "expected_quality_rows": len(quality_expectations),
            "described_columns": len(described),
            "correctness_problems": problems,
            "mapping_update": mapping_update,
            "correctness_checked_rows": len(value_rows),
            "dlq_entries": dlq_entries,
            "source_to_td_confirm_ms": latency,
            "latency_breakdown": latency_breakdown,
            "claimed_in_flight_frames": claimed_in_flight,
            "submit_ms_p50": statistics.median(submit_ms),
            "submit_ms_p99": percentile(submit_ms, .99),
            "pacing_delay_ms_max": max(pacing_ms),
            "event_loop_lag_ms_p99": percentile(loop_lag_ms, .99),
            "event_loop_lag_ms_max": max(loop_lag_ms, default=0),
            "max_rss_bytes": max((s["rss_bytes"] for s in samples), default=0),
            "windows": windows,
            "production_end": production_end, "final": final,
            "samples": samples,
            "notes": [
                "ts 是采样时间戳；延迟统计的是采样时刻→提交确认，不是入库间隔。",
                "非存储点/STRING 点只进 latest 与 window，值超级表里不得有它们的列。",
                "P_SOMETIMES_NULL 点是存储点，每 5 个 tick 有 1 个 NULL："
                "列必须存在且那些单元格必须是 NULL。",
                "未验证：浏览器接收→数值应用→图表绘制延迟、额定规模长测、"
                "可靠移交恢复、查询侧完整性过滤、宽帧提速。",
                "三个延迟指标必须分开：实时显示新鲜度（未测）/ 历史入库确认延迟"
                "（本脚本判定）/ Redis 持久交付延迟（collector 计时与批次时间线）。",
                "1000ms 是本轮复核测试预算（来源：现场『页面显示超过 1 秒』），"
                "不是已约定的产品 SLA；判定用阈值内样本占比，不用直方图桶上界。",
            ],
        }
        Path(args.output).write_text(json.dumps(result, ensure_ascii=False, indent=2),
                                     encoding="utf-8")
        append_progress(progress_path, {"token": token, "status": "complete",
                                        "time_ms": int(time.time() * 1000),
                                        "overall_status": result["overall_status"],
                                        "measured_status": result["measured_status"]})
        print(json.dumps({k: v for k, v in result.items() if k != "samples"},
                         ensure_ascii=False), flush=True)
        return result
    except Exception as exc:
        Path(args.output).write_text(json.dumps(
            {"overall_status": "fail", "measured_status": "fail", "database": database,
             "error": str(exc), "generated_frames": generated, "samples": samples},
            ensure_ascii=False, indent=2), encoding="utf-8")
        append_progress(progress_path, {"token": token, "status": "error",
                                        "time_ms": int(time.time() * 1000),
                                        "error": repr(exc),
                                        "generated_frames": generated})
        raise
    finally:
        done.set()
        for task in monitors + [consume_task]:
            task.cancel()
        await asyncio.gather(*monitors, consume_task, return_exceptions=True)
        await asyncio.to_thread(collector.close)
        await td.close()
        mysql.close()
        r.close()


def expected_columns_of(args, new_point: int) -> list[str]:
    return [tdengine_value_column(p) for p in range(1, new_point + 1)
            if p not in NEVER_STORED]


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--devices", type=int, default=5)
    parser.add_argument("--points", type=int, default=100)
    parser.add_argument("--seconds", type=int, default=60)
    parser.add_argument("--period-ms", type=int, default=100)
    parser.add_argument("--drain-seconds", type=int, default=180)
    parser.add_argument("--point-batch", type=int, default=200,
                        help="值表全字段对账时每条 SELECT 的列数")
    parser.add_argument("--latest-devices", type=int, default=3)
    parser.add_argument("--mapping-update-fraction", type=float, default=0.5,
                        help="在第几个 tick 写入新点位映射（占总 tick 比例）")
    parser.add_argument("--p99-budget-ms", type=float, default=DEFAULT_P99_BUDGET_MS)
    parser.add_argument("--output", default="/tmp/pipeline-validation.json")
    args = parser.parse_args()
    if min(args.devices, args.points, args.seconds, args.period_ms,
           args.drain_seconds, args.point_batch) < 1:
        parser.error("all load and duration values must be positive")
    if args.points < 8:
        parser.error("points 必须 >= 8：共享期望模型用 1..8 覆盖 NULL/布尔/负值/"
                     "质量变化/非存储/STRING/时有时无的 NULL")
    logging.basicConfig(level=logging.WARNING)
    outcome = asyncio.run(run(args))
    raise SystemExit(0 if outcome["measured_status"] == PASS else 1)
