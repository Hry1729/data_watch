"""PR #22 第二轮：设置页保存的 Redis DB 必须落到服务实际读取的 [redis].db。

App 设置页 PUT /admin/config 发送 {"redis": {"database": "<n>"}}；collector/writer/ingest
的 Redis DB 来自 Agent 启动时按 [redis].db 下发的 DATAWATCH_REDIS_DB / INGEST_REDIS_URL。
修复前 database 只写进 [redis].database，从未被读取（保存看似成功但不生效）。
"""
import asyncio

import pytest
import toml
from fastapi import HTTPException

from app.core import config
from app.routers import admin
from app.schemas.models import ConnectionConfigIn


@pytest.fixture
def isolated_config(tmp_path, monkeypatch):
    path = tmp_path / "config.toml"
    monkeypatch.setattr(config, "CONFIG_PATH", path)
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    config.save_config(config._default_config())
    return path


def _put(body: dict):
    return asyncio.run(admin.update_config(ConnectionConfigIn(**body), _user="admin"))


def test_settings_redis_db_lands_in_the_key_services_read(isolated_config, monkeypatch):
    _put({"redis": {"database": "3"}})

    stored = toml.load(isolated_config)["redis"]
    assert stored["db"] == 3
    assert admin._stream_contract(config.load_config()) == (3, 4)

    monkeypatch.setattr(admin, "dpapi_decrypt", lambda _value: "")
    assert admin._writer_env()["DATAWATCH_REDIS_DB"] == "3"
    assert admin._ingest_env()["INGEST_REDIS_URL"].endswith("/3")

    # GET 回显实际生效值
    out = asyncio.run(admin.get_config(_user="admin"))
    assert out.redis.database == "3"


@pytest.mark.parametrize("bad", ["abc", "-1", "1.5", ""])
def test_invalid_redis_db_is_rejected_with_400_and_not_saved(isolated_config, bad):
    with pytest.raises(HTTPException) as exc:
        _put({"redis": {"database": bad}})
    assert exc.value.status_code == 400
    assert toml.load(isolated_config)["redis"]["db"] == 0


def test_mysql_and_tdengine_database_still_saved_as_database(isolated_config):
    _put({"mysql": {"database": "dw2"}, "tdengine": {"database": "td2"}})
    stored = toml.load(isolated_config)
    assert stored["mysql"]["database"] == "dw2"
    assert stored["tdengine"]["database"] == "td2"
    assert "db" not in stored["mysql"]


def test_legacy_unread_database_key_is_shown_as_the_effective_db(isolated_config):
    # 旧版本只写了 database="5"、从未生效；回显必须是实际生效的 0，而不是假装已是 5
    cfg = toml.load(isolated_config)
    cfg["redis"].pop("db", None)
    cfg["redis"]["database"] = "5"
    isolated_config.write_text(toml.dumps(cfg), encoding="utf-8")

    loaded = config.load_config()
    assert loaded["redis"]["database"] == "0"
    assert admin._stream_contract(loaded)[0] == 0


def test_bootstrap_redis_database_also_writes_db():
    from app.routers.bootstrap import _apply_db_section
    from app.schemas.models import BootstrapDbIn

    cfg: dict = {}
    _apply_db_section(cfg, "redis", BootstrapDbIn(host="rds", port=6379, database="2"))
    assert cfg["redis"]["db"] == 2
    with pytest.raises(HTTPException) as exc:
        _apply_db_section({}, "redis", BootstrapDbIn(host="rds", port=6379, database="x"))
    assert exc.value.status_code == 400
