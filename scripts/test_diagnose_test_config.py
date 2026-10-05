"""针对性测试：诊断脚本的连接配置统一、隔离闸门、子进程环境与退出码契约。

不连任何数据库：被测的都是纯函数。能力脚本在导入期会 import taosws/psutil，
而本机 taosws 与某些解释器 ABI 不兼容（混用会 ACCESS_VIOLATION），故在导入前注入轻量替身；
被测函数都不调用真实 taosws。
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
                             group="gcap", consumer_workers=1)
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
        for forbidden in ("perf-redis", "perf-mysql", "perf-td", "taosdata"):
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
    monkeypatch.setattr(cap.taosws, "connect", lambda **kw: _FakeConn(sqls), raising=False)
    monkeypatch.setattr(cap.pymysql, "connect", lambda **kw: _FakeConn(sqls), raising=False)
    client = _FakeRedis()
    cfg = {"mysql": {"host": "127.0.0.1", "port": 13306, "user": "root", "password": "x"},
           "tdengine": {"host": "127.0.0.1", "port": 6041, "user": "root", "password": "x"}}
    dropped = cap.cleanup_round_resources(
        cfg, database="dwcap_abc", client=client,
        keys=["rt:stream:cap:gcap:1", "rt:latest:cw1:gcap"])
    assert dropped == {"tdengine_database": "dwcap_abc", "mysql_database": "dwcap_abc",
                       "redis_keys": 2}
    assert client.deleted == ["rt:stream:cap:gcap:1", "rt:latest:cw1:gcap"]
    joined = " ".join(sqls).upper()
    assert "DROP DATABASE IF EXISTS DWCAP_ABC" in joined
    assert "FLUSHALL" not in joined and "FLUSHDB" not in joined
