"""针对性测试：诊断脚本的连接配置统一、隔离闸门、子进程环境与退出码契约。

不连任何数据库：配置、模式入口和故障清理使用纯数据或连接替身。
原生数据库依赖只在实际使用点导入；计划模式另以屏蔽依赖的子进程验证。
"""
from __future__ import annotations

import json
from pathlib import Path
import sys
import tomllib
import types

import pytest

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))

# 本文件的被测函数不需要真实 taosws；注入替身避免 ABI 冲突（真实运行见脚本 docstring 的容器约定）
sys.modules.setdefault("taosws", types.ModuleType("taosws"))

import diagnose_writer_tdengine_capability as cap  # noqa: E402

SCRIPT = ROOT / "scripts" / "diagnose_writer_tdengine_capability.py"
JOINT_SCRIPT = ROOT / "scripts" / "diagnose_joint_pipeline.py"


def _cfg_file(tmp_path: Path, **overrides) -> Path:
    cfg = {
        "mysql": {"host": "127.0.0.1", "port": 13306, "user": "root", "password": "pw-mysql"},
        "redis": {"host": "127.0.0.1", "port": 16379, "db": 7, "password": "pw-redis"},
        "tdengine": {"host": "127.0.0.1", "port": 6041, "user": "root", "password": "pw-td"},
        "allowed_hosts": ["127.0.0.1", "localhost"],
        "allowed_db_prefixes": ["dwcap_", "dwjnt_"],
    }
    cfg.update(overrides)
    path = tmp_path / "test-config.json"
    path.write_text(json.dumps(cfg), encoding="utf-8")
    return path


# ── 退出码契约（要求 7：失败必须非零，且此前恒 0）─────────────────────────────


def test_exit_code_is_zero_only_for_pass():
    assert cap.exit_code_for("pass") == 0
    assert cap.exit_code_for("fail") == 1
    assert cap.exit_code_for("not_tested") == 1


def test_scripts_do_not_hardcode_success_exit():
    """两个脚本的验收路径都不得再写死 return 0。"""
    assert "return exit_code_for(" in SCRIPT.read_text(encoding="utf-8")
    joint = JOINT_SCRIPT.read_text(encoding="utf-8")
    assert "_exit_code_for(" in joint


# ── 配置装载与隔离闸门 ───────────────────────────────────────────────────────


def test_missing_config_raises_actionable_error(tmp_path):
    with pytest.raises(SystemExit) as exc:
        cap.load_test_config(tmp_path / "nope.json")
    assert "测试连接配置不存在" in str(exc.value)


def test_config_requires_all_sections(tmp_path):
    path = tmp_path / "bad.json"
    complete = {"host": "127.0.0.1", "port": 1}
    path.write_text(json.dumps({"mysql": complete, "tdengine": complete}), encoding="utf-8")
    with pytest.raises(SystemExit) as exc:
        cap.load_test_config(path)
    assert "缺少 [redis] 段" in str(exc.value)


def test_config_requires_host_and_port(tmp_path):
    path = tmp_path / "bad2.json"
    path.write_text(json.dumps({"mysql": {"host": "127.0.0.1"},
                                "redis": {"host": "127.0.0.1", "port": 1},
                                "tdengine": {"host": "127.0.0.1", "port": 1}}),
                    encoding="utf-8")
    with pytest.raises(SystemExit) as exc:
        cap.load_test_config(path)
    assert "[mysql].port 未设置" in str(exc.value)


def test_host_outside_allowlist_is_rejected(tmp_path):
    cfg = cap.load_test_config(_cfg_file(tmp_path, tdengine={"host": "10.1.2.3", "port": 6041}))
    with pytest.raises(SystemExit) as exc:
        cap.assert_test_targets(cfg, prefix="dwcap_", database="dwcap_abc123")
    assert "不在显式白名单" in str(exc.value)


def test_explicit_allowlist_admits_extra_host(tmp_path):
    cfg = cap.load_test_config(_cfg_file(
        tmp_path, tdengine={"host": "10.1.2.3", "port": 6041},
        allowed_hosts=["127.0.0.1", "10.1.2.3"]))
    cap.assert_test_targets(cfg, prefix="dwcap_", database="dwcap_abc123")


