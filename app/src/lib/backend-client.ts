import { invoke } from "@tauri-apps/api/core";
import type {
  AppError,
  AuthUserInfo,
  CollectorConfig,
  CollectorModuleMetrics,
  DashboardConnectionHealth,
  DashboardEdgePipelineHealth,
  DashboardDeviceTypeDistribution,
  DashboardOverview,
  DashboardRecentEventList,
  DashboardTrend,
  ModuleRates,
  WriterLogRow,
  WriterModuleMetrics,
} from "@/types";
import type {
  DeviceForm,
  DeviceGroupTreeNode,
  DeviceListItem,
  DeviceListQuery,
  DeviceRealtimeStatus,
  DeviceTypeItem,
  RealtimePointGroup,
  DriverListItem,
  HistoryChartResponse,
  HistoryDeviceItem,
  HistoryExportPayload,
  HistoryExportStatus,
  HistoryPointItem,
  HistoryQuery,
  HistoryRawMatrixResponse,
  ImportResult,
  PaginatedResult,
  PointDeviceTreeNode,
  PointForm,
  PointListItem,
  PointListQuery,
} from "@/types/device-monitor";
import type {
  LogCleanupPayload,
  LogCleanupResult,
  SystemLogPageResult,
  SystemLogQuery,
  SystemLogStats,
  TdengineLogFile,
  TdengineLogTail,
} from "@/types/log";
import type {
  MenuPayload,
  MenuTreeItem,
  PermissionItem,
  PermissionPayload,
  RoleGrant,
  RoleOption,
  RolePageResult,
  RolePayload,
  RoleQuery,
  UserPageResult,
  UserPayload,
  UserQuery,
} from "@/types/system-management";
import type {
  AssignmentOccupancy,
  AssignmentUpdateResult,
  DeviceStatusSyncResult,
  DistributedNode,
  NodeAssignment,
  RegistrationCodeResult,
} from "@/types/distributed";
import {
  isAllowedBackendMethod,
  isAllowedBackendPath,
  MAX_BACKEND_REQUEST_BYTES,
  normalizeBackendOrigin,
  requestBodyByteLength,
} from "@/lib/backend-proxy-policy";
import { stripClientPassword } from "@/lib/password-contract";
import { assertImportRequestSize } from "@/lib/import-workbook";

// ─── 接口 ─────────────────────────────────────────────────────────────────────

/** /log/list 查询参数（camelCase，与 backend 路由对齐） */
export interface LogListQuery {
  serviceName?: string;
  kind?: string;
  type?: string;
  pageNo?: number;
  pageSize?: number;
  level?: string;
  source?: string;
  startTime?: string;
  endTime?: string;
}

export interface UserPasswordProvisionResult {
  id: number;
  userId: number;
  temporaryPassword: string;
  mustChangePassword: true;
  tokenVersion: number;
}

