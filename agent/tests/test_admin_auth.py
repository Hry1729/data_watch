import pytest
from fastapi import HTTPException
from jose import JWTError, jwt

from app.core.deps import (
    get_current_admin,
    get_current_admin_or_driver_service,
    get_driver_service,
)
from app.core.security import (
    JWT_AUDIENCE,
    JWT_ISSUER,
    create_access_token,
    verify_token,
)
from app.routers.auth import login
from app.schemas.models import LoginRequest


def _config():
    return {
        "agent": {
            "bootstrap_done": True,
            "jwt_secret": "test-secret",
            "jwt_expire_hours": 8,
        }
    }


def test_admin_dependency_requires_admin_role(monkeypatch):
    monkeypatch.setattr("app.core.deps.load_config", _config)
    user_token = create_access_token("user", "test-secret", 8, roles=["R_USER"])
    admin_token = create_access_token("admin", "test-secret", 8, roles=["R_ADMIN"])

    with pytest.raises(HTTPException) as exc_info:
        get_current_admin(f"Bearer {user_token}")
    assert exc_info.value.status_code == 403
    assert get_current_admin(f"Bearer {admin_token}") == "admin"


def test_agent_token_has_fixed_issuer_and_audience():
    token = create_access_token("admin", "test-secret", 8, roles=["R_ADMIN"])
    payload = jwt.decode(
        token,
        "test-secret",
        algorithms=["HS256"],
        issuer=JWT_ISSUER,
        audience=JWT_AUDIENCE,
    )

    assert payload["iss"] == JWT_ISSUER
    assert payload["aud"] == JWT_AUDIENCE
    assert verify_token(token, "test-secret") == "admin"


@pytest.mark.parametrize(
    "claims",
    [
        {"sub": "admin", "exp": 4_102_444_800, "aud": JWT_AUDIENCE},
        {"sub": "admin", "exp": 4_102_444_800, "iss": JWT_ISSUER},
        {
            "sub": "admin",
            "exp": 4_102_444_800,
            "iss": "other-service",
            "aud": JWT_AUDIENCE,
        },
        {
            "sub": "admin",
            "exp": 4_102_444_800,
            "iss": JWT_ISSUER,
            "aud": "other-service",
        },
    ],
)
def test_verify_token_rejects_missing_or_wrong_issuer_audience(claims):
    token = jwt.encode(claims, "test-secret", algorithm="HS256")

    with pytest.raises(JWTError):
        verify_token(token, "test-secret")


def test_driver_service_uses_dedicated_non_jwt_credential(monkeypatch):
    monkeypatch.setenv("DATAWATCH_AGENT_SERVICE_TOKEN", "service-secret")

    assert get_driver_service("service-secret") == "service:backend"
    assert (
        get_current_admin_or_driver_service(None, "service-secret")
        == "service:backend"
    )
    with pytest.raises(HTTPException) as exc_info:
        get_driver_service("wrong")
    assert exc_info.value.status_code == 401


class FakeCursor:
    def __init__(self):
        self.query = ""

    def __enter__(self):
        return self

    def __exit__(self, *_args):
        return False

    def execute(self, query, _params):
        self.query = query

    def fetchone(self):
        if "FROM sys_user " in self.query:
            return {"id": 1, "password_hash": "hash", "status": "1", "deleted": 0}
        return {"role_code": "R_ADMIN"}

    def fetchall(self):
        return [{"role_code": "R_ADMIN"}]


class FakeConnection:
    def cursor(self):
        return FakeCursor()

    def close(self):
        pass


async def test_login_embeds_enabled_role_codes_in_agent_token(monkeypatch):
    captured = {}
    monkeypatch.setattr("app.routers.auth.load_config", _config)
    monkeypatch.setattr("app.routers.auth._get_mysql_conn", lambda _cfg: FakeConnection())
    monkeypatch.setattr("app.routers.auth.verify_password", lambda _plain, _encoded: True)

    def capture_token(**kwargs):
        captured.update(kwargs)
        return "token"

    monkeypatch.setattr("app.routers.auth.create_access_token", capture_token)
    response = await login(LoginRequest(username="admin", password="password"))

    assert response.token == "token"
    assert response.roles == ["R_ADMIN"]
    assert captured["roles"] == ["R_ADMIN"]


async def test_edge_login_uses_center_mysql(monkeypatch):
    cfg = _config()
    opened = {"value": False}
    monkeypatch.setenv("DATAWATCH_ROLE", "edge")
    monkeypatch.setattr("app.routers.auth.load_config", lambda: cfg)

    def connect(_cfg):
        opened["value"] = True
        return FakeConnection()

    monkeypatch.setattr("app.routers.auth._get_mysql_conn", connect)
    monkeypatch.setattr("app.routers.auth.verify_password", lambda _plain, _encoded: True)
    monkeypatch.setattr("app.routers.auth.create_access_token", lambda **_kwargs: "edge-token")

    response = await login(LoginRequest(username="edge-admin", password="password"))

    assert response.token == "edge-token"
    assert response.roles == ["R_ADMIN"]
    assert opened["value"] is True
