/**
 * API 接口类型定义模块
 *
 * 提供所有后端接口的类型定义
 *
 * ## 主要功能
 *
 * - 通用类型（分页参数、响应结构等）
 * - 认证类型（登录、用户信息等）
 * - 系统管理类型（用户、角色等）
 * - 全局命名空间声明
 *
 * ## 使用场景
 *
 * - API 请求参数类型约束
 * - API 响应数据类型定义
 * - 接口文档类型同步
 *
 * ## 注意事项
 *
 * - 在 .vue 文件使用需要在 eslint.config.mjs 中配置 globals: { Api: 'readonly' }
 * - 使用全局命名空间，无需导入即可使用
 *
 * ## 使用方式
 *
 * ```typescript
 * const params: Api.Auth.LoginParams = { userName: 'admin', password: '123456' }
 * const response: Api.Auth.UserInfo = await fetchUserInfo()
 * ```
 *
 * @module types/api/api
 * @author Art Design Pro Team
 */

declare namespace Api {
  /** 通用类型 */
  namespace Common {
    /** 分页参数 */
    interface PaginationParams {
      /** 当前页码 */
      current: number
      /** 每页条数 */
      size: number
      /** 总条数 */
      total: number
    }

    /** 通用搜索参数 */
    type CommonSearchParams = Pick<PaginationParams, 'current' | 'size'>

    /** 分页响应基础结构 */
    interface PaginatedResponse<T = any> {
      records: T[]
      current: number
      size: number
      total: number
    }

    /** 启用状态 */
    type EnableStatus = '1' | '2'
  }

  /** 认证类型 */
  namespace Auth {
    /** 登录参数 */
    interface LoginParams {
      userName: string
      password: string
    }

    /** 登录响应 */
    interface LoginResponse {
      token: string
      refreshToken: string
    }

    /** 用户信息 */
    interface UserInfo {
      buttons: string[]
      roles: string[]
      userId: number
      userName: string
      email: string
      avatar?: string
    }
  }

  /** 系统管理类型 */
  namespace SystemManage {
    /** 用户列表 */
    type UserList = Api.Common.PaginatedResponse<UserListItem>

    /** 用户列表项 */
    interface UserListItem {
      id: number
      avatar: string
      status: string
      userName: string
      userGender: string
      nickName: string
      userPhone: string
      userEmail: string
      userRoles: string[]
      createBy: string
      createTime: string
      updateBy: string
      updateTime: string
    }

    /** 用户搜索参数 */
    type UserSearchParams = Partial<
      Pick<UserListItem, 'id' | 'userName' | 'userGender' | 'userPhone' | 'userEmail' | 'status'> &
        Api.Common.CommonSearchParams
    >

    /** 用户表单参数 */
    interface UserPayload {
      userName: string
      nickName?: string
      userPhone?: string
      userEmail?: string
      userGender?: string
      avatar?: string
      status?: string
      userRoles?: string[]
      password?: string
    }

    /** 角色列表 */
    type RoleList = Api.Common.PaginatedResponse<RoleListItem>

    /** 角色列表项 */
    interface RoleListItem {
      roleId: number
      roleName: string
      roleCode: string
      description: string
      enabled: boolean
      createTime: string
    }

    /** 角色搜索参数 */
    type RoleSearchParams = Partial<
      Pick<RoleListItem, 'roleId' | 'roleName' | 'roleCode' | 'description' | 'enabled'> &
        Api.Common.CommonSearchParams & {
          startTime: string | null
          endTime: string | null
        }
    >

    /** 角色选项 */
    interface RoleOption {
      roleId: number
      roleName: string
      roleCode: string
      enabled: boolean
    }

    /** 角色表单参数 */
    interface RolePayload {
      roleName: string
      roleCode: string
      description?: string
      enabled?: boolean
    }

    /** 角色授权响应 */
    interface RoleGrant {
      menuIds: number[]
      permissionIds: number[]
      dataScopes: DataScopeItem[]
    }

    /** 角色授权参数 */
    interface RoleGrantPayload {
      menuIds: number[]
      permissionIds: number[]
      dataScopes: DataScopeItem[]
    }

    /** 数据权限项 */
    interface DataScopeItem {
      id?: number
      scopeType: string
      scopeValue: string
      enabled?: boolean
    }

    /** 系统菜单树项 */
    interface SystemMenuTreeItem {
      id: number
      parentId?: number
      path: string
      name: string
      component: string
      menuType?: string
      sort?: number
      enabled?: boolean
      meta: {
        title: string
        icon?: string
        keepAlive?: boolean
        isHide?: boolean
        isHideTab?: boolean
        link?: string
        isIframe?: boolean
        fixedTab?: boolean
        activePath?: string
        isFullPage?: boolean
        authList?: PermissionItem[]
      }
      children?: SystemMenuTreeItem[]
    }

    /** 菜单表单参数 */
    interface MenuPayload {
      name: string
      path: string
      component: string
      titleKey: string
      icon?: string
      parentId?: number
      menuType?: string
      link?: string
      isIframe?: boolean
      keepAlive?: boolean
      isHide?: boolean
      isHideTab?: boolean
      fixedTab?: boolean
      activePath?: string
      isFullPage?: boolean
      sort?: number
      enabled?: boolean
    }

    /** 权限项 */
    interface PermissionItem {
      id: number
      title: string
      authMark: string
      enabled?: boolean
      permissionCode?: string
      permissionType?: string
      sort?: number
    }

    /** 权限表单参数 */
    interface PermissionPayload {
      permissionCode: string
      permissionName: string
      permissionType?: string
      authMark?: string
      description?: string
      sort?: number
      enabled?: boolean
    }
  }
}
