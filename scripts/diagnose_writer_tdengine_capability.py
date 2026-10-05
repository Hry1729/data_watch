"""独立测量 Writer→TDengine 能力（生产入口形态，隔离环境）。

为什么要独立测：Collector 的回放速率（受 WAITAOF/AOF 节奏与 Redis CPU 限制）会掩盖
下游 Writer 的真实能力。本工具直接构造**与生产同形**的 Redis Stream 积压，再用
`writer/main.py` 生产启动形态消费，测出 Writer→TDengine 这一侧的帧率/行率上限。

与生产形态的覆盖差异（必须如实声明）
------------------------------------
* 使用生产入口 `writer/main.py`（配置发现、多 consumer 分区、性能日志、TDengine 连接、
  shutdown flag 都是生产代码路径），不是只构造组件。
* 但 MySQL 里的范围表/映射表是本工具自建的**最小同构 schema**（只含 SQL 查询用到的列），
  TDengine 也是空库自建；不覆盖真实业务库的历史数据、权限与真实点位规模。
* 输入由本工具按 `build_stream_fields` 写入，**不经 collector.executor 的分组/过滤**。
* 单档只跑一次；不做故障注入与多 Worker 容量结论。

护栏
----
* 全字段对账规模预检：devices × ticks × (points+1) 超过 2,000,000 直接拒绝。
* 连接配置来自**仓库外**的测试配置文件（`--test-config`，样例见
  `scripts/diagnose-test-config.example.json`）；准备数据、Writer 配置、采集与查询**共用同一份**。
* 隔离闸门：主机必须在配置的 `allowed_hosts` 白名单内（默认仅回环）；库名必须是本轮临时库前缀
  （本脚本 `dwcap_`、联合脚本 `dwjnt_`）；同名库已存在**直接拒绝**（只新建临时库，不接管既有库再删）。
  它不是"连生产环境"的通行证。
* Writer 子进程环境**剥离继承的 `DATAWATCH_*`** 后再显式注入本次配置，外部环境无法改变测试目标。
* 验收结论非 `pass` 时进程返回**非零退出码**（失败详情仍完整写入报告文件）。
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import statistics
import subprocess
import sys
import time
from uuid import uuid4

ROOT = Path(__file__).resolve().parents[1]
sys.path[:0] = [str(ROOT), str(ROOT / "collector"), str(ROOT / "writer")]


MAX_FULL_FIELD_CELLS = 2_000_000

# 仓库外的开发机测试连接配置（普通文件权限，不做 ACL 加固）；样例见
# scripts/diagnose-test-config.example.json（仓库内只放占位值，真口令不入 Git）。
DEFAULT_TEST_CONFIG = Path(os.environ.get("DW_DIAG_TEST_CONFIG")
                           or r"D:\python\device_alarm\dw-test-env\diagnose-test-config.json")


def exit_code_for(overall: str) -> int:
    """验收结论 → 进程退出码：只有 pass 才是 0（此前 main() 恒 return 0，把 fail 报成成功）。"""
    return 0 if overall == "pass" else 1


def round_resource_keys(*, stream_keys, devices, group: str, period_ms: int,
                        device_type: str | None = None, receipt_keys=()) -> list[str]:
    """本轮测试对象在 Redis 里的键（stream + latest + window）。只用于**按名**清理。"""
    keys = set(stream_keys)
    keys.update(receipt_keys)
    if device_type is not None:
        keys.add(f"rt:dlq:{device_type}:{group}")
    for device in devices:
        keys.add(f"rt:latest:{device}:{group}")
        keys.add(f"rt:window:{device}:{group}:{period_ms}")
    return sorted(keys)


def cleanup_round_resources(cfg: dict, *, database: str, client, keys, owned_databases) -> dict:
    """只清理本轮创建的临时资源：TDengine 库、MySQL 库、按名删除的 Redis 键。

    绝不做 FLUSHDB/FLUSHALL，也不触碰本轮之外的任何键或库。
    """
    dropped = {"tdengine_database": None, "mysql_database": None, "redis_keys": 0}
    errors = []
    for section in ("tdengine", "mysql"):
        if section not in owned_databases:
            continue
        conn = None
        try:
            spec = cfg[section]
            kwargs = dict(host=spec["host"], port=int(spec["port"]),
                          user=spec.get("user", "root"), password=spec.get("password", ""))
            if section == "tdengine":
                import taosws
                conn = taosws.connect(**kwargs)
                conn.execute(f"DROP DATABASE IF EXISTS {database}")
            else:
                import pymysql
                conn = pymysql.connect(**kwargs, autocommit=True)
                with conn.cursor() as cur:
                    cur.execute(f"DROP DATABASE IF EXISTS `{database}`")
            dropped[f"{section}_database"] = database
        except Exception as exc:
            errors.append(f"{section}: {type(exc).__name__}: {exc}")
        finally:
            if conn is not None:
                try:
                    conn.close()
                except Exception as exc:
                    errors.append(f"{section} close: {type(exc).__name__}: {exc}")
    for key in keys:
        try:
            dropped["redis_keys"] += int(client.delete(key))
        except Exception as exc:
            errors.append(f"redis {key}: {type(exc).__name__}: {exc}")
    if errors:
        dropped["errors"] = errors
    return dropped


def stop_writer_process(process, runtime: Path) -> None:
    """Always reap this run's child, including exception and forced-stop paths."""
    if process is None or process.poll() is not None:
        return
    flag_error = None
    try:
        (runtime / "writer.shutdown_flag").write_text("stop\n", encoding="utf-8")
    except OSError as exc:
        flag_error = exc
    try:
        process.wait(timeout=0 if flag_error else 5)
    except subprocess.TimeoutExpired:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
    if flag_error is not None:
        raise flag_error


