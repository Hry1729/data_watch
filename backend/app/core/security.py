from __future__ import annotations

import base64
import hashlib
import hmac
import json
import secrets
import string
import time
from typing import Any

PBKDF2_ALGORITHM = "pbkdf2_sha256"
PBKDF2_ITERATIONS = 600000
TEMPORARY_PASSWORD_ALPHABET = string.ascii_letters + string.digits + "-_"


def _base64url_encode(raw: bytes) -> str:
    return base64.urlsafe_b64encode(raw).rstrip(b"=").decode("ascii")


def _base64url_decode(raw: str) -> bytes:
    padding = "=" * (-len(raw) % 4)
    return base64.urlsafe_b64decode(f"{raw}{padding}")


def hash_password(password: str, *, iterations: int = PBKDF2_ITERATIONS) -> str:
    salt = secrets.token_hex(16)
    digest = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt.encode("utf-8"), iterations)
    return f"{PBKDF2_ALGORITHM}${iterations}${salt}${digest.hex()}"


def verify_password(password: str, encoded: str) -> bool:
    try:
        algorithm, iterations_raw, salt, digest = encoded.split("$", 3)
    except ValueError:
        return False

    if algorithm != PBKDF2_ALGORITHM:
        return False

    try:
        iterations = int(iterations_raw)
    except ValueError:
        return False

    candidate = hashlib.pbkdf2_hmac(
        "sha256",
        password.encode("utf-8"),
        salt.encode("utf-8"),
        iterations,
    ).hex()
    return hmac.compare_digest(candidate, digest)


def generate_temporary_password(length: int = 24) -> str:
    """Generate a high-entropy password intended for one-time delivery."""
    if length < 16:
        raise ValueError("temporary password length must be at least 16")
    return "".join(secrets.choice(TEMPORARY_PASSWORD_ALPHABET) for _ in range(length))


def create_signed_token(
    payload: dict[str, Any],
    *,
    secret_key: str,
    expires_in_seconds: int,
    token_type: str,
) -> str:
    issued_at = int(time.time())
    claims = {
        **payload,
        "type": token_type,
        "iat": issued_at,
        "exp": issued_at + expires_in_seconds,
    }
    header = {"alg": "HS256", "typ": "JWT"}
    header_segment = _base64url_encode(json.dumps(header, separators=(",", ":")).encode("utf-8"))
    payload_segment = _base64url_encode(json.dumps(claims, separators=(",", ":")).encode("utf-8"))
    signing_input = f"{header_segment}.{payload_segment}".encode("ascii")
    signature = hmac.new(secret_key.encode("utf-8"), signing_input, hashlib.sha256).digest()
    return f"Bearer {header_segment}.{payload_segment}.{_base64url_encode(signature)}"


def decode_signed_token(raw_token: str, *, secret_key: str) -> dict[str, Any]:
    token = normalize_bearer_token(raw_token)
    parts = token.split(".")
    if len(parts) != 3:
        raise ValueError("Invalid token format")

    header_segment, payload_segment, signature_segment = parts
    signing_input = f"{header_segment}.{payload_segment}".encode("ascii")
    expected_signature = hmac.new(secret_key.encode("utf-8"), signing_input, hashlib.sha256).digest()
    actual_signature = _base64url_decode(signature_segment)
    if not hmac.compare_digest(expected_signature, actual_signature):
        raise ValueError("Invalid token signature")

    payload = json.loads(_base64url_decode(payload_segment).decode("utf-8"))
    expires_at = int(payload.get("exp", 0))
    if expires_at <= int(time.time()):
        raise ValueError("Token expired")
    return payload


def normalize_bearer_token(raw_token: str | None) -> str:
    if raw_token is None:
        raise ValueError("Missing token")

    normalized = raw_token.strip()
    if not normalized:
        raise ValueError("Missing token")

    if normalized.lower().startswith("bearer "):
        return normalized.split(" ", 1)[1].strip()
    return normalized
