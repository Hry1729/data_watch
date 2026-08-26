import assert from "node:assert/strict";
import test from "node:test";
import { buildPasswordResetRequest, stripClientPassword } from "../src/lib/password-contract.ts";

test("password reset request has no client-selected password", () => {
  assert.deepEqual(buildPasswordResetRequest(), {});
  assert.equal(Object.prototype.hasOwnProperty.call(buildPasswordResetRequest(), "password"), false);
});

test("user creation strips a stale password field before the request", () => {
  const payload = { userName: "operator", password: "legacy-value", userRoles: ["R_USER"] };
  assert.deepEqual(stripClientPassword(payload), { userName: "operator", userRoles: ["R_USER"] });
  assert.equal(Object.prototype.hasOwnProperty.call(stripClientPassword(payload), "password"), false);
  assert.equal(payload.password, "legacy-value");
});
