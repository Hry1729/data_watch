"""C10：启动时清掉残留 shutdown_flag，避免残留 flag 让 collector 启动即自杀。"""

from main import _clear_stale_shutdown_flag


def test_clear_stale_shutdown_flag_removes_existing(tmp_path):
    """残留 flag 在启动清理后被删除。"""
    flag = tmp_path / "collector.shutdown_flag"
    flag.write_text("", encoding="utf-8")

    _clear_stale_shutdown_flag(flag)

    assert not flag.exists()


def test_clear_stale_shutdown_flag_noop_when_missing(tmp_path):
    """flag 不存在时不报错（missing_ok）。"""
    flag = tmp_path / "collector.shutdown_flag"

    _clear_stale_shutdown_flag(flag)  # 不抛

    assert not flag.exists()
