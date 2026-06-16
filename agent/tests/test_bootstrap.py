"""引导页四段配置 + 运行期后端地址 + 四目标连通性测试。

直接 await 路由协程 + monkeypatch（不落真实 config、不连真 DB、不调 DPAPI）。
"""

import pytest
from fastapi import HTTPException

from app.routers import bootstrap as bs
from app.schemas.models import (
    BootstrapConfigRequest,
    HealthCheckResponse,
)
from app.schemas.models import TestConnectionRequest as ConnReq  # 避免 pytest 误收集


@pytest.fixture
def fake_cfg(monkeypatch):
    cfg = {
        "agent": {"bootstrap_done": False},
        "backend": {"base_url": "http://127.0.0.1:8000"},
        "mysql": {},
        "redis": {},
        "tdengine": {},
    }
    # load_config 每次返回同一 dict，路由原地 mutate；save_config 设为 no-op 即可。
    monkeypatch.setattr(bs, "load_config", lambda: cfg)
    monkeypatch.setattr(bs, "save_config", lambda c: None)
    monkeypatch.setattr(bs, "dpapi_encrypt", lambda s: f"enc({s})")
    return cfg


@pytest.mark.asyncio
async def test_runtime_config_returns_backend(fake_cfg):
    resp = await bs.runtime_config()
    assert resp.backend_base_url == "http://127.0.0.1:8000"


@pytest.mark.asyncio
async def test_put_config_saves_all_sections(fake_cfg):
    body = BootstrapConfigRequest(
        backend={"base_url": "http://10.0.0.5:8000"},
        mysql={"host": "db", "port": 3306, "user": "root", "password": "p1", "database": "device_watch"},
        redis={"host": "r", "port": 6379, "password": ""},
        tdengine={"host": "t", "port": 6041, "user": "root", "password": "p2", "database": "device_data"},
    )
    await bs.bootstrap_config(body)

    assert fake_cfg["backend"]["base_url"] == "http://10.0.0.5:8000"
    assert fake_cfg["mysql"]["host"] == "db"
    assert fake_cfg["mysql"]["password_enc"] == "enc(p1)"
    assert "password" not in fake_cfg["mysql"]  # 明文不残留
    assert fake_cfg["redis"]["password_enc"] == "enc()"  # 空密码也加密存储
    assert fake_cfg["tdengine"]["password_enc"] == "enc(p2)"


@pytest.mark.asyncio
async def test_test_connection_backend(fake_cfg, monkeypatch):
    captured = {}

    def fake_backend(url):
        captured["url"] = url
        return HealthCheckResponse(success=True)

    monkeypatch.setattr(bs, "check_backend", fake_backend)
    resp = await bs.bootstrap_test_connection(ConnReq(target="backend", base_url="http://x:8000"))
    assert resp.success
    assert captured["url"] == "http://x:8000"


@pytest.mark.asyncio
async def test_test_connection_mysql_passes_inline_config(fake_cfg, monkeypatch):
    captured = {}

    def fake_check(cfg):
        captured.update(cfg)
        return HealthCheckResponse(success=True)

    monkeypatch.setattr(bs, "check_mysql", fake_check)
    resp = await bs.bootstrap_test_connection(
        ConnReq(target="mysql", host="h", port=3306, user="root", password="pw", database="d")
    )
    assert resp.success
    assert captured["host"] == "h"
    assert captured["password"] == "pw"


@pytest.mark.asyncio
async def test_finish_sets_done(fake_cfg):
    await bs.bootstrap_finish()
    assert fake_cfg["agent"]["bootstrap_done"] is True


@pytest.mark.asyncio
async def test_config_rejected_after_done(fake_cfg):
    fake_cfg["agent"]["bootstrap_done"] = True
    with pytest.raises(HTTPException) as ei:
        await bs.bootstrap_config(BootstrapConfigRequest(backend={"base_url": "http://x"}))
    assert ei.value.status_code == 403
