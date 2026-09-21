"""驱动 ZIP 静态校验与安全解压（安装阶段不 import / 不执行驱动代码）。"""

from __future__ import annotations

import hashlib
import io
import logging
import os
import re
import tomllib
import unicodedata
import zipfile
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable
from data_watch_driver_manifest import load_driver_toml, DriverManifestError
from data_watch_driver_catalog import verify_identity, read_catalog, default_catalog_path

logger = logging.getLogger(__name__)

# 上传与解压限制（设计 §9.1）
MAX_ZIP_BYTES = 50 * 1024 * 1024
MAX_ZIP_ENTRIES = 5_000
MAX_SINGLE_FILE_BYTES = 20 * 1024 * 1024
MAX_UNCOMPRESSED_BYTES = 100 * 1024 * 1024
MAX_PATH_COMPONENT_CHARS = 200
MAX_ZIP_RELATIVE_PATH_CHARS = 240
MAX_FINAL_PATH_CHARS = 259
MAX_PROTOCOL_CHARS = 20
MAX_VERSION_CHARS = 50
MAX_NAME_CHARS = 200
MAX_DESCRIPTION_CHARS = 255

_ID_PATTERN = re.compile(r"^[a-z][a-z0-9_]{1,63}$")
_SEMVER_PATTERN = re.compile(r"^\d+\.\d+\.\d+$")
_ALLOWED_CAPABILITIES = frozenset(
    {
        "poll",
        "read",
        "write",
        "batch_contiguous",
        "batch_random",
        "health_check",
        "native_quality",
        "source_timestamp",
    }
)
_REQUIRED_FIELDS = (
    "manifest_version",
    "driver_api",
    "id",
    "protocol",
    "name",
    "description",
    "version",
    "entry",
    "python_abi",
    "os",
    "architectures",
    "capabilities",
    "read_write",
)

# 内置协议码：外置包不得覆盖（与 collector DriverRegistry 对齐）
BUILTIN_PROTOCOLS = frozenset(
    {
        "s7",
        "snap7",
        "siemens",
        "siemens_s7",
        "siemens_snap7",
        "ge_srtp",
        "opc",
        "opcua",
        "opc_ua",
        "opcda_center",
        "modbus_tcp",
    }
)

# collector 运行时 ABI（校验相对 collector，而非 Agent 解释器）
EXPECTED_COLLECTOR_ABI = "cp314t"


class DriverZipError(Exception):
    """ZIP / 清单静态校验失败。"""

    def __init__(self, message: str, *, status_code: int = 400):
        super().__init__(message)
        self.status_code = status_code


class StagingPathConflictError(DriverZipError):
    """staging 已存在或是链接；调用方不得删除该路径。"""


def is_valid_driver_id(value: object) -> bool:
    """Return whether *value* is a valid manifest/installation directory ID."""
    return isinstance(value, str) and _ID_PATTERN.fullmatch(value) is not None


@dataclass(frozen=True)
class ValidatedDriverPackage:
    """通过静态校验的驱动包元数据。"""

    driver_id: str
    protocol: str
    version: str
    name: str
    description: str
    entry: str
    capabilities: tuple[str, ...]
    read_write: str
    python_abi: str
    sha256: str
    package_root_name: str  # ZIP 内唯一包根目录名（应等于 driver_id）
    aliases: tuple[str, ...] = ()
    publisher: str = ""
    tree_sha256: str = ""
    signature_verified: bool = False


