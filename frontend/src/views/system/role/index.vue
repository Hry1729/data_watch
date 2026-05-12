<!-- 角色管理页面 -->
<template>
  <div class="art-full-height">
    <RoleSearch
      v-show="showSearchBar"
      v-model="searchForm"
      @search="handleSearch"
      @reset="resetSearchParams"
    ></RoleSearch>

    <ElCard class="art-table-card" :style="{ 'margin-top': showSearchBar ? '12px' : '0' }">
      <ArtTableHeader
        v-model:columns="columnChecks"
        v-model:showSearchBar="showSearchBar"
        :loading="loading"
        @refresh="refreshData"
      >
        <template #left>
          <ElSpace wrap>
            <ElButton @click="showDialog('add')" v-ripple v-auth="'system:role:create'">新增角色</ElButton>
          </ElSpace>
        </template>
      </ArtTableHeader>

      <!-- 表格 -->
      <ArtTable
        :loading="loading"
        :data="data"
        :columns="columns"
        :pagination="pagination"
        @pagination:size-change="handleSizeChange"
        @pagination:current-change="handleCurrentChange"
      >
      </ArtTable>
    </ElCard>

    <!-- 角色编辑弹窗 -->
    <RoleEditDialog
      v-model="dialogVisible"
      :dialog-type="dialogType"
      :role-data="currentRoleData"
      @success="refreshData"
    />

    <!-- 菜单权限弹窗 -->
    <RolePermissionDialog
      v-model="permissionDialog"
      :role-data="currentRoleData"
      @success="refreshData"
    />
  </div>
</template>

