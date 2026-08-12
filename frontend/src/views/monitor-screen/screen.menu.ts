/**
 * 「监控大屏」一级菜单
 *
 * 由前端注入到菜单列表最顶部（见 router/core/MenuProcessor），
 * 不依赖后端 sys_menu，对所有登录用户可见。
 */
import type { AppRouteRecord } from '@/types/router'

/** 构建监控大屏一级菜单（无子级，点击直接进入全屏大屏页） */
export function buildMonitorScreenMenu(): AppRouteRecord {
  return {
    name: 'MonitorScreen',
    path: '/monitor-screen',
    component: '/monitor-screen/index',
    meta: {
      title: 'menus.monitorScreen.title',
      icon: 'ri:tv-2-line',
      isFullPage: true,
      keepAlive: false
    }
  }
}