def test_database_prefix_must_match_calling_script(tmp_path):
    cfg = cap.load_test_config(_cfg_file(tmp_path))
    with pytest.raises(SystemExit) as exc:
        cap.assert_test_targets(cfg, prefix="dwcap_", database="dwjnt_abc123")
    assert "必须以本轮前缀" in str(exc.value)
    # 联合脚本用自己的前缀，同一份配置必须被接受（不是只允许一种前缀）
    cap.assert_test_targets(cfg, prefix="dwjnt_", database="dwjnt_abc123")


def test_only_the_calling_scripts_prefix_is_accepted(tmp_path):
    """两个脚本各自的临时库前缀都要能用，其它前缀（如生产库名）必须拒绝。"""
    cfg = cap.load_test_config(_cfg_file(tmp_path))
    cap.assert_test_targets(cfg, prefix="dwcap_", database="dwcap_abc123")
    cap.assert_test_targets(cfg, prefix="dwjnt_", database="dwjnt_abc123")
    with pytest.raises(SystemExit) as exc:
        cap.assert_test_targets(cfg, prefix="dwcap_", database="device_watch")
    assert "必须以本轮前缀" in str(exc.value)


# ── 三处连接必须一致 ─────────────────────────────────────────────────────────


def test_write_runtime_uses_same_connection_config(tmp_path):
    cfg = cap.load_test_config(_cfg_file(tmp_path))
    path = cap.write_runtime(tmp_path / "rt", cfg, database="dwcap_abc123",
                             stable="sdcap", device_type="capabc", shard_count=2,
                             group="gcap", consumer_workers=1, batch_count=1)
    settings = tomllib.loads(path.read_text(encoding="utf-8"))
    assert settings["mysql"] == {"host": cfg["mysql"]["host"], "port": cfg["mysql"]["port"],
                                 "user": cfg["mysql"]["user"],
                                 "password": cfg["mysql"]["password"],
                                 "database": "dwcap_abc123"}
    assert settings["redis"]["host"] == cfg["redis"]["host"]
    assert settings["redis"]["port"] == cfg["redis"]["port"]
    assert settings["redis"]["db"] == cfg["redis"]["db"]  # redis 无 env 覆盖，只能靠配置
    assert settings["redis"]["password"] == cfg["redis"]["password"]
    assert settings["tdengine"]["host"] == cfg["tdengine"]["host"]
    assert settings["tdengine"]["password"] == cfg["tdengine"]["password"]
    assert settings["tdengine"]["database"] == "dwcap_abc123"


def test_writer_child_env_strips_inherited_and_injects(monkeypatch, tmp_path):
    cfg = cap.load_test_config(_cfg_file(tmp_path))
    monkeypatch.setenv("DATAWATCH_TDENGINE_HOST", "attacker-host")
    monkeypatch.setenv("DATAWATCH_MYSQL_DATABASE", "production_db")
    monkeypatch.setenv("SOME_OTHER_VAR", "keep-me")
    env = cap.writer_child_env(cfg, database="dwcap_abc123")
    assert env["DATAWATCH_TDENGINE_HOST"] == cfg["tdengine"]["host"]
    assert env["DATAWATCH_MYSQL_DATABASE"] == "dwcap_abc123"
    assert env["DATAWATCH_REDIS_PASSWORD"] == cfg["redis"]["password"]
    assert "DATAWATCH_REDIS_DB" not in env  # writer 不支持该覆盖，库号在 settings.toml
    assert env["SOME_OTHER_VAR"] == "keep-me"


def test_both_scripts_share_one_config_source():
    """两个脚本都走 --test-config 的同一份配置，且不再有硬编码 perf-* 与默认口令字面量。"""
    for path in (SCRIPT, JOINT_SCRIPT):
        text = path.read_text(encoding="utf-8")
        assert "--test-config" in text
        assert "load_test_config" in text or "_exit_code_for" in text
        # 默认口令字面量用拼接构造，避免它本身出现在仓库里
        for forbidden in ("perf-redis", "perf-mysql", "perf-td", "taos" + "data"):
            assert forbidden not in text, f"{path.name} 仍有 {forbidden}"


# ── 资源清理（只清本轮对象，绝不清空整个 Redis）────────────────────────────


class _FakeCursor:
    def __init__(self, sink):
        self._sink = sink

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        return False

    def execute(self, sql, params=None):
        self._sink.append(sql)


class _FakeConn:
    def __init__(self, sink):
        self._sink = sink

    def cursor(self):
        return _FakeCursor(self._sink)

    def execute(self, sql):
        self._sink.append(sql)

    def close(self):
        pass


