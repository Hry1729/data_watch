import os
import sys
import shutil
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..")))

from app.services import tdengine_log as tlog


class TestTdengineLogService(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        self._orig_get_settings = tlog.get_settings
        self._orig_max_bytes = tlog.MAX_TAIL_BYTES

    def tearDown(self):
        tlog.get_settings = self._orig_get_settings
        tlog.MAX_TAIL_BYTES = self._orig_max_bytes
        shutil.rmtree(self.dir, ignore_errors=True)

    def _point_settings_here(self):
        tlog.get_settings = lambda: SimpleNamespace(tdengine_log_dir=str(self.dir))

    def _write(self, name: str, content: str) -> None:
        (self.dir / name).write_text(content, encoding="utf-8")

    # ── 扫描 / 分类 ──────────────────────────────────────────────
    def test_scan_classifies_and_marks_gz(self):
        self._write("taosdlog.0", "a\nb\n")
        self._write("taosadapter.log", "x\n")
        self._write("taosdlog.20240101.gz", "binary")
        by_name = {f["name"]: f for f in tlog._scan(self.dir)}
        self.assertEqual(by_name["taosdlog.0"]["kind"], "taosd")
        self.assertTrue(by_name["taosdlog.0"]["tailSupported"])
        self.assertEqual(by_name["taosadapter.log"]["kind"], "taosadapter")
        self.assertTrue(by_name["taosdlog.20240101.gz"]["compressed"])
        self.assertFalse(by_name["taosdlog.20240101.gz"]["tailSupported"])

    # ── 路径安全 ────────────────────────────────────────────────
    def test_resolve_target_rejects_traversal_and_missing(self):
        self._write("taosdlog.0", "a\n")
        self.assertIsNotNone(tlog._resolve_target(self.dir, "taosdlog.0"))
        for bad in ("../taosdlog.0", "..", ".", "sub/taosdlog.0", "nope.log", ""):
            self.assertIsNone(tlog._resolve_target(self.dir, bad), bad)

    # ── tail ────────────────────────────────────────────────────
    def test_tail_returns_last_n_lines(self):
        self._write("taosdlog.0", "".join(f"line{i}\n" for i in range(20)))
        rows, truncated = tlog._tail(self.dir / "taosdlog.0", 5)
        self.assertEqual(rows, ["line15", "line16", "line17", "line18", "line19"])
        self.assertFalse(truncated)

    def test_tail_truncates_at_byte_cap(self):
        self._write("taosdlog.0", "".join(f"line{i}\n" for i in range(100)))
        tlog.MAX_TAIL_BYTES = 30  # 强制只读尾部少量字节
        rows, truncated = tlog._tail(self.dir / "taosdlog.0", 1000)
        self.assertTrue(truncated)
        self.assertTrue(rows)  # 仍有内容
        self.assertEqual(rows[-1], "line99")  # 末行准确

    # ── 公共接口降级 ────────────────────────────────────────────
    def test_disabled_when_dir_unset(self):
        tlog.get_settings = lambda: SimpleNamespace(tdengine_log_dir="")
        files = tlog._list_log_files_sync()
        self.assertFalse(files["enabled"])
        self.assertEqual(files["files"], [])
        tail = tlog._tail_log_file_sync("taosdlog.0", 100)
        self.assertFalse(tail["enabled"])

    def test_tail_gz_not_supported(self):
        self._write("taosdlog.x.gz", "data")
        self._point_settings_here()
        result = tlog._tail_log_file_sync("taosdlog.x.gz", 100)
        self.assertTrue(result["enabled"])
        self.assertEqual(result["lines"], [])
        self.assertIn("gz", result["reason"].lower())

    def test_tail_ok_through_public(self):
        self._write("taosdlog.0", "a\nb\nc\n")
        self._point_settings_here()
        result = tlog._tail_log_file_sync("taosdlog.0", 2)
        self.assertTrue(result["enabled"])
        self.assertEqual(result["lines"], ["b", "c"])
        self.assertEqual(result["maxBytes"], tlog.MAX_TAIL_BYTES)


if __name__ == "__main__":
    unittest.main()