def load_test_config(path: str | os.PathLike[str] | None) -> dict:
    """读取测试连接配置：准备数据、Writer 配置、采集与查询共用这一份。"""
    target = Path(path) if path else DEFAULT_TEST_CONFIG
    if not target.is_file():
        raise SystemExit(f"测试连接配置不存在：{target}"
                         "（样例见 scripts/diagnose-test-config.example.json）")
    cfg = json.loads(target.read_text(encoding="utf-8"))
    for section in ("mysql", "redis", "tdengine"):
        sect = cfg.get(section)
        if not isinstance(sect, dict):
            raise SystemExit(f"测试连接配置缺少 [{section}] 段：{target}")
        for field in ("host", "port"):
            if sect.get(field) in (None, ""):
                raise SystemExit(f"测试连接配置 [{section}].{field} 未设置：{target}")
    cfg["_source"] = str(target)
    return cfg


def assert_test_targets(cfg: dict, *, prefix: str, database: str) -> None:
    """隔离闸门：主机须在显式白名单内（默认仅回环），库名须是本轮临时库前缀。

    这不是"连生产环境"的通行证：非回环主机必须在配置的 allowed_hosts 中显式列出。
    """
    allowed = {str(host).strip().lower() for host in (cfg.get("allowed_hosts") or [])}
    allowed |= {"127.0.0.1", "localhost"}
    for section in ("mysql", "redis", "tdengine"):
        host = str(cfg[section].get("host", "")).strip().lower()
        if host not in allowed:
            raise SystemExit(f"拒绝：{section} 主机 {host!r} 不在显式白名单内"
                             "（配置 allowed_hosts）；本工具不得直连生产环境")
    if not database.startswith(prefix):
        raise SystemExit(f"拒绝：库名 {database!r} 必须以本轮前缀 {prefix!r} 开头"
                         "（能力脚本 dwcap_、联合脚本 dwjnt_）")


def assert_tdengine_database_absent(cfg: dict, database: str) -> None:
    """TDengine 同名库已存在则拒绝（只新建临时库，不接管既有库再删）。"""
    import taosws
    td = cfg["tdengine"]
    conn = taosws.connect(host=td["host"], port=int(td["port"]),
                          user=td.get("user", "root"), password=td.get("password", ""))
    try:
        rows = list(conn.query("SELECT COUNT(*) FROM information_schema.ins_databases "
                               f"WHERE name = '{database}'"))
    finally:
        conn.close()
    if rows and rows[0][0]:
        raise SystemExit(f"拒绝：TDengine 库 {database!r} 已存在"
                         "（本轮只允许新建临时库，不接管既有库）")


def writer_child_env(cfg: dict, *, database: str) -> dict:
    """Writer 子进程环境：剥离继承的 DATAWATCH_*，再显式注入本次测试配置。

    注意 redis 段没有 database 字段（writer 只支持 host/port/password 覆盖），
    Redis 库号由 write_runtime 生成的 settings.toml 的 db 决定。
    """
    env = {key: value for key, value in os.environ.items()
           if not key.startswith("DATAWATCH_")}
    env.update({
        "PYTHONPATH": f"{ROOT}{os.pathsep}{ROOT / 'writer'}{os.pathsep}{ROOT / 'collector'}",
        "PYTHONUNBUFFERED": "1",
        "DATAWATCH_REDIS_HOST": str(cfg["redis"]["host"]),
        "DATAWATCH_REDIS_PORT": str(int(cfg["redis"]["port"])),
        "DATAWATCH_REDIS_PASSWORD": str(cfg["redis"].get("password", "")),
        "DATAWATCH_MYSQL_HOST": str(cfg["mysql"]["host"]),
        "DATAWATCH_MYSQL_PORT": str(int(cfg["mysql"]["port"])),
        "DATAWATCH_MYSQL_USER": str(cfg["mysql"].get("user", "root")),
        "DATAWATCH_MYSQL_DATABASE": database,
        "DATAWATCH_MYSQL_PASSWORD": str(cfg["mysql"].get("password", "")),
        "DATAWATCH_TDENGINE_HOST": str(cfg["tdengine"]["host"]),
        "DATAWATCH_TDENGINE_PORT": str(int(cfg["tdengine"]["port"])),
        "DATAWATCH_TDENGINE_USER": str(cfg["tdengine"].get("user", "root")),
        "DATAWATCH_TDENGINE_DATABASE": database,
        "DATAWATCH_TDENGINE_PASSWORD": str(cfg["tdengine"].get("password", "")),
    })
    return env


def file_sha(path: Path) -> str | None:
    return hashlib.sha256(path.read_bytes()).hexdigest() if path.exists() else None


def provenance() -> dict:
    files = ["writer/main.py", "writer/consumer.py", "writer/tdengine_writer.py",
             "writer/mysql_mapper.py", "collector/storage/durable_redis_writer.py",
             "scripts/diagnose_writer_tdengine_capability.py"]
    return {"file_sha256": {name: file_sha(ROOT / name) for name in files},
            "python": sys.version.split()[0],
            "gil_enabled": getattr(sys, "_is_gil_enabled", lambda: None)()}


