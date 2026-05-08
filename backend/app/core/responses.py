from __future__ import annotations

from typing import Any


def success_response(data: Any, msg: str = "success") -> dict[str, Any]:
    return {"code": 200, "msg": msg, "data": data}