export interface BackendClient {
  login(userName: string, password: string): Promise<{
    token: string;
    refreshToken: string;
    userId?: number;
    mustChangePassword?: boolean;
  }>;
  /** 查询 DB system_log（需 backend token 鉴权），返回行数组 */
  logList(token: string, query: LogListQuery): Promise<WriterLogRow[]>;
  /** 采集页一期即时指标 */
  collectorMetrics(token: string): Promise<CollectorModuleMetrics>;
  /** 入库页一期即时指标 */
  writerMetrics(token: string): Promise<WriterModuleMetrics>;
  /** 二期：采集/入库速率与趋势（同源，一个接口出两侧） */
  moduleRates(token: string): Promise<ModuleRates>;
  /** collector 运行参数配置（stream_maxlen 等，存 MySQL collector_setting） */
  getCollectorConfig(token: string): Promise<CollectorConfig>;
  updateCollectorConfig(token: string, streamMaxlen: number): Promise<CollectorConfig>;
  dashboardOverview(token: string): Promise<DashboardOverview>;
  dashboardTrend(token: string, rangeType: "today" | "last24h" | "last7d"): Promise<DashboardTrend>;
  dashboardDeviceTypeDistribution(token: string): Promise<DashboardDeviceTypeDistribution>;
  dashboardConnectionHealth(token: string): Promise<DashboardConnectionHealth>;
  dashboardEdgePipelineHealth(token: string): Promise<DashboardEdgePipelineHealth>;
  dashboardRecentEvents(token: string): Promise<DashboardRecentEventList>;
  distributedNodes(token: string): Promise<DistributedNode[]>;
  syncDistributedNodeDeviceStatuses(token: string): Promise<DeviceStatusSyncResult>;
  createNodeRegistrationCode(token: string, nodeName: string, expiresMinutes: number): Promise<RegistrationCodeResult>;
  updateDistributedNodeStatus(token: string, nodeUuid: string, status: "approved" | "revoked"): Promise<unknown>;
  updateDistributedNodeSettings(token: string, nodeUuid: string, readTimeoutMs: number | null): Promise<unknown>;
  distributedNodeAssignments(token: string, nodeUuid: string): Promise<NodeAssignment[]>;
  distributedAssignmentOccupancy(token: string): Promise<AssignmentOccupancy[]>;
  assignDistributedNodeDevices(token: string, nodeUuid: string, deviceIds: number[]): Promise<AssignmentUpdateResult>;
  driverList(token: string, query: { pageNo?: number; pageSize?: number; keyword?: string }): Promise<PaginatedResult<DriverListItem>>;
  driverOptions(token: string): Promise<{ driverName: string; driverDesc: string }[]>;
  registerDriver(
    token: string,
    payload: {
      protocol: string;
      driverId?: string;
      description?: string;
      name?: string;
      port?: number;
      version?: string;
      sha256?: string;
    },
  ): Promise<{ id: number; protocol: string; description: string; created: boolean; driverId?: string }>;
  uninstallDriver(
    token: string,
    payload: { protocol: string; driverId?: string },
  ): Promise<{ protocol: string; status: string; catalogDeleted?: boolean; driverId?: string }>;
  deviceGroupTree(token: string): Promise<DeviceGroupTreeNode[]>;
  deviceList(token: string, query: DeviceListQuery): Promise<PaginatedResult<DeviceListItem>>;
  createDevice(token: string, payload: DeviceForm): Promise<{ id: number }>;
  updateDevice(token: string, id: number, payload: DeviceForm): Promise<unknown>;
  updateDeviceStatus(token: string, id: number, enabled: boolean): Promise<unknown>;
  deleteDevice(token: string, id: number): Promise<unknown>;
  batchDeleteDevices(token: string, ids: number[]): Promise<{ count: number }>;
  exportDevices(token: string, query: DeviceListQuery, template?: boolean): Promise<{ bytes: number[]; fileName: string }>;
  importDevices(token: string, rows: Record<string, unknown>[]): Promise<ImportResult>;
  pointDeviceTree(token: string): Promise<PointDeviceTreeNode[]>;
  pointList(token: string, query: PointListQuery): Promise<PaginatedResult<PointListItem>>;
  createPoint(token: string, payload: PointForm): Promise<{ id: number }>;
  updatePoint(token: string, id: number, payload: PointForm): Promise<unknown>;
  updatePointStatus(token: string, id: number, enabled: boolean, locator?: { deviceId?: number; varSeq?: number }): Promise<unknown>;
  deletePoint(token: string, id: number): Promise<unknown>;
  batchDeletePoints(token: string, ids: number[]): Promise<{ count: number }>;
  exportPoints(token: string, query: PointListQuery, template?: boolean): Promise<{ bytes: number[]; fileName: string }>;
  importPoints(token: string, rows: Record<string, unknown>[]): Promise<ImportResult>;
  importPointsFromTemplate(token: string, deviceId: number): Promise<ImportResult>;
  realtimeInit(token: string, deviceId: number, groupCode?: string): Promise<DeviceRealtimeStatus>;
  realtimePointGroups(token: string, deviceCode: string): Promise<RealtimePointGroup[]>;
  historyDevices(token: string): Promise<HistoryDeviceItem[]>;
  historyDeviceTypes(token: string): Promise<DeviceTypeItem[]>;
  historyPoints(token: string, deviceId: number): Promise<HistoryPointItem[]>;
  historyChart(token: string, query: HistoryQuery): Promise<HistoryChartResponse>;
  historyRawMatrix(token: string, query: HistoryQuery & { alignGranularity?: "100ms" | "500ms" | "1s"; pageSize?: number; cursorTs?: string }): Promise<HistoryRawMatrixResponse>;
  historyExport(token: string, payload: HistoryExportPayload): Promise<{ taskId: string }>;
  historyExportStatus(token: string, taskId: string): Promise<HistoryExportStatus>;
  historyExportDownload(token: string, taskId: string): Promise<{ bytes: number[]; fileName: string }>;
  systemLogList(token: string, query: SystemLogQuery): Promise<SystemLogPageResult>;
  logStats(token: string): Promise<SystemLogStats>;
  toggleLogDebug(token: string, payload: { enabled: boolean; durationMinutes?: number; reason?: string }): Promise<unknown>;
  cleanupLogs(token: string, payload: LogCleanupPayload): Promise<LogCleanupResult>;
  tdengineLogFiles(token: string): Promise<TdengineLogFile[]>;
  tdengineLogTail(token: string, file: string, lines?: number): Promise<TdengineLogTail>;
  userList(token: string, query: UserQuery): Promise<UserPageResult>;
  /** 当前登录用户信息（角色 / 按钮权限） */
  userInfo(token: string): Promise<AuthUserInfo>;
  roleOptions(token: string): Promise<RoleOption[]>;
  createUser(token: string, payload: UserPayload): Promise<UserPasswordProvisionResult>;
  updateUser(token: string, id: number, payload: UserPayload): Promise<{ id: number }>;
  updateUserStatus(token: string, id: number, status: string): Promise<{ id: number }>;
  resetUserPassword(token: string, id: number): Promise<UserPasswordProvisionResult>;
  changePassword(token: string, oldPassword: string, newPassword: string): Promise<boolean>;
  deleteUser(token: string, id: number): Promise<{ id: number }>;
  roleList(token: string, query: RoleQuery): Promise<RolePageResult>;
  createRole(token: string, payload: RolePayload): Promise<{ id: number }>;
  updateRole(token: string, id: number, payload: RolePayload): Promise<{ id: number }>;
  updateRoleStatus(token: string, id: number, enabled: boolean): Promise<{ id: number }>;
  deleteRole(token: string, id: number): Promise<{ id: number }>;
  roleGrant(token: string, id: number): Promise<RoleGrant>;
  updateRoleGrant(token: string, id: number, payload: RoleGrant): Promise<{ id: number }>;
  adminMenus(token: string): Promise<MenuTreeItem[]>;
  /** 当前用户可见菜单树（已按角色过滤） */
  currentUserMenus(token: string): Promise<MenuTreeItem[]>;
  createMenu(token: string, payload: MenuPayload): Promise<{ id: number }>;
  updateMenu(token: string, id: number, payload: MenuPayload): Promise<{ id: number }>;
  deleteMenu(token: string, id: number): Promise<{ id: number }>;
  menuPermissions(token: string, menuId: number): Promise<PermissionItem[]>;
  createMenuPermission(token: string, menuId: number, payload: PermissionPayload): Promise<{ id: number }>;
  updateMenuPermission(token: string, menuId: number, permissionId: number, payload: PermissionPayload): Promise<{ id: number }>;
  deleteMenuPermission(token: string, menuId: number, permissionId: number): Promise<{ id: number }>;
}

