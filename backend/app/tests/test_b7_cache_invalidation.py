"""B7｜控制面成功提交后必须统一发布 Ingest 控制缓存失效。

证据等级：单元（路由函数直调 + monkeypatch，不连接 MySQL / Redis）。
"""

from __future__ import annotations

import json
from typing import Any
from unittest.mock import AsyncMock

import pytest

from app.api.routes import catalog as catalog_routes
from app.api.routes import distributed_nodes
from app.core.exceptions import ApiError


class _FakeSession:
    async def __aenter__(self) -> "_FakeSession":
        return self

    async def __aexit__(self, *exc_info: object) -> bool:
        return False


def _session_factory_provider():
    # 路由内的调用序列是 get_mysql_session_factory() -> session_factory() -> async with
    return _FakeSession


def _install_fakes(
    monkeypatch: pytest.MonkeyPatch,
    service_calls: list[str],
    published: list[dict[str, Any]],
) -> None:
    class FakeCatalogService:
        def __init__(self, *, session: Any, tdengine: Any) -> None:
            pass

        def __getattr__(self, name: str):
            async def call(*args: Any, **kwargs: Any) -> dict[str, int]:
                service_calls.append(name)
                return {}

            return call

    async def fake_publish(payload: dict[str, Any]) -> None:
        published.append(payload)

    monkeypatch.setattr(catalog_routes, "CatalogService", FakeCatalogService)
    monkeypatch.setattr(catalog_routes, "get_tdengine_client", lambda: object())
    monkeypatch.setattr(
        catalog_routes, "get_mysql_session_factory", _session_factory_provider
    )
    monkeypatch.setattr(
        catalog_routes,
        "require_permissions",
        AsyncMock(return_value={"user_name": "admin"}),
    )
    monkeypatch.setattr(
        catalog_routes, "_publish_control_invalidation", fake_publish
    )


_ROUTE_CALLS = {
    "import_devices": lambda: catalog_routes.import_devices(
        {"rows": []}, credentials=None
    ),
    "create_device": lambda: catalog_routes.create_device({}, credentials=None),
    "update_device": lambda: catalog_routes.update_device(7, {}, credentials=None),
    "update_device_status": lambda: catalog_routes.update_device_status(
        7, {"enabled": True}, credentials=None
    ),
    "batch_delete_devices": lambda: catalog_routes.batch_delete_devices(
        {"ids": [7]}, credentials=None
    ),
    "delete_device": lambda: catalog_routes.delete_device(7, credentials=None),
    "import_points": lambda: catalog_routes.import_points(
        {"rows": []}, credentials=None
    ),
    "import_points_from_template": lambda: catalog_routes.import_points_from_template(
        {"deviceId": 7}, credentials=None
    ),
    "create_point": lambda: catalog_routes.create_point({}, credentials=None),
    "update_point": lambda: catalog_routes.update_point(7, {}, credentials=None),
    "update_point_status": lambda: catalog_routes.update_point_status(
        7, {"enabled": True}, credentials=None
    ),
    "batch_delete_points": lambda: catalog_routes.batch_delete_points(
        {"ids": [7]}, credentials=None
    ),
    "delete_point": lambda: catalog_routes.delete_point(7, credentials=None),
}


@pytest.mark.asyncio
@pytest.mark.parametrize("route_name", sorted(_ROUTE_CALLS))
async def test_catalog_mutation_publishes_both_invalidation_scopes(
    monkeypatch: pytest.MonkeyPatch, route_name: str
) -> None:
    service_calls: list[str] = []
    published: list[dict[str, Any]] = []
    _install_fakes(monkeypatch, service_calls, published)

    response = await _ROUTE_CALLS[route_name]()

    assert response is not None
    assert service_calls, "路由必须先调用服务层"
    assert [payload["scope"] for payload in published] == ["assignments", "mappings"]


