<!-- 设备管理页 -->
<template>
  <div class="device-page art-full-height">
    <div class="page-layout" :class="{ collapsed: sidebarCollapsed }">
      <ElCard class="tree-card sidebar-card">
        <div class="tree-shell">
          <div class="tree-head">
            <div v-show="!sidebarCollapsed" class="tree-head__title">
              <p class="panel-kicker">Device Tree</p>
              <h3>设备树</h3>
            </div>
            <div class="tree-head__actions">
              <ElTag v-show="!sidebarCollapsed" effect="light" round>{{ deviceTreeCount }} 台</ElTag>
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
            description="页面先打开，树数据正在补齐"
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
                      <ElIcon v-else-if="data.status === 'online'" class="text-[#18a47a]"><CircleCheckFilled /></ElIcon>
                      <ElIcon v-else-if="data.status === 'unstable'" class="text-[#d18b2f]"><WarningFilled /></ElIcon>
                      <ElIcon v-else class="text-[#d44f63]"><CircleCloseFilled /></ElIcon>
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
            <p class="panel-kicker">Device Archive</p>
            <h3>设备列表</h3>
          </div>
          <div class="content-metrics">
            <div class="total-card">
              <div class="total-card__icon">
                <ArtSvgIcon icon="ri:hard-drive-2-line" />
              </div>
              <div class="total-card__content">
                <ArtCountTo :target="currentScopeCount" :duration="2000" />
                <span class="total-card__label">当前范围</span>
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
              :percentage="abnormalRate"
              title="异常比例"
              color="#d44f63"
              icon="ri:wifi-off-line"
              iconStyle="bg-[rgba(212,79,99,0.1)] text-[#d44f63]"
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
                  新增设备
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
            <template #deviceInfo="{ row }">
              <div class="device-info-cell">
                <span class="device-name">{{ row.deviceName }}</span>
                <span class="device-meta">{{ row.driverName || '未绑定驱动' }}</span>
              </div>
            </template>
            <template #deviceStatus="{ row }">
              <ElTag :type="getDeviceStatusConfig(row.deviceStatus).type" effect="light" round>
                {{ getDeviceStatusConfig(row.deviceStatus).text }}
              </ElTag>
            </template>
            <template #connectStatus="{ row }">
              <ElTag :type="getConnectStatusConfig(row.connectStatus).type" effect="light" round>
                {{ getConnectStatusConfig(row.connectStatus).text }}
              </ElTag>
            </template>
            <template #dataStatus="{ row }">
              <ElTag :type="getDataStatusConfig(row.dataStatus).type" effect="light" round>
                {{ getDataStatusConfig(row.dataStatus).text }}
              </ElTag>
            </template>
            <template #operation="{ row }">
              <div class="row-actions">
                <ElButton link type="primary" @click="showDialog('edit', row)">编辑</ElButton>
                <ElButton link type="primary" @click="goToPointDetail(row)">点位</ElButton>
                <ElButton link type="danger" @click="deleteDevice(row)">删除</ElButton>
              </div>
            </template>
          </ArtTable>
        </div>

        <DeviceDialog
          v-model:visible="dialogVisible"
          :type="dialogType"
          :device-data="currentDeviceData"
          :driver-options="driverOptions"
          @submit="handleDialogSubmit"
        />
      </ElCard>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { computed, defineAsyncComponent, h, nextTick, onMounted, ref, watch } from 'vue'
  import { useRouter } from 'vue-router'
  import {
    Search,
    Folder,
    Plus,
    Upload,
    Download,
    Delete,
    CircleCheckFilled,
    CircleCloseFilled,
    WarningFilled
  } from '@element-plus/icons-vue'
  import { ElMessage, ElMessageBox, type ElTree } from 'element-plus'
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
    fetchAddDevice,
    fetchBatchDeleteDevices,
    fetchDeleteDevice,
    fetchDeviceGroupTree,
    fetchDeviceList,
    fetchDriverOptions,
    fetchExportDevices,
    fetchImportDevices,
    fetchUpdateDevice
  } from '@/api/device-monitor'
  import type { ColumnOption } from '@/types/component'
  import { DialogType } from '@/types'

  defineOptions({ name: 'DeviceManage' })

  const DeviceDialog = defineAsyncComponent(() => import('./modules/device-dialog.vue'))

  const router = useRouter()

  type DeviceListItem = Api.DeviceMonitor.DeviceListItem
  type DeviceSearchParams = Api.DeviceMonitor.DeviceSearchParams
  type ImportResult = Api.DeviceMonitor.ImportResult

  interface TreeNode {
    key: string
    label: string
    type: 'group' | 'device'
    count?: number
    status?: Api.DeviceMonitor.ConnectStatus
    deviceId?: number
    deviceCode?: string
    deviceName?: string
    groupValue?: string
    subLabel?: string
    children?: TreeNode[]
  }

  const DEVICE_STATUS_CONFIG = {
    enabled: { type: 'success' as const, text: '启用' },
    disabled: { type: 'danger' as const, text: '停用' }
  }

  const CONNECT_STATUS_CONFIG = {
    online: { type: 'success' as const, text: '在线' },
    offline: { type: 'danger' as const, text: '离线' },
    unstable: { type: 'warning' as const, text: '异常' }
  }

  const DEVICE_IMPORT_REQUIRED_COLUMNS = ['设备类型', '设备编号', '设备名称', '关联驱动']
  const DEVICE_IMPORT_OPTIONAL_COLUMNS = ['设备状态', '描述']
  const DEVICE_IMPORT_ENUM_EXAMPLES = ['设备状态: enabled / disabled / 启用 / 停用']

  const DATA_STATUS_CONFIG = {
    normal: { type: 'success' as const, text: '正常' },
    abnormal: { type: 'warning' as const, text: '异常' },
    nodata: { type: 'info' as const, text: '无数据' }
  }

  const treeKeyword = ref('')
  const treeRef = ref<InstanceType<typeof ElTree>>()
  const deviceTree = ref<TreeNode[]>([])
  const treeLoading = ref(false)
  const currentTreeKey = ref('all')
  const showSearchBar = ref(false)
  const sidebarCollapsed = ref(false)
  const activeGroupValue = ref<string | undefined>()
  const activeGroupLabel = ref('全部设备')
  const selectedDeviceCode = ref<string | undefined>()
  const selectedRows = ref<DeviceListItem[]>([])
  const driverOptions = ref<{ label: string; value: string }[]>([])

  const dialogType = ref<DialogType>('add')
  const dialogVisible = ref(false)
  const currentDeviceData = ref<Partial<DeviceListItem>>({})

  const searchForm = ref<DeviceSearchParams>({
    keyword: '',
    deviceStatus: undefined,
    connectStatus: undefined
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
        placeholder: '搜索设备编号 / 设备名称',
        clearable: true
      }
    },
    {
      key: 'deviceStatus',
      label: '设备状态',
      type: 'select',
      props: {
        placeholder: '全部状态',
        clearable: true,
        options: [
          { label: '启用', value: 'enabled' },
          { label: '停用', value: 'disabled' }
        ]
      }
    },
    {
      key: 'connectStatus',
      label: '实时状态',
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
    }
  ])

  const getDeviceStatusConfig = (value: Api.DeviceMonitor.DeviceStatus) => {
    return DEVICE_STATUS_CONFIG[value] || DEVICE_STATUS_CONFIG.enabled
  }

  const getConnectStatusConfig = (value: Api.DeviceMonitor.ConnectStatus) => {
    return CONNECT_STATUS_CONFIG[value] || CONNECT_STATUS_CONFIG.offline
  }

  const getDataStatusConfig = (value: Api.DeviceMonitor.DataStatus) => {
    return DATA_STATUS_CONFIG[value] || DATA_STATUS_CONFIG.nodata
  }

  const columnsFactory = (): ColumnOption[] => [
    { type: 'selection', width: 50 },
    { type: 'index', width: 64, label: '序号' },
    { prop: 'deviceType', label: '类型', width: 92 },
    { prop: 'deviceCode', label: '设备编号', width: 124 },
    { prop: 'deviceInfo', label: '设备名称 / 驱动', minWidth: 260, useSlot: true },
    { prop: 'deviceStatus', label: '设备状态', width: 96, useSlot: true },
    { prop: 'connectStatus', label: '实时状态', width: 96, useSlot: true },
    { prop: 'dataStatus', label: '数据质量', width: 96, useSlot: true },
    { prop: 'dataUpdateTime', label: '更新时间', width: 168 },
    { prop: 'operation', label: '操作', width: 160, fixed: 'right', useSlot: true }
  ]

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
    refreshUpdate,
    refreshRemove,
    handleSizeChange,
    handleCurrentChange
  } = useTable({
    core: {
      apiFn: fetchDeviceList,
      apiParams: {
        pageNo: 1,
        pageSize: 20
      },
      paginationKey: {
        current: 'pageNo',
        size: 'pageSize'
      },
      columnsFactory
    }
  })

  const tableRows = computed(() => data.value as DeviceListItem[])

  const deviceTreeCount = computed(() => {
    // 排除"全部设备"节点（key 为 'all'），避免重复计算
    const allNode = deviceTree.value.find((node) => node.key === 'all')
    return allNode?.count || 0
  })

  const currentScopeText = computed(() => {
    const label = activeGroupLabel.value || 'ALL'
    // 转换设备类型名称为英文缩写
    if (label === '全部设备' || label === 'ALL') return 'ALL'
    if (label.includes('桥吊')) return 'QC'
    if (label.includes('轨道吊')) return 'ASC'
    if (label.toUpperCase().includes('AGV')) return 'AGV'
    return label
  })
  const currentScopeCount = computed(() => pagination.total || tableRows.value.length)

  // 计算在线/异常比例（基于后端返回的统计信息）
  const onlineRate = computed(() => {
    const totalCount = pagination.total
    const onlineCount = (responseMeta.value?.onlineCount as number) || 0
    if (!totalCount) return 0
    return Number(((onlineCount / totalCount) * 100).toFixed(1))
  })
  const abnormalRate = computed(() => {
    const totalCount = pagination.total
    const offlineCount = (responseMeta.value?.offlineCount as number) || 0
    if (!totalCount) return 0
    return Number(((offlineCount / totalCount) * 100).toFixed(1))
  })

  const collapsedGroupList = computed(() => {
    return deviceTree.value
      .filter((node) => node.type === 'group')
      .map((group) => ({
        key: group.key,
        label: group.label,
        groupValue: group.groupValue,
        count: group.count
      }))
  })

  const filterTreeNode = (value: string, node: Record<string, any>) => {
    if (!value) return true
    const keyword = value.toLowerCase()
    return [node.label, node.deviceCode, node.deviceName, node.subLabel].some((field) => field?.toLowerCase().includes(keyword))
  }

  const buildTreeNodes = (nodes: Api.DeviceMonitor.DeviceGroupTreeNode[] = []) => {
    const groupNodes = nodes
      .filter((item) => (item.nodeType || 'group') === 'group')
      .map((group) => {
        const children = (group.children || [])
          .filter((item) => (item.nodeType || 'device') === 'device')
          .map((item) => ({
            key: item.key || `device-${item.deviceId}`,
            label: item.deviceCode || item.deviceName || '-',
            type: 'device' as const,
            status: item.connectStatus || 'offline',
            deviceId: item.deviceId,
            deviceCode: item.deviceCode,
            deviceName: item.deviceName,
            subLabel: item.deviceName || getConnectStatusConfig(item.connectStatus || 'offline').text
          }))

        return {
          key: group.key,
          label: group.name,
          type: 'group' as const,
          groupValue: group.name,
          count: group.count ?? children.length,
          children
        }
      })

    const total = groupNodes.reduce((sum, item) => sum + (item.count || 0), 0)

    deviceTree.value = [
      {
        key: 'all',
        label: '全部设备',
        type: 'group',
        count: total
      },
      ...groupNodes
    ]
  }

  const loadDeviceGroupTree = async () => {
    treeLoading.value = true
    try {
      const tree = await fetchDeviceGroupTree()
      buildTreeNodes(tree)
      nextTick(() => {
        treeRef.value?.setCurrentKey(currentTreeKey.value)
      })
    } catch (error) {
      console.error('加载设备分组树失败:', error)
      deviceTree.value = [
        {
          key: 'all',
          label: '全部设备',
          type: 'group',
          count: 0
        }
      ]
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

  const syncTableQuery = () => {
    replaceSearchParams({
      keyword: searchForm.value.keyword,
      deviceStatus: searchForm.value.deviceStatus,
      connectStatus: searchForm.value.connectStatus,
      deviceType: activeGroupValue.value,
      deviceCode: selectedDeviceCode.value
    })
    getData()
  }

  const handleSearch = () => {
    syncTableQuery()
  }

  const handleReset = () => {
    searchForm.value = {
      keyword: '',
      deviceStatus: undefined,
      connectStatus: undefined
    }
    syncTableQuery()
  }

  const handleNodeClick = (node: TreeNode) => {
    currentTreeKey.value = node.key

    if (node.type === 'group') {
      activeGroupValue.value = node.key === 'all' ? undefined : node.groupValue || node.label
      activeGroupLabel.value = node.label
      selectedDeviceCode.value = undefined
    } else {
      activeGroupValue.value = undefined
      activeGroupLabel.value = node.label
      selectedDeviceCode.value = node.deviceCode
    }

    syncTableQuery()
  }

  const handleCollapsedGroupClick = (group: { key: string; label: string; groupValue?: string }) => {
    currentTreeKey.value = group.key
    activeGroupValue.value = group.key === 'all' ? undefined : group.groupValue || group.label
    activeGroupLabel.value = group.label
    selectedDeviceCode.value = undefined
    syncTableQuery()
  }

  const showDialog = (type: DialogType, row?: DeviceListItem) => {
    dialogType.value = type
    currentDeviceData.value = row ? { ...row } : {}
    nextTick(() => {
      dialogVisible.value = true
    })
  }

  const deleteDevice = (row: DeviceListItem) => {
    ElMessageBox.confirm(`确定要删除设备 ${row.deviceName} 吗？`, '删除确认', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    }).then(async () => {
      try {
        await fetchDeleteDevice(row.id)
        ElMessage.success('删除成功')
        refreshRemove()
        loadDeviceGroupTree()
      } catch {
        ElMessage.error('删除失败')
      }
    })
  }

  const handleBatchDelete = () => {
    if (!selectedRows.value.length) return

    ElMessageBox.confirm(`确定删除选中的 ${selectedRows.value.length} 台设备吗？`, '批量删除', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    }).then(async () => {
      try {
        await fetchBatchDeleteDevices(selectedRows.value.map((item) => item.id))
        ElMessage.success('批量删除成功')
        selectedRows.value = []
        refreshRemove()
        loadDeviceGroupTree()
      } catch {
        ElMessage.error('批量删除失败')
      }
    })
  }

  const goToPointDetail = (row: DeviceListItem) => {
    router.push({
      path: '/device-monitor/point',
      query: {
        deviceId: row.id,
        deviceCode: row.deviceCode
      }
    })
  }

  const downloadDeviceWorkbook = async (template: boolean) => {
    const blob = await fetchExportDevices(
      {
        pageNo: 1,
        pageSize: 10000,
        keyword: searchForm.value.keyword,
        deviceStatus: searchForm.value.deviceStatus,
        connectStatus: searchForm.value.connectStatus,
        deviceType: activeGroupValue.value,
        deviceCode: selectedDeviceCode.value
      },
      template
    )

    const filename = template
      ? `设备导入模板_${new Date().toLocaleDateString()}.xlsx`
      : `设备列表_${new Date().toLocaleDateString()}.xlsx`
    await saveBlobAs(blob, filename)
  }

  const renderImportGuide = () =>
    h('div', { class: 'import-guide' }, [
      h('p', { class: 'import-guide__intro' }, '仅支持 .xlsx，按行导入，失败行不会影响成功行。'),
      h('p', { class: 'import-guide__title' }, '必填列'),
      h(
        'ul',
        { class: 'import-guide__list' },
        DEVICE_IMPORT_REQUIRED_COLUMNS.map(item => h('li', item))
      ),
      h('p', { class: 'import-guide__title' }, '可选列'),
      h(
        'ul',
        { class: 'import-guide__list' },
        DEVICE_IMPORT_OPTIONAL_COLUMNS.map(item => h('li', item))
      ),
      h('p', { class: 'import-guide__title' }, '枚举示例'),
      h(
        'ul',
        { class: 'import-guide__list' },
        DEVICE_IMPORT_ENUM_EXAMPLES.map(item => h('li', item))
      )
    ])

  const openDeviceImportFilePicker = () => {
    const input = document.createElement('input')
    input.type = 'file'
    input.accept = '.xlsx'
    input.onchange = async (event: Event) => {
      const target = event.target as HTMLInputElement
      const file = target.files?.[0]
      if (!file) return

      try {
        const result = await fetchImportDevices(file)
        showImportResult(result)
        refreshCreate()
        loadDeviceGroupTree()
      } catch (error) {
        ElMessage.error(error instanceof Error ? error.message : '导入失败')
      }
    }
    input.click()
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
    try {
      await ElMessageBox({
        title: '设备导入说明',
        message: renderImportGuide(),
        showCancelButton: true,
        distinguishCancelAndClose: true,
        confirmButtonText: '继续导入',
        cancelButtonText: '下载模板',
        closeOnClickModal: false
      })
      openDeviceImportFilePicker()
    } catch (action) {
      if (action === 'cancel') {
        try {
          await downloadDeviceWorkbook(true)
        } catch (error) {
          ElMessage.error(error instanceof Error ? error.message : '模板下载失败')
        }
      }
    }
  }

  const handleExport = async () => {
    try {
      await downloadDeviceWorkbook(false)
    } catch (error) {
      ElMessage.error(error instanceof Error ? error.message : '导出失败')
    }
  }

  const handleDialogSubmit = async (formData: Api.DeviceMonitor.DeviceForm) => {
    try {
      if (dialogType.value === 'add') {
        await fetchAddDevice(formData)
        ElMessage.success('新增成功')
        refreshCreate()
      } else {
        await fetchUpdateDevice(formData)
        ElMessage.success('编辑成功')
        refreshUpdate()
      }

      dialogVisible.value = false
      currentDeviceData.value = {}
      loadDeviceGroupTree()
    } catch {
      ElMessage.error(dialogType.value === 'add' ? '新增失败' : '编辑失败')
    }
  }

  const handleSelectionChange = (selection: DeviceListItem[]) => {
    selectedRows.value = selection
  }

  watch(treeKeyword, (value) => {
    treeRef.value?.filter(value)
  })

  onMounted(() => {
    loadDeviceGroupTree()
    loadDriverOptions()
  })
