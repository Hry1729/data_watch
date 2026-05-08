<!-- 实时监控核心页面 -->
<template>
  <div class="realtime-page art-full-height">
    <!-- 顶部控制栏 -->
    <ElCard class="control-bar-card">
      <div class="control-bar">
        <div class="control-left">
          <span class="device-label">当前设备</span>
          <ElButton class="device-btn" @click="openDeviceSelector">
            <span class="device-name">{{ currentDevice?.deviceName || currentDevice?.deviceCode || '未选择' }}</span>
            <ElIcon class="device-arrow"><ArrowRight /></ElIcon>
          </ElButton>
        </div>
        <div class="point-selector-box" @click="openPointSelector">
          <template v-if="selectedPoints.length > 0">
            <span class="point-tags-text">{{ pointTagsText }}</span>
          </template>
          <span v-else class="point-placeholder">点击选择监控点位</span>
        </div>
        <div class="control-right">
          <ElButton type="primary" :disabled="!canStartMonitor" @click="startMonitor">
            开始监控
          </ElButton>
          <ElButton type="danger" :disabled="!isMonitoring" @click="stopMonitor">
            停止监控
          </ElButton>
          <ElButton :disabled="selectedPoints.length === 0" @click="clearSelection">清空选择</ElButton>
          <ElButton :disabled="!isMonitoring" @click="handleExport">导出</ElButton>
          <div class="ws-status" :class="{ connected: wsConnected }">
            <span class="ws-dot"></span>
            <span>{{ wsConnected ? 'WebSocket已连接' : 'WebSocket未连接' }}</span>
          </div>
          <ElSelect v-model="refreshRate" class="refresh-select" size="small">
            <ElOption label="0.5秒" :value="500" />
            <ElOption label="1秒" :value="1000" />
            <ElOption label="2秒" :value="2000" />
            <ElOption label="5秒" :value="5000" />
          </ElSelect>
        </div>
      </div>
    </ElCard>

    <!-- 主内容区 -->
    <div class="main-content">
      <!-- 中央趋势图区域 -->
      <ElCard class="chart-card">
        <div class="chart-header">
          <span class="chart-title">点位实时趋势</span>
          <div class="chart-actions">
            <ElButton
              size="small"
              :class="['chart-type-btn', { active: chartType === 'line' }]"
              @click="chartType = 'line'"
            >
              折线图
            </ElButton>
            <ElButton
              size="small"
              :class="['chart-type-btn', { active: chartType === 'status' }]"
              @click="chartType = 'status'"
            >
              状态图
            </ElButton>
            <ElButton text @click="toggleChartFullscreen">
              <ArtSvgIcon icon="ri:fullscreen-line" />
            </ElButton>
            <ElButton text @click="openChartSettings">
              <ArtSvgIcon icon="ri:settings-3-line" />
            </ElButton>
          </div>
        </div>

        <!-- 图例 -->
        <div v-if="selectedPoints.length > 0" class="chart-legend">
          <span
            v-for="point in selectedPoints"
            :key="point.varSeq"
            class="legend-item"
            :style="{ color: getPointColor(point.varSeq) }"
          >
            {{ point.pointName }}({{ getPointUnit(point) }})
          </span>
        </div>

        <!-- Perspective 图表容器 -->
        <div v-if="isMonitoring && hasData" ref="perspectiveRef" class="perspective-container"></div>
        <ElEmpty v-else-if="!isMonitoring" description="点击开始监控以查看实时趋势" :image-size="120" />
        <ElEmpty v-else description="暂无数据" :image-size="120" />
      </ElCard>

      <!-- 右侧实时点位卡片容器 -->
      <ElCard class="point-cards-panel">
        <template #header>
          <div class="panel-header">
            <span class="panel-title">实时监测点</span>
            <ElButton v-if="selectedPoints.length > 0" text type="primary" size="small" @click="openPointSelector">
              编辑
            </ElButton>
          </div>
        </template>

        <div v-if="selectedPoints.length > 0" class="point-cards-list">
          <div v-for="point in selectedPoints" :key="point.varSeq" class="point-card">
            <div class="point-card-name">{{ point.pointName }}</div>
            <div class="point-card-address">{{ point.ioAddress }}</div>
            <div class="point-card-value" :style="{ color: getPointColor(point.varSeq) }">
              {{ formatPointValue(point) }}
            </div>
          </div>
        </div>
        <div v-else class="empty-points">
          <ArtSvgIcon icon="ri:cursor-line" class="empty-icon" />
          <p class="empty-text">暂未选择监控点位</p>
          <p class="empty-hint">请从上方选择需要监控的变量</p>
          <ElButton type="primary" @click="openPointSelector">添加监控点位</ElButton>
        </div>
      </ElCard>
    </div>

    <!-- 底部状态栏 -->
    <ElCard class="status-bar-card">
      <div class="status-bar">
        <div class="status-item">
          <span class="status-icon success">✅</span>
          <span>采集服务运行正常</span>
        </div>
        <div class="status-item">
          <span class="status-icon success">🟢</span>
          <span>TDengine {{ tdengineConnected ? '已连接' : '未连接' }}</span>
        </div>
        <div class="status-item">
          <span class="status-icon success">🧩</span>
          <span>Perspective 绘图{{ perspectiveReady ? '更新正常' : '未就绪' }}</span>
        </div>
        <div class="status-item">
          <span class="status-icon time">⏱</span>
          <span>最后同步时间：{{ lastSyncTime || '--' }}</span>
        </div>
      </div>
    </ElCard>

    <!-- 设备选择器抽屉 -->
    <ElDrawer v-model="deviceSelectorVisible" title="选择设备" direction="rtl" size="360px">
      <div class="device-selector-content">
        <ElInput v-model="deviceSearchKeyword" placeholder="搜索设备编号" prefix-icon="Search" clearable class="device-search" />
        <div class="device-list">
          <div
            v-for="device in filteredDeviceList"
            :key="device.deviceId"
            class="device-item"
            :class="{ active: currentDevice?.deviceId === device.deviceId }"
            @click="selectDevice(device)"
          >
            <span class="device-item-code">{{ device.deviceCode }}</span>
            <span class="device-item-name">{{ device.deviceName || '' }}</span>
          </div>
        </div>
      </div>
    </ElDrawer>

    <!-- 点位选择器抽屉 -->
    <ElDrawer v-model="pointSelectorVisible" title="选择监控点位" direction="rtl" size="480px">
      <div class="selector-content">
        <div class="selector-item">
          <label class="selector-label">选择设备</label>
          <ElSelect v-model="tempDeviceId" placeholder="请选择设备" filterable class="selector-control" @change="loadSelectorPoints">
            <ElOption v-for="device in deviceList" :key="device.deviceId" :label="device.deviceName || device.deviceCode" :value="device.deviceId" />
          </ElSelect>
        </div>
        <div class="selector-item">
          <label class="selector-label">选择点位</label>
          <ElInput v-model="pointSearchKeyword" placeholder="搜索点位名称或地址" prefix-icon="Search" clearable class="point-search" />
          <div class="point-checkbox-list">
            <ElCheckbox
              v-for="point in filteredSelectorPoints"
              :key="point.varSeq"
              :label="point.varSeq"
              :model-value="tempSelectedVarSeqs.includes(point.varSeq)"
              @change="(val: CheckboxValueType) => handlePointSelect(point.varSeq, Boolean(val))"
            >
              <div class="point-checkbox-content">
                <span class="point-checkbox-name">{{ point.pointName }}</span>
                <span class="point-checkbox-address">{{ point.ioAddress }}</span>
              </div>
            </ElCheckbox>
          </div>
          <div v-if="selectorPoints.length > 0" class="point-actions">
            <ElButton text type="primary" size="small" @click="selectAllInSelector">全选</ElButton>
            <ElButton text size="small" @click="clearAllInSelector">清空</ElButton>
          </div>
        </div>
      </div>
      <template #footer>
        <ElButton @click="pointSelectorVisible = false">取消</ElButton>
        <ElButton type="primary" @click="confirmPointSelection">确认</ElButton>
      </template>
    </ElDrawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, shallowRef, watch } from 'vue'
