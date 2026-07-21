# 工作台与全局刷新 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在不修改后端接口、不重置页面筛选状态的前提下，为全部已实现页面增加顶部当前页刷新，并按确认稿重排工作台与三类图表。

**Architecture:** 受保护路由外层挂载 `PageRefreshProvider`，顶部按钮只递增刷新版本；公共数据 Hook 把该版本加入现有 effect 依赖，保留页面传入的查询参数。工作台继续使用现有 dashboard API 与 shadcn Chart 包装器，只调整呈现结构和交互样式。

**Tech Stack:** React 19、TypeScript、React Router 7、shadcn UI preset `b1a1cafyq`、Recharts 3、Node test runner。

## Global Constraints

- 仅修改 `app` 前端，不修改 Agent、backend、collector 或 writer。
- 不新增运行时依赖，不打包 Tauri 安装包。
- 顶部刷新不得调用 `window.location.reload()`，不得改变当前路由、筛选、分页、选项卡或已输入表单。
- 自动轮询继续生效，手动刷新不得新增第二个定时器。
- 现有未提交文件必须原样保留，不做无关格式化或重构。

---

### Task 1: 全局刷新信号与顶部按钮

**Files:**
- Create: `app/src/contexts/PageRefreshContext.tsx`
- Modify: `app/src/App.tsx`
- Modify: `app/src/components/AppLayout.tsx`
- Create: `app/tests/page-refresh.test.mjs`

**Interfaces:**
- Produces: `PageRefreshProvider({ children })`
- Produces: `usePageRefresh(): { refreshVersion: number; refreshCurrentPage: () => void }`

- [ ] **Step 1: 写失败测试**

```js
test("protected application shell provides a current-page refresh action", () => {
  assert.match(appSource, /<PageRefreshProvider>/);
  assert.match(layoutSource, /refreshCurrentPage/);
  assert.match(layoutSource, /aria-label="刷新当前页面"/);
  assert.match(layoutSource, /<RefreshCw/);
  assert.doesNotMatch(layoutSource, /window\.location\.reload/);
});
```

- [ ] **Step 2: 运行测试并确认因 Provider/按钮缺失而失败**

Run: `pnpm test -- page-refresh.test.mjs`
Expected: FAIL，错误包含 `PageRefreshProvider` 或 `刷新当前页面` 未匹配。

- [ ] **Step 3: 实现最小上下文与按钮**

```tsx
const PageRefreshContext = createContext<PageRefreshValue | null>(null);

export function PageRefreshProvider({ children }: PropsWithChildren) {
  const [refreshVersion, setRefreshVersion] = useState(0);
  const refreshCurrentPage = useCallback(() => {
    setRefreshVersion((version) => version + 1);
  }, []);
  return (
    <PageRefreshContext.Provider value={{ refreshVersion, refreshCurrentPage }}>
      {children}
    </PageRefreshContext.Provider>
  );
}
```

将 `PageRefreshProvider` 包在 `AppLayout` 外层；`AppLayout` 调用 `refreshCurrentPage`，在头像左侧渲染 icon-only `Button`。

- [ ] **Step 4: 运行测试确认通过**

Run: `pnpm test -- page-refresh.test.mjs`
Expected: PASS。

### Task 2: 公共数据 Hook 响应当前页刷新

**Files:**
- Modify: `app/src/hooks/useBackendQuery.ts`
- Modify: `app/src/hooks/useDashboardData.ts`
- Modify: `app/src/hooks/useConnectionHealth.ts`
- Modify: `app/src/hooks/useModuleStatus.ts`
- Modify: `app/src/hooks/useModuleMetrics.ts`
- Modify: `app/src/hooks/useModuleRates.ts`
- Modify: `app/src/hooks/useModuleDbLogs.ts`
- Modify: `app/src/hooks/useRealtimeWebSocket.ts`
- Modify: `app/src/hooks/useWebSocketLog.ts`
- Modify: `app/tests/page-refresh.test.mjs`

**Interfaces:**
- Consumes: `usePageRefresh().refreshVersion`
- Produces: 各 Hook 在版本变化时重复当前请求或重建当前订阅。

- [ ] **Step 1: 扩展失败测试，要求公共 Hook 订阅刷新版本**

```js
for (const file of sharedHookFiles) {
  const source = await readFile(file, "utf8");
  assert.match(source, /usePageRefresh/);
  assert.match(source, /refreshVersion/);
}
```

- [ ] **Step 2: 运行测试并确认因 Hook 未接入而失败**

Run: `pnpm test -- page-refresh.test.mjs`
Expected: FAIL，首个未接入 Hook 缺少 `usePageRefresh`。

- [ ] **Step 3: 在现有 effect 中加入刷新版本**

```tsx
const { refreshVersion } = usePageRefresh();

useEffect(() => {
  // 保留现有 fetch、清理和定时器逻辑
}, [existingDependencies, refreshVersion]);
```

`useDashboardData` 删除自己的 `refreshVersion` state，返回的 `refresh` 直接使用 `refreshCurrentPage`。WebSocket Hook 在版本变化时执行现有 cleanup 后按当前设备/模块参数重连，不清空用户选择。

