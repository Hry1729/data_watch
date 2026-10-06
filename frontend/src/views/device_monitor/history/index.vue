<!-- 历史数据查询页 -->
<template>
  <div class="history-page art-full-height">
    <!-- 顶部控制栏 -->
    <ElCard class="control-bar-card">
      <div class="control-bar">
        <div class="control-left">
          <span class="control-label">当前设备</span>
          <ElButton class="device-btn" @click="openDeviceSelector">
            <span class="device-name">{{ currentDevice?.name || '未选择' }}</span>
            <ElIcon class="device-arrow"><ArrowRight /></ElIcon>
          </ElButton>
        </div>
        <div class="control-middle">
          <span class="control-label">已选点位</span>
          <div class="point-selector-box" @click="openPointSelector">
            <template v-if="selectedPoints.length > 0">
              <span class="point-tags-text">{{ pointTagsText }}</span>
            </template>
            <span v-else class="point-placeholder">点击选择查询点位</span>
          </div>
          <span class="control-label" style="margin-left: 16px">时间范围</span>
          <ElDatePicker
            v-model="timeRange"
            type="datetimerange"
            range-separator="至"
            start-placeholder="开始时间"
            end-placeholder="结束时间"
            value-format="YYYY-MM-DD HH:mm:ss"
            class="time-picker"
            :shortcuts="timeShortcuts"
          />
        </div>
        <div class="control-right">
          <ElSelect v-model="chartInterval" class="interval-select" placeholder="聚合粒度">
            <ElOption label="自动" value="auto" />
            <ElOption label="100毫秒" value="100ms" />
            <ElOption label="500毫秒" value="500ms" />
            <ElOption label="1秒" value="1s" />
            <ElOption label="5秒" value="5s" />
            <ElOption label="30秒" value="30s" />
            <ElOption label="1分钟" value="1m" />
            <ElOption label="5分钟" value="5m" />
          </ElSelect>
          <ElSelect v-model="chartAggregate" class="aggregate-select" placeholder="聚合方式">
            <ElOption label="平均值" value="avg" />
            <ElOption label="最大值" value="max" />
            <ElOption label="最小值" value="min" />
            <ElOption label="最新值" value="last" />
          </ElSelect>
          <ElButton type="primary" :disabled="!canQuery" :loading="loading" @click="handleQuery">
            查询
          </ElButton>
          <ElButton :disabled="!hasResultData" @click="openExportDialog">导出</ElButton>
        </div>
      </div>
    </ElCard>

    <!-- 主内容区 -->
    <div class="main-content">
      <!-- 图表区域 -->
      <ElCard class="chart-card">
        <div class="chart-header">
          <span class="chart-title">历史趋势分析</span>
          <div class="chart-actions">
            <ElButton
              size="small"
              :class="['chart-type-btn', { active: chartType === 'line' }]"
              @click="handleChartTypeChange('line')"
            >
              折线图
            </ElButton>
            <ElButton
              size="small"
              :class="['chart-type-btn', { active: chartType === 'area' }]"
              @click="handleChartTypeChange('area')"
            >
              面积图
            </ElButton>
            <ElButton
              size="small"
              :class="['chart-type-btn', { active: chartType === 'bar' }]"
              @click="handleChartTypeChange('bar')"
            >
              柱状图
            </ElButton>
            <ElButton
              size="small"
              :class="['chart-type-btn', { active: chartType === 'scatter' }]"
              @click="handleChartTypeChange('scatter')"
            >
              散点图
            </ElButton>
          </div>
        </div>

        <MonitorLoadingPlaceholder
          v-if="loading && !hasResultData"
          title="历史数据加载中"
          description="正在读取历史数据并准备图表渲染"
          min-height="520px"
          :rows="6"
        />
        <div v-else-if="hasResultData" class="chart-wrapper">
          <div ref="chartRef" class="echarts-container"></div>
        </div>
        <ElEmpty v-else :description="emptyDescription" :image-size="120" />
      </ElCard>

      <!-- 数据明细矩阵表 -->
      <ElCard v-if="hasResultData" class="table-card">
        <template #header>
          <div class="panel-header">
            <span class="panel-title">历史数据矩阵</span>
            <div class="table-controls">
              <span class="control-label">对齐粒度：</span>
              <ElSelect v-model="matrixAlignGranularity" size="small" class="granularity-select">
                <ElOption label="1秒" value="1s" />
                <ElOption label="500毫秒" value="500ms" />
                <ElOption label="100毫秒" value="100ms" />
              </ElSelect>
            </div>
          </div>
        </template>
        <ElTable :data="matrixRows" border stripe height="400" size="small" v-loading="loadingMatrix">
          <ElTableColumn prop="ts" label="采样时间" width="240" fixed="left" />
          <ElTableColumn
            v-for="col in valueColumns"
            :key="col.prop"
            :prop="col.prop"
            :label="col.label"
            :min-width="120"
          >
            <template #default="scope">
              {{ formatMatrixValue(scope.row[col.prop], col.dataType) }}
            </template>
          </ElTableColumn>
        </ElTable>
        <div v-if="matrixNextCursor" class="load-more">
          <ElButton :loading="loadingMatrix" @click="loadMatrixData()">加载更多</ElButton>
        </div>
      </ElCard>
    </div>

    <!-- 设备选择器抽屉 -->
    <ElDialog v-model="deviceDrawerVisible" title="选择设备" width="400px">
      <div class="selector-content">
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
              :key="device.id"
              :label="device.deviceCode"
              :value="device.id"
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
        <ElButton @click="deviceDrawerVisible = false">取消</ElButton>
        <ElButton type="primary" :disabled="!selectedDeviceId" @click="confirmDeviceSelection">确认</ElButton>
      </template>
    </ElDialog>

    <!-- 点位选择器抽屉 -->
    <ElDialog v-model="pointDrawerVisible" title="选择查询点位" width="480px">
      <div class="selector-content">
        <div class="selector-item">
          <label class="selector-label">选择设备</label>
          <ElSelect v-model="tempDeviceId" placeholder="请选择设备" filterable class="selector-control" @change="handleTempDeviceChange">
            <ElOption v-for="device in deviceList" :key="device.id" :label="device.name" :value="device.id" />
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
        <ElButton @click="pointDrawerVisible = false">取消</ElButton>
        <ElButton type="primary" :disabled="tempSelectedVarSeqs.length === 0" @click="confirmPointSelection">确认</ElButton>
      </template>
    </ElDialog>

    <!-- 导出数据对话框 -->
    <ElDialog v-model="exportDialogVisible" title="导出历史数据" width="500px">
      <ElForm :model="exportForm" label-width="80px">
        <ElFormItem label="设备">
          <ElInput :value="deviceList.find(d => d.id === exportForm.deviceId)?.name || '未选择'" disabled />
        </ElFormItem>
        <ElFormItem label="时间范围">
          <ElDatePicker
            v-model="exportTimeRange"
            type="datetimerange"
            range-separator="至"
            start-placeholder="开始时间"
            end-placeholder="结束时间"
            value-format="YYYY-MM-DD HH:mm:ss"
            style="width: 100%"
            :shortcuts="timeShortcuts"
          />
        </ElFormItem>
        <ElFormItem label="导出点位">
          <ElSelect
            v-model="exportForm.varSeqs"
            multiple
            collapse-tags
            collapse-tags-tooltip
            placeholder="请选择导出点位 (最多20个)"
            style="width: 100%"
          >
            <ElOption
              v-for="p in pointList"
              :key="p.varSeq"
              :label="p.pointName"
              :value="p.varSeq"
              :disabled="exportForm.varSeqs.length >= 20 && !exportForm.varSeqs.includes(p.varSeq)"
            />
          </ElSelect>
        </ElFormItem>
        <ElFormItem label="导出格式">
          <ElSelect v-model="exportForm.format" style="width: 100%">
            <ElOption label="CSV 文件 (*.csv)" value="csv" />
          </ElSelect>
        </ElFormItem>
      </ElForm>
      <template #footer>
        <div v-if="exporting" style="display: flex; align-items: center; justify-content: space-between; width: 100%;">
          <span style="color: var(--el-color-primary); font-size: 13px;">{{ exportStatusMsg }} ({{ exportProgress }}%)</span>
          <ElProgress :percentage="exportProgress" :show-text="false" style="width: 200px; margin-left: 10px;" />
        </div>
        <div v-else>
          <ElButton @click="exportDialogVisible = false">取消</ElButton>
          <ElButton type="primary" @click="handleExport">开始导出</ElButton>
        </div>
      </template>
    </ElDialog>
  </div>
