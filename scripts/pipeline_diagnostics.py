"""Read-only field diagnostics. Runs in the existing Backend container."""
from __future__ import annotations

import argparse
import base64
from collections import Counter
from datetime import datetime
import json
import os
import re
import sys
import time
import urllib.error
import urllib.request


def error_info(exc: Exception) -> dict:
    # Do not export exception strings: they can contain DSNs or credentials.
    return {"type": type(exc).__name__, "available": False}


def classify_error(text: str) -> str:
    text = text.lower()
    if any(word in text for word in ("0x0219", "statement too long", "sql too long", "permanent size")):
        return "SQL/单帧超过大小限制"
    if any(word in text for word in ("timeout", "timed out", "超时")):
        return "超时"
    if any(word in text for word in ("connection", "unreachable", "broken pipe", "连接")):
        return "连接故障"
    if any(word in text for word in ("schema", "column", "stable", "table does not", "mapping")):
        return "表结构/点位映射"
    if any(word in text for word in ("bad data", "invalid", "parse", "decode", "incomplete")):
        return "数据格式/不完整帧"
    if any(word in text for word in ("saturated", "capacity reached", "dropping cycle")):
        return "采集线程池已满"
    if "flush failed" in text or "backing off" in text:
        return "入库失败退避"
    return "其他（需进一步查看原机日志）"


def http_probe(url: str, data: bytes | None = None, headers: dict | None = None) -> dict:
    started = time.perf_counter()
    try:
        request = urllib.request.Request(url, data=data, headers=headers or {})
        with urllib.request.urlopen(request, timeout=3) as response:
            body = response.read(4097)
            result = {"available": True, "http_status": response.status}
            if data is not None:
                decoded = json.loads(body) if len(body) <= 4096 else {}
                result["code"] = decoded.get("code")
                result["available"] = result["code"] == 0
            return {**result, "elapsed_ms": round((time.perf_counter() - started) * 1000, 3)}
    except urllib.error.HTTPError as exc:
        return {**error_info(exc), "http_status": exc.code,
                "elapsed_ms": round((time.perf_counter() - started) * 1000, 3)}
    except Exception as exc:
        return {**error_info(exc), "elapsed_ms": round((time.perf_counter() - started) * 1000, 3)}


def bounded_scan(r, pattern: str, maximum: int, deadline: float) -> tuple[list, bool]:
    keys, cursor, pages = [], 0, 0
    while True:
        cursor, found = r.scan(cursor, match=pattern, count=500)
        keys.extend(found)
        pages += 1
        if len(keys) > maximum or (cursor and (pages >= 200 or time.monotonic() >= deadline)):
            return sorted(set(keys))[:maximum], True
        if not cursor:
            return sorted(set(keys)), False


