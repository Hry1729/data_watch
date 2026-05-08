<!-- 历史数据查询页 -->
<template>
  <div class="history-page">
    <ElCard class="panel-card viewer-card">
      <div class="card-head card-head--split">
        <div>
          <p class="card-title">历史分析</p>
          <p class="card-subtitle">Perspective Viewer 主渲染</p>
        </div>
        <div class="head-actions">
          <ElButtonGroup size="small">
            <ElButton
              :type="viewerPlugin === 'Y Line' ? 'primary' : 'default'"
              @click="handleViewerPluginChange('Y Line')"
            >
              Y Line
            </ElButton>
            <ElButton
              :type="viewerPlugin === 'Datagrid' ? 'primary' : 'default'"
              @click="handleViewerPluginChange('Datagrid')"
            >
              Datagrid
            </ElButton>
          </ElButtonGroup>
          <ElButton size="small" :loading="exporting" @click="handleExport">导出 CSV</ElButton>
          <ElButton type="primary" @click="openDrawer">
            <ElIcon><Setting /></ElIcon>
            <span>选择数据</span>
          </ElButton>
        </div>
      </div>

      <div v-if="selectedPoints.length" class="selected-info">
        <div class="selected-header">
          <div class="selected-meta">
            <ElIcon><Clock /></ElIcon>
            <span>{{ searchForm.startTime }} 至 {{ searchForm.endTime }}</span>
          </div>
          <ElTag v-if="activeQueryContext" size="small" effect="plain" type="info">
            Query v{{ activeQueryContext.queryVersion }}
          </ElTag>
        </div>
        <div class="selected-points">
          <ElTag v-for="point in selectedPoints" :key="point.varSeq" class="point-tag">
            {{ point.pointName }}
          </ElTag>
        </div>
      </div>

      <MonitorLoadingPlaceholder
        v-if="loading && !hasResultData"
        title="历史数据加载中"
        description="正在读取历史数据并准备 Perspective 渲染"
        min-height="520px"
        :rows="6"
      />
      <div v-else-if="hasResultData" ref="perspectiveRef" class="perspective-container"></div>
      <ElEmpty v-else :description="emptyDescription" :image-size="120" />
    </ElCard>

    <ElDrawer v-model="drawerVisible" title="数据选择" direction="rtl" size="520px" :close-on-click-modal="false">
      <div class="drawer-content">
        <div class="form-item">
          <label class="form-label">设备</label>
          <ElSelect
            v-model="searchForm.deviceId"
            placeholder="请选择设备"
            filterable
            clearable
            class="form-control"
          >
            <ElOption v-for="item in deviceList" :key="item.id" :label="item.name" :value="item.id" />
          </ElSelect>
        </div>

        <div class="form-item">
          <label class="form-label">时间范围</label>
          <ElDatePicker
            v-model="timeRange"
            type="datetimerange"
            range-separator="至"
            start-placeholder="开始时间"
            end-placeholder="结束时间"
            value-format="YYYY-MM-DD HH:mm:ss"
            class="form-control"
            :shortcuts="timeShortcuts"
          />
        </div>

        <div class="form-item">
          <label class="form-label">
            点位选择
            <span v-if="searchForm.varSeqs.length" class="selected-count">
              (已选 {{ searchForm.varSeqs.length }} 个)
            </span>
          </label>
          <div class="point-selector">
            <ElInput
              v-model="pointSearch"
              placeholder="搜索点位名称或地址..."
              prefix-icon="Search"
              clearable
              class="point-search"
            />
            <div class="point-list">
              <ElCheckbox
                v-for="point in filteredPointList"
                :key="point.varSeq"
                :label="point.varSeq"
                :model-value="searchForm.varSeqs.includes(point.varSeq)"
                class="point-item"
                @change="(val: CheckboxValueType) => handlePointChange(point.varSeq, Boolean(val))"
              >
                <div class="point-info">
                  <span class="point-name">{{ point.pointName }}</span>
                  <span class="point-address">{{ point.ioAddress }}</span>
                </div>
              </ElCheckbox>
              <ElEmpty v-if="!filteredPointList.length && pointList.length" description="未找到匹配的点位" :image-size="60" />
              <ElEmpty v-if="!pointList.length && searchForm.deviceId" description="该设备暂无点位数据" :image-size="60" />
              <div v-if="!searchForm.deviceId" class="point-placeholder">
                <ElIcon><InfoFilled /></ElIcon>
                <span>请先选择设备</span>
              </div>
            </div>
            <div v-if="pointList.length" class="point-actions">
              <ElButton text type="primary" size="small" @click="selectAllPoints">全选</ElButton>
              <ElButton text size="small" @click="clearAllPoints">清空</ElButton>
            </div>
          </div>
        </div>
      </div>

      <template #footer>
        <ElButton @click="drawerVisible = false">取消</ElButton>
        <ElButton type="primary" :loading="loading" @click="handleQuery">确认查询</ElButton>
      </template>
    </ElDrawer>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue'
