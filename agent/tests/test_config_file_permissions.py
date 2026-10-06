"""Synthetic Windows JWT files: protection covers create, load and replacement."""
import os
import subprocess

import pytest

from app.core import config

pytestmark = pytest.mark.skipif(os.name != "nt", reason="Windows configuration ACLs")


def _assert_private(path):
    import win32security as security
    sd = security.GetNamedSecurityInfo(str(path), security.SE_FILE_OBJECT, security.DACL_SECURITY_INFORMATION)
    assert sd.GetSecurityDescriptorControl()[0] & security.SE_DACL_PROTECTED
    acl = sd.GetSecurityDescriptorDacl()
    principals = {security.ConvertSidToStringSid(acl.GetAce(i)[2]) for i in range(acl.GetAceCount())}
    assert "S-1-5-18" in principals and "S-1-5-32-544" in principals
    assert not principals.intersection({"S-1-5-32-545", "S-1-1-0", "S-1-5-11"})


@pytest.fixture
def isolated_config(tmp_path, monkeypatch):
    path = tmp_path / "config.toml"
    monkeypatch.setattr(config, "CONFIG_PATH", path)
    monkeypatch.setenv("DATAWATCH_DATA_ROOT", str(tmp_path))
    return path


def test_temp_is_protected_before_serialization_and_remains_private(isolated_config, monkeypatch):
    cfg = config._default_config()
    original_dump = config.toml.dump
    def checked_dump(value, handle):
        _assert_private(isolated_config.with_name(".config.toml.tmp"))
        return original_dump(value, handle)
    monkeypatch.setattr(config.toml, "dump", checked_dump)
    config.save_config(cfg)
    _assert_private(isolated_config)
    assert config.load_config()["agent"]["jwt_secret"] == cfg["agent"]["jwt_secret"]
    config.mutate_config(lambda value: value["agent"].update(log_level="DEBUG"))
    _assert_private(isolated_config)
    assert not isolated_config.with_name(".config.toml.tmp").exists()


def test_load_repairs_explicit_and_inherited_grants_without_rotating(isolated_config):
    cfg = config._default_config()
    config.save_config(cfg)
    stale = isolated_config.with_name(".config.toml.tmp")
    stale.write_text(isolated_config.read_text(encoding="utf-8"), encoding="utf-8")
    before = isolated_config.read_bytes()
    for path in (isolated_config, stale):
        result = subprocess.run(["icacls", str(path), "/inheritance:e", "/grant",
                                 "*S-1-5-32-545:RX", "*S-1-1-0:RX"], capture_output=True)
        assert result.returncode == 0
    config.initialize_config_permissions()
    assert config.load_config()["agent"]["jwt_secret"] == cfg["agent"]["jwt_secret"]
    assert isolated_config.read_bytes() == before
    _assert_private(isolated_config)
    _assert_private(stale)


def test_save_failure_preserves_original_and_removes_secret_temp(isolated_config, monkeypatch):
    cfg = config._default_config()
    config.save_config(cfg)
    before = isolated_config.read_bytes()
    def failed_replace(*args):
        raise OSError("synthetic replacement failure")
    monkeypatch.setattr(config.os, "replace", failed_replace)
    with pytest.raises(OSError, match="synthetic"):
        config.save_config(cfg)
    assert isolated_config.read_bytes() == before
    assert not isolated_config.with_name(".config.toml.tmp").exists()
    _assert_private(isolated_config)


def test_acl_failure_refuses_to_use_existing_secret(isolated_config, monkeypatch):
    config.save_config(config._default_config())
    def failed_acl(path):
        raise PermissionError("synthetic ACL failure")
    monkeypatch.setattr(config, "_restrict_config_acl", failed_acl)
    with pytest.raises(PermissionError, match="synthetic"):
        config.initialize_config_permissions()


def test_regular_load_does_not_rewrite_acl(isolated_config, monkeypatch):
    cfg = config._default_config()
    config.save_config(cfg)
    config.initialize_config_permissions()
    def forbidden(path):
        raise AssertionError("ACL write in authentication read path")
    monkeypatch.setattr(config, "_restrict_config_acl", forbidden)
    for _ in range(3):
        assert config.load_config()["agent"]["jwt_secret"] == cfg["agent"]["jwt_secret"]
