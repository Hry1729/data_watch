from __future__ import annotations

from pathlib import Path

from storage.edge_outbox_writer import EdgeOutboxWriter
from edge.app.outbox import OutboxStore


def test_collector_sink_persists_outbox_before_local_latest(tmp_path: Path) -> None:
    store = OutboxStore(tmp_path / "edge.db")
    store.configure_assignment("QC100", 5, 100, 1)
    writer = EdgeOutboxWriter(store)

    writer.write_window_stream(
        "QC100",
        "qc",
        "g001",
        100,
        1721800000000,
        12,
        {"values": {"1": "12.5"}},
        {"1": "12.5"},
        {"1": 192},
    )
    writer.write_latest(
        "QC100",
        "g001",
        {"1": {"v": "12.5", "t": 1721800000000, "q": 192}},
    )

    assert store.snapshot_target_seq() == 1
    assert store.stats() == {"pending": 1}
    assert store.read_local_latest("QC100", "g001")["1"]["v"] == "12.5"
    writer.close()
