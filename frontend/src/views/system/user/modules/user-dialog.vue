<template>
  <ElDialog
    v-model="dialogVisible"
    :title="dialogType === 'add' ? '添加用户' : '编辑用户'"
    width="500px"
    align-center
  >
    <ElForm ref="formRef" :model="formData" :rules="rules" label-width="80px">
      <ElFormItem label="用户名" prop="userName">
        <ElInput
          v-model="formData.userName"
          placeholder="请输入用户名"
          :disabled="dialogType === 'edit'"
        />
      </ElFormItem>
      <p v-if="dialogType === 'add'" class="mb-4 text-xs text-gray-500">创建后系统会生成一次性临时密码，提交成功后仅显示一次。</p>
      <ElFormItem label="昵称" prop="nickName">
        <ElInput v-model="formData.nickName" placeholder="请输入昵称" />
      </ElFormItem>
      <ElFormItem label="手机号" prop="userPhone">
        <ElInput v-model="formData.userPhone" placeholder="请输入手机号" />
      </ElFormItem>
      <ElFormItem label="邮箱" prop="userEmail">
        <ElInput v-model="formData.userEmail" placeholder="请输入邮箱" />
      </ElFormItem>
      <ElFormItem label="性别" prop="userGender">
        <ElSelect v-model="formData.userGender" style="width: 100%">
          <ElOption label="未知" value="unknown" />
          <ElOption label="男" value="male" />
          <ElOption label="女" value="female" />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="状态" prop="status">
        <ElSelect v-model="formData.status" style="width: 100%">
          <ElOption label="启用" value="1" />
          <ElOption label="禁用" value="2" />
        </ElSelect>
      </ElFormItem>
      <ElFormItem label="角色" prop="userRoles">
        <ElSelect
          v-model="formData.userRoles"
          multiple
          style="width: 100%"
          :loading="roleLoading"
          :disabled="!isSuperAdmin"
        >
          <ElOption
            v-for="role in roleOptions"
            :key="role.roleCode"
            :value="role.roleCode"
            :label="role.roleName"
            :disabled="!role.enabled"
          />
        </ElSelect>
      </ElFormItem>
    </ElForm>
    <template #footer>
      <div class="dialog-footer">
        <ElButton @click="dialogVisible = false">取消</ElButton>
        <ElButton type="primary" :loading="submitLoading" @click="handleSubmit">提交</ElButton>
      </div>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
  import { fetchGetRoleOptions, fetchCreateUser, fetchUpdateUser } from '@/api/system-manage'
  import { useUserStore } from '@/store/modules/user'
  import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'

  interface Props {
    visible: boolean
    type: string
    userData?: Partial<Api.SystemManage.UserListItem>
  }

  interface Emits {
    (e: 'update:visible', value: boolean): void
    (e: 'submit'): void
  }

  const props = defineProps<Props>()
  const emit = defineEmits<Emits>()
  const userStore = useUserStore()

  // 角色选项数据
  const roleOptions = ref<Api.SystemManage.RoleOption[]>([])
  const roleLoading = ref(false)

  // 对话框显示控制
  const dialogVisible = computed({
    get: () => props.visible,
    set: (value) => emit('update:visible', value)
  })

  const dialogType = computed(() => props.type)
  const isSuperAdmin = computed(() => userStore.getUserInfo.roles?.includes('R_SUPER'))

  // 表单实例
  const formRef = ref<FormInstance>()
  const submitLoading = ref(false)

  // 表单数据
  const formData = reactive<Api.SystemManage.UserPayload>({
    userName: '',
    nickName: '',
    userPhone: '',
    userEmail: '',
    userGender: 'unknown',
    status: '1',
    userRoles: []
  })
  const originalRoles = ref<string[]>([])

  // 表单验证规则
  const rules: FormRules = {
    userName: [
      { required: true, message: '请输入用户名', trigger: 'blur' },
      { min: 2, max: 20, message: '长度在 2 到 20 个字符', trigger: 'blur' }
    ],
    userPhone: [
      { pattern: /^1[3-9]\d{9}$/, message: '请输入正确的手机号格式', trigger: 'blur' }
    ],
    userEmail: [
      { type: 'email', message: '请输入正确的邮箱格式', trigger: 'blur' }
    ]
  }

  /**
   * 加载角色选项
   */
  const loadRoleOptions = async () => {
    roleLoading.value = true
    try {
      roleOptions.value = await fetchGetRoleOptions()
    } catch (error) {
      console.error('加载角色选项失败:', error)
    } finally {
      roleLoading.value = false
    }
  }

  /**
   * 初始化表单数据
   */
  const initFormData = () => {
    const isEdit = props.type === 'edit' && props.userData
    const row = props.userData
    const userRoles = isEdit && row ? (Array.isArray(row.userRoles) ? row.userRoles : []) : ['R_USER']

    Object.assign(formData, {
      userName: isEdit && row ? row.userName || '' : '',
      nickName: isEdit && row ? row.nickName || '' : '',
      userPhone: isEdit && row ? row.userPhone || '' : '',
      userEmail: isEdit && row ? row.userEmail || '' : '',
      userGender: isEdit && row ? row.userGender || 'unknown' : 'unknown',
      status: isEdit && row ? row.status || '1' : '1',
      userRoles
    })
    originalRoles.value = [...userRoles]
  }

  const hasRoleChanged = () => {
    const current = [...(formData.userRoles || [])].sort()
    const original = [...originalRoles.value].sort()
    return current.length !== original.length || current.some((role, index) => role !== original[index])
  }

  /**
   * 监听对话框状态变化
   */
  watch(
    () => props.visible,
    (visible) => {
      if (visible) {
        initFormData()
        loadRoleOptions()
        nextTick(() => {
          formRef.value?.clearValidate()
        })
      }
    },
    { immediate: true }
  )

  /**
   * 提交表单
   */
  const handleSubmit = async () => {
    if (!formRef.value) return

    await formRef.value.validate(async (valid) => {
      if (!valid) return

      submitLoading.value = true
      try {
        if (!isSuperAdmin.value && hasRoleChanged()) {
          ElMessage.error('只有超级管理员可以修改用户角色')
          return
        }

        if (dialogType.value === 'add') {
          const result = await fetchCreateUser(formData)
          ElMessage.success('添加成功')
          await ElMessageBox.alert(`临时密码：${result.temporaryPassword}\n\n请通过安全渠道交付给用户；关闭后不会再次显示，也不会保存到浏览器。`, '临时密码（仅显示本次）', {
            confirmButtonText: '我已安全交付',
            type: 'success'
          })
        } else if (props.userData?.id) {
          await fetchUpdateUser(props.userData.id, formData)
          ElMessage.success('更新成功')
        }
        dialogVisible.value = false
        emit('submit')
      } catch (error: any) {
        ElMessage.error(error?.message || '操作失败')
      } finally {
        submitLoading.value = false
      }
    })
  }
</script>
