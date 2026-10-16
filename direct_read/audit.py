"""本地审计日志。只记谁、何时、哪台设备/点位和结果，不记密码或令牌。"""

from __future__ import annotations

import json
import os
from datetime import datetime, timezone
from pathlib import Path


_FORBIDDEN_KEYS = frozenset(
    {
        "password",
        "token",
        "refresh_token",
        "refreshtoken",
        "secret",
        "authorization",
        "mysql_password",
        "access_token",
    }
)
_SECRET_NAME_PARTS = ("PASSWORD", "TOKEN", "SECRET", "AUTHORIZATION")
_MAX_BYTES = 1_048_576


def default_state_dir() -> Path:
    root = os.environ.get("DATAWATCH_DATA_ROOT")
    if root:
        return Path(root) / "direct_read"
    return Path.home() / ".datawatch" / "direct_read"


def environment_secrets() -> list[str]:
    """从环境变量收集需要从日志里抹掉的机密值。不返回变量名。"""
    secrets: list[str] = []
    for key, value in os.environ.items():
        if not value or len(value) < 4:
            continue
        upper = key.upper()
        if any(part in upper for part in _SECRET_NAME_PARTS):
            secrets.append(value)
    return secrets


def scrub_text(text: str, secrets: list[str] | tuple[str, ...] = ()) -> str:
    redacted = text
    for secret in (*secrets, *environment_secrets()):
        if secret and len(secret) >= 4:
            redacted = redacted.replace(secret, "***")
    return redacted


def record_audit(
    *,
    actor: str | None,
    device: str | None,
    point: str | None,
    outcome: str,
    error_type: str | None = None,
    elapsed_ms: int | None = None,
    topology_source: str | None = None,
    audit_path: Path | None = None,
    state_dir: Path | None = None,
    extra_secrets: list[str] | tuple[str, ...] = (),
) -> None:
    """尽力写入一行 JSON。失败不影响读点结果。"""
    try:
        record = {
            "ts": datetime.now(timezone.utc).isoformat(),
            "actor": actor or None,
            "device": device,
            "point": point,
            "outcome": outcome,
            "errorType": error_type,
            "elapsedMs": elapsed_ms,
            "topologySource": topology_source,
        }
        safe = {
            key: value
            for key, value in record.items()
            if key.lower() not in _FORBIDDEN_KEYS
        }
        line = scrub_text(
            json.dumps(safe, ensure_ascii=False, default=str),
            extra_secrets,
        )
        path = audit_path or (state_dir or default_state_dir()) / "audit.log"
        _append_line(path, line)
    except Exception:
        return


def _append_line(path: Path, line: str, *, max_bytes: int = _MAX_BYTES) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.stat().st_size >= max_bytes:
        backup = path.with_name(path.name + ".1")
        if backup.exists():
            backup.unlink()
        path.replace(backup)
    with path.open("a", encoding="utf-8") as handle:
        handle.write(line + "\n")


def audit_record_keys() -> frozenset[str]:
    """审计行允许出现的字段。密码和令牌不在其中。"""
    return frozenset(
        {
            "ts",
            "actor",
            "device",
            "point",
            "outcome",
            "errorType",
            "elapsedMs",
            "topologySource",
        }
    )
