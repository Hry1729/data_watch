from __future__ import annotations

from typing import Any

from cli.resolvers import Resolver


class RouteSession:
    def __init__(self) -> None:
        self.requests: list[str] = []

    def request(self, method: str, path: str, *, params=None, json_body=None) -> Any:
        self.requests.append(path)
        if path.endswith("/devices"):
            return [{"id": 7, "deviceCode": "ASC701L", "deviceName": "ASC701L 轨道吊"}]
        if path.endswith("/points"):
            return [
                {"varSeq": 1, "pointName": "TEMP001", "ioAddress": "DB1.DBW0"},
                {"varSeq": 2, "pointName": "PRESS001", "ioAddress": "DB1.DBW2"},
            ]
        if path.endswith("/device-tree"):
            return [
                {
                    "nodeType": "group",
                    "children": [
                        {
                            "nodeType": "device",
                            "deviceId": 7,
                            "deviceCode": "ASC701L",
                        }
                    ],
                }
            ]
        raise AssertionError(path)


def test_history_device_and_points_share_session_cache() -> None:
    session = RouteSession()
    resolver = Resolver(session)

    device = resolver.resolve_history_device("轨道吊701陆侧")
    points = resolver.resolve_history_points(int(device["id"]), ["TEMP001", "2"])
    repeated = resolver.resolve_history_points(int(device["id"]), ["TEMP001"])

    assert [point["varSeq"] for point in points] == [1, 2]
    assert repeated[0]["varSeq"] == 1
    assert session.requests.count("/api/device-monitor/history/devices") == 1
    assert session.requests.count("/api/device-monitor/history/points") == 1


def test_monitor_tree_resolution_is_cached() -> None:
    session = RouteSession()
    resolver = Resolver(session)

    assert resolver.resolve_monitor_device("轨道吊701L")["deviceId"] == 7
    assert resolver.resolve_monitor_device("7")["deviceCode"] == "ASC701L"
    assert session.requests.count("/api/device-monitor/monitor/device-tree") == 1
