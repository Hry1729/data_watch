from __future__ import annotations

import pytest

from app.api.routes import module_monitor
from app.services.auth_bootstrap import PERMISSION_SEEDS, ROLE_PERMISSION_GRANTS


PERMISSION = "device_monitor:collector_config:update"


def test_collector_config_permission_is_not_granted_to_default_user() -> None:
    assert PERMISSION in {seed.permission_code for seed in PERMISSION_SEEDS}
    assert PERMISSION in ROLE_PERMISSION_GRANTS["R_SUPER"]
    assert PERMISSION in ROLE_PERMISSION_GRANTS["R_ADMIN"]
    assert PERMISSION not in ROLE_PERMISSION_GRANTS["R_USER"]


@pytest.mark.asyncio
async def test_collector_config_put_checks_dedicated_permission(monkeypatch) -> None:
    class PermissionChecked(Exception):
        pass

    async def fake_require_permissions(_credentials, required):
        assert required == PERMISSION
        raise PermissionChecked

    monkeypatch.setattr(module_monitor, "require_permissions", fake_require_permissions)

    with pytest.raises(PermissionChecked):
        await module_monitor.update_collector_config(
            {"streamMaxlen": 100000},
            credentials=None,
        )
