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
          <h1>设备数据采集与监控大屏</h1>
        </div>
        <i class="title-wing title-wing--r" />
      </div>
      <div class="screen__time">时间：{{ clockText }}</div>
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
          <i class="corner corner--tl" /><i class="corner corner--tr" />
          <i class="corner corner--bl" /><i class="corner corner--br" />
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
          <i class="corner corner--tl" /><i class="corner corner--tr" />
          <i class="corner corner--bl" /><i class="corner corner--br" />
          <h3>设备类型分布</h3>
          <div ref="typeChartRef" class="panel__chart" />
        </article>

        <article class="panel panel--split" @click="go('/device-monitor/log')">
          <i class="corner corner--tl" /><i class="corner corner--tr" />
          <i class="corner corner--bl" /><i class="corner corner--br" />
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

      <div class="col col--center">
        <div class="hero-row">
          <article class="panel panel--gauge" @click="go('/device-monitor/log')">
            <i class="corner corner--tl" /><i class="corner corner--tr" />
            <i class="corner corner--bl" /><i class="corner corner--br" />
            <h3>日志容量</h3>
            <div class="chart-wrap">
              <div ref="logCapChartRef" class="panel__chart" />
              <div class="chart-center chart-center--sm">{{ logCapRate }}%</div>
            </div>
            <p>{{ logCapText }}</p>
          </article>
          <article class="panel panel--gauge panel--gauge-main" @click="go('/device-monitor/monitor')">
            <i class="corner corner--tl" /><i class="corner corner--tr" />
            <i class="corner corner--bl" /><i class="corner corner--br" />
            <h3>设备在线率</h3>
            <div class="chart-wrap">
              <div ref="onlineChartRef" class="panel__chart" />
              <div class="chart-center">{{ onlineRate }}%</div>
            </div>
            <p>{{ overview?.onlineDeviceTotal ?? 0 }} / {{ overview?.deviceTotal ?? 0 }} 台</p>
          </article>
          <article class="panel panel--gauge" @click="go('/device-monitor/driver')">
            <i class="corner corner--tl" /><i class="corner corner--tr" />
            <i class="corner corner--bl" /><i class="corner corner--br" />
            <h3>驱动可用率</h3>
            <div class="chart-wrap">
              <div ref="driverRateChartRef" class="panel__chart" />
              <div class="chart-center chart-center--sm">{{ driverRate }}%</div>
            </div>
            <p>{{ driverAvailable }} / {{ driverTotal }}</p>
          </article>
        </div>

        <article class="panel" @click="go('/device-monitor/history')">
          <i class="corner corner--tl" /><i class="corner corner--tr" />
          <i class="corner corner--bl" /><i class="corner corner--br" />
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
          <i class="corner corner--tl" /><i class="corner corner--tr" />
          <i class="corner corner--bl" /><i class="corner corner--br" />
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
        <article class="panel panel--alarm" @click="go('/device-monitor/log')">
          <i class="corner corner--tl" /><i class="corner corner--tr" />
          <i class="corner corner--bl" /><i class="corner corner--br" />
          <div class="panel__head">
            <h3>告警类型分布</h3>
            <strong class="badge">{{ alarmTotal }}</strong>
          </div>
          <div ref="alarmChartRef" class="panel__chart" />
        </article>

        <article class="panel" @click="go('/device-monitor/log')">
          <i class="corner corner--tl" /><i class="corner corner--tr" />
          <i class="corner corner--bl" /><i class="corner corner--br" />
          <h3>日志级别趋势</h3>
          <div ref="logLevelChartRef" class="panel__chart" />
        </article>

        <article class="panel" @click="go('/device-monitor/driver')">
          <i class="corner corner--tl" /><i class="corner corner--tr" />
          <i class="corner corner--bl" /><i class="corner corner--br" />
          <div class="panel__head">
            <h3>驱动绑定 TOP</h3>
            <span class="sub">可用率 {{ driverRate }}%</span>
          </div>
          <div ref="driverBarRef" class="panel__chart" />
        </article>

        <article class="panel panel--sys">
          <i class="corner corner--tl" /><i class="corner corner--tr" />
          <i class="corner corner--bl" /><i class="corner corner--br" />
          <h3>系统构成</h3>
          <div class="sys-grid">
            <button v-for="item in sysTiles" :key="item.path" type="button" class="sys-tile" @click="go(item.path)">
              <ArtSvgIcon :icon="item.icon" />
              <strong>{{ item.value }}</strong>
              <span>{{ item.label }}</span>
            </button>
          </div>
        </article>
      </div>
    </section>
  </div>
