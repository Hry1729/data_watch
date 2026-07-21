import assert from "node:assert/strict";
import test from "node:test";

import {
  normalizeSettingsTab,
  SETTINGS_TABS,
} from "../src/components/layout/settings-tabs.ts";

test("settings tabs expose the approved sections in display order", () => {
  assert.deepEqual(SETTINGS_TABS, [
    { value: "basic", label: "基础" },
    { value: "database", label: "数据库" },
    { value: "collector", label: "Collector" },
    { value: "writer", label: "Writer" },
  ]);
});

test("settings tab normalization defaults missing and invalid values to database", () => {
  assert.equal(normalizeSettingsTab(null), "database");
  assert.equal(normalizeSettingsTab("unknown"), "database");
  assert.equal(normalizeSettingsTab("basic"), "basic");
  assert.equal(normalizeSettingsTab("collector"), "collector");
});

