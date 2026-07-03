from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from app.core.exceptions import ApiError


@dataclass(slots=True)
class CollectorConfigPayload:
    """collector 运行参数配置请求体。"""
    stream_maxlen: int

    @classmethod
    def from_dict(cls, payload: dict[str, Any]) -> "CollectorConfigPayload":
        if "streamMaxlen" in payload:
            raw = payload["streamMaxlen"]
        elif "stream_maxlen" in payload:
            raw = payload["stream_maxlen"]
        else:
            raise ApiError(400, "streamMaxlen is required")
        try:
            stream_maxlen = int(raw)
        except (TypeError, ValueError):
            raise ApiError(400, "streamMaxlen must be an integer")
        if stream_maxlen < 1:
            raise ApiError(400, "streamMaxlen must be >= 1")
        return cls(stream_maxlen=stream_maxlen)
