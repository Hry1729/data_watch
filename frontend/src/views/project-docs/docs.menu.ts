/**
 * 「项目文档」菜单子树构建
 *
 * 由前端注入到后端下发的菜单列表中（见 router/core/MenuProcessor）。
 * 结构遵循 art-design-pro 约定：
 * - 一级菜单 component 指向 Layout(/index/index)
 * - 中间目录不配 component（留空 → 仅作路径分组，子页在 Layout 的 router-view 渲染）
 * - 叶子分类页统一复用 /project-docs/category 组件，靠路由末段区分分类
 * - 阅读器为隐藏路由（isHide），带 :slug 参数，由卡片点击进入
 */
import type { AppRouteRecord } from '@/types/router'
import { docSections } from './docs.config'

/** 叶子分类 → 菜单项 */
function categoryToMenu(key: string, title: string, icon: string): AppRouteRecord {
  return {
    name: `Docs_${key}`,
    path: key,
    component: '/project-docs/category',
    meta: {
      title,
      icon,
      keepAlive: true
    }
  }
}

/** 构建「项目文档」一级菜单子树 */
export function buildProjectDocsMenu(): AppRouteRecord {
  const children: AppRouteRecord[] = [
    {
      name: 'DocsOverview',
      path: 'overview',
      component: '/project-docs/overview',
      meta: {
        title: '总览',
        icon: 'ri:apps-2-line',
        keepAlive: true
      }
    }
  ]

  for (const section of docSections) {
    if (section.type === 'category') {
      children.push(categoryToMenu(section.key, section.title, section.icon))
    } else {
      children.push({
        name: `DocsGroup_${section.key}`,
        path: section.key,
        // 中间目录：不配 component
        meta: {
          title: section.title,
          icon: section.icon
        },
        children: section.children.map((c) => categoryToMenu(c.key, c.title, c.icon))
      })
    }
  }

  // 隐藏的文档阅读器路由（不在侧边栏显示，由卡片点击跳转）
  children.push({
    name: 'DocsReader',
    path: 'read/:slug',
    component: '/project-docs/read',
    meta: {
      title: '文档阅读',
      icon: 'ri:book-open-line',
      isHide: true,
      isHideTab: true
    }
  })

  return {
    name: 'ProjectDocs',
    path: '/project-docs',
    component: '/index/index',
    meta: {
      title: '项目文档',
      icon: 'ri:book-2-line'
    },
    children
  }
}