def compute_sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def validate_and_extract(zip_bytes: bytes, staging_dir: Path, *, catalog_path: Path | None = None) -> ValidatedDriverPackage:
    """校验 ZIP 并安全解压到 staging_dir，返回清单元数据。

    staging_dir 必须为空或不存在；解压后应仅含一个包根（含 driver.toml）。
    """
    if len(zip_bytes) > MAX_ZIP_BYTES:
        raise DriverZipError(
            f"ZIP 超过大小限制 {MAX_ZIP_BYTES} bytes",
            status_code=413,
        )

    sha256 = compute_sha256(zip_bytes)
    _create_exclusive_staging_dir(staging_dir)

    try:
        zf = zipfile.ZipFile(io.BytesIO(zip_bytes))
    except zipfile.BadZipFile as exc:
        raise DriverZipError(f"非法 ZIP: {exc}", status_code=400) from exc

    with zf:
        _assert_no_dangerous_entries(zf)
        _assert_size_limits(zf)
        _safe_extract(zf, staging_dir)

    package_root = _resolve_single_package_root(staging_dir)
    manifest_path = package_root / "driver.toml"
    if not manifest_path.is_file():
        raise DriverZipError("包根缺少 driver.toml", status_code=422)

    manifest = _load_and_validate_manifest(manifest_path, package_root)
    if package_root.name != manifest["id"]:
        raise DriverZipError(
            f"包根目录名 {package_root.name!r} 必须与 id {manifest['id']!r} 一致",
            status_code=422,
        )
    try:
        catalog = read_catalog(catalog_path or default_catalog_path())
    except (OSError, ValueError):
        catalog = None
    try:
        from data_watch_driver_signatures import verify_package_signature
        from data_watch_driver_catalog import tree_digest
        parsed = load_driver_toml(manifest_path)
        signature_verified = verify_package_signature(package_root, parsed, catalog)
        signed_record = {"publisher": parsed.publisher, "tree_sha256": tree_digest(package_root), "approved_source": True} if signature_verified else None
        package_digest = verify_identity(package_root, parsed, catalog, signed_record)
    except ValueError as exc:
        raise DriverZipError(str(exc), status_code=409) from exc

    return ValidatedDriverPackage(
        driver_id=manifest["id"],
        protocol=manifest["protocol"],
        version=manifest["version"],
        name=manifest["name"],
        description=manifest["description"],
        entry=manifest["entry"],
        capabilities=tuple(manifest["capabilities"]),
        read_write=manifest["read_write"],
        python_abi=manifest["python_abi"],
        sha256=sha256,
        package_root_name=package_root.name,
        aliases=tuple(manifest["aliases"]), publisher=manifest["publisher"], tree_sha256=package_digest, signature_verified=signature_verified,
    )


def _assert_no_dangerous_entries(zf: zipfile.ZipFile) -> None:
    names = zf.namelist()
    if len(names) > MAX_ZIP_ENTRIES:
        raise DriverZipError(
            f"ZIP 条目数超过限制 {MAX_ZIP_ENTRIES}",
            status_code=413,
        )
    if not names:
        raise DriverZipError("ZIP 为空", status_code=400)

    for info in zf.infolist():
        name = info.filename.replace("\\", "/")
        if name.lower().endswith(".pyc") or "__pycache__" in name.lower().split("/"):
            raise DriverZipError("驱动 ZIP 不允许预编译字节码或 __pycache__，避免绕过源码签名", status_code=400)
        if not name or name.endswith("/"):
            if name:
                _assert_safe_member_path(name.rstrip("/"))
            continue
        if info.is_dir():
            _assert_safe_member_path(name.rstrip("/"))
            continue
        # 符号链接 / 硬链接（Unix 外带属性）；Windows 上 ZipInfo 也可能带外带属性
        if _is_symlink_or_link(info):
            raise DriverZipError(f"拒绝符号链接/硬链接: {name}", status_code=400)
        _assert_safe_member_path(name)

    # Windows 大小写不敏感 + Unicode 规范化：拒绝会落到同一路径的条目
    _assert_no_path_collisions(zf)


def _is_symlink_or_link(info: zipfile.ZipInfo) -> bool:
    # Unix: 高 16 位为 mode；symlink 为 0o120000
    mode = (info.external_attr >> 16) & 0xFFFF
    if mode & 0o170000 == 0o120000:
        return True
    # 部分工具用 create_system + external_attr 标记；保守拒绝带有 symlink 标志的条目
    if getattr(info, "is_symlink", None) and info.is_symlink():  # type: ignore[misc]
        return True
    return False


