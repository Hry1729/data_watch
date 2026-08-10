from __future__ import annotations

import json
from typing import Any

from typer.testing import CliRunner

import cli.commands.common as common_module
import cli.main as main_module
from cli.permissions import get_command_policy, is_command_allowed


runner = CliRunner()


class FakeResolver:
    catalog_requests: list[str] = []
    monitor_requests: list[str] = []

    def resolve_catalog_device(self, identifier: str) -> dict[str, Any]:
        type(self).catalog_requests.append(identifier)
        assert identifier == "桥吊100"
        return {"id": 7, "deviceCode": "QC100"}

    def resolve_monitor_device(self, identifier: str) -> dict[str, Any]:
        type(self).monitor_requests.append(identifier)
        assert identifier == "桥吊100"
        return {"deviceId": 7, "deviceCode": "QC100"}


class FakeAuthorizedSession:
    requests: list[str] = []
    request_params: list[tuple[str, Any]] = []
    dashboard_allowed = True
    device_catalog_allowed = True

    def __init__(self, _state) -> None:
        self.resolver = FakeResolver()

    def __enter__(self) -> "FakeAuthorizedSession":
        return self

    def __exit__(self, *_args: object) -> None:
        pass

    def authorize(self, _command: str) -> dict[str, Any]:
        return {"permissions": []}

    def has_any_permission(self, *_permissions: str) -> bool:
        if "device_monitor:device:view" in _permissions:
            return type(self).device_catalog_allowed
        return type(self).dashboard_allowed

    def request(self, method: str, path: str, *, params=None, json_body=None):
        type(self).requests.append(path)
        type(self).request_params.append((path, params))
        if path == "/healthz":
            return {"service": "backend"}
        if path.endswith("/overview"):
            return {
                "systemAlertTotal": 4,
                "systemAlertComparePrev": {"countDiff": 1, "ratioDiff": 0.25},
                "generatedAt": "2026-08-10 12:00:00",
            }
        if path.endswith("/log/list"):
            return {"list": [{"level": "ERROR"}], "total": 1}
        if path.endswith("/collector"):
            return {"totalDevices": 2, "onlineDevices": 1, "tasks": []}
        if path.endswith("/writer"):
            return {"dlqTotal": 0, "streams": []}
        if path.endswith("/rates"):
            return {"samples": []}
        if path.endswith("/edge-pipeline-health"):
            return {
                "collector": {"status": "healthy"},
                "uploader": {"status": "healthy"},
            }
        if path == "/api/distributed/nodes":
            return {"items": []}
        raise AssertionError(path)


def test_system_alerts_only_use_real_overview_and_log_routes(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    summary = runner.invoke(main_module.app, ["system-alerts", "summary"])
    listing = runner.invoke(
        main_module.app,
        ["system-alerts", "list", "--level", "CRITICAL"],
    )

    assert summary.exit_code == 0
    assert listing.exit_code == 0
    assert json.loads(summary.stdout)["data"]["systemAlertTotal"] == 4
    assert FakeAuthorizedSession.requests == [
        "/api/device-monitor/dashboard/overview",
        "/api/device-monitor/log/list",
    ]
    assert all("recent-events" not in path for path in FakeAuthorizedSession.requests)
    assert all("alarm-distribution" not in path for path in FakeAuthorizedSession.requests)


def test_log_device_filters_resolve_business_name(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    FakeAuthorizedSession.request_params = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    alerts = runner.invoke(
        main_module.app,
        ["system-alerts", "list", "--device", "桥吊100"],
    )
    logs = runner.invoke(
        main_module.app,
        ["logs", "list", "--device", "桥吊100"],
    )

    assert alerts.exit_code == 0
    assert logs.exit_code == 0
    log_params = [
        params
        for path, params in FakeAuthorizedSession.request_params
        if path == "/api/device-monitor/log/list"
    ]
    assert [params["deviceCode"] for params in log_params] == ["QC100", "QC100"]


def test_r_user_log_device_filter_uses_monitor_resolver(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    FakeAuthorizedSession.request_params = []
    FakeResolver.catalog_requests = []
    FakeResolver.monitor_requests = []
    monkeypatch.setattr(FakeAuthorizedSession, "device_catalog_allowed", False)
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        ["logs", "list", "--device", "桥吊100"],
    )

    assert result.exit_code == 0
    assert FakeResolver.catalog_requests == []
    assert FakeResolver.monitor_requests == ["桥吊100"]


def test_invalid_system_alert_level_is_structured_usage_error(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(
        main_module.app,
        ["system-alerts", "list", "--level", "INFO"],
    )

    assert result.exit_code == 2
    assert json.loads(result.stderr)["error"]["type"] == "usage_error"
    assert FakeAuthorizedSession.requests == []


def test_module_status_reports_degraded_and_exposes_each_check(monkeypatch) -> None:
    FakeAuthorizedSession.requests = []
    FakeAuthorizedSession.dashboard_allowed = True
    monkeypatch.setattr(common_module, "AuthorizedSession", FakeAuthorizedSession)

    result = runner.invoke(main_module.app, ["modules", "status"])

    assert result.exit_code == 0
    data = json.loads(result.stdout)["data"]
    assert data["status"] == "degraded"
    assert {check["name"] for check in data["checks"]} == {
        "backend",
        "collector",
        "writer",
        "rates",
        "edgePipeline",
    }
    assert any("1 台设备" in reason for reason in data["reasons"])


def test_node_policy_requires_admin_or_super_role() -> None:
    policy = get_command_policy("nodes list")
    assert policy is not None
    assert is_command_allowed(
        policy,
        {"roles": ["R_USER"], "permissions": []},
    ) is False
    assert is_command_allowed(
        policy,
        {"roles": ["R_ADMIN"], "permissions": []},
    ) is True

def test_debug_duration_matches_backend_range() -> None:
    too_short = runner.invoke(
        main_module.app,
        ["logs", "debug", "enable", "--minutes", "1"],
    )
    too_long = runner.invoke(
        main_module.app,
        ["logs", "debug", "enable", "--minutes", "481"],
    )

    assert too_short.exit_code == 2
    assert too_long.exit_code == 2
