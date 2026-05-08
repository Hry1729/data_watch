<template>
  <div class="monitor-loading" :style="{ minHeight }">
    <div class="monitor-loading__icon">
      <ArtSvgIcon icon="ri:loader-4-line" />
    </div>
    <p class="monitor-loading__title">{{ title }}</p>
    <p v-if="description" class="monitor-loading__description">{{ description }}</p>
    <div v-if="rows > 0" class="monitor-loading__rows">
      <span v-for="index in rows" :key="index" class="monitor-loading__row"></span>
    </div>
  </div>
</template>

<script setup lang="ts">
  withDefaults(
    defineProps<{
      title?: string
      description?: string
      minHeight?: string
      rows?: number
    }>(),
    {
      title: '加载中',
      description: '',
      minHeight: '220px',
      rows: 0
    }
  )
</script>

<style scoped lang="scss">
  .monitor-loading {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: 10px;
    width: 100%;
    padding: 24px;
    border-radius: 16px;
    border: 1px dashed #dbe3ef;
    background: linear-gradient(180deg, #fbfcff 0%, #f5f8fc 100%);
    text-align: center;
  }

  .monitor-loading__icon {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    width: 40px;
    height: 40px;
    border-radius: 999px;
    background: rgba(74, 108, 247, 0.1);
    color: #4a6cf7;
    font-size: 20px;
    animation: monitor-loading-spin 0.9s linear infinite;
  }

  .monitor-loading__title {
    margin: 0;
    font-size: 14px;
    font-weight: 600;
    color: #1d2433;
  }

  .monitor-loading__description {
    margin: 0;
    font-size: 12px;
    color: #7b8798;
  }

  .monitor-loading__rows {
    display: flex;
    flex-direction: column;
    gap: 8px;
    width: min(360px, 100%);
    margin-top: 4px;
  }

  .monitor-loading__row {
    height: 10px;
    border-radius: 999px;
    background: linear-gradient(90deg, rgba(74, 108, 247, 0.08) 0%, rgba(74, 108, 247, 0.18) 50%, rgba(74, 108, 247, 0.08) 100%);
    background-size: 200% 100%;
    animation: monitor-loading-shimmer 1.2s ease-in-out infinite;
  }

  @keyframes monitor-loading-spin {
    from {
      transform: rotate(0deg);
    }
    to {
      transform: rotate(360deg);
    }
  }

  @keyframes monitor-loading-shimmer {
    0% {
      background-position: 200% 0;
    }
    100% {
      background-position: -200% 0;
    }
  }
</style>
