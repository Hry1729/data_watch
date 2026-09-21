"""外置驱动注册表：扫描 driver.toml 并动态加载工厂。"""

from __future__ import annotations

import hashlib
import importlib
import importlib.util
import inspect
import os
import sys
import tomllib
import types
import json
import threading
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Callable, Dict, List, Optional, Set, Tuple

from core.logger import get_logger
from data_watch_driver_catalog import OFFICIAL_ALIASES, RESERVED_PROTOCOLS, default_catalog_path, read_catalog, verify_identity, tree_digest, file_digest
from drivers.base import BaseDriver
from core.version import __version__ as COLLECTOR_VERSION
from drivers.context import DriverContext
from drivers.manifest import (
    DriverManifest,
    DriverManifestError,
    load_driver_toml,
    validate_package_compatibility,
)

BUILTIN_DRIVER_ALIASES = OFFICIAL_ALIASES
BUILTIN_PROTOCOLS = RESERVED_PROTOCOLS
_LOAD_LOCK = threading.RLock()
_VENDOR_OWNERS: dict[str, tuple[str, str]] = {}

# 扫描时忽略的目录名
_IGNORED_SCAN_DIRS = frozenset({".staging", ".backup"})

# 外置驱动合成命名空间（隔离驱动包代码；第三方依赖仍可能进入全局 sys.modules）
_EXT_NAMESPACE = "datawatch_ext"

logger = get_logger("driver_registry")


class DriverRegistryError(RuntimeError):
    """驱动注册表错误。"""


@dataclass(frozen=True)
class VendorMetadata:
    """驱动 vendor 依赖的锁定元数据。"""

    name: str
    version: str
    license: str
    sha256: str


@dataclass(frozen=True)
class DriverRegistration:
    """统一驱动注册项（安装包自带或用户安装）。"""

    manifest: DriverManifest
    package_root: Path
    factory: Callable[[DriverContext], BaseDriver]
    tree_sha256: str = ""
    vendor_metadata: Optional[VendorMetadata] = None
    source: str = "installed"
    protected: bool = False
    aliases: Tuple[str, ...] = ()
    build_read_plan: Optional[Callable[..., Any]] = None
    decode_read_results: Optional[Callable[..., Any]] = None


@dataclass(frozen=True)
class DriverLoadFailure:
    driver_id: str
    protocol: str
    package_root: Path
    stage: str
    error: str

