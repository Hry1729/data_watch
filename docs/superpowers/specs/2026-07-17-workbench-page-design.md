# Workbench Page Design

## Scope

Replace `/dashboard/workbench` with the real operational dashboard represented by `frontend/src/views/device_monitor/dashboard/index.vue`. The ArtDesignPro sales-demo page is explicitly out of scope.

## Data Contract

The page consumes the existing authenticated backend routes without backend changes:

- `GET /api/device-monitor/dashboard/overview`
- `GET /api/device-monitor/dashboard/trend`
- `GET /api/device-monitor/dashboard/device-type-distribution`
- `GET /api/device-monitor/dashboard/device-connection-health`
- `GET /api/device-monitor/dashboard/recent-events`

The frontend loads all panels independently, logs out on `401`, exposes per-panel error states, and refreshes every 30 seconds while mounted.

## Layout

- Compact dismissible welcome alert at the top, persisted in local storage.
- Four equal statistic cards: device total, online rate, collection points, and 24-hour system alerts.
- Main row: trend chart at approximately 60% width and connection-health ring chart at approximately 40%.
- Bottom row: device-type stacked horizontal chart at approximately 40% and recent alarms table at approximately 60%.
- At narrower desktop widths, statistics become two columns and chart rows stack without horizontal overflow.

## Component Policy

Use shadcn components from preset `b1a1cafyq`: `Alert`, `Badge`, `Button`, `Card`, `Chart`, `Skeleton`, and `Table`. Recharts is used only through the shadcn Chart wrapper. No bklit-ui or custom placeholder is required for this page.

## Navigation

- `查看设备` opens `/device/devices`.
- `查看更多` opens `/logs/system?level=WARN`.
- The navigation item changes from `placeholder` to `existing` and is removed from `placeholderPages`.

## Verification

- Contract tests cover route paths and mock parity.
- Page-structure tests cover all required cards and panels.
- Full frontend tests, TypeScript, and Vite production build pass.
- Development-mode screenshots verify desktop/narrow layouts, loading/data states, no overlap, and no horizontal overflow.

