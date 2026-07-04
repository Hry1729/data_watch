# -*- mode: python ; coding: utf-8 -*-
# One-file exe for GE SRTP discrete bit-vs-byte diagnosis. READ ONLY - no writes.
# Build (cwd=collector/tests):  ..\.venv-pack\Scripts\pyinstaller.exe diag_ge_discrete.spec --noconfirm
# Output: dist\diag_ge_discrete.exe

datas = [('..\\drivers\\gesrtp-py-main', 'drivers\\gesrtp-py-main')]
hiddenimports = []

a = Analysis(
    ['diag_ge_discrete.py'],
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
    name='diag_ge_discrete',
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
