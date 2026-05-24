# ArtSearchBar 搜索栏组件

一个功能强大、高度可配置的表单搜索组件，支持多种表单控件类型、动态显示隐藏、表单验证等特性。

## 特性

- **多种表单控件** - 支持输入框、选择器、日期选择器、级联选择器等 20+种表单控件
- **高度可配置** - 支持自定义布局、标签位置、间距等
- **响应式设计** - 自适应不同屏幕尺寸
- **插槽支持** - 支持自定义组件和插槽渲染
- **表单验证** - 完整的表单验证支持
- **动态控制** - 支持动态显示隐藏表单项

## 基础用法

最简单的搜索栏用法：

vue

```
<template>
  <ArtSearchBar
    v-model="formData"
    :items="formItems"
    @search="handleSearch"
    @reset="handleReset"
  />
</template>

<script setup>
const formData = ref({
  name: "",
  status: "",
});

const formItems = [
  {
    label: "用户名",
    key: "name",
    type: "input",
    placeholder: "请输入用户名",
  },
  {
    label: "状态",
    key: "status",
    type: "select",
    props: {
      options: [
        { label: "启用", value: "1" },
        { label: "禁用", value: "0" },
      ],
    },
  },
];

const handleSearch = () => {
  console.log("搜索参数:", formData.value);
};

const handleReset = () => {
  console.log("重置表单");
};
</script>
```

## 支持的表单控件类型

### 输入类控件

javascript

```
// 普通输入框
{
  label: '用户名',
  key: 'name',
  type: 'input',
  placeholder: '请输入用户名'
}

// 数字输入框
{
  label: '年龄',
  key: 'age',
  type: 'number',
  props: {
    min: 0,
    max: 120
  }
}

// 多行文本
{
  label: '备注',
  key: 'remark',
  type: 'input',
  props: {
    type: 'textarea',
    rows: 3
  }
}
```

### 选择类控件

javascript

```
// 下拉选择
{
  label: '状态',
  key: 'status',
  type: 'select',
  props: {
    options: [
      { label: '启用', value: '1' },
      { label: '禁用', value: '0' }
    ]
  }
}

// 级联选择器
{
  label: '地区',
  key: 'region',
  type: 'cascader',
  props: {
    options: cascaderOptions,
    props: { multiple: true }
  }
}

// 树选择器
{
  label: '部门',
  key: 'department',
  type: 'treeselect',
  props: {
    data: treeData,
    multiple: true,
    showCheckbox: true
  }
}
```

### 日期时间控件

javascript

```
// 日期选择
{
  label: '创建日期',
  key: 'createDate',
  type: 'datetime',
  props: {
    type: 'date',
    valueFormat: 'YYYY-MM-DD'
  }
}

// 日期范围
{
  label: '时间范围',
  key: 'dateRange',
  type: 'datetime',
  props: {
    type: 'daterange',
    rangeSeparator: '至',
    startPlaceholder: '开始日期',
    endPlaceholder: '结束日期'
  }
}

// 时间选择器
{
  label: '时间',
  key: 'time',
  type: 'timepicker',
  props: {
    valueFormat: 'HH:mm:ss'
  }
}
```

### 其他控件

javascript

```
// 开关
{
  label: '是否启用',
  key: 'enabled',
  type: 'switch'
}

// 单选框组
{
  label: '性别',
  key: 'gender',
  type: 'radiogroup',
  props: {
    options: [
      { label: '男', value: '1' },
      { label: '女', value: '2' }
    ]
  }
}

// 复选框组
{
  label: '兴趣爱好',
  key: 'hobbies',
  type: 'checkboxgroup',
  props: {
    options: [
      { label: '读书', value: 'reading' },
      { label: '运动', value: 'sports' }
    ]
  }
}

// 评分
{
  label: '评分',
  key: 'rating',
  type: 'rate'
}

// 滑块
{
  label: '价格区间',
  key: 'priceRange',
  type: 'slider',
  props: {
    range: true,
    max: 1000
  }
}
```

## 自定义组件

### 使用渲染函数

javascript

