"""直连读错误。``error_type`` 与 CLI ``error.type`` 对齐。"""

from __future__ import annotations

from typing import Any

from direct_read.quality import (
    DIRECT_SOURCE,
    QUALITY_NOT_CONNECTED,
    quality_name,
)


class TopologyUnavailable(Exception):
    """当前拓扑源不可用，调用方可以改试下一个来源。

    设备不存在、点位歧义、账号可写都不是这种错误：那些必须停下来，不能改读别的来源。
    """


class DirectReadError(Exception):
    def __init__(
        self,
        error_type: str,
        message: str,
        *,
        status: int | None = None,
        details: dict[str, Any] | None = None,
        quality: int = QUALITY_NOT_CONNECTED,
    ) -> None:
        super().__init__(message)
        self.error_type = error_type
        self.message = message
        self.status = status
        payload = dict(details or {})
        payload["source"] = DIRECT_SOURCE
        payload.setdefault("quality", quality)
        payload.setdefault("qualityName", quality_name(int(payload["quality"])))
        self.details = payload