import type { CheckboxValueType } from 'element-plus'
import { ElMessage } from 'element-plus'
import { Setting, Clock, InfoFilled } from '@element-plus/icons-vue'
import MonitorLoadingPlaceholder from '@/components/device-monitor/MonitorLoadingPlaceholder.vue'
import { saveBlobAs } from '@/utils/file/save-blob'
import {
  fetchHistoryDevices,
  fetchHistoryExport,
  fetchHistoryPoints,
  fetchHistoryQuery
} from '@/api/device-monitor'
import { isHttpError } from '@/utils/http/error'
import {
  buildPerspectiveWebSocketUrl,
  createPerspectiveSessionState,
  nextPerspectiveQueryVersion,
  parsePerspectiveWsEvent,
  resetPerspectiveSession,
  rotatePerspectiveConnectionId,
  type HistoryPerspectiveRecord,
  type HistoryQueryContext,
  type PerspectiveSessionState,
  type PerspectiveSnapshotEvent
} from './perspective-session'
import {
  ensurePerspectiveViewerRuntime,
  mapPerspectiveViewerRecords,
  type PerspectiveTableClient,
  type PerspectiveViewerClient,
  type PerspectiveWorkerClient
} from './perspective-runtime'

defineOptions({ name: 'HistoryData' })

type PointInfo = Api.DeviceMonitor.HistoryPointItem
type DeviceInfo = Api.DeviceMonitor.HistoryDeviceItem
type ViewerPlugin = 'Y Line' | 'Datagrid'

interface HistorySearchForm {
  deviceId?: number
  varSeqs: number[]
  startTime: string
  endTime: string
}

const searchForm = ref<HistorySearchForm>({
  deviceId: undefined,
  varSeqs: [],
  startTime: '',
  endTime: ''
})

const timeRange = ref<[string, string]>(['', ''])
const pointSearch = ref('')
const drawerVisible = ref(false)

const deviceList = ref<DeviceInfo[]>([])
const pointList = ref<PointInfo[]>([])
const selectedPoints = ref<PointInfo[]>([])

const loading = ref(false)
const exporting = ref(false)
const viewerPlugin = ref<ViewerPlugin>('Y Line')
const renderedRecordCount = ref(0)

const perspectiveRef = ref<HTMLElement>()
const perspectiveWorker = shallowRef<PerspectiveWorkerClient | null>(null)
const perspectiveTable = shallowRef<PerspectiveTableClient | null>(null)
const perspectiveViewer = shallowRef<PerspectiveViewerClient | null>(null)
const perspectiveSocket = shallowRef<WebSocket | null>(null)
const perspectiveSession = ref<PerspectiveSessionState>(createPerspectiveSessionState())
const activeQueryContext = ref<HistoryQueryContext | null>(null)
const latestAppliedQueryVersion = ref(0)

let perspectiveMounted = false
let perspectivePingTimer: number | null = null
let connectingPerspectiveSocket: Promise<void> | null = null
let perspectiveRenderTask: Promise<void> = Promise.resolve()
let suppressDisconnectNotice = false

const hasResultData = computed(() => renderedRecordCount.value > 0)
const emptyDescription = computed(() => {
  if (loading.value) {
    return '正在查询历史数据...'
  }

  if (activeQueryContext.value) {
    return '当前查询范围暂无历史数据'
  }

  return '请选择设备和点位后查询数据'
})

