"""驱动 ZIP 静态校验与安装/回滚事务测试。"""

from __future__ import annotations

import asyncio
import io
import json
import uuid
import zipfile
from pathlib import Path

import pytest

from app.services.driver_installer import (
    DriverInstallError,
    DriverInstaller,
    InconsistentStateError,
)
from app.services.driver_zip import (
    DriverZipError,
    _load_and_validate_manifest,
    compute_sha256,
    validate_and_extract,
)


def _minimal_driver_toml(
    *,
    driver_id: str = "demo_drv",
    version: str = "1.0.0",
    protocol: str | None = None,
    abi: str = "cp314t",
    entry: str | None = None,
    description: str = "测试驱动",
) -> str:
    proto = protocol or driver_id
    ent = entry or f"{driver_id}.driver:create_driver"
    return f"""manifest_version = 1
driver_api = "1.0"
id = "{driver_id}"
protocol = "{proto}"
name = "Demo Driver"
description = "{description}"
version = "{version}"
entry = "{ent}"
python_abi = "{abi}"
os = ["windows"]
architectures = ["x86_64"]
capabilities = ["poll", "read"]
read_write = "只读"
"""


def _build_zip(
    tmp_path: Path,
    *,
    driver_id: str = "demo_drv",
    version: str = "1.0.0",
    protocol: str | None = None,
    abi: str = "cp314t",
    extra_member: str | None = None,
    member_name_override: str | None = None,
) -> bytes:
    """构造合法（或可污染）的驱动 ZIP 字节。"""
    root = tmp_path / f"pkg_{driver_id}_{version}"
    pkg = root / driver_id
    mod = pkg / driver_id
    mod.mkdir(parents=True)
    (pkg / "driver.toml").write_text(
        _minimal_driver_toml(
            driver_id=driver_id,
            version=version,
            protocol=protocol,
            abi=abi,
        ),
        encoding="utf-8",
    )
    (mod / "__init__.py").write_text('"""demo"""\n', encoding="utf-8")
    (mod / "driver.py").write_text(
        "def create_driver(context):\n    return None\n",
        encoding="utf-8",
    )

    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", compression=zipfile.ZIP_DEFLATED) as zf:
        for path in pkg.rglob("*"):
            if path.is_file():
                arc = path.relative_to(root).as_posix()
                zf.write(path, arcname=arc)
        if extra_member:
            name = member_name_override or extra_member
            zf.writestr(name, b"evil")
    return buf.getvalue()


# ── ZIP 校验 ──────────────────────────────────────────────────────────────────


def test_validate_rejects_path_traversal(tmp_path: Path):
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr("../evil.toml", b"x")
        zf.writestr("demo_drv/driver.toml", _minimal_driver_toml().encode("utf-8"))
    with pytest.raises(DriverZipError, match="路径穿越"):
        validate_and_extract(buf.getvalue(), tmp_path / "staging")


def test_validate_rejects_absolute_and_drive_path(tmp_path: Path):
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr("C:/Windows/evil.txt", b"x")
    with pytest.raises(DriverZipError, match="盘符"):
        validate_and_extract(buf.getvalue(), tmp_path / "staging")


def test_validate_rejects_manifest_values_exceeding_database_columns(tmp_path: Path):
    with pytest.raises(DriverZipError, match="protocol 最大长度为 20"):
        validate_and_extract(
            _build_zip(tmp_path, protocol="p" * 21), tmp_path / "protocol-staging"
        )

    with pytest.raises(DriverZipError, match="version 最大长度为 50"):
        validate_and_extract(
            _build_zip(
                tmp_path, driver_id="version_drv", version="1" * 47 + ".0.0"
            ),
            tmp_path / "version-staging",
        )

    zip_bytes = _build_zip(tmp_path, driver_id="desc_drv")
    with zipfile.ZipFile(io.BytesIO(zip_bytes), "r") as source:
        entries = {info.filename: source.read(info) for info in source.infolist()}
    entries["desc_drv/driver.toml"] = _minimal_driver_toml(
        driver_id="desc_drv", description="d" * 256
    ).encode("utf-8")
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", compression=zipfile.ZIP_DEFLATED) as target:
        for name, content in entries.items():
            target.writestr(name, content)
    with pytest.raises(DriverZipError, match="description 最大长度为 255"):
        validate_and_extract(buf.getvalue(), tmp_path / "description-staging")


def test_manifest_read_error_does_not_expose_absolute_path(tmp_path: Path):
    manifest_path = tmp_path / "private" / "driver.toml"

    with pytest.raises(DriverZipError) as exc_info:
        _load_and_validate_manifest(manifest_path, manifest_path.parent)

    assert str(tmp_path) not in str(exc_info.value)


def test_validate_rejects_ntfs_ads_stream(tmp_path: Path):
    """NTFS 备用数据流：文件名含冒号必须拒绝。"""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr("demo_drv/driver.toml:Zone.Identifier", b"[ZoneTransfer]\nZoneId=3\n")
    with pytest.raises(DriverZipError, match="ADS|冒号"):
        validate_and_extract(buf.getvalue(), tmp_path / "staging")


def test_validate_rejects_casefold_path_collision(tmp_path: Path):
    """Windows 大小写不敏感：driver.py 与 DRIVER.py 不得并存。"""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr("demo_drv/driver.py", b"a = 1\n")
        zf.writestr("demo_drv/DRIVER.py", b"a = 2\n")
        zf.writestr("demo_drv/driver.toml", _minimal_driver_toml().encode("utf-8"))
    with pytest.raises(DriverZipError, match="路径碰撞"):
        validate_and_extract(buf.getvalue(), tmp_path / "staging")


