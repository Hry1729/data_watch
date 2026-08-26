<!-- 设备监控卡片视图 -->
<template>
  <div class="device-monitor-page art-full-height">
    <ElCard class="panel-card">
      <!-- 页面标题和统计卡片区 -->
      <div class="panel-head">
        <div class="head-left">
          <p class="panel-kicker">Device Monitor</p>
          <h3>设备通讯</h3>
        </div>
        <div class="panel-summary">
          <div class="total-card">
            <div class="total-card__icon">
              <ArtSvgIcon icon="ri:hard-drive-3-line" />
            </div>
            <div class="total-card__content">
              <ArtCountTo :target="stats.total" :duration="2000" />
              <span class="total-card__label">设备总数</span>
            </div>
            <div class="total-card__progress"></div>
          </div>
          <ArtProgressCard
            :percentage="onlineRate"
            title="在线比例"
            color="#18a47a"
            icon="ri:wifi-line"
            iconStyle="bg-[rgba(24,164,122,0.1)] text-[#18a47a]"
          />
          <ArtProgressCard
            :percentage="offlineRate"
            title="离线比例"
            color="#d44f63"
            icon="ri:wifi-off-line"
            iconStyle="bg-[rgba(212,79,99,0.1)] text-[#d44f63]"
          />
          <ArtProgressCard
            :percentage="disabledRate"
            title="未用比例"
            color="#d18b2f"
            icon="ri:pause-circle-line"
            iconStyle="bg-[rgba(209,139,47,0.1)] text-[#d18b2f]"
          />

          <!-- 筛选卡（约两张统计卡宽） -->
          <div class="filter-card">
            <div class="filter-row">
              <ElSelect
                v-model="searchForm.deviceType"
                placeholder="全部类型"
                clearable
                class="filter-select"
                @change="handleSearch"
                @clear="handleSearch"
              >
                <ElOption
                  v-for="opt in deviceTypeOptions"
                  :key="opt.value"
                  :label="opt.label"
                  :value="opt.value"
                />
              </ElSelect>
              <ElSelect
                v-model="searchForm.statusFilter"
                placeholder="全部状态"
                class="filter-select"
                @change="handleSearch"
              >
                <ElOption
                  v-for="opt in statusOptions"
                  :key="opt.value"
                  :label="opt.label"
                  :value="opt.value"
                />
              </ElSelect>
            </div>
            <div class="filter-row">
              <ElInput
                v-model="searchForm.keyword"
                placeholder="编号 / 名称，回车查询"
                clearable
                class="filter-input"
                @keyup.enter="handleSearch"
                @clear="handleSearch"
              />
              <ElButton class="filter-reset" @click="handleReset">重置</ElButton>
            </div>
          </div>
        </div>
      </div>

      <div class="card-grid-wrapper">
        <MonitorLoadingPlaceholder
          v-if="loading"
          class="grid-loading"
          title="设备列表加载中"
          description="正在获取设备数据"
          min-height="400px"
          :rows="6"
        />

        <template v-else>
          <div v-if="deviceList.length" class="device-groups">
            <section v-for="group in groupedDevices" :key="group.type" class="device-group">
              <div class="group-header">
                <span class="group-name">{{ group.type }}</span>
                <span class="group-count">{{ group.devices.length }}</span>
              </div>
              <div class="card-grid">
                <div
                  v-for="(device, index) in group.devices"
                  :key="device.id"
                  class="device-card"
                  :class="getCardClass(device)"
                  :style="{ '--breath-delay': `${(index % 8) * -0.25}s` }"
                  :title="device.deviceName || device.deviceCode"
                  @click="goToRealtime(device)"
                >
                  <span class="device-code">{{ formatDeviceCode(device.deviceCode) }}</span>
                </div>
              </div>
            </section>
          </div>

          <ElEmpty v-else description="暂无设备数据" :image-size="160" />
        </template>
      </div>
      <div v-if="listTotal > DEVICE_LIST_PAGE_SIZE" class="list-pagination">
        <ElPagination
          background
          layout="total, prev, pager, next"
          :current-page="pageNo"
          :page-size="DEVICE_LIST_PAGE_SIZE"
          :total="listTotal"
          @current-change="handlePageChange"
        />
      </div>
      <ElAlert
        v-if="statsTruncated"
        class="stats-limit-warning"
        title="设备数量超过统计安全上限，比例统计暂不计算；请缩小筛选范围。"
        type="warning"
        :closable="false"
      />
    </ElCard>
  </div>