class DriverRegistry:
    """protocol → 外置驱动注册表。"""

    def __init__(
        self,
        registrations: Optional[Dict[str, DriverRegistration]] = None,
        failures: Optional[Dict[str, DriverLoadFailure]] = None,
    ):
        self._registrations: Dict[str, DriverRegistration] = registrations or {}
        self._failures: Dict[str, DriverLoadFailure] = failures or {}
        # 每次 scan 递增；Agent 用其判断是否为重启后的新代际
        self.generation: int = 0

    @classmethod
    def discover(cls, drivers_root: Optional[Path] = None) -> "DriverRegistry":
        """Only the activation directory participates in runtime discovery."""
        return cls.scan(drivers_root if drivers_root is not None else default_drivers_root())

    @classmethod
    def scan(cls, drivers_root: Path, *, package_roots=None, approved_records=None) -> "DriverRegistry":
        """扫描 drivers_root/*/driver.toml 并注册外置驱动。"""
        if package_roots is None and not drivers_root.is_dir():
            return cls()

        registrations: Dict[str, DriverRegistration] = {}
        failures: Dict[str, DriverLoadFailure] = {}
        # 第一轮：仅做清单校验，不 import 任何驱动代码，
        # 以便在加载前拿到全部候选包、建立跨驱动的 vendor 所有权视图。
        candidates: List[tuple[Path, DriverManifest, str]] = []
        try:
            catalog = read_catalog(default_catalog_path())
        except (OSError, ValueError):
            catalog = None
        try:
            installed_records = json.loads((drivers_root / "state.json").read_text(encoding="utf-8")).get("drivers", {})
        except (OSError, ValueError):
            installed_records = {}
        for child in sorted(package_roots if package_roots is not None else drivers_root.iterdir()):
            if not child.is_dir():
                continue
            if child.name.startswith(".") or child.name in _IGNORED_SCAN_DIRS:
                continue

            manifest_path = child / "driver.toml"
            if not manifest_path.is_file():
                continue

            try:
                manifest = load_driver_toml(manifest_path)
                validate_package_compatibility(manifest, child)
                verify_identity(child, manifest, catalog, (approved_records or {}).get(manifest.id) or installed_records.get(manifest.id))
            except Exception as exc:
                logger.error("驱动包清单校验失败 path=%s: %s", child, exc, exc_info=True)
                failures[child.name.lower()] = DriverLoadFailure(
                    driver_id=child.name,
                    protocol=child.name.lower(),
                    package_root=child,
                    stage="manifest",
                    error=str(exc),
                )
                continue

            protocol = manifest.protocol.lower()
            candidates.append((child, manifest, protocol))

        # 第二轮：按扫描顺序确定 vendor 顶层依赖归属，冲突者不进入加载
        vendor_rejections = _resolve_vendor_ownership(candidates)

        # 第三轮：实际 import 并注册
        for child, manifest, protocol in candidates:
            rejection = vendor_rejections.get(protocol)
            if rejection is not None:
                logger.error("驱动包 load_failed：%s path=%s", rejection, child)
                failures[protocol] = DriverLoadFailure(
                    manifest.id, protocol, child, "vendor_conflict", rejection
                )
                continue

            try:
                vendor_metadata = _load_vendor_metadata(child)
                loaded = _load_driver_package(child, manifest, vendor_metadata)
                # 声明批读能力时 plan/decode 必须齐全，缺任意一个都拒绝注册
                if "batch_contiguous" in manifest.capabilities and (
                    loaded.build_read_plan is None
                    or loaded.decode_read_results is None
                ):
                    raise DriverRegistryError(
                        f"驱动 {manifest.id!r} 声明 batch_contiguous 但 planner hooks 不完整"
                        f"（build_read_plan={loaded.build_read_plan is not None},"
                        f" decode_read_results={loaded.decode_read_results is not None}）"
                    )
            except Exception as exc:
                logger.error("驱动包加载失败 path=%s: %s", child, exc, exc_info=True)
                failures[protocol] = DriverLoadFailure(
                    manifest.id, protocol, child, "load", str(exc)
                )
                continue

            aliases = tuple(dict.fromkeys((protocol, *manifest.aliases)))
            if any(alias in registrations for alias in aliases):
                failures[protocol] = DriverLoadFailure(manifest.id, protocol, child, "manifest", "协议或别名重复注册")
                continue
            registration = DriverRegistration(
                manifest=manifest, package_root=child, factory=loaded.factory, tree_sha256=tree_digest(child),
                vendor_metadata=vendor_metadata, source="official" if manifest.publisher == "datawatch" else "installed",
                protected=False, aliases=aliases,
                build_read_plan=loaded.build_read_plan, decode_read_results=loaded.decode_read_results,
            )
            for alias in aliases:
                registrations[alias] = registration

        registry = cls(registrations, failures)
        # 进程内每次成功 scan 视为一代；空表也递增以便 Agent 感知重启
        registry.generation = 1
        return registry

    def list_loaded(self) -> list[dict[str, Any]]:
        """Return one management record per driver, deduplicating protocol aliases."""
        items: list[dict[str, Any]] = []
        seen: Set[str] = set()
        for registration in self._registrations.values():
            manifest = registration.manifest
            if manifest.id in seen:
                continue
            seen.add(manifest.id)
            vendor = registration.vendor_metadata
            aliases = registration.aliases or (manifest.protocol,)
            items.append(
                {
                    "id": manifest.id,
                    "protocol": manifest.protocol,
                    "aliases": list(aliases),
                    "name": manifest.name,
                    "description": manifest.description,
                    "version": manifest.version,
                    "sha256": vendor.sha256 if vendor is not None else None,
                    "capabilities": list(manifest.capabilities),
                    "readWriteType": manifest.read_write,
                    "source": registration.source,
                    "protected": registration.protected,
                    "generation": self.generation,
                    "tree_sha256": registration.tree_sha256,
                }
            )
        items.sort(key=lambda item: item["id"])
        return items

    def list_status(self) -> list[dict[str, Any]]:
        items = [dict(item, status="loaded", error=None) for item in self.list_loaded()]
        for failure in self._failures.values():
            items.append(
                {
                    "id": failure.driver_id,
                    "protocol": failure.protocol,
                    "version": None,
                    "capabilities": [],
                    "readWriteType": "只读",
                    "source": "installed",
                    "protected": False,
                    "sha256": None,
                    "generation": self.generation,
                    "status": "load_failed",
                    "stage": failure.stage,
                    "error": failure.error,
                }
            )
        items.sort(key=lambda item: item["id"])
        return items

    def has(self, protocol: str) -> bool:
        return protocol.lower() in self._registrations

    def has_failed(self, protocol: str) -> bool:
        return protocol.lower() in self._failures

    def failure_reason(self, protocol: str) -> Optional[str]:
        failure = self._failures.get(protocol.lower())
        return failure.error if failure is not None else None

    def protocols(self) -> tuple[str, ...]:
        return tuple(sorted(self._registrations))

    def get(self, protocol: str) -> Optional[DriverRegistration]:
        return self._registrations.get(protocol.lower())

    def create(
        self,
        protocol: str,
        *,
        read_executor: Any = None,
        data_root: Optional[Path] = None,
        metrics: Any = None,
    ) -> BaseDriver:
        """Resolve bundled/installed protocol aliases through one factory contract."""
        key = protocol.lower()
        registration = self._registrations.get(key)
        if registration is None:
            raise DriverRegistryError(f"未注册的协议: {protocol}")

        manifest = registration.manifest
        context = DriverContext(
            protocol=manifest.protocol,
            driver_id=manifest.id,
            driver_version=manifest.version,
            driver_api=manifest.driver_api,
            logger=get_logger(f"driver.{manifest.id}"),
            read_executor=read_executor,
            data_root=data_root,
            metrics=metrics,
        )
        driver = registration.factory(context)
        if not isinstance(driver, BaseDriver):
            raise DriverRegistryError(
                f"工厂 {manifest.entry} 必须返回 BaseDriver，实际为 {type(driver)!r}"
            )
        driver.native_quality = "native_quality" in manifest.capabilities
        bound_write = getattr(driver, "write", None)
        if getattr(bound_write, "__func__", None) is not BaseDriver.write:
            raise DriverRegistryError(
                f"工厂 {manifest.entry} 返回的驱动覆盖了 write；"
                "collector 生产驱动必须继承 BaseDriver 只读拒写实现"
            )
        driver.read_only = True
        return driver


