"""配置文件加载器（TOML）。"""

from __future__ import annotations

import os
from codecs import BOM_UTF8
import tomllib

from core.config import (
    CollectorConfig,
    InterfaceIngestConfig,
    MySQLConfig,
    RedisConfig,
)
from core.exceptions import ConfigError
from core.logger import get_logger

logger = get_logger("config_loader")


def load_collector_config(path: str) -> CollectorConfig:
    """从 TOML 文件加载 CollectorConfig。"""
    if not os.path.exists(path):
        raise ConfigError(f"Config file not found: {path}")

    try:
        with open(path, "rb") as f:
            content = f.read()
        # PowerShell/Windows 下常见 UTF-8 BOM，tomllib 不接受，需先去掉。
        if content.startswith(BOM_UTF8):
            content = content[len(BOM_UTF8) :]
        raw = tomllib.loads(content.decode("utf-8"))
    except tomllib.TOMLDecodeError as exc:
        raise ConfigError(f"Invalid TOML in config file: {exc}") from exc
    except UnicodeDecodeError as exc:
        raise ConfigError(f"Config file must be UTF-8 encoded: {exc}") from exc
    except OSError as exc:
        raise ConfigError(f"Failed to read config file: {exc}") from exc

    runtime = raw.get("runtime", {})
    mysql_raw = raw.get("mysql", {})
    ingest_raw = raw.get("interface_ingest", {})
    redis_raw = raw.get("redis", {})
    devices_raw = raw.get("devices", [])
    variables_raw = raw.get("variables", [])

    if devices_raw or variables_raw:
        logger.warning(
            "devices/variables in TOML are ignored. "
            "Topology is loaded from MySQL (device/device_variable)."
        )

    try:
        config = CollectorConfig(
            mysql=MySQLConfig(**mysql_raw),
            interface_ingest=InterfaceIngestConfig(**ingest_raw),
            redis=RedisConfig(**redis_raw),
            devices=[],
            variables=[],
            enable_fake_bootstrap=bool(runtime.get("enable_fake_bootstrap", False)),
            log_level=str(runtime.get("log_level", "INFO")),
            log_file=runtime.get("log_file"),  # 可选，为 None 则只输出控制台
            log_max_bytes=int(runtime.get("log_max_bytes", 10 * 1024 * 1024)),
            log_backup_count=int(runtime.get("log_backup_count", 5)),
        )
    except KeyError as exc:
        raise ConfigError(f"Missing required config field: {exc}") from exc
    except Exception as exc:
        raise ConfigError(f"Invalid config content: {exc}") from exc

    env_password = os.getenv("DATAWATCH_MYSQL_PASSWORD")
    if env_password:
        config.mysql.password = env_password

    return config


def load_collector_config_from_env(default_path: str = "config/settings.toml") -> CollectorConfig:
    """
    从环境变量或默认路径读取配置。

    环境变量:
        COLLECTOR_CONFIG: 配置文件的完整路径。
            如果未设置，则使用 default_path 参数指定的路径。
            示例: COLLECTOR_CONFIG=/app/config/production.toml

    Args:
        default_path: 默认配置文件路径，默认为 "config/settings.toml"

    Returns:
        CollectorConfig: 加载的配置对象
    """
    path = os.getenv("COLLECTOR_CONFIG", default_path)
    return load_collector_config(path)
