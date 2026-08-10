import { invoke } from "@tauri-apps/api/core";
import { getAgentAuthToken } from "@/lib/agent-auth-token";
import { AGENT_PORT } from "@/config/build-role";
import type {
  AppError,
  ConnectionConfig,
  ConnectionHealth,
  DbConfig,
  ModuleName,
  ModuleStatus,
} from "@/types";
import type {
  DeviceListItem,
  DeviceListQuery,
  DeviceRealtimeStatus,
  DeviceTypeItem,
  HistoryChartResponse,
  HistoryDeviceItem,
  HistoryPointItem,
  HistoryQuery,
  HistoryRawMatrixResponse,
  PaginatedResult,
  PointListItem,
  PointListQuery,
  RealtimePointGroup,
  RealtimeWireValue,
} from "@/types/device-monitor";

export { setAgentAuthToken } from "@/lib/agent-auth-token";

// ─── 引导页载荷类型 ───────────────────────────────────────────────────────────

export type BootstrapTarget = "backend" | "mysql" | "redis" | "tdengine";

export interface BootstrapDbInput {
  host: string;
  port: number;
  user?: string;
  password?: string;
  database?: string;
}

export interface BootstrapConfigPayload {
  backend?: { base_url: string };
  mysql?: BootstrapDbInput;
  redis?: BootstrapDbInput;
  tdengine?: BootstrapDbInput;
}

export interface BootstrapTestPayload {
  target: BootstrapTarget;
  base_url?: string;
  host?: string;
  port?: number;
  user?: string;
  password?: string;
  database?: string;
}

export interface EdgeBootstrapRegisterPayload {
  center_url: string;
  node_name: string;
  registration_code: string;


}

export interface EdgeBootstrapStatus {
  nodeUuid: string;
  nodeName: string;
  centerUrl: string;
  registered: boolean;
  registrationStatus: string;
  lastSyncError: string;
}

export interface EdgeLocalSettings {
  node_uuid: string;
  node_name: string;
  center_url: string;
  registered: boolean;
  registration_status: string;
  last_sync_error: string;
  applied_revision: number;
  max_offline_retention_days: number;
  outbox_maintenance_seconds: number;
}

export interface EdgeLocalSettingsUpdate {
  max_offline_retention_days: number;
  outbox_maintenance_seconds: number;
}

// ─── 接口 ─────────────────────────────────────────────────────────────────────

export interface AgentDriverInfo {
  id: string;
  protocol: string;
  name: string;
  description: string;
  version: string;
  sha256: string;
  capabilities?: string[];
  readWriteType?: string;
  source?: string;
  status: string;
  error?: string | null;
  updated_at?: string;
}

export interface EdgeDeviceStatus {
  device_code: string;
  device_type: string;
  protocol: string;
  endpoint: string;
  scan_rate_ms: number;
  config_version: number;
  assignment_epoch: number;
  enabled: number;
  status: number | null;
  message: string | null;
  status_updated_at: number | null;
}

export interface EdgeOutboxStatus {
  counts: Record<string, number>;
  backlog_count: number;
  oldest_backlog_at: number | null;
  upload_session_id: string | null;
  last_seq: number;
  upload_interval_ms: number | null;
  session_switch_pending: boolean;
  database_bytes: number;
  dropped_records: number;
  sent_records_total: number;
  disk_total_bytes: number;
  disk_free_bytes: number;
  disk_used_percent: number;
  watermark: "normal" | "warning" | "critical" | "overflow";
}

export interface EdgeStorageStatus extends EdgeOutboxStatus {
  writer_queue_depth: number;
  writer_queue_capacity: number;
  commands_total: number;
  command_counts: Record<string, number>;
  errors_total: number;
  commit_p99_ms: number;
  frames_per_second: number;
  points_per_second: number;
  storage_uptime_seconds: number;
  error?: string;
}

export interface EdgeProcessStatus {
  name: string;
  status: string;
  pid: number | null;
  started_at: string | null;
  uptime_seconds: number;
  memory_mb: number;
  cpu_percent: number;
  restart_count: number;
}

