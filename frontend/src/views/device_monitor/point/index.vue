<!-- 点位管理页 -->
<template>
  <div class="point-page art-full-height">
    <div class="page-layout" :class="{ collapsed: sidebarCollapsed }">
      <ElCard class="tree-card sidebar-card">
        <div class="tree-shell">
          <div class="tree-head">
            <div v-show="!sidebarCollapsed" class="tree-head__title">
              <p class="panel-kicker">Device Tree</p>
              <h3>设备树</h3>
            </div>
            <div class="tree-head__actions">
              <ElTag v-show="!sidebarCollapsed" effect="light" round>{{ deviceCount }} 台</ElTag>
              <button
                type="button"
                class="collapse-btn"
                :title="sidebarCollapsed ? '展开设备树' : '收起设备树'"
                @click="sidebarCollapsed = !sidebarCollapsed"
              >
                <ArtSvgIcon :icon="sidebarCollapsed ? 'ri:menu-unfold-line' : 'ri:menu-fold-line'" />
              </button>
            </div>
          </div>

          <ElInput
            v-show="!sidebarCollapsed"
            v-model="treeKeyword"
            placeholder="搜索设备或分组..."
            :prefix-icon="Search"
            clearable
            class="tree-search"
          />

          <MonitorLoadingPlaceholder
            v-if="!sidebarCollapsed && treeLoading && !deviceTree.length"
            class="tree-loading"
            title="设备树加载中"
            description="页面先打开，设备树正在补齐"
            min-height="220px"
            :rows="4"
          />

          <ElScrollbar v-else-if="!sidebarCollapsed" class="tree-scroll">
            <ElTree
              ref="treeRef"
              :data="deviceTree"
              :props="treeProps"
              node-key="key"
              default-expand-all
              highlight-current
              :expand-on-click-node="false"
              :filter-node-method="filterTreeNode"
              @node-click="handleNodeClick"
            >
              <template #default="{ data }">
                <div class="tree-node" :class="{ active: currentTreeKey === data.key }">
                  <div class="tree-node__main">
                    <span class="tree-node__icon">
                      <ElIcon v-if="data.type === 'group'"><Folder /></ElIcon>
                      <ElIcon v-else><Monitor /></ElIcon>
                    </span>
                    <div class="tree-node__text">
                      <span class="tree-node__label">{{ data.label }}</span>
                      <small v-if="data.subLabel">{{ data.subLabel }}</small>
                    </div>
                  </div>
                  <ElBadge v-if="data.type === 'group' && data.count !== undefined" :value="data.count" class="tree-node__badge" />
                </div>
              </template>
            </ElTree>
          </ElScrollbar>

          <!-- 折叠状态下的分组图标列表 -->
          <div v-show="sidebarCollapsed" class="collapsed-groups">
            <ElTooltip
              v-for="group in collapsedGroupList"
              :key="group.key"
              :content="group.label"
              placement="right"
              :show-after="300"
            >
              <div
                class="collapsed-group-item"
                :class="{ active: currentTreeKey === group.key }"
                @click="handleCollapsedGroupClick(group)"
              >
                <ElIcon><Folder /></ElIcon>
              </div>
            </ElTooltip>
          </div>
        </div>
      </ElCard>

      <ElCard class="content-card">
        <div class="content-head">
          <div>
            <p class="panel-kicker">Point Archive</p>
            <h3>点位列表</h3>
          </div>
          <div class="content-metrics">
            <div class="total-card">
              <div class="total-card__icon">
                <ArtSvgIcon icon="ri:flashlight-line" />
              </div>
              <div class="total-card__content">
                <ArtCountTo v-if="pointListLoaded" :target="pagination.total || data.length" :duration="2000" />
                <span v-else class="text-g-900 tabular-nums">—</span>
                <span class="total-card__label">点位总数</span>
              </div>
              <div class="total-card__progress"></div>
            </div>
            <ArtProgressCard
              :percentage="recentUpdateRate"
              title="近期更新比例"
              color="#18a47a"
              icon="ri:refresh-line"
              iconStyle="bg-[rgba(24,164,122,0.1)] text-[#18a47a]"
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
          class="table-search"
          @search="handleSearch"
          @reset="handleReset"
        />

        <div class="table-panel">
          <div v-if="pointLatestError" class="point-latest-state" role="alert">
            <span>当前页实时值加载失败，点位配置仍可使用</span>
            <ElButton text type="primary" @click="loadPointLatestValues">重试当前页</ElButton>
          </div>
          <div v-else-if="pointLatestLoading" class="point-latest-state" role="status">
            正在加载当前页实时值…
          </div>
          <ArtTableHeader
            v-model:columns="columnChecks"
            v-model:showSearchBar="showSearchBar"
            :loading="loading"
            :show-zebra="false"
            layout="search,refresh,size,fullscreen,columns"
            @refresh="refreshData"
          >
            <template #left>
              <ElSpace wrap>
                <ElTag effect="light" round>已选 {{ selectedRows.length }} 项</ElTag>
                <ElButton type="primary" @click="showDialog('add')" v-ripple>
                  <ElIcon><Plus /></ElIcon>
                  新增点位
                </ElButton>
                <ElButton :disabled="!selectedRows.length" @click="handleBatchDelete" v-ripple>
                  <ElIcon><Delete /></ElIcon>
                  批量删除
                </ElButton>
                <ElButton @click="handleImport" v-ripple>
                  <ElIcon><Upload /></ElIcon>
                  导入
                </ElButton>
                <ElButton @click="handleExport" v-ripple>
                  <ElIcon><Download /></ElIcon>
                  导出
                </ElButton>
              </ElSpace>
            </template>
          </ArtTableHeader>

          <ArtTable
            :loading="loading"
            :data="data"
            :columns="columns"
            :pagination="pagination"
            @selection-change="handleSelectionChange"
            @pagination:size-change="handleSizeChange"
            @pagination:current-change="handleCurrentChange"
          >
            <template #dataType="{ row }">
              <ElTag effect="light" round>{{ DATA_TYPE_MAP[row.dataType] || row.dataType }}</ElTag>
            </template>
            <template #currentValue="{ row }">
              <span class="value-badge">{{ getPointCurrentValue(row) }}</span>
            </template>
            <template #description="{ row }">
              <span class="text-secondary">{{ row.description || '-' }}</span>
            </template>
            <template #operation="{ row }">
              <div class="row-actions">
                <ElButton link type="primary" @click="showDetail(row)">详情</ElButton>
                <ElButton link type="primary" @click="showDialog('edit', row)">编辑</ElButton>
                <ElButton link type="danger" @click="deletePoint(row)">删除</ElButton>
              </div>
            </template>
          </ArtTable>
        </div>

        <PointDialog
          v-model:visible="dialogVisible"
          :type="dialogType"
          :point-data="currentPointData"
          :device-tree="deviceTree"
          :driver-options="driverOptions"
          @submit="handleDialogSubmit"
        />

        <PointDetailDrawer v-model:visible="detailVisible" :point-data="currentPointData" />
      </ElCard>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { computed, defineAsyncComponent, h, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
  import { useRoute } from 'vue-router'
  import { Search, Folder, Monitor, Plus, Upload, Download, Delete } from '@element-plus/icons-vue'
  import { ElButton, ElMessage, ElMessageBox, type ElTree } from 'element-plus'
  import ArtSearchBar from '@/components/core/forms/art-search-bar/index.vue'
  import ArtTable from '@/components/core/tables/art-table/index.vue'
  import ArtTableHeader from '@/components/core/tables/art-table-header/index.vue'
  import ArtCountTo from '@/components/core/text-effect/art-count-to/index.vue'
  import ArtSvgIcon from '@/components/core/base/art-svg-icon/index.vue'
  import ArtProgressCard from '@/components/core/cards/art-progress-card/index.vue'
  import MonitorLoadingPlaceholder from '@/components/device-monitor/MonitorLoadingPlaceholder.vue'
  import { useTable } from '@/hooks/core/useTable'
  import { saveBlobAs } from '@/utils/file/save-blob'
  import {
    fetchAddPoint,
    fetchBatchDeletePoints,
    fetchDeletePoint,
    fetchDriverOptions,
    fetchExportPoints,
    fetchImportPoints,
    fetchImportPointsFromTemplate,
    fetchPointDetail,
    fetchPointDeviceTree,
    fetchPointLatestValues,
    fetchPointList,
    fetchUpdatePoint
  } from '@/api/device-monitor'
  import type { ColumnOption } from '@/types/component'
  import { DialogType } from '@/types'
  import { mergePointLatestValues } from '@/utils/point-latest-values'

  defineOptions({ name: 'PointManage' })

  const PointDialog = defineAsyncComponent(() => import('./modules/point-dialog.vue'))
  const PointDetailDrawer = defineAsyncComponent(() => import('./modules/point-detail-drawer.vue'))

  const route = useRoute()
  const initialSelectedDeviceId = (() => {
    const rawValue = route.query.deviceId
    const normalized = Array.isArray(rawValue) ? rawValue[0] : rawValue
    const parsed = Number(normalized)
    return Number.isInteger(parsed) && parsed > 0 ? parsed : undefined
  })()
  const initialSelectedDeviceCode = (() => {
    const rawValue = route.query.deviceCode
    const normalized = Array.isArray(rawValue) ? rawValue[0] : rawValue
    return normalized ? String(normalized) : undefined
  })()

  type PointListItem = Api.DeviceMonitor.PointListItem & Record<string, any>
  type PointSearchParams = Api.DeviceMonitor.PointSearchParams
  type ImportResult = Api.DeviceMonitor.ImportResult
  type PointImportAction = 'file' | 'template' | 'download' | null

  interface TreeNode {
    key: string
    label: string
    type: 'group' | 'device'
    count?: number
    deviceId?: number
    deviceCode?: string
    groupId?: number
    subLabel?: string
    children?: TreeNode[]
  }

  const DATA_TYPE_MAP: Record<string, string> = {
    BOOL: 'BOOL',
    INT: 'INT',
    DINT: 'DINT',
    REAL: 'REAL',
    STRING: 'STRING',
    WORD: 'WORD',
    DWORD: 'DWORD'
  }

  const POINT_IMPORT_REQUIRED_COLUMNS = ['设备编号', '点位序号', '点位名称', 'IO地址', '数据类型', '驱动名称']
  const POINT_IMPORT_OPTIONAL_COLUMNS = ['是否只读', '描述', '采集周期(ms)', '是否存储']
  const POINT_IMPORT_ENUM_EXAMPLES = [
    '数据类型: BOOL / INT / DINT / REAL / STRING / WORD / DWORD',
    '是否只读、是否存储: true / false / 1 / 0 / 是 / 否'
  ]

  const treeKeyword = ref('')
  const treeRef = ref<InstanceType<typeof ElTree>>()
  const deviceTree = ref<TreeNode[]>([])
  const treeLoading = ref(false)
  const currentTreeKey = ref('')
  const showSearchBar = ref(false)
  const sidebarCollapsed = ref(false)
  const selectedDeviceId = ref<number | undefined>(initialSelectedDeviceId)
  const selectedDeviceCode = ref<string | undefined>(initialSelectedDeviceCode)
  const selectedGroupId = ref<number | undefined>()
  const currentDeviceLabel = ref(initialSelectedDeviceCode || '')
  const driverOptions = ref<{ label: string; value: string }[]>([])
  const selectedRows = ref<PointListItem[]>([])

  // 格式化设备标签显示
  const formatDeviceLabel = (label: string) => {
    if (!label) return 'ALL'
    // 如果是设备名称，提取设备类型缩写
    if (label.includes('桥吊')) return 'QC'
    if (label.includes('轨道吊')) return 'ASC'
    if (label.toUpperCase().includes('AGV')) return 'AGV'
    return label
  }

  const dialogType = ref<DialogType>('add')
  const dialogVisible = ref(false)
  const detailVisible = ref(false)
  const currentPointData = ref<Partial<PointListItem>>({})

  const searchForm = ref<PointSearchParams>({
    keyword: '',
    deviceId: initialSelectedDeviceId
  })

  const treeProps = {
    children: 'children',
    label: 'label'
  }

  const searchItems = computed(() => [
    {
      key: 'keyword',
      label: '关键字',
      type: 'input',
      props: {
        placeholder: '搜索点位名称 / IO 地址',
        clearable: true
      }
    }
  ])

  const columnsFactory = (): ColumnOption[] => [
    { type: 'selection', width: 50 },
    { type: 'index', width: 64, label: '序号' },
    { prop: 'pointName', label: '名称', minWidth: 180 },
    { prop: 'ioAddress', label: 'IO 地址', width: 148 },
    { prop: 'dataType', label: '类型', width: 92, useSlot: true },
    { prop: 'currentValue', label: '当前值', width: 120, useSlot: true },
    { prop: 'description', label: '描述', minWidth: 280, useSlot: true, showOverflowTooltip: true },
    { prop: 'updateTime', label: '更新时间', width: 168 },
    { prop: 'operation', label: '操作', width: 168, fixed: 'right', useSlot: true }
  ]

  const pointListRevision = ref(0)
  const pointListLoaded = ref(false)
  const pointLatestLoading = ref(false)
  const pointLatestError = ref(false)

  const {
    loading,
    data,
    pagination,
    columns,
    columnChecks,
    responseMeta,
    getData,
    replaceSearchParams,
    refreshData,
    refreshCreate,
    refreshRemove,
    refreshUpdate,
    handleSizeChange,
    handleCurrentChange
  } = useTable({
    core: {
      apiFn: fetchPointList,
      apiParams: {
        pageNo: 1,
        pageSize: 20,
        deviceId: initialSelectedDeviceId,
        deviceCode: initialSelectedDeviceCode
      },
      paginationKey: {
        current: 'pageNo',
        size: 'pageSize'
      },
      columnsFactory
    },
    hooks: {
      onSuccess: () => {
        pointListLoaded.value = true
        pointListRevision.value += 1
      },
      onCacheHit: () => {
        pointListLoaded.value = true
        pointListRevision.value += 1
      }
    }
  })

  const pointPageKey = computed(() => (data.value as PointListItem[]).map((row) => row.id).join(','))
  let pointLatestValueRequestVersion = 0

  const loadPointLatestValues = async () => {
    const requestVersion = ++pointLatestValueRequestVersion
    const listRevision = pointListRevision.value
    const currentRows = data.value as PointListItem[]
    const pointIds = currentRows.map((row) => row.id)
    pointLatestError.value = false
    if (!pointIds.length) {
      pointLatestLoading.value = false
      return
    }

    pointLatestLoading.value = true
    try {
      const latestValues = await fetchPointLatestValues(pointIds)
      if (requestVersion !== pointLatestValueRequestVersion || listRevision !== pointListRevision.value) return

      const merged = mergePointLatestValues(data.value as PointListItem[], pointIds, latestValues)
      if (merged) data.value = merged as typeof data.value
    } catch (error) {
      if (requestVersion === pointLatestValueRequestVersion && listRevision === pointListRevision.value) {
        pointLatestError.value = true
        console.error('加载点位当前值失败:', error)
      }
    } finally {
      if (requestVersion === pointLatestValueRequestVersion && listRevision === pointListRevision.value) {
        pointLatestLoading.value = false
      }
    }
  }

  watch([pointListRevision, pointPageKey], () => {
    void loadPointLatestValues()
  }, { immediate: true })

  onUnmounted(() => {
    pointLatestValueRequestVersion += 1
  })

  const deviceCount = computed(() => {
    // 排除"全部设备"节点（key 为 'all'），避免重复计算
    const allNode = deviceTree.value.find((node) => node.key === 'all')
    return allNode?.count || 0
  })

  // 计算近期更新比例（基于后端返回的统计信息）
  const recentUpdateRate = computed<number | null>(() => {
    if (!pointListLoaded.value) return null
    const totalCount = pagination.total
    const recentUpdateCount = (responseMeta.value?.recentUpdateCount as number) || 0
    if (!totalCount) return 0
    return Number(((recentUpdateCount / totalCount) * 100).toFixed(1))
  })

  const collapsedGroupList = computed(() => {
    return deviceTree.value
      .filter((node) => node.type === 'group')
      .map((group) => ({
        key: group.key,
        label: group.label,
        count: group.count,
        groupId: group.groupId
      }))
  })

  const filterTreeNode = (value: string, node: Record<string, any>) => {
    if (!value) return true
    const keyword = value.toLowerCase()
    return [node.label, node.deviceCode, node.subLabel].some((field) => field?.toLowerCase().includes(keyword))
  }

  const mapPointDeviceTree = (nodes: Api.DeviceMonitor.PointDeviceTreeNode[] = []) => {
    const groupNodes = nodes
      .filter((node) => (node.nodeType || 'group') === 'group')
      .map((group) => ({
        key: group.key,
        label: group.name,
        type: 'group' as const,
        count: group.count ?? group.children?.length ?? 0,
        groupId: group.groupId,
        children: (group.children || [])
          .filter((item) => (item.nodeType || 'device') === 'device')
          .map((item) => ({
            key: item.key || `device-${item.deviceId}`,
            label: item.deviceCode || item.name || '-',
            type: 'device' as const,
            deviceId: item.deviceId,
            deviceCode: item.deviceCode,
            subLabel: item.name && item.name !== item.deviceCode ? item.name : undefined
          }))
      }))

    const total = groupNodes.reduce((sum, item) => sum + (item.count || 0), 0)

    deviceTree.value = [
      {
        key: 'all',
        label: '全部设备',
        type: 'group' as const,
        count: total,
        groupId: undefined
      },
      ...groupNodes
    ]
  }

  const findNodeByDeviceCode = (deviceCode?: string) => {
    if (!deviceCode) return undefined
    for (const group of deviceTree.value) {
      const device = group.children?.find((item) => item.deviceCode === deviceCode)
      if (device) return device
    }
    return undefined
  }

  const loadDeviceTree = async () => {
    treeLoading.value = true
    try {
      const tree = await fetchPointDeviceTree()
      mapPointDeviceTree(tree)

      if (route.query.deviceCode) {
        const targetNode = findNodeByDeviceCode(String(route.query.deviceCode))
        if (targetNode) {
          const alreadySelected =
            selectedDeviceId.value === targetNode.deviceId &&
            selectedDeviceCode.value === targetNode.deviceCode

          currentTreeKey.value = targetNode.key
          currentDeviceLabel.value = targetNode.label
          nextTick(() => {
            treeRef.value?.setCurrentKey(targetNode.key)
          })
          if (!alreadySelected) {
            handleNodeClick(targetNode)
          }
        }
      }
    } catch (error) {
      console.error('加载点位设备树失败:', error)
      deviceTree.value = []
    } finally {
      treeLoading.value = false
    }
  }

  const loadDriverOptions = async () => {
    try {
      const drivers = await fetchDriverOptions()
      driverOptions.value = drivers.map((item) => ({
        label: item.driverName,
        value: item.driverName
      }))
    } catch (error) {
      console.error('加载驱动选项失败:', error)
    }
  }

  const syncQuery = () => {
    replaceSearchParams({
      keyword: searchForm.value.keyword,
      deviceId: selectedDeviceId.value,
      deviceCode: selectedDeviceCode.value,
      groupId: selectedGroupId.value
    })
    getData()
  }

  const handleSearch = () => {
    syncQuery()
  }

  const handleReset = () => {
    searchForm.value = {
      keyword: '',
      deviceId: selectedDeviceId.value
    }
    syncQuery()
  }

  const handleNodeClick = (node: TreeNode) => {
    currentTreeKey.value = node.key

    if (node.type === 'device') {
      // 点击设备节点：按设备筛选
      selectedDeviceId.value = node.deviceId
      selectedDeviceCode.value = node.deviceCode
      selectedGroupId.value = undefined
      currentDeviceLabel.value = node.label
    } else {
      // 点击分组节点
      selectedDeviceId.value = undefined
      selectedDeviceCode.value = undefined
      // 点击"全部设备"时清空筛选，否则按分组筛选
      selectedGroupId.value = node.key === 'all' ? undefined : node.groupId
      currentDeviceLabel.value = node.label
    }

    syncQuery()
  }

  const handleCollapsedGroupClick = (group: { key: string; label: string; groupId?: number }) => {
    currentTreeKey.value = group.key
    selectedDeviceId.value = undefined
    selectedDeviceCode.value = undefined
    // 点击"全部设备"时清空筛选，否则按分组筛选
    selectedGroupId.value = group.key === 'all' ? undefined : group.groupId
    currentDeviceLabel.value = group.label
    syncQuery()
  }

  const handleSelectionChange = (selection: PointListItem[]) => {
    selectedRows.value = selection
  }

  const getPointCurrentValue = (row: PointListItem) => {
    return row.currentValue ?? row.value ?? row.lastValue ?? '--'
  }

  const showDialog = (type: DialogType, row?: PointListItem) => {
    dialogType.value = type
    currentPointData.value = row
      ? { ...row }
      : {
          deviceId: selectedDeviceId.value,
          deviceCode: selectedDeviceCode.value || '',
          varSeq: 1,
          pointName: '',
          ioAddress: '',
          dataType: 'INT',
          description: '',
          isReadonly: true,
          driverName: ''
        }

    nextTick(() => {
      dialogVisible.value = true
    })
  }

  const showDetail = async (row: PointListItem) => {
    try {
      const detail = await fetchPointDetail(row)
      currentPointData.value = {
        ...detail,
        currentValue: getPointCurrentValue(detail as PointListItem)
      }
      detailVisible.value = true
    } catch {
      ElMessage.error('获取点位详情失败')
    }
  }

  const downloadPointWorkbook = async (template: boolean) => {
    const blob = await fetchExportPoints(
      {
        pageNo: 1,
        pageSize: 10000,
        keyword: searchForm.value.keyword,
        deviceId: selectedDeviceId.value,
        deviceCode: selectedDeviceCode.value
      },
      template
    )

    const filename = template
      ? `点位导入模板_${new Date().toLocaleDateString()}.xlsx`
      : `点位列表_${new Date().toLocaleDateString()}.xlsx`
    await saveBlobAs(blob, filename)
  }

  const renderImportGuide = () =>
    h('div', { class: 'import-guide' }, [
      h('p', { class: 'import-guide__intro' }, '仅支持 .xlsx，按行导入，失败行不会影响成功行。'),
      h(
        'p',
        { class: 'import-guide__intro' },
        selectedDeviceId.value
          ? `当前已选设备：${currentDeviceLabel.value || selectedDeviceCode.value}。如需“从模板导入”，将按该设备类型从 MySQL 模板点位追加未存在的点位。`
          : '如需“从模板导入”，请先在左侧设备树中选中目标设备。'
      ),
      h('p', { class: 'import-guide__title' }, '必填列'),
      h(
        'ul',
        { class: 'import-guide__list' },
        POINT_IMPORT_REQUIRED_COLUMNS.map(item => h('li', item))
      ),
      h('p', { class: 'import-guide__title' }, '可选列'),
      h(
        'ul',
        { class: 'import-guide__list' },
        POINT_IMPORT_OPTIONAL_COLUMNS.map(item => h('li', item))
      ),
      h('p', { class: 'import-guide__title' }, '枚举示例'),
      h(
        'ul',
        { class: 'import-guide__list' },
        POINT_IMPORT_ENUM_EXAMPLES.map(item => h('li', item))
      )
    ])

  const choosePointImportAction = () =>
    new Promise<PointImportAction>((resolve) => {
      let resolved = false
      const settle = (action: PointImportAction) => {
        if (resolved) return
        resolved = true
        ElMessageBox.close()
        resolve(action)
      }

      void ElMessageBox({
        title: '点位导入说明',
        message: h('div', { class: 'import-guide' }, [
          renderImportGuide(),
          h('div', { class: 'import-guide__actions' }, [
            h(
              ElButton,
              {
                onClick: () => settle('download')
              },
              () => '下载模板'
            ),
            h(
              ElButton,
              {
                type: 'primary',
                plain: true,
                disabled: !selectedDeviceId.value,
                onClick: () => settle('template')
              },
              () => '从模板导入'
            ),
            h(
              ElButton,
              {
                type: 'primary',
                onClick: () => settle('file')
              },
              () => '继续导入'
            )
          ])
        ]),
        showConfirmButton: false,
        showCancelButton: false,
        closeOnClickModal: false,
        callback: () => {
          if (!resolved) {
            resolve(null)
          }
        }
      })
    })

  const openPointImportFilePicker = () => {
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = '.xlsx'
    input.onchange = async (event: Event) => {
      const target = event.target as HTMLInputElement
      const file = target.files?.[0]
      if (!file) return

      try {
        const result = await fetchImportPoints(file)
        showImportResult(result)
        refreshCreate()
        loadDeviceTree()
      } catch (error) {
        ElMessage.error(error instanceof Error ? error.message : '导入失败')
      }
    }
    input.click()
  }

  const importPointsFromTemplate = async () => {
    if (!selectedDeviceId.value) {
      ElMessage.warning('请先在左侧设备树选择目标设备')
      return
    }

    const result = await fetchImportPointsFromTemplate(selectedDeviceId.value)
    showImportResult(result)
    refreshCreate()
    loadDeviceTree()
  }

  const showImportResult = (result: ImportResult) => {
    if (!result.fail) {
      ElMessage.success(`导入成功 ${result.success} 条`)
      return
    }

    void ElMessageBox.alert(
      h('div', { class: 'import-guide' }, [
        h('p', { class: 'import-guide__intro' }, `成功 ${result.success} 条，失败 ${result.fail} 条。`),
        h('p', { class: 'import-guide__title' }, '失败原因（最多显示 10 条）'),
        h(
          'ul',
          { class: 'import-guide__list' },
          (result.errors || []).slice(0, 10).map(item => h('li', `第 ${item.rowNumber} 行：${item.reason}`))
        )
      ]),
      '导入结果',
      {
        confirmButtonText: '知道了'
      }
    )
  }

  const handleImport = async () => {
    const action = await choosePointImportAction()
    try {
      if (action === 'download') {
        try {
          await downloadPointWorkbook(true)
        } catch (error) {
          ElMessage.error(error instanceof Error ? error.message : '模板下载失败')
        }
        return
      }

      if (action === 'template') {
        await importPointsFromTemplate()
        return
      }

      if (action === 'file') {
        openPointImportFilePicker()
      }
    } catch (error) {
      if (action === 'template') {
        ElMessage.error(error instanceof Error ? error.message : '模板导入失败')
      } else if (action === 'file') {
        ElMessage.error(error instanceof Error ? error.message : '导入失败')
      }
    }
  }

  const handleExport = async () => {
    try {
      await downloadPointWorkbook(false)
    } catch (error) {
      ElMessage.error(error instanceof Error ? error.message : '导出失败')
    }
  }

  const deletePoint = (row: PointListItem) => {
    ElMessageBox.confirm(`确定要删除点位 ${row.pointName} 吗？`, '删除确认', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
    }).then(async () => {
      try {
        await fetchDeletePoint(row)
        ElMessage.success('删除成功')
        selectedRows.value = []
        refreshRemove()
        loadDeviceTree()
      } catch {
        ElMessage.error('删除失败')
      }
    })
  }

  const handleBatchDelete = () => {
    if (!selectedRows.value.length) return

    ElMessageBox.confirm(`确定删除选中的 ${selectedRows.value.length} 个点位吗？`, '批量删除', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    }).then(async () => {
      try {
        await fetchBatchDeletePoints(selectedRows.value.map((item) => item.id))
        ElMessage.success('批量删除成功')
        selectedRows.value = []
        refreshRemove()
        loadDeviceTree()
      } catch {
        ElMessage.error('批量删除失败')
      }
    })
  }

  const handleDialogSubmit = async (formData: Api.DeviceMonitor.PointForm) => {
    try {
      if (dialogType.value === 'add') {
        await fetchAddPoint(formData)
        ElMessage.success('新增成功')
        refreshCreate()
      } else {
        await fetchUpdatePoint(formData)
        ElMessage.success('编辑成功')
        refreshUpdate()
      }

      dialogVisible.value = false
      currentPointData.value = {}
      syncQuery()
    } catch {
      ElMessage.error(dialogType.value === 'add' ? '新增失败' : '编辑失败')
    }
  }

  watch(treeKeyword, (value) => {
    treeRef.value?.filter(value)
  })

  onMounted(() => {
    loadDeviceTree()
    loadDriverOptions()
  })
