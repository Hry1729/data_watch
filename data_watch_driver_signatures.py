"""Ed25519 driver signatures. Used by Agent before importing any package code."""
from __future__ import annotations
import base64
import hashlib
import json
from pathlib import Path
from data_watch_driver_catalog import package_files, file_digest, tree_digest

SIGNATURE_FILE = "signature.json"


def signed_payload(root: Path, manifest) -> dict:
    tree_digest(root)  # Reject links before reading the file map.
    return {"id": manifest.id, "publisher": manifest.publisher, "version": manifest.version,
            "files": {path.relative_to(root).as_posix(): file_digest(path)
                      for path in package_files(root) if path.relative_to(root).as_posix() != SIGNATURE_FILE}}


def canonical(payload: dict) -> bytes:
    return json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def verify_package_signature(root: Path, manifest, catalog: dict | None) -> bool:
    path = root / SIGNATURE_FILE
    if not path.exists():
        return False
    from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PublicKey
    from cryptography.exceptions import InvalidSignature
    try:
        if path.stat().st_size > 2 * 1024 * 1024:
            raise ValueError("签名清单过大")
        signature = json.loads(path.read_text(encoding="utf-8"))
        if not isinstance(signature, dict) or signature.get("algorithm") != "ed25519":
            raise ValueError("不支持的签名格式")
        if not manifest.publisher:
            raise ValueError("已签名包必须声明发布者")
        keys = (catalog or {}).get("trusted_publishers", [])
        trusted = next((key for key in keys if key.get("publisher") == manifest.publisher and key.get("key_id") == signature.get("key_id")), None)
        if trusted is None:
            raise ValueError("发布者签名密钥不受信任")
        public = base64.b64decode(trusted["public_key"], validate=True)
        if hashlib.sha256(public).hexdigest()[:16] != signature["key_id"]:
            raise ValueError("可信公钥标识不匹配")
        payload = signed_payload(root, manifest)
        if signature.get("payload") != payload:
            raise ValueError("签名文件清单与包内容不一致")
        Ed25519PublicKey.from_public_bytes(public).verify(base64.b64decode(signature["signature"], validate=True), canonical(payload))
        return True
    except (OSError, KeyError, TypeError, ValueError, InvalidSignature) as exc:
        raise ValueError("驱动发布者签名校验失败: " + (str(exc) or "签名无效")) from exc
