<!-- 文档卡片网格：总览页与分类页共用 -->
<template>
  <ElRow :gutter="20">
    <ElCol v-for="doc in docs" :key="doc.slug" :xs="24" :sm="12" :md="8" :lg="6" class="doc-grid__col">
      <DocCard
        :slug="doc.slug"
        :title="doc.title"
        :intro="doc.intro"
        :cover="doc.cover"
        :icon="icon"
        :color="color"
        :category-title="categoryTitle"
        :category-key="categoryKey"
      />
    </ElCol>
  </ElRow>
</template>

<script setup lang="ts">
  import DocCard from './DocCard.vue'
  import type { DocMeta } from '../docs.config'

  defineProps<{
    docs: DocMeta[]
    icon: string
    color: string
    categoryTitle: string
    categoryKey: string
  }>()
</script>

<style scoped lang="scss">
  .doc-grid__col {
    margin-bottom: 20px;
  }
</style>
