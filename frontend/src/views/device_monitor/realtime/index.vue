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
        <div class="control-middle">
          <span class="device-label">已选点位</span>
          <div class="point-selector-box" @click="openPointSelector">
            <template v-if="selectedPoints.length > 0">
              <span class="point-tags-text">{{ pointTagsText }}</span>
            </template>
            <span v-else class="point-placeholder">点击选择监控点位</span>
          </div>
        </div>
        <div class="control-right">
          <ElButton type="primary" :disabled="!canStartMonitor" @click="startMonitor">
            开始监控
          </ElButton>
          <ElButton type="danger" :disabled="!isMonitoring" @click="stopMonitor">
            停止监控
          </ElButton>
          <ElButton :disabled="selectedPoints.length === 0" @click="clearSelection">清空选择</ElButton>
          <ElButton :disabled="!hasData" @click="handleExport">导出</ElButton>
          <div class="ws-status" :class="{ connected: wsConnected }">
            <span class="ws-dot"></span>
            <span>{{ wsConnected ? 'WebSocket已连接' : 'WebSocket未连接' }}</span>
          </div>
          <span class="device-label">图表刷新</span>
          <ElSelect v-model="refreshRate" class="refresh-select">
            <ElOption label="0.25秒" :value="250" />
            <ElOption label="0.1秒" :value="100" />
            <ElOption label="0.5秒" :value="500" />
            <ElOption label="1秒" :value="1000" />
            <ElOption label="2秒" :value="2000" />
            <ElOption label="5秒" :value="5000" />
            <ElOption label="10秒" :value="10000" />
            <ElOption label="30秒" :value="30000" />
          </ElSelect>
        </div>
      </div>
    </ElCard>

    <!-- 设备离线横幅 -->
    <div v-if="(!wsConnected || deviceOffline) && isMonitoring" class="offline-banner">
      <span>{{ !wsConnected ? 'WebSocket 连接已断开，正在重连...' : '设备数据离线，等待采集恢复...' }}</span>
    </div>

    <!-- 点位分组选择器 -->
    <ElCard v-if="currentDevice && pointGroups.length > 0" class="group-tab-card">
      <div class="group-tab-bar">
        <span class="group-tab-label">点位分组：</span>
        <div class="group-tabs">
          <button
            v-for="g in pointGroups"
            :key="g.group_code"
            :class="['group-tab-btn', { active: activeGroupCode === g.group_code }]"
            @click="switchPointGroup(g.group_code)"
          >
            {{ g.group_code }}
            <span class="group-tab-count">{{ g.active_point_count }}/{{ g.max_point_count }}</span>
          </button>
        </div>
      </div>
    </ElCard>

    <!-- 主内容区 -->
    <div class="main-content">
      <!-- 中央趋势图区域 -->
      <ElCard class="chart-card">
        <div class="chart-header">
          <span class="chart-title" title="按收到的最新采样绘制；完整采样请查看历史数据">实时值趋势</span>
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
              :class="['chart-type-btn', { active: chartType === 'area' }]"
              @click="chartType = 'area'"
            >
              面积图
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
            <ElButton text @click="chartSettingsVisible = true">
              <ArtSvgIcon icon="ri:settings-3-line" />
            </ElButton>
          </div>
        </div>

        <!-- 单图表模式：所有点位在同一图表 -->
        <div
          v-show="!separateBoolChart && (isMonitoring || hasData)"
          ref="perspectiveRef"
          class="perspective-container perspective-single"
        ></div>

        <!-- 分离模式：数值型点位趋势图 -->
        <div
          v-show="separateBoolChart && (isMonitoring || hasNumericData) && hasNumericPoints"
          ref="perspectiveNumericRef"
          class="perspective-container perspective-numeric"
        ></div>

        <!-- 分离模式：Bool 型点位趋势图 -->
        <div
          v-show="separateBoolChart && (isMonitoring || hasBoolData) && hasBoolPoints"
          ref="perspectiveBoolRef"
          class="perspective-container perspective-bool"
        ></div>

        <ElEmpty v-if="!isMonitoring && !hasData" description="点击开始监控以查看实时趋势" :image-size="120" />
        <ElEmpty v-if="isMonitoring && !hasData" description="正在连接并等待数据..." :image-size="120" />
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
            <div
              class="point-card-value"
              :class="{ 'is-absent': isPointValueAbsent(point) }"
              :style="pointValueStyle(point)"
            >
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
          <span>TDengine {{ tdengineConnected === null ? '检测中' : tdengineConnected ? '已连接' : '未连接' }}</span>
        </div>
        <div class="status-item">
          <span class="status-icon success">🧩</span>
          <span>Perspective 绘图{{ perspectiveReady ? '更新正常' : '未就绪' }}</span>
        </div>
        <div class="status-item">
          <span class="status-icon time">⏱</span>
          <span>最新采样时间：{{ lastSyncTime || '--' }}</span>
        </div>
      </div>
    </ElCard>

    <!-- 设备选择器抽屉 -->
    <ElDialog v-model="deviceSelectorVisible" title="选择设备" width="400px">
      <div class="device-selector-content">
        <div class="selector-item">
          <label class="selector-label">设备类型</label>
          <ElSelect
            v-model="selectedTypeCode"
            placeholder="请选择设备类型"
            class="selector-control"
            clearable
            @change="handleTypeChange"
          >
            <ElOption
              v-for="type in deviceTypes"
              :key="type.typeCode"
              :label="`${type.typeName} (${type.deviceCount}台)`"
              :value="type.typeCode"
            />
          </ElSelect>
        </div>
        <div class="selector-item">
          <label class="selector-label">设备号</label>
          <ElSelect
            v-model="selectedDeviceId"
            placeholder="请先选择设备类型"
            class="selector-control"
            filterable
            :disabled="!selectedTypeCode"
            @change="handleDeviceChange"
          >
            <ElOption
              v-for="device in filteredDevicesByType"
              :key="device.deviceId"
              :label="device.deviceCode"
              :value="device.deviceId"
            >
              <div class="device-option">
                <span class="device-option-code">{{ device.deviceCode }}</span>
                <span class="device-option-name">{{ device.deviceName }}</span>
              </div>
            </ElOption>
          </ElSelect>
        </div>
        <div v-if="selectedDeviceId && selectedDeviceDetail" class="selected-device-info">
          <div class="info-label">已选设备</div>
          <div class="info-content">
            <span class="info-code">{{ selectedDeviceDetail.deviceCode }}</span>
            <span class="info-name">{{ selectedDeviceDetail.deviceName }}</span>
          </div>
        </div>
      </div>
      <template #footer>
        <ElButton @click="deviceSelectorVisible = false">取消</ElButton>
        <ElButton type="primary" :disabled="!selectedDeviceId" @click="confirmDeviceSelection">确认</ElButton>
      </template>
    </ElDialog>

    <!-- 点位选择器抽屉 -->
    <ElDialog v-model="pointSelectorVisible" title="选择监控点位" width="480px">
      <div class="selector-content">
        <div class="selector-item">
          <label class="selector-label">选择设备</label>
          <ElSelect v-model="tempDeviceId" placeholder="请选择设备" filterable class="selector-control" @change="handleTempDeviceChange">
            <ElOption v-for="device in deviceList" :key="device.deviceId" :label="device.deviceName || device.deviceCode" :value="device.deviceId" />
          </ElSelect>
        </div>
        <div class="selector-item">
          <label class="selector-label">
            选择点位
            <span v-if="tempSelectedVarSeqs.length" class="selected-count">({{ tempSelectedVarSeqs.length }})</span>
          </label>
          <ElInput v-model="pointSearchKeyword" placeholder="搜索点位名称或地址" prefix-icon="Search" clearable class="point-search" />
          <div class="point-checkbox-list">
            <ElCheckbox
              v-for="point in filteredPointList"
              :key="point.varSeq"
              :label="point.varSeq"
              :model-value="tempSelectedVarSeqs.includes(point.varSeq)"
              @change="(val: CheckboxValueType) => handlePointChange(point.varSeq, Boolean(val))"
            >
              <div class="point-checkbox-content">
                <span class="point-checkbox-name">{{ point.pointName }}</span>
                <span class="point-checkbox-address">{{ point.ioAddress }}</span>
              </div>
            </ElCheckbox>
          </div>
          <div v-if="pointList.length > 0" class="point-actions">
            <ElButton text type="primary" size="small" @click="selectAllPoints">全选</ElButton>
            <ElButton text size="small" @click="clearAllPoints">清空</ElButton>
          </div>
        </div>
      </div>
      <template #footer>
        <ElButton @click="pointSelectorVisible = false">取消</ElButton>
        <ElButton type="primary" :disabled="tempSelectedVarSeqs.length === 0" @click="confirmPointSelection">确认</ElButton>
      </template>
    </ElDialog>

    <!-- 图表设置弹窗 -->
    <ElDialog v-model="chartSettingsVisible" title="图表设置" width="400px">
      <div class="settings-content">
        <div class="settings-item">
          <div class="settings-label">Bool 型变量独立图表</div>
          <div class="settings-desc">开启后，Bool 型变量将显示在单独的图表中，避免与数值型变量混合</div>
          <ElSwitch v-model="separateBoolChart" />
        </div>
      </div>
      <template #footer>
        <ElButton @click="chartSettingsVisible = false">关闭</ElButton>
      </template>
    </ElDialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, onActivated, onDeactivated, ref, shallowRef, watch, nextTick } from 'vue'
