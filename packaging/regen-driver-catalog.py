"""驱动签名后重建离线目录：用仓库自带 write_catalog 重建离线目录，但对 tree_sha256/版本未变的驱动沿用已提交的 zip 与 sha256。

原因：write_catalog 每次重写所有 zip，不同 zlib 产出的 DEFLATE 字节不同，未改动的
opcua/modbus_tcp zip 会无意义变化。正式发布请在发布机上直接运行 README 中的 write_catalog 命令。
用法（在仓库根目录）：python packaging/regen-driver-catalog.py
"""
import hashlib, json, shutil, sys, tempfile
from pathlib import Path
sys.path.insert(0, ".")
from data_watch_driver_catalog import write_catalog

dest = Path("packages/driver-catalog")
old = json.loads((dest / "catalog.json").read_text(encoding="utf-8"))
old_by_id = {e["id"]: e for e in old["drivers"]}
with tempfile.TemporaryDirectory() as tmp:
    out = Path(tmp) / "driver-catalog/catalog.json"
    write_catalog(Path("packages"), out, old["collector_version"])
    data = json.loads(out.read_text(encoding="utf-8"))
    for entry in data["drivers"]:
        prev = old_by_id.get(entry["id"])
        if prev and prev["version"] == entry["version"] and prev["tree_sha256"] == entry["tree_sha256"] \
                and (dest / prev["archive"]).is_file():
            entry["sha256"] = prev["sha256"]
            print("unchanged", entry["id"], entry["version"])
        else:
            shutil.copyfile(out.parent / entry["archive"], dest / entry["archive"])
            print("rebuilt  ", entry["id"], entry["version"], entry["archive"], entry["sha256"], entry["tree_sha256"])
    data.pop("build_id", None)
    data["build_id"] = hashlib.sha256(json.dumps(data, sort_keys=True).encode()).hexdigest()
    # 与已提交目录一致：Windows 发布机文本模式写出 CRLF（packages/** -text，字节即内容）
    (dest / "catalog.json").write_bytes((json.dumps(data, ensure_ascii=False, indent=2) + "\n").replace("\n", "\r\n").encode("utf-8"))
