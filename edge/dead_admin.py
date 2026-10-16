"""本机 dead 运维入口：用 operator 令牌走 Edge Storage IPC。

用法::

    python -m edge.dead_admin list
    python -m edge.dead_admin export --format jsonl|csv --out ...
    python -m edge.dead_admin replay --id <batch_id>
    python -m edge.dead_admin replay --all --device QC100 --dry-run

不开放中心远程 API。令牌来自本机 storage-ipc-tokens.json 的 operator 角色。
"""

from __future__ import annotations

import argparse
import json
import sys
from typing import Any

from edge.app.storage_client import (
    EdgeStorageClient,
    StorageUnauthorized,
    StorageUnavailable,
)


def _client() -> EdgeStorageClient:
    return EdgeStorageClient(role="operator")


def _print_json(payload: Any) -> None:
    print(json.dumps(payload, ensure_ascii=False, default=str))


def _cmd_list(args: argparse.Namespace) -> int:
    client = _client()
    try:
        rows = client.list_dead(limit=args.limit, offset=args.offset, device_code=args.device)
    finally:
        client.close()
    for row in rows:
        _print_json(row)
    return 0


def _cmd_export(args: argparse.Namespace) -> int:
    client = _client()
    try:
        result = client.export_dead_batch(
            args.out,
            fmt=args.format,
            device_code=args.device,
            since_ms=args.since_ms,
            until_ms=args.until_ms,
            last_error=args.last_error,
            node_uuid=args.node_uuid,
        )
    finally:
        client.close()
    _print_json(result)
    return 0


def _cmd_replay(args: argparse.Namespace) -> int:
    if args.all == bool(args.id):
        raise SystemExit("replay 需要且只能指定 --id 或 --all 其中之一")
    client = _client()
    try:
        if args.id:
            rows = _find_by_id(client, args.id, args.device)
            if args.dry_run:
                _print_json({"dry_run": True, "count": len(rows), "rows": rows})
                return 0 if rows else 1
            if not rows:
                _print_json({"replayed": False, "reason": "not_found", "id": args.id})
                return 1
            for row in rows:
                _print_json(
                    client.replay_dead(
                        row["batch_id"],
                        row["upload_session_id"],
                        int(row["local_seq"]),
                    )
                )
            return 0
        rows = _collect(client, args.device)
        pending = [row for row in rows if not row.get("replayed_at")]
        if args.dry_run:
            _print_json({"dry_run": True, "count": len(pending), "rows": pending})
            return 0
        for row in pending:
            _print_json(
                client.replay_dead(
                    row["batch_id"],
                    row["upload_session_id"],
                    int(row["local_seq"]),
                )
            )
        return 0
    finally:
        client.close()


def _collect(client: EdgeStorageClient, device: str | None) -> list[dict[str, Any]]:
    found: list[dict[str, Any]] = []
    offset = 0
    while True:
        page = client.list_dead(limit=500, offset=offset, device_code=device)
        if not page:
            return found
        found.extend(page)
        if len(page) < 500:
            return found
        offset += 500


def _find_by_id(
    client: EdgeStorageClient,
    batch_id: str,
    device: str | None,
) -> list[dict[str, Any]]:
    return [row for row in _collect(client, device) if str(row.get("batch_id")) == batch_id]


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(prog="edge.dead_admin", description="查看、导出或重放本机 dead 载荷")
    sub = parser.add_subparsers(dest="command", required=True)

    list_parser = sub.add_parser("list", help="列出 dead 元数据，不含载荷")
    list_parser.add_argument("--device", default=None)
    list_parser.add_argument("--limit", type=int, default=50)
    list_parser.add_argument("--offset", type=int, default=0)
    list_parser.set_defaults(func=_cmd_list)

    export_parser = sub.add_parser("export", help="批量导出到 JSONL 或 CSV")
    export_parser.add_argument("--format", required=True, choices=("jsonl", "csv"))
    export_parser.add_argument("--out", required=True)
    export_parser.add_argument("--device", default=None)
    export_parser.add_argument("--since-ms", dest="since_ms", type=int, default=None)
    export_parser.add_argument("--until-ms", dest="until_ms", type=int, default=None)
    export_parser.add_argument("--last-error", dest="last_error", default=None)
    export_parser.add_argument("--node-uuid", dest="node_uuid", default=None)
    export_parser.add_argument(
        "--all",
        action="store_true",
        help="导出当前筛选条件下的全部 dead（默认即如此，保留该开关给回滚前留底）",
    )
    export_parser.set_defaults(func=_cmd_export)

    replay_parser = sub.add_parser("replay", help="把 dead 重新入队为新的 outbox 行")
    replay_parser.add_argument("--id", default=None, help="dead 行的 batch_id")
    replay_parser.add_argument("--all", action="store_true")
    replay_parser.add_argument("--device", default=None)
    replay_parser.add_argument("--dry-run", action="store_true")
    replay_parser.set_defaults(func=_cmd_replay)
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    try:
        return int(args.func(args))
    except StorageUnauthorized as exc:
        print(str(exc), file=sys.stderr)
        return 1
    except StorageUnavailable as exc:
        print(str(exc), file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