class _FakeRedis:
    def __init__(self):
        self.deleted = []

    def delete(self, key):
        self.deleted.append(key)
        return 1


def test_round_resource_keys_covers_stream_latest_and_window():
    keys = cap.round_resource_keys(stream_keys=["rt:stream:dt:g:0"], devices=["d1", "d2"],
                                   group="g", period_ms=100)
    assert keys == sorted(["rt:stream:dt:g:0", "rt:latest:d1:g", "rt:window:d1:g:100",
                           "rt:latest:d2:g", "rt:window:d2:g:100"])


def test_cleanup_touches_only_round_objects(monkeypatch):
    sqls: list[str] = []
    import pymysql
    monkeypatch.setattr(sys.modules["taosws"], "connect", lambda **kw: _FakeConn(sqls), raising=False)
    monkeypatch.setattr(pymysql, "connect", lambda **kw: _FakeConn(sqls), raising=False)
    client = _FakeRedis()
    cfg = {"mysql": {"host": "127.0.0.1", "port": 13306, "user": "root", "password": "x"},
           "tdengine": {"host": "127.0.0.1", "port": 6041, "user": "root", "password": "x"}}
    dropped = cap.cleanup_round_resources(
        cfg, database="dwcap_abc", client=client,
        keys=["rt:stream:cap:gcap:1", "rt:latest:cw1:gcap"], owned_databases={"mysql", "tdengine"})
    assert dropped == {"tdengine_database": "dwcap_abc", "mysql_database": "dwcap_abc",
                       "redis_keys": 2}
    assert client.deleted == ["rt:stream:cap:gcap:1", "rt:latest:cw1:gcap"]
    joined = " ".join(sqls).upper()
    assert "DROP DATABASE IF EXISTS DWCAP_ABC" in joined
    assert "FLUSHALL" not in joined and "FLUSHDB" not in joined


@pytest.mark.parametrize("password", ['quote"here', r'back\slash', 'line\nbreak\t密码😀'])
def test_runtime_toml_strings_round_trip(tmp_path, password):
    cfg = cap.load_test_config(_cfg_file(tmp_path))
    for section in ("mysql", "redis", "tdengine"):
        cfg[section]["password"] = password
        cfg[section]["user"] = 'user"\\name'
    path = cap.write_runtime(tmp_path / "runtime", cfg, database="dwcap_strings",
                             stable="sdtest", device_type="test", shard_count=1,
                             group="gtest", consumer_workers=1, batch_count=1)
    parsed = tomllib.loads(path.read_text(encoding="utf-8"))
    for section in ("mysql", "redis", "tdengine"):
        assert parsed[section]["password"] == password
    assert parsed["mysql"]["user"] == cfg["mysql"]["user"]
    assert parsed["tdengine"]["user"] == cfg["tdengine"]["user"]


def test_plan_mode_runs_without_native_database_dependencies(tmp_path):
    import os
    import subprocess
    config = _cfg_file(tmp_path)
    plan = tmp_path / "plan.json"
    code = '''
import importlib.abc, os, sys
class BlockNative(importlib.abc.MetaPathFinder):
    def find_spec(self, fullname, path=None, target=None):
        if fullname.split('.')[0] in {'taosws', 'psutil', 'pymysql', 'redis', 'msgspec'}:
            raise ModuleNotFoundError(fullname)
sys.meta_path.insert(0, BlockNative())
import diagnose_joint_pipeline as joint
sys.argv = ['joint', '--mode', 'plan', '--plan', os.environ['DW_PLAN'],
            '--test-config', os.environ['DW_CONFIG'], '--devices', '1', '--points', '2']
assert joint.main() == 0
assert joint._exit_code_for('fail') == 1
'''
    result = subprocess.run([sys.executable, "-c", code], capture_output=True, text=True,
                            env={**os.environ, "PYTHONPATH": str(ROOT / "scripts"),
                                 "DW_PLAN": str(plan), "DW_CONFIG": str(config)})
    assert result.returncode == 0, result.stderr
    assert json.loads(plan.read_text())["redis_db"] == 7


