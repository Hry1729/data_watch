import os
import secrets
import threading
from pathlib import Path

import toml

CONFIG_PATH = Path(__file__).parent.parent.parent / "config.toml"
_CONFIG_LOCK = threading.RLock()

DB_SECTIONS = ("redis", "mysql", "tdengine")
PROCESS_SECTIONS = ("collector", "writer")
EMPTY_PASSWORD_DETAIL = "password 不允许为空串；如需保留原密码请省略 password 字段"


def load_config() -> dict:
    with _CONFIG_LOCK:
        return _load_config_unlocked()


def save_config(cfg: dict) -> None:
    with _CONFIG_LOCK:
        _save_config_unlocked(cfg)


def mutate_config(mutator) -> dict:
    """Load, mutate, and save config under one lock."""
    with _CONFIG_LOCK:
        cfg = _load_config_unlocked()
        mutator(cfg)
        _save_config_unlocked(cfg)
        return cfg


def _load_config_unlocked() -> dict:
    if not CONFIG_PATH.exists():
        cfg = _default_config()
        _save_config_unlocked(cfg)
        return cfg
    return toml.load(CONFIG_PATH)


def _save_config_unlocked(cfg: dict) -> None:
    CONFIG_PATH.parent.mkdir(parents=True, exist_ok=True)
    tmp_path = CONFIG_PATH.with_name(f".{CONFIG_PATH.name}.tmp")
    with open(tmp_path, "w", encoding="utf-8") as f:
        toml.dump(cfg, f)
        f.flush()
        os.fsync(f.fileno())
    os.replace(tmp_path, CONFIG_PATH)


def apply_db_patch(cfg: dict, section: str, patch: dict) -> None:
    """对 db section（redis/mysql/tdengine）做 partial update。

    密码规则：patch 无 password 键 → 保留原 password_enc；password == "" → 抛
    ValueError（端点转 400）；password 非空 → DPAPI 加密写入 password_enc。
    """
    current = cfg.setdefault(section, {})

    for key in ("host", "port", "user", "database"):
        if key in patch and patch[key] is not None:
            current[key] = patch[key]

    if "password" not in patch:
        return

    password = patch["password"]
    if password == "":
        raise ValueError(EMPTY_PASSWORD_DETAIL)

    if password is not None:
        from app.core.security import dpapi_encrypt

        current["password_enc"] = dpapi_encrypt(password)


def apply_process_patch(cfg: dict, section: str, patch: dict) -> None:
    """对进程 section（collector/writer）做 partial update（无密码）。"""
    current = cfg.setdefault(section, {})

    for key in ("executable", "working_dir"):
        if key in patch and patch[key] is not None:
            current[key] = patch[key]


def _default_config() -> dict:
    return {
        "agent": {
            "host": "127.0.0.1",
            "port": 7070,
            "jwt_secret": secrets.token_hex(32),
            "jwt_expire_hours": 8,
            "bootstrap_done": False,
            "log_level": "INFO",
            "runtime_dir": r"C:\ProgramData\DataWatch\runtime",
        },
        "redis": {"host": "127.0.0.1", "port": 6379, "password_enc": ""},
        "mysql": {
            "host": "127.0.0.1",
            "port": 3306,
            "user": "root",
            "database": "device_watch",
            "password_enc": "",
        },
        "tdengine": {
            "host": "127.0.0.1",
            "port": 6041,
            "user": "root",
            "database": "device_data",
            "password_enc": "",
        },
        "collector": {"executable": "", "args": [], "working_dir": ""},
        "writer": {"executable": "", "args": [], "working_dir": ""},
    }
