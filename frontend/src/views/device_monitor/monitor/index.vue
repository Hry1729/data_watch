<!-- 设备监控卡片视图 -->
<template>
  <div class="device-monitor-page art-full-height">
    <ElCard class="panel-card">
      <!-- 页面标题和统计卡片区 -->
      <div class="panel-head">
        <div class="head-left">
          <p class="panel-kicker">Device Monitor</p>
          <h3>设备监控</h3>
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
        </div>
      </div>

      <!-- 筛选栏 -->
      <ArtSearchBar
        v-model="searchForm"
        :items="searchItems"
        :default-expanded="true"
        :show-expand="false"
        :span="6"
        class="search-bar"
        @search="handleSearch"
        @reset="handleReset"
      />

      <div ref="gridWrapperRef" class="card-grid-wrapper">
        <MonitorLoadingPlaceholder
          v-if="loading"
          class="grid-loading"
          title="设备列表加载中"
          description="正在获取设备数据"
          min-height="400px"
          :rows="6"
        />

        <template v-else>
          <div v-if="deviceList.length" ref="cardGridRef" class="card-grid">
            <div
              v-for="device in deviceList"
              :key="device.id"
              class="device-card"
              :class="getCardClass(device)"
              @click="goToRealtime(device)"
            >
              <!-- 左侧白色内容区 -->
              <div class="card-left">
                <div class="card-title-row">
                  <span class="card-status-dot"></span>
                  <h4 class="card-device-name">{{ device.deviceName }}</h4>
                  <span v-if="device.deviceStatus === 'disabled'" class="disabled-tag">未启用</span>
                </div>
                <div class="card-meta-row driver-row">
                  <span class="meta-label">驱动</span>
                  <span class="meta-value">{{ device.driverName || '-' }}</span>
                </div>
                <div class="card-meta-row">
                  <div class="card-meta-item">
                    <span class="meta-label">状态</span>
                    <span class="meta-value status-text">{{ getConnectStatusText(device.connectStatus) }}</span>
                  </div>
                  <div class="card-meta-item">
                    <span class="meta-label">采集</span>
                    <span class="meta-value">{{ formatTime(device.dataUpdateTime) }}</span>
                  </div>
                </div>
              </div>

              <!-- 左右过渡分割线 -->
              <div class="card-divider"></div>

              <!-- 右侧渐变装饰区 -->
              <div class="card-right">
                <div class="card-icon-wrap">
                  <div class="card-device-icon">
                    <img :src="getDeviceTypeIcon(device.deviceType)" :alt="device.deviceType" />
                  </div>
                  <button class="card-enter-btn" @click.stop="goToRealtime(device)">进入监控</button>
                </div>
              </div>
            </div>
          </div>

          <ElEmpty v-else description="暂无设备数据" :image-size="160" />
        </template>
      </div>

      <div v-if="!loading && deviceList.length" class="pagination-wrapper">
        <ElPagination
          v-model:current-page="pagination.pageNo"
          :page-size="pagination.pageSize"
          :total="pagination.total"
          layout="total, prev, pager, next, jumper"
          @current-change="handlePageChange"
        />
      </div>
    </ElCard>
  </div>
</template>

