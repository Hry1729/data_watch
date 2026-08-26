<!-- 监控大屏：高密度 HUD，深色静默刷新 -->
<template>
  <div class="screen">
    <div class="screen__frame" aria-hidden="true">
      <i class="frame-line frame-line--t" />
      <i class="frame-line frame-line--b" />
      <i class="frame-line frame-line--l" />
      <i class="frame-line frame-line--r" />
      <i class="frame-corner frame-corner--tl" />
      <i class="frame-corner frame-corner--tr" />
      <i class="frame-corner frame-corner--bl" />
      <i class="frame-corner frame-corner--br" />
      <i class="frame-tick frame-tick--t" />
      <i class="frame-tick frame-tick--b" />
    </div>
    <div class="screen__bg" />
    <div class="screen__scan" />

    <header class="screen__header">
      <button class="exit-btn" type="button" @click="exitScreen">
        <ArtSvgIcon icon="ri:logout-box-r-line" />
        退出
      </button>
      <div class="screen__title">
        <i class="title-wing title-wing--l" />
        <div class="title-plate">
          <i class="title-plate__accent title-plate__accent--t" />
          <i class="title-plate__accent title-plate__accent--b" />
          <span class="title-plate__eyebrow">INDUSTRIAL DATA OPERATIONS</span>
          <h1>设备数据采集与监控大屏</h1>
        </div>
        <i class="title-wing title-wing--r" />
      </div>
      <div class="screen__meta">
        <span class="live-state"><i />LIVE <b>30S</b></span>
        <time class="screen__time">{{ clockText }}</time>
      </div>
    </header>

    <section class="kpi-row">
      <article
        v-for="item in kpiCards"
        :key="item.key"
        class="kpi-card"
        :class="'kpi-card--' + item.tone"
        @click="go(item.path)"
      >
        <div class="kpi-card__icon">
          <ArtSvgIcon :icon="item.icon" />
        </div>
        <div class="kpi-card__body">
          <span>{{ item.label }}</span>
          <strong>
            {{ item.display }}
            <small v-if="item.suffix">{{ item.suffix }}</small>
          </strong>
        </div>
        <div v-if="item.bar != null" class="kpi-card__bar">
          <i :style="{ width: item.bar + '%' }" />
        </div>
      </article>
    </section>

    <section class="main-grid">
      <div class="col">
        <article class="panel panel--split" @click="go('/device-monitor/monitor')">
          <h3>设备通讯</h3>
          <div class="split-body">
            <div class="chart-wrap">
              <div ref="commChartRef" class="split-body__chart" />
              <div class="chart-center">{{ deviceTotal }}</div>
            </div>
            <ul class="stat-list">
              <li v-for="item in commStats" :key="item.name">
                <em :style="{ background: item.color }" />
                <span>{{ item.name }}</span>
                <strong>{{ item.value }}</strong>
                <b><i :style="{ width: item.ratio + '%', background: item.color }" /></b>
              </li>
            </ul>
          </div>
        </article>

        <article class="panel" @click="go('/device-monitor/device')">
          <h3>设备类型分布</h3>
          <div ref="typeChartRef" class="panel__chart" />
        </article>

        <article class="panel panel--split" @click="go('/device-monitor/log')">
          <h3>日志类型占比</h3>
          <div class="split-body">
            <div class="chart-wrap">
              <div ref="logTypeChartRef" class="split-body__chart" />
              <div class="chart-center">{{ logTypeTotal }}</div>
            </div>
            <ul class="stat-list">
              <li v-for="item in logTypeStats" :key="item.name">
                <em :style="{ background: item.color }" />
                <span>{{ item.name }}</span>
                <strong>{{ item.value }}</strong>
                <b><i :style="{ width: item.ratio + '%', background: item.color }" /></b>
              </li>
            </ul>
          </div>
        </article>
      </div>

      <div class="col">
        <div class="hero-row">
          <article class="panel panel--gauge" @click="go('/device-monitor/log')">
            <h3>日志剩余容量</h3>
            <div class="chart-wrap">
              <div ref="logCapChartRef" class="panel__chart" />
              <div class="chart-center">{{ logCapRate }}%</div>
            </div>
            <p>{{ logCapText }}</p>
          </article>
          <article class="panel panel--gauge panel--gauge-main" @click="go('/device-monitor/monitor')">
            <h3>设备在线率</h3>
            <div class="chart-wrap">
              <div ref="onlineChartRef" class="panel__chart" />
              <div class="chart-center">{{ onlineRate }}%</div>
            </div>
            <p>{{ overview?.onlineDeviceTotal ?? 0 }} / {{ overview?.deviceTotal ?? 0 }} 台</p>
          </article>
          <article class="panel panel--gauge" @click="go('/device-monitor/driver')">
            <h3>驱动可用率</h3>
            <div class="chart-wrap">
              <div ref="driverRateChartRef" class="panel__chart" />
              <div class="chart-center">{{ driverRate }}%</div>
            </div>
            <p>{{ driverAvailable }} / {{ driverTotal }}</p>
          </article>
        </div>

        <article class="panel" @click="go('/device-monitor/history')">
          <div class="panel__head">
            <h3>采集 / 告警趋势</h3>
            <div class="mini-metrics">
              <span>采集 <b>{{ collectTotal }}</b></span>
              <span>告警 <b class="warn">{{ alarmTrendTotal }}</b></span>
            </div>
          </div>
          <div ref="trendChartRef" class="panel__chart" />
        </article>

        <article class="panel" @click="go('/device-monitor/log')">
          <h3>最近告警</h3>
          <div class="alarm-list">
            <div v-if="recentEvents.length" class="alarm-list__body">
              <div
                v-for="row in recentEvents"
                :key="row.eventId"
                class="alarm-list__row"
                :class="'alarm-list__row--' + levelTone(row.eventLevel)"
              >
                <span class="alarm-list__time">{{ formatTime(row.eventTime) }}</span>
                <em :class="'lv lv--' + levelTone(row.eventLevel)">{{ row.eventLevel || '-' }}</em>
                <span class="alarm-list__dev">{{ row.deviceName || row.deviceCode || '-' }}</span>
                <span class="ellipsis">{{ row.content || '-' }}</span>
              </div>
            </div>
            <div v-else class="empty-hint">暂无告警</div>
          </div>
        </article>
      </div>

      <div class="col">
        <article class="panel panel--split panel--alarm" @click="go('/device-monitor/log')">
          <h3>告警类型分布</h3>
          <div class="split-body">
            <div class="chart-wrap">
              <div ref="alarmChartRef" class="split-body__chart" />
              <div class="chart-center">{{ alarmTotal }}</div>
            </div>
            <ul class="stat-list">
              <li v-for="item in alarmStats" :key="item.name">
                <em :style="{ background: item.color }" />
                <span>{{ item.name }}</span>
                <strong>{{ item.value }}</strong>
                <b>
                  <i :style="{ width: item.ratio + '%', background: item.color }" />
                </b>
              </li>
            </ul>
          </div>
        </article>

        <article class="panel" @click="go('/device-monitor/log')">
          <h3>日志级别趋势</h3>
          <div ref="logLevelChartRef" class="panel__chart" />
        </article>

        <article class="panel" @click="go('/device-monitor/driver')">
          <div class="panel__head">
            <h3>驱动绑定 TOP</h3>
            <span class="sub">可用率 {{ driverRate }}%</span>
          </div>
          <div ref="driverBarRef" class="panel__chart" />
        </article>

      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
  import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
  import { useRouter } from 'vue-router'
  import { echarts, graphic, type EChartsOption } from '@/plugins/echarts'
  import {
    fetchDashboardAlarmDistribution,
    fetchDashboardDeviceConnectionHealth,
    fetchDashboardDeviceTypeDistribution,
    fetchDashboardOverview,
    fetchDashboardRecentEvents,
    fetchDashboardTrend,
    fetchDriverList,
    fetchLogStats
  } from '@/api/device-monitor'
  import { fetchGetUserList } from '@/api/system-manage'
  import { getFlatCategories } from '@/views/project-docs/docs.config'

  defineOptions({ name: 'MonitorScreen' })

  type ChartInst = echarts.ECharts

  const HUD = {
    text: '#EAF7FF',
    muted: '#86A7C8',
    accent: '#43D9FF',
    line: 'rgba(67, 217, 255, 0.3)',
    axis: 'rgba(117, 204, 255, 0.34)',
    grid: 'rgba(67, 217, 255, 0.1)',
    track: 'rgba(67, 217, 255, 0.08)',
    tooltipBg: 'rgba(1, 12, 28, 0.9)',
    colors: ['#43D9FF', '#447BFF', '#45F0C4', '#FFBD4A', '#FF5E7E', '#9B7BFF', '#F4E65A']
  }

  const rgba = (hex: string, alpha: number) => {
    const value = hex.replace('#', '')
    if (!/^[0-9a-f]{6}$/i.test(value)) return hex
    const color = Number.parseInt(value, 16)
    return `rgba(${(color >> 16) & 255}, ${(color >> 8) & 255}, ${color & 255}, ${alpha})`
  }

  const energyGradient = (color: string, vertical = false) =>
    new graphic.LinearGradient(0, 0, vertical ? 0 : 1, vertical ? 1 : 0, [
      { offset: 0, color: rgba(color, 0.12) },
      { offset: 0.48, color },
      { offset: 1, color: '#EAFFFF' }
    ])

  const hudValueAxis = () => ({
    axisLabel: { color: HUD.muted, fontSize: 10, margin: 9 },
    axisLine: { show: false },
    axisTick: { show: false },
    splitLine: {
      show: true,
      lineStyle: { color: HUD.grid, type: 'dashed' as const, width: 1 }
    }
  })

  const hudCategoryAxis = () => ({
    axisLabel: { color: HUD.text, fontSize: 10, margin: 10 },
    axisLine: { show: true, lineStyle: { color: HUD.axis, width: 1 } },
    axisTick: { show: false }
  })

  const hudLegend = () => ({
    icon: 'roundRect' as const,
    itemGap: 16,
    itemWidth: 12,
    itemHeight: 3,
    textStyle: { color: HUD.muted, fontSize: 10 }
  })

  const router = useRouter()

  const commChartRef = ref<HTMLDivElement>()
  const typeChartRef = ref<HTMLDivElement>()
  const logTypeChartRef = ref<HTMLDivElement>()
  const logCapChartRef = ref<HTMLDivElement>()
  const onlineChartRef = ref<HTMLDivElement>()
  const driverRateChartRef = ref<HTMLDivElement>()
  const trendChartRef = ref<HTMLDivElement>()
  const alarmChartRef = ref<HTMLDivElement>()
  const logLevelChartRef = ref<HTMLDivElement>()
  const driverBarRef = ref<HTMLDivElement>()

  const charts: ChartInst[] = []
  const chartMap = new Map<HTMLDivElement, ChartInst>()
  let clockTimer: ReturnType<typeof setInterval> | null = null
  let refreshTimer: ReturnType<typeof setInterval> | null = null

  const now = ref(new Date())
  const overview = ref<Api.DeviceMonitor.DashboardOverview | null>(null)
  const health = ref({ onlineDevices: 0, offlineDevices: 0, riskDevices: 0 })
  const alarmTotal = ref(0)
  const alarmGroups = ref<Api.DeviceMonitor.DashboardAlarmDistributionGroup[]>([])
  const typeGroups = ref<Api.DeviceMonitor.DashboardDeviceTypeDistributionGroup[]>([])
  const recentEvents = ref<Api.DeviceMonitor.DashboardRecentEvent[]>([])
  const driverList = ref<Api.DeviceMonitor.DriverListItem[]>([])
  const driverTotal = ref(0)
  const driverAvailable = ref(0)
  const userTotal = ref(0)
  const logStats = ref<Api.DeviceMonitor.LogStats | null>(null)
  const trendAxis = ref<string[]>([])
  const trendSeries = ref<Api.DeviceMonitor.DashboardTrendSeries[]>([])

  const docCategories = getFlatCategories()
  const docTotal = docCategories.reduce((sum, cat) => sum + cat.docs.length, 0)

  const clockText = computed(() => {
    const d = now.value
    const week = ['日', '一', '二', '三', '四', '五', '六'][d.getDay()]
    return `${d.getFullYear()}年${pad(d.getMonth() + 1)}月${pad(d.getDate())}日 星期${week} ${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
  })

  const onlineCount = computed(() => health.value.onlineDevices || overview.value?.onlineDeviceTotal || 0)
  const offlineCount = computed(() => health.value.offlineDevices || overview.value?.offlineDeviceTotal || 0)
  const riskCount = computed(() => health.value.riskDevices || overview.value?.riskDeviceTotal || 0)
  const deviceTotal = computed(() => overview.value?.deviceTotal || onlineCount.value + offlineCount.value + riskCount.value)

  const onlineRate = computed(() => {
    if (!deviceTotal.value) return 0
    return Number(((onlineCount.value / deviceTotal.value) * 100).toFixed(1))
  })

  const driverRate = computed(() => {
    if (!driverTotal.value) return 0
    return Number(((driverAvailable.value / driverTotal.value) * 100).toFixed(1))
  })

  const logCapRate = computed(() => {
    const max = Math.max(logStats.value?.maxTotalMb || 0, 0)
    const used = Math.max(logStats.value?.totalSizeMb || 0, 0)
    if (!max) return 0
    const remaining = Math.max(max - used, 0)
    return Number(((remaining / max) * 100).toFixed(1))
  })

  const logCapText = computed(() => {
    const used = Math.max(logStats.value?.totalSizeMb || 0, 0)
    const max = Math.max(logStats.value?.maxTotalMb || 0, 0)
    const remaining = Math.max(max - used, 0)
    return max ? `剩余 ${Math.round(remaining)} / ${Math.round(max)} MB` : '剩余容量待配置'
  })

  const todayAbnormal = computed(() => {
    const s = logStats.value
    if (!s) return 0
    return (s.todayWarnTotal || 0) + (s.todayErrorTotal || 0) + (s.todayCriticalTotal || 0)
  })

  const collectTotal = computed(() => {
    const series = trendSeries.value.find((item) => item.metricKey === 'collectCount')
    return Math.round((series?.data || []).reduce((sum, num) => sum + (Number(num) || 0), 0))
  })

  const alarmTrendTotal = computed(() => {
    const series = trendSeries.value.find((item) => item.metricKey === 'alarmCount')
    return Math.round((series?.data || []).reduce((sum, num) => sum + (Number(num) || 0), 0))
  })

  const ratioOf = (value: number, total: number) => {
    if (!total) return 0
    return Math.max(0, Math.min(100, Number(((value / total) * 100).toFixed(1))))
  }

  const commStats = computed(() => {
    const total = Math.max(deviceTotal.value, onlineCount.value + offlineCount.value + riskCount.value)
    return [
      { name: '在线', value: onlineCount.value, color: '#45F0C4', ratio: ratioOf(onlineCount.value, total) },
      { name: '离线', value: offlineCount.value, color: '#3768C8', ratio: ratioOf(offlineCount.value, total) },
      { name: '风险', value: riskCount.value, color: '#FF5C7C', ratio: ratioOf(riskCount.value, total) }
    ]
  })

  const alarmStats = computed(() => {
    const list = alarmGroups.value.slice(0, 4)
    const total = alarmTotal.value || list.reduce((sum, item) => sum + (item.count || 0), 0) || 1
    return list.map((item, index) => ({
      name: item.groupName || '未知',
      value: item.count,
      color: HUD.colors[index % HUD.colors.length],
      ratio: ratioOf(item.count, total)
    }))
  })

  const logTypeStats = computed(() => {
    const list = (logStats.value?.typeRatio || []).slice(0, 4)
    const total = list.reduce((sum, item) => sum + (item.count || 0), 0) || 1
    return list.map((item, index) => ({
      name: item.type || '未知',
      value: item.count,
      color: HUD.colors[index % HUD.colors.length],
      ratio: ratioOf(item.count, total)
    }))
  })

  const logTypeTotal = computed(() =>
    logTypeStats.value.reduce((sum, item) => sum + (Number(item.value) || 0), 0)
  )

  const kpiCards = computed(() => [
    { key: 'device', label: '设备总数', display: String(overview.value?.deviceTotal ?? 0), suffix: '', icon: 'ri:hard-drive-3-line', path: '/device-monitor/device', tone: 'blue', bar: 100 },
    { key: 'online', label: '在线设备', display: String(onlineCount.value), suffix: '', icon: 'ri:wifi-line', path: '/device-monitor/monitor', tone: 'ok', bar: onlineRate.value },
    { key: 'offline', label: '离线设备', display: String(offlineCount.value), suffix: '', icon: 'ri:wifi-off-line', path: '/device-monitor/monitor', tone: 'info', bar: ratioOf(offlineCount.value, deviceTotal.value) },
    { key: 'risk', label: '风险设备', display: String(riskCount.value), suffix: '', icon: 'ri:error-warning-line', path: '/device-monitor/monitor', tone: 'danger', bar: ratioOf(riskCount.value, deviceTotal.value) },
    { key: 'rate', label: '在线率', display: String(onlineRate.value), suffix: '%', icon: 'ri:signal-wifi-3-line', path: '/device-monitor/monitor', tone: 'blue', bar: onlineRate.value },
    { key: 'point', label: '点位总数', display: String(overview.value?.pointTotal ?? 0), suffix: '', icon: 'ri:flashlight-line', path: '/device-monitor/point', tone: 'blue', bar: 100 },
    { key: 'alarm', label: '系统告警', display: String(overview.value?.systemAlertTotal ?? alarmTotal.value), suffix: '', icon: 'ri:alarm-warning-line', path: '/device-monitor/log', tone: 'warn', bar: 100 },
    { key: 'log', label: '今日日志', display: String(logStats.value?.todayTotal ?? 0), suffix: '', icon: 'ri:file-list-3-line', path: '/device-monitor/log', tone: 'info', bar: 100 },
    { key: 'abn', label: '今日异常', display: String(todayAbnormal.value), suffix: '', icon: 'ri:alert-line', path: '/device-monitor/log', tone: 'danger', bar: 100 },
    { key: 'driver', label: '驱动可用', display: `${driverAvailable.value}/${driverTotal.value}`, suffix: '', icon: 'ri:plug-line', path: '/device-monitor/driver', tone: 'ok', bar: driverRate.value },
    { key: 'user', label: '系统用户', display: String(userTotal.value), suffix: '', icon: 'ri:team-line', path: '/system/user', tone: 'info', bar: 100 },
    { key: 'docs', label: '项目文档', display: String(docTotal), suffix: '', icon: 'ri:book-2-line', path: '/project-docs/overview', tone: 'blue', bar: 100 }
  ])

  const go = (path: string) => router.push(path)

  const exitScreen = () => {
    if (window.history.length > 1) {
      router.back()
      return
    }
    router.push('/device-monitor/dashboard')
  }

  const pad = (n: number) => String(n).padStart(2, '0')

  const formatTime = (value?: string) => {
    if (!value) return '-'
    const text = value.replace('T', ' ')
    return text.slice(11, 19) || text
  }

  const levelTone = (level?: string) => {
    const text = String(level || '').toUpperCase()
    if (text.includes('CRIT') || text.includes('ERROR') || text.includes('严重')) return 'danger'
    if (text.includes('WARN') || text.includes('告警')) return 'warn'
    return 'ok'
  }

  const shortTime = (value: string) => {
    const text = String(value).replace('T', ' ')
    return text.length >= 16 ? text.slice(11, 16) : text
  }

  const tooltip = (trigger: 'item' | 'axis' = 'item'): EChartsOption['tooltip'] => ({
    trigger,
    backgroundColor: HUD.tooltipBg,
    borderColor: HUD.accent,
    borderWidth: 1,
    padding: [8, 10],
    textStyle: { color: HUD.text, fontSize: 11 },
    axisPointer:
      trigger === 'axis'
        ? {
            type: 'line',
            lineStyle: { color: HUD.accent, width: 1, type: 'dashed' }
          }
        : undefined,
    extraCssText:
      'backdrop-filter: blur(10px); border-radius: 2px; box-shadow: 0 0 24px rgba(67,217,255,0.28); letter-spacing: .04em'
  })

  const ensureChart = (el?: HTMLDivElement) => {
    if (!el) return null
    let inst = chartMap.get(el)
    if (!inst) {
      inst = echarts.init(el, undefined, { renderer: 'canvas' })
      inst.getZr().on('mousemove', () => {
        inst?.getZr().setCursorStyle('pointer')
      })
      chartMap.set(el, inst)
      charts.push(inst)
    }
    return inst
  }

  const draw = (el: HTMLDivElement | undefined, option: EChartsOption) => {
    const inst = ensureChart(el)
    if (!inst) return
    inst.setOption(
      {
        backgroundColor: 'transparent',
        color: HUD.colors,
        textStyle: {
          color: HUD.text,
          fontFamily: "Bahnschrift, 'Microsoft YaHei UI', sans-serif"
        },
        animationDuration: 720,
        animationEasing: 'cubicOut',
        animationDurationUpdate: 480,
        animationEasingUpdate: 'cubicOut',
        ...option
      },
      { notMerge: false, lazyUpdate: true }
    )
  }

  const ringOption = (
    data: Array<{ name: string; value: number; color?: string }>
  ): EChartsOption => ({
    tooltip: {
      ...tooltip('item'),
      formatter: (params: any) =>
        `${params.name}<br/>数量：${params.value}<br/>占比：${params.percent ?? 0}%`
    },
    series: [
      {
        type: 'pie',
        radius: ['54%', '76%'],
        center: ['50%', '50%'],
        silent: true,
        animation: false,
        label: { show: false },
        data: [{ value: 1, itemStyle: { color: HUD.track } }]
      },
      {
        type: 'pie',
        radius: ['82%', '84%'],
        center: ['50%', '50%'],
        silent: true,
        animation: false,
        label: { show: false },
        data: Array.from({ length: 30 }, (_, index) => ({
          value: 1,
          itemStyle: { color: index % 3 === 0 ? HUD.line : 'transparent' }
        }))
      },
      {
        type: 'pie',
        radius: ['54%', '76%'],
        center: ['50%', '50%'],
        startAngle: 90,
        minAngle: 3,
        padAngle: 2,
        avoidLabelOverlap: true,
        label: { show: false },
        labelLine: { show: false },
        cursor: 'pointer',
        emphasis: {
          scale: true,
          scaleSize: 5,
          itemStyle: { shadowBlur: 22, shadowColor: rgba(HUD.accent, 0.55) }
        },
        data: data.map((item, index) => {
          const color = item.color || HUD.colors[index % HUD.colors.length]
          return {
            name: item.name,
            value: item.value,
            itemStyle: {
              color: energyGradient(color, true),
              borderColor: rgba(color, 0.22),
              borderWidth: 1,
              borderRadius: 6,
              shadowBlur: 10,
              shadowColor: rgba(color, 0.42)
            }
          }
        })
      }
    ]
  })

  const gaugeRing = (percent: number, color: string): EChartsOption => {
    const safe = Math.max(0, Math.min(100, Number(percent) || 0))
    return {
      tooltip: { show: false },
      series: [
        {
          type: 'gauge',
          radius: '88%',
          center: ['50%', '50%'],
          startAngle: 220,
          endAngle: -40,
          min: 0,
          max: 100,
          splitNumber: 10,
          silent: true,
          progress: {
            show: true,
            width: 13,
            roundCap: true,
            itemStyle: {
              color: energyGradient(color),
              borderColor: rgba(color, 0.94),
              borderWidth: 1,
              shadowBlur: 22,
              shadowColor: rgba(color, 0.92)
            }
          },
          axisLine: {
            lineStyle: {
              width: 13,
              color: [[1, rgba(color, 0.12)]],
              shadowBlur: 10,
              shadowColor: rgba(color, 0.22)
            }
          },
          axisTick: {
            show: true,
            splitNumber: 2,
            distance: -18,
            length: 5,
            lineStyle: { color: rgba(color, 0.58), width: 1 }
          },
          splitLine: {
            show: true,
            distance: -21,
            length: 10,
            lineStyle: { color: rgba(color, 0.92), width: 1.5 }
          },
          axisLabel: { show: false },
          pointer: { show: false },
          anchor: { show: false },
          title: { show: false },
          detail: { show: false },
          data: [{ value: safe }]
        },
        {
          type: 'gauge',
          radius: '98%',
          center: ['50%', '50%'],
          startAngle: 220,
          endAngle: -40,
          min: 0,
          max: 100,
          splitNumber: 5,
          silent: true,
          axisLine: {
            lineStyle: {
              width: 2,
              color: [[1, rgba(color, 0.24)]],
              shadowBlur: 8,
              shadowColor: rgba(color, 0.42)
            }
          },
          axisTick: { show: false },
          splitLine: {
            show: true,
            distance: -4,
            length: 7,
            lineStyle: { color: rgba(color, 0.82), width: 1.5 }
          },
          axisLabel: { show: false },
          pointer: { show: false },
          anchor: { show: false },
          title: { show: false },
          detail: { show: false },
          data: [{ value: safe }]
        }
      ]
    }
  }

  const renderCharts = () => {
    draw(
      commChartRef.value,
      ringOption(
        [
          { name: '在线', value: onlineCount.value, color: '#45F0C4' },
          { name: '离线', value: offlineCount.value, color: '#3768C8' },
          { name: '风险', value: riskCount.value, color: '#FF5C7C' }
        ]
      )
    )

    draw(typeChartRef.value, {
      tooltip: tooltip('axis'),
      legend: { ...hudLegend(), top: 0, left: 'center' },
      grid: { top: 28, right: 12, bottom: 8, left: 72 },
      xAxis: { ...hudValueAxis(), type: 'value' },
      yAxis: {
        ...hudCategoryAxis(),
        type: 'category',
        data: typeGroups.value.map((item) => item.typeName)
      },
      series: [
        {
          name: '在线',
          type: 'bar',
          stack: 'total',
          barWidth: 9,
          cursor: 'pointer',
          data: typeGroups.value.map((item) => item.onlineCount),
          itemStyle: {
            color: energyGradient('#45F0C4'),
            borderRadius: [0, 5, 5, 0],
            shadowBlur: 9,
            shadowColor: rgba('#45F0C4', 0.48)
          },
          emphasis: {
            itemStyle: { shadowBlur: 18, shadowColor: rgba('#45F0C4', 0.8) }
          }
        },
        {
          name: '离线',
          type: 'bar',
          stack: 'total',
          barWidth: 9,
          cursor: 'pointer',
          data: typeGroups.value.map((item) => Math.max(item.deviceCount - item.onlineCount, 0)),
          itemStyle: {
            color: energyGradient('#447BFF'),
            borderRadius: [0, 5, 5, 0],
            shadowBlur: 8,
            shadowColor: rgba('#447BFF', 0.45)
          },
          emphasis: {
            itemStyle: { shadowBlur: 18, shadowColor: rgba('#447BFF', 0.8) }
          }
        }
      ]
    })

    const typeRatio = logStats.value?.typeRatio || []
    draw(
      logTypeChartRef.value,
      ringOption(
        typeRatio.map((item, index) => ({
          name: item.type || '未知',
          value: item.count,
          color: HUD.colors[index % HUD.colors.length]
        }))
      )
    )

    draw(logCapChartRef.value, gaugeRing(logCapRate.value, '#9B7BFF'))
    draw(onlineChartRef.value, gaugeRing(onlineRate.value, '#45F0C4'))
    draw(driverRateChartRef.value, gaugeRing(driverRate.value, '#43D9FF'))

    draw(trendChartRef.value, {
      tooltip: tooltip('axis'),
      legend: { ...hudLegend(), top: 0 },
      grid: { top: 30, right: 18, bottom: 22, left: 40 },
      xAxis: {
        ...hudCategoryAxis(),
        type: 'category',
        data: trendAxis.value,
        boundaryGap: false,
        axisLabel: { color: HUD.muted, fontSize: 10 }
      },
      yAxis: { ...hudValueAxis(), type: 'value' },
      series: trendSeries.value.map((item, index) => {
        const color = HUD.colors[index % HUD.colors.length]
        return {
          name: item.metricName,
          type: 'line',
          smooth: 0.45,
          showSymbol: false,
          symbol: 'circle',
          symbolSize: 7,
          cursor: 'pointer',
          data: item.data,
          lineStyle: {
            width: index === 0 ? 3 : 2,
            color,
            shadowBlur: 12,
            shadowColor: rgba(color, 0.72)
          },
          itemStyle: { color: '#eaffff', borderColor: color, borderWidth: 2 },
          emphasis: {
            focus: 'series',
            lineStyle: { width: 4, shadowBlur: 20, shadowColor: color },
            itemStyle: { shadowBlur: 16, shadowColor: color }
          },
          areaStyle: {
            color: new graphic.LinearGradient(0, 0, 0, 1, [
              { offset: 0, color: rgba(color, index === 0 ? 0.34 : 0.2) },
              { offset: 0.62, color: rgba(color, 0.08) },
              { offset: 1, color: rgba(color, 0) }
            ])
          }
        }
      })
    })

    draw(alarmChartRef.value, {
      tooltip: {
        ...tooltip('item'),
        formatter: (params: any) =>
          `${params.name}<br/>数量：${params.value}<br/>占比：${params.percent ?? 0}%<br/>总数：${alarmTotal.value}`
      },
      series: [
        {
          type: 'pie',
          radius: ['71%', '73%'],
          center: ['50%', '50%'],
          silent: true,
          animation: false,
          label: { show: false },
          data: [{ value: 1, itemStyle: { color: HUD.line } }]
        },
        {
          type: 'pie',
          radius: ['48%', '68%'],
          center: ['50%', '50%'],
          padAngle: 2,
          cursor: 'pointer',
          label: {
            show: false,
            color: HUD.text,
            fontSize: 10,
            textShadowBlur: 8,
            textShadowColor: HUD.accent,
            formatter: '{b}\n{d}%'
          },
          labelLine: {
            show: false,
            length: 7,
            length2: 4,
            lineStyle: { color: HUD.axis }
          },
          emphasis: {
            scale: true,
            scaleSize: 7,
            itemStyle: { shadowBlur: 24, shadowColor: rgba(HUD.accent, 0.62) }
          },
          data: alarmStats.value.map((item) => {
            const color = item.color
            return {
              name: item.name,
              value: item.value,
              itemStyle: {
                color: energyGradient(color, true),
                borderColor: rgba(color, 0.28),
                borderWidth: 1,
                borderRadius: 5,
                shadowBlur: 10,
                shadowColor: rgba(color, 0.45)
              }
            }
          })
        }
      ]
    })

    const levelTrend = (logStats.value?.levelTrend || []).slice(-12)
    const levelSeries = [
      { name: 'INFO', color: '#43D9FF', values: levelTrend.map((item) => item.info) },
      { name: 'WARN', color: '#FFBD4A', values: levelTrend.map((item) => item.warn) },
      { name: 'ERROR', color: '#FF5E7E', values: levelTrend.map((item) => item.error) },
      { name: 'CRIT', color: '#F4E65A', values: levelTrend.map((item) => item.critical) }
    ]
    draw(logLevelChartRef.value, {
      tooltip: tooltip('axis'),
      legend: { ...hudLegend(), top: 0 },
      grid: { top: 30, right: 10, bottom: 22, left: 36 },
      xAxis: {
        ...hudCategoryAxis(),
        type: 'category',
        data: levelTrend.map((item) => shortTime(item.time)),
        axisLabel: { color: HUD.muted, fontSize: 10 }
      },
      yAxis: { ...hudValueAxis(), type: 'value' },
      series: levelSeries.map((item, index) => ({
        name: item.name,
        type: 'bar',
        stack: 'lv',
        barWidth: 9,
        cursor: 'pointer',
        data: item.values,
        itemStyle: {
          color: energyGradient(item.color, true),
          borderRadius: index === levelSeries.length - 1 ? [4, 4, 0, 0] : 0,
          shadowBlur: 8,
          shadowColor: rgba(item.color, 0.45)
        },
        emphasis: {
          focus: 'series',
          itemStyle: { shadowBlur: 16, shadowColor: item.color }
        }
      }))
    })

    const topDrivers = [...driverList.value]
      .sort((a, b) => (b.boundDeviceCount || 0) - (a.boundDeviceCount || 0))
      .slice(0, 6)
    draw(driverBarRef.value, {
      tooltip: tooltip('axis'),
      grid: { top: 8, right: 16, bottom: 8, left: 86 },
      xAxis: { ...hudValueAxis(), type: 'value' },
      yAxis: {
        ...hudCategoryAxis(),
        type: 'category',
        data: topDrivers.map((item) => item.driverName)
      },
      series: [
        {
          type: 'bar',
          barWidth: 9,
          barGap: '-100%',
          silent: true,
          animation: false,
          data: topDrivers.map(() => Math.max(...topDrivers.map((item) => item.boundDeviceCount || 0), 1)),
          itemStyle: { color: HUD.track, borderRadius: [0, 5, 5, 0] }
        },
        {
          type: 'bar',
          barWidth: 9,
          cursor: 'pointer',
          data: topDrivers.map((item) => item.boundDeviceCount || 0),
          itemStyle: {
            color: energyGradient('#43D9FF'),
            borderRadius: [0, 5, 5, 0],
            shadowBlur: 12,
            shadowColor: rgba('#43D9FF', 0.58)
          },
          emphasis: {
            itemStyle: { shadowBlur: 20, shadowColor: '#43D9FF' }
          }
        }
      ]
    })
  }

  const resizeCharts = () => {
    charts.forEach((item) => item.resize())
  }

  const loadAll = async () => {
    const [
      overviewRes,
      healthRes,
      trendRes,
      alarmRes,
      typeRes,
      eventRes,
      driverRes,
      userRes,
      logRes
    ] = await Promise.allSettled([
      fetchDashboardOverview(),
      fetchDashboardDeviceConnectionHealth({ statRange: 'last24h', topN: 8, includeOffline: true }),
      fetchDashboardTrend({ rangeType: 'last24h', metricKeys: ['collectCount', 'alarmCount'] }),
      fetchDashboardAlarmDistribution({ statRange: 'last24h', groupBy: 'alarmType', topN: 6 }),
      fetchDashboardDeviceTypeDistribution(6),
      fetchDashboardRecentEvents({ limit: 30, eventType: 'alarm' }),
      fetchDriverList({ pageNo: 1, pageSize: 20 }),
      fetchGetUserList({ current: 1, size: 1 }),
      fetchLogStats()
    ])

    if (overviewRes.status === 'fulfilled') overview.value = overviewRes.value
    if (healthRes.status === 'fulfilled') health.value = healthRes.value.overview
    if (trendRes.status === 'fulfilled') {
      trendAxis.value = (trendRes.value.timeAxis || []).map(shortTime)
      trendSeries.value = trendRes.value.series || []
    }
    if (alarmRes.status === 'fulfilled') {
      alarmTotal.value = alarmRes.value.totalAlarmCount || 0
      alarmGroups.value = alarmRes.value.groups || []
    }
    if (typeRes.status === 'fulfilled') typeGroups.value = typeRes.value.groups || []
    if (eventRes.status === 'fulfilled') recentEvents.value = eventRes.value.list || []
    if (driverRes.status === 'fulfilled') {
      driverList.value = driverRes.value.list || []
      driverTotal.value = driverRes.value.total || driverList.value.length
      driverAvailable.value =
        driverRes.value.availableCount ??
        driverList.value.filter((item) => item.status !== 'disabled').length
    }
    if (userRes.status === 'fulfilled') userTotal.value = userRes.value.total || 0
    if (logRes.status === 'fulfilled') logStats.value = logRes.value

    await nextTick()
    renderCharts()
    resizeCharts()
  }

  onMounted(async () => {
    clockTimer = setInterval(() => {
      now.value = new Date()
    }, 1000)
    await loadAll()
    refreshTimer = setInterval(() => {
      loadAll()
    }, 30000)
    window.addEventListener('resize', resizeCharts)
  })

  onUnmounted(() => {
    if (clockTimer) clearInterval(clockTimer)
    if (refreshTimer) clearInterval(refreshTimer)
    window.removeEventListener('resize', resizeCharts)
    charts.forEach((item) => item.dispose())
    charts.length = 0
    chartMap.clear()
  })
</script>

<style scoped lang="scss">
  .screen {
    position: relative;
    box-sizing: border-box;
    display: grid;
    grid-template-rows: auto auto minmax(0, 1fr);
    gap: 8px;
    height: 100vh !important;
    min-height: 0 !important;
    max-height: 100vh;
    padding: 14px 16px 16px;
    overflow: hidden;
    font-family: 'DIN Alternate', Bahnschrift, 'Microsoft YaHei UI', 'PingFang SC', sans-serif;
    font-variant-numeric: tabular-nums lining-nums;
    color: #e8eef8;
    background: #000b24;
    border: 1px solid rgb(67 217 255 / 42%);
    box-shadow: inset 0 0 0 1px rgb(34 105 160 / 42%);
  }

  .screen__bg,
  .screen__scan {
    position: absolute;
    inset: 0;
    z-index: 0;
    pointer-events: none;
  }

  .screen__bg {
    background:
      linear-gradient(rgb(77 224 255 / 3.5%) 1px, transparent 1px),
      linear-gradient(90deg, rgb(77 224 255 / 3.5%) 1px, transparent 1px),
      radial-gradient(circle at 50% -10%, rgb(0 185 255 / 28%), transparent 42%),
      radial-gradient(circle at 7% 28%, rgb(18 91 255 / 18%), transparent 28%),
      radial-gradient(circle at 92% 72%, rgb(0 221 255 / 10%), transparent 24%),
      linear-gradient(180deg, rgb(2 19 40 / 52%) 0%, rgb(1 8 22 / 72%) 100%),
      url('@/assets/images/background.jpg');
    background-repeat: repeat, repeat, no-repeat, no-repeat, no-repeat, no-repeat, no-repeat;
    background-position:
      0 0,
      0 0,
      center,
      center,
      center,
      center,
      center;
    background-size:
      42px 42px,
      42px 42px,
      auto,
      auto,
      auto,
      auto,
      cover;
  }

  .screen__bg::before,
  .screen__bg::after {
    position: absolute;
    inset: 0;
    content: '';
  }

  .screen__bg::before {
    background-image: radial-gradient(circle, rgb(102 230 255 / 34%) 0 1px, transparent 1.2px);
    background-size: 18px 18px;
    opacity: 0.16;
    mask-image: radial-gradient(ellipse at center, #000 0%, transparent 72%);
  }

  .screen__bg::after {
    background:
      linear-gradient(
        90deg,
        transparent 0 24.8%,
        rgb(102 230 255 / 8%) 25%,
        transparent 25.2% 74.8%,
        rgb(102 230 255 / 8%) 75%,
        transparent 75.2%
      ),
      radial-gradient(ellipse at center, transparent 32%, rgb(0 3 12 / 72%) 100%);
  }

  .screen__scan {
    background: linear-gradient(
      180deg,
      transparent 0 47%,
      rgb(102 230 255 / 3%) 48%,
      rgb(102 230 255 / 16%) 50%,
      rgb(102 230 255 / 3%) 52%,
      transparent 53% 100%
    );
    animation: scan-move 10s linear infinite;
  }

  .screen__frame {
    position: absolute;
    inset: 6px;
    z-index: 2;
    pointer-events: none;
  }

  .frame-line--t,
  .frame-line--b {
    position: absolute;
    left: 36px;
    right: 36px;
    height: 1px;
    background: linear-gradient(90deg, transparent, rgba(102, 230, 255, 0.92), transparent);
  }

  .frame-line--t {
    top: 0;
    box-shadow: 0 4px 0 -3px rgba(61, 110, 255, 0.4);
  }

  .frame-line--b {
    bottom: 0;
    box-shadow: 0 -4px 0 -3px rgba(61, 110, 255, 0.4);
  }

  .frame-line--l,
  .frame-line--r {
    position: absolute;
    top: 36px;
    bottom: 36px;
    width: 1px;
    background: linear-gradient(180deg, transparent, rgba(102, 230, 255, 0.72), transparent);
  }

  .frame-line--l {
    left: 0;
    box-shadow: 4px 0 0 -3px rgba(61, 110, 255, 0.4);
  }

  .frame-line--r {
    right: 0;
    box-shadow: -4px 0 0 -3px rgba(61, 110, 255, 0.4);
  }

  .frame-corner {
    position: absolute;
    width: 26px;
    height: 26px;
    border-color: #66e6ff;
    border-style: solid;
    filter: drop-shadow(0 0 6px rgba(102, 230, 255, 0.78));
  }

  .frame-corner--tl { top: -1px; left: -1px; border-width: 2px 0 0 2px; }
  .frame-corner--tr { top: -1px; right: -1px; border-width: 2px 2px 0 0; }
  .frame-corner--bl { bottom: -1px; left: -1px; border-width: 0 0 2px 2px; }
  .frame-corner--br { right: -1px; bottom: -1px; border-width: 0 2px 2px 0; }

  .frame-corner::before {
    content: '';
    position: absolute;
    width: 12px;
    height: 12px;
    border-color: rgba(130, 235, 255, 0.82);
    border-style: solid;
  }

  .frame-corner--tl::before { top: 5px; left: 5px; border-width: 1px 0 0 1px; }
  .frame-corner--tr::before { top: 5px; right: 5px; border-width: 1px 1px 0 0; }
  .frame-corner--bl::before { bottom: 5px; left: 5px; border-width: 0 0 1px 1px; }
  .frame-corner--br::before { right: 5px; bottom: 5px; border-width: 0 1px 1px 0; }

  .frame-tick--t,
  .frame-tick--b {
    position: absolute;
    left: 50%;
    width: 92px;
    height: 6px;
    transform: translateX(-50%);
    border: 1px solid rgba(90, 140, 255, 0.75);
  }

  .frame-tick--t {
    top: -4px;
    border-bottom: none;
    clip-path: polygon(10px 0, calc(100% - 10px) 0, 100% 100%, 0 100%);
  }

  .frame-tick--b {
    bottom: -4px;
    border-top: none;
    clip-path: polygon(0 0, 100% 0, calc(100% - 10px) 100%, 10px 100%);
  }

  .screen__header,
  .kpi-row,
  .main-grid {
    position: relative;
    z-index: 1;
    min-height: 0;
  }

  .screen__header {
    position: relative;
    z-index: 3;
    display: flex;
    align-items: center;
    justify-content: space-between;
    min-height: 64px;
  }

  .exit-btn,
  .kpi-card,
  .panel,
  .panel__chart,
  .split-body__chart,
  .hero-row .panel__chart {
    cursor: pointer;
  }

  .kpi-card :deep(canvas),
  .panel :deep(canvas) {
    cursor: pointer !important;
  }

  .exit-btn {
    z-index: 2;
    display: inline-flex;
    gap: 4px;
    align-items: center;
    padding: 5px 10px;
    font-size: 12px;
    color: #9bb4d8;
    background: transparent;
    border: 1px solid rgba(61, 110, 255, 0.28);
    border-radius: 4px;
    transition: 0.2s ease;
  }

  .screen__title {
    position: absolute;
    top: 50%;
    left: 50%;
    z-index: 1;
    display: flex;
    gap: 10px;
    align-items: center;
    width: max-content;
    pointer-events: none;
    transform: translate(-50%, -50%);
  }

  .title-plate {
    position: relative;
    display: flex;
    flex-direction: column;
    flex-shrink: 0;
    gap: 1px;
    align-items: center;
    padding: 5px 46px 7px;
    clip-path: polygon(20px 0, calc(100% - 20px) 0, 100% 50%, calc(100% - 20px) 100%, 20px 100%, 0 50%);
    background: linear-gradient(180deg, rgb(9 58 92 / 96%), rgb(3 18 40 / 98%));
    border: 1px solid rgb(102 230 255 / 78%);
    box-shadow:
      0 0 24px rgb(67 217 255 / 34%),
      inset 0 0 18px rgb(67 217 255 / 12%);
  }

  .title-plate h1 {
    margin: 0;
    font-size: 22px;
    font-weight: 800;
    line-height: 1.15;
    color: #f4f8ff;
    letter-spacing: 3px;
    white-space: nowrap;
    background: linear-gradient(180deg, #fff 0%, #c9f7ff 52%, #66dfff 100%);
    filter: drop-shadow(0 0 10px rgb(102 230 255 / 46%));
    background-clip: text;
    -webkit-text-fill-color: transparent;
  }

  .title-plate__eyebrow {
    font-family: 'Roboto Mono', SFMono-Regular, Consolas, monospace;
    font-size: 8px;
    font-weight: 600;
    line-height: 1;
    color: #66e6ff;
    letter-spacing: 3.2px;
    opacity: 0.76;
  }

  .title-plate__accent {
    position: absolute;
    left: 50%;
    width: 72px;
    height: 7px;
    background: rgb(16 133 184 / 38%);
    border: 1px solid rgb(102 230 255 / 86%);
    transform: translateX(-50%);
  }

  .title-plate__accent--t {
    top: -5px;
    border-bottom: none;
    clip-path: polygon(10px 0, calc(100% - 10px) 0, 100% 100%, 0 100%);
  }

  .title-plate__accent--b {
    bottom: -5px;
    border-top: none;
    clip-path: polygon(0 0, 100% 0, calc(100% - 10px) 100%, 10px 100%);
  }

  .title-wing {
    position: relative;
    flex: 0 0 190px;
    width: 190px;
    height: 14px;
  }

  .title-wing::before {
    position: absolute;
    top: 50%;
    right: 0;
    left: 8px;
    height: 2px;
    content: '';
    background: linear-gradient(90deg, transparent 0%, rgb(67 217 255 / 14%) 18%, #43d9ff 68%, #b4f4ff 100%);
    box-shadow:
      0 0 8px rgb(67 217 255 / 64%),
      0 5px 0 -3px rgb(67 217 255 / 34%);
    transform: translateY(-50%);
  }

  .title-wing::after {
    position: absolute;
    top: 50%;
    right: 0;
    width: 9px;
    height: 9px;
    content: '';
    border: 1px solid #66e6ff;
    box-shadow: 0 0 8px rgb(67 217 255 / 76%);
    transform: translateY(-50%) rotate(45deg);
  }

  .title-wing--l {
    transform: scaleX(-1);
  }

  .screen__meta {
    z-index: 2;
    display: flex;
    gap: 18px;
    align-items: center;
  }

  .live-state {
    display: inline-flex;
    gap: 8px;
    align-items: center;
    font-family: 'Roboto Mono', SFMono-Regular, Consolas, monospace;
    font-size: 13px;
    font-weight: 700;
    color: #66e6ff;
    text-shadow: 0 0 10px rgb(102 230 255 / 52%);
    letter-spacing: 1.6px;
  }

  .live-state i {
    flex-shrink: 0;
    width: 10px;
    height: 10px;
    background: #45f0c4;
    border: 1px solid rgb(225 255 249 / 74%);
    border-radius: 50%;
    box-shadow: 0 0 14px #45f0c4;
    animation: live-pulse 2s ease-in-out infinite;
  }

  .live-state b {
    padding-left: 8px;
    font-size: 11px;
    font-weight: 500;
    color: #a9c5e5;
    border-left: 1px solid rgb(102 230 255 / 24%);
  }

  .screen__time {
    z-index: 2;
    font-size: 17px;
    font-weight: 600;
    color: #e8eef8;
    text-shadow: 0 0 12px rgb(67 217 255 / 36%);
    letter-spacing: 0.6px;
  }

  .kpi-row {
    display: grid;
    grid-template-columns: repeat(12, minmax(0, 1fr));
    gap: 6px;
  }

  .kpi-card,
  .panel {
    --surface-accent: #43d9ff;
    --surface-top-alpha: 52%;
    --surface-bottom-alpha: 42%;
    --surface-glint: color-mix(in srgb, var(--surface-accent) 94%, transparent);
    --border-angle: 0deg;

    position: relative;
    isolation: isolate;
    overflow: hidden;
    background:
      linear-gradient(135deg, color-mix(in srgb, var(--surface-accent) 9%, transparent), transparent 20%),
      linear-gradient(
        180deg,
        rgb(5 29 55 / var(--surface-top-alpha)),
        rgb(2 12 29 / var(--surface-bottom-alpha))
      );
    backdrop-filter: blur(5px) saturate(125%);
    border: 1px solid color-mix(in srgb, var(--surface-accent) 48%, transparent);
    box-shadow:
      inset 0 0 0 1px rgb(1 11 28 / 72%),
      inset 0 0 28px color-mix(in srgb, var(--surface-accent) 8%, transparent),
      0 0 0 1px rgb(67 217 255 / 8%),
      0 0 22px color-mix(in srgb, var(--surface-accent) 10%, transparent);
    transition:
      transform 0.2s ease,
      box-shadow 0.2s ease,
      border-color 0.2s ease;
  }

  .kpi-card::before,
  .panel::before {
    position: absolute;
    z-index: 2;
    pointer-events: none;
    content: '';
    opacity: 0.94;
  }

  .kpi-card::before {
    top: 0;
    left: 11px;
    width: 48%;
    height: 3px;
    background: linear-gradient(
      90deg,
      var(--surface-accent) 0,
      #eaffff 34%,
      var(--surface-accent) 68%,
      transparent 100%
    );
    box-shadow: 0 0 12px color-mix(in srgb, var(--surface-accent) 72%, transparent);
    clip-path: polygon(0 0, 100% 0, calc(100% - 9px) 100%, 7px 100%);
  }

  .panel::before {
    top: 0;
    left: 50%;
    width: clamp(78px, 34%, 148px);
    height: 6px;
    background:
      linear-gradient(90deg, transparent, #eaffff 18% 22%, transparent 23% 77%, #eaffff 78% 82%, transparent),
      linear-gradient(
        90deg,
        transparent,
        color-mix(in srgb, var(--surface-accent) 84%, transparent) 12% 88%,
        transparent
      );
    box-shadow:
      0 0 12px color-mix(in srgb, var(--surface-accent) 72%, transparent),
      inset 0 -1px 0 color-mix(in srgb, var(--surface-accent) 84%, transparent);
    clip-path: polygon(0 0, 100% 0, calc(100% - 9px) 100%, 9px 100%);
    transform: translateX(-50%);
  }

  .kpi-card::after,
  .panel::after {
    position: absolute;
    inset: 0;
    z-index: 3;
    padding: 1px;
    pointer-events: none;
    content: '';
    background: conic-gradient(
      from var(--border-angle),
      transparent 0 12%,
      var(--surface-glint) 18%,
      transparent 24% 56%,
      color-mix(in srgb, var(--surface-accent) 48%, transparent) 64%,
      var(--surface-glint) 70%,
      transparent 76% 100%
    );
    filter: drop-shadow(0 0 7px color-mix(in srgb, var(--surface-accent) 78%, transparent));
    opacity: 0.9;
    clip-path: var(--frame-shape);
    -webkit-mask:
      linear-gradient(#000 0 0) content-box,
      linear-gradient(#000 0 0);
    mask:
      linear-gradient(#000 0 0) content-box,
      linear-gradient(#000 0 0);
    -webkit-mask-composite: xor;
    mask-composite: exclude;
    animation: border-patrol 10s linear infinite;
  }

  .panel::after {
    animation-delay: -4.5s;
  }

  .kpi-card:hover,
  .panel:hover,
  .exit-btn:hover {
    border-color: color-mix(in srgb, var(--surface-accent, #43d9ff) 78%, transparent);
    box-shadow:
      0 10px 28px rgb(0 182 255 / 14%),
      inset 0 0 22px rgb(67 217 255 / 8%);
    transform: translateY(-3px);
  }

  .kpi-card {
    --surface-top-alpha: 40%;
    --surface-bottom-alpha: 30%;
    --frame-shape: polygon(0 9px, 5px 9px, 11px 0, calc(100% - 12px) 0, 100% 12px, 100% calc(100% - 8px), calc(100% - 8px) 100%, 42% 100%, calc(42% - 6px) calc(100% - 4px), 21% calc(100% - 4px), calc(21% - 6px) 100%, 8px 100%, 0 calc(100% - 8px));

    display: flex;
    gap: 8px;
    align-items: center;
    min-width: 0;
    padding: 7px 8px 10px;
    clip-path: var(--frame-shape);
  }

  .kpi-card__icon {
    display: grid;
    flex-shrink: 0;
    width: 28px;
    height: 28px;
    place-items: center;
    font-size: 15px;
    color: var(--surface-accent);
    clip-path: polygon(5px 0, 100% 0, 100% calc(100% - 5px), calc(100% - 5px) 100%, 0 100%, 0 5px);
    background: color-mix(in srgb, var(--surface-accent) 10%, transparent);
    border: 1px solid color-mix(in srgb, var(--surface-accent) 28%, transparent);
  }

  .kpi-card--blue { --surface-accent: #43d9ff; }
  .kpi-card--ok { --surface-accent: #45f0c4; }
  .kpi-card--warn { --surface-accent: #ffbd4a; }
  .kpi-card--danger { --surface-accent: #ff5e7e; }
  .kpi-card--info { --surface-accent: #829cff; }

  .panel--alarm {
    --surface-accent: #ff5e7e;
  }

  .kpi-card__body {
    min-width: 0;
  }

  .kpi-card span,
  .panel h3,
  .sub,
  .empty-hint,
  .alarm-list__time,
  .alarm-list__dev {
    color: #9bb4d8;
  }

  .kpi-card span,
  .panel h3,
  .sub,
  .empty-hint,
  .alarm-list,
  .stat-list,
  .mini-metrics {
    font-size: 12px;
  }

  .kpi-card strong,
  .stat-list strong,
  .mini-metrics b,
  .badge {
    font-family: 'Roboto Mono', SFMono-Regular, Consolas, monospace;
    color: #f4fbff;
    text-shadow: 0 0 8px rgb(61 110 255 / 25%);
  }

  .kpi-card strong {
    display: block;
    overflow: hidden;
    font-size: 18px;
    line-height: 1.15;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .kpi-card small {
    margin-left: 2px;
    font-size: 11px;
  }

  .kpi-card__bar {
    position: absolute;
    right: 8px;
    bottom: 4px;
    left: 8px;
    height: 3px;
    overflow: hidden;
    background: rgba(61, 110, 255, 0.12);
    border-radius: 99px;
  }

  .kpi-card__bar i {
    display: block;
    height: 100%;
    background: linear-gradient(90deg, color-mix(in srgb, var(--surface-accent) 28%, transparent), var(--surface-accent));
    box-shadow: 0 0 8px var(--surface-accent);
  }

  .main-grid {
    display: grid;
    grid-template-columns: 0.92fr 1.52fr 0.92fr;
    gap: 8px;
    isolation: isolate;
  }

  .main-grid::before {
    position: absolute;
    inset: 6% 22%;
    z-index: 0;
    pointer-events: none;
    content: '';
    background:
      repeating-radial-gradient(
        circle at 50% 52%,
        transparent 0 48px,
        rgb(67 217 255 / 5%) 49px,
        transparent 50px 76px
      ),
      conic-gradient(
        from 210deg at 50% 52%,
        transparent 0 43%,
        rgb(69 240 196 / 8%) 48%,
        transparent 53% 100%
      );
    filter: drop-shadow(0 0 24px rgb(67 217 255 / 12%));
    opacity: 0.58;
    animation: field-breathe 8s ease-in-out infinite;
  }

  .main-grid > .col {
    position: relative;
    z-index: 1;
  }

  .col {
    display: grid;
    grid-template-rows: repeat(3, minmax(0, 1fr));
    gap: 8px;
    min-height: 0;
  }


  .panel {
    --frame-shape: polygon(0 12px, 5px 12px, 12px 0, 34% 0, calc(34% + 8px) 6px, calc(66% - 8px) 6px, 66% 0, calc(100% - 12px) 0, 100% 12px, 100% calc(100% - 12px), calc(100% - 12px) 100%, 66% 100%, calc(66% - 8px) calc(100% - 5px), calc(34% + 8px) calc(100% - 5px), 34% 100%, 12px 100%, 0 calc(100% - 12px));

    display: flex;
    flex-direction: column;
    min-height: 0;
    padding: 8px 10px 10px;
    clip-path: var(--frame-shape);
  }

  .panel h3 {
    position: relative;
    z-index: 4;
    display: inline-flex;
    gap: 7px;
    align-items: center;
    width: fit-content;
    min-height: 19px;
    margin: 0;
    padding: 2px 16px 2px 8px;
    font-size: 13px;
    font-weight: 700;
    color: #eaf7ff;
    text-shadow: 0 0 9px color-mix(in srgb, var(--surface-accent) 58%, transparent);
    letter-spacing: 1.4px;
    background: linear-gradient(
      90deg,
      color-mix(in srgb, var(--surface-accent) 22%, transparent),
      color-mix(in srgb, var(--surface-accent) 7%, transparent) 72%,
      transparent
    );
    filter: drop-shadow(0 0 5px color-mix(in srgb, var(--surface-accent) 30%, transparent));
    clip-path: polygon(0 0, calc(100% - 9px) 0, 100% 50%, calc(100% - 9px) 100%, 0 100%, 5px 50%);
  }

  .panel h3::before {
    flex-shrink: 0;
    width: 11px;
    height: 3px;
    content: '';
    background: linear-gradient(90deg, #eaffff, var(--surface-accent));
    box-shadow: 0 0 9px var(--surface-accent);
  }

  .panel__head {
    position: relative;
    z-index: 4;
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 4px;
  }

  .panel__chart,
  .split-body,
  .hero-row,
  .alarm-list,
  .chart-wrap {
    flex: 1;
    min-height: 0;
  }

  .chart-wrap {
    position: relative;
    height: 100%;
  }

  .chart-wrap .split-body__chart,
  .chart-wrap .panel__chart {
    height: 100%;
  }

  .chart-center {
    position: absolute;
    top: 50%;
    left: 50%;
    z-index: 2;
    font-family: 'Roboto Mono', SFMono-Regular, Consolas, monospace;
    font-size: 26px;
    font-variant-numeric: tabular-nums;
    font-weight: 900;
    color: #f4feff;
    text-shadow:
      0 0 4px rgb(234 255 255 / 86%),
      0 0 14px rgb(67 217 255 / 72%),
      0 0 28px rgb(68 123 255 / 46%);
    letter-spacing: 1px;
    pointer-events: none;
    transform: translate(-50%, -50%);
  }

  .alarm-list {
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }

  .alarm-list__body {
    flex: 1;
    min-height: 0;
    overflow-x: hidden;
    overflow-y: auto;
  }

  .alarm-list__body::-webkit-scrollbar {
    width: 6px;
  }

  .alarm-list__body::-webkit-scrollbar-thumb {
    background: rgba(61, 110, 255, 0.35);
    border-radius: 99px;
  }

  .mini-metrics {
    display: flex;
    gap: 12px;
    color: #9bb4d8;
  }

  .mini-metrics .warn {
    color: #ff8aa0;
  }

  .badge {
    padding: 1px 8px;
    font-size: 12px;
    background: rgba(255, 92, 124, 0.16);
    border: 1px solid rgba(255, 92, 124, 0.4);
    border-radius: 99px;
  }

  .split-body {
    display: grid;
    grid-template-columns: 1.05fr 0.95fr;
    gap: 8px;
  }

  .stat-list {
    display: flex;
    flex-direction: column;
    gap: 8px;
    justify-content: center;
    margin: 0;
    padding: 0;
    list-style: none;
  }

  .stat-list li {
    display: grid;
    grid-template-columns: 8px 1fr auto;
    gap: 6px 8px;
    align-items: center;
  }

  .stat-list em {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    box-shadow: 0 0 8px currentcolor;
  }

  .stat-list b {
    grid-column: 2 / 4;
    height: 4px;
    overflow: hidden;
    background: rgba(61, 110, 255, 0.12);
    border-radius: 99px;
  }

  .stat-list b i {
    display: block;
    height: 100%;
    border-radius: inherit;
  }

  .hero-row {
    display: grid;
    grid-template-columns: 1fr 1.15fr 1fr;
    gap: 8px;
  }

  .panel--gauge p {
    margin: 0;
    font-size: 11px;
    color: #9bb4d8;
    text-align: center;
  }

  .panel--gauge-main {
    --surface-accent: #45f0c4;
    --surface-top-alpha: 60%;
    --surface-bottom-alpha: 50%;

    box-shadow:
      inset 0 0 0 1px rgba(61, 110, 255, 0.2),
      0 0 22px rgba(61, 110, 255, 0.16);
  }


  .alarm-list__row {
    display: grid;
    grid-template-columns: 58px 58px 0.9fr 1.4fr;
    gap: 6px;
    align-items: center;
    padding: 6px 8px;
    margin-bottom: 4px;
    border-left: 3px solid rgba(61, 110, 255, 0.35);
    background: rgba(61, 110, 255, 0.04);
  }

  .alarm-list__row--warn {
    border-left-color: #ffb020;
    background: rgba(255, 176, 32, 0.08);
  }

  .alarm-list__row--danger {
    border-left-color: #ff5c7c;
    background: rgba(255, 92, 124, 0.1);
  }

  .ellipsis {
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .empty-hint {
    display: grid;
    height: 100%;
    place-items: center;
  }

  .lv {
    display: inline-flex;
    justify-content: center;
    padding: 1px 6px;
    font-size: 11px;
    font-style: normal;
    border-radius: 999px;
  }

  .lv--ok { color: #6B9BFF; background: rgba(107, 155, 255, 0.16); }
  .lv--warn { color: #ffb020; background: rgba(255, 176, 32, 0.16); }
  .lv--danger { color: #ff5c7c; background: rgba(255, 92, 124, 0.16); }

  @keyframes scan-move {
    0% { transform: translateY(-100%); }
    100% { transform: translateY(100%); }
  }

  @keyframes live-pulse {
    0%,
    100% {
      opacity: 0.55;
      transform: scale(0.82);
    }

    50% {
      opacity: 1;
      transform: scale(1.16);
    }
  }

  @property --border-angle {
    syntax: '<angle>';
    inherits: false;
    initial-value: 0deg;
  }

  @keyframes border-patrol {
    to {
      --border-angle: 360deg;
    }
  }

  @keyframes field-breathe {
    0%,
    100% {
      opacity: 0.48;
      transform: scale(0.98);
    }

    50% {
      opacity: 0.68;
      transform: scale(1.03);
    }
  }

  @media (width <= 1500px) {
    .title-wing {
      flex-basis: 100px;
      width: 100px;
    }

    .live-state b {
      display: none;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .main-grid::before,
    .kpi-card::after,
    .panel::after,
    .screen__scan,
    .live-state i {
      animation: none;
    }
  }
</style>