export interface EdgeLocalDevices {
  config: {
    config_revision: number;
    checksum: string;
    read_timeout_ms: number | null;
    applied_at: number;
  } | null;
  fastest_point_scan_rate_ms: number | null;
  devices: EdgeDeviceStatus[];
}
export interface AgentClient {
  getBootstrapStatus():    Promise<{ in_bootstrap: boolean }>;
  getRuntimeConfig():      Promise<{ backend_base_url: string }>;
  putBootstrapConfig(cfg: BootstrapConfigPayload): Promise<void>;
  postBootstrapTestConnection(req: BootstrapTestPayload): Promise<{ success: boolean; message?: string }>;
  postBootstrapFinish():   Promise<void>;
  getEdgeBootstrapStatus(): Promise<EdgeBootstrapStatus>;
  registerEdge(req: EdgeBootstrapRegisterPayload): Promise<{ nodeUuid: string; status: string }>;
  agentLogin(u: string, p: string): Promise<{ token: string; roles: string[] }>;
  getViewerStatus():       Promise<{ agent: string }>;
  getLocalDevices():      Promise<EdgeLocalDevices>;
  getLocalCollector():    Promise<EdgeProcessStatus>;
  getLocalOutbox():       Promise<EdgeOutboxStatus>;
  getLocalUploader():     Promise<EdgeProcessStatus & { outbox: EdgeOutboxStatus }>;
  getLocalStorage():      Promise<EdgeProcessStatus & { storage: EdgeStorageStatus }>;
  getLocalSettings():     Promise<EdgeLocalSettings>;
  updateLocalSettings(settings: EdgeLocalSettingsUpdate): Promise<EdgeLocalSettings>;
  getLocalMonitorDevices(query: DeviceListQuery): Promise<PaginatedResult<DeviceListItem>>;
  getLocalMonitorPoints(query: PointListQuery): Promise<PaginatedResult<PointListItem>>;
  getLocalRealtimeGroups(deviceCode: string): Promise<RealtimePointGroup[]>;
  getLocalRealtime(deviceId: number, groupCode: string): Promise<DeviceRealtimeStatus>;
  getLocalRealtimeLatest(deviceCode: string, groupCode: string): Promise<{
    values: Record<string, RealtimeWireValue>;
    deviceOffline: boolean;
  }>;
  getLocalHistoryDevices(): Promise<HistoryDeviceItem[]>;
  getLocalHistoryDeviceTypes(): Promise<DeviceTypeItem[]>;
  getLocalHistoryPoints(deviceId: number): Promise<HistoryPointItem[]>;
  getLocalHistoryChart(query: HistoryQuery): Promise<HistoryChartResponse>;
  getLocalHistoryMatrix(query: HistoryQuery & {
    alignGranularity?: "100ms" | "500ms" | "1s";
    pageSize?: number;
    cursorTs?: string;
  }): Promise<HistoryRawMatrixResponse>;
  rotateEdgeIdentity():     Promise<{ keyVersion: number; uploaderRestarted: boolean; warning?: string }>;
  getConnectionHealth():    Promise<ConnectionHealth>;
  getProcessMetrics(name: ModuleName): Promise<ModuleStatus>;
  getProcessStatus(name: ModuleName): Promise<{ status: string }>;
  startProcess(name: ModuleName): Promise<void>;
  stopProcess(name: ModuleName): Promise<void>;
  restartProcess(name: ModuleName): Promise<void>;
  getConfig():   Promise<ConnectionConfig>;
  putConfig(cfg: Partial<ConnectionConfig>): Promise<void>;
  checkHealth(
    target: "redis" | "mysql" | "tdengine",
    config?: Partial<DbConfig>
  ): Promise<{ success: boolean; message?: string }>;
  listDrivers(): Promise<AgentDriverInfo[]>;
  installDriver(packageBase64: string): Promise<{ driver: AgentDriverInfo }>;
  uninstallDriver(driverId: string): Promise<{ driver: { id: string; status: string } }>;
}

// ─── 日志脱敏辅助 ─────────────────────────────────────────────────────────────

function sanitize(body: object): string {
  return JSON.stringify(body).replace(
    /"(password(?:_enc)?|registration_code)":"[^"]*"/g,
    '"$1":"[FILTERED]"'
  );
}

