/**
 * 菜单处理器
 *
 * 负责菜单数据的获取、过滤和处理
 *
 * @module router/core/MenuProcessor
 * @author Art Design Pro Team
 */

import type { AppRouteRecord } from '@/types/router'
import { useUserStore } from '@/store/modules/user'
import { useAppMode } from '@/hooks/core/useAppMode'
import { fetchGetMenuList } from '@/api/system-manage'
import { asyncRoutes } from '../routes/asyncRoutes'
import { RoutesAlias } from '../routesAlias'
import { formatMenuTitle } from '@/utils'
import { buildProjectDocsMenu } from '@/views/project-docs/docs.menu'

type LocalMenuModule = {
  buildMonitorScreenMenu?: () => AppRouteRecord
  buildLightMonitorMenu?: () => AppRouteRecord
  buildLandingMenu?: () => AppRouteRecord
}

/**
 * 监控大屏、浅色大屏、项目首页是本地未完成页面，干净检出里可能没有目录。
 * 必须用字面量 glob：目录缺失时结果为空。写成 import('./landing/...') 会让 Vite 构建失败。
 * 查找键要和 glob 展开后的相对路径一致。
 */
const localPages = import.meta.glob('../../views/{landing,monitor-light,monitor-screen}/index.vue')
const localMenuModules = import.meta.glob<LocalMenuModule>(
  '../../views/{landing,monitor-light,monitor-screen}/screen.menu.ts',
  { eager: true }
)

const LOCAL_MENU_ORDER = ['monitor-screen', 'monitor-light', 'landing'] as const

/** 页面文件和菜单模块都在时才注入；缺任一文件就跳过，避免引用不存在的目录。 */
function collectLocalMenus(): AppRouteRecord[] {
  const menus: AppRouteRecord[] = []
  for (const dir of LOCAL_MENU_ORDER) {
    if (!localPages[`../../views/${dir}/index.vue`]) continue
    const mod = localMenuModules[`../../views/${dir}/screen.menu.ts`]
    const menu =
      dir === 'monitor-screen'
        ? mod?.buildMonitorScreenMenu?.()
        : dir === 'monitor-light'
          ? mod?.buildLightMonitorMenu?.()
          : mod?.buildLandingMenu?.()
    if (menu) menus.push(menu)
  }
  return menus
}

export class MenuProcessor {
  /**
   * 获取菜单数据
   */
  async getMenuList(): Promise<AppRouteRecord[]> {
    const { isFrontendMode } = useAppMode()

    let menuList: AppRouteRecord[]
    if (isFrontendMode.value) {
      menuList = await this.processFrontendMenu()
    } else {
      menuList = await this.processBackendMenu()
    }

    // 注入前端管理的菜单项（不依赖后端 sys_menu，对所有登录用户可见）。
    // 顺序：监控大屏 → 浅色监控大屏 → 项目首页 → 后端菜单 → 项目文档。
    // 本地目录不存在时，对应项直接跳过。
    menuList = [...collectLocalMenus(), ...menuList, buildProjectDocsMenu()]

    // 在规范化路径之前，验证原始路径配置
    this.validateMenuPaths(menuList)

    // 规范化路径（将相对路径转换为完整路径）
    return this.normalizeMenuPaths(menuList)
  }

  /**
   * 处理前端控制模式的菜单
   */
  private async processFrontendMenu(): Promise<AppRouteRecord[]> {
    const userStore = useUserStore()
    const roles = userStore.info?.roles

    let menuList = [...asyncRoutes]

    // 根据角色过滤菜单
    if (roles && roles.length > 0) {
      menuList = this.filterMenuByRoles(menuList, roles)
    }

    return this.filterEmptyMenus(menuList)
  }

  /**
   * 处理后端控制模式的菜单
   */
  private async processBackendMenu(): Promise<AppRouteRecord[]> {
    const list = await fetchGetMenuList()
    return this.filterEmptyMenus(list)
  }

  /**
   * 根据角色过滤菜单
   */
  private filterMenuByRoles(menu: AppRouteRecord[], roles: string[]): AppRouteRecord[] {
    return menu.reduce((acc: AppRouteRecord[], item) => {
      const itemRoles = item.meta?.roles
      const hasPermission = !itemRoles || itemRoles.some((role) => roles?.includes(role))

      if (hasPermission) {
        const filteredItem = { ...item }
        if (filteredItem.children?.length) {
          filteredItem.children = this.filterMenuByRoles(filteredItem.children, roles)
        }
        acc.push(filteredItem)
      }

      return acc
    }, [])
  }

