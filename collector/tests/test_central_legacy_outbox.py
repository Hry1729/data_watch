"""The Redis-only switch must neither lose nor replay legacy sampling data."""

import hashlib
import sqlite3
from pathlib import Path

import pytest

from core.config import CollectorConfig
from core.exceptions import ConfigError
from main import CollectorApp, _check_legacy_sampling_outbox


@pytest.fixture
def legacy_path(tmp_path, monkeypatch):
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    path = tmp_path / "runtime" / "collector-retry.db"
    path.parent.mkdir()
    return path


def _files(path):
    return {p.name: hashlib.sha256(p.read_bytes()).hexdigest()
            for p in path.parent.iterdir() if p.is_file()}


def test_missing_outbox_creates_nothing(legacy_path):
    _check_legacy_sampling_outbox()
    assert _files(legacy_path) == {}


def test_empty_legacy_queue_is_read_only(legacy_path):
    with sqlite3.connect(legacy_path) as conn:
        conn.execute("CREATE TABLE collector_retry_outbox (id TEXT)")
        conn.execute("CREATE TABLE collector_retry_outbox_v2 (id TEXT)")
    before = _files(legacy_path)
    _check_legacy_sampling_outbox()
    assert _files(legacy_path) == before


@pytest.mark.parametrize("table", ["collector_retry_outbox", "collector_retry_outbox_v2"])
def test_pending_queue_blocks_real_app_before_resources(legacy_path, table, monkeypatch):
    with sqlite3.connect(legacy_path) as conn:
        conn.execute(f"CREATE TABLE {table} (id TEXT)")
        conn.execute(f"INSERT INTO {table} VALUES ('unpublished-sample')")
    before = _files(legacy_path)

    def unexpected(*args, **kwargs):
        pytest.fail("blocked startup must not initialize worker resources")

    monkeypatch.setattr("main.PerformanceMetrics", unexpected)
    with pytest.raises(ConfigError, match="pending rows"):
        CollectorApp(CollectorConfig())
    assert _files(legacy_path) == before


@pytest.mark.parametrize("contents", [b"not a database", b""])
def test_unknown_database_blocks_without_changes(legacy_path, contents):
    legacy_path.write_bytes(contents)
    before = _files(legacy_path)
    with pytest.raises(ConfigError, match="startup blocked"):
        _check_legacy_sampling_outbox()
    assert _files(legacy_path) == before


def test_live_wal_blocks_without_checkpoint_or_replay(legacy_path):
    conn = sqlite3.connect(legacy_path)
    try:
        conn.execute("PRAGMA journal_mode=WAL")
        conn.execute("CREATE TABLE collector_retry_outbox_v2 (id TEXT)")
        conn.execute("INSERT INTO collector_retry_outbox_v2 VALUES ('pending-wal')")
        conn.commit()
        before = _files(legacy_path)
        with pytest.raises(ConfigError, match="-wal present"):
            _check_legacy_sampling_outbox()
        assert _files(legacy_path) == before
    finally:
        conn.close()


def test_unresolved_rollback_journal_blocks(legacy_path):
    legacy_path.with_name(legacy_path.name + "-journal").write_bytes(b"unresolved")
    before = _files(legacy_path)
    with pytest.raises(ConfigError, match="-journal present"):
        _check_legacy_sampling_outbox()
    assert _files(legacy_path) == before


def test_unreadable_path_is_not_treated_as_missing(legacy_path, monkeypatch):
    original = Path.stat

    def stat(path, *args, **kwargs):
        if path == legacy_path:
            raise PermissionError("denied")
        return original(path, *args, **kwargs)

    monkeypatch.setattr(Path, "stat", stat)
    with pytest.raises(ConfigError, match="state could not be verified"):
        _check_legacy_sampling_outbox()


def test_edge_factory_bypasses_central_legacy_guard(legacy_path):
    legacy_path.write_bytes(b"unrelated to the edge sink")
    before = _files(legacy_path)

    class FactoryReached(Exception):
        pass

    def edge_factory(config, metrics):
        metrics.close()
        raise FactoryReached

    with pytest.raises(FactoryReached):
        CollectorApp(CollectorConfig(), writer_factory=edge_factory)
    assert _files(legacy_path) == before
