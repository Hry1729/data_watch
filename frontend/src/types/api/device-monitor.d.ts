/**
 * 设备监控平台 - 类型定义
 */

declare namespace Api {
  namespace DeviceMonitor {
    // ==================== 通用分页兼容 ====================

    interface CompatiblePaginationParams {
      /** 评审版分页字段 */
      pageNo?: number
      pageSize?: number
      /** 兼容历史分页字段 */
      current?: number
      size?: number
    }

    interface PaginatedResult<T> {
      list: T[]
      total: number
      pageNo?: number
      pageSize?: number
      current?: number
      size?: number
      pageNum?: number
    }

    interface PaginatedResultWithStats<T> extends PaginatedResult<T> {
      /** 可用数量统计（驱动专用） */
      availableCount?: number
    }

    // ==================== 驱动相关 ====================

    /** 驱动列表项 */
    interface DriverListItem {
      id: number
      driverName: string
      driverDesc: string
      readWriteType: string
      updateTime: string
      status?: string
      boundDeviceCount?: number
    }

    /** 驱动列表查询参数 */
    interface DriverSearchParams extends CompatiblePaginationParams {
      keyword?: string
      driverName?: string
    }

    /** 驱动列表响应 */
    type DriverList = PaginatedResultWithStats<DriverListItem>

    // ==================== 设备相关 ====================

    /** 设备状态枚举 */
    type DeviceStatus = 'enabled' | 'disabled'

    /** 连接状态枚举 */
    type ConnectStatus = 'online' | 'offline' | 'unstable'

    /** 数据状态枚举 */
    type DataStatus = 'normal' | 'abnormal' | 'nodata'

    /** 设备列表项 */
    interface DeviceListItem {
      id: number
      deviceType: string
      deviceCode: string
      deviceName: string
      driverName: string
      deviceStatus: DeviceStatus
      connectStatus: ConnectStatus
      connectUpdateTime: string
      dataStatus: DataStatus
      dataUpdateTime: string
      description?: string
    }

    /** 设备分组（兼容旧版） */
    interface DeviceGroup {
      name: string
      key: string
      count: number
    }

    /** 设备分组树节点（评审版） */
    interface DeviceGroupTreeNode {
      key: string
      name: string
      count?: number
      nodeType?: 'group' | 'device'
      deviceId?: number
      deviceCode?: string
      deviceName?: string
      connectStatus?: ConnectStatus
      children?: DeviceGroupTreeNode[]
    }

    /** 设备列表查询参数 */
    interface DeviceSearchParams extends CompatiblePaginationParams {
      keyword?: string
      deviceType?: string
      deviceCode?: string
      deviceName?: string
      deviceStatus?: DeviceStatus
      connectStatus?: ConnectStatus
    }

    /** 设备列表响应 */
    type DeviceList = PaginatedResultWithStats<DeviceListItem>

    /** 新增/编辑设备参数 */
    interface DeviceForm {
      id?: number
      deviceType: string
      deviceCode: string
      deviceName: string
      driverName: string
      deviceStatus: DeviceStatus
      description?: string
    }

    interface ImportErrorItem {
      rowNumber: number
      reason: string
    }

    interface ImportResult {
      success: number
      fail: number
      errors?: ImportErrorItem[]
    }

    // ==================== 点位相关 ====================

    /** 数据类型枚举 */
    type DataType = 'BOOL' | 'INT' | 'DINT' | 'REAL' | 'STRING' | 'WORD' | 'DWORD'

    /** 点位列表项 */
    interface PointListItem {
      id: number
      deviceId: number
      deviceCode: string
      varSeq: number
      pointName: string
      ioAddress: string
      dataType: DataType
      description: string
      isReadonly: boolean
      driverName: string
      createTime?: string
      updateTime?: string
    }

    /** 点位设备树节点（评审版） */
    interface PointDeviceTreeNode {
      key: string
      name: string
      nodeType?: 'group' | 'device'
      count?: number
      deviceId?: number
      deviceCode?: string
      groupId?: number
      children?: PointDeviceTreeNode[]
    }

    /** 点位列表查询参数 */
    interface PointSearchParams extends CompatiblePaginationParams {
      keyword?: string
      deviceId?: number
      deviceCode?: string
      groupId?: number
      varSeq?: number
      pointName?: string
      ioAddress?: string
    }

    /** 点位列表响应 */
    type PointList = PaginatedResultWithStats<PointListItem>

    /** 新增/编辑点位参数 */
    interface PointForm {
      id?: number
      locatorDeviceId?: number
      locatorVarSeq?: number
      deviceId: number
      deviceCode: string
      varSeq: number
      pointName: string
      ioAddress: string
      dataType: DataType
      description: string
      isReadonly: boolean
      driverName: string
      scanRate?: number
      isStorage?: boolean
    }

