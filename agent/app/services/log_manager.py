from __future__ import annotations

import asyncio
import os
import re
import threading
from collections import deque
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Literal, cast

from app.schemas.models import LogLine

LogSource = Literal["collector", "writer"]

_VALID_NAMES: set[str] = {"collector", "writer"}
_LEVEL_RE = re.compile(r"\b(DEBUG|INFO|WARNING|WARN|ERROR|CRITICAL)\b")
_QUEUE_MAXSIZE = 100
_HISTORY_MAXLEN = 100
_TAIL_SLEEP_SECONDS = 0.1


@dataclass
class _TailState:
    path: Path
    loop: asyncio.AbstractEventLoop
    stop_event: threading.Event
    thread: threading.Thread


class LogManager:
    def __init__(self) -> None:
        self._history: dict[LogSource, deque[LogLine]] = {
            "collector": deque(maxlen=_HISTORY_MAXLEN),
            "writer": deque(maxlen=_HISTORY_MAXLEN),
        }
        self._subscribers: dict[LogSource, set[asyncio.Queue[LogLine]]] = {
            "collector": set(),
            "writer": set(),
        }
        self._states: dict[LogSource, _TailState] = {}
        self._lock = threading.RLock()

    def attach(self, name: str, log_path: Path, loop: asyncio.AbstractEventLoop) -> None:
        source = self._validate_name(name)

        if loop.is_closed():
            raise RuntimeError(f"cannot attach log tail for {source}: event loop is closed")

        path = Path(log_path)

        with self._lock:
            existing = self._states.get(source)
            if existing is not None and existing.thread.is_alive():
                return

            stop_event = threading.Event()
            thread = threading.Thread(
                target=self._tail_worker,
                args=(source, path, loop, stop_event),
                name=f"log-tail-{source}",
                daemon=True,
            )
            self._states[source] = _TailState(
                path=path,
                loop=loop,
                stop_event=stop_event,
                thread=thread,
            )

        thread.start()

    def detach(self, name: str) -> None:
        source = self._validate_name(name)

        with self._lock:
            state = self._states.pop(source, None)

        if state is None:
            return

        state.stop_event.set()

        if state.thread.is_alive() and threading.current_thread() is not state.thread:
            state.thread.join(timeout=1.0)

    def get_history(self, name: str) -> list[LogLine]:
        source = self._validate_name(name)

        # CPython 下 deque.append（tail 线程）与 list(deque)（事件循环线程）的并发快照
        # 在这里可接受：最多出现极短窗口内的快照差异，不会破坏数据结构。
        return list(self._history[source])

    def subscribe(self, name: str) -> asyncio.Queue[LogLine]:
        source = self._validate_name(name)
        queue: asyncio.Queue[LogLine] = asyncio.Queue(maxsize=_QUEUE_MAXSIZE)

        with self._lock:
            self._subscribers[source].add(queue)

        return queue

    def unsubscribe(self, name: str, q: asyncio.Queue[LogLine]) -> None:
        source = self._validate_name(name)

        with self._lock:
            self._subscribers[source].discard(q)

    def _tail_worker(
        self,
        source: LogSource,
        path: Path,
        loop: asyncio.AbstractEventLoop,
        stop_event: threading.Event,
    ) -> None:
        start_at_end = True

        while not stop_event.is_set():
            if not path.exists():
                stop_event.wait(_TAIL_SLEEP_SECONDS)
                continue

            try:
                with path.open("r", encoding="utf-8", errors="replace") as fp:
                    if start_at_end:
                        fp.seek(0, os.SEEK_END)
                    else:
                        fp.seek(0, os.SEEK_SET)
                        start_at_end = True

                    while not stop_event.is_set():
                        line = fp.readline()

                        if line:
                            entry = self._parse_line(source, line)
                            self._history[source].append(entry)
                            self._broadcast(loop, source, entry)
                            continue

                        try:
                            position = fp.tell()
                            size = path.stat().st_size
                        except OSError:
                            start_at_end = False
                            break

                        if position > size:
                            start_at_end = False
                            break

                        stop_event.wait(_TAIL_SLEEP_SECONDS)

            except FileNotFoundError:
                stop_event.wait(_TAIL_SLEEP_SECONDS)
            except OSError:
                stop_event.wait(_TAIL_SLEEP_SECONDS)

    def _broadcast(
        self,
        loop: asyncio.AbstractEventLoop,
        source: LogSource,
        entry: LogLine,
    ) -> None:
        with self._lock:
            subscribers = tuple(self._subscribers[source])

        for queue in subscribers:
            try:
                loop.call_soon_threadsafe(self._put_nowait_safely, queue, entry)
            except RuntimeError:
                pass

    @staticmethod
    def _put_nowait_safely(
        queue: asyncio.Queue[LogLine],
        entry: LogLine,
    ) -> None:
        try:
            queue.put_nowait(entry)
        except Exception:
            pass

    @staticmethod
    def _parse_line(source: LogSource, line: str) -> LogLine:
        message = line.rstrip("\r\n")
        match = _LEVEL_RE.search(message)

        level = "INFO"
        if match is not None:
            token = match.group(1)
            if token == "WARNING":
                level = "WARN"
            elif token == "CRITICAL":
                level = "ERROR"
            else:
                level = token

        return LogLine(
            timestamp=datetime.now().astimezone().isoformat(),
            level=level,
            source=source,
            message=message,
        )

    @staticmethod
    def _validate_name(name: str) -> LogSource:
        if name not in _VALID_NAMES:
            raise ValueError(f"unsupported log source: {name}")

        return cast(LogSource, name)
