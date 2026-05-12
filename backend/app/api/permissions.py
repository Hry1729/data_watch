from __future__ import annotations

from collections.abc import Iterable

from turboapi.security import HTTPAuthorizationCredentials

from app.core.exceptions import ApiError
from app.db.mysql import get_mysql_session_factory
from app.services.auth import AuthService


async def require_permissions(
    credentials: HTTPAuthorizationCredentials | None,
    required_permissions: str | Iterable[str],
) -> dict:
    return await require_permissions_from_authorization(
        authorization_from_credentials(credentials),
        required_permissions,
    )


async def require_permissions_from_authorization(
    authorization: str | None,
    required_permissions: str | Iterable[str],
) -> dict:
    required = _normalize_required_permissions(required_permissions)
    session_factory = get_mysql_session_factory()
    async with session_factory() as session:
        service = AuthService(session)
        principal = await service.resolve_principal(authorization)
        granted = set(await service.fetch_user_permission_codes(principal["user_id"]))
    if required and not required.intersection(granted):
        raise ApiError(403, "无权限访问该接口")
    return principal


def authorization_from_credentials(
    credentials: HTTPAuthorizationCredentials | None,
) -> str | None:
    if credentials is None:
        return None
    return f"{credentials.scheme} {credentials.credentials}".strip()


def _normalize_required_permissions(required_permissions: str | Iterable[str]) -> set[str]:
    if isinstance(required_permissions, str):
        return {required_permissions}
    return {str(item).strip() for item in required_permissions if str(item).strip()}