# Windows 保留设备名（含扩展名变体，如 CON.txt）
_WINDOWS_RESERVED_NAMES = frozenset(
    {
        "CON",
        "PRN",
        "AUX",
        "NUL",
        *(f"COM{i}" for i in range(1, 10)),
        *(f"LPT{i}" for i in range(1, 10)),
    }
)


def _normalized_path_key(name: str) -> str:
    """生成 Unicode NFC + casefold 路径键，用于检测 Windows 落点碰撞。"""
    normalized = name.replace("\\", "/").rstrip("/")
    parts = [p for p in normalized.split("/") if p and p != "."]
    return "/".join(unicodedata.normalize("NFC", p).casefold() for p in parts)


def _assert_no_path_collisions(zf: zipfile.ZipFile) -> None:
    """拒绝重复路径、文件/目录冲突与前缀冲突（解压前拦截覆盖）。"""
    seen: dict[str, tuple[str, bool]] = {}
    for info in zf.infolist():
        name = info.filename.replace("\\", "/")
        if not name:
            continue
        is_dir = bool(name.endswith("/") or info.is_dir())
        key = _normalized_path_key(name)
        if not key:
            continue

        if key in seen:
            prev_name, _prev_dir = seen[key]
            raise DriverZipError(
                f"拒绝规范化路径碰撞: {prev_name!r} 与 {name!r}",
                status_code=400,
            )

        # 父路径已是文件 → 不能再解压其子路径
        parts = key.split("/")
        for i in range(1, len(parts)):
            prefix = "/".join(parts[:i])
            if prefix in seen and not seen[prefix][1]:
                raise DriverZipError(
                    f"拒绝文件/目录前缀冲突: {seen[prefix][0]!r} 与 {name!r}",
                    status_code=400,
                )

        # 当前是文件，但已有以其为前缀的子路径 → 冲突
        if not is_dir:
            prefix_slash = key + "/"
            for existing_key, (prev_name, _prev_dir) in seen.items():
                if existing_key.startswith(prefix_slash):
                    raise DriverZipError(
                        f"拒绝文件/目录前缀冲突: {name!r} 与 {prev_name!r}",
                        status_code=400,
                    )

        seen[key] = (name, is_dir)


def _assert_safe_member_path(name: str) -> None:
    normalized = name.replace("\\", "/")
    if normalized.startswith("/") or normalized.startswith("\\"):
        raise DriverZipError(f"拒绝绝对路径: {name}", status_code=400)
    if re.match(r"^[A-Za-z]:", normalized):
        raise DriverZipError(f"拒绝盘符路径: {name}", status_code=400)
    if normalized.startswith("//") or normalized.startswith("\\\\"):
        raise DriverZipError(f"拒绝 UNC 路径: {name}", status_code=400)
    # NTFS ADS：路径分量或文件名中含冒号（非盘符前缀）均拒绝
    if ":" in normalized:
        raise DriverZipError(f"拒绝 NTFS ADS / 冒号路径: {name}", status_code=400)
    parts = [p for p in normalized.split("/") if p not in ("", ".")]
    if any(p == ".." for p in parts):
        raise DriverZipError(f"拒绝路径穿越: {name}", status_code=400)
    relative = "/".join(parts)
    if _windows_char_count(relative) > MAX_ZIP_RELATIVE_PATH_CHARS:
        raise DriverZipError(f"ZIP 相对路径过长: {name}", status_code=400)
    for part in parts:
        if _windows_char_count(part) > MAX_PATH_COMPONENT_CHARS:
            raise DriverZipError(f"路径分量过长: {part}", status_code=400)
        # 尾随点或空格：Windows 会剥离，易造成解压落点偏离预期
        if part.endswith(".") or part.endswith(" ") or part.startswith(" "):
            raise DriverZipError(f"拒绝尾随/前导点或空格的路径分量: {name}", status_code=400)
        stem = part.split(".", 1)[0].upper()
        if stem in _WINDOWS_RESERVED_NAMES:
            raise DriverZipError(f"拒绝 Windows 保留设备名: {name}", status_code=400)


