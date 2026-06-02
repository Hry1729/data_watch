<!-- 点位编辑弹窗 -->
<template>
  <ElDialog v-model="visible" :title="title" width="520px" :before-close="handleClose">
    <ElForm ref="formRef" :model="formData" :rules="rules" label-width="100px" class="px-4">
      <ElFormItem label="所属设备" prop="deviceCode">
        <ElTreeSelect
          v-model="selectedDeviceKey"
          :data="treeSelectData"
          :props="treeSelectProps"
          placeholder="请选择所属设备"
          class="w-full"
          check-strictly
          :render-after-expand="false"
          @change="handleDeviceChange"
        />
      </ElFormItem>

      <ElFormItem label="点位名称" prop="pointName">
        <ElInput v-model="formData.pointName" placeholder="请输入点位名称" />
      </ElFormItem>

      <ElFormItem label="点位序号" prop="varSeq">
        <ElInputNumber v-model="formData.varSeq" :min="1" :step="1" class="w-full" />
      </ElFormItem>

      <ElFormItem label="IO地址" prop="ioAddress">
        <ElInput v-model="formData.ioAddress" placeholder="请输入 IO 地址，如 DB1.DBX0.0" />
      </ElFormItem>

      <ElFormItem label="数据类型" prop="dataType">
        <ElSelect v-model="formData.dataType" placeholder="请选择数据类型" class="w-full">
          <ElOption label="布尔型 (BOOL)" value="BOOL" />
          <ElOption label="整型16位 (INT)" value="INT" />
          <ElOption label="整型32位 (DINT)" value="DINT" />
          <ElOption label="浮点型 (REAL)" value="REAL" />
          <ElOption label="字符串 (STRING)" value="STRING" />
          <ElOption label="字16位 (WORD)" value="WORD" />
          <ElOption label="双字32位 (DWORD)" value="DWORD" />
        </ElSelect>
      </ElFormItem>

      <ElFormItem label="驱动名称" prop="driverName">
        <ElSelect v-model="formData.driverName" placeholder="请选择驱动" class="w-full">
          <ElOption v-for="item in driverOptions" :key="item.value" :label="item.label" :value="item.value" />
        </ElSelect>
      </ElFormItem>

      <ElFormItem label="是否只读" prop="isReadonly">
        <ElRadioGroup v-model="formData.isReadonly">
          <ElRadio :value="true">是</ElRadio>
          <ElRadio :value="false">否</ElRadio>
        </ElRadioGroup>
      </ElFormItem>

      <ElFormItem label="描述说明" prop="description">
        <ElInput v-model="formData.description" type="textarea" :rows="3" placeholder="请输入描述说明" />
      </ElFormItem>
    </ElForm>

    <template #footer>
      <ElButton @click="handleClose">取消</ElButton>
      <ElButton type="primary" @click="handleSubmit" :loading="submitting">确定</ElButton>
    </template>
  </ElDialog>
</template>