</template>

<script setup lang="ts">
  import { computed, onMounted, onUnmounted, ref } from 'vue'
  import { useRouter } from 'vue-router'
  import ArtCountTo from '@/components/core/text-effect/art-count-to/index.vue'
  import ArtSvgIcon from '@/components/core/base/art-svg-icon/index.vue'
  import ArtProgressCard from '@/components/core/cards/art-progress-card/index.vue'
  import MonitorLoadingPlaceholder from '@/components/device-monitor/MonitorLoadingPlaceholder.vue'
  import { fetchDeviceGroupTree, fetchDeviceList } from '@/api/device-monitor'
  import { loadAllDevicePages } from '@/utils/device-pagination'

  defineOptions({ name: 'DeviceMonitor' })

  const router = useRouter()

  type DeviceListItem = Api.DeviceMonitor.DeviceListItem
  type StatusParams = Partial<Pick<Api.DeviceMonitor.DeviceSearchParams, 'deviceStatus' | 'connectStatus'>>

  const loading           = ref(false)
  const deviceList        = ref<DeviceListItem[]>([])
  const deviceTypeOptions = ref<{ label: string; value: string }[]>([])
  const pageNo            = ref(1)
  const listTotal         = ref(0)
  const statsTruncated     = ref(false)
  const DEVICE_LIST_PAGE_SIZE = 100

  // 统计数据
  const stats = ref({ total: 0, online: 0, unstable: 0, offline: 0, disabled: 0 })

  // 计算百分比
  const onlineRate = computed<number | null>(() => {
    if (statsTruncated.value) return null
    if (!stats.value.total) return 0
    return Number(((stats.value.online / stats.value.total) * 100).toFixed(1))
  })

  const offlineRate = computed<number | null>(() => {
    if (statsTruncated.value) return null
    if (!stats.value.total) return 0
    return Number(((stats.value.offline / stats.value.total) * 100).toFixed(1))
  })

  const disabledRate = computed<number | null>(() => {
    if (statsTruncated.value) return null
    if (!stats.value.total) return 0
    return Number(((stats.value.disabled / stats.value.total) * 100).toFixed(1))
  })

  // 按设备类型分组（每个类型独占若干行，保持后端返回的先后顺序）
  const groupedDevices = computed(() => {
    const groups = new Map<string, DeviceListItem[]>()
    for (const device of deviceList.value) {
      const type = device.deviceType || '未分类'
      const list = groups.get(type)
      if (list) list.push(device)
      else groups.set(type, [device])
    }
    return Array.from(groups, ([type, devices]) => ({ type, devices }))
  })

  // 筛选表单：statusFilter 为 UI 组合状态，查询时映射成 deviceStatus + connectStatus
  const searchForm = ref<{ keyword: string; deviceType?: string; statusFilter: string }>({
    keyword: '',
    deviceType: undefined,
    statusFilter: ''
  })

  const statusOptions = [
    { label: '全部', value: '' },
    { label: '在线', value: 'online' },
    { label: '离线', value: 'offline' },
    { label: '异常', value: 'unstable' },
    { label: '未启用', value: 'disabled' }
  ]

  // 组合状态 → 请求参数（在线/离线/异常限定 enabled；未启用不限 connectStatus）
  const STATUS_FILTER_MAP: Record<string, StatusParams> = {
    online:   { deviceStatus: 'enabled',  connectStatus: 'online' },
    offline:  { deviceStatus: 'enabled',  connectStatus: 'offline' },
    unstable: { deviceStatus: 'enabled',  connectStatus: 'unstable' },
    disabled: { deviceStatus: 'disabled' }
  }

  // 卡片编号显示：去掉类型前缀；轨道吊(asc)四位编号末位 1→L、2→S（如 asc7011 → 701L）
  const formatDeviceCode = (code: string) => {
    if (!code) return '-'
    const match = code.match(/^([A-Za-z]+)?(\d+)$/)
    if (!match) return code
    const prefix = (match[1] || '').toLowerCase()
    const num = match[2]
    if (prefix === 'asc' && num.length === 4) {
      const last = num[3]
      if (last === '1') return `${num.slice(0, 3)}L`
      if (last === '2') return `${num.slice(0, 3)}S`
    }
    return num
  }

  // 卡片状态 CSS 类名（决定颜色主题）
  const getCardClass = (device: DeviceListItem) => {
    if (device.deviceStatus === 'disabled') return 'status-disabled'
    const map: Record<string, string> = {
      online:   'status-online',
      offline:  'status-offline',
      unstable: 'status-unstable'
    }
    return map[device.connectStatus] || 'status-offline'
  }

  // 加载设备类型选项
  const loadDeviceTypeOptions = async () => {
    try {
      const tree = await fetchDeviceGroupTree()
      deviceTypeOptions.value = (tree || []).map((node) => ({
        label: node.name,
        value: node.name
      }))
    } catch (error) {
      console.error('加载设备类型失败:', error)
    }
  }

  // 加载当前页设备列表；统计与选择器使用独立的全量分页加载。
  let listRequestVersion = 0
  const loadDeviceList = async (silent = false) => {
    const requestVersion = ++listRequestVersion
    if (!silent) loading.value = true
    try {
      const statusParams = STATUS_FILTER_MAP[searchForm.value.statusFilter] ?? {}
      const result = await fetchDeviceList({
         pageNo: pageNo.value,
         pageSize: DEVICE_LIST_PAGE_SIZE,
        keyword: searchForm.value.keyword.trim() || undefined,
        deviceType: searchForm.value.deviceType,
        deviceStatus: statusParams.deviceStatus,
        connectStatus: statusParams.connectStatus
      })
      if (requestVersion === listRequestVersion) {
        deviceList.value = result.list || []
        listTotal.value = result.total || 0
      }
    } catch (error) {
      console.error('加载设备列表失败:', error)
      if (!silent && requestVersion === listRequestVersion) {
        deviceList.value = []
      }
    } finally {
      if (!silent && requestVersion === listRequestVersion) loading.value = false
    }
  }

  // 加载统计数据（始终全量，不受筛选影响）
  let statsRequestVersion = 0
  const loadStats = async () => {
    const requestVersion = ++statsRequestVersion
    try {
      const allResult = await loadAllDevicePages((page, size) => fetchDeviceList({ pageNo: page, pageSize: size }))
      if (requestVersion !== statsRequestVersion) return
      const all = allResult.list || []
      stats.value.total    = allResult.total || all.length
      stats.value.online   = all.filter((d) => d.connectStatus === 'online').length
      stats.value.offline  = all.filter((d) => d.connectStatus === 'offline').length
      stats.value.unstable = all.filter((d) => d.connectStatus === 'unstable').length
      stats.value.disabled = all.filter((d) => d.deviceStatus === 'disabled').length
      statsTruncated.value = allResult.truncated
    } catch (error) {
      console.error('加载统计数据失败:', error)
    }
  }

  const handleSearch = () => { pageNo.value = 1; loadDeviceList() }
  const handleReset  = () => {
    searchForm.value = { keyword: '', deviceType: undefined, statusFilter: '' }
    pageNo.value = 1
    loadDeviceList()
  }
  const handlePageChange = (value: number) => { pageNo.value = value; loadDeviceList() }

  // 跳转到实时监控
  const goToRealtime = (device: DeviceListItem) => {
    router.push({ path: '/device-monitor/realtime', query: { deviceId: device.id } })
  }

  const REFRESH_INTERVAL = 15000  // 15s 定时静默刷新设备状态
  let refreshTimer: ReturnType<typeof setInterval> | null = null
  let refreshInFlight = false

  const refreshMonitor = async (silent: boolean) => {
    if (refreshInFlight) return
    refreshInFlight = true
    try {
      await Promise.all([loadDeviceList(silent), loadStats()])
    } finally {
      refreshInFlight = false
    }
  }

  onMounted(() => {
    loadDeviceTypeOptions()
    void refreshMonitor(false)
    refreshTimer = setInterval(() => {
      void refreshMonitor(true)
    }, REFRESH_INTERVAL)
  })

  onUnmounted(() => {
    listRequestVersion += 1
    statsRequestVersion += 1
    if (refreshTimer) {
      clearInterval(refreshTimer)
      refreshTimer = null
    }
  })
