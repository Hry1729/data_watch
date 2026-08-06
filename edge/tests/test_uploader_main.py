from __future__ import annotations

import base64

import pytest
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import (
    Encoding,
    NoEncryption,
    PrivateFormat,
)

from edge.app.paths import edge_database_path
from edge.uploader_main import settings_from_env


def _private_key_pem() -> bytes:
    return Ed25519PrivateKey.generate().private_bytes(
        Encoding.PEM,
        PrivateFormat.PKCS8,
        NoEncryption(),
    )


def test_uploader_settings_are_loaded_from_agent_environment(monkeypatch, tmp_path):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    monkeypatch.setenv("DATAWATCH_EDGE_DB", str(tmp_path / "custom.db"))
    monkeypatch.setenv("DATAWATCH_CENTER_URL", "https://center.test/")
    monkeypatch.setenv("DATAWATCH_NODE_UUID", "node-1")
    monkeypatch.setenv(
        "DATAWATCH_UPLOADER_PRIVATE_KEY_B64",
        base64.b64encode(_private_key_pem()).decode("ascii"),
    )

    settings, signer = settings_from_env()

    assert settings.center_url == "https://center.test/"
    assert settings.node_uuid == "node-1"
    assert settings.lock_path == tmp_path / "edge" / "uploader.lock"
    assert edge_database_path() == tmp_path / "custom.db"
    assert signer.headers("GET", "/ingest/session/node-1", {})[
        "x-datawatch-signature"
    ]


def test_uploader_settings_require_agent_identity(monkeypatch):
    for name in (
        "DATAWATCH_CENTER_URL",
        "DATAWATCH_NODE_UUID",
        "DATAWATCH_UPLOADER_PRIVATE_KEY_B64",
    ):
        monkeypatch.delenv(name, raising=False)

    with pytest.raises(RuntimeError, match="DATAWATCH_CENTER_URL"):
        settings_from_env()
