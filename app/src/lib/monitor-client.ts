import { BUILD_ROLE } from "@/config/build-role";
import { agentClient } from "@/lib/agent-client";
import { getAgentAuthToken } from "@/lib/agent-auth-token";
import { backendClient } from "@/lib/backend-client";
import type { AppError } from "@/types";
import type {
  DeviceListItem,
  DeviceListQuery,
  DeviceRealtimeStatus,
  DeviceTypeItem,
  HistoryChartResponse,
  HistoryDeviceItem,
  HistoryExportPayload,
  HistoryExportStatus,
  HistoryPointItem,
  HistoryQuery,
  HistoryRawMatrixResponse,
  PaginatedResult,
  PointListItem,
  PointListQuery,
  RealtimePointGroup,
} from "@/types/device-monitor";

type MatrixQuery = HistoryQuery & {
  alignGranularity?: "100ms" | "500ms" | "1s";
  pageSize?: number;
  cursorTs?: string;
};

type DeviceScope = { ids: Set<number>; codes: Set<string> };

function isLocalToken(token: string): boolean {
  return BUILD_ROLE === "edge" && token === getAgentAuthToken();
}

function canUseLocal(error: unknown): boolean {
  if (BUILD_ROLE !== "edge") return false;
  const appError = error as AppError;
  return appError?.type === "network_error"
    || (appError?.type === "api_error" && appError.status >= 500);
}

async function centerFirst<T>(
  token: string,
  center: () => Promise<T>,
  local: () => Promise<T>,
): Promise<T> {
  if (isLocalToken(token)) return local();
  try {
    return await center();
  } catch (error) {
    if (!canUseLocal(error)) throw error;
    return local();
  }
}

async function assignedScope(): Promise<DeviceScope> {
  const page = await agentClient.getLocalMonitorDevices({ pageNo: 1, pageSize: 5000 });
  return {
    ids: new Set(page.list.map((device) => device.id)),
    codes: new Set(page.list.map((device) => device.deviceCode)),
  };
}

function emptyPage<T>(pageNo = 1, pageSize = 20): PaginatedResult<T> {
  return { list: [], total: 0, pageNo, pageSize, source: "center" };
}

async function requireAssigned(deviceId: number): Promise<void> {
  if (BUILD_ROLE !== "edge") return;
  const scope = await assignedScope();
  if (!scope.ids.has(deviceId)) {
    throw { type: "api_error", status: 403, message: "该设备未分配给本边缘节点" } satisfies AppError;
  }
}

async function requireAssignedCode(deviceCode: string): Promise<void> {
  if (BUILD_ROLE !== "edge") return;
  const scope = await assignedScope();
  if (!scope.codes.has(deviceCode)) {
    throw { type: "api_error", status: 403, message: "该设备未分配给本边缘节点" } satisfies AppError;
  }
}

function localExportUnavailable(): never {
  throw {
    type: "network_error",
    message: "中心不可达，本机离线历史暂不支持导出",
  } satisfies AppError;
}

