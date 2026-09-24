"""写入器进程入口点。Writer process entry point.

Loads config, wires up Redis / MySQL / TDengine connections,
builds the stream key list, and starts the consumer loop.

Run from the writer/ directory:
    python main.py
"""

from __future__ import annotations

import asyncio
import argparse
import json
import logging
import os
import sys
import time
import traceback
from pathlib import Path

try:
    import tomllib  # Python 3.11+
except ImportError:
    try:
        import tomli as tomllib  # type: ignore[no-redef]
    except ImportError:
        print(
            "ERROR: tomllib/tomli not available. "
            "Install tomli: pip install tomli  (Python < 3.11 only)",
            file=sys.stderr,
        )
        sys.exit(1)

import pymysql
import pymysql.cursors
import redis

from consumer import StreamConsumer
from mysql_mapper import MysqlMapper
from performance import WriterPerformanceMetrics
from tdengine_writer import TDengineWriter
from system_log import SystemLogWriter

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)
logger = logging.getLogger("writer.main")

_REQUIRED_CONFIG_KEYS: dict[str, tuple[str, ...]] = {
    "redis": (
        "host", "port", "db", "password",
        "stream_shard_count", "group_name", "consumer_name",
    ),
    "mysql": ("host", "port", "user", "password", "database"),
    "tdengine": ("host", "port", "user", "password", "database"),
    "writer": (
        "flush_interval_ms", "batch_count", "max_buffer_rows",
        "pending_min_idle_ms",
    ),
}

_STREAM_SCOPE_SQL = """
SELECT DISTINCT LOWER(dt.type_code) AS device_type, dpg.group_code AS group_code
FROM device_variable dv
JOIN device_point_group dpg ON dv.point_group_id = dpg.id
JOIN device d ON dpg.device_id = d.id
JOIN device_type dt ON dt.id = d.type_id
WHERE dv.deleted = 0
  AND dv.enable = 1
  AND dv.td_column_name IS NOT NULL
  AND dv.td_column_name <> ''
  AND dt.type_code IS NOT NULL
  AND dt.type_code <> ''
  AND dpg.group_code IS NOT NULL
  AND dpg.group_code <> ''
ORDER BY device_type, group_code
"""


def _validate_config(config: dict) -> None:
    """在建立连接前验证必需的配置节/键。Validate required config sections/keys before wiring connections."""
    missing: list[str] = []
    for section, keys in _REQUIRED_CONFIG_KEYS.items():
        section_data = config.get(section)
        if not isinstance(section_data, dict):
            missing.append(f"[{section}]")
            continue
        for key in keys:
            if key not in section_data:
                missing.append(f"[{section}] {key}")
    if missing:
        raise ValueError("missing required config key(s): " + ", ".join(missing))


# Agent 经 DATAWATCH_<DB>_<FIELD> 下发解密后的连接信息；env 优先于 settings.toml。
# 生产凭据单一来源为 Agent 的 DPAPI config.toml；settings.toml 仅作 dev 回退与运行参数。
_ENV_CONN_FIELDS: dict[str, tuple[str, ...]] = {
    "mysql": ("host", "port", "user", "database", "password"),
    "redis": ("host", "port", "password"),
    "tdengine": ("host", "port", "user", "database", "password"),
}
_ENV_INT_FIELDS = {"port"}


def _apply_env_overrides(config: dict) -> None:
    """用 DATAWATCH_<DB>_<FIELD> 覆盖 config 的连接字段（就地修改，env 优先）。"""
    for section, fields in _ENV_CONN_FIELDS.items():
        section_data = config.setdefault(section, {})
        if not isinstance(section_data, dict):
            continue
        for field in fields:
            env_name = f"DATAWATCH_{section.upper()}_{field.upper()}"
            value = os.environ.get(env_name)
            if not value:
                continue
            if field in _ENV_INT_FIELDS:
                try:
                    section_data[field] = int(value)
                except ValueError as exc:
                    raise ValueError(
                        f"{env_name} must be an integer, got {value!r}"
                    ) from exc
            else:
                section_data[field] = value


