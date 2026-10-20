// 审查批次 6 回归：B020 / B055 / B057 / B058 / B059 / B061 / B062 / B064 / B065 / B076
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";
import { deferred, hookHarness, settle } from "./helpers/hook-harness.mjs";
import { validateBackendOrigin } from "../src/lib/backend-proxy-policy.ts";

const read = async (path) => (await readFile(new URL(path, import.meta.url), "utf8")).replaceAll("\r\n", "\n");

function slice(source, start, end) {
  const from = source.indexOf(start);
  const to = source.indexOf(end, from + 1);
  assert.ok(from >= 0, `missing ${start}`);
  assert.ok(to > from, `missing ${end}`);
  return source.slice(from, to);
}

const compile = (code) => ts.transpileModule(code, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
}).outputText;

// ─── B020：授权弹窗乱序响应 ──────────────────────────────────────────────────

const rolePage = await read("../src/pages/RoleManagementPage.tsx");
const grantCode = compile(
  slice(rolePage, "  async function openGrant(", "  function toggleCollapsed(")
  + "\nObject.assign(globalThis, { openGrant, saveGrant });",
);

test("a late grant response for role A never lands on role B or gets saved as B", async () => {
  const responses = new Map();
  const saved = [];
  const context = {
    token: "t",
    grantSeq: { current: 0 },
    grantRole: null, grantFor: null, grantLoading: false,
    grant: { menuIds: [], permissionIds: [], dataScopes: [] },
    backendClient: {
      roleGrant: (_token, roleId) => { const d = deferred(); responses.set(roleId, d); return d.promise; },
      updateRoleGrant: async (_token, roleId, grant) => { saved.push({ roleId, grant }); },
    },
    toast: { error() {}, success() {} },
  };
  // setter 立即生效，相当于每次 setState 后立刻重新渲染
  for (const key of ["grantRole", "grantFor", "grantLoading", "grant"]) {
    context[`set${key[0].toUpperCase()}${key.slice(1)}`] = (value) => { context[key] = value; };
  }
  runInNewContext(grantCode, context);

  const openA = context.openGrant({ roleId: 1, roleName: "A" });
  const openB = context.openGrant({ roleId: 2, roleName: "B" });
  await context.saveGrant(); // 加载未完成时不得保存
  assert.equal(saved.length, 0);
  responses.get(2).resolve({ menuIds: [20], permissionIds: [202], dataScopes: [] });
  await openB;
  responses.get(1).resolve({ menuIds: [10], permissionIds: [101], dataScopes: [] });
  await openA;
  assert.equal(context.grantRole.roleId, 2);
  assert.deepEqual(context.grant.permissionIds, [202], "旧角色 A 的响应不得覆盖 B 的授权");
  assert.equal(context.grantLoading, false);
  await context.saveGrant();
  assert.deepEqual(saved, [{ roleId: 2, grant: { menuIds: [20], permissionIds: [202], dataScopes: [] } }]);
});

// ─── B055：实时断线后不再把旧值记作新采样 ────────────────────────────────────

const realtimePage = await read("../src/pages/RealtimeMonitorPage.tsx");
const samplingCode = compile(
  slice(realtimePage, "function chartValue(", "function formatSyncTime(")
  + "\nglobalThis.sample = () => {\n"
  + slice(realtimePage, "  useEffect(() => {\n    if (!running || realtimeSocket.sampleVersion === 0) return;", "  useEffect(() => {\n    localStorage")
  + "\n};",
);

test("realtime sampling writes one gap marker while disconnected and resumes after reconnect", () => {
  const context = {
    running: true,
    realtimeSocket: { sampleVersion: 0, status: "connected" },
    selectedPointItemsRef: { current: [{ varSeq: 1, value: 42 }] },
    samples: [],
    formatSampleTime: (value) => String(value),
    REALTIME_WINDOW_MS: 10 * 60_000, MAX_REALTIME_SAMPLES: 5000,
    useEffect: (fn) => fn(),
  };
  context.setSamples = (next) => { context.samples = typeof next === "function" ? next(context.samples) : next; };
  runInNewContext(samplingCode, context);
  const tick = (status) => { context.realtimeSocket = { sampleVersion: context.realtimeSocket.sampleVersion + 1, status }; context.sample(); };

  tick("connected");
  assert.deepEqual([...context.samples].map((row) => row.v1), [42]);
  tick("disconnected"); tick("disconnected"); tick("connecting");
  assert.deepEqual([...context.samples].map((row) => row.v1), [42, null], "断线期间不得新增有效读数，只留一个缺测空洞");
  tick("connected");
  assert.deepEqual([...context.samples].map((row) => row.v1), [42, null, 42]);
});

