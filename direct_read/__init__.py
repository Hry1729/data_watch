"""现场 PLC 短连接只读。

不写平台实时缓存、时序库或 PLC。采集进程仍是周期入库的唯一所有者。
"""

from direct_read.api import read_point

__all__ = ["read_point"]
