<template>
  <ElDialog
    :title="dialogTitle"
    v-model="visible"
    width="500px"
    align-center
    @close="handleClose"
  >
    <ElForm ref="formRef" :model="form" :rules="rules" label-width="100px">
      <ElFormItem label="权限名称" prop="permissionName">
        <ElInput v-model="form.permissionName" placeholder="如 新增、编辑、删除" />
      </ElFormItem>
      <ElFormItem label="权限编码" prop="permissionCode">
        <ElInput
          v-model="form.permissionCode"
          placeholder="如 system:user:create"
          :disabled="type === 'edit'"
        />
      </ElFormItem>
      <ElFormItem label="权限标识" prop="authMark">
        <ElInput v-model="form.authMark" placeholder="如 add、edit、delete 或完整权限码" />
      </ElFormItem>
      <ElFormItem label="权限类型" prop="permissionType">
        <ElSelect v-model="form.permissionType" style="width: 100%">
          <ElOption label="按钮权限" value="action" />
          <ElOption label="页面权限" value="page" />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="排序" prop="sort">
        <ElInputNumber v-model="form.sort" :min="1" :max="999" style="width: 100%" />
      </ElFormItem>
      <ElFormItem label="状态" prop="enabled">
        <ElSwitch v-model="form.enabled" active-text="启用" inactive-text="禁用" />
      </ElFormItem>
    </ElForm>
    <template #footer>
      <ElButton @click="handleClose">取消</ElButton>
      <ElButton type="primary" :loading="submitLoading" @click="handleSubmit">确定</ElButton>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
  import {
    fetchCreateMenuPermission,
    fetchUpdateMenuPermission,
    fetchDeleteMenuPermission
  } from '@/api/system-manage'
  import type { FormInstance, FormRules } from 'element-plus'

  interface Props {
    visible: boolean
    menuId: number
    editData?: Api.SystemManage.PermissionItem | null
  }

  interface Emits {
    (e: 'update:visible', value: boolean): void
    (e: 'submit'): void
  }

  const props = withDefaults(defineProps<Props>(), {
    visible: false,
    menuId: 0,
    editData: null
  })

  const emit = defineEmits<Emits>()

  const formRef = ref<FormInstance>()
  const submitLoading = ref(false)

  const visible = computed({
    get: () => props.visible,
    set: (value) => emit('update:visible', value)
  })

  const type = computed(() => (props.editData ? 'edit' : 'add'))

  const dialogTitle = computed(() => {
    return type.value === 'add' ? '新增权限' : '编辑权限'
  })

  const form = reactive<Api.SystemManage.PermissionPayload>({
    permissionCode: '',
    permissionName: '',
    permissionType: 'action',
    authMark: '',
    sort: 1,
    enabled: true
  })

  const rules: FormRules = {
    permissionName: [{ required: true, message: '请输入权限名称', trigger: 'blur' }],
    permissionCode: [{ required: true, message: '请输入权限编码', trigger: 'blur' }]
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
    if (props.editData) {
      const data = props.editData
      Object.assign(form, {
        permissionCode: data.permissionCode || '',
        permissionName: data.title || '',
        permissionType: data.permissionType || 'action',
        authMark: data.authMark || '',
        sort: data.sort || 1,
        enabled: data.enabled ?? true
      })
    } else {
      Object.assign(form, {
        permissionCode: '',
        permissionName: '',
        permissionType: 'action',
        authMark: '',
        sort: 1,
        enabled: true
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

      if (type.value === 'add') {
        await fetchCreateMenuPermission(props.menuId, form)
        ElMessage.success('新增成功')
      } else if (props.editData?.id) {
        await fetchUpdateMenuPermission(props.menuId, props.editData.id, form)
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