import type { CheckboxValueType } from 'element-plus'
import { Search, ArrowRight } from '@element-plus/icons-vue'
import ArtSvgIcon from '@/components/core/base/art-svg-icon/index.vue'
import { fetchMonitorDeviceTree, fetchRealtimeInit, getRealtimeWebSocketUrl } from '@/api/device-monitor'
import {
  ensurePerspectiveViewerRuntime,
  mapPerspectiveViewerRecords,
  type PerspectiveTableClient,
  type PerspectiveViewerClient,
  type PerspectiveWorkerClient
} from '../history/perspective-runtime'

defineOptions({ name: 'RealtimeMonitor' })

interface PointInfo {
  varSeq: number
  pointName: string
  ioAddress: string
  dataType?: string
  unit?: string
}

interface DeviceInfo {
  deviceId: number
  deviceCode: string
  deviceName?: string
}

interface RealtimeDataRecord {
  timestamp: Date
  varSeq: number
  pointName: string
  value: number | string
}

// 颜色配置
const POINT_COLORS = ['#4a6cf7', '#18a47a', '#f59e0b', '#8b5cf6', '#ec4899', '#06b6d4', '#84cc16', '#f97316']
const POINT_UNITS: Record<string, string> = {
  高度: 'm',
  位置: 'm',
  电流: 'A',
  电压: 'V',
  温度: '°C',
  压力: 'MPa',
  速度: 'm/s',
  状态: ''
}

