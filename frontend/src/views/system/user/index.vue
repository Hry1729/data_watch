<!-- 用户管理页面 -->
<template>
  <div class="user-page art-full-height">
    <!-- 搜索栏 -->
    <UserSearch v-model="searchForm" @search="handleSearch" @reset="resetSearchParams"></UserSearch>

    <ElCard class="art-table-card">
      <!-- 表格头部 -->
      <ArtTableHeader v-model:columns="columnChecks" :loading="loading" @refresh="refreshData">
        <template #left>
          <ElSpace wrap>
            <ElButton @click="showDialog('add')" v-ripple v-auth="'system:user:create'">新增用户</ElButton>
          </ElSpace>
        </template>
      </ArtTableHeader>

      <!-- 表格 -->
      <ArtTable
        :loading="loading"
        :data="data"
        :columns="columns"
        :pagination="pagination"
        @selection-change="handleSelectionChange"
        @pagination:size-change="handleSizeChange"
        @pagination:current-change="handleCurrentChange"
      >
      </ArtTable>

      <!-- 用户弹窗 -->
      <UserDialog
        v-model:visible="dialogVisible"
        :type="dialogType"
        :user-data="currentUserData"
        @submit="handleDialogSubmit"
      />
    </ElCard>
  </div>
</template>

<script setup lang="ts">
  import ArtButtonTable from '@/components/core/forms/art-button-table/index.vue'
  import { useTable } from '@/hooks/core/useTable'
  import {
    fetchGetUserList,
    fetchUpdateUserStatus,
    fetchDeleteUser,
    fetchResetUserPassword
  } from '@/api/system-manage'
  import UserSearch from './modules/user-search.vue'
  import UserDialog from './modules/user-dialog.vue'
  import { ElButton, ElTag, ElMessageBox, ElImage } from 'element-plus'
  import { DialogType } from '@/types'
  import defaultAvatar from '@imgs/user/avatar/avatar.png'

  defineOptions({ name: 'User' })

  type UserListItem = Api.SystemManage.UserListItem

  // 弹窗相关
  const dialogType = ref<DialogType>('add')
  const dialogVisible = ref(false)
  const currentUserData = ref<Partial<UserListItem>>({})

  // 选中行
  const selectedRows = ref<UserListItem[]>([])

  // 搜索表单
  const searchForm = ref({
    userName: undefined,
    userGender: undefined,
    userPhone: undefined,
    userEmail: undefined,
    status: '1'
  })

  // 用户状态配置（账号状态）
  const USER_STATUS_CONFIG = {
    '1': { type: 'success' as const, text: '启用' },
    '2': { type: 'danger' as const, text: '禁用' }
  } as const

  /**
   * 获取用户状态配置
   */
  const getUserStatusConfig = (status: string) => {
    return (
      USER_STATUS_CONFIG[status as keyof typeof USER_STATUS_CONFIG] || {
        type: 'info' as const,
        text: '未知'
      }
    )
  }

  // 默认头像（本地资源，避免外网图加载失败导致头像空白）
  const DEFAULT_AVATAR = defaultAvatar

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
      apiFn: fetchGetUserList,
      apiParams: {
        current: 1,
        size: 20,
        ...searchForm.value
      },
      columnsFactory: () => [
        { type: 'selection' },
        { type: 'index', width: 60, label: '序号' },
        {
          prop: 'userInfo',
          label: '用户名',
          width: 280,
          formatter: (row) => {
            return h('div', { class: 'user flex-c' }, [
              h(ElImage, {
                class: 'size-9.5 rounded-md',
                src: row.avatar || DEFAULT_AVATAR,
                previewSrcList: row.avatar ? [row.avatar] : undefined,
                previewTeleported: true
              }),
              h('div', { class: 'ml-2' }, [
                h('p', { class: 'user-name' }, row.userName),
                h('p', { class: 'email text-gray-400' }, row.userEmail || '-')
              ])
            ])
          }
        },
        {
          prop: 'nickName',
          label: '昵称',
          formatter: (row) => row.nickName || '-'
        },
        {
          prop: 'userGender',
          label: '性别',
          sortable: true,
          formatter: (row) => {
            const genderMap: Record<string, string> = {
              male: '男',
              female: '女',
              unknown: '未知'
            }
            return genderMap[row.userGender] || '未知'
          }
        },
        { prop: 'userPhone', label: '手机号', formatter: (row) => row.userPhone || '-' },
        {
          prop: 'status',
          label: '状态',
          formatter: (row) => {
            const statusConfig = getUserStatusConfig(row.status)
            return h(ElTag, { type: statusConfig.type }, () => statusConfig.text)
          }
        },
        {
          prop: 'createTime',
          label: '创建日期',
          sortable: true
        },
        {
          prop: 'operation',
          label: '操作',
          width: 200,
          fixed: 'right',
          formatter: (row) =>
            h('div', { class: 'flex gap-1' }, [
              h(ArtButtonTable, {
                type: 'edit',
                onClick: () => showDialog('edit', row)
              }),
              h(
                ElButton,
                {
                  type: 'primary',
                  link: true,
                  size: 'small',
                  onClick: () => handleResetPassword(row)
                },
                () => '重置密码'
              ),
              h(ArtButtonTable, {
                type: 'delete',
                onClick: () => deleteUser(row)
              })
            ])
        }
      ]
    }
  })

  /**
   * 搜索处理
   */
  const handleSearch = (params: Api.SystemManage.UserSearchParams) => {
    replaceSearchParams(params)
    getData()
  }

  /**
   * 显示用户弹窗
   */
  const showDialog = (type: DialogType, row?: UserListItem): void => {
    dialogType.value = type
    currentUserData.value = row || {}
    nextTick(() => {
      dialogVisible.value = true
    })
  }

  /**
   * 重置密码
   */
  const handleResetPassword = (row: UserListItem): void => {
    ElMessageBox.confirm(`确定要重置用户 "${row.userName}" 的密码为 123456 吗？`, '重置密码', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'warning'
    }).then(async () => {
      try {
        await fetchResetUserPassword(row.id)
        ElMessage.success('密码已重置为 123456')
      } catch (error: any) {
        ElMessage.error(error?.message || '重置密码失败')
      }
    })
  }

  /**
   * 删除用户
   */
  const deleteUser = (row: UserListItem): void => {
    ElMessageBox.confirm(`确定要删除用户 "${row.userName}" 吗？`, '删除用户', {
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      type: 'error'
    }).then(async () => {
      try {
        await fetchDeleteUser(row.id)
        ElMessage.success('删除成功')
        refreshData()
      } catch (error: any) {
        ElMessage.error(error?.message || '删除失败')
      }
    })
  }

  /**
   * 处理弹窗提交事件
   */
  const handleDialogSubmit = async () => {
    refreshData()
  }

  /**
   * 处理表格行选择变化
   */
  const handleSelectionChange = (selection: UserListItem[]): void => {
    selectedRows.value = selection
  }
</script>
