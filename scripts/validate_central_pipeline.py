"""Isolated real-CollectorApp A/B pipeline validation.

Run on the Windows host with PowerShell:
  python scripts/validate_central_pipeline.py --evidence outputs/central-ab-20260928 --rounds smoke
  python scripts/validate_central_pipeline.py --evidence outputs/central-ab-20260928 --rounds abba
  python scripts/validate_central_pipeline.py --evidence outputs/central-ab-20260928 --rounds soak

Only S7 device I/O is simulated. CollectorApp's default storage choice, registry,
planner, scheduler, executor, decoder, Redis writer and production TD writer run.
The A main.py is loaded from 983ba9a; all other source is the same working tree.
"""
from __future__ import annotations

import argparse
import atexit
import asyncio
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import statistics
import struct
import subprocess
import sys
import threading
import time
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
PREFIX = "dwv-central-ab-20260928"
COLLECTOR_IMAGE = "data-watch-collector-test:20260925"
RUNNER_IMAGE = "data-watch-pipeline-runner:20260925"
SHARDS = 4
OWNER_LABEL = "datawatch.central-ab-owner"


def command(*argv: str, check: bool = True) -> subprocess.CompletedProcess:
    result = subprocess.run(argv, capture_output=True, text=True, encoding="utf-8",
                            errors="replace")
    if check and result.returncode:
        raise RuntimeError(f"command failed ({result.returncode}): {' '.join(argv)}\n"
                           f"{result.stdout[-2000:]}\n{result.stderr[-2000:]}")
    return result


def docker(*argv: str, check: bool = True) -> subprocess.CompletedProcess:
    return command("docker", *argv, check=check)


def write_json(path: Path, value: dict) -> None:
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding="utf-8")


def cpu_seconds(container: str) -> float | None:
    found = docker("exec", container, "cat", "/sys/fs/cgroup/cpu.stat", check=False)
    if found.returncode:
        return None
    match = re.search(r"^usage_usec\s+(\d+)$", found.stdout, re.MULTILINE)
    return int(match.group(1)) / 1e6 if match else None


def owned_resource(kind: str, name: str, owner: str) -> bool:
    if kind == "network":
        result = docker("network", "inspect", "--format",
                        '{{ index .Labels "datawatch.central-ab-owner" }}', name, check=False)
    else:
        result = docker("inspect", "--format",
                        '{{ index .Config.Labels "datawatch.central-ab-owner" }}', name,
                        check=False)
    if result.returncode:
        return False
    if result.stdout.strip() != owner:
        raise RuntimeError(f"Refusing to use pre-existing {kind} {name}: owner label mismatch")
    return True


