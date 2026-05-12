import request from '@/utils/http'
import type { AppRouteRecord } from '@/types/router'

// ==================== 用户相关 ====================

// 获取用户列表
export function fetchGetUserList(params: Api.SystemManage.UserSearchParams) {
  return request.get<Api.SystemManage.UserList>({
    url: '/api/user/list',
    params
  })
}

// 获取用户详情
export function fetchGetUserDetail(userId: number) {
  return request.get<Api.SystemManage.UserListItem>({
    url: `/api/user/${userId}`
  })
}

// 新增用户
export function fetchCreateUser(data: Api.SystemManage.UserPayload) {
  return request.post<{ id: number }>({
    url: '/api/user',
    data
  })
}

// 编辑用户
export function fetchUpdateUser(userId: number, data: Api.SystemManage.UserPayload) {
  return request.put<{ id: number }>({
    url: `/api/user/${userId}`,
    data
  })
}

// 更新用户状态
export function fetchUpdateUserStatus(userId: number, status: string) {
  return request.patch<{ id: number }>({
    url: `/api/user/${userId}/status`,
    data: { status }
  })
}

// 重置用户密码
export function fetchResetUserPassword(userId: number, password?: string) {
  return request.patch<{ id: number }>({
    url: `/api/user/${userId}/password/reset`,
    data: { password: password || '123456' }
  })
}

// 删除用户
export function fetchDeleteUser(userId: number) {
  return request.del<{ id: number }>({
    url: `/api/user/${userId}`
  })
}

// ==================== 角色相关 ====================

// 获取角色列表
export function fetchGetRoleList(params: Api.SystemManage.RoleSearchParams) {
  return request.get<Api.SystemManage.RoleList>({
    url: '/api/role/list',
    params
  })
}

// 获取角色选项
export function fetchGetRoleOptions() {
  return request.get<Api.SystemManage.RoleOption[]>({
    url: '/api/role/options'
  })
}

// 获取角色详情
export function fetchGetRoleDetail(roleId: number) {
  return request.get<Api.SystemManage.RoleListItem>({
    url: `/api/role/${roleId}`
  })
}

// 新增角色
export function fetchCreateRole(data: Api.SystemManage.RolePayload) {
  return request.post<{ id: number }>({
    url: '/api/role',
    data
  })
}

// 编辑角色
export function fetchUpdateRole(roleId: number, data: Api.SystemManage.RolePayload) {
  return request.put<{ id: number }>({
    url: `/api/role/${roleId}`,
    data
  })
}

// 更新角色状态
export function fetchUpdateRoleStatus(roleId: number, enabled: boolean) {
  return request.patch<{ id: number }>({
    url: `/api/role/${roleId}/status`,
    data: { enabled }
  })
}

// 删除角色
export function fetchDeleteRole(roleId: number) {
  return request.del<{ id: number }>({
    url: `/api/role/${roleId}`
  })
}

// 获取角色授权
export function fetchGetRoleGrant(roleId: number) {
  return request.get<Api.SystemManage.RoleGrant>({
    url: `/api/role/${roleId}/grant`
  })
}

// 更新角色授权
export function fetchUpdateRoleGrant(roleId: number, data: Api.SystemManage.RoleGrantPayload) {
  return request.put<{ id: number }>({
    url: `/api/role/${roleId}/grant`,
    data
  })
}

// ==================== 菜单相关 ====================

// 获取当前用户菜单
export function fetchGetMenuList() {
  return request.get<AppRouteRecord[]>({
    url: '/api/v3/system/menus'
  })
}

// 获取管理员菜单树（含禁用菜单）
export function fetchGetAdminMenus() {
  return request.get<Api.SystemManage.SystemMenuTreeItem[]>({
    url: '/api/v3/system/menus/admin'
  })
}

// 新增菜单
export function fetchCreateMenu(data: Api.SystemManage.MenuPayload) {
  return request.post<{ id: number }>({
    url: '/api/v3/system/menu',
    data
  })
}

// 编辑菜单
export function fetchUpdateMenu(menuId: number, data: Api.SystemManage.MenuPayload) {
  return request.put<{ id: number }>({
    url: `/api/v3/system/menu/${menuId}`,
    data
  })
}

// 删除菜单
export function fetchDeleteMenu(menuId: number) {
  return request.del<{ id: number }>({
    url: `/api/v3/system/menu/${menuId}`
  })
}

// 获取菜单权限列表
export function fetchGetMenuPermissions(menuId: number) {
  return request.get<Api.SystemManage.PermissionItem[]>({
    url: `/api/v3/system/menu/${menuId}/permissions`
  })
}

// 新增菜单权限
export function fetchCreateMenuPermission(menuId: number, data: Api.SystemManage.PermissionPayload) {
  return request.post<{ id: number }>({
    url: `/api/v3/system/menu/${menuId}/permissions`,
    data
  })
}

// 编辑菜单权限
export function fetchUpdateMenuPermission(menuId: number, permissionId: number, data: Api.SystemManage.PermissionPayload) {
  return request.put<{ id: number }>({
    url: `/api/v3/system/menu/${menuId}/permissions/${permissionId}`,
    data
  })
}

// 删除菜单权限
export function fetchDeleteMenuPermission(menuId: number, permissionId: number) {
  return request.del<{ id: number }>({
    url: `/api/v3/system/menu/${menuId}/permissions/${permissionId}`
  })
}
