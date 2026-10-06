"""Offline driver catalog and package integrity, without executing driver code."""
from __future__ import annotations
import hashlib
import json
import os
import sys
import zipfile
from pathlib import Path

OFFICIAL_ALIASES = {
    "s7": ("s7", "snap7", "siemens", "siemens_s7", "siemens_snap7"),
    "ge_srtp": ("ge_srtp",), "opcua": ("opcua", "opc", "opc_ua"),
    "opcda_center": ("opcda_center",), "modbus_tcp": ("modbus_tcp",),
}
RESERVED_PROTOCOLS = frozenset(alias for aliases in OFFICIAL_ALIASES.values() for alias in aliases)


def file_digest(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def package_files(root: Path) -> list[Path]:
    return sorted((p for p in root.rglob("*") if p.is_file() and "__pycache__" not in p.parts and p.suffix != ".pyc"), key=lambda p: p.relative_to(root).as_posix())


def tree_digest(root: Path) -> str:
    digest = hashlib.sha256()
    for path in package_files(root):
        if path.is_symlink() or not path.resolve().is_relative_to(root.resolve()):
            raise ValueError("驱动包不允许外部链接")
        digest.update(path.relative_to(root).as_posix().encode("utf-8"))
        digest.update(b"\0")
        digest.update(bytes.fromhex(file_digest(path)))
    return digest.hexdigest()


def default_catalog_path() -> Path:
    configured = os.environ.get("DATAWATCH_DRIVER_CATALOG_ROOT")
    if configured:
        return Path(configured) / "catalog.json"
    if getattr(sys, "frozen", False):
        return Path(sys.executable).parent / "driver-catalog/catalog.json"
    return Path(__file__).resolve().parent / "packages/driver-catalog/catalog.json"


def read_catalog(path: Path) -> dict:
    data = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(data, dict) or data.get("schema_version") != 2 or not isinstance(data.get("drivers"), list):
        raise ValueError("不支持的驱动目录格式")
    ids = set()
    for item in data["drivers"]:
        if not isinstance(item, dict) or item.get("id") not in OFFICIAL_ALIASES or item["id"] in ids:
            raise ValueError("官方目录包含重复或未知驱动")
        ids.add(item["id"])
        archive = item.get("archive", "")
        if not archive or Path(archive).name != archive or not archive.endswith(".zip"):
            raise ValueError("非法驱动归档路径")
    keys = data.get("trusted_publishers", [])
    if not isinstance(keys, list) or any(not isinstance(key, dict) or any(not isinstance(key.get(field), str) or not key[field] for field in ("publisher", "key_id", "public_key")) for key in keys):
        raise ValueError("可信发布者密钥目录格式错误")
    approved = data.get("approved_packages", [])
    if not isinstance(approved, list) or any(not isinstance(item, dict) for item in approved):
        raise ValueError("审核包目录格式错误")
    return data


def verify_identity(package_root: Path, manifest, catalog: dict | None, record: dict | None = None) -> str:
    digest = tree_digest(package_root)
    names = {manifest.id, manifest.protocol.lower(), *manifest.aliases}
    reserved = bool(names & RESERVED_PROTOCOLS) or manifest.publisher == "datawatch"
    if reserved:
        if manifest.publisher != "datawatch" or manifest.id not in OFFICIAL_ALIASES:
            raise ValueError("官方协议名称必须由可信 DataWatch 包提供")
        if manifest.protocol != manifest.id or set(manifest.aliases) != set(OFFICIAL_ALIASES[manifest.id]):
            raise ValueError("官方包协议或别名不匹配")
        catalog_ok = any(item.get("id") == manifest.id and item.get("tree_sha256") == digest for item in (catalog or {}).get("drivers", []))
        installed_ok = bool(record and record.get("publisher") == "datawatch" and record.get("tree_sha256") == digest)
        if not (catalog_ok or installed_ok):
            raise ValueError("官方包未通过发布来源校验；请使用离线目录中已验证的版本")
    else:
        approved = any(item.get("id") == manifest.id and item.get("publisher") == manifest.publisher
            and item.get("version") == manifest.version and item.get("tree_sha256") == digest
            for item in (catalog or {}).get("approved_packages", []))
        installed = bool(record and record.get("approved_source") and record.get("tree_sha256") == digest)
        if record and record.get("tree_sha256") and record["tree_sha256"] != digest:
            raise ValueError("已安装驱动内容与审核摘要不一致")
        if getattr(sys, "frozen", False) and not (approved or installed):
            raise ValueError("生产运行时仅加载经审核的驱动包")
    return digest


def write_catalog(packages_root: Path, destination: Path, version: str, *, executable: str | None = None) -> None:
    """Build reproducible offline archives. This never changes an activation directory."""
    from data_watch_driver_manifest import load_driver_toml
    packages_root = Path(packages_root)
    if (packages_root / "packages").is_dir():
        packages_root = packages_root / "packages"
    trust_path = packages_root / "driver-catalog/catalog.json"
    trust = read_catalog(trust_path) if trust_path.is_file() else {}
    destination.parent.mkdir(parents=True, exist_ok=True)
    entries = []
    for driver_id in OFFICIAL_ALIASES:
        root = packages_root / driver_id
        manifest = load_driver_toml(root / "driver.toml")
        archive = f"{driver_id}-{manifest.version}.zip"
        archive_path = destination.parent / archive
        with zipfile.ZipFile(archive_path, "w", zipfile.ZIP_DEFLATED) as output:
            for path in package_files(root):
                info = zipfile.ZipInfo(f"{driver_id}/{path.relative_to(root).as_posix()}", date_time=(2020, 1, 1, 0, 0, 0))
                info.compress_type = zipfile.ZIP_DEFLATED
                output.writestr(info, path.read_bytes())
        entries.append({"id": manifest.id, "protocol": manifest.protocol, "name": manifest.name,
                        "description": manifest.description, "version": manifest.version,
                        "publisher": manifest.publisher, "aliases": list(manifest.aliases),
                        "capabilities": list(manifest.capabilities), "readWriteType": manifest.read_write,
                        "archive": archive, "sha256": file_digest(archive_path), "tree_sha256": tree_digest(root)})
    data = {"schema_version": 2, "collector_version": version, "python_abi": "cp314t", "drivers": entries}
    for field in ("trusted_publishers", "approved_packages"):
        if field in trust:
            data[field] = trust[field]
    data["build_id"] = hashlib.sha256(json.dumps(data, sort_keys=True).encode()).hexdigest()
    destination.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
