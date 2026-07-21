#!/usr/bin/env python3
"""GE SRTP 离散量位模式与字节模式诊断 - 只读，不写入。

Purpose: a point (e.g. %Q2019) shows wrong value after switching discrete
reads to byte mode. Use field-validated BIT mode as ground truth, read the
same byte in BYTE mode, and compare bit-by-bit to reveal:
  - intra-byte bit order (LSB-first vs MSB-first)
  - +-1 offset base mismatch
  - NACK / out-of-range failures (stale value cause)

Usage:
    diag_ge_discrete.exe <host> <area> <point> [port=18245] [slot=1]
Example:
    diag_ge_discrete.exe 172.25.2.20 Q 2019
"""
import os
import sys

_HERE = os.path.dirname(os.path.abspath(__file__))
# frozen: resources under _MEIPASS; plain: 脚本在 tests/，drivers 在上一级
_BASE = getattr(sys, "_MEIPASS", os.path.join(_HERE, ".."))
_GESRTP = os.path.join(_BASE, "drivers", "gesrtp-py-main")
if _GESRTP not in sys.path:
    sys.path.insert(0, _GESRTP)

import logging
logging.getLogger("src").setLevel(logging.WARNING)
logging.getLogger().setLevel(logging.WARNING)

from src.driver import GE_SRTP_Driver  # type: ignore


def main():
    if len(sys.argv) < 4:
        print(__doc__)
        sys.exit(1)
    host = sys.argv[1]
    area = sys.argv[2].upper()
    point = int(sys.argv[3])
    port = int(sys.argv[4]) if len(sys.argv) > 4 else 18245
    slot = int(sys.argv[5]) if len(sys.argv) > 5 else 1

    drv = GE_SRTP_Driver(host=host, port=port, slot=slot)
    drv.connect()

    read_fn = {
        "I": drv.read_discrete_input,
        "Q": drv.read_discrete_output,
        "M": drv.read_discrete_internal,
    }.get(area)
    if read_fn is None:
        print(f"unsupported area: {area} (use I/Q/M)")
        sys.exit(1)

    byte_idx = point // 8          # collector's assumption: bit N -> byte N//8
    bit_in_byte = point % 8        # ... at LSB position N%8
    base_bit = byte_idx * 8        # first bit of that byte in offset space

    print(f"=== GE discrete diag: %{area}{point} @ {host} ===")
    print(f"collector assumption: byte {byte_idx}, LSB bit {bit_in_byte}\n")

    # [1] 整个字节的位模式基准真相（8 个点 base_bit..base_bit+7）。
    # 单次 64 位窗口读取恰好结束于字节末尾，避免超出
    # 顶部附近配置的区域大小（位模式最小请求为 64 位）。
    win_start = max(0, base_bit + 8 - 64)
    print(f"[1] bit-mode window read: offset={win_start}, count=64")
    bits_by_offset = {}
    try:
        win = read_fn(win_start, 64, mode="bit")
        win = win if isinstance(win, list) else [win]
        for j, v in enumerate(win):
            bits_by_offset[win_start + j] = bool(v)
        row = " ".join(
            f"{base_bit + k}={int(bits_by_offset.get(base_bit + k, -1))}" for k in range(8)
        )
        print(f"    bits {base_bit}..{base_bit + 7}: {row}")
        tgt = bits_by_offset.get(point)
        print(f"    >>> bit-mode ground truth %{area}{point} = {tgt}")
    except Exception as e:
        print(f"    bit-mode window FAILED: {e!r}")

    # [2] 目标直接单 bit 读取（可能超出区域顶部：最小 64 位）
    print(f"\n[2] bit-mode single read: offset={point}, count=1")
    try:
        v = read_fn(point, 1, mode="bit")
        print(f"    %{area}{point} = {v}")
    except Exception as e:
        print(f"    FAILED (expected near area top, min request 64 bits): {e!r}")

    # [3] 字节模式读取：恰好 8 字节，结束于目标字节之后，
    # 因此驱动的最小 8 字节请求不会超出区域顶部。
    start_byte = max(0, byte_idx - 6)
    tb = None
    print(f"\n[3] byte-mode read: byte offset={start_byte}, count=8")
    try:
        bs = read_fn(start_byte, 8, mode="byte")
        bs = bs if isinstance(bs, list) else [bs]
        for j, b in enumerate(bs):
            bno = start_byte + j
            if byte_idx - 1 <= bno <= byte_idx + 1:
                marker = "  <-- target byte" if bno == byte_idx else ""
                print(f"    byte {bno} = 0x{int(b):02X} = {int(b):08b} (MSB..LSB){marker}")
        k = byte_idx - start_byte
        if 0 <= k < len(bs):
            tb = int(bs[k])
            print(f"\n    target byte LSB-first bits: "
                  + " ".join(f"[{j}]={(tb >> j) & 1}" for j in range(8)))
            print(f"    collector reads LSB bit {bit_in_byte} -> "
                  f"{bool((tb >> bit_in_byte) & 1)}")
    except Exception as e:
        print(f"    byte-mode FAILED: {e!r}")

    # [4] 判定：自动比较位模式真相与候选模型下的字节位
    print("\n[4] verdict:")
    truth = [bits_by_offset.get(base_bit + k) for k in range(8)]
    if tb is None or any(v is None for v in truth):
        print("    (missing data - see failures above)")
        print("    if byte-mode FAILED here, the collector block likely NACKs too")
        print("    -> check collector log for: GE SRTP read block %... failed")
    else:
        models = {
            "LSB, no shift (current code)": [(tb >> k) & 1 for k in range(8)],
            "MSB-first packing": [(tb >> (7 - k)) & 1 for k in range(8)],
        }
        for name, got in models.items():
            ok = all(int(truth[k]) == got[k] for k in range(8))
            print(f"    {name:36s}: {'MATCH' if ok else 'mismatch'} (8/8 bits)")
        # +-1 偏移模型：字节位 k == 位模式(base+k+shift)；
        # 在窗口可用位范围内比较（边缘位可能缺失）
        for shift, label in ((1, "LSB, shifted +1 (need -1 base)"),
                             (-1, "LSB, shifted -1 (need +1 base)")):
            t2 = [bits_by_offset.get(base_bit + k + shift) for k in range(8)]
            pairs = [(int(t2[k]), (tb >> k) & 1) for k in range(8) if t2[k] is not None]
            if len(pairs) >= 6:
                ok = all(a == b for a, b in pairs)
                print(f"    {label:36s}: {'MATCH' if ok else 'mismatch'} "
                      f"({len(pairs)}/8 bits)")
        print("\n    notes:")
        print("    - if MULTIPLE models MATCH, this byte is too uniform (0x00/0xFF...)")
        print("      -> rerun on a point whose byte has mixed 0/1 neighbors")
        print("    - if only 'current code' MATCHes: packing fine -> wrong monitor value")
        print("      means block NACK/stale -> check collector log for:")
        print("          GE SRTP read block %... failed")

    drv.disconnect()
    print("\ndone")


if __name__ == "__main__":
    main()
