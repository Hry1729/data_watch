<!-- 工作台（总览页） -->
<template>
  <div class="dashboard-page">
    <ArtTextScroll
      v-if="showWelcomeBanner"
      text="欢迎使用尚东设备数据采集与监控平台，有问题请及时联系管理员"
      type="primary"
      showClose
      @close="handleCloseWelcome"
    />

    <section class="stats-grid" :class="{ 'is-loading': overviewLoading }">
      <div v-for="item in statCards" :key="item.key + item.targetValue" class="art-card relative flex flex-col justify-center h-35 px-5 bg-[var(--default-box-color)]">
        <span class="text-g-700 text-sm">{{ item.label }}</span>
        <div class="flex items-baseline mt-2">
          <ArtCountTo class="text-[26px] font-medium" :target="item.targetValue" :duration="1300" :decimals="item.decimals" />
          <span v-if="item.suffix" class="text-[26px] font-medium ml-0.5">{{ item.suffix }}</span>
        </div>
        <div class="flex-c mt-1">
          <span class="text-xs text-g-600">{{ item.trendLabel }}</span>
          <span
            class="ml-1 text-xs font-semibold"
            :class="item.trendClass"
          >
            {{ item.trendValue }}
          </span>
        </div>
        <div
          class="absolute top-0 bottom-0 right-5 m-auto w-12 h-12 rounded-xl flex-cc"
          :style="{ background: item.iconBg, color: item.iconColor }"
        >
          <ArtSvgIcon :icon="item.icon" class="text-xl" />
        </div>
      </div>
    </section>

    <section class="main-grid">
      <div class="trend-card art-card flex flex-col h-[360px] p-5 bg-[var(--default-box-color)]">
        <div class="art-card-header flex justify-between items-center pr-0">
          <div class="title">
            <h4 class="text-lg font-medium text-g-900">采集量 / 告警趋势</h4>
          </div>
          <ElSpace>
            <ElRadioGroup v-model="trendRangeType" size="small" class="range-switch" @change="loadTrend">
              <ElRadioButton label="今天" value="today" />
              <ElRadioButton label="24h" value="last24h" />
              <ElRadioButton label="7d" value="last7d" />
            </ElRadioGroup>
            <ElButton text type="primary" @click="loadTrend">刷新</ElButton>
          </ElSpace>
        </div>
        <div class="chart-frame chart-frame--lg flex-1 min-h-0">
          <div ref="trendChartRef" class="chart-surface chart-surface--lg h-full" />
          <MonitorLoadingPlaceholder
            v-if="trendLoading"
            class="chart-loading"
            title="趋势图加载中"
            description="正在异步加载图表依赖"
            min-height="200px"
            :rows="4"
          />
        </div>
      </div>

      <div class="health-card art-card flex flex-col h-[360px] p-5 bg-[var(--default-box-color)]">
        <div class="art-card-header pr-0">
          <div class="title">
            <h4 class="text-lg font-medium text-g-900">设备通讯概览</h4>
          </div>
        </div>
        <div class="health-card__body flex-1 min-h-0 pt-4">
          <ArtRingChart
            v-if="!healthLoading"
            :data="healthChartData"
            :radius="['54%', '70%']"
            :colors="['#4A6CF7', '#D66B88', '#F4A542', '#3DBB9A']"
            :showLegend="true"
            legendPosition="bottom"
            :centerText="healthCenterText"
          />
          <MonitorLoadingPlaceholder
            v-else
            class="chart-loading"
            title="通讯概览加载中"
            description="正在准备通讯状态图"
            min-height="180px"
            :rows="3"
          />
        </div>
      </div>
    </section>

    <section class="bottom-grid">
      <div class="distribution-card art-card flex flex-col h-[350px] p-5 bg-[var(--default-box-color)]">
        <div class="art-card-header flex justify-between items-center pr-0">
          <div class="title">
            <h4 class="text-lg font-medium text-g-900">设备类型分布</h4>
          </div>
          <ElButton text type="primary" @click="goToDevice">查看设备</ElButton>
        </div>
        <div class="distribution-body flex-1 min-h-0 pt-4">
          <MonitorLoadingPlaceholder
            v-if="deviceTypeLoading"
            class="chart-loading"
            title="设备类型加载中"
            description="正在异步加载统计图"
            min-height="260px"
            :rows="4"
          />
          <ArtHBarChart
            v-else
            :data="deviceTypeChartData"
            :xAxisData="deviceTypeXAxisData"
            :showLegend="true"
            :stack="true"
            legendPosition="bottom"
            barWidth="30%"
          />
        </div>
      </div>

      <div class="alarms-card list-card recent-wide art-card flex flex-col h-[350px] p-5 bg-[var(--default-box-color)] overflow-hidden">
        <div class="art-card-header flex justify-between items-center pr-0">
          <div class="title">
            <h4 class="text-lg font-medium text-g-900">最近系统报警</h4>
          </div>
          <ElButton text type="primary" @click="goToSystemLog">查看更多</ElButton>
        </div>
        <div class="table-like mt-2 flex-1 min-h-0 overflow-y-auto">
          <MonitorLoadingPlaceholder
            v-if="recentLoading"
            class="list-loading"
            title="系统报警加载中"
            description="正在读取最新报警"
            min-height="220px"
            :rows="4"
          />
          <template v-else>
          <div class="table-like__head recent-head shrink-0">
            <span>时间</span>
            <span>级别</span>
            <span>类型</span>
            <span>来源</span>
            <span>内容</span>
          </div>
          <div v-if="recentLogList.length" class="table-like__body">
            <div v-for="item in recentLogList" :key="item.id" class="table-like__row recent-row">
              <span>{{ getTimeOnly(item.time) }}</span>
              <ElTag :type="getLogLevelTagType(item.level)" effect="light" round>
                {{ item.level }}
              </ElTag>
              <ElTag type="info" effect="light" round>
                {{ getLogTypeText(item.type) }}
              </ElTag>
              <span>{{ item.source || '-' }}</span>
              <span class="ellipsis">{{ item.content }}</span>
            </div>
          </div>
          <ElEmpty v-else description="暂无系统报警" :image-size="96" />
          </template>
        </div>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
  import { computed, onActivated, onDeactivated, onUnmounted, ref } from 'vue'
  import { useRouter } from 'vue-router'
  import ArtCountTo from '@/components/core/text-effect/art-count-to/index.vue'
  import ArtTextScroll from '@/components/core/text-effect/art-text-scroll/index.vue'
  import ArtHBarChart from '@/components/core/charts/art-h-bar-chart/index.vue'
  import ArtRingChart from '@/components/core/charts/art-ring-chart/index.vue'
  import MonitorLoadingPlaceholder from '@/components/device-monitor/MonitorLoadingPlaceholder.vue'
  import {
    fetchDashboardDeviceConnectionHealth,
    fetchDashboardDeviceTypeDistribution,
    fetchDashboardOverview,
    fetchDashboardTrend,
    fetchLogList
  } from '@/api/device-monitor'

  defineOptions({ name: 'MonitorDashboard' })

  const router = useRouter()

  // 欢迎横幅状态
  const WELCOME_BANNER_KEY = 'dashboard-welcome-closed'
  const showWelcomeBanner = ref(!localStorage.getItem(WELCOME_BANNER_KEY))

  const handleCloseWelcome = () => {
    showWelcomeBanner.value = false
    localStorage.setItem(WELCOME_BANNER_KEY, 'true')
  }

  const statsData = ref<{
    totalDevices: number
    onlineDevices: number
    offlineDevices: number
    riskDevices: number
    totalPoints: number
    systemAlertCount: number
  }>({
    totalDevices: 0,
    onlineDevices: 0,
    offlineDevices: 0,
    riskDevices: 0,
    totalPoints: 0,
    systemAlertCount: 0
  })

  const trendSummary = ref<Api.DeviceMonitor.DashboardTrend['summary']>({
    collectCountTotal: 0,
    alarmCountTotal: 0,
    onlineRateAvg: 0
  })
  const systemAlertCompare = ref({
    countDiff: 0,
    ratioDiff: 0
  })

  const trendRangeType = ref<Api.DeviceMonitor.DashboardTrendParams['rangeType']>('last24h')
  const deviceTypeGroups = ref<Api.DeviceMonitor.DashboardDeviceTypeDistributionGroup[]>([])
  const connectionHealthOverview = ref({
    onlineDevices: 0,
    offlineDevices: 0,
    riskDevices: 0
  })
  const connectionHealthList = ref<Api.DeviceMonitor.DashboardDeviceConnectionHealthItem[]>([])
  const recentLogList = ref<Api.DeviceMonitor.LogListItem[]>([])
  const overviewLoading = ref(false)
  const trendLoading = ref(false)
  const deviceTypeLoading = ref(false)
  const healthLoading = ref(false)
  const recentLoading = ref(false)

  const trendChartRef = ref<HTMLElement>()

  type EChartsInstance = import('echarts').ECharts

  let echartsModulePromise: Promise<typeof import('echarts')> | null = null
  let trendChart: EChartsInstance | null = null
  let refreshTimer: ReturnType<typeof setInterval> | null = null
  let dashboardLoadingPromise: Promise<void> | null = null
  let lastDashboardLoadedAt = 0

  const palette = ['#4A6CF7', '#3DBB9A', '#F4A542', '#D66B88', '#6E7FA6', '#95A3BF']
  const DASHBOARD_REFRESH_INTERVAL_MS = 30000
  const DASHBOARD_ACTIVATE_REUSE_MS = 10000

  const onlineRate = computed(() => {
    if (!statsData.value.totalDevices) return 0
    return Number(((statsData.value.onlineDevices / statsData.value.totalDevices) * 100).toFixed(1))
  })

  const healthChartData = computed(() => {
    const highLatency = connectionHealthList.value.filter((item) => item.exceptionCount > 0 || item.reconnectCount > 0).length
    const timeoutCount = connectionHealthList.value.filter((item) => item.timeoutCount > 0).length
    return [
      { name: '在线设备', value: statsData.value.onlineDevices },
      { name: '离线设备', value: statsData.value.offlineDevices },
      { name: '高延迟', value: highLatency },
      { name: '超时', value: timeoutCount }
    ]
  })

  const healthCenterText = computed(() => {
    return `${statsData.value.onlineDevices + statsData.value.offlineDevices} 台`
  })

  // 设备类型分布 - 水平堆叠图表数据
  const deviceTypeXAxisData = computed(() => {
    return deviceTypeGroups.value.map((item) => item.typeName)
  })

  const deviceTypeChartData = computed(() => {
    const onlineData = deviceTypeGroups.value.map((item) => item.onlineCount)
    const offlineData = deviceTypeGroups.value.map((item) => item.deviceCount - item.onlineCount)
    return [
      { name: '在线', data: onlineData, stack: 'total' },
      { name: '离线', data: offlineData, stack: 'total' }
    ]
  })

  const loadEcharts = () => {
    if (!echartsModulePromise) {
      echartsModulePromise = import('echarts')
    }

    return echartsModulePromise
  }

  const statCards = computed(() => {
    const alertTrendPrefix = systemAlertCompare.value.countDiff > 0 ? '+' : ''

    return [
      {
        key: 'devices',
        label: '设备总数',
        targetValue: Number(statsData.value.totalDevices) || 0,
        decimals: 0,
        suffix: '',
        trendLabel: '在线',
        trendValue: `${statsData.value.onlineDevices}`,
        trendClass: 'text-success',
        icon: 'ri:hard-drive-3-line',
        iconBg: '#edf2ff',
        iconColor: '#4A6CF7'
      },
      {
        key: 'online-rate',
        label: '在线率',
        targetValue: Number(onlineRate.value) || 0,
        decimals: 1,
        suffix: '%',
        trendLabel: '风险',
        trendValue: `${statsData.value.riskDevices}`,
        trendClass: statsData.value.riskDevices > 0 ? 'text-warning' : 'text-success',
        icon: 'ri:wifi-line',
        iconBg: '#e9fbf5',
        iconColor: '#18a47a'
      },
      {
        key: 'points',
        label: '采集点位',
        targetValue: Number(statsData.value.totalPoints) || 0,
        decimals: 0,
        suffix: '',
        trendLabel: '趋势',
        trendValue: `${trendSummary.value.collectCountTotal.toLocaleString()}`,
        trendClass: 'text-success',
        icon: 'ri:flashlight-line',
        iconBg: '#fff6e8',
        iconColor: '#d18b2f'
      },
      {
        key: 'alerts',
        label: '近24小时系统告警',
        targetValue: Number(statsData.value.systemAlertCount) || 0,
        decimals: 0,
        suffix: '',
        trendLabel: '较上周期',
        trendValue: `${alertTrendPrefix}${systemAlertCompare.value.countDiff}`,
        trendClass: systemAlertCompare.value.countDiff > 0 ? 'text-danger' : 'text-success',
        icon: 'ri:alarm-warning-line',
        iconBg: '#fff0f3',
        iconColor: '#d44f63'
      }
    ]
  })

  const pad2 = (value: number) => value.toString().padStart(2, '0')

  const formatLocalDateTime = (value: Date) => {
    return `${value.getFullYear()}-${pad2(value.getMonth() + 1)}-${pad2(value.getDate())} ${pad2(value.getHours())}:${pad2(value.getMinutes())}:${pad2(value.getSeconds())}`
  }

  const getTimeOnly = (value: string) => {
    if (!value) return '-'
    return value.split(' ')[1] || value
  }

  const getLogLevelTagType = (level: Api.DeviceMonitor.LogLevel) => {
    if (level === 'ERROR' || level === 'CRITICAL') return 'danger'
    if (level === 'WARN') return 'warning'
    return 'info'
  }

  const getLogTypeText = (type: Api.DeviceMonitor.LogType) => {
    const typeMap: Record<Api.DeviceMonitor.LogType, string> = {
      system: '系统',
      auth: '认证',
      operation: '操作',
      collect: '采集',
      storage: '存储',
      frontend: '前端',
      security: '安全',
      alarm: '告警'
    }
    return typeMap[type] || type
  }

  const goToSystemLog = () => {
    router.push({
      path: '/device-monitor/log',
      query: { level: 'WARN' }
    })
  }

  const loadOverview = async () => {
    overviewLoading.value = true
    try {
      const overview = await fetchDashboardOverview()
      statsData.value = {
        totalDevices: overview.deviceTotal,
        onlineDevices: overview.onlineDeviceTotal,
        offlineDevices: overview.offlineDeviceTotal,
        riskDevices: overview.riskDeviceTotal,
        totalPoints: overview.pointTotal,
        systemAlertCount: overview.systemAlertTotal
      }
      systemAlertCompare.value = overview.systemAlertComparePrev || { countDiff: 0, ratioDiff: 0 }
    } finally {
      overviewLoading.value = false
    }
  }

  const ensureChart = async (target: HTMLElement | undefined, chart: EChartsInstance | null) => {
    if (!target) return null
    if (chart) return chart

    const echarts = await loadEcharts()
    return echarts.init(target)
  }

  const loadTrend = async () => {
    trendLoading.value = true
    try {
      const trend = await fetchDashboardTrend({
        rangeType: trendRangeType.value,
        metricKeys: ['collectCount', 'alarmCount', 'onlineRate']
      })

      trendSummary.value = trend.summary
      trendChart = await ensureChart(trendChartRef.value, trendChart)
      if (!trendChart) return

      const echarts = await loadEcharts()
      const metricColorMap: Record<string, string> = {
        collectCount: '#4A6CF7',
        alarmCount: '#D66B88',
        onlineRate: '#3DBB9A'
      }

      trendChart.setOption({
        color: ['#4A6CF7', '#D66B88', '#3DBB9A'],
        tooltip: { trigger: 'axis' },
        legend: {
          top: 8,
          right: 0,
          icon: 'circle',
          itemWidth: 8,
          itemHeight: 8,
          textStyle: { color: '#66758f' },
          data: (trend.series || []).map((item) => item.metricName)
        },
        grid: {
          top: 52,
          left: 0,
          right: 0,
          bottom: 6,
          containLabel: true
        },
        xAxis: {
          type: 'category',
          boundaryGap: false,
          data: trend.timeAxis,
          axisLine: { lineStyle: { color: '#e5e9f2' } },
          axisLabel: { color: '#8a94a6' }
        },
        yAxis: [
          {
            type: 'value',
            splitLine: { lineStyle: { color: '#eef1f6', type: 'dashed' } },
            axisLabel: { color: '#8a94a6' }
          },
          {
            type: 'value',
            min: 0,
            max: 100,
            splitLine: { show: false },
            axisLabel: {
              color: '#8a94a6',
              formatter: '{value}%'
            }
          }
        ],
        series: (trend.series || []).map((item) => ({
          name: item.metricName,
          type: 'line',
          smooth: true,
          symbol: 'circle',
          symbolSize: 7,
          yAxisIndex: item.unit === '%' ? 1 : 0,
          data: item.data,
          lineStyle: { width: 3, color: metricColorMap[item.metricKey] || '#4A6CF7' },
          itemStyle: { color: metricColorMap[item.metricKey] || '#4A6CF7' },
          areaStyle:
            item.metricKey === 'collectCount'
              ? {
                  color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
                    { offset: 0, color: 'rgba(74, 108, 247, 0.22)' },
                    { offset: 1, color: 'rgba(74, 108, 247, 0.02)' }
                  ])
                }
              : undefined
        }))
      })
    } finally {
      trendLoading.value = false
    }
  }

  const loadDeviceTypeDistribution = async () => {
    deviceTypeLoading.value = true
    try {
      const data = await fetchDashboardDeviceTypeDistribution(10)
      deviceTypeGroups.value = data.groups || []
    } finally {
      deviceTypeLoading.value = false
    }
  }

  const loadConnectionHealth = async () => {
    healthLoading.value = true
    try {
      const health = await fetchDashboardDeviceConnectionHealth({
        statRange: 'last24h',
        topN: 8,
        includeOffline: true
      })

      connectionHealthOverview.value = health.overview
      connectionHealthList.value = health.list || []

    } finally {
      healthLoading.value = false
    }
  }

  const loadRecentLogs = async () => {
    recentLoading.value = true
    try {
      const now = new Date()
      const twentyFourHoursAgo = new Date(now.getTime() - 24 * 60 * 60 * 1000)
      const startTime = formatLocalDateTime(twentyFourHoursAgo)
      const endTime = formatLocalDateTime(now)

      // 并行请求 WARN、ERROR、CRITICAL 级别日志
      const [warnResult, errorResult, criticalResult] = await Promise.all([
        fetchLogList({ pageNo: 1, pageSize: 10, startTime, endTime, level: 'WARN' }),
        fetchLogList({ pageNo: 1, pageSize: 10, startTime, endTime, level: 'ERROR' }),
        fetchLogList({ pageNo: 1, pageSize: 10, startTime, endTime, level: 'CRITICAL' })
      ])

      // 合并并按时间排序，取前8条
      const mergedList = [
        ...(warnResult.list || []),
        ...(errorResult.list || []),
        ...(criticalResult.list || [])
      ]
        .sort((a, b) => (b.time || '').localeCompare(a.time || ''))
        .slice(0, 8)

      recentLogList.value = mergedList
    } finally {
      recentLoading.value = false
    }
  }

  const loadDashboard = async (force = false) => {
    if (!force && dashboardLoadingPromise) {
      return dashboardLoadingPromise
    }

    const now = Date.now()
    if (!force && lastDashboardLoadedAt && now - lastDashboardLoadedAt < DASHBOARD_ACTIVATE_REUSE_MS) {
      return
    }

    dashboardLoadingPromise = (async () => {
      await loadOverview()
      const tasks = [
        loadTrend(),
        loadDeviceTypeDistribution(),
        loadConnectionHealth(),
        loadRecentLogs()
      ]

      const results = await Promise.allSettled(tasks)
      results.forEach((result) => {
        if (result.status === 'rejected') {
          console.error('工作台模块加载失败:', result.reason)
        }
      })
      lastDashboardLoadedAt = Date.now()
    })()

    try {
      await dashboardLoadingPromise
    } finally {
      dashboardLoadingPromise = null
    }
  }

  const startRefreshTimer = () => {
    if (refreshTimer) return
    refreshTimer = setInterval(() => {
      void loadDashboard(true)
    }, DASHBOARD_REFRESH_INTERVAL_MS)
  }

  const stopRefreshTimer = () => {
    if (!refreshTimer) return
    clearInterval(refreshTimer)
    refreshTimer = null
  }

  const resizeCharts = () => {
    trendChart?.resize()
  }

  const goToDevice = () => router.push('/device-monitor/device')

  // 重置数据状态，用于触发动画效果
  const resetDataForAnimation = () => {
    statsData.value = { totalDevices: 0, onlineDevices: 0, offlineDevices: 0, riskDevices: 0, totalPoints: 0, systemAlertCount: 0 }
    systemAlertCompare.value = { countDiff: 0, ratioDiff: 0 }
    trendSummary.value = { collectCountTotal: 0, alarmCountTotal: 0, onlineRateAvg: 0 }
    connectionHealthOverview.value = { onlineDevices: 0, offlineDevices: 0, riskDevices: 0 }
    overviewLoading.value = true
    trendLoading.value = true
    deviceTypeLoading.value = true
    healthLoading.value = true
    recentLoading.value = true
  }

  onActivated(() => {
    resetDataForAnimation()
    lastDashboardLoadedAt = 0 // 重置时间戳，确保会加载数据
    void loadDashboard()
    startRefreshTimer()
    window.addEventListener('resize', resizeCharts)
  })

  onDeactivated(() => {
    stopRefreshTimer()
    window.removeEventListener('resize', resizeCharts)
  })

  onUnmounted(() => {
    stopRefreshTimer()
    window.removeEventListener('resize', resizeCharts)
    trendChart?.dispose()
    trendChart = null
  })