    // ==================== 实时监控相关 ====================

    /** 实时监控设备树节点 */
    interface MonitorDeviceTreeNode {
      key: string
      name: string
      nodeType?: 'group' | 'device'
      status?: ConnectStatus
      connectStatus?: ConnectStatus
      deviceId?: number
      deviceCode?: string
      deviceName?: string
      children?: MonitorDeviceTreeNode[]
    }

    /** 实时数据项 */
    interface RealtimeData {
      pointId?: number
      varSeq: number
      pointName: string
      ioAddress: string
      value: string | number | boolean
      updateTime: string
      quality?: number | null
      dataType?: string
    }

    /** 设备实时状态/初始化快照 */
    interface DeviceRealtimeStatus {
      deviceId: number
      deviceCode: string
      deviceName: string
      driverName: string
      connectStatus: ConnectStatus
      lastCollectTime: string
      data: RealtimeData[]
    }

    /** 实时初始化参数 */
    interface RealtimeInitParams {
      deviceId?: number
      groupCode?: string
    }

    /** WS 客户端消息：订阅 */
    interface RealtimeSubscribeMessage {
      action: 'subscribe'
      channel: 'realtime'
      deviceIds: number[]
    }

    /** WS 客户端消息：取消订阅 */
    interface RealtimeUnsubscribeMessage {
      action: 'unsubscribe'
      channel: 'realtime'
      deviceIds: number[]
    }

    /** WS 客户端消息：心跳 */
    interface RealtimePingMessage {
      action: 'ping'
    }

    type RealtimeClientMessage =
      | RealtimeSubscribeMessage
      | RealtimeUnsubscribeMessage
      | RealtimePingMessage

    /** WS 服务端推送（兼容多种事件包） */
    interface RealtimePushPayload {
      action?: string
      channel?: string
      type?: string
      event?: string
      deviceId?: number
      timestamp?: string
      data?: DeviceRealtimeStatus | RealtimeData[]
      devices?: DeviceRealtimeStatus[]
      device_code?: string
      group_code?: string
      values?: Record<string, { v: string; t: number; q: number }>
    }

    // ==================== 历史数据相关 ====================

    /** 历史查询设备项 */
    interface HistoryDeviceItem {
      id: number | null
      name: string
      deviceCode?: string
      deviceName?: string
      groupId?: number
      groupName?: string
      typeCode?: string
      typeName?: string
    }

    /** 设备类型项（用于级联选择） */
    interface DeviceTypeItem {
      typeCode: string
      typeName: string
      deviceCount: number
    }

    /** 历史查询有效设备项（id非null） */
    interface HistoryValidDeviceItem {
      id: number
      name: string
      deviceCode: string
      deviceName: string
      groupId: number
      groupName: string
      typeCode: string
      typeName: string
    }

    /** 历史查询设备分组（按类型） */
    interface HistoryDeviceTypeGroup {
      typeCode: string
      typeName: string
      devices: HistoryValidDeviceItem[]
    }

    /** 历史查询点位项 */
    interface HistoryPointItem {
      varSeq: number
      pointName: string
      ioAddress: string
    }

    /** 历史点位查询参数 */
    interface HistoryPointSearchParams {
      deviceId: number
    }

    /** 历史数据查询参数 */
    interface HistorySearchParams {
      deviceId: number
      varSeqs: number[]
      startTime: string
      endTime: string
      interval?: string
      aggregate?: string
    }

    /** 历史图表数据响应 */
    interface HistoryChartResponse {
      interval: string
      series: {
        varSeq: number
        name: string
        unit: string
        dataType?: string
        data: [string, number | null][]
      }[]
    }

    /** 历史数据项 */
    interface HistoryDataItem {
      time: string
      [key: string]: string | number
    }

    /** 历史数据响应 */
    interface HistoryDataResponse {
      times: string[]
      points: {
        varSeq: number
        pointName: string
        values: (string | number)[]
      }[]
    }

    /** 历史原始数据项 */
    interface HistoryRawItem {
      ts: string
      value: number | string | boolean | null
      quality?: number
    }

    /** 历史原始数据响应 */
    interface HistoryRawResponse {
      list: HistoryRawItem[]
      nextCursor: string | null
    }

    /** 历史矩阵表列定义 */
    interface HistoryMatrixColumn {
      prop: string
      label: string
      varSeq?: number
      unit?: string
      dataType?: string
      fixed?: 'left' | 'right'
    }

    /** 历史矩阵表响应 */
    interface HistoryRawMatrixResponse {
      columns: HistoryMatrixColumn[]
      rows: Record<string, any>[]
      nextCursor: string | null
      meta: {
        alignGranularity: '100ms' | '500ms' | '1s'
        fillMode: 'none'
        pageSize: number
      }
    }

