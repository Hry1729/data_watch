"""离线驱动目录必须与 packages/<id> 源码同步（PR #27 R2：改了驱动源码却没重建 zip/目录/签名）。"""
from pathlib import Path
import hashlib
import io
import json
import zipfile

import pytest

from data_watch_driver_catalog import OFFICIAL_ALIASES, file_digest, read_catalog, tree_digest
from data_watch_driver_manifest import load_driver_toml

ROOT = Path(__file__).resolve().parents[2]
PACKAGES = ROOT / "packages"
CATALOG = PACKAGES / "driver-catalog/catalog.json"


def _entry(driver_id):
    return next(item for item in read_catalog(CATALOG)["drivers"] if item["id"] == driver_id)


@pytest.mark.parametrize("driver_id", sorted(OFFICIAL_ALIASES))
def test_catalog_entry_matches_package_source(driver_id):
    root = PACKAGES / driver_id
    manifest = load_driver_toml(root / "driver.toml")
    entry = _entry(driver_id)
    assert entry["version"] == manifest.version
    assert entry["archive"] == f"{driver_id}-{manifest.version}.zip"
    assert entry["tree_sha256"] == tree_digest(root), "packages 源码已变，需重新生成离线目录"
    archive = CATALOG.parent / entry["archive"]
    assert archive.is_file()
    assert entry["sha256"] == file_digest(archive)
    # zip 内容与源码逐文件一致
    with zipfile.ZipFile(archive) as bundle:
        names = sorted(n for n in bundle.namelist() if not n.endswith("/"))
        digest = hashlib.sha256()
        for name in names:
            rel = name.split("/", 1)[1]
            digest.update(rel.encode("utf-8"))
            digest.update(b"\0")
            digest.update(hashlib.sha256(bundle.read(name)).digest())
    assert digest.hexdigest() == entry["tree_sha256"]


@pytest.mark.parametrize("driver_id", sorted(OFFICIAL_ALIASES))
def test_signed_package_signature_covers_current_version_and_files(driver_id):
    """带 signature.json 的包：签名载荷的版本与逐文件哈希必须是当前源码（改代码/升版本后需重签）。"""
    root = PACKAGES / driver_id
    signature = root / "signature.json"
    if not signature.is_file():
        pytest.skip("未独立签名的官方包，由可信离线目录 tree_sha256 担保")
    manifest = load_driver_toml(root / "driver.toml")
    payload = json.loads(signature.read_text(encoding="utf-8"))["payload"]
    assert payload["version"] == manifest.version, "驱动版本已升级但未重新签名"
    for rel, expected in payload["files"].items():
        assert file_digest(root / rel) == expected, f"{rel} 已修改但未重新签名"