function getApiErrorMessage(text: string, fallback: string): string {
  try {
    const payload = JSON.parse(text) as { detail?: unknown; message?: unknown };
    if (typeof payload.detail === "string") return payload.detail;
    if (typeof payload.message === "string") return payload.message;
  } catch {
    // 忽略无效/非 JSON 的错误响应体
  }
  return fallback;
}

function toAgentQuery(params: object): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === "") continue;
    if (Array.isArray(value)) {
      value.forEach((item) => query.append(key, String(item)));
      continue;
    }
    query.set(key, String(value));
  }
  return query.toString();
}
// ─── invoke 封装（realAgent 专用）────────────────────────────────────────────

async function agentFetch<T>(
  method: string,
  path: string,
  body?: object,
  token?: string,
  timeoutSecs?: number,
): Promise<T> {
  // 序列化原始 body 用于实际请求；脱敏版本仅用于开发日志，不发送给 Agent
  const bodyStr = body ? JSON.stringify(body) : undefined;
  if (import.meta.env.DEV) {
    // 脱敏：日志中不打印 password 明文；不打印 token 值
    console.debug(`[agent] ${method} ${path}`, body ? sanitize(body) : "");
  }

  let result: [number, string];
  try {
    result = await invoke<[number, string]>("agent_request", {
      method,
      path,
      port: AGENT_PORT,
      body: bodyStr,
      token: token ?? getAgentAuthToken() ?? undefined,
      timeoutSecs: timeoutSecs ?? null,
    });
  } catch {
    throw { type: "agent_unreachable" } satisfies AppError;
  }

  const [status, text] = result;
  if (status === 401) throw { type: "unauthorized" } satisfies AppError;
  if (status >= 400) {
    throw {
      type: "api_error",
      status,
      message: getApiErrorMessage(text, status >= 500 ? "服务异常" : "请求失败"),
    } satisfies AppError;
  }

  return (text ? JSON.parse(text) : {}) as T;
}

// ─── realAgent 实现 ───────────────────────────────────────────────────────────

