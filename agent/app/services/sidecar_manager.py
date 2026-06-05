"""SidecarManager —— OPC DA proxy 动态编排。

本文件当前仅实现 T013：proxy 可执行文件路径解析。spawn、端口闸门、认领、
差量启停等（T014~T020）后续按需扩展，不在此预先铺设。
"""

from __future__ import annotations

from pathlib import Path

PROXY_EXE_NAME = "OpcDaProxyService.exe"

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
    def __init__(self, install_root: Path | str | None = None):
        self.install_root = (
            Path(install_root).resolve() if install_root else _default_install_root()
        )

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
