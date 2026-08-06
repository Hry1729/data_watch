# -*- mode: python ; coding: utf-8 -*-
from pathlib import Path

from PyInstaller.utils.hooks import collect_all

repo_root = Path(SPECPATH).resolve().parent
collector_root = repo_root / 'collector'

datas = [(str(collector_root / 'drivers' / 'gesrtp-py-main'), 'drivers\\gesrtp-py-main')]
binaries = []
hiddenimports = [
    'drivers.s7_driver',
    'drivers.ge_srtp_driver',
    'drivers.opcda_center_driver',
    'drivers.opcua_driver',
]
for package in ('snap7', 'cryptography', 'pymysql', 'aiomysql', 'msgspec'):
    package_data, package_binaries, package_hidden = collect_all(package)
    datas += package_data
    binaries += package_binaries
    hiddenimports += package_hidden

a = Analysis(
    ['collector_main.py'],
    pathex=[str(repo_root), str(collector_root)],
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
    name='data-watch-edge-collector',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=True,
)
coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=False,
    name='data-watch-edge-collector',
)

