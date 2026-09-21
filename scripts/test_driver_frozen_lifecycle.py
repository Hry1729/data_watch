"""Smoke-test built Agent + Collector using a fresh, isolated installation.
Run with agent/.venv-pack/Scripts/python.exe, --agent and --collector pointing to built EXEs.
"""
from pathlib import Path
import argparse
import base64
import io
import zipfile
import json
import os
import secrets
import shutil
import socket
import subprocess
import sys
import time
import urllib.request
import uuid


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--agent", type=Path, required=True)
    parser.add_argument("--collector", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=Path("outputs/driver-lifecycle-build"))
    args = parser.parse_args()
    root = (args.output / ("smoke-" + uuid.uuid4().hex[:8])).resolve()
    root.mkdir(parents=True)
    role = "edge" if "edge" in args.collector.name else "centralized"
    install = root / "install"
    destination = install / ("edge-collector" if role == "edge" else "collector")
    shutil.copytree(args.collector.resolve().parent, destination)
    # Configure an ephemeral test publisher only in this isolated installation.
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
    from cryptography.hazmat.primitives.serialization import Encoding, PrivateFormat, NoEncryption
    package = root / "signed_probe"
    module = package / "signed_probe"
    module.mkdir(parents=True)
    (module / "__init__.py").write_text("", encoding="utf-8")
    (module / "driver.py").write_text("from drivers.base import BaseDriver\nclass Driver(BaseDriver):\n    async def connect(self, config): raise AssertionError('must not connect')\n    async def disconnect(self): pass\n    async def read(self, blocks): return {}\ndef create_driver(context): return Driver()\n", encoding="utf-8")
    fields = {"manifest_version": 1, "driver_api": "1.0", "id": "signed_probe", "protocol": "signed_probe", "name": "Signed Test", "description": "Isolated smoke test", "version": "1.0.0", "entry": "signed_probe.driver:create_driver", "python_abi": "cp314t", "os": ["windows"], "architectures": ["x86_64"], "capabilities": ["poll", "read"], "read_write": "只读", "publisher": "smoke-test-only"}
    (package / "driver.toml").write_text("\n".join(key + " = " + json.dumps(value, ensure_ascii=False) for key, value in fields.items()), encoding="utf-8")
    private = root / "test-only.key"
    private.write_text(base64.b64encode(Ed25519PrivateKey.generate().private_bytes(Encoding.Raw, PrivateFormat.Raw, NoEncryption())).decode(), encoding="ascii")
    signed = subprocess.run([sys.executable, str(Path(__file__).resolve().parents[1] / "packaging/sign-driver.py"), "--package", str(package), "--private-key", str(private)], capture_output=True, text=True, check=True)
    catalog_path = destination / "driver-catalog/catalog.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    catalog["trusted_publishers"] = [json.loads(signed.stdout)]
    catalog_path.write_text(json.dumps(catalog), encoding="utf-8")
    zipped = io.BytesIO()
    with zipfile.ZipFile(zipped, "w", zipfile.ZIP_DEFLATED) as archive:
        for file in package.rglob("*"):
            if file.is_file(): archive.write(file, "signed_probe/" + file.relative_to(package).as_posix())
    signed_body = {"package_base64": base64.b64encode(zipped.getvalue()).decode()}
    with socket.socket() as socket_probe:
        socket_probe.bind(("127.0.0.1", 0))
        port = socket_probe.getsockname()[1]
    service = secrets.token_hex(32)
    env = {**os.environ, "DATAWATCH_INSTALL_ROOT": str(install), "DATAWATCH_DATA_ROOT": str(root / "data"),
           "DATAWATCH_ROLE": role, "DATAWATCH_AGENT_PORT": str(port), "DATAWATCH_AGENT_SERVICE_TOKEN": service,
           "PYTHONIOENCODING": "utf-8"}
    # Use only the freshly generated test secret; never read the user's installation.
    os.environ.update({key: value for key, value in env.items() if key.startswith("DATAWATCH_")})
    sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "agent"))
    from app.core.config import load_config
    from app.core.security import create_access_token
    config = load_config()
    token = create_access_token("smoke-admin", config["agent"]["jwt_secret"], 1, ["R_ADMIN"])
    base = f"http://127.0.0.1:{port}"
    def request(method, path, body=None, admin=True):
        headers = {"Content-Type": "application/json"}
        headers.update({"Authorization": "Bearer " + token} if admin else {"X-DataWatch-Service-Token": service})
        data = None if body is None else json.dumps(body).encode()
        with urllib.request.urlopen(urllib.request.Request(base + path, data=data, headers=headers, method=method), timeout=120) as response:
            return json.load(response)
    def start(log):
        process = subprocess.Popen([str(args.agent.resolve())], env=env, stdout=log, stderr=subprocess.STDOUT, creationflags=subprocess.CREATE_NO_WINDOW)
        deadline = time.monotonic() + 120
        while time.monotonic() < deadline:
            if process.poll() is not None:
                raise RuntimeError("Agent exited; inspect " + str(root / "agent.log"))
            try:
                result = request("GET", "/admin/drivers")
                return process, result
            except OSError:
                time.sleep(0.25)
        process.terminate()
        process.wait(timeout=15)
        raise TimeoutError("Agent did not become ready")
    process = None
    try:
        with (root / "agent.log").open("wb") as log:
            process, result = start(log)
            assert not result["recovery"]["blocked"], result
            assert len(result["drivers"]) == 5
            admin_token = token
            token = create_access_token("smoke-viewer", config["agent"]["jwt_secret"], 1, ["R_USER"])
            assert len(request("GET", "/admin/drivers")["drivers"]) == 5
            try:
                request("POST", "/admin/drivers/preview", {"restore_id": "s7"})
                raise AssertionError("Viewer was allowed to mutate drivers")
            except urllib.error.HTTPError as error:
                assert error.code == 403
            finally:
                token = admin_token
            assert all(item["install_status"] == "installed" and item["runtime_status"] == "collector_stopped" for item in result["drivers"]), result
            preview = request("POST", "/admin/drivers/preview", {"restore_id": "s7"})
            assert preview["driver"]["preflight_verified"]
            request("DELETE", "/admin/drivers/s7", admin=False)
            process.terminate(); process.wait(timeout=15); process = None
            process, result = start(log)
            removed = next(item for item in result["drivers"] if item["id"] == "s7")
            assert removed["install_status"] == "not_installed", removed
            restored = request("POST", "/admin/drivers/s7/restore")
            assert restored["driver"]["status"] == "installed", restored
            state = json.loads((root / "data/drivers/state.json").read_text(encoding="utf-8"))
            assert "s7" not in state["removed"]
            assert state["preinstall_completed"]
            signed_preview = request("POST", "/admin/drivers/preview", signed_body)
            assert signed_preview["driver"]["preflight_verified"]
            signed_install = request("POST", "/admin/drivers", signed_body)
            assert signed_install["driver"]["signature_verified"]
            assert signed_install["driver"]["status"] == "installed"
            assert not list((root / "data/runtime/pids").glob("*collector*"))
        report = {"passed": True, "role": role, "checks": ["five-package first install", "viewer read-only permissions", "stopped stays stopped", "preview", "official uninstall", "Agent restart preserves removal", "official restore", "signed third-party preview and install"], "root": str(root)}
        (root / "report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
        print(json.dumps(report))
    finally:
        if process is not None and process.poll() is None:
            process.terminate(); process.wait(timeout=15)

if __name__ == "__main__":
    main()
