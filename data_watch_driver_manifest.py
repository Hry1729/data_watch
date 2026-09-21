"""driver.toml 清单解析与校验"""

from __future__ import annotations

import platform
import re
import sys
import tomllib
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Sequence, Tuple

def parse_semver(value: str) -> tuple[int, int, int]:
    parts = value.split(".")
    if len(parts) != 3:
        raise ValueError(f"不是合法的 SemVer X.Y.Z: {value!r}")
    return tuple(int(part) for part in parts)

# 第一期支持的能力枚举
ALLOWED_CAPABILITIES = frozenset(
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

# 驱动 id 正则：[a-z][a-z0-9_]{1,63}
_ID_PATTERN = re.compile(r"^[a-z][a-z0-9_]{1,63}$")
_SEMVER_PATTERN = re.compile(r"^\d+\.\d+\.\d+$")
MAX_PROTOCOL_CHARS = 20
MAX_VERSION_CHARS = 50
MAX_NAME_CHARS = 200
MAX_DESCRIPTION_CHARS = 255

# 必填字段
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


class DriverManifestError(ValueError):
    """driver.toml 校验失败。"""


@dataclass(frozen=True)
class DriverManifest:
    """外置驱动清单。"""

    manifest_version: int
    driver_api: str
    id: str
    protocol: str
    name: str
    description: str
    version: str
    entry: str
    python_abi: str
    os: Tuple[str, ...]
    architectures: Tuple[str, ...]
    capabilities: Tuple[str, ...]
    read_write: str
    source_path: Path
    # 可选：驱动要求的最低 collector 版本（SemVer X.Y.Z）
    min_collector_version: str | None = None
    aliases: Tuple[str, ...] = ()
    publisher: str = ""
    dependencies: Tuple[str, ...] = ()
    resources: Tuple[str, ...] = ()
    managed_components: Tuple[Tuple[str, str], ...] = ()


def load_driver_toml(path: Path) -> DriverManifest:
    """读取并校验 driver.toml。"""
    try:
        with path.open("rb") as fp:
            data = tomllib.load(fp)
    except tomllib.TOMLDecodeError as exc:
        raise DriverManifestError(f"driver.toml 解析失败: {path}: {exc}") from exc
    except OSError as exc:
        raise DriverManifestError(f"无法读取 driver.toml: {path}: {exc}") from exc

    if not isinstance(data, dict):
        raise DriverManifestError(f"driver.toml 根节点必须是表: {path}")

    _validate_required_fields(data, path)
    _validate_manifest_version(data["manifest_version"], path)
    _validate_driver_api(data["driver_api"], path)
    _validate_id(data["id"], path)
    protocol = _validate_protocol(data["protocol"], path)
    _validate_version(data["version"], path)
    name = _validate_text(data["name"], "name", MAX_NAME_CHARS, path)
    description = _validate_text(
        data["description"], "description", MAX_DESCRIPTION_CHARS, path
    )
    _validate_entry(data["entry"], path)
    capabilities = _validate_capabilities(data["capabilities"], path)
    os_list = _validate_string_list(data["os"], "os", path)
    arch_list = _validate_string_list(data["architectures"], "architectures", path)
    read_write = _validate_read_write(data["read_write"], path)
    _validate_read_write_vs_capabilities(read_write, capabilities, path)
    # 可选字段：缺省表示不限制 collector 版本
    min_collector_version = data.get("min_collector_version")
    if min_collector_version is not None:
        _validate_version(min_collector_version, path, field="min_collector_version")

    aliases = data.get("aliases", [protocol])
    aliases = tuple(_validate_string_list(aliases, "aliases", path))
    if len(set(aliases)) != len(aliases) or any(not _ID_PATTERN.fullmatch(alias) for alias in aliases):
        raise DriverManifestError(f"aliases 必须为不重复的协议标识: {path}")
    publisher = data.get("publisher", "")
    if not isinstance(publisher, str):
        raise DriverManifestError(f"publisher 必须为字符串: {path}")
    dependencies = data.get("dependencies", [])
    resources = data.get("resources", [])
    components = data.get("managed_components", {})
    if not isinstance(components, dict) or any(name != "opcda_proxy" or version != "1.0" for name, version in components.items()):
        raise DriverManifestError("只支持 Agent 固定管理的 opcda_proxy 1.0；驱动包不能定义安装脚本或服务命令")
    for key, values in (("dependencies", dependencies), ("resources", resources)):
        if not isinstance(values, list) or any(not isinstance(value, str) or not value for value in values):
            raise DriverManifestError(f"{key} 必须为字符串列表: {path}")
    for resource in resources:
        candidate = (path.parent / resource).resolve()
        if not candidate.is_relative_to(path.parent.resolve()) or not candidate.is_file():
            raise DriverManifestError(f"资源不存在或越界: {resource}")

    return DriverManifest(
        manifest_version=int(data["manifest_version"]),
        driver_api=str(data["driver_api"]),
        id=str(data["id"]),
        protocol=protocol,
        name=name,
        description=description,
        version=str(data["version"]),
        entry=str(data["entry"]),
        python_abi=str(data["python_abi"]),
        os=tuple(os_list),
        architectures=tuple(arch_list),
        capabilities=capabilities,
        read_write=read_write,
        source_path=path,
        aliases=aliases, publisher=publisher,
        dependencies=tuple(dependencies), resources=tuple(resources), managed_components=tuple(sorted(components.items())),
        min_collector_version=(
            str(min_collector_version) if min_collector_version is not None else None
        ),
    )


def current_python_abi() -> str:
    """根据解释器版本与 GIL 状态计算当前 python_abi（如 cp314 / cp314t）。"""
    import sysconfig

    ver = sys.version_info
    tag = f"cp{ver.major}{ver.minor}"
    gil_disabled = bool(sysconfig.get_config_var("Py_GIL_DISABLED"))
    return f"{tag}t" if gil_disabled else tag


def validate_package_compatibility(
    manifest: DriverManifest,
    package_root: Path,
    *,
    expected_abi: str | None = None,
    collector_version: str | None = None,
) -> None:
    """校验目录名、入口落点、ABI/OS/架构与当前运行环境。"""
    if expected_abi is None:
        expected_abi = current_python_abi()
    if package_root.name != manifest.id:
        raise DriverManifestError(
            f"驱动目录名 {package_root.name!r} 必须与 id {manifest.id!r} 一致: "
            f"{manifest.source_path}"
        )

    module_path, _, _func = manifest.entry.partition(":")
    top = module_path.split(".", 1)[0]
    if top != manifest.id:
        raise DriverManifestError(
            f"entry 顶层包名必须为驱动 id {manifest.id!r}，当前为 {manifest.entry!r}: "
            f"{manifest.source_path}"
        )
    rel_parts = module_path.split(".")
    candidate = package_root.joinpath(*rel_parts[:-1], rel_parts[-1] + ".py")
    candidate_pkg = package_root.joinpath(*rel_parts, "__init__.py")
    if not candidate.is_file() and not candidate_pkg.is_file():
        raise DriverManifestError(
            f"entry 模块不在驱动包内: {manifest.entry} ({manifest.source_path})"
        )

    if manifest.python_abi != expected_abi:
        raise DriverManifestError(
            f"python_abi={manifest.python_abi!r} 与运行时 {expected_abi!r} 不匹配: "
            f"{manifest.source_path}"
        )

    current_os = _normalize_os(sys.platform)
    if current_os not in {item.lower() for item in manifest.os}:
        raise DriverManifestError(
            f"当前 OS {current_os!r} 不在驱动 os={manifest.os}: {manifest.source_path}"
        )

    current_arch = _normalize_arch(platform.machine())
    allowed_arch = {_normalize_arch(item) for item in manifest.architectures}
    if current_arch not in allowed_arch:
        raise DriverManifestError(
            f"当前架构 {current_arch!r} 不在驱动 architectures={manifest.architectures}: "
            f"{manifest.source_path}"
        )

    if manifest.min_collector_version is not None:
        if collector_version is None:
            raise DriverManifestError("必须提供目标 Collector 版本")
        required = parse_semver(manifest.min_collector_version)
        current = parse_semver(collector_version)
        if current < required:
            raise DriverManifestError(
                f"驱动要求 collector >= {manifest.min_collector_version}，"
                f"当前为 {collector_version}: {manifest.source_path}"
            )


def _normalize_os(value: str) -> str:
    text = (value or "").lower()
    if text.startswith("win"):
        return "windows"
    if text.startswith("linux"):
        return "linux"
    if text in {"darwin", "mac", "macos"}:
        return "macos"
    return text


def _normalize_arch(value: str) -> str:
    text = (value or "").lower()
    if text in {"x86_64", "amd64", "x64"}:
        return "x86_64"
    if text in {"aarch64", "arm64"}:
        return "arm64"
    return text


def _validate_required_fields(data: dict[str, Any], path: Path) -> None:
    for field in _REQUIRED_FIELDS:
        if field not in data:
            raise DriverManifestError(f"缺少必填字段 {field!r}: {path}")


def _validate_manifest_version(value: Any, path: Path) -> None:
    if value != 1:
        raise DriverManifestError(
            f"不支持的 manifest_version={value!r}，第一期仅支持 1: {path}"
        )


def _validate_driver_api(value: Any, path: Path) -> None:
    if value != "1.0":
        raise DriverManifestError(
            f"不支持的 driver_api={value!r}，第一期仅支持 '1.0': {path}"
        )


def _validate_id(value: Any, path: Path) -> None:
    if not isinstance(value, str) or not _ID_PATTERN.match(value):
        raise DriverManifestError(
            f"id 必须符合正则 [a-z][a-z0-9_]{{1,63}}，当前为 {value!r}: {path}"
        )


def _validate_protocol(value: Any, path: Path) -> str:
    if not isinstance(value, str) or not value.strip():
        raise DriverManifestError(f"protocol 必须为非空字符串: {path}")
    protocol = value.strip()
    if len(protocol) > MAX_PROTOCOL_CHARS:
        raise DriverManifestError(
            f"protocol 最大长度为 {MAX_PROTOCOL_CHARS} 字符: {path}"
        )
    return protocol


def _validate_version(value: Any, path: Path, field: str = "version") -> None:
    if not isinstance(value, str) or not _SEMVER_PATTERN.match(value):
        raise DriverManifestError(
            f"{field} 必须为 SemVer X.Y.Z，当前为 {value!r}: {path}"
        )
    if len(value) > MAX_VERSION_CHARS:
        raise DriverManifestError(
            f"{field} 最大长度为 {MAX_VERSION_CHARS} 字符: {path}"
        )


def _validate_text(value: Any, field: str, max_length: int, path: Path) -> str:
    if not isinstance(value, str) or not value.strip():
        raise DriverManifestError(f"{field} 必须为非空字符串: {path}")
    text = value.strip()
    if len(text) > max_length:
        raise DriverManifestError(f"{field} 最大长度为 {max_length} 字符: {path}")
    return text


def _validate_entry(value: Any, path: Path) -> None:
    if not isinstance(value, str) or ":" not in value:
        raise DriverManifestError(
            f"entry 必须为 package.module:function 格式，当前为 {value!r}: {path}"
        )
    module_path, _, func_name = value.partition(":")
    if not module_path or not func_name:
        raise DriverManifestError(f"entry 格式非法 {value!r}: {path}")


def _validate_capabilities(value: Any, path: Path) -> Tuple[str, ...]:
    if not isinstance(value, list) or not value:
        raise DriverManifestError(f"capabilities 必须为非空列表: {path}")
    unknown = [cap for cap in value if cap not in ALLOWED_CAPABILITIES]
    if unknown:
        raise DriverManifestError(
            f"capabilities 含未知项 {unknown}，允许值为 {sorted(ALLOWED_CAPABILITIES)}: {path}"
        )
    as_str = [str(cap) for cap in value]
    if len(as_str) != len(set(as_str)):
        raise DriverManifestError(f"capabilities 不得重复: {path}")
    return tuple(as_str)


def _validate_string_list(value: Any, field: str, path: Path) -> Sequence[str]:
    if not isinstance(value, list) or not value:
        raise DriverManifestError(f"{field} 必须为非空列表: {path}")
    for item in value:
        if not isinstance(item, str) or not item:
            raise DriverManifestError(f"{field} 列表项必须为非空字符串: {path}")
    return [str(item) for item in value]


def _validate_read_write(value: Any, path: Path) -> str:
    if value not in ("只读", "读写"):
        raise DriverManifestError(
            f"read_write 必须为 '只读' 或 '读写'，当前为 {value!r}: {path}"
        )
    return str(value)


def _validate_read_write_vs_capabilities(
    read_write: str, capabilities: Tuple[str, ...], path: Path
) -> None:
    if read_write != "只读":
        raise DriverManifestError(
            f"collector 生产驱动仅允许 read_write=只读: {path}"
        )
    if "write" in capabilities:
        raise DriverManifestError(
            f"collector 生产驱动不得声明 write 能力: {path}"
        )
