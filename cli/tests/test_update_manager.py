from __future__ import annotations

import base64
import hashlib
import json
import zipfile
from pathlib import Path
from typing import Any

import pytest
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding, PublicFormat

import cli.update_manager as update_module
from cli.errors import CliError
from cli.update_manager import UpdateManager, _safe_extract, _verify_manifest


_RELEASE_BYTES = b"signed-release"


def _signed_manifest(private_key: Ed25519PrivateKey) -> dict[str, Any]:
    public = private_key.public_key().public_bytes(Encoding.Raw, PublicFormat.Raw)
    key_id = hashlib.sha256(public).hexdigest()[:16]
    payload = {
        "schemaVersion": "1.0",
        "version": "9.0.0",
        "channel": "stable",
        "platform": "windows-x64",
        "publishedAt": "2026-08-12T00:00:00Z",
        "apiVersion": "1.0",
        "minBackendVersion": "0.1.0",
        "archive": {
            "fileName": "release.zip",
            "downloadUrl": "/download",
            "sha256": hashlib.sha256(_RELEASE_BYTES).hexdigest(),
            "size": len(_RELEASE_BYTES),
        },
    }
    canonical = json.dumps(payload, sort_keys=True, separators=(",", ":")).encode()
    update_module.TRUSTED_RELEASE_KEYS = {key_id: base64.b64encode(public).decode()}
    return {
        **payload,
        "signature": {
            "algorithm": "ed25519",
            "keyId": key_id,
            "value": base64.b64encode(private_key.sign(canonical)).decode(),
        },
    }


def test_signed_manifest_is_verified_and_compared() -> None:
    manifest = _signed_manifest(Ed25519PrivateKey.generate())

    class Client:
        def request(self, *_args: Any, **_kwargs: Any) -> dict[str, Any]:
            return manifest

    result = UpdateManager(Client(), install_root=Path("unused")).check()

    assert result["updateAvailable"] is True
    assert result["latestVersion"] == "9.0.0"


def test_tampered_manifest_is_rejected() -> None:
    manifest = _signed_manifest(Ed25519PrivateKey.generate())
    manifest["version"] = "10.0.0"

    with pytest.raises(CliError) as captured:
        _verify_manifest(manifest, channel="stable")

    assert captured.value.error_type == "update_signature_error"


def test_zip_path_traversal_is_rejected(tmp_path: Path) -> None:
    archive = tmp_path / "bad.zip"
    with zipfile.ZipFile(archive, "w") as bundle:
        bundle.writestr("../outside.txt", "bad")

    with pytest.raises(CliError):
        _safe_extract(archive, tmp_path / "extract")

def test_download_writes_and_verifies_signed_archive(tmp_path: Path) -> None:
    manifest = _signed_manifest(Ed25519PrivateKey.generate())

    class Client:
        def request(self, *_args: Any, **_kwargs: Any) -> dict[str, Any]:
            return manifest

        def download_to(self, _path: str, output: Path, **_kwargs: Any) -> None:
            Path(output).write_bytes(_RELEASE_BYTES)

    result = UpdateManager(Client(), install_root=tmp_path).download()

    assert Path(result["downloadPath"]).read_bytes() == _RELEASE_BYTES
    assert result["downloaded"] is True
