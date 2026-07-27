"""外置驱动注册表测试"""

import sys
import textwrap
from pathlib import Path
from unittest.mock import MagicMock

import pytest

from drivers.base import BaseDriver
from drivers.registry import (
    BUILTIN_PROTOCOLS,
    DriverRegistry,
    DriverRegistryError,
    _load_vendor_metadata,
)


_MODBUS_PACKAGE_ROOT = (
    Path(__file__).resolve().parents[2] / "packages" / "modbus_tcp"
)


def _write_driver_package(tmp_path: Path, driver_id: str, protocol: str) -> Path:
    """在临时目录创建最小外置驱动包。"""
    from drivers.manifest import current_python_abi

    root = tmp_path / "drivers" / driver_id
    root.mkdir(parents=True)
    pkg = root / driver_id
    pkg.mkdir()
    (pkg / "__init__.py").write_text("", encoding="utf-8")
    (pkg / "driver.py").write_text(
        textwrap.dedent(
            f"""
            from drivers.base import BaseDriver

            class FakeDriver(BaseDriver):
                def __init__(self, context=None, **kwargs):
                    super().__init__()
                    self.context = context
                    self.kwargs = kwargs

                async def connect(self, config):
                    self._connected = True

                async def disconnect(self):
                    self._connected = False

                async def read(self, blocks):
                    return {{}}

                async def write(self, points):
                    pass

            def create_driver(context):
                return FakeDriver(context=context)
            """
        ).strip()
        + "\n",
        encoding="utf-8",
    )
    abi = current_python_abi()
    toml = textwrap.dedent(
        f"""
        manifest_version = 1
        driver_api = "1.0"
        id = "{driver_id}"
        protocol = "{protocol}"
        name = "Test Driver"
        description = "测试驱动"
        version = "1.0.0"
        entry = "{driver_id}.driver:create_driver"
        python_abi = "{abi}"
        os = ["windows"]
        architectures = ["x86_64"]
        capabilities = ["poll", "read"]
        read_write = "只读"
        """
    ).strip()
    (root / "driver.toml").write_text(toml + "\n", encoding="utf-8")
    return tmp_path / "drivers"


def test_scan_registers_external_driver(tmp_path):
    drivers_root = _write_driver_package(tmp_path, "demo_proto", "demo_proto")
    registry = DriverRegistry.scan(drivers_root)
    assert registry.has("demo_proto")
    driver = registry.create("demo_proto", read_executor=MagicMock())
    assert isinstance(driver, BaseDriver)


def test_scan_skips_staging_and_backup(tmp_path):
    drivers_root = _write_driver_package(tmp_path, "good_one", "good_one")
    (drivers_root / ".staging").mkdir()
    (drivers_root / ".staging" / "driver.toml").write_text("invalid", encoding="utf-8")
    (drivers_root / ".backup").mkdir()
    (drivers_root / ".backup" / "driver.toml").write_text("invalid", encoding="utf-8")
    registry = DriverRegistry.scan(drivers_root)
    assert registry.has("good_one")
    assert len(registry.protocols()) == 1


def test_builtin_protocol_cannot_be_overridden(tmp_path):
    """覆盖内置协议的坏包 load_failed，不得拖垮其它好包。"""
    drivers_root = _write_driver_package(tmp_path, "good_one", "good_one")
    _write_driver_package(tmp_path, "fake_s7", "s7")
    registry = DriverRegistry.scan(drivers_root)
    assert registry.has("good_one")
    assert not registry.has("s7")


def test_current_python_abi_matches_runtime():
    from drivers.manifest import current_python_abi
    import sys
    import sysconfig

    abi = current_python_abi()
    expected = f"cp{sys.version_info.major}{sys.version_info.minor}"
    if sysconfig.get_config_var("Py_GIL_DISABLED"):
        expected += "t"
    assert abi == expected


