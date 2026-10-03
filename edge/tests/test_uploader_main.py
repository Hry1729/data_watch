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
    # P1-C：锁路径必须与 outbox 库**同源**。本用例刻意让 DATAWATCH_EDGE_DB 与
    # DATAWATCH_DATA_ROOT 分指；原断言写成 `tmp_path / "edge" / "uploader.lock"`（即锁只由
    # DATAWATCH_DATA_ROOT 派生），等于把「同库不同锁」这个缺陷固化成了期望——分指时两个实例
    # 各持一把锁可同时运行，后启动者会重置前者未过期的 live 租约。现改为断言锁随库走。
    assert edge_database_path() == tmp_path / "custom.db"
    assert settings.lock_path == edge_database_path().parent / "uploader.lock"
    assert settings.lock_path == tmp_path / "uploader.lock"
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