def redis_probe(group: str, max_streams: int) -> dict:
    import redis

    r = redis.Redis(
        host=os.environ.get("REDIS_HOST", "redis"), port=int(os.environ.get("REDIS_PORT", "6379")),
        db=int(os.environ.get("REDIS_DB", "0")), password=os.environ.get("REDIS_PASSWORD") or None,
        socket_connect_timeout=2, socket_timeout=2, decode_responses=True,
    )
    result = {"available": False, "group": group, "streams": [], "dlq": []}
    try:
        started = time.perf_counter()
        r.ping()
        result.update(available=True, ping_ms=round((time.perf_counter() - started) * 1000, 3))
        fields = {
            "server": ("redis_version", "run_id", "uptime_in_seconds"),
            "stats": ("total_commands_processed", "instantaneous_ops_per_sec", "rejected_connections", "evicted_keys"),
            "memory": ("used_memory", "used_memory_peak", "maxmemory", "maxmemory_policy"),
            "persistence": ("aof_enabled", "aof_last_write_status", "aof_delayed_fsync", "aof_pending_bio_fsync", "rdb_last_bgsave_status"),
            "clients": ("connected_clients", "blocked_clients"),
            "cpu": ("used_cpu_sys", "used_cpu_user"),
        }
        pipe = r.pipeline(transaction=False)
        for section in fields:
            pipe.info(section)
        pipe.info("commandstats")
        pipe.hgetall("rt:metrics:writer")
        responses = pipe.execute(raise_on_error=False)
        for (section, names), response in zip(fields.items(), responses):
            result[section] = {name: response.get(name) for name in names} if isinstance(response, dict) else error_info(response)
        result["commandstats"] = responses[-2] if isinstance(responses[-2], dict) else {}
        result["writer_counters"] = {k: int(v) for k, v in responses[-1].items() if str(v).isdigit()} if isinstance(responses[-1], dict) else {}
        deadline = time.monotonic() + 4
        keys, truncated = bounded_scan(r, "rt:stream:*", max_streams, deadline)
        result["streams_truncated"] = truncated
        pipe = r.pipeline(transaction=False)
        for key in keys:
            pipe.xinfo_stream(key)
            pipe.xinfo_groups(key)
            pipe.xpending(key, group)
        responses = pipe.execute(raise_on_error=False) if keys else []
        for index, key in enumerate(keys):
            info, groups, pending = responses[index * 3:index * 3 + 3]
            if not isinstance(info, dict) or not isinstance(groups, list):
                result["streams"].append({"key": key, "available": False})
                continue
            selected = next((item for item in groups if item.get("name") == group), {})
            oldest_id = pending.get("min") if isinstance(pending, dict) else None
            oldest_ms = int(oldest_id.split("-")[0]) if oldest_id else None
            result["streams"].append({
                "key": key, "available": True, "length": info.get("length"),
                "entries_added": info.get("entries-added"), "last_id": info.get("last-generated-id"),
                "group_found": bool(selected), "group_count": len(groups),
                "entries_read": selected.get("entries-read"), "lag": selected.get("lag"),
                "pending": selected.get("pending"),
                "oldest_pending_age_ms": max(0, int(time.time() * 1000) - oldest_ms) if oldest_ms else None,
            })
        dlq_keys, truncated = bounded_scan(r, "rt:dlq:*", 16, time.monotonic() + 2)
        result["dlq_truncated"] = truncated
        for key in dlq_keys:
            item = {"key": key, "count": r.llen(key), "recent": []}
            for raw in r.lrange(key, 0, 2):
                try:
                    record = json.loads(raw)
                    item["recent"].append({"failed_at": record.get("failed_at"), "entry_id": record.get("entry_id"),
                                           "retry_count": record.get("retry_count"),
                                           "error_codes": re.findall(r"0x[0-9a-fA-F]{4,8}", str(record.get("last_error", ""))),
                                           "reason": classify_error(str(record.get("last_error", "")))})
                except (ValueError, TypeError):
                    item["recent"].append({"reason": "隔离记录无法解析"})
            result["dlq"].append(item)
        result["slowlog"] = [{"id": entry["id"], "at": entry["start_time"],
                              "duration_ms": entry["duration"] / 1000,
                              "command": (entry["command"].decode("ascii", "replace") if isinstance(entry["command"], bytes) else str(entry["command"])).split(" ", 1)[0]}
                             for entry in r.slowlog_get(5)]
    except Exception as exc:
        result["error"] = error_info(exc)
    finally:
        r.close()
    return result


def sample(group: str, max_streams: int) -> dict:
    started = time.time()
    try:
        redis_result = redis_probe(group or os.environ.get("WRITER_REDIS_GROUP_NAME", "writer_cg"), max_streams)
    except Exception as exc:
        redis_result = error_info(exc)
    scheme = "https" if os.environ.get("TDENGINE_USE_SSL", "false").lower() in {"true", "1", "yes"} else "http"
    td_host = os.environ.get("TDENGINE_HOST", "tdengine")
    td_port = int(os.environ.get("TDENGINE_PORT", "6041"))
    credentials = (os.environ.get("TDENGINE_USER", "root") + ":" + os.environ.get("TDENGINE_PASSWORD", "")).encode()
    td = http_probe(f"{scheme}://{td_host}:{td_port}/rest/sql", b"SELECT SERVER_VERSION()",
                    {"Authorization": "Basic " + base64.b64encode(credentials).decode()})
    return {"at": started, "redis": redis_result, "tdengine_read_probe": td,
            "backend_health_probe": http_probe("http://127.0.0.1:8000/healthz")}


def seconds(value) -> float | None:
    try:
        return datetime.fromisoformat(value.replace("Z", "+00:00")).timestamp()
    except (TypeError, ValueError, AttributeError):
        return None


