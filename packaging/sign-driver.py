"""Sign a reviewed driver directory with an existing Ed25519 release private key."""
from pathlib import Path
import argparse
import base64
import hashlib
import json
import sys
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from cryptography.hazmat.primitives.asymmetric.ed25519 import Ed25519PrivateKey
from cryptography.hazmat.primitives.serialization import Encoding, PublicFormat
from data_watch_driver_manifest import load_driver_toml
from data_watch_driver_signatures import signed_payload, canonical, SIGNATURE_FILE


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--package", type=Path, required=True)
    parser.add_argument("--private-key", type=Path, required=True, help="base64 raw Ed25519 key; compatible with release-tool.py")
    args = parser.parse_args()
    manifest = load_driver_toml(args.package / "driver.toml")
    if not manifest.publisher:
        parser.error("Package must declare publisher")
    key = Ed25519PrivateKey.from_private_bytes(base64.b64decode(args.private_key.read_text(encoding="ascii").strip(), validate=True))
    public = key.public_key().public_bytes(Encoding.Raw, PublicFormat.Raw)
    key_id = hashlib.sha256(public).hexdigest()[:16]
    payload = signed_payload(args.package, manifest)
    record = {"algorithm": "ed25519", "key_id": key_id, "payload": payload,
              "signature": base64.b64encode(key.sign(canonical(payload))).decode("ascii")}
    (args.package / SIGNATURE_FILE).write_text(json.dumps(record, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"publisher": manifest.publisher, "key_id": key_id, "public_key": base64.b64encode(public).decode("ascii")}))

if __name__ == "__main__":
    main()