def _ensure_namespace_package(name: str) -> types.ModuleType:
    """确保命名空间包存在于 sys.modules。"""
    existing = sys.modules.get(name)
    if existing is not None:
        return existing
    module = types.ModuleType(name)
    module.__path__ = []  # type: ignore[attr-defined]
    module.__package__ = name
    sys.modules[name] = module
    return module


def _register_driver_package(ext_pkg_name: str, code_root: Path) -> List[str]:
    """
    将驱动代码目录注册为 datawatch_ext.<driver_id> 包，并执行 __init__.py（若存在）。

    说明：合成包隔离的是驱动自身模块名；pymodbus 等第三方依赖仍会以顶层名
    进入全局 sys.modules（一进程单版本限制，见决策 C）。
    """
    added: List[str] = []
    if ext_pkg_name in sys.modules:
        return added

    search_paths = [str(code_root.resolve())]
    init_file = code_root / "__init__.py"
    if init_file.is_file():
        spec = importlib.util.spec_from_file_location(
            ext_pkg_name,
            init_file,
            submodule_search_locations=search_paths,
        )
        if spec is None or spec.loader is None:
            raise DriverRegistryError(f"无法为驱动包创建加载器: {init_file}")
        pkg = importlib.util.module_from_spec(spec)
        sys.modules[ext_pkg_name] = pkg
        added.append(ext_pkg_name)
        spec.loader.exec_module(pkg)
        return added

    pkg = types.ModuleType(ext_pkg_name)
    pkg.__path__ = search_paths  # type: ignore[attr-defined]
    pkg.__file__ = None
    pkg.__package__ = ext_pkg_name
    sys.modules[ext_pkg_name] = pkg
    added.append(ext_pkg_name)
    return added