</template>

<script setup lang="ts">
  import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
  import { useRouter } from 'vue-router'
  import { storeToRefs } from 'pinia'
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
  import { fetchGetRoleList, fetchGetUserList } from '@/api/system-manage'
  import { getFlatCategories } from '@/views/project-docs/docs.config'
  import { useMenuStore } from '@/store/modules/menu'
  import type { AppRouteRecord } from '@/types/router'

  defineOptions({ name: 'MonitorScreen' })

  type ChartInst = echarts.ECharts

  const HUD = {
    text: '#E8EEF8',
    muted: '#9BB4D8',
    line: 'rgba(61, 110, 255, 0.38)',
    tooltipBg: 'rgba(4, 14, 40, 0.94)',
    colors: ['#3D6EFF', '#6B9BFF', '#1E4A9C', '#FFB020', '#FF5C7C', '#7B8CFF', '#F4E243']
  }

  const router = useRouter()
  const menuStore = useMenuStore()
  const { menuList } = storeToRefs(menuStore)

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
  const roleTotal = ref(0)
  const logStats = ref<Api.DeviceMonitor.LogStats | null>(null)
  const trendAxis = ref<string[]>([])
  const trendSeries = ref<Api.DeviceMonitor.DashboardTrendSeries[]>([])

  const docCategories = getFlatCategories()
  const docTotal = docCategories.reduce((sum, cat) => sum + cat.docs.length, 0)
  const menuTotal = computed(() => countMenus(menuList.value))

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
    const max = logStats.value?.maxTotalMb || 0
    if (!max) return 0
    return Number((((logStats.value?.totalSizeMb || 0) / max) * 100).toFixed(1))
  })

  const logCapText = computed(() => {
    const used = Math.round(logStats.value?.totalSizeMb || 0)
    const max = Math.round(logStats.value?.maxTotalMb || 0)
    return max ? `${used} / ${max} MB` : `${used} MB`
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
      { name: '在线', value: onlineCount.value, color: '#6B9BFF', ratio: ratioOf(onlineCount.value, total) },
      { name: '离线', value: offlineCount.value, color: '#1E4A9C', ratio: ratioOf(offlineCount.value, total) },
      { name: '风险', value: riskCount.value, color: '#FF5C7C', ratio: ratioOf(riskCount.value, total) }
    ]
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

  const sysTiles = computed(() => [
    { label: '用户', value: String(userTotal.value), icon: 'ri:user-3-line', path: '/system/user' },
    { label: '角色', value: String(roleTotal.value), icon: 'ri:shield-user-line', path: '/system/role' },
    { label: '菜单', value: String(menuTotal.value), icon: 'ri:menu-2-line', path: '/system/menu' },
    { label: '文档', value: String(docTotal), icon: 'ri:book-2-line', path: '/project-docs/overview' }
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

  const countMenus = (list: AppRouteRecord[]): number =>
    list.reduce((sum, item) => sum + 1 + (item.children?.length ? countMenus(item.children) : 0), 0)

  const shortTime = (value: string) => {
    const text = String(value).replace('T', ' ')
    return text.length >= 16 ? text.slice(11, 16) : text
  }

  const tooltip = (trigger: 'item' | 'axis' = 'item'): EChartsOption['tooltip'] => ({
    trigger,
    backgroundColor: HUD.tooltipBg,
    borderColor: HUD.line,
    textStyle: { color: HUD.text, fontSize: 12 },
    extraCssText: 'box-shadow: 0 0 16px rgba(61,110,255,0.25)'
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
        animationDurationUpdate: 360,
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
    graphic: [],
    series: [
      {
        type: 'pie',
        radius: ['54%', '76%'],
        center: ['50%', '50%'],
        avoidLabelOverlap: true,
        label: { show: false },
        labelLine: { show: false },
        cursor: 'pointer',
        itemStyle: { borderColor: '#000b24', borderWidth: 3 },
        emphasis: {
          scale: true,
          scaleSize: 6,
          itemStyle: { shadowBlur: 18, shadowColor: 'rgba(61,110,255,0.45)' }
        },
        data: data.map((item, index) => ({
          name: item.name,
          value: item.value,
          itemStyle: { color: item.color || HUD.colors[index % HUD.colors.length] }
        }))
      }
    ]
  })

  const gaugeRing = (percent: number, color: string): EChartsOption => {
    const safe = Math.max(0, Math.min(100, Number(percent) || 0))
    return {
      tooltip: { show: false },
      series: [
        {
          type: 'pie',
          radius: ['68%', '86%'],
          center: ['50%', '50%'],
          silent: true,
          label: { show: false },
          cursor: 'pointer',
          data: [
            {
              value: safe,
              itemStyle: {
                color: new graphic.LinearGradient(0, 0, 1, 1, [
                  { offset: 0, color },
                  { offset: 1, color: '#EAF6FF' }
                ])
              }
            },
            { value: 100 - safe, itemStyle: { color: 'rgba(61, 110, 255, 0.12)' } }
          ]
        }
      ],
      graphic: []
    }
  }

  const renderCharts = () => {
    draw(
      commChartRef.value,
      ringOption(
        [
          { name: '在线', value: onlineCount.value, color: '#6B9BFF' },
          { name: '离线', value: offlineCount.value, color: '#1E4A9C' },
          { name: '风险', value: riskCount.value, color: '#FF5C7C' }
        ]
      )
    )

    draw(typeChartRef.value, {
      tooltip: tooltip('axis'),
      legend: {
        top: 0,
        bottom: 'auto',
        left: 'center',
        itemGap: 18,
        textStyle: { color: HUD.muted, fontSize: 11 },
        itemWidth: 10,
        itemHeight: 8
      },
      grid: { top: 30, right: 12, bottom: 8, left: 72 },
      xAxis: {
        type: 'value',
        axisLabel: { color: HUD.muted },
        axisLine: { lineStyle: { color: HUD.line } },
        splitLine: { lineStyle: { color: 'rgba(61,110,255,0.10)', type: 'dashed' } }
      },
      yAxis: {
        type: 'category',
        data: typeGroups.value.map((item) => item.typeName),
        axisLabel: { color: HUD.text, fontSize: 11 },
        axisLine: { lineStyle: { color: HUD.line } }
      },
      series: [
        {
          name: '在线',
          type: 'bar',
          stack: 'total',
          barWidth: 10,
          cursor: 'pointer',
          data: typeGroups.value.map((item) => item.onlineCount),
          itemStyle: { color: '#4A7CFF', borderRadius: [0, 4, 4, 0] }
        },
        {
          name: '离线',
          type: 'bar',
          stack: 'total',
          barWidth: 10,
          cursor: 'pointer',
          data: typeGroups.value.map((item) => Math.max(item.deviceCount - item.onlineCount, 0)),
          itemStyle: { color: '#1E4A9C', borderRadius: [0, 4, 4, 0] }
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

    draw(logCapChartRef.value, gaugeRing(logCapRate.value, '#7B8CFF'))
    draw(onlineChartRef.value, gaugeRing(onlineRate.value, '#3D6EFF'))
    draw(driverRateChartRef.value, gaugeRing(driverRate.value, '#6B9BFF'))

    draw(trendChartRef.value, {
      tooltip: tooltip('axis'),
      legend: {
        top: 0,
        textStyle: { color: HUD.muted, fontSize: 11 },
        itemWidth: 10,
        itemHeight: 8
      },
      grid: { top: 32, right: 18, bottom: 24, left: 40 },
      xAxis: {
        type: 'category',
        data: trendAxis.value,
        boundaryGap: false,
        axisLabel: { color: HUD.muted, fontSize: 10 },
        axisLine: { lineStyle: { color: HUD.line } }
      },
      yAxis: {
        type: 'value',
        axisLabel: { color: HUD.muted },
        splitLine: { lineStyle: { color: 'rgba(61,110,255,0.10)', type: 'dashed' } }
      },
      series: trendSeries.value.map((item, index) => ({
        name: item.metricName,
        type: 'line',
        smooth: true,
        showSymbol: false,
        cursor: 'pointer',
        data: item.data,
        lineStyle: { width: 2, color: HUD.colors[index % HUD.colors.length] },
        itemStyle: { color: HUD.colors[index % HUD.colors.length] },
        areaStyle: {
          color: new graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: index === 0 ? 'rgba(61,110,255,0.32)' : 'rgba(255,92,124,0.22)' },
            { offset: 1, color: 'rgba(61,110,255,0.02)' }
          ])
        }
      }))
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
          roseType: 'radius',
          radius: ['16%', '68%'],
          center: ['50%', '52%'],
          cursor: 'pointer',
          label: { color: HUD.text, fontSize: 11, formatter: '{b}\n{d}%' },
          itemStyle: { borderColor: '#000b24', borderWidth: 2 },
          emphasis: {
            scale: true,
            itemStyle: { shadowBlur: 16, shadowColor: 'rgba(61,110,255,0.4)' }
          },
          data: alarmGroups.value.map((item, index) => ({
            name: item.groupName,
            value: item.count,
            itemStyle: { color: HUD.colors[index % HUD.colors.length] }
          }))
        }
      ]
    })

    const levelTrend = (logStats.value?.levelTrend || []).slice(-12)
    draw(logLevelChartRef.value, {
      tooltip: tooltip('axis'),
      legend: {
        top: 0,
        textStyle: { color: HUD.muted, fontSize: 11 },
        itemWidth: 8,
        itemHeight: 8
      },
      grid: { top: 32, right: 10, bottom: 22, left: 36 },
      xAxis: {
        type: 'category',
        data: levelTrend.map((item) => shortTime(item.time)),
        axisLabel: { color: HUD.muted, fontSize: 10 },
        axisLine: { lineStyle: { color: HUD.line } }
      },
      yAxis: {
        type: 'value',
        axisLabel: { color: HUD.muted },
        splitLine: { lineStyle: { color: 'rgba(61,110,255,0.10)', type: 'dashed' } }
      },
      series: [
        { name: 'INFO', type: 'bar', stack: 'lv', cursor: 'pointer', data: levelTrend.map((i) => i.info), itemStyle: { color: '#3D6EFF' } },
        { name: 'WARN', type: 'bar', stack: 'lv', cursor: 'pointer', data: levelTrend.map((i) => i.warn), itemStyle: { color: '#FFB020' } },
        { name: 'ERROR', type: 'bar', stack: 'lv', cursor: 'pointer', data: levelTrend.map((i) => i.error), itemStyle: { color: '#FF5C7C' } },
        { name: 'CRIT', type: 'bar', stack: 'lv', cursor: 'pointer', data: levelTrend.map((i) => i.critical), itemStyle: { color: '#F4E243' } }
      ]
    })

    const topDrivers = [...driverList.value]
      .sort((a, b) => (b.boundDeviceCount || 0) - (a.boundDeviceCount || 0))
      .slice(0, 6)
    draw(driverBarRef.value, {
      tooltip: tooltip('axis'),
      grid: { top: 8, right: 16, bottom: 8, left: 86 },
      xAxis: {
        type: 'value',
        axisLabel: { color: HUD.muted },
        splitLine: { lineStyle: { color: 'rgba(61,110,255,0.10)', type: 'dashed' } }
      },
      yAxis: {
        type: 'category',
        data: topDrivers.map((item) => item.driverName),
        axisLabel: { color: HUD.text, fontSize: 11 },
        axisLine: { lineStyle: { color: HUD.line } }
      },
      series: [
        {
          type: 'bar',
          barWidth: 10,
          cursor: 'pointer',
          data: topDrivers.map((item) => item.boundDeviceCount || 0),
          itemStyle: {
            color: new graphic.LinearGradient(0, 0, 1, 0, [
              { offset: 0, color: '#1A3F9C' },
              { offset: 1, color: '#4A7CFF' }
            ]),
            borderRadius: [0, 4, 4, 0]
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
      roleRes,
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
      fetchGetRoleList({ current: 1, size: 1 }),
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
    if (roleRes.status === 'fulfilled') roleTotal.value = roleRes.value.total || 0
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
    max-height: 100vh;
    min-height: 0 !important;
    padding: 14px 16px 16px;
    overflow: hidden;
    color: #e8eef8;
    background: #000b24;
    border: 1px solid rgba(61, 110, 255, 0.42);
    box-shadow: inset 0 0 0 1px rgba(20, 50, 120, 0.45);
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
      radial-gradient(circle at 50% 0%, rgba(0, 85, 255, 0.32), transparent 38%),
      radial-gradient(circle at 8% 18%, rgba(61, 110, 255, 0.14), transparent 26%),
      linear-gradient(180deg, #041433 0%, #000b24 46%, #000817 100%);
  }

  .screen__scan {
    background: linear-gradient(180deg, transparent 0%, rgba(61, 110, 255, 0.06) 50%, transparent 100%);
    animation: scan-move 8s linear infinite;
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
    background: linear-gradient(90deg, transparent, rgba(90, 140, 255, 0.9), transparent);
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
    background: linear-gradient(180deg, transparent, rgba(90, 140, 255, 0.75), transparent);
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
    border-color: #5a8cff;
    border-style: solid;
    filter: drop-shadow(0 0 6px rgba(74, 124, 255, 0.85));
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
    border-color: rgba(140, 175, 255, 0.85);
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
    min-height: 58px;
  }

  .exit-btn,
  .sys-tile,
  .kpi-card,
  .panel,
  .panel__chart,
  .split-body__chart,
  .hero-row .panel__chart {
    cursor: pointer;
  }

  .kpi-card :deep(canvas),
  .panel :deep(canvas),
  .sys-tile :deep(canvas) {
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
    flex-shrink: 0;
    padding: 6px 46px 7px;
    background: linear-gradient(180deg, rgba(22, 48, 110, 0.96), rgba(6, 16, 48, 0.98));
    border: 1px solid rgba(90, 140, 255, 0.88);
    clip-path: polygon(20px 0, calc(100% - 20px) 0, 100% 50%, calc(100% - 20px) 100%, 20px 100%, 0 50%);
    box-shadow:
      0 0 22px rgba(61, 110, 255, 0.45),
      inset 0 0 18px rgba(61, 110, 255, 0.18);
  }

  .title-plate h1 {
    margin: 0;
    font-size: 22px;
    font-weight: 800;
    line-height: 1.2;
    letter-spacing: 3px;
    color: #f4f8ff;
    white-space: nowrap;
    background: linear-gradient(180deg, #ffffff 0%, #c5d8ff 50%, #6b9bff 100%);
    background-clip: text;
    -webkit-text-fill-color: transparent;
    filter: drop-shadow(0 0 10px rgba(90, 140, 255, 0.55));
  }

  .title-plate__accent {
    position: absolute;
    left: 50%;
    width: 72px;
    height: 7px;
    transform: translateX(-50%);
    border: 1px solid rgba(90, 140, 255, 0.9);
    background: rgba(30, 70, 180, 0.45);
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
    flex: 0 0 260px;
    width: 260px;
    height: 14px;
  }

  .title-wing::before {
    position: absolute;
    top: 50%;
    right: 0;
    left: 8px;
    height: 2px;
    content: '';
    background: linear-gradient(90deg, transparent 0%, rgba(74, 124, 255, 0.18) 18%, #4a7cff 68%, #8bb0ff 100%);
    box-shadow:
      0 0 8px rgba(74, 124, 255, 0.7),
      0 5px 0 -3px rgba(74, 124, 255, 0.4);
    transform: translateY(-50%);
  }

  .title-wing::after {
    position: absolute;
    top: 50%;
    right: 0;
    width: 9px;
    height: 9px;
    content: '';
    border: 1px solid #6b9bff;
    box-shadow: 0 0 8px rgba(74, 124, 255, 0.8);
    transform: translateY(-50%) rotate(45deg);
  }

  .title-wing--l {
    transform: scaleX(-1);
  }

  .screen__time {
    z-index: 2;
    font-size: 13px;
    color: #e8eef8;
    text-shadow: 0 0 8px rgba(61, 110, 255, 0.25);
  }

  .kpi-row {
    display: grid;
    grid-template-columns: repeat(12, minmax(0, 1fr));
    gap: 6px;
  }

  .kpi-card,
  .panel,
  .sys-tile {
    position: relative;
    overflow: hidden;
    background: rgba(4, 22, 54, 0.82);
    border: 1px solid rgba(61, 110, 255, 0.3);
    box-shadow:
      inset 0 0 0 1px rgba(0, 85, 255, 0.12),
      0 0 16px rgba(61, 110, 255, 0.08);
    transition:
      transform 0.2s ease,
      box-shadow 0.2s ease,
      border-color 0.2s ease;
  }

  .kpi-card:hover,
  .panel:hover,
  .sys-tile:hover,
  .exit-btn:hover {
    border-color: rgba(61, 110, 255, 0.8);
    box-shadow:
      0 10px 28px rgba(61, 110, 255, 0.22),
      inset 0 0 18px rgba(61, 110, 255, 0.08);
    transform: translateY(-3px);
  }

  .kpi-card {
    display: flex;
    gap: 8px;
    align-items: center;
    min-width: 0;
    padding: 7px 8px 10px;
  }

  .kpi-card__icon {
    display: grid;
    flex-shrink: 0;
    width: 28px;
    height: 28px;
    font-size: 15px;
    color: #4A7CFF;
    place-items: center;
    background: rgba(61, 110, 255, 0.1);
    border-radius: 6px;
  }

  .kpi-card--ok .kpi-card__icon { color: #6B9BFF; }
  .kpi-card--warn .kpi-card__icon { color: #ffb020; }
  .kpi-card--danger .kpi-card__icon { color: #ff5c7c; }
  .kpi-card--info .kpi-card__icon { color: #7fb8ff; }

  .kpi-card__body {
    min-width: 0;
  }

  .kpi-card span,
  .panel h3,
  .sub,
  .empty-hint,
  .sys-tile span,
  .alarm-list__time,
  .alarm-list__dev {
    color: #9bb4d8;
  }

  .kpi-card span,
  .panel h3,
  .sub,
  .sys-tile span,
  .empty-hint,
  .alarm-list,
  .stat-list,
  .mini-metrics {
    font-size: 12px;
  }

  .kpi-card strong,
  .sys-tile strong,
  .stat-list strong,
  .mini-metrics b,
  .badge {
    color: #f4fbff;
    text-shadow: 0 0 8px rgba(61, 110, 255, 0.25);
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
    background: linear-gradient(90deg, #1a3f9c, #4a7cff);
    box-shadow: 0 0 8px #4a7cff;
  }

  .main-grid {
    display: grid;
    grid-template-columns: 1fr 1.28fr 1fr;
    gap: 8px;
  }

  .col {
    display: grid;
    grid-template-rows: 1.08fr 1fr 0.92fr;
    gap: 8px;
    min-height: 0;
  }

  .col--center {
    grid-template-rows: 0.95fr 1.05fr 1fr;
  }

  .col:last-child {
    grid-template-rows: 1fr 1fr 0.9fr 0.9fr;
  }

  .panel {
    display: flex;
    flex-direction: column;
    min-height: 0;
    padding: 8px 10px 10px;
  }

  .panel h3 {
    margin: 0;
    font-size: 13px;
    font-weight: 700;
    color: #e8eef8;
    letter-spacing: 1px;
  }

  .panel__head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 4px;
  }

  .panel__chart,
  .split-body,
  .hero-row,
  .alarm-list,
  .sys-grid,
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
    font-size: 22px;
    font-weight: 800;
    color: #e8eef8;
    letter-spacing: 0.5px;
    pointer-events: none;
    text-shadow: 0 0 12px rgba(61, 110, 255, 0.5);
    transform: translate(-50%, -50%);
  }

  .chart-center--sm {
    font-size: 18px;
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
    box-shadow:
      inset 0 0 0 1px rgba(61, 110, 255, 0.2),
      0 0 22px rgba(61, 110, 255, 0.16);
  }

  .sys-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 8px;
  }

  .sys-tile {
    display: flex;
    flex-direction: column;
    gap: 2px;
    justify-content: center;
    padding: 8px;
    color: inherit;
    text-align: left;
    border-radius: 8px;
  }

  .sys-tile .art-svg-icon {
    font-size: 16px;
    color: #4A7CFF;
  }

  .sys-tile strong {
    font-size: 18px;
  }

  .corner {
    position: absolute;
    z-index: 2;
    width: 12px;
    height: 12px;
    pointer-events: none;
    border-color: #4A7CFF;
    border-style: solid;
    filter: drop-shadow(0 0 4px #4A7CFF);
  }

  .corner--tl { top: -1px; left: -1px; border-width: 2px 0 0 2px; }
  .corner--tr { top: -1px; right: -1px; border-width: 2px 2px 0 0; }
  .corner--bl { bottom: -1px; left: -1px; border-width: 0 0 2px 2px; }
  .corner--br { right: -1px; bottom: -1px; border-width: 0 2px 2px 0; }

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
</style>
