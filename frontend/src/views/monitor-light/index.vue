<!-- 浅色监控大屏：全屏页，静默刷新，失败时清空旧值 -->
<template>
  <div class="screen">
    <header class="top">
      <button class="text-btn" type="button" @click="exitScreen">退出</button>
      <div class="title">
        <span>INDUSTRIAL DATA OPERATIONS</span>
        <h1>设备数据采集与监控</h1>
      </div>
      <div class="top-meta">
        <time>{{ clockText }}</time>
        <button class="text-btn" type="button" aria-label="刷新数据" @click="refresh">刷新</button>
      </div>
    </header>

    <section class="kpis">
      <button
        v-for="item in kpiCards"
        :key="item.key"
        class="kpi"
        type="button"
        @click="go(item.path)"
      >
        <span class="kpi-icon">
          <ArtSvgIcon :icon="item.icon" />
        </span>
        <span class="kpi-label">{{ item.label }}</span>
        <strong>{{ item.display }}</strong>
      </button>
    </section>

    <section class="body">
      <div class="side">
        <article class="card overview">
          <h3>开放概况</h3>
          <p>在线率 {{ rateText(onlineRate) }}</p>
          <p>驱动可用 {{ rateText(driverRate) }}</p>
          <p>日志剩余 {{ rateText(logRemainRate) }}</p>
        </article>
        <article class="card chart-card">
          <h3>设备分布</h3>
          <StatChart :option="deviceOption" />
        </article>
        <article class="card chart-card">
          <h3>资源</h3>
          <StatChart :option="resourceOption" />
        </article>
        <article class="card chart-card">
          <h3>日志类型</h3>
          <StatChart :option="logTypeOption" />
        </article>
      </div>

      <div class="stage">
        <div class="platform">
          <img :src="platformImage" alt="" />
          <div class="modules">
            <button
              v-for="item in moduleCards"
              :key="item.id"
              class="module"
              :class="item.tone"
              type="button"
              @click="openModule(item)"
            >
              <span class="module-state"><i /></span>
              <span>{{ item.name }}</span>
            </button>
          </div>
        </div>
        <article class="card chart-card trend">
          <h3>采集趋势</h3>
          <StatChart :option="trendOption" />
        </article>
      </div>
    </section>

    <section class="bottom">
      <article class="card floating">
        <h3>今日日志</h3>
        <p v-if="logEmpty" class="empty">今日暂无日志</p>
        <StatChart :option="logOption" />
      </article>
      <article class="card floating">
        <h3>近24小时告警</h3>
        <div class="alarm-legend">
          <button v-for="item in severities" :key="item.key" type="button">
            <i :style="{ background: item.color }" />
            {{ item.name }}
            <b>{{ displayCount(item.count) }}</b>
          </button>
        </div>
        <StatChart :option="alarmOption" />
      </article>
    </section>

    <div v-if="activeModule" class="dialog-mask" @click.self="closeModule">
      <div role="dialog" aria-modal="true" class="dialog" :aria-label="activeModule.name">
        <button ref="closeRef" class="dialog-close" type="button" @click="closeModule">关闭</button>
        <h2>{{ activeModule.name }}</h2>
        <p>状态 {{ statusText(activeModule) }}</p>
        <p>{{ activeModule.detailLabel }} {{ detailText(activeModule) }}</p>
        <p>影响 {{ activeModule.impact }}</p>
        <p>建议 {{ activeModule.advice }}</p>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
  import { useRouter } from 'vue-router'
  import type { EChartsOption } from '@/plugins/echarts'
  import request from '@/utils/http'
  import ArtSvgIcon from '@/components/core/base/art-svg-icon/index.vue'
  import platformImage from '@/assets/images/monitor-light/module-platform-v2.png'
  import StatChart from './StatChart.vue'
  import { alignLogCounts, edgeState, linkState, percent, toHourLabel, type LinkTone } from './data'

  defineOptions({ name: 'LightMonitorScreen' })

  const BASE = '/api/device-monitor'
  const REFRESH_MS = 30_000

  interface Overview {
    deviceTotal?: number
    onlineDeviceTotal?: number
    offlineDeviceTotal?: number
    riskDeviceTotal?: number
    pointTotal?: number
    systemAlertTotal?: number
  }

  interface TypeGroup {
    typeName?: string
    deviceCount?: number
  }

  interface LogStats {
    todayTotal?: number
    maxTotalMb?: number
    totalSizeMb?: number
    typeRatio?: Array<{ type?: string; count?: number }>
    levelTrend?: Array<{
      time: string
      info: number
      warn: number
      error: number
      critical: number
      debug: number
    }>
  }

  interface DriverList {
    total?: number
    availableCount?: number
  }

  interface Trend {
    timeAxis?: string[]
    series?: Array<{ metricKey?: string; data?: number[] }>
  }

  interface AlarmGroup {
    groupKey?: string
    groupName?: string
    count?: number
  }

  interface EdgeHealth {
    collector?: { status?: string }
    uploader?: { status?: string }
    storage?: { status?: string }
  }

  interface WriterHealth {
    streams?: Array<{ status?: string }>
    dlqTotal?: number
  }

  interface PingHealth {
    service?: string
  }

  interface ModuleCard {
    id: string
    name: string
    tone: LinkTone
    warningText: string
    detailLabel: string
    detailWarning: string
    detailHealthy: string
    impact: string
    advice: string
  }

  const MODULE_DEFS: Omit<ModuleCard, 'tone'>[] = [
    {
      id: 'collector',
      name: '边缘采集',
      warningText: '采集波动',
      detailLabel: '采集进程',
      detailWarning: '波动',
      detailHealthy: '运行',
      impact: '现场数据可能中断',
      advice: '检查边缘节点心跳与采集进程'
    },
    {
      id: 'storage',
      name: '边缘存储',
      warningText: '缓存升高',
      detailLabel: '本地缓存',
      detailWarning: '升高',
      detailHealthy: '正常',
      impact: '边缘落盘延迟',
      advice: '检查边缘磁盘与本地队列'
    },
    {
      id: 'uploader',
      name: '边缘上传',
      warningText: '积压',
      detailLabel: '上行队列',
      detailWarning: '积压',
      detailHealthy: '畅通',
      impact: '数据上送延迟',
      advice: '检查上行链路与边缘出站队列'
    },
    {
      id: 'server',
      name: '服务端',
      warningText: '服务异常',
      detailLabel: '接口',
      detailWarning: '异常',
      detailHealthy: '可达',
      impact: '平台接口不可用',
      advice: '检查服务端进程与依赖'
    },
    {
      id: 'dispatch',
      name: '实时分发',
      warningText: '积压',
      detailLabel: '实时出口',
      detailWarning: '阻塞',
      detailHealthy: '畅通',
      impact: '实时值推送延迟',
      advice: '检查 Redis、WebSocket 订阅与服务端消费'
    },
    {
      id: 'engine',
      name: '存储引擎',
      warningText: '写入变慢',
      detailLabel: '入库',
      detailWarning: '变慢',
      detailHealthy: '正常',
      impact: '历史入库延迟',
      advice: '检查 TDengine 与写入队列'
    }
  ]

  const SEVERITIES = [
    { key: 'WARN', name: '一般', color: '#f0a04b' },
    { key: 'ERROR', name: '严重', color: '#e15b64' },
    { key: 'CRITICAL', name: '紧急', color: '#c4492c' }
  ]

  const LOG_TYPE_NAME: Record<string, string> = {
    system: '系统',
    collect: '采集',
    storage: '存储',
    alarm: '告警'
  }

  const router = useRouter()
  const now = ref(new Date())
  const loaded = ref(false)
  const unavailable = ref(false)
  const overview = ref<Overview | null>(null)
  const typeGroups = ref<TypeGroup[]>([])
  const logStats = ref<LogStats | null>(null)
  const driverTotal = ref(0)
  const driverAvailable = ref(0)
  const trend = ref<Trend | null>(null)
  const alarmGroups = ref<AlarmGroup[]>([])
  const edge = ref<EdgeHealth | null>(null)
  const writer = ref<WriterHealth | null>(null)
  const ping = ref<PingHealth | null>(null)
  const activeModule = ref<ModuleCard | null>(null)
  const closeRef = ref<HTMLButtonElement>()

  let clockTimer: ReturnType<typeof setInterval> | null = null
  let refreshTimer: ReturnType<typeof setInterval> | null = null
  let refreshInFlight = false

  const clockText = computed(() => {
    const date = now.value
    const pad = (value: number) => String(value).padStart(2, '0')
    return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
  })

  const displayCount = (value?: number | null) => {
    if (!loaded.value || unavailable.value) return '—'
    return String(value ?? 0)
  }

  const rateText = (value: number) =>
    unavailable.value || !loaded.value ? '—' : `${Math.round(value)}%`

  const onlineRate = computed(() =>
    percent(overview.value?.onlineDeviceTotal ?? 0, overview.value?.deviceTotal ?? 0)
  )
  const driverRate = computed(() => percent(driverAvailable.value, driverTotal.value))
  const logRemainRate = computed(() => {
    const max = Math.max(logStats.value?.maxTotalMb || 0, 0)
    const used = Math.max(logStats.value?.totalSizeMb || 0, 0)
    return percent(Math.max(max - used, 0), max)
  })

  const kpiCards = computed(() => [
    {
      key: 'device',
      label: '设备总数',
      display: displayCount(overview.value?.deviceTotal),
      icon: 'ri:hard-drive-3-line',
      path: '/device-monitor/device'
    },
    {
      key: 'online',
      label: '在线设备',
      display: displayCount(overview.value?.onlineDeviceTotal),
      icon: 'ri:wifi-line',
      path: '/device-monitor/monitor'
    },
    {
      key: 'offline',
      label: '离线设备',
      display: displayCount(overview.value?.offlineDeviceTotal),
      icon: 'ri:wifi-off-line',
      path: '/device-monitor/monitor'
    },
    {
      key: 'risk',
      label: '风险设备',
      display: displayCount(overview.value?.riskDeviceTotal),
      icon: 'ri:error-warning-line',
      path: '/device-monitor/monitor'
    },
    {
      key: 'point',
      label: '点位总数',
      display: displayCount(overview.value?.pointTotal),
      icon: 'ri:flashlight-line',
      path: '/device-monitor/point'
    },
    {
      key: 'alarm',
      label: '系统告警',
      display: displayCount(overview.value?.systemAlertTotal),
      icon: 'ri:alarm-warning-line',
      path: '/device-monitor/log'
    }
  ])

  const writerTone = computed<LinkTone>(() => {
    const tones = (writer.value?.streams || []).map((item) => edgeState(item.status))
    if (!tones.length) return 'unknown'
    return tones.reduce((current, tone) => linkState(current, tone))
  })

  const moduleCards = computed<ModuleCard[]>(() => {
    const unknown = MODULE_DEFS.map((item) => ({ ...item, tone: 'unknown' as LinkTone }))
    if (!loaded.value || unavailable.value) return unknown

    const collector = edgeState(edge.value?.collector?.status)
    const storage = edge.value?.storage?.status ? edgeState(edge.value.storage.status) : collector
    const uploader = linkState(edgeState(edge.value?.uploader?.status), writerTone.value)
    const server: LinkTone = ping.value?.service ? 'healthy' : 'unknown'
    const engine = linkState(server, (writer.value?.dlqTotal || 0) > 0 ? 'warning' : 'healthy')
    const tones: Record<string, LinkTone> = {
      collector,
      storage,
      uploader,
      server,
      dispatch: writerTone.value,
      engine
    }
    return MODULE_DEFS.map((item) => ({ ...item, tone: tones[item.id] || 'unknown' }))
  })

  const logLabels = computed(() => {
    const axis = (trend.value?.timeAxis || []).map((item) => toHourLabel(item)).filter(Boolean)
    if (axis.length) return axis
    return (logStats.value?.levelTrend || []).map((item) => toHourLabel(item.time)).filter(Boolean)
  })

  const logCounts = computed(() => alignLogCounts(logLabels.value, logStats.value?.levelTrend))
  const logEmpty = computed(
    () => loaded.value && !unavailable.value && logCounts.value.every((item) => item == null)
  )

  const severities = computed(() =>
    SEVERITIES.map((item) => {
      const group = alarmGroups.value.find(
        (row) =>
          String(row.groupKey || '').toUpperCase() === item.key || row.groupName === item.name
      )
      return { ...item, count: group?.count ?? 0 }
    })
  )

  const chartText = { color: '#5c6b8a', fontSize: 11 }

  const deviceOption = computed<EChartsOption>(() => ({
    tooltip: { trigger: 'item' },
    series: [
      {
        type: 'pie',
        radius: ['48%', '72%'],
        label: { color: '#243056', fontSize: 11 },
        data: typeGroups.value.map((item) => ({
          name: item.typeName || '未知',
          value: item.deviceCount || 0
        }))
      }
    ]
  }))

  const resourceOption = computed<EChartsOption>(() => ({
    grid: { left: 64, right: 16, top: 12, bottom: 20 },
    xAxis: { type: 'value', max: 100, axisLabel: chartText, splitLine: { show: false } },
    yAxis: {
      type: 'category',
      data: ['日志剩余', '驱动可用'],
      axisLabel: chartText
    },
    series: [
      {
        type: 'bar',
        barWidth: 10,
        data: [
          { value: Math.round(logRemainRate.value), itemStyle: { color: '#6d7dff' } },
          { value: Math.round(driverRate.value), itemStyle: { color: '#2fbf9a' } }
        ]
      }
    ]
  }))

  const logTypeOption = computed<EChartsOption>(() => ({
    tooltip: { trigger: 'item' },
    series: [
      {
        type: 'pie',
        radius: ['46%', '70%'],
        label: { color: '#243056', fontSize: 11 },
        data: (logStats.value?.typeRatio || []).map((item) => ({
          name: LOG_TYPE_NAME[item.type || ''] || item.type || '未知',
          value: item.count || 0
        }))
      }
    ]
  }))

  const trendOption = computed<EChartsOption>(() => {
    const collect = trend.value?.series?.find((item) => item.metricKey === 'collectCount')
    const alarm = trend.value?.series?.find((item) => item.metricKey === 'alarmCount')
    return {
      grid: { left: 36, right: 16, top: 16, bottom: 24 },
      tooltip: { trigger: 'axis' },
      xAxis: {
        type: 'category',
        data: (trend.value?.timeAxis || []).map((item) => toHourLabel(item)),
        axisLabel: chartText
      },
      yAxis: {
        type: 'value',
        axisLabel: chartText,
        splitLine: { lineStyle: { color: '#e3e9f5' } }
      },
      series: [
        {
          name: '采集',
          type: 'line',
          smooth: true,
          showSymbol: false,
          data: collect?.data || [],
          color: '#6d7dff'
        },
        {
          name: '告警',
          type: 'line',
          smooth: true,
          showSymbol: false,
          data: alarm?.data || [],
          color: '#f0a04b'
        }
      ]
    }
  })

  const logOption = computed<EChartsOption>(() => ({
    grid: { left: 36, right: 12, top: 12, bottom: 24 },
    xAxis: { type: 'category', data: logLabels.value, axisLabel: chartText },
    yAxis: { type: 'value', axisLabel: chartText, splitLine: { lineStyle: { color: '#e3e9f5' } } },
    series: [
      {
        type: 'line',
        smooth: true,
        showSymbol: false,
        data: logCounts.value,
        color: '#7c6cf0',
        areaStyle: { color: 'rgba(124, 108, 240, 0.16)' }
      }
    ]
  }))

  const alarmOption = computed<EChartsOption>(() => ({
    tooltip: { trigger: 'item' },
    series: [
      {
        type: 'pie',
        radius: ['52%', '74%'],
        label: { show: false },
        data: severities.value.map((item) => ({
          name: item.name,
          value: item.count,
          itemStyle: { color: item.color }
        }))
      }
    ]
  }))

  const go = (path: string) => router.push(path)

  const exitScreen = () => {
    if (window.history.length > 1) {
      router.back()
      return
    }
    router.push('/device-monitor/dashboard')
  }

  const statusText = (item: ModuleCard) => {
    if (item.tone === 'warning') return item.warningText
    if (item.tone === 'critical') return '中断'
    if (item.tone === 'healthy') return '正常'
    return '未知'
  }

  const detailText = (item: ModuleCard) => {
    if (item.tone === 'warning' || item.tone === 'critical') return item.detailWarning
    if (item.tone === 'healthy') return item.detailHealthy
    return '未知'
  }

  const openModule = async (item: ModuleCard) => {
    activeModule.value = item
    await nextTick()
    closeRef.value?.focus()
  }

  const closeModule = () => {
    activeModule.value = null
  }

  const onKeydown = (event: KeyboardEvent) => {
    if (event.key === 'Escape') closeModule()
  }

  const clearSnapshot = () => {
    overview.value = null
    typeGroups.value = []
    logStats.value = null
    driverTotal.value = 0
    driverAvailable.value = 0
    trend.value = null
    alarmGroups.value = []
    edge.value = null
    writer.value = null
    ping.value = null
    unavailable.value = true
    loaded.value = true
  }

  const getJson = <T,>(url: string, params?: Record<string, unknown>) =>
    request.get<T>({ url, params, showErrorMessage: false })

  const refresh = async () => {
    if (refreshInFlight) return
    refreshInFlight = true
    try {
      const [
        overviewRes,
        typeRes,
        logRes,
        driverRes,
        trendRes,
        alarmRes,
        edgeRes,
        writerRes,
        pingRes
      ] = await Promise.all([
        getJson<Overview>(`${BASE}/dashboard/overview`),
        getJson<{ groups?: TypeGroup[] }>(`${BASE}/dashboard/device-type-distribution`, {
          topN: 6
        }),
        getJson<LogStats>(`${BASE}/log/stats`),
        getJson<DriverList>(`${BASE}/driver/list`, { pageNo: 1, pageSize: 20 }),
        getJson<Trend>(`${BASE}/dashboard/trend`, {
          rangeType: 'last24h',
          granularity: 'auto',
          metricKeys: ['collectCount', 'alarmCount']
        }),
        getJson<{ groups?: AlarmGroup[] }>(`${BASE}/dashboard/alarm-distribution`, {
          statRange: 'last24h',
          groupBy: 'severity',
          topN: 6,
          includeOther: true
        }),
        getJson<EdgeHealth>(`${BASE}/dashboard/edge-pipeline-health`),
        getJson<WriterHealth>(`${BASE}/module-monitor/writer`),
        getJson<PingHealth>(`${BASE}/system/ping`)
      ])

      overview.value = overviewRes
      typeGroups.value = typeRes?.groups || []
      logStats.value = logRes
      driverTotal.value = driverRes?.total || 0
      driverAvailable.value = driverRes?.availableCount || 0
      trend.value = trendRes
      alarmGroups.value = alarmRes?.groups || []
      edge.value = edgeRes
      writer.value = writerRes
      ping.value = pingRes
      unavailable.value = false
      loaded.value = true
    } catch {
      clearSnapshot()
    } finally {
      refreshInFlight = false
    }
  }

  onMounted(() => {
    clockTimer = setInterval(() => {
      now.value = new Date()
    }, 1000)
    window.addEventListener('keydown', onKeydown)
    void refresh()
    refreshTimer = setInterval(() => {
      void refresh()
    }, REFRESH_MS)
  })

  onUnmounted(() => {
    if (clockTimer) clearInterval(clockTimer)
    if (refreshTimer) clearInterval(refreshTimer)
    window.removeEventListener('keydown', onKeydown)
  })