def _discover_stream_scopes(mysql_conn: pymysql.connections.Connection) -> list[tuple[str, str]]:
    """从实时 MySQL 配置中发现流设备/组范围。"""
    with mysql_conn.cursor(pymysql.cursors.DictCursor) as cur:
        cur.execute(_STREAM_SCOPE_SQL)
        rows = cur.fetchall()

    scopes: list[tuple[str, str]] = []
    for row in rows:
        device_type = str(row.get("device_type") or "").strip().lower()
        group_code = str(row.get("group_code") or "").strip()
        if device_type and group_code:
            scopes.append((device_type, group_code))
    return scopes


def _resolve_runtime_dir(arg_value: str | None) -> Path:
    if arg_value:
        return Path(arg_value)
    home = os.environ.get("DATAWATCH_HOME")
    if home:
        return Path(home) / "runtime"
    return Path(r"C:\ProgramData\DataWatch\runtime")


def _partition_stream_keys(stream_keys: list[str], worker_count: int) -> list[list[str]]:
    if worker_count <= 0:
        raise ValueError("consumer_workers must be positive")
    actual_count = min(worker_count, len(stream_keys))
    return [stream_keys[index::actual_count] for index in range(actual_count)]


def _stream_keys_for_scopes(
    stream_scopes: list[tuple[str, str]], shard_count: int
) -> list[str]:
    return [
        f"rt:stream:{device_type}:{group_code}:{shard}"
        for device_type, group_code in stream_scopes
        for shard in range(shard_count)
    ]


async def _refresh_stream_scopes_once(
    mysql_config: dict,
    shard_count: int,
    consumers: list[StreamConsumer],
    partitions: list[list[str]],
    known_stream_keys: set[str],
) -> set[str]:
    """发现并接入新流；已存在的流不移除，以保留消费连续性。"""
    loop = asyncio.get_running_loop()

    def discover() -> list[tuple[str, str]]:
        connection = _open_mysql_connection(mysql_config)
        try:
            return _discover_stream_scopes(connection)
        finally:
            connection.close()

    stream_scopes = await loop.run_in_executor(None, discover)
    discovered_keys = set(_stream_keys_for_scopes(stream_scopes, shard_count))
    new_keys = sorted(discovered_keys - known_stream_keys)
    queued_counts = [0] * len(partitions)
    for stream_key in new_keys:
        worker_index = min(
            range(len(partitions)),
            key=lambda index: len(partitions[index]) + queued_counts[index],
        )
        queued_counts[worker_index] += 1
        consumer = consumers[worker_index]
        consumer.queue_stream_key(stream_key)
        known_stream_keys.add(stream_key)
    return set(new_keys)


async def _watch_stream_scopes(
    mysql_config: dict,
    shard_count: int,
    consumers: list[StreamConsumer],
    partitions: list[list[str]],
    known_stream_keys: set[str],
    interval_s: float = 60.0,
) -> None:
    """周期发现 MySQL 新增流范围，查询失败时保留现有消费者并重试。"""
    interval = max(float(interval_s), 1.0)
    while True:
        try:
            new_keys = await _refresh_stream_scopes_once(
                mysql_config,
                shard_count,
                consumers,
                partitions,
                known_stream_keys,
            )
            if new_keys:
                logger.info("Discovered %d new writer stream key(s): %s", len(new_keys), sorted(new_keys))
        except asyncio.CancelledError:
            raise
        except Exception:
            logger.warning("Writer stream scope refresh failed; will retry", exc_info=True)
        await asyncio.sleep(interval)


def _open_mysql_connection(config: dict) -> pymysql.connections.Connection:
    return pymysql.connect(
        host=config["host"],
        port=config["port"],
        user=config["user"],
        password=config["password"],
        database=config["database"],
        charset="utf8mb4",
        autocommit=True,
    )


def _connect_mapper_connections(
    config: dict,
    first_connection: pymysql.connections.Connection,
    count: int,
) -> list[pymysql.connections.Connection]:
    connections = [first_connection]
    try:
        for _ in range(1, count):
            connections.append(_open_mysql_connection(config))
    except Exception:
        for connection in connections:
            connection.close()
        raise
    return connections

