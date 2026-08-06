import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

import {
  BUILD_ROLES,
  resolveBuildRole,
} from "../src/config/build-role.ts";

test("three build roles are fixed", () => {
  assert.deepEqual(BUILD_ROLES, [
    "centralized",
    "edge",
    "distributed-center",
  ]);
});

test("role defaults to centralized and rejects runtime-like values", () => {
  assert.equal(resolveBuildRole(undefined), "centralized");
  assert.equal(resolveBuildRole("edge"), "edge");
  assert.throws(() => resolveBuildRole("center"), /Unsupported/);
});
test("installer entrypoints match the fixed role manifest", () => {
  const appRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
  const installerRoot = path.resolve(appRoot, "../installer");
  const manifest = JSON.parse(
    fs.readFileSync(path.join(installerRoot, "roles.json"), "utf8"),
  ).roles;
  const wrappers = {
    centralized: "DataWatchCentralized.iss",
    edge: "DataWatchEdge.iss",
    "distributed-center": "DataWatchDistributedCenter.iss",
  };

  for (const [roleName, wrapperName] of Object.entries(wrappers)) {
    const source = fs.readFileSync(path.join(installerRoot, wrapperName), "utf8");
    const role = manifest[roleName];
    assert.ok(source.includes('#define BuildRole "' + roleName + '"'));
    assert.ok(source.includes('#define MyAppId "{{' + role.appId.slice(1)));
    assert.ok(source.includes('#define MyServiceName "' + role.serviceName + '"'));
    assert.ok(source.includes('#define MyDataDir "' + role.dataDir + '"'));
    assert.ok(source.includes('#define MyAgentPort "' + role.agentPort + '"'));
    assert.ok(source.includes('#define MyOutputName "' + role.outputName + '"'));
  }
});
test("windows role installers do not bundle or build docker resources", () => {
  const appRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
  const installerRoot = path.resolve(appRoot, "../installer");
  const roleInstaller = fs.readFileSync(
    path.join(installerRoot, "DataWatchRole.iss"),
    "utf8",
  );
  const buildScript = fs.readFileSync(
    path.join(installerRoot, "build-roles.ps1"),
    "utf8",
  );
  const manifest = fs.readFileSync(
    path.join(installerRoot, "roles.json"),
    "utf8",
  );

  assert.doesNotMatch(roleInstaller, /deploy\\images|docker-images|backend\.env/);
  assert.doesNotMatch(buildScript, /docker\s+(?:build|pull|save)/i);
  assert.doesNotMatch(buildScript, /UseExistingCenterImages/);
  assert.doesNotMatch(manifest, /center-infra|"backend"/);
});
test("role upgrades preserve the Agent service registration", () => {
  const appRoot = path.dirname(path.dirname(fileURLToPath(import.meta.url)));
  const roleInstaller = fs.readFileSync(
    path.resolve(appRoot, "../installer/DataWatchRole.iss"),
    "utf8",
  );

  const prepareBlock = roleInstaller.match(
    /function PrepareToInstall[\s\S]*?end;/,
  )?.[0] ?? "";
  const registerBlock = roleInstaller.match(
    /procedure RegisterRoleService[\s\S]*?procedure CurStepChanged/,
  )?.[0] ?? "";

  assert.match(prepareBlock, /StopRoleService;/);
  assert.doesNotMatch(prepareBlock, /RemoveRoleService/);
  assert.match(registerBlock, /if not ServiceExists then/);
  assert.match(registerBlock, /Application/);
  assert.match(registerBlock, /Start SERVICE_AUTO_START.*<> 0/);
  assert.match(
    roleInstaller,
    /CurUninstallStep = usUninstall then\s+RemoveRoleService/,
  );
});