    /** 历史导出任务响应 */
    interface HistoryExportTask {
      taskId: string
    }

    /** 历史导出状态响应 */
    interface HistoryExportStatus {
      taskId: string
      status: 'pending' | 'processing' | 'completed' | 'failed'
      progress: number
      message?: string
      downloadUrl?: string
      fileName?: string
      fileSize?: number
      createTime?: string
      completeTime?: string
    }

    /** 历史导出参数 */
    interface HistoryExportParams extends HistorySearchParams {}

    // ==================== 系统日志相关 ====================

    /** 日志级别 */
    type LogLevel = 'DEBUG' | 'INFO' | 'WARN' | 'ERROR' | 'CRITICAL'

    /** 日志类型 */
    type LogType = 'system' | 'auth' | 'operation' | 'collect' | 'storage' | 'frontend' | 'security' | 'alarm'

    /** 日志种类 */
    type LogKind = 'system' | 'operation' | 'all'

    /** 日志列表项 */
    interface LogListItem {
      id: number
      kind: LogKind
      time: string
      level: LogLevel
      type: LogType
      source: string
      serviceName: string
      moduleName?: string
      actionName?: string
      operatorName?: string
      deviceCode?: string
      content: string
      resultStatus?: string
      durationMs?: number
      requestId?: string
      traceId?: string
      detail?: Record<string, any>
      exceptionStack?: string
    }

    /** 日志列表查询参数 */
    interface LogSearchParams extends CompatiblePaginationParams {
      keyword?: string
      startTime?: string
      endTime?: string
      level?: LogLevel
      type?: LogType
      kind?: LogKind
      source?: string
      serviceName?: string
      deviceCode?: string
      operatorName?: string
      requestId?: string
      traceId?: string
      resultStatus?: 'success' | 'failed' | 'denied'
    }

    /** 日志列表响应 */
    type LogList = PaginatedResult<LogListItem>

    /** 日志统计 */
    interface LogStats {
      todayTotal: number
      yesterdayTotal: number
      todayWarnTotal: number
      todayErrorTotal: number
      todayCriticalTotal: number
      debugEnabled: boolean
      debugRemainingSeconds: number
      debugEnabledUntil?: string
      totalSizeMb: number
      maxTotalMb: number
      lastCleanupTime?: string
      lastCleanupDeletedTotal: number
      levelTrend: Array<{
        time: string
        info: number
        warn: number
        error: number
        critical: number
        debug: number
      }>
      typeRatio: Array<{
        type: string
        count: number
        ratio: number
      }>
      sourceRatio: Array<{
        source: string
        count: number
        ratio: number
      }>
    }

    /** 日志清理策略 */
    interface LogPolicy {
      enabled: boolean
      retentionDays: number
      maxTotalMb: number
      cleanupTime: string
      capacityCheckIntervalMinutes: number
      batchSize: number
      debugDbEnabled: boolean
      debugEnabledUntil?: string
      debugReason?: string
      debugEnabledBy?: string
      lastCleanupTime?: string
      updateBy?: string
      updateTime?: string
    }

    /** 日志策略更新参数 */
    interface LogPolicyUpdateParams {
      enabled?: boolean
      retentionDays?: number
      maxTotalMb?: number
      cleanupTime?: string
      capacityCheckIntervalMinutes?: number
      batchSize?: number
    }

    /** DEBUG 开关参数 */
    interface LogDebugToggleParams {
      enabled: boolean
      durationMinutes?: number
      reason?: string
    }

    /** 日志清理参数 */
    interface LogCleanupParams {
      mode: 'policy' | 'beforeTime' | 'range' | 'currentFilter'
      beforeTime?: string
      startTime?: string
      endTime?: string
      force?: boolean
    }

    /** 日志清理结果 */
    interface LogCleanupResult {
      deletedOperationLogs: number
      deletedSystemLogs: number
      sizeBeforeMb: number
      sizeAfterMb: number
      triggeredBy: string
      triggerMode: string
      force: boolean
    }

    /** 前端日志上报参数 */
    interface LogClientReportParams {
      level: 'DEBUG' | 'WARN' | 'ERROR' | 'CRITICAL'
      type: 'frontend'
      source: 'frontend-runtime' | 'frontend-http' | 'frontend-vue' | 'frontend-resource' | 'frontend-manual'
      content: string
      detail?: Record<string, any>
      exceptionStack?: string
      routePath?: string
      userAgent?: string
      traceId?: string
      clientIp?: string
    }

    // ==================== 仪表盘统计相关（评审版） ====================

    /** 告警较上周期比较 */
    interface AlarmComparePrev {
      countDiff: number
      ratioDiff: number
    }