def test_preloaded_wrong_vendor_version_is_rejected(tmp_path, monkeypatch):
    """已存在于 sys.modules 且版本不匹配的依赖不得绕过 vendor 校验。"""
    import sys
    import types

    from drivers.manifest import current_python_abi
    from drivers import registry as reg_mod

    drivers_root = _write_driver_package(tmp_path, "dep_proto", "dep_proto")
    root = drivers_root / "dep_proto"
    vendor = root / "vendor" / "fakelib"
    vendor.mkdir(parents=True)
    (vendor / "__init__.py").write_text('__version__ = "1.0.0"\n', encoding="utf-8")
    # 伪造元数据（哈希按目录计算）
    from drivers.registry import _calculate_vendor_sha256

    sha = _calculate_vendor_sha256(root / "vendor")
    (root / "vendor_meta.toml").write_text(
        textwrap.dedent(
            f"""
            name = "fakelib"
            version = "1.0.0"
            license = "MIT"
            sha256 = "{sha}"
            """
        ).strip()
        + "\n",
        encoding="utf-8",
    )
    # 驱动依赖 fakelib
    (root / "dep_proto" / "driver.py").write_text(
        textwrap.dedent(
            """
            import fakelib
            from drivers.base import BaseDriver

            class FakeDriver(BaseDriver):
                async def connect(self, config):
                    self._connected = True
                async def disconnect(self):
                    self._connected = False
                async def read(self, blocks):
                    return {}
                async def write(self, points):
                    pass

            def create_driver(context):
                return FakeDriver()
            """
        ).strip()
        + "\n",
        encoding="utf-8",
    )

    fake = types.ModuleType("fakelib")
    fake.__version__ = "0.0.0"
    fake.__file__ = str(tmp_path / "elsewhere" / "fakelib" / "__init__.py")
    monkeypatch.setitem(sys.modules, "fakelib", fake)

    registry = DriverRegistry.scan(drivers_root)
    assert not registry.has("dep_proto")


def test_create_unknown_protocol_raises(tmp_path):
    registry = DriverRegistry.scan(_write_driver_package(tmp_path, "demo_proto", "demo_proto"))
    with pytest.raises(DriverRegistryError, match="未注册"):
        registry.create("missing")


def test_builtin_protocols_frozen():
    assert "s7" in BUILTIN_PROTOCOLS
    assert "opcda_center" in BUILTIN_PROTOCOLS


def test_load_uses_datawatch_ext_without_sys_path_insert(tmp_path):
    """外置驱动经 datawatch_ext 合成包加载，不得永久污染全局 sys.path。"""
    import sys

    drivers_root = _write_driver_package(tmp_path, "iso_a", "iso_a")
    package_root = str((drivers_root / "iso_a").resolve())
    before_path = list(sys.path)

    registry = DriverRegistry.scan(drivers_root)
    assert registry.has("iso_a")
    assert "datawatch_ext.iso_a" in sys.modules
    assert "datawatch_ext.iso_a.driver" in sys.modules
    # 包根不得残留在 sys.path 前部（亦不应新增该路径）
    assert package_root not in sys.path
    assert sys.path == before_path or package_root not in sys.path


def test_two_drivers_with_same_submodule_name_do_not_collide(tmp_path):
    """两包经 datawatch_ext 加载，模块键不得占用顶层包名。"""
    import sys

    from drivers.manifest import current_python_abi

    abi = current_python_abi()
    drivers_root = tmp_path / "drivers"
    for driver_id, marker in (("pkg_x", "X"), ("pkg_y", "Y")):
        root = drivers_root / driver_id
        pkg = root / driver_id
        pkg.mkdir(parents=True)
        (pkg / "__init__.py").write_text("", encoding="utf-8")
        (pkg / "shared.py").write_text(f'MARKER = "{marker}"\n', encoding="utf-8")
        (pkg / "driver.py").write_text(
            textwrap.dedent(
                f"""
                from drivers.base import BaseDriver
                from .shared import MARKER

                class FakeDriver(BaseDriver):
                    marker = MARKER
                    def __init__(self, context=None, **kwargs):
                        super().__init__()
                        self.context = context
                    async def connect(self, config):
                        self._connected = True
                    async def disconnect(self):
                        self._connected = False
                    async def read(self, blocks):
                        return {{}}
                    async def write(self, points):
                        pass

                def create_driver(context):
                    return FakeDriver(context=context)
                """
            ).strip()
            + "\n",
            encoding="utf-8",
        )
        (root / "driver.toml").write_text(
            textwrap.dedent(
                f"""
                manifest_version = 1
                driver_api = "1.0"
                id = "{driver_id}"
                protocol = "{driver_id}"
                name = "Test"
                description = "t"
                version = "1.0.0"
                entry = "{driver_id}.driver:create_driver"
                python_abi = "{abi}"
                os = ["windows"]
                architectures = ["x86_64"]
                capabilities = ["poll", "read"]
                read_write = "只读"
                """
            ).strip()
            + "\n",
            encoding="utf-8",
        )

    registry = DriverRegistry.scan(drivers_root)
    dx = registry.create("pkg_x")
    dy = registry.create("pkg_y")
    assert dx.marker == "X"
    assert dy.marker == "Y"
    assert "datawatch_ext.pkg_x.shared" in sys.modules
    assert "datawatch_ext.pkg_y.shared" in sys.modules
    # 不得以顶层名占用（避免与第三方同名包冲突）
    assert "pkg_x" not in sys.modules
    assert "pkg_y" not in sys.modules


