"""ConfigDB scan_rate normalization tests."""

import pytest

from core.config_db import ConfigDBManager


@pytest.mark.parametrize(
    ("raw_value", "expected"),
    [
        (100, 0.1),
        (500, 0.5),
        (1000, 1.0),
        (1500, 1.5),
        (1, 1.0),
    ],
)
def test_normalize_scan_rate_keeps_millisecond_precision(raw_value, expected):
    assert ConfigDBManager._normalize_scan_rate(raw_value) == pytest.approx(expected)


def test_normalize_scan_rate_uses_default_for_missing_or_invalid_values():
    assert ConfigDBManager._normalize_scan_rate(None, default_seconds=0.5) == pytest.approx(0.5)
    assert ConfigDBManager._normalize_scan_rate(0, default_seconds=0.5) == pytest.approx(0.5)
