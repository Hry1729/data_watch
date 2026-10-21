// H6 回归：配置加载失败时禁止保存；加载成功后只提交改动过的字段。
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";
import { buildConfigPatch, changedFields } from "../src/lib/config-patch.ts";

const settings = (await readFile(new URL("../src/pages/SettingsPage.tsx", import.meta.url), "utf8")).replaceAll("\r\n", "\n");
const edge = (await readFile(new URL("../src/pages/EdgeSettingsPage.tsx", import.meta.url), "utf8")).replaceAll("\r\n", "\n");

function slice(source, start, end) {
  const from = source.indexOf(start);
  const to = source.indexOf(end, from + 1);
  assert.ok(from >= 0, `missing ${start}`);
  assert.ok(to > from, `missing ${end}`);
  return source.slice(from, to);
}

function compile(code) {
  return ts.transpileModule(code, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
}

const LOADED = {
  redis:    { host: "10.1.1.1", port: "6380", user: "", password: "", database: "2" },
  mysql:    { host: "10.1.1.2", port: "3307", user: "dw", password: "", database: "prod_watch" },
  tdengine: { host: "10.1.1.3", port: "6041", user: "td", password: "", database: "prod_data" },
  collector: { executable: "collector/data-watch-collector.exe", working_dir: "E:/dw/collector" },
  ingest:    { executable: "ingest/data-watch-ingest.exe", working_dir: "E:/dw/ingest" },
  writer:    { executable: "writer/data-watch-writer.exe", working_dir: "E:/dw/writer" },
};

const DEFAULTS = {
  redis:    { host: "127.0.0.1", port: "6379", user: "", password: "", database: "0" },
  mysql:    { host: "127.0.0.1", port: "3306", user: "root", password: "", database: "device_watch" },
  tdengine: { host: "127.0.0.1", port: "6041", user: "root", password: "", database: "device_data" },
  collector: LOADED.collector,
  ingest: LOADED.ingest,
  writer: LOADED.writer,
};

const settingsSaveCode = compile(
  slice(settings, "function isValidRedisDb(", "// ─── 测试状态类型")
  + slice(settings, "  const canSave =", "  async function handleSaveStreamMaxlen(")
  + "\nglobalThis.handleSave = handleSave;",
);

function runSettingsSave({ loadedForm, configError, form, passwordDirty }) {
  const calls = { put: [], errors: [], success: [] };
  const context = {
    loadedForm, configError, form,
    passwordDirty: passwordDirty ?? { redis: false, mysql: false, tdengine: false },
    BUILD_ROLE: "centralized",
    buildConfigPatch,
    agentClient: { putConfig: async (payload) => { calls.put.push(payload); } },
    toast: { error: (message) => calls.errors.push(message), success: (message) => calls.success.push(message) },
    formDirtyRef: { current: true },
    editVersionRef: { current: { form: 0, redis: 0, mysql: 0, tdengine: 0 } },
    setSaving() {}, setLoadedForm() {}, setPasswordDirty() {}, logout() {},
  };
  runInNewContext(settingsSaveCode, context);
  return { calls, handleSave: context.handleSave };
}

test("changedFields keeps only modified keys and password only when dirty", () => {
  assert.deepEqual(changedFields({ host: "a", port: 1, password: "" }, { host: "b", port: 1, password: "x" }), { host: "b" });
  assert.deepEqual(changedFields({ host: "a", password: "" }, { host: "a", password: "x" }, true), { password: "x" });
  assert.deepEqual(buildConfigPatch({ redis: { baseline: { host: "a" }, current: { host: "a" } } }), {});
});

test("settings page refuses to save after a failed config load", async () => {
  const { calls, handleSave } = runSettingsSave({
    loadedForm: null,
    configError: "加载配置失败",
    form: DEFAULTS,
  });
  await handleSave();
  assert.equal(calls.put.length, 0, "加载失败后不得 PUT 默认值");
  assert.equal(calls.errors.length, 1);
});

test("settings page submits only the fields changed since the config was loaded", async () => {
  const form = { ...LOADED, mysql: { ...LOADED.mysql, host: "10.9.9.9" } };
  const { calls, handleSave } = runSettingsSave({ loadedForm: LOADED, configError: null, form });
  await handleSave();
  assert.deepEqual(calls.put, [{ mysql: { host: "10.9.9.9" } }]);
});

test("settings page sends a dirty password without rewriting the rest of the section", async () => {
  const form = { ...LOADED, redis: { ...LOADED.redis, password: "new-secret" } };
  const { calls, handleSave } = runSettingsSave({
    loadedForm: LOADED, configError: null, form,
    passwordDirty: { redis: true, mysql: false, tdengine: false },
  });
  await handleSave();
  assert.deepEqual(calls.put, [{ redis: { password: "new-secret" } }]);
});

test("settings save buttons are disabled when the config was not loaded", () => {
  assert.ok(settings.includes("const canSave = loadedForm !== null && configError === null;"));
  assert.ok(settings.includes("disabled={!canSave}"));
  assert.ok(settings.includes("disabled={saving || disabled} onClick={onSave}"));
  assert.match(settings, /disabled=\{saving \|\| disabled\}[\s\S]{0,120}onClick=\{\(\) => onSaveAndRestart\(name\)\}/);
  assert.doesNotMatch(settings, /用户仍可编辑保存/);
});

const edgeCode = compile(
  slice(edge, "const DB_META", "const DEFAULT_DATABASES")
  + slice(edge, "function validateDatabase(", "function testBadge(")
  + slice(edge, "  const canSaveDatabases =", "  function updateDatabase(")
  + slice(edge, "  async function saveDatabases()", "  async function saveLocal()")
  + "\nglobalThis.saveDatabases = saveDatabases;",
);

function runEdgeSave({ loadedDatabases, databaseError, databases, passwordDirty }) {
  const calls = { put: [], errors: [] };
  const context = {
    loadedDatabases, databaseError, databases,
    passwordDirty: passwordDirty ?? { redis: false, mysql: false, tdengine: false },
    buildConfigPatch,
    agentClient: { putConfig: async (payload) => { calls.put.push(payload); } },
    toast: { error: (message) => calls.errors.push(message), success() {} },
    window: { dispatchEvent() {} }, Event: class {}, CONNECTION_HEALTH_REFRESH_EVENT: "x",
    setSavingDatabases() {}, setLoadedDatabases() {}, setDatabases() {}, setPasswordDirty() {},
  };
  runInNewContext(edgeCode, context);
  return { calls, saveDatabases: context.saveDatabases };
}

test("edge settings refuses to save center databases after a failed load", async () => {
  const { calls, saveDatabases } = runEdgeSave({
    loadedDatabases: null,
    databaseError: "中心数据库配置加载失败",
    databases: { redis: DEFAULTS.redis, mysql: DEFAULTS.mysql, tdengine: DEFAULTS.tdengine },
  });
  await saveDatabases();
  assert.equal(calls.put.length, 0);
  assert.equal(calls.errors.length, 1);
});

test("edge settings submits only changed database fields", async () => {
  const loaded = { redis: LOADED.redis, mysql: LOADED.mysql, tdengine: LOADED.tdengine };
  const { calls, saveDatabases } = runEdgeSave({
    loadedDatabases: loaded,
    databaseError: null,
    databases: { ...loaded, tdengine: { ...loaded.tdengine, port: "6042" } },
  });
  await saveDatabases();
  assert.deepEqual(calls.put, [{ tdengine: { port: 6042 } }]);
});

test("edge settings disables database editing, testing and saving until the config loads", () => {
  assert.ok(edge.includes("const canSaveDatabases = loadedDatabases !== null && databaseError === null;"));
  assert.ok(edge.includes("disabled={loading || savingDatabases || !canSaveDatabases}>"));
  assert.ok(edge.includes("disabled={loading || savingDatabases || !canSaveDatabases}\n          onChange"));
  assert.ok(edge.includes("await agentClient.putConfig(databasePatch(loadedDatabases, [target]));"));
});

// ─── B060：保存在途期间编辑的新密码不得被成功回调清掉 dirty ──────────────────

function deferredPromise() {
  let resolve;
  const promise = new Promise((yes) => { resolve = yes; });
  return { promise, resolve };
}

test("a password edited while a save is in flight is still sent by the next save", async () => {
  const puts = [];
  const pending = [];
  const context = {
    loadedForm: LOADED, configError: null,
    form: { ...LOADED, redis: { ...LOADED.redis, password: "A" } },
    passwordDirty: { redis: true, mysql: false, tdengine: false },
    BUILD_ROLE: "centralized",
    buildConfigPatch,
    agentClient: { putConfig: (payload) => { puts.push(payload); const d = deferredPromise(); pending.push(d); return d.promise; } },
    toast: { error() {}, success() {} },
    formDirtyRef: { current: true },
    editVersionRef: { current: { form: 1, redis: 1, mysql: 0, tdengine: 0 } },
    setSaving() {}, logout() {},
  };
  context.setLoadedForm = (value) => { context.loadedForm = value; };
  context.setPasswordDirty = (next) => { context.passwordDirty = typeof next === "function" ? next(context.passwordDirty) : next; };
  runInNewContext(settingsSaveCode, context);

  const first = context.handleSave();
  // 请求未返回时用户输入新密码 B（模拟 onRedisChange + markPasswordDirty）
  context.form = { ...context.form, redis: { ...context.form.redis, password: "B" } };
  context.editVersionRef.current.form += 2;
  context.editVersionRef.current.redis += 1;
  context.passwordDirty = { ...context.passwordDirty, redis: true };
  pending[0].resolve();
  await first;
  assert.equal(context.passwordDirty.redis, true, "保存期间的新输入必须保持 dirty");
  assert.equal(context.formDirtyRef.current, true);

  const second = context.handleSave();
  pending[1].resolve();
  await second;
  assert.deepEqual(puts[0], { redis: { password: "A" } });
  assert.deepEqual(puts[1], { redis: { password: "B" } });
  assert.equal(context.passwordDirty.redis, false);
  assert.equal(context.formDirtyRef.current, false);
});

const edgeTestCode = compile(
  slice(edge, "const DB_META", "const DEFAULT_DATABASES")
  + slice(edge, "function validateDatabase(", "function testBadge(")
  + slice(edge, "  const canSaveDatabases =", "  async function saveDatabases()")
  + "\nglobalThis.testDatabase = testDatabase; globalThis.updatePassword = updatePassword;",
);

test("edge test-and-apply keeps a password typed while the test was running", async () => {
  const loaded = { redis: LOADED.redis, mysql: { ...LOADED.mysql }, tdengine: LOADED.tdengine };
  const health = deferredPromise();
  const puts = [];
  const context = {
    loadedDatabases: loaded, databaseError: null,
    databases: { ...loaded, mysql: { ...loaded.mysql, password: "A" } },
    passwordDirty: { redis: false, mysql: true, tdengine: false },
    tests: {},
    INITIAL_TESTS: { redis: { status: "idle" }, mysql: { status: "idle" }, tdengine: { status: "idle" } },
    editVersionRef: { current: { redis: 0, mysql: 1, tdengine: 0 } },
    buildConfigPatch,
    agentClient: { checkHealth: () => health.promise, putConfig: async (payload) => { puts.push(payload); } },
    toast: { error() {}, success() {} },
    window: { dispatchEvent() {} }, Event: class {}, CONNECTION_HEALTH_REFRESH_EVENT: "x",
  };
  // 与 React 一致：setter 只更新"下一次渲染"的状态，进行中的 testDatabase 闭包仍看到提交时的快照
  const state = { loadedDatabases: context.loadedDatabases, databases: context.databases, passwordDirty: context.passwordDirty, tests: {} };
  const apply = (key) => (next) => { state[key] = typeof next === "function" ? next(state[key]) : next; };
  Object.assign(context, {
    setLoadedDatabases: apply("loadedDatabases"), setDatabases: apply("databases"),
    setPasswordDirty: apply("passwordDirty"), setTests: apply("tests"),
  });
  runInNewContext(edgeTestCode, context);

  const running = context.testDatabase("mysql");
  context.updatePassword("mysql", "B");
  health.resolve({ success: true });
  await running;
  assert.deepEqual(puts, [{ mysql: { password: "A" } }]);
  assert.equal(state.databases.mysql.password, "B", "测试期间输入的新密码不得被清空");
  assert.equal(state.passwordDirty.mysql, true);
  assert.equal(state.loadedDatabases.mysql.password, "");
});
