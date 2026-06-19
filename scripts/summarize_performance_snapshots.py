"""Extract collector/writer performance snapshots from runtime logs."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


MARKERS = ("Performance snapshot: ", "[performance] ")


def extract_snapshots(path: Path) -> list[dict]:
    snapshots: list[dict] = []
    for line in path.read_text(encoding="utf-8", errors="replace").splitlines():
        for marker in MARKERS:
            marker_index = line.find(marker)
            if marker_index < 0:
                continue
            payload = line[marker_index + len(marker):].strip()
            try:
                snapshots.append(json.loads(payload))
            except json.JSONDecodeError:
                pass
            break
    return snapshots


def summarize(path: Path | None) -> dict:
    if path is None or not path.exists():
        return {"path": str(path) if path else None, "snapshot_count": 0}
    snapshots = extract_snapshots(path)
    return {
        "path": str(path.resolve()),
        "snapshot_count": len(snapshots),
        "first": snapshots[0] if snapshots else None,
        "last": snapshots[-1] if snapshots else None,
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--collector-log", type=Path)
    parser.add_argument("--writer-log", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    result = {
        "collector": summarize(args.collector_log),
        "writer": summarize(args.writer_log),
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(result, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
