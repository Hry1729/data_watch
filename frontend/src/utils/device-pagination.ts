export const DEVICE_PAGE_SIZE = 500
export const DEVICE_MAX_ITEMS = 10_000

export interface DevicePage<T> {
  list: T[]
  total: number
}

export interface FullDevicePage<T> extends DevicePage<T> {
  truncated: boolean
}

/** Load all pages needed for selectors/statistics without silently taking only page one. */
export async function loadAllDevicePages<T>(
  fetchPage: (pageNo: number, pageSize: number) => Promise<DevicePage<T>>,
  options: { pageSize?: number; maxItems?: number } = {}
): Promise<FullDevicePage<T>> {
  const pageSize = Math.max(1, options.pageSize ?? DEVICE_PAGE_SIZE)
  const maxItems = Math.max(pageSize, options.maxItems ?? DEVICE_MAX_ITEMS)
  const first = await fetchPage(1, pageSize)
  const total = Math.max(first.total || 0, first.list.length)
  const target = Math.min(total, maxItems)
  const pageCount = Math.ceil(target / pageSize)
  const rest: DevicePage<T>[] = []
  const maxConcurrent = 3
  for (let start = 1; start < pageCount; start += maxConcurrent) {
    const batch = await Promise.all(
      Array.from({ length: Math.min(maxConcurrent, pageCount - start) }, (_, index) => fetchPage(start + index + 1, pageSize))
    )
    rest.push(...batch)
  }
  return {
    list: [first, ...rest].flatMap((page) => page.list).slice(0, target),
    total,
    truncated: total > target
  }
}