// 状态
const deviceList = ref<DeviceInfo[]>([])
const currentDevice = ref<DeviceInfo | null>(null)
const selectedPoints = ref<PointInfo[]>([])
const realtimeValues = ref<Map<number, number | string>>(new Map())
const realtimeRecords = ref<RealtimeDataRecord[]>([])
const isMonitoring = ref(false)
const wsConnected = ref(false)
const refreshRate = ref(1000)
const chartType = ref<'line' | 'status'>('line')
const lastSyncTime = ref('')
const tdengineConnected = ref(true)
const perspectiveReady = ref(false)

// 设备选择器
const deviceSelectorVisible = ref(false)
const deviceSearchKeyword = ref('')

// 点位选择器
const pointSelectorVisible = ref(false)
const selectorPoints = ref<PointInfo[]>([])
const tempSelectedVarSeqs = ref<number[]>([])
const tempDeviceId = ref<number | undefined>()
const pointSearchKeyword = ref('')

// Perspective
const perspectiveRef = ref<HTMLElement>()
const perspectiveWorker = shallowRef<PerspectiveWorkerClient | null>(null)
const perspectiveTable = shallowRef<PerspectiveTableClient | null>(null)
const perspectiveViewer = shallowRef<PerspectiveViewerClient | null>(null)

// WebSocket
const websocket = ref<WebSocket | null>(null)
const reconnectTimer = ref<ReturnType<typeof setTimeout> | null>(null)
const heartbeatTimer = ref<ReturnType<typeof setInterval> | null>(null)
const reconnectAttempts = ref(0)

// 数据窗口配置
const MAX_RECORDS = 600 // 约10分钟数据(1秒刷新)

// 计算属性
const canStartMonitor = computed(() => currentDevice.value !== null && selectedPoints.value.length > 0)
const hasData = computed(() => realtimeRecords.value.length > 0)

const pointTagsText = computed(() => {
  if (selectedPoints.value.length === 0) return ''
  const names = selectedPoints.value.map((p) => p.pointName)
  const maxLen = 50
  let text = ''
  for (const name of names) {
    if (text.length + name.length + 2 > maxLen) {
      text += '...'
      break
    }
    text += (text ? '、' : '') + name
  }
  return text
})

