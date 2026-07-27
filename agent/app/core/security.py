from __future__ import annotations

import base64
import hashlib
import hmac
from datetime import datetime, timedelta, timezone

from jose import JWTError, jwt

# ── DPAPI（Windows 专用）────────────────────────────────────────────────────

def dpapi_encrypt(plaintext: str) -> str:
    """返回 DPAPI 加密后的 base64 字符串"""
    import win32crypt
    encrypted = win32crypt.CryptProtectData(
        plaintext.encode("utf-8"), None, None, None, None, 0
    )
    return base64.b64encode(encrypted).decode()


def dpapi_decrypt(ciphertext_b64: str) -> str:
    """解密 DPAPI base64 字符串，返回明文；空串直接返回空串"""
    if not ciphertext_b64:
        return ""
    import win32crypt
    data = base64.b64decode(ciphertext_b64)
    # CryptUnprotectData 返回 (description: str, plaintext: bytes)
    _description, plaintext_bytes = win32crypt.CryptUnprotectData(
        data, None, None, None, 0
    )
    return plaintext_bytes.decode("utf-8")


# ── 密码验证（PBKDF2-SHA256，与 backend/app/core/security.py 一致）────────────

PBKDF2_ALGORITHM = "pbkdf2_sha256"
_MAX_PBKDF2_ITERATIONS = 10_000_000


def verify_password(password: str, encoded: str) -> bool:
    """
    验证明文密码是否匹配存储的哈希值。
    哈希格式：pbkdf2_sha256$<iterations>$<salt_hex>$<digest_hex>
    """
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

    if iterations < 1 or iterations > _MAX_PBKDF2_ITERATIONS:
        return False

    candidate = hashlib.pbkdf2_hmac(
        "sha256",
        password.encode("utf-8"),
        salt.encode("utf-8"),
        iterations,
    ).hex()
    return hmac.compare_digest(candidate, digest)


# ── JWT（HS256 令牌）───────────────────────────────────────────────────────────

JWT_ISSUER = "datawatch-agent"
JWT_AUDIENCE = "datawatch-agent"

class InsufficientRoleError(JWTError):
    pass


def create_access_token(
    subject: str,
    secret: str,
    expire_hours: int,
    roles: list[str] | None = None,
) -> str:
    expire = datetime.now(timezone.utc) + timedelta(hours=expire_hours)
    payload = {
        "sub": subject,
        "exp": expire,
        "iss": JWT_ISSUER,
        "aud": JWT_AUDIENCE,
    }
    if roles is not None:
        payload["roles"] = roles
    return jwt.encode(
        payload,
        secret,
        algorithm="HS256",
    )


def verify_token(
    token: str,
    secret: str,
    required_roles: set[str] | None = None,
) -> str:
    """返回 subject；签名无效/过期/缺字段均抛出 JWTError"""
    payload = jwt.decode(
        token,
        secret,
        algorithms=["HS256"],
        issuer=JWT_ISSUER,
        audience=JWT_AUDIENCE,
    )
    if payload.get("iss") != JWT_ISSUER:
        raise JWTError("Token issuer is invalid")
    if payload.get("aud") != JWT_AUDIENCE:
        raise JWTError("Token audience is invalid")
    if "exp" not in payload:
        raise JWTError("Token missing exp")
    subject = payload.get("sub")
    if not isinstance(subject, str) or not subject:
        raise JWTError("Token missing subject")
    if required_roles:
        roles = payload.get("roles")
        token_roles = set(roles) if isinstance(roles, list) else set()
        if not token_roles.intersection(required_roles):
            raise InsufficientRoleError("Token role is not allowed")
    return subject