def seed_stream(client, *, devices, device_type, group, points, ticks, period_ms,
                base_ts, shard_count, receipt_key: str | None = None) -> dict:
    """按生产同形把 bounded 积压写进 Redis Stream（真实 FRAME_LUA_V2 + receipt/seq）。"""
    from data_watch_shared.stream_envelope import build_stream_fields, stream_key
    from data_watch_shared.acceptance import build_frame, tdengine_value_column
    from storage.durable_redis_writer import FRAME_LUA_V2
    digest = client.script_load(FRAME_LUA_V2)
    session = uuid4().hex
    receipt_key = receipt_key or f"collector:receipt:v2:{session}"
    seq = 0
    frames = 0
    stream_columns: list[str] = []
    points_in_stream = 0
    quality_rows_expected = 0
    started = time.perf_counter()
    for tick in range(ticks):
        for device in devices:
            seq += 1
            frame = build_frame(device, points, tick, period_ms, base_ts)
            if not stream_columns:
                # stream_values 的键是点位 id；TDengine 列名由 MySQL 映射给出
                # （本工具用同一规则 tdengine_value_column）。只有真正进入历史的点才有列：
                # NULL/非存储/STRING 点没有列，因此对账列集必须取自实际写入 stream 的点，
                # 不能假设 p1..pN 全在。
                stream_columns = sorted(tdengine_value_column(int(point_id))
                                        for point_id in frame["stream_values"])
                points_in_stream = len(stream_columns)
            fields = build_stream_fields(
                device_code=device, device_type=device_type, group_code=group,
                scan_rate_ms=period_ms, ts=frame["ts"], config_version=1,
                values=frame["stream_values"], qualities=frame["stream_qualities"])
            flat = []
            for key, value in fields.items():
                flat.extend((key, value.decode() if isinstance(value, bytes) else value))
            client.evalsha(digest, 4, receipt_key,
                           stream_key(device, device_type, group, shard_count),
                           f"rt:latest:{device}:{group}",
                           f"rt:window:{device}:{group}:{period_ms}",
                           json.dumps(flat), json.dumps(frame["latest"]),
                           json.dumps({"ts": frame["ts"],
                                       "values": frame["window_values"]}),
                           device, group, seq)
            frames += 1
            # 质量窄表行数随"该 tick 实际进入历史的点数"变化（时有时无的 NULL 点在某些
            # tick 不进 stream），因此逐帧累加，不能用 ticks × 常量点数。
            quality_rows_expected += len(frame["stream_values"])
    elapsed = time.perf_counter() - started
    keys = sorted({stream_key(device, device_type, group, shard_count)
                   for device in devices})
    return {"frames": frames, "points_per_frame": points,
            "points_in_stream_per_frame": points_in_stream,
            "stream_columns_at_sample": len(stream_columns),
            "stream_columns": stream_columns,
            "quality_rows_expected": quality_rows_expected,
            "seed_seconds": round(elapsed, 3),
            "seed_frames_per_second": round(frames / elapsed, 2),
            "stream_keys": keys,
            "stream_len": {key: client.xlen(key) for key in keys},
            "receipt_key": receipt_key}


def prepare_mysql(mysql: dict, database: str, *, devices, device_type, group, points,
                  stable: str, owned_resources=None) -> int:
    """最小同构 schema：映射表（MysqlMapper 用）+ 范围表（main.py 发现 scope 用）。"""
    import pymysql
    from data_watch_shared.acceptance import tdengine_value_column
    conn = pymysql.connect(host=mysql["host"], port=int(mysql["port"]),
                           user=mysql.get("user", "root"),
                           password=mysql.get("password", ""), autocommit=True)
    try:
        with conn.cursor() as cur:
            cur.execute("SELECT SCHEMA_NAME FROM information_schema.SCHEMATA "
                        "WHERE SCHEMA_NAME = %s", (database,))
            if cur.fetchone():
                raise SystemExit(f"拒绝：MySQL 库 {database!r} 已存在"
                                 "（本轮只允许新建临时库，不接管既有库）")
            cur.execute(f"CREATE DATABASE `{database}`")
            if owned_resources is not None:
                owned_resources.add("mysql")
            cur.execute(f"USE `{database}`")
            cur.execute("CREATE TABLE point_storage_mapping (point_id BIGINT, "
                        "td_column_name VARCHAR(32), var_type VARCHAR(32), group_code VARCHAR(32), "
                        "device_code VARCHAR(32), device_type_code VARCHAR(32), "
                        "stable_name VARCHAR(64), child_table VARCHAR(32))")
            rows = []
            for device in devices:
                for point in range(1, points + 1):
                    rows.append((point, tdengine_value_column(point), "float", group,
                                 device, device_type, stable, device))
            cur.executemany("INSERT INTO point_storage_mapping VALUES "
                            "(%s,%s,%s,%s,%s,%s,%s,%s)", rows)
            # writer/main.py 的 _STREAM_SCOPE_SQL 依赖这四张表
            cur.execute("CREATE TABLE device_type (id INT PRIMARY KEY, type_code VARCHAR(32))")
            cur.execute("CREATE TABLE device (id INT PRIMARY KEY, type_id INT)")
            cur.execute("CREATE TABLE device_point_group (id INT PRIMARY KEY, device_id INT, "
                        "group_code VARCHAR(32))")
            cur.execute("CREATE TABLE device_variable (id INT PRIMARY KEY, point_group_id INT, "
                        "deleted TINYINT, enable TINYINT, td_column_name VARCHAR(32))")
            cur.execute("INSERT INTO device_type VALUES (1, %s)", (device_type,))
            for index, device in enumerate(devices, 1):
                cur.execute("INSERT INTO device VALUES (%s, 1)", (index,))
                cur.execute("INSERT INTO device_point_group VALUES (%s, %s, %s)",
                            (index, index, group))
                cur.execute("INSERT INTO device_variable VALUES (%s, %s, 0, 1, %s)",
                            (index, index, tdengine_value_column(1)))
    finally:
        conn.close()
    return len(rows) * 0 + len(devices)


