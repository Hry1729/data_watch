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


# --- B45：路径片段与规范化包含性 ----------------------------------------------


def test_segment_rejects_dot_and_dotdot() -> None:
    """'.' 与 '..' 能通过字符集校验，但它们是路径跳转，必须在片段层拒绝。"""
    for value in (".", "..", " .. "):
        with pytest.raises(ApiError):
            cli_releases._segment(value, "segment")


def test_segment_still_accepts_ordinary_names() -> None:
    assert cli_releases._segment("windows-x64", "platform") == "windows-x64"
    assert cli_releases._segment("1.2.3", "version") == "1.2.3"
    # 含点但不等价于路径跳转的名称应放行。
    assert cli_releases._segment("..hidden", "file name") == "..hidden"


def test_manifest_path_rejects_dotdot_segments(tmp_path, monkeypatch) -> None:
    release_root = tmp_path / "releases"
    release_root.mkdir(parents=True)
    monkeypatch.setattr(
        cli_releases,
        "get_settings",
        lambda: SimpleNamespace(cli_release_dir=str(release_root)),
    )
    for kwargs in (
        {"channel": "..", "platform": "windows-x64"},
        {"channel": "stable", "platform": ".."},
        {"channel": "stable", "platform": "windows-x64", "version": ".."},
    ):
        with pytest.raises(ApiError):
            cli_releases._manifest_path(**kwargs)


def test_manifest_path_stays_within_root(tmp_path, monkeypatch) -> None:
    release_root = tmp_path / "releases"
    release_root.mkdir(parents=True)
    monkeypatch.setattr(
        cli_releases,
        "get_settings",
        lambda: SimpleNamespace(cli_release_dir=str(release_root)),
    )
    path = cli_releases._manifest_path("stable", "windows-x64", "1.2.3")
    assert path.is_relative_to(release_root.resolve())
    assert path.name == "manifest.json"


def test_resolve_within_rejects_symlink_escape(tmp_path, monkeypatch) -> None:
    """符号链接指向发布根之外时必须被拒（规范化后再判定包含性）。"""
    release_root = tmp_path / "releases"
    outside = tmp_path / "outside"
    release_root.mkdir(parents=True)
    outside.mkdir(parents=True)
    link = release_root / "escape"
    try:
        link.symlink_to(outside, target_is_directory=True)
    except (OSError, NotImplementedError):
        pytest.skip("此环境不允许创建符号链接")

    with pytest.raises(ApiError):
        cli_releases._resolve_within(release_root.resolve(), link / "latest.json")


@pytest.mark.asyncio
async def test_download_rejects_manifest_file_name_escape(tmp_path, monkeypatch) -> None:
    """清单属半可信输入：fileName 为 '..' 时不得越过版本目录。"""
    release_root = tmp_path / "releases"
    version_root = release_root / "stable" / "windows-x64" / "1.2.3"
    version_root.mkdir(parents=True)
    (version_root / "manifest.json").write_text(
        json.dumps({"version": "1.2.3", "archive": {"fileName": ".."}}),
        encoding="utf-8",
    )
    monkeypatch.setattr(
        cli_releases,
        "get_settings",
        lambda: SimpleNamespace(cli_release_dir=str(release_root)),
    )

    with pytest.raises(ApiError):
        await cli_releases.download_cli_release("1.2.3")