def test_bad_package_does_not_block_good_package(tmp_path):
    """一坏一好时，好协议仍可扫描并 create。"""
    import sys

    drivers_root = _write_driver_package(tmp_path, "good_one", "good_one")
    bad = drivers_root / "bad_one"
    pkg = bad / "bad_one"
    pkg.mkdir(parents=True)
    (pkg / "__init__.py").write_text("", encoding="utf-8")
    (pkg / "driver.py").write_text(
        "raise RuntimeError('intentional import failure')\n",
        encoding="utf-8",
    )
    (bad / "driver.toml").write_text(
        textwrap.dedent(
            """
            manifest_version = 1
            driver_api = "1.0"
            id = "bad_one"
            protocol = "bad_one"
            name = "Bad"
            description = "broken entry"
            version = "1.0.0"
            entry = "bad_one.driver:create_driver"
            python_abi = "cp314t"
            os = ["windows"]
            architectures = ["x86_64"]
            capabilities = ["poll", "read"]
            read_write = "只读"
            """
        ).strip()
        + "\n",
        encoding="utf-8",
    )

    registry = DriverRegistry.scan(drivers_root)
    assert registry.has("good_one")
    assert not registry.has("bad_one")
    assert registry.has_failed("bad_one")
    assert "intentional import failure" in (registry.failure_reason("bad_one") or "")
    assert isinstance(registry.create("good_one"), BaseDriver)
    # 失败加载不得残留本包合成模块
    assert "datawatch_ext.bad_one" not in sys.modules
    assert "datawatch_ext.bad_one.driver" not in sys.modules


def test_device_manager_uses_registry_for_external_protocol(tmp_path):
    """DeviceManager 经注册表创建外置驱动，内置协议仍走内置分支。"""
    from core.config import CollectorConfig
    from device.manager import DeviceManager

    drivers_root = _write_driver_package(tmp_path, "demo_proto", "demo_proto")
    registry = DriverRegistry.scan(drivers_root)
    manager = DeviceManager(
        CollectorConfig(devices=[], variables=[]),
        driver_registry=registry,
    )
    driver = manager._create_driver("demo_proto")
    assert isinstance(driver, BaseDriver)
    # 内置 s7 不受注册表影响
    s7 = manager._create_driver("s7")
    assert type(s7).__name__ == "S7Driver"


def test_modbus_tcp_vendor_metadata_is_exposed():
    """真实驱动的 vendor 元数据必须被注册表读取并保留。"""
    registry = DriverRegistry.scan(_MODBUS_PACKAGE_ROOT.parent)
    registration = registry.get("modbus_tcp")

    assert registration is not None
    assert registration.vendor_metadata is not None
    assert registration.vendor_metadata.name == "pymodbus"
    assert registration.vendor_metadata.version == "3.14.0"
    assert registration.vendor_metadata.license
    assert len(registration.vendor_metadata.sha256) == 64


def test_modbus_tcp_rescan_reexecutes_driver_after_vendor_cache_is_cleared():
    first = DriverRegistry.scan(_MODBUS_PACKAGE_ROOT.parent)
    assert first.has("modbus_tcp")

    for key in list(sys.modules):
        if key == "pymodbus" or key.startswith("pymodbus."):
            sys.modules.pop(key, None)

    second = DriverRegistry.scan(_MODBUS_PACKAGE_ROOT.parent)

    assert second.has("modbus_tcp")
    assert "pymodbus" in sys.modules