@pytest.mark.asyncio
async def test_create_point_publishes_real_channel_and_payload(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """走真实发布函数：校验频道与 JSON payload（Ingest 侧的接收契约）。"""
    published: list[tuple[str, str]] = []

    class FakeRedis:
        async def publish(self, channel: str, payload: str) -> None:
            published.append((channel, payload))

    async def get_redis():
        return FakeRedis()

    monkeypatch.setattr(distributed_nodes, "get_redis_client", get_redis)
    service_calls: list[str] = []
    _install_publisher_free_fakes(monkeypatch, service_calls)

    await catalog_routes.create_point({}, credentials=None)

    assert [channel for channel, _ in published] == [
        "ingest:control:invalidate",
        "ingest:control:invalidate",
    ]
    assert [json.loads(payload) for _, payload in published] == [
        {"scope": "assignments"},
        {"scope": "mappings"},
    ]


@pytest.mark.asyncio
async def test_publish_happens_only_after_service_returns(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    events: list[str] = []
    published: list[dict[str, Any]] = []

    class FakeCatalogService:
        def __init__(self, *, session: Any, tdengine: Any) -> None:
            pass

        async def create_point(self, payload: Any, *, actor: str) -> dict[str, int]:
            # 服务层返回即代表事务已提交
            events.append("service_returned")
            return {"id": 11}

    async def fake_publish(payload: dict[str, Any]) -> None:
        events.append(f"publish:{payload['scope']}")
        published.append(payload)

    monkeypatch.setattr(catalog_routes, "CatalogService", FakeCatalogService)
    monkeypatch.setattr(catalog_routes, "get_tdengine_client", lambda: object())
    monkeypatch.setattr(
        catalog_routes, "get_mysql_session_factory", _session_factory_provider
    )
    monkeypatch.setattr(
        catalog_routes,
        "require_permissions",
        AsyncMock(return_value={"user_name": "admin"}),
    )
    monkeypatch.setattr(
        catalog_routes, "_publish_control_invalidation", fake_publish
    )

    await catalog_routes.create_point({}, credentials=None)

    assert events == [
        "service_returned",
        "publish:assignments",
        "publish:mappings",
    ]


@pytest.mark.asyncio
async def test_failed_mutation_publishes_nothing(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    published: list[dict[str, Any]] = []

    class FailingCatalogService:
        def __init__(self, *, session: Any, tdengine: Any) -> None:
            pass

        async def create_point(self, payload: Any, *, actor: str) -> dict[str, int]:
            raise ApiError(409, "创建点位失败")

    async def fake_publish(payload: dict[str, Any]) -> None:
        published.append(payload)

    monkeypatch.setattr(catalog_routes, "CatalogService", FailingCatalogService)
    monkeypatch.setattr(catalog_routes, "get_tdengine_client", lambda: object())
    monkeypatch.setattr(
        catalog_routes, "get_mysql_session_factory", _session_factory_provider
    )
    monkeypatch.setattr(
        catalog_routes,
        "require_permissions",
        AsyncMock(return_value={"user_name": "admin"}),
    )
    monkeypatch.setattr(
        catalog_routes, "_publish_control_invalidation", fake_publish
    )

    with pytest.raises(ApiError):
        await catalog_routes.create_point({}, credentials=None)

    assert published == []


@pytest.mark.asyncio
async def test_lost_invalidation_notification_does_not_break_committed_response(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    """通知丢失（发布通道不可用）不得让已提交的变更返回失败。"""
    attempts: list[str] = []

    async def broken_redis_client():
        attempts.append("publish_attempt")
        raise RuntimeError("redis unavailable")

    monkeypatch.setattr(distributed_nodes, "get_redis_client", broken_redis_client)
    service_calls: list[str] = []
    _install_publisher_free_fakes(monkeypatch, service_calls)

    response = await catalog_routes.create_point({}, credentials=None)

    assert response is not None
    assert service_calls == ["create_point"]
    # 确实尝试过两次发布（assignments + mappings），两次都失败，但响应仍然成功
    assert attempts == ["publish_attempt", "publish_attempt"]


def _install_publisher_free_fakes(
    monkeypatch: pytest.MonkeyPatch, service_calls: list[str]
) -> None:
    class FakeCatalogService:
        def __init__(self, *, session: Any, tdengine: Any) -> None:
            pass

        async def create_point(self, payload: Any, *, actor: str) -> dict[str, int]:
            service_calls.append("create_point")
            return {"id": 11}

    monkeypatch.setattr(catalog_routes, "CatalogService", FakeCatalogService)
    monkeypatch.setattr(catalog_routes, "get_tdengine_client", lambda: object())
    monkeypatch.setattr(
        catalog_routes, "get_mysql_session_factory", _session_factory_provider
    )
    monkeypatch.setattr(
        catalog_routes,
        "require_permissions",
        AsyncMock(return_value={"user_name": "admin"}),
    )
