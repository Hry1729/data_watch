from __future__ import annotations

import os
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Any

try:
    import tomllib
except ModuleNotFoundError:  # pragma: no cover - Python < 3.11 fallback
    tomllib = None


def _repo_root() -> Path:
    return Path(__file__).resolve().parents[3]


def _load_collector_defaults() -> dict[str, Any]:
    config_path = _repo_root() / "collector" / "config" / "settings.toml"
    if not config_path.exists() or tomllib is None:
        return {}

    with config_path.open("rb") as fp:
        return tomllib.load(fp)


def _env_or_default(name: str, default: Any) -> Any:
    return os.getenv(name, default)


def _load_backend_env_defaults() -> dict[str, str]:
    env_defaults: dict[str, str] = {}
    candidates = [
        _repo_root() / "backend" / ".env.example",
        _repo_root() / "backend" / ".env",
    ]

    for path in candidates:
        if not path.exists():
            continue

        for raw_line in path.read_text(encoding="utf-8").splitlines():
            line = raw_line.strip()
            if not line or line.startswith("#") or "=" not in line:
                continue
            key, value = line.split("=", 1)
            env_defaults[key.strip()] = value.strip()

    return env_defaults


@dataclass(frozen=True)
class Settings:
    app_name: str
    app_version: str
    log_level: str
    api_prefix: str
    host: str
    port: int
    cors_origins: list[str]
    mysql_host: str
    mysql_port: int
    mysql_user: str
    mysql_password: str
    mysql_database: str
    mysql_charset: str
    tdengine_host: str
    tdengine_port: int
    tdengine_user: str
    tdengine_password: str
    tdengine_database: str
    tdengine_use_ssl: bool
    perspective_enabled: bool
    perspective_ws_path: str
    perspective_max_clients: int
    perspective_session_idle_ttl_seconds: int
    perspective_session_restore_ttl_seconds: int
    perspective_max_sessions_per_owner: int
    perspective_max_sessions_global: int
    perspective_max_points_per_query: int
    perspective_max_range_hours: int
    perspective_max_records_per_query: int
    perspective_ping_interval_seconds: int
    perspective_timeout_seconds: int
    auth_secret_key: str
    auth_access_token_expire_seconds: int
    auth_refresh_token_expire_seconds: int
    auth_auto_bootstrap: bool
    redis_host: str
    redis_port: int
    redis_db: int
    redis_password: str

    @property
    def mysql_async_dsn(self) -> str:
        return (
            f"mysql+aiomysql://{self.mysql_user}:{self.mysql_password}"
            f"@{self.mysql_host}:{self.mysql_port}/{self.mysql_database}"
            f"?charset={self.mysql_charset}"
        )

    @property
    def tdengine_ws_dsn(self) -> str:
        scheme = "wss" if self.tdengine_use_ssl else "ws"
        return (
            f"{scheme}://{self.tdengine_user}:{self.tdengine_password}"
            f"@{self.tdengine_host}:{self.tdengine_port}/{self.tdengine_database}"
        )

    @property
    def perspective_ws_route(self) -> str:
        ws_path = self.perspective_ws_path if self.perspective_ws_path.startswith("/") else f"/{self.perspective_ws_path}"
        return f"{self.api_prefix}{ws_path}"


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    defaults = _load_collector_defaults()
    backend_env_defaults = _load_backend_env_defaults()
    runtime_defaults = defaults.get("runtime", {})
    mysql_defaults = defaults.get("mysql", {})
    tdengine_defaults = defaults.get("tdengine", {})

    def read_setting(name: str, default: Any) -> Any:
        return _env_or_default(name, backend_env_defaults.get(name, default))

    cors_default = "http://localhost:3000,http://localhost:5173,http://127.0.0.1:5173,http://localhost:1420,http://127.0.0.1:1420"
    raw_origins = read_setting("BACKEND_CORS_ORIGINS", cors_default)
    cors_origins = [item.strip() for item in str(raw_origins).split(",") if item.strip()]

    return Settings(
        app_name=str(read_setting("BACKEND_APP_NAME", "Industrial Device Monitor Backend")),
        app_version=str(read_setting("BACKEND_APP_VERSION", "0.1.0")),
        log_level=str(read_setting("BACKEND_LOG_LEVEL", runtime_defaults.get("log_level", "INFO"))),
        api_prefix=str(read_setting("BACKEND_API_PREFIX", "/api/device-monitor")),
        host=str(read_setting("BACKEND_HOST", "0.0.0.0")),
        port=int(read_setting("BACKEND_PORT", 8000)),
        cors_origins=cors_origins,
        mysql_host=str(read_setting("MYSQL_HOST", mysql_defaults.get("host", "127.0.0.1"))),
        mysql_port=int(read_setting("MYSQL_PORT", mysql_defaults.get("port", 3306))),
        mysql_user=str(read_setting("MYSQL_USER", mysql_defaults.get("user", "root"))),
        mysql_password=str(read_setting("MYSQL_PASSWORD", mysql_defaults.get("password", ""))),
        mysql_database=str(read_setting("MYSQL_DATABASE", mysql_defaults.get("database", "device_watch"))),
        mysql_charset=str(read_setting("MYSQL_CHARSET", mysql_defaults.get("charset", "utf8mb4"))),
        tdengine_host=str(read_setting("TDENGINE_HOST", tdengine_defaults.get("host", "127.0.0.1"))),
        tdengine_port=int(read_setting("TDENGINE_PORT", tdengine_defaults.get("port", 6041))),
        tdengine_user=str(read_setting("TDENGINE_USER", tdengine_defaults.get("user", "root"))),
        tdengine_password=str(read_setting("TDENGINE_PASSWORD", tdengine_defaults.get("password", ""))),
        tdengine_database=str(read_setting("TDENGINE_DATABASE", tdengine_defaults.get("database", "device_data"))),
        tdengine_use_ssl=str(read_setting("TDENGINE_USE_SSL", tdengine_defaults.get("use_ssl", False))).lower()
        in {"1", "true", "yes"},
        perspective_enabled=str(read_setting("PERSPECTIVE_ENABLED", True)).lower() in {"1", "true", "yes"},
        perspective_ws_path=str(read_setting("PERSPECTIVE_WS_PATH", "/ws/perspective")),
        perspective_max_clients=int(read_setting("PERSPECTIVE_MAX_CLIENTS", 200)),
        perspective_session_idle_ttl_seconds=int(read_setting("PERSPECTIVE_SESSION_IDLE_TTL_SECONDS", 900)),
        perspective_session_restore_ttl_seconds=int(read_setting("PERSPECTIVE_SESSION_RESTORE_TTL_SECONDS", 300)),
        perspective_max_sessions_per_owner=int(read_setting("PERSPECTIVE_MAX_SESSIONS_PER_OWNER", 3)),
        perspective_max_sessions_global=int(read_setting("PERSPECTIVE_MAX_SESSIONS_GLOBAL", 100)),
        perspective_max_points_per_query=int(read_setting("PERSPECTIVE_MAX_POINTS_PER_QUERY", 20)),
        perspective_max_range_hours=int(read_setting("PERSPECTIVE_MAX_RANGE_HOURS", 24)),
        perspective_max_records_per_query=int(read_setting("PERSPECTIVE_MAX_RECORDS_PER_QUERY", 200000)),
        perspective_ping_interval_seconds=int(read_setting("PERSPECTIVE_PING_INTERVAL_SECONDS", 15)),
        perspective_timeout_seconds=int(read_setting("PERSPECTIVE_TIMEOUT_SECONDS", 45)),
        auth_secret_key=str(read_setting("AUTH_SECRET_KEY", "device-monitor-dev-secret")),
        auth_access_token_expire_seconds=int(read_setting("AUTH_ACCESS_TOKEN_EXPIRE_SECONDS", 28800)),
        auth_refresh_token_expire_seconds=int(read_setting("AUTH_REFRESH_TOKEN_EXPIRE_SECONDS", 604800)),
        auth_auto_bootstrap=str(read_setting("AUTH_AUTO_BOOTSTRAP", True)).lower() in {"1", "true", "yes"},
        redis_host=str(read_setting("REDIS_HOST", "127.0.0.1")),
        redis_port=int(read_setting("REDIS_PORT", 6379)),
        redis_db=int(read_setting("REDIS_DB", 0)),
        redis_password=str(read_setting("REDIS_PASSWORD", "")),
    )
