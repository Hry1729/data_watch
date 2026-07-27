"""驱动状态探针与 Registry.list_loaded 测试。"""

from __future__ import annotations

import json
import socket
import urllib.request

from drivers.registry import DriverRegistry
from monitor.driver_status_server import DriverStatusServer
from tests.test_driver_registry import _write_driver_package


def test_registry_list_loaded_includes_generation(tmp_path):
    drivers_root = _write_driver_package(tmp_path, "probe_drv", "probe_drv")
    registry = DriverRegistry.scan(drivers_root)
    assert registry.generation == 1
    loaded = registry.list_loaded()
    assert len(loaded) == 1
    assert loaded[0]["id"] == "probe_drv"
    assert loaded[0]["version"] == "1.0.0"
    assert loaded[0]["generation"] == 1


def test_driver_status_server_serves_drivers():
    sock = socket.socket()
    sock.bind(("127.0.0.1", 0))
    port = sock.getsockname()[1]
    sock.close()

    server = DriverStatusServer(
        host="127.0.0.1",
        port=port,
        list_drivers=lambda: [
            {"id": "demo", "version": "1.0.0", "generation": 3, "protocol": "demo"}
        ],
        get_generation=lambda: 3,
    )
    server.start()
    try:
        with urllib.request.urlopen(f"http://127.0.0.1:{port}/drivers", timeout=2) as resp:
            payload = json.loads(resp.read().decode("utf-8"))
        assert payload["generation"] == 3
        assert payload["drivers"][0]["id"] == "demo"
    finally:
        server.stop()
