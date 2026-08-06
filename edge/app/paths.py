from __future__ import annotations

import os
import sys
from pathlib import Path


def edge_database_path() -> Path:
    configured = os.environ.get("DATAWATCH_EDGE_DB")
    if configured:
        return Path(configured)
    data_root = os.environ.get("DATAWATCH_DATA_ROOT")
    if data_root:
        return Path(data_root) / "edge" / "edge.db"
    if getattr(sys, "frozen", False):
        return Path(r"C:\ProgramData\DataWatch\edge\edge.db")
    return Path(__file__).resolve().parents[2] / "edge" / "data" / "edge.db"