const timeShortcuts = [
  {
    text: '最近1小时',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 3600000)
      return [start, end]
    }
  },
  {
    text: '最近6小时',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 21600000)
      return [start, end]
    }
  },
  {
    text: '最近24小时',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 86400000)
      return [start, end]
    }
  },
  {
    text: '最近7天',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 604800000)
      return [start, end]
    }
  },
  {
    text: '最近30天',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 2592000000)
      return [start, end]
    }
  }
]

const filteredPointList = computed(() => {
  if (!pointSearch.value) return pointList.value
  const keyword = pointSearch.value.toLowerCase()
  return pointList.value.filter(
    (point) =>
      point.pointName.toLowerCase().includes(keyword) ||
      point.ioAddress.toLowerCase().includes(keyword)
  )
})

const formatDateTime = (date: Date) => {
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
}

const buildDefaultTimeRange = (days: number = 7): [string, string] => {
  const end = new Date()
  const start = new Date(end.getTime() - days * 24 * 60 * 60 * 1000)
  return [formatDateTime(start), formatDateTime(end)]
}

const buildFileDate = () => {
  const now = new Date()
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

const buildViewerConfig = () => {
  if (viewerPlugin.value === 'Datagrid') {
    return {
      plugin: 'Datagrid',
      group_by: [],
      split_by: [],
      columns: ['timestamp', 'pointName', 'value', 'ioAddress'],
      sort: [['timestamp', 'asc']]
    }
  }

  return {
    plugin: 'Y Line',
    group_by: ['timestamp'],
    split_by: ['pointName'],
    columns: ['value'],
    sort: [['timestamp', 'asc']],
    aggregates: {
      value: 'avg'
    }
  }
}

const buildHistoryErrorMessage = (error: unknown) => {
  if (!isHttpError(error)) {
    return '查询失败'
  }

  if (error.message.includes('time range exceeds')) {
    return '历史查询时间范围不能超过 24 小时'
  }

  if (error.message.includes('varSeqs exceeds the limit') || error.message.includes('pointIds exceeds the limit')) {
    return '单次历史查询最多选择 20 个点位'
  }

  if (error.message.includes('session protection limit')) {
    return '查询结果过大，请缩小时间范围或减少点位后重试'
  }

  return error.message || '查询失败'
}

const buildPerspectiveRecordsFromResponse = (
  deviceId: number,
  result: Api.DeviceMonitor.HistoryDataResponse
) => {
  const device = deviceList.value.find((item) => item.id === deviceId)
  const pointMeta = new Map(pointList.value.map((point) => [point.varSeq, point]))
  const records: HistoryPerspectiveRecord[] = []

  result.times.forEach((timestamp, timeIndex) => {
    result.points.forEach((point) => {
      const rawValue = point.values[timeIndex]
      if (rawValue === null || rawValue === undefined || rawValue === '' || rawValue === '-') {
        return
      }

      const numericValue =
        typeof rawValue === 'number' ? rawValue : Number.parseFloat(String(rawValue))

      if (!Number.isFinite(numericValue)) {
        return
      }

      const pointInfo = pointMeta.get(point.varSeq)
      records.push({
        timestamp,
        deviceId,
        deviceCode: device?.deviceCode || device?.name || '',
        varSeq: point.varSeq,
        pointName: point.pointName,
        ioAddress: pointInfo?.ioAddress || '',
        value: numericValue
      })
    })
  })

  return records
}

const syncSelectionFromSnapshot = (event: PerspectiveSnapshotEvent) => {
  if (!event.records.length) {
    return
  }

  const pointMap = new Map<number, PointInfo>()
  let minTimestamp = event.records[0].timestamp
  let maxTimestamp = event.records[0].timestamp

  event.records.forEach((record) => {
    if (!pointMap.has(record.varSeq)) {
      pointMap.set(record.varSeq, {
        varSeq: record.varSeq,
        pointName: record.pointName,
        ioAddress: record.ioAddress
      })
    }

    if (record.timestamp < minTimestamp) minTimestamp = record.timestamp
    if (record.timestamp > maxTimestamp) maxTimestamp = record.timestamp
  })

  const preservedStartTime =
    activeQueryContext.value?.queryVersion === event.queryVersion
      ? activeQueryContext.value.startTime
      : searchForm.value.startTime || minTimestamp
  const preservedEndTime =
    activeQueryContext.value?.queryVersion === event.queryVersion
      ? activeQueryContext.value.endTime
      : searchForm.value.endTime || maxTimestamp
  const preservedQueryVersion = activeQueryContext.value?.queryVersion || event.queryVersion

  selectedPoints.value = Array.from(pointMap.values())
  searchForm.value.startTime = preservedStartTime
  searchForm.value.endTime = preservedEndTime
  timeRange.value = [preservedStartTime, preservedEndTime]
  activeQueryContext.value = {
    deviceId: event.records[0].deviceId,
    varSeqs: Array.from(pointMap.keys()),
    startTime: preservedStartTime,
    endTime: preservedEndTime,
    queryVersion: preservedQueryVersion,
    perspectiveSessionId: perspectiveSession.value.perspectiveSessionId
  }
}

const clearPerspectivePing = () => {
  if (perspectivePingTimer !== null) {
    window.clearInterval(perspectivePingTimer)
    perspectivePingTimer = null
  }
}

const closePerspectiveSocket = () => {
  clearPerspectivePing()

  if (!perspectiveSocket.value) return

  const socket = perspectiveSocket.value
  perspectiveSocket.value = null
  socket.onopen = null
  socket.onmessage = null
  socket.onerror = null
  socket.onclose = null

  if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
    socket.close()
  }
}