async def _connect_writers(config: dict, count: int, syslog) -> list[TDengineWriter]:
    writers: list[TDengineWriter] = []
    try:
        for _ in range(count):
            writer = TDengineWriter(syslog=syslog)
            await writer.connect(config)
            writers.append(writer)
    except Exception:
        await asyncio.gather(*(writer.close() for writer in writers), return_exceptions=True)
        raise
    return writers

async def _watch_shutdown_flag(consumer_task: asyncio.Future, flag_path: Path, interval: float = 1.0) -> None:
    while not consumer_task.done():
        try:
            if flag_path.exists():
                logger.info("shutdown_flag detected at %s, cancelling consumer", flag_path)
                consumer_task.cancel()
                return
        except OSError:
            pass
        await asyncio.sleep(interval)


async def _cancel_consumer_tasks(tasks: list[asyncio.Task]) -> None:
    for task in tasks:
        task.cancel()
    await asyncio.gather(*tasks, return_exceptions=True)


def _read_worker_backlogs(
    r: redis.Redis, group_name: str, partitions: list[list[str]], now_ms: int,
) -> list[dict[str, int | None]]:
    """Read Redis lag and PEL summaries off the event loop; unknown stays null."""
    statuses: list[dict[str, int | None]] = []
    for stream_keys in partitions:
        lag_known = 0
        lag_unknown = 0
        pending_known = 0
        backlog_unknown = 0
        source_age_unknown = 0
        oldest_stream_age: int | None = None
        oldest_source_age: int | None = None
        for stream_key in list(stream_keys):
            try:
                groups = r.xinfo_groups(stream_key)
                group = next((item for item in groups if item.get("name") == group_name), None)
                if group is None:
                    lag_unknown += 1
                    backlog_unknown += 1
                    continue
                pending = r.xpending(stream_key, group_name)
            except redis.RedisError:
                lag_unknown += 1
                backlog_unknown += 1
                logger.warning("Cannot read writer Redis backlog for %s", stream_key, exc_info=True)
                continue
            lag = group.get("lag")
            if isinstance(lag, int):
                lag_known += lag
            else:
                lag_unknown += 1
            pending_count = int(pending.get("pending", 0))
            pending_known += pending_count
            oldest_id = pending.get("min")
            if not pending_count or not oldest_id:
                continue
            try:
                stream_age = max(0, now_ms - int(str(oldest_id).split("-", 1)[0]))
                oldest_stream_age = max(oldest_stream_age or 0, stream_age)
            except ValueError:
                pass
            try:
                entries = r.xrange(stream_key, min=oldest_id, max=oldest_id, count=1)
                source_ms = int(entries[0][1]["ts"])
                oldest_source_age = max(oldest_source_age or 0, max(0, now_ms - source_ms))
            except (IndexError, KeyError, TypeError, ValueError, redis.RedisError):
                source_age_unknown += 1
        statuses.append({
            "lag_entries": lag_known if lag_unknown == 0 else None,
            "lag_known_entries": lag_known,
            "lag_unknown_streams": lag_unknown,
            "pending_entries": pending_known if backlog_unknown == 0 else None,
            "pending_known_entries": pending_known,
            "backlog_unknown_streams": backlog_unknown,
            "oldest_pending_stream_age_ms": oldest_stream_age,
            "oldest_pending_source_age_ms": oldest_source_age,
            "pending_source_age_unknown_streams": source_age_unknown,
        })
    return statuses


