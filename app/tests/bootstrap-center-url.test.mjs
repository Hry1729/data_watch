// M1 / M2 / M6 回归：注册后锁定中心地址；统一 URL 校验；检测结果绑定表单版本；失败原因完整展示。
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";
import { normalizeBackendOrigin, validateBackendOrigin } from "../src/lib/backend-proxy-policy.ts";

const read = async (path) => (await readFile(new URL(path, import.meta.url), "utf8")).replaceAll("\r\n", "\n");
const bootstrap = await read("../src/pages/BootstrapPage.tsx");
const edgeBootstrap = await read("../src/pages/EdgeBootstrapPage.tsx");
const backendClient = await read("../src/lib/backend-client.ts");

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

function deferred() {
  let resolve, reject;
  const promise = new Promise((yes, no) => { resolve = yes; reject = no; });
  return { promise, resolve, reject };
}

// ─── M2 ──────────────────────────────────────────────────────────────────────

test("one shared validator explains why a backend / center URL is rejected", () => {
  assert.deepEqual(validateBackendOrigin(" http://192.168.1.10:8080/ "), { origin: "http://192.168.1.10:8080", error: null });
  assert.match(validateBackendOrigin("").error, /不能为空/);
  assert.match(validateBackendOrigin("192.168.1.10:8080").error, /http/);
  assert.match(validateBackendOrigin("http://gw/datawatch").error, /不能带路径/);
  assert.match(validateBackendOrigin("http://host:8000/api").error, /不能带路径/);
  assert.match(validateBackendOrigin("http://u:p@host:8000").error, /用户名或密码/);
  assert.equal(normalizeBackendOrigin("http://gw/datawatch"), null);
});

test("bootstrap pages and the runtime injection all use the shared validator", () => {
  assert.ok(bootstrap.includes("validateBackendOrigin(backend.base_url)"));
  assert.ok(bootstrap.includes("backend: { base_url: backendCheck.origin }"));
  assert.ok(edgeBootstrap.includes("const centerCheck = validateBackendOrigin(form.centerUrl);"));
  assert.ok(edgeBootstrap.includes("const centerUrl = centerCheck.origin;"));
  // 非法地址不再静默置空，后续请求带上具体原因
  assert.ok(backendClient.includes("const check = validateBackendOrigin(url || \"\");"));
  assert.ok(backendClient.includes("后端地址无效（${url}）：${check.error}"));
  assert.equal(backendClient.split("message: backendUrlMissingMessage()").length - 1, 3);
});

// ─── M6：检测结果与表单版本绑定 ─────────────────────────────────────────────

const bootstrapTestCode = compile(
  slice(bootstrap, "function errorMessage(", "function StatusBadge(")
  + slice(bootstrap, "  function resetStatus(", "  function mysqlPort()")
  + slice(bootstrap, "  async function handleTest(", "  async function handleRetestAll()")
  + slice(bootstrap, "  async function handleFinish()", "  const selectedError =")
  + "\nObject.assign(globalThis, { resetStatus, handleTest, handleFinish });",
);

function bootstrapHarness({ testResponse, finish, status: bootstrapStatus } = {}) {
  const state = {
    status: { backend: "idle", mysql: "idle", redis: "idle", tdengine: "idle" },
    errors: { backend: null, mysql: null, redis: null, tdengine: null },
    toasts: [],
  };
  const apply = (key) => (next) => { state[key] = typeof next === "function" ? next(state[key]) : next; };
  const context = {
    formVersionRef: { current: { backend: 0, mysql: 0, redis: 0, tdengine: 0 } },
    backend: { base_url: "http://127.0.0.1:8000" },
    mysql: {}, redis: {}, tdengine: {},
    mysqlPort: () => 3306, redisPort: () => 6379, tdenginePort: () => 6041,
    allPassed: true, finishing: false,
    testPayload: (target) => ({ target }),
    validateBackendOrigin,
    agentClient: {
      postBootstrapTestConnection: () => testResponse,
      putBootstrapConfig: async () => {},
      postBootstrapFinish: finish ?? (async () => {}),
      getBootstrapStatus: async () => bootstrapStatus ?? { in_bootstrap: false },
    },
    toast: { error: (message) => state.toasts.push(message) },
    navigate() {}, setBackendBaseUrl() {}, setFinishing() {},
    setStatus: apply("status"), setErrors: apply("errors"),
    Error,
  };
  runInNewContext(bootstrapTestCode, context);
  return { state, context };
}

