"""One-shot import/ABI/dependency verification in the target Collector runtime.
Never starts CollectorApp, creates protocol connections, or writes active packages.
"""
from __future__ import annotations
import argparse
import json
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
if str(REPO_ROOT) not in sys.path:
    sys.path.insert(0, str(REPO_ROOT))


def preflight(package: Path, active: Path, approved_digest: str | None = None) -> dict:
    from drivers.manifest import load_driver_toml
    from drivers.registry import DriverRegistry
    from data_watch_driver_catalog import tree_digest
    manifest = load_driver_toml(package / "driver.toml")
    others = [path for path in active.iterdir() if path.is_dir() and not path.name.startswith(".") and path.name != manifest.id] if active.is_dir() else []
    approvals = {}
    if approved_digest is not None:
        if tree_digest(package) != approved_digest:
            raise ValueError("Agent 已验证的包摘要不匹配")
        approvals[manifest.id] = {"publisher": manifest.publisher, "tree_sha256": approved_digest, "approved_source": True}
    registry = DriverRegistry.scan(active, package_roots=[*others, package], approved_records=approvals)
    failed = [item for item in registry.list_status() if item["status"] != "loaded"]
    if failed:
        raise ValueError("; ".join(f"{item['id']}: {item.get('error')}" for item in failed))
    registration = registry.get(manifest.protocol)
    if registration is None or registration.manifest.id != manifest.id:
        raise ValueError("目标包未成功加载")
    # Factory construction exercises native resource initialization, but never connect().
    registry.create(manifest.protocol)
    import sysconfig
    if sysconfig.get_config_var("Py_GIL_DISABLED") and sys._is_gil_enabled():
        raise ValueError("依赖不支持 free-threaded 运行时，导入后重新启用了 GIL")
    return {"id": manifest.id, "version": manifest.version, "tree_sha256": tree_digest(package), "status": "verified"}


def main(argv=None):
    parser = argparse.ArgumentParser()
    parser.add_argument("--driver-preflight", required=True)
    parser.add_argument("--active-drivers", required=True)
    parser.add_argument("--approved-digest", help="Agent-only one-shot approval after signature verification; never used by normal startup")
    args = parser.parse_args(argv)
    try:
        result = preflight(Path(args.driver_preflight), Path(args.active_drivers), args.approved_digest)
    except Exception as exc:
        print(json.dumps({"status": "failed", "error": f"{type(exc).__name__}: {exc}"}, ensure_ascii=False))
        return 1
    print(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
