"""Target-runtime lifecycle regression; never connects to a PLC."""
from pathlib import Path
import json
import os
import shutil
import pytest
from app.services.driver_installer import DriverInstaller, DriverInstallError
from data_watch_driver_catalog import read_catalog, tree_digest

ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT / "packages/driver-catalog/catalog.json"
S7_VERSION = next(item["version"] for item in read_catalog(CATALOG)["drivers"] if item["id"] == "s7")

@pytest.fixture
def real_installer(tmp_path, monkeypatch):
    monkeypatch.setenv("DATAWATCH_ROLE", "centralized")
    target = os.environ.get("DATAWATCH_TEST_COLLECTOR_EXE")
    executable = Path(target) if target else tmp_path / "not-built.exe"
    monkeypatch.setattr("app.core.config.module_paths", lambda name: (executable, executable.parent))
    monkeypatch.setattr("app.core.config.install_root", lambda: ROOT)
    return DriverInstaller(tmp_path / "drivers", catalog_path=CATALOG, health_wait_seconds=0.1, health_poll_interval=0.01)

async def stopped():
    return False

async def must_not_start():
    raise AssertionError("A stopped Collector must remain stopped")

@pytest.mark.asyncio
async def test_first_preinstall_uninstall_restart_and_restore(real_installer):
    inst = real_installer
    installed = await inst.initialize_preinstalled(restart_collector=must_not_start, collector_alive=stopped)
    assert set(installed) == {"s7", "ge_srtp", "opcua", "opcda_center", "modbus_tcp"}
    items = await inst.list_drivers(collector_status="stopped")
    assert all(item["install_status"] == "installed" and item["runtime_status"] == "collector_stopped" for item in items)
    assert all(item["preflight_verified"] for item in items)
    await inst.uninstall("s7", restart_collector=must_not_start, collector_alive=stopped)
    assert not (inst.drivers_root / "s7").exists()
    assert "s7" in inst._read_state()["removed"]
    assert await inst.initialize_preinstalled(restart_collector=must_not_start, collector_alive=stopped) == []
    assert not (inst.drivers_root / "s7").exists()
    result = await inst.install(inst.restoration_archive("s7"), restart_collector=must_not_start, collector_alive=stopped)
    assert result.driver["status"] == "installed"
    assert "s7" not in inst._read_state()["removed"]
    assert (inst.drivers_root / "s7/driver.toml").is_file()

@pytest.mark.asyncio
async def test_explicit_repair_replaces_tampered_official_package(real_installer):
    inst = real_installer
    archive = inst.restoration_archive("modbus_tcp")
    await inst.install(archive, restart_collector=must_not_start, collector_alive=stopped)
    code = inst.drivers_root / "modbus_tcp/modbus_tcp/driver.py"
    code.write_text(code.read_text(encoding="utf-8") + "\n# unexpected mutation", encoding="utf-8")
    items = await inst.list_drivers(collector_status="stopped")
    assert next(item for item in items if item["id"] == "modbus_tcp")["install_status"] == "invalid"
    await inst.install(archive, restart_collector=must_not_start, collector_alive=stopped)
    assert tree_digest(inst.drivers_root / "modbus_tcp") == inst.get_driver("modbus_tcp")["tree_sha256"]

@pytest.mark.asyncio
async def test_stop_precedes_switch_and_restart_then_digest_probe(real_installer, monkeypatch):
    inst = real_installer
    running = True
    events = []
    async def alive():
        return running
    async def stop():
        nonlocal running
        assert not (inst.drivers_root / "s7").exists()
        events.append("stop")
        running = False
    async def restart():
        nonlocal running
        assert (inst.drivers_root / "s7/driver.toml").is_file()
        events.append("restart")
        running = True
    identity = {"role": "collector", "deployment_role": "centralized", "data_root": str(inst.drivers_root.parent), "boot_id": "old", "build_id": "build", "pid": 7}
    def fetch(url):
        current = dict(identity, boot_id="new" if "restart" in events else "old")
        active = inst.drivers_root / "s7"
        drivers = [{"id": "s7", "version": S7_VERSION, "status": "loaded", "tree_sha256": tree_digest(active)}] if active.is_dir() else []
        return {"instance": current, "drivers": drivers}
    inst._stop_collector = stop
    inst._collector_pid = lambda: 7
    monkeypatch.setattr(inst, "_fetch_json", fetch)
    result = await inst.install(inst.restoration_archive("s7"), restart_collector=restart, collector_alive=alive)
    assert events == ["stop", "restart"]
    assert result.driver["status"] == "loaded"
    assert running

