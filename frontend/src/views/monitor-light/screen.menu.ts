/**
 * 「浅色监控大屏」一级菜单
 *
 * 由前端注入到菜单列表（见 router/core/MenuProcessor），
 * 不依赖后端 sys_menu，对所有登录用户可见。
 */
import type { AppRouteRecord } from '@/types/router'

/** 构建浅色监控大屏一级菜单（无子级，点击直接进入全屏页） */
export function buildLightMonitorMenu(): AppRouteRecord {
  return {
    name: 'LightMonitorScreen',
    path: '/monitor-light',
    component: '/monitor-light/index',
    meta: {
      title: '浅色监控大屏',
      icon: 'ri:sun-line',
      isFullPage: true,
      keepAlive: false
    }
  }
}