</template>

<script setup lang="ts">
import { useUserStore } from '@/store/modules/user'
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import type { CheckboxValueType } from 'element-plus'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowRight } from '@element-plus/icons-vue'
import { onBeforeRouteLeave } from 'vue-router'
import * as echarts from 'echarts'
import { createHistoryDataZoom, historySampleData } from '@/utils/history-zoom'
import MonitorLoadingPlaceholder from '@/components/device-monitor/MonitorLoadingPlaceholder.vue'
import {
  fetchHistoryChart,
  fetchHistoryDevices,
  fetchDeviceTypes,
  fetchHistoryExport,
  fetchHistoryExportDownload,
  fetchHistoryExportStatus,
  fetchHistoryPoints,
  fetchHistoryRawMatrix
} from '@/api/device-monitor'
import { isHttpError } from '@/utils/http/error'

defineOptions({ name: 'HistoryData' })

// 缓存相关常量
const historyUserStore = useUserStore()
const historyOwnerUserId = historyUserStore.info.userId
const HISTORY_CACHE_KEY = 'history_query_cache'
const HISTORY_CACHE_MAX_CHART_POINTS = 5000
const HISTORY_CACHE_MAX_MATRIX_ROWS = 2000
const HISTORY_CACHE_MAX_POINTS = 10
const HISTORY_CONDITION_CACHE_MAX_AGE = 24 * 60 * 60 * 1000
const HISTORY_DATA_CACHE_MAX_AGE = 10 * 60 * 1000

