# vendored taos-ws-py — provenance

`vendor/taos-ws-py` is the upstream Rust/PyO3 source of the TDengine WebSocket
connector the writer ships with, kept here so the connector can be patched and
rebuilt reproducibly instead of editing `writer/.venv-pack` by hand.

## Where it came from

| item | value |
| --- | --- |
| upstream repository | `https://github.com/taosdata/taos-connector-python` |
| directory in that repository | `taos-ws-py/` |
| commit | `ecfa0b2d8b0a5f2a45374dcb14f540d087f62598` (tag `v2.8.10`) |
| crate version in `Cargo.toml` | `taos-ws-py 0.7.0` |
| PyPI package | `taos-ws-py==0.7.0`, wheel tag `cp37-abi3-win_amd64` |

The wheel installed in `writer/.venv-pack` was built by maturin 1.14.1 with
`pyo3 0.17.3` and the `abi3-py37` feature — the same toolchain contract as the
vendor source, which is what makes the `cp37-abi3` tag match.

## Why we believe this is the source of the shipped wheel

`taos-ws-py-0.7.0.dist-info/sboms/taos-ws-py.cyclonedx.json` (shipped inside the
wheel) records the git revisions of every git dependency. The vendored
`Cargo.lock`, which upstream commits, pins exactly the same revisions:

| dependency | SBOM revision | vendored `Cargo.lock` |
| --- | --- | --- |
| `taosdata/taos-connector-rust` | `f49c37185987a3f5a893a02d04fe5db662a79c37` | same |
| `taosdata/ring` (`fix/loongarch`) | `2d0cfa2c570a86622f4237939b3893d6719682f1` | same |

Because the lock file is committed upstream, the build resolves the whole
dependency graph — including the registry crates and the vendored crypto and
WebSocket forks — to the revisions the shipped wheel was built from.

## The patch

`vendor/patches/0001-release-gil-around-blocking-calls.patch`

`Connection::execute`, `Connection::query`, `*_with_req_id`, `Connection::close`
and the module-level `connect` all run synchronous WebSocket round trips without
releasing the interpreter lock. The writer's four consumers share one asyncio
event loop, so every native call froze the entire process while it waited.

Measured before the patch with `scripts/validate_taosws_gil_release.py`
(ordinary CPython 3.14.6, GIL enabled, `taos-ws-py 0.7.0`):

| scenario | heartbeat coverage | interpreter unavailable |
| --- | --- | --- |
| control (sleep instead of a native call) | 0.983 | 115 ms / 6733 ms |
| connect against a stalled server | 0.022 | **14 301 ms** / 14 623 ms |
| `write_multi_table` against a live TDengine | 0.407 | 1 108 ms / 1 867 ms (longest single stall 638 ms) |

The patch wraps those calls in `Python::allow_threads`, which releases the GIL
for the duration of the blocking work. Only Rust-owned values are used inside
the released section — no Python object is touched.

`schemaless_insert`, `Cursor`, `TaosStmt`, `TaosStmt2` and the TMQ consumer are
deliberately **not** patched: the writer never calls them, and widening the diff
would enlarge the surface that has to be re-verified on every upstream bump.

## Build

```powershell
pwsh -File installer/build-patched-taos-ws-py.ps1
```

The script copies the pristine source to a scratch directory, applies the patch
there, and builds with the pinned `Cargo.lock`. It needs CMake (installed into
its own build virtual environment with `pip install cmake`) and the aws-lc
prebuilt NASM objects; the host does not need NASM installed. It never writes
into `writer/.venv-pack` — `installer/build-roles.ps1` installs the resulting
wheel.