def write_runtime(runtime: Path, cfg: dict, *, database: str, stable: str, device_type: str,
                  shard_count: int, group: str, consumer_workers: int) -> Path:
    runtime.mkdir(parents=True, exist_ok=True)
    (runtime / "config").mkdir(exist_ok=True)
    # 与 prepare_mysql / 采集 / 查询共用同一份配置（此前这里硬编码 perf-*，与命令行主机不一致）
    redis_cfg, mysql_cfg, td_cfg = cfg["redis"], cfg["mysql"], cfg["tdengine"]
    redis_host = redis_cfg["host"]
    redis_port = int(redis_cfg["port"])
    redis_db = int(redis_cfg.get("db", 0))
    redis_password = redis_cfg.get("password", "")
    mysql_host = mysql_cfg["host"]
    mysql_port = int(mysql_cfg["port"])
    mysql_user = mysql_cfg.get("user", "root")
    mysql_password = mysql_cfg.get("password", "")
    td_host = td_cfg["host"]
    td_port = int(td_cfg["port"])
    td_user = td_cfg.get("user", "root")
    td_password = td_cfg.get("password", "")
    # JSON string escapes are also valid TOML basic-string escapes. Keep Unicode
    # literal so supplementary characters are not encoded as surrogate pairs.
    def string(value):
        return json.dumps(str(value), ensure_ascii=False)

    settings = f"""[redis]
host = {string(redis_host)}
port = {redis_port}
db = {redis_db}
password = {string(redis_password)}
stream_shard_count = {shard_count}
group_name = "writer"
consumer_name = "cap-{uuid4().hex[:8]}"

[mysql]
host = {string(mysql_host)}
port = {mysql_port}
user = {string(mysql_user)}
password = {string(mysql_password)}
database = {string(database)}

[tdengine]
host = {string(td_host)}
port = {td_port}
user = {string(td_user)}
password = {string(td_password)}
database = {string(database)}

[writer]
flush_interval_ms = 100
batch_count = 200
max_buffer_rows = 1000
max_buffer_bytes = 33554432
pending_min_idle_ms = 1000
pending_drain_interval_s = 1
consumer_workers = {consumer_workers}
device_types = [{string(device_type)}]
stable_names = [{string(stable)}]
group_code = {string(group)}
"""
    path = runtime / "config" / "settings.toml"
    path.write_text(settings, encoding="utf-8")
    return path