export const monitorClient = {
  deviceList(token: string, query: DeviceListQuery): Promise<PaginatedResult<DeviceListItem>> {
    return centerFirst<PaginatedResult<DeviceListItem>>(
      token,
      async () => {
        if (BUILD_ROLE !== "edge") return backendClient.deviceList(token, query);
        const scope = await assignedScope();
        const requested = query.deviceIds?.length
          ? query.deviceIds.filter((id) => scope.ids.has(id))
          : [...scope.ids];
        if (!requested.length) return emptyPage(query.pageNo, query.pageSize);
        return { ...(await backendClient.deviceList(token, { ...query, deviceIds: requested })), source: "center" as const };
      },
      async () => ({ ...(await agentClient.getLocalMonitorDevices(query)), source: "local" as const }),
    );
  },

  pointList(token: string, query: PointListQuery): Promise<PaginatedResult<PointListItem>> {
    return centerFirst<PaginatedResult<PointListItem>>(
      token,
      async () => {
        if (BUILD_ROLE !== "edge") return backendClient.pointList(token, query);
        const scope = await assignedScope();
        if (query.deviceId && !scope.ids.has(query.deviceId)) return emptyPage(query.pageNo, query.pageSize);
        const requested = query.deviceIds?.length
          ? query.deviceIds.filter((id) => scope.ids.has(id))
          : [...scope.ids];
        if (!query.deviceId && !requested.length) return emptyPage(query.pageNo, query.pageSize);
        return { ...(await backendClient.pointList(token, { ...query, deviceIds: requested })), source: "center" as const };
      },
      async () => ({ ...(await agentClient.getLocalMonitorPoints(query)), source: "local" as const }),
    );
  },

  async realtimePointGroups(token: string, deviceCode: string): Promise<RealtimePointGroup[]> {
    await requireAssignedCode(deviceCode);
    return centerFirst(
      token,
      () => backendClient.realtimePointGroups(token, deviceCode),
      () => agentClient.getLocalRealtimeGroups(deviceCode),
    );
  },

  async realtimeInit(token: string, deviceId: number, groupCode: string): Promise<DeviceRealtimeStatus> {
    await requireAssigned(deviceId);
    return centerFirst(
      token,
      () => backendClient.realtimeInit(token, deviceId, groupCode),
      async () => ({ ...(await agentClient.getLocalRealtime(deviceId, groupCode)), degraded: true }),
    );
  },

  historyDevices(token: string): Promise<HistoryDeviceItem[]> {
    return centerFirst(
      token,
      async () => {
        const devices = await backendClient.historyDevices(token);
        if (BUILD_ROLE !== "edge") return devices;
        const scope = await assignedScope();
        return devices.filter((device) =>
          (device.id != null && scope.ids.has(device.id))
          || (device.deviceCode != null && scope.codes.has(device.deviceCode)),
        );
      },
      () => agentClient.getLocalHistoryDevices(),
    );
  },

  async historyDeviceTypes(token: string): Promise<DeviceTypeItem[]> {
    if (BUILD_ROLE !== "edge") return backendClient.historyDeviceTypes(token);
    const devices = await this.historyDevices(token);
    const groups = new Map<string, DeviceTypeItem>();
    for (const device of devices) {
      const typeCode = device.typeCode || "unknown";
      const current = groups.get(typeCode);
      groups.set(typeCode, {
        typeCode,
        typeName: device.typeName || typeCode,
        deviceCount: (current?.deviceCount ?? 0) + 1,
      });
    }
    return [...groups.values()];
  },

  async historyPoints(token: string, deviceId: number): Promise<HistoryPointItem[]> {
    await requireAssigned(deviceId);
    return centerFirst(token, () => backendClient.historyPoints(token, deviceId), () => agentClient.getLocalHistoryPoints(deviceId));
  },

  async historyChart(token: string, query: HistoryQuery): Promise<HistoryChartResponse> {
    await requireAssigned(query.deviceId);
    return centerFirst(token, () => backendClient.historyChart(token, query), () => agentClient.getLocalHistoryChart(query));
  },

  async historyRawMatrix(token: string, query: MatrixQuery): Promise<HistoryRawMatrixResponse> {
    await requireAssigned(query.deviceId);
    return centerFirst(token, () => backendClient.historyRawMatrix(token, query), () => agentClient.getLocalHistoryMatrix(query));
  },

  async historyExport(token: string, payload: HistoryExportPayload): Promise<{ taskId: string }> {
    await requireAssigned(payload.deviceId);
    if (isLocalToken(token)) localExportUnavailable();
    return backendClient.historyExport(token, payload);
  },

  historyExportStatus(token: string, taskId: string): Promise<HistoryExportStatus> {
    if (isLocalToken(token)) localExportUnavailable();
    return backendClient.historyExportStatus(token, taskId);
  },

  historyExportDownload(token: string, taskId: string): Promise<{ bytes: number[]; fileName: string }> {
    if (isLocalToken(token)) localExportUnavailable();
    return backendClient.historyExportDownload(token, taskId);
  },
};