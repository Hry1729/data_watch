from __future__ import annotations

import asyncio
import base64
import logging
import os
import signal
import sys
from pathlib import Path

from edge.app.paths import edge_database_path
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
    # B25 遗留 P1-C：唯一运行权必须与它保护的 outbox 同源。此前锁路径由
    # DATAWATCH_DATA_ROOT 单独派生，而库路径由 edge_database_path()（优先
    # DATAWATCH_EDGE_DB）决定；两者分指时会出现「同库不同锁」——两个实例各持一把锁
    # 同时运行，后启动者的 recover_inflight 会把前者**未过期**的 live 租约重置。
    # 默认部署两者本就指向同一 data_root\edge\，行为不变。
    lock_path = edge_database_path().parent / "uploader.lock"
    settings = UploaderSettings(
        center_url=center_url,
        node_uuid=node_uuid,
        private_key_path=data_root / "edge" / "unused-private-key.pem",
        lock_path=lock_path,
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
    try:
        # B25：先取得唯一运行权，再做任何 outbox 状态恢复/维护。
        # 抢锁失败时不得触碰存储状态（构造器已无副作用）。
        with SingleInstanceLock(settings.lock_path):
            uploader = Uploader(store, settings, signer=signer)
            loop = asyncio.get_running_loop()
            for sig in (signal.SIGINT, signal.SIGTERM):
                try:
                    loop.add_signal_handler(sig, uploader.stop)
                except NotImplementedError:
                    signal.signal(sig, lambda *_args: uploader.stop())

            recovered = await uploader.recover_inflight()
            if recovered:
                logger.info("Recovered %d inflight outbox records", recovered)
            try:
                await uploader.run_forever()
            finally:
                await uploader.close()
    finally:
        store.close()
        logger.info("Uploader stopped")
    return 0


if __name__ == "__main__":
    configure_logging()
    exit_code = asyncio.run(main())
    if exit_code:
        raise SystemExit(exit_code)
