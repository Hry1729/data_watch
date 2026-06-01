"""Writer process entry point.

Loads config, wires up Redis / MySQL / TDengine connections,
builds the stream key list, and starts the consumer loop.

Run from the writer/ directory:
    python main.py
"""

from __future__ import annotations

import asyncio
import argparse
import logging
import os
import sys
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
        "pending_min_idle_ms", "pending_max_retry",
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
    """Validate required config sections/keys before wiring connections."""
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


def _discover_stream_scopes(mysql_conn: pymysql.connections.Connection) -> list[tuple[str, str]]:
    """Discover stream device/group scopes from the live MySQL configuration."""
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


async def _watch_shutdown_flag(consumer_task: asyncio.Task, flag_path: Path, interval: float = 1.0) -> None:
    while not consumer_task.done():
        try:
            if flag_path.exists():
                logger.info("shutdown_flag detected at %s, cancelling consumer", flag_path)
                consumer_task.cancel()
                return
        except OSError:
            pass
        await asyncio.sleep(interval)


async def main() -> None:
    parser = argparse.ArgumentParser(description="DataWatch writer")
    parser.add_argument("--runtime-dir", type=str, default=None)
    args = parser.parse_args()
    runtime_dir = _resolve_runtime_dir(args.runtime_dir)
    flag_path = runtime_dir / "writer.shutdown_flag"

    with open("config/settings.toml", "rb") as f:
        config = tomllib.load(f)
    try:
        _validate_config(config)
    except ValueError as exc:
        logger.error("Invalid config: %s", exc)
        sys.exit(1)

    rc = config["redis"]
    mc = config["mysql"]
    wc = config["writer"]

    # --- Redis client (sync, used inside executor calls in consumer) ---
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

    # --- MySQL synchronous connection ---
    try:
        mysql_conn = pymysql.connect(
            host=mc["host"],
            port=mc["port"],
            user=mc["user"],
            password=mc["password"],
            database=mc["database"],
            charset="utf8mb4",
            autocommit=True,
        )
        logger.info("MySQL connected: %s:%s db=%s", mc["host"], mc["port"], mc["database"])
    except Exception as exc:
        logger.error("Cannot connect to MySQL: %s", exc)
        sys.exit(1)

    # --- System log writer (dedicated MySQL connection, best-effort) ---
    # Started right after the main MySQL connection so subsequent startup
    # failures (scope discovery / TDengine connect) can be persisted to DB.
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

    # --- TDengine writer ---
    writer = TDengineWriter(syslog=syslog)
    try:
        await writer.connect(config)
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
        sys.exit(1)

    mapper = MysqlMapper(mysql_conn)
    consumer = StreamConsumer(config, mapper, writer, r, syslog=syslog)

    # Build the full set of stream keys:
    #   rt:stream:{device_type}:{group_code}:{shard_no}
    stream_keys = [
        f"rt:stream:{dt}:{gc}:{shard}"
        for dt, gc in stream_scopes
        for shard in range(rc["stream_shard_count"])
    ]
    logger.info("Consuming %d stream key(s): %s", len(stream_keys), stream_keys)

    syslog.emit(
        level="INFO",
        log_type="storage",
        source="writer.main",
        content="writer started",
    )

    consumer_task = asyncio.create_task(consumer.run_loop(stream_keys))
    watchdog_task = asyncio.create_task(_watch_shutdown_flag(consumer_task, flag_path))
    try:
        await consumer_task
    except asyncio.CancelledError:
        logger.info("Consumer loop cancelled, shutting down")
    except KeyboardInterrupt:
        logger.info("KeyboardInterrupt received, shutting down")
    finally:
        watchdog_task.cancel()
        try:
            await watchdog_task
        except asyncio.CancelledError:
            pass
        syslog.emit(
            level="INFO",
            log_type="storage",
            source="writer.main",
            content="writer stopped",
        )
        syslog.stop()
        await writer.close()
        mysql_conn.close()
        r.close()
        logger.info("Writer process stopped")


if __name__ == "__main__":
    asyncio.run(main())
