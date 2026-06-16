import os
import secrets
import sys
import threading
from pathlib import Path

import toml


def data_root() -> Path:
    """可写数据根：环境变量 DATAWATCH_DATA_ROOT 优先；frozen 默认 C:\\ProgramData\\DataWatch；
    dev 回退到 agent 包目录（保持 config.toml 在 agent/config.toml）。

    frozen 下 __file__ 位于解包临时目录，不可作持久/可写配置根；Program Files 对服务账户
    只读，故 bootstrap 写入的 config.toml 必须落在 ProgramData。安装器/NSSM 注入
    DATAWATCH_DATA_ROOT 覆盖默认值。
    """
    env = os.environ.get("DATAWATCH_DATA_ROOT")
    if env:
        return Path(env)
    if getattr(sys, "frozen", False):
        return Path(r"C:\ProgramData\DataWatch")
    return Path(__file__).parent.parent.parent


def install_root() -> Path:
    """只读安装根：环境变量 DATAWATCH_INSTALL_ROOT 优先；frozen 取 exe 父目录；dev 取仓库根。

    安装器/NSSM 注入 DATAWATCH_INSTALL_ROOT，使布局与 frozen 推导解耦——agent.exe 放
    <root>\\agent\\ 子目录时 exe 父目录指向 agent\\ 而非根，故以环境变量为准；缺失时
    回退 frozen(exe 父目录)/dev(仓库根)。
    """
    env = os.environ.get("DATAWATCH_INSTALL_ROOT")
    if env:
        return Path(env).resolve()
    if getattr(sys, "frozen", False):
        return Path(sys.executable).resolve().parent
    return Path(__file__).resolve().parents[3]


CONFIG_PATH = data_root() / "config.toml"
_CONFIG_LOCK = threading.RLock()

DB_SECTIONS = ("redis", "mysql", "tdengine")

_MODULE_EXE = {
    "collector": "data-watch-collector.exe",
    "writer": "data-watch-writer.exe",
}


def module_paths(name: str) -> tuple[Path, Path]:
    """(executable, working_dir)：二进制在只读安装根 <install_root>/<name>/<exe>；工作目录
    (含 config/settings.toml)在可写数据根 <data_root>/<name>。据此 config.toml 不存绝对路径，
    按目标机动态派生。
    """
    return install_root() / name / _MODULE_EXE[name], data_root() / name
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
        # 后端地址运行期可配（引导页录入），不再打包进 app 前端；app 启动时经
        # GET /bootstrap/runtime-config 读取。base_url 无密码，明文存储。
        "backend": {"base_url": "http://127.0.0.1:8000"},
        "collector": {"executable": "", "args": [], "working_dir": ""},
        "writer": {"executable": "", "args": [], "working_dir": ""},
    }