async def _report_performance(
    r: redis.Redis, group_name: str, consumers: list[StreamConsumer],
    partitions: list[list[str]], metrics: list[WriterPerformanceMetrics],
    previous: dict | None = None, elapsed_s: float | None = None,
) -> dict:
    now_ms = int(time.time() * 1000)
    statuses = await asyncio.to_thread(
        _read_worker_backlogs, r, group_name, partitions, now_ms
    )
    workers = []
    previous_workers = {
        worker["consumer_name"]: worker
        for worker in previous["workers"]
    } if previous is not None else {}
    def rates(counters: dict[str, int], earlier: dict[str, int] | None) -> dict | None:
        if earlier is None or elapsed_s is None or elapsed_s <= 0:
            return None
        return {
            "admitted_payload_bytes_per_s": round(
                (counters["payload_bytes_admitted"] - earlier["payload_bytes_admitted"]) / elapsed_s, 2
            ),
            "sql_bytes_per_s": round((counters["sql_bytes"] - earlier["sql_bytes"]) / elapsed_s, 2),
            "committed_frames_per_s": round(
                (counters["committed_value_rows"] - earlier["committed_value_rows"]) / elapsed_s, 2
            ),
            "sql_value_rows_succeeded_per_s": round(
                (counters["sql_value_rows_succeeded"] - earlier["sql_value_rows_succeeded"]) / elapsed_s, 2
            ),
            "sql_quality_rows_succeeded_per_s": round(
                (counters["sql_quality_rows_succeeded"] - earlier["sql_quality_rows_succeeded"]) / elapsed_s, 2
            ),
        }
    for consumer, worker_metrics, status in zip(consumers, metrics, statuses):
        worker_metrics.set_backlog(
            **status,
            oldest_buffered_source_age_ms=consumer.oldest_buffered_source_age_ms(now_ms),
        )
        worker = {"consumer_name": consumer._consumer_name, **worker_metrics.snapshot()}
        earlier = previous_workers.get(consumer._consumer_name)
        worker["rates"] = rates(worker["counters"], earlier["counters"] if earlier else None)
        workers.append(worker)
    counters = {
        name: sum(worker["counters"][name] for worker in workers)
        for name in workers[0]["counters"]
    }
    gauges = {
        "buffer_rows": sum(worker["gauges"]["buffer_rows"] for worker in workers),
        "buffer_bytes": sum(worker["gauges"]["buffer_bytes"] for worker in workers),
    }
    for field in (
        "redis_lag_known_entries", "redis_lag_unknown_streams",
        "redis_pending_known_entries", "redis_backlog_unknown_streams",
        "pending_source_age_unknown_streams",
    ):
        gauges[field] = sum(worker["gauges"][field] for worker in workers)
    for total_field in ("redis_lag_entries", "redis_pending_entries"):
        values = [worker["gauges"][total_field] for worker in workers]
        gauges[total_field] = sum(values) if all(value is not None for value in values) else None
    for name in (
        "oldest_pending_stream_age_ms", "oldest_pending_source_age_ms",
        "oldest_buffered_source_age_ms",
    ):
        ages = [worker["gauges"][name] for worker in workers]
        gauges[name] = max((age for age in ages if age is not None), default=None)
    report = {
        "workers": workers,
        "aggregate": {
            "counters": counters,
            "gauges": gauges,
            # Every worker shares one Python event loop, so do not sum its samples.
            "event_loop_lag_ms": workers[0]["timings"]["event_loop_lag_ms"],
            "rates": rates(counters, previous["aggregate"]["counters"] if previous else None),
        },
    }
    logger.info("[performance-aggregate] %s", json.dumps(report, separators=(",", ":")))
    return report


async def _watch_performance(
    r: redis.Redis, group_name: str, consumers: list[StreamConsumer],
    partitions: list[list[str]], metrics: list[WriterPerformanceMetrics],
    interval_s: float = 30.0,
) -> None:
    report_interval = max(1.0, float(interval_s))
    last_report = time.monotonic()
    previous_report: dict | None = None
    while True:
        due = time.monotonic() + 0.25
        await asyncio.sleep(0.25)
        now = time.monotonic()
        for worker_metrics in metrics:
            worker_metrics.record_event_loop_lag(max(0.0, (now - due) * 1000))
        if now - last_report >= report_interval:
            try:
                previous_report = await _report_performance(
                    r, group_name, consumers, partitions, metrics,
                    previous_report, now - last_report,
                )
            except asyncio.CancelledError:
                raise
            except Exception:
                logger.warning("Writer performance report failed", exc_info=True)
            last_report = time.monotonic()


