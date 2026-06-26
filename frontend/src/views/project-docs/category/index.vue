<!-- 项目文档 · 分类页：展示某一分类下的文档卡片 -->
<template>
  <div class="docs-category art-full-height">
    <template v-if="category">
      <div class="docs-category__header art-card">
        <div
          class="docs-category__icon"
          :style="{ color: category.color, background: `${category.color}1f` }"
        >
          <ArtSvgIcon :icon="category.icon" />
        </div>
        <div>
          <div v-if="category.groupTitle" class="docs-category__crumb">
            {{ category.groupTitle }}
          </div>
          <h1 class="docs-category__title">{{ category.title }}</h1>
          <p class="docs-category__count">共 {{ category.docs.length }} 篇文档</p>
        </div>
      </div>

      <ElScrollbar class="docs-category__scroll">
        <DocGrid
          :docs="category.docs"
          :icon="category.icon"
          :color="category.color"
          :category-title="category.title"
          :category-key="category.key"
        />
      </ElScrollbar>
    </template>

    <ElEmpty v-else description="未找到该分类" />
  </div>
</template>

<script setup lang="ts">
  import { computed } from 'vue'
  import { useRoute } from 'vue-router'
  import DocGrid from '../components/DocGrid.vue'
  import { getCategoryByKey } from '../docs.config'

  defineOptions({ name: 'DocsCategory' })

  const route = useRoute()
  const categoryKey = computed(() => route.path.split('/').filter(Boolean).pop() || '')
  const category = computed(() => getCategoryByKey(categoryKey.value))
</script>

<style scoped lang="scss">
  .docs-category {
    display: flex;
    flex-direction: column;
    gap: 16px;
    height: 100%;

    &__header {
      display: flex;
      gap: 16px;
      align-items: center;
      padding: 22px 24px;
    }

    &__icon {
      display: flex;
      flex-shrink: 0;
      align-items: center;
      justify-content: center;
      width: 46px;
      height: 46px;
      font-size: 23px;
      border-radius: 12px;
    }

    &__crumb {
      margin-bottom: 2px;
      font-size: 12px;
      color: var(--el-text-color-secondary);
    }

    &__title {
      margin: 0;
      font-size: 20px;
      font-weight: 600;
      color: var(--el-text-color-primary);
    }

    &__count {
      margin: 4px 0 0;
      font-size: 13px;
      color: var(--el-text-color-secondary);
    }

    &__scroll {
      flex: 1;
      min-height: 0;
    }
  }
</style>