// ─── 运行期后端地址 ───────────────────────────────────────────────────────────
// 不再依赖打包时的 VITE_BACKEND_URL；app 启动时经 Agent /bootstrap/runtime-config
// 读取后端地址并注入。这样同一安装包可用于不同目标机，无需重新打包。

let _backendBaseUrl = import.meta.hot?.data.backendBaseUrl ?? "";
let _backendOriginReady: Promise<void> = Promise.resolve();

if (import.meta.hot) {
  import.meta.hot.dispose((data) => {
    data.backendBaseUrl = _backendBaseUrl;
  });
}

export function setBackendBaseUrl(url: string) {
  const normalized = normalizeBackendOrigin(url || "");
  _backendBaseUrl = normalized ?? "";
  if (!normalized) {
    _backendOriginReady = Promise.resolve();
    return;
  }
  _backendOriginReady = invoke<void>("set_backend_origin", { baseUrl: normalized });
  // Keep the rejection for the next request, while avoiding an unhandled rejection
  // when startup never reaches a backend call.
  void _backendOriginReady.catch(() => undefined);
}

export function getBackendWebSocketUrl(path = "/api/device-monitor/ws/realtime-push") {
  if (!_backendBaseUrl) {
    throw { type: "network_error", message: "后端地址未配置" } satisfies AppError;
  }
  const base = _backendBaseUrl.replace(/^http:/, "ws:").replace(/^https:/, "wss:");
  return `${base}${path.startsWith("/") ? path : `/${path}`}`;
}

// ─── fetch 封装（realBackend 专用）───────────────────────────────────────────

async function backendFetch<T>(
  method: string,
  path: string,
  body?: object,
  token?: string
): Promise<T> {
  if (!_backendBaseUrl) {
    // 地址未就绪（运行期配置尚未注入或引导未完成）
    throw { type: "network_error", message: "后端地址未配置" } satisfies AppError;
  }
  // backend 登录返回的 token 已带 "Bearer " 前缀，规范化避免出现 "Bearer Bearer ..."
  const authorization = token
    ? (token.startsWith("Bearer ") ? token : `Bearer ${token}`)
    : undefined;

  let result: [number, string];
  try {
    await _backendOriginReady;
    if (!isAllowedBackendMethod(method) || !isAllowedBackendPath(path)) {
      throw new Error("backend request is not allowed");
    }
    const serializedBody = body === undefined ? undefined : JSON.stringify(body);
    if (requestBodyByteLength(serializedBody) > MAX_BACKEND_REQUEST_BYTES) {
      throw new Error("backend request body is too large");
    }
    // 经 Tauri Rust 转发，桌面 app 不再受浏览器 CORS 限制。
    result = await invoke<[number, string]>("backend_request", {
      baseUrl: _backendBaseUrl,
      method,
      path,
      body: serializedBody,
      authorization,
    });
  } catch {
    throw {
      type: "network_error",
      message: "无法连接到后端服务",
    } satisfies AppError;
  }
  const [status, text] = result;
  if (status === 401) throw { type: "unauthorized" } satisfies AppError;
  let json: { code?: number; msg?: string; data?: T };
  try {
    json = JSON.parse(text) as { code?: number; msg?: string; data?: T };
  } catch {
    if (status >= 400) {
      throw {
        type: "api_error",
        status,
        message: "后端服务异常",
      } satisfies AppError;
    }
    throw {
      type: "api_error",
      status,
      message: "后端响应格式异常",
    } satisfies AppError;
  }
  if (json.code === 401) throw { type: "unauthorized" } satisfies AppError;
  if (status >= 400) {
    throw {
      type: "api_error",
      status: json.code ?? status,
      message: json.msg || "后端服务异常",
    } satisfies AppError;
  }
  if (json.code !== undefined && json.code !== 200) {
    throw {
      type: "api_error",
      status: json.code,
      message: json.msg || "请求失败",
    } satisfies AppError;
  }
  return json.data as T; // 后端统一格式：{ code, msg, data }
}