const filteredDeviceList = computed(() => {
  if (!deviceSearchKeyword.value) return deviceList.value
  const keyword = deviceSearchKeyword.value.toLowerCase()
  return deviceList.value.filter((d) => d.deviceCode.toLowerCase().includes(keyword))
})

const filteredSelectorPoints = computed(() => {
  if (!pointSearchKeyword.value) return selectorPoints.value
  const keyword = pointSearchKeyword.value.toLowerCase()
  return selectorPoints.value.filter(
    (p) => p.pointName.toLowerCase().includes(keyword) || p.ioAddress.toLowerCase().includes(keyword)
  )
})

// 工具函数
const getPointColor = (varSeq: number) => {
  const index = selectedPoints.value.findIndex((p) => p.varSeq === varSeq)
  return POINT_COLORS[index % POINT_COLORS.length]
}

const getPointUnit = (point: PointInfo) => {
  for (const [key, unit] of Object.entries(POINT_UNITS)) {
    if (point.pointName.includes(key)) return unit
  }
  return ''
}

const formatPointValue = (point: PointInfo) => {
  const value = realtimeValues.value.get(point.varSeq)
  if (value === undefined) return '--'
  const unit = getPointUnit(point)
  if (typeof value === 'number') {
    return `${value.toFixed(2)}${unit ? ' ' + unit : ''}`
  }
  return `${value}${unit ? ' ' + unit : ''}`
}

