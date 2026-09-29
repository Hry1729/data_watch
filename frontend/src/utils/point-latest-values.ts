export function mergePointLatestValues<T extends { id: number }>(
  currentRows: T[],
  requestedIds: number[],
  latestValues: Record<string, Api.DeviceMonitor.PointLatestValue>
): Array<T & Api.DeviceMonitor.PointLatestValue> | null {
  const currentIds = currentRows.map((row) => row.id)
  if (requestedIds.length !== currentIds.length || requestedIds.some((id, index) => id !== currentIds[index])) {
    return null
  }

  return currentRows.map((row) => ({ ...row, ...(latestValues[String(row.id)] || {}) }))
}