type PointInfo = Api.DeviceMonitor.HistoryPointItem
type DeviceInfo = Api.DeviceMonitor.HistoryValidDeviceItem
type DeviceType = Api.DeviceMonitor.DeviceTypeItem
type ChartType = 'line' | 'bar' | 'scatter' | 'area'

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

// 设备选择器相关
const deviceDrawerVisible = ref(false)
const deviceTypes = ref<DeviceType[]>([])
const selectedTypeCode = ref<string>('')
const selectedDeviceId = ref<number | undefined>()

// 点位选择器相关
const pointDrawerVisible = ref(false)
const pointSearchKeyword = ref('')
const tempDeviceId = ref<number | undefined>()
const tempSelectedVarSeqs = ref<number[]>([])

const selectedPoints = computed(() =>
  pointList.value.filter((p) => searchForm.value.varSeqs.includes(p.varSeq))
)

const currentDevice = computed(() =>
  deviceList.value.find((d) => d.id === searchForm.value.deviceId)
)

// 根据类型过滤的设备列表
const filteredDevicesByType = computed(() => {
  if (!selectedTypeCode.value) return []
  return deviceList.value.filter((d) => d.typeCode === selectedTypeCode.value)
})

// 选中设备的详情
const selectedDeviceDetail = computed(() =>
  deviceList.value.find((d) => d.id === selectedDeviceId.value)
)

// 设备类型变更
const handleTypeChange = () => {
  selectedDeviceId.value = undefined
}

// 设备变更（仅用于显示，不立即确认）
const handleDeviceChange = () => {
  // 可以在这里做一些预览操作
}

// 确认设备选择
const confirmDeviceSelection = async () => {
  if (!selectedDeviceId.value) return
  searchForm.value.deviceId = selectedDeviceId.value
  searchForm.value.varSeqs = []
  deviceDrawerVisible.value = false
  await loadPointList(selectedDeviceId.value)
  saveHistoryCache()
}

const filteredPointList = computed(() => {
  if (!pointSearchKeyword.value) return pointList.value
  const keyword = pointSearchKeyword.value.toLowerCase()
  return pointList.value.filter(
    (point) =>
      point.pointName.toLowerCase().includes(keyword) ||
      point.ioAddress.toLowerCase().includes(keyword)
  )
})

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

const canQuery = computed(() => {
  return searchForm.value.deviceId && searchForm.value.varSeqs.length > 0 && searchForm.value.startTime && searchForm.value.endTime
})

// 设备选择器
const openDeviceSelector = async () => {
  // 加载设备类型列表
  await loadDeviceTypes()
  // 重置选择状态
  selectedTypeCode.value = currentDevice.value?.typeCode || ''
  selectedDeviceId.value = currentDevice.value?.id
  deviceDrawerVisible.value = true
}

// 点位选择器
const openPointSelector = () => {
  tempDeviceId.value = searchForm.value.deviceId
  if (tempDeviceId.value) {
    void loadPointList(tempDeviceId.value)
  }
  tempSelectedVarSeqs.value = [...searchForm.value.varSeqs]
  pointDrawerVisible.value = true
}

