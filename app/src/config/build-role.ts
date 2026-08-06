export const BUILD_ROLES = [
  "centralized",
  "edge",
  "distributed-center",
] as const;

export type BuildRole = (typeof BUILD_ROLES)[number];

export function resolveBuildRole(value: string | undefined): BuildRole {
  const normalized = value?.trim() || "centralized";
  if ((BUILD_ROLES as readonly string[]).includes(normalized)) {
    return normalized as BuildRole;
  }
  throw new Error(`Unsupported VITE_DATAWATCH_ROLE: ${normalized}`);
}

declare const __DATAWATCH_ROLE__: string | undefined;
declare const __DATAWATCH_AGENT_PORT__: number | undefined;

const embeddedRole = typeof __DATAWATCH_ROLE__ === "undefined"
  ? undefined
  : __DATAWATCH_ROLE__;

export const BUILD_ROLE = resolveBuildRole(embeddedRole);

export const AGENT_PORT = typeof __DATAWATCH_AGENT_PORT__ === "undefined"
  ? 7070
  : __DATAWATCH_AGENT_PORT__;
export const AGENT_HTTP_ORIGIN = `http://127.0.0.1:${AGENT_PORT}`;
export const AGENT_WS_ORIGIN = `ws://127.0.0.1:${AGENT_PORT}`;

export const roleCapabilities = {
  collector: BUILD_ROLE !== "distributed-center",
  ingest: BUILD_ROLE === "distributed-center",
  writer: BUILD_ROLE !== "edge",
  edgeOperations: BUILD_ROLE === "edge",
  distributedNodes: BUILD_ROLE === "distributed-center",
  centerAdministration: BUILD_ROLE !== "edge",
} as const;

export function roleAllows(roles: readonly BuildRole[]): boolean {
  return roles.includes(BUILD_ROLE);
}
