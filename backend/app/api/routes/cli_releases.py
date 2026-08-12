from __future__ import annotations

import json
import re
from pathlib import Path
from typing import Any

from turboapi import APIRouter
from turboapi.responses import FileResponse

from app.core.responses import success_response
from app.core.config import get_settings
from app.core.exceptions import ApiError


router = APIRouter()
SEGMENT_PATTERN = re.compile(r"^[A-Za-z0-9._-]+$")


def _segment(value: str, label: str) -> str:
    normalized = str(value).strip()
    if not SEGMENT_PATTERN.fullmatch(normalized):
        raise ApiError(400, f"Invalid {label}")
    return normalized


def _manifest_path(channel: str, platform: str, version: str | None = None) -> Path:
    root = Path(get_settings().cli_release_dir).resolve()
    base = root / _segment(channel, "channel") / _segment(platform, "platform")
    if version is None:
        return base / "latest.json"
    return base / _segment(version, "version") / "manifest.json"


def _load_manifest(path: Path) -> dict[str, Any]:
    try:
        payload = json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise ApiError(404, "CLI release not found") from exc
    except (OSError, ValueError) as exc:
        raise ApiError(500, "CLI release manifest is invalid") from exc
    if not isinstance(payload, dict):
        raise ApiError(500, "CLI release manifest is invalid")
    return payload


@router.get("/api/cli/releases/latest")
async def latest_cli_release(
    channel: str = "stable",
    platform: str = "windows-x64",
):
    return success_response(data=_load_manifest(_manifest_path(channel, platform)))


@router.get("/api/cli/releases/{version}/download")
async def download_cli_release(
    version: str,
    channel: str = "stable",
    platform: str = "windows-x64",
):
    manifest_path = _manifest_path(channel, platform, version)
    manifest = _load_manifest(manifest_path)
    archive = manifest.get("archive")
    if not isinstance(archive, dict):
        raise ApiError(500, "CLI release manifest is invalid")
    file_name = _segment(str(archive.get("fileName") or ""), "file name")
    file_path = manifest_path.parent / file_name
    if not file_path.is_file():
        raise ApiError(404, "CLI release artifact not found")
    return FileResponse(
        path=str(file_path),
        media_type="application/zip",
        filename=file_name,
    )
