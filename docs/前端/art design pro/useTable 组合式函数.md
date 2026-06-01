# useTable 组合式函数

`useTable` 是一个功能强大的 Vue 3 组合式函数，专为现代 Web 应用的表格数据管理而设计。它提供了完整的表格解决方案，包括数据获取、智能缓存、分页控制、搜索功能和多种刷新策略。

## 特性

- **智能缓存** - 基于 LRU 算法的高效缓存机制
- **防抖搜索** - 内置防抖功能，优化搜索体验
- **灵活分页** - 完整的分页控制和状态管理
- **多种刷新策略** - 针对不同业务场景的智能刷新
- **错误处理** - 完善的错误处理和恢复机制

## 快速开始

### 基础用法

vue

```
<template>
  <div>
    <!-- 表格组件 -->
    <ArtTable
      :loading="loading"
      :data="data"
      :columns="columns"
      :pagination="pagination"
      @pagination:size-change="handleSizeChange"
      @pagination:current-change="handleCurrentChange"
    />
  </div>
</template>

<script setup lang="ts">
import { useTable } from "@/composables/useTable";
import { fetchGetUserList } from "@/api/system-manage";

const {
  data,
  loading,
  columns,
  pagination,
  handleSizeChange,
  handleCurrentChange,
} = useTable({
  core: {
    apiFn: fetchGetUserList,
    apiParams: {
      current: 1,
      size: 20,
    },
    columnsFactory: () => [
      { prop: "id", label: "ID" },
      { prop: "name", label: "姓名" },
      { prop: "email", label: "邮箱" },
    ],
  },
});
</script>
```

### 进阶用法

vue

```
<script setup lang="ts">
import { useTable, CacheInvalidationStrategy } from "@/composables/useTable";

const {
  // 数据相关
  data,
  loading,
  error,
  hasData,

  // 分页相关
  pagination,
  handleSizeChange,
  handleCurrentChange,

  // 搜索相关
  searchParams,
  resetSearchParams,

  // 数据操作
  getData,
  getDataDebounced,
  clearData,

  // 刷新策略
  refreshData,
  refreshSoft,
  refreshCreate,
  refreshUpdate,
  refreshRemove,

  // 缓存控制
  cacheInfo,
  clearCache,
  clearExpiredCache,

  // 列配置
  columns,
  columnChecks,
  addColumn,
  removeColumn,
  toggleColumn,
} = useTable<UserListItem>({
  // 核心配置
  core: {
    apiFn: UserService.fetchGetUserList,
    apiParams: {
      current: 1,
      size: 20,
      name: "",
      status: "",
    },
    excludeParams: ["daterange"],
    immediate: true,
    columnsFactory: () => [
      { prop: "name", label: "姓名", sortable: true },
      { prop: "email", label: "邮箱" },
      { prop: "status", label: "状态", useSlot: true },
    ],
  },

  // 数据处理
  transform: {
    dataTransformer: (records) => {
      return records.map((item) => ({
        ...item,
        statusText: item.status === 1 ? "激活" : "禁用",
      }));
    },
  },

  // 性能优化
  performance: {
    enableCache: true,
    cacheTime: 5 * 60 * 1000, // 5分钟
    debounceTime: 300,
    maxCacheSize: 100,
  },

  // 生命周期钩子
  hooks: {
    onSuccess: (data, response) => {
      console.log("数据加载成功:", data.length);
    },
    onError: (error) => {
      console.error("加载失败:", error.message);
    },
    onCacheHit: (data, response) => {
      console.log("缓存命中:", data.length);
    },
  },
});

// 搜索功能
const handleSearch = () => {
  Object.assign(searchParams, {
    name: "John",
    status: 1,
  });
  getData();
};

// CRUD 操作后的刷新
const handleAdd = () => {
  // 新增后回到第一页
  refreshCreate();
};

const handleEdit = () => {
  // 编辑后保持当前页
  refreshUpdate();
};

const handleDelete = () => {
  // 删除后智能处理页码
  refreshRemove();
};
</script>
```

## API 参考

### 配置选项

#### core (核心配置)

| 参数             | 类型       | 默认值                               | 说明                   |
| :--------------- | :--------- | :----------------------------------- | :--------------------- |
| `apiFn`          | `Function` | -                                    | **必需**，API 请求函数 |
| `apiParams`      | `Object`   | `{}`                                 | 默认请求参数           |
| `excludeParams`  | `Array`    | `[]`                                 | 排除的参数字段         |
| `immediate`      | `Boolean`  | `true`                               | 是否立即加载数据       |
| `columnsFactory` | `Function` | -                                    | 列配置工厂函数         |
| `paginationKey`  | `Object`   | `{current: 'current', size: 'size'}` | 分页字段映射           |

#### transform (数据处理)

