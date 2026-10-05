# -*- mode: python ; coding: utf-8 -*-
from pathlib import Path

from PyInstaller.utils.hooks import collect_all, collect_submodules

repo_root = Path(SPECPATH).resolve().parent
collector_root = repo_root / 'collector'

datas = [(str(repo_root / 'packages/modbus_tcp/modbus_tcp/address.py'), 'modbus-rules')]
binaries = []
hiddenimports = ['driver_preflight', 'data_watch_driver_manifest', 'data_watch_driver_catalog']
hiddenimports += ['shelve', 'ctypes.wintypes', 'netrc', 'getpass']
hiddenimports += collect_submodules('xml') + collect_submodules('sqlite3') + collect_submodules('dbm')
for package in ('pymysql', 'aiomysql', 'httpx', 'msgspec'):
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


# Offline archives are installed once into the data-root activation directory.
import sys
import runpy
sys.path.insert(0, str(repo_root))
from data_watch_driver_catalog import write_catalog
version = runpy.run_path(str(Path(repo_root) / 'collector/core/version.py'))['__version__']
artifact_root = Path(coll.name)
write_catalog(Path(repo_root) / 'packages', artifact_root / 'driver-catalog/catalog.json', version, executable='data-watch-edge-collector.exe')
