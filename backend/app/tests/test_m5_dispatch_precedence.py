"""M5 回归：自研 ASGI 分发器的参数优先级。

修复前：``call_args.update(body_values)`` 让请求体顶层键覆盖路径参数和 ``request`` 对象，
query 中的 ``payload`` 会把 dict 参数变成字符串；登录接口可以从 query 取密码。
修复后：路径参数、``request``、请求头参数、依赖注入参数不能被 body / query 覆盖；
dict 形式的请求体参数只从 body 取值；登录拒绝 query 中携带密码。
"""

from __future__ import annotations

import json
from typing import Any

import pytest

from app.api.routes import auth as auth_routes


async def _call(app, method: str, path: str, *, query: bytes = b"", body: bytes = b"") -> tuple[int, Any]:
    messages: list[dict] = []

    async def receive():
        return {"type": "http.request", "body": body, "more_body": False}

    async def send(message):
        messages.append(message)

    headers = [(b"content-type", b"application/json")] if body else []
    await app._dispatch_asgi(
        {
            "type": "http",
            "method": method,
            "path": path,
            "query_string": query,
            "client": ("127.0.0.1", 1),
            "headers": headers,
        },
        receive,
        send,
    )
    status = next(m["status"] for m in messages if m["type"] == "http.response.start")
    raw = b"".join(m.get("body", b"") for m in messages if m["type"] == "http.response.body")
    return status, json.loads(raw) if raw else None


def _app():
    from app.main import EnhancedASGITurboAPI

    app = EnhancedASGITurboAPI(title="m5 dispatch test")

    @app.patch("/nodes/{node_uuid}/status")
    async def node_status(node_uuid: str, payload: dict[str, Any], request):
        return {"node": node_uuid, "payload": payload, "path": request.path}

    return app


@pytest.mark.asyncio
async def test_body_cannot_override_path_param_or_request() -> None:
    status, data = await _call(
        _app(),
        "PATCH",
        "/nodes/X/status",
        body=json.dumps({"node_uuid": "Y", "request": "boom", "status": "revoked"}).encode(),
    )
    assert status == 200
    assert data["node"] == "X"
    assert data["path"] == "/nodes/X/status"
    assert data["payload"]["status"] == "revoked"


@pytest.mark.asyncio
async def test_query_cannot_override_path_param_or_fill_body_dict() -> None:
    status, data = await _call(
        _app(),
        "PATCH",
        "/nodes/X/status",
        query=b"node_uuid=Y&payload=x",
        body=json.dumps({"status": "approved"}).encode(),
    )
    assert status == 200
    assert data["node"] == "X"
    assert data["payload"] == {"status": "approved"}


@pytest.mark.asyncio
async def test_login_rejects_password_in_query(monkeypatch) -> None:
    from app.main import EnhancedASGITurboAPI

    called = []

    class _Service:
        def __init__(self, _session):
            pass

        async def login(self, **kwargs):
            called.append(kwargs)
            return {"token": "t"}

    class _Session:
        async def __aenter__(self):
            return object()

        async def __aexit__(self, *_exc):
            return False

    monkeypatch.setattr(auth_routes, "AuthService", _Service)
    monkeypatch.setattr(auth_routes, "get_mysql_session_factory", lambda: _Session)
    from app.core.exceptions import register_exception_handlers

    app = EnhancedASGITurboAPI(title="m5 login test")
    register_exception_handlers(app)
    app.include_router(auth_routes.router)

    # 业务错误按约定为 HTTP 200 + code
    status, data = await _call(app, "POST", "/api/auth/login", query=b"userName=Admin&password=secret")
    assert data["code"] == 400
    status, data = await _call(
        app,
        "POST",
        "/api/auth/login",
        query=b"password=secret",
        body=json.dumps({"userName": "Admin", "password": "secret"}).encode(),
    )
    assert data["code"] == 400
    assert called == []

    status, data = await _call(
        app,
        "POST",
        "/api/auth/login",
        body=json.dumps({"userName": "Admin", "password": "secret"}).encode(),
    )
    assert status == 200 and data["code"] == 200
    assert called and called[0]["password"] == "secret"