async function backendDownload(path: string, token: string) {
  if (!_backendBaseUrl) {
    throw { type: "network_error", message: "后端地址未配置" } satisfies AppError;
  }
  const authorization = token.startsWith("Bearer ") ? token : `Bearer ${token}`;
  let result: [number, number[], string | null];
  try {
    await _backendOriginReady;
    if (!isAllowedBackendPath(path)) throw new Error("backend download path is not allowed");
    result = await invoke<[number, number[], string | null]>("backend_download", {
      baseUrl: _backendBaseUrl,
      path,
      authorization,
    });
  } catch {
    throw { type: "network_error", message: "无法连接到后端服务" } satisfies AppError;
  }

  const [status, bytes, disposition] = result;
  if (status === 401) throw { type: "unauthorized" } satisfies AppError;
  if (status >= 400) {
    throw { type: "api_error", status, message: "导出文件下载失败" } satisfies AppError;
  }

  const encodedName = disposition?.match(/filename\*=UTF-8''([^;]+)/i)?.[1];
  const plainName = disposition?.match(/filename="?([^";]+)"?/i)?.[1];
  let fileName = plainName ?? "历史数据.csv";
  if (encodedName) {
    try { fileName = decodeURIComponent(encodedName); } catch { fileName = encodedName; }
  }
  return { bytes, fileName };
}

function toQuery(params: object) {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value === undefined || value === null || value === "") continue;
    if (Array.isArray(value)) value.forEach((item) => query.append(key, String(item)));
    else query.append(key, String(value));
  }
  return query.toString();
}

// ─── realBackend 实现 ─────────────────────────────────────────────────────────

