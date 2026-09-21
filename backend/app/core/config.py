from __future__ import annotations

import os
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
from typing import Any
from urllib.parse import quote

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
    # 仅读取真实的 .env；.env.example 是模板，绝不作为运行配置来源（避免漏配时回落到模板里的占位/示例值）。
    candidates = [
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
    app_env: str
    log_level: str
    api_prefix: str
    host: str
    port: int
    max_body_bytes: int
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
    tdengine_log_dir: str
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
    writer_redis_group_name: str
    # Backend→Agent 驱动编排服务凭据（不使用用户 JWT）
    agent_base_url: str
    agent_token: str
    cli_api_version: str
    cli_min_version: str
    cli_max_version: str
    cli_release_dir: str
    export_max_active_tasks: int = 4
    export_max_active_tasks_per_user: int = 2
    export_max_retained_tasks: int = 100
    export_max_records: int = 200000
    realtime_ws_max_subscriptions_per_connection: int = 20
    realtime_ws_send_timeout_seconds: float = 2.0
    realtime_ws_reauth_interval_seconds: int = 30
    realtime_ws_reauth_fail_closed: int = 3
    password_worker_max_workers: int = 2
    password_worker_max_queue: int = 8
    login_fail_max: int = 5
    login_fail_window_seconds: int = 60

    @property
    def mysql_async_dsn(self) -> str:
        # user/password 经 URL 编码，避免密码含 @ : / # % 等字符时 DSN 被解析错误。
        user = quote(self.mysql_user, safe="")
        password = quote(self.mysql_password, safe="")
        return (
            f"mysql+aiomysql://{user}:{password}"
            f"@{self.mysql_host}:{self.mysql_port}/{self.mysql_database}"
            f"?charset={self.mysql_charset}"
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
        app_env=str(read_setting("BACKEND_ENV", "production")).strip().lower(),
        log_level=str(read_setting("BACKEND_LOG_LEVEL", runtime_defaults.get("log_level", "INFO"))),
        api_prefix=str(read_setting("BACKEND_API_PREFIX", "/api/device-monitor")),
        host=str(read_setting("BACKEND_HOST", "0.0.0.0")),
        port=int(read_setting("BACKEND_PORT", 8000)),
        max_body_bytes=int(
            read_setting(
                "BACKEND_MAX_BODY_BYTES",
                read_setting("DATAWATCH_MAX_HTTP_BODY_BYTES", 4 * 1024 * 1024),
            )
        ),
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
        tdengine_log_dir=str(read_setting("TDENGINE_LOG_DIR", "")),
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
        writer_redis_group_name=str(
            read_setting("WRITER_REDIS_GROUP_NAME", "writer_cg")
        ),
        agent_base_url=str(
            read_setting("DATAWATCH_AGENT_BASE_URL", "http://127.0.0.1:7070")
        ).rstrip("/"),
        agent_token=str(read_setting("DATAWATCH_AGENT_SERVICE_TOKEN", "")),
        cli_api_version=str(read_setting("CLI_API_VERSION", "1.0")),
        cli_min_version=str(read_setting("CLI_MIN_VERSION", "0.2.0")),
        cli_max_version=str(read_setting("CLI_MAX_VERSION", "0.x")),
        cli_release_dir=str(
            read_setting(
                "CLI_RELEASE_DIR",
                str(Path(__file__).resolve().parents[2] / "cli-releases"),
            )
        ),
        export_max_active_tasks=int(read_setting("EXPORT_MAX_ACTIVE_TASKS", 4)),
        export_max_active_tasks_per_user=int(
            read_setting("EXPORT_MAX_ACTIVE_TASKS_PER_USER", 2)
        ),
        export_max_retained_tasks=int(read_setting("EXPORT_MAX_RETAINED_TASKS", 100)),
        export_max_records=int(read_setting("EXPORT_MAX_RECORDS", 200000)),
        realtime_ws_max_subscriptions_per_connection=int(
            read_setting("REALTIME_WS_MAX_SUBSCRIPTIONS_PER_CONNECTION", 20)
        ),
        realtime_ws_send_timeout_seconds=float(
            read_setting("REALTIME_WS_SEND_TIMEOUT_SECONDS", 2)
        ),
        realtime_ws_reauth_interval_seconds=int(
            read_setting("REALTIME_WS_REAUTH_INTERVAL_SECONDS", 30)
        ),
        realtime_ws_reauth_fail_closed=int(
            read_setting("REALTIME_WS_REAUTH_FAIL_CLOSED", 3)
        ),
        password_worker_max_workers=int(
            read_setting("PASSWORD_WORKER_MAX_WORKERS", 2)
        ),
        password_worker_max_queue=int(
            read_setting("PASSWORD_WORKER_MAX_QUEUE", 8)
        ),
        login_fail_max=int(read_setting("LOGIN_FAIL_MAX", 5)),
        login_fail_window_seconds=int(
            read_setting("LOGIN_FAIL_WINDOW_SECONDS", 60)
        ),
    )
