"""测试用假 OPC DA proxy 桩。

用途：替代真实 OpcDaProxyService.exe，在 127.0.0.1:<port> 上 LISTEN，
供 SidecarManager 端口闸门 / 认领 / 差量等集成测试使用。

端口来源（二选一，兼容 SidecarManager 以 --config 启动的方式）：
  --port <int>                直接指定
  --config <proxy_xxxx.json>  读取 listenPort

可选行为：
  --delay-ready <秒>   延迟该秒数后才开始 LISTEN（测端口闸门轮询）
  --never-listen       永不 LISTEN，仅空转（测端口闸门超时）
"""
from __future__ import annotations

import argparse
import json
import socket
import sys
import time


def _resolve_port(args: argparse.Namespace) -> int:
    if args.port is not None:
        return int(args.port)
    if args.config:
        with open(args.config, "r", encoding="utf-8") as f:
            return int(json.load(f)["listenPort"])
    print("fake_proxy: 需要 --port 或 --config", file=sys.stderr)
    raise SystemExit(2)


def main() -> None:
    p = argparse.ArgumentParser()
    p.add_argument("--port", type=int, default=None)
    p.add_argument("--config", type=str, default=None)
    p.add_argument("--delay-ready", type=float, default=0.0)
    p.add_argument("--never-listen", action="store_true")
    args, _ = p.parse_known_args()

    if args.never_listen:
        while True:
            time.sleep(3600)

    if args.delay_ready > 0:
        time.sleep(args.delay_ready)

    port = _resolve_port(args)
    srv = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    # 不设 SO_REUSEADDR：模拟真实监听者，便于测试"端口已被占用"语义
    srv.bind(("127.0.0.1", port))
    srv.listen(8)
    print(f"fake_proxy: Listening on 127.0.0.1:{port}", flush=True)

    while True:
        try:
            conn, _ = srv.accept()
            conn.settimeout(1.0)
        except OSError:
            break


if __name__ == "__main__":
    main()
