<!-- 历史数据查询页 -->
<template>
  <div class="history-page">
    <ElCard class="panel-card viewer-card">
      <div class="card-head card-head--split">
        <div>
          <p class="card-title">历史分析</p>
          <p class="card-subtitle">ECharts 趋势渲染</p>
        </div>
        <div class="head-actions">
          <ElButtonGroup size="small">
            <ElButton
              v-for="type in (['line', 'area', 'bar', 'scatter'] as const)"
              :key="type"
              :type="chartType === type ? 'primary' : 'default'"
              @click="handleChartTypeChange(type)"
            >
              {{ { line: '折线图', area: '面积图', bar: '柱状图', scatter: '散点图' }[type] }}
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
        description="正在读取历史数据并准备图表渲染"
        min-height="520px"
        :rows="6"
      />
      <template v-else-if="hasResultData">
        <div class="chart-wrapper">
          <div ref="chartRef" class="echarts-container" v-show="!loading"></div>
        </div>

        <div class="table-wrapper">
          <p class="table-title">历史数据明细</p>
          <ElTable :data="tableData" border stripe height="400" size="small">
            <ElTableColumn prop="time" label="采样时间" fixed="left" width="180" sortable />
            <ElTableColumn
              v-for="point in selectedPoints"
              :key="point.varSeq"
              :prop="`point_${point.varSeq}`"
              :label="point.pointName"
              min-width="120"
            >
              <template #default="scope">
                {{ formatValue(scope.row[`point_${point.varSeq}`]) }}
              </template>
            </ElTableColumn>
          </ElTable>
        </div>
      </template>
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
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import type { CheckboxValueType } from 'element-plus'
import { ElMessage } from 'element-plus'
import { Setting, Clock, InfoFilled } from '@element-plus/icons-vue'
import * as echarts from 'echarts'
import MonitorLoadingPlaceholder from '@/components/device-monitor/MonitorLoadingPlaceholder.vue'
import { saveBlobAs } from '@/utils/file/save-blob'
import {
  fetchHistoryDevices,
  fetchHistoryExport,
  fetchHistoryPoints,
  fetchHistoryQuery
} from '@/api/device-monitor'
import { isHttpError } from '@/utils/http/error'

defineOptions({ name: 'HistoryData' })

type PointInfo = Api.DeviceMonitor.HistoryPointItem
type DeviceInfo = Api.DeviceMonitor.HistoryDeviceItem
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
const pointSearch = ref('')
const drawerVisible = ref(false)

const deviceList = ref<DeviceInfo[]>([])
const pointList = ref<PointInfo[]>([])
const selectedPoints = ref<PointInfo[]>([])

const loading = ref(false)
const exporting = ref(false)
const chartType = ref<ChartType>('line')
const renderedRecordCount = ref(0)

const chartRef = ref<HTMLElement>()
let chartInstance: echarts.ECharts | null = null
const historyData = ref<Api.DeviceMonitor.HistoryDataResponse | null>(null)

const hasResultData = computed(() => !!historyData.value && historyData.value.times.length > 0)

const tableData = computed(() => {
  if (!historyData.value) return []
  const { times, points } = historyData.value
  return times.map((time, index) => {
    const row: any = { time }
    points.forEach((p) => {
      row[`point_${p.varSeq}`] = p.values[index]
    })
    return row
  })
})

const formatValue = (val: any) => {
  if (val === null || val === undefined || val === '') return '--'
  return typeof val === 'number' ? val.toFixed(2) : val
}

const emptyDescription = computed(() => {
  if (loading.value) {
    return '正在查询历史数据...'
  }

  if (historyData.value) {
    return '当前查询范围暂无历史数据'
  }

  return '请选择设备和点位后查询数据'
})

const buildChartOption = (): echarts.EChartsOption => {
  if (!historyData.value) return {}

  const series = historyData.value.points.map((point) => {
    const seriesItem: any = {
      name: point.pointName,
      data: point.values,
      type: chartType.value === 'area' ? 'line' : chartType.value,
      smooth: true,
      symbol: 'circle',
      symbolSize: 4,
      showSymbol: false
    }

    if (chartType.value === 'area') {
      seriesItem.areaStyle = {
        opacity: 0.1
      }
    }

    return seriesItem
  })

  return {
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'cross',
        label: {
          backgroundColor: '#6a7985'
        }
      }
    },
    legend: {
      data: historyData.value.points.map((p) => p.pointName),
      bottom: 0,
      type: 'scroll'
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '15%',
      containLabel: true
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: historyData.value.times
    },
    yAxis: {
      type: 'value'
    },
    dataZoom: [
      {
        type: 'slider',
        show: true,
        xAxisIndex: [0],
        start: 0,
        end: 100,
        bottom: 30
      },
      {
        type: 'inside',
        xAxisIndex: [0],
        start: 0,
        end: 100
      }
    ],
    series
  }
}

const initOrUpdateChart = () => {
  if (!chartRef.value) return

  if (!chartInstance) {
    chartInstance = echarts.init(chartRef.value)
  }

  chartInstance.setOption(buildChartOption(), true)
}

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
    text: '最近3小时',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 10800000)
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
    text: '最近12小时',
    value: () => {
      const end = new Date()
      const start = new Date(end.getTime() - 43200000)
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
    return '单次历史查询最多选择 20 个点位'
  }

  if (error.message.includes('session protection limit')) {
    return '查询结果过大，请缩小时间范围或减少点位后重试'
  }

  return error.message || '查询失败'
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

  if (searchForm.value.varSeqs.length > 20) {
    ElMessage.warning('单次历史查询最多选择 20 个点位')
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
    const result = await fetchHistoryQuery({
      deviceId: searchForm.value.deviceId,
      varSeqs: searchForm.value.varSeqs,
      startTime: searchForm.value.startTime,
      endTime: searchForm.value.endTime
    })

    historyData.value = result
    renderedRecordCount.value = result.times.length
    selectedPoints.value = pointList.value.filter((item) => searchForm.value.varSeqs.includes(item.varSeq))

    await nextTick()
    initOrUpdateChart()

    drawerVisible.value = false
  } catch (error) {
    console.error('查询失败:', error)
    ElMessage.error(buildHistoryErrorMessage(error))
  } finally {
    loading.value = false
  }
}

const handleResize = () => {
  chartInstance?.resize()
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
  const defaultRange = buildDefaultTimeRange(1)
  timeRange.value = defaultRange
  searchForm.value.startTime = defaultRange[0]
  searchForm.value.endTime = defaultRange[1]

  void loadDeviceList()
  window.addEventListener('resize', handleResize)
})

onUnmounted(() => {
  window.removeEventListener('resize', handleResize)
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

.chart-wrapper {
  background: #fff;
  border-radius: 16px;
  overflow: hidden;
  border: 1px solid #eef1f6;
}

.echarts-container {
  min-height: 480px;
  background: linear-gradient(180deg, #ffffff 0%, #f8fbff 100%);
}

.table-wrapper {
  margin-top: 8px;
  background: #fff;
  padding: 16px;
  border-radius: 16px;
  border: 1px solid #eef1f6;
}

.table-title {
  font-size: 15px;
  font-weight: 600;
  margin-bottom: 12px;
  color: #1d2433;
  display: flex;
  align-items: center;

  &::before {
    content: '';
    display: inline-block;
    width: 4px;
    height: 16px;
    background: #4a6cf7;
    margin-right: 8px;
    border-radius: 2px;
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