    /** 工作台概览 */
    interface DashboardOverview {
      deviceTotal: number
      onlineDeviceTotal: number
      offlineDeviceTotal: number
      riskDeviceTotal: number
      pointTotal: number
      systemAlertTotal: number
      systemAlertComparePrev: AlarmComparePrev
      generatedAt: string
    }

    /** 设备类型分布分组 */
    interface DashboardDeviceTypeDistributionGroup {
      typeCode: string
      typeName: string
      deviceCount: number
      onlineCount: number
      ratio: number
    }

    /** 设备类型分布 */
    interface DashboardDeviceTypeDistribution {
      total: number
      groups: DashboardDeviceTypeDistributionGroup[]
    }

    /** 最近事件 */
    interface DashboardRecentEvent {
      eventId: string
      eventTime: string
      deviceId: number
      deviceCode: string
      deviceName: string
      eventType: 'alarm' | 'collect' | 'connection' | string
      eventLevel: string
      content: string
      status: string
    }

    interface DashboardRecentEventParams {
      limit?: number
      eventType?: 'alarm' | 'collect' | 'connection' | 'all'
    }

    interface DashboardRecentEventList {
      list: DashboardRecentEvent[]
    }

    interface DashboardTrendParams {
      rangeType?: 'last24h' | 'today' | 'last7d'
      granularity?: 'auto' | '5m' | '15m' | '1h' | '1d'
      metricKeys?: string[]
    }

    interface DashboardTrendSeries {
      metricKey: string
      metricName: string
      unit: string
      data: number[]
    }

    interface DashboardTrend {
      query: {
        rangeType: string
        granularity: string
      }
      timeAxis: string[]
      series: DashboardTrendSeries[]
      summary: {
        collectCountTotal: number
        alarmCountTotal: number
        onlineRateAvg: number
      }
    }

    interface DashboardDeviceConnectionHealthParams {
      statRange?: 'last1h' | 'last24h' | 'last7d'
      topN?: number
      includeOffline?: boolean
    }

    interface DashboardDeviceConnectionHealthItem {
      deviceId: number
      deviceCode: string
      deviceName: string
      deviceType: string
      connectStatus: ConnectStatus
      exceptionCount: number
      timeoutCount: number
      reconnectCount: number
      successRate: number
      lastHeartbeatTime: string
    }

    interface DashboardDeviceConnectionHealth {
      query: {
        statRange: string
        topN: number
        includeOffline: boolean
      }
      overview: {
        onlineDevices: number
        offlineDevices: number
        riskDevices: number
      }
      list: DashboardDeviceConnectionHealthItem[]
    }

    interface DashboardAlarmDistributionParams {
      statRange?: 'last1h' | 'last24h' | 'last7d'
      groupBy?: 'alarmType' | 'deviceType' | 'severity'
      topN?: number
      includeOther?: boolean
    }

    interface DashboardAlarmDistributionGroup {
      groupKey: string
      groupName: string
      count: number
      ratio: number
    }

    interface DashboardAlarmDistribution {
      query: {
        statRange: string
        groupBy: string
        topN: number
      }
      totalAlarmCount: number
      groups: DashboardAlarmDistributionGroup[]
      comparePrev: {
        countDiff: number
        ratioDiff: number
      }
    }

    // ==================== 点位分组与实时窗口（新接口）====================

    /** 点位分组信息 */
    interface PointGroup {
      group_code: string
      group_name: string | null
      active_point_count: number
      max_point_count: number
      status: string
    }

    /** 设备变量（新接口） */
    interface DeviceVariable {
      id: number
      var_name: string
      var_type: string
      scan_rate: number
      is_alarm: number
      is_storage: number
      group_index: number
      td_column_name: string
    }

    /** 实时窗口帧 */
    interface RealtimeWindowFrame {
      device_code: string
      group_code: string
      ts: number
      scan_rate: number
      values: Record<string, string>
    }

    // ==================== 兼容旧页面的视图模型 ====================

    /** 统计卡片数据（兼容旧字段） */
    interface DashboardStats {
      totalDevices: number
      onlineDevices: number
      totalPoints: number
      alarmCount: number
    }

    /** 设备类型分布（兼容旧字段） */
    interface DeviceTypeDistribution {
      name: string
      value: number
    }

    /** 驱动连接状态（旧页面兼容，后续可删除） */
    interface DriverConnectionStatus {
      driverName: string
      status: ConnectStatus
      statusText: string
    }

    /** 最近告警（旧页面兼容，后续可删除） */
    interface RecentAlarm {
      id: number
      time: string
      device: string
      type: string
      content: string
      status: 'handled' | 'unhandled'
    }
  }
}