test("a detection that returns after the form changed is discarded", async () => {
  const response = deferred();
  const { state, context } = bootstrapHarness({ testResponse: response.promise });
  const pending = context.handleTest("mysql");
  assert.equal(state.status.mysql, "testing");
  context.resetStatus("mysql"); // 用户在检测进行中修改了 MySQL 表单
  response.resolve({ success: true });
  await pending;
  assert.equal(state.status.mysql, "idle", "旧检测结果不得把新表单标成已通过");
});

test("a detection for the current form still records its result", async () => {
  const { state, context } = bootstrapHarness({ testResponse: Promise.resolve({ success: true }) });
  await context.handleTest("redis");
  assert.equal(state.status.redis, "success");
});

test("detection failures keep the Agent's full reason", async () => {
  const { state, context } = bootstrapHarness({
    testResponse: Promise.reject({ type: "api_error", status: 400, message: "MySQL 认证失败：Access denied for user 'dw'" }),
  });
  await context.handleTest("mysql");
  assert.equal(state.status.mysql, "failed");
  assert.equal(state.errors.mysql, "MySQL 认证失败：Access denied for user 'dw'");
});

test("an invalid backend URL fails the backend check with the validator reason", async () => {
  const { state, context } = bootstrapHarness({ testResponse: Promise.resolve({ success: true }) });
  context.backend.base_url = "http://host:8000/api";
  await context.handleTest("backend");
  assert.equal(state.status.backend, "failed");
  assert.match(state.errors.backend, /不能带路径/);
});

test("finish errors and an unfinished bootstrap are reported instead of swallowed", async () => {
  const failing = bootstrapHarness({
    finish: async () => { throw { type: "api_error", status: 409, message: "引导已完成" }; },
  });
  await failing.context.handleFinish();
  assert.deepEqual(failing.state.toasts, ["初始化失败：引导已完成"]);

  const stuck = bootstrapHarness({ status: { in_bootstrap: true } });
  await stuck.context.handleFinish();
  assert.equal(stuck.state.toasts.length, 1);
  assert.match(stuck.state.toasts[0], /初始化未完成/);
});

const edgeTestCode = compile(
  slice(edgeBootstrap, "  function resetTest(", "  function updateCenterUrl(")
  + slice(edgeBootstrap, "  async function testDatabase(", "  async function retestAll()")
  + "\nObject.assign(globalThis, { resetTest, testDatabase });",
);

test("edge MySQL detection is bound to the form version too", async () => {
  const response = deferred();
  const state = { tests: { mysql: { status: "idle", error: null } } };
  const context = {
    formVersionRef: { current: { mysql: 0 } },
    databases: { mysql: { host: "10.0.0.2", port: "3306", user: "dw", password: "", database: "device_watch" } },
    databasePayload: () => ({}),
    agentClient: { postBootstrapTestConnection: () => response.promise },
    errorMessage: () => "x",
    setTests: (next) => { state.tests = typeof next === "function" ? next(state.tests) : next; },
  };
  runInNewContext(edgeTestCode, context);
  const pending = context.testDatabase("mysql");
  context.resetTest("mysql");
  response.resolve({ success: true });
  await pending;
  assert.equal(state.tests.mysql.status, "idle");
});

// ─── M1：注册后锁定中心地址 ─────────────────────────────────────────────────

test("a registered edge node shows the center URL read-only and refuses to change it", () => {
  assert.ok(edgeBootstrap.includes("readOnly={registered}"));
  assert.ok(edgeBootstrap.includes("地址已锁定（只读）。更换中心需要重新注册"));
  const update = slice(edgeBootstrap, "  function updateCenterUrl(", "  function updateDatabase(");
  assert.match(update, /if \(registered\) return;/);
  const submit = slice(edgeBootstrap, "  async function submit(", "  if (loading) {");
  assert.match(submit, /registered && centerCheck\.origin !== validateBackendOrigin\(registeredCenterUrl\)\.origin/);
  assert.ok(edgeBootstrap.includes("setRegisteredCenterUrl(status.registered ? status.centerUrl : \"\");"));
});