def counter_rate(records: list[tuple[float, dict]], name: str) -> dict:
    total, span, resets = 0, 0.0, 0
    for (before_at, before), (after_at, after) in zip(records, records[1:]):
        a, b = before.get(name), after.get(name)
        if not all(isinstance(value, (int, float)) for value in (a, b, before_at, after_at)) or after_at <= before_at:
            continue
        if b < a or before.get("_epoch") != after.get("_epoch"):
            resets += 1
            continue
        total += b - a
        span += after_at - before_at
    return {"delta": total if span else None, "per_second": round(total / span, 3) if span else None,
            "observed_seconds": round(span, 3), "resets": resets}


def histogram_window(records: list[tuple[float, dict]], name: str) -> dict:
    count, total = 0, 0.0
    for (_, before), (_, after) in zip(records, records[1:]):
        a, b = before.get(name, {}), after.get(name, {})
        if not a or not b or b.get("count", 0) < a.get("count", 0):
            continue
        delta = b.get("count", 0) - a.get("count", 0)
        if delta > 0:
            count += delta
            total += max(0, b["count"] * b["average"] - a["count"] * a["average"])
    return {"count": count, "total_ms": round(total, 3), "average_ms": round(total / count, 3) if count else None}


def normalize_timings(payload: dict) -> dict:
    source = payload.get("timings", {})
    if payload.get("workers"):
        names = {name for worker in payload["workers"] for name in worker.get("timings", {})}
        source = {}
        for name in names:
            values = [worker["timings"][name] for worker in payload["workers"]
                      if isinstance(worker.get("timings", {}).get(name), dict)
                      and isinstance(worker["timings"][name].get("count"), (int, float))]
            if not values:
                continue
            if name == "event_loop_lag_ms":
                values = values[:1]  # Writer workers share one event loop.
            count = sum(value.get("count", 0) for value in values)
            source[name] = {"count": count, "average": sum(value.get("count", 0) * value.get("average", 0) for value in values) / count if count else 0}
    return {name: {"count": value.get("count", 0), "average": value.get("average", value.get("average_ms", 0))}
            for name, value in source.items()
            if isinstance(value, dict) and isinstance(value.get("count"), (int, float))}


def writer_timing_window(records: list[tuple[float, dict]]) -> dict:
    """New counters are cumulative totals, not count/average histograms.

    Subtract each worker separately before adding totals: one worker restarting
    must not be hidden by the other workers' increasing counters.
    """
    workers, containment = {}, {}
    for at, payload in records:
        for worker in payload.get("workers", [payload]):
            timings = worker.get("timings", {})
            if "stages_ms" not in timings or "executor_ms" not in timings:
                continue
            name = worker.get("consumer_name", "single_process")
            workers.setdefault(name, []).append((at, timings))
            containment = timings.get("stage_containment", containment)
    stage_names = {name for series in workers.values() for _, timing in series
                   for name in timing["stages_ms"]}
    executor_names = {name for series in workers.values() for _, timing in series
                      for name in timing["executor_ms"]}
    fields = ("calls", "queue_ms", "call_ms", "call_cpu_ms", "resume_ms", "total_ms",
              "incomplete_calls", "incomplete_queue_ms", "incomplete_call_ms")
    per_worker = []
    for name, series in sorted(workers.items()):
        stages = {stage: counter_rate(
            [(at, {stage: timing["stages_ms"].get(stage, 0)}) for at, timing in series], stage)
            for stage in sorted(stage_names)}
        executors = {executor: {field: counter_rate(
            [(at, {field: timing["executor_ms"].get(executor, {}).get(field, 0)})
             for at, timing in series], field)
            for field in fields} for executor in sorted(executor_names)}
        per_worker.append({"consumer_name": name, "stages": stages, "executors": executors})

    def summed(values):
        return round(sum(values), 3) if values and all(value is not None for value in values) else None

    stages = {stage: {"total_ms": summed([worker["stages"][stage]["delta"] for worker in per_worker]),
                      "resets": sum(worker["stages"][stage]["resets"] for worker in per_worker)}
              for stage in sorted(stage_names)}
    loop_total = stages.get("loop_iteration_ms", {}).get("total_ms")
    roots = containment.get("loop_iteration_ms", [])
    for name, stage in stages.items():
        stage["loop_percent"] = (round(stage["total_ms"] / loop_total * 100, 2)
                                 if name in roots and loop_total and stage["total_ms"] is not None else None)
    executors = {}
    for name in sorted(executor_names):
        bucket = {field: summed([worker["executors"][name][field]["delta"] for worker in per_worker])
                  for field in fields}
        calls = bucket["calls"]
        bucket["average_ms"] = {field: round(bucket[field] / calls, 3)
                                if calls and bucket[field] is not None else None
                                for field in ("queue_ms", "call_ms", "call_cpu_ms", "resume_ms")}
        bucket["resets"] = max((worker["executors"][name][field]["resets"]
                               for worker in per_worker for field in fields), default=0)
        executors[name] = bucket
    return {"worker_count": len(workers), "stages": stages, "executors": executors,
            "containment": containment, "workers": per_worker}