import { storeToRefs } from 'pinia'
import { useRoute } from 'vue-router'
import { useUserStore } from '@/store/modules/user'
import { expandRealtimeSamples, type RealtimeSample } from '@/utils/realtime-samples'
import { isQualityAbnormal } from '@/utils/realtime-quality'
import {
  formatExportSampleValue,
  formatMissingValueWithQuality,
  formatStoredPointValue,
  isRealtimeValueAbsent,
  normalizeWsPointValue,
  toChartSampleValue,
  toSampleText
} from '@/utils/realtime-point-value'
import type { CheckboxValueType } from 'element-plus'
import { ElMessage } from 'element-plus'
import { Search, ArrowRight } from '@element-plus/icons-vue'
import ArtSvgIcon from '@/components/core/base/art-svg-icon/index.vue'
import { useSettingStore } from '@/store/modules/setting'
import { fetchDeviceTypes, fetchHistoryDevices, fetchRealtimeInit, getRealtimeWebSocketUrl, getAccessToken, getDevicePointGroups } from '@/api/device-monitor'
import {
  ensurePerspectiveViewerRuntime,
  type PerspectiveTableClient,
  type PerspectiveViewerClient,
  type PerspectiveWorkerClient
} from '../history/perspective-runtime'

defineOptions({ name: 'RealtimeMonitor' })

const userStore = useUserStore()
const ownerUserId = userStore.info.userId
const canPersist = () => userStore.isLogin && userStore.info.userId === ownerUserId
const route = useRoute()
let pageActive = false
let pageEpoch = 0
let resumeMonitoring = false
const settingStore = useSettingStore()
const { isDark } = storeToRefs(settingStore)

type DeviceType = Api.DeviceMonitor.DeviceTypeItem

interface PointInfo {
  pointId?: number
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
  typeCode?: string
  typeName?: string
}

interface RealtimeDataRecord {
  timestamp: Date
  varSeq: number
  pointName: string
  /** 缺值/非数值为 null（曲线断点），不能伪造成 0 */
  value: number | null
  /** 有值但非数值（文本/文本布尔）时的原文，导出时原样写出 */
  text?: string
  quality?: number
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
const pointList = ref<PointInfo[]>([])
const realtimeValues = ref<Map<number, number | string>>(new Map())
const realtimeRecords = ref<RealtimeDataRecord[]>([])
const isMonitoring = ref(false)
const wsConnected = ref(false)
const deviceOffline = ref(false)
const refreshRate = ref(250)
const chartType = ref<'line' | 'area' | 'status'>('line')
const chartSettingsVisible = ref(false)
const separateBoolChart = ref(true) // 默认分离 Bool 图表
const lastSyncTime = ref('')
const tdengineConnected = ref<boolean | null>(null)
const perspectiveReady = ref(false)
const perspectiveTheme = computed(() => (isDark.value ? 'Pro Dark' : 'Pro Light'))

// 点位质量（q=0 为 GOOD；非 0 为异常，显示为红色）
const realtimeQuality = ref<Map<number, number>>(new Map())

// 数值与质量每100ms应用，图表按独立的用户设置刷新。
const pendingValues = new Map<number, number | string>()
const pendingQuality = new Map<number, number>()
const pendingChartRecords: RealtimeDataRecord[] = []
let pendingLastSyncTime = ''
let lastSampleTimestamp = 0
let valueFlushTimer: ReturnType<typeof setInterval> | null = null
let chartFlushTimer: ReturnType<typeof setInterval> | null = null
let activeSubscription: { deviceCode: string; groupCode: string; pointIds: string } | null = null
const queuedChartRecords: RealtimeDataRecord[] = []
let chartUpdateTask: Promise<void> | null = null

// 点位分组
const pointGroups = ref<Api.DeviceMonitor.PointGroup[]>([])
const activeGroupCode = ref<string>('g001')

// 状态持久化
const STORAGE_KEY = 'realtime-monitor-state'
const CHART_STATE_KEY = 'realtime-monitor-chart-state'
const MAX_SNAPSHOT_RECORDS = 500 // 保存最近500条数据快照

const saveStateToStorage = () => {
  if (!canPersist()) return
  const state = {
    deviceId: currentDevice.value?.deviceId,
    deviceCode: currentDevice.value?.deviceCode,
    deviceName: currentDevice.value?.deviceName,
    selectedPoints: selectedPoints.value.map((p) => ({
      pointId: p.pointId,
      varSeq: p.varSeq,
      pointName: p.pointName,
      ioAddress: p.ioAddress,
      dataType: p.dataType
    })),
    chartRefreshRate: refreshRate.value,
    chartType: chartType.value,
    separateBoolChart: separateBoolChart.value,
    wasMonitoring: isMonitoring.value // 记录是否正在监控
  }
  localStorage.setItem(STORAGE_KEY, JSON.stringify(state))

  // 保存数据快照（异步）
  saveChartSnapshot()
}

const saveChartSnapshot = async () => {
  try {
    // 保存最近的数据记录
    const dataSnapshot = realtimeRecords.value.slice(-MAX_SNAPSHOT_RECORDS).map((r) => ({
      timestamp: r.timestamp.toISOString(),
      varSeq: r.varSeq,
      pointName: r.pointName,
      value: r.value,
      text: r.text,
      quality: r.quality
    }))

    // 保存图表配置
    const chartConfigs: Record<string, unknown> = {}

    if (perspectiveViewer.value) {
      chartConfigs.single = await perspectiveViewer.value.save()
    }
    if (perspectiveNumericViewer.value) {
      chartConfigs.numeric = await perspectiveNumericViewer.value.save()
    }
    if (perspectiveBoolViewer.value) {
      chartConfigs.bool = await perspectiveBoolViewer.value.save()
    }

    const chartState = {
      data: dataSnapshot,
      configs: chartConfigs,
      timestamp: Date.now()
    }

    if (canPersist()) localStorage.setItem(CHART_STATE_KEY, JSON.stringify(chartState))
  } catch (error) {
    console.warn('保存图表快照失败:', error)
  }
}

const loadStateFromStorage = (): boolean => {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return false

    const state = JSON.parse(raw)
    if (!deviceList.value.some((device) => device.deviceId === Number(state.deviceId))) return false
    if (route.query.deviceId && Number(route.query.deviceId) !== Number(state.deviceId)) return false
    if (state.deviceId && state.selectedPoints?.length > 0) {
      currentDevice.value = {
        deviceId: state.deviceId,
        deviceCode: state.deviceCode || '',
        deviceName: state.deviceName
      }
      selectedPoints.value = state.selectedPoints.map((p: any) => ({
        pointId: p.pointId,
        varSeq: p.varSeq,
        pointName: p.pointName,
        ioAddress: p.ioAddress,
        dataType: p.dataType
      }))
      // Older saved refreshRate values were user-selected; keep them as chart cadence.
      refreshRate.value = state.chartRefreshRate ?? state.refreshRate ?? 250
      chartType.value = state.chartType || 'line'
      separateBoolChart.value = state.separateBoolChart ?? true
      return state.wasMonitoring === true
    }
    return false
  } catch {
    return false
  }
}