@pytest.mark.asyncio
async def test_stop_failure_never_switches_directory(real_installer):
    inst = real_installer
    async def alive(): return True
    async def ineffective_stop(): pass
    inst._stop_collector = ineffective_stop
    with pytest.raises(DriverInstallError, match="尚未退出"):
        await inst.install(inst.restoration_archive("s7"), restart_collector=must_not_start, collector_alive=alive)
    assert not (inst.drivers_root / "s7").exists()
    assert not inst._latest_incomplete_operations()

@pytest.mark.asyncio
@pytest.mark.parametrize("mismatch", ["pid", "boot_id", "tree_sha256", "version"])
async def test_probe_rejects_wrong_process_or_package(real_installer, monkeypatch, mismatch):
    inst = real_installer
    active = inst.drivers_root / "demo"
    active.mkdir(parents=True)
    (active / "driver.toml").write_text("test", encoding="utf-8")
    inst._collector_pid = lambda: 7
    inst._previous_boot_id = "old"
    identity = {"role": "collector", "deployment_role": "centralized", "data_root": str(inst.drivers_root.parent), "boot_id": "new", "build_id": "build", "pid": 7}
    item = {"id": "demo", "version": "1.0.0", "status": "loaded", "tree_sha256": tree_digest(active)}
    if mismatch in identity: identity[mismatch] = "old" if mismatch == "boot_id" else 99
    else: item[mismatch] = "wrong"
    monkeypatch.setattr(inst, "_fetch_json", lambda url: {"instance": identity, "drivers": [item]})
    assert not await inst._wait_for_driver_loaded(driver_id="demo", version="1.0.0")

@pytest.mark.asyncio
async def test_corrupt_state_never_triggers_first_install(real_installer):
    inst = real_installer
    inst.ensure_layout()
    inst.state_path.write_text("broken", encoding="utf-8")
    with pytest.raises(DriverInstallError):
        await inst.initialize_preinstalled(restart_collector=must_not_start, collector_alive=stopped)
    assert not (inst.drivers_root / "s7").exists()


@pytest.mark.asyncio
async def test_unreviewed_driver_never_reaches_import(real_installer, tmp_path, monkeypatch):
    from tests.test_driver_install import _build_zip
    from unittest.mock import AsyncMock
    preflight = AsyncMock(side_effect=AssertionError("Untrusted code executed"))
    monkeypatch.setattr(real_installer, "_preflight_package", preflight)
    with pytest.raises(DriverInstallError, match="审核"):
        await real_installer.install(_build_zip(tmp_path), restart_collector=must_not_start, collector_alive=stopped)
    preflight.assert_not_awaited()
    assert not (real_installer.drivers_root / "demo_drv").exists()

@pytest.mark.asyncio
async def test_preview_leaves_active_tree_and_process_untouched(real_installer):
    inst = real_installer
    result = await inst.preview(inst.restoration_archive("s7"))
    assert result["preflight_verified"]
    assert not (inst.drivers_root / "s7").exists()
    assert inst._read_state()["drivers"] == {}
    assert list(inst.staging_root.iterdir()) == []


def upgrade_archive(inst, tmp_path):
    import io
    import zipfile
    from data_watch_driver_catalog import package_files
    package = tmp_path / "upgrade/s7"
    shutil.copytree(ROOT / "packages/s7", package, ignore=shutil.ignore_patterns("__pycache__"))
    manifest = package / "driver.toml"
    manifest.write_text(manifest.read_text(encoding="utf-8").replace(f'version = "{S7_VERSION}"', 'version = "2.0.0"'), encoding="utf-8")
    catalog = read_catalog(CATALOG)
    item = next(item for item in catalog["drivers"] if item["id"] == "s7")
    item.update(version="2.0.0", tree_sha256=tree_digest(package))
    path = tmp_path / "trusted/catalog.json"
    path.parent.mkdir()
    path.write_text(json.dumps(catalog), encoding="utf-8")
    inst._catalog_path_override = path
    stream = io.BytesIO()
    with zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as archive:
        for file in package_files(package):
            archive.write(file, "s7/" + file.relative_to(package).as_posix())
    return stream.getvalue(), package