- [ ] **Step 4: 运行刷新测试与现有 Hook 测试**

Run: `pnpm test -- page-refresh.test.mjs workbench-data.test.mjs monitoring-layout.test.mjs realtime-monitor.test.mjs`
Expected: PASS。

### Task 3: 非公共查询页面保留状态刷新

**Files:**
- Modify: `app/src/pages/LogMonitorPage.tsx`
- Modify: `app/src/pages/HistoryDataPage.tsx`
- Modify: `app/src/pages/SettingsPage.tsx`
- Modify: `app/tests/page-refresh.test.mjs`

**Interfaces:**
- Consumes: `usePageRefresh().refreshVersion`
- Produces: 日志沿用当前过滤器，历史页重复最后一次有效查询，设置页不覆盖脏表单。

- [ ] **Step 1: 写三种保留状态的失败测试**

```js
assert.match(logMonitorSource, /refreshVersion/);
assert.match(historySource, /lastQueryRef/);
assert.match(settingsSource, /formDirtyRef/);
assert.match(settingsSource, /streamMaxlenDirtyRef/);
```

- [ ] **Step 2: 运行测试并确认缺少页面级刷新处理**

Run: `pnpm test -- page-refresh.test.mjs`
Expected: FAIL。

- [ ] **Step 3: 实现页面级保留规则**

日志监控把 `refreshVersion` 加入当前日志与统计 effect 依赖，继续使用当前 `filters`、`pollSeconds`、`pausedRef`。历史页在成功查询时保存查询快照，刷新版本变化时只重复该快照对应的 chart/matrix 请求。设置页使用 `formDirtyRef` 与 `streamMaxlenDirtyRef`：未编辑时采用新配置，已编辑时仍发请求但不覆盖本地值；保存成功后清除对应 dirty 标记。

- [ ] **Step 4: 运行相关测试**

Run: `pnpm test -- page-refresh.test.mjs settings-page-layout.test.mjs settings-tabs.test.mjs history-device-fidelity.test.mjs log-pages.test.mjs`
Expected: PASS。

### Task 4: 工作台布局与 shadcn 图表

**Files:**
- Modify: `app/src/pages/WorkbenchPage.tsx`
- Modify: `app/tests/workbench-page.test.mjs`

**Interfaces:**
- Consumes: `useDashboardData()` 的现有 overview、trend、distribution、health、events 与 `trendRange`。
- Produces: 四卡一行、双图一行、趋势整行、告警整行的工作台。

- [ ] **Step 1: 写工作台结构失败测试**

```js
assert.doesNotMatch(source, /dashboard-welcome-closed|刷新数据|<Alert/);
assert.match(source, /lg:grid-cols-4/);
assert.match(source, /data-dashboard-row="overview-charts"/);
assert.match(source, /<AreaChart/);
assert.match(source, /<Select[\s\S]*dashboard\.trendRange/);
assert.match(source, /<BarChart[\s\S]*stackId="devices"/);
assert.match(source, /<PieChart[\s\S]*<ChartLegend/);
```

- [ ] **Step 2: 运行工作台测试并确认旧布局导致失败**

Run: `pnpm test -- workbench-page.test.mjs`
Expected: FAIL，欢迎条、旧图表或布局断言至少一项失败。

- [ ] **Step 3: 删除旧头部并重排面板**

删除欢迎条 state/localStorage、页内刷新按钮和对应 imports。统计区使用 `grid-cols-1 sm:grid-cols-2 lg:grid-cols-4`。设备类型分布放左侧，设备通讯概览放右侧，父级使用 `lg:grid-cols-2`；趋势和最近报警各自独立全宽 Card。

- [ ] **Step 4: 替换图表结构**

趋势使用 `AreaChart`、两条独立 Area、左右 YAxis、渐变填充、`ChartLegend`、`ChartTooltip`，右上角使用 shadcn `Select` 显示 `今日/近24小时/近7天`。设备分布保留 stacked Bar + legend；通讯概览使用 Pie + legend，并在中心展示设备总数。

- [ ] **Step 5: 运行工作台测试**

Run: `pnpm test -- workbench-page.test.mjs workbench-data.test.mjs dashboard-client.test.mjs`
Expected: PASS。

### Task 5: 全量验证与开发模式视觉检查

**Files:**
- Verify only: `app`

- [ ] **Step 1: 运行全部前端测试**

Run: `pnpm test`
Expected: 所有测试 PASS，无未处理异常。

- [ ] **Step 2: 运行 TypeScript 与 Vite 构建**

Run: `pnpm build`
Expected: `tsc` 与 `vite build` 退出码均为 0。

- [ ] **Step 3: 启动或复用开发服务器**

Run: `pnpm dev -- --host localhost`
Expected: Vite 输出可访问的 localhost URL，不执行 Tauri 打包。

- [ ] **Step 4: 浏览器检查标准桌面与窄屏**

在已登录开发环境检查 1440x900 与 1024x768：顶部刷新位于头像左侧；标准宽度四卡单行；两张概览图同排；趋势和告警各占整行；图例与文字无重叠；无无效横向或纵向溢出。