</script>

<style scoped lang="scss">
  .device-page {
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
      color: #1d2433;
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
  }

  .collapse-btn {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 32px;
    height: 32px;
    padding: 0;
    border: 1px solid #e7ebf3;
    border-radius: 10px;
    background: #fff;
    color: #66758f;
    cursor: pointer;
    transition:
      color 0.2s ease,
      border-color 0.2s ease,
      background-color 0.2s ease;

    &:hover {
      border-color: #d5ddeb;
      background: #f7f9fc;
      color: #1d2433;
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
    color: #243041;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .tree-node__badge {
    :deep(.el-badge__content) {
      border: 0;
      background: #edf2ff;
      color: #4a6cf7;
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

  // 进度卡片间距和对齐
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

  .device-info-cell {
    display: flex;
    flex-direction: column;
    gap: 4px;
  }

  .device-name {
    font-weight: 600;
    color: #1d2433;
  }

  .device-meta {
    font-size: 12px;
    color: #8b95a7;
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
      background: linear-gradient(90deg, rgba(74, 108, 247, 0.08) 0%, rgba(74, 108, 247, 0.02) 100%);
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
      background: #f0f3f8;
      color: #4a6cf7;
    }

    &.active {
      background: linear-gradient(135deg, rgba(74, 108, 247, 0.1) 0%, rgba(74, 108, 247, 0.04) 100%);
      color: #4a6cf7;
    }
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
