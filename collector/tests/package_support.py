"""Source-package fixtures for protocol unit tests, independent of production discovery."""
from functools import lru_cache
from pathlib import Path
from device.device import Device
from drivers.registry import DriverRegistry

@lru_cache(maxsize=1)
def source_registry():
    return DriverRegistry.scan(Path(__file__).resolve().parents[2] / "packages")

class PackageDevice(Device):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.external_driver_hooks = source_registry().get(self.protocol)

    def _parse_s7_address(self, address):
        from packages.s7.s7.planner import Planner
        return Planner(self.connection_config)._parse_s7_address(address)

    def _parse_ge_address(self, address):
        from packages.ge_srtp.ge_srtp.planner import Planner
        return Planner(self.connection_config)._parse_ge_address(address)

    def _parse_address(self, address):
        return self._parse_ge_address(address) if self.protocol == "ge_srtp" else self._parse_s7_address(address)
