"""测试用受管子进程桩，替代 collector/writer，供 ProcessManager 表征测试使用。

行为：
  默认            空转，直到 --flag 指向的 shutdown_flag 文件出现则优雅退出(0)
  --ignore-flag   忽略 flag，永不自行退出（用于测 terminate 强杀路径）
  --exit-after N  运行 N 秒后退出
  --exit-code C   配合 --exit-after，退出码（默认 0；非 0 用于测 crashed）
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
import time


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--flag", default=None)
    p.add_argument("--ignore-flag", action="store_true")
    p.add_argument("--exit-after", type=float, default=None)
    p.add_argument("--exit-code", type=int, default=0)
    p.add_argument("--argv-out", default=None)  # 启动时把 argv 落盘，供测 extra_args 透传
    p.add_argument("--spawn-child", action="store_true")
    a, _ = p.parse_known_args()

    if a.argv_out:
        with open(a.argv_out, "w", encoding="utf-8") as f:
            f.write("\n".join(sys.argv))

    if a.spawn_child:
        subprocess.Popen([sys.executable, __file__, "--ignore-flag"])

    start = time.time()
    while True:
        if a.exit_after is not None and (time.time() - start) >= a.exit_after:
            sys.exit(a.exit_code)
        if a.flag and not a.ignore_flag and os.path.exists(a.flag):
            sys.exit(0)
        time.sleep(0.05)


if __name__ == "__main__":
    main()
