<!-- 点位详情抽屉 -->
<template>
  <ElDrawer
    v-model="visible"
    title="点位详情"
    :size="400"
  >
    <ElDescriptions :column="1" border class="px-4">
      <ElDescriptionsItem label="所属设备">
        {{ pointData.deviceCode }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="点位名称">
        {{ pointData.pointName }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="点位序号">
        {{ pointData.varSeq ?? '-' }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="IO地址">
        {{ pointData.ioAddress }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="数据类型">
        {{ DATA_TYPE_MAP[pointData.dataType || ''] || pointData.dataType }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="驱动名称">
        {{ pointData.driverName }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="是否只读">
        <ElTag :type="pointData.isReadonly ? 'warning' : 'success'" size="small">
          {{ pointData.isReadonly ? '是' : '否' }}
        </ElTag>
      </ElDescriptionsItem>
      <ElDescriptionsItem label="创建时间">
        {{ pointData.createTime || '-' }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="更新时间">
        {{ pointData.updateTime || '-' }}
      </ElDescriptionsItem>
      <ElDescriptionsItem label="描述说明">
        {{ pointData.description || '-' }}
      </ElDescriptionsItem>
    </ElDescriptions>

    <template #footer>
      <ElButton @click="visible = false">关闭</ElButton>
    </template>
  </ElDrawer>
</template>

<script setup lang="ts">
  import { computed } from 'vue'

  interface Props {
    visible: boolean
    pointData: Partial<Api.DeviceMonitor.PointListItem>
  }

  interface Emits {
    (e: 'update:visible', value: boolean): void
  }

  const props = defineProps<Props>()
  const emit = defineEmits<Emits>()

  const visible = computed({
    get: () => props.visible,
    set: (val) => emit('update:visible', val)
  })

  const DATA_TYPE_MAP: Record<string, string> = {
    BOOL: '布尔型',
    INT: '整型(16位)',
    DINT: '整型(32位)',
    REAL: '浮点型',
    STRING: '字符串',
    WORD: '字(16位)',
    DWORD: '双字(32位)'
  }
</script>