</script>

<style scoped lang="scss">
  .point-page {
    --panel-gap: 16px;
    min-height: 0;

    :deep(.art-search-bar) {
      margin-bottom: 0;
    }
  }

  .page-layout {
    display: grid;
    grid-template-columns: 308px minmax(0, 1fr);
    gap: var(--panel-gap);
    height: 100%;
    min-height: 0;
    align-items: stretch;

    &.collapsed {
      grid-template-columns: 72px minmax(0, 1fr);

      .sidebar-card {
        :deep(.el-card__body) {
          padding: 18px 10px;
        }
      }

      .tree-head {
        justify-content: center;
      }
    }
  }

  .sidebar-card,
  .content-card {
    min-height: 0;
    height: 100%;
    border: 1px solid var(--art-card-border);
    border-radius: 22px;
    overflow: hidden;
  }

  .sidebar-card :deep(.el-card__body),
  .content-card :deep(.el-card__body) {
    display: flex;
    flex-direction: column;
    height: 100%;
    min-height: 0;
    padding: 20px;
    overflow: hidden;
  }

  .table-search {
    margin-bottom: 8px;
  }

  .table-panel {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-height: 0;
  }

  .point-latest-state {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 8px;
    color: var(--el-text-color-secondary);
    font-size: 13px;
  }

  .tree-shell {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-height: 0;
    gap: 14px;
  }

  .panel-kicker {
    margin: 0;
    font-size: 12px;
    font-weight: 600;
    letter-spacing: 0.14em;
    text-transform: uppercase;
    color: #6b7a90;
  }

  .tree-head,
  .content-head {
    display: flex;
    gap: 20px;
    align-items: center;
    justify-content: space-between;
    padding: 4px 2px;

    h3 {
      margin: 6px 0 0;
      font-size: 22px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }
  }

  .tree-head {
    margin-bottom: 4px;
  }

  .content-head {
    margin-bottom: 0;
  }

  .tree-head__title {
    padding: 14px 0 0 4px;
  }

  .tree-head__actions {
    display: flex;
    gap: 10px;
    align-items: center;

    :deep(.el-tag) {
      background: var(--el-fill-color-light);
      border-color: var(--el-border-color-light);
      color: var(--el-text-color-regular);
    }
  }

  .collapse-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 32px;
    height: 32px;
    padding: 0;
    border: 1px solid var(--el-border-color-light);
    border-radius: 10px;
    background: var(--default-box-color);
    color: var(--el-text-color-regular);
    cursor: pointer;
    transition:
      color 0.2s ease,
      border-color 0.2s ease,
      background-color 0.2s ease;

    &:hover {
      border-color: var(--el-color-primary-light-5);
      background: var(--el-fill-color-light);
      color: var(--el-text-color-primary);
    }
  }

  .tree-search {
    :deep(.el-input__wrapper) {
      min-height: 44px;
      border-radius: 14px;
      box-shadow: 0 0 0 1px #edf0f5 inset;
    }
  }

  .tree-scroll {
    flex: 1;
    min-height: 0;
  }

  .tree-node {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    width: 100%;
    padding: 8px 6px 8px 2px;
    border-radius: 14px;
  }

  .tree-node__main {
    display: flex;
    flex: 1;
    gap: 10px;
    align-items: flex-start;
    min-width: 0;
  }

  .tree-node__icon {
    display: flex;
    flex-shrink: 0;
    align-items: center;
    justify-content: center;
    width: 18px;
    margin-top: 1px;
    color: #6f7b8d;
  }

  .tree-node__text {
    display: flex;
    flex: 1;
    flex-direction: column;
    min-width: 0;
    gap: 2px;

    small {
      overflow: hidden;
      font-size: 12px;
      color: #8b95a7;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
  }

  .tree-node__label {
    overflow: hidden;
    font-size: 14px;
    font-weight: 500;
    color: var(--el-text-color-primary);
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .tree-node__badge {
    :deep(.el-badge__content) {
      border: 0;
      background: var(--el-color-primary-light-9);
      color: #4a6cf7;
    }
  }

  .sidebar-card {
    :deep(.el-tree) {
      background: transparent;
    }

    :deep(.el-tree-node__content) {
      height: auto;
      padding: 4px 6px;
      border-radius: 14px;
    }

    :deep(.el-tree-node.is-current > .el-tree-node__content) {
      background: linear-gradient(90deg, rgba(74, 108, 247, 0.12) 0%, rgba(74, 108, 247, 0.04) 100%);
    }

    :deep(.el-tree-node:focus > .el-tree-node__content) {
      background-color: transparent;
    }
  }

  .content-card {
    :deep(.el-card__body) {
      gap: 12px;
    }
  }

  .content-metrics {
    display: flex;
    flex-wrap: nowrap;
    gap: 16px;
    justify-content: flex-end;
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

  .content-metrics :deep(.art-card) {
    min-width: 160px;
    height: 88px;
    border-radius: 12px;
  }

  .content-metrics :deep(.art-card .size-11) {
    width: 40px;
    height: 40px;
    font-size: 20px;
  }

  .content-metrics :deep(.art-card .text-2xl) {
    font-size: 18px;
  }

  .content-metrics :deep(.art-card .text-sm) {
    font-size: 12px;
  }

  .content-metrics :deep(.art-progress-card .mb-3\.5) {
    margin-bottom: 10px;
  }

  .content-metrics :deep(.art-progress-card .text-2xl) {
    text-align: right;
    font-weight: 600;
  }

  .content-metrics :deep(.art-progress-card .text-sm) {
    text-align: right;
  }

  .value-badge {
    display: inline-flex;
    min-width: 66px;
    align-items: center;
    justify-content: center;
    padding: 6px 10px;
    border-radius: 999px;
    background: var(--el-fill-color-light);
    color: var(--el-text-color-primary);
    font-weight: 600;
  }

  .text-secondary {
    color: #5f6b7c;
  }

  .row-actions {
    display: flex;
    align-items: center;
    gap: 2px;
  }

  .sidebar-card {
    :deep(.el-tree) {
      background: transparent;
    }

    :deep(.el-tree-node__content) {
      height: auto;
      padding: 4px 6px;
      border-radius: 14px;
    }

    :deep(.el-tree-node.is-current > .el-tree-node__content) {
      background: linear-gradient(90deg, rgba(74, 108, 247, 0.12) 0%, rgba(74, 108, 247, 0.04) 100%);
    }

    :deep(.el-tree-node:focus > .el-tree-node__content) {
      background-color: transparent;
    }
  }

  .collapsed-groups {
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding-top: 8px;
  }

  .collapsed-group-item {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 40px;
    height: 40px;
    margin: 0 auto;
    border-radius: 12px;
    font-size: 18px;
    color: #6f7b8d;
    cursor: pointer;
    transition: all 0.2s ease;

    &:hover {
      background: var(--el-fill-color-light);
      color: #4a6cf7;
    }

    &.active {
      background: linear-gradient(135deg, rgba(74, 108, 247, 0.15) 0%, rgba(74, 108, 247, 0.08) 100%);
      color: #4a6cf7;
    }
  }

  :global(html.dark .point-page .tree-node__badge .el-badge__content) {
    background: rgba(74, 108, 247, 0.18);
    color: #8ea2ff;
  }

  :global(html.dark .point-page .collapsed-group-item) {
    color: var(--el-text-color-regular);
  }

  :global(html.dark .point-page .collapsed-group-item:hover),
  :global(html.dark .point-page .collapsed-group-item.active) {
    background: rgba(74, 108, 247, 0.16);
    color: #8ea2ff;
  }

  :global(html.dark .point-page .value-badge) {
    background: rgba(255, 255, 255, 0.08);
    color: var(--el-text-color-primary);
  }

  :deep(.import-guide__actions) {
    display: flex;
    gap: 12px;
    justify-content: flex-end;
    margin-top: 20px;
    flex-wrap: wrap;
  }

  @media (max-width: 1360px) {
    .page-layout {
      grid-template-columns: 300px minmax(0, 1fr);
    }

    .content-head,
    .tree-head {
      flex-direction: column;
    }

    .content-metrics {
      flex-wrap: wrap;
      justify-content: flex-start;
      gap: 12px;
    }
  }

  @media (max-width: 1024px) {
    .page-layout {
      grid-template-columns: 1fr;
      height: auto;
    }

    .sidebar-card,
    .content-card {
      height: auto;
      min-height: 320px;
    }
  }
</style>