async def main() -> None:
    parser = argparse.ArgumentParser(description="DataWatch writer")
    parser.add_argument("--runtime-dir", type=str, default=None)
    args = parser.parse_args()
    runtime_dir = _resolve_runtime_dir(args.runtime_dir)
    flag_path = runtime_dir / "writer.shutdown_flag"

    with open("config/settings.toml", "rb") as f:
        config = tomllib.load(f)
    try:
        _apply_env_overrides(config)
        _validate_config(config)
    except ValueError as exc:
        logger.error("Invalid config: %s", exc)
        sys.exit(1)

    rc = config["redis"]
    mc = config["mysql"]
    wc = config["writer"]

    # --- Redis 客户端（同步，在消费者的执行器调用中使用）---
    r = redis.Redis(
        host=rc["host"],
        port=rc["port"],
        db=rc["db"],
        password=rc["password"] or None,
        decode_responses=True,
        socket_connect_timeout=5,
    )
    try:
        r.ping()
        logger.info("Redis connected: %s:%s db=%s", rc["host"], rc["port"], rc["db"])
    except Exception as exc:
        logger.error("Cannot connect to Redis: %s", exc)
        sys.exit(1)

    # --- MySQL 同步连接 ---
    try:
        mysql_conn = _open_mysql_connection(mc)
        logger.info("MySQL connected: %s:%s db=%s", mc["host"], mc["port"], mc["database"])
    except Exception as exc:
        logger.error("Cannot connect to MySQL: %s", exc)
        sys.exit(1)

    # --- 系统日志写入器（专用 MySQL 连接，尽力投递）---
    # 在主 MySQL 连接之后立即启动，以便后续启动
    # 失败（范围发现/TDengine 连接）可以持久化到数据库。
    syslog = SystemLogWriter(mc)
    syslog.start()

    try:
        stream_scopes = _discover_stream_scopes(mysql_conn)
    except Exception as exc:
        logger.error("Cannot discover writer stream scopes from MySQL: %s", exc)
        syslog.emit(
            level="ERROR",
            log_type="storage",
            source="writer.main",
            content="startup fatal error: cannot discover stream scopes",
            exception_stack=traceback.format_exc(),
        )
        syslog.stop()
        mysql_conn.close()
        r.close()
        sys.exit(1)
    if not stream_scopes:
        logger.error("No writer stream scopes found in MySQL")
        syslog.emit(
            level="ERROR",
            log_type="storage",
            source="writer.main",
            content="startup fatal error: no writer stream scopes found in MySQL",
        )
        syslog.stop()
        mysql_conn.close()
        r.close()
        sys.exit(1)
    wc["stable_names"] = sorted({f"sd{device_type}" for device_type, _ in stream_scopes})
    logger.info("Discovered %d stream scope(s): %s", len(stream_scopes), stream_scopes)

    # 构建完整的流键集合并按消费 worker 分片。
    stream_keys = _stream_keys_for_scopes(stream_scopes, rc["stream_shard_count"])
    logger.info("Consuming %d stream key(s): %s", len(stream_keys), stream_keys)
    partitions = _partition_stream_keys(
        stream_keys, int(wc.get("consumer_workers", 4))
    )

    try:
        mapper_connections = _connect_mapper_connections(
            mc, mysql_conn, len(partitions)
        )
    except Exception as exc:
        logger.error("Cannot create per-worker MySQL connections: %s", exc)
        syslog.emit(
            level="ERROR",
            log_type="storage",
            source="writer.main",
            content="startup fatal error: cannot create per-worker MySQL connections",
            exception_stack=traceback.format_exc(),
        )
        syslog.stop()
        r.close()
        sys.exit(1)

    # 每个消费分片使用独立 TDengine 连接；TDengineWriter 内部锁只保护本连接。
    try:
        writers = await _connect_writers(config, len(partitions), syslog)
    except Exception as exc:
        logger.error("Cannot connect to TDengine: %s", exc)
        syslog.emit(
            level="ERROR",
            log_type="storage",
            source="writer.main",
            content="startup fatal error: cannot connect to TDengine",
            exception_stack=traceback.format_exc(),
        )
        syslog.stop()
        for connection in mapper_connections:
            connection.close()
        r.close()
        sys.exit(1)

    mappers = [MysqlMapper(connection) for connection in mapper_connections]
    performance_metrics: list[WriterPerformanceMetrics] = []
    base_consumer_name = str(rc["consumer_name"])
    consumers: list[StreamConsumer] = []
    consumer_tasks = []
    for index, (partition, writer, mapper) in enumerate(
        zip(partitions, writers, mappers), start=1
    ):
        worker_config = {**config, "redis": {**rc}}
        if len(partitions) > 1:
            worker_config["redis"]["consumer_name"] = f"{base_consumer_name}-{index}"
        worker_metrics = WriterPerformanceMetrics()
        performance_metrics.append(worker_metrics)
        consumer = StreamConsumer(
            worker_config,
            mapper,
            writer,
            r,
            syslog=syslog,
            metrics=worker_metrics,
        )
        consumers.append(consumer)
        consumer_tasks.append(
            asyncio.create_task(
                consumer.run_loop(partition),
                name=f"writer-consumer-{index}",
            )
        )
    logger.info("Started %d writer consumer worker(s)", len(consumer_tasks))

    syslog.emit(
        level="INFO",
        log_type="storage",
        source="writer.main",
        content="writer started",
    )

    consumer_task = asyncio.gather(*consumer_tasks)
    watchdog_task = asyncio.create_task(_watch_shutdown_flag(consumer_task, flag_path))
    known_stream_keys = set(stream_keys)
    scope_refresh_task = asyncio.create_task(
        _watch_stream_scopes(
            mc,
            rc["stream_shard_count"],
            consumers,
            partitions,
            known_stream_keys,
            wc.get("scope_refresh_interval_s", 60.0),
        ),
        name="writer-stream-scope-refresh",
    )
    performance_task = asyncio.create_task(
        _watch_performance(
            r, rc["group_name"], consumers, partitions, performance_metrics,
            wc.get("performance_report_interval_s", 30.0),
        ),
        name="writer-performance-monitor",
    )
    try:
        await consumer_task
    except asyncio.CancelledError:
        logger.info("Consumer loop cancelled, shutting down")
    except KeyboardInterrupt:
        logger.info("KeyboardInterrupt received, shutting down")
    except Exception:
        logger.exception("Writer consumer failed; exiting for service supervision")
        syslog.emit(
            level="ERROR",
            log_type="storage",
            source="writer.main",
            content="writer consumer stopped after unexpected exception",
            exception_stack=traceback.format_exc(),
        )
        raise
    finally:
        consumer_task.cancel()
        await _cancel_consumer_tasks(consumer_tasks)
        watchdog_task.cancel()
        try:
            await watchdog_task
        except asyncio.CancelledError:
            pass
        scope_refresh_task.cancel()
        try:
            await scope_refresh_task
        except asyncio.CancelledError:
            pass
        performance_task.cancel()
        try:
            await performance_task
        except asyncio.CancelledError:
            pass
        syslog.emit(
            level="INFO",
            log_type="storage",
            source="writer.main",
            content="writer stopped",
        )
        syslog.stop()
        logger.info(
            "[performance] %s",
            json.dumps(
                {
                    "workers": [
                        {"consumer_name": consumer._consumer_name, **metric.snapshot()}
                        for consumer, metric in zip(consumers, performance_metrics)
                    ],
                    "total_buffer_rows": sum(
                        metric.snapshot()["gauges"]["buffer_rows"]
                        for metric in performance_metrics
                    ),
                },
                separators=(",", ":"),
            ),
        )
        await asyncio.gather(*(writer.close() for writer in writers), return_exceptions=True)
        for connection in mapper_connections:
            connection.close()
        r.close()
        logger.info("Writer process stopped")


if __name__ == "__main__":
    asyncio.run(main())