def percentile(values: list[float], p: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    return round(ordered[min(len(ordered) - 1, int((len(ordered) - 1) * p))], 2)


def read_complete_probes(path: Path, observed: int) -> tuple[list[dict], int, int]:
    try:
        contents = path.read_text(encoding="utf-8")
    except OSError:
        return [], observed, 1
    # A Windows bind mount may expose a partial final line during append.
    complete = contents[:contents.rfind("\n") + 1] if "\n" in contents else ""
    lines = complete.splitlines()
    return [json.loads(line) for line in lines[observed:]], max(observed, len(lines)), 0


def container_mode_collect(args: argparse.Namespace) -> int:
    run_dir = Path(args.plan).parent
    native_root = Path("/tmp/ab-data")
    shutil.copytree(run_dir / "data" / "drivers" / "s7", native_root / "drivers" / "s7")
    shutil.copytree(run_dir / "data" / "driver-catalog", native_root / "driver-catalog")
    sys.path[:0] = [str(ROOT), str(ROOT / "collector")]
    import redis
    from core.config import CollectorConfig, DeviceConfig, RedisConfig, VariableConfig
    from main import CollectorApp
    from storage.durable_redis_writer import DurableRedisWriter
    from storage.redis_writer import RedisWriter
    from data_watch_shared.stream_envelope import stream_key

    plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    devices = plan["devices"]
    points = plan["points"]
    group = plan["group"]
    config = CollectorConfig(
        redis=RedisConfig(host="perf-redis", port=6379, db=0,
                          stream_shard_count=SHARDS, stream_maxlen=0),
        devices=[DeviceConfig(d, plan["device_type"], "s7", f"10.99.0.{i + 1}",
                              scan_rate=plan["period_ms"] / 1000)
                 for i, d in enumerate(devices)],
        variables=[VariableConfig(str(p), d, f"DB100.DBW{2 * (p - 1)}", "INT",
                                  plan["period_ms"] / 1000, True, p, group)
                   for d in devices for p in range(1, points + 1)],
    )
    client = redis.Redis(host="perf-redis", port=6379, decode_responses=True,
                         socket_timeout=20)
    assert client.ping()
    app = CollectorApp(config)
    expected_writer = DurableRedisWriter if args.variant == "A" else RedisWriter
    if type(app.redis_writer) is not expected_writer:
        raise AssertionError(f"{args.variant}: default writer is {type(app.redis_writer).__name__}")

    registration = app.device_manager._resolve_driver_registry().get("s7")
    if registration is None:
        raise RuntimeError("activated S7 driver not found")
    driver_class = registration.factory.__globals__["S7Driver"]
    point_bytes = points * 2
    io = {"connect": 0, "read": 0, "disconnect": 0}
    reads_by_device = {d: 0 for d in devices}
    device_by_ip = {f"10.99.0.{i + 1}": d for i, d in enumerate(devices)}

    async def connect(driver, _config):
        driver._connected = True
        driver._ab_tick = 0
        driver._ab_device = device_by_ip[_config["ip"]]
        io["connect"] += 1

    async def read(driver, blocks):
        tick = driver._ab_tick
        driver._ab_tick += 1
        io["read"] += 1
        reads_by_device[driver._ab_device] += 1
        # A new deterministic value each read; the installed S7 planner/decoder
        # must reconstruct all point values from its real merged read block.
        raw = b"".join(struct.pack(">h", (tick + p) % 30000)
                       for p in range(1, points + 1))
        result = {}
        for block in blocks:
            offset = int(block.address.split(".DBB", 1)[1])
            result[block.address] = raw[offset:offset + block.count]
        if any(int(b.address.split(".DBB", 1)[1]) + b.count > point_bytes for b in blocks):
            raise AssertionError("S7 plan exceeds simulated device bytes")
        return result

    async def disconnect(driver):
        driver._connected = False
        io["disconnect"] += 1

    async def ping(_driver):
        return True

    driver_class.connect = connect
    driver_class.read = read
    driver_class.disconnect = disconnect
    driver_class.ping = ping

    # Probe a bounded subset of *actual* frames at the storage call boundary.
    # The original method still executes; the monitor verifies each exact ts in TD.
    original_write = app.redis_writer.write_window_stream
    probe_file = Path(args.probes)
    probe_file.parent.mkdir(parents=True, exist_ok=True)
    probe_stream = probe_file.open("w", encoding="utf-8", buffering=1)
    probe_lock = threading.Lock()
    probe_count = {d: 0 for d in devices[:2]}
    probe_emitted = 0
    accepted_by_device = {d: 0 for d in devices}
    last_value_by_device = {}

    def measured_write(device_code, device_type, group_code, rate, ts, version,
                       frame, values, qualities=None):
        nonlocal probe_emitted
        result = original_write(device_code, device_type, group_code, rate, ts,
                                version, frame, values, qualities)
        accepted_by_device[device_code] += 1
        last_value_by_device[device_code] = values.get("1")
        if device_code in probe_count:
            probe_count[device_code] += 1
            if probe_count[device_code] % 10 == 1:
                first_value = values.get("1")
                with probe_lock:
                    probe_stream.write(json.dumps({"device": device_code, "ts": ts,
                                                   "first_value": first_value}) + "\n")
                    probe_emitted += 1
        return result

    app.redis_writer.write_window_stream = measured_write
    keys = sorted({stream_key(d, plan["device_type"], group, SHARDS) for d in devices})
    samples = []
    async def run():
        service = asyncio.create_task(app.start())
        try:
            deadline = time.monotonic() + 30
            while app._performance_task is None:
                if service.done():
                    await service
                    raise RuntimeError("CollectorApp ended before startup")
                if time.monotonic() > deadline:
                    raise TimeoutError("CollectorApp startup timed out")
                await asyncio.sleep(.05)
            before = app.performance_metrics.snapshot()
            cpu_before = client.info("cpu")
            stats_before = client.info("commandstats")
            process_before = time.process_time()
            started = time.monotonic()
            (Path(args.output).parent / "feed.start").write_text(str(time.time()), encoding="ascii")
            stop_reason = "duration_elapsed"
            while time.monotonic() - started < plan["seconds"]:
                if args.stop_file and Path(args.stop_file).exists():
                    stop_reason = "requested"
                    break
                await asyncio.sleep(min(1.0, max(0, plan["seconds"] - (time.monotonic() - started))))
                snap = app.performance_metrics.snapshot()
                backlog = sum(client.xlen(k) for k in keys)
                pending = 0
                for key in keys:
                    try:
                        pending += sum(int(g["pending"]) for g in client.xinfo_groups(key))
                    except redis.ResponseError:
                        pass
                latest = client.hget(f"rt:latest:{devices[0]}:{group}", "1")
                age = None if latest is None else round(time.time() * 1000 - json.loads(latest)["t"], 1)
                outbox = (app.redis_writer.outbox.stats()
                          if isinstance(app.redis_writer, DurableRedisWriter) else None)
                samples.append({"elapsed": round(time.monotonic() - started, 3),
                                "metrics": snap, "backlog": backlog, "pending": pending,
                                "latest_age_ms": age,
                                "outbox_count": outbox.get("count") if outbox else 0})
                if service.done():
                    await service
                    raise RuntimeError("CollectorApp stopped during measurement")
            duration = time.monotonic() - started
            end = app.performance_metrics.snapshot()
            process_after = time.process_time()
            cpu_after = client.info("cpu")
            stats_after = client.info("commandstats")
            (Path(args.output).parent / "feed.end").write_text(str(time.time()), encoding="ascii")
            # Exercise the production stop path, including its in-flight drain.
            app.stop()
            await asyncio.wait_for(service, timeout=240)
            outbox_drain = None
            if isinstance(app.redis_writer, DurableRedisWriter):
                drain_started = time.monotonic()
                while time.monotonic() - drain_started < args.drain_seconds:
                    if app.redis_writer.outbox.stats()["count"] == 0:
                        break
                    await asyncio.sleep(.5)
                outbox_drain = {"seconds": round(time.monotonic() - drain_started, 3),
                                "remaining": app.redis_writer.outbox.stats()["count"]}
        finally:
            app.stop()
            try:
                if not service.done():
                    await asyncio.wait_for(service, timeout=240)
            finally:
                probe_stream.close()
        return (before, end, duration, outbox_drain, cpu_before, cpu_after,
                stats_before, stats_after, process_after - process_before,
                stop_reason, app.performance_metrics.snapshot())

    try:
        (before, end, duration, outbox_drain, cpu_before, cpu_after,
         stats_before, stats_after, collector_cpu_seconds, stop_reason,
         shutdown_metrics) = asyncio.run(run())
    except Exception as exc:
        write_json(Path(args.output), {"status": "error", "shutdown_complete": False,
                                      "reason": str(exc)})
        raise
    counter = lambda snap, name: int(snap["counters"].get(name, 0))
    success = counter(end, "read_success_count") - counter(before, "read_success_count")
    errors = {name: counter(shutdown_metrics, name) - counter(before, name)
              for name in ("read_error_count", "redis_write_error_count",
                           "deadline_miss_count", "plc_read_executor_reject_count",
                           "parse_executor_reject_count", "redis_executor_reject_count",
                           "scheduler_drain_timeout_count", "scheduler_cancel_timeout_count")}
    cmd_delta = {name: int(value.get("calls", 0)) - int(stats_before.get(name, {}).get("calls", 0))
                 for name, value in stats_after.items()}
    outbox_path = native_root / "runtime" / "collector-retry.db"
    result = {"variant": args.variant, "duration": round(duration, 3),
              "stop_reason": stop_reason,
              "planned_duration_complete": stop_reason == "duration_elapsed",
              "shutdown_complete": True,
              "python": sys.version.split()[0],
              "gil_enabled": getattr(sys, "_is_gil_enabled", lambda: None)(),
              "writer_class": type(app.redis_writer).__name__, "writer_path": app.redis_writer.storage_path,
              "read_success": success, "actual_fps": round(success / duration, 2),
              "target_fps": len(devices) * 1000 / plan["period_ms"],
              "attainment": round(success / duration / (len(devices) * 1000 / plan["period_ms"]), 4),
              "errors": errors, "io": io, "samples": samples,
              "reads_by_device": reads_by_device,
              "accepted_by_device": accepted_by_device,
              "last_value_by_device": last_value_by_device,
              "expected_probes": probe_emitted,
              "final_metrics": end,
              "redis_cpu_seconds": round(float(cpu_after["used_cpu_sys"]) + float(cpu_after["used_cpu_user"])
                                         - float(cpu_before["used_cpu_sys"]) - float(cpu_before["used_cpu_user"]), 3),
              "redis_main_cpu_seconds": (None if "used_cpu_user_main_thread" not in cpu_after else
                                         round(float(cpu_after["used_cpu_user_main_thread"])
                                               + float(cpu_after["used_cpu_sys_main_thread"])
                                               - float(cpu_before["used_cpu_user_main_thread"])
                                               - float(cpu_before["used_cpu_sys_main_thread"]), 3)),
              "collector_cpu_seconds": round(collector_cpu_seconds, 3),
              "redis_commands": {k: v for k, v in cmd_delta.items() if v},
              "outbox_drain": outbox_drain,
              "native_outbox_bytes": outbox_path.stat().st_size if outbox_path.exists() else None,
              "probes_file": str(probe_file)}
    write_json(Path(args.output), result)
    print(json.dumps({k: v for k, v in result.items() if k not in ("samples", "final_metrics")}), flush=True)
    client.close()
    return 0


def container_mode_monitor(args: argparse.Namespace) -> int:
    import taosws
    plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    conn = taosws.connect(host="perf-td", port=6041, user="root", password="taosdata",
                          database=plan["mysql_database"])
    probe_file = Path(args.probes)
    pending: list[dict] = []
    latency: list[float] = []
    observed = 0
    read_retries = 0
    started = time.monotonic()
    after_stop = None
    try:
        while time.monotonic() - started < plan["seconds"] + args.drain_seconds + 40:
            if probe_file.exists():
                newly_read, observed, retries = read_complete_probes(probe_file, observed)
                pending.extend(newly_read)
                read_retries += retries
            remaining = []
            for sample in pending:
                try:
                    found = int(list(conn.query(
                        f"SELECT COUNT(*) FROM `{sample['device']}` WHERE ts = {int(sample['ts'])}"))[0][0])
                except Exception:
                    found = 0
                if found:
                    latency.append(max(0.0, time.time() * 1000 - sample["ts"]))
                else:
                    remaining.append(sample)
            pending = remaining
            if Path(args.stop_file).exists():
                after_stop = after_stop or time.monotonic()
                if not pending or time.monotonic() - after_stop >= args.drain_seconds:
                    break
            time.sleep(.25)
    finally:
        conn.close()
    result = {"probed": observed, "confirmed": len(latency), "missing": len(pending),
              "probe_read_retries": read_retries,
              "sampled_td_confirmation_ms": {"p50": percentile(latency, .5),
                                             "p95": percentile(latency, .95),
                                             "max": percentile(latency, 1.0)},
              "sampling_note": "Exact collector frame ts found in TD; 250ms polling adds measurement delay."}
    write_json(Path(args.output), result)
    print(json.dumps(result), flush=True)
    return 0


def container_mode_reconcile(args: argparse.Namespace) -> int:
    sys.path[:0] = [str(ROOT), str(ROOT / "collector"), str(ROOT / "writer")]
    import redis
    import taosws
    from data_watch_shared.acceptance import tdengine_value_column
    from data_watch_shared.stream_envelope import stream_key

    plan = json.loads(Path(args.plan).read_text(encoding="utf-8"))
    collector = json.loads(Path(args.collector_output).read_text(encoding="utf-8"))
    monitor = json.loads(Path(args.monitor_output).read_text(encoding="utf-8"))
    r = redis.Redis(host="perf-redis", port=6379, decode_responses=True)
    conn = taosws.connect(host="perf-td", port=6041, user="root", password="taosdata",
                          database=plan["mysql_database"])
    problems = []
    per_device = {}
    quality_rows = 0
    bad_quality = 0
    sampled_values = []
    try:
        for device in plan["devices"]:
            try:
                count = int(list(conn.query(f"SELECT COUNT(*) FROM `{device}`"))[0][0])
                q_count = int(list(conn.query(f"SELECT COUNT(*) FROM `{device}_q`"))[0][0])
                bad_quality += int(list(conn.query(
                    f"SELECT COUNT(*) FROM `{device}_q` WHERE quality != 0"))[0][0])
                quality_rows += q_count
                per_device[device] = count
                expected = collector["reads_by_device"].get(device)
                if count != expected:
                    problems.append(f"{device} TD rows={count}, device reads={expected}")
                accepted = collector["accepted_by_device"].get(device)
                if accepted != expected:
                    problems.append(f"{device} accepted={accepted}, device reads={expected}")
            except Exception as exc:
                per_device[device] = None
                problems.append(f"{device} TD count failed: {exc}")
        for device in plan["devices"][:2]:
            columns = [tdengine_value_column(p) for p in (1, plan["points"] // 2, plan["points"])]
            select = ", ".join(f"`{c}`" for c in columns)
            rows = list(conn.query(f"SELECT {select} FROM `{device}` ORDER BY ts LIMIT 2"))
            rows += list(conn.query(f"SELECT {select} FROM `{device}` ORDER BY ts DESC LIMIT 2"))
            sampled_values.extend({"device": device, "values": [float(v) for v in row]} for row in rows)
            for row in rows:
                first = int(float(row[0]))
                expected = [((first - 1 + p) % 30000) for p in (1, plan["points"] // 2, plan["points"])]
                if any(abs(float(v) - e) > 1e-6 for v, e in zip(row, expected)):
                    problems.append(f"{device}: sampled point values differ from simulated S7 input")
    finally:
        conn.close()
    stream_keys = {stream_key(d, plan["device_type"], plan["group"], SHARDS)
                   for d in plan["devices"]}
    backlog = sum(r.xlen(key) for key in stream_keys)
    pending = 0
    for key in stream_keys:
        try:
            pending += sum(int(g["pending"]) for g in r.xinfo_groups(key))
        except redis.ResponseError:
            pass
    dlq = sum(r.llen(key) for key in r.scan_iter("rt:dlq:*"))
    latest = {d: r.hlen(f"rt:latest:{d}:{plan['group']}") for d in plan["devices"]}
    total_rows = sum(count or 0 for count in per_device.values())
    if backlog or pending or dlq:
        problems.append(f"stream backlog={backlog}, PEL={pending}, DLQ={dlq}")
    if any(count != plan["points"] for count in latest.values()):
        problems.append("latest hash point count mismatch")
    if quality_rows != total_rows * plan["points"]:
        problems.append(f"quality rows {quality_rows} != {total_rows * plan['points']}")
    if bad_quality:
        problems.append(f"nonzero quality rows={bad_quality}")
    for device, count in latest.items():
        if count != plan["points"]:
            continue
        latest_value = json.loads(r.hget(f"rt:latest:{device}:{plan['group']}", "1"))["v"]
        expected = collector["last_value_by_device"].get(device)
        if latest_value != expected:
            problems.append(f"{device} latest p1={latest_value}, expected={expected}")
    if monitor["missing"]:
        problems.append(f"TD confirmation probes missing={monitor['missing']}")
    if monitor["probed"] != collector["expected_probes"] or not monitor["probed"]:
        problems.append(f"probe count monitor={monitor['probed']} != collector={collector['expected_probes']}")
    failures = {k: v for k, v in collector["errors"].items()
                if k != "deadline_miss_count" and v}
    if failures:
        problems.append(f"collector errors={failures}")
    if collector["attainment"] < .95:
        problems.append(f"input attainment {collector['attainment']:.3f} < .95")
    if not collector.get("shutdown_complete"):
        problems.append("collector shutdown did not complete")
    if not collector.get("planned_duration_complete"):
        problems.append(f"measurement stopped early: {collector.get('stop_reason')}")
    result = {"status": "pass" if not problems else "fail", "problems": problems,
              "total_rows": total_rows, "per_device_rows": per_device,
              "quality_rows": quality_rows, "bad_quality_rows": bad_quality,
              "latest_points": latest,
              "stream_backlog": backlog, "pending": pending, "dlq": dlq,
              "sampled_values": sampled_values, "monitor": monitor}
    write_json(Path(args.output), result)
    print(json.dumps({k: v for k, v in result.items() if k != "sampled_values"}), flush=True)
    r.close()
    return 0


def run_host(args: argparse.Namespace) -> int:
    repo = ROOT
    evidence = Path(args.evidence).resolve()
    evidence.mkdir(parents=True, exist_ok=True)
    baseline = evidence / "baseline-983ba9a-main.py"
    if not baseline.exists():
        source = command("git", "show", "983ba9a:collector/main.py").stdout
        baseline.write_text(source, encoding="utf-8")
    baseline_hash = hashlib.sha256(baseline.read_bytes()).hexdigest()
    session = uuid4().hex[:6]
    owner = uuid4().hex
    net = f"{PREFIX}-{session}"
    names = {kind: f"{PREFIX}-{session}-{kind}"
             for kind in ("redis", "mysql", "td", "writer", "collector", "monitor")}
    last_run_dir = None

    def cleanup_owned_resources() -> None:
        for kind in ("collector", "monitor", "writer", "td", "mysql", "redis"):
            try:
                if owned_resource("container", names[kind], owner):
                    if last_run_dir is not None and kind in ("collector", "monitor", "writer"):
                        log_path = last_run_dir / f"{kind}.log"
                        if not log_path.exists():
                            logs = docker("logs", names[kind], check=False)
                            log_path.write_text(logs.stdout + logs.stderr, encoding="utf-8")
                    docker("rm", "-f", names[kind], check=False)
            except Exception as exc:
                print(f"cleanup {kind} failed: {exc}", file=sys.stderr)
        try:
            if owned_resource("network", net, owner):
                docker("network", "rm", net, check=False)
        except Exception as exc:
            print(f"cleanup network failed: {exc}", file=sys.stderr)

    atexit.register(cleanup_owned_resources)
    docker("network", "create", "--label", f"{OWNER_LABEL}={owner}", net)
    resources = {
        "redis": ("redis:7.4", ["redis-server", "--appendonly", "yes", "--appendfsync",
                                "everysec", "--maxmemory", "4gb", "--maxmemory-policy",
                                "noeviction", "--save", ""]),
        "mysql": ("mysql:8.4", []),
        "td": ("tdengine/tsdb-amd64:3.4.0.9", []),
    }
    for kind, (image, trailing) in resources.items():
        if owned_resource("container", names[kind], owner):
            continue
        opts = ["run", "-d", "--name", names[kind], "--network", net,
                "--network-alias", f"perf-{kind}", "--label", f"{OWNER_LABEL}={owner}"]
        if kind == "redis":
            opts += ["--cpus", "4", "--memory", "6g"]
        if kind == "mysql":
            opts += ["-e", "MYSQL_ALLOW_EMPTY_PASSWORD=yes", "-e", "MYSQL_ROOT_HOST=%"]
        docker(*(opts + [image] + trailing))
    for _ in range(60):
        if docker("exec", names["mysql"], "mysql", "-h", "127.0.0.1",
                  "--protocol=TCP", "-uroot", "-N", "-e", "select 1",
                  check=False).returncode == 0:
            break
        time.sleep(2)
    else:
        raise RuntimeError("isolated MySQL did not become ready")

    if args.rounds == "smoke":
        rounds = [(f"{session}-smoke-{args.devices}x{args.points}-B", "B", args.smoke_seconds)]
    elif args.rounds == "abba":
        rounds = [(f"{session}-{x}", x.split("-")[-1], args.short_seconds)
                  for x in ("ab1-A", "ab1-B", "ba2-B", "ba2-A")]
    else:
        rounds = [(f"{session}-soak-B", "B", args.soak_seconds)]
    summaries = []
    cohort = hashlib.sha256(str(evidence).encode("utf-8")).hexdigest()[:6]
    for label, variant, seconds in rounds:
        run_dir = evidence / label
        run_dir.mkdir(parents=True, exist_ok=True)
        last_run_dir = run_dir
        token = uuid4().hex[:8]
        devices = [f"ab{cohort}{i:02d}" for i in range(args.devices)]
        plan = {"token": token, "devices": devices, "points": args.points,
                "period_ms": args.period_ms, "seconds": seconds, "shard_count": SHARDS,
                "device_type": f"jnt{cohort}", "group": "gjt",
                "stable": f"sdjnt{cohort}", "mysql_database": f"dwjnt_{token}",
                "baseline_main_sha256": baseline_hash,
                "images": {name: docker("image", "inspect", "--format", "{{.Id}}", image).stdout.strip()
                           for name, image in (("collector", COLLECTOR_IMAGE),
                                               ("runner", RUNNER_IMAGE))},
                "source_sha256": {p: hashlib.sha256((repo / p).read_bytes()).hexdigest()
                                  for p in ("collector/main.py", "collector/collector/executor.py",
                                            "collector/storage/redis_writer.py",
                                            "collector/storage/durable_redis_writer.py",
                                            "writer/main.py", "writer/consumer.py",
                                            "scripts/validate_central_pipeline.py")}}
        write_json(run_dir / "plan.json", plan)
        data_root = run_dir / "data"
        data_root.mkdir(exist_ok=True)
        shutil.copytree(repo / "packages" / "s7", data_root / "drivers" / "s7",
                        ignore=shutil.ignore_patterns("__pycache__", "*.pyc"),
                        dirs_exist_ok=True)
        # Only this run-local activation manifest is adapted for Linux; the
        # repository package is Windows-only and remains untouched. This
        # measures pipeline behavior in Docker, not Windows deployment capacity.
        manifest = data_root / "drivers" / "s7" / "driver.toml"
        manifest.write_text(manifest.read_text(encoding="utf-8").replace(
            'os = ["windows"]', 'os = ["linux"]'), encoding="utf-8")
        sys.path.insert(0, str(repo))
        from data_watch_driver_catalog import tree_digest
        catalog = json.loads((repo / "packages" / "driver-catalog" / "catalog.json").read_text(
            encoding="utf-8"))
        digest = tree_digest(data_root / "drivers" / "s7")
        for entry in catalog["drivers"]:
            if entry["id"] == "s7":
                entry["tree_sha256"] = digest
        catalog_dir = data_root / "driver-catalog"
        catalog_dir.mkdir(exist_ok=True)
        write_json(catalog_dir / "catalog.json", catalog)
        plan["driver_manifest_adaptation"] = "run-local os windows→linux; device I/O simulated"
        plan["adapted_s7_sha256"] = digest
        write_json(run_dir / "plan.json", plan)
        for stale in ("monitor.stop", "monitor.json", "feed.stop", "feed.start", "feed.end",
                      "runtime/writer.shutdown_flag"):
            path = run_dir / stale
            if path.exists():
                path.unlink()
        docker("exec", names["redis"], "redis-cli", "FLUSHDB")
        volume = ["-v", f"{repo}:/workspace:ro", "-v", f"{evidence}:/evidence"]
        base = ["--network", net, *volume, "-e", "PYTHONDONTWRITEBYTECODE=1"]
        rel = f"/evidence/{label}"
        prepare = docker("run", "--rm", *base,
                         "-e", "PYTHONPATH=/workspace:/workspace/collector:/workspace/writer",
                         RUNNER_IMAGE, "python", "/workspace/scripts/diagnose_joint_pipeline.py",
                         "--mode", "prep", "--plan", f"{rel}/plan.json",
                         "--runtime", f"{rel}/runtime", "--consumer-workers", "1")
        print(prepare.stdout.strip(), flush=True)
        for kind in ("writer", "collector", "monitor"):
            if owned_resource("container", names[kind], owner):
                docker("rm", "-f", names[kind])
        docker("run", "-d", "--name", names["writer"],
               "--label", f"{OWNER_LABEL}={owner}", *base,
               "-e", "PYTHONPATH=/workspace:/workspace/writer:/workspace/collector",
               "-w", f"{rel}/runtime", RUNNER_IMAGE, "python", "/workspace/writer/main.py",
               "--runtime-dir", f"{rel}/runtime")
        time.sleep(8)
        if not docker("ps", "-q", "-f", f"name=^{names['writer']}$").stdout.strip():
            raise RuntimeError(f"Writer failed: {docker('logs', names['writer']).stdout[-2000:]}")
        writer_cpu_before = None
        writer_cpu_after = None
        stop_file = run_dir / "monitor.stop"
        docker("run", "-d", "--name", names["monitor"],
               "--label", f"{OWNER_LABEL}={owner}", *base,
               RUNNER_IMAGE, "python", "/workspace/scripts/validate_central_pipeline.py",
               "--mode", "monitor", "--plan", f"{rel}/plan.json",
               "--probes", f"{rel}/probes.jsonl", "--output", f"{rel}/monitor.json",
               "--stop-file", f"{rel}/monitor.stop", "--drain-seconds", str(args.drain_seconds))
        collector_args = ["run", "--name", names["collector"],
                          "--label", f"{OWNER_LABEL}={owner}", *base,
                          "-e", "DATAWATCH_DATA_ROOT=/tmp/ab-data",
                          "-e", "DATAWATCH_HOME=/tmp/ab-data",
                          "-e", "DATAWATCH_DRIVER_CATALOG_ROOT=/tmp/ab-data/driver-catalog",
                          "-e", "DATAWATCH_COLLECTOR_STATUS_PORT=0",
                          "-e", "PYTHONPATH=/workspace:/workspace/collector"]
        if variant == "A":
            collector_args += ["-v", f"{baseline}:/workspace/collector/main.py:ro"]
        collector_args += [COLLECTOR_IMAGE, "python3.14t",
                           "/workspace/scripts/validate_central_pipeline.py", "--mode", "collect",
                           "--variant", variant, "--plan", f"{rel}/plan.json",
                           "--probes", f"{rel}/probes.jsonl", "--output", f"{rel}/collector.json",
                           "--stop-file", f"{rel}/feed.stop",
                           "--drain-seconds", str(args.drain_seconds)]
        print(f"{label}: create {run_dir / 'feed.stop'} to request early stop", flush=True)
        with (run_dir / "collector.log").open("w", encoding="utf-8") as log:
            process = subprocess.Popen(["docker", *collector_args], stdout=log,
                                       stderr=subprocess.STDOUT, text=True,
                                       creationflags=getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0))
            next_monitor_check = time.monotonic()
            stop_requested = False
            while process.poll() is None:
                try:
                    if writer_cpu_before is None and (run_dir / "feed.start").exists():
                        writer_cpu_before = cpu_seconds(names["writer"])
                    if writer_cpu_after is None and (run_dir / "feed.end").exists():
                        writer_cpu_after = cpu_seconds(names["writer"])
                    if time.monotonic() >= next_monitor_check:
                        monitor_running = docker("ps", "-q", "-f",
                                                 f"name=^{names['monitor']}$").stdout.strip()
                        if not monitor_running:
                            raise RuntimeError(f"{label}: TD monitor exited during collector feed")
                        next_monitor_check = time.monotonic() + 2
                    time.sleep(.25)
                except KeyboardInterrupt:
                    if stop_requested:
                        raise
                    (run_dir / "feed.stop").write_text("requested\n", encoding="utf-8")
                    stop_requested = True
                    print(f"{label}: early stop requested; waiting for normal shutdown", flush=True)
        if process.returncode:
            failure = (run_dir / "collector.log").read_text(encoding="utf-8", errors="replace")
            raise RuntimeError(f"{label} collector failed: {failure[-3000:]}")
        stop_file.write_text("stop\n", encoding="utf-8")
        for _ in range(int(args.drain_seconds / 2)):
            if (run_dir / "monitor.json").exists():
                break
            if not docker("ps", "-q", "-f", f"name=^{names['monitor']}$").stdout.strip():
                raise RuntimeError(f"{label}: TD monitor exited without result")
            time.sleep(2)
        if not (run_dir / "monitor.json").exists():
            raise RuntimeError(f"{label}: TD monitor exceeded drain deadline")
        monitor_log = docker("logs", names["monitor"], check=False)
        (run_dir / "monitor.log").write_text(monitor_log.stdout + monitor_log.stderr,
                                             encoding="utf-8")
        for _ in range(int(args.drain_seconds / 2)):
            lengths = [int(docker("exec", names["redis"], "redis-cli", "XLEN",
                                  f"rt:stream:{plan['device_type']}:{plan['group']}:{shard}").stdout.strip())
                       for shard in range(SHARDS)]
            if sum(lengths) == 0:
                break
            time.sleep(2)
        (run_dir / "runtime" / "writer.shutdown_flag").write_text("stop\n", encoding="utf-8")
        for _ in range(30):
            if not docker("ps", "-q", "-f", f"name=^{names['writer']}$").stdout.strip():
                break
            time.sleep(2)
        writer_log = docker("logs", names["writer"], check=False)
        (run_dir / "writer.log").write_text(writer_log.stdout + writer_log.stderr,
                                            encoding="utf-8")
        exits = {}
        for kind in ("collector", "monitor", "writer"):
            state = docker("inspect", "--format", "{{.State.ExitCode}} {{.State.Running}}",
                           names[kind], check=False).stdout.strip().split()
            exits[kind] = {"exit_code": int(state[0]), "running": state[1] == "true"} if len(state) == 2 else None
        for kind in ("collector", "monitor", "writer"):
            docker("rm", "-f", names[kind], check=False)
        reconcile = docker("run", "--rm", *base,
                           "-e", "PYTHONPATH=/workspace:/workspace/collector:/workspace/writer",
                           RUNNER_IMAGE, "python", "/workspace/scripts/validate_central_pipeline.py",
                           "--mode", "reconcile", "--plan", f"{rel}/plan.json",
                           "--collector-output", f"{rel}/collector.json",
                           "--monitor-output", f"{rel}/monitor.json",
                           "--output", f"{rel}/reconcile.json", check=False)
        (run_dir / "reconcile.log").write_text(reconcile.stdout + reconcile.stderr,
                                               encoding="utf-8")
        collector = json.loads((run_dir / "collector.json").read_text(encoding="utf-8"))
        recon = json.loads((run_dir / "reconcile.json").read_text(encoding="utf-8")) if reconcile.returncode == 0 else {"status": "error", "problems": [reconcile.stderr[-1000:]]}
        lifecycle_problems = [f"{kind} did not exit cleanly: {state}"
                              for kind, state in exits.items()
                              if state is None or state["running"] or state["exit_code"] != 0]
        summary = {"label": label, "variant": variant, "status": recon["status"],
                   "duration": collector["duration"], "stop_reason": collector["stop_reason"],
                   "planned_duration_complete": collector["planned_duration_complete"],
                   "shutdown_complete": collector["shutdown_complete"],
                   "actual_fps": collector["actual_fps"], "attainment": collector["attainment"],
                   "redis_cpu_busy": round(collector["redis_cpu_seconds"] / collector["duration"], 3),
                   "redis_main_cpu_busy": (None if collector["redis_main_cpu_seconds"] is None else
                                           round(collector["redis_main_cpu_seconds"] / collector["duration"], 3)),
                   "collector_cpu_busy": round(collector["collector_cpu_seconds"] / collector["duration"], 3),
                   "writer_cpu_busy": (None if writer_cpu_before is None or writer_cpu_after is None
                                       else round((writer_cpu_after - writer_cpu_before) / collector["duration"], 3)),
                   "total_rows": recon.get("total_rows"), "problems": recon["problems"] + lifecycle_problems,
                   "container_exits": exits,
                   "td_latency": recon.get("monitor", {}).get("sampled_td_confirmation_ms")}
        if lifecycle_problems:
            summary["status"] = "fail"
        write_json(run_dir / "summary.json", summary)
        summaries.append(summary)
        write_json(evidence / f"summary-{args.rounds}-{session}.json", {"rounds": summaries})
        print(json.dumps(summary), flush=True)
        # TD vnode count is finite; drop only this round's uniquely generated database.
        docker("run", "--rm", *base, RUNNER_IMAGE, "python", "-c",
               "import taosws; c=taosws.connect(host='perf-td',port=6041,user='root',password='taosdata'); "
               f"c.execute('DROP DATABASE IF EXISTS {plan['mysql_database']}'); c.close()", check=False)
        docker("exec", names["mysql"], "mysql", "-uroot", "-e",
               f"DROP DATABASE IF EXISTS `{plan['mysql_database']}`", check=False)
    return 0 if all(s["status"] == "pass" for s in summaries) else 1


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("host", "collect", "monitor", "reconcile"), default="host")
    parser.add_argument("--evidence", default="outputs/central-ab-20260928")
    parser.add_argument("--rounds", choices=("smoke", "abba", "soak"), default="smoke")
    parser.add_argument("--devices", type=int, default=30)
    parser.add_argument("--points", type=int, default=500)
    parser.add_argument("--period-ms", type=int, default=100)
    parser.add_argument("--smoke-seconds", type=float, default=8)
    parser.add_argument("--short-seconds", type=float, default=120)
    parser.add_argument("--soak-seconds", type=float, default=1800)
    parser.add_argument("--drain-seconds", type=float, default=180)
    parser.add_argument("--variant", choices=("A", "B"))
    parser.add_argument("--plan")
    parser.add_argument("--probes")
    parser.add_argument("--stop-file")
    parser.add_argument("--collector-output")
    parser.add_argument("--monitor-output")
    parser.add_argument("--output")
    args = parser.parse_args()
    if args.mode == "collect":
        return container_mode_collect(args)
    if args.mode == "monitor":
        return container_mode_monitor(args)
    if args.mode == "reconcile":
        return container_mode_reconcile(args)
    return run_host(args)


if __name__ == "__main__":
    raise SystemExit(main())