def _assert_size_limits(zf: zipfile.ZipFile) -> None:
    total = 0
    for info in zf.infolist():
        if info.is_dir():
            continue
        if info.file_size > MAX_SINGLE_FILE_BYTES:
            raise DriverZipError(
                f"单文件超过限制 {MAX_SINGLE_FILE_BYTES}: {info.filename}",
                status_code=413,
            )
        total += info.file_size
        if total > MAX_UNCOMPRESSED_BYTES:
            raise DriverZipError(
                f"解压总大小超过限制 {MAX_UNCOMPRESSED_BYTES}",
                status_code=413,
            )


def _safe_extract(zf: zipfile.ZipFile, dest: Path) -> None:
    if _is_link_or_junction(dest):
        raise DriverZipError("staging 目录不得为符号链接或目录联接", status_code=400)
    dest_resolved = dest.resolve(strict=True)
    for info in zf.infolist():
        name = info.filename.replace("\\", "/")
        if not name or name.endswith("/"):
            # 仅创建目录（仍校验路径）
            if name:
                _assert_safe_member_path(name.rstrip("/"))
                target = dest / Path(*[p for p in name.split("/") if p])
                _ensure_within(dest_resolved, target)
                target.mkdir(parents=True, exist_ok=True)
                _ensure_within(dest_resolved, target)
            continue

        _assert_safe_member_path(name)
        parts = [p for p in name.split("/") if p and p != "."]
        target = dest.joinpath(*parts)
        _ensure_within(dest_resolved, target)
        target.parent.mkdir(parents=True, exist_ok=True)
        _ensure_within(dest_resolved, target)
        with zf.open(info, "r") as src, open(target, "wb") as out:
            # 按块写出，防止异常压缩比在 file_size 声明不准时失控
            written = 0
            while True:
                chunk = src.read(1024 * 1024)
                if not chunk:
                    break
                written += len(chunk)
                if written > MAX_SINGLE_FILE_BYTES:
                    raise DriverZipError(
                        f"解压单文件超过限制: {name}",
                        status_code=413,
                    )
                out.write(chunk)


def _ensure_within(root: Path, target: Path) -> None:
    target_absolute = Path(os.path.abspath(target))
    _assert_final_path_length(target_absolute)
    _assert_no_existing_links(root, target_absolute)
    try:
        target_absolute.resolve(strict=False).relative_to(root)
    except ValueError as exc:
        # ✅ 只返回 ZIP 内相对路径，不泄漏绝对路径
        relative_path = target.relative_to(root) if target.is_relative_to(root) else target.name
        raise DriverZipError(f"解压路径越界: {relative_path}", status_code=400) from exc


def _create_exclusive_staging_dir(staging_dir: Path) -> None:
    parent = staging_dir.parent
    parent.mkdir(parents=True, exist_ok=True)
    if not parent.is_dir() or _is_link_or_junction(parent):
        raise StagingPathConflictError(
            "staging 父目录不是安全的普通目录", status_code=409
        )
    try:
        staging_dir.mkdir(mode=0o700, exist_ok=False)
    except FileExistsError as exc:
        raise StagingPathConflictError(
            "staging 路径已存在，拒绝覆盖，无法独占创建", status_code=409
        ) from exc
    if _is_link_or_junction(staging_dir) or staging_dir.resolve().parent != parent.resolve():
        raise StagingPathConflictError(
            "staging 路径越界或为链接", status_code=409
        )


def _is_link_or_junction(path: Path) -> bool:
    if path.is_symlink():
        return True
    isjunction = getattr(os.path, "isjunction", None)
    return bool(isjunction and isjunction(path))


def _assert_no_existing_links(root: Path, target: Path) -> None:
    cursor = target
    while cursor != root:
        if cursor.exists() and _is_link_or_junction(cursor):
            # ✅ 只返回相对于 root 的路径，不泄漏绝对路径
            relative_path = cursor.relative_to(root) if cursor.is_relative_to(root) else cursor.name
            raise DriverZipError(f"解压路径包含链接: {relative_path}", status_code=400)
        parent = cursor.parent
        if parent == cursor:
            break
        cursor = parent


