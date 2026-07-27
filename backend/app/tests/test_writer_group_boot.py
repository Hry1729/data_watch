"""Writer group 解析：统一经 Settings；Docker 缺文件不崩。"""

from __future__ import annotations

import sys
from unittest.mock import MagicMock

import pytest

sys.modules.setdefault("sqlalchemy", MagicMock())
sys.modules.setdefault("sqlalchemy.ext", MagicMock())
sys.modules.setdefault("sqlalchemy.ext.asyncio", MagicMock())

from app.core import config as config_mod  # noqa: E402
from app.core import writer_group as wg  # noqa: E402


@pytest.fixture(autouse=True)
def _clear_caches():
    wg.get_writer_group_name.cache_clear()
    config_mod.get_settings.cache_clear()
    yield
    wg.get_writer_group_name.cache_clear()
    config_mod.get_settings.cache_clear()


def test_writer_group_from_env(monkeypatch):
    monkeypatch.setenv("WRITER_REDIS_GROUP_NAME", "from_env_cg")
    assert wg.get_writer_group_name() == "from_env_cg"
    assert config_mod.get_settings().writer_redis_group_name == "from_env_cg"


def test_writer_group_survives_missing_dotenv(monkeypatch, tmp_path):
    """无 .env / 无额外配置时 Settings 默认 writer_cg。"""
    monkeypatch.delenv("WRITER_REDIS_GROUP_NAME", raising=False)
    # 避免本地 .env 干扰：用空默认覆盖 backend env defaults
    monkeypatch.setattr(config_mod, "_load_backend_env_defaults", lambda: {})
    assert wg.get_writer_group_name() == "writer_cg"


def test_writer_group_via_settings_matches_env(monkeypatch):
    monkeypatch.setenv("WRITER_REDIS_GROUP_NAME", "settings_unified_cg")
    assert wg.get_writer_group_name() == config_mod.get_settings().writer_redis_group_name
    assert wg.get_writer_group_name() == "settings_unified_cg"
