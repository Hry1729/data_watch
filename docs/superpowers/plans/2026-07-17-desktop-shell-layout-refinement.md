# Desktop Shell Layout Refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refine the desktop shell so settings navigation lives in the top bar, monitoring pages fit the viewport, and the sidebar uses the product icon with Settings inside the user menu.

**Architecture:** Share settings-tab metadata and query normalization through a small pure module used by both `AppLayout` and `SettingsPage`. Replace fixed monitoring heights with nested flex/grid constraints so only data-heavy child regions scroll.

**Tech Stack:** React 19, React Router 7, TypeScript, Tailwind CSS 4, Radix Tabs, Node test runner, Tauri 2.

## Global Constraints

- Keep the login page and Tauri title bar unchanged.
- Do not modify backend or Agent behavior.
- Preserve the existing top-right avatar, Settings, theme, and connection controls.
- Default settings section is `database`.
- Use the existing Tauri application icon; do not add a duplicate binary asset.

---

### Task 1: Settings Tab Contract

**Files:**
- Create: `app/src/components/layout/settings-tabs.ts`
- Create: `app/tests/settings-tabs.test.mjs`

**Interfaces:**
- Produces: `SETTINGS_TABS`, `SettingsTabValue`, and `normalizeSettingsTab(value)`.
- `normalizeSettingsTab` returns `database` for missing or invalid values.

- [ ] Write a failing Node test that imports the module source contract and checks the four values and database fallback.
- [ ] Run `pnpm test` and verify failure because the module does not exist.
- [ ] Implement the four-item constant and normalization function.
- [ ] Run `pnpm test` and verify the new tests pass.

### Task 2: Shell Chrome and User Menu

**Files:**
- Modify: `app/src/components/AppLayout.tsx`
- Modify: `app/tests/navigation.test.mjs`

**Interfaces:**
- Consumes: `SETTINGS_TABS` and `normalizeSettingsTab`.
- Produces: query-controlled settings top bar and user-popover Settings action.

- [ ] Add failing assertions for the Tauri icon import, the absence of a standalone footer Settings link, and the presence of the popover Settings action.
- [ ] Run `pnpm test` and verify the expected assertions fail.
- [ ] Replace `Waves` with the imported application icon.
- [ ] On `/settings`, render Back and Settings at the left and the four-section switcher in the header center.
- [ ] Update the query string on section selection while retaining `/settings`.
- [ ] Remove the footer Settings `NavLink` and add a Settings button above Logout in the user popover.
- [ ] Run `pnpm test` and verify the shell regressions pass.

### Task 3: Controlled Settings Content

**Files:**
- Modify: `app/src/pages/SettingsPage.tsx`
- Create: `app/tests/settings-page-layout.test.mjs`

**Interfaces:**
- Consumes: `normalizeSettingsTab(searchParams.get("tab"))`.
- Produces: controlled Tabs content with no local TabsList or page header.

- [ ] Add failing assertions that the page uses query-controlled Tabs and no longer renders its local return row or TabsList.
- [ ] Run `pnpm test` and verify failure against the current page.
- [ ] Remove the local return/header markup and TabsList imports.
- [ ] Read `tab` with `useSearchParams`, normalize it, and pass it as the controlled Tabs value.
- [ ] Keep the existing Escape-to-return behavior and all form/save behavior unchanged.
- [ ] Run `pnpm test` and verify the settings-page regressions pass.

### Task 4: Monitoring Viewport Layout

**Files:**
- Modify: `app/src/pages/CollectorPage.tsx`
- Modify: `app/src/pages/WriterPage.tsx`
- Modify: `app/src/components/TrendChart.tsx`
- Create: `app/tests/monitoring-layout.test.mjs`

**Interfaces:**
- `TrendChartProps` gains optional `fillHeight?: boolean`.
- `fillHeight` makes the card and chart content consume the parent height without changing Overview-page behavior.

- [ ] Add failing assertions that Collector and Writer no longer contain fixed `595`/`560` heights and use `h-full min-h-0 overflow-hidden` page constraints.
- [ ] Run `pnpm test` and verify the fixed-height assertions fail.
- [ ] Convert each page root to a full-height flex column and the lower monitoring region to `min-h-0 flex-1`.
- [ ] Use a two-row grid on the left: fixed task table plus flexible trend chart.
- [ ] Make the right log tabs fill the full lower region.
- [ ] Add `fillHeight` support to `TrendChart` and use it only on Collector and Writer pages.
- [ ] Run `pnpm test` and verify the monitoring regressions pass.

### Task 5: Full Verification

**Files:**
- Verify only; no planned production edits.

- [ ] Run `pnpm test`; expect all tests to pass.
- [ ] Run `pnpm exec tsc --noEmit`; expect exit code 0.
- [ ] Run `pnpm build`; expect the Vite production build to succeed.
- [ ] Start Tauri development mode with the built-in mock Agent for visual inspection only.
- [ ] Verify at desktop and narrower widths: no empty page scroll, centered settings tabs do not overlap, user-menu Settings is above Logout, and the application icon renders in expanded/collapsed sidebar states.

