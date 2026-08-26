/** 系统管理分页结构（与 Web 端 / 后端 records 格式对齐） */
export interface SystemPaginatedResult<T> {
  records: T[];
  current: number;
  size: number;
  total: number;
}

export interface UserListItem {
  id: number;
  avatar: string;
  status: string;
  userName: string;
  userGender: string;
  nickName: string;
  userPhone: string;
  userEmail: string;
  userRoles: string[];
  createBy: string;
  createTime: string;
  updateBy: string;
  updateTime: string;
}

export interface UserQuery {
  current?: number;
  size?: number;
  userName?: string;
  userGender?: string;
  userPhone?: string;
  userEmail?: string;
  status?: string;
}

export interface UserPayload {
  userName: string;
  nickName?: string;
  userPhone?: string;
  userEmail?: string;
  userGender?: string;
  avatar?: string;
  status?: string;
  userRoles?: string[];
}

export type UserPageResult = SystemPaginatedResult<UserListItem>;

export interface RoleListItem {
  roleId: number;
  roleName: string;
  roleCode: string;
  description: string;
  enabled: boolean;
  createTime: string;
}

export interface RoleQuery {
  current?: number;
  size?: number;
  roleName?: string;
  roleCode?: string;
  description?: string;
  enabled?: boolean | "";
  startTime?: string;
  endTime?: string;
}

export interface RoleOption {
  roleId: number;
  roleName: string;
  roleCode: string;
  enabled: boolean;
}

export interface RolePayload {
  roleName: string;
  roleCode: string;
  description?: string;
  enabled?: boolean;
}

export interface DataScopeItem {
  id?: number;
  scopeType: string;
  scopeValue: string;
  enabled?: boolean;
}

export interface RoleGrant {
  menuIds: number[];
  permissionIds: number[];
  dataScopes: DataScopeItem[];
}

export type RolePageResult = SystemPaginatedResult<RoleListItem>;

export interface PermissionItem {
  id: number;
  title: string;
  authMark: string;
  enabled?: boolean;
  permissionCode?: string;
  permissionType?: string;
  sort?: number;
}

export interface MenuTreeItem {
  id: number;
  parentId?: number;
  path: string;
  name: string;
  component: string;
  menuType?: string;
  sort?: number;
  enabled?: boolean;
  meta: {
    title: string;
    icon?: string;
    keepAlive?: boolean;
    isHide?: boolean;
    isHideTab?: boolean;
    link?: string;
    isIframe?: boolean;
    fixedTab?: boolean;
    activePath?: string;
    isFullPage?: boolean;
    authList?: PermissionItem[];
  };
  children?: MenuTreeItem[];
}

export interface MenuPayload {
  name: string;
  path: string;
  component: string;
  titleKey: string;
  icon?: string;
  parentId?: number;
  menuType?: string;
  link?: string;
  isIframe?: boolean;
  keepAlive?: boolean;
  isHide?: boolean;
  isHideTab?: boolean;
  fixedTab?: boolean;
  activePath?: string;
  isFullPage?: boolean;
  sort?: number;
  enabled?: boolean;
}

export interface PermissionPayload {
  permissionCode: string;
  permissionName: string;
  permissionType?: string;
  authMark?: string;
  description?: string;
  sort?: number;
  enabled?: boolean;
}