const initPerspectiveViewer = async () => {
  if (!perspectiveRef.value) return

  const runtime = await ensurePerspectiveViewerRuntime()

  if (!perspectiveWorker.value) {
    perspectiveWorker.value = await runtime.perspective.worker()
  }

  if (!perspectiveViewer.value) {
    perspectiveRef.value.innerHTML = ''
    const viewer = document.createElement('perspective-viewer') as PerspectiveViewerClient
    viewer.setAttribute('theme', 'Pro Light')
    viewer.style.display = 'block'
    viewer.style.width = '100%'
    viewer.style.height = '520px'
    perspectiveRef.value.appendChild(viewer)
    perspectiveViewer.value = viewer
  }
}

const destroyPerspectiveViewer = async () => {
  const viewer = perspectiveViewer.value
  perspectiveViewer.value = null

  if (!viewer) return

  try {
    await viewer.delete()
  } catch (error) {
    console.warn('清理 Perspective Viewer 失败，继续重建:', error)
  }

  viewer.remove()
  if (perspectiveRef.value) {
    perspectiveRef.value.innerHTML = ''
  }
}

const deletePerspectiveTable = async () => {
  const table = perspectiveTable.value
  perspectiveTable.value = null

  if (!table) return

  await table.delete()
}

const cleanupPerspectiveResources = async () => {
  await destroyPerspectiveViewer()
  await deletePerspectiveTable()
}

const applyViewerConfig = async () => {
  if (!perspectiveViewer.value) return

  try {
    await perspectiveViewer.value.restore(buildViewerConfig())
  } catch (error) {
    if (viewerPlugin.value !== 'Y Line') {
      throw error
    }

    console.error('Perspective Y Line 渲染失败，回退到 Datagrid:', error)
    viewerPlugin.value = 'Datagrid'
    await perspectiveViewer.value.restore(buildViewerConfig())
    ElMessage.warning('Y Line 渲染失败，已自动切换到 Datagrid')
  }
}

const performPerspectiveRender = async (records: HistoryPerspectiveRecord[]) => {
  renderedRecordCount.value = records.length

  if (!records.length) {
    await cleanupPerspectiveResources()
    return
  }

  await nextTick()
  await initPerspectiveViewer()

  if (!perspectiveWorker.value || !perspectiveViewer.value) return

  if (perspectiveTable.value) {
    await destroyPerspectiveViewer()
    await deletePerspectiveTable()
    await initPerspectiveViewer()
  }

  if (!perspectiveViewer.value) {
    return
  }

  const viewerRecords = mapPerspectiveViewerRecords(records)
  perspectiveTable.value = await perspectiveWorker.value.table(viewerRecords as unknown as Record<string, unknown>[])

  await perspectiveViewer.value.load(perspectiveTable.value)
  await applyViewerConfig()
  await perspectiveViewer.value.flush()
  await perspectiveViewer.value.resize(true)
}

