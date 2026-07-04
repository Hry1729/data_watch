#!/usr/bin/env python3
"""GE SRTP RTT 基准测试 - 只读，不写入。

Purpose: measure single-block RTT to verify whether 124 blocks x RTT
accounts for the 4-5s scan time observed on device_id=30.

Usage:
    python bench_ge_srtp.py <host> [port=18245] [slot=1] [rounds=50]

Run with collector venv (cwd=collector/tests):
    ..\\.venv-pack\\Scripts\\python.exe bench_ge_srtp.py 192.168.x.x 18245 1 50
"""
import os
import sys
import time
import statistics

_HERE = os.path.dirname(os.path.abspath(__file__))
# PyInstaller frozen: resources live under sys._MEIPASS; plain python: 脚本在 tests/ 下，drivers 在上一级。
_BASE = getattr(sys, "_MEIPASS", os.path.join(_HERE, ".."))
_GESRTP = os.path.join(_BASE, "drivers", "gesrtp-py-main")
if _GESRTP not in sys.path:
    sys.path.insert(0, _GESRTP)

# 静默 gesrtp INFO 日志（每次读取都会记录"Reading register..."）。
# 它们会干扰计时并刷屏控制台。
import logging
logging.getLogger("src").setLevel(logging.WARNING)
logging.getLogger().setLevel(logging.WARNING)

from src.driver import GE_SRTP_Driver  # type: ignore


def timed(fn):
    t0 = time.perf_counter()
    r = fn()
    return (time.perf_counter() - t0) * 1000.0, r


def stats(label, times_ms):
    s = sorted(times_ms)
    n = len(s)
    p95 = s[min(int(n * 0.95), n - 1)] if n > 1 else s[0]
    print(f"  {label:24s} n={n:3d}  min={s[0]:7.2f}ms  max={s[-1]:7.2f}ms  "
          f"avg={statistics.mean(s):7.2f}ms  p50={s[n // 2]:7.2f}ms  p95={p95:7.2f}ms")


def bench(fn, rounds):
    times = []
    for _ in range(rounds):
        ms, _ = timed(fn)
        times.append(ms)
    return times


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    host = sys.argv[1]
    port = int(sys.argv[2]) if len(sys.argv) > 2 else 18245
    slot = int(sys.argv[3]) if len(sys.argv) > 3 else 1
    rounds = int(sys.argv[4]) if len(sys.argv) > 4 else 50

    print("=== GE SRTP RTT bench ===")
    print(f"host={host} port={port} slot={slot} rounds={rounds}")
    print("(READ ONLY - no writes sent)\n")

    drv = GE_SRTP_Driver(host=host, port=port, slot=slot)
    ms, _ = timed(drv.connect)
    print(f"connect: {ms:.2f}ms\n")

    # [1] 单字寄存器读取（count=1）- 常见的分散 %R 情况
    print("[1] read_register(offset=0, count=1)")
    stats("count=1", bench(lambda: drv.read_register(0, 1), rounds))

    # [2] 最大块寄存器读取（count=125）
    print("\n[2] read_register(offset=0, count=125)")
    stats("count=125", bench(lambda: drv.read_register(0, 125), rounds))

    # [3] 离散输入位读取（count=64 - RX3i 最小值）
    print("\n[3] read_discrete_input(offset=0, count=64, bit)")
    stats("discrete bit", bench(lambda: drv.read_discrete_input(0, 64, mode='bit'), rounds))

    # [4] 模拟真实扫描：124 个串行块（offset 0..123, count=1）
    # 模拟 device_id=30 的 124 块串行扫描
    print(f"\n[4] simulate 124 sequential blocks (offset 0..123, count=1)")
    t0 = time.perf_counter()
    errs = 0
    for off in range(124):
        try:
            drv.read_register(off, 1)
        except Exception as e:
            errs += 1
            if errs <= 3:
                print(f"  block offset={off} raised: {e!r}")
    total = (time.perf_counter() - t0) * 1000.0
    print(f"  total={total:.1f}ms  avg={total / 124:.2f}ms  errors={errs}")
    print(f"  >>> compare total to the 4-5s observed scan time <<<")

    # [5] 探测越界地址 - 暴露 NACK / 重试 / 超时行为
    print(f"\n[5] probe out-of-range offset (read_register(99999, 1))")
    try:
        ms, r = timed(lambda: drv.read_register(99999, 1))
        print(f"  returned in {ms:.2f}ms, value={r!r}")
    except Exception as e:
        print(f"  raised: {e!r}")

    drv.disconnect()
    print("\ndone")


if __name__ == "__main__":
    main()
