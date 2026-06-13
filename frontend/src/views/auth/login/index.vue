<!-- 登录页面 -->
<template>
  <div class="flex w-full h-screen">
    <LoginLeftView />

    <div class="relative flex-1 auth-right-panel">
      <AuthTopBar />

      <div class="auth-right-wrap">
        <div class="form">
          <h3 class="title">{{ $t('login.title') }}</h3>
          <p class="sub-title">{{ $t('login.subTitle') }}</p>
          <ElForm
            ref="formRef"
            :model="formData"
            :rules="rules"
            :key="formKey"
            @keyup.enter="handleSubmit"
            style="margin-top: 25px"
          >
            <ElFormItem prop="username">
              <ElSelect
                class="account-select w-full"
                size="large"
                v-model="formData.username"
                filterable
                allow-create
                default-first-option
                clearable
                :reserve-keyword="false"
                :filter-method="onUsernameFilter"
                :placeholder="$t('login.placeholder.username')"
                @change="onAccountChange"
                @clear="usernameQuery = ''"
              >
                <ElOption label="游客" value="guest" />
                <ElOption v-for="name in accountHistory" :key="name" :label="name" :value="name" />
              </ElSelect>
            </ElFormItem>
            <ElFormItem prop="password">
              <ElInput
                class="custom-height"
                :placeholder="$t('login.placeholder.password')"
                v-model.trim="formData.password"
                type="password"
                autocomplete="off"
                show-password
              />
            </ElFormItem>

            <!-- 推拽验证 -->
            <div class="relative pb-5 mt-6">
              <div
                class="relative z-[2] overflow-hidden select-none rounded-lg border border-transparent tad-300"
                :class="{ '!border-[#FF4E4F]': !isPassing && isClickPass }"
              >
                <ArtDragVerify
                  ref="dragVerify"
                  v-model:value="isPassing"
                  :text="$t('login.sliderText')"
                  :textColor="isDark ? '#c9d4e8' : 'var(--art-gray-700)'"
                  :successText="$t('login.sliderSuccessText')"
                  progressBarBg="var(--main-color)"
                  :background="isDark ? '#1b2236' : '#F1F1F4'"
                  :handlerBg="isDark ? '#2a3450' : 'var(--default-box-color)'"
                />
              </div>
              <p
                class="absolute top-0 z-[1] px-px mt-2 text-xs text-[#f56c6c] tad-300"
                :class="{ 'translate-y-10': !isPassing && isClickPass }"
              >
                {{ $t('login.placeholder.slider') }}
              </p>
            </div>

            <div class="flex-cb mt-2 text-sm">
              <ElCheckbox v-model="formData.rememberPassword">{{
                $t('login.rememberPwd')
              }}</ElCheckbox>
              <RouterLink class="text-theme" :to="{ name: 'ForgetPassword' }">{{
                $t('login.forgetPwd')
              }}</RouterLink>
            </div>

            <div style="margin-top: 30px">
              <ElButton
                class="w-full custom-height"
                type="primary"
                @click="handleSubmit"
                :loading="loading"
                v-ripple
              >
                {{ $t('login.btnText') }}
              </ElButton>
            </div>
          </ElForm>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import AppConfig from '@/config'
  import { useUserStore } from '@/store/modules/user'
  import { useI18n } from 'vue-i18n'
  import { HttpError } from '@/utils/http/error'
  import { fetchGetUserInfo, fetchLogin } from '@/api/auth'
  import { ElMessage, ElNotification, type FormInstance, type FormRules } from 'element-plus'
  import { useSettingStore } from '@/store/modules/setting'
  import { resetRouteInitState } from '@/router/guards/beforeEach'

  defineOptions({ name: 'Login' })

  const settingStore = useSettingStore()
  const { isDark } = storeToRefs(settingStore)
  const { t, locale } = useI18n()
  const formKey = ref(0)

  // 监听语言切换，重置表单
  watch(locale, () => {
    formKey.value++
  })

  const dragVerify = ref()

  const userStore = useUserStore()
  const router = useRouter()
  const route = useRoute()
  const isPassing = ref(false)
  const isClickPass = ref(false)

  const systemName = AppConfig.systemInfo.name
  const formRef = ref<FormInstance>()

  const formData = reactive({
    username: '',
    password: '',
    rememberPassword: true
  })

  // 历史登录账号（仅存用户名，绝不存密码；作为前端"禁持久化敏感数据"规则的白名单例外）
  const GUEST_USERNAME = 'guest'
  const GUEST_PASSWORD = '123456'
  const ACCOUNT_HISTORY_KEY = 'data-watch-login-accounts'
  const ACCOUNT_HISTORY_MAX = 5
  const accountHistory = ref<string[]>([])

  const normalizeAccount = (name: unknown) => String(name ?? '').trim()

  // 清洗历史：去字符串、trim、去重、排除空/游客、限长
  const sanitizeAccountHistory = (list: unknown): string[] => {
    if (!Array.isArray(list)) return []
    return Array.from(
      new Set(
        list
          .filter((n): n is string => typeof n === 'string')
          .map((n) => n.trim())
          .filter((n) => n && n !== GUEST_USERNAME)
      )
    ).slice(0, ACCOUNT_HISTORY_MAX)
  }

  const loadAccountHistory = () => {
    try {
      const raw = localStorage.getItem(ACCOUNT_HISTORY_KEY)
      accountHistory.value = sanitizeAccountHistory(raw ? JSON.parse(raw) : [])
    } catch {
      accountHistory.value = []
    }
  }

  const saveAccountHistory = (name: string) => {
    const normalized = normalizeAccount(name)
    if (!normalized || normalized === GUEST_USERNAME) return
    accountHistory.value = sanitizeAccountHistory([normalized, ...accountHistory.value])
    try {
      localStorage.setItem(ACCOUNT_HISTORY_KEY, JSON.stringify(accountHistory.value))
    } catch {
      // 隐私模式/配额限制等写入失败时，不阻断登录
    }
  }

  onMounted(loadAccountHistory)

  const rules = computed<FormRules>(() => ({
    username: [{ required: true, message: t('login.placeholder.username'), trigger: 'blur' }],
    password: [{ required: true, message: t('login.placeholder.password'), trigger: 'blur' }]
  }))

  const loading = ref(false)

  // allow-create 模式下打字未点选时，用户名只是筛选关键词、不会写入 v-model；
  // 用它记录当前输入，登录前兜底回填，避免"手输账号无效"。
  const usernameQuery = ref('')
  const onUsernameFilter = (query: string) => {
    usernameQuery.value = query
  }

  // 选择"游客"回填游客凭据；切到其他账号时清掉残留的游客密码
  const onAccountChange = (val: string) => {
    const name = normalizeAccount(val)
    formData.username = name
    usernameQuery.value = name
    if (name === GUEST_USERNAME) {
      formData.password = GUEST_PASSWORD
      return
    }
    if (formData.password === GUEST_PASSWORD) {
      formData.password = ''
    }
  }

  // Element Plus 表单校验失败时 reject 的是 { [prop]: ValidateFieldError[] } 形状的普通对象，
  // 用它把"校验失败"与真实运行时异常区分开，避免误吞真实错误或误弹提示。
  const isFormValidateError = (error: unknown): boolean =>
    !!error &&
    typeof error === 'object' &&
    !(error instanceof Error) &&
    Object.values(error as Record<string, unknown>).some(Array.isArray)

  // 登录
  const handleSubmit = async () => {
    if (!formRef.value) return

    try {
      // 打字未点选时用户名只在筛选关键词里、未写入 v-model；usernameQuery 实时反映输入框
      // 最新内容且能存活到提交，故优先用它，formData.username 仅作兜底。
      formData.username = normalizeAccount(usernameQuery.value || formData.username)

      // 表单验证
      const valid = await formRef.value.validate()
      if (!valid) return

      // 拖拽验证
      if (!isPassing.value) {
        isClickPass.value = true
        return
      }

      loading.value = true

      // 登录请求
      const username = normalizeAccount(formData.username)
      formData.username = username
      const { password } = formData

      const { token, refreshToken } = await fetchLogin({
        userName: username,
        password
      })

      // 验证token
      if (!token) {
        throw new Error('Login failed - no token received')
      }

      // 存储 token，随后用新 token 初始化用户态和路由态。
      userStore.setToken(token, refreshToken)
      resetRouteInitState()
      const userInfo = await fetchGetUserInfo()
      userStore.setUserInfo(userInfo)
      userStore.setLoginStatus(true)
      userStore.checkAndClearWorktabs()

      // 记录历史账号（仅用户名）
      saveAccountHistory(username)

      // 登录成功处理
      showLoginSuccessNotice()

      // 获取 redirect 参数，如果存在则跳转到指定页面，否则跳转到首页
      const redirect = route.query.redirect as string
      await router.replace(redirect || '/')
    } catch (error) {
      // HttpError：http 层已统一弹出错误提示，无需重复处理
      if (error instanceof HttpError) {
        // 已提示
      } else if (isFormValidateError(error)) {
        // 表单校验失败对象（{username:[...]}）：字段下方已有行内红字，静默处理
      } else {
        // 其余未知错误（含 Error 实例与非 Error 抛出值）：给用户可见提示并记录
        ElMessage.error('登录失败，请检查账号密码或稍后重试')
        console.error('[Login] Unexpected error:', error)
      }
    } finally {
      loading.value = false
      resetDragVerify()
    }
  }

  // 重置拖拽验证
  const resetDragVerify = () => {
    dragVerify.value?.reset()
  }

  // 登录成功提示
  const showLoginSuccessNotice = () => {
    setTimeout(() => {
      ElNotification({
        title: t('login.success.title'),
        type: 'success',
        duration: 2500,
        zIndex: 10000,
        message: `${t('login.success.message')}, ${systemName}!`
      })
    }, 1000)
  }
</script>

<style scoped>
  @import './style.css';
</style>