  /**
   * 递归过滤空菜单项
   */
  private filterEmptyMenus(menuList: AppRouteRecord[]): AppRouteRecord[] {
    return menuList
      .map((item) => {
        // 如果有子菜单，先递归过滤子菜单
        if (item.children && item.children.length > 0) {
          const filteredChildren = this.filterEmptyMenus(item.children)
          return {
            ...item,
            children: filteredChildren
          }
        }
        return item
      })
      .filter((item) => {
        // 如果定义了 children 属性（即使是空数组），说明这是一个目录菜单，应该保留
        if ('children' in item) {
          return true
        }

        // 如果有外链或 iframe，保留
        if (item.meta?.isIframe === true || item.meta?.link) {
          return true
        }

        // 如果有有效的 component，保留
        if (item.component && item.component !== '' && item.component !== RoutesAlias.Layout) {
          return true
        }

        // 其他情况过滤掉
        return false
      })
  }

  /**
   * 验证菜单列表是否有效
   */
  validateMenuList(menuList: AppRouteRecord[]): boolean {
    return Array.isArray(menuList) && menuList.length > 0
  }

  /**
   * 规范化菜单路径
   * 将相对路径转换为完整路径，确保菜单跳转正确
   */
  private normalizeMenuPaths(menuList: AppRouteRecord[], parentPath = ''): AppRouteRecord[] {
    return menuList.map((item) => {
      // 构建完整路径
      const fullPath = this.buildFullPath(item.path || '', parentPath)

      // 递归处理子菜单
      const children = item.children?.length
        ? this.normalizeMenuPaths(item.children, fullPath)
        : item.children

      const redirect = item.redirect || this.resolveDefaultRedirect(children)

      return {
        ...item,
        path: fullPath,
        redirect,
        children
      }
    })
  }

  /**
   * 为目录型菜单推导默认跳转地址
   */
  private resolveDefaultRedirect(children?: AppRouteRecord[]): string | undefined {
    if (!children?.length) {
      return undefined
    }

    for (const child of children) {
      if (this.isNavigableRoute(child)) {
        return child.path
      }

      const nestedRedirect = this.resolveDefaultRedirect(child.children)
      if (nestedRedirect) {
        return nestedRedirect
      }
    }

    return undefined
  }

  /**
   * 判断子路由是否可以作为默认落点
   */
  private isNavigableRoute(route: AppRouteRecord): boolean {
    return Boolean(
      route.path &&
        route.path !== '/' &&
        !route.meta?.link &&
        route.meta?.isIframe !== true &&
        route.component &&
        route.component !== ''
    )
  }

  /**
   * 验证菜单路径配置
   * 检测非一级菜单是否错误使用了 / 开头的路径
   */
  /**
   * 验证菜单路径配置
   * 检测非一级菜单是否错误使用了 / 开头的路径
   */
  private validateMenuPaths(menuList: AppRouteRecord[], level = 1): void {
    menuList.forEach((route) => {
      if (!route.children?.length) return

      const parentName = String(route.name || route.path || '未知路由')

      route.children.forEach((child) => {
        const childPath = child.path || ''

        // 跳过合法的绝对路径：外部链接和 iframe 路由
        if (this.isValidAbsolutePath(childPath)) return

        // 检测非法的绝对路径
        if (childPath.startsWith('/')) {
          this.logPathError(child, childPath, parentName, level)
        }
      })

      // 递归检查更深层级的子路由
      this.validateMenuPaths(route.children, level + 1)
    })
  }

  /**
   * 判断是否为合法的绝对路径
   */
  private isValidAbsolutePath(path: string): boolean {
    return (
      path.startsWith('http://') ||
      path.startsWith('https://') ||
      path.startsWith('/outside/iframe/')
    )
  }

  /**
   * 输出路径配置错误日志
   */
  private logPathError(
    route: AppRouteRecord,
    path: string,
    parentName: string,
    level: number
  ): void {
    const routeName = String(route.name || path || '未知路由')
    const menuTitle = route.meta?.title || routeName
    const suggestedPath = path.split('/').pop() || path.slice(1)

    console.error(
      `[路由配置错误] 菜单 "${formatMenuTitle(menuTitle)}" (name: ${routeName}, path: ${path}) 配置错误\n` +
        `  位置: ${parentName} > ${routeName}\n` +
        `  问题: ${level + 1}级菜单的 path 不能以 / 开头\n` +
        `  当前配置: path: '${path}'\n` +
        `  应该改为: path: '${suggestedPath}'`
    )
  }

  /**
   * 构建完整路径
   */
  private buildFullPath(path: string, parentPath: string): string {
    if (!path) return ''

    // 外部链接直接返回
    if (path.startsWith('http://') || path.startsWith('https://')) {
      return path
    }

    // 如果已经是绝对路径，直接返回
    if (path.startsWith('/')) {
      return path
    }

    // 拼接父路径和当前路径
    if (parentPath) {
      // 移除父路径末尾的斜杠，移除子路径开头的斜杠，然后拼接
      const cleanParent = parentPath.replace(/\/$/, '')
      const cleanChild = path.replace(/^\//, '')
      return `${cleanParent}/${cleanChild}`
    }

    // 没有父路径，添加前导斜杠
    return `/${path}`
  }
}
