# Desktop Shell Layout Refinement Design

## Goal

Refine the approved desktop navigation shell so the product icon, monitoring-page sizing, settings navigation, and user menu behave as one coherent desktop layout.

## Approved Behavior

- The sidebar brand mark uses the existing Tauri application icon from `app/src-tauri/icons/128x128.png`.
- Collector and Writer monitoring pages fill the available main viewport without creating an empty page-level vertical scrollbar.
- Their table and log content areas may scroll internally when their content exceeds the available space.
- On `/settings`, the top bar contains the back action and the Settings title on the left.
- The settings section switcher is centered in the top bar and contains `基础`, `数据库`, `Collector`, and `Writer`.
- The default settings section remains `数据库`.
- The active settings section is represented by `?tab=basic|database|collector|writer`, so refresh and browser navigation preserve it.
- The standalone Settings item is removed from the bottom of the sidebar.
- Settings is added to the user popover immediately above Logout.
- The existing top-right Settings icon remains available and opens the settings page.

## Architecture

`AppLayout` owns route-level chrome. It detects the settings route, renders the back/title controls and centered settings switcher, and updates the query string when a section is selected. `SettingsPage` reads the same normalized query parameter and controls its Tabs content from that value; it no longer renders its own header or Tabs list.

A small `settings-tabs.ts` module is the shared contract for allowed tab values, labels, and default normalization. This avoids duplicating route parsing between the layout and page.

Collector and Writer pages become height-constrained flex columns. Their metric cards retain natural height, while the lower two-column monitoring region consumes the remaining height. The task table, trend chart, and log panel receive explicit `min-h-0` boundaries so overflow stays inside the intended content regions.

## Responsive Behavior

- The centered settings switcher remains visible at normal desktop widths.
- The connection status keeps its existing responsive hiding behavior, preventing collisions with the centered switcher.
- The sidebar icon remains legible in both 240 px expanded and 64 px collapsed states.
- No page-level horizontal overflow is introduced.

## Verification

- Unit tests cover settings-tab normalization and default behavior.
- Source-level layout regressions assert removal of fixed monitoring heights and the standalone sidebar Settings link.
- Existing navigation and sidebar persistence tests remain green.
- TypeScript and Vite production compilation must pass.
- Development-mode screenshots verify expanded/collapsed sidebar, both monitoring pages, settings top bar, and the user popover.

