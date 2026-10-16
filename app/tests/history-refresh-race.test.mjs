import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";

const source = (await readFile(new URL("../src/pages/HistoryDataPage.tsx", import.meta.url), "utf8")).replaceAll("\r\n", "\n");
const refresh = source.slice(source.indexOf("  useEffect(() => {\n    const snapshot = lastQueryRef.current;"), source.indexOf("  useEffect(() => {\n    if (!deviceId"));
const matrix = source.slice(source.indexOf("  async function loadMatrix("), source.indexOf("  async function queryHistory("));
assert.ok(refresh.includes("Promise.all("));
assert.ok(matrix.includes("historyRawMatrix"));
const code = ts.transpileModule(refresh + matrix + "\nglobalThis.loadMatrix = loadMatrix;", {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS }
}).outputText;

function harness(honorCancellation = false) {
  let chartDone, matrixDone, matrixFailed;
  const chartResponse = new Promise(resolve => { chartDone = resolve; });
  const matrixResponse = new Promise((resolve, reject) => { matrixDone = resolve; matrixFailed = reject; });
  const state = { loading: false, matrixLoading: false, chart: null, matrix: null };
  let calls = 0;
  const context = {
    lastQueryRef: { current: { query: { varSeqs: [1] } } }, matrixAlignRef: { current: "100ms" },
    queryVersionRef: { current: 0 }, matrixVersionRef: { current: 0 },
    chartRequestRef: { current: null }, matrixRequestRef: { current: null }, AbortController,
    token: "test", refreshVersion: 1, loading: false, logout() {},
    toast: { error() {} }, historyErrorMessage: (_reason, message) => message,
    setLoading: value => { state.loading = value; },
    setMatrixLoading: value => { state.matrixLoading = value; },
    setChart: value => { state.chart = value; },
    setMatrix: value => { state.matrix = typeof value === "function" ? value(state.matrix) : value; },
    monitorClient: { historyChart: () => chartResponse,
      historyRawMatrix: (_token, _query, signal) => {
        if (++calls !== 1) return Promise.resolve({ marker: "new" });
        if (honorCancellation) signal.addEventListener("abort", () => matrixFailed(signal.reason), { once: true });
        return matrixResponse;
      } },
    useEffect: callback => { context.refresh = callback; }
  };
  runInNewContext(code, context);
  return { context, state, finish: () => { chartDone({ marker: "chart" }); matrixDone({ marker: "old" }); } };
}

test("changing matrix alignment during refresh completes global loading and keeps the new matrix", async () => {
  const page = harness();
  page.context.refresh();
  await page.context.loadMatrix("1s", true);
  page.finish();
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(page.state.chart.marker, "chart");
  assert.equal(page.state.matrix.marker, "new");
  assert.equal(page.state.loading, false);
  assert.equal(page.state.matrixLoading, false);
});

test("an obsolete refresh cannot clear a newer query's loading state", async () => {
  const page = harness();
  page.context.refresh();
  ++page.context.queryVersionRef.current;
  page.finish();
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(page.state.chart, null);
  assert.equal(page.state.matrix, null);
  assert.equal(page.state.loading, true);
});

test("cancelling the previous matrix during refresh keeps the chart request alive", async () => {
  const page = harness(true);
  page.context.refresh();
  const oldMatrix = page.context.matrixRequestRef.current;
  await page.context.loadMatrix("1s", true);
  assert.equal(oldMatrix.signal.aborted, true);
  assert.equal(page.context.chartRequestRef.current.signal.aborted, false);
  page.finish();
  await new Promise(resolve => setImmediate(resolve));
  assert.equal(page.state.chart.marker, "chart");
  assert.equal(page.state.matrix.marker, "new");
  assert.equal(page.state.loading, false);
});

test("a completed history chart is visible while the raw matrix is still loading", async () => {
  const querySource = source.slice(source.indexOf("  async function queryHistory("), source.indexOf("  function openExportDialog("));
  const queryCode = ts.transpileModule(querySource + "\nglobalThis.queryHistory = queryHistory;", {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
  let finishMatrix;
  const matrixRequest = new Promise(resolve => { finishMatrix = resolve; });
  const state = { loading: false, chart: null, matrixStarted: false };
  const context = {
    token: "test", validateQuery: () => true, buildQuery: () => ({ deviceId: 1, varSeqs: [1] }),
    queryVersionRef: { current: 0 }, matrixVersionRef: { current: 0 }, lastQueryRef: { current: null }, matrixAlign: "1s",
    chartRequestRef: { current: null }, matrixRequestRef: { current: null }, AbortController,
    setLoading: value => { state.loading = value; }, setChart: value => { state.chart = value; }, setMatrix() {},
    monitorClient: { historyChart: async () => ({ series: [1] }) },
    loadMatrix: () => { state.matrixStarted = true; return matrixRequest; },
    logout() {}, toast: { error() {} }, historyErrorMessage: (_reason, message) => message,
  };
  runInNewContext(queryCode, context);
  const pending = context.queryHistory();
  await new Promise(resolve => setImmediate(resolve));
  try {
    assert.equal(state.matrixStarted, true);
    assert.ok(state.chart);
    assert.equal(state.loading, false);
  } finally { finishMatrix(); await pending; }
});
