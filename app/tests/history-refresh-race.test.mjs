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

function harness() {
  let chartDone, matrixDone;
  const chartResponse = new Promise(resolve => { chartDone = resolve; });
  const matrixResponse = new Promise(resolve => { matrixDone = resolve; });
  const state = { loading: false, matrixLoading: false, chart: null, matrix: null };
  let calls = 0;
  const context = {
    lastQueryRef: { current: { query: { varSeqs: [1] } } }, matrixAlignRef: { current: "100ms" },
    queryVersionRef: { current: 0 }, matrixVersionRef: { current: 0 },
    token: "test", refreshVersion: 1, loading: false, logout() {},
    toast: { error() {} }, historyErrorMessage: (_reason, message) => message,
    setLoading: value => { state.loading = value; },
    setMatrixLoading: value => { state.matrixLoading = value; },
    setChart: value => { state.chart = value; },
    setMatrix: value => { state.matrix = typeof value === "function" ? value(state.matrix) : value; },
    monitorClient: { historyChart: () => chartResponse,
      historyRawMatrix: () => ++calls === 1 ? matrixResponse : Promise.resolve({ marker: "new" }) },
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
