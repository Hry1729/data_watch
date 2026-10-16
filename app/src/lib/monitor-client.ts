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

type DeviceScope = { ids: Set<number>; codes: Set<string>; types: Set<string> };

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

async function assignedScope(signal?: AbortSignal): Promise<DeviceScope> {
  const devices = await agentClient.getLocalMonitorScope(signal);
  return {
    ids: new Set(devices.map((device) => device.id)),
    codes: new Set(devices.map((device) => device.deviceCode)),
    types: new Set(devices.map((device) => device.deviceType)),
  };
}

function emptyPage<T>(pageNo = 1, pageSize = 20): PaginatedResult<T> {
  return { list: [], total: 0, pageNo, pageSize, source: "center" };
}

async function requireAssigned(deviceId: number, signal?: AbortSignal): Promise<void> {
  if (BUILD_ROLE !== "edge") return;
  const scope = await assignedScope(signal);
  if (!scope.ids.has(deviceId)) {
    throw { type: "api_error", status: 403, message: "该设备未分配给本边缘节点" } satisfies AppError;
  }
}

async function requireAssignedCode(deviceCode: string, signal?: AbortSignal): Promise<void> {
  if (BUILD_ROLE !== "edge") return;
  const scope = await assignedScope(signal);
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
  deviceTypes(token: string, signal?: AbortSignal): Promise<string[]> {
    return centerFirst(
      token,
      async () => {
        if (BUILD_ROLE !== "edge") return backendClient.deviceTypes(token, {}, signal);
        const scope = await assignedScope(signal);
        return [...scope.types];
      },
      async () => [...(await assignedScope(signal)).types],
    );
  },

  deviceList(token: string, query: DeviceListQuery, signal?: AbortSignal): Promise<PaginatedResult<DeviceListItem>> {
    return centerFirst<PaginatedResult<DeviceListItem>>(
      token,
      async () => {
        if (BUILD_ROLE !== "edge") return backendClient.deviceList(token, query, signal);
        const scope = await assignedScope(signal);
        const requested = query.deviceIds !== undefined
          ? query.deviceIds.filter((id) => scope.ids.has(id))
          : [...scope.ids];
        if (!requested.length) return emptyPage(query.pageNo, query.pageSize);
        return { ...(await backendClient.deviceList(token, { ...query, deviceIds: requested }, signal)), source: "center" as const };
      },
      async () => ({ ...(await agentClient.getLocalMonitorDevices(query, signal)), source: "local" as const }),
    );
  },

  pointList(token: string, query: PointListQuery, signal?: AbortSignal): Promise<PaginatedResult<PointListItem>> {
    return centerFirst<PaginatedResult<PointListItem>>(
      token,
      async () => {
        if (BUILD_ROLE !== "edge") return backendClient.pointList(token, query, signal);
        const scope = await assignedScope(signal);
        if (query.deviceId && !scope.ids.has(query.deviceId)) return emptyPage(query.pageNo, query.pageSize);
        const requested = query.deviceIds !== undefined
          ? query.deviceIds.filter((id) => scope.ids.has(id))
          : [...scope.ids];
        if (!query.deviceId && !requested.length) return emptyPage(query.pageNo, query.pageSize);
        return { ...(await backendClient.pointList(token, { ...query, deviceIds: requested }, signal)), source: "center" as const };
      },
      async () => ({ ...(await agentClient.getLocalMonitorPoints(query, signal)), source: "local" as const }),
    );
  },

  async realtimePointGroups(token: string, deviceCode: string, signal?: AbortSignal): Promise<RealtimePointGroup[]> {
    await requireAssignedCode(deviceCode, signal);
    return centerFirst(
      token,
      () => backendClient.realtimePointGroups(token, deviceCode, signal),
      () => agentClient.getLocalRealtimeGroups(deviceCode, signal),
    );
  },

  async realtimeInit(token: string, deviceId: number, groupCode: string, signal?: AbortSignal): Promise<DeviceRealtimeStatus> {
    await requireAssigned(deviceId, signal);
    return centerFirst(
      token,
      () => backendClient.realtimeInit(token, deviceId, groupCode, signal),
      async () => ({ ...(await agentClient.getLocalRealtime(deviceId, groupCode, signal)), degraded: true }),
    );
  },

  historyDevices(token: string, signal?: AbortSignal): Promise<HistoryDeviceItem[]> {
    return centerFirst(
      token,
      async () => {
        const devices = await backendClient.historyDevices(token, signal);
        if (BUILD_ROLE !== "edge") return devices;
        const scope = await assignedScope(signal);
        return devices.filter((device) =>
          (device.id != null && scope.ids.has(device.id))
          || (device.deviceCode != null && scope.codes.has(device.deviceCode)),
        );
      },
      () => agentClient.getLocalHistoryDevices(signal),
    );
  },

  async historyDeviceTypes(token: string, signal?: AbortSignal): Promise<DeviceTypeItem[]> {
    if (BUILD_ROLE !== "edge") return backendClient.historyDeviceTypes(token, signal);
    const devices = await this.historyDevices(token, signal);
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

  async historyPoints(token: string, deviceId: number, signal?: AbortSignal): Promise<HistoryPointItem[]> {
    await requireAssigned(deviceId, signal);
    return centerFirst(token, () => backendClient.historyPoints(token, deviceId, signal), () => agentClient.getLocalHistoryPoints(deviceId, signal));
  },

  async historyChart(token: string, query: HistoryQuery, signal?: AbortSignal): Promise<HistoryChartResponse> {
    await requireAssigned(query.deviceId, signal);
    return centerFirst(token, () => backendClient.historyChart(token, query, signal), () => agentClient.getLocalHistoryChart(query, signal));
  },

  async historyRawMatrix(token: string, query: MatrixQuery, signal?: AbortSignal): Promise<HistoryRawMatrixResponse> {
    await requireAssigned(query.deviceId, signal);
    return centerFirst(token, () => backendClient.historyRawMatrix(token, query, signal), () => agentClient.getLocalHistoryMatrix(query, signal));
  },

  async historyExport(token: string, payload: HistoryExportPayload, signal?: AbortSignal): Promise<{ taskId: string }> {
    await requireAssigned(payload.deviceId, signal);
    if (isLocalToken(token)) localExportUnavailable();
    return backendClient.historyExport(token, payload, signal);
  },

  historyExportStatus(token: string, taskId: string, signal?: AbortSignal): Promise<HistoryExportStatus> {
    if (isLocalToken(token)) localExportUnavailable();
    return backendClient.historyExportStatus(token, taskId, signal);
  },

  historyExportDownload(token: string, taskId: string, signal?: AbortSignal): Promise<{ bytes: number[]; fileName: string }> {
    if (isLocalToken(token)) localExportUnavailable();
    return backendClient.historyExportDownload(token, taskId, signal);
  },
};
