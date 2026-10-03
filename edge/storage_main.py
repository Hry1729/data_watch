from __future__ import annotations

import argparse
import errno
import ipaddress
import logging
import os
import signal
import socket
import sys
from pathlib import Path
import threading


REPO_ROOT = Path(__file__).resolve().parents[1]
COLLECTOR_ROOT = REPO_ROOT / "collector"
for source_root in (REPO_ROOT, COLLECTOR_ROOT):
    if str(source_root) not in sys.path:
        sys.path.insert(0, str(source_root))

from edge.app.paths import edge_database_path
from edge.app.storage_service import (
    IpcAuthenticator,
    StorageEngine,
    StorageHttpServer,
    default_token_file,
    ensure_token_file,
)


def _is_loopback(host: str) -> bool:
    candidate = host.strip().lower()
    if not candidate:
        # ``socket.bind(("", port))`` means INADDR_ANY, i.e. every interface, so an
        # empty (or whitespace-only) host is the opposite of loopback. Anything that
        # is not provably loopback must pass the explicit --allow-non-loopback gate.
        return False
    try:
        if ipaddress.ip_address(candidate).is_loopback:
            return True
    except ValueError:
        pass
    # A single trailing dot is the DNS root form, so ``localhost.`` names the same
    # host as ``localhost``. It is spelled out separately because, unlike a name,
    # an IP literal with a trailing dot (``127.0.0.1.``) is *not* resolvable and
    # must stay rejected rather than be accepted and then fail at bind time.
    if candidate.removesuffix(".") != "localhost":
        return False
    # A name proves nothing on its own: resolve it the way the bind will (AF_INET,
    # the address family of StorageHttpServer) and accept only when every answer is
    # loopback, so a hosts file pointing "localhost" at another interface is refused.
    try:
        infos = socket.getaddrinfo("localhost", None, socket.AF_INET, socket.SOCK_STREAM)
    except OSError:
        return False
    addresses = [info[4][0] for info in infos]
    return bool(addresses) and all(
        ipaddress.ip_address(address).is_loopback for address in addresses
    )


def _bind_error_message(host: str, port: int, exc: OSError) -> str:
    """Classify a failed IPC bind so the operator knows what to change.

    The classes are the ones this endpoint can actually hit: a host that cannot be
    resolved (which is how every IPv6 literal fails on an AF_INET endpoint), an address
    that is not on this host, and a port that is taken or not permitted.
    """
    if isinstance(exc, socket.gaierror):
        reason = (
            "this IPC endpoint is IPv4-only (ThreadingHTTPServer / AF_INET), so this host "
            "cannot be bound here; IPv6 (e.g. ::1) is not supported, use an IPv4 address "
            "such as 127.0.0.1"
        )
    elif exc.errno == errno.EADDRNOTAVAIL:
        reason = "that address does not belong to this host"
    elif exc.errno in (errno.EADDRINUSE, errno.EACCES):
        reason = "the port is already in use, or this process is not permitted to bind it"
    else:
        reason = "the socket could not be bound"
    return (
        f"Cannot bind Edge Storage IPC to {host}:{port}: {reason} "
        f"[{type(exc).__name__}: {exc}]"
    )


def _resolve_data_root(explicit: str | None) -> Path:
    """Resolve the Edge data root without silently targeting a real installation.

    ``DATAWATCH_DATA_ROOT`` is injected by the installer (NSSM ``AppEnvironmentExtra``)
    for the managed edge processes. When it is missing we must not guess: falling back
    to the installed root would let a source-tree / bare run read or modify a **real**
    installation. A packaged (frozen) build keeps the historical default.
    """
    for candidate in (explicit, os.environ.get("DATAWATCH_DATA_ROOT")):
        if candidate and candidate.strip():
            return Path(candidate)
    if getattr(sys, "frozen", False):
        return Path(r"C:\ProgramData\DataWatch Edge")
    raise ValueError(
        "DATAWATCH_DATA_ROOT is not set and this is not a packaged build. Refusing to fall "
        r"back to the installed data root (C:\ProgramData\DataWatch Edge), because a source "
        "run would then read or modify a real installation. Set DATAWATCH_DATA_ROOT or pass "
        "--data-root <dir>."
    )


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="DataWatch Edge Storage")
    parser.add_argument("--host", default=os.environ.get("DATAWATCH_EDGE_STORAGE_HOST", "127.0.0.1"))
    parser.add_argument("--port", type=int, default=int(os.environ.get("DATAWATCH_EDGE_STORAGE_PORT", "7073")))
    parser.add_argument("--queue-capacity", type=int, default=int(os.environ.get("DATAWATCH_EDGE_STORAGE_QUEUE_CAPACITY", "2048")))
    # 数据根的解析必须显式：源码裸跑不得退回真实安装目录（见 _resolve_data_root）。
    parser.add_argument(
        "--data-root",
        default=None,
        help="Edge data root. Overrides DATAWATCH_DATA_ROOT; required when not a packaged build.",
    )
    parser.add_argument(
        "--shutdown-timeout-seconds",
        type=float,
        default=float(os.environ.get("DATAWATCH_EDGE_STORAGE_SHUTDOWN_TIMEOUT_SECONDS", "30")),
    )
    parser.add_argument(
        "--rotate-ipc-tokens",
        action="store_true",
        help="Regenerate all Edge Storage IPC role tokens atomically, then exit. "
        "The running Storage picks the change up within ~1s (revocation without restart).",
    )
    # The IPC endpoint has no transport encryption and authenticates callers only
    # with a local token file, so exposing it beyond loopback must be deliberate.
    parser.add_argument(
        "--allow-non-loopback",
        action="store_true",
        default=os.environ.get("DATAWATCH_EDGE_STORAGE_ALLOW_NON_LOOPBACK", "").strip().lower()
        in {"1", "true", "yes", "on"},
        help="Explicitly permit binding the Edge Storage IPC endpoint to a non-loopback address.",
    )
    return parser


