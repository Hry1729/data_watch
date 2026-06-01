import logging
import signal

import uvicorn

from app.factory import create_app

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
    uvicorn.run(
        "main:app",
        host="127.0.0.1",
        port=7070,
        log_level="info",
    )
