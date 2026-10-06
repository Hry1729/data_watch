from pathlib import Path
import base64
import io
import json
import os
import subprocess
import sys
import zipfile
import pytest
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding, PrivateFormat, NoEncryption
from app.services.driver_zip import validate_and_extract, DriverZipError
from app.services.driver_installer import DriverInstaller
from data_watch_driver_catalog import read_catalog, package_files
from tests.test_driver_install import _minimal_driver_toml

ROOT = Path(__file__).resolve().parents[2]

def archive(root):
    output = io.BytesIO()
    with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as target:
        for file in package_files(root):
            target.write(file, root.name + "/" + file.relative_to(root).as_posix())
    return output.getvalue()

@pytest.fixture
def signed_package(tmp_path):
    root = tmp_path / "package/demo_drv"
    module = root / "demo_drv"
    module.mkdir(parents=True)
    (root / "driver.toml").write_text(_minimal_driver_toml() + '\npublisher = "reviewed-company"\n', encoding="utf-8")
    (module / "__init__.py").write_text("", encoding="utf-8")
    (module / "driver.py").write_text('''from drivers.base import BaseDriver
class Driver(BaseDriver):
    async def connect(self, config): raise AssertionError("Preflight must not connect")
    async def disconnect(self): pass
    async def read(self, blocks): return {}
def create_driver(context): return Driver()
''', encoding="utf-8")
    private = tmp_path / "test-only.key"
    key = Ed25519PrivateKey.generate()
    private.write_text(base64.b64encode(key.private_bytes(Encoding.Raw, PrivateFormat.Raw, NoEncryption())).decode(), encoding="ascii")
    signed = subprocess.run([sys.executable, str(ROOT / "packaging/sign-driver.py"), "--package", str(root), "--private-key", str(private)], capture_output=True, text=True, check=True)
    catalog = read_catalog(ROOT / "packages/driver-catalog/catalog.json")
    catalog["trusted_publishers"] = [json.loads(signed.stdout)]
    catalog_path = tmp_path / "catalog.json"
    catalog_path.write_text(json.dumps(catalog), encoding="utf-8")
    return root, catalog_path


def test_signing_tool_and_static_signature_validation(signed_package, tmp_path):
    root, catalog = signed_package
    result = validate_and_extract(archive(root), tmp_path / "staging", catalog_path=catalog)
    assert result.signature_verified
    assert result.publisher == "reviewed-company"


def test_rebuilt_offline_catalog_preserves_signing_trust(signed_package, tmp_path, monkeypatch):
    from types import SimpleNamespace
    import data_watch_driver_manifest
    from data_watch_driver_catalog import OFFICIAL_ALIASES, write_catalog
    from data_watch_driver_signatures import verify_package_signature
    root, catalog_path = signed_package
    manifest = data_watch_driver_manifest.load_driver_toml(root / "driver.toml")
    packages = tmp_path / "sources"
    source_catalog = packages / "driver-catalog/catalog.json"
    source_catalog.parent.mkdir(parents=True)
    trust = read_catalog(catalog_path)
    trust["approved_packages"] = [{"id": "reviewed", "publisher": "reviewed-company", "version": "1.0.0", "tree_sha256": "a" * 64}]
    source_catalog.write_text(json.dumps(trust), encoding="utf-8")
    for driver_id in OFFICIAL_ALIASES:
        package = packages / driver_id
        package.mkdir()
        (package / "driver.toml").write_text("test fixture", encoding="utf-8")
    def load(path):
        driver_id = path.parent.name
        return SimpleNamespace(id=driver_id, protocol=driver_id, name=driver_id, description="fixture", version="1.0.0", publisher="datawatch", aliases=OFFICIAL_ALIASES[driver_id], capabilities=["read"], read_write="只读")
    monkeypatch.setattr(data_watch_driver_manifest, "load_driver_toml", load)
    generated = tmp_path / "build/driver-catalog/catalog.json"
    write_catalog(packages, generated, "0.1.0")
    actual = read_catalog(generated)
    assert actual["trusted_publishers"] == trust["trusted_publishers"]
    assert actual["approved_packages"] == trust["approved_packages"]
    assert verify_package_signature(root, manifest, actual)


@pytest.mark.parametrize("mutation", ["code", "extra_file", "signature", "publisher", "unknown_key"])
def test_rejects_tampered_or_untrusted_signature(signed_package, tmp_path, mutation):
    root, catalog = signed_package
    if mutation == "code":
        (root / "demo_drv/driver.py").write_text("raise RuntimeError('must not execute')", encoding="utf-8")
    elif mutation == "extra_file":
        (root / "unexpected.py").write_text("pass", encoding="utf-8")
    elif mutation == "publisher":
        path = root / "driver.toml"
        path.write_text(path.read_text(encoding="utf-8").replace("reviewed-company", "datawatch"), encoding="utf-8")
    elif mutation == "unknown_key":
        data = json.loads(catalog.read_text(encoding="utf-8")); data["trusted_publishers"] = []
        catalog.write_text(json.dumps(data), encoding="utf-8")
    else:
        path = root / "signature.json"
        data = json.loads(path.read_text(encoding="utf-8")); data["signature"] = base64.b64encode(b"0" * 64).decode()
        path.write_text(json.dumps(data), encoding="utf-8")
    with pytest.raises(DriverZipError, match="签名"):
        validate_and_extract(archive(root), tmp_path / "staging", catalog_path=catalog)


@pytest.mark.asyncio
async def test_signed_package_installs_through_target_runtime(signed_package, tmp_path, monkeypatch):
    root, catalog = signed_package
    monkeypatch.setenv("DATAWATCH_ROLE", "centralized")
    target = Path(os.environ.get("DATAWATCH_TEST_COLLECTOR_EXE") or str(tmp_path / "missing.exe"))
    monkeypatch.setattr("app.core.config.module_paths", lambda name: (target, target.parent))
    monkeypatch.setattr("app.core.config.install_root", lambda: ROOT)
    inst = DriverInstaller(tmp_path / "data/drivers", catalog_path=catalog)
    async def stopped(): return False
    async def no_start(): raise AssertionError("must stay stopped")
    result = await inst.install(archive(root), restart_collector=no_start, collector_alive=stopped)
    assert result.driver["signature_verified"]
    assert result.driver["approved_source"]
    assert result.driver["status"] == "installed"
    assert (inst.drivers_root / "demo_drv/signature.json").is_file()


def test_signed_zip_cannot_smuggle_unsigned_bytecode(signed_package, tmp_path):
    root, catalog = signed_package
    data = io.BytesIO(archive(root))
    with zipfile.ZipFile(data, "a", zipfile.ZIP_DEFLATED) as target:
        target.writestr("demo_drv/demo_drv/__pycache__/driver.cpython-314.pyc", b"untrusted bytecode")
    with pytest.raises(DriverZipError, match="字节码"):
        validate_and_extract(data.getvalue(), tmp_path / "staging", catalog_path=catalog)