```
import { h } from 'vue'
import CustomComponent from './CustomComponent.vue'

{
  label: '自定义组件',
  key: 'custom',
  type: () => h(CustomComponent, {
    prop1: 'value1',
    onCustomEvent: handleCustomEvent
  })
}
```

### 使用插槽

vue

```
<template>
  <ArtSearchBar v-model="formData" :items="formItems">
    <template #customSlot="{ item, modelValue }">
      <el-input
        v-model="modelValue[item.key]"
        placeholder="我是插槽渲染的组件"
      />
    </template>
  </ArtSearchBar>
</template>

<script setup>
const formItems = [
  {
    label: "自定义插槽",
    key: "customSlot",
    type: "input", // 这里的type会被插槽覆盖
  },
];
</script>
```

## 表单验证

vue

```
<template>
  <ArtSearchBar
    ref="searchBarRef"
    v-model="formData"
    :items="formItems"
    :rules="rules"
    @search="handleSearch"
  />
</template>

<script setup>
const searchBarRef = ref();

const rules = {
  name: [{ required: true, message: "请输入用户名", trigger: "blur" }],
  phone: [
    { required: true, message: "请输入手机号", trigger: "blur" },
    {
      pattern: /^1[3456789]\d{9}$/,
      message: "请输入正确的手机号",
      trigger: "blur",
    },
  ],
};

const handleSearch = async () => {
  try {
    await searchBarRef.value.validate();
    console.log("验证通过，执行搜索");
  } catch (error) {
    console.log("验证失败");
  }
};
</script>
```

## 动态控制

### 动态显示隐藏

javascript

```
const formItems = computed(() => [
  {
    label: "用户名",
    key: "name",
    type: "input",
  },
  {
    label: "高级选项",
    key: "advanced",
    type: "input",
    hidden: !showAdvanced.value, // 动态控制显示隐藏
  },
]);
```

### 动态更新配置

javascript

```
const userNameItem = ref({
  label: "用户名",
  key: "name",
  type: "input",
  placeholder: "请输入用户名",
});

// 动态修改配置
const updateUserNameConfig = () => {
  userNameItem.value = {
    ...userNameItem.value,
    label: "昵称",
    placeholder: "请输入昵称",
  };
};
```

## 布局配置

### 栅格布局

vue

```
<ArtSearchBar v-model="formData" :items="formItems" :span="8" :gutter="16" />
```

### 标签配置

vue

```
<ArtSearchBar
  v-model="formData"
  :items="formItems"
  label-position="top"
  :label-width="120"
/>
```

### 响应式布局

组件会自动适配不同屏幕尺寸：

- 移动端：每行显示 1 个表单项
- 平板：每行显示 2 个表单项
- 桌面端：根据 `span` 属性控制每行显示的表单项数量

## API

### Props

| 参数            | 说明                   | 类型                       | 默认值    |
| :-------------- | :--------------------- | :------------------------- | :-------- |
| modelValue      | 表单数据对象           | `Record<string, any>`      | `{}`      |
| items           | 表单项配置数组         | `SearchFormItem[]`         | `[]`      |
| span            | 每个表单项占据的栅格数 | `number`                   | `6`       |
| gutter          | 栅格间隔               | `number`                   | `12`      |
| labelPosition   | 标签位置               | `'left' | 'right' | 'top'` | `'right'` |
| labelWidth      | 标签宽度               | `string | number`          | `'70px'`  |
| defaultExpanded | 默认是否展开           | `boolean`                  | `false`   |
| showExpand      | 是否显示展开收起按钮   | `boolean`                  | `true`    |
| showReset       | 是否显示重置按钮       | `boolean`                  | `true`    |
| showSearch      | 是否显示搜索按钮       | `boolean`                  | `true`    |
| disabledSearch  | 是否禁用搜索按钮       | `boolean`                  | `false`   |

### SearchFormItem 配置

