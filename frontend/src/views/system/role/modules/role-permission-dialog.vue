<template>
  <ElDialog
    v-model="visible"
    title="菜单权限"
    width="600px"
    align-center
    class="el-dialog-border"
    @close="handleClose"
  >
    <div v-loading="loading" element-loading-text="加载中...">
      <ElScrollbar height="60vh">
        <ElTree
          ref="treeRef"
          :data="menuTree"
          show-checkbox
          node-key="key"
          :default-expand-all="true"
          :props="treeProps"
          :check-strictly="true"
        >
          <template #default="{ data }">
            <div style="display: flex; align-items: center; gap: 8px">
              <span v-if="data.isPermission" class="text-blue-500">
                <ArtSvgIcon icon="ri:key-line" />
              </span>
              <span v-else>
                <ArtSvgIcon :icon="data.icon || 'ri:folder-line'" />
              </span>
              <span>{{ data.label }}</span>
              <ElTag v-if="data.isPermission" type="info" size="small">权限</ElTag>
              <ElTag v-if="!data.enabled" type="danger" size="small">禁用</ElTag>
            </div>
          </template>
        </ElTree>
      </ElScrollbar>
    </div>
    <template #footer>
      <div class="flex justify-between w-full">
        <div>
          <ElButton @click="toggleExpandAll">{{ isExpandAll ? '全部收起' : '全部展开' }}</ElButton>
          <ElButton @click="toggleSelectAll">{{ isSelectAll ? '取消全选' : '全部选择' }}</ElButton>
        </div>
        <div>
          <ElButton @click="handleClose">取消</ElButton>
          <ElButton
            type="primary"
            :loading="saveLoading"
            :disabled="!canSave"
            @click="savePermission"
          >
            保存
          </ElButton>
        </div>
      </div>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
  import { fetchGetAdminMenus, fetchGetRoleGrant, fetchUpdateRoleGrant } from '@/api/system-manage'
  import { buildRoleGrantPayload } from '@/utils/role-grant-payload'
  import { ElMessageBox } from 'element-plus'
  import ArtSvgIcon from '@/components/core/base/art-svg-icon/index.vue'

  type RoleListItem = Api.SystemManage.RoleListItem

  interface Props {
    modelValue: boolean
    roleData?: RoleListItem
  }

  interface Emits {
    (e: 'update:modelValue', value: boolean): void
    (e: 'success'): void
  }

  const props = withDefaults(defineProps<Props>(), {
    modelValue: false,
    roleData: undefined
  })

  const emit = defineEmits<Emits>()

  const treeRef = ref()
  const loading = ref(false)
  const saveLoading = ref(false)
  const isExpandAll = ref(true)
  const isSelectAll = ref(false)

  // 原始菜单树数据
  const menuTree = ref<TreeNode[]>([])
  // 原始授权数据
  const originalGrant = ref<Api.SystemManage.RoleGrant | null>(null)
  // 授权数据成功加载时对应的角色 ID；只有它与当前角色一致时才允许保存
  const loadedRoleId = ref<number | null>(null)
  // 加载请求代号：关闭/重开/切角色后旧响应一律丢弃，防止 A 的授权写进 B
  let loadVersion = 0

  /**
   * 仅在授权已成功加载、且属于当前角色时允许保存
   */
  const canSave = computed(
    () =>
      !loading.value &&
      originalGrant.value !== null &&
      loadedRoleId.value !== null &&
      loadedRoleId.value === props.roleData?.roleId
  )

  /**
   * 树节点类型
   */
  interface TreeNode {
    key: string
    label: string
    icon?: string
    isPermission?: boolean
    enabled?: boolean
    menuId?: number
    permissionId?: number
    children?: TreeNode[]
  }

  /**
   * 弹窗显示状态双向绑定
   */
  const visible = computed({
    get: () => props.modelValue,
    set: (value) => emit('update:modelValue', value)
  })

  /**
   * 树形组件配置
   */
  const treeProps = {
    children: 'children',
    label: 'label'
  }

  /**
   * 监听弹窗打开，加载数据
   */
  watch(
    () => props.modelValue,
    async (newVal) => {
      if (newVal && props.roleData) {
        await loadData()
      }
    }
  )

  /**
   * 加载菜单树和授权数据
   */
  const loadData = async () => {
    if (!props.roleData) return

    const version = ++loadVersion
    const roleId = props.roleData.roleId
    const isCurrent = () =>
      version === loadVersion && visible.value && props.roleData?.roleId === roleId

    loading.value = true
    loadedRoleId.value = null
    originalGrant.value = null
    menuTree.value = []
    try {
      const [menus, grant] = await Promise.all([fetchGetAdminMenus(), fetchGetRoleGrant(roleId)])
      if (!isCurrent()) return

      // 构建树形数据
      menuTree.value = buildMenuTree(menus)
      originalGrant.value = grant

      // 设置选中状态
      await nextTick()
      if (!isCurrent()) return
      setCheckedKeys(grant)
      loadedRoleId.value = roleId
    } catch (error) {
      if (!isCurrent()) return
      console.error('加载数据失败:', error)
      ElMessage.error('加载数据失败')
    } finally {
      if (version === loadVersion) loading.value = false
    }
  }

  /**
   * 构建菜单树，将权限展开为子节点
   */
  const buildMenuTree = (menus: Api.SystemManage.SystemMenuTreeItem[]): TreeNode[] => {
    const processNode = (menu: Api.SystemManage.SystemMenuTreeItem): TreeNode => {
      const node: TreeNode = {
        key: `menu:${menu.id}`,
        label: menu.meta?.title || menu.name,
        icon: menu.meta?.icon,
        enabled: menu.enabled,
        menuId: menu.id,
        children: []
      }

      // 添加权限子节点
      if (menu.meta?.authList?.length) {
        const permissionNodes: TreeNode[] = menu.meta.authList
          .filter((perm) => perm.permissionType === 'action')
          .map((perm) => ({
            key: `permission:${perm.id}`,
            label: perm.title,
            isPermission: true,
            enabled: perm.enabled,
            menuId: menu.id,
            permissionId: perm.id
          }))
        node.children = [...permissionNodes]
      }

      // 递归处理子菜单
      if (menu.children?.length) {
        const childNodes = menu.children.map(processNode)
        node.children = node.children ? [...childNodes, ...node.children] : childNodes
      }

      return node
    }

    return menus.map(processNode)
  }

  /**
   * 设置选中状态
   */
  const setCheckedKeys = (grant: Api.SystemManage.RoleGrant) => {
    if (!treeRef.value) return

    const checkedKeys: string[] = []

    // 添加菜单选中
    grant.menuIds.forEach((id) => {
      checkedKeys.push(`menu:${id}`)
    })

    // 添加权限选中
    grant.permissionIds.forEach((id) => {
      checkedKeys.push(`permission:${id}`)
    })

    treeRef.value.setCheckedKeys(checkedKeys)
  }

  const findNodeByKey = (nodes: TreeNode[], key: string): TreeNode | null => {
    for (const node of nodes) {
      if (node.key === key) return node
      if (node.children?.length) {
        const matched = findNodeByKey(node.children, key)
        if (matched) return matched
      }
    }
    return null
  }

  /**
   * 关闭弹窗
   */
  const handleClose = () => {
    loadVersion++
    loading.value = false
    visible.value = false
    menuTree.value = []
    originalGrant.value = null
    loadedRoleId.value = null
  }

  /**
   * 保存权限配置
   */
  const savePermission = async () => {
    if (!props.roleData || !treeRef.value) return
    // 授权未加载完成/加载失败/角色不匹配时禁止提交，避免空树清空或串角色写入
    if (!canSave.value || loadedRoleId.value === null) {
      ElMessage.warning('权限数据未加载完成，无法保存')
      return
    }
    const roleId = loadedRoleId.value

    saveLoading.value = true
    try {
      const checkedKeys = treeRef.value.getCheckedKeys()
      const halfCheckedKeys = treeRef.value.getHalfCheckedKeys()

      // 分离菜单和权限
      const allKeys = [...checkedKeys, ...halfCheckedKeys]
      const menuIds: number[] = []
      const permissionIds: number[] = []

      allKeys.forEach((key: string) => {
        const node = findNodeByKey(menuTree.value, key)
        if (key.startsWith('menu:')) {
          const id = parseInt(key.replace('menu:', ''), 10)
          if (!isNaN(id)) menuIds.push(id)
        } else if (key.startsWith('permission:')) {
          const id = parseInt(key.replace('permission:', ''), 10)
          if (!isNaN(id)) permissionIds.push(id)
          if (node?.menuId) menuIds.push(node.menuId)
        }
      })

      // 保留原有的 dataScopes；菜单和权限全部取消时按后端契约携带 clearAll: true
      const payload: Api.SystemManage.RoleGrantPayload = buildRoleGrantPayload({
        menuIds,
        permissionIds,
        dataScopes: originalGrant.value?.dataScopes || []
      })

      if (payload.clearAll) {
        try {
          await ElMessageBox.confirm(
            '当前未勾选任何菜单和权限，保存后将清空该角色的全部菜单和权限，确定继续吗？',
            '清空权限确认',
            { confirmButtonText: '确定清空', cancelButtonText: '取消', type: 'warning' }
          )
        } catch {
          return
        }
      }

      await fetchUpdateRoleGrant(roleId, payload)
      ElMessage.success('权限保存成功，相关用户重新登录后生效')
      emit('success')
      handleClose()
    } catch (error: any) {
      ElMessage.error(error?.message || '保存失败')
    } finally {
      saveLoading.value = false
    }
  }

  /**
   * 切换全部展开/收起状态
   */
  const toggleExpandAll = () => {
    const tree = treeRef.value
    if (!tree) return

    const nodes = tree.store.nodesMap
    Object.values(nodes).forEach((node: any) => {
      node.expanded = !isExpandAll.value
    })

    isExpandAll.value = !isExpandAll.value
  }

  /**
   * 切换全选/取消全选状态
   */
  const toggleSelectAll = () => {
    const tree = treeRef.value
    if (!tree) return

    if (!isSelectAll.value) {
      const allKeys = getAllNodeKeys(menuTree.value)
      tree.setCheckedKeys(allKeys)
    } else {
      tree.setCheckedKeys([])
    }

    isSelectAll.value = !isSelectAll.value
  }

  /**
   * 递归获取所有节点的 key
   */
  const getAllNodeKeys = (nodes: TreeNode[]): string[] => {
    const keys: string[] = []
    const traverse = (nodeList: TreeNode[]): void => {
      nodeList.forEach((node) => {
        keys.push(node.key)
        if (node.children?.length) traverse(node.children)
      })
    }
    traverse(nodes)
    return keys
  }
</script>