def summarize_logs(samples: list[dict], start: float, end: float) -> list[dict]:
    paths: dict[str, dict] = {}
    for row in samples:
        for log in row.get("logs", []):
            target = paths.setdefault(log["path"], {"module": log["module"], "path": log["path"], "records": {}, "warnings": {}})
            for record in log.get("snapshots", []):
                at = seconds(record.get("at"))
                if at is not None and start - 90 <= at <= end + 5:
                    payload = record["payload"]
                    target["records"][(at, payload.get("consumer_name", ""), bool(payload.get("aggregate")))] = payload
            for warning in log.get("warnings", []):
                at = seconds(warning.get("at"))
                if at is not None and start <= at <= end + 5:
                    target["warnings"][warning["fingerprint"]] = warning["reason"]
    results = []
    for item in paths.values():
        records = sorted(((key[0], value) for key, value in item.pop("records").items()), key=lambda pair: pair[0])
        aggregates = [(at, payload) for at, payload in records if payload.get("aggregate")]
        scope = "single_process"
        if item["module"] == "writer":
            if aggregates:
                records, scope = aggregates, "all_workers"
            elif len({payload.get("consumer_name") for _, payload in records}) > 1:
                records, scope = [], "mixed_workers_without_aggregate"
        warnings = Counter(item.pop("warnings").values())
        fresh = bool(records and end - records[-1][0] <= 90)
        latest = records[-1][1] if fresh else {}
        counter_records = [(at, payload.get("aggregate", payload).get("counters", {})) for at, payload in records]
        # Only the interval actually observed during capture contributes to stage timings.
        within = [(at, normalize_timings(payload)) for at, payload in records if at >= start]
        names = {name for _, timings in within for name in timings}
        results.append({**item, "fresh": fresh, "scope": scope, "snapshot_count": len(within),
                        "latest_at": records[-1][0] if records else None,
                        "timings": {name: histogram_window(within, name) for name in sorted(names)},
                        "gauges": latest.get("aggregate", latest).get("gauges", {}),
                        "counter_deltas": {name: counter_rate([(at, value) for at, value in counter_records if at >= start], name)
                                           for name in counter_records[-1][1]} if counter_records else {},
                        "warning_categories": dict(warnings),
                        "writer_timing": writer_timing_window(
                            [(at, payload) for at, payload in records if at >= start]
                        ) if item["module"] == "writer" and fresh else {}})
    return results


