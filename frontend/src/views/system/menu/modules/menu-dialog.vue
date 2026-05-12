<template>
  <ElDialog
    :title="dialogTitle"
    v-model="visible"
    width="600px"
    align-center
    @close="handleClose"
  >
    <ElForm ref="formRef" :model="form" :rules="rules" label-width="100px">
      <ElFormItem label="菜单名称" prop="name">
        <ElInput v-model="form.name" placeholder="菜单 name，如 DeviceMonitor" />
      </ElFormItem>
      <ElFormItem label="标题" prop="titleKey">
        <ElInput v-model="form.titleKey" placeholder="标题 key，如 menus.deviceMonitor.title" />
      </ElFormItem>
      <ElFormItem label="路由地址" prop="path">
        <ElInput v-model="form.path" placeholder="如 /device-monitor 或 device" />
      </ElFormItem>
      <ElFormItem label="组件路径" prop="component">
        <ElInput v-model="form.component" placeholder="如 /device_monitor/dashboard/index" />
      </ElFormItem>
      <ElFormItem label="图标" prop="icon">
        <ElInput v-model="form.icon" placeholder="如 ri:dashboard-line" />
      </ElFormItem>
      <ElFormItem label="菜单类型" prop="menuType">
        <ElSelect v-model="form.menuType" style="width: 100%">
          <ElOption label="目录" value="directory" />
          <ElOption label="菜单" value="menu" />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="排序" prop="sort">
        <ElInputNumber v-model="form.sort" :min="1" :max="999" style="width: 100%" />
      </ElFormItem>
      <ElFormItem label="状态" prop="enabled">
        <ElSwitch v-model="form.enabled" active-text="启用" inactive-text="禁用" />
      </ElFormItem>
      <ElFormItem label="页面缓存">
        <ElSwitch v-model="form.keepAlive" />
      </ElFormItem>
      <ElFormItem label="隐藏菜单">
        <ElSwitch v-model="form.isHide" />
      </ElFormItem>
      <ElFormItem label="隐藏标签">
        <ElSwitch v-model="form.isHideTab" />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton @click="handleClose">取消</ElButton>
      <ElButton type="primary" :loading="submitLoading" @click="handleSubmit">确定</ElButton>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
  import { fetchCreateMenu, fetchUpdateMenu } from '@/api/system-manage'
  import type { FormInstance, FormRules } from 'element-plus'

  interface Props {
    visible: boolean
    type: 'add' | 'edit'
    editData?: Api.SystemManage.SystemMenuTreeItem | null
    parentMenuId?: number
  }

  interface Emits {
    (e: 'update:visible', value: boolean): void
    (e: 'submit'): void
  }

  const props = withDefaults(defineProps<Props>(), {
    visible: false,
    type: 'add',
    editData: null,
    parentMenuId: undefined
  })

  const emit = defineEmits<Emits>()

  const formRef = ref<FormInstance>()
  const submitLoading = ref(false)

  const visible = computed({
    get: () => props.visible,
    set: (value) => emit('update:visible', value)
  })

  const dialogTitle = computed(() => {
    return props.type === 'add' ? '新增菜单' : '编辑菜单'
  })

  const form = reactive<Api.SystemManage.MenuPayload>({
    name: '',
    path: '',
    component: '',
    titleKey: '',
    icon: '',
    parentId: undefined,
    menuType: 'menu',
    sort: 1,
    enabled: true,
    keepAlive: false,
    isHide: false,
    isHideTab: false
  })

  const rules: FormRules = {
    name: [{ required: true, message: '请输入菜单名称', trigger: 'blur' }],
    titleKey: [{ required: true, message: '请输入标题', trigger: 'blur' }],
    path: [{ required: true, message: '请输入路由地址', trigger: 'blur' }]
  }

  /**
   * 监听弹窗打开
   */
  watch(
    () => props.visible,
    (newVal) => {
      if (newVal) {
        initForm()
      }
    }
  )

  /**
   * 初始化表单
   */
  const initForm = () => {
    if (props.type === 'edit' && props.editData) {
      const data = props.editData
      Object.assign(form, {
        name: data.name || '',
        path: data.path || '',
        component: data.component || '',
        titleKey: data.meta?.title || '',
        icon: data.meta?.icon || '',
        parentId: data.parentId,
        menuType: data.menuType || 'menu',
        sort: data.sort || 1,
        enabled: data.enabled ?? true,
        keepAlive: data.meta?.keepAlive ?? false,
        isHide: data.meta?.isHide ?? false,
        isHideTab: data.meta?.isHideTab ?? false
      })
    } else {
      Object.assign(form, {
        name: '',
        path: '',
        component: '',
        titleKey: '',
        icon: '',
        parentId: props.parentMenuId,
        menuType: 'menu',
        sort: 1,
        enabled: true,
        keepAlive: false,
        isHide: false,
        isHideTab: false
      })
    }
    nextTick(() => {
      formRef.value?.clearValidate()
    })
  }

  /**
   * 关闭弹窗
   */
  const handleClose = () => {
    visible.value = false
  }

  /**
   * 提交表单
   */
  const handleSubmit = async () => {
    if (!formRef.value) return

    try {
      await formRef.value.validate()
      submitLoading.value = true

      if (props.type === 'add') {
        await fetchCreateMenu(form)
        ElMessage.success('新增成功')
      } else if (props.editData?.id) {
        await fetchUpdateMenu(props.editData.id, form)
        ElMessage.success('修改成功')
      }

      emit('submit')
      handleClose()
    } catch (error: any) {
      if (error?.message) {
        ElMessage.error(error.message)
      }
    } finally {
      submitLoading.value = false
    }
  }
</script>
