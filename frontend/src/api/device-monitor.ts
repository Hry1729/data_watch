/**
 * 设备监控平台 - API接口定义（评审版口径）
 */

import request from '@/utils/http'
import { useUserStore } from '@/store/modules/user'
import {
  assertImportRequestSize,
  MAX_IMPORT_FILE_BYTES,
  MAX_IMPORT_REQUEST_BYTES,
  MAX_IMPORT_SHEET_CELLS,
  MAX_IMPORT_SHEET_ROWS,
  validateImportFileSize,
  validateImportSheetRange
} from '@/utils/import-limits'
import { parseImportRows } from '@/utils/import-workbook'

export {
  assertImportRequestSize,
  MAX_IMPORT_FILE_BYTES,
  MAX_IMPORT_REQUEST_BYTES,
  MAX_IMPORT_SHEET_CELLS,
  MAX_IMPORT_SHEET_ROWS,
  validateImportFileSize,
  validateImportSheetRange
} from '@/utils/import-limits'

const BASE_URL = '/api/device-monitor'
const DEVICE_IMPORT_FIELD_MAP: Record<string, string> = {
  设备类型: 'deviceType',
  设备编号: 'deviceCode',
  设备名称: 'deviceName',
  关联驱动: 'driverName',
  设备状态: 'deviceStatus',
  描述: 'description'
}
const POINT_IMPORT_FIELD_MAP: Record<string, string> = {
  设备编号: 'deviceCode',
  点位序号: 'varSeq',
  点位名称: 'pointName',
  IO地址: 'ioAddress',
  数据类型: 'dataType',
  驱动名称: 'driverName',
  是否只读: 'isReadonly',
  描述: 'description',
  '采集周期(ms)': 'scanRate',
  是否存储: 'isStorage'
}

const toPositiveNumber = (value: unknown, fallback: number) => {
  const num = Number(value)
  return Number.isFinite(num) && num > 0 ? num : fallback
}

const normalizePaginationParams = (
  params: Api.DeviceMonitor.CompatiblePaginationParams | undefined,
  defaultPageNo: number = 1,
  defaultPageSize: number = 20
) => {
  const pageNo = toPositiveNumber(params?.pageNo ?? params?.current, defaultPageNo)
  const pageSize = toPositiveNumber(params?.pageSize ?? params?.size, defaultPageSize)
  return { pageNo, pageSize }
}

const mapOverviewToStats = (
  overview: Api.DeviceMonitor.DashboardOverview
): Api.DeviceMonitor.DashboardStats => ({
  totalDevices: overview.deviceTotal,
  onlineDevices: overview.onlineDeviceTotal,
  totalPoints: overview.pointTotal,
  alarmCount: overview.systemAlertTotal
})

const mapDistributionGroupsToLegacy = (
  data: Api.DeviceMonitor.DashboardDeviceTypeDistribution
): Api.DeviceMonitor.DeviceTypeDistribution[] => {
  return (data.groups || []).map((item) => ({
    name: item.typeName,
    value: item.deviceCount
  }))
}

type PointLocator = Pick<Api.DeviceMonitor.PointListItem, 'id' | 'deviceId' | 'varSeq'>

const normalizePointLocator = (point: number | Partial<PointLocator>) => {
  if (typeof point === 'number') {
    return {
      pathId: point,
      params: undefined as undefined | { deviceId: number; varSeq: number }
    }
  }

  const pathId = toPositiveNumber(point.id, 0)
  const hasCompositeLocator =
    typeof point.deviceId === 'number' &&
    point.deviceId > 0 &&
    typeof point.varSeq === 'number' &&
    point.varSeq > 0

  return {
    pathId,
    params: hasCompositeLocator
      ? {
          deviceId: point.deviceId as number,
          varSeq: point.varSeq as number
        }
      : undefined
  }
}

// ==================== 驱动管理 ====================

/** 获取驱动列表 */
export function fetchDriverList(params: Api.DeviceMonitor.DriverSearchParams) {
  const { pageNo, pageSize } = normalizePaginationParams(params)

  return request.get<Api.DeviceMonitor.DriverList>({
    url: `${BASE_URL}/driver/list`,
    params: {
      pageNo,
      pageSize,
      keyword: params.keyword ?? params.driverName
    }
  })
}

/** 获取所有驱动（下拉选择用） */
export function fetchDriverOptions() {
  return request.get<{ driverName: string; driverDesc: string }[]>({
    url: `${BASE_URL}/driver/options`
  })
}