const formatTimestamp = () => {
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())} ${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}.${String(now.getMilliseconds()).padStart(3, '0')}`
}

// 设备与点位加载
const loadDeviceTree = async () => {
  try {
    const tree = await fetchMonitorDeviceTree()
    const devices: DeviceInfo[] = []
    tree?.forEach((group) => {
      group.children?.forEach((device) => {
        devices.push({
          deviceId: device.deviceId,
          deviceCode: device.deviceCode || device.name || '',
          deviceName: device.deviceName
        })
      })
    })
    deviceList.value = devices

    // 默认选择第一个设备
    if (devices.length > 0) {
      currentDevice.value = devices[0]
      await loadSelectorPoints(devices[0].deviceId)
    }
  } catch (error) {
    console.error('加载设备列表失败:', error)
  }
}

const loadSelectorPoints = async (deviceId?: number) => {
  selectorPoints.value = []
  if (!deviceId) return

  try {
    const snapshot = await fetchRealtimeInit({ deviceId })
    selectorPoints.value = (snapshot.data || []).map((item) => ({
      varSeq: item.varSeq,
      pointName: item.pointName,
      ioAddress: item.ioAddress,
      dataType: item.dataType
    }))
  } catch (error) {
    console.error('加载点位列表失败:', error)
  }
}

// 设备选择
const openDeviceSelector = () => {
  deviceSelectorVisible.value = true
}

const selectDevice = async (device: DeviceInfo) => {
  currentDevice.value = device
  selectedPoints.value = []
  selectorPoints.value = []
  realtimeValues.value.clear()
  deviceSelectorVisible.value = false
  await loadSelectorPoints(device.deviceId)
}

// 点位选择
const openPointSelector = () => {
  tempDeviceId.value = currentDevice.value?.deviceId
  if (tempDeviceId.value) {
    void loadSelectorPoints(tempDeviceId.value)
  }
  tempSelectedVarSeqs.value = selectedPoints.value.map((p) => p.varSeq)
  pointSelectorVisible.value = true
}

const handlePointSelect = (varSeq: number, checked: boolean) => {
  if (checked) {
    if (!tempSelectedVarSeqs.value.includes(varSeq)) {
      tempSelectedVarSeqs.value.push(varSeq)
    }
  } else {
    tempSelectedVarSeqs.value = tempSelectedVarSeqs.value.filter((v) => v !== varSeq)
  }
}

const selectAllInSelector = () => {
  tempSelectedVarSeqs.value = selectorPoints.value.map((p) => p.varSeq)
}

const clearAllInSelector = () => {
  tempSelectedVarSeqs.value = []
}

const confirmPointSelection = () => {
  if (tempDeviceId.value) {
    const device = deviceList.value.find((d) => d.deviceId === tempDeviceId.value)
    if (device) {
      currentDevice.value = device
    }
  }
  selectedPoints.value = selectorPoints.value.filter((p) => tempSelectedVarSeqs.value.includes(p.varSeq))
  pointSelectorVisible.value = false
}

const clearSelection = () => {
  selectedPoints.value = []
}

// 监控控制
const startMonitor = async () => {
  if (!canStartMonitor.value) return
  isMonitoring.value = true
  realtimeRecords.value = []
  realtimeValues.value.clear()

  await initPerspectiveViewer()
  connectWebSocket()
}

const stopMonitor = () => {
  isMonitoring.value = false
  closeWebSocket()
}

// WebSocket 连接
const connectWebSocket = () => {
  if (!isMonitoring.value) return

  closeWebSocket()

  try {
    websocket.value = new WebSocket(getRealtimeWebSocketUrl())

    websocket.value.onopen = () => {
      wsConnected.value = true
      reconnectAttempts.value = 0
      startHeartbeat()

      // 订阅当前设备
      if (currentDevice.value) {
        sendWsMessage({ action: 'subscribe', channel: 'realtime', deviceIds: [currentDevice.value.deviceId] })
      }
    }

    websocket.value.onmessage = (event) => {
      if (event.data === 'pong') return
      try {
        const payload = JSON.parse(event.data)
        handleWsMessage(payload)
      } catch (error) {
        console.warn('解析WebSocket消息失败:', error)
      }
    }

    websocket.value.onerror = () => {
      console.error('WebSocket错误')
    }

    websocket.value.onclose = () => {
      wsConnected.value = false
      clearHeartbeat()
      scheduleReconnect()
    }
  } catch (error) {
    console.error('建立WebSocket连接失败:', error)
    scheduleReconnect()
  }
}

const closeWebSocket = () => {
  clearReconnectTimer()
  clearHeartbeat()

  if (websocket.value) {
    websocket.value.onopen = null
    websocket.value.onmessage = null
    websocket.value.onerror = null
    websocket.value.onclose = null
    websocket.value.close(1000, 'manual close')
    websocket.value = null
  }

  wsConnected.value = false
}

const sendWsMessage = (message: Record<string, unknown>) => {
  if (websocket.value?.readyState === WebSocket.OPEN) {
    websocket.value.send(JSON.stringify(message))
  }
}

const startHeartbeat = () => {
  clearHeartbeat()
  heartbeatTimer.value = setInterval(() => {
    sendWsMessage({ action: 'ping' })
  }, 15000)
}

const clearHeartbeat = () => {
  if (heartbeatTimer.value) {
    clearInterval(heartbeatTimer.value)
    heartbeatTimer.value = null
  }
}

const scheduleReconnect = () => {
  if (!isMonitoring.value || reconnectTimer.value) return
  const delay = Math.min(30000, 1000 * Math.pow(2, reconnectAttempts.value))
  reconnectAttempts.value += 1
  reconnectTimer.value = setTimeout(() => {
    reconnectTimer.value = null
    connectWebSocket()
  }, delay)
}

const clearReconnectTimer = () => {
  if (reconnectTimer.value) {
    clearTimeout(reconnectTimer.value)
    reconnectTimer.value = null
  }
}

// WebSocket 消息处理
const handleWsMessage = (payload: Record<string, unknown>) => {
  if (payload.action === 'pong' || payload.type === 'pong') return

  // 更新实时值
  const data = payload.data as Array<Record<string, unknown>> | undefined
  if (Array.isArray(data)) {
    const now = new Date()
    data.forEach((item) => {
      const varSeq = item.varSeq as number
      const value = item.value
      const pointName = item.pointName as string

      realtimeValues.value.set(varSeq, value as number | string)

      // 添加到趋势记录
      if (selectedPoints.value.some((p) => p.varSeq === varSeq)) {
        realtimeRecords.value.push({
          timestamp: now,
          varSeq,
          pointName,
          value: typeof value === 'number' ? value : Number(value) || 0
        })
      }
    })

    // 限制记录数量
    if (realtimeRecords.value.length > MAX_RECORDS) {
      realtimeRecords.value = realtimeRecords.value.slice(-MAX_RECORDS)
    }

    lastSyncTime.value = formatTimestamp()
    void updatePerspectiveTable()
  }
}

// Perspective 图表
const initPerspectiveViewer = async () => {
  if (!perspectiveRef.value) return

  try {
    const runtime = await ensurePerspectiveViewerRuntime()
    perspectiveReady.value = true

    if (!perspectiveWorker.value) {
      perspectiveWorker.value = await runtime.perspective.worker()
    }

    if (!perspectiveViewer.value) {
      perspectiveRef.value.innerHTML = ''
      const viewer = document.createElement('perspective-viewer') as PerspectiveViewerClient
      viewer.setAttribute('theme', 'Pro Light')
      viewer.style.display = 'block'
      viewer.style.width = '100%'
      viewer.style.height = '100%'
      perspectiveRef.value.appendChild(viewer)
      perspectiveViewer.value = viewer
    }
  } catch (error) {
    console.error('初始化Perspective失败:', error)
    perspectiveReady.value = false
  }
}

const updatePerspectiveTable = async () => {
  if (!perspectiveWorker.value || !perspectiveViewer.value || realtimeRecords.value.length === 0) return

  try {
    // 删除旧表
    if (perspectiveTable.value) {
      await perspectiveTable.value.delete()
      perspectiveTable.value = null
    }

    // 创建新表
    const records = mapPerspectiveViewerRecords(realtimeRecords.value as unknown as Array<{
      timestamp: string
      varSeq: number
      pointName: string
      value: number | string
      ioAddress?: string
      deviceId?: number
      deviceCode?: string
    }>)
    perspectiveTable.value = await perspectiveWorker.value.table(records as unknown as Record<string, unknown>[])

    // 配置图表
    const config = buildViewerConfig()
    await perspectiveViewer.value.load(perspectiveTable.value)
    await perspectiveViewer.value.restore(config)
    await perspectiveViewer.value.flush()
  } catch (error) {
    console.error('更新Perspective图表失败:', error)
  }
}

const buildViewerConfig = () => {
  if (chartType.value === 'status') {
    return {
      plugin: 'Datagrid',
      group_by: [],
      split_by: [],
      columns: ['timestamp', 'pointName', 'value'],
      sort: [['timestamp', 'desc']]
    }
  }

  return {
    plugin: 'Y Line',
    group_by: ['timestamp'],
    split_by: ['pointName'],
    columns: ['value'],
    sort: [['timestamp', 'asc']],
    aggregates: { value: 'last' }
  }
}

const toggleChartFullscreen = async () => {
  const container = perspectiveRef.value?.parentElement
  if (!container) return

  try {
    if (!document.fullscreenElement) {
      await container.requestFullscreen()
    } else {
      await document.exitFullscreen()
    }
  } catch (error) {
    console.error('切换全屏失败:', error)
  }
}

const openChartSettings = () => {
  // 预留图表设置功能
}

const handleExport = () => {
  // 预留导出功能
}

// 生命周期
onMounted(() => {
  void loadDeviceTree()
})

onUnmounted(() => {
  stopMonitor()

  void (async () => {
    if (perspectiveViewer.value) {
      try {
        await perspectiveViewer.value.delete()
      } catch (e) {
        console.warn('清理Perspective失败:', e)
      }
    }
    if (perspectiveWorker.value?.terminate) {
      perspectiveWorker.value.terminate()
    }
    perspectiveWorker.value = null
    perspectiveViewer.value = null
    perspectiveTable.value = null
  })()
})

// 监听图表类型变化
watch(chartType, async () => {
  if (perspectiveViewer.value && perspectiveTable.value) {
    await perspectiveViewer.value.restore(buildViewerConfig())
  }
})
</script>

<style scoped lang="scss">
.realtime-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  min-height: 0;
}

// 顶部控制栏
.control-bar-card {
  border: 1px solid var(--art-card-border);
  border-radius: 18px;

  :deep(.el-card__body) {
    padding: 16px 22px;
  }
}

.control-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  align-items: center;
  justify-content: space-between;
}

.control-left {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
}

.device-label {
  font-size: 13px;
  color: #6b7a90;
}

.device-btn {
  display: inline-flex;
  gap: 6px;
  align-items: center;
  padding: 6px 14px;
  border: 1px solid #e0e5ee;
  border-radius: 10px;
  background: #fff;
  cursor: pointer;
  transition: border-color 0.2s, box-shadow 0.2s;

  &:hover {
    border-color: #c5cde0;
    box-shadow: 0 2px 8px rgba(74, 108, 247, 0.08);
  }
}

.device-name {
  font-size: 14px;
  font-weight: 500;
  color: #4a6cf7;
}

.device-arrow {
  font-size: 12px;
  color: #8b95a7;
}

.point-selector-box {
  flex: 1;
  min-width: 200px;
  max-width: 400px;
  padding: 6px 16px;
  border: 1px solid #e0e5ee;
  border-radius: 10px;
  background: #fff;
  cursor: pointer;
  font-size: 13px;
  transition: border-color 0.2s, box-shadow 0.2s;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;

  &:hover {
    border-color: #c5cde0;
    box-shadow: 0 2px 8px rgba(74, 108, 247, 0.08);
  }
}

.point-tags-text {
  color: #4a6cf7;
}

.point-placeholder {
  color: #8b95a7;
}

.control-right {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: center;
}

.ws-status {
  display: flex;
  gap: 6px;
  align-items: center;
  padding: 6px 12px;
  border-radius: 16px;
  background: #f5f5f5;
  font-size: 13px;
  color: #8b95a7;

  &.connected {
    background: #e8f5e9;
    color: #18a47a;
  }
}

.ws-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #d44f63;

  .connected & {
    background: #18a47a;
  }
}

.refresh-select {
  width: 100px;
}

// 主内容区
.main-content {
  flex: 1;
  display: grid;
  grid-template-columns: 1fr 320px;
  gap: 16px;
  min-height: 0;
}

.chart-card,
.point-cards-panel {
  border: 1px solid var(--art-card-border);
  border-radius: 18px;
  height: 100%;
}

.chart-card {
  :deep(.el-card__body) {
    display: flex;
    flex-direction: column;
    height: 100%;
    padding: 22px;
  }
}

.point-cards-panel {
  :deep(.el-card__header) {
    padding: 16px 22px;
    border-bottom: 1px solid #edf0f5;
  }

  :deep(.el-card__body) {
    height: calc(100% - 53px);
    padding: 16px;
    overflow-y: auto;
  }
}

// 图表区域
.chart-header {
  display: flex;
  gap: 16px;
  align-items: center;
  margin-bottom: 16px;
}

.chart-title {
  font-size: 16px;
  font-weight: 600;
  color: #1d2433;
}

.chart-actions {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-left: auto;
}

.chart-type-btn {
  padding: 6px 14px;
  border: 1px solid #e0e5ee;
  border-radius: 8px;
  background: #fff;
  font-size: 13px;
  color: #516075;
  transition: background 0.2s, color 0.2s;

  &:hover {
    background: #f7f9fc;
  }

  &.active {
    background: #4a6cf7;
    border-color: #4a6cf7;
    color: #fff;
  }
}

.chart-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  justify-content: center;
  margin-bottom: 12px;
  font-size: 13px;
}

.legend-item {
  display: flex;
  align-items: center;
  gap: 4px;
}

.perspective-container {
  flex: 1;
  min-height: 400px;
  border-radius: 16px;
  overflow: hidden;
  background: linear-gradient(180deg, #ffffff 0%, #f8fbff 100%);
  border: 1px solid #eef1f6;

  perspective-viewer {
    width: 100%;
    height: 100%;
  }
}

// 右侧点位卡片
.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.panel-title {
  font-size: 15px;
  font-weight: 600;
  color: #1d2433;
}

.point-cards-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.point-card {
  padding: 14px 16px;
  border-radius: 14px;
  background: linear-gradient(135deg, #f8fbff 0%, #f4f8ff 100%);
  border: 1px solid #edf0f5;
  transition: box-shadow 0.2s ease;

  &:hover {
    box-shadow: 0 4px 12px rgba(74, 108, 247, 0.1);
  }
}

.point-card-name {
  font-size: 14px;
  font-weight: 500;
  color: #1d2433;
  margin-bottom: 4px;
}

.point-card-address {
  font-size: 12px;
  color: #8b95a7;
  margin-bottom: 8px;
}

.point-card-value {
  font-size: 20px;
  font-weight: 600;
}

.empty-points {
  display: flex;
  flex-direction: column;
  gap: 12px;
  align-items: center;
  justify-content: center;
  height: 100%;
  min-height: 200px;
  color: #8b95a7;
  text-align: center;
}

.empty-icon {
  font-size: 48px;
  opacity: 0.5;
}

.empty-text {
  margin: 0;
  font-size: 15px;
  font-weight: 500;
  color: #6b7a90;
}

.empty-hint {
  margin: 0;
  font-size: 13px;
}

// 底部状态栏
.status-bar-card {
  border: 1px solid var(--art-card-border);
  border-radius: 18px;

  :deep(.el-card__body) {
    padding: 12px 22px;
  }
}

.status-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.status-item {
  display: flex;
  gap: 8px;
  align-items: center;
  font-size: 13px;
  color: #516075;
  flex: 1;
  justify-content: center;
}

.status-icon {
  &.success {
    color: #18a47a;
  }

  &.time {
    color: #8b95a7;
  }
}

// 设备选择器
.device-selector-content {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.device-search {
  :deep(.el-input__wrapper) {
    border-radius: 10px;
  }
}

.device-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 400px;
  overflow-y: auto;
}

.device-item {
  display: flex;
  gap: 12px;
  align-items: center;
  padding: 12px 16px;
  border-radius: 10px;
  border: 1px solid #e5e9f2;
  background: #fff;
  cursor: pointer;
  transition: border-color 0.2s, background 0.2s;

  &:hover {
    border-color: #c5cde0;
    background: #f7f9fc;
  }

  &.active {
    border-color: #4a6cf7;
    background: rgba(74, 108, 247, 0.06);
  }
}

.device-item-code {
  font-size: 14px;
  font-weight: 600;
  color: #1d2433;
}

.device-item-name {
  font-size: 13px;
  color: #8b95a7;
}

// 点位选择器
.selector-content {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.selector-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.selector-label {
  font-size: 14px;
  font-weight: 500;
  color: #1d2433;

  .selected-count {
    margin-left: 4px;
    color: #4a6cf7;
    font-weight: 400;
  }
}

.selector-control {
  width: 100%;
}

.point-search {
  margin-bottom: 8px;
}

.point-checkbox-list {
  max-height: 280px;
  padding: 8px;
  overflow-y: auto;
  border: 1px solid #e5e9f2;
  border-radius: 12px;
}

.point-checkbox-content {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.point-checkbox-name {
  font-size: 14px;
  color: #1d2433;
}

.point-checkbox-address {
  font-size: 12px;
  color: #8a94a6;
}

.point-actions {
  display: flex;
  gap: 16px;
  justify-content: flex-end;
  padding: 8px 0;
}

// 响应式
@media (max-width: 1200px) {
  .main-content {
    grid-template-columns: 1fr;
  }

  .point-cards-panel {
    max-height: 300px;
  }
}

@media (max-width: 768px) {
  .control-bar {
    flex-direction: column;
    align-items: flex-start;
  }

  .control-right {
    width: 100%;
    justify-content: flex-start;
  }

  .status-bar {
    flex-direction: column;
    align-items: flex-start;
    gap: 8px;
  }

  .status-item {
    justify-content: flex-start;
  }
}
</style>