| 参数        | 说明             | 类型                        | 默认值    |
| :---------- | :--------------- | :-------------------------- | :-------- |
| key         | 表单项唯一标识   | `string`                    | -         |
| label       | 标签文本         | `string`                    | -         |
| type        | 表单项类型       | `string | (() => VNode)`    | `'input'` |
| hidden      | 是否隐藏         | `boolean`                   | `false`   |
| span        | 栅格占位格数     | `number`                    | -         |
| labelWidth  | 标签宽度         | `string | number`           | -         |
| placeholder | 占位符           | `string`                    | -         |
| props       | 传递给组件的属性 | `Record<string, any>`       | -         |
| slots       | 插槽配置         | `Record<string, () => any>` | -         |

### Events

| 事件名 | 说明               | 参数 |
| :----- | :----------------- | :--- |
| search | 点击搜索按钮时触发 | -    |
| reset  | 点击重置按钮时触发 | -    |

### Methods

| 方法名   | 说明     | 参数                     |
| :------- | :------- | :----------------------- |
| validate | 验证表单 | `() => Promise<boolean>` |
| reset    | 重置表单 | `() => void`             |

### Slots

| 插槽名 | 说明             | 参数                                                        |
| :----- | :--------------- | :---------------------------------------------------------- |
| [key]  | 自定义表单项内容 | `{ item: SearchFormItem, modelValue: Record<string, any> }` |

## 完整示例

vue

```
<template>
  <div class="search-example">
    <ArtSearchBar
      ref="searchBarRef"
      v-model="formData"
      :items="formItems"
      :rules="rules"
      :defaultExpanded="true"
      :labelWidth="100"
      labelPosition="right"
      :span="6"
      :gutter="16"
      @search="handleSearch"
      @reset="handleReset"
    >
      <template #customSlot>
        <el-input
          v-model="formData.customSlot"
          placeholder="我是插槽渲染的组件"
        />
      </template>
    </ArtSearchBar>

    <div class="result">
      <h3>搜索结果：</h3>
      <pre>{{ JSON.stringify(formData, null, 2) }}</pre>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from "vue";

const searchBarRef = ref();

const formData = ref({
  name: "",
  phone: "",
  status: "",
  dateRange: [],
  customSlot: "",
});

const rules = {
  name: [{ required: true, message: "请输入用户名", trigger: "blur" }],
  phone: [
    { required: true, message: "请输入手机号", trigger: "blur" },
    {
      pattern: /^1[3456789]\d{9}$/,
      message: "请输入正确的手机号",
      trigger: "blur",
    },
  ],
};

const formItems = [
  {
    label: "用户名",
    key: "name",
    type: "input",
    placeholder: "请输入用户名",
    props: { clearable: true },
  },
  {
    label: "手机号",
    key: "phone",
    type: "input",
    placeholder: "请输入手机号",
    props: { maxlength: 11 },
  },
  {
    label: "状态",
    key: "status",
    type: "select",
    props: {
      placeholder: "请选择状态",
      options: [
        { label: "启用", value: "1" },
        { label: "禁用", value: "0" },
      ],
    },
  },
  {
    label: "日期范围",
    key: "dateRange",
    type: "datetime",
    props: {
      type: "daterange",
      rangeSeparator: "至",
      startPlaceholder: "开始日期",
      endPlaceholder: "结束日期",
      valueFormat: "YYYY-MM-DD",
    },
  },
  {
    label: "自定义插槽",
    key: "customSlot",
    type: "input",
  },
];

const handleSearch = async () => {
  try {
    await searchBarRef.value.validate();
    console.log("搜索参数:", formData.value);
    // 执行搜索逻辑
  } catch (error) {
    console.log("表单验证失败");
  }
};

const handleReset = () => {
  console.log("重置表单");
};
</script>

<style scoped>
.search-example {
  padding: 20px;
}

.result {
  margin-top: 20px;
  padding: 16px;
  background-color: #f5f5f5;
  border-radius: 4px;
}

.result pre {
  margin: 0;
  font-size: 12px;
}
</style>
```

## 注意事项

1. **表单项 key 值必须唯一**，用于表单数据绑定和验证
2. **props 属性会直接传递给对应的 Element Plus 组件**，请参考 Element Plus 官方文档
3. **表单验证规则格式与 Element Plus Form 组件一致**