// ==================== 设备管理 ====================

/** 获取设备分组树 */
export function fetchDeviceGroupTree() {
  return request.get<Api.DeviceMonitor.DeviceGroupTreeNode[]>({
    url: `${BASE_URL}/device/group-tree`
  })
}

/** 获取设备分组（兼容旧版设备页） */
export async function fetchDeviceGroups() {
  const tree = await fetchDeviceGroupTree()
  return (tree || []).map((node) => ({
    key: node.key,
    name: node.name,
    count: node.count ?? node.children?.length ?? 0
  })) as Api.DeviceMonitor.DeviceGroup[]
}

/** 获取设备列表 */
export function fetchDeviceList(params: Api.DeviceMonitor.DeviceSearchParams) {
  const { pageNo, pageSize } = normalizePaginationParams(params)

  return request.get<Api.DeviceMonitor.DeviceList>({
    url: `${BASE_URL}/device/list`,
    params: {
      pageNo,
      pageSize,
      keyword: params.keyword,
      deviceType: params.deviceType,
      deviceCode: params.deviceCode,
      deviceName: params.deviceName,
      deviceStatus: params.deviceStatus,
      connectStatus: params.connectStatus,
      includeSummary: params.includeSummary ?? false
    }
  })
}

/** 获取设备统计摘要；默认统计当前基础筛选范围，不受连接状态筛选影响。 */
export function fetchDeviceSummary(params: Api.DeviceMonitor.DeviceSummaryParams = {}) {
  return request.get<Api.DeviceMonitor.DeviceSummary>({
    url: `${BASE_URL}/device/summary`,
    params
  })
}

/** 获取设备详情 */
export function fetchDeviceDetail(id: number) {
  return request.get<Api.DeviceMonitor.DeviceListItem>({
    url: `${BASE_URL}/device/${id}`
  })
}

/** 新增设备 */
export function fetchAddDevice(data: Api.DeviceMonitor.DeviceForm) {
  return request.post<{ id: number }>({
    url: `${BASE_URL}/device`,
    data
  })
}

/** 编辑设备（评审版：PUT /device/{id}） */
export function fetchUpdateDevice(data: Api.DeviceMonitor.DeviceForm) {
  if (!data.id) {
    return Promise.reject(new Error('更新设备缺少 id'))
  }

  return request.put({
    url: `${BASE_URL}/device/${data.id}`,
    data
  })
}

/** 删除设备 */
export function fetchDeleteDevice(id: number) {
  return request.del({
    url: `${BASE_URL}/device/${id}`
  })
}

/** 批量删除设备 */
export function fetchBatchDeleteDevices(ids: number[]) {
  return request.del({
    url: `${BASE_URL}/device/batch`,
    data: { ids }
  })
}

/** 导出设备 */
export function fetchExportDevices(
  params: Api.DeviceMonitor.DeviceSearchParams,
  template: boolean = false
) {
  const { pageNo, pageSize } = normalizePaginationParams(params, 1, 10000)

  return request.get<Blob>({
    url: `${BASE_URL}/device/export`,
    params: {
      pageNo,
      pageSize,
      keyword: params.keyword,
      deviceType: params.deviceType,
      deviceCode: params.deviceCode,
      deviceName: params.deviceName,
      deviceStatus: params.deviceStatus,
      connectStatus: params.connectStatus,
      template
    },
    responseType: 'blob'
  })
}

/** 导入设备 */
export async function fetchImportDevices(file: File) {
  const rows = await parseImportRows(file, DEVICE_IMPORT_FIELD_MAP)
  assertImportRequestSize({ rows })
  return request.post<Api.DeviceMonitor.ImportResult>({
    url: `${BASE_URL}/device/import`,
    data: { rows }
  })
}

// ==================== 点位管理 ====================

/** 获取点位设备树 */
export function fetchPointDeviceTree() {
  return request.get<Api.DeviceMonitor.PointDeviceTreeNode[]>({
    url: `${BASE_URL}/point/device-tree`
  })
}

/** 获取点位列表 */
export function fetchPointList(params: Api.DeviceMonitor.PointSearchParams) {
  const { pageNo, pageSize } = normalizePaginationParams(params)

  return request.get<Api.DeviceMonitor.PointList>({
    url: `${BASE_URL}/point/list`,
    params: {
      pageNo,
      pageSize,
      keyword: params.keyword,
      deviceId: params.deviceId,
      deviceCode: params.deviceCode,
      groupId: params.groupId,
      varSeq: params.varSeq,
      pointName: params.pointName,
      ioAddress: params.ioAddress
    }
  })
}