def summarize(data: dict) -> dict:
    rows = data["samples"]
    start, end = seconds(data["started_at"]), seconds(data["ended_at"])
    probes = [(row["probe"]["at"], row["probe"]["redis"]) for row in rows
              if row.get("probe", {}).get("redis", {}).get("available")]
    streams = {}
    for at, redis_info in probes:
        for stream in redis_info.get("streams", []):
            streams.setdefault(stream["key"], []).append((at, {**stream, "_epoch": redis_info.get("server", {}).get("run_id")}))
    stream_results = []
    latest_keys = {stream["key"] for stream in probes[-1][1].get("streams", [])} if probes else set()
    for key, observations in streams.items():
        if key not in latest_keys:
            continue
        latest = observations[-1][1]
        stream_results.append({**latest, "input_rate": counter_rate(observations, "entries_added"),
                               "read_rate": counter_rate(observations, "entries_read"),
                               "backlog_change": ((latest["lag"] + latest["pending"]) - (observations[0][1]["lag"] + observations[0][1]["pending"]))
                               if len(observations) >= 2 and all(isinstance(s.get(field), int) for s in (latest, observations[0][1]) for field in ("lag", "pending"))
                               and not counter_rate(observations, "entries_added")["resets"] else None})
    stream_results.sort(key=lambda value: value.get("lag") if isinstance(value.get("lag"), (int, float)) else -1, reverse=True)
    last_redis = probes[-1][1] if probes else {}
    last_attempt = rows[-1].get("probe", {}).get("redis", {}) if rows else {}
    all_known = bool(last_attempt.get("available")) and bool(stream_results) and not last_redis.get("streams_truncated") and all(s.get("group_found") and isinstance(s.get("lag"), int) and isinstance(s.get("pending"), int) for s in stream_results)
    total = lambda name: sum(s[name] for s in stream_results) if all_known else None
    rates_known = all_known and all(s["input_rate"]["per_second"] is not None and s["read_rate"]["per_second"] is not None and not s["input_rate"]["resets"] and not s["read_rate"]["resets"] for s in stream_results)
    redis_summary = {"successful_samples": len(probes), "latest": last_redis, "last_attempt": last_attempt,
                     "last_valid_at": probes[-1][0] if probes else None,
                     "total_lag": total("lag"), "total_pending": total("pending"),
                     "input_frames_per_s": round(sum(s["input_rate"]["per_second"] for s in stream_results), 3) if rates_known else None,
                     "new_read_frames_per_s": round(sum(s["read_rate"]["per_second"] for s in stream_results), 3) if rates_known else None,
                     "backlog_change": sum(s["backlog_change"] for s in stream_results) if all_known and all(s["backlog_change"] is not None for s in stream_results) else None,
                     "streams": stream_results}
    logs = summarize_logs(rows, start, end)
    findings, gaps = [], []
    if len(probes) < 2:
        gaps.append("Redis 有效采样不足两次，无法计算生产/消费速度。")
    if not last_attempt.get("available") or last_attempt.get("error"):
        failure = last_attempt.get("error", last_attempt).get("type", "probe_unavailable")
        gaps.append(f"最后一次 Redis 探测不可用或部分失败（{failure}），请检查容器连接配置、网络和权限；历史成功样本不能证明当前正常。")
    if not all_known:
        gaps.append("Stream 缺失、未发现 Writer 消费组、lag 未知或扫描达到上限；总积压不能按零处理。")
    if rates_known and redis_summary["input_frames_per_s"] > redis_summary["new_read_frames_per_s"]:
        findings.append("观察期间进入 Stream 的新帧多于 Writer 新领取的帧，消费端存在吞吐缺口；不直接等同于 TDengine 慢。")
    if (redis_summary["backlog_change"] or 0) > 0:
        findings.append(f"未领取 + 未确认积压净增加 {redis_summary['backlog_change']} 帧，当前处理没有追上生产。")
    dlq_counts = [(at, {"count": sum(d["count"] for d in info.get("dlq", []))}) for at, info in probes if "dlq" in info and not info.get("dlq_truncated") and "error" not in info]
    dlq_rate = counter_rate(dlq_counts, "count")
    if (dlq_rate["delta"] or 0) > 0:
        findings.append(f"失败隔离队列净增加 {dlq_rate['delta']} 条，应先排查失败原因，扩大缓存不能修复这些失败。")
    persistence = last_redis.get("persistence", {})
    if persistence.get("aof_last_write_status") == "err" or persistence.get("rdb_last_bgsave_status") == "err":
        findings.append("Redis 持久化状态报错，应检查磁盘与容器日志。")
    fsync = counter_rate([(at, info.get("persistence", {})) for at, info in probes], "aof_delayed_fsync")
    if (fsync["delta"] or 0) > 0:
        findings.append(f"Redis 延迟 fsync 计数增加 {fsync['delta']}，存在磁盘同步压力证据。")
    for module in ("collector", "writer"):
        active = [log for log in logs if log["module"] == module and log["fresh"] and log["snapshot_count"] >= 2]
        if not active:
            gaps.append(f"{module} 没有两条新鲜性能快照，无法拆分其内部耗时；检查日志路径/运行版本，或延长观察时间。")
        for log in active:
            timings = log["timings"]
            stages = ("read_ms", "parse_ms", "redis_ms") if module == "collector" else ("parse_ms", "build_ms", "build_wait_ms", "td_execute_ms", "ack_ms")
            measured = [(name, timings[name]) for name in stages if timings.get(name, {}).get("average_ms") is not None]
            if measured:
                name, value = max(measured, key=lambda pair: pair[1]["total_ms"])
                findings.append(f"{module} 已测阶段中 {name} 累计用时最高（{value['total_ms']} ms，单次平均 {value['average_ms']} ms），可优先排查；累计用时包含并发调用，不等于墙钟时间。")
            failures = {name: value["delta"] for name, value in log["counter_deltas"].items()
                        if any(word in name for word in ("retry_", "error_count", "reject_count", "deadline_miss", "inflight_skip")) and (value["delta"] or 0) > 0}
            if failures:
                findings.append(f"{module} 本次新增失败/重试/跳周期计数：{json.dumps(failures, ensure_ascii=False)}。")
    if not findings:
        findings.append("当前证据未定位到明确慢环节；这不代表全流程性能合格，请在真实积压增长时采样。")
    return {"schema_version": 1, "started_at": data["started_at"], "ended_at": data["ended_at"],
            "sample_count": len(rows), "findings": findings, "evidence_gaps": gaps,
            "redis": redis_summary, "dlq_net_growth": dlq_rate, "redis_delayed_fsync": fsync,
            "logs": logs, "resources": resource_summary(rows),
            "limits": ["TDengine SELECT 探测只表示连接/查询延迟；Writer td_execute_ms 包含线程等待和事件循环恢复，不是服务端纯执行时间。",
                       "Redis PING 从 Backend 容器发起，不等于 Collector 到 Redis 的网络延迟。",
                       "PLC 耗时由 Collector 内部统计；没有端到端帧关联，不能把并发阶段平均值相加当成总延迟。",
                       "分布式部署需在边缘机同时采集 Collector/Storage/Uploader 日志；本报告不把远端未知环节判为正常。"]}


