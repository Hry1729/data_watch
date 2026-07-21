import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const mockSource = await readFile(new URL("../src/lib/mock-agent.ts", import.meta.url), "utf8");
const historySource = await readFile(new URL("../src/pages/HistoryDataPage.tsx", import.meta.url), "utf8");
const proxySource = await readFile(new URL("../src-tauri/src/http_proxy.rs", import.meta.url), "utf8");
const tauriSource = await readFile(new URL("../src-tauri/src/lib.rs", import.meta.url), "utf8");

test("history export uses existing task, status, and download routes", () => {
  for (const operation of ["historyExport", "historyExportStatus", "historyExportDownload"]) {
    assert.ok((clientSource.match(new RegExp(`${operation}\\(`, "g")) ?? []).length >= 3, operation);
    assert.match(mockSource, new RegExp(`async ${operation}\\(`), operation);
  }
  assert.match(clientSource, /\/api\/device-monitor\/history\/export/);
});

test("Tauri exposes an authenticated binary backend download command", () => {
  assert.match(proxySource, /pub async fn backend_download/);
  assert.match(tauriSource, /http_proxy::backend_download/);
});

test("history page runs export instead of rendering a disabled placeholder", () => {
  assert.match(historySource, /backendClient\.historyExport/);
  assert.match(historySource, /backendClient\.historyExportStatus/);
  assert.match(historySource, /backendClient\.historyExportDownload/);
  assert.doesNotMatch(historySource, /历史导出将在导出任务页统一接入/);
});
