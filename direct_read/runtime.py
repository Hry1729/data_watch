"""MVP 运行时门闩：仅 Windows + CPython 3.14 自由线程（cp314t）。"""

from __future__ import annotations

import sys
import sysconfig

from direct_read.errors import DirectReadError
from direct_read.quality import QUALITY_NOT_CONNECTED


def mvp_runtime_supported() -> bool:
    """现场 S7 驱动清单声明 ``os = windows`` 且 ``python_abi = cp314t``。"""
    if sys.platform != "win32":
        return False
    if sys.version_info[:2] != (3, 14):
        return False
    flags = getattr(sys, "abiflags", "") or ""
    if "t" in flags:
        return True
    return sysconfig.get_config_var("Py_GIL_DISABLED") == 1


def ensure_mvp_runtime() -> None:
    if mvp_runtime_supported():
        return
    raise DirectReadError(
        "unsupported_runtime",
        "直连读 PLC 的 MVP 只支持 Windows + Python 3.14t，当前环境不能装载 S7 驱动",
        quality=QUALITY_NOT_CONNECTED,
        details={
            "platform": sys.platform,
            "python": ".".join(str(part) for part in sys.version_info[:3]),
            "abiflags": getattr(sys, "abiflags", "") or "",
        },
    )
