"""从 writer 容器日志里抽出性能 JSON（最终一份），打印关键计时与计数。

用法：python extract_writer_timings.py writer-joint-run2.log
"""
from __future__ import annotations

import json
from pathlib import Path
import re
import sys

KEYS = ("flush_ms", "td_execute_ms", "write_batch_ms", "ack_ms", "build_ms")


def read_log(path: str) -> str:
    """PowerShell 的 `>` 重定向在 Windows 上默认写 UTF-16LE，必须按 BOM 嗅探。"""
    raw = Path(path).read_bytes()
    if raw[:2] in (b"\xff\xfe", b"\xfe\xff"):
        return raw.decode("utf-16")
    if raw[:3] == b"\xef\xbb\xbf":
        return raw[3:].decode("utf-8")
    try:
        return raw.decode("utf-8")
    except UnicodeDecodeError:
        return raw.decode("utf-16", errors="replace")


def main() -> int:
    text = read_log(sys.argv[1])
    # 该 JSON 结构有瑕疵（字符串里有裸控制字符/截断），整体解析不可靠；
    # 这些字段用正则逐个抓，足够且稳健。
    marker = text.rfind("[performance-aggregate]")
    if marker < 0:
        print("日志里没有 [performance-aggregate]；行数 =", len(text.splitlines()))
        return 1
    tail = text[marker:]
    for name in KEYS:
        block = re.search(rf'"{name}":\s*\{{(.*?)\}}', tail, re.S)
        if not block:
            print(f"{name:16s} (未找到)")
            continue
        fields = dict(re.findall(r'"(\w+)":\s*([0-9.]+)', block.group(1)))
        print(f"{name:16s} count={fields.get('count')} avg={fields.get('average')} "
              f"p95={fields.get('p95')} p99={fields.get('p99')} max={fields.get('max')}")
    for key in ("sql_value_rows_succeeded", "sql_quality_rows_succeeded",
                "write_batch_failure_count", "flush_count", "peak_buffer_rows"):
        hit = re.search(rf'"{key}":\s*([0-9]+)', tail)
        print(f"{key:28s} {hit.group(1) if hit else '(未找到)'}")
    rows = re.search(r'"rows_per_flush":\s*\{(.*?)\}', tail, re.S)
    if rows:
        fields = dict(re.findall(r'"(\w+)":\s*([0-9.]+)', rows.group(1)))
        print(f"rows_per_flush avg={fields.get('average')} max={fields.get('max')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
