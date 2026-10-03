<!-- 锁屏 -->
<template>
  <div class="layout-lock-screen">
    <!-- 开发者工具警告覆盖层 -->
    <div
      v-if="showDevToolsWarning"
      class="fixed top-0 left-0 z-[999999] flex-cc w-full h-full text-white bg-gradient-to-br from-[#1e1e1e] to-black animate-fade-in"
    >
      <div class="p-5 text-center select-none">
        <div class="mb-7.5 text-5xl">🔒</div>
        <h1 class="m-0 mb-5 text-3xl font-semibold text-danger">屏幕已遮挡</h1>
        <p class="max-w-125 m-0 text-lg leading-relaxed text-white">
          检测到开发者工具已打开<br />
          锁屏仅用于离席遮挡与本地偏好，<b>不构成访问控制</b>，也不保护数据
        </p>
        <div class="mt-7.5 text-sm text-gray-400">
          Presence curtain only — not access control
        </div>
      </div>
    </div>

    <!-- 锁屏弹窗 -->
    <div v-if="!isLock">
      <ElDialog v-model="visible" :width="370" :show-close="false" @open="handleDialogOpen">
        <div class="flex-c flex-col">
          <img class="w-16 h-16 rounded-full" src="@imgs/user/avatar.webp" alt="用户头像" />
          <div class="mt-7.5 mb-3.5 text-base font-medium">{{ userInfo.userName }}</div>
          <ElForm
            ref="formRef"
            :model="formData"
            :rules="rules"
            class="w-[90%]"
            @submit.prevent="handleLock"
          >
            <ElFormItem prop="password">
              <ElInput
                v-model="formData.password"
                type="password"
                :placeholder="$t('lockScreen.lock.inputPlaceholder')"
                :show-password="true"
                autocomplete="new-password"
                ref="lockInputRef"
                class="w-full mt-9"
                @keyup.enter="handleLock"
              >
                <template #suffix>
                  <ElIcon class="c-p" @click="handleLock">
                    <Lock />
                  </ElIcon>
                </template>
              </ElInput>
            </ElFormItem>
            <ElButton type="primary" class="w-full mt-0.5" @click="handleLock" v-ripple>
              {{ $t('lockScreen.lock.btnText') }}
            </ElButton>
          </ElForm>
        </div>
      </ElDialog>
    </div>

    <!-- 解锁界面 -->
    <div v-else class="unlock-content">
      <div class="flex-c flex-col w-80">
        <img class="w-16 h-16 mt-5 rounded-full" src="@imgs/user/avatar.webp" alt="用户头像" />
        <div class="mt-3 mb-3.5 text-base font-medium">
          {{ userInfo.userName }}
        </div>
        <ElForm
          ref="unlockFormRef"
          :model="unlockForm"
          :rules="rules"
          class="w-full !px-2.5"
          @submit.prevent="handleUnlock"
        >
          <ElFormItem prop="password">
            <ElInput
              v-model="unlockForm.password"
              type="password"
              :placeholder="$t('lockScreen.unlock.inputPlaceholder')"
              :show-password="true"
              autocomplete="new-password"
              ref="unlockInputRef"
              class="mt-5"
            >
              <template #suffix>
                <ElIcon class="c-p" @click="handleUnlock">
                  <Unlock />
                </ElIcon>
              </template>
            </ElInput>
          </ElFormItem>

          <ElButton type="primary" class="w-full mt-2" @click="handleUnlock" v-ripple>
            {{ $t('lockScreen.unlock.btnText') }}
          </ElButton>
          <div class="w-full text-center">
            <ElButton
              text
              class="mt-2.5 !text-g-600 hover:!text-theme hover:!bg-transparent"
              @click="toLogin"
            >
              {{ $t('lockScreen.unlock.backBtnText') }}
            </ElButton>
          </div>
        </ElForm>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { Lock, Unlock } from '@element-plus/icons-vue'
  import type { FormInstance, FormRules } from 'element-plus'
  import { useI18n } from 'vue-i18n'
  import { useUserStore } from '@/store/modules/user'
  import { mittBus } from '@/utils/sys'

  // 国际化
  const { t } = useI18n()

  // 锁屏口令只用于"离席遮挡"与本地偏好，**不是访问控制**（B49）：
  // 原先用随包发布的固定 VITE_LOCK_ENCRYPT_KEY 做可逆 AES，既能把原文解回来、
  // 又给人"已加密受保护"的假象。现在改为加盐 PBKDF2 摘要——原文既不落盘（本来也只
  // 在内存），也不再存在于 store 中，无法被解回。
  const LOCK_SALT_BYTES = 16
  const LOCK_ITERATIONS = 120000

  const toHex = (bytes: Uint8Array): string =>
    Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')

  const fromHex = (value: string): Uint8Array =>
    new Uint8Array((value.match(/.{2}/g) ?? []).map((pair) => parseInt(pair, 16)))

  const deriveLockHash = async (password: string, salt: Uint8Array): Promise<string> => {
    const encoder = new TextEncoder()
    const key = await crypto.subtle.importKey('raw', encoder.encode(password), 'PBKDF2', false, [
      'deriveBits'
    ])
    const bits = await crypto.subtle.deriveBits(
      { name: 'PBKDF2', salt: salt as unknown as BufferSource, iterations: LOCK_ITERATIONS, hash: 'SHA-256' },
      key,
      256
    )
    return toHex(new Uint8Array(bits))
  }

  const subtleAvailable = (): boolean =>
    typeof crypto !== 'undefined' && typeof crypto.subtle !== 'undefined'

  /** 生成锁屏凭据：优先加盐 PBKDF2 摘要；无 crypto.subtle（非安全上下文）时降级为
   *  内存明文并显式标注 `plain:`，不再假装是加密。 */
  const createLockCredential = async (password: string): Promise<string> => {
    if (!subtleAvailable()) {
      console.warn('[lock] crypto.subtle 不可用，锁屏凭据以内存明文保存（锁屏不是访问控制）')
      return `plain:${password}`
    }
    const salt = crypto.getRandomValues(new Uint8Array(LOCK_SALT_BYTES))
    return `${toHex(salt)}:${await deriveLockHash(password, salt)}`
  }

  // Store
  const userStore = useUserStore()
  const { info: userInfo, lockPassword, isLock } = storeToRefs(userStore)

  // 响应式数据
  const visible = ref<boolean>(false)
  const lockInputRef = ref<any>(null)
  const unlockInputRef = ref<any>(null)
  const showDevToolsWarning = ref<boolean>(false)

  // 表单相关
  const formRef = ref<FormInstance>()
  const unlockFormRef = ref<FormInstance>()

  const formData = reactive({
    password: ''
  })

  const unlockForm = reactive({
    password: ''
  })

  // 表单验证规则
  const rules = computed<FormRules>(() => ({
    password: [
      {
        required: true,
        message: t('lockScreen.lock.inputPlaceholder'),
        trigger: 'blur'
      }
    ]
  }))

  // 检测是否为移动设备
  const isMobile = () => {
    return /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(
      navigator.userAgent
    )
  }

  // 添加禁用控制台的函数
  const disableDevTools = () => {
    // 禁用右键菜单
    const handleContextMenu = (e: Event) => {
      if (isLock.value) {
        e.preventDefault()
        e.stopPropagation()
        return false
      }
    }
    document.addEventListener('contextmenu', handleContextMenu, true)

    // 禁用开发者工具相关快捷键
    const handleKeyDown = (e: KeyboardEvent) => {
      if (!isLock.value) return

      // 禁用 F12
      if (e.key === 'F12') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }

      // 禁用 Ctrl+Shift+I/J/C/K (开发者工具)
      if (e.ctrlKey && e.shiftKey) {
        const key = e.key.toLowerCase()
        if (['i', 'j', 'c', 'k'].includes(key)) {
          e.preventDefault()
          e.stopPropagation()
          return false
        }
      }

      // 禁用 Ctrl+U (查看源代码)
      if (e.ctrlKey && e.key.toLowerCase() === 'u') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }

      // 禁用 Ctrl+S (保存页面)
      if (e.ctrlKey && e.key.toLowerCase() === 's') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }

      // 禁用 Ctrl+A (全选)
      if (e.ctrlKey && e.key.toLowerCase() === 'a') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }

      // 禁用 Ctrl+P (打印)
      if (e.ctrlKey && e.key.toLowerCase() === 'p') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }

      // 禁用 Ctrl+F (查找)
      if (e.ctrlKey && e.key.toLowerCase() === 'f') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }

      // 禁用 Alt+Tab (切换窗口)
      if (e.altKey && e.key === 'Tab') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }

      // 禁用 Ctrl+Tab (切换标签页)
      if (e.ctrlKey && e.key === 'Tab') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }

      // 禁用 Ctrl+W (关闭标签页)
      if (e.ctrlKey && e.key.toLowerCase() === 'w') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }

      // 禁用 Ctrl+R 和 F5 (刷新页面)
      if ((e.ctrlKey && e.key.toLowerCase() === 'r') || e.key === 'F5') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }

      // 禁用 Ctrl+Shift+R (强制刷新)
      if (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === 'r') {
        e.preventDefault()
        e.stopPropagation()
        return false
      }
    }
    document.addEventListener('keydown', handleKeyDown, true)

    // 禁用选择文本
    const handleSelectStart = (e: Event) => {
      if (isLock.value) {
        e.preventDefault()
        return false
      }
    }
    document.addEventListener('selectstart', handleSelectStart, true)

    // 禁用拖拽
    const handleDragStart = (e: Event) => {
      if (isLock.value) {
        e.preventDefault()
        return false
      }
    }
    document.addEventListener('dragstart', handleDragStart, true)

    // 监听开发者工具打开状态（仅在桌面端启用）
    let devtools = { open: false }
    const threshold = 160
    let devToolsInterval: ReturnType<typeof setInterval> | null = null

    const checkDevTools = () => {
      if (!isLock.value || isMobile()) return

      const isDevToolsOpen =
        window.outerHeight - window.innerHeight > threshold ||
        window.outerWidth - window.innerWidth > threshold

      if (isDevToolsOpen && !devtools.open) {
        devtools.open = true
        showDevToolsWarning.value = true
      } else if (!isDevToolsOpen && devtools.open) {
        devtools.open = false
        showDevToolsWarning.value = false
      }
    }

    // 仅在桌面端启用开发者工具检测
    if (!isMobile()) {
      devToolsInterval = setInterval(checkDevTools, 500)
    }

    // 返回清理函数
    return () => {
      document.removeEventListener('contextmenu', handleContextMenu, true)
      document.removeEventListener('keydown', handleKeyDown, true)
      document.removeEventListener('selectstart', handleSelectStart, true)
      document.removeEventListener('dragstart', handleDragStart, true)
      if (devToolsInterval) {
        clearInterval(devToolsInterval)
      }
    }
  }

  // 工具函数
  const safeEqual = (left: string, right: string): boolean => {
    if (left.length !== right.length) return false
    let diff = 0
    for (let index = 0; index < left.length; index += 1) {
      diff |= left.charCodeAt(index) ^ right.charCodeAt(index)
    }
    return diff === 0
  }

  const verifyPassword = async (
    inputPassword: string,
    storedCredential: string
  ): Promise<boolean> => {
    const credential = String(storedCredential ?? '')
    if (!credential) return false
    // 降级形态：内存明文（锁屏不是访问控制，这里只做遮挡校验）。
    if (credential.startsWith('plain:')) return safeEqual(inputPassword, credential.slice(6))
    const [saltHex, expected] = credential.split(':')
    if (!saltHex || !expected) return false
    if (!subtleAvailable()) {
      console.warn('[lock] crypto.subtle 不可用，无法校验加盐摘要；请重新设置锁屏口令')
      return false
    }
    return safeEqual(await deriveLockHash(inputPassword, fromHex(saltHex)), expected)
  }

  // 事件处理函数
  const handleKeydown = (event: KeyboardEvent) => {
    if (event.altKey && event.key.toLowerCase() === '¬') {
      event.preventDefault()
      visible.value = true
    }
  }

  const handleDialogOpen = () => {
    setTimeout(() => {
      lockInputRef.value?.input?.focus()
    }, 100)
  }

  const handleLock = async () => {
    if (!formRef.value) return

    try {
      await formRef.value.validate()
    } catch (fields) {
      console.error('表单验证失败:', fields)
      return
    }
    const credential = await createLockCredential(formData.password)
    userStore.setLockStatus(true)
    userStore.setLockPassword(credential)
    visible.value = false
    formData.password = ''
  }

  const handleUnlock = async () => {
    if (!unlockFormRef.value) return

    await unlockFormRef.value.validate(async (valid, fields) => {
      if (valid) {
        const isValid = await verifyPassword(unlockForm.password, lockPassword.value)

        if (isValid) {
          try {
            userStore.setLockStatus(false)
            userStore.setLockPassword('')
            unlockForm.password = ''
            visible.value = false
            showDevToolsWarning.value = false
          } catch (error) {
            console.error('更新store失败:', error)
          }
        } else {
          // 触发抖动动画
          const inputElement = unlockInputRef.value?.$el
          if (inputElement) {
            inputElement.classList.add('shake-animation')
            setTimeout(() => {
              inputElement.classList.remove('shake-animation')
            }, 300)
          }
          ElMessage.error(t('lockScreen.pwdError'))
          unlockForm.password = ''
        }
      } else {
        console.error('表单验证失败:', fields)
      }
    })
  }

  const toLogin = () => {
    userStore.logOut()
  }

  const openLockScreen = () => {
    visible.value = true
  }

  // 监听锁屏状态变化
  watch(isLock, (newValue) => {
    if (newValue) {
      document.body.style.overflow = 'hidden'
      setTimeout(() => {
        unlockInputRef.value?.input?.focus()
      }, 100)
    } else {
      document.body.style.overflow = 'auto'
      showDevToolsWarning.value = false
    }
  })

  // 存储清理函数
  let cleanupDevTools: (() => void) | null = null

  // 生命周期钩子
  onMounted(() => {
    mittBus.on('openLockScreen', openLockScreen)
    document.addEventListener('keydown', handleKeydown)

    if (isLock.value) {
      visible.value = true
      setTimeout(() => {
        unlockInputRef.value?.input?.focus()
      }, 100)
    }

    // 初始化禁用开发者工具功能
    cleanupDevTools = disableDevTools()
  })

  onUnmounted(() => {
    document.removeEventListener('keydown', handleKeydown)
    document.body.style.overflow = 'auto'
    // 清理禁用开发者工具的事件监听器
    if (cleanupDevTools) {
      cleanupDevTools()
      cleanupDevTools = null
    }
  })
</script>

<style lang="scss" scoped>
  .layout-lock-screen :deep(.el-dialog) {
    border-radius: 10px;
  }

  .unlock-content {
    position: fixed;
    inset: 0;
    z-index: 2500;
    display: flex;
    align-items: center;
    justify-content: center;
    overflow: hidden;
    background-color: #fff;
    background-image: url('@imgs/lock/bg_light.webp');
    background-size: cover;
    transition: transform 0.3s ease-in-out;
  }

  .dark {
    .unlock-content {
      background-image: url('@imgs/lock/bg_dark.webp');
    }
  }

  @keyframes fade-in {
    from {
      opacity: 0;
      transform: scale(0.9);
    }

    to {
      opacity: 1;
      transform: scale(1);
    }
  }

  .animate-fade-in {
    animation: fade-in 0.3s ease-in-out;
  }

  @keyframes shake {
    0%,
    100% {
      transform: translateX(0);
    }

    10%,
    30%,
    50%,
    70%,
    90% {
      transform: translateX(-10px);
    }

    20%,
    40%,
    60%,
    80% {
      transform: translateX(10px);
    }
  }

  .shake-animation {
    animation: shake 0.5s ease-in-out;
  }
</style>
