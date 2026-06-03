<!-- 登录、注册、忘记密码左侧品牌区（深色科技风背景入口 + 标题文案） -->
<template>
  <div class="login-left-view">
    <!-- 全屏深色科技背景（fixed 铺满视口，三页共用） -->
    <AuthBackground />

    <div class="logo">
      <ArtLogo class="icon" size="35" />
    </div>

    <div class="text-wrap">
      <h1> {{ $t('login.leftView.title') }} </h1>
      <p> {{ $t('login.leftView.subTitle') }} </p>
    </div>
  </div>
</template>

<script setup lang="ts">
  import AuthBackground from './AuthBackground.vue'

  // 定义 props
  defineProps<{
    hideContent?: boolean // 是否隐藏内容，只显示 logo
  }>()
</script>

<style lang="scss" scoped>
  .login-left-view {
    position: relative;
    box-sizing: border-box;
    width: 65vw;
    height: 100%;
    padding: 15px;
    overflow: hidden;
    background-color: transparent;

    .logo {
      position: relative;
      z-index: 2;
      display: flex;
      align-items: center;

      .title {
        margin-left: 10px;
        font-size: 20px;
        font-weight: 400;
      }
    }

    .text-wrap {
      position: absolute;
      top: 50%;
      left: 20%;
      z-index: 2;
      max-width: 560px;
      transform: translateY(-50%);
      animation: slideInLeft 0.6s cubic-bezier(0.25, 0.46, 0.45, 0.94) forwards;

      h1 {
        font-size: 48px;
        font-weight: 600;
        line-height: 1.2;
        color: var(--art-gray-900) !important;
        text-shadow: 0 2px 24px rgb(64 158 255 / 18%);
      }

      p {
        margin-top: 16px;
        font-size: 16px;
        color: var(--art-gray-600) !important;
        letter-spacing: 1px;
      }
    }

    @media only screen and (width <= 1600px) {
      width: 60vw;

      .text-wrap h1 {
        font-size: 40px;
      }
    }

    @media only screen and (width <= 1180px) {
      width: auto;
      height: auto;
      padding: 0;

      .text-wrap,
      .logo {
        display: none;
      }
    }
  }

  // 滑入动画
  @keyframes slideInLeft {
    from {
      opacity: 0;
      transform: translate(-30px, -50%);
    }

    to {
      opacity: 1;
      transform: translate(0, -50%);
    }
  }
</style>
