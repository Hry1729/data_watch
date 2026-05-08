<!-- 驱动管理页 -->
<template>
  <div class="driver-page art-full-height">
    <ElCard class="panel-card">
      <div class="panel-head">
        <div>
          <p class="panel-kicker">Driver Center</p>
          <h3>驱动列表</h3>
        </div>
        <div class="panel-summary">
          <div class="total-card">
            <div class="total-card__icon">
              <ArtSvgIcon icon="ri:cpu-line" />
            </div>
            <div class="total-card__content">
              <ArtCountTo :target="pagination.total || data.length" :duration="2000" />
              <span class="total-card__label">驱动总数</span>
            </div>
            <div class="total-card__progress"></div>
          </div>
          <ArtProgressCard
            :percentage="availableRate"
            title="可用比例"
            color="#18a47a"
            icon="ri:checkbox-circle-line"
            :iconStyle="'bg-[rgba(24,164,122,0.1)] text-[#18a47a]'"
          />
        </div>
      </div>

      <ArtSearchBar
        v-show="showSearchBar"
        v-model="searchForm"
        :items="searchItems"
        :default-expanded="true"
        :show-expand="false"
        :span="8"
        class="driver-search"
        @search="handleSearch"
        @reset="handleReset"
      />

      <ArtTableHeader
        v-model:columns="columnChecks"
        v-model:showSearchBar="showSearchBar"
        :loading="loading"
        layout="search,refresh,size,fullscreen,columns"
        :show-zebra="false"
        @refresh="refreshData"
      />

      <ArtTable
        :loading="loading"
        :data="data"
        :columns="columns"
        :pagination="pagination"
        @pagination:size-change="handleSizeChange"
        @pagination:current-change="handleCurrentChange"
      >
        <template #driverName="{ row }">
          <div class="driver-name-cell">
            <span class="driver-title">{{ row.driverName }}</span>
            <span class="driver-subtitle">{{ getDriverProtocol(row.driverName) }}</span>
          </div>
        </template>
        <template #readWriteType="{ row }">
          <ElTag :type="getReadWriteConfig(row.readWriteType).type" effect="light" round>
            {{ getReadWriteConfig(row.readWriteType).text }}
          </ElTag>
        </template>
        <template #status="{ row }">
          <ElTag :type="getDriverStatusConfig(row).type" effect="light" round>
            {{ getDriverStatusConfig(row).text }}
          </ElTag>
        </template>
      </ArtTable>
    </ElCard>
  </div>
</template>

