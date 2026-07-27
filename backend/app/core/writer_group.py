"""Writer Redis 消费组名：统一走 Backend Settings（含 env / .env）。"""

from __future__ import annotations

from functools import lru_cache


@lru_cache(maxsize=1)
def get_writer_group_name() -> str:
    """
    单一入口：返回 Settings.writer_redis_group_name。

    Settings 已从环境变量 / .env 解析，默认 writer_cg；
    Docker 镜像缺 writer/settings.toml 时亦不崩溃。
    """
    from app.core.config import get_settings

    return get_settings().writer_redis_group_name