const renderPerspectiveRecords = (records: HistoryPerspectiveRecord[]) => {
  const task = perspectiveRenderTask.then(() => performPerspectiveRender(records))
  perspectiveRenderTask = task.catch(() => undefined)
  return task
}

const applyPerspectiveSnapshot = async (event: PerspectiveSnapshotEvent) => {
  if (event.queryVersion && event.queryVersion < latestAppliedQueryVersion.value) {
    return
  }

  latestAppliedQueryVersion.value = Math.max(latestAppliedQueryVersion.value, event.queryVersion)
  perspectiveSession.value.queryVersion = Math.max(
    perspectiveSession.value.queryVersion,
    event.queryVersion
  )

  syncSelectionFromSnapshot(event)
  await renderPerspectiveRecords(event.records)
}

const ensurePerspectiveSocket = async () => {
  if (!perspectiveMounted) return

  if (perspectiveSocket.value?.readyState === WebSocket.OPEN) {
    return
  }

  if (connectingPerspectiveSocket) {
    return connectingPerspectiveSocket
  }

  rotatePerspectiveConnectionId(perspectiveSession.value)
  suppressDisconnectNotice = false

  connectingPerspectiveSocket = new Promise<void>((resolve) => {
    closePerspectiveSocket()

    const socket = new WebSocket(
      buildPerspectiveWebSocketUrl(
        perspectiveSession.value.perspectiveSessionId,
        perspectiveSession.value.connectionId
      )
    )

    let settled = false
    perspectiveSocket.value = socket

    socket.onopen = () => {
      clearPerspectivePing()
      perspectivePingTimer = window.setInterval(() => {
        if (socket.readyState === WebSocket.OPEN) {
          socket.send(JSON.stringify({ action: 'ping' }))
        }
      }, 15000)

      if (!settled) {
        settled = true
        resolve()
      }
    }

    socket.onmessage = (messageEvent) => {
      let payload: unknown
      try {
        payload = JSON.parse(String(messageEvent.data))
      } catch (error) {
        console.error('解析 Perspective websocket 消息失败:', error)
        return
      }

      const event = parsePerspectiveWsEvent(payload)
      if (!event) return

      if (event.type === 'snapshot') {
        void applyPerspectiveSnapshot(event)
        return
      }

      if (event.type === 'session_restored') {
        ElMessage.success('已恢复当前标签页的历史分析会话')
        return
      }

      if (event.type === 'session_conflict') {
        suppressDisconnectNotice = true
        resetPerspectiveSession(perspectiveSession.value)
        latestAppliedQueryVersion.value = 0
        activeQueryContext.value = null
        selectedPoints.value = []
        searchForm.value.varSeqs = []
        void renderPerspectiveRecords([])
        ElMessage.warning('当前标签页会话冲突，已切换为新会话，请重新查询历史数据')
        void ensurePerspectiveSocket()
        return
      }

      if (event.type === 'error') {
        ElMessage.error(event.message || 'Perspective 会话异常')
      }
    }

    socket.onerror = () => {
      if (!settled) {
        settled = true
        resolve()
      }
    }

    socket.onclose = () => {
      clearPerspectivePing()
      perspectiveSocket.value = null

      if (!settled) {
        settled = true
        resolve()
      }

      if (perspectiveMounted && !suppressDisconnectNotice && activeQueryContext.value) {
        ElMessage.warning('Perspective 会话连接已断开，请重新查询历史数据')
      }
    }
  }).finally(() => {
    connectingPerspectiveSocket = null
  })

  return connectingPerspectiveSocket
}

const openDrawer = () => {
  drawerVisible.value = true
}

const loadDeviceList = async () => {
  try {
    const devices = await fetchHistoryDevices()
    deviceList.value = devices.map((device) => ({
      id: device.id,
      name: device.name || device.deviceName || device.deviceCode || `设备-${device.id}`,
      deviceCode: device.deviceCode,
      deviceName: device.deviceName
    }))
  } catch (error) {
    console.error('加载设备列表失败:', error)
    ElMessage.error('加载设备列表失败')
  }
}