def test_validate_rejects_nfc_nfd_path_collision(tmp_path: Path):
    """Unicode NFC / NFD 规范化后同一路径必须拒绝。"""
    import unicodedata

    # café：NFC 预组合 é，NFD 为 e + 组合尖音符
    nfc = unicodedata.normalize("NFC", "café")
    nfd = unicodedata.normalize("NFD", "café")
    assert nfc != nfd
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr(f"demo_drv/{nfc}.py", b"a = 1\n")
        zf.writestr(f"demo_drv/{nfd}.py", b"a = 2\n")
        zf.writestr("demo_drv/driver.toml", _minimal_driver_toml().encode("utf-8"))
    with pytest.raises(DriverZipError, match="路径碰撞"):
        validate_and_extract(buf.getvalue(), tmp_path / "staging")


def test_validate_rejects_file_then_child_prefix_conflict(tmp_path: Path):
    """先文件后子路径：demo_drv/foo 与 demo_drv/foo/bar 冲突。"""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr("demo_drv/foo", b"file\n")
        zf.writestr("demo_drv/foo/bar.py", b"x\n")
        zf.writestr("demo_drv/driver.toml", _minimal_driver_toml().encode("utf-8"))
    with pytest.raises(DriverZipError, match="前缀冲突"):
        validate_and_extract(buf.getvalue(), tmp_path / "staging")


def test_validate_rejects_child_then_file_prefix_conflict(tmp_path: Path):
    """先子路径后文件：demo_drv/foo/bar 与 demo_drv/foo 冲突。"""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr("demo_drv/foo/bar.py", b"x\n")
        zf.writestr("demo_drv/foo", b"file\n")
        zf.writestr("demo_drv/driver.toml", _minimal_driver_toml().encode("utf-8"))
    with pytest.raises(DriverZipError, match="前缀冲突"):
        validate_and_extract(buf.getvalue(), tmp_path / "staging")


def test_validate_rejects_duplicate_exact_entry(tmp_path: Path):
    """完全相同的重复 entry 也会覆盖，必须拒绝。"""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr("demo_drv/driver.py", b"a = 1\n")
        zf.writestr("demo_drv/driver.py", b"a = 2\n")
        zf.writestr("demo_drv/driver.toml", _minimal_driver_toml().encode("utf-8"))
    with pytest.raises(DriverZipError, match="路径碰撞"):
        validate_and_extract(buf.getvalue(), tmp_path / "staging")


@pytest.mark.parametrize(
    "evil_name",
    [
        "demo_drv/CON",
        "demo_drv/con.txt",
        "demo_drv/PRN/driver.toml",
        "demo_drv/AUX.py",
        "demo_drv/NUL",
        "demo_drv/COM1",
        "demo_drv/LPT9.data",
    ],
)
def test_validate_rejects_windows_reserved_device_names(tmp_path: Path, evil_name: str):
    """Windows 保留设备名（含扩展名变体）必须拒绝。"""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr(evil_name, b"x")
    with pytest.raises(DriverZipError, match="保留设备名"):
        validate_and_extract(buf.getvalue(), tmp_path / "staging")


@pytest.mark.parametrize(
    "evil_name",
    [
        "demo_drv/evil.",
        "demo_drv/evil. ",
        "demo_drv/ trailing.txt",
        "demo_drv/foo./bar.txt",
    ],
)
def test_validate_rejects_trailing_dot_or_space(tmp_path: Path, evil_name: str):
    """尾随点/空格或前导空格路径分量必须拒绝（Windows 会剥离导致落点偏移）。"""
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        zf.writestr(evil_name, b"x")
    with pytest.raises(DriverZipError, match="点或空格"):
        validate_and_extract(buf.getvalue(), tmp_path / "staging")


