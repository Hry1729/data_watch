<!-- 菜单管理页面 -->
<template>
  <div class="menu-page art-full-height">
    <ElCard class="art-table-card">
      <!-- 表格头部 -->
      <ArtTableHeader :loading="loading" @refresh="loadMenuList">
        <template #left>
          <ElSpace wrap>
            <ElButton v-auth="'add'" @click="handleAddRootMenu" v-ripple>添加菜单</ElButton>
            <ElButton @click="toggleExpand" v-ripple>{{ isExpanded ? '收起' : '展开' }}</ElButton>
          </ElSpace>
        </template>
      </ArtTableHeader>

      <ArtTable
        ref="tableRef"
        rowKey="id"
        :loading="loading"
        :columns="columns"
        :data="menuTree"
        :stripe="false"
        :tree-props="{ children: 'children', hasChildren: 'hasChildren' }"
        :default-expand-all="false"
      />

      <!-- 菜单弹窗 -->
      <MenuDialog
        v-model:visible="dialogVisible"
        :type="dialogType"
        :editData="editData"
        :parentMenuId="parentMenuId"
        @submit="handleSubmit"
      />

      <!-- 权限弹窗 -->
      <PermissionDialog
        v-model:visible="permissionDialogVisible"
        :menuId="currentMenuId"
        :editData="editPermissionData"
        @submit="handlePermissionSubmit"
      />
    </ElCard>
  </div>
</template>

