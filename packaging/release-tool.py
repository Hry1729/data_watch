from __future__ import annotations

import argparse
import base64
import hashlib
import json
import shutil
from datetime import UTC, datetime
from pathlib import Path

from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding, NoEncryption, PrivateFormat, PublicFormat


def _encode(value: bytes) -> str:
    return base64.b64encode(value).decode("ascii")


def _decode_file(path: Path) -> bytes:
    return base64.b64decode(path.read_text(encoding="ascii").strip(), validate=True)


def _canonical(payload: dict) -> bytes:
    return json.dumps(payload, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")


def generate(private_path: Path, public_path: Path) -> None:
    if private_path.exists() or public_path.exists():
        raise SystemExit("Refusing to overwrite an existing release key")
    private_key = Ed25519PrivateKey.generate()
    private_path.parent.mkdir(parents=True, exist_ok=True)
    public_path.parent.mkdir(parents=True, exist_ok=True)
    private_path.write_text(
        _encode(private_key.private_bytes(Encoding.Raw, PrivateFormat.Raw, NoEncryption())) + "\n",
        encoding="ascii",
    )
    public_path.write_text(
        _encode(private_key.public_key().public_bytes(Encoding.Raw, PublicFormat.Raw)) + "\n",
        encoding="ascii",
    )
    print(public_path)


def publish(args: argparse.Namespace) -> None:
    archive = args.archive.resolve()
    if not archive.is_file():
        raise SystemExit(f"Archive not found: {archive}")

    private_key = Ed25519PrivateKey.from_private_bytes(_decode_file(args.private_key))
    public_bytes = private_key.public_key().public_bytes(Encoding.Raw, PublicFormat.Raw)
    key_id = hashlib.sha256(public_bytes).hexdigest()[:16]
    archive_hash = hashlib.sha256(archive.read_bytes()).hexdigest()
    file_name = archive.name
    download_url = (
        f"/api/cli/releases/{args.version}/download"
        f"?channel={args.channel}&platform={args.platform}"
    )
    payload = {
        "schemaVersion": "1.0",
        "version": args.version,
        "channel": args.channel,
        "platform": args.platform,
        "publishedAt": datetime.now(UTC).isoformat().replace("+00:00", "Z"),
        "apiVersion": args.api_version,
        "minBackendVersion": args.min_backend_version,
        "archive": {
            "fileName": file_name,
            "downloadUrl": download_url,
            "sha256": archive_hash,
            "size": archive.stat().st_size,
        },
    }
    manifest = {
        **payload,
        "signature": {
            "algorithm": "ed25519",
            "keyId": key_id,
            "value": _encode(private_key.sign(_canonical(payload))),
        },
    }

    platform_root = args.release_root / args.channel / args.platform
    version_root = platform_root / args.version
    version_root.mkdir(parents=True, exist_ok=True)
    shutil.copy2(archive, version_root / file_name)
    content = json.dumps(manifest, ensure_ascii=False, indent=2) + "\n"
    (version_root / "manifest.json").write_text(content, encoding="utf-8")
    temporary = platform_root / "latest.json.tmp"
    temporary.write_text(content, encoding="utf-8")
    temporary.replace(platform_root / "latest.json")
    print(platform_root / "latest.json")


def main() -> None:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)
    key_parser = subparsers.add_parser("generate-key")
    key_parser.add_argument("--private", type=Path, required=True)
    key_parser.add_argument("--public", type=Path, required=True)

    publish_parser = subparsers.add_parser("publish")
    publish_parser.add_argument("--archive", type=Path, required=True)
    publish_parser.add_argument("--private-key", type=Path, required=True)
    publish_parser.add_argument("--release-root", type=Path, required=True)
    publish_parser.add_argument("--version", required=True)
    publish_parser.add_argument("--channel", default="stable")
    publish_parser.add_argument("--platform", default="windows-x64")
    publish_parser.add_argument("--api-version", default="1.0")
    publish_parser.add_argument("--min-backend-version", default="0.1.0")

    args = parser.parse_args()
    if args.command == "generate-key":
        generate(args.private, args.public)
    else:
        publish(args)


if __name__ == "__main__":
    main()