<script setup lang="ts">
  import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
  import { useRouter } from 'vue-router'
  import ArtSearchBar from '@/components/core/forms/art-search-bar/index.vue'
  import ArtCountTo from '@/components/core/text-effect/art-count-to/index.vue'
  import ArtSvgIcon from '@/components/core/base/art-svg-icon/index.vue'
  import ArtProgressCard from '@/components/core/cards/art-progress-card/index.vue'
  import MonitorLoadingPlaceholder from '@/components/device-monitor/MonitorLoadingPlaceholder.vue'
  import { fetchDeviceGroupTree, fetchDeviceList } from '@/api/device-monitor'

  defineOptions({ name: 'DeviceMonitor' })

  const router = useRouter()

  type DeviceListItem = Api.DeviceMonitor.DeviceListItem
  type DeviceSearchParams = Api.DeviceMonitor.DeviceSearchParams

  // 导入设备类型图标
  const qcIcon  = new URL('@/assets/images/device-types/qc.svg',  import.meta.url).href
  const ascIcon = new URL('@/assets/images/device-types/asc.svg', import.meta.url).href
  const agvIcon = new URL('@/assets/images/device-types/agv.svg', import.meta.url).href

  const defaultIcon = agvIcon

  const deviceTypeIconMap: Record<string, string> = {
    qc:     qcIcon,
    桥吊:   qcIcon,
    asc:    ascIcon,
    轨道吊: ascIcon,
    agv:    agvIcon,
    agv小车: agvIcon
  }

  const loading      = ref(false)
  const deviceList   = ref<DeviceListItem[]>([])
  const deviceTypeOptions = ref<{ label: string; value: string }[]>([])
  const gridWrapperRef = ref<HTMLElement>()
  const cardGridRef    = ref<HTMLElement>()

  // 统计数据
  const stats = ref({ total: 0, online: 0, unstable: 0, offline: 0, disabled: 0 })

  // 计算百分比
  const onlineRate = computed(() => {
    if (!stats.value.total) return 0
    return Number(((stats.value.online / stats.value.total) * 100).toFixed(1))
  })

  const offlineRate = computed(() => {
    if (!stats.value.total) return 0
    return Number(((stats.value.offline / stats.value.total) * 100).toFixed(1))
  })

  const disabledRate = computed(() => {
    if (!stats.value.total) return 0
    return Number(((stats.value.disabled / stats.value.total) * 100).toFixed(1))
  })

  const searchForm = ref<DeviceSearchParams>({
    keyword: '',
    deviceType: undefined,
    connectStatus: undefined
  })

  const pagination = ref({ pageNo: 1, pageSize: 16, total: 0 })

  const FALLBACK_CARD_HEIGHT = 120  // 横幅卡片高度
  const FALLBACK_GRID_GAP    = 14
  const MAX_VISIBLE_ROWS     = 4    // 最大可见行数
  let resizeObserver: ResizeObserver | null = null
  let resizeFrame = 0

  const searchItems = computed(() => [
    {
      key: 'deviceType',
      label: '设备类型',
      type: 'select',
      props: {
        placeholder: '全部类型',
        clearable: true,
        options: deviceTypeOptions.value
      }
    },
    {
      key: 'connectStatus',
      label: '在线状态',
      type: 'select',
      props: {
        placeholder: '全部状态',
        clearable: true,
        options: [
          { label: '在线', value: 'online' },
          { label: '离线', value: 'offline' },
          { label: '异常', value: 'unstable' }
        ]
      }
    },
    {
      key: 'keyword',
      label: '关键字',
      type: 'input',
      props: { placeholder: '搜索设备编号 / 名称', clearable: true }
    }
  ])

  // 设备类型图标
  const getDeviceTypeIcon = (deviceType: string) => {
    const key = (deviceType || '').toLowerCase()
    return deviceTypeIconMap[key] || defaultIcon
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

  // 连接状态文字
  const getConnectStatusText = (status: Api.DeviceMonitor.ConnectStatus) => {
    const map: Record<string, string> = { online: '在线', offline: '离线', unstable: '异常' }
    return map[status] || '离线'
  }

  // 格式化时间（只显示 HH:mm:ss）
  const formatTime = (time: string) => {
    if (!time || time === '-') return '--'
    const parts = time.split(' ')
    return parts.length === 2 ? parts[1].substring(0, 8) : time
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

  // 加载设备列表
  const loadDeviceList = async () => {
    loading.value = true
    try {
      const result = await fetchDeviceList({
        pageNo:        pagination.value.pageNo,
        pageSize:      pagination.value.pageSize,
        keyword:       searchForm.value.keyword,
        deviceType:    searchForm.value.deviceType,
        connectStatus: searchForm.value.connectStatus
      })
      deviceList.value       = result.list  || []
      pagination.value.total = result.total || 0
    } catch (error) {
      console.error('加载设备列表失败:', error)
      deviceList.value       = []
      pagination.value.total = 0
    } finally {
      loading.value = false
      nextTick(() => { scheduleResponsivePagination() })
    }
  }

  // 加载统计数据
  const loadStats = async () => {
    try {
      const allResult = await fetchDeviceList({ pageNo: 1, pageSize: 1000 })
      const all = allResult.list || []
      stats.value.total    = allResult.total || all.length
      stats.value.online   = all.filter((d) => d.connectStatus === 'online').length
      stats.value.offline  = all.filter((d) => d.connectStatus === 'offline').length
      stats.value.unstable = all.filter((d) => d.connectStatus === 'unstable').length
      stats.value.disabled = all.filter((d) => d.deviceStatus === 'disabled').length
    } catch (error) {
      console.error('加载统计数据失败:', error)
    }
  }

  const handleSearch = () => { pagination.value.pageNo = 1; loadDeviceList() }
  const handleReset  = () => {
    searchForm.value = { keyword: '', deviceType: undefined, connectStatus: undefined }
    pagination.value.pageNo = 1
    loadDeviceList()
  }

  // ── 响应式分页辅助 ──────────────────────────────────
  const splitTrackList = (value: string) => {
    const tracks: string[] = []
    let depth = 0, current = ''
    for (const char of value) {
      if (char === '(') depth++
      if (char === ')') depth = Math.max(depth - 1, 0)
      if (char === ' ' && depth === 0) {
        if (current.trim()) tracks.push(current.trim())
        current = ''
        continue
      }
      current += char
    }
    if (current.trim()) tracks.push(current.trim())
    return tracks
  }

  const getResolvedColumnCount = () => {
    const grid = cardGridRef.value
    if (!grid) return 1
    const template = window.getComputedStyle(grid).gridTemplateColumns.trim()
    if (template && template !== 'none') {
      const repeatMatch = template.match(/^repeat\((\d+),/i)
      if (repeatMatch) return Number(repeatMatch[1]) || 1
      const tracks = splitTrackList(template).filter((t) => t !== 'subgrid')
      if (tracks.length > 0) return tracks.length
    }
    const cards = Array.from(grid.children).filter((item): item is HTMLElement => item instanceof HTMLElement)
    if (!cards.length) return 1
    const firstRowTop = cards[0].offsetTop
    const nextRowIdx  = cards.findIndex((card, i) => i > 0 && card.offsetTop !== firstRowTop)
    return nextRowIdx === -1 ? cards.length : nextRowIdx
  }

  const getMeasuredCardHeight = () => {
    const card = cardGridRef.value?.querySelector('.device-card') as HTMLElement | null
    return card?.offsetHeight || FALLBACK_CARD_HEIGHT
  }

  const getGridRowGap = () => {
    const grid = cardGridRef.value
    if (!grid) return FALLBACK_GRID_GAP
    const styles = window.getComputedStyle(grid)
    const gap = Number.parseFloat(styles.rowGap || styles.gap || String(FALLBACK_GRID_GAP))
    return Number.isFinite(gap) ? gap : FALLBACK_GRID_GAP
  }

  const getResponsivePageSize = () => {
    const wrapper = gridWrapperRef.value
    const grid    = cardGridRef.value
    if (!wrapper || !grid || !grid.children.length) return pagination.value.pageSize
    const columns    = getResolvedColumnCount()
    const cardHeight = getMeasuredCardHeight()
    const rowGap     = getGridRowGap()
    // 计算能放下的行数，但限制最大行数
    const calculatedRows = Math.max(1, Math.floor((wrapper.clientHeight + rowGap) / (cardHeight + rowGap)))
    const rows = Math.min(calculatedRows, MAX_VISIBLE_ROWS)
    return Math.max(columns * rows, columns)
  }

  const syncResponsivePagination = async () => {
    const nextPageSize = getResponsivePageSize()
    if (!nextPageSize || nextPageSize === pagination.value.pageSize) return
    const currentStartIndex = Math.max(0, (pagination.value.pageNo - 1) * pagination.value.pageSize)
    const maxPage = Math.max(1, Math.ceil((pagination.value.total || 0) / nextPageSize))
    pagination.value.pageSize = nextPageSize
    pagination.value.pageNo   = Math.min(Math.floor(currentStartIndex / nextPageSize) + 1, maxPage)
    await loadDeviceList()
  }

  const scheduleResponsivePagination = () => {
    if (resizeFrame) cancelAnimationFrame(resizeFrame)
    resizeFrame = requestAnimationFrame(() => { resizeFrame = 0; void syncResponsivePagination() })
  }

  const handlePageChange = (page: number) => { pagination.value.pageNo = page; loadDeviceList() }

  // 跳转到实时监控
  const goToRealtime = (device: DeviceListItem) => {
    router.push({ path: '/device-monitor/realtime', query: { deviceId: device.id } })
  }

  onMounted(() => {
    resizeObserver = new ResizeObserver(() => { scheduleResponsivePagination() })
    if (gridWrapperRef.value) resizeObserver.observe(gridWrapperRef.value)
    loadDeviceTypeOptions()
    loadDeviceList()
    loadStats()
  })

  onUnmounted(() => {
    if (resizeFrame) { cancelAnimationFrame(resizeFrame); resizeFrame = 0 }
    resizeObserver?.disconnect()
    resizeObserver = null
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
      padding: 20px;
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

  .search-bar {
    margin-bottom: 16px;
    padding: 18px 20px;
    border: 1px solid var(--art-card-border);
    border-radius: 12px;
    background: var(--default-box-color);

    :deep(.art-search-bar) { padding: 0 !important; }
    :deep(.action-buttons-wrapper) { margin-bottom: 0 !important; }
    :deep(.el-form) { margin: 0; }
    :deep(.el-row) { margin: 0; }
    :deep(.el-form-item) { margin-bottom: 0; }
    :deep(.el-input__wrapper) { padding: 1px 11px; }
    :deep(.el-select .el-input__wrapper) { padding: 1px 11px; }
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
  }

  .grid-loading {
    border: 0;
    border-radius: 18px;
  }

  /* 默认 4 列横幅网格 */
  .card-grid {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 14px;
    padding-top: 6px;   /* 为悬浮上移动画留出空间，避免被父容器裁切 */
  }

  /* ══════════════════════════════════════════
     设备横幅卡片
  ══════════════════════════════════════════ */
  .device-card {
    position: relative;
    display: flex;
    flex-direction: row;   /* 左内容区 | 分隔线 | 右装饰区 */
    align-items: stretch;
    height: 120px;
    overflow: hidden;
    border: 1px solid var(--art-card-border);
    border-radius: 16px;
    background: var(--default-box-color);
    box-shadow: 0 2px 10px rgba(31, 45, 61, 0.04);
    cursor: pointer;
    transition: box-shadow 0.22s ease, transform 0.22s ease, border-color 0.22s ease;

    &:hover {
      border-color: var(--art-gray-300);
      box-shadow: 0 3px 12px rgba(31, 45, 61, 0.08);
      transform: translateY(-2px);
    }

    /* ── 状态颜色 CSS 变量（由根元素 class 注入） ── */
    &.status-online {
      --card-accent:     #18a47a;
      --card-icon-bg:    rgba(24, 164, 122, 0.08);
      --card-dot:        #18a47a;
    }

    &.status-offline {
      --card-accent:     #d44f63;
      --card-icon-bg:    rgba(212, 79, 99, 0.08);
      --card-dot:        #d44f63;
    }

    &.status-unstable {
      --card-accent:     #d18b2f;
      --card-icon-bg:    rgba(209, 139, 47, 0.08);
      --card-dot:        #d18b2f;
    }

    /* 未启用 → 黄色 */
    &.status-disabled {
      --card-accent:     #c98a00;
      --card-icon-bg:    rgba(224, 160, 48, 0.08);
      --card-dot:        #e0a030;
    }
  }

  /* ── 左侧内容区（透明，显示卡片整体渐变） ── */
  .card-left {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
    justify-content: center;
    padding: 0 16px;
    background: transparent;
  }

  .card-title-row {
    display: flex;
    align-items: center;
    gap: 7px;
    margin-bottom: 8px;
  }

  .card-status-dot {
    flex-shrink: 0;
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: var(--card-dot, var(--el-text-color-secondary));
  }

  .card-device-name {
    flex: 1;
    min-width: 0;
    margin: 0;
    font-size: 16px;
    font-weight: 600;
    color: var(--el-text-color-primary);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .disabled-tag {
    flex-shrink: 0;
    display: inline-flex;
    align-items: center;
    height: 18px;
    padding: 0 6px;
    font-size: 11px;
    font-weight: 500;
    color: var(--card-accent, #c98a00);
    background: var(--card-icon-bg, rgba(224, 160, 48, 0.13));
    border-radius: 4px;
  }

  .card-meta-row {
    display: flex;
    align-items: center;
    gap: 14px;
    font-size: 12px;
    overflow: hidden;

    &.driver-row {
      margin-top: 4px;
      gap: 4px;

      .meta-label { color: var(--el-text-color-secondary); }
      .meta-value { color: var(--el-text-color-regular); }
    }

    &:not(.driver-row):not(:first-child) {
      margin-top: 6px;
    }
  }

  .card-meta-item {
    display: flex;
    align-items: center;
    gap: 3px;
    flex-shrink: 0;

    .meta-label { color: var(--el-text-color-secondary); }

    .meta-value {
      color: var(--el-text-color-regular);
      max-width: 80px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;

      &.status-text {
        color: var(--card-accent, var(--el-text-color-regular));
        font-weight: 500;
      }
    }
  }

  /* ── 左右分割线（渐变淡出） ── */
  .card-divider {
    flex-shrink: 0;
    width: 1px;
    background: linear-gradient(
      180deg,
      transparent 0%,
      rgba(0, 0, 0, 0.07) 25%,
      rgba(0, 0, 0, 0.07) 75%,
      transparent 100%
    );
  }

  /* ── 右侧装饰区（透明，显示卡片整体渐变） ── */
  .card-right {
    flex-shrink: 0;
    width: 110px;
    display: flex;
    align-items: center;
    justify-content: center;
    position: relative;
    overflow: hidden;
    background: transparent;
  }


  .card-icon-wrap {
    position: relative;
    z-index: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 5px;
  }

  .card-device-icon {
    width: 42px;
    height: 42px;
    border-radius: 11px;
    background: var(--card-icon-bg, rgba(150, 150, 150, 0.12));
    display: flex;
    align-items: center;
    justify-content: center;

    img {
      width: 26px;
      height: 26px;
      object-fit: contain;
    }
  }

  .card-enter-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    height: 21px;
    padding: 0 8px;
    font-size: 11px;
    font-weight: 500;
    color: var(--card-accent, #409eff);
    background: var(--el-fill-color-light);
    border: none;
    outline: none;
    border-radius: 5px;
    cursor: pointer;
    white-space: nowrap;
    transition: background 0.18s;

    &:hover { background: var(--el-fill-color); }
  }

  /* ── 分页 ── */
  .pagination-wrapper {
    display: flex;
    justify-content: flex-end;
    padding-top: 16px;
    margin-top: 12px;
    border-top: 1px solid var(--art-card-border);
  }

  /* ── 响应式断点 ── */
  @media (max-width: 1500px) {
    .card-grid { grid-template-columns: repeat(3, minmax(0, 1fr)); }
  }

  @media (max-width: 1200px) {
    .card-grid { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  }

  @media (max-width: 860px) {
    .card-grid { grid-template-columns: 1fr; }
  }
</style>