<script setup lang="ts">
  import { computed } from 'vue'
  import ArtSearchBar from '@/components/core/forms/art-search-bar/index.vue'
  import ArtTable from '@/components/core/tables/art-table/index.vue'
  import ArtTableHeader from '@/components/core/tables/art-table-header/index.vue'
  import ArtCountTo from '@/components/core/text-effect/art-count-to/index.vue'
  import ArtSvgIcon from '@/components/core/base/art-svg-icon/index.vue'
  import ArtProgressCard from '@/components/core/cards/art-progress-card/index.vue'
  import { useTable } from '@/hooks/core/useTable'
  import { fetchDriverList } from '@/api/device-monitor'
  import type { ColumnOption } from '@/types/component'

  defineOptions({ name: 'DriverManage' })

  type DriverListItem = Api.DeviceMonitor.DriverListItem & Record<string, any>
  type DriverSearchParams = Api.DeviceMonitor.DriverSearchParams

  const searchForm = ref<DriverSearchParams>({
    keyword: ''
  })
  const showSearchBar = ref(false)

  const searchItems = computed(() => [
    {
      key: 'keyword',
      label: '关键字',
      type: 'input',
      props: {
        placeholder: '搜索驱动名称 / 协议描述',
        clearable: true
      }
    }
  ])

  const READ_WRITE_CONFIG: Record<string, { type: 'success' | 'warning' | 'info'; text: string }> = {
    读写: { type: 'success', text: '读写' },
    只读: { type: 'warning', text: '只读' }
  }

  const PROTOCOL_HINT: Record<string, string> = {
    Siemens: 'PLC 协议驱动',
    GE: '工业控制驱动',
    OPC: '统一接入驱动',
    Modbus: '边缘采集驱动',
    MQTT: '轻量消息驱动',
    IEC: '桥接转换驱动'
  }

  const getReadWriteConfig = (value: string) => {
    return READ_WRITE_CONFIG[value] || {
      type: 'info' as const,
      text: value || '未知'
    }
  }

  const getDriverProtocol = (name: string) => {
    const hit = Object.entries(PROTOCOL_HINT).find(([keyword]) => name?.includes(keyword))
    return hit?.[1] || '协议配置项'
  }

  const getDriverStatusConfig = (row: DriverListItem) => {
    const raw = String(row.status || row.availableStatus || '').toLowerCase()
    const fallbackText = `${row.driverDesc || ''} ${row.driverName || ''}`.toLowerCase()

    if (raw.includes('unused') || raw.includes('idle') || raw.includes('未使用')) {
      return { type: 'info' as const, text: '未使用' }
    }

    if (
      ['error', 'offline', 'abnormal', 'exception', '异常'].some((keyword) => raw.includes(keyword)) ||
      ['离线', '异常', '风险'].some((keyword) => fallbackText.includes(keyword.toLowerCase()))
    ) {
      return { type: 'danger' as const, text: '异常' }
    }

    return { type: 'success' as const, text: '可用' }
  }

  const columnsFactory = (): ColumnOption[] => [
    { type: 'index', width: 64, label: '序号' },
    { prop: 'driverName', label: '驱动名称', minWidth: 100, useSlot: true },
    { prop: 'driverDesc', label: '驱动描述', minWidth: 360, showOverflowTooltip: true },
    { prop: 'readWriteType', label: '读写类型', width: 110, useSlot: true },
    { prop: 'status', label: '状态', width: 96, useSlot: true },
    { prop: 'updateTime', label: '更新日期', width: 168 }
  ]

  const {
    loading,
    data,
    pagination,
    columns,
    columnChecks,
    responseMeta,
    handleSizeChange,
    handleCurrentChange,
    replaceSearchParams,
    resetSearchParams,
    getData,
    refreshData
  } = useTable({
    core: {
      apiFn: fetchDriverList,
      apiParams: {
        pageNo: 1,
        pageSize: 20,
        keyword: ''
      },
      paginationKey: {
        current: 'pageNo',
        size: 'pageSize'
      },
      columnsFactory
    }
  })

  // 计算可用比例（基于后端返回的统计信息）
  const availableRate = computed(() => {
    const totalCount = pagination.total
    const availableCount = (responseMeta.value?.availableCount as number) || 0
    if (!totalCount) return 0
    return Number(((availableCount / totalCount) * 100).toFixed(1))
  })

  const handleSearch = (params: DriverSearchParams) => {
    replaceSearchParams({ keyword: params.keyword })
    getData()
  }

  const handleReset = () => {
    searchForm.value.keyword = ''
    resetSearchParams()
  }
</script>

<style scoped lang="scss">
  .driver-page {
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

  .panel-kicker {
    font-size: 12px;
    font-weight: 600;
    letter-spacing: 0.14em;
    text-transform: uppercase;
    color: var(--el-text-color-secondary);
  }

  h3 {
    margin: 6px 0 0;
    font-size: 22px;
    font-weight: 600;
    color: var(--el-text-color-primary);
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
    width: 40px;
    height: 40px;
    margin-top: 4px;
    border-radius: 8px;
    background: rgba(74, 108, 247, 0.1);
    color: #4a6cf7;
    font-size: 20px;
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
    width: 40px;
    height: 40px;
    font-size: 20px;
  }

  .panel-summary :deep(.art-card .text-2xl) {
    font-size: 18px;
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

  .driver-name-cell {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  .driver-title {
    font-weight: 600;
    color: #1d2433;
  }

  .driver-subtitle {
    font-size: 12px;
    color: #8b95a7;
  }

  @media (max-width: 1280px) {
    .panel-head {
      flex-wrap: wrap;
    }

    .panel-summary {
      justify-content: flex-start;
    }
  }
</style>