def test_collector_mode_loads_config_before_connection(tmp_path, monkeypatch):
    import diagnose_joint_pipeline as joint
    import redis
    from types import SimpleNamespace
    config = _cfg_file(tmp_path)
    plan_path = tmp_path / "plan.json"
    joint.mode_plan(SimpleNamespace(test_config=str(config), plan=str(plan_path), devices=1,
                                   points=2, period_ms=100, seconds=1, shard_count=1))
    seen = []
    def connect(**kwargs):
        seen.append(kwargs)
        raise RuntimeError("isolated connection reached")
    monkeypatch.setattr(redis, "Redis", connect)
    with pytest.raises(RuntimeError, match="isolated connection reached"):
        joint.mode_collector(SimpleNamespace(plan=str(plan_path), test_config=str(config)))
    assert seen[0]["port"] == 16379
    assert seen[0]["db"] == 7


def test_central_prep_mounts_and_passes_its_config(tmp_path):
    import ast
    source = (ROOT / "scripts/validate_central_pipeline.py").read_text(encoding="utf-8")
    call = next(node.value for node in ast.walk(ast.parse(source))
                if isinstance(node, ast.Assign) and any(
                    isinstance(target, ast.Name) and target.id == "prepare" for target in node.targets))
    seen = []
    eval(compile(ast.Expression(call), "central-prep", "eval"), {
        "docker": lambda *args: seen.extend(args), "base": [], "rel": "/evidence/round",
        "test_config_path": tmp_path / "config.json", "RUNNER_IMAGE": "isolated-runner",
    })
    assert seen[seen.index("--test-config") + 1] == "/diagnose-test-config.json"
    assert f"{tmp_path / 'config.json'}:/diagnose-test-config.json:ro" in seen


def test_round_resources_include_only_owned_receipt_and_dlq():
    keys = cap.round_resource_keys(stream_keys=["rt:stream:own:g:0"], devices=["own1"],
                                   device_type="own", group="g", period_ms=100,
                                   receipt_keys=["collector:receipt:v2:own-session"])
    assert "collector:receipt:v2:own-session" in keys
    assert "rt:dlq:own:g" in keys
    assert "rt:dlq:foreign:g" not in keys
    assert "collector:receipt:v2:foreign-session" not in keys


def test_cleanup_failure_does_not_block_other_owned_resources(tmp_path, monkeypatch):
    import pymysql
    sqls = []
    def unavailable(**kwargs):
        raise ConnectionError("synthetic TD outage")
    monkeypatch.setattr(sys.modules["taosws"], "connect", unavailable, raising=False)
    monkeypatch.setattr(pymysql, "connect", lambda **kwargs: _FakeConn(sqls))
    client = _FakeRedis()
    cfg = cap.load_test_config(_cfg_file(tmp_path))
    result = cap.cleanup_round_resources(cfg, database="dwcap_abc", client=client,
                                         keys=["owned-key"], owned_databases={"mysql", "tdengine"})
    assert result["tdengine_database"] is None
    assert result["mysql_database"] == "dwcap_abc"
    assert client.deleted == ["owned-key"]
    assert "synthetic TD outage" in result["errors"][0]


def test_preparation_failure_preserves_report_and_cleans_only_created_database(tmp_path, monkeypatch):
    import pymysql
    cfg_path = _cfg_file(tmp_path)
    output = tmp_path / "failure.json"
    sqls = []
    monkeypatch.setitem(sys.modules, "psutil", types.ModuleType("psutil"))
    monkeypatch.setattr(cap, "assert_tdengine_database_absent", lambda *args: None)
    monkeypatch.setattr(pymysql, "connect", lambda **kwargs: _FakeConn(sqls))
    def prepare(*args, **kwargs):
        kwargs["owned_resources"].add("mysql")
        raise RuntimeError("synthetic failure after MySQL create")
    monkeypatch.setattr(cap, "prepare_mysql", prepare)
    monkeypatch.setattr(sys, "argv", ["cap", "--devices", "1", "--points", "2", "--ticks", "2",
                                      "--test-config", str(cfg_path), "--output", str(output)])
    assert cap.main() == 1
    report = json.loads(output.read_text(encoding="utf-8"))
    assert report["overall_status"] == "fail"
    assert "synthetic failure" in report["error"]
    assert report["cleanup"]["mysql_database"] == report["mysql_database"]
    assert report["cleanup"]["tdengine_database"] is None
    assert len(sqls) == 1


def test_writer_cleanup_reaps_real_child(tmp_path):
    import os
    import subprocess
    flag = tmp_path / "writer.shutdown_flag"
    child = subprocess.Popen([sys.executable, "-c",
        "import os,time; from pathlib import Path; p=Path(os.environ['DW_STOP_PATH']); "
        "\nwhile not p.exists(): time.sleep(.02)"],
        env={**os.environ, "DW_STOP_PATH": str(flag)})
    try:
        cap.stop_writer_process(child, tmp_path)
        assert child.poll() == 0
    finally:
        if child.poll() is None:
            child.kill()
        child.wait(timeout=5)


