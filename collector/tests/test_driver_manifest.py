"""driver.toml 清单解析与校验测试"""

import textwrap
from pathlib import Path

import pytest

from drivers.manifest import DriverManifestError, load_driver_toml


def _write_toml(tmp_path: Path, content: str) -> Path:
    path = tmp_path / "driver.toml"
    path.write_text(textwrap.dedent(content).strip() + "\n", encoding="utf-8")
    return path


def _valid_toml(**overrides) -> str:
    base = {
        "manifest_version": "1",
        'driver_api': '"1.0"',
        'id': '"modbus_tcp"',
        'protocol': '"modbus_tcp"',
        'name': '"Modbus TCP"',
        'description': '"Modbus TCP 只读驱动"',
        'version': '"1.0.0"',
        'entry': '"modbus_tcp.driver:create_driver"',
        'python_abi': '"cp314t"',
        "os": '["windows"]',
        "architectures": '["x86_64"]',
        "capabilities": '["poll", "read", "batch_contiguous"]',
        'read_write': '"只读"',
    }
    base.update(overrides)
    lines = [f"{k} = {v}" for k, v in base.items()]
    return "\n".join(lines)


def test_load_valid_manifest(tmp_path):
    path = _write_toml(tmp_path, _valid_toml())
    manifest = load_driver_toml(path)
    assert manifest.manifest_version == 1
    assert manifest.driver_api == "1.0"
    assert manifest.id == "modbus_tcp"
    assert manifest.protocol == "modbus_tcp"
    assert manifest.entry == "modbus_tcp.driver:create_driver"
    assert manifest.capabilities == ("poll", "read", "batch_contiguous")
    assert manifest.read_write == "只读"


def test_missing_required_field_raises(tmp_path):
    content = _valid_toml()
    content = content.replace('version = "1.0.0"\n', "")
    path = _write_toml(tmp_path, content)
    with pytest.raises(DriverManifestError, match="version"):
        load_driver_toml(path)


def test_invalid_id_raises(tmp_path):
    path = _write_toml(tmp_path, _valid_toml(id="Modbus_TCP"))
    with pytest.raises(DriverManifestError, match="id"):
        load_driver_toml(path)


def test_unknown_capability_raises(tmp_path):
    path = _write_toml(
        tmp_path,
        _valid_toml(capabilities='["poll", "unknown_cap"]'),
    )
    with pytest.raises(DriverManifestError, match="capabilities"):
        load_driver_toml(path)


def test_readonly_cannot_declare_write(tmp_path):
    path = _write_toml(
        tmp_path,
        _valid_toml(capabilities='["poll", "read", "write"]'),
    )
    with pytest.raises(DriverManifestError, match="只读"):
        load_driver_toml(path)


def test_duplicate_capability_raises(tmp_path):
    path = _write_toml(
        tmp_path,
        _valid_toml(capabilities='["poll", "read", "poll"]'),
    )
    with pytest.raises(DriverManifestError, match="重复"):
        load_driver_toml(path)


def test_invalid_semver_raises(tmp_path):
    path = _write_toml(tmp_path, _valid_toml(version='"1.0"'))
    with pytest.raises(DriverManifestError, match="SemVer"):
        load_driver_toml(path)


@pytest.mark.parametrize(
    ("field", "value", "message"),
    [
        ("protocol", '"' + "p" * 21 + '"', "protocol 最大长度为 20"),
        ("description", '"' + "d" * 256 + '"', "description 最大长度为 255"),
        ("version", '"' + "1" * 47 + '.0.0"', "version 最大长度为 50"),
    ],
)
def test_manifest_rejects_values_exceeding_shared_limits(tmp_path, field, value, message):
    path = _write_toml(tmp_path, _valid_toml(**{field: value}))
    with pytest.raises(DriverManifestError, match=message):
        load_driver_toml(path)


def test_wrong_manifest_version_raises(tmp_path):
    path = _write_toml(tmp_path, _valid_toml(manifest_version=2))
    with pytest.raises(DriverManifestError, match="manifest_version"):
        load_driver_toml(path)
