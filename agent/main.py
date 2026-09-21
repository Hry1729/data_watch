import logging
import signal
import sys
from pathlib import Path

_REPO_ROOT = Path(__file__).resolve().parent.parent
if (_REPO_ROOT / "data_watch_driver_catalog.py").is_file():
    sys.path.insert(0, str(_REPO_ROOT))

import uvicorn

from app.factory import create_app
from app.core.config import load_config

app = create_app()


logger = logging.getLogger(__name__)


def _handle_shutdown_signal(signum, _frame):
    logger.info("Received signal %s, stopping DataWatch Agent...", signum)
    raise KeyboardInterrupt


def _register_signal_handlers() -> None:
    for sig_name in ("SIGTERM", "SIGBREAK"):
        sig = getattr(signal, sig_name, None)
        if sig is None:
            continue
        try:
            signal.signal(sig, _handle_shutdown_signal)
        except (OSError, ValueError):
            pass


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    _register_signal_handlers()
    # 传 app 对象而非 "main:app" 字符串：PyInstaller frozen 下无独立 main 模块可重新导入，
    # 字符串形式会触发 ModuleNotFoundError。对象形式不依赖按名导入（也不支持 reload/workers，
    # Agent 单进程服务不需要）。
    cfg = load_config()
    uvicorn.run(
        app,
        host="127.0.0.1",
        port=int(cfg["agent"]["port"]),
        log_level="info",
    )