def main() -> int:
    args = _parser().parse_args()
    logging.basicConfig(
        level=os.environ.get("DATAWATCH_LOG_LEVEL", "INFO"),
        format="%(asctime)s [%(levelname)s] %(name)s - %(message)s",
    )
    if args.rotate_ipc_tokens:
        # 轮换/吊销入口：只在解析数据根之后写盘，失败即退出，不启动服务。
        try:
            _resolve_data_root(args.data_root)
        except ValueError as exc:
            logging.getLogger("edge.storage").error("%s", exc)
            return 2
        target = ensure_token_file(rotate=True)
        logging.getLogger("edge.storage").warning(
            "Rotated Edge Storage IPC tokens in %s. The running Edge Storage reloads this file "
            "within ~1s and stops accepting the previous tokens; Collector/Uploader/Agent reload "
            "it automatically after a 401. Restart any component that injects its token through "
            "DATAWATCH_EDGE_STORAGE_TOKEN.",
            target,
        )
        return 0
    if not _is_loopback(args.host) and not args.allow_non_loopback:
        logging.getLogger("edge.storage").error(
            "Refusing to bind Edge Storage IPC to non-loopback address %s: the endpoint has no "
            "transport encryption and would be reachable from the network. Pass "
            "--allow-non-loopback (or DATAWATCH_EDGE_STORAGE_ALLOW_NON_LOOPBACK=1) to override.",
            args.host,
        )
        return 2
    try:
        data_root = _resolve_data_root(args.data_root)
    except ValueError as exc:
        logging.getLogger("edge.storage").error("%s", exc)
        return 2
    engine = StorageEngine(
        edge_database_path(),
        max_queue=args.queue_capacity,
        shutdown_timeout_seconds=args.shutdown_timeout_seconds,
    )
    # Resolve the IPC credential *outside* the try below, so that try covers the bind
    # only: an unreadable token file must surface as itself, never as a bind failure.
    authenticator = IpcAuthenticator.from_file()
    # Bind before ``engine.start()``: binding is the step that can fail for reasons
    # outside our control (``::1`` on this IPv4-only endpoint, an address that is not on
    # this host, a port already in use). ``start()`` spawns a non-daemon writer thread,
    # so an unhandled bind error afterwards would leave the process hung with no listener
    # at all; failing here also means no database file is created.
    try:
        server = StorageHttpServer((args.host, args.port), engine, authenticator)
    except OSError as exc:
        logging.getLogger("edge.storage").error(
            "%s", _bind_error_message(args.host, args.port, exc)
        )
        return 2
    engine.start()
    server.timeout = 0.5
    stopped = threading.Event()

    def stop(*_args) -> None:
        stopped.set()

    for sig in (signal.SIGINT, signal.SIGTERM):
        signal.signal(sig, stop)

    runtime_dir = Path(os.environ.get("DATAWATCH_RUNTIME_DIR") or data_root / "runtime")
    shutdown_flag = runtime_dir / "storage.shutdown_flag"
    shutdown_flag.unlink(missing_ok=True)

    logging.getLogger("edge.storage").info(
        "Edge Storage listening on http://%s:%s database=%s ipc_token_file=%s",
        args.host,
        args.port,
        edge_database_path(),
        default_token_file(),
    )
    try:
        while not stopped.is_set() and not shutdown_flag.exists():
            server.handle_request()
    finally:
        server.server_close()
        engine.close()
        shutdown_flag.unlink(missing_ok=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
