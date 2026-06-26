<!-- 文档卡片：封面 + 标题 + 简介，点击进入阅读页 -->
<template>
  <div class="doc-card art-card" @click="openDoc">
    <div class="doc-card__cover">
      <!-- 真实封面图 -->
      <template v-if="resolvedCover">
        <img :src="resolvedCover" :alt="title" class="doc-card__img" />
        <span class="doc-card__tag" :style="{ color, background: `${color}1a` }">
          <ArtSvgIcon :icon="icon" class="doc-card__tag-icon" />
          {{ categoryTitle }}
        </span>
      </template>

      <!-- 占位 SVG 横幅：渐变 + 水印图标 + 分类 + 标题 -->
      <div
        v-else
        class="doc-card__banner"
        :style="{ background: `linear-gradient(135deg, ${color} 0%, ${color}b3 100%)` }"
      >
        <ArtSvgIcon :icon="icon" class="doc-card__banner-watermark" />
        <div class="doc-card__banner-cat">
          <ArtSvgIcon :icon="icon" class="doc-card__banner-cat-icon" />
          {{ categoryTitle }}
        </div>
        <div class="doc-card__banner-title">{{ title }}</div>
      </div>
    </div>

    <!-- 内容 -->
    <div class="doc-card__body">
      <!-- 标题仅在「真实封面」时显示（占位横幅已含标题，避免重复） -->
      <h3 v-if="resolvedCover" class="doc-card__title">{{ title }}</h3>
      <p class="doc-card__intro">{{ intro }}</p>
      <!-- 阅读入口贴在简介右下角，避免单独占一行造成底部留白 -->
      <span class="doc-card__more">
        阅读
        <ArtSvgIcon icon="ri:arrow-right-line" class="doc-card__more-icon" />
      </span>
    </div>
  </div>
</template>

<script setup lang="ts">
  import { computed } from 'vue'
  import { useRouter } from 'vue-router'
  import { getDocCover } from '../docs.config'

  const props = defineProps<{
    slug: string
    title: string
    intro: string
    icon: string
    color: string
    categoryTitle: string
    categoryKey: string
    cover?: string
  }>()

  const router = useRouter()

  // 显式 cover 优先，否则按 slug→分类名 自动绑定 src/assets/images/docs 下的图
  const resolvedCover = computed(() => props.cover || getDocCover(props.slug, props.categoryKey))

  const openDoc = () => {
    router.push(`/project-docs/read/${props.slug}`)
  }
</script>

<style scoped lang="scss">
  // 卡片底色/边框/圆角由全局 .art-card 提供（含暗色模式），此处只补布局与 hover
  .doc-card {
    display: flex;
    flex-direction: column;
    height: 100%;
    overflow: hidden;
    cursor: pointer;
    transition:
      transform 0.2s ease,
      box-shadow 0.2s ease,
      border-color 0.2s ease;

    &:hover {
      transform: translateY(-3px);
      border-color: var(--el-color-primary-light-5);
      box-shadow: 0 8px 24px rgb(0 0 0 / 10%);
    }

    &__cover {
      position: relative;
      height: 124px;
      overflow: hidden;
    }

    &__img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    /* ---- 占位横幅 ---- */
    &__banner {
      position: relative;
      width: 100%;
      height: 100%;
      overflow: hidden;
    }

    &__banner-watermark {
      position: absolute;
      right: -14px;
      bottom: -18px;
      font-size: 104px;
      color: rgb(255 255 255 / 16%);
      transform: rotate(-8deg);
      pointer-events: none;
    }

    &__banner-cat {
      position: absolute;
      top: 12px;
      left: 14px;
      display: inline-flex;
      gap: 4px;
      align-items: center;
      font-size: 12px;
      font-weight: 500;
      color: rgb(255 255 255 / 88%);
    }

    &__banner-cat-icon {
      font-size: 14px;
    }

    &__banner-title {
      position: absolute;
      right: 14px;
      bottom: 13px;
      left: 14px;
      display: -webkit-box;
      overflow: hidden;
      font-size: 14.5px;
      font-weight: 600;
      line-height: 1.35;
      color: #fff;
      text-shadow: 0 1px 4px rgb(0 0 0 / 22%);
      -webkit-line-clamp: 2;
      line-clamp: 2;
      -webkit-box-orient: vertical;
    }

    /* ---- 真实图上的分类标签 ---- */
    &__tag {
      position: absolute;
      bottom: 10px;
      left: 12px;
      display: inline-flex;
      gap: 4px;
      align-items: center;
      height: 24px;
      padding: 0 10px;
      font-size: 12px;
      font-weight: 500;
      backdrop-filter: blur(6px);
      border-radius: 12px;
    }

    &__tag-icon {
      font-size: 13px;
    }

    /* ---- 卡身 ---- */
    &__body {
      position: relative;
      display: flex;
      flex: 1;
      flex-direction: column;
      padding: 12px 14px 11px;
    }

    &__title {
      margin: 0;
      overflow: hidden;
      font-size: 15px;
      font-weight: 600;
      line-height: 1.4;
      color: var(--el-text-color-primary);
      text-overflow: ellipsis;
      white-space: nowrap;
    }

    &__intro {
      display: -webkit-box;
      // 固定 2 行高度：短简介也占满 2 行，保证所有卡片等高、"阅读"始终在第二行
      min-height: 3.2em; // 2 行 × line-height 1.6
      margin: 0;
      overflow: hidden;
      font-size: 13px;
      line-height: 1.6;
      color: var(--el-text-color-secondary);
      -webkit-line-clamp: 2;
      line-clamp: 2;
      -webkit-box-orient: vertical;
    }

    // 真实封面态：标题与简介之间留间距
    &__title + &__intro {
      margin-top: 8px;
    }

    // 阅读入口：绝对贴在 body 右下角（简介第二行最右侧），渐变遮罩避免压住简介文字
    &__more {
      position: absolute;
      right: 14px;
      bottom: 9px;
      display: inline-flex;
      gap: 2px;
      align-items: center;
      padding-left: 30px;
      font-size: 12.5px;
      font-weight: 500;
      color: var(--el-color-primary);
      background: linear-gradient(90deg, transparent, var(--default-box-color) 42%);
    }

    &__more-icon {
      font-size: 14px;
      transition: transform 0.2s ease;
    }
  }

  .doc-card:hover .doc-card__more-icon {
    transform: translateX(3px);
  }
</style>