<script setup lang="ts">
  import { ButtonMoreItem } from '@/components/core/forms/art-button-more/index.vue'
  import { useTable } from '@/hooks/core/useTable'
  import { fetchGetRoleList, fetchDeleteRole, fetchUpdateRoleStatus } from '@/api/system-manage'
  import ArtButtonMore from '@/components/core/forms/art-button-more/index.vue'
  import RoleSearch from './modules/role-search.vue'
  import RoleEditDialog from './modules/role-edit-dialog.vue'
  import RolePermissionDialog from './modules/role-permission-dialog.vue'
  import { ElTag, ElMessageBox } from 'element-plus'

  defineOptions({ name: 'Role' })

  type RoleListItem = Api.SystemManage.RoleListItem
  type RoleSearchFormParams = Api.SystemManage.RoleSearchParams & {
    daterange?: string[]
  }

  // 搜索表单
  const searchForm = ref<RoleSearchFormParams>({
    roleName: undefined,
    roleCode: undefined,
    description: undefined,
    enabled: undefined,
    daterange: undefined
  })

  const showSearchBar = ref(false)

  const dialogVisible = ref(false)
  const permissionDialog = ref(false)
  const currentRoleData = ref<RoleListItem | undefined>(undefined)

  const {
    columns,
    columnChecks,
    data,
    loading,
    pagination,
    getData,
    replaceSearchParams,
    resetSearchParams,
    handleSizeChange,
    handleCurrentChange,
    refreshData
  } = useTable({
    core: {
      apiFn: fetchGetRoleList,
      apiParams: {
        current: 1,
        size: 20
      },
      excludeParams: ['daterange'],
      columnsFactory: () => [
        {
          prop: 'roleId',
          label: '角色ID',
          width: 100
        },
        {
          prop: 'roleName',
          label: '角色名称',
          minWidth: 120
        },
        {
          prop: 'roleCode',
          label: '角色编码',
          minWidth: 120
        },
        {
          prop: 'description',
          label: '角色描述',
          minWidth: 150,
          showOverflowTooltip: true,
          formatter: (row) => row.description || '-'
        },
        {
          prop: 'enabled',
          label: '状态',
          width: 100,
          formatter: (row) => {
            const statusConfig = row.enabled
              ? { type: 'success', text: '启用' }
              : { type: 'danger', text: '禁用' }
            return h(
              ElTag,
              { type: statusConfig.type as 'success' | 'danger' },
              () => statusConfig.text
            )
          }
        },
        {
          prop: 'createTime',
          label: '创建日期',
          width: 180,
          sortable: true
        },
        {
          prop: 'operation',
          label: '操作',
          width: 80,
          fixed: 'right',
          formatter: (row) =>
            h('div', [
              h(ArtButtonMore, {
                list: getActionList(row),
                onClick: (item: ButtonMoreItem) => buttonMoreClick(item, row)
              })
            ])
        }
      ]
    }
  })

  /**
   * 获取操作按钮列表
   */
  const getActionList = (row: RoleListItem): ButtonMoreItem[] => {
    const isSuperRole = row.roleCode === 'R_SUPER'
    return [
      {
        key: 'permission',
        label: '菜单权限',
        icon: 'ri:user-3-line'
      },
      {
        key: 'edit',
        label: '编辑角色',
        icon: 'ri:edit-2-line'
      },
      {
        key: 'toggleStatus',
        label: row.enabled ? '禁用角色' : '启用角色',
        icon: row.enabled ? 'ri:pause-circle-line' : 'ri:play-circle-line',
        disabled: isSuperRole && row.enabled
      },
      {
        key: 'delete',
        label: '删除角色',
        icon: 'ri:delete-bin-4-line',
        color: '#f56c6c',
        disabled: isSuperRole
      }
    ]
  }

  const dialogType = ref<'add' | 'edit'>('add')

  const showDialog = (type: 'add' | 'edit', row?: RoleListItem) => {
    dialogVisible.value = true
    dialogType.value = type
    currentRoleData.value = row
  }

  /**
   * 搜索处理
   */
  const handleSearch = (params: RoleSearchFormParams) => {
    const { daterange, ...filtersParams } = params
    const [startTime, endTime] = Array.isArray(daterange) ? daterange : [null, null]

    replaceSearchParams({ ...filtersParams, startTime, endTime })
    getData()
  }

  const buttonMoreClick = (item: ButtonMoreItem, row: RoleListItem) => {
    switch (item.key) {
      case 'permission':
        showPermissionDialog(row)
        break
      case 'edit':
        showDialog('edit', row)
        break
      case 'delete':
        deleteRole(row)
        break
      case 'toggleStatus':
        toggleRoleStatus(row)
        break
    }
  }

  const showPermissionDialog = (row?: RoleListItem) => {
    permissionDialog.value = true
    currentRoleData.value = row
  }

  const deleteRole = (row: RoleListItem) => {
    if (row.roleCode === 'R_SUPER') {
      ElMessage.warning('超级管理员角色不能删除')
      return
    }

    ElMessageBox.confirm(`确定删除角色"${row.roleName}"吗？`, '删除确认', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
      .then(async () => {
        try {
          await fetchDeleteRole(row.roleId)
          ElMessage.success('删除成功')
          refreshData()
        } catch (error: any) {
          ElMessage.error(error?.message || '删除失败')
        }
      })
      .catch(() => {
        // 用户取消
      })
  }

  const toggleRoleStatus = (row: RoleListItem) => {
    if (row.roleCode === 'R_SUPER' && row.enabled) {
      ElMessage.warning('超级管理员角色不能禁用')
      return
    }

    const nextEnabled = !row.enabled
    const actionText = nextEnabled ? '启用' : '禁用'
    ElMessageBox.confirm(`确定${actionText}角色"${row.roleName}"吗？`, `${actionText}确认`, {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    })
      .then(async () => {
        try {
          await fetchUpdateRoleStatus(row.roleId, nextEnabled)
          ElMessage.success(`${actionText}成功`)
          refreshData()
        } catch (error: any) {
          ElMessage.error(error?.message || `${actionText}失败`)
        }
      })
      .catch(() => {
        // 用户取消
      })
  }
</script>
