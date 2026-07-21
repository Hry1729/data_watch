import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import test from "node:test";

const source = await readFile(new URL("../src/pages/SettingsPage.tsx", import.meta.url), "utf8");

test("settings content is controlled by the shared query-string tab", () => {
  assert.match(source, /useSearchParams/);
  assert.match(source, /normalizeSettingsTab/);
  assert.match(source, /<Tabs value=\{activeTab\}>/);
});

test("settings page leaves its header and section navigation to AppLayout", () => {
  assert.doesNotMatch(source, /<TabsList/);
  assert.doesNotMatch(source, /<ArrowLeft/);
  assert.doesNotMatch(source, /顶部返回行/);
});