<script setup lang="ts">
  import { computed, ref, watch } from 'vue'
  import type { FormInstance, FormRules } from 'element-plus'

  interface TreeNode {
    key: string
    label: string
    type: 'group' | 'device'
    deviceId?: number
    deviceCode?: string
    children?: TreeNode[]
  }

  interface Props {
    visible: boolean
    type: 'add' | 'edit' | 'view'
    pointData: Partial<Api.DeviceMonitor.PointListItem>
    deviceTree: TreeNode[]
    driverOptions: { label: string; value: string }[]
  }

  interface Emits {
    (e: 'update:visible', value: boolean): void
    (e: 'submit', data: Api.DeviceMonitor.PointForm): void
  }

  const props = defineProps<Props>()
  const emit = defineEmits<Emits>()

  const formRef = ref<FormInstance>()
  const submitting = ref(false)
  const selectedDeviceKey = ref('')

  const visible = computed({
    get: () => props.visible,
    set: (val) => emit('update:visible', val)
  })

  const title = computed(() => {
    const titles = {
      add: '新增点位',
      edit: '编辑点位',
      view: '点位详情'
    }
    return titles[props.type]
  })

  const formData = ref<Api.DeviceMonitor.PointForm>({
    deviceId: 0,
    deviceCode: '',
    varSeq: 1,
    pointName: '',
    ioAddress: '',
    dataType: 'INT',
    description: '',
    isReadonly: true,
    driverName: ''
  })

  const rules: FormRules = {
    deviceCode: [{ required: true, message: '请选择所属设备', trigger: 'change' }],
    varSeq: [{ required: true, message: '请输入点位序号', trigger: 'change' }],
    pointName: [
      { required: true, message: '请输入点位名称', trigger: 'blur' },
      { min: 2, max: 50, message: '长度在 2 到 50 个字符', trigger: 'blur' }
    ],
    ioAddress: [
      { required: true, message: '请输入 IO 地址', trigger: 'blur' },
      { min: 1, max: 20, message: '长度在 1 到 20 个字符', trigger: 'blur' }
    ],
    dataType: [{ required: true, message: '请选择数据类型', trigger: 'change' }],
    driverName: [{ required: true, message: '请选择驱动', trigger: 'change' }]
  }

  const flatDeviceMap = computed(() => {
    const map = new Map<string, { deviceId: number; deviceCode: string }>()

    const walk = (nodes: TreeNode[]) => {
      nodes.forEach((node) => {
        if (node.type === 'device' && node.deviceCode && node.deviceId) {
          map.set(node.key, {
            deviceId: node.deviceId,
            deviceCode: node.deviceCode
          })
        }
        if (node.children?.length) walk(node.children)
      })
    }

    walk(props.deviceTree)
    return map
  })

  const treeSelectData = computed(() => {
    const mapNode = (node: TreeNode): Record<string, any> => ({
      key: node.key,
      label: node.label,
      disabled: node.type === 'group',
      children: node.children?.map(mapNode)
    })

    return props.deviceTree.map(mapNode)
  })

  const treeSelectProps = {
    children: 'children',
    label: 'label',
    value: 'key',
    disabled: 'disabled'
  }

  const syncSelectedDevice = () => {
    const targetEntry = Array.from(flatDeviceMap.value.entries()).find(
      ([, value]) => value.deviceCode === formData.value.deviceCode
    )
    selectedDeviceKey.value = targetEntry?.[0] || ''
  }

  const handleDeviceChange = (value: string) => {
    const device = flatDeviceMap.value.get(value)
    if (!device) {
      formData.value.deviceId = 0
      formData.value.deviceCode = ''
      return
    }

    formData.value.deviceId = device.deviceId
    formData.value.deviceCode = device.deviceCode
  }

  watch(
    () => props.visible,
    (val) => {
      if (!val) return

      if (props.type !== 'add' && props.pointData) {
        formData.value = {
          id: props.pointData.id,
          locatorDeviceId: props.pointData.deviceId || 0,
          locatorVarSeq: props.pointData.varSeq || 1,
          deviceId: props.pointData.deviceId || 0,
          deviceCode: props.pointData.deviceCode || '',
          varSeq: props.pointData.varSeq || 1,
          pointName: props.pointData.pointName || '',
          ioAddress: props.pointData.ioAddress || '',
          dataType: props.pointData.dataType || 'INT',
          description: props.pointData.description || '',
          isReadonly: props.pointData.isReadonly ?? true,
          driverName: props.pointData.driverName || ''
        }
      } else {
        formData.value = {
          deviceId: props.pointData.deviceId || 0,
          deviceCode: props.pointData.deviceCode || '',
          varSeq: 1,
          pointName: '',
          ioAddress: '',
          dataType: 'INT',
          description: '',
          isReadonly: true,
          driverName: ''
        }
      }

      syncSelectedDevice()
    }
  )

  watch(
    () => props.deviceTree,
    () => {
      syncSelectedDevice()
    },
    { deep: true }
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
