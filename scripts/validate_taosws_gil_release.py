"""Isolated GIL-release probe for the writer's taos-ws-py connector.

Answers one question: while the native connector is blocking, can any other
Python thread in the same interpreter still run?

Method. The workload runs in a *child* process whose heartbeat thread appends
one ``perf_counter()`` per tick to a file and flushes every write. The parent
process only reads that file, so the measurement survives the case it is meant
to detect: a native call that holds the GIL and starves every thread in the
child, including the one that would otherwise report the result.

Scenarios:

  connect-wait  a stalled TCP server accepts the WebSocket handshake and never
                answers, so ``taosws.connect()`` blocks.
  execute-wait  a large multi-table INSERT -- built by the writer's own SQL
                builder -- is executed against a real TDengine, repeatedly.
  control       the child sleeps instead of touching the connector. A healthy
                heartbeat here proves the harness can see one at all.

Headline metric is ``interpreter_unavailable_ms``: wall time inside the tick
span that no heartbeat tick covered. Near zero when the GIL is released around
the blocking call; close to the blocking duration when it is not.

TDengine-side state lives in a probe-only database, dropped at the end only
when this script created it.

Usage (writer venv, TDengine reachable on 127.0.0.1:6041):
    python scripts/validate_taosws_gil_release.py --output report.json
"""
from __future__ import annotations

import argparse
import json
import os
import socket
import subprocess
import sys
import threading
import time
from importlib.metadata import PackageNotFoundError, version
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "writer"))

HEARTBEAT_INTERVAL_S = 0.005
PROBE_DATABASE = "dw_gil_probe"
EXECUTE_PROBE_POINTS = 60
EXECUTE_PROBE_FRAMES = 400
WARMUP_S = 0.3


# ---------------------------------------------------------------------------
# heartbeat
# ---------------------------------------------------------------------------

class RecordingHeartbeat:
    """Appends a timestamp per tick and flushes it, so a wedged interpreter
    still leaves behind a readable record of when it stopped responding."""

    def __init__(self, path: Path, interval_s: float = HEARTBEAT_INTERVAL_S) -> None:
        self.path = path
        self.interval_s = interval_s
        self._stop = threading.Event()
        self._thread = threading.Thread(
            target=self._run, name="gil-heartbeat", daemon=True
        )

    def _run(self) -> None:
        with self.path.open("a", encoding="ascii") as handle:
            while not self._stop.is_set():
                handle.write(f"{time.perf_counter():.6f}\n")
                handle.flush()
                self._stop.wait(self.interval_s)

    def start(self) -> None:
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        self._thread.join(timeout=5)


def read_ticks(path: Path) -> list[float]:
    if not path.exists():
        return []
    ticks = []
    for line in path.read_text(encoding="ascii").splitlines():
        try:
            ticks.append(float(line))
        except ValueError:
            continue
    return ticks


