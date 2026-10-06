"""ManagedProcessRunner 单元测试（002 T004）。

先写、先 FAIL（模块尚未抽取）。覆盖共享监管内核：launch（spawn+PID+日志）、
verify_alive（pid+create_time）、force_stop、读取缺失/陈旧记录。
"""
import os
import sys
import time

import psutil

from app.services.managed_process_runner import ManagedProcessRunner

DUMMY_CHILD = os.path.join(os.path.dirname(__file__), "fixtures", "dummy_child.py")


def _spawn(r, name="proxy_9090"):
    return r.launch(
        name,
        [sys.executable, DUMMY_CHILD, "--ignore-flag"],
        cwd=os.path.dirname(DUMMY_CHILD),
    )


def test_launch_writes_pid_and_log(runtime_dir):
    r = ManagedProcessRunner(runtime_dir)
    pid, create_time = _spawn(r)
    try:
        assert pid > 0
        assert r.pid_file("proxy_9090").exists()
        assert r.log_file("proxy_9090").exists()
        rec = r.read_pid_record("proxy_9090")
        assert rec["pid"] == pid
        assert abs(float(rec["create_time"]) - create_time) < 1.0
        proc = r.verify_alive(rec)
        assert proc is not None and proc.pid == pid
    finally:
        proc = psutil.Process(pid)
        r.force_stop(proc)
        r.close_log("proxy_9090")


def test_force_stop_kills_process(runtime_dir):
    r = ManagedProcessRunner(runtime_dir)
    pid, _ = _spawn(r, "p")
    proc = psutil.Process(pid)
    r.force_stop(proc)
    end = time.time() + 5
    while time.time() < end and proc.is_running():
        time.sleep(0.05)
    assert not proc.is_running()
    r.close_log("p")


def test_force_stop_kills_descendant_processes(runtime_dir):
    r = ManagedProcessRunner(runtime_dir)
    pid, _ = r.launch(
        "tree",
        [sys.executable, DUMMY_CHILD, "--ignore-flag", "--spawn-child"],
        cwd=os.path.dirname(DUMMY_CHILD),
    )
    proc = psutil.Process(pid)
    end = time.time() + 5
    children = []
    while time.time() < end and not children:
        children = proc.children(recursive=True)
        time.sleep(0.05)

    try:
        assert children
        r.force_stop(proc)
        assert not proc.is_running()
        assert all(not child.is_running() for child in children)
    finally:
        for target in [proc, *children]:
            try:
                target.kill()
            except psutil.Error:
                pass
        r.close_log("tree")


def test_verify_alive_rejects_stale_create_time(runtime_dir):
    r = ManagedProcessRunner(runtime_dir)
    # 用本进程 pid 但伪造的 create_time，应判为非同一进程
    assert r.verify_alive({"pid": os.getpid(), "create_time": 1.0}) is None


def test_verify_alive_rejects_subsecond_pid_reuse(runtime_dir):
    r = ManagedProcessRunner(runtime_dir)
    created = psutil.Process(os.getpid()).create_time()
    assert r.verify_alive({"pid": os.getpid(), "create_time": created - 0.5}) is None
    assert r.verify_alive({"pid": os.getpid(), "create_time": created}) is not None


def test_read_pid_record_missing_returns_none(runtime_dir):
    r = ManagedProcessRunner(runtime_dir)
    assert r.read_pid_record("nope") is None


def test_atomic_pid_failure_preserves_previous_record(runtime_dir, monkeypatch):
    import pytest
    r = ManagedProcessRunner(runtime_dir)
    r.write_pid_record("atomic", 123, 10.0)
    before = r.pid_file("atomic").read_bytes()
    def fail_replace(*args):
        raise OSError("interrupted replace")
    monkeypatch.setattr("app.services.managed_process_runner.os.replace", fail_replace)
    with pytest.raises(OSError, match="interrupted"):
        r.write_pid_record("atomic", 456, 20.0)
    assert r.pid_file("atomic").read_bytes() == before
    assert list(r.pids_dir.glob("*.tmp")) == []


def test_restart_preserves_log_even_while_tail_reader_holds_file(runtime_dir):
    r = ManagedProcessRunner(runtime_dir)
    log = r.log_file("proxy_9090")
    log.parent.mkdir(parents=True, exist_ok=True)
    log.write_text("crash diagnostic", encoding="utf-8")
    pid = None
    try:
        with log.open("r", encoding="utf-8"):
            pid, _ = _spawn(r)
            assert log.with_name("previous.log").read_text(encoding="utf-8") == "crash diagnostic"
    finally:
        if pid is not None:
            r.force_stop(psutil.Process(pid))
        r.close_log("proxy_9090")
