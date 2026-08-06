from datetime import datetime, timedelta

from app.services.dashboard import build_edge_pipeline_health


def _node(
    *,
    now: datetime,
    collector: str = "running",
    uploader: str = "running",
    watermark: str = "normal",
    stale: bool = False,
) -> dict:
    return {
        "collector_status": collector,
        "uploader_status": uploader,
        "outbox_watermark": watermark,
        "last_seen_at": now - timedelta(seconds=31 if stale else 5),
    }


def test_edge_pipeline_health_warns_when_any_edge_is_abnormal() -> None:
    now = datetime(2026, 8, 6, 12, 0, 0)
    result = build_edge_pipeline_health(
        [
            _node(now=now),
            _node(now=now, collector="stopped"),
            _node(now=now, watermark="warning"),
        ],
        now=now,
    )

    assert result["collector"] == {
        "status": "warning",
        "totalNodes": 3,
        "healthyNodes": 2,
        "warningNodes": 0,
        "criticalNodes": 1,
    }
    assert result["uploader"] == {
        "status": "warning",
        "totalNodes": 3,
        "healthyNodes": 2,
        "warningNodes": 1,
        "criticalNodes": 0,
    }


def test_edge_pipeline_health_is_critical_when_all_edges_are_unavailable() -> None:
    now = datetime(2026, 8, 6, 12, 0, 0)
    result = build_edge_pipeline_health(
        [_node(now=now, stale=True), _node(now=now, collector="crashed", uploader="stopped")],
        now=now,
    )

    assert result["collector"]["status"] == "critical"
    assert result["collector"]["criticalNodes"] == 2
    assert result["uploader"]["status"] == "critical"
    assert result["uploader"]["criticalNodes"] == 2


def test_edge_pipeline_health_is_critical_when_all_edges_are_warning() -> None:
    now = datetime(2026, 8, 6, 12, 0, 0)
    result = build_edge_pipeline_health(
        [_node(now=now, watermark="warning"), _node(now=now, watermark="warning")],
        now=now,
    )

    assert result["uploader"]["status"] == "critical"
    assert result["uploader"]["healthyNodes"] == 0
    assert result["uploader"]["warningNodes"] == 2


def test_edge_pipeline_health_is_unknown_without_approved_edges() -> None:
    result = build_edge_pipeline_health([], now=datetime(2026, 8, 6, 12, 0, 0))

    assert result["collector"]["status"] == "unknown"
    assert result["uploader"]["status"] == "unknown"
    assert result["collector"]["totalNodes"] == 0

