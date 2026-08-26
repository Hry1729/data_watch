from __future__ import annotations

import asyncio
import base64
import logging
import os
import signal
import sys
from pathlib import Path

from edge.app.storage_client import EdgeStorageClient
from edge.app.uploader import (
    RequestSigner,
    SingleInstanceLock,
    Uploader,
    UploaderSettings,
)


logger = logging.getLogger("edge.uploader")


def configure_logging() -> None:
    level_name = os.environ.get("DATAWATCH_LOG_LEVEL", "INFO").strip().upper()
    level = getattr(logging, level_name, logging.INFO)
    logging.basicConfig(
        level=level,
        format="%(asctime)s | %(levelname)s | %(name)s | %(message)s",
        force=True,
    )


def settings_from_env() -> tuple[UploaderSettings, RequestSigner]:
    center_url = os.environ.get("DATAWATCH_CENTER_URL", "").strip()
    node_uuid = os.environ.get("DATAWATCH_NODE_UUID", "").strip()
    private_key_b64 = os.environ.get("DATAWATCH_UPLOADER_PRIVATE_KEY_B64", "").strip()
    if not center_url:
        raise RuntimeError("DATAWATCH_CENTER_URL is required")
    if not node_uuid:
        raise RuntimeError("DATAWATCH_NODE_UUID is required")
    if not private_key_b64:
        raise RuntimeError("DATAWATCH_UPLOADER_PRIVATE_KEY_B64 is required")
    try:
        private_key_pem = base64.b64decode(private_key_b64, validate=True)
    except ValueError as exc:
        raise RuntimeError("DATAWATCH_UPLOADER_PRIVATE_KEY_B64 is invalid") from exc

    data_root = Path(
        os.environ.get("DATAWATCH_DATA_ROOT") or r"C:\ProgramData\DataWatch"
    )
    settings = UploaderSettings(
        center_url=center_url,
        node_uuid=node_uuid,
        private_key_path=data_root / "edge" / "unused-private-key.pem",
        lock_path=data_root / "edge" / "uploader.lock",
        max_offline_retention_days=_positive_int_env(
            "DATAWATCH_MAX_OFFLINE_RETENTION_DAYS", 7
        ),
        maintenance_interval_seconds=float(
            _positive_int_env("DATAWATCH_OUTBOX_MAINTENANCE_SECONDS", 60)
        ),
    )
    return settings, RequestSigner(private_key_pem)


def _positive_int_env(name: str, default: int) -> int:
    raw = os.environ.get(name, "").strip()
    if not raw:
        return default
    try:
        value = int(raw)
    except ValueError as exc:
        raise RuntimeError(f"{name} must be a positive integer") from exc
    if value < 1:
        raise RuntimeError(f"{name} must be a positive integer")
    return value


async def main() -> int:
    settings, signer = settings_from_env()
    logger.info(
        "Uploader starting node=%s center=%s max_batch_items=%d max_batch_bytes=%d",
        settings.node_uuid,
        settings.center_url,
        settings.max_batch_items,
        settings.max_batch_bytes,
    )
    store = EdgeStorageClient()
    uploader = Uploader(store, settings, signer=signer)
    loop = asyncio.get_running_loop()
    for sig in (signal.SIGINT, signal.SIGTERM):
        try:
            loop.add_signal_handler(sig, uploader.stop)
        except NotImplementedError:
            signal.signal(sig, lambda *_args: uploader.stop())

    try:
        with SingleInstanceLock(settings.lock_path):
            await uploader.run_forever()
    finally:
        await uploader.close()
        store.close()
        logger.info("Uploader stopped")
    return 0


if __name__ == "__main__":
    configure_logging()
    exit_code = asyncio.run(main())
    if exit_code:
        raise SystemExit(exit_code)