const loadPointList = async (deviceId?: number) => {
  pointList.value = []
  searchForm.value.varSeqs = []
  selectedPoints.value = []

  if (!deviceId) return

  try {
    pointList.value = await fetchHistoryPoints({ deviceId })
  } catch (error) {
    console.error('加载点位列表失败:', error)
    ElMessage.error('加载点位列表失败')
  }
}

const handlePointChange = (varSeq: number, checked: boolean) => {
  if (checked) {
    if (!searchForm.value.varSeqs.includes(varSeq)) {
      searchForm.value.varSeqs.push(varSeq)
    }
  } else {
    searchForm.value.varSeqs = searchForm.value.varSeqs.filter((item) => item !== varSeq)
  }
}

const selectAllPoints = () => {
  searchForm.value.varSeqs = pointList.value.map((point) => point.varSeq)
}

const clearAllPoints = () => {
  searchForm.value.varSeqs = []
}

const handleViewerPluginChange = async (plugin: ViewerPlugin) => {
  viewerPlugin.value = plugin
  if (perspectiveTable.value) {
    await applyViewerConfig()
  }
}

const handleQuery = async () => {
  if (!searchForm.value.deviceId) {
    ElMessage.warning('请选择设备')
    return
  }

  if (!searchForm.value.varSeqs.length) {
    ElMessage.warning('请选择至少一个点位')
    return
  }

  if (timeRange.value && timeRange.value[0] && timeRange.value[1]) {
    searchForm.value.startTime = timeRange.value[0]
    searchForm.value.endTime = timeRange.value[1]
  } else {
    const defaultRange = buildDefaultTimeRange()
    searchForm.value.startTime = defaultRange[0]
    searchForm.value.endTime = defaultRange[1]
    timeRange.value = defaultRange
  }

  loading.value = true
  try {
    await ensurePerspectiveSocket()

    const queryVersion = nextPerspectiveQueryVersion(perspectiveSession.value)
    const result = await fetchHistoryQuery(
      {
        deviceId: searchForm.value.deviceId,
        varSeqs: searchForm.value.varSeqs,
        startTime: searchForm.value.startTime,
        endTime: searchForm.value.endTime
      },
      {
        headers: {
          'X-Perspective-Session-Id': perspectiveSession.value.perspectiveSessionId,
          'X-Perspective-Query-Version': String(queryVersion)
        },
        skipAuthorization: true
      }
    )

    selectedPoints.value = pointList.value.filter((item) => searchForm.value.varSeqs.includes(item.varSeq))
    activeQueryContext.value = {
      deviceId: searchForm.value.deviceId,
      varSeqs: [...searchForm.value.varSeqs],
      startTime: searchForm.value.startTime,
      endTime: searchForm.value.endTime,
      queryVersion,
      perspectiveSessionId: perspectiveSession.value.perspectiveSessionId
    }
    latestAppliedQueryVersion.value = Math.max(latestAppliedQueryVersion.value, queryVersion)

    await renderPerspectiveRecords(
      buildPerspectiveRecordsFromResponse(searchForm.value.deviceId, result)
    )
    drawerVisible.value = false
  } catch (error) {
    console.error('查询失败:', error)
    ElMessage.error(buildHistoryErrorMessage(error))
  } finally {
    loading.value = false
  }
}

const handleExport = async () => {
  if (!searchForm.value.deviceId || !searchForm.value.varSeqs.length) {
    ElMessage.warning('请先完成查询')
    return
  }

  exporting.value = true
  try {
    const blob = await fetchHistoryExport({
      deviceId: searchForm.value.deviceId,
      varSeqs: searchForm.value.varSeqs,
      startTime: searchForm.value.startTime,
      endTime: searchForm.value.endTime
    })

    await saveBlobAs(blob, `历史数据_${buildFileDate()}.csv`)
  } catch (error) {
    console.error('导出失败:', error)
    ElMessage.error('导出失败')
  } finally {
    exporting.value = false
  }
}

watch(
  () => searchForm.value.deviceId,
  (value) => {
    void loadPointList(value)
  }
)

