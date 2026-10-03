from __future__ import annotations

import base64
import hashlib
import hmac
import secrets
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
PBKDF2_ITERATIONS = 600_000


def hash_password(password: str) -> str:
    if not password:
        raise ValueError("password must not be empty")
    salt = secrets.token_hex(16)
    digest = hashlib.pbkdf2_hmac(
        "sha256",
        password.encode("utf-8"),
        salt.encode("utf-8"),
        PBKDF2_ITERATIONS,
    ).hex()
    return "$".join((PBKDF2_ALGORITHM, str(PBKDF2_ITERATIONS), salt, digest))
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

# 空密钥可让任何本地进程伪造管理员令牌（HS256 用空串照签照验）。32 字符下限与
# config.toml.example 声明的「32 字节 hex」一致。
JWT_SECRET_MIN_LENGTH = 32


def validate_jwt_secret(secret: object, min_length: int = JWT_SECRET_MIN_LENGTH) -> str:
    """校验 JWT 密钥非空且达到最小长度；不合格抛 ValueError。

    默认下限 32 由配置加载（app.core.config）使用；签发/校验路径只要求非空，
    见 _require_jwt_secret 的说明。
    """
    if not isinstance(secret, str):
        raise ValueError("jwt_secret must be a string")
    if len(secret.strip()) < min_length:
        raise ValueError(f"jwt_secret must be at least {min_length} characters")
    return secret


def _require_jwt_secret(secret: object) -> str:
    """签发/校验前的硬闸门：空密钥即拒绝，绝不降级为「无签名校验」。

    这里只拦空值（P0：空密钥可伪造任意令牌）。长度下限属配置策略，由
    app.core.config._ensure_jwt_secret_unlocked 在加载既有配置时强制执行。
    """
    try:
        return validate_jwt_secret(secret, min_length=1)
    except ValueError as exc:
        raise JWTError(str(exc)) from exc


class InsufficientRoleError(JWTError):
    pass


def create_access_token(
    subject: str,
    secret: str,
    expire_hours: int,
    roles: list[str] | None = None,
) -> str:
    secret = _require_jwt_secret(secret)
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
    secret = _require_jwt_secret(secret)
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
