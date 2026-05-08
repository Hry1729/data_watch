/**
 * 设备监控模块路由配置
 */
import { AppRouteRecord } from '@/types/router'

export const deviceMonitorRoutes: AppRouteRecord = {
  name: 'DeviceMonitor',
  path: '/device-monitor',
  component: '/index/index',
  meta: {
    title: 'menus.deviceMonitor.title',
    icon: 'ri:cpu-line',
    roles: ['R_SUPER', 'R_ADMIN', 'R_USER']
  },
  children: [
    {
      path: 'dashboard',
      name: 'MonitorDashboard',
      component: '/device_monitor/dashboard/index',
      meta: {
        title: 'menus.deviceMonitor.dashboard',
        icon: 'ri:dashboard-line',
        keepAlive: true,
        fixedTab: true,
        roles: ['R_SUPER', 'R_ADMIN', 'R_USER']
      }
    },
    {
      path: 'monitor',
      name: 'DeviceMonitorCard',
      component: '/device_monitor/monitor/index',
      meta: {
        title: 'menus.deviceMonitor.monitor',
        icon: 'ri:dashboard-3-line',
        keepAlive: true,
        roles: ['R_SUPER', 'R_ADMIN', 'R_USER']
      }
    },
    {
      path: 'driver',
      name: 'DriverManage',
      component: '/device_monitor/driver/index',
      meta: {
        title: 'menus.deviceMonitor.driver',
        icon: 'ri:plug-line',
        keepAlive: true,
        roles: ['R_SUPER', 'R_ADMIN']
      }
    },
    {
      path: 'device',
      name: 'DeviceManage',
      component: '/device_monitor/device/index',
      meta: {
        title: 'menus.deviceMonitor.device',
        icon: 'ri:server-line',
        keepAlive: true,
        roles: ['R_SUPER', 'R_ADMIN']
      }
    },
    {
      path: 'point',
      name: 'PointManage',
      component: '/device_monitor/point/index',
      meta: {
        title: 'menus.deviceMonitor.point',
        icon: 'ri:flashlight-line',
        keepAlive: true,
        roles: ['R_SUPER', 'R_ADMIN']
      }
    },
    {
      path: 'realtime',
      name: 'RealtimeMonitor',
      component: '/device_monitor/realtime/index',
      meta: {
        title: 'menus.deviceMonitor.realtime',
        icon: 'ri:pulse-line',
        keepAlive: false,
        roles: ['R_SUPER', 'R_ADMIN', 'R_USER']
      }
    },
    {
      path: 'history',
      name: 'HistoryData',
      component: '/device_monitor/history/index',
      meta: {
        title: 'menus.deviceMonitor.history',
        icon: 'ri:line-chart-line',
        keepAlive: false,
        roles: ['R_SUPER', 'R_ADMIN', 'R_USER']
      }
    },
    {
      path: 'log',
      name: 'SystemLog',
      component: '/device_monitor/log/index',
      meta: {
        title: 'menus.deviceMonitor.log',
        icon: 'ri:file-list-3-line',
        keepAlive: true,
        roles: ['R_SUPER', 'R_ADMIN', 'R_USER']
      }
    },
    {
      path: 'log-terminal',
      name: 'LogTerminal',
      component: '/device_monitor/log-terminal/index',
      meta: {
        title: 'menus.deviceMonitor.logTerminal',
        icon: 'ri:terminal-line',
        keepAlive: false,
        roles: ['R_SUPER', 'R_ADMIN', 'R_USER']
      }
    }
  ]
}
