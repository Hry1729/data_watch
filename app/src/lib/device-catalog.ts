import type { PaginatedResult } from "../types/device-monitor";

/** Selector/list loading limits keep a very large catalog from freezing the WebView. */
export const DEVICE_CATALOG_PAGE_SIZE = 500;
export const DEVICE_CATALOG_MAX_ITEMS = 10_000;

export interface FullDeviceCatalog<T> extends PaginatedResult<T> {
  truncated: boolean;
}

/** Load every backend page needed by selectors, while retaining the server total. */
export async function loadAllDevicePages<T>(
  fetchPage: (pageNo: number, pageSize: number) => Promise<PaginatedResult<T>>,
  options: { pageSize?: number; maxItems?: number } = {},
): Promise<FullDeviceCatalog<T>> {
  const pageSize = Math.max(1, options.pageSize ?? DEVICE_CATALOG_PAGE_SIZE);
  const maxItems = Math.max(pageSize, options.maxItems ?? DEVICE_CATALOG_MAX_ITEMS);
  const first = await fetchPage(1, pageSize);
  const total = Math.max(first.total ?? 0, first.list.length);
  const target = Math.min(total, maxItems);
  const pageCount = Math.ceil(target / pageSize);
  const rest: PaginatedResult<T>[] = [];
  const maxConcurrent = 3;
  for (let start = 1; start < pageCount; start += maxConcurrent) {
    const batch = await Promise.all(
      Array.from({ length: Math.min(maxConcurrent, pageCount - start) }, (_, index) => fetchPage(start + index + 1, pageSize)),
    );
    rest.push(...batch);
  }
  const list = [first, ...rest].flatMap((page) => page.list).slice(0, target);
  return {
    ...first,
    list,
    total,
    truncated: total > target,
  };
}
