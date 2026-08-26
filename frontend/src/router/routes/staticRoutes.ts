import { AppRouteRecordRaw } from '@/utils/router'

/**
 * 静态路由配置（不需要权限就能访问的路由）
 *
 * 属性说明：
 * isHideTab: true 表示不在标签页中显示
 *
 * 注意事项：
 * 1、path、name 不要和动态路由冲突，否则会导致路由冲突无法访问
 * 2、静态路由不管是否登录都可以访问
 */
export const staticRoutes: AppRouteRecordRaw[] = [
  // 不需要登录就能访问的路由示例
  // {
  //   path: '/welcome',
  //   name: 'WelcomeStatic',
  //   component: () => import('@views/dashboard/console/index.vue'),
  //   meta: { title: 'menus.dashboard.title' }
  // },
  {
    path: '/auth/login',
    name: 'Login',
    component: () => import('@views/auth/login/index.vue'),
    meta: { title: 'menus.login.title', isHideTab: true }
  },
  {
    path: '/auth/register',
    name: 'Register',
    component: () => import('@views/auth/register/index.vue'),
    meta: { title: 'menus.register.title', isHideTab: true }
  },
  {
    path: '/auth/forget-password',
    name: 'ForgetPassword',
    component: () => import('@views/auth/forget-password/index.vue'),
    meta: { title: 'menus.forgetPassword.title', isHideTab: true }
  },
  {
    path: '/auth/change-password',
    name: 'ForceChangePassword',
    component: () => import('@views/auth/change-password/index.vue'),
    meta: { title: '修改临时密码', isHideTab: true }
  },
  {
    path: '/403',
    name: 'Exception403',
    component: () => import('@views/exception/403/index.vue'),
    meta: { title: '403', isHideTab: true }
  },
  {
    // 个人中心：后端菜单模式下它不是菜单项，需作为常驻路由注册，否则 404。
    // 用 index/index 作为 layout 外壳，子路由渲染页面，保持 URL 为 /system/user-center。
    path: '/system/user-center',
    component: () => import('@views/index/index.vue'),
    name: 'UserCenterLayout',
    meta: { title: 'menus.system.userCenter', isHide: true, isHideTab: true },
    children: [
      {
        path: '',
        name: 'UserCenter',
        component: () => import('@views/system/user-center/index.vue'),
        meta: { title: 'menus.system.userCenter', keepAlive: true, isHideTab: true }
      }
    ]
  },
  {
    path: '/:pathMatch(.*)*',
    // 名字不能叫 Exception404：后端菜单里有同名动态子路由 /exception/404，
    // 动态注册时同名会覆盖并移除本静态 catch-all，登出后无法恢复（详见路由告警排查）。
    name: 'NotFoundCatchAll',
    component: () => import('@views/exception/404/index.vue'),
    meta: { title: '404', isHideTab: true }
  },
  {
    path: '/500',
    name: 'Exception500',
    component: () => import('@views/exception/500/index.vue'),
    meta: { title: '500', isHideTab: true }
  },
  {
    path: '/outside',
    component: () => import('@views/index/index.vue'),
    name: 'Outside',
    meta: { title: 'menus.outside.title' },
    children: [
      // iframe 内嵌页面
      {
        path: '/outside/iframe/:path',
        name: 'Iframe',
        component: () => import('@/views/outside/Iframe.vue'),
        meta: { title: 'iframe' }
      }
    ]
  }
]