</script>

<style scoped lang="scss">
  .dashboard-page {
    display: flex;
    flex-direction: column;
    gap: 16px;
    padding-bottom: 24px;
  }

  .stats-grid,
  .main-grid,
  .bottom-grid {
    display: grid;
    gap: 16px;
  }

  .stats-grid {
    grid-template-columns: repeat(4, minmax(0, 1fr));

    &.is-loading {
      opacity: 0.78;
    }
  }

  .main-grid {
    grid-template-columns: minmax(0, 1.55fr) minmax(360px, 1.05fr);
  }

  .bottom-grid {
    grid-template-columns: minmax(320px, 0.9fr) minmax(0, 1.45fr);
  }



  .chart-surface {
    width: 100%;
    flex: 1;
  }

  .chart-frame {
    position: relative;
    display: flex;
    flex-direction: column;
    min-height: 0;
  }

  .chart-loading {
    position: absolute;
    inset: 0;
    z-index: 2;
    border: 0;
    border-radius: 18px;
    background: rgba(255, 255, 255, 0.88);
    backdrop-filter: blur(4px);
  }

  .chart-surface--lg {
    flex: 1;
    min-height: 0;
    width: 100%;
  }

  .health-card__body {
    display: flex;
    flex-direction: column;
  }

  .distribution-body {
    display: flex;
    flex-direction: column;
  }

  .dot {
    display: inline-flex;
    flex-shrink: 0;
    width: 10px;
    height: 10px;
    border-radius: 50%;
  }

  .table-like {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-height: 0;
  }

  .table-like__head,
  .table-like__row {
    display: grid;
    gap: 12px;
    align-items: center;
  }

  .recent-head,
  .recent-row {
    grid-template-columns: 90px 90px 90px 110px minmax(0, 1fr);
  }

  .table-like__head {
    padding: 0 0 10px 25px;
    border-bottom: 1px solid #edf0f5;
    font-size: 12px;
    font-weight: 600;
    color: #8b95a7;
    letter-spacing: 0.04em;

    span:first-child {
      margin-left: -10px;
    }
  }

  .table-like__body {
    display: flex;
    flex: 1;
    flex-direction: column;
    gap: 6px;
    padding-top: 10px;
  }

  .table-like__row {
    min-height: 46px;
    padding: 12px 0;
    border-bottom: 1px solid #f1f3f7;
    font-size: 14px;
    color: #445166;

    strong {
      font-weight: 600;
      color: #1d2433;
    }
  }

  .ellipsis {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  @media (max-width: 1440px) {
    .stats-grid {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .bottom-grid {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .health-legend {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }
  }

  @media (max-width: 1080px) {
    .main-grid,
    .bottom-grid,
    .stats-grid {
      grid-template-columns: 1fr;
    }

    .card-head--split {
      flex-direction: column;
    }

    .health-legend {
      grid-template-columns: repeat(2, minmax(0, 1fr));
    }

    .recent-head,
    .recent-row {
      grid-template-columns: 1fr;
    }
  }
</style>
