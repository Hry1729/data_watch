"""SidecarManager —— OPC DA proxy 动态编排。

本文件当前仅实现 T013：proxy 可执行文件路径解析。spawn、端口闸门、认领、
差量启停等（T014~T020）后续按需扩展，不在此预先铺设。
"""

from __future__ import annotations

import json
from pathlib import Path

PROXY_EXE_NAME = "OpcDaProxyService.exe"

# proxy 运行参数默认值（与现有 proxy_*.json 样例一致）。
DEFAULT_UPDATE_RATE_MS = 500
DEFAULT_CONNECTION_TIMEOUT_MS = 10000
# OPC server 标识默认值；serverProgId/channel 全场单一，由 factory 从 config[opcda] 注入（T020）。
DEFAULT_SERVER_PROG_ID = "ZPMC.OPCServer"
DEFAULT_CHANNEL = "OPD_Server"
# endpoint 归一为本地的取值（与 opcda_detect._normalize_endpoint 对应），生成 config 时映射为空 vmHost。
_LOCAL_ENDPOINTS = {"", "localhost", "127.0.0.1"}

# 候选目录（相对安装根/仓库根，依次查找）。集中定义，避免路径硬编码散落。
_PROXY_BIN_CANDIDATES = (
    Path("sidecars/opcda_proxy_service/bin"),            # 打包安装目录
    Path("collector/sidecars/opcda_proxy_service/bin"),  # 开发仓库
)


class ProxyExecutableNotFound(Exception):
    """在所有候选路径下都找不到 OpcDaProxyService.exe。"""


def _default_install_root() -> Path:
    # agent/app/services/sidecar_manager.py → parents[3] = 安装根（打包）/仓库根（开发）。
    # 注意：PyInstaller frozen（onefile/onedir）下 __file__ 可能指向解包临时目录，
    # 此推导未必成立。T014/T020 真正 spawn/接入前须验证 frozen 下根定位，
    # 届时 factory 应显式注入 install_root（构造参数已预留）。
    return Path(__file__).resolve().parents[3]


class SidecarManager:
    def __init__(
        self,
        *,
        runtime_dir: Path | str | None = None,
        install_root: Path | str | None = None,
        runner=None,
        server_prog_id: str = DEFAULT_SERVER_PROG_ID,
        channel: str = DEFAULT_CHANNEL,
        update_rate_ms: int = DEFAULT_UPDATE_RATE_MS,
        connection_timeout_ms: int = DEFAULT_CONNECTION_TIMEOUT_MS,
    ):
        self.runtime_dir = Path(runtime_dir) if runtime_dir else None
        self.install_root = (
            Path(install_root).resolve() if install_root else _default_install_root()
        )
        self.runner = runner
        self._server_prog_id = server_prog_id
        self._channel = channel
        self._update_rate_ms = update_rate_ms
        self._connection_timeout_ms = connection_timeout_ms

    # ── proxy 配置生成（T014）────────────────────────────────────
    def _opcda_dir(self) -> Path:
        if self.runtime_dir is None:
            raise ValueError("SidecarManager 未配置 runtime_dir，无法生成 proxy 配置")
        d = self.runtime_dir / "opcda_proxy"
        d.mkdir(parents=True, exist_ok=True)
        return d

    def generate_proxy_config(self, port: int, vm_host: str) -> Path:
        """生成 proxy_{port}.json，返回路径。endpoint 为本地时 vmHost 写空串。"""
        try:
            port = int(port)
        except (TypeError, ValueError):
            raise ValueError(f"非法 proxy 端口: {port!r}")
        if not (1 <= port <= 65535):
            raise ValueError(f"proxy 端口超出范围(1-65535): {port}")

        raw_vm = str(vm_host or "").strip()
        vm = "" if raw_vm.lower() in _LOCAL_ENDPOINTS else raw_vm
        config = {
            "listenPort": port,
            "updateRateMs": self._update_rate_ms,
            "connectionTimeoutMs": self._connection_timeout_ms,
            "opcServers": [
                {
                    "vmHost": vm,
                    "serverProgId": self._server_prog_id,
                    "channel": self._channel,
                    "devices": [],
                }
            ],
        }
        path = self._opcda_dir() / f"proxy_{port}.json"
        path.write_text(json.dumps(config, indent=2, ensure_ascii=False), encoding="utf-8")
        return path

    def _proxy_candidates(self) -> list[Path]:
        return [self.install_root / rel / PROXY_EXE_NAME for rel in _PROXY_BIN_CANDIDATES]

    def resolve_proxy_executable(self) -> Path:
        """返回第一个存在的 proxy exe；都不存在时抛出含全部候选路径的错误。"""
        candidates = self._proxy_candidates()
        for candidate in candidates:
            if candidate.is_file():
                return candidate
        listed = "; ".join(str(c) for c in candidates)
        raise ProxyExecutableNotFound(
            f"找不到 {PROXY_EXE_NAME}，已查找候选路径: {listed}"
        )
