<!-- 设备弹窗组件 -->
<template>
  <ElDrawer
    v-model="visible"
    :title="title"
    :size="500"
    :before-close="handleClose"
  >
    <ElForm
      ref="formRef"
      :model="formData"
      :rules="rules"
      label-width="100px"
      class="px-4"
    >
      <ElFormItem label="设备类型" prop="deviceType">
        <ElSelect v-model="formData.deviceType" placeholder="请选择设备类型" class="w-full">
          <ElOption label="桥吊 (QC)" value="桥吊" />
          <ElOption label="轨道吊 (RTG/RMG)" value="轨道吊" />
          <ElOption label="AGV" value="AGV" />
          <ElOption label="堆高机" value="堆高机" />
          <ElOption label="其他设备" value="其他" />
        </ElSelect>
      </ElFormItem>

      <ElFormItem label="设备编号" prop="deviceCode">
        <ElInput v-model="formData.deviceCode" placeholder="请输入设备编号" />
      </ElFormItem>

      <ElFormItem label="设备名称" prop="deviceName">
        <ElInput v-model="formData.deviceName" placeholder="请输入设备名称" />
      </ElFormItem>

      <ElFormItem label="关联驱动" prop="driverName">
        <ElSelect v-model="formData.driverName" placeholder="请选择关联驱动" class="w-full">
          <ElOption
            v-for="item in driverOptions"
            :key="item.value"
            :label="item.label"
            :value="item.value"
          />
        </ElSelect>
      </ElFormItem>

      <ElFormItem label="设备状态" prop="deviceStatus">
        <ElRadioGroup v-model="formData.deviceStatus">
          <ElRadio value="enabled">启用</ElRadio>
          <ElRadio value="disabled">停用</ElRadio>
        </ElRadioGroup>
      </ElFormItem>

      <ElFormItem label="描述说明" prop="description">
        <ElInput
          v-model="formData.description"
          type="textarea"
          :rows="3"
          placeholder="请输入描述说明"
        />
      </ElFormItem>
    </ElForm>

    <template #footer>
      <ElButton @click="handleClose">取消</ElButton>
      <ElButton type="primary" @click="handleSubmit" :loading="submitting">确定</ElButton>
    </template>
  </ElDrawer>
</template>

<script setup lang="ts">
  import { ref, computed, watch } from 'vue'
  import type { FormInstance, FormRules } from 'element-plus'

  interface Props {
    visible: boolean
    type: 'add' | 'edit' | 'view'
    deviceData: Partial<Api.DeviceMonitor.DeviceListItem>
    driverOptions: { label: string; value: string }[]
  }

  interface Emits {
    (e: 'update:visible', value: boolean): void
    (e: 'submit', data: Api.DeviceMonitor.DeviceForm): void
  }

  const props = defineProps<Props>()
  const emit = defineEmits<Emits>()

  const formRef = ref<FormInstance>()
  const submitting = ref(false)

  const visible = computed({
    get: () => props.visible,
    set: (val) => emit('update:visible', val)
  })

  const title = computed(() => {
    const titles = {
      add: '新增设备',
      edit: '编辑设备',
      view: '设备详情'
    }
    return titles[props.type]
  })

  const formData = ref<Api.DeviceMonitor.DeviceForm>({
    deviceType: '',
    deviceCode: '',
    deviceName: '',
    driverName: '',
    deviceStatus: 'enabled',
    description: ''
  })

  const rules: FormRules = {
    deviceType: [{ required: true, message: '请选择设备类型', trigger: 'change' }],
    deviceCode: [
      { required: true, message: '请输入设备编号', trigger: 'blur' },
      { min: 2, max: 20, message: '长度在 2 到 20 个字符', trigger: 'blur' }
    ],
    deviceName: [
      { required: true, message: '请输入设备名称', trigger: 'blur' },
      { min: 2, max: 20, message: '长度在 2 到 20 个字符', trigger: 'blur' }
    ],
    driverName: [{ required: true, message: '请选择关联驱动', trigger: 'change' }]
  }

  // 监听弹窗打开
  watch(
    () => props.visible,
    (val) => {
      if (val && props.type !== 'add' && props.deviceData) {
        formData.value = {
          id: props.deviceData.id,
          deviceType: props.deviceData.deviceType || '',
          deviceCode: props.deviceData.deviceCode || '',
          deviceName: props.deviceData.deviceName || '',
          driverName: props.deviceData.driverName || '',
          deviceStatus: props.deviceData.deviceStatus || 'enabled',
          description: props.deviceData.description || ''
        }
      } else if (val && props.type === 'add') {
        formData.value = {
          deviceType: '',
          deviceCode: '',
          deviceName: '',
          driverName: '',
          deviceStatus: 'enabled',
          description: ''
        }
      }
    }
  )

  const handleClose = () => {
    formRef.value?.resetFields()
    visible.value = false
  }

  const handleSubmit = async () => {
    if (!formRef.value) return

    try {
      await formRef.value.validate()
      emit('submit', formData.value)
    } catch {
      // 表单验证失败
    }
  }
</script>
