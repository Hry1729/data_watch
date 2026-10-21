/**
 * 角色授权保存载荷（全量覆盖）。
 *
 * 后端契约（PR #23 / B021）：menuIds 与 permissionIds 均为空表示清空该角色全部菜单和权限，
 * 必须显式携带 clearAll: true，否则 400；其余情况不发送 clearAll。
 */
export interface RoleGrantPayloadInput<TScope> {
  menuIds: number[]
  permissionIds: number[]
  dataScopes: TScope[]
}

export interface BuiltRoleGrantPayload<TScope> extends RoleGrantPayloadInput<TScope> {
  clearAll?: true
}

export function isClearAllGrant(menuIds: number[], permissionIds: number[]): boolean {
  return menuIds.length === 0 && permissionIds.length === 0
}

export function buildRoleGrantPayload<TScope>(
  input: RoleGrantPayloadInput<TScope>
): BuiltRoleGrantPayload<TScope> {
  const menuIds = [...new Set(input.menuIds)]
  const permissionIds = [...new Set(input.permissionIds)]
  const payload: BuiltRoleGrantPayload<TScope> = {
    menuIds,
    permissionIds,
    dataScopes: [...input.dataScopes]
  }
  if (isClearAllGrant(menuIds, permissionIds)) payload.clearAll = true
  return payload
}
