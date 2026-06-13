<!-- 授权页背景：静态科技背景图 + 主题蒙版，三页共用 -->
<template>
  <div class="auth-bg" :class="{ 'is-light': !isDark }">
    <div class="auth-bg__image" :style="{ backgroundImage: `url(${backgroundImage})` }"></div>
    <div class="auth-bg__glow"></div>
    <div class="auth-bg__mask"></div>
  </div>
</template>

<script setup lang="ts">
  import darkBackground from '@imgs/login/login-tech-bg-dark.png'
  import lightBackground from '@imgs/login/login-tech-bg-light.png'
  import { useSettingStore } from '@/store/modules/setting'

  defineOptions({ name: 'AuthBackground' })

  const settingStore = useSettingStore()
  const { isDark } = storeToRefs(settingStore)

  const backgroundImage = computed(() => (isDark.value ? darkBackground : lightBackground))
</script>

<style scoped>
  .auth-bg {
    position: fixed;
    inset: 0;
    z-index: 0;
    overflow: hidden;
    pointer-events: none;
    background: #020611;
  }

  .auth-bg__image {
    position: absolute;
    inset: 0;
    background-repeat: no-repeat;
    background-position: center bottom;
    background-size: cover;
    transform: scale(1.01);
  }

  .auth-bg__glow {
    position: absolute;
    inset: 0;
    background:
      radial-gradient(circle at 50% 12%, rgb(30 105 220 / 18%) 0%, transparent 34%),
      radial-gradient(circle at 86% 76%, rgb(25 139 255 / 18%) 0%, transparent 28%),
      radial-gradient(circle at 18% 82%, rgb(0 106 255 / 14%) 0%, transparent 30%);
    mix-blend-mode: screen;
  }

  .auth-bg__mask {
    position: absolute;
    inset: 0;
    background:
      linear-gradient(90deg, rgb(0 0 0 / 12%) 0%, rgb(0 0 0 / 0%) 34%, rgb(0 0 0 / 24%) 100%),
      radial-gradient(circle at 50% 44%, transparent 0%, rgb(0 0 0 / 22%) 78%),
      linear-gradient(180deg, rgb(0 0 0 / 16%) 0%, transparent 38%, rgb(0 0 0 / 20%) 100%);
  }

  .auth-bg.is-light {
    background: #e8f1ff;
  }

  /* 浅色态：去除提亮滤镜与白色蒙版，让清晰背景图原样透出 */
  .auth-bg.is-light .auth-bg__image {
    filter: none;
  }

  .auth-bg.is-light .auth-bg__glow {
    background: none;
  }

  .auth-bg.is-light .auth-bg__mask {
    background: none;
  }
</style>