@pytest.mark.asyncio
async def test_upgrade_restart_failure_restores_old_package_and_running_state(real_installer, tmp_path, monkeypatch):
    inst = real_installer
    await inst.install(inst.restoration_archive("s7"), restart_collector=must_not_start, collector_alive=stopped)
    old_digest = tree_digest(inst.drivers_root / "s7")
    archive, package = upgrade_archive(inst, tmp_path)
    running = True
    restarts = 0
    async def alive(): return running
    async def stop():
        nonlocal running
        running = False
    async def restart():
        nonlocal restarts, running
        restarts += 1
        running = restarts > 1
    def fetch(url):
        record = inst.get_driver("s7")
        return {"instance": {"role": "collector", "deployment_role": "centralized", "data_root": str(inst.drivers_root.parent), "boot_id": str(restarts), "build_id": "build", "pid": 7}, "drivers": [{"id": "s7", "version": record["version"], "status": "loaded", "tree_sha256": tree_digest(inst.drivers_root / "s7")}]}
    inst._stop_collector = stop
    inst._collector_pid = lambda: 7 if running else None
    monkeypatch.setattr(inst, "_fetch_json", fetch)
    with pytest.raises(DriverInstallError):
        await inst.install(archive, restart_collector=restart, collector_alive=alive)
    assert restarts == 2 and running
    assert tree_digest(inst.drivers_root / "s7") == old_digest
    assert inst.get_driver("s7")["version"] == S7_VERSION
    assert not inst.recovery_blocked
    assert not inst._latest_incomplete_operations()


@pytest.mark.asyncio
async def test_crash_recovery_keeps_originally_stopped_collector_stopped(real_installer, tmp_path):
    inst = real_installer
    await inst.install(inst.restoration_archive("s7"), restart_collector=must_not_start, collector_alive=stopped)
    previous = inst.get_driver("s7")
    archive, package = upgrade_archive(inst, tmp_path)
    backup = inst.backup_root / "s7" / previous["version"]
    backup.parent.mkdir(parents=True)
    shutil.move(str(inst.drivers_root / "s7"), backup)
    shutil.copytree(package, inst.drivers_root / "s7")
    inst._operation_was_running = False
    inst._persist_operation("crashed-upgrade", "activated", "s7", "2.0.0", previous_record=previous)
    result = await inst.recover_incomplete_operations(restart_collector=must_not_start, collector_alive=stopped)
    assert result[0]["verified"]
    assert inst.get_driver("s7")["version"] == previous["version"]
    assert tree_digest(inst.drivers_root / "s7") == previous["tree_sha256"]
    assert not inst.recovery_blocked

@pytest.mark.asyncio
@pytest.mark.parametrize("action", ["start_process", "stop_process", "restart_process"])
async def test_incomplete_transaction_blocks_manual_process_actions(real_installer, action):
    from types import SimpleNamespace
    from starlette.requests import Request
    from fastapi import HTTPException
    from app.routers import admin
    inst = real_installer
    inst.ensure_layout()
    inst._persist_operation("interrupted", "staging", "s7", "1.0.0")
    request = Request({"type": "http", "app": SimpleNamespace(state=SimpleNamespace(driver_installer=inst))})
    with pytest.raises(HTTPException) as error:
        await getattr(admin, action)("collector", request, _user="admin")
    assert error.value.status_code == 409


@pytest.mark.asyncio
async def test_distributed_center_cannot_mutate_local_drivers(real_installer, monkeypatch):
    monkeypatch.setenv("DATAWATCH_ROLE", "distributed-center")
    with pytest.raises(DriverInstallError, match="目标采集节点"):
        await real_installer.preview(b"not a package")