def test_validate_rejects_zip_bomb_entry_count(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    monkeypatch.setattr("app.services.driver_zip.MAX_ZIP_ENTRIES", 3)
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        for i in range(5):
            zf.writestr(f"f{i}.txt", b"x")
    with pytest.raises(DriverZipError) as exc_info:
        validate_and_extract(buf.getvalue(), tmp_path / "staging")
    assert exc_info.value.status_code == 413


def test_validate_rejects_bad_abi(tmp_path: Path):
    data = _build_zip(tmp_path, abi="cp311")
    with pytest.raises(DriverZipError, match="python_abi"):
        validate_and_extract(data, tmp_path / "staging")


def test_validate_rejects_builtin_protocol(tmp_path: Path):
    data = _build_zip(tmp_path, driver_id="s7_ext", protocol="s7")
    # id 与 entry 顶层包需一致；协议冲突在 manifest 校验后触发
    with pytest.raises(DriverZipError, match="内置"):
        validate_and_extract(data, tmp_path / "staging")


def test_validate_ok_extracts_package(tmp_path: Path):
    data = _build_zip(tmp_path)
    staging = tmp_path / "staging"
    result = validate_and_extract(data, staging)
    assert result.driver_id == "demo_drv"
    assert result.version == "1.0.0"
    assert result.sha256 == compute_sha256(data)
    assert (staging / "demo_drv" / "driver.toml").is_file()


def test_validate_rejects_preexisting_staging_without_deleting_it(tmp_path: Path):
    data = _build_zip(tmp_path)
    staging = tmp_path / "staging"
    staging.mkdir()
    marker = staging / "owned-by-other-process.txt"
    marker.write_text("keep", encoding="utf-8")

    with pytest.raises(DriverZipError, match="staging.*存在|独占"):
        validate_and_extract(data, staging)

    assert marker.read_text(encoding="utf-8") == "keep"


def test_validate_rejects_staging_symlink_without_touching_target(tmp_path: Path):
    data = _build_zip(tmp_path)
    target = tmp_path / "outside"
    target.mkdir()
    marker = target / "keep.txt"
    marker.write_text("keep", encoding="utf-8")
    staging = tmp_path / "staging-link"
    try:
        staging.symlink_to(target, target_is_directory=True)
    except OSError as exc:
        pytest.skip(f"当前 Windows 环境不允许创建测试符号链接: {exc}")

    with pytest.raises(DriverZipError, match="staging.*存在|链接|独占"):
        validate_and_extract(data, staging)

    assert marker.read_text(encoding="utf-8") == "keep"


def test_validate_rejects_overlong_path_component(tmp_path: Path):
    data = _build_zip(tmp_path, extra_member=f"demo_drv/{'a' * 201}.py")

    with pytest.raises(DriverZipError, match="路径分量过长"):
        validate_and_extract(data, tmp_path / "staging")


def test_validate_rejects_overlong_relative_path(tmp_path: Path):
    long_relative = "demo_drv/" + "/".join(["segment" * 5] * 7) + "/file.py"
    data = _build_zip(tmp_path, extra_member=long_relative)

    with pytest.raises(DriverZipError, match="ZIP 相对路径过长"):
        validate_and_extract(data, tmp_path / "staging")


def test_validate_rejects_overlong_final_path(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    data = _build_zip(tmp_path)
    staging = tmp_path / "staging"
    monkeypatch.setattr("app.services.driver_zip.MAX_FINAL_PATH_CHARS", 30)

    with pytest.raises(DriverZipError, match="最终落点路径过长"):
        validate_and_extract(data, staging)


# ── 安装事务 ──────────────────────────────────────────────────────────────────


@pytest.fixture
def installer(tmp_path: Path, monkeypatch: pytest.MonkeyPatch) -> DriverInstaller:
    root = tmp_path / "drivers"
    inst = DriverInstaller(root, health_wait_seconds=2.0, health_poll_interval=0.05)

    async def _fake_loaded(**kwargs):
        return True

    async def _fake_unloaded(*_args, **_kwargs):
        return True

    monkeypatch.setattr(inst, "_wait_for_driver_loaded", _fake_loaded)
    monkeypatch.setattr(inst, "_wait_for_driver_unloaded", _fake_unloaded)
    return inst


@pytest.mark.asyncio
async def test_install_success_and_idempotent(installer: DriverInstaller, tmp_path: Path):
    data = _build_zip(tmp_path)
    restarts: list[int] = []

    async def restart():
        restarts.append(1)

    async def alive():
        return True

    result = await installer.install(
        data, restart_collector=restart, collector_alive=alive, operator="admin"
    )
    assert result.created is True
    assert result.driver["status"] == "loaded"
    assert (installer.drivers_root / "demo_drv" / "driver.toml").is_file()
    assert installer.get_driver("demo_drv")["sha256"] == compute_sha256(data)
    assert len(restarts) == 1

    # 幂等
    again = await installer.install(
        data, restart_collector=restart, collector_alive=alive, operator="admin"
    )
    assert again.created is False
    assert len(restarts) == 1


@pytest.mark.asyncio
async def test_install_unexpected_validation_error_is_sanitized(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch, tmp_path: Path
):
    secret_path = tmp_path / "private" / "driver.toml"

    def fail_validation(*_args, **_kwargs):
        raise PermissionError(str(secret_path))

    async def restart():
        return None

    async def alive():
        return True

    monkeypatch.setattr(
        "app.services.driver_installer.validate_and_extract", fail_validation
    )
    with pytest.raises(DriverInstallError) as exc_info:
        await installer.install(
            b"zip", restart_collector=restart, collector_alive=alive, operator="admin"
        )

    assert exc_info.value.status_code == 400
    assert str(tmp_path) not in str(exc_info.value)


@pytest.mark.asyncio
async def test_install_does_not_delete_precreated_operation_staging(
    installer: DriverInstaller, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    data = _build_zip(tmp_path)
    operation_id = uuid.UUID(int=1).hex
    installer.ensure_layout()
    staging = installer.staging_root / operation_id
    staging.mkdir()
    marker = staging / "keep.txt"
    marker.write_text("keep", encoding="utf-8")
    monkeypatch.setattr(
        "app.services.driver_installer.uuid.uuid4", lambda: uuid.UUID(int=1)
    )

    async def restart():
        return None

    async def alive():
        return True

    with pytest.raises(DriverZipError, match="staging.*存在|独占"):
        await installer.install(data, restart_collector=restart, collector_alive=alive)

    assert marker.read_text(encoding="utf-8") == "keep"


@pytest.mark.asyncio
async def test_successful_upgrades_keep_only_current_previous_backup(
    installer: DriverInstaller, tmp_path: Path
):
    async def restart():
        return None

    async def alive():
        return True

    for version in ("1.0.0", "2.0.0", "3.0.0"):
        await installer.install(
            _build_zip(tmp_path, version=version),
            restart_collector=restart,
            collector_alive=alive,
        )

    backup_root = installer.backup_root / "demo_drv"
    assert {path.name for path in backup_root.iterdir()} == {"2.0.0"}

@pytest.mark.asyncio
async def test_install_same_version_different_sha_conflicts(
    installer: DriverInstaller, tmp_path: Path
):
    data1 = _build_zip(tmp_path, version="1.0.0")

    async def restart():
        return None

    async def alive():
        return True

    await installer.install(data1, restart_collector=restart, collector_alive=alive)

    # 同版本但改动入口文件内容 → SHA 不同
    root = tmp_path / "alt"
    pkg = root / "demo_drv" / "demo_drv"
    pkg.mkdir(parents=True)
    (root / "demo_drv" / "driver.toml").write_text(
        _minimal_driver_toml(version="1.0.0"), encoding="utf-8"
    )
    (pkg / "__init__.py").write_text("", encoding="utf-8")
    (pkg / "driver.py").write_text(
        "def create_driver(context):\n    return 'changed'\n",
        encoding="utf-8",
    )
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w") as zf:
        for path in (root / "demo_drv").rglob("*"):
            if path.is_file():
                zf.write(path, arcname=path.relative_to(root).as_posix())
    data2 = buf.getvalue()
    assert compute_sha256(data1) != compute_sha256(data2)

    with pytest.raises(DriverInstallError) as exc_info:
        await installer.install(data2, restart_collector=restart, collector_alive=alive)
    assert exc_info.value.status_code == 409


@pytest.mark.asyncio
async def test_install_rollback_when_collector_not_alive(
    installer: DriverInstaller, tmp_path: Path
):
    data = _build_zip(tmp_path, version="1.0.0")

    async def ok_restart():
        return None

    async def ok_alive():
        return True

    await installer.install(data, restart_collector=ok_restart, collector_alive=ok_alive)
    assert (installer.drivers_root / "demo_drv").is_dir()

    # 升级包
    data_v2 = _build_zip(tmp_path, version="2.0.0")

    async def bad_alive():
        return False

    with pytest.raises(DriverInstallError) as exc_info:
        await installer.install(
            data_v2, restart_collector=ok_restart, collector_alive=bad_alive
        )
    assert exc_info.value.status_code == 503

    # 应回滚到 1.0.0
    record = installer.get_driver("demo_drv")
    assert record is not None
    assert record["version"] == "1.0.0"
    assert record["status"] == "rollback"
    toml = (installer.drivers_root / "demo_drv" / "driver.toml").read_text(encoding="utf-8")
    assert 'version = "1.0.0"' in toml


@pytest.mark.asyncio
async def test_uninstall_removes_package(installer: DriverInstaller, tmp_path: Path):
    data = _build_zip(tmp_path)

    async def restart():
        return None

    async def alive():
        return True

    await installer.install(data, restart_collector=restart, collector_alive=alive)
    result = await installer.uninstall(
        "demo_drv", restart_collector=restart, collector_alive=alive
    )
    assert result["status"] == "uninstalled"
    assert installer.get_driver("demo_drv") is None
    assert not (installer.drivers_root / "demo_drv").exists()


@pytest.mark.asyncio
@pytest.mark.parametrize("driver_id", [".backup", "../escape", "state.json"])
async def test_uninstall_rejects_internal_or_invalid_driver_id(
    installer: DriverInstaller, driver_id: str
):
    installer.ensure_layout()
    sentinel = installer.backup_root / "keep.txt"
    sentinel.write_text("keep", encoding="utf-8")

    async def restart():
        return None

    async def alive():
        return True

    with pytest.raises(DriverInstallError) as exc_info:
        await installer.uninstall(
            driver_id, restart_collector=restart, collector_alive=alive
        )

    assert exc_info.value.status_code == 400
    assert sentinel.read_text(encoding="utf-8") == "keep"


@pytest.mark.asyncio
async def test_uninstall_rejects_directory_without_manifest(installer: DriverInstaller):
    installer.ensure_layout()
    (installer.drivers_root / "demo_drv").mkdir()

    async def restart():
        return None

    async def alive():
        return True

    with pytest.raises(DriverInstallError, match="driver.toml"):
        await installer.uninstall(
            "demo_drv", restart_collector=restart, collector_alive=alive
        )


def test_validate_rejects_builtin_driver_id_with_external_protocol(tmp_path: Path):
    data = _build_zip(tmp_path, driver_id="s7", protocol="demo_proto")

    with pytest.raises(DriverZipError, match="保留名"):
        validate_and_extract(data, tmp_path / "stage")


# ── P1-5：崩溃恢复 / 幂等一致性 ───────────────────────────────────────────────


def _write_driver_tree(
    target: Path, *, version: str, driver_id: str = "demo_drv"
) -> None:
    """在激活或 backup 目录写入最小驱动树。"""
    mod = target / driver_id
    mod.mkdir(parents=True, exist_ok=True)
    (target / "driver.toml").write_text(
        _minimal_driver_toml(driver_id=driver_id, version=version), encoding="utf-8"
    )
    (mod / "__init__.py").write_text('"""demo"""\n', encoding="utf-8")
    (mod / "driver.py").write_text(
        "def create_driver(context):\n    return None\n",
        encoding="utf-8",
    )


async def _recover_ok(installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch):
    """恢复流程：假重启 + /drivers 探针一律通过。"""

    async def restart():
        return None

    async def alive():
        return True

    async def loaded(*, driver_id: str, version: str, status_url=None):
        return True

    async def unloaded(driver_id: str, *, status_url=None):
        return True

    monkeypatch.setattr(installer, "_wait_for_driver_loaded", loaded)
    monkeypatch.setattr(installer, "_wait_for_driver_unloaded", unloaded)
    return await installer.recover_incomplete_operations(
        restart_collector=restart,
        collector_alive=alive,
    )


@pytest.mark.asyncio
async def test_recover_rollback_when_crash_during_activating(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """模拟 activating 阶段崩溃：应恢复 backup、还原 state，并经 collector 验证后终态。"""
    installer.ensure_layout()
    driver_id = "demo_drv"
    op_id = "op_crash_activating"
    previous = {
        "id": driver_id,
        "version": "1.0.0",
        "sha256": "aaa",
        "status": "loaded",
        "source": "installed",
    }
    # 旧版在 backup，半成品新版在 active（崩溃现场）
    backup_dir = installer.backup_root / driver_id / "1.0.0"
    _write_driver_tree(backup_dir, version="1.0.0")
    active_dir = installer.drivers_root / driver_id
    _write_driver_tree(active_dir, version="2.0.0")
    installer._upsert_driver_record(
        {
            "id": driver_id,
            "version": "2.0.0",
            "sha256": "bbb",
            "status": "installed",
            "source": "installed",
        }
    )
    staging = installer.staging_root / op_id
    staging.mkdir(parents=True, exist_ok=True)
    (staging / "leftover.txt").write_text("x", encoding="utf-8")

    installer._persist_operation(
        op_id,
        "activating",
        driver_id,
        "2.0.0",
        previous_record=previous,
    )

    results = await _recover_ok(installer, monkeypatch)
    assert len(results) == 1
    assert results[0]["action"] == "rollback"
    assert results[0]["verified"] is True

    record = installer.get_driver(driver_id)
    assert record is not None
    assert record["version"] == "1.0.0"
    assert record["status"] == "rollback"
    toml = (installer.drivers_root / driver_id / "driver.toml").read_text(encoding="utf-8")
    assert 'version = "1.0.0"' in toml
    assert not (installer.backup_root / driver_id / "1.0.0").exists()
    assert not staging.exists()
    assert installer._read_state().get("operation") is None


@pytest.mark.asyncio
async def test_recover_rollback_when_crash_during_finalizing_legacy(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """旧 finalizing 视为未验证：崩溃恢复必须回滚，不能删 backup 提交坏包。"""
    installer.ensure_layout()
    driver_id = "demo_drv"
    op_id = "op_crash_finalizing"
    previous = {
        "id": driver_id,
        "version": "1.0.0",
        "sha256": "aaa",
        "status": "loaded",
        "source": "installed",
    }
    active_dir = installer.drivers_root / driver_id
    _write_driver_tree(active_dir, version="2.0.0")
    backup_dir = installer.backup_root / driver_id / "1.0.0"
    _write_driver_tree(backup_dir, version="1.0.0")
    installer._upsert_driver_record(
        {
            "id": driver_id,
            "version": "2.0.0",
            "sha256": "bbb",
            "status": "installed",
            "source": "installed",
        }
    )
    installer._persist_operation(
        op_id,
        "finalizing",
        driver_id,
        "2.0.0",
        previous_record=previous,
    )

    results = await _recover_ok(installer, monkeypatch)
    assert len(results) == 1
    assert results[0]["action"] == "rollback"
    assert results[0]["verified"] is True

    record = installer.get_driver(driver_id)
    assert record is not None
    assert record["version"] == "1.0.0"
    assert record["status"] == "rollback"
    toml = (installer.drivers_root / driver_id / "driver.toml").read_text(encoding="utf-8")
    assert 'version = "1.0.0"' in toml
    assert not (installer.backup_root / driver_id / "1.0.0").exists()
    assert installer._read_state().get("operation") is None


@pytest.mark.asyncio
async def test_recover_keeps_incomplete_when_collector_probe_fails(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """磁盘已回滚但 /drivers 验证失败：不得写终态，保留未完成事务。"""
    installer.ensure_layout()
    driver_id = "demo_drv"
    op_id = "op_probe_fail"
    previous = {
        "id": driver_id,
        "version": "1.0.0",
        "sha256": "aaa",
        "status": "loaded",
        "source": "installed",
    }
    backup_dir = installer.backup_root / driver_id / "1.0.0"
    _write_driver_tree(backup_dir, version="1.0.0")
    active_dir = installer.drivers_root / driver_id
    _write_driver_tree(active_dir, version="2.0.0")
    installer._upsert_driver_record(
        {
            "id": driver_id,
            "version": "2.0.0",
            "sha256": "bbb",
            "status": "installed",
            "source": "installed",
        }
    )
    installer._persist_operation(
        op_id,
        "verifying",
        driver_id,
        "2.0.0",
        previous_record=previous,
    )

    async def restart():
        return None

    async def alive():
        return True

    async def loaded_fail(*, driver_id: str, version: str, status_url=None):
        return False

    monkeypatch.setattr(installer, "_wait_for_driver_loaded", loaded_fail)
    results = await installer.recover_incomplete_operations(
        restart_collector=restart,
        collector_alive=alive,
    )
    assert len(results) == 1
    assert results[0]["verified"] is False
    # 磁盘已回到旧版，但事务未终结
    assert installer.get_driver(driver_id)["version"] == "1.0.0"
    assert installer._read_state().get("operation") is not None


@pytest.mark.asyncio
async def test_recover_rollback_when_crash_during_verifying(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """verifying 阶段崩溃：磁盘回滚旧版，collector 验证通过后写 rolled_back。"""
    installer.ensure_layout()
    driver_id = "demo_drv"
    op_id = "op_crash_verifying"
    previous = {
        "id": driver_id,
        "version": "1.0.0",
        "sha256": "aaa",
        "status": "loaded",
        "source": "installed",
    }
    active_dir = installer.drivers_root / driver_id
    _write_driver_tree(active_dir, version="2.0.0")
    backup_dir = installer.backup_root / driver_id / "1.0.0"
    _write_driver_tree(backup_dir, version="1.0.0")
    installer._upsert_driver_record(
        {
            "id": driver_id,
            "version": "2.0.0",
            "sha256": "bbb",
            "status": "installed",
            "source": "installed",
        }
    )
    installer._persist_operation(
        op_id,
        "verifying",
        driver_id,
        "2.0.0",
        previous_record=previous,
    )

    results = await _recover_ok(installer, monkeypatch)
    assert len(results) == 1
    assert results[0]["action"] == "rollback"
    record = installer.get_driver(driver_id)
    assert record is not None
    assert record["version"] == "1.0.0"


@pytest.mark.asyncio
async def test_recover_commit_when_crash_during_verified(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """verified 阶段崩溃：collector 确认版本后提交并保留上一版本。"""
    installer.ensure_layout()
    driver_id = "demo_drv"
    op_id = "op_crash_verified"
    previous = {
        "id": driver_id,
        "version": "1.0.0",
        "sha256": "aaa",
        "status": "loaded",
        "source": "installed",
    }
    active_dir = installer.drivers_root / driver_id
    _write_driver_tree(active_dir, version="2.0.0")
    backup_dir = installer.backup_root / driver_id / "1.0.0"
    _write_driver_tree(backup_dir, version="1.0.0")
    installer._upsert_driver_record(
        {
            "id": driver_id,
            "version": "2.0.0",
            "sha256": "bbb",
            "status": "loaded",
            "source": "installed",
        }
    )
    installer._persist_operation(
        op_id,
        "verified",
        driver_id,
        "2.0.0",
        previous_record=previous,
    )

    results = await _recover_ok(installer, monkeypatch)
    assert len(results) == 1
    assert results[0]["action"] == "commit"
    assert results[0]["verified"] is True

    record = installer.get_driver(driver_id)
    assert record is not None
    assert record["version"] == "2.0.0"
    assert record["status"] == "loaded"
    assert (installer.drivers_root / driver_id / "driver.toml").is_file()
    assert {path.name for path in (installer.backup_root / driver_id).iterdir()} == {
        "1.0.0"
    }
    assert installer._read_state().get("operation") is None


@pytest.mark.asyncio
async def test_recover_uninstall_rollback_when_crash_after_moved_to_trash(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """moved_to_trash 崩溃：应从 trash 还原目录与 state。"""
    installer.ensure_layout()
    driver_id = "demo_drv"
    op_id = "op_uninstall_trash"
    previous = {
        "id": driver_id,
        "version": "1.0.0",
        "sha256": "aaa",
        "status": "loaded",
        "source": "installed",
    }
    trash_dir = installer.trash_root / op_id / driver_id
    _write_driver_tree(trash_dir, version="1.0.0")
    # state 已删除（卸载中途）
    state = installer._read_state()
    state["drivers"] = {}
    installer._write_state(state)
    installer._persist_operation(
        op_id,
        "moved_to_trash",
        driver_id,
        "1.0.0",
        previous_record=previous,
        op_type="uninstall",
    )

    results = await _recover_ok(installer, monkeypatch)
    assert len(results) == 1
    assert results[0]["action"] == "rollback"
    assert results[0]["type"] == "uninstall"
    assert results[0]["verified"] is True

    record = installer.get_driver(driver_id)
    assert record is not None
    assert record["version"] == "1.0.0"
    assert (installer.drivers_root / driver_id / "driver.toml").is_file()
    assert not (installer.trash_root / op_id).exists()
    assert installer._read_state().get("operation") is None


@pytest.mark.asyncio
async def test_recover_uninstall_commit_when_crash_after_collector_verified(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """collector_verified 崩溃：collector 确认 absent 后清理 trash 并提交。"""
    installer.ensure_layout()
    driver_id = "demo_drv"
    op_id = "op_uninstall_verified"
    previous = {
        "id": driver_id,
        "version": "1.0.0",
        "sha256": "aaa",
        "status": "loaded",
        "source": "installed",
    }
    trash_dir = installer.trash_root / op_id / driver_id
    _write_driver_tree(trash_dir, version="1.0.0")
    state = installer._read_state()
    state["drivers"] = {}
    installer._write_state(state)
    installer._persist_operation(
        op_id,
        "collector_verified",
        driver_id,
        "1.0.0",
        previous_record=previous,
        op_type="uninstall",
    )

    results = await _recover_ok(installer, monkeypatch)
    assert len(results) == 1
    assert results[0]["action"] == "commit"
    assert results[0]["type"] == "uninstall"
    assert results[0]["verified"] is True
    assert installer.get_driver(driver_id) is None
    assert not (installer.drivers_root / driver_id).exists()
    assert not (installer.trash_root / op_id).exists()
    assert installer._read_state().get("operation") is None


@pytest.mark.asyncio
async def test_uninstall_rolls_back_when_driver_still_in_registry(
    installer: DriverInstaller, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """restart 成功但 /drivers 仍含目标驱动 → 不得进入 collector_verified，应回滚。"""
    data = _build_zip(tmp_path)
    installer.ensure_layout()

    async def restart():
        return None

    async def alive():
        return True

    async def always_loaded(*, driver_id: str, version: str, status_url=None):
        return True

    async def never_unloaded(driver_id: str, *, status_url=None):
        return False

    monkeypatch.setattr(installer, "_wait_for_driver_loaded", always_loaded)
    monkeypatch.setattr(installer, "_wait_for_driver_unloaded", never_unloaded)

    await installer.install(data, restart_collector=restart, collector_alive=alive)
    assert (installer.drivers_root / "demo_drv").is_dir()

    with pytest.raises(DriverInstallError, match="仍加载驱动"):
        await installer.uninstall(
            "demo_drv",
            restart_collector=restart,
            collector_alive=alive,
        )

    # 回滚后目录与 state 应恢复
    assert (installer.drivers_root / "demo_drv").is_dir()
    assert installer.get_driver("demo_drv") is not None
    assert installer._read_state().get("operation") is None


@pytest.mark.asyncio
async def test_list_drivers_marks_missing_when_active_dir_gone(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """state 仍有记录但目录/driver.toml 缺失 → 列表 status=missing。"""
    installer.ensure_layout()
    installer._upsert_driver_record(
        {
            "id": "demo_drv",
            "version": "1.0.0",
            "sha256": "aaa",
            "status": "loaded",
            "source": "installed",
        }
    )
    assert not (installer.drivers_root / "demo_drv").exists()

    async def probe():
        return {}

    monkeypatch.setattr(installer, "_probe_collector_drivers", probe)
    items = await installer.list_drivers()
    assert len(items) == 1
    assert items[0]["status"] == "missing"
    assert "driver.toml" in (items[0].get("error") or "")


@pytest.mark.asyncio
async def test_list_drivers_marks_load_failed_when_collector_absent(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """目录在但 collector /drivers 无此项 → 不得继续标 loaded。"""
    installer.ensure_layout()
    active = installer.drivers_root / "demo_drv"
    _write_driver_tree(active, version="1.0.0")
    installer._upsert_driver_record(
        {
            "id": "demo_drv",
            "version": "1.0.0",
            "sha256": "aaa",
            "status": "loaded",
            "source": "installed",
        }
    )

    async def probe():
        return {}

    monkeypatch.setattr(installer, "_probe_collector_drivers", probe)
    items = await installer.list_drivers()
    assert items[0]["status"] == "load_failed"


@pytest.mark.asyncio
async def test_list_drivers_preserves_collector_load_failure_reason(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    installer.ensure_layout()
    _write_driver_tree(installer.drivers_root / "demo_drv", version="1.0.0")

    async def probe():
        return {
            "demo_drv": {
                "id": "demo_drv",
                "status": "load_failed",
                "error": "vendor import failed",
            }
        }

    monkeypatch.setattr(installer, "_probe_collector_drivers", probe)
    items = await installer.list_drivers()

    assert items[0]["status"] == "load_failed"
    assert items[0]["error"] == "vendor import failed"


@pytest.mark.asyncio
async def test_list_drivers_unknown_when_collector_unreachable(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """collector 不可达时不得继续报告 loaded。"""
    installer.ensure_layout()
    active = installer.drivers_root / "demo_drv"
    _write_driver_tree(active, version="1.0.0")
    installer._upsert_driver_record(
        {
            "id": "demo_drv",
            "version": "1.0.0",
            "status": "loaded",
            "source": "installed",
        }
    )

    async def probe():
        return None

    monkeypatch.setattr(installer, "_probe_collector_drivers", probe)
    items = await installer.list_drivers()
    assert items[0]["status"] == "unknown"


@pytest.mark.asyncio
async def test_list_drivers_includes_disk_only_untracked(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """磁盘有包但 state 丢失 → 并集中仍出现，标 untracked。"""
    installer.ensure_layout()
    active = installer.drivers_root / "orphan_drv"
    _write_driver_tree(active, version="1.0.0", driver_id="orphan_drv")

    async def probe():
        return {}

    monkeypatch.setattr(installer, "_probe_collector_drivers", probe)
    items = await installer.list_drivers()
    ids = {i["id"] for i in items}
    assert "orphan_drv" in ids
    orphan = next(i for i in items if i["id"] == "orphan_drv")
    assert orphan["source"] == "untracked"
    assert orphan["status"] == "load_failed"


@pytest.mark.asyncio
async def test_list_drivers_disk_missing_but_collector_loaded(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """磁盘包缺失但旧 collector 仍加载 → status=missing，runtime_status=loaded。"""
    installer.ensure_layout()
    installer._upsert_driver_record(
        {
            "id": "demo_drv",
            "version": "1.0.0",
            "status": "loaded",
            "source": "installed",
        }
    )

    async def probe():
        return {"demo_drv": {"id": "demo_drv", "version": "1.0.0"}}

    monkeypatch.setattr(installer, "_probe_collector_drivers", probe)
    items = await installer.list_drivers()
    assert items[0]["status"] == "missing"
    assert items[0]["runtime_status"] == "loaded"
    assert "仍加载" in (items[0].get("error") or "")


@pytest.mark.asyncio
async def test_list_drivers_collector_only_without_disk(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """仅 collector 有、无 state/磁盘 → missing + runtime_status=loaded。"""
    installer.ensure_layout()

    async def probe():
        return {"ghost_drv": {"id": "ghost_drv", "version": "9.9.9"}}

    monkeypatch.setattr(installer, "_probe_collector_drivers", probe)
    items = await installer.list_drivers()
    ghost = next(i for i in items if i["id"] == "ghost_drv")
    assert ghost["status"] == "missing"
    assert ghost["runtime_status"] == "loaded"
    assert ghost["source"] == "untracked"


@pytest.mark.asyncio
async def test_partial_disk_recovery_failure_keeps_block(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """一项磁盘恢复失败、另一项验证成功 → 仍必须保持门禁。"""
    installer.ensure_layout()
    # op1：可正常回滚
    prev = {
        "id": "drv_a",
        "version": "1.0.0",
        "status": "loaded",
        "source": "installed",
    }
    _write_driver_tree(installer.backup_root / "drv_a" / "1.0.0", version="1.0.0", driver_id="drv_a")
    _write_driver_tree(installer.drivers_root / "drv_a", version="2.0.0", driver_id="drv_a")
    installer._persist_operation(
        "op_ok", "verifying", "drv_a", "2.0.0", previous_record=prev
    )
    # op2：人为制造磁盘恢复异常（损坏 previous_record 路径由 monkeypatch）
    installer._persist_operation(
        "op_bad",
        "verifying",
        "drv_b",
        "2.0.0",
        previous_record={"id": "drv_b", "version": "1.0.0"},
    )

    real_apply = installer._apply_install_rollback_disk

    def flaky_apply(op):
        if str(op.get("op_id")) == "op_bad":
            raise RuntimeError("simulated disk failure")
        return real_apply(op)

    monkeypatch.setattr(installer, "_apply_install_rollback_disk", flaky_apply)

    async def restart():
        return None

    async def alive():
        return True

    async def loaded(*, driver_id: str, version: str, status_url=None):
        return True

    monkeypatch.setattr(installer, "_wait_for_driver_loaded", loaded)
    results = await installer.recover_incomplete_operations(
        restart_collector=restart,
        collector_alive=alive,
    )
    assert any(r.get("verified") for r in results)
    assert any(not r.get("verified") for r in results)
    assert installer.recovery_blocked is True
    incomplete = installer._latest_incomplete_operations()
    assert any(str(o.get("op_id")) == "op_bad" for o in incomplete)


@pytest.mark.asyncio
async def test_concurrent_recover_serializes_restart(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    """两个并发 recover 共用锁：第一个终结后，第二个不再重启。"""
    installer.ensure_layout()
    prev = {
        "id": "demo_drv",
        "version": "1.0.0",
        "status": "loaded",
        "source": "installed",
    }
    _write_driver_tree(installer.backup_root / "demo_drv" / "1.0.0", version="1.0.0")
    _write_driver_tree(installer.drivers_root / "demo_drv", version="2.0.0")
    installer._persist_operation(
        "op_conc", "verifying", "demo_drv", "2.0.0", previous_record=prev
    )

    restarts: list[int] = []

    async def restart():
        restarts.append(1)
        await asyncio.sleep(0.05)

    async def alive():
        return True

    async def loaded(*, driver_id: str, version: str, status_url=None):
        return True

    monkeypatch.setattr(installer, "_wait_for_driver_loaded", loaded)
    t1 = asyncio.create_task(
        installer.recover_incomplete_operations(
            restart_collector=restart, collector_alive=alive
        )
    )
    t2 = asyncio.create_task(
        installer.recover_incomplete_operations(
            restart_collector=restart, collector_alive=alive
        )
    )
    r1, r2 = await asyncio.gather(t1, t2)
    assert len(restarts) == 1
    # 其中一个拿到计划并终结；另一个计划为空
    assert sum(1 for r in (r1, r2) if r) == 1
    assert installer.recovery_blocked is False


@pytest.mark.asyncio
async def test_mutation_blocked_when_recovery_failed(
    installer: DriverInstaller, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """恢复验证失败后应阻断新的安装/卸载。"""
    installer.ensure_layout()
    driver_id = "demo_drv"
    op_id = "op_block"
    previous = {
        "id": driver_id,
        "version": "1.0.0",
        "sha256": "aaa",
        "status": "loaded",
        "source": "installed",
    }
    backup_dir = installer.backup_root / driver_id / "1.0.0"
    _write_driver_tree(backup_dir, version="1.0.0")
    active_dir = installer.drivers_root / driver_id
    _write_driver_tree(active_dir, version="2.0.0")
    installer._persist_operation(
        op_id,
        "verifying",
        driver_id,
        "2.0.0",
        previous_record=previous,
    )

    async def restart():
        return None

    async def alive():
        return True

    async def loaded_fail(*, driver_id: str, version: str, status_url=None):
        return False

    monkeypatch.setattr(installer, "_wait_for_driver_loaded", loaded_fail)
    results = await installer.recover_incomplete_operations(
        restart_collector=restart,
        collector_alive=alive,
    )
    assert results[0]["verified"] is False
    assert installer.recovery_blocked is True

    with pytest.raises(DriverInstallError, match="driver_recovery_blocked"):
        await installer.install(
            _build_zip(tmp_path),
            restart_collector=restart,
            collector_alive=alive,
        )


@pytest.mark.asyncio
async def test_install_rollback_keeps_verifying_when_old_driver_not_loaded(
    installer: DriverInstaller, tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """目录已恢复旧版但 /drivers 未加载 → 不得写 rolled_back。"""
    data1 = _build_zip(tmp_path, version="1.0.0")
    data2 = _build_zip(tmp_path, version="2.0.0")

    async def restart():
        return None

    async def alive():
        return True

    # 首次安装成功
    await installer.install(data1, restart_collector=restart, collector_alive=alive)

    # 升级验证与回滚验证均失败 → 停留在 rollback_verifying
    async def loaded_always_fail(*, driver_id: str, version: str, status_url=None):
        return False

    monkeypatch.setattr(installer, "_wait_for_driver_loaded", loaded_always_fail)

    with pytest.raises(DriverInstallError, match="回滚后 collector 未加载|未成功加载"):
        await installer.install(
            data2, restart_collector=restart, collector_alive=alive
        )

    # 事务停留在 rollback_verifying，未终态
    incomplete = installer._latest_incomplete_operations()
    assert incomplete
    assert incomplete[0]["phase"] == "rollback_verifying"
    assert installer.recovery_blocked is True
    assert installer._read_state().get("operation") is not None


@pytest.mark.asyncio
async def test_idempotent_rejects_missing_active_dir(
    installer: DriverInstaller, tmp_path: Path
):
    """state 有同版本同 SHA 记录但激活目录缺失 → 不一致错误。"""
    data = _build_zip(tmp_path)
    sha = compute_sha256(data)
    installer.ensure_layout()
    installer._upsert_driver_record(
        {
            "id": "demo_drv",
            "version": "1.0.0",
            "sha256": sha,
            "status": "loaded",
            "source": "installed",
        }
    )
    assert not (installer.drivers_root / "demo_drv").exists()

    async def restart():
        return None

    async def alive():
        return True

    with pytest.raises(InconsistentStateError, match="激活目录不存在"):
        await installer.install(data, restart_collector=restart, collector_alive=alive)


@pytest.mark.asyncio
async def test_recovery_commit_keeps_only_previous_version_backup(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    installer.ensure_layout()
    _write_driver_tree(installer.drivers_root / "demo_drv", version="3.0.0")
    _write_driver_tree(installer.backup_root / "demo_drv" / "1.0.0", version="1.0.0")
    _write_driver_tree(installer.backup_root / "demo_drv" / "2.0.0", version="2.0.0")
    previous = {
        "id": "demo_drv",
        "version": "2.0.0",
        "status": "loaded",
        "source": "installed",
    }
    installer._persist_operation(
        "verified-upgrade",
        "verified",
        "demo_drv",
        "3.0.0",
        previous_record=previous,
    )

    results = await _recover_ok(installer, monkeypatch)

    assert results[0]["verified"] is True
    backup_root = installer.backup_root / "demo_drv"
    assert {path.name for path in backup_root.iterdir()} == {"2.0.0"}


def test_terminal_operation_compaction_keeps_incomplete_and_limited_audit(
    installer: DriverInstaller, monkeypatch: pytest.MonkeyPatch
):
    installer.ensure_layout()
    monkeypatch.setattr("app.services.driver_installer._TERMINAL_AUDIT_LIMIT", 2)
    installer._persist_operation("live", "staging", "live_drv", "1.0.0")
    installer._persist_operation("live", "activating", "live_drv", "1.0.0")

    for index in range(4):
        installer._persist_operation(
            f"done-{index}", "committed", f"done_{index}", "1.0.0"
        )

    entries = [
        json.loads(line)
        for line in installer.operation_log_path.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]
    assert [entry["op_id"] for entry in entries] == ["live", "done-2", "done-3"]
    assert entries[0]["phase"] == "activating"
    assert installer._latest_incomplete_operations()[0]["op_id"] == "live"
    assert not installer.operation_log_path.with_suffix(".jsonl.tmp").exists()


def test_terminal_compaction_replaces_incomplete_state(installer: DriverInstaller):
    installer.ensure_layout()
    installer._persist_operation("op-1", "staging", "demo_drv", "1.0.0")
    installer._persist_operation("op-1", "committed", "demo_drv", "1.0.0")

    entries = [
        json.loads(line)
        for line in installer.operation_log_path.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]

    assert [(entry["op_id"], entry["phase"]) for entry in entries] == [
        ("op-1", "committed")
    ]
