# Workbench Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Workbench placeholder with the authenticated operational dashboard from the legacy Vue frontend.

**Architecture:** Extend the existing backend client and mock with typed Dashboard methods, centralize refresh/error state in `useDashboardData`, and keep `WorkbenchPage` focused on shadcn composition and chart mapping.

**Tech Stack:** React 19, TypeScript, React Router 7, shadcn/ui preset `b1a1cafyq`, Recharts through shadcn Chart, Node test runner.

## Global Constraints

- Do not modify backend or Agent code.
- Use existing backend routes and authentication token handling.
- Prefer shadcn components; no page-specific component framework.
- Preserve current desktop shell and control console.

---

### Task 1: Dashboard Contract

**Files:** `app/src/types/index.ts`, `app/src/lib/backend-client.ts`, `app/src/lib/mock-agent.ts`, `app/tests/dashboard-client.test.mjs`

- [ ] Add failing tests for the five authenticated Dashboard paths and mock methods.
- [ ] Add exact Dashboard response types.
- [ ] Implement real and mock client methods with query serialization.
- [ ] Run the targeted contract test and confirm it passes.

### Task 2: shadcn Components

**Files:** `app/src/components/ui/chart.tsx`, `app/src/components/ui/alert.tsx`

- [ ] Preview the local shadcn generator changes.
- [ ] Add Chart and Alert using the current preset configuration without overwriting existing components.
- [ ] Run TypeScript to confirm generated components match installed dependencies.

### Task 3: Dashboard State Hook

**Files:** `app/src/hooks/useDashboardData.ts`, `app/tests/workbench-data.test.mjs`

- [ ] Add a failing source/contract test for initial load, refresh interval, range switching, and unauthorized logout.
- [ ] Implement independent panel states and 30-second refresh cleanup.
- [ ] Confirm the targeted test passes.

### Task 4: Workbench UI and Route

**Files:** `app/src/pages/WorkbenchPage.tsx`, `app/src/App.tsx`, `app/src/config/navigation.ts`, `app/tests/workbench-page.test.mjs`, `app/tests/navigation.test.mjs`

- [ ] Add failing tests for the existing route and seven required UI regions.
- [ ] Compose the page from shadcn Alert, Card, Chart, Table, Badge, Button, and Skeleton.
- [ ] Wire range switching, manual refresh, alert dismissal, and page links.
- [ ] Change Workbench navigation kind to `existing` and remove it from placeholders.
- [ ] Confirm targeted and full tests pass.

### Task 5: Verification

**Files:** verification only

- [ ] Run `pnpm test`.
- [ ] Run `pnpm exec tsc --noEmit`.
- [ ] Run `pnpm build`.
- [ ] Verify desktop and narrow screenshots in mock development mode.
- [ ] Confirm real development mode remains connected to the installed Agent.
