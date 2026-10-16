import { invokeHttp, isAbortError } from "@/lib/desktop-request";
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
  aliases?: string[];
  source?: string;
  protected?: boolean;
  status: string;
  install_status?: string;
  runtime_status?: string;
  instance?: { role: string; deployment_role: string; build_id: string; boot_id: string; pid: number; data_root: string };
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
  getViewerStatus(signal?: AbortSignal):       Promise<{ agent: string }>;
  getLocalDevices(signal?: AbortSignal):      Promise<EdgeLocalDevices>;
  getLocalCollector(signal?: AbortSignal):    Promise<EdgeProcessStatus>;
  getLocalOutbox(signal?: AbortSignal):       Promise<EdgeOutboxStatus>;
  getLocalUploader(signal?: AbortSignal):     Promise<EdgeProcessStatus & { outbox: EdgeOutboxStatus }>;
  getLocalStorage(signal?: AbortSignal):      Promise<EdgeProcessStatus & { storage: EdgeStorageStatus }>;
  getLocalSettings(signal?: AbortSignal):     Promise<EdgeLocalSettings>;
  updateLocalSettings(settings: EdgeLocalSettingsUpdate): Promise<EdgeLocalSettings>;
  getLocalMonitorScope(signal?: AbortSignal): Promise<Pick<DeviceListItem, "id" | "deviceCode" | "deviceType">[]>;
  getLocalMonitorDevices(query: DeviceListQuery, signal?: AbortSignal): Promise<PaginatedResult<DeviceListItem>>;
  getLocalMonitorPoints(query: PointListQuery, signal?: AbortSignal): Promise<PaginatedResult<PointListItem>>;
  getLocalRealtimeGroups(deviceCode: string, signal?: AbortSignal): Promise<RealtimePointGroup[]>;
  getLocalRealtime(deviceId: number, groupCode: string, signal?: AbortSignal): Promise<DeviceRealtimeStatus>;
  getLocalRealtimeLatest(deviceCode: string, groupCode: string, signal?: AbortSignal): Promise<{
    values: Record<string, RealtimeWireValue>;
    deviceOffline: boolean;
  }>;
  getLocalHistoryDevices(signal?: AbortSignal): Promise<HistoryDeviceItem[]>;
  getLocalHistoryDeviceTypes(signal?: AbortSignal): Promise<DeviceTypeItem[]>;
  getLocalHistoryPoints(deviceId: number, signal?: AbortSignal): Promise<HistoryPointItem[]>;
  getLocalHistoryChart(query: HistoryQuery, signal?: AbortSignal): Promise<HistoryChartResponse>;
  getLocalHistoryMatrix(query: HistoryQuery & {
    alignGranularity?: "100ms" | "500ms" | "1s";
    pageSize?: number;
    cursorTs?: string;
  }, signal?: AbortSignal): Promise<HistoryRawMatrixResponse>;
  rotateEdgeIdentity():     Promise<{ keyVersion: number; uploaderRestarted: boolean; warning?: string }>;
  getConnectionHealth(signal?: AbortSignal):    Promise<ConnectionHealth>;
  getProcessMetrics(name: ModuleName, signal?: AbortSignal): Promise<ModuleStatus>;
  getProcessStatus(name: ModuleName, signal?: AbortSignal): Promise<{ status: string }>;
  startProcess(name: ModuleName): Promise<void>;
  stopProcess(name: ModuleName): Promise<void>;
  restartProcess(name: ModuleName): Promise<void>;
  getConfig(signal?: AbortSignal):   Promise<ConnectionConfig>;
  putConfig(cfg: Partial<ConnectionConfig>): Promise<void>;
  checkHealth(
    target: "redis" | "mysql" | "tdengine",
    config?: Partial<DbConfig>
  ): Promise<{ success: boolean; message?: string }>;
  listDrivers(signal?: AbortSignal): Promise<AgentDriverInfo[]>;
  previewDriver(input: { package_base64?: string; restore_id?: string }): Promise<{ driver: { id: string; name: string; version: string; sha256: string } }>;
  restoreDriver(driverId: string): Promise<{ driver: AgentDriverInfo }>;
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
  signal?: AbortSignal,
): Promise<T> {
  // 序列化原始 body 用于实际请求；脱敏版本仅用于开发日志，不发送给 Agent
  const bodyStr = body ? JSON.stringify(body) : undefined;
  if (import.meta.env.DEV) {
    // 脱敏：日志中不打印 password 明文；不打印 token 值
    console.debug(`[agent] ${method} ${path}`, body ? sanitize(body) : "");
  }

  let result: [number, string];
  try {
    result = await invokeHttp<[number, string]>("agent_request", {
      method,
      path,
      port: AGENT_PORT,
      body: bodyStr,
      token: token ?? getAgentAuthToken() ?? undefined,
      timeoutSecs: timeoutSecs ?? null,
    }, signal);
  } catch (error) {
    if (isAbortError(error)) throw error;
    const message = String(error);
    if (message.includes("agent_request_too_large") || message.includes("response_body_too_large")) {
      throw { type: "api_error", status: 413, message: "请求或响应数据过大，请缩小数据范围或驱动包" } satisfies AppError;
    }
    if (message.includes("agent_response_invalid_utf8")) {
      throw { type: "api_error", status: 502, message: "Agent 返回的数据格式无效" } satisfies AppError;
    }
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

    getViewerStatus: (signal) =>
      agentFetch<{ agent: string }>("GET", "/viewer/status", undefined, undefined, undefined, signal),

    getLocalDevices: (signal) =>
      agentFetch<EdgeLocalDevices>("GET", "/local/devices", undefined, undefined, undefined, signal),

    getLocalCollector: (signal) =>
      agentFetch<EdgeProcessStatus>("GET", "/local/collector", undefined, undefined, undefined, signal),

    getLocalOutbox: (signal) =>
      agentFetch<EdgeOutboxStatus>("GET", "/local/outbox", undefined, undefined, undefined, signal),

    getLocalUploader: (signal) =>
      agentFetch<EdgeProcessStatus & { outbox: EdgeOutboxStatus }>("GET", "/local/uploader", undefined, undefined, undefined, signal),

    getLocalStorage: (signal) =>
      agentFetch<EdgeProcessStatus & { storage: EdgeStorageStatus }>("GET", "/local/storage", undefined, undefined, undefined, signal),

    getLocalSettings: (signal) =>
      agentFetch<EdgeLocalSettings>("GET", "/local/settings", undefined, undefined, undefined, signal),

    updateLocalSettings: (settings) =>
      agentFetch<EdgeLocalSettings>("PUT", "/local/settings", settings),
    getLocalMonitorScope: (signal) =>
      agentFetch<Pick<DeviceListItem, "id" | "deviceCode" | "deviceType">[]>("GET", "/local/monitor/device-scope", undefined, undefined, undefined, signal),
    getLocalMonitorDevices: (query, signal) =>
      agentFetch<PaginatedResult<DeviceListItem>>("GET", `/local/monitor/devices?${toAgentQuery(query)}`, undefined, undefined, undefined, signal),

    getLocalMonitorPoints: (query, signal) =>
      agentFetch<PaginatedResult<PointListItem>>("GET", `/local/monitor/points?${toAgentQuery(query)}`, undefined, undefined, undefined, signal),

    getLocalRealtimeGroups: (deviceCode, signal) =>
      agentFetch<RealtimePointGroup[]>("GET", `/local/monitor/realtime/groups?${toAgentQuery({ deviceCode })}`, undefined, undefined, undefined, signal),

    getLocalRealtime: (deviceId, groupCode, signal) =>
      agentFetch<DeviceRealtimeStatus>("GET", `/local/monitor/realtime?${toAgentQuery({ deviceId, groupCode })}`, undefined, undefined, undefined, signal),

    getLocalRealtimeLatest: (deviceCode, groupCode, signal) =>
      agentFetch<{ values: Record<string, RealtimeWireValue>; deviceOffline: boolean }>(
        "GET",
        `/local/monitor/realtime/latest?${toAgentQuery({ deviceCode, groupCode })}`, undefined, undefined, undefined, signal,
      ),

    getLocalHistoryDevices: (signal) =>
      agentFetch<HistoryDeviceItem[]>("GET", "/local/monitor/history/devices", undefined, undefined, undefined, signal),

    getLocalHistoryDeviceTypes: (signal) =>
      agentFetch<DeviceTypeItem[]>("GET", "/local/monitor/history/device-types", undefined, undefined, undefined, signal),

    getLocalHistoryPoints: (deviceId, signal) =>
      agentFetch<HistoryPointItem[]>("GET", `/local/monitor/history/points?${toAgentQuery({ deviceId })}`, undefined, undefined, undefined, signal),

    getLocalHistoryChart: (query, signal) =>
      agentFetch<HistoryChartResponse>("POST", "/local/monitor/history/chart", query, undefined, undefined, signal),

    getLocalHistoryMatrix: (query, signal) =>
      agentFetch<HistoryRawMatrixResponse>("POST", "/local/monitor/history/matrix", query, undefined, undefined, signal),
    rotateEdgeIdentity: () =>
      agentFetch<{ keyVersion: number; uploaderRestarted: boolean; warning?: string }>(
        "POST",
        "/local/identity/rotate",
      ),

    getConnectionHealth: (signal) =>
      agentFetch<ConnectionHealth>("GET", "/viewer/health", undefined, undefined, undefined, signal),

    getProcessMetrics: (name, signal) =>
      agentFetch<ModuleStatus>("GET", `/viewer/processes/${name}/metrics`, undefined, undefined, undefined, signal),

    getProcessStatus: (name, signal) =>
      agentFetch<{ status: string }>("GET", `/viewer/processes/${name}/status`, undefined, undefined, undefined, signal),

    startProcess:   (name) => agentFetch<void>("POST", `/admin/processes/${name}/start`),
    stopProcess:    (name) => agentFetch<void>("POST", `/admin/processes/${name}/stop`, undefined, undefined, 60),
    restartProcess: (name) => agentFetch<void>("POST", `/admin/processes/${name}/restart`, undefined, undefined, 120),

    getConfig: (signal) =>
      agentFetch<ConnectionConfig>("GET", "/admin/config", undefined, undefined, undefined, signal),

    putConfig: (cfg) =>
      agentFetch<void>("PUT", "/admin/config", cfg),

    checkHealth: (target, config) =>
      agentFetch<{ success: boolean; message?: string }>(
        "POST", "/admin/health/check", config ? { target, config } : { target }
      ),

    listDrivers: async (signal) => {
      const data = await agentFetch<{ drivers: AgentDriverInfo[] }>("GET", "/admin/drivers", undefined, undefined, undefined, signal);
      return data.drivers ?? [];
    },

    previewDriver: (input) => agentFetch("POST", "/admin/drivers/preview", input, undefined, 180),

    restoreDriver: (driverId) =>
      agentFetch<{ driver: AgentDriverInfo }>("POST", `/admin/drivers/${encodeURIComponent(driverId)}/restore`, undefined, undefined, 180),

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
  getViewerStatus:             (signal)        => via((c) => c.getViewerStatus(signal)),
  getLocalDevices:             (signal)        => via((c) => c.getLocalDevices(signal)),
  getLocalCollector:           (signal)        => via((c) => c.getLocalCollector(signal)),
  getLocalOutbox:              (signal)        => via((c) => c.getLocalOutbox(signal)),
  getLocalUploader:            (signal)        => via((c) => c.getLocalUploader(signal)),
  getLocalStorage:             (signal)        => via((c) => c.getLocalStorage(signal)),
  getLocalSettings:             (signal)        => via((c) => c.getLocalSettings(signal)),
  updateLocalSettings:          (settings) => via((c) => c.updateLocalSettings(settings)),
  getLocalMonitorScope:         (signal)   => via((c) => c.getLocalMonitorScope(signal)),
  getLocalMonitorDevices:       (query, signal)    => via((c) => c.getLocalMonitorDevices(query, signal)),
  getLocalMonitorPoints:        (query, signal)    => via((c) => c.getLocalMonitorPoints(query, signal)),
  getLocalRealtimeGroups:       (code, signal)     => via((c) => c.getLocalRealtimeGroups(code, signal)),
  getLocalRealtime:             (id, group, signal) => via((c) => c.getLocalRealtime(id, group, signal)),
  getLocalRealtimeLatest:       (code, group, signal) => via((c) => c.getLocalRealtimeLatest(code, group, signal)),
  getLocalHistoryDevices:       (signal)         => via((c) => c.getLocalHistoryDevices(signal)),
  getLocalHistoryDeviceTypes:   (signal)         => via((c) => c.getLocalHistoryDeviceTypes(signal)),
  getLocalHistoryPoints:        (id, signal)       => via((c) => c.getLocalHistoryPoints(id, signal)),
  getLocalHistoryChart:         (query, signal)    => via((c) => c.getLocalHistoryChart(query, signal)),
  getLocalHistoryMatrix:        (query, signal)    => via((c) => c.getLocalHistoryMatrix(query, signal)),
  rotateEdgeIdentity:           ()         => via((c) => c.rotateEdgeIdentity()),
  getConnectionHealth:         (signal)        => via((c) => c.getConnectionHealth(signal)),
  getProcessMetrics:           (name, signal)    => via((c) => c.getProcessMetrics(name, signal)),
  getProcessStatus:            (name, signal)    => via((c) => c.getProcessStatus(name, signal)),
  startProcess:                (name)    => via((c) => c.startProcess(name)),
  stopProcess:                 (name)    => via((c) => c.stopProcess(name)),
  restartProcess:              (name)    => via((c) => c.restartProcess(name)),
  getConfig:                   (signal)        => via((c) => c.getConfig(signal)),
  putConfig:                   (cfg)     => via((c) => c.putConfig(cfg)),
  checkHealth:                 (target, config) => via((c) => c.checkHealth(target, config)),
  listDrivers:                 (signal)        => via((c) => c.listDrivers(signal)),
  previewDriver:               (input)   => via((c) => c.previewDriver(input)),
  restoreDriver:               (id)      => via((c) => c.restoreDriver(id)),
  installDriver:               (b64)     => via((c) => c.installDriver(b64)),
  uninstallDriver:             (id)      => via((c) => c.uninstallDriver(id)),
};