const handleTempDeviceChange = async (deviceId?: number) => {
  tempSelectedVarSeqs.value = []
  await loadPointList(deviceId)
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

const confirmPointSelection = () => {
  if (tempDeviceId.value && tempDeviceId.value !== searchForm.value.deviceId) {
    searchForm.value.deviceId = tempDeviceId.value
  }
  searchForm.value.varSeqs = [...tempSelectedVarSeqs.value]
  pointDrawerVisible.value = false
  saveHistoryCache()
}

const exportDialogVisible = ref(false)
const exportTimeRange = ref<[string, string]>(['', ''])
const exportForm = ref({
  deviceId: undefined as number | undefined,
  varSeqs: [] as number[],
  startTime: '',
  endTime: '',
  format: 'csv'
})

const openExportDialog = () => {
  if (!searchForm.value.deviceId) {
    ElMessage.warning('请先选择设备')
    return
  }
  exportForm.value = {
    deviceId: searchForm.value.deviceId,
    varSeqs: [...searchForm.value.varSeqs],
    startTime: searchForm.value.startTime,
    endTime: searchForm.value.endTime,
    format: 'csv'
  }
  exportTimeRange.value = [searchForm.value.startTime, searchForm.value.endTime]
  exportDialogVisible.value = true
}

const chartInterval = ref('auto')
const chartAggregate = ref('avg')

const deviceList = ref<DeviceInfo[]>([])
const pointList = ref<PointInfo[]>([])

const loading = ref(false)
const exporting = ref(false)
const exportProgress = ref(0)
const exportStatusMsg = ref('')
let exportTimer: ReturnType<typeof setInterval> | null = null

const chartType = ref<ChartType>('line')
const renderedRecordCount = ref(0)

const chartRef = ref<HTMLElement>()
let chartInstance: echarts.ECharts | null = null
const chartData = ref<Api.DeviceMonitor.HistoryChartResponse | null>(null)

// 矩阵表相关
const matrixRows = ref<Record<string, any>[]>([])
const matrixColumns = ref<Api.DeviceMonitor.HistoryMatrixColumn[]>([])
const matrixNextCursor = ref<string | null>(null)
const loadingMatrix = ref(false)
const matrixAlignGranularity = ref<'1s' | '500ms' | '100ms'>('1s')
const matrixQueryVersion = ref(0)

const clearMatrixData = () => {
  matrixQueryVersion.value += 1
  matrixRows.value = []
  matrixColumns.value = []
  matrixNextCursor.value = null
}

const valueColumns = computed(() =>
  matrixColumns.value.filter((column) => column.prop !== 'ts')
)

// 全选/清空点位
const selectAllPoints = () => {
  tempSelectedVarSeqs.value = pointList.value.map((p) => p.varSeq)
}

const clearAllPoints = () => {
  tempSelectedVarSeqs.value = []
}

const hasResultData = computed(() => !!chartData.value && chartData.value.series.length > 0)

const formatValue = (val: any) => {
  if (val === null || val === undefined || val === '' || val === '-') return '--'
  return typeof val === 'number' ? val.toFixed(2) : val
}

const loadMatrixData = async (reset = false) => {
  if (!searchForm.value.deviceId || searchForm.value.varSeqs.length === 0) return

  if (reset) {
    clearMatrixData()
  }

  const currentVersion = ++matrixQueryVersion.value
  loadingMatrix.value = true

  try {
    const result = await fetchHistoryRawMatrix({
      deviceId: searchForm.value.deviceId,
      varSeqs: searchForm.value.varSeqs,
      startTime: searchForm.value.startTime,
      endTime: searchForm.value.endTime,
      alignGranularity: matrixAlignGranularity.value,
      pageSize: 200,
      cursorTs: matrixNextCursor.value || undefined,
    })

    if (currentVersion !== matrixQueryVersion.value) return

    if (reset) {
      matrixColumns.value = result.columns
    }
    matrixRows.value = [...matrixRows.value, ...result.rows]
    matrixNextCursor.value = result.nextCursor
  } catch (error) {
    if (currentVersion !== matrixQueryVersion.value) return

    console.error('加载矩阵数据失败:', error)
    ElMessage.error('加载矩阵数据失败')
    if (reset) {
      clearMatrixData()
    }
  } finally {
    if (currentVersion === matrixQueryVersion.value) {
      loadingMatrix.value = false
    }
  }
}

const formatMatrixValue = (value: any, dataType?: string): string => {
  if (value === null || value === undefined) return '--'

  const type = (dataType || '').toUpperCase()
  if (type === 'BOOL') {
    // 显式判断，避免字符串 "0" 被当作 truthy
    if (value === true || value === 1 || value === '1') return '1'
    return '0'
  }
  if (['INT', 'DINT', 'WORD', 'DWORD'].includes(type)) return String(Math.round(value))
  if (type === 'REAL') return typeof value === 'number' ? value.toFixed(2) : String(value)
  return String(value)
}

const emptyDescription = computed(() => {
  if (loading.value) {
    return '正在查询历史数据...'
  }

  if (chartData.value) {
    return '当前查询范围暂无历史数据'
  }

  return '请选择设备和点位后查询数据'
})

/** 判断是否为布尔型点位，用于设置阶梯线 */
const isStepPoint = (series: Api.DeviceMonitor.HistoryChartResponse['series'][0]) => {
  // Use exact dataType if provided by backend
  if (series.dataType && series.dataType.toLowerCase() === 'bool') {
    return true
  }

  // 1. 根据名称关键词判断
  const name = series.name.toLowerCase()
  const stepKeywords = ['status', 'running', 'fault', 'alarm', 'switch', 'state', '状态', '运行', '故障', '告警', '开关']
  if (stepKeywords.some(k => name.includes(k))) return true

  // 2. 根据数据值判断（仅包含 0, 1, null）
  const values = series.data.map(d => d[1]).filter(v => v !== null)
  if (values.length > 0 && values.every(v => v === 0 || v === 1)) return true

  return false
}

const buildChartOption = (): echarts.EChartsOption => {
  if (!chartData.value) return {}

  const series = chartData.value.series.map((s) => {
    const isStep = isStepPoint(s)
    const seriesItem: any = {
      name: s.name,
      data: historySampleData(s.data),
      dimensions: [{ name: 'time', type: 'time' }, { name: 'value', type: 'float' }],
      encode: { x: 'time', y: 'value' },
      type: chartType.value === 'area' ? 'line' : chartType.value,
      smooth: false,
      step: isStep ? 'end' : false,
      symbol: 'circle',
      symbolSize: 4,
      showSymbol: false,
      clip: true,
      // 接口已降采样；保留全部返回点，避免二次抽样隐藏小跳变。
      progressive: 0
    }

    if (chartType.value === 'area') {
      seriesItem.areaStyle = {
        opacity: 0.1
      }
    }

    return seriesItem
  })

  return {
    animation: false, // 禁用动画提升性能
    tooltip: {
      trigger: 'axis',
      confine: true,
      axisPointer: {
        type: 'cross',
        label: {
          backgroundColor: '#6a7985'
        }
      }
    },
    legend: {
      data: chartData.value.series.map((s) => s.name),
      bottom: 0,
      type: 'scroll'
    },
    grid: {
      left: '3%',
      right: 85,
      top: 55,
      bottom: 85,
      containLabel: true
    },
    xAxis: {
      type: 'time',
      boundaryGap: ['0%', '0%']
    },
    yAxis: {
      type: 'value',
      scale: true
    },
    dataZoom: createHistoryDataZoom(),
    series
  }
}

const initOrUpdateChart = () => {
  requestAnimationFrame(() => {
    if (!chartRef.value) {
      console.warn('Chart ref is not available')
      return
    }

    const clientWidth = chartRef.value.clientWidth
    const clientHeight = chartRef.value.clientHeight

    if (clientWidth === 0 || clientHeight === 0) {
      console.warn('Chart container has no dimensions, retrying...', { clientWidth, clientHeight })
      setTimeout(() => initOrUpdateChart(), 50)
      return
    }

    if (!chartInstance) {
      chartInstance = echarts.init(chartRef.value, undefined, { renderer: 'svg' })
    }

    chartInstance.setOption(buildChartOption(), true)
  })
}

const timeShortcuts = [
  {
    text: '最近1小时',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 3600000)
      return [formatDateTime(start), formatDateTime(end)]
    }
  },
  {
    text: '最近3小时',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 10800000)
      return [formatDateTime(start), formatDateTime(end)]
    }
  },
  {
    text: '最近6小时',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 21600000)
      return [formatDateTime(start), formatDateTime(end)]
    }
  },
  {
    text: '最近12小时',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 43200000)
      return [formatDateTime(start), formatDateTime(end)]
    }
  },
  {
    text: '最近24小时',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 86400000)
      return [formatDateTime(start), formatDateTime(end)]
    }
  }
]

