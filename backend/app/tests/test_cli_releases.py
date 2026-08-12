from __future__ import annotations

import json
from types import SimpleNamespace

import pytest

from app.api.routes import cli_releases
from app.core.exceptions import ApiError


@pytest.mark.asyncio
async def test_latest_release_reads_configured_manifest(tmp_path, monkeypatch) -> None:
    release_root = tmp_path / "releases"
    target = release_root / "stable" / "windows-x64"
    target.mkdir(parents=True)
    (target / "latest.json").write_text(
        json.dumps({"version": "1.2.3", "archive": {}}),
        encoding="utf-8",
    )
    monkeypatch.setattr(
        cli_releases,
        "get_settings",
        lambda: SimpleNamespace(cli_release_dir=str(release_root)),
    )

    response = await cli_releases.latest_cli_release()

    assert response["data"]["version"] == "1.2.3"


@pytest.mark.asyncio
async def test_release_segments_reject_path_traversal(tmp_path, monkeypatch) -> None:
    monkeypatch.setattr(
        cli_releases,
        "get_settings",
        lambda: SimpleNamespace(cli_release_dir=str(tmp_path)),
    )

    with pytest.raises(ApiError):
        await cli_releases.latest_cli_release(channel="../private")

@pytest.mark.asyncio
async def test_download_release_returns_manifest_archive(tmp_path, monkeypatch) -> None:
    release_root = tmp_path / "releases"
    version_root = release_root / "stable" / "windows-x64" / "1.2.3"
    version_root.mkdir(parents=True)
    archive = version_root / "release.zip"
    archive.write_bytes(b"zip")
    (version_root / "manifest.json").write_text(
        json.dumps({"version": "1.2.3", "archive": {"fileName": "release.zip"}}),
        encoding="utf-8",
    )
    monkeypatch.setattr(
        cli_releases,
        "get_settings",
        lambda: SimpleNamespace(cli_release_dir=str(release_root)),
    )

    response = await cli_releases.download_cli_release("1.2.3")

    assert str(response.path) == str(archive)