| 参数              | 类型       | 默认值                   | 说明           |
| :---------------- | :--------- | :----------------------- | :------------- |
| `dataTransformer` | `Function` | -                        | 数据转换函数   |
| `responseAdapter` | `Function` | `defaultResponseAdapter` | 响应数据适配器 |

#### performance (性能优化)

| 参数           | 类型      | 默认值   | 说明             |
| :------------- | :-------- | :------- | :--------------- |
| `enableCache`  | `Boolean` | `false`  | 是否启用缓存     |
| `cacheTime`    | `Number`  | `300000` | 缓存时间（毫秒） |
| `debounceTime` | `Number`  | `300`    | 防抖延迟（毫秒） |
| `maxCacheSize` | `Number`  | `50`     | 最大缓存条数     |

#### hooks (生命周期钩子)

| 参数                | 类型       | 说明             |
| :------------------ | :--------- | :--------------- |
| `onSuccess`         | `Function` | 数据加载成功回调 |
| `onError`           | `Function` | 错误处理回调     |
| `onCacheHit`        | `Function` | 缓存命中回调     |
| `resetFormCallback` | `Function` | 重置表单回调     |

### 返回值

#### 数据相关

| 属性      | 类型                               | 说明         |
| :-------- | :--------------------------------- | :----------- |
| `data`    | `Ref<T[]>`                         | 表格数据     |
| `loading` | `Readonly<Ref<boolean>>`           | 加载状态     |
| `error`   | `Readonly<Ref<TableError | null>>` | 错误状态     |
| `hasData` | `ComputedRef<boolean>`             | 是否有数据   |
| `isEmpty` | `ComputedRef<boolean>`             | 数据是否为空 |

#### 分页相关

| 属性                  | 类型                                   | 说明             |
| :-------------------- | :------------------------------------- | :--------------- |
| `pagination`          | `Readonly<Reactive<PaginationParams>>` | 分页状态         |
| `handleSizeChange`    | `Function`                             | 页面大小变化处理 |
| `handleCurrentChange` | `Function`                             | 当前页变化处理   |

#### 搜索相关

| 属性                | 类型          | 说明         |
| :------------------ | :------------ | :----------- |
| `searchParams`      | `Reactive<P>` | 搜索参数     |
| `resetSearchParams` | `Function`    | 重置搜索参数 |

#### 数据操作

| 方法               | 说明                                        |
| :----------------- | :------------------------------------------ |
| `fetchData`        | 手动加载数据，需要配合 `immediate` 选项使用 |
| `getData`          | 获取数据（重置到第一页）                    |
| `getDataDebounced` | 获取数据（防抖）                            |
| `clearData`        | 清空数据                                    |

#### 刷新策略

| 方法            | 使用场景     | 说明                           |
| :-------------- | :----------- | :----------------------------- |
| `refreshData`   | 手动刷新按钮 | 清空所有缓存，重新获取数据     |
| `refreshSoft`   | 定时刷新     | 仅清空当前搜索条件的缓存       |
| `refreshCreate` | 新增数据后   | 回到第一页并清空分页缓存       |
| `refreshUpdate` | 更新数据后   | 保持当前页，仅清空当前搜索缓存 |
| `refreshRemove` | 删除数据后   | 智能处理页码，避免空页面       |

#### 缓存控制

| 属性/方法           | 说明                     |
| :------------------ | :----------------------- |
| `cacheInfo`         | 缓存统计信息             |
| `clearCache`        | 清除缓存（支持多种策略） |
| `clearExpiredCache` | 清理过期缓存             |

#### 列配置（可选）

| 方法           | 说明           |
| :------------- | :------------- |
| `columns`      | 表格列配置     |
| `columnChecks` | 列显示控制     |
| `addColumn`    | 新增列         |
| `removeColumn` | 删除列         |
| `toggleColumn` | 切换列显示状态 |
| `updateColumn` | 更新列配置     |
| `resetColumns` | 重置列配置     |

## 使用场景

### 1. 基础表格

适用于简单的数据展示场景：

typescript

```
const { data, loading, pagination } = useTable({
  core: {
    apiFn: fetchGetUserList,
    columnsFactory: () => basicColumns,
  },
});
```

### 2. 搜索表格

带搜索功能的表格：

typescript

```
const { searchParams, getData, resetSearchParams } = useTable({
  core: {
    apiFn: fetchGetUserList,
    apiParams: { name: "", status: "" },
  },
  performance: {
    debounceTime: 500, // 搜索防抖
  },
});

// 搜索
const handleSearch = () => {
  Object.assign(searchParams, formData);
  getData();
};
```

### 3. 高性能表格

启用缓存的高性能表格：

typescript

```
const { cacheInfo, clearCache } = useTable({
  core: {
    apiFn: fetchGetUserList,
  },
  performance: {
    enableCache: true,
    cacheTime: 10 * 60 * 1000, // 10分钟缓存
    maxCacheSize: 200,
  },
  hooks: {
    onCacheHit: (data) => {
      console.log("从缓存获取数据:", data.length);
    },
  },
});
```

