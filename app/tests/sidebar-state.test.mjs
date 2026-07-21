import assert from "node:assert/strict";
import test from "node:test";

import {
  readSidebarCollapsed,
  SIDEBAR_STORAGE_KEY,
  writeSidebarCollapsed,
} from "../src/components/layout/sidebar-state.ts";

test("sidebar defaults to expanded and restores only an explicit collapsed state", () => {
  assert.equal(readSidebarCollapsed(null), false);
  assert.equal(readSidebarCollapsed({ getItem: () => null }), false);
  assert.equal(readSidebarCollapsed({ getItem: () => "false" }), false);
  assert.equal(readSidebarCollapsed({ getItem: () => "true" }), true);
});

test("sidebar collapse state is written under the stable application key", () => {
  const writes = [];
  const storage = {
    setItem(key, value) {
      writes.push([key, value]);
    },
  };

  writeSidebarCollapsed(storage, true);
  writeSidebarCollapsed(storage, false);

  assert.equal(SIDEBAR_STORAGE_KEY, "data-watch-sidebar-collapsed");
  assert.deepEqual(writes, [
    [SIDEBAR_STORAGE_KEY, "true"],
    [SIDEBAR_STORAGE_KEY, "false"],
  ]);
});