def _calculate_vendor_sha256(vendor_dir: Path) -> str:
    """计算 vendor 源文件树哈希；忽略 Python 运行时生成的字节码缓存。"""
    digest = hashlib.sha256()
    files = sorted(
        (
            path
            for path in vendor_dir.rglob("*")
            if path.is_file()
            and "__pycache__" not in path.parts
            and path.suffix != ".pyc"
        ),
        key=lambda path: path.relative_to(vendor_dir).as_posix(),
    )
    for path in files:
        digest.update(path.relative_to(vendor_dir).as_posix().encode("utf-8"))
        digest.update(b"\0")
        with path.open("rb") as file:
            for chunk in iter(lambda: file.read(1024 * 1024), b""):
                digest.update(chunk)
    return digest.hexdigest()


def _peek_vendor_declaration(package_root: Path) -> Optional[tuple[str, str, str]]:
    """只读 vendor_meta.toml 取 (name, version, sha256)，不做哈希与完整性校验。

    预扫描阶段用于建立跨驱动的依赖所有权视图，故刻意不重复 _load_vendor_metadata
    的目录哈希开销；格式非法一律返回 None，留给后续真正加载时报出准确错误。
    """
    metadata_path = package_root / "vendor_meta.toml"
    if not metadata_path.is_file():
        return None
    try:
        data = tomllib.loads(metadata_path.read_text(encoding="utf-8"))
    except (OSError, tomllib.TOMLDecodeError):
        return None
    name = data.get("name")
    version = data.get("version")
    sha256 = data.get("sha256")
    if not all(isinstance(item, str) and item.strip() for item in (name, version, sha256)):
        return None
    return (name, version, sha256.lower())


def _vendor_modules(package_root: Path) -> dict[str, Path]:
    vendor = package_root / "vendor"
    if not vendor.is_dir():
        return {}
    result = {}
    for path in vendor.iterdir():
        if path.is_dir() and path.name.isidentifier() and (path / "__init__.py").is_file():
            result[path.name] = path
        elif path.suffix in {".py", ".pyd"}:
            name = path.name.split(".")[0]
            if name.isidentifier():
                result[name] = path
    return result


def _module_digest(path: Path) -> str:
    return tree_digest(path) if path.is_dir() else file_digest(path)


def _verify_loaded_vendor_modules(package_root: Path) -> None:
    for name, expected in _vendor_modules(package_root).items():
        module = sys.modules.get(name)
        if module is None:
            continue
        actual_file = getattr(module, "__file__", None)
        if not actual_file:
            raise DriverRegistryError(f"无法核实已加载依赖来源: {name}")
        actual = Path(actual_file)
        if expected.is_dir():
            actual = actual.parent
        if actual.resolve() != expected.resolve() and (not actual.exists() or _module_digest(actual) != _module_digest(expected)):
            raise DriverRegistryError(f"依赖 {name!r} 已从不同内容的来源加载；拒绝替换全局模块")


def _resolve_vendor_ownership(
    candidates: List[tuple[Path, "DriverManifest", str]],
) -> Dict[str, str]:
    """按扫描顺序确定 vendor 顶层包名归属，返回 {protocol: 拒绝原因}。

    Python 的 sys.modules 是全局单例，同一顶层包名无法多版本共存。首个声明者取得
    所有权；后续驱动声明同名依赖时，仅当 (version, sha256) 完全一致才视为同一份
    依赖而放行，否则拒绝并在错误中点明所有者，避免把冲突报成"路径不对"。

    先声明者胜（而非双方皆拒）：拒绝双方会让新装的冲突驱动连带打挂已在运行的驱动。
    """
    owners: Dict[str, tuple[str, str, str]] = {}  # 顶层包名 → (version, sha256, driver_id)
    rejections: Dict[str, str] = {}
    for package_root, manifest, protocol in candidates:
        declaration = _peek_vendor_declaration(package_root)
        if declaration is None:
            continue
        name, version, sha256 = declaration
        owner = owners.get(name)
        if owner is None:
            owners[name] = (version, sha256, manifest.id)
            continue
        owner_version, owner_sha256, owner_id = owner
        if (version, sha256) == (owner_version, owner_sha256):
            # 同名同版本同内容：同一份依赖，共存无歧义
            continue
        rejections[protocol] = (
            f"vendor 依赖 {name!r} 已被驱动 {owner_id!r} 以 "
            f"version={owner_version!r} sha256={owner_sha256[:12]}… 占用；"
            f"本包声明 version={version!r} sha256={sha256[:12]}…，"
            f"同名依赖无法多版本共存"
        )
    # Secondary dependencies (cryptography/cffi/etc.) also share sys.modules.
    modules: dict[str, tuple[str, str]] = {}
    for package_root, manifest, protocol in candidates:
        if protocol in rejections:
            continue
        pending = {}
        for name, path in _vendor_modules(package_root).items():
            digest = _module_digest(path)
            owner = modules.get(name)
            if owner and owner[0] != digest:
                rejections[protocol] = f"vendor 依赖 {name!r} 与驱动 {owner[1]!r} 内容冲突"
                break
            pending[name] = (digest, manifest.id)
        if protocol not in rejections:
            modules.update(pending)
    return rejections