def test_reconcile_preserves_verdict_when_cleanup_fails(tmp_path, monkeypatch):
    import diagnose_joint_pipeline as joint
    import pymysql
    import redis
    from types import SimpleNamespace
    config_path = _cfg_file(tmp_path)
    plan_path = tmp_path / "plan.json"
    joint.mode_plan(SimpleNamespace(test_config=str(config_path), plan=str(plan_path), devices=1,
                                   points=2, period_ms=100, seconds=1, shard_count=1))
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    plan["owned_databases"] = ["mysql", "tdengine"]
    plan_path.write_text(json.dumps(plan), encoding="utf-8")
    collector_path = tmp_path / "collector.json"
    collector_path.write_text(json.dumps({
        "submitted_frames": 1, "confirmed_frames": 1, "redis_confirmed_after_drain": 1,
        "stream_keys": [f"rt:stream:{plan['device_type']}:{plan['group']}:0"],
        "receipt_keys": ["collector:receipt:v2:own-session"], "window_seconds": 1,
    }), encoding="utf-8")
    class Client(_FakeRedis):
        def xlen(self, key): return 0
        def xinfo_groups(self, key): return []
        def hlen(self, key): return 2
        def llen(self, key):
            assert key == f"rt:dlq:{plan['device_type']}:{plan['group']}"
            return 0
        def scan_iter(self, **kwargs):
            raise AssertionError("Must not inspect foreign DLQ keys")
        def close(self): self.closed = True
    client = Client()
    sqls = []
    monkeypatch.setattr(redis, "Redis", lambda **kwargs: client)
    monkeypatch.setattr(cap, "read_counts", lambda *args, **kwargs: {
        "value_rows": 1, "quality_rows": 1, "expected_value_rows": 1, "expected_quality_rows": 1})
    monkeypatch.setattr(cap, "read_sample_fields", lambda *args, **kwargs: {"problem_count": 0})
    def unavailable(**kwargs): raise ConnectionError("synthetic cleanup outage")
    monkeypatch.setattr(sys.modules["taosws"], "connect", unavailable, raising=False)
    monkeypatch.setattr(pymysql, "connect", lambda **kwargs: _FakeConn(sqls))
    output = tmp_path / "reconcile.json"
    assert joint.mode_reconcile(SimpleNamespace(
        test_config=str(config_path), plan=str(plan_path), collector_output=str(collector_path),
        sample_ticks=1, writer_cpu_seconds=None, keep_resources=False, output=str(output))) == 1
    report = json.loads(output.read_text(encoding="utf-8"))
    assert report["rows"]["value_rows"] == 1 and report["problems"] == []
    assert report["status"] == "fail"
    assert "synthetic cleanup outage" in report["cleanup"]["errors"][0]
    assert "collector:receipt:v2:own-session" in client.deleted
    assert f"rt:dlq:{plan['device_type']}:{plan['group']}" in client.deleted
    assert client.closed and len(sqls) == 1


def test_prep_failure_keeps_uncleaned_database_manifest(tmp_path, monkeypatch):
    import diagnose_joint_pipeline as joint
    import pymysql
    from types import SimpleNamespace
    config_path = _cfg_file(tmp_path)
    plan_path = tmp_path / "plan.json"
    joint.mode_plan(SimpleNamespace(test_config=str(config_path), plan=str(plan_path), devices=1,
                                   points=2, period_ms=100, seconds=1, shard_count=1))
    monkeypatch.setattr(cap, "assert_tdengine_database_absent", lambda *args: None)
    def prepare(*args, **kwargs):
        kwargs["owned_resources"].add("mysql")
        raise RuntimeError("synthetic prep failure")
    def unavailable(**kwargs): raise ConnectionError("synthetic cleanup outage")
    monkeypatch.setattr(cap, "prepare_mysql", prepare)
    monkeypatch.setattr(pymysql, "connect", unavailable)
    assert joint.mode_prep(SimpleNamespace(test_config=str(config_path), plan=str(plan_path))) == 1
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    assert plan["owned_databases"] == ["mysql"]
    assert "synthetic prep failure" in plan["prep_error"]
    assert "synthetic cleanup outage" in plan["cleanup"]["errors"][0]