const formatDateTime = (date: Date) => {
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
}

const buildDefaultTimeRange = (days: number = 1): [string, string] => {
  const end = new Date()
  const start = new Date(end.getTime() - days * 24 * 60 * 60 * 1000)
  return [formatDateTime(start), formatDateTime(end)]
}

const buildFileDate = () => {
  const now = new Date()
  const pad = (value: number) => String(value).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

const buildHistoryErrorMessage = (error: unknown) => {
  if (!isHttpError(error)) {
    return '查询失败'
  }

  if (error.message.includes('time range exceeds')) {
    return '历史查询时间范围不能超过 24 小时'
  }

  if (error.message.includes('varSeqs exceeds the limit') || error.message.includes('pointIds exceeds the limit')) {
    return '单次历史查询最多选择 10 个点位'
  }

  if (error.message.includes('session protection limit')) {
    return '查询结果过大，请缩小时间范围或减少点位后重试'
  }

  return error.message || '查询失败'
}

const loadDeviceList = async () => {
  try {
    const devices = await fetchHistoryDevices()
    // 过滤掉没有 id 的设备（空分组行）
    deviceList.value = devices
      .filter((device) => device.id !== null)
      .map((device) => ({
        id: device.id!,
        name: device.deviceName || device.deviceCode || `设备-${device.id}`,
        deviceCode: device.deviceCode || '',
        deviceName: device.deviceName || '',
        groupId: device.groupId || 0,
        groupName: device.groupName || '',
        typeCode: device.typeCode || '',
        typeName: device.typeName || '其他'
      }))
  } catch (error) {
    console.error('加载设备列表失败:', error)
    ElMessage.error('加载设备列表失败')
  }
}

const loadDeviceTypes = async () => {
  try {
    deviceTypes.value = await fetchDeviceTypes()
  } catch (error) {
    console.error('加载设备类型失败:', error)
  }
}

const loadPointList = async (deviceId?: number) => {
  pointList.value = []

  if (!deviceId) return

  try {
    pointList.value = await fetchHistoryPoints({ deviceId })
  } catch (error) {
    console.error('加载点位列表失败:', error)
    ElMessage.error('加载点位列表失败')
  }
}

const handleChartTypeChange = (type: ChartType) => {
  chartType.value = type
  if (hasResultData.value) {
    initOrUpdateChart()
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

  if (searchForm.value.varSeqs.length > 10) {
    ElMessage.warning('单次历史查询最多选择 10 个点位')
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

  // 校验时间范围不能超过 24 小时
  const start = new Date(searchForm.value.startTime).getTime()
  const end = new Date(searchForm.value.endTime).getTime()
  if (end - start > 24 * 3600 * 1000) {
    ElMessage.warning('历史查询时间范围不能超过 24 小时')
    return
  }

  loading.value = true
  try {
    const result = await fetchHistoryChart({
      deviceId: searchForm.value.deviceId,
      varSeqs: searchForm.value.varSeqs,
      startTime: searchForm.value.startTime,
      endTime: searchForm.value.endTime,
      interval: chartInterval.value,
      aggregate: chartAggregate.value
    })

    chartData.value = result
    renderedRecordCount.value = result.series.reduce((acc, s) => acc + s.data.length, 0)

    // 加载矩阵表数据
    if (searchForm.value.varSeqs.length > 0) {
      void loadMatrixData(true)
    }

    // 先关闭loading让图表容器显示，再初始化图表
    loading.value = false
    initOrUpdateChart()

    pointDrawerVisible.value = false
  } catch (error) {
    console.error('查询失败:', error)
    ElMessage.error(buildHistoryErrorMessage(error))
    loading.value = false
  }
}

const handleResize = () => {
  if (chartInstance && chartRef.value?.clientWidth && chartRef.value?.clientHeight) {
    chartInstance.resize()
  }
}

const handleExport = async () => {
  if (exportTimeRange.value && exportTimeRange.value[0] && exportTimeRange.value[1]) {
    exportForm.value.startTime = exportTimeRange.value[0]
    exportForm.value.endTime = exportTimeRange.value[1]
  }

  if (!exportForm.value.deviceId || !exportForm.value.varSeqs.length) {
    ElMessage.warning('请选择要导出的点位')
    return
  }
  if (!exportForm.value.startTime || !exportForm.value.endTime) {
    ElMessage.warning('请选择导出时间范围')
    return
  }
  if (exportForm.value.varSeqs.length > 20) {
    ElMessage.warning('单次导出最多选择 20 个点位')
    return
  }

  if (exporting.value) return

  exporting.value = true
  exportProgress.value = 0
  exportStatusMsg.value = '正在创建导出任务...'

  try {
    const { taskId } = await fetchHistoryExport({
      deviceId: exportForm.value.deviceId,
      varSeqs: exportForm.value.varSeqs,
      startTime: exportForm.value.startTime,
      endTime: exportForm.value.endTime
    })

    ElMessage.info('导出任务已创建，请稍候...')

    exportTimer = setInterval(async () => {
      try {
        const result = await fetchHistoryExportStatus(taskId)
        exportProgress.value = result.progress || 0
        exportStatusMsg.value = result.message || '正在处理...'

        if (result.status === 'completed') {
          if (exportTimer) clearInterval(exportTimer)
          exporting.value = false
          exportProgress.value = 100
          ElMessage.success('导出成功，即将开始下载')
          await downloadExportFile(taskId)
          setTimeout(() => {
            exportDialogVisible.value = false
          }, 1000)
        } else if (result.status === 'failed') {
          if (exportTimer) clearInterval(exportTimer)
          exporting.value = false
          ElMessage.error(result.message || '导出失败')
        }
      } catch (err) {
        console.error('获取导出状态失败:', err)
        if (exportTimer) clearInterval(exportTimer)
        exporting.value = false
        ElMessage.error('获取导出状态失败')
      }
    }, 2000)
  } catch (error) {
    console.error('导出失败:', error)
    ElMessage.error('创建导出任务失败')
    exporting.value = false
  }
}

const downloadExportFile = async (taskId: string) => {
  const blob = await fetchHistoryExportDownload(taskId)
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  const timestamp = formatDateTime(new Date()).replace(/[-:\s]/g, '')
  link.href = url
  link.download = `历史数据_${timestamp}.csv`
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}

watch(
  () => searchForm.value.deviceId,
  (value) => {
    void loadPointList(value)
  }
)

watch(matrixAlignGranularity, () => {
  if (hasResultData.value) {
    void loadMatrixData(true)
  }
})

watch(timeRange, (value) => {
  if (value && value[0] && value[1]) {
    searchForm.value.startTime = value[0]
    searchForm.value.endTime = value[1]
  }
})

// 缓存判断：是否可以缓存当前结果
const canCacheHistoryResult = computed(() => {
  return (
    renderedRecordCount.value <= HISTORY_CACHE_MAX_CHART_POINTS &&
    matrixRows.value.length <= HISTORY_CACHE_MAX_MATRIX_ROWS &&
    searchForm.value.varSeqs.length <= HISTORY_CACHE_MAX_POINTS
  )
})

// 保存缓存（查询条件始终保存，数据根据大小决定）
const saveHistoryCache = () => {
  if (!historyUserStore.isLogin || historyUserStore.info.userId !== historyOwnerUserId) return
  try {
    const cacheData: any = {
      // 查询条件始终保存
      searchForm: {
        deviceId: searchForm.value.deviceId,
        varSeqs: [...searchForm.value.varSeqs],
        startTime: searchForm.value.startTime,
        endTime: searchForm.value.endTime
      },
      timeRange: [...timeRange.value],
      chartInterval: chartInterval.value,
      chartAggregate: chartAggregate.value,
      chartType: chartType.value,
      matrixAlignGranularity: matrixAlignGranularity.value,
      timestamp: Date.now()
    }

    // 数据仅在满足条件时保存
    if (canCacheHistoryResult.value) {
      cacheData.chartData = chartData.value
      cacheData.matrixRows = [...matrixRows.value]
      cacheData.matrixColumns = matrixColumns.value
      cacheData.matrixNextCursor = matrixNextCursor.value
    }

    localStorage.setItem(HISTORY_CACHE_KEY, JSON.stringify(cacheData))
  } catch (error) {
    console.warn('保存历史查询缓存失败:', error)
  }
}

// 恢复缓存
const restoreHistoryCache = (): { restoredData: boolean; restoredConditions: boolean } => {
  const result = { restoredData: false, restoredConditions: false }
  try {
    const cached = localStorage.getItem(HISTORY_CACHE_KEY)
    if (!cached) return result

    const cacheData = JSON.parse(cached)

    const cacheAge = Date.now() - cacheData.timestamp

    // 查询条件保留更久；过期后整体清理
    if (cacheAge > HISTORY_CONDITION_CACHE_MAX_AGE) {
      localStorage.removeItem(HISTORY_CACHE_KEY)
      return result
    }

    // 恢复查询条件（始终恢复）
    if (cacheData.searchForm) {
      searchForm.value.deviceId = cacheData.searchForm.deviceId
      searchForm.value.varSeqs = cacheData.searchForm.varSeqs || []
      searchForm.value.startTime = cacheData.searchForm.startTime || ''
      searchForm.value.endTime = cacheData.searchForm.endTime || ''
      result.restoredConditions = true
    }

    if (cacheData.timeRange) {
      timeRange.value = cacheData.timeRange
    }

    if (cacheData.chartInterval) chartInterval.value = cacheData.chartInterval
    if (cacheData.chartAggregate) chartAggregate.value = cacheData.chartAggregate
    if (cacheData.chartType) chartType.value = cacheData.chartType
    if (cacheData.matrixAlignGranularity) matrixAlignGranularity.value = cacheData.matrixAlignGranularity

    const canRestoreData = cacheAge <= HISTORY_DATA_CACHE_MAX_AGE

    // 恢复数据（仅当有缓存且未超过数据缓存时间）
    if (canRestoreData && cacheData.chartData && cacheData.chartData.series?.length > 0) {
      chartData.value = cacheData.chartData
      renderedRecordCount.value = cacheData.chartData.series.reduce((acc: number, s: any) => acc + (s.data?.length || 0), 0)
      result.restoredData = true
    }

    if (canRestoreData && cacheData.matrixRows?.length > 0) {
      matrixRows.value = cacheData.matrixRows
      result.restoredData = true
    }

    if (canRestoreData && cacheData.matrixColumns?.length > 0) {
      matrixColumns.value = cacheData.matrixColumns
    }

    if (canRestoreData && cacheData.matrixNextCursor) {
      matrixNextCursor.value = cacheData.matrixNextCursor
    }

    return result
  } catch (error) {
    console.warn('恢复历史查询缓存失败:', error)
    return result
  }
}

// 清理缓存
const clearHistoryCache = () => {
  localStorage.removeItem(HISTORY_CACHE_KEY)
}

onMounted(async () => {
  // 先设置默认时间范围
  const defaultRange = buildDefaultTimeRange(1)
  timeRange.value = defaultRange
  searchForm.value.startTime = defaultRange[0]
  searchForm.value.endTime = defaultRange[1]

  // 加载设备列表
  await loadDeviceList()

  // 尝试恢复缓存（查询条件 + 数据）
  const { restoredData, restoredConditions } = restoreHistoryCache()

  // 如果恢复了设备，加载对应点位列表
  if (restoredConditions && searchForm.value.deviceId) {
    await loadPointList(searchForm.value.deviceId)
  }

  // 如果恢复了数据，重新渲染图表
  if (restoredData && hasResultData.value) {
    await nextTick()
    initOrUpdateChart()
  }

  window.addEventListener('resize', handleResize)
})

// 路由离开处理
onBeforeRouteLeave(async (to, from, next) => {
  // 没有选择设备，直接离开并清理缓存
  if (!searchForm.value.deviceId) {
    clearHistoryCache()
    next()
    return
  }

  // 已选择设备但尚未选择点位，也保存设备和时间条件
  if (searchForm.value.varSeqs.length === 0) {
    saveHistoryCache()
    next()
    return
  }

  // 有大结果数据：提示用户数据会丢失
  if (hasResultData.value && !canCacheHistoryResult.value) {
    try {
      await ElMessageBox.confirm(
        '当前历史查询结果较大，切换页面后图表和表格数据将被释放（设备和点位选择会保留）。是否继续离开？',
        '提示',
        {
          confirmButtonText: '离开',
          cancelButtonText: '取消',
          type: 'warning'
        }
      )
      // 用户确认离开，保存查询条件（丢弃大数据）
      saveHistoryCache()
      next()
    } catch {
      // 用户取消，留在当前页
      next(false)
    }
  } else {
    // 小结果或无结果：自动缓存后离开
    saveHistoryCache()
    next()
  }
})

onUnmounted(() => {
  if (searchForm.value.deviceId) {
    saveHistoryCache()
  }
  window.removeEventListener('resize', handleResize)
  if (exportTimer) {
    clearInterval(exportTimer)
    exportTimer = null
  }
  // 清理图表实例（不清理缓存，缓存通过路由钩子处理）
  if (chartInstance) {
    chartInstance.dispose()
    chartInstance = null
  }
})
</script>

<style scoped lang="scss">
.history-page {
  display: flex;
  flex-direction: column;
  gap: 16px;
  height: 100%;
  overflow-y: auto;
  padding-bottom: 24px;
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
  align-items: center;
  gap: 24px;
}

.control-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.control-label {
  font-size: 13px;
  color: var(--art-gray-600);
  white-space: nowrap;
}

.device-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 16px;
  background: var(--art-gray-100);
  border: 1px solid var(--art-gray-300);
  border-radius: 10px;
  cursor: pointer;
  transition: all 0.2s;

  &:hover {
    background: var(--art-gray-200);
  }
}

.device-name {
  font-size: 14px;
  font-weight: 500;
  color: var(--art-gray-900);
}

.device-arrow {
  color: #8a94a6;
}

.control-middle {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
}

.point-selector-box {
  display: flex;
  align-items: center;
  padding: 8px 16px;
  background: var(--art-gray-100);
  border: 1px solid var(--art-gray-300);
  border-radius: 10px;
  cursor: pointer;
  min-width: 200px;
  max-width: 300px;
  transition: all 0.2s;

  &:hover {
    background: var(--art-gray-200);
  }
}

.point-tags-text {
  font-size: 14px;
  color: var(--art-gray-900);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.point-placeholder {
  font-size: 14px;
  color: #8a94a6;
}

.time-picker {
  width: 360px;
}

.control-right {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-left: auto;
}

.interval-select {
  width: 120px;
}

.aggregate-select {
  width: 120px;
}

// 主内容区
.main-content {
  display: flex;
  flex-direction: column;
  gap: 16px;
  flex-shrink: 0;
}

.chart-card {
  border: 1px solid var(--art-card-border);
  border-radius: 18px;

  :deep(.el-card__body) {
    display: flex;
    flex-direction: column;
    gap: 16px;
    padding: 22px;
  }
}

.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 4px;
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

.chart-wrapper {
  background: var(--el-bg-color);
  border-radius: 16px;
  overflow: hidden;
  border: 1px solid var(--art-gray-200);
}

.echarts-container {
  width: 100%;
  height: clamp(360px, 45vh, 520px);
  background: var(--el-bg-color);
}

.table-card {
  border: 1px solid var(--art-card-border);
  border-radius: 18px;
}

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

.table-controls {
  display: flex;
  align-items: center;
  gap: 8px;
}

.granularity-select {
  width: 120px;
}

.load-more {
  display: flex;
  justify-content: center;
  margin-top: 16px;
}

// 设备选择器
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
  margin-bottom: 12px;
}

.point-checkbox-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
  max-height: 300px;
  overflow-y: auto;
  padding: 8px;
  border: 1px solid var(--art-gray-300);
  border-radius: 10px;
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

@media (max-width: 1200px) {
  .control-bar {
    flex-wrap: wrap;
  }

  .control-middle {
    flex-wrap: wrap;
  }

  .time-picker {
    width: 300px;
  }
}

@media (max-width: 768px) {
  .control-bar {
    flex-direction: column;
    align-items: stretch;
  }

  .control-left,
  .control-middle,
  .control-right {
    margin-left: 0;
  }
}
</style>