### 4. CRUD 表格

完整的增删改查表格：

typescript

```
const { refreshCreate, refreshUpdate, refreshRemove } = useTable({
  core: {
    apiFn: fetchGetUserList,
  },
});

// 新增用户后
const handleAddUser = async () => {
  await addUser(userData);
  refreshCreate(); // 回到第一页
};

// 编辑用户后
const handleEditUser = async () => {
  await updateUser(userData);
  refreshUpdate(); // 保持当前页
};

// 删除用户后
const handleDeleteUser = async () => {
  await deleteUser(userId);
  refreshRemove(); // 智能处理页码
};
```

## 高级功能

### 缓存策略

`useTable` 提供了四种缓存清理策略：

typescript

```
import { CacheInvalidationStrategy } from "@/composables/useTable";

// 清空所有缓存
clearCache(CacheInvalidationStrategy.CLEAR_ALL, "手动刷新");

// 只清空当前搜索条件的缓存
clearCache(CacheInvalidationStrategy.CLEAR_CURRENT, "搜索数据");

// 清空分页相关缓存
clearCache(CacheInvalidationStrategy.CLEAR_PAGINATION, "新增数据");

// 不清理任何缓存
clearCache(CacheInvalidationStrategy.KEEP_ALL, "保持缓存");
```

### 自定义响应适配器

处理不同的后端响应格式：

typescript

```
const { data } = useTable({
  core: {
    apiFn: fetchGetUserList,
  },
  transform: {
    responseAdapter: (response) => {
      // 适配自定义响应格式
      return {
        records: response.list,
        total: response.totalCount,
        current: response.pageNum,
        size: response.pageSize,
      };
    },
  },
});
```

### 数据转换

对获取的数据进行转换：

typescript

```
const { data } = useTable({
  core: {
    apiFn: fetchGetUserList,
  },
  transform: {
    dataTransformer: (records) => {
      return records.map((item) => ({
        ...item,
        fullName: `${item.firstName} ${item.lastName}`,
        statusText: item.status === 1 ? "激活" : "禁用",
      }));
    },
  },
});
```

### 动态列配置

运行时动态管理表格列：

typescript

```
const { addColumn, removeColumn, toggleColumn, updateColumn } = useTable({
  core: {
    apiFn: fetchGetUserList,
    columnsFactory: () => initialColumns,
  },
});

// 新增列
addColumn({
  prop: "remark",
  label: "备注",
  width: 150,
});

// 删除列
removeColumn("status");

removeColumn(["status", "score"]);

// 切换列显示
toggleColumn("phone");

// 更新列配置
updateColumn("name", {
  label: "用户姓名",
  width: 200,
});
```

## 错误处理

`useTable` 内置了完善的错误处理机制：

typescript

```
const { error } = useTable({
  core: {
    apiFn: fetchGetUserList,
  },
  hooks: {
    onError: (error) => {
      // 自定义错误处理
      if (error.code === "NETWORK_ERROR") {
        ElMessage.error("网络连接失败，请检查网络");
      } else {
        ElMessage.error(error.message);
      }
    },
  },
});

// 在模板中显示错误
// <div v-if="error" class="error">{{ error.message }}</div>
```

## 调试功能

启用调试模式查看详细日志：

typescript

```
const { cacheInfo } = useTable({
  core: {
    apiFn: fetchGetUserList,
  },
  debug: {
    enableLog: true,
    logLevel: "info",
  },
});

// 查看缓存统计
console.log("缓存信息:", cacheInfo.value);
```

## 最佳实践

### 1. 合理使用缓存

typescript

```
// ✅ 推荐：为频繁访问的数据启用缓存
const { data } = useTable({
  performance: {
    enableCache: true,
    cacheTime: 5 * 60 * 1000, // 5分钟
  },
});

// ❌ 不推荐：为实时性要求高的数据启用缓存
```

### 2. 选择合适的刷新策略

typescript

```
// ✅ 新增数据后使用 refreshCreate
const handleAdd = () => {
  refreshCreate(); // 回到第一页
};

// ✅ 编辑数据后使用 refreshUpdate
const handleEdit = () => {
  refreshUpdate(); // 保持当前页
};

// ✅ 删除数据后使用 refreshRemove
const handleDelete = () => {
  refreshRemove(); // 智能处理页码
};
```

### 3. 优化搜索体验

typescript

```
// ✅ 使用防抖搜索
const { getDataDebounced } = useTable({
  performance: {
    debounceTime: 300,
  },
});

const handleSearch = () => {
  getDataDebounced(); // 自动防抖
};
```

### 4. 错误处理

typescript

```
// ✅ 提供友好的错误提示
const { error } = useTable({
  hooks: {
    onError: (error) => {
      ElMessage.error(error.message || "数据加载失败");
    },
  },
});
```