def summarize(ticks: list[float], child_wall_ms: float | None = None) -> dict:
    """Turn a tick trace into the numbers that decide GIL held vs released.

    The healthy tick period is taken from the trace itself (median gap): writing
    and flushing one line per tick costs far more than the nominal interval, so
    the nominal value would misreport every scenario, control included.
    ``child_wall_ms`` is the parent's measurement of how long the child lived and
    is what makes a stalled tail visible — ticks simply stop before it ends.
    """
    if len(ticks) < 2:
        return {
            "ticks": len(ticks),
            "child_wall_ms": child_wall_ms,
            "max_gap_ms": None,
            "interpreter_unavailable_ms": None,
            "heartbeat_ratio": None,
        }
    gaps = [later - earlier for earlier, later in zip(ticks, ticks[1:])]
    ordered = sorted(gaps)
    observed_interval_ms = ordered[len(ordered) // 2] * 1000
    covered_ms = len(ticks) * observed_interval_ms
    window_ms = child_wall_ms if child_wall_ms is not None else covered_ms
    return {
        "ticks": len(ticks),
        "child_wall_ms": round(child_wall_ms, 3) if child_wall_ms is not None else None,
        "observed_interval_ms": round(observed_interval_ms, 3),
        "max_gap_ms": round(ordered[-1] * 1000, 3),
        "p99_gap_ms": round(ordered[min(len(ordered) - 1, int(len(ordered) * 0.99))] * 1000, 3),
        "interpreter_unavailable_ms": round(max(0.0, window_ms - covered_ms), 3),
        "heartbeat_ratio": round(covered_ms / window_ms, 4) if window_ms > 0 else None,
    }


# ---------------------------------------------------------------------------
# child workloads
# ---------------------------------------------------------------------------

class StallingServer:
    """Accepts TCP connections and never answers the WebSocket handshake."""

    def __init__(self) -> None:
        self._listener = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self._listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._listener.bind(("127.0.0.1", 0))
        self._listener.listen(8)
        self.port = self._listener.getsockname()[1]
        self._accepted: list[socket.socket] = []
        self._thread = threading.Thread(target=self._accept_forever, daemon=True)
        self._thread.start()

    def _accept_forever(self) -> None:
        while True:
            try:
                connection, _ = self._listener.accept()
            except OSError:
                return
            self._accepted.append(connection)

    def close(self) -> None:
        try:
            self._listener.close()
        except OSError:
            pass
        for connection in self._accepted:
            try:
                connection.close()
            except OSError:
                pass


def build_probe_rows(frames: int, points: int) -> list[dict]:
    """Synthetic writer rows; same shape consumer._flush hands to the writer.

    Timestamps must be inside the probe database's retention window: TDengine
    rejects writes older than ``KEEP``, which a hardcoded date would trip.
    """
    base_ts = int(time.time() * 1000)
    return [
        {
            "child_table": f"gq{frame}",
            "stable": "sdgq",
            "device_code": f"gq{frame}",
            "ts": base_ts + frame * 100,
            "columns": {f"p{point}": float(point + frame) for point in range(points)},
            "qualities": {str(point + 1): 0 if point % 10 else 192 for point in range(points)},
        }
        for frame in range(frames)
    ]


def child_control(tick_file: Path, stall_ms: int) -> dict:
    heartbeat = RecordingHeartbeat(tick_file)
    heartbeat.start()
    time.sleep(WARMUP_S)
    started = time.perf_counter()
    time.sleep(stall_ms / 1000)
    blocked_ms = (time.perf_counter() - started) * 1000
    time.sleep(WARMUP_S)
    heartbeat.stop()
    return {"scenario": "control", "workload_ms": round(blocked_ms, 3)}


def child_connect_wait(tick_file: Path, port: int, timeout_ms: int) -> dict:
    import taosws

    heartbeat = RecordingHeartbeat(tick_file)
    heartbeat.start()
    time.sleep(WARMUP_S)
    started = time.perf_counter()
    error = None
    try:
        taosws.connect(
            f"ws://root:taosdata@127.0.0.1:{port}"
            f"?read_timeout={timeout_ms}&conn_timeout={timeout_ms}"
        ).close()
    except Exception as exc:
        error = f"{type(exc).__name__}: {exc}"
    elapsed_ms = (time.perf_counter() - started) * 1000
    time.sleep(WARMUP_S)
    heartbeat.stop()
    return {
        "scenario": "connect-wait",
        "workload_ms": round(elapsed_ms, 3),
        "connect_error": error,
    }


def _database_exists(host: str, port: int, database: str) -> bool:
    import taosws

    connection = taosws.connect(f"ws://root:taosdata@{host}:{port}")
    try:
        existing = {str(row[0]).lower() for row in connection.query("SHOW DATABASES")}
    finally:
        connection.close()
    return database.lower() in existing


async def _run_writer_rounds(config: dict, rows: list[dict], repeat: int) -> dict:
    """Drive the production entry point: connect(), then write_multi_table()."""
    from tdengine_writer import TDengineWriter

    writer = TDengineWriter()
    await writer.connect(config)
    try:
        for _ in range(repeat):
            await writer.write_multi_table(rows)
    finally:
        await writer.close()
    return {
        "build_ms_last_round": round(float(getattr(writer, "_last_build_ms", 0.0)), 3),
        "execute_ms_last_round": round(float(getattr(writer, "_last_execute_ms", 0.0)), 3),
        "sql_bytes_last_round": int(getattr(writer, "_last_sql_bytes", 0)),
    }


def child_execute_wait(
    tick_file: Path, host: str, port: int, repeat: int, database: str,
    frames: int, points: int,
) -> dict:
    """Block on the writer's real write path while the heartbeat runs."""
    import asyncio

    from tdengine_writer import TDengineWriter

    config = {
        "tdengine": {
            "host": host, "port": port, "user": "root",
            "password": "taosdata", "database": database,
        },
        "writer": {"stable_names": ["sdgq"]},
    }
    rows = build_probe_rows(frames, points)
    # Recorded before the writer creates anything: the probe only ever removes
    # the database it created, so pointing --probe-database at real data cannot
    # lose it.
    database_pre_existing = _database_exists(host, port, database)
    probe = TDengineWriter()
    probe._database = database
    statements = [sql for _, sql, _ in probe._iter_chunks(rows) if sql is not None]
    sql_bytes = sum(len(sql.encode("utf-8")) for sql in statements)

    heartbeat = RecordingHeartbeat(tick_file)
    heartbeat.start()
    time.sleep(WARMUP_S)
    started = time.perf_counter()
    error: str | None = None
    stats: dict = {}
    try:
        stats = asyncio.run(_run_writer_rounds(config, rows, repeat))
    except Exception as exc:
        error = f"{type(exc).__name__}: {exc}"
    workload_ms = (time.perf_counter() - started) * 1000
    time.sleep(WARMUP_S)
    heartbeat.stop()

    rows_written = None
    try:
        import taosws

        connection = taosws.connect(f"ws://root:taosdata@{host}:{port}")
        try:
            rows_written = int(
                list(connection.query(f"SELECT COUNT(*) FROM {database}.sdgq"))[0][0]
            )
            if not database_pre_existing:
                connection.execute(f"DROP DATABASE IF EXISTS {database}")
        finally:
            connection.close()
    except Exception:
        pass

    return {
        "scenario": "execute-wait",
        "probe_database": database,
        "probe_database_pre_existing": database_pre_existing,
        "frames": frames,
        "points_per_frame": points,
        "tdengine": f"{host}:{port}",
        "statements_per_round": len(statements),
        "sql_bytes_per_round": sql_bytes,
        "rounds": repeat,
        "workload_ms": round(workload_ms, 3),
        "rows_written": rows_written,
        "rows_expected": len(rows),
        "writer_stats": stats,
        "error": error,
    }


# ---------------------------------------------------------------------------
# parent orchestration
# ---------------------------------------------------------------------------

def _child_argv(args: argparse.Namespace, scenario: str, tick_file: Path, port: int | None) -> list[str]:
    argv = [
        sys.executable,
        str(Path(__file__).resolve()),
        "--child",
        scenario,
        "--tick-file",
        str(tick_file),
        "--td-host",
        args.td_host,
        "--td-port",
        str(args.td_port),
        "--connect-timeout-ms",
        str(args.connect_timeout_ms),
        "--stall-ms",
        str(args.stall_ms),
        "--execute-rounds",
        str(args.execute_rounds),
        "--probe-database",
        args.probe_database,
        "--execute-frames",
        str(args.execute_frames),
        "--execute-points",
        str(args.execute_points),
    ]
    if port is not None:
        argv += ["--port", str(port)]
    return argv


def run_scenario(
    args: argparse.Namespace, scenario: str, tick_file: Path, port: int | None, timeout_s: float
) -> dict:
    tick_file.parent.mkdir(parents=True, exist_ok=True)
    if tick_file.exists():
        tick_file.unlink()
    started = time.perf_counter()
    child = subprocess.Popen(
        _child_argv(args, scenario, tick_file, port),
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
    )
    killed = False
    try:
        stdout, stderr = child.communicate(timeout=timeout_s)
    except subprocess.TimeoutExpired:
        child.kill()
        stdout, stderr = child.communicate()
        killed = True
    child_wall_ms = (time.perf_counter() - started) * 1000

    detail: dict = {}
    if stdout and stdout.strip().startswith("{"):
        detail = json.loads(stdout.strip())
    elif stderr:
        detail = {"child_error": stderr.strip()[-2000:]}
    return {
        "killed_by_parent": killed,
        "exit_code": child.returncode,
        **detail,
        **summarize(read_ticks(tick_file), child_wall_ms),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--child", choices=("control", "connect-wait", "execute-wait"))
    parser.add_argument("--tick-file", type=Path)
    parser.add_argument("--port", type=int, default=0)
    parser.add_argument(
        "--scenario",
        choices=("control", "connect-wait", "execute-wait", "all"),
        default="all",
    )
    parser.add_argument("--td-host", default="127.0.0.1")
    parser.add_argument("--td-port", type=int, default=6041)
    parser.add_argument("--connect-timeout-ms", type=int, default=5000)
    parser.add_argument("--stall-ms", type=int, default=6000)
    parser.add_argument("--execute-rounds", type=int, default=3)
    parser.add_argument("--execute-frames", type=int, default=EXECUTE_PROBE_FRAMES)
    parser.add_argument("--execute-points", type=int, default=EXECUTE_PROBE_POINTS)
    # A fresh database per run keeps the probe free of any state left behind by
    # an earlier run; the child drops it again on the way out.
    parser.add_argument("--probe-database", default=f"{PROBE_DATABASE}_{os.urandom(3).hex()}")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()

    if args.child:
        assert args.tick_file is not None, "--tick-file is required in child mode"
        if args.child == "control":
            detail = child_control(args.tick_file, args.stall_ms)
        elif args.child == "connect-wait":
            detail = child_connect_wait(args.tick_file, args.port, args.connect_timeout_ms)
        else:
            detail = child_execute_wait(
                args.tick_file, args.td_host, args.td_port, args.execute_rounds,
                args.probe_database, args.execute_frames, args.execute_points,
            )
        sys.stdout.write(json.dumps(detail))
        sys.stdout.flush()
        return 0

    workdir = (
        args.output.parent / (args.output.stem + "-ticks")
        if args.output
        else ROOT / "outputs" / "taosws-gil" / "ticks"
    )
    report: dict = {
        "heartbeat_interval_ms": round(HEARTBEAT_INTERVAL_S * 1000, 3),
        "python_version": sys.version.split()[0],
        "gil_enabled": getattr(sys, "_is_gil_enabled", lambda: True)(),
        "taos_ws_py_version": _connector_version(),
        "execute_probe": {
            "frames": EXECUTE_PROBE_FRAMES,
            "points_per_frame": EXECUTE_PROBE_POINTS,
            "probe_database": args.probe_database,
        },
    }

    scenarios = (
        ("control", "connect-wait", "execute-wait") if args.scenario == "all" else (args.scenario,)
    )
    for scenario in scenarios:
        server = None
        port = None
        if scenario == "connect-wait":
            server = StallingServer()
            port = server.port
        # connect-wait is expected to block past its deadline; the parent kills
        # the child once the window is long enough to show the stall.
        timeout_s = {
            "control": args.stall_ms / 1000 + WARMUP_S * 2 + 10,
            "connect-wait": args.stall_ms / 1000 + WARMUP_S * 2 + 8,
            "execute-wait": 150.0,
        }[scenario]
        try:
            report[scenario.replace("-", "_")] = run_scenario(
                args, scenario, workdir / f"{scenario}.ticks", port, timeout_s
            )
            if server is not None:
                report[scenario.replace("-", "_")]["stalled_server_port"] = server.port
        finally:
            if server is not None:
                server.close()

    payload = json.dumps(report, ensure_ascii=False, indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(payload + "\n", encoding="utf-8")
    print(payload)
    return 0


def _connector_version() -> str:
    try:
        return version("taos-ws-py")
    except PackageNotFoundError:
        return "unknown"


if __name__ == "__main__":
    raise SystemExit(main())
