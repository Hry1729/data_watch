# Desktop Navigation Shell Design

## Goal

Replace the desktop app's three-item top navigation with a shadcn-admin-inspired application shell while preserving the login flow, Tauri title bar, settings behavior, and the internals of the existing console, collector, and writer pages.

## Scope

The first phase delivers the shell and route structure only:

- A light gray left sidebar with grouped navigation, collapsible width, active states, and a bottom user area.
- A top application bar with sidebar toggle, current page title, connection status, user avatar, settings shortcut, and theme toggle.
- Working routes for the current console, collector, writer, and settings pages.
- Placeholder pages for every future ArtDesignPro navigation entry.
- Compatibility redirects for the existing `/`, `/collector`, and `/writer` URLs.

The login page, bootstrap page, error page, Tauri title bar, backend services, and existing page internals are out of scope.

## Navigation

| Group | Child | Route | Phase 1 content |
| --- | --- | --- | --- |
| 仪表盘 | 工作台 | `/dashboard/workbench` | Placeholder |
| 仪表盘 | 控制台 | `/dashboard/console` | Existing `OverviewPage` |
| 数据采集 | 采集监控 | `/collection/collector` | Existing `CollectorPage` |
| 数据采集 | 写入监控 | `/collection/writer` | Existing `WriterPage` |
| 设备监控 | 设备通讯 | `/device/communication` | Placeholder |
| 设备监控 | 驱动管理 | `/device/drivers` | Placeholder |
| 设备监控 | 设备管理 | `/device/devices` | Placeholder |
| 设备监控 | 点位管理 | `/device/points` | Placeholder |
| 设备监控 | 实时监控 | `/device/realtime` | Placeholder |
| 设备监控 | 历史数据 | `/device/history` | Placeholder |
| 日志 | 系统日志 | `/logs/system` | Placeholder |
| 日志 | 日志监控 | `/logs/monitor` | Placeholder |
| 系统管理 | 用户管理 | `/system/users` | Placeholder |
| 系统管理 | 角色管理 | `/system/roles` | Placeholder |
| 系统管理 | 菜单管理 | `/system/menus` | Placeholder |

Settings remains a separate `/settings` route. It is available from the top bar gear button and the bottom user menu, not from the `系统管理` group.

## Layout Behavior

- Expanded sidebar width: 240 px. Collapsed sidebar width: 64 px.
- The top-left button uses the familiar panel-open/panel-close icon and toggles sidebar width.
- The collapsed state is persisted in `localStorage` under `data-watch-sidebar-collapsed`.
- Navigation groups start expanded. In collapsed mode only group icons remain; clicking a group icon expands the sidebar and opens that group.
- The sidebar footer contains a user button. It opens a compact menu with `设置` and `退出登录`.
- The current authentication store has tokens only and no profile fields, so phase 1 displays neutral local-user copy instead of inventing account data.
- The main content remains independently scrollable and existing pages keep their own spacing and maximum widths.

## Visual Direction

Use the existing shadcn/Tailwind tokens and Lucide icons. The sidebar is quiet light gray, the content area uses the existing background, borders are restrained, and active items use the existing brand blue as a small accent. Dark mode continues to use the existing theme tokens. No ArtDesignPro theme panel or top multi-tab system is added.

## Placeholder Behavior

Each future route renders one shared placeholder component showing the page title and a concise `页面待复刻` state. It contains no fake charts, tables, or controls.

## Verification

- A navigation-model test asserts the exact group labels, child labels, routes, existing-page mappings, and placeholder mappings.
- TypeScript and Vite production build must complete successfully.
- Browser inspection checks expanded and collapsed sidebars, active navigation, settings access, user menu, all three existing page routes, and a placeholder route at desktop and narrow widths.

