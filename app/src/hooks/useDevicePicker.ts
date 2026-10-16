import { useCallback, useState } from "react";
import { useBackendQuery } from "@/hooks/useBackendQuery";
import type { DeviceListItem, DeviceListQuery, PaginatedResult } from "@/types/device-monitor";

export const DEVICE_PICKER_PAGE_SIZE = 50;

type DeviceClient = {
  deviceList(token: string, query: DeviceListQuery, signal?: AbortSignal): Promise<PaginatedResult<DeviceListItem>>;
};

export function useDevicePicker(client: DeviceClient, keyword: string, deviceType = "all", enabled = true) {
  const search = keyword.trim();
  const key = JSON.stringify([search, deviceType]);
  const [pagination, setPagination] = useState({ key, page: 1 });
  const page = pagination.key === key ? pagination.page : 1;
  const setPage = useCallback((next: number) => setPagination({ key, page: next }), [key]);
  const load = useCallback((token: string, signal: AbortSignal) => client.deviceList(token, {
    pageNo: page,
    pageSize: DEVICE_PICKER_PAGE_SIZE,
    keyword: search || undefined,
    deviceTypeExact: deviceType === "all" ? undefined : deviceType,
  }, signal), [client, deviceType, page, search]);
  const query = useBackendQuery(load, { enabled });
  return { ...query, page, setPage, pageSize: DEVICE_PICKER_PAGE_SIZE };
}