def resource_summary(rows: list[dict]) -> dict:
    processes = {}
    docker = {}
    read_ms, write_ms = [], []
    for row in rows:
        at = seconds(row.get("at"))
        for process in row.get("processes", []):
            identity = (process["name"], process["pid"], process.get("started_at"))
            processes.setdefault(identity, []).append((at, process))
        for container in row.get("docker", []):
            try:
                docker.setdefault(container["Name"], []).append(float(container["CPUPerc"].rstrip("%")))
            except (KeyError, ValueError):
                pass
    for before, after in zip(rows, rows[1:]):
        a, b = before.get("disk_raw", []), after.get("disk_raw", [])
        if not a or not b:
            continue
        for field, dest in (("AvgDisksecPerRead", read_ms), ("AvgDisksecPerWrite", write_ms)):
            delta = int(b[0][field]) - int(a[0][field])
            base = int(b[0][field + "_Base"]) - int(a[0][field + "_Base"])
            frequency = int(b[0]["Frequency_PerfTime"])
            if delta >= 0 and base > 0 and frequency > 0:
                dest.append(round(delta / frequency / base * 1000, 3))
    process_results = []
    for (name, pid, _), observations in processes.items():
        rates = counter_rate(observations, "cpu_seconds")
        process_results.append({"name": name, "pid": pid,
                                "cpu_percent_one_core": round(rates["per_second"] * 100, 2) if rates["per_second"] is not None else None,
                                "peak_rss_mb": round(max(item["rss_bytes"] for _, item in observations) / 1048576, 2)})
    memory = [row["host"]["free_memory_bytes"] for row in rows if row.get("host", {}).get("free_memory_bytes") is not None]
    queues = [disk["CurrentDiskQueueLength"] for row in rows for disk in row.get("disk", []) if disk.get("CurrentDiskQueueLength") is not None]
    return {"processes": process_results,
            "container_cpu_average_percent": {name: round(sum(values) / len(values), 2) for name, values in docker.items()},
            "min_free_memory_mb": round(min(memory) / 1048576, 2) if memory else None,
            "max_disk_queue": max(queues) if queues else None,
            "max_interval_disk_read_ms": max(read_ms) if read_ms else None,
            "max_interval_disk_write_ms": max(write_ms) if write_ms else None,
            "tdengine_read_probes": [row["probe"]["tdengine_read_probe"] for row in rows if row.get("probe", {}).get("tdengine_read_probe")],
            "backend_health_probes": [row["probe"]["backend_health_probe"] for row in rows if row.get("probe", {}).get("backend_health_probe")]}


def display(value) -> str:
    return "无法判断" if value is None else str(value)