def _load_vendor_metadata(package_root: Path) -> Optional[VendorMetadata]:
    """读取并校验驱动 vendor 依赖锁定元数据。"""
    vendor_dir = package_root / "vendor"
    metadata_path = package_root / "vendor_meta.toml"
    if not vendor_dir.is_dir() and not metadata_path.is_file():
        return None
    if not vendor_dir.is_dir():
        raise DriverRegistryError(f"vendor 目录不存在: {vendor_dir}")
    if not metadata_path.is_file():
        raise DriverRegistryError(f"vendor 元数据不存在: {metadata_path}")

    try:
        data = tomllib.loads(metadata_path.read_text(encoding="utf-8"))
        fields = ("name", "version", "license", "sha256")
        if any(
            not isinstance(data.get(field), str) or not data[field].strip()
            for field in fields
        ):
            raise TypeError("vendor 元数据字段必须为非空字符串")
        metadata = VendorMetadata(
            name=data["name"],
            version=data["version"],
            license=data["license"],
            sha256=data["sha256"].lower(),
        )
    except (KeyError, TypeError, tomllib.TOMLDecodeError) as exc:
        raise DriverRegistryError(f"vendor 元数据格式非法: {metadata_path}") from exc

    if len(metadata.sha256) != 64 or any(
        char not in "0123456789abcdef" for char in metadata.sha256
    ):
        raise DriverRegistryError(f"vendor 元数据 sha256 非法: {metadata_path}")
    actual_sha256 = _calculate_vendor_sha256(vendor_dir)
    if actual_sha256 != metadata.sha256:
        raise DriverRegistryError(
            f"vendor 完整性校验失败: {vendor_dir} "
            f"(expected={metadata.sha256}, actual={actual_sha256})"
        )
    return metadata


def _cleanup_ext_modules(ext_pkg_name: str, tracked_keys: Set[str]) -> None:
    """加载失败时清理本包写入的 sys.modules 键（不触碰第三方顶层依赖）。"""
    for key in list(tracked_keys):
        if key == ext_pkg_name or key.startswith(ext_pkg_name + "."):
            sys.modules.pop(key, None)
    for key in list(sys.modules):
        if key == ext_pkg_name or key.startswith(ext_pkg_name + "."):
            sys.modules.pop(key, None)


def _resolve_ext_import_name(driver_id: str, module_path: str) -> str:
    """将 entry 模块路径映射到 datawatch_ext.<driver_id>.*。"""
    parts = module_path.split(".")
    if parts[0] != driver_id:
        raise DriverRegistryError(
            f"entry 顶层包名必须为驱动 id {driver_id!r}，当前为 {module_path!r}"
        )
    suffix = ".".join(parts[1:])
    if suffix:
        return f"{_EXT_NAMESPACE}.{driver_id}.{suffix}"
    return f"{_EXT_NAMESPACE}.{driver_id}"


def _module_version(module: types.ModuleType) -> Optional[str]:
    version = getattr(module, "__version__", None)
    if isinstance(version, str) and version.strip():
        return version.strip()
    return None