const loadChartSnapshot = async (): Promise<boolean> => {
  try {
    const raw = localStorage.getItem(CHART_STATE_KEY)
    if (!raw) return false

    const chartState = JSON.parse(raw)

    // 检查快照是否过期（超过10分钟则不恢复）
    const snapshotAge = Date.now() - (chartState.timestamp || 0)
    if (snapshotAge > 10 * 60 * 1000) {
      localStorage.removeItem(CHART_STATE_KEY)
      return false
    }

    // 恢复数据记录
    if (chartState.data?.length > 0) {
      realtimeRecords.value = chartState.data.map((item: any) => ({
        timestamp: new Date(item.timestamp),
        varSeq: item.varSeq,
        pointName: item.pointName,
        value: toChartSampleValue(item.value),
        text: typeof item.text === 'string' ? item.text : toSampleText(item.value),
        quality: typeof item.quality === 'number' ? item.quality : undefined
      }))

      // 恢复实时值（缺值样本不覆盖为 0）
      realtimeRecords.value.forEach((record) => {
        if (record.value !== null) realtimeValues.value.set(record.varSeq, record.value)
        else if (record.text !== undefined) realtimeValues.value.set(record.varSeq, record.text)
      })
    }

    // 恢复图表布局配置
    if (chartState.configs) {
      if (perspectiveViewer.value && chartState.configs.single) {
        try {
          await perspectiveViewer.value.restore(chartState.configs.single)
        } catch (e) {
          console.warn('恢复主图表配置失败:', e)
        }
      }
      if (perspectiveNumericViewer.value && chartState.configs.numeric) {
        try {
          await perspectiveNumericViewer.value.restore(chartState.configs.numeric)
        } catch (e) {
          console.warn('恢复数值图表配置失败:', e)
        }
      }
      if (perspectiveBoolViewer.value && chartState.configs.bool) {
        try {
          await perspectiveBoolViewer.value.restore(chartState.configs.bool)
        } catch (e) {
          console.warn('恢复布尔图表配置失败:', e)
        }
      }
    }
    applyPerspectiveTheme()

    return true
  } catch (error) {
    console.warn('加载图表快照失败:', error)
    return false
  }
}

const clearStateFromStorage = () => {
  localStorage.removeItem(STORAGE_KEY)
  localStorage.removeItem(CHART_STATE_KEY)
}

// 设备选择器
const deviceSelectorVisible = ref(false)
const deviceTypes = ref<DeviceType[]>([])
const selectedTypeCode = ref<string>('')
const selectedDeviceId = ref<number | undefined>()

// 点位选择器
const pointSelectorVisible = ref(false)
const tempSelectedVarSeqs = ref<number[]>([])
const tempDeviceId = ref<number | undefined>()
const pointSearchKeyword = ref('')

// Perspective
const perspectiveRef = ref<HTMLElement>() // 单图表模式
const perspectiveNumericRef = ref<HTMLElement>() // 分离模式：数值型
const perspectiveBoolRef = ref<HTMLElement>() // 分离模式：Bool 型
const perspectiveWorker = shallowRef<PerspectiveWorkerClient | null>(null)
const isPerspectiveCleaning = ref(false)
const perspectiveTable = shallowRef<PerspectiveTableClient | null>(null) // 单图表模式
const perspectiveViewer = shallowRef<PerspectiveViewerClient | null>(null) // 单图表模式
const perspectiveNumericTable = shallowRef<PerspectiveTableClient | null>(null) // 分离模式：数值型
const perspectiveNumericViewer = shallowRef<PerspectiveViewerClient | null>(null) // 分离模式：数值型
const perspectiveBoolTable = shallowRef<PerspectiveTableClient | null>(null)
const perspectiveBoolViewer = shallowRef<PerspectiveViewerClient | null>(null)

const applyPerspectiveTheme = () => {
  const theme = perspectiveTheme.value
  const viewers = [
    perspectiveViewer.value,
    perspectiveNumericViewer.value,
    perspectiveBoolViewer.value
  ]

  viewers.forEach((viewer) => {
    if (!viewer) return
    viewer.setAttribute('theme', theme)
    void viewer.resize(true).catch(() => undefined)
  })
}

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

// Bool/非Bool 点位分离
const boolVarSeqs = computed(() => {
  return new Set(
    selectedPoints.value.filter((p) => p.dataType?.toUpperCase() === 'BOOL').map((p) => p.varSeq)
  )
})

const hasBoolPoints = computed(() => boolVarSeqs.value.size > 0)
const hasNumericPoints = computed(() => selectedPoints.value.some((p) => p.dataType?.toUpperCase() !== 'BOOL'))

const hasBoolData = computed(() => realtimeRecords.value.some((r) => boolVarSeqs.value.has(r.varSeq)))
const hasNumericData = computed(() => realtimeRecords.value.some((r) => !boolVarSeqs.value.has(r.varSeq)))

// 图表重建标志，防止并发操作
const rebuildingChart = ref(false)

// 重建 Perspective 图表（清空后用现有数据重建）
const rebuildPerspectiveFromRecords = async () => {
  if (rebuildingChart.value) return
  rebuildingChart.value = true

  try {
    if (chartUpdateTask) await chartUpdateTask
    await nextTick()
    await clearPerspectiveTable()
    await initPerspectiveViewer()
    queuedChartRecords.length = 0
    const records = [...realtimeRecords.value]
    if (records.length > 0) {
      await updatePerspectiveTable(records)
    }
  } finally {
    rebuildingChart.value = false
    queuePerspectiveUpdate([])
  }
}

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

// 根据类型过滤的设备列表
const filteredDevicesByType = computed(() => {
  if (!selectedTypeCode.value) return []
  return deviceList.value.filter((d) => d.typeCode === selectedTypeCode.value)
})

// 选中设备的详情
const selectedDeviceDetail = computed(() =>
  deviceList.value.find((d) => d.deviceId === selectedDeviceId.value)
)

// 设备类型变更
const handleTypeChange = () => {
  selectedDeviceId.value = undefined
}

// 设备变更（仅用于显示，不立即确认）
const handleDeviceChange = () => {
  // 预览操作
}

// 确认设备选择
const confirmDeviceSelection = async () => {
  if (!selectedDeviceId.value) return
  const device = deviceList.value.find((d) => d.deviceId === selectedDeviceId.value)
  if (device) {
    await selectDevice(device)
  }
}

