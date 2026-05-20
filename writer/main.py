"""Writer process entry point.

Loads config, wires up Redis / MySQL / TDengine connections,
builds the stream key list, and starts the consumer loop.

Run from the writer/ directory:
    python main.py
"""

from __future__ import annotations

import asyncio
import logging
import sys

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
import redis

from consumer import StreamConsumer
from mysql_mapper import MysqlMapper
from tdengine_writer import TDengineWriter

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s %(levelname)s %(name)s: %(message)s",
)
logger = logging.getLogger("writer.main")


async def main() -> None:
    with open("config/settings.toml", "rb") as f:
        config = tomllib.load(f)

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

    # --- TDengine writer ---
    writer = TDengineWriter()
    try:
        await writer.connect(config)
    except Exception as exc:
        logger.error("Cannot connect to TDengine: %s", exc)
        sys.exit(1)

    mapper = MysqlMapper(mysql_conn)
    consumer = StreamConsumer(config, mapper, writer, r)

    # Build the full set of stream keys:
    #   rt:stream:{device_type}:{group_code}:{shard_no}
    stream_keys = [
        f"rt:stream:{dt}:{gc}:{shard}"
        for dt in wc["device_types"]
        for gc in wc["group_codes"]
        for shard in range(rc["stream_shard_count"])
    ]
    logger.info("Consuming %d stream key(s): %s", len(stream_keys), stream_keys)

    try:
        await consumer.run_loop(stream_keys)
    except asyncio.CancelledError:
        logger.info("Consumer loop cancelled, shutting down")
    except KeyboardInterrupt:
        logger.info("KeyboardInterrupt received, shutting down")
    finally:
        await writer.close()
        mysql_conn.close()
        r.close()
        logger.info("Writer process stopped")


if __name__ == "__main__":
    asyncio.run(main())