/** 批量获取当前页点位实时值。 */
export function fetchPointLatestValues(pointIds: number[]) {
  const batches: number[][] = []
  for (let index = 0; index < pointIds.length; index += 100) {
    batches.push(pointIds.slice(index, index + 100))
  }

  return Promise.all(
    batches.map((batch) => {
      const params = new URLSearchParams()
      batch.forEach((id) => params.append('pointIds', String(id)))
      return request.get<Api.DeviceMonitor.PointLatestValueMap>({
        url: `${BASE_URL}/point/latest`,
        params
      })
    })
  ).then((results) => Object.assign({}, ...results))
}

/** 获取点位详情 */
export function fetchPointDetail(point: number | Partial<PointLocator>) {
  const locator = normalizePointLocator(point)
  return request.get<Api.DeviceMonitor.PointListItem>({
    url: `${BASE_URL}/point/${locator.pathId}`,
    params: locator.params
  })
}

/** 新增点位 */
export function fetchAddPoint(data: Api.DeviceMonitor.PointForm) {
  return request.post<{ id: number }>({
    url: `${BASE_URL}/point`,
    data
  })
}

/** 编辑点位（评审版：PUT /point/{id}） */
export function fetchUpdatePoint(data: Api.DeviceMonitor.PointForm) {
  const locator = normalizePointLocator({
    id: data.id,
    deviceId: data.locatorDeviceId,
    varSeq: data.locatorVarSeq
  })
  if (!locator.pathId && !locator.params) {
    return Promise.reject(new Error('更新点位缺少定位信息'))
  }

  const { locatorDeviceId, locatorVarSeq, ...payload } = data
  return request.put({
    url: `${BASE_URL}/point/${locator.pathId}`,
    params: locator.params
      ? {
          locatorDeviceId: locator.params.deviceId,
          locatorVarSeq: locator.params.varSeq
        }
      : undefined,
    data: payload
  })
}

/** 删除点位 */
export function fetchDeletePoint(point: number | Partial<PointLocator>) {
  const locator = normalizePointLocator(point)
  return request.del({
    url: `${BASE_URL}/point/${locator.pathId}`,
    params: locator.params
  })
}

/** 批量删除点位 */
export function fetchBatchDeletePoints(ids: number[]) {
  return request.del({
    url: `${BASE_URL}/point/batch`,
    data: { ids }
  })
}

/** 导出点位 */
export function fetchExportPoints(
  params: Api.DeviceMonitor.PointSearchParams,
  template: boolean = false
) {
  const { pageNo, pageSize } = normalizePaginationParams(params, 1, 10000)

  return request.get<Blob>({
    url: `${BASE_URL}/point/export`,
    params: {
      pageNo,
      pageSize,
      keyword: params.keyword,
      deviceId: params.deviceId,
      deviceCode: params.deviceCode,
      varSeq: params.varSeq,
      pointName: params.pointName,
      ioAddress: params.ioAddress,
      template
    },
    responseType: 'blob'
  })
}

/** 导入点位 */
export async function fetchImportPoints(file: File) {
  const rows = await parseImportRows(file, POINT_IMPORT_FIELD_MAP)
  assertImportRequestSize({ rows })
  return request.post<Api.DeviceMonitor.ImportResult>({
    url: `${BASE_URL}/point/import`,
    data: { rows }
  })
}

/** 从模板导入点位 */
export function fetchImportPointsFromTemplate(deviceId: number) {
  return request.post<Api.DeviceMonitor.ImportResult>({
    url: `${BASE_URL}/point/import/template`,
    data: { deviceId }
  })
}

// ==================== 实时监控 ====================

/** 获取实时监控设备树 */
export function fetchMonitorDeviceTree() {
  return request.get<Api.DeviceMonitor.MonitorDeviceTreeNode[]>({
    url: `${BASE_URL}/monitor/device-tree`
  })
}

/** 获取实时监控初始化快照 */
export function fetchRealtimeInit(params: Api.DeviceMonitor.RealtimeInitParams = {}) {
  return request.get<Api.DeviceMonitor.DeviceRealtimeStatus>({
    url: `${BASE_URL}/monitor/realtime-init`,
    params
  })
}

