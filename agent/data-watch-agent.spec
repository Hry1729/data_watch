# -*- mode: python ; coding: utf-8 -*-
from pathlib import Path

from PyInstaller.utils.hooks import collect_all, collect_submodules

# config 运行期走 DATAWATCH_DATA_ROOT\config.toml（_default_config 程序生成），
# 不读 config.toml.example；模板由安装器从源码复制，无需进包。
datas = []
binaries = []
# win32crypt/pywintypes: DPAPI 加解密(app.core.security)；routers 在 create_app 内动态 import；
# uvicorn[standard] 协议实现运行期动态选择，WS 日志推送(viewer)未冒烟，显式兜底。
# win32timezone: pywin32 的纯 Python 模块（site-packages\win32\lib），**不被静态分析发现**。
# win32file.GetFileInformationByHandle 返回 FILETIME，pywintypes 转 datetime 时才 import 它；
# 漏掉会让 config._open_config_temp 在冻结包里抛 ModuleNotFoundError——而它位于
# edge_sync._record_sync 的保存路径上，一次同步失败即杀死同步协程（节点永久停同步）。
hiddenimports = [
    'win32crypt',
    'pywintypes',
    'win32api',
    'win32file',
    'win32security',
    'win32timezone',
    'app.routers.bootstrap',
    'app.routers.auth',
    'app.routers.viewer',
    'app.routers.admin',
    'app.routers.local',
    'uvicorn.protocols.websockets.websockets_impl',
    'uvicorn.protocols.websockets.wsproto_impl',
    'uvicorn.protocols.http.httptools_impl',
    'uvicorn.protocols.http.h11_impl',
]

# uvicorn 的协议/事件循环/lifespan 实现走运行期动态导入，整包收集
tmp_ret = collect_all('uvicorn')
datas += tmp_ret[0]; binaries += tmp_ret[1]; hiddenimports += tmp_ret[2]
# python-jose 后端(JWT HS256)运行期动态选择，登录路径未冒烟，整子模块收集兜底
hiddenimports += collect_submodules('jose')

a = Analysis(
    ['main.py'],
    pathex=['.', str(Path(SPECPATH).resolve().parent)],
    binaries=binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name='data-watch-agent',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,  # 服务端 onedir 无需压缩；避免 UPX 压坏 pywin32/cryptography/httptools 等原生扩展或触发杀软误报
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=False,
    upx_exclude=[],
    name='data-watch-agent',
)
