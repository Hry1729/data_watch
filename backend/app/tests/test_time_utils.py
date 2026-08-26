from __future__ import annotations

from datetime import datetime

from app.core.time_utils import LOCAL_TZ, parse_local_datetime


def test_parse_local_datetime_accepts_epoch_seconds_milliseconds_and_microseconds():
    expected = datetime(2026, 8, 6, 10, 0, 0)
    epoch_seconds = expected.replace(tzinfo=LOCAL_TZ).timestamp()

    assert parse_local_datetime(epoch_seconds) == expected
    assert parse_local_datetime(epoch_seconds * 1_000) == expected
    assert parse_local_datetime(epoch_seconds * 1_000_000) == expected
    assert parse_local_datetime(str(int(epoch_seconds * 1_000))) == expected