/** 兼容旧函数：获取设备实时数据 */
export function fetchDeviceRealtime(deviceId: number) {
  return fetchRealtimeInit({ deviceId })
}

/** 获取实时监控 WebSocket URL（不含 token，连接后通过 auth 消息认证） */
export function getRealtimeWebSocketUrl() {
  const { VITE_API_URL } = import.meta.env
  const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:'

  if (!VITE_API_URL || VITE_API_URL === '/') {
    return `${wsProtocol}//${window.location.host}${BASE_URL}/ws/realtime-push`
  }

  if (/^https?:\/\//.test(VITE_API_URL)) {
    const normalizedBase = VITE_API_URL.replace(/^http/, 'ws').replace(/\/$/, '')
    return `${normalizedBase}${BASE_URL}/ws/realtime-push`
  }

  const normalizedPath = VITE_API_URL.startsWith('/') ? VITE_API_URL.slice(1) : VITE_API_URL
  return `${wsProtocol}//${window.location.host}/${normalizedPath.replace(/\/$/, '')}${BASE_URL}/ws/realtime-push`
}

/** 获取当前用户 token，用于 WebSocket auth 消息 */
export function getAccessToken(): string {
  return useUserStore().accessToken || ''
}

// ==================== 点位分组与实时窗口（新接口）====================

/** 获取设备点位分组列表 */
export function getDevicePointGroups(deviceCode: string) {
  return request.get<Api.DeviceMonitor.PointGroup[]>({
    url: `${BASE_URL}/device/${deviceCode}/point-groups`
  })
}

/** 获取实时窗口数据（短历史帧列表） */
export function getRealtimeWindow(params: {
  device_code: string
  group_code?: string
  scan_rate_ms?: number
  limit?: number
}) {
  return request.get<{
    deviceId: number
    deviceCode: string
    groupCode: string
    scanRateMs: number
    frames: Api.DeviceMonitor.RealtimeWindowFrame[]
  }>({
    url: `${BASE_URL}/realtime-window`,
    params
  })
}

// ==================== 历史数据 ====================

/**** 获取历史页设备列表 */
export function fetchHistoryDevices() {
  return request.get<Api.DeviceMonitor.HistoryDeviceItem[]>({
    url: `${BASE_URL}/history/devices`
  })
}

/** 获取设备类型列表（用于级联选择） */
export function fetchDeviceTypes() {
  return request.get<Api.DeviceMonitor.DeviceTypeItem[]>({
    url: `${BASE_URL}/history/device-types`
  })
}

/** 获取历史页点位列表 */
export function fetchHistoryPoints(params: Api.DeviceMonitor.HistoryPointSearchParams) {
  return request.get<Api.DeviceMonitor.HistoryPointItem[]>({
    url: `${BASE_URL}/history/points`,
    params
  })
}

/** 查询历史数据 */
export function fetchHistoryQuery(
  params: Api.DeviceMonitor.HistorySearchParams,
  options: {
    headers?: Record<string, string>
    skipAuthorization?: boolean
  } = {}
) {
  return request.get<Api.DeviceMonitor.HistoryDataResponse>({
    url: `${BASE_URL}/history/query`,
    params,
    headers: options.headers,
    skipAuthorization: options.skipAuthorization,
    showErrorMessage: false
  })
}

/** 查询历史趋势图表数据（带降采样） */
export function fetchHistoryChart(params: Api.DeviceMonitor.HistorySearchParams) {
  return request.get<Api.DeviceMonitor.HistoryChartResponse>({
    url: `${BASE_URL}/history/chart`,
    params
  })
}

/** 查询历史原始数据（带分页） */
export function fetchHistoryRaw(params: {
  deviceId: number
  varSeq: number
  startTime: string
  endTime: string
  pageSize?: number
  cursorTs?: string
}) {
  return request.get<Api.DeviceMonitor.HistoryRawResponse>({
    url: `${BASE_URL}/history/raw`,
    params
  })
}

/** 查询历史数据矩阵表（多点位桶对齐） */
export function fetchHistoryRawMatrix(params: {
  deviceId: number
  varSeqs: number[]
  startTime: string
  endTime: string
  alignGranularity?: '100ms' | '500ms' | '1s'
  pageSize?: number
  cursorTs?: string
}) {
  return request.get<Api.DeviceMonitor.HistoryRawMatrixResponse>({
    url: `${BASE_URL}/history/raw-matrix`,
    params
  })
}

