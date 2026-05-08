from __future__ import annotations

from datetime import datetime, timedelta, timezone


LOCAL_TIMEZONE_NAME = "Asia/Shanghai"
LOCAL_TZ = timezone(timedelta(hours=8), LOCAL_TIMEZONE_NAME)
LOCAL_DATETIME_FORMAT = "%Y-%m-%d %H:%M:%S"


def now_local() -> datetime:
    """Return local project time as a naive datetime for MySQL DATETIME fields."""
    return datetime.now(LOCAL_TZ).replace(tzinfo=None)


def to_local_naive(value: datetime) -> datetime:
    """Convert aware datetimes to local naive time; keep naive values unchanged."""
    if value.tzinfo is None:
        return value
    return value.astimezone(LOCAL_TZ).replace(tzinfo=None)


def parse_local_datetime(value: object) -> datetime | None:
    """Parse API datetime values into local naive datetime.

    Naive strings are treated as local project time. Strings with timezone
    offsets or Z are converted to Asia/Shanghai and then stored as naive time.
    """
    if value is None:
        return None
    if isinstance(value, datetime):
        return to_local_naive(value)
    if isinstance(value, str):
        raw = value.strip()
        if not raw:
            return None
        normalized = raw.replace("Z", "+00:00")
        candidates = [normalized]
        if "T" in normalized:
            candidates.append(normalized.replace("T", " "))
        for candidate in candidates:
            try:
                parsed = datetime.fromisoformat(candidate)
            except ValueError:
                continue
            return to_local_naive(parsed)
    return None


def format_local_datetime(value: datetime | None, fmt: str = LOCAL_DATETIME_FORMAT) -> str:
    if value is None:
        return ""
    return to_local_naive(value).strftime(fmt)
