"""Static catalog endpoints must not be dispatched as numeric device IDs."""

import pytest

from app.api.router import api_router
from app.api.routes import catalog


@pytest.mark.parametrize(
    ("method", "path", "handler", "params"),
    [
        ("GET", "/api/device-monitor/device/types", catalog.device_types, {}),
        ("GET", "/api/device-monitor/device/summary", catalog.device_summary, {}),
        ("POST", "/api/device-monitor/device/query", catalog.query_devices, {}),
        ("GET", "/api/device-monitor/device/151", catalog.device_detail, {"device_id": "151"}),
    ],
)
def test_catalog_route_matching(method, path, handler, params):
    match = api_router.registry.match_route(method, path)
    assert match is not None
    route, path_params = match
    assert route.handler is handler
    assert path_params == params
