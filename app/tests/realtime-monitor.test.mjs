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
  assert.match(hookSource, /agentClient\.getLocalRealtimeLatest/);
  assert.match(hookSource, /socket\.onclose = \(\) => \{[\s\S]*startLocalPolling\(\)[\s\S]*scheduleReconnect\(\)/);
});

test("realtime page retains the original monitoring workflow", () => {
  for (const label of [
    "开始监控",
    "停止监控",
    "清空选择",
    "导出",
    "中心实时连接",
    "本机数据",
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

test("realtime trend stays inside the viewport and compacts the bool chart", () => {
  assert.match(pageSource, /flex h-full min-h-0[\s\S]*overflow-hidden/);
  // 数值主图占满剩余高度，Bool 点位以紧凑状态泳道单独展示在下方
  assert.match(pageSource, /className="min-h-\[200px\] flex-1"/);
  assert.match(pageSource, /function BoolLanes/);
  assert.match(pageSource, /<BoolLanes points=\{boolColored\}/);
  assert.match(pageSource, /compact \? "h-16 max-h-16"/);
  assert.match(pageSource, /chartType !== "status" && separateBoolChart/);
  assert.doesNotMatch(pageSource, /h-\[420px\]/);
});

test("bool trends use a visible step line with a padded binary axis", () => {
  assert.match(pageSource, /const boolOnly = points\.every/);
  assert.match(pageSource, /domain=\{boolOnly \? \[-0\.15, 1\.15\] : undefined\}/);
  assert.match(pageSource, /ticks=\{boolOnly \? \[0, 1\] : undefined\}/);
  assert.match(pageSource, /type=\{boolOnly \? "stepAfter" : "monotone"\}/);
});
test("realtime page loads and switches point groups without hardcoding g001", () => {
  assert.match(pageSource, /monitorClient\.realtimePointGroups/);
  assert.match(pageSource, /selectedGroupCode/);
  assert.match(pageSource, /realtimeInit\(backendToken,\s*selectedDeviceId,\s*selectedGroupCode,\s*signal\)/);
  assert.match(pageSource, /groupCode:\s*selectedGroupCode/);
  assert.match(pageSource, /点位组/);
});

test("realtime page uses platform quality codes", () => {
  assert.match(pageSource, /realtimeSocket\.deviceOffline\s*\?\s*7/);
  assert.match(pageSource, /point\.quality\s*===\s*0/);
  assert.doesNotMatch(pageSource, /point\.quality\s*>=\s*192/);
  assert.match(pageSource, /\[pointList, realtimeSocket\.deviceOffline, realtimeSocket\.values\]/);
});

test("desktop realtime sampling follows the selected cadence and retains five minutes", () => {
  assert.match(hookSource, /setSampleVersion\(\(current\) => current \+ 1\)/);
  assert.match(hookSource, /localPollInFlight/);
  assert.match(pageSource, /REALTIME_WINDOW_MS = 5 \* 60_000/);
  assert.match(pageSource, /MAX_REALTIME_SAMPLES = 3000/);
  assert.match(pageSource, /REALTIME_VIEW_WINDOWS/);
  assert.match(pageSource, /DEFAULT_REALTIME_VIEW_MS = 60_000/);
  assert.match(pageSource, /const firstSampleAt/);
  assert.match(pageSource, /const rightPadding/);
  assert.match(pageSource, /Math\.max\(firstSampleAt, latestSampleAt - viewWindowMs \+ rightPadding\)/);
  assert.match(pageSource, /dataKey="sampleAt"[\s\S]*type="number"[\s\S]*domain=\{viewDomain\}/);
  assert.match(pageSource, /最大显示跨度/);
  assert.match(pageSource, /viewWindowMs/);
  assert.match(pageSource, /realtimeSocket\.sampleVersion/);
  assert.match(pageSource, /padStart\(3, "0"\)/);
  assert.doesNotMatch(pageSource, /lastSampleRef/);
});