<script setup lang="ts">
  import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
  import { useTableColumns } from '@/hooks/core/useTableColumns'
  import MenuDialog from './modules/menu-dialog.vue'
  import PermissionDialog from './modules/permission-dialog.vue'
  import {
    fetchGetAdminMenus,
    fetchDeleteMenu,
    fetchDeleteMenuPermission
  } from '@/api/system-manage'
  import { ElButton, ElTag, ElMessageBox } from 'element-plus'

  defineOptions({ name: 'Menus' })

  // 状态管理
  const loading = ref(false)
  const isExpanded = ref(false)
  const tableRef = ref()

  // 弹窗相关
  const dialogVisible = ref(false)
  const dialogType = ref<'add' | 'edit'>('add')
  const editData = ref<Api.SystemManage.SystemMenuTreeItem | null>(null)
  const parentMenuId = ref<number | undefined>(undefined)

  // 权限弹窗相关
  const permissionDialogVisible = ref(false)
  const currentMenuId = ref<number>(0)
  const editPermissionData = ref<Api.SystemManage.PermissionItem | null>(null)

  // 菜单树数据
  const menuTree = ref<Api.SystemManage.SystemMenuTreeItem[]>([])

  onMounted(() => {
    loadMenuList()
  })

  /**
   * 加载菜单列表
   */
  const loadMenuList = async () => {
    loading.value = true
    try {
      menuTree.value = await fetchGetAdminMenus()
    } catch (error) {
      console.error('加载菜单失败:', error)
      ElMessage.error('加载菜单失败')
    } finally {
      loading.value = false
    }
  }

  /**
   * 获取菜单类型标签
   */
  const getMenuTypeTag = (
    row: Api.SystemManage.SystemMenuTreeItem
  ): 'primary' | 'success' | 'warning' | 'info' | 'danger' => {
    if (row.menuType === 'directory') return 'info'
    if (row.menuType === 'menu') return 'primary'
    if (row.menuType === 'button') return 'danger'
    return 'info'
  }

  /**
   * 获取菜单类型文本
   */
  const getMenuTypeText = (row: Api.SystemManage.SystemMenuTreeItem): string => {
    const typeMap: Record<string, string> = {
      directory: '目录',
      menu: '菜单',
      button: '按钮'
    }
    return typeMap[row.menuType || 'menu'] || '菜单'
  }

  // 表格列配置
  const { columnChecks, columns } = useTableColumns(() => [
    {
      prop: 'meta.title',
      label: '菜单名称',
      minWidth: 180,
      formatter: (row: Api.SystemManage.SystemMenuTreeItem) => row.meta?.title || row.name
    },
    {
      prop: 'menuType',
      label: '类型',
      width: 80,
      formatter: (row: Api.SystemManage.SystemMenuTreeItem) => {
        return h(ElTag, { type: getMenuTypeTag(row), size: 'small' }, () => getMenuTypeText(row))
      }
    },
    {
      prop: 'path',
      label: '路由',
      minWidth: 120,
      formatter: (row: Api.SystemManage.SystemMenuTreeItem) => row.path || '-'
    },
    {
      prop: 'component',
      label: '组件',
      minWidth: 140,
      formatter: (row: Api.SystemManage.SystemMenuTreeItem) => row.component || '-'
    },
    {
      prop: 'sort',
      label: '排序',
      width: 80,
      formatter: (row: Api.SystemManage.SystemMenuTreeItem) => row.sort || 1
    },
    {
      prop: 'enabled',
      label: '状态',
      width: 80,
      formatter: (row: Api.SystemManage.SystemMenuTreeItem) => {
        return h(
          ElTag,
          { type: row.enabled ? 'success' : 'danger', size: 'small' },
          () => (row.enabled ? '启用' : '禁用')
        )
      }
    },
    {
      prop: 'meta.authList',
      label: '按钮权限',
      minWidth: 180,
      formatter: (row: Api.SystemManage.SystemMenuTreeItem) => {
        const permissions = row.meta?.authList || []
        if (permissions.length === 0) return '-'
        return h(
          'div',
          { class: 'flex flex-wrap gap-1' },
          permissions.map((permission) =>
            h(
              ElTag,
              {
                type: permission.enabled ? 'success' : 'info',
                size: 'small',
                closable: true,
                class: 'cursor-pointer',
                onClick: () => handleEditPermission(row, permission),
                onClose: (event: MouseEvent) => {
                  event.stopPropagation()
                  handleDeletePermission(row, permission)
                }
              },
              () => permission.title || permission.authMark
            )
          )
        )
      }
    },
    {
      prop: 'operation',
      label: '操作',
      width: 220,
      fixed: 'right',
      formatter: (row: Api.SystemManage.SystemMenuTreeItem) => {
        return h('div', { class: 'flex gap-1' }, [
          h(ArtButtonTable, {
            type: 'add',
            onClick: () => handleAddChildMenu(row),
            title: '新增子菜单'
          }),
          h(
            ElButton,
            {
              type: 'primary',
              link: true,
              size: 'small',
              onClick: () => handleAddPermission(row)
            },
            () => '权限'
          ),
          h(ArtButtonTable, {
            type: 'edit',
            onClick: () => handleEditMenu(row)
          }),
          h(ArtButtonTable, {
            type: 'delete',
            onClick: () => handleDeleteMenu(row)
          })
        ])
      }
    }
  ])

  /**
   * 添加根菜单
   */
  const handleAddRootMenu = () => {
    dialogType.value = 'add'
    editData.value = null
    parentMenuId.value = undefined
    dialogVisible.value = true
  }

  /**
   * 添加子菜单
   */
  const handleAddChildMenu = (row: Api.SystemManage.SystemMenuTreeItem) => {
    dialogType.value = 'add'
    editData.value = null
    parentMenuId.value = row.id
    dialogVisible.value = true
  }

  /**
   * 编辑菜单
   */
  const handleEditMenu = (row: Api.SystemManage.SystemMenuTreeItem) => {
    dialogType.value = 'edit'
    editData.value = row
    parentMenuId.value = row.parentId
    dialogVisible.value = true
  }

  /**
   * 删除菜单
   */
  const handleDeleteMenu = async (row: Api.SystemManage.SystemMenuTreeItem) => {
    try {
      await ElMessageBox.confirm(`确定删除菜单"${row.meta?.title || row.name}"吗？`, '删除确认', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      })

      await fetchDeleteMenu(row.id)
      ElMessage.success('删除成功')
      loadMenuList()
    } catch (error: any) {
      if (error !== 'cancel') {
        ElMessage.error(error?.message || '删除失败')
      }
    }
  }

  /**
   * 添加权限
   */
  const handleAddPermission = (row: Api.SystemManage.SystemMenuTreeItem) => {
    currentMenuId.value = row.id
    editPermissionData.value = null
    permissionDialogVisible.value = true
  }

  const handleEditPermission = (
    row: Api.SystemManage.SystemMenuTreeItem,
    permission: Api.SystemManage.PermissionItem
  ) => {
    currentMenuId.value = row.id
    editPermissionData.value = permission
    permissionDialogVisible.value = true
  }

  const handleDeletePermission = async (
    row: Api.SystemManage.SystemMenuTreeItem,
    permission: Api.SystemManage.PermissionItem
  ) => {
    try {
      await ElMessageBox.confirm(`确定删除权限"${permission.title}"吗？`, '删除确认', {
        confirmButtonText: '确定',
        cancelButtonText: '取消',
        type: 'warning'
      })

      await fetchDeleteMenuPermission(row.id, permission.id)
      ElMessage.success('删除成功')
      loadMenuList()
    } catch (error: any) {
      if (error !== 'cancel') {
        ElMessage.error(error?.message || '删除失败')
      }
    }
  }

  /**
   * 处理菜单表单提交
   */
  const handleSubmit = () => {
    loadMenuList()
  }

  /**
   * 处理权限表单提交
   */
  const handlePermissionSubmit = () => {
    loadMenuList()
  }

  /**
   * 切换展开/收起
   */
  const toggleExpand = () => {
    isExpanded.value = !isExpanded.value
    nextTick(() => {
      if (tableRef.value?.elTableRef && menuTree.value) {
        const processRows = (rows: Api.SystemManage.SystemMenuTreeItem[]) => {
          rows.forEach((row) => {
            if (row.children?.length) {
              tableRef.value.elTableRef.toggleRowExpansion(row, isExpanded.value)
              processRows(row.children)
            }
          })
        }
        processRows(menuTree.value)
      }
    })
  }
</script>