function makeRealAgent(): AgentClient {
  return {
    getBootstrapStatus: () =>
      agentFetch<{ in_bootstrap: boolean }>("GET", "/bootstrap/status"),

    getRuntimeConfig: () =>
      agentFetch<{ backend_base_url: string }>("GET", "/bootstrap/runtime-config"),

    putBootstrapConfig: (cfg) =>
      agentFetch<void>("PUT", "/bootstrap/config", cfg),

    postBootstrapTestConnection: (req) =>
      agentFetch<{ success: boolean; message?: string }>(
        "POST", "/bootstrap/test-connection", req
      ),

    postBootstrapFinish: () =>
      agentFetch<void>("POST", "/bootstrap/finish"),

    getEdgeBootstrapStatus: () =>
      agentFetch<EdgeBootstrapStatus>("GET", "/bootstrap/edge/status"),

    registerEdge: (req) =>
      agentFetch<{ nodeUuid: string; status: string }>(
        "POST",
        "/bootstrap/edge/register",
        req,
        undefined,
        30,
      ),

    agentLogin: (u, p) =>
      agentFetch<{ token: string; roles: string[] }>("POST", "/auth/login", {
        username: u,
        password: p,
      }),

    getViewerStatus: () =>
      agentFetch<{ agent: string }>("GET", "/viewer/status"),

    getLocalDevices: () =>
      agentFetch<EdgeLocalDevices>("GET", "/local/devices"),

    getLocalCollector: () =>
      agentFetch<EdgeProcessStatus>("GET", "/local/collector"),

    getLocalOutbox: () =>
      agentFetch<EdgeOutboxStatus>("GET", "/local/outbox"),

    getLocalUploader: () =>
      agentFetch<EdgeProcessStatus & { outbox: EdgeOutboxStatus }>("GET", "/local/uploader"),

    getLocalStorage: () =>
      agentFetch<EdgeProcessStatus & { storage: EdgeStorageStatus }>("GET", "/local/storage"),

    getLocalSettings: () =>
      agentFetch<EdgeLocalSettings>("GET", "/local/settings"),

    updateLocalSettings: (settings) =>
      agentFetch<EdgeLocalSettings>("PUT", "/local/settings", settings),
    getLocalMonitorDevices: (query) =>
      agentFetch<PaginatedResult<DeviceListItem>>("GET", `/local/monitor/devices?${toAgentQuery(query)}`),

    getLocalMonitorPoints: (query) =>
      agentFetch<PaginatedResult<PointListItem>>("GET", `/local/monitor/points?${toAgentQuery(query)}`),

    getLocalRealtimeGroups: (deviceCode) =>
      agentFetch<RealtimePointGroup[]>("GET", `/local/monitor/realtime/groups?${toAgentQuery({ deviceCode })}`),

    getLocalRealtime: (deviceId, groupCode) =>
      agentFetch<DeviceRealtimeStatus>("GET", `/local/monitor/realtime?${toAgentQuery({ deviceId, groupCode })}`),

    getLocalRealtimeLatest: (deviceCode, groupCode) =>
      agentFetch<{ values: Record<string, RealtimeWireValue>; deviceOffline: boolean }>(
        "GET",
        `/local/monitor/realtime/latest?${toAgentQuery({ deviceCode, groupCode })}`,
      ),

    getLocalHistoryDevices: () =>
      agentFetch<HistoryDeviceItem[]>("GET", "/local/monitor/history/devices"),

    getLocalHistoryDeviceTypes: () =>
      agentFetch<DeviceTypeItem[]>("GET", "/local/monitor/history/device-types"),

    getLocalHistoryPoints: (deviceId) =>
      agentFetch<HistoryPointItem[]>("GET", `/local/monitor/history/points?${toAgentQuery({ deviceId })}`),

    getLocalHistoryChart: (query) =>
      agentFetch<HistoryChartResponse>("POST", "/local/monitor/history/chart", query),

    getLocalHistoryMatrix: (query) =>
      agentFetch<HistoryRawMatrixResponse>("POST", "/local/monitor/history/matrix", query),
    rotateEdgeIdentity: () =>
      agentFetch<{ keyVersion: number; uploaderRestarted: boolean; warning?: string }>(
        "POST",
        "/local/identity/rotate",
      ),

    getConnectionHealth: () =>
      agentFetch<ConnectionHealth>("GET", "/viewer/health"),

    getProcessMetrics: (name) =>
      agentFetch<ModuleStatus>("GET", `/viewer/processes/${name}/metrics`),

    getProcessStatus: (name) =>
      agentFetch<{ status: string }>("GET", `/viewer/processes/${name}/status`),

    startProcess:   (name) => agentFetch<void>("POST", `/admin/processes/${name}/start`),
    stopProcess:    (name) => agentFetch<void>("POST", `/admin/processes/${name}/stop`),
    restartProcess: (name) => agentFetch<void>("POST", `/admin/processes/${name}/restart`),

    getConfig: () =>
      agentFetch<ConnectionConfig>("GET", "/admin/config"),

    putConfig: (cfg) =>
      agentFetch<void>("PUT", "/admin/config", cfg),

    checkHealth: (target, config) =>
      agentFetch<{ success: boolean; message?: string }>(
        "POST", "/admin/health/check", config ? { target, config } : { target }
      ),

    listDrivers: async () => {
      const data = await agentFetch<{ drivers: AgentDriverInfo[] }>("GET", "/admin/drivers");
      return data.drivers ?? [];
    },

    installDriver: (packageBase64) =>
      agentFetch<{ driver: AgentDriverInfo }>(
        "POST",
        "/admin/drivers",
        { package_base64: packageBase64 },
        undefined,
        180,
      ),

    uninstallDriver: (driverId) =>
      agentFetch<{ driver: { id: string; status: string } }>(
        "DELETE",
        `/admin/drivers/${encodeURIComponent(driverId)}`,
        undefined,
        undefined,
        120,
      ),
  };
}

const realAgent: AgentClient = makeRealAgent();

// ─── mock / real 选择（动态 import 确保 mock-agent 不进入生产包）─────────────

const _useMock =
  import.meta.env.MODE !== "production" &&
  import.meta.env.VITE_USE_MOCK === "true";

