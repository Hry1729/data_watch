from __future__ import annotations

import asyncio
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
COLLECTOR_ROOT = REPO_ROOT / "collector"
for source_root in (REPO_ROOT, COLLECTOR_ROOT):
    if str(source_root) not in sys.path:
        sys.path.insert(0, str(source_root))

from edge.app.config_snapshot import EdgeConfigManager
from edge.app.paths import edge_database_path
from edge.app.storage_client import EdgeStorageClient
from storage.edge_outbox_writer import EdgeOutboxWriter

def _writer_factory(_config, metrics):
    return EdgeOutboxWriter(EdgeStorageClient(), metrics)


def _config_manager_factory(_config):
    return EdgeConfigManager(edge_database_path(), storage=EdgeStorageClient())


async def main():
    from main import main as collector_main

    return await collector_main(
        writer_factory=_writer_factory,
        config_manager_factory=_config_manager_factory,
    )


if __name__ == "__main__":
    exit_code = asyncio.run(main())
    if exit_code not in (None, 0):
        raise SystemExit(exit_code)