def _verify_vendor_dependency(
    vendor_metadata: VendorMetadata, package_root: Path, *, stage: str
) -> None:
    """校验已加载依赖的 __file__/__version__ 与 vendor 锁定一致。"""
    vendor_dir = (package_root / "vendor").resolve()
    module = sys.modules.get(vendor_metadata.name)
    if module is None:
        if stage == "after":
            raise DriverRegistryError(
                f"驱动加载后未找到依赖模块 {vendor_metadata.name!r}"
            )
        return

    mod_file = getattr(module, "__file__", None)
    version = _module_version(module)
    vendor_resolved = vendor_dir.resolve()
    from_vendor = False
    if mod_file:
        try:
            from_vendor = Path(mod_file).resolve().is_relative_to(vendor_resolved)
        except (OSError, ValueError):
            from_vendor = False

    if not from_vendor and mod_file:
        candidate_package = Path(mod_file).resolve().parent.parent.parent
        if (candidate_package / "vendor_meta.toml").is_file():
            existing = _load_vendor_metadata(candidate_package)
            if existing == vendor_metadata:
                _VENDOR_OWNERS[vendor_metadata.name] = (existing.version, existing.sha256)

    if stage == "before":
        if version is not None and version != vendor_metadata.version:
            raise DriverRegistryError(
                f"已加载的 {vendor_metadata.name!r} 版本为 {version!r}，"
                f"与 vendor_meta 锁定 {vendor_metadata.version!r} 不一致"
            )
        if not from_vendor and _VENDOR_OWNERS.get(vendor_metadata.name) != (vendor_metadata.version, vendor_metadata.sha256):
            raise DriverRegistryError(f"依赖 {vendor_metadata.name!r} 已由其他来源加载，拒绝覆盖全局模块")
        return

    if not from_vendor and _VENDOR_OWNERS.get(vendor_metadata.name) != (vendor_metadata.version, vendor_metadata.sha256):
        raise DriverRegistryError(
            f"依赖 {vendor_metadata.name!r} 已从非 vendor 路径加载: {mod_file!r}；"
            f"期望位于 {vendor_resolved}"
        )
    if version != vendor_metadata.version:
        raise DriverRegistryError(
            f"依赖 {vendor_metadata.name!r} 版本为 {version!r}，"
            f"与 vendor_meta 锁定 {vendor_metadata.version!r} 不一致"
        )


@dataclass(frozen=True)
class _LoadedDriverPackage:
    factory: Callable[[DriverContext], BaseDriver]
    build_read_plan: Optional[Callable[..., Any]]
    decode_read_results: Optional[Callable[..., Any]]


def _load_planner_hooks(
    driver_id: str,
) -> tuple[Optional[Callable[..., Any]], Optional[Callable[..., Any]]]:
    """约定从 datawatch_ext.<id>.planner 加载通用 plan/decode 回调。

    仅当 planner 模块本身不存在时降级为 (None, None)；
    planner 存在但其内部依赖缺失时向上抛出，由上层记为 load_failed。
    """
    planner_name = f"{_EXT_NAMESPACE}.{driver_id}.planner"
    package_name = f"{_EXT_NAMESPACE}.{driver_id}"
    try:
        planner = importlib.import_module(planner_name)
    except ModuleNotFoundError as exc:
        missing = getattr(exc, "name", None)
        if missing in {planner_name, package_name}:
            return None, None
        raise
    build = getattr(planner, "build_read_plan", None)
    decode = getattr(planner, "decode_read_results", None)
    return (
        build if callable(build) else None,
        decode if callable(decode) else None,
    )


def _load_driver_package(package_root, manifest, vendor_metadata):
    with _LOAD_LOCK:
        loaded = _load_driver_package_unlocked(package_root, manifest, vendor_metadata)
        if vendor_metadata is not None:
            _VENDOR_OWNERS[vendor_metadata.name] = (vendor_metadata.version, vendor_metadata.sha256)
        return loaded


