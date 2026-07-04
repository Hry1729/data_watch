# -*- mode: python ; coding: utf-8 -*-
# One-file exe for GE SRTP RTT bench. READ ONLY - no writes.
# Build (cwd=collector/tests):  ..\.venv-pack\Scripts\pyinstaller.exe bench_ge_srtp.spec --noconfirm
# Output: dist\bench_ge_srtp.exe

datas = [('..\\drivers\\gesrtp-py-main', 'drivers\\gesrtp-py-main')]
hiddenimports = []

a = Analysis(
    ['bench_ge_srtp.py'],
    pathex=['.'],
    binaries=[],
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
    a.binaries,
    a.datas,
    [],
    name='bench_ge_srtp',
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=True,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