def run_continuous(client, *, devices, device_type, group, points, ticks_unused,
                   period_ms, base_ts, shard_count, duration_s, process, keys) -> dict:
    """持续速率喂入：测"Writer 能否在稳定输入下跟上"，而不是"多久排空一小堆积压"。

    为什么用 XADD-only 喂入：历史 Lua 的每帧成本（500 点约 2.4ms Redis 主线程）会让
    **喂入端**自己打满 Redis，从而掩盖 Writer 的真实能力——这正是要避免的"上游限速"。
    这里只写 stream 字段（与生产信封逐字段一致），不产生 receipt/latest/window 副作用，
    因此本档测的是 **Writer 侧上限**，不代表端到端上限。
    """
    from data_watch_shared.stream_envelope import build_stream_fields, stream_key
    from data_watch_shared.acceptance import build_frame, tdengine_value_column
    started = time.monotonic()
    key_for = {}
    for device in devices:
        key_for[device] = stream_key(device, device_type, group, shard_count)
    fed = 0
    samples = []
    stream_columns: list[str] = []
    quality_rows_expected = 0
    next_sample = started
    tick = 0
    while time.monotonic() - started < duration_s:
        for device in devices:
            frame = build_frame(device, points, tick, period_ms, base_ts)
            if not stream_columns:
                stream_columns = sorted(tdengine_value_column(int(point_id))
                                        for point_id in frame["stream_values"])
            quality_rows_expected += len(frame["stream_values"])
            fields = build_stream_fields(
                device_code=device, device_type=device_type, group_code=group,
                scan_rate_ms=period_ms, ts=frame["ts"], config_version=1,
                values=frame["stream_values"], qualities=frame["stream_qualities"])
            flat = []
            for key, value in fields.items():
                flat.extend((key, value.decode() if isinstance(value, bytes) else value))
            client.xadd(key_for[device], {k: v for k, v in zip(flat[::2], flat[1::2])})
            fed += 1
        tick += 1
        now = time.monotonic()
        if now >= next_sample:
            backlog = sum(client.xlen(key) for key in keys)
            try:
                with process.oneshot():
                    rss = process.memory_info().rss
            except Exception:
                rss = None
            samples.append({"t": round(now - started, 3), "fed": fed, "backlog": backlog,
                            "rss_bytes": rss})
            next_sample = now + 0.5
        target = started + tick * period_ms / 1000.0
        sleep_for = target - time.monotonic()
        if sleep_for > 0:
            time.sleep(sleep_for)
    feed_seconds = time.monotonic() - started
    # 停止喂入后排空
    drain_started = time.monotonic()
    drained_at = None
    while time.monotonic() - drain_started < 120:
        backlog = sum(client.xlen(key) for key in keys)
        if backlog == 0:
            drained_at = time.monotonic() - drain_started
            break
        time.sleep(0.5)
    offered_fps = fed / feed_seconds if feed_seconds else None
    backlog_series = [s["backlog"] for s in samples] or [0]
    first_half = backlog_series[:max(1, len(backlog_series) // 2)]
    second_half = backlog_series[max(1, len(backlog_series) // 2):]
    trend = ("growing" if statistics.mean(second_half) > statistics.mean(first_half) * 1.2
             else "flat_or_shrinking")
    return {"mode": "continuous_xadd",
            "feed_seconds": round(feed_seconds, 3), "fed_frames": fed,
            "stream_columns": stream_columns,
            "stream_columns_at_sample": len(stream_columns),
            "quality_rows_expected": quality_rows_expected,
            "points_in_stream_per_frame": (len(stream_columns) or None),
            "offered_fps": round(offered_fps, 2) if offered_fps else None,
            "offered_points_per_second": (round(offered_fps * points, 2)
                                          if offered_fps else None),
            "backlog_samples": samples[:120], "samples_total": len(samples),
            "backlog_max": max(backlog_series), "backlog_end_of_feed": backlog_series[-1],
            "backlog_trend": trend,
            "kept_up": trend == "flat_or_shrinking" and drained_at is not None,
            "post_feed_drain_seconds": round(drained_at, 3) if drained_at else None}


def monitor(client, keys, process, *, timeout_s: float) -> dict:
    """采样积压/PEL 与子进程资源，直到流清空且 PEL 为 0（或超时）。"""
    import psutil
    started = time.monotonic()
    samples = []
    drained_at = None
    while time.monotonic() - started < timeout_s:
        pending = 0
        backlog = 0
        for key in keys:
            backlog += client.xlen(key)
            try:
                groups = client.xinfo_groups(key)
            except Exception:
                groups = []
            for grp in groups:
                pending += int(grp.get("pending", 0))
        try:
            with process.oneshot():
                rss = process.memory_info().rss
                cpu = sum(process.cpu_times()[:2])
                alive = process.is_running() and process.status() != psutil.STATUS_ZOMBIE
        except Exception:
            rss = cpu = None
            alive = False
        samples.append({"t": round(time.monotonic() - started, 3), "backlog": backlog,
                        "pending": pending, "rss_bytes": rss, "cpu_seconds": cpu})
        if backlog == 0 and pending == 0:
            drained_at = time.monotonic() - started
            break
        if not alive:
            break
        time.sleep(0.5)
    return {"samples": samples, "drained_at_s": drained_at,
            "final_backlog": samples[-1]["backlog"] if samples else None,
            "final_pending": samples[-1]["pending"] if samples else None,
            "max_rss_bytes": max((s["rss_bytes"] or 0) for s in samples) if samples else None,
            "process_alive": process.is_running() if process else None}


def read_counts(td_config: dict, devices, points, ticks, *, expected_quality_rows) -> dict:
    """行数（不拉全字段，避免内存随规模膨胀）。"""
    import taosws
    conn = taosws.connect(**td_config)
    try:
        value_rows = 0
        quality_rows = 0
        per_device = {}
        for device in devices:
            count = int(list(conn.query(f"SELECT COUNT(*) FROM `{device}`"))[0][0])
            per_device[device] = count
            value_rows += count
        for device in devices:
            try:
                quality_rows += int(list(conn.query(
                    f"SELECT COUNT(*) FROM `{device}_q`"))[0][0])
            except Exception:
                pass  # 该设备没有质量行（不应发生）
    finally:
        conn.close()
    return {"value_rows": value_rows, "quality_rows": quality_rows,
            "per_device_value_rows": per_device,
            "expected_value_rows": len(devices) * ticks,
            "expected_quality_rows": expected_quality_rows}


def read_sample_fields(td_config: dict, device: str, columns: list[str],
                       ticks: int, base_ts: int, period_ms: int, *, sample_ticks: int) -> dict:
    """抽样的全字段核对：只取前 sample_ticks 帧与最后 1 帧，逐列比较。"""
    import taosws
    from data_watch_shared.acceptance import expected_value_cell
    conn = taosws.connect(**td_config)
    try:
        select = ", ".join(columns)
        selected = list(range(sample_ticks)) + [ticks - 1]
        timestamps = [base_ts + tick * period_ms for tick in selected]
        in_list = ", ".join(str(ts) for ts in timestamps)
        rows = list(conn.query(
            f"SELECT cast(ts as bigint) AS ts, {select} FROM `{device}` "
            f"WHERE ts IN ({in_list}) ORDER BY ts"))
        problems = []
        checked_cells = 0
        for row in rows:
            ts = int(row[0])
            tick = (ts - base_ts) // period_ms
            # 必须按 SELECT 的列顺序取单元格：列名是字典序（p1,p10,p11,…,p2,p20,…），
            # 按下标对齐"点位顺序"会整体错位（第一版就把 p13 的值读成了 p19 的）。
            for index, column in enumerate(columns, start=1):
                point = int(column[1:])
                expected = expected_value_cell(device, point, tick)
                actual = row[index]
                checked_cells += 1
                if expected is None:
                    if actual is not None:
                        problems.append(f"{device} t{tick} p{point}: 期望 NULL 实得 {actual!r}")
                    continue
                if actual is None or abs(float(actual) - expected) > 1e-6:
                    problems.append(f"{device} t{tick} p{point}: 期望 {expected} 实得 {actual!r}")
        sampled_frames = len(rows)
        return {"device": device, "sampled_ticks": len(selected),
                "sampled_frames_found": sampled_frames,
                "checked_cells": checked_cells, "problems": problems[:20],
                "problem_count": len(problems)}
    finally:
        conn.close()


def main() -> int:
    parser = argparse.ArgumentParser(description="Writer→TDengine 能力独立测量")
    parser.add_argument("--devices", type=int, required=True)
    parser.add_argument("--points", type=int, required=True)
    parser.add_argument("--ticks", type=int, required=True)
    parser.add_argument("--period-ms", type=int, default=100)
    parser.add_argument("--shard-count", type=int, default=4)
    parser.add_argument("--consumer-workers", type=int, default=1)
    parser.add_argument("--timeout-seconds", type=float, default=300)
    parser.add_argument("--duration-seconds", type=float, default=0,
                        help=">0 时改为持续速率喂入（XADD-only）并测 Writer 是否能跟上")
    parser.add_argument("--sample-ticks", type=int, default=2)
    parser.add_argument("--output", required=True)
    parser.add_argument("--test-config", default=None,
                        help="仓库外测试连接配置（默认取 DW_DIAG_TEST_CONFIG 或开发机固定路径）")
    parser.add_argument("--keep-resources", action="store_true",
                        help="保留本轮临时库与 Redis 键以便人工检查"
                             "（默认测完即删，避免 vnode 耗尽与残留键）")
    args = parser.parse_args()

    cfg = load_test_config(args.test_config)
    import psutil
    import redis
    import taosws
    from data_watch_shared.stream_envelope import stream_key
    from data_watch_shared.acceptance import PASS, correctness_verdict, overall_status, verdict
    cells = args.devices * args.ticks * (args.points + 1)
    if cells > MAX_FULL_FIELD_CELLS:
        raise ValueError(f"全字段对账预计 {cells} 单元超过 {MAX_FULL_FIELD_CELLS} 上限")

    token = uuid4().hex[:10]
    device_type = "cap" + token
    group = "gcap"
    stable = f"sd{device_type}"
    database = f"dwcap_{token}"
    assert_test_targets(cfg, prefix="dwcap_", database=database)
    assert_tdengine_database_absent(cfg, database)
    devices = [f"cw{token}{index:02d}" for index in range(args.devices)]
    base_ts = int(time.time() * 1000)

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    report = {"provenance": provenance(),
              "tier": {"devices": args.devices, "points": args.points,
                       "ticks": args.ticks, "period_ms": args.period_ms,
                       "frames": args.devices * args.ticks,
                       "points_total": args.devices * args.ticks * args.points},
              "coverage_note": ("生产入口 writer/main.py + 自建最小同构 MySQL schema + "
                                "空 TDengine 库；输入不经 collector.executor；单档单次，"
                                "不含故障注入与多 worker 容量结论"),
              "mysql_database": database, "stable": stable, "device_type": device_type}
    owned = set()
    client = process = None
    runtime = output.parent / f"runtime-{token}"
    receipt_key = f"collector:receipt:v2:cap-{token}"
    keys = round_resource_keys(
        stream_keys={stream_key(device, device_type, group, args.shard_count) for device in devices},
        devices=devices, group=group, period_ms=args.period_ms, device_type=device_type,
        receipt_keys=[] if args.duration_seconds > 0 else [receipt_key])
    report["redis_resources"] = keys
    try:
        report["mysql_devices"] = prepare_mysql(
            cfg["mysql"], database, devices=devices, device_type=device_type,
            group=group, points=args.points, stable=stable, owned_resources=owned)
        td_config = {"host": cfg["tdengine"]["host"], "port": int(cfg["tdengine"]["port"]),
                     "user": cfg["tdengine"].get("user", "root"),
                     "password": cfg["tdengine"].get("password", ""), "database": database}
        # 先不带 database 连接（否则连不上一个还不存在的库），建库后再按库连接
        admin_config = {key: value for key, value in td_config.items() if key != "database"}
        conn = taosws.connect(**admin_config)
        try:
            conn.execute(f"CREATE DATABASE {database}")
            owned.add("tdengine")
        finally:
            conn.close()

        client = redis.Redis(host=cfg["redis"]["host"], port=int(cfg["redis"]["port"]),
                             db=int(cfg["redis"].get("db", 0)),
                             password=cfg["redis"].get("password") or None,
                             decode_responses=True, socket_timeout=30)
        assert client.ping()
        continuous = args.duration_seconds > 0
        if continuous:
            # 持续模式：先把 writer 启动起来，再按目标速率喂（测"能否跟上"）。
            # 注意：同一 (device_type, group) 的多个设备共享同一批 shard 键，
            # 必须去重后再统计 XLEN，否则会把积压按设备数重复累加（第一版就错了这里）。
            report["seed"] = {"mode": "continuous", "stream_keys": sorted({
                stream_key(device, device_type, group, args.shard_count)
                for device in devices})}
        else:
            report["seed"] = seed_stream(
                client, devices=devices, device_type=device_type, group=group,
                points=args.points, ticks=args.ticks, period_ms=args.period_ms,
                base_ts=base_ts, shard_count=args.shard_count, receipt_key=receipt_key)

        runtime = output.parent / f"runtime-{token}"
        settings = write_runtime(runtime, cfg, database=database, stable=stable,
                                 device_type=device_type, shard_count=args.shard_count,
                                 group=group, consumer_workers=args.consumer_workers)
        report["runtime_settings"] = str(settings)
        log_path = output.parent / f"writer-{token}.log"
        # 剥离继承的 DATAWATCH_*，再显式注入本次配置（防止外部环境把测试目标改到别处）
        env = writer_child_env(cfg, database=database)
        with open(log_path, "w", encoding="utf-8") as log:
            process = subprocess.Popen(
                [sys.executable, str(ROOT / "writer" / "main.py"),
                 "--runtime-dir", str(runtime)],
                cwd=str(runtime), env=env, stdout=log, stderr=subprocess.STDOUT)
            report["writer_pid"] = process.pid
            if continuous:
                time.sleep(2.0)  # 让 writer 完成连接/建表/发现 scope
                monitored = run_continuous(
                    client, devices=devices, device_type=device_type, group=group,
                    points=args.points, ticks_unused=args.ticks, period_ms=args.period_ms,
                    base_ts=base_ts, shard_count=args.shard_count,
                    duration_s=args.duration_seconds, process=psutil.Process(process.pid),
                    keys=report["seed"]["stream_keys"])
                monitored["drained_at_s"] = monitored["post_feed_drain_seconds"]
                report["seed"].update({
                    "stream_columns": monitored["stream_columns"],
                    "stream_columns_at_sample": monitored["stream_columns_at_sample"],
                    "quality_rows_expected": monitored["quality_rows_expected"],
                    "points_in_stream_per_frame": monitored["points_in_stream_per_frame"]})
            else:
                monitored = monitor(client, report["seed"]["stream_keys"],
                                    psutil.Process(process.pid),
                                    timeout_s=args.timeout_seconds)
            # 排空后用生产停机路径退出（写 shutdown_flag），而不是直接 SIGTERM：
            # 这样"退出码"才是生产语义下的结果，0 表示按停机标记正常退出。
            if monitored["drained_at_s"]:
                (runtime / "writer.shutdown_flag").write_text("stop\n", encoding="utf-8")
            deadline = time.monotonic() + 30
            while process.poll() is None and time.monotonic() < deadline:
                time.sleep(0.5)
            graceful = process.poll() is not None
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=15)
        report["drain"] = {"drained_at_s": monitored.get("drained_at_s"),
                           "final_backlog": monitored.get("final_backlog",
                                                          monitored.get("backlog_end_of_feed")),
                           "final_pending": monitored.get("final_pending"),
                           "max_rss_bytes": monitored.get("max_rss_bytes")
                           or max((s.get("rss_bytes") or 0)
                                  for s in monitored.get("backlog_samples", [{"rss_bytes": 0}])),
                           "samples": (monitored.get("samples")
                                       or monitored.get("backlog_samples"))[:80],
                           "samples_total": (monitored.get("samples_total")
                                             or len(monitored.get("backlog_samples") or [])),
                           "graceful_shutdown": graceful,
                           "writer_exit_code": process.returncode,
                           "writer_log": str(log_path)}
        if monitored["drained_at_s"]:
            frames = report["tier"]["frames"]
            points = report["tier"]["points_total"]
            report["capability"] = {
                "frames_per_second": round(frames / monitored["drained_at_s"], 2),
                "points_per_second": round(points / monitored["drained_at_s"], 2),
                "drain_seconds": round(monitored["drained_at_s"], 3)}
        else:
            report["capability"] = {"status": "not_drained",
                                    "note": "在超时内未排空：只能说明该档位不足，不能外推"}

        if monitored["drained_at_s"]:
            frames = report["tier"]["frames"]
            points = report["tier"]["points_total"]
            report["capability"] = {
                "frames_per_second": round(frames / monitored["drained_at_s"], 2),
                "points_per_second": round(points / monitored["drained_at_s"], 2),
                "drain_seconds": round(monitored["drained_at_s"], 3)}
        else:
            report["capability"] = {"status": "not_drained",
                                    "note": "在超时内未排空：只能说明该档位不足，不能外推"}

        if continuous:
            # 持续模式的期望值必须来自"实际喂进去的量"，不能沿用固定积压模式的 ticks
            fed = monitored["fed_frames"]
            feed_s = monitored["feed_seconds"]
            ticks_fed = fed // max(1, len(devices))
            report["tier"]["ticks_fed"] = ticks_fed
            report["seed"]["quality_rows_expected"] = monitored["quality_rows_expected"]
            expected_quality = monitored["quality_rows_expected"]
            counts = read_counts(td_config, devices, args.points, ticks_fed,
                                 expected_quality_rows=expected_quality)
            counts["expected_value_rows"] = fed
            report["rows"] = counts
            report["continuous"] = monitored
            report["capability"] = {
                "mode": "continuous_xadd",
                "offered_fps": monitored["offered_fps"],
                "offered_points_per_second": monitored["offered_points_per_second"],
                "fed_frames": fed, "feed_seconds": feed_s,
                "kept_up": monitored["kept_up"],
                "backlog_trend": monitored["backlog_trend"],
                "backlog_max_frames": monitored["backlog_max"],
                "backlog_at_end_of_feed_frames": monitored["backlog_end_of_feed"],
                "post_feed_drain_seconds": monitored["post_feed_drain_seconds"],
                "written_value_rows_per_second": round(counts["value_rows"] / feed_s, 2),
                "written_quality_rows_per_second": round(counts["quality_rows"] / feed_s, 2),
                "note": ("持续喂入用 XADD-only（字段与生产一致，不产生 receipt/latest/window "
                         "副作用），因此这是 Writer 侧上限；kept_up 只说明『该档位能跟上』，"
                         "不等于找到上限——上限要用更高的 offered_fps 逼近"),
            }
            samples = [read_sample_fields(td_config, device, report["seed"]["stream_columns"],
                                         max(2, ticks_fed), base_ts, args.period_ms,
                                         sample_ticks=args.sample_ticks)
                       for device in devices[:2]]
        else:
            counts = read_counts(td_config, devices, args.points, args.ticks,
                                 expected_quality_rows=report["seed"]["quality_rows_expected"])
            report["rows"] = counts
            if monitored["drained_at_s"]:
                report["capability"]["value_rows_per_second"] = round(
                    counts["value_rows"] / monitored["drained_at_s"], 2)
                report["capability"]["quality_rows_per_second"] = round(
                    counts["quality_rows"] / monitored["drained_at_s"], 2)
            samples = [read_sample_fields(td_config, device, report["seed"]["stream_columns"],
                                         args.ticks, base_ts, args.period_ms,
                                         sample_ticks=args.sample_ticks)
                       for device in devices[:2]]
        report["full_field_sample"] = {
            "devices_checked": len(samples), "devices_total": len(devices),
            "columns_checked": report["seed"]["stream_columns_at_sample"],
            "columns_note": ("只核对真正进入历史的点（NULL/非存储/STRING 点按语义没有列）"),
            "sampled_ticks_per_device": args.sample_ticks + 1,
            "ticks_total": args.ticks, "results": samples,
            "coverage_note": ("只对每设备 2+1 帧做逐列全字段核对；其余行只做行数与身份核对，"
                              "不得表述为全部点值正确")}

        dlq = client.llen(f"rt:dlq:{device_type}:{group}")
        report["dlq_entries"] = dlq
        problems = []
        if counts["value_rows"] != counts["expected_value_rows"]:
            problems.append(f"值行数 {counts['value_rows']} != 期望 {counts['expected_value_rows']}")
        if counts["quality_rows"] != counts["expected_quality_rows"]:
            problems.append(f"质量行数 {counts['quality_rows']} != 期望 "
                            f"{counts['expected_quality_rows']}")
        for item in samples:
            if item["problem_count"]:
                problems.append(f"{item['device']} 抽样全字段不符 {item['problem_count']} 处")
            if item["sampled_frames_found"] != item["sampled_ticks"]:
                problems.append(f"{item['device']} 抽样帧缺失："
                                f"{item['sampled_frames_found']}/{item['sampled_ticks']}")
        if dlq:
            problems.append(f"DLQ 非空：{dlq}")
        report["verdicts"] = {
            "drained": (verdict(PASS, f"积压排空用时 {monitored['drained_at_s']}s")
                        if monitored["drained_at_s"] else
                        verdict("fail", "超时未排空")),
            "rows_match": correctness_verdict(problems[:5], checked="行数与抽样全字段核对"),
            "writer_process": verdict(
                PASS if process.returncode == 0 else "fail",
                f"writer 退出码 {process.returncode}"
                + ("（shutdown_flag 正常停机）" if graceful else "（超时后强制终止）")),
        }
        if continuous:
            # "排空"在持续模式下是必然的（喂完就停），真正要判的是**喂入期间有没有跟上**；
            # 否则"没跟上"也会显示 overall=pass，把结论说反。
            report["verdicts"]["kept_up_under_load"] = verdict(
                PASS if monitored["kept_up"] else "fail",
                (f"持续 {monitored['offered_fps']} 帧/秒喂入期间积压 "
                 f"{monitored['backlog_trend']}（峰值 {monitored['backlog_max']} 帧，"
                 f"停喂后排空 {monitored['post_feed_drain_seconds']}s）"
                 + ("" if monitored["kept_up"] else "：**未跟上**，该档位超过 Writer 能力")))
        report["overall_status"] = overall_status(report["verdicts"])
    except (Exception, SystemExit, KeyboardInterrupt) as exc:
        report["overall_status"] = "fail"
        report["error"] = f"{type(exc).__name__}: {exc}"
    finally:
        try:
            stop_writer_process(process, runtime)
        except Exception as exc:
            report["process_cleanup_error"] = f"{type(exc).__name__}: {exc}"
            report["overall_status"] = "fail"
        if not args.keep_resources:
            report["cleanup"] = cleanup_round_resources(
                cfg, database=database, client=client, keys=keys if client is not None else [],
                owned_databases=owned)
            if report["cleanup"].get("errors"):
                report["overall_status"] = "fail"
        if client is not None:
            try:
                client.close()
            except Exception as exc:
                report["redis_close_error"] = f"{type(exc).__name__}: {exc}"
                report["overall_status"] = "fail"
        output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({k: v for k, v in report.items() if k != "drain"}, ensure_ascii=False, indent=2))
    return exit_code_for(report["overall_status"])



if __name__ == "__main__":
    raise SystemExit(main())
