<!-- 项目文档 · 总览：克制的标题卡 + 全部文章封面卡的整齐网格 -->
<template>
  <div class="docs-overview art-full-height">
    <!-- 头部：干净的标题卡，无渐变/水印/大数字 -->
    <header class="docs-head art-card">
      <div class="docs-head__icon">
        <ArtSvgIcon icon="ri:book-2-line" />
      </div>
      <div class="docs-head__text">
        <h1 class="docs-head__title">项目文档</h1>
        <p class="docs-head__desc">
          DataWatch 系统设计文档，按目录浏览各模块的设计、数据契约与运维说明。
        </p>
      </div>
      <div class="docs-head__meta">{{ categoryCount }} 个目录 · {{ docCount }} 篇</div>
    </header>

    <!-- 全部文章封面卡 -->
    <ElScrollbar class="docs-overview__scroll">
      <div class="docs-overview__grid">
        <DocCard
          v-for="doc in allDocs"
          :key="doc.slug"
          :slug="doc.slug"
          :title="doc.title"
          :intro="doc.intro"
          :cover="doc.cover"
          :icon="doc.categoryIcon"
          :color="doc.categoryColor"
          :category-title="doc.categoryTitle"
          :category-key="doc.categoryKey"
        />
      </div>
    </ElScrollbar>
  </div>
</template>

<script setup lang="ts">
  import DocCard from '../components/DocCard.vue'
  import { getFlatCategories, getAllDocs } from '../docs.config'

  defineOptions({ name: 'DocsOverview' })

  const allDocs = getAllDocs()
  const categoryCount = getFlatCategories().length
  const docCount = allDocs.length
</script>

<style scoped lang="scss">
  .docs-overview {
    display: flex;
    flex-direction: column;
    gap: 16px;
    height: 100%;

    &__scroll {
      flex: 1;
      min-height: 0;
    }

    // 全屏一行 5 个，向下逐级递减；align-items:start 让卡片按内容高度，消除底部留白
    &__grid {
      display: grid;
      grid-template-columns: 1fr;
      gap: 16px;
      align-items: start;
      padding-bottom: 4px;

      @media (min-width: 640px) {
        grid-template-columns: repeat(2, 1fr);
      }

      @media (min-width: 960px) {
        grid-template-columns: repeat(3, 1fr);
      }

      @media (min-width: 1280px) {
        grid-template-columns: repeat(4, 1fr);
      }

      @media (min-width: 1600px) {
        grid-template-columns: repeat(5, 1fr);
      }
    }
  }

  /* ---- 头部标题卡 ---- */
  .docs-head {
    display: flex;
    flex-shrink: 0;
    flex-wrap: wrap;
    gap: 16px;
    align-items: center;
    padding: 22px 24px;

    &__icon {
      display: flex;
      flex-shrink: 0;
      align-items: center;
      justify-content: center;
      width: 46px;
      height: 46px;
      font-size: 23px;
      color: var(--el-color-primary);
      background: var(--el-color-primary-light-9);
      border-radius: 12px;
    }

    &__text {
      flex: 1;
      min-width: 220px;
    }

    &__title {
      margin: 0;
      font-size: 19px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    &__desc {
      margin: 4px 0 0;
      font-size: 13px;
      line-height: 1.6;
      color: var(--el-text-color-secondary);
    }

    &__meta {
      flex-shrink: 0;
      font-size: 13px;
      color: var(--el-text-color-secondary);
    }
  }
</style>
