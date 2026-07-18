# Workbench And Global Refresh Design

## Scope

Optimize the existing desktop Workbench page and add one shared current-page refresh action to the application header. This specification supersedes the conflicting Workbench layout details in `2026-07-17-workbench-page-design.md`.

The work is frontend-only. Existing authenticated backend routes and response contracts remain unchanged. Packaging the desktop application is out of scope; verification uses the development environment.

## Global Refresh

### Interaction

- Add an icon-only refresh button in the top header immediately to the left of the user avatar.
- Use the Lucide `RefreshCw` icon with the tooltip and accessible label `刷新当前页面`.
- The action is available on every application page that uses `AppLayout`, including settings pages.
- Clicking it requests fresh data for the currently displayed page. It does not reload the browser window, remount the route, navigate, or reset authentication.
- Current filters, selected tabs, date ranges, pagination, form values, and other local page state remain unchanged.

### Architecture

- Introduce a small `PageRefreshContext` owned by the application layout. It exposes a monotonically increasing refresh version and a function that increments it.
- The header button increments the shared version.
- Shared data hooks subscribe to that version and repeat their existing request or reconnect behavior while retaining the arguments and state supplied by the page.
- `useBackendQuery` responds centrally so device, log, and system-management pages that use it gain refresh support without page-specific button wiring.
- Dashboard, module-monitoring, connection-health, and WebSocket hooks respond directly because they do not all use `useBackendQuery`.
- Pages with an imperative query, such as history-data results, observe the version and repeat only the last valid query with its current parameters.
- Settings reloads the active settings section's source data without changing the selected settings tab or unsaved editable values. A refresh must not silently overwrite dirty form state; dirty forms retain their values until saved, reset, or revisited.

This is deliberately a data-refresh signal rather than a route key. It preserves page state by construction and keeps each hook responsible for its existing request lifecycle, error handling, and cancellation.

## Workbench Layout

The page uses the existing content width and scroll container. The welcome banner and page-local refresh button are removed.

1. Four statistic cards appear in one row on standard desktop widths: device total, online rate, collection points, and 24-hour system alerts.
2. Device type distribution and device communication overview appear side by side in the next row at equal width.
3. Collection/alarm trend occupies one full-width row.
4. Recent system alarms occupies one full-width row.

At narrower widths, the four statistics may become two columns and then one column, while each two-panel row stacks vertically. No panel may create horizontal overflow.

## Charts

### Device Type Distribution

- Use the shadcn `ChartContainer` and Recharts stacked bar pattern matching `Bar Chart - Stacked + Legend`.
- Keep device types as categories and show their status series as stacked values.
- Render a visible legend through the shadcn chart legend wrapper.
- Tooltips show the device type, series label, and exact count.

### Device Communication Overview

- Use the shadcn pie-chart pattern matching `Pie Chart - Legend`.
- Display the existing connection-health categories and counts with a visible legend.
- Keep the total device count readable in or adjacent to the chart without adding another card.
- Tooltips show the status label, count, and proportion.

### Collection And Alarm Trend

- Use the shadcn interactive area-chart structure from the supplied reference, adapted to the existing dashboard data contract.
- Collection volume and alarm count are the two interactive series.
- Use separate left and right Y axes because the two measures can differ substantially in scale.
- Place the time-range select in the card header at the upper right, styled like the reference example.
- The available ranges are `今日`, `近24小时`, and `近7天`; switching range reuses the existing trend request and changes no other page state.
- Preserve the existing automatic 30-second refresh and ensure it requests the currently selected range.
- The chart legend and tooltip use Chinese labels and exact values. Color choices use existing theme chart tokens and remain legible in light and dark themes.

## Recent System Alarms

Keep the current list/table behavior and existing `查看更多` navigation. Only move it into a full-width row. Loading, empty, and request-error states remain visible inside the panel and do not shift the page layout.

## Data And Error Behavior

- Continue using the existing dashboard endpoints and independent panel loading/error states.
- A global refresh repeats every request needed by the current page and leaves unrelated routes untouched.
- Existing automatic polling remains active; a manual refresh is additive and does not create a second polling interval.
- Concurrent refreshes use the hooks' current cancellation or stale-response protection so an older response cannot replace newer data.
- Authentication failures continue through the existing logout/session-expiry path.

## Component Policy

Use shadcn components from preset `b1a1cafyq`, including `Button`, `Card`, `Chart`, `Select`, `Skeleton`, and `Table`, plus Lucide icons. Recharts remains accessed through the shadcn chart wrappers. No bklit-ui component or new runtime dependency is needed for this work.

## Verification

- Unit tests prove the header refresh increments the shared refresh version without changing the route.
- Hook tests prove a refresh repeats the current request while retaining filter/range parameters.
- Workbench structure tests prove the banner and local refresh button are absent and the requested panel order is present.
- Chart tests cover the selected trend range, both trend series, stacked device-type series, pie legend, and visible loading/error/empty states.
- Existing frontend tests, TypeScript checks, and Vite production build pass.
- Development-mode screenshots verify the standard desktop and narrow layouts, including one-row statistics at standard width, aligned side-by-side overview charts, full-width lower panels, readable legends, and no unnecessary page overflow.