def test_modbus_tcp_loads_pymodbus_from_vendor_without_site_packages(monkeypatch):
    """导入期间只临时暴露 vendor，顶层 pymodbus 必须由 vendor 提供。"""
    pymodbus_modules = {
        key: module
        for key, module in sys.modules.items()
        if key == "pymodbus" or key.startswith("pymodbus.")
    }
    ext_modules = {
        key: module
        for key, module in sys.modules.items()
        if key == "datawatch_ext.modbus_tcp"
        or key.startswith("datawatch_ext.modbus_tcp.")
    }
    original_path = list(sys.path)
    vendor_path = str((_MODBUS_PACKAGE_ROOT / "vendor").resolve())

    for key in pymodbus_modules:
        sys.modules.pop(key, None)
    for key in ext_modules:
        sys.modules.pop(key, None)
    monkeypatch.setattr(
        sys,
        "path",
        [path for path in sys.path if "site-packages" not in path.lower()],
    )

    try:
        registry = DriverRegistry.scan(_MODBUS_PACKAGE_ROOT.parent)
        driver = registry.create("modbus_tcp")

        assert isinstance(driver, BaseDriver)
        assert Path(sys.modules["pymodbus"].__file__).resolve().is_relative_to(
            _MODBUS_PACKAGE_ROOT / "vendor"
        )
        assert vendor_path not in sys.path
    finally:
        for key in list(sys.modules):
            if key == "pymodbus" or key.startswith("pymodbus."):
                sys.modules.pop(key, None)
            if key == "datawatch_ext.modbus_tcp" or key.startswith(
                "datawatch_ext.modbus_tcp."
            ):
                sys.modules.pop(key, None)
        sys.modules.update(pymodbus_modules)
        sys.modules.update(ext_modules)
        monkeypatch.setattr(sys, "path", original_path)


def test_scan_rejects_vendor_with_mismatched_hash(tmp_path):
    """vendor 完整性哈希不匹配时，驱动不得注册。"""
    drivers_root = _write_driver_package(tmp_path, "vendor_guard", "vendor_guard")
    package_root = drivers_root / "vendor_guard"
    vendor_dir = package_root / "vendor"
    vendor_dir.mkdir()
    (vendor_dir / "dependency.py").write_text("VALUE = 1\n", encoding="utf-8")
    (package_root / "vendor_meta.toml").write_text(
        textwrap.dedent(
            """
            name = "test-dependency"
            version = "1.0.0"
            license = "MIT"
            sha256 = "0000000000000000000000000000000000000000000000000000000000000000"
            """
        ).strip()
        + "\n",
        encoding="utf-8",
    )

    with pytest.raises(DriverRegistryError, match="完整性校验失败"):
        _load_vendor_metadata(package_root)

    registry = DriverRegistry.scan(drivers_root)
    assert not registry.has("vendor_guard")


def test_vendor_evil_sibling_prefix_is_rejected(tmp_path, monkeypatch):
    """vendor_evil 不得因 startswith(vendor) 被误判为合法 vendor 子路径。"""
    import types

    from drivers.registry import VendorMetadata, _verify_vendor_dependency

    package_root = tmp_path / "drv"
    vendor = package_root / "vendor"
    vendor.mkdir(parents=True)
    evil_init = package_root / "vendor_evil" / "pymodbus" / "__init__.py"
    evil_init.parent.mkdir(parents=True)
    evil_init.write_text("__version__ = '3.14.0'\n", encoding="utf-8")

    mod = types.ModuleType("pymodbus")
    mod.__file__ = str(evil_init.resolve())
    mod.__version__ = "3.14.0"
    monkeypatch.setitem(sys.modules, "pymodbus", mod)

    meta = VendorMetadata(
        name="pymodbus",
        version="3.14.0",
        license="BSD",
        sha256="0" * 64,
    )
    with pytest.raises(DriverRegistryError, match="非 vendor"):
        _verify_vendor_dependency(meta, package_root, stage="after")