const realBackend: BackendClient = {
  login(userName, password) {
    return backendFetch<{ token: string; refreshToken: string; userId?: number; mustChangePassword?: boolean }>(
      "POST",
      "/api/auth/login",
      { userName, password }
    );
  },

  async logList(token, query) {
    const qs = new URLSearchParams();
    for (const [k, v] of Object.entries(query)) {
      if (v !== undefined && v !== null && v !== "") qs.append(k, String(v));
    }
    const page = await backendFetch<{ list: WriterLogRow[] } | null>(
      "GET",
      `/api/device-monitor/log/list?${qs.toString()}`,
      undefined,
      token
    );
    return page?.list ?? [];
  },

  collectorMetrics(token) {
    return backendFetch<CollectorModuleMetrics>(
      "GET",
      "/api/device-monitor/module-monitor/collector",
      undefined,
      token
    );
  },

  writerMetrics(token) {
    return backendFetch<WriterModuleMetrics>(
      "GET",
      "/api/device-monitor/module-monitor/writer",
      undefined,
      token
    );
  },

  moduleRates(token) {
    return backendFetch<ModuleRates>(
      "GET",
      "/api/device-monitor/module-monitor/rates",
      undefined,
      token
    );
  },
  getCollectorConfig(token) {
    return backendFetch<CollectorConfig>(
      "GET",
      "/api/device-monitor/collector-config",
      undefined,
      token
    );
  },
  updateCollectorConfig(token, streamMaxlen) {
    return backendFetch<CollectorConfig>(
      "PUT",
      "/api/device-monitor/collector-config",
      { streamMaxlen },
      token
    );
  },
  dashboardOverview(token) {
    return backendFetch<DashboardOverview>(
      "GET",
      "/api/device-monitor/dashboard/overview",
      undefined,
      token
    );
  },
  dashboardTrend(token, rangeType) {
    const qs = new URLSearchParams({ rangeType, granularity: "auto" });
    ["collectCount", "alarmCount", "onlineDeviceCount"].forEach((key) => qs.append("metricKeys", key));
    return backendFetch<DashboardTrend>(
      "GET",
      `/api/device-monitor/dashboard/trend?${qs.toString()}`,
      undefined,
      token
    );
  },
  dashboardDeviceTypeDistribution(token) {
    return backendFetch<DashboardDeviceTypeDistribution>(
      "GET",
      "/api/device-monitor/dashboard/device-type-distribution?topN=10",
      undefined,
      token
    );
  },
  dashboardConnectionHealth(token) {
    return backendFetch<DashboardConnectionHealth>(
      "GET",
      "/api/device-monitor/dashboard/device-connection-health?statRange=last24h&topN=8&includeOffline=true",
      undefined,
      token
    );
  },
  dashboardEdgePipelineHealth(token) {
    return backendFetch<DashboardEdgePipelineHealth>(
      "GET",
      "/api/device-monitor/dashboard/edge-pipeline-health",
      undefined,
      token,
    );
  },

  dashboardRecentEvents(token) {
    return backendFetch<DashboardRecentEventList>(
      "GET",
      "/api/device-monitor/dashboard/recent-events?limit=8&eventType=alarm",
      undefined,
      token
    );
  },
  async distributedNodes(token) {
    const data = await backendFetch<{ items: DistributedNode[] }>(
      "GET",
      "/api/distributed/nodes",
      undefined,
      token,
    );
    return data.items;
  },
  syncDistributedNodeDeviceStatuses(token) {
    return backendFetch<DeviceStatusSyncResult>(
      "POST",
      "/api/distributed/nodes/sync-device-status",
      undefined,
      token,
    );
  },
  createNodeRegistrationCode(token, nodeName, expiresMinutes) {
    return backendFetch<RegistrationCodeResult>(
      "POST",
      "/api/distributed/nodes/registration-codes",
      { nodeName, expiresMinutes },
      token,
    );
  },
  updateDistributedNodeStatus(token, nodeUuid, status) {
    return backendFetch(
      "PATCH",
      `/api/distributed/nodes/${nodeUuid}/status`,
      { status },
      token,
    );
  },
  updateDistributedNodeSettings(token, nodeUuid, readTimeoutMs) {
    return backendFetch(
      "PATCH",
      `/api/distributed/nodes/${nodeUuid}/settings`,
      { readTimeoutMs },
      token,
    );
  },
  async distributedNodeAssignments(token, nodeUuid) {
    const data = await backendFetch<{ items: NodeAssignment[] }>(
      "GET",
      `/api/distributed/nodes/${nodeUuid}/assignments`,
      undefined,
      token,
    );
    return data.items;
  },
  async distributedAssignmentOccupancy(token) {
    const data = await backendFetch<{ items: AssignmentOccupancy[] }>(
      "GET",
      "/api/distributed/assignments/occupancy",
      undefined,
      token,
    );
    return data.items;
  },
  assignDistributedNodeDevices(token, nodeUuid, deviceIds) {
    return backendFetch<AssignmentUpdateResult>(
      "PUT",
      `/api/distributed/nodes/${nodeUuid}/assignments`,
      { deviceIds },
      token,
    );
  },
  driverList(token, query) {
    return backendFetch("GET", `/api/device-monitor/driver/list?${toQuery(query)}`, undefined, token);
  },
  driverOptions(token) {
    return backendFetch("GET", "/api/device-monitor/driver/options", undefined, token);
  },
  registerDriver(token, payload) {
    return backendFetch("POST", "/api/device-monitor/driver/register", payload, token);
  },
  uninstallDriver(token, payload) {
    return backendFetch("POST", "/api/device-monitor/driver/uninstall", payload, token);
  },
  deviceGroupTree(token) {
    return backendFetch("GET", "/api/device-monitor/device/group-tree", undefined, token);
  },
  deviceList(token, query) {
    return backendFetch("GET", `/api/device-monitor/device/list?${toQuery(query)}`, undefined, token);
  },
  createDevice(token, payload) {
    return backendFetch("POST", "/api/device-monitor/device", payload, token);
  },
  updateDevice(token, id, payload) {
    return backendFetch("PUT", `/api/device-monitor/device/${id}`, payload, token);
  },
  updateDeviceStatus(token, id, enabled) {
    return backendFetch("PATCH", `/api/device-monitor/device/${id}/status`, { enabled }, token);
  },
  deleteDevice(token, id) {
    return backendFetch("DELETE", `/api/device-monitor/device/${id}`, undefined, token);
  },
  batchDeleteDevices(token, ids) {
    return backendFetch("DELETE", "/api/device-monitor/device/batch", { ids }, token);
  },
  exportDevices(token, query, template = false) {
    return backendDownload(`/api/device-monitor/device/export?${toQuery({ ...query, pageNo: 1, pageSize: 10000, template })}`, token);
  },
  importDevices(token, rows) {
    const body = { rows };
    assertImportRequestSize(body);
    return backendFetch("POST", "/api/device-monitor/device/import", body, token);
  },
  pointDeviceTree(token) {
    return backendFetch("GET", "/api/device-monitor/point/device-tree", undefined, token);
  },
  pointList(token, query) {
    return backendFetch("GET", `/api/device-monitor/point/list?${toQuery(query)}`, undefined, token);
  },
  createPoint(token, payload) {
    return backendFetch("POST", "/api/device-monitor/point", payload, token);
  },
  updatePoint(token, id, payload) {
    return backendFetch("PUT", `/api/device-monitor/point/${id}`, payload, token);
  },
  updatePointStatus(token, id, enabled, locator = {}) {
    const suffix = toQuery({
      locatorDeviceId: locator.deviceId,
      locatorVarSeq: locator.varSeq,
    });
    return backendFetch(
      "PATCH",
      `/api/device-monitor/point/${id}/status${suffix ? `?${suffix}` : ""}`,
      { enabled },
      token,
    );
  },
  deletePoint(token, id) {
    return backendFetch("DELETE", `/api/device-monitor/point/${id}`, undefined, token);
  },
  batchDeletePoints(token, ids) {
    return backendFetch("DELETE", "/api/device-monitor/point/batch", { ids }, token);
  },
  exportPoints(token, query, template = false) {
    return backendDownload(`/api/device-monitor/point/export?${toQuery({ ...query, pageNo: 1, pageSize: 10000, template })}`, token);
  },
  importPoints(token, rows) {
    const body = { rows };
    assertImportRequestSize(body);
    return backendFetch("POST", "/api/device-monitor/point/import", body, token);
  },
  importPointsFromTemplate(token, deviceId) {
    return backendFetch("POST", "/api/device-monitor/point/import/template", { deviceId }, token);
  },
  realtimeInit(token, deviceId, groupCode = "g001") {
    return backendFetch("GET", `/api/device-monitor/monitor/realtime-init?${toQuery({ deviceId, groupCode })}`, undefined, token);
  },
  realtimePointGroups(token, deviceCode) {
    return backendFetch("GET", `/api/device-monitor/device/${encodeURIComponent(deviceCode)}/point-groups`, undefined, token);
  },
  historyDevices(token) {
    return backendFetch("GET", "/api/device-monitor/history/devices", undefined, token);
  },
  historyDeviceTypes(token) {
    return backendFetch("GET", "/api/device-monitor/history/device-types", undefined, token);
  },
  historyPoints(token, deviceId) {
    return backendFetch("GET", `/api/device-monitor/history/points?deviceId=${deviceId}`, undefined, token);
  },
  historyChart(token, query) {
    return backendFetch("GET", `/api/device-monitor/history/chart?${toQuery(query)}`, undefined, token);
  },
  historyRawMatrix(token, query) {
    return backendFetch("GET", `/api/device-monitor/history/raw-matrix?${toQuery(query)}`, undefined, token);
  },
  historyExport(token, payload) {
    return backendFetch("POST", "/api/device-monitor/history/export", payload, token);
  },
  historyExportStatus(token, taskId) {
    return backendFetch("GET", `/api/device-monitor/history/export/${taskId}/status`, undefined, token);
  },
  historyExportDownload(token, taskId) {
    return backendDownload(`/api/device-monitor/history/export/${taskId}/download`, token);
  },
  systemLogList(token, query) {
    return backendFetch("GET", `/api/device-monitor/log/list?${toQuery(query)}`, undefined, token);
  },
  logStats(token) {
    return backendFetch("GET", "/api/device-monitor/log/stats", undefined, token);
  },
  toggleLogDebug(token, payload) {
    return backendFetch("POST", "/api/device-monitor/log/debug", payload, token);
  },
  cleanupLogs(token, payload) {
    return backendFetch("POST", "/api/device-monitor/log/cleanup", payload, token);
  },
  tdengineLogFiles(token) {
    return backendFetch("GET", "/api/device-monitor/log/tdengine/files", undefined, token);
  },
  tdengineLogTail(token, file, lines = 300) {
    return backendFetch("GET", `/api/device-monitor/log/tdengine/tail?${toQuery({ file, lines })}`, undefined, token);
  },
  userList(token, query) { return backendFetch("GET", `/api/user/list?${toQuery(query)}`, undefined, token); },
  userInfo(token) { return backendFetch("GET", "/api/user/info", undefined, token); },
  roleOptions(token) { return backendFetch("GET", "/api/role/options", undefined, token); },
  createUser(token, payload) {
    const requestPayload = stripClientPassword(payload);
    return backendFetch<UserPasswordProvisionResult>("POST", "/api/user", requestPayload, token);
  },
  updateUser(token, id, payload) { return backendFetch("PUT", `/api/user/${id}`, payload, token); },
  updateUserStatus(token, id, status) { return backendFetch("PATCH", `/api/user/${id}/status`, { status }, token); },
  resetUserPassword(token, id) { return backendFetch<UserPasswordProvisionResult>("PATCH", `/api/user/${id}/password/reset`, {}, token); },
  changePassword(token, oldPassword, newPassword) {
    return backendFetch<boolean>("POST", "/api/user/password/change", { oldPassword, newPassword }, token);
  },
  deleteUser(token, id) { return backendFetch("DELETE", `/api/user/${id}`, undefined, token); },
  roleList(token, query) { return backendFetch("GET", `/api/role/list?${toQuery(query)}`, undefined, token); },
  createRole(token, payload) { return backendFetch("POST", "/api/role", payload, token); },
  updateRole(token, id, payload) { return backendFetch("PUT", `/api/role/${id}`, payload, token); },
  updateRoleStatus(token, id, enabled) { return backendFetch("PATCH", `/api/role/${id}/status`, { enabled }, token); },
  deleteRole(token, id) { return backendFetch("DELETE", `/api/role/${id}`, undefined, token); },
  roleGrant(token, id) { return backendFetch("GET", `/api/role/${id}/grant`, undefined, token); },
  updateRoleGrant(token, id, payload) { return backendFetch("PUT", `/api/role/${id}/grant`, payload, token); },
  adminMenus(token) { return backendFetch("GET", "/api/v3/system/menus/admin", undefined, token); },
  currentUserMenus(token) { return backendFetch("GET", "/api/v3/system/menus", undefined, token); },
  createMenu(token, payload) { return backendFetch("POST", "/api/v3/system/menu", payload, token); },
  updateMenu(token, id, payload) { return backendFetch("PUT", `/api/v3/system/menu/${id}`, payload, token); },
  deleteMenu(token, id) { return backendFetch("DELETE", `/api/v3/system/menu/${id}`, undefined, token); },
  menuPermissions(token, menuId) { return backendFetch("GET", `/api/v3/system/menu/${menuId}/permissions`, undefined, token); },
  createMenuPermission(token, menuId, payload) { return backendFetch("POST", `/api/v3/system/menu/${menuId}/permissions`, payload, token); },
  updateMenuPermission(token, menuId, permissionId, payload) { return backendFetch("PUT", `/api/v3/system/menu/${menuId}/permissions/${permissionId}`, payload, token); },
  deleteMenuPermission(token, menuId, permissionId) { return backendFetch("DELETE", `/api/v3/system/menu/${menuId}/permissions/${permissionId}`, undefined, token); },
};

