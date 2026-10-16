import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { runInNewContext } from "node:vm";
import test from "node:test";
import ts from "typescript";

const source = await readFile(new URL("../src/pages/DistributedNodesPage.tsx", import.meta.url), "utf8");
const ast = ts.createSourceFile("page.tsx", source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
const functions = new Map();
let toggle;
function visit(node) {
  if (ts.isFunctionDeclaration(node) && node.name) functions.set(node.name.text, node.getText(ast));
  if (ts.isJsxAttribute(node) && node.name.getText(ast) === "onCheckedChange" && node.initializer?.getText(ast).includes("setSelectedIds")) toggle = node.initializer.expression.getText(ast);
  ts.forEachChild(node, visit);
}
visit(ast);
function run(code, context) { runInNewContext(ts.transpileModule(code, { compilerOptions: { target: ts.ScriptTarget.ES2022 } }).outputText, context); }

test("assignments retain off-page selections when paging, searching, toggling, and saving", async () => {
  let saved;
  const context = {
    token: "test", AbortController, assignmentRequestRef: { current: null }, selectedIds: new Set(), assignmentNode: null,
    backendClient: {
      async distributedNodeAssignments() { return [{ deviceId: 3 }, { deviceId: 15001 }]; },
      async distributedAssignmentOccupancy() { return [{ deviceId: 15001, nodeUuid: "node1" }]; },
      async assignDistributedNodeDevices(_token, _node, ids) { saved = ids; return { migratedDeviceIds: [], removedDeviceIds: [] }; },
    },
    setAssignmentNode(node) { context.assignmentNode = node; },
    setAssignmentType() {}, setAssignmentSearch() {}, setAssignmentLoading() {}, setAssignmentOwners() {},
    setSelectedIds(value) { context.selectedIds = typeof value === "function" ? value(context.selectedIds) : value; },
    setBusy() {}, loadNodes: async () => {}, toast: { success() {}, error() {} }, errorMessage: String,
  };
  run(`${functions.get("openAssignments")}\n${functions.get("saveAssignments")}`, context);
  await context.openAssignments({ nodeUuid: "node1" });
  assert.deepEqual([...context.selectedIds].sort((a, b) => a - b), [3, 15001]);
  context.device = { id: 101 };
  run(`globalThis.toggle = ${toggle};`, context);
  context.toggle(true);
  context.device = { id: 201 };
  context.toggle(true);
  context.device = { id: 3 };
  context.toggle(false);
  await context.saveAssignments();
  assert.deepEqual(Array.from(saved).sort((a, b) => a - b), [101, 201, 15001]);
});

test("assignment controls lock disabled devices and devices owned by other nodes", () => {
  const lockSource = source.slice(source.indexOf("                    const owner ="), source.indexOf("                    return (", source.indexOf("                    const owner =")));
  for (const [status, ownerNode, expected] of [["enabled", "other", true], ["disabled", "node1", true], ["enabled", "node1", false], ["enabled", null, false]]) {
    const context = { device: { id: 1, deviceStatus: status }, assignmentNode: { nodeUuid: "node1" }, assignmentOwners: new Map(ownerNode ? [[1, { nodeUuid: ownerNode }]] : []) };
    run(`${lockSource}\nglobalThis.result = locked;`, context);
    assert.equal(context.result, expected);
  }
});
