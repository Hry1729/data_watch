from __future__ import annotations

import argparse
import logging
import os
import signal
import sys
from pathlib import Path
import threading


REPO_ROOT = Path(__file__).resolve().parents[1]
COLLECTOR_ROOT = REPO_ROOT / "collector"
for source_root in (REPO_ROOT, COLLECTOR_ROOT):
    if str(source_root) not in sys.path:
        sys.path.insert(0, str(source_root))

from edge.app.paths import edge_database_path
from edge.app.storage_service import StorageEngine, StorageHttpServer


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="DataWatch Edge Storage")
    parser.add_argument("--host", default=os.environ.get("DATAWATCH_EDGE_STORAGE_HOST", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.environ.get("DATAWATCH_EDGE_STORAGE_PORT", "7073")))
    parser.add_argument("--queue-capacity", type=int, default=int(os.environ.get("DATAWATCH_EDGE_STORAGE_QUEUE_CAPACITY", "2048")))
    parser.add_argument(
        "--shutdown-timeout-seconds",
        type=float,
        default=float(os.environ.get("DATAWATCH_EDGE_STORAGE_SHUTDOWN_TIMEOUT_SECONDS", "30")),
    )
    return parser


def main() -> int:
    args = _parser().parse_args()
    logging.basicConfig(
        level=os.environ.get("DATAWATCH_LOG_LEVEL", "INFO"),
        format="%(asctime)s [%(levelname)s] %(name)s - %(message)s",
    )
    engine = StorageEngine(
        edge_database_path(),
        max_queue=args.queue_capacity,
        shutdown_timeout_seconds=args.shutdown_timeout_seconds,
    )
    engine.start()
    server = StorageHttpServer((args.host, args.port), engine)
    server.timeout = 0.5
    stopped = threading.Event()

    def stop(*_args) -> None:
        stopped.set()

    for sig in (signal.SIGINT, signal.SIGTERM):
        signal.signal(sig, stop)

    data_root = Path(os.environ.get("DATAWATCH_DATA_ROOT") or r"C:\ProgramData\DataWatch Edge")
    runtime_dir = Path(os.environ.get("DATAWATCH_RUNTIME_DIR") or data_root / "runtime")
    shutdown_flag = runtime_dir / "storage.shutdown_flag"
    shutdown_flag.unlink(missing_ok=True)

    logging.getLogger("edge.storage").info(
        "Edge Storage listening on http://%s:%s database=%s",
        args.host,
        args.port,
        edge_database_path(),
    )
    try:
        while not stopped.is_set() and not shutdown_flag.exists():
            server.handle_request()
    finally:
        server.server_close()
        engine.close()
        shutdown_flag.unlink(missing_ok=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
