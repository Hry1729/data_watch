"""TDengine 服务日志只读查看（「数据库日志」页后端）。

仅当 TDENGINE_LOG_DIR 指向一个可读目录（compose 把 TDengine /var/log/taos 以只读卷
挂到 backend）时可用；否则返回 enabled=false 优雅降级，不报错。

安全：只读；文件名取自实时扫描的白名单（拒绝路径穿越）；tail 反向分块读、限行数与字节。
压缩轮转日志（.gz）不支持在线 tail。
"""

from __future__ import annotations

import asyncio
import os
from pathlib import Path
from typing import Any

from app.core.config import get_settings

DEFAULT_LINES = 300
MAX_LINES = 2000
MAX_TAIL_BYTES = 5 * 1024 * 1024  # 单次最多回读 5MB，防超长行/超大文件吃内存
_CHUNK = 64 * 1024

_DISABLED_REASON = "TDENGINE_LOG_DIR 未配置或目录不可读（仅 compose 托管 TDengine 时可用）"


def clamp_lines(value: Any) -> int:
    """把请求行数夹到 [1, MAX_LINES]，非法值回退默认。"""
    try:
        n = int(value)
    except (TypeError, ValueError):
        return DEFAULT_LINES
    if n < 1:
        return 1
    if n > MAX_LINES:
        return MAX_LINES
    return n


def _classify(name: str) -> str:
    low = name.lower()
    if low.startswith("taosdlog"):
        return "taosd"
    if low.startswith("taosadapter"):
        return "taosadapter"
    if low.startswith("taoskeeper"):
        return "taoskeeper"
    if low.startswith("taoslog"):
        return "taosc"
    return "other"


def _resolve_log_dir() -> Path | None:
    raw = (get_settings().tdengine_log_dir or "").strip()
    if not raw:
        return None
    p = Path(raw)
    if not p.is_dir():
        return None
    return p.resolve()


def _scan(log_dir: Path) -> list[dict]:
    files: list[dict] = []
    for entry in log_dir.iterdir():
        if not entry.is_file():
            continue
        try:
            stat = entry.stat()
        except OSError:
            continue
        compressed = entry.name.lower().endswith(".gz")
        files.append(
            {
                "name": entry.name,
                "size": stat.st_size,
                "mtime": _fmt_mtime(stat.st_mtime),
                "compressed": compressed,
                "tailSupported": not compressed,
                "kind": _classify(entry.name),
            }
        )
    # 最近修改的在前，便于前端默认选当前活动日志
    files.sort(key=lambda f: f["mtime"], reverse=True)
    return files


def _fmt_mtime(ts: float) -> str:
    from datetime import datetime

    return datetime.fromtimestamp(ts).strftime("%Y-%m-%d %H:%M:%S")


def _resolve_target(log_dir: Path, name: str) -> Path | None:
    """name 必须是当前目录下实时扫描到的普通文件名；resolve 后仍须落在 log_dir 内。"""
    if not name or "/" in name or "\\" in name or name in (".", ".."):
        return None
    allow = {e.name for e in log_dir.iterdir() if e.is_file()}
    if name not in allow:
        return None
    target = (log_dir / name).resolve()
    try:
        target.relative_to(log_dir)
    except ValueError:
        return None
    return target if target.is_file() else None


def _tail(path: Path, lines: int) -> tuple[list[str], bool]:
    """从文件尾部最多回读 MAX_TAIL_BYTES，返回最后 lines 行与 truncated 标记。

    truncated=True 表示未读完整个文件（命中 5MB 上限），前端可提示"仅显示最近部分"。
    """
    with open(path, "rb") as f:
        f.seek(0, os.SEEK_END)
        file_size = f.tell()
        to_read = min(file_size, MAX_TAIL_BYTES)
        f.seek(file_size - to_read)
        data = f.read(to_read)

    truncated = to_read < file_size
    text = data.decode("utf-8", errors="replace")
    rows = text.splitlines()
    # 从中间起读时首行可能是被截断的半行，丢弃以免误导
    if truncated and rows:
        rows = rows[1:]
    if len(rows) > lines:
        rows = rows[-lines:]
    return rows, truncated


async def list_log_files() -> dict:
    return await asyncio.to_thread(_list_log_files_sync)


def _list_log_files_sync() -> dict:
    log_dir = _resolve_log_dir()
    if log_dir is None:
        return {"enabled": False, "reason": _DISABLED_REASON, "files": []}
    try:
        return {"enabled": True, "reason": "", "files": _scan(log_dir)}
    except OSError as exc:
        return {"enabled": False, "reason": f"读取日志目录失败: {exc}", "files": []}


async def tail_log_file(name: str, lines: int) -> dict:
    return await asyncio.to_thread(_tail_log_file_sync, name, lines)


def _tail_log_file_sync(name: str, lines: int) -> dict:
    base = {
        "enabled": True,
        "reason": "",
        "file": name,
        "lines": [],
        "truncated": False,
        "maxBytes": MAX_TAIL_BYTES,
    }
    log_dir = _resolve_log_dir()
    if log_dir is None:
        return {**base, "enabled": False, "reason": _DISABLED_REASON}

    target = _resolve_target(log_dir, name)
    if target is None:
        return {**base, "reason": "文件不存在或不可读"}
    if name.lower().endswith(".gz"):
        return {**base, "reason": "压缩轮转日志(.gz)暂不支持在线查看"}

    try:
        rows, truncated = _tail(target, lines)
    except OSError as exc:
        return {**base, "reason": f"读取日志失败: {exc}"}
    return {**base, "lines": rows, "truncated": truncated}