def test_planner_missing_internal_dependency_fails_registration(tmp_path):
    """planner 存在但内部依赖缺失 → 协议不得静默降级注册。"""
    from drivers.manifest import current_python_abi

    driver_id = "need_dep"
    root = tmp_path / "drivers" / driver_id
    pkg = root / driver_id
    pkg.mkdir(parents=True)
    (pkg / "__init__.py").write_text("", encoding="utf-8")
    (pkg / "driver.py").write_text(
        textwrap.dedent(
            """
            from drivers.base import BaseDriver

            class FakeDriver(BaseDriver):
                def __init__(self, context=None, **kwargs):
                    super().__init__()
                    self.context = context

                async def connect(self, config):
                    self._connected = True

                async def disconnect(self):
                    self._connected = False

                async def read(self, blocks):
                    return {}

                async def write(self, points):
                    pass

            def create_driver(context):
                return FakeDriver(context=context)
            """
        ).strip()
        + "\n",
        encoding="utf-8",
    )
    (pkg / "planner.py").write_text(
        "import totally_missing_dep_xyz  # noqa: F401\n",
        encoding="utf-8",
    )
    abi = current_python_abi()
    (root / "driver.toml").write_text(
        textwrap.dedent(
            f"""
            manifest_version = 1
            driver_api = "1.0"
            id = "{driver_id}"
            protocol = "{driver_id}"
            name = "NeedDep"
            description = "planner 缺依赖"
            version = "1.0.0"
            entry = "{driver_id}.driver:create_driver"
            python_abi = "{abi}"
            os = ["windows"]
            architectures = ["x86_64"]
            capabilities = ["poll", "read", "batch_contiguous"]
            read_write = "只读"
            """
        ).strip()
        + "\n",
        encoding="utf-8",
    )

    registry = DriverRegistry.scan(tmp_path / "drivers")
    assert not registry.has(driver_id)


def test_batch_contiguous_without_planner_fails_registration(tmp_path):
    """声明 batch_contiguous 却无 planner → 不得注册。"""
    drivers_root = _write_driver_package(tmp_path, "no_planner", "no_planner")
    # 覆盖 capabilities 声明批读
    from drivers.manifest import current_python_abi

    abi = current_python_abi()
    root = drivers_root / "no_planner"
    (root / "driver.toml").write_text(
        textwrap.dedent(
            f"""
            manifest_version = 1
            driver_api = "1.0"
            id = "no_planner"
            protocol = "no_planner"
            name = "NoPlanner"
            description = "t"
            version = "1.0.0"
            entry = "no_planner.driver:create_driver"
            python_abi = "{abi}"
            os = ["windows"]
            architectures = ["x86_64"]
            capabilities = ["poll", "read", "batch_contiguous"]
            read_write = "只读"
            """
        ).strip()
        + "\n",
        encoding="utf-8",
    )
    registry = DriverRegistry.scan(drivers_root)
    assert not registry.has("no_planner")


def _write_batch_driver_with_partial_planner(
    tmp_path: Path, driver_id: str, *, with_plan: bool, with_decode: bool
) -> Path:
    """声明 batch_contiguous，并按需只写其中一个 planner hook。"""
    from drivers.manifest import current_python_abi

    drivers_root = _write_driver_package(tmp_path, driver_id, driver_id)
    root = drivers_root / driver_id
    abi = current_python_abi()
    (root / "driver.toml").write_text(
        textwrap.dedent(
            f"""
            manifest_version = 1
            driver_api = "1.0"
            id = "{driver_id}"
            protocol = "{driver_id}"
            name = "Partial"
            description = "partial hooks"
            version = "1.0.0"
            entry = "{driver_id}.driver:create_driver"
            python_abi = "{abi}"
            os = ["windows"]
            architectures = ["x86_64"]
            capabilities = ["poll", "read", "batch_contiguous"]
            read_write = "只读"
            """
        ).strip()
        + "\n",
        encoding="utf-8",
    )
    lines = []
    if with_plan:
        lines.append("def build_read_plan(variables, unit_id=1):\n    return [], {}\n")
    if with_decode:
        lines.append("def decode_read_results(result, result_map):\n    return {}\n")
    (root / driver_id / "planner.py").write_text(
        "\n".join(lines) + ("\n" if lines else ""),
        encoding="utf-8",
    )
    return drivers_root


def test_batch_contiguous_plan_only_fails_registration(tmp_path):
    """有 build_read_plan 无 decode → 不得注册。"""
    drivers_root = _write_batch_driver_with_partial_planner(
        tmp_path, "plan_only", with_plan=True, with_decode=False
    )
    registry = DriverRegistry.scan(drivers_root)
    assert not registry.has("plan_only")


def test_batch_contiguous_decode_only_fails_registration(tmp_path):
    """有 decode 无 build_read_plan → 不得注册。"""
    drivers_root = _write_batch_driver_with_partial_planner(
        tmp_path, "decode_only", with_plan=False, with_decode=True
    )
    registry = DriverRegistry.scan(drivers_root)
    assert not registry.has("decode_only")