/** 导出历史数据 */
export function fetchHistoryExport(params: Api.DeviceMonitor.HistoryExportParams) {
  return request.post<Api.DeviceMonitor.HistoryExportTask>({
    url: `${BASE_URL}/history/export`,
    data: params
  })
}

/** 获取历史导出任务状态 */
export function fetchHistoryExportStatus(taskId: string) {
  return request.get<Api.DeviceMonitor.HistoryExportStatus>({
    url: `${BASE_URL}/history/export/${taskId}/status`
  })
}

/** 获取历史导出文件下载链接 */
export function getHistoryExportDownloadUrl(taskId: string) {
  const { VITE_API_URL } = import.meta.env
  const baseUrl = VITE_API_URL && VITE_API_URL !== '/' ? VITE_API_URL : window.location.origin
  const normalizedBase = baseUrl.replace(/\/$/, '')
  return `${normalizedBase}${BASE_URL}/history/export/${taskId}/download`
}

/** 下载历史导出文件（带鉴权） */
export function fetchHistoryExportDownload(taskId: string) {
  return request.get<Blob>({
    url: `${BASE_URL}/history/export/${taskId}/download`,
    responseType: 'blob'
  })
}

/** 兼容旧函数：查询历史数据 */
export function fetchHistoryData(params: Api.DeviceMonitor.HistorySearchParams) {
  return fetchHistoryQuery(params)
}

/** 兼容旧函数：获取设备点位列表 */
export function fetchDevicePoints(deviceId: number) {
  return fetchHistoryPoints({ deviceId })
}

// ==================== 系统日志 ====================

/** 获取日志列表 */
export function fetchLogList(params: Api.DeviceMonitor.LogSearchParams) {
  const { pageNo, pageSize } = normalizePaginationParams(params)

  return request.get<Api.DeviceMonitor.LogList>({
    url: `${BASE_URL}/log/list`,
    params: {
      pageNo,
      pageSize,
      startTime: params.startTime,
      endTime: params.endTime,
      level: params.level,
      type: params.type,
      kind: params.kind,
      source: params.source,
      serviceName: params.serviceName,
      deviceCode: params.deviceCode,
      operatorName: params.operatorName,
      requestId: params.requestId,
      traceId: params.traceId,
      resultStatus: params.resultStatus,
      keyword: params.keyword
    }
  })
}

/** 获取日志统计 */
export function fetchLogStats() {
  return request.get<Api.DeviceMonitor.LogStats>({
    url: `${BASE_URL}/log/stats`
  })
}

/** 获取日志清理策略 */
export function fetchLogPolicy() {
  return request.get<Api.DeviceMonitor.LogPolicy>({
    url: `${BASE_URL}/log/policy`
  })
}

/** 更新日志清理策略 */
export function updateLogPolicy(params: Api.DeviceMonitor.LogPolicyUpdateParams) {
  return request.put<Api.DeviceMonitor.LogPolicy>({
    url: `${BASE_URL}/log/policy`,
    data: params
  })
}

/** 切换 DEBUG 日志开关 */
export function toggleLogDebug(params: Api.DeviceMonitor.LogDebugToggleParams) {
  return request.post<Api.DeviceMonitor.LogPolicy>({
    url: `${BASE_URL}/log/debug`,
    data: params
  })
}

/** 手动清理日志 */
export function cleanupLogs(params: Api.DeviceMonitor.LogCleanupParams) {
  return request.post<Api.DeviceMonitor.LogCleanupResult>({
    url: `${BASE_URL}/log/cleanup`,
    data: params
  })
}

export interface TdengineLogFile {
  name: string
  size: number
  mtime: string
  compressed: boolean
  tailSupported: boolean
  kind: string
}

/** TDengine 服务日志文件列表（数据库日志页） */
export function fetchTdengineLogFiles() {
  return request.get<{ enabled: boolean; reason: string; files: TdengineLogFile[] }>({
    url: `${BASE_URL}/log/tdengine/files`
  })
}

/** 读取指定 TDengine 日志文件的最后若干行（数据库日志页） */
export function fetchTdengineLogTail(file: string, lines = 300) {
  return request.get<{
    enabled: boolean
    reason: string
    file: string
    lines: string[]
    truncated: boolean
    maxBytes: number
  }>({
    url: `${BASE_URL}/log/tdengine/tail`,
    params: { file, lines }
  })
}

/** 前端日志上报（带限流）
 * @returns accepted 是否接受, remaining 剩余配额, reason 拒绝原因
 */
