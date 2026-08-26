from __future__ import annotations

from datetime import datetime, timedelta, timezone
from math import isfinite


LOCAL_TIMEZONE_NAME = "Asia/Shanghai"
LOCAL_TZ = timezone(timedelta(hours=8), LOCAL_TIMEZONE_NAME)
LOCAL_DATETIME_FORMAT = "%Y-%m-%d %H:%M:%S"


def now_local() -> datetime:
    """返回本地项目时间作为 naive datetime，用于 MySQL DATETIME 字段。"""
    return datetime.now(LOCAL_TZ).replace(tzinfo=None)


def to_local_naive(value: datetime) -> datetime:
    """将带时区的 datetime 转换为本地 naive 时间；保持 naive 值不变。"""
    if value.tzinfo is None:
        return value
    return value.astimezone(LOCAL_TZ).replace(tzinfo=None)


def parse_local_datetime(value: object) -> datetime | None:
    """将 API datetime 值解析为本地 naive datetime。不带时区的字符串被视为本地项目时间。带时区偏移或 Z 的字符串转换为 Asia/Shanghai 后以 naive 时间存储。"""
    if value is None:
        return None
    if isinstance(value, datetime):
        return to_local_naive(value)
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        epoch = float(value)
        if not isfinite(epoch):
            return None
        magnitude = abs(epoch)
        if magnitude >= 100_000_000_000_000_000:
            epoch /= 1_000_000_000
        elif magnitude >= 100_000_000_000_000:
            epoch /= 1_000_000
        elif magnitude >= 100_000_000_000:
            epoch /= 1_000
        try:
            return to_local_naive(datetime.fromtimestamp(epoch, tz=LOCAL_TZ))
        except (OSError, OverflowError, ValueError):
            return None
    if isinstance(value, str):
        raw = value.strip()
        if not raw:
            return None
        try:
            numeric = float(raw)
        except ValueError:
            numeric = None
        if numeric is not None and abs(numeric) >= 100_000_000:
            return parse_local_datetime(numeric)
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