def markdown(report: dict) -> str:
    redis_info = report["redis"]
    lines = ["# Data Watch 全流程性能诊断", "", f"观察时间：{report['started_at']} → {report['ended_at']}",
             "", "## 判断与证据", ""] + ["- " + item for item in report["findings"]]
    lines += ["", "## 流量与积压", "", "同一口径：一条 Redis Stream 消息 = 一帧，不能与点位数混算。", "",
              f"- 进入 Redis：{display(redis_info['input_frames_per_s'])} 帧/秒",
              f"- Writer 新领取：{display(redis_info['new_read_frames_per_s'])} 帧/秒（不含重试领取，不等于成功入库）",
              f"- 尚未领取：{display(redis_info['total_lag'])} 帧；已领取未确认：{display(redis_info['total_pending'])} 帧",
              f"- 未领取 + 未确认积压净变化：{display(redis_info['backlog_change'])} 帧",
              f"- DLQ 观察期间净增长：{display(report['dlq_net_growth']['delta'])} 条（已有 DLQ 数量不等于本次失败数）",
              f"- Redis 成功采样：{redis_info['successful_samples']}/{report['sample_count']}；最后一次可用：{redis_info['last_attempt'].get('available', False)}",
              "", "| Stream | 未领取 | 未确认 | 进入帧/秒 | 新领取帧/秒 | 最老 PEL 消息年龄(ms) |",
              "| --- | ---: | ---: | ---: | ---: | ---: |"]
    for stream in redis_info["streams"]:
        lines.append(f"| {stream['key']} | {display(stream.get('lag'))} | {display(stream.get('pending'))} | {display(stream['input_rate']['per_second'])} | {display(stream['read_rate']['per_second'])} | {display(stream.get('oldest_pending_age_ms'))} |")
    lines += ["", "## 内部耗时", "", "以下为观察窗口中新操作的平均耗时；每行操作次数不同，不是累计 P95，也不能直接相加。", ""]
    for log in report["logs"]:
        lines += [f"### {log['module']}", "", f"日志：`{log['path']}`；新鲜：{log['fresh']}；窗口快照：{log['snapshot_count']}；统计范围：{log['scope']}",
                  "", "| 阶段 | 操作次数 | 平均耗时(ms) | 累计用时(ms) |", "| --- | ---: | ---: | ---: |"]
        labels = {"read_ms": "PLC 读取", "parse_ms": "点位解析", "redis_io_ms": "Redis 命令通信", "redis_ms": "采集存储阶段（含排队）",
                  "json_ms": "JSON 编码", "build_ms": "Writer SQL 构建", "build_wait_ms": "SQL 构建线程排队",
                  "td_execute_ms": "TDengine 执行写入", "ack_ms": "Redis 确认/删除", "event_loop_lag_ms": "事件循环延迟",
                  "deadline_lag_ms": "采集任务启动迟到", "execution_ms": "单次采集总过程", "flush_ms": "Writer 刷新过程"}
        for name, timing in log["timings"].items():
            lines.append(f"| {labels.get(name, name)} ({name}) | {timing['count']} | {display(timing['average_ms'])} | {timing['total_ms']} |")
        landed = log["counter_deltas"].get("committed_value_rows", {})
        if landed:
            lines += ["", f"完整确认入库：{display(landed.get('per_second'))} 帧/秒；计数重置：{landed.get('resets', 0)}。"]
        writer_timing = log.get("writer_timing", {})
        if writer_timing.get("worker_count"):
            stage_labels = {"loop_iteration_ms": "循环总量", "read_block_ms": "领取、准备与入缓冲",
                            "read_claim_ms": "Redis 领取（含等待）", "prepare_ms": "批量解析与转换",
                            "handle_entry_ms": "逐条入缓冲", "drain_own_ms": "PEL 回收（扣除刷新）",
                            "overdelivered_own_ms": "溢出回收（扣除刷新）", "flush_outer_ms": "刷新总过程",
                            "ack_retry_outer_ms": "ACK 重试", "delete_retry_outer_ms": "删除重试",
                            "idle_ms": "空转", "loop_other_ms": "循环其余时间"}
            lines += ["", "Writer 循环分解（各 Worker 墙钟时间之和，不是主机 CPU 时间）：", "",
                      "| 阶段 | 窗口累计(ms) | 占循环比例(%) | 跳过的计数重置 |",
                      "| --- | ---: | ---: | ---: |"]
            for name, stage in writer_timing["stages"].items():
                lines.append(f"| {stage_labels.get(name, name)} ({name}) | {display(stage['total_ms'])} | {display(stage['loop_percent'])} | {stage['resets']} |")
            lines += ["", "只有循环直属阶段显示占比；回收 own 阶段已扣除内部刷新，outer 阶段仅作辅助观察。",
                      "父阶段包含子阶段，不跨层相加；窗口边界可能落在进行中的迭代。", "",
                      "Writer 执行器拆分（仅完整调用进入均值；同步调用包含网络、驱动与结果处理）：", "",
                      "| 调用 | 完整次数 | 排队均值(ms) | 调用均值(ms) | 线程CPU均值(ms) | 恢复均值(ms) | 未完成次数 |",
                      "| --- | ---: | ---: | ---: | ---: | ---: | ---: |"]
            for name, bucket in writer_timing["executors"].items():
                average = bucket["average_ms"]
                lines.append(f"| {name} | {display(bucket['calls'])} | {display(average['queue_ms'])} | {display(average['call_ms'])} | {display(average['call_cpu_ms'])} | {display(average['resume_ms'])} | {display(bucket['incomplete_calls'])} |")
            lines += ["", "redis_* 是 ACK/重试线程内的同步子命令，没有额外线程池跳转，也不能与外层 ack 相加。",
                      "线程 CPU 时钟在 Windows 上粒度较粗，短调用的零值不能证明没有 CPU 开销。",
                      "逐 Worker 的计数增量、重置及未完成等待保留在 summary.json 的 writer_timing.workers 中。"]
        elif log["module"] == "writer":
            lines += ["", "Writer 新阶段指标无法判断：需新版 Writer、新鲜日志及至少两次性能快照。"]
        lines += ["", f"最新队列/缓存：`{json.dumps(log['gauges'], ensure_ascii=False)}`",
                  "", f"窗口内警告/错误类别（有限尾部样本）：`{json.dumps(log['warning_categories'], ensure_ascii=False)}`", ""]
    reasons = Counter(record["reason"] for item in redis_info["latest"].get("dlq", []) for record in item["recent"])
    lines += ["## 失败隔离原因抽样", "", f"每个队列最多取最近 3 条，仅保留原因类别：`{json.dumps(reasons, ensure_ascii=False)}`",
              "", "## 主机与容器负载", ""]
    resources = report["resources"]
    lines += [f"- 最少可用内存：{display(resources['min_free_memory_mb'])} MB",
              f"- 磁盘队列最高：{display(resources['max_disk_queue'])}；采样区间平均读/写耗时的最高值：{display(resources['max_interval_disk_read_ms'])}/{display(resources['max_interval_disk_write_ms'])} ms",
              f"- 容器平均 CPU：`{json.dumps(resources['container_cpu_average_percent'], ensure_ascii=False)}`",
              "", "| 本机进程 | PID | 平均 CPU（单核=100%） | 峰值内存(MB) |", "| --- | ---: | ---: | ---: |"]
    for process in resources["processes"]:
        lines.append(f"| {process['name']} | {process['pid']} | {display(process['cpu_percent_one_core'])} | {process['peak_rss_mb']} |")
    for name, key in (("TDengine 只读连接探测", "tdengine_read_probes"), ("Backend 健康接口探测", "backend_health_probes")):
        probes = resources[key]
        lines += ["", f"{name}：成功 {sum(bool(p.get('available')) for p in probes)}/{len(probes)} 次；最慢 {display(max((p.get('elapsed_ms', 0) for p in probes), default=None))} ms。"]
    lines += ["", "## 证据缺口", ""]
    lines += ["- " + item for item in report["evidence_gaps"]] or ["- 中心采集与入库主要指标可用；仍需结合业务扫描周期判断是否达标。"]
    lines += ["", "## 解释边界", ""] + ["- " + item for item in report["limits"]]
    lines += ["", "CPU、内存、磁盘队列、容器负载及只读探测详情见 samples.jsonl 和 summary.json。", ""]
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("sample", "report"))
    parser.add_argument("--group", default="")
    parser.add_argument("--max-streams", type=int, default=64)
    args = parser.parse_args()
    if args.mode == "sample":
        result = sample(args.group, args.max_streams)
    else:
        report = summarize(json.load(sys.stdin))
        result = {"summary": report, "markdown": markdown(report)}
    print(json.dumps(result, ensure_ascii=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
