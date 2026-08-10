from __future__ import annotations

import pytest

from cli.errors import CliError
from cli.resolvers import Resolver, device_lookup_key


class FakeSession:
    def __init__(self, payload: dict) -> None:
        self.payload = payload
        self.requests: list[tuple[str, str, dict]] = []

    def request(self, method: str, path: str, *, params=None, json_body=None):
        self.requests.append((method, path, params))
        return self.payload


def test_catalog_device_resolution_is_cached_for_the_session() -> None:
    session = FakeSession(
        {
            "list": [
                {"id": 7, "deviceCode": "QC100", "deviceName": "QC100"}
            ]
        }
    )
    resolver = Resolver(session)

    first = resolver.resolve_catalog_device("桥吊100")
    second = resolver.resolve_catalog_device("QC100")

    assert first["id"] == 7
    assert second is first
    assert len(session.requests) == 1
    assert session.requests[0][2]["keyword"] == "qc100"


@pytest.mark.parametrize(
    ("identifier", "expected"),
    [
        ("桥吊100", "qc100"),
        ("qc100", "qc100"),
        ("QC100", "qc100"),
        ("轨道吊701L", "asc701l"),
        ("轨道吊701陆侧", "asc701l"),
        ("轨道吊701S", "asc701s"),
        ("轨道吊701海侧", "asc701s"),
        ("AGV801", "agv801"),
    ],
)
def test_device_lookup_key_supports_terminal_business_names(
    identifier: str, expected: str
) -> None:
    assert device_lookup_key(identifier) == expected


def test_numeric_device_identifier_does_not_trigger_lookup() -> None:
    session = FakeSession({"list": []})

    assert Resolver(session).resolve_catalog_device("42") == {"id": 42}
    assert session.requests == []


def test_missing_device_returns_structured_resource_error() -> None:
    resolver = Resolver(FakeSession({"list": []}))

    with pytest.raises(CliError) as captured:
        resolver.resolve_catalog_device("missing")

    assert captured.value.error_type == "resource_not_found"
    assert captured.value.status == 404
