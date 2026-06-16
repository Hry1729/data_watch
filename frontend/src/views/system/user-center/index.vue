<!-- 个人中心页面 -->
<template>
  <div class="w-full h-full p-0 bg-transparent border-none shadow-none">
    <div class="relative flex-b mt-2.5 max-md:block max-md:mt-1">
      <div class="w-112 mr-5 max-md:w-full max-md:mr-0">
        <div class="art-card-sm relative p-9 pb-6 overflow-hidden text-center">
          <img class="absolute top-0 left-0 w-full h-50 object-cover" src="@imgs/user/bg/bg.JPG" />
          <img
            class="relative z-10 w-20 h-20 mt-30 mx-auto object-cover border-2 border-white rounded-full"
            src="@imgs/user/avatar/avatar.png"
          />
          <h2 class="mt-5 text-xl font-normal">{{ userInfo.userName }}</h2>

          <div class="w-75 mx-auto mt-7.5 text-left">
            <div class="mt-2.5">
              <ArtSvgIcon icon="ri:map-pin-line" class="text-g-700" />
              <span class="ml-2 text-sm">洋山四期</span>
            </div>
            <div class="mt-2.5">
              <ArtSvgIcon icon="ri:dribbble-fill" class="text-g-700" />
              <span class="ml-2 text-sm">尚东分公司</span>
            </div>
          </div>

          <div class="mt-10">
            <h3 class="text-sm font-medium">标签</h3>
            <div class="flex flex-wrap justify-center mt-3.5">
              <div
                v-for="item in lableList"
                :key="item"
                class="py-1 px-1.5 mr-2.5 mb-2.5 text-xs border border-g-300 rounded"
              >
                {{ item }}
              </div>
            </div>
          </div>
        </div>
      </div>
      <div class="flex-1 overflow-hidden max-md:w-full max-md:mt-3.5">
        <div class="art-card-sm h-full">
          <h1 class="p-4 text-xl font-normal border-b border-g-300">设置</h1>

          <h2 class="px-5 py-3 text-base font-medium">更改密码</h2>

          <ElForm
            :model="pwdForm"
            class="box-border p-5 pt-0"
            label-width="86px"
            label-position="top"
          >
            <ElFormItem label="当前密码" prop="password">
              <ElInput
                v-model="pwdForm.password"
                type="password"
                :disabled="!isEditPwd"
                show-password
              />
            </ElFormItem>

            <ElFormItem label="新密码" prop="newPassword">
              <ElInput
                v-model="pwdForm.newPassword"
                type="password"
                :disabled="!isEditPwd"
                show-password
              />
            </ElFormItem>

            <ElFormItem label="确认新密码" prop="confirmPassword">
              <ElInput
                v-model="pwdForm.confirmPassword"
                type="password"
                :disabled="!isEditPwd"
                show-password
              />
            </ElFormItem>

            <div class="flex-c justify-end [&_.el-button]:!w-27.5">
              <ElButton type="primary" class="w-22.5" v-ripple @click="editPwd">
                {{ isEditPwd ? '保存' : '编辑' }}
              </ElButton>
            </div>
          </ElForm>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { useUserStore } from '@/store/modules/user'
  import { fetchChangePassword } from '@/api/auth'

  defineOptions({ name: 'UserCenter' })

  const userStore = useUserStore()
  const userInfo = computed(() => userStore.getUserInfo)

  const isEditPwd = ref(false)
  const date = ref('')

  /**
   * 密码修改表单
   */
  const pwdForm = reactive({
    password: '',
    newPassword: '',
    confirmPassword: ''
  })

  /**
   * 用户标签列表
   */
  const lableList: Array<string> = ['认真工作']

  onMounted(() => {
    getDate()
  })

  /**
   * 根据当前时间获取问候语
   */
  const getDate = () => {
    const h = new Date().getHours()

    if (h >= 6 && h < 9) date.value = '早上好'
    else if (h >= 9 && h < 11) date.value = '上午好'
    else if (h >= 11 && h < 13) date.value = '中午好'
    else if (h >= 13 && h < 18) date.value = '下午好'
    else if (h >= 18 && h < 24) date.value = '晚上好'
    else date.value = '很晚了，早点睡'
  }

  /**
   * 编辑/保存密码：保存时校验并调用后端修改密码接口
   */
  const editPwd = async () => {
    if (!isEditPwd.value) {
      isEditPwd.value = true
      return
    }

    if (!pwdForm.password || !pwdForm.newPassword || !pwdForm.confirmPassword) {
      ElMessage.warning('请填写完整的密码信息')
      return
    }
    if (pwdForm.newPassword !== pwdForm.confirmPassword) {
      ElMessage.warning('两次输入的新密码不一致')
      return
    }

    try {
      await fetchChangePassword({
        oldPassword: pwdForm.password,
        newPassword: pwdForm.newPassword
      })
      ElMessage.success('密码修改成功')
      pwdForm.password = ''
      pwdForm.newPassword = ''
      pwdForm.confirmPassword = ''
      isEditPwd.value = false
    } catch {
      // 失败提示由 http 拦截器统一处理
    }
  }
</script>
