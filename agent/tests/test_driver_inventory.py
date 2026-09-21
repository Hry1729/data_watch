from pathlib import Path
import json
import pytest
from app.services.driver_installer import DriverInstaller
from data_watch_driver_catalog import read_catalog, file_digest, tree_digest

ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT / "packages/driver-catalog/catalog.json"


def test_catalog_archives_match_source():
    data = read_catalog(CATALOG)
    assert len(data["drivers"]) == 5
    for item in data["drivers"]:
        assert file_digest(CATALOG.parent / item["archive"]) == item["sha256"]
        assert tree_digest(ROOT / "packages" / item["id"]) == item["tree_sha256"]


@pytest.mark.parametrize("archive", ["../outside.zip", "C:/outside.zip"])
def test_catalog_rejects_path_escape(tmp_path, archive):
    payload = read_catalog(CATALOG)
    payload["drivers"][0]["archive"] = archive
    path = tmp_path / "catalog.json"
    path.write_text(json.dumps(payload), encoding="utf-8")
    with pytest.raises(ValueError):
        read_catalog(path)


@pytest.mark.asyncio
@pytest.mark.parametrize("status,runtime", [("stopped", "collector_stopped"), ("crashed", "collector_stopped"), ("running", "unknown"), ("unknown", "unknown")])
async def test_offline_catalog_is_not_an_active_install(tmp_path, monkeypatch, status, runtime):
    monkeypatch.setenv("DATAWATCH_ROLE", "centralized")
    installer = DriverInstaller(tmp_path / "drivers", catalog_path=CATALOG)
    async def unavailable():
        assert status not in {"stopped", "crashed"}
        return None
    monkeypatch.setattr(installer, "_probe_collector_drivers", unavailable)
    items = await installer.list_drivers(collector_status=status)
    assert len(items) == 5
    assert all(item["install_status"] == "not_installed" and item["runtime_status"] == runtime for item in items)
    assert all(item["source"] == "official" and not item["protected"] for item in items)


@pytest.mark.asyncio
@pytest.mark.parametrize("field,value", [("role", "writer"), ("deployment_role", "edge"), ("data_root", "C:/wrong"), ("boot_id", "")])
async def test_reject_wrong_instance(tmp_path, monkeypatch, field, value):
    installer = DriverInstaller(tmp_path / "drivers")
    monkeypatch.setenv("DATAWATCH_ROLE", "centralized")
    identity = {"role": "collector", "deployment_role": "centralized", "data_root": str(tmp_path), "boot_id": "boot", "build_id": "build", "pid": 1}
    identity[field] = value
    monkeypatch.setattr(installer, "_fetch_json", lambda url: {"instance": identity, "drivers": [{"id": "s7", "status": "loaded"}]})
    assert await installer._probe_collector_drivers() is None


@pytest.mark.asyncio
async def test_accept_matching_instance(tmp_path, monkeypatch):
    installer = DriverInstaller(tmp_path / "drivers")
    monkeypatch.setenv("DATAWATCH_ROLE", "centralized")
    identity = {"role": "collector", "deployment_role": "centralized", "data_root": str(tmp_path), "boot_id": "boot", "build_id": "build", "pid": 1}
    monkeypatch.setattr(installer, "_fetch_json", lambda url: {"instance": identity, "drivers": [{"id": "s7", "status": "loaded"}]})
    result = await installer._probe_collector_drivers()
    assert result["s7"]["instance"] == identity


def test_distributed_center_has_no_local_bundled_inventory(tmp_path, monkeypatch):
    monkeypatch.setenv("DATAWATCH_ROLE", "distributed-center")
    assert DriverInstaller(tmp_path / "drivers")._bundled_inventory() == []


@pytest.mark.asyncio
async def test_admin_list_passes_process_status_and_pid():
    from types import SimpleNamespace
    from starlette.requests import Request
    from app.routers.admin import list_drivers
    calls = []
    class Installer:
        async def list_drivers(self, **kwargs):
            calls.append(kwargs)
            return []
        def recovery_status(self):
            return {}
    state = SimpleNamespace(driver_installer=Installer(), pm=SimpleNamespace(status=lambda name: SimpleNamespace(status="stopped", pid=None)))
    request = Request({"type": "http", "app": SimpleNamespace(state=state)})
    result = await list_drivers(request, _user="test")
    assert result["drivers"] == []
    assert calls == [{"collector_status": "stopped", "collector_pid": None}]