watch(timeRange, (value) => {
  if (value && value[0] && value[1]) {
    searchForm.value.startTime = value[0]
    searchForm.value.endTime = value[1]
  }
})

onMounted(() => {
  perspectiveMounted = true

  const defaultRange = buildDefaultTimeRange()
  timeRange.value = defaultRange
  searchForm.value.startTime = defaultRange[0]
  searchForm.value.endTime = defaultRange[1]

  void loadDeviceList()
  void ensurePerspectiveSocket()
})

onUnmounted(() => {
  perspectiveMounted = false
  suppressDisconnectNotice = true
  closePerspectiveSocket()
  void (async () => {
    await cleanupPerspectiveResources()
    if (perspectiveWorker.value?.terminate) {
      perspectiveWorker.value.terminate()
    }
    perspectiveWorker.value = null
  })()
})
</script>

<style scoped lang="scss">
.history-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.panel-card {
  border: 1px solid var(--art-card-border);
  border-radius: 22px;

  :deep(.el-card__body) {
    display: flex;
    flex-direction: column;
    gap: 16px;
    padding: 22px;
  }
}

.card-head {
  margin-bottom: 4px;
}

.card-head--split {
  display: flex;
  gap: 16px;
  align-items: center;
  justify-content: space-between;
}

.card-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
  color: #1d2433;
}

.card-subtitle {
  margin: 6px 0 0;
  font-size: 13px;
  color: #8a94a6;
}

.head-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-left: auto;
  align-items: center;
  justify-content: flex-end;

  :deep(.el-button-group),
  :deep(.el-button) {
    display: inline-flex;
    align-items: center;
  }
}

.selected-info {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  background: #f7f9fc;
  border-radius: 12px;
}

.selected-header {
  display: flex;
  gap: 12px;
  align-items: center;
  justify-content: space-between;
  color: #516075;
  font-size: 13px;
}

.selected-meta {
  display: flex;
  gap: 8px;
  align-items: center;
}

.selected-points {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.point-tag {
  padding: 6px 12px;
  border-radius: 999px;
}

.perspective-container {
  min-height: 520px;
  border-radius: 16px;
  overflow: hidden;
  background: linear-gradient(180deg, #ffffff 0%, #f8fbff 100%);
  border: 1px solid #eef1f6;

  perspective-viewer {
    width: 100%;
    height: 520px;
  }
}

.drawer-content {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.form-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.form-label {
  font-size: 14px;
  font-weight: 500;
  color: #1d2433;

  .selected-count {
    margin-left: 4px;
    color: #4a6cf7;
    font-weight: 400;
  }
}

.form-control {
  width: 100%;
}

.point-selector {
  border: 1px solid #e5e9f2;
  border-radius: 12px;
  overflow: hidden;
}

.point-search {
  :deep(.el-input__wrapper) {
    border: 0;
    border-bottom: 1px solid #e5e9f2;
    border-radius: 0;
    box-shadow: none;
  }
}

.point-list {
  max-height: 320px;
  padding: 8px;
  overflow-y: auto;
}

.point-item {
  display: flex;
  width: 100%;
  height: auto;
  padding: 10px 12px;
  margin-bottom: 4px;
  border-radius: 8px;

  &:hover {
    background: #f7f9fc;
  }

  :deep(.el-checkbox__label) {
    flex: 1;
  }
}

.point-info {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.point-name {
  font-size: 14px;
  color: #1d2433;
}

.point-address {
  font-size: 12px;
  color: #8a94a6;
}

.point-placeholder {
  display: flex;
  flex-direction: column;
  gap: 8px;
  align-items: center;
  justify-content: center;
  height: 160px;
  color: #8a94a6;
  font-size: 14px;
}

.point-actions {
  display: flex;
  gap: 16px;
  align-items: center;
  justify-content: flex-end;
  padding: 8px 12px;
  background: #f7f9fc;
  border-top: 1px solid #e5e9f2;
}

@media (max-width: 960px) {
  .card-head--split {
    flex-direction: column;
  }

  .head-actions {
    width: 100%;
    justify-content: flex-start;
  }

  .selected-header {
    align-items: flex-start;
    flex-direction: column;
  }
}
</style>