</script>

<style scoped lang="scss">
  .screen {
    position: relative;
    box-sizing: border-box;
    display: grid;
    grid-template-rows: auto auto minmax(0, 1fr) 168px;
    gap: 8px;
    height: 100vh;
    max-height: 100vh;
    padding: 12px 16px 10px;
    overflow: hidden;
    color: #243056;
    background: linear-gradient(180deg, rgb(255 255 255 / 72%), rgb(232 239 252 / 88%)), #e7eefb;
  }

  .top,
  .top-meta,
  .kpis,
  .alarm-legend {
    display: flex;
    align-items: center;
  }

  .top {
    justify-content: space-between;
    min-width: 0;
  }

  .title {
    text-align: center;

    span {
      color: #7b88a8;
      font-size: 11px;
      letter-spacing: 0.14em;
    }

    h1 {
      margin: 0;
      font-size: 22px;
      font-weight: 600;
    }
  }

  .top-meta {
    gap: 12px;
    color: #5c6b8a;
    font-size: 13px;
  }

  .text-btn,
  .kpi,
  .module,
  .alarm-legend button,
  .dialog-close {
    font: inherit;
    cursor: pointer;
  }

  .text-btn,
  .dialog-close {
    border: 0;
    color: #3d4d73;
    background: transparent;
  }

  .kpis {
    gap: 8px;
    min-width: 0;
  }

  .kpi {
    display: flex;
    flex: 1;
    gap: 8px;
    align-items: center;
    min-width: 0;
    padding: 8px 10px;
    text-align: left;
    background: rgb(255 255 255 / 86%);
    border: 0;
    border-radius: 14px;
  }

  .kpi-icon {
    display: grid;
    flex: none;
    place-items: center;
    width: 28px;
    height: 28px;
    color: #6d7dff;
    background: transparent;
    background-color: transparent;
  }

  .kpi-label {
    color: #70809f;
    font-size: 12px;
  }

  .kpi strong {
    margin-left: auto;
    font-size: 20px;
  }

  .body {
    display: grid;
    grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.2fr);
    gap: 8px;
    min-height: 0;
  }

  .side,
  .stage {
    display: grid;
    gap: 8px;
    min-height: 0;
  }

  .side {
    grid-template-rows: auto minmax(0, 1fr) minmax(0, 1fr) minmax(0, 1fr);
  }

  .stage {
    grid-template-rows: minmax(0, 1.3fr) minmax(0, 0.7fr);
  }

  .card {
    min-width: 0;
    min-height: 0;
    padding: 8px 10px;
    background: rgb(255 255 255 / 82%);
    border-radius: 16px;
  }

  .card h3,
  .dialog h2 {
    margin: 0 0 6px;
    font-size: 14px;
  }

  .overview p,
  .dialog p,
  .empty {
    margin: 0;
    color: #5c6b8a;
    font-size: 13px;
  }

  .chart-card,
  .floating {
    display: flex;
    flex-direction: column;
  }

  .platform {
    position: relative;
    min-height: 0;
    overflow: hidden;
    background: rgb(255 255 255 / 55%);
    border-radius: 18px;

    img {
      width: 100%;
      height: 100%;
      object-fit: contain;
    }
  }

  .modules {
    position: absolute;
    inset: 8% 6%;
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 8px;
    align-content: center;
  }

  .module {
    display: flex;
    gap: 6px;
    align-items: center;
    justify-content: center;
    min-width: 0;
    padding: 8px 6px;
    color: #243056;
    background: rgb(255 255 255 / 88%);
    border: 1px solid rgb(109 125 255 / 24%);
    border-radius: 999px;
  }

  .module-state {
    display: grid;
    place-items: center;
    width: 10px;
    height: 10px;

    i {
      width: 8px;
      height: 8px;
      background: #9aa8c4;
      border-radius: 50%;
    }
  }

  .module.healthy .module-state i {
    background: #2fbf9a;
  }

  .module.warning .module-state i {
    background: #f0a04b;
    animation: pulse 1.4s ease-in-out infinite;
  }

  .module.critical .module-state i {
    background: #e15b64;
  }

  .module.unknown .module-state i {
    background: #9aa8c4;
  }

  .bottom {
    display: grid;
    grid-template-columns: 36.5% 36.5%;
    justify-content: space-between;
    min-height: 0;
  }

  .floating {
    height: 100%;
  }

  .alarm-legend {
    flex-wrap: wrap;
    gap: 8px;

    button {
      display: inline-flex;
      gap: 4px;
      align-items: center;
      padding: 0;
      color: #5c6b8a;
      background: transparent;
      border: 0;
    }

    i {
      width: 8px;
      height: 8px;
      border-radius: 50%;
    }
  }

  .dialog-mask {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    background: rgb(22 32 58 / 28%);
  }

  .dialog {
    width: min(420px, calc(100% - 32px));
    padding: 18px 18px 16px;
    background: #fff;
    border-radius: 18px;
  }

  .dialog-close {
    float: right;
  }

  @keyframes pulse {
    50% {
      opacity: 0.35;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .module.warning .module-state i {
      animation: none;
    }
  }

  @media (max-width: 900px) {
    .kpis {
      display: grid;
      grid-template-columns: repeat(3, minmax(0, 1fr));
    }

    .body,
    .bottom {
      grid-template-columns: 1fr;
    }

    .bottom {
      justify-content: stretch;
    }
  }
</style>