// 生产构建：_useMock 被 Vite 替换为 false，if 分支成为死代码被 Rollup 消除，
// import("./mock-agent") 不会出现在生产 dist 中。
let _implPromise: Promise<AgentClient>;
if (_useMock) {
  _implPromise = import("./mock-agent").then((m) => m.mockAgent);
} else {
  _implPromise = Promise.resolve(realAgent);
}

// 代理转发：对业务层完全透明，调用方无需关心 mock/real 切换
async function via<T>(fn: (c: AgentClient) => Promise<T>): Promise<T> {
  return fn(await _implPromise);
}

export const agentClient: AgentClient = {
  getBootstrapStatus:          ()        => via((c) => c.getBootstrapStatus()),
  getRuntimeConfig:            ()        => via((c) => c.getRuntimeConfig()),
  putBootstrapConfig:          (cfg)     => via((c) => c.putBootstrapConfig(cfg)),
  postBootstrapTestConnection: (req)     => via((c) => c.postBootstrapTestConnection(req)),
  postBootstrapFinish:         ()        => via((c) => c.postBootstrapFinish()),
  getEdgeBootstrapStatus:      ()        => via((c) => c.getEdgeBootstrapStatus()),
  registerEdge:                (req)     => via((c) => c.registerEdge(req)),
  agentLogin:                  (u, p)    => via((c) => c.agentLogin(u, p)),
  getViewerStatus:             ()        => via((c) => c.getViewerStatus()),
  getLocalDevices:             ()        => via((c) => c.getLocalDevices()),
  getLocalCollector:           ()        => via((c) => c.getLocalCollector()),
  getLocalOutbox:              ()        => via((c) => c.getLocalOutbox()),
  getLocalUploader:            ()        => via((c) => c.getLocalUploader()),
  getLocalStorage:             ()        => via((c) => c.getLocalStorage()),
  getLocalSettings:             ()        => via((c) => c.getLocalSettings()),
  updateLocalSettings:          (settings) => via((c) => c.updateLocalSettings(settings)),
  getLocalMonitorDevices:       (query)    => via((c) => c.getLocalMonitorDevices(query)),
  getLocalMonitorPoints:        (query)    => via((c) => c.getLocalMonitorPoints(query)),
  getLocalRealtimeGroups:       (code)     => via((c) => c.getLocalRealtimeGroups(code)),
  getLocalRealtime:             (id, group) => via((c) => c.getLocalRealtime(id, group)),
  getLocalRealtimeLatest:       (code, group) => via((c) => c.getLocalRealtimeLatest(code, group)),
  getLocalHistoryDevices:       ()         => via((c) => c.getLocalHistoryDevices()),
  getLocalHistoryDeviceTypes:   ()         => via((c) => c.getLocalHistoryDeviceTypes()),
  getLocalHistoryPoints:        (id)       => via((c) => c.getLocalHistoryPoints(id)),
  getLocalHistoryChart:         (query)    => via((c) => c.getLocalHistoryChart(query)),
  getLocalHistoryMatrix:        (query)    => via((c) => c.getLocalHistoryMatrix(query)),
  rotateEdgeIdentity:           ()         => via((c) => c.rotateEdgeIdentity()),
  getConnectionHealth:         ()        => via((c) => c.getConnectionHealth()),
  getProcessMetrics:           (name)    => via((c) => c.getProcessMetrics(name)),
  getProcessStatus:            (name)    => via((c) => c.getProcessStatus(name)),
  startProcess:                (name)    => via((c) => c.startProcess(name)),
  stopProcess:                 (name)    => via((c) => c.stopProcess(name)),
  restartProcess:              (name)    => via((c) => c.restartProcess(name)),
  getConfig:                   ()        => via((c) => c.getConfig()),
  putConfig:                   (cfg)     => via((c) => c.putConfig(cfg)),
  checkHealth:                 (target, config) => via((c) => c.checkHealth(target, config)),
  listDrivers:                 ()        => via((c) => c.listDrivers()),
  installDriver:               (b64)     => via((c) => c.installDriver(b64)),
  uninstallDriver:             (id)      => via((c) => c.uninstallDriver(id)),
};
