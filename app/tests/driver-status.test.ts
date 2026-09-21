import assert from "node:assert/strict";
import test from "node:test";
import { resolveStatusBadge } from "../src/lib/driver-status.ts";

const row = { id: 1, driverName: "S7", driverDesc: "", readWriteType: "只读", updateTime: "", source: "bundled" as const, installStatus: "installed" };
for (const [runtime, label] of [
  ["collector_stopped", "已安装 · 采集未运行"],
  ["unknown", "已安装 · 运行状态未知"],
  ["load_failed", "已安装 · 加载失败"],
  ["not_loaded", "已安装 · 未加载"],
  ["loaded", "已加载 · 未使用"],
]) {
  test(`installed driver with runtime ${runtime}`, () => {
    assert.equal(resolveStatusBadge({ ...row, loadStatus: runtime }).label, label);
  });
}
test("missing files take precedence over a still-loaded old process", () => {
  assert.equal(resolveStatusBadge({ ...row, installStatus: "missing", loadStatus: "loaded" }).label, "包缺失");
});
test("unknown installation is never promoted to installed by process state", () => {
  assert.equal(resolveStatusBadge({ ...row, installStatus: "unknown", loadStatus: "collector_stopped" }).label, "安装状态未知");
});
test("configuration references do not claim actual runtime use", () => {
  assert.equal(resolveStatusBadge({ ...row, loadStatus: "loaded", boundDeviceCount: 2 }).label, "已加载 · 有设备引用");
});

test("removed official driver remains visibly uninstalled", () => {
  assert.equal(resolveStatusBadge({ ...row, installStatus: "not_installed", loadStatus: "collector_stopped" }).label, "未安装");
});
test("distributed center is not a missing driver node", () => {
  assert.equal(resolveStatusBadge({ ...row, installStatus: "not_applicable" }).label, "本机不适用");
});