const filteredPointList = computed(() => {
  if (!pointSearchKeyword.value) return pointList.value
  const keyword = pointSearchKeyword.value.toLowerCase()
  return pointList.value.filter(
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

const isPointValueAbsent = (point: PointInfo) =>
  isRealtimeValueAbsent(realtimeValues.value.get(point.varSeq))

const formatPointValue = (point: PointInfo) => {
  const value = realtimeValues.value.get(point.varSeq)
  if (isRealtimeValueAbsent(value)) {
    return formatMissingValueWithQuality(realtimeQuality.value.get(point.varSeq))
  }
  return formatStoredPointValue(value, getPointUnit(point))
}

const pointValueStyle = (point: PointInfo) => {
  if (isPointValueAbsent(point)) return undefined
  return {
    color: isQualityAbnormal(realtimeQuality.value.get(point.varSeq))
      ? '#d44f63'
      : getPointColor(point.varSeq)
  }
}

const formatTimestamp = (now: Date) => {
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())} ${pad(now.getHours())}:${pad(now.getMinutes())}:${pad(now.getSeconds())}.${String(now.getMilliseconds()).padStart(3, '0')}`
}

// 设备与点位加载
const loadDeviceTree = async (isCurrent: () => boolean) => {
  try {
    // 加载设备类型列表
    const types = await fetchDeviceTypes()
    if (!isCurrent()) return
    deviceTypes.value = types

    // 加载带类型信息的设备列表
    const devicesWithType = await fetchHistoryDevices()
    if (!isCurrent()) return
    const devices: DeviceInfo[] = devicesWithType
      .filter((d) => d.id !== null)
      .map((d) => ({
        deviceId: d.id!,
        deviceCode: d.deviceCode || '',
        deviceName: d.deviceName || '',
        typeCode: d.typeCode || '',
        typeName: d.typeName || '其他'
      }))
    deviceList.value = devices

    const requestedId = Number(route.query.deviceId)
    const device = requestedId
      ? devices.find((item) => item.deviceId === requestedId)
      : devices[0]
    currentDevice.value = device ?? null
    if (device) {
      await loadPointList(device.deviceId, isCurrent)
      if (!isCurrent()) return
      void loadPointGroups(device.deviceCode, isCurrent)
    }
  } catch (error) {
    if (isCurrent()) console.error('加载设备列表失败:', error)
  }
}

const loadPointList = async (deviceId?: number, isCurrent = () => pageActive && canPersist(), inPointSelector = false) => {
  const groupCode = activeGroupCode.value
  const epoch = pageEpoch
  const canApply = () => isCurrent() && epoch === pageEpoch && groupCode === activeGroupCode.value && (
    inPointSelector
      ? pointSelectorVisible.value && deviceId === tempDeviceId.value
      : deviceId === currentDevice.value?.deviceId
  )
  if (!canApply()) return
  pointList.value = []
  if (!deviceId) return

  try {
    const snapshot = await fetchRealtimeInit({ deviceId, groupCode })
    if (!canApply()) return
    pointList.value = (snapshot.data || []).map((item) => ({
      pointId: item.pointId,
      varSeq: item.varSeq,
      pointName: item.pointName,
      ioAddress: item.ioAddress,
      dataType: item.dataType
    }))
  } catch (error) {
    if (canApply()) console.error('加载点位列表失败:', error)
  }
}

// 加载点位分组
const loadPointGroups = async (deviceCode?: string, isCurrent = () => pageActive && canPersist()) => {
  const canApply = () => isCurrent() && deviceCode === currentDevice.value?.deviceCode
  if (!canApply()) return
  pointGroups.value = []
  if (!deviceCode) return
  try {
    const groups = await getDevicePointGroups(deviceCode)
    if (!canApply()) return
    pointGroups.value = groups || []
  } catch {
    // 新接口不可用时静默失败，不影响现有功能
    if (canApply()) pointGroups.value = []
  }
}

// 切换点位分组
const switchPointGroup = async (groupCode: string) => {
  if (activeGroupCode.value === groupCode) return
  activeGroupCode.value = groupCode
  selectedPoints.value = []
  tempSelectedVarSeqs.value = []
  realtimeValues.value.clear()
  realtimeQuality.value.clear()
  realtimeRecords.value = []
  clearPending()
  lastSyncTime.value = ''
  lastSampleTimestamp = 0
  syncSubscription()
  await clearPerspectiveTable()

  if (currentDevice.value) {
    await loadPointList(currentDevice.value.deviceId)
  }
}

// 设备选择
const openDeviceSelector = async () => {
  // 加载设备类型列表（如果还没有）
  if (deviceTypes.value.length === 0) {
    deviceTypes.value = await fetchDeviceTypes()
  }
  // 重置选择状态
  selectedTypeCode.value = currentDevice.value?.typeCode || ''
  selectedDeviceId.value = currentDevice.value?.deviceId
  deviceSelectorVisible.value = true
}

const selectDevice = async (device: DeviceInfo) => {
  currentDevice.value = device
  selectedPoints.value = []
  pointList.value = []
  realtimeValues.value.clear()
  realtimeQuality.value.clear()
  realtimeRecords.value = []
  clearPending()
  activeGroupCode.value = 'g001'
  deviceOffline.value = false
  lastSyncTime.value = ''
  lastSampleTimestamp = 0
  syncSubscription()
  await clearPerspectiveTable()
  deviceSelectorVisible.value = false
  await loadPointList(device.deviceId)
  void loadPointGroups(device.deviceCode)
}

// 清理 Perspective Table（先删除 viewer，再删除 table）
const clearPerspectiveTable = async () => {
  const wasRebuilding = rebuildingChart.value
  rebuildingChart.value = true
  if (chartUpdateTask) await chartUpdateTask
  queuedChartRecords.length = 0
  // 清理单图表模式：先 viewer 后 table
  if (perspectiveViewer.value) {
    try {
      await perspectiveViewer.value.delete()
    } catch (e) {
      console.warn('清理Viewer失败:', e)
    }
    perspectiveViewer.value = null
  }
  if (perspectiveTable.value) {
    try {
      await perspectiveTable.value.delete()
    } catch (e) {
      console.warn('清理Table失败:', e)
    }
    perspectiveTable.value = null
  }
  // 清理分离模式：数值型 - 先 viewer 后 table
  if (perspectiveNumericViewer.value) {
    try {
      await perspectiveNumericViewer.value.delete()
    } catch (e) {
      console.warn('清理数值型Viewer失败:', e)
    }
    perspectiveNumericViewer.value = null
  }
  if (perspectiveNumericTable.value) {
    try {
      await perspectiveNumericTable.value.delete()
    } catch (e) {
      console.warn('清理数值型Table失败:', e)
    }
    perspectiveNumericTable.value = null
  }
  // 清理分离模式：Bool 型 - 先 viewer 后 table
  if (perspectiveBoolViewer.value) {
    try {
      await perspectiveBoolViewer.value.delete()
    } catch (e) {
      console.warn('清理Bool Viewer失败:', e)
    }
    perspectiveBoolViewer.value = null
  }
  if (perspectiveBoolTable.value) {
    try {
      await perspectiveBoolTable.value.delete()
    } catch (e) {
      console.warn('清理Bool Table失败:', e)
    }
    perspectiveBoolTable.value = null
  }
  if (!wasRebuilding) rebuildingChart.value = false
}

// 点位选择
const openPointSelector = () => {
  tempDeviceId.value = currentDevice.value?.deviceId
  pointSelectorVisible.value = true
  if (tempDeviceId.value) {
    void loadPointList(tempDeviceId.value, undefined, true)
  }
  tempSelectedVarSeqs.value = selectedPoints.value.map((p) => p.varSeq)
}

const handleTempDeviceChange = async (deviceId?: number) => {
  tempSelectedVarSeqs.value = []
  await loadPointList(deviceId, undefined, true)
}

const handlePointChange = (varSeq: number, checked: boolean) => {
  if (checked) {
    if (!tempSelectedVarSeqs.value.includes(varSeq)) {
      tempSelectedVarSeqs.value.push(varSeq)
    }
  } else {
    tempSelectedVarSeqs.value = tempSelectedVarSeqs.value.filter((v) => v !== varSeq)
  }
}

const selectAllPoints = () => {
  tempSelectedVarSeqs.value = pointList.value.map((p) => p.varSeq)
}

const clearAllPoints = () => {
  tempSelectedVarSeqs.value = []
}

const confirmPointSelection = async () => {
  if (tempDeviceId.value) {
    const device = deviceList.value.find((d) => d.deviceId === tempDeviceId.value)
    if (device) {
      currentDevice.value = device
    }
  }
  selectedPoints.value = pointList.value.filter((p) => tempSelectedVarSeqs.value.includes(p.varSeq))
  realtimeRecords.value = []
  clearPending()
  pointSelectorVisible.value = false
  lastSyncTime.value = ''
  lastSampleTimestamp = 0
  syncSubscription()
  await clearPerspectiveTable()
  if (isMonitoring.value && selectedPoints.value.length > 0) {
    await initPerspectiveViewer()
    if (realtimeRecords.value.length > 0) queuePerspectiveUpdate([...realtimeRecords.value])
  }
}

const clearSelection = async () => {
  selectedPoints.value = []
  realtimeValues.value.clear()
  realtimeRecords.value = []
  clearPending()
  await clearPerspectiveTable()
  lastSyncTime.value = ''
  lastSampleTimestamp = 0
  syncSubscription()
  // 清除快照
  localStorage.removeItem(CHART_STATE_KEY)
}

// 监控控制
const startMonitor = async () => {
  if (!canStartMonitor.value) return
  isMonitoring.value = true

  await initPerspectiveViewer()

  // 尝试恢复数据快照
  const snapshotLoaded = await loadChartSnapshot()

  // 如果有恢复的数据，立即写入图表
  if (snapshotLoaded && realtimeRecords.value.length > 0) {
    await updatePerspectiveTable(realtimeRecords.value)
  }

  connectWebSocket()
  startFlushTimer()
}

// pending 缓冲辅助函数
const clearPending = () => {
  pendingValues.clear()
  pendingQuality.clear()
  pendingChartRecords.length = 0
  queuedChartRecords.length = 0
  pendingLastSyncTime = ''
}

const flushPendingValues = () => {
  if (pendingValues.size === 0 && pendingQuality.size === 0 && !pendingLastSyncTime) return
  pendingValues.forEach((v, k) => realtimeValues.value.set(k, v))
  pendingValues.clear()
  pendingQuality.forEach((v, k) => realtimeQuality.value.set(k, v))
  pendingQuality.clear()
  if (pendingLastSyncTime) {
    lastSyncTime.value = pendingLastSyncTime
    pendingLastSyncTime = ''
  }
}

const flushPendingChart = () => {
  if (pendingChartRecords.length === 0) return
  const records = pendingChartRecords.splice(0)
  realtimeRecords.value.push(...records)
  if (realtimeRecords.value.length > MAX_RECORDS) {
    realtimeRecords.value = realtimeRecords.value.slice(-MAX_RECORDS)
  }
  queuePerspectiveUpdate(records)
}

const flushPendingRealtimeUpdates = () => {
  flushPendingValues()
  flushPendingChart()
}

const startFlushTimer = () => {
  if (!pageActive || !canPersist()) return
  if (valueFlushTimer === null) valueFlushTimer = setInterval(flushPendingValues, 100)
  if (chartFlushTimer !== null) clearInterval(chartFlushTimer)
  chartFlushTimer = setInterval(flushPendingChart, refreshRate.value)
}

const stopFlushTimer = () => {
  if (valueFlushTimer !== null) {
    clearInterval(valueFlushTimer)
    valueFlushTimer = null
  }
  if (chartFlushTimer !== null) {
    clearInterval(chartFlushTimer)
    chartFlushTimer = null
  }
  flushPendingRealtimeUpdates()
}

// 用户选择的刷新率只控制图表；数值/质量始终每100ms更新。
watch(refreshRate, () => {
  if (isMonitoring.value) startFlushTimer()
})

const stopMonitor = () => {
  // 停止接收新数据，但不清空历史数据
  isMonitoring.value = false
  deviceOffline.value = false
  stopFlushTimer()
  closeWebSocket()
}

const clearMonitorData = async () => {
  // 清空历史数据和快照（用户主动清空）
  realtimeRecords.value = []
  realtimeValues.value.clear()
  await clearPerspectiveTable()
  localStorage.removeItem(CHART_STATE_KEY)
}

const disconnectForUnmount = () => {
  // 页面卸载时的轻量清理：只断开连接，不清空数据
  isMonitoring.value = false
  stopFlushTimer()
  closeWebSocket()
}

// WebSocket 连接
const connectWebSocket = () => {
  if (!pageActive || !canPersist() || !isMonitoring.value) return

  closeWebSocket()

  try {
    websocket.value = new WebSocket(getRealtimeWebSocketUrl())

    websocket.value.onopen = () => {
      if (!pageActive || !canPersist()) {
        closeWebSocket()
        return
      }
      wsConnected.value = true
      reconnectAttempts.value = 0
      // 发送认证消息（token 不放 URL，避免暴露在日志/历史记录中）
      websocket.value?.send(JSON.stringify({ type: 'auth', token: getAccessToken() }))
      startHeartbeat()

      syncSubscription()
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
  activeSubscription = null
}

const sendWsMessage = (message: Record<string, unknown>) => {
  if (websocket.value?.readyState === WebSocket.OPEN) {
    websocket.value.send(JSON.stringify(message))
  }
}

const syncSubscription = () => {
  if (websocket.value?.readyState !== WebSocket.OPEN) return

  const pointIds = selectedPoints.value
    .map((point) => point.pointId)
    .filter((id): id is number => Number.isInteger(id))
    .sort((a, b) => a - b)
  const desired = isMonitoring.value && currentDevice.value && pointIds.length > 0
    ? {
        deviceCode: currentDevice.value.deviceCode,
        groupCode: activeGroupCode.value,
        pointIds: pointIds.join(',')
      }
    : null

  if (
    activeSubscription &&
    (!desired ||
      activeSubscription.deviceCode !== desired.deviceCode ||
      activeSubscription.groupCode !== desired.groupCode)
  ) {
    sendWsMessage({
      action: 'unsubscribe',
      device_code: activeSubscription.deviceCode,
      group_code: activeSubscription.groupCode
    })
    activeSubscription = null
  }
  if (
    desired &&
    (!activeSubscription || activeSubscription.pointIds !== desired.pointIds)
  ) {
    sendWsMessage({
      action: 'subscribe',
      device_code: desired.deviceCode,
      group_code: desired.groupCode,
      point_ids: pointIds,
      supports_freshness: true
    })
    activeSubscription = desired
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
  if (!pageActive || !isMonitoring.value || reconnectTimer.value) return
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

const updatePerspectiveTable = async (newRecords: RealtimeDataRecord[]) => {
  if (!perspectiveWorker.value || newRecords.length === 0 || isPerspectiveCleaning.value) return

  try {
    const schema = { timestamp: 'datetime', varSeq: 'integer', pointName: 'string', value: 'float' }
    const records = newRecords.map((r) => ({
      timestamp: r.timestamp,
      varSeq: r.varSeq,
      pointName: r.pointName,
      value: r.value
    }))

    // 单图表模式：所有数据放入同一个图表
    if (!separateBoolChart.value && perspectiveViewer.value) {
      if (!perspectiveTable.value) {
        perspectiveTable.value = await perspectiveWorker.value.table(schema as any, { limit: MAX_RECORDS })
        await (perspectiveTable.value as any).update(records)
        const config = buildViewerConfig(false)
        await perspectiveViewer.value.load(perspectiveTable.value)
        await perspectiveViewer.value.restore(config)
      } else {
        await (perspectiveTable.value as any).update(records)
      }
      await perspectiveViewer.value.flush()
      return
    }

    // 分离模式：分别更新数值型和 Bool 型图表
    const numericRecords = records.filter((r) => !boolVarSeqs.value.has(r.varSeq))
    const boolRecords = records.filter((r) => boolVarSeqs.value.has(r.varSeq))

    // 更新数值型图表
    if (numericRecords.length > 0 && perspectiveNumericViewer.value) {
      if (!perspectiveNumericTable.value) {
        perspectiveNumericTable.value = await perspectiveWorker.value.table(schema as any, { limit: MAX_RECORDS })
        await (perspectiveNumericTable.value as any).update(numericRecords)
        const config = buildViewerConfig(false)
        await perspectiveNumericViewer.value.load(perspectiveNumericTable.value)
        await perspectiveNumericViewer.value.restore(config)
      } else {
        await (perspectiveNumericTable.value as any).update(numericRecords)
      }
      await perspectiveNumericViewer.value.flush()
    }

    // 更新 Bool 型图表
    if (boolRecords.length > 0 && perspectiveBoolViewer.value) {
      if (!perspectiveBoolTable.value) {
        perspectiveBoolTable.value = await perspectiveWorker.value.table(schema as any, { limit: MAX_RECORDS })
        await (perspectiveBoolTable.value as any).update(boolRecords)
        const config = buildViewerConfig(true)
        await perspectiveBoolViewer.value.load(perspectiveBoolTable.value)
        await perspectiveBoolViewer.value.restore(config)
      } else {
        await (perspectiveBoolTable.value as any).update(boolRecords)
      }
      await perspectiveBoolViewer.value.flush()
    }
  } catch (error) {
    console.error('更新Perspective图表失败:', error)
  }
}

const queuePerspectiveUpdate = (records: RealtimeDataRecord[]) => {
  queuedChartRecords.push(...records)
  if (queuedChartRecords.length > MAX_RECORDS) {
    queuedChartRecords.splice(0, queuedChartRecords.length - MAX_RECORDS)
  }
  if (chartUpdateTask || rebuildingChart.value || isPerspectiveCleaning.value) return

  chartUpdateTask = (async () => {
    while (queuedChartRecords.length > 0 && !rebuildingChart.value && !isPerspectiveCleaning.value) {
      await updatePerspectiveTable(queuedChartRecords.splice(0))
    }
  })().finally(() => {
    chartUpdateTask = null
    if (queuedChartRecords.length > 0 && !rebuildingChart.value && !isPerspectiveCleaning.value) {
      queuePerspectiveUpdate([])
    }
  })
}

// WebSocket 消息处理
const handleWsMessage = (payload: Record<string, unknown>) => {
  if (payload.action === 'pong' || payload.type === 'pong') return

  // 提取点位数据：snapshot 使用 devices 字段，update 使用 data 或 data.data
  let pointsList: Array<Record<string, unknown>> | undefined

  if (payload.type === 'offline') {
    if (payload.device_code && currentDevice.value?.deviceCode !== payload.device_code) return
    if (payload.group_code && activeGroupCode.value !== payload.group_code) return
    deviceOffline.value = true
    realtimeValues.value.clear()
    realtimeQuality.value.clear()
    clearPending()
    return
  }

  if (
    (payload.type === 'snapshot' || payload.type === 'diff') &&
    payload.values &&
    typeof payload.values === 'object'
  ) {
    if (payload.device_code && currentDevice.value?.deviceCode !== payload.device_code) return
    if (payload.group_code && activeGroupCode.value !== payload.group_code) return
    if (payload.type === 'snapshot' && 'tdengineConnected' in payload) {
      tdengineConnected.value = payload.tdengineConnected as boolean | null
    }
    if (payload.type === 'snapshot') {
      clearPending()
      realtimeValues.value.clear()
      realtimeQuality.value.clear()
    }
    deviceOffline.value = payload.deviceOffline === true
    if (deviceOffline.value) return

    const pointById = new Map(
      selectedPoints.value
        .filter((point) => point.pointId !== undefined)
        .map((point) => [String(point.pointId), point])
    )
    const convertedPoints: Array<Record<string, unknown>> = []
    const samples = expandRealtimeSamples(
      payload.values as Record<string, RealtimeSample>,
      (payload.freshness || {}) as Record<string, unknown>,
      (pointId) => {
        const point = pointById.get(pointId)
        if (!point) return undefined
        const v = pendingValues.has(point.varSeq)
          ? pendingValues.get(point.varSeq) : realtimeValues.value.get(point.varSeq)
        if (v === undefined) return undefined
        return { v, q: pendingQuality.get(point.varSeq) ?? realtimeQuality.value.get(point.varSeq) }
      }
    )
    for (const [pointId, entry] of Object.entries(samples)) {
      const point = pointById.get(pointId)
      if (!point) continue
      convertedPoints.push({
        pointId: Number(pointId),
        varSeq: point.varSeq,
        pointName: point.pointName,
        ioAddress: point.ioAddress,
        dataType: point.dataType,
        value: normalizeWsPointValue(point.dataType, entry?.v),
        quality: entry?.q,
        sampledAt: entry?.t
      })
    }
    pointsList = convertedPoints
  } else if (payload.type === 'snapshot') {
    // 订阅响应：{ type: "snapshot", devices: [{ data: [...] }] }
    const devices = payload.devices as Array<Record<string, unknown>> | undefined
    if (Array.isArray(devices) && devices.length > 0) {
      pointsList = devices[0].data as Array<Record<string, unknown>> | undefined
    }
  } else if (payload.type === 'update') {
    // 更新推送：兼容 { data: [...] } 或 { data: { data: [...] } }
    const rawData = payload.data
    if (Array.isArray(rawData)) {
      pointsList = rawData
    } else if (rawData && typeof rawData === 'object') {
      pointsList = (rawData as any).data as Array<Record<string, unknown>> | undefined
    }
  }

  const selectedPointIds = new Set(selectedPoints.value.map((point) => String(point.pointId)))
  const sourceTimes = [
    ...Object.entries((payload.freshness || {}) as Record<string, unknown>)
      .filter(([pointId]) => selectedPointIds.has(pointId))
      .map(([, timestamp]) => timestamp),
    ...(pointsList || []).map((point) => point.sampledAt)
  ].filter((value): value is number => typeof value === 'number' && Number.isFinite(value))
  if (sourceTimes.length > 0) {
    const newest = Math.max(...sourceTimes)
    if (newest > lastSampleTimestamp) {
      lastSampleTimestamp = newest
      const sampleTime = formatTimestamp(new Date(newest))
      if (payload.type === 'snapshot') lastSyncTime.value = sampleTime
      else pendingLastSyncTime = sampleTime
    }
  }
  if (!Array.isArray(pointsList) || pointsList.length === 0) return

  // 快照立即显示；增量值和质量按100ms批量显示。
  const writeImmediate = payload.type === 'snapshot'
  const now = new Date()
  const newRecords: RealtimeDataRecord[] = []

  pointsList.forEach((item) => {
    const varSeq = item.varSeq as number
    const value = item.value
    const pointName = item.pointName as string
    const quality = item.quality as number | undefined

    if (varSeq !== undefined) {
      if (!isRealtimeValueAbsent(value)) {
        if (writeImmediate) {
          realtimeValues.value.set(varSeq, value as number | string)
        } else {
          pendingValues.set(varSeq, value as number | string)
        }
      }
      if (writeImmediate) {
        if (typeof quality === 'number') realtimeQuality.value.set(varSeq, quality)
      } else if (typeof quality === 'number') {
        pendingQuality.set(varSeq, quality)
      }

      // 保留收到的新采样，包括未变化的值；latest推送仍不等于完整历史序列。
      if (selectedPoints.value.some((p) => p.varSeq === varSeq)) {
        const sampledAt = item.sampledAt
        const record = {
          timestamp: typeof sampledAt === 'number' ? new Date(sampledAt) : now,
          varSeq,
          pointName,
          value: toChartSampleValue(value),
          text: toSampleText(value),
          quality: typeof quality === 'number' ? quality : undefined
        }
        newRecords.push(record)
      }
    }
  })

  if (writeImmediate) {
    // snapshot：立即写 realtimeRecords、lastSyncTime、图表
    realtimeRecords.value.push(...newRecords)
    if (realtimeRecords.value.length > MAX_RECORDS) {
      realtimeRecords.value = realtimeRecords.value.slice(-MAX_RECORDS)
    }
    queuePerspectiveUpdate(newRecords)
  } else {
    // diff/update：攒入 pending，等 flush timer 触发
    pendingChartRecords.push(...newRecords)
    if (pendingChartRecords.length > MAX_RECORDS) {
      pendingChartRecords.splice(0, pendingChartRecords.length - MAX_RECORDS)
    }
  }
}

// Perspective 图表
const initPerspectiveViewer = async () => {
  const epoch = pageEpoch
  if (!pageActive) return
  await nextTick() // 确保 DOM 已渲染

  try {
    const runtime = await ensurePerspectiveViewerRuntime()
    if (!pageActive || epoch !== pageEpoch) return
    perspectiveReady.value = true

    if (!perspectiveWorker.value) {
      const worker = await runtime.perspective.worker()
      if (!pageActive || epoch !== pageEpoch) {
        worker.terminate?.()
        return
      }
      perspectiveWorker.value = worker
    }

    // 单图表模式
    if (perspectiveRef.value && !perspectiveViewer.value) {
      perspectiveRef.value.innerHTML = ''
      const viewer = document.createElement('perspective-viewer') as PerspectiveViewerClient
      viewer.setAttribute('theme', perspectiveTheme.value)
      viewer.style.display = 'block'
      viewer.style.width = '100%'
      viewer.style.height = '100%'
      perspectiveRef.value.appendChild(viewer)
      perspectiveViewer.value = viewer
    }

    // 分离模式：数值型图表
    if (perspectiveNumericRef.value && !perspectiveNumericViewer.value) {
      perspectiveNumericRef.value.innerHTML = ''
      const viewer = document.createElement('perspective-viewer') as PerspectiveViewerClient
      viewer.setAttribute('theme', perspectiveTheme.value)
      viewer.style.display = 'block'
      viewer.style.width = '100%'
      viewer.style.height = '100%'
      perspectiveNumericRef.value.appendChild(viewer)
      perspectiveNumericViewer.value = viewer
    }

    // 分离模式：Bool 型图表
    if (perspectiveBoolRef.value && !perspectiveBoolViewer.value) {
      perspectiveBoolRef.value.innerHTML = ''
      const viewer = document.createElement('perspective-viewer') as PerspectiveViewerClient
      viewer.setAttribute('theme', perspectiveTheme.value)
      viewer.style.display = 'block'
      viewer.style.width = '100%'
      viewer.style.height = '100%'
      perspectiveBoolRef.value.appendChild(viewer)
      perspectiveBoolViewer.value = viewer
    }
  } catch (error) {
    console.error('初始化Perspective失败:', error)
    perspectiveReady.value = false
  }
}

const buildViewerConfig = (isBoolChart = false) => {
  // Bool 图表使用阶梯图样式，Y 轴只显示 0 和 1
  if (isBoolChart) {
    if (chartType.value === 'status') {
      return {
        plugin: 'Datagrid',
        group_by: [],
        split_by: [],
        columns: ['timestamp', 'pointName', 'value'],
        sort: [['timestamp', 'desc']],
        plugin_config: {
          columns: {
            timestamp: {},
            pointName: {},
            value: {
              column_style: {
                type: 'heatmap',
                color_mode: 'gradient',
                pos_color: '#18a47a',
                neg_color: '#d44f63',
                min: 0,
                max: 1
              }
            }
          }
        }
      }
    }

    // Bool 使用 Y Line，设置整数格式和 Y 轴范围
    return {
      plugin: 'Y Line',
      group_by: ['timestamp'],
      split_by: ['pointName'],
      columns: ['value'],
      sort: [['timestamp', 'asc']],
      aggregates: { value: 'last' },
      plugin_config: {
        columns: {
          value: {
            number_format: '0', // 强制整数格式
            axis_min: 0,
            axis_max: 1
          }
        }
      }
    }
  }

  // 非 Bool 图表的配置
  if (chartType.value === 'status') {
    return {
      plugin: 'Datagrid',
      group_by: [],
      split_by: [],
      columns: ['timestamp', 'pointName', 'value'],
      sort: [['timestamp', 'desc']],
      plugin_config: {
        columns: {
          timestamp: {},
          pointName: {},
          value: {
            column_style: {
              type: 'heatmap',
              color_mode: 'gradient',
              pos_color: '#18a47a',
              neg_color: '#d44f63',
              min: 0,
              max: 1
            }
          }
        }
      }
    }
  }

  if (chartType.value === 'area') {
    return {
      plugin: 'Y Area',
      group_by: ['timestamp'],
      split_by: ['pointName'],
      columns: ['value'],
      sort: [['timestamp', 'asc']],
      aggregates: { value: 'last' }
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

const handleExport = async () => {
  if (realtimeRecords.value.length === 0) {
    ElMessage.warning('暂无数据可导出')
    return
  }

  try {
    const ExcelJS = await import('exceljs')
    const { toPng } = await import('html-to-image')

    const workbook = new ExcelJS.Workbook()

    // 生成文件名
    const deviceName = currentDevice.value?.deviceName || currentDevice.value?.deviceCode || '设备'
    const timestampStr = new Date().toISOString().slice(0, 19).replace(/[T:]/g, '-')
    const filename = `${deviceName}_实时监控_${timestampStr}.xlsx`

    // Sheet 1: 趋势图截图
    const chartSheet = workbook.addWorksheet('趋势图')

    // 截取图表
    const chartElements: HTMLElement[] = []
    if (!separateBoolChart.value && perspectiveRef.value) {
      chartElements.push(perspectiveRef.value)
    } else {
      if (perspectiveNumericRef.value) chartElements.push(perspectiveNumericRef.value)
      if (perspectiveBoolRef.value) chartElements.push(perspectiveBoolRef.value)
    }

    // 在 Excel 中嵌入图表截图
    let imageRow = 1
    for (const chartEl of chartElements) {
      try {
        const pngData = await toPng(chartEl, { quality: 0.9, pixelRatio: 2 })
        const imageBase64 = pngData.split(',')[1]
        const imageId = workbook.addImage({
          base64: imageBase64,
          extension: 'png'
        })
        // 根据图表实际尺寸计算 Excel 中的大小
        const width = chartEl.offsetWidth / 8 // 约换算为 Excel 列宽单位
        const height = chartEl.offsetHeight / 1.5 // 约换算为 Excel 行高单位
        chartSheet.addImage(imageId, {
          tl: { col: 0, row: imageRow },
          ext: { width: Math.min(width, 60), height: Math.min(height, 40) }
        })
        imageRow += Math.ceil(height / 15) + 2
      } catch (e) {
        console.warn('图表截图失败:', e)
      }
    }

    // Sheet 2: 趋势数据（宽表格式）
    const dataSheet = workbook.addWorksheet('趋势数据')

    // 构建宽表数据
    // 1. 获取所有变量列（按选中顺序）
    const varColumns = selectedPoints.value.map((p) => {
      const name = p.pointName
      // 如果名称重复，添加序号区分
      const duplicateCount = selectedPoints.value.filter((x) => x.pointName === name).length
      return duplicateCount > 1 ? `${name}(${p.varSeq})` : name
    })

    // 2. 按时间分组聚合数据
    const timeMap = new Map<string, Map<number, number | string>>()
    for (const record of realtimeRecords.value) {
      // 格式化时间：YYYY-MM-DD HH:mm:ss.SSS
      const timeKey = formatExportTime(record.timestamp)
      if (!timeMap.has(timeKey)) {
        timeMap.set(timeKey, new Map())
      }
      // 文本值原样导出；缺值样本导出为占位符+质量文案，不与真实 0 混淆
      timeMap.get(timeKey)!.set(record.varSeq, formatExportSampleValue(record.value, record.quality, record.text))
    }

    // 3. 构建表头和行数据
    const header = ['时间', ...varColumns]
    dataSheet.columns = header.map((h) => ({ header: h, key: h, width: 18 }))

    // 添加数据行
    const sortedTimes = Array.from(timeMap.keys()).sort()
    for (const time of sortedTimes) {
      const row: Record<string, number | string> = { '时间': time }
      const valuesMap = timeMap.get(time)!
      selectedPoints.value.forEach((p) => {
        const colName = varColumns[selectedPoints.value.indexOf(p)]
        row[colName] = valuesMap.get(p.varSeq) ?? ''
      })
      dataSheet.addRow(row)
    }

    // 设置表头样式
    dataSheet.getRow(1).font = { bold: true }
    dataSheet.getRow(1).alignment = { horizontal: 'center' }

    // 导出文件
    const buffer = await workbook.xlsx.writeBuffer()
    const blob = new Blob([buffer], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = filename
    link.click()
    URL.revokeObjectURL(url)

    ElMessage.success('导出成功')
  } catch (error) {
    console.error('导出失败:', error)
    ElMessage.error('导出失败')
  }
}

// 格式化导出时间：YYYY-MM-DD HH:mm:ss.SSS
const formatExportTime = (date: Date): string => {
  const pad = (n: number, len = 2) => String(n).padStart(len, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}.${pad(date.getMilliseconds(), 3)}`
}

// 生命周期
const initializePage = async () => {
  const epoch = ++pageEpoch
  const isCurrent = () => pageActive && epoch === pageEpoch && canPersist()
  await loadDeviceTree(isCurrent)
  if (!isCurrent()) return

  // 恢复之前的状态
  const wasMonitoring = loadStateFromStorage()
  if (currentDevice.value) {
    await loadPointList(currentDevice.value.deviceId, isCurrent)
    if (!isCurrent()) return
    void loadPointGroups(currentDevice.value.deviceCode, isCurrent)
  }

  // 无论是否正在监控，都先初始化 Perspective 并恢复快照
  if (currentDevice.value && selectedPoints.value.length > 0) {
    await nextTick()
    if (!isCurrent()) return
    await initPerspectiveViewer()
    if (!isCurrent()) return

    // 恢复数据快照（如果有）
    const snapshotLoaded = await loadChartSnapshot()
    if (!isCurrent()) return
    if (snapshotLoaded && realtimeRecords.value.length > 0) {
      await updatePerspectiveTable(realtimeRecords.value)
      if (!isCurrent()) return
    }

    // 如果之前正在监控，再连接 WebSocket 继续接收新数据
    if (wasMonitoring) {
      isMonitoring.value = true
      connectWebSocket()
      startFlushTimer()
    }
  }
}

const activatePage = () => {
  if (pageActive) return
  pageActive = true
  isPerspectiveCleaning.value = false
  void initializePage()
}
watch(() => userStore.isLogin, (loggedIn) => {
  if (!loggedIn) {
    pageActive = false
    ++pageEpoch
    disconnectForUnmount()
  }
})
watch(() => route.query.deviceId, async () => {
  if (!pageActive) return
  ++pageEpoch
  resumeMonitoring = false
  disconnectForUnmount()
  selectedPoints.value = []
  realtimeRecords.value = []
  clearPending()
  await clearPerspectiveTable()
  if (pageActive) void initializePage()
})
onMounted(activatePage)
onActivated(activatePage)
onDeactivated(() => {
  resumeMonitoring = isMonitoring.value
  saveStateToStorage()
  pageActive = false
  ++pageEpoch
  isPerspectiveCleaning.value = true
  disconnectForUnmount()
})

onUnmounted(() => {
  pageActive = false
  ++pageEpoch
  isPerspectiveCleaning.value = true
  // 保存状态和数据快照
  const state = {
    deviceId: currentDevice.value?.deviceId,
    deviceCode: currentDevice.value?.deviceCode,
    deviceName: currentDevice.value?.deviceName,
    selectedPoints: selectedPoints.value.map((p) => ({
      pointId: p.pointId,
      varSeq: p.varSeq,
      pointName: p.pointName,
      ioAddress: p.ioAddress,
      dataType: p.dataType
    })),
    chartRefreshRate: refreshRate.value,
    chartType: chartType.value,
    separateBoolChart: separateBoolChart.value,
    wasMonitoring: isMonitoring.value || resumeMonitoring
  }
  if (canPersist()) localStorage.setItem(STORAGE_KEY, JSON.stringify(state))

  // 保存数据快照（保留已有的 configs，避免覆盖 saveChartSnapshot 写入的图表配置）
  flushPendingRealtimeUpdates()
  const dataSnapshot = realtimeRecords.value.slice(-MAX_SNAPSHOT_RECORDS).map((r) => ({
    timestamp: r.timestamp.toISOString(),
    varSeq: r.varSeq,
    pointName: r.pointName,
    value: r.value,
    text: r.text,
    quality: r.quality
  }))
  const existingChartState = (() => {
    try { return JSON.parse(localStorage.getItem(CHART_STATE_KEY) || '{}') } catch { return {} }
  })()
  if (canPersist()) localStorage.setItem(CHART_STATE_KEY, JSON.stringify({
    data: dataSnapshot,
    configs: existingChartState.configs,
    timestamp: Date.now()
  }))

  // 轻量清理：只断开连接，不清空数据
  disconnectForUnmount()

  isPerspectiveCleaning.value = true
  void (async () => {
    if (chartUpdateTask) await chartUpdateTask
    // 清理 Perspective 资源
    if (perspectiveViewer.value) {
      try {
        await perspectiveViewer.value.delete()
      } catch (e) {
        console.warn('清理Perspective失败:', e)
      }
    }
    if (perspectiveNumericViewer.value) {
      try {
        await perspectiveNumericViewer.value.delete()
      } catch (e) {
        console.warn('清理数值型Perspective失败:', e)
      }
    }
    if (perspectiveBoolViewer.value) {
      try {
        await perspectiveBoolViewer.value.delete()
      } catch (e) {
        console.warn('清理Bool Perspective失败:', e)
      }
    }
    if (perspectiveWorker.value?.terminate) {
      perspectiveWorker.value.terminate()
    }
    perspectiveWorker.value = null
    perspectiveViewer.value = null
    perspectiveTable.value = null
    perspectiveNumericViewer.value = null
    perspectiveNumericTable.value = null
    perspectiveBoolViewer.value = null
    perspectiveBoolTable.value = null
    isPerspectiveCleaning.value = false
  })()
})

// 监听图表类型变化
watch(chartType, async () => {
  // 单图表模式
  if (perspectiveViewer.value && perspectiveTable.value) {
    await perspectiveViewer.value.restore(buildViewerConfig(false))
  }
  // 分离模式：数值型
  if (perspectiveNumericViewer.value && perspectiveNumericTable.value) {
    await perspectiveNumericViewer.value.restore(buildViewerConfig(false))
  }
  // 分离模式：Bool 型
  if (perspectiveBoolViewer.value && perspectiveBoolTable.value) {
    await perspectiveBoolViewer.value.restore(buildViewerConfig(true))
  }
})

watch(perspectiveTheme, () => {
  applyPerspectiveTheme()
})

// 监听分离 Bool 图表设置变化
watch(separateBoolChart, async () => {
  // 重建图表，用现有数据重新渲染
  await rebuildPerspectiveFromRecords()
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
  gap: 8px;
  align-items: center;
  justify-content: flex-start;
}

.control-left {
  display: flex;
  flex-shrink: 0;
  gap: 12px;
  align-items: center;
}

.control-middle {
  display: flex;
  flex: 1;
  gap: 8px;
  align-items: center;
  margin-left: 8px;
  min-width: 300px;
}

.device-label {
  font-size: 13px;
  color: var(--art-gray-600);
  white-space: nowrap;
}

.device-btn {
  display: inline-flex;
  gap: 6px;
  flex-shrink: 0;
  align-items: center;
  padding: 6px 14px;
  border: 1px solid var(--art-gray-300);
  border-radius: 10px;
  background: var(--el-bg-color);
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
  color: var(--el-color-primary);
}

.device-arrow {
  font-size: 12px;
  color: var(--art-gray-600);
}

.point-selector-box {
  flex: 1;
  padding: 6px 16px;
  border: 1px solid var(--art-gray-300);
  border-radius: 10px;
  background: var(--el-bg-color);
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
  color: var(--el-color-primary);
}

.point-placeholder {
  color: var(--art-gray-600);
}

.control-right {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  margin-left: 8px;
}

.ws-status {
  display: flex;
  gap: 6px;
  align-items: center;
  padding: 6px 12px;
  margin-left: 8px;
  border-radius: 16px;
  background: var(--art-gray-200);
  font-size: 13px;
  color: var(--art-gray-600);

  &.connected {
    background: rgba(24, 164, 122, 0.1);
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
  margin-left: 8px;
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
    border-bottom: 1px solid var(--art-gray-200);
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
  color: var(--art-gray-900);
}

.chart-actions {
  display: flex;
  gap: 8px;
  align-items: center;
  margin-left: auto;
}

.chart-type-btn {
  padding: 6px 14px;
  border: 1px solid var(--art-gray-300);
  border-radius: 8px;
  background: var(--el-bg-color);
  font-size: 13px;
  color: var(--art-gray-500);
  transition: background 0.2s, color 0.2s;

  &:hover {
    background: #f7f9fc;
  }

  &.active {
    background: var(--el-color-primary);
    border-color: var(--el-color-primary);
    color: #fff;
  }
}

.perspective-container {
  flex: 1;
  min-height: 200px;
  border-radius: 16px;
  overflow: hidden;
  background: var(--el-bg-color);
  border: 1px solid var(--art-gray-200);

  perspective-viewer {
    width: 100%;
    height: 100%;
  }
}

// 单图表模式：占满整个空间
.perspective-single {
  flex: 1;
  min-height: 400px;
}

// 数值型图表占主要空间
.perspective-numeric {
  flex: 3;
  min-height: 300px;
}

// Bool 型图表高度适中
.perspective-bool {
  flex: 1.5;
  min-height: 200px;

  // 隐藏图表顶部图例（图表内已有图例）
  :deep(.pd-column-name-label),
  :deep(perspective-viewer .plugin-legend) {
    display: none !important;
  }
}

// 设置弹窗
.settings-content {
  padding: 8px 0;
}

.settings-item {
  display: flex;
  flex-direction: column;
  gap: 8px;
  padding: 16px 0;
  border-bottom: 1px solid #eee;

  &:last-child {
    border-bottom: none;
  }
}

.settings-label {
  font-size: 14px;
  font-weight: 500;
  color: #333;
}

.settings-desc {
  font-size: 13px;
  color: #666;
  line-height: 1.5;
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
  color: var(--art-gray-900);
}

.point-cards-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.point-card {
  padding: 14px 16px;
  border-radius: 14px;
  background: var(--art-gray-100);
  border: 1px solid var(--art-gray-200);
  transition: box-shadow 0.2s ease;

  &:hover {
    box-shadow: 0 4px 12px rgba(74, 108, 247, 0.1);
  }
}

.point-card-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--art-gray-900);
  margin-bottom: 4px;
}

.point-card-address {
  font-size: 12px;
  color: var(--art-gray-600);
  margin-bottom: 8px;
}

.point-card-value {
  font-size: 20px;
  font-weight: 600;

  &.is-absent {
    color: var(--art-gray-600);
    font-weight: 500;
  }
}

.empty-points {
  display: flex;
  flex-direction: column;
  gap: 12px;
  align-items: center;
  justify-content: center;
  height: 100%;
  min-height: 200px;
  color: var(--art-gray-600);
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
  color: var(--art-gray-600);
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
  color: var(--art-gray-500);
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

.device-selector-content {
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
  color: var(--art-gray-900);
}

.selector-control {
  width: 100%;
}

.device-option {
  display: flex;
  align-items: center;
  gap: 12px;
}

.device-option-code {
  font-size: 14px;
  font-weight: 600;
  color: var(--art-gray-900);
  min-width: 80px;
}

.device-option-name {
  font-size: 13px;
  color: var(--art-gray-600);
}

.selected-device-info {
  padding: 16px;
  background: rgba(74, 108, 247, 0.06);
  border-radius: 12px;
  border: 1px solid rgba(74, 108, 247, 0.2);
}

.info-label {
  font-size: 12px;
  color: var(--art-gray-500);
  margin-bottom: 8px;
}

.info-content {
  display: flex;
  align-items: center;
  gap: 12px;
}

.info-code {
  font-size: 16px;
  font-weight: 600;
  color: var(--el-color-primary);
}

.info-name {
  font-size: 14px;
  color: var(--art-gray-700);
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
  color: var(--art-gray-900);

  .selected-count {
    margin-left: 4px;
    color: var(--el-color-primary);
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
  border: 1px solid var(--art-gray-300);
  border-radius: 12px;
}

.point-checkbox-content {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.point-checkbox-name {
  font-size: 14px;
  color: var(--art-gray-900);
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

// 设备离线横幅
.offline-banner {
  padding: 10px 22px;
  background: rgba(212, 79, 99, 0.1);
  border: 1px solid rgba(212, 79, 99, 0.3);
  border-radius: 12px;
  color: #d44f63;
  font-size: 13px;
  font-weight: 500;
  text-align: center;
}

// 点位分组选择器
.group-tab-card {
  border: 1px solid var(--art-card-border);
  border-radius: 14px;

  :deep(.el-card__body) {
    padding: 10px 18px;
  }
}

.group-tab-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  flex-wrap: wrap;
}

.group-tab-label {
  font-size: 13px;
  color: var(--art-gray-600);
  white-space: nowrap;
}

.group-tabs {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.group-tab-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 5px 14px;
  border: 1px solid var(--art-gray-300);
  border-radius: 8px;
  background: var(--el-bg-color);
  font-size: 13px;
  color: var(--art-gray-700);
  cursor: pointer;
  transition: border-color 0.2s, background 0.2s;

  &:hover {
    border-color: var(--el-color-primary);
    color: var(--el-color-primary);
  }

  &.active {
    border-color: var(--el-color-primary);
    background: rgba(74, 108, 247, 0.08);
    color: var(--el-color-primary);
    font-weight: 500;
  }
}

.group-tab-count {
  font-size: 11px;
  color: var(--art-gray-500);
  background: var(--art-gray-200);
  border-radius: 6px;
  padding: 1px 5px;

  .active & {
    background: rgba(74, 108, 247, 0.12);
    color: var(--el-color-primary);
  }
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