export function reportClientLog(params: Api.DeviceMonitor.LogClientReportParams) {
  return request.post<{ accepted: boolean; remaining: number; reason?: string }>({
    url: `${BASE_URL}/log/client`,
    data: params,
    skipAuthorization: true,
    showErrorMessage: false
  })
}

// ==================== 仪表盘（评审版） ====================

/** 获取工作台概览 */
export function fetchDashboardOverview() {
  return request.get<Api.DeviceMonitor.DashboardOverview>({
    url: `${BASE_URL}/dashboard/overview`
  })
}

/** 获取设备类型分布 */
export function fetchDashboardDeviceTypeDistribution(topN: number = 10) {
  return request.get<Api.DeviceMonitor.DashboardDeviceTypeDistribution>({
    url: `${BASE_URL}/dashboard/device-type-distribution`,
    params: { topN }
  })
}

/** 获取最近事件 */
export function fetchDashboardRecentEvents(
  params: Api.DeviceMonitor.DashboardRecentEventParams = {}
) {
  return request.get<Api.DeviceMonitor.DashboardRecentEventList>({
    url: `${BASE_URL}/dashboard/recent-events`,
    params: {
      limit: params.limit ?? 10,
      eventType: params.eventType ?? 'all'
    }
  })
}

/** 获取趋势数据 */
export function fetchDashboardTrend(params: Api.DeviceMonitor.DashboardTrendParams = {}) {
  return request.get<Api.DeviceMonitor.DashboardTrend>({
    url: `${BASE_URL}/dashboard/trend`,
    params: {
      rangeType: params.rangeType ?? 'last24h',
      granularity: params.granularity ?? 'auto',
      metricKeys: params.metricKeys ?? ['collectCount', 'alarmCount', 'onlineRate']
    }
  })
}

/** 获取设备连接健康 */
export function fetchDashboardDeviceConnectionHealth(
  params: Api.DeviceMonitor.DashboardDeviceConnectionHealthParams = {}
) {
  return request.get<Api.DeviceMonitor.DashboardDeviceConnectionHealth>({
    url: `${BASE_URL}/dashboard/device-connection-health`,
    params: {
      statRange: params.statRange ?? 'last24h',
      topN: params.topN ?? 10,
      includeOffline: params.includeOffline ?? true
    }
  })
}

/** 获取告警分布 */
export function fetchDashboardAlarmDistribution(
  params: Api.DeviceMonitor.DashboardAlarmDistributionParams = {}
) {
  return request.get<Api.DeviceMonitor.DashboardAlarmDistribution>({
    url: `${BASE_URL}/dashboard/alarm-distribution`,
    params: {
      statRange: params.statRange ?? 'last24h',
      groupBy: params.groupBy ?? 'alarmType',
      topN: params.topN ?? 6,
      includeOther: params.includeOther ?? true
    }
  })
}

// ==================== 兼容旧仪表盘函数（过渡期） ====================

/** 兼容旧函数：获取统计卡片 */
export async function fetchDashboardStats() {
  const overview = await fetchDashboardOverview()
  return mapOverviewToStats(overview)
}

/** 兼容旧函数：获取设备类型分布 */
export async function fetchDeviceTypeDistribution() {
  const data = await fetchDashboardDeviceTypeDistribution()
  return mapDistributionGroupsToLegacy(data)
}

/** 兼容旧函数：获取驱动连接状态（由设备连接健康映射） */
export async function fetchDriverConnectionStatus() {
  const data = await fetchDashboardDeviceConnectionHealth({ topN: 10, includeOffline: true })

  return (data.list || []).map((item) => ({
    driverName: item.deviceCode,
    status: item.connectStatus === 'unstable' ? 'offline' : (item.connectStatus as Api.DeviceMonitor.ConnectStatus),
    statusText: item.connectStatus === 'online' ? '在线' : '离线'
  })) as Api.DeviceMonitor.DriverConnectionStatus[]
}

/** 兼容旧函数：获取最近告警 */
export async function fetchRecentAlarms(limit: number = 10) {
  const data = await fetchDashboardRecentEvents({ limit, eventType: 'alarm' })

  return (data.list || []).map((item) => ({
    id: Number(item.eventId) || 0,
    time: item.eventTime,
    device: item.deviceName || item.deviceCode,
    type: item.eventType,
    content: item.content,
    status: item.status === 'handled' ? 'handled' : 'unhandled'
  })) as Api.DeviceMonitor.RecentAlarm[]
}