def _load_driver_package_unlocked(
    package_root: Path,
    manifest: DriverManifest,
    vendor_metadata: Optional[VendorMetadata],
) -> _LoadedDriverPackage:
    """经 datawatch_ext 合成包动态导入工厂与 planner 钩子；失败清理本包 sys.modules。"""
    module_path, _, func_name = manifest.entry.partition(":")
    if not module_path or not func_name:
        raise DriverManifestError(f"entry 格式非法: {manifest.entry}")

    driver_id = manifest.id
    code_root = package_root / driver_id
    if not code_root.is_dir():
        raise DriverRegistryError(
            f"驱动代码目录不存在: {code_root} ({manifest.source_path})"
        )

    _ensure_namespace_package(_EXT_NAMESPACE)
    ext_pkg_name = f"{_EXT_NAMESPACE}.{driver_id}"
    tracked: Set[str] = set()
    before_keys = set(sys.modules)
    vendor_path: Optional[str] = None

    try:
        if vendor_metadata is not None:
            _verify_vendor_dependency(vendor_metadata, package_root, stage="before")

        _verify_loaded_vendor_modules(package_root)
        vendor_dir = package_root / "vendor"
        if vendor_dir.is_dir():
            vendor_path = str(vendor_dir.resolve())
            # 保留 vendor 搜索路径以支持延迟导入；依赖进入全局 sys.modules，
            # 因此同一进程只能使用一个 pymodbus 版本。
            sys.path.insert(0, vendor_path)
        # A rescan must execute this driver's modules again. Replacing only the
        # parent package leaves stale submodules cached and can hide vendor imports.
        _cleanup_ext_modules(ext_pkg_name, set())
        tracked.update(_register_driver_package(ext_pkg_name, code_root))
        import_name = _resolve_ext_import_name(driver_id, module_path)
        module = importlib.import_module(import_name)
        tracked.update(set(sys.modules) - before_keys)

        if vendor_metadata is not None:
            _verify_vendor_dependency(vendor_metadata, package_root, stage="after")

        for requirement in manifest.dependencies:
            name, separator, expected = requirement.partition("==")
            if not separator or not name.isidentifier() or not expected:
                raise DriverRegistryError(f"依赖必须声明 module==version: {requirement}")
            dependency = importlib.import_module(name)
            if getattr(dependency, "__version__", None) != expected:
                raise DriverRegistryError(f"依赖版本不匹配: {requirement}")

        factory = getattr(module, func_name, None)
        if factory is None or not callable(factory):
            raise DriverRegistryError(
                f"驱动入口 {manifest.entry!r} 不存在或不可调用 ({manifest.source_path})"
            )

        try:
            signature = inspect.signature(factory)
        except (TypeError, ValueError) as exc:
            raise DriverRegistryError(
                f"无法解析工厂签名 {manifest.entry!r}: {exc}"
            ) from exc

        params = [
            p
            for p in signature.parameters.values()
            if p.kind
            in (
                inspect.Parameter.POSITIONAL_ONLY,
                inspect.Parameter.POSITIONAL_OR_KEYWORD,
                inspect.Parameter.KEYWORD_ONLY,
            )
        ]
        if len(params) != 1 or params[0].name not in {"context", "ctx"}:
            raise DriverRegistryError(
                f"工厂签名必须为 create_driver(context: DriverContext)，"
                f"当前为 {manifest.entry}{signature}"
            )

        build_read_plan, decode_read_results = _load_planner_hooks(driver_id)
        return _LoadedDriverPackage(
            factory=factory,  # type: ignore[arg-type]
            build_read_plan=build_read_plan,
            decode_read_results=decode_read_results,
        )
    except Exception:
        _cleanup_ext_modules(ext_pkg_name, tracked | (set(sys.modules) - before_keys))
        raise
    finally:
        pass  # dependencies may use lazy imports throughout the package lifetime


def default_drivers_root() -> Path:
    """从 DATAWATCH_DATA_ROOT 推导外置驱动目录。"""
    data_root = os.environ.get("DATAWATCH_DATA_ROOT")
    if data_root:
        return Path(data_root) / "drivers"
    return Path(r"C:\ProgramData\DataWatch") / "drivers"


def default_data_root() -> Path:
    """DATAWATCH_DATA_ROOT 或 ProgramData 默认根。"""
    data_root = os.environ.get("DATAWATCH_DATA_ROOT")
    if data_root:
        return Path(data_root)
    return Path(r"C:\ProgramData\DataWatch")