// ─── mock / real 选择（动态 import 确保 mock-agent 不进入生产包）─────────────

const _useMock =
  import.meta.env.MODE !== "production" &&
  import.meta.env.VITE_USE_MOCK === "true";

let _implPromise: Promise<BackendClient>;
if (_useMock) {
  _implPromise = import("./mock-agent").then((m) => m.mockBackend);
} else {
  _implPromise = Promise.resolve(realBackend);
}

export const backendClient: BackendClient = {
  async login(userName, password) {
    const c = await _implPromise;
    return c.login(userName, password);
  },
  async logList(token, query) {
    const c = await _implPromise;
    return c.logList(token, query);
  },
  async collectorMetrics(token) {
    const c = await _implPromise;
    return c.collectorMetrics(token);
  },
  async writerMetrics(token) {
    const c = await _implPromise;
    return c.writerMetrics(token);
  },
  async moduleRates(token) {
    const c = await _implPromise;
    return c.moduleRates(token);
  },
  async getCollectorConfig(token) {
    const c = await _implPromise;
    return c.getCollectorConfig(token);
  },
  async updateCollectorConfig(token, streamMaxlen) {
    const c = await _implPromise;
    return c.updateCollectorConfig(token, streamMaxlen);
  },
  async dashboardOverview(token) {
    const c = await _implPromise;
    return c.dashboardOverview(token);
  },
  async dashboardTrend(token, rangeType) {
    const c = await _implPromise;
    return c.dashboardTrend(token, rangeType);
  },
  async dashboardDeviceTypeDistribution(token) {
    const c = await _implPromise;
    return c.dashboardDeviceTypeDistribution(token);
  },
  async dashboardConnectionHealth(token) {
    const c = await _implPromise;
    return c.dashboardConnectionHealth(token);
  },
  async dashboardEdgePipelineHealth(token) {
    const c = await _implPromise;
    return c.dashboardEdgePipelineHealth(token);
  },
  async dashboardRecentEvents(token) {
    const c = await _implPromise;
    return c.dashboardRecentEvents(token);
  },
  async distributedNodes(token) { const c = await _implPromise; return c.distributedNodes(token); },
  async syncDistributedNodeDeviceStatuses(token) { const c = await _implPromise; return c.syncDistributedNodeDeviceStatuses(token); },
  async createNodeRegistrationCode(token, nodeName, expiresMinutes) { const c = await _implPromise; return c.createNodeRegistrationCode(token, nodeName, expiresMinutes); },
  async updateDistributedNodeStatus(token, nodeUuid, status) { const c = await _implPromise; return c.updateDistributedNodeStatus(token, nodeUuid, status); },
  async updateDistributedNodeSettings(token, nodeUuid, readTimeoutMs) { const c = await _implPromise; return c.updateDistributedNodeSettings(token, nodeUuid, readTimeoutMs); },
  async distributedNodeAssignments(token, nodeUuid) { const c = await _implPromise; return c.distributedNodeAssignments(token, nodeUuid); },
  async distributedAssignmentOccupancy(token) { const c = await _implPromise; return c.distributedAssignmentOccupancy(token); },
  async assignDistributedNodeDevices(token, nodeUuid, deviceIds) { const c = await _implPromise; return c.assignDistributedNodeDevices(token, nodeUuid, deviceIds); },
  async driverList(token, query) { const c = await _implPromise; return c.driverList(token, query); },
  async driverOptions(token) { const c = await _implPromise; return c.driverOptions(token); },
  async registerDriver(token, payload) { const c = await _implPromise; return c.registerDriver(token, payload); },
  async uninstallDriver(token, payload) { const c = await _implPromise; return c.uninstallDriver(token, payload); },
  async deviceGroupTree(token) { const c = await _implPromise; return c.deviceGroupTree(token); },
  async deviceList(token, query) { const c = await _implPromise; return c.deviceList(token, query); },
  async createDevice(token, payload) { const c = await _implPromise; return c.createDevice(token, payload); },
  async updateDevice(token, id, payload) { const c = await _implPromise; return c.updateDevice(token, id, payload); },
  async updateDeviceStatus(token, id, enabled) { const c = await _implPromise; return c.updateDeviceStatus(token, id, enabled); },
  async deleteDevice(token, id) { const c = await _implPromise; return c.deleteDevice(token, id); },
  async batchDeleteDevices(token, ids) { const c = await _implPromise; return c.batchDeleteDevices(token, ids); },
  async exportDevices(token, query, template) { const c = await _implPromise; return c.exportDevices(token, query, template); },
  async importDevices(token, rows) { const c = await _implPromise; return c.importDevices(token, rows); },
  async pointDeviceTree(token) { const c = await _implPromise; return c.pointDeviceTree(token); },
  async pointList(token, query) { const c = await _implPromise; return c.pointList(token, query); },
  async createPoint(token, payload) { const c = await _implPromise; return c.createPoint(token, payload); },
  async updatePoint(token, id, payload) { const c = await _implPromise; return c.updatePoint(token, id, payload); },
  async updatePointStatus(token, id, enabled, locator) { const c = await _implPromise; return c.updatePointStatus(token, id, enabled, locator); },
  async deletePoint(token, id) { const c = await _implPromise; return c.deletePoint(token, id); },
  async batchDeletePoints(token, ids) { const c = await _implPromise; return c.batchDeletePoints(token, ids); },
  async exportPoints(token, query, template) { const c = await _implPromise; return c.exportPoints(token, query, template); },
  async importPoints(token, rows) { const c = await _implPromise; return c.importPoints(token, rows); },
  async importPointsFromTemplate(token, deviceId) { const c = await _implPromise; return c.importPointsFromTemplate(token, deviceId); },
  async realtimeInit(token, deviceId, groupCode) { const c = await _implPromise; return c.realtimeInit(token, deviceId, groupCode); },
  async realtimePointGroups(token, deviceCode) { const c = await _implPromise; return c.realtimePointGroups(token, deviceCode); },
  async historyDevices(token) { const c = await _implPromise; return c.historyDevices(token); },
  async historyDeviceTypes(token) { const c = await _implPromise; return c.historyDeviceTypes(token); },
  async historyPoints(token, deviceId) { const c = await _implPromise; return c.historyPoints(token, deviceId); },
  async historyChart(token, query) { const c = await _implPromise; return c.historyChart(token, query); },
  async historyRawMatrix(token, query) { const c = await _implPromise; return c.historyRawMatrix(token, query); },
  async historyExport(token, payload) { const c = await _implPromise; return c.historyExport(token, payload); },
  async historyExportStatus(token, taskId) { const c = await _implPromise; return c.historyExportStatus(token, taskId); },
  async historyExportDownload(token, taskId) { const c = await _implPromise; return c.historyExportDownload(token, taskId); },
  async systemLogList(token, query) { const c = await _implPromise; return c.systemLogList(token, query); },
  async logStats(token) { const c = await _implPromise; return c.logStats(token); },
  async toggleLogDebug(token, payload) { const c = await _implPromise; return c.toggleLogDebug(token, payload); },
  async cleanupLogs(token, payload) { const c = await _implPromise; return c.cleanupLogs(token, payload); },
  async tdengineLogFiles(token) { const c = await _implPromise; return c.tdengineLogFiles(token); },
  async tdengineLogTail(token, file, lines) { const c = await _implPromise; return c.tdengineLogTail(token, file, lines); },
  async userList(token, query) { const c = await _implPromise; return c.userList(token, query); },
  async userInfo(token) { const c = await _implPromise; return c.userInfo(token); },
  async roleOptions(token) { const c = await _implPromise; return c.roleOptions(token); },
  async createUser(token, payload) { const c = await _implPromise; return c.createUser(token, payload); },
  async updateUser(token, id, payload) { const c = await _implPromise; return c.updateUser(token, id, payload); },
  async updateUserStatus(token, id, status) { const c = await _implPromise; return c.updateUserStatus(token, id, status); },
  async resetUserPassword(token, id) { const c = await _implPromise; return c.resetUserPassword(token, id); },
  async changePassword(token, oldPassword, newPassword) { const c = await _implPromise; return c.changePassword(token, oldPassword, newPassword); },
  async deleteUser(token, id) { const c = await _implPromise; return c.deleteUser(token, id); },
  async roleList(token, query) { const c = await _implPromise; return c.roleList(token, query); },
  async createRole(token, payload) { const c = await _implPromise; return c.createRole(token, payload); },
  async updateRole(token, id, payload) { const c = await _implPromise; return c.updateRole(token, id, payload); },
  async updateRoleStatus(token, id, enabled) { const c = await _implPromise; return c.updateRoleStatus(token, id, enabled); },
  async deleteRole(token, id) { const c = await _implPromise; return c.deleteRole(token, id); },
  async roleGrant(token, id) { const c = await _implPromise; return c.roleGrant(token, id); },
  async updateRoleGrant(token, id, payload) { const c = await _implPromise; return c.updateRoleGrant(token, id, payload); },
  async adminMenus(token) { const c = await _implPromise; return c.adminMenus(token); },
  async currentUserMenus(token) { const c = await _implPromise; return c.currentUserMenus(token); },
  async createMenu(token, payload) { const c = await _implPromise; return c.createMenu(token, payload); },
  async updateMenu(token, id, payload) { const c = await _implPromise; return c.updateMenu(token, id, payload); },
  async deleteMenu(token, id) { const c = await _implPromise; return c.deleteMenu(token, id); },
  async menuPermissions(token, menuId) { const c = await _implPromise; return c.menuPermissions(token, menuId); },
  async createMenuPermission(token, menuId, payload) { const c = await _implPromise; return c.createMenuPermission(token, menuId, payload); },
  async updateMenuPermission(token, menuId, permissionId, payload) { const c = await _implPromise; return c.updateMenuPermission(token, menuId, permissionId, payload); },
  async deleteMenuPermission(token, menuId, permissionId) { const c = await _implPromise; return c.deleteMenuPermission(token, menuId, permissionId); },
};