</script>

<style scoped lang="scss">
  .device-monitor-page {
    display: flex;
    flex-direction: column;
    min-height: 100%;
  }

  .panel-card {
    flex: 1;
    display: flex;
    flex-direction: column;
    border: 1px solid var(--art-card-border);
    border-radius: 22px;

    :deep(.el-card__body) {
      display: flex;
      flex: 1;
      flex-direction: column;
      min-height: 0;
      padding: 24px;
    }
  }

  .panel-head {
    display: flex;
    gap: 20px;
    align-items: center;
    justify-content: space-between;
    padding: 4px 2px;
    margin-bottom: 16px;
  }

  .head-left { flex-shrink: 0; }

  h3 {
    margin: 6px 0 0;
    font-size: 22px;
    font-weight: 600;
    color: var(--el-text-color-primary);
  }

  .panel-kicker {
    font-size: 12px;
    font-weight: 600;
    letter-spacing: 0.14em;
    text-transform: uppercase;
    color: var(--el-text-color-secondary);
  }

  /* 右上角筛选控件：无卡片外壳，顶/底与统计卡对齐 */
  .filter-card {
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: 14px;
    width: 336px;
    height: 88px;
  }

  .filter-row {
    display: flex;
    align-items: center;
    gap: 8px;
  }

  .filter-select,
  .filter-input {
    flex: 1;
    min-width: 0;
  }

  .filter-reset {
    flex-shrink: 0;
  }

  /* 筛选控件圆角与左侧卡片一致（12px） */
  .filter-card :deep(.el-select__wrapper),
  .filter-card :deep(.el-input__wrapper),
  .filter-card :deep(.el-button) {
    border-radius: 12px;
  }

  .panel-summary {
    display: flex;
    gap: 16px;
    flex-wrap: wrap;
    justify-content: flex-end;
    align-self: center;
  }

  .total-card {
    position: relative;
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    min-width: 160px;
    height: 88px;
    padding: 12px 20px;
    background: var(--default-box-color);
    border: 1px solid var(--art-card-border);
    border-radius: 12px;
  }

  .total-card__icon {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 38px;
    height: 38px;
    margin-top: 5px;
    border-radius: 8px;
    background: rgba(74, 108, 247, 0.1);
    color: #4a6cf7;
    font-size: 19px;
  }

  .total-card__content {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 4px;
  }

  .total-card__content :deep(span.text-g-900) {
    font-size: 18px !important;
    font-weight: 900 !important;
    color: var(--el-text-color-primary) !important;
  }

  .total-card__label {
    font-size: 12px !important;
    color: var(--el-text-color-secondary) !important;
    margin-top: -4px;
  }

  .total-card__progress {
    position: absolute;
    bottom: 10px;
    left: 20px;
    right: 20px;
    height: 5px;
    border-radius: 5px;
    background: linear-gradient(90deg, rgba(74, 108, 247, 0.1) 0%, rgba(74, 108, 247, 0.2) 100%);
  }

  .panel-summary :deep(.art-card) {
    min-width: 160px;
    height: 88px;
    border-radius: 12px;
  }

  .panel-summary :deep(.art-card .size-11) {
    width: 36px;
    height: 36px;
    font-size: 18px;
  }

  .panel-summary :deep(.art-card .text-2xl) {
    font-size: 18px;
  }

  .panel-summary :deep(.art-card .text-lg) {
    font-size: 14px;
  }

  .panel-summary :deep(.art-card .text-sm) {
    font-size: 12px;
  }

  // 进度卡片间距和对齐
  .panel-summary :deep(.art-progress-card .mb-3\.5) {
    margin-bottom: 10px;
  }

  .panel-summary :deep(.art-progress-card .text-2xl) {
    text-align: right;
    font-weight: 600;
  }

  .panel-summary :deep(.art-progress-card .text-sm) {
    text-align: right;
  }

  /* ── 网格区域 ── */
  .card-grid-wrapper {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
    /* 左右留白：避免最左/最右卡片的呼吸光晕被滚动容器边缘裁剪，并与大卡左缘拉开距离 */
    padding: 6px 7px;
  }

  .grid-loading {
    border: 0;
    border-radius: 18px;
  }

  /* 按设备类型分组 */
  .device-groups {
    display: flex;
    flex-direction: column;
    gap: 20px;
  }

  .group-header {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 10px;
    padding-left: 2px;
  }

  .group-name {
    font-size: 16px;
    font-weight: 600;
    color: var(--el-text-color-primary);
  }

  .group-count {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    min-width: 20px;
    height: 18px;
    padding: 0 6px;
    font-size: 12px;
    color: var(--el-text-color-secondary);
    background: var(--el-fill-color-light);
    border-radius: 9px;
  }

  /* 小卡墙：GitHub 活动块式小方块，一行尽量多放 */
  .card-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(43px, 48px));
    justify-content: start;
    gap: 8px;
    padding: 4px 2px;   /* 为 hover 上移/光晕留出空间 */
  }

  /* ══════════════════════════════════════════
     设备小卡：仅显示设备编号，状态以颜色 + 呼吸表达
  ══════════════════════════════════════════ */
  .device-card {
    position: relative;
    display: flex;
    align-items: center;
    justify-content: center;
    height: 44px;
    padding: 3px;
    border: 1px solid var(--card-accent, var(--art-card-border));
    border-radius: 6px;
    background: var(--card-bg, var(--default-box-color));
    cursor: pointer;
    content-visibility: auto;
    contain-intrinsic-size: 44px;
    transition: transform 0.18s ease, box-shadow 0.18s ease;

    &:hover {
      transform: translateY(-2px) scale(1.06);
      box-shadow: 0 4px 12px rgba(31, 45, 61, 0.18);
      z-index: 1;
    }

    /* ── 状态配色：GitHub 活动块式实心填充 ── */
    &.status-online {
      --card-accent: #18a47a;
      --card-bg: #18a47a;
      --card-text: #fff;
    }
    &.status-offline {
      --card-accent: #d44f63;
      --card-bg: #d44f63;
      --card-text: #fff;
    }
    &.status-unstable {
      --card-accent: #d18b2f;
      --card-bg: #d18b2f;
      --card-text: #fff;
    }
    /* 未启用：浅灰实心块（accent=bg，无可见边框，与在线/离线同机制） */
    &.status-disabled {
      --card-accent: #d4d9e0;
      --card-bg: #d4d9e0;
      --card-text: #69707d;
    }
  }

  /* 未启用块：暗色模式用哑光深灰实心，沉下去表示不活跃 */
  :global(html.dark .device-card.status-disabled) {
    --card-accent: #3a414c;
    --card-bg: #3a414c;
    --card-text: rgba(255, 255, 255, 0.5);
  }

  .device-code {
    position: relative;
    z-index: 1;
    font-family: 'Roboto Mono', 'SFMono-Regular', Consolas, monospace;
    font-size: 12px;
    font-weight: 600;
    line-height: 1.2;
    text-align: center;
    word-break: break-all;
    color: var(--card-text, var(--el-text-color-primary));
  }

  /* ── 呼吸灯：仅离线红卡，整卡深红填充 + 光晕，opacity 脉动（走合成层，最轻） ── */
  /* ── 离线红卡：呼吸 = 内部变亮脉动 + 外圈阴影由小变大再消散（警示） ── */
  .device-card.status-offline {
    z-index: 1;                  /* 外圈光晕盖过相邻卡 */
    content-visibility: visible; /* 避免外圈阴影被裁剪 */
    animation: offline-halo 2s ease-in-out infinite;
    animation-delay: var(--breath-delay, 0s);
  }

  .device-card.status-offline::before {
    content: '';
    position: absolute;
    inset: 0;
    border-radius: inherit;
    background: #ff8593;         /* 比底色略亮的红，脉动时整卡轻微变亮 */
    opacity: 0;
    pointer-events: none;
    animation: offline-glow 2s ease-in-out infinite;
    animation-delay: var(--breath-delay, 0s);
  }

  /* 外圈阴影：与内部呼吸同周期同相位，50% 时扩散变深，首尾收回消失 */
  @keyframes offline-halo {
    0%, 100% { box-shadow: 0 0 0 0 rgba(178, 40, 56, 0); }
    50%      { box-shadow: 0 0 6px 2px rgba(178, 40, 56, 0.8); }
  }

  /* 内部变亮脉动（降亮，与阴影同相位） */
  @keyframes offline-glow {
    0%, 100% { opacity: 0; }
    50%      { opacity: 0.32; }
  }
</style>
