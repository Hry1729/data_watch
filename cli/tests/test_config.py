from __future__ import annotations

import json

import pytest

from cli.config import ConfigStore
from cli.errors import CliError, EXIT_USAGE


def test_profiles_are_persisted_and_selected(tmp_path) -> None:
    path = tmp_path / "cli-config.json"
    store = ConfigStore(path=path, environ={})

    store.create_profile("production", "https://backend.example.com/", timeout_seconds=30)
    store.use_profile("production")
    runtime = store.resolve()

    assert runtime.profile == "production"
    assert runtime.base_url == "https://backend.example.com"
    assert runtime.timeout_seconds == 30
    assert json.loads(path.read_text(encoding="utf-8"))["currentProfile"] == "production"


def test_runtime_overrides_take_precedence_without_mutating_file(tmp_path) -> None:
    path = tmp_path / "cli-config.json"
    store = ConfigStore(
        path=path,
        environ={
            "DATAWATCH_PROFILE": "development",
            "DATAWATCH_API_URL": "https://override.example.com/",
            "DATAWATCH_TIMEOUT": "25",
        },
    )

    runtime = store.resolve(api_url_override="https://flag.example.com", timeout_override=5)

    assert runtime.base_url == "https://flag.example.com"
    assert runtime.timeout_seconds == 5
    assert not path.exists()


@pytest.mark.parametrize("url", ["", "localhost:8000", "ftp://example.com"])
def test_invalid_backend_url_is_rejected(tmp_path, url: str) -> None:
    store = ConfigStore(path=tmp_path / "config.json", environ={})

    with pytest.raises(CliError) as captured:
        store.set_url(url)

    assert captured.value.exit_code == EXIT_USAGE
