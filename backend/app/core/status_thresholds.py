"""设备在线状态判定阈值（全系统单一来源）。

connectStatus 基于最近采集数据时间 age = now - last_collect_time 判定：
- age <= ONLINE_MAX_AGE_SECONDS    → online（在线）
- age <= UNSTABLE_MAX_AGE_SECONDS  → unstable（异常）
- 否则                              → offline（离线）

data_status 的 normal 同样以 ONLINE_MAX_AGE_SECONDS 为界。
catalog / dashboard / monitor / module_monitor 等 service 统一引用本模块。
"""

from __future__ import annotations

ONLINE_MAX_AGE_SECONDS = 60
UNSTABLE_MAX_AGE_SECONDS = 120
