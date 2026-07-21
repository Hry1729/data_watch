# Desktop Navigation Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first-phase desktop navigation shell, placeholder routes, and mappings for the three existing monitoring pages.

**Architecture:** A pure navigation model is the single source of truth for sidebar groups and page metadata. `AppLayout` consumes that model to render the collapsible sidebar and top bar, while `App.tsx` owns concrete route-to-page mappings. One shared placeholder component covers all future pages.

**Tech Stack:** React 19, React Router 7, TypeScript 5.8, Tailwind CSS 4, Lucide React, Node 24 built-in test runner, Vite 7.

## Global Constraints

- Keep the existing login, bootstrap, error, authentication, Tauri title bar, and page internals unchanged.
- Preserve all unrelated dirty-worktree changes.
- Do not add ArtDesignPro multi-tabs or its theme configuration panel.
- Settings stays at `/settings` and is reachable from the top bar and bottom user menu.
- Persist sidebar collapse state at `data-watch-sidebar-collapsed`.
- Keep legacy `/`, `/collector`, and `/writer` URLs working through redirects.

---

### Task 1: Navigation Model

**Files:**
- Create: `app/tests/navigation.test.mjs`
- Create: `app/src/config/navigation.ts`
- Modify: `app/package.json`

**Interfaces:**
- Produces: `navigationGroups`, `placeholderPages`, `getPageMeta(pathname)` and route metadata consumed by the layout and router.

- [ ] **Step 1: Write the failing model test**

Assert the exact five groups, fifteen child entries, current-page route kinds, placeholder route kinds, and settings metadata using `node:test` and `node:assert/strict`.

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm test`

Expected: FAIL because `src/config/navigation.ts` does not exist.

- [ ] **Step 3: Implement the minimal navigation model**

Create typed group and item records with Lucide icon references, a flattened placeholder list, and exact-path page lookup. Add `"test": "node --test tests/*.test.mjs"` to package scripts.

- [ ] **Step 4: Run the test to verify it passes**

Run: `pnpm test`

Expected: all navigation tests pass.

### Task 2: Shared Placeholder and Routes

**Files:**
- Create: `app/src/pages/PlaceholderPage.tsx`
- Modify: `app/src/App.tsx`

**Interfaces:**
- Consumes: `placeholderPages` from Task 1.
- Produces: concrete protected routes and compatibility redirects.

- [ ] **Step 1: Add the placeholder component**

Render the page title plus a restrained `页面待复刻` state using existing color tokens.

- [ ] **Step 2: Register protected routes**

Map `/dashboard/console`, `/collection/collector`, and `/collection/writer` to the existing pages. Map every placeholder metadata entry to `PlaceholderPage`. Keep `/settings` mapped to `SettingsPage`.

- [ ] **Step 3: Add compatibility redirects**

Redirect `/` to `/dashboard/console`, `/collector` to `/collection/collector`, and `/writer` to `/collection/writer`.

- [ ] **Step 4: Run type/build verification**

Run: `pnpm build`

Expected: TypeScript and Vite complete with exit code 0.

### Task 3: Sidebar and Top Bar Shell

**Files:**
- Modify: `app/src/components/AppLayout.tsx`

**Interfaces:**
- Consumes: `navigationGroups`, `getPageMeta`, `useAuth`, `ConnectionStatusBar`, and `ThemeToggle`.
- Produces: the protected application shell around React Router's `Outlet`.

- [ ] **Step 1: Replace the top-tab layout**

Create a 240/64 px sidebar, grouped child links, active styles, and independently scrolling main content.

- [ ] **Step 2: Add collapse and group behavior**

Persist the collapse state, default groups open, and expand the sidebar when a collapsed group icon is selected.

- [ ] **Step 3: Add top bar actions**

Render the panel toggle and current page title on the left; connection health, local-user avatar, settings, and theme controls on the right.

- [ ] **Step 4: Add the bottom user menu**

Render a local-user button that opens settings/logout actions above it and closes on navigation or outside clicks.

- [ ] **Step 5: Run tests and build**

Run: `pnpm test`

Expected: all navigation tests pass.

Run: `pnpm build`

Expected: exit code 0.

### Task 4: Visual Verification

**Files:**
- Modify only if inspection reveals a defect directly caused by Tasks 1-3.

- [ ] **Step 1: Start the Vite server**

Run: `pnpm dev -- --host 127.0.0.1`

Expected: Vite reports a local URL without changing application files.

- [ ] **Step 2: Inspect the protected shell**

Use the local browser with the app's existing development authentication path. Verify the expanded sidebar, collapsed sidebar, top actions, bottom user menu, active states, placeholders, and the three existing pages.

- [ ] **Step 3: Inspect a narrow viewport**

Confirm controls do not overlap and labels remain within their containers.

- [ ] **Step 4: Re-run final verification**

Run: `pnpm test`

Run: `pnpm build`

Expected: both commands exit 0 with no test failures or TypeScript errors.