def _windows_char_count(value: str | Path) -> int:
    return len(str(value).encode("utf-16-le")) // 2


def _assert_final_path_length(target: Path) -> None:
    if _windows_char_count(target) > MAX_FINAL_PATH_CHARS:
        # ✅ 只返回文件名，不泄漏完整路径
        raise DriverZipError(f"最终落点路径过长（文件名: {target.name}，总长度超过 {MAX_FINAL_PATH_CHARS} 字符）", status_code=400)


def _resolve_single_package_root(staging_dir: Path) -> Path:
    """要求 staging 下恰好一个包根目录（含 driver.toml），或 driver.toml 直接在根下。"""
    children = [p for p in staging_dir.iterdir() if p.name not in (".", "..")]
    # 允许 ZIP 直接以 driver.toml 为根（无外层目录）——规范上应有包根；
    # 设计要求「只允许一个包根和一个 driver.toml」，故根级 driver.toml 时包根=staging，
    # 但激活目录名必须等于 id，因此根级布局在校验后由调用方改名。这里拒绝根级散落多文件无目录。
    toml_at_root = staging_dir / "driver.toml"
    if toml_at_root.is_file():
        # 根级布局：整个 staging 即包内容，调用方需确保目录名在激活时等于 id
        return staging_dir

    dirs = [p for p in children if p.is_dir()]
    files = [p for p in children if p.is_file()]
    if files:
        raise DriverZipError(
            "ZIP 根只能有一个包目录，不得夹带散落文件",
            status_code=400,
        )
    if len(dirs) != 1:
        raise DriverZipError(
            f"ZIP 必须恰好包含一个包根目录，当前 {len(dirs)} 个",
            status_code=400,
        )
    package_root = dirs[0]
    # 包根内只允许一个 driver.toml（位于包根顶层）
    nested = list(package_root.rglob("driver.toml"))
    if len(nested) != 1 or nested[0] != package_root / "driver.toml":
        raise DriverZipError(
            "包内必须恰好有一个位于包根的 driver.toml",
            status_code=422,
        )
    return package_root


def _load_and_validate_manifest(path: Path, package_root: Path) -> dict[str, Any]:
    try:
        manifest = load_driver_toml(path)
    except DriverManifestError as exc:
        # Keep filesystem details in the log, not in the API response.
        logger.warning("清单校验失败: %s", exc)
        message = str(exc).replace(str(path), "driver.toml")
        raise DriverZipError(message, status_code=422) from exc
    if manifest.python_abi != EXPECTED_COLLECTOR_ABI:
        raise DriverZipError(f"python_abi={manifest.python_abi!r} 与目标 Collector 不匹配", status_code=422)
    if "windows" not in manifest.os or "x86_64" not in {_normalize_arch(v) for v in manifest.architectures}:
        raise DriverZipError("驱动要求 windows/x86_64", status_code=422)
    module, _, function = manifest.entry.partition(":")
    parts = module.split(".")
    if parts[0] != manifest.id or any(not part.isidentifier() for part in parts) or not function.isidentifier():
        raise DriverZipError("entry 顶层包名必须为驱动 id，且模块名称合法", status_code=422)
    if not (package_root.joinpath(*parts).with_suffix(".py").is_file() or package_root.joinpath(*parts, "__init__.py").is_file()):
        raise DriverZipError("entry 模块不在驱动包内", status_code=422)
    return {"id": manifest.id, "protocol": manifest.protocol, "version": manifest.version,
            "name": manifest.name, "description": manifest.description, "entry": manifest.entry,
            "capabilities": list(manifest.capabilities), "read_write": manifest.read_write,
            "python_abi": manifest.python_abi, "aliases": manifest.aliases, "publisher": manifest.publisher}


def _normalize_arch(value: str) -> str:
    text = (value or "").lower()
    if text in {"x86_64", "amd64", "x64"}:
        return "x86_64"
    if text in {"aarch64", "arm64"}:
        return "arm64"
    return text


def iter_builtin_protocols() -> Iterable[str]:
    return sorted(BUILTIN_PROTOCOLS)