// ─── B056 见 startup-loading.test.mjs；B057：Rust IPv6 origin ─────────────────

test("rust origin normalisation serialises the url origin instead of re-bracketing IPv6 hosts", async () => {
  const proxy = await read("../src-tauri/src/http_proxy.rs");
  const fn = slice(proxy, "fn normalize_backend_origin(", "fn validate_backend_path(");
  assert.match(fn, /parsed\.origin\(\)\.ascii_serialization\(\)/);
  assert.doesNotMatch(fn, /format!\("\[\{\}\]"/);
  assert.match(proxy, /normalize_backend_origin\("http:\/\/\[::1\]:8000"\)\.unwrap\(\), "http:\/\/\[::1\]:8000"/);
});

// ─── B058：导出与列表共用筛选条件 ────────────────────────────────────────────

test("point export reuses the list filter including device type and connection status", async () => {
  const page = await read("../src/pages/PointManagementPage.tsx");
  const filter = slice(page, "  const pointFilter = useMemo(", "  const loadPoints =");
  for (const key of ["keyword", "deviceType", "connectStatus", "deviceId", "deviceCode", "dataType"]) {
    assert.match(filter, new RegExp(`${key}:`), key);
  }
  assert.match(page, /backendClient\.pointList\(token, \{\s*pageNo: page,\s*pageSize,\s*\.\.\.pointFilter,/);
  assert.match(page, /backendClient\.exportPoints\(auth\.backendToken, \{ \.\.\.pointFilter \}, template\)/);
});

// ─── B059：未接入的设置项不得假装可编辑 ──────────────────────────────────────

test("unsupported settings controls are disabled and labelled instead of silently ignored on save", async () => {
  const settings = await read("../src/pages/SettingsPage.tsx");
  const placeholders = settings.match(/<Input[^>]*defaultValue=/g) ?? [];
  assert.equal(placeholders.length, 10);
  for (const input of placeholders) assert.match(input, /<Input disabled defaultValue=/);
  assert.equal(settings.match(/（暂不支持）<\/Label>/g)?.length, 10);
});

// ─── B061：健康检查失败后不再显示上次的"正常" ───────────────────────────────

test("connection health records the last success time and keeps the error after it", async () => {
  const calls = [];
  const h = hookHarness("hooks/useConnectionHealth.ts", "useConnectionHealth", {
    "@/lib/agent-client": { agentClient: { getConnectionHealth: () => { const d = deferred(); calls.push(d); return d.promise; } } },
  });
  calls[0].resolve({ redis: { status: "connected" }, mysql: { status: "connected" }, tdengine: { status: "connected" } });
  await settle(); await settle();
  assert.equal(typeof h.value.lastSuccessAt, "number");
  assert.equal(h.value.error, null);
  h.tick();
  calls[1].reject({ type: "agent_unreachable" });
  await settle(); await settle();
  assert.equal(h.value.error, "Agent 不可达");
  assert.notEqual(h.value.lastSuccessAt, null);
  h.unmount();
});

test("status bar shows an unknown state with the last success time when health checks fail", async () => {
  const bar = await read("../src/components/ConnectionStatusBar.tsx");
  assert.match(bar, /const \{ data, loading, error, lastSuccessAt \} = useConnectionHealth\(\);/);
  assert.match(bar, /const stale = error !== null;/);
  assert.match(bar, /stale \? STALE_STYLE : STATUS_STYLE\[health\.status\]/);
  assert.match(bar, /formatLastSuccess\(lastSuccessAt\)/);
});

// ─── B062：设备变更后刷新汇总 KPI ────────────────────────────────────────────

test("every successful device mutation also refreshes the summary KPIs", async () => {
  const page = await read("../src/pages/DeviceManagementPage.tsx");
  assert.match(page, /function refreshAfterMutation\(\) \{ devices\.refresh\(\); deviceCatalog\.refresh\(\); summary\.refresh\(\); \}/);
  assert.equal(page.match(/refreshAfterMutation\(\);/g)?.length, 5);
  assert.doesNotMatch(page, /devices\.refresh\(\); deviceCatalog\.refresh\(\);(?! summary)/);
});

// ─── B064：注册成功、finish 失败后同页可重试完成 ─────────────────────────────

const edgeBootstrap = await read("../src/pages/EdgeBootstrapPage.tsx");
const submitCode = compile(
  slice(edgeBootstrap, "  async function submit(", "  if (loading) {")
  + "\nglobalThis.submit = submit;",
);

test("edge bootstrap retries finish on the same center without registering again", async () => {
  let finishCalls = 0, registerCalls = 0;
  const toasts = [];
  const centerUrl = "http://10.0.0.5:8000";
  const context = {
    submitting: false,
    centerCheck: validateBackendOrigin(centerUrl),
    validateBackendOrigin,
    registered: false, registeredCenterUrl: "",
    form: { nodeName: "edge-1", registrationCode: "CODE" },
    allPassed: true,
    databases: { mysql: { host: "127.0.0.1", port: "3306", user: "root", password: "", database: "dw" } },
    agentClient: {
      putBootstrapConfig: async () => {},
      registerEdge: async () => { registerCalls += 1; return { nodeUuid: "uuid-1", status: "pending" }; },
      postBootstrapFinish: async () => { finishCalls += 1; if (finishCalls === 1) throw { message: "finish failed" }; },
    },
    errorMessage: (error) => error?.message ?? "error",
    toast: { error: (message) => toasts.push(message) },
    setBackendBaseUrl() {}, navigate: (path) => { context.navigated = path; },
    setSubmitting() {}, setNodeUuid() {}, setRegistrationStatus() {},
  };
  context.setRegistered = (value) => { context.registered = value; };
  context.setRegisteredCenterUrl = (value) => { context.registeredCenterUrl = value; };
  runInNewContext(submitCode, context);

  await context.submit({ preventDefault() {} });
  assert.deepEqual(toasts, ["finish failed"]);
  assert.equal(context.registered, true);
  assert.equal(context.registeredCenterUrl, centerUrl);
  await context.submit({ preventDefault() {} });
  assert.deepEqual(toasts, ["finish failed"], "同一中心重试不得报'不能直接修改中心地址'");
  assert.equal(registerCalls, 1);
  assert.equal(finishCalls, 2);
  assert.equal(context.navigated, "/login");
});

// ─── B065：Edge 本机快照轮询不被旧响应回滚 ───────────────────────────────────

const edgeModules = await read("../src/pages/EdgeModulePages.tsx");
const snapshotCode = compile(
  slice(edgeModules, "function useLocalSnapshot<T>(", "function formatBytes(")
  + "\nglobalThis.useLocalSnapshot = useLocalSnapshot;",
);

test("edge local snapshot polling never overlaps requests and drops responses after unmount", async () => {
  const slots = [];
  let cursor = 0;
  const cleanups = [];
  const timers = [];
  const react = {
    useState: (initial) => { const i = cursor++; slots[i] ??= { value: initial }; return [slots[i].value, (v) => { slots[i].value = v; }]; },
    useRef: (initial) => { const i = cursor++; return slots[i] ??= { current: initial }; },
    useCallback: (fn) => fn,
    useEffect: (fn) => { cleanups.push(fn()); },
  };
  const requests = [];
  const request = () => { const d = deferred(); requests.push(d); return d.promise; };
  const context = {
    ...react,
    window: { setInterval: (fn) => { timers.push(fn); return timers.length; }, clearInterval: () => { timers.length = 0; } },
  };
  runInNewContext(snapshotCode, context);
  context.useLocalSnapshot(request);
  const dataSlot = slots[0];

  assert.equal(requests.length, 1);
  timers[0]();
  assert.equal(requests.length, 1, "上一次请求未返回时不得再发起新请求");
  requests[0].resolve({ version: 1 });
  await settle();
  assert.deepEqual(dataSlot.value, { version: 1 });
  timers[0]();
  assert.equal(requests.length, 2);
  for (const cleanup of cleanups) cleanup?.();
  requests[1].resolve({ version: 0 });
  await settle();
  assert.deepEqual(dataSlot.value, { version: 1 }, "卸载后的迟到响应不得更新状态");
});

// ─── B076：菜单树全部折叠后保持折叠 ──────────────────────────────────────────

const menuPage = await read("../src/pages/MenuManagementPage.tsx");
const expandCode = compile(
  "globalThis.render = () => {\n"
  + slice(menuPage, "  // 仅首次成功加载到菜单时默认展开顶级菜单", "  function openAdd(")
  + "\n};",
);

test("collapsing the last expanded menu keeps the tree collapsed across reloads", () => {
  const ref = { current: false };
  const context = {
    useRef: () => ref,
    useEffect: (fn) => fn(),
    menus: { data: null },
    expanded: new Set(),
  };
  context.setExpanded = (value) => { context.expanded = value; };
  runInNewContext(expandCode, context);

  context.render();
  assert.equal(context.expanded.size, 0);
  context.menus = { data: [{ id: 1 }, { id: 2 }] };
  context.render();
  assert.deepEqual([...context.expanded], [1, 2]);
  context.setExpanded(new Set()); // 用户逐个折叠到空
  context.render();
  assert.equal(context.expanded.size, 0);
  context.menus = { data: [{ id: 1 }, { id: 2 }, { id: 3 }] }; // 刷新数据
  context.render();
  assert.equal(context.expanded.size, 0);
});
