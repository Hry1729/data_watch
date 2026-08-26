<template>
  <div class="flex w-full h-screen items-center justify-center bg-layout">
    <div class="w-[420px] max-w-[calc(100%-32px)] rounded-xl bg-card p-8 shadow-sm">
      <h3 class="text-xl font-semibold">请修改临时密码</h3>
      <p class="mt-2 text-sm text-muted-foreground">当前账号必须先设置新密码才能继续使用系统。</p>
      <ElForm ref="formRef" class="mt-6" :model="formData" :rules="rules" @keyup.enter="handleSubmit">
        <ElFormItem prop="oldPassword">
          <ElInput v-model="formData.oldPassword" type="password" show-password autocomplete="current-password" placeholder="当前密码" />
        </ElFormItem>
        <ElFormItem prop="newPassword">
          <ElInput v-model="formData.newPassword" type="password" show-password autocomplete="new-password" placeholder="新密码（至少 6 位）" />
        </ElFormItem>
        <ElFormItem prop="confirmPassword">
          <ElInput v-model="formData.confirmPassword" type="password" show-password autocomplete="new-password" placeholder="确认新密码" />
        </ElFormItem>
        <ElAlert v-if="errorMessage" class="mb-4" :title="errorMessage" type="error" :closable="false" />
        <ElButton class="w-full" type="primary" :loading="loading" @click="handleSubmit">保存新密码</ElButton>
      </ElForm>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { fetchChangePassword } from '@/api/auth'
  import { passwordChangeCompletion } from '@/utils/password-change-flow'
  import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
  import { useUserStore } from '@/store/modules/user'

  defineOptions({ name: 'ForceChangePassword' })

  const userStore = useUserStore()
  const formRef = ref<FormInstance>()
  const loading = ref(false)
  const errorMessage = ref('')
  const formData = reactive({ oldPassword: '', newPassword: '', confirmPassword: '' })
  const rules: FormRules = {
    oldPassword: [{ required: true, message: '请输入当前密码', trigger: 'blur' }],
    newPassword: [{ required: true, min: 6, message: '新密码至少 6 位', trigger: 'blur' }],
    confirmPassword: [{ required: true, message: '请再次输入新密码', trigger: 'blur' }]
  }

  const handleSubmit = async () => {
    if (!formRef.value || loading.value) return
    const valid = await formRef.value.validate().catch(() => false)
    if (!valid) return
    if (formData.newPassword !== formData.confirmPassword) {
      errorMessage.value = '两次输入的新密码不一致'
      return
    }
    errorMessage.value = ''
    loading.value = true
    try {
      await fetchChangePassword({ oldPassword: formData.oldPassword, newPassword: formData.newPassword })
      const completion = passwordChangeCompletion()
      ElMessage.success('密码修改成功，请使用新密码重新登录')
      if (completion.clearSession) {
        // Backend increments tokenVersion, so force a fresh login with the new password.
        userStore.logOut()
      }
    } catch (error: any) {
      errorMessage.value = error?.message || '密码修改失败，请确认当前密码并重试'
    } finally {
      loading.value = false
    }
  }
</script>
