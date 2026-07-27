import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const clientSource = await readFile(new URL("../src/lib/backend-client.ts", import.meta.url), "utf8");
const hookSource = await readFile(new URL("../src/hooks/useRealtimeWebSocket.ts", import.meta.url), "utf8");
const pageSource = await readFile(new URL("../src/pages/RealtimeMonitorPage.tsx", import.meta.url), "utf8");

test("realtime client exposes the existing group and websocket endpoints", () => {
  assert.match(clientSource, /realtimePointGroups\(/);
  assert.match(clientSource, /\/api\/device-monitor\/device\/\$\{encodeURIComponent\(deviceCode\)\}\/point-groups/);
  assert.match(clientSource, /getBackendWebSocketUrl/);
  assert.match(clientSource, /\/api\/device-monitor\/ws\/realtime-push/);
});

test("realtime websocket authenticates, subscribes, heartbeats, and reconnects", () => {
  assert.match(hookSource, /type:\s*"auth"/);
  assert.match(hookSource, /action:\s*"subscribe"/);
  assert.match(hookSource, /action:\s*"unsubscribe"/);
  assert.match(hookSource, /action:\s*"ping"/);
  assert.match(hookSource, /setTimeout/);
  assert.match(hookSource, /tdengineConnected/);
  assert.match(hookSource, /message\.type === "offline"/);
  assert.match(hookSource, /setDeviceOffline\(message\.deviceOffline \?\? false\)/);
  assert.match(hookSource, /socket\.onclose = \(\) => \{[\s\S]*pendingRef\.current = \{\}[\s\S]*setValues\(\{\}\)[\s\S]*setDeviceOffline\(true\)/);
});

test("realtime page retains the original monitoring workflow", () => {
  for (const label of [
    "开始监控",
    "停止监控",
    "清空选择",
    "导出",
    "WebSocket已连接",
    "0.1秒",
    "30秒",
    "折线图",
    "面积图",
    "状态图",
    "图表设置",
    "Bool 型变量独立图表",
    "最后同步时间",
  ]) {
    assert.match(pageSource, new RegExp(label.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")), label);
  }
  assert.match(pageSource, /useRealtimeWebSocket/);
  assert.match(pageSource, /realtimePointGroups/);
  assert.match(pageSource, /saveDownload/);
});
test("realtime page loads and switches point groups without hardcoding g001", () => {
  assert.match(pageSource, /backendClient\.realtimePointGroups/);
  assert.match(pageSource, /selectedGroupCode/);
  assert.match(pageSource, /realtimeInit\(backendToken,\s*selectedDeviceId,\s*selectedGroupCode\)/);
  assert.match(pageSource, /groupCode:\s*selectedGroupCode/);
  assert.match(pageSource, /点位组/);
});

test("realtime page uses platform quality codes", () => {
  assert.match(pageSource, /realtimeSocket\.deviceOffline\s*\?\s*7/);
  assert.match(pageSource, /point\.quality\s*===\s*0/);
  assert.doesNotMatch(pageSource, /point\.quality\s*>=\s*192/);
  assert.match(pageSource, /\[pointList, realtimeSocket\.deviceOffline, realtimeSocket\.values\]/);
});
